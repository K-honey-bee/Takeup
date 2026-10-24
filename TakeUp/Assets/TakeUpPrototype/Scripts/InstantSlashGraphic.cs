using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    // Procedural fallback VFX: screen-space blades remain sharp at any output resolution.
    public sealed class InstantSlashGraphic : MaskableGraphic
    {
        public SurvivalGame game;
        Vector2 Point(Vector2 normalized) => rectTransform.rect.min + Vector2.Scale(normalized, rectTransform.rect.size);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (game == null) return;
            var rect = rectTransform.rect;
            if (!game.InstantResolved)
            {
                Quad(mesh, rect.min, new Vector2(rect.xMax, rect.yMin), rect.max, new Vector2(rect.xMin, rect.yMax), Color.black);
                foreach (var target in game.InstantTargets)
                {
                    Vector2 p = Point(target.position);
                    float radius = rect.height * game.settings.instantSlash.targetRadius;
                    if (target.sliced)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            Vector2 d = new Vector2(Mathf.Cos(i * Mathf.PI / 3), Mathf.Sin(i * Mathf.PI / 3));
                            Line(mesh, p + d * radius * .8f, p + d * radius * 1.35f, 2, new Color(1, 1, 1, .28f));
                        }
                        continue;
                    }
                    Line(mesh, p + Vector2.up * radius, p + Vector2.up * radius * 3, radius * .12f, new Color(1, 1, 1, .28f));
                    if (game.settings.instantSlash.fallingSprite == null)
                    {
                        Diamond(mesh, p, radius, Color.white);
                        Diamond(mesh, p, radius * .65f, Color.black);
                        Line(mesh, p + new Vector2(-radius * .28f, 0), p + new Vector2(radius * .28f, 0), 3, Color.white);
                    }
                }
                foreach (var trail in game.InstantTrails)
                {
                    Line(mesh, Point(trail.from), Point(trail.to), 10, new Color(.3f, .3f, .3f, trail.remaining / .18f));
                    Line(mesh, Point(trail.from), Point(trail.to), 3, new Color(1, 1, 1, trail.remaining / .18f));
                }
                return;
            }
            if (!game.InstantSucceeded)
            {
                float alpha = Mathf.Clamp01(1 - game.InstantReleaseTime / .55f);
                Quad(mesh, rect.min, new Vector2(rect.xMax, rect.yMin), rect.max, new Vector2(rect.xMin, rect.yMax), new Color(0, 0, 0, alpha));
                return;
            }
            // Short local impact accents, with alternating black/white blade cores over a monochrome world.
            int latest = Mathf.Min(5, Mathf.FloorToInt(game.InstantReleaseTime / .14f));
            for (int burst = Mathf.Max(0, latest - 2); burst <= latest; burst++)
            {
                float age = game.InstantReleaseTime - burst * .14f;
                float alpha = Mathf.Clamp01(1 - age / .42f);
                float length = Mathf.Clamp01(age / .09f);
                bool reverse = burst % 2 != 0;
                float baseline = .22f + burst * .105f;
                Vector2 previous = Point(new Vector2(reverse ? 1.04f : -.04f, baseline));
                for (int i = 1; i <= 22; i++)
                {
                    float f = Mathf.Min(i / 22f, length);
                    float x = reverse ? 1.04f - f * 1.08f : -.04f + f * 1.08f;
                    float jitter = (((i * 73 + burst * 41) % 17) / 8f - 1) * .026f;
                    Vector2 next = Point(new Vector2(x, baseline + jitter + (f - .5f) * (reverse ? -.13f : .13f)));
                    Line(mesh, previous, next, rect.height * .019f, new Color(0, 0, 0, alpha));
                    Line(mesh, previous, next, rect.height * .006f, new Color(1, 1, 1, alpha));
                    if (i % 4 == 0)
                        Line(mesh, next, next + new Vector2(rect.width * .025f, rect.height * .055f), 2, new Color(1, 1, 1, alpha));
                    previous = next;
                    if (f >= length) break;
                }
            }
        }

        static void Diamond(VertexHelper mesh, Vector2 p, float size, Color color)
            => Quad(mesh, p + Vector2.left * size, p + Vector2.down * size, p + Vector2.right * size, p + Vector2.up * size, color);

        static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * .5f;
            Quad(mesh, from - normal, to - normal, to + normal, from + normal, color);
        }

        static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero); mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}

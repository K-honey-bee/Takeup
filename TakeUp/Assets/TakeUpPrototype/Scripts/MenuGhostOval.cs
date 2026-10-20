using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    // Vector UI shapes stay clean at any Canvas resolution.
    public sealed class MenuGhostOval : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            mesh.AddVert(center, color, new Vector2(.5f, .5f));
            const int segments = 64;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 unit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + Vector2.Scale(unit, rect.size * .5f), color, unit * .5f + Vector2.one * .5f);
                if (i > 0) mesh.AddTriangle(0, i, i + 1);
            }
        }
    }
}

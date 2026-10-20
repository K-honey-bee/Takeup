using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        sealed class HitParticle
        {
            public SpriteRenderer visual;
            public Vector2 velocity;
            public float age, duration, gravity, spin;
            public Vector3 scale;
            public bool flash;
        }

        readonly List<HitParticle> hitParticles = new List<HitParticle>();

        void SpawnHitEffect(Vector2 position, Vector2 direction, int layer = 0, int order = 30)
        {
            var config = settings.hitEffect;
            if (config == null) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            AddHitParticle(config.flashSprite, position, config.flashSize, config.flashDuration,
                Vector2.zero, 0, 0, angle, true, layer, order);
            for (int i = 0; i < 3; i++)
            {
                Sprite sprite = i == 0 ? config.fragment1 : i == 1 ? config.fragment2 : config.fragment3;
                // A single assigned fragment can also supply all three particles.
                if (sprite == null) sprite = config.fragment1 ?? config.fragment2 ?? config.fragment3;
                float spread = angle + 60 + i * 120 + Random.Range(-15f, 15f);
                Vector2 velocity = new Vector2(Mathf.Cos(spread * Mathf.Deg2Rad), Mathf.Sin(spread * Mathf.Deg2Rad))
                    * config.fragmentSpeed * Random.Range(.8f, 1.2f);
                AddHitParticle(sprite, position, config.fragmentSize, config.fragmentDuration,
                    velocity, config.fragmentGravity, config.fragmentSpin * (i % 2 == 0 ? 1 : -1),
                    spread, false, layer, order);
            }
        }

        void AddHitParticle(Sprite sprite, Vector2 position, float size, float duration,
            Vector2 velocity, float gravity, float spin, float angle, bool flash, int layer, int order)
        {
            if (sprite == null) return;
            // Bound the number of render objects during dense piercing/ricochet hits.
            if (hitParticles.Count >= 256)
            {
                Destroy(hitParticles[0].visual.gameObject);
                hitParticles.RemoveAt(0);
            }
            var visual = new GameObject(flash ? "Hit flash" : "Hit fragment", typeof(SpriteRenderer))
                .GetComponent<SpriteRenderer>();
            visual.transform.SetParent(world, false);
            visual.transform.position = position;
            visual.transform.rotation = Quaternion.Euler(0, 0, angle);
            visual.sprite = sprite;
            visual.sortingLayerID = layer;
            visual.sortingOrder = order;
            float scale = Mathf.Max(.01f, size) / Mathf.Max(.001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            visual.transform.localScale = Vector3.one * scale;
            hitParticles.Add(new HitParticle { visual = visual, velocity = velocity,
                duration = Mathf.Max(.01f, duration), gravity = gravity, spin = spin,
                scale = visual.transform.localScale, flash = flash });
        }

        void TickHitEffects(float dt)
        {
            for (int i = hitParticles.Count - 1; i >= 0; i--)
            {
                var particle = hitParticles[i];
                particle.age += dt;
                if (particle.age >= particle.duration)
                {
                    Destroy(particle.visual.gameObject);
                    hitParticles.RemoveAt(i);
                    continue;
                }
                float t = particle.age / particle.duration;
                particle.visual.transform.position += (Vector3)(particle.velocity * dt + Vector2.down * (.5f * particle.gravity * dt * dt));
                particle.velocity += Vector2.down * particle.gravity * dt;
                particle.visual.transform.Rotate(0, 0, particle.spin * dt);
                particle.visual.transform.localScale = particle.scale * (particle.flash ? Mathf.Lerp(1.2f, .6f, t) : 1f);
                particle.visual.color = new Color(1, 1, 1, particle.flash ? 1 - t : 1 - Mathf.InverseLerp(.5f, 1, t));
            }
        }
    }
}

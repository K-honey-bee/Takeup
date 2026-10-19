using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        sealed class EnemyDeath
        {
            public SpriteRenderer visual;
            public EnemyDefinition definition;
            public Sprite fallback;
            public float elapsed;
        }
        readonly List<EnemyDeath> enemyDeaths = new List<EnemyDeath>();

        void KnockBackEnemy(Enemy enemy, Vector2 direction)
        {
            if (enemy.definition.knockbackDistance <= 0 || direction.sqrMagnitude < .0001f) return;
            enemy.knockbackDirection = direction.normalized;
            enemy.knockbackRemaining = Mathf.Max(.01f, enemy.definition.knockbackDuration);
        }

        void TickEnemyKnockback(Enemy enemy, float dt)
        {
            float duration = Mathf.Max(.01f, enemy.definition.knockbackDuration);
            float before = Mathf.Clamp01(enemy.knockbackRemaining / duration);
            enemy.knockbackRemaining = Mathf.Max(0, enemy.knockbackRemaining - dt);
            float after = Mathf.Clamp01(enemy.knockbackRemaining / duration);
            // Integrate an ease-out displacement: same total distance at every frame rate.
            enemy.body.position += (Vector3)(enemy.knockbackDirection * enemy.definition.knockbackDistance * (before * before - after * after));
        }

        void BeginEnemyDeath(Enemy enemy)
        {
            // A render-only copy cannot chase, collide, take hits or grant another reward.
            var visual = new GameObject("Corpse - " + enemy.definition.displayName, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            visual.transform.SetParent(world, false);
            visual.transform.SetPositionAndRotation(enemy.body.position, enemy.body.rotation);
            Vector3 parentScale = world.lossyScale, sourceScale = enemy.body.lossyScale;
            visual.transform.localScale = new Vector3(sourceScale.x / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                sourceScale.y / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1);
            visual.sprite = enemy.renderer.sprite; visual.color = enemy.color;
            visual.sharedMaterial = enemy.renderer.sharedMaterial;
            visual.flipX = enemy.renderer.flipX; visual.flipY = enemy.renderer.flipY;
            visual.sortingLayerID = enemy.renderer.sortingLayerID;
            visual.sortingOrder = enemy.renderer.sortingOrder - 1;
            var death = new EnemyDeath { visual = visual, definition = enemy.definition, fallback = enemy.renderer.sprite };
            enemyDeaths.Add(death); UpdateEnemyDeath(death);
        }

        void TickEnemyDeaths(float dt)
        {
            for (int i = enemyDeaths.Count - 1; i >= 0; i--)
            {
                var death = enemyDeaths[i]; death.elapsed += dt;
                if (UpdateEnemyDeath(death)) continue;
                Destroy(death.visual.gameObject); enemyDeaths.RemoveAt(i);
            }
        }

        bool UpdateEnemyDeath(EnemyDeath death)
        {
            var config = death.definition;
            var frames = config.deathFrames;
            int count = frames == null ? 0 : frames.Length;
            float fps = Mathf.Max(1, config.deathFramesPerSecond);
            float animationTime = count / fps;
            if (death.elapsed < animationTime)
            {
                var frame = frames[Mathf.Min(count - 1, Mathf.FloorToInt(death.elapsed * fps))];
                if (frame != null) death.visual.sprite = frame;
                return true;
            }
            Sprite lastFrame = count > 0 ? frames[count - 1] : null;
            death.visual.sprite = config.corpseSprite != null ? config.corpseSprite : lastFrame != null ? lastFrame : death.fallback;
            float blinkTime = death.elapsed - animationTime - Mathf.Max(0, config.corpseHoldTime);
            if (blinkTime < 0) { death.visual.enabled = true; return true; }
            float interval = Mathf.Max(.02f, config.corpseBlinkInterval);
            if (blinkTime >= Mathf.Max(0, config.corpseBlinkCount) * interval * 2) return false;
            death.visual.enabled = Mathf.FloorToInt(blinkTime / interval) % 2 != 0;
            return true;
        }
    }
}

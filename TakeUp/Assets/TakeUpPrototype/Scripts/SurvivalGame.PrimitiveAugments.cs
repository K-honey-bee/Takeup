using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    [Serializable]
    public sealed class PrimitiveAugmentSettings
    {
        [Min(.1f)] public float bounceRange = 5;
        [Min(0)] public float thornDamage = 18;
        [Range(4, 24)] public int thornCount = 8;
        [Min(.1f)] public float thornRange = 5;
        [Min(.1f)] public float thornSpeed = 10;
        public Sprite thornSprite;
        public Color thornTint = new Color(.85f, .72f, .45f);
    }
    public static class PrimitiveAugments
    {
        public static void Ensure(SurvivalSettings settings)
        {
            if (settings.primitive == null) settings.primitive = new PrimitiveAugmentSettings();
            var cards = new List<AugmentDefinition>(settings.augments ?? Array.Empty<AugmentDefinition>());
            if (!cards.Exists(a => a != null && a.kind == AugmentKind.RicochetStone))
                cards.Add(new AugmentDefinition { title = "튕기는 돌", description = "돌이 근처 다른 적에게 1회 도약\n같은 적 재적중 불가 · 불 효과 유지", kind = AugmentKind.RicochetStone, amount = 1, maxStacks = 1 });
            if (!cards.Exists(a => a != null && a.kind == AugmentKind.ThornSkin))
                cards.Add(new AugmentDefinition { title = "가시 가죽", description = "피해를 받고 생존하면 8방향 가시 발사\n기본 피해 18 · 사거리 5m", kind = AugmentKind.ThornSkin, amount = 1, maxStacks = 1 });
            settings.augments = cards.ToArray();
        }
    }
    public sealed partial class SurvivalGame
    {
        bool TryRicochet(Shot shot, Vector2 origin)
        {
            if (shot.bounces <= 0) return false;
            float nearest = Mathf.Max(.1f, settings.primitive.bounceRange);
            Vector2 target = origin;
            bool found = false;
            foreach (var enemy in enemies)
            {
                if (enemy.health <= 0 || shot.hit.Contains(enemy)) continue;
                float distance = Vector2.Distance(origin, enemy.body.position);
                if (distance > nearest) continue;
                nearest = distance; target = enemy.body.position; found = true;
            }
            if (!shot.hitBoss && ActiveBoss != null && !ActiveBoss.IsDefeated && !ActiveBoss.IsFinale
                && Vector2.Distance(origin, ActiveBoss.Position) <= nearest)
            { target = ActiveBoss.Position; found = true; }
            if (!found) return false;
            shot.bounces--; shot.pierce = 1; shot.life = 2;
            shot.body.position = origin;
            shot.direction = (target - origin).sqrMagnitude > .0001f ? (target - origin).normalized : Vector2.up;
            shot.body.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg);
            return true;
        }
        void ReleaseThorns()
        {
            var config = settings.primitive;
            int count = Mathf.Clamp(config.thornCount, 4, 24);
            float speed = Mathf.Max(.1f, config.thornSpeed);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var visual = Visual("Thorn - Replace Via Primitive Settings", config.thornSprite, diamond,
                    config.thornTint, new Vector2(.4f, .13f), player.position, 12);
                visual.transform.rotation = Quaternion.Euler(0, 0, angle);
                shots.Add(new Shot { body = visual.transform, direction = direction, speed = speed,
                    damage = Mathf.Max(0, config.thornDamage) * damageMultiplier, radius = .12f,
                    life = Mathf.Max(.1f, config.thornRange) / speed, pierce = 1 });
            }
        }
    }
}

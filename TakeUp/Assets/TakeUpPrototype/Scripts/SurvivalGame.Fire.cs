using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    [Serializable]
    public sealed class FireSettings
    {
        [Min(0)] public float damagePerSecond = 6;
        [Min(.1f)] public float duration = 3;
        [Min(.1f)] public float spreadRadius = 3;
        public Color tint = new Color(1, .3f, .04f, 1);
        [Tooltip("비워두면 주황색 불씨 도형")]
        public Sprite emberSprite;
    }

    public static class FireAugments
    {
        public static void Ensure(SurvivalSettings settings)
        {
            if (settings.fire == null) settings.fire = new FireSettings();
            var entries = new List<AugmentDefinition>(settings.augments ?? Array.Empty<AugmentDefinition>());
            Add(entries, AugmentKind.FireDiscovery, "불의 발견", "돌·창 적중 시 화상 부여\n기본 초당 6 피해, 3초 지속 · 재적중 시 시간 갱신", 1, 1);
            Add(entries, AugmentKind.FireDamage, "뜨거운 불씨", "[불의 발견 필요] 화상 초당 피해 +3", 3, 4);
            Add(entries, AugmentKind.FireDuration, "오래 타는 불", "[불의 발견 필요] 화상 지속 시간 +1.5초", 1.5f, 3);
            Add(entries, AugmentKind.FireSpread, "불씨 확산", "[불의 발견 필요] 불타는 적 처치 시\n주변 3m 적에게 화상 전파", 1, 1);
            Add(entries, AugmentKind.FireDance, "불의 춤", "[불의 발견 + 발구르기 필요]\n발구르기 성공 충격파에 화상 추가", 1, 1);
            settings.augments = entries.ToArray();
        }
        static void Add(List<AugmentDefinition> entries, AugmentKind kind, string title, string description, float amount, int cap)
        {
            if (!entries.Exists(a => a != null && a.kind == kind)) entries.Add(new AugmentDefinition
                { kind = kind, title = title, description = description, amount = amount, maxStacks = cap });
        }
    }

    public sealed partial class SurvivalGame
    {
        public bool HasFire => StackCount(AugmentKind.FireDiscovery) > 0;
        public float BurnDamagePerSecond => Mathf.Max(0, settings.fire.damagePerSecond + FireBonus(AugmentKind.FireDamage)) * damageMultiplier;
        public float BurnDuration => Mathf.Max(.1f, settings.fire.duration + FireBonus(AugmentKind.FireDuration));
        Color FlameColor => settings.fire.tint;
        float bossBurnRemaining;
        SpriteRenderer bossBurnVisual;
        readonly List<FirePulse> firePulses = new List<FirePulse>();
        sealed class FirePulse { public SpriteRenderer visual; public float remaining = .35f; }

        float FireBonus(AugmentKind kind)
        {
            if (settings.augments != null) foreach (var entry in settings.augments)
                if (entry != null && entry.kind == kind) return entry.amount * StackCount(kind);
            return 0;
        }
        public bool CanOfferAugment(AugmentDefinition augment)
        {
            if (augment == null || StackCount(augment.kind) >= augment.maxStacks) return false;
            switch (augment.kind)
            {
                case AugmentKind.FireDamage: case AugmentKind.FireDuration: case AugmentKind.FireSpread: return HasFire;
                case AugmentKind.FireDance: return HasFire && HasDance;
                default: return true;
            }
        }
        SpriteRenderer CreateEmber(Transform target, float height)
        {
            var visual = Visual("Burn Ember - Replace Via Fire Settings", settings.fire.emberSprite, diamond,
                FlameColor, new Vector2(.22f, .38f), (Vector2)target.position + Vector2.up * height, 14);
            visual.transform.SetParent(target, true);
            return visual;
        }
        void Ignite(Enemy enemy)
        {
            enemy.burnRemaining = BurnDuration;
            if (enemy.burnVisual == null) enemy.burnVisual = CreateEmber(enemy.body, enemy.radius + .2f);
            enemy.burnVisual.gameObject.SetActive(true);
        }
        void IgniteBoss()
        {
            if (ActiveBoss == null || ActiveBoss.IsFinale || ActiveBoss.IsDefeated) { ClearBossBurn(); return; }
            bossBurnRemaining = BurnDuration;
            if (bossBurnVisual == null) bossBurnVisual = CreateEmber(ActiveBoss.Body, ActiveBoss.Radius + .3f);
            bossBurnVisual.gameObject.SetActive(true);
        }
        void ClearBossBurn()
        {
            bossBurnRemaining = 0;
            if (bossBurnVisual != null) bossBurnVisual.gameObject.SetActive(false);
        }
        void TickBurns(float dt)
        {
            if ((State != RunState.Playing && State != RunState.Dancing) || dt <= 0) return;
            for (int i = firePulses.Count - 1; i >= 0; i--)
            {
                var pulse = firePulses[i]; pulse.remaining -= dt;
                if (pulse.remaining <= 0) { Destroy(pulse.visual.gameObject); firePulses.RemoveAt(i); continue; }
                float t = 1 - pulse.remaining / .35f;
                pulse.visual.transform.localScale = Vector3.one * (settings.fire.spreadRadius * 2 * t);
                var color = FlameColor; color.a = 1 - t; pulse.visual.color = color;
            }
            // Damage all existing burns first; death spread starts dealing damage on the next simulation tick.
            foreach (var enemy in enemies)
            {
                if (enemy.burnRemaining <= 0) continue;
                float step = Mathf.Min(dt, enemy.burnRemaining);
                enemy.health -= BurnDamagePerSecond * step;
                enemy.burnRemaining = Mathf.Max(0, enemy.burnRemaining - dt);
                if (enemy.burnVisual != null)
                {
                    enemy.burnVisual.color = Color.Lerp(FlameColor, Color.yellow, .5f + .5f * Mathf.Sin(enemy.burnRemaining * 16));
                    enemy.burnVisual.gameObject.SetActive(enemy.burnRemaining > 0);
                }
                // Retain burning status until death is processed, including the last fractional tick.
                if (enemy.health <= 0) enemy.burnRemaining = Mathf.Max(.001f, enemy.burnRemaining);
            }
            for (int i = enemies.Count - 1; i >= 0; i--) if (enemies[i].health <= 0) KillEnemy(i);
            if (ActiveBoss == null || ActiveBoss.IsFinale || ActiveBoss.IsDefeated) { ClearBossBurn(); return; }
            if (bossBurnRemaining > 0)
            {
                float step = Mathf.Min(dt, bossBurnRemaining);
                bossBurnRemaining = Mathf.Max(0, bossBurnRemaining - dt);
                ActiveBoss.Damage(BurnDamagePerSecond * step);
                if (ActiveBoss.IsFinale || bossBurnRemaining <= 0) ClearBossBurn();
            }
        }
        void KillEnemy(int index)
        {
            var enemy = enemies[index];
            Vector2 position = enemy.body.position;
            bool spread = enemy.burnRemaining > 0 && StackCount(AugmentKind.FireSpread) > 0;
            enemies.RemoveAt(index);
            DropExperience(position, enemy.experience); Kills++;
            Destroy(enemy.body.gameObject);
            if (!spread) return;
            float radius = Mathf.Max(.1f, settings.fire.spreadRadius);
            foreach (var other in enemies)
                if (other.health > 0 && Vector2.Distance(position, other.body.position) <= radius) Ignite(other);
            if (ActiveBoss != null && Vector2.Distance(position, ActiveBoss.Position) <= radius + ActiveBoss.Radius) IgniteBoss();
            var visual = Visual("Spreading Ember", null, ring, FlameColor, Vector2.one * .1f, position, 13);
            firePulses.Add(new FirePulse { visual = visual });
        }
    }
}

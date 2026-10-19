using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    [Serializable]
    public sealed class OrbitBoneSettings
    {
        [Header("회전 불꽃 — 첫 획득 2개 / 이후 +1개")]
        public Sprite orbitFireSprite;
        public Color orbitFireTint = Color.white;
        [Min(.05f)] public float orbitFireSize = .7f;
        [Min(.1f)] public float orbitRadius = 2;
        public float orbitDegreesPerSecond = 150;
        [Min(0)] public float orbitDamage = 12;
        [Min(.05f)] public float orbitHitInterval = .3f;
        [Header("뼈 부메랑 — 좌클릭 유지 시 마우스 방향 발사")]
        public Sprite boneSprite;
        public Color boneTint = Color.white;
        public Vector2 boneSize = new Vector2(.8f, .3f);
        [Min(0)] public float boneDamage = 22;
        [Min(.1f)] public float boneInterval = 1.4f;
        [Min(.1f)] public float boneDistance = 6;
        [Min(.1f)] public float boneSpeed = 10;
        [Min(.1f)] public float boneReturnSpeed = 14;
        public float boneSpinDegreesPerSecond = 720;
    }

    public sealed partial class SurvivalGame
    {
        sealed class BoneFlight
        {
            public SpriteRenderer visual;
            public Vector2 direction;
            public float travelled, age;
            public bool returning, hitBoss;
            public readonly HashSet<Enemy> hit = new HashSet<Enemy>();
        }
        readonly List<SpriteRenderer> orbitFires = new List<SpriteRenderer>();
        readonly List<BoneFlight> bones = new List<BoneFlight>();
        float orbitAngle, orbitClock, nextOrbitBossHit, boneTimer;

        void InitializeOrbitBone()
        {
            if (settings.orbitBone == null) settings.orbitBone = new OrbitBoneSettings();
        }

        void ThrowBone(Vector2 direction)
        {
            var config = settings.orbitBone;
            var visual = Visual("Bone Boomerang", config.boneSprite, square, config.boneTint,
                new Vector2(Mathf.Max(.05f, config.boneSize.x), Mathf.Max(.05f, config.boneSize.y)), player.position, 12);
            bones.Add(new BoneFlight { visual = visual, direction = direction.normalized });
        }

        void TickOrbitBone(float dt)
        {
            var config = settings.orbitBone;
            orbitClock += dt;
            int count = StackCount(AugmentKind.OrbitFire);
            int desired = count > 0 ? count + 1 : 0;
            while (orbitFires.Count < desired)
                orbitFires.Add(Visual("Orbit Fire", config.orbitFireSprite, diamond,
                    config.orbitFireSprite != null ? config.orbitFireTint : new Color(1, .35f, .05f),
                    Vector2.one * Mathf.Max(.05f, config.orbitFireSize), player.position, 12));
            orbitAngle = Mathf.Repeat(orbitAngle + config.orbitDegreesPerSecond * dt, 360);
            float hitRadius = Mathf.Max(.05f, config.orbitFireSize) * .4f;
            for (int i = 0; i < orbitFires.Count; i++)
            {
                float angle = (orbitAngle + i * 360f / orbitFires.Count) * Mathf.Deg2Rad;
                Vector2 position = PlayerPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Max(.1f, config.orbitRadius);
                orbitFires[i].transform.position = position;
                bool occluded = MapWall.Blocks(PlayerPosition, position);
                orbitFires[i].enabled = !occluded;
                if (occluded) continue;
                for (int j = enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = enemies[j];
                    if (MapWall.Blocks(position, enemy.body.position)) continue;
                    if (enemy.nextOrbitHit > orbitClock || Vector2.Distance(position, enemy.body.position) > hitRadius + enemy.radius) continue;
                    enemy.nextOrbitHit = orbitClock + Mathf.Max(.05f, config.orbitHitInterval);
                    enemy.health -= config.orbitDamage * damageMultiplier; enemy.flash = .1f;
                    if (enemy.health <= 0) KillEnemy(j);
                }
                if (ActiveBoss != null && !ActiveBoss.IsFinale && !ActiveBoss.IsDefeated && nextOrbitBossHit <= orbitClock
                    && !MapWall.Blocks(position, ActiveBoss.Position) && Vector2.Distance(position, ActiveBoss.Position) <= hitRadius + ActiveBoss.Radius)
                {
                    nextOrbitBossHit = orbitClock + Mathf.Max(.05f, config.orbitHitInterval);
                    ActiveBoss.Damage(config.orbitDamage * damageMultiplier);
                }
            }

            for (int i = bones.Count - 1; i >= 0; i--)
            {
                var bone = bones[i]; bone.age += dt;
                Vector2 from = bone.visual.transform.position;
                bool returningAtStart = bone.returning;
                Vector2 to;
                if (returningAtStart)
                    to = Vector2.MoveTowards(from, PlayerPosition, Mathf.Max(config.boneReturnSpeed, moveSpeed * 1.5f, .1f) * dt);
                else
                {
                    float step = Mathf.Min(Mathf.Max(.1f, config.boneSpeed) * dt, Mathf.Max(0, config.boneDistance - bone.travelled));
                    to = from + bone.direction * step; bone.travelled += step;
                }
                float radius = Mathf.Max(.05f, Mathf.Min(config.boneSize.x, config.boneSize.y) * .5f);
                bool hitWall = MapWall.Cast(from, to, radius, out Vector2 wallPoint, out Vector2 wallNormal);
                if (hitWall) to = wallPoint + wallNormal * .003f;
                bone.visual.transform.position = to;
                bone.visual.transform.Rotate(0, 0, config.boneSpinDegreesPerSecond * dt);
                for (int j = enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = enemies[j];
                    if (MapWall.Blocks(from, enemy.body.position)) continue;
                    if (bone.hit.Contains(enemy) || OctopusBossEncounter.SegmentDistance(enemy.body.position, from, to) > enemy.radius + radius) continue;
                    bone.hit.Add(enemy); enemy.health -= config.boneDamage * damageMultiplier; enemy.flash = .1f;
                    KnockBackEnemy(enemy, to - from);
                    if (enemy.health <= 0) KillEnemy(j);
                }
                if (!bone.hitBoss && ActiveBoss != null && !ActiveBoss.IsFinale && !ActiveBoss.IsDefeated && !MapWall.Blocks(from, ActiveBoss.Position) && ActiveBoss.HitBySegment(from, to, radius))
                {
                    bone.hitBoss = true; ActiveBoss.Damage(config.boneDamage * damageMultiplier);
                }
                if (!returningAtStart && (hitWall || bone.travelled >= config.boneDistance))
                {
                    bone.returning = true; bone.hit.Clear(); bone.hitBoss = false;
                }
                if ((returningAtStart && (hitWall || Vector2.Distance(to, PlayerPosition) < .25f)) || bone.age >= 10)
                {
                    Destroy(bone.visual.gameObject); bones.RemoveAt(i);
                }
            }
        }
    }
}

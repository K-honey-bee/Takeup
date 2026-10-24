using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    public enum StartingWeapon { Stone, Sword }

    public sealed partial class SurvivalGame
    {
        public StartingWeapon SelectedWeapon { get; private set; }
        public string WeaponLabel => SelectedWeapon == StartingWeapon.Sword ? "검 · 흑백 참격" : "기계 돌 · 원거리 투척";
        SurvivalSettings weaponSourceSettings, runtimeWeaponSettings;
        Sprite slashSprite;
        int swordAttacks;
        sealed class SwordStrike
        {
            public Vector2 origin, direction;
            public float delay, range, angle, damage;
            public bool burning;
        }
        sealed class SwordVisual { public SpriteRenderer body; public float life = .22f; }
        readonly List<SwordStrike> swordStrikes = new List<SwordStrike>();
        readonly List<SwordVisual> swordVisuals = new List<SwordVisual>();

        void InitializeWeaponSettings()
        {
            weaponSourceSettings = bossEntry != null && bossEntry.playerSettings != null ? bossEntry.playerSettings : settings;
            runtimeWeaponSettings = Instantiate(settings);
            settings = runtimeWeaponSettings;
            var cards = new List<AugmentDefinition>();
            foreach (var card in settings.augments ?? System.Array.Empty<AugmentDefinition>())
            {
                if (card == null) continue;
                cards.Add(new AugmentDefinition { title = card.title, description = card.description,
                    icon = card.icon, kind = card.kind, amount = card.amount, maxStacks = card.maxStacks });
            }
            Add(AugmentKind.SwordReach, "확장 칼날", "[검] 베기 범위 +20%", 3);
            Add(AugmentKind.SwordDouble, "이중 참격", "[검] 공격 후 60% 피해의 추가 참격", 1);
            Add(AugmentKind.SwordWave, "검기 방출", "[검] 공격마다 관통 검기 발사\n검 기본 피해의 65% · 최대 3회 관통", 1);
            Add(AugmentKind.SwordEcho, "잔류 참격", "[검] 베었던 자리에 0.35초 후\n50% 피해의 잔류 참격", 1);
            Add(AugmentKind.SwordSpin, "회전 칼날", "[검] 기본 공격 4회마다\n주변 전체에 100% 피해", 1);
            Add(AugmentKind.SwordOverload, "과부하 발도", "[검] 기본 공격 5회마다\n전방 장거리 참격 · 250% 피해", 1);
            foreach (var card in cards)
            {
                if (card.kind == AugmentKind.Damage) { card.title = "무기 출력 강화"; card.description = "무기 피해량 +" + Mathf.RoundToInt(card.amount * 100) + "%"; }
                if (card.kind == AugmentKind.AttackSpeed) card.description = "기본 무기와 창의 공격 속도 +" + Mathf.RoundToInt(card.amount * 100) + "%";
                if (card.kind == AugmentKind.FireDiscovery) card.description = "기본 무기·창 적중 시 화상 부여\n지속 시간 내 재적중 시 갱신";
            }
            settings.augments = cards.ToArray();
            void Add(AugmentKind kind, string title, string description, int cap)
            {
                if (!cards.Exists(c => c.kind == kind)) cards.Add(new AugmentDefinition
                    { kind = kind, title = title, description = description, amount = 1, maxStacks = cap });
            }
        }

        void BeginWeaponSelection()
        {
            if (ActiveBoss != null && bossEntry != null) return;
            State = RunState.ChoosingWeapon;
            hud.ShowWeaponSelection();
        }

        public void SelectWeapon(StartingWeapon weapon)
        {
            if (State != RunState.ChoosingWeapon || (weapon != StartingWeapon.Stone && weapon != StartingWeapon.Sword)) return;
            SelectedWeapon = weapon;
            stoneTimer = 0;
            State = RunState.Playing;
            hud.HideOverlay();
            if (PendingChoices > 0) OfferAugments();
        }

        public bool WeaponAllows(AugmentKind kind)
        {
            if (kind >= AugmentKind.SwordReach && kind <= AugmentKind.SwordOverload) return SelectedWeapon == StartingWeapon.Sword;
            if (kind == AugmentKind.ExtraStone || kind == AugmentKind.RicochetStone) return SelectedWeapon == StartingWeapon.Stone;
            return true;
        }

        void SwingSword(Vector2 direction)
        {
            float range = 2.8f * (1 + .2f * StackCount(AugmentKind.SwordReach));
            float damage = settings.stoneDamage * 1.7f * damageMultiplier;
            swordAttacks++;
            PlayAttackSound();
            Queue(0, range, 120, damage);
            if (StackCount(AugmentKind.SwordDouble) > 0) Queue(.14f, range, 120, damage * .6f);
            if (StackCount(AugmentKind.SwordEcho) > 0) Queue(.35f, range, 120, damage * .5f);
            if (StackCount(AugmentKind.SwordSpin) > 0 && swordAttacks % 4 == 0) Queue(0, range, 360, damage);
            if (StackCount(AugmentKind.SwordOverload) > 0 && swordAttacks % 5 == 0) Queue(.08f, range * 2.2f, 28, damage * 2.5f);
            if (StackCount(AugmentKind.SwordWave) > 0)
            {
                var visual = Visual("Sword Wave", SwordSprite(), circle, Color.white, new Vector2(1.5f, 1.5f), player.position, 14);
                visual.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                shots.Add(new Shot { body = visual.transform, direction = direction, speed = 12, damage = damage * .65f,
                    radius = .5f, life = .7f, pierce = 3, burning = HasFire });
            }
            void Queue(float delay, float reach, float angle, float power) => swordStrikes.Add(new SwordStrike
            { origin = player.position, direction = direction, delay = delay, range = reach, angle = angle, damage = power, burning = HasFire });
        }

        // Distance to the filled sector, including target radius at its arc and side edges.
        static bool SwordContains(Vector2 origin, Vector2 forward, float range, float angle, Vector2 point, float radius)
        {
            Vector2 offset = point - origin;
            float distance = offset.magnitude;
            if (distance > range + radius) return false;
            if (angle >= 360 || distance <= radius || Vector2.Angle(forward, offset) <= angle * .5f) return true;
            float signed = Vector2.SignedAngle(forward, offset);
            Vector2 edge = Quaternion.Euler(0, 0, Mathf.Sign(signed) * angle * .5f) * forward;
            return Vector2.Distance(offset, edge * Mathf.Clamp(Vector2.Dot(offset, edge), 0, range)) <= radius;
        }

        void TickSwordEffects(float dt)
        {
            for (int i = swordVisuals.Count - 1; i >= 0; i--)
            {
                var fx = swordVisuals[i]; fx.life -= dt;
                if (fx.life <= 0) { Destroy(fx.body.gameObject); swordVisuals.RemoveAt(i); continue; }
                fx.body.color = new Color(1, 1, 1, fx.life / .22f);
            }
            for (int i = swordStrikes.Count - 1; i >= 0; i--)
            {
                var strike = swordStrikes[i]; strike.delay -= dt;
                if (strike.delay > 0) continue;
                swordStrikes.RemoveAt(i);
                DrawSwordStrike(strike);
                for (int j = enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = enemies[j];
                    if (!SwordContains(strike.origin, strike.direction, strike.range, strike.angle, enemy.body.position, enemy.radius)
                        || MapWall.Blocks(strike.origin, enemy.body.position)) continue;
                    if (strike.burning) Ignite(enemy);
                    enemy.health -= strike.damage; enemy.flash = .1f;
                    KnockBackEnemy(enemy, strike.direction);
                    if (enemy.health <= 0) KillEnemy(j);
                }
                if (ActiveBoss != null && !ActiveBoss.IsDefeated && !ActiveBoss.IsFinale
                    && SwordContains(strike.origin, strike.direction, strike.range, strike.angle, ActiveBoss.Position, ActiveBoss.Radius)
                    && !MapWall.Blocks(strike.origin, ActiveBoss.Position))
                {
                    ActiveBoss.Damage(strike.damage);
                    if (strike.burning) IgniteBoss();
                }
                if (State != RunState.Playing) { swordStrikes.Clear(); break; }
            }
        }

        void DrawSwordStrike(SwordStrike strike)
        {
            int count = strike.angle >= 360 ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                var visual = Visual("Black White Slash", SwordSprite(), circle, Color.white,
                    Vector2.one * strike.range * 2, strike.origin, 14);
                visual.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(strike.direction.y, strike.direction.x) * Mathf.Rad2Deg + i * 120);
                if (strike.angle < 120) visual.transform.localScale = Vector3.Scale(visual.transform.localScale, new Vector3(1, .2f, 1));
                swordVisuals.Add(new SwordVisual { body = visual });
            }
        }

        Sprite SwordSprite()
        {
            if (slashSprite != null) return slashSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1);
                float angle = Mathf.Abs(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
                float inner = Mathf.Lerp(.68f, .94f, angle / 60);
                pixels[y * size + x] = angle <= 60 && p.magnitude >= inner && p.magnitude <= .97f
                    ? (p.magnitude > .92f || p.magnitude < inner + .025f ? Color.black : Color.white) : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply();
            slashSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            generatedSprites.Add(slashSprite);
            return slashSprite;
        }
    }
}

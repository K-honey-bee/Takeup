using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public enum BullPhase { Intro, StompWarning, Shockwave, Recovery, ChargeWarning, Charge, Stunned, Defeated }
    public sealed class BullBossEncounter : MonoBehaviour, IBossEncounter
    {
        public BullBossSettings settings;
        public SurvivalGame game;
        public Transform bossBody;
        public SpriteRenderer bodyArtwork;
        [Tooltip("5시, 7시, 9시, 11시 벽. Sprite를 교체해도 이 영역이 충돌 범위입니다.")]
        public SpriteRenderer[] walls;
        public LineRenderer waveVisual, chargeVisual;
        public Text instructionText, healthText;
        public Image healthFill;
        public string DisplayName => "수호 황소";
        public float Health { get; private set; }
        public float MaximumHealth => settings.health;
        public bool IsDefeated => Phase == BullPhase.Defeated;
        public bool IsFinale => false;
        public Vector2 Position => bossBody.position;
        public float Radius => settings.bodyRadius;
        public Transform Body => bossBody;
        public BullPhase Phase { get; private set; }
        public float Remaining { get; private set; }
        public float WaveRadius { get; private set; }
        public Vector2 AttackDirection { get; private set; }
        public Vector2 WaveOrigin { get; private set; }
        bool nextCharge, waveHit, chargeHit;
        Font font;
        string instruction;
        Vector3 bodyScale;
        public void Initialize()
        {
            Health = MaximumHealth; bodyScale = bodyArtwork.transform.localScale;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 22);
            healthText.font = instructionText.font = font;
            Hide(); SetPhase(BullPhase.Intro, 2, "황소 · 충격파는 Shift 대시로 통과! 돌진은 흰 벽으로 유도하세요");
        }
        void SetPhase(BullPhase phase, float duration, string text)
        { Phase = phase; Remaining = duration; instruction = text; Refresh(); }
        void Refresh()
        {
            healthText.text = $"{DisplayName}  {Mathf.CeilToInt(Health)} / {MaximumHealth:0}";
            healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Health / MaximumHealth), 1);
            instructionText.text = instruction + (IsDefeated ? "" : $"  {Mathf.Max(0, Remaining):0.0}초");
        }
        void Hide()
        {
            waveVisual.gameObject.SetActive(false); chargeVisual.gameObject.SetActive(false);
            bodyArtwork.transform.localScale = bodyScale; bodyArtwork.color = Color.white;
        }
        void Aim()
        {
            Vector2 delta = game.PlayerPosition - Position;
            AttackDirection = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.down;
        }
        void BeginPattern()
        {
            Hide(); Aim();
            if (nextCharge)
            {
                nextCharge = false; chargeHit = false;
                chargeVisual.gameObject.SetActive(true); chargeVisual.positionCount = 2;
                chargeVisual.SetPosition(0, Position); chargeVisual.SetPosition(1, Position + AttackDirection * 35);
                chargeVisual.startWidth = chargeVisual.endWidth = Radius * 2;
                SetPhase(BullPhase.ChargeWarning, settings.chargeWarning, "돌진 방향 고정! 벽 앞에서 유도한 뒤 옆으로 피하세요");
            }
            else
            {
                nextCharge = true; waveHit = false; WaveRadius = 0; WaveOrigin = Position;
                DrawArc(settings.waveRadius, .18f);
                SetPhase(BullPhase.StompWarning, settings.stompWarning, "앞발 구르기 예고! 다가오는 충격파를 향해 Shift 대시");
            }
        }
        public void Tick(float dt)
        {
            if (IsDefeated || dt <= 0) return;
            Remaining -= dt;
            if (Phase == BullPhase.StompWarning)
                bodyArtwork.transform.localScale = Vector3.Scale(bodyScale, new Vector3(1.05f, .9f + .08f * Mathf.Sin(Remaining * 14), 1));
            if (Phase == BullPhase.Shockwave)
            {
                float previous = WaveRadius;
                WaveRadius = Mathf.Min(settings.waveRadius, WaveRadius + settings.waveSpeed * dt);
                DrawArc(WaveRadius, .95f);
                if (!waveHit && WaveTouches(game.PlayerPosition, previous, WaveRadius))
                { waveHit = true; game.DamagePlayer(settings.waveDamage); }
            }
            if (Phase == BullPhase.Charge) MoveCharge(dt);
            if (Phase == BullPhase.Stunned) bodyArtwork.color = new Color(1, 1, 1, .55f + .4f * Mathf.Abs(Mathf.Sin(Remaining * 8)));
            if (Remaining <= 0)
            {
                switch (Phase)
                {
                    case BullPhase.Intro: case BullPhase.Recovery: BeginPattern(); break;
                    case BullPhase.StompWarning:
                        bodyArtwork.transform.localScale = bodyScale; DrawArc(0, 1);
                        SetPhase(BullPhase.Shockwave, settings.waveRadius / Mathf.Max(1, settings.waveSpeed), "충격파! 바깥으로 퍼지는 흰 띠를 대시로 통과하세요"); break;
                    case BullPhase.ChargeWarning:
                        chargeVisual.gameObject.SetActive(false);
                        SetPhase(BullPhase.Charge, settings.chargeTimeout, "직선 돌진!"); break;
                    case BullPhase.Charge: case BullPhase.Shockwave: case BullPhase.Stunned:
                        Hide(); SetPhase(BullPhase.Recovery, settings.recovery, "다음 공격 준비"); break;
                }
            }
            Refresh();
        }
        public bool WaveTouches(Vector2 point, float previous, float current)
        {
            Vector2 delta = point - WaveOrigin; float distance = delta.magnitude;
            float padding = settings.waveThickness * .5f + .3f;
            return distance >= previous - padding && distance <= current + padding
                && (distance < .001f || Vector2.Angle(AttackDirection, delta) <= settings.waveAngle * .5f);
        }
        void DrawArc(float radius, float alpha)
        {
            const int points = 65;
            waveVisual.gameObject.SetActive(true); waveVisual.positionCount = points;
            waveVisual.startWidth = waveVisual.endWidth = settings.waveThickness;
            waveVisual.startColor = waveVisual.endColor = new Color(1, 1, 1, alpha);
            float facing = Mathf.Atan2(AttackDirection.y, AttackDirection.x) * Mathf.Rad2Deg;
            for (int i = 0; i < points; i++)
            {
                float angle = (facing - settings.waveAngle * .5f + settings.waveAngle * i / (points - 1)) * Mathf.Deg2Rad;
                waveVisual.SetPosition(i, WaveOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
        void MoveCharge(float dt)
        {
            float distance = settings.chargeSpeed * Mathf.Min(dt, Mathf.Max(0, Remaining + dt));
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .1f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 from = Position, to = from + AttackDirection * (distance / steps);
                foreach (var wall in walls)
                    if ((PushOutsideWall(to, wall, Radius) - to).sqrMagnitude > .000001f)
                    {
                        Hide(); SetPhase(BullPhase.Stunned, settings.stunDuration, "벽 충돌! 기절한 황소에게 추가 피해"); return;
                    }
                Vector2 half = game.settings.arenaHalfSize - Vector2.one * Radius;
                if (Mathf.Abs(to.x) > half.x || Mathf.Abs(to.y) > half.y)
                { Hide(); SetPhase(BullPhase.Recovery, settings.recovery, "돌진 종료 · 흰 벽에 부딪히게 하면 기절합니다"); return; }
                bossBody.position = to;
                if (!chargeHit && OctopusBossEncounter.SegmentDistance(game.PlayerPosition, from, to) <= Radius + .3f)
                { chargeHit = true; game.DamagePlayer(settings.chargeDamage); }
            }
        }
        public static Vector2 PushOutsideWall(Vector2 point, SpriteRenderer wall, float radius)
        {
            Vector2 center = wall.transform.TransformPoint(wall.sprite.bounds.center);
            Vector2 x = wall.transform.right, y = wall.transform.up;
            Vector2 half = Vector2.Scale(wall.sprite.bounds.extents, new Vector2(Mathf.Abs(wall.transform.lossyScale.x), Mathf.Abs(wall.transform.lossyScale.y)));
            Vector2 d = point - center, local = new Vector2(Vector2.Dot(d, x), Vector2.Dot(d, y));
            Vector2 closest = new Vector2(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y));
            Vector2 away = local - closest;
            if (away.sqrMagnitude >= radius * radius) return point;
            if (away.sqrMagnitude > .000001f) local = closest + away.normalized * radius;
            else if (half.x - Mathf.Abs(local.x) < half.y - Mathf.Abs(local.y)) local.x = (local.x < 0 ? -1 : 1) * (half.x + radius);
            else local.y = (local.y < 0 ? -1 : 1) * (half.y + radius);
            return center + x * local.x + y * local.y;
        }
        public Vector2 ConstrainMovement(Vector2 from, Vector2 to, float radius)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .1f));
            Vector2 result = from, step = (to - from) / steps;
            for (int i = 0; i < steps; i++)
            {
                result += step;
                Vector2 away = result - Position;
                if (away.magnitude < Radius + radius) result = Position + (away.sqrMagnitude < .001f ? Vector2.down : away.normalized) * (Radius + radius);
                foreach (var wall in walls) result = PushOutsideWall(result, wall, radius);
            }
            return result;
        }
        public float MovementMultiplier(Vector2 point) => 1;
        public bool HitBySegment(Vector2 from, Vector2 to, float radius) => !IsDefeated && OctopusBossEncounter.SegmentDistance(Position, from, to) <= Radius + radius;
        public void Damage(float amount)
        {
            if (IsDefeated || amount <= 0) return;
            Health = Mathf.Max(0, Health - amount * (Phase == BullPhase.Stunned ? settings.stunnedDamageMultiplier : 1));
            if (Health <= 0) { Hide(); SetPhase(BullPhase.Defeated, 0, "황소 처치 완료!"); game.CompleteBossFight(); }
            Refresh();
        }
        void OnDestroy() { if (font != null) Destroy(font); }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public enum OctopusPhase { Intro, SweepWarning, Sweep, SlamWarning, SlamImpact, InkWarning, MemoryShow, MemoryHide, MemoryBlast, Recovery, Defeated, RageWarning, RageImpact, FinaleShow, FinaleChoose, FinaleImpact }

    public sealed partial class OctopusBossEncounter : MonoBehaviour, IBossEncounter
    {
        public string DisplayName => "고대 문어";
        public float MaximumHealth => settings.health;
        public Transform Body => bossBody;
        public OctopusBossSettings settings;
        public SurvivalGame game;
        [Header("Hierarchy에서 위치와 이미지를 편집")]
        public Transform bossBody;
        public SpriteRenderer bodyArtwork;
        public BossCoverRock[] rocks;
        public SpriteRenderer sweepArm;
        public MeshFilter sweepWarning;
        public SpriteRenderer[] slamZones;
        public SpriteRenderer inkPrefab;
        public Transform effectsRoot;
        public Image healthFill;
        public Text healthText, instructionText, phaseText;
        public Image[] colorLights;
        public OctopusPhase Phase { get; private set; }
        public float Health { get; private set; }
        public float Remaining { get; private set; }
        public int MemoryStep { get; private set; }
        public string Instruction { get; private set; }
        public bool IsDefeated => Phase == OctopusPhase.Defeated;
        public Vector2 Position => bossBody.position;
        public float Radius => settings.bodyRadius * Mathf.Max(Mathf.Abs(bossBody.lossyScale.x), Mathf.Abs(bossBody.lossyScale.y));
        public static readonly Color[] Colors = { new Color(1, .24f, .22f), new Color(.25f, .58f, 1), new Color(1, .85f, .18f), Color.white };
        public static readonly string[] Names = { "빨강", "파랑", "노랑", "흰색" };
        readonly List<Ink> puddles = new List<Ink>();
        sealed class Ink { public SpriteRenderer visual; public float life; }
        int pattern = 2;
        bool sweepHit;
        Color baseColor;
        Font runtimeFont;
        Mesh fan;
        Vector2 inkTarget;

        public void Initialize()
        {
            Health = settings.health; baseColor = bodyArtwork.color;
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 22);
            healthText.font = instructionText.font = phaseText.font = runtimeFont;
            BuildSweepFan(); InitializeNewVisuals(); HideAttackVisuals();
            SetPhase(OctopusPhase.Intro, 2.5f, "문어가 깨어납니다 · 돌은 자동으로 던집니다");
            RefreshHud();
        }

        public void Tick(float dt)
        {
            if (IsDefeated || dt <= 0) return;
            for (int i = puddles.Count - 1; i >= 0; i--)
            {
                var p = puddles[i]; p.life -= dt;
                if (p.life <= 0) { Destroy(p.visual.gameObject); puddles.RemoveAt(i); }
            }
            Remaining -= dt;
            if (Phase == OctopusPhase.Sweep)
            {
                float angle = Mathf.Lerp(settings.sweepStartAngle, settings.sweepEndAngle, 1 - Mathf.Clamp01(Remaining / settings.sweepDuration));
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                SetSweepArm(direction);
                if (!sweepHit && SegmentDistance(game.PlayerPosition, Position, Position + direction * settings.sweepLength) < settings.sweepWidth * .5f + .3f)
                { game.DamagePlayer(settings.tentacleDamage); sweepHit = true; }
            }
            TickNewVisuals();
            if (Phase == OctopusPhase.MemoryShow)
            {
                SetLights(Remaining > settings.colorFlashTime * .25f ? memorySequence[MemoryStep] : -1);
                bodyArtwork.color = Remaining > settings.colorFlashTime * .25f ? Colors[memorySequence[MemoryStep]] : baseColor;
            }
            if (Remaining <= 0) Advance();
            RefreshHud();
        }

        void Advance()
        {
            if (AdvanceNewPatterns()) return;
            switch (Phase)
            {
                case OctopusPhase.Intro: case OctopusPhase.Recovery: BeginNextPattern(); break;
                case OctopusPhase.SweepWarning:
                    sweepWarning.gameObject.SetActive(false); sweepArm.gameObject.SetActive(true); sweepHit = false;
                    SetPhase(OctopusPhase.Sweep, settings.sweepDuration, "촉수 휘두르기! 바깥쪽으로 피하세요"); break;
                case OctopusPhase.Sweep: Recover(); break;
                case OctopusPhase.SlamWarning:
                    foreach (var zone in slamZones) zone.color = new Color(1, .15f, .1f, .8f);
                    foreach (var zone in slamZones) if (InsideZone(game.PlayerPosition, zone)) { game.DamagePlayer(settings.tentacleDamage); break; }
                    SetPhase(OctopusPhase.SlamImpact, .35f, "양쪽 촉수 내려치기!"); break;
                case OctopusPhase.SlamImpact: Recover(); break;
                case OctopusPhase.InkWarning:
                    AddInk(inkTarget); AddInk(inkTarget + new Vector2(-2.6f, .8f)); AddInk(inkTarget + new Vector2(2.6f, .8f));
                    Recover(); break;
                case OctopusPhase.MemoryShow:
                    MemoryStep++;
                    if (MemoryStep < 4) SetPhase(OctopusPhase.MemoryShow, settings.colorFlashTime, "색상 순서를 기억하세요!");
                    else { MemoryStep = 0; BeginHide(); }
                    break;
                case OctopusPhase.MemoryHide:
                    bool safe = IsCovered(game.PlayerPosition, memorySequence[MemoryStep]);
                    if (!safe) game.DamagePlayer(settings.memoryDamage);
                    bodyArtwork.color = Colors[memorySequence[MemoryStep]];
                    SetPhase(OctopusPhase.MemoryBlast, .45f, safe ? "방어 성공!" : "파동에 맞았습니다! 바위의 반대편으로 숨으세요"); break;
                case OctopusPhase.MemoryBlast:
                    MemoryStep++;
                    if (MemoryStep < 4) BeginHide(); else Recover();
                    break;
            }
        }

        void BeginNextPattern()
        {
            HideAttackVisuals();
            if (IsEnraged) { BeginRage(); return; }
            int next = pattern; pattern = (pattern + 1) % 3;
            switch (next)
            {
                case 0:
                    sweepWarning.transform.position = bossBody.position; sweepWarning.gameObject.SetActive(true);
                    SetPhase(OctopusPhase.SweepWarning, settings.warningTime, "촉수 휘두르기 예고 · 빨간 부채꼴 밖으로!"); break;
                case 1:
                    foreach (var zone in slamZones) { zone.color = new Color(1, .05f, .08f, .45f); zone.gameObject.SetActive(true); }
                    SetPhase(OctopusPhase.SlamWarning, settings.warningTime, "양쪽 내려치기 예고 · 두 영역 사이로!"); break;
                case 99:
                    inkTarget = game.PlayerPosition;
                    ShowInkWarning(inkTarget);
                    SetPhase(OctopusPhase.InkWarning, settings.inkWarningTime, "잉크가 떨어집니다 · 표시된 자리에서 벗어나세요"); break;
                default:
                    MemoryStep = 0; RandomizeSequence(4);
                    SetPhase(OctopusPhase.MemoryShow, settings.colorFlashTime, "랜덤 색상 4개를 기억하세요!"); break;
            }
        }

        void BeginHide()
        {
            bodyArtwork.color = baseColor; SetLights(memorySequence[MemoryStep]);
            foreach (var rock in rocks) if (rock.safeMarker != null)
            {
                Vector2 away = ((Vector2)rock.transform.position - Position).normalized;
                rock.safeMarker.transform.position = (Vector2)rock.transform.position + away * (rock.WorldRadius + .65f);
                rock.safeMarker.gameObject.SetActive(rock.colorIndex == memorySequence[MemoryStep]);
            }
            SetPhase(OctopusPhase.MemoryHide, settings.hideTime, $"{MemoryStep + 1}/4  {Names[memorySequence[MemoryStep]]} 바위 뒤! 문어 → 바위 → 나 순서로");
        }

        public bool IsCovered(Vector2 playerPosition, int colorIndex)
        {
            Vector2 ray = playerPosition - Position; float length = ray.magnitude;
            if (length < .001f) return false;
            Vector2 direction = ray / length;
            foreach (var rock in rocks)
            {
                if (rock.colorIndex != colorIndex) continue;
                Vector2 delta = (Vector2)rock.transform.position - Position;
                float along = Vector2.Dot(delta, direction);
                if (along <= 0 || along >= length - .25f) continue;
                if ((delta - direction * along).magnitude <= Mathf.Max(.1f, rock.WorldRadius - .2f)) return true;
            }
            return false;
        }

        public Vector2 ConstrainMovement(Vector2 from, Vector2 to, float radius)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .2f));
            Vector2 step = (to - from) / steps, result = from;
            for (int i = 0; i < steps; i++)
            {
                result += step;
                result = OutsideCircle(result, Position, Radius + radius);
                if (!IsFinale) foreach (var rock in rocks) result = OutsideCircle(result, rock.transform.position, rock.WorldRadius + radius);
            }
            return result;
        }

        static Vector2 OutsideCircle(Vector2 point, Vector2 center, float radius)
        {
            Vector2 delta = point - center;
            return delta.sqrMagnitude >= radius * radius ? point : center + (delta.sqrMagnitude < .0001f ? Vector2.down : delta.normalized) * radius;
        }

        public float MovementMultiplier(Vector2 point)
        {
            foreach (var p in puddles) if (Vector2.Distance(point, p.visual.transform.position) <= settings.inkRadius) return settings.inkMoveMultiplier;
            return 1;
        }

        public bool HitBySegment(Vector2 from, Vector2 to, float radius) => !IsDefeated && SegmentDistance(Position, from, to) <= Radius + radius;
        public void Damage(float amount)
        {
            if (IsDefeated || IsFinale || amount <= 0) return;
            bool wasEnraged = IsEnraged;
            Health = Mathf.Max(0, Health - amount);
            if (Health <= 0) BeginFinale();
            else if (!wasEnraged && IsEnraged)
            {
                ClearInk(); HideAttackVisuals(); BeginRage();
            }
            RefreshHud();
        }

        void AddInk(Vector2 position)
        {
            var visual = Instantiate(inkPrefab, position, Quaternion.identity, effectsRoot);
            visual.gameObject.SetActive(true); visual.color = new Color(.14f, .07f, .22f, .8f);
            Size(visual, Vector2.one * settings.inkRadius * 2);
            puddles.Add(new Ink { visual = visual, life = settings.inkLifetime });
        }

        readonly List<SpriteRenderer> inkWarnings = new List<SpriteRenderer>();
        void ShowInkWarning(Vector2 target)
        {
            for (int i = 0; i < 3; i++)
            {
                var visual = Instantiate(inkPrefab, target + (i == 0 ? Vector2.zero : new Vector2(i == 1 ? -2.6f : 2.6f, .8f)), Quaternion.identity, effectsRoot);
                visual.gameObject.SetActive(true); Size(visual, Vector2.one * settings.inkRadius * 2);
                visual.color = new Color(.7f, .35f, 1, .35f); inkWarnings.Add(visual);
            }
        }
        void Recover() { HideAttackVisuals(); SetPhase(OctopusPhase.Recovery, settings.recoveryTime, "공격 기회! 다음 패턴을 준비하세요"); }
        void HideAttackVisuals()
        {
            HideNewVisuals();
            sweepArm.gameObject.SetActive(false); sweepWarning.gameObject.SetActive(false);
            foreach (var zone in slamZones) zone.gameObject.SetActive(false);
            foreach (var rock in rocks) if (rock.safeMarker != null) rock.safeMarker.gameObject.SetActive(false);
            foreach (var visual in inkWarnings) if (visual != null) Destroy(visual.gameObject); inkWarnings.Clear();
            bodyArtwork.color = baseColor; SetLights(-1);
        }
        void SetLights(int index) { for (int i = 0; i < colorLights.Length; i++) colorLights[i].color = i == index ? Colors[i] : Colors[i] * new Color(.25f, .25f, .25f, 1); }
        void SetPhase(OctopusPhase phase, float duration, string instruction) { Phase = phase; Remaining = duration; Instruction = instruction; }
        void RefreshHud()
        {
            healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Health / settings.health), 1);
            healthText.text = $"고대 문어   {Mathf.CeilToInt(Health)} / {settings.health:0}";
            instructionText.text = Instruction;
            phaseText.text = IsDefeated ? "승리" : $"다음 동작까지 {Mathf.Max(0, Remaining):0.0}초";
        }
        void SetSweepArm(Vector2 direction)
        {
            sweepArm.transform.position = Position + direction * settings.sweepLength * .5f;
            sweepArm.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Size(sweepArm, new Vector2(settings.sweepLength, settings.sweepWidth));
        }
        static void Size(SpriteRenderer renderer, Vector2 size)
        {
            Vector2 bounds = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(size.x / Mathf.Max(.001f, bounds.x), size.y / Mathf.Max(.001f, bounds.y), 1);
        }
        public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 v = b - a; float t = v.sqrMagnitude < .0001f ? 0 : Mathf.Clamp01(Vector2.Dot(point - a, v) / v.sqrMagnitude);
            return Vector2.Distance(point, a + v * t);
        }
        public static bool InsideZone(Vector2 point, SpriteRenderer zone)
        {
            Vector3 local = zone.transform.InverseTransformPoint(point);
            Bounds bounds = zone.sprite.bounds;
            return Mathf.Abs(local.x - bounds.center.x) <= bounds.extents.x && Mathf.Abs(local.y - bounds.center.y) <= bounds.extents.y;
        }
        void BuildSweepFan()
        {
            const int segments = 40;
            var vertices = new Vector3[segments + 2]; var triangles = new int[segments * 3];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(settings.sweepStartAngle, settings.sweepEndAngle, (float)i / segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * settings.sweepLength;
                if (i < segments) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            }
            fan = new Mesh { name = "Sweep telegraph" }; fan.vertices = vertices; fan.triangles = triangles; fan.RecalculateBounds();
            sweepWarning.sharedMesh = fan;
        }
        void OnDestroy() { if (runtimeFont != null) Destroy(runtimeFont); if (fan != null) Destroy(fan); DestroyNewVisuals(); }
    }
}

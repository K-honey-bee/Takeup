using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TakeUp.Prototype
{
    [Serializable]
    public sealed class InstantSlashSettings
    {
        [Tooltip("켜면 검 선택 직후 사용 가능. 끄면 순간베기 증강 획득 후 해금됩니다.")]
        public bool startUnlocked = true;
        [Range(1, 12)] public int targetCount = 6;
        [Min(1)] public float inputDuration = 5;
        [Min(.1f)] public float spawnInterval = .42f;
        [Range(.08f, .6f)] public float fallSpeed = .22f;
        [Range(.02f, .1f)] public float targetRadius = .045f;
        [Min(.5f)] public float releaseDuration = 1.1f;
        [Min(1)] public float cooldown = 20;
        [Min(0)] public float bossDamage = 350;
        [Range(0, 1)] public float volume = .65f;
        [Header("교체 가능한 이미지 / 비어 있으면 흑백 기본 도형")]
        public Sprite fallingSprite;
        [Header("교체 가능한 소리 / 비어 있으면 합성 효과음")]
        public AudioClip beginSound, sliceSound, releaseSound, failureSound;
        [Header("월드 파티클 프리팹 / 자동 재생 · 종료 시 정리")]
        [Tooltip("암전이 풀리는 순간 플레이어 위치에서 재생합니다. 흑백 참격/파편 프리팹을 연결하세요.")]
        public ParticleSystem releaseParticles;
    }

    public sealed partial class SurvivalGame
    {
        public sealed class InstantTarget
        {
            public Vector2 position;
            public bool sliced;
        }
        public sealed class InstantTrail
        {
            public Vector2 from, to;
            public float remaining;
        }
        public bool HasInstantSlash => SelectedWeapon == StartingWeapon.Sword && StackCount(AugmentKind.InstantSlash) > 0;
        public float InstantSlashCooldown { get; private set; }
        public int InstantHits { get; private set; }
        public bool InstantResolved { get; private set; }
        public bool InstantSucceeded { get; private set; }
        public float InstantTime { get; private set; }
        public float InstantReleaseTime { get; private set; }
        public int InstantTargetCount => Mathf.Clamp(settings.instantSlash.targetCount, 1, 12);
        public IReadOnlyList<InstantTarget> InstantTargets => instantTargets;
        public IReadOnlyList<InstantTrail> InstantTrails => instantTrails;
        public Sprite InstantPlayerSprite => playerRenderer != null ? playerRenderer.sprite : null;
        public Vector2 InstantPlayerViewport => worldCamera.WorldToViewportPoint(player.position);
        public float InstantTimeLimit => Mathf.Max(settings.instantSlash.inputDuration,
            (InstantTargetCount - 1) * Mathf.Max(.1f, settings.instantSlash.spawnInterval) + 1.5f);
        readonly List<InstantTarget> instantTargets = new List<InstantTarget>();
        readonly List<InstantTrail> instantTrails = new List<InstantTrail>();
        readonly List<ParticleSystem> instantParticles = new List<ParticleSystem>();
        Vector2 previousInstantMouse;
        bool instantMousePrimed, instantOwnsClock, instantDamageApplied;
        float instantPreviousTimeScale;
        int instantBurst = -1;
        Volume instantVolume;
        VolumeProfile instantProfile;
        ColorAdjustments instantMonochrome;
        UniversalAdditionalCameraData instantCameraData;
        bool instantPreviousPost;
        LayerMask instantPreviousVolumeMask;
        AudioClip instantDefaultSlice, instantDefaultRelease;

        public bool StartWeaponSkill() => SelectedWeapon == StartingWeapon.Sword ? StartInstantSlash() : StartDance();

        public bool StartInstantSlash()
        {
            if (State != RunState.Playing || !HasInstantSlash || InstantSlashCooldown > 0 || IsDashing) return false;
            InstantHits = 0; InstantTime = InstantReleaseTime = 0;
            InstantResolved = InstantSucceeded = instantDamageApplied = false;
            instantBurst = -1; instantMousePrimed = false;
            instantTargets.Clear(); instantTrails.Clear();
            swordStrikes.Clear();
            instantPreviousTimeScale = Time.timeScale;
            instantOwnsClock = true; Time.timeScale = 0;
            InstantSlashCooldown = Mathf.Max(1, settings.instantSlash.cooldown);
            State = RunState.InstantSlashing;
            PrepareInstantMonochrome();
            if (instantDefaultSlice == null) instantDefaultSlice = MakeSlashSound(false);
            if (instantDefaultRelease == null) instantDefaultRelease = MakeSlashSound(true);
            PlayInstantSound(settings.instantSlash.beginSound, defaultHit);
            SpawnInstantTarget();
            hud.ShowInstantSlashHud();
            return true;
        }

        void SpawnInstantTarget()
        {
            // Alternating lanes encourage deliberate left/right swipes rather than camping in one place.
            int index = instantTargets.Count;
            float[] lanes = { .27f, .72f, .42f, .62f, .22f, .78f };
            instantTargets.Add(new InstantTarget { position = new Vector2(lanes[index % lanes.Length], .86f) });
        }

        void TickInstantSlash(float dt)
        {
            dt = Mathf.Min(dt, .1f);
            for (int i = instantTrails.Count - 1; i >= 0; i--)
            {
                instantTrails[i].remaining -= dt;
                if (instantTrails[i].remaining <= 0) instantTrails.RemoveAt(i);
            }
            if (InstantResolved)
            {
                InstantReleaseTime += dt;
                if (InstantSucceeded)
                {
                    int burst = Mathf.Min(5, Mathf.FloorToInt(InstantReleaseTime / .14f));
                    if (burst != instantBurst) { instantBurst = burst; PlayInstantSound(settings.instantSlash.sliceSound, instantDefaultSlice); }
                }
                float duration = InstantSucceeded ? Mathf.Max(.5f, settings.instantSlash.releaseDuration) : .55f;
                if (InstantReleaseTime >= duration) FinishInstantSlash();
                return;
            }
            InstantTime += dt;
            while (instantTargets.Count < InstantTargetCount &&
                InstantTime >= instantTargets.Count * Mathf.Max(.1f, settings.instantSlash.spawnInterval)) SpawnInstantTarget();
            foreach (var target in instantTargets)
                if (!target.sliced) target.position.y -= Mathf.Clamp(settings.instantSlash.fallSpeed, .08f, .6f) * dt;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                Vector2 next = new Vector2(mouse.position.x.ReadValue() / Mathf.Max(1, Screen.width),
                    mouse.position.y.ReadValue() / Mathf.Max(1, Screen.height));
                if (instantMousePrimed) SubmitInstantSwipe(previousInstantMouse, next);
                previousInstantMouse = next; instantMousePrimed = true;
            }
            else instantMousePrimed = false;
            if (InstantResolved) return;
            foreach (var target in instantTargets)
                if (!target.sliced && target.position.y < .12f) { ResolveInstantSlash(false); return; }
            if (InstantTime >= InstantTimeLimit) ResolveInstantSlash(false);
        }

        public int SubmitInstantSwipe(Vector2 from, Vector2 to)
        {
            if (State != RunState.InstantSlashing || InstantResolved) return 0;
            // Screen-height units give the same circular hit tolerance on wide and tall screens.
            Vector2 scale = new Vector2((float)Screen.width / Mathf.Max(1, Screen.height), 1);
            Vector2 a = Vector2.Scale(from, scale), b = Vector2.Scale(to, scale);
            Vector2 segment = b - a;
            if (segment.magnitude < .006f) return 0; // A stationary held click never cuts a falling target.
            if (instantTrails.Count >= 24) instantTrails.RemoveAt(0);
            instantTrails.Add(new InstantTrail { from = from, to = to, remaining = .18f });
            int hits = 0;
            foreach (var target in instantTargets)
            {
                if (target.sliced || target.position.y < .12f) continue;
                Vector2 point = Vector2.Scale(target.position, scale);
                float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude);
                if (Vector2.Distance(point, a + segment * t) > Mathf.Clamp(settings.instantSlash.targetRadius, .02f, .1f)) continue;
                target.sliced = true; InstantHits++; hits++;
            }
            if (hits > 0) PlayInstantSound(settings.instantSlash.sliceSound, instantDefaultSlice);
            if (InstantHits >= InstantTargetCount) ResolveInstantSlash(true);
            return hits;
        }

        Vector3 InstantViewportToWorld(Vector2 position) => worldCamera.ViewportToWorldPoint(
            new Vector3(position.x, position.y, worldCamera.WorldToViewportPoint(player.position).z));

        void ResolveInstantSlash(bool success)
        {
            if (InstantResolved) return;
            InstantResolved = true; InstantSucceeded = success; InstantReleaseTime = 0;
            instantMousePrimed = false;
            if (instantVolume != null) instantVolume.weight = success ? 1 : 0;
            PlayInstantSound(success ? settings.instantSlash.releaseSound : settings.instantSlash.failureSound,
                success ? instantDefaultRelease : defaultFail);
            if (success) SpawnInstantParticles(settings.instantSlash.releaseParticles, player.position);
        }

        bool InInstantViewport(Vector3 position)
        {
            Vector3 point = worldCamera.WorldToViewportPoint(position);
            return point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
        }

        void ApplyInstantSlashDamage()
        {
            if (instantDamageApplied || !InstantSucceeded) return;
            instantDamageApplied = true;
            // No enemy cap: all living ordinary enemies whose centres are on screen are executed.
            for (int i = enemies.Count - 1; i >= 0; i--)
                if (enemies[i].health > 0 && InInstantViewport(enemies[i].body.position)) KillEnemy(i);
            if (ActiveBoss != null && !ActiveBoss.IsDefeated && !ActiveBoss.IsFinale && InInstantViewport(ActiveBoss.Position))
                ActiveBoss.Damage(Mathf.Max(0, settings.instantSlash.bossDamage) * damageMultiplier);
        }

        void FinishInstantSlash()
        {
            // Restore before damage, because boss death can change state and show the victory UI.
            RestoreInstantClockAndColor();
            hud.HideInstantSlashHud();
            ClearInstantParticles();
            State = RunState.Playing;
            ApplyInstantSlashDamage();
            invulnerability = Mathf.Max(invulnerability, settings.hurtInvulnerability);
            if (State == RunState.Playing && PendingChoices > 0) OfferAugments();
        }

        void PrepareInstantMonochrome()
        {
            instantCameraData = worldCamera.GetUniversalAdditionalCameraData();
            instantPreviousPost = instantCameraData.renderPostProcessing;
            instantPreviousVolumeMask = instantCameraData.volumeLayerMask;
            instantCameraData.renderPostProcessing = true;
            instantCameraData.volumeLayerMask |= 1 << gameObject.layer;
            var obj = new GameObject("Instant Slash Monochrome"); obj.transform.SetParent(transform);
            obj.layer = gameObject.layer;
            instantVolume = obj.AddComponent<Volume>(); instantVolume.isGlobal = true;
            instantVolume.priority = 10000; instantVolume.weight = 0;
            instantProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            instantMonochrome = instantProfile.Add<ColorAdjustments>(true);
            instantMonochrome.saturation.Override(-100);
            instantMonochrome.contrast.Override(25);
            instantVolume.sharedProfile = instantProfile;
        }

        void RestoreInstantClockAndColor()
        {
            if (instantOwnsClock) { Time.timeScale = instantPreviousTimeScale; instantOwnsClock = false; }
            if (instantCameraData != null)
            {
                instantCameraData.renderPostProcessing = instantPreviousPost;
                instantCameraData.volumeLayerMask = instantPreviousVolumeMask;
                instantCameraData = null;
            }
            if (instantVolume != null) { instantVolume.weight = 0; Destroy(instantVolume.gameObject); instantVolume = null; }
            if (instantMonochrome != null) { Destroy(instantMonochrome); instantMonochrome = null; }
            if (instantProfile != null) { Destroy(instantProfile); instantProfile = null; }
        }

        void SpawnInstantParticles(ParticleSystem prefab, Vector3 position)
        {
            if (prefab == null) return;
            var effect = Instantiate(prefab, position, Quaternion.identity, world);
            foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true))
            { var main = system.main; main.useUnscaledTime = true; }
            effect.Play(true); instantParticles.Add(effect);
        }

        void ClearInstantParticles()
        {
            foreach (var effect in instantParticles) if (effect != null) Destroy(effect.gameObject);
            instantParticles.Clear();
        }

        void PauseInstantEffects(bool pause)
        {
            instantMousePrimed = false;
            if (pause) skillAudio.Pause(); else skillAudio.UnPause();
            foreach (var effect in instantParticles)
                if (effect != null) { if (pause) effect.Pause(true); else effect.Play(true); }
        }

        void PlayInstantSound(AudioClip clip, AudioClip fallback)
        {
            if (skillAudio != null && (clip != null || fallback != null))
                skillAudio.PlayOneShot(clip != null ? clip : fallback, settings.instantSlash.volume);
        }

        AudioClip MakeSlashSound(bool release)
        {
            const int rate = 22050;
            float duration = release ? .45f : .12f;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            uint seed = 1957;
            for (int i = 0; i < samples.Length; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float t = (float)i / rate, progress = t / duration;
                float noise = (seed & 65535) / 32767.5f - 1;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Min(1, progress * 7)) * Mathf.Exp(-progress * 3);
                if (progress > 1f / 7) envelope = Mathf.Exp(-progress * 5);
                samples[i] = (noise * .35f + Mathf.Sin(2 * Mathf.PI * (release ? 75 : 900) * t * (1 - progress * .4f)) * .25f) * envelope;
            }
            var clip = AudioClip.Create(release ? "Instant slash release" : "Instant slash cut", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); generatedAudio.Add(clip); return clip;
        }

        void CleanupInstantSlash()
        {
            RestoreInstantClockAndColor(); ClearInstantParticles();
            if (hud != null) hud.HideInstantSlashHud();
            instantTargets.Clear(); instantTrails.Clear();
        }

        void OnDisable()
        {
            bool active = State == RunState.InstantSlashing || (State == RunState.Paused && pausedState == RunState.InstantSlashing);
            CleanupInstantSlash();
            if (active) { State = RunState.Playing; if (hud != null) hud.HideOverlay(); }
        }
    }
}

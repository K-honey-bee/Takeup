using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        public RhythmJudge DanceJudge { get; private set; }
        public bool HasDance => StackCount(AugmentKind.StompDance) > 0;
        public float DanceCooldown { get; private set; }
        public float DashCooldown { get; private set; }
        public string DanceFeedback { get; private set; } = "";
        public bool DanceResolved { get; private set; }
        public bool DanceSucceeded { get; private set; }
        public double DanceTime => (audioPaused ? pauseStarted : AudioSettings.dspTime) - danceStart - dancePausedTime;
        public bool IsDashing => dashRemaining > 0;
        RunState pausedState = RunState.Playing;
        Vector2 lastDirection = Vector2.down, dashDirection;
        float dashRemaining, resolveRemaining, beatKick;
        double danceStart, dancePausedTime, pauseStarted;
        bool audioPaused;
        int cueIndex;
        Sprite idleSprite;
        Vector3 idleScale;
        Quaternion idleRotation;
        AudioSource musicSource, skillAudio;
        AudioClip defaultMusic, defaultHit, defaultWin, defaultFail;
        readonly List<AudioClip> generatedAudio = new List<AudioClip>();
        sealed class WaveEffect
        {
            public SpriteRenderer visual;
            public Vector2 origin;
            public float radius;
            public bool hitBoss, burning;
            public readonly HashSet<Enemy> hit = new HashSet<Enemy>();
        }
        readonly List<WaveEffect> shockwaves = new List<WaveEffect>();
        Sprite ring;

        void InitializeSkills()
        {
            if (settings.dance == null) settings.dance = new StompDanceSettings();
            if (settings.dance.startUnlocked) stacks[AugmentKind.StompDance] = 1;
            idleSprite = playerRenderer.sprite; idleScale = player.localScale;
            idleRotation = player.rotation;
            musicSource = gameObject.AddComponent<AudioSource>();
            skillAudio = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = skillAudio.playOnAwake = false;
            musicSource.spatialBlend = skillAudio.spatialBlend = 0;
            defaultHit = Tone("Stomp hit", 100, 0.14f, false);
            defaultWin = Tone("Stomp perfect", 180, 0.55f, false);
            defaultFail = Tone("Stomp oops", 220, 0.4f, true);
            ring = MakeRing();
        }

        public bool StartDash()
        {
            if (State != RunState.Playing || DashCooldown > 0 || dashRemaining > 0) return false;
            dashDirection = lastDirection;
            dashRemaining = Mathf.Max(0.05f, settings.dashDuration);
            DashCooldown = Mathf.Max(0.1f, settings.dashCooldown);
            if (settings.dashSound != null) skillAudio.PlayOneShot(settings.dashSound, settings.dance.volume);
            return true;
        }

        void TickDash(float dt)
        {
            float step = Mathf.Min(dt, dashRemaining);
            float speed = Mathf.Max(0.1f, settings.dashDistance) / Mathf.Max(0.05f, settings.dashDuration);
            Vector2 position = (Vector2)player.position + dashDirection * speed * step;
            player.position = ClampToArena(position);
            dashRemaining = Mathf.Max(0, dashRemaining - dt);
        }

        public bool StartDance()
        {
            var config = settings.dance;
            if (State != RunState.Playing || !HasDance || DanceCooldown > 0 || dashRemaining > 0
                || config.sequence == null || config.sequence.Length == 0) return false;
            DanceJudge = new RhythmJudge(config.sequence, config.firstBeatTime, config.bpm, config.hitWindow);
            DanceResolved = DanceSucceeded = false;
            DanceFeedback = "준비! 박자에 맞춰 A / D 또는 ← / →";
            cueIndex = 0; beatKick = 0;
            dancePausedTime = 0; audioPaused = false;
            danceStart = AudioSettings.dspTime + 0.12;
            if (config.music == null)
            {
                if (defaultMusic != null) { generatedAudio.Remove(defaultMusic); Destroy(defaultMusic); }
                defaultMusic = CreateRhythmTrack();
            }
            musicSource.clip = config.music != null ? config.music : defaultMusic;
            musicSource.volume = config.volume;
            musicSource.loop = false;
            musicSource.PlayScheduled(danceStart);
            State = RunState.Dancing;
            return true;
        }

        void TickDance(float dt)
        {
            // Slow only world simulation. Music, input judgement and camera use real time.
            float worldDt = Mathf.Min(dt, 0.05f) * Mathf.Clamp(settings.dance.enemyTimeScale, 0.01f, 0.5f);
            Elapsed += worldDt;
            if (bossEncounter != null) bossEncounter.Tick(worldDt); else TickSpawner(worldDt);
            TickBurns(worldDt); TickEnemies(worldDt); TickShots(worldDt);
            TickShockwaves(Mathf.Min(dt, 0.05f));
            if (State != RunState.Dancing) return;
            beatKick = Mathf.MoveTowards(beatKick, 0, dt * 1.8f);
            if (DanceResolved)
            {
                resolveRemaining -= dt;
                if (resolveRemaining <= 0) EndDance();
                return;
            }
            double time = DanceTime;
            while (cueIndex < DanceJudge.Count && time >= DanceJudge.TimeFor(cueIndex))
            {
                if (settings.dance.music != null && settings.dance.playMetronome)
                    skillAudio.PlayOneShot(settings.dance.beatSound != null ? settings.dance.beatSound : defaultHit, settings.dance.volume);
                cueIndex++;
            }
            var keys = Keyboard.current;
            if (keys != null)
            {
                bool left = keys.aKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame;
                bool right = keys.dKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame;
                if (left && right) ResolveDance(false);
                else if (left) SubmitDanceInput(BeatDirection.Left);
                else if (right) SubmitDanceInput(BeatDirection.Right);
            }
            if (!DanceResolved)
            {
                DanceJudge.Advance(time - settings.dance.inputLatencyOffset);
                if (DanceJudge.Failed) ResolveDance(false);
            }
        }

        public void SubmitDanceInput(BeatDirection direction)
        {
            if (State != RunState.Dancing || DanceResolved) return;
            if (!DanceJudge.Press(direction, DanceTime - settings.dance.inputLatencyOffset)) { ResolveDance(false); return; }
            DanceFeedback = $"둥!  {DanceJudge.Hits} / {DanceJudge.Count}";
            beatKick = settings.dance.beatZoomKick;
            player.rotation = Quaternion.Euler(0, 0, direction == BeatDirection.Left ? 12 : -12);
            var frames = settings.dance.danceFrames;
            if (frames != null && frames.Length > 0)
            {
                var sprite = frames[(DanceJudge.Hits - 1) % frames.Length];
                if (sprite != null)
                {
                    playerRenderer.sprite = sprite;
                    player.localScale = Vector3.one * (settings.playerSize / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y, 0.001f));
                }
            }
            skillAudio.PlayOneShot(settings.dance.hitSound != null ? settings.dance.hitSound : defaultHit, settings.dance.volume);
            if (DanceJudge.Complete) ResolveDance(true);
        }

        void ResolveDance(bool success)
        {
            if (DanceResolved) return;
            DanceResolved = true; DanceSucceeded = success;
            resolveRemaining = success ? 0.8f : 0.65f;
            DanceFeedback = success ? "완벽!  팡!" : "잉...?  다시 박자를 맞춰봐!";
            DanceCooldown = Mathf.Max(0.1f, settings.dance.cooldown);
            musicSource.Stop(); skillAudio.Stop();
            AudioClip clip = success ? settings.dance.successSound : settings.dance.failureSound;
            skillAudio.PlayOneShot(clip != null ? clip : success ? defaultWin : defaultFail, settings.dance.volume);
            if (success)
            {
                var visual = Visual("Stomp shockwave", settings.dance.shockwaveSprite, ring, settings.dance.shockwaveColor,
                    Vector2.one * 0.1f, player.position, 15);
                shockwaves.Add(new WaveEffect { visual = visual, origin = player.position, burning = HasFire && StackCount(AugmentKind.FireDance) > 0 });
            }
            RestorePlayerArt();
        }

        void EndDance()
        {
            RestorePlayerArt();
            State = RunState.Playing;
            invulnerability = settings.hurtInvulnerability;
            if (PendingChoices > 0) OfferAugments();
            else if (bossEncounter == null && Elapsed >= settings.stageDuration) FinishStage();
        }

        void RestorePlayerArt()
        {
            playerRenderer.sprite = idleSprite;
            player.localScale = idleScale;
            player.rotation = idleRotation;
        }

        void TickSkills(float dt)
        {
            DanceCooldown = Mathf.Max(0, DanceCooldown - dt);
            DashCooldown = Mathf.Max(0, DashCooldown - dt);
            TickShockwaves(dt);
        }

        void TickShockwaves(float dt)
        {
            for (int i = shockwaves.Count - 1; i >= 0; i--)
            {
                var wave = shockwaves[i];
                wave.radius = Mathf.Min(settings.dance.radius, wave.radius + settings.dance.waveSpeed * dt);
                wave.visual.transform.localScale = Vector3.one * (wave.radius * 2 / Mathf.Max(wave.visual.sprite.bounds.size.x, 0.001f));
                Color color = wave.burning ? FlameColor : settings.dance.shockwaveColor;
                color.a *= 1 - wave.radius / Mathf.Max(1, settings.dance.radius);
                wave.visual.color = color;
                if (!wave.hitBoss && bossEncounter != null && Vector2.Distance(bossEncounter.Position, wave.origin) <= wave.radius + bossEncounter.Radius)
                { wave.hitBoss = true; bossEncounter.Damage(settings.dance.damage * damageMultiplier); if (wave.burning) IgniteBoss(); }
                for (int j = enemies.Count - 1; j >= 0; j--)
                {
                    Enemy enemy = enemies[j];
                    if (wave.hit.Contains(enemy) || Vector2.Distance(enemy.body.position, wave.origin) > wave.radius + enemy.radius) continue;
                    wave.hit.Add(enemy);
                    if (wave.burning) Ignite(enemy);
                    enemy.health -= settings.dance.damage * damageMultiplier; enemy.flash = 0.15f;
                    if (enemy.health <= 0)
                    {
                        KillEnemy(j);
                    }
                }
                if (wave.radius >= settings.dance.radius)
                {
                    Destroy(wave.visual.gameObject); shockwaves.RemoveAt(i);
                }
            }
        }

        void UpdateSkillCamera()
        {
            if (State == RunState.Paused || State == RunState.Codex || State == RunState.ChoosingAugment) return;
            float size = baseCameraSize;
            if (State == RunState.Dancing && !DanceResolved)
            {
                float progress = DanceJudge == null ? 0 : (float)DanceJudge.Hits / DanceJudge.Count;
                size *= Mathf.Lerp(1, settings.dance.finalZoomRatio, progress);
                size -= beatKick;
            }
            // Exponential return works equally for failure, success and ordinary play.
            worldCamera.orthographicSize = Mathf.Lerp(worldCamera.orthographicSize, Mathf.Max(1, size),
                1 - Mathf.Exp(-settings.dance.cameraReturnSpeed * Time.unscaledDeltaTime));
        }

        public void ToggleCodex()
        {
            if (State == RunState.Playing) { State = RunState.Codex; hud.ShowCodex(); }
            else if (State == RunState.Codex)
            {
                State = RunState.Playing; hud.HideOverlay();
                if (PendingChoices > 0) OfferAugments();
            }
        }

        void PauseSkillAudio()
        {
            if (State != RunState.Dancing) return;
            pauseStarted = AudioSettings.dspTime; audioPaused = true;
            musicSource.Pause(); skillAudio.Pause();
        }

        void ResumeSkillAudio()
        {
            if (!audioPaused) return;
            double duration = AudioSettings.dspTime - pauseStarted;
            dancePausedTime += duration; audioPaused = false;
            // Scheduled clips paused before they start must be re-scheduled, not merely unpaused.
            if (pauseStarted < danceStart)
            {
                musicSource.Stop(); danceStart += duration; dancePausedTime -= duration;
                if (!DanceResolved) musicSource.PlayScheduled(danceStart);
            }
            else musicSource.UnPause();
            skillAudio.UnPause();
        }

        AudioClip Tone(string label, float frequency, float duration, bool fall)
        {
            const int rate = 22050;
            float[] data = new float[Mathf.CeilToInt(rate * duration)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float phase = fall ? frequency * t - frequency * 0.7f * t * t / (2 * duration) : frequency * t;
                data[i] = Mathf.Sin(2 * Mathf.PI * phase) * Mathf.Exp(-t * 8 / duration) * 0.6f;
            }
            var clip = AudioClip.Create(label, data.Length, 1, rate, false); clip.SetData(data, 0); generatedAudio.Add(clip); return clip;
        }

        AudioClip CreateRhythmTrack()
        {
            const int rate = 22050;
            double interval = 60 / Mathf.Max(40, settings.dance.bpm);
            double end = DanceJudge.TimeFor(DanceJudge.Count - 1) + 1;
            float[] data = new float[Mathf.CeilToInt((float)end * rate)];
            for (int beat = -Mathf.CeilToInt((float)(settings.dance.firstBeatTime / interval)); beat < DanceJudge.Count; beat++)
            {
                int start = Mathf.RoundToInt((float)(DanceJudge.TimeFor(0) + beat * interval) * rate);
                if (start < 0) continue;
                for (int sample = 0; sample < rate / 5 && start + sample < data.Length; sample++)
                {
                    float t = (float)sample / rate;
                    data[start + sample] += Mathf.Sin(2 * Mathf.PI * (beat % 2 == 0 ? 90 : 140) * t) * Mathf.Exp(-t * 28) * 0.55f;
                }
            }
            var clip = AudioClip.Create("Prototype stomp rhythm", data.Length, 1, rate, false);
            clip.SetData(data, 0); generatedAudio.Add(clip); return clip;
        }

        Sprite MakeRing()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float radius = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64;
                pixels[y * size + x] = radius > 0.88f && radius < 0.96f ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            generatedSprites.Add(sprite); return sprite;
        }

        void CleanupSkills()
        {
            if (musicSource != null) musicSource.Stop();
            if (skillAudio != null) skillAudio.Stop();
            foreach (var clip in generatedAudio) if (clip != null) Destroy(clip);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public sealed partial class OctopusBossEncounter
    {
        sealed class TentacleStrike
        {
            public SpriteRenderer warning, arm;
            public Vector2 center, size;
            public float remaining, duration;
            public bool hit, circular;
        }
        sealed class InkBomb { public SpriteRenderer visual; public float remaining; }
        sealed class InkShot { public SpriteRenderer visual; public Vector2 velocity; public float life = 5; }
        readonly List<TentacleStrike> mobileStrikes = new List<TentacleStrike>();
        readonly List<InkBomb> inkBombs = new List<InkBomb>();
        readonly List<InkShot> inkShots = new List<InkShot>();
        readonly List<Image> screenBlots = new List<Image>();
        Vector2 glideFrom, glideTo;
        Vector3 bodyArtScale;
        float glideTime, pauseTime, pressureTimer = 1, trackingTimer, bombTimer = 2;
        float blotRemaining, blotCooldown;
        int trackingLeft, pressureIndex;
        bool gliding;
        GameObject inkOverlay;
        AudioSource inkAudio;
        AudioClip inkSound;
        Texture2D blotTexture;
        Sprite blotSprite;
        bool ProtectedColorPhase => IsFinale || Phase == OctopusPhase.MemoryShow || Phase == OctopusPhase.MemoryHide || Phase == OctopusPhase.MemoryBlast;
        float PressureSpeed => Health <= MaximumHealth * .35f ? .75f : 1;
        Rect MobileArena => Rect.MinMaxRect(
            game.westBoundary != null ? game.westBoundary.position.x : -game.settings.arenaHalfSize.x,
            game.southBoundary != null ? game.southBoundary.position.y : -game.settings.arenaHalfSize.y,
            game.eastBoundary != null ? game.eastBoundary.position.x : game.settings.arenaHalfSize.x,
            game.northBoundary != null ? game.northBoundary.position.y : game.settings.arenaHalfSize.y);

        void InitializeMobilePatterns()
        {
            bodyArtScale = bodyArtwork.transform.localScale;
            inkOverlay = new GameObject("Temporary Screen Ink", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            inkOverlay.transform.SetParent(transform, false);
            var canvas = inkOverlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = inkOverlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            blotTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                Vector2 p = new Vector2((x - 63.5f) / 64, (y - 63.5f) / 64);
                float angle = Mathf.Atan2(p.y, p.x);
                float edge = .65f + .12f * Mathf.Sin(angle * 5) + .07f * Mathf.Cos(angle * 9);
                pixels[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01((edge - p.magnitude) * 45));
            }
            blotTexture.SetPixels(pixels); blotTexture.Apply();
            blotSprite = Sprite.Create(blotTexture, new Rect(0, 0, 128, 128), Vector2.one * .5f);
            for (int i = 0; i < 3; i++)
            {
                var image = new GameObject("Ink Splatter", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(inkOverlay.transform, false); image.sprite = blotSprite;
                image.raycastTarget = false; image.color = Color.clear; screenBlots.Add(image);
            }
            // Short deterministic wet noise: no external audio asset or scene edits required.
            const int sampleRate = 22050;
            var samples = new float[sampleRate / 4]; var random = new System.Random(73);
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, .3f);
                samples[i] = (filtered * .7f + Mathf.Sin(i * (.055f - .025f * t)) * .3f) * (1 - t) * Mathf.Min(1, t * 35);
            }
            inkSound = AudioClip.Create("Ink Squelch", samples.Length, 1, sampleRate, false); inkSound.SetData(samples, 0);
            inkAudio = gameObject.AddComponent<AudioSource>(); inkAudio.playOnAwake = false; inkAudio.spatialBlend = 0;
        }

        void TickMobilePatterns(float dt)
        {
            TickScreenInk(dt);
            if (!settings.mobilePatterns || ProtectedColorPhase || Phase == OctopusPhase.Intro) return;
            TickGlide(dt);
            pressureTimer -= dt; bombTimer -= dt;
            if (pressureTimer <= 0 && trackingLeft == 0 && mobileStrikes.Count == 0
                && (Health <= MaximumHealth * .7f || (inkBombs.Count == 0 && inkShots.Count == 0)))
            {
                if (pressureIndex++ % 2 == 0) { trackingLeft = Health <= MaximumHealth * .35f ? 6 : 4; trackingTimer = 0; }
                else BeginSideWave();
                pressureTimer = settings.pressureInterval * PressureSpeed;
            }
            if (trackingLeft > 0)
            {
                trackingTimer -= dt;
                if (trackingTimer <= 0)
                {
                    AddStrike(game.PlayerPosition, Vector2.one * settings.trackingRadius * 2, settings.trackingWarning * PressureSpeed, true);
                    trackingLeft--; trackingTimer = settings.trackingInterval * PressureSpeed;
                }
            }
            bool overlappingAllowed = Health <= MaximumHealth * .7f;
            if (bombTimer <= 0 && inkBombs.Count == 0 && (overlappingAllowed || (mobileStrikes.Count == 0 && trackingLeft == 0)))
            {
                SpawnInkBomb(); bombTimer = settings.bombInterval * PressureSpeed;
            }
            TickStrikes(dt); TickInkBombs(dt); TickInkShots(dt);
            foreach (var puddle in puddles)
                if (Vector2.Distance(game.PlayerPosition, puddle.visual.transform.position) <= settings.inkRadius) { SplashScreenInk(); break; }
        }

        void TickGlide(float dt)
        {
            // Keep the source of an already telegraphed sweep fixed until it finishes.
            if (Phase == OctopusPhase.SweepWarning || Phase == OctopusPhase.Sweep) return;
            if (!gliding)
            {
                pauseTime -= dt;
                float anticipation = Mathf.Clamp01(1 - pauseTime / .25f);
                bodyArtwork.transform.localScale = Vector3.Scale(bodyArtScale, new Vector3(1 + anticipation * .08f, 1 - anticipation * .08f, 1));
                if (pauseTime > 0) return;
                var arena = MobileArena;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    Vector2 candidate = Position + Random.insideUnitCircle.normalized * Random.Range(settings.glideDistance * .6f, settings.glideDistance);
                    candidate.x = Mathf.Clamp(candidate.x, arena.xMin + Radius + .5f, arena.xMax - Radius - .5f);
                    candidate.y = Mathf.Clamp(candidate.y, arena.yMin + Radius + .5f, arena.yMax - Radius - .5f);
                    if (Vector2.Distance(candidate, Position) < 1 || !ClearGlidePath(Position, candidate)) continue;
                    glideFrom = Position; glideTo = candidate; glideTime = 0; gliding = true; break;
                }
                if (!gliding) pauseTime = .3f;
                return;
            }
            glideTime += dt;
            float t = Mathf.Clamp01(glideTime / Mathf.Max(.2f, settings.glideDuration));
            Vector2 next = Vector2.Lerp(glideFrom, glideTo, 1 - Mathf.Pow(1 - t, 3));
            // Do not sweep the boss collider through a player who crossed its path.
            if (SegmentDistance(game.PlayerPosition, Position, next) < Radius + .65f) t = 1;
            else bossBody.position = new Vector3(next.x, next.y, bossBody.position.z);
            float stretch = Mathf.Sin(t * Mathf.PI) * .13f;
            bodyArtwork.transform.localScale = Vector3.Scale(bodyArtScale, new Vector3(1 + stretch, 1 - stretch, 1));
            if (t >= 1) { gliding = false; pauseTime = settings.glidePause * PressureSpeed; bodyArtwork.transform.localScale = bodyArtScale; }
        }

        bool ClearGlidePath(Vector2 from, Vector2 to)
        {
            if (MapWall.Cast(from, to, Radius, out _, out _)) return false;
            if (SegmentDistance(game.PlayerPosition, from, to) < Radius + 1) return false;
            foreach (var rock in rocks)
                if (SegmentDistance(rock.transform.position, from, to) < Radius + rock.WorldRadius + .3f) return false;
            return true;
        }

        SpriteRenderer MobileVisual(string label, Vector2 point, Vector2 size, Color color, bool arm = false)
        {
            var visual = Instantiate(arm ? sweepArm : inkPrefab, point, Quaternion.identity, effectsRoot);
            visual.name = label; visual.gameObject.SetActive(true); visual.color = color; visual.sortingOrder = arm ? 6 : 2;
            Size(visual, size); return visual;
        }

        void AddStrike(Vector2 center, Vector2 size, float delay, bool circular)
        {
            var warning = MobileVisual("Locked Tentacle Warning", center, size, new Color(1, .18f, .08f, .35f), !circular);
            var arm = MobileVisual("Slamming Tentacle", center, new Vector2(size.x * .65f, size.y), new Color(.65f, .24f, .55f), true);
            arm.gameObject.SetActive(false);
            mobileStrikes.Add(new TentacleStrike { warning = warning, arm = arm, center = center, size = size, remaining = delay, duration = delay, circular = circular });
        }

        void BeginSideWave()
        {
            Rect arena = MobileArena;
            bool fromLeft = Random.value > .5f;
            const int columns = 7;
            float width = arena.width / columns;
            // A continuous horizontal escape lane remains open through the entire wave.
            float gap = Mathf.Clamp(game.PlayerPosition.y, arena.yMin + 1.5f, arena.yMax - 1.5f);
            float gapHalf = 1.8f;
            for (int i = 0; i < columns; i++)
            {
                int column = fromLeft ? i : columns - 1 - i;
                float x = arena.xMin + (column + .5f) * width;
                float delay = settings.waveWarning + i * settings.waveInterval * PressureSpeed;
                float lower = gap - gapHalf - arena.yMin, upper = arena.yMax - gap - gapHalf;
                if (lower > .1f) AddStrike(new Vector2(x, arena.yMin + lower * .5f), new Vector2(width * .85f, lower), delay, false);
                if (upper > .1f) AddStrike(new Vector2(x, arena.yMax - upper * .5f), new Vector2(width * .85f, upper), delay, false);
            }
        }

        void TickStrikes(float dt)
        {
            for (int i = mobileStrikes.Count - 1; i >= 0; i--)
            {
                var strike = mobileStrikes[i]; strike.remaining -= dt;
                if (!strike.hit && strike.remaining <= 0)
                {
                    strike.hit = true; strike.remaining = .28f;
                    game.ShakeBossAttack();
                    strike.warning.gameObject.SetActive(false); strike.arm.gameObject.SetActive(true);
                    Vector2 delta = game.PlayerPosition - strike.center;
                    bool inside = strike.circular ? delta.magnitude <= strike.size.x * .5f + .3f
                        : Mathf.Abs(delta.x) <= strike.size.x * .5f + .3f && Mathf.Abs(delta.y) <= strike.size.y * .5f + .3f;
                    if (inside) game.DamagePlayer(settings.tentacleDamage);
                }
                else if (strike.hit)
                {
                    if (strike.remaining <= 0) { Destroy(strike.warning.gameObject); Destroy(strike.arm.gameObject); mobileStrikes.RemoveAt(i); }
                    else { var color = strike.arm.color; color.a = strike.remaining / .28f; strike.arm.color = color; }
                }
                else
                {
                    float progress = 1 - Mathf.Clamp01(strike.remaining / strike.duration);
                    strike.warning.color = new Color(1, .18f, .08f, .3f + .4f * progress);
                }
            }
        }

        void SpawnInkBomb()
        {
            Rect arena = MobileArena;
            Vector2 target = game.PlayerPosition + Random.insideUnitCircle * 2;
            target.x = Mathf.Clamp(target.x, arena.xMin + 1, arena.xMax - 1);
            target.y = Mathf.Clamp(target.y, arena.yMin + 1, arena.yMax - 1);
            inkBombs.Add(new InkBomb { visual = MobileVisual("Ink Bomb - Eight Way", target, Vector2.one, new Color(.35f, .12f, .48f)), remaining = settings.bombFuse });
        }

        void TickInkBombs(float dt)
        {
            for (int i = inkBombs.Count - 1; i >= 0; i--)
            {
                var bomb = inkBombs[i]; bomb.remaining -= dt;
                Size(bomb.visual, Vector2.one * Mathf.Lerp(.65f, 1.5f, 1 - Mathf.Clamp01(bomb.remaining / settings.bombFuse)));
                if (bomb.remaining > 0) continue;
                game.ShakeBossAttack();
                Vector2 point = bomb.visual.transform.position;
                for (int shot = 0; shot < 8; shot++)
                {
                    float angle = shot * Mathf.PI / 4;
                    inkShots.Add(new InkShot { visual = MobileVisual("Ink Projectile", point, Vector2.one * .45f, new Color(.22f, .05f, .35f)), velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * settings.inkShotSpeed });
                }
                AddInk(point); Destroy(bomb.visual.gameObject); inkBombs.RemoveAt(i);
            }
        }

        void TickInkShots(float dt)
        {
            Rect arena = MobileArena;
            for (int i = inkShots.Count - 1; i >= 0; i--)
            {
                var shot = inkShots[i]; Vector2 from = shot.visual.transform.position, to = from + shot.velocity * dt;
                bool hitWall = MapWall.Cast(from, to, .225f, out Vector2 wallPoint, out _);
                if (hitWall) to = wallPoint;
                shot.visual.transform.position = to; shot.life -= dt;
                bool hit = !MapWall.Blocks(from, game.PlayerPosition) && SegmentDistance(game.PlayerPosition, from, to) <= .5f;
                if (hit) { game.DamagePlayer(settings.inkDamage); SplashScreenInk(); }
                if (hitWall || hit || shot.life <= 0 || !arena.Contains(to)) { Destroy(shot.visual.gameObject); inkShots.RemoveAt(i); }
            }
        }

        void SplashScreenInk()
        {
            if (blotCooldown > 0 || ProtectedColorPhase) return;
            blotRemaining = settings.screenInkDuration; blotCooldown = settings.screenInkDuration + .35f;
            for (int i = 0; i < screenBlots.Count; i++)
            {
                var rect = screenBlots[i].rectTransform;
                Vector2 anchor = new Vector2(i == 1 ? .84f : .16f, i == 2 ? .72f : .26f);
                rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(440, 300); rect.localRotation = Quaternion.Euler(0, 0, Random.Range(-35, 35));
            }
            if (inkAudio != null) inkAudio.PlayOneShot(inkSound, .55f);
        }

        void TickScreenInk(float dt)
        {
            blotRemaining = Mathf.Max(0, blotRemaining - dt); blotCooldown = Mathf.Max(0, blotCooldown - dt);
            float alpha = Mathf.Clamp01(blotRemaining / Mathf.Max(.1f, settings.screenInkDuration - .25f)) * .88f;
            Vector2 playerScreen = game.worldCamera.WorldToScreenPoint(game.PlayerPosition);
            Vector2 cursor = Mouse.current != null ? Mouse.current.position.ReadValue() : playerScreen;
            foreach (var blot in screenBlots)
            {
                blot.rectTransform.anchoredPosition += Vector2.down * (dt * 16);
                // Never obscure the player or cursor, even when they approach the screen edges.
                bool protectedPoint = RectTransformUtility.RectangleContainsScreenPoint(blot.rectTransform, playerScreen)
                    || RectTransformUtility.RectangleContainsScreenPoint(blot.rectTransform, cursor);
                blot.color = new Color(.065f, .025f, .09f, protectedPoint ? 0 : alpha);
            }
        }

        void ClearMobileHazards()
        {
            foreach (var strike in mobileStrikes) { Destroy(strike.warning.gameObject); Destroy(strike.arm.gameObject); }
            mobileStrikes.Clear();
            foreach (var bomb in inkBombs) Destroy(bomb.visual.gameObject); inkBombs.Clear();
            foreach (var shot in inkShots) Destroy(shot.visual.gameObject); inkShots.Clear();
            ClearInk(); trackingLeft = 0; pressureTimer = 1; bombTimer = 2; gliding = false; pauseTime = .3f;
            blotRemaining = blotCooldown = 0;
            foreach (var blot in screenBlots) blot.color = Color.clear;
            if (bodyArtwork != null && bodyArtScale != Vector3.zero) bodyArtwork.transform.localScale = bodyArtScale;
        }

        void DestroyMobilePatterns()
        {
            if (inkOverlay != null) Destroy(inkOverlay);
            if (inkSound != null) Destroy(inkSound);
            if (inkAudio != null) Destroy(inkAudio);
            if (blotSprite != null) Destroy(blotSprite);
            if (blotTexture != null) Destroy(blotTexture);
        }

        void LateUpdate()
        {
            if (game != null && (game.State == RunState.Dead || game.State == RunState.BossReady))
            {
                blotRemaining = 0;
                foreach (var blot in screenBlots) blot.color = Color.clear;
            }
        }
    }
}

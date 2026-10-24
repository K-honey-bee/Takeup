using System;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed class InstantSlashSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        static int phase;
        System.Collections.IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (SceneLoader.IsLoading && Time.realtimeSinceStartup < deadline) yield return null;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-instant-preview") >= 0)
            {
                Application.runInBackground = true;
                game.PrepareInstantPreview();
                yield return new WaitForSecondsRealtime(.65f);
                SavePreview("instant-dark.png");
                game.ReleaseInstantPreview();
                yield return new WaitForSecondsRealtime(.32f);
                SavePreview("instant-release.png");
                yield return new WaitForSecondsRealtime(1.3f);
                Application.Quit(0); yield break;
            }
            try
            {
                if (SceneLoader.IsLoading) throw new Exception("Instant slash loading timeout");
                if (phase == 0)
                {
                    game.ValidateInstantSlash(); phase = 1;
                    game.FinishStage(); game.EnterBossScene(); yield break;
                }
                game.ValidateInstantSlashBoss();
                Debug.Log("INSTANT SLASH PASS: weapon gating, augment unlock, swept mouse cuts, duplicate/stationary rejection, pause, clock restore, success/failure, 80-enemy execute, offscreen exclusion, boss damage, cleanup.");
                Application.Quit(0);
            }
            catch (Exception error) { Time.timeScale = 1; Debug.LogException(error); Application.Quit(1); }
        }

        void SavePreview(string filename)
        {
            // Hidden windows do not present a reliable backbuffer. Render the same camera/HUD explicitly.
            var canvas = game.GetComponentInChildren<Canvas>();
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance; int oldOrder = canvas.sortingOrder;
            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = game.worldCamera;
                canvas.planeDistance = 1; canvas.sortingOrder = 1000;
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(game.worldCamera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", filename)), texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance; canvas.sortingOrder = oldOrder;
                target.Release(); Destroy(target); if (texture != null) Destroy(texture);
            }
        }
    }

    public sealed partial class SurvivalGame
    {
        public void PrepareInstantPreview()
        {
            SelectWeapon(StartingWeapon.Sword);
            for (int i = 0; i < 12; i++) SpawnEnemy(settings.enemies[i % settings.enemies.Length],
                InstantViewportToWorld(new Vector2(.2f + i % 4 * .18f, .25f + i / 4 * .2f)));
            StartInstantSlash();
        }

        public void ReleaseInstantPreview()
        {
            while (instantTargets.Count < InstantTargetCount) SpawnInstantTarget();
            foreach (var target in instantTargets)
                if (!target.sliced) SubmitInstantSwipe(target.position - new Vector2(.1f, 0), target.position + new Vector2(.1f, 0));
        }

        void InstantCheck(bool condition, string label)
        { if (!condition) throw new Exception("Instant slash: " + label); }

        void CutAllInstantTargets()
        {
            while (instantTargets.Count < InstantTargetCount) SpawnInstantTarget();
            // One long sweep crosses every lane; hit testing must not depend on sampling at target centres.
            int hits = SubmitInstantSwipe(new Vector2(.05f, .86f), new Vector2(.95f, .86f));
            InstantCheck(hits == InstantTargetCount && InstantSucceeded, "fast drag hits all targets");
        }

        void CompleteInstantAnimation()
        {
            for (int i = 0; i < 100 && State == RunState.InstantSlashing; i++) TickInstantSlash(.1f);
        }

        public void ValidateInstantSlash()
        {
            InstantCheck(State == RunState.ChoosingWeapon, "selection first");
            SelectWeapon(StartingWeapon.Stone);
            InstantCheck(!StartInstantSlash() && !WeaponAllows(AugmentKind.InstantSlash), "stone cannot use instant slash");
            InstantCheck(StartWeaponSkill() && State == RunState.Dancing, "stone Q is dance");
            ResolveDance(false); EndDance();
            State = RunState.ChoosingWeapon; SelectWeapon(StartingWeapon.Sword);
            InstantCheck(!StartDance() && !WeaponAllows(AugmentKind.StompDance) && !WeaponAllows(AugmentKind.FireDance), "sword excludes dance and dance synergy");
            stacks.Remove(AugmentKind.InstantSlash);
            var card = Array.Find(settings.augments, a => a.kind == AugmentKind.InstantSlash);
            InstantCheck(card != null && CanOfferAugment(card) && !StartInstantSlash(), "locked skill offered as augment");
            ApplyAugment(card);
            InstantCheck(HasInstantSlash && !CanOfferAugment(card), "augment unlocks once");
            Time.timeScale = .5f;
            InstantCheck(StartWeaponSkill() && Time.timeScale == 0, "sword Q freezes time");
            Vector2 target = instantTargets[0].position;
            InstantCheck(SubmitInstantSwipe(target, target) == 0, "stationary cursor cannot slice");
            float elapsed = Elapsed, hp = Health;
            Vector3 position = player.position;
            MovePlayer(Vector2.up, 1); DamagePlayer(10); TickInstantSlash(.01f);
            InstantCheck(Elapsed == elapsed && Health == hp && player.position == position, "world state frozen");
            TogglePause();
            InstantCheck(State == RunState.Paused && Time.timeScale == 0, "pause freezes QTE");
            InstantCheck(SubmitInstantSwipe(Vector2.zero, Vector2.one) == 0, "pause ignores swipes");
            TogglePause();
            InstantCheck(State == RunState.InstantSlashing && !instantMousePrimed, "resume does not create phantom swipe");
            enabled = false;
            InstantCheck(Time.timeScale == .5f && instantVolume == null && State == RunState.Playing, "disable restores previous clock and camera");
            enabled = true; Time.timeScale = 1; InstantSlashCooldown = 0;

            // Clear only fixture enemies, then stress the uncapped execution with 80 visible targets.
            foreach (var enemy in enemies) Destroy(enemy.body.gameObject);
            enemies.Clear();
            for (int i = 0; i < 80; i++)
                SpawnEnemy(settings.enemies[0], InstantViewportToWorld(new Vector2(.2f + i % 10 * .06f, .2f + i / 10 * .07f)));
            SpawnEnemy(settings.enemies[0], InstantViewportToWorld(new Vector2(-.3f, .5f)));
            int kills = Kills;
            InstantCheck(StartInstantSlash(), "ready again");
            CutAllInstantTargets();
            InstantCheck(SubmitInstantSwipe(Vector2.zero, Vector2.one) == 0, "resolution cannot double-hit");
            InstantCheck(Kills == kills && instantVolume.weight == 1 && Time.timeScale == 0, "release is monochrome and still frozen");
            CompleteInstantAnimation();
            InstantCheck(Kills == kills + 80 && enemies.Count == 1, "executes all 80 visible enemies only");
            ApplyInstantSlashDamage();
            InstantCheck(Kills == kills + 80 && Time.timeScale == 1 && State == RunState.Playing, "damage once and world resumes");
            InstantCheck(!StartInstantSlash(), "cooldown gates skill");

            InstantSlashCooldown = 0;
            SpawnEnemy(settings.enemies[0], InstantViewportToWorld(new Vector2(.5f, .5f)));
            kills = Kills; StartInstantSlash();
            for (int i = 0; i < 100 && !InstantResolved; i++) TickInstantSlash(.1f);
            InstantCheck(InstantResolved && !InstantSucceeded, "missed target fails");
            CompleteInstantAnimation();
            InstantCheck(Kills == kills && Time.timeScale == 1 && instantVolume == null, "failure deals no damage and restores world");
        }

        public void ValidateInstantSlashBoss()
        {
            InstantCheck(ActiveBoss != null && HasInstantSlash, "boss entry retains instant slash");
            InstantSlashCooldown = 0;
            settings.instantSlash.bossDamage = 10;
            float before = ActiveBoss.Health;
            InstantCheck(StartInstantSlash(), "boss activation");
            CutAllInstantTargets(); CompleteInstantAnimation();
            InstantCheck(Mathf.Abs(ActiveBoss.Health - (before - 10 * damageMultiplier)) < .1f, "boss takes configured damage, not execution");
            InstantCheck(Time.timeScale == 1 && State == RunState.Playing, "boss fight resumes");
        }
    }
}

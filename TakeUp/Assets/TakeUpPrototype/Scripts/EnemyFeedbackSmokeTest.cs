using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeUp.Prototype
{
    public sealed class EnemyFeedbackSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-enemy-feedback-smoke") < 0) return;
            var runner = new GameObject("Enemy feedback validation");
            DontDestroyOnLoad(runner); runner.AddComponent<EnemyFeedbackSmokeTest>();
        }
        IEnumerator Start()
        {
            if (SceneManager.GetActiveScene().name != "PrehistoricPrototype") yield return SceneManager.LoadSceneAsync("PrehistoricPrototype");
            yield return null;
            try
            {
                var game = FindFirstObjectByType<SurvivalGame>(); game.enabled = false;
                var enemies = (IList)Field(game, "enemies");
                foreach (var enemy in enemies) Destroy(((Transform)Field(enemy, "body")).gameObject);
                enemies.Clear();
                var frame = (Sprite)Field(game, "circle"); var corpse = (Sprite)Field(game, "diamond");
                var config = new EnemyDefinition { health = 100, speed = 1, sprite = frame,
                    deathFrames = new[] { frame, corpse }, deathFramesPerSecond = 10, corpseSprite = corpse };
                Vector2 origin = game.PlayerPosition + Vector2.right * 3;
                Invoke(game, "SpawnEnemy", config, origin);
                var target = enemies[0]; var body = (Transform)Field(target, "body");
                Invoke(game, "Fire", Vector2.right, false);
                for (int i = 0; i < 40; i++) Invoke(game, "TickShots", .02f);
                Require((float)Field(target, "health") < 100, "projectile hit");
                Invoke(game, "TickEnemies", config.knockbackDuration);
                Require(Mathf.Abs(body.position.x - origin.x - config.knockbackDistance) < .001f, "hit pushes away exact configured distance");
                float pushedX = body.position.x; Invoke(game, "TickEnemies", .1f);
                Require(body.position.x < pushedX, "enemy resumes pursuit after knockback");
                int kills = game.Kills, drops = ((IList)Field(game, "gems")).Count;
                target.GetType().GetField("health", BindingFlags.Instance | BindingFlags.Public).SetValue(target, 1f);
                Invoke(game, "Fire", Vector2.right, false);
                for (int i = 0; i < 50; i++) Invoke(game, "TickShots", .02f);
                Require(enemies.Count == 0 && game.Kills == kills + 1, "lethal hit removes enemy once");
                Require(((IList)Field(game, "gems")).Count == drops + 1, "one experience drop");
                var deaths = (IList)Field(game, "enemyDeaths"); Require(deaths.Count == 1, "death visual remains");
                var visual = (SpriteRenderer)Field(deaths[0], "visual");
                Require(visual.sprite == frame && visual.GetComponent<Collider2D>() == null, "first frame and no corpse collision");
                Invoke(game, "TickEnemyDeaths", .21f);
                Require(visual.sprite == corpse && visual.enabled, "animation transitions to corpse");
                Invoke(game, "TickEnemyDeaths", .47f); Require(visual.enabled, "corpse holds for half a second");
                Invoke(game, "TickEnemyDeaths", .03f); Require(!visual.enabled, "blink off");
                Invoke(game, "TickEnemyDeaths", .1f); Require(visual.enabled, "blink on");
                Invoke(game, "TickEnemyDeaths", .6f); Require(deaths.Count == 0, "corpse expires after blinks");
                Require(game.Kills == kills + 1, "death visual grants no duplicate kill");
                Debug.Log("PASS: projectile knockback, pursuit resumes, death frames, corpse hold/blink/expiry, single reward, no corpse collision.");
                Application.Quit(0);
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
        }
        static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
    }
}

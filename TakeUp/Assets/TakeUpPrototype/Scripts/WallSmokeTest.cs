using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeUp.Prototype
{
    public sealed class WallSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-wall-smoke") < 0) return;
            var runner = new GameObject("Wall validation"); DontDestroyOnLoad(runner); runner.AddComponent<WallSmokeTest>();
        }
        IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync("PrehistoricPrototype"); yield return null;
            try
            {
                var game = FindFirstObjectByType<SurvivalGame>(); game.enabled = false;
                var enemies = (IList)Field(game, "enemies");
                foreach (var enemy in enemies) Destroy(((Transform)Field(enemy, "body")).gameObject); enemies.Clear();
                Vector2 origin = game.PlayerPosition;
                var wall = new GameObject("Test Wall", typeof(BoxCollider2D), typeof(MapWall));
                wall.transform.position = origin + Vector2.right * 2; wall.transform.localScale = new Vector3(.5f, 8, 1);
                Invoke(game, "SpawnEnemy", new EnemyDefinition { health = 1000 }, origin + Vector2.right * 4);
                var behind = enemies[0];
                Invoke(game, "SpawnEnemy", new EnemyDefinition { health = 1000 }, origin + Vector2.right);
                var front = enemies[1];
                Invoke(game, "Fire", Vector2.right, true); Invoke(game, "TickShots", .5f);
                Require((float)Field(front, "health") < 1000, "enemy before wall can be hit");
                Require((float)Field(behind, "health") == 1000, "enemy behind wall protected from piercing spear");
                Require(((IList)Field(game, "shots")).Count == 0, "wall consumes spear");
                Invoke(game, "Fire", Vector2.right, false); Invoke(game, "TickShots", .5f);
                Require((float)Field(behind, "health") == 1000, "wall blocks stone");
                game.MovePlayer(Vector2.right, 2);
                Require(game.PlayerPosition.x < wall.transform.position.x - .25f, "player cannot cross wall");
                game.scenePlayer.transform.position = origin;
                Invoke(game, "ThrowBone", Vector2.right);
                for (int i = 0; i < 150; i++) Invoke(game, "TickOrbitBone", .02f);
                Require(((IList)Field(game, "bones")).Count == 0 && (float)Field(behind, "health") == 1000, "bone returns without hitting through wall");
                wall.SetActive(false);
                Invoke(game, "Fire", Vector2.right, true); Invoke(game, "TickShots", .5f);
                Require((float)Field(behind, "health") < 1000, "disabled wall allows shots");
                Debug.Log("PASS: wall blocks stone/piercing spear, preserves front-target hit, blocks player, returns bone, disabling restores path.");
                Application.Quit(0);
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
        }
        static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static void Require(bool value, string label) { if (!value) throw new Exception(label); }
    }
}

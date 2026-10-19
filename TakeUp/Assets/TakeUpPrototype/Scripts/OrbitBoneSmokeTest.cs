using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeUp.Prototype
{
    public sealed class OrbitBoneSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-orbit-bone-smoke") < 0) return;
            var runner = new GameObject("Orbit bone validation"); DontDestroyOnLoad(runner); runner.AddComponent<OrbitBoneSmokeTest>();
        }
        IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync("PrehistoricPrototype"); yield return null;
            try
            {
                var game = FindFirstObjectByType<SurvivalGame>(); game.enabled = false;
                game.settings = Instantiate(game.settings);
                var config = game.settings.orbitBone = new OrbitBoneSettings();
                config.orbitFireSprite = (Sprite)Field(game, "circle"); config.boneSprite = (Sprite)Field(game, "diamond");
                var enemies = (IList)Field(game, "enemies");
                foreach (var enemy in enemies) Destroy(((Transform)Field(enemy, "body")).gameObject); enemies.Clear();
                Invoke(game, "ApplyAugment", new AugmentDefinition { kind = AugmentKind.OrbitFire });
                Invoke(game, "TickOrbitBone", .02f);
                var fires = (IList)Field(game, "orbitFires"); Require(fires.Count == 2, "first pickup gives two fires");
                Invoke(game, "ApplyAugment", new AugmentDefinition { kind = AugmentKind.OrbitFire }); Invoke(game, "TickOrbitBone", .02f);
                Require(fires.Count == 3, "second pickup adds one fire");
                game.scenePlayer.transform.position += Vector3.up;
                Invoke(game, "TickOrbitBone", .02f);
                foreach (SpriteRenderer fire in fires)
                    Require(Mathf.Abs(Vector2.Distance(fire.transform.position, game.PlayerPosition) - config.orbitRadius) < .001f && fire.sprite == config.orbitFireSprite, "fires follow player with assigned sprite");
                var type = new EnemyDefinition { health = 1000 };
                Invoke(game, "SpawnEnemy", type, (Vector2)((SpriteRenderer)fires[0]).transform.position);
                var target = enemies[0]; Invoke(game, "TickOrbitBone", 0f);
                float health = (float)Field(target, "health"); Require(health < 1000, "orbit deals contact damage");
                Invoke(game, "TickOrbitBone", 0f); Require((float)Field(target, "health") == health, "orbit contact cooldown");
                Destroy(((Transform)Field(target, "body")).gameObject); enemies.Clear(); config.orbitDamage = 0;
                Invoke(game, "SpawnEnemy", type, game.PlayerPosition + Vector2.right * 3); target = enemies[0];
                Invoke(game, "ThrowBone", Vector2.right);
                var bones = (IList)Field(game, "bones");
                Require(((SpriteRenderer)Field(bones[0], "visual")).sprite == config.boneSprite, "bone uses assigned sprite");
                for (int i = 0; i < 100; i++) Invoke(game, "TickOrbitBone", .02f);
                Require(Mathf.Approximately((float)Field(target, "health"), 1000 - config.boneDamage * 2), "bone hits once outbound and once returning");
                Require(bones.Count == 0, "bone caught and removed");
                Invoke(game, "ThrowBone", Vector2.right);
                for (int i = 0; i < 40; i++) Invoke(game, "TickOrbitBone", .02f);
                game.scenePlayer.transform.position += Vector3.up * 4;
                for (int i = 0; i < 150; i++) Invoke(game, "TickOrbitBone", .02f);
                Require(bones.Count == 0, "return tracks moving player");
                Require(Array.Exists(game.settings.augments, a => a != null && a.kind == AugmentKind.OrbitFire)
                    && Array.Exists(game.settings.augments, a => a != null && a.kind == AugmentKind.BoneBoomerang), "both cards registered");
                Debug.Log("PASS: orbit 2 then +1, sprite slots, follow player, contact cooldown, bone outbound/return hits and moving-player catch, augment registration.");
                Application.Quit(0);
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
        }
        static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TakeUp.Prototype.Editor
{
    public static class SceneLayoutSetup
    {
        const string Root = "Assets/TakeUpPrototype";
        const string ScenePath = Root + "/Scenes/PrehistoricPrototype.unity";

        [MenuItem("TakeUp/Create Editable Scene Layout")]
        public static void ConvertFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Convert();
        }

        // Idempotent: never rebuild a scene already converted or overwrite existing prefabs.
        public static void Convert()
        {
            PrototypeSetup.Create();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivalGame>();
            if (game == null) throw new Exception("Prototype controller missing");
            if (game.scenePlayer != null) { Debug.Log("Editable scene already exists; preserving authored layout."); return; }
            Directory.CreateDirectory(Root + "/Art/Placeholders");
            Directory.CreateDirectory(Root + "/Prefabs");
            var settings = game.settings;
            var circle = Placeholder("Circle", 0);
            var diamond = Placeholder("Diamond", 1);
            var square = Placeholder("Square", 2);
            var stage = new GameObject("Stage - Edit Layout").transform;
            var ground = Visual("Background", settings.groundSprite != null ? settings.groundSprite : square,
                settings.groundTint, settings.arenaHalfSize * 2, -20, stage);
            // No grid is generated at runtime; the background is now an ordinary scene sprite.
            var walls = new GameObject("Walls - Move To Set Play Area").transform; walls.SetParent(stage, false);
            Vector2 half = settings.arenaHalfSize;
            Color border = new Color(0.55f, 0.49f, 0.29f);
            game.northBoundary = Visual("North", square, border, new Vector2(half.x * 2, 0.15f), -10, walls).transform;
            game.southBoundary = Visual("South", square, border, new Vector2(half.x * 2, 0.15f), -10, walls).transform;
            game.eastBoundary = Visual("East", square, border, new Vector2(0.15f, half.y * 2), -10, walls).transform;
            game.westBoundary = Visual("West", square, border, new Vector2(0.15f, half.y * 2), -10, walls).transform;
            game.northBoundary.localPosition = new Vector3(0, half.y);
            game.southBoundary.localPosition = new Vector3(0, -half.y);
            game.eastBoundary.localPosition = new Vector3(half.x, 0);
            game.westBoundary.localPosition = new Vector3(-half.x, 0);

            var playerPrefab = Prefab("Player", settings.playerSprite != null ? settings.playerSprite : circle,
                settings.playerTint, Vector2.one * settings.playerSize, 10);
            game.scenePlayer = ((GameObject)PrefabUtility.InstantiatePrefab(playerPrefab.gameObject, scene)).GetComponent<SpriteRenderer>();
            game.scenePlayer.name = "Player - Start Position";
            game.scenePlayer.transform.SetParent(stage, true);
            game.runtimeRoot = new GameObject("Spawned Objects").transform;
            game.runtimeRoot.SetParent(game.transform, false);
            game.stonePrefab = Prefab("Stone", settings.stoneSprite != null ? settings.stoneSprite : circle, settings.stoneTint, Vector2.one * settings.stoneSize, 12);
            game.spearPrefab = Prefab("Spear", settings.spearSprite != null ? settings.spearSprite : square, settings.spearTint, settings.spearSize, 12);
            game.experiencePrefab = Prefab("Experience", settings.experienceSprite != null ? settings.experienceSprite : diamond, settings.experienceTint, Vector2.one * 0.25f, 5);
            for (int i = 0; i < settings.enemies.Length; i++)
            {
                var enemy = settings.enemies[i];
                if (enemy.prefab == null) enemy.prefab = Prefab("Enemy_" + (i + 1), enemy.sprite != null ? enemy.sprite : diamond, enemy.tint, Vector2.one * enemy.size, 9);
            }
            game.worldCamera.orthographic = true;
            game.worldCamera.orthographicSize = settings.cameraSize;
            game.worldCamera.clearFlags = CameraClearFlags.SolidColor;
            game.worldCamera.backgroundColor = new Color(0.035f, 0.06f, 0.055f);
            if (settings.dance == null) settings.dance = new StompDanceSettings();
            game.hud.BakeLayout(game);
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Editable layout saved: scene player, background, boundaries, all HUD panels and seven reusable prefabs.");
        }

        static SpriteRenderer Prefab(string name, Sprite sprite, Color tint, Vector2 size, int order)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<SpriteRenderer>();
            var renderer = Visual(name, sprite, tint, size, order, null);
            var saved = PrefabUtility.SaveAsPrefabAsset(renderer.gameObject, path);
            UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            return saved.GetComponent<SpriteRenderer>();
        }

        static SpriteRenderer Visual(string name, Sprite sprite, Color tint, Vector2 size, int order, Transform parent)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false);
            renderer.sprite = sprite; renderer.color = tint; renderer.sortingOrder = order;
            Vector2 bounds = sprite.bounds.size;
            renderer.transform.localScale = Mathf.Approximately(size.x, size.y)
                ? Vector3.one * (size.x / Mathf.Max(bounds.x, bounds.y, 0.001f))
                : new Vector3(size.x / Mathf.Max(bounds.x, 0.001f), size.y / Mathf.Max(bounds.y, 0.001f), 1);
            return renderer;
        }

        static Sprite Placeholder(string name, int shape)
        {
            string path = Root + "/Art/Placeholders/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var pixels = new Color[1024];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    Vector2 p = new Vector2(x - 15.5f, y - 15.5f) / 16;
                    pixels[y * 32 + x] = shape == 2 || (shape == 0 ? p.sqrMagnitude < 0.88f : Mathf.Abs(p.x) + Mathf.Abs(p.y) < 0.95f) ? Color.white : Color.clear;
                }
                texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void ConvertAndBuild()
        {
            Convert();
            ValidateSavedLayout();
            PrototypeSetup.BuildSmokePlayer();
        }

        public static void ValidateSavedLayout()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivalGame>();
            if (game.scenePlayer == null || game.stonePrefab == null || game.spearPrefab == null || game.experiencePrefab == null)
                throw new Exception("Missing serialized scene or prefab bindings");
            foreach (var enemy in game.settings.enemies) if (enemy.prefab == null) throw new Exception("Missing enemy prefab");
            var slots = game.hud.GetComponentsInChildren<HudLayoutSlot>(true);
            if (slots.Length < 100) throw new Exception("UI was not saved into the scene");
            foreach (var slot in slots)
            {
                if (slot.GetComponent<UnityEngine.UI.Text>() is UnityEngine.UI.Text label && label.font == null)
                    throw new Exception("Missing font: " + label.name);
            }
            Debug.Log("PASS: serialized layout reloaded with " + slots.Length + " UI slots and prefab references.");
        }
    }
}

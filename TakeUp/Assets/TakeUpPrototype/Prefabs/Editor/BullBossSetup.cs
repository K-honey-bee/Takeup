using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

namespace TakeUp.Prototype.Editor
{
    public static class BullBossSetup
    {
        const string Root = "Assets/TakeUpPrototype";
        const string Scene = Root + "/Scenes/BullBossPrototype.unity";
        static Sprite square, circle;
        static Material material;
        [MenuItem("TakeUp/Open Bull Boss Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create(); EditorSceneManager.OpenScene(Scene);
        }
        public static void Create()
        {
            if (!File.Exists(Scene))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Placeholders/Square.png");
                circle = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Placeholders/Circle.png");
                material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/BullWhite.mat");
                if (material == null) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, Root + "/Art/BullWhite.mat"); }
                var source = AssetDatabase.LoadAssetAtPath<SurvivalSettings>(Root + "/Data/PrehistoricSettings.asset");
                var config = AssetDatabase.LoadAssetAtPath<SurvivalSettings>(Root + "/Data/BullPlayerSettings.asset");
                if (config == null)
                {
                    config = UnityEngine.Object.Instantiate(source); config.name = "BullPlayerSettings";
                    config.arenaHalfSize = new Vector2(12, 8); config.attackRange = 30; config.cameraSize = 14;
                    config.playerSprite = config.stoneSprite = config.spearSprite = null;
                    config.playerTint = config.stoneTint = config.spearTint = Color.white;
                    AssetDatabase.CreateAsset(config, Root + "/Data/BullPlayerSettings.asset");
                }
                var settings = AssetDatabase.LoadAssetAtPath<BullBossSettings>(Root + "/Data/BullBossSettings.asset");
                if (settings == null) { settings = ScriptableObject.CreateInstance<BullBossSettings>(); AssetDatabase.CreateAsset(settings, Root + "/Data/BullBossSettings.asset"); }
                var controller = new GameObject("Bull Fight Controller");
                var game = controller.AddComponent<SurvivalGame>(); game.settings = config;
                var boss = controller.AddComponent<BullBossEncounter>(); game.bullEncounter = boss; boss.game = game; boss.settings = settings;
                game.runtimeRoot = new GameObject("Runtime Effects").transform;
                var stage = new GameObject("Bull Arena - Edit White Placeholders").transform;
                Visual("Ground", square, Vector2.zero, new Vector2(24, 16), stage, -20).color = new Color(.12f, .12f, .12f);
                game.northBoundary = Visual("North Boundary", square, new Vector2(0, 8), new Vector2(24, .15f), stage, 0).transform;
                game.southBoundary = Visual("South Boundary", square, new Vector2(0, -8), new Vector2(24, .15f), stage, 0).transform;
                game.eastBoundary = Visual("East Boundary", square, new Vector2(12, 0), new Vector2(.15f, 16), stage, 0).transform;
                game.westBoundary = Visual("West Boundary", square, new Vector2(-12, 0), new Vector2(.15f, 16), stage, 0).transform;
                game.scenePlayer = Visual("Player - Replace Sprite", circle, new Vector2(0, -5), Vector2.one * .8f, stage, 10);
                boss.bossBody = new GameObject("Bull - Move Here").transform; boss.bossBody.SetParent(stage); boss.bossBody.position = Vector3.zero;
                boss.bodyArtwork = Visual("Bull Body - Replace Sprite", circle, Vector2.zero, new Vector2(2, 2), boss.bossBody, 8);
                var wallRoot = new GameObject("Charge Walls - 5 7 9 11 o'clock").transform; wallRoot.SetParent(stage);
                boss.walls = new SpriteRenderer[4];
                int[] hours = { 5, 7, 9, 11 };
                for (int i = 0; i < hours.Length; i++)
                {
                    float angle = (90 - hours[i] * 30) * Mathf.Deg2Rad;
                    Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 6.5f;
                    var wall = Visual("Wall " + hours[i] + " o'clock - Replace Sprite", square, position, new Vector2(2.8f, .6f), wallRoot, 6);
                    wall.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90);
                    boss.walls[i] = wall;
                }
                boss.waveVisual = Line("Stomp Wave - White Arc", stage, 4, 1);
                boss.chargeVisual = Line("Charge Preview - White Lane", stage, 3, .2f);
                game.worldCamera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                game.worldCamera.tag = "MainCamera"; game.worldCamera.orthographic = true; game.worldCamera.orthographicSize = 14;
                game.worldCamera.transform.position = new Vector3(0, 3, -10); game.worldCamera.backgroundColor = new Color(.055f, .055f, .055f);
                game.worldCamera.clearFlags = CameraClearFlags.SolidColor; game.followPlayer = false;
                game.hud = controller.AddComponent<SurvivalHud>(); game.hud.BakeLayout(game);
                var canvas = game.hud.GetComponentInChildren<Canvas>(true).transform;
                var panel = Rect("Bull HUD", canvas, new Vector2(0, -135), new Vector2(1100, 88));
                var bar = Rect("Health Background", panel, Vector2.zero, new Vector2(660, 22)); bar.gameObject.AddComponent<Image>().color = Color.gray;
                var fill = Rect("Health Fill", bar, Vector2.zero, Vector2.zero); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
                boss.healthFill = fill.gameObject.AddComponent<Image>(); boss.healthFill.color = Color.white;
                boss.healthText = Text("Bull Health", panel, Vector2.zero, new Vector2(660, 22), 16); boss.healthText.color = Color.black;
                boss.instructionText = Text("Bull Instruction", panel, new Vector2(0, -34), new Vector2(1100, 50), 19);
                panel.SetSiblingIndex(0);
                EditorSceneManager.SaveScene(scene, Scene); AssetDatabase.SaveAssets();
            }
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == Scene)) scenes.Add(new EditorBuildSettingsScene(Scene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        static SpriteRenderer Visual(string name, Sprite sprite, Vector2 position, Vector2 size, Transform parent, int order)
        {
            var r = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>(); r.transform.SetParent(parent, false);
            r.transform.localPosition = position; r.sprite = sprite; r.color = Color.white; r.sharedMaterial = material; r.sortingOrder = order;
            r.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1); return r;
        }
        static LineRenderer Line(string name, Transform parent, int order, float alpha)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.sharedMaterial = material; line.useWorldSpace = true; line.sortingOrder = order;
            line.startColor = line.endColor = new Color(1, 1, 1, alpha); line.numCapVertices = 4;
            line.gameObject.SetActive(false); return line;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, 1); r.anchoredPosition = position; r.sizeDelta = size; return r;
        }
        static Text Text(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var t = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize = fontSize; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; return t;
        }
        public static void AdjustPreviewAndBuild()
        {
            EditorSceneManager.OpenScene(Scene);
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivalGame>();
            game.worldCamera.orthographicSize = game.settings.cameraSize = 14;
            game.worldCamera.transform.position = new Vector3(0, 3, -10);
            game.settings.playerSprite = game.settings.stoneSprite = game.settings.spearSprite = null;
            EditorUtility.SetDirty(game.settings); AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            CreateAndBuild();
            OctopusBossSetup.CreateAndBuild();
        }
        public static void CreateAndBuild()
        {
            Create(); EditorSceneManager.OpenScene(Scene);
            string path = Path.GetFullPath("Builds/BullPrototype/TakeUpBull.exe"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Scene, Root + "/Scenes/PrehistoricPrototype.unity", Root + "/Scenes/OctopusBossPrototype.unity" }, locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Bull build failed");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype.Editor
{
    public static class OctopusBossSetup
    {
        const string Root = "Assets/TakeUpPrototype";
        const string BossScene = Root + "/Scenes/OctopusBossPrototype.unity";
        const string SurvivalScene = Root + "/Scenes/PrehistoricPrototype.unity";
        static Sprite circle, square;
        static Material spriteMaterial;
        [MenuItem("TakeUp/Open Octopus Boss Scene")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(BossScene)) Create();
            EditorSceneManager.OpenScene(BossScene);
        }
        public static void Create()
        {
            if (File.Exists(BossScene)) { Register(); return; }
            var scene = EditorSceneManager.OpenScene(SurvivalScene);
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivalGame>();
            // Add the stage-to-boss button while retaining every authored UI transform.
            game.hud.BakeLayout(game);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.SaveScene(scene, BossScene);
            game.name = "TakeUp - Boss Controller";
            var originalSettings = game.settings;
            var playerSettings = UnityEngine.Object.Instantiate(originalSettings);
            playerSettings.name = "BossPlayerSettings"; playerSettings.attackRange = 25;
            playerSettings.arenaHalfSize = new Vector2(12, 8);
            AssetDatabase.CreateAsset(playerSettings, Root + "/Data/BossPlayerSettings.asset");
            game.settings = playerSettings;
            game.followPlayer = false;
            game.worldCamera.transform.position = new Vector3(0, 0, -10);
            game.worldCamera.orthographicSize = 10.5f;
            game.worldCamera.backgroundColor = new Color(.025f, .07f, .1f);
            game.scenePlayer.transform.position = new Vector3(0, -6.5f);
            var stage = GameObject.Find("Stage - Edit Layout").transform;
            stage.name = "Boss Arena - Edit Layout";
            circle = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Placeholders/Circle.png");
            square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Placeholders/Square.png");
            var ground = stage.Find("Background").GetComponent<SpriteRenderer>();
            spriteMaterial = ground.sharedMaterial;
            ground.sprite = square; ground.color = new Color(.13f, .24f, .27f);
            ground.transform.localScale = new Vector3(24, 16, 1);
            game.northBoundary.position = new Vector3(0, 8); game.southBoundary.position = new Vector3(0, -8);
            game.eastBoundary.position = new Vector3(12, 0); game.westBoundary.position = new Vector3(-12, 0);
            game.northBoundary.localScale = game.southBoundary.localScale = new Vector3(24, .15f, 1);
            game.eastBoundary.localScale = game.westBoundary.localScale = new Vector3(.15f, 16, 1);

            var encounter = game.gameObject.AddComponent<OctopusBossEncounter>(); game.bossEncounter = encounter; encounter.game = game;
            var config = ScriptableObject.CreateInstance<OctopusBossSettings>();
            AssetDatabase.CreateAsset(config, Root + "/Data/OctopusBossSettings.asset"); encounter.settings = config;
            var body = new GameObject("Octopus - Move Boss Here").transform; body.SetParent(stage, false); body.position = new Vector3(0, 1);
            encounter.bossBody = body;
            var tentacles = new GameObject("Tentacles - Replace Art").transform; tentacles.SetParent(body, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * .25f;
                for (int j = 0; j < 3; j++)
                {
                    Vector2 p = new Vector2(Mathf.Cos(a + .12f * j), Mathf.Sin(a + .12f * j)) * (1.2f + .55f * j);
                    Visual("Arm " + (i + 1) + " - " + (j + 1), circle, new Color(.42f + j * .04f, .28f, .59f), new Vector2(.95f - j * .12f, .75f - j * .1f), p, 7, tentacles);
                }
            }
            encounter.bodyArtwork = Visual("Body - Replace Sprite", circle, new Color(.57f, .36f, .72f), new Vector2(3.5f, 3.1f), Vector2.zero, 9, body);
            Visual("Eye L", circle, new Color(.95f, .89f, .68f), new Vector2(.5f, .6f), new Vector2(-.65f, -.55f), 10, body);
            Visual("Eye R", circle, new Color(.95f, .89f, .68f), new Vector2(.5f, .6f), new Vector2(.65f, -.55f), 10, body);
            Visual("Pupil L", circle, new Color(.07f, .04f, .1f), new Vector2(.22f, .34f), new Vector2(-.65f, -.64f), 11, body);
            Visual("Pupil R", circle, new Color(.07f, .04f, .1f), new Vector2(.22f, .34f), new Vector2(.65f, -.64f), 11, body);

            var cover = new GameObject("Color Rocks - Move Each Rock").transform; cover.SetParent(stage, false);
            encounter.rocks = new BossCoverRock[4];
            var positions = new[] { new Vector2(-6, -1.5f), new Vector2(-2.2f, -3.4f), new Vector2(2.2f, -3.4f), new Vector2(6, -1.5f) };
            var labels = new[] { "1 RED", "2 BLUE", "3 YELLOW", "4 WHITE" };
            for (int i = 0; i < 4; i++)
            {
                var rockObject = new GameObject(labels[i]); rockObject.transform.SetParent(cover, false); rockObject.transform.localPosition = positions[i];
                var rock = rockObject.AddComponent<BossCoverRock>(); rock.colorIndex = i;
                rock.artwork = Visual("Rock Sprite", circle, OctopusBossEncounter.Colors[i], Vector2.one * 1.7f, Vector2.zero, 8, rock.transform);
                var label = new GameObject("Color Label", typeof(TextMesh)).GetComponent<TextMesh>(); label.transform.SetParent(rock.transform, false);
                label.text = labels[i]; label.anchor = TextAnchor.MiddleCenter; label.characterSize = .12f; label.fontSize = 40;
                label.transform.localPosition = new Vector3(0, .02f, -.1f); label.color = Color.black;
                label.GetComponent<MeshRenderer>().sortingOrder = 13;
                rock.safeMarker = Visual("Safe Side Hint", circle, new Color(.4f, 1, .7f, .35f), Vector2.one * 1.1f, Vector2.down * 1.5f, 2, rock.transform);
                rock.safeMarker.gameObject.SetActive(false); encounter.rocks[i] = rock;
            }
            var attacks = new GameObject("Attack Areas - Edit Slam Positions").transform; attacks.SetParent(stage, false);
            encounter.effectsRoot = new GameObject("Temporary Ink").transform; encounter.effectsRoot.SetParent(stage, false);
            encounter.sweepArm = Visual("Sweeping Tentacle", square, new Color(.72f, .3f, .56f), new Vector2(9, .8f), Vector2.zero, 6, attacks);
            var fanObject = new GameObject("Sweep Warning - Configured By Boss Settings", typeof(MeshFilter), typeof(MeshRenderer)); fanObject.transform.SetParent(attacks, false);
            encounter.sweepWarning = fanObject.GetComponent<MeshFilter>();
            var material = new Material(Shader.Find("Sprites/Default")); material.color = new Color(1, .55f, .12f, .25f);
            AssetDatabase.CreateAsset(material, Root + "/Art/BossTelegraph.mat");
            fanObject.GetComponent<MeshRenderer>().sharedMaterial = material; fanObject.GetComponent<MeshRenderer>().sortingOrder = 1;
            encounter.slamZones = new[] {
                Visual("Left Slam Zone", square, new Color(1, .55f, .12f, .4f), new Vector2(3.2f, 9), new Vector2(-4, -2), 1, attacks),
                Visual("Right Slam Zone", square, new Color(1, .55f, .12f, .4f), new Vector2(3.2f, 9), new Vector2(4, -2), 1, attacks) };
            var ink = Visual("Ink Puddle", circle, new Color(.12f, .06f, .19f, .8f), Vector2.one, Vector2.zero, 0, null);
            var inkPrefab = PrefabUtility.SaveAsPrefabAsset(ink.gameObject, Root + "/Prefabs/BossInk.prefab");
            encounter.inkPrefab = inkPrefab.GetComponent<SpriteRenderer>(); UnityEngine.Object.DestroyImmediate(ink.gameObject);
            encounter.sweepArm.gameObject.SetActive(false); fanObject.SetActive(false);
            foreach (var zone in encounter.slamZones) zone.gameObject.SetActive(false);
            BuildHud(encounter, game.hud.GetComponentInChildren<Canvas>(true).transform);
            game.hud.BakeLayout(game);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, BossScene);
            AssetDatabase.SaveAssets(); Register();
            Debug.Log("Octopus boss scene created with editable boss, rocks, attack zones, HUD and settings.");
        }
        static void Register()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == BossScene)) scenes.Add(new EditorBuildSettingsScene(BossScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        static SpriteRenderer Visual(string name, Sprite sprite, Color color, Vector2 size, Vector2 position, int order, Transform parent)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent, false); renderer.transform.localPosition = position;
            renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order; renderer.sharedMaterial = spriteMaterial;
            renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1); return renderer;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; return text;
        }
        static void BuildHud(OctopusBossEncounter boss, Transform canvas)
        {
            var panel = Rect("Boss HUD - Position Here", canvas, new Vector2(0, -132), new Vector2(1060, 100));
            var bar = Rect("Boss Health", panel, Vector2.zero, new Vector2(660, 20));
            bar.gameObject.AddComponent<Image>().color = new Color(.13f, .1f, .2f);
            var fill = Rect("Fill", bar, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            boss.healthFill = fill.gameObject.AddComponent<Image>(); boss.healthFill.color = new Color(.78f, .34f, .65f);
            boss.healthText = Label("Boss Health Text", panel, "고대 문어", Vector2.zero, new Vector2(660, 20), 16);
            boss.colorLights = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var cell = Rect("Memory Color " + (i + 1), panel, new Vector2(-66 + i * 44, -27), new Vector2(32, 16));
                boss.colorLights[i] = cell.gameObject.AddComponent<Image>(); boss.colorLights[i].color = OctopusBossEncounter.Colors[i];
            }
            boss.instructionText = Label("Boss Instructions", panel, "바위 뒤로 숨으세요", new Vector2(0, -48), new Vector2(1060, 27), 20);
            boss.phaseText = Label("Pattern Timer", panel, "", new Vector2(0, -77), new Vector2(1060, 22), 15);
            panel.SetSiblingIndex(0);
        }
        public static void CreateAndBuild()
        {
            Create();
            EditorSceneManager.OpenScene(BossScene);
            var boss = UnityEngine.Object.FindFirstObjectByType<OctopusBossEncounter>();
            if (boss == null || boss.rocks.Length != 4 || boss.game.bossEncounter != boss) throw new Exception("Boss scene bindings missing");
            Debug.Log("PASS: boss scene serialized references and four cover rocks.");
            string output = Path.GetFullPath("Builds/BossPrototype/TakeUpBoss.exe"); Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { BossScene, SurvivalScene },
                locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Boss build failed: " + report.summary.result);
            PrototypeSetup.BuildSmokePlayer();
        }
    }
}

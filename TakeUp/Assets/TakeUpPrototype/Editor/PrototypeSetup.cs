using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TakeUp.Prototype.Editor
{
    public static class PrototypeSetup
    {
        const string Folder = "Assets/TakeUpPrototype";
        const string ScenePath = Folder + "/Scenes/PrehistoricPrototype.unity";
        [MenuItem("TakeUp/Open Prototype Scene")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Create();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("TakeUp/Select Gameplay Settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<SurvivalSettings>(Folder + "/Data/PrehistoricSettings.asset");

        public static void Create()
        {
            Directory.CreateDirectory(Folder + "/Data");
            Directory.CreateDirectory(Folder + "/Scenes");
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<SurvivalSettings>(Folder + "/Data/PrehistoricSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SurvivalSettings>();
                settings.enemies = new[]
                {
                    new EnemyDefinition { displayName = "작은 야수", health = 28, speed = 1.65f, damage = 8, experience = 1, tint = new Color(0.8f, 0.36f, 0.3f) },
                    new EnemyDefinition { displayName = "날쌘 야수", firstWave = 2, health = 20, speed = 2.8f, damage = 6, experience = 2, size = 0.55f, spawnWeight = 0.5f, tint = new Color(0.8f, 0.65f, 0.28f) },
                    new EnemyDefinition { displayName = "큰 야수", firstWave = 3, health = 85, speed = 1.1f, damage = 16, experience = 4, size = 1.3f, spawnWeight = 0.3f, tint = new Color(0.53f, 0.4f, 0.75f) }
                };
                settings.augments = new[]
                {
                    Augment("날카로운 돌", "돌과 창의 피해량 +25%", AugmentKind.Damage, 0.25f, 8),
                    Augment("재빠른 손", "돌과 창의 공격 속도 +20%", AugmentKind.AttackSpeed, 0.2f, 6),
                    Augment("사냥꾼의 발", "이동 속도 +15%", AugmentKind.MoveSpeed, 0.15f, 4),
                    Augment("강인한 몸", "최대 체력 +25\n체력 25 회복", AugmentKind.MaxHealth, 25, 6),
                    Augment("채집 본능", "경험치 획득 반경 +1.2", AugmentKind.PickupRadius, 1.2f, 5),
                    Augment("한 줌의 돌", "동시에 던지는 돌 +1", AugmentKind.ExtraStone, 1, 3),
                    Augment("첫 번째 창", "자동 창 던지기 해금\n한 번에 적 3마리 관통", AugmentKind.Spear, 1, 1),
                    Augment("자연 회복", "매초 체력 1 회복", AugmentKind.Regeneration, 1, 4)
                };
                AssetDatabase.CreateAsset(settings, Folder + "/Data/PrehistoricSettings.asset");
            }
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 9;
                camera.transform.position = new Vector3(0, 0, -10);
                var game = new GameObject("TakeUp - Prototype Controller").AddComponent<SurvivalGame>();
                game.settings = settings; game.worldCamera = camera;
                game.hud = game.gameObject.AddComponent<SurvivalHud>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var existing in EditorBuildSettings.scenes) if (existing.path != ScenePath) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("TakeUp prototype scene and settings ready.");
        }

        static AugmentDefinition Augment(string title, string description, AugmentKind kind, float amount, int cap)
            => new AugmentDefinition { title = title, description = description, kind = kind, amount = amount, maxStacks = cap };

        public static void BuildSmokePlayer()
        {
            Create();
            // The project already uses URP; all generated sprites use its supported default sprite material.
            string output = Path.GetFullPath("Builds/Prototype/TakeUpPrototype.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Prototype build failed: " + report.summary.result);
            Debug.Log("TakeUp build succeeded: " + output);
        }
    }
}

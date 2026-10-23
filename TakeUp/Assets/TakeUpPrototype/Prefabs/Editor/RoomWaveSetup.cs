using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TakeUp.Prototype.Editor
{
    public static class RoomWaveSetup
    {
        const string Folder = "Assets/TakeUpPrototype/Scenes/";
        const string Target = Folder + "JoseonWaveOne.unity";
        [MenuItem("TakeUp/Open Wave 1 Room Map")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Target) == null) Create();
            else EditorSceneManager.OpenScene(Target);
        }
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Target) != null) return;
            var scene = EditorSceneManager.OpenScene(Folder + "PrehistoricPrototype.unity");
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivalGame>();
            if (game == null || game.scenePlayer == null) throw new Exception("Editable player missing");
            game.scenePlayer.transform.SetParent(null,true);
            foreach (var wall in UnityEngine.Object.FindObjectsByType<MapWall>(FindObjectsSortMode.None)) wall.gameObject.SetActive(false);
            var stage = GameObject.Find("Stage - Edit Layout");
            if (stage != null) stage.SetActive(false);
            var tile = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TakeUpPrototype/Art/Placeholders/Square.png");
            if (tile == null) throw new Exception("Square placeholder missing");
            var map = new GameObject("Wave 1 · Rooms and Gates").AddComponent<RoomWaveMap>();
            map.Build(tile); game.roomWaveMap = map;
            game.northBoundary = Boundary("Map North",new Vector2(0,28),map.transform);
            game.southBoundary = Boundary("Map South",new Vector2(0,-28),map.transform);
            game.westBoundary = Boundary("Map West",new Vector2(-11,0),map.transform);
            game.eastBoundary = Boundary("Map East",new Vector2(59,0),map.transform);
            game.scenePlayer.transform.position = new Vector3(0,20,0);
            game.worldCamera.transform.position = new Vector3(0,20,-10);
            game.worldCamera.orthographicSize = 10;
            game.followPlayer = true;
            EditorSceneManager.SaveScene(scene,Target);
            var entries = EditorBuildSettings.scenes.ToList();
            if (!entries.Any(s => s.path == Target)) entries.Add(new EditorBuildSettingsScene(Target,true));
            EditorBuildSettings.scenes = entries.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("PASS: authored six-room map saved: " + Target);
        }
        static Transform Boundary(string title,Vector2 p,Transform parent)
        {
            var t = new GameObject(title).transform; t.SetParent(parent); t.position = p; return t;
        }
        public static void CreateAndBuild()
        {
            Create();
            var result = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                "../../output/RoomWaveValidation/TakeUp.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Build failed");
        }
    }
}

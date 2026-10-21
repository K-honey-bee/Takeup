using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public static class DispatchTutorialSetup
    {
        const string Path = "Assets/TakeUpPrototype/Scenes/DispatchTutorial.unity";
        [MenuItem("TakeUp/Install Dispatch Tutorial")]
        public static void Install()
        {
            var original = EditorSceneManager.OpenScene("Assets/TakeUpPrototype/Scenes/Charater_Select.unity");
            var source = UnityEngine.Object.FindFirstObjectByType<StoryDialogue>();
            source.nextSceneAfterDialogue = "DispatchTutorial";
            EditorSceneManager.MarkSceneDirty(original); EditorSceneManager.SaveScene(original);
            if (!System.IO.File.Exists(Path))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Dispatch Tutorial - Assign Images Here");
                var flow = root.AddComponent<DispatchTutorial>();
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 6;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f,.2f,.23f);
                camera.transform.position = new Vector3(0,0,-10); flow.sceneCamera = camera;
                flow.office = Art(root.transform,"Office Background", new Vector3(0,0,0), -10);
                flow.room = Art(root.transform,"Dispatch Room Background", new Vector3(24,0,0), -10);
                flow.player = Art(root.transform,"Player", new Vector3(-2,-2,0), 10);
                flow.jaehee = Art(root.transform,"Jaehee", new Vector3(2,-2,0), 10);
                flow.coffin = Art(root.transform,"Coffin", new Vector3(24,10,0), 9);
                flow.goal = Art(root.transform,"Movement Goal", new Vector3(27,1,0), 1);
                flow.target = Art(root.transform,"Practice Target", new Vector3(28,1,0), 5);
                var canvas = new GameObject("Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
                flow.arrival = UI<Image>(canvas.transform,"Joseon Arrival",Vector2.zero,Vector2.one);
                flow.arrival.raycastTarget = false;
                flow.guide = UI<Image>(canvas.transform,"Instruction Image",new Vector2(.02f,.35f),new Vector2(.27f,.68f));
                flow.guide.raycastTarget = false; flow.guide.preserveAspect = true;
                flow.instruction = UI<Text>(canvas.transform,"Objective",new Vector2(.05f,.03f),new Vector2(.95f,.12f));
                flow.instruction.alignment = TextAnchor.MiddleCenter; flow.instruction.fontSize = 28;
                flow.instruction.font = Font.CreateDynamicFontFromOSFont(new[] {"Malgun Gothic","Arial"},28);
                var dialogueObject = UnityEngine.Object.Instantiate(source.gameObject, canvas.transform);
                dialogueObject.name = "Story Dialogue"; var dialogue = dialogueObject.GetComponent<StoryDialogue>();
                flow.dialogue = dialogue; dialogue.playOnStart = false; dialogue.nextSceneAfterDialogue = "";
                dialogue.lines = Array.Empty<StoryLine>(); dialogue.storyEndImage = null;
                var hidden = new GameObject("Unused Dialogue Start Button"); hidden.transform.SetParent(canvas.transform); hidden.SetActive(false);
                dialogue.startButton = hidden;
                var buttonImage = UI<Image>(canvas.transform,"Depart Button",new Vector2(.38f,.15f),new Vector2(.62f,.24f));
                buttonImage.color = new Color(.14f,.4f,.44f);
                flow.departButton = buttonImage.gameObject.AddComponent<Button>(); flow.departButton.targetGraphic = buttonImage;
                var label = UI<Text>(buttonImage.transform,"Label",Vector2.zero,Vector2.one);
                label.font = flow.instruction.font; label.fontSize = 28; label.alignment = TextAnchor.MiddleCenter;
                label.text = "전송 장치 탑승"; label.raycastTarget = false;
                var events = new GameObject("EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                var settings = AssetDatabase.LoadAssetAtPath<SurvivalSettings>("Assets/TakeUpPrototype/Data/PrehistoricSettings.asset");
                flow.playerImage = settings.playerSprite;
                foreach(var line in source.lines)
                    if(line != null && line.speaker != null && line.speaker.Contains("재희") && line.portrait != null) { flow.jaeheeImage = line.portrait; break; }
                flow.officeLines = new[] { Line("최재희","이쪽은 출동 준비실이에요. 여기 앉으세요.") };
                flow.simulationLines = new[] { Line("최재희","시뮬레이션 시작할게요. 먼저 이동부터 해보세요."), Line("최재희","조작 설명은 옆에 띄워드릴게요. 저는 잠깐 눈 좀… 아니, 보고 있을게요.") };
                flow.missionLines = new[] {
                    Line("최재희","빨리 하셨네요ㅎㅎ… 이제 임무 설명할게요."),
                    Line("최재희","파견지는 조선이에요. 오래된 봉인석에서 시간 조각이 사라진 뒤 균열이 생겼어요."),
                    Line("최재희","서로 다른 시간이 뒤섞이면서 도깨비불과 그림자호랑이가 나타나고 있어요."),
                    Line("최재희","균열을 가로막는 먹물 요괴를 처리하고, 사라진 조각의 흔적을 찾아주세요. 누가 가져갔는지는 아직 몰라요."),
                    Line("최재희","설명 끝났어요. 이제 옷 벗어주세요."),
                    Line("주인공","네?! 갑자기요?!"),
                    Line("최재희","겉옷이요. 전송 장치에 끼어요. 이상한 생각 하지 마세요."),
                    Line("주인공","저 관처럼 생긴 게… 전송 장치라고요?"),
                    Line("최재희","네. 준비됐으면 탑승 눌러주세요. 저는 이제 잘 거예요. 건들지 말아주세요.") };
                flow.arrivalLines = new[] { Line("주인공","여기가 조선…? 분위기가 심상치 않은데."), Line("최재희 · 통신","균열 주변에는 사람들이 없어요. 움직이는 게 보이면 조심하세요.") };
                EditorSceneManager.SaveScene(scene,Path);
            }
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s=>s.path==Path)) scenes.Add(new EditorBuildSettingsScene(Path,true));
            EditorBuildSettings.scenes = scenes.ToArray(); AssetDatabase.SaveAssets();
        }
        static StoryLine Line(string speaker,string text) => new StoryLine {speaker=speaker,dialogue=text};
        static SpriteRenderer Art(Transform parent,string name,Vector3 position,int order)
        {
            var renderer = new GameObject(name,typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent); renderer.transform.position=position; renderer.sortingOrder=order; return renderer;
        }
        static T UI<T>(Transform parent,string name,Vector2 min,Vector2 max) where T:Graphic
        {
            var graphic = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(T)).GetComponent<T>();
            graphic.transform.SetParent(parent,false); var rect=graphic.rectTransform;
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return graphic;
        }
        public static void InstallAndBuild()
        {
            Install();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[] {Path,"Assets/TakeUpPrototype/Scenes/PrehistoricPrototype.unity","Assets/TakeUpPrototype/Scenes/OctopusBossPrototype.unity"},
                locationPathName="Builds/DispatchValidation/TakeUpDispatch.exe", target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
            });
            if(report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Dispatch build failed");
        }
    }
}

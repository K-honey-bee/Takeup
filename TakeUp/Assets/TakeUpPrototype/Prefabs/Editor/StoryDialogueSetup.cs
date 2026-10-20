using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TakeUp.Prototype
{
    public static class StoryDialogueSetup
    {
        public static void InstallAndBuild()
        {
            LoadingScreenSetup.Install();
            var scene = EditorSceneManager.OpenScene("Assets/TakeUpPrototype/Scenes/Charater_Select.unity");
            var canvas = GameObject.Find("Canvas").transform;
            if (canvas.Find("Story Dialogue") == null)
            {
                var root = new GameObject("Story Dialogue", typeof(RectTransform), typeof(StoryDialogue),
                    typeof(AudioSource), typeof(VideoPlayer));
                root.transform.SetParent(canvas, false);
                Stretch((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var story = root.GetComponent<StoryDialogue>();
                story.startButton = canvas.Find("Image").gameObject;
                story.voiceSource = root.GetComponent<AudioSource>();
                story.voiceSource.playOnAwake = false;
                story.voiceSource.spatialBlend = 0;
                story.voiceVideo = root.GetComponent<VideoPlayer>();
                story.voiceVideo.playOnAwake = false;
                story.voiceVideo.renderMode = VideoRenderMode.APIOnly;
                story.voiceVideo.audioOutputMode = VideoAudioOutputMode.AudioSource;
                var panel = UI<Image>(root.transform, "Dialogue Panel", Vector2.zero, Vector2.one);
                Stretch(panel.rectTransform, new Vector2(0, .73f), Vector2.one, new Vector2(12, 0), new Vector2(-12, -12));
                panel.color = new Color(.055f, .09f, .18f, .97f);
                panel.raycastTarget = true;
                story.dialoguePanel = panel.gameObject;
                story.portraitImage = UI<Image>(panel.transform, "Portrait", new Vector2(.015f, .08f), new Vector2(.18f, .94f));
                story.portraitImage.preserveAspect = true;
                story.portraitImage.raycastTarget = false;
                story.portraitImage.enabled = false;
                story.speakerText = Label(panel.transform, "Speaker", new Vector2(.20f, .70f), new Vector2(.97f, .96f), 30, new Color(.55f, .85f, 1));
                story.dialogueText = Label(panel.transform, "Dialogue", new Vector2(.20f, .20f), new Vector2(.97f, .70f), 28, Color.white);
                story.hintText = Label(panel.transform, "Controls", new Vector2(.20f, .02f), new Vector2(.97f, .18f), 17, new Color(.7f, .76f, .85f));
                story.dialogueText.alignment = TextAnchor.UpperLeft;
                story.dialogueText.supportRichText = false;
                story.lines = new[] {
                    new StoryLine { speaker = "안내 유령", dialogue = "시간의 집에 온 걸 환영해! 조선에 생긴 시간 균열을 조사해 줘." },
                    new StoryLine { speaker = "신입 유령", dialogue = "좋아! 준비가 되면 출발할게." }
                };
                story.speakerText.text = story.lines[0].speaker;
                story.dialogueText.text = story.lines[0].dialogue;
                story.hintText.text = "Ctrl 다음 대화  ·  Space 길게: 대화 건너뛰기";
                EditorSceneManager.SaveScene(scene);
            }
            MenuFlowSmokeTest.BuildValidationPlayer();
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }
        static T UI<T>(Transform parent, string name, Vector2 min, Vector2 max) where T : Graphic
        {
            var graphic = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T)).GetComponent<T>();
            graphic.transform.SetParent(parent, false);
            Stretch(graphic.rectTransform, min, max, Vector2.zero, Vector2.zero);
            return graphic;
        }
        static Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size, Color color)
        {
            var text = UI<Text>(parent, name, min, max);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.color = color;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            return text;
        }
    }
}

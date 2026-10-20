using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TakeUp.Prototype
{
    [Serializable]
    public sealed class StoryLine
    {
        public string speaker;
        public Sprite portrait;
        [TextArea(3, 8)] public string dialogue;
        [Tooltip("녹음한 MP4를 Project에 넣은 뒤 VideoClip을 연결합니다. 영상은 표시하지 않고 소리만 재생합니다.")]
        public VideoClip voiceMp4;
        [Tooltip("WAV/MP3를 쓸 경우 이 칸에 연결. MP4가 있으면 MP4를 우선 사용합니다.")]
        public AudioClip voiceAudio;
    }

    public sealed class StoryDialogue : MonoBehaviour
    {
        [Header("대화 / 위에서부터 순서대로")]
        public StoryLine[] lines = Array.Empty<StoryLine>();
        [Header("재생")]
        [Min(1)] public float charactersPerSecond = 28;
        [Min(.2f)] public float holdSpaceSeconds = 1.2f;
        [Range(0, 1)] public float voiceVolume = .8f;
        [Header("UI 연결")]
        public GameObject dialoguePanel;
        public Image portraitImage;
        public Text speakerText, dialogueText, hintText;
        public GameObject startButton;
        public AudioSource voiceSource;
        public VideoPlayer voiceVideo;

        public bool Finished { get; private set; }
        public int CurrentLine => lineIndex;
        public bool IsTyping => shownCharacters < characterEnds.Length;
        int lineIndex = -1, shownCharacters;
        int[] characterEnds = Array.Empty<int>();
        string fullText = "";
        float typingClock, spaceHeld;
        bool ready, waitingForVideo;
        Font runtimeFont;

        void Awake()
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 28);
            speakerText.font = dialogueText.font = hintText.font = runtimeFont;
            dialogueText.supportRichText = false;
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            voiceSource.spatialBlend = 0;
            voiceVideo.playOnAwake = false;
            voiceVideo.isLooping = false;
            voiceVideo.renderMode = VideoRenderMode.APIOnly;
            voiceVideo.audioOutputMode = VideoAudioOutputMode.AudioSource;
            voiceVideo.prepareCompleted += VideoPrepared;
            voiceVideo.errorReceived += VideoError;
            startButton.SetActive(false);
            dialoguePanel.SetActive(false);
        }

        IEnumerator Start()
        {
            while (SceneLoader.IsLoading) yield return null;
            ready = true;
            ShowLine(0);
        }

        void Update()
        {
            if (!ready || Finished || SceneLoader.IsLoading) return;
            float dt = Time.unscaledDeltaTime;
            var keys = Keyboard.current;
            UpdateSpaceHold(keys != null && keys.spaceKey.isPressed && Application.isFocused, dt);
            if (Finished) return;
            if (keys != null && (keys.leftCtrlKey.wasPressedThisFrame || keys.rightCtrlKey.wasPressedThisFrame))
                Advance();
            if (Finished) return;
            if (IsTyping)
            {
                typingClock += dt * Mathf.Max(1, charactersPerSecond);
                int count = Mathf.Min(characterEnds.Length, Mathf.FloorToInt(typingClock));
                if (count != shownCharacters)
                {
                    shownCharacters = count;
                    dialogueText.text = shownCharacters == 0 ? "" : fullText.Substring(0, characterEnds[shownCharacters - 1]);
                }
            }
            hintText.text = spaceHeld > 0
                ? $"Space 길게 누르기… {Mathf.RoundToInt(Mathf.Clamp01(spaceHeld / holdSpaceSeconds) * 100)}%"
                : IsTyping ? "Ctrl 문장 바로 보기  ·  Space 길게: 대화 건너뛰기"
                : "Ctrl 다음 대화  ·  Space 길게: 대화 건너뛰기";
        }

        public void UpdateSpaceHold(bool held, float dt)
        {
            if (!ready || Finished) return;
            spaceHeld = held ? spaceHeld + Mathf.Max(0, dt) : 0;
            if (spaceHeld >= Mathf.Max(.2f, holdSpaceSeconds)) Finish();
        }

        public void Advance()
        {
            if (!ready || Finished || SceneLoader.IsLoading) return;
            if (IsTyping)
            {
                shownCharacters = characterEnds.Length;
                typingClock = shownCharacters;
                dialogueText.text = fullText;
            }
            else ShowLine(lineIndex + 1);
        }

        void ShowLine(int index)
        {
            StopVoice();
            if (lines == null || index >= lines.Length) { Finish(); return; }
            lineIndex = index;
            var line = lines[index] ?? new StoryLine();
            fullText = line.dialogue ?? "";
            int[] starts = StringInfo.ParseCombiningCharacters(fullText);
            characterEnds = new int[starts.Length];
            for (int i = 0; i < starts.Length; i++)
                characterEnds[i] = i + 1 < starts.Length ? starts[i + 1] : fullText.Length;
            shownCharacters = 0;
            typingClock = 0;
            speakerText.text = line.speaker ?? "";
            dialogueText.text = "";
            portraitImage.sprite = line.portrait;
            portraitImage.enabled = line.portrait != null;
            portraitImage.preserveAspect = true;
            dialoguePanel.SetActive(true);
            voiceSource.volume = voiceVolume;
            if (line.voiceMp4 != null)
            {
                voiceVideo.source = VideoSource.VideoClip;
                voiceVideo.clip = line.voiceMp4;
                voiceVideo.controlledAudioTrackCount = 1;
                voiceVideo.EnableAudioTrack(0, true);
                voiceVideo.SetTargetAudioSource(0, voiceSource);
                waitingForVideo = true;
                voiceVideo.Prepare();
            }
            else if (line.voiceAudio != null)
            {
                voiceSource.clip = line.voiceAudio;
                voiceSource.Play();
            }
        }

        void VideoPrepared(VideoPlayer source)
        {
            if (!waitingForVideo || Finished || !isActiveAndEnabled || lineIndex < 0
                || source.clip != lines[lineIndex]?.voiceMp4) return;
            waitingForVideo = false;
            source.Play();
        }

        void VideoError(VideoPlayer source, string message)
        {
            waitingForVideo = false;
            Debug.LogWarning("대화 MP4 음성을 재생할 수 없습니다. 대사는 계속 진행됩니다: " + message, this);
        }

        void StopVoice()
        {
            waitingForVideo = false;
            if (voiceVideo != null) { voiceVideo.Stop(); voiceVideo.clip = null; }
            if (voiceSource != null) { voiceSource.Stop(); voiceSource.clip = null; }
        }

        void Finish()
        {
            Finished = true;
            spaceHeld = 0;
            StopVoice();
            dialoguePanel.SetActive(false);
            // Revealing the button never invokes its click action.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            startButton.SetActive(true);
        }

        void OnDisable() { spaceHeld = 0; StopVoice(); }
        void OnDestroy()
        {
            if (voiceVideo != null)
            {
                voiceVideo.prepareCompleted -= VideoPrepared;
                voiceVideo.errorReceived -= VideoError;
            }
            if (runtimeFont != null) Destroy(runtimeFont);
        }
    }
}

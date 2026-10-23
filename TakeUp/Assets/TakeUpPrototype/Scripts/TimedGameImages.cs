using System;
using UnityEngine;
using UnityEngine.UI;
using TakeUp.Prototype;
using static UnityEngine.EventSystems.EventTrigger;

public class TimedGameImages : MonoBehaviour
{
    [Serializable]
    public class ImageEvent
    {
        [Min(0)] public float showAt = 30f;
        public Sprite image;
        [Min(0.1f)] public float duration = 3f;
    }

    [Header("연결")]
    public SurvivalGame game;
    public Image displayImage;

    [Header("등장할 이미지 목록")]
    public ImageEvent[] events = Array.Empty<ImageEvent>();

    bool[] shown;
    bool displaying;
    float hideAt;

    void Start()
    {
        if (game == null || displayImage == null)
        {
            Debug.LogError("Game과 Display Image를 연결해 주세요.", this);
            enabled = false;
            return;
        }

        shown = new bool[events.Length];

        displayImage.raycastTarget = false;
        displayImage.preserveAspect = true;
        displayImage.enabled = false;
    }

    void Update()
    {
        if (game.State != RunState.Playing)
            return;

        float now = game.Elapsed;

        if (displaying && now >= hideAt)
        {
            displayImage.enabled = false;
            displaying = false;
        }

        // 현재 그림이 끝나면 다음 그림 표시
        if (displaying)
            return;

        int next = -1;

        for (int i = 0; i < events.Length; i++)
        {
            var entry = events[i];

            if (shown[i] || entry == null ||
                entry.image == null  || now < entry.showAt)
                continue;

        if (next < 0 || entry.showAt < events[next].showAt)
            next = i;
    }

        if (next< 0)
            return;

        shown[next] = true;
        displayImage.sprite = events[next].image;
        displayImage.enabled = true;

        hideAt = now + Mathf.Max(0.1f, events[next].duration);
        displaying = true;
    }
}
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        [Header("씬 배치 / Hierarchy에서 편집")]
        public SpriteRenderer scenePlayer;
        public Transform runtimeRoot;
        [Tooltip("경계의 위치로 이동 범위를 정합니다. 축에 평행한 사각형입니다.")]
        public Transform northBoundary, southBoundary, eastBoundary, westBoundary;
        [Tooltip("끄면 카메라를 씬에 배치한 위치에 고정합니다.")]
        public bool followPlayer = true;
        [Header("생성할 프리팹 / 이미지와 크기는 프리팹에서 편집")]
        public SpriteRenderer stonePrefab;
        public SpriteRenderer spearPrefab;
        public SpriteRenderer experiencePrefab;
        Color playerColor;
        Vector3 cameraOffset;
        float baseCameraSize;

        public float BaseCameraSize => baseCameraSize;

        Vector2 ClampToArena(Vector2 position)
        {
            float left = westBoundary != null ? westBoundary.position.x : -settings.arenaHalfSize.x;
            float right = eastBoundary != null ? eastBoundary.position.x : settings.arenaHalfSize.x;
            float bottom = southBoundary != null ? southBoundary.position.y : -settings.arenaHalfSize.y;
            float top = northBoundary != null ? northBoundary.position.y : settings.arenaHalfSize.y;
            Vector2 margin = playerRenderer.bounds.extents;
            position.x = right - left <= margin.x * 2 ? (left + right) * 0.5f : Mathf.Clamp(position.x, left + margin.x, right - margin.x);
            position.y = top - bottom <= margin.y * 2 ? (top + bottom) * 0.5f : Mathf.Clamp(position.y, bottom + margin.y, top - margin.y);
            if (bossEncounter != null) position = bossEncounter.ConstrainMovement(player.position, position, Mathf.Max(margin.x, margin.y));
            position.x = Mathf.Clamp(position.x, Mathf.Min(left + margin.x, right - margin.x), Mathf.Max(left + margin.x, right - margin.x));
            position.y = Mathf.Clamp(position.y, Mathf.Min(bottom + margin.y, top - margin.y), Mathf.Max(bottom + margin.y, top - margin.y));
            return position;
        }

        SpriteRenderer SpawnVisual(SpriteRenderer prefab, Vector2 position)
        {
            var instance = Instantiate(prefab, position, prefab.transform.rotation, world);
            instance.gameObject.SetActive(true);
            return instance;
        }

        static float VisualRadius(SpriteRenderer visual)
        {
            var circleCollider = visual.GetComponent<CircleCollider2D>();
            if (circleCollider != null)
                return circleCollider.radius * Mathf.Max(Mathf.Abs(visual.transform.lossyScale.x), Mathf.Abs(visual.transform.lossyScale.y));
            return Mathf.Max(0.02f, Mathf.Min(visual.bounds.extents.x, visual.bounds.extents.y) * 0.84f);
        }
    }
}

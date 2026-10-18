using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed class BossCoverRock : MonoBehaviour
    {
        [Range(0, 3)] public int colorIndex;
        [Min(0.1f)] public float radius = 0.85f;
        public SpriteRenderer artwork;
        public SpriteRenderer safeMarker;
        public float WorldRadius => radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, WorldRadius); }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MapWall : MonoBehaviour
    {
        static readonly List<MapWall> walls = new List<MapWall>();
        BoxCollider2D box;
        void OnEnable() { box = GetComponent<BoxCollider2D>(); if (!walls.Contains(this)) walls.Add(this); }
        void OnDisable() { walls.Remove(this); }

        public static bool Cast(Vector2 from, Vector2 to, float radius, out Vector2 point, out Vector2 normal)
        {
            float nearest = 2; normal = Vector2.zero;
            foreach (var wall in walls)
            {
                if (wall == null || !wall.isActiveAndEnabled || wall.box == null || !wall.box.enabled) continue;
                if (wall.Intersect(from, to, radius, out float fraction, out Vector2 hitNormal) && fraction < nearest)
                { nearest = fraction; normal = hitNormal; }
            }
            point = nearest <= 1 ? Vector2.Lerp(from, to, nearest) : to;
            return nearest <= 1;
        }

        bool Intersect(Vector2 from, Vector2 to, float radius, out float fraction, out Vector2 normal)
        {
            Vector2 a = (Vector2)transform.InverseTransformPoint(from) - box.offset;
            Vector2 b = (Vector2)transform.InverseTransformPoint(to) - box.offset;
            Vector3 scale = transform.lossyScale;
            Vector2 half = box.size * .5f + new Vector2(radius / Mathf.Max(.001f, Mathf.Abs(scale.x)), radius / Mathf.Max(.001f, Mathf.Abs(scale.y)));
            Vector2 delta = b - a, localNormal = Vector2.zero;
            float enter = 0, exit = 1;
            for (int axis = 0; axis < 2; axis++)
            {
                float start = a[axis], direction = delta[axis], extent = half[axis];
                if (Mathf.Abs(direction) < .000001f)
                {
                    if (Mathf.Abs(start) > extent) { fraction = 0; normal = Vector2.zero; return false; }
                    continue;
                }
                float near = (-extent - start) / direction, far = (extent - start) / direction;
                float sign = -1;
                if (near > far) { float swap = near; near = far; far = swap; sign = 1; }
                if (near >= enter) { enter = near; localNormal = Vector2.zero; localNormal[axis] = sign; }
                exit = Mathf.Min(exit, far);
                if (enter > exit) { fraction = 0; normal = Vector2.zero; return false; }
            }
            fraction = enter;
            normal = ((Vector2)transform.TransformDirection(localNormal)).normalized;
            return enter <= 1 && exit >= 0;
        }

        public static bool Blocks(Vector2 from, Vector2 to) => Cast(from, to, 0, out _, out _);

        public static Vector2 Constrain(Vector2 from, Vector2 to, float radius)
        {
            Vector2 position = from, remaining = to - from;
            for (int i = 0; i < 3 && remaining.sqrMagnitude > .000001f; i++)
            {
                if (!Cast(position, position + remaining, radius, out Vector2 hit, out Vector2 normal)) return position + remaining;
                if (normal.sqrMagnitude < .5f) return position;
                Vector2 end = position + remaining;
                position = hit + normal * .002f;
                remaining = end - position;
                remaining -= normal * Mathf.Min(0, Vector2.Dot(remaining, normal));
            }
            return position;
        }
    }
}

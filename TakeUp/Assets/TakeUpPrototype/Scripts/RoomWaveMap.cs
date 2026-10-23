using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    // Authored geometry is saved in the scene; all room state is fresh for each run.
    public sealed class RoomWaveMap : MonoBehaviour
    {
        [Serializable] public sealed class Room
        {
            public string title;
            public Rect bounds;
            [Min(0)] public int enemyCount;
            [NonSerialized] public bool entered, cleared;
        }
        public Room[] rooms;
        public GameObject[] gates;
        public SpriteRenderer portal, seal;
        [Min(.1f)] public float spawnInterval = .65f;
        [Min(0)] public float warningDuration = 1.2f;
        public void SetGates(bool closed)
        {
            foreach (var gate in gates) gate.SetActive(closed);
        }

        // Called by the editor builder. Room geometry remains editable in Hierarchy.
        public void Build(Sprite tile)
        {
            var centers = new[] { new Vector2(0,20), Vector2.zero, new Vector2(24,0),
                new Vector2(48,0), new Vector2(0,-20), new Vector2(24,-20) };
            string[] titles = { "01 · 조선 도착", "02 · 마을길", "03 · 포탈 수비대", "04 · 복귀 포탈", "05 · 선택 전투", "06 · 봉인석" };
            int[] counts = { 0, 8, 12, 0, 10, 0 };
            // Up, right, down, left openings.
            int[][] exits = { new[]{2}, new[]{0,1,2}, new[]{1,3}, new[]{3}, new[]{0,1}, new[]{3} };
            rooms = new Room[6];
            var doors = new List<GameObject>();
            for (int i = 0; i < rooms.Length; i++)
            {
                Vector2 c = centers[i];
                rooms[i] = new Room { title = titles[i], bounds = new Rect(c - new Vector2(10,7), new Vector2(20,14)), enemyCount = counts[i] };
                var root = new GameObject(titles[i]).transform; root.SetParent(transform, false);
                Paint(root, "Floor", c, new Vector2(20,14), new Color(.16f,.22f,.22f), -20, false, tile);
                for (int side = 0; side < 4; side++)
                {
                    bool horizontal = side == 0 || side == 2;
                    Vector2 axis = horizontal ? Vector2.right : Vector2.up;
                    Vector2 edge = c + (side == 0 ? Vector2.up * 7 : side == 1 ? Vector2.right * 10 : side == 2 ? Vector2.down * 7 : Vector2.left * 10);
                    float length = horizontal ? 20 : 14;
                    bool opening = Array.IndexOf(exits[i], side) >= 0;
                    if (opening)
                    {
                        float segment = (length - 4) / 2;
                        foreach (int sign in new[]{-1,1})
                            Wall(root, edge + axis * sign * (2 + segment/2), horizontal ? new Vector2(segment,.5f) : new Vector2(.5f,segment), tile);
                        var door = Paint(root, "Gate · 전투 중 봉쇄", edge, horizontal ? new Vector2(4,.5f) : new Vector2(.5f,4), new Color(.85f,.23f,.26f), 8, true, tile);
                        doors.Add(door.gameObject);
                    }
                    else Wall(root, edge, horizontal ? new Vector2(length,.5f) : new Vector2(.5f,length), tile);
                }
            }
            // Corridors match the user's six-room diagram, including the optional branch.
            foreach (var pair in new[]{new Vector2Int(0,1), new Vector2Int(1,2), new Vector2Int(2,3), new Vector2Int(1,4), new Vector2Int(4,5)})
            {
                Vector2 a = centers[pair.x], b = centers[pair.y], mid = (a+b)/2;
                bool horizontal = a.y == b.y;
                float length = horizontal ? Mathf.Abs(a.x-b.x)-20 : Mathf.Abs(a.y-b.y)-14;
                Paint(transform,"Passage",mid,horizontal ? new Vector2(length,4) : new Vector2(4,length),new Color(.21f,.28f,.27f),-20,false,tile);
                foreach (int sign in new[]{-1,1}) Wall(transform, mid + (horizontal ? Vector2.up : Vector2.right)*sign*2.25f,
                    horizontal ? new Vector2(length+.5f,.5f) : new Vector2(.5f,length+.5f), tile);
            }
            gates = doors.ToArray(); SetGates(false);
            portal = Paint(transform,"Portal · E",centers[3],new Vector2(2,3),new Color(.25f,.5f,.55f),2,false,tile);
            seal = Paint(transform,"Seal · E",centers[5],new Vector2(2,2),new Color(.9f,.7f,.3f),2,false,tile);
        }
        void Wall(Transform parent, Vector2 p, Vector2 size, Sprite tile) => Paint(parent,"Wall",p,size,new Color(.39f,.43f,.46f),8,true,tile);
        static SpriteRenderer Paint(Transform parent,string title,Vector2 p,Vector2 size,Color color,int order,bool solid,Sprite tile)
        {
            var obj = new GameObject(title); obj.transform.SetParent(parent,false); obj.transform.position = p;
            var sr = obj.AddComponent<SpriteRenderer>(); sr.sprite = tile; sr.color = color; sr.sortingOrder = order;
            Vector2 pixels = tile.bounds.size;
            obj.transform.localScale = new Vector3(size.x/pixels.x,size.y/pixels.y,1);
            if (solid) { obj.AddComponent<BoxCollider2D>().size = pixels; obj.AddComponent<MapWall>(); }
            return sr;
        }
    }
}

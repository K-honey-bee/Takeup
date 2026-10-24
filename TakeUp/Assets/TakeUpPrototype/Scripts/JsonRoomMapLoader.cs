using System;
using System.Collections.Generic;
using UnityEngine;

namespace TakeUp.Prototype
{
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(RoomWaveMap))]
    public sealed class JsonRoomMapLoader : MonoBehaviour
    {
        [Header("현재 씬의 SurvivalGame 연결")]
        public SurvivalGame game;

        [Header("웹에서 내보낸 JSON 파일들")]
        public TextAsset[] maps;

        [Tooltip("켜면 Play할 때 JSON 하나를 랜덤 선택합니다.")]
        public bool randomOnPlay = true;

        [Min(0)]
        public int selectedMap;

        public string[] RoomTypes { get; private set; }
        public int StartRoom { get; private set; }

        [Serializable]
        public sealed class MapData
        {
            public int schemaVersion;
            public int stage;
            public float corridorWidth;
            public RoomData[] rooms;
            public EdgeData[] edges;
            public TileData[] floorTiles;
        }

        [Serializable]
        public sealed class RoomData
        {
            public int id;
            public float x, y, width, height;
            public string type;
            public int enemies;
        }

        [Serializable]
        public sealed class EdgeData
        {
            public int a, b;
        }

        [Serializable]
        public sealed class TileData
        {
            public int x, y;
        }

        Sprite tileSprite;
        Texture2D tileTexture;

        void Awake()
        {
            if (game == null)
            {
                Debug.LogError("JsonRoomMapLoader: Game을 연결하세요.", this);
                return;
            }

            var available = new List<TextAsset>();
            if (maps != null)
                foreach (var asset in maps)
                    if (asset != null) available.Add(asset);

            if (available.Count == 0)
            {
                Debug.LogError("JsonRoomMapLoader: Maps에 JSON을 넣으세요.", this);
                return;
            }

            int index = randomOnPlay
                ? UnityEngine.Random.Range(0, available.Count)
                : Mathf.Clamp(selectedMap, 0, available.Count - 1);

            MapData data;

            try
            {
                data = JsonUtility.FromJson<MapData>(available[index].text);
                Validate(data);
            }
            catch (Exception error)
            {
                Debug.LogError("JSON 맵 오류: " + error.Message, this);
                return;
            }

            // 원본 맵은 삭제하지 않고 실행 중에만 비활성화합니다.
            RoomWaveMap target = GetComponent<RoomWaveMap>();

            if (game.roomWaveMap != null &&
                game.roomWaveMap != target)
            {
                game.roomWaveMap.gameObject.SetActive(false);
            }

            Build(data, target);
            game.roomWaveMap = target;
        }

        static void Validate(MapData data)
        {
            if (data == null || data.schemaVersion != 1)
                throw new Exception("지원하지 않는 JSON 형식입니다.");

            if (data.rooms == null || data.rooms.Length < 2 ||
                data.edges == null ||
                data.floorTiles == null || data.floorTiles.Length == 0)
                throw new Exception("방, 통로 또는 바닥 데이터가 없습니다.");

            if (data.corridorWidth <= 0)
                throw new Exception("통로 너비가 올바르지 않습니다.");

            int starts = 0;
            int exits = 0;

            for (int i = 0; i < data.rooms.Length; i++)
            {
                var room = data.rooms[i];

                if (room.id != i || room.width <= 0 || room.height <= 0)
                    throw new Exception("방 번호 또는 크기가 올바르지 않습니다.");

                if (room.type == "start") starts++;
                if (room.type == "exit" || room.type == "boss") exits++;
            }

            if (starts != 1 || exits != 1)
                throw new Exception("시작 방과 도착 방이 각각 하나 필요합니다.");

            foreach (var edge in data.edges)
            {
                if (edge.a < 0 || edge.b < 0 ||
                    edge.a >= data.rooms.Length ||
                    edge.b >= data.rooms.Length ||
                    edge.a == edge.b)
                    throw new Exception("통로의 방 번호가 올바르지 않습니다.");

                var a = data.rooms[edge.a];
                var b = data.rooms[edge.b];

                bool sameX = Mathf.Approximately(a.x, b.x);
                bool sameY = Mathf.Approximately(a.y, b.y);

                if (sameX == sameY)
                    throw new Exception("통로는 가로 또는 세로로 연결되어야 합니다.");
            }
        }

        void Build(MapData data, RoomWaveMap map)
        {
            tileTexture = new Texture2D(1, 1);
            tileTexture.SetPixel(0, 0, Color.white);
            tileTexture.Apply();

            tileSprite = Sprite.Create(
                tileTexture,
                new Rect(0, 0, 1, 1),
                new Vector2(.5f, .5f),
                1
            );

            var root = new GameObject("Generated Map").transform;
            root.SetParent(transform, false);

            // 새 Loader 오브젝트는 Position 0 / Rotation 0 / Scale 1로 두세요.
            RoomTypes = new string[data.rooms.Length];
            map.rooms = new RoomWaveMap.Room[data.rooms.Length];

            int exitRoom = -1;
            int firstReward = -1;

            for (int i = 0; i < data.rooms.Length; i++)
            {
                var source = data.rooms[i];
                RoomTypes[i] = source.type;

                bool combat =
                    source.type == "combat" ||
                    source.type == "optional";

                map.rooms[i] = new RoomWaveMap.Room
                {
                    title = $"{i + 1:00} · {source.type}",
                    bounds = new Rect(
                        source.x - source.width / 2,
                        source.y - source.height / 2,
                        source.width,
                        source.height
                    ),
                    enemyCount = combat ? Mathf.Max(0, source.enemies) : 0
                };

                if (source.type == "start") StartRoom = i;

                if (source.type == "exit" || source.type == "boss")
                    exitRoom = i;

                if (source.type == "reward")
                {
                    if (firstReward < 0) firstReward = i;

                    Paint(
                        root, $"Reward {i}",
                        new Vector2(source.x, source.y),
                        Vector2.one * 1.5f,
                        new Color(.35f, .7f, .85f),
                        2, false
                    );
                }
            }

            var floor = new HashSet<Vector2Int>();

            foreach (var t in data.floorTiles)
                floor.Add(new Vector2Int(t.x, t.y));

            var directions = new[]
            {
                Vector2Int.right,
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.down
            };

            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;

            foreach (var cell in floor)
            {
                Vector2 center = (Vector2)cell + Vector2.one * .5f;

                Paint(
                    root, "Floor", center, Vector2.one,
                    new Color(.16f, .23f, .2f), -20, false
                );

                left = Mathf.Min(left, cell.x);
                right = Mathf.Max(right, cell.x + 1);
                bottom = Mathf.Min(bottom, cell.y);
                top = Mathf.Max(top, cell.y + 1);

                foreach (var direction in directions)
                {
                    // 隣のセルにも床があれば、そこに壁は作らない。
                    if (floor.Contains(cell + direction)) continue;

                    bool vertical = direction.x != 0;

                    Paint(
                        root,
                        "Wall",
                        center + (Vector2)direction * .5f,
                        vertical
                            ? new Vector2(.3f, 1.3f)
                            : new Vector2(1.3f, .3f),
                        new Color(.38f, .44f, .4f),
                        8,
                        true
                    );
                }
            }

            var gates = new List<GameObject>();

            foreach (var edge in data.edges)
            {
                AddGate(data.rooms[edge.a], data.rooms[edge.b]);
                AddGate(data.rooms[edge.b], data.rooms[edge.a]);
            }

            map.gates = gates.ToArray();
            map.SetGates(false);

            var exit = data.rooms[exitRoom];

            map.portal = Paint(
                root, "Portal",
                new Vector2(exit.x, exit.y),
                new Vector2(1.8f, 2.2f),
                new Color(.3f, .8f, .7f),
                3, false
            );

            map.seal = null;

            if (firstReward >= 0)
            {
                var reward = data.rooms[firstReward];

                map.seal = Paint(
                    root, "Seal",
                    new Vector2(reward.x, reward.y),
                    Vector2.one,
                    new Color(.85f, .72f, .4f),
                    3, false
                );
            }

            // 기존 맵 범위 때문에 새 맵에서 이동이 막히지 않게 갱신합니다.
            game.westBoundary = Boundary(root, "Map West", left - 1, 0);
            game.eastBoundary = Boundary(root, "Map East", right + 1, 0);
            game.southBoundary = Boundary(root, "Map South", 0, bottom - 1);
            game.northBoundary = Boundary(root, "Map North", 0, top + 1);

            void AddGate(RoomData from, RoomData to)
            {
                Vector2 center = new Vector2(from.x, from.y);
                bool horizontalTravel = !Mathf.Approximately(from.x, to.x);

                Vector2 position;
                Vector2 size;

                if (horizontalTravel)
                {
                    position = center + Vector2.right *
                        Mathf.Sign(to.x - from.x) * from.width * .5f;

                    size = new Vector2(.3f, data.corridorWidth);
                }
                else
                {
                    position = center + Vector2.up *
                        Mathf.Sign(to.y - from.y) * from.height * .5f;

                    size = new Vector2(data.corridorWidth, .3f);
                }

                var gate = Paint(
                    root, "Gate", position, size,
                    new Color(.85f, .3f, .3f), 9, true
                );

                gates.Add(gate.gameObject);
            }
        }

        SpriteRenderer Paint(
            Transform parent,
            string title,
            Vector2 position,
            Vector2 size,
            Color color,
            int order,
            bool solid)
        {
            var obj = new GameObject(title);
            obj.transform.SetParent(parent, false);
            obj.transform.position = new Vector3(position.x, position.y, 0);
            obj.transform.localScale = new Vector3(size.x, size.y, 1);

            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = tileSprite;
            renderer.color = color;
            renderer.sortingOrder = order;

            if (solid)
            {
                obj.AddComponent<BoxCollider2D>().size = Vector2.one;
                obj.AddComponent<MapWall>();
            }

            return renderer;
        }

        static Transform Boundary(
            Transform parent, string title, float x, float y)
        {
            var obj = new GameObject(title);
            obj.transform.SetParent(parent, false);
            obj.transform.position = new Vector3(x, y, 0);
            return obj.transform;
        }

        void OnDestroy()
        {
            if (tileSprite != null) Destroy(tileSprite);
            if (tileTexture != null) Destroy(tileTexture);
        }
    }
}
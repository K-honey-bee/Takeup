using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        [Header("방 탐험 맵")]
        public RoomWaveMap roomWaveMap;

        public bool RoomWaveActive => roomWaveMap != null;
        public bool RoomWaveComplete { get; private set; }
        public int CurrentRoom { get; private set; }
        public int ActiveRoom { get; private set; } = -1;

        int roomSpawned;
        float roomSpawnClock;

        readonly HashSet<int> claimedRewards = new HashSet<int>();

        public string RoomInstruction { get; private set; }

        public int RoomRemaining => ActiveRoom < 0
            ? 0
            : Mathf.Max(
                0,
                roomWaveMap.rooms[ActiveRoom].enemyCount
                - roomSpawned + enemies.Count
            );

        string RoomType(int index)
        {
            var loader = roomWaveMap.GetComponent<JsonRoomMapLoader>();

            if (loader != null && loader.RoomTypes != null)
                return loader.RoomTypes[index];

            // 기존 6개 방 조선 맵도 사용할 수 있도록 유지합니다.
            switch (index)
            {
                case 0: return "start";
                case 3: return "exit";
                case 4: return "optional";
                case 5: return "reward";
                default: return "combat";
            }
        }

        public float RoomProgress
        {
            get
            {
                if (!RoomWaveActive) return 0;

                int required = 0;
                int cleared = 0;

                for (int i = 0; i < roomWaveMap.rooms.Length; i++)
                {
                    if (RoomType(i) != "combat") continue;

                    required++;

                    if (roomWaveMap.rooms[i].cleared)
                        cleared++;
                }

                return required == 0 ? 1f : (float)cleared / required;
            }
        }

        void InitializeRoomWave()
        {
            if (!RoomWaveActive) return;

            RoomWaveComplete = false;
            ActiveRoom = -1;
            claimedRewards.Clear();

            roomWaveMap.SetGates(false);

            foreach (var room in roomWaveMap.rooms)
            {
                room.entered = false;
                room.cleared = room.enemyCount <= 0;
            }

            CurrentRoom = 0;

            for (int i = 0; i < roomWaveMap.rooms.Length; i++)
            {
                if (RoomType(i) != "start") continue;
                CurrentRoom = i;
                break;
            }

            player.position = roomWaveMap.rooms[CurrentRoom].bounds.center;

            cameraOffset = new Vector3(0, 0, -10);
            worldCamera.transform.position = player.position + cameraOffset;
            followPlayer = true;

            RoomInstruction = "방을 탐색하세요 · 좌클릭 공격";
        }

        void TickRoomWave(float dt)
        {
            if (RoomWaveComplete) return;

            for (int i = 0; i < roomWaveMap.rooms.Length; i++)
            {
                if (roomWaveMap.rooms[i].bounds.Contains(player.position))
                    CurrentRoom = i;
            }

            if (ActiveRoom < 0)
            {
                var room = roomWaveMap.rooms[CurrentRoom];
                string type = RoomType(CurrentRoom);

                Rect trigger = room.bounds;

                // 이동 반경 0.4 + 문 두께 여유.
                const float inset = .65f;
                trigger.xMin += inset;
                trigger.xMax -= inset;
                trigger.yMin += inset;
                trigger.yMax -= inset;

                bool combat = type == "combat" || type == "optional";

                if (combat &&
                    room.enemyCount > 0 &&
                    !room.entered &&
                    trigger.Contains(player.position))
                {
                    room.entered = true;
                    ActiveRoom = CurrentRoom;
                    roomSpawned = 0;
                    roomSpawnClock = roomWaveMap.warningDuration;

                    roomWaveMap.SetGates(true);

                    foreach (var shot in shots)
                        Destroy(shot.body.gameObject);

                    shots.Clear();
                }
            }

            if (ActiveRoom >= 0)
            {
                var room = roomWaveMap.rooms[ActiveRoom];

                RoomInstruction =
                    $"{room.title} · 남은 적 {RoomRemaining}";

                roomSpawnClock -= dt;

                if (roomSpawned < room.enemyCount &&
                    roomSpawnClock <= 0 &&
                    enemies.Count < Mathf.Max(1, settings.maxAliveEnemies))
                {
                    if (settings.enemies == null ||
                        settings.enemies.Length == 0)
                    {
                        RoomInstruction = "Settings에 적 종류를 연결하세요.";
                        return;
                    }

                    var type =
                        settings.enemies[roomSpawned % settings.enemies.Length];

                    if (type == null)
                    {
                        foreach (var candidateType in settings.enemies)
                        {
                            if (candidateType == null) continue;
                            type = candidateType;
                            break;
                        }
                    }

                    if (type == null)
                    {
                        RoomInstruction = "Settings에 유효한 적 종류가 없습니다.";
                        return;
                    }

                    Vector2 center = room.bounds.center;
                    Vector2 spawn = center;
                    float bestDistance = -1;

                    float radiusX = Mathf.Max(0, room.bounds.width / 2 - 3);
                    float radiusY = Mathf.Max(0, room.bounds.height / 2 - 3);

                    for (int j = 0; j < 12; j++)
                    {
                        float angle =
                            (j + roomSpawned * .37f) * Mathf.PI * 2 / 12;

                        Vector2 candidate = center + new Vector2(
                            Mathf.Cos(angle) * radiusX,
                            Mathf.Sin(angle) * radiusY
                        );

                        float distance =
                            ((Vector2)player.position - candidate).sqrMagnitude;

                        if (distance <= bestDistance) continue;

                        bestDistance = distance;
                        spawn = candidate;
                    }

                    SpawnEnemy(type, spawn);
                    roomSpawned++;
                    roomSpawnClock = Mathf.Max(.1f, roomWaveMap.spawnInterval);
                }

                if (roomSpawned >= room.enemyCount && enemies.Count == 0)
                {
                    room.cleared = true;
                    ActiveRoom = -1;
                    roomWaveMap.SetGates(false);

                    RoomInstruction = "전투 완료 · 증강을 선택하세요";
                    PendingChoices++;
                    OfferAugments();
                }

                return;
            }

            bool ready = RoomProgress >= 1;

            if (roomWaveMap.portal != null)
            {
                roomWaveMap.portal.color = ready
                    ? new Color(.3f, 1, .85f)
                    : new Color(.25f, .35f, .4f);
            }

            string currentType = RoomType(CurrentRoom);

            if (currentType == "exit" || currentType == "boss")
            {
                RoomInstruction = ready
                    ? "[E] 포탈 사용"
                    : "필수 전투 방을 모두 정리하세요";
            }
            else if (currentType == "reward")
            {
                RoomInstruction = claimedRewards.Contains(CurrentRoom)
                    ? "보상 획득 완료"
                    : "방 중앙에서 [E] · 회복 + 증강";
            }
            else
            {
                RoomInstruction =
                    $"필수 전투 진행 {RoomProgress:P0} · 방을 탐색하세요";
            }

            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                InteractRoomWave();
            }
        }

        public void InteractRoomWave()
        {
            if (!RoomWaveActive ||
                State != RunState.Playing ||
                ActiveRoom >= 0 ||
                RoomWaveComplete)
            {
                return;
            }

            var room = roomWaveMap.rooms[CurrentRoom];
            string type = RoomType(CurrentRoom);

            bool inside = room.bounds.Contains(player.position);

            if (inside &&
                type == "reward" &&
                !claimedRewards.Contains(CurrentRoom) &&
                Vector2.Distance(player.position, room.bounds.center) < 2.5f)
            {
                claimedRewards.Add(CurrentRoom);

                Health = Mathf.Min(
                    MaximumHealth,
                    Health + MaximumHealth * .35f
                );

                PendingChoices++;
                OfferAugments();
                return;
            }

            if (inside &&
                (type == "exit" || type == "boss") &&
                roomWaveMap.portal != null &&
                RoomProgress >= 1 &&
                Vector2.Distance(
                    player.position,
                    roomWaveMap.portal.transform.position
                ) < 2.5f)
            {
                RoomWaveComplete = true;
                State = RunState.BossReady;
                hud.ShowEnd(true);
            }
        }
    }
}
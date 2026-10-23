using UnityEngine;
using UnityEngine.InputSystem;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        [Header("1웨이브 방 탐험 / 전용 씬에서 연결")]
        public RoomWaveMap roomWaveMap;
        public bool RoomWaveActive => roomWaveMap != null;
        public bool RoomWaveComplete { get; private set; }
        public int CurrentRoom { get; private set; }
        public int ActiveRoom { get; private set; } = -1;
        int roomSpawned;
        float roomSpawnClock;
        bool sealClaimed;
        public int RoomRemaining => ActiveRoom < 0 ? 0 : roomWaveMap.rooms[ActiveRoom].enemyCount - roomSpawned + enemies.Count;
        public float RoomProgress => !RoomWaveActive ? 0 : ((roomWaveMap.rooms[1].cleared ? 1 : 0) + (roomWaveMap.rooms[2].cleared ? 1 : 0)) / 2f;
        public string RoomInstruction { get; private set; }

        void InitializeRoomWave()
        {
            if (!RoomWaveActive) return;
            roomWaveMap.SetGates(false);
            foreach (var room in roomWaveMap.rooms) { room.entered = false; room.cleared = false; }
            player.position = roomWaveMap.rooms[0].bounds.center;
            cameraOffset = new Vector3(0,0,-10);
            worldCamera.transform.position = player.position + cameraOffset;
            followPlayer = true;
            RoomInstruction = "아래 마을길로 이동 · 마우스 조준 / 좌클릭 공격";
        }

        void TickRoomWave(float dt)
        {
            if (RoomWaveComplete) return;
            for (int i = 0; i < roomWaveMap.rooms.Length; i++)
                if (roomWaveMap.rooms[i].bounds.Contains(player.position)) CurrentRoom = i;
            if (ActiveRoom < 0)
            {
                var room = roomWaveMap.rooms[CurrentRoom];
                // Close only once the whole player is safely inside, not in the doorway.
                Rect trigger = room.bounds;
                // Match the wall half-thickness + collision radius. A wider inset lets
                // players skirt the perimeter and bypass the encounter trigger.
                float inset = Mathf.Max(playerRenderer.bounds.extents.x, playerRenderer.bounds.extents.y) + .25f;
                trigger.xMin += inset; trigger.xMax -= inset; trigger.yMin += inset; trigger.yMax -= inset;
                if (room.enemyCount > 0 && !room.entered && trigger.Contains(player.position))
                {
                    room.entered = true; ActiveRoom = CurrentRoom; roomSpawned = 0;
                    roomSpawnClock = roomWaveMap.warningDuration;
                    roomWaveMap.SetGates(true);
                    // Clear old projectiles so they cannot hit a new encounter before it starts.
                    foreach (var shot in shots) Destroy(shot.body.gameObject);
                    shots.Clear();
                }
            }
            if (ActiveRoom >= 0)
            {
                var room = roomWaveMap.rooms[ActiveRoom];
                RoomInstruction = $"{room.title} · 출입구 봉쇄 · 남은 적 {RoomRemaining} (등장 대기 포함)";
                roomSpawnClock -= dt;
                if (roomSpawned < room.enemyCount && roomSpawnClock <= 0 && enemies.Count < Mathf.Max(1,settings.maxAliveEnemies))
                {
                    var type = settings.enemies[roomSpawned % settings.enemies.Length];
                    if (type == null) type = settings.enemies[0];
                    Vector2 c = room.bounds.center;
                    // Open rectangular arenas need no pathfinding; spawn on a safe inner ring.
                    Vector2 position = c;
                    float best = -1;
                    for (int j = 0; j < 12; j++)
                    {
                        float angle = (j + roomSpawned * .37f) * Mathf.PI * 2 / 12;
                        Vector2 candidate = c + new Vector2(Mathf.Cos(angle)*6,Mathf.Sin(angle)*3.5f);
                        float distance = ((Vector2)player.position-candidate).sqrMagnitude;
                        if (distance > best) { best = distance; position = candidate; }
                    }
                    SpawnEnemy(type,position); roomSpawned++;
                    roomSpawnClock = Mathf.Max(.1f,roomWaveMap.spawnInterval);
                }
                if (roomSpawned == room.enemyCount && enemies.Count == 0)
                {
                    room.cleared = true; ActiveRoom = -1; roomWaveMap.SetGates(false);
                    RoomInstruction = "전투 완료 · 출입구 개방 · 증강을 고르고 탐색하세요";
                    PendingChoices++; OfferAugments();
                }
                return;
            }
            bool ready = RoomProgress >= 1;
            roomWaveMap.portal.color = ready ? new Color(.3f,1,.85f) : new Color(.25f,.35f,.4f);
            RoomInstruction = CurrentRoom == 0 ? "조선 도착 · 아래 마을길로 이동 · 좌클릭 공격" :
                CurrentRoom == 3 ? ready ? "[E] 포탈 조사 · 1웨이브 완료" : "마을길과 포탈 수비대의 적을 모두 처치하세요" :
                CurrentRoom == 5 ? sealClaimed ? "봉인석 복구 완료 · 포탈로 돌아가세요" : "[E] 봉인석 복구 · 체력 회복 + 증강 1회" :
                "오른쪽: 포탈 경로 / 아래: 선택 전투와 봉인석 · 정리한 방은 안전합니다";
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) InteractRoomWave();
        }
        public void InteractRoomWave()
        {
            if (!RoomWaveActive || State != RunState.Playing || ActiveRoom >= 0 || RoomWaveComplete) return;
            if (Vector2.Distance(player.position,roomWaveMap.seal.transform.position) < 2.5f && roomWaveMap.rooms[4].cleared && !sealClaimed)
            {
                sealClaimed = true; roomWaveMap.seal.color = new Color(.4f,.8f,.8f);
                Health = Mathf.Min(MaximumHealth,Health + MaximumHealth * .35f);
                PendingChoices++; OfferAugments();
            }
            else if (Vector2.Distance(player.position,roomWaveMap.portal.transform.position) < 2.5f && RoomProgress >= 1)
            {
                RoomWaveComplete = true; State = RunState.BossReady; hud.ShowEnd(true);
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public sealed class DispatchTutorial : MonoBehaviour
    {
        public enum Step { Office, Pan, SimulationIntro, Move, Attack, Dash, Mission, Coffin, Arrival, Loading }
        [HideInInspector]
        public Sprite officeImage, dispatchRoomImage, simulationImage, playerImage, jaeheeImage, coffinImage;
        [Header("설명 그림 / 배경과 캐릭터는 Hierarchy의 Sprite Renderer에서 편집")]
        public Sprite movementGuide, attackGuide, dashGuide, missionImage;
        public Sprite[] joseonImages = Array.Empty<Sprite>();
        [Header("추가 대사 / 음성과 초상화도 각 줄에 연결 가능")]
        public StoryLine[] officeLines, simulationLines, missionLines, arrivalLines;
        [Header("시간과 조작")]
        [Min(.1f)] public float panDuration = 1.5f;
        [Min(.1f)] public float coffinDropDuration = 1, ascentDuration = 1.2f, arrivalImageDuration = 2;
        [Min(.1f)] public float moveSpeed = 5, dashDistance = 3, dashDuration = .18f;
        [Min(1)] public int requiredHits = 3;
        [Min(.01f)] public float goalRadius = .8f, targetRadius = .7f;
        [Min(.01f)] public float shotInterval = .22f, shotSpeed = 12, shotLifetime = 2;
        public string gameScene = "PrehistoricPrototype";
        [Header("씬 연결")]
        public Camera sceneCamera;
        public StoryDialogue dialogue;
        public SpriteRenderer office, room, player, jaehee, coffin, target, goal;
        public SpriteRenderer simulation, projectileTemplate;
        [Header("Hierarchy의 배치 지점 / Scene에서 이동 도구로 조절")]
        public Transform officeCameraPoint, roomCameraPoint;
        public Transform playerTrainingPoint, jaeheeRoomPoint, playerMissionPoint;
        public Transform coffinStartPoint, coffinDockPoint, coffinExitPoint;
        public Transform trainingMinPoint, trainingMaxPoint;
        [HideInInspector] public bool editableLayoutInstalled;
        public Image guide, arrival;
        public Text instruction;
        public Button departButton;
        public Step CurrentStep { get; private set; }
        public int TargetHits { get; private set; }
        [HideInInspector] public Vector3 OfficeCameraPosition = new Vector3(0, 0, -10);
        [HideInInspector] public Vector3 RoomCameraPosition = new Vector3(24, 0, -10);
        readonly List<Projectile> shots = new List<Projectile>();
        sealed class Projectile { public GameObject visual; public Vector2 direction; public float life = 2; }
        float fireClock, dashRemaining;
        Vector2 dashDirection, moveDirection = Vector2.right;
        bool dialogueDone;

        IEnumerator Start()
        {
            instruction.font = dialogue.speakerText.font;
            departButton.GetComponentInChildren<Text>(true).font = dialogue.speakerText.font;
            sceneCamera.transform.position = officeCameraPoint.position;
            simulation.gameObject.SetActive(false); projectileTemplate.gameObject.SetActive(false);
            coffin.gameObject.SetActive(false); goal.gameObject.SetActive(false); target.gameObject.SetActive(false);
            guide.gameObject.SetActive(false); arrival.gameObject.SetActive(false); departButton.gameObject.SetActive(false);
            dialogue.Completed += DialogueCompleted;
            departButton.onClick.AddListener(Depart);
            while (SceneLoader.IsLoading) yield return null;
            StartCoroutine(Run());
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-dispatch-smoke") >= 0)
                StartCoroutine(SmokeTest());
        }

        void DialogueCompleted() { dialogueDone = true; }
        IEnumerator Say(StoryLine[] lines)
        {
            dialogueDone = false;
            dialogue.PlayLines(lines);
            while (!dialogueDone) yield return null;
        }
        IEnumerator Run()
        {
            CurrentStep = Step.Office;
            instruction.text = "출동 준비실";
            yield return Say(officeLines);
            CurrentStep = Step.Pan;
            yield return Slide(sceneCamera.transform, sceneCamera.transform.position, roomCameraPoint.position, panDuration);
            player.transform.position = playerTrainingPoint.position;
            jaehee.transform.position = jaeheeRoomPoint.position;
            CurrentStep = Step.SimulationIntro;
            yield return Say(simulationLines);
            room.gameObject.SetActive(false); simulation.gameObject.SetActive(true);
            jaehee.gameObject.SetActive(false);
            CurrentStep = Step.Move;
            ShowGuide(movementGuide, "WASD로 초록색 표시까지 이동하세요");
            goal.gameObject.SetActive(true);
            while (CurrentStep == Step.Move) yield return null;
            goal.gameObject.SetActive(false);
            ShowGuide(attackGuide, $"마우스로 빨간 표적을 조준하고 좌클릭을 꾹 누르세요 · 0/{requiredHits}");
            target.gameObject.SetActive(true);
            while (CurrentStep == Step.Attack) yield return null;
            ClearShots(); target.gameObject.SetActive(false);
            ShowGuide(dashGuide, "WASD로 방향을 잡고 Shift로 대시하세요");
            while (CurrentStep == Step.Dash) yield return null;
            guide.gameObject.SetActive(false); instruction.text = "훈련 완료 · 임무 안내";
            simulation.gameObject.SetActive(false); room.gameObject.SetActive(true);
            player.transform.position = playerMissionPoint.position;
            jaehee.gameObject.SetActive(true);
            ShowGuide(missionImage, "조선 균열 조사 임무");
            yield return Say(missionLines);
            guide.gameObject.SetActive(false); instruction.text = "출동 준비 완료";
            departButton.gameObject.SetActive(true);
        }
        void ShowGuide(Sprite sprite, string text)
        {
            guide.sprite = sprite;
            guide.gameObject.SetActive(sprite != null); instruction.text = text;
        }
        void Update()
        {
            if (SceneLoader.IsLoading || !Application.isFocused) return;
            if (CurrentStep != Step.Move && CurrentStep != Step.Attack && CurrentStep != Step.Dash) return;
            var keys = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (keys != null)
            {
                input.x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
                input.y = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
            }
            MoveSimulation(input, Time.deltaTime);
            if (CurrentStep == Step.Dash && keys != null && (keys.leftShiftKey.wasPressedThisFrame || keys.rightShiftKey.wasPressedThisFrame)) BeginDash();
            if (dashRemaining > 0)
            {
                float dt = Mathf.Min(dashRemaining, Time.deltaTime);
                MoveClamped(dashDirection * (dashDistance / Mathf.Max(.01f, dashDuration)) * dt);
                dashRemaining -= dt;
                if (dashRemaining <= 0) CurrentStep = Step.Mission;
            }
            fireClock -= Time.deltaTime;
            if (CurrentStep == Step.Attack && Mouse.current != null && Mouse.current.leftButton.isPressed && fireClock <= 0)
            {
                Vector2 aim = sceneCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                FireAt(aim);
            }
            TickProjectiles(Time.deltaTime);
        }
        public void MoveSimulation(Vector2 input, float dt)
        {
            if (CurrentStep != Step.Move && CurrentStep != Step.Attack && CurrentStep != Step.Dash) return;
            if (input.sqrMagnitude > .01f) moveDirection = input.normalized;
            if (dashRemaining <= 0) MoveClamped(Vector2.ClampMagnitude(input, 1) * moveSpeed * dt);
            if (CurrentStep == Step.Move && Vector2.Distance(player.transform.position, goal.transform.position) < goalRadius)
                CurrentStep = Step.Attack;
        }
        void MoveClamped(Vector2 delta)
        {
            Vector3 p = player.transform.position + (Vector3)delta;
            p.x = Mathf.Clamp(p.x, Mathf.Min(trainingMinPoint.position.x, trainingMaxPoint.position.x), Mathf.Max(trainingMinPoint.position.x, trainingMaxPoint.position.x));
            p.y = Mathf.Clamp(p.y, Mathf.Min(trainingMinPoint.position.y, trainingMaxPoint.position.y), Mathf.Max(trainingMinPoint.position.y, trainingMaxPoint.position.y));
            player.transform.position = p;
        }
        public void FireAt(Vector2 aim)
        {
            if (CurrentStep != Step.Attack || fireClock > 0) return;
            Vector2 delta = aim - (Vector2)player.transform.position;
            if (delta.sqrMagnitude < .001f) return;
            fireClock = shotInterval;
            var shot = Instantiate(projectileTemplate, player.transform.position, projectileTemplate.transform.rotation, transform);
            shot.gameObject.SetActive(true);
            shots.Add(new Projectile { visual = shot.gameObject, direction = delta.normalized, life = shotLifetime });
        }
        void TickProjectiles(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i]; Vector2 from = shot.visual.transform.position;
                Vector2 to = from + shot.direction * (shotSpeed * dt);
                shot.visual.transform.position = to; shot.life -= dt;
                bool hit = CurrentStep == Step.Attack && OctopusBossEncounter.SegmentDistance(target.transform.position, from, to) < targetRadius;
                if (hit)
                {
                    TargetHits++;
                    instruction.text = $"표적 명중 · {TargetHits}/{requiredHits}";
                    if (TargetHits >= requiredHits) CurrentStep = Step.Dash;
                }
                if (hit || shot.life <= 0) { Destroy(shot.visual); shots.RemoveAt(i); }
            }
        }
        public void BeginDash()
        {
            if (CurrentStep != Step.Dash || dashRemaining > 0) return;
            dashDirection = moveDirection; dashRemaining = Mathf.Max(.01f, dashDuration);
        }
        public void Depart()
        {
            if (CurrentStep != Step.Mission || !dialogueDone || !departButton.gameObject.activeSelf) return;
            departButton.gameObject.SetActive(false); CurrentStep = Step.Coffin;
            StartCoroutine(Departure());
        }
        IEnumerator Departure()
        {
            instruction.text = "전송 장치 연결 중…";
            coffin.gameObject.SetActive(true);
            Vector3 dock = coffinDockPoint.position;
            yield return Slide(coffin.transform, coffinStartPoint.position, dock, coffinDropDuration);
            player.gameObject.SetActive(true);
            yield return Slide(player.transform, player.transform.position, dock, ascentDuration);
            player.gameObject.SetActive(false);
            yield return Slide(coffin.transform, dock, coffinExitPoint.position, ascentDuration);
            CurrentStep = Step.Arrival; instruction.text = "조선 · 시간 균열 발생 지역";
            arrival.gameObject.SetActive(true);
            if (joseonImages.Length > 0 && joseonImages[0] != null) arrival.sprite = joseonImages[0];
            yield return Say(arrivalLines);
            foreach (var sprite in joseonImages)
            {
                if (sprite == null) continue;
                arrival.sprite = sprite;
                yield return new WaitForSecondsRealtime(arrivalImageDuration);
            }
            if (joseonImages.Length == 0) yield return new WaitForSecondsRealtime(arrivalImageDuration);
            CurrentStep = Step.Loading;
            if (!SceneLoader.Load(gameScene)) { instruction.text = "게임 씬을 불러올 수 없습니다. Game Scene 설정을 확인하세요."; Debug.LogError(instruction.text); }
        }
        IEnumerator Slide(Transform item, Vector3 from, Vector3 to, float seconds)
        {
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                item.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, seconds))));
                yield return null;
            }
            item.position = to;
        }
        void ClearShots() { foreach (var shot in shots) if (shot.visual != null) Destroy(shot.visual); shots.Clear(); }
        void OnDestroy()
        {
            if (dialogue != null) dialogue.Completed -= DialogueCompleted;
            ClearShots();
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (goal != null) Gizmos.DrawWireSphere(goal.transform.position, goalRadius);
            Gizmos.color = Color.red;
            if (target != null) Gizmos.DrawWireSphere(target.transform.position, targetRadius);
            Gizmos.color = Color.cyan;
            if (trainingMinPoint != null && trainingMaxPoint != null)
                Gizmos.DrawWireCube((trainingMinPoint.position + trainingMaxPoint.position) * .5f, new Vector3(Mathf.Abs(trainingMaxPoint.position.x-trainingMinPoint.position.x),Mathf.Abs(trainingMaxPoint.position.y-trainingMinPoint.position.y),0));
            if (officeCameraPoint != null && roomCameraPoint != null) Gizmos.DrawLine(officeCameraPoint.position, roomCameraPoint.position);
            if (coffinStartPoint != null && coffinDockPoint != null && coffinExitPoint != null)
            { Gizmos.DrawLine(coffinStartPoint.position, coffinDockPoint.position); Gizmos.DrawLine(coffinDockPoint.position, coffinExitPoint.position); }
        }
        IEnumerator SmokeTest()
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!dialogue.Finished) { dialogue.Advance(); dialogue.Advance(); }
                if (CurrentStep == Step.Move) MoveSimulation((goal.transform.position - player.transform.position).normalized, .03f);
                if (CurrentStep == Step.Attack) { fireClock = 0; FireAt(target.transform.position); TickProjectiles(.1f); }
                if (CurrentStep == Step.Dash)
                {
                    BeginDash();
                    if (dashRemaining > 0) { MoveClamped(dashDirection * dashDistance); dashRemaining = 0; CurrentStep = Step.Mission; }
                }
                if (departButton.gameObject.activeSelf) Depart();
                if (CurrentStep == Step.Loading)
                {
                    if (TargetHits < requiredHits) { Debug.LogError("FAIL dispatch: attack gate"); Application.Quit(1); }
                    else
                    {
                        var verifier = new GameObject("Dispatch arrival verifier").AddComponent<DispatchArrivalVerifier>();
                        verifier.gameScene = gameScene;
                        DontDestroyOnLoad(verifier.gameObject);
                    }
                    yield break;
                }
                yield return null;
            }
            Debug.LogError("FAIL dispatch timeout: " + CurrentStep); Application.Quit(1);
        }
    }

    public sealed class DispatchArrivalVerifier : MonoBehaviour
    {
        public string gameScene;
        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < deadline)
            {
                var game = FindFirstObjectByType<SurvivalGame>();
                if (!SceneLoader.IsLoading && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == gameScene && game != null && game.State == RunState.Playing)
                { Debug.Log("PASS dispatch: training gates, dialogue, camera pan, coffin, Joseon arrival and playable game scene"); Application.Quit(0); yield break; }
                yield return null;
            }
            Debug.LogError("FAIL dispatch: game scene did not start"); Application.Quit(1);
        }
    }
}

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
        [Header("이미지 / 비워 두면 임시 도형 표시")]
        public Sprite officeImage, dispatchRoomImage, simulationImage, playerImage, jaeheeImage, coffinImage;
        public Sprite movementGuide, attackGuide, dashGuide, missionImage;
        public Sprite[] joseonImages = Array.Empty<Sprite>();
        [Header("추가 대사 / 음성과 초상화도 각 줄에 연결 가능")]
        public StoryLine[] officeLines, simulationLines, missionLines, arrivalLines;
        [Header("시간과 조작")]
        [Min(.1f)] public float panDuration = 1.5f;
        [Min(.1f)] public float coffinDropDuration = 1, ascentDuration = 1.2f, arrivalImageDuration = 2;
        [Min(.1f)] public float moveSpeed = 5, dashDistance = 3, dashDuration = .18f;
        [Min(1)] public int requiredHits = 3;
        public string gameScene = "PrehistoricPrototype";
        [Header("씬 연결")]
        public Camera sceneCamera;
        public StoryDialogue dialogue;
        public SpriteRenderer office, room, player, jaehee, coffin, target, goal;
        public Image guide, arrival;
        public Text instruction;
        public Button departButton;
        public Step CurrentStep { get; private set; }
        public int TargetHits { get; private set; }
        public Vector3 OfficeCameraPosition = new Vector3(0, 0, -10);
        public Vector3 RoomCameraPosition = new Vector3(24, 0, -10);
        readonly List<Projectile> shots = new List<Projectile>();
        sealed class Projectile { public GameObject visual; public Vector2 direction; public float life = 2; }
        Sprite placeholder;
        Texture2D placeholderTexture;
        float fireClock, dashRemaining;
        Vector2 dashDirection, moveDirection = Vector2.right;
        bool dialogueDone;

        IEnumerator Start()
        {
            instruction.font = dialogue.speakerText.font;
            departButton.GetComponentInChildren<Text>(true).font = dialogue.speakerText.font;
            placeholderTexture = new Texture2D(1, 1);
            placeholderTexture.SetPixel(0, 0, Color.white); placeholderTexture.Apply();
            placeholder = Sprite.Create(placeholderTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            Paint(office, officeImage, new Vector2(22, 12), new Color(.85f, .8f, .74f));
            Paint(room, dispatchRoomImage, new Vector2(22, 12), new Color(.32f, .42f, .47f));
            Paint(player, playerImage, Vector2.one * .9f, new Color(.5f, .95f, .85f));
            Paint(jaehee, jaeheeImage, Vector2.one, new Color(1, .85f, .35f));
            Paint(coffin, coffinImage, new Vector2(1.7f, 2.8f), new Color(.35f, .22f, .3f));
            Paint(goal, null, Vector2.one * 1.3f, new Color(.3f, .9f, .7f, .5f));
            Paint(target, null, Vector2.one * 1.2f, new Color(1, .4f, .4f));
            sceneCamera.transform.position = OfficeCameraPosition;
            coffin.gameObject.SetActive(false); goal.gameObject.SetActive(false); target.gameObject.SetActive(false);
            guide.gameObject.SetActive(false); arrival.gameObject.SetActive(false); departButton.gameObject.SetActive(false);
            dialogue.Completed += DialogueCompleted;
            departButton.onClick.AddListener(Depart);
            while (SceneLoader.IsLoading) yield return null;
            StartCoroutine(Run());
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-dispatch-smoke") >= 0)
                StartCoroutine(SmokeTest());
        }

        void Paint(SpriteRenderer renderer, Sprite sprite, Vector2 size, Color fallback)
        {
            renderer.sprite = sprite != null ? sprite : placeholder;
            renderer.color = sprite != null ? Color.white : fallback;
            Vector2 bounds = renderer.sprite.bounds.size;
            float scale = Mathf.Min(size.x / Mathf.Max(.001f, bounds.x), size.y / Mathf.Max(.001f, bounds.y));
            renderer.transform.localScale = sprite != null ? Vector3.one * scale : new Vector3(size.x, size.y, 1);
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
            yield return Slide(sceneCamera.transform, OfficeCameraPosition, RoomCameraPosition, panDuration);
            player.transform.position = new Vector3(21, -2, 0);
            jaehee.transform.position = new Vector3(26, -2, 0);
            CurrentStep = Step.SimulationIntro;
            yield return Say(simulationLines);
            if (simulationImage != null) Paint(room, simulationImage, new Vector2(22, 12), Color.white);
            jaehee.gameObject.SetActive(false);
            CurrentStep = Step.Move;
            ShowGuide(movementGuide, "WASD로 초록색 표시까지 이동하세요");
            goal.transform.position = new Vector3(27, 1, 0); goal.gameObject.SetActive(true);
            while (CurrentStep == Step.Move) yield return null;
            goal.gameObject.SetActive(false);
            ShowGuide(attackGuide, $"마우스로 빨간 표적을 조준하고 좌클릭을 꾹 누르세요 · 0/{requiredHits}");
            target.transform.position = new Vector3(28, 1, 0); target.gameObject.SetActive(true);
            while (CurrentStep == Step.Attack) yield return null;
            ClearShots(); target.gameObject.SetActive(false);
            ShowGuide(dashGuide, "WASD로 방향을 잡고 Shift로 대시하세요");
            while (CurrentStep == Step.Dash) yield return null;
            guide.gameObject.SetActive(false); instruction.text = "훈련 완료 · 임무 안내";
            Paint(room, dispatchRoomImage, new Vector2(22, 12), new Color(.32f, .42f, .47f));
            player.transform.position = new Vector3(23, -2, 0);
            jaehee.gameObject.SetActive(true);
            ShowGuide(missionImage, "조선 균열 조사 임무");
            yield return Say(missionLines);
            guide.gameObject.SetActive(false); instruction.text = "출동 준비 완료";
            departButton.gameObject.SetActive(true);
        }
        void ShowGuide(Sprite sprite, string text)
        {
            guide.sprite = sprite; guide.preserveAspect = true;
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
            if (CurrentStep == Step.Move && Vector2.Distance(player.transform.position, goal.transform.position) < .8f)
                CurrentStep = Step.Attack;
        }
        void MoveClamped(Vector2 delta)
        {
            Vector3 p = player.transform.position + (Vector3)delta;
            p.x = Mathf.Clamp(p.x, 15, 33); p.y = Mathf.Clamp(p.y, -4, 2.5f);
            player.transform.position = p;
        }
        public void FireAt(Vector2 aim)
        {
            if (CurrentStep != Step.Attack || fireClock > 0) return;
            Vector2 delta = aim - (Vector2)player.transform.position;
            if (delta.sqrMagnitude < .001f) return;
            fireClock = .22f;
            var shot = new GameObject("Simulation projectile", typeof(SpriteRenderer));
            shot.transform.SetParent(transform); shot.transform.position = player.transform.position;
            var sr = shot.GetComponent<SpriteRenderer>(); sr.sprite = placeholder; sr.color = Color.white; sr.sortingOrder = 12;
            shot.transform.localScale = Vector3.one * .18f;
            shots.Add(new Projectile { visual = shot, direction = delta.normalized });
        }
        void TickProjectiles(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i]; Vector2 from = shot.visual.transform.position;
                Vector2 to = from + shot.direction * (12 * dt);
                shot.visual.transform.position = to; shot.life -= dt;
                bool hit = CurrentStep == Step.Attack && OctopusBossEncounter.SegmentDistance(target.transform.position, from, to) < .7f;
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
            Vector3 dock = new Vector3(24, 1.4f, 0);
            yield return Slide(coffin.transform, new Vector3(24, 10, 0), dock, coffinDropDuration);
            player.gameObject.SetActive(true);
            yield return Slide(player.transform, player.transform.position, dock, ascentDuration);
            player.gameObject.SetActive(false);
            yield return Slide(coffin.transform, dock, new Vector3(24, 11, 0), ascentDuration);
            CurrentStep = Step.Arrival; instruction.text = "조선 · 시간 균열 발생 지역";
            arrival.gameObject.SetActive(true);
            arrival.color = joseonImages.Length > 0 && joseonImages[0] != null ? Color.white : new Color(.13f, .2f, .23f);
            arrival.sprite = joseonImages.Length > 0 ? joseonImages[0] : null;
            yield return Say(arrivalLines);
            foreach (var sprite in joseonImages)
            {
                if (sprite == null) continue;
                arrival.sprite = sprite; arrival.color = Color.white;
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
            if (placeholder != null) Destroy(placeholder);
            if (placeholderTexture != null) Destroy(placeholderTexture);
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

using UnityEditor;
using UnityEngine;

namespace TakeUp.Prototype.Editor
{
    [CustomEditor(typeof(SurvivalHud))]
    public sealed class SurvivalHudEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Play를 끈 상태에서 UI를 미리 보고 Rect Transform을 편집하세요. 실행 중에는 수치와 표시 상태만 갱신됩니다. 요소의 부모나 HudLayoutSlot은 유지하세요.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("기본 HUD 보기")) Preview(null);
                if (GUILayout.Button("증강 선택창 배치")) Preview("Augment Choices");
                if (GUILayout.Button("증강 도감 배치")) Preview("Augment Codex");
                if (GUILayout.Button("일시정지 메뉴 배치")) Preview("Pause Menu");
                if (GUILayout.Button("완료 화면 배치")) Preview("Stage Complete");
                if (GUILayout.Button("게임오버 화면 배치")) Preview("Game Over");
                if (GUILayout.Button("벽사무 리듬 UI 배치")) Preview("Stomp Dance");
            }
            DrawDefaultInspector();
        }

        void Preview(string panelName)
        {
            var hud = (SurvivalHud)target;
            RectTransform selected = null;
            foreach (var rect in hud.GetComponentsInChildren<RectTransform>(true))
            {
                bool isModal = rect.name == "Augment Choices" || rect.name == "Augment Codex" || rect.name == "Pause Menu"
                    || rect.name == "Stage Complete" || rect.name == "Game Over";
                if (rect.name == "Overlay" || rect.name == "Stomp Dance" || isModal)
                {
                    Undo.RecordObject(rect.gameObject, "Preview HUD panel");
                    rect.gameObject.SetActive(rect.name == "Overlay" ? panelName != null && panelName != "Stomp Dance" : rect.name == panelName);
                }
                if (rect.name == panelName) selected = rect;
            }
            if (selected != null) { Selection.activeGameObject = selected.gameObject; EditorGUIUtility.PingObject(selected.gameObject); }
            SceneView.RepaintAll();
        }
    }
}

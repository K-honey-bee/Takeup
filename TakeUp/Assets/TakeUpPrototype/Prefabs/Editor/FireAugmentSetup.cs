using UnityEditor;

namespace TakeUp.Prototype.Editor
{
    public static class FireAugmentSetup
    {
        [MenuItem("TakeUp/Install Fire Augments")]
        public static void Install()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:SurvivalSettings", new[] { "Assets/TakeUpPrototype" }))
            {
                var settings = AssetDatabase.LoadAssetAtPath<SurvivalSettings>(AssetDatabase.GUIDToAssetPath(guid));
                FireAugments.Ensure(settings); PrimitiveAugments.Ensure(settings);
                var entries = new System.Collections.Generic.List<AugmentDefinition>(settings.augments);
                if (!entries.Exists(a => a != null && a.kind == AugmentKind.OrbitFire))
                    entries.Add(new AugmentDefinition { kind = AugmentKind.OrbitFire, title = "회전 불꽃",
                        description = "플레이어 주위를 도는 불꽃 2개 생성\n추가 획득마다 불꽃 +1개", amount = 1, maxStacks = 6 });
                if (!entries.Exists(a => a != null && a.kind == AugmentKind.BoneBoomerang))
                    entries.Add(new AugmentDefinition { kind = AugmentKind.BoneBoomerang, title = "뼈 부메랑",
                        description = "좌클릭을 누르면 조준 방향으로 뼈 투척\n돌아오면서도 적을 관통 공격", amount = 1, maxStacks = 1 });
                settings.augments = entries.ToArray();
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }
        public static void InstallAndBuild() { Install(); OctopusBossSetup.CreateAndBuild(); }
    }
}

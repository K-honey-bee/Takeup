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
                FireAugments.Ensure(settings); PrimitiveAugments.Ensure(settings); EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }
        public static void InstallAndBuild() { Install(); OctopusBossSetup.CreateAndBuild(); }
    }
}

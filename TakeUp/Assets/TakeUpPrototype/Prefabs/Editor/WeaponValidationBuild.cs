using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace TakeUp.Prototype.Editor
{
    public static class WeaponValidationBuild
    {
        public static void Build()
        {
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/TakeUpPrototype/Scenes/PrehistoricPrototype.unity", "Assets/TakeUpPrototype/Scenes/OctopusBossPrototype.unity" },
                locationPathName = "Builds/WeaponValidation/TakeUpWeapons.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Weapon validation build failed");
        }
    }
}

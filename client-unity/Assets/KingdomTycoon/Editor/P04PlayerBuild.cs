using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KingdomTycoon.Editor
{
    public static class P04PlayerBuild
    {
        public static void BuildAndroid()
        {
            P03ContentAssetGenerator.Verify();
            P04KingdomAssetGenerator.Verify();
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "artifacts", "player", "KingdomTycoon-P04-Development.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("Player output directory is invalid."));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException($"P04 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"P04 Android player build succeeded: {output}, bytes={report.summary.totalSize}");
        }
    }
}

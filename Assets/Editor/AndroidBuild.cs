using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Command line:
//   Unity.exe -batchmode -quit -projectPath <project> -buildTarget Android -executeMethod AndroidBuild.BuildApk -apkPath <output.apk> -logFile <log>
public static class AndroidBuild
{
    private const string ScenePath = "Assets/Scenes/TobiiSample_new.unity";

    [MenuItem("Build/Build Android APK")]
    public static void BuildApk()
    {
        string output = GetArgument("-apkPath")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Eye_Metric_V2.apk");

        EditorUserBuildSettings.buildAppBundle = false;
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"[AndroidBuild] result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} size={summary.totalSize} bytes output={output}");

        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }
        return null;
    }
}

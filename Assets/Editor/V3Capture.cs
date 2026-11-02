using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Design check without the robot: opens the build scene in the editor at Kebbi's 1024 x 600, plays it, saves a few
// screenshots of the home and quits. Run: Unity.exe -projectPath . -executeMethod V3Capture.Home -captureDir <dir>
// (not -batchmode: the game view has to draw).
[InitializeOnLoad]
public static class V3Capture
{
    private const string DirKey = "V3Capture.Dir";
    private static readonly double[] ShotsAt = { 3.0, 4.2, 7.5, 10.0 };
    private static double _start = -1;
    private static int _next;

    static V3Capture()
    {
        // entering play mode reloads the scripts; pick the run up again
        if (!string.IsNullOrEmpty(SessionState.GetString(DirKey, "")))
            EditorApplication.update += Tick;
    }

    public static void Home()
    {
        string dir = Arg("-captureDir") ?? Path.Combine(Application.dataPath, "..", "Temp", "V3Capture");
        Directory.CreateDirectory(dir);
        SessionState.SetString(DirKey, dir);
        EditorSceneManager.OpenScene("Assets/Scenes/TobiiSample_new.unity");
        SetGameViewSize(1024, 600);
        EditorApplication.isPlaying = true;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
            return;
        if (_start < 0)
            _start = EditorApplication.timeSinceStartup;
        double t = EditorApplication.timeSinceStartup - _start;
        string dir = SessionState.GetString(DirKey, "");
        if (_next < ShotsAt.Length && t >= ShotsAt[_next])
        {
            string path = Path.Combine(dir, $"home_{_next}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[V3Capture] " + path);
            _next++;
        }
        if (t >= ShotsAt[ShotsAt.Length - 1] + 1.5)
        {
            SessionState.EraseString(DirKey);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(0);
        }
    }

    private static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // Adds (once) and selects a fixed-resolution game view size; the editor keeps no public API for this.
    private static void SetGameViewSize(int width, int height)
    {
        var sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var groupType = (GameViewSizeGroupType)sizesType.GetProperty("currentGroupType").GetValue(instance);
        var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)groupType });
        var sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        var kindType = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
        int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < count && index < 0; i++)
        {
            var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((int)sizeType.GetProperty("width").GetValue(size) == width && (int)sizeType.GetProperty("height").GetValue(size) == height)
                index = i;
        }
        if (index < 0)
        {
            var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
            var size = ctor.Invoke(new object[] { Enum.ToObject(kindType, 1), width, height, "Kebbi" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = count;
        }
        var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        var gameView = EditorWindow.GetWindow(gameViewType);
        gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(gameView, new object[] { index, null });
    }
}

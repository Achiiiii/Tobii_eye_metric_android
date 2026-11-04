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
    private const string ShotsKey = "V3Capture.Shots";
    private const string ReplayKey = "V3Capture.Replay";
    private static double[] ShotsAt = { 3.0, 4.2, 7.5, 10.0 };
    private const string ChooseKey = "V3Capture.Choose";
    private const string RingsKey = "V3Capture.Rings";
    private static double _replayAt = -1, _chooseAt = -1, _ringsAt = -1;
    private static V3LiquidRing[] _rings;
    private static bool _ringLocked;
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
        // -shots 1.2,1.8,2.4: other moments (s after play starts)
        SessionState.SetString(ShotsKey, Arg("-shots") ?? "");
        // -replay 4: start the home's intro again at that moment (to see the loading once everything is warm)
        SessionState.SetString(ReplayKey, Arg("-replay") ?? "");
        // -choose 4: press the home's first mode (both eyes) at that moment, to see the pages after it
        SessionState.SetString(ChooseKey, Arg("-choose") ?? "");
        // -rings 4: from that moment, a sample of the progress rings on top of the screen (filling, wavering, locking)
        SessionState.SetString(RingsKey, Arg("-rings") ?? "");
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
        {
            _start = EditorApplication.timeSinceStartup;
            string shots = SessionState.GetString(ShotsKey, "");
            if (shots.Length > 0)
                ShotsAt = Array.ConvertAll(shots.Split(','), x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
            string replay = SessionState.GetString(ReplayKey, "");
            if (replay.Length > 0)
                _replayAt = double.Parse(replay, System.Globalization.CultureInfo.InvariantCulture);
            string rings = SessionState.GetString(RingsKey, "");
            if (rings.Length > 0)
                _ringsAt = double.Parse(rings, System.Globalization.CultureInfo.InvariantCulture);
            string choose = SessionState.GetString(ChooseKey, "");
            if (choose.Length > 0)
                _chooseAt = double.Parse(choose, System.Globalization.CultureInfo.InvariantCulture);
        }
        double t = EditorApplication.timeSinceStartup - _start;
        if (_replayAt >= 0 && t >= _replayAt)
        {
            _replayAt = -1;
            var home = UnityEngine.Object.FindObjectOfType<V3Home>();
            home?.GetType().GetMethod("StartIntro", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(home, null);
        }
        if (_ringsAt >= 0 && t >= _ringsAt)
        {
            if (_rings == null)
            {
                var canvas = UiFactory.CreateOverlayCanvas("RingSample", 30, null);
                var bg = UiFactory.CreateImage("Bg", canvas.transform, UiFactory.White, V3.LightFlat);
                UiFactory.Stretch(bg.rectTransform);
                _rings = new V3LiquidRing[3];
                for (int i = 0; i < 3; i++)
                {
                    var holder = UiFactory.CreateRect("Holder", canvas.transform);
                    UiFactory.Place(holder, new Vector2(0.25f + 0.25f * i, 0.5f), UiFactory.Center, Vector2.zero, Vector2.one * 240f);
                    _rings[i] = V3LiquidRing.Create(holder, 240f, i == 2 ? 26f * 2f : 46f, 4.2f, V3.A(i == 2 ? V3.Violet : V3.Groove, i == 2 ? 0.22f : 0.18f));
                }
            }
            float k = (float)(t - _ringsAt);
            // a still gaze, a wandering one, and one that completes and locks
            _rings[0].Fill = Mathf.Clamp01(k / 2.5f);
            _rings[1].Fill = Mathf.Clamp01(k / 2.5f);
            _rings[1].Steady = 0.15f;
            _rings[1].Lean = new Vector2(0.8f, 0.4f);
            _rings[2].Fill = Mathf.Clamp01(k / 1.2f);
            if (k > 1.2f && !_ringLocked && (_ringLocked = true))
                _rings[2].Lock();
        }
        if (_chooseAt >= 0 && t >= _chooseAt)
        {
            _chooseAt = -1;
            var home = UnityEngine.Object.FindObjectOfType<V3Home>();
            home?.GetType().GetMethod("Choose", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(home, new object[] { 0 });
        }
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

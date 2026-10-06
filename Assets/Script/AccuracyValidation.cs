using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Accuracy check, started from the debug overlay: nine dots on a 3x3 grid at 10/50/90% of the
// screen (deliberately off the calibration points) are shown one by one with the gaze dot hidden.
// For each, after the eyes have had time to land, one second of gaze is collected both as Tobii
// gives it (raw) and after the app's drift correction (corrected). Reports per point and overall:
// accuracy (distance of the mean gaze from the dot) and precision (RMS spread around that mean),
// in pixels and degrees, on screen ([VALID] log) and in <persistentDataPath>/validation.csv.
public class AccuracyValidation : MonoBehaviour
{
    private static readonly Vector2[] Points =
    {
        new Vector2(0.5f, 0.5f),
        new Vector2(0.1f, 0.9f), new Vector2(0.5f, 0.9f), new Vector2(0.9f, 0.9f),
        new Vector2(0.9f, 0.5f), new Vector2(0.9f, 0.1f), new Vector2(0.5f, 0.1f),
        new Vector2(0.1f, 0.1f), new Vector2(0.1f, 0.5f),
    };
    private const float ShrinkSeconds = 0.5f;
    private const float SettleSeconds = 0.6f;
    private const float CollectSeconds = 1f;
    private const int MinSamples = 5;
    private static readonly Color Background = new Color32(0x22, 0x26, 0x2B, 0xFF);
    private static readonly Color DotColor = new Color32(0xFF, 0xC2, 0x3D, 0xFF);
    private static readonly Color TargetColor = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color RawColor = new Color32(0xF2, 0x6D, 0x5B, 0xFF);
    private static readonly Color CorrectedColor = new Color32(0x5B, 0xD1, 0x8A, 0xFF);

    private FollowGazePoint2D _pointer;
    private HeadFollow _head;
    private StreamEngineDevice _device;
    private TMP_FontAsset _font;
    private GameObject _root;
    private RectTransform _area;
    private TextMeshProUGUI _text;
    private GameObject _buttons;
    private readonly List<Vector2> _raw = new List<Vector2>();
    private readonly List<Vector2> _corrected = new List<Vector2>();
    private bool _collecting;
    private bool _wasPaused;
    private int _run;

    public bool Running { get; private set; }

    public static AccuracyValidation Create(Transform parent, FollowGazePoint2D pointer, HeadFollow head, TMP_FontAsset font)
    {
        var canvas = UiFactory.CreateOverlayCanvas("AccuracyValidation", 40, parent);
        var v = canvas.gameObject.AddComponent<AccuracyValidation>();
        v._pointer = pointer;
        v._head = head;
        v._device = FindObjectOfType<StreamEngineDevice>();
        v._font = font;

        // Opaque, so the gaze dot and everything else stay hidden underneath.
        var bg = UiFactory.CreateImage("Background", canvas.transform, UiFactory.White, Background, true);
        UiFactory.Stretch(bg.rectTransform);
        v._area = UiFactory.Stretch(UiFactory.CreateRect("Area", canvas.transform));

        // Between the middle and bottom rows of dots, clear of the points.
        v._text = UiFactory.CreateText("Text", canvas.transform, font, "", 20f, Color.white);
        UiFactory.Place(v._text.rectTransform, new Vector2(0.5f, 0.34f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 80f));

        v._buttons = UiFactory.CreateRect("Buttons", canvas.transform).gameObject;
        v.AddButton("再測一次", new Vector2(-90f, 0f), v.Run);
        v.AddButton("關閉", new Vector2(90f, 0f), v.Close);

        v._root = canvas.gameObject;
        v._root.SetActive(false);
        return v;
    }

    private void AddButton(string label, Vector2 position, Action onClick)
    {
        var button = UiFactory.CreateButton(label, _buttons.transform, UiFactory.RoundedRect, new Color(1f, 1f, 1f, 0.18f), () => onClick());
        ((Image)button.targetGraphic).type = Image.Type.Sliced;
        UiFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), position, new Vector2(150f, 46f));
        var text = UiFactory.CreateText("Label", button.transform, _font, label, 22f, Color.white);
        UiFactory.Stretch(text.rectTransform);
    }

    public void Run()
    {
        if (Running)
            return;
        // Coroutines need an active object: show the overlay first.
        if (!_root.activeSelf)
        {
            _wasPaused = ButtonTrigger.Paused;
            _root.SetActive(true);
        }
        StartCoroutine(Validate());
    }

    private void Close()
    {
        if (Running)
            return;
        _root.SetActive(false);
        ButtonTrigger.Paused = _wasPaused;
    }

    private void OnEnable()
    {
        if (_pointer != null)
            _pointer.RawSampleAdded += OnRawSample;
    }

    private void OnDisable()
    {
        if (_pointer != null)
            _pointer.RawSampleAdded -= OnRawSample;
    }

    private void OnRawSample(Vector2 raw)
    {
        if (!_collecting)
            return;
        _raw.Add(raw);
        _corrected.Add(_pointer.Correction != null ? _pointer.Correction(raw) : raw);
    }

    private IEnumerator Validate()
    {
        Running = true;
        ButtonTrigger.Paused = true;
        _buttons.SetActive(false);
        Clear();
        _run++;
        _text.text = UiFactory.WithLatinFallback("準確度驗證\n請依序注視出現的圓點，頭部保持不動", _font);
        yield return new WaitForSecondsRealtime(2f);
        _text.text = "";

        var results = new List<Result>();
        foreach (var point in Points)
        {
            var dot = UiFactory.CreateImage("Dot", _area, UiFactory.Circle, DotColor);
            UiFactory.Place(dot.rectTransform, point, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            dot.rectTransform.localScale = Vector3.one * 3.5f;
            dot.rectTransform.DOScale(1f, ShrinkSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
            yield return new WaitForSecondsRealtime(ShrinkSeconds + SettleSeconds);

            _raw.Clear();
            _corrected.Clear();
            _collecting = true;
            yield return new WaitForSecondsRealtime(CollectSeconds);
            _collecting = false;
            Destroy(dot.gameObject);

            var target = new Vector2(point.x * Screen.width, point.y * Screen.height);
            results.Add(new Result(point, target, new List<Vector2>(_raw), new List<Vector2>(_corrected)));
            yield return new WaitForSecondsRealtime(0.15f);
        }

        Report(results);
        _buttons.SetActive(true);
        Running = false;
    }

    private void Report(List<Result> results)
    {
        float distanceMm = _head != null && _head.Distance > 0.1f ? _head.Distance * 1000f : 400f;
        Vector2 displayMm = _device != null ? _device.DisplaySizeMm : new Vector2(154f, 86f);
        float mmPerPx = displayMm.x / Screen.width;
        float Degrees(float px) => Mathf.Atan(px * mmPerPx / distanceMm) * Mathf.Rad2Deg;

        var valid = results.FindAll(r => r.Raw.Count >= MinSamples);
        float rawAcc = 0f, corAcc = 0f, precision = 0f;
        foreach (var r in valid)
        {
            rawAcc += r.RawError;
            corAcc += r.CorrectedError;
            precision += r.Precision;
        }
        if (valid.Count > 0)
        {
            rawAcc /= valid.Count;
            corAcc /= valid.Count;
            precision /= valid.Count;
        }

        string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var csv = new StringBuilder();
        foreach (var r in results)
        {
            DrawResult(r);
            string line = r.Raw.Count < MinSamples
                ? $"[VALID] run {_run} point ({r.Point.x:0.0},{r.Point.y:0.0}): no gaze ({r.Raw.Count} samples)"
                : $"[VALID] run {_run} point ({r.Point.x:0.0},{r.Point.y:0.0}): raw offset {r.RawOffset.x:+0;-0},{r.RawOffset.y:+0;-0} px ({r.RawError:0} px, {Degrees(r.RawError):0.00} deg) | corrected {r.CorrectedOffset.x:+0;-0},{r.CorrectedOffset.y:+0;-0} px ({r.CorrectedError:0} px) | precision {r.Precision:0} px | {r.Raw.Count} samples";
            Debug.Log(line);
            csv.AppendLine(string.Join(",", stamp, _run, r.Point.x.ToString("0.0", CultureInfo.InvariantCulture), r.Point.y.ToString("0.0", CultureInfo.InvariantCulture),
                r.Raw.Count, F(r.RawOffset.x), F(r.RawOffset.y), F(r.RawError), F(r.CorrectedOffset.x), F(r.CorrectedOffset.y), F(r.CorrectedError), F(r.Precision), F(distanceMm / 10f)));
        }
        string summary = $"[VALID] run {_run} summary: raw {rawAcc:0} px ({Degrees(rawAcc):0.00} deg) | corrected {corAcc:0} px ({Degrees(corAcc):0.00} deg) | precision {precision:0} px ({Degrees(precision):0.00} deg) | {valid.Count}/{results.Count} points | eyes at {distanceMm / 10f:0} cm";
        Debug.Log(summary);
        SaveCsv(csv.ToString());

        _text.text = UiFactory.WithLatinFallback(
            $"原始 {rawAcc:0} px（{Degrees(rawAcc):0.0}°）　校正後 {corAcc:0} px（{Degrees(corAcc):0.0}°）\n精確度 {precision:0} px（{Degrees(precision):0.0}°）　{valid.Count}/{results.Count} 點　距離 {distanceMm / 10f:0} cm", _font);
    }

    private static string F(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    private void SaveCsv(string rows)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "validation.csv");
            if (!File.Exists(path))
                File.WriteAllText(path, "time,run,x,y,samples,raw_dx,raw_dy,raw_px,corr_dx,corr_dy,corr_px,precision_px,distance_cm\n");
            File.AppendAllText(path, rows);
        }
        catch (Exception e)
        {
            Debug.LogError("[VALID] " + e.Message);
        }
    }

    // Target ring, mean raw gaze (red) and mean corrected gaze (green), each joined to the target.
    private void DrawResult(Result r)
    {
        var ring = UiFactory.CreateImage("Target", _area, UiFactory.Ring, TargetColor);
        UiFactory.Place(ring.rectTransform, r.Point, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
        if (r.Raw.Count < MinSamples)
            return;
        DrawMark(r.Point, r.RawMean - r.Target, RawColor);
        DrawMark(r.Point, r.CorrectedMean - r.Target, CorrectedColor);
    }

    private void DrawMark(Vector2 anchor, Vector2 offsetPx, Color color)
    {
        // Gaze offsets are in screen pixels; convert to canvas units so the marks land where the gaze was.
        float scale = _area.rect.width > 0f ? _area.rect.width / Screen.width : 1f;
        Vector2 offset = offsetPx * scale;
        var line = UiFactory.CreateImage("Line", _area, UiFactory.White, new Color(color.r, color.g, color.b, 0.7f));
        UiFactory.Place(line.rectTransform, anchor, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(offset.magnitude, 2f));
        line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
        var mark = UiFactory.CreateImage("Mark", _area, UiFactory.Circle, color);
        UiFactory.Place(mark.rectTransform, anchor, new Vector2(0.5f, 0.5f), offset, new Vector2(14f, 14f));
    }

    private void Clear()
    {
        for (int i = _area.childCount - 1; i >= 0; i--)
            Destroy(_area.GetChild(i).gameObject);
    }

    private sealed class Result
    {
        public readonly Vector2 Point;
        public readonly Vector2 Target;
        public readonly List<Vector2> Raw;
        public readonly Vector2 RawMean;
        public readonly Vector2 CorrectedMean;
        public readonly float Precision;

        public Result(Vector2 point, Vector2 target, List<Vector2> raw, List<Vector2> corrected)
        {
            Point = point;
            Target = target;
            Raw = raw;
            RawMean = Mean(raw);
            CorrectedMean = Mean(corrected);
            float sum = 0f;
            foreach (var s in raw)
                sum += (s - RawMean).sqrMagnitude;
            Precision = raw.Count > 0 ? Mathf.Sqrt(sum / raw.Count) : 0f;
        }

        // Offsets reported as target - gaze, like the [OPTION] / [DRIFT] logs.
        public Vector2 RawOffset => Target - RawMean;
        public Vector2 CorrectedOffset => Target - CorrectedMean;
        public float RawError => RawOffset.magnitude;
        public float CorrectedError => CorrectedOffset.magnitude;

        private static Vector2 Mean(List<Vector2> points)
        {
            if (points.Count == 0)
                return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (var p in points)
                sum += p;
            return sum / points.Count;
        }
    }
}

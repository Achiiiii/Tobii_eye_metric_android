using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// V3 head position page: the camera mirror with a face target, Kebbi's line,
// the distance scale (20-60 cm, right zone 30-50), the height scale (+-25 deg, right zone as HeadFollow
// accepts) and the status line with the 3 s hold bar. When the eyes sit above the accepted height, a
// "raise Kebbi" tip takes the mirror's place. DetectDistance still runs the check and the speech.
public class V3Position : MonoBehaviour
{
    private const float D0 = 20f, D1 = 60f;           // distance scale, cm
    private const float HScale = 25f;                 // height scale, +-deg
    private const float TrackX = 333f, TrackY = 346f, TrackW = 472f, TrackH = 48f;
    private const float VX = 877f, VY = 213f, VW = 48f, VH = 219f;
    private const float ConfirmedSeconds = 1.8f;      // as EyeMetricFlow's head confirmed pause

    private DetectDistance _detect;
    private HeadFollow _head;
    private GameObject _trackBox;
    private MetricTest _test;
    private V3Flow _flow;
    private Action _back;

    private GameObject _page;
    private V3.SideParts _side, _sideSingle;
    private V3.PanelParts _panel;
    private GameObject _who, _raise;
    private V3.LineParts _line;
    private RawImage _mirror;
    private Image _silhouette;
    private Image[] _target;
    private Texture2D _mirrorTex;
    private Color32[] _mirrorPx;
    private float _mirrorAt;
    private bool _mirrorLive;
    private RectTransform _dKnob, _hKnob, _dHint, _hHint;
    private Image _dKnobFill, _hKnobFill, _dWin, _hWin;
    private TextMeshProUGUI _dKnobText, _hKnobText;
    private Image _statDot;
    private TextMeshProUGUI _statText, _statSub;
    private RectTransform _hold;
    private float _shownD = 0.5f, _shownH;
    private float _confirmedUntil = -1f;
    private bool _wasDOk, _wasHOk;

    public static V3Position Create(V3Flow flow, DetectDistance detect, HeadFollow head, GameObject trackBox, MetricTest test, Action back)
    {
        var root = V3.Full("Position", flow.Pages);
        var page = root.gameObject.AddComponent<V3Position>();
        page._flow = flow;
        page._detect = detect;
        page._head = head;
        page._trackBox = trackBox;
        page._test = test;
        page._back = back;
        page._page = V3.Full("View", root).gameObject;
        page.Build();
        page._page.SetActive(false);
        V3Flow.Conceal(trackBox);
        detect.HeadPositionConfirmed += () => page._confirmedUntil = Time.unscaledTime + ConfirmedSeconds;
        return page;
    }

    private void Build()
    {
        var t = _page.transform;
        _side = V3.Side(t, "Setup", 3, new[] { "位置", "校正", "練習" }, new[] { "Position", "Calibrate", "Practice" });
        _sideSingle = V3.Side(t, "Setup", 4, new[] { "位置", "遮眼", "校正", "練習" }, new[] { "Position", "Cover", "Calibrate", "Practice" });
        V3.SetStep(_side, 0);
        V3.SetStep(_sideSingle, 0);
        V3.Key(t, "上一步", "Back", "arrowL", new Color32(0xED, 0xED, 0xEA, 0xFF), 40f, 504f, 224f, true, () => _back?.Invoke());

        _panel = V3.Panel(t, "person", "頭部位置", "Head position", "HEAD POSITION");
        V3.SetTag(_panel, V3.T("自動偵測", "Automatic"));
        var p = _panel.Root;   // panel space: page x - 304, y - 132

        // mirror + Kebbi's line (normal) / raise tip (eyes too high)
        _who = V3.Rect("Who", p, 0f, 0f, 680f, 428f).gameObject;
        BuildMirror(_who.transform, 341f - 304f, 198f - 132f);
        _line = V3.KebbiLine(_who.transform, 447f - 304f, 198f - 132f, 30f, 300f);
        _line.Root.Find("Kebbi").gameObject.SetActive(false);
        _raise = BuildRaise(p);

        BuildDistanceScale(p);
        BuildHeightScale(p);

        // status line on a rule
        V3.Box("StatRule", p, V3.A(V3.Ink, 0.12f), 29f, 446f - 132f, 622f, 1f);
        _statDot = V3.Sprite("Dot", p, UiFactory.Circle, V3.Ink, 29f, 471f - 132f, 10f, 10f);
        _statText = V3.Text("Status", p, V3.Zh, "", 24f, V3.Ink, 51f, 459f - 132f, 600f, 32f, TextAlignmentOptions.TopLeft);
        _statSub = V3.Text("Sub", p, V3.ZhMedium, "", 17f, V3.Text2, 51f, 498f - 132f, 600f, 24f, TextAlignmentOptions.TopLeft);
        _statSub.enableAutoSizing = true;
        _statSub.fontSizeMin = 13f;
        _statSub.fontSizeMax = 17f;
        V3.Box("HoldTrack", p, V3.A(V3.Ink, 0.1f), 29f, 532f - 132f, 622f, 3f);
        _hold = V3.Box("Hold", p, V3.Green, 29f, 532f - 132f, 0f, 3f).rectTransform;
    }

    private void BuildMirror(Transform parent, float x, float y)
    {
        var frame = V3.Rect("Mirror", parent, x, y, 160f, 120f);
        V3.Box("Bg", frame, new Color32(0xE2, 0xE2, 0xDF, 0xFF), 0f, 0f, 160f, 120f);
        _silhouette = V3.Sprite("Head", frame, HeadSprite(), V3.Ink, 50f, 33f, 60f, 60f);
        _mirror = new GameObject("Camera", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        _mirror.transform.SetParent(frame, false);
        V3.At(_mirror.rectTransform, 0f, 0f, 160f, 120f);
        _mirror.raycastTarget = false;
        // mirrored like a looking glass
        _mirror.uvRect = new Rect(1f, 0f, -1f, 1f);
        _mirror.enabled = false;
        var mask = V3.Sprite("Target", frame, TargetSprite(), Color.white, 0f, 0f, 160f, 120f);
        _target = new[] { mask };
    }

    // The raise tip: Kebbi on two boxes, a violet up arrow, "把凱比墊高" + how.
    private GameObject BuildRaise(Transform p)
    {
        var r = V3.Rect("Raise", p, 349f - 304f, 198f - 132f, 456f, 120f);
        V3.Box("Fill", r, V3.A(V3.Violet, 0.05f), 0f, 0f, 456f, 120f);
        V3.Hairline(r, V3.Violet, 0f, 0f, 456f, 120f);
        V3.Sprite("Kebbi", r, V3.KebbiSprite, Color.white, 23f, 21f, 42f, 42f);
        V3.Box("Box1", r, Color.white, 21f, 66f, 46f, 9f);
        V3.Hairline(r, V3.Ink, 21f, 66f, 46f, 9f);
        V3.Box("Box2", r, V3.MintSoft, 19f, 76f, 50f, 9f);
        V3.Hairline(r, V3.Ink, 19f, 76f, 50f, 9f);
        V3.Sprite("Arrow", r, V3.Icon("arrowU"), V3.Violet, 74f, 28f, 36f, 36f);
        V3.Text("Title", r, V3.Zh, "把凱比墊高", "Raise Kebbi", 26f, V3.Violet, 125f, 14f, 320f, 34f);
        var sub = V3.Text("Sub", r, V3.ZhMedium, "放在書本或盒子上，\n讓鏡頭和您的眼睛一樣高", "Set it on books or a box so the\ncamera is level with your eyes", 18f, V3.Text2, 125f, 52f, 320f, 56f);
        sub.lineSpacing = -6f;
        r.gameObject.SetActive(false);
        return r.gameObject;
    }

    private void BuildDistanceScale(Transform p)
    {
        float x = TrackX - 304f, y = TrackY - 132f;
        V3.Box("Track", p, Color.white, x, y, TrackW, TrackH);
        V3.Hairline(p, V3.A(V3.Ink, 0.2f), x, y, TrackW, TrackH);
        float w0 = DX(DetectDistance.MinDistanceMeters * 100f), w1 = DX(DetectDistance.MaxDistanceMeters * 100f);
        _dWin = V3.Box("Window", p, V3.MintSoft, x + w0, y, w1 - w0, TrackH);
        V3.Hairline(_dWin.transform, V3.Green, 0f, 0f, w1 - w0, TrackH);
        V3.Text("Near", p, V3.ZhMedium, "近", "Near", 20f, V3.Text2, x + 14f, y + 12f, 80f, 24f);
        V3.Text("Far", p, V3.ZhMedium, "遠", "Far", 20f, V3.Text2, x + TrackW - 94f, y + 12f, 80f, 24f, TextAlignmentOptions.TopRight);
        for (int cm = (int)D0; cm <= D1; cm++)
        {
            bool major = cm % 5 == 0;
            V3.Box("Tick", p, major ? V3.Ink : V3.A(V3.Ink, 0.3f), x + DX(cm) - 1f, y + 56f, 2f, major ? 10f : 6f);
            if (major)
                V3.Text("Num", p, V3.Mono, cm.ToString(), 14f, V3.Text2, x + DX(cm) - 20f, y + 69f, 40f, 16f, TextAlignmentOptions.Top);
        }
        _dKnob = Knob(p, "DistanceKnob", out _dKnobFill, out _dKnobText);
        _dHint = Hint(p, "DistanceHint");
    }

    private void BuildHeightScale(Transform p)
    {
        float x = VX - 304f, y = VY - 132f;
        V3.Text("Cap", p, V3.Zh, "高度", "Height", 18f, V3.Ink, 849f - 304f, 183f - 132f, 100f, 22f);
        V3.Box("Track", p, Color.white, x, y, VW, VH);
        V3.Hairline(p, V3.A(V3.Ink, 0.2f), x, y, VW, VH);
        float top = VYPos(HeadFollow.EyeLevelHighDeg), bottom = VYPos(HeadFollow.EyeLevelLowDeg);
        _hWin = V3.Box("Window", p, V3.MintSoft, x, y + top, VW, bottom - top);
        V3.Hairline(_hWin.transform, V3.Green, 0f, 0f, VW, bottom - top);
        V3.Text("High", p, V3.ZhMedium, "高", "Hi", 20f, V3.Text2, x, y + 10f, VW, 24f, TextAlignmentOptions.Top);
        V3.Text("Low", p, V3.ZhMedium, "低", "Lo", 20f, V3.Text2, x, y + VH - 34f, VW, 24f, TextAlignmentOptions.Top);
        for (int d = -(int)HScale; d <= HScale; d++)
        {
            bool major = d % 5 == 0;
            float w = major ? 14f : 8f;
            V3.Box("Tick", p, major ? V3.Ink : V3.A(V3.Ink, 0.3f), x - 8f - w, y + VYPos(d) - 1f, w, 2f);
        }
        _hKnob = Knob(p, "HeightKnob", out _hKnobFill, out _hKnobText);
        _hHint = Hint(p, "HeightHint");
    }

    private static RectTransform Knob(Transform p, string name, out Image fill, out TextMeshProUGUI text)
    {
        var k = V3.Rect(name, p, 0f, 0f, 52f, 52f);
        fill = V3.Box("Fill", k, Color.white, 0f, 0f, 52f, 52f);
        V3.Hairline(k, V3.Ink, 0f, 0f, 52f, 52f, 1.5f);
        text = V3.Text("Value", k, V3.Mono, "", 21f, V3.Ink, 0f, 0f, 52f, 52f, TextAlignmentOptions.Center);
        return k;
    }

    private static RectTransform Hint(Transform p, string name)
    {
        var h = V3.Rect(name, p, 0f, 0f, 34f, 34f);
        V3.Box("Fill", h, V3.Mint, 0f, 0f, 34f, 34f);
        var arrow = V3.Sprite("Arrow", h, V3.Icon("arrowR"), V3.Ink, 5f, 5f, 24f, 24f).rectTransform;
        // turn about its centre
        arrow.pivot = UiFactory.Center;
        arrow.anchoredPosition = new Vector2(17f, -17f);
        return h;
    }

    private static float DX(float cm) => Mathf.InverseLerp(D0, D1, cm) * TrackW;
    private static float VYPos(float deg) => (HScale - Mathf.Clamp(deg, -HScale, HScale)) / (2f * HScale) * VH;

    private bool Confirmed => Time.unscaledTime < _confirmedUntil;

    private void Update()
    {
        bool show = _flow.FlowVisible && ((_detect.IsChecking && _trackBox.activeInHierarchy) || Confirmed);
        if (_page.activeSelf != show)
        {
            _page.SetActive(show);
            SetMirrorLive(show);
            if (show)
            {
                V3.SetTag(_panel, V3.T("自動偵測", "Automatic"));
                V3.SetLine(_line, V3.T("請把頭對準框框，\n臉離螢幕約 40 公分。", "Put your head in the frame,\nabout 40 cm from the screen."));
                bool single = _test.Mode == MetricTest.TestMode.Single;
                _side.Root.gameObject.SetActive(!single);
                _sideSingle.Root.gameObject.SetActive(single);
            }
        }
        if (!show)
            return;
        _flow.SetProgress(1f);

        float cm = _detect.DistanceMeters * 100f;
        float deg = _head != null && _head.HasFace ? _head.EyeElevationDeg : 0f;
        float k = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
        _shownD = Mathf.Lerp(_shownD, cm, k);
        _shownH = Mathf.Lerp(_shownH, deg, k);
        bool dOk = _detect.Zone == DetectDistance.DistanceZone.InRange;
        bool hOk = _detect.EyeLevel == 0;
        bool ok = Confirmed || _detect.PositionOk;
        if ((dOk && !_wasDOk) || (hOk && !_wasHOk && dOk))
            V3Sounds.Play(V3Sounds.Cue.InRange);
        _wasDOk = dOk;
        _wasHOk = hOk;

        // distance knob + hint chip on the side toward the right zone
        float kx = TrackX - 304f + DX(_shownD) - 26f;
        _dKnob.anchoredPosition = new Vector2(kx, -(TrackY - 132f - 4f));
        _dKnobText.text = Mathf.RoundToInt(cm).ToString();
        _dKnobFill.color = dOk ? V3.Mint : Color.white;
        _dHint.gameObject.SetActive(!dOk && !Confirmed);
        bool far = _detect.Zone == DetectDistance.DistanceZone.TooFar;
        _dHint.anchoredPosition = new Vector2(far ? kx - 46f : kx + 52f + 12f, -(TrackY - 132f + 7f));
        _dHint.GetChild(1).localEulerAngles = new Vector3(0f, 0f, far ? 180f : 0f);

        // height knob shows the eyes' height against the camera in cm at this distance
        float ky = VY - 132f + VYPos(_shownH) - 26f;
        _hKnob.anchoredPosition = new Vector2(VX - 304f - 4f, -ky);
        int offset = Mathf.RoundToInt(cm * Mathf.Tan(deg * Mathf.Deg2Rad));
        _hKnobText.text = offset > 0 ? "+" + offset : offset.ToString();
        _hKnobFill.color = hOk ? V3.Mint : Color.white;
        _hHint.gameObject.SetActive(!hOk && !Confirmed);
        _hHint.anchoredPosition = new Vector2(VX - 304f - 4f + 50f, -(ky + 9f));
        _hHint.GetChild(1).localEulerAngles = new Vector3(0f, 0f, _detect.EyeLevel > 0 ? -90f : 90f);

        bool raise = dOk && _detect.EyeLevel > 0 && !Confirmed;
        _raise.SetActive(raise);
        _who.SetActive(!raise);

        string text, sub = "";
        if (Confirmed)
            text = V3.T("頭部位置確認完成", "Head position confirmed");
        else if (!dOk)
            text = far ? V3.T("請往前靠近一點", "Please move a little closer") : V3.T("請往後遠離一點", "Please move back a little");
        else if (!hOk)
        {
            text = V3.T("請調整高度", "Please adjust the height");
            int need = HeightOffsetCm(_detect.EyeLevel);
            sub = _detect.EyeLevel > 0
                ? V3.T($"眼睛比鏡頭高，請把凱比墊高或把座椅調低（約差 {need} 公分）", $"Eyes above the camera: raise Kebbi or lower the seat (about {need} cm)")
                : V3.T($"眼睛比鏡頭低，請把座椅調高或把凱比放低（約差 {need} 公分）", $"Eyes below the camera: raise the seat or lower Kebbi (about {need} cm)");
        }
        else
            text = V3.T("很好，請保持不動", "Good, please hold still");
        _statText.text = text;
        _statSub.text = sub;
        _statText.color = ok ? V3.Green : V3.Ink;
        _statDot.color = ok ? V3.Green : V3.Ink;
        _hold.sizeDelta = new Vector2(622f * (Confirmed ? 1f : _detect.HoldProgress), 3f);

        foreach (var g in _target)
            g.color = ok ? V3.Mint : Color.white;
        _silhouette.color = ok ? V3.Green : V3.Ink;
        // the stand-in head follows distance and height when there is no camera picture
        float scale = Mathf.Clamp(40f / Mathf.Max(1f, _shownD), 0.55f, 1.7f);
        _silhouette.rectTransform.localScale = Vector3.one * scale;
        _silhouette.rectTransform.anchoredPosition = new Vector2(50f, -33f - Mathf.Clamp(-_shownH * 2.6f, -34f, 34f));
    }

    // How far the eyes are beyond the accepted height, in cm, at the current distance (at least 1).
    private int HeightOffsetCm(int side)
    {
        float edge = side > 0 ? HeadFollow.EyeLevelHighDeg : HeadFollow.EyeLevelLowDeg;
        float meters = _detect.DistanceMeters * Mathf.Abs(Mathf.Tan(_head.EyeElevationDeg * Mathf.Deg2Rad) - Mathf.Tan(edge * Mathf.Deg2Rad));
        return Mathf.Max(1, Mathf.CeilToInt(meters * 100f));
    }

    // ==================== camera mirror ====================

    private void SetMirrorLive(bool on)
    {
        if (on == _mirrorLive)
            return;
        _mirrorLive = on;
        if (on)
            AndroidWebcamCaptureClient.PreviewSink = OnCameraFrame;
        else if (AndroidWebcamCaptureClient.PreviewSink == (Action<sbyte[], int, int>)OnCameraFrame)
            AndroidWebcamCaptureClient.PreviewSink = null;
    }

    private void OnDisable() => SetMirrorLive(false);

    // About 12 updates a second, shrunk 4x into a small texture; shown only, never kept.
    private void OnCameraFrame(sbyte[] grey, int width, int height)
    {
        if (Time.unscaledTime < _mirrorAt || grey == null || grey.Length < width * height)
            return;
        _mirrorAt = Time.unscaledTime + 0.08f;
        const int step = 4;
        int w = width / step, h = height / step;
        if (_mirrorTex == null || _mirrorTex.width != w || _mirrorTex.height != h)
        {
            _mirrorTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            _mirrorPx = new Color32[w * h];
            _mirror.texture = _mirrorTex;
            // crop the camera picture to the mirror's 4:3
            float aspect = (float)w / h, want = 160f / 120f;
            float u = aspect > want ? want / aspect : 1f, v = aspect > want ? 1f : aspect / want;
            _mirror.uvRect = new Rect(1f - (1f - u) / 2f, (1f - v) / 2f, -u, v);
        }
        for (int y = 0; y < h; y++)
        {
            int src = y * step * width, dst = (h - 1 - y) * w;   // camera rows run top-down
            for (int x = 0; x < w; x++)
            {
                byte g = (byte)grey[src + x * step];
                _mirrorPx[dst + x] = new Color32(g, g, g, 255);
            }
        }
        _mirrorTex.SetPixels32(_mirrorPx);
        _mirrorTex.Apply(false);
        _mirror.enabled = true;
        _silhouette.enabled = false;
    }

    // ==================== sprites ====================

    private static Sprite s_head, s_target;

    // Head and shoulders silhouette (60 x 60).
    public static Sprite HeadSprite()
    {
        return s_head != null ? s_head : (s_head = V3.Raster(120, p =>
        {
            Vector2 q = new Vector2(p.x / 2f, 60f - p.y / 2f);
            float head = (q - new Vector2(30f, 23f)).magnitude - 13f;
            float body = Mathf.Max((q - new Vector2(30f, 66f)).magnitude - 24f, 40f - q.y);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - Mathf.Min(head, body) * 2f));
        }));
    }

    // Face target over the mirror (160 x 120): a soft dark mask outside an oval, the oval, corner brackets,
    // eye-line and side ticks. White; tinted mint once the position is right.
    public static Sprite TargetSprite()
    {
        if (s_target != null)
            return s_target;
        const int k = 2, w = 160 * k, h = 120 * k;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 q = new Vector2((x + 0.5f) / k, 120f - (y + 0.5f) / k);
                float e = Mathf.Sqrt(Sq((q.x - 80f) / 27f) + Sq((q.y - 56f) / 35f));
                float edge = (e - 1f) * 30f;                       // ~px from the oval
                float oval = Mathf.Clamp01(0.5f - (Mathf.Abs(edge) - 0.75f) * k);
                float mask = e > 1f ? 0.28f : 0f;
                float lines = 0f;
                foreach (var (ax, ay, bx, by) in Segments)
                    lines = Mathf.Max(lines, Mathf.Clamp01(0.5f - (SegDist(q, new Vector2(ax, ay), new Vector2(bx, by)) - 1.25f) * k));
                float white = Mathf.Max(oval * 0.9f, lines);
                // dark mask (black) under the white strokes
                float a = white + mask * (1f - white);
                float c = a > 0f ? white / a : 0f;
                px[y * w + x] = new Color(c, c, c, a);
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return s_target = UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static readonly (float, float, float, float)[] Segments =
    {
        (47, 25, 47, 17), (47, 17, 57, 17), (113, 25, 113, 17), (113, 17, 103, 17),
        (47, 87, 47, 95), (47, 95, 57, 95), (113, 87, 113, 95), (113, 95, 103, 95),
        (80, 14, 80, 18), (80, 94, 80, 98), (44, 56, 48, 56), (112, 56, 116, 56),
    };

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    private static float Sq(float v) => v * v;
}

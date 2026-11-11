using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// V3 head position page: the round camera mirror with a face target in a glass rim on the left; on the right
// the instruction, the distance scale (20-60 cm, right zone lit) and the height scale (+-25 deg, right zone as
// HeadFollow accepts) with violet markers, the status line and the hold guide. When the eyes sit above the accepted
// height, a "raise Kebbi" tip takes the instruction's place. DetectDistance still runs the check and the speech.
public class V3Position : MonoBehaviour
{
    private const float D0 = 20f, D1 = 60f;           // distance scale, cm
    private const float HScale = 25f;                 // height scale, +-deg
    private const float ScaleX = 470f, ScaleW = 480f;
    private const float ConfirmedSeconds = 1.8f;      // as EyeMetricFlow's head confirmed pause

    private DetectDistance _detect;
    private HeadFollow _head;
    private GameObject _trackBox;
    private MetricTest _test;
    private V3Flow _flow;
    private Action _back;

    private GameObject _page;
    private GameObject _who, _raise;
    private RawImage _mirror;
    private Image _silhouette;
    private Image[] _target;
    private Texture2D _mirrorTex;
    private Color32[] _mirrorPx;
    private float _mirrorAt;
    private bool _mirrorLive;
    private RectTransform _dMark, _hMark;
    private Image _statDot;
    private TextMeshProUGUI _statText, _statSub;
    private V3.GuideParts _hold;
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
        page._page.AddComponent<V3PageIn>();
        page.Build();
        page._page.SetActive(false);
        V3Flow.Conceal(trackBox);
        detect.HeadPositionConfirmed += () => page._confirmedUntil = Time.unscaledTime + ConfirmedSeconds;
        return page;
    }

    private void Build()
    {
        var t = _page.transform;
        V3.Note(t, 40f, 70f, 600f, "").gameObject.AddComponent<V3Loc>().Set("頭部位置  ·  自動偵測", "Head position  ·  automatic", 4f, 4f);
        V3.Key(t, "上一步", "Back", "arrowL", Color.white, 40f, 470f, 210f, false, () => _back?.Invoke());

        // the round mirror in its glass rim
        BuildMirror(t, 76f, 120f);
        V3.GlassRim(t, 226f, 270f, 153f, 8f);

        // the instruction (normal) / raise tip (eyes too high)
        _who = V3.Rect("Who", t, 0f, 0f, V3.W, V3.H).gameObject;
        var title = V3.Title(_who.transform, ScaleX, 106f, 500f, 90f, V3Type.Title);
        title.gameObject.AddComponent<V3Loc>().Set("請把頭對準框框，\n臉離螢幕約 40 公分。", "Put your head in the frame,\nabout 40 cm from the screen.", 3f, 0f);
        _raise = BuildRaise(t);

        V3.Note(t, ScaleX, 210f, 300f).gameObject.AddComponent<V3Loc>().Set("距離  ·  公分", "DISTANCE  ·  CM", 4f, 4f);
        float z0 = Mathf.InverseLerp(D0, D1, DetectDistance.MinDistanceMeters * 100f), z1 = Mathf.InverseLerp(D0, D1, DetectDistance.MaxDistanceMeters * 100f);
        _dMark = V3.Scale(t, ScaleX, 250f, ScaleW, z0, z1, new[] { "20", "30", "40", "50", "60" });
        V3.Note(t, ScaleX, 310f, 300f).gameObject.AddComponent<V3Loc>().Set("高度", "HEIGHT", 4f, 4f);
        _hMark = V3.Scale(t, ScaleX, 350f, ScaleW, HX(HeadFollow.EyeLevelLowDeg), HX(HeadFollow.EyeLevelHighDeg),
            new[] { "低", "", "平視", "", "高" }, new[] { "LOW", "", "LEVEL", "", "HIGH" });

        // status line and the hold guide
        _statDot = V3.Sprite("Dot", t, UiFactory.Circle, V3.Ink, ScaleX, 420f, 12f, 12f);
        V3LampGlow.Attach(_statDot, 12f);
        _statText = V3.Text("Status", t, V3.Zh, "", V3Type.Body, V3.Ink, ScaleX + 22f, 410f, 470f, 28f, TextAlignmentOptions.TopLeft);
        _statSub = V3.Text("Sub", t, V3.ZhMedium, "", V3Type.Label, V3.Text2, ScaleX + 22f, 440f, 470f, 20f, TextAlignmentOptions.TopLeft);
        _statSub.enableAutoSizing = true;
        _statSub.fontSizeMin = V3Type.Data;
        _statSub.fontSizeMax = V3Type.Label;
        _hold = V3.Guide(t, ScaleX, 472f, ScaleW);
    }

    // height on the scale: -25 deg (low) on the left to +25 deg (high) on the right
    private static float HX(float deg) => (Mathf.Clamp(deg, -HScale, HScale) + HScale) / (2f * HScale);

    // A 300 px round mirror: the camera picture (4:3, cropped by the circle) under the face target.
    private void BuildMirror(Transform parent, float x, float y)
    {
        var disc = V3.Sprite("Mirror", parent, UiFactory.Circle, V3.Silver, x, y, 300f, 300f);
        disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var frame = disc.transform;
        _silhouette = V3.Sprite("Head", frame, HeadSprite(), V3.A(V3.Ink, 0.45f), 75f, 70f, 150f, 150f);
        _silhouette.rectTransform.pivot = UiFactory.Center;
        _mirror = new GameObject("Camera", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        _mirror.transform.SetParent(frame, false);
        V3.At(_mirror.rectTransform, -50f, 0f, 400f, 300f);
        _mirror.raycastTarget = false;
        // mirrored like a looking glass
        _mirror.uvRect = new Rect(1f, 0f, -1f, 1f);
        _mirror.enabled = false;
        var mask = V3.Sprite("Target", frame, TargetSprite(), Color.white, -50f, 0f, 400f, 300f);
        _target = new[] { mask };
    }

    // The raise tip: a violet up arrow, "把凱比墊高" + how.
    private GameObject BuildRaise(Transform p)
    {
        var r = V3.Rect("Raise", p, ScaleX, 100f, 500f, 100f);
        V3.Sprite("Arrow", r, V3.Icon("arrowU"), V3.Violet, 0f, 8f, 36f, 36f);
        V3.Text("Title", r, V3.Zh, "把凱比墊高", "Raise Kebbi", V3Type.Title, V3.Violet, 48f, 4f, 440f, 38f);
        var sub = V3.Text("Sub", r, V3.ZhMedium, "放在書本或盒子上，\n讓鏡頭和您的眼睛一樣高", "Set it on books or a box so the\ncamera is level with your eyes", V3Type.BodyS, V3.Text2, 48f, 46f, 440f, 50f);
        sub.lineSpacing = -6f;
        r.gameObject.SetActive(false);
        return r.gameObject;
    }

    private bool Confirmed => Time.unscaledTime < _confirmedUntil;

    private void Update()
    {
        bool show = _flow.FlowVisible && ((_detect.IsChecking && _trackBox.activeInHierarchy) || Confirmed);
        if (_page.activeSelf != show)
        {
            _page.SetActive(show);
            SetMirrorLive(show);
        }
        if (!show)
            return;
        _flow.NoRuler = true;

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

        // the markers on the two scales
        bool far = _detect.Zone == DetectDistance.DistanceZone.TooFar;
        V3.SetMark(_dMark, ScaleW, Mathf.InverseLerp(D0, D1, _shownD));
        V3.SetMark(_hMark, ScaleW, HX(_shownH));

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
        _statText.color = V3.Ink;
        _statDot.color = ok ? V3.Green : V3.Violet;
        V3.SetGuide(_hold, Confirmed ? 1f : _detect.HoldProgress);

        foreach (var g in _target)
            g.color = ok ? V3.Green : V3.A(V3.Green, 0.7f);
        // the stand-in head follows distance and height when there is no camera picture
        float scale = Mathf.Clamp(40f / Mathf.Max(1f, _shownD), 0.55f, 1.7f);
        _silhouette.rectTransform.localScale = Vector3.one * scale;
        _silhouette.rectTransform.anchoredPosition = new Vector2(150f, -145f - Mathf.Clamp(-_shownH * 6.5f, -85f, 85f));
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
            float aspect = (float)w / h, want = 400f / 300f;
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

    // Face target over the mirror, drawn at its shown size (400 x 300 px; the design in 160 x 120 units, 2.5 px each):
    // a soft dark mask outside an oval, the oval (1.2 px), corner brackets, eye-line and side ticks (1.6 px). White,
    // to tint: green (dimmer until the position is right).
    public static Sprite TargetSprite()
    {
        if (s_target != null)
            return s_target;
        const float unit = 2.5f;
        const int w = 400, h = 300;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 q = new Vector2((x + 0.5f) / unit, 120f - (y + 0.5f) / unit);
                float e = Mathf.Sqrt(Sq((q.x - 80f) / 27f) + Sq((q.y - 56f) / 35f));
                float edge = (e - 1f) * 30f * unit;                 // ~px from the oval
                float oval = Mathf.Clamp01(1.1f - Mathf.Abs(edge));
                float mask = e > 1f ? 0.28f : 0f;
                float lines = 0f;
                foreach (var (ax, ay, bx, by) in Segments)
                    lines = Mathf.Max(lines, Mathf.Clamp01(1.3f - SegDist(q, new Vector2(ax, ay), new Vector2(bx, by)) * unit));
                float white = Mathf.Max(oval * 0.95f, lines);
                // dark mask (black) under the strokes
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

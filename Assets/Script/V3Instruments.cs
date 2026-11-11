using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The inner pages' small instruments, in the home's glass and light: the home dial's scale on an arc, the trial
// frame's lenses on the cover page, the calibration's engraved glass plate and the result's readout.
public static class V3Instruments
{
    private static readonly Dictionary<string, Sprite> s_scales = new Dictionary<string, Sprite>();
    private static Sprite s_smudge;

    // Ticks on an arc, as the home dial's ring, rendered once at twice the size into a square of 2 half px: count + 1
    // ticks hanging outward from radius r, from startDeg (clockwise from the top), stepDeg apart - every major'th long
    // (12 px), every mid'th medium (7 px), the rest short (4 px). band: also a fine rule at r and a faint band out to a
    // second rule at r + 13.
    public static Sprite ArcScale(float half, float r, float startDeg, float stepDeg, int count, int major, int mid, bool band)
    {
        string key = string.Join("/", half, r, startDeg, stepDeg, count, major, mid, band);
        if (s_scales.TryGetValue(key, out var cached))
            return cached;
        float mean = startDeg + count * stepDeg / 2f, reach = count * stepDeg / 2f;
        Color ink = V3.Ink;
        var sprite = V3.Raster(Mathf.CeilToInt(4f * half), p =>
        {
            float x = p.x / 2f - half, y = p.y / 2f - half, d = Mathf.Sqrt(x * x + y * y);
            float deg = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
            float a = 0f;
            int k = Mathf.Clamp(Mathf.RoundToInt((deg - startDeg) / stepDeg), 0, count);
            bool isMajor = k % major == 0, isMid = k % mid == 0;
            float len = isMajor ? 12f : isMid ? 7f : 4f, width = isMajor ? 0.7f : 0.5f;
            float off = Mathf.Abs(d * Mathf.Sin((deg - (startDeg + k * stepDeg)) * Mathf.Deg2Rad));
            float along = Mathf.Min(d - r, r + len - d);
            a = Mathf.Max(a, (isMajor ? 0.9f : isMid ? 0.62f : 0.42f) * Mathf.Clamp01((width - off) * 2f + 0.5f) * Mathf.Clamp01(along * 2f + 0.5f));
            if (band)
            {
                float within = Mathf.Clamp01((reach - Mathf.Abs(deg - mean)) * d * Mathf.Deg2Rad * 2f + 0.5f);
                a = Mathf.Max(a, 0.4f * Mathf.Clamp01((0.28f - Mathf.Abs(d - r)) * 2f + 0.5f) * within);
                a = Mathf.Max(a, 0.22f * Mathf.Clamp01((0.23f - Mathf.Abs(d - r - 13f)) * 2f + 0.5f) * within);
                a = Mathf.Max(a, 0.05f * Mathf.Clamp01(Mathf.Min(d - r, r + 13f - d) * 2f + 0.5f) * within);
            }
            return new Color(ink.r, ink.g, ink.b, a);
        });
        s_scales[key] = sprite;
        return sprite;
    }

    // ==================== the trial frame's lens ====================

    public class Lens
    {
        public Image Eye, Smudge, Palm;
        public Material Glass;
        public bool Covered;
        private float _frost;

        // Each frame: the glass frosts over (or clears) toward Covered - the eye fading behind it, the palm coming on.
        public void Ease()
        {
            _frost = Mathf.MoveTowards(_frost, Covered ? 1f : 0f, V3Motion.Step / V3Motion.Slow);
            float f = _frost * _frost * (3f - 2f * _frost);
            Glass.SetFloat("_Frost", f);
            Eye.color = V3.A(V3.Ink, 1f - f);
            Smudge.color = V3.A(Color.white, 0.22f * f);
            Palm.color = V3.A(V3.Violet, f);
            Palm.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, f);
        }
    }

    // One lens of a trial frame centred on (cx, cy): the frame's axis scale round its top, the eye seen through the clear
    // lens (or only its smudge through the frosted one, the palm on it), the glass, and a glass rim of radius r, width w
    // over its edge.
    public static Lens CreateLens(Transform parent, float cx, float cy, float r, float w, Sprite palm)
    {
        var lens = new Lens();
        float half = r + w / 2f + 16f;
        V3.Sprite("Axis", parent, ArcScale(half, r + w / 2f + 5f, -54f, 4.5f, 24, 6, 3, false), Color.white, cx - half, cy - half, 2f * half, 2f * half);
        lens.Eye = V3.Sprite("Eye", parent, V3.Icon("eye"), V3.Ink, cx - 46f, cy - 46f, 92f, 92f);
        float glass = r - w / 2f + 1f, box = 2f * (glass + 1f);
        var image = V3.Sprite("Lens", parent, UiFactory.White, Color.white, cx - box / 2f, cy - box / 2f, box, box);
        lens.Glass = new Material(Resources.Load<Shader>("V3/Shaders/Lens"));
        lens.Glass.SetFloat("_Size", box);
        lens.Glass.SetFloat("_R", glass);
        image.material = lens.Glass;
        lens.Smudge = V3.Sprite("Smudge", parent, Smudge(), V3.A(Color.white, 0.22f), cx - 48f, cy - 48f, 96f, 96f);
        lens.Palm = V3.Sprite("Palm", parent, palm, V3.Violet, cx - 33f, cy - 33f, 66f, 66f);
        lens.Palm.rectTransform.pivot = UiFactory.Center;
        lens.Palm.rectTransform.anchoredPosition = new Vector2(cx, -cy);
        lens.Ease();
        V3.GlassRim(parent, cx, cy, r, w);
        return lens;
    }

    // The eye behind frosted glass: its shape out of focus.
    private static Sprite Smudge()
    {
        if (s_smudge != null)
            return s_smudge;
        Color ink = V3.Ink;
        return s_smudge = V3.Raster(96, p =>
        {
            float x = p.x / 48f - 1f, y = p.y / 48f - 1f;
            float t = Mathf.Clamp01((x + 0.84f) / 1.68f), lid = 0.5f * Mathf.Sin(Mathf.PI * t);
            float almond = Mathf.Clamp01(0.5f - (Mathf.Abs(y) - lid) / 0.24f) * Mathf.Clamp01((0.84f - Mathf.Abs(x)) / 0.2f);
            float iris = Mathf.Clamp01(0.5f - (Mathf.Sqrt(x * x + y * y) - 0.26f) / 0.26f);
            return new Color(ink.r, ink.g, ink.b, Mathf.Clamp01(0.5f * almond + 0.8f * iris));
        });
    }

    // ==================== the calibration's glass plate ====================

    // The graticule cut in the calibration's glass plate, w x h px, drawn 1:1: each cut a dark line with a bright lip
    // (below a horizontal, right of a vertical) - the two centre lines with fine ticks, a small circle round the centre
    // and corner brackets.
    public static Sprite Reticle(int w, int h)
    {
        string key = "reticle" + w + "/" + h;
        if (s_scales.TryGetValue(key, out var cached))
            return cached;
        var px = new Color32[w * h];
        Color32 dark = V3.ShadowInk, light = Color.white;
        void Put(int x, int y, Color32 c, float a)
        {
            if (x < 0 || x >= w || y < 0 || y >= h)
                return;
            int i = (h - 1 - y) * w + x;
            if (a * 255f > px[i].a)
                px[i] = new Color32(c.r, c.g, c.b, (byte)(a * 255f));
        }
        void Horizontal(int x0, int x1, int y, float a)
        {
            for (int x = x0; x < x1; x++)
            {
                Put(x, y + 1, light, 0.75f);
                Put(x, y, dark, a);
            }
        }
        void Vertical(int x, int y0, int y1, float a)
        {
            for (int y = y0; y < y1; y++)
            {
                Put(x + 1, y, light, 0.6f);
                Put(x, y, dark, a);
            }
        }
        int cx = w / 2, cy = h / 2;
        const int inset = 16, arm = 12;
        Horizontal(inset, w - inset, cy, 0.36f);
        Vertical(cx, inset, h - inset, 0.36f);
        for (int k = 8; k < w / 2 - inset; k += 8)
            foreach (int x in new[] { cx - k, cx + k })
            {
                int t = k % 40 == 0 ? 6 : 3;
                Vertical(x, cy - t, cy + t + 1, 0.32f);
            }
        for (int k = 8; k < h / 2 - inset; k += 8)
            foreach (int y in new[] { cy - k, cy + k })
            {
                int t = k % 40 == 0 ? 6 : 3;
                Horizontal(cx - t, cx + t + 1, y, 0.32f);
            }
        for (int y = cy - 18; y <= cy + 18; y++)
            for (int x = cx - 18; x <= cx + 18; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float lip = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy - 1f) * (y - cy - 1f));
                Put(x, y, light, 0.7f * Mathf.Clamp01(0.8f - Mathf.Abs(lip - 14f)));
                Put(x, y, dark, 0.34f * Mathf.Clamp01(0.8f - Mathf.Abs(d - 14f)));
            }
        foreach (var (x, dx) in new[] { (inset, 1), (w - inset - 1, -1) })
            foreach (var (y, dy) in new[] { (inset, 1), (h - inset - 1, -1) })
            {
                Horizontal(Mathf.Min(x, x + dx * arm), Mathf.Max(x, x + dx * arm) + 1, y, 0.42f);
                Vertical(x, Mathf.Min(y, y + dy * arm), Mathf.Max(y, y + dy * arm) + 1, 0.42f);
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        s_scales[key] = sprite;
        return sprite;
    }

}

// The result's readout: a small dial, the home's scale bent round the value. The eleven levels (0.1 to 2.0) sit
// along a 270 degree arc, the gap at the bottom holding which eye and the level; the green liquid's arc runs round to
// the reading as the page comes in, the value in the middle counting up the levels with it, and stands there large.
public class V3Readout : MonoBehaviour
{
    private const float Box = 320f, C = Box / 2f;
    private const float StartDeg = -135f, SweepDeg = 270f;
    private const int Levels = 11;
    private const float ArcSeconds = 0.9f;

    private V3LiquidRing _arc;
    private TextMeshProUGUI _value;
    private int _level, _shown;
    private float _target, _t;

    private static float Deg(int level) => StartDeg + (level - 1) * SweepDeg / (Levels - 1);

    // in the box's top-left coordinates
    private static Vector2 Polar(float r, float deg) => new Vector2(C + r * Mathf.Sin(deg * Mathf.Deg2Rad), C - r * Mathf.Cos(deg * Mathf.Deg2Rad));

    private static string Value(int level) => MetricTest.GetEyeMetric(level).ToString("0.0", CultureInfo.InvariantCulture);

    public static V3Readout Create(Transform parent, float cx, float cy, string label, int level)
    {
        level = Mathf.Clamp(level, 1, Levels);
        var root = V3.Rect("Readout", parent, cx - C, cy - C, Box, Box);
        var readout = root.gameObject.AddComponent<V3Readout>();
        V3.Sprite("Scale", root, V3Instruments.ArcScale(C, 110f, StartDeg, SweepDeg / 120f, 120, 12, 6, true), Color.white, 0f, 0f, Box, Box);
        float span = SweepDeg / 360f;
        readout._arc = V3LiquidRing.Gauge(root, 260f, 103f, 3.5f, V3.A(V3.Groove, 0.18f), span, StartDeg);
        readout._level = level;
        readout._target = Mathf.Max(0.006f, span * (level - 1f) / (Levels - 1f));
        for (int i = 1; i <= Levels; i++)
        {
            var p = Polar(136f, Deg(i));
            V3.Text("Mark", root, V3.Mono, Value(i), V3Type.Data, V3.A(V3.Ink, i == level ? 1f : 0.55f), p.x - 20f, p.y - 8f, 40f, 16f, TextAlignmentOptions.Center);
        }
        // the reading's own tick, lit
        var index = V3.Box("Index", root, V3.GreenLit, 0f, 0f, 2f, 14f).rectTransform;
        var at = Polar(116f, Deg(level));
        UiFactory.Place(index, new Vector2(0f, 1f), UiFactory.Center, new Vector2(at.x, -at.y), new Vector2(2f, 14f));
        index.localEulerAngles = new Vector3(0f, 0f, -Deg(level));
        readout._value = V3.Text("Value", root, V3.Num, Value(1), V3Type.Big, V3.Ink, C - 88f, C - 66f, 176f, 126f, TextAlignmentOptions.Center, -0.02f);
        readout._shown = 1;
        V3.Text("Eye", root, V3.Zh, label, V3Type.Key, V3.Ink, C - 80f, C + 104f, 160f, 30f, TextAlignmentOptions.Center);
        return readout;
    }

    private void Update()
    {
        if (_t >= 1f)
            return;
        _t = Mathf.Min(1f, _t + V3Motion.Step / ArcSeconds);
        float k = V3Motion.EaseOut(_t);
        _arc.Fill = _target * k;
        // the value counts up the levels as the liquid passes them
        int level = _t >= 1f ? _level : 1 + Mathf.FloorToInt(k * (_level - 1));
        if (level != _shown)
        {
            _shown = level;
            _value.text = Value(level);
        }
    }
}

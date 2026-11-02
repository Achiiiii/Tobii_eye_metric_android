using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// V3.1 look shared by every flow page: the page is an eye-chart light box, cards and keys are backlit windows in it
// (radius 10, a soft contact shadow), keys carry their icon first and a violet light guide that fills while they are
// looked at, Kebbi's lines are violet with a small voice wave, Barlow Condensed for big numbers, mono for small data. Everything is laid out in the 1024 x 600 reference space, origin top-left.
public static class V3
{
    public const float W = 1024f, H = 600f;

    public static readonly Color Ink = new Color32(0x14, 0x14, 0x14, 0xFF);
    public static readonly Color Page = new Color32(0xF7, 0xF7, 0xF5, 0xFF);
    // The flow pages sit on a cool light grey with a fine grain; the test and
    // its lead-in keep the home's Page colour. Shadows are a cool blue-grey.
    public static readonly Color Cool = new Color32(0xF3, 0xF4, 0xF6, 0xFF);
    public static readonly Color ShadowInk = new Color32(0x18, 0x20, 0x30, 0xFF);
    // the one green (progress, done, action): the violet's saturation and brightness, the hue a cool technical green
    public static readonly Color Green = new Color32(0x56, 0xCF, 0x9F, 0xFF);
    public static readonly Color Violet = new Color32(0x6E, 0x56, 0xCF, 0xFF);
    public static readonly Color Text2 = new Color32(0x5A, 0x5F, 0x63, 0xFF);
    public static readonly Color Red = new Color32(0xE5, 0x48, 0x4D, 0xFF);

    public static Color A(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

    // ==================== language ====================

    // 0 = 中文, 1 = English. Picked on the home; every page reads it when it shows.
    public static int Lang { get; private set; }
    public static event Action LangChanged;

    public static void SetLang(int lang)
    {
        if (lang == Lang)
            return;
        Lang = lang;
        LangChanged?.Invoke();
    }

    public static string T(string zh, string en) => Lang == 1 ? en : zh;

    // ==================== fonts ====================

    private static TMP_FontAsset s_zh, s_zhMedium, s_mono, s_num, s_headline;

    // Noto Sans TC Bold: every Chinese text, and English body text.
    public static TMP_FontAsset Zh => s_zh != null ? s_zh : (s_zh = LoadFont("NotoSansTC-Bold", 0.12f));
    // Archivo Condensed Black (Archivo at width 75, weight 900, made static): the home headline.
    public static TMP_FontAsset Headline => s_headline != null ? s_headline : (s_headline = LoadFont("ArchivoCondensed-Black", 0f));
    // Barlow Condensed ExtraBold: big numbers (vision values, percentages).
    public static TMP_FontAsset Num => s_num != null ? s_num : (s_num = LoadFont("BarlowCondensed-ExtraBold", 0f));
    public static TMP_FontAsset ZhMedium => s_zhMedium != null ? s_zhMedium : (s_zhMedium = LoadFont("NotoSansTC-Medium", 0.08f));
    // IBM Plex Mono Medium: small data labels and codes.
    // (no CJK in Plex Mono: Chinese in a mono label falls back to Noto Sans TC)
    public static TMP_FontAsset Mono
    {
        get
        {
            if (s_mono != null)
                return s_mono;
            s_mono = LoadFont("IBMPlexMono-Medium", 0f);
            s_mono.fallbackFontAssetTable = new List<TMP_FontAsset> { Zh };
            return s_mono;
        }
    }

    // Dynamic SDF assets made at runtime from the TTFs in Resources/V3/Fonts.
    private static TMP_FontAsset LoadFont(string name, float dilate)
    {
        var font = Resources.Load<Font>("V3/Fonts/" + name);
        if (font == null)
        {
            Debug.LogError("[V3] missing font V3/Fonts/" + name);
            return TMP_Settings.defaultFontAsset;
        }
        var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = name + " (V3)";
        // TMP draws Noto a little light at this weight; a small dilate evens it out.
        asset.material.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
        return asset;
    }

    // ==================== layout ====================

    public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        return At(UiFactory.CreateRect(name, parent), x, y, w, h);
    }

    // Top-left placement inside the parent (y down).
    public static RectTransform At(RectTransform rect, float x, float y, float w, float h)
    {
        return UiFactory.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
    }

    public static RectTransform Full(string name, Transform parent)
    {
        return UiFactory.Stretch(UiFactory.CreateRect(name, parent));
    }

    public static Image Box(string name, Transform parent, Color color, float x, float y, float w, float h, bool raycast = false)
    {
        var image = UiFactory.CreateImage(name, parent, UiFactory.White, color, raycast);
        At(image.rectTransform, x, y, w, h);
        return image;
    }

    public static Image Sprite(string name, Transform parent, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        var image = UiFactory.CreateImage(name, parent, sprite, color);
        At(image.rectTransform, x, y, w, h);
        return image;
    }

    public static RawImage Picture(string name, Transform parent, string resource, float x, float y, float w, float h, Color? tint = null)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(parent, false);
        image.texture = Resources.Load<Texture2D>(resource);
        image.color = tint ?? Color.white;
        image.raycastTarget = false;
        At(image.rectTransform, x, y, w, h);
        return image;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        float x, float y, float w, float h, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float spacingEm = 0f)
    {
        var label = UiFactory.CreateText(name, parent, font, text, size, color, align);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.characterSpacing = spacingEm * 100f;
        label.lineSpacing = 0f;
        At(label.rectTransform, x, y, w, h);
        return label;
    }

    // A text whose words follow the language toggle.
    public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string zh, string en, float size, Color color,
        float x, float y, float w, float h, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float spacingEm = 0f)
    {
        var label = Text(name, parent, font, T(zh, en), size, color, x, y, w, h, align, spacingEm);
        label.gameObject.AddComponent<V3Loc>().Set(zh, en, spacingEm * 100f, Mathf.Min(spacingEm, 0.02f) * 100f);
        return label;
    }

    // Hairline rectangle (four 1 px lines) inside the given rect.
    public static void Hairline(Transform parent, Color color, float x, float y, float w, float h, float px = 1f)
    {
        Box("LineT", parent, color, x, y, w, px);
        Box("LineB", parent, color, x, y + h - px, w, px);
        Box("LineL", parent, color, x, y, px, h);
        Box("LineR", parent, color, x + w - px, y, px, h);
    }

    // ==================== components ====================

    public class KeyParts
    {
        public Button Button;
        public RectTransform Root;
        public TextMeshProUGUI Label;
        public Image Chip, Glyph;
        public CanvasGroup Group;
    }

    // Key: a backlit window, the icon first, then the label; a glass tube along the bottom edge fills while the key is
    // looked at (V3KeyDwell reads the ButtonTrigger). Secondary keys (back) are lower.
    public static KeyParts Key(Transform parent, string zh, string en, string icon, Color chip, float x, float y, float w = 224f, bool secondary = false, Action onClick = null)
    {
        float h = secondary ? 56f : 72f;
        var k = new KeyParts();
        // the key and its shadow move and hide together: both live in one container (Root)
        k.Root = Rect("Key", parent, x, y, w, h);
        k.Group = k.Root.gameObject.AddComponent<CanvasGroup>();
        PatchShadow(k.Root, 0f, 0f, w, h);
        k.Button = UiFactory.CreateButton("Face", k.Root, PatchSprite, Color.white, onClick != null ? new UnityEngine.Events.UnityAction(onClick) : null);
        k.Button.transition = Selectable.Transition.None;
        ((Image)k.Button.targetGraphic).type = Image.Type.Sliced;
        var face = At((RectTransform)k.Button.transform, 0f, 0f, w, h);
        float g = secondary ? 20f : 24f;
        k.Glyph = Sprite("Glyph", face, Icon(icon), Ink, 20f, (h - g) / 2f - 2f, g, g);
        k.Chip = k.Glyph;
        var tube = Tube(face, 16f, h - 15f, w - 32f, 9f);
        float lx = 20f + g + 14f;
        k.Label = Text("Label", face, Zh, zh, en, secondary ? 20f : 24f, Ink, lx, 0f, w - lx - 14f, h - 4f, TextAlignmentOptions.Left);
        // a long label (English) shrinks rather than running past the frame
        k.Label.enableAutoSizing = true;
        k.Label.fontSizeMin = 14f;
        k.Label.fontSizeMax = secondary ? 20f : 24f;
        k.Button.gameObject.AddComponent<V3PressTint>().Target = (Image)k.Button.targetGraphic;
        k.Button.gameObject.AddComponent<V3KeyDwell>().Init(tube, face, new Vector2(16f, h - 15f), w - 32f);
        return k;
    }

    public static void SetKeyEnabled(KeyParts k, bool on)
    {
        k.Button.interactable = on;
        k.Group.alpha = on ? 1f : 0.35f;
    }

    public class LineParts
    {
        public RectTransform Root;
        public TextMeshProUGUI Text;
        public Image Rule;
    }

    // Kebbi's line: a violet light (Kebbi) with a small voice wave, then the words in violet.
    public static LineParts KebbiLine(Transform parent, float x, float y, float textSize, float textWidth)
    {
        var l = new LineParts();
        l.Root = Rect("KebbiLine", parent, x, y, 102f + textWidth, 120f);
        var mark = Rect("Kebbi", l.Root, 0f, 0f, 80f, 24f);
        Sprite("Halo", mark, UiFactory.Circle, A(Violet, 0.22f), 0f, 0f, 24f, 24f);
        Sprite("Light", mark, UiFactory.Circle, Violet, 5f, 5f, 14f, 14f);
        var bars = new RectTransform[7];
        for (int i = 0; i < bars.Length; i++)
            bars[i] = Box("Wave", mark, Violet, 36f + i * 6f, 10f, 3f, 4f).rectTransform;
        mark.gameObject.AddComponent<V3Wave>().Bars = bars;
        l.Rule = Box("Rule", l.Root, new Color(1f, 1f, 1f, 0f), 0f, 0f, 1f, 1f);
        l.Text = Text("Words", l.Root, Zh, "", textSize, Violet, 102f, 0f, textWidth, 96f, TextAlignmentOptions.TopLeft);
        l.Text.enableWordWrapping = true;
        l.Text.lineSpacing = -8f;
        return l;
    }

    // Fit the block height to the words; keeps Kebbi's light centred on them.
    public static void SetLine(LineParts l, string words)
    {
        l.Text.text = words;
        l.Text.ForceMeshUpdate();
        float h = Mathf.Max(24f, l.Text.preferredHeight + 2f);
        l.Root.sizeDelta = new Vector2(l.Root.sizeDelta.x, h);
        l.Text.rectTransform.sizeDelta = new Vector2(l.Text.rectTransform.sizeDelta.x, h);
        var kebbi = (RectTransform)l.Root.Find("Kebbi");
        kebbi.anchoredPosition = new Vector2(0f, -(h - 24f) / 2f);
    }

    // ==================== light box ====================

    // The page as an eye-chart light box: an evenly lit white panel (a touch brighter at the top) in a 9 px brushed
    // aluminium frame.
    public static Image LightBox(Transform parent, bool raycast = false)
    {
        var bg = UiFactory.CreateImage("LightBox", parent, LightBoxSprite, Color.white, raycast);
        UiFactory.Stretch(bg.rectTransform);
        var frame = new Color32(0xC9, 0xCE, 0xD6, 0xFF);
        Box("FrameT", bg.transform, frame, 0f, 0f, W, 9f);
        Box("FrameB", bg.transform, frame, 0f, H - 9f, W, 9f);
        Box("FrameL", bg.transform, frame, 0f, 0f, 9f, H);
        Box("FrameR", bg.transform, frame, W - 9f, 0f, 9f, H);
        Hairline(bg.transform, new Color32(0xFF, 0xFF, 0xFF, 0xE6), 9f, 9f, W - 18f, H - 18f);
        return bg;
    }

    // A backlit window in the light box (cards, answers) with its contact shadow.
    public static Image Patch(Transform parent, float x, float y, float w, float h)
    {
        PatchShadow(parent, x, y, w, h);
        var image = Sprite("Patch", parent, PatchSprite, Color.white, x, y, w, h);
        image.type = Image.Type.Sliced;
        return image;
    }

    // The window's contact shadow: 0 10 18 -10 and a tight 0 2 2.
    public static void PatchShadow(Transform parent, float x, float y, float w, float h)
    {
        Shadow(parent, x, y, w, h, 18f, -10f, 10f, A(ShadowInk, 0.18f));
        Shadow(parent, x, y, w, h, 2f, 0f, 2f, A(ShadowInk, 0.14f));
    }

    private static Sprite s_lightBox, s_patch, s_rim;

    // #FAFBFD at the top to #ECEFF3 at the bottom.
    public static Sprite LightBoxSprite
    {
        get
        {
            if (s_lightBox != null)
                return s_lightBox;
            Color top = new Color32(0xFA, 0xFB, 0xFD, 0xFF), bottom = new Color32(0xEC, 0xEF, 0xF3, 0xFF);
            return s_lightBox = Raster(64, p => Color.Lerp(bottom, top, p.y / 64f));
        }
    }

    // The chosen rim round a window: a 2.5 px outline, radius 14 (the window's 10 plus the 4 px gap), 9-sliced at 20.
    public static Sprite RimSprite
    {
        get
        {
            if (s_rim != null)
                return s_rim;
            const int s = 64;
            const float r = 14f;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var q = new Vector2(Mathf.Abs(x + 0.5f - s / 2f) - (s / 2f - r), Mathf.Abs(y + 0.5f - s / 2f) - (s / 2f - r));
                    float d = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;   // < 0 inside
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1.75f - Mathf.Abs(d + 1.25f)));
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return s_rim = UnityEngine.Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
        }
    }

    // 64 px, 9-sliced at 20: radius 10, a cool white face, a thin LED line along the top, a little shade inside the
    // bottom edge, a recessed rim (grey edge, bright lip, cool groove).
    public static Sprite PatchSprite
    {
        get
        {
            if (s_patch != null)
                return s_patch;
            const int s = 64;
            const float r = 10f;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[s * s];
            Color face = new Color32(0xFB, 0xFC, 0xFE, 0xFF), edge = new Color32(0xD3, 0xD8, 0xE0, 0xFF);
            Color groove = new Color32(0x96, 0xA5, 0xBE, 0xFF), shade = new Color32(0xB9, 0xC8, 0xDE, 0xFF);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var q = new Vector2(Mathf.Abs(p.x - s / 2f) - (s / 2f - r), Mathf.Abs(p.y - s / 2f) - (s / 2f - r));
                    float inside = -(Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r);
                    Color c = Color.Lerp(face, Color.white, 0.95f * Mathf.Clamp01(1f - (s - p.y) / 7f));
                    c = Color.Lerp(c, shade, 0.4f * Mathf.Clamp01(1f - p.y / 10f));
                    if (inside < 1f) c = edge;
                    else if (inside < 2f) c = Color.white;
                    else if (inside < 3.2f) c = Color.Lerp(c, groove, 0.34f);
                    c.a = Mathf.Clamp01(0.5f + inside);
                    px[y * s + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return s_patch = UnityEngine.Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
        }
    }

    // ==================== light guides, scales, metal ====================

    public class GuideParts
    {
        public RectTransform Root;
        public V3Slosh Tube;
        public float W, F;
    }

    // A horizontal progress guide (24 px tall box): a 12 px glass tube with the green liquid in it (V3/Tube), sparks
    // off the liquid's front while it is on the way, ticks under it every 12 px (longer every 120) when asked.
    public static GuideParts Guide(Transform parent, float x, float y, float w, bool ticks = false)
    {
        var g = new GuideParts { W = w };
        g.Root = Rect("Guide", parent, x, y, w, 24f);
        g.Tube = Tube(g.Root, 0f, 0f, w, 12f);
        if (ticks)
            for (float t = 0f; t <= w; t += 12f)
                Box("Tick", g.Root, A(Ink, t % 120f == 0f ? 0.6f : 0.3f), t, 16f, 1f, t % 120f == 0f ? 7f : 3f);
        V3Sparks.Attach(g.Root, () => g.F > 0.002f && g.F < 0.999f ? (new Vector2(g.W * g.F, 6f), Vector2.right) : ((Vector2, Vector2)?)null);
        SetGuide(g, 0f);
        return g;
    }

    public static void SetGuide(GuideParts g, float f)
    {
        g.F = Mathf.Clamp01(f);
        g.Tube.Fill = g.F;
    }

    // A glass tube w x h with green liquid in it (V3/Tube, its own material); set how full through the V3Slosh.
    public static V3Slosh Tube(Transform parent, float x, float y, float w, float h)
    {
        var image = Sprite("Tube", parent, UiFactory.White, Green, x, y, w, h);
        var material = new Material(Resources.Load<Shader>("V3/Shaders/Tube"));
        material.SetVector("_Size", new Vector4(w, h, 0f, 0f));
        image.material = material;
        return image.gameObject.AddComponent<V3Slosh>().Init(material);
    }

    // A metal scale: a guide groove with ticks, the right zone lit violet (from - to, 0-1), a black needle pointer
    // over it and mono labels spread under it. Returns the pointer (move it with SetMark).
    public static RectTransform Scale(Transform parent, float x, float y, float w, float zoneFrom, float zoneTo, string[] labels)
    {
        var g = Guide(parent, x, y, w, true);
        Box("Zone", g.Root, A(Violet, 0.75f), zoneFrom * w, 2f, (zoneTo - zoneFrom) * w, 8f);
        for (int i = 0; i < labels.Length; i++)
        {
            if (string.IsNullOrEmpty(labels[i]))
                continue;
            float lx = labels.Length == 1 ? 0f : i * w / (labels.Length - 1);
            var align = i == 0 ? TextAlignmentOptions.TopLeft : i == labels.Length - 1 ? TextAlignmentOptions.TopRight : TextAlignmentOptions.Top;
            float left = i == 0 ? lx : i == labels.Length - 1 ? lx - 120f : lx - 60f;
            Text("Label", g.Root, Mono, labels[i], 11f, A(Ink, 0.6f), left, 30f, 120f, 14f, align, 0.06f);
        }
        // the pointer: a black triangle with a needle through the groove
        var mark = Rect("Mark", g.Root, 0f, -18f, 18f, 30f);
        Sprite("Tri", mark, TriangleDown, Ink, 0f, 0f, 18f, 15f);
        Box("Needle", mark, Ink, 8f, 12f, 2f, 20f);
        return mark;
    }

    // marker position along a scale of width w (0-1)
    public static void SetMark(RectTransform mark, float w, float f) => mark.anchoredPosition = new Vector2(Mathf.Clamp01(f) * w - 9f, 18f);

    private static Sprite s_triDown;
    private static readonly Dictionary<string, Sprite> s_metal = new Dictionary<string, Sprite>();

    // 14 x 12 triangle, tip down.
    public static Sprite TriangleDown => s_triDown != null ? s_triDown : (s_triDown = Raster(56, p =>
    {
        float x = p.x / 4f - 7f, y = 14f - p.y / 4f;   // y down from the top of a 14-unit square, 12 tall
        float half = 6f * (1f - Mathf.Clamp01((y - 1f) / 11f));
        return new Color(1f, 1f, 1f, y >= 1f && y <= 12f ? Mathf.Clamp01((half - Mathf.Abs(x)) * 4f + 0.5f) : 0f);
    }));

    // A machined steel bezel of radius r and width w centred on (cx, cy): the home's 3D ring (V3Bezel, rendered once)
    // over a soft contact shadow.
    public static RawImage MetalRing(Transform parent, float cx, float cy, float r, float w)
    {
        float R = r + w / 2f + 12f;
        Sprite("BezelShadow", parent, RingShadowSprite(r, w), Color.white, cx - R, cy - R, 2f * R, 2f * R);
        float half = r + w / 2f + 4f;
        var ring = new GameObject("Bezel", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        ring.transform.SetParent(parent, false);
        At(ring.rectTransform, cx - half, cy - half, 2f * half, 2f * half);
        ring.raycastTarget = false;
        ring.texture = V3Bezel.Baked(r, w);
        return ring;
    }

    // The bezel's contact shadows: a tight one 2.5 px down, a wide one 5 px down.
    public static Sprite RingShadowSprite(float r, float w)
    {
        string key = r + "/" + w;
        if (s_metal.TryGetValue(key, out var cached))
            return cached;
        float R = r + w / 2f + 12f;
        const int k = 2;
        int size = Mathf.CeilToInt(2f * R * k);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[size * size];
        for (int py = 0; py < size; py++)
            for (int pxi = 0; pxi < size; pxi++)
            {
                float x = (pxi + 0.5f) / k - R, y = R - (py + 0.5f) / k;   // y down from the centre
                float s1 = Mathf.Abs(Mathf.Sqrt((x - 1f) * (x - 1f) + (y - 2.5f) * (y - 2.5f)) - r) - w / 2f;
                float s2 = Mathf.Abs(Mathf.Sqrt((x - 1f) * (x - 1f) + (y - 5f) * (y - 5f)) - r) - w / 2f;
                px[py * size + pxi] = new Color(30 / 255f, 40 / 255f, 60 / 255f, Mathf.Clamp01(0.4f * Falloff(s1, 1.6f) + 0.2f * Falloff(s2, 5f)));
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        s_metal[key] = sprite;
        return sprite;
    }

    private static float Falloff(float b, float s) => b <= 0f ? 1f : Mathf.Exp(-b * b / (2f * s * s));

    // A small mono note (page labels like "Q2 / 7  ·  眼科病史").
    public static TextMeshProUGUI Note(Transform parent, float x, float y, float w = 600f, string text = "")
    {
        return Text("Note", parent, Mono, text, 12f, Ink, x, y, w, 16f, TextAlignmentOptions.TopLeft, 0.04f);
    }

    // A page title: Noto Sans TC, wrapping, shrinking to fit.
    public static TextMeshProUGUI Title(Transform parent, float x, float y, float w, float h, float size)
    {
        var t = Text("Title", parent, Zh, "", size, Ink, x, y, w, h, TextAlignmentOptions.TopLeft, 0.03f);
        t.enableWordWrapping = true;
        t.enableAutoSizing = true;
        t.fontSizeMin = Mathf.Round(size * 0.7f);
        t.fontSizeMax = size;
        t.lineSpacing = -14f;
        return t;
    }

    // ==================== liquid ====================

    private static readonly Dictionary<string, Material> s_liquid = new Dictionary<string, Material>();

    // The lit part of a progress guide as flowing liquid (V3/Liquid). Straight guides flow along their x; round ones
    // (radial) along the arc of an image size px square, dir +1 / -1 the way it fills.
    public static Image Liquid(Image image, bool radial = false, float dir = 1f, float size = 100f)
    {
        string key = radial + "/" + dir + "/" + size;
        if (!s_liquid.TryGetValue(key, out var material) || material == null)
        {
            material = new Material(Resources.Load<Shader>("V3/Shaders/Liquid"));
            material.SetFloat("_Radial", radial ? 1f : 0f);
            material.SetFloat("_Dir", dir);
            material.SetFloat("_Size", size);
            s_liquid[key] = material;
        }
        image.material = material;
        return image;
    }

    // ==================== material ====================

    // A soft drop shadow behind the rect (x, y, w, h), as CSS box-shadow: 0 offsetY blur spread colour.
    public static Image Shadow(Transform parent, float x, float y, float w, float h, float blur, float spread, float offsetY, Color color)
    {
        var image = UiFactory.CreateImage("Shadow", parent, ShadowSprite, color);
        image.type = Image.Type.Sliced;
        // the sprite's soft edge is 24 px wide: scale it to the blur
        image.pixelsPerUnitMultiplier = 24f / Mathf.Max(1f, blur);
        // the shadow's rect: grown by the spread, then by the blur on every side, moved down by the offset
        float l = x - spread - blur, t = y + offsetY - spread - blur;
        float ww = w + 2f * spread + 2f * blur, hh = h + 2f * spread + 2f * blur;
        At(image.rectTransform, l, t, Mathf.Max(2f * blur, ww), Mathf.Max(2f * blur, hh));
        return image;
    }

    private static Sprite s_shadow;

    // 64 px, a 16 px solid core with a 24 px soft falloff on every side (9-sliced).
    public static Sprite ShadowSprite
    {
        get
        {
            if (s_shadow != null)
                return s_shadow;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(24f - (x + 0.5f), (x + 0.5f) - 40f));
                    float dy = Mathf.Max(0f, Mathf.Max(24f - (y + 0.5f), (y + 0.5f) - 40f));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 24f;
                    px[y * size + x] = new Color(1f, 1f, 1f, d >= 1f ? 0f : Mathf.Exp(-3.2f * d * d) * (1f - d));
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return s_shadow = UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
        }
    }

    // ==================== sprites ====================

    private static readonly Dictionary<string, Sprite> s_icons = new Dictionary<string, Sprite>();
    private static Sprite s_ring;

    public static Sprite RingSprite => s_ring != null ? s_ring : (s_ring = Raster(64, p =>
    {
        float d = Mathf.Abs((p - new Vector2(32f, 32f)).magnitude - 27f) - 4f;
        return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
    }));

    private static Color Over(Color under, Color over, float a)
    {
        float outA = a + under.a * (1f - a);
        if (outA <= 0f)
            return new Color(over.r, over.g, over.b, 0f);
        Color c = (over * a + under * under.a * (1f - a)) / outA;
        c.a = outA;
        return c;
    }

    // Line icons (24 / 32 unit boxes), rasterised as white strokes to tint.
    public static Sprite Icon(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (s_icons.TryGetValue(name, out var cached))
            return cached;
        // polylines in a 32-unit box (y down), stroke width in units
        float[][] lines;
        float stroke = 2.6f;
        Vector3[] dots = null;      // x, y, radius (filled)
        Vector3[] rings = null;     // x, y, radius (stroked)
        switch (name)
        {
            case "arrowR": lines = new[] { new float[] { 7, 16, 24, 16 }, new float[] { 17, 9, 24, 16, 17, 23 } }; break;
            case "arrowL": lines = new[] { new float[] { 25, 16, 8, 16 }, new float[] { 15, 9, 8, 16, 15, 23 } }; break;
            case "arrowU": lines = new[] { new float[] { 16, 25, 16, 8 }, new float[] { 9, 15, 16, 8, 23, 15 } }; break;
            case "arrowD": lines = new[] { new float[] { 16, 7, 16, 24 }, new float[] { 9, 17, 16, 24, 23, 17 } }; break;
            case "home": lines = new[] { new float[] { 6, 15, 16, 6, 26, 15 }, new float[] { 9, 13, 9, 26, 23, 26, 23, 13 } }; break;
            case "check": lines = new[] { new float[] { 7, 16.5f, 13, 22.5f, 25, 10 } }; stroke = 3f; break;
            case "list": lines = new[] { new float[] { 12, 8, 27, 8 }, new float[] { 12, 16, 27, 16 }, new float[] { 12, 24, 27, 24 } }; stroke = 2.4f;
                dots = new[] { new Vector3(6, 8, 2f), new Vector3(6, 16, 2f), new Vector3(6, 24, 2f) }; break;
            case "person": lines = new[] { new float[] { 5, 28, 7, 24, 11, 21, 16, 20, 21, 21, 25, 24, 27, 28 } }; stroke = 2.4f;
                rings = new[] { new Vector3(16, 11, 5.3f) }; break;
            case "target": lines = new[] { new float[] { 16, 3, 16, 9 }, new float[] { 16, 23, 16, 29 }, new float[] { 3, 16, 9, 16 }, new float[] { 23, 16, 29, 16 } }; stroke = 2.4f;
                rings = new[] { new Vector3(16, 16, 8f) }; break;
            case "exit": lines = new[] { new float[] { 13, 5, 5, 5, 5, 27, 13, 27 }, new float[] { 19, 11, 24, 16, 19, 21 }, new float[] { 24, 16, 12, 16 } }; stroke = 2.9f; break;
            case "eye": lines = EyeOutline(16f, 16f, 13.5f, 8.5f); rings = new[] { new Vector3(16, 16, 4f) }; stroke = 2.4f; break;
            default: lines = new float[0][]; break;
        }
        const int size = 96;
        const float k = size / 32f;
        var sprite = Raster(size, p =>
        {
            Vector2 u = new Vector2(p.x / k, 32f - p.y / k);
            float d = float.MaxValue;
            foreach (var line in lines)
                for (int i = 0; i + 3 < line.Length; i += 2)
                    d = Mathf.Min(d, Segment(u, new Vector2(line[i], line[i + 1]), new Vector2(line[i + 2], line[i + 3])) - stroke / 2f);
            if (dots != null)
                foreach (var c in dots)
                    d = Mathf.Min(d, (u - new Vector2(c.x, c.y)).magnitude - c.z);
            if (rings != null)
                foreach (var c in rings)
                    d = Mathf.Min(d, Mathf.Abs((u - new Vector2(c.x, c.y)).magnitude - c.z) - stroke / 2f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * k));
        });
        s_icons[name] = sprite;
        return sprite;
    }

    // Almond eye outline as two polylines (upper and lower lids).
    private static float[][] EyeOutline(float cx, float cy, float rx, float ry)
    {
        var up = new List<float>();
        var down = new List<float>();
        for (int i = 0; i <= 16; i++)
        {
            float t = i / 16f, x = cx - rx + 2f * rx * t, y = ry * Mathf.Sin(Mathf.PI * t);
            up.Add(x); up.Add(cy - y);
            down.Add(x); down.Add(cy + y);
        }
        return new[] { up.ToArray(), down.ToArray() };
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    public static Sprite Raster(int size, Func<Vector2, Color> shade)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = shade(new Vector2(x + 0.5f, y + 0.5f));
        tex.SetPixels(px);
        tex.Apply(true, true);
        return UnityEngine.Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    public static void Speak(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        Nuwa.stopTTS();
        Nuwa.startTTS(text);
    }
}

// The glass tube along a key's bottom edge: how far the gaze dwell on it has run.
public class V3KeyDwell : MonoBehaviour
{
    private V3Slosh _tube;
    private float _f;
    private ButtonTrigger _trigger;

    // the tube at (x, y) in the key's face, w wide
    public void Init(V3Slosh tube, RectTransform face, Vector2 at, float width)
    {
        _tube = tube;
        V3Sparks.Attach(face, () => _f > 0.01f && _f < 1f ? (new Vector2(at.x + width * _f, at.y + 4.5f), Vector2.right) : ((Vector2, Vector2)?)null);
    }

    private void LateUpdate()
    {
        if (_trigger == null)
            _trigger = GetComponent<ButtonTrigger>();
        _f = _trigger != null ? Mathf.Clamp01(_trigger.Progress) : 0f;
        _tube.Fill = _f;
    }
}

// Sparks off the head of a progress guide: small green lights shed behind and to the sides of the moving head,
// slowing and fading out over about .7 s (a few white-hot ones among them). Tip gives the head's position in the
// parent's top-left space (y down) and its forward direction, or null while there is nothing to show. The sparks
// live on their own canvas so they do not rebuild the page every frame.
public class V3Sparks : MonoBehaviour
{
    private const int Pool = 28;
    private Func<(Vector2 pos, Vector2 dir)?> _tip;
    private float _rate, _due;
    private readonly RectTransform[] _dots = new RectTransform[Pool];
    private readonly Image[] _images = new Image[Pool];
    private readonly Vector2[] _pos = new Vector2[Pool], _vel = new Vector2[Pool];
    private readonly float[] _age = new float[Pool], _life = new float[Pool], _size = new float[Pool];
    private readonly bool[] _hot = new bool[Pool];

    public static V3Sparks Attach(RectTransform parent, Func<(Vector2 pos, Vector2 dir)?> tip, float rate = 40f)
    {
        var layer = V3.Rect("Sparks", parent, 0f, 0f, 0f, 0f);
        layer.gameObject.AddComponent<Canvas>();
        var sparks = layer.gameObject.AddComponent<V3Sparks>();
        sparks._tip = tip;
        sparks._rate = rate;
        for (int i = 0; i < Pool; i++)
        {
            var image = V3.Sprite("Spark", layer, UiFactory.Circle, V3.Green, 0f, 0f, 4f, 4f);
            image.raycastTarget = false;
            image.rectTransform.pivot = UiFactory.Center;
            image.enabled = false;
            sparks._dots[i] = image.rectTransform;
            sparks._images[i] = image;
            sparks._age[i] = sparks._life[i] = 1f;
        }
        return sparks;
    }

    private void OnDisable()
    {
        for (int i = 0; i < Pool; i++)
        {
            _images[i].enabled = false;
            _age[i] = _life[i];
        }
        _due = 0f;
    }

    private void Update()
    {
        float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
        var tip = _tip?.Invoke();
        if (tip.HasValue)
        {
            _due += _rate * dt;
            while (_due >= 1f)
            {
                _due -= 1f;
                Emit(tip.Value.pos, tip.Value.dir.normalized);
            }
        }
        else
            _due = 0f;
        for (int i = 0; i < Pool; i++)
        {
            if (_age[i] >= _life[i])
                continue;
            _age[i] += dt;
            if (_age[i] >= _life[i])
            {
                _images[i].enabled = false;
                continue;
            }
            _vel[i] *= Mathf.Exp(-2.8f * dt);
            _pos[i] += _vel[i] * dt;
            float k = _age[i] / _life[i], size = _size[i] * (1f - 0.55f * k);
            _dots[i].anchoredPosition = new Vector2(_pos[i].x, -_pos[i].y);
            _dots[i].sizeDelta = new Vector2(size, size);
            var c = _hot[i] ? Color.Lerp(new Color(0.92f, 1f, 0.95f), V3.Green, k) : V3.Green;
            c.a = Mathf.Pow(1f - k, 1.4f);
            _images[i].color = c;
        }
    }

    private void Emit(Vector2 pos, Vector2 dir)
    {
        int slot = 0;
        for (int i = 0; i < Pool; i++)
        {
            if (_age[i] >= _life[i]) { slot = i; break; }
            if (_age[i] / _life[i] > _age[slot] / _life[slot]) slot = i;
        }
        var normal = new Vector2(-dir.y, dir.x);
        _pos[slot] = pos + normal * UnityEngine.Random.Range(-1.5f, 1.5f);
        _vel[slot] = -dir * UnityEngine.Random.Range(6f, 42f) + normal * UnityEngine.Random.Range(-30f, 30f);
        _life[slot] = UnityEngine.Random.Range(0.45f, 0.95f);
        _size[slot] = UnityEngine.Random.Range(2.2f, 4.8f);
        _hot[slot] = UnityEngine.Random.value < 0.3f;
        _age[slot] = 0f;
        _images[slot].enabled = true;
    }
}

// A glass tube's liquid (V3/Tube): how full it is, and its meniscus tilting while the level moves and swinging a few
// times as it settles.
public class V3Slosh : MonoBehaviour
{
    private Material _material;
    private float _shown, _amp;

    public float Fill { get; set; }

    public V3Slosh Init(Material material)
    {
        _material = material;
        return this;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
        float speed = Mathf.Abs(Fill - _shown) / dt;
        _shown = Fill;
        _amp = Mathf.Lerp(_amp, Mathf.Clamp01(speed * 2.5f), 1f - Mathf.Exp(-dt * (speed > 0.01f ? 8f : 2.2f)));
        _material.SetFloat("_Fill", _shown);
        _material.SetFloat("_Slosh", _amp * Mathf.Sin(Time.unscaledTime * 13f));
    }
}

// Kebbi's voice wave: seven small bars rising and falling out of step.
public class V3Wave : MonoBehaviour
{
    public RectTransform[] Bars;
    // 1 while Kebbi speaks, easing to 0 (short still bars) when it stops
    private float _level;

    private void Update()
    {
        _level = Mathf.MoveTowards(_level, KebbiSound.Speaking ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        for (int i = 0; i < Bars.Length; i++)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.3f + i * 1.6f);
            float h = Mathf.Lerp(4f, Mathf.Lerp(4f, 18f, k), _level);
            Bars[i].sizeDelta = new Vector2(3f, h);
            Bars[i].anchoredPosition = new Vector2(Bars[i].anchoredPosition.x, -(12f - h / 2f));
        }
    }
}

// A flow page's entrance: the page rises 22 px into place (.75 s, ease out) while its pieces fade in one after
// another, 45 ms apart. Pieces that already have their own CanvasGroup (keys) fade with the page.
public class V3PageIn : MonoBehaviour
{
    private float _t0;
    private bool _done, _built;
    private CanvasGroup _group;
    private readonly List<CanvasGroup> _pieces = new List<CanvasGroup>();
    private readonly HashSet<CanvasGroup> _mine = new HashSet<CanvasGroup>();

    private void OnEnable()
    {
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();
        _pieces.Clear();
        foreach (Transform child in transform)
        {
            var g = child.GetComponent<CanvasGroup>();
            if (g == null)
            {
                g = child.gameObject.AddComponent<CanvasGroup>();
                _mine.Add(g);
            }
            if (_mine.Contains(g))
                _pieces.Add(g);
        }
        _t0 = Time.unscaledTime;
        _done = false;
        Apply(0f);
        // a soft page sound - not while the pages are being built at start-up
        if (_built)
            V3Sounds.Play(V3Sounds.Cue.Page, 0.6f);
        _built = true;
    }

    private void Update()
    {
        if (!_done)
            Apply(Time.unscaledTime - _t0);
    }

    private void Apply(float t)
    {
        float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.75f), 4f);
        ((RectTransform)transform).anchoredPosition = new Vector2(0f, -22f * (1f - k));
        _group.alpha = Mathf.Clamp01(t / 0.3f);
        for (int i = 0; i < _pieces.Count; i++)
            _pieces[i].alpha = Mathf.Clamp01((t - i * 0.045f) / 0.35f);
        _done = t > 0.75f + _pieces.Count * 0.045f;
    }
}

// Keeps a static label in the current language.
public class V3Loc : MonoBehaviour
{
    private string _zh, _en;
    private float _spacingZh, _spacingEn;
    private TMP_Text _label;

    public void Set(string zh, string en, float spacingZh = 0f, float spacingEn = 0f)
    {
        _zh = zh;
        _en = en;
        _spacingZh = spacingZh;
        _spacingEn = spacingEn;
        _label = GetComponent<TMP_Text>();
        Apply();
    }

    private void OnEnable()
    {
        V3.LangChanged += Apply;
        Apply();
    }

    private void OnDisable() => V3.LangChanged -= Apply;

    private void Apply()
    {
        if (_label == null)
            return;
        _label.text = V3.T(_zh, _en);
        _label.characterSpacing = V3.Lang == 1 ? _spacingEn : _spacingZh;
    }
}

// Places a label right after another one's text (e.g. the mono code after a panel title) as the text changes.
public class V3Follow : MonoBehaviour
{
    private TMP_Text _lead;
    private RectTransform _follower;
    private float _gap;
    private string _seen;

    public void Init(TMP_Text lead, RectTransform follower, float gap)
    {
        _lead = lead;
        _follower = follower;
        _gap = gap;
    }

    private void LateUpdate()
    {
        if (_lead == null || _lead.text == _seen)
            return;
        _seen = _lead.text;
        _lead.ForceMeshUpdate();
        var lead = _lead.rectTransform;
        float left = lead.anchoredPosition.x;
        if (_lead.alignment == TextAlignmentOptions.Left || _lead.alignment == TextAlignmentOptions.TopLeft)
            _follower.anchoredPosition = new Vector2(left + _lead.preferredWidth + _gap, _follower.anchoredPosition.y);
    }
}

// Pressed state for V3 keys: a light grey face while held.
public class V3PressTint : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Image Target;
    private Color _up;
    private bool _down;

    public void OnPointerDown(PointerEventData eventData) => Set(true);
    public void OnPointerUp(PointerEventData eventData) => Set(false);
    public void OnPointerExit(PointerEventData eventData) => Set(false);
    private void OnDisable() => Set(false);

    private void Set(bool down)
    {
        if (Target == null || down == _down)
            return;
        if (down)
        {
            _up = Target.color;
            V3Sounds.Play(V3Sounds.Cue.Press);
        }
        _down = down;
        Target.color = down ? new Color32(0xF2, 0xF2, 0xEF, 0xFF) : _up;
    }
}

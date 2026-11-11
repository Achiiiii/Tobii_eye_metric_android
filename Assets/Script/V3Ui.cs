using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// V3 look shared by every flow page: the page is an eye-chart light box, cards are backlit windows in it (radius 10, a
// soft contact shadow), what is pressed or chosen (keys, answers) is a piece of milky glass with a fine rim
// (V3GlassFace), keys carry their icon first and a glass tube that fills while they are looked at, Kebbi's lines are
// violet with a small voice wave, Barlow Condensed for big numbers, mono for small data. Everything is laid out in the
// 1024 x 600 reference space, origin top-left.
public static class V3
{
    public const float W = 1024f, H = 600f;

    // ==================== colour ====================
    // Every colour the interface uses. Colour is light: green is the light of progress and what is done or chosen,
    // violet is Kebbi and the gaze, red only a failure; everything else is ink, the light box's whites and greys and
    // the metal's blue-greys.

    // ink and text
    public static readonly Color Ink = new Color32(0x14, 0x14, 0x14, 0xFF);
    public static readonly Color HeadInk = new Color32(0x3A, 0x3D, 0x43, 0xFF);      // the home's headline
    public static readonly Color Text2 = new Color32(0x5A, 0x5F, 0x63, 0xFF);        // secondary lines
    // light: green as a light source (liquid in the tubes, light guides, lamps); GreenLit for small solid marks lit by
    // it (arcs, checks, small lights), as the liquid shows it; GreenPale for its hottest, palest part (sparks, glows)
    public static readonly Color Green = new Color32(0x5F, 0xE3, 0xAE, 0xFF);
    public static readonly Color GreenLit = new Color(Green.r * 0.84f, Green.g * 0.84f, Green.b * 0.84f, 1f);
    public static readonly Color GreenPale = new Color32(0xDB, 0xFF, 0xF0, 0xFF);
    public static readonly Color Violet = new Color32(0x6E, 0x56, 0xCF, 0xFF);
    public static readonly Color Red = new Color32(0xE5, 0x48, 0x4D, 0xFF);
    // the light box: its lit face (top to bottom), the flat white behind the test, the veil a card lays over a page,
    // the aluminium frame and its fine white inner line
    public static readonly Color LightTop = new Color32(0xFA, 0xFB, 0xFD, 0xFF);
    public static readonly Color LightBottom = new Color32(0xEC, 0xEF, 0xF3, 0xFF);
    public static readonly Color LightFlat = new Color32(0xF5, 0xF7, 0xFA, 0xFF);
    public static readonly Color Veil = new Color32(0xF2, 0xF5, 0xFA, 0xFF);
    public static readonly Color Frame = new Color32(0xC9, 0xCE, 0xD6, 0xFF);
    public static readonly Color FrameLine = new Color32(0xFF, 0xFF, 0xFF, 0xE6);
    // keys: a secondary key, a face pressed in
    public static readonly Color KeySecondary = new Color32(0xED, 0xED, 0xEA, 0xFF);
    public static readonly Color Pressed = new Color32(0xF2, 0xF2, 0xEF, 0xFF);
    // grooves cut in the light box (with an alpha: a track, a light that is off)
    public static readonly Color Groove = new Color32(0x1C, 0x22, 0x2C, 0xFF);
    // metal: brushed steel, the machined groove and its shade, the key's centre dot and lit rim, a silver disc
    public static readonly Color Steel = new Color32(0xE2, 0xE5, 0xEA, 0xFF);
    public static readonly Color MetalGroove = new Color32(0x96, 0xA5, 0xBE, 0xFF);
    public static readonly Color MetalShade = new Color32(0xB9, 0xC8, 0xDE, 0xFF);
    public static readonly Color MetalDot = new Color32(0x78, 0x8C, 0xAA, 0xFF);
    public static readonly Color MetalRim = new Color32(0xED, 0xF2, 0xF9, 0xFF);
    public static readonly Color Silver = new Color32(0xB4, 0xBA, 0xC3, 0xFF);
    // every shadow (with an alpha), and the warm white of the studio's key light
    public static readonly Color ShadowInk = new Color32(0x18, 0x20, 0x30, 0xFF);
    public static readonly Color KeyLight = new Color32(0xFF, 0xFC, 0xF7, 0xFF);

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

    // Key: a piece of milky glass (V3GlassFace), the icon first, then the label; a glass tube along the bottom edge fills
    // while the key is looked at (V3KeyDwell reads the ButtonTrigger). Secondary keys (back) are lower.
    public static KeyParts Key(Transform parent, string zh, string en, string icon, Color chip, float x, float y, float w = 224f, bool secondary = false, Action onClick = null)
    {
        float h = secondary ? 56f : 72f;
        var k = new KeyParts();
        // the key and its shadow move and hide together: both live in one container (Root)
        k.Root = Rect("Key", parent, x, y, w, h);
        k.Group = k.Root.gameObject.AddComponent<CanvasGroup>();
        PatchShadow(k.Root, 0f, 0f, w, h, false);
        k.Button = UiFactory.CreateButton("Face", k.Root, UiFactory.White, Color.white, onClick != null ? new UnityEngine.Events.UnityAction(onClick) : null);
        k.Button.transition = Selectable.Transition.None;
        var glass = V3GlassFace.Attach((Image)k.Button.targetGraphic, w, h);
        var face = At((RectTransform)k.Button.transform, 0f, 0f, w, h);
        float g = secondary ? 20f : 24f;
        k.Glyph = Sprite("Glyph", face, Icon(icon), Ink, 20f, (h - g) / 2f - 2f, g, g);
        k.Chip = k.Glyph;
        // the tube clear of the glass's rim below it
        var tube = Tube(face, 16f, h - 18f, w - 32f, 9f);
        float lx = 20f + g + 14f;
        k.Label = Text("Label", face, Zh, zh, en, secondary ? V3Type.Body : V3Type.Key, Ink, lx, 0f, w - lx - 14f, h - 4f, TextAlignmentOptions.Left);
        // a long label (English) shrinks rather than running past the frame
        k.Label.enableAutoSizing = true;
        k.Label.fontSizeMin = V3Type.Label;
        k.Label.fontSizeMax = secondary ? V3Type.Body : V3Type.Key;
        var press = k.Button.gameObject.AddComponent<V3PressTint>();
        press.Target = (Image)k.Button.targetGraphic;
        press.Glass = glass;
        k.Button.gameObject.AddComponent<V3KeyDwell>().Init(tube, face, new Vector2(16f, h - 18f), w - 32f);
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
        l.Text.lineSpacing = 10f;
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
        LightBoxFrame(bg.transform);
        return bg;
    }

    // The light box's 9 px aluminium frame round the screen's edge, with a fine white line inside it.
    public static void LightBoxFrame(Transform parent)
    {
        Box("FrameT", parent, Frame, 0f, 0f, W, 9f);
        Box("FrameB", parent, Frame, 0f, H - 9f, W, 9f);
        Box("FrameL", parent, Frame, 0f, 0f, 9f, H);
        Box("FrameR", parent, Frame, W - 9f, 0f, 9f, H);
        Hairline(parent, FrameLine, 9f, 9f, W - 18f, H - 18f);
    }

    // A dialog's panel: a slab of the keys' milky glass with a wider rim, the page behind faintly through its face.
    public static void GlassPanel(Transform parent, float x, float y, float w, float h)
    {
        PatchShadow(parent, x, y, w, h, false);
        V3GlassFace.Attach(Box("Glass", parent, Color.white, x, y, w, h), w, h, 14f, 5f, 0.4f);
    }

    // The window's contact shadow: 0 10 18 -10 and a tight 0 2 2 (a glass piece draws its own tight one, rounded with
    // it: tight false).
    public static void PatchShadow(Transform parent, float x, float y, float w, float h, bool tight = true)
    {
        Shadow(parent, x, y, w, h, 18f, -10f, 10f, A(ShadowInk, 0.18f));
        if (tight)
            Shadow(parent, x, y, w, h, 2f, 0f, 2f, A(ShadowInk, 0.14f));
    }

    private static Sprite s_lightBox, s_rim;

    // #FAFBFD at the top to #ECEFF3 at the bottom.
    public static Sprite LightBoxSprite
    {
        get
        {
            if (s_lightBox != null)
                return s_lightBox;
            Color top = LightTop, bottom = LightBottom;
            return s_lightBox = Raster(64, p => Color.Lerp(bottom, top, p.y / 64f));
        }
    }

    // The chosen rim round a window: a 1.5 px outline, radius 14 (the window's 10 plus the 4 px gap), 9-sliced at 20.
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
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1.25f - Mathf.Abs(d + 0.75f)));
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return s_rim = UnityEngine.Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
        }
    }

    // The green's light round something lit (GlowAt): a tight bright halo (3 px) and a wider faint one (9 px) off its
    // edge - for what is d px from the lit thing's edge.
    public const float GlowReach = 22f;

    public static float GlowAt(float d)
    {
        d = Mathf.Max(d, 0f);
        return 0.5f * Mathf.Exp(-d * d / 9f) + 0.18f * Mathf.Exp(-d * d / 81f);
    }

    private static Sprite s_rimGlow;

    // The chosen rim's light: the glow round RimSprite's outline, 9-sliced at 52, GlowReach px wider than the rim on
    // every side.
    public static Sprite RimGlowSprite
    {
        get
        {
            if (s_rimGlow != null)
                return s_rimGlow;
            const int s = 128;
            const float r = 14f, m = GlowReach;
            return s_rimGlow = Slice(Raster(s, p =>
            {
                var q = new Vector2(Mathf.Abs(p.x - s / 2f) - (s / 2f - m - r), Mathf.Abs(p.y - s / 2f) - (s / 2f - m - r));
                float d = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
                return new Color(1f, 1f, 1f, GlowAt(Mathf.Abs(d + 0.75f) - 0.75f) * Mathf.Clamp01((s / 2f - Mathf.Max(Mathf.Abs(p.x - s / 2f), Mathf.Abs(p.y - s / 2f))) / 3f));
            }), 52f);
        }
    }

    private static Sprite Slice(Sprite sprite, float border)
    {
        return UnityEngine.Sprite.Create(sprite.texture, sprite.rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    // The glow round a bar h px tall with round ends, GlowReach px wider on every side: BarGlowSide px tall, 9-sliced
    // along its length so the image can be as long as the bar is.
    public static float BarGlowSide(float h) => h + 2f * GlowReach;

    public static Sprite BarGlowSprite(float h)
    {
        string key = "barglow" + h;
        if (s_metal.TryGetValue(key, out var cached))
            return cached;
        int side = Mathf.CeilToInt(BarGlowSide(h));
        float end = GlowReach + h / 2f;
        var sprite = Raster(side, p =>
        {
            float x = Mathf.Clamp(p.x, end, side - end), d = Mathf.Sqrt((p.x - x) * (p.x - x) + (p.y - side / 2f) * (p.y - side / 2f)) - h / 2f;
            float edge = Mathf.Min(Mathf.Min(p.x, side - p.x), Mathf.Min(p.y, side - p.y));
            return new Color(1f, 1f, 1f, GlowAt(d) * Mathf.Clamp01(edge / 3f));
        });
        sprite = UnityEngine.Sprite.Create(sprite.texture, sprite.rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(end + 1f, 0f, end + 1f, 0f));
        s_metal[key] = sprite;
        return sprite;
    }

    // The glow round a disc of the given diameter, in a square GlowReach px wider on every side.
    public static Sprite DiscGlowSprite(float diameter)
    {
        string key = "glow" + diameter;
        if (s_metal.TryGetValue(key, out var cached))
            return cached;
        float half = diameter / 2f + GlowReach;
        var sprite = Raster(Mathf.CeilToInt(4f * half), p =>
        {
            float x = p.x / 2f - half, y = p.y / 2f - half, d = Mathf.Sqrt(x * x + y * y);
            return new Color(1f, 1f, 1f, GlowAt(d - diameter / 2f) * Mathf.Clamp01((half - d) / 3f));
        });
        s_metal[key] = sprite;
        return sprite;
    }

    // ==================== light guides, scales, metal ====================

    public class GuideParts
    {
        public RectTransform Root;
        public V3Slosh Tube;
        public float W, F;
    }

    // A horizontal progress guide (24 px tall box): a 12 px glass tube with the green liquid in it (V3/Tube), sparks
    // off the liquid's front while it is on the way, the home dial's scale under it (in that many sections) when asked.
    public static GuideParts Guide(Transform parent, float x, float y, float w, bool ticks = false, int sections = 4)
    {
        var g = new GuideParts { W = w };
        g.Root = Rect("Guide", parent, x, y, w, 24f);
        g.Tube = Tube(g.Root, 0f, 0f, w, 12f);
        if (ticks)
            Sprite("Scale", g.Root, ScaleStrip(w, sections), Color.white, 0f, 15f, Mathf.Ceil(w) + 1f, ScaleStripH);
        V3Sparks.Attach(g.Root, () => g.F > 0.002f && g.F < 0.999f ? (new Vector2(g.W * g.F, 6f), Vector2.right) : ((Vector2, Vector2)?)null);
        SetGuide(g, 0f);
        return g;
    }

    public static void SetGuide(GuideParts g, float f)
    {
        g.F = Mathf.Clamp01(f);
        g.Tube.Fill = g.F;
    }


    private const int ScaleStripH = 14;
    private static readonly Dictionary<string, Sprite> s_strips = new Dictionary<string, Sprite>();

    // The home dial's scale unrolled, w px long in the given sections: a fine rule, ticks of three lengths hanging from
    // it (about 5.6 px apart, longer each quarter section, longest at each section's end), a faint engraved band and a
    // second, fainter rule at their feet. Drawn 1:1 with every tick on a whole pixel, so it stays crisp.
    public static Sprite ScaleStrip(float w, int sections)
    {
        string key = "scale" + w + "/" + sections;
        if (s_strips.TryGetValue(key, out var cached))
            return cached;
        int width = Mathf.CeilToInt(w) + 1, height = ScaleStripH;
        var px = new Color32[width * height];
        byte r = (byte)(Ink.r * 255f), g = (byte)(Ink.g * 255f), b = (byte)(Ink.b * 255f);
        void Put(int x, int row, float a)
        {
            if (x < 0 || x >= width || row < 0 || row >= height)
                return;
            int i = (height - 1 - row) * width + x;
            if (a * 255f > px[i].a)
                px[i] = new Color32(r, g, b, (byte)(a * 255f));
        }
        for (int x = 0; x < width; x++)
            for (int row = 0; row < height; row++)
                Put(x, row, row == 0 ? 0.3f : row == height - 1 ? 0.14f : 0.05f);
        float section = w / sections;
        int per = Mathf.Max(4, Mathf.RoundToInt(section / 5.6f / 4f) * 4);
        for (int i = 0; i <= sections * per; i++)
        {
            int x = Mathf.RoundToInt(i * w / (sections * per));
            bool major = i % per == 0, mid = i % (per / 4) == 0;
            int len = major ? 12 : mid ? 7 : 4;
            for (int row = 0; row < len; row++)
            {
                Put(x, row, major ? 0.9f : mid ? 0.62f : 0.42f);
                if (major)
                    Put(x + 1, row, 0.25f);
            }
        }
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        s_strips[key] = sprite;
        return sprite;
    }

    // A glass tube w x h with green liquid in it (V3/Tube, its own material); set how full through the V3Slosh.
    public static V3Slosh Tube(Transform parent, float x, float y, float w, float h)
    {
        // the liquid's light on the light box: a soft green glow under the tube, as long as the liquid
        var glow = Shadow(parent, x, y, 0f, h, 10f, -1f, 3f, A(Green, 0f));
        var image = Sprite("Tube", parent, UiFactory.White, Green, x, y, w, h);
        var material = new Material(Resources.Load<Shader>("V3/Shaders/Tube"));
        material.SetVector("_Size", new Vector4(w, h, 0f, 0f));
        image.material = material;
        var slosh = image.gameObject.AddComponent<V3Slosh>().Init(material);
        slosh.Glow = glow.rectTransform;
        slosh.GlowImage = glow;
        slosh.Width = w;
        return slosh;
    }

    // A scale: a guide tube over the dial's ticks, the right zone lit violet (from - to, 0-1), a black needle pointer
    // over it and mono labels spread under it. Returns the pointer (move it with SetMark).
    public static RectTransform Scale(Transform parent, float x, float y, float w, float zoneFrom, float zoneTo, string[] labels, string[] labelsEn = null)
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
            // words follow the language (larger: Chinese at 11 px does not read); numbers stay as they are
            if (labelsEn != null)
                Text("Label", g.Root, Mono, labels[i], labelsEn[i], V3Type.Data, A(Ink, 0.7f), left, 30f, 120f, 16f, align, 0.06f);
            else
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

    // A round rim of the keys' polished glass, radius r (its middle) and width w, centred on (cx, cy): the bright line on
    // its outer edge (brightest toward the light, top left), the pale grey-green fall across it, the fine dark line on
    // its inner edge, a tight contact shadow round it and a soft one on what it holds.
    public static Image GlassRim(Transform parent, float cx, float cy, float r, float w)
    {
        float half = r + w / 2f + 4f;
        return Sprite("GlassRim", parent, GlassRimSprite(r, w), Color.white, cx - half, cy - half, 2f * half, 2f * half);
    }

    private static Sprite GlassRimSprite(float r, float w)
    {
        string key = "rim" + r + "/" + w;
        if (s_metal.TryGetValue(key, out var cached))
            return cached;
        float outer = r + w / 2f, inner = r - w / 2f, half = outer + 4f;
        Vector2 light = new Vector2(-0.5f, 0.8f).normalized;
        Color hair = new Color(0.58f, 0.65f, 0.63f);
        var sprite = Raster(Mathf.CeilToInt(4f * half), p =>
        {
            float x = p.x / 2f - half, y = p.y / 2f - half, d = Mathf.Sqrt(x * x + y * y);
            float e = outer - d;
            float cover = Mathf.Clamp01(e * 2f + 0.5f) * Mathf.Clamp01((d - inner) * 2f + 0.5f);
            float lit = Mathf.Clamp01((d > 0f ? (x * light.x + y * light.y) / d : 0f) * 0.5f + 0.5f);
            Color c = Color.Lerp(new Color(0.92f, 0.95f, 0.94f), new Color(0.80f, 0.87f, 0.85f), Mathf.Clamp01(e / w)) * Mathf.Lerp(0.9f, 1.05f, lit);
            c = Color.Lerp(c, Color.white, 0.75f * Mathf.Clamp01(1f - Mathf.Abs(e - 0.7f) / 0.8f) * Mathf.Lerp(0.5f, 1f, lit));
            c = Color.Lerp(c, hair, 0.55f * Mathf.Clamp01(1f - Mathf.Abs(e - (w - 0.5f)) / 0.75f));
            // the shadows: tight round the outside (a little deeper below), soft on what is inside
            float below = Mathf.Sqrt(x * x + (y + 0.8f) * (y + 0.8f)) - outer;
            float shade = d > r ? 0.16f * Mathf.Clamp01(1f - Mathf.Max(below, 0f) / 2.4f)
                : 0.2f * Mathf.Clamp01(1f - Mathf.Max(inner - Mathf.Sqrt(x * x + (y - 1.5f) * (y - 1.5f)), 0f) / 5f);
            shade *= 1f - cover;
            float a = cover + shade;
            Color rgb = a > 0f ? (c * cover + ShadowInk * shade) / a : ShadowInk;
            return new Color(rgb.r, rgb.g, rgb.b, a);
        });
        s_metal[key] = sprite;
        return sprite;
    }

    // A small mono note (page labels like "Q2 / 7  ·  眼科病史").
    public static TextMeshProUGUI Note(Transform parent, float x, float y, float w = 600f, string text = "")
    {
        return Text("Note", parent, Mono, text, V3Type.Data, Ink, x, y, w, 16f, TextAlignmentOptions.TopLeft, 0.04f);
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

// The glass tube along a key's bottom edge: how far the gaze dwell on it has run - shown only on keys that can be
// chosen by looking (a ButtonTrigger on them); a touch key has no empty tube.
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
        bool gaze = _trigger != null && _trigger.enabled;
        if (_tube.gameObject.activeSelf != gaze)
        {
            _tube.gameObject.SetActive(gaze);
            if (_tube.Glow != null)
                _tube.Glow.gameObject.SetActive(gaze);
        }
        _f = gaze ? Mathf.Clamp01(_trigger.Progress) : 0f;
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
            var c = _hot[i] ? Color.Lerp(V3.GreenPale, V3.Green, k) : V3.Green;
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

// A glass tube's liquid (V3/Tube): how full it is, its meniscus tilting while the level moves and swinging a few
// times as it settles, bobbing when it rises; a light sweeping through to the front each time it rises (again every
// 0.9 s while it keeps rising), a flash once it is full, and its light on the light box under it (Glow).
public class V3Slosh : MonoBehaviour
{
    private const float SweepSeconds = V3Motion.Base, FlashSeconds = V3Motion.Slow;
    private Material _material;
    private float _shown, _amp, _bob, _sweepT0 = -10f, _flashT0 = -10f, _risingSince = -10f;

    public float Fill { get; set; }
    // the tube's glow on the light box (set by V3.Tube), stretched to the liquid
    public RectTransform Glow { get; set; }
    public Image GlowImage { get; set; }
    public float Width { get; set; }

    public V3Slosh Init(Material material)
    {
        _material = material;
        return this;
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime, dt = Mathf.Max(1e-4f, Time.unscaledDeltaTime);
        float step = Fill - _shown, speed = Mathf.Abs(step) / dt;
        if (step > 0.0005f)
        {
            if (now - _risingSince > 0.3f || now - _sweepT0 > 0.9f)
                _sweepT0 = now;
            _risingSince = now;
            _bob = Mathf.Max(_bob, Mathf.Clamp01(step * 20f));
            if (Fill >= 0.999f && _shown < 0.999f)
                _flashT0 = now;
        }
        _shown = Fill;
        _amp = Mathf.Lerp(_amp, Mathf.Clamp01(speed * 2.5f), 1f - Mathf.Exp(-dt * (speed > 0.01f ? 8f : 2.2f)));
        _bob *= Mathf.Exp(-dt * 6f);
        float sweep = (now - _sweepT0) / SweepSeconds, flash = (now - _flashT0) / FlashSeconds;
        _material.SetFloat("_Fill", _shown);
        _material.SetFloat("_Slosh", _amp * Mathf.Sin(now * 13f));
        _material.SetFloat("_Bob", _bob * 1.6f * Mathf.Sin(now * 17f));
        _material.SetFloat("_Sweep", sweep >= 0f && sweep < 1f ? (1f - Mathf.Pow(1f - sweep, 2f)) * _shown : -1f);
        _material.SetFloat("_Flash", flash >= 0f && flash < 1f ? Mathf.Sin(flash * Mathf.PI) : 0f);
        if (Glow != null)
        {
            Glow.sizeDelta = new Vector2(Mathf.Max(0f, Width * _shown) + 18f, Glow.sizeDelta.y);
            var c = GlowImage.color;
            c.a = _shown > 0.002f ? 0.34f + 0.2f * (flash >= 0f && flash < 1f ? Mathf.Sin(flash * Mathf.PI) : 0f) : 0f;
            GlowImage.color = c;
        }
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

// A flow page's entrance: the page comes into focus (V3Focus) and rises 16 px into place (.45 s, ease out) while its
// pieces fade in one after another, 30 ms apart. Pieces that already have their own CanvasGroup (keys) fade with the
// page.
public class V3PageIn : MonoBehaviour
{
    private float _t;
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
        _t = 0f;
        _done = false;
        Apply(0f);
        // a soft page sound and the focus - not while the pages are being built at start-up
        if (_built)
        {
            V3Sounds.Play(V3Sounds.Cue.Page, 0.6f);
            V3Focus.Pulse();
        }
        _built = true;
    }

    private void Update()
    {
        if (!_done)
            Apply(_t += V3Motion.Step);
    }

    private void Apply(float t)
    {
        float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / V3Motion.Slow), 4f);
        ((RectTransform)transform).anchoredPosition = new Vector2(0f, -16f * (1f - k));
        _group.alpha = Mathf.Clamp01(t / V3Motion.Quick);
        for (int i = 0; i < _pieces.Count; i++)
            _pieces[i].alpha = Mathf.Clamp01((t - i * V3Motion.Stagger) / V3Motion.Quick);
        _done = t > V3Motion.Slow + _pieces.Count * V3Motion.Stagger;
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

// Pressed state for V3 keys: the face sinks a little and darkens while held, springing back past its size when let
// go, and a disc of green light spreads from where it was touched. A glass key (Glass) does both inside itself: it
// shades along its top as if pressed in, and the light follows its rounded shape.
public class V3PressTint : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private const float Sink = 0.035f;
    public Image Target;
    public V3GlassFace Glass;
    private Color _up;
    private bool _down, _centred;
    private float _depth, _speed, _rippleT = 1f;
    private Vector2 _rippleAt;
    private RectTransform _ripple;
    private Image _rippleImage;

    public void OnPointerDown(PointerEventData eventData)
    {
        Set(true);
        Ripple(eventData);
    }

    public void OnPointerUp(PointerEventData eventData) => Set(false);
    public void OnPointerExit(PointerEventData eventData) => Set(false);

    private void OnDisable()
    {
        Set(false);
        _depth = _speed = 0f;
        _rippleT = 1f;
        transform.localScale = Vector3.one;
        if (_rippleImage != null)
            _rippleImage.enabled = false;
        if (Glass != null)
        {
            Glass.Press = 0f;
            Glass.Ripple(Vector2.zero, 0f, 0f);
        }
    }

    private void Ripple(PointerEventData e)
    {
        var rect = (RectTransform)transform;
        // only on keys, not the small round buttons
        if (rect.rect.width < 60f)
            return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local);
        _rippleT = 0f;
        if (Glass != null)
        {
            _rippleAt = local - rect.rect.center;
            return;
        }
        if (_ripple == null)
        {
            if (GetComponent<RectMask2D>() == null)
                gameObject.AddComponent<RectMask2D>();
            _rippleImage = UiFactory.CreateImage("Ripple", rect, UiFactory.Circle, V3.A(V3.Green, 0f));
            _ripple = _rippleImage.rectTransform;
            _ripple.SetAsFirstSibling();
        }
        _ripple.anchorMin = _ripple.anchorMax = rect.pivot;
        _ripple.pivot = UiFactory.Center;
        _ripple.anchoredPosition = local;
        _rippleImage.enabled = true;
    }

    private void Update()
    {
        var rect = (RectTransform)transform;
        if (!_centred)
        {
            // sink about the middle of the face
            Vector2 size = rect.rect.size, shift = UiFactory.Center - rect.pivot;
            rect.pivot = UiFactory.Center;
            rect.anchoredPosition += new Vector2(shift.x * size.x, shift.y * size.y);
            _centred = true;
        }
        float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
        _speed += ((_down ? 1f : 0f) - _depth) * 900f * dt - _speed * 32f * dt;
        _depth += _speed * dt;
        rect.localScale = Vector3.one * (1f - Sink * _depth);
        if (Glass != null)
            Glass.Press = Mathf.Clamp01(_depth);
        if (_rippleT < 1f)
        {
            _rippleT = Mathf.Min(1f, _rippleT + dt / V3Motion.Slow);
            float k = 1f - Mathf.Pow(1f - _rippleT, 3f);
            float size = Mathf.Lerp(16f, 2.4f * rect.rect.width, k), alpha = 0.3f * (1f - _rippleT);
            if (Glass != null)
                Glass.Ripple(_rippleAt, size / 2f, alpha);
            else
            {
                _ripple.sizeDelta = Vector2.one * size;
                _rippleImage.color = V3.A(V3.Green, alpha);
                _rippleImage.enabled = _rippleT < 1f;
            }
        }
    }

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
        if (Glass == null)
            Target.color = down ? V3.Pressed : _up;
    }
}

// The focus layer: everything under it (the flow pages and the scene) seen out of focus under a light veil, on its own
// canvas between the pages and the overlays (V3/FocusBlur). A page change runs it from soft to sharp (Pulse), as an
// eye focusing on the new page; a card holds it soft behind it while it is up (Hold / Release).
public class V3Focus : MonoBehaviour
{
    private static V3Focus s_instance;
    private Image _image;
    private Material _material;
    private readonly HashSet<object> _holds = new HashSet<object>();
    private float _pulse, _pulseSeconds = V3Motion.Slow, _hold;

    private static V3Focus Get()
    {
        if (s_instance != null)
            return s_instance;
        var canvas = UiFactory.CreateOverlayCanvas("V3Focus", V3Layer.Focus, null);
        DontDestroyOnLoad(canvas.gameObject);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        s_instance = canvas.gameObject.AddComponent<V3Focus>();
        s_instance._material = new Material(Resources.Load<Shader>("V3/Shaders/FocusBlur"));
        s_instance._image = UiFactory.CreateImage("Focus", canvas.transform, UiFactory.White, Color.white);
        s_instance._image.material = s_instance._material;
        s_instance._image.raycastTarget = false;
        UiFactory.Stretch(s_instance._image.rectTransform);
        s_instance._image.enabled = false;
        return s_instance;
    }

    public static void Pulse(float seconds = V3Motion.Slow)
    {
        var f = Get();
        f._pulse = 1f;
        f._pulseSeconds = seconds;
    }

    public static void Hold(object who) => Get()._holds.Add(who);

    public static void Release(object who)
    {
        if (s_instance != null)
            s_instance._holds.Remove(who);
    }

    private void LateUpdate()
    {
        // paced by frames, so a page that takes a frame or two to come up still focuses in front of the eyes
        float dt = V3Motion.Step;
        _pulse = Mathf.Max(0f, _pulse - dt / _pulseSeconds);
        float pulse = _pulse * _pulse;
        _hold = Mathf.MoveTowards(_hold, _holds.Count > 0 ? 1f : 0f, dt / V3Motion.Quick);
        float hold = 1f - (1f - _hold) * (1f - _hold);
        float radius = Mathf.Max(pulse * 18f, hold * 10f), veil = Mathf.Max(pulse * 0.45f, hold * 0.3f);
        _image.enabled = radius > 0.05f;
        if (_image.enabled)
        {
            _material.SetFloat("_Radius", radius);
            _material.SetColor("_Veil", V3.A(V3.Veil, veil));
        }
    }
}

// A card's entrance (put on the root that is shown and hidden): the veil fades in to a lighter tint than before, so
// the page shows through it out of focus (V3Focus holds while the card is up), and the card rises into focus from a
// little smaller. ownCanvas: a card on the pages' own canvas gets one above the focus layer.
public class V3CardIn : MonoBehaviour
{
    private const float VeilAlpha = 0.55f;
    private Image _veil;
    private RectTransform _card;
    private CanvasGroup _cardGroup;
    private float _t;

    public static void Attach(GameObject root, Image veil, RectTransform card, bool ownCanvas = false)
    {
        var c = root.AddComponent<V3CardIn>();
        c._veil = veil;
        c._card = card;
        c._cardGroup = card.GetComponent<CanvasGroup>();
        if (c._cardGroup == null)
            c._cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        // turn about the card's middle
        Vector2 size = card.rect.size, shift = UiFactory.Center - card.pivot;
        card.pivot = UiFactory.Center;
        card.anchoredPosition += new Vector2(shift.x * size.x, shift.y * size.y);
        if (ownCanvas)
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null)
                canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = V3Layer.CardOnPages;
            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();
        }
    }

    private void OnEnable()
    {
        _t = 0f;
        V3Focus.Hold(this);
        Apply(0f);
        // a light passes across the card's glass as it comes up
        var glass = _card != null ? _card.Find("Glass") : null;
        if (glass != null && glass.TryGetComponent<V3GlassFace>(out var face))
            face.Sweep();
    }

    private void OnDisable() => V3Focus.Release(this);

    private void Update() => Apply(_t += V3Motion.Step);

    private void Apply(float t)
    {
        if (_card == null)
            return;
        _card.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, V3Motion.Back(t / V3Motion.Quick));
        _cardGroup.alpha = Mathf.Clamp01(t / V3Motion.Instant);
        if (_veil != null)
        {
            var c = _veil.color;
            c.a = VeilAlpha * Mathf.Clamp01(t / V3Motion.Instant);
            _veil.color = c;
        }
    }
}

// A light on the light box that follows the gaze, slowly, as a lamp's reflection moves on glass when the head moves:
// the page answers the eyes - or, on the touch pages (no gaze then), the last touch, drifting slowly when left alone.
// Off while gaze is being measured (V3Flow.SheenAllowed false: the calibration dots, the countdown, the test).
public class V3Sheen : MonoBehaviour
{
    public FollowGazePoint2D Pointer;
    public RectTransform Light;
    public Image LightImage;
    private Vector2 _at = new Vector2(V3.W * 0.5f, V3.H * 0.35f), _touch = new Vector2(V3.W * 0.5f, V3.H * 0.35f);
    private float _touchAt = -100f, _alpha;

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        bool on = V3Flow.SheenAllowed;
        if (Input.touchCount > 0 || Input.GetMouseButton(0))
        {
            Vector2 s = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            _touch = new Vector2(s.x * V3.W / Screen.width, (Screen.height - s.y) * V3.W / Screen.width);
            _touchAt = now;
        }
        Vector2 target;
        if (Pointer != null && Pointer.isActiveAndEnabled)
        {
            var s = Pointer.DisplayedScreenPosition;
            target = new Vector2(s.x * V3.W / Screen.width, (Screen.height - s.y) * V3.W / Screen.width);
        }
        else if (now - _touchAt < 4f)
            target = _touch;
        else
            target = new Vector2(V3.W * (0.5f + 0.3f * Mathf.Sin(now * 0.21f)), V3.H * (0.4f + 0.2f * Mathf.Sin(now * 0.33f + 1f)));
        if (on)
            _at = Vector2.Lerp(_at, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.5f));
        _alpha = Mathf.MoveTowards(_alpha, on ? 1f : 0f, Time.unscaledDeltaTime / 0.4f);
        Light.anchoredPosition = new Vector2(_at.x, -_at.y);
        var c = LightImage.color;
        c.a = 0.55f * _alpha;
        LightImage.color = c;
    }
}

// Type sizes (px at 1024 x 600): one step for each kind of text. Fonts by role: V3.Zh (bold, the voice of the
// interface), V3.ZhMedium (secondary lines), V3.Mono (small data, notes), V3.Num (big values), V3.Headline (the
// home's word). English titles run a step smaller where Chinese and English share a box (x 0.9).
public static class V3Type
{
    public const float Dial = 7f;       // the dial's engraving and degree numbers
    public const float Micro = 10f;     // mono labels on scales
    public const float Data = 12f;      // mono data: level, notes
    public const float Label = 16f;     // app name, chips, small labels
    public const float BodyS = 18f;     // secondary lines, tips, the percentage
    public const float Body = 20f;      // status lines, secondary keys
    public const float Key = 24f;       // keys, Kebbi's line, card labels
    public const float Lead = 26f;      // answers, the home's modes
    public const float Title = 30f;     // card titles, the practice line
    public const float Display = 40f;   // page titles
    public const float Answer = 48f;    // yes / no answers
    public const float Big = 124f;      // the result's values
    public const float Huge = 170f;     // the countdown
}

// Motion: four durations and the easings every transition is built from. Shared pieces (page entrance, focus, cards,
// presses, rings, tubes) use these; the home's opening is its own choreography on top of them. Clocks advance by the
// frames drawn, each at most MaxStep, so a slow frame does not swallow a transition.
public static class V3Motion
{
    public const float Instant = 0.12f;  // a tint, a fade of a small piece
    public const float Quick = 0.2f;     // a press, a pop, a card rising
    public const float Base = 0.3f;      // a question changing, a sweep
    public const float Slow = 0.45f;     // a page coming in, a ripple, a flash
    public const float Stagger = 0.03f;  // between pieces coming in one after another
    public const float MaxStep = 1f / 30f;

    public static float Step => Mathf.Min(Time.unscaledDeltaTime, MaxStep);
    public static float EaseOut(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);
    // overshoots a little and settles (as a press springing back)
    public static float Back(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f + 2.2f * Mathf.Pow(x - 1f, 3f) + 1.2f * Mathf.Pow(x - 1f, 2f);
    }
}

// The canvases' drawing order (overlay sorting order): what lies over what.
public static class V3Layer
{
    public const int Scene = 0;          // the scene's own canvas (test symbol and answers)
    public const int Pages = 3;          // the flow pages
    public const int Focus = 4;          // the focus layer (V3Focus) over the pages
    public const int Home = 6;           // the home
    public const int CardOnPages = 17;   // a card belonging to a page, lifted over the focus layer
    public const int Overlay = 18;       // pause card, exit dialog
    public const int Presence = 19;      // "still there?"
    public const int Hud = 20;           // the top-right keys
}

// A round progress ring as a glass tube of green liquid (V3/RingTube): the selection and calibration rings. Set Fill
// (0-1); Steady (0-1) and Lean (-1..1, y up) let it answer the gaze; Lock() closes it - a ring of light running in to
// the centre and a flash there. A light sweeps through the liquid each time it starts rising (again every 0.9 s while it
// keeps rising).
public class V3LiquidRing : MonoBehaviour
{
    private Material _material;
    private float _shown, _steady = 1f, _sweepT0 = -10f, _risingSince = -10f, _lock = -1f;
    private Vector2 _lean;

    public float Fill { get; set; }
    public float Steady { get; set; } = 1f;
    public Vector2 Lean { get; set; }

    // size: the ring's box (in the parent's units); radius and half thickness in the 120-unit ring sprites' terms
    public static V3LiquidRing Create(Transform parent, float size, float radius, float half, Color track)
    {
        var image = UiFactory.CreateImage("LiquidRing", parent, UiFactory.White, V3.Green);
        image.raycastTarget = false;
        UiFactory.Place(image.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * size);
        var material = new Material(Resources.Load<Shader>("V3/Shaders/RingTube"));
        material.SetFloat("_Q", size);
        material.SetFloat("_R", radius * size / 120f);
        material.SetFloat("_T", half * size / 120f);
        material.SetColor("_Track", track);
        material.SetFloat("_Glow", 1f);
        image.material = material;
        var ring = image.gameObject.AddComponent<V3LiquidRing>();
        ring._material = material;
        return ring;
    }

    // An open arc of the ring: span (0-1 of the circle) from startDeg (clockwise from the top), its radius and half
    // thickness in px. Fill counts in parts of the whole circle.
    public static V3LiquidRing Gauge(Transform parent, float size, float radius, float half, Color track, float span, float startDeg)
    {
        var ring = Create(parent, size, radius * 120f / size, half * 120f / size, track);
        ring._material.SetFloat("_Span", span);
        ring._material.SetFloat("_Start", startDeg * Mathf.Deg2Rad);
        ring._material.SetFloat("_Px", 1f);
        return ring;
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }

    public void Lock() => _lock = 0f;

    public void Unlock() => _lock = -1f;

    private void LateUpdate()
    {
        float now = Time.unscaledTime, dt = V3Motion.Step;
        if (Fill - _shown > 0.0005f)
        {
            if (now - _risingSince > 0.3f || now - _sweepT0 > 0.9f)
                _sweepT0 = now;
            _risingSince = now;
        }
        _shown = Fill;
        float k = 1f - Mathf.Exp(-dt * 8f);
        _steady = Mathf.Lerp(_steady, Steady, k);
        _lean = Vector2.Lerp(_lean, Vector2.ClampMagnitude(Lean, 1f), k);
        if (_lock >= 0f)
        {
            _lock += dt / V3Motion.Slow;
            if (_lock >= 1f)
                _lock = -1f;
        }
        float sweep = (now - _sweepT0) / V3Motion.Base;
        _material.SetFloat("_Fill", _shown);
        _material.SetFloat("_Steady", _steady);
        _material.SetVector("_Lean", new Vector4(_lean.x, _lean.y, 0f, 0f));
        _material.SetFloat("_Sweep", sweep >= 0f && sweep < 1f ? V3Motion.EaseOut(sweep) : -1f);
        _material.SetFloat("_Lock", Mathf.Max(0f, _lock));
    }
}

// How still the eyes are: the gaze's spread over the last third of a second (Steady 1 = still, 0 = wandering, by
// Spread screen px), and where it is drifting from its mean (Lean, -1..1, y up).
public class V3GazeSteadiness
{
    private const float Window = 0.35f;
    private readonly Queue<(float t, Vector2 p)> _samples = new Queue<(float, Vector2)>();
    public float Spread = 18f;
    public float Steady { get; private set; } = 1f;
    public Vector2 Lean { get; private set; }

    public void Add(Vector2 screen)
    {
        float now = Time.unscaledTime;
        _samples.Enqueue((now, screen));
        while (_samples.Count > 2 && _samples.Peek().t < now - Window)
            _samples.Dequeue();
        Vector2 mean = Vector2.zero;
        foreach (var s in _samples)
            mean += s.p;
        mean /= _samples.Count;
        float var = 0f;
        foreach (var s in _samples)
            var += (s.p - mean).sqrMagnitude;
        float spread = Spread * Screen.width / V3.W;
        Steady = 1f - Mathf.Clamp01(Mathf.Sqrt(var / _samples.Count) / spread);
        Lean = Vector2.ClampMagnitude((screen - mean) / spread, 1f);
    }

    public void Clear()
    {
        _samples.Clear();
        Steady = 1f;
        Lean = Vector2.zero;
    }
}

// A small round light's glow: laid just under it, it shows while the light is on and green (the green is light; the
// violet and the ink are not given one).
public class V3LampGlow : MonoBehaviour
{
    private Image _lamp, _glow;

    public static void Attach(Image lamp, float diameter)
    {
        var glow = UiFactory.CreateImage("Glow", lamp.transform.parent, V3.DiscGlowSprite(diameter), Color.clear);
        glow.transform.SetSiblingIndex(lamp.transform.GetSiblingIndex());
        float box = diameter + 2f * V3.GlowReach;
        var rect = glow.rectTransform;
        rect.anchorMin = rect.anchorMax = lamp.rectTransform.anchorMin;
        rect.pivot = UiFactory.Center;
        rect.sizeDelta = new Vector2(box, box);
        var l = lamp.gameObject.AddComponent<V3LampGlow>();
        l._lamp = lamp;
        l._glow = glow;
    }

    private void LateUpdate()
    {
        var c = _lamp.color;
        bool green = _lamp.enabled && c.a > 0.5f && c.g > c.r + 0.25f && c.g > c.b;
        _glow.enabled = green;
        if (!green)
            return;
        _glow.color = c;
        var lamp = _lamp.rectTransform;
        // centred on the lamp, wherever it is put
        _glow.rectTransform.anchoredPosition = lamp.anchoredPosition + Vector2.Scale(lamp.rect.size, UiFactory.Center - lamp.pivot);
    }

    private void OnDisable()
    {
        if (_glow != null)
            _glow.enabled = false;
    }
}

// A pressable piece (keys, answers) drawn as a block of the E's milky glass (V3/GlassKey): Lit lights it from inside
// when it is chosen (eased), Press shades it while it is held, Ripple spreads a touch's green light through it. It
// draws its own tight contact shadow, rounded with it, so it is laid on the window's soft shadow only
// (V3.PatchShadow, tight false).
public class V3GlassFace : MonoBehaviour
{
    private const float SheenSeconds = 0.7f;
    private Material _material;
    private float _lit, _sheen = -1f;

    public bool Lit { get; set; }

    public float Press
    {
        set
        {
            if (_material != null)
                _material.SetFloat("_Press", value);
        }
    }

    // radius, the rim's width (px) and how much of what is behind shows through the face (0: none)
    public static V3GlassFace Attach(Image image, float w, float h, float radius = 8f, float edge = 4f, float clear = 0f)
    {
        var face = image.gameObject.AddComponent<V3GlassFace>();
        face._material = new Material(Resources.Load<Shader>("V3/Shaders/GlassKey"));
        face._material.SetVector("_Size", new Vector4(w, h, 0f, 0f));
        face._material.SetFloat("_Radius", radius);
        face._material.SetFloat("_Edge", edge);
        face._material.SetFloat("_Clear", clear);
        image.sprite = UiFactory.White;
        image.type = Image.Type.Simple;
        image.material = face._material;
        return face;
    }

    // A disc of green light: its centre (px from the middle, y up), radius (px) and strength (0: none).
    public void Ripple(Vector2 at, float radius, float strength)
    {
        if (_material != null)
            _material.SetVector("_Ripple", new Vector4(at.x, at.y, radius, strength));
    }

    // A light passes across the glass once.
    public void Sweep() => _sheen = 0f;

    private void LateUpdate()
    {
        if (_sheen >= 0f)
        {
            _sheen += V3Motion.Step / SheenSeconds;
            _material.SetFloat("_Sheen", _sheen < 1f ? Mathf.Lerp(-0.2f, 1.2f, _sheen * _sheen * (3f - 2f * _sheen)) : -1f);
            if (_sheen >= 1f)
                _sheen = -1f;
        }
        float lit = Mathf.MoveTowards(_lit, Lit ? 1f : 0f, V3Motion.Step / V3Motion.Quick);
        if (lit == _lit)
            return;
        _lit = lit;
        _material.SetFloat("_Lit", V3Motion.EaseOut(_lit));
    }

    private void OnDestroy()
    {
        if (_material != null)
            Destroy(_material);
    }
}

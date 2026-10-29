using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// V3 "lab" look shared by every flow page: bright page, hairline panels with ink corner
// brackets, keys bracketed like the home's mode keys, Kebbi's lines as a violet rule + text, Unbounded for big
// numbers, mono for small data. Everything is laid out in the 1024 x 600 reference space, origin top-left.
public static class V3
{
    public const float W = 1024f, H = 600f;

    public static readonly Color Ink = new Color32(0x14, 0x14, 0x14, 0xFF);
    public static readonly Color Page = new Color32(0xF7, 0xF7, 0xF5, 0xFF);
    // The flow pages sit on a cool light grey with a fine grain; the test and
    // its lead-in keep the home's Page colour. Shadows are a cool blue-grey.
    public static readonly Color Cool = new Color32(0xF3, 0xF4, 0xF6, 0xFF);
    public static readonly Color ShadowInk = new Color32(0x18, 0x20, 0x30, 0xFF);
    public static readonly Color Mint = new Color32(0x7C, 0xF2, 0xA8, 0xFF);
    public static readonly Color MintSoft = new Color32(0xE3, 0xFB, 0xEC, 0xFF);
    public static readonly Color Green = new Color32(0x1F, 0x9D, 0x5C, 0xFF);
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

    private static TMP_FontAsset s_zh, s_zhMedium, s_head, s_mono;

    // Noto Sans TC Bold: every Chinese text, and English body text.
    public static TMP_FontAsset Zh => s_zh != null ? s_zh : (s_zh = LoadFont("NotoSansTC-Bold", 0.12f));
    public static TMP_FontAsset ZhMedium => s_zhMedium != null ? s_zhMedium : (s_zhMedium = LoadFont("NotoSansTC-Medium", 0.08f));
    // Unbounded Bold: big numbers (question number, vision values, countdown).
    public static TMP_FontAsset Head => s_head != null ? s_head : (s_head = LoadFont("Unbounded-Bold", 0f));
    // IBM Plex Mono Medium: small data labels and codes.
    public static TMP_FontAsset Mono => s_mono != null ? s_mono : (s_mono = LoadFont("IBMPlexMono-Medium", 0f));

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

    // Ink L-brackets on the corners of a rect (the home's crop marks): arm length, stroke, offset outward.
    public static void Brackets(Transform parent, float x, float y, float w, float h, float arm, float stroke, float offset, bool allFour, Color? color = null)
    {
        var c = color ?? Ink;
        float l = x - offset, t = y - offset, r = x + w + offset, b = y + h + offset;
        Box("BrTL1", parent, c, l, t, arm, stroke); Box("BrTL2", parent, c, l, t, stroke, arm);
        Box("BrBR1", parent, c, r - arm, b - stroke, arm, stroke); Box("BrBR2", parent, c, r - stroke, b - arm, stroke, arm);
        if (!allFour)
            return;
        Box("BrTR1", parent, c, r - arm, t, arm, stroke); Box("BrTR2", parent, c, r - stroke, t, stroke, arm);
        Box("BrBL1", parent, c, l, b - stroke, arm, stroke); Box("BrBL2", parent, c, l, b - arm, stroke, arm);
    }

    // ==================== components ====================

    public class PanelParts
    {
        public RectTransform Root, Body;
        public TextMeshProUGUI Title, Tag;
        public Image TagDot;
    }

    // Instrument panel: white 84 %, hairline frame, ink brackets on all four corners, a 48 px header on a rule
    // (icon + name + mono code on the left, a violet dot + mode on the right). Body = the area under the header.
    public static PanelParts Panel(Transform parent, string icon, string zh, string en, string code, float x = 304f, float y = 132f, float w = 680f, float h = 428f)
    {
        var p = new PanelParts();
        // the card floats: a long cool shadow (0 26 48 -30) and a tight one (0 2 4), drawn behind it
        Shadow(parent, x, y, w, h, 48f, -30f, 26f, A(ShadowInk, 0.42f));
        Shadow(parent, x, y, w, h, 4f, 0f, 2f, A(ShadowInk, 0.06f));
        p.Root = Rect("Panel", parent, x, y, w, h);
        Box("Fill", p.Root, Color.white, 0f, 0f, w, h);
        Hairline(p.Root, new Color(20 / 255f, 24 / 255f, 32 / 255f, 0.16f), 0f, 0f, w, h);
        Brackets(p.Root, 0f, 0f, w, h, 14f, 1.5f, 1f, true);
        Box("HeadRule", p.Root, A(Ink, 0x1F / 255f), 1f, 48f, w - 2f, 1f);
        float tx = 28f;
        if (icon != null)
        {
            Sprite("Icon", p.Root, Icon(icon), Ink, 28f, 15f, 18f, 18f);
            tx = 54f;
        }
        p.Title = Text("Title", p.Root, Zh, zh, en, 17f, Ink, tx, 0f, 300f, 48f, TextAlignmentOptions.Left, 0.06f);
        p.Title.ForceMeshUpdate();
        var codeLabel = Text("Code", p.Root, Mono, code, 10f, A(Ink, 0x73 / 255f), tx + p.Title.preferredWidth + 10f, 0f, 200f, 48f, TextAlignmentOptions.Left, 0.1f);
        p.Title.gameObject.AddComponent<V3Follow>().Init(p.Title, codeLabel.rectTransform, 10f);
        p.Tag = Text("Tag", p.Root, ZhMedium, "", 14f, A(Ink, 0x99 / 255f), w - 24f - 300f, 0f, 300f, 48f, TextAlignmentOptions.Right);
        p.TagDot = Sprite("TagDot", p.Root, UiFactory.Circle, Violet, 0f, 21f, 6f, 6f);
        p.Body = Rect("Body", p.Root, 28f, 49f, w - 56f, h - 49f - 24f);
        return p;
    }

    public static void SetTag(PanelParts p, string text)
    {
        p.Tag.text = text;
        p.Tag.ForceMeshUpdate();
        float w = ((RectTransform)p.Root).sizeDelta.x;
        At(p.TagDot.rectTransform, w - 24f - p.Tag.preferredWidth - 14f, 21f, 6f, 6f);
        p.TagDot.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    public class KeyParts
    {
        public Button Button;
        public RectTransform Root;
        public TextMeshProUGUI Label;
        public Image Chip, Glyph, Fill;
        public CanvasGroup Group;
    }

    // Key like the home's mode keys: white, 1 px ink frame, brackets on two corners, round chip first, then the
    // label. Secondary keys (back) are lower, lighter and have no brackets.
    public static KeyParts Key(Transform parent, string zh, string en, string icon, Color chip, float x, float y, float w = 224f, bool secondary = false, Action onClick = null)
    {
        float h = secondary ? 56f : 72f;
        var k = new KeyParts();
        // the key and its shadow move and hide together: both live in one container (Root)
        k.Root = Rect("Key", parent, x, y, w, h);
        k.Group = k.Root.gameObject.AddComponent<CanvasGroup>();
        KeyShadow(k.Root, 0f, 0f, w, h);
        k.Button = UiFactory.CreateButton("Face", k.Root, UiFactory.White, Color.white, onClick != null ? new UnityEngine.Events.UnityAction(onClick) : null);
        k.Button.transition = Selectable.Transition.None;
        var face = At((RectTransform)k.Button.transform, 0f, 0f, w, h);
        // dwell / pressed fill from the bottom (used by the gaze key on the practice page)
        k.Fill = Box("Fill", face, Mint, 0f, 0f, w, 0f);
        UiFactory.Place(k.Fill.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(w, 0f));
        Hairline(face, secondary ? A(Ink, 0x66 / 255f) : Ink, 0f, 0f, w, h);
        if (!secondary)
            Brackets(face, 0f, 0f, w, h, 7f, 1.5f, 4f, false);
        float chipSize = secondary ? 30f : 40f;
        k.Chip = Sprite("Chip", face, UiFactory.Circle, chip, 16f, (h - chipSize) / 2f, chipSize, chipSize);
        float g = secondary ? 20f : 26f;
        k.Glyph = Sprite("Glyph", k.Chip.transform, Icon(icon), Ink, (chipSize - g) / 2f, (chipSize - g) / 2f, g, g);
        k.Label = Text("Label", face, Zh, zh, en, secondary ? 20f : 24f, Ink, 16f + chipSize + 16f, 0f, w - chipSize - 48f - 12f, h, TextAlignmentOptions.Left);
        // a long label (English) shrinks rather than running past the frame
        k.Label.enableAutoSizing = true;
        k.Label.fontSizeMin = 14f;
        k.Label.fontSizeMax = secondary ? 20f : 24f;
        k.Button.gameObject.AddComponent<V3PressTint>().Target = (Image)k.Button.targetGraphic;
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

    // Kebbi's line: the Kebbi icon, a 2 px violet rule, a mono "KEBBI" caption and the words (large).
    public static LineParts KebbiLine(Transform parent, float x, float y, float textSize, float textWidth)
    {
        var l = new LineParts();
        l.Root = Rect("KebbiLine", parent, x, y, 54f + 26f + 22f + textWidth, 120f);
        Sprite("Kebbi", l.Root, KebbiSprite, Color.white, 0f, 33f, 54f, 54f);
        l.Rule = Box("Rule", l.Root, Violet, 80f, 0f, 2f, 120f);
        var cap = Text("Caption", l.Root, Mono, "KEBBI", 10f, A(Violet, 0.85f), 102f, 4f, 100f, 12f, TextAlignmentOptions.TopLeft, 0.12f);
        l.Text = Text("Words", l.Root, Zh, "", textSize, Ink, 102f, 22f, textWidth, 96f, TextAlignmentOptions.TopLeft);
        l.Text.enableWordWrapping = true;
        l.Text.lineSpacing = -8f;
        return l;
    }

    // Fit the rule and the block height to the words; keeps the icon centred on them.
    public static void SetLine(LineParts l, string words)
    {
        l.Text.text = words;
        l.Text.ForceMeshUpdate();
        float h = Mathf.Max(54f, 22f + l.Text.preferredHeight + 2f);
        l.Root.sizeDelta = new Vector2(l.Root.sizeDelta.x, h);
        l.Rule.rectTransform.sizeDelta = new Vector2(2f, h);
        l.Text.rectTransform.sizeDelta = new Vector2(l.Text.rectTransform.sizeDelta.x, h - 22f);
        var kebbi = (RectTransform)l.Root.Find("Kebbi");
        kebbi.anchoredPosition = new Vector2(0f, -(h - 54f) / 2f);
    }

    public class SideParts
    {
        public RectTransform Root;
        public TextMeshProUGUI Label, Number, Of;
        public TextMeshProUGUI[] Steps;
        public Image[] StepDots;
    }

    // Left column (x 40-264): mono caps label, the big Unbounded number with "/ 07", optional step list.
    public static SideParts Side(Transform parent, string label, int of, string[] stepsZh = null, string[] stepsEn = null)
    {
        var s = new SideParts();
        s.Root = Rect("Side", parent, 40f, 132f, 224f, 428f);
        s.Label = Text("Label", s.Root, Mono, label.ToUpperInvariant(), 11f, A(Ink, 0x99 / 255f), 0f, 0f, 224f, 14f, TextAlignmentOptions.TopLeft, 0.14f);
        s.Number = Text("Number", s.Root, Head, "01", 52f, Ink, 0f, 14f, 160f, 64f, TextAlignmentOptions.TopLeft);
        s.Of = Text("Of", s.Root, Mono, "/ " + of.ToString("00"), 18f, A(Ink, 0.7f), 0f, 44f, 80f, 24f, TextAlignmentOptions.TopLeft);
        s.Number.gameObject.AddComponent<V3Follow>().Init(s.Number, s.Of.rectTransform, 12f);
        if (stepsZh != null)
        {
            s.Steps = new TextMeshProUGUI[stepsZh.Length];
            s.StepDots = new Image[stepsZh.Length];
            for (int i = 0; i < stepsZh.Length; i++)
            {
                float y = 96f + i * 28f;
                s.StepDots[i] = Sprite("Dot", s.Root, UiFactory.Circle, Ink, 64f, y + 5f, 8f, 8f);
                s.Steps[i] = Text("Step", s.Root, Zh, stepsZh[i], stepsEn[i], 18f, Ink, 82f, y, 140f, 20f, TextAlignmentOptions.TopLeft);
            }
        }
        return s;
    }

    // Steps before the current one: ink dot; current: mint dot with an ink ring; later: hollow, grey.
    public static void SetStep(SideParts s, int current)
    {
        if (s.Steps == null)
            return;
        for (int i = 0; i < s.Steps.Length; i++)
        {
            s.Steps[i].color = i <= current ? Ink : A(Ink, 0x59 / 255f);
            s.StepDots[i].sprite = i < current ? UiFactory.Circle : RingSprite;
            s.StepDots[i].color = i < current ? Ink : i == current ? Green : A(Ink, 0x59 / 255f);
        }
    }

    public static void SetNumber(SideParts s, int n)
    {
        s.Number.text = n.ToString("00");
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

    // The keys' drop shadow (A2): 0 1 0 hairline + 0 8 14 -10.
    public static void KeyShadow(Transform parent, float x, float y, float w, float h)
    {
        Shadow(parent, x, y, w, h, 14f, -10f, 8f, A(ShadowInk, 0.38f));
        Box("Edge", parent, A(ShadowInk, 0.14f), x, y + h, w, 1f);
    }

    private static Sprite s_shadow, s_reticle;
    private static Texture2D s_grain;

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

    // Progress reticle for the ruler (22 px): a white ring with an ink edge, four ticks, a violet centre.
    public static Sprite ReticleSprite => s_reticle != null ? s_reticle : (s_reticle = Raster(88, p =>
    {
        Vector2 q = p / 4f - new Vector2(11f, 11f);   // 22-unit square centred
        float r = q.magnitude;
        const float k = 4f;
        Color c = new Color(1f, 1f, 1f, 0f);
        float disc = r - 5f;
        c = Blend(c, Color.white, Mathf.Clamp01(0.5f - disc * k));
        float ring = Mathf.Abs(r - 5f) - 0.65f;
        float ax = Mathf.Abs(q.x), ay = Mathf.Abs(q.y);
        float ticks = Mathf.Min(Mathf.Max(ax - 0.65f, Mathf.Max(7f - ay, ay - 10.5f)), Mathf.Max(ay - 0.65f, Mathf.Max(7f - ax, ax - 10.5f)));
        c = Blend(c, Ink, Mathf.Clamp01(0.5f - Mathf.Min(ring, ticks) * k));
        c = Blend(c, Violet, Mathf.Clamp01(0.5f - (r - 1.9f) * k));
        return c;
    }));

    // A fine cool grain to lay over the page (128 px tile, at most ~6 % ink).
    public static Texture2D Grain
    {
        get
        {
            if (s_grain != null)
                return s_grain;
            const int size = 128;
            s_grain = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[size * size];
            var random = new System.Random(11);
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(0x17, 0x1C, 0x24, (byte)(random.NextDouble() * random.NextDouble() * 16));
            s_grain.SetPixels32(px);
            s_grain.Apply(false, true);
            return s_grain;
        }
    }

    private static Color Blend(Color under, Color over, float a)
    {
        float outA = a + under.a * (1f - a);
        if (outA <= 0f)
            return new Color(over.r, over.g, over.b, 0f);
        Color c = (over * a + under * under.a * (1f - a)) / outA;
        c.a = outA;
        return c;
    }

    // ==================== sprites ====================

    private static readonly Dictionary<string, Sprite> s_icons = new Dictionary<string, Sprite>();
    private static Sprite s_kebbi, s_ring;

    public static Sprite RingSprite => s_ring != null ? s_ring : (s_ring = Raster(64, p =>
    {
        float d = Mathf.Abs((p - new Vector2(32f, 32f)).magnitude - 27f) - 4f;
        return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
    }));

    // Simplified Kebbi Air: white head with an ink edge, black face screen, two mint ring eyes.
    public static Sprite KebbiSprite => s_kebbi != null ? s_kebbi : (s_kebbi = Raster(128, p =>
    {
        Vector2 q = p / 2f;   // 64-unit space, y up
        q.y = 64f - q.y;
        float head = RoundBox(q, new Vector2(32f, 32f), new Vector2(29f, 23f), 17f);
        float face = RoundBox(q, new Vector2(32f, 32f), new Vector2(22f, 17f), 11f);
        float eyeL = Mathf.Abs((q - new Vector2(23.5f, 32f)).magnitude - 6f) - 1.4f;
        float eyeR = Mathf.Abs((q - new Vector2(40.5f, 32f)).magnitude - 6f) - 1.4f;
        const float k = 2f;   // texels per unit
        Color c = new Color(1f, 1f, 1f, 0f);
        c = Over(c, Ink, Mathf.Clamp01(0.5f - (head - 1f) * k));
        c = Over(c, Color.white, Mathf.Clamp01(0.5f - (head + 1f) * k));
        c = Over(c, Ink, Mathf.Clamp01(0.5f - face * k));
        c = Over(c, Mint, Mathf.Clamp01(0.5f - Mathf.Min(eyeL, eyeR) * k));
        return c;
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

    private static float RoundBox(Vector2 p, Vector2 center, Vector2 half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + Vector2.one * r;
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
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

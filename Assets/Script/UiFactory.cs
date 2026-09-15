using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class UiFactory
{
    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    private static readonly Dictionary<TMP_FontAsset, Material> PlainMaterials = new Dictionary<TMP_FontAsset, Material>();

    private static Sprite _white;
    private static Sprite _circle;
    private static Sprite _ring;
    private static Sprite _thickRing;
    private static Sprite _roundedRect;
    private static Sprite _roundedFrame;
    private static Sprite _check;
    private static Sprite _refreshArrow;

    public static Sprite White => _white != null ? _white : (_white = MakeSprite(4, (p, size) => -1f));
    public static Sprite Circle => _circle != null ? _circle : (_circle = MakeSprite(128, CircleDistance));
    public static Sprite Ring => _ring != null ? _ring : (_ring = MakeSprite(128, RingDistance));
    public static Sprite ThickRing => _thickRing != null ? _thickRing : (_thickRing = MakeSprite(128, (p, size) => RingDistance(p, size, 0.18f)));
    public static Sprite RoundedRect => _roundedRect != null ? _roundedRect : (_roundedRect = MakeSprite(64, RoundedRectDistance, new Vector4(24f, 24f, 24f, 24f)));
    public static Sprite RoundedFrame => _roundedFrame != null ? _roundedFrame : (_roundedFrame = MakeSprite(64, RoundedFrameDistance, new Vector4(24f, 24f, 24f, 24f)));
    public static Sprite Check => _check != null ? _check : (_check = MakeSprite(128, CheckDistance));
    public static Sprite RefreshArrow => _refreshArrow != null ? _refreshArrow : (_refreshArrow = MakeSprite(128, RefreshArrowDistance));

    public static Canvas CreateOverlayCanvas(string name, int sortingOrder, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        // Same reference resolution as the scene's main Canvas so sizes line up with existing UI.
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024f, 600f);
        scaler.matchWidthOrHeight = 0f;
        return canvas;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Center;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, bool raycastTarget = false)
    {
        var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSharedMaterial = PlainMaterial(font);
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>
    /// Wraps characters missing from a static font atlas (e.g. ASCII punctuation in the Chinese atlas)
    /// in a font tag for TMP's default font, so they render instead of showing as empty boxes.
    /// </summary>
    public static string WithLatinFallback(string text, TMP_FontAsset font)
    {
        var fallback = TMP_Settings.defaultFontAsset;
        if (string.IsNullOrEmpty(text) || fallback == null || fallback == font)
            return text;

        var builder = new StringBuilder(text.Length + 32);
        bool inFallback = false;
        foreach (char original in text)
        {
            char c = original;
            // Full-width punctuation missing from both atlases (e.g. U+FF0F) is shown as its ASCII form.
            if (c >= (char)0xFF01 && c <= (char)0xFF5E && !font.HasCharacter(c) && !fallback.HasCharacter(c))
                c = (char)(c - 0xFEE0);
            bool useFallback = !char.IsWhiteSpace(c) && !font.HasCharacter(c) && fallback.HasCharacter(c);
            if (useFallback != inFallback)
            {
                builder.Append(useFallback ? "<font=\"" + fallback.name + "\">" : "</font>");
                inFallback = useFallback;
            }
            builder.Append(c);
        }
        if (inFallback)
            builder.Append("</font>");
        return builder.ToString();
    }

    /// <summary>
    /// The Chinese font asset's default material has a yellow face, heavy dilate and a navy outline (title style).
    /// Body text uses a copy without them so its vertex colour shows as-is.
    /// </summary>
    public static Material PlainMaterial(TMP_FontAsset font)
    {
        if (PlainMaterials.TryGetValue(font, out var cached) && cached != null)
            return cached;

        ShaderUtilities.GetShaderPropertyIDs();
        var material = new Material(font.material) { name = font.material.name + " (Plain)" };
        material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
        material.DisableKeyword(ShaderUtilities.Keyword_Outline);
        material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        ShaderUtilities.UpdateShaderRatios(material);
        PlainMaterials[font] = material;
        return material;
    }

    public static Button CreateButton(string name, Transform parent, Sprite sprite, Color color, UnityAction onClick)
    {
        var image = CreateImage(name, parent, sprite, color, true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
            button.onClick.AddListener(onClick);
        return button;
    }

    // Shapes are rasterised from signed distance functions (negative inside) so edges stay anti-aliased.
    private static Sprite MakeSprite(int size, Func<Vector2, float, float> signedDistance, Vector4 border = default)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float coverage = Mathf.Clamp01(0.5f - signedDistance(new Vector2(x + 0.5f, y + 0.5f), size));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Center, 100f, 0, SpriteMeshType.FullRect, border);
    }

    private static float CircleDistance(Vector2 p, float size)
    {
        return Vector2.Distance(p, Vector2.one * (size / 2f)) - (size / 2f - 1f);
    }

    private static float RingDistance(Vector2 p, float size)
    {
        return RingDistance(p, size, 0.11f);
    }

    private static float RingDistance(Vector2 p, float size, float thicknessFraction)
    {
        float thickness = size * thicknessFraction;
        float radius = size / 2f - 1f - thickness / 2f;
        return Mathf.Abs(Vector2.Distance(p, Vector2.one * (size / 2f)) - radius) - thickness / 2f;
    }

    private static float RoundedRectDistance(Vector2 p, float size)
    {
        const float radius = 20f;
        float half = size / 2f;
        Vector2 q = new Vector2(Mathf.Abs(p.x - half), Mathf.Abs(p.y - half)) - Vector2.one * (half - 1f - radius);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    private static float RoundedFrameDistance(Vector2 p, float size)
    {
        const float thickness = 8f;
        return Mathf.Abs(RoundedRectDistance(p, size) + thickness / 2f) - thickness / 2f;
    }

    private static float CheckDistance(Vector2 p, float size)
    {
        Vector2 a = new Vector2(0.26f, 0.52f) * size;
        Vector2 b = new Vector2(0.43f, 0.34f) * size;
        Vector2 c = new Vector2(0.76f, 0.68f) * size;
        return Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c)) - size * 0.055f;
    }

    private static float RefreshArrowDistance(Vector2 p, float size)
    {
        const float startAngle = 50f;
        const float endAngle = 340f;
        Vector2 center = Vector2.one * (size / 2f);
        float radius = size * 0.3f;
        float halfThickness = size * 0.05f;

        Vector2 offset = p - center;
        float angle = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg, 360f);
        float arc = angle >= startAngle && angle <= endAngle
            ? Mathf.Abs(offset.magnitude - radius) - halfThickness
            : Mathf.Min(Vector2.Distance(p, center + Polar(startAngle) * radius), Vector2.Distance(p, center + Polar(endAngle) * radius)) - halfThickness;

        Vector2 normal = Polar(endAngle);
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        Vector2 end = center + normal * radius;
        float head = TriangleDistance(p, end + tangent * size * 0.17f, end + normal * size * 0.14f, end - normal * size * 0.14f);
        return Mathf.Min(arc, head);
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float TriangleDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float orientation = Mathf.Sign((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
        return Mathf.Max(EdgeDistance(p, a, b, orientation), Mathf.Max(EdgeDistance(p, b, c, orientation), EdgeDistance(p, c, a, orientation)));
    }

    private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b, float orientation)
    {
        Vector2 edge = (b - a).normalized;
        return -orientation * (edge.x * (p.y - a.y) - edge.y * (p.x - a.x));
    }

    private static Vector2 Polar(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}

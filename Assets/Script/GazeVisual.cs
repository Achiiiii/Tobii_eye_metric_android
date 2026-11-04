using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Replaces the gaze pointer's ring sprite with the V3 dial cursor: a slowly turning tick ring, an inner ring
// on a faint white disc and a violet centre dot. GazeDwellIndicator draws the dwell arc outside the ticks.
// The dial stays on the screen: when the gaze goes past an edge, the dial
// waits at that edge, faded, with a small violet arrow pointing to where the gaze is; the pointer itself (its rect and
// collider, and the recorded gaze) keeps the true position, so nothing at the edge is selected by it.
public class GazeVisual : MonoBehaviour
{
    // The gaze canvas is 1920 units wide: the 80 px dial is 150 units.
    private const float DialSize = 150f;

    private const float EdgeMargin = 46f;   // how far the dial's centre stays inside the edge (canvas units)
    private RectTransform _pointer;
    private RectTransform _ticks;
    private RectTransform _dial, _arrow;
    private CanvasGroup _dialGroup;
    private static Sprite s_ticks;

    // 24 ticks around the dial, every sixth longer (120-unit square, as the other dial sprites).
    public static Sprite TickSprite()
    {
        return s_ticks != null ? s_ticks : (s_ticks = V3.Raster(240, p =>
        {
            Vector2 q = p / 2f - new Vector2(60f, 60f);
            float r = q.magnitude, a = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
            float nearest = Mathf.Round(a / 15f) * 15f;
            int index = Mathf.RoundToInt(nearest / 15f);
            bool major = ((index % 6) + 6) % 6 == 0;
            float across = Mathf.Abs(Mathf.Sin((a - nearest) * Mathf.Deg2Rad)) * r;
            float inner = 31.5f, outer = major ? 39f : 34.5f;
            float along = Mathf.Max(inner - r, r - outer);
            float d = Mathf.Max(across - 1.1f, along);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * 2f));
        }));
    }

    public static void Attach(FollowGazePoint2D pointer)
    {
        var pointerRect = (RectTransform)pointer.transform;

        // Keep the pointer's rect and collider (they drive gaze-dwell hit testing); only hide its sprite.
        var originalImage = pointer.GetComponent<Image>();
        originalImage.color = Color.clear;
        originalImage.raycastTarget = false;

        // the dial's parts hang from one rect, so they can stay on the screen while the pointer goes past an edge
        var dial = UiFactory.CreateRect("Dial", pointerRect);
        UiFactory.Place(dial, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * DialSize);
        var dialGroup = dial.gameObject.AddComponent<CanvasGroup>();
        dialGroup.blocksRaycasts = false;
        pointerRect = dial;

        // 120-unit sprites: inner ring r17 of the 80 px dial = 25.5, ticks r21-23 (every 6th from r26)
        // a 2.4 px ink ring on a white outline, bold ticks and an 8 px dot, readable on any background
        var disc = UiFactory.CreateImage("Disc", pointerRect, UiFactory.Circle, new Color(1f, 1f, 1f, 0.5f));
        UiFactory.Place(disc.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * DialSize * 34f / 80f);
        var outline = UiFactory.CreateImage("Outline", pointerRect, V3Setup.RingSprite(25.5f, 3.6f), Color.white);
        UiFactory.Place(outline.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * DialSize);
        var ring = UiFactory.CreateImage("Ring", pointerRect, V3Setup.RingSprite(25.5f, 1.8f), V3.Ink);
        UiFactory.Place(ring.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * DialSize);
        var ticks = UiFactory.CreateImage("Ticks", pointerRect, TickSprite(), V3.A(V3.Ink, 0.85f));
        UiFactory.Place(ticks.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * DialSize);
        var dotEdge = UiFactory.CreateImage("GazeDotEdge", pointerRect, UiFactory.Circle, Color.white);
        UiFactory.Place(dotEdge.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * DialSize * 11f / 80f);
        var dot = UiFactory.CreateImage("GazeDot", pointerRect, UiFactory.Circle, V3.Violet);
        UiFactory.Place(dot.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * DialSize * 8f / 80f);

        // the arrow past the edge: a small violet chevron just outside the ring, turned toward the gaze
        var arrow = UiFactory.CreateRect("EdgeArrow", dial);
        UiFactory.Place(arrow, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.zero);
        foreach (float side in new[] { 1f, -1f })
        {
            var stroke = UiFactory.CreateImage("Stroke", arrow, UiFactory.White, V3.Violet);
            UiFactory.Place(stroke.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(-5f, side * 5f), new Vector2(16f, 4f));
            stroke.rectTransform.localEulerAngles = new Vector3(0f, 0f, -side * 40f);
        }
        arrow.gameObject.SetActive(false);

        pointerRect = (RectTransform)pointer.transform;
        var container = UiFactory.CreateRect("GazeVisual", pointerRect.parent);
        UiFactory.Place(container, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.zero);
        container.SetSiblingIndex(pointerRect.GetSiblingIndex());

        var visual = container.gameObject.AddComponent<GazeVisual>();
        visual._pointer = pointerRect;
        visual._ticks = ticks.rectTransform;
        visual._dial = dial;
        visual._dialGroup = dialGroup;
        visual._arrow = arrow;
    }

    private void LateUpdate()
    {
        // the tick ring turns once every 12 s
        if (_ticks != null)
            _ticks.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 30f);
        if (!_pointer.gameObject.activeInHierarchy)
            return;
        KeepOnScreen();

    }

    // The dial at the nearest point inside the screen; faded, with the arrow toward the gaze, while the gaze is past it.
    private void KeepOnScreen()
    {
        var area = ((RectTransform)_pointer.parent).rect;
        Vector2 at = _pointer.anchoredPosition;
        var inside = new Vector2(Mathf.Clamp(at.x, area.xMin + EdgeMargin, area.xMax - EdgeMargin),
            Mathf.Clamp(at.y, area.yMin + EdgeMargin, area.yMax - EdgeMargin));
        Vector2 past = at - inside;
        _dial.anchoredPosition = past.sqrMagnitude > 0f ? -past : Vector2.zero;
        bool outside = past.sqrMagnitude > 4f;
        _dialGroup.alpha = Mathf.MoveTowards(_dialGroup.alpha, outside ? 0.45f : 1f, Time.unscaledDeltaTime * 4f);
        _arrow.gameObject.SetActive(outside);
        if (outside)
        {
            float a = Mathf.Atan2(past.y, past.x);
            _arrow.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * DialSize * 0.42f;
            _arrow.localEulerAngles = new Vector3(0f, 0f, a * Mathf.Rad2Deg);
        }
    }

}

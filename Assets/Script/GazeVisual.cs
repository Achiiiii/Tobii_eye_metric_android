using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Replaces the gaze pointer's ring sprite with the V3 dial cursor: a slowly turning tick ring, an inner ring
// on a faint white disc and a violet centre dot. GazeDwellIndicator draws the dwell arc outside the ticks.
// (The red dot's fading trail is off in V3.)
public class GazeVisual : MonoBehaviour
{
    private const int TrailLength = 0;
    // The gaze canvas is 1920 units wide: the 80 px dial is 150 units.
    private const float DialSize = 150f;
    private const float TrailSpacingSeconds = 0.03f;
    private const float HistorySeconds = 0.5f;
    private static readonly Color DotColor = new Color32(0xE5, 0x39, 0x35, 0xFF);

    private readonly List<(float time, Vector2 position)> _history = new List<(float time, Vector2 position)>();
    private RectTransform _pointer;
    private RectTransform _ticks;
    private Image[] _trail;
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

        var container = UiFactory.CreateRect("GazeTrail", pointerRect.parent);
        UiFactory.Place(container, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.zero);
        container.SetSiblingIndex(pointerRect.GetSiblingIndex());

        var visual = container.gameObject.AddComponent<GazeVisual>();
        visual._pointer = pointerRect;
        visual._ticks = ticks.rectTransform;
        visual._trail = new Image[TrailLength];
        for (int i = 0; i < TrailLength; i++)
        {
            float t = (i + 1f) / TrailLength;
            var color = new Color(DotColor.r, DotColor.g, DotColor.b, Mathf.Lerp(0.45f, 0.05f, t));
            var image = UiFactory.CreateImage("Trail" + i, container, UiFactory.Circle, color);
            UiFactory.Place(image.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * Mathf.Lerp(30f, 9f, t));
            image.gameObject.SetActive(false);
            visual._trail[i] = image;
        }
    }

    private void LateUpdate()
    {
        // the tick ring turns once every 12 s
        if (_ticks != null)
            _ticks.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 30f);
        if (!_pointer.gameObject.activeInHierarchy)
        {
            if (_history.Count > 0)
            {
                _history.Clear();
                foreach (var image in _trail)
                    image.gameObject.SetActive(false);
            }
            return;
        }

        float now = Time.unscaledTime;
        _history.Add((now, _pointer.anchoredPosition));
        while (_history.Count > 2 && _history[1].time < now - HistorySeconds)
            _history.RemoveAt(0);

        for (int i = 0; i < _trail.Length; i++)
        {
            bool visible = TrySample(now - (i + 1) * TrailSpacingSeconds, out Vector2 position);
            _trail[i].gameObject.SetActive(visible);
            if (visible)
                _trail[i].rectTransform.anchoredPosition = position;
        }
    }

    private bool TrySample(float time, out Vector2 position)
    {
        position = default;
        if (_history.Count == 0 || time < _history[0].time)
            return false;

        for (int i = _history.Count - 1; i > 0; i--)
        {
            var older = _history[i - 1];
            if (older.time > time)
                continue;
            var newer = _history[i];
            float span = newer.time - older.time;
            position = span > 0f ? Vector2.Lerp(older.position, newer.position, (time - older.time) / span) : newer.position;
            return true;
        }
        position = _history[0].position;
        return true;
    }
}

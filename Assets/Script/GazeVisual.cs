using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Replaces the gaze pointer's ring sprite with a solid dot plus a short fading trail.
public class GazeVisual : MonoBehaviour
{
    private const int TrailLength = 10;
    private const float TrailSpacingSeconds = 0.03f;
    private const float HistorySeconds = 0.5f;
    private static readonly Color DotColor = new Color32(0xE5, 0x39, 0x35, 0xFF);

    private readonly List<(float time, Vector2 position)> _history = new List<(float time, Vector2 position)>();
    private RectTransform _pointer;
    private Image[] _trail;

    public static void Attach(FollowGazePoint2D pointer)
    {
        var pointerRect = (RectTransform)pointer.transform;

        // Keep the pointer's rect and collider (they drive gaze-dwell hit testing); only hide its sprite.
        var originalImage = pointer.GetComponent<Image>();
        originalImage.color = Color.clear;
        originalImage.raycastTarget = false;

        var outline = UiFactory.CreateImage("GazeDotOutline", pointerRect, UiFactory.Circle, Color.white);
        UiFactory.Place(outline.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(48f, 48f));
        var dot = UiFactory.CreateImage("GazeDot", pointerRect, UiFactory.Circle, DotColor);
        UiFactory.Place(dot.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(38f, 38f));

        var container = UiFactory.CreateRect("GazeTrail", pointerRect.parent);
        UiFactory.Place(container, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.zero);
        container.SetSiblingIndex(pointerRect.GetSiblingIndex());

        var visual = container.gameObject.AddComponent<GazeVisual>();
        visual._pointer = pointerRect;
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

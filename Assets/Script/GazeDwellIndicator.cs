using UnityEngine;
using UnityEngine.UI;

// Progress ring around the gaze dot while a ButtonTrigger is being dwelled on.
// Completion feedback is deliberately neutral ("selected"), never "correct".
public class GazeDwellIndicator : MonoBehaviour
{
    // Gaze canvas reference width is 1920; on the robot's 1024-wide screen 150 units is about 80 px.
    private const float RingSize = 150f;
    private const float PopSeconds = 0.2f;
    private const float HoldSeconds = 0.3f;
    private const float FadeSeconds = 0.25f;
    // V3.1: the home's light guide outside the dial's ticks - a groove with a bright outer lip, then the green guide
    // as a soft spill, a body and a pale core.
    private static readonly Color ProgressColor = new Color32(0x56, 0xCF, 0x9F, 0xFF);
    private static readonly Color TrackColor = new Color32(0x1C, 0x22, 0x2C, 0x2E);

    private static GazeDwellIndicator _instance;

    private CanvasGroup _group;
    private Image[] _fill;
    private float _shown;
    private Object _owner;
    private bool _completing;
    private float _completeElapsed;

    public static void Attach(Transform pointer)
    {
        var root = UiFactory.CreateRect("GazeDwellIndicator", pointer);
        UiFactory.Place(root, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * RingSize);
        var indicator = root.gameObject.AddComponent<GazeDwellIndicator>();

        var visual = UiFactory.Stretch(UiFactory.CreateRect("Visual", root));
        indicator._group = visual.gameObject.AddComponent<CanvasGroup>();
        indicator._group.blocksRaycasts = false;
        indicator._group.interactable = false;

        Image Ring(string name, float radius, float half, Color color)
        {
            var image = UiFactory.CreateImage(name, visual, V3Setup.RingSprite(radius, half), color);
            UiFactory.Place(image.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * RingSize);
            return image;
        }
        Ring("Lip", 49.5f, 0.5f, new Color(1f, 1f, 1f, 0.9f));
        Ring("Track", 46f, 3.5f, TrackColor);
        indicator._fill = new[]
        {
            Ring("Spill", 46f, 5.5f, new Color(ProgressColor.r, ProgressColor.g, ProgressColor.b, 0.28f)),
            V3.Liquid(Ring("Guide", 46f, 3f, ProgressColor), true, -1f, RingSize),
            Ring("Core", 46f, 0.4f, new Color32(0xE9, 0xFF, 0xF2, 0xBF)),
        };
        foreach (var fill in indicator._fill)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
        }

        // sparks off the ring's head while the dwell runs (ring radius 46 of the 120-unit sprite)
        V3Sparks.Attach(visual, () =>
        {
            if (indicator._owner == null || indicator._shown <= 0.01f || indicator._shown >= 1f)
                return null;
            float r = RingSize * 46f / 120f, a = (90f - indicator._shown * 360f) * Mathf.Deg2Rad;
            return (new Vector2(RingSize / 2f + r * Mathf.Cos(a), RingSize / 2f - r * Mathf.Sin(a)), new Vector2(Mathf.Sin(a), Mathf.Cos(a)));
        });
        indicator.Hide();
        _instance = indicator;
    }

    // True while a gaze selection is filling (or its completion is playing).
    public static bool IsDwelling => _instance != null && _instance._owner != null;
    // True while the finish of a completed selection plays; the owner is already cleared then.
    public static bool IsCompleting => _instance != null && _instance._completing;

    public static void Report(Object owner, float progress)
    {
        if (_instance != null)
            _instance.ShowProgress(owner, progress);
    }

    public static void Release(Object owner)
    {
        if (_instance != null && !_instance._completing && _instance._owner == owner)
            _instance.Hide();
    }

    public static void Complete()
    {
        if (_instance != null)
            _instance.PlayComplete();
    }

    private void ShowProgress(Object owner, float progress)
    {
        // Let the completion feedback finish before showing a new dwell.
        if (_completing)
            return;
        _owner = owner;
        _group.gameObject.SetActive(true);
        _group.alpha = 1f;
        _group.transform.localScale = Vector3.one;
        _shown = Mathf.Clamp01(progress);
        foreach (var fill in _fill)
            fill.fillAmount = _shown;
    }

    private void PlayComplete()
    {
        _owner = null;
        _completing = true;
        _completeElapsed = 0f;
        _group.gameObject.SetActive(true);
        _group.alpha = 1f;
        foreach (var fill in _fill)
            fill.fillAmount = 1f;
    }

    private void Update()
    {
        if (!_completing)
            return;

        _completeElapsed += Time.unscaledDeltaTime;
        float t = _completeElapsed;
        float scale = t < PopSeconds * 0.5f
            ? Mathf.Lerp(1f, 1.12f, t / (PopSeconds * 0.5f))
            : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((t - PopSeconds * 0.5f) / (PopSeconds * 0.5f)));
        _group.transform.localScale = Vector3.one * scale;

        if (t > HoldSeconds)
            _group.alpha = 1f - Mathf.Clamp01((t - HoldSeconds) / FadeSeconds);
        if (t >= HoldSeconds + FadeSeconds)
            Hide();
    }

    private void Hide()
    {
        _owner = null;
        _completing = false;
        _group.transform.localScale = Vector3.one;
        _group.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}

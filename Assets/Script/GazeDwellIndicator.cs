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
    // V3: a mint arc outside the dial's ticks (r ~31 of the 80 px dial, ~5 px wide) over a faint track, so the
    // dwell reads clearly while choosing an answer.
    private static readonly Color ProgressColor = new Color32(0x7C, 0xF2, 0xA8, 0xFF);
    private static readonly Color SelectedColor = new Color32(0x7C, 0xF2, 0xA8, 0xFF);
    private static readonly Color TrackColor = new Color32(0x14, 0x14, 0x14, 0x24);

    private static GazeDwellIndicator _instance;

    private CanvasGroup _group;
    private Image _fill;
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

        var track = UiFactory.CreateImage("Track", visual, V3Setup.RingSprite(46f, 4f), TrackColor);
        UiFactory.Place(track.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * RingSize);
        indicator._fill = UiFactory.CreateImage("Fill", visual, V3Setup.RingSprite(46f, 4f), ProgressColor);
        UiFactory.Place(indicator._fill.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, Vector2.one * RingSize);
        indicator._fill.type = Image.Type.Filled;
        indicator._fill.fillMethod = Image.FillMethod.Radial360;
        indicator._fill.fillOrigin = (int)Image.Origin360.Top;
        indicator._fill.fillClockwise = true;

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
        _fill.color = ProgressColor;
        _fill.fillAmount = Mathf.Clamp01(progress);
    }

    private void PlayComplete()
    {
        _owner = null;
        _completing = true;
        _completeElapsed = 0f;
        _group.gameObject.SetActive(true);
        _group.alpha = 1f;
        _fill.color = SelectedColor;
        _fill.fillAmount = 1f;
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

using UnityEngine;
using UnityEngine.UI;

// Progress ring around the gaze dot while a ButtonTrigger is being dwelled on.
// Completion feedback is deliberately neutral ("selected"), never "correct".
public class GazeDwellIndicator : MonoBehaviour
{
    // Gaze canvas reference width is 1920; on the robot's 1024-wide screen 150 units is about 80 px.
    private const float RingSize = 150f;
    private const float PopSeconds = V3Motion.Quick;
    private const float HoldSeconds = V3Motion.Base;
    private const float FadeSeconds = V3Motion.Quick;
    // A ring-shaped glass tube of green liquid filling clockwise from the top in a white outline (V3LiquidRing), brighter
    // and calmer the stiller the eyes are, catching the light where the gaze drifts; the whole ring sinks a little as it
    // fills, as if pressed, and when the selection is made it locks - the ring of light running in to the centre - and
    // springs back with a click.
    private const float PressDepth = 0.06f;
    private static readonly Color TrackColor = V3.A(V3.Groove, 0.18f);

    private static GazeDwellIndicator _instance;

    private CanvasGroup _group;
    private V3LiquidRing _ring;
    private FollowGazePoint2D _pointer;
    private readonly V3GazeSteadiness _steadiness = new V3GazeSteadiness();
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

        // ring radius 46 of the 120-unit sprite, just outside the gaze dial's ticks
        indicator._ring = V3LiquidRing.Create(visual, RingSize, 46f, 4.2f, TrackColor);
        indicator._pointer = pointer.GetComponent<FollowGazePoint2D>();
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
        if (_owner != owner)
        {
            _steadiness.Clear();
            _ring.Unlock();
        }
        _owner = owner;
        _group.gameObject.SetActive(true);
        _group.alpha = 1f;
        _shown = Mathf.Clamp01(progress);
        _group.transform.localScale = Vector3.one * (1f - PressDepth * _shown);
        Light(_shown);
    }

    // The liquid up to the progress.
    private void Light(float progress)
    {
        _ring.Fill = progress;
    }

    private void PlayComplete()
    {
        _owner = null;
        _completing = true;
        _completeElapsed = 0f;
        _group.gameObject.SetActive(true);
        _group.alpha = 1f;
        Light(1f);
        _ring.Lock();
        KebbiSound.Play("ui_dwell_done", 0.5f);
    }

    private void Update()
    {
        // the eyes' steadiness while a selection fills
        if (_pointer != null && _owner != null)
        {
            _steadiness.Add(_pointer.DisplayedScreenPosition);
            _ring.Steady = _steadiness.Steady;
            _ring.Lean = _steadiness.Lean;
        }
        if (!_completing)
            return;

        _completeElapsed += Time.unscaledDeltaTime;
        float t = _completeElapsed;
        // springs back from pressed in, a little past its size, and settles
        float scale = t < PopSeconds * 0.5f
            ? Mathf.Lerp(1f - PressDepth, 1.1f, t / (PopSeconds * 0.5f))
            : Mathf.Lerp(1.1f, 1f, Mathf.Clamp01((t - PopSeconds * 0.5f) / (PopSeconds * 0.5f)));
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

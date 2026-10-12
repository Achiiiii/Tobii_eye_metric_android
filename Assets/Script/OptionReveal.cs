using UnityEngine;

// Each new symbol appears alone; the four options come in once the user has looked at it, so the
// gaze correction gets a clean look at the centre on nearly every trial (with the options showing,
// a quarter of trials were lost to fixations that sat nearer an option).
// The wait counts from when the robot's head is still - HeadFollow may correct just after an
// answer, and gaze while the screen moves is useless - with an overall cap.
public class OptionReveal : MonoBehaviour
{
    private const float WaitAfterStillSeconds = 1.2f;
    private const float MaxWaitSeconds = 2f;

    private MetricTest _metricTest;
    private DriftCorrector _drift;
    private HeadFollow _head;
    private FollowGazePoint2D _pointer;

    private bool _waiting;
    private float _shownAt;
    private float _stillSince = -1f;

    public static OptionReveal Create(Transform parent, MetricTest metricTest, DriftCorrector drift, HeadFollow head, FollowGazePoint2D pointer)
    {
        var go = new GameObject("OptionReveal");
        go.transform.SetParent(parent, false);
        // Inactive until wired: AddComponent would otherwise run OnEnable with null references.
        go.SetActive(false);
        var reveal = go.AddComponent<OptionReveal>();
        reveal._metricTest = metricTest;
        reveal._drift = drift;
        reveal._head = head;
        reveal._pointer = pointer;
        go.SetActive(true);
        return reveal;
    }

    private void OnEnable()
    {
        _metricTest.SymbolShown += OnSymbolShown;
    }

    private void OnDisable()
    {
        _metricTest.SymbolShown -= OnSymbolShown;
    }

    private void OnSymbolShown()
    {
        GazeSelectionGate.Open();
        _waiting = true;
        _shownAt = Time.unscaledTime;
        _stillSince = -1f;
    }

    private void Update()
    {
        if (!_waiting)
            return;
        if (!_metricTest.gameObject.activeInHierarchy)
        {
            _waiting = false;
            GazeSelectionGate.Open();
            return;
        }
        // Nothing to learn from with the correction off: show the options straight away.
        if (!_drift.Enabled)
        {
            Reveal("correction off");
            return;
        }
        // While the user is walked back into place answering is paused anyway.
        if (PositionGuide.Active)
            return;

        float now = Time.unscaledTime;
        if (_head.IsMoving)
            _stillSince = -1f;
        else if (_stillSince < 0f)
            _stillSince = now;

        if (_drift.CentreLookFound)
        {
            // The user is looking at the symbol right now: an answer must start with the gaze
            // moving away from it (GazeSelectionGate).
            GazeSelectionGate.Close(_pointer);
            Reveal("looked at the symbol");
        }
        else if (_stillSince >= 0f && now - _stillSince >= WaitAfterStillSeconds)
            Reveal("waited");
        else if (now - _shownAt >= MaxWaitSeconds)
            Reveal("cap");
    }

    private void Reveal(string reason)
    {
        _waiting = false;
        _metricTest.SetOptionsVisible(true);
        Debug.Log($"[REVEAL] options after {Time.unscaledTime - _shownAt:0.00} s ({reason})");
    }
}

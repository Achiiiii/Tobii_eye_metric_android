using System.Collections;
using UnityEngine;

// Temporary experiment: which focus distance gives the sharpest eyes? The camera locked its focus
// at 28 cm when the app started and never refocused, while the user sat at about 44 cm. When a
// head distance check starts (the user is seated at test distance), this holds the lens at each
// distance below, saves a frame for each (FrameCapture), then returns to autofocus - which now
// focuses on the seated user.
public class FocusSweepProbe : MonoBehaviour
{
    private static readonly int[] DistancesCm = { 25, 33, 42, 50, 60 };
    private const float StartDelaySeconds = 2f;
    private const float SettleSeconds = 1.2f;

    private DetectDistance _detectDistance;
    private AndroidWebcamCaptureClient _camera;
    private bool _wasChecking;
    private bool _sweeping;

    public static FocusSweepProbe Create(Transform parent, DetectDistance detectDistance, AndroidWebcamCaptureClient camera)
    {
        FrameCapture.Init();
        var go = new GameObject("FocusSweepProbe");
        go.transform.SetParent(parent, false);
        var probe = go.AddComponent<FocusSweepProbe>();
        probe._detectDistance = detectDistance;
        probe._camera = camera;
        return probe;
    }

    private void Update()
    {
        bool checking = _detectDistance.IsChecking;
        if (checking && !_wasChecking && !_sweeping && _camera != null)
            StartCoroutine(Sweep());
        _wasChecking = checking;
    }

    private IEnumerator Sweep()
    {
        _sweeping = true;
        yield return new WaitForSecondsRealtime(StartDelaySeconds);
        Debug.Log("[FOCUS] sweep start");
        foreach (int cm in DistancesCm)
        {
            if (!_camera.SetManualFocus(100f / cm))
            {
                Debug.Log("[FOCUS] manual focus not available; sweep stopped");
                break;
            }
            yield return new WaitForSecondsRealtime(SettleSeconds);
            FrameCapture.Request($"focus{cm}cm");
            yield return new WaitForSecondsRealtime(0.3f);
        }
        _camera.RestoreAutoFocus();
        yield return new WaitForSecondsRealtime(2.5f);
        FrameCapture.Request("autofocus");
        Debug.Log("[FOCUS] sweep done");
        _sweeping = false;
    }
}

using System.Collections;
using UnityEngine;

// Temporary experiment: where is this lens sharp for a seated user, and can autofocus find it?
// Left alone, autofocus locked at the wrong distance (it picked the ceiling light or background),
// and a first sweep from 1.7 to 4.0 diopters came out uniformly blurry. When a head distance check
// starts, this waits for the robot's head to settle, then:
// 1. holds the lens at 2.5-8.0 diopters in 0.5 steps and saves a frame at each (the scale is
//    uncalibrated on this camera, so the values are only relative positions);
// 2. runs one autofocus pass limited to the face area and saves a frame 3 s and 8 s later.
public class FocusSweepProbe : MonoBehaviour
{
    private const float StartDelaySeconds = 4f;
    private const float SettleSeconds = 1.5f;
    private const float FirstDiopter = 2.5f;
    private const float LastDiopter = 8f;
    private const float StepDiopter = 0.5f;
    // Where the face sits in the frame at the test seat (normalised, y down).
    private static readonly Rect FaceRegion = Rect.MinMaxRect(0.35f, 0.45f, 0.65f, 0.85f);

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
        for (float d = FirstDiopter; d <= LastDiopter + 0.01f; d += StepDiopter)
        {
            if (!_camera.SetManualFocus(d))
            {
                Debug.Log("[FOCUS] manual focus not available; sweep stopped");
                break;
            }
            yield return new WaitForSecondsRealtime(SettleSeconds);
            FrameCapture.Request($"d{d:0.0}");
            yield return new WaitForSecondsRealtime(0.3f);
        }

        Debug.Log("[FOCUS] face-region autofocus");
        _camera.TriggerRegionAutoFocus(FaceRegion);
        yield return new WaitForSecondsRealtime(3f);
        FrameCapture.Request("faceaf3s");
        yield return new WaitForSecondsRealtime(5f);
        FrameCapture.Request("faceaf8s");
        Debug.Log("[FOCUS] sweep done; the lens stays where the face-region autofocus locked");
        _sweeping = false;
    }
}

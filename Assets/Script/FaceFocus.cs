using System.Collections;
using UnityEngine;

// Focuses the camera on the user's face at each head distance check and keeps it there.
// Left to itself the camera's autofocus locked wherever it happened to look when the app started
// (17-28 cm on its uncalibrated scale, the ceiling light or background) and stayed there, so the
// eyes Tobii worked from were blurred. A focus sweep on the robot showed the face sharp only around
// 5.5-6.5 on that scale - a range that may differ between robots - and one autofocus pass limited
// to the face found it (5.96) and held. So: once the user is seated for the check and the robot's
// head has stopped turning, run that pass, then leave the lens alone for the session.
public class FaceFocus : MonoBehaviour
{
    // Where the face sits in the frame once HeadFollow has centred the user (normalised, y down).
    private static readonly Rect FaceRegion = Rect.MinMaxRect(0.35f, 0.45f, 0.65f, 0.85f);
    private const float MinDelaySeconds = 2f;
    private const float StillSeconds = 0.5f;
    private const float GiveUpSeconds = 15f;

    private DetectDistance _detectDistance;
    private HeadFollow _head;
    private AndroidWebcamCaptureClient _camera;
    private bool _wasChecking;
    private Coroutine _running;

    public static FaceFocus Create(Transform parent, DetectDistance detectDistance, HeadFollow head, AndroidWebcamCaptureClient camera)
    {
        var go = new GameObject("FaceFocus");
        go.transform.SetParent(parent, false);
        var focus = go.AddComponent<FaceFocus>();
        focus._detectDistance = detectDistance;
        focus._head = head;
        focus._camera = camera;
        return focus;
    }

    private void Update()
    {
        bool checking = _detectDistance.IsChecking;
        if (checking && !_wasChecking && _camera != null)
        {
            if (_running != null)
                StopCoroutine(_running);
            _running = StartCoroutine(FocusOnFace());
        }
        _wasChecking = checking;
    }

    private IEnumerator FocusOnFace()
    {
        float start = Time.unscaledTime;
        float stillSince = -1f;
        while (true)
        {
            float now = Time.unscaledTime;
            if (now - start > GiveUpSeconds)
            {
                Debug.Log("[FOCUS] no settled face within 15 s; focus left as it was");
                _running = null;
                yield break;
            }
            bool ready = _head.HasFace && !_head.IsMoving;
            if (!ready)
                stillSince = -1f;
            else if (stillSince < 0f)
                stillSince = now;
            if (stillSince >= 0f && now - stillSince >= StillSeconds && now - start >= MinDelaySeconds)
                break;
            yield return null;
        }

        bool ok = _camera.TriggerRegionAutoFocus(FaceRegion);
        // The lens position it locks at shows in the camera plugin's [CAM] log.
        Debug.Log($"[FOCUS] face autofocus {(ok ? "triggered" : "not available")} {Time.unscaledTime - start:0.0} s into the head check");
        _running = null;
    }
}

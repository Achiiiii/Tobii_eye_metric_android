using UnityEngine;

// Temporary experiment: does the camera FOV handed to Tobii change the gaze accuracy? V2.3.5 passes
// the horizontal FOV (69.9 deg on the Kebbi) instead of the diagonal one (82.0 deg); one session on
// another robot showed the left/right error flip from overshooting to undershooting. Sessions
// alternate - odd: horizontal, even: diagonal - so both run on the same robot, person and seat.
// The switch happens when a head distance check starts, before that session's calibration.
public class FovABProbe : MonoBehaviour
{
    private DetectDistance _detectDistance;
    private StreamEngineDevice _device;
    private bool _wasChecking;
    private int _session;

    public static FovABProbe Create(Transform parent, DetectDistance detectDistance)
    {
        var go = new GameObject("FovABProbe");
        go.transform.SetParent(parent, false);
        var probe = go.AddComponent<FovABProbe>();
        probe._detectDistance = detectDistance;
        probe._device = FindObjectOfType<StreamEngineDevice>();
        return probe;
    }

    private void Update()
    {
        bool checking = _detectDistance.IsChecking;
        if (checking && !_wasChecking)
            StartSession();
        _wasChecking = checking;
    }

    private void StartSession()
    {
        if (_device == null || _device.CameraReportedFov <= 0f)
        {
            Debug.Log("[FOVAB] camera FOV not known yet; this session keeps the current FOV");
            return;
        }
        _session++;
        bool horizontal = _session % 2 == 1;
        float fov = horizontal ? _device.CameraReportedFov : DiagonalFov(_device.CameraReportedFov, _device.FrameSize);
        _device.RequestFov(fov);
        Debug.Log($"[FOVAB] session {_session}: FOV {fov:0.0} ({(horizontal ? "horizontal" : "diagonal")})");
    }

    // The diagonal FOV of the same frame, from its horizontal FOV and aspect ratio.
    private static float DiagonalFov(float horizontalFov, Vector2 frameSize)
    {
        float aspect = frameSize.x > 0f && frameSize.y > 0f ? frameSize.y / frameSize.x : 780f / 1050f;
        float halfTan = Mathf.Tan(horizontalFov * 0.5f * Mathf.Deg2Rad) * Mathf.Sqrt(1f + aspect * aspect);
        return 2f * Mathf.Atan(halfTan) * Mathf.Rad2Deg;
    }
}

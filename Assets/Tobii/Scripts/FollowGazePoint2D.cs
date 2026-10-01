/*
  COPYRIGHT 2025 - PROPERTY OF TOBII AB
  -------------------------------------
  2025 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

  NOTICE:  All information contained herein is, and remains, the property of Tobii AB and its suppliers, if any.
  The intellectual and technical concepts contained herein are proprietary to Tobii AB and its suppliers and may be
  covered by U.S.and Foreign Patents, patent applications, and are protected by trade secret or copyright law.
  Dissemination of this information or reproduction of this material is strictly forbidden unless prior written
  permission is obtained from Tobii AB.
*/

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DG.Tweening;

public class FollowGazePoint2D : MonoBehaviour
{
    private Vector2 _normalisedGazepoint = new Vector2(0.5f, 0.5f);
    private RectTransform _rectTransform;
    private Canvas _canvas;
    private RectTransform _canvasRect;

    public bool useFiltering = true;

    // Webcam gaze is noisy: hold the dot steady within a fixation and move only on real saccades.
    // Starting points for tuning on the device via GazeDebugOverlay.
    // 0.05 is about 51 px on the robot. At 1050x780 the scatter within a fixation measured 13 px
    // RMS, so the old 0.08 (82 px) held back medium-sized moves for no benefit.
    [SerializeField] private float fixationRadiusScreenFraction = 0.05f;
    [SerializeField] private float fixationWindowSeconds = 0.5f;
    [SerializeField] private float displaySmoothingSeconds = 0.05f;
    // Jumps beyond this many fixation radii move the dot at once (the overlay's radius buttons
    // therefore tune both).
    private const float ImmediateJumpRadii = 2f;

    // Measures how long the dot takes to reach a large gaze jump (smoothing and easing only;
    // camera and inference latency come before the sample arrives).
    private const float CatchUpArrivePixels = 40f;
    private const float CatchUpGiveUpSeconds = 1.5f;
    private float _catchUpStart = -1f;
    private Vector2 _catchUpTarget;
    private readonly FixationSmoother _smoother = new FixationSmoother();

    // Maps a raw screen-space gaze sample to the position to show. DriftCorrector supplies it,
    // learning a position-dependent correction during the test.
    public System.Func<Vector2, Vector2> Correction { get; set; }
    // Each raw gaze sample in screen pixels, before any correction.
    public event System.Action<Vector2> RawSampleAdded;
    // Where the dot is drawn, in screen pixels (before clamping to the screen edge).
    public Vector2 DisplayedScreenPosition => _displayedScreenPosition;
    private Vector2 _displayedScreenPosition;
    private bool _hasDisplayedPosition = false;

    public float FixationRadiusScreenFraction
    {
        get => fixationRadiusScreenFraction;
        set => fixationRadiusScreenFraction = Mathf.Clamp(value, 0.005f, 0.5f);
    }

    public float FixationWindowSeconds
    {
        get => fixationWindowSeconds;
        set => fixationWindowSeconds = Mathf.Clamp(value, 0.05f, 2f);
    }

    // Optional: Add clamping and padding
    public bool clampToScreen = true;
    public float edgePadding = 20f;
    [SerializeField] private UILineRenderer uILineRenderer; // UI canvas（需設為 Screen Space - Overlay）
    public Transform headGO;
    public UnityEngine.UI.Image gazeDotImage;

    [System.Serializable]
    public class GazeData
    {
        public float time;
        public Vector3 position;
    }
    [System.Serializable]
    private class GazeDataWrapper
    {
        public List<GazeData> gazeDataList;
    }
    private bool _isRecording = false;
    private List<GazeData> recordedData = new List<GazeData>();
    private bool _lineActive = false;

    void Start()
    {
        // Get required components
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _canvasRect = _canvas.GetComponent<RectTransform>();

        if (_rectTransform == null)
        {
            Debug.LogError("No RectTransform found on this object!");
            enabled = false;
            return;
        }

        if (_canvas == null)
        {
            Debug.LogError("No Canvas found in parents!");
            enabled = false;
            return;
        }

        // Set the anchors to the center
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    void LateUpdate()
    {
        Vector2 rawScreenPosition = Corrected(ToScreen(_normalisedGazepoint));
        Vector2 target = useFiltering && _smoother.HasOutput ? _smoother.Output : rawScreenPosition;

        if (!_hasDisplayedPosition)
        {
            _displayedScreenPosition = target;
            _hasDisplayedPosition = true;
        }
        else
        {
            // Short easing so jumps between fixations look like movement rather than teleporting.
            _displayedScreenPosition = Vector2.Lerp(_displayedScreenPosition, target, 1f - Mathf.Exp(-Time.deltaTime / displaySmoothingSeconds));
        }

        Vector2 screenPosition = _displayedScreenPosition;
        if (_catchUpStart >= 0f)
        {
            if (Vector2.Distance(screenPosition, _catchUpTarget) < CatchUpArrivePixels)
            {
                GazeLatencyStats.RecordCatchUp(Time.time - _catchUpStart);
                _catchUpStart = -1f;
            }
            else if (Time.time - _catchUpStart > CatchUpGiveUpSeconds)
            {
                // The jump never settled (a spike, or the gaze moved on): not a latency sample.
                _catchUpStart = -1f;
            }
        }
        GazeLatencyStats.RecordFilterLag(Vector2.Distance(rawScreenPosition, screenPosition));

        // Convert screen position to canvas position
        Vector2 canvasPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            screenPosition,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out canvasPosition
        );

        if (clampToScreen)
        {
            // Get image dimensions
            float halfWidth = _rectTransform.rect.width * 0.5f;
            float halfHeight = _rectTransform.rect.height * 0.5f;

            // Get canvas dimensions
            Vector2 canvasSize = _canvasRect.rect.size;
            float halfCanvasWidth = canvasSize.x * 0.5f;
            float halfCanvasHeight = canvasSize.y * 0.5f;

            // Clamp coordinates to keep the image within the canvas
            canvasPosition.x = Mathf.Clamp(canvasPosition.x,
                -halfCanvasWidth + halfWidth + edgePadding,
                halfCanvasWidth - halfWidth - edgePadding);
            canvasPosition.y = Mathf.Clamp(canvasPosition.y,
                -halfCanvasHeight + halfHeight + edgePadding,
                halfCanvasHeight - halfHeight - edgePadding);
        }

        // Set the position
        _rectTransform.anchoredPosition = canvasPosition;

        if (_isRecording)
        {
            Vector3 newPos = new Vector3(canvasPosition.x, canvasPosition.y, headGO.position.z);
            recordedData.Add(new GazeData
            {
                time = Time.time,
                position = newPos
            });
        }
        if (_lineActive)
        {
            uILineRenderer.points.Add(canvasPosition);
            uILineRenderer.SetVerticesDirty();
            if (uILineRenderer.points.Count > 40)
            {
                uILineRenderer.points.RemoveAt(0);
            }
        }
    }

    void OnDisable()
    {
        // The pointer is hidden between sessions; start from fresh samples when it shows again.
        _smoother.Reset();
        _hasDisplayedPosition = false;
    }

    public void OnGazePoint(Vector2 normalizedGazePoint)
    {
        _normalisedGazepoint = normalizedGazePoint;
        _smoother.FixationRadius = fixationRadiusScreenFraction * Screen.width;
        _smoother.FixationWindowSeconds = fixationWindowSeconds;
        _smoother.ImmediateJump = ImmediateJumpRadii * _smoother.FixationRadius;
        Vector2 raw = ToScreen(normalizedGazePoint);
        RawSampleAdded?.Invoke(raw);
        Vector2 sample = Corrected(raw);
        if (_hasDisplayedPosition && _catchUpStart < 0f
            && Vector2.Distance(sample, _displayedScreenPosition) > ImmediateJumpRadii * fixationRadiusScreenFraction * Screen.width)
        {
            _catchUpStart = Time.time;
            _catchUpTarget = sample;
        }
        _smoother.AddSample(Time.time, sample);
        GazeLatencyStats.RecordFixationSpread(_smoother.Spread);
    }

    private Vector2 Corrected(Vector2 raw)
    {
        return Correction != null ? Correction(raw) : raw;
    }

    private static Vector2 ToScreen(Vector2 normalizedGazePoint)
    {
        // Normalized gaze has its origin at the top-left; screen space at the bottom-left.
        return new Vector2(normalizedGazePoint.x * Screen.width, (1 - normalizedGazePoint.y) * Screen.height);
    }

    public void OnToggleFiltering(bool value)
    {
        useFiltering = value;
    }
    public void StartRecord()
    {
        recordedData.Clear();
        _isRecording = true;
    }

    public void StopRecord(string filename, bool needSave = true)
    {
        _isRecording = false;
        if (needSave)
        {
            string json = JsonUtility.ToJson(new GazeDataWrapper { gazeDataList = recordedData }, true);

            string filePath = Path.Combine(Application.persistentDataPath, "gaze_data_" + filename + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(filePath, json);

            Debug.Log("Gaze recording saved to: " + filePath);
        }
    }
    public void ToggleLineActive(bool value)
    {
        _lineActive = value;
        if (!value)
        {
            uILineRenderer.points.Clear();
            uILineRenderer.SetVerticesDirty();
        }
    }
    public void ToggleGazeDot(bool value)
    {
        if (value)
        {
            gazeDotImage.DOFade(1, 0);
        }
        else
        {
            gazeDotImage.DOFade(0, 0);
        }
    }
}

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

using UnityEngine;
using DG.Tweening;

public class FollowGazePoint2D : MonoBehaviour
{
    private Vector2 _normalisedGazepoint = new Vector2(0.5f, 0.5f);
    private RectTransform _rectTransform;
    private Canvas _canvas;
    private RectTransform _canvasRect;

    public bool useFiltering = true;
    private OneEuroFilter _filter = new OneEuroFilter();

    // Adjusted filter parameters for more smoothing
    private const float BETA = 0.003f;
    private const float MIN_CUTOFF = 0.015f;
    private const float D_CUTOFF = 1.0f;

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

        // Set basic default 1� filter values using constants
        _filter.Beta = BETA;
        _filter.MinCutoff = MIN_CUTOFF;
        _filter.DCutoff = D_CUTOFF;

        // Set the anchors to the center
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    void LateUpdate()
    {
        // Get screen dimensions
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // Convert normalized coordinates to screen space and invert Y
        Vector2 screenPosition = new Vector2(
            _normalisedGazepoint.x * screenWidth,
            (1 - _normalisedGazepoint.y) * screenHeight
        );

        if (useFiltering)
        {
            screenPosition = _filter.Step(Time.time, screenPosition);
        }

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

    public void OnGazePoint(Vector2 normalizedGazePoint)
    {
        _normalisedGazepoint = normalizedGazePoint;
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
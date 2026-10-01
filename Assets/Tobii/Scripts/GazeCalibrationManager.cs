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

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tobii
{
    public class GazeCalibrationManager : MonoBehaviour
    {
        /// <summary>
        /// Struct for positioning the stimuli point in screen space
        /// </summary>
        [Serializable]
        public struct StimuliPosition
        {
            /// <summary>
            /// Normalized XY stimuli position from bottom left of screen, so (0,0) is bottom left and (1,1) is top right
            /// </summary>
            [SerializeField]
            [Tooltip("Normalized XY stimuli position from bottom left of screen.")]
            public Vector2 screenPos;
        }

        /// <summary>
        /// Structure for a sequence or calibration points.
        /// </summary>
        [Serializable]
        public struct CalibrationSequence
        {
            // --- PREVIOUS FIELDS (KEPT AS COMMENT ONLY TO PRESERVE ORIGINAL DOCUMENTATION / HISTORY) ---
            // Normalized starting size of gaze region (BoxCollider) around stimuli points where 1 is display width.
            // public float gazeColliderStartSize;
            // Normalized end size of gaze region (BoxCollider) around stimuli points where 1 is display width.
            // public float gazeColliderEndSize;
            // Gaze collider grow time from start size to end size in seconds.
            // public float gazeColliderGrowTime;

            /// <summary>
            /// Normalized FIXED size of gaze region (BoxCollider) around stimuli points (x = fraction of screen width, y = fraction of screen height).
            /// </summary>
            [SerializeField]
            [Tooltip("Normalized FIXED size of gaze region (BoxCollider) around stimuli points (x = fraction of screen width, y = fraction of screen height).")]
            public Vector2 gazeColliderSize;

            /// <summary>
            /// Sequece of calibration point locations
            /// </summary>
            [SerializeField]
            [Tooltip("Sequential stimuli points.")]
            public StimuliPosition[] stimuliPoints;
        }

        /// <summary>
        /// Indicates the state of the calibraion component
        /// </summary>
        public enum ComponentState
        {
            /// <summary>
            /// Idle
            /// </summary>
            Idle = 0,
            /// <summary>
            /// The component is being stopped and is in the process of freeing up resources
            /// </summary>
            Stopping,
            /// <summary>
            /// Calibration procedure is running
            /// </summary>
            CalibrationRunning,
            /// <summary>
            /// Internal error indicates major issues with the eye tracking device
            /// </summary>
            InternalError
        };

        /// <summary>
        /// Indicates the result of the calibration script
        /// </summary>
        public enum CalibrationState
        {
            /// <summary>
            /// Calibration is ongoing or is not started
            /// </summary>
            CalibrationNotDone = 0,
            /// <summary>
            /// Calibration has been completed successfully
            /// </summary>
            CalibrationSuccess,
            /// <summary>
            /// Calibration has finished but did not complete successfully
            /// </summary>
            CalibrationFail
        };

        /// <summary>
        /// Provides access to gaze data etc from StreamEngineDevice.
        /// </summary>
        [SerializeField]
        private StreamEngineDevice streamEngineDevice;

        /// <summary>
        /// Provides access to calibration routines from StreamEngineDevice.
        /// </summary>
        public StreamEngineCalibration streamEngineCalibration;

        /// <summary>
        /// Prefab based on StimuliPoint component.
        /// </summary>
        [SerializeField]
        private GameObject stimuliPrefab;

        /// <summary>
        /// Group of stimuli points separated by a call to Compute and Apply.
        /// </summary>
        [SerializeField]
        [Tooltip("Group of stimuli points separated by a call to Compute and Apply.")]
        private CalibrationSequence[] calibrationSequence;

        public CalibrationState CalibrationStatus { get; private set; }
        public ComponentState ComponentStatus { get; private set; }

        /// <summary>
        /// Keep a list of calibation objects to destroy if close is pressed early.
        /// </summary>
        private List<GameObject> calibrationPoints;

        /// <summary>
        /// Keep a count to test if sequence is complete.
        /// </summary>
        private int completeCount = 0;

        /// <summary>
        /// GameObjects to hide during calibration. These objects are hidden during and shown after calibration in case you need to clear the UI.
        /// </summary>
        [SerializeField]
        [Tooltip("GameObjects to hide during calibration.")]
        private GameObject[] hideTheseDuringCalibration;


        /// <summary>
        /// Get <see cref="TrackBoxGuide"/> instance. This is assigned
        /// in Awake(), so call earliest in Start().
        /// </summary>
        public AudioSource audioSource;
        public AudioClip questionAudio;

        [SerializeField] private GameObject pointer;
        [SerializeField] private TMPro.TMP_Text content;
        [SerializeField] private GameObject blackTestBtn;
        [SerializeField] private GameObject colorTestBtn;
        [SerializeField] private GameObject mainCanvas;
        [SerializeField] private MetricTest metricTest;
        [SerializeField] private Sprite[] sampleSprites;
        [SerializeField] private Image sampleImage;

        public event Action CalibrationStarted;
        /// <summary>
        /// (1-based index within the sequence, points in the sequence, stimulus position in screen pixels)
        /// </summary>
        public event Action<int, int, Vector2> StimulusShown;
        public event Action StimulusCleared;
        public event Action CalibrationEnded;
        public event Action CalibrationFailed;
        public event Action GazeIntroStarted;
        public event Action GazeIntroEnded;
        /// <summary>
        /// (side: "right" / "left" / "both", countdown seconds) fired before each test round starts.
        /// </summary>
        public event Action<string, float> TestCountdownStarted;
        public event Action TestCountdownEnded;

        private float _countDownTime = 5;
        private bool _countDownLocker = false;
        private bool _isCalibrating = false;
        private bool _objectsHidden = false;
        private bool[] _activeBeforeCalibration;
        private bool _deviceCalibrationActive = false;
        private bool _stopPending = false;
        private bool _gazeIntroShown = false;
        private bool _gazeIntroConfirmed = false;
        private string _currentSide = "right";
        private const float TestCountdownSeconds = 3f;

        private void Awake()
        {
            CalibrationStatus = CalibrationState.CalibrationNotDone;
            calibrationPoints = new List<GameObject>();
            LayoutTrialInstructions();
        }

        // The original layout overlapped the illustration and pushed the instructions off the bottom of the screen.
        private void LayoutTrialInstructions()
        {
            var imageRect = sampleImage.rectTransform;
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = new Vector2(-250f, -20f);
            imageRect.sizeDelta = new Vector2(240f, 360f);
            sampleImage.preserveAspect = true;

            var textRect = content.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(150f, -10f);
            textRect.sizeDelta = new Vector2(520f, 300f);
            content.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            content.enableWordWrapping = true;
            content.fontSize = 26f;
            content.paragraphSpacing = 18f;
            content.color = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
            content.fontSharedMaterial = UiFactory.PlainMaterial(content.font);
        }

        void Update()
        {
            if (_countDownLocker)
            {
                _countDownTime -= Time.deltaTime;
                // content.text = $"眼部校正將在 {Mathf.Max(Mathf.CeilToInt(_countDownTime), 0)} 秒後開始\n稍後請將視線跟隨<color=blue>藍色圓點";
                if (_countDownTime < 0)
                {
                    _countDownLocker = false;
                    mainCanvas.SetActive(false);

                    StartCalibration();
                }
            }
        }

        public void NextButton(){
            mainCanvas.SetActive(false);
            StartCalibration();
        }

        /// <summary>
        /// Called once the head position has been confirmed by DetectDistance.
        /// </summary>
        public void BeginFirstTrial()
        {
            SetTrialCountDown(metricTest.FirstSide, true);
        }

        /// <summary>
        /// Called when the user selects "continue" on the gaze intro screen.
        /// </summary>
        public void ConfirmGazeIntro()
        {
            _gazeIntroConfirmed = true;
        }

        public void SetTrialCountDown(string side, bool headPositionConfirmed = false)
        {
            // _countDownTime = 5;
            // _countDownLocker = true;
            metricTest.gameObject.SetActive(false);
            mainCanvas.SetActive(true);
            _currentSide = side;
            string coverHint;
            switch (side)
            {
                case "right":
                    sampleImage.sprite = sampleSprites[0];
                    coverHint = "請先遮擋左邊眼睛，使用右眼檢測";
                    break;
                case "left":
                    sampleImage.sprite = sampleSprites[1];
                    coverHint = "請遮擋右邊眼睛，使用左眼檢測";
                    break;
                case "both":
                    sampleImage.sprite = sampleSprites[2];
                    coverHint = "請勿遮擋眼睛，直接進行檢測";
                    break;
                default:
                    sampleImage.sprite = sampleSprites[0];
                    coverHint = "";
                    break;
            }
            var lines = new List<string>();
            if (coverHint != "")
                lines.Add("<size=32><b>" + coverHint + "</b></size>");
            lines.Add("頭部請保持不動，稍後請依序注視<color=#1E88E5>藍色圓點</color>");
            lines.Add("準備好後，請按右下角的繼續按鈕");
            content.text = string.Join("\n", lines);
            string headHint = headPositionConfirmed ? "接下來請保持頭部不動。" : "請保持頭部不動。";
            PlayTTS(headHint + coverHint);
        }

        public void StartCalibration()
        {
            if (_isCalibrating)
                return;
            _isCalibrating = true;
            StartCoroutine(Calibrate());
        }

        /// <summary>
        /// Abort any calibration or gaze intro in progress and return to a clean state (used by recalibration).
        /// </summary>
        public void ResetSession()
        {
            StopAllCoroutines();
            _stopPending = false;
            _countDownLocker = false;

            foreach (var point in calibrationPoints)
            {
                if (point != null)
                    Destroy(point);
            }
            calibrationPoints.Clear();

            if (_objectsHidden)
            {
                for (int i = 0; i < hideTheseDuringCalibration.Length; i++)
                    hideTheseDuringCalibration[i].SetActive(_activeBeforeCalibration[i]);
                _objectsHidden = false;
            }

            _isCalibrating = false;
            _gazeIntroShown = false;
            ComponentStatus = ComponentState.Idle;
            CalibrationStatus = CalibrationState.CalibrationNotDone;

            if (_deviceCalibrationActive)
                StartCoroutine(StopDeviceCalibration());
        }

        private void PlayTTS(string text)
        {
            if (text == "")
                return;
            Nuwa.stopTTS();
            Nuwa.startTTS(text);
        }

        public IEnumerator Calibrate()
        {
            _isCalibrating = true;
            while (_stopPending)
                yield return null;

            HideObjectsForCalibration();
            CalibrationStarted?.Invoke();

            ComponentStatus = ComponentState.CalibrationRunning;
            var success = new ReferenceBool(false);

            if (streamEngineDevice.IsConnected == false)
            {
                FailCalibration();
                yield break;
            }

            yield return streamEngineCalibration.StartCalibrationRoutine(success);
            if (success == false)
            {
                FailCalibration();
                yield break;
            }
            _deviceCalibrationActive = true;

            yield return streamEngineCalibration.ClearCalibrationRoutine(success);
            if (success == false)
            {
                FailCalibration();
                yield break;
            }

            foreach (var sequence in calibrationSequence)
            {
                // Show one point at a time, in a fixed order, so the user can be guided through them.
                var points = OrderForGuidance(sequence.stimuliPoints);
                for (int i = 0; i < points.Length; i++)
                {
                    completeCount = 0;
                    var screenPosition = AddStimulus(sequence.gazeColliderSize, points[i].screenPos);
                    StimulusShown?.Invoke(i + 1, points.Length, screenPosition);
                    yield return new WaitUntil(() => completeCount >= 1);
                    StimulusCleared?.Invoke();
                    yield return new WaitForSeconds(0.3f);
                }

                // Compute and apply
                yield return commit(success);
                CalibrationStatus = success == true ? CalibrationState.CalibrationSuccess : CalibrationState.CalibrationFail;
                ComponentStatus = ComponentState.Idle;

                // Don't rush into next sequence
                yield return new WaitForSeconds(1.5f);
            }

            yield return streamEngineCalibration.StopCalibrationRoutine(success);
            if (success == false)
            {
                FailCalibration();
                yield break;
            }
            _deviceCalibrationActive = false;

            ComponentStatus = ComponentState.Idle;

            foreach (var hideThisDuringCalibration in hideTheseDuringCalibration)
                hideThisDuringCalibration.SetActive(true);
            _objectsHidden = false;
            _isCalibrating = false;
            CalibrationEnded?.Invoke();

            mainCanvas.SetActive(true);

            Debug.Log("Calibration was successful");
            pointer.SetActive(true);

            if (!_gazeIntroShown)
            {
                _gazeIntroShown = true;
                _gazeIntroConfirmed = false;
                GazeIntroStarted?.Invoke();
                yield return new WaitUntil(() => _gazeIntroConfirmed);
                GazeIntroEnded?.Invoke();
            }

            TestCountdownStarted?.Invoke(_currentSide, TestCountdownSeconds);
            yield return new WaitForSeconds(TestCountdownSeconds);
            TestCountdownEnded?.Invoke();

            metricTest.gameObject.SetActive(true);
            metricTest.StartMeticTest();
            // blackTestBtn.SetActive(true);
            // colorTestBtn.SetActive(true);
            // content.text = "請問您今天想進行哪一種眼動測試呢？\n（凝視選項3秒）";
            // AudioPlay(questionAudio);
        }

        private void HideObjectsForCalibration()
        {
            if (!_objectsHidden)
            {
                _activeBeforeCalibration = new bool[hideTheseDuringCalibration.Length];
                for (int i = 0; i < hideTheseDuringCalibration.Length; i++)
                    _activeBeforeCalibration[i] = hideTheseDuringCalibration[i].activeSelf;
            }
            foreach (var hideThisDuringCalibration in hideTheseDuringCalibration)
                hideThisDuringCalibration.SetActive(false);
            _objectsHidden = true;
        }

        private void FailCalibration()
        {
            ComponentStatus = ComponentState.InternalError;
            CalibrationStatus = CalibrationState.CalibrationFail;
            _isCalibrating = false;
            if (_deviceCalibrationActive)
                StartCoroutine(StopDeviceCalibration());
            CalibrationFailed?.Invoke();
        }

        private IEnumerator StopDeviceCalibration()
        {
            _stopPending = true;
            // A data-collection task may still be running on a worker thread; give it time to finish first.
            yield return new WaitForSeconds(0.5f);
            var success = new ReferenceBool(false);
            yield return streamEngineCalibration.StopCalibrationRoutine(success);
            _deviceCalibrationActive = success == false;
            _stopPending = false;
        }

        // Clockwise starting from the top-left point; a single point is returned as-is.
        private static StimuliPosition[] OrderForGuidance(StimuliPosition[] points)
        {
            if (points.Length < 2)
                return points;

            Vector2 center = Vector2.zero;
            foreach (var point in points)
                center += point.screenPos;
            center /= points.Length;

            var ordered = (StimuliPosition[])points.Clone();
            Array.Sort(ordered, (a, b) => ClockwiseFromTopLeft(a.screenPos - center).CompareTo(ClockwiseFromTopLeft(b.screenPos - center)));
            return ordered;
        }

        private static float ClockwiseFromTopLeft(Vector2 offset)
        {
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            return Mathf.Repeat(135f - angle, 360f);
        }

        private void AudioPlay(AudioClip clip)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }

        /// <summary>
        /// Add a stimulus point at position stimulusPoint using a fixed collider size (Vector2 width/height fractions, no growth animation).
        /// Returns the stimulus position in screen pixels.
        /// </summary>
        private Vector2 AddStimulus(Vector2 gazeColliderSize, Vector2 stimulusPoint)
        {
            var currentStimulusPoint = Instantiate(stimuliPrefab);
            var sp = currentStimulusPoint.GetComponent<StimulusPoint>();
            sp.SetStreamHandleCalibration(streamEngineCalibration);
            calibrationPoints.Add(currentStimulusPoint);

            var screenWidth = Display.displays[Camera.main.targetDisplay].renderingWidth;
            var screenHeight = Display.displays[Camera.main.targetDisplay].renderingHeight;
            var screenPosFromNormalized = new Vector3(stimulusPoint.x * screenWidth, stimulusPoint.y * screenHeight, 5);

            currentStimulusPoint.transform.localPosition = Camera.main.ScreenToWorldPoint(screenPosFromNormalized);

            // Use new Vector2 overload for fixed collider size
            sp.ConfigureCollider(gazeColliderSize);
            sp.stimulusCompleteEvent.AddListener(stimulusCompleted);
            return new Vector2(screenPosFromNormalized.x, screenPosFromNormalized.y);
        }

        public void stimulusCompleted()
        {
            completeCount++;
        }

        private IEnumerator commit(ReferenceBool success)
        {
            yield return streamEngineCalibration.ComputeAndApplyCalibrationRoutine(success);
        }
    }
}

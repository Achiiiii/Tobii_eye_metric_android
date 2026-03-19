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

        private float _countDownTime = 5;
        private bool _countDownLocker = false;

        private void Awake()
        {
            CalibrationStatus = CalibrationState.CalibrationNotDone;
            calibrationPoints = new List<GameObject>();
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

        public void SetTrialCountDown(string side)
        {
            // _countDownTime = 5;
            // _countDownLocker = true;
            metricTest.gameObject.SetActive(false);
            mainCanvas.SetActive(true);
            switch (side)
            {
                case "right":
                    sampleImage.sprite = sampleSprites[0];
                    PlayTTS("請先遮擋左邊眼睛，使用右眼檢測");
                    break;
                case "left":
                    sampleImage.sprite = sampleSprites[1];
                    PlayTTS("請遮擋右邊眼睛，使用左眼檢測");
                    break;
                case "both":
                    sampleImage.sprite = sampleSprites[2];
                    PlayTTS("請勿遮擋眼睛，直接進行檢測");
                    break;
                default:
                    sampleImage.sprite = sampleSprites[0];
                    break;
            }
        }

        public void StartCalibration()
        {
            StartCoroutine(Calibrate());
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
            foreach (var hideThisDuringCalibration in hideTheseDuringCalibration)
                hideThisDuringCalibration.SetActive(false);

            ComponentStatus = ComponentState.CalibrationRunning;
            var success = new ReferenceBool(false);

            if (streamEngineDevice.IsConnected == false)
            {
                ComponentStatus = ComponentState.InternalError;
                CalibrationStatus = CalibrationState.CalibrationFail;
                yield break;
            }

            yield return streamEngineCalibration.StartCalibrationRoutine(success);
            if (success == false)
            {
                ComponentStatus = ComponentState.InternalError;
                CalibrationStatus = CalibrationState.CalibrationFail;
                yield break;
            }

            yield return streamEngineCalibration.ClearCalibrationRoutine(success);
            if (success == false)
            {
                ComponentStatus = ComponentState.InternalError;
                CalibrationStatus = CalibrationState.CalibrationFail;
                streamEngineCalibration.StopCalibrationRoutine(success);
                yield break;
            }

            foreach (var sequence in calibrationSequence)
            {
                completeCount = 0;
                foreach (var stimulusPoint in sequence.stimuliPoints)
                {
                    // Fixed collider size (Vector2: width fraction, height fraction)
                    AddStimulus(sequence.gazeColliderSize, stimulusPoint.screenPos);
                }
                yield return new WaitUntil(() => completeCount >= sequence.stimuliPoints.Length);

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
                ComponentStatus = ComponentState.InternalError;
                CalibrationStatus = CalibrationState.CalibrationFail;
                yield break;
            }

            ComponentStatus = ComponentState.Idle;

            foreach (var hideThisDuringCalibration in hideTheseDuringCalibration)
                hideThisDuringCalibration.SetActive(true);

            mainCanvas.SetActive(true);

            Debug.Log("Calibration was successful");
            pointer.SetActive(true);
            metricTest.gameObject.SetActive(true);
            metricTest.StartMeticTest();
            // blackTestBtn.SetActive(true);
            // colorTestBtn.SetActive(true);
            // content.text = "請問您今天想進行哪一種眼動測試呢？\n（凝視選項3秒）";
            // AudioPlay(questionAudio);
        }

        private void AudioPlay(AudioClip clip)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }

        /// <summary>
        /// Add a stimulus point at position stimulusPoint using a fixed collider size (Vector2 width/height fractions, no growth animation).
        /// </summary>
        private void AddStimulus(Vector2 gazeColliderSize, Vector2 stimulusPoint)
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

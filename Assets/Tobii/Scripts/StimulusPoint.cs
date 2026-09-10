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

using System.Collections;
using UnityEngine;
using static Tobii.GazeCalibrationManager;
using UnityEngine.Events;

namespace Tobii
{
    /// <summary>
    /// Requres BoxCollider to be gaze responsive
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class StimulusPoint : MonoBehaviour, IGazeFocusable
    {
        /// <summary>
        /// Prefab or GameObject that is subjected to spin and shrink.
        /// </summary>
        [SerializeField]
        [Tooltip("Prefab or GameObject that is subjected to spin and shrink.")]
        private GameObject stimulusObject;

        /// <summary>
        /// Prefab instantiated on successful data sampled, typically will destroy itself. 
        /// </summary>
        [SerializeField]
        [Tooltip("Prefab instantiated on successful data sampled, typically will destroy itself.")]
        private GameObject successPrefab;

        /// <summary>
        /// Prefab to be instantiated on a failed data sample, typically will destroy itself.
        /// </summary>
        [SerializeField]
        [Tooltip("Prefab instantiated on failed data sampled, typically will destroy itself.")]
        private GameObject failPrefab;

        /// <summary>
        /// Time before the moment spin and shrink finish and data is sampled.
        /// </summary>
        [SerializeField]
        [Tooltip("Time before spin and shrink finish and data is sampled.")]
        private float focusTime = 2f;

        /// <summary>
        /// Shrink acceleration curve.
        /// </summary>
        [SerializeField]
        [Tooltip("Shrink acceleration curve.")]
        private AnimationCurve shrinkCurve;

        /// <summary>
        /// Spin acceleration curve.
        /// </summary>
        [SerializeField]
        [Tooltip("Spin acceleration curve.")]
        private AnimationCurve spinCurve;

        /// <summary>
        /// Final rotation speed in degrees/sec.
        /// </summary>
        [SerializeField]
        [Tooltip("Final rotation speed in degrees/sec.")]
        private float spinSpeed = 1000f;

        /// <summary>
        /// The percentage scale of the stimulusObject at end of focus time.
        /// </summary>
        [SerializeField]
        [Tooltip("Scale to which stimulusObject reaches at moment data sample is taken.")]
        private float shrinkToPercentage = 20f;

        /// <summary>
        /// Exposes gaze focus state for additional visual representation if needed.
        /// </summary>
        public bool HasFocus { get; private set; }

        // --- PREVIOUS COLLIDER GROWTH STATE (KEPT FOR BACKWARD COMPATIBILITY / HISTORY) ---
        // private Vector3 _gazeColliderStartSize;
        // private Vector3 _gazeColliderEndSize;
        // private float _gazeColliderGrowTime;
        // private float timer = 0;

        /// <summary>
        /// Configure gaze collider sizing variables (legacy animated version).
        /// </summary>
        public void ConfigureCollider(float gazeColliderStartSize, float gazeColliderEndSize, float gazeColliderGrowTime)
        {
            var startSize = GetSizeFromFractionOfScreenWidth(transform.position, Camera.main, gazeColliderStartSize);
            var endSize = GetSizeFromFractionOfScreenWidth(transform.position, Camera.main, gazeColliderEndSize);
            _gazeColliderStartSize = new Vector3(startSize, startSize, 0.01f);
            _gazeColliderEndSize = new Vector3(endSize, endSize, 0.01f);
            _gazeColliderGrowTime = gazeColliderGrowTime;
            timer = 0; // reset animation timer
            _useFixedVector2Size = false;
        }

        /// <summary>
        /// Configure gaze collider (fixed size using independent normalized fractions of screen width / height).
        /// </summary>
        /// <param name="gazeColliderSize">x = fraction of screen width, y = fraction of screen height.</param>
        public void ConfigureCollider(Vector2 gazeColliderSize)
        {
            var sizeWorld = GetSizeFromFractionOfScreen(transform.position, Camera.main, gazeColliderSize);
            boxCollider.size = new Vector3(sizeWorld.x, sizeWorld.y, 0.01f);
            _useFixedVector2Size = true;
        }

        private bool _useFixedVector2Size = false;
        private Vector3 _gazeColliderStartSize;
        private Vector3 _gazeColliderEndSize;
        private float _gazeColliderGrowTime;
        private float timer = 0;

        private BoxCollider boxCollider;
        private float timeToPop;
        private Vector3 saveScale;

        private StreamEngineCalibration streamEngineCalibration;

        public UnityEvent stimulusCompleteEvent;

        void Awake()
        {
            boxCollider = GetComponent<BoxCollider>();
        }
        void Start()
        {
            timeToPop = focusTime;

            saveScale = stimulusObject.transform.localScale;
            if (stimulusObject.gameObject.scene.name == null) // Is a prefab
                stimulusObject = Instantiate(stimulusObject, transform);

            StartCoroutine(Calibrate());
        }

        void Update()
        {
            var normalizedFocusToPopTime = (focusTime - timeToPop) / focusTime;
            stimulusObject.transform.Rotate(new Vector3(0, 0, -spinCurve.Evaluate(normalizedFocusToPopTime) * spinSpeed * Time.deltaTime));
            float eval = shrinkCurve.Evaluate(normalizedFocusToPopTime);
            stimulusObject.transform.localScale = new Vector3(
                eval.Map(0, 1, saveScale.x, saveScale.x * shrinkToPercentage / 100),
                eval.Map(0, 1, saveScale.y, saveScale.y * shrinkToPercentage / 100),
                eval.Map(0, 1, saveScale.z, saveScale.z * shrinkToPercentage / 100));

            // Legacy animated collider growth (only runs if legacy configuration was used).
            if (!_useFixedVector2Size && timer < _gazeColliderGrowTime)
            {
                timer += Time.deltaTime;
                boxCollider.size = new Vector3(
                    timer.Map(0, _gazeColliderGrowTime, _gazeColliderStartSize.x, _gazeColliderEndSize.x),
                    timer.Map(0, _gazeColliderGrowTime, _gazeColliderStartSize.y, _gazeColliderEndSize.y),
                    timer.Map(0, _gazeColliderGrowTime, _gazeColliderStartSize.z, _gazeColliderEndSize.z));
            }
        }

        public IEnumerator Calibrate()
        {
            var success = new ReferenceBool(false);
            while (timeToPop > 0)
            {
                if (HasFocus)
                {
                    timeToPop -= Time.deltaTime;
                    if (timeToPop < 0)
                        timeToPop = 0;
                }
                else
                {
                    timeToPop += Time.deltaTime * 2; // Unwind at twice the speed
                    if (timeToPop > focusTime)
                        timeToPop = focusTime;
                }

                yield return null;
            }

            Vector2 stimulusPoint = Camera.main.WorldToViewportPoint(transform.position);
            yield return streamEngineCalibration.CollectCalibrationDataRoutine(stimulusPoint, success);

            stimulusCompleteEvent.Invoke();

            if (success == false)
            {
                Instantiate(failPrefab, new Vector3(transform.position.x, transform.position.y, transform.position.z - 0.01f), Quaternion.identity);
            }
            else
            {
                Instantiate(successPrefab, new Vector3(transform.position.x, transform.position.y, transform.position.z - 0.01f), Quaternion.identity);
            }
            Destroy(gameObject);
        }

        private float GetSizeFromFractionOfScreenWidth(Vector3 calibrationPointWorldPosition, Camera cam, float size)
        {
            var distanceInMeters = cam.transform.position.z - calibrationPointWorldPosition.z;
            var targetPixelWidth = size * Display.displays[cam.targetDisplay].renderingWidth;
            var a = cam.ScreenToWorldPoint(new Vector3(0, 0, distanceInMeters));
            var b = cam.ScreenToWorldPoint(new Vector3(targetPixelWidth, 0, distanceInMeters));
            return (Vector3.Distance(a, b));
        }

        /// <summary>
        /// Converts normalized (width, height) fractions of the screen into world space size (meters).
        /// </summary>
        /// <param name="calibrationPointWorldPosition">World position of the stimulus center.</param>
        /// <param name="cam">Camera used for conversion.</param>
        /// <param name="size">x=fraction of screen width, y=fraction of screen height.</param>
        /// <returns>World space width/height in meters.</returns>
        private Vector2 GetSizeFromFractionOfScreen(Vector3 calibrationPointWorldPosition, Camera cam, Vector2 size)
        {
            var distanceInMeters = cam.transform.position.z - calibrationPointWorldPosition.z;

            var display = Display.displays[cam.targetDisplay];
            var pixelWidth = display.renderingWidth;
            var pixelHeight = display.renderingHeight;

            var targetPixelWidth = size.x * pixelWidth;
            var targetPixelHeight = size.y * pixelHeight;

            var origin = cam.ScreenToWorldPoint(new Vector3(0, 0, distanceInMeters));
            var widthPoint = cam.ScreenToWorldPoint(new Vector3(targetPixelWidth, 0, distanceInMeters));
            var heightPoint = cam.ScreenToWorldPoint(new Vector3(0, targetPixelHeight, distanceInMeters));

            return new Vector2(Vector3.Distance(origin, widthPoint), Vector3.Distance(origin, heightPoint));
        }

        public void GazeFocusChanged(bool hasFocus)
        {
            HasFocus = hasFocus;
        }

        public void SetStreamHandleCalibration(StreamEngineCalibration shc)
        {
            streamEngineCalibration = shc;
        }
    }

}
public static class ExtensionMethods
{
    public static float Map(this float value, float fromLow, float fromHigh, float toLow, float toHigh)
    {
        return toLow + (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow);
    }
}

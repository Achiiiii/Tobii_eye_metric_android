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

public class HandleHeadMotion : MonoBehaviour
{
    public bool useFiltering = true;
    private OneEuroFilter _positionfilter = new OneEuroFilter();
    private OneEuroFilter _rotationfilter = new OneEuroFilter();
    private Vector3 _position = Vector3.zero;
    private Vector3 _rotation = Vector3.zero;

    // Define constants for Beta, MinCutoff, and DCutoff
    private const float BETA = 1.0f;
    private const float MIN_CUTOFF = 1.0f;
    private const float D_CUTOFF = 1.0f;

    private void Start()
    {
        // Use constants for filter parameters
        _positionfilter.Beta = BETA;
        _positionfilter.MinCutoff = MIN_CUTOFF;
        _positionfilter.DCutoff = D_CUTOFF;

        _rotationfilter.Beta = BETA;
        _rotationfilter.MinCutoff = MIN_CUTOFF;
        _rotationfilter.DCutoff = D_CUTOFF;
    }

    public void OnHeadPosePositionChanged(Vector3 position)
    {
        _position = position;
    }
    public void OnHeadPoseRotationChanged(Vector3 rotation)
    {
        // Convert from radians to degrees
        rotation.x = rotation.x * Mathf.Rad2Deg;
        rotation.y = rotation.y * Mathf.Rad2Deg;
        rotation.z = rotation.z * Mathf.Rad2Deg;

        if (rotation.y > 90)
            rotation.y -= 180;
        else if (rotation.y < -90)
            rotation.y += 180;

        _rotation = rotation;
    }
    void LateUpdate()
    {
        if (useFiltering)
            _position = _positionfilter.Step(Time.time, _position);
        transform.localPosition = _position;

        if (useFiltering)
            _rotation = _rotationfilter.Step(Time.time, _rotation);
        transform.localEulerAngles = _rotation;
    }

    public void OnToggleFiltering(bool value)
    {
        useFiltering = value;
    }
}

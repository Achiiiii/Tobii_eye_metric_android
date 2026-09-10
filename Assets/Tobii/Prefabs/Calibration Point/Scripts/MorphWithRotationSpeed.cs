/*
  COPYRIGHT 2024 - PROPERTY OF TOBII AB
  -------------------------------------
  2015 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

  NOTICE:  All information contained herein is, and remains, the property of Tobii AB and its suppliers, if any.
  The intellectual and technical concepts contained herein are proprietary to Tobii AB and its suppliers and may be
  covered by U.S.and Foreign Patents, patent applications, and are protected by trade secret or copyright law.
  Dissemination of this information or reproduction of this material is strictly forbidden unless prior written
  permission is obtained from Tobii AB.
*/

using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]  
public class MorphWithRotationSpeed : MonoBehaviour
{
    /// <summary>
    /// Maximum rotation speed in degrees/sec to map to full stretch morph.
    /// </summary>
    [SerializeField]
    private float maxSpeed = 2000;

    /// <summary>
    /// Blendshape animation curve.
    /// </summary>
    [SerializeField]
    private AnimationCurve animationCurve;

    private SkinnedMeshRenderer skinnedMeshRenderer;
    private Quaternion previousRotation;
    private float rotationSpeed;

    void Start()
    {
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();  
    }

    void Update()
    {
        // Calculate the change in rotation since the last frame
        Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(previousRotation);
        previousRotation = transform.rotation;

        // Convert the change in rotation to an angular velocity (in degrees per second)
        float angle;
        Vector3 axis;
        deltaRotation.ToAngleAxis(out angle, out axis);
        rotationSpeed = angle / Time.deltaTime;

        skinnedMeshRenderer.SetBlendShapeWeight(0, 100 * animationCurve.Evaluate(rotationSpeed / maxSpeed)); 
    }
}

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
using UnityEngine;

public class CalibrationSuccessPop : MonoBehaviour
{
    /// <summary>
    /// Number of spheres to spawn for pop.
    /// </summary>
    [SerializeField]
    [Tooltip("Number of spheres to spawn for pop.")]
    private int sphereCount = 7;

    /// <summary>
    /// Radius of spawned spheres.
    /// </summary>
    [SerializeField]
    [Tooltip("Radius of spawned spheres.")]
    private float sphereRadius = 1;

    /// <summary>
    /// Distance spawned spheres to travel from center.
    /// </summary>
    [SerializeField]
    [Tooltip("Distance spawned spheres to travel from center.")]
    private float sphereDistance = 5;

    /// <summary>
    /// Material to use for spawned spheres.
    /// </summary>
    [SerializeField]
    [Tooltip("Material to use for spawned spheres.")]
    private Material material;

    /// <summary>
    /// Spawned sphere's decceleration animation curve. 
    /// </summary>
    [SerializeField]
    [Tooltip("Spawned sphere's decceleration animation curve.")]
    private AnimationCurve animationCurve;

    private List<GameObject> spheres;
    private List<Vector3> randomTargetPositions;
    private float time = 1f;

    private float alpha = 1;
    private float timer;
    private Material duplicateMaterial;

    void Start()
    {
        spheres = new List<GameObject>();
        randomTargetPositions = new List<Vector3>();

        duplicateMaterial = new Material(material);
        Color materialColor = duplicateMaterial.color;
        materialColor.a = 1f;
        duplicateMaterial.color = materialColor;

        for (int i = 0; i < sphereCount; i++)
        {
            // Create sphere manually without CreatePrimitive to avoid SphereCollider dependency
            GameObject sphere = new GameObject($"PopSphere_{i}");
            MeshFilter meshFilter = sphere.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = sphere.AddComponent<MeshRenderer>();

            // Use Unity's built-in sphere mesh
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            meshRenderer.material = duplicateMaterial;

            sphere.transform.parent = transform;
            sphere.transform.localPosition = new Vector3(0, 0, 0);
            
            // Unity's built-in sphere has diameter of 2 (radius 1), so scale by 0.5 to match CreatePrimitive behavior
            float scale = sphereRadius * 0.5f;
            sphere.transform.localScale = new Vector3(scale, scale, scale);
            spheres.Add(sphere);

            float x = UnityEngine.Random.Range(-sphereDistance, sphereDistance);
            float y = UnityEngine.Random.Range(-sphereDistance, sphereDistance);
            float z = UnityEngine.Random.Range(-sphereDistance, sphereDistance);

            Vector3 vector = new Vector3(x, y, z);
            randomTargetPositions.Add(vector);
        }

        timer = time;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        alpha = animationCurve.Evaluate(timer / time);

        Color materialColor = duplicateMaterial.color;
        materialColor.a = alpha;
        duplicateMaterial.color = materialColor;

        for (int i = 0; i < sphereCount; i++)
        {
            var a = 1 - animationCurve.Evaluate(timer / time);
            spheres[i].transform.localPosition = new Vector3(randomTargetPositions[i].x * a, randomTargetPositions[i].y * a, randomTargetPositions[i].z * a);
        }
    }
}

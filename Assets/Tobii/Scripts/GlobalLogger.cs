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

public class GlobalLogger : MonoBehaviour
{
    private static GlobalLogger _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure GameObject has the exact name expected by Java
        gameObject.name = "GlobalLogger";

        Debug.Log("[GlobalLogger] Initialized and ready to receive Java logs");
    }

    // Called from Java via UnitySendMessage
    private void OnJavaLog(string message)
    {
        Debug.Log($"[Java] {message}");
    }
}
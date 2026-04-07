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
using UnityEngine;

public static class TobiiAndroidBridge
{
    private static AndroidJavaObject _bridge;
    private static bool _init;

    public static bool Initialize()
    {
        Debug.Log("[TobiiAndroidBridge]: TobiiAndroidBridge Initialize called");
        if (_init) return true;

        if (Application.platform != RuntimePlatform.Android) return false;

        using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");

        var act = up.GetStatic<AndroidJavaObject>("currentActivity");
        _bridge = new AndroidJavaObject("com.tobii.TobiiUnityBridge", act);
        _init = true;
        return true;
    }

    public static bool IsReady() => _bridge != null && _bridge.Call<bool>("isReady");

}

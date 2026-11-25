using System.Collections.Generic;
using UnityEngine;

public static class RobotDataSender
{
    private const int RESULT_AUTO_PLAY = -1;

    private static bool? s_startedForResult;

    // True when another app (the robot's Roflow flow) started this one and waits for its result: the data set with
    // setResult reaches it only once this app closes.
    public static bool StartedForResult
    {
        get
        {
            if (s_startedForResult == null)
                s_startedForResult = ReadStartedForResult();
            return s_startedForResult.Value;
        }
    }

    private static bool ReadStartedForResult()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var caller = currentActivity.Call<AndroidJavaObject>("getCallingActivity"))
            {
                Debug.Log("[RobotDataSender] started for result: " + (caller != null ? caller.Call<string>("flattenToShortString") : "no"));
                return caller != null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[RobotDataSender] getCallingActivity failed: " + e.Message);
        }
#endif
        return false;
    }

    // 單筆送出（注意：會覆蓋掉前一次 setResult 的內容）
    public static void SendData(string extraKey, string yourData)
    {
        SendData(new Dictionary<string, string> { { extraKey, yourData } });
    }

    // 多筆一次送出，避免 setResult 互相覆蓋
    public static void SendData(Dictionary<string, string> data)
    {
        if (data == null || data.Count == 0)
        {
            Debug.LogWarning("[RobotDataSender] SendData called with empty data.");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                foreach (var kv in data)
                {
                    intent.Call<AndroidJavaObject>("putExtra", kv.Key, kv.Value);
                }
                currentActivity.Call("setResult", RESULT_AUTO_PLAY, intent);

                foreach (var kv in data)
                {
                    Debug.Log($"[RobotDataSender] setResult extra: {kv.Key} = {kv.Value}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RobotDataSender] Failed: {e.Message}");
        }
#else
        foreach (var kv in data)
        {
            Debug.Log($"[RobotDataSender] (Editor mock) {kv.Key} = {kv.Value}");
        }
#endif
    }
}

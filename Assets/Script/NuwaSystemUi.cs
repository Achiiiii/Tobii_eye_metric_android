using System;
using UnityEngine;

// Controls the Kebbi robot's system alert bar, which the Nuwa Unity bridge does not expose directly.
public static class NuwaSystemUi
{
    public static void SetSystemAlertsEnabled(bool enabled)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var plugin = new AndroidJavaClass("com.u2a.sdk.NuwaPlugin"))
            using (var robot = plugin.GetStatic<AndroidJavaObject>("mRobot"))
            {
                if (robot == null)
                {
                    Debug.LogWarning("[NuwaSystemUi] Nuwa robot API is not ready yet.");
                    return;
                }
                robot.Call(enabled ? "enableSystemAlertUI" : "disableSystemAlertUI");
                Debug.Log("[NuwaSystemUi] System alert UI " + (enabled ? "enabled" : "disabled"));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[NuwaSystemUi] Failed to change system alert UI: " + e.Message);
        }
#else
        Debug.Log("[NuwaSystemUi] (Editor) system alert UI " + (enabled ? "enabled" : "disabled"));
#endif
    }
}

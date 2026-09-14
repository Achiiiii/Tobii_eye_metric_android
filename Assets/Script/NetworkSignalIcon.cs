using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class NetworkSignalIcon : MonoBehaviour
{
    private const float PollSeconds = 2f;
    private const int TapsToToggleDebug = 5;
    private const float TapWindowSeconds = 3f;
    private const int Offline = -1;
    private const int Unknown = -2;
    private static readonly Color InactiveColor = new Color32(0xBD, 0xBD, 0xBD, 0xFF);
    private static readonly Color GoodColor = new Color32(0x43, 0xA0, 0x47, 0xFF);
    private static readonly Color FairColor = new Color32(0xF9, 0xA8, 0x25, 0xFF);
    private static readonly Color PoorColor = new Color32(0xE5, 0x39, 0x35, 0xFF);

    private Image[] _bars;
    private GameObject _offlineMark;
    private Action _onDebugToggle;
    private int _taps;
    private float _firstTapTime;

    public static NetworkSignalIcon Create(Transform parent, Vector2 anchoredPosition, float size, Color background, Action onDebugToggle)
    {
        var button = UiFactory.CreateButton("NetworkSignalIcon", parent, UiFactory.Circle, background, null);
        var rect = UiFactory.Place((RectTransform)button.transform, Vector2.one, Vector2.one, anchoredPosition, new Vector2(size, size));
        var icon = button.gameObject.AddComponent<NetworkSignalIcon>();
        icon._onDebugToggle = onDebugToggle;
        button.onClick.AddListener(icon.OnTap);

        float barWidth = size * 0.12f;
        float gap = size * 0.07f;
        float totalWidth = barWidth * 4f + gap * 3f;
        icon._bars = new Image[4];
        for (int i = 0; i < icon._bars.Length; i++)
        {
            var bar = UiFactory.CreateImage("Bar" + i, rect, UiFactory.White, InactiveColor);
            var position = new Vector2(-totalWidth / 2f + barWidth / 2f + i * (barWidth + gap), -size * 0.26f);
            UiFactory.Place(bar.rectTransform, UiFactory.Center, new Vector2(0.5f, 0f), position, new Vector2(barWidth, size * (0.2f + 0.12f * i)));
            icon._bars[i] = bar;
        }

        var mark = UiFactory.Place(UiFactory.CreateRect("OfflineMark", rect), UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(size, size));
        foreach (float angle in new[] { 45f, -45f })
        {
            var stroke = UiFactory.CreateImage("Stroke", mark, UiFactory.White, PoorColor);
            UiFactory.Place(stroke.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(size * 0.55f, size * 0.1f));
            stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        icon._offlineMark = mark.gameObject;

        icon.Apply(Unknown);
        return icon;
    }

    private IEnumerator Start()
    {
        while (true)
        {
            Apply(ReadLevel());
            yield return new WaitForSecondsRealtime(PollSeconds);
        }
    }

    // Returns 0..4 bars, Offline, or Unknown.
    private static int ReadLevel()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
            return Offline;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (var wifiManager = context.Call<AndroidJavaObject>("getSystemService", "wifi"))
                using (var connectionInfo = wifiManager.Call<AndroidJavaObject>("getConnectionInfo"))
                using (var wifiManagerClass = new AndroidJavaClass("android.net.wifi.WifiManager"))
                {
                    int rssi = connectionInfo.Call<int>("getRssi");
                    return wifiManagerClass.CallStatic<int>("calculateSignalLevel", rssi, 5);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NetworkSignalIcon] Failed to read Wi-Fi signal: " + e.Message);
                return Unknown;
            }
        }
#endif
        // Carrier data (or Editor): reachable, but strength is not readable without extra permissions.
        return 4;
    }

    private void Apply(int level)
    {
        Color activeColor = level >= 3 ? GoodColor : level == 2 ? FairColor : PoorColor;
        int lit = level >= 0 ? Mathf.Max(level, 1) : 0;
        for (int i = 0; i < _bars.Length; i++)
            _bars[i].color = i < lit ? activeColor : InactiveColor;
        _offlineMark.SetActive(level == Offline);
    }

    private void OnTap()
    {
        if (Time.unscaledTime - _firstTapTime > TapWindowSeconds)
        {
            _taps = 0;
            _firstTapTime = Time.unscaledTime;
        }
        if (++_taps < TapsToToggleDebug)
            return;
        _taps = 0;
        _onDebugToggle?.Invoke();
    }
}

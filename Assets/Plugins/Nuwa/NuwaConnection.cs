using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class NuwaConnection
{

#if UNITY_ANDROID && !UNITY_EDITOR
	static AndroidJavaClass androidJavaClass {
		get {
			init();
			return jc;
		}
	}
	static AndroidJavaClass jc = null;
#else

#endif

    private static bool isInitialized = false;

    public static void init()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		if (jc != null) {
			return;
		}
        Debug.Log("NuwaConnection.init();");
		jc = new AndroidJavaClass("com.u2a.sdk.Connection.NuwaConnection");
        jc.CallStatic("setNuwaConnectionCallback", new ConnecttionCallBack());
#else
        if (isInitialized)
        {
            return;
        }
        isInitialized = true;
        Debug.Log("NuwaConnection.init();");
#endif
    }

    public static void CreateConnectionManager()
    {
        Debug.Log("createConnectionManager");

#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("createConnectionManager");
#endif
    }

    public static void StartScan()
    {
        Debug.Log("starScan");
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("starScan");
#endif
    }

    public static void StopScan()
    {
        Debug.Log("StopScan");
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("stopScan");
#endif
    }

    public static void Disconnect()
    {
        Debug.Log("Disconnect");
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("onDisconnect");
#endif
    }

    public static void StartConnect(string device_name, string device_address)
    {
        Debug.Log("StartConnect , " + device_name + " , " + device_address);
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("onStartConnect", device_name, device_address);
#endif
    }

    public static event Action<string> OnReceiveScanResultEvent;
    public static event Action<EConnectResult> OnReceiveConnectionResultEvent;

    public enum EConnectResult
    {
        Disconnected = 0,
        Connected = 1,
        Error = 999,
    }

    class ConnecttionCallBack : AndroidJavaProxy
    {
        public ConnecttionCallBack() : base("com.u2a.sdk.Connection.NuwaConnectionCallback") { }

        void OnScanResult(string value)
        {
            OnReceiveScanResultEvent?.Invoke(value);
        }

        void OnConnectionResult(string result)
        {
            try
            {
                EConnectResult eConnectResult = (EConnectResult)Enum.Parse(typeof(EConnectResult), result, true);
                OnReceiveConnectionResultEvent?.Invoke(eConnectResult);
            }
            catch
            {
                OnReceiveConnectionResultEvent?.Invoke(EConnectResult.Error);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

public static class Nuwa
{

    public static void init()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		if (jc != null) {
			return;
		}

        Debug.Log("Unity : Nuwa AndroidJavaClass Instance");
		jc = new AndroidJavaClass("com.u2a.sdk.NuwaPlugin");
		jc.CallStatic("setNuwaCallback", new KiwiCallback());
		jc.CallStatic("getInstance", AndroidJavaUtility.currentActivity, Application.identifier);
#else
        if (isInitialized)
        {
            return;
        }
        isInitialized = true;
        Debug.Log("Nuwa.init();");
#endif
    }

    /// <summary>
    /// assets/motion_bin/
    /// </summary>
    /// <param name="actionName"></param>
    /// <param name="autoFadein"> set this value = false</param>
    /// <param name="specified_motion_path"> 
    ///     put motions into "specified_motion_path/assets/motion_bin/"
    ///     ex. if you put motions into path : "/storage/emulated/0/Download/assets/motion_bin/"
    ///     so you need set this param as : "/storage/emulated/0/Download"
    /// </param>
    public static void motionPlay(string actionName, bool autoFadein = false, string specified_motion_path = "")
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if(specified_motion_path.Length == 0)
        {
            androidJavaClass.CallStatic("motionPlay", actionName, autoFadein);
        }
        else
        {
            androidJavaClass.CallStatic("MotionPlayWithPath", actionName, autoFadein, specified_motion_path);
        }
		
#else
        Debug.Log("Nuwa.motionPlay(" + actionName + ");");
        if (onCompleteOfMotionPlay != null)
        {
            CompleteMotion(actionName);
        }
#endif
    }

    private static async void CompleteMotion(string actionName)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        if (onCompleteOfMotionPlay != null)
            onCompleteOfMotionPlay(actionName);
    }

    public static void motionStop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("motionStop");
#else
        Debug.Log("Nuwa.motionStop();");
#endif
    }

    public static void motionReset()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("motionReset");
#else
        Debug.Log("Nuwa.motionReset();");
#endif
    }

    public static void motionResume()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("motionResume");
#else
        Debug.Log("Nuwa.motionResume();");
#endif
    }

    public static void motionPause()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("motionPause");
#else
        Debug.Log("Nuwa.motionPause();");
#endif
    }

    public static void startTTS(string tts)
    {

        if (onStartTTs != null) onStartTTs();

#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("startTTS", tts);
#else
        Debug.Log("Nuwa.startTTS(" + tts + ");");
        if (onTTSComplete != null)
        {
            CompleteTTs();
        }
#endif
    }


    private static async void CompleteTTs()
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        if (onTTSComplete != null)
            onTTSComplete(true);
    }

    /// <summary>
    /// 取得機器名稱
    /// </summary>
    /// <param name="resource_package_name"></param>
    /// <returns></returns>
    public static string[] GetMachineName(string package_name = "")
    {
        string resource_package_name = "com.nuwarobotics.skupartner";
        if (!package_name.Equals(string.Empty))
            resource_package_name = package_name;
        Debug.Log("========GetMachineName===========");
        string[] Result = null;
#if UNITY_ANDROID && !UNITY_EDITOR

            //首先获得UnityPlayer：
            var playerCls = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            //由此可以获得当前activity：
            var activity = playerCls.GetStatic<AndroidJavaObject>("currentActivity");
            //同时还可以获得applicationContext：
            var applicationContext = activity.Call<AndroidJavaObject>("getApplicationContext");
            //使用createPackageContext抓取對應的context, resource_package_name
            var otherContext = applicationContext.Call<AndroidJavaObject>("createPackageContext", resource_package_name, 0);
            //androidJavaClass.CallStatic<float>("getMotorPresentPossitionInDegree", mortorid);
		Result = androidJavaClass.CallStatic<String []>("GetMachineName", otherContext);
#else
        Debug.Log("Nuwa.GetText();");
#endif

        //回傳getMachineName
        if (Result != null)
        {
            Debug.Log(" name : " + Result[0] + " , " + Result[1]);
        }
        return Result;
    }

    public static void stopTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("stopTTS");
#else
        Debug.Log("Nuwa.stopTTS();");
#endif
    }

    public static void pauseTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("pauseTTS");
#else
        Debug.Log("Nuwa.pauseTTS();");
#endif
    }

    public static void resumeTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("resumeTTS");
#else
        Debug.Log("Nuwa.resumeTTS();");
#endif
    }

    public static void startWakeUp()
    {
        startWakeUp(true);
    }

    public static void startWakeUp(bool isAsync)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("startWakeUp", isAsync);
#else
        Debug.Log("Nuwa.startWakeUp" + isAsync + "();");
#endif
    }

    public class BnfData
    {

        public BnfData(string name)
        {
            _name = name;
#if UNITY_ANDROID && !UNITY_EDITOR
			bnfData = androidJavaClass.CallStatic<AndroidJavaObject>("getNewBnfData", name);
#endif
        }

        public void addSlot(string tag, bool isRequired, int cmdKey, params string[] values)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
			bnfData = androidJavaClass.CallStatic<AndroidJavaObject>("addSlot", bnfData, tag, isRequired, cmdKey, values);
#endif
        }

        public bool updateBody()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
			return bnfData.Call<bool>("updateBody");
#else
            return false;
#endif
        }

        public string name
        {
            get
            {
                return _name;
            }
        }

        public string body
        {
            get
            {
                return
#if UNITY_ANDROID && !UNITY_EDITOR
					bnfData.Get<string>("body");
#else
                    null;
#endif
            }
        }

        string _name;
#if UNITY_ANDROID && !UNITY_EDITOR
		AndroidJavaObject bnfData = null;
#endif
    }

    //
    public static void prepareGrammarToRobot(string name, string[] values)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("prepareGrammarToRobot", name, values);
#else
        Debug.Log("Nuwa.prepareGrammarToRobot(" + name + ", " + values + ");");
#endif
    }


    public static void addSlot(ref BnfData data, string tag, bool isRequired, int cmdKey, params string[] values)
    {
        data.addSlot(tag, isRequired, cmdKey, values);
    }

    public static void createCrammer(string name, string body)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("createCrammer", name, body);
#else
        Debug.Log("Nuwa.createCrammer(" + name + ", " + body + ");");
#endif
    }

    public static void startMixUnderstand()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("startMixUnderstand");
#else
        Debug.Log("Nuwa.startMixUnderstand();");
#endif
    }

    public static void setRecognizeLanguage(string language)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("setRecognizeLanguage",language);
#else
        Debug.Log("Nuwa.setRecognizeLanguage(" + language + ");");
#endif
    }
    public static void setListenParameter(ListenType type, string key, string value)
    {

#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("setListenParameter", type.ToString(),key,value);
#else
        Debug.Log("Nuwa.setListenParameter(" + type + "," + key + "," + value + ");");
#endif
    }

    /// <summary>
    /// Set Speak Parameter
    /// </summary>
    /// <param name="key"> use "speed" or "pitch" </param>
    /// <param name="value"> value between 1 to 9 </param>
    public static void SetSpeakParameter(string key, string value)
    {

#if UNITY_ANDROID && !UNITY_EDITOR
        ListenType  type = ListenType.NONE;
		androidJavaClass.CallStatic("setSpeakParameter", type.ToString(),key,value);
#else
        Debug.Log("Nuwa.SetSpeakParameter(" + key + "," + value + ");");
#endif
    }

    public static void startSpeech2Text(bool wakeup)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("startSpeech2Text",wakeup);
#else
        Debug.Log("Nuwa.startSpeech2Text(" + wakeup + ");");
#endif
    }

    public static void startLocalCommand()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("startLocalCommand");
#else
        Debug.Log("Nuwa.startLocalCommand();");
#endif
    }

    public static void stopListen()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("stopListen");
#else
        Debug.Log("Nuwa.stopListen();");
#endif
    }

    public static void showWindow(bool anim)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("showWindow", anim);
#else
        Debug.Log("Nuwa.showWindow(" + anim + ");");
#endif
    }

    public static void hideWindow(bool anim)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("hideWindow", anim);
#else
        Debug.Log("Nuwa.hideWindow(" + anim + ");");
#endif
    }

    public enum LEDPosition
    {
        Head = 1, Chest = 2, LeftHand = 4, RightHand = 3
    }

    public static void enableLed(LEDPosition id, bool enable)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("enableLed", (int)id, enable? 1 : 0);
#else
        Debug.Log("Nuwa.enableLed(" + id + ", " + enable + ");");
#endif
    }

    public static void setLedColor(LEDPosition id, Color color)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		Color32 color32 = color;
		androidJavaClass.CallStatic("setLedColor", (int)id, (int)color32.a, (int)color32.r, (int)color32.g, (int)color32.b);
#else
        Debug.Log("Nuwa.setLedColor(" + id + ", " + color + ");");
#endif
    }

    public static void enableLedBreath(LEDPosition id, int interval, int ratio)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("enableLedBreath", (int)id, interval, ratio);
#else
        Debug.Log("Nuwa.enableLedBreath(" + id + ", " + interval + ", " + ratio + ");");
#endif
    }

    public static void enableSystemLED()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("enableSystemLED");
#else
        Debug.Log("Nuwa.enableSystemLED();");
#endif
    }

    public static void disableSystemLED()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("disableSystemLED");
#else
        Debug.Log("Nuwa.disableSystemLED();");
#endif
    }

    public static void requestSensor(int i)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("requestSensor",i);
#else
        Debug.Log("Nuwa.requestSensor(" + i + ");");
#endif

    }
    public static void releaseSensor(int i)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("releaseSensor",i);
#else
        Debug.Log("Nuwa.releaseSensor(" + i + ");");
#endif

    }

    public enum NuwaRecognition
    {
        OBJ = 1,
        FACE_TRACK = 2,
        FACE_RECOGNITION = 4,
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type">1 : OBJ, 2: FACE, 4:FACE_RECOGNITION </param>
    public static void startRecognition(NuwaRecognition type)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("startRecognition", AndroidJavaUtility.currentActivity , (int)type);
#else
        Debug.Log("Nuwa.startRecognition();");
#endif
    }

    public static void stopRecognition()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("stopRecognition");
#else
        Debug.Log("Nuwa.stopRecognition();");
#endif
    }

    public static void StartCamera(NuwaRecognition type)
    {

    }


    /// <summary>
    /// 可動馬達的類型
    /// </summary>
    public enum NuwaMotorType
    {
        neck_y = 1,
        neck_z = 2,
        right_shoulder_z = 3,
        right_shoulder_y = 4,
        right_shoulder_x = 5,
        right_bow_y = 6,
        left_shoulder_z = 7,
        left_shoulder_y = 8,
        left_shoulder_x = 9,
        left_bow_y = 10,
    }

    public static float getMotorPresentPossitionInDegree(int mortorid)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
       return androidJavaClass.CallStatic<float>("getMotorPresentPossitionInDegree", mortorid);
#else
        Debug.Log("Nuwa.getMotorPresentPossitionInDegree(" + mortorid + ");");
        return -1;
#endif
    }

    public static float getMotorPresentPossitionInDegree(NuwaMotorType type)
    {
        return getMotorPresentPossitionInDegree((int)type);
    }


    /// <summary>
    /// 控制motor
    /// </summary>
    /// <param name="mortorid"></param>
    /// <param name="position"></param>
    /// <param name="speedDegree"></param>
    /// <returns>bool確認是否成功</returns>
    public static bool setMotorPositionInDegree(int mortorid, float position, float speedDegree)
    {
        if (speedDegree <= 10) speedDegree = 10;
#if UNITY_ANDROID && !UNITY_EDITOR
	   return androidJavaClass.CallStatic<bool>("setMotorPositionInDegree", mortorid,  position , speedDegree);
#else
        // Debug.Log("Nuwa.setMotorPositionInDegree(" + mortorid  + " , " + position +" , " + speedDegree + ");");
        return false;
#endif
    }

    public static bool setMotorPositionInDegree(NuwaMotorType type, float position, float speedDegree)
    {
        return setMotorPositionInDegree((int)type, position, speedDegree);
    }


    #region Wheel

    public static void UnlockWheel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("unlockWheel");
#else
        Debug.Log("UnlockWheel();");
#endif
    }

    public static void LockWheel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("lockWheel");
#else
        Debug.Log("LockWheel();");
#endif
    }

    #endregion Wheel

    #region RobotMove

    public static void SetMove(float move_value = 0)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("setMove" , move_value);
#else
        Debug.Log("SetMove();");
#endif

    }

    public static void MoveForwardInAccelerationEx()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("moveForwardInAccelerationEx");
#else
        Debug.Log("MoveForwardInAccelerationEx();");
#endif
    }

    public static void MoveBackInAccelerationEx()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("moveBackInAccelerationEx");
#else
        Debug.Log("MoveBackInAccelerationEx()");
#endif

    }

    public static void StopInAcclerationEx()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("stopInAcclerationEx");
#else
        Debug.Log("StopInAcclerationEx();");
#endif

    }

    #endregion RobotMove

    #region RobotTurn

    public static void TurnLeftEx()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("turnLeftEx");
#else
        Debug.Log("TurnLeftEx();");
#endif

    }

    public static void TurnRightEx()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("turnRightEx");
#else
        Debug.Log("TurnRightEx();");
#endif

    }

    public static void StopTurnEx()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("stopTurnEx");
#else
        Debug.Log("StopTurnEx();");
#endif

    }


    public static void SetTurn(float turn_value = 0)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("setTurn" , turn_value);
#else
        Debug.Log("SetTurn();");
#endif

    }

    #endregion RobotTurn


    public static void release()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		androidJavaClass.CallStatic("release");
#else
        Debug.Log("Nuwa.release();");
#endif
    }



    /// <summary>
    /// 呼叫Android端的Activity離開遊戲
    /// </summary>
    public static void ExitGame()
    {
        AndroidJavaObject mainActivityJavaObj = null;
#if UNITY_ANDROID && !UNITY_EDITOR

        mainActivityJavaObj = new AndroidJavaObject("com.u2a.sdk.NuwaUnityPlayerActivity");

        mainActivityJavaObj.Call("FinishActivity");
#else
        Debug.Log("Nuwa.ExitGame(); FinishActivity");
#endif

    }



    public static event Action onRelease = null;
    public static event Action onWikiServiceStart = null;
    public static event Action onWikiServiceStop = null;
    public static event Action onWikiServiceCrash = null;
    public static event Action onWikiServiceRecovery = null;
    public static event Action<string> onStartOfMotionPlay = null;
    public static event Action<string> onPauseOfMotionPlay = null;
    public static event Action<string> onStopOfMotionPlay = null;
    public static event Action<string> onCompleteOfMotionPlay = null;
    public static event Action<string> onPlayBackOfMotionPlay = null;
    public static event Action<int> onErrorOfMotionPlay = null;
    public static event Action<bool, string, float> onPrepareMotion = null;
    public static event Action<string> onCameraOfMotionPlay = null;
    [Serializable]
    public struct CameraPosition
    {
        public float x;
        public float y;
        public float z;
        public float Xx;
        public float Yx;
        public float Zx;
        public float Xy;
        public float Yy;
        public float Zy;
        public float Xz;
        public float Yz;
        public float Zz;
    }
    public static event Action<CameraPosition> onGetCameraPose = null;
    public enum TouchEventType
    {
        Head = 1, Chest = 2, RightHand = 3, LeftHand = 4, LeftCheck = 5, RightCheck = 6,
    }
    public static event Action<TouchEventType> onTouchBegan = null;
    public static event Action<TouchEventType> onTouchEnd = null;
    public static event Action<TouchEventType> onTap = null;
    public static event Action<TouchEventType> onLongPress = null;
    public static event Action<int> onPIREvent = null;
    public static event Action onWindowSurfaceReady = null;
    public static event Action onWindowSurfaceDestroy = null;
    public static event Action<int, int> onTouchEyes = null;
    public static event Action<float> onFaceSpeaker = null;
    [Serializable]
    public struct ScoreInfoOnWakeUp
    {
        public ScoreInfoOnWakeUp(string json)
        {
            this = JsonUtility.FromJson<ScoreInfoOnWakeUp>(json);
        }
        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
        public string sst;
        public int id;
        public int score;
        public int bos;
        public int eos;
    }
    public static event Action<bool, ScoreInfoOnWakeUp, float> onWakeup = null;
    public static event Action<bool, string> onGrammarState = null;
    public enum ResultType
    {
        NONE,
        /// <summary> 雲端 </summary>
        UNDERSTAND,
        RECOGNIZE,
        /// <summary> 本地 </summary>
        LOCAL_COMMAND
    }
    public static event Action<bool, ResultType, string> onMixUnderstandComplete = null;

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public struct NuwaVoiceRecognition
    {
        public NuwaVoiceRecognition(string json)
        {
            Debug.Log("NuwaVoiceRecognition");
            this = JsonUtility.FromJson<NuwaVoiceRecognition>(json);
            
            //string temp = this.content;
            //temp = temp.Replace("\n", "");
            //this.content = temp;
        }
        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }

        /// <summary> ASR result(String base)​ </summary>
        public string result;

        /** 韓文ASR會出錯 json格式不一樣
         * 
        /// <summary> Result format, 1 is Command, 2 is Recognized </summary>
        public int type;
        /// <summary> version control (double base)​ </summary>
        public int version;
        public string engine;
        public string x-trace-id;
        public string class;
        public string extra;
        public string content;
        **/
    }

    /// <summary>
    /// 語音辨識的Json資料結構 舊的
    /// https://www.xfyun.cn/doccenter/awd 詳看 附录 - 识别结果说明 
    /// </summary>
	[Serializable]
    public struct VoiceRecognition
    {
        public VoiceRecognition(string json)
        {
            this = JsonUtility.FromJson<VoiceRecognition>(json);
        }
        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }

        /// <summary>
        /// sentence
        /// </summary>
		public int sn;
        /// <summary>
        /// last sentence
        /// </summary>
		public bool ls;
        /// <summary>
        /// begin
        /// </summary>
		public int bg;
        /// <summary>
        /// end
        /// </summary>
		public int ed;
        /// <summary>
        /// words
        /// </summary>
		[Serializable]
        public struct WS
        {
            public WS(string json)
            {
                this = JsonUtility.FromJson<WS>(json);
            }
            public override string ToString()
            {
                return JsonUtility.ToJson(this);
            }
            public int bg;
            public string slot;
            /// <summary>
            /// chinese word
            /// </summary>
			[Serializable]
            public struct CW
            {
                public CW(string json)
                {
                    this = JsonUtility.FromJson<CW>(json);
                }
                public override string ToString()
                {
                    return JsonUtility.ToJson(this);
                }
                /// <summary>
                /// score
                /// </summary>
				public int sc;
                public int gm;
                /// <summary>
                /// word
                /// </summary>
				public string w;
                public int id;
            }
            public CW[] cw;
        }
        public WS[] ws;
        /// <summary>
        /// score 
        /// </summary>
		public int sc;
        public string content;
    }

    public static event Action<bool, string> onSpeech2TextComplete = null;
    public enum ListenType
    {
        NONE,
        WAKE_UP,
        UNDERSTAND,
        RECOGNIZE,
        LOCAL_COMMAND,
        MIX,
        ONE_SHOT
    }

    public static event Action<NuwaVoiceRecognition> onLocalCommandComplete = null;
    public static event Action<ResultType, string> onLocalCommandException = null;
    public static event Action<bool> onTTSComplete = null;
    public static event Action onStartTTs = null;
    public static event Action<int, int> onActionEvent = null;
    public static event Action<int> onDropSensorEvent = null;
    public static event Action<bool> onConnected = null;
    [Serializable]
    public struct OutputData
    {
        public OutputData(int id, float processTime, string data)
        {
            this.id = id;
            this.processTime = processTime;
            string[] dataStrings = data.Trim('[', ']').Split('{', '}');
            List<Data> dataList = new List<Data>();
            for (int i = 1; i < dataStrings.Length; i += 2)
            {
                dataList.Add(new Data("{" + dataStrings[i] + "}"));
            }
            dataSets = dataList.ToArray();
        }
        public int id;
        public float processTime;
        [Serializable]
        public struct Data
        {
            public Data(string json)
            {
                this = JsonUtility.FromJson<Data>(json);
            }
            public override string ToString()
            {
                return JsonUtility.ToJson(this);
            }
            public float confidence;
            public string id;
            public string title;
        }
        public Data[] dataSets;
        public override string ToString()
        {
            string[] dataStrings = new string[dataSets.Length];
            for (int i = 0; i < dataStrings.Length; ++i)
            {
                dataStrings[i] = dataSets[i].ToString();
            }
            return "{id: " + id + ", processTime: " + processTime + ", data: [" + string.Join(", ", dataStrings) + "]}";
        }
    }
    [Serializable]
    public struct TrackData
    {
        public string height;
        public string width;
        public string x;
        public string y;
    }
    [Serializable]
    public struct FaceRecognizeData
    {
        public float conf;
        public int idx;
        public string name;
        public rect rect;
    }
    [Serializable]
    public struct rect
    {
        public int buttom;
        public int left;
        public int right;
        public int top;
    }

    public static event Action<OutputData[]> onOutput = null;
    public static event Action<TrackData[]> onTrack = null;
    public static event Action<FaceRecognizeData[]> onFaceRecognize = null;
    //public static event Action onNullTrack = null; 

#if UNITY_ANDROID && !UNITY_EDITOR
	static AndroidJavaClass androidJavaClass {
		get {
			init();
			return jc;
		}
	}
	static AndroidJavaClass jc = null;
#else
    static bool isInitialized = false;
#endif

    class KiwiCallback : AndroidJavaProxy
    {

        public KiwiCallback() : base("com.u2a.sdk.NuwaCallback") { }

        void onWikiServiceStart()
        {
            if (Nuwa.onWikiServiceStart != null)
            {
                Nuwa.onWikiServiceStart();
            }
        }

        void onWikiServiceStop()
        {
            if (Nuwa.onWikiServiceStop != null)
            {
                Nuwa.onWikiServiceStop();
            }
        }

        void onWikiServiceCrash()
        {
            if (Nuwa.onWikiServiceCrash != null)
            {
                Nuwa.onWikiServiceCrash();
            }
        }

        void onWikiServiceRecovery()
        {
            if (Nuwa.onWikiServiceRecovery != null)
            {
                Nuwa.onWikiServiceRecovery();
            }
        }

        void onStartOfMotionPlay(string motion)
        {
            if (Nuwa.onStartOfMotionPlay != null)
            {
                Nuwa.onStartOfMotionPlay(motion);
            }
        }

        void onPauseOfMotionPlay(string motion)
        {
            if (Nuwa.onPauseOfMotionPlay != null)
            {
                Nuwa.onPauseOfMotionPlay(motion);
            }
        }

        void onStopOfMotionPlay(string motion)
        {
            if (Nuwa.onStopOfMotionPlay != null)
            {
                Nuwa.onStopOfMotionPlay(motion);
            }
        }

        void onCompleteOfMotionPlay(string motion)
        {
            if (Nuwa.onCompleteOfMotionPlay != null)
            {
                Nuwa.onCompleteOfMotionPlay(motion);
            }
        }

        void onPlayBackOfMotionPlay(string motion)
        {
            if (Nuwa.onPlayBackOfMotionPlay != null)
            {
                Nuwa.onPlayBackOfMotionPlay(motion);
            }
        }

        void onErrorOfMotionPlay(int errorcode)
        {
            if (Nuwa.onErrorOfMotionPlay != null)
            {
                Nuwa.onErrorOfMotionPlay(errorcode);
            }
        }

        void onPrepareMotion(bool b, string s, float v)
        {
            if (Nuwa.onPrepareMotion != null)
            {
                Nuwa.onPrepareMotion(b, s, v);
            }
        }

        void onCameraOfMotionPlay(String motion)
        {
            if (Nuwa.onCameraOfMotionPlay != null)
            {
                Nuwa.onCameraOfMotionPlay(motion);
            }
        }

        void onGetCameraPose(float x, float y, float z, float Xx, float Yx, float Zx, float Xy, float Yy, float Zy, float Xz, float Yz, float Zz)
        {
            if (Nuwa.onGetCameraPose != null)
            {
                CameraPosition cameraPosition = new CameraPosition();
                cameraPosition.x = x;
                cameraPosition.y = y;
                cameraPosition.z = z;
                cameraPosition.Xx = Xx;
                cameraPosition.Yx = Yx;
                cameraPosition.Zx = Zx;
                cameraPosition.Xy = Xy;
                cameraPosition.Yy = Yy;
                cameraPosition.Zy = Zy;
                cameraPosition.Xz = Xz;
                cameraPosition.Yz = Yz;
                cameraPosition.Zz = Zz;
                Nuwa.onGetCameraPose(cameraPosition);
            }
        }

        void onTouchEvent(int type, int touch)
        {
            switch (touch)
            {
                case 0:
                    if (Nuwa.onTouchEnd != null)
                        Nuwa.onTouchEnd((TouchEventType)type);
                    break;
                case 1:
                    if (Nuwa.onTouchBegan != null)
                        Nuwa.onTouchBegan((TouchEventType)type);
                    break;
            }
        }

        void onTap(int type)
        {
            if (Nuwa.onTap != null)
                Nuwa.onTap((TouchEventType)type);
        }

        void onLongPress(int type)
        {
            if (Nuwa.onLongPress != null)
                Nuwa.onLongPress((TouchEventType)type);
        }

        void onPIREvent(int val)
        {
            if (Nuwa.onPIREvent != null)
            {
                Nuwa.onPIREvent(val);
            }
        }

        void onWindowSurfaceReady()
        {
            if (Nuwa.onWindowSurfaceReady != null)
            {
                Nuwa.onWindowSurfaceReady();
            }
        }

        void onWindowSurfaceDestroy()
        {
            if (Nuwa.onWindowSurfaceDestroy != null)
            {
                Nuwa.onWindowSurfaceDestroy();
            }
        }

        void onTouchEyes(int eyeLR, int type)
        {
            if (Nuwa.onTouchEyes != null)
            {
                Nuwa.onTouchEyes(eyeLR, type);
            }
        }

        void onFaceSpeaker(float direction)
        {
            if (Nuwa.onFaceSpeaker != null)
            {
                Nuwa.onFaceSpeaker(direction);
            }
        }

        void onWakeup(bool error, string score, float direction)
        {
            if (Nuwa.onWakeup != null)
            {
                Nuwa.onWakeup(error, new Nuwa.ScoreInfoOnWakeUp(score), direction);
            }
        }

        void onGrammarState(bool isError, string info)
        {
            if (Nuwa.onGrammarState != null)
            {
                Nuwa.onGrammarState(isError, info);
            }
        }

        void onMixUnderstandComplete(bool isError, AndroidJavaObject androidJavaResultType, string json)
        {
            ResultType resultType = (ResultType)Enum.Parse(typeof(ResultType), androidJavaResultType.Get<string>("name"));
            Debug.Log(string.Format("isError : {0}, Result Type : {1}, json : {2}", isError, resultType, json));

            if (!isError && resultType == ResultType.UNDERSTAND)
            {
                if (Nuwa.onMixUnderstandComplete != null)
                {
                    Nuwa.onMixUnderstandComplete(isError, resultType, json);
                }
            }
            else if (!isError && resultType == ResultType.LOCAL_COMMAND)
            {
                if (json == null || json.Length == 0)
                {
                    Debug.Log("Result Type : " + resultType + " json : 為空 ");
                    if (Nuwa.onLocalCommandException != null)
                        Nuwa.onLocalCommandException(resultType, string.Empty);
                }
                else if (Nuwa.onLocalCommandComplete != null)
                {
                    NuwaVoiceRecognition vreg = new NuwaVoiceRecognition(json);

                    Nuwa.onLocalCommandComplete(vreg);
                }
            }
            else if (Nuwa.onLocalCommandException != null)
            {
                if (Nuwa.onLocalCommandException != null)
                    Nuwa.onLocalCommandException(resultType, json);
            }
        }

        void onTTSComplete(bool error)
        {
            if (Nuwa.onTTSComplete != null)
            {
                Nuwa.onTTSComplete(error);
            }
        }

        void onActionEvent(int i, int i1)
        {
            if (Nuwa.onActionEvent != null)
            {
                Nuwa.onActionEvent(i, i1);
            }
        }

        void onDropSensorEvent(int i)
        {
            if (Nuwa.onDropSensorEvent != null)
            {
                Nuwa.onDropSensorEvent(i);
            }
        }

        void onConnected(bool isConnected)
        {
            if (Nuwa.onConnected != null)
            {
                Nuwa.onConnected(isConnected);
            }
        }

        void onOutput(AndroidJavaObject resultMap)
        {
            if (Nuwa.onOutput != null)
            {
                List<OutputData> outputDataSets = new List<OutputData>();
                int count = resultMap.CallStatic<int>("getSize");
                for (int i = 0; i < count; ++i)
                {
                    AndroidJavaObject outputData = resultMap.CallStatic<AndroidJavaObject>("getOutputData", i);
                    outputDataSets.Add(new OutputData(
                        outputData.Get<int>("id"),
                        (float)outputData.Get<long>("processTime") * .001f,
                        outputData.Get<string>("data")
                    ));
                }
                Nuwa.onOutput(outputDataSets.ToArray());
            }
            if (Nuwa.onTrack != null)
            {

                List<TrackData> trackData = new List<TrackData>();
                int count = resultMap.CallStatic<int>("getSize");

                int index = 0;
                for (int i = 0; i < count; ++i)
                {
                    AndroidJavaObject outputData = resultMap.CallStatic<AndroidJavaObject>("getOutputData", i);
                    int t = outputData.Get<int>("id");

                    if (t == 4)
                    {
                        var tempTrackData = JsonUtility.FromJson<TrackData>(outputData.Get<string>("data"));

                        trackData.Add(tempTrackData);
                        index++;
                    }

                }

                if (index == 0)
                {
                    Nuwa.onTrack(null);
                }
                else
                {
                    Nuwa.onTrack(trackData.ToArray());
                }
            }

            if (Nuwa.onFaceRecognize != null)
            {
                List<FaceRecognizeData> recognizeData = new List<FaceRecognizeData>();
                int count = resultMap.CallStatic<int>("getSize");

                for (int i = 0; i < count; ++i)
                {
                    AndroidJavaObject outputData = resultMap.CallStatic<AndroidJavaObject>("getOutputData", i);
                    int t = outputData.Get<int>("id");
                    string dataStr = outputData.Get<string>("data");
                    if(dataStr.Length > 0)
                    {
                        var tempRecognizeData = JsonUtility.FromJson<FaceRecognizeData>(dataStr);
                        recognizeData.Add(tempRecognizeData);
                        Debug.Log("NuwaUnity : " + JsonUtility.ToJson(tempRecognizeData));
                        Nuwa.onFaceRecognize(recognizeData.Count > 0 ? recognizeData.ToArray() : null);
                    }
                }
            }
        }

        void onSpeechState(AndroidJavaObject arg0, AndroidJavaObject arg1)
        {
            Debug.Log("onSpeechState, " + arg0.ToString() + "," + arg1.ToString());

        }


        public void onSpeakState(AndroidJavaObject arg0, AndroidJavaObject arg1)
        {

        }

        void onSpeech2TextComplete(bool isError, string json)
        {
            if (Nuwa.onSpeech2TextComplete != null)
            {
                Nuwa.onSpeech2TextComplete(isError, json);
            }
        }

        void onRelease()
        {
            if (Nuwa.onRelease != null)
            {
                Nuwa.onRelease();
            }
        }
    }
}
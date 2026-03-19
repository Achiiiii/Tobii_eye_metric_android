using System;
using UnityEngine;
using UnityEngine.Events;

public class NuwaEventTrigger : MonoBehaviour {

	public UnityEvent onWikiServiceStart = null;
	public UnityEvent onWikiServiceStop = null;
	public UnityEvent onWikiServiceCrash = null;
	public UnityEvent onWikiServiceRecovery = null;
	public UnityEventWithString onStartOfMotionPlay = null;
	public UnityEventWithString onPauseOfMotionPlay = null;
	public UnityEventWithString onStopOfMotionPlay = null;
	public UnityEventWithString onCompleteOfMotionPlay = null;
	public UnityEventWithString onPlayBackOfMotionPlay = null;
	public UnityEventWithInteger onErrorOfMotionPlay = null;
	public UnityEventOnPrepareMotion onPrepareMotion = null;
	public UnityEventWithString onCameraOfMotionPlay = null;
	public UnityEventWithCameraPosition onGetCameraPose = null;
	public UnityEventWithTouch onTouchBegan = null;
	public UnityEventWithTouch onTouchEnd = null;
	public UnityEventWithTouch onTap = null;
	public UnityEventWithTouch onLongPress = null;
	public UnityEventWithInteger onPIREvent = null;
	public UnityEvent onWindowSurfaceReady = null;
	public UnityEvent onWindowSurfaceDestroy = null;
	public UnityEventWithTwoIntegers onTouchEyes = null;
	public UnityEventWithFloat onFaceSpeaker = null;
	public UnityEventOnWakeUp onWakeup = null;
	public UnityEventOnMixUnderstandComplete onMixUnderstandComplete;
	public UnityEventOnLocalCommandComplete onLocalCommandComplete;
	public UnityEventOnLocalCommandException onLocalCommandException;
	public UnityEventOnGrammarState onGrammarState = null;
	public UnityEventWithBoolean onTTSComplete = null;
	public UnityEventWithTwoIntegers onActionEvent = null;
	public UnityEventWithInteger onDropSensorEvent = null;
	public UnityEventWithBoolean onConnected = null;
	public UnityEventOnOutput onOutput = null;

	[Serializable] public class UnityEventWithBoolean : UnityEvent<bool> {}
	[Serializable] public class UnityEventWithInteger : UnityEvent<int> {}
	[Serializable] public class UnityEventWithFloat : UnityEvent<float> {}
	[Serializable] public class UnityEventWithString : UnityEvent<string> {}
	[Serializable] public class UnityEventWithTwoIntegers : UnityEvent<int, int> {}
	[Serializable] public class UnityEventWithCameraPosition : UnityEvent<Nuwa.CameraPosition> {}
	[Serializable] public class UnityEventWithTouch : UnityEvent<Nuwa.TouchEventType> {}
	[Serializable] public class UnityEventOnPrepareMotion : UnityEvent<bool, string, float> {}
	[Serializable] public class UnityEventOnWakeUp : UnityEvent<bool, Nuwa.ScoreInfoOnWakeUp, float> {}
	[Serializable] public class UnityEventOnMixUnderstandComplete : UnityEvent<bool, Nuwa.ResultType, string> {}
	[Serializable] public class UnityEventOnLocalCommandComplete : UnityEvent<Nuwa.NuwaVoiceRecognition> {}
	[Serializable] public class UnityEventOnLocalCommandException : UnityEvent<Nuwa.ResultType, string> {}
	[Serializable] public class UnityEventOnGrammarState : UnityEvent<bool, string> {}
	[Serializable] public class GraphicRecognitionOutputDataInfo {
		public GraphicRecognitionOutputDataInfo(params Nuwa.OutputData[] outputDataInfo) {
			this.outputDataInfo = outputDataInfo;
		}
		public Nuwa.OutputData[] outputDataInfo;
	}
	[Serializable] public class UnityEventOnOutput : UnityEvent<GraphicRecognitionOutputDataInfo> {}

	void OnEnable() {
        
		Nuwa.init();
		Nuwa.onWikiServiceStart += onWikiServiceStart.Invoke;
		Nuwa.onWikiServiceStop += onWikiServiceStop.Invoke;
		Nuwa.onWikiServiceCrash += onWikiServiceCrash.Invoke;
		Nuwa.onWikiServiceRecovery += onWikiServiceRecovery.Invoke;
		Nuwa.onStartOfMotionPlay += onStartOfMotionPlay.Invoke;
		Nuwa.onPauseOfMotionPlay += onPauseOfMotionPlay.Invoke;
		Nuwa.onStopOfMotionPlay += onStopOfMotionPlay.Invoke;
		Nuwa.onCompleteOfMotionPlay += onCompleteOfMotionPlay.Invoke;
		Nuwa.onPlayBackOfMotionPlay += onPlayBackOfMotionPlay.Invoke;
		Nuwa.onErrorOfMotionPlay += onErrorOfMotionPlay.Invoke;
		Nuwa.onPrepareMotion += onPrepareMotion.Invoke;
		Nuwa.onCameraOfMotionPlay += onCameraOfMotionPlay.Invoke;
		Nuwa.onGetCameraPose += onGetCameraPose.Invoke;
		Nuwa.onTouchBegan += onTouchBegan.Invoke;
		Nuwa.onTouchEnd += onTouchEnd.Invoke;
		Nuwa.onTap += onTap.Invoke;
		Nuwa.onLongPress += onLongPress.Invoke;
		Nuwa.onPIREvent += onPIREvent.Invoke;
		Nuwa.onWindowSurfaceReady += onWindowSurfaceReady.Invoke;
		Nuwa.onWindowSurfaceDestroy += onWindowSurfaceDestroy.Invoke;
		Nuwa.onTouchEyes += onTouchEyes.Invoke;
		Nuwa.onFaceSpeaker += onFaceSpeaker.Invoke;
		Nuwa.onWakeup += onWakeup.Invoke;
		Nuwa.onMixUnderstandComplete += onMixUnderstandComplete.Invoke;
		Nuwa.onLocalCommandComplete += onLocalCommandComplete.Invoke;
		Nuwa.onLocalCommandException += onLocalCommandException.Invoke;
		Nuwa.onGrammarState += onGrammarState.Invoke;
		Nuwa.onTTSComplete += onTTSComplete.Invoke;
		Nuwa.onActionEvent += onActionEvent.Invoke;
		Nuwa.onDropSensorEvent += onDropSensorEvent.Invoke;
		Nuwa.onConnected += onConnected.Invoke;
		Nuwa.onOutput += InvokeOnOutputEvent;
	}

	void OnDisable() {
		Nuwa.onWikiServiceStart -= onWikiServiceStart.Invoke;
		Nuwa.onWikiServiceStop -= onWikiServiceStop.Invoke;
		Nuwa.onWikiServiceCrash -= onWikiServiceCrash.Invoke;
		Nuwa.onWikiServiceRecovery -= onWikiServiceRecovery.Invoke;
		Nuwa.onStartOfMotionPlay -= onStartOfMotionPlay.Invoke;
		Nuwa.onPauseOfMotionPlay -= onPauseOfMotionPlay.Invoke;
		Nuwa.onStopOfMotionPlay -= onStopOfMotionPlay.Invoke;
		Nuwa.onCompleteOfMotionPlay -= onCompleteOfMotionPlay.Invoke;
		Nuwa.onPlayBackOfMotionPlay -= onPlayBackOfMotionPlay.Invoke;
		Nuwa.onErrorOfMotionPlay -= onErrorOfMotionPlay.Invoke;
		Nuwa.onPrepareMotion -= onPrepareMotion.Invoke;
		Nuwa.onCameraOfMotionPlay -= onCameraOfMotionPlay.Invoke;
		Nuwa.onGetCameraPose -= onGetCameraPose.Invoke;
		Nuwa.onTouchBegan -= onTouchBegan.Invoke;
		Nuwa.onTouchEnd -= onTouchEnd.Invoke;
		Nuwa.onTap -= onTap.Invoke;
		Nuwa.onLongPress -= onLongPress.Invoke;
		Nuwa.onPIREvent -= onPIREvent.Invoke;
		Nuwa.onWindowSurfaceReady -= onWindowSurfaceReady.Invoke;
		Nuwa.onWindowSurfaceDestroy -= onWindowSurfaceDestroy.Invoke;
		Nuwa.onTouchEyes -= onTouchEyes.Invoke;
		Nuwa.onFaceSpeaker -= onFaceSpeaker.Invoke;
		Nuwa.onWakeup -= onWakeup.Invoke;
		Nuwa.onMixUnderstandComplete -= onMixUnderstandComplete.Invoke;
		Nuwa.onLocalCommandComplete -= onLocalCommandComplete.Invoke;
		Nuwa.onLocalCommandException -= onLocalCommandException.Invoke;
		Nuwa.onGrammarState -= onGrammarState.Invoke;
		Nuwa.onTTSComplete -= onTTSComplete.Invoke;
		Nuwa.onActionEvent -= onActionEvent.Invoke;
		Nuwa.onDropSensorEvent -= onDropSensorEvent.Invoke;
		Nuwa.onConnected -= onConnected.Invoke;
		Nuwa.onOutput -= InvokeOnOutputEvent;
	}

	void InvokeOnOutputEvent(Nuwa.OutputData[] outputData) {
		onOutput.Invoke(new GraphicRecognitionOutputDataInfo(outputData));
	}
}
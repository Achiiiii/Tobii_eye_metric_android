using System.Collections;
using System.Collections.Generic;
using Tobii;
using UnityEngine;
using UnityEngine.UI;

public class DetectDistance : MonoBehaviour
{
    public enum DistanceZone
    {
        TooFar,
        InRange,
        TooClose
    }

    // Accepted head-to-display distance; the same range as the original check (0.5 < 1 - distance < 0.7).
    public const float MinDistanceMeters = 0.3f;
    public const float MaxDistanceMeters = 0.5f;
    public const float HoldSeconds = 3f;
    // A zone change must persist this long before guidance switches, so the boundary doesn't flicker.
    private const float ZoneSettleSeconds = 0.4f;
    private const float ReminderSeconds = 5f;
    private const float MinSpeechGapSeconds = 3f;
    private const float IntroSpeechSeconds = 8f;

    public GameObject headGO;
    public GameObject displayGO;
    public GameObject canvasTrackBox;
    public float moverScale;
    public AudioSource audioSource;
    public AudioClip closerAudio;
    public AudioClip farerAudio;
    public GameObject pointer;
    public GazeCalibrationManager gazeCalibrationManager;
    public event System.Action HeadPositionConfirmed;

    public bool IsChecking => _locker;
    public float DistanceMeters { get; private set; }
    public DistanceZone Zone { get; private set; } = DistanceZone.TooFar;
    public float HoldProgress => Mathf.Clamp01(_validateTime / HoldSeconds);

    private Color _colorMoverGood;
    private Color _colorMoverBad;
    private Color _colorEyeGood;
    private Color _colorEyeBad;

    private Rect _box;
    private Image _mover;
    private Image _colorPanel;
    private float _validateTime;
    private bool _locker = false;
    private bool _zoneKnown = false;
    private DistanceZone _pendingZone;
    private float _pendingSince;
    private float _lastGuidanceTime;
    private float _nextSpeechTime;

    void Start()
    {
        var box = canvasTrackBox.transform.Find("ImageBox");
        _mover = box.Find("PanelMover").GetComponent<Image>();
        _colorPanel = box.Find("ImagePanel").GetComponent<Image>();
        _colorMoverGood = new Color32(0x3F, 0xA9, 0x6B, 255);
        _colorMoverBad = new Color32(0xE0, 0x70, 0x48, 255);
    }
    void Update()
    {
        if (!_locker)
            return;

        float headZ = headGO.transform.position.z;
        float displayZ = displayGO.transform.position.z;

        moverScale = PositionMover(headZ, displayZ);
        DistanceMeters = Mathf.Abs(headZ - displayZ);

        var rawZone = DistanceMeters > MaxDistanceMeters ? DistanceZone.TooFar
            : DistanceMeters < MinDistanceMeters ? DistanceZone.TooClose
            : DistanceZone.InRange;
        UpdateZone(rawZone);
        _colorPanel.color = Zone == DistanceZone.InRange ? _colorMoverGood : _colorMoverBad;

        if (rawZone == DistanceZone.InRange) _validateTime += Time.deltaTime;
        else _validateTime = 0;
        if (_validateTime >= HoldSeconds)
        {
            _validateTime = 0;
            _locker = false;

            canvasTrackBox.SetActive(false);
            HeadPositionConfirmed?.Invoke();
            Debug.Log("validate");
        }
    }

    public void OpenLock()
    {
        _validateTime = 0;
        _zoneKnown = false;
        _locker = true;
        PlayTTS("請將頭部距離機器人大約四十公分，臉部與螢幕保持平行。調整到畫面變成綠色後，請保持不動");
        _nextSpeechTime = Time.time + IntroSpeechSeconds;
    }

    public void CloseLock()
    {
        _locker = false;
        _validateTime = 0;
    }

    private void UpdateZone(DistanceZone rawZone)
    {
        if (!_zoneKnown)
        {
            _zoneKnown = true;
            Zone = rawZone;
            _pendingZone = rawZone;
            _lastGuidanceTime = Time.time;
            return;
        }

        if (rawZone != _pendingZone)
        {
            _pendingZone = rawZone;
            _pendingSince = Time.time;
        }

        if (_pendingZone != Zone && Time.time - _pendingSince >= ZoneSettleSeconds)
        {
            Zone = _pendingZone;
            SpeakGuidance();
        }
        else if (Zone != DistanceZone.InRange && Time.time - _lastGuidanceTime >= ReminderSeconds)
        {
            SpeakGuidance();
        }
    }

    private void SpeakGuidance()
    {
        // Counted even when speech is suppressed so reminders stay spaced out.
        _lastGuidanceTime = Time.time;
        if (Time.time < _nextSpeechTime)
            return;

        switch (Zone)
        {
            case DistanceZone.TooFar:
                PlayTTS("請往前靠近一點");
                break;
            case DistanceZone.TooClose:
                PlayTTS("請往後遠離一點");
                break;
            default:
                PlayTTS("很好，請保持不動");
                break;
        }
        _nextSpeechTime = Time.time + MinSpeechGapSeconds;
    }

    private void AudioPlay(AudioClip clip)
    {
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }
    private float PositionMover(float z1, float z2)
    {

        var scale = 1 - Mathf.Abs(z1 - z2);

        // Set the scale.
        _mover.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, scale);

        return scale;
    }

    private void PlayTTS(string text)
    {
        if (text == "")
            return;
        Nuwa.stopTTS();
        Nuwa.startTTS(text);
    }
}

using System.Collections;
using System.Collections.Generic;
using Tobii;
using UnityEngine;
using UnityEngine.UI;

public class DetectDistance : MonoBehaviour
{
    public GameObject headGO;
    public GameObject displayGO;
    public GameObject canvasTrackBox;
    public float moverScale;
    public AudioSource audioSource;
    public AudioClip closerAudio;
    public AudioClip farerAudio;
    public GameObject pointer;
    public GazeCalibrationManager gazeCalibrationManager;

    private Color _colorMoverGood;
    private Color _colorMoverBad;
    private Color _colorEyeGood;
    private Color _colorEyeBad;

    private Rect _box;
    private Image _mover;
    private Image _colorPanel;
    private float _time = 0;
    private float _validateTime;
    private bool _locker = false;

    void Start()
    {
        var box = canvasTrackBox.transform.Find("ImageBox");
        _mover = box.Find("PanelMover").GetComponent<Image>();
        _colorPanel = box.Find("ImagePanel").GetComponent<Image>();
        _colorMoverGood = new Color32(23, 66, 57, 255);
        _colorMoverBad = new Color32(72, 26, 37, 255);
    }
    void Update()
    {
        if (_locker)
        {
            float headZ = headGO.transform.position.z;
            float displayZ = displayGO.transform.position.z;

            // Debug.Log($"headZ: {headZ}, displayZ: {displayZ}\ndistance: {headZ - displayZ}");

            moverScale = PositionMover(headZ, displayZ);
            _time += Time.deltaTime;
            if (moverScale < 0.5f || moverScale > 0.7f)
            {
                if (_time >= 5)
                {
                    if (moverScale < 0.5f)
                    {
                        _time = 0;
                        // AudioPlay(closerAudio);
                        PlayTTS("頭部請再靠近一點");
                    }
                    if (moverScale > 0.7f)
                    {
                        _time = 0;
                        // AudioPlay(farerAudio);
                        PlayTTS("頭部請再遠離一點");
                    }
                }
            }
            else _time = 0;

            if (moverScale > 0.5f && moverScale < 0.7f) _validateTime += Time.deltaTime;
            else _validateTime = 0;
            if (_validateTime >= 3)
            {
                _validateTime = 0;
                _locker = false;

                canvasTrackBox.SetActive(false);
                gazeCalibrationManager.SetTrialCountDown("right");
                Debug.Log("validate");
            }
        }

    }
    public void OpenLock()
    {
        _locker = true;
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

        // Set the color.
        _colorPanel.color = Color.Lerp(_colorMoverGood, _colorMoverBad, Mathf.Abs(0.5f - scale) * 2f);
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

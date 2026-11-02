using System.Collections.Generic;
using UnityEngine;

// KebbiKit sound library: short cues in one voice - a small friendly robot (round blips, little chirps, a glassy bell),
// all in D major pentatonic so any two sound well together. The cues are WAVs in Resources/KebbiKit/Sounds, listed in
// Resources/KebbiKit/sounds.json and made by Tools~/gen_sounds.py.
//   KebbiSound.Install(transform);   once, at start-up
//   KebbiSound.Play("ui_tap");
// A few voices so cues can overlap, each play nudged +-2 % in pitch so repeats do not tire, and everything turned down
// while Kebbi speaks (Nuwa's speech is not Unity audio, so the listener volume ducks only the app's own sounds).
public class KebbiSound : MonoBehaviour
{
    private const int Voices = 4;
    private const float DuckVolume = 0.4f;

    // overall volume of the cues (0-1)
    public static float Volume = 1f;

    private static KebbiSound s_instance;
    private static readonly Dictionary<string, AudioClip> s_clips = new Dictionary<string, AudioClip>();

    private readonly AudioSource[] _sources = new AudioSource[Voices];
    private int _next;
    private volatile bool _ttsEnded;
    private float _speakingUntil = -1f, _duck = 1f;

    public static void Install(Transform parent)
    {
        if (s_instance != null)
            return;
        var go = new GameObject("KebbiSound");
        go.transform.SetParent(parent, false);
        s_instance = go.AddComponent<KebbiSound>();
        for (int i = 0; i < Voices; i++)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            s_instance._sources[i] = source;
        }
        Nuwa.onStartTTs += s_instance.OnSpeechStart;
        Nuwa.onTTSComplete += s_instance.OnSpeechEnd;
    }

    public static void Play(string name, float volume = 1f)
    {
        if (s_instance == null)
            return;
        var clip = Clip(name);
        if (clip == null)
            return;
        var source = s_instance._sources[s_instance._next];
        s_instance._next = (s_instance._next + 1) % Voices;
        source.pitch = Random.Range(0.98f, 1.02f);
        source.PlayOneShot(clip, volume * Volume);
    }

    public static AudioClip Clip(string name)
    {
        if (s_clips.TryGetValue(name, out var clip))
            return clip;
        clip = Resources.Load<AudioClip>("KebbiKit/Sounds/" + name);
        if (clip == null)
            Debug.LogWarning("[KebbiSound] no cue " + name);
        s_clips[name] = clip;
        return clip;
    }

    // ==================== ducking ====================

    // Runs on the main thread (Nuwa.startTTS). The end arrives from the robot service on its own thread, so it only
    // sets a flag; a length-based estimate ends the duck too, in case an end never comes (stopTTS).
    private void OnSpeechStart()
    {
        string text = Nuwa.LastTTS ?? "";
        _speakingUntil = Time.unscaledTime + 1.5f + text.Length * 0.2f;
        _ttsEnded = false;
    }

    private void OnSpeechEnd(bool error) => _ttsEnded = true;

    private void Update()
    {
        if (_ttsEnded)
        {
            _ttsEnded = false;
            _speakingUntil = -1f;
        }
        bool speaking = Time.unscaledTime < _speakingUntil;
        _duck = Mathf.MoveTowards(_duck, speaking ? DuckVolume : 1f, Time.unscaledDeltaTime * 4f);
        AudioListener.volume = _duck;
    }

    private void OnDestroy()
    {
        Nuwa.onStartTTs -= OnSpeechStart;
        Nuwa.onTTSComplete -= OnSpeechEnd;
        AudioListener.volume = 1f;
    }
}

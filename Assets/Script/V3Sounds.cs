using System.Collections.Generic;
using UnityEngine;

// V3 sound effects, all generated in code (no audio assets): short, mid-pitched (500-2000 Hz suits older ears),
// "instrument" clicks and soft tones. Nothing in the test tells right from wrong: one neutral answer click, and
// the result chime is the same for every score. While Kebbi speaks, all app sound is turned down (ducking).
public class V3Sounds : MonoBehaviour
{
    public enum Cue
    {
        Press,       // a touch key
        Select,      // an answer chosen / an option ticked
        Unselect,    // an option unticked
        InRange,     // distance or height came right; back in place after a pause
        Done,        // head position confirmed, calibration complete
        Fail,        // calibration did not succeed
        DotShow,     // a calibration dot appears
        DotDone,     // a calibration dot taken
        Count,       // countdown 3, 2
        CountLast,   // countdown 1
        Pause,       // the drift pause card appears
        Result,      // the result page
    }

    private const int Rate = 44100;
    private const float DuckVolume = 0.4f;

    private static V3Sounds s_instance;
    private static readonly Dictionary<Cue, AudioClip> s_clips = new Dictionary<Cue, AudioClip>();

    private AudioSource _source;
    private volatile bool _ttsEnded;
    private float _speakingUntil = -1f;
    private float _duck = 1f;

    public static void Install(Transform parent)
    {
        if (s_instance != null)
            return;
        var go = new GameObject("V3Sounds");
        go.transform.SetParent(parent, false);
        s_instance = go.AddComponent<V3Sounds>();
        s_instance._source = go.AddComponent<AudioSource>();
        s_instance._source.playOnAwake = false;
        Nuwa.onStartTTs += s_instance.OnSpeechStart;
        Nuwa.onTTSComplete += s_instance.OnSpeechEnd;
    }

    public static void Play(Cue cue, float volume = 1f)
    {
        if (s_instance == null)
            return;
        s_instance._source.PlayOneShot(Clip(cue), volume);
    }

    public static AudioClip Clip(Cue cue)
    {
        if (!s_clips.TryGetValue(cue, out var clip))
            s_clips[cue] = clip = Make(cue);
        return clip;
    }

    // ==================== ducking ====================

    // Runs on the main thread (Nuwa.startTTS). The end arrives from the robot service on its own thread, so it
    // only sets a flag; a length-based estimate ends the duck too, in case an end never comes (stopTTS).
    private void OnSpeechStart()
    {
        string text = Nuwa.LastTTS ?? "";
        _speakingUntil = Time.unscaledTime + 1.5f + text.Length * (V3.Lang == 1 ? 0.07f : 0.24f);
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
        // the robot's speech is not Unity audio, so the listener volume turns down only the app's own sounds
        AudioListener.volume = _duck;
    }

    private void OnDestroy()
    {
        Nuwa.onStartTTs -= OnSpeechStart;
        Nuwa.onTTSComplete -= OnSpeechEnd;
        AudioListener.volume = 1f;
    }

    // ==================== synthesis ====================

    private static AudioClip Make(Cue cue)
    {
        switch (cue)
        {
            case Cue.Press: return Build("Press", 0.06f, Click(1400f, 0.018f, 0.3f));
            case Cue.Select: return Build("Select", 0.12f, Tone(880f, 0f, 0.09f, 0.28f));
            case Cue.Unselect: return Build("Unselect", 0.1f, Tone(587f, 0f, 0.07f, 0.22f));
            case Cue.InRange: return Build("InRange", 0.25f, Tone(740f, 0f, 0.2f, 0.25f, 0.012f));
            case Cue.Done: return Build("Done", 0.35f, Tone(523f, 0f, 0.12f, 0.26f), Tone(784f, 0.11f, 0.2f, 0.26f));
            case Cue.Fail: return Build("Fail", 0.4f, Tone(440f, 0f, 0.14f, 0.22f), Tone(370f, 0.14f, 0.22f, 0.22f));
            case Cue.DotShow: return Build("DotShow", 0.18f, Tone(698f, 0f, 0.15f, 0.24f));
            case Cue.DotDone: return Build("DotDone", 0.12f, Tone(1047f, 0f, 0.09f, 0.22f));
            case Cue.Count: return Build("Count", 0.1f, Tone(660f, 0f, 0.07f, 0.26f));
            case Cue.CountLast: return Build("CountLast", 0.14f, Tone(880f, 0f, 0.1f, 0.28f));
            case Cue.Pause: return Build("Pause", 0.32f, Tone(659f, 0f, 0.12f, 0.24f), Tone(494f, 0.12f, 0.18f, 0.24f));
            case Cue.Result: return Build("Result", 0.7f, Tone(523f, 0f, 0.3f, 0.2f), Tone(659f, 0.1f, 0.3f, 0.2f), Tone(784f, 0.2f, 0.45f, 0.22f));
            default: return Build("Silence", 0.01f);
        }
    }

    private delegate float Voice(float t);

    // A soft bell-ish tone: sine with a little octave, 4 ms attack, exponential decay over `length`.
    private static Voice Tone(float hz, float start, float length, float gain, float attack = 0.004f)
    {
        return t =>
        {
            float u = t - start;
            if (u < 0f || u > length * 2.5f)
                return 0f;
            float env = Mathf.Clamp01(u / attack) * Mathf.Exp(-u * 4.6f / length);
            float w = 2f * Mathf.PI * hz * u;
            return (Mathf.Sin(w) + 0.18f * Mathf.Sin(2f * w)) * env * gain;
        };
    }

    // An instrument-key click: a short tone burst with a tiny noise transient.
    private static Voice Click(float hz, float length, float gain)
    {
        var random = new System.Random(7);
        return t =>
        {
            if (t > length * 3f)
                return 0f;
            float env = Mathf.Clamp01(t / 0.0015f) * Mathf.Exp(-t * 4.6f / length);
            float noise = t < 0.004f ? (float)(random.NextDouble() * 2.0 - 1.0) * 0.5f : 0f;
            return (Mathf.Sin(2f * Mathf.PI * hz * t) + noise) * env * gain;
        };
    }

    private static AudioClip Build(string name, float seconds, params Voice[] voices)
    {
        int count = Mathf.CeilToInt(Rate * seconds);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate, v = 0f;
            foreach (var voice in voices)
                v += voice(t);
            // fade the last 10 ms so nothing clicks at the end
            float tail = Mathf.Clamp01((seconds - t) / 0.01f);
            samples[i] = Mathf.Clamp(v * tail, -1f, 1f);
        }
        var clip = AudioClip.Create("V3" + name, count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

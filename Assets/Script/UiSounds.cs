using UnityEngine;

// Small generated sound effects so no audio asset has to be imported.
public static class UiSounds
{
    private static AudioClip _tick;

    /// <summary>Short neutral click used for selections and countdown beats.</summary>
    public static AudioClip Tick => _tick != null ? _tick : (_tick = CreateTick());

    private static AudioClip CreateTick()
    {
        const int sampleRate = 44100;
        const float duration = 0.07f;
        const float frequency = 1100f;

        int count = Mathf.CeilToInt(sampleRate * duration);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 60f) * Mathf.Clamp01(t * 800f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.35f;
        }

        var clip = AudioClip.Create("UiTick", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

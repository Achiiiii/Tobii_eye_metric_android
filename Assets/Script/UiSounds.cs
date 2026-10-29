using UnityEngine;

// The selection click, shared with scripts that predate V3Sounds.
public static class UiSounds
{
    private static AudioClip _tick;

    /// <summary>Short neutral click used for selections (V3: the same tone as every other selection).</summary>
    public static AudioClip Tick => _tick != null ? _tick : (_tick = V3Sounds.Clip(V3Sounds.Cue.Select));
}

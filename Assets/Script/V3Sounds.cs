using System.Collections.Generic;
using UnityEngine;

// The app's sound cues, played from the KebbiKit sound library (KebbiSound). Nothing in the test tells right from
// wrong: one neutral answer sound, and the result sound is the same for every score.
public static class V3Sounds
{
    public enum Cue
    {
        Press,       // a touch key
        Select,      // an answer chosen / an option ticked
        Unselect,    // an option unticked
        Back,        // back, previous question, cancel
        Toggle,      // the language switch
        InRange,     // distance or height came right; back in place after a pause
        Done,        // head position confirmed, calibration complete
        Fail,        // calibration did not succeed
        DotShow,     // a calibration dot appears
        DotDone,     // a calibration dot taken
        Count,       // countdown 3, 2
        CountLast,   // countdown 1
        Pause,       // the drift pause card, "still there?"
        Result,      // the result page
        Page,        // a flow page comes in
        Hello,       // Kebbi greets someone on the home
        Happy,       // the practice done
        Bye,         // leaving the app
        Intro,       // the home's opening
    }

    private static readonly Dictionary<Cue, string> Names = new Dictionary<Cue, string>
    {
        { Cue.Press, "ui_tap" }, { Cue.Select, "ui_confirm" }, { Cue.Unselect, "ui_toggle_off" }, { Cue.Back, "ui_back" },
        { Cue.Toggle, "ui_toggle_on" }, { Cue.InRange, "state_inrange" }, { Cue.Done, "flow_done" }, { Cue.Fail, "state_softfail" },
        { Cue.DotShow, "test_dot_show" }, { Cue.DotDone, "test_dot_done" }, { Cue.Count, "flow_tick" }, { Cue.CountLast, "flow_go" },
        { Cue.Pause, "state_attention" }, { Cue.Result, "kebbi_cheer" }, { Cue.Page, "flow_page" }, { Cue.Hello, "kebbi_hi" },
        { Cue.Happy, "kebbi_happy" }, { Cue.Bye, "kebbi_bye" }, { Cue.Intro, "brand_intro" },
    };

    public static void Install(Transform parent) => KebbiSound.Install(parent);

    public static void Play(Cue cue, float volume = 1f) => KebbiSound.Play(Names[cue], volume);

    public static AudioClip Clip(Cue cue) => KebbiSound.Clip(Names[cue]);
}

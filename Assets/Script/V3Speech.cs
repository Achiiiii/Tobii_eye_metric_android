using System.Collections.Generic;
using UnityEngine;

// English speech: when the app is in English, every line the app speaks (the Chinese strings passed to
// Nuwa.startTTS across the scripts) is looked up here and spoken in English instead. A line missing from the
// table is spoken in Chinese and logged, so it can be added.
public static class V3Speech
{
    private static Dictionary<string, string> s_table;

    public static void Install()
    {
        Nuwa.English = zh => V3.Lang == 1 ? Translate(zh) : null;
    }

    public static string Translate(string zh)
    {
        if (string.IsNullOrEmpty(zh))
            return null;
        if (s_table == null)
            s_table = Build();
        if (s_table.TryGetValue(zh.Trim(), out var en))
            return en;
        Debug.LogWarning("[TTS] no English for: " + zh);
        return null;
    }

    private static Dictionary<string, string> Build()
    {
        var t = new Dictionary<string, string>
        {
            // home
            { "請選擇要進行的測驗", "Please choose a test." },
            // head position (DetectDistance)
            { "請將頭部距離機器人大約四十公分，臉部與螢幕保持平行。調整到畫面變成綠色後，請保持不動",
              "Please keep your head about forty centimetres from Kebbi, with your face parallel to the screen. When the screen turns green, please hold still." },
            { "請往前靠近一點", "Please move a little closer." },
            { "請往後遠離一點", "Please move back a little." },
            { "眼睛比凱比的鏡頭高，請把凱比墊高，例如放在書本或盒子上，或把座椅調低",
              "Your eyes are above Kebbi's camera. Please raise Kebbi, for example on some books or a box, or lower your seat." },
            { "眼睛比凱比的鏡頭低，請把座椅調高，或把凱比放低", "Your eyes are below Kebbi's camera. Please raise your seat, or lower Kebbi." },
            { "很好，請保持不動", "Good, please hold still." },
            { "頭部位置確認完成", "Head position confirmed." },
            // calibration
            { "眼睛校正開始，請保持頭部不動，依序看著紫色圓點", "Calibration is starting. Keep your head still and look at each violet dot in turn." },
            { "校正沒有成功，請按重新校正，再看一次圓點", "Calibration did not succeed. Please press Recalibrate and look at the dots again." },
            // practice
            { "這個圓圈會跟著您的眼睛。請看著紫色的E不動，就能選取。", "This ring follows your eyes. Look at the violet E and hold still to select it." },
            { "做得很好！請看著開始測驗的按鈕", "Well done! Now look at the Start test button." },
            // countdown
            { "右眼測驗，即將開始", "Right eye test, starting soon." },
            { "左眼測驗，即將開始", "Left eye test, starting soon." },
            { "雙眼測驗，即將開始", "Both eyes test, starting soon." },
            // drift pause (PositionGuide)
            { "請往左移一點", "Please move a little to the left." },
            { "請往右移一點", "Please move a little to the right." },
            { "請回到螢幕正前方", "Please come back in front of the screen." },
        };

        // instructions before each calibration (GazeCalibrationManager): head hint + cover hint
        var heads = new[] { ("接下來請保持頭部不動。", "From now on, keep your head still. "), ("請保持頭部不動。", "Keep your head still. ") };
        var covers = new[]
        {
            ("請先遮擋左邊眼睛，使用右眼檢測", "First cover your left eye, and test with your right eye."),
            ("請遮擋右邊眼睛，使用左眼檢測", "Now cover your right eye, and test with your left eye."),
            ("請勿遮擋眼睛，直接進行檢測", "Don't cover your eyes; we test both together."),
            ("", ""),
        };
        foreach (var (hz, he) in heads)
            foreach (var (cz, ce) in covers)
                t[hz + cz] = (he + ce).Trim();

        // the pause card opens with its title, then the direction
        foreach (var dir in new[] { "請往左移一點", "請往右移一點", "請往前靠近一點", "請往後遠離一點" })
            t["請回到剛才的位置，" + dir] = "Please go back to where you were. " + t[dir];

        // questions as QA speaks them: the words without line breaks, plus what to do on the choice questions
        var qs = V3Questionnaire.Questions;
        for (int i = 0; i < qs.Length; i++)
        {
            string zh = qs[i].Words.Replace("\n", "");
            string en = qs[i].WordsEn.Replace("\n", " ");
            if (i == 3 || i == 5)
            {
                zh += "請勾選所有符合的項目";
                en += " Please choose all that apply.";
            }
            else if (i == 6)
            {
                zh += "請選擇一項";
                en += " Please choose one.";
            }
            t[zh] = en;
        }

        // result messages (MetricTest)
        foreach (var pair in V3Test.ResultEn)
            t[pair.Key] = pair.Value;
        return t;
    }
}

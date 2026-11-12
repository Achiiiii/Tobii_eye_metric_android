using System.Collections;
using UnityEngine;

// KebbiKit LED library: Kebbi's head, chest and hand lights in a few named looks. The app takes the lights from the
// system while it is in front (Take) and must give them back when it leaves (Release - done here on pause and quit).
//   KebbiLight.Install(transform);  KebbiLight.Take();
//   KebbiLight.Breathe(KebbiLight.Violet);  KebbiLight.Flash(KebbiLight.Green);  KebbiLight.Steady(KebbiLight.Violet, 0.5f);
public class KebbiLight : MonoBehaviour
{
    public static readonly Color Violet = new Color32(0x6E, 0x56, 0xCF, 0xFF);
    public static readonly Color Green = new Color32(0x56, 0xCF, 0x9F, 0xFF);
    public static readonly Color SoftRed = new Color32(0xE5, 0x48, 0x4D, 0xFF);

    private static readonly Nuwa.LEDPosition[] Body = { Nuwa.LEDPosition.Chest, Nuwa.LEDPosition.LeftHand, Nuwa.LEDPosition.RightHand };

    private static KebbiLight s_instance;
    private static bool s_taken, s_wanted, s_enabled = true;

    // The app's setting: off, the lights stay with the system whatever the app asks for.
    public static bool Enabled
    {
        get => s_enabled;
        set
        {
            if (s_enabled == value)
                return;
            s_enabled = value;
            if (value && s_wanted)
                Take();
            else if (!value && s_taken)
            {
                s_taken = false;
                Nuwa.enableSystemLED();
            }
        }
    }
    private static Color s_base = Violet;
    private static bool s_breathing = true;
    private Coroutine _flash;

    public static void Install(Transform parent)
    {
        if (s_instance != null)
            return;
        var go = new GameObject("KebbiLight");
        go.transform.SetParent(parent, false);
        s_instance = go.AddComponent<KebbiLight>();
    }

    // The app controls the lights (the system's own patterns stop).
    public static void Take()
    {
        s_wanted = true;
        if (!s_enabled)
            return;
        s_taken = true;
        Nuwa.disableSystemLED();
        Apply();
    }

    // The lights go back to the system.
    public static void Release()
    {
        s_wanted = false;
        s_taken = false;
        Nuwa.enableSystemLED();
    }

    // A slow breathing glow in a colour (the resting look).
    public static void Breathe(Color color)
    {
        s_base = color;
        s_breathing = true;
        Apply();
    }

    // A steady glow (alpha = brightness): while a test runs, nothing on the robot should change.
    public static void Steady(Color color, float brightness = 1f)
    {
        s_base = new Color(color.r * brightness, color.g * brightness, color.b * brightness, 1f);
        s_breathing = false;
        Apply();
    }

    // Two quick flashes of a colour, then back to the current look.
    public static void Flash(Color color)
    {
        if (s_instance == null || !s_taken)
            return;
        if (s_instance._flash != null)
            s_instance.StopCoroutine(s_instance._flash);
        s_instance._flash = s_instance.StartCoroutine(s_instance.FlashRun(color));
    }

    private IEnumerator FlashRun(Color color)
    {
        for (int i = 0; i < 2; i++)
        {
            Set(color, false);
            yield return new WaitForSecondsRealtime(0.18f);
            Set(Color.black, false);
            yield return new WaitForSecondsRealtime(0.12f);
        }
        _flash = null;
        Apply();
    }

    private static void Apply()
    {
        if (!s_taken)
            return;
        Set(s_base, s_breathing);
    }

    private static void Set(Color color, bool breathe)
    {
        foreach (var part in Body)
        {
            // off and on again clears a breathing pattern before a steady colour
            if (!breathe)
                Nuwa.enableLed(part, false);
            Nuwa.enableLed(part, true);
            Nuwa.setLedColor(part, color);
            if (breathe)
                Nuwa.enableLedBreath(part, 6, 8);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && s_taken)
        {
            Nuwa.enableSystemLED();
        }
        else if (!paused && s_taken)
        {
            Nuwa.disableSystemLED();
            Apply();
        }
    }

    private void OnApplicationQuit() => Release();
}

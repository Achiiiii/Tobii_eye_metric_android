using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The staff's settings, kept on the robot (PlayerPrefs) and applied when the app starts: the language, the volume of
// the app's own sounds, and Kebbi's gestures, sounds, speech and lights each on or off. Everything is on by default.
public static class V3Settings
{
    // The languages offered, in V3.Lang's order (a new language adds a line here and its strings).
    public static readonly string[] Languages = { "中文", "English" };

    private const string LangKey = "v3.lang", VolumeKey = "v3.volume", MotionKey = "v3.motion", SoundKey = "v3.sound",
        SpeechKey = "v3.speech", LightKey = "v3.light";

    public static float Volume { get; private set; } = 1f;
    public static bool Motion { get; private set; } = true;
    public static bool Sound { get; private set; } = true;
    public static bool Speech { get; private set; } = true;
    public static bool Light { get; private set; } = true;

    public static void Load()
    {
        Volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        Motion = PlayerPrefs.GetInt(MotionKey, 1) != 0;
        Sound = PlayerPrefs.GetInt(SoundKey, 1) != 0;
        Speech = PlayerPrefs.GetInt(SpeechKey, 1) != 0;
        Light = PlayerPrefs.GetInt(LightKey, 1) != 0;
        V3.SetLang(Mathf.Clamp(PlayerPrefs.GetInt(LangKey, 0), 0, Languages.Length - 1));
        Apply();
    }

    public static void SetLanguage(int index)
    {
        PlayerPrefs.SetInt(LangKey, index);
        V3.SetLang(index);
    }

    public static void SetVolume(float volume)
    {
        Volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VolumeKey, Volume);
        Apply();
    }

    public static void SetMotion(bool on) => Set(MotionKey, on, v => Motion = v);
    public static void SetSound(bool on) => Set(SoundKey, on, v => Sound = v);
    public static void SetSpeech(bool on) => Set(SpeechKey, on, v => Speech = v);
    public static void SetLight(bool on) => Set(LightKey, on, v => Light = v);

    private static void Set(string key, bool on, Action<bool> store)
    {
        store(on);
        PlayerPrefs.SetInt(key, on ? 1 : 0);
        Apply();
    }

    private static void Apply()
    {
        KebbiMotion.Enabled = Motion;
        KebbiSound.Enabled = Sound;
        KebbiSound.Volume = Volume;
        KebbiLight.Enabled = Light;
        Nuwa.Muted = !Speech;
    }
}

// The settings card, made as the flow's other cards are (the exit dialog, "still there?"): a glass panel on the veil
// with its title centred at the top and its key centred at the bottom, coming up the same way (V3CardIn). In it: the
// languages as glass pieces, the volume as a guide to drag along, and four switches.
public class V3SettingsPage : MonoBehaviour
{
    private const float W = 600f, H = 470f, RowX = 230f, VolumeW = 250f, VolumeY = 176f;

    private static V3SettingsPage s_page;
    private GameObject _root;
    private V3GlassFace[] _langs;
    private V3.GuideParts _volume;
    private RectTransform _volumeKnob;
    private TextMeshProUGUI _volumeText;

    public static void Open(Transform parent)
    {
        if (s_page == null)
        {
            var canvas = UiFactory.CreateOverlayCanvas("V3Settings", V3Layer.Overlay, parent);
            s_page = canvas.gameObject.AddComponent<V3SettingsPage>();
            s_page.Build(canvas.transform);
        }
        s_page.Paint();
        s_page._root.SetActive(true);
    }

    public static void Close()
    {
        if (s_page != null)
            s_page._root.SetActive(false);
    }

    private void Build(Transform canvas)
    {
        var dim = UiFactory.CreateImage("Settings", canvas, UiFactory.White, V3.Veil, true);
        UiFactory.Stretch(dim.rectTransform);
        _root = dim.gameObject;
        var card = V3.Rect("Card", dim.transform, (V3.W - W) / 2f, (V3.H - H) / 2f, W, H);
        V3.GlassPanel(card, 0f, 0f, W, H);
        V3.Text("Title", card, V3.Zh, "設定", "Settings", V3Type.Title, V3.Ink, 0f, 32f, W, 40f, TextAlignmentOptions.Top);

        // the languages: one glass piece each, the one in use lit
        var language = V3.Rect("Language", card, 0f, 0f, W, H);
        Label(language, "語言", "Language", 48f, 100f);
        _langs = new V3GlassFace[V3Settings.Languages.Length];
        for (int i = 0; i < _langs.Length; i++)
        {
            int index = i;
            const float w = 132f, h = 52f;
            float x = RowX + i * (w + 12f);
            V3.PatchShadow(language, x, 92f, w, h, false);
            var button = UiFactory.CreateButton("Piece", language, UiFactory.White, Color.white, () =>
            {
                V3Sounds.Play(V3Sounds.Cue.Toggle);
                V3Settings.SetLanguage(index);
                Paint();
            });
            button.transition = Selectable.Transition.None;
            var rect = V3.At((RectTransform)button.transform, x, 92f, w, h);
            _langs[i] = V3GlassFace.Attach((Image)button.targetGraphic, w, h);
            V3.Text("Name", rect, V3.Zh, V3Settings.Languages[i], V3Type.Body, V3.Ink, 0f, 0f, w, h - 2f, TextAlignmentOptions.Center);
        }

        // the volume of the app's own sounds: a guide to drag along
        Label(card, "音效音量", "Sound volume", 48f, VolumeY - 12f);
        _volume = V3.Guide(card, RowX, VolumeY, VolumeW);
        _volumeKnob = V3.Sprite("Knob", card, UiFactory.Circle, Color.white, 0f, 0f, 26f, 26f).rectTransform;
        V3.Sprite("Dot", _volumeKnob, UiFactory.Circle, V3.Ink, 9f, 9f, 8f, 8f);
        _volumeText = V3.Text("Value", card, V3.Mono, "", V3Type.Label, V3.Ink, RowX + VolumeW + 20f, VolumeY - 6f, 60f, 24f, TextAlignmentOptions.Left);
        var drag = UiFactory.CreateImage("Drag", card, UiFactory.White, new Color(1f, 1f, 1f, 0f), true);
        V3.At(drag.rectTransform, RowX - 16f, VolumeY - 18f, VolumeW + 32f, 48f);
        drag.gameObject.AddComponent<V3Slide>().Init(16f, VolumeW, value =>
        {
            V3Settings.SetVolume(value);
            Paint();
        }, () => V3Sounds.Play(V3Sounds.Cue.Select));

        // Kebbi's effects, each on or off
        Switch(card, "凱比動作", "Gestures", 48f, 230f, () => V3Settings.Motion, V3Settings.SetMotion);
        Switch(card, "凱比燈光", "Lights", 316f, 230f, () => V3Settings.Light, V3Settings.SetLight);
        Switch(card, "音效", "Sounds", 48f, 284f, () => V3Settings.Sound, V3Settings.SetSound);
        Switch(card, "凱比語音", "Speech", 316f, 284f, () => V3Settings.Speech, V3Settings.SetSpeech);

        V3.Key(card, "完成", "Done", "check", V3.Green, (W - 210f) / 2f, H - 40f - 72f, 210f, false, Close);
        V3CardIn.Attach(_root, dim, card);
        _root.SetActive(false);
    }

    private static void Label(Transform parent, string zh, string en, float x, float y)
    {
        V3.Text("Label", parent, V3.Zh, zh, en, V3Type.Body, V3.Ink, x, y, 170f, 36f, TextAlignmentOptions.Left);
    }

    private static void Switch(Transform card, string zh, string en, float x, float y, Func<bool> get, Action<bool> set)
    {
        Label(card, zh, en, x, y);
        V3Switch.Create(card, x + 172f, y + 1f, get, on =>
        {
            V3Sounds.Play(V3Sounds.Cue.Toggle);
            set(on);
        });
    }

    private void Paint()
    {
        for (int i = 0; i < _langs.Length; i++)
            _langs[i].Lit = i == V3.Lang;
        V3.SetGuide(_volume, V3Settings.Volume);
        _volumeKnob.anchoredPosition = new Vector2(RowX + VolumeW * V3Settings.Volume - 13f, -(VolumeY + 6f - 13f));
        _volumeText.text = Mathf.RoundToInt(V3Settings.Volume * 100f).ToString();
    }
}

// An on / off switch: a 64 x 34 groove with a white knob that slides across; on, the groove is lit green.
public class V3Switch : MonoBehaviour
{
    private const float Wide = 64f, Tall = 34f;
    private Func<bool> _get;
    private Image _groove;
    private RectTransform _knob;
    private float _shown = -1f;

    public static V3Switch Create(Transform parent, float x, float y, Func<bool> get, Action<bool> set)
    {
        var button = UiFactory.CreateButton("Switch", parent, UiFactory.RoundedRect, Color.white, null);
        button.transition = Selectable.Transition.None;
        var rect = V3.At((RectTransform)button.transform, x, y, Wide, Tall);
        var groove = (Image)button.targetGraphic;
        groove.type = Image.Type.Sliced;
        groove.pixelsPerUnitMultiplier = 24f / (Tall / 2f);
        var s = button.gameObject.AddComponent<V3Switch>();
        s._get = get;
        s._groove = groove;
        s._knob = V3.Sprite("Knob", rect, UiFactory.Circle, Color.white, 4f, 4f, Tall - 8f, Tall - 8f).rectTransform;
        button.onClick.AddListener(() => set(!get()));
        return s;
    }

    private void OnEnable() => _shown = -1f;

    private void Update()
    {
        float goal = _get() ? 1f : 0f;
        _shown = _shown < 0f ? goal : Mathf.MoveTowards(_shown, goal, V3Motion.Step / V3Motion.Quick);
        float k = V3Motion.EaseOut(_shown);
        _groove.color = Color.Lerp(V3.A(V3.Groove, 0.28f), V3.Green, k);
        _knob.anchoredPosition = new Vector2(Mathf.Lerp(4f, Wide - Tall + 4f, k), -4f);
    }
}

// A track to drag along: reports where the touch is along it (0-1) while it is held, and once when it is let go.
public class V3Slide : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private float _inset, _length;
    private Action<float> _changed;
    private Action _released;

    public void Init(float inset, float length, Action<float> changed, Action released)
    {
        _inset = inset;
        _length = length;
        _changed = changed;
        _released = released;
    }

    public void OnPointerDown(PointerEventData e) => Report(e);
    public void OnDrag(PointerEventData e) => Report(e);
    public void OnPointerUp(PointerEventData e) => _released?.Invoke();

    private void Report(PointerEventData e)
    {
        var rect = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local);
        _changed?.Invoke(Mathf.Clamp01((local.x - rect.rect.xMin - _inset) / _length));
    }
}

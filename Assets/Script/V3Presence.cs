using System;
using TMPro;
using UnityEngine;

// Whether someone is in front of Kebbi, from the camera (HeadFollow.HasFace):
// - on the home, a person who sits down is greeted once; the greeting re-arms after the seat has been empty a while;
// - during a session, if nobody has been seen for a while, a card asks whether to go on and counts down; when it
//   runs out the app goes back to the home for the next person. A face coming back closes the card.
public class V3Presence : MonoBehaviour
{
    private const float GreetAfterSeconds = 1.5f;     // a face this long on the home = someone sat down
    private const float RearmAfterSeconds = 60f;      // empty this long = the next face is a new person
    private const float AskAfterSeconds = 30f;        // nobody this long mid-session = ask
    private const float HomeAfterSeconds = 30f;       // then this long more = back to the home

    private HeadFollow _head;
    private GameObject _home;
    private Action _goHome;

    private float _faceSince = -1f, _emptySince;
    private bool _greetArmed = true;
    private float _absentFor;

    private GameObject _card;
    private TextMeshProUGUI _countdown;
    private V3.GuideParts _wait;
    private float _askedAt;
    private float _idleGestureAt;
    private int _idleGesture;

    public static V3Presence Create(Transform parent, HeadFollow head, GameObject home, Action goHome)
    {
        var canvas = UiFactory.CreateOverlayCanvas("V3Presence", V3Layer.Presence, parent);
        var presence = canvas.gameObject.AddComponent<V3Presence>();
        presence._head = head;
        presence._home = home;
        presence._goHome = goHome;
        presence._emptySince = Time.unscaledTime;
        presence.BuildCard(canvas.transform);
        return presence;
    }

    private void BuildCard(Transform root)
    {
        var dim = UiFactory.CreateImage("StillThere", root, UiFactory.White, V3.Veil, true);
        UiFactory.Stretch(dim.rectTransform);
        const float w = 520f, h = 270f;
        var card = V3.Rect("Card", dim.transform, (V3.W - w) / 2f, 165f, w, h);
        V3.GlassPanel(card, 0f, 0f, w, h);
        V3.Text("Title", card, V3.Zh, "還在嗎？", "Are you still there?", V3Type.Title, V3.Ink, 0f, 34f, w, 40f, TextAlignmentOptions.Top);
        _countdown = V3.Text("Countdown", card, V3.Zh, "", V3Type.BodyS, V3.Text2, 0f, 84f, w, 30f, TextAlignmentOptions.Top);
        // the time left as a light guide running down
        _wait = V3.Guide(card, 60f, 126f, w - 120f);
        V3.Key(card, "回首頁", "Home", "home", V3.KeySecondary, 40f, 160f, 210f, false, () => { Hide(); _goHome?.Invoke(); });
        V3.Key(card, "繼續", "Continue", "arrowR", V3.Green, 270f, 160f, 210f, false, () => { KebbiMotion.Play("ok"); Hide(); });
        _card = dim.gameObject;
        V3CardIn.Attach(_card, dim, card);
        _card.SetActive(false);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        bool face = _head != null && _head.HasFace;
        bool onHome = _home.activeSelf;

        if (face)
        {
            if (_faceSince < 0f)
                _faceSince = now;
            if (now - _emptySince >= RearmAfterSeconds && !_greetArmed && _faceSince == now)
                _greetArmed = true;
        }
        else
        {
            if (_faceSince >= 0f)
                _emptySince = now;
            _faceSince = -1f;
        }

        V3Home.SomeoneFor = onHome && face ? now - _faceSince : -1f;
        if (onHome)
        {
            _absentFor = 0f;
            Hide();
            // someone there: now and then a small gesture, so Kebbi does not stand still as a statue
            if (face && now - _faceSince > 12f && now - _idleGestureAt > 20f)
            {
                _idleGestureAt = now;
                KebbiMotion.Play(_idleGesture++ % 2 == 0 ? "welcome" : "think");
            }
            if (face && _greetArmed && now - _faceSince >= GreetAfterSeconds)
            {
                _greetArmed = false;
                V3.Speak("你好！要來測一下視力嗎？請選擇雙眼或單眼檢測。");
                V3Sounds.Play(V3Sounds.Cue.Hello);
                KebbiMotion.Play("wave");
                KebbiLight.Flash(KebbiLight.Violet);
            }
            return;
        }

        // in a session the person is already greeted; re-arm only after they leave
        _greetArmed = false;
        _absentFor = face ? 0f : _absentFor + Time.unscaledDeltaTime;
        if (face)
        {
            Hide();
            return;
        }
        if (!_card.activeSelf && _absentFor >= AskAfterSeconds)
        {
            _card.SetActive(true);
            KebbiMotion.Play("wave");
            KebbiLight.Flash(KebbiLight.Violet);
            _askedAt = now;
            V3Sounds.Play(V3Sounds.Cue.Pause);
            V3.Speak("還在嗎？要繼續的話，請按繼續。");
        }
        if (_card.activeSelf)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt(HomeAfterSeconds - (now - _askedAt)));
            _countdown.text = V3.T($"沒有看到人，<size=24><color=#141414>{left}</color></size> 秒後回到首頁", $"Nobody in view - back to the home in <size=24><color=#141414>{left}</color></size> s");
            V3.SetGuide(_wait, 1f - (now - _askedAt) / HomeAfterSeconds);
            if (left <= 0)
            {
                Hide();
                _goHome?.Invoke();
            }
        }
    }

    // "Continue" (or a face) closes the card and starts the wait again.
    private void Hide()
    {
        if (_card != null && _card.activeSelf)
            _card.SetActive(false);
        _absentFor = 0f;
    }
}

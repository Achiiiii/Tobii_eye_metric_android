using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The V3 page layer behind every flow page (questionnaire to result): the light box, the app name and the progress
// ruler (a green light guide in a groove, y 66). Pages are built on Canvas; they show and hide themselves.
public class V3Flow : MonoBehaviour
{
    public Canvas Canvas { get; private set; }
    public RectTransform Pages { get; private set; }
    // Set each frame by the page on screen: hide the app name and the ruler (calibration dots,
    // countdown, test); Transparent also drops the page colour so the scene canvas below shows (the test).
    public bool Plain { get; set; }
    public bool Transparent { get; set; }
    // Set each frame by a page that has no progress to show (position, calibration, practice): no ruler.
    public bool NoRuler { get; set; }

    private GameObject _home;
    private GameObject _background;
    private GameObject _chrome;
    private V3.GuideParts _ruler;
    private float _progressShown, _progressTarget;

    public static V3Flow Create(Transform parent, GameObject home)
    {
        var go = new GameObject("V3Flow");
        go.transform.SetParent(parent, false);
        var flow = go.AddComponent<V3Flow>();
        flow._home = home;
        // Above the scene's main canvas (0), below the flow overlay (5), the home (6) and the gaze dot (10).
        flow.Canvas = UiFactory.CreateOverlayCanvas("V3Pages", V3Layer.Pages, go.transform);
        flow.Build();
        return flow;
    }

    public bool FlowVisible => _home == null || !_home.activeSelf;

    // the light box's sheen (V3Sheen) is off while gaze is measured: the calibration dots and countdown, the test
    public static bool SheenOffSetup, SheenOffTest;
    public static bool SheenAllowed => !SheenOffSetup && !SheenOffTest;

    // Keeps a scene object (and its logic) but stops drawing it and taking touches; the V3 page draws instead.
    public static void Conceal(GameObject target)
    {
        if (target == null)
            return;
        var group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    public void SetProgress(float fraction)
    {
        _progressTarget = Mathf.Clamp01(fraction);
    }

    private void Build()
    {
        var root = Canvas.transform;
        _background = V3.LightBox(root, true).gameObject;
        // a soft light on the light box that follows the eyes (or the touch)
        var sheen = V3.Sprite("Sheen", _background.transform, SheenSprite(), new Color(1f, 1f, 1f, 0f), 0f, 0f, 560f, 560f);
        sheen.raycastTarget = false;
        sheen.rectTransform.pivot = UiFactory.Center;
        var follow = _background.AddComponent<V3Sheen>();
        follow.Pointer = FindObjectOfType<FollowGazePoint2D>(true);
        follow.Light = sheen.rectTransform;
        follow.LightImage = sheen;

        _chrome = V3.Full("Chrome", root).gameObject;
        var name = V3.Text("AppName", _chrome.transform, V3.Zh, "眼動視力檢測", "Eye-Tracking Vision Test", V3Type.Label, V3.Ink, 40f, 26f, 400f, 36f, TextAlignmentOptions.Left, 0.1f);
        var eye = V3.Picture("EyeIcon", _chrome.transform, "V3/eye", 0f, 34f, 20f, 20f);
        name.gameObject.AddComponent<V3Follow>().Init(name, eye.rectTransform, 8f);
        BuildRuler(_chrome.transform);

        Pages = V3.Full("Pages", root);
    }

    private static Sprite s_sheen;

    // a round soft light, brightest in the middle
    private static Sprite SheenSprite()
    {
        return s_sheen != null ? s_sheen : (s_sheen = V3.Raster(128, p =>
        {
            float r = Mathf.Clamp01(Vector2.Distance(p, new Vector2(64f, 64f)) / 64f);
            return new Color(1f, 1f, 1f, Mathf.Pow(1f - r, 2.2f));
        }));
    }

    // The progress ruler under the chrome row (y 66): the glass tube over the home dial's scale, in seven sections (the
    // questions), each numbered at its end.
    private void BuildRuler(Transform root)
    {
        float w = V3.W - 80f;
        _ruler = V3.Guide(root, 40f, 66f, w, true, 7);
        for (int i = 1; i <= 7; i++)
            V3.Text("No", _ruler.Root, V3.Mono, i.ToString("00"), V3Type.Micro, V3.A(V3.Ink, 0.66f), w * i / 7f - 34f, 19f, 30f, 11f, TextAlignmentOptions.Right);
    }

    // After the pages' Update: apply this frame's requests, then clear them for the next frame.
    private void LateUpdate()
    {
        bool flow = FlowVisible;
        bool background = flow && !Transparent;
        if (_background.activeSelf != background)
            _background.SetActive(background);
        bool chrome = flow && !Plain;
        if (_chrome.activeSelf != chrome)
            _chrome.SetActive(chrome);
        _ruler.Root.gameObject.SetActive(!NoRuler);
        Plain = false;
        Transparent = false;
        NoRuler = false;

        _progressShown = Mathf.Lerp(_progressShown, _progressTarget, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        V3.SetGuide(_ruler, _progressShown);
    }

}

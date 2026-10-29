using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The V3 page layer behind every flow page (questionnaire to result): the bright page with its dot grid, the
// crop marks, the app name and the progress ruler. Pages are built on Canvas; they show and hide themselves.
public class V3Flow : MonoBehaviour
{
    public Canvas Canvas { get; private set; }
    public RectTransform Pages { get; private set; }
    // Set each frame by the page on screen: hide the app name, the ruler and the grid (calibration dots,
    // countdown, test); Transparent also drops the page colour so the scene canvas below shows (the test).
    public bool Plain { get; set; }
    public bool Transparent { get; set; }

    private GameObject _home;
    private GameObject _background;
    private Image _page;
    private RawImage _grid, _grain;
    private GameObject _chrome;
    private RectTransform _progress;
    private RectTransform _progressDot;
    private float _progressShown, _progressTarget;

    public static V3Flow Create(Transform parent, GameObject home)
    {
        var go = new GameObject("V3Flow");
        go.transform.SetParent(parent, false);
        var flow = go.AddComponent<V3Flow>();
        flow._home = home;
        // Above the scene's main canvas (0), below the flow overlay (5), the home (6) and the gaze dot (10).
        flow.Canvas = UiFactory.CreateOverlayCanvas("V3Pages", 3, go.transform);
        flow.Build();
        return flow;
    }

    public bool FlowVisible => _home == null || !_home.activeSelf;

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
        var bg = UiFactory.CreateImage("Page", root, UiFactory.White, V3.Cool, true);
        UiFactory.Stretch(bg.rectTransform);
        _background = bg.gameObject;
        _page = bg;

        _grain = new GameObject("Grain", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        _grain.transform.SetParent(_background.transform, false);
        UiFactory.Stretch(_grain.rectTransform);
        _grain.texture = V3.Grain;
        _grain.uvRect = new Rect(0f, 0f, V3.W / 128f, V3.H / 128f);
        _grain.raycastTarget = false;

        _grid = new GameObject("Grid", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        _grid.transform.SetParent(_background.transform, false);
        UiFactory.Stretch(_grid.rectTransform);
        _grid.texture = DotTile();
        _grid.uvRect = new Rect(0f, 0f, V3.W / 24f, V3.H / 24f);
        _grid.raycastTarget = false;

        // crop marks 14 px in from each corner
        V3.Brackets(_background.transform, 14f, 14f, V3.W - 28f, V3.H - 28f, 14f, 1f, 0f, true, V3.A(V3.Ink, 0.5f));

        _chrome = V3.Full("Chrome", root).gameObject;
        var name = V3.Text("AppName", _chrome.transform, V3.Zh, "眼動視力檢測", "Eye-Tracking Vision Test", 22f, V3.Ink, 40f, 34f, 600f, 44f, TextAlignmentOptions.Left, 0.1f);
        var eye = V3.Picture("EyeIcon", _chrome.transform, "V3/eye", 0f, 44f, 24f, 24f);
        name.gameObject.AddComponent<V3Follow>().Init(name, eye.rectTransform, 10f);
        BuildRuler(_chrome.transform);

        Pages = V3.Full("Pages", root);
    }

    // The fine metric ruler under the chrome row (y 100): hairline, minor ticks every 12 px, numbers every 120 px,
    // a 2 px ink progress line with a mint dot at its end.
    private void BuildRuler(Transform root)
    {
        const float width = V3.W - 80f;
        var ruler = V3.Rect("Ruler", root, 40f, 100f, width, 26f);
        var lines = V3.Rect("Lines", ruler, 0f, 0f, width, 10f);
        lines.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
        V3.Box("Line", lines, V3.Ink, 0f, 0f, width, 1f);
        for (float x = 0f; x < width; x += 12f)
            V3.Box("Minor", lines, V3.A(V3.Ink, 0.4f), x, 1f, 1f, 4f);
        for (float x = 0f; x < width; x += 120f)
            V3.Box("Major", lines, V3.Ink, x, 1f, 1f, 9f);
        V3.Box("CapL", ruler, V3.Ink, 0f, -5f, 2f, 12f);
        V3.Box("CapR", ruler, V3.Ink, width - 2f, -5f, 2f, 12f);
        for (int i = 0; i <= 7; i++)
            V3.Text("Num", ruler, V3.Mono, (i * 10).ToString(), 10f, V3.A(V3.Ink, 0.6f), i * 120f - 20f, 12f, 40f, 12f, TextAlignmentOptions.Top, 0.04f);
        _progress = V3.Box("Progress", ruler, V3.Ink, 0f, -1f, 0f, 2f).rectTransform;
        // progress marker: a reticle
        _progressDot = V3.Rect("ProgressMark", ruler, -11f, -11f, 22f, 22f);
        V3.Sprite("Reticle", _progressDot, V3.ReticleSprite, Color.white, 0f, 0f, 22f, 22f);
    }

    // After the pages' Update: apply this frame's requests, then clear them for the next frame.
    private void LateUpdate()
    {
        bool flow = FlowVisible;
        bool background = flow && !Transparent;
        if (_background.activeSelf != background)
            _background.SetActive(background);
        _grid.enabled = !Plain;
        // the test and its lead-in on the home's plain page colour; the other flow pages on the cool grey + grain
        _page.color = Plain ? V3.Page : V3.Cool;
        _grain.enabled = !Plain;
        bool chrome = flow && !Plain;
        if (_chrome.activeSelf != chrome)
            _chrome.SetActive(chrome);
        Plain = false;
        Transparent = false;

        _progressShown = Mathf.Lerp(_progressShown, _progressTarget, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        float w = (V3.W - 80f) * _progressShown;
        _progress.sizeDelta = new Vector2(w, 2f);
        _progressDot.anchoredPosition = new Vector2(w - 11f, 11f - 0.5f);
    }

    // 24 px tile with one soft 1 px dot (ink 13 %).
    private static Texture2D DotTile()
    {
        const int s = 24;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt(Sq(Mathf.Min(x + 0.5f, s - x - 0.5f)) + Sq(Mathf.Min(y + 0.5f, s - y - 0.5f)));
                float a = Mathf.Clamp01(1.3f - d) * 0.13f;
                px[y * s + x] = new Color32(0x14, 0x14, 0x14, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static float Sq(float v) => v * v;
}

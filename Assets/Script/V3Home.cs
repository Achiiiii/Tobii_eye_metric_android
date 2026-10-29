using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// V3 home ("optical instrument"). Left: the EYE METRIC headline, seen through a
// lens (letters away from the focus point go soft; the focus racks along the word, or follows the gaze on it),
// and the two mode keys. Right: a dial centred at (672, 334) - lens disc, degree ring with the live gaze angle
// and its afterimages, the level arc 0.1-2.0, two silver bezels - in three layers that shift by different
// amounts as the E leans toward the gaze. The E is 3D (a matte extruded plate) and, like the test, steps down
// a level and turns to a new direction every 2.4 s. Layout in the 1024 x 600 reference space, origin top-left.
public class V3Home : MonoBehaviour
{
    private const float CX = 672f, CY = 334f, R1 = 190f, R2 = 158f;
    private const float Fov = 30f, CamDist = 12f;
    private const int WorldLayer = 31;
    private const float IdleSeconds = 2.5f;
    private const float StepSeconds = 2.4f;
    private static readonly float[] VA = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.7f, 0.8f, 1.0f, 1.3f, 1.5f, 2.0f };
    private static readonly int[] TestSize = { 160, 96, 64, 48, 32, 24, 20, 16, 13, 10, 8 };   // MetricTest, 1920-wide units
    // world units per pixel at z = 0: frame height = 2 * 12 * tan(15 deg) over 600 px
    private static readonly float UPP = 2f * CamDist * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) / 600f;

    public event Action<bool> ShownChanged;

    private FollowGazePoint2D _pointer;
    private Action<MetricTest.TestMode> _onSelect;
    private Canvas _canvas;
    private RectTransform _root;

    // 3D
    private GameObject _world;
    private Camera _cam;
    private Transform _holder, _eTransform;
    private RenderTexture _rt;
    private RawImage _view;

    // dial layers (back, mid, front) and their parallax amounts (px per radian of lean)
    private readonly RectTransform[] _layers = new RectTransform[3];
    private static readonly float[] LayerShift = { 10f, 18f, 26f };
    private Image[] _ghosts;
    private Image _liveArc;
    private RectTransform _livePointer;
    private readonly List<(float angle, float time)> _angleHistory = new List<(float, float)>();
    private Image[] _levelTicks = new Image[11];
    private TextMeshProUGUI[] _levelLabels = new TextMeshProUGUI[11];
    private TextMeshProUGUI _lvText, _vaText, _oriText;

    // headline
    private RectTransform _headline;
    private readonly List<(Graphic g, float cx, bool optotype, TextMeshProUGUI tmp)> _letters = new List<(Graphic, float, bool, TextMeshProUGUI)>();
    private Sprite[] _eBlur;
    private float _focusX, _focusDir = 1f;
    // the headline is laid out at 54 px and drawn HeadScale larger
    private const float HeadX = 48f, HeadY = 126f, HeadW = 431f, HeadScale = 1.08f;   // 1.08 keeps "METRIC" clear of the dial's labels (x ~520 at this height)

    // chrome
    private TextMeshProUGUI _appName, _clock, _readout1, _readout2;
    private RectTransform _appIcon, _knob;
    private TextMeshProUGUI _langZh, _langEn;
    private TextMeshProUGUI[] _modeLabels = new TextMeshProUGUI[2];
    private bool _selecting;

    // cursor + gaze path
    private RectTransform _cursor, _cursorTicks;
    private Image _cursorArc;
    private TextMeshProUGUI _cursorMs;
    private float _dwellFrom = -1f;
    private Image[] _path;
    private readonly List<(Vector2 p, float t)> _pathPoints = new List<(Vector2, float)>();
    private float _pathAt;

    // gaze (reference px, y down)
    private float _tx = CX, _ty = CY, _gx = CX, _gy = CY;
    private float _lastSample = -100f;
    private bool _hasSample;
    private bool Idle => Time.unscaledTime - _lastSample > IdleSeconds;

    // E sequence
    private int _level = 1, _dir;
    private float _rotFrom, _rotT = 1f, _sizeNow, _sizeTo, _nextAt;
    private float _leanX, _leanY;
    private static readonly float[] Rot = { 0f, -Mathf.PI / 2f, Mathf.PI, Mathf.PI / 2f };   // right, down, left, up
    private static readonly int[] OriDeg = { 0, 270, 180, 90 };

    public static V3Home Create(Transform parent, FollowGazePoint2D pointer, Action<MetricTest.TestMode> onSelect)
    {
        var go = new GameObject("V3Home");
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        var home = go.AddComponent<V3Home>();
        home._pointer = pointer;
        home._onSelect = onSelect;
        home.BuildWorld();
        home.BuildUi();
        return home;
    }

    // ==================== show / hide ====================

    private void OnEnable()
    {
        if (_cam == null)
            return;
        // keep the app's own cameras and lights out of the E world, and the key light out of theirs
        foreach (var cam in Camera.allCameras)
            if (cam != _cam)
                cam.cullingMask &= ~(1 << WorldLayer);
        foreach (var light in FindObjectsOfType<Light>())
            if (light.transform.parent != _world.transform)
                light.cullingMask &= ~(1 << WorldLayer);
        _rt = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "V3HomeE" };
        _cam.targetTexture = _rt;
        _view.texture = _rt;
        ApplyProjection();
        _world.SetActive(true);
        if (_pointer != null)
            _pointer.RawSampleAdded += OnGazeSample;
        _hasSample = false;
        _lastSample = -100f;
        _selecting = false;
        _angleHistory.Clear();
        _pathPoints.Clear();
        _nextAt = Time.unscaledTime + StepSeconds;
        SetLevel(1);
        _sizeNow = _sizeTo;
        ApplyLanguage();
        ShownChanged?.Invoke(true);
    }

    private void OnDisable()
    {
        if (_cam == null)
            return;
        if (_pointer != null)
            _pointer.RawSampleAdded -= OnGazeSample;
        _world.SetActive(false);
        _cam.targetTexture = null;
        _view.texture = null;
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
        ShownChanged?.Invoke(false);
    }

    // Tobii samples arrive even while the app's gaze dot is hidden; raw screen pixels (y up).
    private void OnGazeSample(Vector2 raw)
    {
        float k = V3.W / Screen.width;
        float x = raw.x * k, y = (Screen.height - raw.y) * k;
        // Before calibration the gaze can land far off the screen: treat that as not looking at it.
        if (x < -0.1f * V3.W || x > 1.1f * V3.W || y < -0.1f * V3.H || y > 1.1f * V3.H)
            return;
        Look(Mathf.Clamp(x, 0f, V3.W), Mathf.Clamp(y, 0f, V3.H));
    }

    private void Look(float x, float y)
    {
        if (!_hasSample || Idle)
        {
            _tx = x;
            _ty = y;
            _hasSample = true;
        }
        else
        {
            // light smoothing of the 12-15 Hz samples; the per-frame easing does the rest
            _tx += (x - _tx) * 0.5f;
            _ty += (y - _ty) * 0.5f;
        }
        _lastSample = Time.unscaledTime;
    }

    // ==================== 3D: the E ====================

    private void BuildWorld()
    {
        _world = new GameObject("V3HomeWorld");
        _world.transform.SetParent(transform, false);
        _world.layer = WorldLayer;

        _cam = AddChild<Camera>("Camera", _world.transform);
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _cam.cullingMask = 1 << WorldLayer;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 100f;
        _cam.allowHDR = false;
        _cam.allowMSAA = true;
        _cam.fieldOfView = Fov;
        // Unity's usual setup: the camera on -z looking along +z, so screen right = +x and up = +y.
        _cam.transform.position = new Vector3(0f, 0f, -CamDist);
        _cam.transform.rotation = Quaternion.identity;

        // key light from the upper left, in front
        var key = AddChild<Light>("Key", _world.transform);
        key.type = LightType.Directional;
        key.color = new Color32(0xFF, 0xF6, 0xEA, 0xFF);
        key.intensity = 1.2f;
        key.transform.rotation = Quaternion.LookRotation(-new Vector3(-5f, 6f, -8f));
        key.shadows = LightShadows.None;
        key.cullingMask = 1 << WorldLayer;

        _holder = new GameObject("Holder").transform;
        _holder.SetParent(_world.transform, false);
        _holder.gameObject.layer = WorldLayer;
        _eTransform = new GameObject("Turn").transform;
        _eTransform.SetParent(_holder, false);
        _eTransform.gameObject.layer = WorldLayer;
        var mat = new Material(LoadShader("Ink"));
        // a matte, thin plate: dark grey face, a little sheen on the bevels
        mat.SetColor("_Color", new Color32(0x10, 0x10, 0x12, 0xFF));
        mat.SetFloat("_Spec", 0.08f);
        mat.SetFloat("_Gloss", 10f);
        mat.SetFloat("_Sky", 0.08f);
        mat.SetFloat("_Rim", 0.04f);
        var e = AddMesh("E", _eTransform, BuildEMesh(), mat);
        e.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        e.receiveShadows = false;
        // the mesh lies flat (thickness along y): stand it up facing the camera
        e.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        _world.SetActive(false);
    }

    // three.js PerspectiveCamera(30) at distance 12 with setViewOffset so that the origin lands on the dial centre.
    private void ApplyProjection()
    {
        float near = _cam.nearClipPlane;
        float px = 2f * near * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) / V3.H;   // one reference pixel at the near plane
        float halfW = V3.W * 0.5f * px, halfH = V3.H * 0.5f * px;
        float left = -halfW - (CX - V3.W / 2f) * px, top = halfH + (CY - V3.H / 2f) * px;
        _cam.projectionMatrix = Matrix4x4.Frustum(left, left + 2f * halfW, top - 2f * halfH, top, near, _cam.farClipPlane);
    }

    private static Shader LoadShader(string name)
    {
        var shader = Resources.Load<Shader>("V3/Shaders/" + name);
        if (shader == null)
            Debug.LogError("[V3HOME] missing shader V3/Shaders/" + name);
        return shader;
    }

    private static T AddChild<T>(string name, Transform parent) where T : Component
    {
        var go = new GameObject(name);
        go.layer = WorldLayer;
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    private static MeshRenderer AddMesh(string name, Transform parent, Mesh mesh, Material material)
    {
        var filter = AddChild<MeshFilter>(name, parent);
        filter.sharedMesh = mesh;
        var renderer = filter.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return renderer;
    }

    // The E optotype: the 5 x 5 grid outline extruded with a small bevel, 1 unit wide, lying flat (thickness
    // along y, centred), opening toward +x. A thin plate (depth 0.32 of 5, bevel 0.04).
    private static Mesh BuildEMesh()
    {
        int[,] grid = { { 0, 0 }, { 5, 0 }, { 5, 1 }, { 1, 1 }, { 1, 2 }, { 5, 2 }, { 5, 3 }, { 1, 3 }, { 1, 4 }, { 5, 4 }, { 5, 5 }, { 0, 5 } };
        int n = grid.GetLength(0);
        const float unit = 0.2f, depth = 0.32f * unit, bevelT = 0.04f * unit, bevelS = 0.035f * unit;
        var cap = new Vector2[n];
        for (int i = 0; i < n; i++)
            cap[i] = new Vector2((grid[i, 0] - 2.5f) * unit, (grid[i, 1] - 2.5f) * unit);
        // Outline is counter-clockwise, so the outward normal of edge a->b is (dy, -dx).
        var edgeNormal = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 d = (cap[(i + 1) % n] - cap[i]).normalized;
            edgeNormal[i] = new Vector2(d.y, -d.x);
        }
        var wall = new Vector2[n];
        for (int i = 0; i < n; i++)
            wall[i] = cap[i] + bevelS * (edgeNormal[(i + n - 1) % n] + edgeNormal[i]);

        float yCap = depth * 0.5f + bevelT, yWall = depth * 0.5f;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
                (b, c) = (c, b);
            int i0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            norms.Add(normal); norms.Add(normal); norms.Add(normal);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            Tri(a, b, c, normal);
            Tri(a, c, d, normal);
        }
        Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        int[] capTris = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 7, 0, 7, 8, 0, 8, 11, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11 };
        for (int t = 0; t < capTris.Length; t += 3)
        {
            Tri(At(cap[capTris[t]], yCap), At(cap[capTris[t + 1]], yCap), At(cap[capTris[t + 2]], yCap), Vector3.up);
            Tri(At(cap[capTris[t]], -yCap), At(cap[capTris[t + 1]], -yCap), At(cap[capTris[t + 2]], -yCap), Vector3.down);
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var outward = new Vector3(edgeNormal[i].x, 0f, edgeNormal[i].y);
            Quad(At(wall[i], yWall), At(wall[j], yWall), At(wall[j], -yWall), At(wall[i], -yWall), outward);
            Quad(At(cap[i], yCap), At(cap[j], yCap), At(wall[j], yWall), At(wall[i], yWall), (outward * bevelT + Vector3.up * bevelS).normalized);
            Quad(At(cap[i], -yCap), At(cap[j], -yCap), At(wall[j], -yWall), At(wall[i], -yWall), (outward * bevelT + Vector3.down * bevelS).normalized);
        }
        var mesh = new Mesh { name = "E" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ==================== UI ====================

    private void BuildUi()
    {
        _canvas = UiFactory.CreateOverlayCanvas("V3HomeCanvas", 6, transform);
        _root = (RectTransform)_canvas.transform;
        var bg = UiFactory.CreateImage("Page", _root, UiFactory.White, V3.Page, true);
        UiFactory.Stretch(bg.rectTransform);
        V3.Brackets(_root, 14f, 14f, V3.W - 28f, V3.H - 28f, 14f, 1f, 0f, true, V3.A(V3.Ink, 0.5f));

        BuildDial();
        _view = new GameObject("E", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        _view.transform.SetParent(_root, false);
        UiFactory.Stretch(_view.rectTransform);
        _view.raycastTarget = false;
        BuildReadouts();
        BuildPath();
        BuildHeadline();
        BuildModes();
        BuildChrome();
        BuildCursor();
    }

    // A layer of the dial: its own canvas (it moves every frame), origin at the dial centre, y up.
    private RectTransform Layer(string name)
    {
        var rect = UiFactory.CreateRect(name, _root);
        UiFactory.Place(rect, new Vector2(0f, 1f), UiFactory.Center, new Vector2(CX, -CY), Vector2.zero);
        rect.gameObject.AddComponent<Canvas>();
        return rect;
    }

    private static Image At(Image image, float x, float y, float w, float h, float rotation = 0f)
    {
        UiFactory.Place(image.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(x, y), new Vector2(w, h));
        image.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
        return image;
    }

    // canvas-style angle (degrees, clockwise from +x on a y-down screen) to a dial-local point (y up)
    private static Vector2 Polar(float r, float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), -Mathf.Sin(deg * Mathf.Deg2Rad)) * r;

    private void BuildDial()
    {
        // back: the lens disc (white, fading out at r 250) and a fine crosshair
        var back = _layers[0] = Layer("DialBack");
        At(UiFactory.CreateImage("Lens", back, LensSprite(), Color.white), 0f, 0f, 500f, 500f);
        var hair = V3.A(V3.Ink, 0.22f);
        float far = R1 + 46f;
        At(UiFactory.CreateImage("CrossL", back, UiFactory.White, hair), -(far + 120f) / 2f, 0f, far - 120f, 1f);
        At(UiFactory.CreateImage("CrossR", back, UiFactory.White, hair), (far + 120f) / 2f, 0f, far - 120f, 1f);
        At(UiFactory.CreateImage("CrossT", back, UiFactory.White, hair), 0f, (far + 120f) / 2f, 1f, far - 120f);
        At(UiFactory.CreateImage("CrossB", back, UiFactory.White, hair), 0f, -(R2 - 30f + 120f) / 2f, 1f, R2 - 30f - 120f);

        // mid: degree ring (every 2 deg, labels every 30), the silver outer bezel, the live gaze arc
        var mid = _layers[1] = Layer("DialMid");
        for (int d = 0; d < 360; d += 2)
        {
            float len = d % 30 == 0 ? 12f : d % 10 == 0 ? 7f : 3f;
            float a = d - 90f;
            var p = Polar(R1 + 4f + len / 2f, a);
            At(UiFactory.CreateImage("Tick", mid, UiFactory.White, d % 30 == 0 ? V3.Ink : V3.A(V3.Ink, 0.4f)), p.x, p.y, len, 1f, -a);
            if (d % 30 == 0)
            {
                var lp = Polar(R1 + 28f, a);
                var label = V3.Text("Deg", mid, V3.Mono, d.ToString("000"), 10f, V3.A(V3.Ink, 0.6f), 0f, 0f, 40f, 14f, TextAlignmentOptions.Center);
                UiFactory.Place(label.rectTransform, UiFactory.Center, UiFactory.Center, lp, new Vector2(40f, 14f));
            }
        }
        At(UiFactory.CreateImage("Bezel", mid, SilverRing(R1, 1.5f), Color.white), 0f, 0f, 2f * R1 + 16f, 2f * R1 + 16f);
        var arc = ArcSprite(R1 - 1f, 1.5f, 0f, 360f);
        _ghosts = new Image[12];
        for (int i = 0; i < _ghosts.Length; i++)
        {
            _ghosts[i] = Arc(mid, arc, "Ghost");
            _ghosts[i].enabled = false;
        }
        _liveArc = Arc(mid, arc, "Live");
        _liveArc.color = V3.A(V3.Violet, 0.95f);
        _livePointer = At(UiFactory.CreateImage("Pointer", mid, TriangleSprite(), V3.Violet), 0f, 0f, 10f, 9f).rectTransform;

        // front: the level arc along the bottom of the inner ring, its 11 ticks and values, the inner bezel
        var front = _layers[2] = Layer("DialFront");
        At(UiFactory.CreateImage("LevelArc", front, ArcSprite(R2, 0.5f, 20f, 160f), V3.A(V3.Ink, 0.25f)), 0f, 0f, Mathf.Ceil(2f * R2 + 16f), Mathf.Ceil(2f * R2 + 16f));
        for (int i = 0; i < 11; i++)
        {
            float a = 160f - i * 14f;
            var p = Polar(R2, a);
            _levelTicks[i] = At(UiFactory.CreateImage("LevelTick", front, UiFactory.White, V3.Ink), p.x, p.y, 12f, 2f, -a);
            var lp = Polar(R2 - 22f, a);
            _levelLabels[i] = V3.Text("Level", front, V3.Mono, VA[i].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), 10f, V3.A(V3.Ink, 0.6f), 0f, 0f, 40f, 16f, TextAlignmentOptions.Center);
            UiFactory.Place(_levelLabels[i].rectTransform, UiFactory.Center, UiFactory.Center, lp, new Vector2(40f, 16f));
        }
        float inner = 2f * (R2 - 46f) + 16f;
        At(UiFactory.CreateImage("InnerBezel", front, SilverRing(R2 - 46f, 1f), Color.white), 0f, 0f, Mathf.Ceil(inner), Mathf.Ceil(inner));
    }

    private static Image Arc(Transform parent, Sprite ring, string name)
    {
        var image = UiFactory.CreateImage(name, parent, ring, V3.Violet);
        At(image, 0f, 0f, ring.rect.width, ring.rect.height);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Right;
        image.fillClockwise = false;
        return image;
    }

    // Puts a filled-ring arc of the given half-span (radians) centred on a canvas-style angle (radians).
    private static void PlaceArc(Image arc, float angle, float half)
    {
        arc.fillAmount = 2f * half / (2f * Mathf.PI);
        arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, (-angle - half) * Mathf.Rad2Deg);
    }

    private void BuildReadouts()
    {
        var t = _root;
        float right = CX + R1 + 46f, left = CX - R1 - 46f;
        _oriText = V3.Text("Ori", t, V3.Mono, "", 13f, V3.Ink, right, CY - 21f, 160f, 17f, TextAlignmentOptions.Left, 0.06f);
        V3.Text("OriSub", t, V3.Mono, "ORIENTATION", 10f, V3.Ink, right, CY + 1f, 160f, 13f, TextAlignmentOptions.Left, 0.06f);
        _lvText = V3.Text("Lv", t, V3.Mono, "", 13f, V3.Ink, left - 160f, CY - 21f, 160f, 17f, TextAlignmentOptions.Right, 0.06f);
        _vaText = V3.Text("Va", t, V3.Mono, "", 10f, V3.Ink, left - 160f, CY + 1f, 160f, 13f, TextAlignmentOptions.Right, 0.06f);
    }

    // The gaze path: the last ~2.6 s of looks as small fading squares.
    private void BuildPath()
    {
        var layer = V3.Full("Path", _root);
        layer.gameObject.AddComponent<Canvas>();
        _path = new Image[36];
        for (int i = 0; i < _path.Length; i++)
        {
            _path[i] = V3.Box("Dot", layer, V3.Ink, 0f, 0f, 3f, 3f);
            _path[i].enabled = false;
        }
    }

    // ==================== headline ====================

    private void BuildHeadline()
    {
        _headline = V3.Rect("Headline", _root, HeadX, HeadY, HeadW, 80f);
        _headline.gameObject.AddComponent<Canvas>();
        _headline.localScale = Vector3.one * HeadScale;
        _eBlur = new Sprite[7];
        for (int i = 0; i < _eBlur.Length; i++)
            _eBlur[i] = BlurredE(i * 0.6f);
        // letter boxes (x from the headline's left; optotype E's are 41 px squares at y 7)
        var parts = new (string ch, float x, float w)[]
        {
            ("E", 3f, 41f), ("Y", 46f, 50f), ("E", 99f, 41f), ("M", 156f, 67f), ("E", 226f, 41f), ("T", 269f, 45f), ("R", 314f, 46f), ("I", 360f, 19f), ("C", 380f, 51f),
        };
        var ink = V3.A(V3.Ink, 0.95f);
        foreach (var (ch, x, w) in parts)
        {
            if (ch == "E")
            {
                var e = V3.Sprite("E", _headline, _eBlur[0], ink, x - 41f * 0.12f, 7f - 41f * 0.12f, w * 1.24f, w * 1.24f);
                _letters.Add((e, x + w / 2f, true, null));
            }
            else
            {
                // a 100 px box whose middle is the baseline (TMP Baseline alignment): the baseline at y 48
                var letter = V3.Text("L", _headline, V3.Head, ch, 54f, ink, x - 10f, -2f, w + 20f, 100f, TextAlignmentOptions.Baseline);
                letter.fontMaterial = new Material(letter.fontSharedMaterial);
                _letters.Add((letter, x + w / 2f, false, letter));
            }
        }
        float subY = HeadY + 63f * HeadScale;
        var sub = V3.Text("Sub", _root, V3.Head, "VISION CHECK", 12f * HeadScale, ink, HeadX, subY, 300f, 18f, TextAlignmentOptions.Left, 0.2f);
        var mark = V3.Picture("Mark", _root, "V3/mosaic", HeadX + 160f, subY, 14f * HeadScale, 14f * HeadScale);
        // after the subtitle's text once the font has laid it out
        sub.gameObject.AddComponent<V3Follow>().Init(sub, mark.rectTransform, 4f);
        V3.Picture("Reg1", _root, "V3/cross", 19f, HeadY - 4f, 21f, 21f);
        V3.Picture("Reg2", _root, "V3/cross", HeadX + 437f * HeadScale, HeadY + 35f * HeadScale, 21f, 21f);
    }

    // Each letter seen through the lens: soft away from the focus point. The focus racks slowly along the word,
    // or follows the gaze while it is on the headline.
    private void UpdateHeadline(float dt, bool idle)
    {
        float? focusAt = null;
        if (!idle && _gy > HeadY - 30f && _gy < HeadY + 78f * HeadScale + 30f && _gx > HeadX - 40f && _gx < HeadX + HeadW * HeadScale + 40f)
            focusAt = (_gx - HeadX) / HeadScale;
        float target;
        if (focusAt.HasValue)
            target = focusAt.Value;
        else
        {
            target = _focusX + _focusDir * dt * HeadW / 3.2f;
            if (target > HeadW * 1.05f) _focusDir = -1f;
            if (target < -HeadW * 0.05f) _focusDir = 1f;
        }
        _focusX += (target - _focusX) * (focusAt.HasValue ? 0.18f : 1f);
        foreach (var (g, cx, optotype, tmp) in _letters)
        {
            float dist = Mathf.Abs(cx - _focusX) / HeadW;
            float blur = Mathf.Min(3.2f, Mathf.Max(0f, dist - 0.07f) * 7.5f) * (optotype ? 0.55f : 1f);
            var c = g.color;
            c.a = 0.95f * (1f - Mathf.Min(0.25f, blur * 0.07f));
            g.color = c;
            if (optotype)
                ((Image)g).sprite = _eBlur[Mathf.Clamp(Mathf.RoundToInt(blur / 0.6f), 0, _eBlur.Length - 1)];
            else
                tmp.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, blur * 0.11f);
        }
    }

    // ==================== mode keys ====================

    private void BuildModes()
    {
        BuildMode(0, 384f, V3.Mint, "V3/eyes2", MetricTest.TestMode.Both);
        BuildMode(1, 480f, V3.Violet, "V3/eye1", MetricTest.TestMode.Single);
    }

    // .mode: 300 x 80, white 90 %, 1 px ink frame, brackets on two corners, round chip, label, long thin arrow.
    private void BuildMode(int index, float y, Color chip, string icon, MetricTest.TestMode mode)
    {
        const float w = 300f, h = 80f;
        var button = UiFactory.CreateButton("Mode" + mode, _root, UiFactory.White, V3.A(Color.white, 0.9f), () =>
        {
            if (!_selecting)
                StartCoroutine(SelectAfterPress(mode));
        });
        button.transition = Selectable.Transition.None;
        var rect = V3.At((RectTransform)button.transform, 40f, y, w, h);
        V3.Hairline(rect, V3.Ink, 0f, 0f, w, h);
        V3.Brackets(rect, 0f, 0f, w, h, 7f, 1.5f, 4f, false);
        var chipImage = V3.Sprite("Chip", rect, UiFactory.Circle, chip, 17f, 17f, 46f, 46f);
        V3.Picture("Icon", chipImage.transform, icon, 8f, 8f, 30f, 30f);
        _modeLabels[index] = V3.Text("Label", rect, V3.Zh, "", 26f, V3.Ink, 81f, 0f, 150f, h, TextAlignmentOptions.Left);
        // the arrow sprite is a 40-unit square with the arrow across y 14-26
        V3.Sprite("Arrow", rect, LongArrowSprite(), V3.Ink, 221f, 20f, 40f, 40f);
        rect.gameObject.AddComponent<V3PressTint>().Target = (Image)button.targetGraphic;
    }

    private IEnumerator SelectAfterPress(MetricTest.TestMode mode)
    {
        _selecting = true;
        // let the pressed state show before the home goes away
        yield return new WaitForSecondsRealtime(0.15f);
        _onSelect?.Invoke(mode);
    }

    // ==================== chrome ====================

    private void BuildChrome()
    {
        _appName = V3.Text("AppName", _root, V3.Zh, "", 22f, V3.Ink, 40f, 34f, 600f, 44f, TextAlignmentOptions.Left, 0.1f);
        _appIcon = V3.Picture("EyeIcon", _root, "V3/eye", 0f, 44f, 24f, 24f).rectTransform;
        _appName.gameObject.AddComponent<V3Follow>().Init(_appName, _appIcon, 10f);

        // zh / EN toggle: 126 x 44, 1.5 px ink frame, an ink knob sliding between two 60 x 37 cells
        var lang = V3.Rect("Lang", _root, 800f, 34f, 126f, 44f);
        V3.Box("Fill", lang, Color.white, 0f, 0f, 126f, 44f);
        V3.Hairline(lang, V3.Ink, 0f, 0f, 126f, 44f, 1.5f);
        _knob = V3.Box("Knob", lang, V3.Ink, 2.5f, 3.5f, 60f, 37f).rectTransform;
        _langZh = LangCell(lang, 2.5f, "中文", 0);
        _langEn = LangCell(lang, 63.5f, "EN", 1);

        _clock = V3.Text("Clock", _root, V3.Mono, "", 10f, V3.A(V3.Ink, 0.65f), 800f, 84f, 184f, 16f, TextAlignmentOptions.Right, 0.08f);
        _readout1 = V3.Text("Readout1", _root, V3.Mono, "", 12f, V3.A(V3.Ink, 0.8f), 784f, 548f, 200f, 15f, TextAlignmentOptions.Right, 0.06f);
        _readout2 = V3.Text("Readout2", _root, V3.Mono, "", 12f, V3.A(V3.Ink, 0.8f), 784f, 564f, 200f, 15f, TextAlignmentOptions.Right, 0.06f);
    }

    private TextMeshProUGUI LangCell(Transform parent, float x, string text, int lang)
    {
        var button = UiFactory.CreateButton("Lang" + lang, parent, UiFactory.White, new Color(1f, 1f, 1f, 0f), () =>
        {
            if (V3.Lang != lang)
                V3Sounds.Play(V3Sounds.Cue.Press);
            V3.SetLang(lang);
        });
        button.transition = Selectable.Transition.None;
        V3.At((RectTransform)button.transform, x, 3.5f, 60f, 37f);
        return V3.Text("Label", button.transform, V3.Zh, text, 17f, V3.Text2, 0f, 0f, 60f, 37f, TextAlignmentOptions.Center);
    }

    private void ApplyLanguage()
    {
        _appName.text = V3.T("眼動視力檢測", "Eye-Tracking Vision Test");
        _appName.characterSpacing = V3.Lang == 1 ? 2f : 10f;
        _modeLabels[0].text = V3.T("雙眼檢測", "Both eyes");
        _modeLabels[1].text = V3.T("單眼檢測", "One eye");
        _langZh.color = V3.Lang == 0 ? Color.white : V3.Text2;
        _langEn.color = V3.Lang == 1 ? Color.white : V3.Text2;
    }

    private void Awake() => V3.LangChanged += OnLangChanged;
    private void OnDestroy() => V3.LangChanged -= OnLangChanged;

    private void OnLangChanged()
    {
        if (_appName == null)
            return;
        ApplyLanguage();
        if (isActiveAndEnabled)
            V3.Speak("請選擇要進行的測驗");
    }

    // ==================== cursor ====================

    // The dial cursor (as on the flow pages, 80 px), with a violet dwell arc while the gaze rests on the E.
    private void BuildCursor()
    {
        _cursor = V3.Rect("Cursor", _root, 0f, 0f, 80f, 80f);
        _cursor.gameObject.AddComponent<Canvas>();
        _cursor.pivot = UiFactory.Center;
        At(UiFactory.CreateImage("Disc", _cursor, UiFactory.Circle, V3.A(Color.white, 0.5f)), 0f, 0f, 34f, 34f);
        At(UiFactory.CreateImage("Outline", _cursor, V3Setup.RingSprite(25.5f, 3.6f), Color.white), 0f, 0f, 80f, 80f);
        At(UiFactory.CreateImage("Ring", _cursor, V3Setup.RingSprite(25.5f, 1.8f), V3.Ink), 0f, 0f, 80f, 80f);
        _cursorTicks = At(UiFactory.CreateImage("Ticks", _cursor, GazeVisual.TickSprite(), V3.A(V3.Ink, 0.85f)), 0f, 0f, 80f, 80f).rectTransform;
        _cursorArc = At(UiFactory.CreateImage("Dwell", _cursor, V3Setup.RingSprite(25.5f, 3.4f), V3.Violet), 0f, 0f, 80f, 80f);
        _cursorArc.type = Image.Type.Filled;
        _cursorArc.fillMethod = Image.FillMethod.Radial360;
        _cursorArc.fillOrigin = (int)Image.Origin360.Top;
        At(UiFactory.CreateImage("DotEdge", _cursor, UiFactory.Circle, Color.white), 0f, 0f, 11f, 11f);
        At(UiFactory.CreateImage("Dot", _cursor, UiFactory.Circle, V3.Violet), 0f, 0f, 8f, 8f);
        _cursorMs = V3.Text("Ms", _cursor, V3.Mono, "", 10f, V3.Violet, 62f, 14f, 80f, 12f, TextAlignmentOptions.TopLeft, 0.06f);
        _cursor.gameObject.SetActive(false);
    }

    // ==================== frame ====================

    private void SetLevel(int n)
    {
        _level = n;
        _sizeTo = 190f - (n - 1) * 11f;   // shrinks per level but stays readable on the home
        _lvText.text = $"LV <color=#6E56CF>{n:00}</color>";
        _vaText.text = $"VA {VA[n - 1].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} · {Mathf.RoundToInt(TestSize[n - 1] * 1024f / 1920f)} px";
        for (int i = 0; i < 11; i++)
        {
            bool on = i == n - 1, past = i < n - 1;
            _levelTicks[i].color = on ? V3.Violet : past ? V3.Ink : V3.A(V3.Ink, 0.25f);
            _levelTicks[i].rectTransform.sizeDelta = new Vector2(12f, on ? 4f : 2f);
            _levelLabels[i].color = on ? V3.Violet : V3.A(V3.Ink, 0.6f);
            _levelLabels[i].fontSize = on ? 13f : 10f;
        }
    }

    private static float EaseBack(float t)
    {
        const float c = 1.6f;
        return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
    }

    private void Update()
    {
        float now = Time.unscaledTime, dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
        // a touch on empty space stands in for the gaze
        if (Input.touchCount > 0 && EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
        {
            var t = Input.GetTouch(0).position;
            Look(t.x * V3.W / Screen.width, (Screen.height - t.y) * V3.W / Screen.width);
        }
        bool idle = Idle;
        float k = 1f - Mathf.Pow(0.78f, dt * 60f);
        _gx += (_tx - _gx) * k;
        _gy += (_ty - _gy) * k;

        // the E steps down a level and turns to a new direction
        if (now > _nextAt)
        {
            _nextAt = now + StepSeconds;
            _rotFrom = Rot[_dir];
            int nd;
            do { nd = UnityEngine.Random.Range(0, 4); } while (nd == _dir);
            _dir = nd;
            _rotT = 0f;
            SetLevel(_level % 11 + 1);
        }
        _rotT = Mathf.Min(1f, _rotT + dt / 0.6f);
        _sizeNow += (_sizeTo - _sizeNow) * (1f - Mathf.Pow(0.85f, dt * 60f));
        float to = Rot[_dir];
        while (to - _rotFrom > Mathf.PI) to -= 2f * Mathf.PI;
        while (to - _rotFrom < -Mathf.PI) to += 2f * Mathf.PI;
        _eTransform.localEulerAngles = new Vector3(0f, 0f, (_rotFrom + (to - _rotFrom) * EaseBack(_rotT)) * Mathf.Rad2Deg);
        _eTransform.localScale = Vector3.one * (_sizeNow * UPP);
        _oriText.text = $"ORI <color=#6E56CF>{OriDeg[_dir]:000}°</color>";

        // the E leans a little toward the gaze; the dial layers shift with it, the front ones most
        float tx = idle ? Mathf.Sin(now / 1.7f) * 0.12f : Mathf.Clamp((_gx - CX) / 900f, -0.3f, 0.3f);
        float ty = idle ? Mathf.Cos(now / 2.1f) * 0.08f : Mathf.Clamp((_gy - CY) / 900f, -0.25f, 0.25f);
        _leanX += (tx - _leanX) * 0.06f;
        _leanY += (ty - _leanY) * 0.06f;
        _holder.localEulerAngles = new Vector3(-_leanY * Mathf.Rad2Deg, -_leanX * Mathf.Rad2Deg, 0f);
        for (int i = 0; i < 3; i++)
            _layers[i].anchoredPosition = new Vector2(CX + _leanX * LayerShift[i], -CY - _leanY * LayerShift[i]);

        // gaze angle on the outer ring, with afterimages over the last 0.7 s
        float ang = Mathf.Atan2(_gy - CY, _gx - CX), dist = Mathf.Sqrt((_gx - CX) * (_gx - CX) + (_gy - CY) * (_gy - CY));
        UpdateLive(now, idle, ang);
        float deg = Mathf.Repeat(ang * Mathf.Rad2Deg + 90f, 360f);
        _readout1.text = idle ? "<color=#14141473>GAZE</color>  X ----  Y ----" : $"<color=#14141473>GAZE</color>  X {Mathf.RoundToInt(_gx):0000}  Y {Mathf.RoundToInt(_gy):0000}";
        _readout2.text = idle ? "<color=#14141473>θ</color> ---°  <color=#14141473>R</color> ---" : $"<color=#14141473>θ</color> {Mathf.RoundToInt(deg):000}°  <color=#14141473>R</color> {Mathf.RoundToInt(dist):000}";
        _clock.text = DateTime.Now.ToString("HH:mm:ss") + "  ·  SID EM-" + DateTime.Now.ToString("MMdd-HH");

        UpdatePath(now, idle);
        UpdateHeadline(dt, idle);
        UpdateCursor(now, idle, dist);
        // knob slides between the language cells
        float knobX = 2.5f + V3.Lang * 61f;
        _knob.anchoredPosition = new Vector2(Mathf.Lerp(_knob.anchoredPosition.x, knobX, 1f - Mathf.Exp(-dt / 0.06f)), _knob.anchoredPosition.y);
    }

    private void UpdateLive(float now, bool idle, float ang)
    {
        if (!idle)
            _angleHistory.Add((ang, now));
        while (_angleHistory.Count > 0 && (now - _angleHistory[0].time > 0.7f || idle))
            _angleHistory.RemoveAt(0);
        for (int i = 0; i < _ghosts.Length; i++)
        {
            float age = (i + 1) / 12f * 0.7f;
            int found = _angleHistory.FindIndex(s => now - s.time <= age + 0.035f && now - s.time >= age - 0.035f);
            _ghosts[i].enabled = found >= 0;
            if (found < 0)
                continue;
            _ghosts[i].color = V3.A(V3.Violet, (1f - age / 0.7f) * 0.45f);
            PlaceArc(_ghosts[i], _angleHistory[found].angle, 0.1f);
        }
        _liveArc.enabled = !idle;
        _livePointer.gameObject.SetActive(!idle);
        if (idle)
            return;
        PlaceArc(_liveArc, ang, 0.12f);
        var p = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang)) * (R1 - 10f);
        _livePointer.anchoredPosition = p;
        _livePointer.localEulerAngles = new Vector3(0f, 0f, -ang * Mathf.Rad2Deg - 90f);
    }

    private void UpdatePath(float now, bool idle)
    {
        if (!idle && now - _pathAt > 0.06f)
        {
            _pathPoints.Add((new Vector2(_gx, _gy), now));
            _pathAt = now;
        }
        while (_pathPoints.Count > 0 && (_pathPoints.Count > _path.Length || now - _pathPoints[0].t > 2.6f || idle))
            _pathPoints.RemoveAt(0);
        for (int i = 0; i < _path.Length; i++)
        {
            bool on = i < _pathPoints.Count;
            _path[i].enabled = on;
            if (!on)
                continue;
            var p = _pathPoints[i].p;
            _path[i].rectTransform.anchoredPosition = new Vector2(p.x - 1.5f, -(p.y - 1.5f));
            _path[i].color = V3.A(V3.Ink, 0.15f + (i + 1f) / _pathPoints.Count * 0.6f);
        }
    }

    private void UpdateCursor(float now, bool idle, float dist)
    {
        _cursor.gameObject.SetActive(!idle);
        if (idle)
        {
            _dwellFrom = -1f;
            return;
        }
        _cursor.anchoredPosition = new Vector2(_gx, -_gy);
        _cursorTicks.localEulerAngles = new Vector3(0f, 0f, -now * 30f);
        // a dwell arc while the gaze rests on the E (inside the inner ring) - decoration, nothing is selected
        if (dist < R2 - 20f) { if (_dwellFrom < 0f) _dwellFrom = now; }
        else _dwellFrom = -1f;
        float f = _dwellFrom < 0f ? 0f : now - _dwellFrom;
        _cursorArc.fillAmount = Mathf.Min(1f, f / 1.2f);
        _cursorMs.text = f > 0.05f ? Mathf.RoundToInt(f * 1000f) + " ms" : "";
    }

    // ==================== sprites ====================

    private static Sprite s_lens, s_tri, s_arrow;
    private static readonly Dictionary<string, Sprite> s_rings = new Dictionary<string, Sprite>();

    // Lens disc: white in the middle, fading into the page colour at r 250 (Design: radial gradient).
    private static Sprite LensSprite()
    {
        return s_lens != null ? s_lens : (s_lens = V3.Raster(256, p =>
        {
            float r = (p - new Vector2(128f, 128f)).magnitude / 128f;
            Color c;
            if (r < 0.62f) c = Color.Lerp(Color.white, new Color32(0xFC, 0xFC, 0xFB, 0xFF), r / 0.62f);
            else if (r < 0.85f) c = Color.Lerp(new Color32(0xFC, 0xFC, 0xFB, 0xFF), new Color(0.969f, 0.969f, 0.961f, 0.8f), (r - 0.62f) / 0.23f);
            else c = Color.Lerp(new Color(0.969f, 0.969f, 0.961f, 0.8f), new Color(0.969f, 0.969f, 0.961f, 0f), Mathf.Clamp01((r - 0.85f) / 0.15f));
            return c;
        }));
    }

    // An arc of radius r (px) between two canvas-style angles (degrees, clockwise from +x on a y-down screen),
    // drawn 1:1 in a (2r + 16) square.
    private static Sprite ArcSprite(float r, float halfWidth, float from, float to)
    {
        string key = $"arc{r}/{halfWidth}/{from}/{to}";
        if (s_rings.TryGetValue(key, out var cached))
            return cached;
        int size = Mathf.CeilToInt(2f * r + 16f);
        float c = size / 2f;
        var sprite = V3.Raster(size, p =>
        {
            Vector2 q = new Vector2(p.x - c, c - p.y);   // y down
            float a = Mathf.Repeat(Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg, 360f);
            bool inside = to - from >= 360f || (a >= from && a <= to);
            float d = Mathf.Abs(q.magnitude - r) - halfWidth;
            return new Color(1f, 1f, 1f, inside ? Mathf.Clamp01(0.5f - d) : 0f);
        });
        s_rings[key] = sprite;
        return sprite;
    }

    // Brushed-silver bezel: a 2-3 px ring lit from the upper left - bright on that side and along the tube's
    // upper edge, darker toward the lower right.
    private static Sprite SilverRing(float r, float halfWidth)
    {
        string key = $"silver{r}/{halfWidth}";
        if (s_rings.TryGetValue(key, out var cached))
            return cached;
        int size = Mathf.CeilToInt(2f * r + 16f);
        float c = size / 2f;
        Color dark = new Color32(0x8A, 0x8C, 0x93, 0xFF), mid = new Color32(0xC4, 0xC5, 0xCB, 0xFF), light = new Color32(0xF6, 0xF7, 0xFA, 0xFF);
        var sprite = V3.Raster(size, p =>
        {
            Vector2 q = new Vector2(p.x - c, p.y - c);   // y up
            float len = q.magnitude;
            float d = Mathf.Abs(len - r) - halfWidth;
            float alpha = Mathf.Clamp01(0.5f - d);
            if (alpha <= 0f)
                return new Color(1f, 1f, 1f, 0f);
            Vector2 n = q / Mathf.Max(1e-3f, len);
            float facing = Vector2.Dot(n, new Vector2(-0.7071f, 0.7071f));   // toward the light
            float across = Mathf.Clamp((len - r) / Mathf.Max(0.5f, halfWidth), -1f, 1f);
            float shade = Mathf.Clamp01(0.5f + 0.35f * facing - 0.25f * across * facing);
            Color col = shade < 0.5f ? Color.Lerp(dark, mid, shade * 2f) : Color.Lerp(mid, light, (shade - 0.5f) * 2f);
            col.a = alpha;
            return col;
        });
        s_rings[key] = sprite;
        return sprite;
    }

    // Small pointer triangle (10 x 9) with its tip up.
    private static Sprite TriangleSprite()
    {
        return s_tri != null ? s_tri : (s_tri = V3.Raster(40, p =>
        {
            float x = p.x / 40f * 10f - 5f, y = p.y / 40f * 10f;   // y up, tip at y 9
            float halfAtY = 5f * (1f - y / 9f);
            bool inside = y >= 0f && y <= 9f && Mathf.Abs(x) <= halfAtY;
            return new Color(1f, 1f, 1f, inside ? 1f : 0f);
        }));
    }

    // The mode keys' long thin arrow (Design: 40 x 12, 1.5 px stroke), in a 40-unit square sprite.
    private static Sprite LongArrowSprite()
    {
        return s_arrow != null ? s_arrow : (s_arrow = V3.Raster(160, p =>
        {
            Vector2 q = new Vector2(p.x / 4f, 40f - p.y / 4f);   // y down
            q.y -= 14f;   // the arrow lives in y 14-26
            float d = Seg(q, new Vector2(0f, 6f), new Vector2(38f, 6f));
            d = Mathf.Min(d, Seg(q, new Vector2(32f, 1f), new Vector2(38f, 6f)));
            d = Mathf.Min(d, Seg(q, new Vector2(32f, 11f), new Vector2(38f, 6f)));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - (d - 0.75f) * 4f));
        }));
    }

    private static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    // The optotype E (5 x 5 grid) with an edge softness of about the given px at 41 px (the lens blur). The
    // sprite has a 12 % margin on each side for the soft edge.
    private static Sprite BlurredE(float blurPx)
    {
        const int size = 96;
        const float m = 0.6f;   // grid units of margin (12 % of 5)
        float unitsPerTexel = (5f + 2f * m) / size;
        float soft = Mathf.Max(unitsPerTexel, blurPx * 5f / 41f);
        return V3.Raster(size, p =>
        {
            float x = p.x * unitsPerTexel - m, y = (size - p.y) * unitsPerTexel - m;   // y down
            float d = Mathf.Min(Box(x, y, 0f, 0f, 1f, 5f), Mathf.Min(Box(x, y, 0f, 0f, 5f, 1f), Mathf.Min(Box(x, y, 0f, 2f, 5f, 3f), Box(x, y, 0f, 4f, 5f, 5f))));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d / soft));
        });
    }

    // signed distance to an axis-aligned box [x0,x1] x [y0,y1]
    private static float Box(float x, float y, float x0, float y0, float x1, float y1)
    {
        float dx = Mathf.Max(x0 - x, x - x1), dy = Mathf.Max(y0 - y, y - y1);
        return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f);
    }
}

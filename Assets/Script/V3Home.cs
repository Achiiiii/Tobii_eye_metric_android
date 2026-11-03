using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// V3.1 home ("an instrument on a light box"). The page is the light box. In the middle, the E with its dial: degree
// ring with the live gaze angle and its afterimages, the level scale 0.1-2.0 with a green light guide up to the
// current level, in three layers that shift by different amounts as the E leans toward the gaze. The E and the two
// rings are real 3D under one soft light from above (a dark satin E, brushed-steel rings mirroring a studio and the
// light box) with soft contact shadows on the page. The E leads the eye: it points at each mode in turn, runs two
// test steps as a demo, turns toward the mode being looked at, and on a choice rushes forward into the next page. EYE METRIC runs across the top behind the dial, seen through a lens (letters away from the
// focus point go soft; the focus racks along the word, or follows the gaze on it). The two modes are backlit
// windows either side of the E. Layout in the 1024 x 600 reference space, origin top-left.
public class V3Home : MonoBehaviour
{
    private const float CX = 512f, CY = 334f, R1 = 168f, R2 = 140f, R3 = R1 + 36f;
    private const float InnerR = R2 - 28f, OuterTube = 3.5f, InnerTube = 2.5f;
    // the two bezels: (radius, tube radius) in px
    private static readonly (float r, float tube)[] Rings = { (R1, OuterTube), (InnerR, InnerTube) };
    private const float RingReach = R1 + OuterTube;
    private const float ESizeMax = 290f, ESizeMin = 220f;   // the E's width at level 1 and 11
    private const float ViewTiltDeg = 10f;   // the E and rings seen a little from above
    private const float Fov = 30f, CamDist = 12f;
    private const int WorldLayer = 31;
    private const float IdleSeconds = 2.5f;
    private static readonly float[] VA = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.7f, 0.8f, 1.0f, 1.3f, 1.5f, 2.0f };
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
    private readonly RectTransform[] _eShadows = new RectTransform[3];
    // the light that comes through the frosted E onto the light box, just below it (inside its shadow)
    private Image _eLight;
    private RenderTexture _rt;
    private RawImage _view;

    // dial layers (back, mid, front) and their parallax amounts (px per radian of lean)
    private readonly RectTransform[] _layers = new RectTransform[3];
    private static readonly float[] LayerShift = { 10f, 18f, 26f };
    private Image[] _levelTicks = new Image[11];
    private TextMeshProUGUI[] _levelLabels = new TextMeshProUGUI[11];
    private Image _progGlow, _progBody, _progCore;
    private RectTransform _progTip;

    // headline
    private RectTransform _headline;
    // each letter: its window onto the headline's coverage textures (the windows tile the word, so every pixel is drawn
    // once and the blur, worked out per pixel, runs on without a seam); one lens material for all
    private readonly List<RawImage> _letters = new List<RawImage>();
    private Material _lens;
    // the letters as typeset (character, left, width, font size) - drawn into _headTex[0] on the first show
    private readonly List<(char ch, float x, float w, float size)> _headType = new List<(char, float, float, float)>();
    private Texture2D[] _headTex;
    private const int HeadLayer = 30;
    private const float HeadBoxH = HeadMaxH * 1.4f, LevelSigma = 1.2f;   // blur levels 1-3: sigma 1.2, 2.4, 3.6 px
    private float _focusX, _focusDir = 1f, _headLeft, _headRight;
    // EYE METRIC: centred across the top, as wide as 944 px allows but no taller than HeadMaxH
    private const float HeadY = 84f, HeadMaxW = 944f, HeadMaxH = 180f;
    private static readonly Color HeadInk = new Color32(0x3A, 0x3D, 0x43, 0xFF);

    // chrome
    private TextMeshProUGUI _appName, _jointCheck;
    private RectTransform _appIcon, _knob;
    private TextMeshProUGUI _langZh, _langEn;
    private TextMeshProUGUI[] _modeLabels = new TextMeshProUGUI[2];
    private bool _selecting;


    // gaze (reference px, y down)
    private float _tx = CX, _ty = CY, _gx = CX, _gy = CY;
    private float _lastSample = -100f;
    private bool _hasSample;
    private bool Idle => Time.unscaledTime - _lastSample > IdleSeconds;

    // E sequence
    private int _level = 1, _dir;
    private float _rotFrom, _rotT = 1f, _sizeNow, _sizeTo;
    private float _leanX, _leanY;
    private static readonly float[] Rot = { 0f, -Mathf.PI / 2f, Mathf.PI, Mathf.PI / 2f };   // right, down, left, up
    private const int DirRight = 0, DirLeft = 2;

    // V3.1: backdrop, foreground, intro, the E's guidance, the gaze selection and the leaving transition
    private Transform _ringRoot;
    private readonly List<(Graphic g, float a)> _shadows = new List<(Graphic, float)>();
    private CanvasGroup _backdrop, _fgGroup, _modesGroup, _chromeGroup;
    private RectTransform _fg;
    private readonly List<(Image img, float deg)> _degTicks = new List<(Image, float)>();
    private readonly RectTransform[] _modeRects = new RectTransform[2];
    private readonly MetricTest.TestMode[] _modeOf = { MetricTest.TestMode.Both, MetricTest.TestMode.Single };
    private readonly float[] _lift = new float[2];
    private Image _dot, _iris;
    // leaving: the guide running round the outer bezel, the rings' lock click, the E's gap steered to the centre
    private Image _lockArc;
    private Transform _eMesh;
    // the E in milky frosted glass (V3/Frost): the green of the light inside it, and how heavily it is frosted
    private static readonly Color FrostLight = new Color32(0x7E, 0xF0, 0xBE, 0xFF);
    private const float FrostAmount = 0.5f;
    private Material _frostFace, _frostEdge;
    // the callout from the E to the caption: a marker on the E's side facing the caption, a slanted leg and a level run
    // to the caption's code line; the marker follows the E (its turn, size and lean)
    private static readonly Vector3[] CalloutSides = { new Vector3(0.53f, 0.05f, 0f), new Vector3(-0.53f, 0.05f, 0f), new Vector3(0f, 0.05f, 0.53f), new Vector3(0f, 0.05f, -0.53f) };
    private const float CalloutY = 379f, CalloutEnd = 781f;
    private RectTransform _calloutMark, _calloutLeg, _calloutRun;
    // the framing marks: an L at each corner of the E's square, a little outside it, turning and leaning with it
    private static readonly Vector3[] BracketCorners = { new Vector3(-0.58f, 0.05f, 0.58f), new Vector3(0.58f, 0.05f, 0.58f), new Vector3(0.58f, 0.05f, -0.58f), new Vector3(-0.58f, 0.05f, -0.58f) };
    private const float BracketArm = 20f;
    private readonly RectTransform[] _brackets = new RectTransform[8];
    private Vector2 _calloutAt;
    private bool _calloutSet;
    private float _lockT0 = -100f, _gapSteer;
    private bool _sweepDone;
    private CanvasGroup _talk;
    private static readonly Vector3 GapLocal = new Vector3(0.1f, 0f, -0.2f);   // the centre of one of the E's gaps
    private TextMeshProUGUI _pct;
    private enum Stage { Loading, Grow, Idle, Leaving }
    private Stage _stage;
    private float _stageT0, _phaseT0, _eZoom = 1f, _introE = 1f, _nodT0 = -100f;
    private int _phase, _demo, _chosen = -1;
    private bool _guided, _swung;

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
                cam.cullingMask &= ~(1 << WorldLayer | 1 << HeadLayer);
        foreach (var light in FindObjectsOfType<Light>())
            if (light.transform.parent != _world.transform)
                light.cullingMask &= ~(1 << WorldLayer);
        if (_headTex == null)
            RenderHeadline();
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
        SetLevel(4);
        _sizeNow = _sizeTo;
        _dir = DirRight;
        _rotFrom = 0f;
        _rotT = 1f;
        ApplyLanguage();
        StartIntro();
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

        // one soft light from above, a little to the left and toward the viewer
        var key = AddChild<Light>("Key", _world.transform);
        key.type = LightType.Directional;
        key.color = new Color32(0xFF, 0xFC, 0xF7, 0xFF);
        key.intensity = 1f;
        key.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.3f, 0.85f, -0.45f));
        key.shadows = LightShadows.None;
        key.cullingMask = 1 << WorldLayer;

        _holder = new GameObject("Holder").transform;
        _holder.SetParent(_world.transform, false);
        _holder.gameObject.layer = WorldLayer;
        _eTransform = new GameObject("Turn").transform;
        _eTransform.SetParent(_holder, false);
        _eTransform.gameObject.layer = WorldLayer;
        // a block of milky frosted glass with polished edges (V3/Frost; the headline's blurred coverage, which it shows
        // through, is set once the headline is typeset)
        _frostFace = new Material(LoadShader("Frost"));
        _frostEdge = new Material(LoadShader("Frost"));
        _frostEdge.SetFloat("_Edge", 1f);
        foreach (var m in new[] { _frostFace, _frostEdge })
        {
            m.SetTexture("_Glow", BlurredE(12f).texture);
            m.SetColor("_Light", FrostLight);
            m.SetFloat("_Frost", FrostAmount);
            m.SetFloat("_HeadY", HeadY);
            m.SetFloat("_HeadH", HeadBoxH);
            m.SetVector("_Ink", new Vector4(0x3A / 255f, 0x3D / 255f, 0x43 / 255f, 1f));
        }
        var e = AddMesh("E", _eTransform, BuildEMesh(), _frostFace);
        e.sharedMaterials = new[] { _frostFace, _frostEdge };
        _eMesh = e.transform;
        // a light rim round the E with a touch of the green, just behind it (hidden where the E is in front), as if lit
        // from behind, and a wide green glow spilling onto the light box
        var rimMat = new Material(LoadShader("Shadow")) { mainTexture = BlurredE(0.7f).texture };
        rimMat.SetColor("_Color", new Color(0.74f, 0.98f, 0.86f, 0.7f));
        var rim = AddMesh("Rim", _eTransform, Quad(), rimMat);
        rim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rim.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        rim.transform.localScale = Vector3.one * 1.24f * 1.06f;
        var haloMat = new Material(LoadShader("Shadow")) { mainTexture = BlurredE(6f).texture };
        haloMat.SetColor("_Color", V3.A(V3.Green, 0.16f));
        var halo = AddMesh("Halo", _eTransform, Quad(), haloMat);
        halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        halo.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        halo.transform.localScale = Vector3.one * 1.24f * 1.7f;
        e.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        e.receiveShadows = false;
        // the mesh lies flat (thickness along y): stand it up facing the camera
        e.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        // the outer and inner bezels: brushed-steel rings, leaning with the E
        var steel = V3Bezel.Steel(260f);
        _ringRoot = new GameObject("Rings").transform;
        _ringRoot.SetParent(_holder, false);
        _ringRoot.gameObject.layer = WorldLayer;
        foreach (var (r, tube) in Rings)
            AddMesh("Ring", _ringRoot, V3Bezel.Mesh(r * UPP, 2f * tube * UPP, 0.9f * tube * UPP), steel);

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

    private static Mesh Quad()
    {
        var mesh = new Mesh { name = "Quad" };
        mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
        mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
        mesh.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // All the rings' silhouette, softened by about softPx, for their shadow.
    private static Texture RingShadow(float softPx)
    {
        const int size = 512;
        float half = RingReach + 18f, pxPerTexel = 2f * half / size;
        return V3.Raster(size, p =>
        {
            float x = p.x * pxPerTexel - half, y = p.y * pxPerTexel - half;
            float d = Mathf.Sqrt(x * x + y * y);
            float edge = float.MaxValue;
            foreach (var (r, tube) in Rings)
                edge = Mathf.Min(edge, Mathf.Abs(d - r) - tube);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - edge / Mathf.Max(pxPerTexel, softPx)));
        }).texture;
    }

    // The E optotype: the 5 x 5 grid outline extruded with wide 45-degree chamfers, 1 unit wide, lying flat (thickness
    // along y, centred), opening toward +x. A machined block (depth 0.5 of 5, chamfers 0.16); the faces are submesh 0,
    // the chamfers and walls submesh 1.
    private static Mesh BuildEMesh()
    {
        int[,] grid = { { 0, 0 }, { 5, 0 }, { 5, 1 }, { 1, 1 }, { 1, 2 }, { 5, 2 }, { 5, 3 }, { 1, 3 }, { 1, 4 }, { 5, 4 }, { 5, 5 }, { 0, 5 } };
        int n = grid.GetLength(0);
        const float unit = 0.2f, depth = 0.5f * unit, bevelT = 0.16f * unit, bevelS = 0.16f * unit;
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
        var faces = new List<int>();
        var sides = new List<int>();
        var tris = faces;
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
        tris = sides;
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
        // brushed across the E (V3/Metal reads the tangents)
        var tangents = new List<Vector4>();
        for (int i = 0; i < verts.Count; i++)
            tangents.Add(new Vector4(1f, 0f, 0f, 1f));
        mesh.subMeshCount = 2;
        mesh.SetTangents(tangents);
        mesh.SetTriangles(faces, 0);
        mesh.SetTriangles(sides, 1);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ==================== UI ====================

    private void BuildUi()
    {
        _canvas = UiFactory.CreateOverlayCanvas("V3HomeCanvas", 6, transform);
        _root = (RectTransform)_canvas.transform;
        // the light box: an evenly lit white panel (a touch brighter at the top) in a brushed aluminium frame
        V3.LightBox(_root, true);

        BuildBackdrop();
        BuildShadows();
        BuildHeadline();
        BuildDial();
        _view = new GameObject("E", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        _view.transform.SetParent(_root, false);
        UiFactory.Stretch(_view.rectTransform);
        _view.raycastTarget = false;
        // over the 3D rings: the light that runs once round the outer bezel when a mode is chosen
        _lockArc = Arc(Layer("Lock"), ArcSprite(R1, 1.2f, 0f, 360f), "Lock");
        _lockArc.color = V3.Green;
        _lockArc.enabled = false;
        BuildForeground();
        _chromeGroup = Group("Chrome");
        _modesGroup = Group("Modes");
        BuildModes();
        BuildChrome();
        BuildIntro();
    }

    // A full-screen group that fades as one (chrome, modes).
    private CanvasGroup Group(string name)
    {
        var rect = V3.Full(name, _root);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    // ==================== backdrop and foreground ====================

    // The light box's drawing: eight faint dashed contour lines round the dial (like a topographic chart). Drawn once
    // into one texture.
    private void BuildBackdrop()
    {
        var image = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(_root, false);
        UiFactory.Stretch(image.rectTransform);
        image.raycastTarget = false;
        image.texture = BackdropTexture();
        _backdrop = image.gameObject.AddComponent<CanvasGroup>();
    }

    private static Texture2D s_backdrop;

    // The contact shadows on the light box - the rings' (a tight dark one and a faint wide one) and the E's, which
    // follows the E's turn and size - drawn under the headline so they never darken its letters.
    private void BuildShadows()
    {
        var layer = V3.Full("Shadows", _root);
        layer.gameObject.AddComponent<Canvas>();
        float span = 2f * (RingReach + 18f);
        foreach (var (texture, alpha, down) in new (Texture, float, float)[] { (RingShadow(7f), 0.16f, 6f), (RingShadow(1.8f), 0.42f, 2.5f) })
        {
            var ring = new GameObject("RingShadow", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            ring.transform.SetParent(layer, false);
            ring.texture = texture;
            ring.raycastTarget = false;
            V3.At(ring.rectTransform, CX - span / 2f, CY + down - span / 2f, span, span);
            _shadows.Add((ring, alpha));
        }
        int n = 0;
        // the frosted E sits close on the light box: a soft shadow and a tight, crisp one where it touches
        foreach (var (blur, alpha, down) in new (float, float, float)[] { (1.6f, 0.1f, 5f), (0.3f, 0.26f, 1.2f) })
        {
            var e = UiFactory.CreateImage("EShadow", layer, BlurredE(blur), Color.white);
            e.raycastTarget = false;
            UiFactory.Place(e.rectTransform, new Vector2(0f, 1f), UiFactory.Center, new Vector2(CX, -(CY + down)), Vector2.zero);
            _shadows.Add((e, alpha));
            _eShadows[n++] = e.rectTransform;
        }
        _eLight = UiFactory.CreateImage("ELight", layer, BlurredE(1.2f), Color.clear);
        _eLight.raycastTarget = false;
        UiFactory.Place(_eLight.rectTransform, new Vector2(0f, 1f), UiFactory.Center, new Vector2(CX + 1.5f, -(CY + 7f)), Vector2.zero);
        _eShadows[n] = _eLight.rectTransform;
        SetShadows(0f);
    }

    private static Texture2D BackdropTexture()
    {
        if (s_backdrop != null)
            return s_backdrop;
        const int w = 1024, h = 600, bins = 1440;
        // contour radii by angle, from the design: base * (1 + .07 sin(3t + .9k) + .04 sin(5t + 1.7k) + .02 sin(9t + k))
        var radius = new float[8, bins];
        for (int k = 0; k < 8; k++)
            for (int b = 0; b < bins; b++)
            {
                float t = b * 2f * Mathf.PI / bins;
                radius[k, b] = (214f + k * 44f) * (1f + 0.07f * Mathf.Sin(3f * t + k * 0.9f) + 0.04f * Mathf.Sin(5f * t + k * 1.7f) + 0.02f * Mathf.Sin(9f * t + k));
            }
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sx = x + 0.5f, sy = h - y - 0.5f;   // screen px, y down
                float dx = (sx - CX) / 1.18f, dy = (sy - CY) / 0.92f;
                float rn = Mathf.Sqrt(dx * dx + dy * dy), t = Mathf.Atan2(dy, dx);
                int b = (int)(Mathf.Repeat(t, 2f * Mathf.PI) / (2f * Mathf.PI) * bins) % bins;
                float alpha = 0f;
                if (rn > 180f && Mathf.Repeat(t * rn, 6f) < 2f)
                    for (int k = 0; k < 8; k++)
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(1f - Mathf.Abs(rn - radius[k, b]) / 1.25f) * (0.32f - k * 0.022f));
                px[y * w + x] = new Color32(0x14, 0x14, 0x14, (byte)(alpha * 255f));
            }
        s_backdrop = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        s_backdrop.SetPixels32(px);
        s_backdrop.Apply(false, true);
        return s_backdrop;
    }

    // In front of everything and nearest the viewer (the most parallax): two optotypes out of focus, in the empty space
    // either side of the dial.
    private void BuildForeground()
    {
        _fg = V3.Full("Foreground", _root);
        _fg.gameObject.AddComponent<Canvas>();
        _fgGroup = _fg.gameObject.AddComponent<CanvasGroup>();
        var items = new (float x, float y, float size, float rot, float blur, float alpha)[]
        {
            (150f, 330f, 58f, 90f, 4f, 0.16f), (965f, 300f, 28f, 180f, 1.4f, 0.30f),
            (60f, 560f, 130f, 0f, 9f, 0.10f), (990f, 580f, 70f, 270f, 6f, 0.12f),
        };
        foreach (var it in items)
        {
            float box = it.size * 1.24f;
            var e = V3.Sprite("E", _fg, BlurredE(it.blur * 41f / it.size), V3.A(V3.Ink, it.alpha), it.x - box / 2f, it.y - box / 2f, box, box);
            e.rectTransform.localEulerAngles = new Vector3(0f, 0f, -it.rot);
        }
    }

    // ==================== intro and transition overlays ====================

    // The intro's dot and percentage, and the iris that opens onto the next page.
    private void BuildIntro()
    {
        _dot = V3.Sprite("Dot", _root, UiFactory.Circle, V3.Ink, CX - 6f, CY - 6f, 12f, 12f);
        _pct = V3.Text("Percent", _root, V3.Num, "0%", 18f, V3.Ink, 784f, 94f, 200f, 20f, TextAlignmentOptions.Right);
        _iris = V3.Sprite("Iris", _root, UiFactory.Circle, V3.Cool, CX - 5f, CY - 5f, 10f, 10f);
        _iris.rectTransform.localScale = Vector3.zero;
        _iris.raycastTarget = true;
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
        // back: the dial reaching outward - a fine outer scale (1 deg steps, every 10 numbered) and the test's four
        // directions as small optotypes on dashed leader lines
        var back = _layers[0] = Layer("DialBack");
        BuildOuterScale(back);

        // mid: degree ring (every 2 deg, labels every 30; the ticks invert what is under them, so they read light where
        // they cross the headline), the engraving on the outer ring, the live gaze arc
        var mid = _layers[1] = Layer("DialMid");
        var invert = new Material(LoadShader("UIInvert"));
        for (int d = 0; d < 360; d += 2)
        {
            float len = d % 30 == 0 ? 10f : 3f;
            float a = d - 90f;
            var p = Polar(R1 + 4f + len / 2f, a);
            var tick = At(UiFactory.CreateImage("Tick", mid, UiFactory.White, new Color(1f, 1f, 1f, d % 30 == 0 ? 0.6f : 0.28f)), p.x, p.y, len, 1f, -a);
            tick.material = invert;
            _degTicks.Add((tick, d));
            if (d % 30 == 0)
            {
                var lp = Polar(R1 + 28f, a);
                var label = V3.Text("Deg", mid, V3.Mono, d.ToString("000"), 10f, V3.A(V3.Ink, 0.4f), 0f, 0f, 40f, 14f, TextAlignmentOptions.Center);
                UiFactory.Place(label.rectTransform, UiFactory.Center, UiFactory.Center, lp, new Vector2(40f, 14f));
            }
        }
        BuildEngraving(mid);

        // front: the level scale along the bottom (0.1 at 155 deg to 2.0 at 25 deg) - a shallow groove with a bright lower
        // lip, the green light guide up to the current level ending in a small bright tip, the 11 ticks and values
        var front = _layers[2] = Layer("DialFront");
        float scale = Mathf.Ceil(2f * R2 + 16f), lip = Mathf.Ceil(2f * (R2 + 4.5f) + 16f);
        At(UiFactory.CreateImage("Groove", front, ArcSprite(R2, 4f, 25f, 155f), new Color32(0x1C, 0x22, 0x2C, 0x2E)), 0f, 0f, scale, scale);
        At(UiFactory.CreateImage("Lip", front, ArcSprite(R2 + 4.5f, 0.5f, 25f, 155f), new Color(1f, 1f, 1f, 0.9f)), 0f, 0f, lip, lip);
        _progGlow = Arc(front, ArcSprite(R2, 4.5f, 0f, 360f), "GuideGlow");
        _progGlow.color = V3.A(V3.Green, 0.28f);
        _progBody = Arc(front, ArcSprite(R2, 3f, 0f, 360f), "Guide");
        V3.Liquid(_progBody, true, 1f, Mathf.Ceil(2f * R2 + 16f));
        _progBody.color = V3.Green;
        _progCore = Arc(front, ArcSprite(R2, 0.45f, 0f, 360f), "GuideCore");
        _progCore.color = new Color32(0xE9, 0xFF, 0xF2, 0x00);
        _progTip = At(UiFactory.CreateImage("Tip", front, UiFactory.Circle, V3.A(V3.Green, 0.45f)), 0f, 0f, 9f, 9f).rectTransform;
        // a few sparks off the guide's head, drifting back along the scale (the front layer's origin is the dial centre)
        V3Sparks.Attach(front, () =>
        {
            var p = _progTip.anchoredPosition;
            float a = Mathf.Atan2(p.y, p.x);
            return (new Vector2(p.x, -p.y), new Vector2(-Mathf.Sin(a), -Mathf.Cos(a)));
        }, 14f);
        At(UiFactory.CreateImage("TipCore", _progTip, UiFactory.Circle, new Color32(0xF4, 0xFF, 0xF8, 0xFF)), 0f, 0f, 3.6f, 3.6f);
        for (int i = 0; i < 11; i++)
        {
            float a = 155f - i * 13f;
            var p = Polar(R2, a);
            _levelTicks[i] = At(UiFactory.CreateImage("LevelTick", front, UiFactory.White, V3.Ink), p.x, p.y, 8f, 1f, -a);
            var lp = Polar(R2 - 16f, a);
            _levelLabels[i] = V3.Text("Level", front, V3.Mono, VA[i].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), 9f, V3.A(V3.Ink, 0.45f), 0f, 0f, 40f, 16f, TextAlignmentOptions.Center);
            UiFactory.Place(_levelLabels[i].rectTransform, UiFactory.Center, UiFactory.Center, lp, new Vector2(40f, 16f));
        }
    }

    private void BuildOuterScale(Transform parent)
    {
        var invert = new Material(LoadShader("UIInvert"));
        Image Ink(string name, Sprite sprite, float alpha) => UiFactory.CreateImage(name, parent, sprite, V3.A(V3.Ink, alpha));
        At(Ink("Circle", ArcSprite(R3, 0.5f, 0f, 360f), 0.14f), 0f, 0f, Mathf.Ceil(2f * R3 + 16f), Mathf.Ceil(2f * R3 + 16f));
        for (int d = 0; d < 360; d++)
        {
            if (Mathf.Min(d % 90, 90 - d % 90) < 8)
                continue;   // room for the direction markers
            float len = d % 10 == 0 ? 8f : d % 5 == 0 ? 5f : 2.5f, a = d - 90f;
            var p = Polar(R3 + len / 2f, a);
            var tick = At(UiFactory.CreateImage("Tick", parent, UiFactory.White, new Color(1f, 1f, 1f, d % 10 == 0 ? 0.4f : 0.17f)), p.x, p.y, len, 1f, -a);
            tick.material = invert;
            _degTicks.Add((tick, d));
            if (d % 10 == 0 && d % 30 != 0)
            {
                var label = V3.Text("Deg", parent, V3.Mono, d.ToString(), 7f, V3.A(V3.Ink, 0.25f), 0f, 0f, 30f, 10f, TextAlignmentOptions.Center);
                UiFactory.Place(label.rectTransform, UiFactory.Center, UiFactory.Center, Polar(R3 + 17f, a), new Vector2(30f, 10f));
            }
        }
        var e = BlurredE(0.4f);
        foreach (var (d, name) in new (int, string)[] { (0, "UP"), (90, "RIGHT"), (180, "DOWN"), (270, "LEFT") })
        {
            float a = d - 90f;
            for (float r = R1 + 16f; r < R3 + 10f; r += 5f)
            {
                var q = Polar(r + 1f, a);
                At(Ink("Lead", UiFactory.White, 0.3f), q.x, q.y, 2f, 1f, -a);
            }
            var ep = Polar(R3 + 20f, a);
            At(Ink("Dir", e, 0.55f), ep.x, ep.y, 13f * 1.24f, 13f * 1.24f, -a);
            bool side = d % 180 == 0;
            var label = V3.Text("DirName", parent, V3.Mono, name, 7f, V3.A(V3.Ink, 0.3f), 0f, 0f, 50f, 10f, side ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, 0.17f);
            UiFactory.Place(label.rectTransform, UiFactory.Center, UiFactory.Center, ep + (side ? new Vector2(13f + 25f, 0f) : new Vector2(0f, -15f)), new Vector2(50f, 10f));
        }
    }

    // Fine lettering engraved on the outer ring's inner face, along the top: each character on the arc (r 188), a dark
    // cut with a bright lower lip.
    private static void BuildEngraving(Transform parent)
    {
        const string text = "VISUAL ACUITY 0.1 – 2.0 · TUMBLING E · Ø 40 CM · EM-31 · KEBBI";
        const float r = R1 - 16f, advance = 6.6f;
        float step = advance / r * Mathf.Rad2Deg;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ')
                continue;
            float deg = 270f + (i - (text.Length - 1) / 2f) * step;
            var p = Polar(r, deg);
            foreach (var (dy, colour) in new (float, Color)[] { (-0.7f, new Color(1f, 1f, 1f, 0.9f)), (0f, new Color32(0x14, 0x18, 0x20, 0x6B)) })
            {
                var ch = V3.Text("Engrave", parent, V3.Mono, text[i].ToString(), 7.4f, colour, 0f, 0f, 12f, 12f, TextAlignmentOptions.Center);
                UiFactory.Place(ch.rectTransform, UiFactory.Center, UiFactory.Center, p + new Vector2(0f, dy), new Vector2(12f, 12f));
                ch.rectTransform.localEulerAngles = new Vector3(0f, 0f, 270f - deg);
            }
        }
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


    // ==================== headline ====================

    private void BuildHeadline()
    {
        _headline = V3.Rect("Headline", _root, 0f, HeadY, V3.W, HeadBoxH);
        _headline.gameObject.AddComponent<Canvas>();
        const string word = "EYE METRIC";
        // Archivo Condensed Black advance widths at 100 pt (the dynamic font has no glyph metrics yet at start-up)
        var advance = new System.Collections.Generic.Dictionary<char, float>
            { { 'E', 55.1f }, { 'Y', 58.3f }, { 'M', 79.6f }, { 'T', 55.9f }, { 'R', 60.1f }, { 'I', 29.9f }, { 'C', 60.4f }, { ' ', 13.4f } };
        var widths = new float[word.Length];
        float total = 0f;
        for (int i = 0; i < word.Length; i++)
        {
            widths[i] = advance[word[i]] - (word[i] == ' ' ? 0f : 5f);
            if (i + 1 < word.Length && (word.Substring(i, 2) == "EY" || word.Substring(i, 2) == "YE"))
                widths[i] -= 4f;
            total += widths[i];
        }
        float k = Mathf.Min(HeadMaxW / total, HeadMaxH / 68.6f);   // Archivo's capitals stand 0.686 em
        float x = (V3.W - total * k) / 2f;
        _headLeft = _focusX = x;
        _lens = new Material(LoadShader("LensText"));
        var cells = new List<(float left, float right)>();
        for (int i = 0; i < word.Length; i++)
        {
            float w = widths[i] * k;
            if (word[i] != ' ')
            {
                _headType.Add((word[i], x - 40f, w + 80f, 100f * k));
                cells.Add((x, x + w));
            }
            x += w;
        }
        _headRight = x;
        // the windows meet halfway between letters; the first and last reach 60 px out for the blur
        for (int i = 0; i < cells.Count; i++)
        {
            float left = i == 0 ? cells[i].left - 60f : (cells[i - 1].right + cells[i].left) / 2f;
            float right = i == cells.Count - 1 ? cells[i].right + 60f : (cells[i].right + cells[i + 1].left) / 2f;
            var img = new GameObject("L", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(_headline, false);
            V3.At(img.rectTransform, left, 0f, right - left, HeadBoxH);
            img.raycastTarget = false;
            img.color = Color.white;
            img.uvRect = new Rect(left / V3.W, 0f, (right - left) / V3.W, 1f);
            img.material = _lens;
            _letters.Add(img);
        }
    }

    // The headline is typeset once off screen (white on black, at screen resolution) into a coverage texture, then
    // blurred on the GPU into three more levels (sigma 1.2, 2.4, 3.6 px); each letter shows its part of them through
    // V3/LensText, blending between two levels by its distance from the focus - a real defocus, the whole glyph spreads.
    private void RenderHeadline()
    {
        float s = Screen.width / V3.W;
        int w = Mathf.CeilToInt(V3.W * s), h = Mathf.CeilToInt(HeadBoxH * s);
        var levels = new RenderTexture[4];
        for (int i = 0; i < levels.Length; i++)
            levels[i] = new RenderTexture(w, h, i == 0 ? 24 : 0, RenderTextureFormat.ARGB32) { name = "V3Headline" + i, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

        // the type on a world-space canvas far from everything, seen by its own orthographic camera
        var source = new GameObject("HeadlineType", typeof(RectTransform), typeof(Canvas));
        source.layer = HeadLayer;
        var rect = (RectTransform)source.transform;
        rect.sizeDelta = new Vector2(V3.W, HeadBoxH);
        rect.position = new Vector3(0f, -10000f, 0f);
        source.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var texts = new List<TextMeshProUGUI>();
        foreach (var (ch, left, width, size) in _headType)
        {
            // raised so the capitals start at the top of the box (ascent 0.878 em, capitals 0.686 em)
            var t = V3.Text("L", rect, V3.Headline, ch.ToString(), size, Color.white, left, -0.17f * size, width, HeadBoxH + 0.17f * size, TextAlignmentOptions.Top);
            t.gameObject.layer = HeadLayer;
            texts.Add(t);
        }
        var cam = new GameObject("HeadlineCamera").AddComponent<Camera>();
        cam.gameObject.layer = HeadLayer;
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = HeadBoxH / 2f;
        cam.aspect = V3.W / HeadBoxH;
        cam.transform.position = rect.position + new Vector3(0f, 0f, -10f);
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 20f;
        cam.cullingMask = 1 << HeadLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.targetTexture = levels[0];
        foreach (var t in texts)
            t.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        cam.Render();
        cam.targetTexture = null;
        Destroy(cam.gameObject);
        Destroy(source);

        // the blur levels: a horizontal then a vertical pass each, from the sharp type
        var blur = new Material(LoadShader("Blur"));
        var temp = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        for (int i = 1; i < levels.Length; i++)
        {
            float sigma = i * LevelSigma * s;
            blur.SetVector("_Step", new Vector4(sigma / w, 0f, 0f, 0f));
            Graphics.Blit(levels[0], temp, blur);
            blur.SetVector("_Step", new Vector4(0f, sigma / h, 0f, 0f));
            Graphics.Blit(temp, levels[i], blur);
        }
        RenderTexture.ReleaseTemporary(temp);
        Destroy(blur);
        // kept as plain textures: a render target's content can be lost while the app is in the background
        _headTex = new Texture2D[levels.Length];
        for (int i = 0; i < levels.Length; i++)
        {
            _headTex[i] = V3Bezel.ReadBack(levels[i]);
            levels[i].Release();
            Destroy(levels[i]);
        }
        foreach (var img in _letters)
            img.texture = _headTex[0];
        _lens.SetTexture("_Tex1", _headTex[1]);
        _lens.SetTexture("_Tex2", _headTex[2]);
        _lens.SetTexture("_Tex3", _headTex[3]);
        _lens.SetVector("_Px", new Vector4(1f / w, 1f / h, 0f, 0f));
        _lens.SetColor("_Face", HeadInk);
        // the frosted E shows the headline behind it from the most blurred level
        foreach (var m in new[] { _frostFace, _frostEdge })
            m.SetTexture("_Head", _headTex[3]);
    }

    // Each letter seen through the lens: soft away from the focus point. The focus racks slowly along the word,
    // or follows the gaze while it is on the headline.
    private void UpdateHeadline(float dt, bool idle)
    {
        float w = _headRight - _headLeft;
        float? focusAt = null;
        if (!idle && _gy > HeadY - 20f && _gy < HeadY + HeadMaxH + 20f && _gx > _headLeft - 30f && _gx < _headRight + 30f)
            focusAt = _gx;
        float target;
        if (focusAt.HasValue)
            target = focusAt.Value;
        else
        {
            target = _focusX + _focusDir * dt * w / 3.2f;
            if (target > _headRight + 0.05f * w) _focusDir = -1f;
            if (target < _headLeft - 0.05f * w) _focusDir = 1f;
        }
        _focusX += (target - _focusX) * (focusAt.HasValue ? 0.18f : 1f);
        // the blur itself is worked out per pixel in V3/LensText
        _lens.SetFloat("_FocusU", _focusX / V3.W);
        _lens.SetFloat("_SpanU", w / V3.W);
    }

    // ==================== mode keys ====================

    private static readonly float[] ModeX = { 40f, 744f };
    private const float ModeY = 460f, ModeW = 240f, ModeH = 74f;

    private void BuildModes()
    {
        BuildMode(0, false, MetricTest.TestMode.Both);
        BuildMode(1, true, MetricTest.TestMode.Single);
    }

    // A backlit window in the light box: a faint contact shadow under it, the eyes (both open / one shut), the label,
    // an arrow; a small lamp on the top edge (green while the E points at it, violet while it is looked at) and a green
    // light guide along the bottom edge that fills while it is looked at. Looking 1.5 s or a touch selects.
    private void BuildMode(int index, bool oneEye, MetricTest.TestMode mode)
    {
        float x = ModeX[index];
        var key = V3.Rect("Key" + index, _modesGroup.transform, x, ModeY, ModeW, ModeH);
        _modeRects[index] = key;
        V3.Shadow(key, 0f, 0f, ModeW, ModeH, 18f, -10f, 10f, new Color(0.12f, 0.16f, 0.23f, 0.18f));
        V3.Shadow(key, 0f, 0f, ModeW, ModeH, 3f, 0f, 2f, new Color(0.12f, 0.16f, 0.23f, 0.14f));
        var button = UiFactory.CreateButton("Mode" + mode, key, KeySprite(), Color.white, () => Choose(index));
        button.transition = Selectable.Transition.None;
        var rect = V3.At((RectTransform)button.transform, 0f, 0f, ModeW, ModeH);
        V3.Sprite("Eyes", rect, EyesSprite(oneEye, false), V3.Ink, 20f, 16f, 42f, 42f);
        V3.Sprite("Pupils", rect, EyesSprite(oneEye, true), V3.Violet, 20f, 16f, 42f, 42f);
        _modeLabels[index] = V3.Text("Label", rect, V3.Zh, "", 26f, V3.Ink, 76f, 0f, 130f, ModeH, TextAlignmentOptions.Left);
        V3.Sprite("Arrow", rect, V3.Icon("arrowR"), V3.Ink, ModeW - 38f, (ModeH - 18f) / 2f, 18f, 18f);
        Pill(V3.Sprite("Lamp", rect, UiFactory.RoundedRect, V3.Green, (ModeW - 18f) / 2f, 10f, 18f, 3f), 1.5f);
        rect.gameObject.AddComponent<V3PressTint>().Target = (Image)button.targetGraphic;
    }

    // The E turns to the chosen mode, rushes toward the viewer until its face fills the screen, then the next page
    // opens from the centre (an iris in the flow page's colour) and the questionnaire takes over.
    private void Choose(int index)
    {
        if (_selecting || _stage != Stage.Idle)
            return;
        _selecting = true;
        _chosen = index;
        TurnTo(index == 0 ? DirLeft : DirRight);
        V3Sounds.Play(V3Sounds.Cue.Select);
        _stage = Stage.Leaving;
        _stageT0 = Time.unscaledTime;
    }

    // ==================== chrome ====================

    private void BuildChrome()
    {
        _appName = V3.Text("AppName", _chromeGroup.transform, V3.Zh, "", 16f, V3.Ink, 40f, 26f, 400f, 36f, TextAlignmentOptions.Left, 0.1f);
        // three quick taps on the app name: the arm motors' direction check (for setting up the gestures)
        float tapAt = -10f;
        int taps = 0;
        var check = UiFactory.CreateButton("JointCheck", _chromeGroup.transform, UiFactory.White, new Color(1f, 1f, 1f, 0f), () =>
        {
            taps = Time.unscaledTime - tapAt < 0.6f ? taps + 1 : 1;
            tapAt = Time.unscaledTime;
            if (taps >= 3)
            {
                taps = 0;
                KebbiMotion.ToggleReadout();
            }
        });
        check.transition = Selectable.Transition.None;
        V3.At((RectTransform)check.transform, 40f, 26f, 180f, 36f);
        // while the check runs: which motor and which way, large, at the bottom centre
        _jointCheck = V3.Text("JointCheckLabel", _root, V3.Zh, "", 30f, V3.Violet, 0f, 440f, V3.W, 140f, TextAlignmentOptions.Top);
        _jointCheck.lineSpacing = -10f;
        // after the name: the eye icon, then Kebbi's violet lamp with its voice wave (shown while Kebbi greets)
        _appIcon = V3.Rect("Tail", _chromeGroup.transform, 0f, 26f, 100f, 36f);
        V3.Picture("EyeIcon", _appIcon, "V3/eye", 0f, 8f, 20f, 20f);
        V3.Sprite("KebbiHalo", _appIcon, UiFactory.Circle, V3.A(V3.Violet, 0.2f), 31f, 11f, 14f, 14f);
        V3.Sprite("Kebbi", _appIcon, UiFactory.Circle, V3.Violet, 33f, 13f, 10f, 10f);
        var wave = V3.Rect("Wave", _appIcon, 52f, 6f, 40f, 24f);
        _talk = wave.gameObject.AddComponent<CanvasGroup>();
        _talk.alpha = 0f;
        var bars = new RectTransform[7];
        for (int i = 0; i < bars.Length; i++)
            bars[i] = V3.Box("Bar", wave, V3.Violet, i * 4.5f, 10f, 2.5f, 4f).rectTransform;
        wave.gameObject.AddComponent<V3Wave>().Bars = bars;
        _appName.gameObject.AddComponent<V3Follow>().Init(_appName, _appIcon, 8f);

        // zh / EN toggle: a white 88 x 30 pill with an ink pill knob sliding between the two cells
        var lang = V3.Rect("Lang", _chromeGroup.transform, 832f, 26f, 104f, 36f);
        Pill(V3.Sprite("Fill", lang, UiFactory.RoundedRect, Color.white, 0f, 0f, 104f, 36f), 18f);
        _knob = Pill(V3.Sprite("Knob", lang, UiFactory.RoundedRect, V3.Ink, 3f, 3f, 49f, 30f), 15f).rectTransform;
        // a touch anywhere on the switch changes to the other language
        var toggle = UiFactory.CreateButton("LangToggle", lang, UiFactory.White, new Color(1f, 1f, 1f, 0f), () =>
        {
            V3Sounds.Play(V3Sounds.Cue.Toggle);
            KebbiMotion.Play("ok");
            V3.SetLang(1 - V3.Lang);
        });
        toggle.transition = Selectable.Transition.None;
        V3.At((RectTransform)toggle.transform, 0f, 0f, 104f, 36f);
        _langZh = LangLabel(lang, 3f, "中文");
        _langEn = LangLabel(lang, 52f, "EN");

        // a short caption at the right, under the RIGHT marker, balancing the headline
        V3.Text("CaptionCode", _chromeGroup.transform, V3.Mono, "VISUAL ACUITY", 10f, V3.A(V3.Ink, 0.55f), 790f, 372f, 200f, 14f, TextAlignmentOptions.TopLeft, 0.08f);
        var caption = V3.Text("Caption", _chromeGroup.transform, V3.Zh, "看 E 的開口方向，\n測驗你的視力", "Which way does\nthe E open?", 17f, V3.A(V3.Ink, 0.8f), 790f, 392f, 194f, 52f, TextAlignmentOptions.TopLeft, 0.04f);
        caption.lineSpacing = -10f;
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 12f;
        caption.fontSizeMax = 17f;

        // the callout's lines (placed each frame by UpdateCallout) and its marker: an ink dot on a white disc
        _calloutLeg = CalloutLine("CalloutLeg");
        _calloutRun = CalloutLine("CalloutRun");
        _calloutMark = UiFactory.CreateRect("CalloutMark", _chromeGroup.transform);
        V3.At(_calloutMark, 0f, 0f, 0f, 0f);
        At(UiFactory.CreateImage("Disc", _calloutMark, UiFactory.Circle, new Color(1f, 1f, 1f, 0.92f)), 0f, 0f, 13f, 13f);
        At(UiFactory.CreateImage("Ring", _calloutMark, ArcSprite(6f, 0.6f, 0f, 360f), V3.A(V3.Ink, 0.35f)), 0f, 0f, 28f, 28f);
        At(UiFactory.CreateImage("Dot", _calloutMark, UiFactory.Circle, V3.A(V3.Ink, 0.8f)), 0f, 0f, 5f, 5f);
        for (int i = 0; i < _brackets.Length; i++)
        {
            _brackets[i] = CalloutLine("Bracket");
            _brackets[i].GetComponent<Image>().color = V3.A(V3.Ink, 0.85f);
        }
    }

    // a hairline from its left end, laid along its length by SetCalloutLine
    private RectTransform CalloutLine(string name)
    {
        var rect = UiFactory.CreateImage(name, _chromeGroup.transform, UiFactory.White, V3.A(V3.Ink, 0.4f)).rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        return rect;
    }

    private static void SetCalloutLine(RectTransform line, Vector2 a, Vector2 b, float width = 1.2f)
    {
        Vector2 d = b - a;
        line.anchoredPosition = new Vector2(a.x, -a.y);
        line.sizeDelta = new Vector2(d.magnitude, width);
        line.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    // The framing marks follow the E's corners. The marker sits on whichever side of the E faces the caption (eased
    // over as the E turns); the leg leaves it at 45 degrees to the caption's code line and the run carries on level to
    // just before the code.
    private void UpdateCallout(float dt)
    {
        if (_calloutMark == null)
            return;
        Vector2 at = default;
        float best = float.MinValue;
        foreach (var side in CalloutSides)
        {
            var v = _cam.WorldToViewportPoint(_eMesh.TransformPoint(side));
            if (v.x > best)
            {
                best = v.x;
                at = new Vector2(v.x * V3.W, (1f - v.y) * V3.H);
            }
        }
        _calloutAt = _calloutSet ? Vector2.Lerp(_calloutAt, at, 1f - Mathf.Exp(-dt / 0.06f)) : at;
        _calloutSet = true;
        var elbow = new Vector2(Mathf.Min(_calloutAt.x + Mathf.Abs(CalloutY - _calloutAt.y), CalloutEnd - 12f), CalloutY);
        _calloutMark.anchoredPosition = new Vector2(_calloutAt.x, -_calloutAt.y);
        SetCalloutLine(_calloutLeg, _calloutAt, elbow);
        SetCalloutLine(_calloutRun, elbow, new Vector2(CalloutEnd, CalloutY));

        // the framing marks: from each corner, a short arm along each of its two sides
        var c = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            var v = _cam.WorldToViewportPoint(_eMesh.TransformPoint(BracketCorners[i]));
            c[i] = new Vector2(v.x * V3.W, (1f - v.y) * V3.H);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector2 next = c[(i + 1) % 4] - c[i], prev = c[(i + 3) % 4] - c[i];
            float arm = Mathf.Min(BracketArm, next.magnitude * 0.3f);
            SetCalloutLine(_brackets[2 * i], c[i] - next.normalized * 0.8f, c[i] + next.normalized * arm, 1.6f);
            SetCalloutLine(_brackets[2 * i + 1], c[i] - prev.normalized * 0.8f, c[i] + prev.normalized * arm, 1.6f);
        }
    }

    // UiFactory.RoundedRect sliced so its corners come out at the given radius
    private static Image Pill(Image image, float radius)
    {
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 24f / radius;
        return image;
    }

    private static TextMeshProUGUI LangLabel(Transform parent, float x, string text)
    {
        var label = V3.Text("Label", parent, V3.Zh, text, 14f, V3.Text2, x, 3f, 49f, 30f, TextAlignmentOptions.Center);
        label.raycastTarget = false;
        return label;
    }

    private void ApplyLanguage()
    {
        _appName.text = V3.T("眼動視力檢測", "Eye-Tracking Vision Test");
        _appName.characterSpacing = V3.Lang == 1 ? 2f : 10f;
        _modeLabels[0].text = V3.T("雙眼檢測", "Both eyes");
        _modeLabels[1].text = V3.T("單眼檢測", "One eye");
        foreach (var label in _modeLabels)
        {
            label.fontSize = V3.Lang == 1 ? 21f : 26f;
            label.characterSpacing = V3.Lang == 1 ? 0f : 4f;
        }
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

    // ==================== frame ====================

    private void SetLevel(int n)
    {
        _level = n;
        _sizeTo = Mathf.Lerp(ESizeMax, ESizeMin, (n - 1) / 10f);   // a little smaller each level, never tiny
        for (int i = 0; i < 11; i++)
        {
            bool on = i == n - 1;
            _levelTicks[i].color = on ? V3.Ink : V3.A(V3.Ink, 0.55f);
            _levelTicks[i].rectTransform.sizeDelta = new Vector2(8f, on ? 2.4f : 1f);
            _levelLabels[i].color = on ? V3.Ink : V3.A(V3.Ink, 0.45f);
            _levelLabels[i].fontSize = on ? 10.5f : 9f;
        }
        ShowGuide(n);
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

        UpdateStage(now, dt, idle);
        _rotT = Mathf.Min(1f, _rotT + dt / 0.6f);
        _sizeNow += (_sizeTo - _sizeNow) * (1f - Mathf.Pow(0.85f, dt * 60f));
        float to = Rot[_dir];
        while (to - _rotFrom > Mathf.PI) to -= 2f * Mathf.PI;
        while (to - _rotFrom < -Mathf.PI) to += 2f * Mathf.PI;
        // the intro grows the E from the dot with a quarter turn; leaving, it rushes toward the viewer
        float spin = (1f - _introE) * -90f;
        // Kebbi's greeting: the E gives two small nods, fading out over 1.2 s
        float nk = (now - _nodT0) / 1.2f, nod = nk >= 0f && nk < 1f ? Mathf.Sin(nk * Mathf.PI * 4f) * 12f * (1f - nk) : 0f;
        _eTransform.localEulerAngles = new Vector3(nod, 0f, (_rotFrom + (to - _rotFrom) * EaseBack(_rotT)) * Mathf.Rad2Deg + spin);
        _eTransform.localScale = Vector3.one * (_sizeNow * UPP * _introE * _eZoom);
        _eTransform.localPosition = Vector3.zero;
        if (_gapSteer > 0f)
            _eTransform.localPosition = -_gapSteer * _holder.InverseTransformVector(_eMesh.TransformPoint(GapLocal) - _eTransform.position);
        foreach (var shadow in _eShadows)
        {
            shadow.localEulerAngles = new Vector3(0f, 0f, _eTransform.localEulerAngles.z);
            shadow.sizeDelta = Vector2.one * (_sizeNow * 1.24f * _introE);
        }

        // the E leans a little toward the gaze; the dial layers shift with it, the front ones most
        float tx = idle ? Mathf.Sin(now / 1.7f) * 0.12f : Mathf.Clamp((_gx - CX) / 900f, -0.3f, 0.3f);
        float ty = idle ? Mathf.Cos(now / 2.1f) * 0.08f : Mathf.Clamp((_gy - CY) / 900f, -0.25f, 0.25f);
        _leanX += (tx - _leanX) * 0.06f;
        _leanY += (ty - _leanY) * 0.06f;
        _holder.localEulerAngles = new Vector3(ViewTiltDeg - _leanY * Mathf.Rad2Deg, -_leanX * Mathf.Rad2Deg, 0f);
        for (int i = 0; i < 3; i++)
            _layers[i].anchoredPosition = new Vector2(CX + _leanX * LayerShift[i], -CY - _leanY * LayerShift[i]);
        _fg.anchoredPosition = new Vector2(_leanX * 34f, -_leanY * 34f);

        _jointCheck.text = KebbiMotion.CheckStep ?? "";
        _talk.alpha = Mathf.MoveTowards(_talk.alpha, now - _nodT0 < 3.2f ? 1f : 0f, dt / 0.3f);

        UpdateHeadline(dt, idle);
        UpdateCallout(dt);
        // knob slides between the language cells
        float knobX = 3f + V3.Lang * 49f;
        _knob.anchoredPosition = new Vector2(Mathf.Lerp(_knob.anchoredPosition.x, knobX, 1f - Mathf.Exp(-dt / 0.06f)), _knob.anchoredPosition.y);
    }

    // ==================== intro, guidance, selection, leaving ====================

    private void StartIntro()
    {
        V3Sounds.Play(V3Sounds.Cue.Intro, 0.8f);
        _stage = Stage.Loading;
        _stageT0 = Time.unscaledTime;
        _selecting = false;
        _chosen = -1;
        _eZoom = 1f;
        _introE = 0f;
        _guided = false;
        _dot.enabled = _pct.enabled = true;
        foreach (var l in _layers)
            l.gameObject.SetActive(true);
        _headline.gameObject.SetActive(true);
        _pct.text = "0%";
        _iris.rectTransform.localScale = Vector3.zero;
        _gapSteer = 0f;
        _sweepDone = false;
        _lockArc.enabled = false;
        _ringRoot.localEulerAngles = Vector3.zero;
        foreach (var l in _layers)
            Fade(l).alpha = 0f;
        Fade(_headline).alpha = 1f;
        _backdrop.alpha = _fgGroup.alpha = _modesGroup.alpha = _chromeGroup.alpha = 0f;
        _modesGroup.blocksRaycasts = false;
        _ringRoot.gameObject.SetActive(false);
        SetShadows(0f);
        foreach (var (img, _) in _degTicks)
            img.enabled = false;
        SetHeadline(0f);
    }

    private static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 4f);

    private void SetShadows(float k)
    {
        foreach (var (g, a) in _shadows)
            g.color = new Color(0.07f, 0.09f, 0.14f, a * k);
        if (_eLight != null)
            _eLight.color = new Color(0.86f, 1f, 0.94f, 0.55f * k);
    }

    // The headline's letters rising in, staggered (k 0-1 over the whole word)
    private void SetHeadline(float k)
    {
        for (int i = 0; i < _letters.Count; i++)
        {
            float t = EaseOut(k * 1.6f - i * 0.07f);
            var img = _letters[i];
            img.color = V3.A(Color.white, t);
            var p = img.rectTransform.anchoredPosition;
            img.rectTransform.anchoredPosition = new Vector2(p.x, -(1f - t) * 50f);
        }
    }

    // turn the E's opening to a direction the short way round
    private void TurnTo(int dir)
    {
        if (dir == _dir)
            return;
        float current = _rotFrom + (Rot[_dir] - _rotFrom) * EaseBack(_rotT);
        _rotFrom = current;
        _dir = dir;
        _rotT = 0f;
    }

    private void UpdateStage(float now, float dt, bool idle)
    {
        float t = now - _stageT0;
        switch (_stage)
        {
            case Stage.Loading:
                // a dot and 0-100 %
                float p = t / 2.2f;
                p = p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;
                _pct.text = Mathf.RoundToInt(Mathf.Clamp01(p) * 100f) + "%";
                if (t >= 2.3f)
                {
                    _stage = Stage.Grow;
                    _stageT0 = now;
                    _pct.enabled = false;
                }
                break;
            case Stage.Grow:
                // the dot becomes the E; the rings turn in; the ticks are drawn round; the headline rises; then the E
                // points at a mode and the modes come up
                _introE = EaseOut(t / 1.1f);
                _dot.enabled = t < 0.08f;
                _backdrop.alpha = Mathf.Clamp01((t - 0.5f) / 1.2f);
                float rings = EaseOut((t - 0.55f) / 1.2f);
                _ringRoot.gameObject.SetActive(t > 0.55f);
                _ringRoot.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, rings);
                _ringRoot.localEulerAngles = new Vector3(0f, 0f, (1f - rings) * 50f);
                SetShadows(Mathf.Clamp01(t / 1.1f) * rings);
                foreach (var (img, deg) in _degTicks)
                    img.enabled = t > 0.55f + deg / 360f * 0.9f;
                foreach (var l in _layers)
                    Fade(l).alpha = Mathf.Clamp01((t - 0.5f) / 0.6f);
                SetHeadline(Mathf.Clamp01((t - 0.9f) / 0.9f));
                _chromeGroup.alpha = _fgGroup.alpha = Mathf.Clamp01((t - 0.3f) / 0.6f);
                if (t > 1.5f && !_guided)
                {
                    _guided = true;
                    TurnTo(DirLeft);
                }
                if (t > 1.75f && _nodT0 < _stageT0)
                    _nodT0 = now;
                float modes = EaseOut((t - 1.75f) / 0.8f);
                _modesGroup.alpha = Mathf.Clamp01((t - 1.75f) / 0.5f);
                for (int i = 0; i < 2; i++)
                    _lift[i] = -(1f - modes) * 40f;
                if (t > 2.6f)
                {
                    SetHeadline(1f);
                    _stage = Stage.Idle;
                    _modesGroup.blocksRaycasts = true;
                    _phase = 0;
                    _phaseT0 = now;
                    _swung = false;
                }
                PlaceModes();
                break;
            case Stage.Idle:
                UpdateGuide(now);
                PlaceModes();
                break;
            case Stage.Leaving:
                UpdateLeaving(now, t);
                _lift[_chosenIndexForPress] = 3f;
                PlaceModes();
                break;
        }
    }

    private int _chosenIndexForPress => _chosen >= 0 ? _chosen : 0;

    // The instrument confirms the choice - the green guide runs up the scale to 2.0, a light runs once round the outer
    // bezel and the rings lock with a small click - then the view pushes through the E's opening into the light box and
    // the flow page takes over.
    private void UpdateLeaving(float now, float t)
    {
        float up = Mathf.Clamp01((t - 0.3f) / 0.35f);
        if (up > 0f && !_sweepDone)
        {
            ShowGuide(_level + (11 - _level) * EaseOut(up));
            _sweepDone = up >= 1f;
        }
        float run = Mathf.Clamp01((t - 0.55f) / 0.4f);
        float sweep = run < 0.5f ? 4f * run * run * run : 1f - Mathf.Pow(-2f * run + 2f, 3f) / 2f;
        _lockArc.enabled = run > 0f && t < 1.1f;
        _lockArc.color = V3.A(V3.Green, 1f - Mathf.Clamp01((t - 0.9f) / 0.2f));
        float length = Mathf.Max(0.001f, sweep * 2f * Mathf.PI);
        PlaceArc(_lockArc, -Mathf.PI / 2f + length / 2f, length / 2f);
        if (run >= 1f && _lockT0 < _stageT0)
            _lockT0 = now;
        float click = (now - _lockT0) / 0.4f;
        float lockRot = click >= 0f && click < 1f ? 3f * Mathf.Sin(click * Mathf.PI) * (1f - click * 0.5f) : 0f;
        _ringRoot.localEulerAngles = new Vector3(0f, 0f, lockRot);

        float k = Mathf.Clamp01((t - 1.05f) / 0.75f);
        _eZoom = 1f + k * k * k * 26f;
        float c = Mathf.Min(1f, k / 0.45f);
        _gapSteer = c < 0.5f ? 4f * c * c * c : 1f - Mathf.Pow(-2f * c + 2f, 3f) / 2f;
        SetShadows(1f - Mathf.Clamp01(k * 3f));
        float fade = 1f - Mathf.Clamp01(k * 2.5f);
        _modesGroup.alpha = _chromeGroup.alpha = _fgGroup.alpha = _backdrop.alpha = fade;
        foreach (var l in _layers)
            Fade(l).alpha = fade;
        Fade(_headline).alpha = fade;
        _ringRoot.gameObject.SetActive(k < 0.3f);
        if (k >= 1f && _chosen >= 0)
        {
            var mode = _modeOf[_chosen];
            _chosen = -1;
            _onSelect?.Invoke(mode);
        }
    }

    private static CanvasGroup Fade(Component c)
    {
        var group = c.GetComponent<CanvasGroup>();
        return group != null ? group : c.gameObject.AddComponent<CanvasGroup>();
    }

    // the green light guide from 0.1 (155 deg) up to a level (fractional while it sweeps)
    private void ShowGuide(float level)
    {
        float from = 155f * Mathf.Deg2Rad, to = (155f - (level - 1f) * 13f) * Mathf.Deg2Rad;
        foreach (var guide in new[] { _progGlow, _progBody, _progCore })
            PlaceArc(guide, (from + to) / 2f, (from - to) / 2f);
        _progTip.anchoredPosition = Polar(R2, to * Mathf.Rad2Deg);
    }

    // The E leads the eye: it points at one mode, then the other, runs two test
    // steps as a demo and points again. The modes are chosen by touch only (no gaze selection on the home).
    private void UpdateGuide(float now)
    {
        if (_phase == 0 && now - _phaseT0 > 1.7f && !_swung)
        {
            _swung = true;
            TurnTo(DirRight);
        }
        else if (_phase == 0 && now - _phaseT0 > 3.4f)
        {
            _phase = 1;
            _phaseT0 = now;
            _demo = 0;
        }
        else if (_phase == 1 && now - _phaseT0 > 1.3f)
        {
            _phaseT0 = now;
            if (_demo++ < 2)
            {
                SetLevel(_level % 11 + 1);
                int nd;
                do { nd = UnityEngine.Random.Range(0, 4); } while (nd == _dir);
                TurnTo(nd);
            }
            else
            {
                _phase = 0;
                _swung = false;
                TurnTo(DirLeft);
            }
        }
    }

    private void PlaceModes()
    {
        for (int i = 0; i < 2; i++)
            _modeRects[i].anchoredPosition = new Vector2(ModeX[i], -(ModeY + _lift[i]));
    }

    // ==================== sprites ====================

    private static Sprite s_tri, s_key;
    private static readonly Sprite[] s_eyes = new Sprite[4];
    private static readonly Dictionary<string, Sprite> s_rings = new Dictionary<string, Sprite>();

    // The mode key: a backlit window (ModeW x ModeH, corner radius 10) - brightest in the middle with a cool falloff to
    // the edge, a fine diffuser dot screen, a thin LED line along the top, a recessed rim (bright lip, cool groove) and
    // a little shade inside the bottom edge.
    private static Sprite KeySprite()
    {
        if (s_key != null)
            return s_key;
        int w = (int)ModeW, h = (int)ModeH;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        Color centre = Color.white, edge = new Color32(0xED, 0xF2, 0xF9, 0xFF), dot = new Color32(0x78, 0x8C, 0xAA, 0xFF);
        Color groove = new Color32(0x96, 0xA5, 0xBE, 0xFF), shade = new Color32(0xB9, 0xC8, 0xDE, 0xFF);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var q = new Vector2(Mathf.Abs(p.x - w / 2f) - (w / 2f - 10f), Mathf.Abs(p.y - h / 2f) - (h / 2f - 10f));
                float d = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 10f;
                float inside = -d;
                var r = new Vector2((p.x - w / 2f) / (w * 0.55f), (p.y - h * 0.42f) / (h * 0.8f));
                Color c = Color.Lerp(centre, edge, Mathf.Clamp01(r.magnitude));
                if (x % 4 < 1 && y % 4 < 1) c = Color.Lerp(c, dot, 0.15f);
                c = Color.Lerp(c, Color.white, 0.95f * Mathf.Clamp01(1f - (h - p.y) / 7f));
                c = Color.Lerp(c, shade, 0.4f * Mathf.Clamp01(1f - p.y / 9f));
                if (inside < 1f) c = Color.white;
                else if (inside < 2.2f) c = Color.Lerp(c, groove, 0.34f);
                c.a = Mathf.Clamp01(0.5f - d);
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return s_key = UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    // Two eyes in a 58 x 32 glyph centred in a square: almond outlines (pupils = false) or just the pupils; for the
    // single-eye mode the right eye is shut (a lower lid line, no pupil).
    private static Sprite EyesSprite(bool oneEye, bool pupils)
    {
        int key = (oneEye ? 2 : 0) + (pupils ? 1 : 0);
        if (s_eyes[key] != null)
            return s_eyes[key];
        const int size = 128;
        const float k = 58f / size;
        return s_eyes[key] = V3.Raster(size, p =>
        {
            float u = p.x * k, v = (size - p.y) * k - 13f;   // glyph units, y down, 0-32 band
            float a = 0f;
            for (int eye = 0; eye < 2; eye++)
            {
                float cx = 15f + eye * 28f, cy = 16f;
                bool shut = oneEye && eye == 1;
                if (pupils)
                {
                    if (!shut) a = Mathf.Max(a, Mathf.Clamp01(0.5f - (Vector2.Distance(new Vector2(u, v), new Vector2(cx, cy)) - 4.4f) / k));
                    continue;
                }
                var e = new Vector2((u - cx) / 12f, (v - cy) / 7f);
                float ring = Mathf.Abs(e.magnitude - 1f) * 7f - 1.2f;
                if (shut && v < cy) ring = 99f;
                a = Mathf.Max(a, Mathf.Clamp01(0.5f - ring / k));
            }
            return new Color(1f, 1f, 1f, a);
        });
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

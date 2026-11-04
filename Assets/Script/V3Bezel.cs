using System.Collections.Generic;
using UnityEngine;

// The machined bezel shared by the home dial and the flow pages: a ring turned on a lathe - short walls, two fine
// chamfers and a flat top face, not a round tube - in V3/Metal, so the face shows the brushed steel and the chamfers
// catch thin bright lines. The flow pages get it rendered once into a texture (Baked).
public static class V3Bezel
{
    private const int BakeLayer = 30;
    private static readonly Dictionary<string, Texture2D> s_baked = new Dictionary<string, Texture2D>();

    // The ring's mesh in the xy plane facing a camera on -z: radius R (the middle of the face), width w, height h
    // toward the viewer. Tangents run round the ring - the brushing direction for V3/Metal.
    public static Mesh Mesh(float R, float w, float h, int segments = 240)
    {
        float half = w / 2f, ch = Mathf.Min(half * 0.32f, h * 0.45f);
        // the profile (radial offset, height) from the inner foot over the face to the outer foot
        var profile = new[]
        {
            new Vector2(-half, 0f), new Vector2(-half, h - ch), new Vector2(-half + ch, h),
            new Vector2(half - ch, h), new Vector2(half, h - ch), new Vector2(half, 0f),
        };
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tangents = new List<Vector4>();
        var tris = new List<int>();
        int rows = (profile.Length - 1) * 2;
        for (int i = 0; i <= segments; i++)
        {
            float a = 2f * Mathf.PI * i / segments;
            var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            var along = new Vector4(-Mathf.Sin(a), Mathf.Cos(a), 0f, 1f);
            for (int p = 0; p < profile.Length - 1; p++)
            {
                Vector2 p0 = profile[p], p1 = profile[p + 1], d = (p1 - p0).normalized;
                var n = radial * -d.y + Vector3.back * d.x;   // each facet flat across, smooth round the ring
                foreach (var q in new[] { p0, p1 })
                {
                    verts.Add(radial * (R + q.x) + Vector3.back * q.y);
                    norms.Add(n.normalized);
                    tangents.Add(along);
                }
            }
        }
        for (int i = 0; i < segments; i++)
            for (int p = 0; p < profile.Length - 1; p++)
            {
                int a0 = i * rows + p * 2, a1 = a0 + 1, b0 = a0 + rows, b1 = b0 + 1;
                // wind each quad so it faces along its normal
                bool flip = Vector3.Dot(Vector3.Cross(verts[b0] - verts[a0], verts[a1] - verts[a0]), norms[a0]) < 0f;
                if (flip) { tris.Add(a0); tris.Add(a1); tris.Add(b0); tris.Add(a1); tris.Add(b1); tris.Add(b0); }
                else { tris.Add(a0); tris.Add(b0); tris.Add(a1); tris.Add(a1); tris.Add(b0); tris.Add(b1); }
            }
        var mesh = new Mesh { name = "Bezel" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTangents(tangents);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // The steel the bezels are made of.
    public static Material Steel(float lathePerUnit)
    {
        var steel = new Material(Resources.Load<Shader>("V3/Shaders/Metal"));
        steel.SetColor("_Tint", V3.Steel);
        steel.SetFloat("_Lathe", lathePerUnit);
        steel.SetFloat("_Marks", 0.5f);
        return steel;
    }

    // A bezel of radius r and width w (reference px) lit as on the home, rendered once at screen resolution and read
    // back into a plain texture (2 (r + w / 2 + 4) px square) whose alpha is the ring's own outline - transparent
    // round it whatever the GPU left in the render target.
    public static Texture Baked(float r, float w)
    {
        string key = r + "/" + w;
        if (s_baked.TryGetValue(key, out var cached) && cached != null)
            return cached;
        float s = Screen.width / V3.W, half = r + w / 2f + 4f;
        int size = Mathf.CeilToInt(2f * half * s);
        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "V3Bezel" + key };

        // a little scene far away: the ring (1 unit = 1 px), the home's key light, an orthographic camera
        var root = new GameObject("BezelBake");
        root.transform.position = new Vector3(0f, 20000f, 0f);
        var ring = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer));
        ring.layer = BakeLayer;
        ring.transform.SetParent(root.transform, false);
        ring.GetComponent<MeshFilter>().sharedMesh = Mesh(r, w, w * 0.45f);
        var renderer = ring.GetComponent<MeshRenderer>();
        var steel = Steel(2.8f);   // turning marks about every 1.6 px, as on the home
        renderer.sharedMaterial = steel;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var light = new GameObject("Key").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.type = LightType.Directional;
        light.color = V3.KeyLight;
        light.intensity = 1f;
        light.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.3f, 0.85f, -0.45f));
        light.cullingMask = 1 << BakeLayer;
        var others = new List<(Light l, int mask)>();
        foreach (var l in Object.FindObjectsOfType<Light>())
            if (l != light && (l.cullingMask & (1 << BakeLayer)) != 0)
            {
                others.Add((l, l.cullingMask));
                l.cullingMask &= ~(1 << BakeLayer);
            }
        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.transform.SetParent(root.transform, false);
        cam.transform.localPosition = new Vector3(0f, 0f, -1000f);
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = half;
        cam.aspect = 1f;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 2000f;
        cam.cullingMask = 1 << BakeLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.allowHDR = false;
        cam.allowMSAA = true;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        foreach (var (l, mask) in others)
            l.cullingMask = mask;
        Object.Destroy(root);
        Object.Destroy(steel);

        var tex = ReadBack(rt);
        rt.Release();
        Object.Destroy(rt);
        var px = tex.GetPixels32();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / s - half, dy = (y + 0.5f) / s - half;
                float cover = Mathf.Clamp01(0.5f - (Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - w / 2f) * s);
                px[y * size + x].a = (byte)(cover * 255f);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s_baked[key] = tex;
        return tex;
    }

    // A render target's picture as a plain texture (resolving its anti-aliasing first).
    public static Texture2D ReadBack(RenderTexture rt)
    {
        var plain = RenderTexture.GetTemporary(rt.width, rt.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(rt, plain);
        var active = RenderTexture.active;
        RenderTexture.active = plain;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
        tex.Apply(false, false);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(plain);
        return tex;
    }
}

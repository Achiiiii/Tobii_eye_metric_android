using System.Collections.Generic;
using UnityEngine;

// The machined bezel of the home dial: a ring turned on a lathe - short walls, two fine chamfers and a flat top face,
// not a round tube - in V3/Metal, so the face shows the brushed steel and the chamfers catch thin bright lines.
public static class V3Bezel
{
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

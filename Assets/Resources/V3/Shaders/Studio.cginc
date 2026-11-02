// V3 home: the room the 3D E and the metal rings sit in, as a function of direction (no cubemap): a darker floor,
// a lighter ceiling, a soft box overhead (a little to the left and toward the viewer) and the light box behind them.
// "blur" widens the soft box and the light box for rougher surfaces.
#ifndef V3_STUDIO_INCLUDED
#define V3_STUDIO_INCLUDED

float3 V3Studio(float3 r, float blur)
{
    float3 c = lerp(float3(0.30, 0.31, 0.33), float3(0.80, 0.82, 0.85), saturate(r.y * 0.5 + 0.5));
    float box = pow(saturate(dot(r, normalize(float3(-0.30, 0.90, -0.32)))), lerp(28.0, 3.0, blur));
    c += lerp(1.25, 0.45, blur) * box;
    c += lerp(0.55, 0.30, blur) * smoothstep(0.15, 0.95, r.z);
    return c;
}

// A photographer's studio for polished metal: a dark room with a white reflector card behind the camera, a long strip
// soft box overhead, a tall strip either side (the left one brighter), a dark floor below a soft horizon, and the
// white light box the instrument stands on behind it. "rough" softens every edge. The contrast between these is what reads as metal.
float V3Box(float2 p, float2 c, float2 half, float soft)
{
    float2 d = abs(p - c) - half;
    return 1.0 - smoothstep(-soft, soft, max(d.x, d.y));
}

float3 V3StudioSharp(float3 r, float rough)
{
    float soft = lerp(0.02, 0.35, rough);
    // the room: floor dark, walls mid, ceiling a little lighter; darker toward the camera side
    float3 c = lerp(float3(0.10, 0.11, 0.12), float3(0.42, 0.44, 0.47), smoothstep(-0.35, 0.15, r.y));
    c = lerp(c, float3(0.58, 0.60, 0.63), smoothstep(0.1, 0.9, r.y));
    c *= lerp(0.55, 1.0, smoothstep(-0.6, 0.4, r.z));
    // a soft bright horizon line
    c += 0.25 * exp(-r.y * r.y / max(0.002, 0.004 + soft * 0.05));
    // the strip soft box overhead (seen on the plane y = 1)
    if (r.y > 0.05)
        c += 2.4 * V3Box(r.xz / r.y, float2(-0.2, -0.35), float2(1.1, 0.22), soft);
    // tall strips at the sides (planes x = -1 and x = 1)
    if (r.x < -0.05)
        c += 1.5 * V3Box(r.yz / -r.x, float2(0.15, -0.25), float2(0.9, 0.18), soft);
    if (r.x > 0.05)
        c += 0.8 * V3Box(r.yz / r.x, float2(0.15, -0.25), float2(0.9, 0.18), soft);
    // a large white reflector card behind the camera (seen on the plane z = -1): faces turned to the viewer read silver
    if (r.z < -0.2)
        c += 0.75 * V3Box(r.xy / -r.z, float2(0.0, 0.2), float2(0.95, 0.7), soft + 0.15);
    // the light box behind
    c = lerp(c, float3(0.95, 0.96, 0.98), smoothstep(0.55, 0.95, r.z));
    return c;
}

// value noise for the surfaces' fine detail (turning marks on the rings, the E's bead-blasted grain)
float V3Hash(float n) { return frac(sin(n) * 43758.5453); }
float V3Noise1(float x)
{
    float i = floor(x), f = frac(x);
    return lerp(V3Hash(i), V3Hash(i + 1.0), f * f * (3.0 - 2.0 * f));
}
float V3Noise3(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n = i.x + i.y * 57.0 + i.z * 113.0;
    return lerp(lerp(lerp(V3Hash(n), V3Hash(n + 1.0), f.x), lerp(V3Hash(n + 57.0), V3Hash(n + 58.0), f.x), f.y),
                lerp(lerp(V3Hash(n + 113.0), V3Hash(n + 114.0), f.x), lerp(V3Hash(n + 170.0), V3Hash(n + 171.0), f.x), f.y), f.z);
}

#endif

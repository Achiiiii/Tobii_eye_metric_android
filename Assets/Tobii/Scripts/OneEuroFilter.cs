using Unity.Mathematics;

sealed class OneEuroFilter
{
    public float Beta { get; set; }
    public float MinCutoff { get; set; }
    public float DCutoff { get; set; } 

    public float2 Step(float t, float2 x)
    {
        var t_e = t - _prev2D.t;

        // Do nothing if the time difference is too small.
        if (t_e < 1e-5f) return _prev2D.x;

        var dx = (x - _prev2D.x) / t_e;
        var dx_res = math.lerp(_prev2D.dx, dx, Alpha(t_e, DCutoff));

        var cutoff = MinCutoff + Beta * math.length(dx_res);
        var x_res = math.lerp(_prev2D.x, x, Alpha(t_e, cutoff));

        _prev2D = (t, x_res, dx_res);

        return x_res;
    }
    public float3 Step(float t, float3 x)
    {
        var t_e = t - _prev3D.t;

        // Do nothing if the time difference is too small.
        if (t_e < 1e-5f) return _prev3D.x;

        var dx = (x - _prev3D.x) / t_e;
        var dx_res = math.lerp(_prev3D.dx, dx, Alpha(t_e, DCutoff));

        var cutoff = MinCutoff + Beta * math.length(dx_res);
        var x_res = math.lerp(_prev3D.x, x, Alpha(t_e, cutoff));

        _prev3D = (t, x_res, dx_res);

        return x_res;
    }

    static float Alpha(float t_e, float cutoff)
    {
        var r = 2 * math.PI * cutoff * t_e;
        return r / (r + 1);
    }

    (float t, float2 x, float2 dx) _prev2D;
    (float t, float3 x, float3 dx) _prev3D;
}
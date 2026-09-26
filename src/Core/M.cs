namespace Mikro;

/// <summary>Matematyka i losowość (szybki xorshift, rozkład normalny, szum 1D).</summary>
public static class M
{
    public const float TAU = MathF.PI * 2f;
    public const float PI = MathF.PI;

    static ulong _s = (ulong)DateTime.Now.Ticks | 1UL;

    public static float R01()
    {
        _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17;
        return (float)((_s >> 40) * (1.0 / 16777216.0));
    }
    public static float Rand(float a = 0f, float b = 1f) => a + R01() * (b - a);
    public static int RandI(int a, int b) { int v = a + (int)(R01() * (b - a + 1)); return v > b ? b : v; }
    public static bool Chance(float p) => R01() < p;
    public static float Sign() => R01() < 0.5f ? -1f : 1f;

    static bool _hasSpare; static float _spare;
    public static float RandN()
    {
        if (_hasSpare) { _hasSpare = false; return _spare; }
        float u, v, q;
        do { u = R01() * 2f - 1f; v = R01() * 2f - 1f; q = u * u + v * v; } while (q >= 1f || q == 0f);
        float m = MathF.Sqrt(-2f * MathF.Log(q) / q);
        _spare = v * m; _hasSpare = true;
        return u * m;
    }

    public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float WrapA(float a) { a = (a + PI) % TAU; if (a < 0) a += TAU; return a - PI; }
    public static float Turn(float a, float t, float step) => a + Clamp(WrapA(t - a), -step, step);
    public static float Damp(float rate, float dt) => 1f - MathF.Exp(-rate * dt);
    public static float Smooth(float t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
    public static float ExpRand(float mean) => -MathF.Log(1f - R01() * 0.999999f) * mean;
    public static float Gauss(float x, float w) => MathF.Exp(-(x * x) / (w * w));
    public static float Hypot(float x, float y) => MathF.Sqrt(x * x + y * y);
    public static float Sq(float x) => x * x;

    static float Hash(double i)
    {
        double s = Math.Sin(i * 127.1 + 311.7) * 43758.5453;
        return (float)((s - Math.Floor(s)) * 2.0 - 1.0);
    }
    /// <summary>Gładki szum wartości 1D w zakresie ~[-1,1].</summary>
    public static float Noise1(float x)
    {
        double i = Math.Floor(x); float f = x - (float)i, u = f * f * (3 - 2 * f);
        float a = Hash(i), b = Hash(i + 1);
        return a + (b - a) * u;
    }
    public static T Pick<T>(T[] a) => a[RandI(0, a.Length - 1)];

    /// <summary>Deterministyczny hash liczb całkowitych → [0,1).</summary>
    public static float Hash2(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
    /// <summary>Gładki szum wartości 2D w [0,1] (biomy, rozkłady).</summary>
    public static float Noise2(float x, float y, int seed)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = x - ix, fy = y - iy, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        float a = Hash2(ix, iy, seed), b = Hash2(ix + 1, iy, seed), c = Hash2(ix, iy + 1, seed), d = Hash2(ix + 1, iy + 1, seed);
        return Lerp(Lerp(a, b, u), Lerp(c, d, u), v);
    }
    public static int FloorDiv(float v, float s) => (int)MathF.Floor(v / s);
}

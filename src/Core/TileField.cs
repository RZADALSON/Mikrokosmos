namespace Mikro;

/// <summary>
/// Nieskończone pole stężenia (glukoza, antybiotyk) przechowywane w kaflach = chunkach świata.
/// Kafel ma N×N komórek; dyfuzja liczona tylko dla aktywnych chunków, z wymianą przez granice kafli.
/// </summary>
public sealed class TileField
{
    public readonly float TS, H;
    public readonly int N;
    readonly Dictionary<long, float[]> _tiles = new();
    readonly Dictionary<long, float[]> _tmp = new();
    float _acc;

    public TileField(float tileSize, int n) { TS = tileSize; N = n; H = tileSize / n; }

    public static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    static int Fd(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    public float[] Tile(int tx, int ty) => _tiles.TryGetValue(Key(tx, ty), out var t) ? t : null;
    public float[] Ensure(int tx, int ty)
    {
        long k = Key(tx, ty);
        if (!_tiles.TryGetValue(k, out var t)) { t = new float[N * N]; _tiles[k] = t; }
        return t;
    }
    public float Cell(int ix, int iy)
    {
        int tx = Fd(ix, N), ty = Fd(iy, N);
        var t = Tile(tx, ty);
        return t == null ? 0 : t[(iy - ty * N) * N + (ix - tx * N)];
    }
    public float Sample(float x, float y)
    {
        float fx = x / H - 0.5f, fy = y / H - 0.5f;
        int i = (int)MathF.Floor(fx), j = (int)MathF.Floor(fy);
        float u = fx - i, v = fy - j;
        return (Cell(i, j) * (1 - u) + Cell(i + 1, j) * u) * (1 - v) + (Cell(i, j + 1) * (1 - u) + Cell(i + 1, j + 1) * u) * v;
    }
    public float Consume(float x, float y, float amt)
    {
        int ix = (int)MathF.Floor(x / H), iy = (int)MathF.Floor(y / H);
        int tx = Fd(ix, N), ty = Fd(iy, N);
        var t = Tile(tx, ty); if (t == null) return 0;
        int q = (iy - ty * N) * N + (ix - tx * N);
        float got = Math.Min(t[q], amt); t[q] -= got; return got;
    }
    public void Blob(float x, float y, float r, float amt)
    {
        int i0 = (int)MathF.Floor((x - r * 2) / H), i1 = (int)MathF.Floor((x + r * 2) / H);
        int j0 = (int)MathF.Floor((y - r * 2) / H), j1 = (int)MathF.Floor((y + r * 2) / H);
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float dx = (i + 0.5f) * H - x, dy = (j + 0.5f) * H - y;
                float f = MathF.Exp(-(dx * dx + dy * dy) / (r * r));
                if (f < 0.01f) continue;
                int tx = Fd(i, N), ty = Fd(j, N);
                var t = Tile(tx, ty); if (t == null) continue;
                int q = (j - ty * N) * N + (i - tx * N);
                t[q] = Math.Min(1.5f, t[q] + amt * f);
            }
    }
    public void Diffuse(float dt, float D, List<Chunk> active)
    {
        _acc += dt;
        if (_acc < 0.05f) return;
        float k = Clamp(D * _acc / (H * H), 0, 0.2f);
        _acc = 0;
        foreach (var c in active)
        {
            var t = Tile(c.X, c.Y); if (t == null) continue;
            long key = Key(c.X, c.Y);
            if (!_tmp.TryGetValue(key, out var o)) { o = new float[N * N]; _tmp[key] = o; }
            int bx = c.X * N, by = c.Y * N;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int q = j * N + i; float v = t[q];
                    float l = i > 0 ? t[q - 1] : Cell(bx + i - 1, by + j);
                    float r = i < N - 1 ? t[q + 1] : Cell(bx + i + 1, by + j);
                    float u = j > 0 ? t[q - N] : Cell(bx + i, by + j - 1);
                    float d = j < N - 1 ? t[q + N] : Cell(bx + i, by + j + 1);
                    o[q] = v + k * (l + r + u + d - 4 * v);
                }
        }
        foreach (var c in active)
        {
            long key = Key(c.X, c.Y);
            var t = Tile(c.X, c.Y);
            if (t != null && _tmp.TryGetValue(key, out var o)) Array.Copy(o, t, t.Length);
        }
    }
    public void Draw(Painter p, View v, Color col, float alpha)
    {
        int tx0 = (int)MathF.Floor(v.X0 / TS), tx1 = (int)MathF.Floor(v.X1 / TS), ty0 = (int)MathF.Floor(v.Y0 / TS), ty1 = (int)MathF.Floor(v.Y1 / TS);
        for (int ty = ty0; ty <= ty1; ty++)
            for (int tx = tx0; tx <= tx1; tx++)
            {
                if (Tile(tx, ty) == null) continue;
                int bx = tx * N, by = ty * N;
                p.Grid(bx * H + H * 0.5f, by * H + H * 0.5f, H, H, N + 1, N + 1, (i, j) => WithA(col, Clamp(Cell(bx + i, by + j), 0, 1.2f) / 1.2f * alpha));
            }
    }
    /// <summary>Zgrubne rysowanie na minimapie (co 4 komórki).</summary>
    public void DrawMap(Painter p, float x0, float y0, float x1, float y1, Color col, float alpha)
    {
        int tx0 = (int)MathF.Floor(x0 / TS), tx1 = (int)MathF.Floor(x1 / TS), ty0 = (int)MathF.Floor(y0 / TS), ty1 = (int)MathF.Floor(y1 / TS);
        const int S = 4; int n = N / S;
        for (int ty = ty0; ty <= ty1; ty++)
            for (int tx = tx0; tx <= tx1; tx++)
            {
                if (Tile(tx, ty) == null) continue;
                int bx = tx * N, by = ty * N;
                p.Grid(bx * H + H * S * 0.5f, by * H + H * S * 0.5f, H * S, H * S, n + 1, n + 1, (i, j) => WithA(col, Clamp(Cell(bx + i * S, by + j * S), 0, 1.2f) / 1.2f * alpha));
            }
    }
}

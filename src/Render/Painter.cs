using System.Runtime.CompilerServices;

namespace Mikro;

/// <summary>
/// Wsadowy renderer wektorowy. Wszystko (wypełnienia, obrysy, kropki, gradienty)
/// trafia do dużych tablic trójkątów wysyłanych kilkoma wywołaniami
/// RenderingServer.CanvasItemAddTriangleArray. Krawędzie mają 1-pikselową
/// "frędzlę" z alfą → gładki antyaliasing bez MSAA.
/// Współrzędne: mikrometry świata (przekształcane przez bieżącą macierz lokalną).
/// </summary>
public sealed class Painter
{
    /// <summary>Maks. liczba wierzchołków w jednej paczce (jedno wywołanie CanvasItemAddTriangleArray).</summary>
    readonly int LIMIT;

    sealed class Chunk
    {
        public Vector2[] P, U;
        public Color[] C;
        public int[] I;
        public int Nv, Ni;
        public Chunk(int limit) { P = new Vector2[limit + 16]; C = new Color[limit + 16]; I = new int[limit * 5]; }
    }

    readonly List<Chunk> _chunks = new();
    int _cur;
    Chunk _ch;
    public readonly bool Textured;

    /// <summary>Rozmiar piksela ekranu w µm (świat).</summary>
    public float Px = 1f;
    /// <summary>Globalny mnożnik przezroczystości.</summary>
    public float Alpha = 1f;
    /// <summary>Punkt odniesienia wierzchołków (odejmowany) – utrzymuje małe liczby w buforze GPU.</summary>
    public float OX, OY;
    /// <summary>Towarzyszący malarz z teksturą <see cref="SpriteAtlas"/> (null = rysuj wektorowo).</summary>
    public Painter Tex;

    // macierz: x' = a*x + c*y + e ; y' = b*x + d*y + f
    float _a = 1, _b, _c, _d = 1, _e, _f, _sc = 1;
    readonly float[] _stack = new float[7 * 16];
    int _sp;

    public Painter(bool textured = false, int limit = 60000) { Textured = textured; LIMIT = limit; }

    // bufory robocze dla prymitywów
    public readonly Vector2[] Buf = new Vector2[8192];
    public readonly Vector2[] Buf2 = new Vector2[8192];
    /// <summary>Bufor na obrys membrany (nie używany przez prymitywy).</summary>
    public readonly Vector2[] Shape = new Vector2[8192];
    public readonly Vector2[] Shape2 = new Vector2[8192];
    public readonly float[] Tx = new float[4096], Ty = new float[4096];
    readonly Vector2[] _nrm = new Vector2[8192];
    readonly float[] _mit = new float[8192];
    readonly Vector2[] _cap = new Vector2[16];

    Chunk NewChunk() { var c = new Chunk(LIMIT); if (Textured) c.U = new Vector2[LIMIT + 16]; return c; }

    // ---------------------------------------------------------------- geometria zapamiętana (nieruchome encje)
    /// <summary>
    /// Trójkąty nieruchomej encji (np. włókna kolagenu) w układzie bufora (po odjęciu OX/OY). Ważne, dopóki nie zmienią się:
    /// powiększenie, punkt odniesienia, globalna alfa ani klucz stanu podany przez encję – wtedy odtwarzane zamiast liczone.
    /// </summary>
    public sealed class Cached
    {
        public Vector2[] P = Array.Empty<Vector2>(); public Color[] C = Array.Empty<Color>(); public int[] I = Array.Empty<int>();
        public int Nv, Ni, Used; public float Px, OX, OY, Alpha, Key = float.NaN;
    }
    // pamięć wspólna dla wszystkich malarzy (geometria budowana równolegle – encja trafia co klatkę do innego wątku);
    // każda encja jest rysowana przez jeden wątek naraz, więc blokada chroni tylko sam słownik
    static readonly Dictionary<int, Cached> _cache = new();
    static readonly List<int> _evict = new();
    static int _frame;
    Chunk _capCh; int _capV, _capI;
    /// <summary>Pamięć geometrii encji o danym Id (tylko encje rysowane w ostatnich ~2 s – pula nie rośnie bez końca).</summary>
    public Cached CacheFor(int id)
    {
        Cached g;
        lock (_cache) { if (!_cache.TryGetValue(id, out g)) { g = new Cached(); _cache[id] = g; } }
        g.Used = _frame; return g;
    }
    /// <summary>Nowa klatka pamięci geometrii: co ~1 s usuwa wpisy nieużywane od ~2 s (encje poza kadrem). Wątek główny, przed rysowaniem.</summary>
    public static void CacheTick()
    {
        if ((++_frame & 63) != 0 || _cache.Count == 0) return;
        _evict.Clear();
        foreach (var kv in _cache) if (_frame - kv.Value.Used > 120) _evict.Add(kv.Key);
        foreach (int k in _evict) _cache.Remove(k);
    }
    /// <summary>Odtwarza zapamiętane trójkąty, jeśli wciąż pasują do bieżącego widoku i stanu encji.</summary>
    public bool TryReplay(Cached g, float key)
    {
        if (g.Nv == 0 || g.Key != key || g.Px != Px || g.OX != OX || g.OY != OY || g.Alpha != Alpha) return false;
        Reserve(g.Nv, g.Ni);
        int b = _ch.Nv, n = _ch.Ni; var I = _ch.I;
        Array.Copy(g.P, 0, _ch.P, b, g.Nv); Array.Copy(g.C, 0, _ch.C, b, g.Nv);
        for (int i = 0; i < g.Ni; i++) I[n + i] = g.I[i] + b;
        _ch.Nv += g.Nv; _ch.Ni += g.Ni;
        return true;
    }
    public void BeginCapture() { _capCh = _ch; _capV = _ch.Nv; _capI = _ch.Ni; }
    public void EndCapture(Cached g, float key)
    {
        g.Nv = 0;
        // rysunek przeszedł do nowej paczki (albo malarz z teksturą) – bez zapamiętywania, spróbujemy w następnej klatce
        if (_capCh != _ch || Textured) return;
        int nv = _ch.Nv - _capV, ni = _ch.Ni - _capI;
        if (g.P.Length < nv) { g.P = new Vector2[nv]; g.C = new Color[nv]; }
        if (g.I.Length < ni) g.I = new int[ni];
        Array.Copy(_ch.P, _capV, g.P, 0, nv); Array.Copy(_ch.C, _capV, g.C, 0, nv);
        for (int i = 0; i < ni; i++) g.I[i] = _ch.I[_capI + i] - _capV;
        g.Nv = nv; g.Ni = ni; g.Px = Px; g.OX = OX; g.OY = OY; g.Alpha = Alpha; g.Key = key;
    }

    public void Begin(float px)
    {
        _cur = 0;
        if (_chunks.Count == 0) _chunks.Add(NewChunk());
        _ch = _chunks[0]; _ch.Nv = _ch.Ni = 0; _done = 0;
        Px = px; Alpha = 1f; _sp = 0;
        Identity();
    }

    public int VertexCount { get { int n = 0; for (int i = 0; i <= _cur; i++) n += _chunks[i].Nv; return n; } }
    /// <summary>Liczba wierzchołków bieżącej paczki + poprzednich (tanio – do szacowania kosztu rysowania encji).</summary>
    public int Mark => _done + _ch.Nv;
    int _done;

    public void Flush(Rid item, Rid tex = default)
    {
        for (int k = 0; k <= _cur && k < _chunks.Count; k++)
        {
            var c = _chunks[k];
            if (c.Ni == 0) continue;
            RenderingServer.CanvasItemAddTriangleArray(item,
                new ReadOnlySpan<int>(c.I, 0, c.Ni),
                new ReadOnlySpan<Vector2>(c.P, 0, c.Nv),
                new ReadOnlySpan<Color>(c.C, 0, c.Nv),
                Textured ? new ReadOnlySpan<Vector2>(c.U, 0, c.Nv) : default,
                default, default, tex, -1);
        }
    }

    void Reserve(int nv, int ni)
    {
        if (_ch.Nv + nv <= LIMIT && _ch.Ni + ni <= _ch.I.Length) return;
        _done += _ch.Nv;
        _cur++;
        if (_cur >= _chunks.Count) _chunks.Add(NewChunk());
        _ch = _chunks[_cur]; _ch.Nv = _ch.Ni = 0;
    }

    // ---------------------------------------------------------------- transformacje
    public void Identity() { _a = 1; _b = 0; _c = 0; _d = 1; _e = 0; _f = 0; _sc = 1; _warp = false; }

    // odkształcenie ciała: wnętrze wygina się i obraca razem z obrysem membrany (y += β·x², potem obrót o kąt opóźnienia)
    bool _warp; float _wx, _wy, _wc, _ws, _wb, _wrc = 1, _wrs;
    /// <summary>Dalsze rysowanie (do Identity/NoWarp) podąża za wygięciem i opóźnieniem obrotu membrany.</summary>
    public void Warp(Membrane m)
    {
        if (m == null || (MathF.Abs(m.BendEff) < 1e-7f && MathF.Abs(m.LagA) < 1e-4f)) { _warp = false; return; }
        _warp = true; _wx = m.Cx; _wy = m.Cy; _wc = MathF.Cos(m.Ang); _ws = MathF.Sin(m.Ang); _wb = m.BendEff;
        _wrc = MathF.Cos(m.LagA); _wrs = MathF.Sin(m.LagA);
    }
    public void NoWarp() => _warp = false;
    /// <summary>Wygięcie osi ciała bez membrany (np. niesporczak): y += bend·x² w układzie ciała (cx, cy, ang), potem obrót o lag.</summary>
    public void Warp(float cx, float cy, float ang, float bend, float lag = 0)
    {
        if (MathF.Abs(bend) < 1e-7f && MathF.Abs(lag) < 1e-4f) { _warp = false; return; }
        _warp = true; _wx = cx; _wy = cy; _wc = MathF.Cos(ang); _ws = MathF.Sin(ang); _wb = bend; _wrc = MathF.Cos(lag); _wrs = MathF.Sin(lag);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void WarpPt(ref float X, ref float Y)
    {
        float dx = X - _wx, dy = Y - _wy, bx = dx * _wc + dy * _ws, by = -dx * _ws + dy * _wc;
        by += _wb * bx * bx;
        float rx = bx * _wrc - by * _wrs, ry = bx * _wrs + by * _wrc;
        X = _wx + rx * _wc - ry * _ws; Y = _wy + rx * _ws + ry * _wc;
    }
    public void Local(float x, float y, float ang, float sc = 1f)
    {
        float c = MathF.Cos(ang) * sc, s = MathF.Sin(ang) * sc;
        _a = c; _b = s; _c = -s; _d = c; _e = x; _f = y; _sc = sc;
    }
    public void Translate(float x, float y) { _e += _a * x + _c * y; _f += _b * x + _d * y; }
    public void Rotate(float ang)
    {
        float c = MathF.Cos(ang), s = MathF.Sin(ang);
        float na = _a * c + _c * s, nb = _b * c + _d * s, nc = -_a * s + _c * c, nd = -_b * s + _d * c;
        _a = na; _b = nb; _c = nc; _d = nd;
    }
    public void Scale(float k) { _a *= k; _b *= k; _c *= k; _d *= k; _sc *= k; }
    public void Push()
    {
        int o = _sp * 7; _stack[o] = _a; _stack[o + 1] = _b; _stack[o + 2] = _c; _stack[o + 3] = _d; _stack[o + 4] = _e; _stack[o + 5] = _f; _stack[o + 6] = _sc; _sp++;
    }
    public void Pop()
    {
        _sp--; int o = _sp * 7; _a = _stack[o]; _b = _stack[o + 1]; _c = _stack[o + 2]; _d = _stack[o + 3]; _e = _stack[o + 4]; _f = _stack[o + 5]; _sc = _stack[o + 6];
    }
    /// <summary>Rozmiar piksela w bieżących jednostkach lokalnych.</summary>
    public float PxL => Px / _sc;

    // ---------------------------------------------------------------- niskopoziomowe
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int Vx(float x, float y, Color col)
    {
        int i = _ch.Nv++;
        float X = _a * x + _c * y + _e, Y = _b * x + _d * y + _f;
        if (_warp) WarpPt(ref X, ref Y);
        _ch.P[i] = new Vector2(X - OX, Y - OY);
        col.A *= Alpha;
        _ch.C[i] = col;
        if (Textured) _ch.U[i] = SpriteAtlas.WhiteUv; // zwykła geometria na białym polu atlasu
        return i;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int VxUV(float x, float y, Color col, float u, float v)
    {
        int i = Vx(x, y, col);
        _ch.U[i] = new Vector2(u, v);
        return i;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Tri(int a, int b, int c) { var I = _ch.I; int n = _ch.Ni; I[n] = a; I[n + 1] = b; I[n + 2] = c; _ch.Ni = n + 3; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

    static Color Clear(Color c) { c.A = 0; return c; }

    // ---------------------------------------------------------------- krzywe pomocnicze
    /// <summary>Gładka zamknięta krzywa (kwadratowe Béziery przez punkty środkowe) → out.</summary>
    public static int SmoothClosed(float[] px, float[] py, int n, Vector2[] outp, int sub = 3, float ox = 0, float oy = 0)
    {
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            int h = i == 0 ? n - 1 : i - 1, j = i == n - 1 ? 0 : i + 1;
            float x0 = (px[h] + px[i]) * 0.5f - ox, y0 = (py[h] + py[i]) * 0.5f - oy;
            float cx = px[i] - ox, cy = py[i] - oy;
            float x1 = (px[i] + px[j]) * 0.5f - ox, y1 = (py[i] + py[j]) * 0.5f - oy;
            for (int s = 0; s < sub; s++)
            {
                float t = (float)s / sub, u = 1 - t;
                outp[k++] = new Vector2(u * u * x0 + 2 * u * t * cx + t * t * x1, u * u * y0 + 2 * u * t * cy + t * t * y1);
            }
        }
        return k;
    }
    public static int SmoothOpen(float[] px, float[] py, int n, Vector2[] outp, int sub = 3)
    {
        int k = 0;
        outp[k++] = new Vector2(px[0], py[0]);
        if (n == 2) { outp[k++] = new Vector2(px[1], py[1]); return k; }
        float sx = px[0], sy = py[0];
        for (int i = 1; i < n - 1; i++)
        {
            float ex = i == n - 2 ? px[n - 1] : (px[i] + px[i + 1]) * 0.5f, ey = i == n - 2 ? py[n - 1] : (py[i] + py[i + 1]) * 0.5f;
            for (int s = 1; s <= sub; s++)
            {
                float t = (float)s / sub, u = 1 - t;
                outp[k++] = new Vector2(u * u * sx + 2 * u * t * px[i] + t * t * ex, u * u * sy + 2 * u * t * py[i] + t * t * ey);
            }
            sx = ex; sy = ey;
        }
        return k;
    }
    public int CirclePts(float x, float y, float r, Vector2[] outp, int seg = 0)
    {
        if (seg <= 0) seg = (int)Clamp(MathF.Sqrt(Math.Max(r, 1e-6f) / PxL) * 4.2f, 8, 96);
        for (int i = 0; i < seg; i++) { float t = i * TAU / seg; outp[i] = new Vector2(x + MathF.Cos(t) * r, y + MathF.Sin(t) * r); }
        return seg;
    }
    public int EllipsePts(float x, float y, float rx, float ry, float rot, Vector2[] outp, float a0 = 0, float a1 = TAU, bool full = true)
    {
        int seg = (int)Clamp(MathF.Sqrt(Math.Max(Math.Max(rx, ry), 1e-6f) / PxL) * 4.2f, 8, 96);
        float c = MathF.Cos(rot), s = MathF.Sin(rot);
        int n = full ? seg : seg + 1;
        for (int i = 0; i < n; i++)
        {
            float t = a0 + (a1 - a0) * i / seg;
            float ex = MathF.Cos(t) * rx, ey = MathF.Sin(t) * ry;
            outp[i] = new Vector2(x + ex * c - ey * s, y + ex * s + ey * c);
        }
        return n;
    }

    // ---------------------------------------------------------------- wypełnienia
    /// <summary>Wypełnienie kształtu gwiaździstego względem (cx,cy) z antyaliasingiem.</summary>
    public void FillFan(ReadOnlySpan<Vector2> pts, float cx, float cy, Color col, bool aa = true)
    {
        int n = pts.Length; if (n < 3) return;
        Reserve(n * 2 + 2, n * 9);
        int c0 = Vx(cx, cy, col);
        int b = _ch.Nv;
        for (int i = 0; i < n; i++) Vx(pts[i].X, pts[i].Y, col);
        for (int i = 0; i < n; i++) Tri(c0, b + i, b + (i + 1) % n);
        if (aa) Fringe(pts, cx, cy, col, b);
    }
    void Fringe(ReadOnlySpan<Vector2> pts, float cx, float cy, Color col, int b)
    {
        int n = pts.Length; float f = PxL; var cl = Clear(col);
        int o = _ch.Nv;
        for (int i = 0; i < n; i++)
        {
            var p0 = pts[i == 0 ? n - 1 : i - 1]; var p1 = pts[i == n - 1 ? 0 : i + 1];
            float tx = p1.X - p0.X, ty = p1.Y - p0.Y, l = MathF.Sqrt(tx * tx + ty * ty);
            float nx = l > 0 ? ty / l : 0, ny = l > 0 ? -tx / l : 0;
            if (nx * (pts[i].X - cx) + ny * (pts[i].Y - cy) < 0) { nx = -nx; ny = -ny; }
            Vx(pts[i].X + nx * f, pts[i].Y + ny * f, cl);
        }
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; Quad(b + i, o + i, o + j, b + j); }
    }
    /// <summary>Wypełnienie z gradientem radialnym (przystanki t∈[0,1] od środka do brzegu).</summary>
    public void FillRadial(ReadOnlySpan<Vector2> pts, float cx, float cy, ReadOnlySpan<float> ts, ReadOnlySpan<Color> cols, bool aa = true)
    {
        int n = pts.Length, k = ts.Length; if (n < 3 || k < 1) return;
        Reserve(n * (k + 1) + 2, n * (6 * k + 6));
        int prev = -1;
        bool centerSingle = ts[0] <= 0.0001f;
        int start = 0;
        if (centerSingle) { prev = Vx(cx, cy, cols[0]); start = 1; }
        int prevRow = -1;
        for (int s = start; s < k; s++)
        {
            float t = ts[s]; int row = _ch.Nv;
            for (int i = 0; i < n; i++) Vx(cx + (pts[i].X - cx) * t, cy + (pts[i].Y - cy) * t, cols[s]);
            if (s == start && centerSingle) { for (int i = 0; i < n; i++) Tri(prev, row + i, row + (i + 1) % n); }
            else if (s == start) { int c0 = Vx(cx, cy, cols[s]); for (int i = 0; i < n; i++) Tri(c0, row + i, row + (i + 1) % n); }
            else for (int i = 0; i < n; i++) { int j = (i + 1) % n; Quad(prevRow + i, row + i, row + j, prevRow + j); }
            prevRow = row;
        }
        if (aa && ts[k - 1] >= 0.999f) Fringe(pts, cx, cy, cols[k - 1], prevRow);
    }
    public void FillCircle(float x, float y, float r, Color col)
    {
        if (r <= 0) return;
        if (r < PxL * 2.2f) { Dot(x, y, r, col); return; } // kilka pikseli: wielokąt z frędzlą niczego nie dodaje
        int n = CirclePts(x, y, r, Buf);
        FillFan(new ReadOnlySpan<Vector2>(Buf, 0, n), x, y, col);
    }
    public void FillCircleGrad(float x, float y, float r, Color cin, Color cout)
    {
        int n = CirclePts(x, y, r, Buf);
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { cin, cout };
        FillRadial(new ReadOnlySpan<Vector2>(Buf, 0, n), x, y, ts, cs);
    }
    public void FillEllipse(float x, float y, float rx, float ry, float rot, Color col)
    {
        if (Math.Max(rx, ry) < PxL * 1.6f) { Dot(x, y, MathF.Sqrt(Math.Max(rx * ry, 0)), col); return; }
        int n = EllipsePts(x, y, rx, ry, rot, Buf);
        FillFan(new ReadOnlySpan<Vector2>(Buf, 0, n), x, y, col);
    }
    /// <summary>Miękka plama (środek → przezroczysty brzeg), np. poświata.</summary>
    public void Glow(float x, float y, float r, Color col)
    {
        int n = CirclePts(x, y, r, Buf, 24);
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { col, Clear(col) };
        FillRadial(new ReadOnlySpan<Vector2>(Buf, 0, n), x, y, ts, cs, false);
    }
    public void Rect(float x, float y, float w, float h, Color col)
    {
        Reserve(4, 6);
        int a = Vx(x, y, col), b = Vx(x + w, y, col), c = Vx(x + w, y + h, col), d = Vx(x, y + h, col);
        Quad(a, b, c, d);
    }
    /// <summary>Mała kropka (sześciokąt) – ziarnistości, cząstki. r w jednostkach lokalnych.</summary>
    public void Dot(float x, float y, float r, Color col)
    {
        float pl = PxL;
        if (r < pl * 0.5f) { col.A *= r / (pl * 0.5f); r = pl * 0.5f; }
        // liczba boków zależna od wielkości na ekranie – duże kropki nie wyglądają jak sześciokąty
        float rp = r / pl;
        if (rp < 1.6f)
        {
            // ~1–3 px: romb (4 wierzchołki) wygląda tak samo jak sześciokąt
            Reserve(4, 6);
            int a = Vx(x + r, y, col), b = Vx(x, y + r, col), c = Vx(x - r, y, col), d = Vx(x, y - r, col);
            Quad(a, b, c, d); return;
        }
        int n = rp < 4 ? 6 : rp < 9 ? 8 : 12;
        Reserve(n + 1, n * 3);
        int c0 = Vx(x, y, col);
        float step = TAU / n;
        for (int i = 0; i < n; i++) { float t = i * step; Vx(x + MathF.Cos(t) * r, y + MathF.Sin(t) * r, col); }
        for (int i = 0; i < n; i++) Tri(c0, c0 + 1 + i, c0 + 1 + (i + 1) % n);
    }
    public void Diamond(float x, float y, float rw, float rh, Color col)
    {
        Reserve(4, 6);
        int a = Vx(x, y - rh, col), b = Vx(x + rw, y, col), c = Vx(x, y + rh, col), d = Vx(x - rw, y, col);
        Quad(a, b, c, d);
    }

    // ---------------------------------------------------------------- obrysy
    /// <summary>Obrys polilinii z antyaliasingiem (fazowane złącza, opcjonalne okrągłe końce).</summary>
    public void Stroke(ReadOnlySpan<Vector2> pts, bool closed, float w, Color col, bool roundCap = false)
    {
        int n = pts.Length; if (n < 2) return;
        float pl = PxL;
        if (w < pl) { col.A *= w / pl; w = pl; }
        float hw = w * 0.5f, f = pl;
        ComputeMiters(pts, closed);
        Reserve(n * 4 + 40, n * 18 + 120);
        var cl = Clear(col);
        int b = _ch.Nv;
        for (int i = 0; i < n; i++)
        {
            float mx = _nrm[i].X * _mit[i], my = _nrm[i].Y * _mit[i];
            float x = pts[i].X, y = pts[i].Y;
            Vx(x + mx * (hw + f), y + my * (hw + f), cl);
            Vx(x + mx * hw, y + my * hw, col);
            Vx(x - mx * hw, y - my * hw, col);
            Vx(x - mx * (hw + f), y - my * (hw + f), cl);
        }
        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            int r0 = b + i * 4, r1 = b + ((i + 1) % n) * 4;
            Quad(r0, r1, r1 + 1, r0 + 1);
            Quad(r0 + 1, r1 + 1, r1 + 2, r0 + 2);
            Quad(r0 + 2, r1 + 2, r1 + 3, r0 + 3);
        }
        if (roundCap && !closed) { Cap(pts[0], pts[1], hw, col); Cap(pts[n - 1], pts[n - 2], hw, col); }
    }
    void Cap(Vector2 p, Vector2 q, float hw, Color col)
    {
        float dx = p.X - q.X, dy = p.Y - q.Y, l = MathF.Sqrt(dx * dx + dy * dy);
        if (l < 1e-9f) return;
        float a0 = MathF.Atan2(dy, dx) - PI / 2;
        const int S = 8;
        for (int i = 0; i <= S; i++) _cap[i] = new Vector2(p.X + MathF.Cos(a0 + PI * i / S) * hw, p.Y + MathF.Sin(a0 + PI * i / S) * hw);
        Reserve(S * 2 + 4, S * 9);
        int c0 = Vx(p.X, p.Y, col), b = _ch.Nv;
        for (int i = 0; i <= S; i++) Vx(_cap[i].X, _cap[i].Y, col);
        for (int i = 0; i < S; i++) Tri(c0, b + i, b + i + 1);
        float f = PxL; var cl = Clear(col); int o = _ch.Nv;
        for (int i = 0; i <= S; i++) { float t = a0 + PI * i / S; Vx(p.X + MathF.Cos(t) * (hw + f), p.Y + MathF.Sin(t) * (hw + f), cl); }
        for (int i = 0; i < S; i++) Quad(b + i, o + i, o + i + 1, b + i + 1);
    }
    /// <summary>Miękki obrys: pełna alfa na osi, zero na brzegach (halo kontrastu fazowego).</summary>
    public void SoftStroke(ReadOnlySpan<Vector2> pts, bool closed, float w, Color col)
    {
        int n = pts.Length; if (n < 2) return;
        float pl = PxL;
        if (w < pl * 2) { col.A *= w / (pl * 2); w = pl * 2; }
        float hw = w * 0.5f;
        ComputeMiters(pts, closed);
        Reserve(n * 3 + 4, n * 12 + 12);
        var cl = Clear(col);
        int b = _ch.Nv;
        for (int i = 0; i < n; i++)
        {
            float mx = _nrm[i].X * _mit[i] * hw, my = _nrm[i].Y * _mit[i] * hw;
            float x = pts[i].X, y = pts[i].Y;
            Vx(x + mx, y + my, cl); Vx(x, y, col); Vx(x - mx, y - my, cl);
        }
        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            int r0 = b + i * 3, r1 = b + ((i + 1) % n) * 3;
            Quad(r0, r1, r1 + 1, r0 + 1);
            Quad(r0 + 1, r1 + 1, r1 + 2, r0 + 2);
        }
    }
    void ComputeMiters(ReadOnlySpan<Vector2> pts, bool closed)
    {
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            int ip = i - 1, inx = i + 1;
            if (closed) { if (ip < 0) ip = n - 1; if (inx >= n) inx = 0; }
            float d0x = 0, d0y = 0, d1x = 0, d1y = 0;
            if (ip >= 0) { d0x = pts[i].X - pts[ip].X; d0y = pts[i].Y - pts[ip].Y; float l = MathF.Sqrt(d0x * d0x + d0y * d0y); if (l > 1e-12f) { d0x /= l; d0y /= l; } else { d0x = d0y = 0; } }
            if (inx < n) { d1x = pts[inx].X - pts[i].X; d1y = pts[inx].Y - pts[i].Y; float l = MathF.Sqrt(d1x * d1x + d1y * d1y); if (l > 1e-12f) { d1x /= l; d1y /= l; } else { d1x = d1y = 0; } }
            if (d0x == 0 && d0y == 0) { d0x = d1x; d0y = d1y; }
            if (d1x == 0 && d1y == 0) { d1x = d0x; d1y = d0y; }
            float n0x = -d0y, n0y = d0x, n1x = -d1y, n1y = d1x;
            float mx = n0x + n1x, my = n0y + n1y, ml = MathF.Sqrt(mx * mx + my * my);
            if (ml < 1e-6f) { mx = n1x; my = n1y; ml = 1; }
            mx /= ml; my /= ml;
            float dot = mx * n1x + my * n1y;
            _nrm[i] = new Vector2(mx, my);
            _mit[i] = 1f / Math.Max(dot, 0.4f);
        }
    }
    /// <summary>Cienka polilinia bez frędzli (2 wierzchołki na punkt) – włókna, DNA, rzęski.</summary>
    public void Thin(ReadOnlySpan<Vector2> pts, bool closed, float w, Color col)
    {
        int n = pts.Length; if (n < 2) return;
        float pl = PxL;
        if (w < pl) { col.A *= w / pl; w = pl; }
        float hw = w * 0.5f;
        ComputeMiters(pts, closed);
        Reserve(n * 2 + 2, n * 6 + 6);
        int b = _ch.Nv;
        for (int i = 0; i < n; i++)
        {
            float mx = _nrm[i].X * _mit[i] * hw, my = _nrm[i].Y * _mit[i] * hw;
            Vx(pts[i].X + mx, pts[i].Y + my, col); Vx(pts[i].X - mx, pts[i].Y - my, col);
        }
        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++) { int r0 = b + i * 2, r1 = b + ((i + 1) % n) * 2; Quad(r0, r1, r1 + 1, r0 + 1); }
    }
    /// <summary>Zwężająca się polilinia z antyaliasingiem – grubość w[i] w każdym punkcie (wypustki komórek, filopodia).</summary>
    public void Taper(ReadOnlySpan<Vector2> pts, ReadOnlySpan<float> w, Color col)
    {
        int n = pts.Length; if (n < 2) return;
        float pl = PxL;
        ComputeMiters(pts, false);
        Reserve(n * 4 + 4, n * 18 + 18);
        int b = _ch.Nv;
        for (int i = 0; i < n; i++)
        {
            float wi = w[i]; var c = col;
            if (wi < pl) { c.A *= wi / pl; wi = pl; }
            float hw = wi * 0.5f, mx = _nrm[i].X * _mit[i], my = _nrm[i].Y * _mit[i], x = pts[i].X, y = pts[i].Y;
            var cl = Clear(c);
            Vx(x + mx * (hw + pl), y + my * (hw + pl), cl);
            Vx(x + mx * hw, y + my * hw, c);
            Vx(x - mx * hw, y - my * hw, c);
            Vx(x - mx * (hw + pl), y - my * (hw + pl), cl);
        }
        for (int i = 0; i < n - 1; i++)
        {
            int r0 = b + i * 4, r1 = b + (i + 1) * 4;
            Quad(r0, r1, r1 + 1, r0 + 1);
            Quad(r0 + 1, r1 + 1, r1 + 2, r0 + 2);
            Quad(r0 + 2, r1 + 2, r1 + 3, r0 + 3);
        }
    }
    public void Line(float x1, float y1, float x2, float y2, float w, Color col, bool round = false)
    {
        if (w < PxL * 1.2f) { Seg(x1, y1, x2, y2, w, col); return; } // kreska ~1 px: frędzla i zaokrąglenia są niewidoczne
        if (w < PxL * 2.5f) round = false;
        Span<Vector2> s = stackalloc Vector2[2]; s[0] = new Vector2(x1, y1); s[1] = new Vector2(x2, y2);
        Stroke(s, false, w, col, round);
    }
    /// <summary>Szybki cienki odcinek (bez frędzli) – rzęski, igły, pile.</summary>
    public void Seg(float x1, float y1, float x2, float y2, float w, Color col)
    {
        float pl = PxL;
        if (w < pl) { col.A *= w / pl; w = pl; }
        float dx = x2 - x1, dy = y2 - y1, l = MathF.Sqrt(dx * dx + dy * dy);
        if (l < 1e-12f) return;
        float nx = -dy / l * w * 0.5f, ny = dx / l * w * 0.5f;
        Reserve(4, 6);
        int a = Vx(x1 + nx, y1 + ny, col), b = Vx(x2 + nx, y2 + ny, col), c = Vx(x2 - nx, y2 - ny, col), d = Vx(x1 - nx, y1 - ny, col);
        Quad(a, b, c, d);
    }
    public void Circle(float x, float y, float r, float w, Color col)
    {
        if (r < PxL * 1.2f) { col.A *= 0.5f; Dot(x, y, Math.Max(r, w * 0.5f), col); return; }
        int n = CirclePts(x, y, r, Buf);
        Stroke(new ReadOnlySpan<Vector2>(Buf, 0, n), true, w, col);
    }
    public void Ellipse(float x, float y, float rx, float ry, float rot, float w, Color col)
    {
        if (Math.Max(rx, ry) < PxL * 1.2f) return;
        int n = EllipsePts(x, y, rx, ry, rot, Buf);
        Stroke(new ReadOnlySpan<Vector2>(Buf, 0, n), true, w, col);
    }
    public void Arc(float x, float y, float r, float a0, float a1, float w, Color col, bool round = false)
    {
        if (r < PxL * 1.2f) return;
        if (w < PxL * 2.5f) round = false;
        int n = EllipsePts(x, y, r, r, 0, Buf, a0, a1, false);
        Stroke(new ReadOnlySpan<Vector2>(Buf, 0, n), false, w, col, round);
    }
    /// <summary>Przerywany obrys wzdłuż polilinii.</summary>
    public void Dashed(ReadOnlySpan<Vector2> pts, bool closed, float w, float dash, float gap, Color col, float offset = 0)
    {
        int n = pts.Length; if (n < 2) return;
        float period = dash + gap, pos = ((offset % period) + period) % period;
        int segs = closed ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % n];
            float dx = b.X - a.X, dy = b.Y - a.Y, L = MathF.Sqrt(dx * dx + dy * dy);
            if (L < 1e-12f) continue;
            float t = 0;
            while (t < L)
            {
                float inDash = pos < dash ? dash - pos : 0;
                float step = pos < dash ? Math.Min(inDash, L - t) : Math.Min(period - pos, L - t);
                if (pos < dash) Seg(a.X + dx * t / L, a.Y + dy * t / L, a.X + dx * (t + step) / L, a.Y + dy * (t + step) / L, w, col);
                t += step; pos += step; if (pos >= period) pos -= period;
            }
        }
    }

    // ---------------------------------------------------------------- tekstury (atlas)
    public void Sprite(float x, float y, float w, float h, float ang, Rect2 uv, Color col)
    {
        Reserve(4, 6);
        float c = MathF.Cos(ang), s = MathF.Sin(ang), hw = w * 0.5f, hh = h * 0.5f;
        float ax = -hw * c + hh * s, ay = -hw * s - hh * c, bx = hw * c + hh * s, by = hw * s - hh * c;
        int i0 = VxUV(x + ax, y + ay, col, uv.Position.X, uv.Position.Y);
        int i1 = VxUV(x + bx, y + by, col, uv.End.X, uv.Position.Y);
        int i2 = VxUV(x - ax, y - ay, col, uv.End.X, uv.End.Y);
        int i3 = VxUV(x - bx, y - by, col, uv.Position.X, uv.End.Y);
        Quad(i0, i1, i2, i3);
    }

    /// <summary>
    /// Tekstura komórki rozpięta na membranie radialnej (malarz z teksturą): wachlarz ze środka przez węzły obrysu.
    /// Węzeł i leży w teksturze pod kątem i·2π/N (kąt lokalny membrany), więc wnętrze ugina się razem z obrysem;
    /// wierzchołki są odsunięte o zapas pad, w którym tekstura ma miękki brzeg i halo.
    /// </summary>
    public void TexFan(Membrane m, float pad, Rect2 uv, Color tint)
    {
        int n = m.N;
        Reserve(n + 1, n * 3);
        float cx = m.Cx, cy = m.Cy, uc = uv.Position.X + uv.Size.X * 0.5f, vc = uv.Position.Y + uv.Size.Y * 0.5f, hu = uv.Size.X * 0.5f, hv = uv.Size.Y * 0.5f, k = 1 + pad;
        int c0 = VxUV(cx, cy, tint, uc, vc);
        for (int i = 0; i < n; i++)
        {
            float t = i * TAU / n;
            VxUV(cx + (m.Px[i] - cx) * k, cy + (m.Py[i] - cy) * k, tint, uc + MathF.Cos(t) * hu, vc + MathF.Sin(t) * hv);
        }
        for (int i = 0; i < n; i++) Tri(c0, c0 + 1 + i, c0 + 1 + (i + 1) % n);
    }

    /// <summary>
    /// Ciało z teksturą cytoplazmy: wachlarz ze środka membrany przez jej węzły; współrzędne tekstury z kształtu spoczynkowego
    /// (Rx, Ry) przeskalowane do pola atlasu, więc tekstura wygina się i odkształca razem z obrysem. Gradient środek → brzeg
    /// (jaśniejszy środek, ciemniejszy, gęstszy brzeg – jak FillRadial) daje kolor wierzchołków.
    /// </summary>
    public void TexBody(Membrane m, Color cin, Color cout, int sub = 1)
    {
        // ten sam wygładzony obrys co wektorowy kontur (inaczej wierzchołki łamanej wystawałyby poza linię błony)
        int n = SmoothClosed(m.Px, m.Py, m.N, Shape, sub);
        SmoothClosed(m.Rx, m.Ry, m.N, Shape2, sub);
        float ex = 1e-4f, ey = 1e-4f;
        for (int i = 0; i < n; i++) { ex = Math.Max(ex, MathF.Abs(Shape2[i].X)); ey = Math.Max(ey, MathF.Abs(Shape2[i].Y)); }
        var uv = SpriteAtlas.Uv[SpriteAtlas.CytoFor(ex / ey)];
        float uc = uv.Position.X + uv.Size.X * 0.5f, vc = uv.Position.Y + uv.Size.Y * 0.5f, hu = uv.Size.X * 0.5f / ex, hv = uv.Size.Y * 0.5f / ey;
        // drabinka zamiast wachlarza: szczeble łączą punkt obrysu po jednej stronie osi z punktem po drugiej (węzeł i leży pod
        // kątem i·2π/N, więc i ↔ n − i), a środek szczebla (oś ciała) ma jaśniejszy kolor – działa też dla ciał wygiętych
        // w półksiężyc (Spirostomum, Loxodes), gdzie wachlarz ze środka wychodziłby poza ciało
        int h = n / 2;
        Reserve(n + h + 2, n * 6 + 12);
        int b = _ch.Nv;
        for (int i = 0; i < n; i++) VxUV(Shape[i].X, Shape[i].Y, cout, uc + Shape2[i].X * hu, vc + Shape2[i].Y * hv);
        int mid = _ch.Nv;
        for (int k = 1; k < h; k++)
        {
            int l = n - k;
            VxUV((Shape[k].X + Shape[l].X) * 0.5f, (Shape[k].Y + Shape[l].Y) * 0.5f, cin,
                uc + (Shape2[k].X + Shape2[l].X) * 0.5f * hu, vc + (Shape2[k].Y + Shape2[l].Y) * 0.5f * hv);
        }
        // czubek przedni (węzeł 0) i tylny (h): trójkąty; pomiędzy – po dwa czworokąty na szczebel (górny–oś, oś–dolny)
        for (int k = 0; k < h; k++)
        {
            int u0 = b + k, u1 = b + k + 1, l0 = b + (n - k) % n, l1 = b + (n - k - 1);
            int m0 = k == 0 ? -1 : mid + k - 1, m1 = k + 1 >= h ? -1 : mid + k;
            if (m0 < 0 && m1 < 0) { Tri(u0, u1, l1); continue; }
            if (m0 < 0) { Tri(u0, u1, m1); Tri(u0, m1, l1); continue; }
            if (m1 < 0) { Tri(u0, u1, m0); Tri(m0, u1, l0); continue; }
            Quad(u0, u1, m1, m0); Quad(m0, m1, l1, l0);
        }
    }
    /// <summary>
    /// Ciało z teksturą cytoplazmy dla dowolnego wielokąta gwiaździstego (np. dzwonek wirczyka, tułów wrotka) w bieżącym
    /// układzie lokalnym malarza src (macierz jest kopiowana); współrzędne tekstury – z obwiedni punktów.
    /// </summary>
    public void TexPoly(Painter src, ReadOnlySpan<Vector2> pts, float cx, float cy, Color cin, Color cout)
    {
        int n = pts.Length; if (n < 3) return;
        CopyXform(src);
        float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
        for (int i = 0; i < n; i++) { x0 = Math.Min(x0, pts[i].X); x1 = Math.Max(x1, pts[i].X); y0 = Math.Min(y0, pts[i].Y); y1 = Math.Max(y1, pts[i].Y); }
        float ex = Math.Max((x1 - x0) * 0.5f, 1e-4f), ey = Math.Max((y1 - y0) * 0.5f, 1e-4f), mx = (x0 + x1) * 0.5f, my = (y0 + y1) * 0.5f;
        var uv = SpriteAtlas.Uv[SpriteAtlas.CytoFor(ex / ey)];
        float uc = uv.Position.X + uv.Size.X * 0.5f, vc = uv.Position.Y + uv.Size.Y * 0.5f, hu = uv.Size.X * 0.5f / ex, hv = uv.Size.Y * 0.5f / ey;
        Reserve(n + 1, n * 3);
        int c0 = VxUV(cx, cy, cin, uc + (cx - mx) * hu, vc + (cy - my) * hv);
        for (int i = 0; i < n; i++) VxUV(pts[i].X, pts[i].Y, cout, uc + (pts[i].X - mx) * hu, vc + (pts[i].Y - my) * hv);
        for (int i = 0; i < n; i++) Tri(c0, c0 + 1 + i, c0 + 1 + (i + 1) % n);
        Identity();
    }
    void CopyXform(Painter src)
    {
        _a = src._a; _b = src._b; _c = src._c; _d = src._d; _e = src._e; _f = src._f; _sc = src._sc; _warp = src._warp;
        _wx = src._wx; _wy = src._wy; _wc = src._wc; _ws = src._ws; _wb = src._wb; _wrc = src._wrc; _wrs = src._wrs;
    }
    /// <summary>
    /// Ciało radialne z tekstury pola atlasu, w którym komórka jest kołem wpisanym w pole (np. mastocyt z ziarnistościami):
    /// współrzędne tekstury z kąta węzła (i·2π/N) i jego promienia spoczynkowego względem rMax.
    /// </summary>
    public void TexDisc(Membrane m, int slot, float rMax, Color tint)
    {
        int n = m.N;
        var uv = SpriteAtlas.Uv[slot];
        float uc = uv.Position.X + uv.Size.X * 0.5f, vc = uv.Position.Y + uv.Size.Y * 0.5f, hu = uv.Size.X * 0.5f / rMax, hv = uv.Size.Y * 0.5f / rMax;
        Reserve(n + 1, n * 3);
        m.Centroid(out float cx, out float cy);
        int c0 = VxUV(cx, cy, tint, uc, vc);
        for (int i = 0; i < n; i++) VxUV(m.Px[i], m.Py[i], tint, uc + m.Rx[i] * hu, vc + m.Ry[i] * hv);
        for (int i = 0; i < n; i++) Tri(c0, c0 + 1 + i, c0 + 1 + (i + 1) % n);
    }
    /// <summary>
    /// Ciało miękkie z tekstury komórki kory (elipsa wpisana w pole atlasu, oś długa wzdłuż x): obwiednia kształtu spoczynkowego
    /// membrany (±maxRx, ±maxRy) trafia na elipsę tekstury o półosiach ex i ey (ułamki połowy pola), więc jądro i cytoplazma
    /// odkształcają się razem z obrysem (wgniecenia, drżenie, wydłużenie przy skoku jądra).
    /// </summary>
    public void TexSoft(Membrane m, Rect2 uv, float ex, float ey, Color tint)
    {
        int n = m.N;
        float mx = 1e-4f, my = 1e-4f;
        for (int i = 0; i < n; i++) { mx = Math.Max(mx, MathF.Abs(m.Rx[i])); my = Math.Max(my, MathF.Abs(m.Ry[i])); }
        float uc = uv.Position.X + uv.Size.X * 0.5f, vc = uv.Position.Y + uv.Size.Y * 0.5f, hu = uv.Size.X * 0.5f * ex / mx, hv = uv.Size.Y * 0.5f * ey / my;
        Reserve(n + 1, n * 3);
        m.Centroid(out float cx, out float cy);
        int c0 = VxUV(cx, cy, tint, uc, vc);
        for (int i = 0; i < n; i++) VxUV(m.Px[i], m.Py[i], tint, uc + m.Rx[i] * hu, vc + m.Ry[i] * hv);
        for (int i = 0; i < n; i++) Tri(c0, c0 + 1 + i, c0 + 1 + (i + 1) % n);
    }
    /// <summary>Halo kontrastu fazowego pod ciałem z teksturą: rysowane malarzem z teksturą (białe pole), więc leży pod cytoplazmą.</summary>
    public void TexHalo(Painter src, ReadOnlySpan<Vector2> pts, float w, Color col)
    {
        CopyXform(src);
        float a0 = Alpha; Alpha = src.Alpha;
        SoftStroke(pts, true, w, col);
        Alpha = a0; Identity();
    }
    static Color Lift(Color c, float alpha)
    {
        float k = 1 / SpriteAtlas.CytoBase;
        return new Color(Math.Min(1, c.R * k), Math.Min(1, c.G * k), Math.Min(1, c.B * k), c.A * alpha);
    }
    /// <summary>
    /// Ciało komórki: halo kontrastu fazowego i wypełnienie z gradientem (pierwszy przystanek – środek, ostatni – brzeg).
    /// Z malarzem z teksturą: ziarnista cytoplazma rozpięta na membranie, a halo pod nią; bez niego – jak dotąd, wektorowo.
    /// </summary>
    public void Body(Membrane m, ReadOnlySpan<Vector2> sh, int sub, float haloW, Color halo, float cx, float cy, ReadOnlySpan<float> ts, ReadOnlySpan<Color> cs, bool aa = true)
    {
        if (Tex != null)
        {
            if (haloW > 0) Tex.TexHalo(this, sh, haloW, halo);
            Tex.TexBody(m, Lift(cs[0], Alpha), Lift(cs[cs.Length - 1], Alpha), sub);
            return;
        }
        if (haloW > 0) SoftStroke(sh, true, haloW, halo);
        FillRadial(sh, cx, cy, ts, cs, aa);
    }
    /// <summary>Jak <see cref="Body"/>, dla wielokąta w bieżącym układzie lokalnym (bez membrany).</summary>
    public void BodyPoly(ReadOnlySpan<Vector2> sh, float haloW, Color halo, float cx, float cy, ReadOnlySpan<float> ts, ReadOnlySpan<Color> cs)
    {
        if (Tex != null)
        {
            if (haloW > 0) Tex.TexHalo(this, sh, haloW, halo);
            Tex.TexPoly(this, sh, cx, cy, Lift(cs[0], Alpha), Lift(cs[cs.Length - 1], Alpha));
            return;
        }
        if (haloW > 0) SoftStroke(sh, true, haloW, halo);
        FillRadial(sh, cx, cy, ts, cs);
    }

    /// <summary>Kolory wierzchołków ciała z teksturą cytoplazmy odpowiadające gradientowi FillRadial (środek ×1,12, brzeg ×0,8).</summary>
    public static (Color cin, Color cout) CytoTint(Color body, float alpha)
    {
        float k = 1 / SpriteAtlas.CytoBase;
        return (new Color(Math.Min(1, body.R * 1.12f * k), Math.Min(1, body.G * 1.12f * k), Math.Min(1, body.B * 1.12f * k), Math.Min(1, body.A * 0.85f) * alpha),
                new Color(Math.Min(1, body.R * 0.8f * k), Math.Min(1, body.G * 0.8f * k), Math.Min(1, body.B * 0.8f * k), Math.Min(1, body.A * 1.2f) * alpha));
    }

    /// <summary>
    /// Tekstura komórki na elipsie (16 punktów) z przesuniętym środkiem wachlarza (fx, fy) – np. erytrocyt w naczyniu
    /// włosowatym wygięty w „spadochron”: jaśniejszy środek krążka przesuwa się ku tyłowi.
    /// </summary>
    public void TexEllipse(float x, float y, float rx, float ry, float rot, float fx, float fy, float pad, Rect2 uv, Color tint)
    {
        const int n = 16;
        Reserve(n + 1, n * 3);
        float c = MathF.Cos(rot), s = MathF.Sin(rot), k = 1 + pad;
        float uc = uv.Position.X + uv.Size.X * 0.5f, vc = uv.Position.Y + uv.Size.Y * 0.5f, hu = uv.Size.X * 0.5f, hv = uv.Size.Y * 0.5f;
        int c0 = VxUV(fx, fy, tint, uc, vc);
        for (int i = 0; i < n; i++)
        {
            float t = i * TAU / n, ex = MathF.Cos(t) * rx * k, ey = MathF.Sin(t) * ry * k;
            VxUV(x + ex * c - ey * s, y + ex * s + ey * c, tint, uc + MathF.Cos(t) * hu, vc + MathF.Sin(t) * hv);
        }
        for (int i = 0; i < n; i++) Tri(c0, c0 + 1 + i, c0 + 1 + (i + 1) % n);
    }

    /// <summary>Siatka wartości (pole stężeń) z interpolacją kolorów wierzchołków.</summary>
    public void Grid(float x0, float y0, float cw, float ch, int nx, int ny, Func<int, int, Color> colAt)
    {
        // wiersz wierzchołków jest wspólny dla dwóch sąsiednich pasów; po przejściu do nowej paczki poprzedni wiersz jest powtarzany
        int prev = -1; Chunk pc = null;
        for (int j = 0; j < ny; j++)
        {
            Reserve(nx * 2, nx * 6);
            if (j > 0 && _ch != pc) { prev = _ch.Nv; for (int i = 0; i < nx; i++) Vx(x0 + i * cw, y0 + (j - 1) * ch, colAt(i, j - 1)); }
            int row = _ch.Nv;
            for (int i = 0; i < nx; i++) Vx(x0 + i * cw, y0 + j * ch, colAt(i, j));
            if (j > 0) for (int i = 0; i < nx - 1; i++) Quad(prev + i, prev + i + 1, row + i + 1, row + i);
            prev = row; pc = _ch;
        }
    }
}

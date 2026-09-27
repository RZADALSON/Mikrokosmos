namespace Mikro;

/// <summary>Ziarno mineralne / piasek – nieruchoma, szklista przeszkoda (w kontraście fazowym jasny brzeg).</summary>
public sealed class Grain : Ent
{
    readonly Vector2[] _pts; readonly float _r; readonly Vector2[] _facets;
    public override float Radius => _r;
    public Grain(World w, float x, float y, float r) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.None; Layer = 1; Fixed = true; FlowK = 0; Mass = 1e6f; _r = r; R = r * 1.25f;
        int n = RandI(7, 11); _pts = new Vector2[n];
        for (int i = 0; i < n; i++) { float t = i * TAU / n + Rand(-0.2f, 0.2f), q = r * Rand(0.75f, 1.2f); _pts[i] = new Vector2(MathF.Cos(t) * q, MathF.Sin(t) * q); }
        _facets = new Vector2[6];
        for (int i = 0; i < 6; i++) _facets[i] = _pts[RandI(0, n - 1)] * Rand(0.2f, 0.7f);
        Name = "ziarno mineralne";
    }
    public override void UpdatePrim() => SetCircle(_r * 0.92f);
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        p.SoftStroke(_pts, true, _r * 0.35f, Rgb(255, 255, 240, 0.35f));
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(170, 168, 150, 0.55f), Rgb(120, 118, 100, 0.7f) };
        p.FillRadial(_pts, _r * 0.15f, -_r * 0.15f, ts, cs);
        for (int i = 0; i < _facets.Length - 1; i += 2) p.Line(_facets[i].X, _facets[i].Y, _facets[i + 1].X, _facets[i + 1].Y, _r * 0.04f, Rgb(245, 245, 230, 0.35f));
        p.Stroke(_pts, true, _r * 0.05f, Rgb(40, 40, 32, 0.8f));
    }
}

/// <summary>Włókno kolagenowe tkanki łącznej – falista wiązka fibryli ("crimp"), przeszkoda dla komórek.</summary>
public sealed class Collagen : Ent
{
    readonly float _hl, _rad, _ph;
    public Collagen(World w, float x, float y, float a, float len, float rad) : base(w, x, y, a)
    {
        Kind = Kind.None; Layer = 0; Fixed = true; FlowK = 0; Name = "włókno kolagenowe"; _hl = len / 2; _rad = rad; R = len / 2 + rad; _ph = Rand(0, TAU);
    }
    public override void UpdatePrim() => SetCapsule(_hl, _rad);
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        var buf = p.Buf2; int n = (int)Clamp(_hl * 2 / 0.8f, 8, 180);
        for (int f = -1; f <= 1; f++)
        {
            for (int k = 0; k <= n; k++)
            {
                float x = -_hl + 2 * _hl * k / n;
                buf[k] = new Vector2(x, f * _rad * 0.45f + MathF.Sin(x * 1.1f + _ph + f) * _rad * 0.35f);
            }
            var s = new ReadOnlySpan<Vector2>(buf, 0, n + 1);
            if (f == 0) p.SoftStroke(s, false, _rad * 3.2f, Rgb(255, 255, 245, 0.22f));
            p.Stroke(s, false, _rad * 0.32f, Rgb(225, 222, 205, 0.55f), true);
        }
    }
}

/// <summary>Komórka nitki skrętnicy (Spirogyra) – spiralne, taśmowate chloroplasty z pirenoidami.</summary>
public sealed class SpiroCell : Ent
{
    readonly float _hl, _hw, _ph, _pitch; readonly int _ribbons;
    public SpiroCell(World w, float x, float y, float a, float len, float wid, int ribbons, float phase) : base(w, x, y, a)
    {
        Kind = Kind.None; Layer = 1; Fixed = true; FlowK = 0; _hl = len / 2; _hw = wid / 2; R = len / 2 + wid / 2; _ph = phase; _ribbons = ribbons; _pitch = Rand(55, 80);
        Name = "skrętnica"; Latin = "Spirogyra";
    }
    public override void UpdatePrim() => SetCapsule(Math.Max(0, _hl - _hw * 0.6f), _hw);
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        float hl = _hl, hw = _hw;
        Span<Vector2> box = stackalloc Vector2[] { new(-hl, -hw), new(hl, -hw), new(hl, hw), new(-hl, hw) };
        p.SoftStroke(box, true, 6, Rgb(255, 255, 240, 0.3f));
        p.FillFan(box, 0, 0, Rgb(175, 205, 150, 0.4f));
        var buf = p.Buf2;
        for (int r = 0; r < _ribbons; r++)
        {
            int n = 60;
            for (int k = 0; k <= n; k++)
            {
                float x = -hl + 2 * hl * k / n;
                buf[k] = new Vector2(x, hw * 0.78f * MathF.Sin(TAU * x / _pitch + _ph + r * PI));
            }
            var s = new ReadOnlySpan<Vector2>(buf, 0, n + 1);
            p.Stroke(s, false, 5.5f, Rgb(50, 125, 40, 0.85f), true);
            for (int k = 4; k < n; k += 8) p.FillCircle(buf[k].X, buf[k].Y, 2.2f, Rgb(200, 235, 170, 0.8f));
        }
        p.FillCircle(0, 0, hw * 0.22f, Rgb(215, 225, 205, 0.55f));
        for (int k = 0; k < 4; k++) { float t = k * TAU / 4 + 0.4f; p.Line(MathF.Cos(t) * hw * 0.22f, MathF.Sin(t) * hw * 0.22f, MathF.Cos(t) * hw * 0.8f, MathF.Sin(t) * hw * 0.8f, 0.8f, Rgb(215, 225, 205, 0.35f)); }
        p.Stroke(box, true, 1.2f, Rgb(40, 70, 35, 0.75f));
        p.Line(-hl, -hw, -hl, hw, 1.6f, Rgb(30, 60, 25, 0.85f));
    }

    /// <summary>Tworzy nitkę z komórek wzdłuż łagodnie zakrzywionej krzywej.</summary>
    public static void Filament(World w, float x, float y, float a, int cells)
    {
        float wid = Rand(32, 46), ph = Rand(0, TAU), curv = Rand(-0.08f, 0.08f); int rib = RandI(1, 2);
        for (int i = 0; i < cells; i++)
        {
            float len = Rand(80, 120);
            float cx = x + MathF.Cos(a) * len / 2, cy = y + MathF.Sin(a) * len / 2;
            if (cx < 50 || cy < 50 || cx > w.W - 50 || cy > w.H - 50) break;
            w.Add(new SpiroCell(w, cx, cy, a, len, wid, rib, ph + i * 1.7f));
            x += MathF.Cos(a) * len; y += MathF.Sin(a) * len;
            a += curv + RandN() * 0.03f;
        }
    }
}

/// <summary>
/// Wirczyk (Vorticella) – dzwonkowaty orzęsek na kurczliwym trzonku (spazmonema).
/// Przy dotyku trzonek zwija się w spiralę w milisekundy, potem powoli się prostuje; wir rzęsek wciąga bakterie.
/// </summary>
public sealed class Vorticella : Ent
{
    readonly float _ax, _ay, _len, _br; float _ext = 1, _cool, _cph, _sway;
    public Vorticella(World w, float gx, float gy, float ang, float baseR = 0) : base(w, gx + MathF.Cos(ang) * baseR, gy + MathF.Sin(ang) * baseR, ang)
    {
        float ax = gx + MathF.Cos(ang) * baseR, ay = gy + MathF.Sin(ang) * baseR;
        Kind = Kind.Protist; Layer = 2; Solid = false; FlowK = 0; _ax = ax; _ay = ay; _len = Rand(140, 260); _br = Rand(22, 30); R = _len + _br * 2; _sway = Rand(0, TAU);
        Name = "wirczyk"; Latin = "Vorticella";
    }
    public float BellX => _ax + MathF.Cos(A) * _len * StalkF;
    public float BellY => _ay + MathF.Sin(A) * _len * StalkF;
    float StalkF => 0.18f + 0.82f * _ext;
    public override void Integrate(float dt) { }
    public void Contract() { if (_ext > 0.6f) { _ext = 0; _cool = 1.5f; W.Sfx("snap", BellX, BellY, 0.6f); } }
    public override void Step(float dt, float gdt)
    {
        _cph += gdt * TAU * 4; _cool -= gdt; _sway += dt * 0.3f;
        A += MathF.Sin(_sway) * 0.02f * dt;
        if (_cool <= 0) _ext = Math.Min(1, _ext + dt / 3.0f);
        float bx = BellX, by = BellY;
        if (_ext > 0.7f)
        {
            var o = W.Near(bx, by, _br * 1.8f, e => e.Solid && !e.Fixed && e.Kind != Kind.None);
            if (o != null) Contract();
            else if (Chance(dt * 0.01f)) Contract();
        }
        var B = W.Bact;
        if (B != null && _ext > 0.8f)
        {
            float mx = bx + MathF.Cos(A) * _br, my = by + MathF.Sin(A) * _br;
            B.Feed(mx, my, 70, 12, 110 * dt, null);
        }
    }
    public override void Draw(Painter p, View v)
    {
        var buf = p.Buf2;
        float L = _len * StalkF, ux = MathF.Cos(A), uy = MathF.Sin(A);
        int n = 40; float coil = (1 - _ext) * 9;
        for (int k = 0; k <= n; k++)
        {
            float t = (float)k / n, off = coil * MathF.Sin(t * TAU * 9) + 3 * MathF.Sin(t * 5 + _sway) * _ext;
            buf[k] = new Vector2(_ax + ux * L * t - uy * off, _ay + uy * L * t + ux * off);
        }
        var st = new ReadOnlySpan<Vector2>(buf, 0, n + 1);
        p.SoftStroke(st, false, 5, Rgb(255, 255, 240, 0.25f));
        p.Stroke(st, false, 2.2f, Rgb(70, 76, 64, 0.55f), true);
        p.FillCircle(_ax, _ay, 3.5f, Rgb(80, 80, 60, 0.6f));
        p.Local(_ax + ux * L, _ay + uy * L, A);
        float r = _br, open = 0.35f + 0.65f * _ext;
        Span<Vector2> bell = stackalloc Vector2[20];
        for (int i = 0; i < 20; i++)
        {
            float t = (float)i / 19;
            float x = t * r * 2.1f, half = r * (0.25f + 0.75f * MathF.Sqrt(t)) * (i == 19 ? open : 1);
            bell[i] = new Vector2(x, -half);
        }
        Span<Vector2> shape = stackalloc Vector2[40];
        for (int i = 0; i < 20; i++) { shape[i] = bell[i]; shape[39 - i] = new Vector2(bell[i].X, -bell[i].Y); }
        p.SoftStroke(shape, true, 7, Rgb(255, 255, 240, 0.28f));
        p.FillFan(shape, r * 1.2f, 0, Rgb(140, 150, 130, 0.45f));
        p.Stroke(shape, true, 1, Rgb(40, 46, 38, 0.7f));
        p.Arc(r * 1.15f, 0, r * 0.45f, 0.8f, TAU - 0.8f, 4, Rgb(80, 86, 70, 0.45f), true);
        float cvp = (W.T * 0.2f + Id * 0.3f) % 1;
        p.FillCircle(r * 1.7f, r * 0.35f, 5 * Smooth(cvp / 0.9f) * (cvp > 0.9f ? 0 : 1) + 0.1f, Rgb(215, 225, 205, 0.45f));
        if (_ext > 0.5f)
            for (int k = 0; k < 14; k++)
            {
                float yy = -r * open + 2 * r * open * k / 13, ph = MathF.Sin(_cph - k * 0.6f);
                p.Seg(r * 2.1f, yy, r * 2.1f + 10 + ph * 4, yy + ph * 3, Math.Max(0.9f, v.Px), Rgb(60, 66, 55, 0.5f));
            }
    }
}

/// <summary>Zewnątrzkomórkowa pułapka neutrofila (NET) – sieć DNA z histonami i białkami ziarnistości.</summary>
public sealed class Net : Ent
{
    readonly List<Vector2[]> _strands = new(); readonly Vector2[] _dots; float _age; readonly float _rad;
    public Net(World w, float x, float y, float rad) : base(w, x, y, 0)
    {
        Kind = Kind.None; Layer = 1; Solid = false; FlowK = 0; _rad = rad; R = rad;
        var nodes = new Vector2[34];
        for (int i = 0; i < nodes.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * rad; nodes[i] = new Vector2(MathF.Cos(t) * q, MathF.Sin(t) * q); }
        for (int i = 0; i < nodes.Length; i++)
            for (int j = i + 1; j < nodes.Length; j++)
            {
                float d = (nodes[i] - nodes[j]).Length();
                if (d > rad * 0.45f) continue;
                var s = new Vector2[8];
                for (int k = 0; k < 8; k++) { float t = k / 7f; s[k] = nodes[i].Lerp(nodes[j], t) + new Vector2(RandN(), RandN()) * rad * 0.02f; }
                _strands.Add(s);
            }
        _dots = new Vector2[120];
        for (int i = 0; i < _dots.Length; i++) { var s = _strands[RandI(0, _strands.Count - 1)]; _dots[i] = s[RandI(0, 7)]; }
    }
    public override void Integrate(float dt) { }
    public override void Step(float dt, float gdt)
    {
        _age += gdt;
        if (_age > 150) { Alive = false; return; }
        foreach (var e in W.Ents)
            if (e is Staph s && s.State == St.Free && Hypot(s.X - X, s.Y - Y) < _rad) { s.State = St.Caught; s.Solid = false; }
    }
    public override void Draw(Painter p, View v)
    {
        float al = Math.Min(1, _age / 1.5f) * Math.Min(1, (150 - _age) / 10);
        p.Local(X, Y, 0);
        p.Glow(0, 0, _rad * 1.1f, Rgb(170, 200, 230, 0.12f * al));
        var c = Rgb(200, 225, 245, 0.45f * al);
        foreach (var s in _strands) p.Stroke(s, false, 0.12f, c);
        foreach (var d in _dots) p.Dot(d.X, d.Y, 0.18f, Rgb(245, 235, 200, 0.6f * al));
    }
}

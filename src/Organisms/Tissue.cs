namespace Mikro;

/// <summary>
/// Makrofag tkankowy (~20 µm): duży, pofałdowany brzeg (ruffling), nerkowate jądro, mnóstwo lizosomów.
/// Pełza wolniej niż neutrofil; pochłania bakterie, drożdże Candida i szczątki martwych komórek (eferocytoza).
/// </summary>
public sealed class Macrophage : Ent
{
    const int NN = 72;
    readonly float _r0; readonly float[] _rr = new float[NN]; readonly float _seed; float _dir, _turnT;
    readonly Vector3[] _lys;
    sealed class Ph { public Ent O; public float St, Lx, Ly; }
    readonly List<Ph> _ph = new();
    public override float Radius => _r0;
    public Macrophage(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.TissueCell; Layer = 2; Name = "makrofag"; Latin = "makrofag tkankowy (histiocyt)"; Soft = 0.8f;
        _r0 = Rand(9, 11.5f); R = _r0 * 1.7f; Mass = 30; _seed = Rand(0, 99); _dir = Rand(0, TAU);
        Mem = new Membrane(NN) { MaxDef = 4, Stiff = 6, Damping = 0.6f }; MemK = 4; MemS = 0.45f;
        _lys = new Vector3[110];
        for (int i = 0; i < _lys.Length; i++) _lys[i] = new Vector3(Rand(0, TAU), MathF.Sqrt(R01()) * 0.8f, Rand(0.12f, 0.35f));
        Shape(); Mem.Follow(x, y, 0, 1, 1);
    }
    void Shape()
    {
        float t = W.T;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, c = MathF.Cos(th - _dir);
            float r = _r0 * (1 + 0.1f * Noise1(th * 3 + t * 0.4f + _seed) + 0.05f * Noise1(th * 11 + t * 1.3f) * Math.Max(0, c) + 0.18f * Gauss(WrapA(th - _dir), 0.7f));
            _rr[i] = r;
        }
        foreach (var p in _ph) if (p.St < 30) { float ap = MathF.Atan2(p.Ly, p.Lx), need = Hypot(p.Lx, p.Ly) + p.O.Radius + 0.5f; for (int i = 0; i < NN; i++) { float g = Gauss(WrapA(i * TAU / NN - ap), 0.3f); _rr[i] = Math.Max(_rr[i], Lerp(_rr[i], need, g)); } }
        Mem.Radial(_rr);
    }
    public override void UpdatePrim() => RadialParts(_rr, NN, 8);
    public override bool Rotates => false; // kształt liczony względem kierunku pełzania w układzie świata
    bool Food(Ent o) => o.Alive && o.Holder == null && (o is Staph s && s.State == St.Free || o is CandidaYeast y && y.State == St.Free);
    public override bool Collides(Ent o) => o.Holder != this;
    public override void Step(float dt, float gdt)
    {
        _turnT -= dt;
        var f = W.Near(X, Y, 45, Food);
        if (f != null) _dir = Turn(_dir, MathF.Atan2(f.Y - Y, f.X - X), 0.8f * gdt);
        else if (_turnT <= 0) { _dir += RandN(); _turnT = ExpRand(60); }
        float v = 8f / 60f * (1 - _ph.Count * 0.06f);
        Vx = MathF.Cos(_dir) * v; Vy = MathF.Sin(_dir) * v;
        if (_ph.Count < 10)
            foreach (var o in Nb)
            {
                if (!Food(o)) continue;
                o.State = St.Cup; o.Solid = false; o.Holder = this;
                _ph.Add(new Ph { O = o, Lx = o.X - X, Ly = o.Y - Y });
                break;
            }
        for (int k = _ph.Count - 1; k >= 0; k--)
        {
            var p = _ph[k]; var o = p.O;
            if (!o.Alive || o.Holder != this) { _ph.RemoveAt(k); continue; }
            p.St += dt;
            if (p.St > 30) o.State = St.Vac;
            float q = Damp(0.05f, dt); p.Lx = Lerp(p.Lx, p.Lx * 0.4f, q); p.Ly = Lerp(p.Ly, p.Ly * 0.4f, q);
            o.Dig = Clamp((p.St - 30) / 1500, 0, 1);
            if (o.Dig >= 1) { o.Alive = false; _ph.RemoveAt(k); continue; }
            o.X = X + p.Lx; o.Y = Y + p.Ly;
        }
        Shape();
    }
    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 2.6f, Rgb(255, 255, 240, 0.28f));
        Span<float> ts = stackalloc float[] { 0f, 0.6f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(120, 118, 108, 0.55f), Rgb(135, 132, 120, 0.45f), Rgb(185, 182, 170, 0.22f) };
        p.FillRadial(sh, X, Y, ts, cs);
        p.Stroke(sh, true, 0.14f, Rgb(40, 40, 34, 0.7f));
        p.Local(X, Y, 0);
        float nx = -MathF.Cos(_dir) * _r0 * 0.25f, ny = -MathF.Sin(_dir) * _r0 * 0.25f;
        p.FillEllipse(nx, ny, _r0 * 0.38f, _r0 * 0.28f, _dir, Rgb(85, 85, 105, 0.45f));
        p.Arc(nx + MathF.Cos(_dir) * _r0 * 0.3f, ny + MathF.Sin(_dir) * _r0 * 0.3f, _r0 * 0.15f, _dir + 2, _dir + 4.3f, 0.5f, Rgb(160, 158, 146, 0.6f));
        foreach (var l in _lys) { float r = RadialUtil.Interp(_rr, NN, l.X) * l.Y; p.Dot(MathF.Cos(l.X) * r, MathF.Sin(l.X) * r, l.Z, l.Z > 0.28f ? Rgb(215, 220, 200, 0.55f) : Rgb(50, 50, 42, 0.5f)); }
        foreach (var ph in _ph) p.Circle(ph.Lx, ph.Ly, ph.O.Radius + 0.35f, 0.12f, Rgb(235, 240, 220, 0.5f));
    }
}

/// <summary>Fibroblast – wrzecionowata komórka tkanki łącznej (~50–100 µm z wypustkami), produkuje kolagen; pełza bardzo wolno.</summary>
public sealed class Fibroblast : Ent
{
    readonly float _l, _w; readonly float _seed;
    sealed class Proc { public float Ang, Len, W, Ph; }
    readonly Proc[] _proc;
    readonly Vector3[] _gran; readonly Vector2[] _chrom;
    const int NS = 150;
    readonly float[] _r = new float[NS];
    float _t;
    public override float Length => _l;
    public override float Radius => _w / 2;
    public Fibroblast(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.TissueCell; Layer = 1; Name = "fibroblast"; Latin = "fibroblast"; Soft = 0.6f; FlowK = 0;
        _l = Rand(55, 100); _w = Rand(7, 10); R = _l / 2 + 6; Mass = 80; _seed = Rand(0, 99);
        // dwie główne wypustki wzdłuż osi + 0–3 boczne; w tkance fibroblast jest płaski, gwiaździsto-wrzecionowaty
        int side = RandI(0, 3);
        _proc = new Proc[2 + side];
        _proc[0] = new Proc { Ang = 0, Len = _l * Rand(0.36f, 0.46f), W = Rand(0.1f, 0.14f), Ph = Rand(0, TAU) };
        _proc[1] = new Proc { Ang = PI + RandN() * 0.15f, Len = _l * Rand(0.34f, 0.46f), W = Rand(0.1f, 0.14f), Ph = Rand(0, TAU) };
        for (int i = 0; i < side; i++) _proc[2 + i] = new Proc { Ang = Sign() * Rand(0.7f, 2.4f), Len = _l * Rand(0.12f, 0.3f), W = Rand(0.08f, 0.12f), Ph = Rand(0, TAU) };
        _gran = new Vector3[70];
        for (int i = 0; i < _gran.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()); _gran[i] = new Vector3(MathF.Cos(t) * q, MathF.Sin(t) * q, Rand(0.15f, 0.3f)); }
        _chrom = new Vector2[18];
        for (int i = 0; i < _chrom.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * 0.8f; _chrom[i] = new Vector2(MathF.Cos(t) * q, MathF.Sin(t) * q); }
        Shape();
    }
    void Shape()
    {
        float a0 = _w * 0.9f, b0 = _w * 0.5f;
        for (int i = 0; i < NS; i++)
        {
            float th = i * TAU / NS, c = MathF.Cos(th), s = MathF.Sin(th);
            float r = a0 * b0 / MathF.Sqrt(Sq(b0 * c) + Sq(a0 * s));
            foreach (var p in _proc)
            {
                float len = p.Len * (0.92f + 0.08f * MathF.Sin(_t * 0.002f + p.Ph)); // powolne wysuwanie i cofanie
                r = Math.Max(r, len * Gauss(WrapA(th - p.Ang), p.W) + r * 0.2f);
            }
            r *= 1 + 0.04f * Noise1(th * 5 + _seed);
            _r[i] = r;
        }
    }
    public override void UpdatePrim() => AxisParts(3, _l / 2, _w / 2, t => MathF.Pow(Math.Max(0, 1 - t * t), 1.6f) + 0.05f, 0);
    public override bool Permeable(Ent o) => o is Neutrophil or Macrophage or Lymphocyte;
    public override void Step(float dt, float gdt)
    {
        _t += dt; float v = 1f / 60f; Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v; A += RandN() * 0.002f * MathF.Sqrt(dt);
        if ((W.Frame + Id) % 10 == 0) Shape();
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        var sh = p.Buf2;
        for (int i = 0; i < NS; i++) { float th = i * TAU / NS; sh[i] = new Vector2(MathF.Cos(th) * _r[i], MathF.Sin(th) * _r[i]); }
        var s = new ReadOnlySpan<Vector2>(sh, 0, NS);
        float lw = Math.Max(v.Px, 0.1f);
        p.SoftStroke(s, true, 1.6f, Rgb(255, 255, 240, 0.22f));
        Span<float> ts = stackalloc float[] { 0f, 0.35f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(150, 146, 132, 0.52f), Rgb(160, 156, 142, 0.42f), Rgb(175, 172, 160, 0.2f) };
        p.FillRadial(s, 0, 0, ts, cs);
        p.Stroke(s, true, lw, Rgb(60, 58, 48, 0.4f));
        // włókienka naprężeniowe (aktyna) wzdłuż osi komórki
        for (int k = -2; k <= 2; k++) p.Line(-_l * 0.3f, k * _w * 0.08f, _l * 0.3f, k * _w * 0.06f, lw * 0.7f, Rgb(90, 88, 75, 0.18f));
        // drobnoziarnista cytoplazma (szorstka siateczka śródplazmatyczna, mitochondria)
        foreach (var g in _gran) p.Dot(g.X * _w * 1.1f, g.Y * _w * 0.45f, g.Z, Rgb(80, 78, 64, 0.45f));
        // jądro owalne z chromatyną i 1–2 jąderkami
        float nx = _w * 0.1f;
        p.FillEllipse(nx, 0, _w * 0.8f, _w * 0.3f, 0, Rgb(115, 112, 128, 0.5f));
        p.Ellipse(nx, 0, _w * 0.8f, _w * 0.3f, 0, lw, Rgb(60, 58, 72, 0.55f));
        foreach (var c in _chrom) p.Dot(nx + c.X * _w * 0.7f, c.Y * _w * 0.24f, 0.18f, Rgb(70, 66, 88, 0.45f));
        p.FillCircle(nx + _w * 0.2f, _w * 0.05f, 0.45f, Rgb(45, 42, 60, 0.75f));
        p.FillCircle(nx - _w * 0.25f, -_w * 0.06f, 0.32f, Rgb(45, 42, 60, 0.7f));
    }
}

/// <summary>Adipocyt (komórka tłuszczowa, ~50–150 µm): jedna wielka kropla lipidu, jądro zepchnięte do brzegu (kształt sygnetu).</summary>
public sealed class Adipocyte : Ent
{
    const int NN = 40;
    readonly float _r; readonly float _na;
    public override float Radius => _r;
    public Adipocyte(World w, float x, float y, float r) : base(w, x, y, 0)
    {
        Kind = Kind.TissueCell; Layer = 0; Name = "adipocyt"; Latin = "komórka tłuszczowa"; Soft = 0.5f; Fixed = true; FlowK = 0;
        _r = r; R = r + 2; Mass = 1e5f; _na = Rand(0, TAU);
        Mem = new Membrane(NN) { MaxDef = r * 0.12f }; MemK = 2; MemS = 0.4f;
        var rr = new float[NN]; for (int i = 0; i < NN; i++) rr[i] = r; Mem.Radial(rr); Mem.Follow(x, y, 0, 1, 1);
    }
    public override void UpdatePrim() => SetCircle(_r * 0.93f);
    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 3, Rgb(255, 255, 240, 0.3f));
        p.FillFan(sh, X, Y, Rgb(225, 222, 200, 0.28f));
        p.Stroke(sh, true, 0.8f, Rgb(120, 110, 80, 0.55f));
        p.Local(X, Y, 0);
        p.Glow(-_r * 0.25f, -_r * 0.25f, _r * 0.6f, Rgb(255, 255, 245, 0.25f));
        float nx = MathF.Cos(_na) * _r * 0.88f, ny = MathF.Sin(_na) * _r * 0.88f;
        p.FillEllipse(nx, ny, _r * 0.1f, _r * 0.045f, _na + PI / 2, Rgb(95, 90, 115, 0.6f));
    }
}

/// <summary>
/// Mastocyt (komórka tuczna, ~10–15 µm) wypełniony ciemnymi ziarnistościami z histaminą.
/// Wykrywając bakterie degranuluje – histamina rozszerza naczynia i przywołuje neutrofile.
/// </summary>
public sealed class MastCell : Ent
{
    readonly float _r; readonly float _seed;
    struct Gr { public float X, Y, S, Vx, Vy, Life; public bool Out; }
    readonly Gr[] _g;
    bool _fired; float _degr;
    readonly float[] _rr = new float[48];
    public override float Radius => _r;
    public MastCell(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.TissueCell; Layer = 1; Name = "mastocyt"; Latin = "komórka tuczna"; Soft = 0.5f;
        _r = Rand(5, 7); R = _r * 1.6f; Mass = 10; FlowK = 0; _seed = Rand(0, 99);
        for (int i = 0; i < 48; i++) _rr[i] = _r * (1 + 0.08f * Noise1(i * 0.6f + _seed) + 0.05f * MathF.Cos(i * TAU / 48 * 2));
        // żywy, niebarwiony mastocyt: szary, gęsto wypełniony drobnymi, silnie załamującymi światło ziarnistościami
        _g = new Gr[420];
        for (int i = 0; i < _g.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * 0.9f; _g[i] = new Gr { X = MathF.Cos(t) * q, Y = MathF.Sin(t) * q, S = Rand(0.11f, 0.2f), Life = 1 }; }
    }
    public override void Step(float dt, float gdt)
    {
        Vx = Vy = 0;
        if (!_fired && W.Near(X, Y, 25, e => e is Staph s && s.State == St.Free) != null)
        {
            _fired = true;
            W.AddStat("histamine", 1);
            W.LogOnce("mast", "Mastocyt wykrył bakterie i degranulował – histamina rozszerza naczynia, z żyłki wyjdą kolejne neutrofile.", "info", 30);
        }
        if (!_fired) return;
        _degr = Math.Min(1, _degr + dt / 120);
        // degranulacja: ziarnistości przechodzą przez błonę i rozpraszają się w tkance (wydzielanie histaminy)
        for (int i = 0; i < _g.Length; i++)
        {
            ref var g = ref _g[i];
            if (!g.Out && R01() < dt * 0.012f * (Hypot(g.X, g.Y) + 0.2f))
            {
                g.Out = true; float l = Math.Max(Hypot(g.X, g.Y), 0.05f);
                g.Vx = g.X / l * Rand(0.004f, 0.012f); g.Vy = g.Y / l * Rand(0.004f, 0.012f);
            }
            if (g.Out) { g.X += g.Vx * dt; g.Y += g.Vy * dt; g.Life = Math.Max(0, g.Life - dt / 300); }
        }
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        var sh = p.Buf2;
        for (int i = 0; i < 48; i++) { float t = i * TAU / 48; sh[i] = new Vector2(MathF.Cos(t) * _rr[i], MathF.Sin(t) * _rr[i]); }
        var s = new ReadOnlySpan<Vector2>(sh, 0, 48);
        float lw = Math.Max(v.Px, 0.1f);
        p.SoftStroke(s, true, 1.8f, Rgb(255, 255, 240, 0.25f));
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(150, 148, 140, 0.5f), Rgb(125, 122, 115, 0.55f) };
        p.FillRadial(s, 0, 0, ts, cs);
        // jądro prawie zasłonięte przez ziarnistości – widać tylko jaśniejszą plamę
        p.FillEllipse(_r * 0.08f, -_r * 0.05f, _r * 0.38f, _r * 0.32f, 0.4f, Rgb(170, 168, 165, 0.3f));
        p.Stroke(s, true, lw, Rgb(50, 48, 42, 0.6f));
        bool hi = 0.2f / v.Px > 1.2f;
        for (int i = 0; i < _g.Length; i++)
        {
            var g = _g[i]; if (g.Life <= 0.02f) continue;
            float x = g.X * _r, y = g.Y * _r;
            p.Dot(x, y, g.S, Rgb(50, 47, 52, 0.55f * g.Life));
            if (hi && (i & 3) == 0) p.Dot(x - g.S * 0.3f, y - g.S * 0.3f, g.S * 0.35f, Rgb(240, 240, 232, 0.55f * g.Life));
        }
    }
}

/// <summary>Drożdżak Candida albicans (4–6 µm) – może być sfagocytowany; z czasem wypuszcza strzępkę (formę inwazyjną).</summary>
public sealed class CandidaYeast : Ent
{
    public readonly float Rr; float _germ;
    public override float Radius => Rr;
    public CandidaYeast(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.Fungus; Layer = 2; Name = "drożdżak"; Latin = "Candida albicans"; Soft = 0.1f; State = St.Free;
        Rr = Rand(2, 2.8f); R = Rr + 0.5f; Mass = 2; D = 0.001f; _germ = Rand(0, 0.3f);
    }
    public override void UpdatePrim() => SetCircle(State == St.Free ? Rr : 0.1f);
    public override bool Collides(Ent o) => State == St.Free;
    public override void Step(float dt, float gdt)
    {
        if (State != St.Free) return;
        _germ += dt / 2400;
        if (_germ >= 1 && !W.Preview)
        {
            // przejście drożdże → strzępka (w 37 °C, w tkance)
            W.Add(new CandidaHypha(W, X, Y, A));
            Alive = false;
        }
    }
    public override void Draw(Painter p, View v)
    {
        float oldA = p.Alpha; if (State != St.Free) p.Alpha = 1 - Dig;
        p.Local(X, Y, A);
        p.Glow(0, 0, Rr * 1.4f, Rgb(255, 255, 240, 0.25f));
        p.FillEllipse(0, 0, Rr * 1.1f, Rr, 0, Rgb(170, 165, 140, 0.55f));
        p.Ellipse(0, 0, Rr * 1.1f, Rr, 0, 0.15f, Rgb(60, 58, 45, 0.75f));
        p.FillCircle(-Rr * 0.2f, 0, Rr * 0.35f, Rgb(215, 215, 200, 0.55f));
        if (_germ > 0.7f) p.Line(Rr, 0, Rr + (_germ - 0.7f) * Rr * 4, 0, Rr * 0.6f, Rgb(170, 165, 140, 0.55f), true);
        p.Alpha = oldA;
    }
}

/// <summary>
/// Strzępka Candida albicans – rośnie wierzchołkowo (kilka µm/h, w grze szybciej) i rozgałęzia się. Za duża do fagocytozy:
/// neutrofile rozpoznają rozmiar drobnoustroju i wyrzucają na nią sieci NET (Branzk i in., Nat Immunol 2014).
/// </summary>
public sealed class CandidaHypha : Ent
{
    readonly List<Vector2> _pts = new(); float _grow, _dirA, _hp = 1, _dmgT;
    public override float Length => _pts.Count * 3f;
    public override float Radius => 1.5f;
    public CandidaHypha(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Fungus; Layer = 2; Name = "strzępka"; Latin = "Candida albicans (forma strzępkowa)"; Solid = false; FlowK = 0;
        _pts.Add(new Vector2(x, y)); _dirA = a; R = 5;
    }
    public bool Near(float x, float y, float r)
    {
        foreach (var q in _pts) if ((q.X - x) * (q.X - x) + (q.Y - y) * (q.Y - y) < r * r) return true;
        return false;
    }
    public void Hurt(float d) { _hp -= d; _dmgT = 1; if (_hp <= 0 && Alive) { Kill(); W.AddStat("killed", 3); W.AddStat("fungi", 1); } }
    public override void Kill()
    {
        if (!Alive) return;
        foreach (var q in _pts) if (Chance(0.3f)) W.Fx.Spill(q.X, q.Y, 3, new FX.SpillOpt { Speed = 2, R = 0.3f, Life = 4, D = 0.02f, Cols = new[] { Rgb(170, 165, 140, 0.6f) } });
        W.Sfx("pop", X, Y, 0.7f); Alive = false;
    }
    public override void Step(float dt, float gdt)
    {
        _dmgT -= gdt;
        if (_pts.Count < 40)
        {
            _grow += dt / 60;
            while (_grow >= 1 && _pts.Count < 40)
            {
                _grow -= 1; _dirA += RandN() * 0.25f;
                var last = _pts[^1];
                _pts.Add(new Vector2(last.X + MathF.Cos(_dirA) * 3, last.Y + MathF.Sin(_dirA) * 3));
            }
        }
        float mx = 0, my = 0; foreach (var q in _pts) { mx += q.X; my += q.Y; }
        X = mx / _pts.Count; Y = my / _pts.Count; R = _pts.Count * 1.6f + 4;
        // sieć NET w pobliżu niszczy strzępkę
        foreach (var e in W.Ents) if (e is Net nt && nt.Alive && Near(nt.X, nt.Y, 24)) { Hurt(dt * 0.02f); break; }
    }
    public override void Draw(Painter p, View v)
    {
        p.Identity();
        if (_pts.Count < 2) return;
        int n = _pts.Count; var buf = p.Buf2;
        for (int i = 0; i < n; i++) buf[i] = _pts[i];
        var s = new ReadOnlySpan<Vector2>(buf, 0, n);
        p.SoftStroke(s, false, 4, Rgb(255, 255, 240, 0.25f));
        p.Stroke(s, false, 2.4f, _dmgT > 0 ? Rgb(200, 150, 130, 0.6f) : Rgb(170, 165, 140, 0.55f), true);
        p.Stroke(s, false, 0.8f, Rgb(215, 212, 195, 0.4f), true);
        for (int i = 4; i < n; i += 4) { var a = buf[i]; var b = buf[i - 1]; float tx = a.X - b.X, ty = a.Y - b.Y, l = Math.Max(Hypot(tx, ty), 1e-3f); p.Line(a.X - ty / l * 1.2f, a.Y + tx / l * 1.2f, a.X + ty / l * 1.2f, a.Y - tx / l * 1.2f, 0.25f, Rgb(80, 78, 60, 0.6f)); }
    }
}

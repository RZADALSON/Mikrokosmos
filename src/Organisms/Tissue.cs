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
    public override void UpdatePrim() => SetCircle(_r0 * 0.9f);
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
    readonly float _l, _w; float _ph; readonly float _seed;
    public override float Length => _l;
    public override float Radius => _w / 2;
    public Fibroblast(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.TissueCell; Layer = 1; Name = "fibroblast"; Latin = "fibroblast"; Soft = 0.6f; FlowK = 0;
        _l = Rand(55, 100); _w = Rand(7, 10); R = _l / 2 + 4; Mass = 80; _seed = Rand(0, 99);
    }
    public override void UpdatePrim() => SetCapsule(_l * 0.25f, _w * 0.45f);
    public override void Step(float dt, float gdt) { _ph += dt * 0.01f; float v = 1f / 60f; Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v; A += RandN() * 0.002f * MathF.Sqrt(dt); }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        var buf = p.Buf2; int n = 0; const int K = 30;
        for (int k = 0; k <= K; k++) { float t = -1 + 2f * k / K, hw = _w / 2 * MathF.Pow(Math.Max(0, 1 - t * t), 1.6f) + 0.25f; buf[n++] = new Vector2(t * _l / 2, hw + MathF.Sin(t * 4 + _seed) * 0.6f); }
        for (int k = K; k >= 0; k--) { float t = -1 + 2f * k / K, hw = _w / 2 * MathF.Pow(Math.Max(0, 1 - t * t), 1.6f) + 0.25f; buf[n++] = new Vector2(t * _l / 2, -hw + MathF.Sin(t * 4 + _seed) * 0.6f); }
        var sh = new ReadOnlySpan<Vector2>(buf, 0, n);
        p.SoftStroke(sh, true, 2.2f, Rgb(255, 255, 240, 0.26f));
        p.FillFan(sh, 0, 0, Rgb(150, 145, 128, 0.5f));
        p.Stroke(sh, true, 0.12f, Rgb(50, 48, 40, 0.5f));
        for (int k = 0; k < 14; k++) { float t = -0.8f + 1.6f * k / 13; p.Dot(t * _l / 2, MathF.Sin(k * 2.3f + _seed) * _w * 0.15f, 0.25f, Rgb(70, 68, 55, 0.5f)); }
        p.FillEllipse(0, 0, _w * 0.9f, _w * 0.3f, 0, Rgb(100, 98, 118, 0.5f));
        p.Ellipse(0, 0, _w * 0.9f, _w * 0.3f, 0, 0.12f, Rgb(50, 50, 64, 0.5f));
        p.FillCircle(_w * 0.2f, 0, 0.5f, Rgb(40, 40, 52, 0.7f));
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
    readonly float _r; readonly Vector3[] _g; float _degr; bool _fired;
    public override float Radius => _r;
    public MastCell(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.TissueCell; Layer = 1; Name = "mastocyt"; Latin = "komórka tuczna"; Soft = 0.5f;
        _r = Rand(5, 7); R = _r + 1; Mass = 10; FlowK = 0;
        _g = new Vector3[160];
        for (int i = 0; i < _g.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * 0.88f; _g[i] = new Vector3(MathF.Cos(t) * q, MathF.Sin(t) * q, Rand(0.18f, 0.3f)); }
    }
    public override void Step(float dt, float gdt)
    {
        Vx = Vy = 0;
        if (!_fired && W.Near(X, Y, 25, e => e is Staph s && s.State == St.Free) != null)
        {
            _fired = true;
            W.Fx.Spill(X, Y, 60, new FX.SpillOpt { Speed = 6, R = 0.22f, Life = 8, R0 = _r, D = 0.05f, Cols = new[] { Rgb(70, 50, 90, 0.8f), Rgb(110, 80, 130, 0.7f) } });
            W.AddStat("histamine", 1);
            W.LogOnce("mast", "Mastocyt wykrył bakterie i degranulował – histamina rozszerza naczynia, z żyłki wyjdą kolejne neutrofile.", "info", 30);
        }
        if (_fired) _degr = Math.Min(1, _degr + dt / 60);
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        p.Glow(0, 0, _r * 1.3f, Rgb(255, 255, 240, 0.2f));
        p.FillCircle(0, 0, _r, Rgb(150, 140, 155, 0.4f));
        p.Circle(0, 0, _r, 0.14f, Rgb(50, 40, 60, 0.6f));
        int keep = (int)(_g.Length * (1 - _degr * 0.8f));
        for (int i = 0; i < keep; i++) p.Dot(_g[i].X * _r, _g[i].Y * _r, _g[i].Z, Rgb(70, 45, 95, 0.75f));
        p.FillCircle(0, 0, _r * 0.3f, Rgb(120, 110, 135, 0.5f));
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

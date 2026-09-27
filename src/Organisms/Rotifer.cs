namespace Mikro;

/// <summary>
/// Wrotek (Philodina, ~300–450 µm): dwa wieńce rzęsek ("koła") tworzą wir, który wciąga bakterie i małe glony;
/// mastaks (szczęki) miele pokarm; stopa z palcami przytwierdza się do podłoża.
/// Porusza się jak pijawka, a dotknięty chowa koronę i kurczy ciało.
/// </summary>
public sealed class Rotifer : Ent
{
    enum Mode { Feed, Crawl, Retract }
    Mode _mode = Mode.Feed;
    public float Lmax, Wd, Ext = 1, Corona = 1, Cph, Mph, St0, Gut;
    float _dir;

    public override float Length => Lmax * (0.45f + 0.55f * Ext);
    public override float Radius => Wd / 2;

    public Rotifer(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Rotifer; Layer = 3; Lmax = Rand(300, 440); Wd = Lmax * 0.2f; Mass = 30; R = Lmax / 2 + 20;
        Edible = true; Food = 3; Name = "wrotek"; Latin = "Philodina"; Soft = 0.2f; State = St.Free; St0 = Rand(4, 12); _dir = a;
    }
    public override void UpdatePrim() => AxisParts(3, Length / 2, Wd / 2, t => Math.Max(0.3f, HalfW((t + 1) / 2) / (Wd / 2)), 0);
    public override bool Collides(Ent o) => State == St.Free;
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (State != St.Free || o.Fixed || o is Grain || o is SpiroCell) return;
        Poke();
    }
    /// <summary>Odruch obronny: dotknięty wrotek chowa koronę i kurczy ciało.</summary>
    public void Poke()
    {
        if (State != St.Free) return;
        if (_mode != Mode.Retract) { _mode = Mode.Retract; St0 = Rand(3, 6); W.Sfx("snap", X, Y, 0.4f); }
        else St0 = Math.Max(St0, 1.5f);
    }
    public override void Step(float dt, float gdt)
    {
        Cph += gdt * TAU * 1.2f;
        Mph += gdt * TAU * (_mode == Mode.Feed ? 2.2f : 0.6f);
        if (State != St.Free) { Vx = Vy = 0; Fixed = false; return; }
        St0 -= dt;
        Gut = Math.Max(0, Gut - dt * 0.01f);
        switch (_mode)
        {
            case Mode.Feed:
                Fixed = true; Vx = Vy = 0;
                Ext = Lerp(Ext, 1, Damp(1.5f, dt)); Corona = Lerp(Corona, 1, Damp(3, dt));
                A += MathF.Sin(W.T * 0.7f + Id) * 0.05f * dt;
                FeedVortex(dt);
                if (St0 <= 0) { _mode = Mode.Crawl; St0 = Rand(5, 10); _dir = A + RandN() * 0.8f; }
                break;
            case Mode.Crawl:
                Fixed = false;
                Corona = Lerp(Corona, 0, Damp(4, dt));
                float cyc = MathF.Sin(W.T * 2.4f + Id);
                Ext = 0.65f + 0.35f * cyc;
                A = Turn(A, _dir, 0.4f * dt);
                float v = Math.Max(0, -MathF.Cos(W.T * 2.4f + Id)) * 90;
                Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
                if (St0 <= 0) { _mode = Mode.Feed; St0 = Rand(10, 25); }
                break;
            case Mode.Retract:
                Fixed = false; Vx = Vy = 0;
                Ext = Lerp(Ext, 0, Damp(8, gdt)); Corona = Lerp(Corona, 0, Damp(10, gdt));
                if (St0 <= 0) { _mode = Mode.Feed; St0 = Rand(6, 14); }
                break;
        }
    }
    readonly List<Ent> _q = new();
    void FeedVortex(float dt)
    {
        float hx = X + MathF.Cos(A) * Length * 0.5f, hy = Y + MathF.Sin(A) * Length * 0.5f;
        const float RV = 170;
        if (W.Bact != null) Gut += W.Bact.Feed(hx, hy, RV, 22, 170 * dt, null) * 0.004f;
        W.Query(hx, hy, RV, _q);
        foreach (var e in _q)
        {
            if (!e.Alive || e == this) continue;
            float dx = hx - e.X, dy = hy - e.Y, d = Hypot(dx, dy);
            if (d > RV || d < 1e-3f) continue;
            float front = (-dx * MathF.Cos(A) - dy * MathF.Sin(A)) / d;
            if (front < 0.2f) continue;
            if (e is Chlamy c && c.State == St.Free)
            {
                if (d < 22) { c.Alive = false; Gut += 0.3f; W.Fx.Spill(hx, hy, 4, new FX.SpillOpt { Speed = 20, R = 1.5f, Life = 2, Cols = new[] { Rgb(60, 130, 50, 0.7f) } }); continue; }
                float k = 120 * (1 - d / RV) * dt / d; c.X += dx * k; c.Y += dy * k;
            }
            else if (e is Paramecium pm && pm.State == St.Swim)
            {
                float k = 60 * (1 - d / RV) * dt / d; pm.X += dx * k; pm.Y += dy * k;
            }
        }
    }

    // profil ciała Philodina (t: 0 = palce stopy, 1 = korona), gładka interpolacja punktów kontrolnych
    static readonly float[] PT = { 0f, 0.06f, 0.16f, 0.3f, 0.45f, 0.6f, 0.72f, 0.82f, 0.9f, 0.96f, 1f };
    static readonly float[] PW = { 0.1f, 0.2f, 0.34f, 0.55f, 0.86f, 1f, 0.9f, 0.62f, 0.6f, 0.5f, 0.2f };
    float HalfW(float t)
    {
        t = Clamp(t, 0, 1);
        int i = 0; while (i < PT.Length - 2 && t > PT[i + 1]) i++;
        float u = (t - PT[i]) / (PT[i + 1] - PT[i]), s = u * u * (3 - 2 * u);
        return Lerp(PW[i], PW[i + 1], s) * Wd / 2;
    }

    static readonly Color Body = Rgb(165, 165, 140, 0.34f), Edge = Rgb(60, 60, 45, 0.55f), GutC = Rgb(110, 130, 60, 0.5f);
    readonly float[] _ox = new float[80], _oy = new float[80];

    public override void Draw(Painter p, View v)
    {
        float oldA = p.Alpha; p.Alpha = 1 - Dig * 0.9f;
        p.Local(X, Y, A, 1 - Dig * 0.3f);
        float L = Length, lw = Math.Max(v.Px, Wd * 0.012f);
        // lekkie wygięcie ciała przy pełzaniu (pijawkowaty ruch)
        float bend = MathF.Sin(W.T * 2.4f + Id) * 0.04f * (1 - Ext) * Wd;
        const int N = 40;
        for (int i = 0; i < N; i++)
        {
            float t = (float)i / (N - 1), x = -L / 2 + L * t, hw = HalfW(t), yc = bend * MathF.Sin(t * PI);
            _ox[i] = x; _oy[i] = yc - hw; _ox[2 * N - 1 - i] = x; _oy[2 * N - 1 - i] = yc + hw;
        }
        int ns = Painter.SmoothClosed(_ox, _oy, 2 * N, p.Shape, 2);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, ns);
        p.SoftStroke(sh, true, Wd * 0.12f, Rgb(255, 255, 240, 0.26f));
        Span<float> ts = stackalloc float[] { 0f, 0.7f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(190, 190, 165, 0.26f), Body, Rgb(130, 130, 105, 0.45f) };
        p.FillRadial(sh, 0, 0, ts, cs);
        // pseudosegmenty stopy i tułowia: łukowate fałdy kutykuli (teleskopowo chowane przy skurczu)
        for (int k = 0; k < 6; k++)
        {
            float t = 0.07f + k * 0.055f * (0.6f + 0.4f * Ext), x = -L / 2 + L * t, hw = HalfW(t) * 0.95f;
            p.Arc(x - hw * 0.35f, 0, hw * 1.05f, -1.2f, 1.2f, lw * 0.9f, Rgb(70, 70, 52, 0.35f));
        }
        // stopa: dwie ostrogi i palce
        float fx = -L / 2;
        p.Line(fx + L * 0.03f, -Wd * 0.05f, fx - Wd * 0.05f, -Wd * 0.1f, lw * 1.4f, Edge, true);
        p.Line(fx + L * 0.03f, Wd * 0.05f, fx - Wd * 0.05f, Wd * 0.1f, lw * 1.4f, Edge, true);
        p.Line(fx + L * 0.12f, -HalfW(0.12f) * 0.9f, fx + L * 0.09f, -HalfW(0.12f) * 1.25f, lw * 1.2f, Edge, true);
        p.Line(fx + L * 0.12f, HalfW(0.12f) * 0.9f, fx + L * 0.09f, HalfW(0.12f) * 1.25f, lw * 1.2f, Edge, true);
        // żołądek (syncytium) z pokarmem i jelito
        float gx = -L * 0.02f;
        p.FillEllipse(gx, bend * 0.8f, L * 0.16f, Wd * 0.25f, 0, Mul(GutC, 0.35f + Math.Min(0.6f, Gut)));
        for (int k = 0; k < 14; k++) p.Dot(gx + MathF.Cos(k * 2.4f + Id) * L * 0.12f, bend * 0.8f + MathF.Sin(k * 1.7f + Id) * Wd * 0.17f, Wd * 0.025f, Rgb(80, 100, 40, 0.3f + 0.5f * Math.Min(1, Gut)));
        p.Line(gx - L * 0.16f, 0, -L * 0.3f, 0, Wd * 0.05f, Rgb(120, 125, 90, 0.35f), true);
        // pęcherz tętniący (protonefrydia) przy końcu jelita
        float bl = 0.7f + 0.3f * MathF.Sin(W.T * 1.5f + Id);
        p.FillCircle(-L * 0.3f, Wd * 0.06f, Wd * 0.07f * bl, Rgb(220, 228, 215, 0.4f));
        // mastaks – szczęki (trophi) rytmicznie mielą
        float jaw = 0.5f + 0.5f * MathF.Sin(Mph), mx = L * 0.24f;
        p.FillEllipse(mx, 0, Wd * 0.17f, Wd * 0.14f, 0, Rgb(125, 118, 90, 0.5f));
        p.Ellipse(mx, 0, Wd * 0.17f, Wd * 0.14f, 0, lw, Rgb(70, 65, 45, 0.5f));
        p.Arc(mx + Wd * 0.02f, -Wd * 0.02f - jaw * Wd * 0.03f, Wd * 0.08f, 0.2f, PI - 0.2f, lw * 1.6f, Rgb(40, 38, 28, 0.75f), true);
        p.Arc(mx + Wd * 0.02f, Wd * 0.02f + jaw * Wd * 0.03f, Wd * 0.08f, PI + 0.2f, TAU - 0.2f, lw * 1.6f, Rgb(40, 38, 28, 0.75f), true);
        for (int k = -2; k <= 2; k++) p.Seg(mx - Wd * 0.03f, k * Wd * 0.018f, mx + Wd * 0.05f, k * Wd * 0.018f * (1 + jaw * 0.4f), lw * 0.6f, Rgb(40, 38, 28, 0.5f));
        // ryjek z dwiema czerwonymi plamkami ocznymi
        float rx = L * 0.36f;
        p.FillEllipse(rx, 0, Wd * 0.1f, Wd * 0.07f, 0, Rgb(170, 170, 145, 0.35f));
        p.FillCircle(rx - Wd * 0.02f, -Wd * 0.05f, Wd * 0.025f, Rgb(200, 45, 35, 0.85f));
        p.FillCircle(rx - Wd * 0.02f, Wd * 0.05f, Wd * 0.025f, Rgb(200, 45, 35, 0.85f));
        p.Stroke(sh, true, lw, Edge);
        // korona: dwa płaty z wieńcami rzęsek – fala metachroniczna daje złudzenie kręcących się kół
        if (Corona > 0.05f)
        {
            float cr = Wd * 0.3f * Corona, cx = L / 2 + cr * 0.45f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float cy = sd * Wd * 0.25f * Corona;
                p.FillCircle(cx, cy, cr, Rgb(185, 188, 165, 0.28f * Corona));
                p.Circle(cx, cy, cr, lw, Mul(Edge, 0.8f * Corona));
                p.SoftStroke(new ReadOnlySpan<Vector2>(p.Buf2, 0, p.CirclePts(cx, cy, cr * 1.18f, p.Buf2, 40)), true, cr * 0.25f, Rgb(80, 82, 65, 0.12f * Corona));
                const int NC = 44;
                for (int k = 0; k < NC; k++)
                {
                    float t = k * TAU / NC, wave = 0.5f + 0.5f * MathF.Sin(k * 0.9f - Cph * 3 * sd);
                    float a = t + 0.5f * wave * sd, r0 = cr, r1 = cr + Wd * (0.05f + 0.07f * wave) * Corona;
                    p.Seg(cx + MathF.Cos(t) * r0, cy + MathF.Sin(t) * r0, cx + MathF.Cos(a) * r1, cy + MathF.Sin(a) * r1, Math.Max(v.Px * 0.8f, Wd * 0.01f), Rgb(55, 58, 45, (0.25f + 0.35f * wave) * Corona));
                }
            }
        }
        p.Alpha = oldA;
    }
}

/// <summary>
/// Acanthamoeba (trofozoit ~15–35 µm): kolczaste akantopodia, jądro z dużym ciemnym jąderkiem,
/// wodniczka tętniąca; pożera bakterie przez fagocytozę. Pełza powoli (~0,5 µm/s).
/// </summary>
public sealed class Acanth : Ent
{
    const int NN = 56;
    public float Rb;
    readonly float[] _rr = new float[NN];
    sealed class Spike { public float Ang, Len, Tgt; }
    readonly List<Spike> _spikes = new();
    sealed class Vac { public Ent O; public bool Cup = true; public float St, Lx, Ly, Sx, Sy, Tx, Ty; }
    readonly List<Vac> _vac = new();
    float _dir, _turnT, _cv, _seed;
    readonly Vector3[] _gran = new Vector3[60];

    public Acanth(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.Acanth; Layer = 2; Soft = 0.55f; Latin = "Acanthamoeba castellanii"; Rb = Rand(10, 14); Mass = 20; R = Rb * 1.8f;
        Mem = new Membrane(NN) { MaxDef = 3, Stiff = 8, Damping = 0.6f }; MemK = 4; MemS = 0.5f; _dir = Rand(0, TAU); _seed = Rand(0, 99);
        for (int i = 0; i < 12; i++) _spikes.Add(new Spike { Ang = Rand(0, TAU), Len = Rand(0, 5), Tgt = Rand(2, 6) });
        for (int i = 0; i < _gran.Length; i++) { float t = Rand(0, TAU), r = MathF.Sqrt(R01()) * 0.7f; _gran[i] = new Vector3(MathF.Cos(t) * r, MathF.Sin(t) * r, Chance(0.5f) ? 1 : 0); }
        Name = "Acanthamoeba";
        Compute(); Mem.Follow(x, y, 0, 1, 1);
    }
    void Compute()
    {
        float t = W.T;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN;
            float r = Rb * (1 + 0.12f * Noise1(th * 2 + t * 0.2f + _seed) + 0.18f * Gauss(WrapA(th - _dir), 0.8f));
            foreach (var s in _spikes) r += s.Len * Gauss(WrapA(th - s.Ang), 0.045f);
            _rr[i] = r;
        }
        foreach (var f in _vac)
        {
            if (!f.Cup) continue;
            float d = Hypot(f.Lx, f.Ly), ap = MathF.Atan2(f.Ly, f.Lx), need = d + 1.4f;
            for (int i = 0; i < NN; i++) { float g = Gauss(WrapA(i * TAU / NN - ap), 0.35f); if (g > 0.01f) _rr[i] = Math.Max(_rr[i], Lerp(_rr[i], need, g)); }
        }
        Mem.Radial(_rr);
    }
    public override void UpdatePrim() => SetCircle(Rb * 0.9f);
    public override bool Collides(Ent o) => !((o is EColi e && (e.State == St.Cup || e.State == St.Vac)) || (o is Bdello b && (b.State == St.Cup || b.State == St.Vac)));
    public override void Step(float dt, float gdt)
    {
        _turnT -= gdt;
        var prey = W.Near(X, Y, 30, e => (e is EColi c && c.CanHost()) || (e is Bdello b && b.State == St.Swim));
        if (prey != null) _dir = Turn(_dir, MathF.Atan2(prey.Y - Y, prey.X - X), 0.5f * gdt);
        else if (_turnT <= 0) { _dir += RandN() * 1.2f; _turnT = Rand(4, 10); }
        float v = 0.8f;
        Vx = MathF.Cos(_dir) * v; Vy = MathF.Sin(_dir) * v;
        foreach (var s in _spikes)
        {
            s.Len += Clamp(s.Tgt - s.Len, -3 * gdt, 3 * gdt);
            if (MathF.Abs(s.Len - s.Tgt) < 0.1f) { s.Tgt = Chance(0.5f) ? 0 : Rand(2, 6.5f); if (s.Len < 0.2f) s.Ang = Rand(0, TAU); }
        }
        _cv = (_cv + gdt / 6) % 1;
        if (_vac.Count < 6)
            foreach (var o in Nb)
            {
                bool ok = (o is EColi c && c.CanHost()) || (o is Bdello b && b.State == St.Swim);
                if (!ok) continue;
                float lx = o.X - X, ly = o.Y - Y, d = Hypot(lx, ly);
                if (d > RadialUtil.Interp(_rr, NN, MathF.Atan2(ly, lx)) + 0.6f) continue;
                o.State = St.Cup; o.Solid = false; o.Holder = this;
                float ang = MathF.Atan2(ly, lx), rin = Rb * Rand(0.2f, 0.5f);
                _vac.Add(new Vac { O = o, Sx = lx, Sy = ly, Lx = lx, Ly = ly, Tx = MathF.Cos(ang) * rin, Ty = MathF.Sin(ang) * rin });
                W.Sfx("gulp", X, Y, 0.5f);
                if (o.IsPlayer) W.Log("Acanthamoeba Cię pochłania! Fagocytoza – nie ma ucieczki z wodniczki.", "bad");
                break;
            }
        for (int k = _vac.Count - 1; k >= 0; k--)
        {
            var f = _vac[k]; var o = f.O;
            if (!o.Alive) { _vac.RemoveAt(k); continue; }
            f.St += gdt;
            if (f.Cup)
            {
                float q = Smooth(f.St / 1.5f);
                f.Lx = Lerp(f.Sx, f.Tx, q); f.Ly = Lerp(f.Sy, f.Ty, q);
                if (f.St >= 1.5f) { f.Cup = false; f.St = 0; o.State = St.Vac; }
            }
            else
            {
                o.Dig = Clamp(f.St / 25, 0, 1);
                if (f.St >= 25) { o.Alive = false; _vac.RemoveAt(k); continue; }
            }
            o.X = X + f.Lx; o.Y = Y + f.Ly;
        }
        Compute();
    }
    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape, 2);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 1.6f, Rgb(255, 255, 240, 0.3f));
        Span<float> ts = stackalloc float[] { 0f, 0.7f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(105, 110, 98, 0.55f), Rgb(125, 130, 118, 0.45f), Rgb(175, 180, 168, 0.3f) };
        p.FillRadial(sh, X, Y, ts, cs);
        p.Stroke(sh, true, 0.12f, Rgb(40, 44, 38, 0.75f));
        p.Local(X, Y, 0);
        foreach (var g in _gran) p.Dot(g.X * Rb, g.Y * Rb, 0.22f, g.Z > 0 ? Rgb(35, 40, 35, 0.5f) : Rgb(235, 240, 225, 0.55f));
        float nx = -Rb * 0.15f, ny = Rb * 0.1f;
        p.FillCircle(nx, ny, Rb * 0.28f, Rgb(185, 190, 175, 0.55f));
        p.Circle(nx, ny, Rb * 0.28f, 0.1f, Rgb(50, 55, 48, 0.5f));
        p.FillCircle(nx, ny, Rb * 0.12f, Rgb(40, 42, 40, 0.8f));
        float vr = _cv < 0.9f ? MathF.Sqrt(_cv / 0.9f) * Rb * 0.25f : (1 - (_cv - 0.9f) / 0.1f) * Rb * 0.25f;
        if (vr > 0.2f) { p.FillCircle(Rb * 0.45f, -Rb * 0.3f, vr, Rgb(215, 222, 210, 0.5f)); p.Circle(Rb * 0.45f, -Rb * 0.3f, vr, 0.08f, Rgb(250, 255, 240, 0.6f)); }
        foreach (var f in _vac) p.Circle(f.Lx, f.Ly, f.O.Length * 0.5f + 0.4f, 0.1f, f.Cup ? Rgb(235, 240, 220, 0.3f) : Rgb(235, 240, 220, 0.55f));
    }
}

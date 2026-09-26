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
        Edible = true; Food = 3; Name = "wrotek (Philodina)"; State = St.Free; St0 = Rand(4, 12); _dir = a;
    }
    public override void UpdatePrim() => SetCapsule(Length * 0.42f, Wd * 0.45f);
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

    static readonly Color Body = Rgb(150, 150, 128, 0.42f), Edge = Rgb(50, 50, 38, 0.7f), GutC = Rgb(105, 125, 60, 0.55f);

    float HalfW(float t)
    {
        // t: 0 = palce stopy, 1 = korona
        float f = t < 0.12f ? 0.18f + t / 0.12f * 0.18f : t < 0.35f ? 0.36f + (t - 0.12f) / 0.23f * 0.4f : t < 0.72f ? 0.76f + 0.24f * MathF.Sin((t - 0.35f) / 0.37f * PI) : t < 0.86f ? 0.85f - (t - 0.72f) / 0.14f * 0.33f : 0.52f + (t - 0.86f) / 0.14f * 0.2f;
        return f * Wd / 2;
    }

    public override void Draw(Painter p, View v)
    {
        float oldA = p.Alpha; p.Alpha = 1 - Dig * 0.9f;
        p.Local(X, Y, A, 1 - Dig * 0.3f);
        float L = Length; const int N = 36;
        Span<Vector2> sh = stackalloc Vector2[N * 2];
        for (int i = 0; i < N; i++)
        {
            float t = (float)i / (N - 1), x = -L / 2 + L * t;
            float ridge = 1 + 0.05f * MathF.Sin(t * 40) * (t < 0.4f ? 1 : 0);
            float hw = HalfW(t) * ridge;
            sh[i] = new Vector2(x, -hw); sh[N * 2 - 1 - i] = new Vector2(x, hw);
        }
        p.SoftStroke(sh, true, 9, Rgb(255, 255, 240, 0.28f));
        p.FillFan(sh, 0, 0, Body);
        // pseudosegmenty (teleskopowe)
        for (int k = 1; k < 6; k++) { float t = k * 0.06f + 0.05f, x = -L / 2 + L * t, hw = HalfW(t); p.Line(x, -hw * 0.95f, x + 2, hw * 0.95f, 0.9f, Rgb(60, 60, 45, 0.35f)); }
        // żołądek z pokarmem
        p.FillEllipse(-L * 0.02f, 0, L * 0.17f, Wd * 0.27f, 0, Mul(GutC, 0.4f + Math.Min(0.6f, Gut)));
        // mastaks (szczęki) – rytmiczne mielenie
        float jaw = 0.5f + 0.5f * MathF.Sin(Mph);
        float mx = L * 0.24f;
        p.FillEllipse(mx, 0, Wd * 0.16f, Wd * 0.14f, 0, Rgb(120, 115, 90, 0.55f));
        p.Arc(mx - 2, -2 - jaw * 3, Wd * 0.09f, -0.3f, PI + 0.3f, 1.6f, Rgb(40, 38, 28, 0.7f), true);
        p.Arc(mx - 2, 2 + jaw * 3, Wd * 0.09f, PI - 0.3f, TAU + 0.3f, 1.6f, Rgb(40, 38, 28, 0.7f), true);
        // oczka (rostrum)
        p.FillCircle(L * 0.36f, -Wd * 0.08f, 2.2f, Rgb(200, 50, 40, 0.8f));
        p.FillCircle(L * 0.36f, Wd * 0.08f, 2.2f, Rgb(200, 50, 40, 0.8f));
        p.Stroke(sh, true, 1.1f, Edge);
        // palce stopy
        p.Line(-L / 2, -2, -L / 2 - 10, -6, 2, Edge, true); p.Line(-L / 2, 2, -L / 2 - 10, 6, 2, Edge, true);
        // korona – dwa "koła" rzęsek
        if (Corona > 0.05f)
        {
            float cr = Wd * 0.3f * Corona, cx = L / 2 + cr * 0.6f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float cy = sd * Wd * 0.26f * Corona;
                p.FillCircle(cx, cy, cr, Rgb(170, 175, 150, 0.35f * Corona));
                p.Circle(cx, cy, cr, 0.9f, Mul(Edge, Corona));
                const int NC = 22;
                for (int k = 0; k < NC; k++)
                {
                    float t = k * TAU / NC + Cph * sd, wave = 0.5f + 0.5f * MathF.Sin(k * 1.4f - Cph * 3);
                    float r0 = cr, r1 = cr + (6 + 6 * wave) * Corona;
                    p.Seg(cx + MathF.Cos(t) * r0, cy + MathF.Sin(t) * r0, cx + MathF.Cos(t) * r1, cy + MathF.Sin(t) * r1, Math.Max(1, v.Px), Rgb(55, 58, 45, 0.55f * Corona));
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
        Kind = Kind.Acanth; Layer = 2; Rb = Rand(10, 14); Mass = 20; R = Rb * 1.8f;
        Mem = new Membrane(NN) { MaxDef = 2 }; MemK = 4; MemS = 0.5f; _dir = Rand(0, TAU); _seed = Rand(0, 99);
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
                o.State = St.Cup; o.Solid = false;
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

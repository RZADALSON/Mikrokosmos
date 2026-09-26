namespace Mikro;

/// <summary>Bazowa klasa pokarmu ameby (glony, kłaczki).</summary>
public abstract class Food : Ent
{
    public float Rr;
    public override float Radius => Rr;
    protected Food(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Layer = 3; State = St.Free; Edible = true; D = 0.02f; Dr = 0.0005f;
    }
    public override bool Collides(Ent o) => State == St.Free;
    public override void Draw(Painter p, View v)
    {
        float oldA = p.Alpha; p.Alpha = 1 - Dig * 0.9f;
        p.Local(X, Y, A, 1 - Dig * 0.35f);
        DrawBody(p, v);
        p.Alpha = oldA;
    }
    protected abstract void DrawBody(Painter p, View v);
}

/// <summary>Navicula – okrzemka pierzasta; ślizga się dzięki szczelinie (rafie) ~4–12 µm/s.</summary>
public sealed class Diatom : Food
{
    public float L, Wd, Glide, Dir, RevT;
    readonly Vector2[] _shape, _chl1, _chl2;
    public override float Length => L;
    public Diatom(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Diatom; Name = "okrzemka Navicula";
        L = Rand(45, 85); Wd = L * Rand(0.2f, 0.25f); Rr = Wd / 2; R = L / 2 + 4; Mass = 5; Food = 1;
        Glide = Rand(4, 12); Dir = Chance(0.5f) ? 1 : -1; RevT = ExpRand(10);
        _shape = new Vector2[40];
        for (int i = 0; i < 40; i++) { float t = i * TAU / 40, c = MathF.Cos(t); _shape[i] = new Vector2(c * L / 2, MathF.Sin(t) * Wd / 2 * MathF.Pow(1 - MathF.Pow(MathF.Abs(c), 2.2f), 0.35f)); }
        _chl1 = new Vector2[24]; _chl2 = new Vector2[24];
        for (int i = 0; i < 24; i++) { float t = i * TAU / 24; _chl1[i] = new Vector2(MathF.Cos(t) * L * 0.36f, Wd * 0.2f + MathF.Sin(t) * Wd * 0.17f); _chl2[i] = new Vector2(_chl1[i].X, -_chl1[i].Y); }
    }
    public override void UpdatePrim() => SetCapsule(L / 2 - Wd / 2, Wd / 2);
    public override void Step(float dt, float gdt)
    {
        if (State != St.Free) { Vx = Vy = 0; return; }
        RevT -= dt; if (RevT <= 0) { Dir *= -1; RevT = ExpRand(10); }
        A += RandN() * 0.004f * MathF.Sqrt(dt * 100);
        Vx = MathF.Cos(A) * Glide * Dir; Vy = MathF.Sin(A) * Glide * Dir;
    }
    protected override void DrawBody(Painter p, View v)
    {
        p.SoftStroke(_shape, true, 5, Rgb(255, 255, 240, 0.4f));
        p.FillFan(_shape, 0, 0, Rgb(214, 208, 172, 0.35f));
        p.FillFan(_chl1, 0, Wd * 0.2f, Rgb(150, 108, 38, 0.62f));
        p.FillFan(_chl2, 0, -Wd * 0.2f, Rgb(150, 108, 38, 0.62f));
        var sc = Rgb(70, 64, 44, 0.35f);
        for (float x = -L * 0.44f; x < L * 0.44f; x += 1.3f)
        {
            float h = Wd / 2 * MathF.Pow(1 - MathF.Pow(MathF.Abs(x) / (L / 2), 2.2f), 0.35f) * 0.9f;
            p.Seg(x, -h, x, -1.2f, 0.35f, sc); p.Seg(x, 1.2f, x, h, 0.35f, sc);
        }
        p.Line(-L * 0.46f, 0, -2, 0, 0.6f, Rgb(50, 46, 30, 0.7f)); p.Line(2, 0, L * 0.46f, 0, 0.6f, Rgb(50, 46, 30, 0.7f));
        p.FillCircle(0, 0, 1.6f, Rgb(245, 245, 225, 0.6f));
        p.FillCircle(L * 0.22f, Wd * 0.05f, 1.5f, Rgb(255, 250, 210, 0.7f));
        p.FillCircle(-L * 0.22f, -Wd * 0.05f, 1.2f, Rgb(255, 250, 210, 0.7f));
        p.Stroke(_shape, true, 0.7f, Rgb(40, 36, 24, 0.8f));
    }
}

/// <summary>Cosmarium – desmidia z dwóch półkomórek, głęboka zatoka, pirenoidy.</summary>
public sealed class Cosmarium : Food
{
    readonly float S;
    readonly Vector2[] _h1, _h2; readonly Vector3[] _dots;
    public Cosmarium(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Cosm; Name = "desmidia Cosmarium";
        S = Rand(40, 60); Rr = S * 0.45f; R = S * 0.6f; Mass = 6; Food = 1.2f;
        _h1 = Half(1); _h2 = Half(-1);
        _dots = new Vector3[24];
        for (int k = 0; k < 24; k++) _dots[k] = new Vector3(Rand(-S * 0.3f, S * 0.3f), (k < 12 ? 1 : -1) * S * Rand(0.12f, 0.38f), 0);
    }
    Vector2[] Half(int sd)
    {
        var a = new Vector2[24];
        float a0 = sd > 0 ? -0.15f : PI - 0.15f, a1 = sd > 0 ? PI + 0.15f : TAU + 0.15f;
        for (int i = 0; i < 24; i++) { float t = a0 + (a1 - a0) * i / 23; a[i] = new Vector2(MathF.Cos(t) * S * 0.44f, sd * S * 0.24f + MathF.Sin(t) * S * 0.25f); }
        return a;
    }
    public override void UpdatePrim() => SetCircle(Rr);
    protected override void DrawBody(Painter p, View v)
    {
        foreach (var (h, sd) in new[] { (_h1, 1), (_h2, -1) })
        {
            p.SoftStroke(h, true, 4, Rgb(255, 255, 240, 0.35f));
            p.FillFan(h, 0, sd * S * 0.3f, Rgb(70, 140, 58));
            p.Stroke(h, true, 0.8f, Rgb(20, 50, 15, 0.75f));
            p.FillCircle(0, sd * S * 0.25f, S * 0.07f, Rgb(190, 225, 160, 0.75f));
        }
        foreach (var d in _dots) p.Dot(d.X, d.Y, 1, Rgb(30, 70, 25, 0.4f));
        p.FillEllipse(0, 0, S * 0.12f, S * 0.05f, 0, Rgb(210, 220, 190, 0.6f));
    }
}

/// <summary>Closterium – półksiężyc; w wakuolach końcowych kryształy gipsu w ruchach Browna.</summary>
public sealed class Closterium : Food
{
    public float L, Wd, Bend;
    readonly Vector2[] _shape, _axis; readonly Vector2[] _cr = new Vector2[12];
    public override float Length => L;
    public Closterium(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Clost; Name = "desmidia Closterium";
        L = Rand(170, 260); Wd = L * 0.11f; Rr = Wd / 2; R = L / 2 + 5; Mass = 12; Food = 2.2f; Bend = L * 0.14f;
        _shape = new Vector2[62];
        for (int i = 0; i <= 30; i++) { float t = -1 + 2f * i / 30, x2 = t * L / 2, y2 = Bend * t * t - Bend * 0.5f, w2 = Wd / 2 * MathF.Sqrt(1 - t * t * 0.92f); _shape[i] = new Vector2(x2, y2 - w2); _shape[61 - i] = new Vector2(x2, y2 + w2); }
        _axis = new Vector2[31];
        for (int i = 0; i <= 30; i++) { float t = -0.85f + 1.7f * i / 30; _axis[i] = new Vector2(t * L / 2, Bend * t * t - Bend * 0.5f); }
    }
    public override void UpdatePrim() => SetCapsule(L * 0.36f, Wd * 0.45f);
    public override void Step(float dt, float gdt)
    {
        float s = MathF.Sqrt(gdt) * 1.6f;
        for (int i = 0; i < 12; i++) _cr[i] = new Vector2(Clamp(_cr[i].X + RandN() * s, -2.2f, 2.2f), Clamp(_cr[i].Y + RandN() * s, -2.2f, 2.2f));
    }
    float Cy(float t) => Bend * t * t - Bend * 0.5f;
    protected override void DrawBody(Painter p, View v)
    {
        p.SoftStroke(_shape, true, 5, Rgb(255, 255, 240, 0.35f));
        p.FillFan(_shape, 0, -Bend * 0.5f + Wd * 0.1f, Rgb(76, 146, 62));
        p.Stroke(_shape, true, 0.8f, Rgb(25, 60, 20, 0.8f));
        p.Stroke(_axis, false, 0.6f, Rgb(40, 90, 30, 0.5f));
        for (int k = -3; k <= 3; k++) { if (k == 0) continue; float t = k / 4.2f; p.FillCircle(t * L / 2, Cy(t), Wd * 0.13f, Rgb(200, 230, 170, 0.75f)); }
        foreach (int e in new[] { -1, 1 }) { float t = e * 0.9f; p.FillCircle(t * L / 2, Cy(t), Wd * 0.28f, Rgb(210, 225, 200, 0.8f)); }
        p.FillEllipse(0, -Bend * 0.5f, Wd * 0.3f, Wd * 0.38f, 0, Rgb(220, 230, 210, 0.7f));
        if (Dig < 0.5f)
            for (int i = 0; i < 12; i++) { float t = (i < 6 ? 1 : -1) * 0.9f; p.Rect(t * L / 2 + _cr[i].X - 0.45f, Cy(t) + _cr[i].Y - 0.45f, 0.9f, 0.9f, Rgb(40, 40, 30, 0.9f)); }
    }
}

/// <summary>Kłaczek bakterii / detrytusu.</summary>
public sealed class Floc : Food
{
    readonly Vector2[] _shape = new Vector2[12]; readonly Vector3[] _rods = new Vector3[90];
    public Floc(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Floc; Name = "kłaczek bakterii";
        Rr = Rand(8, 16); R = Rr + 3; Mass = 1.5f; Food = 0.45f;
        for (int i = 0; i < 12; i++) { float t = i * TAU / 12, q = Rr * Rand(0.6f, 1.1f); _shape[i] = new Vector2(MathF.Cos(t) * q, MathF.Sin(t) * q); }
        for (int k = 0; k < 90; k++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * Rr * 0.9f; _rods[k] = new Vector3(MathF.Cos(t) * q, MathF.Sin(t) * q, Rand(0, TAU)); }
    }
    public override void UpdatePrim() => SetCircle(Rr * 0.8f);
    protected override void DrawBody(Painter p, View v)
    {
        p.FillFan(_shape, 0, 0, Rgb(120, 115, 90, 0.3f));
        var c = Rgb(35, 38, 30, 0.75f);
        foreach (var r in _rods) { float dx = MathF.Cos(r.Z), dy = MathF.Sin(r.Z); p.Seg(r.X - dx, r.Y - dy, r.X + dx, r.Y + dy, 0.8f, c); }
    }
}

/// <summary>
/// Amoeba proteus (200–800 µm): pseudopodia, prąd cytoplazmy do 10 µm/s (centroid ~1–2,5 µm/s),
/// ektoplazma/endoplazma, jądro dyskowate, wodniczka tętniąca, kryształy, wodniczki pokarmowe.
/// Pole powierzchni (objętość) zachowane.
/// </summary>
public sealed class Amoeba : Ent
{
    const int NN = 96;
    public float Rb, A0, Vmax = 2.5f, Seed, Energy, Div, DivA, AiT, AiA, Hunger;
    public bool Dividing;
    readonly float[] _rr = new float[NN];
    sealed class Pod { public float Ang, Len, Tgt, W; }
    List<Pod> _pods = new();
    public sealed class Meal { public Ent O; public bool Cup = true; public float St, Sx, Sy, Lx, Ly, Tx, Ty; }
    public readonly List<Meal> Meals = new();
    const int G = 460;
    readonly float[] _gth = new float[G], _grho = new float[G], _gsz = new float[G]; readonly byte[] _gty = new byte[G];
    float _nucX, _nucY, _cvX, _cvY, _cvPh;

    public Amoeba(World w, float x, float y, float rb = 150) : base(w, x, y, 0)
    {
        Kind = Kind.Amoeba; Layer = 2;
        Rb = rb; A0 = PI * Rb * Rb;
        Mem = new Membrane(NN) { MaxDef = 26 }; MemK = 3; MemS = 0.35f;
        Mass = 70; Seed = Rand(0, 99); AiA = Rand(0, TAU);
        for (int i = 0; i < G; i++)
        {
            _gth[i] = Rand(0, TAU); _grho[i] = MathF.Sqrt(R01()) * 0.82f;
            _gty[i] = (byte)(R01() < 0.6f ? 1 : R01() < 0.9f ? 2 : 3);
            _gsz[i] = _gty[i] == 3 ? Rand(1.4f, 2.2f) : Rand(0.8f, 1.7f);
        }
        _nucX = Rand(-20, 20); _nucY = Rand(-20, 20); _cvPh = Rand(0, 1);
        for (int k = 0; k < 3; k++) _pods.Add(new Pod { Ang = Rand(0, TAU), Len = Rand(30, 90), Tgt = Rand(20, 80), W = Rand(0.3f, 0.5f) });
        ComputeRest(); Mem.Follow(x, y, 0, 1, 1);
    }
    void ComputeRest()
    {
        float t = W.T, area = 0;
        float rearA = MathF.Atan2(-Vy, -Vx); bool moving = Hypot(Vx, Vy) > 0.3f;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, r = Rb * 0.8f;
            foreach (var p in _pods) r += p.Len * Gauss(WrapA(th - p.Ang), p.W);
            r += Rb * 0.035f * Noise1(th * 3 + t * 0.25f + Seed);
            if (moving) r += 4 * Gauss(WrapA(th - rearA), 0.45f) * MathF.Sin(th * 22 + t);
            if (Div > 0) { float c = MathF.Cos(th - DivA); r *= (1 + 0.45f * Div * c * c) * (1 - 0.72f * Div * MathF.Exp(-(c * c) / 0.08f)); }
            _rr[i] = r; area += 0.5f * r * r * (TAU / NN);
        }
        float k = MathF.Sqrt(A0 / area);
        for (int i = 0; i < NN; i++) _rr[i] *= k;
        foreach (var f in Meals)
        {
            if (!f.Cup) continue;
            float d = Hypot(f.Lx, f.Ly), ap = MathF.Atan2(f.Ly, f.Lx), pr = f.O.Radius;
            float need = d + pr + 8, wd = Math.Max(0.12f, (pr + 25) / Math.Max(d, 1));
            for (int i = 0; i < NN; i++) { float g = Gauss(WrapA(i * TAU / NN - ap), wd); if (g > 0.01f) _rr[i] = Math.Max(_rr[i], Lerp(_rr[i], need, g)); }
        }
        Mem.Radial(_rr);
        float mx = 0; for (int i = 0; i < NN; i++) mx = Math.Max(mx, _rr[i]);
        R = mx + 10;
    }
    public override void UpdatePrim() => SetCircle(MathF.Sqrt(A0 / PI) * 0.62f);
    public override bool Collides(Ent o) => o.State != St.Cup && o.State != St.Vac;

    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        if (!W.Preview)
        {
            Hunger = Math.Min(1.2f, Hunger + gdt / 220);
            if (Hunger > 0.6f) A0 *= 1 - 0.012f * gdt * (Hunger - 0.6f) * 2.5f;
            if (A0 < PI * 85 * 85) { Starve(); return; }
        }
        bool on; float ta, mag;
        if (IsPlayer) { var I = W.Input; on = I.Mag > 0.05f; ta = I.Ang; mag = I.Mag; }
        else
        {
            AiT -= dt; if (AiT <= 0) { AiA += RandN() * 1.2f; AiT = ExpRand(60); }
            var f = W.Near(X, Y, Rb * 2.5f, e => e.Edible && e.State == St.Free);
            on = true; ta = f != null ? MathF.Atan2(f.Y - Y, f.X - X) : AiA; mag = 0.7f;
        }
        if (Dividing) on = false;
        Pod lead = null;
        if (on)
        {
            foreach (var p in _pods) if (MathF.Abs(WrapA(p.Ang - ta)) < 0.55f && (lead == null || p.Len > lead.Len)) lead = p;
            if (lead == null && _pods.Count < 7) { lead = new Pod { Ang = ta + RandN() * 0.15f, W = Rand(0.26f, 0.42f) }; _pods.Add(lead); }
        }
        foreach (var p in _pods)
        {
            if (p == lead) { p.Tgt = 170 * mag; p.Ang = Turn(p.Ang, ta, 0.03f * dt); }
            else if (on && MathF.Abs(WrapA(p.Ang - ta)) < 1.1f) p.Tgt = Math.Min(p.Tgt, 70);
            else if (on) p.Tgt = 0;
            else if (R01() < dt * 0.02f) p.Tgt = Rand(0, 90);
            const float rate = 9;
            p.Len += Clamp(p.Tgt - p.Len, -rate * dt, rate * dt);
        }
        if (!on && _pods.Count < 3 && R01() < dt * 0.05f) _pods.Add(new Pod { Ang = Rand(0, TAU), Tgt = Rand(30, 80), W = Rand(0.3f, 0.5f) });
        _pods.RemoveAll(p => p.Len <= 1 && p.Tgt <= 0);
        float sx = 0, sy = 0;
        foreach (var p in _pods) { float k = p.Len / 170; sx += MathF.Cos(p.Ang) * k; sy += MathF.Sin(p.Ang) * k; }
        float sm = Hypot(sx, sy);
        if (sm > 1) { sx /= sm; sy /= sm; }
        float full = Math.Min(0.5f, Meals.Count * 0.08f);
        Vx = sx * Vmax * (1 - full); Vy = sy * Vmax * (1 - full);
        // prąd cytoplazmy – ziarnistości płyną do czoła nibynóżki
        float flowA = lead != null ? lead.Ang : MathF.Atan2(sy, sx), flow = Math.Min(1, sm + (lead != null ? 0.5f : 0));
        float jr = MathF.Sqrt(gdt);
        for (int i = 0; i < G; i++)
        {
            float th = _gth[i], rho = _grho[i];
            if (flow > 0.05f)
            {
                float al = MathF.Cos(th - flowA);
                th += WrapA(flowA - th) * 0.35f * dt * flow;
                rho += (al > 0 ? 1 : -0.4f) * 0.05f * dt * flow;
                if (rho > 0.84f) { rho = Rand(0.05f, 0.35f); th = flowA + PI + RandN() * 0.8f; }
            }
            th += RandN() * 0.02f * jr; rho = Clamp(rho + RandN() * 0.01f * jr, 0.02f, 0.84f);
            _gth[i] = th; _grho[i] = rho;
        }
        _nucX = Lerp(_nucX, -sx * 25, Damp(0.02f, dt)); _nucY = Lerp(_nucY, -sy * 25, Damp(0.02f, dt));
        _cvPh += dt / 90; if (_cvPh >= 1) _cvPh = 0;
        float rearA = MathF.Atan2(-sy, -sx);
        _cvX = Lerp(_cvX, MathF.Cos(rearA) * Rb * 0.45f, Damp(0.05f, dt)); _cvY = Lerp(_cvY, MathF.Sin(rearA) * Rb * 0.45f, Damp(0.05f, dt));
        // kontakt z pokarmem
        foreach (var o in Nb)
        {
            if (!o.Edible || o.State != St.Free) continue;
            float lx = o.X - X, ly = o.Y - Y, d = Hypot(lx, ly);
            bool touch = d < RadialUtil.Interp(_rr, NN, MathF.Atan2(ly, lx)) + o.Radius * 0.2f + o.Length * 0.3f;
            if (touch && o is Rotifer rt) rt.Poke();
            if (d < RadialUtil.Interp(_rr, NN, MathF.Atan2(ly, lx)) + o.Radius * 0.2f && o.Length < Rb * 2.2f) Engulf(o, lx, ly);
        }
        // trawienie
        for (int k = Meals.Count - 1; k >= 0; k--)
        {
            var f = Meals[k]; var o = f.O;
            if (!o.Alive) { Meals.RemoveAt(k); continue; }
            f.St += dt;
            if (f.Cup)
            {
                float q = Smooth(f.St / 70);
                f.Lx = Lerp(f.Sx, f.Tx, q); f.Ly = Lerp(f.Sy, f.Ty, q);
                if (f.St >= 70) { f.Cup = false; f.St = 0; o.State = St.Vac; if (IsPlayer) W.Log($"Wodniczka pokarmowa zamknięta ({o.Name}). Lizosomy rozpoczynają trawienie.", "good"); }
            }
            else
            {
                float ang = MathF.Atan2(f.Ly, f.Lx), lim = RadialUtil.Interp(_rr, NN, ang) * 0.55f, d = Hypot(f.Lx, f.Ly);
                f.Lx += RandN() * 0.4f * MathF.Sqrt(dt) - Vx * dt * 0.3f; f.Ly += RandN() * 0.4f * MathF.Sqrt(dt) - Vy * dt * 0.3f;
                if (d > lim) { f.Lx *= lim / d; f.Ly *= lim / d; }
                o.Dig = Clamp(f.St / 600, 0, 1);
                if (f.St >= 600)
                {
                    o.Alive = false; Meals.RemoveAt(k); Energy += o.Food; Hunger = Math.Max(0, Hunger - o.Food * 0.35f);
                    if (IsPlayer || Lineage) { W.AddStat("eaten", 1); W.AddStat("biomass", o.Food); if (o is Rotifer) W.AddStat("rotifers", 1); }
                    W.Fx.Spill(X + _cvX, Y + _cvY, 8, new FX.SpillOpt { Speed = 30, R = 2, Life = 5, D = 3, Cols = new[] { Rgb(80, 70, 40, 0.6f), Rgb(50, 50, 35, 0.6f) } });
                    A0 += PI * 900 * o.Food;
                    continue;
                }
            }
            o.X = X + f.Lx; o.Y = Y + f.Ly;
        }
        if (!Dividing && Energy >= 5 && Meals.Count == 0 && !W.Preview) { Dividing = true; DivA = Rand(0, TAU); if (IsPlayer) W.Log("Mitoza! Ameba zaokrągla się i dzieli...", "good"); }
        if (Dividing) { Div = Math.Min(1, Div + bdt / 3600); if (Div >= 1) Split(); }
        ComputeRest();
    }
    void Starve()
    {
        W.Fx.Lyse(Mem, new FX.LyseOpt { Pieces = 7, Speed = 20, Life = 8, Lw = 1.3f, C = Rgb(40, 46, 40, 0.6f), Halo = Rgb(255, 255, 240, 0.2f), HasHalo = true, Curl = 0.4f, D = 2 });
        W.Sfx("pop", X, Y);
        if (IsPlayer) W.Log("Ameba zagłodzona – komórka skurczyła się i rozpadła.", "bad");
        foreach (var f in Meals) if (f.O.Alive) { f.O.State = St.Free; f.O.Solid = true; }
        Alive = false;
    }
    void Engulf(Ent o, float lx, float ly)
    {
        W.Sfx("gulp", X, Y, 0.7f);
        o.State = St.Cup; o.Solid = false;
        float ang = MathF.Atan2(ly, lx), rin = Rb * Rand(0.25f, 0.45f);
        Meals.Add(new Meal { O = o, Sx = lx, Sy = ly, Lx = lx, Ly = ly, Tx = MathF.Cos(ang) * rin, Ty = MathF.Sin(ang) * rin });
        if (IsPlayer) W.Log("Pseudopodia otaczają ofiarę (miseczka pokarmowa)...", "info");
    }
    void Split()
    {
        float c = MathF.Cos(DivA), s = MathF.Sin(DivA), off = Rb * 0.75f;
        var child = W.Add(new Amoeba(W, X - c * off, Y - s * off, Rb));
        child.A0 = A0 / 2; child.Lineage = true;
        A0 /= 2; X += c * off * 0.5f; Y += s * off * 0.5f;
        Div = 0; Dividing = false; Energy = 0;
        _pods = new List<Pod> { new Pod { Ang = DivA, Len = 40, Tgt = 40, W = 0.4f } };
        W.AddStat("pop", 1);
        if (IsPlayer) W.Log("Podział zakończony – powstała nowa ameba z Twojej linii.", "good");
    }
    public float EnergyFrac => Energy / 5f;

    static readonly Color GranD = Rgb(38, 44, 38, 0.55f), GranB = Rgb(232, 238, 222, 0.55f), Crys = Rgb(255, 255, 245, 0.85f);

    public override void Draw(Painter p, View v)
    {
        var m = Mem;
        int n = m.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 16, Rgb(255, 255, 240, 0.22f));
        p.FillFan(sh, X, Y, Rgb(128, 136, 120, 0.38f));
        // endoplazma (ziarnista) – przeskalowany obrys
        for (int i = 0; i < n; i++) p.Shape2[i] = new Vector2(X + (p.Shape[i].X - X) * 0.84f, Y + (p.Shape[i].Y - Y) * 0.84f);
        Span<float> ts = stackalloc float[] { 0f, 0.8f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(86, 94, 80, 0.2f), Rgb(86, 94, 80, 0.16f), Rgb(86, 94, 80, 0f) };
        p.FillRadial(new ReadOnlySpan<Vector2>(p.Shape2, 0, n), X, Y, ts, cs, false);
        p.Stroke(sh, true, 1.3f, Rgb(40, 46, 40, 0.55f));
        p.Local(X, Y, 0);
        p.FillEllipse(_nucX, _nucY, 23, 20, 0.4f, Rgb(158, 166, 146, 0.42f));
        p.Ellipse(_nucX, _nucY, 23, 20, 0.4f, 1.6f, Rgb(62, 68, 56, 0.55f));
        int ne = p.EllipsePts(_nucX, _nucY, 19, 16.5f, 0.4f, p.Buf2);
        p.Dashed(new ReadOnlySpan<Vector2>(p.Buf2, 0, ne), true, 2.5f, 1.2f, 1.6f, Rgb(70, 76, 64, 0.45f));
        if (Div > 0.35f)
        {
            float c = MathF.Cos(DivA), s = MathF.Sin(DivA), d = Div * 60;
            p.FillCircle(_nucX + c * d, _nucY + s * d, 17, Rgb(158, 166, 146, 0.42f));
            p.FillCircle(_nucX - c * d, _nucY - s * d, 17, Rgb(158, 166, 146, 0.42f));
        }
        float ph = _cvPh, vr = ph < 0.92f ? MathF.Sqrt(ph / 0.92f) * 24 : (1 - (ph - 0.92f) / 0.08f) * 24;
        if (vr > 1)
        {
            p.FillCircle(_cvX, _cvY, vr, Rgb(210, 218, 205, 0.42f));
            p.Circle(_cvX, _cvY, vr, 1.2f, Rgb(250, 255, 240, 0.55f));
            p.Circle(_cvX, _cvY, vr + 1.2f, 0.8f, Rgb(40, 46, 40, 0.3f));
        }
        foreach (var f in Meals)
        {
            float r = f.O.Radius * (f.Cup ? 1.05f : 1.15f) + 3;
            if (f.O.Length > 0) r = Math.Max(r, f.O.Length * 0.35f);
            if (!f.Cup) p.FillCircle(f.Lx, f.Ly, r, Rgb(200, 215, 190, 0.14f));
            p.Circle(f.Lx, f.Ly, r, 1.4f, f.Cup ? Rgb(235, 240, 220, 0.25f) : Rgb(235, 240, 220, 0.5f));
        }
        for (int i = 0; i < G; i++)
        {
            float r = RadialUtil.Interp(_rr, NN, _gth[i]) * _grho[i], s = _gsz[i];
            float x = MathF.Cos(_gth[i]) * r, y = MathF.Sin(_gth[i]) * r;
            if (_gty[i] == 1) p.Dot(x, y, s * 0.55f, GranD);
            else if (_gty[i] == 2) p.Dot(x, y, s * 0.55f, GranB);
            else p.Diamond(x, y, s * 0.6f, s, Crys);
        }
    }
}

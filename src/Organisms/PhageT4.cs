namespace Mikro;

public sealed class PhagePal
{
    public Color Head, Head2, Empty, Line, Sheath, Stripe, Fiber, Plate, Dna;
    public static readonly PhagePal Em = new()
    {
        Head = Rgb(222, 150, 72), Head2 = Rgb(120, 68, 24), Empty = Rgb(222, 160, 90, 0.18f), Line = Rgb(40, 20, 5, 0.95f), Sheath = Rgb(236, 186, 104),
        Stripe = Rgb(90, 50, 15, 0.8f), Fiber = Rgb(250, 210, 140, 0.95f), Plate = Rgb(210, 140, 70), Dna = Rgb(120, 230, 255, 0.9f),
    };
    public static readonly PhagePal Ph = new()
    {
        Head = Rgb(40, 44, 40), Head2 = Rgb(20, 22, 20), Empty = Rgb(60, 64, 60, 0.3f), Line = Rgb(15, 18, 15, 0.9f), Sheath = Rgb(55, 60, 55),
        Stripe = Rgb(20, 20, 20, 0.6f), Fiber = Rgb(40, 44, 40, 0.7f), Plate = Rgb(45, 50, 45), Dna = Rgb(30, 60, 70, 0.8f),
    };
}

/// <summary>
/// Bakteriofag T4: główka ~120 × 86 nm (wydłużony dwudziestościan), ogonek ~100 nm z kurczliwą pochewką
/// (~92 → 42 nm), płytka podstawowa, 6 długich włókien ~150 nm. Brak napędu – ruchy Browna.
/// </summary>
public sealed class PhageT4 : Ent
{
    public float Contract, Empty, Fib, Surf = 0.06f, St0, Kx, Ky, Fade = 1, TipX, TipY;
    public EColi Host;
    float _lx, _ly, _nlx, _nly, _sx, _sy, _sa, _cool;
    Omv _omv;
    readonly List<Ent> _q = new();

    public PhageT4(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Phage; Layer = 4; Solid = false; Name = "bakteriofag T4"; Latin = "Tequatrovirus";
        State = St.Free; D = 3.0f; Dr = 120; R = 0.14f; DrawR = 0.1f;
        TipX = x; TipY = y;
    }
    public override bool CanControl() => State == St.Free;

    public override void Step(float dt, float gdt)
    {
        if (State == St.Free)
        {
            if (IsPlayer)
            {
                var I = W.Input; float drift = W.Sc.PhageDrift * (I.Boost ? 1.6f : 1);
                Vx = I.Dx * drift; Vy = I.Dy * drift;
            }
            else { Vx = Vy = 0; }
            if (Kx != 0 || Ky != 0)
            {
                X += Kx * gdt; Y += Ky * gdt;
                float k = MathF.Exp(-2.5f * gdt); Kx *= k; Ky *= k;
                if (MathF.Abs(Kx) + MathF.Abs(Ky) < 0.01f) Kx = Ky = 0;
            }
            float c = MathF.Cos(A), s = MathF.Sin(A);
            TipX = X + c * 0.115f; TipY = Y + s * 0.115f;
            _cool -= gdt;
            W.Query(X, Y, 3.2f, _q);
            for (int i = 0; i < _q.Count; i++)
            {
                if (_q[i] is Omv ov && ov.Alive && Kx == 0 && _cool <= 0 && Hypot(TipX - ov.X, TipY - ov.Y) < ov.Rv + 0.02f)
                {
                    // wabik: szybkie, nieodwracalne wiązanie z pęcherzykiem błony zewnętrznej
                    _omv = ov; ov.ToLocal(X, Y, out _lx, out _ly); State = St.Attach; St0 = 0; Holder = ov; Fib = 1; Surf = 0;
                    if (IsPlayer) W.Log("Związałeś się z pęcherzykiem błony zewnętrznej (OMV) – to wabik! Wiązanie jest nieodwracalne, DNA trafia w pustkę.", "bad");
                    return;
                }
                if (_q[i] is not EColi e || !e.Alive || e.State == St.Dead) continue;
                if (MathF.Abs(e.X - X) > e.R + 0.3f || MathF.Abs(e.Y - Y) > e.R + 0.3f) continue;
                var r = Geo.PointPrim(X, Y, e.Prim);
                if (r.D < 0.07f) { float push = 0.07f - r.D; X += r.Nx * push; Y += r.Ny * push; }
                r = Geo.PointPrim(TipX, TipY, e.Prim);
                if (r.D < 0.1f && e.CanAdsorb() && Kx == 0 && _cool <= 0) { Bind(e, r); break; }
            }
            return;
        }
        if (State == St.Debris)
        {
            if (Holder is Omv om && om.Alive) { om.ToWorld(_lx, _ly, out X, out Y); Vx = Vy = 0; }
            Fade -= gdt / 6;
            if (Fade <= 0) Alive = false;
            return;
        }
        if (State == St.Attach)
        {
            St0 += gdt;
            if (_omv == null || !_omv.Alive) { State = St.Debris; Holder = null; Fade = 1; return; }
            _omv.ToWorld(_lx, _ly, out X, out Y); A = MathF.Atan2(_omv.Y - Y, _omv.X - X);
            Contract = Smooth(St0 / 0.3f); Empty = Smooth((St0 - 0.3f) / 2);
            if (St0 > 2.5f) { State = St.Debris; Fade = 1; if (IsPlayer) W.Log("Kapsyd pusty – Twój wirion przepadł na wabiku.", "bad"); }
            Vx = Vy = 0;
            return;
        }
        var h = Host;
        St0 += gdt;
        if (State == St.Bind)
        {
            float p = Smooth(St0 / 1.1f);
            Fib = p; Surf = Lerp(0.07f, 0, p);
            if (h != null && h.Resistant && St0 >= 0.7f) { Reject(h); return; }
            if (St0 >= 1.1f) { State = St.Contract; St0 = 0; }
        }
        else if (State == St.Contract)
        {
            Contract = Smooth(St0 / 0.25f);
            if (St0 >= 0.25f)
            {
                State = St.Inject; St0 = 0; Contract = 1;
                bool wasPlayer = IsPlayer;
                h.StartInfection(this);
                if (wasPlayer && IsPlayer) { W.Focus = h; W.Log("Pochewka skurczona – rurka przebiła osłony. Wstrzykiwanie DNA (~169 kpz)...", "good"); }
            }
        }
        else if (State == St.Inject)
        {
            Empty = Smooth(St0 / 2.2f);
            if (St0 >= 2.2f) { State = St.Ghost; St0 = 0; if (IsPlayer) W.Log("Kapsyd pusty (\"duch\" faga zostaje na powierzchni). Komórka przejęta – Spacja przyspiesza czas.", "info"); }
        }
        if (h != null && h.Alive)
        {
            h.ToWorld(_lx + _nlx * (0.113f + Surf), _ly + _nly * (0.113f + Surf), out float ax, out float ay);
            float ta = h.A + MathF.Atan2(-_nly, -_nlx);
            if (State == St.Bind)
            {
                float p = Smooth(Math.Min(1, St0 / 0.6f));
                X = Lerp(_sx, ax, p); Y = Lerp(_sy, ay, p); A = _sa + WrapA(ta - _sa) * p;
            }
            else { X = ax; Y = ay; A = ta; }
            h.ToWorld(_lx, _ly, out TipX, out TipY);
        }
        else Release();
        Vx = Vy = 0;
    }
    public override void Integrate(float dt) { if (State == St.Free || State == St.Debris) base.Integrate(dt); }

    void Bind(EColi e, PHit r)
    {
        Host = e; Holder = e; State = St.Bind; St0 = 0; e.Bound++;
        e.ToLocal(r.Cx, r.Cy, out _lx, out _ly);
        float c = MathF.Cos(-e.A), s = MathF.Sin(-e.A);
        _nlx = r.Nx * c - r.Ny * s; _nly = r.Nx * s + r.Ny * c;
        _sx = X; _sy = Y; _sa = A; Surf = 0.07f;
        if (IsPlayer) W.Log("Włókna ogonka związały receptor (LPS / OmpC). Płytka podstawowa zmienia konformację...", "info");
    }
    /// <summary>Mutant bez receptora (OmpC⁻ / zmieniony LPS): wiązanie tylko odwracalne – fag odpada.</summary>
    void Reject(EColi h)
    {
        h.Bound--;
        State = St.Free; Host = null; Holder = null; Fib = 0; Surf = 0.06f; _cool = 2; D = 3.0f; Dr = 120;
        float ox = -MathF.Cos(A), oy = -MathF.Sin(A); Kx = ox * 0.8f; Ky = oy * 0.8f;
        if (IsPlayer || Lineage) h.KnownResistant = true;
        if (IsPlayer) W.Log("Brak receptora (mutant OmpC⁻ / zmieniony LPS) – wiązanie odwracalne, wirion odpada. Ta komórka jest odporna.", "bad");
    }
    public void Release()
    {
        if (State == St.Free || State == St.Debris) return;
        if (Host != null) Host.Bound--;
        State = St.Debris; Host = null; Holder = null; D = 1.0f; Dr = 40; Fade = 1;
    }

    public override void Draw(Painter p, View v)
    {
        var pal = W.Em ? PhagePal.Em : PhagePal.Ph;
        p.Local(X, Y, A);
        float lw = Math.Max(0.004f, v.Px * 1.1f);
        bool crowd = v.Px > 0 && W.PhagesVisible > 90;
        if (State == St.Free) { DrawT4(p, pal, 0, 0, 0, null, lw, 1, IsPlayer ? 0 : crowd ? 2 : 1); return; }
        float al = State == St.Debris ? Fade : 1;
        int lod = IsPlayer || State == St.Bind || State == St.Contract || State == St.Inject ? 0 : 2;
        DrawT4(p, pal, Contract, Empty, Fib, Surf, lw, al, lod);
        if (State == St.Inject && W.Em)
        {
            Span<Vector2> s = stackalloc Vector2[] { new(-0.01f, 0), new(0.15f, 0) };
            p.Dashed(s, false, 0.006f, 0.012f, 0.01f, pal.Dna, -W.T * 0.15f);
        }
    }

    static readonly Vector2[] HeadPts = { new(-0.125f, 0), new(-0.103f, -0.043f), new(-0.022f, -0.043f), new(0, 0), new(-0.022f, 0.043f), new(-0.103f, 0.043f) };

    /// <summary>Główka faga (sam kapsyd).</summary>
    public static void DrawHead(Painter p, PhagePal P, float hs, Color fill, float lw)
    {
        Span<Vector2> h = stackalloc Vector2[6];
        for (int i = 0; i < 6; i++) h[i] = new Vector2(HeadPts[i].X + hs, HeadPts[i].Y);
        p.FillFan(h, -0.0625f + hs, 0, fill);
        p.Stroke(h, true, lw * 1.2f, P.Line);
    }

    /// <summary>Pełny rysunek T4 w układzie lokalnym (oś +x: od główki do płytki; (0,0) = szyjka).</summary>
    public static void DrawT4(Painter p, PhagePal P, float c, float e, float fib, float? surf, float lw, float alpha, int lod = 0)
    {
        float oldA = p.Alpha; p.Alpha *= alpha;
        float hs = c * 0.05f;
        // długie włókna (3 na stronę, różna głębia; przy niższym LOD mniej)
        Span<Vector2> f = stackalloc Vector2[3];
        int kmax = lod == 0 ? 2 : lod == 1 ? 1 : -1;
        for (int side = -1; side <= 1; side += 2)
            for (int k = kmax; k >= 0; k--)
            {
                float bx = 0.104f, by = side * (0.02f + k * 0.004f);
                float t1 = Lerp(2.7f, 1.85f, fib) + k * 0.12f;
                float kx = bx + MathF.Cos(t1) * 0.07f, ky = by + side * MathF.Sin(t1) * 0.07f;
                float t2 = surf.HasValue ? MathF.Acos(Clamp((0.113f + surf.Value - kx) / 0.08f, -1, 1)) + k * 0.1f : Lerp(0.55f, 1.0f, fib) - k * 0.12f;
                float tx = kx + MathF.Cos(t2) * 0.08f, ty = ky + side * MathF.Sin(t2) * 0.08f;
                f[0] = new(bx, by); f[1] = new(kx, ky); f[2] = new(tx, ty);
                p.Stroke(f, false, lw * 1.05f, Mul(P.Fiber, k == 0 ? 1f : k == 1 ? 0.7f : 0.45f));
            }
        // rurka ogonka (po skurczu przebija osłony)
        if (c > 0.02f)
        {
            float tl = 0.113f + c * 0.035f;
            p.Rect(0.008f + hs, -0.0045f, tl - 0.008f - hs, 0.009f, P.Sheath);
        }
        // pochewka z prążkami
        float hw = Lerp(0.012f, 0.0175f, c), x0 = 0.008f + hs, x1 = 0.1f;
        p.Rect(x0, -hw, x1 - x0, hw * 2, P.Sheath);
        Span<Vector2> box = stackalloc Vector2[] { new(x0, -hw), new(x1, -hw), new(x1, hw), new(x0, hw) };
        p.Stroke(box, true, lw, P.Line);
        int ns = lod == 0 ? 12 : lod == 1 ? 5 : 0;
        for (int i = 1; i < ns; i++) { float x = x0 + (x1 - x0) * i / ns; p.Seg(x, -hw, x, hw, lw * 0.6f, P.Stripe); }
        // płytka podstawowa
        float pw = Lerp(0.03f, 0.046f, c);
        Span<Vector2> plate = stackalloc Vector2[] { new(0.099f, -0.02f), new(0.113f, -pw), new(0.116f, 0), new(0.113f, pw), new(0.099f, 0.02f) };
        p.FillFan(plate, 0.107f, 0, P.Plate);
        p.Stroke(plate, true, lw, P.Line);
        if (fib > 0.3f)
        {
            var fc = Mul(P.Fiber, (fib - 0.3f) / 0.7f);
            p.Seg(0.114f, -0.012f, 0.121f, -0.028f, lw * 0.9f, fc);
            p.Seg(0.114f, 0.012f, 0.121f, 0.028f, lw * 0.9f, fc);
        }
        // kołnierz + wąsy (fibrytyna)
        p.Rect(hs - 0.002f, -0.019f, 0.011f, 0.038f, P.Plate);
        if (lod == 0)
        {
            p.Seg(hs + 0.003f, -0.019f, hs - 0.028f, -0.047f, lw * 0.8f, P.Fiber);
            p.Seg(hs + 0.003f, 0.019f, hs - 0.028f, 0.047f, lw * 0.8f, P.Fiber);
        }
        // główka
        Span<Vector2> h = stackalloc Vector2[6];
        for (int i = 0; i < 6; i++) h[i] = new Vector2(HeadPts[i].X + hs, HeadPts[i].Y);
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { Mul(P.Head, 1 - e * 0.85f), Mul(P.Head2, 1 - e * 0.85f) };
        p.FillRadial(h, -0.075f + hs, -0.015f, ts, cs);
        if (e > 0) p.FillFan(h, -0.0625f + hs, 0, P.Empty);
        p.Stroke(h, true, lw * 1.2f, P.Line);
        if (lod > 0) { p.Alpha = oldA; return; }
        var fl = Mul(P.Line, 0.55f); float lf = lw * 0.6f;
        p.Seg(-0.103f + hs, -0.043f, -0.085f + hs, 0, lf, fl); p.Seg(-0.085f + hs, 0, -0.103f + hs, 0.043f, lf, fl);
        p.Seg(-0.022f + hs, -0.043f, -0.04f + hs, 0, lf, fl); p.Seg(-0.04f + hs, 0, -0.022f + hs, 0.043f, lf, fl);
        p.Seg(-0.125f + hs, 0, hs, 0, lf, fl);
        p.Alpha = oldA;
    }
}

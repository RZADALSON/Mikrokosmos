namespace Mikro;

public enum MicroType { Bacillus, Pseudomonas, Caulobacter, Spirillum, Yeast }

/// <summary>
/// Inne bakterie i drobne grzyby skali biofilmu: Bacillus (Gram+, łańcuszki, przetrwalniki), Pseudomonas (jedna rzęska
/// biegunowa, szybki), Caulobacter (trzonek + holdfast), Spirillum volutans (olbrzymia spirala z pękami rzęsek),
/// drożdże (pączkowanie). Rysowane w kontraście fazowym albo w stylu TEM.
/// </summary>
public sealed class Microbe : Ent
{
    public readonly MicroType T;
    public float L, Wd, Speed; readonly int _cells; readonly bool _spore; float _timer, _ph, _stalk, _bud, _dir = 1; readonly float _seed;
    readonly float _ax, _ay;
    public override float Length => L;
    public override float Radius => Wd / 2;

    public Microbe(World w, MicroType t, float x, float y, float a, int cells = 1, float stalkAng = float.NaN) : base(w, x, y, a)
    {
        T = t; Layer = 2; Kind = t == MicroType.Yeast ? Kind.Fungus : Kind.Microbe; _seed = Rand(0, 99); State = St.Free;
        switch (t)
        {
            case MicroType.Bacillus:
                _cells = Math.Max(1, cells); Wd = Rand(0.8f, 1.0f); L = _cells * Rand(2.2f, 3.2f); Speed = _cells == 1 ? Rand(15, 25) : 0;
                _spore = Chance(0.3f); Name = "laseczka sienna"; Latin = "Bacillus subtilis (Gram+)"; Soft = 0.05f; D = 0.01f; Dr = _cells == 1 ? 0.05f : 0.002f; break;
            case MicroType.Pseudomonas:
                Wd = Rand(0.5f, 0.7f); L = Rand(1.5f, 2.6f); Speed = Rand(35, 55); Name = "pałeczka ropy błękitnej"; Latin = "Pseudomonas aeruginosa"; Soft = 0.08f; D = 0.05f; Dr = 0.08f; break;
            case MicroType.Caulobacter:
                Wd = Rand(0.5f, 0.6f); L = Rand(1.4f, 2.0f); _stalk = Rand(1.5f, 3.5f); Name = "Caulobacter (komórka z trzonkiem)"; Latin = "Caulobacter crescentus"; Soft = 0.05f; Fixed = !float.IsNaN(stalkAng); FlowK = 0;
                if (Fixed) { _ax = x; _ay = y; A = stalkAng; X = x + MathF.Cos(A) * (_stalk + L / 2); Y = y + MathF.Sin(A) * (_stalk + L / 2); } break;
            case MicroType.Spirillum:
                Wd = Rand(1.3f, 1.7f); L = Rand(18, 45); Speed = Rand(15, 25); Name = "krętek olbrzymi"; Latin = "Spirillum volutans"; Soft = 0.02f; D = 0; Dr = 0.002f; _timer = ExpRand(6); break;
            case MicroType.Yeast:
                Wd = Rand(4, 6.5f); L = Wd * Rand(1.1f, 1.3f); _bud = Chance(0.6f) ? Rand(0.2f, 0.8f) : 0; Name = "drożdże"; Latin = "Saccharomyces cerevisiae"; Soft = 0.1f; D = 0.002f; Dr = 0.001f; break;
        }
        R = L / 2 + (t == MicroType.Spirillum ? 3 : 1); Mass = L * Wd * 0.5f + 0.3f;
        Edible = true; Food = 0.1f;
    }
    public override void UpdatePrim()
    {
        if (State is St.Cup or St.Vac) { SetCircle(0.1f); return; }
        if (T == MicroType.Yeast) SetCircle(Wd / 2);
        else if (T == MicroType.Spirillum) SetCapsule(L / 2 - 2, 2.2f);
        else SetCapsule(Math.Max(0, L / 2 - Wd / 2), Wd / 2);
    }
    public override bool Collides(Ent o) => State is not (St.Cup or St.Vac);
    public override void Step(float dt, float gdt)
    {
        _ph += gdt * TAU * 3;
        if (State is St.Cup or St.Vac) { Vx = Vy = 0; D = 0; Dr = 0; return; }
        if (Fixed) { Vx = Vy = 0; return; }
        _timer -= dt;
        switch (T)
        {
            case MicroType.Pseudomonas:
                if (_timer <= 0) { _dir = Chance(0.6f) ? -_dir : _dir; A += RandN() * 0.6f; _timer = ExpRand(1.5f); }
                Vx = MathF.Cos(A) * Speed * _dir; Vy = MathF.Sin(A) * Speed * _dir; break;
            case MicroType.Spirillum:
                if (_timer <= 0) { _dir = -_dir; _timer = ExpRand(6); }
                A += RandN() * 0.05f * MathF.Sqrt(dt);
                Vx = MathF.Cos(A) * Speed * _dir; Vy = MathF.Sin(A) * Speed * _dir; break;
            case MicroType.Bacillus:
                if (Speed > 0) { if (_timer <= 0) { A += RandN() * 1.1f; _timer = ExpRand(1); } Vx = MathF.Cos(A) * Speed; Vy = MathF.Sin(A) * Speed; }
                else Vx = Vy = 0; break;
            default: Vx = Vy = 0; break;
        }
    }
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (T == MicroType.Spirillum && !o.Fixed) _dir = -_dir;
    }

    public override void Draw(Painter p, View v)
    {
        bool em = W.Em;
        float oldA = p.Alpha; if (State is St.Cup or St.Vac) p.Alpha = 1 - Dig * 0.9f;
        var body = em ? Rgb(120, 150, 165, 0.9f) : Rgb(50, 56, 50, 0.9f);
        var halo = em ? Rgb(200, 230, 240, 0.25f) : Rgb(255, 255, 240, 0.3f);
        var edge = em ? Rgb(40, 60, 70, 0.95f) : Rgb(15, 18, 15, 0.9f);
        var fl = em ? Rgb(160, 200, 215, 0.6f) : Rgb(35, 40, 36, 0.28f);
        float lw = Math.Max(v.Px * 0.9f, em ? 0.012f : 0.03f);
        if (T == MicroType.Caulobacter && Fixed)
        {
            p.Identity();
            p.Line(_ax, _ay, X - MathF.Cos(A) * L / 2, Y - MathF.Sin(A) * L / 2, Math.Max(lw, 0.08f), em ? Rgb(170, 200, 215, 0.8f) : Rgb(40, 44, 40, 0.5f));
            p.FillCircle(_ax, _ay, 0.18f, em ? Rgb(200, 220, 230, 0.8f) : Rgb(30, 34, 30, 0.7f));
        }
        p.Local(X, Y, A);
        var buf = p.Buf2;
        switch (T)
        {
            case MicroType.Spirillum:
            {
                const int K = 60; float amp = 2.2f, lam = 11;
                for (int k = 0; k <= K; k++) { float x = -L / 2 + L * k / K; buf[k] = new Vector2(x, amp * MathF.Sin(TAU * x / lam + _ph * 0.3f * _dir)); }
                var s = new ReadOnlySpan<Vector2>(buf, 0, K + 1);
                p.SoftStroke(s, false, Wd * 2.2f, halo);
                p.Stroke(s, false, Wd, body, true);
                for (int e = -1; e <= 1; e += 2)
                    for (int f = 0; f < 5; f++) { float a = (e > 0 ? 0 : PI) + (f - 2) * 0.18f + MathF.Sin(_ph + f) * 0.1f; float x0 = e * L / 2, y0 = buf[e > 0 ? K : 0].Y; p.Seg(x0, y0, x0 + MathF.Cos(a) * 5, y0 + MathF.Sin(a) * 5, lw, fl); }
                break;
            }
            case MicroType.Yeast:
            {
                p.Glow(0, 0, Wd * 0.8f, halo);
                p.FillEllipse(0, 0, L / 2, Wd / 2, 0, em ? Rgb(110, 140, 155, 0.9f) : Rgb(150, 150, 130, 0.55f));
                p.Ellipse(0, 0, L / 2, Wd / 2, 0, em ? Wd * 0.08f : lw * 1.5f, edge);
                p.FillCircle(L * 0.08f, Wd * 0.05f, Wd * 0.2f, em ? Rgb(60, 80, 90, 0.8f) : Rgb(215, 220, 205, 0.55f));
                for (int k = 0; k < 8; k++) p.Dot(MathF.Cos(k * 2.3f + _seed) * Wd * 0.3f, MathF.Sin(k * 1.7f) * Wd * 0.3f, Wd * 0.04f, em ? Rgb(30, 45, 55, 0.8f) : Rgb(60, 62, 55, 0.6f));
                if (_bud > 0)
                {
                    float br = Wd * 0.28f * _bud + Wd * 0.1f;
                    p.FillCircle(L / 2 + br * 0.75f, 0, br, em ? Rgb(110, 140, 155, 0.9f) : Rgb(150, 150, 130, 0.5f));
                    p.Circle(L / 2 + br * 0.75f, 0, br, em ? Wd * 0.06f : lw * 1.3f, edge);
                }
                break;
            }
            default:
            {
                float hl = Math.Max(0, L / 2 - Wd / 2), r = Wd / 2;
                if (T == MicroType.Pseudomonas || (T == MicroType.Bacillus && Speed > 0) || (T == MicroType.Caulobacter && !Fixed))
                {
                    int nfl = T == MicroType.Bacillus ? 6 : 1;
                    for (int f = 0; f < nfl; f++)
                    {
                        float sx = T == MicroType.Bacillus ? (f - 2.5f) * hl * 0.35f : -hl - r, sy = T == MicroType.Bacillus ? (f % 2 == 0 ? r : -r) : 0;
                        for (int k = 0; k <= 20; k++) { float d = k * 0.3f; buf[k] = new Vector2(sx - d, sy + 0.18f * MathF.Sin(d * 2.8f - _ph) * Math.Min(1, d)); }
                        p.Thin(new ReadOnlySpan<Vector2>(buf, 0, 21), false, lw, fl);
                    }
                }
                int n = 0;
                for (int k = 0; k <= 12; k++) { float t = -PI / 2 + PI * k / 12; buf[n++] = new Vector2(hl + MathF.Cos(t) * r, MathF.Sin(t) * r * (T == MicroType.Caulobacter ? 1 : 1)); }
                for (int k = 0; k <= 12; k++) { float t = PI / 2 + PI * k / 12; buf[n++] = new Vector2(-hl + MathF.Cos(t) * r, MathF.Sin(t) * r); }
                if (T == MicroType.Caulobacter) for (int k = 0; k < n; k++) { float u = buf[k].X / Math.Max(L / 2, 1e-3f); buf[k] = new Vector2(buf[k].X, buf[k].Y + (u * u - 0.5f) * Wd * 0.8f); }
                var sh = new ReadOnlySpan<Vector2>(buf, 0, n);
                p.SoftStroke(sh, true, em ? Wd * 0.25f : Wd * 1.1f, halo);
                p.FillFan(sh, 0, T == MicroType.Caulobacter ? -Wd * 0.4f : 0, body);
                if (em && T == MicroType.Bacillus) p.Stroke(sh, true, Wd * 0.09f, Rgb(200, 225, 235, 0.8f)); // gruba ściana Gram+
                p.Stroke(sh, true, em ? Wd * 0.03f : lw, edge);
                if (T == MicroType.Bacillus)
                {
                    for (int c = 1; c < _cells; c++) { float x = -L / 2 + L * c / _cells; p.Line(x, -r * 0.9f, x, r * 0.9f, em ? Wd * 0.07f : lw * 1.3f, em ? Rgb(200, 225, 235, 0.7f) : Rgb(200, 205, 190, 0.5f)); }
                    if (_spore) { float sx = -L / 2 + L / _cells * 0.5f; p.FillEllipse(sx, 0, r * 0.75f, r * 0.55f, 0, em ? Rgb(20, 30, 36, 0.95f) : Rgb(245, 250, 235, 0.9f)); }
                }
                if (em) for (int k = 0; k < 10; k++) p.Dot(MathF.Cos(k * 2.1f + _seed) * hl * 0.8f, MathF.Sin(k * 1.3f + _seed) * r * 0.5f, 0.012f, Rgb(40, 55, 65, 0.8f));
                break;
            }
        }
        p.Alpha = oldA;
    }
}

/// <summary>
/// Pęcherzyk błony zewnętrznej (OMV, 20–250 nm) – "wabik": T4 wiąże się z nim szybko i nieodwracalnie
/// i wstrzykuje DNA w pustkę (Manning i Kuehn 2011, BMC Microbiol).
/// </summary>
public sealed class Omv : Ent
{
    public readonly float Rv;
    public override float Radius => Rv;
    public Omv(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.Particle; Layer = 2; Rv = Rand(0.02f, 0.12f); R = Rv + 0.02f; Mass = 0.05f; D = 0.12f / Rv * 0.03f; Solid = true;
        Name = "pęcherzyk błony zewnętrznej (OMV)"; Latin = "outer membrane vesicle"; Soft = 0.3f;
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, 0);
        p.FillCircle(0, 0, Rv, W.Em ? Rgb(110, 140, 155, 0.55f) : Rgb(60, 60, 55, 0.5f));
        p.Circle(0, 0, Rv, Math.Max(v.Px, Rv * 0.12f), W.Em ? Rgb(185, 220, 232, 0.9f) : Rgb(30, 30, 25, 0.7f));
        p.Circle(0, 0, Rv * 0.8f, Math.Max(v.Px * 0.7f, Rv * 0.06f), W.Em ? Rgb(185, 220, 232, 0.5f) : Rgb(30, 30, 25, 0.3f));
    }
}

/// <summary>
/// Konkurencyjne fagi: T7 (podowirus, krótki ogonek, cykl ~13–15 min) i λ (sifowirus, długi giętki ogonek,
/// często lizogenia). Wiążą się z E. coli i przejmują komórkę przed Tobą.
/// </summary>
public sealed class OtherPhage : Ent
{
    public readonly bool Lambda;
    EColi _host; float _lx, _ly, _st; float _empty;
    readonly List<Ent> _q = new();
    public OtherPhage(World w, float x, float y, bool lambda) : base(w, x, y, Rand(0, TAU))
    {
        Lambda = lambda; Kind = Kind.Particle; Layer = 4; Solid = false; State = St.Free; D = 3.0f; Dr = 120; R = 0.2f;
        Name = lambda ? "bakteriofag λ" : "bakteriofag T7"; Latin = lambda ? "Lambdavirus lambda" : "Teseptimavirus T7";
    }
    public override float Length => Lambda ? 0.21f : 0.08f;
    public override void Integrate(float dt) { if (State == St.Free) base.Integrate(dt); }
    public override void Step(float dt, float gdt)
    {
        if (State == St.Free)
        {
            W.Query(X, Y, 3, _q);
            foreach (var o in _q)
            {
                if (o is not EColi e || !e.Alive || !e.CanHost() || e.Foreign > 0 || (Lambda && e.Lysogen)) continue;
                var r = Geo.PointPrim(X, Y, e.Prim);
                if (r.D < 0.06f)
                {
                    _host = e; Holder = e; State = St.Bind; _st = 0; e.ToLocal(r.Cx, r.Cy, out _lx, out _ly); break;
                }
            }
            return;
        }
        if (_host == null || !_host.Alive) { Alive = false; return; }
        _st += gdt;
        _host.ToWorld(_lx, _ly, out X, out Y);
        A = MathF.Atan2(Y - _host.Y, X - _host.X);
        if (State == St.Bind && _st > 1.2f)
        {
            State = St.Ghost;
            if (_host.Inf == null && _host.Foreign <= 0)
            {
                if (Lambda && Chance(0.7f)) _host.Lysogen = true; // lizogenia: profag w chromosomie
                else { _host.Foreign = Lambda ? 2400 : 1000; _host.ForeignLambda = Lambda; }
            }
        }
        if (State == St.Ghost) { _empty = Math.Min(1, _empty + gdt / 2); if (_st > 40) Alive = false; }
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A + PI);
        var col = W.Em ? Rgb(190, 220, 235, 0.9f) : Rgb(40, 40, 36, 0.8f);
        var fill = W.Em ? Rgb(70, 95, 110, 0.95f - 0.6f * _empty) : Rgb(60, 60, 55, 0.6f);
        float lw = Math.Max(v.Px, 0.004f);
        Span<Vector2> hex = stackalloc Vector2[6];
        float hr = Lambda ? 0.03f : 0.03f, hx = Lambda ? -0.18f : -0.045f;
        for (int i = 0; i < 6; i++) { float t = i * TAU / 6; hex[i] = new Vector2(hx + MathF.Cos(t) * hr, MathF.Sin(t) * hr); }
        p.FillFan(hex, hx, 0, fill); p.Stroke(hex, true, lw, col);
        if (Lambda)
        {
            var buf = p.Buf2;
            for (int k = 0; k <= 12; k++) { float t = k / 12f; buf[k] = new Vector2(hx + hr + t * 0.15f, 0.01f * MathF.Sin(t * 6 + W.T * 2)); }
            p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, 13), false, 0.009f, col);
        }
        else p.Line(hx + hr, 0, hx + hr + 0.02f, 0, 0.012f, col);
    }
}

/// <summary>Zerwana rzęska bakteryjna (filament flageliny, Ø ≈ 20 nm) – szczątek w preparacie TEM.</summary>
public sealed class FlagFrag : Ent
{
    readonly Vector2[] _pts;
    public FlagFrag(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.None; Layer = 0; Solid = false; D = 0.02f; Dr = 0.02f; Name = "zerwana rzęska (flagelina)";
        float len = Rand(1.5f, 5); int n = 40; _pts = new Vector2[n + 1]; R = len / 2;
        for (int k = 0; k <= n; k++) { float d = -len / 2 + len * k / n; _pts[k] = new Vector2(d, 0.2f * MathF.Sin(d * 2.7f)); }
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        p.Thin(_pts, false, Math.Max(v.Px, 0.02f), W.Em ? Rgb(170, 205, 220, 0.55f) : Rgb(40, 44, 40, 0.3f));
    }
}

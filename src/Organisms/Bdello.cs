namespace Mikro;

/// <summary>
/// Bdellovibrio bacteriovorus – drapieżna bakteria 0,3 × ~1 µm, osłonięta rzęska, do ~160 µm/s.
/// Cykl: przyczepienie → wniknięcie do peryplazmy → bdeloplast → filament → 3–6 potomków → liza (~3–4 h).
/// </summary>
public sealed class Bdello : Ent
{
    const float T_ATTACH = 300, T_ENTER = 600, T_GROW = 9600, T_SEPT = 1200;
    public float L, Wd = 0.32f, Speed, Ph, Roll, RevT, St0, Len, Spin, Hunger;
    public int NProg;
    public bool Boosting;
    public EColi Prey;
    float _alx, _aly, _anx, _any;

    public override float Length => L;

    public Bdello(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Bdello; Layer = 3;
        L = Rand(0.85f, 1.1f); State = St.Swim;
        Speed = Rand(40, 65); Mass = 0.35f; D = 0.06f; Dr = 0.12f; R = L / 2 + 0.15f;
        Ph = Rand(0, TAU); Roll = Rand(0, TAU); RevT = ExpRand(3); Len = L;
    }
    public override bool CanControl() => State == St.Swim;
    public override void UpdatePrim() { if (State == St.Swim) SetCapsule(Math.Max(0, L / 2 - Wd / 2), Wd / 2); else SetCircle(0.01f); }
    public override bool Collides(Ent o) => State == St.Swim && !(o is EColi e && e == Prey);
    public override void Integrate(float dt) { if (State == St.Swim) base.Integrate(dt); }

    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        Ph += gdt * TAU * (Boosting ? 7 : 4);
        Roll += gdt * TAU * 1.6f;
        if (State == St.Cup || State == St.Vac) { Vx = Vy = 0; return; }
        if (State == St.Swim)
        {
            if (IsPlayer && !W.Preview && W.Sc.Starve > 0)
            {
                Hunger += gdt / W.Sc.Starve;
                if (Hunger >= 1)
                {
                    W.Log("Bdellovibrio wyczerpał rezerwy energii w fazie ataku – komórka obumiera.", "bad");
                    W.Fx.Spill(X, Y, 10, new FX.SpillOpt { Speed = 1, R = 0.03f, Life = 3, D = 0.01f, Cols = new[] { Rgb(30, 35, 30, 0.6f) } });
                    Alive = false; return;
                }
            }
            if (IsPlayer)
            {
                var I = W.Input; bool on = I.Mag > 0.05f;
                if (on) A = Turn(A, I.Ang, 7 * gdt);
                Boosting = I.Boost && on;
                float v = (Boosting ? 160 : Speed) * (on ? I.Mag : 0);
                Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
            }
            else
            {
                RevT -= dt;
                if (RevT <= 0) { A += PI + RandN() * 0.4f; RevT = ExpRand(3); }
                var prey = W.Near(X, Y, 9, e => e is EColi c && c.CanHost());
                if (prey != null) A = Turn(A, MathF.Atan2(prey.Y - Y, prey.X - X), 2.2f * dt);
                Boosting = prey != null && Hypot(prey.X - X, prey.Y - Y) < 5;
                float v = Boosting ? 120 : Speed;
                Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
            }
            return;
        }
        var P = Prey;
        if (P == null || !P.Alive) { State = St.Swim; Prey = null; Solid = true; return; }
        Vx = Vy = 0;
        St0 += bdt;
        if (State == St.Attach)
        {
            Spin += gdt * 40;
            PlaceOnSurface(0);
            if (St0 >= T_ATTACH)
            {
                State = St.Enter; St0 = 0;
                bool preyWasPlayer = P.IsPlayer;
                P.State = St.Bdelloplast; P.Invader = this;
                if (IsPlayer) W.Log("Wnikanie do peryplazmy – ofiara ginie, ściana komórkowa zmieniona (bdeloplast).", "good");
                if (preyWasPlayer)
                {
                    W.Log("Bdellovibrio wniknął do Twojej peryplazmy. Komórka zabita.", "bad");
                    W.Player = null;
                    if (!W.Adopt(Kind.EColi, P.X, P.Y)) W.GameOver("Zjedzony przez Bdellovibrio");
                }
            }
        }
        else if (State == St.Enter)
        {
            float p = Smooth(St0 / T_ENTER);
            P.Round = Math.Min(1, p * 0.8f); P.Opacity = Lerp(1, 0.55f, p);
            PlaceOnSurface(p);
            if (St0 >= T_ENTER) { State = St.Grow; St0 = 0; NProg = P.ProgenyCount(); }
        }
        else if (State == St.Grow)
        {
            float p = St0 / T_GROW;
            P.Round = Math.Min(1, 0.8f + p);
            P.Opacity = Lerp(0.55f, 0.2f, Smooth(p));
            Len = Lerp(L, NProg * 0.92f, Smooth(p));
            X = P.X; Y = P.Y;
            if (St0 >= T_GROW) { State = St.Septate; St0 = 0; if (IsPlayer) W.Log($"Filament dzieli się na {NProg} komórki potomne...", "info"); }
        }
        else if (State == St.Septate)
        {
            X = P.X; Y = P.Y;
            if (St0 >= T_SEPT) Burst();
        }
    }
    void PlaceOnSurface(float p)
    {
        var P = Prey;
        float inside = -(L / 2 + 0.08f) * p + (L / 2 - 0.04f) * (1 - p);
        P.ToWorld(_alx + _anx * inside, _aly + _any * inside, out X, out Y);
        A = P.A + MathF.Atan2(-_any, -_anx);
    }
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (State != St.Swim || o is not EColi e || !e.CanHost()) return;
        var r = Geo.PointPrim(X + MathF.Cos(A) * L * 0.5f, Y + MathF.Sin(A) * L * 0.5f, e.Prim);
        e.ToLocal(r.Cx, r.Cy, out _alx, out _aly);
        float c = MathF.Cos(-e.A), s = MathF.Sin(-e.A);
        _anx = r.Nx * c - r.Ny * s; _any = r.Nx * s + r.Ny * c;
        Prey = e; e.Invader = this; State = St.Attach; St0 = 0; Solid = false; Hunger = 0;
        W.Sfx("tick", X, Y);
        if (IsPlayer) W.Log("Przyczepienie do ofiary! Bdellovibrio \"wierci\" otwór w błonie zewnętrznej (obrót ~100 Hz, enzymy).", "good");
        if (e.IsPlayer) W.Log("Bdellovibrio przyczepił się! Koziołkuj (Spacja), żeby go strząsnąć!", "bad");
    }
    public void Detach()
    {
        if (Prey != null) Prey.Invader = null;
        Prey = null; State = St.Swim; Solid = true; St0 = 0; A += PI;
        W.Log("Bdellovibrio odczepiony!", "good");
    }
    void Burst()
    {
        var P = Prey; int n = NProg;
        int np = FilamentPts(p: null, world: true);
        var outp = new List<Bdello>();
        for (int k = 0; k < n; k++)
        {
            int idx = (int)((k + 0.5f) / n * (np - 1));
            var q = _fil[idx];
            var b = W.Add(new Bdello(W, q.X, q.Y, _filA[idx] + (Chance(0.5f) ? 0 : PI)));
            b.Lineage = Lineage || IsPlayer; b.RevT = Rand(0.2f, 1);
            outp.Add(b);
        }
        P.LyseBdello();
        W.Sfx("blip", X, Y, 0.8f);
        if (Lineage || IsPlayer) { W.AddStat("kills", 1); W.AddStat("born", n); }
        if (IsPlayer) { W.Log($"LIZA BDELOPLASTU! Uwolniono {n} potomków. Kontrolujesz jednego z nich.", "good"); Alive = false; W.Player = null; W.SetPlayer(outp[0]); }
        else if (Lineage) W.Log($"Twoja linia: kolejna ofiara zjedzona (+{n} Bdellovibrio).", "info");
        Alive = false;
    }

    readonly Vector2[] _fil = new Vector2[40];
    readonly float[] _filA = new float[40];
    /// <summary>Oś filamentu wzdłuż wnętrza bdeloplastu (spirala do środka).</summary>
    int FilamentPts(Painter p, bool world)
    {
        var P = Prey;
        float Rb = P.Rb, rho0 = Rb - Wd * 0.62f;
        float phi0 = MathF.Atan2(_aly, _alx);
        const int N = 28;
        for (int k = 0; k <= N; k++)
        {
            float u = (float)k / N * Len;
            float phi = phi0 + u / rho0;
            float rho = rho0 * (1 - 0.28f * (u / (rho0 * TAU)));
            float x = MathF.Cos(phi) * rho, y = MathF.Sin(phi) * rho;
            if (world) { P.ToWorld(x, y, out float wx, out float wy); _fil[k] = new Vector2(wx, wy); _filA[k] = P.A + phi + PI / 2; }
            else _fil[k] = new Vector2(x, y);
        }
        return N + 1;
    }

    static readonly Color HaloA = Rgb(255, 255, 240, 0.35f), Body = Rgb(28, 32, 29), Core = Rgb(120, 130, 118, 0.35f), Flag = Rgb(30, 34, 30, 0.45f);

    void DrawBody(Painter p, float L2, float curv, float roll)
    {
        float Lh = Math.Max(0.01f, (L2 - Wd) / 2), c = MathF.Cos(roll);
        Span<Vector2> s = stackalloc Vector2[7];
        for (int k = 0; k <= 6; k++) { float u = -Lh + 2 * Lh * k / 6; s[k] = new Vector2(u, curv * Lh * (Sq(u / Lh) - 0.4f) * c); }
        p.SoftStroke(s, false, Wd + 0.55f, HaloA);
        p.Stroke(s, false, Wd, Body, true);
        p.Stroke(s, false, Wd * 0.35f, Core, true);
    }

    public override void Draw(Painter p, View v)
    {
        if (State == St.Swim || State == St.Attach || State == St.Cup || State == St.Vac)
        {
            if (State == St.Cup || State == St.Vac) p.Alpha = 1 - Dig * 0.9f;
            p.Local(X, Y, A);
            if (State == St.Attach) p.Translate(MathF.Sin(Spin) * 0.015f, MathF.Cos(Spin * 1.3f) * 0.015f);
            float Lh = L / 2;
            var buf = p.Buf2;
            const int K = 24; const float fl = 2.4f;
            for (int k = 0; k <= K; k++)
            {
                float d = (float)k / K * fl, env = Math.Min(1, d / 0.4f);
                buf[k] = new Vector2(-Lh + 0.05f - d, 0.14f * env * MathF.Sin(TAU * d / 1.0f - Ph));
            }
            p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, K + 1), false, Math.Max(0.03f, v.Px * 1.1f), Flag, true);
            DrawBody(p, L, 0.35f, State == St.Attach ? Spin : Roll);
            return;
        }
        if (Prey == null || !Prey.Alive) return;
        var P = Prey;
        p.Local(P.X, P.Y, P.A);
        if (State == St.Enter)
        {
            float sp = Smooth(St0 / T_ENTER);
            float inside = -(L / 2 + 0.08f) * sp + (L / 2 - 0.04f) * (1 - sp);
            p.Push();
            p.Translate(_alx + _anx * inside, _aly + _any * inside);
            p.Rotate(MathF.Atan2(-_any, -_anx));
            DrawBody(p, L, 0.35f, 0);
            p.Pop();
            p.FillCircle(_alx, _aly, Wd * 0.45f, Rgb(240, 240, 225, 0.6f));
            return;
        }
        int n = FilamentPts(p, false);
        void DrawSeg(int a, int b)
        {
            var span = new ReadOnlySpan<Vector2>(_fil, a, b - a + 1);
            p.SoftStroke(span, false, Wd + 0.3f, HaloA);
            p.Stroke(span, false, Wd, Rgb(26, 30, 27), true);
        }
        if (State == St.Grow) DrawSeg(0, n - 1);
        else
        {
            int N = n - 1; float sp = Smooth(St0 / T_SEPT);
            for (int k = 0; k < NProg; k++)
            {
                int a = (int)MathF.Round((float)k / NProg * N + (k > 0 ? sp * 1.2f : 0));
                int b = (int)MathF.Round((float)(k + 1) / NProg * N - (k < NProg - 1 ? sp * 1.2f : 0));
                if (b > a) DrawSeg(a, b);
            }
        }
    }
}

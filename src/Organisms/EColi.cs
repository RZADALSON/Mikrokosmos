namespace Mikro;

/// <summary>
/// Escherichia coli – pałeczka ~2 × 0,8 µm, rzęski perytrychalne,
/// "bieg i koziołek" (~25 µm/s, bieg ~1 s, obrót ~68°), podział ~20 min.
/// Może być ofiarą Bdellovibrio (bdeloplast) lub gospodarzem faga T4.
/// </summary>
public sealed class EColi : Ent
{
    public float Wd, L, L0, Speed, Round, Pinch, Opacity = 1, Bund = 1, Wob, Cmem, Rb, Timer;
    public bool Sessile, Dividing, Warned, Resistant, KnownResistant;
    public float Bulge;
    /// <summary>Infekcja obcym fagiem (T7/λ) – czas do lizy (s biologiczne); 0 = brak.</summary>
    public float Foreign; public bool ForeignLambda, Lysogen;
    /// <summary>Liczba fagów związanych z powierzchnią (ograniczona liczba miejsc receptorowych).</summary>
    public int Bound;
    public Infection Inf;
    public Bdello Invader;

    sealed class Flag { public float S, Side, Ph, Len, Spl, Fa; }
    readonly Flag[] _flags;

    public sealed class Capsid { public float X, Y, A, T, J; }
    public sealed class DnaSpot { public float X, Y, T, S; }
    public sealed class Infection
    {
        public float T, Base, Lat, Ex, Ey; public int Lin; public bool ByPlayer;
        public readonly List<Capsid> Caps = new(); public readonly List<DnaSpot> Dna = new();
    }

    // szczegóły dla trybu mikroskopu elektronowego
    List<Vector2[]> _nucleoid, _pili;
    Vector2[] _ribo;

    public override float Length => L;
    public override St FreeState => St.Run;
    public float Hl => Math.Max(0, (L - Wd) / 2);

    public EColi(World w, float x, float y, float a, float len = 0, float width = 0, float l0 = 0, bool sessile = false, int nf = 0) : base(w, x, y, a)
    {
        Kind = Kind.EColi; Layer = 2; Soft = 0.08f; Name = "pałeczka okrężnicy"; Latin = "Escherichia coli";
        Wd = width > 0 ? width : Rand(0.75f, 0.88f);
        L = len > 0 ? len : Rand(2.0f, 2.8f);
        L0 = l0 > 0 ? l0 : Rand(1.9f, 2.2f);
        Sessile = sessile;
        Mem = new Membrane(34) { MaxDef = 0.25f }; MemK = 14; MemS = 0.5f;
        State = St.Run; Timer = ExpRand(1);
        Speed = Rand(20, 30);
        D = Sessile ? 0.0005f : 0.05f; Dr = Sessile ? 0 : 0.06f;
        int n = nf > 0 ? nf : RandI(4, 7);
        _flags = new Flag[n];
        for (int i = 0; i < n; i++)
            _flags[i] = new Flag { S = Rand(-0.9f, 0.9f), Side = Chance(0.5f) ? 1 : -1, Ph = Rand(0, TAU), Len = Rand(5, 8.5f), Spl = Rand(0.5f, 2.3f), Fa = Sessile ? Rand(-PI, PI) : PI };
        Rest(); UpdatePrim();
    }

    public void Rest()
    {
        var m = Mem; int n = m.N; float r = Wd / 2, hl = Hl;
        float capL = PI * r, per = 4 * hl + 2 * capL;
        float area = 4 * hl * r + PI * r * r;
        Rb = MathF.Sqrt(area / PI) * 1.14f;
        float kr = Smooth(Round);
        for (int i = 0; i < n; i++)
        {
            float s = (i + 0.5f) / n * per, x, y;
            if (s < capL) { float f = -PI / 2 + s / r; x = hl + MathF.Cos(f) * r; y = MathF.Sin(f) * r; }
            else if ((s -= capL) < 2 * hl) { x = hl - s; y = r; }
            else if ((s -= 2 * hl) < capL) { float f = PI / 2 + s / r; x = -hl + MathF.Cos(f) * r; y = MathF.Sin(f) * r; }
            else { s -= capL; x = -hl + s; y = -r; }
            if (Pinch > 0) y *= 1 - Pinch * 0.94f * MathF.Exp(-(x * x) / (r * r * 0.3f));
            if (Bulge > 0) y += MathF.Sign(y) * Bulge * r * 1.4f * MathF.Exp(-(x * x) / (r * r * 0.45f));
            if (kr > 0) { float ang = MathF.Atan2(y, x); x = Lerp(x, MathF.Cos(ang) * Rb, kr); y = Lerp(y, MathF.Sin(ang) * Rb, kr); }
            if (Wob > 0) { float ang = MathF.Atan2(y, x), f = 1 + Wob * 0.05f * MathF.Sin(ang * 5 + W.T * 9); x *= f; y *= f; }
            m.Rx[i] = x; m.Ry[i] = y;
        }
        R = Math.Max(L / 2, Rb) + 0.1f;
    }

    public override void UpdatePrim()
    {
        if (Round > 0.5f) SetCircle(Rb);
        else SetCapsule(Hl, Wd / 2 * (1 - Pinch * 0.3f));
    }
    public bool CanHost() => Alive && Invader == null && Inf == null && Foreign <= 0 && Round == 0 && !Dividing && State != St.Dead && State != St.Cup && State != St.Vac;
    public bool CanAdsorb() => Alive && Bound < 8 && State != St.Dead && State != St.Bdelloplast && State != St.Cup && State != St.Vac && (Inf == null || Inf.T / Inf.Lat < 0.8f);
    public override bool CanControl() => Alive && Inf == null && Invader == null && State != St.Bdelloplast && State != St.Cup && State != St.Vac;
    public override bool Collides(Ent o) => !(o is Bdello b && b.Prey == this);

    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        foreach (var f in _flags) f.Ph += gdt * TAU * Math.Min(5, 100 * W.TimeScale);
        if (State == St.Cup || State == St.Vac) { Vx = Vy = 0; D = 0; Dr = 0; Opacity = 1 - Dig; Rest(); return; }
        if (State == St.Bdelloplast || State == St.Dead) { Vx = Vy = 0; D = 0; Dr = 0; Rest(); return; }
        if (Inf != null) StepInfect(bdt);
        if (Foreign > 0) { Foreign -= bdt; if (Foreign <= 0) { LyseForeign(); return; } }
        if (!Alive) return;
        Bund = Lerp(Bund, State == St.Tumble ? 0 : 1, Damp(12, gdt));
        if (Sessile) { Vx = Vy = 0; }
        else if (IsPlayer) Control(dt, gdt);
        else Ai(dt);
        if (Inf != null) { float k = 1 - Clamp(Inf.T / Inf.Lat * 1.6f, 0, 1); Vx *= k; Vy *= k; }
        if (Invader != null && Invader.State == St.Attach) { Vx *= 0.7f; Vy *= 0.7f; }
        Grow(bdt);
        Rest();
    }
    void Tumble(float dt)
    {
        Vx *= 0.1f; Vy *= 0.1f;
        A += RandN() * MathF.Sqrt(dt) * 2.2f;
        Timer -= dt;
        if (Timer <= 0) { A += Sign() * (1.19f + RandN() * 0.45f); State = St.Run; Timer = ExpRand(1.0f); }
    }
    void Ai(float dt)
    {
        if (State == St.Run)
        {
            Timer -= dt;
            if (W.Nutri != null)
            {
                float c = W.Nutri.Sample(X, Y), dc = c - Cmem;
                Cmem = Lerp(Cmem, c, Damp(2, dt));
                if (dc > 0.0005f) Timer += dt * 0.7f; // chemotaksja: dłuższe biegi w górę gradientu
            }
            if (Timer <= 0) { State = St.Tumble; Timer = ExpRand(0.14f); }
            Vx = MathF.Cos(A) * Speed; Vy = MathF.Sin(A) * Speed;
        }
        else Tumble(dt);
    }
    void Control(float dt, float gdt)
    {
        var I = W.Input;
        if (I.Action && State == St.Run)
        {
            State = St.Tumble; Timer = 0.12f;
            if (Invader != null && Invader.State == St.Attach && Chance(0.4f)) Invader.Detach();
        }
        if (State == St.Tumble) { Tumble(dt); return; }
        bool on = I.Mag > 0.05f;
        if (on) A = Turn(A, I.Ang, 3.2f * gdt);
        float v = Speed * (on ? I.Mag : 0) * (I.Boost ? 1.2f : 1);
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
    }
    void Grow(float bdt)
    {
        if (!W.Sc.GrowEcoli || Inf != null || Invader != null || W.Preview) return;
        // gęsta mikrokolonia: brak miejsca i składników – wzrost zahamowany
        if (Sessile && !Dividing) { int touching = 0; foreach (var o in Nb) if (o is EColi) touching++; if (touching >= 4) return; }
        if (Dividing)
        {
            // β-laktam (ampicylina) blokuje syntezę peptydoglikanu w przegrodzie → wybrzuszenie i liza
            if (W.Amp != null && (Bulge > 0 || W.Amp.Sample(X, Y) > 0.3f))
            {
                if (Bulge == 0 && IsPlayer) W.Log("Ampicylina! Przegroda nie powstaje – komórka puchnie w miejscu podziału...", "bad");
                Bulge += bdt / 200;
                if (Bulge >= 1) LyseAmp();
                return;
            }
            Pinch = Math.Min(1, Pinch + bdt / 240);
            if (Pinch >= 1) Divide();
            return;
        }
        if (W.Nutri != null)
        {
            float c = W.Nutri.Sample(X, Y);
            float g = MathF.Log(2) / 1200 * c / (c + 0.3f);
            float dL = L * g * bdt;
            float got = W.Nutri.Consume(X, Y, dL * 1.6f);
            L += got / 1.6f;
            if (IsPlayer) W.AddStat("eaten", got);
        }
        else L += L * MathF.Log(2) / W.Sc.EcoliDouble * bdt;
        if (L >= L0 * 2) Dividing = true;
    }
    void Divide()
    {
        float half = L / 2, off = L / 4, c = MathF.Cos(A), s = MathF.Sin(A);
        EColi Mk(float sg)
        {
            var e = new EColi(W, X + c * off * sg, Y + s * off * sg, A + (Sessile ? 0 : Rand(-0.15f, 0.15f)), half, Wd, Clamp(half * Rand(0.95f, 1.05f), 1.8f, 2.3f), Sessile);
            e.Lineage = Lineage || IsPlayer; e.State = St.Run; e.Timer = ExpRand(0.6f); e.Resistant = Resistant; e.KnownResistant = KnownResistant;
            return W.Add(e);
        }
        var A1 = Mk(1); Mk(-1);
        Alive = false;
        if (IsPlayer) W.Sfx("blip", X, Y);
        if (Lineage || IsPlayer) W.AddStat("divisions", 1);
        if (IsPlayer) { W.Player = null; W.SetPlayer(A1); W.Log("Podział komórki! Kontrolujesz komórkę potomną, druga pływa samodzielnie.", "good"); }
    }

    // ------------------------------------------------------------------ infekcja T4
    void LyseForeign()
    {
        int n = W.Em ? RandI(6, 12) : 3;
        for (int k = 0; k < n; k++) { ToWorld(Rand(-Hl, Hl), Rand(-Wd, Wd) * 0.3f, out float x, out float y); var q = W.Add(new OtherPhage(W, x, y, ForeignLambda)); q.X += RandN() * 0.1f; }
        if (IsPlayer) W.Log(ForeignLambda ? "Fag λ przeszedł w cykl lityczny – Twoja komórka pęka!" : "Fag T7 przejął Twoją komórkę – liza!", "bad");
        Kill();
    }
    public void StartInfection(PhageT4 ph)
    {
        if (Foreign > 0)
        {
            if (ph.IsPlayer) W.Log($"Ta komórka jest już przejęta przez faga {(ForeignLambda ? "λ" : "T7")} – DNA gospodarza zdegradowane, Twoja infekcja się nie uda. Przejmujesz inny wirion.", "bad");
            if (ph.IsPlayer) { var alt = W.Near(ph.X, ph.Y, 1e9f, e => e is PhageT4 q && q.State == St.Free); if (alt != null) { W.Player = null; W.SetPlayer(alt); W.Focus = null; } }
            return;
        }
        if (Inf != null)
        {
            Inf.Lat += Inf.Base * 0.3f; Inf.Lin++;
            if (ph.IsPlayer && !Inf.ByPlayer)
            {
                W.Log("Wykluczenie nadkażenia (białka Imm/Sp): ta komórka należy już do innego faga – Twoje DNA zablokowane. Przejmujesz najbliższy wolny wirion.", "bad");
                var alt = W.Near(ph.X, ph.Y, 1e9f, e => e is PhageT4 q && q.State == St.Free);
                if (alt != null) { W.Player = null; W.SetPlayer(alt); W.Focus = null; }
            }
            else if (ph.IsPlayer || Inf.ByPlayer) W.Log("Nadkażenie → inhibicja lizy (LIN): liza opóźniona, więcej potomstwa.", "info");
            return;
        }
        ToLocal(ph.TipX, ph.TipY, out float lx, out float ly);
        const float bas = 1500;
        Inf = new Infection { T = 0, Base = bas, Lat = bas * Rand(0.92f, 1.08f), Ex = lx, Ey = ly, ByPlayer = ph.IsPlayer || ph.Lineage };
        float hl = Hl, r = Wd / 2; int nc = W.Em ? 70 : 16;
        for (int k = 0; k < nc; k++) Inf.Caps.Add(new Capsid { X = Rand(-1, 1) * (hl + r * 0.35f) * 0.88f, Y = Rand(-1, 1) * r * 0.55f, A = Rand(0, TAU), T = Rand(0.5f, 0.86f), J = Rand(0, TAU) });
        for (int k = 0; k < 7; k++) Inf.Dna.Add(new DnaSpot { X = Rand(-hl, hl) * 0.8f, Y = Rand(-r, r) * 0.4f, T = Rand(0.2f, 0.45f), S = Rand(0.06f, 0.12f) });
        State = St.Infected;
        if (ph.IsPlayer || ph.Lineage) W.AddStat("infected", 1);
    }
    void StepInfect(float bdt)
    {
        Inf.T += bdt;
        float p = Inf.T / Inf.Lat;
        if (p > 0.9f) { Wob = (p - 0.9f) * 10; Round = Math.Min(0.3f, (p - 0.9f) * 3); }
        if (p >= 1) LysePhage();
    }
    void LysePhage()
    {
        var I = Inf;
        int burst = (int)MathF.Round(Rand(100, 150) * (1 + I.Lin * 0.4f));
        float pole = Chance(0.75f) ? Sign() : 0; // T4: liza najczęściej na biegunie
        ToWorld(pole * (Hl + Wd / 2), 0, out float px, out float py);
        int cap = W.Sc.PhageCap;
        int cur = W.Count(e => e is PhageT4 q && q.State == St.Free);
        int nShow = (int)Clamp(Math.Min(burst, cap - cur), 6, W.Em ? 120 : 30);
        var outp = new List<PhageT4>();
        for (int k = 0; k < nShow; k++)
        {
            var cp = I.Caps[k % I.Caps.Count];
            ToWorld(cp.X * 0.9f + Rand(-0.05f, 0.05f), cp.Y + Rand(-0.05f, 0.05f), out float x, out float y);
            var ph = W.Add(new PhageT4(W, x, y, Rand(0, TAU)));
            float dx = x - px, dy = y - py, d = Hypot(dx, dy); if (d < 1e-6f) d = 1;
            float sp = W.Em ? Rand(0.4f, 1.6f) : Rand(2, 6);
            ph.Kx = dx / d * sp + RandN() * sp * 0.3f; ph.Ky = dy / d * sp + RandN() * sp * 0.3f;
            ph.Lineage = I.ByPlayer;
            outp.Add(ph);
        }
        float sc = W.Em ? 1 : 3;
        W.Fx.Lyse(Mem, new FX.LyseOpt
        {
            Pieces = RandI(4, 7), Speed = 0.35f * sc, Life = 7, Lw = W.Em ? 0.012f : 0.05f,
            C = W.Em ? Rgb(170, 215, 230, 0.9f) : Rgb(25, 30, 27, 0.8f), Halo = W.Em ? Rgb(170, 215, 230, 0.18f) : Rgb(255, 255, 240, 0.2f), HasHalo = true,
            Curl = 0.8f, Spin = 0.6f, D = 0.002f, Ring = W.Em ? 0.8f : 1.5f, RingCol = W.Em ? Rgb(170, 215, 230) : Rgb(255, 255, 240),
        });
        W.Fx.Spill(px, py, W.Em ? 90 : 30, new FX.SpillOpt
        {
            Speed = 0.9f * sc, R = W.Em ? 0.008f : 0.04f, Life = 4, R0 = 0.2f, D = 0.004f, Dir = MathF.Atan2(py - Y, px - X), Spread = 1.2f,
            Cols = W.Em ? new[] { Rgb(160, 200, 215, 0.8f), Rgb(120, 160, 180, 0.8f), Rgb(200, 230, 240, 0.7f) } : new[] { Rgb(30, 35, 30, 0.7f), Rgb(220, 225, 210, 0.6f) },
        });
        W.Fx.Spill(X, Y, W.Em ? 60 : 12, new FX.SpillOpt
        {
            Speed = 0.4f * sc, R = W.Em ? 0.01f : 0.05f, Life = 5, R0 = L * 0.35f, D = 0.003f,
            Cols = W.Em ? new[] { Rgb(150, 190, 205, 0.7f) } : new[] { Rgb(30, 35, 30, 0.6f) },
        });
        W.Nutri?.Blob(X, Y, 3, 0.25f);
        W.Sfx("pop", px, py);
        Alive = false; State = St.Dead;
        if (I.ByPlayer)
        {
            W.AddStat("virions", burst); W.AddStat("lysed", 1);
            W.Log($"LIZA! Holiny + endolizyna + spaniny rozerwały osłony. Uwolniono ~{burst} wirionów T4.", "good");
        }
        for (int i = 0; i < W.Ents.Count; i++) if (W.Ents[i] is PhageT4 q && q.Host == this) q.Release();
        if (W.Focus == this)
        {
            W.Focus = null;
            if (outp.Count > 0 && I.ByPlayer)
            {
                PhageT4 best = outp[0];
                foreach (var o in outp) if (Hypot(o.X - W.CamX, o.Y - W.CamY) < Hypot(best.X - W.CamX, best.Y - W.CamY)) best = o;
                W.Player = null; W.SetPlayer(best);
            }
        }
        W.Sc.OnLysis(W, this, I.ByPlayer);
    }
    public override void Kill()
    {
        if (!Alive) return;
        for (int i = 0; i < W.Ents.Count; i++) if (W.Ents[i] is PhageT4 q && q.Host == this) q.Release();
        ReleaseHeld();
        LyseBdello();
    }
    public void LyseBdello()
    {
        W.Fx.Lyse(Mem, new FX.LyseOpt { Pieces = RandI(3, 5), Speed = 1.0f, Life = 6, Lw = 0.05f, C = Rgb(30, 34, 30, 0.75f), Halo = Rgb(255, 255, 240, 0.2f), HasHalo = true, Curl = 0.7f, D = 0.003f, Ring = 1.5f, RingCol = Rgb(255, 255, 240) });
        W.Fx.Spill(X, Y, 18, new FX.SpillOpt { Speed = 1.5f, R = 0.04f, Life = 3.5f, R0 = Rb * 0.6f, D = 0.01f, Cols = new[] { Rgb(30, 35, 30, 0.6f), Rgb(230, 230, 215, 0.5f) } });
        W.Nutri?.Blob(X, Y, 3, 0.2f);
        W.Sfx("pop", X, Y, 0.8f);
        Alive = false; State = St.Dead;
    }
    void LyseAmp()
    {
        W.Fx.Lyse(Mem, new FX.LyseOpt { Pieces = RandI(3, 5), Speed = 1.2f, Life = 6, Lw = 0.05f, C = Rgb(30, 34, 30, 0.75f), Halo = Rgb(255, 255, 240, 0.2f), HasHalo = true, Curl = 0.8f, D = 0.003f, Ring = 1.2f, RingCol = Rgb(255, 220, 200) });
        W.Fx.Spill(X, Y, 16, new FX.SpillOpt { Speed = 2, R = 0.04f, Life = 3.5f, R0 = 0.3f, D = 0.01f, Cols = new[] { Rgb(30, 35, 30, 0.6f), Rgb(230, 230, 215, 0.5f) } });
        W.Sfx("pop", X, Y);
        if (IsPlayer || Lineage) W.AddStat("ampkills", 1);
        if (IsPlayer) W.Log("Liza wywołana ampicyliną (liza wybrzuszeniowa w miejscu podziału).", "bad");
        Alive = false; State = St.Dead;
    }
    public int ProgenyCount() => (int)Clamp(MathF.Round(L * 1.6f + Rand(-0.5f, 0.5f)), 3, 6);

    // ------------------------------------------------------------------ rysowanie
    void DrawFlagella(Painter p, View v, Color col, float lwMin)
    {
        float hl = Hl, r = Wd / 2, b = Smooth(Bund);
        p.Local(X, Y, A);
        float lw = Math.Max(0.022f, v.Px * lwMin);
        var buf = p.Buf2;
        foreach (var f in _flags)
        {
            float ax = f.S * hl, ay = f.Side * r * 0.85f, ang;
            if (Sessile) ang = f.Fa;
            else
            {
                float runA = MathF.Atan2(-ay * 0.2f, (-hl - r - 2.5f) - ax);
                float tumA = f.Side * f.Spl;
                ang = runA + WrapA(tumA - runA) * (1 - b);
            }
            float ux = MathF.Cos(ang), uy = MathF.Sin(ang);
            float lam = Lerp(1.0f, 2.3f, b), Am = Lerp(0.14f, 0.2f, b);
            const int K = 24;
            for (int k = 0; k <= K; k++)
            {
                float d = (float)k / K * f.Len, env = Math.Min(1, d / 0.6f);
                float off = Am * env * MathF.Sin(TAU * d / lam - f.Ph);
                buf[k] = new Vector2(ax + ux * d - uy * off, ay + uy * d + ux * off);
            }
            p.Thin(new ReadOnlySpan<Vector2>(buf, 0, K + 1), false, lw, col);
        }
    }

    static readonly Color FlagPh = Rgb(35, 40, 36, 0.28f), HaloPh = Rgb(255, 255, 240, 0.30f), BodyPh = Rgb(44, 50, 46), EdgePh = Rgb(12, 14, 12, 0.9f);

    public override void Draw(Painter p, View v)
    {
        bool eaten = State == St.Cup || State == St.Vac;
        float oldA = p.Alpha;
        if (eaten) p.Alpha = 1 - Dig * 0.9f;
        DrawInner(p, v);
        if (KnownResistant && W.Em)
        {
            p.Identity();
            int n = Mem.Smooth(p.Shape);
            p.Dashed(new ReadOnlySpan<Vector2>(p.Shape, 0, n), true, 0.02f, 0.05f, 0.05f, Rgb(255, 110, 90, 0.55f));
        }
        p.Alpha = oldA;
    }
    void DrawInner(Painter p, View v)
    {
        if (W.Em) { DrawEM(p, v); return; }
        if (Inf == null && State != St.Bdelloplast && State != St.Cup && State != St.Vac) DrawFlagella(p, v, FlagPh, 0.9f);
        p.Identity();
        int n = Mem.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 0.75f, HaloPh);
        p.FillFan(sh, X, Y, Mul(BodyPh, Opacity));
        p.Stroke(sh, true, 0.05f, EdgePh);
        p.Local(X, Y, A);
        float hl = Hl, r = Wd / 2, nuc = Opacity;
        if (Inf != null) nuc *= 1 - Smooth((Inf.T / Inf.Lat - 0.05f) / 0.25f);
        if (nuc > 0.02f && Round < 0.5f)
        {
            var nc = Rgb(150, 160, 148, 0.33f * nuc);
            if (Pinch > 0.3f || L > L0 * 1.6f)
            {
                p.FillEllipse(-hl * 0.5f, 0, hl * 0.35f + r * 0.1f, r * 0.4f, 0, nc);
                p.FillEllipse(hl * 0.5f, 0, hl * 0.35f + r * 0.1f, r * 0.4f, 0, nc);
            }
            else p.FillEllipse(0, 0, hl * 0.75f + r * 0.25f, r * 0.42f * (1 - Pinch), 0, nc);
        }
        if (Pinch > 0.05f) p.Line(0, -r * (1 - Pinch * 0.9f), 0, r * (1 - Pinch * 0.9f), 0.06f, Rgb(10, 12, 10, 0.6f * Pinch));
        if (Inf != null)
        {
            float q = Inf.T / Inf.Lat;
            foreach (var c in Inf.Caps) if (q >= c.T) p.FillCircle(c.X + MathF.Sin(W.T * 3 + c.J) * 0.02f, c.Y, 0.06f, Rgb(15, 17, 15, 0.85f));
        }
    }

    void BuildDetail()
    {
        float hl = Hl, r = Wd / 2;
        _nucleoid = new List<Vector2[]>();
        for (int k = 0; k < 34; k++)
        {
            float x = Rand(-hl * 0.8f, hl * 0.8f), y = Rand(-r * 0.35f, r * 0.35f);
            var pts = new List<Vector2> { new(x, y) };
            for (int q = 0; q < 3; q++)
            {
                float x2 = x + Rand(-0.25f, 0.25f), y2 = Clamp(y + Rand(-0.12f, 0.12f), -r * 0.45f, r * 0.45f);
                float cx = x + Rand(-0.15f, 0.15f), cy = y + Rand(-0.15f, 0.15f);
                for (int s = 1; s <= 3; s++) { float t = s / 3f, u = 1 - t; pts.Add(new(u * u * x + 2 * u * t * cx + t * t * x2, u * u * y + 2 * u * t * cy + t * t * y2)); }
                x = x2; y = y2;
            }
            _nucleoid.Add(pts.ToArray());
        }
        _pili = new List<Vector2[]>();
        var m = Mem;
        for (int k = 0; k < 45; k++)
        {
            int i = RandI(0, m.N - 1); float rx = m.Rx[i], ry = m.Ry[i];
            float ang = MathF.Atan2(ry * (hl + r) / r, rx) + RandN() * 0.25f, len = Rand(0.25f, 0.9f);
            float cx = rx + MathF.Cos(ang) * len * 0.5f + RandN() * 0.05f, cy = ry + MathF.Sin(ang) * len * 0.5f + RandN() * 0.05f;
            float ex = rx + MathF.Cos(ang) * len, ey = ry + MathF.Sin(ang) * len;
            var pts = new Vector2[4];
            for (int s = 0; s < 4; s++) { float t = s / 3f, u = 1 - t; pts[s] = new(u * u * rx + 2 * u * t * cx + t * t * ex, u * u * ry + 2 * u * t * cy + t * t * ey); }
            _pili.Add(pts);
        }
        var rib = new List<Vector2>();
        for (int k = 0; k < 380; k++)
        {
            float x = Rand(-hl - r, hl + r), y = Rand(-r, r);
            if (MathF.Abs(x) > hl && Sq(MathF.Abs(x) - hl) + y * y > r * r * 0.72f) continue;
            if (y * y > r * r * 0.8f) continue;
            rib.Add(new(x, y));
        }
        _ribo = rib.ToArray();
    }

    static readonly Color FlagEm = Rgb(160, 205, 220, 0.32f), PiliEm = Rgb(160, 205, 220, 0.35f), LpsEm = Rgb(150, 200, 215, 0.45f),
        CytoIn = Rgb(58, 96, 112), CytoOut = Rgb(38, 66, 82), OmEm = Rgb(185, 225, 238, 0.95f), PgEm = Rgb(120, 170, 185, 0.6f),
        ImEm = Rgb(185, 225, 238, 0.85f), RiboEm = Rgb(150, 200, 215, 0.28f), NucEm = Rgb(210, 235, 245, 0.16f);

    void DrawEM(Painter p, View v)
    {
        if (_ribo == null) BuildDetail();
        var I = Inf; float q = I != null ? I.T / I.Lat : 0;
        if (I == null || q < 0.6f) DrawFlagella(p, v, FlagEm, 1.2f);
        p.Local(X, Y, A);
        foreach (var pl in _pili) p.Thin(pl, false, 0.008f, PiliEm);
        p.Identity();
        Mem.Normals();
        int n = Mem.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.Dashed(sh, true, 0.06f, 0.008f, 0.016f, LpsEm);
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { Mul(CytoIn, Opacity), Mul(CytoOut, Opacity) };
        p.FillRadial(sh, X, Y, ts, cs);
        p.Stroke(sh, true, 0.009f, OmEm);
        int n2 = Mem.SmoothInset(p.Shape2, 0.02f, p.Tx, p.Ty);
        p.Stroke(new ReadOnlySpan<Vector2>(p.Shape2, 0, n2), true, 0.004f, PgEm);
        n2 = Mem.SmoothInset(p.Shape2, 0.035f, p.Tx, p.Ty);
        p.Stroke(new ReadOnlySpan<Vector2>(p.Shape2, 0, n2), true, 0.008f, ImEm);
        p.Local(X, Y, A);
        var rc = Mul(RiboEm, Opacity);
        foreach (var r in _ribo) p.Dot(r.X, r.Y, 0.012f, rc);
        float nucA = 1 - (I != null ? Smooth((q - 0.06f) / 0.22f) : 0);
        if (nucA > 0.01f) { var nc = Mul(NucEm, nucA); foreach (var pl in _nucleoid) p.Thin(pl, false, 0.008f, nc); }
        if (I != null) DrawInfectEM(p, q);
    }

    void DrawInfectEM(Painter p, float q)
    {
        var I = Inf; float t = W.T;
        var buf = p.Buf2;
        if (q < 0.12f)
        {
            float k = Smooth(q / 0.1f);
            float dx = -I.Ex, dy = -I.Ey, L2 = Hypot(dx, dy); if (L2 < 1e-6f) L2 = 1;
            int steps = (int)(30 * k);
            buf[0] = new Vector2(I.Ex, I.Ey);
            for (int s = 1; s <= steps; s++)
            {
                float f = s / 30f, w = MathF.Sin(s * 1.7f + t * 2) * 0.03f;
                buf[s] = new Vector2(I.Ex + dx * f * 0.8f + w * (-dy / L2), I.Ey + dy * f * 0.8f + w * (dx / L2));
            }
            if (steps > 0) p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, steps + 1), false, 0.008f, PhagePal.Em.Dna);
        }
        if (q > 0.15f)
        {
            var dc = Rgb(120, 230, 255, 0.45f);
            foreach (var d in I.Dna)
            {
                if (q < d.T) continue;
                float g = Math.Min(1, (q - d.T) / 0.2f) * (q > 0.85f ? Math.Max(0, 1 - (q - 0.85f) / 0.1f) : 1);
                if (g <= 0.01f) continue;
                for (int s = 0; s < 5; s++)
                {
                    float a = s * 1.3f + d.X * 7;
                    float x0 = d.X + MathF.Cos(a) * d.S * g, y0 = d.Y + MathF.Sin(a) * d.S * g * 0.6f;
                    float x1 = d.X + MathF.Cos(a + 2) * d.S * g, y1 = d.Y + MathF.Sin(a + 2) * d.S * g * 0.6f;
                    for (int j = 0; j < 6; j++) { float tt = j / 5f, u = 1 - tt; buf[j] = new Vector2(u * u * x0 + 2 * u * tt * d.X + tt * tt * x1, u * u * y0 + 2 * u * tt * d.Y + tt * tt * y1); }
                    p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, 6), false, 0.008f, dc);
                }
            }
        }
        foreach (var c in I.Caps)
        {
            if (q < c.T) continue;
            float age = (q - c.T) / 0.12f, full = Clamp(age - 0.4f, 0, 1);
            bool tails = q > 0.82f;
            p.Push();
            p.Translate(c.X + MathF.Sin(t * 1.3f + c.J) * 0.01f, c.Y + MathF.Cos(t * 1.1f + c.J) * 0.01f);
            p.Rotate(c.A);
            p.Scale(Math.Min(1, 0.4f + age));
            if (tails) PhageT4.DrawT4(p, PhagePal.Em, 0, 0, 0, null, 0.005f, 0.9f, 2);
            else PhageT4.DrawHead(p, PhagePal.Em, 0, Rgb(222, 150, 72, 0.15f + full * 0.75f), 0.005f);
            p.Pop();
        }
    }
}

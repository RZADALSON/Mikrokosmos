namespace Mikro;

/// <summary>
/// Świdrowiec Trypanosoma brucei, forma krwiopochodna. Smukła (≈ 20–30 × 1,5–3 µm) dzieli się co ~6 h;
/// krępa (krótsza, szersza, prawie bez wolnej wici) nie dzieli się i czeka na muchę tse-tse.
/// Wić biegnie wzdłuż ciała (błona falująca) i wychodzi wolna z przodu – fala biegnie od czubka ku tyłowi,
/// więc świdrowiec płynie wicią naprzód („ciągacz”). Ruch jest niezbędny do przeżycia (Broadhead i in. 2006):
/// opływ przesuwa kompleksy VSG–przeciwciało ku kieszonce wiciowej, gdzie są wchłaniane (Engstler i in. 2007).
/// </summary>
public sealed class Trypanosome : Ent
{
    public int Vsg;
    /// <summary>Opłaszczenie przeciwciałami 0..1, cykl 0..1, podział 0..1, przemiana w formę krępą 0..1, wiek formy krępej (s bio).</summary>
    public float Ab, Growth, Div, Stumpy, Age, Beat, Roll, V0, Fwd;
    public bool Reverse;
    public string Cause = "";
    float _wA, _wT, _revT, _life;
    readonly float _seed;
    public bool IsStumpy => Stumpy >= 1;
    float Len => Lerp(25, 18, Smooth(Stumpy));
    float Hw => Lerp(1.4f, 2.2f, Smooth(Stumpy));
    public override float Length => Len;
    public override float Radius => Hw;
    public override float Dipole => State == St.Swim ? -1 : 0;
    public override bool CanControl() => Alive && State == St.Swim && Holder == null;
    public override bool Collides(Ent o) => State == St.Swim && Holder == null;

    public Trypanosome(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Trypano; Layer = 2; Name = "świdrowiec"; Latin = "Trypanosoma brucei"; Soft = 0.25f; Mass = 2;
        R = 14; V0 = Rand(9, 14); D = 0.01f; FlowK = 1; State = St.Swim;
        _seed = Rand(0, 100); Beat = Rand(0, TAU); Roll = Rand(0, TAU); _wA = a; Growth = Rand(0, 0.9f);
        _life = Rand(48, 72) * 3600;
    }
    public override void UpdatePrim()
    {
        if (Holder != null || State != St.Swim) { SetCircle(0.5f); return; }
        float st = Smooth(Stumpy);
        AxisParts(3, Len * 0.47f, Hw, t => MathF.Pow(Math.Max(0.05f, 1 - t * t), st > 0.5f ? 0.35f : 0.6f), 0, null);
    }
    public override void Kill()
    {
        if (!Alive) return;
        if (IsPlayer && !W.Preview && Cause.Length > 0) W.Log(Cause, "bad");
        W.Fx.Spill(X, Y, 14, new FX.SpillOpt { Speed = 3, R = 0.2f, Life = 3, R0 = Len * 0.3f, D = 0.05f, Cols = new[] { Rgb(60, 62, 55, 0.5f), Rgb(235, 238, 225, 0.5f) } });
        W.Sfx("pop", X, Y, 0.5f);
        Alive = false;
    }

    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        var sc = W.Sc as ScTryp;
        Beat += gdt * TAU * (IsStumpy ? 11 : 18) * (Reverse ? -1 : 1); Roll += gdt * TAU * 1.2f;
        if (Holder != null || State != St.Swim) { Vx = Vy = 0; Fwd = 0; return; }
        bool player = IsPlayer && !W.Preview;
        var I = W.Input;
        // ------------------------------------------------ przeciwciała: wiązanie zależne od miana, usuwanie napędzane pływaniem naprzód
        float titer = sc?.Titer(Vsg) ?? 0;
        // usuwanie: przy pełnej prędkości naprzód połowa kompleksów znika w ~30 s; wstecz i w miejscu – wcale
        float kon = titer / 60f, kcl = Clamp(Fwd / Math.Max(1, V0), 0, 1.2f) / 40f;
        Ab = Clamp(Ab + (kon * (1 - Ab) - kcl * Ab) * gdt, 0, 1);
        // dopełniacz: im więcej przeciwciał na płaszczu i im wyższe miano, tym szybciej błona zostaje przedziurawiona
        if (Ab > 0.55f && !W.Preview)
        {
            Cause = "Dopełniacz: przeciwciała pokryły płaszcz VSG szybciej, niż ruch zdążył je usunąć – błona została przedziurawiona.";
            Damage(gdt * (Ab - 0.55f) * titer * 0.1f);
            if (!Alive) return;
        }
        else if (Ab < 0.4f) Hp = Math.Min(1, Hp + gdt * 0.02f);
        // ------------------------------------------------ cykl życiowy (czas procesów życiowych)
        if (!W.Preview && sc != null)
        {
            if (Stumpy <= 0)
            {
                if (Div > 0) { Div += bdt / (1.2f * 3600); if (Div >= 1) Divide(sc); }
                else if (player || sc.Room)
                {
                    Growth += bdt / (6 * 3600);
                    if (Growth >= 1) { Growth = 1; Div = 0.001f; }
                }
                // SIF (sygnał gęstości populacji) → zatrzymanie cyklu w G1 i przemiana w formę krępą
                if (Div <= 0 && Chance(sc.CommitRate * bdt))
                {
                    Stumpy = 0.001f;
                    if (player) W.Log("SIF: populacja jest gęsta – Twoja komórka zatrzymuje cykl w G1 i zaczyna przemianę w formę krępą (PAD1, rozbudowa mitochondrium). Formy krępe nie dzielą się – czekają na muchę tse-tse.", "info");
                }
            }
            else if (Stumpy < 1)
            {
                Stumpy = Math.Min(1, Stumpy + bdt / (24 * 3600));
                if (Stumpy >= 1 && player) W.Log("Forma krępa gotowa. Żyje tylko ~2–3 doby – płyń do naczyń skóry (strefa „skóra” na radarze), gdzie kłuje mucha tse-tse.", "good");
            }
            else
            {
                Age += bdt;
                if (Age > _life) { Cause = "Forma krępa żyje tylko 2–3 doby – nie doczekała się muchy tse-tse."; Kill(); return; }
            }
        }
        // ------------------------------------------------ pływanie (świdrowiec nie może się zatrzymać – ruch jest niezbędny do życia)
        int rbc = 0; foreach (var o in Nb) if (o is RBC) rbc++;
        float mag;
        if (player)
        {
            Reverse = I.ActionHeld;
            if (I.Mag > 0.05f) A = Turn(A, I.Ang, 2.4f * gdt);
            mag = I.Mag > 0.05f ? 1 : 0.6f;
        }
        else
        {
            _wT -= gdt; if (_wT <= 0) { _wA = A + RandN() * 0.9f; _wT = ExpRand(2.5f); }
            _revT -= gdt;
            if (_revT <= 0) { Reverse = !Reverse && Chance(0.25f); _revT = Reverse ? Rand(0.3f, 1.2f) : ExpRand(4); }
            A = Turn(A, _wA, 1.2f * gdt);
            mag = 0.8f;
        }
        // w tłoku erytrocytów ruch jest bardziej kierunkowy (Heddergott i in. 2012); bez przeszkód – częstsze obroty
        A += RandN() * MathF.Sqrt(2 * (rbc > 0 ? 0.03f : 0.12f) * gdt);
        float v = V0 * mag * (Reverse ? -0.45f : 1) * (IsStumpy ? 0.65f : 1) * (Div > 0.6f ? 0.6f : 1) * (0.5f + 0.5f * Hp) * (1 + 0.15f * Math.Min(1, rbc / 3f));
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v; Fwd = v;
    }

    void Divide(ScTryp sc)
    {
        Div = 0; Growth = Rand(0, 0.1f);
        // zmiana wariantu VSG (w grze ~1 na 8 podziałów; w naturze 10⁻²–10⁻⁶ na podział)
        if (Chance(0.12f))
        {
            Vsg = sc.NewVariant(); Ab *= 0.3f;
            if (IsPlayer) { W.Log($"Zmiana płaszcza! Rekombinacja umieściła w miejscu ekspresji nowy gen – teraz VSG-{Vsg + 1}. Stare przeciwciała go nie rozpoznają.", "good"); W.Sfx("goal", X, Y, 0.5f); }
            else if (Lineage) W.Log($"W Twojej linii pojawił się nowy wariant płaszcza: VSG-{Vsg + 1}.", "info");
        }
        float dx = MathF.Cos(A + PI / 2) * 2.5f, dy = MathF.Sin(A + PI / 2) * 2.5f;
        var d = W.Add(new Trypanosome(W, X + dx, Y + dy, A + Rand(0.4f, 0.8f) * Sign()) { Vsg = Vsg, Ab = Ab * 0.5f, V0 = Math.Clamp(V0 + RandN(), 8, 15), Growth = Rand(0, 0.1f) });
        X -= dx; Y -= dy; Ab *= 0.5f;
        W.Born(this, d);
        if (IsPlayer) W.AddStat("t_div", 1);
    }

    // ------------------------------------------------ rysowanie
    const int NS = 26;
    static readonly Color BodyF = Rgb(118, 124, 112, 0.5f), BodyE = Rgb(40, 44, 38, 0.8f), Halo = Rgb(255, 255, 240, 0.3f),
        FlagC = Rgb(35, 38, 34, 0.75f), NucC = Rgb(80, 82, 100, 0.55f), KinC = Rgb(25, 25, 35, 0.85f), AbC = Rgb(255, 225, 120, 0.75f);
    readonly Vector2[] _c = new Vector2[NS + 1], _n = new Vector2[NS + 1];
    readonly float[] _w = new float[NS + 1];

    void Centerline()
    {
        float L = Len, st = Smooth(Stumpy), lam = Lerp(9, 11, st);
        for (int i = 0; i <= NS; i++)
        {
            float s = L * i / NS, u = s / L;
            // fala biegnie od czubka ku tyłowi; wychylenie największe z przodu
            float env = (0.2f + 1.4f * u * u) * Lerp(1, 0.55f, st);
            _c[i] = new Vector2(s - L * 0.52f, env * MathF.Sin(TAU * s / lam + Beat));
            float prof = MathF.Pow(MathF.Sin(PI * Clamp(u * 0.96f + 0.02f, 0, 1)), st > 0.5f ? 0.35f : 0.7f);
            if (u > 0.75f) prof *= 1 - (u - 0.75f) / 0.25f * Lerp(0.7f, 0.4f, st);
            _w[i] = Hw * prof;
        }
        for (int i = 0; i <= NS; i++)
        {
            var t = _c[Math.Min(NS, i + 1)] - _c[Math.Max(0, i - 1)]; float l = t.Length();
            _n[i] = l > 1e-5f ? new Vector2(-t.Y / l, t.X / l) : new Vector2(0, 1);
        }
    }
    Vector2 At(float u, float side)
    {
        float f = Clamp(u, 0, 1) * NS; int i = Math.Min(NS - 1, (int)f); float k = f - i;
        var c = _c[i].Lerp(_c[i + 1], k); var n = _n[i].Lerp(_n[i + 1], k); float w = Lerp(_w[i], _w[i + 1], k);
        return c + n * w * side;
    }
    void DrawBody(Painter p, View v, bool detail, bool second)
    {
        var poly = p.Shape;
        for (int i = 0; i <= NS; i++) poly[i] = _c[i] + _n[i] * _w[i];
        for (int i = 0; i <= NS; i++) poly[NS + 1 + i] = _c[NS - i] - _n[NS - i] * _w[NS - i];
        var sh = new ReadOnlySpan<Vector2>(poly, 0, 2 * NS + 2);
        p.SoftStroke(sh, true, 1.6f, Halo);
        // ciało jest wygięte (niewypukłe) – wypełnienie odcinkami, nie wachlarzem z jednego środka
        Span<Vector2> quad = stackalloc Vector2[4];
        var fc = IsStumpy ? Rgb(100, 106, 96, 0.58f) : BodyF;
        for (int i = 0; i < NS; i++)
        {
            quad[0] = poly[i]; quad[1] = poly[i + 1]; quad[2] = poly[2 * NS + 1 - (i + 1)]; quad[3] = poly[2 * NS + 1 - i];
            p.FillFan(quad, (_c[i].X + _c[i + 1].X) / 2, (_c[i].Y + _c[i + 1].Y) / 2, fc, false);
        }
        p.Stroke(sh, true, 0.12f, BodyE);
        float lw = Math.Max(v.Px * 0.8f, 0.28f);
        // wić: wychodzi z kieszonki przy kinetoplaście, owija się pół zwoju wokół ciała (błona falująca) i wychodzi wolna z przodu
        var fl = p.Buf2; int nf = 0;
        for (int i = 0; i <= 20; i++)
        {
            float u = 0.13f + 0.87f * i / 20;
            fl[nf++] = At(u, 0.95f * MathF.Cos(PI * (u - 0.13f) / 0.87f + Roll * 0.15f));
        }
        float free = Lerp(6, 1.2f, Smooth(Stumpy)), lam = Lerp(9, 11, Smooth(Stumpy)), L = Len;
        var tip = _c[NS];
        // wolna wić: fala biegnie dalej od czubka – gęsto próbkowana, żeby zgięcia były gładkie
        const int NF = 14;
        for (int i = 1; i <= NF; i++)
        {
            float s = L + free * i / NF, env = (0.2f + 1.4f * Sq(s / L)) * Lerp(1, 0.55f, Smooth(Stumpy));
            fl[nf++] = new Vector2(tip.X + free * i / NF, env * MathF.Sin(TAU * s / lam + Beat));
        }
        p.Stroke(new ReadOnlySpan<Vector2>(fl, 0, nf), false, lw, FlagC, true);
        if (!detail) return;
        var nu = At(0.5f, 0); p.FillEllipse(nu.X, nu.Y, 1.5f, Math.Min(1.0f, Hw * 0.7f), 0, NucC);
        var ki = At(0.1f, 0); p.Dot(ki.X, ki.Y, 0.38f, KinC);
        if (!second && Div > 0.35f) { var n2 = At(0.62f, 0); p.FillEllipse(n2.X, n2.Y, 1.3f, Math.Min(0.9f, Hw * 0.6f), 0, NucC); var k2 = At(0.16f, 0.3f); p.Dot(k2.X, k2.Y, 0.34f, KinC); }
    }
    public override void Draw(Painter p, View v)
    {
        float oa = p.Alpha; if (Holder != null) p.Alpha = Math.Max(0, 1 - Dig);
        p.Local(X, Y, A);
        Centerline();
        bool detail = Len / Math.Max(v.Px, 1e-6f) > 30;
        if (Div > 0.62f)
        {
            // cytokineza od przodu ku tyłowi: córki rozchodzą się, połączone tylnymi końcami
            float phi = (Div - 0.62f) * 1.3f; var c0 = _c[0];
            for (int sd = -1; sd <= 1; sd += 2)
            {
                p.Push(); p.Translate(c0.X, c0.Y); p.Rotate(phi * sd * 0.5f); p.Translate(-c0.X, -c0.Y);
                DrawBody(p, v, detail, sd > 0); p.Pop();
            }
        }
        else DrawBody(p, v, detail, false);
        // przeciwciała na płaszczu VSG: opływ przesuwa je ku kieszonce wiciowej (z tyłu), gdzie są wchłaniane
        if (detail && Ab > 0.03f)
        {
            int n = (int)(Ab * 26); float drift = W.T * Clamp(Fwd / Math.Max(1, V0), 0, 1.2f) * 0.35f;
            for (int k = 0; k < n; k++)
            {
                float h = Hash2(k, Id, 5), u = 0.14f + 0.84f * (1 - ((h + drift) % 1f));
                var q = At(u, Hash2(k, Id, 9) < 0.5f ? 0.85f : -0.85f);
                p.Dot(q.X, q.Y, 0.22f, AbC);
            }
        }
        p.Alpha = oa;
    }
}

/// <summary>
/// Komórka Kupffera – makrofag osiadły w zatokach wątroby. Pochłania świdrowce opłaszczone przeciwciałami IgM/IgG
/// (to wątroba usuwa większość pasożytów przy każdej fali odpowiedzi odpornościowej – Macaskill i in. 1980).
/// </summary>
public sealed class Kupffer : Ent
{
    const int NN = 48;
    readonly float _r0, _seed; readonly float[] _rr = new float[NN];
    readonly List<Trypanosome> _eat = new();
    public Kupffer(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.Kupffer; Layer = 1; Fixed = true; FlowK = 0; Soft = 0.4f; Name = "komórka Kupffera"; Latin = "makrofag osiadły wątroby";
        _r0 = Rand(9, 11); R = _r0 * 1.9f; Mass = 60; _seed = Rand(0, 99);
        Mem = new Membrane(NN) { MaxDef = 3, Stiff = 6, Damping = 0.6f }; MemK = 3; MemS = 0.4f;
        Shape(); Mem.Follow(x, y, A, 1, 1);
    }
    void Shape()
    {
        float t = W.T;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, r = _r0 * (1 + 0.06f * Noise1(th * 3 + t * 0.2f + _seed));
            // wypustki sięgające w światło zatoki
            for (int k = 0; k < 5; k++) r += _r0 * 0.55f * Gauss(WrapA(th - (k * 1.26f + _seed + 0.2f * MathF.Sin(t * 0.3f + k))), 0.16f) * (0.6f + 0.4f * MathF.Sin(t * 0.4f + k * 2));
            _rr[i] = r;
        }
        foreach (var e in _eat) if (e.Alive) { float a = MathF.Atan2(e.Y - Y, e.X - X) - A; for (int i = 0; i < NN; i++) _rr[i] = Math.Max(_rr[i], _r0 * 1.3f * Gauss(WrapA(i * TAU / NN - a), 0.5f)); }
        Mem.Radial(_rr);
    }
    public override void UpdatePrim() => RadialParts(_rr, NN, 6);
    public override bool Collides(Ent o) => o.Holder != this;
    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        if ((W.Frame + Id) % 3 == 0) Shape();
        for (int i = _eat.Count - 1; i >= 0; i--)
        {
            var e = _eat[i];
            if (!e.Alive) { _eat.RemoveAt(i); continue; }
            e.X += (X - e.X) * Damp(0.8f, gdt); e.Y += (Y - e.Y) * Damp(0.8f, gdt);
            e.Dig += bdt / (1.5f * 3600);
            if (e.Dig >= 1) { e.Cause = "Komórka Kupffera strawiła opłaszczonego świdrowca w fagolizosomie."; W.AddStat("kupffer", 1); e.Kill(); _eat.RemoveAt(i); }
        }
    }
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (o is not Trypanosome t || t.Holder != null || t.State != St.Swim || t.Ab < 0.35f || W.Preview) return;
        t.Holder = this; t.State = St.Cup; t.Dig = 0; _eat.Add(t);
        if (t.IsPlayer) W.Log("Komórka Kupffera rozpoznała przeciwciała (receptory Fc i dopełniacza) i pochłonęła Twojego świdrowca.", "bad");
    }
    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape, 2);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 2.6f, Rgb(255, 255, 240, 0.25f));
        Span<float> ts = stackalloc float[] { 0f, 0.6f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(120, 116, 100, 0.55f), Rgb(135, 130, 112, 0.45f), Rgb(185, 180, 160, 0.22f) };
        p.FillRadial(sh, X, Y, ts, cs);
        p.Stroke(sh, true, 0.14f, Rgb(45, 42, 35, 0.7f));
        BodyLocal(p);
        p.FillEllipse(-_r0 * 0.25f, 0.5f, _r0 * 0.42f, _r0 * 0.3f, 0.4f, Rgb(80, 78, 98, 0.5f));
        for (int i = 0; i < 16; i++) { float th = i * 2.4f + _seed, r = _r0 * (0.35f + 0.5f * ((i * 37 % 10) / 10f)); p.Dot(MathF.Cos(th) * r, MathF.Sin(th) * r, 0.35f, i % 2 == 0 ? Rgb(60, 55, 45, 0.5f) : Rgb(240, 235, 210, 0.5f)); }
    }
}

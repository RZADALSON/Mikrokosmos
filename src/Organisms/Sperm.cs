namespace Mikro;

/// <summary>
/// Plemnik człowieka. Główka spłaszczona jak łopatka ≈ 4,5 × 3 µm (akrosom na 40–70% przedniej części),
/// wstawka ≈ 4 µm z mitochondriami, witka ≈ 45 µm, odcinek końcowy ≈ 5 µm – razem ≈ 55 µm (WHO 2021).
/// Wić bije ≈ 15–25 Hz falą biegnącą ku tyłowi; główka kołysze się na boki (ALH ≈ 2–5 µm) i obraca wokół osi.
/// Pływanie progresywne ≈ 25–60 µm/s. Fizyka (niska liczba Reynoldsa, „pchacz”):
///  • ściany przyciągają plemnik hydrodynamicznie – płynie wzdłuż nich (Elgeti i in. 2010; Denissenko i in. 2012),
///  • w przepływie ścinającym przy ścianie obraca się pod prąd – reotaksja (Miki i Clapham 2013; Kantsler i in. 2014),
///  • po kapacytacji hiperaktywacja: asymetryczne, silne bicie – tor w pętle, większa siła (Ho i Suarez 2001),
///  • termotaksja ku cieplejszej bańce jajowodu (Bahat i in. 2003), chemotaksja do progesteronu przy wzgórku (Lishko i in. 2011).
/// </summary>
public sealed class Sperm : Ent
{
    /// <summary>Ruchliwość wg WHO 2021: postępowa (PR), niepostępowa (NP – bicie wici bez postępu, ciasne kółka), brak ruchu (IM).</summary>
    public enum Motil { Progressive, NonProgressive, Immotile }
    /// <summary>
    /// Budowa (morfologia). W nasieniu zdrowego mężczyzny większość plemników ma wady budowy – wg ścisłych kryteriów WHO
    /// prawidłowych bywa kilka–kilkanaście procent (dolna norma 4%). W 400× widać m.in. wydłużoną lub bezkształtną główkę,
    /// główkę za dużą / za małą, dwie główki, zagiętą szyjkę, kroplę cytoplazmy na wstawce, zwiniętą albo podwójną witkę.
    /// </summary>
    public enum Morph { Normal, Tapered, Amorphous, Macro, Micro, DoubleHead, BentNeck, Droplet, Coiled, DoubleTail }
    public Motil Mot = Motil.Progressive; public Morph Form = Morph.Normal;
    float _bend, _neck, _fade = 1;
    /// <summary>Plemnik wpływa do płaszczyzny ostrości z głębi (z góry albo z dołu) – obraz wyostrza się w ~1 s.</summary>
    public void FadeIn() => _fade = 0;
    public float V0, Beat, Roll, Energy = 1, Cap, Zona, Hyper, AttachT, DeadT;
    /// <summary>Pomiar ruchu jak w analizatorze CASA (próbkowanie co klatkę, okno 1 s): prędkość krzywoliniowa i prostoliniowa główki.</summary>
    public float Vcl, Vsl;
    /// <summary>Krzywizna własna toru (1/µm): chiralne bicie wici przy powierzchni zakrzywia tor w łuki (Elgeti i in. 2010).</summary>
    public float Curv;
    readonly Queue<(Vector2 p, float t)> _track = new();
    float _mucus;
    public bool Acrosome, Hyperactive, Fused;
    float _capK, _want, _turnT, _alh;
    public override float Length => 55;
    public override float Radius => 1.6f;
    public override float Dipole => State == St.Swim && V0 > 0 ? 1 : 0;
    /// <summary>Wić bije z tyłu – „pchacz” (Gaffney i in. 2011).</summary>
    public override bool CanControl() => Alive && State is St.Swim or St.Attach or St.Enter;
    public override bool Collides(Ent o) => State is St.Swim or St.Attach && o is not Sperm;

    public Sperm(World w, float x, float y, float a, bool fast = false) : base(w, x, y, a)
    {
        Kind = Kind.Sperm; Layer = 3; Name = "plemnik"; Latin = "Homo sapiens (gameta męska)"; Soft = 0.1f;
        V0 = fast ? Rand(48, 56) : Rand(30, 52); R = 3.2f; Mass = 0.4f; FlowK = 1; State = St.Swim;
        D = 0.05f; Dr = 0; Beat = Rand(0, TAU); Roll = Rand(0, TAU); _want = a; _alh = Rand(2f, 5f); Curv = Rand(1f / 160, 1f / 60) * Sign();
        // kapacytacja trwa godziny i jest bardzo różna u różnych plemników – tylko część jest gotowa jednocześnie
        _capK = fast ? 1 : Rand(0.35f, 0.9f);
        _bend = RandN() * 0.0015f;
    }
    /// <summary>
    /// Plemnik z ejakulatu: ruchliwość i budowa jak w typowym nasieniu płodnego mężczyzny (WHO 2021 – dolne normy:
    /// ruch postępowy ≥ 30%, ruchliwe ≥ 42%, żywe ≥ 54%, budowa prawidłowa ≥ 4%; mediany są znacznie wyższe).
    /// </summary>
    public void FromSemen()
    {
        float r = R01();
        Form = r < 0.5f ? Morph.Normal : r < 0.62f ? Morph.Tapered : r < 0.72f ? Morph.Amorphous : r < 0.76f ? Morph.Macro : r < 0.8f ? Morph.Micro
             : r < 0.815f ? Morph.DoubleHead : r < 0.87f ? Morph.BentNeck : r < 0.92f ? Morph.Droplet : r < 0.985f ? Morph.Coiled : Morph.DoubleTail;
        float m = R01();
        Mot = m < 0.55f ? Motil.Progressive : m < 0.65f ? Motil.NonProgressive : Motil.Immotile;
        // zwinięta witka prawie nie napędza; zagięta szyjka i podwójna witka psują ruch postępowy
        if (Form == Morph.Coiled) Mot = m < 0.5f ? Motil.NonProgressive : Motil.Immotile;
        else if (Form is Morph.DoubleTail or Morph.BentNeck or Morph.DoubleHead && Mot == Motil.Progressive && Chance(0.6f)) Mot = Motil.NonProgressive;
        Apply();
    }
    /// <summary>Plemnik, który przeszedł przez śluz szyjki: śluz przepuszcza głównie ruchliwe plemniki o prawidłowej budowie.</summary>
    public void Selected()
    {
        Form = Chance(0.85f) ? Morph.Normal : Chance(0.5f) ? Morph.Tapered : Morph.Droplet;
        Mot = Chance(0.92f) ? Motil.Progressive : Motil.NonProgressive;
        Apply();
    }
    void Apply()
    {
        if (Mot == Motil.NonProgressive) { V0 = Rand(6, 16); Curv = Rand(1f / 14, 1f / 6) * Sign(); }
        else if (Mot == Motil.Immotile) V0 = 0;
        if (Form == Morph.BentNeck) _neck = Rand(0.5f, 0.9f) * Sign();
    }
    /// <summary>Opis tego plemnika (tryb podglądu).</summary>
    public override string Note => (Mot switch { Motil.Progressive => "ruch postępowy", Motil.NonProgressive => "ruch niepostępowy", _ => "nieruchomy" })
        + " · " + Form switch
        {
            Morph.Normal => "budowa prawidłowa", Morph.Tapered => "wydłużona główka", Morph.Amorphous => "bezkształtna główka", Morph.Macro => "za duża główka",
            Morph.Micro => "za mała główka", Morph.DoubleHead => "dwie główki", Morph.BentNeck => "zagięta szyjka", Morph.Droplet => "kropla cytoplazmy na wstawce",
            Morph.Coiled => "zwinięta witka", _ => "podwójna witka",
        };
    public override void UpdatePrim() { if (State is St.Swim or St.Attach) SetCapsule(0.9f, 1.4f); else SetCircle(0.5f); }
    public override void Integrate(float dt) { if (State == St.Swim) base.Integrate(dt); }

    public override void Step(float dt, float gdt)
    {
        var sc = W.Sc as ScSperm;
        float f = State == St.Dead || Energy <= 0 || Mot == Motil.Immotile ? 0 : (Hyperactive ? 12 : 15) * (0.5f + 0.5f * Math.Min(1, Energy * 3));
        Beat += gdt * TAU * f; if (f > 0) Roll += gdt * TAU * 1.5f;
        if (_fade < 1) _fade = Math.Min(1, _fade + gdt / 1.2f);
        if (State is St.Cup or St.Vac) { Vx = Vy = 0; return; }
        if (State == St.Dead) { Vx = Vy = 0; DeadT += gdt; if (DeadT > 30 && !IsPlayer) Alive = false; return; }
        if (sc == null) { Vx = MathF.Cos(A) * V0 * 0.3f; Vy = MathF.Sin(A) * V0 * 0.3f; return; }
        // ATP: glikoliza w witce i oddychanie w wstawce; hiperaktywacja zużywa kilka razy więcej
        // kwaśne pH pochwy uszkadza plemniki (plazma nasienia buforuje je tylko kilka minut)
        float ph = sc.Ph(X, Y), acid = 1 + Math.Max(0, 6.3f - ph) * 3.5f;
        Energy -= dt / sc.Life * (Hyperactive ? 3.2f : 1) * (IsPlayer ? 1 : 1.15f) * acid;
        if (player0(sc) && acid > 2) W.LogOnce("acid", $"Kwaśno (pH {ph:0.0})! W pochwie plemniki szybko tracą ruchliwość – uciekaj do śluzu szyjki.", "bad", 20);
        _mucus = sc.Mucus(X, Y);
        if (Energy <= 0) { Energy = 0; Die(IsPlayer ? "Plemnik wyczerpał zapasy ATP – wić przestaje bić." : null); return; }
        // nieruchomy plemnik (martwy albo z niesprawną wicią) tylko dryfuje z płynem i drga (ruchy Browna)
        if (Mot == Motil.Immotile) { Vx = Vy = 0; return; }
        // kapacytacja: w rezerwuarze (przyczepiony do nabłonka cieśni) przebiega szybciej i w kontrolowany sposób
        if (Cap < 1) { Cap = Math.Min(1, Cap + dt / sc.CapT * _capK * (State == St.Attach ? 3 : 1)); if (Cap >= 1 && IsPlayer) W.Log("Kapacytacja zakończona: z błony ubyło cholesterolu, napływ HCO₃⁻ i Ca²⁺ przez kanały CatSper – możesz hiperaktywować wić (przytrzymaj Spację).", "good"); }
        bool player = IsPlayer && !W.Preview;
        var I = W.Input;
        Hyperactive = Cap >= 1 && Energy > 0.03f && (player ? I.ActionHeld : (State == St.Attach || sc.InCumulus(X, Y) || Zona > 0));
        Hyper = Lerp(Hyper, Hyperactive ? 1 : 0, Damp(4, gdt));

        if (State == St.Enter) { StepZona(sc, dt); return; }
        if (State == St.Attach)
        {
            // związany z nabłonkiem cieśni (rezerwuar): uwolnienie wymaga hiperaktywacji
            Vx = Vy = 0; AttachT += dt;
            if (Hyperactive && AttachT > 1.5f) { State = St.Swim; A += Sign() * 0.8f; if (player) W.Log("Hiperaktywowana wić odrywa plemnik od nabłonka – wolny!", "good"); }
            else if (player && I.Mag > 0.05f && AttachT > 2 && Cap < 1) { /* bez hiperaktywacji plemnik tkwi w rezerwuarze */ }
            return;
        }
        // ---------------------------------------------------------- sterowanie
        float w = 0, turnMax = 1.1f + 2.2f * Hyper;
        if (player)
        {
            if (I.Mag > 0.05f) w += Clamp(WrapA(I.Ang - A) * 2.2f, -turnMax, turnMax);
        }
        else
        {
            _turnT -= dt;
            if (_turnT <= 0) { _turnT = ExpRand(2); _want = A + RandN() * 0.6f; }
            // termotaksja: słaby zwrot ku cieplejszej bańce (+x); chemotaksja do progesteronu w pobliżu wzgórka
            float bias = 0, bw = 0;
            if (sc.ChemoDir(X, Y, out float ca)) { bias = ca; bw = 0.9f; }
            else if (sc.Axis(X, Y, out float atx, out float aty, out _, out _)) { bias = MathF.Atan2(aty, atx); bw = 0.1f; }
            w += Clamp(WrapA(_want - A) * 0.8f, -0.8f, 0.8f) + WrapA(bias - A) * bw;
        }
        // ---------------------------------------------------------- ściana: przyciąganie hydrodynamiczne + reotaksja
        float ax = 0, ay = 0;
        sc.Wall(X, Y, out float d, out float nx, out float ny);
        W.FlowAt(X, Y, out float fx, out float fy);
        float fl = Hypot(fx, fy), phys = player ? 0.6f : 1;
        // reotaksja przy dnie/stropie kanału (płaszczyzna obrazu): główka dociśnięta do powierzchni, wić niesiona prądem –
        // efekt „wiatrowskazu”: płynięcie pod prąd jest stabilne, a z prądem – najmniej stabilne (moment największy)
        if (fl > 0.3f)
        {
            float da = WrapA(MathF.Atan2(-fy, -fx) - A);
            w += MathF.Sign(da == 0 ? 1 : da) * MathF.Sqrt((1 - MathF.Cos(da)) / 2) * Math.Min(1.3f, fl / 22) * phys;
        }
        // przechwycenie przy krawędzi: przy dnie i ścianie bocznej kanału („narożnik”) pchacz jest przyciągany hydrodynamicznie
        // i sterycznie – zasięg ~ 1/3 długości plemnika; plemniki płyną wzdłuż krawędzi na długich odcinkach (Denissenko i in. 2012)
        const float WR = 20;
        if (d < WR)
        {
            float k = 1 - d / WR;
            // ściana boczna prowadzi plemnik wzdłuż krawędzi – przy przepływie w stronę pod prąd (Kantsler i in. 2014)
            float tx = ny, ty = -nx;
            bool flip = fl > 0.3f ? tx * -fx + ty * -fy < 0 : tx * MathF.Cos(A) + ty * MathF.Sin(A) < 0;
            if (flip) { tx = -tx; ty = -ty; }
            w += WrapA(MathF.Atan2(ty, tx) - A) * 2.4f * k * (1 - 0.6f * Hyper) * phys;
            if (d > 2.5f) { ax -= nx * 16 * k; ay -= ny * 16 * k; }
            // przyczepienie do nabłonka cieśni (rezerwuar plemników)
            if (d < 3.5f && Cap < 1 && sc.Isthmus(X, Y) && (player ? I.Mag <= 0.05f : Chance(dt * 0.25f)))
            {
                State = St.Attach; AttachT = 0; Vx = Vy = 0;
                if (player) W.Log("Główka związała się z nabłonkiem cieśni (rezerwuar plemników): tu kapacytacja przebiega szybciej, a plemnik żyje dłużej.", "info");
                return;
            }
        }
        // hiperaktywacja: asymetryczne bicie o dużej amplitudzie – tor zakręca w pętle i ósemki
        if (Hyper > 0.05f) w += Hyper * 3.2f * MathF.Sin(Beat * 0.045f + Roll * 0.1f);
        // krzywizna własna toru (przy dnie/stropie kanału – zawsze w obrazie); w lepkosprężystym śluzie tor jest prostszy (Smith i in. 2009)
        w += V0 * Curv * (1 - 0.7f * _mucus);
        // śluz szyjki: nici mucyny tworzą kanaliki wzdłuż osi – plemnik ustawia się wzdłuż nich
        if (_mucus > 0 && sc.Axis(X, Y, out float mx, out float my, out _, out _))
        {
            float ma = MathF.Atan2(my, mx); if (MathF.Cos(ma - A) < 0) ma += PI;
            w += WrapA(ma - A) * 1.2f * _mucus;
        }
        A += w * dt + MathF.Sqrt(2 * (0.02f + 0.25f * Hyper) * dt) * RandN();
        float visc = sc.Viscosity(X, Y, Hyper);
        // śluz szyjki jest filtrem: plemniki o wadliwej budowie główki i witki grzęzną w sieci mucyn (Suarez i Pacey 2006)
        if (_mucus > 0.3f && Form is not (Morph.Normal or Morph.Droplet)) visc *= 0.3f;
        float v = V0 * visc * (0.35f + 0.65f * Math.Min(1, Energy * 4)) * (1 - 0.35f * Hyper);
        Vx = MathF.Cos(A) * v + ax; Vy = MathF.Sin(A) * v + ay;
        if (player) Casa(gdt);
    }

    void StepZona(ScSperm sc, float dt)
    {
        var egg = sc.Egg; Vx = Vy = 0;
        if (sc.Fertilized != null && sc.Fertilized != this)
        {
            // reakcja korowa: owastacyna tnie ZP2 – osłonka twardnieje, plemniki w niej utykają (blokada polispermii)
            Die(IsPlayer ? "Osłonka przejrzysta stwardniała po reakcji korowej – inny plemnik był pierwszy." : null); return;
        }
        float ang = MathF.Atan2(Y - egg.Y, X - egg.X);
        A = Turn(A, ang + PI, 1.5f * dt);
        Zona = Math.Min(1, Zona + dt / sc.ZonaT * (0.25f + 0.75f * Hyper));
        float r = Lerp(egg.ZonaR + 1.5f, egg.OolemmaR + 0.8f, Smooth(Zona));
        X = egg.X + MathF.Cos(ang) * r; Y = egg.Y + MathF.Sin(ang) * r;
        if (Zona >= 1) { Fused = true; State = St.Bind; sc.Fertilize(W, this); }
    }

    bool player0(ScSperm sc) => IsPlayer && !W.Preview;
    /// <summary>Boczne wychylenie główki (połowa ALH) w fazie bicia; w śluzie amplituda maleje (Smith i in. 2009).</summary>
    float HeadOffset() => State is St.Swim ? _alh * 0.5f * (1 - 0.5f * _mucus) * (1 + 0.8f * Hyper) * MathF.Sin(-Beat + 0.6f) * Math.Min(1, Energy * 4) : 0;
    /// <summary>Tor główki = tor średni + wychylenie boczne; VCL – suma odcinków, VSL – przemieszczenie w oknie 1 s.</summary>
    void Casa(float gdt)
    {
        float h = HeadOffset(); var hp = new Vector2(X - MathF.Sin(A) * h, Y + MathF.Cos(A) * h);
        _track.Enqueue((hp, W.T));
        while (_track.Count > 2 && W.T - _track.Peek().t > 1) _track.Dequeue();
        if (_track.Count < 3) return;
        float len = 0; Vector2 prev = default; bool first = true;
        foreach (var (q, _) in _track) { if (!first) len += (q - prev).Length(); prev = q; first = false; }
        float span = W.T - _track.Peek().t; if (span < 0.2f) return;
        Vcl = len / span; Vsl = (hp - _track.Peek().p).Length() / span;
    }
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (o is not Oocyte egg || State != St.Swim || W.Sc is not ScSperm sc) return;
        if (sc.Fertilized != null) return;
        if (Cap < 1)
        {
            if (IsPlayer) W.LogOnce("nocap", "Niekapacytowany plemnik nie wiąże się z osłonką przejrzystą (ZP2) – najpierw kapacytacja.", "bad", 8);
            return;
        }
        // reakcja akrosomalna: egzocytoza akrosomu, na powierzchni odsłania się IZUMO1
        Acrosome = true; State = St.Enter; Zona = 0; Solid = false;
        W.Sfx("zap", X, Y, 0.5f);
        if (IsPlayer) { W.AddStat("zona", 1); W.Log("Reakcja akrosomalna! Enzymy akrosomu i hiperaktywowana wić przepychają główkę przez osłonkę przejrzystą (przytrzymaj Spację).", "good"); }
    }

    public void Die(string msg)
    {
        if (State == St.Dead) return;
        State = St.Dead; Vx = Vy = 0; Energy = 0;
        if (msg != null) W.Log(msg, "bad");
        if (IsPlayer) W.After(2.5f, () => { if (Alive) { Alive = false; } });
    }
    public override void Kill() { if (State != St.Dead) Die(null); Alive = false; }

    static readonly Color HeadC = Rgb(40, 44, 40, 0.88f), AcroC = Rgb(150, 156, 146, 0.75f), MidC = Rgb(55, 58, 52, 0.85f), TailC = Rgb(45, 48, 44, 0.75f), HaloC = Rgb(255, 255, 240, 0.28f);
    /// <summary>Oś witki (s – odległość od szyjki w tył): fala bicia, stałe lekkie wygięcie, a przy wadzie – zwinięty koniec.</summary>
    void Tail(Vector2[] b, int ns, float amp, float lam, float headY, float phase, float yOff)
    {
        for (int i = 0; i <= ns; i++)
        {
            float s = 55f * i / ns;
            float env = amp * (0.6f + 5.4f * Smooth(s / 45f)) * (s > 50 ? 1 - (s - 50) / 12 : 1);
            float y = env * MathF.Sin(TAU * s / lam - Beat + phase) + Hyper * 4 * Sq(s / 55) * MathF.Sin(Beat * 0.5f) + _bend * s * s + yOff * Smooth(s / 12);
            b[i] = new Vector2(-s, y + headY * (1 - Math.Min(1, s / 10)));
        }
        if (Form == Morph.Coiled)
        {
            // zwinięta witka: za ~18 µm wić zawija się w pętle (prawie nie napędza plemnika)
            const float s0 = 18, rc = 3.2f; int i0 = (int)(s0 / 55f * ns); var p0 = b[i0]; float sd = _bend >= 0 ? 1 : -1;
            for (int i = i0 + 1; i <= ns; i++) { float ph = (55f * i / ns - s0) / rc; b[i] = new Vector2(p0.X - rc * MathF.Sin(ph), p0.Y + sd * rc * (1 - MathF.Cos(ph))); }
        }
    }
    void DrawTail(Painter p, View v, Vector2[] b, int ns)
    {
        for (int i = 0; i < ns; i++)
        {
            float s = 55f * i / ns, wdt = s < 4 ? 0.95f : s < 49 ? 0.55f - 0.25f * (s - 4) / 45 : 0.2f;
            p.Seg(b[i].X, b[i].Y, b[i + 1].X, b[i + 1].Y, Math.Max(v.Px * 0.9f, wdt), s < 4 ? MidC : TailC);
        }
    }
    /// <summary>Główka (łopatka widziana pod różnym kątem przy obrocie) w kształcie zależnym od budowy; akrosom z przodu jaśniejszy.</summary>
    void Head(Painter p, float wv, bool detail)
    {
        float lx = 2.25f, ly = wv;
        switch (Form)
        {
            case Morph.Tapered: lx = 2.7f; ly = wv * 0.62f; break;       // główka wydłużona, zwężona
            case Morph.Macro: lx = 2.85f; ly = wv * 1.3f; break;         // za duża (często diploidalna)
            case Morph.Micro: lx = 1.5f; ly = wv * 0.72f; break;         // za mała
        }
        if (Form == Morph.Amorphous)
        {
            // bezkształtna główka – nieregularny zarys
            Span<Vector2> hs = stackalloc Vector2[14];
            for (int i = 0; i < 14; i++) { float th = i * TAU / 14, r = 1 + 0.22f * MathF.Sin(3 * th + Id) + 0.12f * MathF.Cos(5 * th + Id * 0.7f); hs[i] = new Vector2(MathF.Cos(th) * 2.3f * r, MathF.Sin(th) * wv * 1.1f * r); }
            p.FillFan(hs, 0, 0, HeadC);
            if (detail && !Acrosome) p.FillEllipse(0.7f, 0, 1.1f, wv * 0.7f, 0.3f, AcroC);
        }
        else
        {
            p.FillEllipse(0, 0, lx + 0.35f, ly + 0.5f, 0, HaloC);
            p.FillEllipse(0, 0, lx, ly, 0, HeadC);
            if (detail && !Acrosome) p.FillEllipse(lx * 0.33f, 0, lx * 0.6f, ly * 0.82f, 0, AcroC);
        }
        if (detail) p.Dot(-1.3f, 0, 0.35f, Rgb(20, 22, 20, 0.6f)); // szyjka
    }
    public override void Draw(Painter p, View v)
    {
        bool eaten = State is St.Cup or St.Vac;
        float oldA = p.Alpha; if (eaten) p.Alpha = 1 - Dig; if (State == St.Dead) p.Alpha = Math.Max(0.35f, 1 - DeadT / 40);
        if (_fade < 1) p.Alpha *= Smooth(_fade);
        p.Local(X, Y, A);
        const int NS = 30; var b = p.Buf2;
        float amp = State == St.Dead || Energy <= 0 || Mot == Motil.Immotile ? 0 : (1 + 1.2f * Hyper), lam = 32 - 8 * Hyper;
        // kołysanie główki (ALH) wynika z fali u nasady wici
        float headY = HeadOffset(), headYaw = 0.2f * amp * MathF.Cos(-Beat + 0.6f) + _neck;
        bool detail = 4.5f / Math.Max(v.Px, 1e-6f) > 5;
        // witka: coraz cieńsza ku końcowi (wstawka ≈ 1 µm, witka 0,5 µm, odcinek końcowy 0,2 µm);
        // przód wici przejmuje boczne wychylenie główki, które zanika w głąb witki
        Tail(b, NS, amp, lam, headY, 0, 0);
        DrawTail(p, v, b, NS);
        if (Form == Morph.DoubleTail) { Tail(b, NS, amp, lam, headY, 1.3f, 2.2f); DrawTail(p, v, b, NS); }
        // kropla cytoplazmy na wstawce (resztka cytoplazmy spermatydy)
        if (Form == Morph.Droplet) { p.FillCircle(-2.6f, headY + 0.5f, 1.25f, Rgb(170, 172, 160, 0.6f)); p.Circle(-2.6f, headY + 0.5f, 1.25f, 0.12f, Rgb(50, 52, 46, 0.7f)); }
        float wv = 1.45f * (0.45f + 0.55f * MathF.Abs(MathF.Cos(Roll)));
        p.Push(); p.Translate(2.25f, headY); p.Rotate(headYaw);
        if (Form == Morph.DoubleHead)
        {
            // dwie główki na jednej szyjce
            p.Push(); p.Rotate(0.42f); p.Translate(0.4f, 0); Head(p, wv, detail); p.Pop();
            p.Rotate(-0.42f); p.Translate(0.4f, 0); Head(p, wv, detail);
        }
        else Head(p, wv, detail);
        p.Pop();
        p.Alpha = oldA;
    }
}

/// <summary>
/// Komórka jajowa (oocyt II rzędu) w kompleksie ze wzgórkiem jajonośnym. Oocyt ≈ 110–120 µm, osłonka przejrzysta ≈ 13–15 µm
/// (glikoproteiny ZP1–ZP4), wąska przestrzeń okołożółtkowa z I ciałkiem kierunkowym; wieniec promienisty i komórki wzgórka
/// w macierzy kwasu hialuronowego (plemniki przechodzą przez nią dzięki hialuronidazie PH-20).
/// Po zapłodnieniu: reakcja korowa (owastacyna tnie ZP2 – blokada polispermii; Burkart i in. 2012), przedjądrza, bruzdkowanie.
/// </summary>
public sealed class Oocyte : Ent
{
    public readonly float OolemmaR = 55, ZonaR = 70, CumR;
    public float FertT = -1;
    readonly Vector3[] _cells, _gran;
    readonly float _pb;
    public override float Radius => ZonaR;
    public Oocyte(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.Oocyte; Layer = 1; Fixed = true; FlowK = 0; Mass = 1e6f; R = ZonaR + 4; Name = "komórka jajowa (oocyt II rzędu)"; Latin = "Homo sapiens";
        CumR = Rand(210, 240); _pb = Rand(0, TAU);
        // komórki wieńca promienistego (przy osłonce, wydłużone promieniście) i luźniej – wzgórka jajonośnego
        _cells = new Vector3[520];
        for (int i = 0; i < _cells.Length; i++)
        {
            float a = Rand(0, TAU), r = i < 140 ? ZonaR + Rand(3, 16) : ZonaR + 14 + MathF.Pow(R01(), 0.8f) * (CumR - ZonaR - 14);
            _cells[i] = new Vector3(a, r, Rand(3.5f, 5.5f));
        }
        _gran = new Vector3[260];
        for (int i = 0; i < _gran.Length; i++) { float a = Rand(0, TAU), r = MathF.Sqrt(R01()) * (OolemmaR - 3); _gran[i] = new Vector3(MathF.Cos(a) * r, MathF.Sin(a) * r, Rand(0.5f, 1.4f)); }
    }
    public override void UpdatePrim() => SetCircle(ZonaR);
    public override bool Collides(Ent o) => o is not Sperm s || s.State == St.Swim;
    public override void Step(float dt, float gdt) { if (FertT >= 0) FertT += gdt; }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, 0);
        // macierz wzgórka: lśniący, galaretowaty kwas hialuronowy
        p.Glow(0, 0, CumR * 1.08f, Rgb(250, 250, 235, 0.16f));
        float lw = Math.Max(v.Px, 0.25f);
        for (int i = 0; i < _cells.Length; i++)
        {
            var c = _cells[i]; float x = MathF.Cos(c.X) * c.Y, y = MathF.Sin(c.X) * c.Y;
            if (!v.Vis(X + x, Y + y, c.Z * 2)) continue;
            bool corona = i < 140;
            // wieniec promienisty: komórki wydłużone ku osłonce; wzgórek – okrągławe, z jądrem
            p.FillEllipse(x, y, corona ? c.Z * 1.6f : c.Z, corona ? c.Z * 0.75f : c.Z * 0.9f, corona ? c.X : c.X * 3, Rgb(160, 160, 145, 0.45f));
            p.Ellipse(x, y, corona ? c.Z * 1.6f : c.Z, corona ? c.Z * 0.75f : c.Z * 0.9f, corona ? c.X : c.X * 3, lw, Rgb(60, 60, 50, 0.45f));
            if (c.Z / v.Px > 6) p.Dot(x, y, c.Z * 0.35f, Rgb(85, 85, 75, 0.45f));
        }
        // osłonka przejrzysta: gruby, szklisty pierścień (w kontraście fazowym jasny, z delikatnym prążkowaniem)
        float zr = (ZonaR + OolemmaR + 4) / 2, zw = ZonaR - OolemmaR - 4;
        float hard = FertT >= 0 ? Math.Min(1, FertT / 4) : 0;
        p.Circle(0, 0, zr, zw, Rgb((int)Lerp(215, 235, hard), (int)Lerp(218, 225, hard), (int)Lerp(200, 190, hard), 0.55f));
        p.Circle(0, 0, ZonaR, lw * 1.4f, Rgb(70, 70, 58, 0.6f));
        p.Circle(0, 0, OolemmaR + 4, lw, Rgb(90, 90, 75, 0.5f));
        if (zw / v.Px > 8) for (int k = 0; k < 90; k++) { float a = k * TAU / 90; p.Seg(MathF.Cos(a) * (OolemmaR + 5), MathF.Sin(a) * (OolemmaR + 5), MathF.Cos(a) * (ZonaR - 1), MathF.Sin(a) * (ZonaR - 1), lw * 0.6f, Rgb(170, 170, 150, 0.18f)); }
        // przestrzeń okołożółtkowa z ciałkiem kierunkowym
        p.FillCircle(MathF.Cos(_pb) * (OolemmaR + 1), MathF.Sin(_pb) * (OolemmaR + 1), 6, Rgb(150, 150, 130, 0.6f));
        p.Circle(MathF.Cos(_pb) * (OolemmaR + 1), MathF.Sin(_pb) * (OolemmaR + 1), 6, lw, Rgb(60, 60, 50, 0.6f));
        if (FertT > 6) { float a2 = _pb + 0.5f; p.FillCircle(MathF.Cos(a2) * (OolemmaR + 1), MathF.Sin(a2) * (OolemmaR + 1), 5, Rgb(150, 150, 130, 0.6f)); } // II ciałko kierunkowe
        // ooplazma: ziarnista
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(160, 158, 140, 0.7f), Rgb(125, 122, 105, 0.75f) };
        var sh = p.Buf2; int n = p.CirclePts(0, 0, OolemmaR, sh, 72);
        p.FillRadial(new ReadOnlySpan<Vector2>(sh, 0, n), 0, 0, ts, cs);
        p.Circle(0, 0, OolemmaR, lw * 1.2f, Rgb(55, 55, 45, 0.7f));
        foreach (var g in _gran) p.Dot(g.X, g.Y, g.Z, g.Z > 1 ? Rgb(90, 88, 72, 0.4f) : Rgb(235, 232, 210, 0.35f));
        if (FertT >= 0)
        {
            // reakcja korowa: fala egzocytozy ziaren korowych biegnie wokół oolemmy
            float q = Math.Min(1, FertT / 3);
            p.Circle(0, 0, OolemmaR + 1.5f, 2 + 3 * (1 - q), Rgb(255, 250, 225, 0.6f * (1 - q) + 0.1f));
            // przedjądrza męskie i żeńskie (widoczne po kilku godzinach; w grze po kilku sekundach)
            float pn = Smooth(Clamp((FertT - 5) / 6, 0, 1));
            if (pn > 0)
            {
                float sep = Lerp(24, 9, Smooth(Clamp((FertT - 11) / 8, 0, 1)));
                for (int s = -1; s <= 1; s += 2)
                {
                    p.FillCircle(s * sep, 0, 11 * pn, Rgb(195, 195, 178, 0.75f));
                    p.Circle(s * sep, 0, 11 * pn, lw, Rgb(70, 70, 58, 0.7f));
                    p.Dot(s * sep + 2, -2, 2.2f * pn, Rgb(60, 60, 50, 0.6f)); // jąderka
                }
            }
        }
    }
}

/// <summary>
/// Złuszczona komórka nabłonka pochwy. Pochwę wyściela nabłonek wielowarstwowy płaski; jego powierzchowne warstwy
/// stale się złuszczają, więc w płynie pochwy leżą duże (≈ 40–60 µm), płaskie, wielokątne komórki z małym, zbitym
/// (pyknotycznym) jądrem. Są pełne glikogenu – pałeczki kwasu mlekowego (Lactobacillus) przerabiają go na kwas
/// mlekowy, który utrzymuje kwaśne pH pochwy (Boskey i in. 1999). Często przylegają do nich pałeczki.
/// </summary>
public sealed class Squamous : Ent
{
    readonly Vector2[] _poly; readonly Vector2 _nuc; readonly float _r, _nr; readonly Vector3[] _gran, _rods; readonly (Vector2 a, Vector2 b)? _fold;
    public override float Radius => _r;
    public Squamous(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.TissueCell; Layer = 0; Solid = false; Fixed = true; FlowK = 0; Name = "komórka nabłonka pochwy"; Latin = "komórka nabłonka płaskiego (złuszczona)";
        _r = Rand(19, 29); R = _r * 1.25f;
        int n = RandI(6, 9); _poly = new Vector2[n];
        float t0 = Rand(0, TAU);
        for (int i = 0; i < n; i++) { float t = t0 + i * TAU / n + Rand(-0.22f, 0.22f), q = _r * Rand(0.82f, 1.15f); _poly[i] = new Vector2(MathF.Cos(t) * q, MathF.Sin(t) * q * Rand(0.8f, 1f)); }
        _nuc = new Vector2(Rand(-0.25f, 0.25f) * _r, Rand(-0.25f, 0.25f) * _r); _nr = Rand(1.8f, 2.8f);
        _gran = new Vector3[40];
        for (int i = 0; i < _gran.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * _r * 0.8f; _gran[i] = new Vector3(MathF.Cos(t) * q, MathF.Sin(t) * q, Rand(0.15f, 0.35f)); }
        // przylegające pałeczki kwasu mlekowego
        _rods = new Vector3[RandI(0, 9)];
        for (int i = 0; i < _rods.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * _r * 0.85f; _rods[i] = new Vector3(MathF.Cos(t) * q, MathF.Sin(t) * q, Rand(0, TAU)); }
        if (Chance(0.5f)) { var a = _poly[RandI(0, n - 1)] * 0.8f; var c = _poly[RandI(0, n - 1)] * 0.5f; _fold = (a, c); }
    }
    public override void UpdatePrim() => SetCircle(_r * 0.8f);
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        var poly = new ReadOnlySpan<Vector2>(_poly);
        float lw = Math.Max(v.Px, 0.12f);
        p.SoftStroke(poly, true, 2.2f, Rgb(255, 255, 240, 0.22f));
        p.FillFan(poly, 0, 0, Rgb(196, 190, 176, 0.3f));
        p.Stroke(poly, true, lw, Rgb(95, 90, 80, 0.55f));
        if (_fold.HasValue) { var (fa, fb) = _fold.Value; p.Line(fa.X, fa.Y, fb.X, fb.Y, lw * 1.4f, Rgb(120, 115, 104, 0.35f)); } // zagięcie płaskiej komórki
        foreach (var g in _gran) p.Dot(g.X, g.Y, g.Z, Rgb(110, 105, 95, 0.28f));
        p.FillEllipse(_nuc.X, _nuc.Y, _nr, _nr * 0.8f, 0.3f, Rgb(70, 64, 88, 0.62f));
        foreach (var r in _rods) p.Line(r.X - MathF.Cos(r.Z) * 1.4f, r.Y - MathF.Sin(r.Z) * 1.4f, r.X + MathF.Cos(r.Z) * 1.4f, r.Y + MathF.Sin(r.Z) * 1.4f, 0.75f, Rgb(40, 44, 38, 0.7f), true);
    }
}

/// <summary>
/// Neutrofil w świetle jajowodu. Neutrofile napływają do dróg rodnych po stosunku i fagocytują plemniki
/// (także wyrzucając sieci NET, które je unieruchamiają; Alghamdi i Foster 2005). Pełzają powoli – groźne są przy kontakcie.
/// </summary>
public sealed class Leuko : Ent
{
    const int NN = 40;
    readonly float _r0, _seed; readonly float[] _rr = new float[NN]; float _dir, _eatCD;
    readonly Vector3[] _gran; readonly List<(Sperm s, float t)> _gut = new();
    public override float Radius => _r0;
    public override bool Swims => false;
    public Leuko(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.Neutro; Layer = 2; Soft = 0.75f; Name = "neutrofil"; Latin = "granulocyt obojętnochłonny";
        _r0 = Rand(5.5f, 6.5f); R = _r0 * 1.5f; Mass = 12; FlowK = 0.3f; _seed = Rand(0, 99); _dir = Rand(0, TAU);
        Mem = new Membrane(NN) { MaxDef = 2, Stiff = 7, Damping = 0.6f, Fluid = 0.8f }; MemK = 4; MemS = 0.5f;
        _gran = new Vector3[70];
        for (int i = 0; i < _gran.Length; i++) _gran[i] = new Vector3(Rand(0, TAU), MathF.Sqrt(R01()) * 0.8f, Chance(0.5f) ? 1 : 0);
        Shape(); Mem.Follow(x, y, 0, 1, 1);
    }
    public override bool Rotates => false;
    public override void UpdatePrim() => SetCircle(_r0);
    void Shape()
    {
        float t = W?.T ?? 0;
        for (int i = 0; i < NN; i++) { float th = i * TAU / NN; _rr[i] = _r0 * (1 + 0.12f * Noise1(th * 3 + t * 0.5f + _seed) + 0.15f * Gauss(WrapA(th - _dir), 0.6f)); }
        Mem.Radial(_rr);
    }
    public override void Step(float dt, float gdt)
    {
        _eatCD -= dt;
        // pełzanie ≈ 15 µm/min z chemotaksją do najbliższego plemnika (dopełniacz, IL-8)
        var t = W.Near(X, Y, 120, e => e is Sperm s && s.State is St.Swim or St.Attach or St.Dead);
        if (t != null) _dir = Turn(_dir, MathF.Atan2(t.Y - Y, t.X - X), 0.5f * dt);
        Vx = MathF.Cos(_dir) * 0.25f; Vy = MathF.Sin(_dir) * 0.25f;
        for (int i = _gut.Count - 1; i >= 0; i--)
        {
            var (s, t0) = _gut[i]; float age = W.T - t0;
            s.X = X + MathF.Cos(i * 2.4f) * _r0 * 0.4f; s.Y = Y + MathF.Sin(i * 2.4f) * _r0 * 0.4f;
            s.Dig = Math.Min(1, age / 25); if (age > 25) { s.Alive = false; _gut.RemoveAt(i); }
        }
        if (W.OnScreen(this)) Shape();
    }
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (o is not Sperm s || _eatCD > 0 || s.State is not (St.Swim or St.Attach or St.Dead) || _gut.Count > 4) return;
        if (!Chance(0.6f)) { _eatCD = 0.3f; return; }
        _eatCD = 1.2f;
        s.State = St.Cup; s.Holder = this; s.Solid = false; _gut.Add((s, W.T));
        W.Sfx("gulp", X, Y, 0.5f);
        if (s.IsPlayer) { W.Log("Neutrofil otoczył Cię nibynóżkami – fagocytoza. Plemnik zostanie strawiony.", "bad"); W.After(2.5f, () => { if (s.Alive) s.Alive = false; }); }
    }
    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape, 2);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 1.4f, Rgb(255, 255, 240, 0.28f));
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(150, 150, 142, 0.5f), Rgb(125, 125, 118, 0.55f) };
        p.FillRadial(sh, X, Y, ts, cs);
        p.Stroke(sh, true, Math.Max(v.Px, 0.12f), Rgb(45, 45, 40, 0.65f));
        p.Local(X, Y, 0); p.Warp(Mem);
        // jądro wielopłatowe
        for (int k = 0; k < 3; k++) { float a = _seed + k * 1.9f; p.FillEllipse(MathF.Cos(a) * _r0 * 0.3f, MathF.Sin(a) * _r0 * 0.3f, _r0 * 0.3f, _r0 * 0.22f, a, Rgb(85, 85, 110, 0.45f)); }
        foreach (var g in _gran) { float r = g.Y * _r0; p.Dot(MathF.Cos(g.X) * r, MathF.Sin(g.X) * r, 0.25f, g.Z > 0 ? Rgb(235, 235, 220, 0.5f) : Rgb(50, 50, 45, 0.45f)); }
    }
}

namespace Mikro;

/// <summary>
/// Akson neuronu spoidłowego: łamana od wzgórka aksonu (P[0], przy ciele komórki) do stożka wzrostu (P[^1]).
/// Odcinek przy ciele to dawna wypustka tylna – ciało wędruje dalej, a akson zostaje w tkance (Hatanaka i Murakami 2002).
/// Stożek w drodze (istota biała) posuwa się ~37 µm/h, po przejściu linii środkowej ~55 µm/h (Hutchins i in. 2011),
/// w korze docelowej – o połowę wolniej niż w drodze (Halloran i Kalil 1994).
/// </summary>
public sealed class Axon
{
    public readonly List<Vector2> P = new();
    public float Len;
    /// <summary>Stożek przeszedł linię środkową; dotarł pod korę homotopową drugiej półkuli; wypuścił gałęzie do kory.</summary>
    public bool Crossed, AtTarget;
    /// <summary>Kierunek wzrostu (rad), przerwa (h czasu biologicznego), zapadnięcie stożka po sygnale odpychającym (0..1).</summary>
    public float Dir = PI, Pause, Collapse;
    /// <summary>Głębokość, na której akson powstał (kontakt z aksonem TAG-1), i miejsce homotopowe po drugiej stronie.</summary>
    public float Y0, TargetX;
    /// <summary>Własny pas w ciele modzelowatym (przesunięcie od środka pasma, µm) – aksony nie biegną jeden po drugim.</summary>
    public readonly float Lane = Rand(-22f, 22f);
    /// <summary>Stożek „trzymany” przed linią środkową, dopóki gracz nie przejmie sterowania.</summary>
    public bool Hold;
    /// <summary>Gałęzie wnikające do kory docelowej (pojawiają się po dotarciu pod cel, ~P6).</summary>
    public readonly List<List<Vector2>> Branches = new();
    public Vector2 Tip => P[^1];
    public float MinX = float.MaxValue, MaxX = float.MinValue, MinY = float.MaxValue, MaxY = float.MinValue;

    public Axon(float x0, float y0, float x1, float y1) { P.Add(new Vector2(x0, y0)); P.Add(new Vector2(x1, y1)); Len = Hypot(x1 - x0, y1 - y0); Y0 = y1; Bounds(x0, y0); Bounds(x1, y1); }
    void Bounds(float x, float y) { if (x < MinX) MinX = x; if (x > MaxX) MaxX = x; if (y < MinY) MinY = y; if (y > MaxY) MaxY = y; }
    public bool Near(View v, float m) => MaxX + m > v.X0 && MinX - m < v.X1 && MaxY + m > v.Y0 && MinY - m < v.Y1;

    /// <summary>Ciało komórki przesunęło się: wypustka tylna zostaje wzdłuż drogi ciała (nowy punkt co ~4 µm).</summary>
    public void Soma(float x, float y)
    {
        var s = new Vector2(x, y);
        if ((s - P[1]).Length() > 4.5f && (P[0] - P[1]).Length() > 3.5f) P.Insert(1, P[0]);
        P[0] = s; Bounds(x, y);
    }
    /// <summary>Całkowita długość aksonu (µm) – z odcinkiem przy ciele (dawną wypustką tylną).</summary>
    public float Length()
    {
        float l = 0; for (int i = 1; i < P.Count; i++) l += (P[i] - P[i - 1]).Length();
        return l;
    }
    /// <summary>Stożek wzrostu przesuwa się o (dx, dy): nowy punkt co ~3 µm, poprzedni zostaje w tkance.</summary>
    public void Advance(float dx, float dy)
    {
        var t = P[^1] + new Vector2(dx, dy);
        if ((P[^1] - P[^2]).Length() > 3) P.Add(t); else P[^1] = t;
        Len += Hypot(dx, dy); Bounds(t.X, t.Y);
    }
    /// <summary>Cofnięcie stożka (zapadnięcie): akson skraca się od końca o d µm.</summary>
    public void Retract(float d)
    {
        while (d > 0 && P.Count > 2)
        {
            var s = P[^1] - P[^2]; float l = s.Length();
            if (l <= d) { P.RemoveAt(P.Count - 1); d -= l; Len -= l; continue; }
            P[^1] = P[^2] + s * ((l - d) / l); Len -= d; d = 0;
        }
    }
}

/// <summary>
/// Młody neuron piramidowy kory (mysz, urodzony w E15,5 – przyszła warstwa II/III, neuron spoidłowy) i jego droga:
/// progenitor pośredni (Tbr2⁺) → podział w SVZ na dwa neurony (Noctor i in. 2004) → komórka wielobiegunowa, ~4,4 µm/h
/// (Tabata i Nakajima 2003) → kontakt z aksonem TAG-1 daje akson (Namba i in. 2014) → pod podpłytką synapsy z neuronów
/// podpłytki (NMDA) przełączają ją w komórkę dwubiegunową (Ohtaka-Maruyama i in. 2018) → lokomocja po włóknie gleju
/// promienistego skokami jądra (średnio ~9–12 µm/h) → translokacja końcowa ostatnich ~50 µm (Sekine i in. 2011).
/// </summary>
public sealed class CortNeuron : Ent
{
    public enum Stage { Ipc, Mitosis, Multipolar, Bipolar, Locomotion, Translocation, Settled, Stranded, Apoptotic }
    public Stage S;
    /// <summary>Wyznakowany (GFP po elektroporacji w E15,5 – ta sama kohorta co gracz); siostrzany neuron gracza.</summary>
    public bool Gfp, Sister;
    public float R0 = 4.3f;
    /// <summary>Cykl progenitora 0..1, mitoza 0..1, synapsy z podpłytki 0..1, przejście w komórkę dwubiegunową 0..1, apoptoza 0..1.</summary>
    public float Cycle, Mit, Syn, Trans, Apo;
    public const int NoFiber = int.MinValue;
    public int Fiber = NoFiber; public float Side = 1;
    /// <summary>Wypustka wiodąca: długość (µm); zakotwiczona w strefie brzeżnej (translokacja, osiadły) – y końca.</summary>
    public float Lead, AnchorY; public bool Anchored;
    /// <summary>Cykl lokomocji 0..1: wydłużanie wypustki i poszerzenie (dylatacja) → skok jądra (nukleokineza) → przerwa.</summary>
    public float Phase;
    public Axon Ax;
    /// <summary>Wiek zarodka (dni, E) przy osiedleniu; −1 = jeszcze wędruje.</summary>
    public float SettledAge = -1;
    /// <summary>Stożek wzrostu sterowany przez gracza (akson rośnie w jego Step, nie automatycznie).</summary>
    public GrowthCone Cone;
    /// <summary>Przejście na sąsiednie włókno gleju: postęp (h) i kierunek.</summary>
    float _sw; int _swDir; float _swFromX;
    float _lastDx, _wT, _wA, _pauseT, _seed, _apicalA;
    /// <summary>Neuryty komórki wielobiegunowej: kąt (lokalny świata), długość, długość docelowa, czas do zmiany.</summary>
    readonly float[] _na = new float[6], _nl = new float[6], _nt = new float[6], _nc = new float[6]; int _nn;
    /// <summary>Dendryty podstawne po osiedleniu (kąt, długość).</summary>
    readonly float[] _da = new float[5], _dl = new float[5];
    public override bool Swims => false;
    public override bool Rotates => false;
    public override float Radius => R0;
    public override bool CanControl() => Alive && S != Stage.Apoptotic;
    public override string Note => S switch
    {
        Stage.Ipc => "progenitor pośredni (Tbr2⁺)", Stage.Mitosis => "mitoza progenitora pośredniego", Stage.Multipolar => Ax == null ? "komórka wielobiegunowa – bez aksonu" : "komórka wielobiegunowa – z aksonem",
        Stage.Bipolar => "przejście w komórkę dwubiegunową", Stage.Locomotion => "lokomocja po włóknie gleju promienistego", Stage.Translocation => "translokacja końcowa",
        Stage.Settled => "osiadły u szczytu płytki korowej", Stage.Stranded => "heterotopia – utknął w istocie białej", _ => "apoptoza",
    } + (Gfp ? " · GFP" : "") + (Sister ? " · siostrzany neuron" : "");

    public CortNeuron(World w, float x, float y, Stage s, bool gfp) : base(w, x, y, -PI / 2)
    {
        Kind = Kind.Neuron; Layer = 2; S = s; Gfp = gfp; Soft = 0.5f; Mass = 6; FlowK = 0;
        Name = s <= Stage.Mitosis ? "progenitor pośredni" : "młody neuron piramidowy"; Latin = s <= Stage.Mitosis ? "komórka Tbr2⁺ (Mus musculus)" : "neuron warstwy II/III (Mus musculus)";
        R0 = s <= Stage.Mitosis ? Rand(4.5f, 4.9f) : Rand(4.1f, 4.5f); R = R0 + 0.5f;
        _seed = Rand(0, 100); _wA = Rand(0, TAU); Cycle = Rand(0, 0.15f); _apicalA = Rand(-0.25f, 0.25f);
        InitNeurites();
        for (int i = 0; i < _da.Length; i++) { _da[i] = PI / 2 + (i - 2) * 0.55f + Rand(-0.2f, 0.2f); _dl[i] = 0; }
        // miękkie ciało: żelowa cytoplazma w błonie – obrys wgniata się o sąsiednie komórki, drży i wydłuża się przy skoku jądra
        Mem = new Membrane(NN) { MaxDef = 2.2f, Stiff = 6, Damping = 0.5f, Fluid = 0.7f }; MemK = 3; MemS = 0.55f;
        Shape(); Mem.Follow(x, y, A, 1, 1);
    }
    const int NN = 32;
    readonly float[] _rr = new float[NN];
    bool _shapeDue = true;
    /// <summary>Kształt przeliczany w równoległej fazie membran (zależy tylko od stanu tej komórki).</summary>
    public override void PreMem() { if (_shapeDue) { _shapeDue = false; Shape(); } }
    /// <summary>
    /// Kształt spoczynkowy miękkiego ciała w układzie ciała (+x – oś ku wypustce wiodącej): elipsa z półosiami ze stanu komórki;
    /// wędrujący neuron ma kształt kropli zwężającej się w szyjkę wypustki wiodącej, komórka wielobiegunowa – wybrzuszenia
    /// u nasady neurytów, komórka w apoptozie – pęcherzyki błony; do tego powolne falowanie obrysu (cytoplazma jest żelem).
    /// </summary>
    void Shape()
    {
        Soma(out float a, out float b, out _);
        float t = W?.T ?? 0;
        bool tear = S is Stage.Bipolar or Stage.Locomotion or Stage.Translocation;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, c = MathF.Cos(th), s = MathF.Sin(th);
            float r = a * b / MathF.Sqrt(Sq(b * c) + Sq(a * s));
            if (tear) r += a * 0.26f * Gauss(WrapA(th), 0.42f) - a * 0.05f * Gauss(WrapA(th - PI), 0.7f);
            if (S == Stage.Multipolar) for (int k = 0; k < _nn; k++) r += R0 * 0.1f * Gauss(WrapA(th - (_na[k] - A)), 0.28f) * Math.Min(1, _nl[k] / 6);
            if (S == Stage.Mitosis) r = R0 * 1.05f;
            if (S == Stage.Apoptotic) r *= 1 + 0.14f * Smooth(Apo) * MathF.Pow(Math.Max(0, MathF.Sin(6 * th + _seed)), 3);
            r *= 1 + 0.035f * Noise1(th * 2.1f + t * 0.35f + _seed) + 0.018f * Noise1(th * 5.3f + t * 0.8f + _seed * 1.7f);
            _rr[i] = r;
        }
        Mem.Radial(_rr);
    }
    void InitNeurites()
    {
        _nn = RandI(3, 4);
        for (int i = 0; i < _nn; i++) { _na[i] = i * TAU / _nn + Rand(-0.4f, 0.4f); _nl[i] = Rand(2, 7); _nt[i] = Rand(2, 11); _nc[i] = Rand(0.5f, 2.5f); }
    }
    public override void UpdatePrim() => SetCircle(R0 * 0.9f);
    /// <summary>Nie zderza się ze stożkiem wzrostu ani z komórkami w innej płaszczyźnie (apoptotyczne).</summary>
    public override bool Collides(Ent o) => o is not GrowthCone && S is not (Stage.Apoptotic or Stage.Translocation)
        && !(o is CortNeuron cn && (cn.Fiber != NoFiber || Fiber != NoFiber || cn.S is Stage.Apoptotic or Stage.Translocation));

    ScNeuron Sc => W.Sc as ScNeuron;
    CortexField F => Sc?.F;
    /// <summary>Tempo procesów w godzinach biologicznych na krok.</summary>
    static float H(float dt) => dt / 3600f;

    public override void Step(float dt, float gdt)
    {
        Vx = Vy = 0;
        if (W.Preview) { PreviewStep(dt); return; }
        var sc = Sc; if (sc == null) return;
        bool me = IsPlayer;
        float h = H(dt);
        var I = W.Input;
        switch (S)
        {
            case Stage.Ipc: StepIpc(sc, me, I, h, dt); break;
            case Stage.Mitosis:
                Mit += h / 0.5f; // mitoza progenitora pośredniego ~0,5 h (Arai i in. 2011)
                if (Mit >= 1) sc.DivideIpc(this);
                break;
            case Stage.Multipolar: StepMultipolar(sc, me, I, h, dt); break;
            case Stage.Bipolar: StepBipolar(sc, h, dt); break;
            case Stage.Locomotion: StepLoco(sc, me, I, h, dt); break;
            case Stage.Translocation: StepTrans(sc, h, dt); break;
            case Stage.Settled: case Stage.Stranded:
                // dendryty podstawne po urodzeniu (P0+) – powolny wzrost do ~15–20 µm w pierwszym tygodniu
                if (sc.Age > 19.5f) for (int i = 0; i < _da.Length; i++) _dl[i] = Math.Min(8 + (i * 3) % 7, _dl[i] + h * 0.25f);
                break;
            case Stage.Apoptotic:
                Apo += h / 1.5f; // piknoza i obkurczenie: ~1–2 h, potem ciałka apoptotyczne sprząta mikroglej
                if (Apo >= 1 && !IsPlayer && Holder == null) Alive = false;
                break;
        }
        Ax?.Soma(X, Y + R0 * 0.8f);
        // oś ciała: komórka wielobiegunowa nie ma stałej biegunowości (powoli się obraca), wędrująca i osiadła – wzdłuż gleju, ku oponie
        if (S == Stage.Multipolar) A += RandN() * 0.3f * MathF.Sqrt(h);
        else if (S != Stage.Mitosis) A = Turn(A, -PI / 2, Math.Max(0.02f, h * 1.5f));
        // kształt poza kadrem przeliczany rzadziej
        if (me || W.OnScreen(this, 1.3f) || ((W.Frame + Id) & 7) == 0) _shapeDue = true;
    }

    // ------------------------------------------------ progenitor pośredni: z powierzchni komory do SVZ
    void StepIpc(ScNeuron sc, bool me, InputState I, float h, float dt)
    {
        // cykl neurogennego progenitora pośredniego 26,2 h, z czego G1 ~21 h (E14,5 – Arai i in. 2011); dzieli się w strefie podkomorowej
        Cycle += h / 26.2f;
        bool inSvz = Y < Cortex.SvzB - 4 && Y > Cortex.IzB + 6;
        if (Cycle >= 1 && inSvz) { S = Stage.Mitosis; Mit = 0; if (me) W.Log("Mitoza: progenitor zaokrągla się, chromosomy ustawiają się w płytce metafazowej – podział symetryczny na dwa neurony.", "info"); return; }
        Cycle = Math.Min(Cycle, 1);
        if (me && Cycle >= 1) W.LogOnce("ipcsvz", "Cykl komórkowy dobiegł końca – progenitor pośredni dzieli się w strefie podkomorowej (SVZ). Wróć do niej (zielona strzałka).", "info", 20);
        // ~100 µm przez strefę komorową w ~12 h (FlashTag: E16,0 jeszcze w VZ, E16,5 już w SVZ/MAZ – Yoshinaga i in. 2021)
        const float V = 8f / 3600;
        float dx, dy, mag;
        if (me) { dx = I.Dx; dy = I.Dy; mag = I.Mag; }
        else
        {
            // automat: w górę, z lekkim błądzeniem; w SVZ – zostaje i czeka na podział
            if (inSvz && Y < Cortex.SvzB - 15) { dx = 0; dy = 0; mag = 0; }
            else { _wA += RandN() * 0.4f * MathF.Sqrt(h); dx = 0.3f * MathF.Cos(_wA); dy = -1; mag = 1; float l = Hypot(dx, dy); dx /= l; dy /= l; }
        }
        Vx = dx * V * mag; Vy = dy * V * mag;
    }

    // ------------------------------------------------ komórka wielobiegunowa: neuryty, kontakt z aksonami TAG-1, pod podpłytkę
    void StepMultipolar(ScNeuron sc, bool me, InputState I, float h, float dt)
    {
        var f = F;
        // neuryty wysuwają się i chowają (~10 µm/h), bez stałej biegunowości (Tabata i Nakajima 2003)
        float mvA = me && I.Mag > 0.1f ? MathF.Atan2(I.Dy, I.Dx) : _wA;
        for (int i = 0; i < _nn; i++)
        {
            _nc[i] -= h;
            if (_nc[i] <= 0)
            {
                _nc[i] = Rand(0.8f, 2.5f);
                bool front = MathF.Cos(_na[i] - mvA) > 0.6f;
                _nt[i] = front ? Rand(8, 17) : Rand(2, 10);
            }
            float rate = _nt[i] > _nl[i] ? 10f : 14f;
            _nl[i] += Math.Clamp(_nt[i] - _nl[i], -rate * h, rate * h);
            _na[i] += RandN() * 0.25f * MathF.Sqrt(h);
        }
        // polaryzacja: koniec neurytu dotyka aksonu TAG-1 w dolnej strefie pośredniej → ten neuryt staje się aksonem (Namba i in. 2014)
        if (Ax == null && f != null && Y < Cortex.IzB + 30)
            for (int i = 0; i < _nn; i++)
            {
                if (_nl[i] < 4) continue;
                float tx = X + MathF.Cos(_na[i]) * (R0 + _nl[i]), ty = Y + MathF.Sin(_na[i]) * (R0 + _nl[i]);
                if (!f.NearestFascicle(tx, ty, 1.6f, out int j, out _) || !CortexField.Tag1(j)) continue;
                FormAxon(sc, tx, f.FascY(j, tx), i);
                break;
            }
        // pod podpłytką neurony podpłytki tworzą przejściowe synapsy glutaminianergiczne (NMDA) → przejście w komórkę dwubiegunową
        bool underSp = Y > Cortex.SpB + R0 && Y < Cortex.SpB + 30;
        if (underSp && Ax != null) Syn = Math.Min(1, Syn + h / 3f);
        else Syn = Math.Max(0, Syn - h / 6f);
        if (me && underSp && Ax == null) W.LogOnce("noax", "Komórka bez aksonu nie przechodzi w dwubiegunową – najpierw polaryzacja: dotknij neurytem aksonu TAG-1 w dolnej strefie pośredniej (ciemne pęczki).", "info", 20);
        if (Syn >= 1) { S = Stage.Bipolar; Trans = 0; sc.OnBipolar(this); return; }
        // ruch: powoli, w dowolną stronę, ~4,4 µm/h (1,6–6,4 µm/h)
        float v;
        float dx, dy, mag;
        if (me) { dx = I.Dx; dy = I.Dy; mag = I.Mag; v = 6f / 3600; }
        else
        {
            _wT -= h;
            if (_wT <= 0) { _wT = Rand(0.8f, 2.5f); _wA = -PI / 2 + RandN() * 1.1f; }
            // bez aksonu komórka krąży w dolnej strefie pośredniej (MAZ); z aksonem – dryfuje ku podpłytce
            if (Ax == null && Y < Cortex.IzB - 45) _wA = PI / 2 + RandN() * 0.6f;
            dx = MathF.Cos(_wA); dy = MathF.Sin(_wA); mag = underSp ? 0.1f : 1; v = Rand(1.6f, 6.4f) / 3600;
        }
        Vx = dx * v * mag; Vy = dy * v * mag;
        // komórka wielobiegunowa nie wchodzi w podpłytkę i płytkę korową – dalej prowadzi dopiero glej promienisty
        if (Y + Vy * dt < Cortex.SpB + R0 && Vy < 0) { Vy = 0; if (me) W.LogOnce("nosp", Ax == null ? "Komórka wielobiegunowa nie wejdzie w podpłytkę – potrzebny akson (kontakt z aksonami TAG-1) i przejście w komórkę dwubiegunową." : "Zostań tuż pod podpłytką – neurony podpłytki tworzą tu na Tobie synapsy.", "info", 15); }
    }
    void FormAxon(ScNeuron sc, float tx, float ty, int ni)
    {
        Ax = new Axon(X, Y, tx, ty) { TargetX = 2 * Cortex.XM - X };
        // neuryt, który dotknął aksonu, staje się aksonem – znika z listy neurytów
        _na[ni] = _na[_nn - 1]; _nl[ni] = _nl[_nn - 1]; _nt[ni] = _nt[_nn - 1]; _nc[ni] = _nc[_nn - 1]; _nn = Math.Max(1, _nn - 1);
        sc.OnAxon(this);
    }

    // ------------------------------------------------ przejście w komórkę dwubiegunową i przyczepienie do włókna gleju
    void StepBipolar(ScNeuron sc, float h, float dt)
    {
        Trans += h / 1.2f;
        for (int i = 0; i < _nn; i++) _nl[i] = Math.Max(0, _nl[i] - 12 * h);
        Lead = Math.Min(16, Lead + 14 * h);
        var f = F;
        if (Fiber == NoFiber && f != null && f.NearestFiber(X, Y, 24, out int fi, out float fx)) { Fiber = fi; Side = X >= fx ? 1 : -1; }
        if (Fiber != NoFiber && f != null)
        {
            float tx = f.FiberX(Fiber, Y) + Side * R0 * 0.55f;
            Vx = Math.Clamp(tx - X, -4, 4) / 3600 * 3;
        }
        if (Trans >= 1 && Fiber != NoFiber) { S = Stage.Locomotion; Phase = 0; sc.OnLocomotion(this); }
        else if (Trans >= 1.6f) { Trans = 0.8f; } // brak włókna w zasięgu – szuka dalej
    }

    // ------------------------------------------------ lokomocja po włóknie gleju: dylatacja → skok jądra → przerwa
    void StepLoco(ScNeuron sc, bool me, InputState I, float h, float dt)
    {
        var f = F; if (f == null) return;
        // włókno zniknęło (glej promienisty przekształca się w astrocyty po urodzeniu) – komórka zostaje, gdzie jest
        if (!sc.FibersPresent) { S = Stage.Stranded; SettledAge = sc.Age; Fiber = NoFiber; sc.OnStranded(this); return; }
        bool fwd, back = false; int sw = 0;
        if (me)
        {
            fwd = I.Mag > 0.1f && I.Dy < -0.35f;
            back = I.Mag > 0.1f && I.Dy > 0.5f;
            float dxs = MathF.Abs(I.Dx) > 0.6f && I.Mag > 0.1f ? MathF.Sign(I.Dx) : 0;
            if (dxs != 0 && _lastDx == 0) sw = (int)dxs;
            _lastDx = dxs;
        }
        else
        {
            fwd = _pauseT <= 0;
            _pauseT -= h; if (_pauseT < -Rand(2, 6)) _pauseT = Rand(0.3f, 1.2f); // krótkie przystanki
        }
        // przejście na sąsiednie włókno (~0,6 h)
        if (sw != 0 && _sw <= 0)
        {
            int nf = Fiber + sw; int guard = 0;
            while (!f.FiberExists(nf) && guard++ < 3) nf += sw;
            if (f.FiberExists(nf) && Y < f.FiberBottom(nf)) { _swDir = nf; _sw = 0.001f; _swFromX = X; }
        }
        if (_sw > 0)
        {
            _sw += h / 0.6f;
            float tx = f.FiberX(_swDir, Y) + Side * R0 * 0.55f;
            X = Lerp(_swFromX, tx, Smooth(Math.Min(1, _sw)));
            if (_sw >= 1) { Fiber = _swDir; _sw = 0; }
            return;
        }
        // wypustka wiodąca: w strefie pośredniej ~25 µm, w płytce korowej dłuższa (do ~50 µm)
        float leadT = Y > Cortex.SpB ? 26 : 50;
        Lead += Math.Clamp(leadT - Lead, -10 * h, 14 * h);
        float tipY = Y - R0 - Lead;
        if (tipY < Cortex.MzB - 6) { Lead = Y - R0 - (Cortex.MzB - 6); tipY = Cortex.MzB - 6; }
        // translokacja końcowa: koniec wypustki w strefie brzeżnej (relina), ciało tuż pod strefą PCZ (~50 µm, Sekine i in. 2011)
        if (tipY <= Cortex.MzB - 1 && Y <= Cortex.PczB + 26)
        {
            S = Stage.Translocation; Anchored = true; AnchorY = Rand(6, 11); Fiber = NoFiber; sc.OnTranslocation(this); return;
        }
        float v = 0;
        if (fwd)
        {
            // cykl ~0,85 h: 0,5 h wydłużanie i dylatacja, 0,25 h skok jądra ~36 µm/h, 0,1 h przerwa → średnio ~10–11 µm/h
            Phase += h / 0.85f;
            if (Phase >= 1) Phase -= 1;
            if (Phase > 0.59f && Phase < 0.88f) v = 36f / 3600;
        }
        else if (back) v = -3f / 3600;
        // naczynie promieniste na drodze: ciało nie przejdzie – trzeba zmienić włókno
        float ny2 = Y - v * dt;
        if (v > 0 && sc.F.VesselDist(X, ny2 - R0 * 0.6f, out _, out _) < 0.4f)
        {
            v = 0; if (me) W.LogOnce("vessel", "Naczynie krwionośne na drodze – przejdź na sąsiednie włókno gleju (A/D).", "info", 10);
        }
        Vy = -v;
        float fx = f.FiberX(Fiber, Y) + Side * R0 * 0.55f;
        Vx = (fx - X) / Math.Max(dt, 1e-3f) * 0.5f;
    }

    // ------------------------------------------------ translokacja końcowa: wypustka zakotwiczona w strefie brzeżnej, ciało „podciągane”
    void StepTrans(ScNeuron sc, float h, float dt)
    {
        // ostatnie ~50 µm (strefa PCZ) w niecałą godzinę (Sekine i in. 2011)
        float stopY = Cortex.MzB + R0 + 1.5f;
        Vy = -55f / 3600;
        Lead = Math.Max(0, Y - R0 - AnchorY);
        if (Y <= stopY) { Y = stopY; Vy = 0; S = Stage.Settled; SettledAge = sc.Age; Fixed = true; sc.OnSettled(this); }
    }

    void PreviewStep(float dt)
    {
        // karta menu: neuron w lokomocji – cykl skoków jądra w miejscu
        S = S == Stage.Ipc ? Stage.Locomotion : S; Lead = 30; Phase = (Phase + dt / 3600 / 0.85f) % 1;
        Shape();
    }

    public void StartApoptosis(string why)
    {
        if (S == Stage.Apoptotic) return;
        S = Stage.Apoptotic; Apo = 0;
        if (IsPlayer || Cone != null && W.Player == Cone) W.Log(why, "bad");
    }

    // ------------------------------------------------ rysowanie
    static Color Tt(int r, int g, int b, float a) => new(r / 255f / SpriteAtlas.NeuBase, g / 255f / SpriteAtlas.NeuBase, b / 255f / SpriteAtlas.NeuBase, a);
    static readonly Color Body = Tt(150, 144, 158, 0.62f), BodyIpc = Tt(148, 142, 158, 0.6f), BodyMit = Tt(168, 162, 172, 0.66f), BodyApo = Tt(150, 140, 146, 0.75f);
    static readonly Color ProcC = Rgb(96, 90, 106, 0.42f), Halo = Rgb(255, 255, 240, 0.14f);
    public static readonly Color GfpC = Rgb(70, 200, 90, 0.9f), GfpD = Rgb(50, 190, 75, 0.75f), GfpGlow = Rgb(40, 200, 70, 0.16f);

    /// <summary>Kształt ciała (półosie wzdłuż i w poprzek osi) i kierunek osi.</summary>
    void Soma(out float a, out float b, out float ang)
    {
        ang = -PI / 2; a = R0; b = R0;
        switch (S)
        {
            case Stage.Ipc: a = R0 * 1.15f; b = R0 * 0.9f; break;
            case Stage.Multipolar: a = R0 * (1.06f + 0.05f * Noise1(W.T * 0.3f + _seed)); b = R0 * 0.96f; ang = _seed; break;
            case Stage.Bipolar: case Stage.Translocation: a = R0 * 1.25f; b = R0 * 0.82f; break;
            case Stage.Locomotion:
            {
                // w czasie skoku jądra ciało wydłuża się ku dylatacji
                float nk = Phase > 0.59f && Phase < 0.88f ? MathF.Sin((Phase - 0.59f) / 0.29f * PI) : 0;
                a = R0 * (1.22f + 0.3f * nk); b = R0 * (0.84f - 0.1f * nk); break;
            }
            case Stage.Settled: case Stage.Stranded: a = R0 * 1.18f; b = R0 * 0.92f; break;
            case Stage.Apoptotic: a = b = R0 * (1 - 0.35f * Smooth(Apo)); break;
        }
    }

    public override void Draw(Painter p, View v)
    {
        bool fl = W.Fluo;
        if (fl && !Gfp) return;
        bool far = v.Px > 0.35f;
        float lw = Math.Max(v.Px, 0.45f);
        Soma(out float sa, out float sb, out float ang);
        float ca = MathF.Cos(ang), sn = MathF.Sin(ang);
        var pc = fl ? GfpD : ProcC;
        // ---- wypustki (pod ciałem)
        if (S == Stage.Ipc && Y > Cortex.VzB - 30)
        {
            // krótka wypustka szczytowa do powierzchni komory (odrywa się, gdy komórka odchodzi – delaminacja)
            float yb = Cortex.VzB;
            if (yb - Y < 22) p.Seg(X, Y + sb * 0.8f, X + 0.4f, yb, Math.Max(v.Px, 0.6f), pc);
        }
        Span<Vector2> cp = stackalloc Vector2[12]; Span<float> cw = stackalloc float[12];
        if (S == Stage.Ipc)
        {
            int n = Curve(cp, cw, X, Y - sa * 0.85f, -PI / 2 + 0.1f * MathF.Sin(_seed), 6.5f, 0.02f * MathF.Sin(_seed * 3), 0.15f, _seed, Math.Max(lw, 0.8f), 0.3f, out _);
            p.Taper(cp[..n], cw[..n], pc);
        }
        if (S == Stage.Multipolar || S == Stage.Bipolar)
            for (int i = 0; i < _nn; i++) Neurite(p, i, _na[i], _nl[i], pc, lw, fl, far);
        if (S is Stage.Bipolar or Stage.Locomotion or Stage.Translocation) LeadProc(p, v, pc, lw, fl, far);
        if (S is Stage.Settled or Stage.Stranded) Dendrites(p, v, pc, lw, fl, far);
        if (Ax == null && S is Stage.Locomotion or Stage.Bipolar && !Gfp)
        {
            // wypustka tylna (akson nieznakowanych komórek nie jest rysowany)
            int n = Curve(cp, cw, X, Y + sb * 0.8f, PI / 2 + 0.05f * MathF.Sin(_seed), 16, 0.01f * MathF.Sin(_seed * 2), 0.08f, _seed, Math.Max(lw * 0.9f, 0.55f), 0.3f, out _);
            p.Taper(cp[..n], cw[..n], Mul(pc, 0.7f));
        }
        // ---- ciało
        // mitoza: komórka zaokrągla się i sztywnieje – rysowana z tekstury płytki metafazowej
        if (S == Stage.Mitosis) { DrawMitosis(p, v, fl); return; }
        // miękkie ciało: tekstura (jądro, chromatyna, jąderko, rąbek cytoplazmy) rozpięta na odkształcanym obrysie błony
        var m = Mem;
        float bx = MathF.Cos(A), by = MathF.Sin(A);
        if (fl)
        {
            int n = m.Smooth(p.Shape, 2); var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
            p.Glow(X, Y, Math.Max(sa, sb) * 1.9f, GfpGlow);
            p.FillFan(sh, m.Cx, m.Cy, S == Stage.Apoptotic ? Mul(GfpC, 1 - 0.6f * Apo) : GfpC);
            // jądro ciut ciemniejsze (GFP w cytoplazmie i jądrze, ale jądro jest grubsze optycznie)
            p.FillEllipse(X + bx * sa * 0.08f, Y + by * sa * 0.08f, sa * 0.62f, sb * 0.62f, A, Rgb(40, 150, 60, 0.28f));
            return;
        }
        var col = S == Stage.Apoptotic ? BodyApo : S == Stage.Ipc ? BodyIpc : Body;
        if (p.Tex != null)
        {
            if (S == Stage.Apoptotic) p.Tex.TexSoft(m, SpriteAtlas.Uv[SpriteAtlas.Pykno], 0.94f, 0.94f, col);
            else
            {
                bool el = sa > sb * 1.3f;
                p.Tex.TexSoft(m, SpriteAtlas.Uv[el ? SpriteAtlas.NeuEv(Id) : SpriteAtlas.NeuRv(Id)], 0.94f, el ? 0.94f / 1.6f : 0.94f, col);
            }
        }
        else
        {
            int n = m.Smooth(p.Shape, 2); var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
            p.FillFan(sh, m.Cx, m.Cy, Rgb(140, 134, 150, 0.55f));
            p.FillEllipse(X, Y, sa * 0.72f, sb * 0.78f, A, Rgb(104, 96, 120, 0.4f));
            p.Dot(X + bx * sa * 0.1f, Y + by * sa * 0.1f, 0.7f, Rgb(50, 44, 62, 0.7f));
        }
    }

    /// <summary>
    /// Ciało komórki z tekstury kory (duże jądro, cienki rąbek cytoplazmy) jako elipsa o półosiach a (wzdłuż ang) i b.
    /// Tekstura NeuE ma elipsę 1,6 : 1 wpisaną w pole (oś długa wzdłuż x), NeuR – koło; bok pola = oś elipsy / 0,94.
    /// </summary>
    public static void EllipseSprite(Painter tex, float x, float y, float a, float b, float ang, Color col, int id = 0)
    {
        bool el = a > b * 1.3f;
        var uv = SpriteAtlas.Uv[el ? SpriteAtlas.NeuEv(id) : SpriteAtlas.NeuRv(id)];
        tex.Sprite(x, y, 2 * a / 0.94f, 2 * b * (el ? 1.6f : 1) / 0.94f, ang, uv, col);
    }

    /// <summary>
    /// Punkty wypustki jako zwężającej się krzywej: od (x0,y0) w kierunku a, długość l, krzywizna k (rad/µm), falowanie wob
    /// (faza ph); grubość od w0 przy nasadzie do w1 na końcu. Zwraca liczbę punktów, aEnd – kierunek na końcu.
    /// </summary>
    static int Curve(Span<Vector2> pts, Span<float> ws, float x0, float y0, float a, float l, float k, float wob, float ph, float w0, float w1, out float aEnd)
    {
        int n = Math.Clamp((int)(l / 1.5f) + 2, 3, pts.Length);
        float x = x0, y = y0, ds = l / (n - 1), th = a;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            pts[i] = new Vector2(x, y);
            ws[i] = w1 + (w0 - w1) * (1 - t) * MathF.Exp(-1.5f * t);
            th = a + k * t * l + wob * t * MathF.Sin(t * 5.2f + ph);
            x += MathF.Cos(th) * ds; y += MathF.Sin(th) * ds;
        }
        aEnd = th; return n;
    }

    /// <summary>Maleńki stożek wzrostu na końcu neurytu: zgrubienie i do trzech krótkich filopodiów, które wysuwają się i chowają.</summary>
    void TipCone(Painter p, Vector2 e, float a, int key, float sz, Color c, bool fl)
    {
        float t = W.T, ca = MathF.Cos(a), sa = MathF.Sin(a);
        p.FillEllipse(e.X + ca * 0.2f * sz, e.Y + sa * 0.2f * sz, 0.5f * sz, 0.32f * sz, a, Mul(c, 0.8f));
        if (fl) p.Glow(e.X, e.Y, 1.6f * sz, GfpGlow);
        // filopodia (~0,1–0,2 µm) są na granicy rozdzielczości – w DIC ledwie widoczne
        c = Mul(c, fl ? 0.8f : 0.6f);
        Span<Vector2> q = stackalloc Vector2[3];
        Span<float> w = stackalloc float[] { 0.18f, 0.13f, 0.08f };
        var s0 = new Vector2(e.X + ca * 0.5f * sz, e.Y + sa * 0.5f * sz);
        for (int j = 0; j < 3; j++)
        {
            float ph = t * (0.3f + 0.2f * Hash2(key, j, 11)) + Hash2(key, j, 12);
            int cyc = (int)MathF.Floor(ph); float u = ph - cyc;
            float L = sz * (0.8f + 2.2f * Hash2(key * 3 + j, cyc, 13)) * Smooth(u / 0.4f) * Smooth((1 - u) / 0.3f);
            if (L < 0.25f) continue;
            float fa = a + (Hash2(key * 3 + j, cyc, 14) - 0.5f) * 1.8f, b = (Hash2(key * 3 + j, cyc, 15) - 0.5f) * 0.6f;
            q[0] = s0; q[1] = s0 + new Vector2(MathF.Cos(fa), MathF.Sin(fa)) * (L * 0.5f); q[2] = q[1] + new Vector2(MathF.Cos(fa + b), MathF.Sin(fa + b)) * (L * 0.5f);
            p.Taper(q, w, c);
        }
    }

    /// <summary>
    /// Neuryt komórki wielobiegunowej: cienki, zwężający się, lekko wygięty (wygięcie powoli się zmienia – neuryty pełzną,
    /// zginają się i prostują), czasem z gałązką boczną; na końcu maleńki stożek wzrostu.
    /// </summary>
    void Neurite(Painter p, int i, float a, float l, Color c, float lw, bool fl, bool far)
    {
        if (l < 0.5f) return;
        float ca = MathF.Cos(a), sa = MathF.Sin(a), x0 = X + ca * R0 * 0.8f, y0 = Y + sa * R0 * 0.8f;
        if (far) { p.Seg(x0, y0, X + ca * (R0 + l), Y + sa * (R0 + l), Math.Max(lw, 0.5f), c); return; }
        float s = _seed + i * 7.3f, t = W.T;
        int key = Id * 8 + i;
        Span<Vector2> pts = stackalloc Vector2[12]; Span<float> ws = stackalloc float[12];
        int n = Curve(pts, ws, x0, y0, a, l + R0 * 0.2f, 0.075f * Noise1(s + t * 0.1f), 0.26f + 0.12f * Noise1(s * 1.7f), s + t * 0.2f, Math.Max(lw, 1.05f), 0.3f, out float ae);
        if (fl) p.SoftStroke(pts[..n], false, 1.8f, GfpGlow);
        p.Taper(pts[..n], ws[..n], c);
        if (l > 7 && Hash2(key, 0, 21) < 0.45f)
        {
            int j = Math.Min(n - 2, (int)(n * (0.45f + 0.2f * Hash2(key, 0, 22))));
            var d = pts[j + 1] - pts[j];
            float ba = MathF.Atan2(d.Y, d.X) + (Hash2(key, 0, 23) < 0.5f ? -1 : 1) * (0.6f + 0.4f * Hash2(key, 0, 24));
            Span<Vector2> bp = stackalloc Vector2[8]; Span<float> bw = stackalloc float[8];
            int m = Curve(bp, bw, pts[j].X, pts[j].Y, ba, l * 0.35f, 0.06f * Noise1(s * 2.3f + t * 0.12f), 0.15f, s * 3.1f + t * 0.2f, ws[j] * 0.8f, 0.25f, out float be);
            p.Taper(bp[..m], bw[..m], c);
            TipCone(p, bp[m - 1], be, key * 2 + 1, 0.7f, c, fl);
        }
        TipCone(p, pts[n - 1], ae, key * 2, 1, c, fl);
    }

    /// <summary>Wypustka wiodąca: gruba przy ciele, zwężająca się; z poszerzeniem (dylatacją) przed skokiem jądra; na końcu rozgałęziona.</summary>
    void LeadProc(Painter p, View v, Color c, float lw, bool fl, bool far)
    {
        if (Lead < 0.5f) return;
        // nasada wypustki w czubku „kropli” – na przednim węźle odkształcanej błony (bez nakładania na ciało)
        var mem = Mem;
        float x0 = mem != null ? Lerp(X, mem.Px[0], 0.84f) : X, y0 = mem != null ? Lerp(Y, mem.Py[0], 0.84f) : Y - R0, fx;
        float ty = Anchored ? AnchorY : Y - R0 - Lead, l = y0 - ty;
        if (l < 0.5f) return;
        var f = F;
        if (f != null && Fiber != NoFiber) fx = f.FiberX(Fiber, ty) + Side * 0.7f;
        else fx = X + 0.6f * MathF.Sin(_seed);
        float tx = fx;
        // zwężający się wielokąt wzdłuż osi
        int n = Math.Clamp((int)(l / 2.2f), 5, 18);
        Span<Vector2> L = stackalloc Vector2[40];
        int k = 0;
        float dil = S == Stage.Locomotion ? Smooth(Math.Min(1, Phase / 0.55f)) * (Phase < 0.88f ? 1 : 0) : 0;
        float dilT = Math.Min(10f, l * 0.35f);
        // profil grubości: szeroka szyjka zlewająca się z ciałem, szybkie zwężenie, cienki koniec; poszerzenie przed skokiem jądra
        float Wd(float t) { float d = t * l, dd = (d - dilT) / 2.6f; return 0.3f + 0.95f * MathF.Exp(-d / 5f) + 0.25f * (1 - t) + 1.5f * dil * MathF.Exp(-dd * dd); }
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n, yy = Lerp(y0, ty, t), xx = Lerp(x0, tx, t) + 0.6f * MathF.Sin(t * 5 + _seed) * t;
            L[k++] = new Vector2(xx - Wd(t), yy);
        }
        for (int i = n; i >= 0; i--)
        {
            float t = (float)i / n, yy = Lerp(y0, ty, t), xx = Lerp(x0, tx, t) + 0.6f * MathF.Sin(t * 5 + _seed) * t;
            L[k++] = new Vector2(xx + Wd(t), yy);
        }
        var poly = L[..k];
        if (fl) { p.SoftStroke(poly, true, 2.2f, GfpGlow); FillPoly(p, poly, GfpD); }
        else
        {
            FillPoly(p, poly, c);
        }
        // koniec: w strefie pośredniej rozgałęziony, gałązki z maleńkimi stożkami wzrostu; w strefie brzeżnej – kępka
        // (przyszły pęczek dendrytu szczytowego)
        if (!far)
        {
            int nb = Anchored ? 3 : 2;
            Span<Vector2> bp = stackalloc Vector2[8]; Span<float> bw = stackalloc float[8];
            for (int b = 0; b < nb; b++)
            {
                int key = Id * 8 + 5 + b;
                float s = _seed + b * 3.7f;
                float ba = -PI / 2 + (b - (nb - 1) * 0.5f) * 0.62f + 0.2f * Noise1(W.T * 0.15f + s);
                float bl = (Anchored ? 4.5f + 1.5f * b : 3 + 1.2f * b) * (0.8f + 0.4f * Hash2(key, 1, 31));
                int m = Curve(bp, bw, tx, ty, ba, bl, 0.08f * Noise1(s * 1.3f + W.T * 0.05f), 0.18f, s + W.T * 0.2f, Math.Max(lw, 0.6f), 0.28f, out float be);
                p.Taper(bp[..m], bw[..m], c);
                if (!Anchored) TipCone(p, bp[m - 1], be, key, 0.8f, c, fl);
            }
        }
    }
    /// <summary>
    /// Wypełnienie pasa (wielokąta z lewą krawędzią w pierwszej połowie punktów i prawą – w odwrotnej kolejności w drugiej),
    /// który nie musi być gwiaździsty: czworokąty między krawędziami, bez frędzli na wewnętrznych bokach.
    /// </summary>
    static void FillPoly(Painter p, ReadOnlySpan<Vector2> poly, Color c)
    {
        int n = poly.Length / 2;
        Span<Vector2> q = stackalloc Vector2[4];
        for (int i = 0; i < n - 1; i++)
        {
            q[0] = poly[i]; q[1] = poly[i + 1]; q[2] = poly[poly.Length - 2 - i]; q[3] = poly[poly.Length - 1 - i];
            p.FillFan(q, (q[0].X + q[1].X + q[2].X + q[3].X) * 0.25f, (q[0].Y + q[1].Y + q[2].Y + q[3].Y) * 0.25f, c, false);
        }
    }

    /// <summary>Osiadły neuron: dendryt szczytowy do strefy brzeżnej z kępką, rosnące dendryty podstawne.</summary>
    void Dendrites(Painter p, View v, Color c, float lw, bool fl, bool far)
    {
        Span<Vector2> q = stackalloc Vector2[12]; Span<float> qw = stackalloc float[12];
        if (S == Stage.Settled)
        {
            // dendryt szczytowy: prawie prosty (biegnie promieniście), zwęża się ku strefie brzeżnej, tam kępka gałązek
            float ty = AnchorY, y0 = Y - R0 * 0.9f, dl = y0 - ty, tx = X + 0.5f * MathF.Sin(_seed);
            int n = far ? 2 : Math.Clamp((int)(dl / 3) + 2, 3, 12);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (n - 1);
                q[i] = new Vector2(Lerp(X, tx, t) + (far ? 0 : 0.35f * MathF.Sin(t * 7 + _seed) * MathF.Sin(t * PI)), Lerp(y0, ty, t));
                qw[i] = Math.Max(lw * (1 - 0.3f * t), 1.5f - 0.8f * t);
            }
            if (fl) p.SoftStroke(q[..n], false, 2.4f, GfpGlow);
            p.Taper(q[..n], qw[..n], c);
            for (int b = 0; b < 4; b++)
            {
                float ba = -PI / 2 + (b - 1.5f) * 0.65f + _apicalA, bl = 5 + 2.5f * (b % 2);
                if (far) { p.Seg(tx, ty, tx + MathF.Cos(ba) * bl, ty + MathF.Sin(ba) * bl * 0.6f, Math.Max(lw, 0.45f), c); continue; }
                int m = Curve(q, qw, tx, ty, ba, bl, 0.06f * MathF.Sin(_seed + b * 2.1f), 0.12f, _seed + b, Math.Max(lw, 0.6f), 0.3f, out _);
                p.Taper(q[..m], qw[..m], c);
            }
        }
        // dendryty podstawne rosną po urodzeniu: cienkie, lekko wygięte, z maleńkimi stożkami wzrostu
        for (int i = 0; i < _da.Length; i++)
        {
            if (_dl[i] < 0.5f) continue;
            float a = _da[i], x0 = X + MathF.Cos(a) * R0 * 0.8f, y0 = Y + MathF.Sin(a) * R0 * 0.8f;
            if (far) { p.Seg(x0, y0, X + MathF.Cos(a) * (R0 + _dl[i]), Y + MathF.Sin(a) * (R0 + _dl[i]), Math.Max(lw, 0.6f), c); continue; }
            int m = Curve(q, qw, x0, y0, a, _dl[i] + R0 * 0.2f, 0.05f * MathF.Sin(_seed * 1.9f + i * 2.3f), 0.15f, _seed + i * 1.7f, Math.Max(lw, 1f), 0.32f, out float ae);
            p.Taper(q[..m], qw[..m], c);
            TipCone(p, q[m - 1], ae, Id * 8 + i, 0.6f, c, fl);
        }
    }

    void DrawMitosis(Painter p, View v, bool fl)
    {
        // zaokrąglona komórka z płytką metafazową, potem bruzda podziałowa
        float r = R0 * 1.05f, sep = Smooth((Mit - 0.6f) / 0.4f) * R0 * 0.55f;
        if (fl)
        {
            for (int s = -1; s <= 1; s += 2) { p.Glow(X + s * sep, Y, r * 1.6f, GfpGlow); p.FillCircle(X + s * sep, Y, r * (sep > 0.1f ? 0.78f : 1), GfpC); if (sep < 0.1f) break; }
            return;
        }
        if (p.Tex != null)
        {
            if (sep < 0.1f) p.Tex.Sprite(X, Y, r * 2 / 0.94f, r * 2 / 0.94f, 0, SpriteAtlas.Uv[SpriteAtlas.Mitosis], BodyMit);
            else for (int s = -1; s <= 1; s += 2) p.Tex.Sprite(X + s * sep, Y, r * 1.6f / 0.94f, r * 1.6f / 0.94f, 0, SpriteAtlas.Uv[SpriteAtlas.NeuR], BodyMit);
        }
        else p.FillCircle(X, Y, r, Rgb(150, 144, 160, 0.55f));
    }

    /// <summary>Akson (rysowany przez scenariusz – także wtedy, gdy ciało komórki jest zamrożone w dalekim chunku).</summary>
    public void DrawAxon(Painter p, View v, bool fl)
    {
        var a = Ax; if (a == null || !a.Near(v, 20)) return;
        var buf = p.Buf2; int n = 0;
        // w DIC akson z kohorty wygląda jak każdy inny akson pęczka; własny akson gracza – odrobinę wyraźniej
        bool mine = Sc?.Me == this;
        float lw = fl ? Math.Max(v.Px * 1.2f, 0.7f) : Math.Max(v.Px, mine ? 0.5f : 0.4f);
        var col = fl ? GfpD : mine ? Rgb(84, 78, 96, 0.3f) : Rgb(86, 82, 96, 0.2f);
        float step = Math.Max(1, v.Px * 3);
        void Flush()
        {
            if (n < 2) { n = 0; return; }
            var s = new ReadOnlySpan<Vector2>(buf, 0, n);
            if (fl) p.SoftStroke(s, false, 2.6f, GfpGlow);
            p.Thin(s, false, lw, col); n = 0;
        }
        Vector2 last = default;
        for (int i = 0; i < a.P.Count; i++)
        {
            var q = a.P[i];
            bool vis = v.Vis(q.X, q.Y, 8);
            if (!vis) { if (n > 0 && n < buf.Length) buf[n++] = q; Flush(); continue; }
            if (n > 0 && (q - last).Length() < step && i != a.P.Count - 1) continue;
            if (n == 0 && i > 0) buf[n++] = a.P[i - 1];
            if (n < buf.Length) buf[n++] = q; last = q;
            if (n >= buf.Length - 2) { Flush(); buf[n++] = q; }
        }
        Flush();
        foreach (var br in a.Branches)
        {
            n = 0; foreach (var q in br) if (n < buf.Length) buf[n++] = q;
            Flush();
        }
    }
}

/// <summary>
/// Stożek wzrostu aksonu sterowany przez gracza. Domena centralna (mikrotubule, pęcherzyki, mitochondria) przechodzi w szyjkę
/// aksonu; przed nią cienka, płaska blaszka (lamellipodium) z filopodiami, które wysuwają się, badają teren i chowają.
/// W całej istocie białej – także przy linii środkowej – stożki aksonów spoidłowych mają szerokie blaszki i liczne, często
/// długie filopodia (Norris i Kalil 1990) i ciągle zmieniają kształt; w korze docelowej są zwarte i mniejsze (Halloran
/// i Kalil 1994). Klin glejowy i szare pokrywające wydzielają Slit2 – stożek z receptorem Robo zapada się i cofa
/// (Shu i Richards 2001).
/// </summary>
public sealed class GrowthCone : Ent
{
    public readonly CortNeuron Owner;
    readonly float _seed; readonly int _sd;
    public override bool Swims => false;
    public override bool Rotates => false;
    public override float Radius => 3;
    public override string Note => Owner.Ax == null ? "" : (Owner.Ax.Crossed ? "za linią środkową" : "przed linią środkową") + $" · akson {Owner.Ax.Len:0} µm";
    public GrowthCone(World w, CortNeuron owner) : base(w, owner.Ax.Tip.X, owner.Ax.Tip.Y, PI)
    {
        Owner = owner; Kind = Kind.Neuron; Layer = 3; Solid = false; FlowK = 0; Name = "stożek wzrostu aksonu"; Latin = "growth cone";
        R = 3; _seed = Rand(0, 99); _sd = (int)(_seed * 1000); A = owner.Ax.Dir;
    }
    public override bool CanControl() => Owner.Alive && Owner.S != CortNeuron.Stage.Apoptotic;
    public override void UpdatePrim() => SetCircle(2);
    public override void Step(float dt, float gdt)
    {
        Vx = Vy = 0;
        var a = Owner.Ax; if (a == null || W.Sc is not ScNeuron sc) return;
        if (IsPlayer && CanControl())
        {
            var I = W.Input;
            sc.GrowStep(Owner, a, I.Mag > 0.1f ? MathF.Atan2(I.Dy, I.Dx) : float.NaN, I.Mag, dt, true);
        }
        X = a.Tip.X; Y = a.Tip.Y; A = a.Dir;
    }

    const int NV = 30;
    public override void Draw(Painter p, View v)
    {
        var a = Owner.Ax; if (a == null) return;
        bool fl = W.Fluo;
        var sc = W.Sc as ScNeuron;
        float t = W.T, ang = a.Dir, coll = a.Collapse;
        // w istocie białej szeroka blaszka i liczne filopodia; w korze docelowej mały, zwarty stożek; po sygnale Slit2 – zapadnięty
        bool cortex = sc != null && sc.InTargetCortex(X, Y);
        float s = cortex ? 0.55f : 1f, spread = cortex ? 0.5f : 1f, k = 1 - 0.8f * coll, ys = 0.45f + 0.55f * spread;
        float ca = MathF.Cos(ang), sa = MathF.Sin(ang);
        Color veilC, edgeC, coreC, filoC;
        if (fl) { veilC = Mul(CortNeuron.GfpD, 0.5f); edgeC = Mul(CortNeuron.GfpD, 0.7f); coreC = CortNeuron.GfpC; filoC = CortNeuron.GfpD; p.Glow(X, Y, 6 * s + 2, CortNeuron.GfpGlow); }
        else { veilC = Rgb(128, 122, 138, 0.24f); edgeC = Rgb(78, 72, 90, 0.26f); coreC = Rgb(96, 90, 108, 0.4f); filoC = Rgb(74, 68, 86, 0.3f); }
        // szyjka: koniec aksonu rozszerza się w domenę centralną (wzdłuż prawdziwej drogi aksonu)
        Neck(p, a, 1.9f * s * (1 - 0.35f * coll), coreC);
        // blaszka: cienka i płaska (w DIC ledwie widoczna); jej płaty wysuwają się i cofają
        float cx = X + ca * 0.9f * s, cy = Y + sa * 0.9f * s;
        Span<Vector2> veil = stackalloc Vector2[NV]; Span<float> vr = stackalloc float[NV];
        for (int i = 0; i < NV; i++)
        {
            float th = -PI + i * TAU / NV, fw = MathF.Max(0, MathF.Cos(th));
            float r = s * (1.1f + 2.7f * MathF.Pow(fw, 0.8f) + (0.95f * Noise1(th * 1.5f + t * 0.45f + _seed) + 0.45f * Noise1(th * 3.4f - t * 0.7f + _seed * 1.9f)) * (0.25f + 0.75f * fw));
            r = Math.Max(r, s * 0.9f) * k;
            vr[i] = r;
            float lx = MathF.Cos(th) * r, ly = MathF.Sin(th) * r * ys;
            veil[i] = new Vector2(cx + lx * ca - ly * sa, cy + lx * sa + ly * ca);
        }
        p.FillFan(veil, cx, cy, veilC);
        p.Stroke(veil, true, Math.Max(v.Px * 0.8f, 0.1f), edgeC);
        // domena centralna: grubsza i ciemniejsza, z drobnymi organellami (pęcherzyki, mitochondria)
        p.FillEllipse(X + ca * 0.5f * s, Y + sa * 0.5f * s, 1.6f * s, 1.05f * s * (1 - 0.2f * coll), ang, coreC);
        if (!fl && v.Px < 0.3f)
            for (int i = 0; i < 4; i++)
            {
                float u = (Hash2(i, 1, _sd) - 0.5f) * 2.4f * s + 0.25f * Noise1(t * 0.6f + i * 2.7f), w = ((Hash2(i, 2, _sd) - 0.5f) * 1.5f + 0.2f * Noise1(t * 0.5f + i * 5.1f + 9)) * s;
                p.Dot(X + ca * (0.5f * s + u) - sa * w, Y + sa * (0.5f * s + u) + ca * w, 0.16f, Rgb(60, 54, 72, 0.25f));
            }
        // filopodia: każde żyje własnym cyklem – wysuwa się, bada teren i chowa, a następne rośnie w innym kierunku;
        // zaczyna się w blaszce jako żebro aktynowe. Większość krótka, część długa.
        int nf = (int)MathF.Round((cortex ? 3 : 9) * k);
        Span<Vector2> q = stackalloc Vector2[4];
        Span<float> qw = stackalloc float[] { 0.2f, 0.22f, 0.15f, 0.08f };
        for (int j = 0; j < nf; j++)
        {
            float ph = t * (0.3f + 0.25f * Hash2(j, 7, _sd)) + Hash2(j, 8, _sd);
            int cyc = (int)MathF.Floor(ph); float u = ph - cyc;
            float env = Smooth(u / 0.4f) * Smooth((1 - u) / 0.3f);
            float h1 = Hash2(j, cyc, _sd + 1), h2 = Hash2(j, cyc, _sd + 2), h3 = Hash2(j, cyc, _sd + 3);
            float L = s * (2.5f + 8.5f * h2 * h2) * env * k;
            if (L < 0.3f) continue;
            float th = (h1 - 0.5f) * (1.2f + 1.3f * spread);
            float fi = (th + PI) / TAU * NV; int i0 = (int)fi % NV;
            float rv = Lerp(vr[i0], vr[(i0 + 1) % NV], fi - MathF.Floor(fi));
            // kierunek w świecie: od środka blaszki ku jej brzegowi w tym miejscu, z lekkim wygięciem
            float lx = MathF.Cos(th), ly = MathF.Sin(th) * ys, ll = MathF.Sqrt(lx * lx + ly * ly);
            float wa = MathF.Atan2(lx * sa + ly * ca, lx * ca - ly * sa), rr = rv * ll;
            float b = (h3 - 0.5f) * 0.6f + 0.1f * Noise1(t * 0.8f + j * 3.3f + _seed);
            var c0 = new Vector2(cx, cy);
            q[0] = c0 + new Vector2(MathF.Cos(wa), MathF.Sin(wa)) * (rr * 0.55f);
            q[1] = c0 + new Vector2(MathF.Cos(wa), MathF.Sin(wa)) * rr;
            q[2] = q[1] + new Vector2(MathF.Cos(wa + b * 0.5f), MathF.Sin(wa + b * 0.5f)) * (L * 0.5f);
            q[3] = q[2] + new Vector2(MathF.Cos(wa + b), MathF.Sin(wa + b)) * (L * 0.5f);
            p.Taper(q, qw, filoC);
        }
    }
    /// <summary>Szyjka stożka: ostatnie kilka µm aksonu pogrubia się ku domenie centralnej (w – szerokość przy stożku).</summary>
    static void Neck(Painter p, Axon a, float w, Color c)
    {
        const int N = 8;
        Span<Vector2> q = stackalloc Vector2[N]; Span<float> ws = stackalloc float[N];
        float want = 3.2f * w, len = 0;
        int n = 0; q[n] = a.Tip; ws[n++] = w;
        for (int i = a.P.Count - 2; i >= 0 && n < N; i--)
        {
            var pt = a.P[i]; float sl = (pt - q[n - 1]).Length();
            if (sl < 1e-3f) continue;
            bool last = len + sl >= want;
            if (last) pt = q[n - 1] + (pt - q[n - 1]) * ((want - len) / sl);
            len = last ? want : len + sl;
            q[n] = pt; ws[n++] = 0.5f + (w - 0.5f) * MathF.Exp(-3f * len / want);
            if (last) break;
        }
        if (n >= 2) p.Taper(q[..n], ws[..n], c);
    }
}

/// <summary>
/// Interneuron (GABA) wędrujący stycznie z wyniosłości zwojowej przyśrodkowej: w strumieniach strefy brzeżnej i dolnej
/// strefy pośredniej/podkomorowej, średnio 12 ± 6 µm/h w żywym zarodku (Yanagida i in. 2012). Rozgałęziona wypustka wiodąca;
/// jądro wchodzi do jednej gałęzi, a pozostałe się cofają – tak zmienia kierunek.
/// </summary>
public sealed class Interneuron : Ent
{
    readonly float _v, _yT, _seed, _home; float _heading, _b1, _b2, _l1, _l2, _choose, _ph;
    /// <summary>Środek pasa strumienia (y), w którym wędruje.</summary>
    public float StreamY => _yT;
    static readonly Color Col = new(152 / 255f / SpriteAtlas.NeuBase, 146 / 255f / SpriteAtlas.NeuBase, 160 / 255f / SpriteAtlas.NeuBase, 0.6f);
    public override bool Swims => false;
    public override bool Rotates => false;
    public override float Radius => 4;
    public Interneuron(World w, float x, float y, float streamY) : base(w, x, y, PI)
    {
        Kind = Kind.Neuron; Layer = 2; Soft = 0.5f; Mass = 4; FlowK = 0; R = 4.4f;
        Name = "interneuron (GABA)"; Latin = "interneuron z MGE/CGE";
        _v = Clamp(12 + 6 * RandN(), 3, 30) / 3600; _yT = streamY; _seed = Rand(0, 99);
        // strumienie styczne biegną od wyniosłości zwojowych (bocznie, w dół przekroju) ku grzbietowi i linii środkowej
        _home = x > Cortex.XM ? PI : 0;
        _heading = _home + Rand(-0.3f, 0.3f); _b1 = Rand(0.25f, 0.5f); _b2 = -Rand(0.25f, 0.5f); _l1 = Rand(8, 18); _l2 = Rand(6, 16); _choose = Rand(1, 4); _ph = Rand(0, 1);
    }
    public override void UpdatePrim() => SetCircle(3.6f);
    public override void Step(float dt, float gdt)
    {
        float h = dt / 3600;
        if (W.Preview) return;
        // wybór gałęzi co kilka godzin: jądro wchodzi do jednej z nich (Yanagida i in. 2012)
        _choose -= h;
        if (_choose <= 0)
        {
            _choose = Rand(1.5f, 4);
            float pick = Chance(0.5f) ? _b1 : _b2;
            _heading += pick * 0.8f;
            _b1 = Rand(0.2f, 0.55f); _b2 = -Rand(0.2f, 0.55f); _l1 = 2; _l2 = 2;
        }
        _l1 = Math.Min(20, _l1 + 6 * h); _l2 = Math.Min(16, _l2 + 5 * h);
        // utrzymuje się w strumieniu (miękki powrót ku środkowi pasa), kierunek ogólnie ku linii środkowej
        float dy = _yT - Y;
        _heading = WrapA(_heading); float want = _home + Math.Clamp(dy * 0.04f, -0.6f, 0.6f) * (_home > 1 ? -1 : 1);
        _heading = Turn(_heading, want, 0.25f * h);
        // ruch skokowy: jądro przesuwa się zrywami (cykl ~1 h)
        _ph = (_ph + h) % 1;
        float k = _ph < 0.6f ? 0.35f : 2f;
        float v = _v * k;
        Vx = MathF.Cos(_heading) * v; Vy = MathF.Sin(_heading) * v; A = _heading;
    }
    public override void Draw(Painter p, View v)
    {
        if (W.Fluo) return;
        float ca = MathF.Cos(_heading), sa = MathF.Sin(_heading), lw = Math.Max(v.Px, 0.5f);
        var c = Rgb(92, 86, 100, 0.42f);
        // wypustka wiodąca z dwiema gałęziami
        float lx = X + ca * 14, ly = Y + sa * 14;
        p.Line(X + ca * 3.5f, Y + sa * 3.5f, lx, ly, Math.Max(lw, 0.9f), c);
        foreach (var (b, l) in new[] { (_b1, _l1), (_b2, _l2) })
        {
            float a2 = _heading + b; float ex = lx + MathF.Cos(a2) * l, ey = ly + MathF.Sin(a2) * l;
            p.Seg(lx, ly, ex, ey, Math.Max(lw, 0.55f), c);
            if (v.Px < 0.3f) p.Dot(ex, ey, 0.6f, c);
        }
        p.Seg(X - ca * 3.5f, Y - sa * 3.5f, X - ca * 9, Y - sa * 9, lw * 0.8f, Mul(c, 0.8f));
        float nk = _ph < 0.6f ? 0 : MathF.Sin((_ph - 0.6f) / 0.4f * PI);
        if (p.Tex != null) CortNeuron.EllipseSprite(p.Tex, X, Y, 4.2f + nk, 2.7f, _heading, Col);
        else p.FillEllipse(X, Y, 4.2f, 2.7f, _heading, Rgb(140, 134, 150, 0.5f));
    }
}

/// <summary>
/// Mikroglej zarodka: makrofagi mózgu (pochodzą z woreczka żółtkowego). W korze zarodka są rozproszone w strefie
/// komorowej, podkomorowej i pośredniej (Hattori i Miyata 2018); pochłaniają komórki apoptotyczne i część progenitorów
/// (Cunningham i in. 2013), a ich rozmieszczenie wpływa na rozwój połączeń (Squarzoni i in. 2014).
/// </summary>
public sealed class Microglia : Ent
{
    readonly float[] _pa = new float[4], _pl = new float[4], _pt = new float[4], _pk = new float[4];
    readonly float _seed; float _wA, _wT, _dig;
    public int Target = -1; public float Meals;
    static readonly Color Col = new(150 / 255f / SpriteAtlas.NeuBase, 140 / 255f / SpriteAtlas.NeuBase, 136 / 255f / SpriteAtlas.NeuBase, 0.6f);
    public override bool Swims => false;
    public override bool Rotates => false;
    public override float Radius => 4.2f;
    public Microglia(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.TissueCell; Layer = 2; Soft = 0.6f; Mass = 5; FlowK = 0; R = 4.6f;
        Name = "mikroglej"; Latin = "komórka mikrogleju (makrofag mózgu)";
        _seed = Rand(0, 99); _wA = Rand(0, TAU);
        for (int i = 0; i < _pa.Length; i++) { _pa[i] = i * TAU / _pa.Length + Rand(-0.5f, 0.5f); _pl[i] = Rand(2, 7); _pt[i] = Rand(2, 9); _pk[i] = Rand(-0.7f, 0.7f); }
    }
    public override void UpdatePrim() => SetCircle(3.6f);
    public override void Step(float dt, float gdt)
    {
        float h = dt / 3600;
        if (W.Preview) return;
        for (int i = 0; i < _pa.Length; i++)
        {
            if (MathF.Abs(_pt[i] - _pl[i]) < 0.5f) _pt[i] = Rand(1.5f, 10);
            _pl[i] += Math.Clamp(_pt[i] - _pl[i], -5 * h, 5 * h);
            _pa[i] += RandN() * 0.3f * MathF.Sqrt(h);
        }
        _dig = Math.Max(0, _dig - h / 2);
        var sc = W.Sc as ScNeuron;
        float vx = 0, vy = 0;
        if (sc != null && _dig <= 0 && sc.NearestDying(X, Y, 90, out int id, out float tx, out float ty))
        {
            // komórka apoptotyczna wystawia sygnały „zjedz mnie” (fosfatydyloseryna) – mikroglej kieruje ku niej wypustki i ciało
            Target = id; float d = Hypot(tx - X, ty - Y);
            if (d < R + 5) { sc.Eat(id, this); Meals++; _dig = 1; Target = -1; }
            else { vx = (tx - X) / d; vy = (ty - Y) / d; float v = 12f / 3600; vx *= v; vy *= v; }
        }
        else
        {
            Target = -1;
            _wT -= h; if (_wT <= 0) { _wT = Rand(1, 4); _wA += RandN() * 1.3f; }
            float v = 2.5f / 3600; vx = MathF.Cos(_wA) * v; vy = MathF.Sin(_wA) * v;
        }
        Vx = vx; Vy = vy;
    }
    public override void Draw(Painter p, View v)
    {
        if (W.Fluo) return;
        float lw = Math.Max(v.Px, 0.45f);
        var c = Rgb(86, 80, 76, 0.36f);
        for (int i = 0; i < _pa.Length; i++)
        {
            // wypustka zagięta w połowie, z krótką gałązką – mikroglej zarodka jest ameboidalny lub słabo rozgałęziony
            float a = _pa[i], l = _pl[i], x0 = X + MathF.Cos(a) * 3, y0 = Y + MathF.Sin(a) * 3;
            float xm = x0 + MathF.Cos(a) * l * 0.55f, ym = y0 + MathF.Sin(a) * l * 0.55f, a2 = a + _pk[i];
            float x1 = xm + MathF.Cos(a2) * l * 0.45f, y1 = ym + MathF.Sin(a2) * l * 0.45f;
            p.Seg(x0, y0, xm, ym, Math.Max(lw, 0.8f), c); p.Seg(xm, ym, x1, y1, Math.Max(lw, 0.55f), c);
            if (l > 5 && v.Px < 0.3f)
            {
                float a3 = a - _pk[i] * 1.3f;
                p.Seg(xm, ym, xm + MathF.Cos(a3) * l * 0.3f, ym + MathF.Sin(a3) * l * 0.3f, lw, Mul(c, 0.8f));
            }
        }
        // nieregularne ciało z ciemnymi inkluzjami (fagosomy)
        if (p.Tex != null) CortNeuron.EllipseSprite(p.Tex, X, Y, 4.3f, 3.4f, A, Col);
        else p.FillEllipse(X, Y, 4.3f, 3.4f, A, Rgb(146, 136, 132, 0.5f));
        if (Meals > 0 || _dig > 0)
            for (int k = 0; k < Math.Min(3, 1 + (int)Meals); k++)
                p.Dot(X + 1.6f * MathF.Cos(k * 2.1f + _seed), Y + 1.4f * MathF.Sin(k * 2.1f + _seed), 0.9f, Rgb(50, 42, 46, 0.65f));
    }
}

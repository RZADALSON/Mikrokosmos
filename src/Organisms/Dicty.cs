namespace Mikro;

/// <summary>
/// Fale cAMP. Centra agregacji (rozruszniki) pulsują co ~6–10 min; każda komórka, która odbierze impuls, wydziela
/// cAMP i przekazuje go dalej, więc fala biegnie ze stałą prędkością ~300 µm/min jako koncentryczne kręgi.
/// Po impulsie komórka jest przez kilka minut niepobudliwa (refrakcja), dlatego fale z dwóch centrów znoszą się
/// na granicy ich terytoriów. Stan w punkcie = czas od przejścia ostatniej fali z centrum, do którego należy.
/// </summary>
public sealed class CampField
{
    public readonly float Size;
    sealed class Center { public float X, Y, Next; public readonly List<float> Fires = new(); }
    readonly List<Center> _c = new();
    float _t;
    /// <summary>Okres rozruszników (s biologiczne): na początku ~10 min, skraca się do ~6 min.</summary>
    public float Period = 600;
    /// <summary>Czas pobudzenia i refrakcji (s biologiczne); prędkość fali 300 µm/min = 5 µm/s.</summary>
    public const float TE = 70, TR = 250, C = 5;
    readonly int _seed;
    public int Ticks;
    public CampField(float size) { Size = size; _seed = RandI(1, 99999); }
    public void AddPacemaker(float x, float y, float phase) => _c.Add(new Center { X = x, Y = y, Next = Period - phase });
    public void Update(float dt)
    {
        _t += dt; Ticks++;
        foreach (var c in _c)
            if (_t >= c.Next)
            {
                c.Fires.Add(c.Next); if (c.Fires.Count > 8) c.Fires.RemoveAt(0);
                c.Next += Period; Period = Math.Max(360, Period - 12);
            }
    }
    Center Owner(float x, float y)
    {
        Center best = null; float bd = float.MaxValue;
        foreach (var c in _c) { float d = Sq(c.X - x) + Sq(c.Y - y); if (d < bd) { bd = d; best = c; } }
        return best;
    }
    /// <summary>Czas (s) od przejścia ostatniej fali; duża liczba = spoczynek.</summary>
    public float PhaseAt(float x, float y)
    {
        var c = Owner(x, y); if (c == null) return 1e4f;
        // czoło fali lekko nieregularne – komórki nie są rozłożone idealnie równomiernie
        float d = Hypot(c.X - x, c.Y - y) + 18 * (Noise2(x / 140, y / 140, _seed) - 0.5f);
        float best = 1e4f;
        for (int k = c.Fires.Count - 1; k >= 0; k--) { float ph = _t - (c.Fires[k] + d / C); if (ph >= 0) { best = ph; break; } }
        return best;
    }
    /// <summary>Czoło fali (cAMP rośnie) i kierunek ku źródłu.</summary>
    public bool Front(float x, float y, out float gx, out float gy)
    {
        gx = gy = 0;
        if (PhaseAt(x, y) > TE * 0.7f) return false;
        var c = Owner(x, y); float l = Hypot(c.X - x, c.Y - y); if (l < 1) return false;
        gx = (c.X - x) / l; gy = (c.Y - y) / l; return true;
    }
    public int ExcitedCount()
    {
        int n = 0; const int S = 64;
        for (int j = 0; j < S; j++) for (int i = 0; i < S; i++) if (PhaseAt((i + 0.5f) * Size / S, (j + 0.5f) * Size / S) < TE) n++;
        return n;
    }
    /// <summary>Fale w ciemnym polu i kontraście fazowym: ruchome, wydłużone komórki rozpraszają światło inaczej niż spoczywające – widać pasma.</summary>
    public void Draw(Painter p, View v)
    {
        p.Identity();
        float cell = Math.Max(6, (v.X1 - v.X0) / 70);
        int nx = (int)((v.X1 - v.X0) / cell) + 3, ny = (int)((v.Y1 - v.Y0) / cell) + 3;
        float x0 = MathF.Floor(v.X0 / cell) * cell - cell, y0 = MathF.Floor(v.Y0 / cell) * cell - cell;
        p.Grid(x0, y0, cell, cell, nx, ny, (i, j) => Col(PhaseAt(x0 + i * cell, y0 + j * cell)));
    }
    static Color Col(float ph)
    {
        if (ph < TE) { float k = 1 - ph / TE; return new Color(0.28f, 0.3f, 0.22f, 0.22f * MathF.Sqrt(k)); }
        if (ph < TR) { float k = 1 - (ph - TE) / (TR - TE); return new Color(1f, 1f, 0.92f, 0.1f * k); }
        return new Color(1, 1, 1, 0);
    }
    public void DrawMap(Painter p)
    {
        const int S = 40; float cs = Size / S;
        for (int j = 0; j < S; j++)
            for (int i = 0; i < S; i++)
            {
                float ph = PhaseAt((i + 0.5f) * cs, (j + 0.5f) * cs);
                if (ph < TE) p.Rect(i * cs, j * cs, cs, cs, new Color(1f, 1f, 0.8f, 0.55f * (1 - ph / TE)));
            }
    }
}

/// <summary>
/// Ameba społeczna Dictyostelium discoideum (~10 µm). Faza wegetatywna: zjada bakterie (chemotaksja do kwasu foliowego),
/// dzieli się co kilka godzin. Głodująca komórka staje się zdolna do agregacji: przekazuje impulsy cAMP i idzie
/// ku ich źródłu – przy każdej fali ok. minutę, potem stoi. Wydłuża się i łączy w strumienie.
/// </summary>
public sealed class DictyCell : Ent
{
    /// <summary>Pełza/chodzi po podłożu – nie wytwarza pola przepływu pływaka.</summary>
    public override bool Swims => false;
    public float Eaten, CycleT, Energy = 0.5f, Pol;
    float _dir, _turnT, _seekT, _bx, _by; bool _hasB;
    readonly float _r, _seed; const int NP = 24; readonly float[] _rr = new float[NP];
    readonly Vector3[] _vac;
    public override float Radius => _r;
    public DictyCell(World w, float x, float y, float energy = 0.5f) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.Amoeba; Layer = 2; Soft = 0.6f; Name = "ameba społeczna"; Latin = "Dictyostelium discoideum";
        _r = Rand(4.2f, 5.6f); R = _r + 1; Mass = 1; _seed = Rand(0, 99); _dir = A; Energy = energy; CycleT = Rand(0, 8000);
        _vac = new Vector3[5];
        for (int i = 0; i < _vac.Length; i++) _vac[i] = new Vector3(Rand(0, TAU), Rand(0.2f, 0.6f), Rand(0.12f, 0.22f));
        // miękki, płynny obrys: ciasno upakowane ameby spłaszczają się o siebie (jak w strumieniach agregacji)
        Mem = new Membrane(NP) { MaxDef = _r * 0.6f, Stiff = 8, Damping = 0.7f, Fluid = 0.8f }; MemK = 5; MemS = 0.5f;
        Shape(0); Mem.Follow(x, y, A, 1, 1);
    }
    public override void UpdatePrim() => SetCircle(_r * 0.85f);
    /// <summary>Kształt: nibynóżki z przodu; w agregacji komórka jest wydłużona (głowa–ogon, strumienie).</summary>
    void Shape(float t)
    {
        for (int i = 0; i < NP; i++)
        {
            float th = i * TAU / NP + A, c = MathF.Cos(th - _dir);
            float el = 1 + 0.45f * Pol * c * c - 0.15f * Pol;
            _rr[i] = _r * el * (1 + 0.12f * Noise1(th * 2 + t + _seed) + 0.18f * (1 - Pol) * Math.Max(0, Noise1(th * 3 - t * 1.3f + _seed * 2)));
        }
        Mem.Radial(_rr);
    }
    public override bool Rotates => false;
    public override void Step(float dt, float gdt)
    {
        if (W.Sc is not ScDicty sc || W.Bact == null || sc.Camp == null) { Vx = Vy = 0; return; }
        CycleT += dt; _turnT -= dt; _seekT -= gdt;
        float v = 0, tgt = _dir; bool polarize = false;
        switch (sc.Phase)
        {
            case ScDicty.Ph.Veg:
                // chemotaksja do kwasu foliowego wydzielanego przez bakterie
                if (_seekT <= 0) { _seekT = 0.8f; _hasB = W.Bact.Densest(X, Y, 90, out _bx, out _by); }
                if (_hasB && Hypot(_bx - X, _by - Y) > 20) tgt = MathF.Atan2(_by - Y, _bx - X);
                else if (_turnT <= 0) { tgt = _dir + RandN() * 1.2f; _turnT = ExpRand(120); }
                v = 8f / 60;
                int n = W.Bact.Feed(X, Y, _r + 3, _r + 1.5f, 0, null);
                if (n > 0) { Eaten += n; Energy = Math.Min(1.5f, Energy + n * 0.012f); if (IsPlayer) W.AddStat("bact", n); }
                // podział: po zjedzeniu ~40 bakterii, nie częściej niż co ~4 h
                if (Eaten >= 40 && CycleT > 4 * 3600 && !W.Preview && W.Count(e => e is DictyCell) < 420) Divide();
                break;
            case ScDicty.Ph.Starve:
                Energy = Math.Max(0, Energy - dt / (3600 * 12));
                if (_turnT <= 0) { tgt = _dir + RandN() * 1.4f; _turnT = ExpRand(200); }
                v = 4f / 60;
                break;
            default:
                // agregacja: ruch tylko na czole fali cAMP, ku źródłu; między falami komórka prawie stoi
                if (sc.Camp.Front(X, Y, out float gx, out float gy)) { tgt = MathF.Atan2(gy, gx); v = 16f / 60; polarize = true; }
                else { v = 1.5f / 60; if (_turnT <= 0) { tgt = _dir + RandN() * 0.4f; _turnT = ExpRand(120); } }
                break;
        }
        if (IsPlayer && !W.Preview)
        {
            var I = W.Input; bool on = I.Mag > 0.05f;
            if (on) tgt = I.Ang;
            float basev = sc.Phase == ScDicty.Ph.Veg ? 10f / 60 : sc.Phase == ScDicty.Ph.Starve ? 5f / 60 : 6f / 60;
            // na czole fali i w kierunku źródła komórka pełznie najszybciej (~20 µm/min)
            if (sc.Phase == ScDicty.Ph.Agg && sc.Camp.Front(X, Y, out float fx, out float fy))
            {
                float al = MathF.Cos(tgt) * fx + MathF.Sin(tgt) * fy;
                basev += Math.Max(0, al) * 14f / 60; polarize = al > 0.3f;
            }
            v = on ? basev * I.Mag : 0;
        }
        _dir = Turn(_dir, tgt, 2.5f * gdt);
        Pol = Lerp(Pol, polarize ? 1 : 0.2f, Damp(polarize ? 2 : 0.5f, gdt));
        Vx = MathF.Cos(_dir) * v; Vy = MathF.Sin(_dir) * v;
        if (W.OnScreen(this)) Shape(W.T * 0.6f);
    }
    void Divide()
    {
        Eaten = 0; CycleT = 0; Energy *= 0.55f;
        float a = _dir + PI / 2;
        var d = W.Add(new DictyCell(W, X + MathF.Cos(a) * _r, Y + MathF.Sin(a) * _r, Energy));
        d.CycleT = 0; W.Born(this, d, true);
        X -= MathF.Cos(a) * _r * 0.5f; Y -= MathF.Sin(a) * _r * 0.5f;
        if (IsPlayer || Lineage) W.AddStat("pop", 1);
        if (IsPlayer) W.Log("Podział komórki – dwie ameby. Druga pełznie już sama (należy do Twojej linii).", "good");
    }
    public override void Draw(Painter p, View v)
    {
        int ns = Mem.Smooth(p.Shape, 2);
        var s = new ReadOnlySpan<Vector2>(p.Shape, 0, ns);
        bool detail = _r / v.Px > 4;
        if (detail) p.SoftStroke(s, true, 1.6f, Rgb(255, 255, 240, 0.3f));
        p.FillFan(s, X, Y, Rgb(150, 155, 140, 0.5f));
        p.Stroke(s, true, Math.Max(v.Px, 0.12f), Rgb(45, 50, 40, 0.65f));
        if (!detail) return;
        p.Local(X, Y, _dir); p.Warp(Mem);
        p.FillCircle(-_r * 0.15f, 0, _r * 0.3f, Rgb(185, 188, 175, 0.5f)); // jądro
        p.FillCircle(-_r * 0.15f, 0, _r * 0.1f, Rgb(80, 85, 75, 0.6f));    // jąderko
        float full = Math.Min(1, Energy);
        for (int k = 0; k < _vac.Length; k++)
        {
            var q = _vac[k]; float r = _r * q.Y;
            if (k < 2) p.Circle(MathF.Cos(q.X) * r, MathF.Sin(q.X) * r, _r * q.Z, 0.1f, Rgb(235, 240, 225, 0.6f)); // wodniczki
            else if (full > k * 0.25f) p.FillCircle(MathF.Cos(q.X) * r, MathF.Sin(q.X) * r, _r * q.Z, Rgb(60, 62, 55, 0.5f)); // wodniczki pokarmowe z bakteriami
        }
    }
}

/// <summary>Agregat (kopczyk) – tu zbiegają się strumienie komórek. Z niego powstaje ślimak.</summary>
public sealed class Mound : Ent
{
    public int N; public float Age, JoinT; public bool HasPlayer;
    public float Rm => 14 + MathF.Sqrt(N) * 3.2f;
    public override float Radius => Rm;
    public Mound(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.None; Layer = 1; Fixed = true; Solid = false; FlowK = 0; Name = "agregat (kopczyk)"; Latin = "Dictyostelium discoideum"; R = 30;
    }
    public override void Step(float dt, float gdt)
    {
        Age += gdt; R = Rm + 20; if (HasPlayer) JoinT += gdt;
        foreach (var e in W.Ents)
        {
            if (e is not DictyCell c || !c.Alive) continue;
            if (Hypot(c.X - X, c.Y - Y) > Rm + 2) continue;
            N++; c.Alive = false;
            if (c.IsPlayer) { HasPlayer = true; W.Player = null; W.SetPlayer(this); W.AddStat("joined", 1); W.Log("Dołączasz do agregatu! Tysiące komórek (w grze setki) tworzą kopczyk – wkrótce powstanie ślimak.", "good"); }
            else if (c.Lineage) W.AddStat("joinedLine", 1);
        }
    }
    public override bool CanControl() => false;
    public override void Draw(Painter p, View v)
    {
        float r = Rm;
        p.Glow(X, Y, r * 1.8f, Rgb(255, 255, 235, 0.18f));
        p.FillCircleGrad(X, Y, r, Rgb(215, 215, 195, 0.75f), Rgb(140, 142, 125, 0.6f));
        p.Circle(X, Y, r, Math.Max(v.Px, 0.4f), Rgb(60, 62, 50, 0.6f));
        // ciasno upakowane komórki na powierzchni kopczyka
        int n = Math.Min(160, 20 + N);
        for (int i = 0; i < n; i++) { float a = i * 2.399f, q = MathF.Sqrt((i + 0.5f) / n) * r * 0.95f; p.Dot(X + MathF.Cos(a) * q, Y + MathF.Sin(a) * q, 1.6f, Rgb(95, 98, 85, 0.35f)); }
        if (Age > 8) p.FillCircle(X, Y, r * 0.22f, Rgb(235, 235, 215, 0.6f)); // wyrastający czubek (tip)
    }
}

//// <summary>
/// Ślimak (pseudoplazmodium): ~10⁵ komórek w osłonce śluzu (celuloza + glikoproteiny), płynne ciało bez szkieletu.
/// Prowadzi go czubek (przód, ~20% komórek przedtrzonkowych); reszta ciała sunie po torze czubka jak pociąg po szynach
/// (zakręty „przepływają” wzdłuż ciała), zostawiając ślad śluzu. Ruch ~1–2 mm/h ku światłu i ciepłu (Bonner 2009).
/// Obrys to miękka membrana: wgniata się o przeszkody, a ubytek objętości wybrzusza ciało obok (nieściśliwe wnętrze).
/// </summary>
public sealed class Slug : Ent
{
    /// <summary>Pełza po podłożu – nie wytwarza pola przepływu pływaka.</summary>
    public override bool Swims => false;
    public int N; public float L, Wd; public Steer Sw; float _head;
    const int K = 18;
    /// <summary>Tor czubka (najnowszy na końcu) – po nim sunie reszta ciała.</summary>
    readonly List<Vector2> _path = new();
    readonly Vector2[] _c = new Vector2[K];
    readonly float[] _w = new float[K];
    readonly List<Vector2> _trail = new();
    public override float Length => L;
    public override float Radius => Wd / 2;
    public override bool Rotates => false;
    public Slug(World w, float x, float y, float a, int n) : base(w, x, y, a)
    {
        N = n; Kind = Kind.None; Layer = 2; Soft = 0.5f; Name = "ślimak (pseudoplazmodium)"; Latin = "Dictyostelium discoideum";
        L = Math.Clamp(60 + n * 1.3f, 80, 520); Wd = L * 0.16f; R = L + 10; Mass = n; _head = a;
        Mem = new Membrane(2 * K) { MaxDef = Wd * 0.35f, Stiff = 5, Damping = 0.8f, Fluid = 0.9f }; MemK = 2; MemS = 0.6f;
        // tor startowy: ciało leży prosto za czubkiem
        for (int k = 12; k >= 0; k--) _path.Add(new Vector2(x - MathF.Cos(a) * L * k / 12, y - MathF.Sin(a) * L * k / 12));
        Centerline(); Outline(); Mem.Follow(X, Y, A, 1, 1);
    }
    /// <summary>Profil szerokości: stożkowy czubek, pełny środek, zaokrąglony tył; lekka fala perystaltyczna biegnąca do tyłu.</summary>
    float Width(float t)
    {
        float tip = Smooth(Math.Min(1, t / 0.22f)), tail = MathF.Sqrt(Math.Max(0, 1 - Sq(Math.Max(0, t - 0.8f) / 0.2f)));
        return Wd / 2 * (0.35f + 0.65f * tip) * Math.Max(0.25f, tail) * (1 + 0.04f * MathF.Sin(t * 14 - (W?.T ?? 0) * 1.5f));
    }
    /// <summary>Oś ciała: punkty wzdłuż toru czubka, co L/(K−1) (od czubka do tyłu).</summary>
    void Centerline()
    {
        float step = L / (K - 1), need = 0; int k = 0;
        _c[k++] = new Vector2(X, Y);
        var prev = new Vector2(X, Y);
        for (int i = _path.Count - 1; i >= 0 && k < K; i--)
        {
            var q = _path[i]; float d = (q - prev).Length();
            while (d > 1e-4f && k < K && need + d >= step)
            {
                float u = (step - need) / d; var pt = prev + (q - prev) * u;
                _c[k++] = pt; d = (q - pt).Length(); prev = pt; need = 0;
            }
            need += d; prev = q;
        }
        // tor za krótki – dopełnij prosto w tył
        var dir = k >= 2 ? (_c[k - 1] - _c[k - 2]).Normalized() : new Vector2(-MathF.Cos(A), -MathF.Sin(A));
        for (; k < K; k++) _c[k] = _c[k - 1] + dir * step;
        for (int i = 0; i < K; i++) _w[i] = Width((float)i / (K - 1));
    }
    /// <summary>Obrys spoczynkowy (lewa strona od czubka do tyłu, prawa z powrotem).</summary>
    void Outline()
    {
        for (int i = 0; i < K; i++)
        {
            var t = i == 0 ? _c[0] - _c[1] : i == K - 1 ? _c[K - 2] - _c[K - 1] : _c[i - 1] - _c[i + 1];
            t = t.Normalized(); var n = new Vector2(-t.Y, t.X);
            var l = _c[i] + n * _w[i]; var r = _c[i] - n * _w[i];
            // membrana obraca obrys o kąt A – zapisujemy go w układzie lokalnym (obrócony o −A)
            ToLocal(l.X, l.Y, out Mem.Rx[i], out Mem.Ry[i]);
            ToLocal(r.X, r.Y, out Mem.Rx[2 * K - 1 - i], out Mem.Ry[2 * K - 1 - i]);
        }
        Mem.IsRadial = false;
    }
    /// <summary>Kolizje wzdłuż wygiętego ciała (kapsuły między punktami osi).</summary>
    public override void UpdatePrim()
    {
        UseParts(K - 1);
        for (int i = 0; i < K - 1; i++)
        {
            var p = Parts[i]; p.Cap = true; p.Ax = _c[i].X; p.Ay = _c[i].Y; p.Bx = _c[i + 1].X; p.By = _c[i + 1].Y;
            p.R = Math.Max(0.5f, (_w[i] + _w[i + 1]) * 0.45f); p.X = (p.Ax + p.Bx) / 2; p.Y = (p.Ay + p.By) / 2;
        }
        Prim.Cap = false; Prim.X = X; Prim.Y = Y; Prim.R = Wd * 0.3f;
    }
    public override bool CanControl() => true;
    public override void Step(float dt, float gdt)
    {
        if (W.Sc is not ScDicty) return;
        // fototaksja: ślimak skręca ku światłu (w grze – górna krawędź = powierzchnia gleby)
        float light = -PI / 2, tgt = light;
        if (IsPlayer && !W.Preview) { var I = W.Input; if (I.Mag > 0.05f) tgt = I.Ang; }
        float al = MathF.Cos(tgt - light);
        _head = tgt;
        // ~1–2 mm/h; szybciej, gdy w stronę światła
        float v = (IsPlayer && W.Input.Mag <= 0.05f ? 0.3f : 1) * (0.28f + 0.14f * Math.Max(0, al)) * (N > 40 ? 1 : 0.7f);
        Sw.Update(this, dt, _head, v, 0.004f, 0.01f, 0.01f);
        if (_trail.Count == 0 || Hypot(_trail[^1].X - _c[K - 1].X, _trail[^1].Y - _c[K - 1].Y) > 8) { _trail.Add(_c[K - 1]); if (_trail.Count > 400) _trail.RemoveAt(0); }
    }
    public override void Post(float dt, float gdt)
    {
        // czubek zapisuje tor; ciało (oś i obrys) podąża za nim
        if (_path.Count == 0 || Hypot(_path[^1].X - X, _path[^1].Y - Y) > L / 60) { _path.Add(new Vector2(X, Y)); if (_path.Count > 200) _path.RemoveAt(0); }
        Centerline(); Outline();
    }
    public override void Draw(Painter p, View v)
    {
        // ślad śluzu (osłonka zostawiana za ślimakiem)
        if (_trail.Count > 1)
        {
            var b = p.Buf2; int n = Math.Min(_trail.Count, b.Length);
            for (int i = 0; i < n; i++) b[i] = _trail[_trail.Count - n + i];
            p.Stroke(new ReadOnlySpan<Vector2>(b, 0, n), false, Wd * 0.8f, Rgb(225, 225, 205, 0.18f), true);
        }
        int ns = Mem.Smooth(p.Shape, 3);
        var s = new ReadOnlySpan<Vector2>(p.Shape, 0, ns);
        float hw = Wd / 2;
        p.SoftStroke(s, true, hw * 0.5f, Rgb(255, 255, 240, 0.25f));
        // wypełnienie paskiem czworokątów wzdłuż ciała (ciało bywa wklęsłe w zakrętach – wachlarz ze środka by się wylewał)
        Span<Vector2> q = stackalloc Vector2[4];
        var mp = Mem;
        for (int i = 0; i < K - 1; i++)
        {
            int l0 = i, l1 = i + 1, r0 = 2 * K - 1 - i, r1 = 2 * K - 2 - i;
            q[0] = new Vector2(mp.Px[l0], mp.Py[l0]); q[1] = new Vector2(mp.Px[l1], mp.Py[l1]);
            q[2] = new Vector2(mp.Px[r1], mp.Py[r1]); q[3] = new Vector2(mp.Px[r0], mp.Py[r0]);
            float t = (i + 0.5f) / (K - 1), cx = (q[0].X + q[1].X + q[2].X + q[3].X) / 4, cy = (q[0].Y + q[1].Y + q[2].Y + q[3].Y) / 4;
            p.FillFan(q, cx, cy, Rgb((int)Lerp(178, 140, t), (int)Lerp(180, 142, t), (int)Lerp(160, 122, t), 0.75f), false);
        }
        p.Stroke(s, true, Math.Max(v.Px, hw * 0.05f), Rgb(70, 70, 55, 0.65f));
        // strefa przedtrzonkowa (przód, ~20%) – jaśniejsza, uniesiony czubek
        int kb = (int)(K * 0.2f);
        var tb = (_c[kb - 1] - _c[kb + 1]).Normalized(); var nb = new Vector2(-tb.Y, tb.X);
        p.Line(_c[kb].X + nb.X * _w[kb] * 0.8f, _c[kb].Y + nb.Y * _w[kb] * 0.8f, _c[kb].X - nb.X * _w[kb] * 0.8f, _c[kb].Y - nb.Y * _w[kb] * 0.8f, Math.Max(v.Px, hw * 0.04f), Rgb(110, 110, 90, 0.35f));
        for (int i = 0; i < kb; i++) p.FillCircle(_c[i].X, _c[i].Y, _w[i] * 0.6f, Rgb(235, 235, 220, 0.12f));
        // komórki w ciele – rozłożone wzdłuż wygiętej osi
        int nc = Math.Min(220, N);
        for (int i = 0; i < nc; i++)
        {
            float t = 0.05f + 0.9f * ((i * 0.618f) % 1), f = t * (K - 1); int k = Math.Min(K - 2, (int)f); float u = f - k;
            var c = _c[k] + (_c[k + 1] - _c[k]) * u; var d = (_c[k + 1] - _c[k]).Normalized();
            float side = ((i * 0.377f) % 1 * 2 - 1) * (_w[k] + (_w[k + 1] - _w[k]) * u) * 0.85f;
            p.Dot(c.X - d.Y * side, c.Y + d.X * side, 1.3f, Rgb(100, 100, 85, 0.3f));
        }
    }
}

// <summary>Owocnik: trzonek z martwych komórek (celuloza) unosi kulkę zarodników (sorus). Widok skośny, jak pod lupą stereoskopową.</summary>
public sealed class FruitBody : Ent
{
    public float Prog; readonly int _n; readonly float _h;
    public FruitBody(World w, float x, float y, int n) : base(w, x, y, 0)
    {
        Kind = Kind.None; Layer = 3; Fixed = true; Solid = false; FlowK = 0; _n = n; _h = Math.Clamp(80 + n * 1.4f, 100, 700); R = _h; Name = "owocnik (sorokarp)"; Latin = "Dictyostelium discoideum";
    }
    public override void Step(float dt, float gdt) { Prog = Math.Min(1, Prog + gdt / 18); }
    public override bool CanControl() => false;
    public override void Draw(Painter p, View v)
    {
        float up = Smooth(Clamp(Prog * 1.4f - 0.2f, 0, 1)), h = _h * up, sr = 8 + MathF.Sqrt(_n) * 2.2f * Math.Min(1, Prog * 1.5f);
        p.FillEllipse(X, Y, sr * 1.2f, sr * 0.45f, 0, Rgb(170, 168, 150, 0.6f)); // tarczka podstawowa
        p.FillEllipse(X + h * 0.35f, Y + 4, sr * 1.3f, sr * 0.4f, 0, Rgb(40, 40, 30, 0.18f)); // cień
        p.Line(X, Y, X, Y - h, Math.Max(v.Px, sr * 0.14f), Rgb(215, 210, 185, 0.8f), true);
        p.Line(X, Y, X, Y - h, Math.Max(v.Px, sr * 0.05f), Rgb(120, 115, 90, 0.6f), true);
        float sy = Y - h, ripe = Clamp((Prog - 0.6f) / 0.4f, 0, 1);
        p.Glow(X, sy, sr * 1.8f, Rgb(255, 250, 220, 0.2f));
        p.FillCircleGrad(X - sr * 0.2f, sy - sr * 0.2f, sr, Rgb((int)Lerp(220, 250, ripe), (int)Lerp(220, 235, ripe), (int)Lerp(205, 170, ripe), 0.85f), Rgb((int)Lerp(150, 200, ripe), (int)Lerp(150, 175, ripe), (int)Lerp(135, 90, ripe), 0.85f));
        p.Circle(X, sy, sr, Math.Max(v.Px, 0.5f), Rgb(90, 85, 60, 0.6f));
    }
}

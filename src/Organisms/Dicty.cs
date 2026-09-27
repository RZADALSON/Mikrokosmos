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
    public float Eaten, CycleT, Energy = 0.5f, Pol;
    float _dir, _turnT, _seekT, _bx, _by; bool _hasB;
    readonly float _r, _seed; const int NP = 16; readonly float[] _rr = new float[NP];
    readonly Vector3[] _vac;
    public override float Radius => _r;
    public DictyCell(World w, float x, float y, float energy = 0.5f) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.Amoeba; Layer = 2; Soft = 0.6f; Name = "ameba społeczna"; Latin = "Dictyostelium discoideum";
        _r = Rand(4.2f, 5.6f); R = _r + 1; Mass = 1; _seed = Rand(0, 99); _dir = A; Energy = energy; CycleT = Rand(0, 8000);
        _vac = new Vector3[5];
        for (int i = 0; i < _vac.Length; i++) _vac[i] = new Vector3(Rand(0, TAU), Rand(0.2f, 0.6f), Rand(0.12f, 0.22f));
    }
    public override void UpdatePrim() => SetCircle(_r * 0.85f);
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
    }
    void Divide()
    {
        Eaten = 0; CycleT = 0; Energy *= 0.55f;
        float a = _dir + PI / 2;
        var d = W.Add(new DictyCell(W, X + MathF.Cos(a) * _r, Y + MathF.Sin(a) * _r, Energy));
        d.CycleT = 0; d.Lineage = Lineage || IsPlayer;
        X -= MathF.Cos(a) * _r * 0.5f; Y -= MathF.Sin(a) * _r * 0.5f;
        if (IsPlayer || Lineage) W.AddStat("pop", 1);
        if (IsPlayer) W.Log("Podział komórki – dwie ameby. Druga pełznie już sama (należy do Twojej linii).", "good");
    }
    public override void Draw(Painter p, View v)
    {
        float t = W.T * 0.6f;
        // kształt: nibynóżki z przodu; w agregacji komórka jest wydłużona (głowa–ogon, strumienie)
        for (int i = 0; i < NP; i++)
        {
            float th = i * TAU / NP, c = MathF.Cos(th - _dir);
            float el = 1 + 0.45f * Pol * c * c - 0.15f * Pol;
            _rr[i] = _r * el * (1 + 0.12f * Noise1(th * 2 + t + _seed) + 0.18f * (1 - Pol) * Math.Max(0, Noise1(th * 3 - t * 1.3f + _seed * 2)));
        }
        var sh = p.Buf2;
        for (int i = 0; i < NP; i++) { float th = i * TAU / NP; sh[i] = new Vector2(X + MathF.Cos(th) * _rr[i], Y + MathF.Sin(th) * _rr[i]); }
        var s = new ReadOnlySpan<Vector2>(sh, 0, NP);
        bool detail = _r / v.Px > 4;
        if (detail) p.SoftStroke(s, true, 1.6f, Rgb(255, 255, 240, 0.3f));
        p.FillFan(s, X, Y, Rgb(150, 155, 140, 0.5f));
        p.Stroke(s, true, Math.Max(v.Px, 0.12f), Rgb(45, 50, 40, 0.65f));
        if (!detail) return;
        p.Local(X, Y, _dir);
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

/// <summary>
/// Ślimak (pseudoplazmodium): wielokomórkowy "organizm" w osłonce śluzu, pełznie ku światłu i ciepłu,
/// zostawiając ślad śluzu. Przód (~20%) to komórki przedtrzonkowe, reszta – przedzarodnikowe.
/// </summary>
public sealed class Slug : Ent
{
    public int N; public float L, Wd; public Steer Sw; float _head;
    readonly List<Vector2> _trail = new();
    public override float Length => L;
    public override float Radius => Wd / 2;
    public Slug(World w, float x, float y, float a, int n) : base(w, x, y, a)
    {
        N = n; Kind = Kind.None; Layer = 2; Soft = 0.5f; Name = "ślimak (pseudoplazmodium)"; Latin = "Dictyostelium discoideum";
        L = Math.Clamp(60 + n * 1.3f, 80, 520); Wd = L * 0.16f; R = L / 2 + 10; Mass = n; _head = a;
    }
    public override void UpdatePrim() => AxisParts(3, L / 2, Wd / 2, t => MathF.Sqrt(Math.Max(0, 1 - t * t)) * (t > 0 ? 1 - 0.3f * t : 1), 0);
    public override bool CanControl() => true;
    public override void Step(float dt, float gdt)
    {
        if (W.Sc is not ScDicty sc) return;
        // fototaksja: ślimak skręca ku światłu (w grze – górna krawędź = powierzchnia gleby)
        float light = -PI / 2, tgt = light;
        if (IsPlayer && !W.Preview) { var I = W.Input; if (I.Mag > 0.05f) tgt = I.Ang; }
        float al = MathF.Cos(tgt - light);
        _head = tgt;
        // ~1–2 mm/h; szybciej, gdy w stronę światła
        float v = (IsPlayer && W.Input.Mag <= 0.05f ? 0.3f : 1) * (0.28f + 0.14f * Math.Max(0, al)) * (N > 40 ? 1 : 0.7f);
        Sw.Update(this, dt, _head, v, 0.004f, 0.01f, 0.01f);
        if (_trail.Count == 0 || Hypot(_trail[^1].X - X, _trail[^1].Y - Y) > 8) { ToWorld(-L / 2, 0, out float tx, out float ty); _trail.Add(new Vector2(tx, ty)); if (_trail.Count > 400) _trail.RemoveAt(0); }
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
        p.Local(X, Y, A);
        float hl = L / 2, hw = Wd / 2;
        const int M = 24; var sh = p.Buf2;
        for (int k = 0; k <= M; k++) { float t = -1 + 2f * k / M, w = MathF.Sqrt(Math.Max(0, 1 - t * t)) * (t > 0 ? 1 - 0.3f * t : 1) * hw; sh[k] = new Vector2(t * hl, w); sh[2 * M + 1 - k] = new Vector2(t * hl, -w); }
        var s = new ReadOnlySpan<Vector2>(sh, 0, 2 * M + 2);
        p.SoftStroke(s, true, hw * 0.5f, Rgb(255, 255, 240, 0.25f));
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(205, 205, 185, 0.7f), Rgb(150, 150, 130, 0.7f) };
        p.FillRadial(s, 0, 0, ts, cs);
        p.Stroke(s, true, Math.Max(v.Px, hw * 0.05f), Rgb(70, 70, 55, 0.65f));
        // strefa przedtrzonkowa (przód, ~20%) – jaśniejsza; granica z przedzarodnikową
        p.Line(hl * 0.6f, -hw * 0.75f, hl * 0.6f, hw * 0.75f, Math.Max(v.Px, hw * 0.04f), Rgb(110, 110, 90, 0.35f));
        p.FillEllipse(hl * 0.8f, 0, hl * 0.2f, hw * 0.55f, 0, Rgb(235, 235, 220, 0.35f));
        int nc = Math.Min(220, N);
        for (int i = 0; i < nc; i++) { float t = -0.9f + 1.8f * ((i * 0.618f) % 1), w = MathF.Sqrt(Math.Max(0, 1 - t * t)) * hw * 0.85f; p.Dot(t * hl, ((i * 0.377f) % 1 * 2 - 1) * w, 1.3f, Rgb(100, 100, 85, 0.3f)); }
    }
}

/// <summary>Owocnik: trzonek z martwych komórek (celuloza) unosi kulkę zarodników (sorus). Widok skośny, jak pod lupą stereoskopową.</summary>
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

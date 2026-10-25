namespace Mikro;

/// <summary>
/// Glony Chlorella na listkach mchu – nieruchome kuliste komórki 3–10 µm (pole cząstek, jak chmury bakterii).
/// Niesporczak przebija je sztyletami i wysysa zawartość: zostaje pusta, przezroczysta ścianka.
/// </summary>
public sealed class AlgaField
{
    public struct Alga { public float X, Y, R, Empty; }
    readonly Dictionary<long, List<Alga>> _by = new();
    readonly float _cs;
    public AlgaField(float chunk) { _cs = chunk; }
    static long K(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
    List<Alga> L(float x, float y)
    {
        long k = K(FloorDiv(x, _cs), FloorDiv(y, _cs));
        if (!_by.TryGetValue(k, out var l)) { l = new List<Alga>(); _by[k] = l; }
        return l;
    }
    /// <summary>Kożuszek glonów: skupisko n komórek wokół punktu.</summary>
    public void Patch(float cx, float cy, float r, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a = Rand(0, TAU), d = MathF.Sqrt(R01()) * r;
            float x = cx + MathF.Cos(a) * d, y = cy + MathF.Sin(a) * d;
            L(x, y).Add(new Alga { X = x, Y = y, R = Rand(1.8f, 4.5f) });
        }
    }
    /// <summary>Przebija najbliższą pełną komórkę w zasięgu; zwraca jej objętość względną (0 = brak).</summary>
    public float Pierce(float x, float y, float reach)
    {
        int cx = FloorDiv(x, _cs), cy = FloorDiv(y, _cs);
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                if (!_by.TryGetValue(K(cx + i, cy + j), out var l)) continue;
                for (int q = 0; q < l.Count; q++)
                {
                    var a = l[q];
                    if (a.Empty > 0 || Sq(a.X - x) + Sq(a.Y - y) > Sq(reach + a.R)) continue;
                    a.Empty = 0.001f; l[q] = a;
                    return a.R * a.R / 10f;
                }
            }
        return 0;
    }
    /// <summary>Najbliższa pełna komórka (cel dla AI i wskaźnika pokarmu).</summary>
    public bool Nearest(float x, float y, float r, out float bx, out float by)
    {
        bx = by = 0; float bd = r * r; bool found = false;
        int cx = FloorDiv(x, _cs), cy = FloorDiv(y, _cs), rr = (int)MathF.Ceiling(r / _cs);
        for (int j = -rr; j <= rr; j++)
            for (int i = -rr; i <= rr; i++)
            {
                if (!_by.TryGetValue(K(cx + i, cy + j), out var l)) continue;
                foreach (var a in l) { if (a.Empty > 0) continue; float d = Sq(a.X - x) + Sq(a.Y - y); if (d < bd) { bd = d; bx = a.X; by = a.Y; found = true; } }
            }
        return found;
    }
    /// <summary>Puste ścianki powoli się rozpadają, a glony dzielą się (autospory) i odrastają.</summary>
    public void Update(float gdt, float cx, float cy, float r)
    {
        int x0 = FloorDiv(cx - r, _cs), x1 = FloorDiv(cx + r, _cs), y0 = FloorDiv(cy - r, _cs), y1 = FloorDiv(cy + r, _cs);
        for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                if (!_by.TryGetValue(K(i, j), out var l)) continue;
                for (int q = 0; q < l.Count; q++) { var a = l[q]; if (a.Empty > 0) { a.Empty += gdt / 40; if (a.Empty >= 1) { a.Empty = 0; a.R = Rand(1.8f, 3); } l[q] = a; } }
            }
    }
    public void Draw(Painter p, View v)
    {
        p.Identity();
        int x0 = FloorDiv(v.X0, _cs), x1 = FloorDiv(v.X1, _cs), y0 = FloorDiv(v.Y0, _cs), y1 = FloorDiv(v.Y1, _cs);
        bool detail = 3 / v.Px > 5;
        for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                if (!_by.TryGetValue(K(i, j), out var l)) continue;
                foreach (var a in l)
                {
                    if (a.X < v.X0 - 5 || a.X > v.X1 + 5 || a.Y < v.Y0 - 5 || a.Y > v.Y1 + 5) continue;
                    if (a.Empty > 0)
                    {
                        // pusta ścianka komórkowa po wyssaniu
                        if (detail) p.Circle(a.X, a.Y, a.R, Math.Max(v.Px, 0.25f), Rgb(120, 140, 100, 0.4f * (1 - a.Empty)));
                        continue;
                    }
                    p.Dot(a.X, a.Y, a.R, Rgb(55, 135, 40, 0.8f));
                    if (detail) p.Dot(a.X - a.R * 0.3f, a.Y - a.R * 0.3f, a.R * 0.35f, Rgb(200, 240, 160, 0.5f));
                }
            }
    }
    public void DrawMap(Painter p, float cx, float cy, float r)
    {
        int x0 = FloorDiv(cx - r, _cs), x1 = FloorDiv(cx + r, _cs), y0 = FloorDiv(cy - r, _cs), y1 = FloorDiv(cy + r, _cs);
        for (int j = y0; j <= y1; j++)
            for (int i = x0; i <= x1; i++)
            {
                if (!_by.TryGetValue(K(i, j), out var l)) continue;
                for (int q = 0; q < l.Count; q += 6) if (l[q].Empty <= 0) p.Rect(l[q].X - 15, l[q].Y - 15, 30, 30, Rgb(90, 200, 70, 0.35f));
            }
    }
}

/// <summary>
/// Listek mchu – jedna warstwa wydłużonych komórek z chloroplastami, z żeberkiem (costa) pośrodku.
/// To podłoże, a nie ściana: blaszka ma grubość jednej komórki (~10–15 µm), a niesporczak, wrotek czy nicień chodzą po
/// jej powierzchni w błonce wody i przechodzą przez krawędź na drugą stronę albo na sąsiedni listek (poduszka mchu jest
/// trójwymiarowa – listki leżą na sobie warstwami). W rzucie 2D zwierzęta są więc rysowane na listku i nie zderzają się z nim.
/// </summary>
public sealed class MossLeaf : Ent
{
    readonly float _l, _w; readonly float[] _cellX; readonly Vector3[] _chl;
    public override float Length => _l;
    public override float Radius => _w / 2;
    static float Prof(float t) => MathF.Pow(Math.Max(0, 1 - t * t), 0.7f) * (t > 0 ? 1 - 0.45f * t : 1);
    public MossLeaf(World w, float x, float y, float a, float len) : base(w, x, y, a)
    {
        Kind = Kind.None; Layer = 1; Fixed = true; Solid = false; FlowK = 0; Mass = 1e6f; Name = "listek mchu"; Latin = "Bryophyta (np. Brachythecium)";
        _l = len; _w = len * Rand(0.2f, 0.28f); R = _l / 2 + 10;
        // komórki blaszki: kolumny wydłużonych komórek (≈ 10–15 × 50–80 µm), przesunięte względem siebie
        _cellX = new float[(int)(_l / 60) + 2];
        for (int i = 0; i < _cellX.Length; i++) _cellX[i] = Rand(0, 1);
        _chl = new Vector3[(int)(_l * _w / 650)];
        for (int i = 0; i < _chl.Length; i++) { float t = Rand(-0.92f, 0.92f); _chl[i] = new Vector3(t, Rand(-0.9f, 0.9f) * Prof(t), Rand(1.8f, 2.8f)); }
    }
    public override void UpdatePrim() => AxisParts(5, _l / 2, _w / 2, Prof, 0);
    public override bool StaticDraw => true;
    public override void Draw(Painter p, View v)
    {
        if (!v.Vis(X, Y, _l * 0.5f + 8)) return; // liść jest długi – margines silnika (1,6 R) rysowałby dziesiątki liści poza kadrem
        p.Local(X, Y, A);
        float hl = _l / 2, hw = _w / 2;
        var sh = p.Buf2; const int M = 30;
        for (int k = 0; k <= M; k++) { float t = -1 + 2f * k / M; sh[k] = new Vector2(t * hl, Prof(t) * hw); sh[2 * M + 1 - k] = new Vector2(t * hl, -Prof(t) * hw); }
        var s = new ReadOnlySpan<Vector2>(sh, 0, 2 * M + 2);
        p.SoftStroke(s, true, 6, Rgb(255, 255, 235, 0.22f));
        Span<float> ts = stackalloc float[] { 0f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(130, 170, 90, 0.42f), Rgb(95, 140, 60, 0.5f) };
        p.FillRadial(s, 0, 0, ts, cs);
        p.Stroke(s, true, Math.Max(v.Px, 0.8f), Rgb(55, 80, 35, 0.6f));
        // żeberko
        p.Line(-hl * 0.95f, 0, hl * 0.85f, 0, hw * 0.12f, Rgb(80, 110, 50, 0.45f), true);
        if (6 / v.Px < 3) return;
        bool walls = 60 / v.Px > 16;
        // ściany komórek: podłużne rzędy + poprzeczne ścianki
        var wc = Rgb(60, 85, 40, 0.35f); float lw = Math.Max(v.Px * 0.7f, 0.4f);
        for (int r = -4; r <= 4; r++)
        {
            if (r == 0) continue;
            float f = r / 4.5f;
            for (int k = 0; k < 16; k++) { float t0 = -0.92f + 1.84f * k / 16, t1 = t0 + 1.84f / 16; p.Seg(t0 * hl, Prof(t0) * hw * f, t1 * hl, Prof(t1) * hw * f, lw, wc); }
            if (!walls) continue;
            float off = _cellX[(r + 4) % _cellX.Length];
            for (float x = -hl * 0.9f + off * 60; x < hl * 0.9f; x += 60)
            {
                float t = x / hl, y0 = Prof(t) * hw * (r - 0.5f * Math.Sign(r)) / 4.5f, y1 = Prof(t) * hw * f;
                p.Seg(x, y0, x, y1, lw, wc);
            }
        }
        // chloroplasty: tylko w kadrze; przy małym powiększeniu co n-ty, większy (ta sama "ziarnista" zieleń, mniej wierzchołków)
        float px = 2.3f / v.Px; int step = px > 5 ? 1 : px > 2.5f ? 2 : 4; float kk = MathF.Sqrt(step);
        float ca = MathF.Cos(A), sa = MathF.Sin(A), m = 4;
        for (int i = 0; i < _chl.Length; i += step)
        {
            var c = _chl[i]; float lx = c.X * hl, ly = c.Y * hw, wx = X + lx * ca - ly * sa, wy = Y + lx * sa + ly * ca;
            if (wx < v.X0 - m || wx > v.X1 + m || wy < v.Y0 - m || wy > v.Y1 + m) continue;
            p.Dot(lx, ly, c.Z * kk * 0.85f, Rgb(60, 130, 40, 0.5f));
        }
    }
}

/// <summary>Wylinka niesporczaka z jajami (Hypsibius składa jaja do zrzuconego oskórka). Z jaj wylęgają się młode.</summary>
public sealed class Exuvia : Ent
{
    readonly float _l; readonly int _n; float _t; readonly Vector2[] _eggs; readonly bool _mine;
    readonly int _pGen; readonly float _pBorn;
    /// <summary>Jaja Twojej linii (żywe potomstwo, choć jeszcze niewyklute).</summary>
    public int MineEggs => _mine && Alive ? _n : 0;
    public Exuvia(World w, float x, float y, float a, float len, int eggs, bool mine, int pGen = 0, float pBorn = 0) : base(w, x, y, a)
    {
        _pGen = pGen; _pBorn = pBorn;
        Kind = Kind.None; Layer = 1; Solid = false; FlowK = 0; _l = len; _n = eggs; _mine = mine; R = len / 2 + 5; Name = "wylinka z jajami"; Latin = "Hypsibius exemplaris";
        _eggs = new Vector2[eggs];
        for (int i = 0; i < eggs; i++) _eggs[i] = new Vector2(Rand(-0.3f, 0.3f) * len, Rand(-0.1f, 0.1f) * len);
    }
    public override void Step(float dt, float gdt)
    {
        if (W.Sc.Dormant) return;
        _t += gdt;
        // rozwój zarodka ~4–5 dni (w grze skrócony do ~50 s)
        if (_t > 50 && !W.Preview)
        {
            for (int i = 0; i < _n; i++)
            {
                ToWorld(_eggs[i].X, _eggs[i].Y, out float x, out float y);
                var j = W.Add(new Tardigrade(W, x, y, Rand(0, TAU), false, 0.42f)); W.Born(_pGen, _pBorn, _mine, j, i == 0); j.Energy = 0.25f;
            }
            W.Sfx("hatch", X, Y, 0.9f);
            if (_mine) { W.AddStat("hatched", _n); W.Log($"Z jaj w wylince wykluło się {_n} młodych niesporczaków.", "good"); }
            Alive = false;
        }
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        float hl = _l / 2, hw = _l * 0.2f;
        p.Ellipse(0, 0, hl, hw, 0, Math.Max(v.Px, 0.8f), Rgb(200, 195, 170, 0.35f));
        for (int k = -3; k <= 3; k++) p.Arc(k * hl * 0.25f, 0, hw * 0.9f, -1.2f, 1.2f, Math.Max(v.Px, 0.5f), Rgb(190, 185, 160, 0.2f));
        float dev = Math.Min(1, _t / 50);
        foreach (var e in _eggs)
        {
            p.FillCircle(e.X, e.Y, _l * 0.055f, Rgb(215, 205, 170, 0.6f));
            p.Circle(e.X, e.Y, _l * 0.055f, Math.Max(v.Px, 0.5f), Rgb(120, 110, 80, 0.6f));
            if (dev > 0.5f) p.FillEllipse(e.X, e.Y, _l * 0.04f * dev, _l * 0.02f * dev, A, Rgb(140, 130, 100, 0.5f)); // zarodek
        }
    }
}

/// <summary>
/// Niesporczak (Tardigrada). Hypsibius exemplaris (~250 µm): 4 pary krępych nóżek z pazurkami, chodzi jak owady
/// (średnio ~163 µm/s, ok. 0,5 długości ciała/s, zakres ~80–260 µm/s – Nirody i in., PNAS 2021). Przebija komórki glonów sztyletami
/// i wysysa zawartość. Przy wysychaniu zwija się w "beczułkę" (tun) i przechodzi w anhydrobiozę.
/// Milnesium – drapieżny niesporczak z szerokim otworem gębowym; poluje na wrotki i mniejsze niesporczaki.
/// </summary>
public sealed class Tardigrade : Ent
{
    /// <summary>Pełza/chodzi po podłożu – nie wytwarza pola przepływu pływaka.</summary>
    public override bool Swims => false;
    public readonly bool Predator;
    public float L, Wd, Energy = 0.4f, Tun, Molt, Stylet, Walk, Grow = 1;
    public bool TunWant;
    public Steer Sw;
    float _head, _wT, _seekT, _eatCD, _dmgFlash; Ent _prey;
    /// <summary>Wygięcie osi ciała (y = Bend·x²): szkielet hydrostatyczny – ciało wygina się w stronę skrętu.</summary>
    public float Bend;
    readonly Vector3[] _store;
    public override float Length => L * Grow * (1 - 0.45f * Tun);
    public override float Radius => Wd * Grow * (1 + 0.05f * Tun) / 2;
    /// <summary>Beczułka (tun) = anhydrobioza: odporna na wyschnięcie; ruch niemożliwy.</summary>
    public bool IsTun => Tun > 0.9f;

    public Tardigrade(World w, float x, float y, float a, bool predator = false, float grow = 1) : base(w, x, y, a)
    {
        Predator = predator; Grow = grow;
        Kind = Kind.Metazoan; Layer = 3; Soft = 0.18f; // szkielet hydrostatyczny pod oskórkiem: ciało lekko ustępuje, ale nie przenika się z innymi Edible = !predator; Food = 2;
        Name = predator ? "niesporczak drapieżny" : "niesporczak"; Latin = predator ? "Milnesium inceptum" : "Hypsibius exemplaris";
        // H. exemplaris: ~250 × 65–75 µm (smukły, ok. 3,5 : 1); Milnesium: do ~0,7 mm, szerszy przód
        L = predator ? Rand(420, 600) : Rand(220, 290); Wd = L * (predator ? 0.3f : 0.29f);
        Mass = L * Wd / 250; R = L / 2 + 40; _head = a;
        // komórki zapasowe (celomocyty) – setki okrągłych komórek ~5–12 µm swobodnie krążących w jamie ciała
        _store = new Vector3[70];
        for (int i = 0; i < _store.Length; i++) _store[i] = new Vector3(Rand(-0.72f, 0.55f), Rand(-0.75f, 0.75f), Rand(0, TAU));
    }
    static float Prof(float t)
    {
        // tułów: 4 lekko zaznaczone segmenty, zaokrąglona głowa, tył z czwartą parą nóg
        float b = MathF.Pow(Math.Max(0, 1 - t * t), 0.45f);
        return b * (1 + 0.035f * MathF.Cos((t + 1) * PI * 4.5f));
    }
    public override void UpdatePrim() => AxisParts(3, Length / 2, Radius, Prof, Bend);
    /// <summary>Długość kroku (µm) i udział fazy podporu: stopa stoi na podłożu, gdy ciało przesuwa się nad nią.</summary>
    float Stride => 0.16f * L * Grow;
    const float Duty = 0.6f;
    public override bool CanControl() => Alive;
    public override bool Collides(Ent o) => !(o is Exuvia);

    public override void Step(float dt, float gdt)
    {
        _eatCD -= dt; _dmgFlash -= gdt; Stylet = Math.Max(0, Stylet - dt * 3);
        bool dry = W.Sc.Dormant, player = IsPlayer && !W.Preview;
        float moist = W.Sc is ScTardi sc ? sc.Moisture : 1;
        if (Grow < 1) Grow = Math.Min(1, Grow + dt / 200 * (0.4f + Energy)); // młode rosną (kolejne linienia)
        // tun: zwinięcie ciała, wciągnięcie nóg, utrata wody (trehaloza, białka CAHS)
        if (!player && !W.Preview) TunWant = moist < 0.4f;
        if (TunWant && !dry) Tun = Math.Min(1, Tun + dt / 7);
        else if (!TunWant && !dry && moist > 0.3f) Tun = Math.Max(0, Tun - dt / 4);
        if (Tun > 0.05f || dry)
        {
            Vx = Vy = 0; Sw.V = 0; Sw.Omega = 0; Bend *= 1 - Damp(3, dt);
            return;
        }
        // linienie: zrzucenie oskórka i złożenie jaj do wylinki
        if (Molt > 0)
        {
            Molt += dt / 8; Vx = Vy = 0; Sw.V *= 0.9f;
            if (Molt >= 1)
            {
                Molt = 0;
                int eggs = RandI(2, 6);
                W.Add(new Exuvia(W, X - MathF.Cos(A) * L * 0.1f, Y - MathF.Sin(A) * L * 0.1f, A, L * Grow, eggs, Lineage || IsPlayer, Gen, BornT));
                Energy = 0.3f;
                if (IsPlayer || Lineage) W.AddStat("eggs", eggs);
                if (IsPlayer) W.Log($"Linienie zakończone: {eggs} jaj zostało w zrzuconym oskórku (wylince). Młode wyklują się za kilka dni (w grze ~50 s).", "good");
            }
            return;
        }
        float v = 0, maxW = 2.5f;
        if (player)
        {
            var I = W.Input; bool on = I.Mag > 0.05f;
            if (on) _head = I.Ang;
            v = (on ? I.Mag : 0) * (I.Boost ? 260 : 160) * Grow * (0.6f + 0.4f * moist); // maks. zmierzone ≈ 264 µm/s
        }
        else Ai(dt, ref v);
        Sw.Update(this, dt, _head, v, maxW, 5, 6);
        // cykl kroku związany z przebytą drogą: w fazie podporu stopa stoi w miejscu na podłożu (bez „ślizgania” nóg)
        Walk += dt * Math.Abs(Sw.V) / (Stride / Duty) * TAU;
        Bend += (Sw.Bend(L * Grow, Wd * Grow, 160, 0.35f) - Bend) * Damp(6, dt);
        // koszt życia i chodzenia
        Energy = Math.Max(0, Energy - dt * (0.0012f + Math.Abs(Sw.V) * 0.000006f));
        if (Energy <= 0 && !W.Preview) { Hp -= dt / 60; if (Hp <= 0) { if (player) W.Log("Niesporczak umarł z głodu.", "bad"); Kill(); return; } }
        Feed(dt);
        if (!Predator && Energy >= 1 && Grow >= 1 && !W.Preview) { Molt = 0.001f; if (player) W.Log("Dużo pokarmu – niesporczak linieje. Samica Hypsibius (partenogeneza) składa jaja do starego oskórka.", "info"); }
    }
    void Ai(float dt, ref float v)
    {
        _wT -= dt; _seekT -= dt;
        if (_seekT <= 0)
        {
            _seekT = 0.7f;
            if (Predator)
            {
                _prey = W.Near(X, Y, 320, e => e != this && e.Alive && (e is Rotifer || e is Tardigrade t && !t.Predator && !t.IsTun) && e.Holder == null);
            }
            else if (W.Sc is ScTardi sc && sc.Algae != null && sc.Algae.Nearest(X, Y, 220, out float ax, out float ay)) { _tx = ax; _ty = ay; _hasT = true; }
            else _hasT = false;
        }
        if (Predator && _prey != null && _prey.Alive) { _head = MathF.Atan2(_prey.Y - Y, _prey.X - X); v = 200; return; }
        if (!Predator && _hasT)
        {
            float d = Hypot(_tx - X, _ty - Y);
            _head = MathF.Atan2(_ty - Y, _tx - X); v = d < L * 0.6f ? 30 : 120; return;
        }
        if (_wT <= 0) { _head += RandN() * 1.2f; _wT = ExpRand(3); }
        v = 90;
    }
    float _tx, _ty; bool _hasT;
    void Mouth(out float x, out float y) => ToWorld(Length / 2 + 2, 0, out x, out y);
    void Feed(float dt)
    {
        if (_eatCD > 0 || W.Preview) return;
        Mouth(out float mx, out float my);
        if (Predator)
        {
            // Milnesium: przebija oskórek ofiary i wysysa jej zawartość
            foreach (var o in Nb)
            {
                if (!o.Alive || o == this) continue;
                if (!(o is Rotifer || o is Tardigrade t && !t.Predator)) continue;
                if (Hypot(o.X - mx, o.Y - my) > o.Radius + o.Length * 0.5f + 10) continue;
                if (o is Tardigrade tt && tt.IsTun) continue; // beczułka jest twarda i sucha – drapieżnik jej nie wysysa
                o.Damage(0.18f); Stylet = 1; _eatCD = 0.4f; Energy = Math.Min(1.2f, Energy + 0.05f);
                if (o.IsPlayer) { W.LogOnce("miln", "Milnesium przebił Twój oskórek sztyletami i wysysa zawartość ciała – uciekaj (Shift)!", "bad", 6); W.Sfx("hurt", X, Y, 0.8f); }
                if (o is Rotifer r) r.Poke();
                break;
            }
            return;
        }
        if (W.Sc is not ScTardi sc || sc.Algae == null) return;
        if (Math.Abs(Sw.V) > 90) return; // trzeba się zatrzymać, żeby wbić sztylety
        float got = sc.Algae.Pierce(mx, my, 5 * Grow);
        if (got > 0)
        {
            Energy = Math.Min(1.2f, Energy + got * 0.03f); Hp = Math.Min(1, Hp + 0.01f); Stylet = 1; _eatCD = 0.35f;
            if (IsPlayer) { W.AddStat("algae", 1); W.LogOnce("tstyl", "Sztylety przebijają ścianę komórki Chlorella, mięśniowa gardziel wysysa zawartość – zostaje pusta ścianka.", "info", 60); }
        }
    }
    public override void Kill()
    {
        if (!Alive) return;
        W.Fx.Spill(X, Y, 30, new FX.SpillOpt { Speed = L * 0.2f, R = 2.5f, Life = 5, R0 = L * 0.25f, D = 3, Cols = new[] { Rgb(190, 185, 160, 0.6f), Rgb(90, 120, 60, 0.5f) } });
        W.Sfx("pop", X, Y);
        Alive = false;
    }

    static readonly Color Cut = Rgb(205, 200, 178, 0.45f), EdgeC = Rgb(70, 66, 50, 0.7f), ClawC = Rgb(70, 62, 44, 0.6f), StoreC = Rgb(225, 220, 200, 0.45f);
    public override void Draw(Painter p, View v)
    {
        float g = Grow, tun = Smooth(Tun), hl = Length / 2, hw = Radius;
        p.Local(X, Y, A); p.Warp(X, Y, A, Bend); // cały niesporczak (tułów, nogi, głowa) wygina się razem
        float lw = Math.Max(v.Px, L * g * 0.004f);
        bool detail = L * g / Math.Max(v.Px, 1e-6f) > 60;
        // nóżki (lobopodia) – rysowane pod tułowiem; fala kroków od tyłu do przodu, strony naprzemiennie.
        // Krępy, stożkowy wyrostek (ok. tak długi jak szeroki) z dwoma podwójnymi pazurkami typu Hypsibius na końcu.
        if (tun < 0.7f && detail)
        {
            float[] lt = { 0.5f, 0.17f, -0.17f, -0.64f };
            var lb = p.Shape2;
            for (int i = 0; i < 4; i++)
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    Foot(i, sd, out float bx, out float by, out float fx, out float fy, out _);
                    float len = Hypot(fx - bx, fy - by) * (1 - tun);
                    float ang = MathF.Atan2(fy - by, fx - bx);
                    float ca = MathF.Cos(ang), sa = MathF.Sin(ang), nx = -sa, ny = ca;
                    float bw = hw * 0.2f, tw = hw * 0.13f * (1 - tun * 0.4f);
                    float tx = bx + ca * len, ty = by + sa * len;
                    // obrys nóżki: podstawa szeroka, koniec zaokrąglony
                    int k = 0;
                    lb[k++] = new Vector2(bx + nx * bw, by + ny * bw);
                    lb[k++] = new Vector2(bx + ca * len * 0.55f + nx * bw * 0.85f, by + sa * len * 0.55f + ny * bw * 0.85f);
                    for (int q = 0; q <= 6; q++) { float a2 = ang + PI / 2 - q * PI / 6; lb[k++] = new Vector2(tx + MathF.Cos(a2) * tw, ty + MathF.Sin(a2) * tw); }
                    lb[k++] = new Vector2(bx + ca * len * 0.55f - nx * bw * 0.85f, by + sa * len * 0.55f - ny * bw * 0.85f);
                    lb[k++] = new Vector2(bx - nx * bw, by - ny * bw);
                    var leg = new ReadOnlySpan<Vector2>(lb, 0, k);
                    p.FillFan(leg, bx + ca * len * 0.5f, by + sa * len * 0.5f, Rgb(214, 210, 192, 0.6f));
                    p.Stroke(leg, false, lw * 0.9f, Rgb(92, 86, 66, 0.55f));
                    // pazurki: dwa podwójne, zakrzywione haczyki (zewnętrzny dłuższy)
                    float cx = tx + ca * tw * 0.8f, cy = ty + sa * tw * 0.8f, cl = hw * 0.16f * (1 - tun), cwid = Math.Max(lw * 0.9f, hw * 0.022f);
                    for (int c = -1; c <= 1; c += 2)
                    {
                        float ba = ang + c * 0.32f, lc = cl * (c > 0 ? 1 : 0.8f);
                        float mx = cx + MathF.Cos(ba) * lc * 0.6f, my = cy + MathF.Sin(ba) * lc * 0.6f;
                        float ex2 = mx + MathF.Cos(ba + c * 0.9f) * lc * 0.5f, ey2 = my + MathF.Sin(ba + c * 0.9f) * lc * 0.5f;
                        p.Line(cx, cy, mx, my, cwid, ClawC, true);
                        p.Line(mx, my, ex2, ey2, cwid * 0.8f, ClawC, true);
                        // krótsza gałąź wtórna
                        p.Line(mx - MathF.Cos(ba) * lc * 0.25f, my - MathF.Sin(ba) * lc * 0.25f, mx + MathF.Cos(ba - c * 0.7f) * lc * 0.25f, my + MathF.Sin(ba - c * 0.7f) * lc * 0.25f, cwid * 0.7f, ClawC, true);
                    }
                }
        }
        // tułów
        const int M = 44; var sh = p.Buf2;
        for (int k = 0; k <= M; k++) { float t = -1 + 2f * k / M; float w = Prof(t) * hw; sh[k] = new Vector2(t * hl, w); sh[2 * M + 1 - k] = new Vector2(t * hl, -w); }
        var s = new ReadOnlySpan<Vector2>(sh, 0, 2 * M + 2);
        p.SoftStroke(s, true, hw * 0.3f, Rgb(255, 255, 240, 0.25f));
        Span<float> ts = stackalloc float[] { 0f, 0.7f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(218, 213, 194, 0.62f), Rgb(205, 200, 178, 0.68f), Rgb(160, 150, 120, 0.75f + 0.2f * tun) };
        p.FillRadial(s, 0, 0, ts, cs);
        p.Stroke(s, true, lw, _dmgFlash > 0 ? Rgb(170, 60, 50, 0.8f) : EdgeC);
        if (Molt > 0) p.Stroke(s, true, lw * 2.5f, Rgb(235, 230, 210, 0.45f * (1 - Molt))); // odklejający się stary oskórek
        // bruzdy oskórka – w beczułce głębokie zmarszczki
        int nf = tun > 0.3f ? 9 : 4;
        for (int k = 1; k <= nf; k++) { float t = -1 + 2f * k / (nf + 1), w = Prof(t) * hw * 0.92f; p.Arc(t * hl - w * 0.3f, 0, w, -1.25f, 1.25f, lw * 0.8f, Rgb(120, 112, 88, 0.25f + 0.25f * tun)); }
        if (!detail) return;
        // jelito: zielone od wyssanych glonów (u drapieżnika – brązowe od wrotków)
        float gut = Clamp(Energy, 0.1f, 1);
        var gc = Predator ? Rgb(150, 110, 60, 0.3f + 0.35f * gut) : Rgb(70, 130, 45, 0.25f + 0.45f * gut);
        p.FillEllipse(-hl * 0.12f, 0, hl * 0.48f * (1 - 0.3f * tun), hw * 0.42f, 0, gc);
        // komórki zapasowe swobodnie pływające w jamie ciała
        foreach (var c in _store)
        {
            // krążą z płynem jamy ciała, przepychane skurczami mięśni przy chodzeniu
            float jx = c.X + MathF.Sin(W.T * 0.7f + c.Z + Walk * 0.3f) * 0.04f, jy = c.Y + MathF.Cos(W.T * 0.6f + c.Z) * 0.04f;
            float cr = hw * (0.05f + 0.035f * MathF.Sin(c.Z * 3.7f) * MathF.Sin(c.Z * 3.7f));
            float sx = jx * hl, sy = jy * Prof(jx) * hw * 0.82f;
            p.FillCircle(sx, sy, cr, StoreC);
            p.Circle(sx, sy, cr, Math.Max(v.Px * 0.7f, cr * 0.18f), Rgb(120, 112, 86, 0.35f));
            p.Dot(sx - cr * 0.3f, sy - cr * 0.3f, cr * 0.3f, Rgb(250, 248, 235, 0.55f));
        }
        // oocyty przy dużym zapasie energii
        if (!Predator && Energy > 0.6f && g >= 1) for (int k = 0; k < 3; k++) p.FillCircle(-hl * (0.35f + k * 0.13f), (k - 1) * hw * 0.25f, hw * 0.16f * Math.Min(1, (Energy - 0.6f) * 2.5f), Rgb(225, 215, 180, 0.55f));
        if (tun > 0.5f) return;
        // głowa: oczka, rurka gębowa, sztylety, mięśniowa gardziel z płytkami (placoidami)
        float ex = hl * 0.72f;
        for (int sd = -1; sd <= 1; sd += 2) p.FillCircle(ex, sd * hw * 0.32f, hw * 0.06f, Rgb(40, 25, 25, 0.85f));
        float bulbX = hl * 0.42f;
        p.Line(hl * 0.98f, 0, bulbX + hw * 0.2f, 0, Math.Max(lw, hw * 0.05f), Rgb(90, 80, 60, 0.6f));
        if (Predator) p.Arc(hl * 0.98f, 0, hw * 0.22f, -1.3f, 1.3f, lw * 1.5f, EdgeC); // szeroki otwór gębowy Milnesium
        float st = Stylet * hw * 0.25f;
        for (int sd = -1; sd <= 1; sd += 2) p.Line(bulbX + hw * 0.15f, sd * hw * 0.12f, hl * 0.95f + st, sd * hw * 0.03f, Math.Max(lw * 0.8f, hw * 0.025f), Rgb(60, 50, 35, 0.75f));
        p.FillEllipse(bulbX, 0, hw * 0.24f, hw * 0.2f, 0, Rgb(150, 140, 110, 0.55f));
        p.Ellipse(bulbX, 0, hw * 0.24f, hw * 0.2f, 0, lw, Rgb(80, 72, 52, 0.6f));
        if (!Predator) for (int k = -1; k <= 1; k++) p.Line(bulbX - hw * 0.12f + k * hw * 0.09f, -hw * 0.08f, bulbX - hw * 0.12f + k * hw * 0.09f, hw * 0.08f, Math.Max(lw, hw * 0.04f), Rgb(60, 52, 36, 0.7f));
    }
    public void Hurt() { _dmgFlash = 0.3f; }
    static readonly float[] LegT = { 0.5f, 0.17f, -0.17f, -0.64f };
    /// <summary>
    /// Kinematyka nogi i (0 = przednia) po stronie sd (układ ciała): nasada (bx, by) i stopa (fx, fy); stance = stopa na podłożu.
    /// Chód: fala kroków od tyłu ku przodowi, nogi jednej pary w przeciwfazie (Nirody i in., PNAS 2021); w podporze stopa jest
    /// nieruchoma względem podłoża (cofa się względem ciała o długość kroku), w przenoszeniu – uniesiona wraca do przodu.
    /// </summary>
    public void Foot(int i, int sd, out float bx, out float by, out float fx, out float fy, out bool stance)
    {
        float tun = Smooth(Tun), hl = Length / 2, hw = Radius;
        float u = ((Walk + (3 - i) * 1.3f + (sd > 0 ? PI : 0)) / TAU % 1 + 1) % 1, off, lift;
        stance = u < Duty;
        if (stance) { off = 0.5f - u / Duty; lift = 0; }
        else { float s2 = (u - Duty) / (1 - Duty); off = -0.5f + Smooth(s2); lift = MathF.Sin(PI * s2); }
        float t = LegT[i]; bx = t * hl; by = sd * Prof(t) * hw * 0.8f;
        float reach = hw * (0.62f - 0.1f * lift), str = Stride * (i == 3 ? 0.35f : 1) * (1 - tun);
        fx = i == 3 ? bx - reach * 0.75f + off * str : bx + off * str; fy = by + sd * (i == 3 ? reach * 0.55f : reach);
    }
}

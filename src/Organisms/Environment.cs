namespace Mikro;

/// <summary>
/// Ziarno mineralne (najczęściej kwarc) – nieruchoma, szklista przeszkoda. Brzeg nieregularny, kanciasty
/// (ziarna słodkowodne rzadko są dobrze obtoczone), z odłamami muszlowymi. Kwarc (n ≈ 1,54) załamuje światło mocniej
/// niż woda (1,33), więc w mikroskopie ziarno ma wyraźny ciemny brzeg, jasny pas światła wewnątrz krawędzi (linia Beckego)
/// i kilka zakrzywionych spękań oraz drobnych wrostków.
/// </summary>
public sealed class Grain : Ent
{
    readonly bool _static;
    public override bool StaticDraw => _static;
    readonly Vector2[] _pts, _fine; readonly float _r;
    readonly (Vector2 a, Vector2 b, Vector2 c)[] _cracks; readonly Vector3[] _incl;
    public override float Radius => _r;
    public Grain(World w, float x, float y, float r) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.None; Layer = 1; Fixed = true; FlowK = 0; Mass = 1e6f; _r = r; R = r * 1.25f; _static = true;
        // zgrubny wielokąt (kolizje) – kilka ścian łupliwych o różnej długości
        int n = RandI(7, 10); _pts = new Vector2[n];
        float t0 = Rand(0, TAU), el = Rand(0.75f, 1f);
        for (int i = 0; i < n; i++)
        {
            float t = t0 + i * TAU / n + Rand(-0.25f, 0.25f), q = r * Rand(0.78f, 1.15f);
            _pts[i] = new Vector2(MathF.Cos(t) * q, MathF.Sin(t) * q * el);
        }
        // drobny obrys do rysowania: podział krawędzi z przesunięciem punktu środkowego (chropowatość fraktalna)
        var fine = new List<Vector2>();
        for (int i = 0; i < n; i++)
        {
            var a = _pts[i]; var b = _pts[(i + 1) % n];
            var seg = new List<Vector2> { a, b };
            float amp = (b - a).Length() * 0.07f;
            for (int lev = 0; lev < 3; lev++, amp *= 0.5f)
            {
                var nxt = new List<Vector2>();
                for (int k = 0; k < seg.Count - 1; k++)
                {
                    var p0 = seg[k]; var p1 = seg[k + 1]; var d = p1 - p0; var nr = new Vector2(-d.Y, d.X).Normalized();
                    nxt.Add(p0); nxt.Add((p0 + p1) * 0.5f + nr * Rand(-amp, amp * 0.6f));
                }
                nxt.Add(seg[^1]); seg = nxt;
            }
            for (int k = 0; k < seg.Count - 1; k++) fine.Add(seg[k]);
        }
        _fine = fine.ToArray();
        // spękania muszlowe (łuki od brzegu) i wrostki (pęcherzyki cieczy, drobne kryształki)
        _cracks = new (Vector2, Vector2, Vector2)[RandI(2, 4)];
        for (int i = 0; i < _cracks.Length; i++)
        {
            var e0 = _pts[RandI(0, n - 1)] * Rand(0.85f, 0.97f);
            var e1 = _pts[RandI(0, n - 1)] * Rand(0.2f, 0.6f);
            var mid = (e0 + e1) * 0.5f + new Vector2(-(e1 - e0).Y, (e1 - e0).X) * Rand(-0.35f, 0.35f);
            _cracks[i] = (e0, mid, e1);
        }
        _incl = new Vector3[RandI(3, 8)];
        for (int i = 0; i < _incl.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * r * 0.6f; _incl[i] = new Vector3(MathF.Cos(t) * q, MathF.Sin(t) * q * el, r * Rand(0.015f, 0.05f)); }
        Name = "ziarno kwarcu"; Latin = "SiO₂";
    }
    public override void UpdatePrim()
    {
        int n = _pts.Length; UseParts(n + 1);
        float mn = float.MaxValue; foreach (var q in _pts) mn = Math.Min(mn, q.Length());
        PartCircle(0, 0, 0, mn * 0.95f);
        float er = _r * 0.1f;
        for (int i = 0; i < n; i++)
        {
            var a = _pts[i]; var b = _pts[(i + 1) % n];
            var ai = a * (1 - er / Math.Max(a.Length(), 1e-3f)); var bi = b * (1 - er / Math.Max(b.Length(), 1e-3f));
            PartCap(i + 1, ai.X, ai.Y, bi.X, bi.Y, er);
        }
        Prim.Cap = false; Prim.X = X; Prim.Y = Y; Prim.R = mn;
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        var f = new ReadOnlySpan<Vector2>(_fine);
        float lw = Math.Max(v.Px * 1.2f, Math.Min(_r * 0.03f, v.Px * 3.5f));
        // szkliste wnętrze: prawie przezroczyste, lekko jaśniejsze od strony światła
        Span<float> ts = stackalloc float[] { 0f, 0.75f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(214, 214, 196, 0.32f), Rgb(186, 186, 166, 0.38f), Rgb(140, 138, 118, 0.5f) };
        p.FillRadial(f, -_r * 0.2f, -_r * 0.25f, ts, cs);
        // linia Beckego: jasny pas tuż wewnątrz brzegu (ziarno ma większy współczynnik załamania niż woda)
        var buf = p.Shape2; int m = _fine.Length;
        for (int i = 0; i < m; i++) buf[i] = _fine[i] * 0.93f;
        p.SoftStroke(new ReadOnlySpan<Vector2>(buf, 0, m), true, _r * 0.1f, Rgb(250, 250, 236, 0.4f));
        foreach (var (a, b, c) in _cracks)
        {
            var cb = p.Buf2;
            for (int k = 0; k <= 10; k++) { float t = k / 10f, u = 1 - t; cb[k] = a * (u * u) + b * (2 * u * t) + c * (t * t); }
            var cr = new ReadOnlySpan<Vector2>(cb, 0, 11);
            p.Stroke(cr, false, lw * 0.8f, Rgb(70, 70, 58, 0.45f), true);
            p.Stroke(cr, false, lw * 2.2f, Rgb(250, 250, 235, 0.12f), true);
        }
        foreach (var q in _incl)
        {
            p.Circle(q.X, q.Y, q.Z, Math.Max(v.Px, q.Z * 0.35f), Rgb(50, 50, 40, 0.5f));
            p.Dot(q.X - q.Z * 0.3f, q.Y - q.Z * 0.3f, q.Z * 0.35f, Rgb(255, 255, 245, 0.7f));
        }
        p.Stroke(f, true, lw, Rgb(38, 38, 30, 0.85f));
    }
}

/// <summary>Włókno kolagenowe tkanki łącznej – falista wiązka fibryli ("crimp"), przeszkoda dla komórek.</summary>
public sealed class Collagen : Ent
{
    readonly float _hl, _rad, _ph, _crimp;
    /// <summary>Postęp trawienia włókna przez metaloproteinazy (MT1-MMP komórki nowotworowej), 0..1.</summary>
    public float Deg;
    /// <summary>crimp &lt; 1 – włókno wyprostowane i napięte (np. kolagen promienisty przy froncie inwazji guza, TACS-3).</summary>
    public Collagen(World w, float x, float y, float a, float len, float rad, float crimp = 1) : base(w, x, y, a)
    {
        Kind = Kind.None; Layer = 0; Fixed = true; FlowK = 0; Name = "włókno kolagenowe"; _hl = len / 2; _rad = rad; R = len / 2 + rad; _ph = Rand(0, TAU); _crimp = crimp;
    }
    public override void UpdatePrim() => SetCapsule(_hl, _rad);
    public override bool StaticDraw => true;
    public override float StaticKey => MathF.Round(Deg * 20) / 20;
    /// <summary>Leukocyty i erytrocyty przeciskają się przez sieć kolagenu (tkanka jest trójwymiarowa, włókna tworzą oczka).</summary>
    /// <remarks>Fibroblasty żyją wewnątrz sieci kolagenu (same ją wytwarzają) – w rzucie 2D włókna leżą nad nimi i pod nimi.</remarks>
    public override bool Permeable(Ent o) => o.Soft >= 0.5f && !o.Fixed || o is Fibroblast;
    /// <summary>
    /// Włókno leży wyżej lub niżej w trójwymiarowej sieci, więc nie odpycha komórki, tylko ją spowalnia: leukocyt
    /// wsuwa przód między włókna i przeciska jądro przez oczko (Lämmermann i in. 2008, Wolf i in. 2013), wybierając
    /// drogę najmniejszego oporu (Renkawitz i in. 2019). Ruch w poprzek włókna kosztuje więcej niż wzdłuż (prowadzenie kontaktowe).
    /// </summary>
    public override void Seep(Ent o, float pen, float nx, float ny)
    {
        if (o is Fibroblast fb) { fb.Guide(A); return; }
        float sp = Hypot(o.Vx, o.Vy);
        float cross = sp > 1e-9f ? MathF.Abs(o.Vx * MathF.Sin(A) - o.Vy * MathF.Cos(A)) / sp : 1;
        if (o is TumorCell t)
        {
            // komórka nowotworowa ma duże, sztywne jądro: bez proteolizy utyka w ciasnych oczkach (Wolf i in. 2013);
            // w trybie mezenchymalnym MT1-MMP tnie włókna i otwiera tunel (Sabeh i in. 2004)
            float k = 1 - 0.85f * Deg;
            o.Mesh += (t.Amoeboid ? 0.7f + 2.2f * cross : 0.2f + 0.3f * cross) * k;
            if (!t.Amoeboid && t.Emt > 0.3f) t.Cutting = this;
            return;
        }
        o.Mesh += 0.1f + 0.3f * cross;
    }
    /// <summary>Proteoliza włókna; strawione znika (w tkance zostaje tunel, którym mogą iść kolejne komórki).</summary>
    public void Degrade(float amt)
    {
        Deg += amt;
        if (Deg >= 1) { Alive = false; W.Fx.Spill(X, Y, 10, new FX.SpillOpt { Speed = 1.5f, R = 0.2f, Life = 3, R0 = _hl * 0.6f, D = 0.02f, Cols = new[] { Rgb(225, 222, 205, 0.5f) } }); }
    }
    public override void Draw(Painter p, View v)
    {
        // włókno jest nieruchome: trójkąty liczone raz i odtwarzane, dopóki nie zmieni się powiększenie albo stopień strawienia
        var geo = p.CacheFor(Id);
        if (p.TryReplay(geo, Deg)) return;
        p.BeginCapture();
        DrawFibre(p);
        p.EndCapture(geo, Deg);
    }
    void DrawFibre(Painter p)
    {
        p.Local(X, Y, A);
        var buf = p.Buf2; int n = (int)Clamp(_hl * 2 / Math.Max(1.1f, p.Px * 5), 8, 100);
        // boczne włókienka są ~0,3–0,4 µm od środkowego: przy oddaleniu (< 2,5 px) zlewają się w jedno szersze pasmo
        bool merge = _rad * 0.45f / p.Px < 2.5f;
        for (int f = merge ? 0 : -1; f <= (merge ? 0 : 1); f++)
        {
            for (int k = 0; k <= n; k++)
            {
                float x = -_hl + 2 * _hl * k / n;
                buf[k] = new Vector2(x, f * _rad * 0.45f + MathF.Sin(x * 1.1f + _ph + f) * _rad * 0.35f * _crimp);
            }
            var s = new ReadOnlySpan<Vector2>(buf, 0, n + 1);
            float al = 1 - 0.75f * Deg;
            if (f == 0) p.SoftStroke(s, false, _rad * 3.2f, Rgb(255, 255, 245, 0.22f * al));
            // środkowe pasmo z wygładzaniem krawędzi, boczne – cienkie linie (tańsze przy setkach włókien)
            if (f == 0) p.Stroke(s, false, _rad * (merge ? 0.7f : 0.32f), Rgb(225, 222, 205, (merge ? 0.62f : 0.55f) * al), !merge);
            else p.Thin(s, false, _rad * 0.32f, Rgb(225, 222, 205, 0.5f * al));
        }
    }
}

/// <summary>Komórka nitki skrętnicy (Spirogyra) – spiralne, taśmowate chloroplasty z pirenoidami.</summary>
public sealed class SpiroCell : Ent
{
    readonly float _hl, _hw, _ph, _pitch; readonly int _ribbons;
    public SpiroCell(World w, float x, float y, float a, float len, float wid, int ribbons, float phase) : base(w, x, y, a)
    {
        Kind = Kind.None; Layer = 1; Fixed = true; FlowK = 0; _hl = len / 2; _hw = wid / 2; R = len / 2 + wid / 2; _ph = phase; _ribbons = ribbons; _pitch = Rand(55, 80);
        Name = "skrętnica"; Latin = "Spirogyra";
    }
    public override void UpdatePrim() => SetCapsule(Math.Max(0, _hl - _hw * 0.6f), _hw);
    public override bool StaticDraw => true;
    /// <summary>
    /// Komórka skrętnicy w świetle przechodzącym: prawie przezroczysta, cienka ściana; taśmowaty chloroplast o ząbkowanych
    /// brzegach biegnie spiralnie przy ścianie – odcinki po bliższej stronie komórki są ostre i ciemnozielone, po dalszej –
    /// blade i rozmyte; w taśmie pirenoidy (jasne ziarna z otoczką skrobi); jądro w środku na pasmach cytoplazmy.
    /// </summary>
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        float hl = _hl, hw = _hw;
        bool detail = hw * 2 / Math.Max(v.Px, 1e-6f) > 30;
        Span<Vector2> box = stackalloc Vector2[] { new(-hl, -hw), new(hl, -hw), new(hl, hw), new(-hl, hw) };
        p.SoftStroke(box, true, 6, Rgb(255, 255, 240, 0.26f));
        p.FillFan(box, 0, 0, Rgb(196, 222, 176, 0.2f));
        var buf = p.Buf2;
        int n = detail ? 64 : 24;
        for (int r = 0; r < _ribbons; r++)
        {
            // taśma: dwa przebiegi (bliższy/dalszy) rysowane osobno – bliższy ciemniejszy i szerszy
            for (int pass = 0; pass < 2; pass++)
            {
                int k0 = -1;
                for (int k = 0; k <= n + 1; k++)
                {
                    bool inPass = false; float x = 0, y = 0;
                    if (k <= n)
                    {
                        x = -hl + 2 * hl * k / n; float ph = TAU * x / _pitch + _ph + r * PI;
                        y = hw * 0.8f * MathF.Sin(ph); inPass = (MathF.Cos(ph) >= 0) == (pass == 0);
                    }
                    if (inPass) { if (k0 < 0) k0 = k; buf[k - k0] = new Vector2(x, y); }
                    else if (k0 >= 0)
                    {
                        int cnt = k - k0;
                        if (cnt > 1)
                        {
                            var sg = new ReadOnlySpan<Vector2>(buf, 0, cnt);
                            if (pass == 0) { p.Stroke(sg, false, 6.5f, Rgb(62, 140, 44, 0.72f), true); if (detail) p.Thin(sg, false, 1.4f, Rgb(170, 220, 130, 0.35f)); }
                            else p.SoftStroke(sg, false, 9f, Rgb(90, 160, 70, 0.32f));
                        }
                        k0 = -1;
                    }
                }
            }
            // pirenoidy w taśmie (tylko na bliższym przebiegu – na dalszym są poza ostrością)
            if (detail)
                for (float x = -hl + 6; x < hl - 4; x += 11)
                {
                    float ph = TAU * x / _pitch + _ph + r * PI; if (MathF.Cos(ph) < 0.2f) continue;
                    float y = hw * 0.8f * MathF.Sin(ph);
                    p.FillCircle(x, y, 1.9f, Rgb(215, 240, 190, 0.75f));
                    p.Circle(x, y, 2.3f, Math.Max(v.Px, 0.5f), Rgb(40, 90, 30, 0.55f));
                }
        }
        // jądro zawieszone na pasmach cytoplazmy
        p.FillCircle(0, 0, hw * 0.2f, Rgb(210, 222, 200, 0.45f));
        p.Circle(0, 0, hw * 0.2f, Math.Max(v.Px, 0.5f), Rgb(90, 110, 80, 0.4f));
        if (detail) for (int k = 0; k < 4; k++) { float t = k * TAU / 4 + 0.4f; p.Seg(MathF.Cos(t) * hw * 0.2f, MathF.Sin(t) * hw * 0.2f, MathF.Cos(t) * hw * 0.85f, MathF.Sin(t) * hw * 0.85f, Math.Max(v.Px, 0.6f), Rgb(215, 225, 205, 0.3f)); }
        // cienka ściana komórkowa i poprzeczna ścianka
        p.Stroke(box, true, 0.9f, Rgb(50, 80, 45, 0.55f));
        p.Line(-hl, -hw, -hl, hw, 1.1f, Rgb(45, 75, 40, 0.6f));
    }

    /// <summary>Tworzy nitkę z komórek wzdłuż łagodnie zakrzywionej krzywej.</summary>
    public static void Filament(World w, float x, float y, float a, int cells)
    {
        float wid = Rand(32, 46), ph = Rand(0, TAU), curv = Rand(-0.08f, 0.08f); int rib = RandI(1, 2);
        for (int i = 0; i < cells; i++)
        {
            float len = Rand(80, 120);
            float cx = x + MathF.Cos(a) * len / 2, cy = y + MathF.Sin(a) * len / 2;
            if (cx < 50 || cy < 50 || cx > w.W - 50 || cy > w.H - 50) break;
            w.Add(new SpiroCell(w, cx, cy, a, len, wid, rib, ph + i * 1.7f));
            x += MathF.Cos(a) * len; y += MathF.Sin(a) * len;
            a += curv + RandN() * 0.03f;
        }
    }
}

/// <summary>
/// Wirczyk (Vorticella) – dzwonkowaty orzęsek na kurczliwym trzonku (spazmonema).
/// Przy dotyku trzonek zwija się w spiralę w milisekundy, potem powoli się prostuje; wir rzęsek wciąga bakterie.
/// </summary>
public sealed class Vorticella : Ent
{
    readonly float _ax, _ay, _len, _br; float _ext = 1, _cool, _cph, _sway;
    public Vorticella(World w, float gx, float gy, float ang, float baseR = 0) : base(w, gx + MathF.Cos(ang) * baseR, gy + MathF.Sin(ang) * baseR, ang)
    {
        float ax = gx + MathF.Cos(ang) * baseR, ay = gy + MathF.Sin(ang) * baseR;
        Kind = Kind.Protist; Layer = 2; Solid = false; FlowK = 0; _ax = ax; _ay = ay; _len = Rand(140, 260); _br = Rand(22, 30); R = _len + _br * 2; _sway = Rand(0, TAU);
        Name = "wirczyk"; Latin = "Vorticella";
    }
    public float BellX => _ax + MathF.Cos(A) * _len * StalkF;
    public float BellY => _ay + MathF.Sin(A) * _len * StalkF;
    float StalkF => 0.18f + 0.82f * _ext;
    public override void Integrate(float dt) { }
    public void Contract() { if (_ext > 0.6f) { _ext = 0; _cool = 1.5f; W.Sfx("snap", BellX, BellY, 0.6f); } }
    public override void Step(float dt, float gdt)
    {
        _cph += gdt * TAU * 4; _cool -= gdt; _sway += dt * 0.3f;
        A += MathF.Sin(_sway) * 0.02f * dt;
        if (_cool <= 0) _ext = Math.Min(1, _ext + dt / 3.0f);
        float bx = BellX, by = BellY;
        if (_ext > 0.7f)
        {
            var o = W.Near(bx, by, _br * 1.8f, e => e.Solid && !e.Fixed && e.Kind != Kind.None);
            if (o != null) Contract();
            else if (Chance(dt * 0.01f)) Contract();
        }
        var B = W.Bact;
        if (B != null && _ext > 0.8f)
        {
            float mx = bx + MathF.Cos(A) * _br, my = by + MathF.Sin(A) * _br;
            B.Feed(mx, my, 70, 12, 110 * dt, null);
        }
    }
    public override void Draw(Painter p, View v)
    {
        var buf = p.Buf2;
        float L = _len * StalkF, ux = MathF.Cos(A), uy = MathF.Sin(A);
        int n = 40; float coil = (1 - _ext) * 9;
        for (int k = 0; k <= n; k++)
        {
            float t = (float)k / n, off = coil * MathF.Sin(t * TAU * 9) + 3 * MathF.Sin(t * 5 + _sway) * _ext;
            buf[k] = new Vector2(_ax + ux * L * t - uy * off, _ay + uy * L * t + ux * off);
        }
        var st = new ReadOnlySpan<Vector2>(buf, 0, n + 1);
        p.SoftStroke(st, false, 5, Rgb(255, 255, 240, 0.25f));
        p.Stroke(st, false, 2.2f, Rgb(70, 76, 64, 0.55f), true);
        p.FillCircle(_ax, _ay, 3.5f, Rgb(80, 80, 60, 0.6f));
        p.Local(_ax + ux * L, _ay + uy * L, A);
        float r = _br, open = 0.35f + 0.65f * _ext;
        Span<Vector2> bell = stackalloc Vector2[20];
        for (int i = 0; i < 20; i++)
        {
            // dzwonek: zaokrąglony spód przy trzonku, wypukłe boki i lekko wywinięta warga perystomu
            float t = (float)i / 19;
            float x = t * r * 2.1f, half = r * (0.22f + 0.78f * MathF.Pow(MathF.Sin(t * PI * 0.62f) / MathF.Sin(PI * 0.62f), 0.8f)) * (i == 19 ? open * 1.06f : 1);
            bell[i] = new Vector2(x, -half);
        }
        Span<Vector2> shape = stackalloc Vector2[40];
        for (int i = 0; i < 20; i++) { shape[i] = bell[i]; shape[39 - i] = new Vector2(bell[i].X, -bell[i].Y); }
        Span<float> vts = stackalloc float[] { 0f, 1f };
        Span<Color> vcs = stackalloc Color[] { Rgb(150, 156, 128, 0.42f), Rgb(122, 132, 108, 0.5f) };
        p.BodyPoly(shape, 7, Rgb(255, 255, 240, 0.28f), r * 1.2f, 0, vts, vcs);
        p.Stroke(shape, true, 0.8f, Rgb(40, 46, 38, 0.55f));
        // makronukleus w kształcie litery C – w kontraście fazowym ledwie widoczny, jaśniejszy pas
        p.Arc(r * 1.1f, 0, r * 0.5f, 0.8f, TAU - 0.8f, 2.4f, Rgb(170, 176, 150, 0.35f), true);
        // wodniczki pokarmowe z bakteriami (ciemniejsze, różnej wielkości) krążą w cytoplazmie
        for (int k = 0; k < 6; k++)
        {
            float ph = _cph * 0.05f + k * 1.05f + Id, fx = r * (1.05f + 0.55f * MathF.Cos(ph)), fy = r * 0.45f * MathF.Sin(ph * 1.3f);
            float fr = r * (0.09f + 0.035f * (k % 3));
            p.FillCircle(fx, fy, fr, Rgb(95, 100, 70, 0.4f));
            p.Circle(fx, fy, fr, Math.Max(v.Px, fr * 0.15f), Rgb(225, 230, 205, 0.35f));
        }
        float cvp = (W.T * 0.2f + Id * 0.3f) % 1;
        p.FillCircle(r * 1.7f, r * 0.35f, 5 * Smooth(cvp / 0.9f) * (cvp > 0.9f ? 0 : 1) + 0.1f, Rgb(215, 225, 205, 0.45f));
        if (_ext > 0.5f)
            for (int k = 0; k < 14; k++)
            {
                float yy = -r * open + 2 * r * open * k / 13, ph = MathF.Sin(_cph - k * 0.6f);
                p.Seg(r * 2.1f, yy, r * 2.1f + 10 + ph * 4, yy + ph * 3, Math.Max(0.9f, v.Px), Rgb(60, 66, 55, 0.5f));
            }
    }
}

/// <summary>Zewnątrzkomórkowa pułapka neutrofila (NET) – sieć DNA z histonami i białkami ziarnistości.</summary>
public sealed class Net : Ent
{
    readonly List<Vector2[]> _strands = new(); readonly Vector2[] _dots; float _age; readonly float _rad;
    /// <summary>Sieć wyrzucona przez neutrofil gracza (bakterie zabite w niej liczą się jako Twoje).</summary>
    public bool ByPlayer;
    public Net(World w, float x, float y, float rad) : base(w, x, y, 0)
    {
        Kind = Kind.None; Layer = 1; Solid = false; FlowK = 0; _rad = rad; R = rad;
        var nodes = new Vector2[34];
        for (int i = 0; i < nodes.Length; i++) { float t = Rand(0, TAU), q = MathF.Sqrt(R01()) * rad; nodes[i] = new Vector2(MathF.Cos(t) * q, MathF.Sin(t) * q); }
        for (int i = 0; i < nodes.Length; i++)
            for (int j = i + 1; j < nodes.Length; j++)
            {
                float d = (nodes[i] - nodes[j]).Length();
                if (d > rad * 0.45f) continue;
                var s = new Vector2[8];
                for (int k = 0; k < 8; k++) { float t = k / 7f; s[k] = nodes[i].Lerp(nodes[j], t) + new Vector2(RandN(), RandN()) * rad * 0.02f; }
                _strands.Add(s);
            }
        _dots = new Vector2[120];
        for (int i = 0; i < _dots.Length; i++) { var s = _strands[RandI(0, _strands.Count - 1)]; _dots[i] = s[RandI(0, 7)]; }
    }
    public override void Integrate(float dt) { }
    public override void Step(float dt, float gdt)
    {
        _age += gdt;
        if (_age > 150) { Alive = false; return; }
        foreach (var e in W.Ents)
            if (e is Staph s && s.State == St.Free && Hypot(s.X - X, s.Y - Y) < _rad) { s.State = St.Caught; s.Solid = false; s.Owner = this; }
    }
    public override void Draw(Painter p, View v)
    {
        float al = Math.Min(1, _age / 1.5f) * Math.Min(1, (150 - _age) / 10);
        p.Local(X, Y, 0);
        p.Glow(0, 0, _rad * 1.1f, Rgb(170, 200, 230, 0.12f * al));
        var c = Rgb(200, 225, 245, 0.45f * al);
        foreach (var s in _strands) p.Stroke(s, false, 0.12f, c);
        foreach (var d in _dots) p.Dot(d.X, d.Y, 0.18f, Rgb(245, 235, 200, 0.6f * al));
    }
}

namespace Mikro;

/// <summary>Drapieżnik, który trzyma ofiarę i może ją wypuścić (np. po wystrzale trichocyst).</summary>
public interface ICaptor { void Release(bool escaped); }

public enum Motion { Ciliate, Flagellate, Walk, Sessile, Jump }

/// <summary>Opis gatunku pierwotniaka (kształt, barwa, organelle, ruch, dieta).</summary>
public sealed class Spec
{
    public string Id, Name, Latin;
    public float L0, L1, Aspect = 0.4f, Bend;
    /// <summary>Połowa szerokości (0..1) wzdłuż osi ciała t ∈ [-1 (tył), 1 (przód)].</summary>
    public Func<float, float> Profile = t => MathF.Sqrt(Math.Max(0, 1 - t * t));
    public float Speed, Turn = 1;
    public Motion Move = Motion.Ciliate;
    public Color Body = Rgb(125, 130, 115, 0.42f), Edge = Rgb(40, 44, 38, 0.7f), Halo = Rgb(255, 255, 240, 0.26f);
    public int GranN = 60; public float GranR = 1; public Color GranA = Rgb(45, 52, 45, 0.35f), GranB = Rgb(230, 236, 220, 0.35f);
    /// <summary>Chloroplasty / zoochlorelle / ziarna pigmentu.</summary>
    public int ChlN; public float ChlR = 2; public Color Chl = Rgb(70, 150, 45, 0.6f), ChlHi = Rgb(200, 240, 150, 0.4f);
    public float Cilia; public int Flag; public float FlagL;
    public bool Eyespot, Groove, Beads, MacroC, Armor, Cirri, Stripes, OralRing;
    public Color StripeC = Rgb(60, 120, 150, 0.45f);
    public int CV = 1;
    public float Soft = 0.4f;
    /// <summary>Promień prądu pokarmowego dla chmur bakterii (µm); 0 = brak.</summary>
    public float BactFeed;
    /// <summary>Ofiary pochłaniane do wodniczek.</summary>
    public Func<Protist, Ent, bool> Eats;
    /// <summary>Cele ataku (gryzienie – Coleps).</summary>
    public Func<Protist, Ent, bool> Victim;
    /// <summary>Siła prądu wciągającego ofiary (µm/s).</summary>
    public float Suck;
    public bool Edible = true; public float Food = 1;
    /// <summary>Długość po skurczu (ułamek); 0 = nie kurczy się.</summary>
    public float Contract;
    /// <summary>Obrażenia zadawane drapieżnikowi, który ją połknie (toksyny obronne).</summary>
    public float Toxin;
    public string ToxinName = "toksyny";
    /// <summary>Obrażenia na sekundę (czasu biologicznego) zadawane ofierze przy kontakcie.</summary>
    public float Bite;
    public float Metaboly;
    public float Dig = 60;
    public float Sense = 0;
    public Kind Kind = Kind.Protist;
    public string Fact;
}

/// <summary>
/// Uniwersalny pierwotniak z miękką membraną: kształt z profilu (rzutowanie promieni), rzęski/wici,
/// organelle, ruch (spirala orzęsków, podskoki wiciowców, "chodzenie" na cirrach, osiadły tryb),
/// odżywianie (prąd pokarmowy, fagocytoza, gryzienie), skurcz, toksyny obronne.
/// </summary>
public sealed class Protist : Ent, ICaptor
{
    public readonly Spec S;
    public float L, Wd, Speed, Contr, Cph, Roll, St0, AvoidCD, Energy, Meta, MetaPh, Stun;
    float _moveT, _jump, _hold, _seekT, _metaT, _lk = -1, _mk = -1, _mph, _head, _track;
    public Steer Sw;
    bool _walking = true;
    readonly int NN;
    readonly float[] _rr;
    readonly Vector3[] _gran, _chl;
    readonly float _seed, _ax, _ay, _a0;
    Ent _target;
    public sealed class Vac { public Ent O; public bool Cup = true; public float St, Lx, Ly, Sx, Sy, Tx, Ty; }
    public readonly List<Vac> Gut = new();
    readonly List<Ent> _q = new();
    static readonly Vector2[] _poly = new Vector2[128];

    public float LenK => Lerp(1, S.Contract > 0 ? S.Contract : 1, Smooth(Contr)) * (1 - 0.22f * Meta);
    public float WidK => 1 / MathF.Sqrt(LenK);
    public override float Length => L * LenK;
    public override float Radius => Wd * WidK / 2;
    public override St FreeState => St.Swim;
    public override bool CanControl() => false;

    public Protist(World w, Spec s, float x, float y, float a, float scale = 1) : base(w, x, y, a)
    {
        S = s; Kind = s.Kind; Layer = 3; Name = s.Name; Latin = s.Latin; Soft = s.Soft;
        L = Rand(s.L0, s.L1) * scale; Wd = L * s.Aspect; Speed = s.Speed * Rand(0.8f, 1.15f);
        NN = L * s.Aspect > 40 || L > 300 ? 56 : 44;
        _rr = new float[NN];
        Mem = new Membrane(NN) { MaxDef = Wd * 0.35f, Stiff = s.Armor ? 20 : 8, Damping = 0.55f }; MemK = 4; MemS = s.Armor ? 0.3f : 0.75f;
        _head = a;
        Mass = Math.Max(0.5f, L * Wd / 400);
        Edible = s.Edible; Food = s.Food; State = St.Swim;
        Roll = Rand(0, TAU); Cph = Rand(0, TAU); _seed = Rand(0, 99); MetaPh = Rand(0, TAU); _metaT = ExpRand(12);
        if (s.Move == Motion.Sessile) { Fixed = true; FlowK = 0; }
        _ax = x; _ay = y; _a0 = a;
        _gran = new Vector3[s.GranN];
        for (int i = 0; i < _gran.Length; i++) _gran[i] = new Vector3(Rand(0, TAU), MathF.Sqrt(R01()) * 0.85f, Chance(0.5f) ? 1 : 0);
        _chl = new Vector3[s.ChlN];
        for (int i = 0; i < _chl.Length; i++) _chl[i] = new Vector3(Rand(0, TAU), MathF.Sqrt(R01()) * 0.86f, Rand(0.75f, 1.2f));
        R = L / 2 + s.Cilia + 6;
        Compute(); Mem.Follow(x, y, a, 1, 1);
    }

    // ------------------------------------------------------------------ kształt
    void Compute()
    {
        float lk = LenK;
        // kształt przeliczany tylko przy wyraźnej zmianie (skurcz, metabolia) i tylko w pobliżu kadru
        bool metaMove = Meta > 0 && MathF.Abs(MetaPh - _mph) > 0.12f;
        if (_lk > 0 && MathF.Abs(lk - _lk) < 0.008f && MathF.Abs(Meta - _mk) < 0.02f && !metaMove) return;
        if (_lk > 0 && !W.OnScreen(this, 1.8f)) return;
        _lk = lk; _mk = Meta; _mph = MetaPh;
        float hl = L * lk / 2, hw = Wd * WidK / 2;
        const int M = 24;
        for (int k = 0; k <= M; k++)
        {
            float t = -1 + 2f * k / M, x = t * hl;
            float w = S.Profile(t) * hw;
            if (Meta > 0) w *= 1 + Meta * 0.5f * MathF.Sin(t * 3.2f - MetaPh);
            float yc = S.Bend * hw * (t * t - 0.35f);
            _poly[k] = new Vector2(x, yc + Math.Max(w, hw * 0.04f));
            _poly[2 * M + 1 - k] = new Vector2(x, yc - Math.Max(w, hw * 0.04f));
        }
        int np = 2 * M + 2;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, dx = MathF.Cos(th), dy = MathF.Sin(th), best = 0;
            for (int k = 0; k < np; k++)
            {
                var p = _poly[k]; var q = _poly[(k + 1) % np];
                float ex = q.X - p.X, ey = q.Y - p.Y, den = dx * ey - dy * ex;
                if (MathF.Abs(den) < 1e-9f) continue;
                float r = (p.X * ey - p.Y * ex) / den, u = (p.X * dy - p.Y * dx) / den;
                if (u >= -1e-4f && u <= 1.0001f && r > best) best = r;
            }
            _rr[i] = Math.Max(best, hw * 0.1f);
        }
        foreach (var f in Gut)
        {
            if (!f.Cup) continue;
            float d = Hypot(f.Lx, f.Ly), ap = MathF.Atan2(f.Ly, f.Lx), need = d + f.O.Radius * 0.9f + 1;
            for (int i = 0; i < NN; i++) { float g = Gauss(WrapA(i * TAU / NN - ap), 0.35f); if (g > 0.01f) _rr[i] = Math.Max(_rr[i], Lerp(_rr[i], need, g)); }
            _lk = -1;
        }
        Mem.Radial(_rr);
    }
    public override void UpdatePrim()
    {
        if (State is St.Cup or St.Vac) { SetCircle(Math.Max(0.1f, Radius * 0.5f)); return; }
        float hl = L * LenK / 2, hw = Wd * WidK / 2;
        // kształt kolizyjny = profil ciała (stożek Stentora, gruszka Blepharisma, wrzeciono Eugleny), wygięty jak membrana
        AxisParts(L / Wd > 5 ? 6 : 4, hl, hw, S.Profile, Mem.Bend, S.Bend != 0 ? t => S.Bend * hw * (t * t - 0.35f) : null);
    }
    public override bool Collides(Ent o) => State is not (St.Cup or St.Vac or St.Caught) && !(o.Holder == this);

    public void ContractNow()
    {
        if (S.Contract <= 0) return;
        if (Contr < 0.5f) W.Sfx("snap", X, Y, 0.5f);
        Contr = 1; _hold = Rand(0.4f, 1.2f);
    }

    // ------------------------------------------------------------------ zachowanie
    public override void Step(float dt, float gdt)
    {
        Cph += gdt * TAU * 4;
        AvoidCD -= dt; Stun -= dt;
        if (State is St.Cup or St.Vac or St.Caught) { Vx = Vy = 0; Compute(); return; }
        if (S.Contract > 0)
        {
            if (_hold > 0) _hold -= dt;
            else Contr = Math.Max(0, Contr - dt / (S.Move == Motion.Sessile ? 8 : 3));
        }
        float v = Speed, maxW = 2.2f * S.Turn, acc = 3, tacc = 3.5f;
        switch (S.Move)
        {
            case Motion.Ciliate:
                Roll += dt * TAU * 0.8f;
                _head += RandN() * 0.3f * MathF.Sqrt(dt);
                break;
            case Motion.Flagellate:
                Roll += dt * TAU * 1.4f;
                _head += RandN() * 0.45f * MathF.Sqrt(dt);
                if (S.Metaboly > 0)
                {
                    _metaT -= dt;
                    if (_metaT <= 0) { _walking = !_walking; _metaT = _walking ? ExpRand(14) : Rand(4, 9); }
                    Meta = _walking ? Math.Max(0, Meta - dt / 1.5f) : Math.Min(1, Meta + dt / 1.5f);
                    if (Meta > 0) { MetaPh += dt * 2.4f; v *= 1 - Meta; }
                }
                break;
            case Motion.Walk:
                _moveT -= dt;
                if (_moveT <= 0) { _walking = !_walking; _moveT = _walking ? Rand(1.5f, 4) : Rand(0.4f, 1.6f); if (_walking) _head += RandN() * 1.3f; }
                if (!_walking) v = 0;
                acc = 6; tacc = 6;
                break;
            case Motion.Jump:
                _moveT -= dt; _jump -= dt;
                if (_moveT <= 0) { _moveT = ExpRand(1.2f); _head += RandN() * 1.6f; _jump = 0.2f; }
                v *= _jump > 0 ? 3 : 0.2f;
                acc = 14; tacc = 10; maxW = 8;
                break;
            case Motion.Sessile:
                v = 0; X = _ax; Y = _ay;
                A = _a0 + MathF.Sin(W.T * 0.25f + _seed) * 0.25f; Vx = Vy = 0;
                break;
        }
        // polowanie: słaba chemotaksja – ofiara musi być przed nami (rzęski nie "widzą" do tyłu), pościg ma limit czasu
        if ((S.Eats != null || S.Victim != null) && S.Move != Motion.Sessile)
        {
            _seekT -= dt; _track -= dt;
            if (_seekT <= 0)
            {
                _seekT = 0.6f;
                float sense = S.Sense > 0 ? S.Sense : L * 3 + 100;
                bool Front(Ent e) => MathF.Abs(WrapA(MathF.Atan2(e.Y - Y, e.X - X) - A)) < 1.5f;
                var nt = W.Near(X, Y, sense, e => e != this && e.Alive && Front(e) && ((S.Victim != null && S.Victim(this, e) && e.Hp < 1) || (S.Eats != null && S.Eats(this, e))));
                if (nt == null && S.Victim != null) nt = W.Near(X, Y, sense * 0.5f, e => e != this && e.Alive && Front(e) && S.Victim(this, e));
                if (nt != _target) { _target = nt; _track = 8; }
                if (_track <= 0) _target = null;
            }
            if (_target != null && _target.Alive) _head = Turn(_head, MathF.Atan2(_target.Y - Y, _target.X - X), (S.Victim != null ? 1.0f : 0.45f) * dt);
        }
        if (State == St.Avoid)
        {
            St0 -= dt; v = -0.45f * Speed;
            if (St0 <= 0) { State = St.Swim; _head = A + Sign() * Rand(0.7f, 2.0f); }
        }
        if (Contr > 0.3f || Stun > 0) v *= 0.1f;
        if (S.Move != Motion.Sessile)
        {
            float wob = S.Move == Motion.Ciliate ? MathF.Sin(Roll) * 0.1f : S.Move == Motion.Flagellate ? MathF.Sin(Roll) * 0.12f : 0;
            Sw.Update(this, dt, _head + wob, v, maxW, acc, tacc);
            Mem.Bend = Sw.Bend(L * LenK, Wd * WidK, Math.Max(Speed, 1), S.Armor ? 0.03f : 0.2f);
        }
        // poza kadrem odżywianie liczone co 4 klatki (statystycznie to samo, 4× taniej)
        long t0 = World.Prof ? World.TS : 0;
        if (W.OnScreen(this)) Feed(dt);
        else if ((W.Frame + Id) % 4 == 0) Feed(dt * 4);
        if (World.Prof) { World.PAdd("p:feed", t0); t0 = World.TS; }
        Digest(dt);
        Compute();
        if (World.Prof) World.PAdd("p:compute", t0);
    }

    void Mouth(out float mx, out float my) => ToWorld(L * LenK * 0.42f, S.Groove ? Wd * 0.12f : 0, out mx, out my);

    void Feed(float dt)
    {
        if (Contr > 0.5f) return;
        Mouth(out float mx, out float my);
        if (S.BactFeed > 0 && W.Bact != null)
        {
            int n = W.Bact.Feed(mx, my, S.BactFeed, S.BactFeed * 0.2f, 120 * dt, null);
            if (n > 0) { Energy += n * 0.01f; Hp = Math.Min(1, Hp + n * 0.002f); }
        }
        if (S.Suck > 0 && S.Eats != null)
        {
            float rs = Math.Max(S.BactFeed, L * 0.5f);
            W.Query(mx, my, rs, _q);
            foreach (var e in _q)
            {
                if (e == this || !e.Alive || e.Fixed || e.Holder != null || !S.Eats(this, e)) continue;
                float dx = mx - e.X, dy = my - e.Y, d = Hypot(dx, dy);
                if (d > rs || d < 1e-3f) continue;
                float front = (-dx * MathF.Cos(A) - dy * MathF.Sin(A)) / d;
                if (front < 0.1f) continue;
                float k = Math.Min(0.5f, S.Suck * (1 - d / rs) * dt / d);
                e.X += dx * k; e.Y += dy * k;
                if (d < e.Radius + L * 0.12f && Gut.Count < 5) Engulf(e);
            }
        }
        foreach (var o in Nb)
        {
            if (!o.Alive || o.Holder != null) continue;
            if (S.Contract > 0 && !o.Fixed && o.Kind != Kind.None && !(S.Eats != null && S.Eats(this, o)) && o.Mass > Mass * 0.05f) ContractNow();
            if (S.Eats != null && Gut.Count < 4 && S.Eats(this, o) && o.State is not (St.Cup or St.Vac or St.Caught or St.Eaten))
            {
                float dx = o.X - X, dy = o.Y - Y, d = Math.Max(Hypot(dx, dy), 1e-4f);
                if ((dx * MathF.Cos(A) + dy * MathF.Sin(A)) / d > 0.2f || S.Move == Motion.Walk) Engulf(o);
            }
            else if (S.Victim != null && S.Victim(this, o))
            {
                bool wasAlive = o.Alive;
                if (o.IsPlayer) W.LogOnce("bite" + S.Id, $"{S.Name} ({S.Latin}) atakuje! Toksycysty i gryzienie – uciekaj, Twoja błona jest uszkadzana.", "bad", 10);
                o.Damage(S.Bite * dt);
                if (wasAlive && !o.Alive) { Energy += 1; Hp = 1; }
            }
        }
    }

    void Engulf(Ent o)
    {
        o.State = St.Cup; o.Solid = false; o.Holder = this;
        if (o is Paramecium pm) pm.Captor = this;
        ToLocal(o.X, o.Y, out float lx, out float ly);
        float ang = MathF.Atan2(ly, lx), rin = Wd * Rand(0.1f, 0.25f);
        Gut.Add(new Vac { O = o, Sx = lx, Sy = ly, Lx = lx, Ly = ly, Tx = MathF.Cos(ang) * rin - L * 0.05f, Ty = MathF.Sin(ang) * rin });
        W.Sfx("gulp", X, Y, 0.5f);
        if (o.IsPlayer) W.Log($"{S.Name} ({S.Latin}) Cię połyka! {(o is Paramecium ? "Spacja – trichocysty, może się uda wyrwać." : "Z wodniczki pokarmowej nie ma ucieczki.")}", "bad");
        Swallowed(this, o);
    }

    /// <summary>Toksyny obronne ofiary uderzają w drapieżnika (np. blefaryzmina, toksyna Spirostomum).</summary>
    public static void Swallowed(Ent pred, Ent prey)
    {
        if (prey is not Protist pr) return;
        pr.ContractNow();
        if (pr.S.Toxin <= 0) return;
        pred.Damage(pr.S.Toxin);
        if (pred.IsPlayer) pred.W.Log($"{pr.S.Name} wydziela {pr.S.ToxinName} – połknięcie uszkadza Twoją błonę!", "bad");
    }

    void Digest(float dt)
    {
        for (int k = Gut.Count - 1; k >= 0; k--)
        {
            var f = Gut[k]; var o = f.O;
            if (!o.Alive || o.Holder != this) { Gut.RemoveAt(k); continue; }
            f.St += dt;
            if (f.Cup)
            {
                float q = Smooth(f.St / 2.5f);
                f.Lx = Lerp(f.Sx, f.Tx, q); f.Ly = Lerp(f.Sy, f.Ty, q);
                if (f.St >= 2.5f) { f.Cup = false; f.St = 0; o.State = St.Vac; }
            }
            else
            {
                o.Dig = Clamp(f.St / S.Dig, 0, 1);
                if (f.St >= S.Dig)
                {
                    Gut.RemoveAt(k); Energy += o.Food; Hp = Math.Min(1, Hp + 0.3f);
                    if (o.IsPlayer) W.Log($"Strawiony w wodniczce pokarmowej {S.Name}.", "bad");
                    o.Alive = false;
                    continue;
                }
            }
            ToWorld(f.Lx, f.Ly, out o.X, out o.Y);
        }
    }

    public void Release(bool escaped)
    {
        for (int k = Gut.Count - 1; k >= 0; k--)
        {
            var f = Gut[k];
            if (!f.Cup || !f.O.Alive) continue;
            f.O.Unhold(); f.O.X += MathF.Cos(A) * L * 0.4f; f.O.Y += MathF.Sin(A) * L * 0.4f;
            Gut.RemoveAt(k);
            if (escaped && f.O.IsPlayer) { W.Log($"Wyrwałeś się z {S.Name}!", "good"); W.AddStat("escapes", 1); }
        }
        Stun = 1.5f; _head = A + PI;
    }

    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (State != St.Swim || AvoidCD > 0 || S.Move == Motion.Sessile) return;
        if (o == _target || (S.Eats != null && S.Eats(this, o)) || (S.Victim != null && S.Victim(this, o))) return;
        State = St.Avoid; St0 = Rand(0.25f, 0.5f); AvoidCD = 1.2f;
    }

    // ------------------------------------------------------------------ rysowanie
    float Rr(float th) => RadialUtil.Interp(_rr, NN, th);
    public static Color Shade(Color c, float k, float ak) => new(Math.Min(1, c.R * k), Math.Min(1, c.G * k), Math.Min(1, c.B * k), Math.Min(1, c.A * ak));
    void Pt(Vector3 g, float k, out float x, out float y) { float r = Rr(g.X) * g.Y * k; x = MathF.Cos(g.X) * r; y = MathF.Sin(g.X) * r; }

    public override void Draw(Painter p, View v)
    {
        var m = Mem;
        bool eaten = State is St.Cup or St.Vac;
        float al = eaten ? (1 - Dig) * 0.9f : 1;
        if (al <= 0.01f) return;
        float oldA = p.Alpha; p.Alpha = al;
        float lk = LenK, hl = L * lk / 2, hw = Wd * WidK / 2;
        m.Normals();
        // rzęski
        if (S.Cilia > 0 && !eaten && Contr < 0.8f)
        {
            float fx = MathF.Cos(A), fy = MathF.Sin(A), cl = S.Cilia, cw = Math.Max(Math.Min(0.9f, S.Cilia * 0.1f), v.Px * 0.8f);
            var cc = Mul(S.Edge, 0.55f);
            int n = m.N;
            for (int i = 0; i < n * 2; i++)
            {
                int a = i >> 1, b = (a + 1) % n; bool h = (i & 1) == 1;
                float px = h ? (m.Px[a] + m.Px[b]) / 2 : m.Px[a], py = h ? (m.Py[a] + m.Py[b]) / 2 : m.Py[a];
                float nx = h ? (m.Nx[a] + m.Nx[b]) / 2 : m.Nx[a], ny = h ? (m.Ny[a] + m.Ny[b]) / 2 : m.Ny[a];
                float ph = 0.65f + 0.5f * MathF.Sin(Cph - i * 0.42f);
                float dx = nx * MathF.Cos(ph) - fx * MathF.Sin(ph), dy = ny * MathF.Cos(ph) - fy * MathF.Sin(ph);
                float l = Hypot(dx, dy); if (l < 1e-6f) continue;
                p.Seg(px, py, px + dx / l * cl, py + dy / l * cl, cw, cc);
            }
        }
        int ns = m.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, ns);
        p.SoftStroke(sh, true, Clamp(Wd * 0.2f, 1.5f, 14), S.Halo);
        Mem.Centroid(out float bcx, out float bcy);
        Span<float> bts = stackalloc float[] { 0f, 0.65f, 1f };
        Span<Color> bcs = stackalloc Color[] { Shade(S.Body, 1.12f, 0.85f), S.Body, Shade(S.Body, 0.8f, 1.2f) };
        p.FillRadial(sh, bcx, bcy, bts, bcs);
        p.Stroke(sh, true, Clamp(Wd * 0.02f, 0.25f, 1.4f), S.Edge);
        if (S.Armor)
        {
            int n2 = m.SmoothInset(p.Shape2, Wd * 0.06f, p.Tx, p.Ty);
            p.Stroke(new ReadOnlySpan<Vector2>(p.Shape2, 0, n2), true, Wd * 0.03f, Rgb(235, 235, 210, 0.4f));
        }
        p.Local(X, Y, A);
        // pancerz z płytek (Coleps)
        if (S.Armor)
        {
            var ac = Rgb(60, 58, 40, 0.45f);
            for (int r = -3; r <= 3; r++)
            {
                var buf = p.Buf2; int kk = 0;
                for (int k = 0; k <= 16; k++) { float t = -0.92f + 1.84f * k / 16; buf[kk++] = new Vector2(t * hl, S.Profile(t) * hw * r / 3.6f); }
                p.Dashed(new ReadOnlySpan<Vector2>(buf, 0, kk), false, Wd * 0.035f, Wd * 0.08f, Wd * 0.03f, ac);
            }
            for (int r = -2; r <= 2; r++) { float t = r * 0.3f, h = S.Profile(t) * hw * 0.95f; p.Line(t * hl, -h, t * hl, h, Wd * 0.03f, ac); }
            for (int k = -1; k <= 1; k++) p.Line(-hl * 0.95f, k * hw * 0.35f, -hl * 1.15f, k * hw * 0.55f, Wd * 0.04f, S.Edge, true);
        }
        // paski pigmentu (Stentor)
        if (S.Stripes)
        {
            var buf = p.Buf2;
            for (int r = -9; r <= 9; r++)
            {
                for (int k = 0; k <= 16; k++) { float t = -0.95f + 1.9f * k / 16; buf[k] = new Vector2(t * hl, S.Profile(t) * hw * r / 10f); }
                p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, 17), false, Math.Max(hw * 0.018f, v.Px), S.StripeC);
            }
        }
        foreach (var g in _gran) { Pt(g, 1, out float x, out float y); p.Dot(x, y, S.GranR, g.Z > 0 ? S.GranA : S.GranB); }
        foreach (var c in _chl) { Pt(c, 1, out float x, out float y); p.Dot(x, y, S.ChlR * c.Z, S.Chl); }
        if (S.ChlN > 0 && S.ChlR * v.S > 2.5f) foreach (var c in _chl) if (c.Z > 1.05f) { Pt(c, 1, out float x, out float y); p.Dot(x - S.ChlR * 0.25f, y - S.ChlR * 0.25f, S.ChlR * 0.35f, S.ChlHi); }
        // makronukleus
        var nucF = Rgb(165, 170, 150, 0.45f); var nucE = Rgb(70, 74, 62, 0.5f);
        if (S.Beads)
        {
            int nb = L > 400 ? 12 : 7; float br = Math.Min(hw * 0.2f, L * 0.013f);
            for (int k = 0; k < nb; k++) { float t = -0.45f + 1.0f * k / (nb - 1), yy = MathF.Sin(k * 1.3f + _seed) * Math.Min(hw * 0.12f, S.Profile(t) * hw * 0.4f); p.FillCircle(t * hl, yy, br, nucF); p.Circle(t * hl, yy, br, Math.Max(v.Px, br * 0.1f), nucE); }
        }
        else if (S.MacroC) p.Arc(-hl * 0.05f, 0, hw * 0.55f, -2.2f, 2.2f, hw * 0.16f, nucF, true);
        else { p.FillEllipse(-hl * 0.05f, 0, hl * 0.18f, hw * 0.35f, 0, nucF); p.Ellipse(-hl * 0.05f, 0, hl * 0.18f, hw * 0.35f, 0, Math.Max(v.Px, hw * 0.02f), nucE); }
        // wodniczka tętnicza
        for (int k = 0; k < S.CV; k++)
        {
            float ph = (W.T * 0.15f + _seed + k * 0.5f) % 1, vr = ph < 0.9f ? Smooth(ph / 0.9f) * hw * 0.3f : (1 - (ph - 0.9f) / 0.1f) * hw * 0.3f;
            float cx = -hl * (0.72f - k * 0.1f), cy = hw * 0.15f * (k == 0 ? 1 : -1);
            vr = Math.Min(vr, L * 0.03f);
            if (vr > v.Px) { p.FillCircle(cx, cy, vr, Rgb(215, 225, 210, 0.45f)); p.Circle(cx, cy, vr, Math.Max(v.Px, vr * 0.08f), Rgb(250, 255, 240, 0.55f)); }
        }
        // wodniczki pokarmowe
        foreach (var f in Gut) p.Circle(f.Lx, f.Ly, f.O.Radius + Math.Max(0.4f, hw * 0.04f), Math.Max(v.Px, hw * 0.03f), f.Cup ? Rgb(235, 240, 220, 0.3f) : Rgb(235, 240, 220, 0.55f));
        // rowek gębowy / peristom z membranellami
        if (S.Groove)
        {
            var buf = p.Buf2;
            for (int k = 0; k <= 12; k++) { float t = k / 12f; buf[k] = new Vector2(hl * (0.95f - t * 0.75f), hw * (0.55f - 0.45f * t)); }
            p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, 13), false, hw * 0.05f, Rgb(40, 44, 36, 0.3f), true);
            for (int k = 0; k < 12; k++) { var q = buf[k]; float ph = MathF.Sin(Cph - k * 0.7f); p.Seg(q.X, q.Y, q.X + hw * 0.08f * ph, q.Y + hw * 0.16f, Math.Max(v.Px, hw * 0.015f), Rgb(40, 40, 30, 0.35f)); }
        }
        // wieniec membranelli na tarczy gębowej (Stentor)
        if (S.OralRing && Contr < 0.6f)
        {
            float w2 = S.Profile(0.98f) * hw;
            for (int k = 0; k < 26; k++)
            {
                float t = k / 25f, y = -w2 + 2 * w2 * t, ph = MathF.Sin(Cph * 1.3f - k * 0.5f);
                p.Seg(hl, y, hl + hw * 0.22f + ph * hw * 0.06f, y + ph * hw * 0.05f, Math.Max(v.Px, hw * 0.03f), Rgb(40, 60, 70, 0.6f));
            }
        }
        // cirry (Euplotes) – "nóżki"
        if (S.Cirri)
        {
            // grzbietowe listewki (Euplotes ma podłużne żeberka na grzbiecie)
            for (int r = -2; r <= 2; r++)
            {
                var rb = p.Buf2;
                for (int k = 0; k <= 10; k++) { float t = -0.8f + 1.6f * k / 10; rb[k] = new Vector2(t * hl, S.Profile(t) * hw * r * 0.3f); }
                p.Stroke(new ReadOnlySpan<Vector2>(rb, 0, 11), false, Math.Max(v.Px, hw * 0.02f), Rgb(60, 62, 50, 0.25f));
            }
            // cirry czołowo-brzuszne: grube, krótkie "nóżki" wystające spod ciała, poruszające się przy chodzeniu
            var cc2 = Rgb(45, 47, 38, 0.6f);
            for (int k = 0; k < 7; k++)
            {
                float t = 0.75f - k * 0.2f, sd = k % 2 == 0 ? 1 : -1, ph = _walking ? MathF.Sin(Cph * 0.6f + k * 1.3f) : 0;
                float by = sd * S.Profile(t) * hw * 0.92f, bx = t * hl;
                p.Line(bx, by, bx - hw * 0.12f + ph * hw * 0.1f, by + sd * hw * 0.22f, hw * 0.06f, cc2, true);
            }
            // cirry poprzeczne i ogonowe z tyłu
            for (int k = 0; k < 5; k++) { float yy = (k - 2) * hw * 0.2f, ph = _walking ? MathF.Sin(Cph * 0.6f + k) * hw * 0.05f : 0; p.Line(-hl * 0.8f, yy, -hl * 1.18f + ph, yy * 1.4f, hw * 0.055f, cc2, true); }
        }
        // wici
        if (S.Flag > 0 && !eaten)
        {
            var buf = p.Buf2;
            for (int f = 0; f < S.Flag; f++)
            {
                bool trail = f == 1;
                float len = S.FlagL * L * (trail ? 1.6f : 1), bx = trail ? hl * 0.6f : hl, amp = len * 0.08f;
                const int K = 18;
                for (int k = 0; k <= K; k++)
                {
                    float t = (float)k / K, d = t * len;
                    float wv = amp * t * MathF.Sin(TAU * t * 1.5f - Cph * 1.4f + f);
                    buf[k] = trail ? new Vector2(bx - d * 0.9f, hw * 0.3f + wv + d * 0.1f) : new Vector2(bx + d * 0.95f, wv + d * 0.1f * MathF.Sin(Cph * 0.3f));
                }
                p.Thin(new ReadOnlySpan<Vector2>(buf, 0, K + 1), false, Math.Max(v.Px * 0.9f, hw * 0.05f), Mul(S.Edge, 0.7f));
            }
        }
        if (S.Eyespot) p.FillEllipse(hl * 0.78f, hw * 0.25f, hw * 0.18f, hw * 0.12f, 0.3f, Rgb(230, 70, 30, 0.9f));
        p.Alpha = oldA;
    }
}

/// <summary>Katalog gatunków (parametry zweryfikowane w literaturze – patrz Scenario.Sources).</summary>
public static class Species
{
    static float Ell(float t) => MathF.Sqrt(Math.Max(0, 1 - t * t));

    // ----------------------------------------------------- staw
    public static readonly Spec Blepharisma = new()
    {
        Id = "blepharisma", Name = "Blepharisma", Latin = "Blepharisma japonicum", L0 = 180, L1 = 330, Aspect = 0.34f,
        Profile = t => Ell(t) * (1 - 0.4f * Math.Max(0, t)) * 1.15f, Speed = 380, Move = Motion.Ciliate,
        Body = Rgb(205, 125, 150, 0.5f), Edge = Rgb(110, 40, 70, 0.7f), GranN = 40, ChlN = 260, ChlR = 1.4f, Chl = Rgb(200, 70, 120, 0.45f), ChlHi = Rgb(255, 200, 220, 0.4f),
        Cilia = 7, Groove = true, CV = 1, Soft = 0.5f, BactFeed = 45, Food = 1.5f, Toxin = 0.3f, ToxinName = "blefaryzminę (różowy barwnik toksyczny dla drapieżników)",
        Eats = (me, o) => o is Chlamy c && c.State == St.Free || o is Protist q && q != me && q.State == St.Swim && q.Length < me.Length * 0.4f && q.S != Stentor,
        Fact = "Blepharisma (200–500 µm) zawdzięcza różową barwę blefaryzminie – barwnik chroni przed UV i jest toksyczny dla drapieżnych orzęsków. Kanibalizm prowadzi do gigantyzmu: olbrzymy zjadają mniejsze osobniki.",
    };
    public static readonly Spec Euglena = new()
    {
        Id = "euglena", Name = "klejnotka", Latin = "Euglena gracilis", L0 = 48, L1 = 85, Aspect = 0.2f,
        Profile = t => MathF.Pow(Math.Max(0, 1 - t * t), t < 0 ? 1.0f : 0.55f), Speed = 68, Move = Motion.Flagellate, Metaboly = 1,
        Body = Rgb(150, 190, 120, 0.45f), Edge = Rgb(30, 70, 25, 0.75f), GranN = 16, GranR = 1.3f, GranA = Rgb(240, 245, 225, 0.6f), GranB = Rgb(240, 245, 225, 0.5f),
        ChlN = 38, ChlR = 2.4f, Chl = Rgb(60, 145, 45, 0.7f), Flag = 1, FlagL = 0.9f, Eyespot = true, CV = 1, Soft = 0.55f, Food = 0.5f,
        Fact = "Euglena (≈ 50–100 µm): wić, czerwona plamka oczna (fototaksja), dziesiątki chloroplastów, ziarna paramylonu. Pływa ≈ 70 µm/s po spirali; zatrzymana zmienia kształt falami skurczu (metabolia, ruch euglenoidalny).",
    };
    public static readonly Spec Spirostomum = new()
    {
        Id = "spirostomum", Name = "Spirostomum", Latin = "Spirostomum ambiguum", L0 = 650, L1 = 1200, Aspect = 0.085f,
        Profile = t => MathF.Pow(Math.Max(0, 1 - MathF.Pow(MathF.Abs(t), 6)), 0.5f) * (t > 0 ? 1 - 0.15f * t : 1), Speed = 320, Move = Motion.Ciliate, Turn = 0.4f,
        Body = Rgb(190, 175, 125, 0.45f), Edge = Rgb(80, 70, 40, 0.7f), GranN = 150, GranR = 1.4f, Cilia = 6, Beads = true, Groove = true, CV = 1,
        Soft = 0.45f, BactFeed = 60, Food = 4, Contract = 0.3f, Toxin = 0.35f, ToxinName = "toksynę obronną",
        Fact = "Spirostomum ambiguum (≈ 1 mm) kurczy się do ~1/3 długości w ok. 5 ms – białka centryna–Sfi1 zapadają się po związaniu Ca²⁺. Skurcz tworzy falę w wodzie; kolonie synchronicznie uwalniają toksyny odstraszające drapieżniki. Makronukleus paciorkowaty.",
    };
    public static readonly Spec Stentor = new()
    {
        Id = "stentor", Name = "trębacz", Latin = "Stentor coeruleus", L0 = 500, L1 = 1100, Aspect = 0.34f,
        Profile = t => (0.08f + 0.92f * MathF.Pow((t + 1) / 2, 2.2f)) * (t > 0.78f ? 1 - 0.4f * Sq((t - 0.78f) / 0.22f) : 1), Speed = 0, Move = Motion.Sessile,
        Body = Rgb(125, 170, 190, 0.36f), Edge = Rgb(30, 70, 90, 0.7f), GranN = 90, Stripes = true, StripeC = Rgb(40, 100, 160, 0.4f), Beads = true, OralRing = true, CV = 1,
        Soft = 0.45f, BactFeed = 110, Suck = 260, Edible = false, Food = 5, Contract = 0.35f,
        Eats = (me, o) => o.Alive && o.State is St.Free or St.Swim or St.Avoid && !o.Fixed && o.Kind != Kind.None && o.Kind != Kind.Bot && o.Length < me.Length * 0.28f && (o is Chlamy || o is Protist || o is Paramecium || o is Rotifer),
        Fact = "Stentor coeruleus – trąbkowaty orzęsek do 2 mm, niebieski od stentoryny. Przytwierdzony stopką, wieńcem membranelli tworzy wir; zjada bakterie, glony, orzęski, a nawet wrotki. Podrażniony kurczy się błyskawicznie w kulkę.",
    };
    public static readonly Spec Coleps = new()
    {
        Id = "coleps", Name = "Coleps", Latin = "Coleps hirtus", L0 = 42, L1 = 65, Aspect = 0.55f,
        Profile = t => MathF.Pow(Math.Max(0, 1 - MathF.Pow(MathF.Abs(t), 3)), 0.5f), Speed = 330, Move = Motion.Ciliate,
        Body = Rgb(170, 175, 140, 0.5f), Edge = Rgb(60, 58, 36, 0.8f), GranN = 30, Armor = true, Cilia = 4, CV = 1, Soft = 0.08f, Food = 0.5f,
        Bite = 0.12f, Sense = 450,
        Victim = (me, o) => o.Alive && o != me && !(o is Protist q && q.S == Coleps) && o.State is St.Swim or St.Avoid or St.Free && !o.Fixed &&
            (o is Paramecium || o is Didinium || o is Rotifer || o is Protist { S.Armor: false } && o.Length > me.Length),
        Fact = "Coleps hirtus (40–65 µm) ma pancerz z wapiennych płytek w 15–16 rzędach. Histofag: stadami atakuje ranne i martwe orzęski, wrotki i skorupiaki, unieruchamiając je toksycystami.",
    };
    public static readonly Spec Euplotes = new()
    {
        Id = "euplotes", Name = "Euplotes", Latin = "Euplotes sp.", L0 = 70, L1 = 140, Aspect = 0.66f,
        Profile = t => MathF.Pow(Math.Max(0, 1 - MathF.Pow(MathF.Abs(t), 2.6f)), 0.5f), Speed = 160, Move = Motion.Walk,
        Body = Rgb(150, 150, 130, 0.42f), Edge = Rgb(45, 45, 35, 0.75f), GranN = 70, Cirri = true, MacroC = true, Groove = true, CV = 1, Soft = 0.2f, BactFeed = 40, Food = 0.8f,
        Eats = (me, o) => o is Chlamy c && c.State == St.Free,
        Fact = "Euplotes (≈ 60–150 µm) to orzęsek „chodzący”: po podłożu kroczy na cirrach – wiązkach 25–50 rzęsek działających jak nóżki. Makronukleus w kształcie litery C.",
    };
    public static readonly Spec Tetrahymena = new()
    {
        Id = "tetrahymena", Name = "Tetrahymena", Latin = "Tetrahymena pyriformis", L0 = 35, L1 = 60, Aspect = 0.55f,
        Profile = t => Ell(t) * (1 - 0.3f * Math.Max(0, t)) * 1.1f, Speed = 250, Move = Motion.Ciliate,
        Body = Rgb(140, 145, 128, 0.42f), Edge = Rgb(40, 44, 36, 0.7f), GranN = 70, GranR = 0.5f, Cilia = 3.5f, Groove = true, CV = 1, Soft = 0.35f, BactFeed = 20, Food = 0.35f,
        Suck = 30, Sense = 60,
        Eats = (me, o) => o.Alive && !o.Fixed && o.Holder == null && (o is EColi e && e.State is St.Run or St.Tumble || o is Bdello b && b.State == St.Swim || o is Microbe mb && mb.Length < 6 || o is Staph),
        Fact = "Tetrahymena (30–70 µm) prądem wody z aparatu gębowego wciąga bakterie – w hodowli ≈ 1600–3900 cząstek na godzinę. Dla bakterii to olbrzym przepływający jak wieloryb.",
    };
    public static readonly Spec Bodo = new()
    {
        Id = "bodo", Name = "Bodo", Latin = "Bodo saltans", L0 = 4, L1 = 8, Aspect = 0.62f, Bend = 0.5f,
        Profile = t => Ell(t) * (1 - 0.2f * Math.Max(0, t)), Speed = 25, Move = Motion.Jump,
        Body = Rgb(150, 150, 135, 0.45f), Edge = Rgb(40, 42, 36, 0.8f), GranN = 12, GranR = 0.18f, Flag = 2, FlagL = 1.1f, CV = 1, Soft = 0.4f, Food = 0.2f, Sense = 18, Dig = 40,
        Eats = (me, o) => o.Alive && !o.Fixed && o.Holder == null && (o is EColi e && e.State is St.Run or St.Tumble && e.L < 3.2f || o is Bdello b && b.State == St.Swim || o is Microbe mb && mb.Length < 3),
        Fact = "Bodo saltans (3–8 µm) – wiciowiec „skaczący”: krótka wić przednia zagarnia bakterie do otworu gębowego, dłuższa ciągnie się z tyłu. Jeden z głównych konsumentów bakterii w wodach słodkich.",
    };
}

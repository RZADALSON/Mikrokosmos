namespace Mikro;

/// <summary>Segment ciała łańcucha (nicienia, nitki sinicy) – osobna kapsuła kolizyjna, rysuje ją właściciel.</summary>
public sealed class ChainSeg : Ent
{
    public readonly Chain Owner; public readonly int Idx; public float Cx0, Cy0;
    public ChainSeg(World w, Chain o, int idx) : base(w, o.X, o.Y, 0)
    {
        Owner = o; Idx = idx; Kind = o.Kind; Layer = o.Layer; Holder = o; FlowK = 0; Soft = o.Soft; Name = o.Name; Latin = o.Latin;
        Mass = o.Mass;
    }
    public float Hl, Rad;
    public override void UpdatePrim() => SetCapsule(Hl, Rad);
    public override void Integrate(float dt) { }
    public override bool Collides(Ent o) => !(o is ChainSeg s && s.Owner == Owner);
    public override void Step(float dt, float gdt) { if (!Owner.Alive) Alive = false; }
    public override void Draw(Painter p, View v) { }
    public override void Kill() => Owner.Kill();
    public override string Info => Owner.Info;
    public override float Length => Owner.Length;
    public override float Radius => Rad;
    public override void Unhold() { }
}

/// <summary>
/// Ciało z wielu segmentów (nicień, nitka sinicy Oscillatoria). Segmenty zderzają się osobno,
/// a łańcuch zbiera ich przesunięcia – ciało wygina się na przeszkodach i przeciska między nimi.
/// </summary>
public sealed class Chain : Ent
{
    public enum Type { Nematode, Oscillatoria }
    public readonly Type T;
    readonly int N; readonly float _seg, _rad, _len, _speed;
    readonly float[] _px, _py;
    readonly ChainSeg[] _segs;
    float _ph, _base, _revT, _dir = 1, _gut;
    readonly float _seed;

    public override float Length => _len;
    public override float Radius => _rad;

    public Chain(World w, Type t, float x, float y, float a, float len) : base(w, x, y, a)
    {
        T = t; _seed = Rand(0, 99); _base = a;
        if (t == Type.Nematode) { Kind = Kind.Metazoan; Name = "nicień"; Latin = "Nematoda (wolno żyjący)"; _rad = len * 0.028f; Soft = 0.25f; _speed = Rand(120, 260); Layer = 3; }
        else { Kind = Kind.Microbe; Name = "sinica"; Latin = "Oscillatoria sp."; _rad = Rand(3.5f, 5f); Soft = 0.05f; _speed = Rand(2, 8); Layer = 1; }
        _len = len;
        N = Math.Clamp((int)(len / Math.Max(_rad * 2.2f, len / 14)), 6, 14);
        _seg = len / N;
        _px = new float[N + 1]; _py = new float[N + 1];
        float c = MathF.Cos(a), s = MathF.Sin(a), bend = Rand(-0.04f, 0.04f), ang = a;
        _px[0] = x; _py[0] = y;
        for (int i = 1; i <= N; i++) { ang += bend + RandN() * 0.05f; _px[i] = _px[i - 1] - MathF.Cos(ang) * _seg; _py[i] = _py[i - 1] - MathF.Sin(ang) * _seg; }
        Solid = false; FlowK = 0; Mass = t == Type.Nematode ? 30 : 60;
        _segs = new ChainSeg[N];
        for (int i = 0; i < N; i++) { _segs[i] = w.Add(new ChainSeg(w, this, i)); }
        _revT = ExpRand(30);
        Sync();
    }
    void Sync()
    {
        float mx = 0, my = 0;
        for (int i = 0; i < N; i++)
        {
            var g = _segs[i];
            g.X = (_px[i] + _px[i + 1]) / 2; g.Y = (_py[i] + _py[i + 1]) / 2; g.A = MathF.Atan2(_py[i] - _py[i + 1], _px[i] - _px[i + 1]);
            float taper = T == Type.Nematode ? Taper((i + 0.5f) / N) : 1;
            g.Rad = _rad * taper; g.Hl = _seg / 2; g.R = _seg / 2 + _rad;
            g.Cx0 = g.X; g.Cy0 = g.Y; g.UpdatePrim();
            mx += g.X; my += g.Y;
        }
        X = mx / N; Y = my / N; R = _len / 2 + _rad * 2;
    }
    static float Taper(float u) => u < 0.08f ? 0.45f + u / 0.08f * 0.55f : u > 0.8f ? Math.Max(0.15f, 1 - (u - 0.8f) / 0.2f * 0.85f) : 1;

    public override void Integrate(float dt) { }
    public override void Step(float dt, float gdt)
    {
        // 1) przesunięcia segmentów z kolizji → punkty ciała
        for (int i = 0; i < N; i++)
        {
            var g = _segs[i]; if (!g.Alive) continue;
            float dx = g.X - g.Cx0, dy = g.Y - g.Cy0;
            if (dx == 0 && dy == 0) continue;
            _px[i] += dx; _py[i] += dy; _px[i + 1] += dx; _py[i + 1] += dy;
        }
        // 2) ruch
        if (T == Type.Nematode)
        {
            _ph += gdt * TAU * Math.Min(2 * W.TimeScale, 2.5f);
            _revT -= dt; if (_revT <= 0) { _base += RandN() * 1.2f; _revT = ExpRand(12); }
            // głowa zatacza łagodny łuk; fala zginania biegnie wzdłuż ciała (pływanie falujące ~2 Hz)
            float cur = MathF.Atan2(_py[0] - _py[1], _px[0] - _px[1]);
            float head = Turn(cur, _base + 0.6f * MathF.Sin(_ph), 3.5f * gdt);
            float v = (W.TimeScale > 5 ? Math.Min(_speed, W.Sc.ViewW * 0.3f / W.TimeScale) : _speed) * dt;
            _px[0] += MathF.Cos(head) * v; _py[0] += MathF.Sin(head) * v;
            if (W.Bact != null) _gut = Math.Min(1, _gut + W.Bact.Feed(_px[0], _py[0], _rad * 3, _rad * 1.2f, 80 * dt, null) * 0.01f);
            _gut = Math.Max(0, _gut - dt * 0.002f);
        }
        else
        {
            // ślizg wzdłuż własnej osi; co jakiś czas zmiana kierunku; koniec nitki się kołysze ("oscillatoria")
            _revT -= dt; if (_revT <= 0) { _dir = -_dir; _revT = ExpRand(40); }
            int h = _dir > 0 ? 0 : N, nb = _dir > 0 ? 1 : N - 1;
            float tx = _px[h] - _px[nb], ty = _py[h] - _py[nb], tl = Math.Max(Hypot(tx, ty), 1e-4f);
            float sway = MathF.Sin(W.T * 0.8f + _seed) * 0.02f;
            float c = MathF.Cos(sway), s = MathF.Sin(sway), ux = (tx * c - ty * s) / tl, uy = (tx * s + ty * c) / tl;
            _px[h] += ux * _speed * dt; _py[h] += uy * _speed * dt;
        }
        // 3) więzy długości (podążanie za liderem)
        if (T == Type.Nematode || _dir > 0) for (int i = 1; i <= N; i++) Link(i - 1, i);
        else for (int i = N - 1; i >= 0; i--) Link(i + 1, i);
        Sync();
    }
    void Link(int a, int b)
    {
        float dx = _px[b] - _px[a], dy = _py[b] - _py[a], d = Hypot(dx, dy);
        if (d < 1e-6f) return;
        // sztywność zginania: kąt między kolejnymi segmentami ograniczony (brak ostrych załamań)
        int c = a + (a - b);
        if (c >= 0 && c <= N)
        {
            float pa = MathF.Atan2(_py[a] - _py[c], _px[a] - _px[c]), na = MathF.Atan2(dy, dx);
            float lim = T == Type.Nematode ? 0.45f : 0.25f, diff = WrapA(na - pa);
            if (MathF.Abs(diff) > lim) { float ang = pa + Math.Sign(diff) * lim; dx = MathF.Cos(ang) * d; dy = MathF.Sin(ang) * d; }
        }
        float k = _seg / d; _px[b] = _px[a] + dx * k; _py[b] = _py[a] + dy * k;
    }
    public override void Kill()
    {
        if (!Alive) return;
        for (int i = 0; i < N; i += 2) W.Fx.Spill(_px[i], _py[i], 4, new FX.SpillOpt { Speed = _rad * 1.5f, R = _rad * 0.12f, Life = 4, R0 = _rad, D = _rad * 0.02f, Cols = T == Type.Nematode ? new[] { Rgb(120, 110, 80, 0.6f), Rgb(230, 230, 210, 0.5f) } : new[] { Rgb(40, 100, 60, 0.7f) } });
        W.Sfx("pop", X, Y, 0.8f);
        foreach (var g in _segs) g.Alive = false;
        Alive = false;
    }

    public override void Draw(Painter p, View v)
    {
        p.Identity();
        var buf = p.Buf; int n = N + 1;
        for (int i = 0; i <= N; i++) buf[i] = new Vector2(_px[i], _py[i]);
        int ns = Painter.SmoothOpen(_px, _py, n, p.Shape, 3);
        var mid = new ReadOnlySpan<Vector2>(p.Shape, 0, ns);
        if (T == Type.Oscillatoria)
        {
            p.SoftStroke(mid, false, _rad * 3.2f, Rgb(255, 255, 240, 0.25f));
            p.Stroke(mid, false, _rad * 2, Rgb(55, 110, 80, 0.75f), true);
            p.Stroke(mid, false, _rad * 1.2f, Rgb(80, 140, 100, 0.6f), true);
            // ściany poprzeczne komórek (co ~3 µm)
            if (_rad * v.S > 1.5f) p.Dashed(mid, false, _rad * 1.9f, Math.Max(0.4f, v.Px), 3, Rgb(30, 70, 50, 0.55f));
            return;
        }
        // nicień: przezroczysta rurka z kutykulą, gardziel z bulwą, jelito
        var outl = p.Shape2; int k = 0;
        for (int i = 0; i < ns; i++)
        {
            var a = p.Shape[Math.Max(0, i - 1)]; var b = p.Shape[Math.Min(ns - 1, i + 1)];
            float tx = b.X - a.X, ty = b.Y - a.Y, tl = Math.Max(Hypot(tx, ty), 1e-5f), w = _rad * Taper((float)i / (ns - 1));
            outl[k++] = new Vector2(p.Shape[i].X - ty / tl * w, p.Shape[i].Y + tx / tl * w);
        }
        for (int i = ns - 1; i >= 0; i--)
        {
            var a = p.Shape[Math.Max(0, i - 1)]; var b = p.Shape[Math.Min(ns - 1, i + 1)];
            float tx = b.X - a.X, ty = b.Y - a.Y, tl = Math.Max(Hypot(tx, ty), 1e-5f), w = _rad * Taper((float)i / (ns - 1));
            outl[k++] = new Vector2(p.Shape[i].X + ty / tl * w, p.Shape[i].Y - tx / tl * w);
        }
        var o = new ReadOnlySpan<Vector2>(outl, 0, k);
        p.SoftStroke(o, true, _rad * 0.6f, Rgb(255, 255, 240, 0.3f));
        // wypełnienie paskami (kształt nie jest gwiaździsty) – czworokąty między brzegami
        Span<Vector2> q = stackalloc Vector2[4];
        var bodyC = Rgb(185, 185, 165, 0.33f);
        for (int i = 0; i < ns - 1; i++)
        {
            q[0] = outl[i]; q[1] = outl[i + 1]; q[2] = outl[k - 2 - i]; q[3] = outl[k - 1 - i];
            p.FillFan(q, (q[0].X + q[2].X) / 2, (q[0].Y + q[2].Y) / 2, bodyC, false);
        }
        p.Stroke(o, true, Math.Max(v.Px, _rad * 0.08f), Rgb(60, 60, 45, 0.7f));
        // jelito i gardziel
        int i0 = ns / 5, i1 = (int)(ns * 0.82f);
        p.Stroke(new ReadOnlySpan<Vector2>(p.Shape, i0, i1 - i0), false, _rad * 0.7f, Rgb(140, 120, 70, 0.3f + 0.35f * _gut), true);
        p.Stroke(new ReadOnlySpan<Vector2>(p.Shape, 1, i0), false, _rad * 0.35f, Rgb(120, 115, 90, 0.5f), true);
        var bulb = p.Shape[i0]; p.FillCircle(bulb.X, bulb.Y, _rad * 0.5f, Rgb(120, 115, 90, 0.5f));
        for (int i = i0; i < i1; i += 3) p.Dot(p.Shape[i].X + MathF.Sin(i * 2.1f) * _rad * 0.2f, p.Shape[i].Y + MathF.Cos(i * 1.7f) * _rad * 0.2f, _rad * 0.12f, Rgb(80, 70, 40, 0.5f));
    }
}

/// <summary>
/// Słonecznica (Actinosphaerium, 200–1000 µm): kulisty pierwotniak z setkami promienistych aksopodiów.
/// Ekstrusomy na aksopodiach przyklejają orzęski, wiciowce i drobne wielokomórkowce; prąd cytoplazmy
/// przenosi zdobycz do ciała, gdzie jest pochłaniana.
/// </summary>
public sealed class Helio : Ent, ICaptor
{
    readonly float _rb, _ax; readonly int _n; readonly float[] _aa, _al; readonly Vector3[] _vac;
    sealed class Catch { public Ent O; public float Ang, D, St; public bool In; }
    readonly List<Catch> _c = new();
    readonly List<Ent> _q = new();
    float _scan, _dir;
    public override float Radius => _rb;
    public Helio(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.Protist; Layer = 2; Name = "słonecznica"; Latin = "Actinosphaerium eichhornii"; Soft = 0.3f;
        _rb = Rand(90, 150); _ax = _rb * Rand(0.9f, 1.3f); R = _rb + _ax + 5; Mass = 200;
        _n = 72; _aa = new float[_n]; _al = new float[_n];
        for (int i = 0; i < _n; i++) { _aa[i] = i * TAU / _n + Rand(-0.03f, 0.03f); _al[i] = _ax * Rand(0.75f, 1.1f); }
        _vac = new Vector3[140];
        for (int i = 0; i < _vac.Length; i++) { float t = Rand(0, TAU), r = Rand(0.55f, 0.95f) * _rb; _vac[i] = new Vector3(MathF.Cos(t) * r, MathF.Sin(t) * r, Rand(4, 9)); }
        _dir = Rand(0, TAU); FlowK = 0.6f;
    }
    public override void UpdatePrim() => SetCircle(_rb * 0.95f);
    bool Prey(Ent o) => o.Alive && o.Holder == null && !o.Fixed && o.State is St.Swim or St.Free or St.Avoid && o.Kind != Kind.None && o.Kind != Kind.Bot
        && (o is Paramecium || o is Chlamy || o is Rotifer || o is Protist { S.Armor: false } && o.Length < _rb * 2.2f && !(o is Protist { S.Move: Motion.Sessile }));
    public override void Step(float dt, float gdt)
    {
        Vx = MathF.Cos(_dir) * 3; Vy = MathF.Sin(_dir) * 3;
        _scan -= dt;
        if (_scan <= 0 && _c.Count < 5)
        {
            _scan = 0.15f;
            W.Query(X, Y, _rb + _ax, _q);
            foreach (var o in _q)
            {
                if (!Prey(o)) continue;
                float dx = o.X - X, dy = o.Y - Y, d = Hypot(dx, dy);
                if (d > _rb + _ax + o.Radius || d < _rb * 0.8f) continue;
                o.State = St.Caught; o.Holder = this; o.Solid = false;
                if (o is Paramecium pm) pm.Captor = this;
                _c.Add(new Catch { O = o, Ang = MathF.Atan2(dy, dx), D = d });
                if (o.IsPlayer) W.Log("Przyklejony do aksopodiów słonecznicy! Ekstrusomy trzymają – Spacja (trichocysty) może pomóc się wyrwać.", "bad");
                break;
            }
        }
        for (int k = _c.Count - 1; k >= 0; k--)
        {
            var c = _c[k]; var o = c.O;
            if (!o.Alive || o.Holder != this) { _c.RemoveAt(k); continue; }
            c.St += dt;
            if (!c.In)
            {
                c.D = Math.Max(_rb * 0.6f, c.D - dt * 6);
                if (c.D <= _rb * 0.85f) { c.In = true; o.State = St.Vac; Protist.Swallowed(this, o); }
            }
            else
            {
                o.Dig = Clamp((c.St - 10) / 90, 0, 1);
                if (o.Dig >= 1) { _c.RemoveAt(k); if (o.IsPlayer) W.Log("Strawiony przez słonecznicę.", "bad"); o.Alive = false; continue; }
            }
            o.X = X + MathF.Cos(c.Ang) * c.D; o.Y = Y + MathF.Sin(c.Ang) * c.D;
            if (o.IsPlayer && !c.In) o.Damage(dt * 0.01f);
        }
    }
    public void Release(bool escaped)
    {
        for (int k = _c.Count - 1; k >= 0; k--)
        {
            var c = _c[k]; if (c.In || !c.O.Alive) continue;
            c.O.Unhold(); c.O.X = X + MathF.Cos(c.Ang) * (_rb + _ax + 20); c.O.Y = Y + MathF.Sin(c.Ang) * (_rb + _ax + 20); c.O.A = c.Ang;
            _c.RemoveAt(k);
            if (escaped && c.O.IsPlayer) { W.Log("Oderwałeś się od aksopodiów!", "good"); W.AddStat("escapes", 1); }
        }
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, 0);
        var ac = Rgb(70, 72, 60, 0.45f); float aw = Math.Max(v.Px * 0.8f, 1.2f);
        for (int i = 0; i < _n; i++)
        {
            float a = _aa[i] + MathF.Sin(W.T * 0.2f + i) * 0.01f, c = MathF.Cos(a), s = MathF.Sin(a);
            p.Seg(c * _rb * 0.9f, s * _rb * 0.9f, c * (_rb + _al[i]), s * (_rb + _al[i]), aw, ac);
            for (int g = 1; g <= 3; g++) { float d = _rb + _al[i] * g / 4f; p.Dot(c * d, s * d, 1.4f, Rgb(40, 40, 30, 0.5f)); }
        }
        p.Glow(0, 0, _rb * 1.15f, Rgb(255, 255, 240, 0.18f));
        p.FillCircle(0, 0, _rb, Rgb(165, 170, 150, 0.35f));
        foreach (var q in _vac) { p.FillCircle(q.X, q.Y, q.Z, Rgb(225, 230, 215, 0.3f)); p.Circle(q.X, q.Y, q.Z, 0.8f, Rgb(70, 74, 60, 0.35f)); }
        p.FillCircle(0, 0, _rb * 0.5f, Rgb(120, 125, 105, 0.3f));
        for (int k = 0; k < 14; k++) { float t = k * 2.4f, r = _rb * 0.35f * MathF.Sqrt((k + 0.5f) / 14); p.FillCircle(MathF.Cos(t) * r, MathF.Sin(t) * r, 3.5f, Rgb(95, 100, 85, 0.5f)); }
        p.Circle(0, 0, _rb, 1.2f, Rgb(50, 52, 40, 0.6f));
        foreach (var c in _c) if (c.In) p.Circle(MathF.Cos(c.Ang) * c.D, MathF.Sin(c.Ang) * c.D, c.O.Radius + 4, 1, Rgb(235, 240, 220, 0.5f));
    }
}

/// <summary>
/// Lacrymaria olor ("łza łabędzia", ciało ~100–400 µm): szyja wysuwa się na 7× (nawet do ~30×) długości ciała,
/// głowa miota się w poszukiwaniu ofiary i wbija toksycysty, po czym szyja się zwija i ofiara jest połykana.
/// </summary>
public sealed class Lacrymaria : Ent, ICaptor
{
    const int NN = 40;
    readonly float _l, _w; readonly float[] _rr = new float[NN];
    float _neck, _neckMax, _hx, _hy, _hvx, _hvy, _cph, _wand, _turnT, _st, _cool;
    Ent _prey; bool _hold; float _dig;
    readonly List<Ent> _q = new();
    public override float Length => _l;
    public override float Radius => _w / 2;
    public Lacrymaria(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Protist; Layer = 3; Name = "Lacrymaria"; Latin = "Lacrymaria olor"; Soft = 0.5f;
        _l = Rand(160, 300); _w = _l * 0.36f; _neckMax = _l * Rand(4, 7); R = _l / 2 + 10; Mass = 20;
        Mem = new Membrane(NN) { MaxDef = _w * 0.25f }; MemK = 7; MemS = 0.5f;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, c = MathF.Cos(th), s = MathF.Sin(th);
            float A2 = _l / 2, B2 = _w / 2 * (c > 0 ? 1 - 0.55f * c * c : 1);
            _rr[i] = A2 * B2 / MathF.Sqrt(Sq(B2 * c) + Sq(A2 * s));
        }
        Mem.Radial(_rr); Mem.Follow(x, y, a, 1, 1);
        _hx = x; _hy = y; _wand = Rand(0, TAU); Edible = true; Food = 1.2f;
    }
    public override void UpdatePrim() => SetCapsule(_l * 0.3f, _w * 0.42f);
    public override bool Collides(Ent o) => o != _prey;
    float NeckBaseX => X + MathF.Cos(A) * _l * 0.5f;
    float NeckBaseY => Y + MathF.Sin(A) * _l * 0.5f;
    bool IsPrey(Ent o) => o.Alive && o != this && o.Holder == null && !o.Fixed && o.State is St.Swim or St.Free or St.Avoid && o.Kind != Kind.None && o.Kind != Kind.Bot
        && (o is Paramecium || o is Chlamy || o is Protist { S.Armor: false, S.Move: not Motion.Sessile } && o.Length < _l * 1.3f);
    public override void Step(float dt, float gdt)
    {
        _cph += gdt * TAU * 4; _cool -= dt;
        if (State is St.Cup or St.Vac or St.Caught) { Vx = Vy = 0; _neck = 0; return; }
        float bx = NeckBaseX, by = NeckBaseY;
        if (_prey != null && (!_prey.Alive || _prey.Holder != this)) { _prey = null; _hold = false; }
        if (_hold)
        {
            // szyja się zwija, ofiara wciągana do cytostomu
            _neck = Math.Max(0, _neck - dt * 60);
            float ang = MathF.Atan2(_hy - by, _hx - bx);
            _hx = bx + MathF.Cos(ang) * _neck; _hy = by + MathF.Sin(ang) * _neck;
            _prey.X = _hx; _prey.Y = _hy;
            if (_neck <= 1)
            {
                _prey.State = St.Vac; _dig += dt;
                ToWorld(_l * 0.05f, 0, out _prey.X, out _prey.Y);
                _prey.Dig = Clamp(_dig / 80, 0, 1);
                if (_dig > 80) { if (_prey.IsPlayer) W.Log("Połknięty i strawiony przez Lacrymarię.", "bad"); _prey.Alive = false; _prey = null; _hold = false; _dig = 0; }
            }
            Vx = Vy = 0;
            return;
        }
        // polowanie: szyja wysuwa się, głowa miota się; ciało pływa powoli
        _st -= dt;
        _turnT -= dt; if (_turnT <= 0) { _wand = A + RandN() * 1.3f; _turnT = Rand(0.6f, 2); }
        Ent tgt = null;
        if (_cool <= 0)
        {
            W.Query(bx, by, _neckMax + 40, _q);
            float bd = float.MaxValue;
            foreach (var o in _q) if (IsPrey(o)) { float d = Hypot(o.X - bx, o.Y - by); if (d < _neckMax && d < bd) { bd = d; tgt = o; } }
        }
        float tx, ty;
        if (tgt != null) { tx = tgt.X; ty = tgt.Y; _neck = Math.Min(_neckMax, _neck + dt * 250); }
        else { _neck = Clamp(_neck + dt * (MathF.Sin(W.T * 0.4f + Id) > -0.2f ? 120 : -180), _l * 0.3f, _neckMax * 0.7f); tx = bx + MathF.Cos(_wand) * _neck; ty = by + MathF.Sin(_wand) * _neck; }
        float dxh = tx - _hx, dyh = ty - _hy, dh = Math.Max(Hypot(dxh, dyh), 1e-3f), sp = tgt != null ? 420 : 160;
        _hx += dxh / dh * Math.Min(dh, sp * dt); _hy += dyh / dh * Math.Min(dh, sp * dt);
        float nd = Hypot(_hx - bx, _hy - by);
        if (nd > _neck) { _hx = bx + (_hx - bx) / nd * _neck; _hy = by + (_hy - by) / nd * _neck; }
        if (tgt != null && Hypot(tgt.X - _hx, tgt.Y - _hy) < tgt.Radius + 6) Strike(tgt);
        A = Turn(A, MathF.Atan2(_hy - Y, _hx - X), 0.4f * dt);
        float v = tgt != null ? 20 : 90;
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
    }
    void Strike(Ent o)
    {
        for (int k = 0; k < 10; k++) W.Fx.NeedleAt(_hx, _hy, Rand(0, TAU), Rand(10, 25), Rgb(255, 245, 210, 0.9f), 1.2f);
        W.Sfx("zap", _hx, _hy);
        o.Damage(0.2f);
        if (!o.Alive) { _cool = 3; return; }
        if (o.Length < _l * 1.3f)
        {
            _prey = o; _hold = true; o.State = St.Caught; o.Holder = this; o.Solid = false;
            if (o is Paramecium pm) pm.Captor = this;
            if (o.IsPlayer) W.Log("Lacrymaria wbiła toksycysty i ciągnie Cię szyją do pyszczka! Spacja – trichocysty!", "bad");
        }
        else _cool = 2;
    }
    public void Release(bool escaped)
    {
        if (_prey == null || _prey.State == St.Vac) return;
        _prey.Unhold(); if (escaped && _prey.IsPlayer) { W.Log("Wyrwałeś się Lacrymarii!", "good"); W.AddStat("escapes", 1); }
        _prey = null; _hold = false; _cool = 4; _neck *= 0.3f;
    }
    public override void Kill() { if (_prey != null && _prey.Alive) _prey.Unhold(); base.Kill(); }
    public override void Draw(Painter p, View v)
    {
        bool eaten = State is St.Cup or St.Vac; float oldA = p.Alpha; if (eaten) p.Alpha = 1 - Dig;
        var m = Mem; m.Normals();
        float bx = NeckBaseX, by = NeckBaseY;
        // szyja – krzywa od ciała do głowy
        if (_neck > 2)
        {
            var buf = p.Buf2; float ang = A, hx = _hx, hy = _hy; const int K = 20;
            float cx = bx + MathF.Cos(ang) * _neck * 0.4f, cy = by + MathF.Sin(ang) * _neck * 0.4f;
            for (int k = 0; k <= K; k++)
            {
                float t = (float)k / K, u = 1 - t, wig = MathF.Sin(t * 9 + W.T * 3) * _neck * 0.02f * t * (1 - t) * 4;
                buf[k] = new Vector2(u * u * bx + 2 * u * t * cx + t * t * hx + wig, u * u * by + 2 * u * t * cy + t * t * hy - wig);
            }
            var s = new ReadOnlySpan<Vector2>(buf, 0, K + 1);
            p.SoftStroke(s, false, 8, Rgb(255, 255, 240, 0.25f));
            p.Stroke(s, false, _w * 0.12f, Rgb(130, 135, 115, 0.5f), true);
            p.Stroke(s, false, _w * 0.04f, Rgb(60, 62, 50, 0.5f), true);
            p.FillCircle(hx, hy, _w * 0.13f, Rgb(120, 125, 105, 0.7f));
            for (int k = 0; k < 10; k++) { float t = k * TAU / 10 + _cph * 0.3f; p.Seg(hx, hy, hx + MathF.Cos(t) * _w * 0.25f, hy + MathF.Sin(t) * _w * 0.25f, Math.Max(v.Px, 0.8f), Rgb(50, 52, 40, 0.5f)); }
        }
        int n = m.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 10, Rgb(255, 255, 240, 0.26f));
        p.FillFan(sh, X, Y, Rgb(140, 145, 125, 0.42f));
        p.Stroke(sh, true, 1, Rgb(40, 44, 36, 0.7f));
        p.Local(X, Y, A);
        var cc = Rgb(50, 54, 44, 0.3f);
        for (int k = -4; k <= 4; k++)
        {
            var buf = p.Buf2;
            for (int j = 0; j <= 12; j++) { float t = -0.95f + 1.9f * j / 12, x = t * _l / 2, hw = _w / 2 * MathF.Sqrt(Math.Max(0, 1 - t * t)) * (t > 0 ? 1 - 0.55f * t * t : 1); buf[j] = new Vector2(x, hw * k / 4.6f + MathF.Sin(t * 3 + k) * 1.5f); }
            p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, 13), false, Math.Max(v.Px, 0.7f), cc);
        }
        p.FillEllipse(-_l * 0.05f, 0, _l * 0.12f, _w * 0.2f, 0, Rgb(165, 170, 150, 0.45f));
        float cvp = (W.T * 0.2f + Id * 0.3f) % 1;
        p.FillCircle(-_l * 0.38f, 0, _w * 0.15f * Smooth(cvp / 0.9f) * (cvp > 0.9f ? 0 : 1) + 0.1f, Rgb(215, 225, 205, 0.45f));
        p.Alpha = oldA;
    }
}

/// <summary>
/// Rozwielitka (Daphnia, 1–3 mm) – skorupiak filtrujący: odnóża tułowiowe tworzą prąd, który wciąga glony, bakterie
/// i orzęski do wielkości pantofelka. Pływa "podskokami" – uderzeniami wielkich czułków II pary.
/// </summary>
public sealed class Daphnia : Ent
{
    readonly float _l, _w; float _hop, _hopT, _dir, _legPh, _heart, _gut, _eatCD;
    readonly List<Ent> _q = new();
    readonly Vector3[] _eggs;
    public override float Length => _l;
    public override float Radius => _w / 2;
    public Daphnia(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Metazoan; Layer = 5; Name = "rozwielitka"; Latin = "Daphnia pulex"; Soft = 0.05f;
        _l = Rand(1300, 2200); _w = _l * 0.62f; R = _l * 0.75f; Mass = 3000; _dir = a; FlowK = 0.2f;
        _eggs = new Vector3[RandI(0, 6)];
        for (int i = 0; i < _eggs.Length; i++) _eggs[i] = new Vector3(Rand(-0.25f, 0.05f), Rand(-0.28f, -0.12f), Rand(0.8f, 1.1f));
    }
    public override void UpdatePrim() => SetCapsule(_l * 0.22f, _l * 0.27f);
    public override void Step(float dt, float gdt)
    {
        _legPh += gdt * TAU * Math.Min(4 * W.TimeScale, 5);
        _heart += gdt * TAU * Math.Min(4 * W.TimeScale, 4);
        _hopT -= dt; _eatCD -= dt;
        if (_hopT <= 0) { _hop = 1; _hopT = Rand(0.4f, 1.1f); _dir += RandN() * 0.35f; }
        _hop = Math.Max(0, _hop - dt * 4);
        A = Turn(A, _dir, 0.5f * dt);
        float v = 900 * _hop * _hop + 40;
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v + 15; // lekkie opadanie
        // prąd filtracyjny między skorupkami (brzuszna strona, przód)
        ToWorld(_l * 0.1f, _w * 0.22f, out float mx, out float my);
        if (W.Bact != null) _gut = Math.Min(1, _gut + W.Bact.Feed(mx, my, 260, 60, 200 * dt, null) * 0.001f);
        W.Query(mx, my, 300, _q);
        foreach (var e in _q)
        {
            if (!e.Alive || e == this || e.Fixed || e.Holder != null || e.Kind is Kind.None or Kind.Bot or Kind.Metazoan) continue;
            if (e.Length > 240 || e.Radius > 70) continue;
            float dx = mx - e.X, dy = my - e.Y, d = Math.Max(Hypot(dx, dy), 1e-3f);
            if (d > 300) continue;
            float k = Math.Min(0.4f, 260 * (1 - d / 300) * dt / d);
            e.X += dx * k; e.Y += dy * k;
            if (d < 70 && _eatCD <= 0)
            {
                _eatCD = 0.3f; _gut = Math.Min(1, _gut + 0.15f);
                if (e.IsPlayer) W.Log("Wciągnięty prądem filtracyjnym rozwielitki – zmielony przez odnóża i połknięty.", "bad");
                W.Fx.Spill(mx, my, 10, new FX.SpillOpt { Speed = 30, R = 2, Life = 2, D = 2, Cols = new[] { Rgb(70, 110, 50, 0.6f) } });
                e.ReleaseHeld(); e.Alive = false;
            }
        }
        _gut = Math.Max(0, _gut - dt * 0.003f);
    }
    // kontur skorupki (widok z boku, x do przodu, y<0 grzbiet) w jednostkach L
    static readonly Vector2[] Carap =
    {
        new(0.20f, -0.27f), new(0.08f, -0.33f), new(-0.08f, -0.35f), new(-0.24f, -0.31f), new(-0.37f, -0.21f), new(-0.45f, -0.09f),
        new(-0.47f, 0.00f), new(-0.42f, 0.11f), new(-0.30f, 0.21f), new(-0.12f, 0.27f), new(0.05f, 0.27f), new(0.16f, 0.21f), new(0.20f, 0.10f), new(0.21f, -0.08f),
    };
    static readonly Vector2[] Head =
    {
        new(0.19f, -0.24f), new(0.27f, -0.27f), new(0.37f, -0.23f), new(0.44f, -0.12f), new(0.45f, 0.00f), new(0.42f, 0.10f), new(0.37f, 0.20f), new(0.33f, 0.30f),
        new(0.29f, 0.26f), new(0.25f, 0.14f), new(0.20f, 0.06f),
    };
    readonly float[] _tx = new float[32], _ty = new float[32];
    int Curve(Painter p, Vector2[] src, float L, Vector2[] outp)
    {
        for (int i = 0; i < src.Length; i++) { _tx[i] = src[i].X * L; _ty[i] = src[i].Y * L; }
        return Painter.SmoothClosed(_tx, _ty, src.Length, outp, 4);
    }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        float L = _l, lw = Math.Max(v.Px * 1.2f, L * 0.0016f);
        var edge = Rgb(95, 82, 45, 0.7f); var shell = Rgb(215, 200, 150, 0.22f);
        // kolec ogonowy (spina)
        p.Line(-0.45f * L, -0.05f * L, -0.78f * L, -0.13f * L, L * 0.012f, Rgb(190, 175, 120, 0.5f), true);
        p.Line(-0.45f * L, -0.05f * L, -0.78f * L, -0.13f * L, lw, edge, true);
        // skorupka
        int n = Curve(p, Carap, L, p.Shape2);
        var cs = new ReadOnlySpan<Vector2>(p.Shape2, 0, n);
        p.SoftStroke(cs, true, L * 0.012f, Rgb(255, 255, 240, 0.3f));
        p.FillFan(cs, -0.12f * L, 0, shell);
        // siateczkowata rzeźba pancerza
        var net = Rgb(120, 105, 60, 0.12f);
        for (float x = -0.42f; x < 0.18f; x += 0.035f)
            for (float y = -0.3f; y < 0.26f; y += 0.035f)
            {
                float ox = (MathF.Round(y / 0.035f) % 2) * 0.0175f;
                float px = (x + ox) * L, py = y * L;
                if (Sq((px + 0.12f * L) / (0.33f * L)) + Sq(py / (0.3f * L)) > 1) continue;
                p.Seg(px - L * 0.01f, py, px + L * 0.01f, py, lw * 0.7f, net);
                p.Seg(px, py - L * 0.01f, px, py + L * 0.01f, lw * 0.7f, net);
            }
        p.Stroke(cs, true, lw * 1.4f, edge);
        // szczecinki na brzusznym brzegu skorupki
        for (int k = 0; k < 18; k++) { float t = k / 17f, x = Lerp(-0.4f, 0.12f, t) * L, y = (0.27f - 0.9f * Sq(t - 0.55f) * 0.3f) * L; p.Seg(x, y, x - L * 0.012f, y + L * 0.02f, lw * 0.6f, Rgb(95, 82, 45, 0.45f)); }
        // komora lęgowa z zarodkami
        foreach (var e in _eggs)
        {
            float ex = e.X * L, ey = (e.Y - 0.02f) * L, r = L * 0.042f * e.Z;
            p.FillEllipse(ex, ey, r * 1.2f, r, 0.3f, Rgb(125, 150, 80, 0.5f));
            p.Ellipse(ex, ey, r * 1.2f, r, 0.3f, lw, Rgb(60, 75, 35, 0.55f));
            p.FillCircle(ex + r * 0.35f, ey - r * 0.2f, r * 0.22f, Rgb(40, 40, 40, 0.6f));
        }
        // postabdomen z pazurkami
        p.Line(-0.36f * L, 0.08f * L, -0.28f * L, 0.2f * L, L * 0.03f, Rgb(185, 170, 115, 0.45f), true);
        p.Line(-0.28f * L, 0.2f * L, -0.22f * L, 0.24f * L, L * 0.008f, Rgb(80, 65, 35, 0.7f), true);
        // odnóża tułowiowe – filtr
        for (int k = 0; k < 5; k++)
        {
            float x = (0.1f - k * 0.075f) * L, ph = MathF.Sin(_legPh - k * 0.9f);
            float x2 = x - 0.03f * L + ph * 0.025f * L, y2 = 0.22f * L;
            p.Line(x, 0.04f * L, x2, y2, L * 0.012f, Rgb(175, 160, 105, 0.28f), true);
            for (int s = 0; s < 6; s++) { float t = 0.3f + s * 0.12f, sx = Lerp(x, x2, t), sy = Lerp(0.04f * L, y2, t); p.Seg(sx, sy, sx - L * 0.03f, sy + L * 0.008f, lw * 0.5f, Rgb(110, 95, 55, 0.35f)); }
        }
        // jelito z uchyłkami wątrobowymi
        var buf = p.Buf; int gn = 0;
        for (int k = 0; k <= 24; k++) { float t = k / 24f; buf[gn++] = new Vector2((0.3f - t * 0.68f) * L, (-0.06f - 0.05f * MathF.Sin(t * PI) + (t > 0.85f ? (t - 0.85f) * 0.9f : 0)) * L); }
        var gut = new ReadOnlySpan<Vector2>(buf, 0, gn);
        p.Stroke(gut, false, L * 0.045f, Rgb((int)Lerp(170, 70, _gut), (int)Lerp(160, 120, _gut), (int)Lerp(90, 40, _gut), 0.5f), true);
        p.Stroke(gut, false, lw, Rgb(80, 70, 40, 0.5f), true);
        p.Arc(0.33f * L, -0.13f * L, 0.03f * L, PI * 0.3f, PI * 1.6f, L * 0.02f, Rgb((int)Lerp(170, 70, _gut), (int)Lerp(160, 120, _gut), 60, 0.45f), true);
        // serce
        float hb = 0.85f + 0.15f * MathF.Sin(_heart);
        p.FillEllipse(0.13f * L, -0.22f * L, 0.035f * L * hb, 0.022f * L * hb, 0.3f, Rgb(215, 150, 125, 0.55f));
        // głowa
        int hn = Curve(p, Head, L, p.Shape);
        var hs = new ReadOnlySpan<Vector2>(p.Shape, 0, hn);
        p.FillFan(hs, 0.33f * L, 0, Rgb(215, 200, 150, 0.25f));
        p.Stroke(hs, true, lw * 1.3f, edge);
        // oko złożone i przyoczko
        float ex0 = 0.385f * L, ey0 = -0.06f * L, er = 0.05f * L;
        p.Glow(ex0, ey0, er * 1.6f, Rgb(0, 0, 0, 0.25f));
        p.FillCircle(ex0, ey0, er, Rgb(18, 18, 22, 0.95f));
        for (int k = 0; k < 12; k++) { float t = k * TAU / 12 + W.T * 0.2f; p.FillCircle(ex0 + MathF.Cos(t) * er * 0.92f, ey0 + MathF.Sin(t) * er * 0.92f, er * 0.2f, Rgb(200, 215, 230, 0.55f)); }
        p.FillCircle(0.33f * L, 0.05f * L, 0.012f * L, Rgb(20, 20, 20, 0.9f));
        // czułki I pary pod dziobkiem
        p.Line(0.33f * L, 0.28f * L, 0.36f * L, 0.33f * L, L * 0.01f, Rgb(160, 145, 95, 0.5f), true);
        // czułki II pary – wiosła z długimi szczecinami
        float row = MathF.Sin(Math.Min(1, _hop) * PI);
        float bx = 0.24f * L, by = -0.2f * L, a0 = -PI * 0.72f + row * 0.9f;
        float mx = bx + MathF.Cos(a0) * 0.2f * L, my = by + MathF.Sin(a0) * 0.2f * L;
        p.Line(bx, by, mx, my, L * 0.03f, Rgb(185, 170, 115, 0.5f), true);
        p.Line(bx, by, mx, my, lw, edge, true);
        for (int br = 0; br < 2; br++)
        {
            float a1 = a0 + (br == 0 ? -0.35f : 0.3f) - row * 0.3f;
            float fx = mx + MathF.Cos(a1) * 0.2f * L, fy = my + MathF.Sin(a1) * 0.2f * L;
            p.Line(mx, my, fx, fy, L * 0.016f, Rgb(185, 170, 115, 0.5f), true);
            p.Line(mx, my, fx, fy, lw * 0.8f, edge, true);
            for (int s = 0; s < 4; s++)
            {
                float t = 0.35f + s * 0.22f, sx = Lerp(mx, fx, t), sy = Lerp(my, fy, t), sa = a1 - 1.9f + row * 0.6f;
                p.Line(sx, sy, sx + MathF.Cos(sa) * 0.18f * L, sy + MathF.Sin(sa) * 0.18f * L, lw * 0.9f, Rgb(120, 105, 60, 0.4f));
                for (int f = 1; f < 6; f++) { float u = f / 6f, qx = sx + MathF.Cos(sa) * 0.18f * L * u, qy = sy + MathF.Sin(sa) * 0.18f * L * u; p.Seg(qx, qy, qx + MathF.Cos(sa + 0.9f) * L * 0.012f, qy + MathF.Sin(sa + 0.9f) * L * 0.012f, lw * 0.4f, Rgb(120, 105, 60, 0.3f)); }
            }
        }
    }
}

/// <summary>Toczek (Volvox, 150–500 µm): kolonia tysięcy komórek z wiciami na powierzchni kuli, w środku kolonie potomne; toczy się.</summary>
public sealed class Volvox : Ent
{
    readonly float _r; readonly Vector3[] _cells; readonly Vector3[] _kids; float _rot, _dir;
    public override float Radius => _r;
    public Volvox(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.Protist; Layer = 2; Name = "toczek"; Latin = "Volvox aureus"; Soft = 0.3f; Edible = false;
        _r = Rand(110, 240); R = _r + 8; Mass = 60; _dir = Rand(0, TAU);
        _cells = new Vector3[360];
        for (int i = 0; i < _cells.Length; i++) { float z = Rand(-1, 1), t = Rand(0, TAU), q = MathF.Sqrt(1 - z * z); _cells[i] = new Vector3(q * MathF.Cos(t), q * MathF.Sin(t), z); }
        _kids = new Vector3[RandI(3, 8)];
        for (int i = 0; i < _kids.Length; i++) { float t = Rand(0, TAU), q = Rand(0.1f, 0.55f); _kids[i] = new Vector3(MathF.Cos(t) * q, MathF.Sin(t) * q, Rand(0.12f, 0.24f)); }
    }
    public override void UpdatePrim() => SetCircle(_r);
    public override void Step(float dt, float gdt)
    {
        _rot += dt * 1.2f; _dir += RandN() * 0.1f * MathF.Sqrt(dt);
        Vx = MathF.Cos(_dir) * 110; Vy = MathF.Sin(_dir) * 110;
    }
    public override void OnTouch(Ent o, float nx, float ny) => _dir = MathF.Atan2(-ny, -nx) + RandN() * 0.4f;
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, 0);
        p.SoftStroke(new ReadOnlySpan<Vector2>(p.Buf2, 0, p.CirclePts(0, 0, _r, p.Buf2)), true, 12, Rgb(255, 255, 240, 0.28f));
        p.FillCircle(0, 0, _r, Rgb(170, 200, 140, 0.18f));
        foreach (var k in _kids)
        {
            float kr = k.Z * _r;
            p.FillCircle(k.X * _r, k.Y * _r, kr, Rgb(60, 140, 50, 0.55f));
            p.Circle(k.X * _r, k.Y * _r, kr, 1, Rgb(30, 80, 25, 0.6f));
            for (int i = 0; i < 16; i++) { float t = i * TAU / 16 + _rot; p.Dot(k.X * _r + MathF.Cos(t) * kr * 0.7f, k.Y * _r + MathF.Sin(t) * kr * 0.7f, kr * 0.12f, Rgb(40, 110, 35, 0.6f)); }
        }
        float c = MathF.Cos(_rot), s = MathF.Sin(_rot), cr = Math.Max(_r * 0.022f, v.Px * 0.8f);
        foreach (var q in _cells)
        {
            float y = q.Y * c - q.Z * s, z = q.Y * s + q.Z * c;
            float al = z > 0 ? 0.75f : 0.3f;
            p.Dot(q.X * _r, y * _r, cr, Rgb(50, 130, 40, al));
        }
        p.Circle(0, 0, _r, 1.4f, Rgb(40, 80, 30, 0.5f));
    }
}

/// <summary>
/// Detrytus – kłaczki martwej materii organicznej z bakteriami, grzybnią i ziarnami mineralnymi
/// (brązowo-pomarańczowe skupiska widoczne w każdej kropli stawowej). Miękkie, dryfują z prądem.
/// </summary>
public sealed class Detritus : Ent
{
    readonly Vector3[] _b; readonly Color[] _c; readonly float _r;
    public override float Radius => _r;
    public Detritus(World w, float x, float y, float r) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.None; Layer = 1; Name = "detrytus"; Latin = "martwa materia organiczna z bakteriami"; Soft = 0.8f;
        _r = r; R = r * 1.3f; Mass = r * r * 0.02f; FlowK = 0.5f; D = 0.2f; Dr = 0.0005f;
        int n = (int)Clamp(r * 0.9f, 20, 90);
        _b = new Vector3[n]; _c = new Color[n];
        for (int i = 0; i < n; i++)
        {
            float t = Rand(0, TAU), q = MathF.Pow(R01(), 0.7f) * r * (0.7f + 0.3f * MathF.Sin(t * 3 + r));
            _b[i] = new Vector3(MathF.Cos(t) * q, MathF.Sin(t) * q, Rand(0.06f, 0.2f) * r);
            float k = R01();
            _c[i] = k < 0.45f ? Rgb(150, 105, 45, Rand(0.35f, 0.6f)) : k < 0.75f ? Rgb(120, 110, 60, Rand(0.3f, 0.5f)) : k < 0.9f ? Rgb(90, 70, 40, 0.55f) : Rgb(235, 225, 190, 0.5f);
        }
    }
    public override void UpdatePrim() => SetCircle(_r * 0.6f);
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        p.Glow(0, 0, _r * 1.2f, Rgb(170, 140, 80, 0.12f));
        for (int i = 0; i < _b.Length; i++) p.Dot(_b[i].X, _b[i].Y, _b[i].Z, _c[i]);
        for (int i = 0; i < _b.Length; i += 7) p.Dot(_b[i].X - _b[i].Z * 0.3f, _b[i].Y - _b[i].Z * 0.3f, _b[i].Z * 0.3f, Rgb(255, 250, 225, 0.35f));
    }
}

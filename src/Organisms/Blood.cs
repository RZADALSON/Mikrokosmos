namespace Mikro;

public static class RadialUtil
{
    public static float Interp(float[] rr, int n, float th)
    {
        float f = th / TAU * n; f = ((f % n) + n) % n;
        int i = (int)f; if (i >= n) i = n - 1;
        int j = (i + 1) % n; float u = f - i;
        return rr[i] * (1 - u) + rr[j] * u;
    }
}

/// <summary>
/// Neutrofil (~12 µm): jądro 3–5-płatowe, ziarnistości, lamellipodium z przodu, uropod z tyłu;
/// pełzanie ~10–25 µm/min; fagocytoza → fagosom → wybuch tlenowy + degranulacja.
/// </summary>
public sealed class Neutrophil : Ent
{
    public const int NN = 64;
    public float R0 = 6, Pol = 0.3f, Vmax = 25f / 60f, Seed;
    public readonly float[] Rr = new float[NN];
    public readonly int MaxPhago = 14;
    public int G = 170, GranLeft;
    public float NetCharge;
    readonly float[] _gth, _grho, _gsz, _gfT; readonly byte[] _gty; readonly int[] _gfu;

    public sealed class Phago { public Staph O; public bool Cup = true; public float St, Sx, Sy, Lx, Ly, Tx, Ty; }
    public readonly List<Phago> Phagos = new();

    sealed class Lobe { public float X, Y, Rx, Ry, Rot; }
    readonly List<Ent> _q = new();

    /// <summary>Śmierć komórki; niestrawione bakterie wydostają się na zewnątrz.</summary>
    public override void Kill() => Die(true);
    public void Die(bool release)
    {
        if (!Alive) return;
        if (!release) foreach (var ph in Phagos) if (ph.O.Alive) { ph.O.Alive = false; }
        if (release)
            foreach (var ph in Phagos) if (ph.O.Alive && ph.O.Dig < 0.6f) { ph.O.State = St.Free; ph.O.Solid = true; ph.O.Dig = 0; ph.O.Holder = null; }
        W.Fx.Lyse(Mem, new FX.LyseOpt { Pieces = 6, Speed = 3, Life = 7, Lw = 0.12f, C = Rgb(40, 44, 40, 0.7f), Halo = Rgb(255, 255, 240, 0.2f), HasHalo = true, Curl = 0.6f, D = 0.02f, Ring = 8, RingCol = Rgb(255, 255, 240) });
        W.Fx.Spill(X, Y, 50, new FX.SpillOpt { Speed = 6, R = 0.15f, Life = 6, R0 = R0 * 0.6f, D = 0.05f, Cols = new[] { Rgb(238, 242, 226, 0.7f), Rgb(30, 34, 30, 0.5f) } });
        W.Sfx("pop", X, Y);
        if (IsPlayer) W.Log(release ? "Leukocydyny S. aureus przebiły błonę – neutrofil ginie, bakterie uciekają z fagosomów!" : "Neutrofil zginął.", "bad");
        Alive = false;
    }
    /// <summary>NEToza: samobójcze wyrzucenie sieci chromatyny, która unieruchamia i zabija bakterie.</summary>
    public void NetOsis()
    {
        W.Add(new Net(W, X, Y, 24));
        W.AddStat("nets", 1);
        W.Sfx("net", X, Y);
        if (IsPlayer) W.Log("NETOZA! Chromatyna z histonami i elastazą wyrzucona na zewnątrz – sieć pułapkuje bakterie. Neutrofil ginie.", "good");
        Die(false);
    }
    readonly List<Lobe> _lobes = new();

    public Neutrophil(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Neutro; Layer = 2; Soft = 0.75f; Name = "neutrofil"; Latin = "granulocyt obojętnochłonny";
        Mem = new Membrane(NN) { MaxDef = 2.2f }; MemK = 5; MemS = 0.45f;
        Mass = 12; R = R0 * 1.75f; Seed = Rand(0, 100);
        int nl = RandI(3, 5);
        float lx = -2.4f, ly = Rand(-1.5f, 1.5f);
        for (int i = 0; i < nl; i++)
        {
            _lobes.Add(new Lobe { X = lx, Y = ly, Rx = Rand(1.2f, 1.7f), Ry = Rand(0.9f, 1.25f), Rot = Rand(0, TAU) });
            float ang = Rand(-1.2f, 1.2f) + (i % 2 == 1 ? 0.8f : -0.8f);
            lx += MathF.Cos(ang) * 1.9f + 0.5f; ly = Clamp(ly + MathF.Sin(ang) * 1.9f, -2.5f, 2.5f);
        }
        _gth = new float[G]; _grho = new float[G]; _gsz = new float[G]; _gty = new byte[G]; _gfu = new int[G]; _gfT = new float[G];
        for (int i = 0; i < G; i++) { _gth[i] = Rand(0, TAU); _grho[i] = MathF.Sqrt(R01()) * 0.72f; _gty[i] = (byte)(Chance(0.55f) ? 1 : 2); _gsz[i] = Rand(0.1f, 0.19f); }
        GranLeft = G;
        ComputeRest();
        Mem.Follow(x, y, a, 1, 1);
    }

    void ComputeRest()
    {
        float t = W.T, A2 = R0 * (1 + 0.26f * Pol), B2 = R0 * (1 - 0.15f * Pol), area = 0;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, c = MathF.Cos(th), s = MathF.Sin(th);
            float r = A2 * B2 / MathF.Sqrt(Sq(B2 * c) + Sq(A2 * s));
            float wb = WrapA(th - PI);
            r += Pol * R0 * 0.2f * Gauss(WrapA(th), 0.95f);
            r -= Pol * R0 * 0.1f * Gauss(wb, 1.0f);
            r += Pol * R0 * 0.36f * Gauss(wb, 0.17f);
            r += R0 * 0.055f * Noise1(th * 2.6f + t * 0.9f + Seed) * (0.35f + 0.65f * Math.Max(0, c));
            r += R0 * 0.025f * Noise1(th * 7 + t * 1.7f + Seed * 2) * Math.Max(0, c);
            Rr[i] = r; area += 0.5f * r * r * (TAU / NN);
        }
        float k = MathF.Sqrt(PI * R0 * R0 / area);
        for (int i = 0; i < NN; i++) Rr[i] *= k;
        foreach (var p in Phagos)
        {
            if (!p.Cup) continue;
            float d = Hypot(p.Lx, p.Ly), ap = MathF.Atan2(p.Ly, p.Lx);
            float need = d + p.O.Rr + 0.4f, wd = Math.Max(0.14f, (p.O.Rr + 0.9f) / Math.Max(d, 1));
            for (int i = 0; i < NN; i++) { float g = Gauss(WrapA(i * TAU / NN - ap), wd); if (g > 0.01f) Rr[i] = Math.Max(Rr[i], Lerp(Rr[i], need, g)); }
        }
        Mem.Radial(Rr);
    }
    public override void UpdatePrim() => SetCircle(R0 * 0.92f);
    public override bool Collides(Ent o) => !(o is Staph s && s.State != St.Free);

    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        bool on = false; float tgtA = A, mag = 0;
        if (IsPlayer) { var I = W.Input; on = I.Mag > 0.05f; tgtA = I.Ang; mag = I.Mag; }
        else
        {
            var b = W.Near(X, Y, 40, e => e is Staph s && s.State == St.Free);
            if (b != null) { on = true; tgtA = MathF.Atan2(b.Y - Y, b.X - X); mag = 0.8f; }
            else if (W.Preview) { on = true; tgtA = 0; mag = 1; }
        }
        if (on) A = Turn(A, tgtA, 1.6f * gdt);
        Pol = Lerp(Pol, on ? 1 : 0.25f, Damp(on ? 1.2f : 0.5f, gdt));
        float fill = (float)Phagos.Count / MaxPhago;
        // leukocydyny S. aureus (np. PVL) uszkadzają błonę neutrofila
        if (!W.Preview)
        {
            W.Query(X, Y, 10, _q); float dmg = 0; int near = 0;
            foreach (var o in _q) if (o is Staph s && s.State == St.Free && !s.Strep) { float d = Hypot(o.X - X, o.Y - Y); if (d < 10) { dmg += 1 - d / 10; near++; } }
            Hp -= dmg * 0.0035f * gdt;
            if (Hp <= 0) { Die(true); return; }
            if (IsPlayer)
            {
                NetCharge = W.Input.ActionHeld ? NetCharge + gdt / 1.5f : Math.Max(0, NetCharge - gdt * 2);
                if (NetCharge >= 1) { NetOsis(); return; }
            }
            if (!IsPlayer && Hp < 0.25f && near > 12) { NetOsis(); return; }
        }
        // strzępki Candida: za duże do fagocytozy – neutrofil atakuje je degranulacją z zewnątrz
        if (!W.Preview) foreach (var o in _q) if (o is CandidaHypha hy && hy.Alive && hy.Near(X, Y, R0 + 2)) { hy.Hurt(gdt * 0.004f); break; }
        float v = Vmax * mag * Smooth(Pol) * (1 - fill * 0.5f) * (0.4f + 0.6f * Hp);
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
        if (Phagos.Count < MaxPhago)
            foreach (var o in Nb)
            {
                if (o is not Staph s || s.State != St.Free) continue;
                ToLocal(o.X, o.Y, out float lx, out float ly);
                float d = Hypot(lx, ly);
                if (d < RadialUtil.Interp(Rr, NN, MathF.Atan2(ly, lx)) + s.Rr + 0.15f) Engulf(s, lx, ly);
                if (Phagos.Count >= MaxPhago) break;
            }
        float gf = 0.3f + 0.7f * GranLeft / G;
        for (int k = Phagos.Count - 1; k >= 0; k--)
        {
            var p = Phagos[k]; var o = p.O;
            if (!o.Alive) { Phagos.RemoveAt(k); continue; }
            p.St += p.Cup ? dt : bdt;
            if (p.Cup)
            {
                float q = Smooth(p.St / 40);
                p.Lx = Lerp(p.Sx, p.Tx, q); p.Ly = Lerp(p.Sy, p.Ty, q);
                if (p.St >= 40)
                {
                    p.Cup = false; p.St = 0; o.State = St.Vac;
                    if (IsPlayer || Lineage) W.AddStat("engulfed", 1);
                    Degranulate(p);
                    if (IsPlayer) W.Log("Fagosom zamknięty. Wybuch tlenowy (NADPH-oksydaza) + fuzja ziarnistości.", "good");
                }
            }
            else
            {
                p.Lx = Lerp(p.Lx, p.Tx * 0.6f - 1, Damp(0.02f, dt)); p.Ly = Lerp(p.Ly, p.Ty * 0.6f, Damp(0.02f, dt));
                o.Dig = Clamp(p.St / (1200 / gf), 0, 1);
                if (o.Dig >= 1)
                {
                    o.Alive = false; Phagos.RemoveAt(k);
                    if (IsPlayer || Lineage) W.AddStat("killed", 1);
                    continue;
                }
            }
            ToWorld(p.Lx, p.Ly, out o.X, out o.Y);
        }
        float jr = MathF.Sqrt(gdt);
        for (int i = 0; i < G; i++)
        {
            if (_gty[i] == 0) continue;
            if (_gty[i] == 3) { _gfT[i] -= gdt; if (_gfT[i] <= 0 || _gfu[i] >= Phagos.Count) _gty[i] = 0; continue; }
            float th = _gth[i] += RandN() * 0.09f * jr;
            float rmax = 0.76f - 0.34f * Pol * Sq(Math.Max(0, MathF.Cos(th)));
            _grho[i] = Clamp(_grho[i] + RandN() * 0.05f * jr, 0.08f, rmax);
        }
        ComputeRest();
    }
    void Engulf(Staph o, float lx, float ly)
    {
        o.State = St.Cup; o.Solid = false; o.Owner = this; o.Holder = this;
        float ang = MathF.Atan2(ly, lx), rin = Rand(0.25f, 0.5f) * R0;
        Phagos.Add(new Phago { O = o, Sx = lx, Sy = ly, Lx = lx, Ly = ly, Tx = MathF.Cos(ang) * rin, Ty = MathF.Sin(ang) * rin });
        if (IsPlayer) W.Log("Kontakt! Receptory (FcγR, CR3) wiążą opsonizowaną bakterię – tworzy się miseczka fagocytarna.", "info");
    }
    void Degranulate(Phago p)
    {
        int n = 0, idx = Phagos.IndexOf(p);
        for (int i = 0; i < G && n < 9; i++)
        {
            if (_gty[i] != 1 && _gty[i] != 2) continue;
            float r = RadialUtil.Interp(Rr, NN, _gth[i]) * _grho[i];
            if (Hypot(MathF.Cos(_gth[i]) * r - p.Lx, MathF.Sin(_gth[i]) * r - p.Ly) < 4.5f) { _gty[i] = 3; _gfu[i] = idx; _gfT[i] = Rand(0.3f, 1.5f); n++; GranLeft--; }
        }
        for (int i = 0; i < G && n < 5; i++) if (_gty[i] == 1 || _gty[i] == 2) { _gty[i] = 3; _gfu[i] = idx; _gfT[i] = Rand(0.3f, 1.5f); n++; GranLeft--; }
    }

    static readonly Color HaloN = Rgb(255, 255, 240, 0.28f), EdgeN = Rgb(36, 40, 36, 0.75f), GranB = Rgb(238, 242, 226, 0.75f), GranD = Rgb(30, 34, 30, 0.55f),
        LobeF = Rgb(70, 72, 95, 0.38f), LobeE = Rgb(40, 42, 58, 0.5f), Strand = Rgb(58, 60, 78, 0.45f);

    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 2.4f, HaloN);
        ToWorld(-1.4f, 0, out float cx, out float cy);
        Span<float> ts = stackalloc float[] { 0f, 0.55f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(96, 102, 94, 0.62f), Rgb(112, 118, 108, 0.52f), Rgb(178, 184, 172, 0.22f) };
        p.FillRadial(sh, cx, cy, ts, cs);
        p.Stroke(sh, true, 0.13f, EdgeN);
        p.Local(X, Y, A);
        for (int i = 0; i < _lobes.Count - 1; i++) p.Line(_lobes[i].X, _lobes[i].Y, _lobes[i + 1].X, _lobes[i + 1].Y, 0.28f, Strand, true);
        foreach (var l in _lobes) { p.FillEllipse(l.X, l.Y, l.Rx, l.Ry, l.Rot, LobeF); p.Ellipse(l.X, l.Y, l.Rx, l.Ry, l.Rot, 0.12f, LobeE); }
        foreach (var ph in Phagos)
        {
            float r = ph.O.Rr + (ph.Cup ? 0.15f : 0.45f);
            if (!ph.Cup) p.FillCircle(ph.Lx, ph.Ly, r, Rgb(210, 225, 200, 0.18f));
            p.Circle(ph.Lx, ph.Ly, r, 0.13f, ph.Cup ? Rgb(235, 240, 220, 0.3f) : Rgb(235, 240, 220, 0.55f));
        }
        for (int i = 0; i < G; i++)
        {
            byte ty = _gty[i]; if (ty == 0) continue;
            float r0 = RadialUtil.Interp(Rr, NN, _gth[i]) * _grho[i];
            float gx = MathF.Cos(_gth[i]) * r0, gy = MathF.Sin(_gth[i]) * r0;
            if (ty == 1) p.Dot(gx, gy, _gsz[i], GranB);
            else if (ty == 2) p.Dot(gx, gy, _gsz[i] * 0.9f, GranD);
            else if (_gfu[i] < Phagos.Count)
            {
                var ph = Phagos[_gfu[i]]; float k = Clamp(1 - _gfT[i] / 1.5f, 0, 1);
                p.Dot(Lerp(gx, ph.Lx, k), Lerp(gy, ph.Ly, k), 0.13f, Rgb(255, 250, 200, 0.9f));
            }
        }
    }
}

/// <summary>Erytrocyt – dysk dwuwklęsły ~7,8 µm (grubość 2,6 µm brzeg / 0,8 µm środek).</summary>
public sealed class RBC : Ent
{
    public float Rr, El;
    public RBC(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.Rbc; Layer = 1; Soft = 0.85f; Name = "erytrocyt";
        Rr = Rand(3.7f, 4.05f); El = Rand(0, 0.08f);
        Mem = new Membrane(36) { MaxDef = 2.4f }; MemK = 3; MemS = 0.85f;
        var rr = new float[36];
        for (int i = 0; i < 36; i++) { float t = i * TAU / 36; rr[i] = Rr * (1 + El * MathF.Cos(2 * t)); }
        Mem.Radial(rr);
        Mass = 2.5f; D = 0.004f; Dr = 0.001f; R = Rr + 0.4f;
        Mem.Follow(x, y, A, 1, 1);
    }
    public override void UpdatePrim() => SetCircle(Rr * 0.9f);
    public override void Step(float dt, float gdt) { Vx = W.Sc.FlowX; Vy = W.Sc.FlowY; }
    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape, 2);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 2.2f, Rgb(255, 255, 240, 0.26f));
        Mem.Centroid(out float cx, out float cy);
        Span<float> ts = stackalloc float[] { 0f, 0.42f, 0.72f, 0.9f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(208, 196, 176, 0.30f), Rgb(178, 156, 140, 0.42f), Rgb(142, 108, 98, 0.62f), Rgb(160, 126, 112, 0.55f), Rgb(196, 168, 150, 0.35f) };
        p.FillRadial(sh, cx, cy, ts, cs);
        p.Stroke(sh, true, 0.12f, Rgb(78, 56, 50, 0.55f));
    }
}

/// <summary>Płytka krwi ~2–3 µm (granulomer + hialomer).</summary>
public sealed class Platelet : Ent
{
    readonly float Rr; readonly float[] _shape = new float[9];
    public Platelet(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.Plt; Layer = 1; Name = "płytka krwi"; Rr = Rand(1.0f, 1.5f); R = Rr + 0.3f; Mass = 0.4f; D = 0.01f; Dr = 0.02f;
        for (int i = 0; i < 9; i++) _shape[i] = Rr * Rand(0.8f, 1.15f);
    }
    public override void Step(float dt, float gdt) { Vx = W.Sc.FlowX; Vy = W.Sc.FlowY; }
    public override void Draw(Painter p, View v)
    {
        p.Local(X, Y, A);
        Span<Vector2> s = stackalloc Vector2[9];
        for (int i = 0; i < 9; i++) { float t = i * TAU / 9; s[i] = new Vector2(MathF.Cos(t) * _shape[i], MathF.Sin(t) * _shape[i]); }
        p.SoftStroke(s, true, 0.9f, Rgb(255, 255, 240, 0.3f));
        p.FillFan(s, 0, 0, Rgb(150, 155, 160, 0.45f));
        p.FillCircle(0.1f, -0.1f, Rr * 0.35f, Rgb(80, 70, 110, 0.55f));
        p.FillCircle(-0.25f, 0.2f, Rr * 0.2f, Rgb(80, 70, 110, 0.55f));
    }
}

/// <summary>Limfocyt ~7–8 µm – duże okrągłe jądro, wąski rąbek cytoplazmy.</summary>
public sealed class Lymphocyte : Ent
{
    readonly float Rr, _seed; float _turnT; readonly float[] _rr = new float[40];
    public Lymphocyte(World w, float x, float y) : base(w, x, y, Rand(0, TAU))
    {
        Kind = Kind.Lympho; Layer = 1; Soft = 0.5f; Name = "limfocyt"; Rr = Rand(3.6f, 4.1f); R = Rr + 0.8f; Mass = 5; D = 0.003f;
        Mem = new Membrane(40) { MaxDef = 1.5f }; MemK = 4; MemS = 0.6f; _seed = Rand(0, 99);
        Shape(); Mem.Follow(x, y, A, 1, 1);
    }
    void Shape()
    {
        float t = W.T;
        for (int i = 0; i < 40; i++) { float th = i * TAU / 40; _rr[i] = Rr * (1 + 0.05f * Noise1(th * 3 + t * 0.4f + _seed) + 0.08f * Gauss(WrapA(th - PI), 0.3f)); }
        Mem.Radial(_rr);
    }
    public override void UpdatePrim() => SetCircle(Rr * 0.9f);
    public override void Step(float dt, float gdt)
    {
        _turnT -= dt;
        if (_turnT <= 0) { A += RandN() * 0.8f; _turnT = ExpRand(90); }
        float v = 6f / 60f;
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
        Shape();
    }
    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape, 2);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        p.SoftStroke(sh, true, 2.2f, Rgb(255, 255, 240, 0.28f));
        p.FillFan(sh, X, Y, Rgb(150, 158, 150, 0.35f));
        p.Stroke(sh, true, 0.12f, Rgb(40, 44, 40, 0.7f));
        p.Local(X, Y, A);
        p.FillCircleGrad(0.3f, 0, Rr * 0.8f, Rgb(80, 84, 104, 0.55f), Rgb(60, 62, 82, 0.6f));
        p.Circle(0.3f, 0, Rr * 0.8f, 0.14f, Rgb(35, 36, 50, 0.5f));
    }
}

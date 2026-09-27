namespace Mikro;

/// <summary>
/// Chmury bakterii (pokarm orzęsków i wrotków) – cząstki w oknie aktywności wokół gracza.
/// Cząstki opuszczające okno trafiają do zapasu (Bact) swojego chunka i wracają przy jego aktywacji.
/// </summary>
public sealed class BactField
{
    readonly World _w;
    public readonly int Max; public int N;
    public readonly float[] X, Y; readonly byte[] _o, _dead;
    const float CELL = 100;
    readonly int _nx, _ny; readonly int[] _head, _next;
    float _ox, _oy, _wx0, _wy0, _wx1, _wy1;
    int _phase, _added; bool _anyDead;
    public BactField(World w, int max)
    {
        _w = w; Max = max; X = new float[max]; Y = new float[max]; _o = new byte[max]; _dead = new byte[max]; _next = new int[max];
        _nx = (int)MathF.Ceiling(w.WinW / CELL) + 2; _ny = (int)MathF.Ceiling(w.WinH / CELL) + 2; _head = new int[_nx * _ny];
        SetBounds(w.WinX0, w.WinY0);
        Rebuild();
    }
    void SetBounds(float x0, float y0)
    {
        _wx0 = x0; _wy0 = y0; _wx1 = x0 + _w.WinW; _wy1 = y0 + _w.WinH;
        _ox = x0 - CELL; _oy = y0 - CELL;
    }
    bool Inside(float x, float y) => x >= _wx0 && x < _wx1 && y >= _wy0 && y < _wy1;
    void Absorb(int i)
    {
        _dead[i] = 1; _anyDead = true;
        if (_w.Infinite) _w.ChunkAt(X[i], Y[i]).Bact += 1;
    }
    /// <summary>Przesunięcie okna: cząstki spoza nowego okna trafiają do zapasów chunków.</summary>
    public void SetWindow(float x0, float y0)
    {
        SetBounds(x0, y0);
        for (int i = 0; i < N; i++) if (_dead[i] == 0 && !Inside(X[i], Y[i])) Absorb(i);
        Compact(); Rebuild();
    }
    public void Cloud(float cx, float cy, float r, int k)
    {
        for (int i = 0; i < k; i++)
        {
            float a = Rand(0, TAU), d = MathF.Abs(RandN()) * r * 0.6f;
            float x = cx + MathF.Cos(a) * d, y = cy + MathF.Sin(a) * d;
            if (!Inside(x, y) || N >= Max) { if (_w.Infinite) _w.ChunkAt(x, y).Bact += 1; continue; }
            X[N] = x; Y[N] = y; _o[N] = (byte)RandI(0, 3); _dead[N] = 0; _next[N] = -1; N++; _added++;
        }
    }
    int CellI(float v, float o, int n) { int i = (int)MathF.Floor((v - o) / CELL); return i < 0 ? 0 : i >= n ? n - 1 : i; }
    void Rebuild()
    {
        Array.Fill(_head, -1);
        for (int i = 0; i < N; i++) { int c = CellI(Y[i], _oy, _ny) * _nx + CellI(X[i], _ox, _nx); _next[i] = _head[c]; _head[c] = i; }
    }
    void Compact()
    {
        if (!_anyDead) return;
        int w = 0;
        for (int i = 0; i < N; i++) if (_dead[i] == 0) { X[w] = X[i]; Y[w] = Y[i]; _o[w] = _o[i]; _dead[w] = 0; w++; }
        N = w; _anyDead = false;
    }
    public void Update(float dt, float gdt)
    {
        bool rebuild = _anyDead;
        Compact();
        // co klatkę porusza się 1/6 cząstek (krok ×6) – ruch Browna i prąd są statystycznie takie same
        const int STRIDE = 6;
        float s = MathF.Sqrt(gdt * STRIDE) * 3, d4 = dt * STRIDE;
        _phase = (_phase + 1) % STRIDE;
        for (int i = _phase; i < N; i += STRIDE)
        {
            _w.FlowAt(X[i], Y[i], out float fx, out float fy);
            X[i] += RandN() * s + fx * d4; Y[i] += RandN() * s + fy * d4;
            if (!Inside(X[i], Y[i])) { if (_w.Infinite) Absorb(i); else { X[i] = Clamp(X[i], _wx0 + 2, _wx1 - 2); Y[i] = Clamp(Y[i], _wy0 + 2, _wy1 - 2); } }
        }
        if (rebuild || _phase == 0 || _added > 0) { Rebuild(); _added = 0; }
    }
    /// <summary>Prąd pokarmowy: wciąga cząstki w promieniu R ku (mx,my), zjada te bliżej niż eatR. Zwraca liczbę zjedzonych.</summary>
    public int Feed(float mx, float my, float R, float eatR, float pull, Func<bool> canEat)
    {
        int eaten = 0; float R2 = R * R;
        int i0 = CellI(mx - R, _ox, _nx), i1 = CellI(mx + R, _ox, _nx), j0 = CellI(my - R, _oy, _ny), j1 = CellI(my + R, _oy, _ny);
        for (int j = j0; j <= j1; j++)
            for (int ci = i0; ci <= i1; ci++)
                for (int k = _head[j * _nx + ci]; k >= 0; k = _next[k])
                {
                    if (_dead[k] != 0) continue;
                    float dx = mx - X[k], dy = my - Y[k], d2 = dx * dx + dy * dy;
                    if (d2 > R2) continue;
                    float d = MathF.Sqrt(d2);
                    if (d < eatR && (canEat == null || canEat())) { _dead[k] = 1; _anyDead = true; eaten++; continue; }
                    float q = Math.Min(pull * (1 - d / R) / Math.Max(d, 1e-3f), 0.9f);
                    X[k] += dx * q; Y[k] += dy * q;
                }
        return eaten;
    }
    /// <summary>Liczba bakterii w pobliżu.</summary>
    public int Density(float x, float y, float r)
    {
        int n = 0; int i0 = CellI(x - r, _ox, _nx), i1 = CellI(x + r, _ox, _nx), j0 = CellI(y - r, _oy, _ny), j1 = CellI(y + r, _oy, _ny);
        for (int j = j0; j <= j1; j++) for (int ci = i0; ci <= i1; ci++) for (int k = _head[j * _nx + ci]; k >= 0; k = _next[k]) if (_dead[k] == 0) n++;
        return n;
    }
    /// <summary>Środek najgęstszej chmury w promieniu (dla wskaźnika pokarmu).</summary>
    public bool Densest(float x, float y, float r, out float bx, out float by)
    {
        bx = by = 0; int best = 0;
        int i0 = CellI(x - r, _ox, _nx), i1 = CellI(x + r, _ox, _nx), j0 = CellI(y - r, _oy, _ny), j1 = CellI(y + r, _oy, _ny);
        for (int j = j0; j <= j1; j++)
            for (int ci = i0; ci <= i1; ci++)
            {
                int n = 0; for (int k = _head[j * _nx + ci]; k >= 0; k = _next[k]) if (_dead[k] == 0) n++;
                if (n > best) { best = n; bx = _ox + (ci + 0.5f) * CELL; by = _oy + (j + 0.5f) * CELL; }
            }
        return best >= 8;
    }
    public void Draw(Painter p, View v)
    {
        p.Identity();
        float sz = Math.Max(1.1f, v.Px * 1.6f);
        var c = Rgb(30, 36, 30, 0.6f);
        int i0 = CellI(v.X0, _ox, _nx), i1 = CellI(v.X1, _ox, _nx), j0 = CellI(v.Y0, _oy, _ny), j1 = CellI(v.Y1, _oy, _ny);
        for (int j = j0; j <= j1; j++)
            for (int ci = i0; ci <= i1; ci++)
                for (int k = _head[j * _nx + ci]; k >= 0; k = _next[k])
                {
                    if (_dead[k] != 0) continue;
                    float x = X[k], y = Y[k];
                    if ((_o[k] & 1) == 1) p.Rect(x - sz, y - sz * 0.45f, sz * 2, sz * 0.9f, c); else p.Rect(x - sz * 0.45f, y - sz, sz * 0.9f, sz * 2, c);
                }
    }
    /// <summary>Rozkład gęstości na minimapie.</summary>
    public void DrawMap(Painter p, Color col)
    {
        for (int j = 0; j < _ny; j++)
            for (int ci = 0; ci < _nx; ci++)
            {
                int n = 0; for (int k = _head[j * _nx + ci]; k >= 0; k = _next[k]) if (_dead[k] == 0) n++;
                if (n > 3) p.Rect(_ox + ci * CELL, _oy + j * CELL, CELL, CELL, Mul(col, Math.Min(1, n / 60f)));
            }
    }
}

/// <summary>Paramecium caudatum (~200 µm): rzęski z falą metachroniczną, rowek gębowy, 2 wodniczki tętniące, trichocysty.</summary>
public sealed class Paramecium : Ent
{
    const int NN = 56;
    public float L, Wd, Sk = 1, Speed, St0, Roll, Cph, AvoidCD, Energy, Div, TrichCD, Hunger;
    public Steer Sw;
    float _head, _spin;
    public int FoodCount;
    public bool GrowBack;
    public ICaptor Captor;
    /// <summary>Paramecium bursaria – zielony, z ~700 endosymbiotycznymi zielenicami Chlorella.</summary>
    public bool Green;
    readonly Vector3[] _zoo;
    readonly float[] _rr = new float[NN];
    sealed class Cv { public float X, Y, Ph; }
    readonly Cv[] _cv;
    public sealed class FoodVac { public float U, Age, R; }
    public readonly List<FoodVac> Fv = new();
    readonly Vector3[] _gran = new Vector3[110];

    public override float Length => L;
    public override float Radius => Wd / 2;

    public Paramecium(World w, float x, float y, float a, float len = 0, float energy = 0, bool green = false) : base(w, x, y, a)
    {
        Kind = Kind.Para; Layer = 3; Green = green; Soft = 0.35f; Edible = true; Food = green ? 1.2f : 1.6f;
        Name = green ? "pantofelek zielony" : "pantofelek"; Latin = green ? "Paramecium bursaria" : "Paramecium caudatum";
        L = len > 0 ? len : green ? Rand(90, 150) : Rand(190, 235); Wd = L * (green ? 0.42f : 0.27f);
        if (green)
        {
            _zoo = new Vector3[300];
            for (int i = 0; i < _zoo.Length; i++) { float t = Rand(0, TAU), r = MathF.Sqrt(R01()) * 0.9f; _zoo[i] = new Vector3(MathF.Cos(t) * r, MathF.Sin(t) * r, Rand(0.8f, 1.15f)); }
        }
        Mem = new Membrane(NN) { MaxDef = Wd * 0.35f, Stiff = 9, Damping = 0.55f }; MemK = 4; MemS = 0.8f;
        _head = a;
        Speed = Rand(750, 1050); Mass = 25; State = St.Swim; Roll = Rand(0, TAU); Cph = Rand(0, TAU); Energy = energy;
        _cv = new[] { new Cv { X = 0.27f, Y = -0.12f, Ph = Rand(0, 1) }, new Cv { X = -0.27f, Y = -0.1f, Ph = Rand(0, 1) } };
        for (int i = 0; i < 4; i++) Fv.Add(new FoodVac { U = Rand(0, 1), Age = Rand(0, 0.8f), R = Rand(5, 8) });
        for (int i = 0; i < _gran.Length; i++) { float t = Rand(0, TAU), r = MathF.Sqrt(R01()) * 0.8f; _gran[i] = new Vector3(MathF.Cos(t) * r, MathF.Sin(t) * r, Chance(0.5f) ? 1 : 0); }
        ComputeRest(); Mem.Follow(x, y, a, 1, 1);
    }
    void ComputeRest()
    {
        float A2 = L / 2 * Sk, B2 = Wd / 2 * Sk;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, c = MathF.Cos(th), s = MathF.Sin(th);
            float r = A2 * B2 / MathF.Sqrt(Sq(B2 * c) + Sq(A2 * s));
            if (!Green) r += L * 0.035f * Sk * Gauss(WrapA(th - PI), 0.22f);
            r -= Wd * 0.14f * Sk * Gauss(WrapA(th - 0.75f), 0.32f);
            if (Div > 0) r *= 1 - 0.82f * Div * Gauss(c, 0.2f);
            _rr[i] = r;
        }
        Mem.Radial(_rr);
        R = L / 2 * Sk + 14;
    }
    public override void UpdatePrim()
    {
        if (State is St.Eaten or St.Cup or St.Vac) { SetCircle(1); return; }
        SetCapsule(Math.Max(0, (L / 2 - Wd / 2) * Sk * 0.95f), Wd / 2 * Sk * 0.92f);
    }
    public override bool Collides(Ent o) => State is not (St.Eaten or St.Cup or St.Vac) && !(o is Didinium d && d.Prey == this) && o.Holder != this;
    public override bool CanControl() => State == St.Swim || State == St.Avoid;
    public override St FreeState => St.Swim;
    public override void Unhold() { base.Unhold(); Captor = null; if (Sk < 1) GrowBack = true; }
    public override void Kill() => Die();

    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        Cph += gdt * TAU * (State == St.Caught ? 1.5f : State == St.Eaten ? 0 : 5);
        foreach (var c in _cv) c.Ph = (c.Ph + dt / 8) % 1;
        TrichCD -= gdt; AvoidCD -= dt;
        for (int i = Fv.Count - 1; i >= 0; i--)
        {
            var v = Fv[i];
            v.U = (v.U + dt / 45) % 1; v.Age += bdt / 1500;
            if (v.Age >= 1 && !W.Preview)
            {
                Fv.RemoveAt(i); Energy += 1;
                ToWorld(-L * 0.32f * Sk, Wd * 0.18f * Sk, out float ex, out float ey);
                W.Fx.Spill(ex, ey, 5, new FX.SpillOpt { Speed = 20, R = 1.2f, Life = 4, D = 2, Cols = new[] { Rgb(60, 60, 45, 0.6f) } });
            }
        }
        // schwytany: nadal można wystrzelić trichocysty i szarpać się
        if (IsPlayer && W.Input.Action && TrichCD <= 0 && State is St.Caught or St.Cup) Discharge();
        if (State is St.Eaten or St.Caught or St.Cup or St.Vac)
        {
            Vx = Vy = 0; Sw.V = 0; Sw.Omega = 0;
            if (State is St.Caught or St.Cup) Mem.Bend = MathF.Sin(W.T * 9) * 0.6f * Wd / Sq(L / 2) * (IsPlayer && W.Input.Mag > 0.05f ? 1 : 0.4f);
            ComputeRest(); return;
        }
        if (IsPlayer && W.Sc.Starve > 0 && !W.Preview && W.Sc.Id == "para")
        {
            Hunger += gdt / W.Sc.Starve;
            if (Hunger >= 1) { W.Log("Śmierć głodowa – zabrakło pokarmu (bakterii).", "bad"); Die(); return; }
        }
        float v2 = Speed, maxW = IsPlayer ? 4.5f : 3f;
        // obrót wokół długiej osi (~1 obrót/s) → tor spiralny: w rzucie kurs lekko się kołysze
        _spin += dt * TAU * (0.4f + 0.8f * Math.Abs(Sw.V) / Speed);
        float wob = MathF.Sin(_spin) * 0.1f;
        if (State == St.Divide)
        {
            Div += bdt / 1800;
            if (Div >= 1) { Split(); return; }
            v2 = Speed * 0.35f;
            if (IsPlayer) { var I = W.Input; if (I.Mag > 0.05f) _head = I.Ang; v2 *= I.Mag > 0.05f ? I.Mag : 0; }
            maxW = 1.2f;
        }
        else if (State == St.Avoid)
        {
            // reakcja unikania: rzęski biją wstecz, potem obrót w nowym kierunku (płynny, nie skokowy)
            St0 -= dt; v2 = -0.45f * Speed;
            if (St0 <= 0) { State = St.Swim; _head = A + Sign() * Rand(0.7f, 2.0f); }
        }
        else if (IsPlayer)
        {
            var I = W.Input; bool on = I.Mag > 0.05f;
            if (on) _head = I.Ang;
            v2 = Speed * (on ? I.Mag : 0) * (I.Boost ? 1.7f : 1);
            if (I.Action && TrichCD <= 0) Discharge();
        }
        else
        {
            Roll += dt * TAU * 0.9f;
            _head += MathF.Sin(Roll) * 0.5f * dt + RandN() * 0.3f * MathF.Sqrt(dt);
        }
        Sw.Update(this, dt, _head + (Math.Abs(Sw.V) > Speed * 0.2f ? wob : 0), v2, maxW, 3.2f, IsPlayer ? 6 : 4);
        Mem.Bend = Sw.Bend(L * Sk, Wd * Sk, Speed);
        Feed(dt);
        if (Energy >= 6 && State == St.Swim && !W.Preview) { State = St.Divide; Energy = 0; if (IsPlayer) W.Log("Podział poprzeczny (amitoza makronukleusa, mitoza mikronukleusa) – nadal możesz płynąć, ale wolniej.", "good"); }
        ComputeRest();
    }
    void Feed(float dt)
    {
        var B = W.Bact; if (B == null) return;
        ToWorld(L * 0.02f * Sk, Wd * 0.2f * Sk, out float mx, out float my);
        int n = B.Feed(mx, my, 55, 9, 160 * dt, null);
        if (n <= 0) return;
        for (int i = 0; i < n; i++)
        {
            FoodCount++;
            if (FoodCount % 20 == 0) Fv.Add(new FoodVac { U = 0.05f, Age = 0, R = Rand(6, 8.5f) });
        }
        Hunger = Math.Max(0, Hunger - n * 0.006f); Hp = Math.Min(1, Hp + n * 0.0003f);
        if (IsPlayer) W.AddStat("bact", n);
    }
    void Split()
    {
        float c = MathF.Cos(A), s = MathF.Sin(A), off = L * 0.27f;
        Paramecium Mk(float sg)
        {
            var e = W.Add(new Paramecium(W, X + c * off * sg, Y + s * off * sg, A, L * Rand(0.92f, 1.0f), 0, Green));
            e.Sw.V = Sw.V; e._head = _head;
            e.Lineage = Lineage || IsPlayer; e.Sk = 0.62f; e.GrowBack = true; return e;
        }
        var A1 = Mk(1); Mk(-1);
        Alive = false;
        if (Lineage || IsPlayer) W.AddStat("pop", 1);
        if (IsPlayer) { W.Player = null; W.SetPlayer(A1); W.Log("Podział zakończony – dwa pantofelki. Kontrolujesz przedni (proter).", "good"); }
    }
    public override void Post(float dt, float gdt)
    {
        if (GrowBack && Sk < 1) { Sk = Math.Min(1, Sk + gdt * 0.05f); if (Sk >= 1) GrowBack = false; }
    }
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (State != St.Swim || AvoidCD > 0) return;
        if (o is Didinium d && d.Prey == this) return;
        if (IsPlayer)
        {
            // gracz: uderzenie przodem hamuje rzęski (depolaryzacja) – bez automatycznego cofania
            float front = nx * MathF.Cos(A) + ny * MathF.Sin(A);
            if (front > 0.6f) { Sw.V *= 0.4f; AvoidCD = 0.4f; }
            return;
        }
        State = St.Avoid; St0 = Rand(0.25f, 0.5f); AvoidCD = 1.2f;
    }
    public void Die()
    {
        if (!Alive) return;
        ReleaseHeld();
        W.Fx.Lyse(Mem, new FX.LyseOpt { Pieces = 5, Speed = 25, Life = 6, Lw = 1.2f, C = Rgb(40, 46, 40, 0.7f), Halo = Rgb(255, 255, 240, 0.2f), HasHalo = true, Curl = 0.5f, D = 2 });
        W.Fx.Spill(X, Y, 40, new FX.SpillOpt { Speed = 40, R = 2, Life = 5, R0 = Wd * 0.4f, D = 3, Cols = new[] { Rgb(60, 64, 52, 0.6f), Rgb(220, 225, 205, 0.5f) } });
        W.Sfx("pop", X, Y);
        Alive = false;
    }
    public void Discharge()
    {
        var m = Mem; m.Normals();
        for (int i = 0; i < m.N; i++) W.Fx.NeedleAt(m.Px[i], m.Py[i], MathF.Atan2(m.Ny[i], m.Nx[i]) + RandN() * 0.15f, Rand(18, 34), Rgb(235, 240, 225, 0.85f), Rand(2, 3.5f));
        TrichCD = 6;
        if (IsPlayer) W.Log("Trichocysty wystrzelone! (wydłużają się ~8× w kilka ms)", "info");
        W.Sfx("zap", X, Y);
        if ((State == St.Caught || State == St.Cup) && Captor != null && Chance(IsPlayer ? 0.55f : 0.25f)) Captor.Release(true);
    }

    static readonly Color CiliaC = Rgb(60, 66, 58, 0.38f), HaloC = Rgb(255, 255, 240, 0.26f), BodyC = Rgb(122, 130, 114, 0.42f), EdgeC = Rgb(38, 44, 38, 0.68f),
        TrichC = Rgb(40, 46, 40, 0.28f), GranD = Rgb(45, 52, 45, 0.35f), GranB = Rgb(230, 236, 220, 0.35f),
        ZooC = Rgb(70, 150, 45, 0.62f), ZooH = Rgb(200, 240, 150, 0.45f);

    public override void Draw(Painter p, View v)
    {
        var m = Mem; float sk = Sk, L2 = L * sk, W2 = Wd * sk;
        bool eaten = State is St.Eaten or St.Cup or St.Vac;
        float al = eaten ? (1 - Dig) * 0.85f : 1;
        if (al <= 0.01f) return;
        float oldA = p.Alpha; p.Alpha = al;
        m.Normals();
        if (!eaten)
        {
            float fx = MathF.Cos(A), fy = MathF.Sin(A), cl = 9 * sk, cw = Math.Max(0.9f, v.Px * 0.8f);
            int n = m.N;
            for (int i = 0; i < n * 2; i++)
            {
                int a = i >> 1, b = (a + 1) % n; bool h = (i & 1) == 1;
                float px = h ? (m.Px[a] + m.Px[b]) / 2 : m.Px[a], py = h ? (m.Py[a] + m.Py[b]) / 2 : m.Py[a];
                float nx = h ? (m.Nx[a] + m.Nx[b]) / 2 : m.Nx[a], ny = h ? (m.Ny[a] + m.Ny[b]) / 2 : m.Ny[a];
                float ph = 0.65f + 0.5f * MathF.Sin(Cph - i * 0.42f);
                float dx = nx * MathF.Cos(ph) - fx * MathF.Sin(ph), dy = ny * MathF.Cos(ph) - fy * MathF.Sin(ph);
                float l = Hypot(dx, dy); if (l < 1e-6f) continue;
                p.Seg(px, py, px + dx / l * cl, py + dy / l * cl, cw, CiliaC);
            }
        }
        int ns = m.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, ns);
        p.SoftStroke(sh, true, 11, HaloC);
        p.FillFan(sh, X, Y, BodyC);
        p.Stroke(sh, true, 1.0f, EdgeC);
        int n2 = m.SmoothInset(p.Shape2, 2.4f * sk, p.Tx, p.Ty);
        p.Dashed(new ReadOnlySpan<Vector2>(p.Shape2, 0, n2), true, 2.4f * sk, 0.8f * sk, 1.4f * sk, TrichC);
        p.Local(X, Y, A);
        if (Green)
        {
            foreach (var z in _zoo) p.Dot(z.X * L2 * 0.46f, z.Y * W2 * 0.43f, 2.7f * z.Z * sk, ZooC);
            foreach (var z in _zoo) if (z.Z > 1.05f) p.Dot(z.X * L2 * 0.46f - 0.6f, z.Y * W2 * 0.43f - 0.6f, 1.0f * sk, ZooH);
        }
        else foreach (var g in _gran) p.Rect(g.X * L2 * 0.45f, g.Y * W2 * 0.42f, g.Z > 0 ? 1.4f : 1.2f, g.Z > 0 ? 1.4f : 1.2f, g.Z > 0 ? GranD : GranB);
        // rowek gębowy
        var buf = p.Buf2;
        float rc = MathF.Cos(_spin); // obrót wokół długiej osi – rowek przechodzi z jednej strony na drugą
        for (int k = 0; k < 11; k++) { float t = k / 10f, u = 1 - t; buf[k] = new Vector2(u * u * L2 * 0.44f + 2 * u * t * L2 * 0.25f + t * t * L2 * 0.02f, (u * u * W2 * 0.1f + 2 * u * t * W2 * 0.33f + t * t * W2 * 0.2f) * rc); }
        var groove = new ReadOnlySpan<Vector2>(buf, 0, 11);
        p.Stroke(groove, false, 2.4f * sk, Rgb(40, 46, 40, 0.4f), true);
        p.Stroke(groove, false, 1f * sk, Rgb(230, 236, 220, 0.3f), true);
        // makronukleus + mikronukleus
        p.FillEllipse(-L2 * 0.03f, -W2 * 0.02f, L2 * 0.14f, W2 * 0.22f, 0.15f, Rgb(168, 175, 158, 0.42f));
        p.Ellipse(-L2 * 0.03f, -W2 * 0.02f, L2 * 0.14f, W2 * 0.22f, 0.15f, 1, Rgb(78, 84, 74, 0.45f));
        p.FillCircle(L2 * 0.07f, -W2 * 0.16f, 3.2f * sk, Rgb(62, 66, 58, 0.6f));
        // wodniczki pokarmowe (cykloza)
        foreach (var fv in Fv)
        {
            float x = MathF.Cos(fv.U * TAU) * L2 * 0.3f, y = MathF.Sin(fv.U * TAU) * W2 * 0.26f, d = Clamp(fv.Age, 0, 1);
            p.FillCircle(x, y, fv.R * sk, new Color(Lerp(60, 185, d) / 255f, Lerp(64, 170, d) / 255f, Lerp(52, 120, d) / 255f, Lerp(0.65f, 0.35f, d)));
            p.Circle(x, y, fv.R * sk, 0.8f, Rgb(235, 240, 225, 0.35f));
        }
        // wodniczki tętniące z kanalikami
        foreach (var c in _cv)
        {
            float x = c.X * L2, y = c.Y * W2 * MathF.Cos(_spin), ph = c.Ph;
            float canal = ph < 0.55f ? Smooth(ph / 0.5f) : Math.Max(0, 1 - (ph - 0.55f) / 0.2f);
            float vr = ph < 0.4f ? 0 : ph < 0.9f ? Smooth((ph - 0.4f) / 0.5f) * 8 * sk : (1 - (ph - 0.9f) / 0.1f) * 8 * sk;
            if (canal > 0.02f)
                for (int k = 0; k < 8; k++)
                {
                    float t = k * TAU / 8, rl = 17 * sk * (0.6f + canal * 0.4f);
                    p.Line(x + MathF.Cos(t) * 3, y + MathF.Sin(t) * 3, x + MathF.Cos(t) * rl, y + MathF.Sin(t) * rl, 1.6f * sk, Rgb(225, 235, 220, 0.45f * canal), true);
                }
            if (vr > 0.3f) { p.FillCircle(x, y, vr, Rgb(215, 225, 210, 0.45f)); p.Circle(x, y, vr, 0.8f, Rgb(250, 255, 240, 0.55f)); }
        }
        p.Alpha = oldA;
    }
}

/// <summary>Didinium nasutum (~60–200 µm): "beczułka" z ryjkiem, 2 wieńce rzęsek, toksycysty; połyka pantofelka w całości.</summary>
public sealed class Didinium : Ent, ICaptor
{
    const int NN = 48;
    public float L, Wd, Speed, St0, Swell = 1, Mouth, Stun, Roll, Cph, Hunger, Cyst, TurnT;
    public int Meals;
    public Paramecium Prey;
    public Steer Sw;
    float _psx, _psy, _psa, _head;
    readonly float[] _rr = new float[NN];
    readonly Vector3[] _gran = new Vector3[70];

    public override float Length => L;

    public Didinium(World w, float x, float y, float a, float len = 0) : base(w, x, y, a)
    {
        Kind = Kind.Didi; Layer = 3; Soft = 0.35f; Edible = true; Food = 1.2f; Name = "Didinium"; Latin = "Didinium nasutum";
        L = len > 0 ? len : Rand(100, 125); Wd = L * 0.78f;
        Mem = new Membrane(NN) { MaxDef = L * 0.12f, Stiff = 9, Damping = 0.55f }; MemK = 4; MemS = 0.7f;
        _head = a;
        Speed = Rand(650, 800); Mass = 18; State = St.Swim;
        Roll = Rand(0, TAU); Cph = Rand(0, TAU);
        for (int i = 0; i < _gran.Length; i++) { float t = Rand(0, TAU), r = MathF.Sqrt(R01()) * 0.78f; _gran[i] = new Vector3(MathF.Cos(t) * r, MathF.Sin(t) * r, Chance(0.5f) ? 1 : 0); }
        ComputeRest(); Mem.Follow(x, y, a, 1, 1);
    }
    void ComputeRest()
    {
        float sw = MathF.Sqrt(Swell), cy = Smooth(Cyst);
        float A2 = L / 2 * sw * Lerp(1, 0.78f, cy), B2 = Wd / 2 * sw * Lerp(1, 0.98f, cy); const float pw = 2.6f;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, c = MathF.Abs(MathF.Cos(th)), s = MathF.Abs(MathF.Sin(th));
            float r = MathF.Pow(MathF.Pow(c / A2, pw) + MathF.Pow(s / B2, pw), -1f / pw);
            r += L * 0.15f * (1 - cy) * Gauss(WrapA(th), 0.2f + Mouth * 0.5f) * (1 - Mouth * 0.4f);
            _rr[i] = r;
        }
        Mem.Radial(_rr);
        R = L * 0.62f * sw + 16;
    }
    public override void UpdatePrim() { float sw = MathF.Sqrt(Swell); SetCapsule(L * 0.08f * sw, Wd / 2 * sw * 0.95f); }
    public override bool Collides(Ent o) => o != Prey;
    public override bool CanControl() => State != St.Cyst;

    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        Cph += gdt * TAU * (State == St.Cyst ? 0 : 5);
        Stun -= gdt;
        if (State == St.Cyst) { Cyst = Math.Min(1, Cyst + gdt / 2.5f); Vx = Vy = 0; ComputeRest(); return; }
        if (State is St.Cup or St.Vac) { Vx = Vy = 0; ComputeRest(); return; }
        if ((IsPlayer || Lineage) && !W.Preview)
        {
            Hunger += gdt / W.Sc.Starve * (State == St.Digest ? 0 : 1);
            if (Hunger >= 1 && IsPlayer)
            {
                State = St.Cyst; W.Log("Głód → encystacja. Didinium otacza się ścianą cysty.", "bad");
                W.After(2.6f, () => W.GameOver("Encystacja z głodu"));
                return;
            }
        }
        var P = Prey;
        if (State == St.Swim)
        {
            float v = Speed;
            if (IsPlayer)
            {
                var I = W.Input; bool on = I.Mag > 0.05f;
                if (on) _head = I.Ang;
                v = Speed * (on ? I.Mag : 0) * (I.Boost ? 1.5f : 1);
            }
            else
            {
                Roll += dt * TAU * 1.1f;
                // Didinium trafia ofiary głównie przypadkowymi zderzeniami – słaba chemotaksja z bliska, tylko z przodu
                var t = W.Near(X, Y, 250, e => e is Paramecium q && (q.State == St.Swim || q.State == St.Avoid));
                if (t != null) { float ta = MathF.Atan2(t.Y - Y, t.X - X); if (MathF.Abs(WrapA(ta - A)) < 1.6f) _head = Turn(_head, ta, 0.6f * dt); }
                _head += MathF.Sin(Roll) * 0.6f * dt + RandN() * 0.4f * MathF.Sqrt(dt);
                TurnT -= dt; if (TurnT <= 0) { _head += RandN() * 1.2f; TurnT = ExpRand(3); }
            }
            if (Stun > 0) v *= 0.2f;
            Sw.Update(this, dt, _head, v, 3f, 3.2f, 4);
            Mem.Bend = Sw.Bend(L, Wd, Speed, 0.12f);
        }
        else if (State == St.Capture)
        {
            Vx = Vy = 0; St0 += gdt;
            if (P == null || !P.Alive) { State = St.Swim; Prey = null; }
            else if (St0 > 1.0f)
            {
                State = St.Swallow; St0 = 0; P.State = St.Eaten; _psx = P.X; _psy = P.Y; _psa = P.A;
                if (IsPlayer) W.Log("Połykanie! Cytostom rozszerza się – ofiara większa od drapieżnika wchodzi w całości.", "good");
                W.Sfx("gulp", X, Y);
                if (P.IsPlayer) W.Log("Didinium Cię połyka...", "bad");
            }
        }
        else if (State == St.Swallow)
        {
            Vx = Vy = 0; St0 += gdt;
            float p = Smooth(St0 / 4.5f);
            Mouth = MathF.Sin(Math.Min(1, St0 / 4.5f) * PI);
            float preyArea = P.L * P.Wd * 0.8f, myArea = L * Wd * 0.8f;
            Swell = 1 + preyArea / myArea * 0.55f * p;
            ToWorld(0, 0, out float tx, out float ty);
            ToWorld(L * 0.5f, 0, out float fx, out float fy);
            float q = Math.Min(1, p * 1.4f);
            P.X = Lerp(Lerp(_psx, fx, Math.Min(1, p * 3)), tx, q); P.Y = Lerp(Lerp(_psy, fy, Math.Min(1, p * 3)), ty, q);
            P.A = _psa + WrapA(A - _psa) * p;
            P.Sk = Lerp(1, 0.46f, p);
            if (St0 >= 4.5f)
            {
                State = St.Digest; St0 = 0; Mouth = 0;
                if (P.IsPlayer) P.Alive = false;
                if (IsPlayer) { W.AddStat("eaten", 1); Hunger = 0; }
                Hp = 1;
            }
        }
        else if (State == St.Digest)
        {
            St0 += bdt;
            float p = St0 / 2400;
            float v = Speed * 0.35f;
            if (IsPlayer) { var I = W.Input; bool on = I.Mag > 0.05f; if (on) _head = I.Ang; v *= on ? I.Mag : 0; }
            Sw.Update(this, dt, _head, v, 1.5f, 2.5f, 3);
            if (P != null && P.Alive) { ToWorld(0, 0, out P.X, out P.Y); P.A = A; P.Dig = Smooth(p); }
            float preyArea = P != null ? P.L * P.Wd * 0.8f : 0;
            Swell = 1 + preyArea / (L * Wd * 0.8f) * 0.55f * (1 - Smooth(p));
            if (p >= 1)
            {
                if (P != null) P.Alive = false;
                Prey = null; State = St.Swim; Meals++;
                if ((IsPlayer || Lineage) && Meals % 2 == 0) Divide();
            }
        }
        ComputeRest();
    }
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (State != St.Swim || Stun > 0 || o is not Paramecium pr || (pr.State != St.Swim && pr.State != St.Avoid)) return;
        float front = nx * MathF.Cos(A) + ny * MathF.Sin(A);
        if (front < 0.55f) return;
        State = St.Capture; St0 = 0; Prey = pr; pr.State = St.Caught; pr.Captor = this; pr.Holder = this;
        ToWorld(L * 0.58f, 0, out float fx, out float fy);
        for (int k = 0; k < 14; k++) W.Fx.NeedleAt(fx, fy, A + RandN() * 0.35f, Rand(20, 45), Rgb(255, 245, 210, 0.9f), 1.6f);
        W.Sfx("zap", fx, fy);
        if (IsPlayer) W.Log("Toksycysty wbite w ofiarę! Pantofelek sparaliżowany.", "good");
        if (pr.IsPlayer) W.Log("TOKSYCYSTY! Didinium Cię trafił – wystrzel trichocysty (Spacja)!", "bad");
        if (!pr.IsPlayer && Chance(0.9f)) W.After(0.15f, () => { if (pr.Alive && pr.State == St.Caught) pr.Discharge(); });
    }
    public void Release(bool escaped)
    {
        var P = Prey;
        if (P != null) { P.State = St.Swim; P.Captor = null; P.Holder = null; P.A = A + Rand(-0.6f, 0.6f); P.X += MathF.Cos(A) * 25; P.Y += MathF.Sin(A) * 25; }
        bool wasPlayerPrey = P != null && P.IsPlayer;
        Prey = null; State = St.Swim; Stun = 1.2f; _head = A + PI + RandN() * 0.5f;
        if (escaped) { W.Log("Ofiara wyrwała się dzięki trichocystom!", IsPlayer ? "bad" : "good"); if (wasPlayerPrey) W.AddStat("escapes", 1); }
    }
    void Divide()
    {
        float c = MathF.Cos(A), s = MathF.Sin(A);
        var e = W.Add(new Didinium(W, X - c * L * 0.6f, Y - s * L * 0.6f, A + Rand(-1, 1), L));
        e.Lineage = true;
        W.AddStat("pop", 1);
        if (IsPlayer) W.Log("Podział! Twoja linia Didinium rośnie.", "good");
    }

    public override void Draw(Painter p, View v)
    {
        var m = Mem; float sw = MathF.Sqrt(Swell), L2 = L * sw, W2 = Wd * sw, cy = Smooth(Cyst);
        int ns = m.Smooth(p.Shape);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, ns);
        p.SoftStroke(sh, true, 11, Rgb(255, 255, 240, 0.26f));
        p.FillFan(sh, X, Y, Rgb(128, 124, 104, 0.45f));
        p.Stroke(sh, true, 1.0f, Rgb(46, 44, 34, 0.7f));
        if (cy > 0) p.Stroke(sh, true, 6 * cy, Rgb(200, 190, 150, 0.7f * cy));
        p.Local(X, Y, A);
        float A2 = L2 / 2, B2 = W2 / 2;
        foreach (var g in _gran) p.Rect(g.X * A2, g.Y * B2, g.Z > 0 ? 1.6f : 1.3f, g.Z > 0 ? 1.6f : 1.3f, g.Z > 0 ? Rgb(48, 46, 36, 0.35f) : Rgb(235, 230, 210, 0.3f));
        p.Arc(-L2 * 0.04f, 0, B2 * 0.45f, 0.9f, TAU - 0.9f, 8 * sw, Rgb(78, 76, 62, 0.45f), true);
        float cvp = (W.T * 0.12f + Id * 0.37f) % 1;
        p.FillCircle(-A2 * 0.72f, B2 * 0.1f, 7 * Smooth(cvp / 0.9f) * (cvp > 0.9f ? 0 : 1) + 0.1f, Rgb(215, 225, 205, 0.4f));
        if (cy > 0.6f) return;
        float[] bands = { A2 * 0.36f, -A2 * 0.18f };
        Span<Vector2> band = stackalloc Vector2[2];
        for (int bi = 0; bi < 2; bi++)
        {
            float x = bands[bi], half = B2 * MathF.Sqrt(Math.Max(0, 1 - MathF.Pow(MathF.Abs(x) / A2, 2.6f))) * 0.98f;
            band[0] = new(x, -half); band[1] = new(x, half);
            p.Dashed(band, false, 3, 2, 2.2f, Rgb(50, 48, 38, 0.45f));
            var cc = Rgb(60, 58, 46, 0.5f); float cw = Math.Max(1, v.Px);
            for (int sd = -1; sd <= 1; sd += 2)
                for (int k = 0; k < 7; k++)
                {
                    float ph = 0.5f * MathF.Sin(Cph - k * 0.6f + bi);
                    float ang = sd * (PI / 2 + 0.35f + ph * 0.6f) + (k - 3) * 0.05f * sd;
                    float bx = x + (k - 3) * 1.4f, by = sd * half;
                    p.Seg(bx, by, bx + MathF.Cos(ang) * 16, by + MathF.Sin(ang) * 16, cw, cc);
                }
        }
        p.FillEllipse(A2 + L * 0.08f * (1 - Mouth * 0.4f), 0, 6 + Mouth * 14, 6 + Mouth * 20, 0, Rgb(245, 240, 220, 0.25f));
        var buf = p.Buf2; float mx = A2 + L * 0.13f;
        for (int k = 0; k <= 8; k++) { float t = k / 8f, u = 1 - t; buf[k] = new Vector2(u * u * mx + 2 * u * t * (A2 - 6) + t * t * mx, u * u * (-3 - Mouth * 16) + t * t * (3 + Mouth * 16)); }
        p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, 9), false, 1.4f, Rgb(40, 38, 30, 0.55f), true);
    }
}

/// <summary>Chlamydomonas (~10 µm): 2 wici (styl "żabka"), kubkowaty chloroplast z pirenoidem, czerwona plamka oczna; ~100–120 µm/s.</summary>
public sealed class Chlamy : Ent
{
    public float Rr, Speed, Beat, Roll;
    public override float Radius => Rr;
    public Chlamy(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Chlamy; Layer = 3; Latin = "Chlamydomonas"; Rr = Rand(4.2f, 5.4f); R = Rr + 12; Mass = 1.2f;
        Speed = Rand(90, 130); Beat = Rand(0, TAU); Roll = Rand(0, TAU); D = 0.3f; State = St.Free; Edible = true; Food = 0.3f; Name = "Chlamydomonas";
    }
    public override void UpdatePrim() => SetCircle(Rr);
    public override bool Collides(Ent o) => State == St.Free;
    public override void Step(float dt, float gdt)
    {
        if (State != St.Free) { Vx = Vy = 0; return; }
        Beat += gdt * TAU * Math.Min(6, 50 * W.TimeScale);
        Roll += dt * TAU * 2;
        A += MathF.Sin(Roll) * 1.2f * dt + RandN() * 0.5f * MathF.Sqrt(dt);
        float v = Speed * (0.7f + 0.3f * MathF.Sin(Beat));
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
    }
    public override void OnTouch(Ent o, float nx, float ny) { if (State == St.Free) A += PI * Rand(0.6f, 1.4f); }
    public override void Draw(Painter p, View v)
    {
        float k = Rr / 5.4f, oldA = p.Alpha; p.Alpha = 1 - Dig;
        p.Local(X, Y, A, k);
        if (State == St.Free)
        {
            var buf = p.Buf2; float b = 0.5f + 0.5f * MathF.Sin(Beat);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float a1 = sd * (0.35f + b * 1.5f), bend = sd * (1 - b) * 1.2f;
                float sx = 5.2f, sy = sd * 0.6f, mx = sx + MathF.Cos(a1) * 6, my = sy + MathF.Sin(a1) * 6;
                float ex = mx + MathF.Cos(a1 + bend) * 6, ey = my + MathF.Sin(a1 + bend) * 6;
                for (int j = 0; j <= 8; j++) { float t = j / 8f, u = 1 - t; buf[j] = new Vector2(u * u * sx + 2 * u * t * mx + t * t * ex, u * u * sy + 2 * u * t * my + t * t * ey); }
                p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, 9), false, Math.Max(0.35f, v.Px), Rgb(50, 70, 45, 0.45f));
            }
        }
        p.Glow(0, 0, 8, Rgb(255, 255, 240, 0.25f));
        p.FillEllipse(0, 0, 5.4f, 4.8f, 0, Rgb(170, 190, 150, 0.5f));
        int n = p.EllipsePts(-0.8f, 0, 4.4f, 4.1f, 0, p.Buf2, PI * 0.35f, PI * 1.65f, false);
        p.FillFan(new ReadOnlySpan<Vector2>(p.Buf2, 0, n), -1.5f, 0, Rgb(62, 132, 52));
        p.FillCircle(-2.4f, 0, 1.3f, Rgb(150, 200, 120, 0.8f));
        p.FillEllipse(0.8f, -3.6f, 0.9f, 0.5f, 0.3f, Rgb(220, 50, 30));
        p.FillCircle(1.6f, 0, 1.2f, Rgb(200, 215, 180, 0.6f));
        p.Ellipse(0, 0, 5.4f, 4.8f, 0, 0.3f, Rgb(30, 50, 25, 0.7f));
        p.Alpha = oldA;
    }
}

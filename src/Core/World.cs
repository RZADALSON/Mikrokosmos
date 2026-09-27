namespace Mikro;

public sealed class InputState
{
    public float Dx, Dy, Mag, Ang;
    public bool Boost, Action, ActionHeld, HasAim;
    /// <summary>Pozycja kursora w świecie (µm).</summary>
    public float AimX, AimY;
}

/// <summary>Kwadratowy fragment nieskończonego świata. Poza promieniem aktywności jego organizmy są zamrożone.</summary>
public sealed class Chunk
{
    public readonly int X, Y;
    public readonly float X0, Y0, S;
    public bool Active, Generated;
    public readonly int Biome;
    /// <summary>Zapas bakterii (cząstek) zamrożonych w tym chunku.</summary>
    public float Bact;
    public readonly List<Ent> Parked = new();
    /// <summary>Punkty specjalne chunka (np. źródła antybiotyku).</summary>
    public readonly List<Vector2> Sources = new();
    public Chunk(int x, int y, float s, int biome) { X = x; Y = y; S = s; X0 = x * s; Y0 = y * s; Biome = biome; }
    public float CX => X0 + S / 2;
    public float CY => Y0 + S / 2;
    public bool Contains(float x, float y) => x >= X0 && x < X0 + S && y >= Y0 && y < Y0 + S;
}

/// <summary>Jednorodna siatka przestrzenna w oknie świata (listy łączone w tablicach, bez alokacji na klatkę).</summary>
public sealed class SpatialGrid
{
    float _cell = 1, _x0, _y0; int _nx, _ny;
    int[] _head = Array.Empty<int>(), _next = new int[256];
    Ent[] _items = new Ent[256];
    public bool Ready;

    public void Build(List<Ent> ents, float x0, float y0, float w, float h, float cell)
    {
        int nx = Math.Max(1, (int)MathF.Ceiling(w / cell)), ny = Math.Max(1, (int)MathF.Ceiling(h / cell));
        if (nx * ny != _head.Length) _head = new int[nx * ny];
        _nx = nx; _ny = ny; _cell = cell; _x0 = x0; _y0 = y0;
        Array.Fill(_head, -1);
        if (_items.Length < ents.Count) { _items = new Ent[ents.Count * 2]; _next = new int[ents.Count * 2]; }
        int n = 0;
        foreach (var e in ents)
        {
            if (!e.Alive) continue;
            int c = Cell(e.X, e.Y);
            _items[n] = e; _next[n] = _head[c]; _head[c] = n; n++;
        }
        Ready = true;
    }
    int Ci(float v, float o, int n) { int i = (int)MathF.Floor((v - o) / _cell); return i < 0 ? 0 : i >= n ? n - 1 : i; }
    int Cell(float x, float y) => Ci(y, _y0, _ny) * _nx + Ci(x, _x0, _nx);
    /// <summary>Encje, których środki leżą w komórkach pokrywających kwadrat (x±r, y±r).</summary>
    public void Query(float x, float y, float r, List<Ent> outp)
    {
        outp.Clear();
        int i0 = Ci(x - r, _x0, _nx), i1 = Ci(x + r, _x0, _nx), j0 = Ci(y - r, _y0, _ny), j1 = Ci(y + r, _y0, _ny);
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
                for (int k = _head[j * _nx + i]; k >= 0; k = _next[k]) outp.Add(_items[k]);
    }
}

/// <summary>
/// Świat symulacji. Tryb nieskończony: świat dzielony na chunki; aktywne są te w promieniu AR
/// od gracza, reszta jest zamrożona (organizmy "zaparkowane" w chunkach) lub jeszcze niewygenerowana.
/// </summary>
public sealed class World
{
    public readonly Scenario Sc;
    public readonly float W, H;
    public readonly bool Infinite;
    public readonly float CS;
    public readonly int AR;
    public readonly List<Ent> Ents = new();
    public readonly FX Fx = new();
    public readonly SpatialGrid Grid = new();
    public float T, SimT, TimeScale, Bio, Ff = 1;
    public Ent Player, Focus;
    public bool Over, Preview, ShowField, AllGoals;
    /// <summary>Tryb nanobota (eksploracja): bez celów i końca gry.</summary>
    public bool Nano;
    public int GoalIdx;
    public readonly Dictionary<string, float> Stats = new();
    public InputState Input = new();
    public TileField Nutri, Amp;
    public BactField Bact;
    public int Wave = 1;
    public float CamX, CamY, ViewR = 50;
    public float WinX0, WinY0, WinW, WinH;
    public bool Em => Sc.Em;
    public readonly int Seed = RandI(1, 1 << 29);
    /// <summary>Liczba widocznych fagów w ostatniej klatce (do wyboru poziomu szczegółowości).</summary>
    public int PhagesVisible;

    public Action<string, string> OnLog;
    public Action<string, string> OnBanner;
    public Action<string, bool> OnGameOver;
    public Action<string, float> OnSfx;
    public Action<Scenario.Goal> OnGoal;
    public Action<Chunk> OnEnterBiome;

    public struct Debris { public float X, Y, S, Al, D; public int K; }
    public readonly List<Debris> DebrisList = new();
    public float DebrisTile = 1;

    readonly List<(float t, Action a)> _timers = new();
    readonly List<Ent> _sorted = new(), _near = new();
    readonly Dictionary<long, Chunk> _chunks = new();
    public readonly List<Chunk> Active = new();
    int _ccx = int.MinValue, _ccy = int.MinValue;
    float _scanT;
    Chunk _lastBiomeChunk;

    // pole prądu wody (siatka w oknie, odświeżana co 0,5 s)
    float[] _fx, _fy; const int FN = 48; float _fX0, _fY0, _fW = 1, _fH = 1; float _fAcc = 99; int _dPhase;

    public World(Scenario sc, bool preview = false)
    {
        Sc = sc; Preview = preview;
        Infinite = !preview && sc.ChunkSize > 0;
        CS = sc.ChunkSize; AR = sc.ActiveR;
        W = preview ? 1e6f : Infinite ? (2 * AR + 1) * CS : sc.Wd;
        H = preview ? 1e6f : Infinite ? (2 * AR + 1) * CS : sc.Ht;
        TimeScale = sc.TimeScale; Bio = sc.Bio;
        WinX0 = 0; WinY0 = 0; WinW = W; WinH = H;
    }

    public T0 Add<T0>(T0 e) where T0 : Ent { Ents.Add(e); return e; }
    public void Log(string text, string cls = "info") { if (!Preview) OnLog?.Invoke(text, cls); }
    public void Banner(string t, string s = "") { if (!Preview) OnBanner?.Invoke(t, s); }
    /// <summary>Dźwięk zdarzenia – głośność maleje z odległością od kamery.</summary>
    public void Sfx(string kind, float x, float y, float vol = 1)
    {
        if (Preview || OnSfx == null) return;
        float d = Hypot(x - CamX, y - CamY) / Math.Max(ViewR, 1e-6f);
        if (d > 1.6f) return;
        OnSfx(kind, vol * Clamp(1.2f - d * 0.7f, 0, 1));
    }
    public void After(float delay, Action a) => _timers.Add((T + delay, a));
    readonly Dictionary<string, float> _logT = new();
    /// <summary>Komunikat nie częściej niż co cooldown sekund (dla powtarzalnych zdarzeń).</summary>
    public void LogOnce(string key, string text, string cls, float cooldown = 8)
    {
        if (_logT.TryGetValue(key, out var t) && T - t < cooldown) return;
        _logT[key] = T; Log(text, cls);
    }
    /// <summary>Czy encja jest w pobliżu kadru (poza nim część obliczeń kosmetycznych jest pomijana).</summary>
    public bool OnScreen(Ent e, float k = 1.4f) => Preview || (MathF.Abs(e.X - CamX) < ViewR * k + e.R && MathF.Abs(e.Y - CamY) < ViewR * k * 0.7f + e.R);
    public int Frame;

    public float Stat(string k) => Stats.TryGetValue(k, out var v) ? v : 0;
    public void AddStat(string k, float v) => Stats[k] = Stat(k) + v;

    public int Count(Func<Ent, bool> fn) { int n = 0; foreach (var e in Ents) if (e.Alive && fn(e)) n++; return n; }
    /// <summary>Liczba encji łącznie z zamrożonymi w chunkach (np. cała populacja gracza).</summary>
    public int CountAll(Func<Ent, bool> fn)
    {
        int n = Count(fn);
        foreach (var c in _chunks.Values) foreach (var e in c.Parked) if (e.Alive && fn(e)) n++;
        return n;
    }

    /// <summary>Najbliższa encja spełniająca warunek (siatka przestrzenna dla małych promieni).</summary>
    public Ent Near(float x, float y, float r, Func<Ent, bool> fn)
    {
        Ent best = null; float bd = r * r;
        float gridMax = Infinite ? CS * (AR + 0.5f) : Math.Max(W, H) * 0.3f;
        if (Grid.Ready && r < gridMax)
        {
            Grid.Query(x, y, r, _near);
            foreach (var e in _near)
            {
                if (!e.Alive || !fn(e)) continue;
                float d = (e.X - x) * (e.X - x) + (e.Y - y) * (e.Y - y);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }
        foreach (var e in Ents)
        {
            if (!e.Alive || !fn(e)) continue;
            float d = (e.X - x) * (e.X - x) + (e.Y - y) * (e.Y - y);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }
    public void Query(float x, float y, float r, List<Ent> outp)
    {
        if (Grid.Ready) Grid.Query(x, y, r, outp);
        else { outp.Clear(); outp.AddRange(Ents); }
    }

    public void SetPlayer(Ent e)
    {
        if (Player != null && Player != e) Player.Lineage = true;
        Player = e;
        if (e != null) { e.Lineage = true; Focus = null; if (e.ParkedIn != null) Unpark(e); }
    }
    /// <summary>Gracz zginął – przejmij potomka ze swojej linii (najpierw spośród aktywnych).</summary>
    public bool Adopt(Kind kind, float x, float y)
    {
        var e = Near(x, y, 1e9f, o => o.Kind == kind && o.Lineage && o != Player && o.CanControl());
        if (e == null && Infinite)
        {
            float bd = float.MaxValue;
            foreach (var c in _chunks.Values)
                foreach (var o in c.Parked)
                    if (o.Alive && o.Kind == kind && o.Lineage && o.CanControl()) { float d = Hypot(o.X - x, o.Y - y); if (d < bd) { bd = d; e = o; } }
        }
        if (e != null) { Player = null; SetPlayer(e); Log("Przejmujesz kontrolę nad potomkiem z Twojej linii.", "good"); return true; }
        return false;
    }

    // ================================================================ chunki
    static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    public Chunk GetChunk(int cx, int cy)
    {
        long k = Key(cx, cy);
        if (!_chunks.TryGetValue(k, out var c)) { c = new Chunk(cx, cy, CS, Sc.BiomeAt(this, cx, cy)); _chunks[k] = c; }
        return c;
    }
    public Chunk ChunkAt(float x, float y) => GetChunk(FloorDiv(x, CS), FloorDiv(y, CS));
    public IEnumerable<Chunk> AllChunks => _chunks.Values;
    public int ChunkCount => _chunks.Count;

    /// <summary>Przesuwa okno aktywności do chunka gracza/kamery: zamraża oddalone, odmraża/generuje nowe.</summary>
    public void UpdateChunks(bool force = false)
    {
        if (!Infinite) return;
        var t = CamTarget();
        float px = t != null ? t.X : CamX, py = t != null ? t.Y : CamY;
        int cx = FloorDiv(px, CS), cy = FloorDiv(py, CS);
        var here = GetChunk(cx, cy);
        if (here != _lastBiomeChunk && !Preview)
        {
            if (_lastBiomeChunk == null || _lastBiomeChunk.Biome != here.Biome) OnEnterBiome?.Invoke(here);
            _lastBiomeChunk = here;
        }
        if (!force && cx == _ccx && cy == _ccy) return;
        _ccx = cx; _ccy = cy;
        WinX0 = (cx - AR) * CS; WinY0 = (cy - AR) * CS; WinW = WinH = (2 * AR + 1) * CS;
        // 1) zamrożenie chunków poza promieniem
        var deact = new List<Chunk>();
        foreach (var c in Active) if (Math.Max(Math.Abs(c.X - cx), Math.Abs(c.Y - cy)) > AR) deact.Add(c);
        foreach (var c in deact) { c.Active = false; Active.Remove(c); Sc.OnChunkDeactivated(this, c); }
        if (deact.Count > 0) ParkInactive();
        Bact?.SetWindow(WinX0, WinY0);
        _fAcc = 99;
        // 2) aktywacja (odmrożenie lub wygenerowanie) nowych
        for (int dy = -AR; dy <= AR; dy++)
            for (int dx = -AR; dx <= AR; dx++)
            {
                var c = GetChunk(cx + dx, cy + dy);
                if (c.Active) continue;
                c.Active = true; Active.Add(c);
                foreach (var e in c.Parked) { e.ParkedIn = null; Ents.Add(e); }
                c.Parked.Clear();
                if (!c.Generated) { c.Generated = true; Sc.GenerateChunk(this, c); }
                Sc.OnChunkActivated(this, c);
            }
    }
    /// <summary>Zamraża encje leżące w nieaktywnych chunkach (i wszystko, co w nich siedzi – ofiary, fagi na komórkach).</summary>
    void ParkInactive()
    {
        foreach (var e in Ents)
        {
            if (!e.Alive || e == Player || e == Focus || e.Holder != null || e.ParkedIn != null) continue;
            var c = ChunkAt(e.X, e.Y);
            if (!c.Active) Park(e, c);
        }
        bool again = true;
        while (again)
        {
            again = false;
            foreach (var e in Ents)
                if (e.Alive && e.ParkedIn == null && e.Holder != null && e.Holder.ParkedIn != null && e != Player && e != Focus) { Park(e, e.Holder.ParkedIn); again = true; }
        }
        Ents.RemoveAll(e => !e.Alive || e.ParkedIn != null);
    }
    void Park(Ent e, Chunk c)
    {
        if (!e.Fixed && !e.Lineage && e.Holder == null)
        {
            int dyn = 0; foreach (var o in c.Parked) if (!o.Fixed) dyn++;
            if (dyn >= Sc.ParkCap) { e.Alive = false; return; } // nadmiar rozprasza się
        }
        e.ParkedIn = c; c.Parked.Add(e);
    }
    void Unpark(Ent e) { e.ParkedIn.Parked.Remove(e); e.ParkedIn = null; Ents.Add(e); }

    // ================================================================ prąd wody
    public void FlowAt(float x, float y, out float fx, out float fy)
    {
        fx = fy = 0;
        if (_fx == null) return;
        float gx = Clamp((x - _fX0) / _fW * (FN - 1), 0, FN - 1.001f), gy = Clamp((y - _fY0) / _fH * (FN - 1), 0, FN - 1.001f);
        int i = (int)gx, j = (int)gy; float u = gx - i, v = gy - j;
        int a = j * FN + i, b = a + 1, c = a + FN, d = c + 1;
        fx = (_fx[a] * (1 - u) + _fx[b] * u) * (1 - v) + (_fx[c] * (1 - u) + _fx[d] * u) * v;
        fy = (_fy[a] * (1 - u) + _fy[b] * u) * (1 - v) + (_fy[c] * (1 - u) + _fy[d] * u) * v;
    }
    void UpdateFlow(float gdt)
    {
        if (Sc.Flow <= 0) return;
        _fAcc += gdt;
        if (_fAcc < 0.5f) return;
        _fAcc = 0;
        _fx ??= new float[FN * FN]; _fy ??= new float[FN * FN];
        _fX0 = WinX0 - CS * 0.5f; _fY0 = WinY0 - CS * 0.5f; _fW = WinW + CS; _fH = WinH + CS;
        // ψ = [sin(0.8·k1·x + t)·sin(k1·y + 1.3) + 0.5·sin(k2·y − 1.3t)·sin(0.7·k2·x + 2.1)] / k1 → v = (∂ψ/∂y, −∂ψ/∂x)
        double L = Sc.FlowScale, k1 = Math.PI * 2 / L, k2 = Math.PI * 2 / (L * 0.53), t = SimT * 0.002;
        for (int j = 0; j < FN; j++)
            for (int i = 0; i < FN; i++)
            {
                double x = _fX0 + (double)i * _fW / (FN - 1), y = _fY0 + (double)j * _fH / (FN - 1);
                double a1 = 0.8 * k1 * x + t, b1 = k1 * y + 1.3, a2 = k2 * y - 1.3 * t, b2 = 0.7 * k2 * x + 2.1;
                double dpdy = Math.Sin(a1) * k1 * Math.Cos(b1) + 0.5 * k2 * Math.Cos(a2) * Math.Sin(b2);
                double dpdx = 0.8 * k1 * Math.Cos(a1) * Math.Sin(b1) + 0.5 * Math.Sin(a2) * 0.7 * k2 * Math.Cos(b2);
                int q = j * FN + i;
                _fx[q] = (float)(dpdy / k1 * Sc.Flow); _fy[q] = (float)(-dpdx / k1 * Sc.Flow);
            }
    }

    // ================================================================ profilowanie
    public static bool Prof;
    public static readonly Dictionary<string, double> ProfT = new();
    public static readonly Dictionary<string, double> VertT = new();
    public static void PAdd(string k, long t0) { if (!Prof) return; double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; ProfT[k] = (ProfT.TryGetValue(k, out var v) ? v : 0) + ms; }
    public static long TS => System.Diagnostics.Stopwatch.GetTimestamp();

    // ================================================================ pętla
    public void Update(float fdt)
    {
        float dt = fdt * TimeScale * Ff, gdt = fdt * Ff;
        T += gdt; SimT += dt; Frame++;
        for (int i = _timers.Count - 1; i >= 0; i--)
            if (_timers[i].t <= T) { var a = _timers[i].a; _timers.RemoveAt(i); a(); }
        long t0 = TS;
        if (Infinite)
        {
            UpdateChunks();
            _scanT -= gdt;
            if (_scanT <= 0) { _scanT = 0.5f; ParkInactive(); }
            PAdd("chunks", t0); t0 = TS;
        }
        if (!Preview)
        {
            if (Infinite) Grid.Build(Ents, WinX0 - CS, WinY0 - CS, WinW + 2 * CS, WinH + 2 * CS, Sc.GridCell);
            else Grid.Build(Ents, 0, 0, W, H, Sc.GridCell);
        }
        UpdateFlow(gdt);
        PAdd("grid+flow", t0);
        int n0 = Ents.Count;
        for (int i = 0; i < n0; i++)
        {
            var e = Ents[i];
            if (!e.Alive) continue;
            long ts = Prof ? TS : 0;
            e.Step(dt, gdt);
            if (Prof) PAdd("step:" + e.GetType().Name, ts);
            if (!e.Alive) continue;
            if (TimeScale > 5 && e != Player && !e.Fixed && !Preview)
            {
                // time-lapse: szybkie pływaki ograniczone do ~0,3 kadru na sekundę (inaczej byłyby niewidoczną smugą)
                float vm = Sc.ViewW * 0.3f / TimeScale, v2 = e.Vx * e.Vx + e.Vy * e.Vy;
                if (v2 > vm * vm) { float k = vm / MathF.Sqrt(v2); e.Vx *= k; e.Vy *= k; }
            }
            e.Integrate(dt);
            if (!Preview)
            {
                if (_fx != null && e.FlowK > 0 && !e.Fixed) { FlowAt(e.X, e.Y, out float fx, out float fy); e.X += fx * dt * e.FlowK; e.Y += fy * dt * e.FlowK; }
                if (!Infinite)
                {
                    float m = Math.Min(e.R, 30);
                    if (e.X < m) e.X += (m - e.X) * 0.5f; else if (e.X > W - m) e.X -= (e.X - W + m) * 0.5f;
                    if (e.Y < m) e.Y += (m - e.Y) * 0.5f; else if (e.Y > H - m) e.Y -= (e.Y - H + m) * 0.5f;
                }
                Sc.Constrain(this, e);
            }
            e.UpdatePrim();
        }
        t0 = TS;
        Physics.CollideAll(Ents);
        PAdd("collide", t0); t0 = TS;
        for (int i = 0; i < Ents.Count; i++)
        {
            var e = Ents[i];
            if (!e.Alive) continue;
            if (e.Mem != null)
            {
                e.Mem.Follow(e.X, e.Y, e.A, e.MemK, gdt);
                if (e.MemS > 0 && OnScreen(e))
                    foreach (var o in e.Nb)
                    {
                        if (!o.Alive || !o.Solid) continue;
                        // dwie miękkie komórki: wzajemne spłaszczenie obrysów; inaczej wgniecenie o sztywny kształt
                        if (o.Mem != null && o.Mem.IsRadial && o.Mem.MaxR > 0 && e.Mem.IsRadial && o.Soft > 0 && e.Soft > 0)
                            e.Mem.PushOutMem(o.Mem, e.MemS * Clamp(e.Soft / (e.Soft + o.Soft) * 2, 0.3f, 1));
                        else e.Mem.PushOut(o.Prim, e.MemS);
                    }
            }
            e.Post(dt, gdt);
        }
        PAdd("membranes+post", t0); t0 = TS;
        UpdateDebris(dt);
        PAdd("debris", t0); t0 = TS;
        Fx.Update(gdt);
        if (!Preview)
        {
            Sc.Update(this, dt, gdt);
            PAdd("scenario", t0);
            if (EverySecond(gdt)) CheckGoals();
        }
        if (T % 1f < gdt) Ents.RemoveAll(e => !e.Alive);
        if (Player != null && !Player.Alive && !Over && !Preview) OnPlayerLost();
    }

    void UpdateDebris(float dt)
    {
        float T0 = Infinite ? DebrisTile : W, T1 = Infinite ? DebrisTile : H;
        _dPhase = (_dPhase + 1) % 3;
        float d3 = dt * 3;
        for (int i = _dPhase; i < DebrisList.Count; i += 3)
        {
            var d = DebrisList[i];
            float s = MathF.Sqrt(2 * d.D * d3);
            d.X += s * RandN() + Sc.FlowX * d3; d.Y += s * RandN() + Sc.FlowY * d3;
            if (_fx != null) { FlowAt(CamX, CamY, out float fx, out float fy); d.X += fx * d3; d.Y += fy * d3; }
            if (d.X < 0) d.X += T0; else if (d.X > T0) d.X -= T0;
            if (d.Y < 0) d.Y += T1; else if (d.Y > T1) d.Y -= T1;
            DebrisList[i] = d;
        }
    }

    void CheckGoals()
    {
        var G = Sc.Goals;
        if (G == null || AllGoals || Over || Nano) return;
        while (GoalIdx < G.Length && G[GoalIdx].Val(this) >= G[GoalIdx].Target)
        {
            var g = G[GoalIdx]; GoalIdx++;
            OnGoal?.Invoke(g);
        }
        if (GoalIdx >= G.Length) AllGoals = true;
    }

    void OnPlayerLost()
    {
        var p = Player;
        if (Sc.OnPlayerLost(this, p)) return;
        if (!Adopt(p.Kind, p.X, p.Y))
        {
            Player = null;
            if (Focus == null) GameOver(Sc.LoseText);
        }
    }

    public void GameOver(string text, bool win = false)
    {
        if (Over || Nano) return;
        Over = true;
        if (!Preview) OnGameOver?.Invoke(text, win);
    }

    public Ent CamTarget() => Focus != null && Focus.Alive ? Focus : Player;
    public bool EverySecond(float gdt) => T % 1f < gdt;

    public void Draw(Painter p, View v)
    {
        p.Identity();
        Sc.DrawUnder(this, p, v);
        _sorted.Clear();
        PhagesVisible = 0;
        foreach (var e in Ents) if (e.Alive && v.Vis(e.X, e.Y, e.R * 1.6f + e.DrawR)) { _sorted.Add(e); if (e.Kind == Kind.Phage) PhagesVisible++; }
        _sorted.Sort((a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Id.CompareTo(b.Id));
        foreach (var e in _sorted)
        {
            p.Identity(); p.Alpha = 1;
            if (Prof) { int v0 = p.VertexCount; e.Draw(p, v); string k = "verts:" + e.GetType().Name; VertT[k] = Math.Max(VertT.TryGetValue(k, out var o) ? o : 0, 0) + (p.VertexCount - v0); }
            else e.Draw(p, v);
        }
        p.Identity(); p.Alpha = 1;
        Fx.Draw(p, v);
        p.Identity();
        Sc.DrawOver(this, p, v);
        if (!Infinite) DrawBounds(p, v);
    }
    /// <summary>Przyciemnienie poza granicą pola (tylko świat skończony).</summary>
    void DrawBounds(Painter p, View v)
    {
        if (Preview) return;
        var dark = new Color(0, 0, 0, 0.45f);
        if (v.X0 < 0) p.Rect(v.X0, v.Y0, -v.X0, v.Y1 - v.Y0, dark);
        if (v.X1 > W) p.Rect(W, v.Y0, v.X1 - W, v.Y1 - v.Y0, dark);
        if (v.Y0 < 0) p.Rect(Math.Max(0, v.X0), v.Y0, Math.Min(W, v.X1) - Math.Max(0, v.X0), -v.Y0, dark);
        if (v.Y1 > H) p.Rect(Math.Max(0, v.X0), H, Math.Min(W, v.X1) - Math.Max(0, v.X0), v.Y1 - H, dark);
    }

    public void DrawDebris(Painter pt, View v, Rect2[] uv, Color[] cols)
    {
        pt.Identity();
        float TX = Infinite ? DebrisTile : W, TY = Infinite ? DebrisTile : H;
        int kx0 = Infinite ? (int)MathF.Floor(v.X0 / TX) - 1 : 0, kx1 = Infinite ? (int)MathF.Floor(v.X1 / TX) : 0;
        int ky0 = Infinite ? (int)MathF.Floor(v.Y0 / TY) - 1 : 0, ky1 = Infinite ? (int)MathF.Floor(v.Y1 / TY) : 0;
        for (int ky = ky0; ky <= ky1; ky++)
            for (int kx = kx0; kx <= kx1; kx++)
                foreach (var d in DebrisList)
                {
                    float x = d.X + kx * TX, y = d.Y + ky * TY;
                    if (!v.Vis(x, y, d.S * 2)) continue;
                    pt.Sprite(x, y, d.S * 2, d.S * 2, 0, uv[d.K], Mul(cols[d.K % cols.Length], d.Al));
                }
    }

    public void MakeDebris(int n, float s0, float s1, float D)
    {
        float TX = Infinite ? DebrisTile : W, TY = Infinite ? DebrisTile : H;
        for (int i = 0; i < n; i++)
            DebrisList.Add(new Debris { X = Rand(0, TX), Y = Rand(0, TY), S = Rand(s0, s1), K = RandI(0, 5), Al = Rand(0.25f, 0.7f), D = D });
    }
}

public static class Physics
{
    static readonly List<Ent> _buf = new();
    /// <summary>Kolizje metodą sort &amp; sweep; rozsuwanie proporcjonalne do "masy" (oporu).</summary>
    public static void CollideAll(List<Ent> ents)
    {
        _buf.Clear();
        foreach (var e in ents) { e.Nb.Clear(); if (e.Alive && e.Solid) _buf.Add(e); }
        _buf.Sort((a, b) => (a.X - a.R).CompareTo(b.X - b.R));
        int n = _buf.Count;
        for (int i = 0; i < n; i++)
        {
            var a = _buf[i]; float ax1 = a.X + a.R;
            for (int j = i + 1; j < n; j++)
            {
                var b = _buf[j];
                if (b.X - b.R > ax1) break;
                if (MathF.Abs(a.Y - b.Y) > a.R + b.R) continue;
                if (a.Fixed && b.Fixed) continue;
                if (!a.Collides(b) || !b.Collides(a)) continue;
                a.Nb.Add(b); b.Nb.Add(a);
                if (!Geo.PrimPrim(a.Prim, b.Prim, out float pen, out float nx, out float ny)) continue;
                float ma = a.Fixed ? 1e9f : a.Mass, mb = b.Fixed ? 1e9f : b.Mass;
                float wa = mb / (ma + mb), wb = ma / (ma + mb);
                // miękkie ciała: część penetracji to ściśnięcie (sprężyste, rozwiązywane powoli), reszta – twardy kontakt
                float give = (a.Soft * a.Prim.R + b.Soft * b.Prim.R) * 0.3f;
                float res = pen <= give ? pen * 0.22f : (pen - give) + give * 0.22f;
                a.X -= nx * res * wa; a.Y -= ny * res * wa;
                b.X += nx * res * wb; b.Y += ny * res * wb;
                a.UpdatePrim(); b.UpdatePrim();
                a.OnTouch(b, nx, ny); b.OnTouch(a, -nx, -ny);
            }
        }
    }
}

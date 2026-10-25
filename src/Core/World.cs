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
    /// <summary>Krążenie (światy tkankowe): tętno (/min; 0 = brak serca), faza cyklu 0..1, liczba uderzeń,
    /// temperatura ciała (°C), nasilenie zakażenia 0..1, przebyta droga krwi w żyłkach (µm).</summary>
    public float HeartBpm, HeartPh, TempC = 36.8f, Severity, BloodS, BloodV;
    public int Beats;
    /// <summary>Czas skurczu (odstęp S1–S2, s) dla bieżącego uderzenia.</summary>
    public float Systole = 0.3f;
    public Ent Player, Focus;
    public bool Over, Preview, ShowField, AllGoals;
    /// <summary>Tryb nanobota (eksploracja): bez celów i końca gry.</summary>
    public bool Nano;
    /// <summary>Bieżący filtr mikroskopu (ScopeMode.Filter); 7 = fluorescencja – widać tylko komórki z GFP.</summary>
    public int ScopeFilter;
    public bool Fluo => ScopeFilter == 7;
    public int GoalIdx;
    public readonly Dictionary<string, float> Stats = new();
    public InputState Input = new();
    public TileField Nutri, Amp;
    public WaterField Water;
    public BactField Bact;
    public int Wave = 1;
    public float CamX, CamY, ViewR = 50;
    /// <summary>Rozmiar piksela obrazu w µm (0 = nieznany, np. test bez okna – wtedy szacowany z ViewR).</summary>
    public float PxUm;
    public float PixelUm => PxUm > 0 ? PxUm : ViewR / 800f;
    public float WinX0, WinY0, WinW, WinH;
    public bool Em => Sc.Em;
    public readonly int Seed = RandI(1, 1 << 29);
    /// <summary>Liczba widocznych fagów w ostatniej klatce (do wyboru poziomu szczegółowości).</summary>
    public int PhagesVisible;

    public Action<string, string> OnLog;
    public Action<string, string> OnBanner;
    public Action<string, bool> OnGameOver;
    public Action<string, float, float> OnSfx;
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
        if (!preview && sc.Water) Water = new WaterField(this);
        Fx.Flow = (x, y) => { FlowAt(x, y, out float fx, out float fy); SwimFlow(x, y, -1, out float sx, out float sy); return new Vector2(fx + sx, fy + sy); };
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
        OnSfx(kind, vol * Clamp(1.2f - d * 0.7f, 0, 1), Clamp((x - CamX) / Math.Max(ViewR, 1e-6f), -1, 1) * 0.8f);
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

    // ================================================================ hydrodynamika pływaków (przepływ Stokesa)
    /// <summary>Źródło przepływu: poruszające się ciało (prędkość w µm/s czasu biologicznego, promień zastępczy a).</summary>
    public struct FlowSrc { public float X, Y, Ux, Uy, A, K; public int Id; }
    public readonly List<FlowSrc> Srcs = new();
    readonly List<FlowSrc> _srcNext = new();
    /// <summary>
    /// Prędkość wody w punkcie wywołana przez pływaki. Pływak sam się napędza, więc całkowita siła na wodę jest zerowa
    /// i nie ma członu stokesletu (~1/r, który „ciągnąłby” wodę za ciałem). Zostają:
    ///  • dipol siłowy (stresslet): u = K·α·U·(a/r)²·(3cos²θ − 1)·r̂ – „pchacz” (K = +1, E. coli) wyrzuca wodę przodem i tyłem,
    ///    a zasysa bokami; „ciągacz” (K = −1, Chlamydomonas) odwrotnie. α ≈ 0,45 odtwarza pomiar dla E. coli
    ///    (Drescher i in., PNAS 2011: dipol ≈ 0,4 pN·µm, pole spada poniżej szumu Browna już po ~8 µm);
    ///  • dipol źródłowy: u = ½·U·(a/r)³·(3cosθ·r̂ − ê) – pole „squirmera” (orzęski, Volvox; Drescher i in., PRL 2010),
    ///    przez które drobiny obok pływaka zataczają małe pętle zamiast odpływać razem z nim.
    /// Lauga i Powers, Rep Prog Phys 2009. Pole zanika do 6a.
    /// </summary>
    public void SwimFlow(float x, float y, int selfId, out float ux, out float uy, float size = 0)
    {
        ux = uy = 0;
        if (_sgNx == 0) return;
        int gi = (int)((x - _sgX0) / _sgCell), gj = (int)((y - _sgY0) / _sgCell);
        if (x < _sgX0 || y < _sgY0 || gi >= _sgNx || gj >= _sgNy) return;
        int cell = gj * _sgNx + gi;
        for (int qi = _sgStart[cell], qe = _sgStart[cell + 1]; qi < qe; qi++)
        {
            var s = Srcs[_sgIdx[qi]]; if (s.Id == selfId) continue;
            float rx = x - s.X, ry = y - s.Y, r2 = rx * rx + ry * ry, lim = 6 * s.A;
            if (r2 > lim * lim) continue;
            float r = MathF.Sqrt(r2); if (r < s.A) r = s.A;
            float u = MathF.Sqrt(s.Ux * s.Ux + s.Uy * s.Uy); if (u < 1e-6f) continue;
            float ex = s.Ux / u, ey = s.Uy / u, nx = rx / r, ny = ry / r, c = ex * nx + ey * ny;
            float q = s.A / r, q2 = q * q, q3 = q2 * q;
            float st = s.K * 0.45f * u * q2 * (3 * c * c - 1);    // stresslet (radialny)
            float sd = 0.5f * u * q3;                              // dipol źródłowy
            float fx = st * nx + sd * (3 * c * nx - ex);
            float fy = st * ny + sd * (3 * c * ny - ey);
            float taper = 1 - r / lim, w = taper * taper / (1 + Sq(size / s.A));
            ux += fx * w; uy += fy * w;
        }
    }
    void CollectFlowSources()
    {
        _srcNext.Clear();
        foreach (var e in Ents)
        {
            if (!e.Alive || e.Fixed || e.Holder != null || !e.Solid || !e.Swims) continue;
            float v2 = e.Vx * e.Vx + e.Vy * e.Vy; if (v2 < 1e-6f) continue;
            float a = Math.Max(e.Radius, e.Length * 0.3f); if (a <= 0) continue;
            if (!OnScreen(e, 1.8f)) continue;
            _srcNext.Add(new FlowSrc { X = e.X, Y = e.Y, Ux = e.Vx, Uy = e.Vy, A = a, K = e.Dipole, Id = e.Id });
        }
        Srcs.Clear(); Srcs.AddRange(_srcNext);
        BuildSrcGrid();
    }
    // siatka źródeł przepływu (CSR): każde źródło wpisane do komórek pokrywających jego zasięg 6a –
    // punkt sprawdza tylko pływaki w pobliżu, a nie wszystkie w kadrze (tysiące drobin × setki pływaków)
    float _sgX0, _sgY0, _sgCell = 1; int _sgNx, _sgNy; int[] _sgStart = new int[1], _sgIdx = new int[64], _sgFill = new int[1];
    void BuildSrcGrid()
    {
        _sgNx = _sgNy = 0;
        int n = Srcs.Count; if (n == 0) return;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue, limSum = 0;
        foreach (var s in Srcs) { float l = 6 * s.A; limSum += l; x0 = Math.Min(x0, s.X - l); y0 = Math.Min(y0, s.Y - l); x1 = Math.Max(x1, s.X + l); y1 = Math.Max(y1, s.Y + l); }
        float cell = Math.Max(limSum / n, Math.Max(x1 - x0, y1 - y0) / 64);
        int nx = Math.Max(1, (int)MathF.Ceiling((x1 - x0) / cell)), ny = Math.Max(1, (int)MathF.Ceiling((y1 - y0) / cell)), nc = nx * ny;
        if (_sgStart.Length < nc + 1) { _sgStart = new int[nc + 1]; _sgFill = new int[nc + 1]; }
        Array.Clear(_sgStart, 0, nc + 1);
        for (int k = 0; k < n; k++)
        {
            var s = Srcs[k]; float l = 6 * s.A;
            int i0 = (int)((s.X - l - x0) / cell), i1 = Math.Min(nx - 1, (int)((s.X + l - x0) / cell)), j0 = (int)((s.Y - l - y0) / cell), j1 = Math.Min(ny - 1, (int)((s.Y + l - y0) / cell));
            for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) _sgStart[j * nx + i + 1]++;
        }
        for (int c = 0; c < nc; c++) _sgStart[c + 1] += _sgStart[c];
        if (_sgIdx.Length < _sgStart[nc]) _sgIdx = new int[_sgStart[nc] * 2];
        Array.Copy(_sgStart, _sgFill, nc + 1);
        for (int k = 0; k < n; k++)
        {
            var s = Srcs[k]; float l = 6 * s.A;
            int i0 = (int)((s.X - l - x0) / cell), i1 = Math.Min(nx - 1, (int)((s.X + l - x0) / cell)), j0 = (int)((s.Y - l - y0) / cell), j1 = Math.Min(ny - 1, (int)((s.Y + l - y0) / cell));
            for (int j = j0; j <= j1; j++) for (int i = i0; i <= i1; i++) _sgIdx[_sgFill[j * nx + i]++] = k;
        }
        _sgX0 = x0; _sgY0 = y0; _sgCell = cell; _sgNx = nx; _sgNy = ny;
    }
    /// <summary>Czy w punkcie działa jakikolwiek pływak (tania wstępna selekcja).</summary>
    public bool FlowNear(float x, float y)
    {
        if (_sgNx == 0) return false;
        int gi = (int)((x - _sgX0) / _sgCell), gj = (int)((y - _sgY0) / _sgCell);
        if (x < _sgX0 || y < _sgY0 || gi >= _sgNx || gj >= _sgNy) return false;
        int c = gj * _sgNx + gi; return _sgStart[c + 1] > _sgStart[c];
    }
    St _lastPS;

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

    // ================================================================ linia (dostosowanie = potomstwo)
    /// <summary>
    /// Linia gracza. W ewolucji liczy się nie przetrwanie osobnika, tylko liczba potomków, którzy sami się rozmnożą
    /// (dostosowanie darwinowskie). Urodzeni łącznie, najgłębsze pokolenie, szczyt liczebności żyjącej linii.
    /// </summary>
    public int LineBorn, MaxGen, LinePeak, LineNow;
    /// <summary>Suma i liczba zmierzonych czasów pokolenia (wiek rodzica przy podziale, s czasu procesów życiowych).</summary>
    public double GenTSum; public int GenTN;
    /// <summary>Średni czas pokolenia w czasie organizmu (s) – 0, gdy jeszcze nie było podziału.</summary>
    public float GenTime => GenTN > 0 ? (float)(GenTSum / GenTN) : 0;
    /// <summary>
    /// Narodziny potomka. Dziedziczy przynależność do linii i dostaje kolejne pokolenie.
    /// countAge = false dla drugiej córki tego samego podziału (jeden podział = jeden pomiar czasu pokolenia).
    /// </summary>
    public void Born(Ent parent, Ent child, bool countAge = true)
    {
        if (parent == null || child == null) return;
        Born(parent.Gen, parent.BornT, parent.Lineage || parent == Player, child, countAge);
    }
    /// <summary>Wariant, gdy rodzica nie ma już jako encji (np. DNA faga w zakażonej komórce).</summary>
    public void Born(int parentGen, float parentBornT, bool mine, Ent child, bool countAge = true)
    {
        child.Gen = parentGen + 1; child.BornT = SimT;
        child.Lineage = mine;
        if (!mine || Preview) return;
        LineBorn++;
        // dźwięk narodzin (wiriony T4 mają własny dźwięk lizy, młode niesporczaki – wyklucia)
        if (child is not PhageT4 && child is not Tardigrade && countAge) Sfx("divide", child.X, child.Y, 0.8f);
        if (child.Gen > MaxGen) MaxGen = child.Gen;
        float age = (SimT - parentBornT) * Bio;
        // założyciele (pokolenie 0) zaczynają w losowej fazie cyklu – ich wiek nie jest czasem pokolenia
        if (countAge && parentGen > 0 && age > 0) { GenTSum += age; GenTN++; }
    }
    /// <summary>Potomstwo, którego nie ma na scenie jako osobnych encji (np. niewidoczna część wyrzutu faga).</summary>
    public void BornUnseen(int n, int gen) { if (n <= 0 || Preview) return; LineBorn += n; if (gen > MaxGen) MaxGen = gen; }

    /// <summary>Typ założyciela linii (pierwszy organizm gracza).</summary>
    public Type LineType;
    public void SetPlayer(Ent e)
    {
        if (Player != null && Player != e) Player.Lineage = true;
        Player = e;
        if (e != null && LineType == null && e is not Nanobot) LineType = e.GetType();
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
                if (!c.Generated)
                {
                    // nowe chunki powstają na brzegu okna (poza kadrem) – generowane po jednym na klatkę, bez przycięć
                    if (force) { c.Generated = true; Sc.GenerateChunk(this, c); Sc.OnChunkActivated(this, c); }
                    else _pending.Add(c);
                }
                else Sc.OnChunkActivated(this, c);
            }
    }
    /// <summary>Zamraża encje leżące w nieaktywnych chunkach (i wszystko, co w nich siedzi – ofiary, fagi na komórkach).</summary>
    readonly List<Chunk> _pending = new();
    void GeneratePending()
    {
        _pending.RemoveAll(c => !c.Active || c.Generated);
        if (_pending.Count == 0) return;
        var t = CamTarget(); float px = t != null ? t.X : CamX, py = t != null ? t.Y : CamY;
        int bi = 0; float bd = float.MaxValue;
        for (int i = 0; i < _pending.Count; i++) { float d = Sq(_pending[i].CX - px) + Sq(_pending[i].CY - py); if (d < bd) { bd = d; bi = i; } }
        var c = _pending[bi]; _pending.RemoveAt(bi);
        c.Generated = true; Sc.GenerateChunk(this, c); Sc.OnChunkActivated(this, c);
    }
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
        if (Sc.HasCustomFlow && Sc.CustomFlow(this, x, y, out fx, out fy)) return;
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
    /// <summary>Najdłuższy czas części geometrii (ms) – profil równoległego rysowania.</summary>
    public static readonly Dictionary<string, double> PartT = new();
    static void VAdd(string k, int n) { lock (VertT) VertT[k] = (VertT.TryGetValue(k, out var o) ? o : 0) + n; }
    /// <summary>Czas sekcji (profilowanie). Bezpieczne wątkowo – geometria jest budowana równolegle.</summary>
    public static void PAdd(string k, long t0) { if (!Prof) return; double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; lock (ProfT) ProfT[k] = (ProfT.TryGetValue(k, out var v) ? v : 0) + ms; }
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
            GeneratePending();
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
                // unoszenie przez wodę poruszaną przez inne pływaki (mniejsze ciała bardziej)
                if (Srcs.Count > 0 && !e.Fixed && e.Holder == null && e.FlowK > 0 && OnScreen(e, 1.6f))
                {
                    SwimFlow(e.X, e.Y, e.Id, out float sx, out float sy, Math.Max(e.Radius, e.Length * 0.3f));
                    e.X += sx * dt; e.Y += sy * dt;
                }
                if ((_fx != null || Sc.HasCustomFlow) && e.FlowK > 0 && !e.Fixed) { FlowAt(e.X, e.Y, out float fx, out float fy); e.X += fx * dt * e.FlowK; e.Y += fy * dt * e.FlowK; }
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
        if (!Preview)
        {
            long tw = TS; if (Sc.Fluid) CollectFlowSources(); else { Srcs.Clear(); _sgNx = 0; } PAdd("flow:src", tw); tw = TS;
            Water?.Update(dt); PAdd("flow:water", tw); tw = TS;
            Bact?.Advect(this, dt); PAdd("flow:bact", tw);
        }
        Fx.FlowScale = Preview ? 0 : TimeScale * Ff;
        t0 = TS;
        Physics.CollideAll(Ents, Preview ? null : this);
        PAdd("collide", t0); t0 = TS;
        // membrany na wszystkich rdzeniach: każda komórka zmienia tylko własny obrys (odczyt obrysu sąsiada w trakcie jego
        // aktualizacji daje co najwyżej jednoklatkowe opóźnienie wgniecenia – niewidoczne); Post – potem, po kolei
        _memEnts.Clear();
        for (int i = 0; i < Ents.Count; i++) { var e = Ents[i]; if (e.Alive && e.Mem != null) _memEnts.Add(e); }
        int nm = _memEnts.Count, parts = nm < 48 ? 1 : Math.Min(Workers * 2, nm / 24);
        if (parts <= 1) for (int i = 0; i < nm; i++) MemStep(_memEnts[i], gdt);
        else System.Threading.Tasks.Parallel.For(0, parts, k => { int a = nm * k / parts, b = nm * (k + 1) / parts; for (int i = a; i < b; i++) MemStep(_memEnts[i], gdt); });
        for (int i = 0; i < Ents.Count; i++) { var e = Ents[i]; if (e.Alive) e.Post(dt, gdt); }
        PAdd("membranes+post", t0); t0 = TS;
        UpdateDebris(dt);
        PAdd("debris", t0); t0 = TS;
        Fx.Update(gdt);
        if (!Preview)
        {
            Sc.Update(this, dt, gdt);
            PAdd("scenario", t0);
            if (EverySecond(gdt))
            {
                LineNow = Sc.LinePop(this);
                if (LineNow > LinePeak) LinePeak = LineNow;
                CheckGoals();
            }
        }
        if (T % 1f < gdt) Ents.RemoveAll(e => !e.Alive);
        if (Player != null && !Player.Alive && !Over && !Preview) OnPlayerLost();
        if (Player != null && Player.Alive && !Preview)
        {
            var ps = Player.State;
            if (ps != _lastPS && ps is St.Caught or St.Cup && _lastPS is not (St.Caught or St.Cup or St.Vac))
            {
                Banner("SCHWYTANY!", Player is Paramecium ? "Spacja – wystrzel trichocysty, może uda się wyrwać" : "Pochłonięty przez drapieżnika – z wodniczki pokarmowej nie ma ucieczki");
                OnSfx?.Invoke("hurt", 1, 0);
            }
            _lastPS = ps;
        }
    }

    readonly List<Ent> _memEnts = new();
    void MemStep(Ent e, float gdt)
    {
        e.PreMem();
        e.Mem.Lod = !OnScreen(e);
        if (e.Mem.Lod && !OnScreen(e, 2.6f) && (Frame + e.Id) % 8 != 0) return;
        e.Mem.Follow(e.X, e.Y, e.A, e.MemK, gdt);
        // wgniecenia od sąsiadów liczone tylko, gdy ciało jest na ekranie dość duże, by je zobaczyć
        bool big = e.Mem.MaxR * 2 > PixelUm * e.MemPushPx;
        if (e.MemS > 0 && big && OnScreen(e))
            foreach (var o in e.Nb)
            {
                if (!o.Alive || !o.Solid || e.SkipMemPush(o, Frame)) continue;
                if (e.Permeable(o) || o.Permeable(e)) continue; // inna warstwa tkanki – nie wgniata błony
                // dwie miękkie komórki: wzajemne spłaszczenie obrysów; inaczej wgniecenie o sztywny kształt
                if (o.Mem != null && o.Mem.IsRadial && o.Mem.MaxR > 0 && e.Mem.IsRadial && o.Soft > 0 && e.Soft > 0)
                    e.Mem.PushOutMem(o.Mem, e.MemS * Clamp(e.Soft / (e.Soft + o.Soft) * 2, 0.3f, 1));
                else for (int pi = 0; pi < o.PartCount; pi++) e.Mem.PushOut(o.Part(pi), e.MemS);
            }
        if (big && OnScreen(e)) e.Mem.ConserveArea();
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

    /// <summary>Tytuł ekranu końca ustawiony przez scenariusz (np. „Samotnik”); pusty = domyślny.</summary>
    public string OverTitle = "";
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
        long tdu = TS; int vu = p.VertexCount;
        Sc.DrawUnder(this, p, v);
        PAdd("draw:under", tdu); tdu = TS; if (Prof) VAdd("verts:(tło sceny)", p.VertexCount - vu); vu = p.VertexCount;
        Water?.Draw(p, v);
        PAdd("draw:water", tdu); if (Prof) VAdd("verts:(woda)", p.VertexCount - vu);
        p.Identity();
        CollectVisible(v);
        foreach (var e in _sorted) DrawEnt(e, p, v);
        DrawTop(p, v);
    }
    void CollectVisible(View v, bool skipStatic = false)
    {
        _sorted.Clear();
        PhagesVisible = 0;
        foreach (var e in Ents) if (e.Alive && !(skipStatic && e.StaticDraw) && v.Vis(e.X, e.Y, e.R * 1.6f + e.DrawR)) { _sorted.Add(e); if (e.Kind == Kind.Phage) PhagesVisible++; }
        _sorted.Sort((a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Id.CompareTo(b.Id));
    }

    // ---------------------------------------------------------------- pamięć geometrii statycznej
    readonly List<Ent> _stEnts = new();
    readonly List<int> _stCut = new(), _stLayer = new();
    Painter[] _sp = Array.Empty<Painter>(), _st = Array.Empty<Painter>();
    readonly View _stView = new();
    float _stPx, _stOX = float.NaN, _stOY; long _stSig; int _stParts;
    /// <summary>Podpis zbioru statycznych encji w obszarze pamięci (nowe chunki, strawione włókna → przebudowa).</summary>
    long StaticSig(View r)
    {
        long sig = 0;
        foreach (var e in Ents)
            if (e.Alive && e.StaticDraw && r.Vis(e.X, e.Y, e.R * 1.6f + e.DrawR))
                sig = sig * 1000003 + e.Id * 7919 + (long)(e.StaticKey * 1000);
        return sig;
    }
    /// <summary>
    /// Pamięć statyczna: jeśli widok wyszedł poza zapamiętany obszar, zmieniło się powiększenie (&gt; 4%), punkt odniesienia
    /// albo zbiór encji – przebudowa (obszar = kadr powiększony o 60% z każdej strony), inaczej nic nie jest wysyłane.
    /// </summary>
    void UpdateStatic(View v, float px, float ox, float oy, WorldView view, Rid tex)
    {
        bool inside = v.X0 >= _stView.X0 && v.X1 <= _stView.X1 && v.Y0 >= _stView.Y0 && v.Y1 <= _stView.Y1;
        bool ok = view.StaticOwner == this && !float.IsNaN(_stOX) && inside && MathF.Abs(px / _stPx - 1) < 0.04f && ox == _stOX && oy == _stOY;
        if (ok && (Frame & 7) == 0 && StaticSig(_stView) != _stSig) ok = false;
        if (ok) return;
        float mx = (v.X1 - v.X0) * 0.6f, my = (v.Y1 - v.Y0) * 0.6f;
        _stView.X0 = v.X0 - mx; _stView.X1 = v.X1 + mx; _stView.Y0 = v.Y0 - my; _stView.Y1 = v.Y1 + my;
        _stView.Px = v.Px; _stView.S = v.S; _stView.T = v.T;
        _stPx = px; _stOX = ox; _stOY = oy; view.StaticOwner = this;
        _stSig = StaticSig(_stView);
        _stEnts.Clear();
        foreach (var e in Ents) if (e.Alive && e.StaticDraw && _stView.Vis(e.X, e.Y, e.R * 1.6f + e.DrawR)) _stEnts.Add(e);
        _stEnts.Sort((a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Id.CompareTo(b.Id));
        int n = _stEnts.Count, nw = Workers;
        _stCut.Clear(); _stCut.Add(0);
        for (int i = 1; i < n; i++) if (_stEnts[i].Layer != _stEnts[i - 1].Layer || (i * nw / Math.Max(1, n)) != ((i - 1) * nw / Math.Max(1, n))) _stCut.Add(i);
        _stCut.Add(n);
        int parts = n == 0 ? 0 : _stCut.Count - 1;
        if (_sp.Length < parts)
        {
            var a = new Painter[parts]; var t = new Painter[parts];
            for (int i = 0; i < parts; i++) { a[i] = i < _sp.Length ? _sp[i] : new Painter(false, 16384); t[i] = i < _st.Length ? _st[i] : new Painter(true, 16384); a[i].Tex = t[i]; }
            _sp = a; _st = t;
        }
        _stLayer.Clear();
        for (int k = 0; k < parts; k++) _stLayer.Add(_stEnts[_stCut[k]].Layer);
        for (int k = 0; k < parts * 2; k++) view.StaticItem(k);
        var items = new Rid[parts * 2]; for (int k = 0; k < parts * 2; k++) items[k] = view.StaticItem(k);
        System.Threading.Tasks.Parallel.For(0, parts, k =>
        {
            var p = _sp[k]; var pt = _st[k];
            p.Begin(px); p.OX = ox; p.OY = oy; pt.Begin(px); pt.OX = ox; pt.OY = oy;
            for (int i = _stCut[k]; i < _stCut[k + 1]; i++) DrawEnt(_stEnts[i], p, _stView);
            RenderingServer.CanvasItemClear(items[2 * k]); RenderingServer.CanvasItemClear(items[2 * k + 1]);
            pt.Flush(items[2 * k], tex); p.Flush(items[2 * k + 1]);
        });
        for (int k = parts * 2; k < _stParts * 2 && k < view.StaticCount; k++) RenderingServer.CanvasItemClear(view.StaticItem(k));
        // pozostałości po innym świecie (menu ↔ gra)
        for (int k = Math.Max(parts, _stParts) * 2; k < view.StaticCount; k++) RenderingServer.CanvasItemClear(view.StaticItem(k));
        _stParts = parts;
        if (Prof) VAdd("static:rebuild", 1);
    }
    static void DrawEnt(Ent e, Painter p, View v)
    {
        p.Identity(); p.Alpha = 1;
        int m0 = p.Mark;
        if (Prof) { long td = TS; e.Draw(p, v); PAdd("draw:" + e.GetType().Name, td); VAdd("verts:" + e.GetType().Name, p.Mark - m0); }
        else e.Draw(p, v);
        e.DrawCost = p.Mark - m0;
    }
    void DrawTop(Painter p, View v)
    {
        p.Identity(); p.Alpha = 1;
        long tfx = TS; int vf = p.VertexCount; Fx.Draw(p, v); PAdd("draw:fx", tfx); if (Prof) VAdd("verts:(efekty)", p.VertexCount - vf);
        p.Identity(); vf = p.VertexCount; long tov = TS;
        Sc.DrawOver(this, p, v); PAdd("draw:over", tov); if (Prof) VAdd("verts:(nakładka sceny)", p.VertexCount - vf);
        if (!Infinite) DrawBounds(p, v);
    }

    // ---------------------------------------------------------------- równoległe budowanie geometrii
    /// <summary>
    /// Geometria klatki budowana na wszystkich rdzeniach: tło sceny (+ zawiesina), kolejne zakresy posortowanych encji
    /// i nakładka (efekty, DrawOver) trafiają do osobnych malarzy, a wysyłane są w stałej kolejności – kolejność rysowania
    /// (warstwa, Id) jest taka sama jak przy jednym wątku. Zakresy dzielone są według kosztu z poprzedniej klatki
    /// (liczba wierzchołków encji), żeby wątki kończyły mniej więcej razem. Metody Draw encji nie zmieniają stanu świata.
    /// </summary>
    public static readonly int Workers = Math.Clamp(System.Environment.ProcessorCount, 2, 24);
    Painter[] _wp = Array.Empty<Painter>(), _wt = Array.Empty<Painter>();
    readonly List<int> _cut = new();
    public int DrawParts { get; private set; }
    /// <summary>
    /// Buduje geometrię klatki na wszystkich rdzeniach i wysyła ją (także równolegle) do elementów płótna widoku.
    /// Zwraca liczbę użytych elementów płótna.
    /// </summary>
    public int DrawParallel(View v, float px, float ox, float oy, WorldView view)
    {
        int nu = Math.Max(1, Sc.UnderParts);
        Painter.CacheTick();
        var tex = SpriteAtlas.Tex.GetRid();
        long tst = TS;
        UpdateStatic(v, px, ox, oy, view, tex);
        PAdd("static", tst);
        CollectVisible(v, true);
        // zakresy o podobnym koszcie (wierzchołki w poprzedniej klatce + stały narzut encji); każda zmiana warstwy
        // jest granicą zakresu
        int n = _sorted.Count;
        long total = 0; foreach (var e in _sorted) total += e.DrawCost + 24;
        int nw = Workers; _cut.Clear(); _cut.Add(0);
        long acc = 0, step = Math.Max(1, total / nw); int made = 1;
        for (int i = 0; i < n; i++)
        {
            acc += _sorted[i].DrawCost + 24;
            bool layerEnd = i + 1 < n && _sorted[i + 1].Layer != _sorted[i].Layer;
            if (layerEnd) { _cut.Add(i + 1); continue; }
            if (acc >= step * made && made < nw && i + 1 < n) { made++; _cut.Add(i + 1); }
        }
        if (_cut[^1] != n) _cut.Add(n);
        int ranges = _cut.Count - 1, parts = ranges + 1 + nu;
        if (_wp.Length < parts)
        {
            var a = new Painter[parts]; var t = new Painter[parts];
            for (int i = 0; i < parts; i++) { a[i] = i < _wp.Length ? _wp[i] : new Painter(false, 16384); t[i] = i < _wt.Length ? _wt[i] : new Painter(true, 16384); a[i].Tex = t[i]; }
            _wp = a; _wt = t;
        }
        // kolejność rysowania: tło (wektory, potem tekstury); dla każdej warstwy – statyczne tekstury i wektory, potem
        // dynamiczne tekstury i wektory; na końcu nakładka. Część k ma elementy 2k (tekstury) i 2k+1 (wektory).
        if (_ord.Length < parts * 2) _ord = new int[parts * 2];
        int o = 0;
        for (int k = 0; k < nu; k++) _ord[2 * k + 1] = o++;
        for (int k = 0; k < nu; k++) _ord[2 * k] = o++;
        int sk = 0;
        void StaticUpTo(int layer)
        {
            for (; sk < _stParts && _stLayer[sk] <= layer; sk++)
            {
                RenderingServer.CanvasItemSetDrawIndex(view.StaticItem(2 * sk), o++);
                RenderingServer.CanvasItemSetDrawIndex(view.StaticItem(2 * sk + 1), o++);
            }
        }
        for (int k = nu; k < parts - 1; )
        {
            int layer = _sorted[_cut[k - nu]].Layer, e = k;
            StaticUpTo(layer);
            while (e < parts - 1 && _sorted[_cut[e - nu]].Layer == layer) e++;
            for (int q = k; q < e; q++) _ord[2 * q] = o++;
            for (int q = k; q < e; q++) _ord[2 * q + 1] = o++;
            k = e;
        }
        StaticUpTo(int.MaxValue);
        _ord[2 * (parts - 1)] = o++; _ord[2 * (parts - 1) + 1] = o++;
        for (int k = 0; k < parts * 2; k++) RenderingServer.CanvasItemSetDrawIndex(view.Item(k), _ord[k]);
        if (_items.Length < parts * 2) _items = new Rid[parts * 2];
        for (int k = 0; k < parts * 2; k++) _items[k] = view.Item(k);
        DrawParts = parts;
        System.Threading.Tasks.Parallel.For(0, parts, k =>
        {
            long tpart = TS;
            var p = _wp[k]; var pt = _wt[k];
            p.Begin(px); p.OX = ox; p.OY = oy; pt.Begin(px); pt.OX = ox; pt.OY = oy;
            if (k < nu)
            {
                long tdu = TS; int vu = p.VertexCount;
                p.Identity(); Sc.DrawUnderPart(this, p, v, k);
                PAdd("draw:under", tdu); tdu = TS; if (Prof) VAdd("verts:(tło sceny)", p.VertexCount - vu); vu = p.VertexCount;
                if (k == nu - 1) { p.Identity(); Water?.Draw(p, v); }
                PAdd("draw:water", tdu); if (Prof) VAdd("verts:(woda)", p.VertexCount - vu);
            }
            else if (k == parts - 1) DrawTop(p, v);
            else { int a = _cut[k - nu], b = _cut[k - nu + 1]; for (int i = a; i < b; i++) DrawEnt(_sorted[i], p, v); }
            // wysyłka z wątku roboczego (serwer renderowania kolejkuje polecenia z innych wątków)
            long tf = TS;
            RenderingServer.CanvasItemClear(_items[2 * k]); RenderingServer.CanvasItemClear(_items[2 * k + 1]);
            pt.Flush(_items[2 * k], tex);
            p.Flush(_items[2 * k + 1]);
            PAdd("flush", tf);
            if (Prof && k < nu) { PAdd($"under{k}:flush", tf); PAdd($"under{k}:all", tpart); lock (VertT) VertT[$"verts:under{k}"] = (VertT.TryGetValue($"verts:under{k}", out var vv) ? vv : 0) + p.VertexCount + pt.VertexCount; }
            if (Prof) { string kind = k < nu ? $"tło {k}" : k == parts - 1 ? "nakładka" : $"encje {_sorted[_cut[k - nu]].GetType().Name}+ ({_cut[k - nu + 1] - _cut[k - nu]})"; double ms = (TS - tpart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; if (k >= nu && k < parts - 1) kind = $"encje (zakres {k - nu})"; lock (PartT) PartT[kind] = (PartT.TryGetValue(kind, out var q) ? q : 0) + ms; }
        });
        return parts * 2;
    }
    int[] _ord = Array.Empty<int>(); Rid[] _items = Array.Empty<Rid>();
    public int ParallelVertices { get { int n = 0; for (int k = 0; k < DrawParts && k < _wp.Length; k++) n += _wp[k].VertexCount + _wt[k].VertexCount; return n; } }
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
    /// <summary>Pary w twardym kontakcie z pierwszego przebiegu – rozwiązywane jeszcze raz (iteracje Gaussa–Seidla).</summary>
    static readonly List<(Ent a, Ent b)> _pairs = new();
    /// <summary>Dodatkowe przebiegi rozwiązywania kontaktów: ciało wciśnięte między dwie przeszkody nie „ucieka” z jednej w drugą.</summary>
    const int Iter = 2;
    static float[] _keys = new float[256]; static Ent[] _arr = new Ent[256];
    static List<(Ent, Ent, bool)>[] _cand = Array.Empty<List<(Ent, Ent, bool)>>();
    /// <summary>Kolizje metodą sort &amp; sweep; rozsuwanie proporcjonalne do "masy" (oporu).</summary>
    /// <summary>Obrót od zderzenia: dθ = (r × Δ) / (ℓ²/4), ℓ – długość ciała (opór obrotowy pręta ~ ℓ³, posuwisty ~ ℓ).</summary>
    static void Torque(Ent e, float cx, float cy, float dx, float dy)
    {
        if (e.Fixed || !e.Rotates) return;
        float l = Math.Max(e.Length, e.Radius * 2); if (l <= 0) return;
        float rx = cx - e.X, ry = cy - e.Y;
        float dth = (rx * dy - ry * dx) / (l * l * 0.25f) * 0.8f;
        e.A += Clamp(dth, -0.15f, 0.15f);
    }
    public static long CntY, CntPair, CntHit, CntN;
    public static void CollideAll(List<Ent> ents, World w = null)
    {
        _buf.Clear();
        int frame = w?.Frame ?? 0;
        foreach (var e in ents) { e.Nb.Clear(); if (e.Alive && e.Solid) { _buf.Add(e); e.Near = w == null || w.OnScreen(e, 1.8f); } }
        int cnt = _buf.Count; if (World.Prof) CntN += cnt;
        if (_keys.Length < cnt) { _keys = new float[cnt * 2]; _arr = new Ent[cnt * 2]; }
        for (int i = 0; i < cnt; i++) { _arr[i] = _buf[i]; _keys[i] = _buf[i].X - _buf[i].R; }
        Array.Sort(_keys, _arr, 0, cnt);
        _buf.Clear(); for (int i = 0; i < cnt; i++) _buf.Add(_arr[i]);
        _pairs.Clear();
        int n = _buf.Count;
        // faza 1 (wszystkie rdzenie): kandydaci z przeglądu wzdłuż osi X i test kształtów przy położeniach z początku przebiegu;
        // faza 2 (po kolei, jak dotąd): sąsiedztwo, zdarzenia i rozsuwanie – trafienia sprawdzane ponownie przy bieżących
        // położeniach, więc wynik jest taki sam jak w przebiegu jednowątkowym
        int chunks = n < 300 ? 1 : Math.Min(World.Workers * 2, n / 60);
        if (_cand.Length < chunks) { var c2 = new List<(Ent, Ent, bool)>[chunks]; for (int c = 0; c < chunks; c++) c2[c] = c < _cand.Length ? _cand[c] : new(); _cand = c2; }
        void Scan(int c)
        {
            var list = _cand[c]; list.Clear();
            int i0 = n * c / chunks, i1 = n * (c + 1) / chunks;
            for (int i = i0; i < i1; i++)
            {
                var a = _buf[i]; float ax1 = a.X + a.R;
                for (int j = i + 1; j < n; j++)
                {
                    var b = _buf[j];
                    if (b.X - b.R > ax1) break;
                    if (World.Prof) System.Threading.Interlocked.Increment(ref CntY);
                    if (MathF.Abs(a.Y - b.Y) > a.R + b.R) continue;
                    if (a.Fixed && b.Fixed) continue;
                    // daleko od kadru kontakty liczone co 4. klatkę (niewidoczne, 4× taniej)
                    if (!a.Near && !b.Near && ((frame + a.Id + b.Id) & 3) != 0) continue;
                    if (!a.Collides(b) || !b.Collides(a)) continue;
                    list.Add((a, b, Geo.EntEnt(a, b, out _, out _, out _)));
                }
            }
        }
        if (chunks == 1) Scan(0); else System.Threading.Tasks.Parallel.For(0, chunks, Scan);
        for (int c = 0; c < chunks; c++)
            foreach (var (a, b, hit0) in _cand[c])
            {
                a.Nb.Add(b); b.Nb.Add(a);
                if (World.Prof) CntPair++;
                if (!hit0 || !Geo.EntEnt(a, b, out float pen, out float nx, out float ny)) continue;
                if (World.Prof) CntHit++;
                if (a.Permeable(b) || b.Permeable(a))
                {
                    // przeciskanie się przez sieć włókien: opór zamiast twardej przeszkody
                    if (a.Permeable(b)) a.Seep(b, pen, nx, ny); else b.Seep(a, pen, -nx, -ny);
                    a.OnTouch(b, nx, ny); b.OnTouch(a, -nx, -ny);
                    continue;
                }
                float ma = a.Fixed ? 1e9f : a.Mass, mb = b.Fixed ? 1e9f : b.Mass;
                float wa = mb / (ma + mb), wb = ma / (ma + mb);
                // miękkie ciała: część penetracji to ściśnięcie (sprężyste, rozwiązywane powoli), reszta – twardy kontakt
                float give = (a.Soft * a.Prim.R + b.Soft * b.Prim.R) * 0.3f;
                float res = pen <= give ? pen * 0.22f : (pen - give) + give * 0.22f;
                float cX = Geo.ContactX, cY = Geo.ContactY;
                a.X -= nx * res * wa; a.Y -= ny * res * wa;
                b.X += nx * res * wb; b.Y += ny * res * wb;
                Torque(a, cX, cY, -nx * res * wa, -ny * res * wa);
                Torque(b, cX, cY, nx * res * wb, ny * res * wb);
                a.ShiftPrim(-nx * res * wa, -ny * res * wa); b.ShiftPrim(nx * res * wb, ny * res * wb);
                a.OnTouch(b, nx, ny); b.OnTouch(a, -nx, -ny);
                _pairs.Add((a, b));
            }
        // kolejne przebiegi: tylko twarda część penetracji (miękkie wgniecenie zostaje – jest fizyczne), bez zdarzeń i obrotu
        for (int it = 0; it < Iter && _pairs.Count > 0; it++)
            foreach (var (a, b) in _pairs)
            {
                if (!a.Alive || !b.Alive || !a.Solid || !b.Solid) continue;
                if (!Geo.EntEnt(a, b, out float pen, out float nx, out float ny)) continue;
                float give = (a.Soft * a.Prim.R + b.Soft * b.Prim.R) * 0.3f, res = pen - give;
                if (res <= 0) continue;
                float ma = a.Fixed ? 1e9f : a.Mass, mb = b.Fixed ? 1e9f : b.Mass;
                float wa = mb / (ma + mb), wb = ma / (ma + mb);
                a.X -= nx * res * wa; a.Y -= ny * res * wa;
                b.X += nx * res * wb; b.Y += ny * res * wb;
                a.ShiftPrim(-nx * res * wa, -ny * res * wa); b.ShiftPrim(nx * res * wb, ny * res * wb);
            }
    }
}

namespace Mikro;

public sealed class InputState
{
    public float Dx, Dy, Mag, Ang;
    public bool Boost, Action, ActionHeld;
}

/// <summary>Jednorodna siatka przestrzenna (listy łączone w tablicach, bez alokacji na klatkę).</summary>
public sealed class SpatialGrid
{
    float _cell = 1, _w, _h; int _nx, _ny;
    int[] _head = Array.Empty<int>(), _next = new int[256];
    Ent[] _items = new Ent[256];
    public bool Ready;

    public void Build(List<Ent> ents, float w, float h, float cell)
    {
        if (_head.Length == 0 || _w != w || _h != h || _cell != cell)
        {
            _w = w; _h = h; _cell = cell;
            _nx = Math.Max(1, (int)MathF.Ceiling(w / cell)); _ny = Math.Max(1, (int)MathF.Ceiling(h / cell));
            _head = new int[_nx * _ny];
        }
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
    int Cell(float x, float y)
    {
        int i = (int)(x / _cell), j = (int)(y / _cell);
        i = i < 0 ? 0 : i >= _nx ? _nx - 1 : i; j = j < 0 ? 0 : j >= _ny ? _ny - 1 : j;
        return j * _nx + i;
    }
    /// <summary>Encje, których środki leżą w komórkach pokrywających kwadrat (x±r, y±r).</summary>
    public void Query(float x, float y, float r, List<Ent> outp)
    {
        outp.Clear();
        int i0 = Math.Max(0, (int)((x - r) / _cell)), i1 = Math.Min(_nx - 1, (int)((x + r) / _cell));
        int j0 = Math.Max(0, (int)((y - r) / _cell)), j1 = Math.Min(_ny - 1, (int)((y + r) / _cell));
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
                for (int k = _head[j * _nx + i]; k >= 0; k = _next[k]) outp.Add(_items[k]);
    }
}

/// <summary>Świat symulacji: encje, kolizje, efekty, prądy, zegary, cele, statystyki.</summary>
public sealed class World
{
    public readonly Scenario Sc;
    public readonly float W, H;
    public readonly List<Ent> Ents = new();
    public readonly FX Fx = new();
    public readonly SpatialGrid Grid = new();
    public float T, SimT, TimeScale, Bio, Ff = 1;
    public Ent Player, Focus;
    public bool Over, Preview, ShowField, AllGoals;
    public int GoalIdx;
    public readonly Dictionary<string, float> Stats = new();
    public InputState Input = new();
    public Field Nutri, Amp;
    public BactField Bact;
    public int Wave = 1;
    public float CamX, CamY, ViewR = 50;
    public bool Em => Sc.Em;

    public Action<string, string> OnLog;
    public Action<string, string> OnBanner;
    public Action<string, bool> OnGameOver;
    public Action<string, float> OnSfx;
    public Action<Scenario.Goal> OnGoal;

    public struct Debris { public float X, Y, S, Al, D; public int K; }
    public readonly List<Debris> DebrisList = new();

    readonly List<(float t, Action a)> _timers = new();
    readonly List<Ent> _sorted = new(), _near = new();

    // pole prądu wody (siatka odświeżana co 0,5 s)
    float[] _fx, _fy; int _fnx, _fny; float _fAcc = 99; int _dPhase;

    public World(Scenario sc, bool preview = false)
    {
        Sc = sc; Preview = preview;
        W = preview ? 1e6f : sc.Wd; H = preview ? 1e6f : sc.Ht;
        TimeScale = sc.TimeScale; Bio = sc.Bio;
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

    public float Stat(string k) => Stats.TryGetValue(k, out var v) ? v : 0;
    public void AddStat(string k, float v) => Stats[k] = Stat(k) + v;

    public int Count(Func<Ent, bool> fn) { int n = 0; foreach (var e in Ents) if (e.Alive && fn(e)) n++; return n; }

    /// <summary>Najbliższa encja spełniająca warunek (siatka przestrzenna dla małych promieni).</summary>
    public Ent Near(float x, float y, float r, Func<Ent, bool> fn)
    {
        Ent best = null; float bd = r * r;
        if (Grid.Ready && r < Math.Max(W, H) * 0.3f)
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
    /// <summary>Encje w pobliżu (do własnej listy wywołującego).</summary>
    public void Query(float x, float y, float r, List<Ent> outp)
    {
        if (Grid.Ready) Grid.Query(x, y, r, outp);
        else { outp.Clear(); outp.AddRange(Ents); }
    }

    public void SetPlayer(Ent e)
    {
        if (Player != null && Player != e) Player.Lineage = true;
        Player = e;
        if (e != null) { e.Lineage = true; Focus = null; }
    }
    /// <summary>Gracz zginął – przejmij potomka ze swojej linii.</summary>
    public bool Adopt(Kind kind, float x, float y)
    {
        var e = Near(x, y, 1e9f, o => o.Kind == kind && o.Lineage && o != Player && o.CanControl());
        if (e != null) { Player = null; SetPlayer(e); Log("Przejmujesz kontrolę nad potomkiem z Twojej linii.", "good"); return true; }
        return false;
    }

    // ---------------------------------------------------------------- prąd wody
    public void FlowAt(float x, float y, out float fx, out float fy)
    {
        fx = fy = 0;
        if (_fx == null) return;
        float gx = Clamp(x / W * (_fnx - 1), 0, _fnx - 1.001f), gy = Clamp(y / H * (_fny - 1), 0, _fny - 1.001f);
        int i = (int)gx, j = (int)gy; float u = gx - i, v = gy - j;
        int a = j * _fnx + i, b = a + 1, c = a + _fnx, d = c + 1;
        fx = (_fx[a] * (1 - u) + _fx[b] * u) * (1 - v) + (_fx[c] * (1 - u) + _fx[d] * u) * v;
        fy = (_fy[a] * (1 - u) + _fy[b] * u) * (1 - v) + (_fy[c] * (1 - u) + _fy[d] * u) * v;
    }
    void UpdateFlow(float gdt)
    {
        if (Sc.Flow <= 0) return;
        _fAcc += gdt;
        if (_fAcc < 0.5f) return;
        _fAcc = 0;
        if (_fx == null) { _fnx = 48; _fny = Math.Max(2, (int)(48 * H / W)); _fx = new float[_fnx * _fny]; _fy = new float[_fnx * _fny]; }
        // funkcja prądu ψ → pole bezźródłowe v = (∂ψ/∂y, −∂ψ/∂x)
        float L = Sc.FlowScale, k1 = TAU / L, k2 = TAU / (L * 0.53f), t = SimT * 0.002f;
        for (int j = 0; j < _fny; j++)
            for (int i = 0; i < _fnx; i++)
            {
                float x = i * W / (_fnx - 1), y = j * H / (_fny - 1);
                // ψ = [sin(0.8·k1·x + t)·sin(k1·y + 1.3) + 0.5·sin(k2·y − 1.3t)·sin(0.7·k2·x + 2.1)] / k1
                float a1 = 0.8f * k1 * x + t, b1 = k1 * y + 1.3f, a2 = k2 * y - 1.3f * t, b2 = 0.7f * k2 * x + 2.1f;
                float dpdy = MathF.Sin(a1) * k1 * MathF.Cos(b1) + 0.5f * k2 * MathF.Cos(a2) * MathF.Sin(b2);
                float dpdx = 0.8f * k1 * MathF.Cos(a1) * MathF.Sin(b1) + 0.5f * MathF.Sin(a2) * 0.7f * k2 * MathF.Cos(b2);
                int q = j * _fnx + i;
                _fx[q] = dpdy / k1 * Sc.Flow; _fy[q] = -dpdx / k1 * Sc.Flow;
            }
    }

    // ---------------------------------------------------------------- pętla
    public static bool Prof;
    public static readonly Dictionary<string, double> ProfT = new();
    static void PAdd(string k, long t0) { if (!Prof) return; double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; ProfT[k] = (ProfT.TryGetValue(k, out var v) ? v : 0) + ms; }
    static long TS => System.Diagnostics.Stopwatch.GetTimestamp();

    public void Update(float fdt)
    {
        float dt = fdt * TimeScale * Ff, gdt = fdt * Ff;
        T += gdt; SimT += dt;
        for (int i = _timers.Count - 1; i >= 0; i--)
            if (_timers[i].t <= T) { var a = _timers[i].a; _timers.RemoveAt(i); a(); }
        long t0 = TS;
        if (!Preview) Grid.Build(Ents, W, H, Sc.GridCell);
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
            e.Integrate(dt);
            if (!Preview)
            {
                if (_fx != null && e.FlowK > 0 && !e.Fixed) { FlowAt(e.X, e.Y, out float fx, out float fy); e.X += fx * dt * e.FlowK; e.Y += fy * dt * e.FlowK; }
                float m = Math.Min(e.R, 30);
                if (e.X < m) e.X += (m - e.X) * 0.5f; else if (e.X > W - m) e.X -= (e.X - W + m) * 0.5f;
                if (e.Y < m) e.Y += (m - e.Y) * 0.5f; else if (e.Y > H - m) e.Y -= (e.Y - H + m) * 0.5f;
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
                if (e.MemS > 0) foreach (var o in e.Nb) if (o.Alive && o.Solid) e.Mem.PushOut(o.Prim, e.MemS);
            }
            e.Post(dt, gdt);
        }
        PAdd("membranes+post", t0); t0 = TS;
        _dPhase = (_dPhase + 1) % 3;
        float d3 = dt * 3;
        for (int i = _dPhase; i < DebrisList.Count; i += 3)
        {
            var d = DebrisList[i];
            float s = MathF.Sqrt(2 * d.D * d3);
            d.X += s * RandN() + Sc.FlowX * d3; d.Y += s * RandN() + Sc.FlowY * d3;
            if (_fx != null) { FlowAt(d.X, d.Y, out float fx, out float fy); d.X += fx * d3; d.Y += fy * d3; }
            if (d.X < 0) d.X += W; else if (d.X > W) d.X -= W;
            if (d.Y < 0) d.Y += H; else if (d.Y > H) d.Y -= H;
            DebrisList[i] = d;
        }
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

    void CheckGoals()
    {
        var G = Sc.Goals;
        if (G == null || AllGoals || Over) return;
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
        if (Over) return;
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
        foreach (var e in Ents) if (e.Alive && v.Vis(e.X, e.Y, e.R * 1.6f + e.DrawR)) _sorted.Add(e);
        _sorted.Sort((a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Id.CompareTo(b.Id));
        foreach (var e in _sorted) { p.Identity(); p.Alpha = 1; e.Draw(p, v); }
        p.Identity(); p.Alpha = 1;
        Fx.Draw(p, v);
        p.Identity();
        Sc.DrawOver(this, p, v);
        DrawBounds(p, v);
    }
    /// <summary>Przyciemnienie poza granicą pola (brzeg preparatu).</summary>
    void DrawBounds(Painter p, View v)
    {
        if (Preview) return;
        var dark = new Color(0, 0, 0, 0.45f); var clear = new Color(0, 0, 0, 0);
        float fe = Math.Max(W, H) * 0.02f;
        if (v.X0 < 0) { p.Rect(v.X0, v.Y0, -v.X0, v.Y1 - v.Y0, dark); }
        if (v.X1 > W) { p.Rect(W, v.Y0, v.X1 - W, v.Y1 - v.Y0, dark); }
        if (v.Y0 < 0) { p.Rect(Math.Max(0, v.X0), v.Y0, Math.Min(W, v.X1) - Math.Max(0, v.X0), -v.Y0, dark); }
        if (v.Y1 > H) { p.Rect(Math.Max(0, v.X0), H, Math.Min(W, v.X1) - Math.Max(0, v.X0), v.Y1 - H, dark); }
        if (v.X0 < 0 || v.X1 > W || v.Y0 < 0 || v.Y1 > H)
        {
            Span<Vector2> r = stackalloc Vector2[] { new(0, 0), new(W, 0), new(W, H), new(0, H) };
            p.SoftStroke(r, true, fe, new Color(0, 0, 0, 0.3f));
        }
    }

    public void DrawDebris(Painter pt, View v, Rect2[] uv, Color[] cols)
    {
        pt.Identity();
        foreach (var d in DebrisList)
        {
            if (!v.Vis(d.X, d.Y, d.S * 2)) continue;
            pt.Sprite(d.X, d.Y, d.S * 2, d.S * 2, 0, uv[d.K], Mul(cols[d.K % cols.Length], d.Al));
        }
    }

    public void MakeDebris(int n, float s0, float s1, float D)
    {
        for (int i = 0; i < n; i++)
            DebrisList.Add(new Debris { X = Rand(0, W), Y = Rand(0, H), S = Rand(s0, s1), K = RandI(0, 5), Al = Rand(0.25f, 0.7f), D = D });
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
                a.X -= nx * pen * wa; a.Y -= ny * pen * wa;
                b.X += nx * pen * wb; b.Y += ny * pen * wb;
                a.UpdatePrim(); b.UpdatePrim();
                a.OnTouch(b, nx, ny); b.OnTouch(a, -nx, -ny);
            }
        }
    }
}

namespace Mikro;

public enum St
{
    None, Free, Run, Tumble, Cup, Vac, Eaten, Caught, Swim, Avoid, Divide, Attach, Enter, Grow, Septate,
    Bind, Contract, Inject, Ghost, Debris, Dead, Bdelloplast, Infected, Capture, Swallow, Digest, Cyst,
}

public enum Kind { None, EColi, Phage, Bdello, Staph, Neutro, Rbc, Plt, Lympho, Para, Didi, Chlamy, Diatom, Cosm, Clost, Floc, Amoeba, Rotifer, Acanth, Protist, Metazoan, Microbe, Particle, TissueCell, Fungus, Bot }

/// <summary>Widoczny obszar świata (µm) + rozmiar piksela.</summary>
public sealed class View
{
    public float X0, Y0, X1, Y1, Px = 1, S = 1, T;
    public bool Vis(float x, float y, float r) => x + r > X0 && x - r < X1 && y + r > Y0 && y - r < Y1;
}

/// <summary>Bazowa encja symulacji (dynamika przetłumiona – brak bezwładności, niska liczba Reynoldsa).</summary>
public abstract class Ent
{
    static int _nextId = 1;
    public World W;
    public readonly int Id;
    public Kind Kind;
    public St State = St.None;
    public float X, Y, A, Vx, Vy;
    public float Mass = 1, R = 1, DrawR, D, Dr;
    public float MemK = 10, MemS = 0.6f;
    public bool Alive = true, Solid = true, Fixed, Lineage, Edible;
    public int Layer = 1;
    public readonly Prim Prim = new();
    public readonly List<Ent> Nb = new();
    public Membrane Mem;
    public float Food, Dig;
    /// <summary>Jak silnie encję unosi prąd wody (0 = zakotwiczona).</summary>
    public float FlowK = 1;
    /// <summary>Zdrowie / żywotność 0..1 (używane przez część organizmów).</summary>
    public float Hp = 1;
    /// <summary>Encja, w której/na której ta się znajduje (pożeracz, gospodarz) – zamrażane razem.</summary>
    public Ent Holder;
    /// <summary>Chunk, w którym encja jest zamrożona (null = aktywna).</summary>
    public Chunk ParkedIn;
    public string Name = "pokarm";
    /// <summary>Nazwa łacińska (skaner nanobota).</summary>
    public string Latin = "";
    /// <summary>
    /// Miękkość ciała: 0 = sztywne (krzemionka, ziarno, pancerz), 1 = bardzo miękkie (ameba).
    /// Miękkie ciała mogą się w siebie wcisnąć – kontakt rozwiązywany jest częściowo, a membrany się spłaszczają.
    /// </summary>
    public float Soft;

    /// <summary>Opis do skanera (nazwa, rozmiar).</summary>
    public virtual string Info
    {
        get
        {
            float s = Math.Max(Length, Radius * 2);
            string sz = s >= 1000 ? $"{s / 1000:0.0#} mm" : s >= 1 ? $"{s:0.#} µm" : $"{s * 1000:0} nm";
            return Latin.Length > 0 ? $"{Name} ({Latin}) · {sz}" : $"{Name} · {sz}";
        }
    }
    /// <summary>Stan "wolny" encji (po uwolnieniu z drapieżnika).</summary>
    public virtual St FreeState => St.Free;
    /// <summary>Uwolnienie z wnętrza/uchwytu martwego drapieżnika lub gospodarza.</summary>
    public virtual void Unhold()
    {
        Holder = null; Solid = true; Dig = 0;
        if (State is St.Cup or St.Vac or St.Caught or St.Eaten or St.Capture) State = FreeState;
    }
    /// <summary>Uwalnia wszystko, co ta encja trzyma (ofiary w wodniczkach, pasożyty).</summary>
    public void ReleaseHeld()
    {
        foreach (var e in W.Ents) if (e.Alive && e.Holder == this) e.Unhold();
    }
    /// <summary>Obrażenia (0..1 = cała integralność). Przy Hp ≤ 0 – śmierć.</summary>
    public void Damage(float amt)
    {
        if (!Alive || amt <= 0) return;
        Hp -= amt;
        if (Hp <= 0) { Hp = 0; Kill(); }
    }
    /// <summary>Śmierć komórki z efektami (pęknięcie błony, wyciek cytoplazmy).</summary>
    public virtual void Kill()
    {
        if (!Alive) return;
        ReleaseHeld();
        float s = Math.Max(Math.Max(Radius, Length * 0.5f), R * 0.3f);
        bool em = W.Em;
        if (Mem != null)
            W.Fx.Lyse(Mem, new FX.LyseOpt
            {
                Pieces = RandI(4, 7), Speed = s * 0.5f, Life = 6, Lw = s * 0.025f,
                C = em ? Rgb(170, 215, 230, 0.9f) : Rgb(40, 46, 40, 0.7f), Halo = Rgb(255, 255, 240, 0.2f), HasHalo = !em,
                Curl = 0.6f, D = s * s * 0.0004f, Ring = s * 0.8f, RingCol = em ? Rgb(170, 215, 230) : Rgb(255, 255, 240),
            });
        W.Fx.Spill(X, Y, 26, new FX.SpillOpt
        {
            Speed = s * 0.7f, R = s * 0.035f, Life = 4.5f, R0 = s * 0.6f, D = s * s * 0.0006f,
            Cols = em ? new[] { Rgb(160, 200, 215, 0.8f), Rgb(120, 160, 180, 0.8f) } : new[] { Rgb(50, 56, 46, 0.65f), Rgb(225, 230, 210, 0.5f) },
        });
        W.Sfx("pop", X, Y, 0.8f);
        Alive = false;
    }

    /// <summary>Promień "ofiary" (jak o.r w wersji web).</summary>
    public virtual float Radius => R;
    /// <summary>Długość ciała (jak o.L), 0 gdy nie dotyczy.</summary>
    public virtual float Length => 0;

    protected Ent(World w, float x, float y, float a) { W = w; Id = _nextId++; X = x; Y = y; A = a; Prim.X = x; Prim.Y = y; }

    public bool IsPlayer => W != null && W.Player == this;

    /// <summary>
    /// Kształt złożony: kilka kapsuł/kół odwzorowujących rysunek (ciało wygięte, stożkowe, z nibynóżkami).
    /// NParts = 0 → kolizja tylko przez Prim.
    /// </summary>
    public Prim[] Parts; public int NParts;
    public int PartCount => NParts > 0 ? NParts : 1;
    public Prim Part(int i) => NParts > 0 ? Parts[i] : Prim;
    protected void UseParts(int n)
    {
        if (Parts == null || Parts.Length < n) { Parts = new Prim[n]; for (int i = 0; i < n; i++) Parts[i] = new Prim(); }
        NParts = n;
    }
    /// <summary>Kapsuła części i od punktu lokalnego (x0,y0) do (x1,y1) o promieniu r.</summary>
    protected void PartCap(int i, float x0, float y0, float x1, float y1, float r)
    {
        var p = Parts[i]; ToWorld(x0, y0, out p.Ax, out p.Ay); ToWorld(x1, y1, out p.Bx, out p.By);
        p.Cap = true; p.R = r; p.X = (p.Ax + p.Bx) / 2; p.Y = (p.Ay + p.By) / 2;
    }
    protected void PartCircle(int i, float x, float y, float r)
    {
        var p = Parts[i]; ToWorld(x, y, out p.X, out p.Y); p.Cap = false; p.R = r;
    }
    /// <summary>Części wzdłuż (wygiętej) osi ciała: profil hw(t) dla t∈[-1,1], oś y = bend·x² + yOff(t).</summary>
    protected void AxisParts(int n, float hl, float hw, Func<float, float> prof, float bend, Func<float, float> yOff = null)
    {
        UseParts(n);
        for (int k = 0; k < n; k++)
        {
            float t0 = -1 + 2f * k / n, t1 = -1 + 2f * (k + 1) / n, tm = (t0 + t1) / 2;
            float r = Math.Max(hw * 0.08f, hw * Math.Max(prof(tm), 0.6f * Math.Max(prof(t0), prof(t1))) * 0.93f);
            float x0 = t0 * hl, x1 = t1 * hl;
            if (k == 0) x0 = Math.Min(-hl + r, x1); if (k == n - 1) x1 = Math.Max(hl - r, x0);
            float y0 = bend * x0 * x0 + (yOff?.Invoke(x0 / hl) ?? 0), y1 = bend * x1 * x1 + (yOff?.Invoke(x1 / hl) ?? 0);
            PartCap(k, x0, y0, x1, y1, r);
        }
        var m = Parts[n / 2]; Prim.Cap = m.Cap; Prim.Ax = m.Ax; Prim.Ay = m.Ay; Prim.Bx = m.Bx; Prim.By = m.By; Prim.R = m.R; Prim.X = m.X; Prim.Y = m.Y;
    }
    /// <summary>Części dla kształtu gwiaździstego rr (kąty lokalne i·2π/nn): rdzeń + "szprychy" w wypukłościach (nibynóżki, płaty).</summary>
    protected void RadialParts(float[] rr, int nn, int k)
    {
        float mn = float.MaxValue; for (int i = 0; i < nn; i++) mn = Math.Min(mn, rr[i]);
        float r0 = mn * 0.92f;
        UseParts(k + 1);
        PartCircle(0, 0, 0, r0);
        for (int j = 0; j < k; j++)
        {
            float th = (j + 0.5f) * TAU / k, rk = RadialUtil.Interp(rr, nn, th);
            float rs = Clamp(rk * 0.32f, r0 * 0.3f, r0 * 0.85f), d = Math.Max(0, rk * 0.97f - rs);
            PartCap(j + 1, 0, 0, MathF.Cos(th) * d, MathF.Sin(th) * d, rs);
        }
        Prim.Cap = false; Prim.X = X; Prim.Y = Y; Prim.R = r0;
    }
    /// <summary>Ciało przepuszczalne dla o (np. sieć kolagenu dla pełzających leukocytów): kontakt stawia opór, ale nie blokuje.</summary>
    public virtual bool Permeable(Ent o) => false;

    public void SetCircle(float r) { NParts = 0; Prim.Cap = false; Prim.X = X; Prim.Y = Y; Prim.R = r; }
    public void SetCapsule(float hl, float r)
    {
        NParts = 0;
        float c = MathF.Cos(A) * hl, s = MathF.Sin(A) * hl;
        Prim.Cap = true; Prim.Ax = X - c; Prim.Ay = Y - s; Prim.Bx = X + c; Prim.By = Y + s; Prim.R = r;
    }
    public virtual void UpdatePrim() => SetCircle(R);

    public virtual void Integrate(float dt)
    {
        X += Vx * dt; Y += Vy * dt;
        if (D > 0) { float s = MathF.Sqrt(2 * D * dt); X += s * RandN(); Y += s * RandN(); }
        if (Dr > 0) A += MathF.Sqrt(2 * Dr * dt) * RandN();
    }
    public void ToLocal(float x, float y, out float lx, out float ly)
    {
        float c = MathF.Cos(-A), s = MathF.Sin(-A), dx = x - X, dy = y - Y;
        lx = dx * c - dy * s; ly = dx * s + dy * c;
    }
    public void ToWorld(float lx, float ly, out float x, out float y)
    {
        float c = MathF.Cos(A), s = MathF.Sin(A);
        x = X + lx * c - ly * s; y = Y + lx * s + ly * c;
    }
    /// <summary>Obraca wektor lokalny do świata (bez przesunięcia).</summary>
    public void DirToWorld(float lx, float ly, out float x, out float y)
    {
        float c = MathF.Cos(A), s = MathF.Sin(A);
        x = lx * c - ly * s; y = lx * s + ly * c;
    }

    public virtual void Step(float dt, float gdt) { }
    public virtual void Post(float dt, float gdt) { }
    public abstract void Draw(Painter p, View v);
    public virtual bool Collides(Ent o) => true;
    public virtual void OnTouch(Ent o, float nx, float ny) { }
    public virtual bool CanControl() => true;
}

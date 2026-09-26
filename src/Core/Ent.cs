namespace Mikro;

public enum St
{
    None, Free, Run, Tumble, Cup, Vac, Eaten, Caught, Swim, Avoid, Divide, Attach, Enter, Grow, Septate,
    Bind, Contract, Inject, Ghost, Debris, Dead, Bdelloplast, Infected, Capture, Swallow, Digest, Cyst,
}

public enum Kind { None, EColi, Phage, Bdello, Staph, Neutro, Rbc, Plt, Lympho, Para, Didi, Chlamy, Diatom, Cosm, Clost, Floc, Amoeba, Rotifer, Acanth }

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

    /// <summary>Promień "ofiary" (jak o.r w wersji web).</summary>
    public virtual float Radius => R;
    /// <summary>Długość ciała (jak o.L), 0 gdy nie dotyczy.</summary>
    public virtual float Length => 0;

    protected Ent(World w, float x, float y, float a) { W = w; Id = _nextId++; X = x; Y = y; A = a; Prim.X = x; Prim.Y = y; }

    public bool IsPlayer => W != null && W.Player == this;

    public void SetCircle(float r) { Prim.Cap = false; Prim.X = X; Prim.Y = Y; Prim.R = r; }
    public void SetCapsule(float hl, float r)
    {
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

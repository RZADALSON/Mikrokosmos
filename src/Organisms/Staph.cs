namespace Mikro;

/// <summary>Staphylococcus aureus – ziarniak ~0,9 µm, nieruchomy; podział w 3 prostopadłych płaszczyznach → "grona".</summary>
public sealed class Staph : Ent
{
    public float Rr, Cyc, Plane, Div, Gen;
    /// <summary>Streptococcus: podział zawsze w tej samej płaszczyźnie → łańcuszki.</summary>
    public bool Strep;
    public Ent Owner;

    public override float Radius => Rr;

    public Staph(World w, float x, float y, float cyc = -1, float plane = float.NaN, bool strep = false) : base(w, x, y, Rand(0, TAU))
    {
        Strep = strep;
        Kind = Kind.Staph; Layer = 3; Name = strep ? "paciorkowiec" : "gronkowiec złocisty"; Latin = strep ? "Streptococcus pyogenes" : "Staphylococcus aureus";
        Rr = strep ? Rand(0.36f, 0.42f) : Rand(0.42f, 0.5f); R = Rr + 0.35f; Mass = 0.25f; D = 0.002f;
        State = St.Free; Cyc = cyc >= -0.5f ? cyc : Rand(0, 0.9f); Plane = float.IsNaN(plane) ? Rand(0, TAU) : plane;
        Gen = Rand(0.85f, 1.2f);
    }
    public override void UpdatePrim() => SetCircle(Rr * (1 + 0.35f * Div));
    public override bool Collides(Ent o) => State == St.Free;

    public override void Step(float dt, float gdt)
    {
        if (State == St.Caught)
        {
            Dig += gdt / 40;
            if (Dig >= 1) { Alive = false; W.AddStat("killed", 1); }
            return;
        }
        if (State != St.Free || W.Preview) return;
        float bdt = dt * W.Bio;
        if (Div > 0 || Cyc >= 1) { Div += bdt / 300; if (Div >= 1) Split(); }
        else Cyc += bdt / (1800 * Gen);
    }
    void Split()
    {
        float ux = MathF.Cos(Plane), uy = MathF.Sin(Plane), d = Rr * 0.62f;
        for (int s = -1; s <= 1; s += 2)
        {
            var e = W.Add(new Staph(W, X + ux * d * s, Y + uy * d * s, Rand(-0.1f, 0.1f) + 0.1f, Strep ? Plane + RandN() * 0.08f : Plane + PI / 2, Strep));
            e.Rr = Rr;
        }
        Alive = false;
    }
    public override void Post(float dt, float gdt)
    {
        if (State != St.Free) return;
        foreach (var o in Nb)
        {
            if (o is not Staph s || s.State != St.Free) continue;
            float dx = o.X - X, dy = o.Y - Y, d = Hypot(dx, dy); if (d < 1e-6f) continue;
            float gap = d - Rr - s.Rr;
            if (gap > 0 && gap < 0.3f) { float k = gap * 0.08f; X += dx / d * k; Y += dy / d * k; }
        }
    }

    static readonly Color Halo = Rgb(255, 255, 240, 0.45f), In = Rgb(82, 84, 76), Out = Rgb(22, 24, 22);

    void Coccus(Painter p, float x, float y, float r, float al)
    {
        p.Glow(x, y, r * 2.2f, Mul(Halo, al * 0.8f));
        Span<float> ts = stackalloc float[] { 0f, 0.7f, 1f };
        Span<Color> cs = stackalloc Color[] { Mul(In, al), Mul(Rgb(40, 42, 38), al), Mul(Out, al) };
        int n = p.CirclePts(x - r * 0.0f, y, r, p.Buf, 20);
        p.FillRadial(new ReadOnlySpan<Vector2>(p.Buf, 0, n), x - r * 0.25f, y - r * 0.25f, ts, cs);
    }

    public override void Draw(Painter p, View v)
    {
        float al = 1 - Dig * 0.85f, sc = 1 - Dig * 0.4f;
        if (Div > 0.02f)
        {
            float ux = MathF.Cos(Plane), uy = MathF.Sin(Plane), d = Rr * 0.62f * Div;
            Coccus(p, X - ux * d, Y - uy * d, Rr, al);
            Coccus(p, X + ux * d, Y + uy * d, Rr, al);
            p.Local(X, Y, Plane);
            p.Line(0, -Rr * 0.8f, 0, Rr * 0.8f, 0.05f, Rgb(10, 10, 10, 0.8f * al));
        }
        else Coccus(p, X, Y, Rr * sc, al);
    }
}

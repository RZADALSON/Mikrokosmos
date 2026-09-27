namespace Mikro;

/// <summary>Efekty: rozlana cytoplazma, fragmenty błon zwijające się w pęcherzyki, fale, igły (tricho-/toksycysty).</summary>
public sealed class FX
{
    public sealed class Bit { public float X, Y, Vx, Vy, R, Life, Max, D; public Color C; }
    public sealed class Frag
    {
        public float X, Y, Vx, Vy, A, Va, Life, Max, Lw, Curl, D; public int M;
        public float[] Lx, Ly, Tx, Ty; public Color C, Halo; public bool HasHalo;
    }
    public sealed class RingFx { public float X, Y, R, Vr, Life, Max, Lw; public Color C; }
    public sealed class Needle { public float X, Y, A, Len, Tl, Life, Max; public Color C; }

    public readonly List<Bit> Bits = new();
    public readonly List<Frag> Frags = new();
    public readonly List<RingFx> Rings = new();
    public readonly List<Needle> Needles = new();
    /// <summary>Prędkość wody w punkcie (µm/s czasu biologicznego) i przelicznik czasu – drobiny i fragmenty błon płyną z wodą.</summary>
    public Func<float, float, Vector2> Flow; public float FlowScale;

    public struct SpillOpt
    {
        public float Speed, R, Life, R0, D, Spread; public float? Dir; public Color[] Cols;
    }

    public void Spill(float x, float y, int n, SpillOpt o)
    {
        for (int i = 0; i < n; i++)
        {
            float a = o.Dir.HasValue ? o.Dir.Value + RandN() * (o.Spread > 0 ? o.Spread : 0.8f) : Rand(0, TAU);
            float sp = o.Speed * Rand(0.2f, 1f);
            float r0 = o.R0 * MathF.Sqrt(R01());
            float life = o.Life * Rand(0.6f, 1.3f);
            Bits.Add(new Bit
            {
                X = x + MathF.Cos(a) * r0, Y = y + MathF.Sin(a) * r0, Vx = MathF.Cos(a) * sp, Vy = MathF.Sin(a) * sp,
                R = o.R * Rand(0.5f, 1.4f), Life = life, Max = life, C = Pick(o.Cols), D = o.D,
            });
        }
    }

    public struct LyseOpt
    {
        public int Pieces; public float Speed, Life, Lw, Curl, Spin, D, Ring; public Color C, Halo, RingCol; public bool HasHalo;
    }

    /// <summary>Rozerwanie membrany: kawałki odlatują i zwijają się w pęcherzyki (liza eksplozywna → MV).</summary>
    public void Lyse(Membrane mem, LyseOpt o)
    {
        int n = mem.N;
        mem.Centroid(out float cx, out float cy);
        int pieces = o.Pieces > 0 ? o.Pieces : RandI(3, 6);
        int start = RandI(0, n - 1);
        var cuts = new int[pieces + 1];
        for (int k = 0; k <= pieces; k++)
            cuts[k] = start + (int)MathF.Round(k * (float)n / pieces + (k > 0 && k < pieces ? Rand(-0.3f, 0.3f) * n / pieces : 0));
        for (int k = 0; k < pieces; k++)
        {
            int i0 = cuts[k], i1 = cuts[k + 1] - 1;
            int m = Math.Max(2, i1 - i0 + 1);
            var lx = new float[m]; var ly = new float[m]; var tx = new float[m]; var ty = new float[m];
            float fx = 0, fy = 0;
            for (int q = 0; q < m; q++) { int i = (i0 + q) % n; lx[q] = mem.Px[i]; ly[q] = mem.Py[i]; fx += lx[q]; fy += ly[q]; }
            fx /= m; fy /= m;
            float len = 0;
            for (int q = 0; q < m; q++) { lx[q] -= fx; ly[q] -= fy; if (q > 0) len += Hypot(lx[q] - lx[q - 1], ly[q] - ly[q - 1]); }
            const float close = 0.92f;
            float rad = len / (TAU * close);
            float a0 = MathF.Atan2(ly[0], lx[0]);
            float cross = lx[0] * ly[m - 1] - ly[0] * lx[m - 1];
            float dir = cross >= 0 ? 1 : -1;
            for (int q = 0; q < m; q++) { float ang = a0 + dir * ((float)q / (m - 1)) * TAU * close; tx[q] = MathF.Cos(ang) * rad; ty[q] = MathF.Sin(ang) * rad; }
            float ox = fx - cx, oy = fy - cy, ol = Hypot(ox, oy); if (ol < 1e-9f) ol = 1; ox /= ol; oy /= ol;
            float sp = o.Speed * Rand(0.5f, 1.2f), life = o.Life * Rand(0.8f, 1.2f);
            Frags.Add(new Frag
            {
                X = fx, Y = fy, Vx = ox * sp, Vy = oy * sp, A = 0, Va = Rand(-1, 1) * (o.Spin > 0 ? o.Spin : 1.5f), M = m,
                Lx = lx, Ly = ly, Tx = tx, Ty = ty, Life = life, Max = life, Lw = o.Lw, C = o.C, Halo = o.Halo, HasHalo = o.HasHalo,
                Curl = Rand(0.5f, 1.3f) * (o.Curl > 0 ? o.Curl : 1), D = o.D,
            });
        }
        if (o.Ring > 0) RingAt(cx, cy, o.Ring, o.RingCol, o.Ring * 0.08f);
    }

    public void RingAt(float x, float y, float r, Color c, float lw, float life = 0.6f) =>
        Rings.Add(new RingFx { X = x, Y = y, R = r * 0.3f, Vr = r * 2.2f, Life = life, Max = life, C = c, Lw = lw });

    public void NeedleAt(float x, float y, float a, float len, Color c, float life) =>
        Needles.Add(new Needle { X = x, Y = y, A = a, Len = 0, Tl = len, C = c, Life = life, Max = life });

    public void Update(float fdt)
    {
        float dec = MathF.Exp(-5 * fdt);
        for (int i = Bits.Count - 1; i >= 0; i--)
        {
            var b = Bits[i];
            b.Vx *= dec; b.Vy *= dec; b.X += b.Vx * fdt; b.Y += b.Vy * fdt;
            if (Flow != null && FlowScale > 0) { var u = Flow(b.X, b.Y); b.X += u.X * fdt * FlowScale; b.Y += u.Y * fdt * FlowScale; }
            if (b.D > 0) { float s = MathF.Sqrt(2 * b.D * fdt); b.X += s * RandN(); b.Y += s * RandN(); }
            b.Life -= fdt;
            if (b.Life <= 0) { Bits[i] = Bits[^1]; Bits.RemoveAt(Bits.Count - 1); }
        }
        for (int i = Frags.Count - 1; i >= 0; i--)
        {
            var f = Frags[i];
            f.Vx *= dec; f.Vy *= dec; f.X += f.Vx * fdt; f.Y += f.Vy * fdt; f.Va *= dec; f.A += f.Va * fdt;
            if (Flow != null && FlowScale > 0) { var u = Flow(f.X, f.Y); f.X += u.X * fdt * FlowScale; f.Y += u.Y * fdt * FlowScale; }
            if (f.D > 0) { float s = MathF.Sqrt(2 * f.D * fdt); f.X += s * RandN(); f.Y += s * RandN(); }
            float k = Damp(f.Curl * 1.2f, fdt);
            for (int q = 0; q < f.M; q++) { f.Lx[q] += (f.Tx[q] - f.Lx[q]) * k; f.Ly[q] += (f.Ty[q] - f.Ly[q]) * k; }
            f.Life -= fdt;
            if (f.Life <= 0) Frags.RemoveAt(i);
        }
        for (int i = Rings.Count - 1; i >= 0; i--)
        {
            var r = Rings[i]; r.R += r.Vr * fdt; r.Vr *= dec; r.Life -= fdt;
            if (r.Life <= 0) Rings.RemoveAt(i);
        }
        for (int i = Needles.Count - 1; i >= 0; i--)
        {
            var n = Needles[i]; n.Len += (n.Tl - n.Len) * Damp(30, fdt); n.Life -= fdt;
            if (n.Life <= 0) Needles.RemoveAt(i);
        }
    }

    public void Draw(Painter p, View v)
    {
        p.Identity();
        foreach (var r in Rings) p.Circle(r.X, r.Y, r.R, r.Lw, Mul(r.C, 0.5f * r.Life / r.Max));
        var buf = p.Buf2;
        foreach (var f in Frags)
        {
            if (!v.Vis(f.X, f.Y, 20)) continue;
            float al = Math.Min(1, f.Life / f.Max * 2.5f);
            p.Local(f.X, f.Y, f.A);
            int n = Painter.SmoothOpen(f.Lx, f.Ly, f.M, buf, 2);
            var span = new ReadOnlySpan<Vector2>(buf, 0, n);
            if (f.HasHalo) p.SoftStroke(span, false, f.Lw * 5f, Mul(f.Halo, al));
            p.Stroke(span, false, f.Lw, Mul(f.C, al), true);
        }
        p.Identity();
        foreach (var n in Needles)
        {
            float al = Math.Min(1, n.Life / n.Max * 3);
            p.Seg(n.X, n.Y, n.X + MathF.Cos(n.A) * n.Len, n.Y + MathF.Sin(n.A) * n.Len, v.Px * 1.2f, Mul(n.C, al));
        }
        foreach (var b in Bits)
        {
            if (b.X < v.X0 - 5 || b.X > v.X1 + 5 || b.Y < v.Y0 - 5 || b.Y > v.Y1 + 5) continue;
            p.Dot(b.X, b.Y, Math.Max(b.R, v.Px * 0.7f), Mul(b.C, Math.Min(1, b.Life / b.Max * 2)));
        }
    }
}

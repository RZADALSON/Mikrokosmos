namespace Mikro;

/// <summary>
/// Membrana soft-body: węzły podążają za kształtem spoczynkowym (dopasowanie kształtu),
/// a kontakty z sąsiadami wgniatają je; deformacje wygasają wykładniczo.
/// </summary>
public sealed class Membrane
{
    public readonly int N;
    public readonly float[] Px, Py, Rx, Ry, Dx, Dy, Nx, Ny;
    public float MaxDef = 1e9f;

    public Membrane(int n)
    {
        N = n;
        Px = new float[n]; Py = new float[n]; Rx = new float[n]; Ry = new float[n];
        Dx = new float[n]; Dy = new float[n]; Nx = new float[n]; Ny = new float[n];
    }

    /// <summary>Kształt gwiaździsty: węzeł i leży pod kątem lokalnym i·2π/N (pozwala szybko liczyć promień w danym kierunku).</summary>
    public bool IsRadial;
    /// <summary>Środek i obrót z ostatniego Follow + największa odległość węzła od środka.</summary>
    public float Cx, Cy, Ang, MaxR;

    public void Radial(float[] rr)
    {
        IsRadial = true;
        for (int i = 0; i < N; i++) { float t = i * TAU / N; Rx[i] = MathF.Cos(t) * rr[i]; Ry[i] = MathF.Sin(t) * rr[i]; }
    }

    public void Follow(float x, float y, float a, float k, float fdt)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a), dec = MathF.Exp(-k * fdt), md = MaxDef, mr = 0;
        for (int i = 0; i < N; i++)
        {
            float dx = Dx[i] * dec, dy = Dy[i] * dec, m = dx * dx + dy * dy;
            if (m > md * md) { float f = md / MathF.Sqrt(m); dx *= f; dy *= f; }
            Dx[i] = dx; Dy[i] = dy;
            float rx = Rx[i], ry = Ry[i];
            float px = x + c * rx - s * ry + dx, py = y + s * rx + c * ry + dy;
            Px[i] = px; Py[i] = py;
            float d2 = (px - x) * (px - x) + (py - y) * (py - y); if (d2 > mr) mr = d2;
        }
        Cx = x; Cy = y; Ang = a; MaxR = MathF.Sqrt(mr);
    }

    /// <summary>Promień bieżącego (zdeformowanego) obrysu w kierunku świata ang – tylko dla kształtów radialnych.</summary>
    public float RadiusAt(float ang)
    {
        float f = WrapA(ang - Ang) / TAU * N; if (f < 0) f += N;
        int i = (int)f; if (i >= N) i = N - 1;
        int j = i + 1 == N ? 0 : i + 1; float u = f - i;
        float di = Hypot(Px[i] - Cx, Py[i] - Cy), dj = Hypot(Px[j] - Cx, Py[j] - Cy);
        return di + (dj - di) * u;
    }

    /// <summary>
    /// Kontakt dwóch miękkich membran: węzły tej membrany leżące wewnątrz drugiej są wypychane na zewnątrz
    /// (część s głębokości na klatkę). Wywoływane dla obu stron → wspólna, spłaszczona granica jak u ściśniętych komórek.
    /// </summary>
    public void PushOutMem(Membrane o, float s)
    {
        float r2 = o.MaxR * o.MaxR;
        for (int i = 0; i < N; i++)
        {
            float dx = Px[i] - o.Cx, dy = Py[i] - o.Cy, d2 = dx * dx + dy * dy;
            if (d2 > r2 || d2 < 1e-12f) continue;
            float d = MathF.Sqrt(d2), rb = o.RadiusAt(MathF.Atan2(dy, dx));
            if (d >= rb) continue;
            float k = (rb - d) * s / d, mx = dx * k, my = dy * k;
            Px[i] += mx; Py[i] += my; Dx[i] += mx; Dy[i] += my;
        }
    }

    public void PushOut(Prim p, float s)
    {
        for (int i = 0; i < N; i++)
        {
            var r = Geo.PointPrim(Px[i], Py[i], p);
            if (r.D < 0)
            {
                float mx = -r.D * s * r.Nx, my = -r.D * s * r.Ny;
                Px[i] += mx; Py[i] += my; Dx[i] += mx; Dy[i] += my;
            }
        }
    }

    public void Normals()
    {
        for (int i = 0; i < N; i++)
        {
            int a = i == 0 ? N - 1 : i - 1, b = i == N - 1 ? 0 : i + 1;
            float tx = Px[b] - Px[a], ty = Py[b] - Py[a], l = MathF.Sqrt(tx * tx + ty * ty);
            if (l < 1e-12f) l = 1;
            Nx[i] = ty / l; Ny[i] = -tx / l;
        }
    }

    public void Centroid(out float x, out float y)
    {
        x = 0; y = 0;
        for (int i = 0; i < N; i++) { x += Px[i]; y += Py[i]; }
        x /= N; y /= N;
    }

    /// <summary>Wygładzony obwód (w świecie) do bufora.</summary>
    public int Smooth(Vector2[] outp, int sub = 3) => Painter.SmoothClosed(Px, Py, N, outp, sub);

    /// <summary>Wygładzony obwód przesunięty do środka o d (wymaga Normals()).</summary>
    public int SmoothInset(Vector2[] outp, float d, float[] tx, float[] ty, int sub = 3)
    {
        for (int i = 0; i < N; i++) { tx[i] = Px[i] - Nx[i] * d; ty[i] = Py[i] - Ny[i] * d; }
        return Painter.SmoothClosed(tx, ty, N, outp, sub);
    }
}

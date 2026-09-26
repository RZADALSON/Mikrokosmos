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

    public void Radial(float[] rr)
    {
        for (int i = 0; i < N; i++) { float t = i * TAU / N; Rx[i] = MathF.Cos(t) * rr[i]; Ry[i] = MathF.Sin(t) * rr[i]; }
    }

    public void Follow(float x, float y, float a, float k, float fdt)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a), dec = MathF.Exp(-k * fdt), md = MaxDef;
        for (int i = 0; i < N; i++)
        {
            float dx = Dx[i] * dec, dy = Dy[i] * dec, m = dx * dx + dy * dy;
            if (m > md * md) { float f = md / MathF.Sqrt(m); dx *= f; dy *= f; }
            Dx[i] = dx; Dy[i] = dy;
            float rx = Rx[i], ry = Ry[i];
            Px[i] = x + c * rx - s * ry + dx; Py[i] = y + s * rx + c * ry + dy;
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

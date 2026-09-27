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

    /// <summary>
    /// Sprężystość ciała (rad/s): 0 = kształt narzucany natychmiast; &gt; 0 = węzły dążą do kształtu spoczynkowego
    /// jak tłumiona sprężyna (ciało "galaretowate": wygina się, faluje i powoli wraca do formy).
    /// </summary>
    public float Stiff;
    /// <summary>Współczynnik tłumienia sprężyny (1 = krytyczne, &lt; 1 = lekkie drżenie).</summary>
    public float Damping = 0.65f;
    /// <summary>Wygięcie osi ciała: y += Bend·x² (w układzie lokalnym) – ciało krzywi się w stronę skrętu.</summary>
    public float Bend;
    /// <summary>Poza kadrem: bez symulacji sprężystości (oszczędność).</summary>
    public bool Lod;
    float[] _lx, _ly, _vx, _vy;
    float _lastA = float.NaN;

    public void Follow(float x, float y, float a, float k, float fdt)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a), dec = MathF.Exp(-k * fdt), md = MaxDef, mr = 0;
        bool jelly = Stiff > 0 && fdt > 0 && !Lod;
        if (Lod && _lx != null) { for (int i = 0; i < N; i++) { _lx[i] = Rx[i]; _ly[i] = Ry[i] + Bend * Rx[i] * Rx[i]; _vx[i] = _vy[i] = 0; } }
        if (jelly && _lx == null)
        {
            _lx = new float[N]; _ly = new float[N]; _vx = new float[N]; _vy = new float[N];
            for (int i = 0; i < N; i++) { _lx[i] = Rx[i]; _ly[i] = Ry[i]; }
        }
        // obrót ciała: końce "nie nadążają" – kształt lokalny obraca się lekko wstecz (bezwładność lepkościowa kształtu)
        if (jelly && !float.IsNaN(_lastA))
        {
            float da = WrapA(a - _lastA) * 0.35f, cr = MathF.Cos(-da), sr = MathF.Sin(-da);
            for (int i = 0; i < N; i++) { float lx = _lx[i], ly = _ly[i]; _lx[i] = lx * cr - ly * sr; _ly[i] = lx * sr + ly * cr; }
        }
        _lastA = a;
        float w = Stiff, k2 = w * w, cd = 2 * Damping * w, h = Math.Min(fdt, 1f / 30);
        for (int i = 0; i < N; i++)
        {
            float dx = Dx[i] * dec, dy = Dy[i] * dec, m = dx * dx + dy * dy;
            if (m > md * md) { float f = md / MathF.Sqrt(m); dx *= f; dy *= f; }
            Dx[i] = dx; Dy[i] = dy;
            float rx = Rx[i], ry = Ry[i] + Bend * Rx[i] * Rx[i];
            if (jelly)
            {
                float ax = (rx - _lx[i]) * k2 - _vx[i] * cd, ay = (ry - _ly[i]) * k2 - _vy[i] * cd;
                _vx[i] += ax * h; _vy[i] += ay * h;
                _lx[i] += _vx[i] * h; _ly[i] += _vy[i] * h;
                rx = _lx[i]; ry = _ly[i];
            }
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

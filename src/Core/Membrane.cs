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
    /// <summary>
    /// Nieściśliwość wnętrza 0..1 (cytoplazma to w praktyce ciecz nieściśliwa): po wgnieceniu przez kontakt
    /// ubytek pola wraca przez wybrzuszenie wolnych (niedotykających) części obrysu – ściśnięta komórka rozpływa się na boki.
    /// </summary>
    public float Fluid = 0.6f;
    readonly bool[] _touch;
    float _restArea;

    public Membrane(int n)
    {
        N = n;
        Px = new float[n]; Py = new float[n]; Rx = new float[n]; Ry = new float[n];
        Dx = new float[n]; Dy = new float[n]; Nx = new float[n]; Ny = new float[n];
        _touch = new bool[n];
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
    /// <summary>Stan rzeczywistego obrysu po ostatnim Follow: kąt opóźnienia obrotu i efektywne wygięcie osi (do rysowania wnętrza).</summary>
    public float LagA, BendEff;
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
        // rzeczywisty kształt: opóźnienie obrotu (galaretowate ciało „nie nadąża” za skrętem) i efektywne wygięcie osi –
        // wnętrze (jądro, rowek gębowy, ryjek…) musi się wyginać i obracać razem z obrysem, inaczej „twarz” zostaje w miejscu
        if (jelly)
        {
            float sc = 0, ss = 0;
            for (int i = 0; i < N; i++) { float rx = Rx[i], ry = Ry[i] + Bend * Rx[i] * Rx[i]; sc += rx * _lx[i] + ry * _ly[i]; ss += rx * _ly[i] - ry * _lx[i]; }
            LagA = MathF.Atan2(ss, sc);
            float c2 = MathF.Cos(-LagA), s2 = MathF.Sin(-LagA), num = 0, den = 0;
            for (int i = 0; i < N; i++)
            {
                float uy = _lx[i] * s2 + _ly[i] * c2, x2 = Rx[i] * Rx[i];
                num += (uy - (Ry[i] + Bend * x2)) * x2; den += x2 * x2;
            }
            BendEff = Bend + (den > 1e-9f ? num / den : 0);
        }
        else { LagA = 0; BendEff = Bend; }
        // pole kształtu spoczynkowego (z wygięciem) – wartość, którą ciecz we wnętrzu stara się zachować
        float ra = 0;
        for (int i = 0, j = N - 1; i < N; j = i++) ra += Rx[j] * (Ry[i] + Bend * Rx[i] * Rx[i]) - Rx[i] * (Ry[j] + Bend * Rx[j] * Rx[j]);
        _restArea = MathF.Abs(ra) * 0.5f;
    }

    /// <summary>Pole obrysu ze znakiem (wzór Gaussa – „sznurowadłowy”).</summary>
    public float SignedArea()
    {
        float a = 0;
        for (int i = 0, j = N - 1; i < N; j = i++) a += Px[j] * Py[i] - Px[i] * Py[j];
        return a * 0.5f;
    }

    /// <summary>
    /// Zachowanie objętości (w 2D – pola): brakujące pole oddają węzły, które w tej klatce niczego nie dotykały,
    /// przesuwając się wzdłuż normalnej na zewnątrz. Korekta trafia do deformacji D, więc wygasa razem z wgnieceniem.
    /// </summary>
    public void ConserveArea()
    {
        if (Fluid <= 0 || _restArea <= 0) { Array.Clear(_touch); return; }
        float sa = SignedArea(), a = MathF.Abs(sa), err = _restArea - a;
        if (MathF.Abs(err) < _restArea * 0.002f) { Array.Clear(_touch); return; }
        float sg = sa >= 0 ? 1 : -1, per = 0;
        for (int i = 0; i < N; i++)
        {
            if (_touch[i]) continue;
            int b = i == N - 1 ? 0 : i + 1;
            per += Hypot(Px[b] - Px[i], Py[b] - Py[i]);
        }
        if (per < 1e-6f) { Array.Clear(_touch); return; }
        float d = err / per * Fluid, md = MaxDef;
        for (int i = 0; i < N; i++)
        {
            if (_touch[i]) { _touch[i] = false; continue; }
            int pa = i == 0 ? N - 1 : i - 1, pb = i == N - 1 ? 0 : i + 1;
            float tx = Px[pb] - Px[pa], ty = Py[pb] - Py[pa], l = MathF.Sqrt(tx * tx + ty * ty);
            if (l < 1e-9f) continue;
            float nx = ty / l * sg, ny = -tx / l * sg, mx = nx * d, my = ny * d;
            float dx = Dx[i] + mx, dy = Dy[i] + my, m2 = dx * dx + dy * dy;
            if (m2 > md * md) { float f = md / MathF.Sqrt(m2); mx = dx * f - Dx[i]; my = dy * f - Dy[i]; }
            Px[i] += mx; Py[i] += my; Dx[i] += mx; Dy[i] += my;
        }
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
            Px[i] += mx; Py[i] += my; Dx[i] += mx; Dy[i] += my; _touch[i] = true;
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
                Px[i] += mx; Py[i] += my; Dx[i] += mx; Dy[i] += my; _touch[i] = true;
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

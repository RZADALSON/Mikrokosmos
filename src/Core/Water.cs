namespace Mikro;

/// <summary>
/// Zawiesina w wodzie wokół kadru: drobiny organiczne, koloidy i pojedyncze pęcherzyki gazu. Same w sobie nic nie robią,
/// ale pokazują ruch wody – ruchy Browna, powolny prąd preparatu i przepływ wokół pływających komórek
/// (to one "płyną" obok orzęska, jak na nagraniach mikroskopowych).
/// </summary>
public sealed class WaterField
{
    readonly World _w;
    readonly float[] _x, _y, _s, _ph, _sx, _sy; readonly byte[] _k;
    readonly int _n;
    bool _init; int _par;
    public WaterField(World w, int n = 420)
    {
        _w = w; _n = n; _x = new float[n]; _y = new float[n]; _s = new float[n]; _ph = new float[n]; _k = new byte[n]; _sx = new float[n]; _sy = new float[n];
    }
    void Spawn(int i, float x0, float y0, float x1, float y1)
    {
        float v = _w.Sc.ViewW;
        _x[i] = Rand(x0, x1); _y[i] = Rand(y0, y1);
        float r = R01();
        _k[i] = (byte)(r < 0.008f ? 2 : r < 0.55f ? 1 : 0);            // 2 = pęcherzyk gazu, 1 = jasna drobina, 0 = ciemna drobina
        _s[i] = _k[i] == 2 ? v * Rand(0.004f, 0.011f) : v * Rand(0.0007f, 0.0022f);
        _ph[i] = Rand(0, TAU);
    }
    public void Update(float dt)
    {
        float hw = _w.ViewR * 1.25f, hh = hw * 0.62f, cx = _w.CamX, cy = _w.CamY;
        float x0 = cx - hw, x1 = cx + hw, y0 = cy - hh, y1 = cy + hh;
        if (!_init) { for (int i = 0; i < _n; i++) Spawn(i, x0, y0, x1, y1); _init = true; }
        float dts = Math.Max(dt, 0);
        _par ^= 1;
        for (int i = 0; i < _n; i++)
        {
            float x = _x[i], y = _y[i];
            if (x < x0 || x > x1 || y < y0 || y > y1)
            {
                // wyszła z pola – pojawia się po przeciwnej stronie (świat nieskończony, zawiesina jednorodna)
                if (x < x0) x += 2 * hw; else if (x > x1) x -= 2 * hw;
                if (y < y0) y += 2 * hh; else if (y > y1) y -= 2 * hh;
                if (x < x0 || x > x1 || y < y0 || y > y1) { Spawn(i, x0, y0, x1, y1); continue; }
            }
            // ruchy Browna: D = kT / (3πηd) ≈ 0,44/d µm²/s (woda, 20 °C)
            float sd = MathF.Sqrt(2 * 0.44f / Math.Max(2 * _s[i], 0.01f) * dts) * (_k[i] == 2 ? 0.3f : 1);
            x += RandN() * sd; y += RandN() * sd;
            _w.FlowAt(x, y, out float fx, out float fy);
            // przepływ od pływaków zmienia się płynnie – każda drobina odświeża go co drugą klatkę (przy setkach pływaków to połowa kosztu)
            if ((i & 1) == _par) { _w.SwimFlow(x, y, -1, out float sx, out float sy); _sx[i] = sx; _sy[i] = sy; }
            x += (fx + _sx[i]) * dt; y += (fy + _sy[i]) * dt;
            _x[i] = x; _y[i] = y; _ph[i] += dt;
        }
    }
    public void Draw(Painter p, View v)
    {
        p.Identity();
        for (int i = 0; i < _n; i++)
        {
            float x = _x[i], y = _y[i], s = _s[i];
            if (!v.Vis(x, y, s * 2)) continue;
            switch (_k[i])
            {
                case 0: p.Dot(x, y, Math.Max(s, v.Px * 0.6f), Rgb(55, 55, 45, 0.35f)); break;
                case 1: p.Dot(x, y, Math.Max(s, v.Px * 0.6f), Rgb(250, 250, 235, 0.45f)); break;
                default:
                    // pęcherzyk gazu w kontraście fazowym: ciemny pierścień, jasny środek i blik
                    p.Circle(x, y, s, Math.Max(v.Px, s * 0.18f), Rgb(40, 42, 36, 0.35f));
                    p.FillCircle(x, y, s * 0.8f, Rgb(245, 248, 235, 0.22f));
                    p.Dot(x - s * 0.3f, y - s * 0.3f, s * 0.18f, Rgb(255, 255, 255, 0.8f));
                    break;
            }
        }
    }
}

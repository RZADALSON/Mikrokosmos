namespace Mikro;

/// <summary>Pole stężenia (np. glukozy) na siatce z dyfuzją.</summary>
public sealed class Field
{
    public readonly float W, H;
    public readonly int Nx, Ny;
    public readonly float[] C;
    readonly float[] _tmp;
    float _acc;

    public Field(float w, float h, int n)
    {
        W = w; H = h; Nx = n; Ny = Math.Max(2, (int)MathF.Round(n * h / w));
        C = new float[Nx * Ny]; _tmp = new float[Nx * Ny];
    }
    int Idx(float x, float y)
    {
        int i = (int)Clamp(MathF.Floor(x / W * Nx), 0, Nx - 1), j = (int)Clamp(MathF.Floor(y / H * Ny), 0, Ny - 1);
        return j * Nx + i;
    }
    public float Sample(float x, float y)
    {
        float fx = Clamp(x / W * Nx - 0.5f, 0, Nx - 1.001f), fy = Clamp(y / H * Ny - 0.5f, 0, Ny - 1.001f);
        int i = (int)fx, j = (int)fy; float u = fx - i, v = fy - j;
        int i1 = Math.Min(i + 1, Nx - 1), j1 = Math.Min(j + 1, Ny - 1);
        return (C[j * Nx + i] * (1 - u) + C[j * Nx + i1] * u) * (1 - v) + (C[j1 * Nx + i] * (1 - u) + C[j1 * Nx + i1] * u) * v;
    }
    public float Consume(float x, float y, float amt)
    {
        int k = Idx(x, y); float got = Math.Min(C[k], amt);
        C[k] -= got; return got;
    }
    public void Blob(float x, float y, float r, float amt)
    {
        float cw = W / Nx;
        for (int j = 0; j < Ny; j++)
            for (int i = 0; i < Nx; i++)
            {
                float dx = (i + 0.5f) * cw - x, dy = (j + 0.5f) * cw - y;
                float f = MathF.Exp(-(dx * dx + dy * dy) / (r * r));
                if (f > 0.01f) C[j * Nx + i] = Math.Min(1.5f, C[j * Nx + i] + amt * f);
            }
    }
    public void Diffuse(float dt, float D)
    {
        _acc += dt;
        if (_acc < 0.05f) return;
        float h = W / Nx, k = Clamp(D * _acc / (h * h), 0, 0.2f);
        _acc = 0;
        for (int j = 0; j < Ny; j++)
            for (int i = 0; i < Nx; i++)
            {
                int q = j * Nx + i; float v = C[q];
                float l = i > 0 ? C[q - 1] : v, r = i < Nx - 1 ? C[q + 1] : v, u = j > 0 ? C[q - Nx] : v, d = j < Ny - 1 ? C[q + Nx] : v;
                _tmp[q] = v + k * (l + r + u + d - 4 * v);
            }
        Array.Copy(_tmp, C, C.Length);
    }
    public void Draw(Painter p, Color col, float alpha)
    {
        float cw = W / Nx, ch = H / Ny;
        p.Grid(cw * 0.5f, ch * 0.5f, cw, ch, Nx, Ny, (i, j) => { float v = Clamp(C[j * Nx + i], 0, 1.2f) / 1.2f; return WithA(col, v * alpha); });
    }
}

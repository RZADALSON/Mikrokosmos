namespace Mikro;

/// <summary>Kształt kolizyjny: koło albo kapsuła (odcinek A–B z promieniem R).</summary>
public sealed class Prim
{
    public bool Cap;
    public float X, Y, R = 1, Ax, Ay, Bx, By;
}

public struct PHit { public float D, Nx, Ny, Cx, Cy; }

public static class Geo
{
    /// <summary>Odległość punktu od powierzchni prymitywu (ujemna = wewnątrz) + normalna + najbliższy punkt powierzchni.</summary>
    public static PHit PointPrim(float px, float py, Prim p)
    {
        float cx, cy;
        if (!p.Cap) { cx = p.X; cy = p.Y; }
        else
        {
            float dx = p.Bx - p.Ax, dy = p.By - p.Ay, l2 = dx * dx + dy * dy;
            float t = l2 > 0 ? ((px - p.Ax) * dx + (py - p.Ay) * dy) / l2 : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            cx = p.Ax + dx * t; cy = p.Ay + dy * t;
        }
        float ex = px - cx, ey = py - cy, d = MathF.Sqrt(ex * ex + ey * ey);
        PHit h;
        if (d < 1e-9f) { h.Nx = 1; h.Ny = 0; h.D = -p.R; }
        else { h.Nx = ex / d; h.Ny = ey / d; h.D = d - p.R; }
        h.Cx = cx + h.Nx * p.R; h.Cy = cy + h.Ny * p.R;
        return h;
    }

    /// <summary>Najbliższa powierzchnia encji (po wszystkich częściach kształtu).</summary>
    public static PHit PointEnt(float px, float py, Ent e)
    {
        PHit best = PointPrim(px, py, e.Part(0));
        for (int i = 1; i < e.PartCount; i++) { var h = PointPrim(px, py, e.Part(i)); if (h.D < best.D) best = h; }
        return best;
    }
    static float Bound(Prim p) => p.Cap ? p.R + 0.5f * MathF.Sqrt(Sq(p.Bx - p.Ax) + Sq(p.By - p.Ay)) : p.R;
    /// <summary>Najgłębsza penetracja między dwiema encjami (wszystkie pary części).</summary>
    public static bool EntEnt(Ent a, Ent b, out float pen, out float nx, out float ny)
    {
        pen = 0; nx = ny = 0; bool hit = false;
        int na = a.PartCount, nb = b.PartCount;
        if (na == 1 && nb == 1) return PrimPrim(a.Prim, b.Prim, out pen, out nx, out ny);
        float bcx = 0, bcy = 0;
        for (int i = 0; i < na; i++)
        {
            var pa = a.Part(i); float ra = Bound(pa);
            if (Sq(pa.X - b.X) + Sq(pa.Y - b.Y) > Sq(ra + b.R)) continue; // część daleko od całego b
            for (int j = 0; j < nb; j++)
            {
                var pb = b.Part(j); float rb = Bound(pb);
                if (Sq(pa.X - pb.X) + Sq(pa.Y - pb.Y) > Sq(ra + rb)) continue;
                if (PrimPrim(pa, pb, out float p, out float x, out float y) && p > pen) { pen = p; nx = x; ny = y; hit = true; bcx = ContactX; bcy = ContactY; }
            }
        }
        if (hit) { ContactX = bcx; ContactY = bcy; }
        return hit;
    }

    static void SegSeg(float p1x, float p1y, float q1x, float q1y, float p2x, float p2y, float q2x, float q2y,
        out float x1, out float y1, out float x2, out float y2)
    {
        float d1x = q1x - p1x, d1y = q1y - p1y, d2x = q2x - p2x, d2y = q2y - p2y, rx = p1x - p2x, ry = p1y - p2y;
        float a = d1x * d1x + d1y * d1y, e = d2x * d2x + d2y * d2y, f = d2x * rx + d2y * ry;
        float s, t;
        const float EPS = 1e-12f;
        if (a <= EPS && e <= EPS) { s = t = 0; }
        else if (a <= EPS) { s = 0; t = Clamp(f / e, 0, 1); }
        else
        {
            float c = d1x * rx + d1y * ry;
            if (e <= EPS) { t = 0; s = Clamp(-c / a, 0, 1); }
            else
            {
                float b = d1x * d2x + d1y * d2y, den = a * e - b * b;
                s = den != 0 ? Clamp((b * f - c * e) / den, 0, 1) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = Clamp(-c / a, 0, 1); }
                else if (t > 1) { t = 1; s = Clamp((b - c) / a, 0, 1); }
            }
        }
        x1 = p1x + d1x * s; y1 = p1y + d1y * s; x2 = p2x + d2x * t; y2 = p2y + d2y * t;
    }

    /// <summary>Penetracja dwóch prymitywów; normalna od a do b.</summary>
    public static bool PrimPrim(Prim a, Prim b, out float pen, out float nx, out float ny)
    {
        float ax = a.Cap ? a.Ax : a.X, ay = a.Cap ? a.Ay : a.Y, bx = a.Cap ? a.Bx : a.X, by = a.Cap ? a.By : a.Y;
        float cx = b.Cap ? b.Ax : b.X, cy = b.Cap ? b.Ay : b.Y, dx = b.Cap ? b.Bx : b.X, dy = b.Cap ? b.By : b.Y;
        SegSeg(ax, ay, bx, by, cx, cy, dx, dy, out float x1, out float y1, out float x2, out float y2);
        float vx = x2 - x1, vy = y2 - y1, d = MathF.Sqrt(vx * vx + vy * vy);
        pen = a.R + b.R - d;
        if (pen <= 0) { nx = ny = 0; return false; }
        if (d < 1e-9f) { vx = 1; vy = 0; d = 1; }
        nx = vx / d; ny = vy / d;
        // punkt kontaktu – w połowie strefy nachodzenia
        ContactX = x1 + nx * (a.R - pen * 0.5f); ContactY = y1 + ny * (a.R - pen * 0.5f);
        return true;
    }
    /// <summary>Punkt kontaktu z ostatniego wykrytego zderzenia (PrimPrim / EntEnt).</summary>
    public static float ContactX, ContactY;
}

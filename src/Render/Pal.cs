namespace Mikro;

/// <summary>Pomocnicze tworzenie kolorów (0–255 jak w CSS).</summary>
public static class Pal
{
    public static Color Rgb(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
    public static Color WithA(Color c, float a) { c.A = a; return c; }
    public static Color Mul(Color c, float a) { c.A *= a; return c; }
    public static Color Hex(string h) => Color.FromHtml(h);
}

namespace Mikro;

/// <summary>Węzeł rysujący świat: detrytus (atlas tekstur) + cała geometria wektorowa w paczkach trójkątów.</summary>
public partial class WorldView : Node2D
{
    public World World;
    public readonly View V = new();
    public float Stretch = 1f;
    readonly Painter _p = new();
    readonly Painter _pt = new(true);
    public int LastVertices;
    public double LastBuildMs;

    public override void _Ready() => DebrisAtlas.Ensure();

    public void SetCamera(float cx, float cy, float s, Vector2 size)
    {
        Position = size / 2 - new Vector2(cx, cy) * s;
        Scale = new Vector2(s, s);
        V.S = s; V.Px = 1f / (s * Stretch);
        V.X0 = cx - size.X / 2 / s; V.X1 = cx + size.X / 2 / s;
        V.Y0 = cy - size.Y / 2 / s; V.Y1 = cy + size.Y / 2 / s;
        if (World != null) V.T = World.T;
    }

    public override void _Draw()
    {
        if (World == null) return;
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var item = GetCanvasItem();
        if (World.DebrisList.Count > 0)
        {
            _pt.Begin(V.Px);
            World.DrawDebris(_pt, V, DebrisAtlas.Uv, World.Sc.DebrisCols);
            _pt.Flush(item, DebrisAtlas.Tex.GetRid());
        }
        _p.Begin(V.Px);
        World.Draw(_p, V);
        _p.Flush(item);
        LastVertices = _p.VertexCount;
        LastBuildMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }

    /// <summary>Buduje geometrię bez wysyłania (do testów wydajności).</summary>
    public int BuildOnly()
    {
        _p.Begin(V.Px);
        World.Draw(_p, V);
        return _p.VertexCount;
    }
}

/// <summary>Proceduralny atlas: plamki i pierścienie o różnym rozmyciu (głębia ostrości).</summary>
public static class DebrisAtlas
{
    public static ImageTexture Tex;
    public static Rect2[] Uv;
    const int C = 64;

    public static void Ensure()
    {
        if (Tex != null) return;
        var img = Image.CreateEmpty(C * 6, C, false, Image.Format.Rgba8);
        Uv = new Rect2[6];
        for (int k = 0; k < 6; k++)
        {
            bool ring = k % 3 == 2;
            float blur = k < 2 ? 0.03f : k < 4 ? 0.12f : 0.25f;
            var rad = new float[7];
            for (int i = 0; i < 7; i++) rad[i] = 0.35f + R01() * 0.35f;
            for (int y = 0; y < C; y++)
                for (int x = 0; x < C; x++)
                {
                    float u = (x + 0.5f) / C * 2 - 1, v = (y + 0.5f) / C * 2 - 1;
                    float d = MathF.Sqrt(u * u + v * v), a;
                    if (ring) a = MathF.Exp(-Sq((d - 0.55f) / (0.09f + blur)));
                    else
                    {
                        float th = (MathF.Atan2(v, u) / TAU + 1) % 1 * 7; int i0 = (int)th % 7, i1 = (i0 + 1) % 7; float f = th - (int)th;
                        float r = rad[i0] * (1 - f) + rad[i1] * f;
                        a = Clamp((r - d) / (blur * 2 + 0.02f) + 0.5f, 0, 1);
                    }
                    if (d > 0.98f) a *= Clamp((1 - d) / 0.02f, 0, 1);
                    img.SetPixel(k * C + x, y, new Color(1, 1, 1, a));
                }
            Uv[k] = new Rect2((k * C + 0.5f) / (C * 6), 0.5f / C, (C - 1f) / (C * 6), (C - 1f) / C);
        }
        Tex = ImageTexture.CreateFromImage(img);
    }
}


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
    /// <summary>Znaczniki czasu początku i końca ostatniego _Draw (Stopwatch) – oś czasu klatki w teście płynności.</summary>
    public long LastDrawStart, LastDrawEnd;

    /// <summary>
    /// Pula elementów płótna (dzieci węzła): każda część geometrii klatki (część tła, zakres encji, nakładka) ma własną parę
    /// – ciała z tekstury atlasu i geometrię wektorową. Kolejność rysowania nadaje indeks rysowania ustawiany co klatkę
    /// (warstwa po warstwie: tekstury warstwy, potem jej wektory), więc części mogą być wysyłane do serwera renderowania
    /// równolegle, z wątków roboczych, w dowolnej kolejności. Węzeł sam rysuje tylko detrytus.
    /// </summary>
    readonly List<Rid> _pool = new();
    public Rid Item(int i)
    {
        while (_pool.Count <= i)
        {
            var it = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(it, GetCanvasItem());
            RenderingServer.CanvasItemSetDefaultTextureFilter(it, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
            _pool.Add(it);
        }
        return _pool[i];
    }
    int _used;
    /// <summary>Pula elementów płótna geometrii statycznej (przebudowywanej rzadko) i świat, który ją zbudował.</summary>
    readonly List<Rid> _spool = new();
    public World StaticOwner;
    public Rid StaticItem(int i)
    {
        while (_spool.Count <= i)
        {
            var it = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(it, GetCanvasItem());
            RenderingServer.CanvasItemSetDefaultTextureFilter(it, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
            _spool.Add(it);
        }
        return _spool[i];
    }
    public int StaticCount => _spool.Count;
    public override void _Ready() { DebrisAtlas.Ensure(); SpriteAtlas.Ensure(); }
    public override void _ExitTree()
    {
        foreach (var it in _pool) if (it.IsValid) RenderingServer.FreeRid(it);
        foreach (var it in _spool) if (it.IsValid) RenderingServer.FreeRid(it);
        _pool.Clear(); _spool.Clear();
    }

    float _ox, _oy;
    public void SetCamera(float cx, float cy, float s, Vector2 size)
    {
        // origin skokowo co ~ szerokość ekranu: wierzchołki w buforze GPU pozostają małe (brak drgań daleko od startu)
        float step = size.X / s;
        if (MathF.Abs(cx - _ox) > step || MathF.Abs(cy - _oy) > step) { _ox = MathF.Round(cx / step) * step; _oy = MathF.Round(cy / step) * step; }
        Position = size / 2 - new Vector2(cx - _ox, cy - _oy) * s;
        Scale = new Vector2(s, s);
        V.S = s; V.Px = 1f / (s * Stretch);
        V.X0 = cx - size.X / 2 / s; V.X1 = cx + size.X / 2 / s;
        V.Y0 = cy - size.Y / 2 / s; V.Y1 = cy + size.Y / 2 / s;
        if (World != null) V.T = World.T;
    }

    public override void _Draw()
    {
        if (World == null)
        {
            // menu: nic nie jest rysowane – po grze trzeba wyczyścić pozostałą geometrię
            if (_used > 0 || StaticOwner != null)
            {
                foreach (var it in _pool) RenderingServer.CanvasItemClear(it);
                foreach (var it in _spool) RenderingServer.CanvasItemClear(it);
                _used = 0; StaticOwner = null; LastVertices = 0;
            }
            return;
        }
        var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var item = GetCanvasItem();
        if (World.DebrisList.Count > 0)
        {
            long td = World.TS;
            _pt.Begin(V.Px); _pt.OX = _ox; _pt.OY = _oy;
            World.DrawDebris(_pt, V, DebrisAtlas.Uv, World.Sc.DebrisCols);
            _pt.Flush(item, DebrisAtlas.Tex.GetRid());
            World.PAdd("draw:debris+flush", td);
        }
        int used = World.DrawParallel(V, V.Px, _ox, _oy, this);
        // elementy nieużyte w tej klatce (mniej części niż poprzednio) – wyczyszczone
        for (int i = used; i < _used; i++) RenderingServer.CanvasItemClear(_pool[i]);
        _used = used;
        LastVertices = World.ParallelVertices;
        LastDrawStart = t0; LastDrawEnd = System.Diagnostics.Stopwatch.GetTimestamp();
        LastBuildMs = (LastDrawEnd - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
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


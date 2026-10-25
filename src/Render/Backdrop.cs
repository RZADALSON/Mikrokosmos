namespace Mikro;

/// <summary>
/// Gęstości materii poza płaszczyzną ostrości w danym miejscu preparatu (0..1): duże plamy, mniejsze plamy, pręty/nici,
/// drobiny; zmętnienie i odcień strefy (0,5 = obojętny). Scenariusz podaje je dla każdej strefy (biomu) albo punktu.
/// </summary>
public struct BgZone
{
    public float B1, B2, Rod, Pts, Haze; public Color Tint;
    public BgZone(float b1, float b2, float rod, float pts, float haze, float tr = 0.5f, float tg = 0.5f, float tb = 0.5f)
    { B1 = b1; B2 = b2; Rod = rod; Pts = pts; Haze = haze; Tint = new Color(tr, tg, tb); }
    public static BgZone Lerp(BgZone a, BgZone b, float t) => new(M.Lerp(a.B1, b.B1, t), M.Lerp(a.B2, b.B2, t), M.Lerp(a.Rod, b.Rod, t), M.Lerp(a.Pts, b.Pts, t), M.Lerp(a.Haze, b.Haze, t),
        M.Lerp(a.Tint.R, b.Tint.R, t), M.Lerp(a.Tint.G, b.Tint.G, t), M.Lerp(a.Tint.B, b.Tint.B, t));
}

/// <summary>
/// Wygląd rozogniskowanej materii w świecie danego trybu – prawdziwe rozmiary (µm) i barwy tego, co leży nad i pod
/// płaszczyzną ostrości (patrz shaders/scope_bg.gdshader). P = (rozmiar, odstęp ×rozmiar, rozmycie, nieregularność),
/// Q = (jądro / jasny środek, pierścień rozogniskowania, wydłużenie, ciemny brzeg); pręty: P = (długość, połowa szerokości,
/// rozmycie, wygięcie), Q = (prążki na µm, siła prążków, ściany co µm, odstęp ×długość); drobiny: P = (promień, odstęp,
/// rozmycie, jasny pierścień).
/// </summary>
public sealed class BgLook
{
    public Vector4 B1, B1Q, B2, B2Q, Rod, RodQ, Pts, Needle;
    public Color B1Col, B2Col, RodCol, PtsCol, NeedleCol = Rgb(115, 100, 70, 0.4f);
    /// <summary>
    /// Ułożenie prętów: 0 – dowolne kierunki; 1 – promieniste (pionowe, jak włókna gleju promienistego w korze), a w pasie
    /// RodBand (y od–do, µm) – poziome (pęczki aksonów w strefie pośredniej).
    /// </summary>
    public int RodMode; public Vector2 RodBand;

    /// <summary>
    /// Staw (100–400×): brunatne kłaczki detrytusu i grudki osadu, zielone kolonie glonów, rozmyte nici skrętnicy
    /// z taśmą chloroplastu i ścianami komórek, drobne ziarna – jak na zdjęciach kropli stawowej.
    /// </summary>
    public static BgLook Pond => new()
    {
        B1 = new(75, 4.2f, 0.5f, 0.45f), B1Q = new(0.35f, 0.3f, 0.6f, 0.4f), B1Col = Rgb(44, 40, 26, 0.62f),
        B2 = new(24, 5, 0.45f, 0.3f), B2Q = new(0.3f, 0.4f, 0.4f, 0.15f), B2Col = Rgb(84, 108, 52, 0.45f),
        Rod = new(900, 13, 0.6f, 0.25f), RodQ = new(1 / 45f, 0.45f, 120, 1.05f), RodCol = Rgb(92, 138, 66, 0.36f),
        Needle = new(0.45f, 0.22f, 0.3f, 0), NeedleCol = Rgb(120, 104, 66, 0.45f),
        Pts = new(4.5f, 6, 0.6f, 0.5f), PtsCol = Rgb(64, 62, 48, 0.5f),
    };
    /// <summary>Poduszka mchu (40–100×): rozmyte listki mchu, kożuchy glonów, chwytniki i strzępki, detrytus.</summary>
    public static BgLook Moss => new()
    {
        B1 = new(180, 3, 0.55f, 0.2f), B1Q = new(0.1f, 0.25f, 2.0f, 0.2f), B1Col = Rgb(70, 104, 44, 0.42f),
        B2 = new(30, 5, 0.45f, 0.3f), B2Q = new(0.3f, 0.3f, 0.3f, 0.1f), B2Col = Rgb(62, 118, 48, 0.45f),
        Rod = new(500, 4, 0.7f, 0.4f), RodQ = new(0, 0, 0, 1.1f), RodCol = Rgb(110, 86, 56, 0.34f),
        Pts = new(5, 7, 0.55f, 0.5f), PtsCol = Rgb(70, 64, 46, 0.45f),
    };
    /// <summary>Biofilm (1000×): rozmyte pałeczki i ziarniaki w innych płaszczyznach, kłaczki macierzy (EPS), drobiny.</summary>
    public static BgLook Biofilm => new()
    {
        B1 = new(5, 5, 0.6f, 0.4f), B1Q = new(0.1f, 0.25f, 0.4f, 0.2f), B1Col = Rgb(70, 72, 60, 0.4f),
        B2 = new(1.6f, 5, 0.5f, 0.05f), B2Q = new(-0.2f, 0.5f, 0.15f, 0.3f), B2Col = Rgb(58, 60, 54, 0.45f),
        Rod = new(2.4f, 0.42f, 0.9f, 0.1f), RodQ = new(0, 0, 0, 1.4f), RodCol = Rgb(48, 54, 46, 0.42f),
        Pts = new(0.25f, 8, 0.6f, 0.5f), PtsCol = Rgb(58, 58, 52, 0.4f),
    };
    /// <summary>Tkanka łączna (400×): komórki nad i pod płaszczyzną ostrości (ciemniejsze jądra), pęczki kolagenu, erytrocyty.</summary>
    public static BgLook Tissue => new()
    {
        B1 = new(8, 4.2f, 0.45f, 0.2f), B1Q = new(0.5f, 0.35f, 0.7f, 0.15f), B1Col = Rgb(118, 104, 124, 0.45f),
        B2 = new(3.9f, 2.4f, 0.35f, 0.03f), B2Q = new(-0.45f, 0.35f, 0.1f, 0.1f), B2Col = Rgb(178, 118, 104, 0.45f),
        Rod = new(90, 2.2f, 0.9f, 0.35f), RodQ = new(1 / 10f, 0.2f, 0, 1.0f), RodCol = Rgb(238, 228, 208, 0.34f),
        Pts = new(0.6f, 9, 0.6f, 0.4f), PtsCol = Rgb(88, 78, 70, 0.35f),
    };
    /// <summary>Krew (400×): erytrocyty w innych płaszczyznach, krople tłuszczu / hepatocyty, płytki.</summary>
    public static BgLook Blood => new()
    {
        B1 = new(3.9f, 2.1f, 0.4f, 0.03f), B1Q = new(-0.5f, 0.35f, 0.12f, 0.1f), B1Col = Rgb(188, 138, 126, 0.42f),
        B2 = new(30, 2.5f, 0.4f, 0.05f), B2Q = new(-0.15f, 0.6f, 0.15f, 0.25f), B2Col = Rgb(212, 202, 168, 0.34f),
        Rod = new(40, 0.8f, 0.8f, 0.4f), RodQ = new(0, 0, 0, 1.1f), RodCol = Rgb(200, 190, 180, 0.25f),
        Pts = new(0.8f, 6, 0.5f, 0.3f), PtsCol = Rgb(128, 110, 138, 0.4f),
    };
    /// <summary>Drogi rodne (400×): złuszczone komórki nabłonka, komórki okrągłe, pasma śluzu, plemniki poza ostrością.</summary>
    public static BgLook Tract => new()
    {
        B1 = new(40, 3.5f, 0.4f, 0.28f), B1Q = new(0.35f, 0.25f, 0.3f, 0.1f), B1Col = Rgb(165, 155, 135, 0.3f),
        B2 = new(6, 5, 0.45f, 0.1f), B2Q = new(0.4f, 0.4f, 0.15f, 0.1f), B2Col = Rgb(140, 130, 145, 0.35f),
        Rod = new(260, 1.3f, 1.0f, 0.5f), RodQ = new(0, 0, 0, 1.0f), RodCol = Rgb(230, 228, 215, 0.3f),
        Pts = new(2.2f, 5, 0.6f, 0.5f), PtsCol = Rgb(70, 70, 68, 0.45f),
    };
    /// <summary>Agar (100×): pęcherzyki i nierówności żelu, ameby poza ostrością, rozmyta murawa bakterii.</summary>
    public static BgLook Agar => new()
    {
        B1 = new(25, 7, 0.3f, 0.08f), B1Q = new(-0.1f, 0.8f, 0.1f, 0.05f), B1Col = Rgb(190, 190, 175, 0.18f),
        B2 = new(6, 6, 0.5f, 0.2f), B2Q = new(0.35f, 0.3f, 0.3f, 0.1f), B2Col = Rgb(120, 118, 95, 0.3f),
        Rod = new(10, 1, 1, 0), RodQ = new(0, 0, 0, 1), RodCol = new Color(0, 0, 0, 0),
        Pts = new(0.9f, 3, 0.8f, 0.2f), PtsCol = Rgb(80, 78, 60, 0.35f),
    };
    /// <summary>
    /// Zarodkowa kora mózgu (400×, skrawek żywej tkanki): gęsto upakowane jądra komórek nad i pod płaszczyzną ostrości,
    /// promieniste włókna gleju (w strefie pośredniej – poziome pęczki aksonów), drobiny neuropilu.
    /// </summary>
    public static BgLook Cortex => new()
    {
        B1 = new(7, 2.3f, 0.5f, 0.12f), B1Q = new(0.45f, 0.3f, 0.45f, 0.12f), B1Col = Rgb(112, 104, 122, 0.36f),
        B2 = new(4.5f, 2.2f, 0.4f, 0.08f), B2Q = new(0.4f, 0.3f, 0.9f, 0.1f), B2Col = Rgb(126, 118, 132, 0.34f),
        Rod = new(70, 0.7f, 0.9f, 0.08f), RodQ = new(0, 0, 0, 0.35f), RodCol = Rgb(228, 222, 206, 0.3f),
        Pts = new(0.45f, 7, 0.6f, 0.4f), PtsCol = Rgb(90, 84, 92, 0.32f),
        RodMode = 1, RodBand = new(170, 345),
    };
    /// <summary>TEM (25 000×): nierówna warstwa barwnika (octan uranylu) na błonce węglowej i jej ziarno – bez rozogniskowania.</summary>
    public static BgLook Tem => new()
    {
        B1 = new(0.8f, 3, 0.9f, 0.4f), B1Q = new(0, 0, 0.5f, 0), B1Col = Rgb(18, 22, 26, 0.28f),
        B2 = new(0.3f, 4, 0.8f, 0.3f), B2Q = new(0, 0, 0.3f, 0), B2Col = Rgb(92, 104, 114, 0.14f),
        Rod = new(1, 0.1f, 1, 0), RodQ = new(0, 0, 0, 1), RodCol = new Color(0, 0, 0, 0),
        Pts = new(0.01f, 6, 0.5f, 0.2f), PtsCol = Rgb(150, 165, 175, 0.22f),
    };
}

/// <summary>
/// Tło głębi preparatu: przekazuje wygląd trybu do shadera i co chwilę próbkuje strefy wokół kamery do tekstury 16 × 16
/// (gęstości materii i odcień). Filtrowanie dwuliniowe tej tekstury daje płynne przejścia między strefami.
/// </summary>
public sealed class Backdrop
{
    Image _i0, _i1;
    ImageTexture _t0, _t1;
    Scenario _sc; World _w; float _zx = 1, _zy = 1, _ox = float.NaN, _oy, _refresh; int _nx = 16, _ny = 16;

    public void Free() { _t0?.Dispose(); _t1?.Dispose(); _i0?.Dispose(); _i1?.Dispose(); _t0 = _t1 = null; _i0 = _i1 = null; }
    public void Apply(ShaderMaterial m, World w)
    {
        _w = w; _sc = w?.Sc; _ox = float.NaN;
        var L = _sc?.LookFor(w?.ScopeFilter ?? 0);
        m.SetShaderParameter("kind", L == null ? 0 : 1);
        if (L == null) return;
        // siatka stref: domyślnie 16 × 16 pól po pół chunka; scenariusz może podać własną (np. cienkie warstwy kory)
        var g = _sc.ZoneGrid;
        if (g.nx != _nx || g.ny != _ny || _i0 == null)
        {
            Free();
            _nx = g.nx; _ny = g.ny;
            _i0 = Image.CreateEmpty(_nx, _ny, false, Image.Format.Rgba8); _i1 = Image.CreateEmpty(_nx, _ny, false, Image.Format.Rgba8);
        }
        _zx = g.cx; _zy = g.cy;
        m.SetShaderParameter("b1_col", L.B1Col); m.SetShaderParameter("b1_p", L.B1); m.SetShaderParameter("b1_q", L.B1Q);
        m.SetShaderParameter("b2_col", L.B2Col); m.SetShaderParameter("b2_p", L.B2); m.SetShaderParameter("b2_q", L.B2Q);
        m.SetShaderParameter("r_col", L.RodCol); m.SetShaderParameter("r_p", L.Rod); m.SetShaderParameter("r_q", L.RodQ);
        m.SetShaderParameter("d_col", L.PtsCol); m.SetShaderParameter("d_p", L.Pts);
        m.SetShaderParameter("r_n", L.Needle); m.SetShaderParameter("r_ncol", L.NeedleCol);
        m.SetShaderParameter("rod_mode", L.RodMode); m.SetShaderParameter("rod_band", L.RodBand);
        m.SetShaderParameter("wseed", (float)(w.Seed % 997));
        m.SetShaderParameter("zone_span", new Vector2(_zx * _nx, _zy * _ny));
        // nowa tekstura dla innej siatki
        m.SetShaderParameter("zone0", _t0); m.SetShaderParameter("zone1", _t1);
    }

    public void Update(ShaderMaterial m, float camX, float camY, float pxUm, float dt)
    {
        if (_sc?.LookFor(_w?.ScopeFilter ?? 0) == null || _w == null || _i0 == null) return;
        m.SetShaderParameter("cam", new Vector2(camX, camY)); m.SetShaderParameter("px_um", pxUm);
        float ox = (MathF.Floor(camX / _zx) - _nx / 2) * _zx, oy = (MathF.Floor(camY / _zy) - _ny / 2) * _zy;
        _refresh -= dt;
        if (ox == _ox && oy == _oy && _refresh > 0) return;
        _ox = ox; _oy = oy; _refresh = 0.5f;
        for (int j = 0; j < _ny; j++)
            for (int i = 0; i < _nx; i++)
            {
                var z = _sc.Backdrop(_w, ox + (i + 0.5f) * _zx, oy + (j + 0.5f) * _zy);
                _i0.SetPixel(i, j, new Color(Clamp(z.B1, 0, 1), Clamp(z.B2, 0, 1), Clamp(z.Rod, 0, 1), Clamp(z.Pts, 0, 1)));
                _i1.SetPixel(i, j, new Color(z.Tint.R, z.Tint.G, z.Tint.B, Clamp(z.Haze, 0, 1)));
            }
        if (_t0 == null) { _t0 = ImageTexture.CreateFromImage(_i0); _t1 = ImageTexture.CreateFromImage(_i1); m.SetShaderParameter("zone0", _t0); m.SetShaderParameter("zone1", _t1); }
        else { _t0.Update(_i0); _t1.Update(_i1); }
        m.SetShaderParameter("zone_o", new Vector2(ox, oy));
    }
}

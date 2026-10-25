namespace Mikro;

/// <summary>Nakładka ekranowa (bez filtra trybu mikroskopu): celownik, wskaźniki zmysłu chemicznego, minimapa, podziałka.</summary>
public partial class Overlay : Node2D
{
    public World World;
    public float CamX, CamY, S, Stretch = 1;
    public Vector2 ScreenSize;
    public bool ShowHud = true, ShowMap = true;
    /// <summary>Szczegóły (F8): radar i pełny HUD.</summary>
    public bool Debug;
    /// <summary>Pasek danych: tryb i obiektyw, powiększenie (rozbłyska po kliknięciu zoomu), tempo czasu.</summary>
    public string DataCode = "", DataMag = "", DataTime = ""; public float MagFlash;
    readonly List<(Vector2 pos, string text, int size, Color col)> _texts = new();
    /// <summary>Tryb podglądu (F): obiekt pod kursorem, wybrany obiekt albo punkt tła, nazwa przy kursorze.</summary>
    public bool Inspect; public Ent InspHover, InspSel; public Vector2? InspPoint; public string InspHoverName = ""; public Vector2 InspMouse;
    readonly Painter _p = new();
    float _senseT; bool _hasFood, _hasThreat; float _fx, _fy, _tx, _ty;
    readonly List<(Vector2 pos, string text, Color col)> _labels = new();
    Rect2 _map = new(1e6f, 1e6f, 0, 0);
    World _senseW;

    public override void _Process(double delta)
    {
        if (World == null || !ShowHud) return;
        if (MagFlash > 0) MagFlash = Math.Max(0, MagFlash - (float)delta * 2.2f);
        // nowy świat (restart, inny tryb) – wskaźniki od razu z nowego świata, nie z poprzedniego
        if (World != _senseW) { _senseW = World; _senseT = 0; }
        _senseT -= (float)delta;
        if (_senseT <= 0)
        {
            _senseT = 0.25f;
            _hasFood = World.Sc.FoodPoint(World, out _fx, out _fy);
            _hasThreat = World.Sc.ThreatPoint(World, out _tx, out _ty);
        }
    }

    /// <summary>Czas budowania nakładki w ostatniej klatce (ms) – test wydajności.</summary>
    public double LastMs;
    public override void _Draw()
    {
        if (World == null || !ShowHud) return;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        try { DrawAll(); } finally { LastMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
    }
    void DrawAll()
    {
        _labels.Clear(); _texts.Clear();
        _p.Begin(1f / Math.Max(0.25f, Stretch));
        if (Inspect) DrawInspect();
        else
        {
            DrawReticle();
            if (World.Nano) DrawScanner(); else DrawSense();
        }
        if (ShowMap && Debug && !Inspect) DrawMinimap();
        DrawDataBar();
        _p.Flush(GetCanvasItem());
        var font = Ui.Scope; var halo = new Color(0, 0, 0, 0.6f);
        foreach (var (pos, text0, col) in _labels)
        {
            string text = Loc.T(text0);
            var at = pos + new Vector2(-font.GetStringSize(text, HorizontalAlignment.Left, -1, 15).X / 2, 0);
            DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, 15, 5, halo);
            DrawString(font, at, text, HorizontalAlignment.Left, -1, 15, col);
        }
        foreach (var (pos, text, size, col) in _texts)
        {
            DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1, size, 6, halo);
            DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, col);
        }
    }

    Vector2 ToScreen(float x, float y) => new((x - CamX) * S + ScreenSize.X / 2, (y - CamY) * S + ScreenSize.Y / 2);

    void DrawReticle()
    {
        var p = World.Player;
        if (p == null || !p.Alive || World.Over) return;
        var c = ToScreen(p.X, p.Y);
        float r = (p.Kind == Kind.Phage ? 0.16f : p.R * 0.75f) * S;
        if (p.Kind == Kind.Phage) r = Math.Max(r, 34);
        if (r > 260 || r < 6) return;
        r += 6 + MathF.Sin(World.T * 3) * 3;
        float k = Math.Min(18, r * 0.4f);
        var col = World.Em ? new Color(0.55f, 0.9f, 1f, 0.6f) : new Color(1, 1, 1, 0.6f);
        Span<Vector2> s = stackalloc Vector2[3];
        foreach (var (ax, ay) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
        {
            s[0] = new(c.X + ax * r, c.Y + ay * r - ay * k); s[1] = new(c.X + ax * r, c.Y + ay * r); s[2] = new(c.X + ax * r - ax * k, c.Y + ay * r);
            _p.Stroke(s, false, 1.6f, col, true);
        }
    }

    /// <summary>Strzałki na krawędzi ekranu: najbliższy pokarm (zielona) i zagrożenie (czerwona) – "zmysł chemiczny".</summary>
    void DrawSense()
    {
        var p = World.Player;
        if (p == null || !p.Alive || World.Over) return;
        if (_hasFood) Arrow(_fx, _fy, new Color(0.55f, 0.95f, 0.6f, 0.85f), p);
        if (_hasThreat) Arrow(_tx, _ty, new Color(1f, 0.45f, 0.4f, 0.9f), p);
    }
    void Arrow(float wx, float wy, Color col, Ent p)
    {
        var t = ToScreen(wx, wy);
        var ss = ScreenSize;
        const float M = 46;
        if (t.X > M && t.X < ss.X - M && t.Y > M && t.Y < ss.Y - M) return;
        var c = ss / 2; var d = t - c;
        float sx = d.X != 0 ? (ss.X / 2 - M) / MathF.Abs(d.X) : 1e9f, sy = d.Y != 0 ? (ss.Y / 2 - M) / MathF.Abs(d.Y) : 1e9f;
        var e = c + d * Math.Min(sx, sy);
        // nie zasłaniaj opisu w lewym górnym rogu (w szczegółach F8 – panelu i prawego górnego opisu) ani minimapy
        float hx = Debug ? 330 : 440, hy = Debug ? 380 : 210;
        if (e.X < hx && e.Y < hy) { if (MathF.Abs(d.X) > MathF.Abs(d.Y)) e.Y = hy + 10; else e.X = hx + 10; }
        if (Debug && e.X > ss.X - 320 && e.Y < 130) e.Y = 140;
        if (e.Y > ss.Y - 80) e.Y = ss.Y - 80;
        if (ShowMap && Debug && e.X > _map.Position.X - 50 && e.Y > _map.Position.Y - 40) { if (MathF.Abs(d.X) > MathF.Abs(d.Y)) e.Y = _map.Position.Y - 50; else e.X = _map.Position.X - 60; }
        float a = MathF.Atan2(d.Y, d.X), pulse = 1 + 0.12f * MathF.Sin(World.T * 5);
        _p.Push(); _p.Translate(e.X, e.Y); _p.Rotate(a); _p.Scale(pulse);
        Span<Vector2> tri = stackalloc Vector2[] { new(14, 0), new(-8, -9), new(-4, 0), new(-8, 9) };
        _p.FillFan(tri, 0, 0, col);
        _p.Pop();
        float dist = Hypot(wx - p.X, wy - p.Y);
        string lbl = dist >= 1000 ? $"{dist / 1000:0.0} mm" : dist >= 1 ? $"{dist:0} µm" : $"{dist * 1000:0} nm";
        _labels.Add((e - new Vector2(MathF.Cos(a), MathF.Sin(a)) * 40 + new Vector2(0, 5), lbl, col));
    }

    void DrawMinimap()
    {
        var w = World; var ss = ScreenSize;
        float wx0 = w.Infinite ? w.WinX0 : 0, wy0 = w.Infinite ? w.WinY0 : 0, ww = w.Infinite ? w.WinW : w.W, wh = w.Infinite ? w.WinH : w.H;
        float mw = 230, mh = mw * wh / ww;
        if (mh > 200) { mh = 200; mw = mh * ww / wh; }
        float x0 = ss.X - 18 - mw, y0 = ss.Y - 64 - mh;
        _map = new Rect2(x0 - 7, y0 - 20, mw + 14, mh + 27);
        Span<Vector2> box = stackalloc Vector2[] { new(x0 - 7, y0 - 7), new(x0 + mw + 7, y0 - 7), new(x0 + mw + 7, y0 + mh + 7), new(x0 - 7, y0 + mh + 7) };
        _p.FillFan(box, x0 + mw / 2, y0 + mh / 2, new Color(0.05f, 0.065f, 0.07f, 0.72f));
        _p.Stroke(box, true, 1, new Color(1, 1, 1, 0.12f));
        float k = mw / ww;
        _p.Push();
        _p.Translate(x0, y0); _p.Scale(k); _p.Translate(-wx0, -wy0);
        if (w.Infinite)
        {
            // kolory biomów aktywnych chunków + siatka chunków
            foreach (var c in w.Active) _p.Rect(c.X0, c.Y0, c.S, c.S, WithA(w.Sc.BiomeColors[Math.Clamp(c.Biome, 0, w.Sc.BiomeColors.Length - 1)], 0.2f));
            var gl = new Color(1, 1, 1, 0.07f);
            for (int i = 0; i <= 2 * w.AR + 1; i++)
            {
                _p.Seg(wx0 + i * w.CS, wy0, wx0 + i * w.CS, wy0 + wh, 1 / k, gl);
                _p.Seg(wx0, wy0 + i * w.CS, wx0 + ww, wy0 + i * w.CS, 1 / k, gl);
            }
        }
        w.Sc.DrawMinimap(w, _p);
        float dot = 1.6f / k;
        var colFood = new Color(0.5f, 0.95f, 0.55f, 0.9f); var colThreat = new Color(1f, 0.4f, 0.35f, 0.95f); var colAlly = new Color(0.45f, 0.85f, 1f, 0.95f); var colFix = new Color(0.75f, 0.78f, 0.72f, 0.28f);
        foreach (var e in w.Ents)
        {
            if (!e.Alive || e == w.Player) continue;
            if (e.X < wx0 || e.Y < wy0 || e.X > wx0 + ww || e.Y > wy0 + wh) continue;
            if (e.Fixed)
            {
                if (e.R * k < 0.5f) continue;
                if (e.Prim.Cap) _p.Seg(e.Prim.Ax, e.Prim.Ay, e.Prim.Bx, e.Prim.By, Math.Max(e.Prim.R * 2, 1 / k), colFix);
                else _p.Dot(e.X, e.Y, Math.Max(e.Prim.R, dot * 0.6f), colFix);
                continue;
            }
            int mk = w.Sc.MapKind(w, e);
            if (mk == 0) continue;
            _p.Dot(e.X, e.Y, mk == 2 ? dot * 1.25f : dot, mk == 1 ? colFood : mk == 2 ? colThreat : colAlly);
        }
        // widoczny obszar
        float hw = ss.X / 2 / S, hh = ss.Y / 2 / S;
        Span<Vector2> view = stackalloc Vector2[] { new(CamX - hw, CamY - hh), new(CamX + hw, CamY - hh), new(CamX + hw, CamY + hh), new(CamX - hw, CamY + hh) };
        _p.Stroke(view, true, 1.2f / k, new Color(1, 1, 1, 0.55f));
        var pl = w.Player;
        if (pl != null && pl.Alive)
        {
            _p.FillCircle(pl.X, pl.Y, 3.2f / k, Colors.White);
            _p.Circle(pl.X, pl.Y, (5.5f + MathF.Sin(w.T * 4) * 1.5f) / k, 1.2f / k, new Color(1, 1, 1, 0.6f));
        }
        _p.Pop();
        _labels.Add((new Vector2(x0 + (w.Infinite ? 30 : 26), y0 - 12), w.Infinite ? "RADAR" : "MAPA", new Color(1, 1, 1, 0.5f)));
    }

    /// <summary>Skaner nanobota: obrys i opis organizmu pod kursorem.</summary>
    void DrawScanner()
    {
        var I = World.Input; if (!I.HasAim || World.Player == null) return;
        var e = Nanobot.Pick(World, I.AimX, I.AimY, World.Sc.ViewW * 0.03f, World.Player);
        if (e == null) return;
        var tgt = e is ChainSeg cs ? (Ent)cs.Owner : e;
        var c = ToScreen(tgt.X, tgt.Y);
        float r = Math.Clamp(Math.Max(tgt.Radius, Math.Max(tgt.Length * 0.5f, e.Prim.R)) * S + 8, 14, 420);
        var col = new Color(0.55f, 0.9f, 1f, 0.85f);
        float t = World.T;
        for (int k = 0; k < 4; k++)
        {
            float a0 = k * PI / 2 + t * 0.8f;
            int n = 10; Span<Vector2> arc = stackalloc Vector2[n];
            for (int i = 0; i < n; i++) { float a = a0 + i * 0.9f / (n - 1); arc[i] = new Vector2(c.X + MathF.Cos(a) * r, c.Y + MathF.Sin(a) * r); }
            _p.Stroke(arc, false, 2, col, true);
        }
        string info = e.Info;
        string state = tgt.State switch { St.Cup or St.Vac => " · pochłaniany", St.Caught => " · schwytany", St.Avoid => " · reakcja unikania", _ => "" };
        if (tgt.Hp < 0.999f && tgt.Alive) state += $" · integralność {tgt.Hp * 100:0}%";
        _labels.Add((new Vector2(c.X, c.Y - r - 12), info + state, col));
    }

    void Ring(Ent e, Color col, float w, float spin)
    {
        var tgt = e is ChainSeg cs ? (Ent)cs.Owner : e;
        var c = ToScreen(e.X, e.Y);
        float r = Math.Clamp(Math.Max(e.Radius, Math.Max(e.Length * 0.5f, e.Prim.R)) * S + 7, 10, 520);
        for (int k = 0; k < 4; k++)
        {
            float a0 = k * PI / 2 + spin;
            const int n = 10; Span<Vector2> arc = stackalloc Vector2[n];
            for (int i = 0; i < n; i++) { float a = a0 + i * 1.0f / (n - 1); arc[i] = new Vector2(c.X + MathF.Cos(a) * r, c.Y + MathF.Sin(a) * r); }
            _p.Stroke(arc, false, w, col, true);
        }
    }
    void DrawInspect()
    {
        float t = World.T + (float)Time.GetTicksMsec() / 1000f;
        if (InspSel != null && InspSel.Alive) Ring(InspSel, new Color(0.55f, 0.9f, 1f, 0.95f), 2.5f, t * 0.6f);
        if (InspPoint is Vector2 pt)
        {
            var c = ToScreen(pt.X, pt.Y);
            _p.Circle(c.X, c.Y, 12, 2, new Color(0.55f, 0.9f, 1f, 0.95f));
            _p.Line(c.X - 18, c.Y, c.X - 6, c.Y, 2, new Color(0.55f, 0.9f, 1f, 0.95f)); _p.Line(c.X + 6, c.Y, c.X + 18, c.Y, 2, new Color(0.55f, 0.9f, 1f, 0.95f));
            _p.Line(c.X, c.Y - 18, c.X, c.Y - 6, 2, new Color(0.55f, 0.9f, 1f, 0.95f)); _p.Line(c.X, c.Y + 6, c.X, c.Y + 18, 2, new Color(0.55f, 0.9f, 1f, 0.95f));
        }
        if (InspHover != null && InspHover != InspSel) Ring(InspHover, new Color(1, 1, 1, 0.7f), 1.5f, 0);
        if (InspHoverName.Length > 0) _labels.Add((InspMouse + new Vector2(0, -22), InspHoverName, new Color(1, 1, 1, 0.95f)));
    }

    /// <summary>
    /// Pasek danych jak na mikrografii z mikroskopu elektronowego: z lewej tryb, obiektyw/napięcie, powiększenie i tempo czasu,
    /// z prawej kropkowana podziałka (11 kresek = 10 działek, długość z szeregu 1–2–5, ok. 1/7 kadru) z opisem pod nią.
    /// </summary>
    void DrawDataBar()
    {
        var font = Ui.Scope; const int fs = 22;
        float y = ScreenSize.Y - 20, x = 26;
        var white = new Color(0.95f, 0.96f, 0.93f, 0.96f);
        var magCol = white.Lerp(new Color(1f, 0.84f, 0.42f), Math.Min(1, MagFlash));
        foreach (var (str, col) in new[] { (DataCode, white), (DataMag, magCol), (DataTime, new Color(0.86f, 0.9f, 0.85f, 0.85f)) })
        {
            if (string.IsNullOrEmpty(str)) continue;
            _texts.Add((new Vector2(x, y), str, fs, col));
            x += font.GetStringSize(str, HorizontalAlignment.Left, -1, fs).X + fs * 1.2f;
        }
        float target = ScreenSize.X * 0.14f / S;
        float p = MathF.Pow(10, MathF.Floor(MathF.Log10(target))), len = p;
        foreach (float m in new[] { 1f, 2f, 5f, 10f }) if (m * p <= target) len = m * p;
        float px = len * S, xr = ScreenSize.X - 26, x0 = xr - px, ty = y - fs * 0.8f;
        string lbl = Scenario.LenStr(len);
        _texts.Add((new Vector2(xr - font.GetStringSize(lbl, HorizontalAlignment.Left, -1, fs).X, y), lbl, fs, white));
        var dark = new Color(0, 0, 0, 0.55f);
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i <= 10; i++)
            {
                float tx = x0 + px * i / 10, h = i == 0 || i == 10 ? 9 : 6;
                if (pass == 0) _p.Rect(tx - 2.2f, ty - h - 1.2f, 4.4f, h + 2.4f, dark); else _p.Rect(tx - 1.1f, ty - h, 2.2f, h, white);
            }
    }

}

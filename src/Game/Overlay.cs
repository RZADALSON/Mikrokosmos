namespace Mikro;

/// <summary>Nakładka ekranowa (bez filtra trybu mikroskopu): celownik, wskaźniki zmysłu chemicznego, minimapa, podziałka.</summary>
public partial class Overlay : Node2D
{
    public World World;
    public float CamX, CamY, S, Stretch = 1;
    public Vector2 ScreenSize;
    public bool ShowHud = true, ShowMap = true;
    readonly Painter _p = new();
    float _senseT; bool _hasFood, _hasThreat; float _fx, _fy, _tx, _ty;
    readonly List<(Vector2 pos, string text, Color col)> _labels = new();
    Rect2 _map = new(1e6f, 1e6f, 0, 0);

    public override void _Process(double delta)
    {
        if (World == null || !ShowHud) return;
        _senseT -= (float)delta;
        if (_senseT <= 0)
        {
            _senseT = 0.25f;
            _hasFood = World.Sc.FoodPoint(World, out _fx, out _fy);
            _hasThreat = World.Sc.ThreatPoint(World, out _tx, out _ty);
        }
    }

    public override void _Draw()
    {
        if (World == null || !ShowHud) return;
        _labels.Clear();
        _p.Begin(1f / Math.Max(0.25f, Stretch));
        DrawReticle();
        DrawSense();
        if (ShowMap) DrawMinimap();
        DrawScaleBar();
        _p.Flush(GetCanvasItem());
        var font = Ui.FontBold;
        foreach (var (pos, text, col) in _labels)
        {
            var sz = font.GetStringSize(text, HorizontalAlignment.Left, -1, 13);
            DrawString(font, pos + new Vector2(-sz.X / 2 + 1, 1), text, HorizontalAlignment.Left, -1, 13, new Color(0, 0, 0, 0.55f));
            DrawString(font, pos + new Vector2(-sz.X / 2, 0), text, HorizontalAlignment.Left, -1, 13, col);
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
        // nie zasłaniaj paneli HUD (lewy górny, prawy górny) ani minimapy (prawy dolny)
        if (e.X < 330 && e.Y < 380) { if (MathF.Abs(d.X) > MathF.Abs(d.Y)) e.Y = 390; else e.X = 340; }
        if (e.X > ss.X - 320 && e.Y < 130) e.Y = 140;
        if (e.Y > ss.Y - 80) e.Y = ss.Y - 80;
        if (ShowMap && e.X > _map.Position.X - 50 && e.Y > _map.Position.Y - 40) { if (MathF.Abs(d.X) > MathF.Abs(d.Y)) e.Y = _map.Position.Y - 50; else e.X = _map.Position.X - 60; }
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

    void DrawScaleBar()
    {
        float target = 150 / S;
        float p = MathF.Pow(10, MathF.Floor(MathF.Log10(target)));
        float len = p;
        foreach (float m in new[] { 1f, 2f, 5f, 10f }) if (m * p <= target) len = m * p;
        float px = len * S, x1 = ScreenSize.X - 28, x0 = x1 - px, y = ScreenSize.Y - 34;
        _p.Rect(x0 - 1, y - 1, px + 2, 6, new Color(0, 0, 0, 0.35f));
        _p.Rect(x0, y, px, 4, Colors.White);
        string lbl = len >= 1000 ? $"{len / 1000:0.###} mm" : len >= 1 ? $"{len:0.###} µm" : $"{MathF.Round(len * 1000)} nm";
        _labels.Add((new Vector2((x0 + x1) / 2, y - 8), lbl, Colors.White));
    }
}

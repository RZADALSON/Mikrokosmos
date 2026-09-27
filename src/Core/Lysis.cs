namespace Mikro;

/// <summary>
/// Rozpad komórki eukariotycznej (jak na nagraniach Loxodes czy pantofelka w złych warunkach):
/// 1) na błonie wyrastają pęcherze (bleby) – błona odrywa się od cytoszkieletu, komórka lekko puchnie;
/// 2) w najsłabszym miejscu błona pęka;
/// 3) ziarnista cytoplazma wypływa smugą przez otwór i jest unoszona przez wodę, a pusta błona blaknie.
/// </summary>
public sealed class Lysis : Ent
{
    readonly Vector2[] _pts; readonly int _n;
    readonly Color _body, _edge, _g1, _g2;
    readonly float _size;
    float _t, _open, _emit;
    int _breach = -1;
    struct Bleb { public int I; public float Max, T0; }
    readonly Bleb[] _blebs;
    readonly Vector3[] _gran;
    public override float Radius => _size;

    Lysis(Ent src, Vector2[] pts, int n, Color body, Color edge, Color g1, Color g2) : base(src.W, src.X, src.Y, 0)
    {
        Kind = Kind.None; Layer = src.Layer; Solid = false; FlowK = 1; Name = "rozpadająca się komórka"; Latin = src.Name;
        _pts = pts; _n = n; _body = body; _edge = edge; _g1 = g1; _g2 = g2;
        float mx = 0; for (int i = 0; i < n; i++) mx = Math.Max(mx, pts[i].Length());
        _size = mx; R = mx * 1.4f;
        _blebs = new Bleb[RandI(3, 6)];
        for (int k = 0; k < _blebs.Length; k++) _blebs[k] = new Bleb { I = RandI(0, n - 1), Max = mx * Rand(0.07f, 0.16f), T0 = Rand(0, 0.8f) };
        _gran = new Vector3[70];
        for (int i = 0; i < _gran.Length; i++) { var p = pts[RandI(0, n - 1)] * MathF.Sqrt(R01()) * 0.85f; _gran[i] = new Vector3(p.X, p.Y, Rand(0.6f, 1.4f)); }
    }

    /// <summary>Zamiast natychmiastowego zniknięcia – animowany rozpad z kształtem i barwą komórki.</summary>
    public static void Start(Ent e, Color body, Color edge, Color g1, Color g2)
    {
        var m = e.Mem; if (m == null) return;
        m.Lod = true; m.Follow(e.X, e.Y, e.A, e.MemK, 0.016f);
        var buf = new Vector2[512];
        int ns = Painter.SmoothClosed(m.Px, m.Py, m.N, buf, 2);
        int step = Math.Max(1, ns / 96), n = 0;
        var pts = new Vector2[ns / step + 1];
        for (int i = 0; i < ns && n < pts.Length; i += step) pts[n++] = new Vector2(buf[i].X - e.X, buf[i].Y - e.Y);
        e.W.Add(new Lysis(e, pts, n, body, edge, g1, g2));
    }

    public override void Step(float dt, float gdt)
    {
        _t += gdt;
        Vx = Vy = 0;
        float s = _size;
        if (_t > 1.2f && _breach < 0)
        {
            // pęka największy pęcherz
            int bi = 0; for (int k = 1; k < _blebs.Length; k++) if (_blebs[k].Max > _blebs[bi].Max) bi = k;
            _breach = _blebs[bi].I;
            var p = _pts[_breach]; float l = Math.Max(p.Length(), 1e-3f);
            W.Fx.Spill(X + p.X, Y + p.Y, 30, new FX.SpillOpt { Speed = s * 0.5f, R = s * 0.02f, Life = 8, R0 = s * 0.05f, D = s * s * 0.0002f, Dir = MathF.Atan2(p.Y, p.X), Spread = 0.6f, Cols = new[] { _g1, _g2 } });
            W.Sfx("pop", X, Y, 0.9f);
        }
        if (_breach >= 0)
        {
            _open = Math.Min(0.3f, _open + gdt * 0.12f);
            // cytoplazma wypływa smugą przez otwór (strumień słabnie)
            _emit += gdt * 45 * Math.Max(0, 1 - (_t - 1.2f) / 2.6f);
            var p = _pts[_breach];
            while (_emit >= 1)
            {
                _emit -= 1;
                W.Fx.Spill(X + p.X * 0.95f, Y + p.Y * 0.95f, 1, new FX.SpillOpt { Speed = s * Rand(0.1f, 0.35f), R = s * 0.018f, Life = Rand(5, 9), R0 = s * 0.04f, D = s * s * 0.0003f, Dir = MathF.Atan2(p.Y, p.X), Spread = 0.45f, Cols = new[] { _g1, _g2, _g1 } });
            }
            // ziarna wewnątrz przesuwają się ku otworowi
            float k = Damp(0.8f, gdt);
            for (int i = 0; i < _gran.Length; i++) { var g = _gran[i]; _gran[i] = new Vector3(g.X + (p.X - g.X) * k * 0.5f, g.Y + (p.Y - g.Y) * k * 0.5f, g.Z * (1 - gdt * 0.35f)); }
        }
        if (_t > 6.5f) Alive = false;
    }

    public override void Draw(Painter p, View v)
    {
        float swell = 1 + 0.06f * Math.Min(1, _t / 1.2f) - (_t > 1.4f ? Math.Min(0.12f, (_t - 1.4f) * 0.04f) : 0);
        float fillA = _t < 1.2f ? 1 : Math.Max(0, 1 - (_t - 1.2f) / 2.8f), edgeA = _t < 3.5f ? 1 : Math.Max(0, 1 - (_t - 3.5f) / 3);
        p.Local(X, Y, 0, swell);
        var sh = p.Buf2;
        for (int i = 0; i < _n; i++) sh[i] = _pts[i];
        var poly = new ReadOnlySpan<Vector2>(sh, 0, _n);
        if (fillA > 0.01f) p.FillFan(poly, 0, 0, Mul(_body, fillA));
        // ziarnista cytoplazma
        foreach (var g in _gran) if (g.Z > 0.15f) p.Dot(g.X, g.Y, _size * 0.012f * g.Z, Mul(_g1, fillA * 0.9f));
        // pęcherze na błonie
        foreach (var b in _blebs)
        {
            float r = b.Max * Smooth((_t - b.T0) / 0.6f) * (b.I == _breach && _t > 1.2f ? Math.Max(0, 1 - (_t - 1.2f) * 4) : 1);
            if (r <= v.Px) continue;
            var q = _pts[b.I]; float l = Math.Max(q.Length(), 1e-3f); float cx = q.X + q.X / l * r * 0.6f, cy = q.Y + q.Y / l * r * 0.6f;
            p.FillCircle(cx, cy, r, Rgb(235, 240, 225, 0.25f * edgeA));
            p.Circle(cx, cy, r, Math.Max(v.Px, r * 0.06f), Mul(_edge, 0.8f * edgeA));
            p.Dot(cx - r * 0.35f, cy - r * 0.35f, r * 0.12f, Rgb(255, 255, 250, 0.6f * edgeA));
        }
        // błona – z otworem w miejscu pęknięcia
        if (edgeA > 0.01f)
        {
            int gap = _breach < 0 ? 0 : (int)(_open * _n / 2);
            var col = Mul(_edge, edgeA); float lw = Math.Max(v.Px, _size * 0.012f);
            if (gap == 0) p.Stroke(poly, true, lw, col);
            else
            {
                int len = _n - 2 * gap - 1, st = (_breach + gap + 1) % _n;
                if (len > 1)
                {
                    var ob = p.Buf;
                    for (int i = 0; i < len; i++) ob[i] = _pts[(st + i) % _n];
                    // brzegi rozdarcia lekko się podwijają
                    ob[0] = ob[0] * 0.97f; ob[len - 1] = ob[len - 1] * 0.97f;
                    p.Stroke(new ReadOnlySpan<Vector2>(ob, 0, len), false, lw, col, true);
                }
            }
        }
    }
}

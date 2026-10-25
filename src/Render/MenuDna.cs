namespace Mikro;

/// <summary>
/// Tło menu: nieruchome, ciemne pole widzenia, przez które powoli przepływają podwójne helisy DNA (forma B).
/// Geometria jak w prawdziwej cząsteczce: helisa prawoskrętna, średnica ≈ 2 nm, wznios ≈ 0,34 nm na parę zasad,
/// ≈ 10,4 pary na skręt w roztworze (Wang 1979), dwie nici przesunięte o ≈ 130° – stąd bruzda mniejsza (~1,2 nm)
/// i większa (~2,2 nm); pary A–T i G–C (Watson i Crick 1953; Wing i in. 1980 – struktura B-DNA z krystalografii).
/// Helisy leżą na różnych głębokościach: bliższe i dalsze od płaszczyzny ostrości są rozmyte i ciemniejsze.
/// </summary>
public partial class MenuDna : Node2D
{
    /// <summary>Rozmiar piksela ekranu w jednostkach węzła (1 / skala okna).</summary>
    public float Px = 1;
    readonly Painter _p = new(false, 30000);

    sealed class Helix
    {
        public float Y, Tilt, Scale, Speed, Spin, Phase, X0, Blur, Bright; public byte[] Seq;
    }
    readonly List<Helix> _h = new();
    float _t;
    const float Rise = 0.34f, Twist = TAU / 10.4f, Radius = 1.0f, Groove = 0.36f * TAU;

    public override void _Ready()
    {
        // kilka helis na różnych głębokościach (skala = px na nm); ostra – średnia skala
        var spec = new (float y, float tilt, float scale, float blur, float bright, float speed)[]
        {
            (0.075f, 0.03f, 9f, 2.4f, 0.3f, 6f),     // daleko, rozmyta
            (0.19f, -0.025f, 15f, 0f, 1f, 12f),      // w płaszczyźnie ostrości, pod tytułem
            (0.52f, -0.16f, 34f, 5.5f, 0.45f, 20f),  // blisko obiektywu, mocno rozmyta – przez cały ekran, za kartami
            (0.89f, 0.02f, 12f, 0f, 0.85f, 9f),      // ostra, nad stopką
        };
        ulong rng = 0x5DEECE66DUL;
        byte R4() { rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17; return (byte)(rng >> 62); }
        foreach (var s in spec)
        {
            var seq = new byte[512];
            for (int i = 0; i < seq.Length; i++) seq[i] = R4(); // 0 A, 1 T, 2 G, 3 C (nić wiodąca); druga nić – komplementarna
            _h.Add(new Helix { Y = s.y, Tilt = s.tilt, Scale = s.scale, Blur = s.blur, Bright = s.bright, Speed = s.speed, Spin = TAU * 0.12f, Phase = (float)(rng % 1000) / 1000f * TAU, X0 = (float)(rng % 777), Seq = seq });
        }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _t += (float)Math.Min(delta, 0.05);
        QueueRedraw();
    }

    // barwy zasad (typowe w modelach: A zielona, T czerwona, G żółta, C niebieska) i szkieletu cukrowo-fosforanowego
    static readonly Color[] BaseC = { Rgb(92, 196, 110), Rgb(226, 92, 88), Rgb(236, 196, 78), Rgb(84, 146, 228) };
    static readonly Color Back = Rgb(188, 208, 222), Phos = Rgb(214, 160, 92);
    static int Pair(int b) => b ^ 1; // A↔T, G↔C

    public override void _Draw()
    {
        var size = GetViewportRect().Size;
        _p.Begin(Px);
        foreach (var h in _h) DrawHelix(h, size);
        _p.Flush(GetCanvasItem());
    }

    void DrawHelix(Helix h, Vector2 size)
    {
        float k = h.Scale * (size.Y / 900f); // px na nm, niezależnie od rozdzielczości
        float ca = MathF.Cos(h.Tilt), sa = MathF.Sin(h.Tilt);
        float len = size.X / ca + 160 * k;
        // przesuw wzdłuż osi (helisa „wpływa” z jednej strony i wypływa z drugiej)
        float shift = (h.X0 + _t * h.Speed * size.Y / 900f) % (Rise * 10.4f * k * 40);
        // wysokość osi podana dla środka ekranu (nachylenie nie przesuwa helisy na tekst)
        float x0 = -80 * k - shift, y0 = h.Y * size.Y - (size.X * 0.5f - x0) * sa / ca;
        int nbp = (int)(len / (Rise * k)) + 2;
        float spin = h.Phase + _t * h.Spin;
        float blur = h.Blur * (size.Y / 900f), al = h.Bright;
        float wBack = 0.34f * k, wBase = 0.2f * k, rBead = 0.16f * k;
        var pts = _p.Buf2;
        // 1) tylne odcinki szkieletu (dalej od obserwatora) – ciemniejsze
        for (int strand = 0; strand < 2; strand++) Backbone(h, strand, nbp, k, ca, sa, x0, y0, spin, pts, false, wBack, blur, al * 0.55f);
        // 2) pary zasad: od nici 1 do osi – zasada nici 1, od osi do nici 2 – zasada komplementarna
        for (int i = 0; i < nbp; i++)
        {
            int b = h.Seq[i % h.Seq.Length];
            Point(i, 0, k, ca, sa, x0, y0, spin, out float ax, out float ay, out float az);
            Point(i, 1, k, ca, sa, x0, y0, spin, out float bx, out float by, out float bz);
            if (ax < -60 && bx < -60 || ax > size.X + 60 && bx > size.X + 60) continue;
            float mx = (ax + bx) * 0.5f, my = (ay + by) * 0.5f, dz = (az + bz) * 0.5f;
            float shade = 0.62f + 0.38f * (dz / Radius * 0.5f + 0.5f);
            Rung(ax, ay, mx, my, wBase, Shade(BaseC[b], shade), blur, al);
            Rung(mx, my, bx, by, wBase, Shade(BaseC[Pair(b)], shade), blur, al);
        }
        // 3) przednie odcinki szkieletu i fosforany
        for (int strand = 0; strand < 2; strand++) Backbone(h, strand, nbp, k, ca, sa, x0, y0, spin, pts, true, wBack, blur, al);
        for (int i = 0; i < nbp; i++)
            for (int strand = 0; strand < 2; strand++)
            {
                Point(i + 0.5f, strand, k, ca, sa, x0, y0, spin, out float x, out float y, out float z);
                if (x < -40 || x > size.X + 40) continue;
                float f = z / Radius * 0.5f + 0.5f; // 0 tył, 1 przód
                var c = Shade(Phos, 0.45f + 0.55f * f);
                float r = rBead * (0.85f + 0.3f * f);
                if (blur > 0.5f) _p.Glow(x, y, r + blur * 1.6f, new Color(c.R, c.G, c.B, 0.35f * al));
                else { _p.FillCircle(x, y, r, new Color(c.R, c.G, c.B, 0.8f * al)); if (f > 0.5f) _p.Dot(x - r * 0.3f, y - r * 0.3f, r * 0.35f, new Color(1, 1, 0.92f, 0.45f * al * f)); }
            }
    }

    /// <summary>Punkt nici (0 lub 1) przy parze zasad i (ułamkowe – fosforan między parami) po rzucie na ekran.</summary>
    static void Point(float i, int strand, float k, float ca, float sa, float x0, float y0, float spin, out float x, out float y, out float z)
    {
        float th = i * Twist + spin + (strand == 1 ? Groove : 0);
        float s = i * Rise * k, r = Radius * k;
        // oś y ekranu biegnie w dół (układ lewoskrętny), więc głębokość ma znak ujemny – inaczej helisa byłaby lewoskrętna
        float py = MathF.Cos(th) * r; z = -MathF.Sin(th) * Radius;
        x = x0 + s * ca - py * sa; y = y0 + s * sa + py * ca;
    }

    void Backbone(Helix h, int strand, int nbp, float k, float ca, float sa, float x0, float y0, float spin, Vector2[] pts, bool front, float w, float blur, float al)
    {
        // nić jako gęsta łamana; odcinki po stronie obserwatora (z > 0) albo za osią rysowane osobno
        int n = 0; float size = GetViewportRect().Size.X;
        for (int q = 0; q <= nbp * 4; q++)
        {
            float i = q / 4f;
            Point(i, strand, k, ca, sa, x0, y0, spin, out float x, out float y, out float z);
            bool vis = (z >= 0) == front && x > -80 && x < size + 80;
            if (vis) { if (n < pts.Length) pts[n++] = new Vector2(x, y); }
            else if (n > 0) { Flush(pts, n, w, blur, al, front); n = 0; }
        }
        if (n > 0) Flush(pts, n, w, blur, al, front);
    }
    void Flush(Vector2[] pts, int n, float w, float blur, float al, bool front)
    {
        if (n < 2) return;
        var s = new ReadOnlySpan<Vector2>(pts, 0, n);
        var c = Shade(Back, front ? 1f : 0.62f);
        if (blur > 0.5f) { _p.SoftStroke(s, false, w + blur * 2.4f, new Color(c.R, c.G, c.B, 0.55f * al)); return; }
        _p.SoftStroke(s, false, w * 2.6f, new Color(c.R, c.G, c.B, 0.12f * al)); // połysk
        _p.Stroke(s, false, w, new Color(c.R, c.G, c.B, 0.9f * al), true);
        if (front) _p.Thin(s, false, w * 0.3f, new Color(1, 1, 1, 0.35f * al));
    }
    void Rung(float x1, float y1, float x2, float y2, float w, Color c, float blur, float al)
    {
        if (blur > 0.5f) { Span<Vector2> s = stackalloc Vector2[] { new(x1, y1), new(x2, y2) }; _p.SoftStroke(s, false, w + blur * 2, new Color(c.R, c.G, c.B, 0.5f * al)); return; }
        _p.Line(x1, y1, x2, y2, w, new Color(c.R, c.G, c.B, 0.85f * al), true);
    }
    static Color Shade(Color c, float k) => new(Math.Min(1, c.R * k), Math.Min(1, c.G * k), Math.Min(1, c.B * k), c.A);
}

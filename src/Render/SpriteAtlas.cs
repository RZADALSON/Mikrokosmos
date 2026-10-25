namespace Mikro;

/// <summary>
/// Atlas tekstur komórek o jednolitym wnętrzu (np. erytrocyt): obraz liczony raz, proceduralnie, tymi samymi barwami
/// co wersja wektorowa. Komórka jest rysowana jako wachlarz trójkątów rozpięty na jej (odkształcalnej) membranie
/// z tą teksturą – wygląda tak samo, ugina się tak samo, a zamiast ~1000 wierzchołków ma ~40.
/// </summary>
public static class SpriteAtlas
{
    public const int Cell = 256, Cols = 4, Rows = 4;
    public static ImageTexture Tex;
    public static Rect2[] Uv;
    /// <summary>Zapas na obwódce (halo i miękki brzeg): tekstura sięga do promienia komórki × (1 + Pad).</summary>
    public static readonly float[] Pad = new float[Cols * Rows];
    public const int Rbc = 0;
    /// <summary>Cytoplazma (ziarnistości, mętne pęcherzyki) dla ciał o wydłużeniu 1, 2, 4 i 8 : 1 – granule w teksturze są
    /// spłaszczone o tyle, o ile ciało rozciągnie teksturę, więc na komórce są okrągłe.</summary>
    public const int Cyto1 = 1, Cyto2 = 2, Cyto4 = 3, Cyto8 = 4;
    /// <summary>Cytoplazma gęsto wypełniona ziarnistościami (mastocyt, bazofil): setki ciemnych, silnie załamujących światło ziaren.</summary>
    public const int Gran = 5;
    /// <summary>Szczelina wokół pola atlasu (px) – mipmapy sąsiednich pól nie przenikają się.</summary>
    const int Gut = 4;
    public static int CytoFor(float aspect) => aspect < 1.45f ? Cyto1 : aspect < 2.9f ? Cyto2 : aspect < 5.7f ? Cyto4 : Cyto8;
    /// <summary>Jasność tła tekstury cytoplazmy (kolor wierzchołka trzeba podzielić przez nią, by zachować barwę ciała).</summary>
    public const float CytoBase = 0.8f;
    /// <summary>
    /// Komórki zarodkowej kory mózgu (tryb neuronu): niedojrzały neuron / komórka progenitorowa z dużym jądrem i cienkim
    /// rąbkiem cytoplazmy – okrągła (1 : 1), wydłużona (1,6 : 1), wrzecionowata (2,6 : 1, jądra gleju promienistego
    /// w strefie komorowej); komórka w mitozie (płytka metafazowa), komórka apoptotyczna (piknoza, ciałka), płaska komórka
    /// opon (≈ 5 : 1). Pole = elipsa wpisana w kwadrat (oś długa wzdłuż x); barwę daje kolor wierzchołków.
    /// </summary>
    public const int NeuR = 6, NeuE = 7, NeuS = 8, Mitosis = 9, Pykno = 10, Flat = 11;
    /// <summary>Drugie warianty (inny układ chromatyny i jąderek) – sąsiednie komórki nie wyglądają identycznie.</summary>
    public const int NeuR2 = 12, NeuE2 = 13;
    public static int NeuRv(int id) => (id & 1) == 0 ? NeuR : NeuR2;
    public static int NeuEv(int id) => (id & 1) == 0 ? NeuE : NeuE2;
    /// <summary>Jasność cytoplazmy w teksturach komórek kory (kolor wierzchołka dzielony przez nią zachowuje barwę).</summary>
    public const float NeuBase = 0.86f;
    /// <summary>Białe pole – zwykła (nieteksturowana) geometria rysowana malarzem z teksturą (np. halo pod ciałem).</summary>
    public const int White = 15;
    public static Vector2 WhiteUv;

    public static void Ensure()
    {
        if (Tex != null) return;
        var img = Image.CreateEmpty(Cell * Cols, Cell * Rows, false, Image.Format.Rgba8);
        Uv = new Rect2[Cols * Rows];
        for (int k = 0; k < Cols * Rows; k++)
        {
            int cx = k % Cols, cy = k / Cols;
            Uv[k] = new Rect2((cx * Cell + Gut) / (float)(Cell * Cols), (cy * Cell + Gut) / (float)(Cell * Rows), (Cell - 2f * Gut) / (Cell * Cols), (Cell - 2f * Gut) / (Cell * Rows));
        }
        PaintRbc(img, Rbc);
        PaintCyto(img, Cyto1, 1); PaintCyto(img, Cyto2, 2); PaintCyto(img, Cyto4, 4); PaintCyto(img, Cyto8, 8);
        PaintGran(img, Gran);
        PaintNeu(img, NeuR, 1, 0.8f); PaintNeu(img, NeuE, 1.6f, 0.8f); PaintNeu(img, NeuS, 2.6f, 0.86f); PaintNeu(img, Flat, 5f, 0.5f);
        PaintNeu(img, NeuR2, 1, 0.78f); PaintNeu(img, NeuE2, 1.6f, 0.82f);
        PaintMitosis(img, Mitosis); PaintPykno(img, Pykno);
        img.FillRect(new Rect2I(White % Cols * Cell, White / Cols * Cell, Cell, Cell), new Color(1, 1, 1, 1));
        WhiteUv = Uv[White].GetCenter();
        img.GenerateMipmaps();
        Tex = ImageTexture.CreateFromImage(img);
    }

    /// <summary>Nakłada kolor c (alfa niepremnożona) na piksel – jak kolejne warstwy rysunku wektorowego.</summary>
    static Color Over(Color dst, Color c)
    {
        float a = c.A + dst.A * (1 - c.A);
        if (a < 1e-6f) return new Color(0, 0, 0, 0);
        return new Color((c.R * c.A + dst.R * dst.A * (1 - c.A)) / a, (c.G * c.A + dst.G * dst.A * (1 - c.A)) / a, (c.B * c.A + dst.B * dst.A * (1 - c.A)) / a, a);
    }
    static Color Ramp(float t, float[] ts, Color[] cs)
    {
        if (t <= ts[0]) return cs[0];
        for (int i = 1; i < ts.Length; i++) if (t <= ts[i]) { float u = (t - ts[i - 1]) / (ts[i] - ts[i - 1]); return cs[i - 1].Lerp(cs[i], u); }
        return cs[^1];
    }

    /// <summary>
    /// Cytoplazma orzęsków, ameb i komórek tkanek w kontraście fazowym: drobne ciemne ziarnistości (mitochondria, rybosomy
    /// w skupiskach), jaśniejsze, silnie załamujące światło ziarna i kropelki, mętne plamy (wodniczki poza ostrością).
    /// Tekstura jest jasna (~0,8), a barwę i gradient środek–brzeg daje kolor wierzchołków. aspect – wydłużenie ciała.
    /// </summary>
    static void PaintCyto(Image img, int slot, float aspect)
    {
        int ox = slot % Cols * Cell + Gut, oy = slot / Cols * Cell + Gut, S = Cell - 2 * Gut;
        var v = new float[S * S];
        ulong rng = 0x2545F4914F6CDD1DUL + (ulong)slot * 7919;
        float Rf() { rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17; return (rng >> 40) * (1f / 16777216f); }
        // mętne plamy w dużej skali (niejednorodna gęstość cytoplazmy)
        var lo = new float[9 * 9]; for (int i = 0; i < lo.Length; i++) lo[i] = Rf();
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float fx = x / (float)S * 8, fy = y / (float)S * 8; int ix = (int)fx, iy = (int)fy; float ux = Smooth(fx - ix), uy = Smooth(fy - iy);
                float n = Lerp(Lerp(lo[iy * 9 + ix], lo[iy * 9 + ix + 1], ux), Lerp(lo[(iy + 1) * 9 + ix], lo[(iy + 1) * 9 + ix + 1], ux), uy);
                v[y * S + x] = CytoBase + (n - 0.5f) * 0.1f;
            }
        void Gran(float cx, float cy, float r, float val, float str)
        {
            // w teksturze ziarno jest węższe w osi x (oś długa ciała) o wydłużenie ciała
            float rx = r / aspect, ry = r;
            int x0 = Math.Max(0, (int)(cx - rx - 2)), x1 = Math.Min(S - 1, (int)(cx + rx + 2)), y0 = Math.Max(0, (int)(cy - ry - 2)), y1 = Math.Min(S - 1, (int)(cy + ry + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / Math.Max(rx, 0.35f), dy = (y + 0.5f - cy) / ry, d = MathF.Sqrt(dx * dx + dy * dy);
                    float a = Clamp(1.5f - d, 0, 1) * str;
                    if (a > 0) v[y * S + x] = Lerp(v[y * S + x], val, a);
                }
        }
        float area = S * S / 65536f;
        // mętne pęcherzyki i wodniczki (jaśniejsze, większe, miękkie)
        for (int i = 0; i < 60 * area; i++) Gran(Rf() * S, Rf() * S, 3 + Rf() * 6, 0.9f, 0.35f);
        // drobne ciemne ziarnistości – w skupiskach
        for (int c = 0; c < 140 * area; c++)
        {
            float cx = Rf() * S, cy = Rf() * S;
            for (int i = 0; i < 12; i++) Gran(cx + (Rf() - 0.5f) * 22 / aspect, cy + (Rf() - 0.5f) * 22, 0.7f + Rf() * 1.3f, 0.42f + Rf() * 0.2f, 0.75f);
        }
        for (int i = 0; i < 900 * area; i++) Gran(Rf() * S, Rf() * S, 0.6f + Rf() * 1.1f, 0.5f + Rf() * 0.2f, 0.6f);
        // jasne, silnie załamujące światło ziarna (lipidy, kryształki)
        for (int i = 0; i < 260 * area; i++) Gran(Rf() * S, Rf() * S, 0.8f + Rf() * 1.6f, 1f, 0.85f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++) { float g = Clamp(v[y * S + x], 0, 1); img.SetPixel(ox + x, oy + y, new Color(g, g, g * 0.985f, 1)); }
    }

    /// <summary>
    /// Mastocyt żywy, niebarwiony: cała cytoplazma wypełniona ziarnistościami ~0,3–0,5 µm (ok. 400–1000 w komórce), ciemnymi
    /// z jasnym refleksem; jaśniejsza plama jądra prawie zasłonięta. Pole = koło o promieniu komórki (jak rysunek wektorowy).
    /// </summary>
    static void PaintGran(Image img, int slot)
    {
        int ox = slot % Cols * Cell + Gut, oy = slot / Cols * Cell + Gut, S = Cell - 2 * Gut;
        var v = new float[S * S]; var a = new float[S * S];
        ulong rng = 0x9E3779B97F4A7C15UL;
        float Rf() { rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17; return (rng >> 40) * (1f / 16777216f); }
        float h = S * 0.5f;
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = MathF.Sqrt(Sq(x + 0.5f - h) + Sq(y + 0.5f - h)) / h; v[y * S + x] = 0.82f; a[y * S + x] = Clamp((1 - d) * h * 0.5f, 0, 1); }
        // jaśniejsza plama jądra
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = MathF.Sqrt(Sq(x + 0.5f - h * 1.08f) + Sq(y + 0.5f - h * 0.95f)) / (h * 0.36f); if (d < 1) v[y * S + x] = Lerp(v[y * S + x], 0.95f, (1 - d * d) * 0.6f); }
        // granule: promień komórki 6 µm ≈ h px, ziarno 0,15 µm ≈ h/40 px
        for (int i = 0; i < 1400; i++)
        {
            float t = Rf() * TAU, q = MathF.Sqrt(Rf()) * 0.9f, cx = h + MathF.Cos(t) * q * h, cy = h + MathF.Sin(t) * q * h, r = h * (0.018f + Rf() * 0.015f);
            int x0 = Math.Max(0, (int)(cx - r - 2)), x1 = Math.Min(S - 1, (int)(cx + r + 2)), y0 = Math.Max(0, (int)(cy - r - 2)), y1 = Math.Min(S - 1, (int)(cy + r + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dd = MathF.Sqrt(Sq(x + 0.5f - cx) + Sq(y + 0.5f - cy)), k = Clamp(r + 0.5f - dd, 0, 1);
                    if (k <= 0) continue;
                    // ciemne ziarno z jasnym refleksem od strony światła
                    float hl = Clamp(1 - MathF.Sqrt(Sq(x + 0.5f - cx + r * 0.35f) + Sq(y + 0.5f - cy + r * 0.35f)) / (r * 0.45f), 0, 1);
                    v[y * S + x] = Lerp(v[y * S + x], 0.3f, k * 0.8f); v[y * S + x] = Lerp(v[y * S + x], 1f, hl * 0.6f);
                }
        }
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float g = Clamp(v[y * S + x], 0, 1); img.SetPixel(ox + x, oy + y, new Color(g, g * 0.98f, g * 1.02f, a[y * S + x])); }
    }

    /// <summary>
    /// Komórka zarodkowej kory w kontraście fazowym: cienki rąbek cytoplazmy z drobnymi ziarnistościami, ciemniejsza linia błony,
    /// duże jądro (u młodych neuronów i progenitorów zajmuje większość komórki) z plamistą chromatyną, grudkami heterochromatyny
    /// przy otoczce i 1–2 jąderkami. Elipsa o wydłużeniu aspect (oś długa wzdłuż x) – kwadratowy sprite daje prawdziwy kształt.
    /// </summary>
    static void PaintNeu(Image img, int slot, float aspect, float nucFrac)
    {
        int ox = slot % Cols * Cell + Gut, oy = slot / Cols * Cell + Gut, S = Cell - 2 * Gut;
        ulong rng = 0xC2B2AE3D27D4EB4FUL + (ulong)slot * 104729;
        float Rf() { rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17; return (rng >> 40) * (1f / 16777216f); }
        float h = S * 0.5f, a = S * 0.47f, b = a / aspect;
        var lo = new float[10 * 10]; for (int i = 0; i < lo.Length; i++) lo[i] = Rf();
        float Lo(float x, float y)
        {
            float fx = x / S * 8, fy = y / S * 8; int ix = Math.Clamp((int)fx, 0, 8), iy = Math.Clamp((int)fy, 0, 8); float ux = Smooth(fx - ix), uy = Smooth(fy - iy);
            return Lerp(Lerp(lo[iy * 10 + ix], lo[iy * 10 + ix + 1], ux), Lerp(lo[(iy + 1) * 10 + ix], lo[(iy + 1) * 10 + ix + 1], ux), uy);
        }
        // jądro lekko przesunięte (u neuronów – w stronę dawnej wypustki wiodącej); półosie w pikselach
        float nx0 = h + a * 0.04f, an = a * nucFrac, bn = Math.Min(b * 0.86f, b * nucFrac * 1.05f);
        var v = new float[S * S]; var al = new float[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f - h) / a, w = (y + 0.5f - h) / b, d = MathF.Sqrt(u * u + w * w);
                al[y * S + x] = Clamp((1 - d) * b / 1.8f, 0, 1);
                float c = NeuBase + (Lo(x, y) - 0.5f) * 0.06f;
                c -= 0.13f * Smooth(Clamp((d - 0.9f) / 0.1f, 0, 1));                       // błona – ciemniejszy brzeg
                float nu = (x + 0.5f - nx0) / an, nw = (y + 0.5f - h) / bn, dn = MathF.Sqrt(nu * nu + nw * nw);
                if (dn < 1)
                {
                    float k = Clamp((1 - dn) * bn / 1.4f, 0, 1);
                    float nv = 0.66f + (Lo(x * 2.3f % S, y * 2.3f % S) - 0.5f) * 0.12f;      // plamista chromatyna
                    nv -= 0.08f * Smooth(Clamp((dn - 0.86f) / 0.1f, 0, 1));                  // otoczka jądrowa
                    c = Lerp(c, nv, k);
                }
                v[y * S + x] = c;
            }
        void Blob(float cx, float cy, float r, float val, float str)
        {
            int x0 = Math.Max(0, (int)(cx - r - 2)), x1 = Math.Min(S - 1, (int)(cx + r + 2)), y0 = Math.Max(0, (int)(cy - r - 2)), y1 = Math.Min(S - 1, (int)(cy + r + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dd = MathF.Sqrt(Sq(x + 0.5f - cx) + Sq(y + 0.5f - cy)), k = Clamp(r + 0.7f - dd, 0, 1) * str;
                    if (k > 0) v[y * S + x] = Lerp(v[y * S + x], val, k);
                }
        }
        // cytoplazma: drobne ziarnistości (mitochondria, szorstka siateczka) i nieliczne jasne kropelki
        for (int i = 0; i < 90; i++)
        {
            float t = Rf() * TAU, q = 0.55f + 0.4f * MathF.Sqrt(Rf());
            float cx = h + MathF.Cos(t) * a * q, cy = h + MathF.Sin(t) * b * q;
            if (Sq((cx - nx0) / an) + Sq((cy - h) / bn) < 1.1f) continue;
            Blob(cx, cy, Math.Max(1.2f, a * (0.012f + Rf() * 0.018f)), Rf() < 0.25f ? 0.97f : 0.7f, 0.7f);
        }
        // heterochromatyna: grudki głównie przy otoczce jądrowej
        for (int i = 0; i < 26; i++)
        {
            float t = Rf() * TAU, q = 0.55f + 0.38f * MathF.Sqrt(Rf());
            Blob(nx0 + MathF.Cos(t) * an * q, h + MathF.Sin(t) * bn * q, Math.Max(1.2f, bn * (0.05f + Rf() * 0.06f)), 0.5f, 0.55f);
        }
        // jąderko (u części komórek drugie, mniejsze – wyraźnie oddalone od pierwszego)
        float j1x = nx0 + (Rf() - 0.5f) * an * 0.5f, j1y = h + (Rf() - 0.5f) * bn * 0.4f;
        Blob(j1x, j1y, Math.Max(2f, bn * (0.15f + Rf() * 0.05f)), 0.4f, 0.85f);
        if (slot is NeuR2 or NeuE2 or Flat)
        {
            float th = Rf() * TAU;
            Blob(Math.Clamp(j1x + MathF.Cos(th) * an * 0.5f, nx0 - an * 0.6f, nx0 + an * 0.6f), Math.Clamp(j1y + MathF.Sin(th) * bn * 0.5f, h - bn * 0.6f, h + bn * 0.6f), Math.Max(1.5f, bn * 0.1f), 0.44f, 0.75f);
        }
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float g = Clamp(v[y * S + x], 0, 1); img.SetPixel(ox + x, oy + y, new Color(g, g, g * 0.99f, al[y * S + x])); }
    }

    /// <summary>
    /// Komórka w mitozie (metafaza): zaokrąglona, jaśniejsza cytoplazma bez otoczki jądrowej, ciemna płytka metafazowa
    /// z wystającymi ramionami chromosomów (płytka wzdłuż osi y pola – obrót sprite'u ustawia płaszczyznę podziału).
    /// </summary>
    static void PaintMitosis(Image img, int slot)
    {
        int ox = slot % Cols * Cell + Gut, oy = slot / Cols * Cell + Gut, S = Cell - 2 * Gut;
        ulong rng = 0x5851F42D4C957F2DUL;
        float Rf() { rng ^= rng << 13; rng ^= rng >> 7; rng ^= rng << 17; return (rng >> 40) * (1f / 16777216f); }
        float h = S * 0.5f, a = S * 0.47f;
        var v = new float[S * S]; var al = new float[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = MathF.Sqrt(Sq(x + 0.5f - h) + Sq(y + 0.5f - h)) / a;
                al[y * S + x] = Clamp((1 - d) * a / 1.8f, 0, 1);
                v[y * S + x] = 0.9f - 0.14f * Smooth(Clamp((d - 0.9f) / 0.1f, 0, 1));
            }
        void Blob(float cx, float cy, float rx, float ry, float val, float str)
        {
            int x0 = Math.Max(0, (int)(cx - rx - 2)), x1 = Math.Min(S - 1, (int)(cx + rx + 2)), y0 = Math.Max(0, (int)(cy - ry - 2)), y1 = Math.Min(S - 1, (int)(cy + ry + 2));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dd = MathF.Sqrt(Sq((x + 0.5f - cx) / rx) + Sq((y + 0.5f - cy) / ry)), k = Clamp((1 - dd) * Math.Min(rx, ry) / 1.2f + 0.3f, 0, 1) * str;
                    if (k > 0) v[y * S + x] = Lerp(v[y * S + x], val, k);
                }
        }
        // chromosomy: płytka ~0,15 średnicy grubości, ~0,65 średnicy długości, z ramionami
        for (int i = 0; i < 70; i++)
        {
            float yy = h + (Rf() - 0.5f) * a * 1.25f, xx = h + (Rf() - 0.5f) * a * 0.18f;
            Blob(xx, yy, a * (0.03f + Rf() * 0.05f), a * (0.02f + Rf() * 0.025f), 0.55f, 0.55f);
        }
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float g = Clamp(v[y * S + x], 0, 1); img.SetPixel(ox + x, oy + y, new Color(g, g, g * 0.99f, al[y * S + x])); }
    }

    /// <summary>
    /// Komórka apoptotyczna: obkurczona, ze zbitą (piknotyczną), silnie załamującą światło chromatyną rozpadającą się na kulki;
    /// w kontraście fazowym ciemne grudki z jasnym pierścieniem. Pole – koło o promieniu pierwotnej komórki.
    /// </summary>
    static void PaintPykno(Image img, int slot)
    {
        int ox = slot % Cols * Cell + Gut, oy = slot / Cols * Cell + Gut, S = Cell - 2 * Gut;
        float h = S * 0.5f, a = S * 0.47f;
        var v = new float[S * S]; var al = new float[S * S];
        (float x, float y, float r)[] bl = { (0, 0, 0.36f), (0.34f, -0.22f, 0.17f), (-0.3f, 0.3f, 0.14f), (0.12f, 0.4f, 0.1f) };
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float fx = (x + 0.5f - h) / a, fy = (y + 0.5f - h) / a, cov = 0, val = 0.8f, ring = 0;
                // obkurczona cytoplazma (~0,65 promienia pierwotnej komórki)
                float dc = MathF.Sqrt(fx * fx + fy * fy) / 0.66f;
                cov = Math.Max(cov, Clamp((1 - dc) * a * 0.66f / 1.8f, 0, 1) * 0.8f);
                foreach (var (bx, by, br) in bl)
                {
                    float d = MathF.Sqrt(Sq(fx - bx) + Sq(fy - by)) / br;
                    if (d < 1) { val = Lerp(val, 0.3f, Clamp((1 - d) * br * a / 1.4f, 0, 1)); cov = 1; }
                    ring = Math.Max(ring, MathF.Exp(-Sq((d - 1.15f) / 0.12f)));
                }
                v[y * S + x] = Math.Min(1, val + ring * 0.2f); al[y * S + x] = Math.Max(cov, ring * 0.6f);
            }
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float g = Clamp(v[y * S + x], 0, 1); img.SetPixel(ox + x, oy + y, new Color(g, g * 0.98f, g * 1.01f, al[y * S + x])); }
    }

    /// <summary>
    /// Erytrocyt (dwuwklęsły krążek ~7,8 µm) w kontraście fazowym: jaśniejsze, cieńsze centrum, ciemniejszy wał brzeżny,
    /// ciemna linia błony i jasne halo – te same przystanki barw co w <see cref="RBC.Draw"/>.
    /// </summary>
    static void PaintRbc(Image img, int slot)
    {
        const float r0 = 3.9f, halo = 2.2f;
        float pad = (halo * 0.5f + 0.25f) / r0;
        Pad[slot] = pad;
        float[] ts = { 0f, 0.42f, 0.72f, 0.9f, 1f };
        Color[] cs = { Rgb(208, 196, 176, 0.30f), Rgb(178, 156, 140, 0.42f), Rgb(142, 108, 98, 0.62f), Rgb(160, 126, 112, 0.55f), Rgb(196, 168, 150, 0.35f) };
        var haloC = Rgb(255, 255, 240, 0.26f); var edge = Rgb(78, 56, 50, 0.55f);
        int ox = slot % Cols * Cell + Gut, oy = slot / Cols * Cell + Gut, S = Cell - 2 * Gut;
        float half = S * 0.5f, um = half / (r0 * (1 + pad)); // pikseli na µm
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f - half) / um, dy = (y + 0.5f - half) / um, d = MathF.Sqrt(dx * dx + dy * dy);
                var c = new Color(0, 0, 0, 0);
                // halo (miękki obrys: pełna alfa na brzegu, zero w odległości halo/2)
                float hk = 1 - MathF.Abs(d - r0) / (halo * 0.5f);
                if (hk > 0) c = Over(c, new Color(haloC.R, haloC.G, haloC.B, haloC.A * hk));
                // wnętrze z gradientem radialnym, brzeg wygładzony na ~1 px
                float cov = Clamp((r0 - d) * um + 0.5f, 0, 1);
                if (cov > 0) { var f = Ramp(d / r0, ts, cs); f.A *= cov; c = Over(c, f); }
                // błona: cienka ciemna linia (0,12 µm, ale co najmniej ~1 px tekstury)
                float ew = Math.Max(0.12f, 1.2f / um), ek = Clamp(1 - (MathF.Abs(d - r0) - ew * 0.5f) * um, 0, 1);
                if (ek > 0) c = Over(c, new Color(edge.R, edge.G, edge.B, edge.A * ek));
                img.SetPixel(ox + x, oy + y, c);
            }
    }
}

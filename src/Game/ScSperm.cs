using System.Linq;
namespace Mikro;

// =====================================================================
//  Plemnik – droga przez drogi rodne kobiety:
//  pochwa (sklepienie tylne) → kanał szyjki → jama macicy → połączenie maciczno-jajowodowe → cieśń → bańka (komórka jajowa)
// =====================================================================
public sealed class ScSperm : Scenario
{
    public enum Reg { Vagina, Cervix, Crypt, Uterus, Utj, Isthmus, Ampulla, Infund }
    static readonly string[] RegName = { "pochwa (sklepienie tylne)", "kanał szyjki macicy", "krypta szyjki", "jama macicy", "połączenie maciczno-jajowodowe", "cieśń jajowodu", "bańka jajowodu", "lejek jajowodu" };

    /// <summary>Kanał (odcinek światła dróg rodnych): oś, połowa szerokości w węzłach, odcinek anatomiczny każdego segmentu.</summary>
    sealed class Chan
    {
        public Vector2[] P; public float[] Hw; public Reg[] R; public float[] S; public int Side; public bool Tip;
        public Chan(int side, params (float x, float y, float hw, Reg r)[] pts)
        {
            Side = side; int n = pts.Length; P = new Vector2[n]; Hw = new float[n]; R = new Reg[Math.Max(1, n - 1)]; S = new float[n];
            for (int i = 0; i < n; i++) { P[i] = new Vector2(pts[i].x + OX, pts[i].y + OY); Hw[i] = pts[i].hw; if (i < n - 1) R[i] = pts[i].r; }
        }
    }
    const float OX = 5500, OY = 8000;
    readonly List<Chan> _ch = new();
    public Oocyte Egg;
    public Sperm Fertilized;
    /// <summary>Strona owulacji: −1 lewy jajnik, +1 prawy. Tylko w tym jajowodzie jest komórka jajowa.</summary>
    public int EggSide;
    /// <summary>Zapas energii plemnika i czasy kapacytacji oraz przejścia przez osłonkę (s gry; w naturze – godziny).</summary>
    public float Life = 720, CapT = 60, ZonaT = 9;
    float _t, _popT;
    /// <summary>Upłynnienie skrzepu nasienia 0..1: nasienie krzepnie tuż po wytrysku (semenogeliny), a PSA rozkłada skrzep zwykle w ~15 min, najwyżej w godzinę (w grze ~50 s).</summary>
    public float Liq;
    /// <summary>Pałeczki kwasu mlekowego (Lactobacillus) w płynie pochwy: x, y, kąt; włókna skrzepu nasienia.</summary>
    Vector3[] _lac = Array.Empty<Vector3>();
    readonly List<Vector2[]> _gel = new();
    /// <summary>Testy: bez uzupełniania populacji plemników.</summary>
    public bool NoRefill;

    public ScSperm()
    {
        Id = "sperm"; Name = "Plemnik"; Latin = "Homo sapiens – gameta męska"; TypeName = "Komórka rozrodcza (haploidalna)"; Size = "≈ 55 µm (główka 4,5 µm)"; Color = Hex("#cfd8e8");
        ReproFact = "Plemnik niesie haploidalny zestaw genów ojca; „rozmnaża się” tylko wtedy, gdy zapłodni komórkę jajową – powstaje diploidalna zygota. Z ~200 mln plemników w ejakulacie do bańki jajowodu dociera zwykle najwyżej kilkaset (Suarez i Pacey 2006), a zapładnia jeden – po nim reakcja korowa blokuje pozostałe.";
        Desc = "Cała droga: z kwaśnej pochwy przez śluz szyjki i jamę macicy do jednego z dwóch jajowodów – komórka jajowa jest tylko po stronie owulacji. Kapacytacja, hiperaktywacja, osłonka przejrzysta – przed tysiącami rywali.";
        Wd = 11000; Ht = 8400; ViewW = 320; TimeScale = 1; Bio = 1; Style = ScopeStyle.Tissue; Modes = LightModes; GridCell = 40; Water = true;
        Look = BgLook.Tract;
        Mag = "Obiektyw 40× (400×) – czas rzeczywisty; odległości skrócone (patrz: wiedza)"; ScoreLabel = "zapłodnień"; LineUnit = "plemnik"; BirthWord = "zygot";
        HasCustomFlow = true;
        DebrisN = 900; DebrisS0 = 0.4f; DebrisS1 = 1.6f; DebrisD = 0.08f; DebrisCols = new[] { Rgb(60, 58, 50, 0.4f), Rgb(255, 255, 240, 0.4f) };
        Controls = "WASD / przytrzymaj LPM – kierunek (plemnik nie może się zatrzymać) · Spacja (przytrzymaj) – hiperaktywacja po kapacytacji · stój przy nabłonku cieśni – rezerwuar (szybsza kapacytacja)";
        LoseText = "Twój plemnik nie zapłodnił komórki jajowej";
        Goals = new[]
        {
            new Goal("Dopłyń z pochwy do śluzu szyjki, zanim wróci kwaśne pH", 1, w => w.Stat("cervix")),
            new Goal("Przepłyń przez kanał szyjki do jamy macicy", 1, w => w.Stat("uterus")),
            new Goal("Wpłyń do jajowodu (połączenie maciczno-jajowodowe)", 1, w => w.Stat("tube")),
            new Goal("Kapacytacja (najszybciej przy nabłonku cieśni)", 1, w => w.Player is Sperm s ? s.Cap : 1),
            new Goal("Reakcja akrosomalna na osłonce przejrzystej", 1, w => w.Stat("zona")),
            new Goal("Zapłodnij komórkę jajową", 1, w => w.Stat("fert")),
        };
        Facts = new[]
        {
            "Skala: w naturze droga od sklepienia pochwy do bańki jajowodu ma ok. 15–20 cm, a plemnik płynie ok. 0,05 mm/s. W grze długości są skrócone ok. 12×, a szerokości szerokich odcinków (pochwa, macica, bańka) – kilkadziesiąt razy; wszystko w mikroskali (plemnik, komórki, prędkości, bicie wici, siły) ma prawdziwe wartości.",
            "Plemnik człowieka: główka spłaszczona ≈ 4–5 × 2,5–3,5 µm z akrosomem (40–70% powierzchni), wstawka z mitochondriami ≈ 4 µm, witka ≈ 45 µm – razem ≈ 55 µm (podręcznik WHO 2021). Wić bije ≈ 15–25 Hz.",
            "Plemnik nie płynie po prostej. Główka kołysze się na boki z każdym uderzeniem wici (amplituda ALH ≈ 2–7 µm), więc droga rzeczywista (VCL ≈ 60–100 µm/s) jest ok. 2× dłuższa niż postęp w linii prostej (VSL ≈ 30–50 µm/s); liniowość LIN = VSL/VCL ≈ 0,5 (Mortimer 1997). Przy powierzchniach chiralne bicie wici zakrzywia tor w łuki i okręgi (Woolley 2003; Elgeti i in. 2010). W HUD widać pomiar jak w analizatorze CASA.",
            "Pochwa: nasienie trafia do sklepienia tylnego przy ujściu zewnętrznym szyjki. Pochwa jest kwaśna (pH ≈ 4–4,5), bo pałeczki kwasu mlekowego przerabiają glikogen złuszczonych komórek nabłonka na kwas mlekowy. Nasienie zobojętnia ją w ~8 s – pH skacze z ~4,3 do ~7,2 (Fox i in. 1973) – i utrzymuje to przez kilka godzin; potem pochwa znów się zakwasza, ok. 0,5 pH na godzinę (Boskey i in. 1999). Plemniki, które do tego czasu nie weszły do śluzu szyjki, giną albo wypływają – tu kończy większość z nich (Suarez i Pacey 2006). W grze powrót kwaśnego pH trwa kilka minut.",
            "Liczby: w ejakulacie są dziesiątki–setki milionów plemników (dolna norma WHO: 39 mln). Do śluzu szyjki wchodzi mniejszość, do jajowodów dociera kilka tysięcy, a w pobliże komórki jajowej – kilkaset (Suarez i Pacey 2006). Dlatego w pochwie w polu widzenia 400× widać ich dziesiątki, w śluzie szyjki zwykle ~10–50 ruchliwych (tak ocenia się test po stosunku), a w macicy i jajowodzie – pojedyncze. W grze symulowane są plemniki w Twoim otoczeniu, a ich gęstość odpowiada odcinkowi dróg rodnych (wiersz „Plemniki w polu widzenia”).",
            "Nie wszystkie plemniki są sprawne. Wg norm WHO 2021 wystarczy, by ruchliwych było ≥ 42% (z ruchem postępowym ≥ 30%), a prawidłowej budowy ≥ 4% (ścisłe kryteria). W nasieniu widać plemniki nieruchome, krążące w miejscu, z wydłużoną albo bezkształtną główką, dwiema główkami, zagiętą szyjką, kroplą cytoplazmy na wstawce, zwiniętą lub podwójną witką. Śluz szyjki działa jak filtr: przepuszcza głównie ruchliwe plemniki o prawidłowej budowie (Suarez i Pacey 2006). W trybie podglądu widać cechy każdego plemnika.",
            "Tuż po wytrysku nasienie krzepnie: białka pęcherzyków nasiennych (semenogeliny) tworzą galaretowatą sieć, a enzym prostaty PSA rozkłada ją zwykle w ciągu kilkunastu minut (wg WHO – najpóźniej po godzinie). Do tego czasu plemniki poruszają się z trudem. W grze skrzep upłynnia się w ~50 s.",
            "W płynie pochwy leżą duże, płaskie, złuszczone komórki nabłonka (40–60 µm, z małym zbitym jądrem) i mnóstwo pałeczek kwasu mlekowego (Lactobacillus crispatus, L. iners i in.) – to „dobra flora” pochwy, która utrzymuje kwaśne pH i chroni przed zakażeniami.",
            "Szyjka macicy: w okresie owulacji śluz jest wodnisty i alkaliczny, a nici mucyny układają się w kanaliki, wzdłuż których plemniki płyną prościej; w lepkosprężystym śluzie tor jest bardziej liniowy, a amplituda bicia mniejsza (Smith i in. 2009). Część plemników zostaje w kryptach szyjki – rezerwuarze, z którego uwalniają się godzinami.",
            "Macica: skurcze perystaltyczne mięśniówki w fazie okołoowulacyjnej biegną od szyjki ku dnu macicy i przenoszą plemniki w ciągu minut – głównie do jajowodu po stronie dominującego pęcherzyka (Kunz i in. 1996). W jamie macicy czekają też liczne neutrofile.",
            "Połączenie maciczno-jajowodowe to wąskie, kręte przejście – silny filtr; przechodzą tylko plemniki o prawidłowej budowie i ruchu (Suarez i Pacey 2006).",
            "Rezerwuar w cieśni: plemniki wiążą się z nabłonkiem, żyją dłużej i powoli przechodzą kapacytację; uwalniają się, gdy hiperaktywacja zmienia ich bicie (Suarez, Int J Dev Biol 2008).",
            "Reotaksja: w przepływie przy powierzchni plemnik obraca się i płynie pod prąd (Miki i Clapham 2013; Kantsler i in. 2014), a przy ścianach podąża wzdłuż krawędzi (Denissenko i in. 2012). Płyn w jajowodzie płynie ku macicy – plemniki kierują się w przeciwną stronę, ku komórce jajowej.",
            "Kapacytacja trwa godziny: albumina usuwa cholesterol z błony, napływają HCO₃⁻ i Ca²⁺ (kanał CatSper), rośnie aktywność PKA. Skutek – hiperaktywacja: asymetryczne bicie o dużej amplitudzie, tor w pętle i duża siła (Ho i Suarez 2001).",
            "Termotaksja: bańka jajowodu jest w okresie owulacji cieplejsza od cieśni o ok. 1–2 °C (Bahat i in., Nat Med 2003). Progesteron z komórek wzgórka otwiera kanał CatSper (Lishko i in.; Strünker i in., Nature 2011) – przy komórce jajowej działa chemotaksja.",
            "Owulacja zachodzi zwykle z jednego jajnika. Komórka jajowa trafia przez lejek do bańki swojego jajowodu – plemniki, które popłynęły do drugiego jajowodu, nie mają szans.",
            "Wzgórek jajonośny to komórki w macierzy kwasu hialuronowego (hialuronidaza PH-20). Osłonka przejrzysta (≈ 13–15 µm) wiąże plemnik przez ZP2; po reakcji akrosomalnej IZUMO1 plemnika łączy się z JUNO na oolemmie (Bianchi i in., Nature 2014). U myszy większość zapładniających plemników zaczyna reakcję akrosomalną już we wzgórku (Jin i in. 2011); w grze – przy osłonce.",
            "Blokada polispermii: po wniknięciu jednego plemnika owastacyna z ziaren korowych tnie ZP2 – osłonka twardnieje (Burkart i in., J Cell Biol 2012).",
            "Neutrofile napływają do dróg rodnych po stosunku i fagocytują plemniki, a sieci NET je unieruchamiają; DNaza nasienia częściowo je rozcina (Alghamdi i Foster 2005).",
            "W drogach rodnych plemnik może żyć do ok. 5 dni – stąd „okno płodne” obejmuje dni przed owulacją (Wilcox i in., NEJM 1995). W grze czasy są mocno skrócone, a ruch – w czasie rzeczywistym.",
        };
        BuildTract();
    }

    // ------------------------------------------------ anatomia (µm; oś y do góry ujemna, ujście pochwy na dole)
    void BuildTract()
    {
        _ch.Clear();
        _ch.Add(new Chan(0, (0, -500, 520, Reg.Vagina), (0, -1500, 620, Reg.Vagina), (0, -2500, 480, Reg.Cervix), (0, -2650, 150, Reg.Cervix),
            (0, -3300, 190, Reg.Cervix), (0, -4000, 110, Reg.Uterus), (0, -5100, 650, Reg.Uterus), (0, -6200, 950, Reg.Uterus), (0, -6500, 900, Reg.Uterus)));
        // krypty szyjki (ślepe zatoki śluzowe – rezerwuar plemników)
        foreach (int sd in new[] { -1, 1 })
            foreach (float y in new[] { -2900f, -3250f, -3600f })
                _ch.Add(new Chan(0, (0, y, 70, Reg.Crypt), (sd * 360, y - 150 + sd * 20, 45, Reg.Crypt)) { Tip = true });
        foreach (int sd in new[] { -1, 1 })
        {
            // rogi jamy macicy ku ujściom jajowodów
            _ch.Add(new Chan(sd, (0, -6000, 900, Reg.Uterus), (sd * 900, -6600, 480, Reg.Uterus), (sd * 1450, -6850, 140, Reg.Utj)));
            // jajowód: część śródścienna (połączenie) → cieśń → bańka → lejek przy jajniku
            _ch.Add(new Chan(sd, (sd * 1450, -6850, 45, Reg.Utj), (sd * 1900, -6980, 60, Reg.Isthmus), (sd * 2550, -7120, 110, Reg.Isthmus),
                (sd * 3250, -7200, 140, Reg.Ampulla), (sd * 3900, -7050, 260, Reg.Ampulla), (sd * 4400, -6700, 380, Reg.Ampulla),
                (sd * 4700, -6100, 420, Reg.Ampulla), (sd * 4750, -5450, 460, Reg.Infund), (sd * 4450, -4950, 560, Reg.Infund)) { Tip = true });
        }
        // droga wzdłuż trasy (do „kto jest bliżej celu”)
        float s0 = 0;
        var c0 = _ch[0];
        for (int i = 0; i < c0.P.Length; i++) { if (i > 0) s0 += (c0.P[i] - c0.P[i - 1]).Length(); c0.S[i] = s0; }
        foreach (var c in _ch.Skip(1))
        {
            Nearest(c.P[0].X, c.P[0].Y, out var host, out _, out _, out float hs, out _, out _, exclude: c);
            float s = hs;
            for (int i = 0; i < c.P.Length; i++) { if (i > 0) s += (c.P[i] - c.P[i - 1]).Length(); c.S[i] = s; }
        }
        BuildCache();
    }
    /// <summary>Fałdy błony śluzowej: fałdy pochwy, fałdy palmowate szyjki, niskie w cieśni, wysokie i gęste w bańce.</summary>
    static float Fold(Reg r, float s, float side)
    {
        float amp = r switch { Reg.Vagina => 45, Reg.Cervix => 22, Reg.Uterus => 15, Reg.Isthmus => 9, Reg.Ampulla => 55, Reg.Infund => 60, _ => 4 };
        float k = side * 7.3f;
        return amp * Math.Max(0, 0.55f * MathF.Sin(s * 0.011f + k) + 0.35f * MathF.Sin(s * 0.027f + k * 2) + 0.25f * MathF.Sin(s * 0.061f + k * 3));
    }
    // wszystkie segmenty kanałów + siatka pamięci podręcznej: w każdej komórce 20 µm – najbliższy segment
    readonly List<(Chan c, int i)> _segs = new();
    const float CC = 20; int _cnx, _cny; short[] _cseg;
    void BuildCache()
    {
        _segs.Clear();
        foreach (var c in _ch) for (int i = 0; i < c.P.Length - 1; i++) _segs.Add((c, i));
        _cnx = (int)(Wd / CC) + 1; _cny = (int)(Ht / CC) + 1; _cseg = new short[_cnx * _cny];
        for (int j = 0; j < _cny; j++)
            for (int i = 0; i < _cnx; i++)
            {
                float x = (i + 0.5f) * CC, y = (j + 0.5f) * CC, best = 1e9f; int bi = 0;
                for (int k = 0; k < _segs.Count; k++) { float sd = SegSdf(k, x, y, out _, out _, out _); if (sd < best) { best = sd; bi = k; } }
                _cseg[j * _cnx + i] = (short)bi;
            }
    }
    float SegSdf(int k, float x, float y, out float t, out float ss, out Vector2 q)
    {
        var (c, i) = _segs[k];
        var a = c.P[i]; var ab = c.P[i + 1] - a; float l2 = ab.LengthSquared();
        var p = new Vector2(x, y);
        t = l2 < 1e-6f ? 0 : Clamp((p - a).Dot(ab) / l2, 0, 1); q = a + ab * t; var d = p - q;
        float hw = Lerp(c.Hw[i], c.Hw[i + 1], t); ss = Lerp(c.S[i], c.S[i + 1], t);
        float side = (ab.X * d.Y - ab.Y * d.X) >= 0 ? 1 : -1;
        return d.Length() - hw + Fold(c.R[i], ss, side);
    }
    readonly int[] _cand = new int[9];
    /// <summary>Najbliższy kanał (najgłębiej „w środku”): funkcja odległości ze znakiem (sdf &lt; 0 w świetle).</summary>
    void Nearest(float x, float y, out Chan best, out int seg, out float u, out float s, out float sdf, out Vector2 cp, Chan exclude = null)
    {
        best = null; seg = 0; u = 0; s = 0; sdf = 1e9f; cp = default;
        if (_cseg == null || exclude != null)
        {
            // pełne przeszukanie (budowa trasy i pamięci podręcznej)
            var p = new Vector2(x, y);
            foreach (var c in _ch)
            {
                if (c == exclude) continue;
                for (int i = 0; i < c.P.Length - 1; i++)
                {
                    var a = c.P[i]; var ab = c.P[i + 1] - a; float l2 = ab.LengthSquared(); if (l2 < 1e-6f) continue;
                    float t = Clamp((p - a).Dot(ab) / l2, 0, 1); var q = a + ab * t; var d = p - q;
                    float hw = Lerp(c.Hw[i], c.Hw[i + 1], t), ss = Lerp(c.S[i], c.S[i + 1], t);
                    float side = (ab.X * d.Y - ab.Y * d.X) >= 0 ? 1 : -1;
                    float sd = d.Length() - hw + Fold(c.R[i], ss, side);
                    if (sd < sdf) { sdf = sd; best = c; seg = i; u = t; s = ss; cp = q; }
                }
            }
            return;
        }
        int ci = Math.Clamp((int)(x / CC), 0, _cnx - 1), cj = Math.Clamp((int)(y / CC), 0, _cny - 1), n = 0;
        for (int dj = -1; dj <= 1; dj++)
            for (int di = -1; di <= 1; di++)
            {
                int ii = ci + di, jj = cj + dj; if (ii < 0 || jj < 0 || ii >= _cnx || jj >= _cny) continue;
                int k = _cseg[jj * _cnx + ii]; bool dup = false;
                for (int m = 0; m < n; m++) if (_cand[m] == k) { dup = true; break; }
                if (!dup) _cand[n++] = k;
            }
        for (int m = 0; m < n; m++)
        {
            float sd = SegSdf(_cand[m], x, y, out float t, out float ss, out var q);
            if (sd < sdf) { sdf = sd; best = _segs[_cand[m]].c; seg = _segs[_cand[m]].i; u = t; s = ss; cp = q; }
        }
    }
    public float Sdf(float x, float y) { Nearest(x, y, out _, out _, out _, out _, out float sd, out _); return sd; }
    /// <summary>
    /// Materia poza ostrością wzdłuż drogi: w pochwie złuszczone komórki nabłonka i plemniki w innych płaszczyznach puli
    /// nasienia, w szyjce – pasma śluzu, w jajowodzie – pojedyncze komórki, przy wzgórku – komórki wzgórka.
    /// </summary>
    static readonly BgZone[] RegBg =
    {
        new(0.3f, 0.05f, 0.1f, 0.65f, 0.12f), new(0.05f, 0.1f, 0.8f, 0.3f, 0.1f), new(0.05f, 0.1f, 0.8f, 0.3f, 0.1f), new(0.02f, 0.05f, 0.1f, 0.1f, 0.05f),
        new(0.02f, 0.05f, 0.4f, 0.15f, 0.05f), new(0.02f, 0.1f, 0.15f, 0.2f, 0.05f), new(0.02f, 0.25f, 0.1f, 0.1f, 0.05f), new(0.02f, 0.3f, 0.1f, 0.1f, 0.05f),
    };
    public override BgZone Backdrop(World w, float x, float y) => RegBg[(int)RegionAt(x, y)];
    public Reg RegionAt(float x, float y) { Nearest(x, y, out var c, out int i, out _, out _, out _, out _); return c?.R[i] ?? Reg.Vagina; }
    public string RegionName(float x, float y) => RegName[(int)RegionAt(x, y)];
    /// <summary>Postęp wzdłuż trasy (µm) – kto jest bliżej jajowodu i komórki jajowej.</summary>
    public float RouteS(float x, float y) { Nearest(x, y, out _, out _, out _, out float s, out _, out _); return s; }
    /// <summary>Strona ciała (−1 lewa, +1 prawa, 0 linia środkowa).</summary>
    public int SideAt(float x, float y) { Nearest(x, y, out var c, out _, out _, out _, out _, out _); return c?.Side ?? 0; }
    /// <summary>Odległość od ściany i normalna skierowana do światła.</summary>
    public void Wall(float x, float y, out float d, out float nx, out float ny)
    {
        Nearest(x, y, out _, out _, out _, out _, out float sd, out var cp);
        d = -sd; float dx = cp.X - x, dy = cp.Y - y, l = MathF.Sqrt(dx * dx + dy * dy);
        if (l < 1e-4f) { nx = ny = 0; return; }
        nx = dx / l; ny = dy / l;
    }
    /// <summary>Kierunek osi kanału (w stronę trasy plemnika: od pochwy ku jajowodom).</summary>
    public bool Axis(float x, float y, out float tx, out float ty, out Reg r, out float frac)
    {
        Nearest(x, y, out var c, out int i, out float u, out _, out float sd, out var cp);
        tx = ty = 0; r = Reg.Vagina; frac = 0; if (c == null) return false;
        var ab = c.P[i + 1] - c.P[i]; float l = ab.Length(); if (l < 1e-4f) return false;
        tx = ab.X / l; ty = ab.Y / l; r = c.R[i];
        float hw = Lerp(c.Hw[i], c.Hw[i + 1], u); frac = Clamp((x - cp.X) * (x - cp.X) + (y - cp.Y) * (y - cp.Y), 0, hw * hw) / Math.Max(1, hw * hw);
        return true;
    }
    public override string InspectAt(World w, float x, float y)
    {
        if (InCumulus(x, y)) return "cumulus";
        var r = RegionAt(x, y);
        if (r == Reg.Vagina && Sdf(x, y) < 0)
        {
            if (NearLacto(x, y, 1.8f)) return "lactobacillus";
            if (Liq < 0.85f && y > OY - 2500 && y < OY - 1500) return "semen-coagulum";
        }
        return r switch { Reg.Vagina => "vagina", Reg.Cervix or Reg.Crypt => "cervix", Reg.Uterus => "uterus", _ => "oviduct" };
    }
    public bool Isthmus(float x, float y) => RegionAt(x, y) is Reg.Isthmus or Reg.Utj;
    /// <summary>Śluz szyjki i połączenia maciczno-jajowodowego – lepkosprężysty, z kanalikami z nici mucyny.</summary>
    public float Mucus(float x, float y) => RegionAt(x, y) switch { Reg.Cervix => 1, Reg.Crypt => 1, Reg.Utj => 0.6f, _ => 0 };

    /// <summary>Skurcz perystaltyczny macicy: fala co ~25 s trwająca kilka sekund (0..1).</summary>
    float Peristalsis => MathF.Pow(Math.Max(0, MathF.Sin(_t * TAU / 25)), 6);
    public override bool CustomFlow(World w, float x, float y, out float fx, out float fy)
    {
        fx = fy = 0;
        Nearest(x, y, out var c, out int si, out float su, out _, out float sd, out var cp);
        if (c == null || sd > 0) return true;
        var ab = c.P[si + 1] - c.P[si]; float al = ab.Length(); if (al < 1e-4f) return true;
        float tx = ab.X / al, ty = ab.Y / al; var r = c.R[si];
        float hw = Lerp(c.Hw[si], c.Hw[si + 1], su), dd = (new Vector2(x, y) - cp).LengthSquared();
        float pois = Math.Max(0, 1 - dd / Math.Max(1, hw * hw));
        float u = r switch
        {
            // szyjka: śluz powoli spływa ku pochwie
            Reg.Cervix or Reg.Crypt => -4,
            // macica: fale perystaltyczne ku dnu – silniej do rogu po stronie owulacji
            Reg.Uterus => 110 * Peristalsis * (c.Side == 0 || c.Side == EggSide ? 1 : 0.25f),
            // jajowód: rzęski i skurcze pchają płyn ku macicy (strumień zachowany: wąsko – szybciej)
            Reg.Utj => -22, Reg.Isthmus => -30, Reg.Ampulla => -9, Reg.Infund => -5,
            _ => 0,
        };
        fx = tx * u * pois; fy = ty * u * pois;
        return true;
    }
    /// <summary>Temperatura: bańka cieplejsza od cieśni o ~1,5 °C (termotaksja).</summary>
    public float Temp(float x, float y) => RegionAt(x, y) switch { Reg.Vagina => 36.4f, Reg.Cervix or Reg.Crypt => 36.7f, Reg.Uterus => 37.0f, Reg.Utj => 36.5f, Reg.Isthmus => 36.4f, Reg.Ampulla => 37.8f, _ => 37.6f };
    /// <summary>
    /// pH: nasienie podnosi pH pochwy z ~4,3 do ~7,2 w 8 s (Fox i in. 1973) i utrzymuje je godzinami; potem pałeczki kwasu
    /// mlekowego znów zakwaszają pochwę (~0,5 pH na godzinę; Boskey i in. 1999) – w grze ten powrót jest skrócony do minut.
    /// </summary>
    public float Ph(float x, float y) => RegionAt(x, y) switch { Reg.Vagina => Lerp(7.2f, 4.3f, Smooth(_t / 180)), Reg.Cervix or Reg.Crypt => 7.8f, Reg.Uterus => 7.2f, _ => 7.6f };
    public bool InCumulus(float x, float y) => Egg != null && Hypot(x - Egg.X, y - Egg.Y) < Egg.CumR;
    /// <summary>Lepkość: śluz szyjki i macierz hialuronowa wzgórka spowalniają (hiperaktywowane plemniki przechodzą łatwiej).</summary>
    public float Viscosity(float x, float y, float hyper)
    {
        if (InCumulus(x, y)) return Lerp(0.45f, 0.8f, hyper);
        var r = RegionAt(x, y);
        float v = 1 - 0.4f * (r switch { Reg.Cervix or Reg.Crypt => 1, Reg.Utj => 0.6f, _ => 0 });
        // skrzep nasienia (sieć semenogelin) krępuje ruch plemników, dopóki PSA go nie upłynni
        if (r == Reg.Vagina) v *= Lerp(0.55f, 1, Liq);
        return v;
    }
    /// <summary>Kierunek chemotaksji (progesteron) – tylko w pobliżu wzgórka.</summary>
    public bool ChemoDir(float x, float y, out float a)
    {
        a = 0; if (Egg == null) return false;
        float d = Hypot(Egg.X - x, Egg.Y - y); if (d > Egg.CumR + 260) return false;
        a = MathF.Atan2(Egg.Y - y, Egg.X - x); return true;
    }
    /// <summary>Środek n-tego segmentu danego odcinka (po stronie owulacji) i kierunek w górę dróg rodnych (zrzuty, testy).</summary>
    public (float x, float y, float a) Center(Reg r, int nth = 0)
    {
        int k = 0;
        foreach (var c in _ch)
            for (int i = 0; i < c.P.Length - 1; i++)
                if (c.R[i] == r && (c.Side == EggSide || c.Side == 0) && k++ == nth)
                {
                    var m = (c.P[i] + c.P[i + 1]) / 2; var t = c.P[i + 1] - c.P[i];
                    return (m.X, m.Y, MathF.Atan2(t.Y, t.X));
                }
        return (Wd / 2, Ht / 2, 0);
    }
    /// <summary>Punkt testowy: środek pierwszego segmentu danego odcinka po stronie owulacji, przesunięty ku ścianie.</summary>
    public (float x, float y, float downA) TestSpot(Reg r, float fromWall)
    {
        foreach (var c in _ch)
            for (int i = 0; i < c.P.Length - 1; i++)
                if (c.R[i] == r && (c.Side == EggSide || c.Side == 0))
                {
                    var a = c.P[i]; var b = c.P[i + 1]; var m = (a + b) / 2; var t = (b - a).Normalized(); var n = new Vector2(-t.Y, t.X);
                    float hw = (c.Hw[i] + c.Hw[i + 1]) / 2 - fromWall;
                    for (int k = 0; k < 40 && Sdf(m.X + n.X * hw, m.Y + n.Y * hw) > -fromWall * 0.5f; k++) hw -= 2;
                    return (m.X + n.X * hw, m.Y + n.Y * hw, MathF.Atan2(-t.Y, -t.X));
                }
        return (Wd / 2, Ht / 2, 0);
    }

    public override void Init(World w)
    {
        Fertilized = null; _t = 0; EggSide = Chance(0.5f) ? -1 : 1;
        // komórka jajowa w bańce jajowodu po stronie owulacji
        var ep = new Vector2(EggSide * 4520 + OX, -6450 + OY);
        Egg = w.Add(new Oocyte(w, ep.X, ep.Y));
        // nasienie w sklepieniu tylnym pochwy, przy ujściu zewnętrznym szyjki
        Liq = 0; _popT = 0; NoRefill = false;
        var me = w.Add(new Sperm(w, OX + Rand(-80, 80), OY - 2150, -PI / 2 + Rand(-0.4f, 0.4f), true));
        // pula nasienia: w ejakulacie są dziesiątki–setki milionów plemników, więc w kadrze 400× widać ich dziesiątki
        // (najgęściej przy ujściu szyjki); ok. ⅓ nie rusza się, a wiele ma wady budowy – jak w badaniu nasienia
        for (int k = 0, n = 0; k < 5000 && n < 620; k++)
        {
            float y = OY - 2440 + MathF.Abs(RandN()) * 330, x = OX + RandN() * 260;
            if (y > OY - 1450 || Sdf(x, y) > -6) continue;
            var sp = w.Add(new Sperm(w, x, y, Rand(0, TAU))); sp.FromSemen(); sp.Energy = Rand(0.75f, 1); n++;
        }
        // złuszczone komórki nabłonka pochwy, pałeczki kwasu mlekowego i skrzep nasienia
        for (int k = 0, n = 0; k < 400 && n < 26; k++)
        {
            float x = OX + Rand(-420, 420), y = OY - Rand(900, 2450);
            if (Sdf(x, y) > -28) continue;
            w.Add(new Squamous(w, x, y)); n++;
        }
        var lac = new List<Vector3>();
        for (int k = 0; k < 9000 && lac.Count < 2600; k++)
        {
            float x = OX + Rand(-560, 560), y = OY - Rand(600, 2490);
            if (Sdf(x, y) > -2 || RegionAt(x, y) != Reg.Vagina) continue;
            lac.Add(new Vector3(x, y, Rand(0, TAU)));
        }
        _lac = lac.ToArray();
        _gel.Clear();
        for (int k = 0; k < 600 && _gel.Count < 160; k++)
        {
            float x = OX + RandN() * 280, y = OY - 2430 + MathF.Abs(RandN()) * 380;
            if (Sdf(x, y) > -8) continue;
            int m = RandI(6, 11); var g = new Vector2[m]; float a = Rand(0, TAU);
            for (int j = 0; j < m; j++) { g[j] = new Vector2(x, y); a += RandN() * 0.5f; float st = Rand(4, 8); x += MathF.Cos(a) * st; y += MathF.Sin(a) * st; }
            _gel.Add(g);
        }
        // leukocyty: po stosunku napływają do pochwy, szyjki i macicy (w jajowodzie – nieliczne)
        foreach (var (n, y0, y1, xs) in new[] { (4, -2400f, -1200f, 450f), (5, -3900f, -2700f, 120f), (14, -6300f, -4200f, 700f), (3, -7200f, -6300f, 4500f) })
            for (int i = 0; i < n; i++)
                for (int k = 0; k < 30; k++)
                {
                    float x = OX + Rand(-xs, xs), y = OY + Rand(y0, y1);
                    if (Sdf(x, y) < -12) { w.Add(new Leuko(w, x, y)); break; }
                }
        w.SetPlayer(me);
        w.CamX = me.X; w.CamY = me.Y;
        w.Log("Jesteś plemnikiem w sklepieniu tylnym pochwy, wśród setek innych. Pochwa jest kwaśna (pH ≈ 4); nasienie zobojętniło ją w kilka sekund, ale z czasem kwas wraca. Płyń w górę, do ujścia szyjki macicy (śluz).", "info");
    }
    public override void Constrain(World w, Ent e)
    {
        if (e.Fixed) return;
        float m = e is Sperm ? 1.5f : e.Radius * 0.9f;
        Nearest(e.X, e.Y, out _, out _, out _, out _, out float sd, out var cp);
        if (sd > -m)
        {
            float dx = cp.X - e.X, dy = cp.Y - e.Y, l = MathF.Sqrt(dx * dx + dy * dy);
            if (l > 1e-4f) { float k = (sd + m) / l; e.X += dx * k; e.Y += dy * k; }
        }
    }
    public void Fertilize(World w, Sperm s)
    {
        if (Fertilized != null) return;
        Fertilized = s; Egg.FertT = 0;
        w.Sfx("goal", Egg.X, Egg.Y, 1);
        foreach (var e in w.Ents) if (e is Sperm o && o != s && o.State == St.Swim && InCumulus(o.X, o.Y)) o.Energy *= 0.5f;
        if (s.IsPlayer)
        {
            w.AddStat("fert", 1); w.BornUnseen(1, 1);
            w.Banner("Zapłodnienie!", "IZUMO1 łączy się z JUNO – błony zlewają się, rusza reakcja korowa");
            w.Log("Fuzja błon: jądro plemnika wchodzi do oocytu, który kończy II podział mejotyczny. Za kilka godzin powstaną przedjądrza, a po ~24–30 h pierwszy podział zygoty.", "good");
            w.OverTitle = "Zapłodnienie!";
            w.After(18, () => w.GameOver("Twoje geny (haploidalny zestaw ojca) są w zygocie – połączą się z genami matki. Reakcja korowa zablokowała pozostałe plemniki. Z tysięcy konkurentów zapłodnił jeden: Ty.", true));
        }
        else
        {
            w.Log("Inny plemnik z tego samego ejakulatu zapłodnił komórkę jajową. Reakcja korowa: osłonka twardnieje – polispermia zablokowana.", "bad");
            w.OverTitle = "Zapłodnił inny plemnik";
            w.After(12, () => w.GameOver("Zapłodnił Twój „brat” z tego samego ejakulatu. Geny ojca przejdą do następnego pokolenia – ale w innym haploidalnym zestawie niż Twój (każdy plemnik ma inną kombinację po mejozie)."));
        }
    }
    Reg _lastReg = Reg.Vagina;
    public override void Update(World w, float dt, float gdt)
    {
        _t += dt;
        Liq = Smooth(Clamp(_t / 50f, 0, 1));
        if (w.Player is not Sperm p) return;
        if (Liq > 0.97f && w.Stat("liq") == 0) { w.AddStat("liq", 1); if (RegionAt(p.X, p.Y) == Reg.Vagina) w.Log("Skrzep nasienia upłynnił się – enzym PSA pociął sieć semenogelin i plemniki pływają swobodnie.", "info"); }
        _popT -= gdt;
        if (_popT <= 0) { _popT = 0.5f; Population(w, p); }
        var r = RegionAt(p.X, p.Y);
        if (r != _lastReg)
        {
            _lastReg = r;
            switch (r)
            {
                case Reg.Cervix: if (w.Stat("cervix") == 0) { w.AddStat("cervix", 1); w.Log("Śluz szyjki: wodnisty i zasadowy w okresie owulacji. Nici mucyny tworzą kanaliki – plemnik płynie prościej, ale wolniej. Boczne krypty to rezerwuar plemników.", "info"); } break;
                case Reg.Uterus: if (w.Stat("uterus") == 0) { w.AddStat("uterus", 1); w.Log("Jama macicy: fale perystaltyczne mięśniówki niosą plemniki ku dnu macicy – najsilniej do jajowodu po stronie owulacji. Uważaj na neutrofile.", "info"); } break;
                case Reg.Utj: case Reg.Isthmus: if (w.Stat("tube") == 0) { w.AddStat("tube", 1); w.Log(SideAt(p.X, p.Y) == EggSide ? "Jajowód. Płyn płynie ku macicy – płyń pod prąd, przy ścianie. W cieśni możesz związać się z nabłonkiem (rezerwuar) i przejść kapacytację." : "Jajowód… ale owulacja była z drugiego jajnika. W tym jajowodzie nie ma komórki jajowej – większość plemników kończy tak swoją drogę.", SideAt(p.X, p.Y) == EggSide ? "info" : "bad"); } break;
            }
        }
        if (InCumulus(p.X, p.Y) && w.Stat("cumulus") == 0) { w.AddStat("cumulus", 1); w.Log("Wzgórek jajonośny: komórki w galaretowatej macierzy kwasu hialuronowego. Hiperaktywacja i hialuronidaza PH-20 pomagają przejść.", "info"); }
    }

    // ------------------------------------------------ liczebność plemników w kolejnych odcinkach
    /// <summary>
    /// Plemniki na pole widzenia 400× (≈ 0,06 mm²) w odcinku, w którym jesteś. Z dziesiątek–setek milionów w pochwie do
    /// śluzu szyjki wchodzi mniejszość (test po stosunku: zwykle ~10–50 ruchliwych w polu widzenia), w macicy jest ich
    /// niewiele, do jajowodów dociera kilka tysięcy, a do komórki jajowej – kilkaset (Suarez i Pacey 2006). Symulujemy
    /// plemniki w pobliżu gracza; resztę populacji reprezentuje gęstość w kadrze.
    /// </summary>
    float PerField(Reg r, float x, float y) => r switch
    {
        Reg.Cervix or Reg.Crypt => 18, Reg.Uterus => 2.5f, Reg.Utj or Reg.Isthmus => 6,
        Reg.Ampulla or Reg.Infund => Egg != null && Hypot(x - Egg.X, y - Egg.Y) < Egg.CumR + 300 ? 5 : 1.2f,
        _ => 0, // pochwa: pula z wytrysku – nie przybywa, tylko ubywa
    };
    void Population(World w, Sperm p)
    {
        if (NoRefill) return;
        // okrąg wokół gracza (większy niż kadr): oczekiwana liczba plemników = Σ gęstość odcinka × pole światła
        const float Rad = 650, Min = 200; const int NS = 72;
        float exp = 0, cell = PI * Rad * Rad / NS / 57600f;
        for (int i = 0; i < NS; i++)
        {
            float a = i * 2.39996f, d = Rad * MathF.Sqrt((i + 0.5f) / NS), x = p.X + MathF.Cos(a) * d, y = p.Y + MathF.Sin(a) * d;
            if (Sdf(x, y) < 0) exp += PerField(RegionAt(x, y), x, y) * cell;
        }
        int target = (int)MathF.Round(exp);
        int have = 0; var far = new List<(Sperm s, float d)>();
        foreach (var e in w.Ents)
            if (e is Sperm s && s != p && s.Alive && s.Holder == null && s.State is St.Swim or St.Attach)
            {
                float d = Hypot(s.X - p.X, s.Y - p.Y);
                if (d > Rad || RegionAt(s.X, s.Y) == Reg.Vagina) continue;
                have++;
                if (d > Min) far.Add((s, d));
            }
        if (have < target)
        {
            // plemniki, które już wcześniej przeszły dalej (najszybsze docierają do jajowodów w minuty) i dopływ z rezerwuarów;
            // zwykle pojawiają się poza kadrem, a gdy dopiero wpływasz do nowego odcinka – także w kadrze (wpływają w ostrość z głębi)
            float min = have < target * 0.6f ? 45 : Min;
            for (int k = 0, tries = 0; k < 30 && have < target && tries < 400; tries++)
            {
                float a = Rand(0, TAU), d = MathF.Sqrt(Rand(min * min, Rad * Rad)), x = p.X + MathF.Cos(a) * d, y = p.Y + MathF.Sin(a) * d;
                if (Sdf(x, y) > -5) continue;
                var rr = RegionAt(x, y); float pf = PerField(rr, x, y);
                if (pf <= 0 || R01() * 18 > pf) continue;
                Axis(x, y, out float tx, out float ty, out _, out _);
                float up = MathF.Atan2(ty, tx);
                var ns = w.Add(new Sperm(w, x, y, (rr is Reg.Cervix or Reg.Crypt && Chance(0.2f) ? up + PI : up) + RandN() * 0.5f)); ns.Selected(); ns.FadeIn();
                // za śluzem szyjki plemniki zaczynają kapacytację – każdy w swoim tempie
                ns.Cap = rr is Reg.Cervix or Reg.Crypt ? Rand(0, 0.15f) : Rand(0.1f, 0.75f); ns.Energy = Rand(0.55f, 1);
                if (rr == Reg.Isthmus && ns.Cap < 1 && Chance(0.5f))
                {
                    // rezerwuar: główka związana z nabłonkiem cieśni
                    Wall(x, y, out float wd, out float nx, out float ny);
                    if (wd > 2.5f) { ns.X -= nx * (wd - 2.5f); ns.Y -= ny * (wd - 2.5f); }
                    ns.A = MathF.Atan2(-ny, -nx) + RandN() * 0.4f; ns.State = St.Attach; ns.AttachT = Rand(0, 5);
                }
                have++; k++;
            }
        }
        else if (have > target * 1.6f + 5)
        {
            // nadmiar poza kadrem rozprasza się (najdalsze)
            far.Sort((a, b) => b.d.CompareTo(a.d));
            for (int i = 0; i < Math.Min(5, far.Count) && have > target * 1.3f; i++) { far[i].s.Alive = false; have--; }
        }
    }

    // ------------------------------------------------ tło: pałeczki kwasu mlekowego, skrzep nasienia, pasma śluzu szyjki
    public override void DrawUnder(World w, Painter p, View v) { for (int i = 0; i < UnderParts; i++) DrawUnderPart(w, p, v, i); }
    /// <summary>Tło równolegle: pałeczki kwasu mlekowego w dwóch połowach, skrzep nasienia, pasma śluzu szyjki.</summary>
    public override int UnderParts => 4;
    public override void DrawUnderPart(World w, Painter p, View v, int part)
    {
        p.Identity();
        if (part < 2) { DrawLacto(w, p, v, part * _lac.Length / 2, (part + 1) * _lac.Length / 2); return; }
        if (part == 3) { DrawMucus(p, v); return; }
        float ga = 1 - Liq;
        if (ga > 0.02f)
            foreach (var g in _gel)
            {
                if (!v.Vis(g[0].X, g[0].Y, 70)) continue;
                var gs = new ReadOnlySpan<Vector2>(g);
                p.SoftStroke(gs, false, 2.6f, Rgb(255, 255, 245, 0.12f * ga));
                p.Stroke(gs, false, 0.45f, Rgb(205, 202, 186, 0.4f * ga));
            }
    }
    void DrawLacto(World w, Painter p, View v, int i0, int i1)
    {
        float t = w.T;
        var lc = Rgb(38, 42, 36, 0.62f); var lh = Rgb(255, 255, 240, 0.22f);
        for (int i = i0; i < i1; i++)
        {
            var q = _lac[i]; if (!v.Vis(q.X, q.Y, 4)) continue;
            // nieruchome pałeczki drgają w ruchach Browna
            float jx = 0.25f * Noise1(t * 1.7f + i * 0.37f), jy = 0.25f * Noise1(t * 1.9f + i * 0.61f + 50), a = q.Z + 0.25f * Noise1(t * 0.6f + i);
            float hl = 1.1f + (i % 5) * 0.35f, ca = MathF.Cos(a) * hl, sa = MathF.Sin(a) * hl, x = q.X + jx, y = q.Y + jy;
            p.Seg(x - ca * 1.2f, y - sa * 1.2f, x + ca * 1.2f, y + sa * 1.2f, 1.6f, lh);
            p.Line(x - ca, y - sa, x + ca, y + sa, 0.7f, lc, true);
        }
    }
    /// <summary>W okresie owulacji śluz szyjki jest wodnisty, a nici mucyn układają się w kanaliki wzdłuż kanału – plemniki płyną nimi.</summary>
    void DrawMucus(Painter p, View v)
    {
        var buf = p.Shape2; float lw = Math.Max(v.Px, 0.3f);
        foreach (var c in _ch)
            for (int i = 0; i < c.P.Length - 1; i++)
            {
                if (c.R[i] is not (Reg.Cervix or Reg.Crypt)) continue;
                var a = c.P[i]; var b = c.P[i + 1]; float len = (b - a).Length(), hwm = Math.Max(c.Hw[i], c.Hw[i + 1]);
                if (len < 1 || Math.Max(a.X, b.X) + hwm < v.X0 || Math.Min(a.X, b.X) - hwm > v.X1 || Math.Max(a.Y, b.Y) + hwm < v.Y0 || Math.Min(a.Y, b.Y) - hwm > v.Y1) continue;
                var tv = (b - a) / len; var nv = new Vector2(-tv.Y, tv.X);
                int lanes = (int)(hwm * 2 / 11);
                for (int l = 0; l <= lanes; l++)
                {
                    float off = -hwm + l * 11 + 3 * MathF.Sin(l * 1.7f); int m = 0;
                    for (float st = 0; st <= len; st += 6)
                    {
                        var q = a + tv * st + nv * (off + 1.6f * MathF.Sin(st * 0.08f + l));
                        bool ok = v.Vis(q.X, q.Y, 8) && Sdf(q.X, q.Y) < -3;
                        if (ok) buf[m++] = q;
                        if ((!ok || st + 6 > len) && m > 1) { p.Stroke(new ReadOnlySpan<Vector2>(buf, 0, m), false, lw, Rgb(228, 225, 208, 0.28f)); m = 0; }
                        else if (!ok) m = 0;
                    }
                }
            }
    }
    /// <summary>Pałeczka kwasu mlekowego w pobliżu punktu (tryb podglądu).</summary>
    bool NearLacto(float x, float y, float r)
    {
        foreach (var q in _lac) if (Sq(q.X - x) + Sq(q.Y - y) < r * r) return true;
        return false;
    }

    // ------------------------------------------------ rysowanie: tkanka poza światłem, nabłonek wzdłuż ścian
    static readonly Color TisV = Rgb(196, 150, 150, 0.62f), TisM = Rgb(185, 150, 140, 0.62f);
    // siatka tkanki zaczepiona w układzie świata: ściany kanałów są nieruchome, więc kolor węzła liczy się raz, a przy przesuwaniu
    // kadru – tylko dla węzłów, które weszły na ekran (pamięć toroidalna). Oczko skwantowane co 2^¼, żeby płynny zoom nie
    // unieważniał pamięci w każdej klatce; przy okazji faktura tkanki nie „pływa” już przy ruchu kamery.
    Color[] _gcCol = Array.Empty<Color>(); long[] _gcKey = Array.Empty<long>(); int _gcW, _gcH; float _gcCs;
    Color TissueCol(int ix, int iy, float cs)
    {
        int k = (((iy % _gcH) + _gcH) % _gcH) * _gcW + ((ix % _gcW) + _gcW) % _gcW;
        long key = ((long)ix << 32) | (uint)iy;
        if (_gcKey[k] == key) return _gcCol[k];
        float x = ix * cs, y = iy * cs;
        Nearest(x, y, out var ch, out int sg, out _, out _, out float sd, out _);
        float a = Clamp(sd / cs + 0.5f, 0, 1);
        var c = ch != null && ch.R[sg] == Reg.Vagina ? TisV : TisM;
        float n = 0.06f * Noise1(x * 0.03f + y * 0.017f);
        var col = new Color(c.R + n, c.G + n, c.B + n, c.A * a);
        _gcKey[k] = key; _gcCol[k] = col;
        return col;
    }
    public override void DrawOver(World w, Painter p, View v)
    {
        p.Identity();
        float vw = v.X1 - v.X0, cs = Math.Max(vw / 150, v.Px * 6);
        cs = MathF.Pow(2, MathF.Round(MathF.Log2(cs) * 4) / 4);
        int ix0 = (int)MathF.Floor(v.X0 / cs) - 1, iy0 = (int)MathF.Floor(v.Y0 / cs) - 1;
        int nx = (int)MathF.Ceiling(v.X1 / cs) + 2 - ix0, ny = (int)MathF.Ceiling(v.Y1 / cs) + 2 - iy0;
        if (nx > _gcW || ny > _gcH)
        {
            _gcW = Math.Max(_gcW, nx + 8); _gcH = Math.Max(_gcH, ny + 8);
            _gcKey = new long[_gcW * _gcH]; _gcCol = new Color[_gcW * _gcH]; _gcCs = 0;
        }
        if (cs != _gcCs) { _gcCs = cs; Array.Fill(_gcKey, long.MinValue); }
        p.Grid(ix0 * cs, iy0 * cs, cs, cs, nx, ny, (i, j) => TissueCol(ix0 + i, iy0 + j, cs));
        DrawEpithelium(w, p, v);
    }
    void DrawEpithelium(World w, Painter p, View v)
    {
        float lw = Math.Max(v.Px, 0.4f), step = Math.Max(6, v.Px * 5);
        foreach (var c in _ch)
            for (int i = 0; i < c.P.Length - 1; i++)
            {
                var a = c.P[i]; var b = c.P[i + 1]; float len = (b - a).Length();
                float hwm = Math.Max(c.Hw[i], c.Hw[i + 1]) + 80;
                if (Math.Max(a.X, b.X) + hwm < v.X0 || Math.Min(a.X, b.X) - hwm > v.X1 || Math.Max(a.Y, b.Y) + hwm < v.Y0 || Math.Min(a.Y, b.Y) - hwm > v.Y1) continue;
                var t = (b - a) / len; var n = new Vector2(-t.Y, t.X);
                bool squam = c.R[i] is Reg.Vagina; // pochwa: nabłonek wielowarstwowy płaski, bez rzęsek
                bool cilia = c.R[i] is Reg.Utj or Reg.Isthmus or Reg.Ampulla or Reg.Infund or Reg.Uterus;
                for (int side = -1; side <= 1; side += 2)
                    for (float s = 0; s < len; s += step)
                    {
                        float u = s / len, hw = Lerp(c.Hw[i], c.Hw[i + 1], u), ss = Lerp(c.S[i], c.S[i + 1], u);
                        float r = hw - Fold(c.R[i], ss, side);
                        var q = a + t * s + n * side * r;
                        if (!v.Vis(q.X, q.Y, 20)) continue;
                        if (Sdf(q.X + n.X * side * 3, q.Y + n.Y * side * 3) < -2) continue; // w świetle innego kanału (połączenie)
                        var o = n * side; // na zewnątrz
                        if (squam) p.FillEllipse(q.X + o.X * 5, q.Y + o.Y * 5, 4.5f, 1.6f, MathF.Atan2(t.Y, t.X), Rgb(150, 110, 110, 0.35f));
                        else
                        {
                            p.Seg(q.X, q.Y, q.X + o.X * 14, q.Y + o.Y * 14, lw * 0.6f, Rgb(120, 100, 90, 0.35f));
                            p.FillEllipse(q.X + o.X * 8 + t.X * 2, q.Y + o.Y * 8 + t.Y * 2, 3.2f, 1.6f, MathF.Atan2(o.Y, o.X), Rgb(110, 90, 120, 0.4f));
                        }
                        if (cilia && ((int)(ss / step)) % 3 != 0)
                        {
                            float ph = ss * 0.25f + w.T * 12, bend = MathF.Sin(ph) * 3.5f;
                            p.Seg(q.X, q.Y, q.X - o.X * (7 + 0.8f * MathF.Cos(ph)) - t.X * Math.Max(0, bend), q.Y - o.Y * (7 + 0.8f * MathF.Cos(ph)) - t.Y * Math.Max(0, bend), lw * 0.5f, Rgb(90, 85, 75, 0.35f));
                        }
                    }
            }
    }
    /// <summary>Mapa anatomiczna (jak na schemacie): mięśniówka, światło, jajniki; pęcherzyk po stronie owulacji.</summary>
    public override void DrawMinimap(World w, Painter p)
    {
        var buf = p.Shape2;
        foreach (var pass in new[] { 0, 1 })
            foreach (var c in _ch)
                for (int i = 0; i < c.P.Length - 1; i++)
                {
                    float wa = (c.Hw[i] + c.Hw[i + 1]) / 2;
                    if (pass == 0) p.Line(c.P[i].X, c.P[i].Y, c.P[i + 1].X, c.P[i + 1].Y, 2 * wa + (c.R[i] is Reg.Uterus or Reg.Cervix ? 900 : c.R[i] == Reg.Vagina ? 300 : 160), Rgb(225, 130, 175, 0.85f), true);
                    else p.Line(c.P[i].X, c.P[i].Y, c.P[i + 1].X, c.P[i + 1].Y, 2 * wa, Rgb(250, 190, 225, 0.9f), true);
                }
        foreach (int sd in new[] { -1, 1 })
        {
            float ox = OX + sd * 3700, oy = OY - 4700;
            p.FillEllipse(ox, oy, 700, 520, 0, Rgb(245, 238, 240, 0.9f));
            if (sd == EggSide) p.FillCircle(ox + sd * 250, oy - 300, 160, Rgb(250, 200, 120, 0.9f)); // pęknięty pęcherzyk (ciałko żółte)
        }
    }
    public override string[] KeyRows => new[] { "Odcinek", "pH", "Do komórki jajowej" };
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Sperm;
        if (p == null) return new() { ("Stan", "-") };
        string st = p.State switch
        {
            St.Attach => "związany z nabłonkiem (rezerwuar)", St.Enter => "przechodzi przez osłonkę przejrzystą", St.Bind => "fuzja z oolemmą",
            St.Cup or St.Vac => "fagocytowany!", St.Dead => "martwy",
            _ => p.Hyperactive ? "hiperaktywacja" : InCumulus(p.X, p.Y) ? "we wzgórku jajonośnym" : "pływanie progresywne",
        };
        w.FlowAt(p.X, p.Y, out float fx, out float fy);
        // jak w badaniu nasienia i teście po stosunku: ile plemników widać w polu widzenia 400× i ile z nich płynie naprzód
        int inView = 0, prog = 0; float vw = w.ViewR, vh = w.ViewR * 0.5625f;
        foreach (var e in w.Ents)
            if (e is Sperm s && s != p && s.Alive && s.Holder == null && s.State != St.Dead && MathF.Abs(s.X - w.CamX) < vw && MathF.Abs(s.Y - w.CamY) < vh)
            { inView++; if (s.Mot == Sperm.Motil.Progressive && s.State == St.Swim) prog++; }
        float dEgg = Egg != null ? Math.Max(0, Hypot(Egg.X - p.X, Egg.Y - p.Y) - Egg.ZonaR) : 0;
        return new()
        {
            ("Odcinek", RegionName(p.X, p.Y)), ("Stan", st),
            ("Ruch (CASA)", $"VCL {p.Vcl:0} · VSL {p.Vsl:0} µm/s · LIN {(p.Vcl > 1 ? p.Vsl / p.Vcl : 0):0.00}"),
            ("pH", $"{Ph(p.X, p.Y):0.0}"), ("Temperatura", $"{Temp(p.X, p.Y):0.0} °C"), ("Przepływ", $"{Hypot(fx, fy):0} µm/s"),
            ("Kapacytacja", Pct(p.Cap)), ("Akrosom", p.Acrosome ? "reakcja zaszła" : "nienaruszony"),
            ("Do komórki jajowej", dEgg >= 1000 ? $"{dEgg / 1000:0.00} mm" : $"{dEgg:0} µm"), ("Plemniki w polu widzenia", $"{inView} (ruch postępowy: {prog})"),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Sperm p) return null;
        var l = new List<(string, float, Color)>
        {
            ("Zapas energii (ATP)", p.Energy, p.Energy < 0.2f ? Hex("#e05a4a") : Hex("#cfd8e8")),
            ("Kapacytacja", p.Cap, Hex("#9fd0ff")),
        };
        if (p.State == St.Enter) l.Add(("Przejście przez osłonkę", p.Zona, Hex("#ffd27a")));
        return l;
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Oocyte) return 1;
        if (e is Leuko) return 2;
        return e is Sperm s && s.State != St.Dead && e != w.Player ? 3 : 0;
    }
    /// <summary>
    /// Wskazówka kierunku: plemnik nie „wie”, gdzie jest komórka jajowa. Z daleka jedyne wskazówki to prąd (reotaksja: pod prąd),
    /// w macicy – kierunek fal perystaltycznych; dopiero przy wzgórku – progesteron.
    /// </summary>
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var p = w.Player; if (p == null || Egg == null) return false;
        if (ChemoDir(p.X, p.Y, out _)) { x = Egg.X; y = Egg.Y; return true; }
        if (!Axis(p.X, p.Y, out float tx, out float ty, out var r, out _)) return false;
        x = p.X + tx * 300; y = p.Y + ty * 300;
        return r != Reg.Crypt;
    }
    public override float Score(World w) => w.Stat("fert");
    public override int LinePop(World w) => w.Player is Sperm s && s.State != St.Dead ? 1 : 0;
    public override bool OnPlayerLost(World w, Ent p) => false;
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Sperm(w, 27, 0, 0, true)); return (e, 75); }
}

using System.Linq;
namespace Mikro;

// =====================================================================
//  Komórka nowotworowa – kaskada przerzutowania w tkance z żyłkami:
//  guz pierwotny (gradient tlenu, niedotlenienie, angiogeneza) → EMT → inwazja przez kolagen → intrawazacja
//  → krążenie (naprężenia ścinające, komórki NK, osłona płytek) → przyleganie → ekstrawazacja → MET i kolonia przerzutowa
// =====================================================================
public sealed class ScCancer : ScNeutro
{
    protected override bool Infection => false;
    /// <summary>Środek guza pierwotnego i miejsce wyjścia z naczynia (przerzut).</summary>
    public float TumX, TumY, MetX, MetY;
    public bool Seeded;
    /// <summary>Kiełki naczyń (angiogeneza): łamana od ściany żyłki ku niedotlenionym komórkom.</summary>
    sealed class Sprout { public readonly List<Vector2> P = new(); public Vector2 Target; public bool Active = true; }
    readonly List<Sprout> _spr = new();
    float _vegf;
    float _nkT;
    /// <summary>Test: bez napływu limfocytów.</summary>
    public bool NoImmune;
    /// <summary>Test: promień gniazda nie zmienia się z liczbą komórek.</summary>
    public bool FixedR;

    public ScCancer()
    {
        Id = "cancer"; Name = "Komórka nowotworowa"; Latin = "rak (carcinoma) – Homo sapiens"; TypeName = "Komórka somatyczna (nowotwór)"; Size = "≈ 15–20 µm"; Color = Hex("#d9b3c2");
        Desc = "Uciekaj z niedotlenionego guza: EMT, przebij się przez kolagen do żyłki, przetrwaj krążenie pod osłoną płytek i załóż przerzut. Uważaj na limfocyty T i komórki NK.";
        ViewW = 190; TimeScale = 240; Bio = 8; NeutroLife = 0;
        BiomeNames = new[] { "Tkanka łączna luźna", "Tkanka zbita (kolagen)", "Zrąb z makrofagami", "Wybroczyna (wylew krwi)" };
        BiomeColors = new[] { Rgb(200, 190, 170), Rgb(235, 230, 215), Rgb(205, 175, 120), Rgb(190, 80, 80) };
        Mag = "Obiektyw 40× (400×) – time-lapse ×240 (we krwi – czas rzeczywisty)"; ScoreLabel = "komórek klonu naraz"; LineUnit = "komórek"; BirthWord = "podziałów";
        Reproduces = true; SomaticShort = ""; SomaticNote = ""; FieldName = "Mapa niedotlenienia (pimonidazol)";
        Controls = "WASD / przytrzymaj LPM – pełzanie · Spacja – tryb migracji (mezenchymalny ↔ ameboidalny) · napieraj na żyłkę – intrawazacja · we krwi: przytrzymaj Spację przy ścianie – przyleganie · G – mapa niedotlenienia";
        LoseText = "Twój klon wymarł – układ odpornościowy i brak tlenu wygrały";
        ReproFact = "Komórka nowotworowa dzieli się (cykl ≈ 1–2 doby) i przekazuje mutacje potomnym – w guzie działa dobór naturalny: klony z korzystnymi mutacjami wypierają inne (Nowell 1976; Greaves i Maley 2012). To jednak ewolucyjny ślepy zaułek: klon ginie razem z gospodarzem. Wyjątkiem są rzadkie nowotwory zakaźne – guz twarzy diabła tasmańskiego i przenośny guz weneryczny psów – które przeskoczyły na nowych żywicieli.";
        Goals = new[]
        {
            new Goal("Podziel się (mitoza)", 1, w => w.Stat("t_div")),
            new Goal("EMT: stań się komórką inwazyjną (głąb guza albo kontakt z makrofagiem)", 1, w => w.Stat("t_emt")),
            new Goal("Inwazja: wyjdź z gniazda guza w głąb zrębu (≥ 40 µm od jego brzegu)", 1, w => w.Stat("t_inv")),
            new Goal("Intrawazacja: wejdź do żyłki (najszybciej obok makrofaga)", 1, w => w.Stat("t_intra")),
            new Goal("Przeżyj krążenie i wyjdź z naczynia ≥ 1 mm od guza", 1, w => w.Stat("t_extra")),
            new Goal("Przerzut: 4 komórki Twojej linii w nowym miejscu", 4, w => w.Stat("t_meta")),
        };
        Facts = new[]
        {
            "Komórka raka (carcinoma) ma zwykle 15–20 µm, duże nieregularne jądro z wyraźnymi jąderkami i wysoki stosunek jądra do cytoplazmy – tak rozpoznaje ją patolog. Rak powstaje z nabłonka; tu komórka wniknęła już do tkanki łącznej pod nim.",
            "Guz pod mikroskopem to nie równy „plaster miodu”: komórki raka tworzą gniazda i pasma w zrębie, różnią się wielkością i kształtem, a ich jądra – wielkością, zarysem i gęstością chromatyny. Zdarzają się komórki dwujądrowe i olbrzymie oraz figury podziału. Ta różnorodność (pleomorfizm) to jeden z głównych znaków złośliwości. W grze pokazane jest jedno gniazdo o średnicy ~0,25 mm – prawdziwy wykrywalny guz ma milimetry–centymetry i miliardy komórek.",
            "Desmoplazja: fibroblasty związane z nowotworem (CAF – miofibroblasty z α-SMA) otaczają guz i odkładają gęsty, sztywny kolagen (Kalluri, Nat Rev Cancer 2016). Na granicy guza włókna biegną stycznie, a w miejscach inwazji prostują się i ustawiają promieniście – komórki raka wędrują wzdłuż nich jak po szynach (Provenzano i in., BMC Med 2006). Taki układ kolagenu wiąże się z gorszym rokowaniem (Conklin i in. 2011).",
            "Naczynia w guzie: rosnące komórki ściskają naczynia włosowate i przepływ w nich ustaje (Padera i in., Nature 2004) – dlatego środek gniazda jest niedotleniony, choć w zdrowym zrębie obok naczynia leżą co kilkadziesiąt µm. Naczynia z angiogenezy są kręte i nieszczelne (Jain, Science 2005). Gdy guz rośnie, jego środek obumiera – powstaje martwica z bladymi „cieniami” komórek.",
            "Śmierć w guzie: brak tlenu zabija przez martwicę – komórka puchnie i pęka, zostaje blady „cień” z okruchami chromatyny. p53 i limfocyty T/NK zabijają przez apoptozę – komórka obkurcza się i rozpada na ciałka apoptotyczne, które makrofagi zjadają w ciągu godzin (eferocytoza), więc widać ich niewiele naraz.",
            "Makrofagi mogą stanowić dużą część masy guza; większość z nich wspiera wzrost, angiogenezę i przerzutowanie (Qian i Pollard, Cell 2010). Limfocyty T często utykają w zrębie na obrzeżu guza – gęsty kolagen i sygnały CAF utrudniają im dotarcie do komórek raka (Salmon i in., J Clin Invest 2012; Joyce i Fearon, Science 2015).",
            "Inwazja zbiorowa: wiele raków nacieka pasmami i gniazdami komórek, które trzymają się razem, a drogę otwierają komórki prowadzące albo CAF (Gaggioli i in. 2007; Friedl i in. 2012). Do krwi trafiają też skupiska komórek – łatwiej zakładają przerzut niż pojedyncze komórki (Aceto i in., Cell 2014). Raki często najpierw przerzutują przez naczynia chłonne do węzłów chłonnych; w grze pokazana jest droga przez krew.",
            "Rak to ewolucja wewnątrz ciała: mutacje są losowe, a dobór zostawia klony, które szybciej się dzielą i lepiej przeżywają (Nowell 1976). Cechy nowotworu – samowystarczalność wzrostu, unikanie apoptozy, angiogeneza, inwazja i przerzuty, unikanie odporności – opisali Hanahan i Weinberg (Cell 2011).",
            "Tlen dyfunduje z naczyń tylko na ~150–200 µm. Dalej komórki giną i powstaje martwica – to zaobserwowali już Thomlinson i Gray (1955) w przekrojach raka płuca. Mapa niedotlenienia w grze pokazuje te strefy (w badaniach barwi się je pimonidazolem).",
            "Niedotlenienie stabilizuje czynnik HIF-1α. Ten włącza geny angiogenezy (VEGF), glikolizy i EMT, m.in. TWIST (Yang i in., Nat Cell Biol 2008) – dlatego guzy niedotlenione częściej dają przerzuty.",
            "Umiarkowane niedotlenienie (kilka mmHg – mediana pO₂ w wielu rakach to ~5–10 mmHg, Vaupel 2004) komórki raka przeżywają. W ciężkim niedotlenieniu i anoksji (poniżej ~0,2% O₂) białko p53 uruchamia apoptozę; komórki z uszkodzonym TP53 przeżywają i są wybierane przez dobór (Graeber i in., Nature 1996; Hammond i Giaccia 2005).",
            "Angiogeneza: VEGF z niedotlenionych komórek przyciąga kiełki naczyń – komórka „szczytowa” z filopodiami prowadzi kiełek (Folkman 1971). W rogówce królika naczynia rosły ku guzowi ok. 0,2 mm na dobę (Gimbrone i in. 1974); w grze to przyspieszone jak inne procesy życiowe.",
            "EMT (przejście nabłonkowo-mezenchymalne): komórka traci E-kadherynę i połączenia z sąsiadami, staje się ruchliwa. Proces jest odwracalny – w miejscu przerzutu musi zajść MET, by komórki znów się dzieliły (Ocaña i in. 2012; Tsai i in. 2012).",
            "„Idź albo rośnij”: komórka, która wędruje, dzieli się wolniej – migracja i podział konkurują o te same zasoby komórki (Giese i in., J Clin Oncol 2003). Dlatego w grze EMT spowalnia cykl komórkowy.",
            "Dwa tryby ruchu w tkance: mezenchymalny – wrzecionowata komórka trawi kolagen metaloproteinazą MT1-MMP i tworzy tunel (Sabeh i in., J Cell Biol 2004), ≈ 0,1–1 µm/min; ameboidalny – okrągła komórka z pęcherzykami błony, szybsza, ale bez trawienia utyka, gdy oczka sieci są węższe niż jej jądro (Wolf i in. 2003, 2013; Friedl i Wolf 2003).",
            "Makrofagi towarzyszące guzowi (TAM) dostarczają EGF, a komórki raka – CSF-1: ta pętla przyspiesza ruch obu (Wyckoff i in., Cancer Res 2004). Przy naczyniu makrofag, komórka raka i komórka śródbłonka tworzą „bramę” TMEM, przez którą komórki wchodzą do krwi (Harney i in., Cancer Discov 2015).",
            "Krążenie to wąskie gardło: naprężenia ścinające i komórki NK zabijają większość krążących komórek w ciągu minut–godzin. W modelu wątroby 83% wstrzykniętych komórek wyszło z naczyń, ale tylko ~2% utworzyło mikroprzerzuty, a ~0,02% – przerzuty makroskopowe (Luzzi i in. 1998; Chambers i in. 2002).",
            "Czynnik tkankowy komórki raka uruchamia krzepnięcie; płytki krwi okrywają ją i chronią przed komórkami NK (Nieswandt i in. 1999; Palumbo i in. 2005), a ich TGF-β podtrzymuje EMT (Labelle i in. 2011).",
            "Odporność: limfocyty T CD8⁺ rozpoznają antygeny nowotworowe na MHC-I; pojedyncze trafienie perforyną rzadko zabija – uszkodzenia się sumują (Weigelin i in. 2021), a in vivo limfocyt T zabija tylko kilka komórek na dobę (Halle i in. 2016). Utrata MHC-I (B2M) ukrywa przed nimi, ale odsłania przed komórkami NK – „brak swojego” (Ljunggren i Kärre 1990). PD-L1 wyłącza limfocyty T (Freeman i in. 2000) – na tę oś działają leki anty-PD-1.",
            "Uproszczenia gry: czas jest przyspieszony (tkanka ×240, a podziały, EMT i trawienie jeszcze 8× – cykl komórkowy ~1 min gry); mutacje kierujące pojawiają się dużo częściej niż w naturze; krew w żyłce jest spowolniona ~4× (jak w trybie neutrofila); „odległy narząd” to ta sama tkanka ≥ 1 mm dalej – naprawdę krążąca komórka przechodzi przez serce i zatrzymuje się zwykle w pierwszej sieci naczyń włosowatych (płuca, wątroba).",
            "Rzut 2D trójwymiarowej tkanki: fibroblasty (CAF) nie są ścianą – komórki raka i odporności przechodzą nad nimi i pod nimi (wolniej). Limfocyty T/NK i makrofagi obchodzą żyłkę nad nią lub pod nią, a komórka raka zatrzymuje się na ścianie żyłki – tu zaczyna się intrawazacja.",
        };
    }

    // ------------------------------------------------ tlen i naczynia
    /// <summary>
    /// Ciśnienie parcjalne tlenu (mmHg): ~45 przy ścianie drożnego naczynia (żyłka, kiełek, naczynie włosowate), spadek do
    /// zera w ~185 µm (Thomlinson i Gray 1955). W zdrowej tkance naczynia włosowate są co kilkadziesiąt µm, więc tlenu nie
    /// brakuje; w guzie komórki ściskają naczynia, dlatego środek gniazda jest niedotleniony.
    /// </summary>
    public float O2(World w, float x, float y)
    {
        int k = VIdx(y);
        float d = MathF.Abs(y - Yc(w, k, x)) - VHW;
        float dc = CapDist(w, x, y, true) - CapR; if (dc < d) d = dc;
        d = Math.Max(0, d);
        float o = 45 * MathF.Pow(Math.Max(0, 1 - d / 185f), 1.4f);
        // naczynia z angiogenezy niosą krew dopiero po połączeniu (anastomozie), a i tak słabo: są kręte i nieszczelne,
        // przepływ w nich jest wolny i przerywany – dlatego guz pozostaje niedotleniony mimo nowych naczyń (Jain 2005)
        float ds = 1e9f;
        foreach (var s in _spr)
        {
            var P = s.P;
            if (s.Active || P.Count < 2) continue;
            for (int i = 0; i < P.Count - 1; i++)
            {
                var a = P[i]; var ab = P[i + 1] - a; float l2 = ab.LengthSquared(); if (l2 < 1e-4f) continue;
                float t = Clamp(((x - a.X) * ab.X + (y - a.Y) * ab.Y) / l2, 0, 1);
                float dd = Hypot(x - a.X - ab.X * t, y - a.Y - ab.Y * t) - 3.5f;
                if (dd < ds) ds = dd;
            }
        }
        if (ds < 1e8f) o = Math.Max(o, 22 * MathF.Pow(Math.Max(0, 1 - Math.Max(0, ds) / 150f), 1.4f));
        return o;
    }
    /// <summary>Promień gniazda guza (µm; rośnie z liczbą komórek) i fazy jego płatowatego zarysu.</summary>
    public float TumR = 118;
    float _nPh1, _nPh2, _nPh3;
    /// <summary>Promień zarysu gniazda w kierunku θ – zarys nieregularny, płatowaty (guz nacieka nierówno).</summary>
    public float NestR(float th) => TumR * (1 + 0.1f * MathF.Sin(3 * th + _nPh1) + 0.07f * MathF.Sin(5 * th + _nPh2) + 0.04f * MathF.Sin(9 * th + _nPh3));
    /// <summary>Guz jest bryłą – nad i pod płaszczyzną ostrości leżą kolejne, rozmyte komórki gniazda.</summary>
    public override BgZone Backdrop(World w, float x, float y)
    {
        var z = base.Backdrop(w, x, y);
        if (!Seeded && TumX == 0 && TumY == 0) return z;
        float d = Hypot(x - TumX, y - TumY), k = 1 - Smooth((d - TumR * 0.8f) / (TumR * 0.9f));
        if (k > 0) { z.B1 = Lerp(z.B1, 0.85f, k); z.Rod = Lerp(z.Rod, 0.2f, k); z.Haze = Lerp(z.Haze, 0.2f, k); }
        return z;
    }
    public bool InNest(float x, float y, float margin = 0) => Hypot(x - TumX, y - TumY) < NestR(MathF.Atan2(y - TumY, x - TumX)) + margin;
    /// <summary>Strefa w HUD: gniazdo raka (miąższ guza), otaczający je zrąb z CAF i kolagenem, światło żyłki – albo biom tkanki.</summary>
    public override string ZoneAt(World w, Ent p)
    {
        if (p is TumorCell { Where: not TumorCell.Site.Tissue }) return "Światło żyłki (krew)";
        if (InNest(p.X, p.Y)) return "Gniazdo raka (miąższ guza)";
        if (InNest(p.X, p.Y, 45)) return "Zrąb guza (desmoplazja)";
        return base.ZoneAt(w, p);
    }
    /// <summary>Naczynia w obrębie litego guza są ściśnięte przez komórki (naprężenie w guzie) – przepływ w nich ustaje (Padera i in. 2004).</summary>
    protected override bool CapOpen(World w, float x, float y) => !InNest(x, y, 4);
    public override string InspectAt(World w, float x, float y)
    {
        foreach (var s in _spr)
            for (int i = 0; i < s.P.Count - 1; i++)
            {
                var a = s.P[i]; var ab = s.P[i + 1] - a; float l2 = ab.LengthSquared(); if (l2 < 1e-4f) continue;
                float t = Clamp(((x - a.X) * ab.X + (y - a.Y) * ab.Y) / l2, 0, 1);
                if (Hypot(x - a.X - ab.X * t, y - a.Y - ab.Y * t) < 5) return "sprout";
            }
        return base.InspectAt(w, x, y);
    }
    /// <summary>Odległość od ściany najbliższej żyłki (≤ 0 w świetle) i kierunek ku niej.</summary>
    public float WallDist(World w, float x, float y, out float nx, out float ny)
    {
        int k = VIdx(y); float dy = y - Yc(w, k, x);
        nx = 0; ny = dy >= 0 ? -1 : 1;
        return MathF.Abs(dy) - VHW;
    }
    /// <summary>Limit liczby komórek nowotworowych w aktywnym oknie (wydajność); gracz dzieli się zawsze.</summary>
    public bool RoomToGrow(World w) => _room;
    public int SproutCount => _spr.Count;
    bool _room = true;

    public override void Init(World w)
    {
        ResetTissue();
        _spr.Clear(); _vegf = 0; Seeded = false; NoImmune = false; FixedR = false;
        _nPh1 = Rand(0, TAU); _nPh2 = Rand(0, TAU); _nPh3 = Rand(0, TAU); TumR = 118;
        // gniazdo raka w zrębie między dwiema żyłkami (środek ~210 µm od każdej)
        TumX = 60; TumY = VesselY(w, 60, 0) + 210;
        w.CamX = TumX; w.CamY = TumY;
        w.UpdateChunks(true);
        // guz zastąpił zrąb: w gnieździe nie ma włókien ani komórek tkanki; kolagen wokół przebudowują CAF (desmoplazja)
        foreach (var e in w.Ents)
            if (e is Collagen && InNest(e.X, e.Y, 48) || e is Adipocyte && InNest(e.X, e.Y, e.Radius + 6) || e is Fibroblast or MastCell or Lymphocyte or RBC or Platelet or Macrophage && InNest(e.X, e.Y, 12)) e.Alive = false;
        w.Ents.RemoveAll(e => !e.Alive);
        // makrofagi towarzyszące guzowi (TAM): na obrzeżu, w gnieździe i przy żyłce (TMEM – brama do krwi)
        var placed = new List<(float x, float y, float r)>();
        void Tam(float x, float y) { var m = w.Add(new Macrophage(w, x, y)); placed.Add((x, y, m.R0 * 1.1f)); }
        for (int i = 0; i < 6; i++) { float th = i * TAU / 6 + Rand(-0.4f, 0.4f), r = NestR(th) + Rand(4, 18); Tam(TumX + MathF.Cos(th) * r, TumY + MathF.Sin(th) * r); }
        for (int i = 0; i < 2; i++) { float th = Rand(0, TAU), r = NestR(th) * Rand(0.4f, 0.65f); Tam(TumX + MathF.Cos(th) * r, TumY + MathF.Sin(th) * r); }
        for (int i = 0; i < 2; i++) { float x = TumX + Rand(-80, 80); Tam(x, VesselY(w, x, 0) + VHW + 15); }
        // komórki raka: gęsto, ale bez ładu – różnej wielkości (anizocytoza), z różnymi jądrami (pleomorfizm)
        var cells = new List<TumorCell>();
        float rmax = TumR * 1.25f;
        for (int tries = 0; tries < 24000; tries++)
        {
            float x = TumX + Rand(-rmax, rmax), y = TumY + Rand(-rmax, rmax), r0 = Clamp(8.6f + RandN() * 1.15f, 6.3f, 12f);
            if (Chance(0.012f)) r0 = Rand(12f, 13.5f); // komórka olbrzymia
            if (!InNest(x, y, -r0 * 0.7f) || InVessel(w, x, y, r0 + 6)) continue;
            bool ok = true;
            // spójny nabłonek (E-kadheryna): komórki ściśle przylegają – błony spłaszczają się o sąsiadów, bez przerw między nimi
            foreach (var q in placed) if (Sq(q.x - x) + Sq(q.y - y) < Sq((q.r + r0) * 0.8f)) { ok = false; break; }
            if (!ok) continue;
            placed.Add((x, y, r0));
            var c = w.Add(new TumorCell(w, x, y, Rand(0, TAU)) { Emt = Rand(0, 0.12f), R0 = r0 });
            c.R = r0 * 1.5f; c.Bi |= r0 > 11.8f; c.Shape(); c.Mem.Follow(x, y, c.A, 1, 1);
            cells.Add(c);
        }
        TumorCell me = null; float bd = 1e9f;
        foreach (var c in cells) { float d = Hypot(c.X - TumX, c.Y - (TumY - 32)); if (d < bd && c.R0 < 11.8f) { bd = d; me = c; } }
        me ??= w.Add(new TumorCell(w, TumX, TumY - 32, 0));
        me.Growth = 0.55f; me.Emt = 0.1f; me.Muts = Muts.None; me.Bi = false;
        w.SetPlayer(me);
        // fibroblasty związane z nowotworem (CAF) owijają gniazdo; kolagen wokół: styczny (TACS-2) i promienisty w miejscach inwazji (TACS-3)
        for (int i = 0; i < 10; i++)
        {
            float th = i * TAU / 10 + Rand(-0.25f, 0.25f), r = NestR(th) + Rand(14, 34), x = TumX + MathF.Cos(th) * r, y = TumY + MathF.Sin(th) * r;
            if (!InVessel(w, x, y, 16)) w.Add(new Fibroblast(w, x, y, th + PI / 2 + RandN() * 0.15f, true));
        }
        for (int i = 0; i < 34; i++)
        {
            float th = Rand(0, TAU), r = NestR(th) + Rand(3, 44), x = TumX + MathF.Cos(th) * r, y = TumY + MathF.Sin(th) * r;
            if (!InVessel(w, x, y, 12)) w.Add(new Collagen(w, x, y, th + PI / 2 + RandN() * 0.12f, Rand(30, 60), Rand(0.6f, 0.95f), 0.7f));
        }
        foreach (float th0 in new[] { -PI / 2 + Rand(-0.3f, 0.3f), Rand(0, TAU) })
            for (int i = 0; i < 7; i++)
            {
                float th = th0 + Rand(-0.32f, 0.32f), r0 = NestR(th) - 6, len = 0, want = Rand(55, 95), ca = MathF.Cos(th), sa = MathF.Sin(th);
                while (len < want && !InVessel(w, TumX + ca * (r0 + len + 2), TumY + sa * (r0 + len + 2), 6)) len += 2;
                if (len < 25) continue;
                float rm = r0 + len / 2;
                w.Add(new Collagen(w, TumX + ca * rm, TumY + sa * rm, th, len, Rand(0.7f, 1f), 0.2f));
            }
        // limfocyty T CD8⁺ i komórki NK – głównie w zrębie na obrzeżu guza
        for (int i = 0; i < 7; i++) { float th = Rand(0, TAU), r = NestR(th) + Rand(8, 30); w.Add(new Killer(w, TumX + MathF.Cos(th) * r, TumY + MathF.Sin(th) * r, i >= 5)); }
        w.Log("Jesteś komórką raka w gnieździe guza (~0,25 mm) otoczonym zrębem. Naczynia wewnątrz gniazda są ściśnięte, więc w środku brakuje tlenu – niedotlenienie (HIF-1α) albo kontakt z makrofagiem uruchomi EMT. Klawisz G – mapa niedotlenienia.", "info");
    }
    /// <summary>Limfocyt z najbliższej żyłki (diapedeza) w pobliżu guza lub gracza.</summary>
    void Recruit(World w, bool nk)
    {
        var p = w.Player; float x = (p?.X ?? TumX) + Rand(-120, 120), yref = p?.Y ?? TumY;
        int k = VIdx(yref); float yc = Yc(w, k, x), side = yref >= yc ? 1 : -1;
        w.Add(new Killer(w, x, yc + side * (VHW + 6), nk));
    }

    public override void Constrain(World w, Ent e)
    {
        if (e is TumorCell { Where: not TumorCell.Site.Tissue } t)
        {
            int k = VIdx(t.Y); float yc = Yc(w, k, t.X), half = VHW - t.R0 * 0.75f;
            t.Y = yc + (t.Where == TumorCell.Site.Wall ? MathF.Sign(t.Lane == 0 ? 1 : t.Lane) : t.Lane) * half;
            return;
        }
        if (e is Killer { InBlood: true } kb)
        {
            int k = VIdx(kb.Y); kb.Y = Yc(w, k, kb.X) + kb.Lane * (VHW - 4.6f);
            return;
        }
        base.Constrain(w, e);
    }
    public void EnterBlood(World w, TumorCell c)
    {
        int k = VIdx(c.Y); float yc = Yc(w, k, c.X);
        c.Where = TumorCell.Site.Blood; c.Lane = Math.Sign(c.Y - yc) * 0.4f; c.Shield = 0; c.Adh = 0;
        c.Y = yc + c.Lane * (VHW - c.R0 * 0.75f);
        w.TimeScale = 1;
        w.AddStat("t_intra", 1);
        w.Banner("Intrawazacja!", "Jesteś w krwiobiegu – czas płynie teraz normalnie");
        w.Log("Komórka przecisnęła się między komórkami śródbłonka do światła żyłki. Krew niesie komórkę; płytki zaczynają ją okrywać. Przytrzymaj Spację przy ścianie, by przylgnąć – ale im dalej od guza, tym lepiej (≥ 1 mm).", "info");
        _nkT = 1.5f;
    }
    public void Arrest(World w, TumorCell c)
    {
        c.Where = TumorCell.Site.Wall; c.Extra = 0;
        w.TimeScale = TimeScale;
        w.AddStat("t_arrest", 1);
        float dist = Hypot(c.X - TumX, c.Y - TumY);
        w.Log($"Komórka przylgnęła do śródbłonka {dist / 1000:0.0#} mm od guza. Teraz godzinami przeciska się przez ścianę naczynia (ekstrawazacja).", "good");
    }
    public void Extravasate(World w, TumorCell c)
    {
        int k = VIdx(c.Y); float yc = Yc(w, k, c.X), side = MathF.Sign(c.Lane == 0 ? 1 : c.Lane);
        c.Where = TumorCell.Site.Tissue; c.Y = yc + side * (VHW + c.R0 + 1.5f); c.A = side > 0 ? PI / 2 : -PI / 2; c.Shield = 0;
        foreach (var e in w.Ents) if (e is Killer { InBlood: true }) e.Alive = false;
        float dist = Hypot(c.X - TumX, c.Y - TumY);
        MetX = c.X; MetY = c.Y; Seeded = true;
        if (dist >= 1000)
        {
            w.AddStat("t_extra", 1);
            w.Banner("Ekstrawazacja", $"{dist / 1000:0.0#} mm od guza pierwotnego");
            w.Log("Jesteś w nowym miejscu. Żeby założyć kolonię, komórka musi wrócić do fenotypu nabłonkowego (MET) – zatrzymaj się w dobrze natlenionej tkance i dziel się. Większość takich komórek nigdy tego nie robi (uśpienie, Luzzi i in. 1998).", "good");
        }
        else w.Log($"Komórka wyszła z naczynia zaledwie {dist:0} µm od guza – to naciekanie miejscowe, nie przerzut odległy. Wejdź do żyłki jeszcze raz.", "info");
    }

    public override void Update(World w, float dt, float gdt)
    {
        Circulation(w, gdt);
        var pl = w.Player as TumorCell;
        // we krwi czas płynie normalnie (przepływ ~0,3 mm/s), w tkance – time-lapse
        float ts = pl is { Where: TumorCell.Site.Blood } ? 1 : TimeScale;
        if (w.TimeScale != ts) w.TimeScale = ts;
        if (pl is { Where: TumorCell.Site.Blood })
        {
            // komórki NK we krwi: toczą się wolniej niż krew – krążąca komórka na nie wpada
            _nkT -= gdt;
            if (_nkT <= 0)
            {
                _nkT = ExpRand(2.6f);
                var nk = w.Add(new Killer(w, pl.X + Rand(260, 480), pl.Y, true, true) { Lane = Rand(-0.85f, 0.85f) });
                nk.Y = VesselY(w, nk.X, pl.Y) + nk.Lane * (VHW - 4.6f);
            }
            foreach (var e in w.Ents) if (e is Killer { InBlood: true } k && k.X < pl.X - 250) k.Alive = false;
            float dist = Hypot(pl.X - TumX, pl.Y - TumY);
            if (dist > 1000) w.LogOnce("far", "Ponad 1 mm od guza – tu przyleganie (Spacja przy ścianie) da przerzut odległy.", "good", 999);
        }
        UpdateSprouts(w, dt * w.Bio);
        if (!w.EverySecond(gdt)) return;
        _room = w.Count(e => e is TumorCell) < 230;
        // gniazdo rośnie z liczbą komórek (i martwiczych „cieni”, które nadal zajmują miejsce) – ściska kolejne naczynia
        int nest = 0;
        foreach (var e in w.Ents)
            if ((e is TumorCell { Where: TumorCell.Site.Tissue } || e is TumorDebris { Necrotic: true }) && Hypot(e.X - TumX, e.Y - TumY) < TumR * 1.3f + 30) nest++;
        if (!FixedR) TumR = Lerp(TumR, Clamp(MathF.Sqrt(nest * 336 / PI), 106, 175), 0.2f);
        if (pl != null)
        {
            if (pl.Emt >= 0.6f && w.Stat("t_emt") == 0) { w.AddStat("t_emt", 1); w.Log("EMT: E-kadheryna znika, komórka staje się ruchliwa (vimentyna, N-kadheryna). Spacja przełącza tryb mezenchymalny ↔ ameboidalny.", "good"); }
            if (pl.Where == TumorCell.Site.Tissue && !InNest(pl.X, pl.Y, 40) && w.Stat("t_inv") == 0) { w.AddStat("t_inv", 1); w.Log("Inwazja: komórka opuściła gniazdo guza i wędruje w zrębie. Naczynia włosowate zrębu dają tlen, a najbliższa żyłka – drogę do krwi.", "good"); }
            if (pl.Where == TumorCell.Site.Tissue && pl.Hif > 0.6f) w.LogOnce("hif", "Niedotlenienie (pO₂ < 10 mmHg): HIF-1α włącza VEGF i program EMT.", "info", 60);
        }
        if (Seeded) w.Stats["t_meta"] = w.Count(e => e is TumorCell t && t.Lineage && t.Where == TumorCell.Site.Tissue && Hypot(t.X - MetX, t.Y - MetY) < 160);
        // odporność: z guzem rośnie naciek limfocytów (immunoedycja)
        if (pl is { Where: TumorCell.Site.Tissue } && !NoImmune)
        {
            int tumor = w.Count(e => e is TumorCell), kill = w.Count(e => e is Killer { InBlood: false });
            int want = Math.Min(9, 2 + tumor / 8);
            if (kill < want && Chance(0.12f)) Recruit(w, Chance(0.3f));
        }
    }

    // ------------------------------------------------ angiogeneza
    void UpdateSprouts(World w, float bdt)
    {
        // wzrost kiełka ≈ 0,2 mm/dobę (Gimbrone i in. 1974) – czas procesów życiowych
        float step = 200f / 86400 * bdt;
        foreach (var s in _spr)
        {
            if (!s.Active || s.P.Count == 0) continue;
            var tip = s.P[^1]; var d = s.Target - tip; float l = d.Length();
            if (l < 12)
            {
                s.Active = false;
                if (w.Player is TumorCell pt && Hypot(pt.X - tip.X, pt.Y - tip.Y) < 220)
                    w.LogOnce("anast", "Kiełek dotarł do niedotlenionych komórek i połączył się z innym naczyniem (anastomoza) – płynie nim krew, ale naczynie guza jest kręte i nieszczelne, więc tlenu przybywa niewiele.", "info", 120);
                continue;
            }
            float wob = 0.5f * MathF.Sin(s.P.Count * 0.7f);
            var dir = (d / l).Rotated(wob * 0.4f);
            var nt = tip + dir * step;
            if ((nt - s.P[Math.Max(0, s.P.Count - 2)]).Length() > 4 || s.P.Count < 2) s.P.Add(nt); else s.P[^1] = nt;
            if (s.P.Count > 90) s.Active = false;
        }
        if (w.Frame % 30 != 0) return;
        // VEGF z niedotlenionych komórek; nowy kiełek, gdy w pobliżu nie ma naczynia ani kiełka
        TumorCell far = null; int hyp = 0;
        foreach (var e in w.Ents)
            if (e is TumorCell t && t.Where == TumorCell.Site.Tissue && t.Hif > 0.5f) { hyp++; if (far == null || t.Hif > far.Hif) far = t; }
        _vegf += hyp * 0.5f;
        foreach (var s in _spr) if (s.Active && far != null && (s.Target - new Vector2(far.X, far.Y)).Length() < 90) s.Target = new Vector2(far.X, far.Y);
        if (far == null || _vegf < 60 || _spr.Count(s => s.Active) >= 3) return;
        foreach (var s in _spr) if (s.P.Count > 0 && (s.P[^1] - new Vector2(far.X, far.Y)).Length() < 70) return;
        _vegf = 0;
        int k = VIdx(far.Y); float yc = Yc(w, k, far.X), side = far.Y >= yc ? 1 : -1;
        var sp = new Sprout { Target = new Vector2(far.X, far.Y) };
        sp.P.Add(new Vector2(far.X + Rand(-20, 20), yc + side * (VHW - 1)));
        _spr.Add(sp);
        if (w.Player is TumorCell pl && Hypot(pl.X - far.X, pl.Y - far.Y) < 200) w.LogOnce("angio", "Angiogeneza: VEGF z niedotlenionych komórek – z żyłki wyrasta kiełek naczynia z komórką szczytową na czele.", "info", 90);
    }
    void DrawSprouts(World w, Painter p, View v)
    {
        var buf = p.Shape2;
        foreach (var s in _spr)
        {
            int n = s.P.Count; if (n < 2) continue;
            bool vis = false; foreach (var q in s.P) if (v.Vis(q.X, q.Y, 12)) { vis = true; break; }
            if (!vis) continue;
            for (int i = 0; i < n; i++) buf[i] = s.P[i];
            var sp = new ReadOnlySpan<Vector2>(buf, 0, n);
            p.SoftStroke(sp, false, 9, Rgb(255, 255, 240, 0.2f));
            p.Stroke(sp, false, 7.5f, Rgb(120, 90, 90, 0.5f), true);
            p.Stroke(sp, false, 4.2f, Rgb(170, 115, 105, 0.45f), true);
            for (int i = 1; i < n - 1; i += 3)
            {
                var a = s.P[i]; var b = s.P[i + 1]; float ang = MathF.Atan2(b.Y - a.Y, b.X - a.X);
                float sd = (i / 3) % 2 == 0 ? 1 : -1;
                p.FillEllipse(a.X - MathF.Sin(ang) * 3.2f * sd, a.Y + MathF.Cos(ang) * 3.2f * sd, 2.6f, 0.9f, ang, Rgb(90, 80, 110, 0.5f));
            }
            // komórka szczytowa z filopodiami (Gerhardt i in. 2003)
            var tp = s.P[^1]; var pv = s.P[^2]; float ta = MathF.Atan2(tp.Y - pv.Y, tp.X - pv.X);
            if (s.Active)
                for (int f = 0; f < 5; f++)
                {
                    float fa = ta + (f - 2) * 0.35f + 0.15f * MathF.Sin(w.T * 0.8f + f), fl = 5 + 3 * MathF.Sin(w.T * 0.5f + f * 1.7f);
                    p.Line(tp.X, tp.Y, tp.X + MathF.Cos(fa) * fl, tp.Y + MathF.Sin(fa) * fl, 0.2f, Rgb(70, 60, 60, 0.55f));
                }
        }
    }
    protected override void DrawUnderExtra(World w, Painter p, View v) => DrawSprouts(w, p, v);
    /// <summary>Mapa niedotlenienia: jak barwienie pimonidazolem – brązowe tam, gdzie pO₂ &lt; 10 mmHg.</summary>
    public override void DrawOver(World w, Painter p, View v)
    {
        if (!w.ShowField) return;
        p.Identity();
        float vw = v.X1 - v.X0, cs = Math.Max(vw / 70, v.Px * 4);
        int nx = (int)(vw / cs) + 2, ny = (int)((v.Y1 - v.Y0) / cs) + 2;
        float x0 = v.X0 - cs * 0.5f, y0 = v.Y0 - cs * 0.5f;
        var cols = new Color[nx * ny];
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                float o = O2(w, x0 + i * cs, y0 + j * cs), h = Clamp(1 - o / 10, 0, 1);
                cols[j * nx + i] = o < 1.5f ? Rgb(70, 40, 20, 0.45f) : Rgb(150, 90, 35, 0.38f * h);
            }
        p.Grid(x0, y0, cs, cs, nx, ny, (i, j) => cols[j * nx + i]);
    }
    public override void DrawMinimap(World w, Painter p)
    {
        base.DrawMinimap(w, p);
        foreach (var s in _spr) for (int i = 0; i < s.P.Count - 1; i++) p.Line(s.P[i].X, s.P[i].Y, s.P[i + 1].X, s.P[i + 1].Y, 6, Rgb(170, 70, 70, 0.55f));
        p.Circle(TumX, TumY, TumR, 5, Rgb(220, 150, 180, 0.8f));
        if (Seeded) p.Circle(MetX, MetY, 40, 5, Rgb(255, 210, 120, 0.9f));
    }

    public override string[] KeyRows => new[] { "Stan", "pO₂" };
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as TumorCell;
        if (p == null) return new() { ("Stan", "-") };
        string st = p.Where switch
        {
            TumorCell.Site.Blood => "w krwiobiegu",
            TumorCell.Site.Wall => "przylega do śródbłonka – ekstrawazacja",
            _ => p.Mit > 0 ? "mitoza" : p.Emt < 0.3f ? "nabłonkowa (w guzie)" : p.Emt < 0.6f ? "częściowe EMT" : p.Amoeboid ? "inwazyjna – ameboidalna" : "inwazyjna – mezenchymalna",
        };
        float dist = Hypot(p.X - TumX, p.Y - TumY);
        var l = new List<(string, string)> { ("Stan", st) };
        if (p.Where == TumorCell.Site.Tissue)
        {
            l.Add(("pO₂", (p.O2 < 2 ? $"{p.O2:0.0} mmHg" : $"{p.O2:0} mmHg") + (p.O2 < 0.5f ? " – anoksja!" : p.O2 < 1.5f ? " – ciężkie niedotlenienie!" : p.O2 < 10 ? " – niedotlenienie" : "")));
            l.Add(("HIF-1α", Pct(p.Hif)));
        }
        else l.Add(("Przepływ krwi", $"{w.BloodV:0} µm/s"));
        l.Add(("Mutacje", MutInfo.List(p.Muts)));
        l.Add(("Od guza pierwotnego", dist >= 1000 ? $"{dist / 1000:0.00} mm" : $"{dist:0} µm"));
        l.Add(("Komórki guza w okolicy", w.Count(e => e is TumorCell).ToString()));
        l.Add(("Limfocyty T / NK", $"{w.Count(e => e is Killer { NK: false, Exhausted: false })} / {w.Count(e => e is Killer { NK: true })}"));
        if (w.Stat("t_exh") > 0) l.Add(("Wyczerpane limfocyty T", N(w.Stat("t_exh"))));
        l.Add(("Tętno", $"{w.HeartBpm:0}/min"));
        return l;
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not TumorCell p) return null;
        var l = new List<(string, float, Color)> { ("Integralność komórki", p.Hp, p.Hp < 0.35f ? Hex("#e05a4a") : Hex("#d9b3c2")) };
        if (p.Where == TumorCell.Site.Tissue)
        {
            l.Add(("Cykl komórkowy", p.Mit > 0 ? 1 : Math.Min(1, p.Growth), Hex("#b6d7a8")));
            l.Add(("EMT", p.Emt, Hex("#e6b86a")));
            if (p.Intra > 0) l.Add(("Intrawazacja", p.Intra, Hex("#e07a7a")));
        }
        else
        {
            l.Add(("Osłona płytek krwi", p.Shield, Hex("#a9a6c8")));
            if (p.Where == TumorCell.Site.Blood) l.Add(("Przyleganie", p.Adh, Hex("#9fd0ff")));
            else l.Add(("Ekstrawazacja", p.Extra, Hex("#ffd27a")));
        }
        return l;
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Killer { Exhausted: false }) return 2;
        if (e is Macrophage) return 1;
        return e is TumorCell t && t.Lineage && e != w.Player ? 3 : 0;
    }
    /// <summary>Wskazówka: przed EMT – makrofag; po EMT – najbliższa ściana żyłki (intrawazacja); po przerzucie – brak.</summary>
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0;
        if (w.Player is not TumorCell p || p.Where != TumorCell.Site.Tissue) return false;
        if (p.Emt < 0.5f) return base.FoodPoint(w, out x, out y);
        if (Seeded && w.Stat("t_extra") > 0) return false;
        int k = VIdx(p.Y); float yc = Yc(w, k, p.X);
        x = p.X; y = yc + (p.Y >= yc ? VHW : -VHW);
        return true;
    }
    public override bool OnPlayerLost(World w, Ent p) => false;
    public override float Score(World w) => w.LinePeak;
    public override int LinePop(World w) => w.CountAll(e => e is TumorCell && e.Lineage && e.Alive);
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new TumorCell(w, 0, 0, 0)); return (e, 30); }
}

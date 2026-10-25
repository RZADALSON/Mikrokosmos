namespace Mikro;

/// <summary>Styl tła mikroskopu (przekazywany do shadera).</summary>
public sealed class ScopeStyle
{
    public Color BgIn, BgMid, BgOut; public float Vig, VigIn, Grain, Uneven; public bool Dust;
    public static readonly ScopeStyle Phase = new() { BgIn = Hex("#c9ccb9"), BgMid = Hex("#b0b4a2"), BgOut = Hex("#6a6f60"), Vig = 0.62f, VigIn = 0.5f, Grain = 0.07f, Dust = true, Uneven = 0.04f };
    public static readonly ScopeStyle Tissue = new() { BgIn = Hex("#cdc8b6"), BgMid = Hex("#b6af9d"), BgOut = Hex("#6c6657"), Vig = 0.62f, VigIn = 0.5f, Grain = 0.07f, Dust = true, Uneven = 0.04f };
    public static readonly ScopeStyle Pond = new() { BgIn = Hex("#c6ccb3"), BgMid = Hex("#abb298"), BgOut = Hex("#5f6852"), Vig = 0.66f, VigIn = 0.48f, Grain = 0.07f, Dust = true, Uneven = 0.05f };
    /// <summary>Menu: ciemne pole (jak w ciemnym polu / fluorescencji), na którym widać helisy DNA.</summary>
    public static readonly ScopeStyle Menu = new() { BgIn = Hex("#1d2b33"), BgMid = Hex("#141f26"), BgOut = Hex("#05080a"), Vig = 0.7f, VigIn = 0.42f, Grain = 0.08f, Dust = true, Uneven = 0.05f };
    public static readonly ScopeStyle Em = new() { BgIn = Hex("#3c444d"), BgMid = Hex("#2d343c"), BgOut = Hex("#0e1216"), Vig = 0.72f, VigIn = 0.45f, Grain = 0.13f, Dust = false, Uneven = 0.06f };
}

public sealed class ScopeMode { public string Name; public int Filter; public ScopeMode(string n, int f) { Name = n; Filter = f; } }

/// <summary>Scenariusz = wybrany organizm + jego świat (skala, czas, mikroskop, reguły).</summary>
public abstract class Scenario
{
    public string Id, Name, Latin, TypeName, Size, Desc, Controls, Mag, LoseText = "Twoja linia wymarła";
    public Color Color;
    public float Wd, Ht, ViewW, TimeScale = 1, Bio = 1, PhageDrift = 20, FlowX, FlowY, Starve = 70, EcoliDouble = 3000;
    public int PhageCap = 150;
    public bool Em, GrowEcoli;
    /// <summary>Widoczna zawiesina w wodzie (staw, biofilm).</summary>
    public bool Water;
    /// <summary>
    /// Czy organizmy są zanurzone w swobodnej wodzie (pola przepływu wokół pływaków). W tkance przestrzeń między komórkami
    /// wypełnia żel macierzy zewnątrzkomórkowej (przepływ śródmiąższowy ~0,1–1 µm/s), a na agarze – cienka błonka wody.
    /// </summary>
    public bool Fluid = true;
    /// <summary>Prędkość prądu wody (µm/s czasu biologicznego) i skala wirów (µm).</summary>
    public float Flow, FlowScale = 2000;
    /// <summary>Rozmiar komórki siatki przestrzennej (µm).</summary>
    public float GridCell = 20;
    public string ScoreLabel = "żyjących potomków naraz";
    /// <summary>Średni czas życia neutrofila w tkance (s czasu biologicznego); 0 = bez apoptozy.</summary>
    public float NeutroLife;
    /// <summary>Świat w stanie uśpienia (np. wyschnięty mech) – drobne zwierzęta nie poruszają się.</summary>
    public bool Dormant;
    /// <summary>Nazwa mapy przełączanej klawiszem G (np. „Mapa niedotlenienia”); pusta = brak.</summary>
    public string FieldName = "";
    /// <summary>Natężenie światła PAR w punkcie (µmol fotonów m⁻² s⁻¹); &lt; 0 = scenariusz bez modelu światła.</summary>
    public virtual float Light(World w, float x, float y) => -1;
    /// <summary>Tryb podglądu (F): klucz atlasu dla tła w punkcie – naczynie, ściana, woda, agar… (null = brak).</summary>
    public virtual string InspectAt(World w, float x, float y) => null;

    // ------------------------------------------------ świat nieskończony (chunki)
    /// <summary>Bok chunka (µm); 0 = świat skończony Wd × Ht.</summary>
    public float ChunkSize;
    /// <summary>Promień aktywności w chunkach (2 → 5 × 5 aktywnych chunków).</summary>
    public int ActiveR = 2;
    /// <summary>Maks. liczba zamrożonych (nie należących do gracza) organizmów na chunk.</summary>
    public int ParkCap = 60;
    public string[] BiomeNames = { "Strefa" };
    public Color[] BiomeColors = { new(0.5f, 0.5f, 0.5f) };
    /// <summary>Skala biomów w chunkach (większa = większe obszary).</summary>
    public float BiomeScale = 3.2f;

    public sealed class Goal
    {
        public string Text; public Func<World, float> Val; public float Target;
        public Goal(string text, float target, Func<World, float> val) { Text = text; Target = target; Val = val; }
    }
    public Goal[] Goals;
    public ScopeStyle Style = ScopeStyle.Phase;
    /// <summary>Styl tła dla danego filtra mikroskopu (np. czarne tło w fluorescencji).</summary>
    public virtual ScopeStyle StyleFor(int filter) => Style;
    /// <summary>Materia poza ostrością dla danego filtra (null = brak, np. w fluorescencji widać tylko znakowane komórki).</summary>
    public virtual BgLook LookFor(int filter) => Look;
    /// <summary>Materia poza płaszczyzną ostrości (tło głębi preparatu): wygląd w tym świecie i gęstości w strefach (biomach).</summary>
    public BgLook Look;
    public BgZone[] Zones;
    /// <summary>Gęstości rozogniskowanej materii w punkcie – domyślnie według strefy chunka (bez tworzenia chunków).</summary>
    /// <summary>Test (zrzuty): tło zawsze jak w strefie o tym numerze (MIKRO_BGZONE); −1 = normalnie.</summary>
    public static readonly int ForceZone = int.TryParse(System.Environment.GetEnvironmentVariable("MIKRO_BGZONE"), out int fz) ? fz : -1;
    public virtual BgZone Backdrop(World w, float x, float y)
    {
        if (Zones == null || Zones.Length == 0) return default;
        int b = ForceZone >= 0 ? ForceZone : ChunkSize > 0 ? BiomeAt(w, FloorDiv(x, ChunkSize), FloorDiv(y, ChunkSize)) : 0;
        return Zones[Math.Clamp(b, 0, Zones.Length - 1)];
    }
    /// <summary>
    /// Siatka próbkowania stref tła (liczba pól i ich boki w µm) – domyślnie 16 × 16 pól po pół chunka. Świat o cienkich
    /// warstwach (kora mózgu: strefy po ~20 µm) potrzebuje drobniejszego kroku w pionie.
    /// </summary>
    public virtual (int nx, int ny, float cx, float cy) ZoneGrid { get { float z = (ChunkSize > 0 ? ChunkSize : ViewW) * 0.5f; return (16, 16, z, z); } }
    public ScopeMode[] Modes = LightModes;
    public int DebrisN; public float DebrisS0, DebrisS1, DebrisD; public Color[] DebrisCols;
    public string[] Facts;

    public static readonly ScopeMode[] LightModes = { new("Kontrast fazowy", 5), new("Ciemne pole", 1), new("Jasne pole (bez kontrastu)", 2) };
    public static readonly ScopeMode[] EmModes = { new("TEM – koloryzowany", 0), new("TEM – oryginał (skala szarości)", 3), new("Negatyw (barwienie negatywne)", 4) };

    public abstract void Init(World w);

    /// <summary>Biom chunka – deterministyczny (szum 2D z ziarnem świata). Okolica startu = biom 0.</summary>
    public virtual int BiomeAt(World w, int cx, int cy)
    {
        if (Math.Abs(cx) <= 1 && Math.Abs(cy) <= 1) return 0;
        int nb = BiomeNames.Length, best = 0; float bv = -1;
        for (int k = 0; k < nb; k++)
        {
            float v = Noise2(cx / BiomeScale + k * 17.3f, cy / BiomeScale - k * 9.1f, w.Seed + k * 101) + (k == 0 ? 0.08f : 0);
            if (v > bv) { bv = v; best = k; }
        }
        return best;
    }
    /// <summary>Zawartość chunka przy pierwszym odwiedzeniu.</summary>
    public virtual void GenerateChunk(World w, Chunk c) { }
    public virtual void OnChunkActivated(World w, Chunk c) { }
    public virtual void OnChunkDeactivated(World w, Chunk c) { }

    public virtual void Update(World w, float dt, float gdt) { }
    public virtual void DrawUnder(World w, Painter p, View v) { }
    /// <summary>Na ile niezależnych części (rysowanych równolegle, w tej kolejności) dzieli się tło sceny.</summary>
    public virtual int UnderParts => 1;
    /// <summary>Część tła sceny (domyślnie całe DrawUnder w części 0). Części nie mogą zmieniać stanu świata.</summary>
    public virtual void DrawUnderPart(World w, Painter p, View v, int part) { if (part == 0) DrawUnder(w, p, v); }
    public abstract List<(string, string)> Hud(World w);
    /// <summary>Paski stanu (głód, zdrowie...) w HUD.</summary>
    public virtual List<(string label, float v, Color col)> Bars(World w) => null;
    /// <summary>Etykiety wierszy Hud pokazywanych w minimalnym HUD (najważniejsze odczyty); null = dwa pierwsze.</summary>
    public virtual string[] KeyRows => null;

    float _magX = -1; int _obj = -1;
    /// <summary>
    /// Powiększenie całkowite przy podstawowym przybliżeniu, z opisu Mag: „Obiektyw 40× (400×)” → 400 (obiektyw × okular 10×),
    /// „Obiektyw 10× – …” → 100, „TEM … ≈ 25 000×” → 25 000.
    /// </summary>
    public float MagX { get { if (_magX < 0) ParseMag(); return _magX; } }
    /// <summary>Powiększenie obiektywu (0 – mikroskop elektronowy).</summary>
    public int Objective { get { if (_obj < 0) ParseMag(); return _obj; } }
    void ParseMag()
    {
        string s = Mag ?? "";
        var o = System.Text.RegularExpressions.Regex.Match(s, @"Obiektyw (\d+)×");
        _obj = o.Success ? int.Parse(o.Groups[1].Value) : 0;
        var m = System.Text.RegularExpressions.Regex.Match(s, @"\((\d[\d ]*)×\)");
        if (!m.Success) m = System.Text.RegularExpressions.Regex.Match(s, @"≈\s*(\d[\d ]*)×");
        _magX = m.Success ? float.Parse(m.Groups[1].Value.Replace(" ", ""), System.Globalization.CultureInfo.InvariantCulture) : _obj > 0 ? _obj * 10 : 400;
    }
    static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    /// <summary>Powiększenie w zapisie paska danych mikroskopu elektronowego: X450, X1.25K, X25.0K.</summary>
    public static string MagStr(float m) => m >= 100000 ? "X" + (m / 1000).ToString("0", Inv) + "K" : m >= 10000 ? "X" + (m / 1000).ToString("0.0", Inv) + "K"
        : m >= 1000 ? "X" + (m / 1000).ToString("0.00", Inv) + "K" : "X" + m.ToString("0", Inv);
    /// <summary>Długość podziałki z trzema cyframi znaczącymi: 2.00µm, 20.0µm, 500nm, 1.00mm.</summary>
    public static string LenStr(float um)
    {
        static string F3(float v) => v >= 99.5f ? v.ToString("0", Inv) : v >= 9.95f ? v.ToString("0.0", Inv) : v.ToString("0.00", Inv);
        return um >= 1000 ? F3(um / 1000) + "mm" : um >= 1 ? F3(um) + "µm" : F3(um * 1000) + "nm";
    }
    /// <summary>Kategoria na minimapie / wskaźnikach: 0 brak, 1 cel/pokarm, 2 zagrożenie, 3 sojusznik.</summary>
    public virtual int MapKind(World w, Ent e) => e.Lineage && e != w.Player ? 3 : 0;
    /// <summary>Opcjonalne rysowanie na minimapie (np. naczynie, pole stężeń); p w układzie świata.</summary>
    public virtual void DrawMinimap(World w, Painter p) { }
    /// <summary>Ograniczenia terenu (np. ściany naczynia).</summary>
    public virtual void Constrain(World w, Ent e) { }
    /// <summary>Własne pole przepływu scenariusza (np. przepływ Poiseuille'a w jajowodzie); true = użyte zamiast prądu z funkcji prądu.</summary>
    public virtual bool CustomFlow(World w, float x, float y, out float fx, out float fy) { fx = fy = 0; return false; }
    public bool HasCustomFlow;
    public virtual void DrawOver(World w, Painter p, View v) { }
    /// <summary>Punkt pokarmu dla wskaźnika zmysłu chemicznego (domyślnie najbliższy cel z minimapy).</summary>
    public virtual bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var p = w.Player; if (p == null) return false;
        var e = w.Near(p.X, p.Y, ViewW * 5, o => o != p && MapKind(w, o) == 1);
        if (e == null) return false;
        x = e.X; y = e.Y; return true;
    }
    public bool ThreatPoint(World w, out float x, out float y)
    {
        x = y = 0; var p = w.Player; if (p == null) return false;
        var e = w.Near(p.X, p.Y, ViewW * 2.5f, o => o != p && MapKind(w, o) == 2);
        if (e == null) return false;
        x = e.X; y = e.Y; return true;
    }
    /// <summary>
    /// Czy grany organizm sam się rozmnaża. Neutrofil jest komórką somatyczną, terminalnie zróżnicowaną:
    /// nie dzieli się, a jego geny przechodzą dalej tylko wtedy, gdy przeżyje i rozmnoży się gospodarz.
    /// </summary>
    public bool Reproduces = true;
    /// <summary>Wyjaśnienie dla komórek somatycznych (HUD – krótko, ekran końca – pełniej).</summary>
    public string SomaticShort = "", SomaticNote = "";
    /// <summary>Wiersz HUD „Twoje geny” dla komórek somatycznych (null = stan gospodarza: zakażenie i temperatura).</summary>
    public virtual string SomaticLine(World w) => null;
    /// <summary>Jak ten organizm naprawdę się rozmnaża (pierwszy punkt w oknie wiedzy).</summary>
    public string ReproFact = "";
    /// <summary>Fizyka wspólna dla światów wodnych (okno wiedzy).</summary>
    public static readonly string[] PhysicsFacts =
    {
        "Fizyka pływania: przy liczbie Reynoldsa 10⁻⁵–10⁻² lepkość całkowicie dominuje nad bezwładnością – gdy napęd ustaje, komórka zatrzymuje się na drodze krótszej niż średnica atomu (Purcell, „Life at low Reynolds number”, 1977). Dlatego w grze nie ma rozpędu ani ślizgu.",
        "Pływak sam się napędza, więc wypadkowa siła na wodę jest zerowa: pole przepływu wokół niego to dipol, a nie „ciągnięcie” wody za sobą. Bakterie z wiciami z tyłu („pchacze”, np. E. coli) odpychają wodę przodem i tyłem, a zasysają bokami; Chlamydomonas („ciągacz”) odwrotnie; orzęski zbliżone są do „squirmera” – drobina obok nich zatacza małą pętlę (Drescher i in. 2010, 2011; Lauga i Powers 2009). Widać to po zawiesinie w wodzie.",
        "Ruchy Browna: drobina o promieniu a dyfunduje ze współczynnikiem D = kT/(6πηa) ≈ 0,22/a µm²/s w wodzie 20 °C (Stokes–Einstein), a jej kierunek „rozmywa się” z D_r = kT/(8πηa³). Dlatego bakteria o długości 2 µm po kilku sekundach zapomina, dokąd płynęła, a pantofelek – nie.",
        "Bakterie przy dnie szkiełka pływają po okręgach (zgodnie z ruchem wskazówek zegara, patrząc od strony cieczy): ciało obraca się w jedną stronę, a pęk wici w drugą, i przy ścianie daje to moment skręcający (Lauga i in., Biophys J 2006).",
        "Optyka: w kontraście fazowym obiekty gęstsze optycznie od wody są ciemne i otoczone jasnym halo (artefakt pierścienia fazowego); w ciemnym polu tło jest czarne, a świecą krawędzie i ziarnistości rozpraszające światło; w zwykłym jasnym polu niebarwione komórki są prawie niewidoczne.",
    };
    /// <summary>Jednostka liczebności linii (np. „komórek”, „wirionów”).</summary>
    public string LineUnit = "osobników";
    /// <summary>Jak nazywa się tu „urodzenie” (podział, wyrzut wirionów, wyklucie).</summary>
    public string BirthWord = "podziałów";
    /// <summary>Żyjąca linia gracza (łącznie z zamrożonymi chunkami). Domyślnie: osobniki tego samego typu co założyciel.</summary>
    public virtual int LinePop(World w)
    {
        var t = w.LineType; if (t == null) return 0;
        return w.CountAll(e => e.Lineage && e.GetType() == t && e.CanControl());
    }
    /// <summary>Wynik zapisywany jako rekord: szczyt liczebności żyjącej linii (dostosowanie), a nie „zwycięstwo”.</summary>
    public virtual float Score(World w) => w.LinePeak;
    public virtual bool OnPlayerLost(World w, Ent p) => false;
    public virtual void OnLysis(World w, EColi c, bool byPlayer) { }
    /// <summary>Encja do podglądu w menu + szerokość kadru w µm.</summary>
    public abstract (Ent e, float size) Preview(World w);

    public World Create(bool preview = false)
    {
        var w = new World(this, preview);
        if (!preview && DebrisN > 0)
        {
            if (w.Infinite)
            {
                w.DebrisTile = ViewW * 1.7f;
                int n = (int)Math.Max(40, DebrisN * w.DebrisTile * w.DebrisTile / (Wd * Ht));
                w.MakeDebris(n, DebrisS0, DebrisS1, DebrisD);
            }
            else w.MakeDebris(DebrisN, DebrisS0, DebrisS1, DebrisD);
        }
        return w;
    }

    /// <summary>Wolne miejsce w chunku (sprawdza encje dodane w tym chunku i aktywne w pobliżu).</summary>
    public static (float, float) Spot(World w, Chunk c, float r, List<Ent> local, float margin = 0, int tries = 30)
    {
        float x = 0, y = 0;
        for (int i = 0; i < tries; i++)
        {
            x = c.X0 + Rand(margin, c.S - margin); y = c.Y0 + Rand(margin, c.S - margin);
            bool ok = true;
            foreach (var e in local) if (e.Solid && Hypot(e.X - x, e.Y - y) < e.R + r) { ok = false; break; }
            if (ok && w.Near(x, y, r + c.S * 0.25f, e => e.Solid && Hypot(e.X - x, e.Y - y) < e.R + r) != null) ok = false;
            if (ok) return (x, y);
        }
        return (x, y);
    }
    public static T0 Put<T0>(World w, List<Ent> local, T0 e) where T0 : Ent { w.Add(e); local.Add(e); return e; }
    /// <summary>Losowe miejsce w oknie aktywności, poza widokiem gracza (napływ nowych organizmów).</summary>
    public (float, float) SpawnAway(World w, float r)
    {
        float cx = w.CamX, cy = w.CamY, minD = ViewW * 0.9f;
        for (int i = 0; i < 20; i++)
        {
            float x = w.WinX0 + Rand(r, w.WinW - r), y = w.WinY0 + Rand(r, w.WinH - r);
            if (Hypot(x - cx, y - cy) > minD) return (x, y);
        }
        return (w.WinX0 + r, w.WinY0 + r);
    }
    public string BiomeName(int b) => BiomeNames[Math.Clamp(b, 0, BiomeNames.Length - 1)];
    /// <summary>Nazwa miejsca pod graczem (HUD „Strefa”): domyślnie biom chunku; scenariusz może ją doprecyzować.</summary>
    public virtual string ZoneAt(World w, Ent p) => BiomeName(w.ChunkAt(p.X, p.Y).Biome);

    // ------------------------------------------------ pomocnicze
    public static (float, float) EdgePos(World w, float m)
    {
        int s = RandI(0, 3);
        return s switch { 0 => (Rand(0, w.W), m), 1 => (Rand(0, w.W), w.H - m), 2 => (m, Rand(0, w.H)), _ => (w.W - m, Rand(0, w.H)) };
    }
    public static (float, float) FreeSpot(World w, float r, int tries = 40)
    {
        for (int i = 0; i < tries; i++)
        {
            float x = Rand(r, w.W - r), y = Rand(r, w.H - r); bool ok = true;
            foreach (var e in w.Ents) if (e.Alive && e.Solid && Hypot(e.X - x, e.Y - y) < e.R + r) { ok = false; break; }
            if (ok) return (x, y);
        }
        return (Rand(r, w.W - r), Rand(r, w.H - r));
    }
    protected static string Pct(float v) => $"{MathF.Round(v * 100)}%";
    protected static string N(float v) => ((int)MathF.Round(v)).ToString();

    public static readonly (string, string)[] Sources =
    {
        ("BioNumbers – T4: burst size, okres utajenia", "https://bionumbers.hms.harvard.edu/bionumber.aspx?s=n&v=1&id=106725"),
        ("Fokine i in., PNAS – architektura główki T4", "https://www.pnas.org/doi/10.1073/pnas.0400444101"),
        ("Aksyuk i in., EMBO J – pochewka ogonka T4", "https://link.springer.com/article/10.1038/emboj.2009.36"),
        ("Liza T4: holiny, endolizyna, spaniny (npj Biofilms)", "https://www.nature.com/articles/s41522-021-00195-7"),
        ("Liza eksplozywna i pęcherzyki błonowe (PMC)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC8289217/"),
        ("Bdellovibrio – cykl życiowy, AFM (Biophys J)", "https://www.sciencedirect.com/science/article/pii/S0006349503700617"),
        ("Bdellovibrio – przegląd (FEMS Microbiol Rev)", "https://academic.oup.com/femsre/article/47/6/fuad057/7288655"),
        ("E. coli – run & tumble (J R Soc Interface)", "https://royalsocietypublishing.org/rsif/article/22/227/20250035/235931/Run-and-tumble-dynamics-of-Escherichia-coli-is"),
        ("Fizyka ruchu rzęsek E. coli (PMC)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC5418374/"),
        ("S. aureus – płaszczyzny podziału (Nat Commun)", "https://www.nature.com/articles/ncomms1025"),
        ("Prędkość chemotaksji neutrofili (PubMed)", "https://pubmed.ncbi.nlm.nih.gov/24809064/"),
        ("BioNumbers – wymiary erytrocytu", "https://bionumbers.hms.harvard.edu/bionumber.aspx?id=100798&ver=7"),
        ("Amoeba proteus – mikroreologia, prędkości (arXiv)", "https://arxiv.org/pdf/0710.2510"),
        ("Amoeba proteus – Real Micro Life", "https://realmicrolife.com/amoeba-proteus/"),
        ("Paramecium – \"pływający neuron\" (eNeuro)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC8208649/"),
        ("BioNumbers – prędkość Paramecium", "https://bionumbers.hms.harvard.edu/bionumber.aspx?s=n&v=0&id=110786"),
        ("Didinium nasutum – Micscape", "http://www.microscopy-uk.org.uk/mag/art97/dingley3.html"),
        ("Didinium (Wikipedia)", "https://en.wikipedia.org/wiki/Didinium"),
        ("Chlamydomonas – szybki pływak (DBG)", "https://www.dbg-phykologie.de/en/chlamydomonas-reinhardtii-a-fast-swimmer-serves-as-a-model-organism"),
        ("Ruch ślizgowy okrzemek (PNAS)", "https://www.pnas.org/doi/10.1073/pnas.2426910122"),
        ("Closterium – kryształy w wakuolach (Wikipedia)", "https://en.wikipedia.org/wiki/Closterium"),
        ("Dyfuzja wirusów – Stokes–Einstein (PMC)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC9641563/"),
        ("Brinkmann i in., Science 2004 – NET zabijają bakterie", "https://www.science.org/doi/10.1126/science.1092385"),
        ("Leukocydyna PVL zabija neutrofile (PLOS Pathogens)", "https://journals.plos.org/plospathogens/article?id=10.1371%2Fjournal.ppat.1000715"),
        ("β-laktamy: wybrzuszenie i liza E. coli (Mol Cell 2012)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC3525771/"),
        ("Acanthamoeba – biologia (FEMS Microbiol Rev)", "https://academic.oup.com/femsre/article/30/4/564/2367614"),
        ("Vorticella – skurcz trzonka w ms (PMC)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC5070450/"),
        ("Philodina – wrotek (Lander University)", "https://lanwebs.lander.edu/faculty/rsfox/invertebrates/philodina.html"),
        ("Blepharisma – blefaryzmina, kanibalizm i gigantyzm", "https://en.wikipedia.org/wiki/Blepharisma"),
        ("Coleps hirtus – toksycysty, atak na orzęski (J Eukaryot Microbiol 2014)", "https://onlinelibrary.wiley.com/doi/10.1111/jeu.12106"),
        ("Spirostomum – skurcz w ~5 ms, sieć centryna–Sfi1 (bioRxiv)", "https://www.biorxiv.org/content/10.1101/2024.11.07.622534.full.pdf"),
        ("Lacrymaria olor – szyja do 7–30× długości ciała (PMC)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC7511173/"),
        ("Paramecium bursaria – ~700 symbiotycznych Chlorella", "https://en.wikipedia.org/wiki/Paramecium_bursaria"),
        ("Euglena gracilis – ruch wici, metabolia (PNAS)", "https://www.pnas.org/doi/10.1073/pnas.1708064114"),
        ("Stentor coeruleus – stentoryna, dieta, skurcz", "https://en.wikipedia.org/wiki/Stentor_coeruleus"),
        ("Actinosphaerium – aksopodia i chwytanie ofiar", "https://en.wikipedia.org/wiki/Actinosphaerium"),
        ("Euplotes – chodzenie na cirrach (Curr Biol 2024)", "https://www.cell.com/current-biology/fulltext/S0960-9822(23)01751-7"),
        ("Volvox – rozmiar i pływanie kolonii (arXiv)", "https://arxiv.org/pdf/1803.03542"),
        ("Pływanie C. elegans – ~2 Hz (arXiv)", "https://arxiv.org/pdf/0911.1731"),
        ("Ślizg sinic Oscillatoria (J Phycol)", "https://onlinelibrary.wiley.com/doi/10.1111/j.1529-8817.1971.tb01492.x"),
        ("Daphnia żywi się orzęskami do wielkości pantofelka", "https://www.researchgate.net/publication/228345866_Freshwater_ciliates_as_ecophysiological_model_organisms_-_Lessons_from_Daphnia_major_achievements_and_future_perspectives"),
        ("Pantofelek przeciska się przez wąskie kanały (Sci Rep)", "https://www.nature.com/articles/srep13148"),
        ("OMV jako wabik dla T4 – Manning i Kuehn 2011 (PMC)", "https://www.ncbi.nlm.nih.gov/pmc/articles/PMC3248377/"),
        ("Bodo saltans – żerowanie na bakteriach", "https://onlinelibrary.wiley.com/doi/abs/10.1111/j.1550-7408.1988.tb04327.x"),
        ("Tetrahymena – tempo pobierania cząstek (PMC)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC8988290/"),
        ("Spirillum volutans – rozmiar, pęki rzęsek (microbewiki)", "https://microbewiki.kenyon.edu/index.php/Spirillum_volutans"),
        ("Neutrofile a rozmiar drobnoustroju, NET na strzępki Candida (Nat Immunol 2014)", "https://www.nature.com/articles/ni.2987"),
        ("Rozmiar adipocytów (Am J Physiol)", "https://journals.physiology.org/doi/full/10.1152/ajpregu.00257.2017"),
        ("Loxodes – mikroaerofil osadów, pęcherzyki Müllera", "https://en.wikipedia.org/wiki/Loxodes"),
        ("Spirillum volutans – mikroaerofil wód stojących (Appl Environ Microbiol 1982)", "https://journals.asm.org/doi/pdf/10.1128/aem.43.2.469-477.1982"),
        ("Caulobacter – bakteria wód oligotroficznych (J Bacteriol)", "https://journals.asm.org/doi/full/10.1128/jb.186.24.8254-8266.2004"),
        ("Drożdże w wodach słodkich i biofilmach (J Water Health)", "https://iwaponline.com/jwh/article/18/2/91/73027/Aquatic-yeasts-diversity-characteristics-and"),
        ("Wrotki bdelloidalne – siedliska (microbewiki)", "https://microbewiki.kenyon.edu/index.php/Bdelloid_rotifer"),
        ("Wynaczynienie płytek w stanach zapalnych (Front Immunol)", "https://www.frontiersin.org/journals/immunology/articles/10.3389/fimmu.2026.1764398/full"),
        ("Makrofagi i neutrofile – odmienne tryby migracji w tkance (PubMed 2017)", "https://pubmed.ncbi.nlm.nih.gov/28972134/"),
        ("Makrofagi: migracja mezenchymalna vs ameboidalna (Cancer Immunol Res 2018)", "https://aacrjournals.org/cancerimmunolres/article/6/11/1337/468868/The-Protease-Dependent-Mesenchymal-Migration-of"),
        ("Chemokiny CXCL1/CXCL2 makrofagów i mastocytów ściągają neutrofile (Blood 2013)", "https://ashpublications.org/blood/article-abstract/121/24/4930/31645"),
        ("Fadok i in. 1998 – eferocytoza hamuje cytokiny zapalne (TGF-β, PGE2)", "https://www.jci.org/articles/view/11638"),
        ("Martwica wtórna nieuprzątniętych neutrofili (PMC)", "https://www.ncbi.nlm.nih.gov/pmc/articles/PMC6084545/"),
        ("Eferocytoza neutrofili i zapalenie przewlekłe (Front Immunol 2026)", "https://www.frontiersin.org/journals/immunology/articles/10.3389/fimmu.2026.1835168/full"),
        ("Erytrofagocytoza, oksygenaza hemowa, żelazo (Front Immunol 2017)", "https://www.frontiersin.org/journals/immunology/articles/10.3389/fimmu.2017.00073/full"),
        ("Ucieczka Candida z makrofagów – kandydalizyna (Cell Rep 2022)", "https://www.cell.com/cell-reports/fulltext/S2211-1247(22)01206-2"),
        ("Candida wykorzystuje piroptozę do ucieczki z makrofagów (mBio 2014)", "https://journals.asm.org/doi/10.1128/mbio.00003-14"),
        ("Euglena – fotoaktywowana cyklaza adenylanowa steruje fototaksją (Plant Physiol 2003)", "https://academic.oup.com/plphys/article-abstract/133/4/1517/6103414"),
        ("Euglena – fototaksja dodatnia i ujemna zależnie od natężenia (PNAS 2015)", "https://www.pnas.org/doi/10.1073/pnas.1422922112"),
        ("Euglena – fotoinhibicja i reaktywne formy tlenu (Planta)", "https://link.springer.com/article/10.1007/BF00195688"),
        ("Euglena – wzrost i skład komórek w różnych warunkach, paramylon (PLOS One 2018)", "https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0195329"),
        ("Reguła Liebermeistera – tętno a temperatura (LITFL)", "https://litfl.com/liebermeister-rule/"),
        ("Zmienność rytmu serca a rokowanie w sepsie (PLOS One 2018)", "https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0203487"),
        ("Tony serca S1/S2 – czas trwania i pasmo (Complexity 2017)", "https://onlinelibrary.wiley.com/doi/10.1155/2017/1580414"),
        ("Prędkość krwinek w mikrokrążeniu (Fiber Optical Anemometry)", "https://www.tandfonline.com/doi/full/10.3109/10731191003670533"),
        ("Noselli i in. 2019 – metabolia jako pełzanie w ciasnocie (arXiv)", "https://arxiv.org/pdf/2005.11952"),
        // --- fizyka pływania, dyfuzja, optyka
        ("Purcell 1977 – Life at low Reynolds number (Am J Phys)", "https://doi.org/10.1119/1.10903"),
        ("Lauga i Powers 2009 – hydrodynamika pływających mikroorganizmów (Rep Prog Phys)", "https://doi.org/10.1088/0034-4885/72/9/096601"),
        ("Drescher i in. 2011 – pole przepływu wokół E. coli, dipol „pchacza” (PNAS)", "https://doi.org/10.1073/pnas.1019079108"),
        ("Drescher i in. 2010 – przepływ wokół Volvox i Chlamydomonas (PRL)", "https://doi.org/10.1103/PhysRevLett.105.168101"),
        ("Guasto i in. 2010 – Chlamydomonas jako „ciągacz” (PRL)", "https://doi.org/10.1103/PhysRevLett.105.168102"),
        ("Lauga i in. 2006 – bakterie pływają po okręgach przy ścianie (Biophys J)", "https://doi.org/10.1529/biophysj.105.069401"),
        ("Berg 1993 – Random Walks in Biology (Princeton UP): dyfuzja Stokesa–Einsteina, D_r E. coli", "https://press.princeton.edu/books/paperback/9780691000640/random-walks-in-biology"),
        ("Zernike 1942 – kontrast fazowy (Physica)", "https://doi.org/10.1016/S0031-8914(42)80035-X"),
        ("Otaki 2000 – artefakt halo w kontraście fazowym (Optical Review)", "https://doi.org/10.1007/s10043-000-0119-5"),
        // --- rozmnażanie i cykle życiowe
        ("Taheri-Araghi i in. 2015 – reguła „adder” u bakterii (Curr Biol)", "https://doi.org/10.1016/j.cub.2014.12.009"),
        ("Fenton i in. 2010 – nieparzysta liczba potomków Bdellovibrio w bdeloplaście (J Bacteriol)", "https://doi.org/10.1128/jb.00914-10"),
        ("Sonneborn 1954 – autogamia, starzenie i odmładzanie klonów Paramecium (J Protozool)", "https://doi.org/10.1111/j.1550-7408.1954.tb00792.x"),
        ("Butzel i Horwitz 1965 – ekscystacja Didinium nasutum (J Protozool)", "https://doi.org/10.1111/j.1550-7408.1965.tb03234.x"),
        ("Beers 1946 – ekscystacja Didinium nasutum a bakterie (J Exp Zool)", "https://doi.org/10.1002/jez.1401030202"),
        ("Hashimoto i in. 2013 – makrofagi tkankowe odnawiają się lokalnie (Immunity)", "https://doi.org/10.1016/j.immuni.2013.04.004"),
        ("Gerlach i in. 2021 – eferocytoza pobudza proliferację makrofagów (Cell Metab)", "https://doi.org/10.1016/j.cmet.2021.10.015"),
        ("Summers i in. 2010 – kinetyka i czas życia neutrofili (Trends Immunol)", "https://doi.org/10.1016/j.it.2010.05.006"),
        // --- DNA (tło menu)
        ("Watson i Crick 1953 – struktura kwasu deoksyrybonukleinowego: podwójna helisa (Nature)", "https://doi.org/10.1038/171737a0"),
        ("Wing i in. 1980 – krystalografia pełnego skrętu B-DNA (Nature)", "https://doi.org/10.1038/287755a0"),
        ("Wang 1979 – skok helisy DNA w roztworze: 10,4 pary zasad na skręt (PNAS)", "https://doi.org/10.1073/pnas.76.1.200"),
        // --- liczebność organizmów w polu widzenia
        ("O'Toole, Kaplan i Kolter 2000 – tworzenie biofilmu: przyczepione komórki i mikrokolonie (Annu Rev Microbiol)", "https://doi.org/10.1146/annurev.micro.54.1.49"),
        ("Hall-Stoodley, Costerton i Stoodley 2004 – biofilmy bakteryjne w środowisku i zakażeniach (Nat Rev Microbiol)", "https://doi.org/10.1038/nrmicro821"),
        ("Wilkinson i in. 1992 – liczba mastocytów w skórze właściwej, ~50/mm² w prawidłowej skórze (J Cutan Pathol)", "https://onlinelibrary.wiley.com/doi/10.1111/j.1600-0560.1992.tb00607.x"),
        ("Korosec i in. 2019 – fibroblasty warstwy brodawkowej i siateczkowatej skóry (J Invest Dermatol)", "https://www.sciencedirect.com/science/article/pii/S0022202X18325569"),
        ("Lukashanets, Hihiniak i Miamin 2022 – bardzo liczne wrotki i niesporczaki w kożuchach glonów (Polar Research)", "https://doi.org/10.33265/polar.v41.7781"),
        ("Mäenpää, Elo i Calhim 2024 – niesporczaki w mchach torfowisk: 84–321 osobników na gram mchu (Ecol Evol)", "https://doi.org/10.1002/ece3.70045"),
        // --- plemnik i zapłodnienie
        ("Suarez i Pacey 2006 – transport plemników w drogach rodnych samicy (Hum Reprod Update)", "https://doi.org/10.1093/humupd/dmi047"),
        ("Suarez 2008 – rezerwuar plemników i ich ruch w jajowodzie (Int J Dev Biol)", "https://doi.org/10.1387/ijdb.072527ss"),
        ("Miki i Clapham 2013 – reotaksja plemników ssaków (Curr Biol)", "https://doi.org/10.1016/j.cub.2013.02.007"),
        ("Kantsler i in. 2014 – reotaksja: pływanie pod prąd wzdłuż powierzchni (eLife)", "https://doi.org/10.7554/eLife.02403"),
        ("Elgeti, Kaupp i Gompper 2010 – hydrodynamika plemników przy powierzchniach (Biophys J)", "https://doi.org/10.1016/j.bpj.2010.05.015"),
        ("Denissenko i in. 2012 – plemniki człowieka podążają wzdłuż krawędzi mikrokanałów (PNAS)", "https://doi.org/10.1073/pnas.1202934109"),
        ("Gaffney i in. 2011 – ruch plemników ssaków: obserwacje i teoria (Annu Rev Fluid Mech)", "https://doi.org/10.1146/annurev-fluid-121108-145442"),
        ("Ho i Suarez 2001 – hiperaktywacja plemników (Reproduction)", "https://doi.org/10.1530/rep.0.1220519"),
        ("Bahat i in. 2003 – termotaksja plemników (Nat Med)", "https://doi.org/10.1038/nm0203-149"),
        ("Lishko i in. 2011 – progesteron aktywuje kanał CatSper plemników człowieka (Nature)", "https://doi.org/10.1038/nature09767"),
        ("Strünker i in. 2011 – CatSper i napływ Ca²⁺ wywołany progesteronem (Nature)", "https://doi.org/10.1038/nature09769"),
        ("Jin i in. 2011 – reakcja akrosomalna zaczyna się przed osłonką przejrzystą (PNAS)", "https://doi.org/10.1073/pnas.1018202108"),
        ("Bianchi i in. 2014 – JUNO, receptor IZUMO1 na komórce jajowej (Nature)", "https://doi.org/10.1038/nature13203"),
        ("Burkart i in. 2012 – owastacyna tnie ZP2 i blokuje polispermię (J Cell Biol)", "https://doi.org/10.1083/jcb.201112094"),
        ("Alghamdi i Foster 2005 – DNaza nasienia uwalnia plemniki z sieci NET (Biol Reprod)", "https://doi.org/10.1095/biolreprod.105.045666"),
        ("Wilcox i in. 1995 – współżycie a owulacja: okno płodne (NEJM)", "https://doi.org/10.1056/NEJM199512073332301"),
        ("WHO 2021 – podręcznik badania nasienia (wymiary plemnika, ruchliwość)", "https://www.who.int/publications/i/item/9789240030787"),
        ("Mortimer 1997 – ruch plemników ssaków i jego analiza (VCL, VSL, ALH) (Hum Reprod Update)", "https://doi.org/10.1093/humupd/3.5.403"),
        ("Woolley 2003 – ruch plemników przy powierzchniach (Reproduction)", "https://doi.org/10.1530/rep.0.1260259"),
        ("Smith i in. 2009 – bicie wici plemników człowieka a lepkość (Cell Motil Cytoskeleton)", "https://doi.org/10.1002/cm.20345"),
        ("Kunz i in. 1996 – szybki transport plemników: perystaltyka macicy (Hum Reprod)", "https://doi.org/10.1093/humrep/11.3.627"),
        ("Fox, Meldrum i Watson 1973 – pH pochwy podczas stosunku: z 4,3 do 7,2 w 8 s (J Reprod Fertil)", "https://rep.bioscientifica.com/view/journals/rep/33/1/jrf_33_1_008.xml"),
        ("Boskey i in. 1999 – pałeczki kwasu mlekowego i tempo zakwaszania pochwy (Infect Immun)", "https://pmc.ncbi.nlm.nih.gov/articles/PMC96867/"),
        ("Pandya i Cohen 1985 – napływ leukocytów do szyjki macicy po unasiennieniu (Fertil Steril)", "https://www.sciencedirect.com/science/article/pii/S0015028216484426"),
        // --- tkanka: przeciskanie się leukocytów przez kolagen
        ("Lämmermann i in. 2008 – leukocyty przeciskają się przez tkankę bez integryn (Nature)", "https://doi.org/10.1038/nature06887"),
        ("Abercrombie i Heaysman 1954 – hamowanie kontaktowe ruchu fibroblastów (Exp Cell Res)", "https://doi.org/10.1016/0014-4827(54)90176-7"),
        ("Dunn i Heath 1976 – prowadzenie kontaktowe komórek tkankowych (Exp Cell Res)", "https://doi.org/10.1016/0014-4827(76)90405-5"),
        ("Nirody i in. 2021 – koordynacja nóg niesporczaków przy różnych prędkościach (PNAS)", "https://doi.org/10.1073/pnas.2107289118"),
        ("Renkawitz i in. 2019 – jądro wybiera drogę najmniejszego oporu (Nature)", "https://doi.org/10.1038/s41586-019-1087-5"),
        ("Wolf i in. 2013 – fizyczne granice migracji: oczka macierzy a odkształcalność jądra (J Cell Biol)", "https://doi.org/10.1083/jcb.201210152"),
        // --- rekrutacja neutrofili, rojenie, naczynia włosowate
        ("Ley i in. 2007 – kaskada adhezji leukocytów: toczenie, zatrzymanie, diapedeza (Nat Rev Immunol)", "https://doi.org/10.1038/nri2156"),
        ("Kolaczkowska i Kubes 2013 – rekrutacja i funkcje neutrofili (Nat Rev Immunol)", "https://doi.org/10.1038/nri3399"),
        ("Liese i in. 2013 – mikroskopia przyżyciowa ropnia gronkowcowego w skórze (Cell Microbiol)", "https://doi.org/10.1111/cmi.12085"),
        ("Lämmermann i in. 2013 – roje neutrofili wymagają LTB4 i integryn (Nature)", "https://doi.org/10.1038/nature12175"),
        ("Kienle i in. 2021 – neutrofile same wyciszają rój, co pomaga zamknąć bakterie (Science)", "https://doi.org/10.1126/science.abe7729"),
        ("Carmeliet i Jain 2000 – angiogeneza; komórki leżą w zasięgu 100–200 µm od naczyń (Nature)", "https://doi.org/10.1038/35025220"),
        // --- komórka nowotworowa
        ("Hanahan i Weinberg 2011 – cechy nowotworów: następna generacja (Cell)", "https://doi.org/10.1016/j.cell.2011.02.013"),
        ("Nowell 1976 – ewolucja klonalna populacji komórek nowotworowych (Science)", "https://doi.org/10.1126/science.959840"),
        ("Greaves i Maley 2012 – ewolucja klonalna w nowotworach (Nature)", "https://doi.org/10.1038/nature10762"),
        ("Thomlinson i Gray 1955 – martwica ~150–200 µm od naczyń w raku płuca (Br J Cancer)", "https://doi.org/10.1038/bjc.1955.55"),
        ("Graeber i in. 1996 – niedotlenienie wybiera komórki bez apoptozy zależnej od p53 (Nature)", "https://doi.org/10.1038/379088a0"),
        ("Hammond i Giaccia 2005 – p53 w apoptozie wywołanej niedotlenieniem: dopiero ciężka hipoksja/anoksja (Biochem Biophys Res Commun)", "https://doi.org/10.1016/j.bbrc.2005.03.154"),
        ("Vaupel 2004 – mikrośrodowisko guza: niedotlenienie, anoksja, kwasica (Semin Radiat Oncol)", "https://doi.org/10.1016/j.semradonc.2004.04.008"),
        ("Yang i in. 2008 – HIF-1α bezpośrednio reguluje TWIST i przerzuty (Nat Cell Biol)", "https://doi.org/10.1038/ncb1691"),
        ("Folkman 1971 – angiogeneza nowotworowa (N Engl J Med)", "https://doi.org/10.1056/NEJM197111182852108"),
        ("Gimbrone i in. 1974 – wzrost guza i neowaskularyzacja w rogówce królika (J Natl Cancer Inst)", "https://doi.org/10.1093/jnci/52.2.413"),
        ("Wolf i in. 2003 – przejście mezenchymalno-ameboidalne po zablokowaniu proteolizy (J Cell Biol)", "https://doi.org/10.1083/jcb.200209006"),
        ("Sabeh i in. 2004 – MT1-MMP otwiera komórkom nowotworowym drogę przez kolagen (J Cell Biol)", "https://doi.org/10.1083/jcb.200408028"),
        ("Friedl i Wolf 2003 – inwazja i migracja komórek nowotworowych (Nat Rev Cancer)", "https://doi.org/10.1038/nrc1075"),
        ("Wyckoff i in. 2004 – pętla parakrynna makrofag–komórka raka (EGF/CSF-1) (Cancer Res)", "https://doi.org/10.1158/0008-5472.CAN-04-1449"),
        ("Harney i in. 2015 – TMEM: przepuszczalność naczyń i intrawazacja (Cancer Discov)", "https://doi.org/10.1158/2159-8290.CD-15-0012"),
        ("Luzzi i in. 1998 – wieloetapowa nieefektywność przerzutowania (Am J Pathol)", "https://doi.org/10.1016/S0002-9440(10)65628-3"),
        ("Chambers i in. 2002 – rozsiew i wzrost komórek nowotworowych w miejscach przerzutów (Nat Rev Cancer)", "https://doi.org/10.1038/nrc865"),
        ("Nieswandt i in. 1999 – płytki krwi chronią komórki nowotworowe przed komórkami NK (Cancer Res)", "https://pubmed.ncbi.nlm.nih.gov/10096562/"),
        ("Palumbo i in. 2005 – płytki i fibryna utrudniają eliminację przez komórki NK (Blood)", "https://doi.org/10.1182/blood-2004-06-2272"),
        ("Labelle i in. 2011 – płytki indukują przejście podobne do EMT i przerzuty (Cancer Cell)", "https://doi.org/10.1016/j.ccr.2011.09.009"),
        ("Ljunggren i Kärre 1990 – „brak swojego”: MHC a rozpoznanie przez komórki NK (Immunol Today)", "https://doi.org/10.1016/0167-5699(90)90097-S"),
        ("Freeman i in. 2000 – PD-L1 hamuje limfocyty przez PD-1 (J Exp Med)", "https://doi.org/10.1084/jem.192.7.1027"),
        ("Weigelin i in. 2021 – addytywna cytotoksyczność limfocytów T (Nat Commun)", "https://doi.org/10.1038/s41467-021-25282-3"),
        ("Halle i in. 2016 – ograniczona zdolność zabijania limfocytów T in vivo (Immunity)", "https://pubmed.ncbi.nlm.nih.gov/26872694/"),
        ("Ocaña i in. 2012 – kolonizacja przerzutowa wymaga wyciszenia EMT (Prrx1) (Cancer Cell)", "https://doi.org/10.1016/j.ccr.2012.10.012"),
        ("Tsai i in. 2012 – przestrzenno-czasowa regulacja EMT a przerzuty (Cancer Cell)", "https://doi.org/10.1016/j.ccr.2012.09.022"),
        ("Giese i in. 2003 – koszt migracji: „idź albo rośnij” (J Clin Oncol)", "https://doi.org/10.1200/JCO.2003.05.063"),
        ("Padera i in. 2004 – komórki raka ściskają naczynia w guzie (Nature)", "https://doi.org/10.1038/427695a"),
        ("Jain 2005 – normalizacja naczyń guza; naczynia kręte i nieszczelne (Science)", "https://doi.org/10.1126/science.1104819"),
        ("Kalluri 2016 – fibroblasty w nowotworach (CAF) (Nat Rev Cancer)", "https://doi.org/10.1038/nrc.2016.73"),
        ("Provenzano i in. 2006 – przebudowa kolagenu na granicy guza (TACS) (BMC Med)", "https://doi.org/10.1186/1741-7015-4-38"),
        ("Conklin i in. 2011 – uporządkowany kolagen a rokowanie w raku piersi (Am J Pathol)", "https://doi.org/10.1016/j.ajpath.2010.11.076"),
        ("Qian i Pollard 2010 – makrofagi w progresji guza i przerzutach (Cell)", "https://doi.org/10.1016/j.cell.2010.03.014"),
        ("Salmon i in. 2012 – architektura macierzy zatrzymuje limfocyty T w zrębie guza (J Clin Invest)", "https://doi.org/10.1172/JCI45817"),
        ("Joyce i Fearon 2015 – wykluczenie limfocytów T z guza (Science)", "https://doi.org/10.1126/science.aaa6204"),
        ("Gaggioli i in. 2007 – inwazja zbiorowa prowadzona przez fibroblasty (Nat Cell Biol)", "https://doi.org/10.1038/ncb1658"),
        ("Friedl i in. 2012 – klasyfikacja zbiorowej inwazji komórek raka (Nat Cell Biol)", "https://doi.org/10.1038/ncb2548"),
        ("Aceto i in. 2014 – skupiska krążących komórek raka a przerzuty (Cell)", "https://doi.org/10.1016/j.cell.2014.07.013"),
        // --- świdrowiec Trypanosoma brucei
        ("Broadhead i in. 2006 – ruch wici jest niezbędny do przeżycia świdrowca we krwi (Nature)", "https://doi.org/10.1038/nature04541"),
        ("Heddergott i in. 2012 – ruch świdrowca jako przystosowanie do tłoku krwi (PLoS Pathog)", "https://doi.org/10.1371/journal.ppat.1003023"),
        ("Engstler i in. 2007 – opływ usuwa przeciwciała z płaszcza VSG (Cell)", "https://doi.org/10.1016/j.cell.2007.08.046"),
        ("Mugnier i in. 2015 – dynamika zmienności antygenowej in vivo (Science)", "https://doi.org/10.1126/science.aaa4502"),
        ("Ross i Thomson 1910 – okresowe fale parazytemii w śpiączce afrykańskiej (Proc R Soc B)", "https://doi.org/10.1098/rspb.1910.0035"),
        ("Macaskill i in. 1980 – usuwanie świdrowców z krwi u odpornych myszy (Immunology)", "https://pubmed.ncbi.nlm.nih.gov/7429544/"),
        ("Liu i in. 2019 – CRIg komórek Kupffera wyłapuje pasożyty krwi (PNAS)", "https://pubmed.ncbi.nlm.nih.gov/31723045/"),
        ("Vassella i in. 1997 – wyczuwanie gęstości steruje różnicowaniem świdrowców (J Cell Sci)", "https://doi.org/10.1242/jcs.110.21.2661"),
        ("Rojas i in. 2019 – oligopeptydy i TbGPR89: wyczuwanie gęstości (Cell)", "https://doi.org/10.1016/j.cell.2018.10.041"),
        ("Dean i in. 2009 – PAD1 i sygnał różnicowania świdrowca (Nature)", "https://doi.org/10.1038/nature07997"),
        ("MacGregor i in. 2012 – unikanie odporności, przewlekłość i przenoszenie (Nat Rev Microbiol)", "https://doi.org/10.1038/nrmicro2779"),
        ("Capewell i in. 2016 – skóra jako rezerwuar świdrowców (eLife)", "https://doi.org/10.7554/eLife.17716"),
        ("Trindade i in. 2016 – świdrowce w tkance tłuszczowej (Cell Host Microbe)", "https://doi.org/10.1016/j.chom.2016.05.002"),
        ("Schuster i in. 2021 – nieoczekiwana plastyczność cyklu życiowego T. brucei (eLife)", "https://doi.org/10.7554/eLife.66028"),
        // --- neuron kory mózgu (mysz): neurogeneza, wędrówka, akson spoidłowy, naturalna śmierć neuronów
        ("Yoshinaga i in. 2021 – profile wędrówki neuronów kory w znakowaniu FlashTag (iScience)", "https://doi.org/10.1016/j.isci.2021.102277"),
        ("Govindan, Oberst i Jabaudon 2018 – FlashTag: znakowanie izochronicznych kohort komórek (Nat Protoc)", "https://doi.org/10.1038/s41596-018-0038-1"),
        ("Tonosaki i in. 2023 – grubość kory myszy w E16,5 ≈ 482 µm (PLoS One)", "https://doi.org/10.1371/journal.pone.0294893"),
        ("Takahashi, Nowakowski i Caviness 1995 – cykl komórkowy strefy komorowej: 8,1 → 18,4 h (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.15-09-06046.1995"),
        ("Noctor i in. 2004 – neurony z podziałów symetrycznych w SVZ i fazy wędrówki (Nat Neurosci)", "https://doi.org/10.1038/nn1172"),
        ("Kowalczyk i in. 2009 – progenitory pośrednie wytwarzają neurony wszystkich warstw (Cereb Cortex)", "https://doi.org/10.1093/cercor/bhn260"),
        ("Arai i in. 2011 – długości faz cyklu progenitorów szczytowych i pośrednich (Nat Commun)", "https://doi.org/10.1038/ncomms1155"),
        ("Tabata i Nakajima 2003 – wędrówka wielobiegunowa ~4,4 µm/h (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.23-31-09996.2003"),
        ("Namba i in. 2014 – aksony pionierskie z TAG-1 wyznaczają akson neuronu (Neuron)", "https://doi.org/10.1016/j.neuron.2013.12.015"),
        ("Hatanaka i Murakami 2002 – wypustka tylna wędrującego neuronu staje się aksonem (J Comp Neurol)", "https://doi.org/10.1002/cne.10421"),
        ("Ohtaka-Maruyama i in. 2018 – synapsy neuronów podpłytki przełączają wędrówkę (Science)", "https://doi.org/10.1126/science.aar2866"),
        ("Rakic 1972 – wędrówka neuronów po włóknach gleju promienistego (J Comp Neurol)", "https://doi.org/10.1002/cne.901450105"),
        ("Nadarajah i in. 2001 – lokomocja i translokacja somy w korze (Nat Neurosci)", "https://doi.org/10.1038/83967"),
        ("Sekine i in. 2011 – translokacja końcowa w strefie PCZ (~50 µm) (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.0650-11.2011"),
        ("Angevine i Sidman 1961 – kora powstaje od środka na zewnątrz (Nature)", "https://doi.org/10.1038/192766b0"),
        ("Schmechel i Rakic 1979 – glej promienisty przekształca się w astrocyty (Anat Embryol)", "https://doi.org/10.1007/BF00300010"),
        ("Vasudevan i in. 2008 – sploty naczyń zarodkowej kory (Nat Neurosci)", "https://doi.org/10.1038/nn2074"),
        ("Yanagida i in. 2012 – interneurony w żywym zarodku: 12 ± 6 µm/h (PNAS)", "https://doi.org/10.1073/pnas.1209166109"),
        ("Hattori i Miyata 2018 – mikroglej w ścianie mózgu zarodka a elektroporacja (eNeuro)", "https://doi.org/10.1523/ENEURO.0312-18.2018"),
        ("Cunningham, Martínez-Cerdeño i Noctor 2013 – mikroglej fagocytuje progenitory (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.3441-12.2013"),
        ("Squarzoni i in. 2014 – mikroglej a połączenia w przodomózgowiu zarodka (Cell Rep)", "https://doi.org/10.1016/j.celrep.2014.07.042"),
        ("Koester i O'Leary 1994 – aksony kory obręczy pionierami ciała modzelowatego (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.14-11-06608.1994"),
        ("Rash i Richards 2001 – aksony z kory obręczy przechodzą linię środkową w E15,5 (J Comp Neurol)", "https://doi.org/10.1002/cne.1170"),
        ("Shu i Richards 2001 – klin glejowy i Slit2 prowadzą aksony ciała modzelowatego (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.21-08-02749.2001"),
        ("Fothergill i in. 2014 – netryna-1/DCC a przejście linii środkowej (Cereb Cortex)", "https://doi.org/10.1093/cercor/bhs395"),
        ("Gobius i in. 2016 – astrocyty przebudowują szczelinę międzypółkulową (Cell Rep)", "https://doi.org/10.1016/j.celrep.2016.09.033"),
        ("Halloran i Kalil 1994 – zachowanie stożków wzrostu w ciele modzelowatym (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.14-04-02161.1994"),
        ("Hutchins, Li i Kalil 2011 – ~37 µm/h przed i ~55 µm/h za linią środkową (Dev Neurobiol)", "https://doi.org/10.1002/dneu.20846"),
        ("Wang i in. 2007 – rozwój projekcji spoidłowych warstwy II/III (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.3380-07.2007"),
        ("Mizuno, Hirano i Tagawa 2007 – połączenia międzypółkulowe kory wzrokowej (J Neurosci)", "https://doi.org/10.1523/JNEUROSCI.1215-07.2007"),
        ("Wong i in. 2018 – fale śmierci: ~12% neuronów pobudzających w P2–P5 (Nature)", "https://doi.org/10.1038/s41586-018-0139-6"),
        ("Blanquie i in. 2017 – aktywność elektryczna a apoptoza neuronów kory (eLife)", "https://doi.org/10.7554/eLife.27696"),
        ("Wong i Marín 2019 – naturalna śmierć komórek w korze (Annu Rev Cell Dev Biol)", "https://doi.org/10.1146/annurev-cellbio-100818-125204"),
        ("Southwell i in. 2012 – śmierć interneuronów zdeterminowana wewnętrznie (Nature)", "https://doi.org/10.1038/nature11523"),
        ("Yu i in. 2009 – siostrzane neurony częściej łączą się synapsami (Nature)", "https://doi.org/10.1038/nature07722"),
    };

    public static readonly Scenario[] All = { new ScT4(), new ScBdello(), new ScEcoli(), new ScNeutro(), new ScMacro(), new ScPara(), new ScDidinium(), new ScAmoeba(), new ScEuglena(), new ScTardi(), new ScDicty(), new ScSperm(), new ScCancer(), new ScTryp(), new ScNeuron() };
}

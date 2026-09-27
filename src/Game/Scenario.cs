namespace Mikro;

/// <summary>Styl tła mikroskopu (przekazywany do shadera).</summary>
public sealed class ScopeStyle
{
    public Color BgIn, BgMid, BgOut; public float Vig, VigIn, Grain, Uneven; public bool Dust;
    public static readonly ScopeStyle Phase = new() { BgIn = Hex("#c9ccb9"), BgMid = Hex("#b0b4a2"), BgOut = Hex("#6a6f60"), Vig = 0.62f, VigIn = 0.5f, Grain = 0.07f, Dust = true, Uneven = 0.04f };
    public static readonly ScopeStyle Tissue = new() { BgIn = Hex("#cdc8b6"), BgMid = Hex("#b6af9d"), BgOut = Hex("#6c6657"), Vig = 0.62f, VigIn = 0.5f, Grain = 0.07f, Dust = true, Uneven = 0.04f };
    public static readonly ScopeStyle Pond = new() { BgIn = Hex("#c6ccb3"), BgMid = Hex("#abb298"), BgOut = Hex("#5f6852"), Vig = 0.66f, VigIn = 0.48f, Grain = 0.07f, Dust = true, Uneven = 0.05f };
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
    /// <summary>Prędkość prądu wody (µm/s czasu biologicznego) i skala wirów (µm).</summary>
    public float Flow, FlowScale = 2000;
    /// <summary>Rozmiar komórki siatki przestrzennej (µm).</summary>
    public float GridCell = 20;
    public string ScoreLabel = "wynik";

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
    public ScopeMode[] Modes = LightModes;
    public int DebrisN; public float DebrisS0, DebrisS1, DebrisD; public Color[] DebrisCols;
    public string[] Facts;

    public static readonly ScopeMode[] LightModes = { new("Kontrast fazowy", 0), new("Ciemne pole", 1), new("Jasne pole (bez kontrastu)", 2) };
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
    public abstract List<(string, string)> Hud(World w);
    /// <summary>Paski stanu (głód, zdrowie...) w HUD.</summary>
    public virtual List<(string label, float v, Color col)> Bars(World w) => null;
    /// <summary>Kategoria na minimapie / wskaźnikach: 0 brak, 1 cel/pokarm, 2 zagrożenie, 3 sojusznik.</summary>
    public virtual int MapKind(World w, Ent e) => e.Lineage && e != w.Player ? 3 : 0;
    /// <summary>Opcjonalne rysowanie na minimapie (np. naczynie, pole stężeń); p w układzie świata.</summary>
    public virtual void DrawMinimap(World w, Painter p) { }
    /// <summary>Ograniczenia terenu (np. ściany naczynia).</summary>
    public virtual void Constrain(World w, Ent e) { }
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
    /// <summary>Wynik zapisywany jako rekord.</summary>
    public virtual float Score(World w) => 0;
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
    };

    public static readonly Scenario[] All = { new ScT4(), new ScBdello(), new ScEcoli(), new ScNeutro(), new ScPara(), new ScDidinium(), new ScAmoeba() };
}

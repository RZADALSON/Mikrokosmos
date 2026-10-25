using System.Linq;
namespace Mikro;

// =====================================================================
//  Neuron kory mózgu – od podziału przy komorze do połączenia z drugą półkulą
//  (mysz, neuron piramidowy warstwy II/III urodzony w E15,5; przekrój czołowy kory grzbietowej)
// =====================================================================
public sealed class ScNeuron : Scenario
{
    public CortexField F;
    /// <summary>Neuron gracza (także wtedy, gdy steruje się stożkiem wzrostu jego aksonu) i jego siostra z tego samego podziału.</summary>
    public CortNeuron Me, SisterN;
    /// <summary>Środek miejsca elektroporacji (x) – tam świecą wyznakowane komórki gleju promienistego.</summary>
    public float SiteX;
    /// <summary>Kohorta wyznakowana GFP (elektroporacja in utero w E15,5): gracz, siostra i neurony z sąsiednich progenitorów.</summary>
    public readonly List<CortNeuron> Cohort = new();
    /// <summary>Wiek zarodka/młodej myszy w dniach od zapłodnienia (E); poród w E19,5 = P0.</summary>
    public float Age => F?.Age ?? 15.5f;
    public const float Birth = 19.5f;
    /// <summary>Włókna gleju promienistego są jeszcze dostępne do wędrówki (po urodzeniu glej przekształca się w astrocyty).</summary>
    public bool FibersPresent => Age < Birth + 4;
    /// <summary>Komórki pola w apoptozie (piknoza) – do pochłonięcia przez mikroglej.</summary>
    public struct Dying { public int Id; public float X, Y, W, Asp, Ang, T0; public CortexField.CT T; public bool Eaten; }
    public readonly List<Dying> Dead = new();
    public int DeadCount, DeadPost;
    /// <summary>Liczba neuronów płytki korowej i podpłytki oraz komórek stref proliferacyjnych w oknie ±520 µm (do tempa apoptozy).</summary>
    public int CpCount = 2700, ProgCount = 4500;
    float _deathAcc, _embAcc, _popT, _survT = -1, _winT = -1;
    bool _p2, _fate, _branch;
    public bool FateDone => _fate;
    /// <summary>Test: bez losowania śmierci gracza w oknie P2–P5.</summary>
    public bool NoFate;
    /// <summary>Test: bez rozwidlenia wyniku (ryzyko nadpisane).</summary>
    public float RiskOverride = -1;

    public static readonly ScopeStyle Dic = new() { BgIn = Hex("#b7b6b0"), BgMid = Hex("#a5a49e"), BgOut = Hex("#5a5955"), Vig = 0.6f, VigIn = 0.5f, Grain = 0.06f, Dust = true, Uneven = 0.035f };
    public static readonly ScopeStyle Fluo = new() { BgIn = Hex("#0b100c"), BgMid = Hex("#070a08"), BgOut = Hex("#010201"), Vig = 0.66f, VigIn = 0.45f, Grain = 0.1f, Dust = false, Uneven = 0.02f };
    public override ScopeStyle StyleFor(int filter) => filter == 7 ? Fluo : filter == 2 ? ScopeStyle.Tissue : Dic;
    public override BgLook LookFor(int filter) => filter == 7 ? null : Look;

    public ScNeuron()
    {
        Id = "neuron"; Name = "Młody neuron kory"; Latin = "Mus musculus – neuron piramidowy warstwy II/III"; TypeName = "Komórka somatyczna (neuron)"; Size = "≈ 9 µm → akson 2–3 mm"; Color = Hex("#9ad8a4");
        Desc = "Rodzisz się w E15,5 przy komorze mózgu zarodka myszy. Wędruj po włóknach gleju promienistego na szczyt płytki korowej, poprowadź akson przez ciało modzelowate do drugiej półkuli i przetrwaj falę naturalnej śmierci neuronów.";
        ViewW = 190; TimeScale = 1800; Bio = 1; Style = Dic; Look = BgLook.Cortex; GridCell = 24; Fluid = false;
        Modes = new[] { new ScopeMode("Kontrast interferencyjny (IR-DIC, żywy skrawek)", 6), new ScopeMode("Fluorescencja – GFP (elektroporacja in utero)", 7), new ScopeMode("Jasne pole (bez kontrastu)", 2) };
        ChunkSize = 240; ActiveR = 2; ParkCap = 70;
        BiomeNames = new[] { "Kora mózgu zarodka" }; BiomeColors = new[] { Rgb(180, 170, 190) };
        Mag = "Obiektyw 40× (400×) – time-lapse ×1800"; ScoreLabel = "µm aksonu"; LineUnit = "neuronów"; BirthWord = "podziałów";
        Reproduces = false;
        SomaticShort = "Neuron już się nie dzieli – geny przejdą dalej tylko przez komórki rozrodcze myszy.";
        SomaticNote = "Neuron jest komórką postmitotyczną: po ostatnim podziale progenitora nie dzieli się już nigdy i żyje tak długo jak mysz (~2 lata). Jego „dostosowanie” to przeżycie i włączenie się w sieć – właściwa warstwa, akson w drugiej półkuli, aktywność. Geny przejdą do następnego pokolenia tylko przez komórki rozrodcze myszy, jeśli jej mózg będzie działał.";
        ReproFact = "Neuron jest komórką postmitotyczną – po ostatnim podziale progenitora już nigdy się nie dzieli. Twój neuron powstał z podziału symetrycznego progenitora pośredniego (Tbr2⁺) w strefie podkomorowej: dwa siostrzane neurony (Noctor i in. 2004). Geny neuronu przechodzą dalej wyłącznie przez komórki rozrodcze myszy.";
        Controls = "WASD / przytrzymaj LPM – ruch (progenitor i komórka wielobiegunowa – powoli w dowolną stronę; na włóknie gleju: W – naprzód ku oponie, A/D – sąsiednie włókno) · stożek wzrostu: WASD – kierunek wzrostu aksonu · Spacja – widok: ciało komórki ↔ stożek wzrostu · Shift – przyspieszenie czasu · M – mikroskop (DIC / fluorescencja GFP / jasne pole)";
        LoseText = "Twój neuron obumarł";
        Goals = new[]
        {
            new Goal("Opuść strefę komorową – dotrzyj do strefy podkomorowej (SVZ)", 1, w => w.Stat("n_svz")),
            new Goal("Podział progenitora pośredniego: dwa siostrzane neurony", 1, w => w.Stat("n_div")),
            new Goal("Polaryzacja: dotknij neurytem aksonu TAG-1 – powstanie Twój akson", 1, w => w.Stat("n_axon")),
            new Goal("Pod podpłytką: synapsy z neuronów podpłytki przełączą Cię w komórkę dwubiegunową", 1, w => w.Stat("n_bipolar")),
            new Goal("Wędruj po włóknie gleju i osiądź na szczycie płytki korowej (translokacja końcowa)", 1, w => w.Stat("n_settle")),
            new Goal("Poprowadź akson przez linię środkową (ciało modzelowate)", 1, w => w.Stat("n_cross")),
            new Goal("Akson pod korą drugiej półkuli, w miejscu lustrzanym (homotopowym)", 1, w => w.Stat("n_target")),
            new Goal("Przetrwaj falę naturalnej śmierci neuronów (P2–P5)", 1, w => w.Stat("n_survive")),
        };
        Facts = new[]
        {
            "Kora mózgu powstaje „od środka na zewnątrz”: neurony urodzone najwcześniej tworzą warstwy głębokie (VI, V), a późniejsze – jak Twój, urodzony w E15,5 – przechodzą obok nich do warstw powierzchownych II/III (Angevine i Sidman 1961). Poród myszy następuje w E19,5 (= P0).",
            "Neurony rodzą się przy komorze mózgu z komórek gleju promienistego, które są zarazem komórkami macierzystymi i rusztowaniem dla wędrówki. Większość neuronów warstw powierzchownych pochodzi z progenitorów pośrednich (Tbr2⁺), które w strefie podkomorowej dzielą się symetrycznie na dwa neurony (Noctor i in. 2004; Kowalczyk i in. 2009).",
            "Jądra komórek gleju promienistego wędrują w rytmie cyklu komórkowego (wędrówka jądra śródfazowa): mitoza zawsze przy powierzchni komory, faza S – przy podstawnym brzegu strefy komorowej. Cykl wydłuża się z 8,1 h w E11 do 18,4 h w E16 (Takahashi i in. 1995). Progenitory pośrednie mają cykl dłuższy – ~26 h, głównie przez długą fazę G1 (~21 h), a sama mitoza trwa ~0,5 h (Arai i in. 2011).",
            "Nowo narodzone neurony są najpierw wielobiegunowe: wysuwają i chowają cienkie wypustki i wędrują powoli (średnio 4,4 µm/h), często zmieniając kierunek (Tabata i Nakajima 2003). Znakowanie FlashTag pokazuje, że neurony urodzone w E15,5 po dobie gromadzą się w dolnej strefie pośredniej, a po ~3 dniach docierają do szczytu płytki korowej (Yoshinaga i in. 2021).",
            "Akson powstaje, gdy neuryt komórki wielobiegunowej dotknie aksonów pionierskich z białkiem adhezyjnym TAG-1 w dolnej strefie pośredniej (Namba i in. 2014). Akson zostaje z tyłu jako wypustka tylna, a z przodu wyrasta przyszły dendryt szczytowy (Hatanaka i Murakami 2002).",
            "Pod podpłytką neurony podpłytki tworzą na komórkach wielobiegunowych przejściowe synapsy glutaminianergiczne; przekaźnictwo przez receptory NMDA przełącza komórkę w dwubiegunową, szybciej wędrującą po gleju (Ohtaka-Maruyama i in. 2018).",
            "Lokomocja: neuron obejmuje włókno gleju promienistego (Rakic 1972), wysuwa wypustkę wiodącą, tworzy w niej poszerzenie (dylatację), a potem jądro skacze do przodu. W skrawkach średnio 9–12 µm/h (Tabata i Nakajima 2003), w samych skokach wyraźnie szybciej.",
            "Translokacja końcowa: gdy wypustka wiodąca sięgnie strefy brzeżnej (relina z komórek Cajala–Retziusa), neuron przestaje korzystać z gleju i w niecałą godzinę podciąga ciało przez ostatnie ~50 µm (Sekine i in. 2011; Nadarajah i in. 2001). Dlatego każdy nowy neuron mija starsze i osiada najwyżej.",
            "Interneurony hamujące (GABA) nie rodzą się w korze – przychodzą stycznie z wyniosłości zwojowych, w strumieniach w strefie brzeżnej i w dolnej strefie pośredniej; w żywym zarodku średnio 12 ± 6 µm/h (Yanagida i in. 2012).",
            "Mikroglej – makrofagi mózgu, pochodzące z woreczka żółtkowego – jest w korze zarodka rozproszony w strefie komorowej, podkomorowej i pośredniej (Hattori i Miyata 2018). Pochłania komórki apoptotyczne i część progenitorów (Cunningham i in. 2013), a jego rozmieszczenie wpływa na rozwój połączeń (Squarzoni i in. 2014).",
            "Ciało modzelowate łączy półkule. Pierwsze przez linię środkową przechodzą aksony pionierskie z kory obręczy (E15,5), a aksony kory nowej płyną wzdłuż nich (Koester i O'Leary 1994; Rash i Richards 2001).",
            "Klin glejowy pod ciałem modzelowatym i glej szarego pokrywającego nad nim wydzielają Slit2 – aksony z receptorami Robo omijają je i trafiają w wąską drogę przez linię środkową (Shu i Richards 2001). Netryna-1 przez receptor DCC osłabia to odpychanie przed linią środkową (Fothergill i in. 2014). Wcześniej astrocyty przebudowują szczelinę międzypółkulową w „most” dla aksonów (Gobius i in. 2016).",
            "Stożki wzrostu aksonów spoidłowych posuwają się ~37 µm/h przed linią środkową i ~55 µm/h za nią; częstsze przejściowe wzrosty Ca²⁺ w stożku idą w parze z szybszym wzrostem (Hutchins i in. 2011). W drodze stożki są szerokie i ruchliwe, w korze docelowej – zwarte i dwa razy wolniejsze (Halloran i Kalil 1994).",
            "Aksony neuronów warstwy II/III urodzonych w E15,5 przechodzą linię środkową ok. P3 i wnikają do kory drugiej półkuli ok. P6 (kora czuciowa – Wang i in. 2007); do istoty białej pod korą wzrokową drugiej strony docierają ok. P3 (Mizuno i in. 2007). Do prawidłowego unerwienia potrzebna jest aktywność neuronów.",
            "Naturalna śmierć neuronów: między P2 a P5 ginie ~12% neuronów pobudzających kory, a między P5 a P10 ~30% interneuronów (Wong i in. 2018). Szczyt apoptozy wypada ok. P5–P6 i jest silniejszy w warstwach powierzchownych kory ruchowej; aktywność elektryczna chroni neurony przed apoptozą (Blanquie i in. 2017; przegląd – Wong i Marín 2019).",
            "Neurony z jednego klonu (potomkowie tej samej komórki gleju promienistego) układają się w kolumnie i częściej łączą się synapsami ze sobą niż z obcymi neuronami (Yu i in. 2009).",
            "Zarodkowa kora jest dobrze unaczyniona: splot okołokomorowy leży nad strefą komorową, a z opony miękkiej wnikają naczynia promieniste (Vasudevan i in. 2008). Glej promienisty po urodzeniu przekształca się w astrocyty (Schmechel i Rakic 1979).",
            "Obrazowanie: migrację bada się w żywych skrawkach kory w kontraście interferencyjnym (IR-DIC) albo po elektroporacji in utero plazmidu z GFP – w fluorescencji widać tylko wyznakowaną kohortę (klawisz M). Sama elektroporacja plazmidu ściąga mikroglej ku komorze (Hattori i Miyata 2018).",
            "Uproszczenia gry: time-lapse ×1800 (1 s gry = 30 min), w oczekiwaniu na fale śmierci ×7200; przekrój kory jest „rozprostowany” – kora przyśrodkowa w naturze wygina się ku spoidłu, a droga aksonu do linii środkowej jest ok. 2× dłuższa; grubość stref jest stała, jak w E16,5 (Tonosaki i in. 2023), choć naprawdę kora rośnie; kierunek wędrówki wybiera tu gracz, a naprawdę – sygnały z tkanki.",
        };
    }

    // ------------------------------------------------ świat
    public override int BiomeAt(World w, int cx, int cy) => 0;
    public override string ZoneAt(World w, Ent p) => Cortex.NameAt(p.X, p.Y);
    /// <summary>Strefy kory mają po ~20–90 µm grubości: siatka tła co 10 µm w pionie.</summary>
    public override (int nx, int ny, float cx, float cy) ZoneGrid => (16, 64, 55, 10);
    public override BgZone Backdrop(World w, float x, float y)
    {
        // gęstość materii poza płaszczyzną ostrości w strefach kory (skrawek ma grubość – nad i pod nami leżą kolejne komórki)
        return Cortex.At(x, y) switch
        {
            Cortex.Z.Out => new BgZone(0, 0, 0, 0.05f, 0.02f),
            Cortex.Z.Meninges or Cortex.Z.Fissure => new BgZone(0.12f, 0.2f, 0.05f, 0.25f, 0.05f, 0.52f, 0.5f, 0.47f),
            Cortex.Z.MZ => new BgZone(0.15f, 0.25f, 0.35f, 0.35f, 0.06f),
            Cortex.Z.CP => new BgZone(0.85f, 0.7f, 0.35f, 0.35f, 0.18f, 0.49f, 0.49f, 0.51f),
            Cortex.Z.SP => new BgZone(0.45f, 0.45f, 0.6f, 0.5f, 0.1f),
            Cortex.Z.IZ or Cortex.Z.CC => new BgZone(0.3f, 0.35f, 0.85f, 0.6f, 0.1f, 0.5f, 0.5f, 0.49f),
            Cortex.Z.SVZ => new BgZone(0.7f, 0.6f, 0.35f, 0.4f, 0.14f),
            Cortex.Z.VZ or Cortex.Z.LGE => new BgZone(0.9f, 0.8f, 0.45f, 0.3f, 0.2f, 0.49f, 0.49f, 0.51f),
            Cortex.Z.Ventricle => new BgZone(0.03f, 0.08f, 0, 0.1f, 0),
            _ => new BgZone(0.55f, 0.5f, 0.4f, 0.4f, 0.12f),
        };
    }

    public override void Init(World w)
    {
        F = new CortexField(w.Seed) { Age = 15.5f, SimT = 0 };
        Cohort.Clear(); Dead.Clear(); Me = null; SisterN = null;
        DeadCount = DeadPost = 0; _deathAcc = _embAcc = 0; _popT = 0; _survT = -1; _winT = -1; _p2 = _fate = _branch = false; NoFate = false; RiskOverride = -1;
        // miejsce startu: kolumna bez naczynia promienistego, z włóknami gleju (najdalej od naczyń na całej grubości kory)
        float x0 = 0, best = -1;
        for (int k = 0; k < 40; k++)
        {
            float x = -60 + k * 3, dmin = 1e9f;
            for (float y = 10; y < Cortex.VzB; y += 12) dmin = Math.Min(dmin, F.VesselDist(x, y, out _, out _));
            if (dmin > best) { best = dmin; x0 = x; }
        }
        var me = w.Add(new CortNeuron(w, x0, Cortex.VzB - 5.5f, CortNeuron.Stage.Ipc, true) { Cycle = 0 });
        Me = me; Cohort.Add(me); SiteX = x0;
        CpCount = ProgCount = 0;
        for (int c = (int)MathF.Floor((x0 - 520) / CortexField.CW); c <= (int)MathF.Floor((x0 + 520) / CortexField.CW); c++)
            foreach (var f in F.Column(c))
            {
                if (f.T is CortexField.CT.CPY or CortexField.CT.CPD or CortexField.CT.SPN) CpCount++;
                else if (f.T is CortexField.CT.RG or CortexField.CT.IPC or CortexField.CT.MP) ProgCount++;
            }
        w.SetPlayer(me);
        w.CamX = me.X; w.CamY = me.Y;
        // kohorta GFP: sąsiednie progenitory pośrednie, wyznakowane tym samym plazmidem
        for (int i = 0; i < 6; i++)
        {
            float x = x0 + Rand(-110, 110), y = Rand(440, Cortex.VzB - 6);
            if (F.VesselDist(x, y, out _, out _) < 6) continue;
            var c = w.Add(new CortNeuron(w, x, y, CortNeuron.Stage.Ipc, true) { Cycle = Rand(0.05f, 0.35f) });
            Cohort.Add(c);
        }
        w.UpdateChunks(true);
        Interneurons(w, true);
        MicrogliaPop(w, true);
        // pierwsze wywołanie rzadkich ścieżek (kompilacja JIT) jeszcze przy ładowaniu świata, a nie w trakcie gry
        PerSecond(w); CollectMovers(w); Hud(w); Bars(w); FoodPoint(w, out _, out _); Risk();
        w.Log("E15,5: komórka gleju promienistego podzieliła się przy powierzchni komory. Jedna córka została komórką macierzystą, a druga – Ty – to progenitor pośredni (Tbr2⁺). Idź w górę (W), do strefy podkomorowej.", "info");
    }

    /// <summary>Organizmy chunka: interneurony w strumieniach, mikroglej, wędrujące neurony (bez znacznika).</summary>
    public override void GenerateChunk(World w, Chunk c)
    {
        if (F == null) return;
        float x0 = c.X0, x1 = c.X0 + c.S, y0 = c.Y0, y1 = c.Y0 + c.S;
        bool Pick(float ya, float yb, float r, out float x, out float y)
        {
            x = y = 0; float a = Math.Max(ya, y0), b = Math.Min(yb, y1);
            if (b <= a) return false;
            for (int t = 0; t < 12; t++)
            {
                x = Rand(x0 + 2, x1 - 2); y = Rand(a, b);
                if (Cortex.Tissue(Cortex.At(x, y)) && F.VesselDist(x, y, out _, out _) > r + 1 && Math.Abs(x - Cortex.XM) > 90) return true;
            }
            return false;
        }
        // wędrujące neurony innych kohort (bez GFP): wielobiegunowe w MAZ, dwubiegunowe na włóknach w IZ i CP
        if (Age < Birth + 1)
        {
            for (int i = 0; i < 3; i++) if (Pick(292, 338, 5, out float x, out float y)) w.Add(new CortNeuron(w, x, y, CortNeuron.Stage.Multipolar, false));
            for (int i = 0; i < 7; i++)
                if (Pick(i < 4 ? 190 : 80, i < 4 ? 330 : 155, 5, out float x, out float y))
                {
                    var n = w.Add(new CortNeuron(w, x, y, CortNeuron.Stage.Locomotion, false) { Lead = Rand(15, 30) });
                    if (F.NearestFiber(x, y, 8, out int fi, out float fx)) { n.Fiber = fi; n.Side = x >= fx ? 1 : -1; } else n.S = CortNeuron.Stage.Bipolar;
                }
        }
    }

    // ------------------------------------------------ zdarzenia etapów (wywoływane przez neurony)
    bool Mine(CortNeuron n) => n == Me;
    public void DivideIpc(CortNeuron n)
    {
        float ax = Rand(0, TAU), off = n.R0 * 0.62f;
        n.S = CortNeuron.Stage.Multipolar; n.Mit = 0; n.R0 = Rand(4.1f, 4.4f); n.R = n.R0 + 0.5f;
        n.Name = "młody neuron piramidowy"; n.Latin = "neuron warstwy II/III (Mus musculus)";
        float bx = n.X - MathF.Cos(ax) * off, by = n.Y - MathF.Sin(ax) * off;
        n.X += MathF.Cos(ax) * off; n.Y += MathF.Sin(ax) * off;
        var b = n.W.Add(new CortNeuron(n.W, bx, by, CortNeuron.Stage.Multipolar, n.Gfp) { Sister = Mine(n) });
        if (n.Gfp) Cohort.Add(b);
        if (Mine(n))
        {
            SisterN = b; n.W.AddStat("n_div", 1);
            n.W.Banner("Dwa siostrzane neurony", "Progenitor pośredni podzielił się symetrycznie – Twoja siostra wędruje obok");
            n.W.Log("Podział symetryczny: z progenitora pośredniego powstały dwa neurony (Noctor i in. 2004). Neurony nie dzielą się już nigdy. Teraz jesteś komórką wielobiegunową – szukaj w dolnej strefie pośredniej ciemnych pęczków aksonów TAG-1 (wyżej, W).", "good");
        }
    }
    public void OnAxon(CortNeuron n)
    {
        if (!Mine(n)) return;
        n.W.AddStat("n_axon", 1);
        n.W.Log("Polaryzacja: neuryt dotknął aksonu pionierskiego z TAG-1 i staje się aksonem (Namba i in. 2014). Akson rośnie wzdłuż pęczka ku linii środkowej. Teraz idź powoli w górę – pod podpłytkę, gdzie neurony podpłytki utworzą na Tobie synapsy.", "good");
        // stożek wzrostu jako osobny obiekt (kamera może na nim stanąć – Spacja); rośnie sam, dopóki ciało wędruje
        n.Cone = n.W.Add(new GrowthCone(n.W, n));
    }
    public void OnBipolar(CortNeuron n)
    {
        if (!Mine(n)) return;
        n.W.AddStat("n_bipolar", 1);
        n.W.Log("Synapsy z neuronów podpłytki (receptory NMDA) przełączyły Cię w komórkę dwubiegunową (Ohtaka-Maruyama i in. 2018): neuryty się cofają, rośnie wypustka wiodąca ku oponie i komórka obejmuje włókno gleju promienistego.", "good");
    }
    public void OnLocomotion(CortNeuron n)
    {
        if (!Mine(n)) return;
        n.W.Log("Lokomocja po włóknie gleju: W – naprzód. Najpierw wydłuża się wypustka i powstaje w niej poszerzenie, potem jądro skacze do przodu. A/D – przejście na sąsiednie włókno (np. obok naczynia).", "info");
    }
    public void OnTranslocation(CortNeuron n)
    {
        if (!Mine(n)) return;
        n.W.Log("Translokacja końcowa: wypustka wiodąca zakotwiczyła się w strefie brzeżnej (relina z komórek Cajala–Retziusa). Neuron puszcza glej i podciąga ciało przez ostatnie ~50 µm (Sekine i in. 2011).", "good");
    }
    public void OnSettled(CortNeuron n)
    {
        if (!Mine(n)) return;
        var w = n.W; w.AddStat("n_settle", 1);
        string age = AgeStr(Age);
        w.Banner("Na szczycie płytki korowej", $"Twój neuron osiadł w {age} – nad starszymi neuronami (od środka na zewnątrz)");
        w.Log(Age <= Birth + 0.5f
            ? $"Osiadłeś w {age} – na czas (FlashTag: neurony z E15,5 docierają tu w E18,5–P0,5; Yoshinaga i in. 2021). Wypustka wiodąca staje się dendrytem szczytowym. Teraz steruj stożkiem wzrostu aksonu – przez linię środkową do drugiej półkuli."
            : $"Osiadłeś w {age} – później niż większość kohorty. Teraz steruj stożkiem wzrostu aksonu – przez linię środkową do drugiej półkuli.", "good");
        TakeCone(w);
    }
    public void OnStranded(CortNeuron n)
    {
        if (!Mine(n)) return;
        var w = n.W;
        w.Log("Glej promienisty przekształca się w astrocyty – włókien już nie ma, a Twój neuron nie dotarł do płytki korowej. Zostaje w istocie białej (heterotopia); takie neurony mają mało wejść synaptycznych i częściej giną w fali śmierci P2–P5.", "bad");
        n.AnchorY = n.Y - n.R0 - 10;
        TakeCone(w);
    }
    /// <summary>Sterowanie przechodzi na stożek wzrostu aksonu (ciało już nie wędruje).</summary>
    void TakeCone(World w)
    {
        if (Me?.Ax == null) return;
        Me.Ax.Hold = false;
        Me.Cone ??= w.Add(new GrowthCone(w, Me));
        w.Player = null; w.SetPlayer(Me.Cone); w.Focus = null;
    }

    // ------------------------------------------------ akson: wzrost stożka (automatyczny i gracza)
    /// <summary>Stożek w korze docelowej (druga półkula, płytka korowa lub strefa brzeżna) – wolniejszy, zwarty.</summary>
    public bool InTargetCortex(float x, float y) => x < Cortex.XM - Cortex.FW && Cortex.At(x, y) is Cortex.Z.CP or Cortex.Z.MZ;
    /// <summary>Podłoże dla aksonu spoidłowego: 1 – istota biała (droga), 2 – kora docelowa (połowa prędkości), 0 – zamknięte, −1 – odpychanie (Slit2).</summary>
    public int AxonZone(float x, float y, bool crossed, out string why)
    {
        why = "";
        var z = Cortex.At(x, y);
        switch (z)
        {
            case Cortex.Z.IZ: case Cortex.Z.SP: case Cortex.Z.CC: return 1;
            case Cortex.Z.CP: case Cortex.Z.MZ:
                if (x < Cortex.XM - Cortex.FW && crossed) return 2;
                why = "Akson spoidłowy nie wrasta do własnej kory – biegnie w istocie białej (strefie pośredniej) ku linii środkowej."; return 0;
            case Cortex.Z.GW: why = "Zapadnięcie stożka wzrostu: klin glejowy wydziela Slit2, a receptory Robo stożka każą mu się cofnąć (Shu i Richards 2001). Droga przez linię środkową biegnie wyżej – w ciele modzelowatym."; return -1;
            case Cortex.Z.IG: why = "Zapadnięcie stożka: glej szarego pokrywającego (nad ciałem modzelowatym) też wydziela Slit2. Zejdź niżej, w pas aksonów."; return -1;
            case Cortex.Z.SVZ: case Cortex.Z.VZ: case Cortex.Z.Ventricle: case Cortex.Z.LGE:
                why = "Aksony kory nie wrastają w strefy proliferacyjne ani do komory – ich droga to strefa pośrednia (przyszła istota biała)."; return 0;
            default: why = "Tędy akson nie przejdzie – przegroda, opona albo szczelina międzypółkulowa. Trzymaj się pasa aksonów w ciele modzelowatym."; return 0;
        }
    }
    /// <summary>
    /// Krok wzrostu aksonu. ang = kierunek żądany (NaN – bez zmiany), mag – ile „chce” rosnąć (gracz), player – steruje gracz.
    /// Prędkości: ~37 µm/h przed linią środkową, ~55 µm/h za nią (Hutchins i in. 2011), w korze docelowej o połowę mniej (Halloran i Kalil 1994).
    /// </summary>
    public void GrowStep(CortNeuron n, Axon a, float ang, float mag, float dt, bool player)
    {
        float h = dt / 3600;
        if (a.Collapse > 0) a.Collapse = Math.Max(0, a.Collapse - h / 0.8f);
        if (a.Pause > 0) { a.Pause -= h; return; }
        if (a.AtTarget && !player) return;
        var tip = a.Tip;
        if (!player)
        {
            // automat: wzdłuż pęczków ku linii środkowej, przy niej w pas ciała modzelowatego; za nią – ku miejscu homotopowemu
            float yc = (Cortex.Cc0 + Cortex.Cc1) * 0.5f + a.Lane, yT;
            if (!a.Crossed)
            {
                float dx = tip.X - Cortex.XM;
                yT = Lerp(yc, a.Y0, Smooth((dx - 160) / 560));
                // akson gracza czeka przed linią środkową, aż gracz przejmie stożek (stożek „bada” teren – przerwy przy punktach decyzji)
                if (n == Me && dx < 210) { a.Hold = true; return; }
            }
            else
            {
                float dm = Cortex.XM - tip.X;
                yT = Lerp(yc, a.Y0, Smooth((dm - 160) / 560));
                if (tip.X <= a.TargetX + 45) { a.AtTarget = true; return; }
            }
            ang = MathF.Atan2(Math.Clamp((yT - tip.Y) * 0.06f, -0.8f, 0.8f), -1);
            if (a.Crossed && tip.X < a.TargetX) ang = 0;
            mag = 1;
            // krótkie przerwy i zmiany kształtu (Halloran i Kalil 1994)
            if (Chance(h * 0.35f)) { a.Pause = Rand(0.2f, 0.9f); return; }
        }
        if (mag < 0.05f) return;
        if (!float.IsNaN(ang)) a.Dir = Turn(a.Dir, ang, 2.2f * h + 0.02f);
        float v = (a.Crossed ? 54.6f : 36.9f) / 3600;
        if (InTargetCortex(tip.X, tip.Y)) v *= 0.5f;
        float d = v * mag * dt, dx2 = MathF.Cos(a.Dir) * d, dy2 = MathF.Sin(a.Dir) * d;
        float nx = tip.X + dx2 * 3 + MathF.Cos(a.Dir) * 1.5f, ny = tip.Y + dy2 * 3 + MathF.Sin(a.Dir) * 1.5f;
        int z = AxonZone(nx, ny, a.Crossed, out string why);
        if (z == -1)
        {
            a.Collapse = 1; a.Pause = 0.7f; a.Retract(6);
            if (player) { n.W.LogOnce("slit", why, "bad", 8); n.W.AddStat("n_collapse", 1); }
            return;
        }
        if (z == 0) { if (player) n.W.LogOnce("block", why, "info", 8); return; }
        a.Advance(dx2, dy2);
        if (!a.Crossed && a.Tip.X < Cortex.XM - 2)
        {
            a.Crossed = true;
            if (n == Me) { n.W.AddStat("n_cross", 1); n.W.Banner("Linia środkowa!", "Akson w drugiej półkuli – teraz rośnie szybciej"); n.W.Log("Akson przeszedł przez linię środkową w ciele modzelowatym. Za nią stożki wzrostu przyspieszają (~55 µm/h zamiast ~37 µm/h; Hutchins i in. 2011). Cel: miejsce lustrzane pod korą drugiej półkuli (zielona strzałka).", "good"); }
        }
        if (n == Me && a.Crossed && !a.AtTarget && MathF.Abs(a.Tip.X - a.TargetX) < 110 && a.Tip.Y > Cortex.CpB - 10)
        {
            a.AtTarget = true; n.W.AddStat("n_target", 1);
            n.W.Banner("Pod korą docelową", "Akson dotarł pod korę homotopową drugiej półkuli");
            n.W.Log("Akson jest pod korą lustrzanego pola drugiej półkuli. Tu stożki wzrostu długo czekają, a do kory wnikają zwykle bocznymi gałęziami (Halloran i Kalil 1994) – u myszy ok. P6 (Wang i in. 2007). Czas przyspiesza – przetrwaj falę śmierci neuronów.", "good");
        }
    }

    // ------------------------------------------------ apoptoza komórek pola i mikroglej
    public bool NearestDying(float x, float y, float r, out int id, out float dx, out float dy)
    {
        // uwaga: Id komórek pola bywa ujemne (kolumny po drugiej stronie osi x) – osobna flaga trafienia
        id = 0; dx = dy = 0; float bd = r * r; bool hit = false;
        for (int i = 0; i < Dead.Count; i++)
        {
            var d = Dead[i]; if (d.Eaten) continue;
            float q = Sq(d.X - x) + Sq(d.Y - y);
            if (q < bd) { bd = q; id = d.Id; dx = d.X; dy = d.Y; hit = true; }
        }
        return hit;
    }
    public void Eat(int id, Microglia m)
    {
        for (int i = 0; i < Dead.Count; i++) if (Dead[i].Id == id) { var d = Dead[i]; d.Eaten = true; Dead[i] = d; }
    }
    /// <summary>Apoptoza losowej komórki pola danego typu w pobliżu kamery – w miejscu, gdzie komórka akurat jest (jądra gleju wędrują w cyklu).</summary>
    bool KillField(World w, Func<CortexField.CT, bool> type, float yMin, float yMax)
    {
        for (int t = 0; t < 8; t++)
        {
            float x = w.CamX + Rand(-520, 520);
            int c = (int)MathF.Floor(x / CortexField.CW);
            var col = F.Column(c); if (col.Length == 0) continue;
            var f = col[RandI(0, col.Length - 1)];
            if (!type(f.T) || f.Y < yMin || f.Y > yMax || F.Dead.Contains(f.Id)) continue;
            F.CellPos(f, out float cx, out float cy, out float ang, out bool mit);
            if (mit) continue;
            F.Dead.Add(f.Id);
            Dead.Add(new Dying { Id = f.Id, X = cx, Y = cy, W = f.W, Asp = f.Asp, Ang = ang, T = f.T, T0 = w.SimT });
            DeadCount++;
            return true;
        }
        return false;
    }
    /// <summary>Tempo naturalnej śmierci neuronów pobudzających (na dobę): ~12% w P2–P5 (Wong i in. 2018), szczyt P4–P6 (Blanquie i in. 2017).</summary>
    public static float DeathRate(float age) => 0.12f * MathF.Exp(-Sq((age - (Birth + 4f)) / 1.1f)) / (1.1f * MathF.Sqrt(PI));

    // ------------------------------------------------ pętla
    public override void Update(World w, float dt, float gdt)
    {
        if (F == null) return;
        F.SimT = w.SimT; F.Age = 15.5f + w.SimT / 86400f;
        float h = dt / 3600;
        CollectMovers(w);
        // aksony kohorty rosną same (także gdy ich ciała są zamrożone w dalekich chunkach); akson gracza – do przejęcia stożka
        foreach (var n in Cohort)
        {
            if (!n.Alive || n.Ax == null || n.S == CortNeuron.Stage.Apoptotic) continue;
            if (n == Me && w.Player == Me.Cone) continue;
            GrowStep(n, n.Ax, float.NaN, 1, dt, false);
        }
        // Spacja: kamera na ciele komórki ↔ na stożku wzrostu
        if (w.Input.Action && Me?.Cone != null && Me.Alive)
        {
            if (w.Player == Me.Cone) { w.Focus = w.Focus == null ? Me : null; }
            else { w.Focus = w.Focus == null ? Me.Cone : null; }
            if (w.Focus != null) w.LogOnce("view", w.Focus == Me ? "Widok: ciało komórki (Spacja – z powrotem do stożka wzrostu)." : "Widok: stożek wzrostu aksonu – rośnie sam, dopóki ciało wędruje (Spacja – powrót).", "info", 30);
        }
        // tempo czasu: w oczekiwaniu (podział progenitora, okno śmierci) szybciej; Shift – przyspieszenie
        bool wait = Me != null && Me.S == CortNeuron.Stage.Ipc && Me.Cycle < 1 && Me.Y < Cortex.SvzB - 4 && Me.Y > Cortex.IzB + 6;
        bool late = Me?.Ax != null && Me.Ax.AtTarget || _fate;
        w.TimeScale = late ? 7200 : TimeScale;
        w.Ff = w.Input.Boost ? 4 : wait ? 3 : 1;
        // zgony: nieliczne w strefach proliferacyjnych przed urodzeniem (mikroglej pochłania też progenitory – Cunningham i in. 2013),
        // fala P2–P5 w płytce korowej i podpłytce (Wong i in. 2018; Blanquie i in. 2017)
        _deathAcc += DeathRate(Age) / 24 * h * CpCount;
        _embAcc += (Age > 16.8f && Age < Birth ? 0.004f : 0.0008f) / 24 * h * ProgCount;
        while (_deathAcc >= 1) { _deathAcc -= 1; if (KillField(w, t => t is CortexField.CT.CPY or CortexField.CT.CPD or CortexField.CT.SPN, Cortex.MzB, Cortex.SpB)) DeadPost++; }
        while (_embAcc >= 1) { _embAcc -= 1; KillField(w, t => t is CortexField.CT.RG or CortexField.CT.IPC or CortexField.CT.MP, Cortex.SpB, Cortex.VzB); }
        // ciałka apoptotyczne znikają (pochłonięte albo po ~6 h rozpadu)
        Dead.RemoveAll(d => d.Eaten || w.SimT - d.T0 > 6 * 3600);
        // kohorta GFP w oknie śmierci: ~12% neuronów ginie (hazard jak wyżej)
        foreach (var n in Cohort)
            if (n != Me && n.Alive && n.S is CortNeuron.Stage.Settled or CortNeuron.Stage.Stranded && Chance(DeathRate(Age) / 24 * h * (n.S == CortNeuron.Stage.Stranded ? 5 : 1)))
                n.StartApoptosis("");
        if (w.EverySecond(gdt)) PerSecond(w);
        Fate(w, dt);
    }

    Vector3[] _mv = new Vector3[256];
    static readonly Comparison<Vector3> ByX = (p, q) => p.X.CompareTo(q.X);
    static readonly IComparer<Vector3> ByXc = Comparer<Vector3>.Create(ByX);
    void CollectMovers(World w)
    {
        int k = 0;
        foreach (var e in w.Ents)
        {
            if (!e.Alive || e is not (CortNeuron or Interneuron or Microglia)) continue;
            if (k >= _mv.Length) Array.Resize(ref _mv, _mv.Length * 2);
            _mv[k++] = new Vector3(e.X, e.Y, e.Radius + 0.6f);
        }
        Array.Sort(_mv, 0, k, ByXc);
        F.Movers = _mv; F.MoverN = k;
    }

    void PerSecond(World w)
    {
        var me = Me; if (me == null) return;
        if (me.S == CortNeuron.Stage.Ipc && w.Stat("n_svz") == 0 && me.Y < Cortex.SvzB - 2)
        {
            w.AddStat("n_svz", 1);
            w.Log("Strefa podkomorowa: tu progenitory pośrednie dzielą się na dwa neurony. Poczekaj na koniec cyklu komórkowego (czas przyspiesza).", "good");
        }
        // neuron, który do P2 nie zaczął wędrówki po glej (albo utknął jako progenitor), nie włączy się w korę – ginie w fali śmierci
        if (Age >= Birth + 2 && me.S is CortNeuron.Stage.Ipc or CortNeuron.Stage.Mitosis or CortNeuron.Stage.Multipolar or CortNeuron.Stage.Bipolar && me.Alive)
        {
            me.StartApoptosis("Twój neuron nie dokończył wędrówki: jako komórka wielobiegunowa w strefie pośredniej nie dostaje wejść synaptycznych kory – w fali naturalnej śmierci (P2–P5) uruchamia program apoptozy.");
            if (w.Player != me) { w.Player = null; w.SetPlayer(me); }
        }
        if (!_p2 && Age >= Birth + 2)
        {
            _p2 = true;
            w.Banner("P2: fala śmierci neuronów", "Do P5 zginie ~12% neuronów pobudzających kory");
            w.Log("Zaczyna się naturalna śmierć neuronów: między P2 a P5 ginie ~12% neuronów pobudzających (Wong i in. 2018). Obkurczone komórki z ciemną, zbitą chromatyną (piknoza) to apoptoza – sprząta je mikroglej. Przeżywają głównie neurony dobrze włączone w sieć i aktywne (Blanquie i in. 2017).", "info");
        }
        // uzupełnianie wędrujących neuronów bez znacznika, dopóki trwa neurogeneza (do ~E18) i ich wędrówka
        _popT += 1;
        if (_popT >= 2 && Age < Birth - 0.8f)
        {
            _popT = 0;
            int mig = w.Count(e => e is CortNeuron { Gfp: false } c && c.S is CortNeuron.Stage.Multipolar or CortNeuron.Stage.Locomotion or CortNeuron.Stage.Bipolar);
            if (mig < 55)
            {
                var (x, y) = (w.CamX + Sign() * Rand(ViewW * 0.7f, ViewW * 2.2f), Rand(292, 336));
                if (Cortex.Tissue(Cortex.At(x, y)) && Math.Abs(x - Cortex.XM) > 100 && F.VesselDist(x, y, out _, out _) > 6) w.Add(new CortNeuron(w, x, y, CortNeuron.Stage.Multipolar, false));
            }
        }
        // osiadłe neurony bez znacznika wtapiają się w płytkę (poza kadrem usuwane – ich miejsce zajmują komórki pola)
        foreach (var e in w.Ents)
            if (e is CortNeuron { Gfp: false } c && c.S is CortNeuron.Stage.Settled && w.SimT > 0 && !w.OnScreen(e, 1.6f) && c.SettledAge > 0 && Age - c.SettledAge > 0.5f) e.Alive = false;
        Interneurons(w, false);
        MicrogliaPop(w, false);
    }

    /// <summary>Strumienie interneuronów (y od–do, liczba na 240 µm szerokości): strefa brzeżna, podpłytka, dolna strefa pośrednia i SVZ.</summary>
    static readonly (float a, float b, float n)[] Streams = { (6, 17, 2), (164, 176, 1), (318, 388, 4) };
    /// <summary>
    /// Stała gęstość interneuronów w oknie: brakujące pojawiają się poza kadrem, nadmiar daleko za kadrem znika. Po urodzeniu
    /// strumienie styczne słabną – interneurony wchodzą już do płytki korowej.
    /// </summary>
    void Interneurons(World w, bool init)
    {
        float k = Age < Birth ? 1 : Math.Max(0.3f, 1 - (Age - Birth) / 4);
        float x0 = w.WinX0, x1 = w.WinX0 + w.WinW;
        foreach (var (a, b, n) in Streams)
        {
            int want = (int)MathF.Round(n * k * (x1 - x0) / 240f), have = 0;
            foreach (var e in w.Ents) if (e is Interneuron i && i.StreamY > a - 1 && i.StreamY < b + 1 && e.Alive) have++;
            for (int t = 0; t < 30 && have < want; t++)
            {
                float x = Rand(x0, x1), y = Rand(a, b);
                if (!init && MathF.Abs(x - w.CamX) < ViewW * 0.75f) continue;
                if (!Cortex.Tissue(Cortex.At(x, y)) || MathF.Abs(x - Cortex.XM) < 90 || F.VesselDist(x, y, out _, out _) < 5) continue;
                w.Add(new Interneuron(w, x, y, (a + b) * 0.5f)); have++;
            }
            if (have > want + 2)
                foreach (var e in w.Ents)
                    if (e is Interneuron i && i.StreamY > a - 1 && i.StreamY < b + 1 && e.Alive && !w.OnScreen(e, 1.8f)) { e.Alive = false; if (--have <= want) break; }
        }
    }

    /// <summary>
    /// Mikroglej: w zarodku rozproszony w VZ, SVZ i IZ (Hattori i Miyata 2018), w płytce korowej nieliczny; po urodzeniu
    /// zasiedla płytkę korową i sprząta neurony ginące w fali P2–P5. Gęstość na 240 µm szerokości okna.
    /// </summary>
    void MicrogliaPop(World w, bool init)
    {
        float post = Clamp((Age - Birth) / 3, 0, 1);
        float x0 = w.WinX0, x1 = w.WinX0 + w.WinW;
        foreach (var (a, b, n) in new[] { (185f, Cortex.VzB - 6, 4f), (26f, 155f, Lerp(0.5f, 3.5f, post)) })
        {
            int want = (int)MathF.Round(n * (x1 - x0) / 240f), have = 0;
            foreach (var e in w.Ents) if (e is Microglia && e.Alive && e.Y > a - 10 && e.Y < b + 10) have++;
            for (int t = 0; t < 30 && have < want; t++)
            {
                float x = Rand(x0, x1), y = Rand(a, b);
                if (!init && MathF.Abs(x - w.CamX) < ViewW * 0.75f) continue;
                if (!Cortex.Tissue(Cortex.At(x, y)) || MathF.Abs(x - Cortex.XM) < 90 || F.VesselDist(x, y, out _, out _) < 6) continue;
                w.Add(new Microglia(w, x, y)); have++;
            }
            if (have > want + 2)
                foreach (var e in w.Ents)
                    if (e is Microglia && e.Alive && e.Y > a - 10 && e.Y < b + 10 && !w.OnScreen(e, 1.8f)) { e.Alive = false; if (--have <= want) break; }
        }
    }

    /// <summary>Okno naturalnej śmierci: los neuronu gracza rozstrzyga się ok. P5 – zależy od tego, jak dobrze jest włączony w sieć.</summary>
    public float Risk()
    {
        if (RiskOverride >= 0) return RiskOverride;
        var me = Me; if (me == null) return 1;
        if (me.S == CortNeuron.Stage.Stranded) return 0.6f;
        if (me.S != CortNeuron.Stage.Settled) return 0.6f;
        // na czas (FlashTag: szczyt płytki E18,5–P0,5) – dobrze włączony w sieć; później – mniej wejść i aktywności
        return me.SettledAge <= Birth + 0.5f ? 0 : me.SettledAge <= Birth + 2 ? 0.15f : 0.35f;
    }
    void Fate(World w, float dt)
    {
        var me = Me; if (me == null || w.Over) return;
        if (!me.Alive || me.S == CortNeuron.Stage.Apoptotic)
        {
            if (_survT < 0) _survT = w.T;
            if (w.T - _survT > 2.5f)
            {
                w.OverTitle = "Apoptoza";
                w.GameOver(me.S == CortNeuron.Stage.Stranded || me.SettledAge < 0
                    ? "Twój neuron nie dotarł do płytki korowej i zginął w fali naturalnej śmierci (P2–P5). Neurony uwięzione w istocie białej (heterotopie) dostają mało wejść synaptycznych – bez aktywności uruchamia się program apoptozy (Blanquie i in. 2017)."
                    : "Twój neuron zginął w fali naturalnej śmierci (P2–P5), jak ~12% neuronów pobudzających kory (Wong i in. 2018). Osiadł późno i miał mniej wejść – mniej aktywności, a aktywność chroni przed apoptozą (Blanquie i in. 2017). Mikroglej pochłonie ciałka apoptotyczne w ciągu kilku godzin.");
            }
            return;
        }
        // ok. P5 – rozstrzygnięcie
        if (!_fate && Age >= Birth + 5)
        {
            _fate = true;
            float risk = Risk();
            if (!NoFate && risk > 0 && Chance(risk))
            {
                me.StartApoptosis("Apoptoza: kaspazy tną białka komórki, chromatyna zbija się (piknoza), ciało się obkurcza.");
                w.Banner("Apoptoza", "Twój neuron nie przetrwał fali śmierci");
            }
            else
            {
                w.AddStat("n_survive", 1);
                w.Banner("Przetrwałeś!", "Twój neuron przeżył okres naturalnej śmierci");
                w.Log(risk <= 0
                    ? "Przeżyłeś okno P2–P5: neuron osiadł na czas, ma dendryt szczytowy w warstwie I i bierze udział w spontanicznej aktywności sieci – to chroni przed apoptozą (Blanquie i in. 2017). Będzie żył tak długo jak mysz."
                    : "Przeżyłeś okno P2–P5 – mimo gorszego położenia neuron uniknął apoptozy. Będzie żył tak długo jak mysz.", "good");
            }
        }
        // finał: gałęzie aksonu wnikają do kory docelowej (~P6; Wang i in. 2007) → cykl zamknięty
        if (_fate && Age >= Birth + 6 && me.S != CortNeuron.Stage.Apoptotic && me.Ax != null && me.Ax.AtTarget)
        {
            if (!_branch) { _branch = true; StartBranches(me.Ax); w.Log("P6: z aksonu pod korą docelową wyrastają gałęzie boczne i wnikają do kory drugiej półkuli – w warstwy II/III i V (Wang i in. 2007). Połączenie między półkulami jest gotowe.", "good"); }
            GrowBranches(me.Ax, dt);
            if (_winT < 0) _winT = w.T;
            if (w.T - _winT > 6)
            {
                w.OverTitle = "Neuron połączył półkule";
                w.GameOver($"Twój neuron urodził się w E15,5, przewędrował ~{Cortex.VzB - Cortex.MzB:0} µm przez całą grubość kory, osiadł nad starszymi neuronami, przetrwał falę naturalnej śmierci, a jego akson (~{me.Ax.Length():0} µm) przeszedł przez ciało modzelowate i wniknął do kory drugiej półkuli. Takich neuronów spoidłowych jest w korze myszy miliony.", true);
            }
        }
    }
    void StartBranches(Axon a)
    {
        a.Branches.Clear();
        // 2–3 gałęzie boczne z odcinka pod celem
        for (int k = 0; k < 3; k++)
        {
            int i = Math.Max(1, a.P.Count - 1 - k * 6 - RandI(2, 5));
            var b = new List<Vector2> { a.P[i] };
            a.Branches.Add(b);
        }
    }
    void GrowBranches(Axon a, float dt)
    {
        float d = 27f / 3600 * dt; // w korze połowa prędkości w drodze (Halloran i Kalil 1994)
        foreach (var b in a.Branches)
        {
            var t = b[^1]; if (t.Y < Cortex.PczB) continue;
            float wob = 0.35f * MathF.Sin(b.Count * 0.9f + t.X * 0.1f);
            var n = t + new Vector2(MathF.Sin(wob) * d, -MathF.Cos(wob) * d);
            if ((n - b[Math.Max(0, b.Count - 2)]).Length() > 3 || b.Count < 2) b.Add(n); else b[^1] = n;
        }
    }

    // ------------------------------------------------ rysowanie
    public override void DrawUnder(World w, Painter p, View v) { for (int i = 0; i < UnderParts; i++) DrawUnderPart(w, p, v, i); }
    public override int UnderParts => 7;
    public override void DrawUnderPart(World w, Painter p, View v, int part)
    {
        if (F == null) return;
        p.Identity();
        bool fl = w.Fluo;
        if (fl)
        {
            // fluorescencja: tło czarne, widać tylko komórki z GFP (i włókna wyznakowanych komórek gleju)
            if (part == 1) { DrawGfpFibers(p, v); foreach (var n in Cohort) n.DrawAxon(p, v, true); }
            return;
        }
        switch (part)
        {
            case 0: F.DrawGround(p, v); F.DrawVessels(p, v, w.T); DrawDying(w, p, v); break;
            case 1: F.DrawFibers(p, v, FiberAlpha); break;
            case 2:
                F.DrawFascicles(p, v);
                foreach (var n in Cohort) n.DrawAxon(p, v, false);
                break;
            default: F.DrawCells(p, v, part - 3, UnderParts - 3); break;
        }
    }
    /// <summary>Włókna gleju promienistego zanikają po urodzeniu (glej przekształca się w astrocyty – Schmechel i Rakic 1979).</summary>
    public float FiberAlpha => 1 - 0.8f * Smooth((Age - Birth) / 6);
    void DrawDying(World w, Painter p, View v)
    {
        var tp = p.Tex; if (tp == null) return;
        var uv = SpriteAtlas.Uv[SpriteAtlas.Pykno];
        foreach (var d in Dead)
        {
            if (d.Eaten || !v.Vis(d.X, d.Y, d.W)) continue;
            // apoptoza trwa: przez pierwsze ~40 min komórka jeszcze wygląda zwyczajnie, potem obkurcza się i chromatyna się zbija
            float age = (w.SimT - d.T0) / 3600, k = Smooth(age / 1.2f), fade = 1 - Smooth((age - 4) / 2), live = 1 - Smooth(age / 0.7f);
            if (live > 0.01f)
            {
                var uvL = SpriteAtlas.Uv[d.Asp > 2 ? SpriteAtlas.NeuS : d.Asp > 1.3f ? SpriteAtlas.NeuEv(d.Id) : SpriteAtlas.NeuRv(d.Id)];
                float D = d.W * (1 - 0.15f * k) / 0.94f;
                tp.Sprite(d.X, d.Y, D, D, d.Ang, uvL, new Color(150 / 255f / SpriteAtlas.NeuBase, 144 / 255f / SpriteAtlas.NeuBase, 158 / 255f / SpriteAtlas.NeuBase, 0.52f * live));
            }
            float s = d.W * (1 - 0.3f * k) / 0.94f;
            tp.Sprite(d.X, d.Y, s, s, d.Id * 0.7f, uv, new Color(150 / 255f / SpriteAtlas.NeuBase, 142 / 255f / SpriteAtlas.NeuBase, 150 / 255f / SpriteAtlas.NeuBase, 0.7f * fade * (1 - live)));
        }
    }
    void DrawGfpFibers(Painter p, View v)
    {
        // elektroporacja znakuje też komórki gleju promienistego w pobliżu miejsca wstrzyknięcia – ich włókna świecą (słabiej z czasem: plazmid się rozcieńcza)
        float al = Math.Max(0, 1 - (Age - 15.5f) / 3.5f) * FiberAlpha;
        if (al < 0.03f || Me == null) return;
        int i0 = (int)MathF.Floor(v.X0 / CortexField.FS) - 1, i1 = (int)MathF.Floor(v.X1 / CortexField.FS) + 1;
        var buf = p.Buf2; var col = Mul(CortNeuron.GfpD, 0.55f * al); var glow = Mul(CortNeuron.GfpGlow, al);
        for (int i = i0; i <= i1; i++)
        {
            if (!F.FiberExists(i) || Hash2(i, 77, F.Seed) > 0.1f || MathF.Abs(F.FiberBase(i) - SiteX) > 170) continue;
            int n = 0; float yb = F.FiberBottom(i);
            for (float y = Math.Max(0, v.Y0 - 10); y <= Math.Min(yb, v.Y1 + 10) && n < buf.Length; y += Math.Max(3, v.Px * 10)) buf[n++] = new Vector2(F.FiberX(i, y), y);
            if (n < 2) continue;
            var s = new ReadOnlySpan<Vector2>(buf, 0, n);
            p.SoftStroke(s, false, 2.2f, glow); p.Thin(s, false, Math.Max(v.Px, 0.6f), col);
            if (yb > v.Y0 && yb < v.Y1 + 10) { p.Glow(F.FiberX(i, yb), yb, 7, glow); p.FillEllipse(F.FiberX(i, yb), yb, 2.4f, 4.6f, 0, Mul(CortNeuron.GfpC, 0.7f * al)); }
        }
    }
    public override void DrawMinimap(World w, Painter p)
    {
        float x0 = w.WinX0, x1 = w.WinX0 + w.WinW;
        void Band(float a, float b, Color c) { p.Rect(x0, a, x1 - x0, b - a, c); }
        Band(Cortex.Pia, Cortex.MzB, Rgb(200, 200, 170, 0.25f)); Band(Cortex.MzB, Cortex.CpB, Rgb(170, 150, 200, 0.45f)); Band(Cortex.CpB, Cortex.SpB, Rgb(190, 170, 140, 0.35f));
        Band(Cortex.SpB, Cortex.IzB, Rgb(200, 200, 200, 0.18f)); Band(Cortex.IzB, Cortex.SvzB, Rgb(150, 170, 200, 0.35f)); Band(Cortex.SvzB, Cortex.VzB, Rgb(120, 150, 210, 0.45f));
        Band(Cortex.VzB, Cortex.VenB, Rgb(40, 60, 90, 0.4f));
        if (x0 < Cortex.XM + 200 && x1 > Cortex.XM - 200) { p.Rect(Cortex.XM - 60, Cortex.Cc0, 120, Cortex.Cc1 - Cortex.Cc0, Rgb(230, 230, 200, 0.45f)); p.Rect(Cortex.XM - Cortex.FW, Cortex.MenT, 2 * Cortex.FW, Cortex.Cc0 - 16 - Cortex.MenT, Rgb(60, 60, 50, 0.6f)); }
        if (Me?.Ax != null) { var a = Me.Ax.P; for (int i = 1; i < a.Count; i += 2) p.Seg(a[i - 1].X, a[i - 1].Y, a[i].X, a[i].Y, 6, Rgb(120, 255, 140, 0.8f)); }
    }
    public override string InspectAt(World w, float x, float y)
    {
        if (F == null) return "cortex-cp";
        foreach (var d in Dead) if (!d.Eaten && Hypot(d.X - x, d.Y - y) < d.W * 0.45f) return "pyknotic-cell";
        if (F.CellAt(x, y, out var t))
            return t switch
            {
                CortexField.CT.RG or CortexField.CT.LGE => "radial-glia", CortexField.CT.IPC => "ipc", CortexField.CT.MP => "cortneuron", CortexField.CT.SPN => "subplate-neuron",
                CortexField.CT.CPY or CortexField.CT.CPD => "cp-neuron", CortexField.CT.CR => "cajal-retzius", CortexField.CT.Men => "meninges",
                CortexField.CT.Glia => Cortex.At(x, y) switch { Cortex.Z.IG => "indusium-griseum", Cortex.Z.GW => "glial-wedge", _ => "corpus-callosum" },
                _ => "septum",
            };
        if (F.VesselDist(x, y, out _, out _) < 1.2f) return "cortex-vessel";
        if (F.NearestFiber(x, y, 1.1f, out _, out _) && y > Cortex.Pia) return "radial-glia";
        return Cortex.At(x, y) switch
        {
            Cortex.Z.MZ => "cortex-mz", Cortex.Z.CP => "cortex-cp", Cortex.Z.SP => "cortex-sp", Cortex.Z.IZ => "cortex-iz", Cortex.Z.SVZ => "cortex-svz", Cortex.Z.VZ => "cortex-vz",
            Cortex.Z.Ventricle => "ventricle", Cortex.Z.LGE => "lge", Cortex.Z.IG => "indusium-griseum", Cortex.Z.CC => "corpus-callosum", Cortex.Z.GW => "glial-wedge", Cortex.Z.Septum => "septum",
            _ => "meninges",
        };
    }

    // ------------------------------------------------ ruch w tkance: bez wchodzenia w naczynia, komorę, opony i struktury linii środkowej
    public override void Constrain(World w, Ent e)
    {
        if (F == null || e is GrowthCone || e.Holder != null) return;
        float r = e.Radius;
        // naczynia
        float dv = F.VesselDist(e.X, e.Y, out float nx, out float ny);
        if (dv < r) { e.X += nx * (r - dv); e.Y += ny * (r - dv); }
        // opona i komora
        float ymin = e is Interneuron ? Cortex.Pia + 3 : Cortex.MzB + r * 0.9f;
        if (e is Microglia) ymin = Cortex.Pia + 3;
        if (e.Y < ymin) e.Y = ymin;
        if (e.Y > Cortex.VzB - r * 0.9f) e.Y = Cortex.VzB - r * 0.9f;
        // linia środkowa: szczelina, ciało modzelowate, klin glejowy, przegroda – komórki kory tu nie wchodzą
        float dx = e.X - Cortex.XM;
        if (MathF.Abs(dx) < 70)
        {
            var z = Cortex.At(e.X, e.Y);
            if (z is Cortex.Z.Fissure or Cortex.Z.CC or Cortex.Z.GW or Cortex.Z.IG or Cortex.Z.Septum) e.X += MathF.Sign(dx == 0 ? 1 : dx) * 1.5f;
        }
        else if (Cortex.At(e.X, e.Y) == Cortex.Z.GW) e.X += MathF.Sign(dx) * 1.5f;
    }

    // ------------------------------------------------ HUD
    public static string AgeStr(float age) => age < Birth ? $"E{age:0.0}" : $"P{age - Birth:0.0}";
    string StageStr(CortNeuron n) => n.S switch
    {
        CortNeuron.Stage.Ipc => "progenitor pośredni (Tbr2⁺)",
        CortNeuron.Stage.Mitosis => "mitoza – podział symetryczny",
        CortNeuron.Stage.Multipolar => n.Ax == null ? "komórka wielobiegunowa – szuka aksonu TAG-1" : "komórka wielobiegunowa z aksonem",
        CortNeuron.Stage.Bipolar => "przejście w komórkę dwubiegunową",
        CortNeuron.Stage.Locomotion => "lokomocja po włóknie gleju",
        CortNeuron.Stage.Translocation => "translokacja końcowa",
        CortNeuron.Stage.Settled => "osiadły – szczyt płytki korowej",
        CortNeuron.Stage.Stranded => "heterotopia (istota biała)",
        _ => "apoptoza",
    };
    public override string[] KeyRows => new[] { "Wiek", "Etap" };
    public override List<(string, string)> Hud(World w)
    {
        var me = Me;
        var l = new List<(string, string)> { ("Wiek", AgeStr(Age) + (Age < Birth ? " (zarodek)" : " (po urodzeniu)")) };
        if (me == null) return l;
        bool cone = w.Player is GrowthCone;
        l.Add(("Etap", cone ? (me.Ax.AtTarget ? "akson pod korą docelową" : me.Ax.Crossed ? "stożek wzrostu – druga półkula" : "stożek wzrostu aksonu") : StageStr(me)));
        l.Add(("Ciało: od opony miękkiej", $"{Math.Max(0, me.Y - Cortex.Pia):0} µm"));
        if (me.Ax != null)
        {
            l.Add(("Akson", $"{me.Ax.Length():0} µm"));
            if (!me.Ax.Crossed) l.Add(("Stożek: do linii środkowej", $"{Math.Max(0, me.Ax.Tip.X - Cortex.XM):0} µm"));
            else if (!me.Ax.AtTarget) l.Add(("Stożek: do celu", $"{Math.Max(0, me.Ax.Tip.X - me.Ax.TargetX):0} µm"));
        }
        if (SisterN != null) l.Add(("Siostrzany neuron", !SisterN.Alive || SisterN.S == CortNeuron.Stage.Apoptotic ? "zginął (apoptoza)" : StageStr(SisterN)));
        int gfp = Cohort.Count(c => c.Alive && c.S != CortNeuron.Stage.Apoptotic), settled = Cohort.Count(c => c.Alive && c.S == CortNeuron.Stage.Settled);
        l.Add(("Kohorta GFP (osiadłe)", $"{gfp} ({settled})"));
        if (Age >= Birth + 1.5f) l.Add(("Apoptozy w płytce (od P2)", DeadPost.ToString()));
        if (Age >= Birth + 1.5f && !_fate) l.Add(("Ryzyko apoptozy Twojego neuronu", Risk() <= 0 ? "małe – dobrze włączony w sieć" : Pct(Risk())));
        return l;
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        var me = Me; if (me == null) return null;
        var l = new List<(string, float, Color)>();
        switch (me.S)
        {
            case CortNeuron.Stage.Ipc: l.Add(("Cykl komórkowy progenitora", me.Cycle, Hex("#b6d7a8"))); break;
            case CortNeuron.Stage.Mitosis: l.Add(("Mitoza", me.Mit, Hex("#b6d7a8"))); break;
            case CortNeuron.Stage.Multipolar: if (me.Ax != null) l.Add(("Synapsy z podpłytki (NMDA)", me.Syn, Hex("#e6b86a"))); break;
            case CortNeuron.Stage.Bipolar: l.Add(("Przejście w komórkę dwubiegunową", me.Trans, Hex("#e6b86a"))); break;
            case CortNeuron.Stage.Locomotion: l.Add(("Cykl lokomocji (dylatacja → skok jądra)", me.Phase, Hex("#9fd0ff"))); break;
        }
        if (me.Ax != null && me.Ax.Collapse > 0) l.Add(("Zapadnięcie stożka wzrostu", me.Ax.Collapse, Hex("#e05a4a")));
        return l.Count > 0 ? l : null;
    }
    public override string SomaticLine(World w)
    {
        var me = Me; if (me == null) return "";
        return $"Mysz: {AgeStr(Age)} · Twój neuron: {(me.S == CortNeuron.Stage.Apoptotic || !me.Alive ? "apoptoza" : "żyje")}";
    }
    public override int MapKind(World w, Ent e) => e is CortNeuron { Gfp: true } && e != Me ? 3 : 0;
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var me = Me; if (me == null || !me.Alive) return false;
        if (w.Player is GrowthCone gc)
        {
            var a = me.Ax; if (a == null) return false;
            if (!a.Crossed) { x = Cortex.XM - 20; y = (Cortex.Cc0 + Cortex.Cc1) * 0.5f; return true; }
            if (!a.AtTarget) { x = a.TargetX; y = 250; return true; }
            return false;
        }
        switch (me.S)
        {
            case CortNeuron.Stage.Ipc: x = me.X; y = (Cortex.IzB + Cortex.SvzB) * 0.5f + 8; return me.Y > Cortex.SvzB - 4 || me.Y < Cortex.IzB + 6;
            case CortNeuron.Stage.Multipolar:
                x = me.X;
                if (me.Ax == null) { y = CortexField.FascBase(CortexField.NF - 1) - 2; return me.Y > y + 25; }
                y = Cortex.SpB + 14; return true;
            case CortNeuron.Stage.Locomotion: x = me.X; y = Cortex.MzB; return true;
        }
        return false;
    }
    // ------------------------------------------------ testy i zrzuty: skok do etapu
    /// <summary>Akson od ciała: w dół do dolnej strefy pośredniej, potem wzdłuż drogi ku linii środkowej aż do x = toX.</summary>
    public void MakeAxon(CortNeuron n, float toX)
    {
        float y0 = 300;
        n.Ax = new Axon(n.X, n.Y + n.R0, n.X, y0) { TargetX = 2 * Cortex.XM - n.X };
        for (float y = n.Y + n.R0 + 4; y < y0; y += 4) n.Ax.P.Insert(n.Ax.P.Count - 1, new Vector2(n.X, y));
        float yc = (Cortex.Cc0 + Cortex.Cc1) * 0.5f + n.Ax.Lane;
        for (float x = n.X - 3; x > toX; x -= 3)
        {
            bool cr = x < Cortex.XM;
            float d = cr ? Cortex.XM - x : x - Cortex.XM;
            n.Ax.Advance(x - n.Ax.Tip.X, Lerp(yc, y0, Smooth((d - 160) / 560)) - n.Ax.Tip.Y);
            n.Ax.Crossed |= cr;
        }
        n.Ax.Dir = PI;
    }
    /// <summary>Test/zrzut: gracz od razu w danym etapie (1 SVZ, 2 wielobiegunowy, 3 pod podpłytką, 4 lokomocja w IZ, 5 w CP, 6 osiadły – stożek przed linią środkową, 7 stożek w ciele modzelowatym, 8 akson u celu w P3).</summary>
    public void DebugJump(World w, int st)
    {
        var me = Me; if (me == null || st < 1 || st > 8) return;
        float[] ages = { 15.5f, 16.2f, 16.5f, 17.2f, 17.5f, 18.0f, 18.4f, 18.8f, Birth + 3 };
        w.SimT = (ages[Math.Clamp(st, 0, 8)] - 15.5f) * 86400; F.SimT = w.SimT; F.Age = ages[Math.Clamp(st, 0, 8)];
        float x = me.X;
        void Place(float y) { me.X = x; me.Y = y; w.CamX = x; w.CamY = y; }
        switch (st)
        {
            case 1: Place(380); w.Stats["n_svz"] = 1; me.Cycle = 0.95f; break;
            case 2: Place(330); w.Stats["n_svz"] = 1; DivideIpc(me); break;
            case 3:
                Place(Cortex.SpB + 16); w.Stats["n_svz"] = 1; DivideIpc(me); SisterN.X = x + 14; SisterN.Y = Cortex.SpB + 22;
                MakeAxon(me, x - 200); me.Syn = 0.6f; w.Stats["n_axon"] = 1; me.Cone = w.Add(new GrowthCone(w, me)); break;
            case 4: case 5:
            {
                Place(st == 4 ? 250 : 110); DivideIpc(me); SisterN.S = CortNeuron.Stage.Locomotion; SisterN.X = x + 16; SisterN.Y = me.Y + 24;
                MakeAxon(me, x - 400); me.Cone = w.Add(new GrowthCone(w, me));
                me.S = CortNeuron.Stage.Locomotion; me.Lead = st == 4 ? 26 : 45;
                if (F.NearestFiber(x, me.Y, 10, out int fi, out float fx)) { me.Fiber = fi; me.Side = 1; me.X = fx + me.R0 * 0.55f; }
                if (F.NearestFiber(SisterN.X, SisterN.Y, 10, out int f2, out float fx2)) { SisterN.Fiber = f2; SisterN.Side = 1; SisterN.X = fx2 + SisterN.R0 * 0.55f; SisterN.Lead = 26; }
                foreach (var k in new[] { "n_svz", "n_div", "n_axon", "n_bipolar" }) w.Stats[k] = 1;
                break;
            }
            default:
            {
                Place(Cortex.MzB + me.R0 + 1.5f); DivideIpc(me);
                me.S = CortNeuron.Stage.Settled; me.Anchored = true; me.AnchorY = 9; me.SettledAge = 18.2f;
                SisterN.S = CortNeuron.Stage.Settled; SisterN.Anchored = true; SisterN.AnchorY = 10; SisterN.SettledAge = 18.3f; SisterN.X = x + 13; SisterN.Y = me.Y + 2;
                float to = st == 6 ? Cortex.XM + 215 : st == 7 ? Cortex.XM + 40 : me.X * 0 + (2 * Cortex.XM - x) + 60;
                MakeAxon(me, to);
                foreach (var k in new[] { "n_svz", "n_div", "n_axon", "n_bipolar", "n_settle" }) w.Stats[k] = 1;
                if (st == 8) { w.Stats["n_cross"] = 1; me.Ax.Crossed = true; }
                TakeCone(w);
                w.CamX = me.Ax.Tip.X; w.CamY = me.Ax.Tip.Y;
                break;
            }
        }
        foreach (var n in Cohort) if (n != me && n != SisterN && n.S <= CortNeuron.Stage.Mitosis && st >= 2)
            {
                // reszta kohorty: w podobnym etapie, obok
                n.S = st >= 6 ? CortNeuron.Stage.Settled : st >= 4 ? CortNeuron.Stage.Locomotion : CortNeuron.Stage.Multipolar;
                n.Y = st >= 6 ? Cortex.MzB + n.R0 + 1.5f + Rand(0, 6) : me.Y + Rand(-30, 30); n.X = x + Rand(-90, 90);
                if (n.S == CortNeuron.Stage.Settled) { n.Anchored = true; n.AnchorY = Rand(6, 12); n.SettledAge = 18.4f; }
                if (n.S == CortNeuron.Stage.Locomotion && F.NearestFiber(n.X, n.Y, 10, out int fi, out float fx)) { n.Fiber = fi; n.X = fx + n.R0 * 0.55f; n.Lead = 30; }
                if (st >= 4) MakeAxon(n, st >= 6 ? Cortex.XM + Rand(-300, 300) : n.X - Rand(200, 500));
            }
        w.UpdateChunks(true);
    }

    public override bool OnPlayerLost(World w, Ent p) => true;
    public override float Score(World w) => Me?.Ax != null ? MathF.Round(Me.Ax.Length()) : 0;
    public override int LinePop(World w) => Me != null && Me.Alive && Me.S != CortNeuron.Stage.Apoptotic ? 1 : 0;
    public override (Ent e, float size) Preview(World w)
    {
        var e = w.Add(new CortNeuron(w, 0, 0, CortNeuron.Stage.Locomotion, false) { Lead = 30 });
        return (e, 48);
    }
}

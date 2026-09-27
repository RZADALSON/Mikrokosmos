namespace Mikro;

// =====================================================================
//  T4 – skala nano, mikroskop elektronowy (świat nieskończony)
// =====================================================================
public sealed class ScT4 : Scenario
{
    public ScT4()
    {
        Id = "t4"; Name = "Bakteriofag T4"; Latin = "Tequatrovirus (Escherichia virus T4)"; TypeName = "Wirus"; Size = "≈ 0,2 µm"; Color = Hex("#e0a060");
        Desc = "Wirus bakteryjny. Nie ma napędu – dryfujesz ruchami Browna po nieskończonym szkiełku. Przyczep się do E. coli, wstrzyknij DNA i doprowadź do lizy. Uwaga na odporne mutanty.";
        Wd = 40; Ht = 28; ViewW = 4.3f; TimeScale = 0.05f; Bio = 2100; Em = true; Style = ScopeStyle.Em; Modes = EmModes; GridCell = 1.5f;
        ChunkSize = 6; ActiveR = 2; ParkCap = 80;
        BiomeNames = new[] { "Mikrokolonia E. coli", "Rozproszone komórki", "Pusta powierzchnia szkiełka", "Kolonia mutantów odpornych" };
        BiomeColors = new[] { Rgb(90, 170, 200), Rgb(70, 120, 150), Rgb(45, 55, 65), Rgb(210, 100, 90) };
        Mag = "TEM (symulacja) ≈ 25 000×"; PhageCap = 160; PhageDrift = 20; GrowEcoli = true; EcoliDouble = 14000; ScoreLabel = "wirionów";
        DebrisN = 400; DebrisS0 = 0.004f; DebrisS1 = 0.02f; DebrisD = 0.4f; DebrisCols = new[] { Rgb(200, 220, 230, 0.5f), Rgb(10, 12, 14, 0.6f) };
        Controls = "WASD / przytrzymaj LPM – dryf (symulowany mikroprzepływ; wirus nie pływa) · Shift – mocniejszy dryf · Spacja (przytrzymaj) – przyspiesz infekcję";
        Goals = new[]
        {
            new Goal("Zainfekuj pierwszą komórkę E. coli", 1, w => w.Stat("infected")),
            new Goal("Wytwórz 300 wirionów potomnych", 300, w => w.Stat("virions")),
            new Goal("Doprowadź do lizy 5 komórek", 5, w => w.Stat("lysed")),
            new Goal("Wytwórz 2000 wirionów", 2000, w => w.Stat("virions")),
        };
        Facts = new[]
        {
            "Główka: wydłużony dwudziestościan ≈ 120 × 86 nm; ogonek ≈ 100 nm; 6 długich, załamanych włókien ≈ 150–160 nm.",
            "Włókna wiążą LPS / OmpC na powierzchni E. coli → płytka podstawowa zmienia kształt → pochewka kurczy się (≈ 92 → 42 nm) i wbija rurkę przez błonę zewnętrzną.",
            "Mutanty bez receptora (OmpC⁻ lub ze zmienionym LPS) są odporne – włókna wiążą się tylko odwracalnie i wirion odpada. Bywają całe kolonie odpornych mutantów.",
            "Genom: dsDNA ≈ 169 kpz. Po infekcji fag rozkłada DNA gospodarza i przejmuje jego rybosomy.",
            "Okres utajenia ≈ 25 min w 37 °C, wydajność (burst size) ≈ 100–150 wirionów.",
            "Liza: holiny perforują błonę wewnętrzną, endolizyna tnie peptydoglikan, spaniny rozrywają błonę zewnętrzną – komórka pęka eksplozywnie, zwykle na biegunie.",
            "Nadkażenie wywołuje inhibicję lizy (LIN); DNA drugiego faga jest blokowane (wykluczenie nadkażenia, białka Imm/Sp).",
            "Ruch zwolniony 20× (×0,05), infekcja przyspieszona. Szkiełko jest nieskończone – mikrokolonie leżą rozproszone po powierzchni.",
            "Pęcherzyki błony zewnętrznej (OMV, 20–250 nm) odrywają się od E. coli i działają jak wabiki: T4 wiąże się z nimi szybko i nieodwracalnie, marnując swoje DNA (Manning i Kuehn 2011).",
            "Konkurencja: fag T7 (krótki ogonek, cykl ~15 min, degraduje DNA gospodarza) i fag λ (długi giętki ogonek – często lizogenia zamiast lizy). Komórka przejęta przez innego faga jest dla T4 stracona.",
            "Bakterie Gram-dodatnie (np. Bacillus) mają grubą ścianę peptydoglikanową i nie mają LPS ani OmpC – T4 nie ma się do czego przyczepić.",
        };
    }
    static void Colony(World w, Chunk c, List<Ent> loc, int n, float resist)
    {
        var (cx, cy) = Spot(w, c, 2.5f, loc, 1.5f);
        float a0 = Rand(0, TAU);
        for (int i = 0; i < n; i++)
        {
            var e = Put(w, loc, new EColi(w, cx + RandN() * 1.4f, cy + RandN() * 1.4f, a0 + RandN() * 0.4f, Rand(2.0f, 3.0f), sessile: true, nf: RandI(2, 4)));
            e.Resistant = Chance(resist);
        }
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        switch (c.Biome)
        {
            case 0: Colony(w, c, loc, RandI(4, 8), 0.15f); break;
            case 1: for (int i = RandI(2, 3); i > 0; i--) { var (x, y) = Spot(w, c, 1.6f, loc, 1); Put(w, loc, new EColi(w, x, y, Rand(0, TAU), sessile: true, nf: RandI(2, 4))).Resistant = Chance(0.15f); } break;
            case 2: if (Chance(0.4f)) { var (x, y) = Spot(w, c, 1.6f, loc, 1); Put(w, loc, new EColi(w, x, y, Rand(0, TAU), sessile: true, nf: 3)); } for (int i = 0; i < 2; i++) w.Add(new PhageT4(w, c.X0 + Rand(0, c.S), c.Y0 + Rand(0, c.S), Rand(0, TAU))); break;
            case 3: Colony(w, c, loc, RandI(4, 7), 0.65f); break;
        }
        if (Chance(0.4f)) w.Add(new PhageT4(w, c.X0 + Rand(0, c.S), c.Y0 + Rand(0, c.S), Rand(0, TAU)));
        // pęcherzyki błony zewnętrznej (wabiki), konkurencyjne fagi, szczątki rzęsek, bakterie Gram-dodatnie
        for (int i = RandI(1, 3) + (c.Biome is 0 or 3 ? 3 : 0); i > 0; i--) w.Add(new Omv(w, c.X0 + Rand(0, c.S), c.Y0 + Rand(0, c.S)));
        if (Chance(0.35f)) w.Add(new OtherPhage(w, c.X0 + Rand(0, c.S), c.Y0 + Rand(0, c.S), Chance(0.5f)));
        if (Chance(0.2f)) w.Add(new FlagFrag(w, c.X0 + Rand(0, c.S), c.Y0 + Rand(0, c.S)));
        if (c.Biome is 1 or 2 && Chance(0.35f)) { var (x, y) = Spot(w, c, 2, loc, 1); Put(w, loc, new Microbe(w, MicroType.Bacillus, x, y, Rand(0, TAU), RandI(1, 2))); }
    }
    public override void Init(World w)
    {
        w.UpdateChunks(true);
        var c0 = w.GetChunk(0, 0);
        float px = c0.CX, py = c0.CY;
        for (int k = 0; k < 150; k++)
        {
            bool ok = true;
            foreach (var e in w.Ents) if (e is EColi && Hypot(e.X - px, e.Y - py) < e.R + 1.2f) { ok = false; break; }
            if (ok) break;
            px = c0.X0 + Rand(0, c0.S); py = c0.Y0 + Rand(0, c0.S);
        }
        w.SetPlayer(w.Add(new PhageT4(w, px, py, Rand(0, TAU))));
        w.Stats["virions"] = 1;
        w.Log("Jesteś wirionem T4 (≈ 200 nm) na nieskończonym szkiełku. Zielone kropki na radarze to E. coli, czerwone – znane odporne komórki.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Ff = (w.Focus != null && w.Input.ActionHeld) ? 6 : 1;
        if (!w.EverySecond(gdt)) return;
        int op = 0; foreach (var e in w.Ents) if (e is OtherPhage o && o.State == St.Free && ++op > 40) o.Alive = false;
        int tot = w.Count(e => e is PhageT4 q && q.State == St.Free);
        if (tot > PhageCap)
        {
            int kill = tot - PhageCap;
            foreach (var e in w.Ents) { if (kill <= 0) break; if (e is PhageT4 q && q.Alive && q.State == St.Free && !q.Lineage && q != w.Player) { q.Alive = false; kill--; } }
        }
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player; var f = w.Focus as EColi;
        string st = "dryf";
        if (f != null && f.Inf != null)
        {
            float q = f.Inf.T / f.Inf.Lat;
            st = q < 0.12f ? "wstrzykiwanie DNA" : q < 0.3f ? "degradacja DNA gospodarza" : q < 0.5f ? "replikacja DNA faga" : q < 0.82f ? "składanie kapsydów" : q < 0.95f ? "dołączanie ogonków" : "holiny → LIZA";
            st += $" ({Pct(q)})";
        }
        else if (p != null && p.State != St.Free)
            st = p.State switch { St.Bind => "wiązanie receptora", St.Contract => "skurcz pochewki", St.Inject => "wstrzykiwanie DNA", St.Ghost => "infekcja", _ => st };
        return new()
        {
            ("Stan", st), ("Wiriony (Twoja linia)", N(w.Stat("virions"))), ("Zainfekowane komórki", N(w.Stat("infected"))), ("Lizy", N(w.Stat("lysed"))),
            ("E. coli w okolicy", w.Count(e => e is EColi c && c.State != St.Dead).ToString()), ("Wolne fagi w okolicy", w.Count(e => e is PhageT4 q && q.State == St.Free).ToString()),
        };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is EColi c && c.State != St.Dead)
        {
            if (c.Inf != null) return c.Inf.ByPlayer ? 3 : 0;
            return c.KnownResistant ? 2 : 1;
        }
        return 0;
    }
    public override float Score(World w) => w.Stat("virions");
    public override void OnLysis(World w, EColi c, bool byPlayer) { if (byPlayer) w.Banner("LIZA!", "Eksplozywne pęknięcie osłon – potomne wiriony uwolnione"); }
    public override bool OnPlayerLost(World w, Ent p)
    {
        if (w.Focus != null && w.Focus.Alive) return true;
        if (w.Adopt(Kind.Phage, p.X, p.Y)) return true;
        var alt = w.Near(p.X, p.Y, 1e9f, e => e is PhageT4 q && q.State == St.Free);
        if (alt != null) { w.Player = null; w.SetPlayer(alt); w.Log("Twoja linia zginęła – przejmujesz inny wolny wirion.", "info"); return true; }
        return false;
    }
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new PhageT4(w, 0, 0, -0.5f)); e.D = 0; e.Dr = 0; return (e, 0.42f); }
}

// =====================================================================
//  Biofilm – E. coli i Bdellovibrio (świat nieskończony)
// =====================================================================
public abstract class ScBiofilm : Scenario
{
    protected ScBiofilm()
    {
        Wd = 260; Ht = 180; ViewW = 30; TimeScale = 0.25f; Style = ScopeStyle.Phase; Modes = LightModes; GrowEcoli = true; GridCell = 6; Water = true;
        ChunkSize = 40; ActiveR = 2; ParkCap = 50;
        Mag = "Obiektyw 100× imersyjny (1000×)";
        DebrisN = 1200; DebrisS0 = 0.08f; DebrisS1 = 0.5f; DebrisD = 0.03f; DebrisCols = new[] { Rgb(40, 45, 40, 0.5f), Rgb(255, 255, 240, 0.5f), Rgb(30, 30, 30, 0.5f) };
    }
    protected static void Ecoli(World w, Chunk c, List<Ent> loc, int n) { for (int i = 0; i < n; i++) { var (x, y) = Spot(w, c, 2, loc); Put(w, loc, new EColi(w, x, y, Rand(0, TAU))); } }
    protected static void Grains(World w, Chunk c, List<Ent> loc, int n) { for (int i = 0; i < n; i++) { var (x, y) = Spot(w, c, 10, loc, 6); Put(w, loc, new Grain(w, x, y, Rand(3, 11))); } }
    protected static void Acanths(World w, Chunk c, List<Ent> loc, int n) { for (int i = 0; i < n; i++) { var (x, y) = Spot(w, c, 15, loc, 8); Put(w, loc, new Acanth(w, x, y)); } }
    protected static void Glucose(World w, Chunk c, int n, float amt) { for (int i = 0; i < n; i++) w.Nutri.Blob(c.X0 + Rand(4, c.S - 4), c.Y0 + Rand(4, c.S - 4), Rand(5, 12), amt * Rand(0.6f, 1.1f)); }
    /// <summary>Mieszkańcy biofilmu poza E. coli: inne bakterie, drożdże, wiciowce Bodo, rzadko olbrzymie orzęski Tetrahymena.</summary>
    protected static void Neighbours(World w, Chunk c, List<Ent> loc)
    {
        int b = c.Biome; bool start = c.X == 0 && c.Y == 0;
        for (int i = RandI(1, 3); i > 0; i--) { var (x, y) = Spot(w, c, 2, loc); Put(w, loc, new Microbe(w, MicroType.Pseudomonas, x, y, Rand(0, TAU))); }
        for (int i = b == 3 ? RandI(2, 4) : RandI(0, 1); i > 0; i--) { var (x, y) = Spot(w, c, 6, loc, 3); Put(w, loc, new Microbe(w, MicroType.Bacillus, x, y, Rand(0, TAU), Chance(0.4f) ? 1 : RandI(2, 5))); }
        // Caulobacter – bakteria wód ubogich (oligotroficznych): najwięcej w czystej/ubogiej wodzie i na ziarnach mineralnych
        if (Chance(b == 1 ? 0.7f : b == 3 ? 0.5f : b == 0 ? 0.08f : 0.2f))
        {
            var (x, y) = Spot(w, c, 5, loc, 5); int n = RandI(3, 6); float a0 = Rand(0, TAU);
            for (int k = 0; k < n; k++) Put(w, loc, new Microbe(w, MicroType.Caulobacter, x, y, 0, 1, a0 + k * TAU / n + Rand(-0.2f, 0.2f)));
        }
        // Spirillum volutans – mikroaerofil stojącej wody bogatej w materię organiczną, żyje tuż pod biofilmem
        if (Chance(b == 0 ? 0.6f : b == 2 ? 0.3f : 0.08f)) { var (x, y) = Spot(w, c, 20, loc, 10); Put(w, loc, new Microbe(w, MicroType.Spirillum, x, y, Rand(0, TAU))); }
        if (Chance(b == 0 ? 0.5f : 0.15f)) for (int i = RandI(1, 3); i > 0; i--) { var (x, y) = Spot(w, c, 4, loc, 3); Put(w, loc, new Microbe(w, MicroType.Yeast, x, y, Rand(0, TAU))); }
        if (!start && Chance(b == 0 ? 0.6f : 0.35f)) for (int i = RandI(1, 2); i > 0; i--) { var (x, y) = Spot(w, c, 5, loc, 3); Put(w, loc, new Protist(w, Species.Bodo, x, y, Rand(0, TAU))); }
        if (Math.Abs(c.X) + Math.Abs(c.Y) > 1 && Chance(b == 1 ? 0.25f : 0.07f)) { var (x, y) = Spot(w, c, 30, loc, 15); Put(w, loc, new Protist(w, Species.Tetrahymena, x, y, Rand(0, TAU))); }
    }
    protected static void Visitors(World w)
    {
        if (w.T > 30 && Chance(0.02f) && w.Count(e => e is Protist p && p.S == Species.Tetrahymena) < 2) { var (x, y) = SpawnAwayS(w, 30); w.Add(new Protist(w, Species.Tetrahymena, x, y, Rand(0, TAU))); }
        if (w.Count(e => e is Protist p && p.S == Species.Bodo) < 5 && Chance(0.3f)) { var (x, y) = SpawnAwayS(w, 5); w.Add(new Protist(w, Species.Bodo, x, y, Rand(0, TAU))); }
    }
    protected static (float, float) SpawnAwayS(World w, float r) => w.Sc.SpawnAway(w, r);
    protected const string BiofilmLife = "W biofilmie żyją też: Pseudomonas (jedna rzęska biegunowa, ≈ 40–50 µm/s), Bacillus (Gram+, łańcuszki, jasne przetrwalniki), olbrzymi krętek Spirillum volutans (14–60 µm, pęki ~50 rzęsek na obu biegunach; mikroaerofil – żyje pod biofilmem w wodzie bogatej w materię organiczną) i różowe drożdże Rhodotorula (częste w wodach i biofilmach). Caulobacter (trzonek z lepkim holdfastem, rozetki) to bakteria wód ubogich – najwięcej go w czystej wodzie.";
    protected const string GrazerFact = "Drapieżcy bakterii: wiciowiec Bodo saltans (3–8 µm, „podskakuje”, zagarnia bakterie krótką wicią) i orzęsek Tetrahymena (30–70 µm) – prąd z jego aparatu gębowego wciąga tysiące bakterii na godzinę. Dla Ciebie to olbrzymy.";
    protected static int FreeEcoli(World w) => w.Count(e => e is EColi c && c.State != St.Dead && c.State != St.Bdelloplast && c.State != St.Cup && c.State != St.Vac);
    protected const string AcanthFact = "Acanthamoeba: trofozoit ≈ 12–35 µm z kolczastymi akantopodiami (do ~8 µm), jądro z dużym ciemnym jąderkiem. Pożera bakterie przez fagocytozę – spotyka je głównie przypadkiem, pełzając powoli.";
    protected static void PlacePlayer(World w, Func<World, float, float, Ent> make)
    {
        var c0 = w.GetChunk(0, 0);
        var (x, y) = Spot(w, c0, 3, new List<Ent>(), 4);
        for (int k = 0; k < 50 && (w.Near(x, y, 30, e => e is Acanth) != null || (w.Amp != null && w.Amp.Sample(x, y) > 0.05f)); k++) (x, y) = Spot(w, c0, 3, new List<Ent>(), 4);
        w.SetPlayer(w.Add(make(w, x, y)));
    }
}

public sealed class ScBdello : ScBiofilm
{
    public ScBdello()
    {
        Id = "bdello"; Name = "Bdellovibrio"; Latin = "Bdellovibrio bacteriovorus"; TypeName = "Bakteria drapieżna"; Size = "≈ 0,3 × 1 µm"; Color = Hex("#9fd0a0");
        Desc = "Najszybszy drapieżnik mikroświata (do 160 µm/s). Przemierzaj nieskończony biofilm, wwierć się w E. coli i zjedz ją od środka. Omijaj pełzające Acanthamoeby.";
        Bio = 2000; PhageCap = 30; Starve = 160; ScoreLabel = "ofiar";
        BiomeNames = new[] { "Biofilm E. coli", "Czysta woda", "Kolonia Acanthamoeb", "Złoża mineralne" };
        BiomeColors = new[] { Rgb(120, 200, 120), Rgb(90, 140, 170), Rgb(210, 110, 90), Rgb(170, 160, 130) };
        Controls = "WASD / przytrzymaj LPM – płyń · Shift – sprint 160 µm/s · Spacja (przytrzymaj) – przyspiesz czas wewnątrz ofiary";
        Goals = new[]
        {
            new Goal("Upoluj pierwszą ofiarę", 1, w => w.Stat("kills")),
            new Goal("Twoja populacja: 8 komórek", 8, w => w.CountAll(e => e is Bdello && e.Lineage)),
            new Goal("Zjedz 10 ofiar", 10, w => w.Stat("kills")),
            new Goal("Twoja populacja: 40 komórek", 40, w => w.CountAll(e => e is Bdello && e.Lineage)),
        };
        Facts = new[]
        {
            "Rozmiar ≈ 0,2–0,5 × 0,5–2,5 µm, jedna osłonięta rzęska biegunowa. Prędkość ≈ 35–160 µm/s – nawet ~100 długości ciała na sekundę.",
            "Poluje na bakterie Gram-ujemne (np. E. coli). Przyczepia się biegunem, enzymatycznie „wierci” otwór w błonie zewnętrznej i wnika do peryplazmy.",
            "Ofiara ginie i zaokrągla się w bdeloplast. Bdellovibrio rośnie w niej jako długi filament, trawiąc zawartość komórki.",
            "Filament dzieli się na 2 (małe ofiary) lub 3–6+ (duże ofiary) potomków, które lizują resztki komórki i odpływają.",
            "Komórki w fazie ataku nie rosną – bez ofiary tracą energię (w grze pasek „Energia”).",
            AcanthFact, GrazerFact, BiofilmLife,
            "Cały cykl trwa ≈ 3–4 h. W grze: ruch zwolniony 4× (×0,25), cykl życiowy mocno przyspieszony. Liczba Reynoldsa ≈ 10⁻⁴ – brak bezwładności.",
        };
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        w.Nutri.Ensure(c.X, c.Y);
        switch (c.Biome)
        {
            case 0: Glucose(w, c, 2, 0.5f); Grains(w, c, loc, RandI(0, 1)); Ecoli(w, c, loc, RandI(10, 14)); break;
            case 1: Glucose(w, c, 1, 0.2f); Ecoli(w, c, loc, RandI(2, 4)); break;
            case 2: Glucose(w, c, 1, 0.3f); Acanths(w, c, loc, 2); Ecoli(w, c, loc, RandI(4, 6)); break;
            case 3: Grains(w, c, loc, RandI(4, 6)); Ecoli(w, c, loc, RandI(5, 7)); break;
        }
        Neighbours(w, c, loc);
        if (Chance(0.12f)) { var (x, y) = Spot(w, c, 2, loc); Put(w, loc, new Bdello(w, x, y, Rand(0, TAU))); }
    }
    public override void Init(World w)
    {
        w.Nutri = new TileField(ChunkSize, 32);
        w.UpdateChunks(true);
        PlacePlayer(w, (ww, x, y) => new Bdello(ww, x, y, Rand(0, TAU)));
        w.Log("Jesteś Bdellovibrio. Biofilm ciągnie się bez końca – zielone na radarze to E. coli, czerwone to Acanthamoeby.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Nutri.Diffuse(dt, 3, w.Active);
        var p = w.Player;
        w.Ff = (p != null && p.State != St.Swim && w.Input.ActionHeld) ? 5 : 1;
        if (!w.EverySecond(gdt)) return;
        if (FreeEcoli(w) < 70) for (int i = 0; i < 2; i++) { var (x, y) = SpawnAway(w, 2); w.Add(new EColi(w, x, y, Rand(0, TAU))); }
        Visitors(w);
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player;
        string st = p == null ? "-" : p.State switch { St.Swim => "faza ataku (pływanie)", St.Attach => "przyczepienie, wiercenie", St.Enter => "wnikanie do peryplazmy", St.Grow => "wzrost filamentu (bdeloplast)", St.Septate => "podział na potomków", St.Cup => "pochłaniany!", St.Vac => "trawiony w wodniczce", _ => "-" };
        return new() { ("Stan", st), ("Zjedzone ofiary", N(w.Stat("kills"))), ("Potomkowie", N(w.Stat("born"))), ("Twoja populacja", w.CountAll(e => e is Bdello && e.Lineage).ToString()), ("E. coli w okolicy", FreeEcoli(w).ToString()) };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Bdello b) return null;
        return new() { ("Energia (faza ataku)", 1 - b.Hunger, b.Hunger > 0.7f ? Hex("#e05a4a") : Hex("#8fe0a0")) };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is EColi c && c.CanHost()) return 1;
        if (e is Acanth || e is Protist) return 2;
        return e is Bdello && e.Lineage && e != w.Player ? 3 : 0;
    }
    public override void DrawMinimap(World w, Painter p) => w.Nutri.DrawMap(p, w.WinX0, w.WinY0, w.WinX0 + w.WinW, w.WinY0 + w.WinH, Rgb(255, 140, 20), 0.35f);
    public override float Score(World w) => w.Stat("kills");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Bdello(w, 0, 0, 0)); e.D = e.Dr = 0; return (e, 3.4f); }
}

public sealed class ScEcoli : ScBiofilm
{
    public ScEcoli()
    {
        Id = "ecoli"; Name = "Escherichia coli"; Latin = "Escherichia coli"; TypeName = "Bakteria"; Size = "≈ 2 × 0,8 µm"; Color = Hex("#b8c7a8");
        Desc = "Szukaj glukozy w nieskończonym biofilmie i dziel się co ~20 min. Omijaj strefy ampicyliny, drapieżne Bdellovibrio, Acanthamoeby i fagi T4.";
        Bio = 250; PhageCap = 60; ScoreLabel = "komórek (maks. populacja)";
        BiomeNames = new[] { "Bogata pożywka", "Uboga strefa", "Strefa ampicyliny", "Łowisko drapieżników" };
        BiomeColors = new[] { Rgb(230, 150, 50), Rgb(120, 130, 140), Rgb(220, 50, 110), Rgb(200, 90, 70) };
        Controls = "WASD / przytrzymaj LPM – bieg (run) · Spacja – koziołek (tumble; strząsa drapieżnika) · Shift – szybciej · G – mapy stężeń";
        Goals = new[]
        {
            new Goal("Podziel się po raz pierwszy", 1, w => w.Stat("divisions")),
            new Goal("Twoja populacja: 5 komórek", 5, w => w.Stat("maxpop")),
            new Goal("Zjedz 25 jednostek glukozy", 25, w => w.Stat("eaten")),
            new Goal("Twoja populacja: 20 komórek", 20, w => w.Stat("maxpop")),
        };
        Facts = new[]
        {
            "Pałeczka ≈ 1–2 (do 4) µm × 0,5–1 µm, 4–8 rzęsek perytrychalnych (≈ 5–10 µm, filament o skoku ≈ 2,3 µm).",
            "Pływa „bieg i koziołek”: bieg ≈ 1 s przy ≈ 20–30 µm/s, koziołek ≈ 0,1 s (obrót średnio ≈ 68°). Chemotaksja wydłuża biegi w górę gradientu. W grze sterowanie jest uproszczeniem.",
            "W bogatej pożywce dzieli się co ≈ 20 min: wydłuża się, nukleoid się rozdziela, pierścień FtsZ zaciska przegrodę.",
            "Ampicylina (β-laktam) blokuje białka PBP budujące peptydoglikan. Najbardziej wrażliwy jest podział: w miejscu przegrody powstaje wybrzuszenie, które pęcznieje, aż komórka pęka (liza wybrzuszeniowa). Czerwona mapa = stężenie ampicyliny.",
            AcanthFact, GrazerFact, BiofilmLife,
            "Mapy glukozy i ampicyliny to wizualizacje pomocnicze (cząsteczek nie widać w mikroskopie); dyfuzja w grze jest spowolniona.",
        };
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        w.Nutri.Ensure(c.X, c.Y); w.Amp.Ensure(c.X, c.Y);
        switch (c.Biome)
        {
            case 0: Glucose(w, c, 3, 0.9f); Ecoli(w, c, loc, RandI(3, 4)); Grains(w, c, loc, RandI(0, 1)); break;
            case 1: Glucose(w, c, 1, 0.3f); Ecoli(w, c, loc, 2); break;
            case 2:
                var src = new Vector2(c.X0 + Rand(10, c.S - 10), c.Y0 + Rand(10, c.S - 10));
                c.Sources.Add(src); w.Amp.Blob(src.X, src.Y, 14, 1.2f);
                Glucose(w, c, 1, 0.8f); Ecoli(w, c, loc, 2); break;
            case 3:
                Glucose(w, c, 2, 0.6f); Ecoli(w, c, loc, 3); Acanths(w, c, loc, 1);
                for (int i = 0; i < 2; i++) { var (x, y) = Spot(w, c, 2, loc); Put(w, loc, new Bdello(w, x, y, Rand(0, TAU))); }
                for (int i = 0; i < 2; i++) w.Add(new PhageT4(w, c.X0 + Rand(0, c.S), c.Y0 + Rand(0, c.S), Rand(0, TAU)));
                if (Chance(0.4f)) { var (x, y) = Spot(w, c, 30, loc, 15); Put(w, loc, new Protist(w, Species.Tetrahymena, x, y, Rand(0, TAU))); }
                break;
        }
        Neighbours(w, c, loc);
    }
    public override void Init(World w)
    {
        w.Nutri = new TileField(ChunkSize, 32);
        w.Amp = new TileField(ChunkSize, 16);
        w.UpdateChunks(true);
        PlacePlayer(w, (ww, x, y) => new EColi(ww, x, y, Rand(0, TAU), 2.2f));
        w.Nutri.Blob(w.Player.X, w.Player.Y, 7, 0.8f);
        w.ShowField = true;
        w.Log("Jesteś E. coli. Pomarańczowa mapa = glukoza, czerwona = ampicylina (G – wyłącz). Biofilm nie ma końca – szukaj bogatych stref.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Nutri.Diffuse(dt, 4, w.Active);
        w.Amp.Diffuse(dt, 2.5f, w.Active);
        if (w.EverySecond(gdt))
        {
            foreach (var c in w.Active) foreach (var s in c.Sources) w.Amp.Blob(s.X, s.Y, 10, 0.08f);
            if (Chance(0.5f)) { var c = w.Active[RandI(0, w.Active.Count - 1)]; if (c.Biome != 1) Glucose(w, c, 1, 0.6f); }
            if (w.Count(e => e is EColi c && c.State != St.Dead && !c.Lineage) < 45) { var (x, y) = SpawnAway(w, 2); w.Add(new EColi(w, x, y, Rand(0, TAU))); }
            if (w.Count(e => e is PhageT4 q && q.State == St.Free) < 6) { var (x, y) = SpawnAway(w, 1); w.Add(new PhageT4(w, x, y, Rand(0, TAU))); }
            if (w.T > 45 && w.Count(e => e is Bdello) < 5 && Chance(0.05f)) { var (x, y) = SpawnAway(w, 3); w.Add(new Bdello(w, x, y, Rand(0, TAU))); }
            Visitors(w);
            int pop = Pop(w);
            if (pop > w.Stat("maxpop")) w.Stats["maxpop"] = pop;
        }
        if (w.Player is EColi p && p.Inf != null && !p.Warned) { p.Warned = true; w.Log("Fag T4 wstrzyknął DNA do Twojej komórki! Za chwilę liza...", "bad"); }
    }
    static int Pop(World w) => w.CountAll(e => e is EColi c && c.Lineage && c.State != St.Dead && c.State != St.Bdelloplast && c.State != St.Cup && c.State != St.Vac);
    public override void DrawUnder(World w, Painter p, View v)
    {
        if (!w.ShowField) return;
        w.Nutri.Draw(p, v, Rgb(255, 140, 20), 0.42f);
        w.Amp.Draw(p, v, Rgb(230, 30, 90), 0.4f);
    }
    public override void DrawMinimap(World w, Painter p)
    {
        float x0 = w.WinX0, y0 = w.WinY0, x1 = x0 + w.WinW, y1 = y0 + w.WinH;
        w.Nutri.DrawMap(p, x0, y0, x1, y1, Rgb(255, 140, 20), 0.5f);
        w.Amp.DrawMap(p, x0, y0, x1, y1, Rgb(230, 30, 90), 0.55f);
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as EColi;
        return new()
        {
            ("Długość komórki", p != null ? $"{p.L:0.00} µm" : "-"), ("Podział przy", p != null ? $"{p.L0 * 2:0.00} µm" : "-"),
            ("Glukoza pod Tobą", p != null ? Pct(w.Nutri.Sample(p.X, p.Y)) : "-"), ("Ampicylina pod Tobą", p != null ? Pct(w.Amp.Sample(p.X, p.Y)) : "-"),
            ("Twoja populacja", Pop(w).ToString()), ("Podziały", N(w.Stat("divisions"))),
        };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Bdello || e is Acanth || e is Protist) return 2;
        return e is EColi c && c.Lineage && e != w.Player && c.State != St.Dead ? 3 : 0;
    }
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var p = w.Player; if (p == null) return false;
        float best = 0.25f;
        for (int k = 0; k < 32; k++)
        {
            float a = k * TAU / 16, r = ViewW * (k < 16 ? 0.7f : 1.6f);
            float qx = p.X + MathF.Cos(a) * r, qy = p.Y + MathF.Sin(a) * r;
            float val = w.Nutri.Sample(qx, qy) - w.Amp.Sample(qx, qy) * 2;
            if (val > best) { best = val; x = qx; y = qy; }
        }
        return best > 0.25f;
    }
    public override float Score(World w) => w.Stat("maxpop");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new EColi(w, 0, 0, 0, 2.3f)); e.D = e.Dr = 0; return (e, 8); }
}

// =====================================================================
//  Tkanka – neutrofil, sieć żyłek, kolagen (świat nieskończony)
// =====================================================================
public class ScNeutro : Scenario
{
    protected const float VHW = 16;   // połowa średnicy żyłki (µm)
    protected const float VS = 420;   // odstęp między żyłkami (µm)
    public ScNeutro()
    {
        Id = "neutro"; Name = "Neutrofil"; Latin = "Granulocyt obojętnochłonny (Homo sapiens)"; TypeName = "Komórka odporności"; Size = "≈ 12 µm"; Color = Hex("#c9b8d8");
        Desc = "Patroluj nieskończoną tkankę między żyłkami. Przeciskaj się przez kolagen, szukaj ognisk zakażenia i pochłaniaj bakterie. Uważaj na leukocydyny – w ostateczności wyrzuć sieć NET.";
        Wd = 520; Ht = 360; ViewW = 72; TimeScale = 40; Bio = 1; Style = ScopeStyle.Tissue; Modes = LightModes; FlowX = 0.002f; GridCell = 20;
        ChunkSize = 100; ActiveR = 2; ParkCap = 80;
        BiomeNames = new[] { "Tkanka łączna luźna", "Tkanka zbita (kolagen)", "Ognisko zakażenia", "Wybroczyna (wylew krwi)" };
        BiomeColors = new[] { Rgb(200, 190, 170), Rgb(235, 230, 215), Rgb(225, 205, 60), Rgb(190, 80, 80) };
        Mag = "Obiektyw 40× (400×) – time-lapse"; ScoreLabel = "zabitych bakterii";
        DebrisN = 700; DebrisS0 = 0.3f; DebrisS1 = 1.4f; DebrisD = 0.01f; DebrisCols = new[] { Rgb(60, 55, 50, 0.5f), Rgb(255, 255, 240, 0.5f) };
        Controls = "WASD / przytrzymaj LPM – pełzanie (chemotaksja) · dotknij bakterii, by ją sfagocytować · Spacja (przytrzymaj) – NEToza";
        Goals = new[]
        {
            new Goal("Sfagocytuj 5 bakterii", 5, w => w.Stat("engulfed")),
            new Goal("Zabij 25 bakterii", 25, w => w.Stat("killed")),
            new Goal("Oczyść ognisko zakażenia (zabij łącznie 60 bakterii)", 60, w => w.Stat("killed")),
            new Goal("Zabij 150 bakterii", 150, w => w.Stat("killed")),
        };
        LoseText = "Wszystkie neutrofile zginęły";
        Facts = new[]
        {
            "Średnica ≈ 12 µm; jądro 3–5-płatowe; setki ziarnistości (azurofilne z mieloperoksydazą, swoiste z laktoferyną).",
            "Pełzanie ameboidalne ≈ 10–20 µm/min: z przodu lamellipodium (bez ziarnistości), z tyłu uropod. W tkance przeciska się między włóknami kolagenu.",
            "Neutrofile opuszczają krew w żyłkach pozawłośniczkowych (toczenie, adhezja, diapedeza) – w grze nowe neutrofile wychodzą z najbliższej żyłki.",
            "Fagocytoza → fagosom: NADPH-oksydaza wytwarza reaktywne formy tlenu, ziarnistości się z nim zlewają (degranulacja). Neutrofil nie odtwarza ziarnistości.",
            "Leukocydyny S. aureus (np. PVL – typowa dla szczepów z zakażeń martwiczych) tworzą pory w błonie neutrofila. Martwy neutrofil uwalnia niestrawione bakterie.",
            "NEToza (Brinkmann i in., Science 2004): neutrofil wyrzuca sieć chromatyny z histonami i elastazą, która wiąże i zabija bakterie – kosztem własnego życia.",
            "S. aureus dzieli się w 3 płaszczyznach („grona”), Streptococcus w jednej – stąd łańcuszki. Ogniska zakażenia to miejsca, gdzie bakterie mnożą się najszybciej.",
            "Wybroczyna: erytrocyty, które wydostały się z uszkodzonych naczyń do tkanki. W time-lapse ×40 przepływ krwi w żyłkach widać jako rozmyte smugi.",
            "Makrofagi tkankowe (~20 µm) pełzają wolniej niż neutrofile, pochłaniają bakterie i drożdże, a po zapaleniu sprzątają martwe neutrofile (eferocytoza).",
            "Mastocyty są wypełnione ziarnistościami z histaminą. Pobudzone przez bakterie degranulują – naczynia się rozszerzają i napływa więcej neutrofili.",
            "Tkanka tłuszczowa: adipocyty 50–150 µm, jedna kropla lipidu i jądro zepchnięte do brzegu. Fibroblasty to wrzecionowate komórki wytwarzające kolagen.",
            "Candida albicans: drożdżaki (4–6 µm) można sfagocytować, ale strzępki są za duże. Neutrofile rozpoznają rozmiar drobnoustroju i na duże strzępki wyrzucają sieci NET (Branzk i in., Nat Immunol 2014) – przytrzymaj Spację przy strzępce.",
        };
    }
    float Ph(World w, int k) => Hash2(k, 7, w.Seed) * TAU;
    protected float Yc(World w, int k, float x) => k * VS + 26 * MathF.Sin(x * TAU / 330 + Ph(w, k));
    static int VIdx(float y) => (int)MathF.Round(y / VS);
    /// <summary>Środek najbliższej żyłki w danym x (testy, AI).</summary>
    public float VesselY(World w, float x, float y) => Yc(w, VIdx(y), x);
    protected bool InVessel(World w, float x, float y, float margin) { int k = VIdx(y); return MathF.Abs(y - Yc(w, k, x)) < VHW + margin; }

    void Bact(World w, Chunk c, List<Ent> loc, int clusters, int chains)
    {
        for (int i = 0; i < clusters; i++)
        {
            var (x, y) = Spot(w, c, 3, loc, 4); if (InVessel(w, x, y, 6)) continue;
            for (int k = RandI(1, 4); k > 0; k--) w.Add(new Staph(w, x + RandN() * 0.8f, y + RandN() * 0.8f));
        }
        for (int i = 0; i < chains; i++)
        {
            var (x, y) = Spot(w, c, 3, loc, 4); if (InVessel(w, x, y, 6)) continue;
            float a = Rand(0, TAU);
            for (int k = 0; k < 3; k++) w.Add(new Staph(w, x + MathF.Cos(a) * k * 0.8f, y + MathF.Sin(a) * k * 0.8f, plane: a, strep: true));
        }
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        int col = c.Biome switch { 0 => 6, 1 => 18, 2 => 5, _ => 4 };
        for (int i = 0; i < col; i++) { var (x, y) = Spot(w, c, 8, loc, 5); if (!InVessel(w, x, y, 12)) Put(w, loc, new Collagen(w, x, y, Rand(0, TAU), Rand(25, 80), Rand(0.5f, 0.9f))); }
        // krwinki czerwone i płytki trafiają do tkanki tylko przy uszkodzeniu naczyń (wylew) albo w stanie zapalnym
        int rbc = c.Biome == 3 ? 14 : c.Biome == 2 ? RandI(0, 2) : 0, plt = c.Biome == 3 ? 8 : c.Biome == 2 ? RandI(0, 2) : 0;
        for (int i = 0; i < rbc; i++) { var (x, y) = Spot(w, c, 4.5f, loc, 5); if (!InVessel(w, x, y, 6)) Put(w, loc, new RBC(w, x, y)); }
        for (int i = 0; i < plt; i++) { var (x, y) = Spot(w, c, 1.5f, loc, 2); if (!InVessel(w, x, y, 3)) Put(w, loc, new Platelet(w, x, y)); }
        if (Chance(0.2f)) { var (x, y) = Spot(w, c, 5, loc, 6); if (!InVessel(w, x, y, 6)) Put(w, loc, new Lymphocyte(w, x, y)); }
        // komórki własne tkanki: tłuszczowe, fibroblasty, mastocyty, makrofagi
        if (c.Biome == 0 && Chance(0.4f) && Math.Abs(c.X) + Math.Abs(c.Y) > 1)
        {
            var (cx, cy) = Spot(w, c, 50, loc, 30);
            for (int k = RandI(4, 8); k > 0; k--)
            {
                float r = Rand(26, 48), x = cx + RandN() * 40, y = cy + RandN() * 40;
                if (InVessel(w, x, y, r + 4)) continue;
                bool ok = true; foreach (var e in loc) if (e.Solid && Hypot(e.X - x, e.Y - y) < e.R + r * 0.9f) { ok = false; break; }
                if (ok) Put(w, loc, new Adipocyte(w, x, y, r));
            }
        }
        for (int i = RandI(1, 2) + (c.Biome == 1 ? 2 : 0); i > 0; i--) { var (x, y) = Spot(w, c, 30, loc, 10); if (!InVessel(w, x, y, 14)) Put(w, loc, new Fibroblast(w, x, y, Rand(0, TAU))); }
        for (int i = RandI(0, 2); i > 0; i--) { var (x, y) = Spot(w, c, 7, loc, 8); if (!InVessel(w, x, y, 8)) Put(w, loc, new MastCell(w, x, y)); }
        if (Chance(c.Biome == 2 ? 0.9f : 0.35f)) { var (x, y) = Spot(w, c, 12, loc, 10); if (!InVessel(w, x, y, 12)) Put(w, loc, new Macrophage(w, x, y)); }
        if (Math.Abs(c.X) + Math.Abs(c.Y) <= 1) { Bact(w, c, loc, RandI(1, 2), 0); return; }
        // Candida: drożdżaki w ognisku zakażenia, czasem już ze strzępką
        if (c.Biome == 2 && Chance(0.35f))
        {
            var (x, y) = Spot(w, c, 6, loc, 8);
            if (!InVessel(w, x, y, 8)) { for (int k = RandI(3, 6); k > 0; k--) w.Add(new CandidaYeast(w, x + RandN() * 3, y + RandN() * 3)); if (Chance(0.5f)) w.Add(new CandidaHypha(w, x + 5, y, Rand(0, TAU))); }
        }
        switch (c.Biome)
        {
            case 2: Bact(w, c, loc, RandI(5, 8), RandI(1, 3)); break;
            case 0: if (Chance(0.45f)) Bact(w, c, loc, RandI(1, 2), Chance(0.3f) ? 1 : 0); break;
            default: if (Chance(0.25f)) Bact(w, c, loc, 1, 0); break;
        }
    }
    protected Neutrophil Diapedesis(World w, float x, float nearY)
    {
        int k = VIdx(nearY);
        float side = nearY >= Yc(w, k, x) ? 1 : -1, y = Yc(w, k, x) + side * (VHW + 8);
        var n = w.Add(new Neutrophil(w, x, y, side > 0 ? PI / 2 : -PI / 2)); n.Lineage = true;
        return n;
    }
    public override void Init(World w)
    {
        w.CamX = 50; w.CamY = 60;
        w.UpdateChunks(true);
        var me = Diapedesis(w, 50, 60);
        w.SetPlayer(me);
        Diapedesis(w, -60, 60); Diapedesis(w, 170, -60);
        w.Log("Jesteś neutrofilem, który wyszedł z żyłki. Tkanka ciągnie się bez końca – szukaj ognisk zakażenia (żółte strefy na radarze).", "info");
    }
    public override void Constrain(World w, Ent e)
    {
        if (e.Fixed || e is Net) return;
        int k = VIdx(e.Y); float yc = Yc(w, k, e.X), dy = e.Y - yc, m = VHW + Math.Min(e.Prim.R, 6) * 0.8f;
        if (MathF.Abs(dy) < m) e.Y = yc + (dy >= 0 ? m : -m);
    }
    public override void Update(World w, float dt, float gdt)
    {
        if (!w.EverySecond(gdt)) return;
        int n = w.Count(e => e is Staph s && s.State == St.Free);
        if (n > 400) w.GameOver($"Infekcja w okolicy wymknęła się spod kontroli ({n} bakterii)");
        if (w.T > 5 && (int)w.T % 100 == 0)
        {
            w.Wave++;
            w.Banner($"Fala {w.Wave}", "Kolejne bakterie przedostają się do tkanki...");
            foreach (var c in w.Active) if (Chance(0.25f + w.Wave * 0.04f)) Bact(w, c, new List<Ent>(), RandI(1, 1 + w.Wave), w.Wave > 2 ? 1 : 0);
        }
        if (w.Stat("histamine") > w.Stat("histDone") && w.Player != null)
        {
            w.AddStat("histDone", 1);
            if (w.Count(e => e is Neutrophil) < 9)
            {
                Diapedesis(w, w.Player.X + Rand(-80, 80), w.Player.Y);
                w.LogOnce("hist", "Histamina z mastocytu → rozszerzenie naczyń: z żyłki wychodzą kolejne neutrofile.", "good", 30);
            }
        }
        int neut = w.Count(e => e is Neutrophil);
        if (neut < 5 && (w.T % 35f) < gdt * 1.5f && w.Player != null)
        {
            Diapedesis(w, w.Player.X + Rand(-150, 150), w.Player.Y);
            w.Log("Diapedeza: nowy neutrofil przeciska się przez śródbłonek żyłki.", "info");
        }
    }
    void DrawVessel(World w, Painter p, int k, float x0, float x1)
    {
        int n = (int)Clamp((x1 - x0) / 4, 8, 400);
        var top = p.Shape; var bot = p.Shape2;
        for (int i = 0; i <= n; i++) { float x = x0 + (x1 - x0) * i / n, yc = Yc(w, k, x); top[i] = new Vector2(x, yc - VHW); bot[i] = new Vector2(x, yc + VHW); }
        Span<Vector2> quad = stackalloc Vector2[4];
        for (int i = 0; i < n; i++)
        {
            quad[0] = top[i]; quad[1] = top[i + 1]; quad[2] = bot[i + 1]; quad[3] = bot[i];
            p.FillFan(quad, (top[i].X + top[i + 1].X) / 2, (top[i].Y + bot[i].Y) / 2, Rgb(170, 110, 100, 0.35f), false);
        }
        float t = w.T, ph = Ph(w, k);
        const float period = 1400;
        float xb = MathF.Floor(x0 / period) * period;
        for (int j = 0; j < 90; j++)
        {
            float lane = (j * 0.618f) % 1f * 2 - 1, spd = 900 * (1 - lane * lane);
            for (float xo = xb; xo < x1 + period; xo += period)
            {
                float x = xo + ((j * 173.3f + t * spd) % period);
                if (x < x0 - 30 || x > x1 + 30) continue;
                float yc = Yc(w, k, x), y = yc + lane * VHW * 0.75f, ang = MathF.Atan(26 * TAU / 330 * MathF.Cos(x * TAU / 330 + ph));
                p.Push(); p.Translate(x, y); p.Rotate(ang);
                p.Glow(0, 0, 9, Rgb(150, 60, 55, 0.35f));
                p.FillEllipse(0, 0, 14, 3.2f, 0, Rgb(140, 70, 65, 0.25f));
                p.Pop();
            }
        }
        var topS = new ReadOnlySpan<Vector2>(top, 0, n + 1); var botS = new ReadOnlySpan<Vector2>(bot, 0, n + 1);
        p.SoftStroke(topS, false, 4, Rgb(255, 255, 240, 0.25f)); p.SoftStroke(botS, false, 4, Rgb(255, 255, 240, 0.25f));
        p.Stroke(topS, false, 1.2f, Rgb(90, 70, 70, 0.6f)); p.Stroke(botS, false, 1.2f, Rgb(90, 70, 70, 0.6f));
        for (float x = MathF.Floor(x0 / 38) * 38; x < x1; x += 38)
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float xx = x + (sd > 0 ? 19 : 0), yc = Yc(w, k, xx), ang = MathF.Atan(26 * TAU / 330 * MathF.Cos(xx * TAU / 330 + ph));
                p.FillEllipse(xx, yc + sd * (VHW - 1.2f), 7, 1.4f, ang, Rgb(90, 80, 110, 0.45f));
            }
    }
    public override void DrawUnder(World w, Painter p, View v)
    {
        int k0 = VIdx(v.Y0 - 60), k1 = VIdx(v.Y1 + 60);
        for (int k = k0; k <= k1; k++) DrawVessel(w, p, k, v.X0 - 40, v.X1 + 40);
    }
    public override void DrawMinimap(World w, Painter p)
    {
        float x0 = w.WinX0, x1 = w.WinX0 + w.WinW;
        int k0 = (int)MathF.Ceiling((w.WinY0 + VHW + 30) / VS), k1 = (int)MathF.Floor((w.WinY0 + w.WinH - VHW - 30) / VS);
        const int n = 60; Span<Vector2> s = stackalloc Vector2[n + 1];
        for (int k = k0; k <= k1; k++)
        {
            for (int i = 0; i <= n; i++) { float x = x0 + (x1 - x0) * i / n; s[i] = new Vector2(x, Yc(w, k, x)); }
            p.Stroke(s, false, VHW * 2, Rgb(170, 70, 70, 0.55f));
        }
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Neutrophil;
        return new()
        {
            ("Sfagocytowane", N(w.Stat("engulfed"))), ("Zabite bakterie", N(w.Stat("killed"))),
            ("Fagosomy", p != null ? $"{p.Phagos.Count}/{p.MaxPhago}" : "-"), ("Ziarnistości", p != null ? Pct((float)p.GranLeft / p.G) : "-"),
            ("Bakterie w okolicy", w.Count(e => e is Staph s && s.State == St.Free).ToString()), ("Neutrofile", w.Count(e => e is Neutrophil).ToString()),
            ("Rezerwa z krwi", N(5 - w.Stat("reinf"))), ("Fala", w.Wave.ToString()),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Neutrophil p) return null;
        var l = new List<(string, float, Color)> { ("Integralność błony", p.Hp, p.Hp < 0.35f ? Hex("#e05a4a") : Hex("#c9b8d8")) };
        if (p.NetCharge > 0) l.Add(("NEToza – trzymaj Spację", p.NetCharge, Hex("#9cd2ff")));
        return l;
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Staph s && s.State == St.Free) return 1;
        if (e is CandidaYeast cy && cy.State == St.Free || e is CandidaHypha) return 1;
        return (e is Neutrophil || e is Macrophage) && e != w.Player ? 3 : 0;
    }
    public override bool OnPlayerLost(World w, Ent p)
    {
        var n = w.Near(p.X, p.Y, 1e9f, e => e is Neutrophil);
        if (n == null && w.Stat("reinf") < 5)
        {
            w.AddStat("reinf", 1);
            n = Diapedesis(w, p.X, p.Y);
            w.Log($"Posiłki: z żyłki wychodzi kolejny neutrofil (rezerwa {5 - w.Stat("reinf"):0}).", "info");
        }
        if (n != null) { w.Player = null; w.SetPlayer(n); w.Log("Przejmujesz kontrolę nad kolejnym neutrofilem.", "info"); return true; }
        return false;
    }
    public override float Score(World w) => w.Stat("killed");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Neutrophil(w, 0, 0, 0)); return (e, 22); }
}

/// <summary>
/// Makrofag tkankowy: ten sam świat tkanki co neutrofil, ale inna rola – strażnik i sprzątacz.
/// Wzywa neutrofile, pochłania drobnoustroje, erytrocyty z wylewów i zużyte (apoptotyczne) neutrofile.
/// </summary>
public sealed class ScMacro : ScNeutro
{
    public ScMacro()
    {
        Id = "macro"; Name = "Makrofag"; Latin = "Makrofag tkankowy (Homo sapiens)"; TypeName = "Komórka odporności (fagocyt)"; Size = "≈ 20–25 µm"; Color = Hex("#cbbf9c");
        Desc = "Powolny, ale żarłoczny strażnik tkanki. Pochłaniaj bakterie i drożdże, cytokinami wzywaj neutrofile, a potem po nich sprzątaj: zjadaj umierające neutrofile, zanim pękną, i erytrocyty z wylewów.";
        ViewW = 95; TimeScale = 90; Bio = 0.5f; NeutroLife = 4 * 3600;
        Mag = "Obiektyw 40× (400×) – time-lapse ×90"; ScoreLabel = "punktów sprzątania";
        Controls = "WASD / przytrzymaj LPM – pełzanie · dotknij celu, by go otoczyć i sfagocytować · Spacja – cytokiny (wezwanie neutrofili z żyłki)";
        Goals = new[]
        {
            new Goal("Sfagocytuj 8 bakterii lub drożdży", 8, w => w.Stat("m_eng")),
            new Goal("Wezwij neutrofile cytokinami (Spacja)", 1, w => w.Stat("m_signal")),
            new Goal("Eferocytoza: pochłoń 3 apoptotyczne neutrofile", 3, w => w.Stat("m_effero")),
            new Goal("Oczyść wylew: pochłoń 6 erytrocytów (czerwona strefa)", 6, w => w.Stat("m_rbc")),
            new Goal("Zabij 60 drobnoustrojów", 60, w => w.Stat("m_killed")),
        };
        LoseText = "Makrofagi zginęły – tkanka została bez strażnika";
        Facts = new[]
        {
            "Makrofag tkankowy ma ≈ 20–25 µm: pofałdowany brzeg (ruffling), nerkowate jądro, setki lizosomów. W tkance pełza ≈ 0,6 µm/min (tryb mezenchymalny) do ≈ 3 µm/min (ameboidalny) – kilka razy wolniej niż neutrofil.",
            "Makrofagi tkankowe są strażnikami: po wykryciu bakterii wydzielają chemokiny CXCL1/CXCL2 (u człowieka też IL-8) i cytokiny, które ściągają neutrofile z krwi (De Filippo i in., Blood 2013).",
            "Neutrofil żyje w tkance kilka–kilkanaście godzin, a fagocytoza przyspiesza jego apoptozę. Komórka apoptotyczna obkurcza się, zaokrągla, a jądro zbija w gęste grudki (piknoza).",
            "Eferocytoza: makrofag rozpoznaje fosfatydyloserynę na powierzchni apoptotycznej komórki i ją pochłania. Potem wydziela TGF-β, PGE2 i hamuje IL-1β, IL-8, TNF – zapalenie wygasa (Fadok i in., J Clin Invest 1998).",
            "Nieuprzątnięte komórki apoptotyczne ulegają martwicy wtórnej: pęka błona, a enzymy ziarnistości i DAMP podtrzymują zapalenie i uszkadzają tkankę.",
            "Erytrofagocytoza: makrofagi zjadają erytrocyty, które wydostały się z naczyń. Oksygenaza hemowa-1 rozkłada hem na biliwerdynę, CO i żelazo; żelazo trafia do ferrytyny i hemosyderyny – złotobrązowych ziaren (syderofagi, zmiana barwy siniaka).",
            "Candida albicans potrafi w fagosomie przejść w strzępkę. Toksyna kandydalizyna (Moyes i in., Nature 2016) i piroptoza przebijają makrofaga od środka – drożdżaki trzeba strawić szybko.",
            "Leukocydyny S. aureus (PVL, γ-hemolizyna) niszczą nie tylko neutrofile, ale też monocyty i makrofagi.",
            "Time-lapse ×90: 1 s gry ≈ 1,5 min w tkance. Trawienie w fagolizosomie trwa od kilkudziesięciu minut (bakterie) do godzin (erytrocyt).",
            "Gdy makrofag zginie, z krwi przez żyłkę wychodzi monocyt i w tkance różnicuje się w nowego makrofaga (w grze – rezerwa 3).",
        };
    }
    /// <summary>Neutrofile wezwane cytokinami: wychodzą z najbliższej żyłki (diapedeza).</summary>
    public void Recruit(World w, float x, float y, int n)
    {
        int have = w.Count(e => e is Neutrophil nn && !nn.Apoptotic);
        for (int i = 0; i < n && have + i < 16; i++) Diapedesis(w, x + Rand(-45, 45), y);
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        base.GenerateChunk(w, c);
        // po wcześniejszym zapaleniu: neutrofile już w apoptozie (ognisko zakażenia) i świeże wylewy
        int apo = c.Biome == 2 ? RandI(1, 3) : Math.Abs(c.X) + Math.Abs(c.Y) <= 1 ? 1 : Chance(0.3f) ? 1 : 0;
        for (int i = 0; i < apo; i++)
        {
            var (x, y) = Spot(w, c, 7, new List<Ent>(), 8); if (InVessel(w, x, y, 10)) continue;
            var n = w.Add(new Neutrophil(w, x, y, Rand(0, TAU))); n.StartApoptosis(); n.Apo = Rand(200, 3000);
        }
    }
    public override void Init(World w)
    {
        w.CamX = 40; w.CamY = 95;
        w.UpdateChunks(true);
        var me = w.Add(new Macrophage(w, 40, 95, 11.5f)); me.Lineage = true;
        w.SetPlayer(me);
        Diapedesis(w, 0, 60); Diapedesis(w, 110, 60);
        w.Log("Jesteś makrofagiem tkankowym. Zielone na radarze = do pochłonięcia (bakterie, drożdże, umierające neutrofile, erytrocyty poza naczyniem). Spacja – wezwij neutrofile.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        base.Update(w, dt, gdt);
        if (w.EverySecond(gdt)) w.Stats["m_score"] = w.Stat("m_killed") + 5 * w.Stat("m_effero") + 3 * w.Stat("m_rbc");
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Macrophage;
        return new()
        {
            ("Sfagocytowane", N(w.Stat("m_eng"))), ("Zabite drobnoustroje", N(w.Stat("m_killed"))),
            ("Eferocytoza", N(w.Stat("m_effero"))), ("Erytrocyty → hemosyderyna", N(w.Stat("m_rbc"))),
            ("Fagosomy", p != null ? $"{p.Phagos.Count} ({Pct(p.Load / Macrophage.MaxLoad)})" : "-"),
            ("Fenotyp", p == null ? "-" : p.M2 > 0.3f ? "naprawczy (TGF-β, IL-10)" : "prozapalny (TNF, IL-1β)"),
            ("Cytokiny", p == null ? "-" : p.CytoCD > 0 ? $"za {p.CytoCD:0} s" : "gotowe (Spacja)"),
            ("Martwica wtórna", N(w.Stat("necro"))), ("Neutrofile", w.Count(e => e is Neutrophil n && !n.Apoptotic).ToString()),
            ("Rezerwa monocytów", N(3 - w.Stat("mono"))),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Macrophage p) return null;
        return new()
        {
            ("Integralność błony", p.Hp, p.Hp < 0.35f ? Hex("#e05a4a") : Hex("#cbbf9c")),
            ("Wypełnienie fagosomami", p.Load / Macrophage.MaxLoad, p.Load > 80 ? Hex("#e0b050") : Hex("#9cc0a0")),
        };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e.Holder != null) return 0;
        if (e is Staph s && s.State == St.Free) return 1;
        if (e is CandidaYeast cy && cy.State == St.Free) return 1;
        if (e is Neutrophil n) return n.Apoptotic ? 1 : 3;
        if (e is RBC) return 1;
        if (e is CandidaHypha) return 2;
        return e is Macrophage && e != w.Player ? 3 : 0;
    }
    public override bool OnPlayerLost(World w, Ent p)
    {
        if (w.Stat("mono") >= 3) return false;
        w.AddStat("mono", 1);
        float x = p.X, yv = VesselY(w, x, p.Y), side = p.Y >= yv ? 1 : -1;
        var m = w.Add(new Macrophage(w, x, yv + side * (16 + 14), 11)); m.Lineage = true;
        w.Player = null; w.SetPlayer(m);
        w.Log($"Monocyt z krwi przechodzi przez ścianę żyłki i różnicuje się w makrofaga (rezerwa {3 - w.Stat("mono"):0}).", "info");
        return true;
    }
    public override float Score(World w) => w.Stat("m_score");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Macrophage(w, 0, 0, 11)); return (e, 34); }
}

// =====================================================================
//  Staw – ameba, Didinium, pantofelek (świat nieskończony)
// =====================================================================
public abstract class ScPond : Scenario
{
    protected bool Ciliates;
    protected ScPond()
    {
        Style = ScopeStyle.Pond; Modes = LightModes; Wd = 9000; Ht = 6400; GridCell = 250; FlowScale = 2600; Water = true;
        ChunkSize = 1400; ActiveR = 2; ParkCap = 60;
        BiomeNames = new[] { "Otwarta woda", "Zarośla skrętnicy", "Osad denny", "Chmury bakterii" };
        BiomeColors = new[] { Rgb(90, 140, 160), Rgb(80, 170, 80), Rgb(150, 125, 85), Rgb(190, 170, 100) };
        DebrisN = 2400; DebrisS0 = 3; DebrisS1 = 16; DebrisD = 0.5f; DebrisCols = new[] { Rgb(70, 66, 45, 0.45f), Rgb(255, 255, 235, 0.4f), Rgb(40, 50, 35, 0.5f) };
    }
    protected static void Habitat(World w, Chunk c, List<Ent> loc)
    {
        switch (c.Biome)
        {
            case 0:
                if (Chance(0.3f)) { var (x, y) = Spot(w, c, 100, loc, 120); Put(w, loc, new Grain(w, x, y, Rand(35, 90))); }
                break;
            case 1:
                for (int i = RandI(2, 3); i > 0; i--) SpiroCell.Filament(w, c.X0 + Rand(150, c.S - 150), c.Y0 + Rand(150, c.S - 150), Rand(0, TAU), RandI(8, 16));
                if (Chance(0.4f)) { var (x, y) = Spot(w, c, 100, loc, 120); Put(w, loc, new Grain(w, x, y, Rand(35, 80))); }
                if (Chance(0.5f)) { var (x, y) = Spot(w, c, 150, loc, 100); Put(w, loc, new Rotifer(w, x, y, Rand(0, TAU))); } // bdelloidy żyją na roślinach
                break;
            case 2:
                for (int i = RandI(3, 4); i > 0; i--) { var (x, y) = Spot(w, c, 110, loc, 120); Put(w, loc, new Grain(w, x, y, Rand(35, 110))); }
                if (Chance(0.8f))
                {
                    var (x, y) = Spot(w, c, 60, loc, 300); float a0 = Rand(0, TAU), gr = Rand(25, 45);
                    Put(w, loc, new Grain(w, x, y, gr));
                    int n = RandI(3, 7);
                    for (int k = 0; k < n; k++) w.Add(new Vorticella(w, x, y, a0 + (k - n / 2f) * 0.35f + Rand(-0.1f, 0.1f), gr * 0.9f));
                }
                break;
            case 3:
                if (Chance(0.5f)) { var (x, y) = Spot(w, c, 150, loc, 100); Put(w, loc, new Rotifer(w, x, y, Rand(0, TAU))); }
                break;
        }
    }
    /// <summary>
    /// Pełna fauna i flora stawu (jak w prawdziwej kropli wody): wiciowce, orzęski, słonecznice, nicienie, sinice,
    /// toczki, a na otwartej wodzie czasem rozwielitka. Ilości zależą od strefy.
    /// </summary>
    protected static void PondLife(World w, Chunk c, List<Ent> loc)
    {
        int b = c.Biome; bool start = Math.Abs(c.X) + Math.Abs(c.Y) <= 1;
        void P(Spec s, int n, float r, float scale = 1) { for (int i = 0; i < n; i++) { var (x, y) = Spot(w, c, r, loc, r); Put(w, loc, new Protist(w, s, x, y, Rand(0, TAU), scale)); } }
        P(Species.Euglena, b <= 1 ? RandI(4, 7) : RandI(1, 3), 50);
        for (int i = b == 2 ? RandI(6, 10) : RandI(2, 5); i > 0; i--) { var (x, y) = Spot(w, c, 60, loc, 60); Put(w, loc, new Detritus(w, x, y, Rand(15, b == 2 ? 70 : 45))); }
        P(Species.Blepharisma, b == 3 ? RandI(1, 3) : Chance(0.5f) ? 1 : 0, 150);
        if (!start && Chance(0.12f)) P(Species.Blepharisma, 1, 260, 1.7f); // kanibal-olbrzym
        for (int i = b == 1 ? RandI(1, 3) : Chance(0.4f) ? 1 : 0; i > 0; i--) { var (x, y) = Spot(w, c, 80, loc, 80); Put(w, loc, new Paramecium(w, x, y, Rand(0, TAU), 0, 0, true)); }
        P(Species.Spirostomum, b == 2 ? RandI(1, 2) : Chance(0.3f) ? 1 : 0, 500);
        P(Species.Loxodes, b == 2 ? RandI(1, 3) : b == 3 && Chance(0.3f) ? 1 : 0, 300); // tuż nad gnijącym mułem, mało tlenu
        if (b == 2 && Chance(0.6f)) { var (x, y) = Spot(w, c, 150, loc, 100); Put(w, loc, new Rotifer(w, x, y, Rand(0, TAU))); }
        P(Species.Euplotes, b == 2 ? RandI(2, 3) : Chance(0.3f) ? 1 : 0, 70);
        P(Species.Tetrahymena, b == 3 ? RandI(1, 3) : Chance(0.4f) ? 1 : 0, 40);
        P(Species.Stentor, b is 1 or 2 ? RandI(1, 2) : Chance(0.25f) ? 1 : 0, 500);
        if (!start && Chance(b is 2 or 3 ? 0.5f : 0.25f))
        {
            var (x, y) = Spot(w, c, 60, loc, 100);
            for (int k = RandI(3, 5); k > 0; k--) Put(w, loc, new Protist(w, Species.Coleps, x + RandN() * 50, y + RandN() * 50, Rand(0, TAU)));
        }
        if (!start && Chance(b is 2 or 3 ? 0.35f : 0.15f)) { var (x, y) = Spot(w, c, 150, loc, 150); Put(w, loc, new Lacrymaria(w, x, y, Rand(0, TAU))); }
        if (Chance(b <= 1 ? 0.3f : 0.1f)) { var (x, y) = Spot(w, c, 260, loc, 260); Put(w, loc, new Helio(w, x, y)); }
        if (b == 0 && Chance(0.45f)) for (int i = RandI(1, 2); i > 0; i--) { var (x, y) = Spot(w, c, 250, loc, 250); Put(w, loc, new Volvox(w, x, y)); }
        if (!start && b == 0 && Chance(0.1f)) { var (x, y) = Spot(w, c, 700, loc, 400); Put(w, loc, new Daphnia(w, x, y, Rand(0, TAU))); }
        for (int i = b == 2 ? RandI(1, 2) : Chance(0.2f) ? 1 : 0; i > 0; i--) { var (x, y) = Spot(w, c, 200, loc, 200); Put(w, loc, new Chain(w, Chain.Type.Nematode, x, y, Rand(0, TAU), Rand(400, 1000))); }
        for (int i = b is 1 or 2 ? RandI(0, 2) : Chance(0.15f) ? 1 : 0; i > 0; i--) { var (x, y) = Spot(w, c, 200, loc, 200); Put(w, loc, new Chain(w, Chain.Type.Oscillatoria, x, y, Rand(0, TAU), Rand(300, 800))); }
    }
    /// <summary>Kategoria zagrożenia w stawie (dla radaru i strzałek).</summary>
    protected static bool PondThreat(Ent e, Ent me)
    {
        if (e is Daphnia || e is Helio || e is Lacrymaria) return true;
        if (e is Protist p) return p.S == Species.Coleps || (p.S == Species.Stentor && me != null && me.Length < p.Length * 0.28f) || (p.S == Species.Blepharisma && me != null && p.Length * 0.4f > me.Length && !(me is Amoeba));
        return false;
    }
    protected static readonly string[] LifeFacts =
    {
        "W kropli stawowej roi się od życia: zielone klejnotki (Euglena) z czerwoną plamką oczną, różowe Blepharisma, zielone pantofelki P. bursaria z ~700 zielenicami Chlorella w środku, niebieskie trąbkowate Stentory, pancerne Coleps, „chodzące” Euplotes.",
        "Blepharisma: różowa blefaryzmina chroni przed UV i truje drapieżniki. Kanibale rosną w olbrzymy (gigantyzm) i zjadają mniejsze orzęski.",
        "Spirostomum ambiguum (~1 mm) kurczy się do 1/3 długości w ~5 ms, a jego toksyny odstraszają drapieżniki. Stentor coeruleus (do 2 mm) siedzi na stopce i wirem wciąga nawet orzęski i wrotki.",
        "Coleps hirtus (40–65 µm, pancerz z płytek wapiennych) poluje stadami na ranne orzęski. Lacrymaria olor wysuwa „łabędzią szyję” na 7× długości ciała i trafia ofiary toksycystami.",
        "Słonecznica (Actinosphaerium, 200–1000 µm) przykleja ofiary do promienistych aksopodiów. Nicienie (~0,5–1 mm) wiją się z częstotliwością ~2 Hz; nitki sinic Oscillatoria ślizgają się po kilka µm/s.",
        "Toczek (Volvox, do 0,5 mm) to kula tysięcy komórek z wiciami, w środku kolonie potomne. Rozwielitka (Daphnia, 1–3 mm) odnóżami filtruje wodę i zjada glony, bakterie i orzęski wielkości pantofelka.",
        "Loxodes magnus (≈ 0,5 mm) żyje tuż nad gnijącym mułem, gdzie prawie nie ma tlenu. Kierunek „góra–dół” wyczuwa 10–24 pęcherzykami Müllera (grawitaksja): gdy tlenu jest za dużo, płynie w dół. Je bakterie i glony.",
        "Wszystkie miękkie komórki odkształcają się przy kontakcie – pantofelek potrafi przecisnąć się przez szczelinę węższą niż jego ciało i potem wraca do kształtu.",
    };
    protected static void Many<T>(World w, Chunk c, List<Ent> loc, int n, float r, Func<float, float, T> mk) where T : Ent
    {
        for (int i = 0; i < n; i++) { var (x, y) = Spot(w, c, r, loc, r); Put(w, loc, mk(x, y)); }
    }
    public override void OnChunkActivated(World w, Chunk c)
    {
        if (w.Bact == null || c.Bact < 1) return;
        int total = (int)c.Bact; c.Bact = 0;
        int clouds = Math.Clamp(total / 250, 1, 6);
        for (int i = 0; i < clouds; i++) w.Bact.Cloud(c.X0 + Rand(150, c.S - 150), c.Y0 + Rand(150, c.S - 150), Rand(90, 200), total / clouds);
    }
    protected void InitialBact(Chunk c)
    {
        if (!Ciliates) return;
        c.Bact += c.Biome switch { 3 => 1500, 2 => 700, 1 => 500, _ => 350 };
    }
    protected static void RegrowBact(World w)
    {
        if (w.Bact == null || w.Bact.N > w.Bact.Max * 0.7f || w.Active.Count == 0) return;
        var c = w.Active[RandI(0, w.Active.Count - 1)];
        int amt = c.Biome == 3 ? 260 : c.Biome == 0 ? 60 : 120;
        w.Bact.Cloud(c.X0 + Rand(150, c.S - 150), c.Y0 + Rand(150, c.S - 150), Rand(80, 180), amt);
    }
    public override void DrawUnder(World w, Painter p, View v) { w.Bact?.Draw(p, v); }
    public override void DrawMinimap(World w, Painter p) { w.Bact?.DrawMap(p, Rgb(150, 130, 70, 0.7f)); }
    protected static void PlaceStart(World w, Func<World, float, float, Ent> mk, Func<Ent, bool> avoid = null)
    {
        var c0 = w.GetChunk(0, 0);
        var (x, y) = Spot(w, c0, 160, new List<Ent>(), 200);
        if (avoid != null) for (int k = 0; k < 80 && w.Near(x, y, 1100, avoid) != null; k++) (x, y) = Spot(w, c0, 160, new List<Ent>(), 200);
        w.SetPlayer(w.Add(mk(w, x, y)));
    }
    protected static readonly string[] HabitatFacts =
    {
        "Wrotki (np. Philodina, ≈ 300–450 µm) to zwierzęta wielokomórkowe: dwa wieńce rzęsek na koronie tworzą wir, który wciąga bakterie i glony; mastaks miele pokarm. Dotknięte chowają koronę.",
        "Wirczyki (Vorticella) siedzą na kurczliwym trzonku: spazmonema zwija go w ok. 9 ms (jedna z najszybszych maszyn komórkowych), potem trzonek powoli się prostuje.",
        "Skrętnica (Spirogyra) tworzy nitki komórek o szerokości ~10–100 µm ze spiralnymi, taśmowatymi chloroplastami z pirenoidami.",
        "Woda w preparacie nie stoi: parowanie i różnice temperatur tworzą powolne prądy (tu ≈ kilka µm/s), które niosą bakterie i detrytus. Staw jest nieskończony – różne strefy mają różną faunę.",
    };
}

public sealed class ScAmoeba : ScPond
{
    public ScAmoeba()
    {
        Id = "amoeba"; Name = "Ameba"; Latin = "Amoeba proteus"; TypeName = "Pierwotniak (Amoebozoa)"; Size = "≈ 200–800 µm"; Color = Hex("#b9c4a4");
        Desc = "Olbrzymia komórka bez stałego kształtu w nieskończonym stawie. Otaczaj okrzemki, desmidie i wrotki, traw je w wodniczkach i dziel się – zanim zagłodzisz się na śmierć.";
        ViewW = 1150; TimeScale = 25; Bio = 30; Flow = 1.2f; ChunkSize = 1600; Mag = "Obiektyw 10× (100×) – time-lapse"; ScoreLabel = "biomasy";
        Controls = "WASD / przytrzymaj LPM – wysuwaj nibynóżkę w danym kierunku · otocz pokarm, by go wchłonąć";
        Goals = new[]
        {
            new Goal("Zjedz 3 porcje pokarmu", 3, w => w.Stat("eaten")),
            new Goal("Pochłoń wrotka (dotknięty chowa koronę – wtedy się zmieści)", 1, w => w.Stat("rotifers")),
            new Goal("Podziel się (populacja 2)", 2, w => w.CountAll(e => e is Amoeba && e.Lineage)),
            new Goal("Twoja populacja: 5 ameb", 5, w => w.CountAll(e => e is Amoeba && e.Lineage)),
        };
        Facts = new[]
        {
            "Amoeba proteus osiąga ≈ 200–800 µm – widać ją gołym okiem jako białą kropkę.",
            "Ruch ameboidalny: prąd cytoplazmy do ≈ 10 µm/s, a sam środek komórki przesuwa się ok. 4× wolniej (≈ 1–2,5 µm/s).",
            "Ektoplazma (szklista) i endoplazma (ziarnista); z przodu nibynóżki – przejrzysta „czapeczka hialinowa”, z tyłu pomarszczony uroid.",
            "Odżywia się fagocytozą: nibynóżki otaczają ofiarę i zamykają ją w wodniczce trawiennej. Poluje też na wrotki i orzęski.",
            "A. proteus nie tworzy cyst – głodująca kurczy się i w końcu ginie (w grze pasek „Głód”).",
            "Wodniczka tętniąca cyklicznie wyrzuca nadmiar wody. Jądro dyskowate ≈ 40–50 µm; w cytoplazmie kryształy.",
            HabitatFacts[0], HabitatFacts[2], HabitatFacts[3], LifeFacts[0], LifeFacts[1], LifeFacts[2], LifeFacts[4], LifeFacts[5], LifeFacts[6], LifeFacts[7],
            "Time-lapse ×25: szybkie pływaki są w grze spowolnione do ~⅓ ekranu na sekundę, żeby dało się je zobaczyć (w prawdziwym nagraniu poklatkowym byłyby rozmytą smugą).",
        };
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        Habitat(w, c, loc);
        int b = c.Biome;
        Many(w, c, loc, b == 2 ? 8 : b == 1 ? 5 : 3, 60, (x, y) => new Diatom(w, x, y, Rand(0, TAU)));
        Many(w, c, loc, b == 1 ? 3 : 1, 40, (x, y) => new Cosmarium(w, x, y, Rand(0, TAU)));
        if (b == 1 || Chance(0.3f)) Many(w, c, loc, 1, 150, (x, y) => new Closterium(w, x, y, Rand(0, TAU)));
        Many(w, c, loc, b == 2 ? 9 : 3, 20, (x, y) => new Floc(w, x, y, Rand(0, TAU)));
        if (b == 3) Many(w, c, loc, 1, 150, (x, y) => new Rotifer(w, x, y, Rand(0, TAU)));
        if (Math.Abs(c.X) + Math.Abs(c.Y) > 1 && Chance(0.12f)) Many(w, c, loc, 1, 300, (x, y) => new Amoeba(w, x, y, Rand(120, 150)));
        PondLife(w, c, loc);
    }
    public override void Init(World w)
    {
        w.UpdateChunks(true);
        PlaceStart(w, (ww, x, y) => new Amoeba(ww, x, y, 150));
        w.Stats["pop"] = 1;
        w.Log("Jesteś Amoeba proteus w nieskończonym stawie. Zielone na radarze = pokarm; najwięcej okrzemek jest w osadzie dennym.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        if (!w.EverySecond(gdt)) return;
        if (w.Count(e => e is Food f && f.State == St.Free) < 180)
        {
            var (x, y) = SpawnAway(w, 60); float r = R01();
            if (r < 0.45f) w.Add(new Diatom(w, x, y, Rand(0, TAU)));
            else if (r < 0.7f) w.Add(new Floc(w, x, y, 0));
            else if (r < 0.92f) w.Add(new Cosmarium(w, x, y, Rand(0, TAU)));
            else w.Add(new Closterium(w, x, y, Rand(0, TAU)));
        }
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Amoeba;
        return new()
        {
            ("Zjedzone", N(w.Stat("eaten"))), ("Biomasa (pokarm)", $"{w.Stat("biomass"):0.0}"), ("Do podziału", p != null ? Pct(Math.Min(1, p.EnergyFrac)) : "-"),
            ("Wodniczki pokarmowe", p != null ? p.Meals.Count.ToString() : "-"), ("Średnica zastępcza", p != null ? $"{2 * MathF.Sqrt(p.A0 / PI):0} µm" : "-"),
            ("Twoja populacja", w.CountAll(e => e is Amoeba && e.Lineage).ToString()),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Amoeba a) return null;
        return new() { ("Głód", Math.Min(1, a.Hunger), a.Hunger > 0.6f ? Hex("#e05a4a") : Hex("#e0b050")) };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e.Edible && e.State is St.Free or St.Swim or St.Avoid && e.Holder == null && e.Length < 330 && !(e is Amoeba)) return 1;
        return e is Amoeba && e.Lineage && e != w.Player ? 3 : 0;
    }
    public override float Score(World w) => w.Stat("biomass");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Amoeba(w, 0, 0, 150)); return (e, 620); }
}

public sealed class ScDidinium : ScPond
{
    public ScDidinium()
    {
        Id = "didinium"; Name = "Didinium"; Latin = "Didinium nasutum"; TypeName = "Orzęsek drapieżny"; Size = "≈ 60–200 µm"; Color = Hex("#d4c79a");
        Desc = "Beczułkowaty drapieżnik z ryjkiem w nieskończonym stawie. Szukaj stad pantofelków (najwięcej w chmurach bakterii), trafiaj je przodem i połykaj – głód oznacza encystację.";
        ViewW = 1050; TimeScale = 0.5f; Bio = 250; Starve = 75; Flow = 8; Mag = "Obiektyw 10× (100×)"; ScoreLabel = "pantofelków"; Ciliates = true;
        Controls = "WASD / przytrzymaj LPM – płyń · Shift – szybciej · uderz ryjkiem (przodem) w pantofelka · głód = encystacja!";
        Goals = new[]
        {
            new Goal("Upoluj pierwszego pantofelka", 1, w => w.Stat("eaten")),
            new Goal("Podziel się (populacja 2)", 2, w => w.CountAll(e => e is Didinium && e.Lineage)),
            new Goal("Zjedz 6 pantofelków", 6, w => w.Stat("eaten")),
            new Goal("Twoja populacja: 8", 8, w => w.CountAll(e => e is Didinium && e.Lineage)),
        };
        Facts = new[]
        {
            "Didinium nasutum: ≈ 60–200 µm, kształt beczułki z ryjkiem (proboscis) i dwoma wieńcami rzęsek (pektynele).",
            "Wyspecjalizowany drapieżnik pantofelków – ofiara bywa 1,5–2× większa od niego.",
            "Po kontakcie ryjka z ofiarą wystrzeliwuje toksycysty (paraliżujące) i peksycysty (przytrzymujące). Cytostom rozciąga się ogromnie.",
            "Pantofelek broni się, wystrzeliwując trichocysty – przeciw Didinium tylko częściowo skutecznie.",
            "Głodujący Didinium tworzy cystę spoczynkową – w grze oznacza to koniec rozgrywki.",
            HabitatFacts[0], HabitatFacts[1], HabitatFacts[3], LifeFacts[0], LifeFacts[3], LifeFacts[4], LifeFacts[5], LifeFacts[6],
        };
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        Habitat(w, c, loc); InitialBact(c);
        int b = c.Biome;
        Many(w, c, loc, b == 3 ? 7 : b == 0 ? 3 : 2, 130, (x, y) => new Paramecium(w, x, y, Rand(0, TAU)));
        Many(w, c, loc, b == 0 ? 5 : 3, 10, (x, y) => new Chlamy(w, x, y, Rand(0, TAU)));
        Many(w, c, loc, RandI(1, 2), 60, (x, y) => new Diatom(w, x, y, Rand(0, TAU)));
        if (Math.Abs(c.X) + Math.Abs(c.Y) > 1 && Chance(0.08f)) Many(w, c, loc, 1, 80, (x, y) => new Didinium(w, x, y, Rand(0, TAU)));
        PondLife(w, c, loc);
    }
    public override void Init(World w)
    {
        w.Bact = new BactField(w, 24000);
        w.UpdateChunks(true);
        PlaceStart(w, (ww, x, y) => new Didinium(ww, x, y, Rand(0, TAU)));
        w.Stats["pop"] = 1;
        w.Log("Jesteś Didinium. Pantofelek musi dotknąć Twojego ryjka (przodu). Zielone na radarze = pantofelki.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Bact.Update(dt, gdt);
        if (!w.EverySecond(gdt)) return;
        RegrowBact(w);
        if (w.Count(e => e is Paramecium q && q.State != St.Eaten) < 40) { var (x, y) = SpawnAway(w, 130); w.Add(new Paramecium(w, x, y, Rand(0, TAU))); }
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Didinium;
        string st = p == null ? "-" : p.State switch { St.Swim => "polowanie", St.Capture => "toksycysty!", St.Swallow => "połykanie", St.Digest => "trawienie", St.Cyst => "encystacja", _ => "-" };
        return new()
        {
            ("Stan", st), ("Zjedzone pantofelki", N(w.Stat("eaten"))),
            ("Twoja populacja", w.CountAll(e => e is Didinium && e.Lineage).ToString()), ("Pantofelki w okolicy", w.Count(e => e is Paramecium q && q.State != St.Eaten).ToString()),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Didinium p) return null;
        return new() { ("Głód", p.Hunger, p.Hunger > 0.7f ? Hex("#e05a4a") : Hex("#e0b050")) };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Paramecium q && (q.State == St.Swim || q.State == St.Avoid)) return 1;
        if (PondThreat(e, w.Player)) return 2;
        return e is Didinium && e.Lineage && e != w.Player ? 3 : 0;
    }
    public override float Score(World w) => w.Stat("eaten");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Didinium(w, 0, 0, 0)); return (e, 220); }
}

public sealed class ScPara : ScPond
{
    public ScPara()
    {
        Id = "para"; Name = "Pantofelek"; Latin = "Paramecium caudatum"; TypeName = "Orzęsek"; Size = "≈ 200 µm"; Color = Hex("#b0c8c0");
        Desc = "Wsysaj bakterie rowkiem gębowym i rozmnażaj się w nieskończonym stawie. Na otwartej wodzie polują Didinium (Spacja – trichocysty); w zaroślach skrętnicy jest bezpieczniej.";
        ViewW = 900; TimeScale = 0.5f; Bio = 150; Starve = 160; Flow = 8; Mag = "Obiektyw 10× (100×)"; ScoreLabel = "bakterii"; Ciliates = true;
        Controls = "WASD / przytrzymaj LPM – płyń · Shift – ucieczka · Spacja – trichocysty · przepływaj przez chmury bakterii";
        Goals = new[]
        {
            new Goal("Zjedz 150 bakterii", 150, w => w.Stat("bact")),
            new Goal("Podziel się (populacja 2)", 2, w => w.CountAll(e => e is Paramecium q && q.Lineage && q.State != St.Eaten)),
            new Goal("Wyrwij się Didinium dzięki trichocystom", 1, w => w.Stat("escapes")),
            new Goal("Twoja populacja: 6", 6, w => w.CountAll(e => e is Paramecium q && q.Lineage && q.State != St.Eaten)),
        };
        Facts = new[]
        {
            "Paramecium caudatum: ≈ 200 (do 300) µm, tylny koniec zaostrzony (caudatum = „ogoniasty”).",
            "Tysiące rzęsek biją skoordynowanie (fala metachroniczna). Pływa po spirali ≈ 1 mm/s.",
            "Reakcja unikania: po uderzeniu w przeszkodę depolaryzacja błony odwraca bicie rzęsek – pantofelek cofa się i skręca.",
            "Rowek gębowy prowadzi do cytostomu; bakterie trafiają do wodniczek pokarmowych, które krążą po komórce (cykloza).",
            "Dwie gwiaździste wodniczki tętniące z kanalikami usuwają nadmiar wody. Makronukleus i mikronukleus; podział poprzeczny.",
            "Trichocysty: „igły” pod pellikulą, które w kilka milisekund wydłużają się kilkukrotnie – mechanizm obronny (działa też na Lacrymarię, słonecznicę i Stentora).",
            HabitatFacts[0], HabitatFacts[1], HabitatFacts[3], LifeFacts[0], LifeFacts[1], LifeFacts[2], LifeFacts[3], LifeFacts[4], LifeFacts[5], LifeFacts[6], LifeFacts[7],
        };
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        Habitat(w, c, loc); InitialBact(c);
        int b = c.Biome;
        Many(w, c, loc, RandI(1, 2), 130, (x, y) => new Paramecium(w, x, y, Rand(0, TAU)));
        Many(w, c, loc, 3, 10, (x, y) => new Chlamy(w, x, y, Rand(0, TAU)));
        Many(w, c, loc, RandI(1, 2), 60, (x, y) => new Diatom(w, x, y, Rand(0, TAU)));
        float pd = b switch { 0 => 0.45f, 3 => 0.3f, 2 => 0.2f, _ => 0.08f };
        if (Math.Abs(c.X) + Math.Abs(c.Y) > 1 && Chance(pd)) Many(w, c, loc, 1, 80, (x, y) => new Didinium(w, x, y, Rand(0, TAU)));
        PondLife(w, c, loc);
    }
    public override void Init(World w)
    {
        w.Bact = new BactField(w, 24000);
        w.UpdateChunks(true);
        PlaceStart(w, (ww, x, y) => new Paramecium(ww, x, y, Rand(0, TAU)), e => e is Didinium);
        w.Stats["pop"] = 1;
        w.Log("Jesteś pantofelkiem. Brązowe plamy na radarze to chmury bakterii, czerwone – drapieżniki. Zielone strefy (skrętnica) są bezpieczniejsze.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Bact.Update(dt, gdt);
        if (!w.EverySecond(gdt)) return;
        RegrowBact(w);
        if (w.Count(e => e is Didinium) < 4 + (int)(w.T / 90)) { var (x, y) = SpawnAway(w, 80); if (w.ChunkAt(x, y).Biome != 1) w.Add(new Didinium(w, x, y, Rand(0, TAU))); }
        if (w.Count(e => e is Paramecium q && !q.Lineage && q.State != St.Eaten) < 14) { var (x, y) = SpawnAway(w, 130); w.Add(new Paramecium(w, x, y, Rand(0, TAU))); }
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Paramecium;
        return new()
        {
            ("Zjedzone bakterie", N(w.Stat("bact"))), ("Wodniczki pokarmowe", p != null ? p.Fv.Count.ToString() : "-"),
            ("Do podziału", p != null ? Pct(Math.Min(1, p.Energy / 6)) : "-"), ("Trichocysty", p != null ? (p.TrichCD > 0 ? $"ładowanie {p.TrichCD:0.0} s" : "gotowe") : "-"),
            ("Twoja populacja", w.CountAll(e => e is Paramecium q && q.Lineage && q.State != St.Eaten).ToString()), ("Drapieżniki w okolicy", w.Count(e => e is Didinium).ToString()),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Paramecium p) return null;
        return new() { ("Głód", p.Hunger, p.Hunger > 0.7f ? Hex("#e05a4a") : Hex("#e0b050")) };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Didinium || e is Rotifer || PondThreat(e, w.Player)) return 2;
        return e is Paramecium q && q.Lineage && e != w.Player && q.State != St.Eaten ? 3 : 0;
    }
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var p = w.Player;
        return p != null && w.Bact.Densest(p.X, p.Y, ViewW * 3, out x, out y);
    }
    public override float Score(World w) => w.Stat("bact");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Paramecium(w, 0, 0, 0)); return (e, 290); }
}

/// <summary>
/// Klejnotka Euglena gracilis – miksotroficzny wiciowiec. Energia pochodzi ze światła, którego natężenie zmienia się
/// w przestrzeni (cienie rzęsy wodnej, zarośla, ciemny osad, mętna woda) i w czasie (chmury).
/// </summary>
public sealed class ScEuglena : ScPond
{
    public ScEuglena()
    {
        Id = "euglena"; Name = "Klejnotka"; Latin = "Euglena gracilis"; TypeName = "Wiciowiec fotosyntetyzujący (Euglenozoa)"; Size = "≈ 50–85 µm"; Color = Hex("#8fc46a");
        Desc = "Żyjesz ze światła. Szukaj półcienia – pełne słońce niszczy chloroplasty, a w ciemności kończy się zapas. Gromadź paramylon, dziel się wzdłuż ciała i chowaj w detrytusie przed orzęskami.";
        ViewW = 330; TimeScale = 0.5f; Bio = 150; Flow = 4; Mag = "Obiektyw 40× (400×)"; ScoreLabel = "podziałów";
        Controls = "WASD / przytrzymaj LPM – płyń (wić) · Shift – szybciej · Spacja (przytrzymaj) – metabolia: pełzanie i chowanie się w kłaczkach detrytusu";
        Goals = new[]
        {
            new Goal("Znajdź półcień i zgromadź 80% paramylonu", 0.8f, w => w.Stat("paramax")),
            new Goal("Schowaj się w detrytusie (metabolia – przytrzymaj Spację)", 1, w => w.Stat("hidden")),
            new Goal("Podziel się podłużnie (populacja 2)", 2, w => w.CountAll(e => e is Protist q && q.S.Photo && q.Lineage)),
            new Goal("Twoja populacja: 6 klejnotek", 6, w => w.CountAll(e => e is Protist q && q.S.Photo && q.Lineage)),
        };
        LoseText = "Twoje klejnotki zginęły";
        Facts = new[]
        {
            "Euglena gracilis: ≈ 35–85 µm, jedna długa wić wychodząca ze zbiorniczka (rezerwuaru) na przednim końcu, ≈ 10 chloroplastów z pirenoidami, czerwona plamka oczna. Pływa ≈ 50–100 µm/s, obracając się wokół długiej osi.",
            "Fototaksja: plamka oczna (stigma, karotenoidy) przy każdym obrocie zacienia fotoreceptor u nasady wici – ciałko paraflagellarne z fotoaktywowaną cyklazą adenylanową PAC (Iseki i in., Nature 2002; Ntefidou i in., Plant Physiol 2003). Zmiana sygnału koryguje kurs.",
            "Przy słabym świetle fototaksja jest dodatnia (do światła), przy silnym (> ~10 W/m²) – ujemna (od światła). Tak Euglena utrzymuje się w natężeniu najlepszym do fotosyntezy i unika fotouszkodzeń.",
            "Zbyt silne światło uszkadza fotoukład II – powstają reaktywne formy tlenu (H₂O₂, rodnik ponadtlenkowy) i chloroplasty bledną (fotoinhibicja). Naprawa trwa w półcieniu.",
            "Zapasem energii jest paramylon – β-1,3-glukan w postaci jasnych ziaren w cytoplazmie. W ciemności na związkach organicznych może stanowić 50–70% suchej masy komórki.",
            "Miksotrofia: w ciemności E. gracilis żyje heterotroficznie, wchłaniając rozpuszczone związki organiczne (octan, etanol, glukozę) – osmotrofia, a nie połykanie cząstek.",
            "Metabolia (ruch euglenoidalny): paski pellikuli ślizgają się po sobie, a wzdłuż ciała biegną fale skurczu. W ciasnych szczelinach Euglena przełącza się z pływania na pełzanie – ok. 1 długość ciała na 10 s (Noselli i in., Nature Physics 2019).",
            "Podział podłużny: bruzda biegnie od przedniego końca ku tyłowi, a każda córka dostaje połowę organelli. W hodowli E. gracilis dzieli się mniej więcej raz na dobę (w grze szybciej).",
            "Na wiciowce polują orzęski (Loxodes, Blepharisma, Stentor, Lacrymaria), słonecznice i rozwielitki. Pantofelek i Didinium Euglen nie jedzą.",
            "Światło w stawie: rzęsa wodna i zarośla rzucają ruchome cienie, osad jest ciemny, woda z bakteriami mętna, a chmury zmieniają oświetlenie w ciągu minut. Pełne słońce to ~1500–2000 µmol fotonów/m²/s.",
            HabitatFacts[3], LifeFacts[0], LifeFacts[2], LifeFacts[3], LifeFacts[4], LifeFacts[5], LifeFacts[6],
        };
    }
    // ------------------------------------------------ model światła
    int _cacheSeed = int.MinValue;
    readonly Dictionary<long, float> _base = new();
    float Base(World w, int cx, int cy)
    {
        if (_cacheSeed != w.Seed) { _base.Clear(); _cacheSeed = w.Seed; }
        long k = ((long)cx << 32) ^ (uint)cy;
        if (_base.TryGetValue(k, out float b)) return b;
        // otwarta woda – pełne światło; zarośla skrętnicy – półcień; osad – mrok; chmury bakterii – mętna woda
        b = BiomeAt(w, cx, cy) switch { 0 => 1f, 1 => 0.42f, 2 => 0.1f, _ => 0.5f };
        if (_base.Count > 20000) _base.Clear();
        _base[k] = b; return b;
    }
    public override float Light(World w, float x, float y)
    {
        float cs = ChunkSize, fx = x / cs - 0.5f, fy = y / cs - 0.5f;
        int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy); float ux = Smooth(fx - ix), uy = Smooth(fy - iy);
        float b = Lerp(Lerp(Base(w, ix, iy), Base(w, ix + 1, iy), ux), Lerp(Base(w, ix, iy + 1), Base(w, ix + 1, iy + 1), ux), uy);
        float t = w.T;
        // cienie liści rzęsy wodnej (Lemna) dryfujących po powierzchni
        float n = Noise2(x / 300 + t * 0.02f, y / 300 - t * 0.013f, w.Seed + 555);
        float sh = 1 - 0.78f * Smooth((n - 0.42f) / 0.22f);
        // chmury: całe pole jaśnieje i ciemnieje w ciągu dziesiątek sekund
        float cl = 0.84f + 0.16f * Noise1(t * 0.015f + w.Seed * 0.01f);
        // pełne słońce latem ≈ 2000 µmol fotonów m⁻² s⁻¹ tuż pod powierzchnią
        return 2000 * b * sh * cl;
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        Habitat(w, c, loc);
        int b = c.Biome;
        for (int i = b == 2 ? RandI(6, 10) : RandI(3, 6); i > 0; i--) { var (x, y) = Spot(w, c, 60, loc, 60); Put(w, loc, new Detritus(w, x, y, Rand(25, 70))); }
        Many(w, c, loc, b == 0 ? 6 : 3, 10, (x, y) => new Chlamy(w, x, y, Rand(0, TAU)));
        Many(w, c, loc, RandI(1, 2), 60, (x, y) => new Diatom(w, x, y, Rand(0, TAU)));
        Many(w, c, loc, RandI(0, 1), 130, (x, y) => new Paramecium(w, x, y, Rand(0, TAU)));
        PondLife(w, c, loc);
    }
    public override void Init(World w)
    {
        w.UpdateChunks(true);
        PlaceStart(w, (ww, x, y) => { var e = new Protist(ww, Species.Euglena, x, y, Rand(0, TAU)); e.Paramylon = 0.45f; e.Lineage = true; return e; },
            e => e is Protist q && (q.S == Species.Loxodes || q.S == Species.Blepharisma || q.S == Species.Stentor) || e is Helio || e is Lacrymaria);
        w.Stats["pop"] = 1;
        w.Log("Jesteś klejnotką (Euglena gracilis). Plamka oczna czuje kierunek światła – strzałka prowadzi do najlepszego półcienia. Ciemne plamy to cienie rzęsy wodnej.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        if (!w.EverySecond(gdt)) return;
        if (w.Count(e => e is Protist q && q.S.Photo && !q.Lineage) < 25) { var (x, y) = SpawnAway(w, 50); w.Add(new Protist(w, Species.Euglena, x, y, Rand(0, TAU))); }
    }
    /// <summary>Plamy światła i cienia nad całym kadrem (tak widać je w preparacie przy małej przysłonie).</summary>
    public override void DrawOver(World w, Painter p, View v)
    {
        const int nx = 26, ny = 18;
        float x0 = v.X0 - 20, y0 = v.Y0 - 20, cw = (v.X1 - v.X0 + 40) / (nx - 1), ch = (v.Y1 - v.Y0 + 40) / (ny - 1);
        p.Grid(x0, y0, cw, ch, nx, ny, (i, j) => LightCol(Light(w, x0 + i * cw, y0 + j * ch)));
    }
    static Color LightCol(float lux)
    {
        if (lux < 700) { float d = 1 - lux / 700; return new Color(0.03f, 0.06f, 0.03f, 0.5f * d * d); }
        float h = Clamp((lux - 700) / 900, 0, 1);
        return new Color(1f, 0.98f, 0.85f, 0.14f * h);
    }
    public override void DrawMinimap(World w, Painter p)
    {
        const int n = 34;
        float cw = w.WinW / (n - 1), ch = w.WinH / (n - 1);
        p.Grid(w.WinX0, w.WinY0, cw, ch, n, n, (i, j) =>
        {
            float l = Light(w, w.WinX0 + i * cw, w.WinY0 + j * ch);
            return l > 800 ? new Color(1f, 0.95f, 0.6f, 0.35f) : l > 150 ? new Color(0.45f, 0.8f, 0.35f, 0.35f) : new Color(0.05f, 0.08f, 0.05f, 0.45f);
        });
    }
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var p = w.Player; if (p == null) return false;
        // plamka oczna "wie", gdzie jest najlepsze światło: próbkujemy okolicę i szukamy ~300–450 µmol m⁻² s⁻¹
        float best = 1e9f;
        for (int r = 1; r <= 4; r++)
            for (int k = 0; k < 12; k++)
            {
                float a = k * TAU / 12 + r * 0.26f, d = r * ViewW * 0.45f, px = p.X + MathF.Cos(a) * d, py = p.Y + MathF.Sin(a) * d;
                float s = MathF.Abs(MathF.Log((Light(w, px, py) + 15) / 380)) + d / (ViewW * 6);
                if (s < best) { best = s; x = px; y = py; }
            }
        return true;
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Protist;
        string st = p == null ? "-" : p.Div > 0 ? "podział podłużny" : p.Hidden ? "schowana w detrytusie" : p.Meta > 0.5f ? "metabolia (pełzanie)" : "pływanie";
        return new()
        {
            ("Stan", st),
            ("Światło (µmol/m²/s)", p != null && p.Lux >= 0 ? $"{p.Lux:0}" : "-"),
            ("Fotosynteza", p != null ? Pct(p.PhotoRate) : "-"),
            ("Paramylon", p != null ? Pct(Math.Min(1, p.Paramylon)) : "-"),
            ("Fotouszkodzenie PSII", p != null ? Pct(p.PhotoDmg) : "-"),
            ("Twoja populacja", w.CountAll(e => e is Protist q && q.S.Photo && q.Lineage).ToString()),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Protist p) return null;
        var l = new List<(string, float, Color)> { ("Paramylon (zapas)", Math.Min(1, p.Paramylon), p.Paramylon < 0.2f ? Hex("#e0b050") : Hex("#9fd07a")) };
        if (p.PhotoDmg > 0.02f) l.Add(("Fotouszkodzenie", p.PhotoDmg, Hex("#e05a4a")));
        if (p.Hunger > 0.01f) l.Add(("Głód", p.Hunger, Hex("#e05a4a")));
        if (p.Div > 0) l.Add(("Podział", p.Div, Hex("#9cd2ff")));
        return l;
    }
    public override int MapKind(World w, Ent e)
    {
        var me = w.Player;
        if (e is Protist q && me != null && q != me && (q.S == Species.Loxodes || q.S == Species.Blepharisma && q.Length * 0.4f > me.Length)) return 2;
        if (PondThreat(e, me)) return 2;
        return e is Protist r && r.S.Photo && r.Lineage && e != me ? 3 : 0;
    }
    public override float Score(World w) => w.Stat("pop") - 1;
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Protist(w, Species.Euglena, 0, 0, 0)); e.Paramylon = 0.7f; return (e, 110); }
}

namespace Mikro;

// =====================================================================
//  T4 – skala nano, mikroskop elektronowy
// =====================================================================
public sealed class ScT4 : Scenario
{
    public ScT4()
    {
        Id = "t4"; Name = "Bakteriofag T4"; Latin = "Tequatrovirus (Escherichia virus T4)"; TypeName = "Wirus"; Size = "≈ 0,2 µm"; Color = Hex("#e0a060");
        Desc = "Wirus bakteryjny. Nie ma napędu – dryfujesz ruchami Browna. Przyczep się do E. coli, wstrzyknij DNA i doprowadź do eksplozywnej lizy. Uwaga na odporne mutanty.";
        Wd = 40; Ht = 28; ViewW = 4.3f; TimeScale = 0.05f; Bio = 2100; Em = true; Style = ScopeStyle.Em; Modes = EmModes; GridCell = 1.5f;
        Mag = "TEM (symulacja) ≈ 25 000×"; PhageCap = 260; PhageDrift = 20; GrowEcoli = true; EcoliDouble = 14000; ScoreLabel = "wirionów";
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
            "Mutanty bez receptora (OmpC⁻ lub ze zmienionym LPS) są odporne – włókna wiążą się tylko odwracalnie i wirion odpada. W grze ok. 20% komórek jest odpornych.",
            "Genom: dsDNA ≈ 169 kpz. Po infekcji fag rozkłada DNA gospodarza i przejmuje jego rybosomy.",
            "Okres utajenia ≈ 25 min w 37 °C, wydajność (burst size) ≈ 100–150 wirionów.",
            "Liza: holiny perforują błonę wewnętrzną, endolizyna tnie peptydoglikan, spaniny rozrywają błonę zewnętrzną – komórka pęka eksplozywnie, zwykle na biegunie.",
            "Nadkażenie wywołuje inhibicję lizy (LIN); DNA drugiego faga jest blokowane (wykluczenie nadkażenia, białka Imm/Sp).",
            "E. coli rosną tu w mikrokoloniach przyczepionych do podłoża i powoli się dzielą. Ruch zwolniony 20× (×0,05), infekcja przyspieszona.",
        };
    }
    public override void Init(World w)
    {
        for (int c = 0; c < 12; c++)
        {
            var (cx, cy) = FreeSpot(w, 3);
            int n = RandI(3, 7); float a0 = Rand(0, TAU);
            for (int i = 0; i < n; i++)
            {
                float x = Clamp(cx + RandN() * 1.6f, 1.5f, w.W - 1.5f), y = Clamp(cy + RandN() * 1.6f, 1.5f, w.H - 1.5f);
                var e = w.Add(new EColi(w, x, y, a0 + RandN() * 0.4f, Rand(2.0f, 3.0f), sessile: true, nf: RandI(2, 4)));
                e.Resistant = Chance(0.2f);
            }
        }
        for (int i = 0; i < 14; i++) w.Add(new PhageT4(w, Rand(0.5f, w.W - 0.5f), Rand(0.5f, w.H - 0.5f), Rand(0, TAU)));
        float px = w.W / 2, py = w.H / 2;
        for (int k = 0; k < 120; k++)
        {
            bool ok = true;
            foreach (var e in w.Ents) if (e is EColi && Hypot(e.X - px, e.Y - py) < e.R + 1.6f) ok = false;
            if (ok) break;
            px = Rand(2, w.W - 2); py = Rand(2, w.H - 2);
        }
        w.SetPlayer(w.Add(new PhageT4(w, px, py, Rand(0, TAU))));
        w.Stats["virions"] = 1;
        w.Log("Jesteś wirionem T4 (≈ 200 nm). Dryfujesz ruchami Browna. Zielone kropki na minimapie to komórki E. coli.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Ff = (w.Focus != null && w.Input.ActionHeld) ? 6 : 1;
        if (!w.EverySecond(gdt)) return;
        int tot = w.Count(e => e is PhageT4 q && q.State == St.Free);
        if (tot > PhageCap)
        {
            int kill = tot - PhageCap;
            foreach (var e in w.Ents) { if (kill <= 0) break; if (e is PhageT4 q && q.Alive && q.State == St.Free && !q.Lineage && q != w.Player) { q.Alive = false; kill--; } }
        }
        int cells = w.Count(e => e is EColi c && c.State != St.Dead);
        if (cells < 25 && !w.Stats.ContainsKey("refill"))
        {
            w.Stats["refill"] = 1; w.Wave++;
            w.Banner("Nowa kolonia", "Świeże komórki E. coli osiadają na podłożu...");
            w.After(2.5f, () =>
            {
                for (int c = 0; c < 4; c++)
                {
                    var (cx, cy) = FreeSpot(w, 3);
                    int n = RandI(3, 6);
                    for (int i = 0; i < n; i++) { var e = w.Add(new EColi(w, cx + RandN() * 1.5f, cy + RandN() * 1.5f, Rand(0, TAU), sessile: true, nf: RandI(2, 4))); e.Resistant = Chance(0.2f); }
                }
                w.Stats.Remove("refill");
            });
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
            ("E. coli w polu", w.Count(e => e is EColi c && c.State != St.Dead).ToString()), ("Wolne fagi", w.Count(e => e is PhageT4 q && q.State == St.Free).ToString()),
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
//  Biofilm – E. coli i Bdellovibrio
// =====================================================================
public abstract class ScBiofilm : Scenario
{
    protected ScBiofilm()
    {
        Wd = 260; Ht = 180; ViewW = 30; TimeScale = 0.25f; Style = ScopeStyle.Phase; Modes = LightModes; GrowEcoli = true; GridCell = 6;
        Mag = "Obiektyw 100× imersyjny (1000×)";
        DebrisN = 1200; DebrisS0 = 0.08f; DebrisS1 = 0.5f; DebrisD = 0.03f; DebrisCols = new[] { Rgb(40, 45, 40, 0.5f), Rgb(255, 255, 240, 0.5f), Rgb(30, 30, 30, 0.5f) };
    }
    protected static void Common(World w, int blobs, float rich, int ecoli, int phages, int bdello, int acanth, int grains)
    {
        w.Nutri = new Field(w.W, w.H, 120);
        for (int i = 0; i < blobs; i++) w.Nutri.Blob(Rand(5, w.W - 5), Rand(5, w.H - 5), Rand(5, 12), Rand(0.4f, 0.9f) * rich);
        for (int i = 0; i < grains; i++) { var (x, y) = FreeSpot(w, 12); w.Add(new Grain(w, x, y, Rand(3, 11))); }
        for (int i = 0; i < ecoli; i++) { var (x, y) = FreeSpot(w, 2); w.Add(new EColi(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < phages; i++) w.Add(new PhageT4(w, Rand(2, w.W - 2), Rand(2, w.H - 2), Rand(0, TAU)));
        for (int i = 0; i < bdello; i++) { var (x, y) = FreeSpot(w, 2); w.Add(new Bdello(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < acanth; i++) { var (x, y) = FreeSpot(w, 15); w.Add(new Acanth(w, x, y)); }
    }
    protected static int FreeEcoli(World w) => w.Count(e => e is EColi c && c.State != St.Dead && c.State != St.Bdelloplast && c.State != St.Cup && c.State != St.Vac);
    protected const string AcanthFact = "Acanthamoeba: trofozoit ≈ 12–35 µm z kolczastymi akantopodiami (do ~8 µm), jądro z dużym ciemnym jąderkiem. Pożera bakterie przez fagocytozę – spotyka je głównie przypadkiem, pełzając powoli.";
}

public sealed class ScBdello : ScBiofilm
{
    public ScBdello()
    {
        Id = "bdello"; Name = "Bdellovibrio"; Latin = "Bdellovibrio bacteriovorus"; TypeName = "Bakteria drapieżna"; Size = "≈ 0,3 × 1 µm"; Color = Hex("#9fd0a0");
        Desc = "Najszybszy drapieżnik mikroświata (do 160 µm/s). Uderz w E. coli, wwierć się do peryplazmy i zjedz ofiarę od środka. Omijaj pełzające Acanthamoeby.";
        Bio = 2000; PhageCap = 30; Starve = 160; ScoreLabel = "ofiar";
        Controls = "WASD / przytrzymaj LPM – płyń · Shift – sprint 160 µm/s · Spacja (przytrzymaj) – przyspiesz czas wewnątrz ofiary";
        Goals = new[]
        {
            new Goal("Upoluj pierwszą ofiarę", 1, w => w.Stat("kills")),
            new Goal("Twoja populacja: 8 komórek", 8, w => w.Count(e => e is Bdello && e.Lineage)),
            new Goal("Zjedz 10 ofiar", 10, w => w.Stat("kills")),
            new Goal("Twoja populacja: 40 komórek", 40, w => w.Count(e => e is Bdello && e.Lineage)),
        };
        Facts = new[]
        {
            "Rozmiar ≈ 0,2–0,5 × 0,5–2,5 µm, jedna osłonięta rzęska biegunowa. Prędkość ≈ 35–160 µm/s – nawet ~100 długości ciała na sekundę.",
            "Poluje na bakterie Gram-ujemne (np. E. coli). Przyczepia się biegunem, enzymatycznie „wierci” otwór w błonie zewnętrznej i wnika do peryplazmy.",
            "Ofiara ginie i zaokrągla się w bdeloplast. Bdellovibrio rośnie w niej jako długi filament, trawiąc zawartość komórki.",
            "Filament dzieli się na 2 (małe ofiary) lub 3–6+ (duże ofiary) potomków, które lizują resztki komórki i odpływają.",
            "Komórki w fazie ataku nie rosną – bez ofiary tracą energię (w grze pasek „Energia”).",
            AcanthFact,
            "Cały cykl trwa ≈ 3–4 h. W grze: ruch zwolniony 4× (×0,25), cykl życiowy mocno przyspieszony. Liczba Reynoldsa ≈ 10⁻⁴ – brak bezwładności.",
        };
    }
    public override void Init(World w)
    {
        Common(w, 16, 0.35f, 180, 0, 6, 6, 30);
        var (x, y) = FreeSpot(w, 2);
        for (int k = 0; k < 50 && w.Near(x, y, 30, e => e is Acanth) != null; k++) (x, y) = FreeSpot(w, 2);
        w.SetPlayer(w.Add(new Bdello(w, x, y, Rand(0, TAU))));
        w.Log("Jesteś Bdellovibrio. Namierz E. coli (zielone na minimapie) i uderz przednim biegunem. Czerwone = Acanthamoeba.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Nutri.Diffuse(dt, 3);
        var p = w.Player;
        w.Ff = (p != null && p.State != St.Swim && w.Input.ActionHeld) ? 5 : 1;
        if (!w.EverySecond(gdt)) return;
        if (FreeEcoli(w) < 140) for (int i = 0; i < 3; i++) { var (x, y) = EdgePos(w, 2); w.Add(new EColi(w, x, y, Rand(0, TAU))); }
        if (Chance(0.15f)) w.Nutri.Blob(Rand(5, w.W - 5), Rand(5, w.H - 5), Rand(4, 8), 0.3f);
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player;
        string st = p == null ? "-" : p.State switch { St.Swim => "faza ataku (pływanie)", St.Attach => "przyczepienie, wiercenie", St.Enter => "wnikanie do peryplazmy", St.Grow => "wzrost filamentu (bdeloplast)", St.Septate => "podział na potomków", St.Cup => "pochłaniany!", St.Vac => "trawiony w wodniczce", _ => "-" };
        return new() { ("Stan", st), ("Zjedzone ofiary", N(w.Stat("kills"))), ("Potomkowie", N(w.Stat("born"))), ("Twoja populacja", w.Count(e => e is Bdello && e.Lineage).ToString()), ("E. coli w polu", FreeEcoli(w).ToString()) };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Bdello b) return null;
        return new() { ("Energia (faza ataku)", 1 - b.Hunger, b.Hunger > 0.7f ? Hex("#e05a4a") : Hex("#8fe0a0")) };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is EColi c && c.CanHost()) return 1;
        if (e is Acanth) return 2;
        return e is Bdello && e.Lineage && e != w.Player ? 3 : 0;
    }
    public override float Score(World w) => w.Stat("kills");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Bdello(w, 0, 0, 0)); e.D = e.Dr = 0; return (e, 3.4f); }
}

public sealed class ScEcoli : ScBiofilm
{
    public ScEcoli()
    {
        Id = "ecoli"; Name = "Escherichia coli"; Latin = "Escherichia coli"; TypeName = "Bakteria"; Size = "≈ 2 × 0,8 µm"; Color = Hex("#b8c7a8");
        Desc = "Żyw się glukozą i dziel co ~20 min. Omijaj ampicylinę (liza przy podziale), drapieżne Bdellovibrio, pełzające Acanthamoeby i fagi T4.";
        Bio = 250; PhageCap = 60; ScoreLabel = "komórek (maks. populacja)";
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
            AcanthFact,
            "Mapy glukozy i ampicyliny to wizualizacje pomocnicze (cząsteczek nie widać w mikroskopie); dyfuzja w grze jest spowolniona.",
        };
    }
    public override void Init(World w)
    {
        Common(w, 40, 1.3f, 60, 8, 0, 5, 20);
        w.Amp = new Field(w.W, w.H, 80);
        for (int i = 0; i < 4; i++) { float x = Rand(20, w.W - 20), y = Rand(20, w.H - 20); w.Stats[$"ampx{i}"] = x; w.Stats[$"ampy{i}"] = y; w.Amp.Blob(x, y, 14, 1.2f); }
        var (px, py) = FreeSpot(w, 2);
        for (int k = 0; k < 60 && (w.Amp.Sample(px, py) > 0.05f || w.Near(px, py, 30, e => e is Acanth) != null); k++) (px, py) = FreeSpot(w, 2);
        var me = w.Add(new EColi(w, px, py, Rand(0, TAU), 2.2f));
        w.Nutri.Blob(px, py, 7, 0.8f);
        w.SetPlayer(me); w.ShowField = true;
        w.Log("Jesteś E. coli. Pomarańczowa mapa = glukoza, czerwona = ampicylina (G – wyłącz mapy). Rośnij i dziel się!", "info");
        w.After(40, () =>
        {
            if (w.Over) return;
            for (int i = 0; i < 3; i++) { var (x2, y2) = EdgePos(w, 3); w.Add(new Bdello(w, x2, y2, Rand(0, TAU))); }
            w.Log("Do pola widzenia wpłynęły drapieżne Bdellovibrio!", "bad");
        });
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Nutri.Diffuse(dt, 4);
        w.Amp.Diffuse(dt, 2.5f);
        if (w.EverySecond(gdt))
        {
            for (int i = 0; i < 4; i++) w.Amp.Blob(w.Stat($"ampx{i}"), w.Stat($"ampy{i}"), 10, 0.08f);
            if (Chance(0.6f)) w.Nutri.Blob(Rand(5, w.W - 5), Rand(5, w.H - 5), Rand(4, 10), Rand(0.5f, 1.0f));
            if (w.Count(e => e is EColi c && c.State != St.Dead && !c.Lineage) < 50) { var (x, y) = EdgePos(w, 2); w.Add(new EColi(w, x, y, Rand(0, TAU))); }
            if (w.Count(e => e is PhageT4 q && q.State == St.Free) < 6) w.Add(new PhageT4(w, Rand(2, w.W - 2), Rand(2, w.H - 2), Rand(0, TAU)));
            if (w.T > 45 && w.Count(e => e is Bdello) < 6 && Chance(0.05f)) { var (x, y) = EdgePos(w, 3); w.Add(new Bdello(w, x, y, Rand(0, TAU))); }
            int pop = Pop(w);
            if (pop > w.Stat("maxpop")) w.Stats["maxpop"] = pop;
        }
        if (w.Player is EColi p && p.Inf != null && !p.Warned) { p.Warned = true; w.Log("Fag T4 wstrzyknął DNA do Twojej komórki! Za chwilę liza...", "bad"); }
    }
    static int Pop(World w) => w.Count(e => e is EColi c && c.Lineage && c.State != St.Dead && c.State != St.Bdelloplast && c.State != St.Cup && c.State != St.Vac);
    public override void DrawUnder(World w, Painter p, View v)
    {
        if (!w.ShowField) return;
        w.Nutri.Draw(p, Rgb(255, 140, 20), 0.42f);
        w.Amp.Draw(p, Rgb(230, 30, 90), 0.4f);
    }
    public override void DrawMinimap(World w, Painter p)
    {
        w.Nutri.Draw(p, Rgb(255, 140, 20), 0.5f);
        w.Amp.Draw(p, Rgb(230, 30, 90), 0.55f);
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
        if (e is Bdello || e is Acanth) return 2;
        return e is EColi c && c.Lineage && e != w.Player && c.State != St.Dead ? 3 : 0;
    }
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var p = w.Player; if (p == null) return false;
        float best = 0.25f;
        for (int k = 0; k < 24; k++)
        {
            float a = k * TAU / 24, r = ViewW * (k % 2 == 0 ? 0.7f : 1.4f);
            float qx = Clamp(p.X + MathF.Cos(a) * r, 0, w.W), qy = Clamp(p.Y + MathF.Sin(a) * r, 0, w.H);
            float val = w.Nutri.Sample(qx, qy) - w.Amp.Sample(qx, qy) * 2;
            if (val > best) { best = val; x = qx; y = qy; }
        }
        return best > 0.25f;
    }
    public override float Score(World w) => w.Stat("maxpop");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new EColi(w, 0, 0, 0, 2.3f)); e.D = e.Dr = 0; return (e, 8); }
}

// =====================================================================
//  Tkanka – neutrofil, żyłka pozawłośniczkowa, kolagen
// =====================================================================
public sealed class ScNeutro : Scenario
{
    const float VHW = 16; // połowa średnicy żyłki (µm)
    public ScNeutro()
    {
        Id = "neutro"; Name = "Neutrofil"; Latin = "Granulocyt obojętnochłonny (Homo sapiens)"; TypeName = "Komórka odporności"; Size = "≈ 12 µm"; Color = Hex("#c9b8d8");
        Desc = "Przeciskaj się między włóknami kolagenu i pochłaniaj gronkowce oraz paciorkowce. Uważaj na leukocydyny – w ostateczności wyrzuć sieć NET.";
        Wd = 520; Ht = 360; ViewW = 72; TimeScale = 40; Bio = 1; Style = ScopeStyle.Tissue; Modes = LightModes; FlowX = 0.002f; GridCell = 20;
        Mag = "Obiektyw 40× (400×) – time-lapse"; ScoreLabel = "zabitych bakterii";
        DebrisN = 700; DebrisS0 = 0.3f; DebrisS1 = 1.4f; DebrisD = 0.01f; DebrisCols = new[] { Rgb(60, 55, 50, 0.5f), Rgb(255, 255, 240, 0.5f) };
        Controls = "WASD / przytrzymaj LPM – pełzanie (chemotaksja) · dotknij bakterii, by ją sfagocytować · Spacja – NEToza (sieć NET, neutrofil ginie)";
        Goals = new[]
        {
            new Goal("Sfagocytuj 5 bakterii", 5, w => w.Stat("engulfed")),
            new Goal("Zabij 25 bakterii", 25, w => w.Stat("killed")),
            new Goal("Przetrwaj do 3. fali zakażenia", 3, w => w.Wave),
            new Goal("Zabij 120 bakterii", 120, w => w.Stat("killed")),
        };
        LoseText = "Wszystkie neutrofile zginęły";
        Facts = new[]
        {
            "Średnica ≈ 12 µm; jądro 3–5-płatowe; setki ziarnistości (azurofilne z mieloperoksydazą, swoiste z laktoferyną).",
            "Pełzanie ameboidalne ≈ 10–20 µm/min: z przodu lamellipodium (bez ziarnistości), z tyłu uropod. W tkance przeciska się między włóknami kolagenu.",
            "Neutrofile opuszczają krew w żyłkach pozawłośniczkowych (toczenie, adhezja, diapedeza) – w grze nowe neutrofile wychodzą z żyłki.",
            "Fagocytoza → fagosom: NADPH-oksydaza wytwarza reaktywne formy tlenu, ziarnistości się z nim zlewają (degranulacja). Neutrofil nie odtwarza ziarnistości.",
            "Leukocydyny S. aureus (np. PVL – typowa dla szczepów z zakażeń martwiczych) tworzą pory w błonie neutrofila. Martwy neutrofil uwalnia niestrawione bakterie.",
            "NEToza (Brinkmann i in., Science 2004): neutrofil wyrzuca sieć chromatyny z histonami i elastazą, która wiąże i zabija bakterie – kosztem własnego życia.",
            "S. aureus dzieli się w 3 płaszczyznach („grona”), Streptococcus w jednej – stąd łańcuszki.",
            "Erytrocyt: dysk dwuwklęsły ≈ 7,8 µm. W time-lapse ×40 przepływ krwi w żyłce widać jako rozmyte smugi.",
        };
    }
    float Yc(float x) => Ht * 0.52f + 26 * MathF.Sin(x * TAU / 330 + 0.7f);
    void SpawnBact(World w, int clusters, int chains)
    {
        for (int c = 0; c < clusters; c++)
        {
            var (x, y) = FreeSpot(w, 3); if (MathF.Abs(y - Yc(x)) < VHW + 6) continue;
            int n = RandI(1, 4);
            for (int k = 0; k < n; k++) w.Add(new Staph(w, x + RandN() * 0.8f, y + RandN() * 0.8f));
        }
        for (int c = 0; c < chains; c++)
        {
            var (x, y) = FreeSpot(w, 3); if (MathF.Abs(y - Yc(x)) < VHW + 6) continue;
            float a = Rand(0, TAU);
            for (int k = 0; k < 3; k++) w.Add(new Staph(w, x + MathF.Cos(a) * k * 0.8f, y + MathF.Sin(a) * k * 0.8f, plane: a, strep: true));
        }
    }
    Neutrophil Diapedesis(World w, float x)
    {
        float side = Chance(0.5f) ? 1 : -1, y = Yc(x) + side * (VHW + 8);
        var n = w.Add(new Neutrophil(w, x, y, side > 0 ? PI / 2 : -PI / 2)); n.Lineage = true;
        return n;
    }
    public override void Init(World w)
    {
        for (int i = 0; i < 160; i++)
        {
            var (x, y) = FreeSpot(w, 10);
            if (MathF.Abs(y - Yc(x)) < VHW + 10) continue;
            w.Add(new Collagen(w, x, y, Rand(0, TAU), Rand(25, 80), Rand(0.5f, 0.9f)));
        }
        for (int i = 0; i < 90; i++) { var (x, y) = FreeSpot(w, 4.5f); if (MathF.Abs(y - Yc(x)) > VHW + 6) w.Add(new RBC(w, x, y)); }
        for (int i = 0; i < 60; i++) { var (x, y) = FreeSpot(w, 1.5f); if (MathF.Abs(y - Yc(x)) > VHW + 3) w.Add(new Platelet(w, x, y)); }
        for (int i = 0; i < 8; i++) { var (x, y) = FreeSpot(w, 5); if (MathF.Abs(y - Yc(x)) > VHW + 6) w.Add(new Lymphocyte(w, x, y)); }
        SpawnBact(w, 22, 6);
        var me = Diapedesis(w, w.W / 2);
        w.SetPlayer(me);
        Diapedesis(w, w.W * 0.3f); Diapedesis(w, w.W * 0.75f);
        w.Log("Jesteś neutrofilem, który właśnie wyszedł z żyłki. Bakterie (zielone na minimapie) mnożą się – pochłaniaj je!", "info");
    }
    public override void Constrain(World w, Ent e)
    {
        if (e.Fixed || e is Net) return;
        float yc = Yc(e.X), dy = e.Y - yc, m = VHW + Math.Min(e.Prim.R, 6) * 0.8f;
        if (MathF.Abs(dy) < m) e.Y = yc + (dy >= 0 ? m : -m);
    }
    public override void Update(World w, float dt, float gdt)
    {
        if (!w.EverySecond(gdt)) return;
        int n = w.Count(e => e is Staph s && s.State == St.Free);
        if (n > 350) w.GameOver($"Infekcja wymknęła się spod kontroli ({n} bakterii)");
        if (w.Count(e => e is Staph) == 0 && !w.Stats.ContainsKey("waiting"))
        {
            w.Stats["waiting"] = 1; w.Wave++;
            w.Banner($"Fala {w.Wave}", "Kolejne bakterie przedostają się do tkanki...");
            w.After(2.5f, () => { SpawnBact(w, 12 + w.Wave * 5, 3 + w.Wave * 2); w.Stats.Remove("waiting"); });
        }
        int neut = w.Count(e => e is Neutrophil);
        if (neut < 6 && (w.T % 35f) < gdt * 1.5f)
        {
            float x = w.Player != null ? Clamp(w.Player.X + Rand(-150, 150), 20, w.W - 20) : Rand(20, w.W - 20);
            Diapedesis(w, x);
            w.Log("Diapedeza: nowy neutrofil przeciska się przez śródbłonek żyłki.", "info");
        }
    }
    public override void DrawUnder(World w, Painter p, View v)
    {
        float x0 = Math.Max(0, v.X0 - 40), x1 = Math.Min(w.W, v.X1 + 40);
        float ym = (v.Y0 + v.Y1) / 2;
        if (MathF.Abs(ym - Yc((v.X0 + v.X1) / 2)) > (v.Y1 - v.Y0) / 2 + VHW + 60) return;
        int n = (int)Clamp((x1 - x0) / 4, 8, 400);
        var top = p.Shape; var bot = p.Shape2;
        for (int i = 0; i <= n; i++) { float x = x0 + (x1 - x0) * i / n, yc = Yc(x); top[i] = new Vector2(x, yc - VHW); bot[i] = new Vector2(x, yc + VHW); }
        Span<Vector2> quad = stackalloc Vector2[4];
        for (int i = 0; i < n; i++)
        {
            quad[0] = top[i]; quad[1] = top[i + 1]; quad[2] = bot[i + 1]; quad[3] = bot[i];
            p.FillFan(quad, (top[i].X + top[i + 1].X) / 2, (top[i].Y + bot[i].Y) / 2, Rgb(170, 110, 100, 0.35f), false);
        }
        // rozmyte smugi erytrocytów (time-lapse)
        float t = w.T;
        for (int k = 0; k < 90; k++)
        {
            float lane = (k * 0.618f) % 1f * 2 - 1, spd = 900 * (1 - lane * lane);
            float period = w.W + 200, x = ((k * 173.3f + t * spd) % period) - 100;
            if (x < x0 - 30 || x > x1 + 30) continue;
            float yc = Yc(x), y = yc + lane * VHW * 0.75f, ang = MathF.Atan(26 * TAU / 330 * MathF.Cos(x * TAU / 330 + 0.7f));
            p.Push(); p.Translate(x, y); p.Rotate(ang);
            p.Glow(0, 0, 9, Rgb(150, 60, 55, 0.35f));
            p.FillEllipse(0, 0, 14, 3.2f, 0, Rgb(140, 70, 65, 0.25f));
            p.Pop();
        }
        var topS = new ReadOnlySpan<Vector2>(top, 0, n + 1); var botS = new ReadOnlySpan<Vector2>(bot, 0, n + 1);
        p.SoftStroke(topS, false, 4, Rgb(255, 255, 240, 0.25f)); p.SoftStroke(botS, false, 4, Rgb(255, 255, 240, 0.25f));
        p.Stroke(topS, false, 1.2f, Rgb(90, 70, 70, 0.6f)); p.Stroke(botS, false, 1.2f, Rgb(90, 70, 70, 0.6f));
        for (float x = MathF.Floor(x0 / 38) * 38; x < x1; x += 38)
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float xx = x + (sd > 0 ? 19 : 0), yc = Yc(xx), ang = MathF.Atan(26 * TAU / 330 * MathF.Cos(xx * TAU / 330 + 0.7f));
                p.FillEllipse(xx, yc + sd * (VHW - 1.2f), 7, 1.4f, ang, Rgb(90, 80, 110, 0.45f));
            }
    }
    public override void DrawMinimap(World w, Painter p)
    {
        const int n = 60; Span<Vector2> s = stackalloc Vector2[n + 1];
        for (int i = 0; i <= n; i++) { float x = w.W * i / n; s[i] = new Vector2(x, Yc(x)); }
        p.Stroke(s, false, VHW * 2, Rgb(170, 70, 70, 0.55f));
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Neutrophil;
        return new()
        {
            ("Sfagocytowane", N(w.Stat("engulfed"))), ("Zabite bakterie", N(w.Stat("killed"))),
            ("Fagosomy", p != null ? $"{p.Phagos.Count}/{p.MaxPhago}" : "-"), ("Ziarnistości", p != null ? Pct((float)p.GranLeft / p.G) : "-"),
            ("Bakterie żywe", w.Count(e => e is Staph s && s.State == St.Free).ToString()), ("Neutrofile", w.Count(e => e is Neutrophil).ToString()),
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
        return e is Neutrophil && e != w.Player ? 3 : 0;
    }
    public override bool OnPlayerLost(World w, Ent p)
    {
        var n = w.Near(p.X, p.Y, 1e9f, e => e is Neutrophil);
        if (n == null && w.Stat("reinf") < 5)
        {
            w.AddStat("reinf", 1);
            n = Diapedesis(w, Clamp(p.X, 20, w.W - 20));
            w.Log($"Posiłki: z żyłki wychodzi kolejny neutrofil (rezerwa {5 - w.Stat("reinf"):0}).", "info");
        }
        if (n != null) { w.Player = null; w.SetPlayer(n); w.Log("Przejmujesz kontrolę nad kolejnym neutrofilem.", "info"); return true; }
        return false;
    }
    public override float Score(World w) => w.Stat("killed");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Neutrophil(w, 0, 0, 0)); return (e, 22); }
}

// =====================================================================
//  Staw – ameba, Didinium, pantofelek
// =====================================================================
public abstract class ScPond : Scenario
{
    protected ScPond()
    {
        Style = ScopeStyle.Pond; Modes = LightModes; Wd = 9000; Ht = 6400; GridCell = 250; FlowScale = 2600;
        DebrisN = 2400; DebrisS0 = 3; DebrisS1 = 16; DebrisD = 0.5f; DebrisCols = new[] { Rgb(70, 66, 45, 0.45f), Rgb(255, 255, 235, 0.4f), Rgb(40, 50, 35, 0.5f) };
    }
    protected static void Habitat(World w, int spiro, int grains, int vortColonies, int rotifers)
    {
        for (int i = 0; i < spiro; i++) SpiroCell.Filament(w, Rand(200, w.W - 200), Rand(200, w.H - 200), Rand(0, TAU), RandI(8, 22));
        for (int i = 0; i < grains; i++) { var (x, y) = FreeSpot(w, 120); w.Add(new Grain(w, x, y, Rand(35, 110))); }
        for (int i = 0; i < vortColonies; i++)
        {
            var (x, y) = FreeSpot(w, 60); float a0 = Rand(0, TAU);
            float gr = Rand(25, 45);
            w.Add(new Grain(w, x, y, gr));
            int n = RandI(3, 7);
            for (int k = 0; k < n; k++) w.Add(new Vorticella(w, x, y, a0 + (k - n / 2f) * 0.35f + Rand(-0.1f, 0.1f), gr * 0.9f));
        }
        for (int i = 0; i < rotifers; i++) { var (x, y) = FreeSpot(w, 150); w.Add(new Rotifer(w, x, y, Rand(0, TAU))); }
    }
    protected static void Cilia(World w, int clouds, int para, int chlamy, int didi)
    {
        w.Bact = new BactField(w, 24000);
        for (int i = 0; i < clouds; i++) w.Bact.Cloud(Rand(100, w.W - 100), Rand(100, w.H - 100), Rand(80, 200), RandI(200, 400));
        for (int i = 0; i < para; i++) { var (x, y) = FreeSpot(w, 130); w.Add(new Paramecium(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < chlamy; i++) { var (x, y) = FreeSpot(w, 10); w.Add(new Chlamy(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < didi; i++) { var (x, y) = FreeSpot(w, 80); w.Add(new Didinium(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < 30; i++) { var (x, y) = FreeSpot(w, 60); w.Add(new Diatom(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < 10; i++) { var (x, y) = FreeSpot(w, 150); w.Add(new Closterium(w, x, y, Rand(0, TAU))); }
    }
    public override void DrawUnder(World w, Painter p, View v) { w.Bact?.Draw(p, v); }
    public override void DrawMinimap(World w, Painter p) { w.Bact?.DrawMap(p, Rgb(150, 130, 70, 0.7f)); }
    protected static readonly string[] HabitatFacts =
    {
        "Wrotki (np. Philodina, ≈ 300–450 µm) to zwierzęta wielokomórkowe: dwa wieńce rzęsek na koronie tworzą wir, który wciąga bakterie i glony; mastaks miele pokarm. Dotknięte chowają koronę.",
        "Wirczyki (Vorticella) siedzą na kurczliwym trzonku: spazmonema zwija go w ok. 9 ms (jedna z najszybszych maszyn komórkowych), potem trzonek powoli się prostuje.",
        "Skrętnica (Spirogyra) tworzy nitki komórek o szerokości ~10–100 µm ze spiralnymi, taśmowatymi chloroplastami z pirenoidami.",
        "Woda w preparacie nie stoi: parowanie i różnice temperatur tworzą powolne prądy (tu ≈ kilka µm/s), które niosą bakterie i detrytus.",
    };
}

public sealed class ScAmoeba : ScPond
{
    public ScAmoeba()
    {
        Id = "amoeba"; Name = "Ameba"; Latin = "Amoeba proteus"; TypeName = "Pierwotniak (Amoebozoa)"; Size = "≈ 200–800 µm"; Color = Hex("#b9c4a4");
        Desc = "Olbrzymia komórka bez stałego kształtu. Otaczaj okrzemki, desmidie i wrotki, traw je w wodniczkach i dziel się – zanim zagłodzisz się na śmierć.";
        ViewW = 1150; TimeScale = 25; Bio = 30; Flow = 1.2f; Mag = "Obiektyw 10× (100×) – time-lapse"; ScoreLabel = "biomasy";
        Controls = "WASD / przytrzymaj LPM – wysuwaj nibynóżkę w danym kierunku · otocz pokarm, by go wchłonąć";
        Goals = new[]
        {
            new Goal("Zjedz 3 porcje pokarmu", 3, w => w.Stat("eaten")),
            new Goal("Pochłoń wrotka (dotknięty chowa koronę – wtedy się zmieści)", 1, w => w.Stat("rotifers")),
            new Goal("Podziel się (populacja 2)", 2, w => w.Count(e => e is Amoeba && e.Lineage)),
            new Goal("Twoja populacja: 5 ameb", 5, w => w.Count(e => e is Amoeba && e.Lineage)),
        };
        Facts = new[]
        {
            "Amoeba proteus osiąga ≈ 200–800 µm – widać ją gołym okiem jako białą kropkę.",
            "Ruch ameboidalny: prąd cytoplazmy do ≈ 10 µm/s, a sam środek komórki przesuwa się ok. 4× wolniej (≈ 1–2,5 µm/s).",
            "Ektoplazma (szklista) i endoplazma (ziarnista); z przodu nibynóżki – przejrzysta „czapeczka hialinowa”, z tyłu pomarszczony uroid.",
            "Odżywia się fagocytozą: nibynóżki otaczają ofiarę i zamykają ją w wodniczce trawiennej. Poluje też na wrotki i orzęski.",
            "A. proteus nie tworzy cyst – głodująca kurczy się i w końcu ginie (w grze pasek „Głód”).",
            "Wodniczka tętniąca cyklicznie wyrzuca nadmiar wody. Jądro dyskowate ≈ 40–50 µm; w cytoplazmie kryształy.",
            HabitatFacts[0], HabitatFacts[2], HabitatFacts[3],
        };
    }
    public override void Init(World w)
    {
        Habitat(w, 12, 45, 8, 22);
        for (int i = 0; i < 150; i++) { var (x, y) = FreeSpot(w, 60); w.Add(new Diatom(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < 70; i++) { var (x, y) = FreeSpot(w, 40); w.Add(new Cosmarium(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < 25; i++) { var (x, y) = FreeSpot(w, 150); w.Add(new Closterium(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < 160; i++) { var (x, y) = FreeSpot(w, 20); w.Add(new Floc(w, x, y, Rand(0, TAU))); }
        for (int i = 0; i < 6; i++) { var (x, y) = FreeSpot(w, 300); w.Add(new Amoeba(w, x, y, Rand(120, 150))); }
        var (px, py) = FreeSpot(w, 300);
        w.SetPlayer(w.Add(new Amoeba(w, px, py, 150)));
        w.Stats["pop"] = 1;
        w.Log("Jesteś Amoeba proteus. Zielone na minimapie = pokarm. Pilnuj paska głodu!", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        if (!w.EverySecond(gdt)) return;
        if (w.Count(e => e is Food f && f.State == St.Free) < 320)
        {
            var (x, y) = EdgePos(w, 40); float r = R01();
            if (r < 0.45f) w.Add(new Diatom(w, x, y, Rand(0, TAU)));
            else if (r < 0.7f) w.Add(new Floc(w, x, y, 0));
            else if (r < 0.92f) w.Add(new Cosmarium(w, x, y, Rand(0, TAU)));
            else w.Add(new Closterium(w, x, y, Rand(0, TAU)));
        }
        if (w.Count(e => e is Rotifer) < 16) { var (x, y) = EdgePos(w, 150); w.Add(new Rotifer(w, x, y, Rand(0, TAU))); }
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Amoeba;
        return new()
        {
            ("Zjedzone", N(w.Stat("eaten"))), ("Biomasa (pokarm)", $"{w.Stat("biomass"):0.0}"), ("Do podziału", p != null ? Pct(Math.Min(1, p.EnergyFrac)) : "-"),
            ("Wodniczki pokarmowe", p != null ? p.Meals.Count.ToString() : "-"), ("Średnica zastępcza", p != null ? $"{2 * MathF.Sqrt(p.A0 / PI):0} µm" : "-"),
            ("Twoja populacja", w.Count(e => e is Amoeba && e.Lineage).ToString()),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Amoeba a) return null;
        return new() { ("Głód", Math.Min(1, a.Hunger), a.Hunger > 0.6f ? Hex("#e05a4a") : Hex("#e0b050")) };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e.Edible && e.State == St.Free) return 1;
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
        Desc = "Beczułkowaty drapieżnik z ryjkiem. Trafiaj pantofelki przodem, wystrzel toksycysty i połknij ofiarę większą od siebie – głód oznacza encystację.";
        ViewW = 1050; TimeScale = 0.5f; Bio = 250; Starve = 75; Flow = 8; Mag = "Obiektyw 10× (100×)"; ScoreLabel = "pantofelków";
        Controls = "WASD / przytrzymaj LPM – płyń · Shift – szybciej · uderz ryjkiem (przodem) w pantofelka · głód = encystacja!";
        Goals = new[]
        {
            new Goal("Upoluj pierwszego pantofelka", 1, w => w.Stat("eaten")),
            new Goal("Podziel się (populacja 2)", 2, w => w.Count(e => e is Didinium && e.Lineage)),
            new Goal("Zjedz 6 pantofelków", 6, w => w.Stat("eaten")),
            new Goal("Twoja populacja: 8", 8, w => w.Count(e => e is Didinium && e.Lineage)),
        };
        Facts = new[]
        {
            "Didinium nasutum: ≈ 60–200 µm, kształt beczułki z ryjkiem (proboscis) i dwoma wieńcami rzęsek (pektynele).",
            "Wyspecjalizowany drapieżnik pantofelków – ofiara bywa 1,5–2× większa od niego.",
            "Po kontakcie ryjka z ofiarą wystrzeliwuje toksycysty (paraliżujące) i peksycysty (przytrzymujące). Cytostom rozciąga się ogromnie.",
            "Pantofelek broni się, wystrzeliwując trichocysty – przeciw Didinium tylko częściowo skutecznie.",
            "Głodujący Didinium tworzy cystę spoczynkową – w grze oznacza to koniec rozgrywki.",
            HabitatFacts[0], HabitatFacts[1], HabitatFacts[3],
        };
    }
    public override void Init(World w)
    {
        Habitat(w, 10, 35, 10, 10);
        Cilia(w, 50, 95, 90, 3);
        var (px, py) = FreeSpot(w, 150);
        w.SetPlayer(w.Add(new Didinium(w, px, py, Rand(0, TAU))));
        w.Stats["pop"] = 1;
        w.Log("Jesteś Didinium. Pantofelek musi dotknąć Twojego ryjka (przodu). Zielone na minimapie = pantofelki.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Bact.Update(dt, gdt);
        if (!w.EverySecond(gdt)) return;
        if (w.Count(e => e is Paramecium q && q.State != St.Eaten) < 75) { var (x, y) = EdgePos(w, 130); w.Add(new Paramecium(w, x, y, Rand(0, TAU))); }
        if (w.Bact.N < 16000) w.Bact.Cloud(Rand(100, w.W - 100), Rand(100, w.H - 100), 150, 300);
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Didinium;
        string st = p == null ? "-" : p.State switch { St.Swim => "polowanie", St.Capture => "toksycysty!", St.Swallow => "połykanie", St.Digest => "trawienie", St.Cyst => "encystacja", _ => "-" };
        return new()
        {
            ("Stan", st), ("Zjedzone pantofelki", N(w.Stat("eaten"))),
            ("Twoja populacja", w.Count(e => e is Didinium && e.Lineage).ToString()), ("Paramecium w polu", w.Count(e => e is Paramecium q && q.State != St.Eaten).ToString()),
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
        Desc = "Wsysaj bakterie rowkiem gębowym i rozmnażaj się. Uciekaj przed Didinium (Spacja – trichocysty), omijaj wiry wrotków.";
        ViewW = 900; TimeScale = 0.5f; Bio = 150; Starve = 160; Flow = 8; Mag = "Obiektyw 10× (100×)"; ScoreLabel = "bakterii";
        Controls = "WASD / przytrzymaj LPM – płyń · Shift – ucieczka · Spacja – trichocysty · przepływaj przez chmury bakterii";
        Goals = new[]
        {
            new Goal("Zjedz 150 bakterii", 150, w => w.Stat("bact")),
            new Goal("Podziel się (populacja 2)", 2, w => w.Count(e => e is Paramecium q && q.Lineage && q.State != St.Eaten)),
            new Goal("Wyrwij się Didinium dzięki trichocystom", 1, w => w.Stat("escapes")),
            new Goal("Twoja populacja: 6", 6, w => w.Count(e => e is Paramecium q && q.Lineage && q.State != St.Eaten)),
        };
        Facts = new[]
        {
            "Paramecium caudatum: ≈ 200 (do 300) µm, tylny koniec zaostrzony (caudatum = „ogoniasty”).",
            "Tysiące rzęsek biją skoordynowanie (fala metachroniczna). Pływa po spirali ≈ 1 mm/s.",
            "Reakcja unikania: po uderzeniu w przeszkodę depolaryzacja błony odwraca bicie rzęsek – pantofelek cofa się i skręca.",
            "Rowek gębowy prowadzi do cytostomu; bakterie trafiają do wodniczek pokarmowych, które krążą po komórce (cykloza).",
            "Dwie gwiaździste wodniczki tętniące z kanalikami usuwają nadmiar wody. Makronukleus i mikronukleus; podział poprzeczny.",
            "Trichocysty: „igły” pod pellikulą, które w kilka milisekund wydłużają się kilkukrotnie – mechanizm obronny.",
            HabitatFacts[0], HabitatFacts[1], HabitatFacts[3],
        };
    }
    public override void Init(World w)
    {
        Habitat(w, 10, 35, 14, 12);
        Cilia(w, 70, 25, 80, 7);
        var (x, y) = FreeSpot(w, 150);
        for (int k = 0; k < 80 && w.Near(x, y, 1100, e => e is Didinium) != null; k++) (x, y) = FreeSpot(w, 150);
        w.SetPlayer(w.Add(new Paramecium(w, x, y, Rand(0, TAU))));
        w.Stats["pop"] = 1;
        w.Log("Jesteś pantofelkiem. Brązowe plamy na minimapie to chmury bakterii, czerwone – drapieżniki.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        w.Bact.Update(dt, gdt);
        if (!w.EverySecond(gdt)) return;
        if (w.Bact.N < 19000) w.Bact.Cloud(Rand(100, w.W - 100), Rand(100, w.H - 100), Rand(80, 180), 300);
        if (w.Count(e => e is Didinium) < 7 + (int)(w.T / 60)) { var (x, y) = EdgePos(w, 80); w.Add(new Didinium(w, x, y, Rand(0, TAU))); }
        if (w.Count(e => e is Paramecium q && !q.Lineage && q.State != St.Eaten) < 18) { var (x, y) = EdgePos(w, 130); w.Add(new Paramecium(w, x, y, Rand(0, TAU))); }
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Paramecium;
        return new()
        {
            ("Zjedzone bakterie", N(w.Stat("bact"))), ("Wodniczki pokarmowe", p != null ? p.Fv.Count.ToString() : "-"),
            ("Do podziału", p != null ? Pct(Math.Min(1, p.Energy / 6)) : "-"), ("Trichocysty", p != null ? (p.TrichCD > 0 ? $"ładowanie {p.TrichCD:0.0} s" : "gotowe") : "-"),
            ("Twoja populacja", w.Count(e => e is Paramecium q && q.Lineage && q.State != St.Eaten).ToString()), ("Drapieżniki", w.Count(e => e is Didinium).ToString()),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Paramecium p) return null;
        return new() { ("Głód", p.Hunger, p.Hunger > 0.7f ? Hex("#e05a4a") : Hex("#e0b050")) };
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Didinium || e is Rotifer) return 2;
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

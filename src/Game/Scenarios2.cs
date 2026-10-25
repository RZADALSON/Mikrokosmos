namespace Mikro;

// =====================================================================
//  Niesporczak w poduszce mchu – cykle wysychania i deszczu
// =====================================================================
public sealed class ScTardi : Scenario
{
    public override string InspectAt(World w, float x, float y) => Algae != null && Algae.Nearest(x, y, 14, out _, out _) ? "chlorella" : "moss-water";
    public AlgaField Algae;
    /// <summary>Wilgotność błonki wody 0..1.</summary>
    public float Moisture = 1;
    enum Wx { Wet, Drying, Dry, Rain }
    Wx _wx; float _wxT, _dryDur;
    public ScTardi()
    {
        Id = "tardi"; Name = "Niesporczak"; Latin = "Hypsibius exemplaris"; TypeName = "Zwierzę (Tardigrada)"; Size = "≈ 250 µm"; Color = Hex("#d8cfa8");
        ReproFact = "Hypsibius exemplaris to same samice rozmnażające się partenogenetycznie: przy linieniu składają kilka jaj do zrzuconego oskórka. Młode rosną przez kolejne linienia. Linia przetrwa suszę tylko wtedy, gdy zdąży zwinąć się w beczułkę.";
        Desc = "Chodź na ośmiu nóżkach po poduszce mchu i wysysaj glony. Mech co chwilę wysycha – zdąż zwinąć się w beczułkę (anhydrobioza). Uważaj na drapieżnego Milnesium.";
        Wd = 6000; Ht = 6000; ViewW = 1000; TimeScale = 1; Bio = 1; Style = ScopeStyle.Pond; Modes = LightModes; GridCell = 120; Water = true;
        ChunkSize = 1000; ActiveR = 2; ParkCap = 50;
        Look = BgLook.Moss;
        Zones = new[] { new BgZone(0.55f, 0.2f, 0.15f, 0.3f, 0.1f, 0.47f, 0.52f, 0.45f), new BgZone(0.2f, 0.6f, 0.1f, 0.35f, 0.15f, 0.46f, 0.53f, 0.45f), new BgZone(0.3f, 0.1f, 0.2f, 0.7f, 0.25f, 0.54f, 0.5f, 0.44f), new BgZone(0.08f, 0.1f, 0.05f, 0.25f, 0.05f) };
        BiomeNames = new[] { "Listki mchu", "Kożuchy glonów", "Piasek i detrytus", "Szczelina z wodą" };
        BiomeColors = new[] { Rgb(110, 160, 70), Rgb(70, 190, 60), Rgb(170, 150, 110), Rgb(90, 140, 170) };
        Mag = "Obiektyw 4× (40×) – czas rzeczywisty, pogoda przyspieszona"; LineUnit = "niesporczaków i jaj"; BirthWord = "wyklutych";
        DebrisN = 1800; DebrisS0 = 2; DebrisS1 = 9; DebrisD = 0.3f; DebrisCols = new[] { Rgb(70, 66, 45, 0.45f), Rgb(255, 255, 235, 0.4f) };
        Controls = "WASD / przytrzymaj LPM – chodzenie · Shift – bieg · zatrzymaj się przy glonach, by je wyssać · Spacja – zwiń się w beczułkę / rozwiń";
        Goals = new[]
        {
            new Goal("Wyssij 15 komórek glonów Chlorella", 15, w => w.Stat("algae")),
            new Goal("Przetrwaj wyschnięcie mchu jako beczułka", 1, w => w.Stat("survived")),
            new Goal("Najedz się i złóż jaja do wylinki", 1, w => w.Stat("eggs")),
            new Goal("Twoja populacja: 5 niesporczaków", 5, w => w.CountAll(e => e is Tardigrade t && !t.Predator && t.Lineage)),
        };
        LoseText = "Twoje niesporczaki zginęły";
        Facts = new[]
        {
            "Hypsibius exemplaris (≈ 250 µm) ma 4 pary krępych nóżek z pazurkami. Chodzi średnio ≈ 163 µm/s (ok. pół długości ciała na sekundę; zakres ≈ 80–260 µm/s) – przyspiesza jak owady: częstszymi krokami, bez zmiany chodu. Kroki biegną falą od tyłu ku przodowi, a nogi jednej pary są w przeciwfazie (Nirody i in., PNAS 2021).",
            "Pokarm: glony (np. Chlorella) i sinice. Niesporczak przebija ścianę komórki dwoma aragonitowymi sztyletami i wysysa zawartość mięśniową gardzielą – zostaje pusta ścianka.",
            "Anhydrobioza: przy powolnym wysychaniu niesporczak wciąga nóżki i zwija się w beczułkę (tun), traci ~95% wody, a metabolizm prawie ustaje. Chronią go m.in. białka nieuporządkowane CAHS/SAHS i białko Dsup chroniące DNA.",
            "Beczułka przeżywa miesiące, a nawet lata; po krótkim uśpieniu niesporczak rusza się po kilku minutach od zwilżenia, po długim – znacznie później i nie zawsze (Roszkowska i in. 2023).",
            "Zbyt szybkie wysychanie zabija – ciało nie zdąży przygotować ochrony. Dlatego niesporczaki żyją w poduszkach mchu, które wysychają powoli.",
            "Milnesium to drapieżny niesporczak z szerokim otworem gębowym. Rozmnaża się tylko na pokarmie zwierzęcym – wysysa wrotki, nicienie i mniejsze niesporczaki.",
            "H. exemplaris rozmnaża się partenogenetycznie: samica przy linieniu składa jaja do zrzuconego oskórka (wylinki). Zarodek rozwija się ok. 4–5 dni (w grze ~50 s).",
            "W mchu żyją też wrotki bdelloidalne i nicienie – one również przetrwają wyschnięcie w stanie anhydrobiozy.",
            "Czas: zwierzęta poruszają się w czasie rzeczywistym, ale pogoda jest mocno przyspieszona – w naturze mech wysycha godzinami, a sucho bywa tygodniami.",
            "Rzut 2D poduszki mchu: listki leżą warstwami jeden na drugim, a zwierzęta chodzą po nich i pod nimi, przechodząc przez krawędzie (blaszka ma grubość jednej komórki) – w grze są rysowane na listkach i nie zderzają się z nimi. Przeszkodą są ziarna piasku (60–180 µm).",
        };
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        int b = c.Biome; bool start = Math.Abs(c.X) + Math.Abs(c.Y) <= 1;
        int leaves = b switch { 0 => RandI(3, 5), 1 => RandI(1, 2), 2 => RandI(0, 1), _ => 2 };
        for (int i = 0; i < leaves; i++) { var (x, y) = Spot(w, c, 250, loc, 120); if (start && Hypot(x, y) < 350) continue; Put(w, loc, new MossLeaf(w, x, y, Rand(0, TAU), Rand(700, 1500))); }
        if (b == 2) for (int i = RandI(4, 6); i > 0; i--) { var (x, y) = Spot(w, c, 80, loc, 80); Put(w, loc, new Grain(w, x, y, Rand(30, 90))); }
        for (int i = b == 2 ? 3 : 1; i > 0; i--) { var (x, y) = Spot(w, c, 50, loc, 50); Put(w, loc, new Detritus(w, x, y, Rand(15, 45))); }
        int patches = b switch { 1 => RandI(6, 9), 0 => RandI(2, 4), _ => RandI(1, 3) };
        for (int i = 0; i < patches; i++) Algae.Patch(c.X0 + Rand(60, c.S - 60), c.Y0 + Rand(60, c.S - 60), Rand(30, b == 1 ? 110 : 60), b == 1 ? RandI(60, 140) : RandI(20, 50));
        if (start) Algae.Patch(Rand(-150, 150), Rand(-150, 150), 70, 70);
        // wrotki bdelloidalne są zwykle najliczniejszymi zwierzętami mchów i kożuchów glonów (setki do dziesiątek tysięcy na gram
        // suchej masy – Lukashanets i in. 2022), niesporczaków ~80–320 na gram mchu (Mäenpää i in. 2024, torfowiska);
        // nicienie – zmiennie. W grze ~4–5 wrotków na niesporczaka, najwięcej w wodzie i przy glonach
        for (int i = b switch { 3 => RandI(6, 9), 1 => RandI(5, 8), 0 => RandI(4, 6), _ => RandI(2, 4) }; i > 0; i--) { var (x, y) = Spot(w, c, 150, loc, 100); if (start && Hypot(x, y) < 250) continue; Put(w, loc, new Rotifer(w, x, y, Rand(0, TAU))); }
        for (int i = b is 2 or 3 ? RandI(1, 2) : Chance(0.5f) ? 1 : 0; i > 0; i--) { var (x, y) = Spot(w, c, 200, loc, 200); Put(w, loc, new Chain(w, Chain.Type.Nematode, x, y, Rand(0, TAU), Rand(400, 900))); }
        for (int i = RandI(0, 2); i > 0; i--) { var (x, y) = Spot(w, c, 150, loc, 100); Put(w, loc, new Tardigrade(w, x, y, Rand(0, TAU))); }
        if (!start && Chance(0.3f)) { var (x, y) = Spot(w, c, 250, loc, 150); Put(w, loc, new Tardigrade(w, x, y, Rand(0, TAU), true)); }
    }
    public override void Init(World w)
    {
        Algae = new AlgaField(ChunkSize); Moisture = 1; Dormant = false; _wx = Wx.Wet; _wxT = 95;
        w.UpdateChunks(true);
        var me = w.Add(new Tardigrade(w, 0, 0, Rand(0, TAU))); me.Energy = 0.45f;
        w.SetPlayer(me);
        w.Log("Jesteś niesporczakiem w poduszce mchu. Zielone kropki to glony – podejdź, zatrzymaj się, a sztylety same je przebiją. Obserwuj wilgotność!", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        var pl = w.Player;
        if (pl != null) Algae.Update(gdt, pl.X, pl.Y, ChunkSize * 1.5f);
        if (pl is Tardigrade me && !w.Preview && w.Input.Action) { me.TunWant = !me.TunWant; w.Sfx(me.TunWant ? "cyst" : "snap", me.X, me.Y, 0.7f); }
        _wxT -= gdt;
        switch (_wx)
        {
            case Wx.Wet:
                Moisture = 1;
                if (_wxT <= 0)
                {
                    _wx = Wx.Drying; _dryDur = Rand(35, 70); _wxT = _dryDur;
                    w.Banner("Mech wysycha!", _dryDur < 45 ? "Słońce i wiatr – woda znika szybko. Zwiń się w beczułkę!" : "Woda powoli paruje – przygotuj się do anhydrobiozy");
                    w.Log("Błonka wody między listkami robi się coraz cieńsza. Beczułka musi się uformować, zanim woda zniknie.", "bad");
                }
                break;
            case Wx.Drying:
                Moisture = Math.Max(0, _wxT / _dryDur);
                if (_wxT <= 0) { _wx = Wx.Dry; _wxT = Rand(12, 20); Moisture = 0; Dormant = true; Desiccate(w); }
                break;
            case Wx.Dry:
                Moisture = 0;
                if (_wxT <= 0) { _wx = Wx.Rain; _wxT = 5; w.Banner("Deszcz!", "Mech nasiąka wodą – niesporczaki rozwijają się z beczułek"); }
                break;
            case Wx.Rain:
                Moisture = Math.Min(1, 1 - _wxT / 5);
                if (Moisture > 0.3f) Dormant = false;
                if (_wxT <= 0) { _wx = Wx.Wet; _wxT = Rand(70, 110); Moisture = 1; if (w.Player is Tardigrade t && t.Tun > 0.5f) w.Log("Woda wróciła – naciśnij Spację, by wyjść z beczułki.", "info"); }
                break;
        }
    }
    /// <summary>Koniec wysychania: przeżywają tylko zwierzęta w beczułce (u wrotków i nicieni – ich własna anhydrobioza).</summary>
    void Desiccate(World w)
    {
        foreach (var e in w.Ents)
        {
            if (e is not Tardigrade t || !t.Alive) continue;
            bool ok = t.IsTun && Chance(t.Energy > 0.2f ? 0.97f : 0.75f);
            if (ok) { if (t.IsPlayer) { w.AddStat("survived", 1); w.Log("Anhydrobioza: beczułka straciła prawie całą wodę, metabolizm stanął. Mijają tygodnie suszy…", "good"); } continue; }
            if (t.IsPlayer) w.Log(t.IsTun ? "Beczułka nie przetrwała – zabrakło zapasów energii na ochronę komórek." : "Woda zniknęła, zanim niesporczak zwinął się w beczułkę – wysechł na śmierć.", "bad");
            t.Kill();
        }
    }
    public override void DrawUnder(World w, Painter p, View v) => Algae?.Draw(p, v);
    /// <summary>Wysychanie: błonka wody pęka na suche plamy (jaśniejsze, bez zawiesiny), najdłużej woda trzyma się przy listkach.</summary>
    public override void DrawOver(World w, Painter p, View v)
    {
        if (Moisture >= 0.999f) return;
        const int nx = 26, ny = 18;
        float x0 = v.X0 - 30, y0 = v.Y0 - 30, cw = (v.X1 - v.X0 + 60) / (nx - 1), ch = (v.Y1 - v.Y0 + 60) / (ny - 1), m = Moisture;
        p.Grid(x0, y0, cw, ch, nx, ny, (i, j) =>
        {
            float n = Noise2((x0 + i * cw) / 400, (y0 + j * ch) / 400, w.Seed + 77);
            float dry = Clamp((n - m * 1.3f + 0.3f) * 3, 0, 1) * (1 - m * 0.5f);
            return new Color(0.93f, 0.9f, 0.8f, 0.45f * dry);
        });
    }
    public override void DrawMinimap(World w, Painter p) { var pl = w.Player; if (pl != null) Algae?.DrawMap(p, pl.X, pl.Y, ChunkSize * 2.5f); }
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var p = w.Player;
        return p != null && Algae.Nearest(p.X, p.Y, ViewW * 3, out x, out y);
    }
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Tardigrade;
        string wx = _wx switch { Wx.Wet => "wilgotno", Wx.Drying => $"wysychanie ({_wxT:0} s)", Wx.Dry => "sucho (tygodnie)", _ => "deszcz" };
        string st = p == null ? "-" : p.IsTun ? "beczułka (anhydrobioza)" : p.Tun > 0.05f ? (p.TunWant ? "zwijanie się…" : "rozwijanie…") : p.Molt > 0 ? "linienie" : "aktywny";
        return new()
        {
            ("Pogoda", wx), ("Wilgotność", Pct(Moisture)), ("Stan", st),
            ("Wyssane glony", N(w.Stat("algae"))), ("Jaja", N(w.Stat("eggs"))), ("Wyklute młode", N(w.Stat("hatched"))),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Tardigrade p) return null;
        var l = new List<(string, float, Color)>
        {
            ("Zapasy energii", Math.Min(1, p.Energy), p.Energy < 0.2f ? Hex("#e05a4a") : Hex("#9fd07a")),
            ("Wilgotność mchu", Moisture, Moisture < 0.35f ? Hex("#e0b050") : Hex("#7fb8e0")),
        };
        if (p.Tun > 0.02f) l.Add(("Beczułka (tun)", p.Tun, Hex("#d8cfa8")));
        if (p.Hp < 0.99f) l.Add(("Integralność ciała", p.Hp, Hex("#e05a4a")));
        return l;
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Tardigrade t) return t.Predator ? 2 : t.Lineage && e != w.Player ? 3 : 0;
        return 0;
    }
    /// <summary>Linia = żywe niesporczaki i jaja w wylinkach (potomstwo, które już istnieje).</summary>
    public override int LinePop(World w)
    {
        int n = w.CountAll(e => e is Tardigrade t && !t.Predator && t.Lineage);
        foreach (var e in w.Ents) if (e is Exuvia x) n += x.MineEggs;
        return n;
    }
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Tardigrade(w, 0, 0, 0)); e.Energy = 0.8f; return (e, 360); }
}

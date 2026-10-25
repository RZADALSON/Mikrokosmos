using System.Linq;
namespace Mikro;

// =====================================================================
//  Świdrowiec (Trypanosoma brucei) we krwi – zmienność antygenowa, usuwanie przeciwciał ruchem,
//  sygnał gęstości SIF i formy krępe, ukłucie muchy tse-tse (przeniesienie)
// =====================================================================
public sealed class ScTryp : Scenario
{
    /// <summary>Ekspozycja układu odpornościowego na każdy wariant (pasożyto-godziny) i miano przeciwciał (0..3).</summary>
    readonly Dictionary<int, float> _exp = new(), _tit = new();
    const float E0 = 1500;
    int _next = 1;
    /// <summary>Sygnał gęstości (SIF) względem progu, miejsce na podziały (wydajność), liczba pasożytów w oknie.</summary>
    public float Sif; public bool Room = true; public int Pop;
    // ukłucie muchy tse-tse
    public bool Bite; public float BiteX, BiteY, BiteA, BiteT;
    float _biteWait = 25;
    const float BiteDur = 28, SuckFrom = 3.5f;

    public ScTryp()
    {
        Id = "tryp"; Name = "Świdrowiec"; Latin = "Trypanosoma brucei"; TypeName = "Pasożyt krwi (wiciowiec)"; Size = "≈ 20–30 µm"; Color = Hex("#b9c9a8");
        Desc = "Świdrowiec śpiączki afrykańskiej we krwi. Pływaj naprzód, by zdzierać przeciwciała z płaszcza VSG, zmieniaj płaszcz, gdy odporność Cię dogoni, a jako forma krępa daj się wyssać musze tse-tse.";
        Wd = 400; Ht = 300; ViewW = 90; TimeScale = 1; Bio = 720; Style = ScopeStyle.Phase; Modes = LightModes; GridCell = 20; Water = false;
        ChunkSize = 60; ActiveR = 2; ParkCap = 60; HasCustomFlow = true;
        // krew ma grubość: erytrocyty w innych płaszczyznach widać jako rozmyte krążki; w wątrobie – hepatocyty, w tłuszczu – krople lipidu
        Look = BgLook.Blood;
        Zones = new[] { new BgZone(0.6f, 0, 0.03f, 0.35f, 0.05f, 0.52f, 0.49f, 0.48f), new BgZone(0.45f, 0.05f, 0.25f, 0.3f, 0.1f, 0.52f, 0.49f, 0.47f), new BgZone(0.35f, 0.4f, 0.1f, 0.3f, 0.15f, 0.53f, 0.49f, 0.45f), new BgZone(0.25f, 0.7f, 0.15f, 0.25f, 0.05f, 0.52f, 0.51f, 0.46f) };
        BiomeNames = new[] { "Krew (osocze z erytrocytami)", "Naczynia skóry – tu kłuje tse-tse", "Zatoki wątroby (komórki Kupffera)", "Tkanka tłuszczowa" };
        BiomeColors = new[] { Rgb(200, 120, 110), Rgb(225, 190, 150), Rgb(140, 90, 70), Rgb(240, 225, 160) };
        Mag = "Obiektyw 40× (400×) – czas rzeczywisty; podziały i odporność przyspieszone ×720"; ScoreLabel = "przeniesionych do tse-tse"; LineUnit = "świdrowców"; BirthWord = "podziałów";
        DebrisN = 600; DebrisS0 = 0.3f; DebrisS1 = 1.2f; DebrisD = 0.05f; DebrisCols = new[] { Rgb(70, 60, 55, 0.4f), Rgb(255, 250, 240, 0.4f) };
        Controls = "WASD / przytrzymaj LPM – kierunek (świdrowiec nie może stanąć) · Spacja (przytrzymaj) – pływanie wstecz · płyń naprzód, by zdzierać przeciwciała";
        LoseText = "Twoja linia wygasła – odporność gospodarza oczyściła krew";
        ReproFact = "Forma smukła dzieli się podłużnie co ~6 h (najpierw nowa wić, kinetoplast, potem jądro; bruzda biegnie od przodu ku tyłowi). Ale geny przechodzą dalej tylko przez muchę tse-tse: klasycznie przenoszą je formy krępe, które przestały się dzielić. Tu dostosowanie to liczba Twoich świdrowców wyssanych przez muchę.";
        Goals = new[]
        {
            new Goal("Podziel się 2 razy (podział podłużny)", 2, w => w.Stat("t_div")),
            new Goal("Przetrwaj pierwszą falę przeciwciał (pływaj naprzód)", 1, w => w.Stat("t_wave")),
            new Goal("Zmienność antygenowa: nowy wariant VSG w Twojej linii", 1, w => w.Stat("t_switch")),
            new Goal("Forma krępa (sygnał gęstości SIF)", 1, w => w.Stat("t_stumpy")),
            new Goal("Daj się wyssać musze tse-tse jako forma krępa", 1, w => w.Stat("transmit")),
        };
        Facts = new[]
        {
            "Trypanosoma brucei wywołuje śpiączkę afrykańską (u bydła – naganę). Forma krwiopochodna smukła ma ≈ 20–30 µm długości i 1,5–3 µm szerokości; w środku jądro, z tyłu kinetoplast – DNA jedynego, olbrzymiego mitochondrium.",
            "Wić wychodzi z kieszonki wiciowej przy tylnym końcu, biegnie wzdłuż ciała (błona falująca) i wystaje wolno z przodu. Fala biegnie od czubka ku tyłowi, więc świdrowiec płynie wicią naprzód; potrafi też odwrócić falę i cofnąć się.",
            "Ruch jest dla niego niezbędny do życia – wyciszenie genów wici zabija formy krwiopochodne (Broadhead i in., Nature 2006). We krwi, między erytrocytami, pływa sprawniej i bardziej kierunkowo niż w samym płynie (Heddergott i in., PLoS Pathog 2012).",
            "Krew: w 1 µl jest ok. 4–6 mln erytrocytów, 150–400 tys. płytek krwi i 4–11 tys. leukocytów. Erytrocyty zajmują ~40–45% objętości krwi (hematokryt), więc świdrowiec przeciska się między nimi; krwinkę białą widać rzadko – przypada jedna na kilkaset czerwonych.",
            "Całą powierzchnię pokrywa płaszcz ~10 mln cząsteczek jednej glikoproteiny VSG. Przeciwciała, które się z nią zwiążą, są przy pływaniu naprzód spychane opływem ku kieszonce wiciowej i tam wchłaniane i trawione – w kilka minut (Engstler i in., Cell 2007). Stojący pasożyt tego nie potrafi.",
            "Zmienność antygenowa: genom ma ok. 2000 genów i pseudogenów VSG, ale w danej chwili ulega ekspresji tylko jeden. Rzadka rekombinacja (w naturze ~10⁻²–10⁻⁶ na podział; w grze ok. 1 na 8) przenosi inny gen do miejsca ekspresji – przeciwciała przestają pasować.",
            "Dlatego parazytemia przychodzi falami: odporność niszczy dominujący wariant, a przeżywają pasożyty, które zdążyły zmienić płaszcz (Ross i Thomson 1910). W jednej infekcji krąży naraz wiele wariantów (Mugnier i in., Science 2015).",
            "Opłaszczone przeciwciałami IgM i IgG pasożyty niszczy dopełniacz, a w wątrobie pochłaniają je komórki Kupffera (Macaskill i in. 1980; Liu i in. 2019).",
            "Wyczuwanie gęstości: pasożyty uwalniają oligopeptydy (SIF), a gdy ich przybywa, formy smukłe zatrzymują cykl w G1 i przechodzą w krępe (Vassella i in. 1997; Rojas i in., Cell 2019). Krępa forma ma aktywniejsze mitochondrium i białko PAD1 (Dean i in., Nature 2009) – jest przygotowana do życia w musze.",
            "Formy krępe nie dzielą się i żyją tylko ~2–3 doby. To hamuje wzrost parazytemii – gospodarz żyje dłużej, więc jest więcej okazji do przeniesienia (MacGregor i in., Nat Rev Microbiol 2012).",
            "Mucha tse-tse (Glossina) rozcina skórę i ssie krew z powstałej „kałuży”. Świdrowce czekają w naczyniach i tkance skóry (Capewell i in., eLife 2016) oraz w tkance tłuszczowej (Trindade i in., Cell Host Microbe 2016).",
            "Klasycznie muchę zarażają tylko formy krępe; Schuster i in. (eLife 2021) pokazali jednak, że czasem mogą to zrobić także smukłe. W grze liczymy formy krępe.",
            "Uproszczenia: pływanie, wiązanie i usuwanie przeciwciał – w czasie rzeczywistym; podziały (~6 h), przemiana w formę krępą (~1 doba) i narastanie przeciwciał (dni) są przyspieszone ×720. Strefy (skóra, wątroba, tłuszcz) leżą w grze obok siebie, a krew jest pokazana tak, jakby kamera płynęła razem z nią.",
            "Adipocyty w grze nie stykają się – między nimi zostaje kanał ≥ 9 µm: tak w rzucie 2D wygląda ciągła, trójwymiarowa przestrzeń śródmiąższowa tkanki tłuszczowej, w której żyją świdrowce.",
        };
    }

    public override string InspectAt(World w, float x, float y)
    {
        if (Bite)
        {
            // kłujka: odcinek od czubka wstecz wzdłuż kierunku ukłucia (szerokość ~15–25 µm)
            float ca = MathF.Cos(BiteA), sa = MathF.Sin(BiteA), dx = x - BiteX, dy = y - BiteY, t = -(dx * ca + dy * sa);
            if (t > -8 && t < 500 && MathF.Abs(-dx * sa + dy * ca) < 8 + t * 0.04f) return "tsetse";
        }
        return "blood-plasma";
    }
    public float Titer(int v) => _tit.TryGetValue(v, out var t) ? t : 0;
    public int NewVariant() => _next++;
    /// <summary>Test: ustawia miano przeciwciał przeciw wariantowi.</summary>
    public void SetTiter(int v, float t) { _tit[v] = t; _exp[v] = E0; }
    /// <summary>Test: miano przeciwciał nie zmienia się.</summary>
    public bool Frozen;
    /// <summary>Prawdopodobieństwo przejścia w formę krępą na sekundę czasu biologicznego (rośnie z SIF powyżej progu).</summary>
    public float CommitRate => Sif > 0.6f ? (Sif - 0.6f) * 0.25f / 3600 : 0;

    public override void Init(World w)
    {
        _exp.Clear(); _tit.Clear(); _next = 1; Frozen = false; Bite = false; _biteWait = 25; Sif = 0; Room = true;
        w.CamX = 0; w.CamY = 0;
        w.UpdateChunks(true);
        foreach (var e in w.Ents) if (e is RBC && Hypot(e.X, e.Y) < 14) e.Alive = false;
        w.Ents.RemoveAll(e => !e.Alive);
        var me = w.Add(new Trypanosome(w, 0, 0, Rand(0, TAU)) { Growth = 0.6f, V0 = 12 });
        for (int i = 0; i < 7; i++) w.Add(new Trypanosome(w, Rand(-80, 80), Rand(-60, 60), Rand(0, TAU)));
        w.SetPlayer(me);
        w.Log("Jesteś świdrowcem we krwi, kilka dni po ukłuciu muchy tse-tse. Twoja wić bije bez przerwy – nie możesz stanąć. Dziel się, a gdy pojawią się przeciwciała, pływaj naprzód: opływ zepchnie je do kieszonki wiciowej.", "info");
    }
    public override void GenerateChunk(World w, Chunk c)
    {
        var loc = new List<Ent>();
        bool start = Math.Abs(c.X) <= 1 && Math.Abs(c.Y) <= 1;
        // tkanka tłuszczowa: świdrowce żyją w przestrzeni śródmiąższowej między adipocytami (Trindade i in. 2016). W trójwymiarowej
        // tkance ta przestrzeń jest ciągła wokół każdej komórki, więc w rzucie 2D adipocyty nie mogą się stykać ani zamykać kieszeni:
        // między nimi zostaje kanał ≥ 9 µm (mieści świdrowca, a nawet zepchniętego erytrocyta ~8 µm – nic go trwale nie zatka)
        if (c.Biome == 3)
            for (int i = RandI(1, 2); i > 0; i--)
            {
                float r = Rand(18, 26);
                for (int t = 0; t < 30; t++)
                {
                    float x = c.X0 + Rand(10, c.S - 10), y = c.Y0 + Rand(10, c.S - 10);
                    bool Blocks(Ent e) => e is Adipocyte a && Hypot(a.X - x, a.Y - y) < a.Radius + r + 9 || e.Solid && e is not Adipocyte && Hypot(e.X - x, e.Y - y) < e.R + r;
                    // sąsiednie adipocyty: aktywne i zamrożone w sąsiednich chunkach (siatka przestrzenna nie zna jeszcze nowych)
                    if (loc.Any(Blocks) || w.Ents.Any(e => e.Alive && e is Adipocyte && Blocks(e))
                        || w.AllChunks.Any(n => Math.Abs(n.X - c.X) <= 1 && Math.Abs(n.Y - c.Y) <= 1 && n.Parked.Any(e => e.Alive && Blocks(e)))) continue;
                    Put(w, loc, new Adipocyte(w, x, y, r)); break;
                }
            }
        if (c.Biome == 2 && !start)
            for (int i = RandI(1, 2); i > 0; i--) { var (x, y) = Spot(w, c, 14, loc, 12); Put(w, loc, new Kupffer(w, x, y)); }
        // krew: ~4–6 mln erytrocytów, 150–400 tys. płytek i 4–11 tys. leukocytów w 1 µl – erytrocyty wypełniają ~połowę
        // objętości (hematokryt ~40–45%), więc w kadrze leżą gęsto, a krwinka biała trafia się raz na kilkaset czerwonych
        int rbc = c.Biome switch { 0 => 44, 1 => 34, 2 => 36, _ => 10 };
        for (int i = 0; i < rbc; i++) { var (x, y) = Spot(w, c, 4.0f, loc, 3, 16); Put(w, loc, new RBC(w, x, y)); }
        for (int i = rbc / 18 + (Chance(0.5f) ? 1 : 0); i > 0; i--) { var (x, y) = Spot(w, c, 2, loc, 3, 8); Put(w, loc, new Platelet(w, x, y)); }
        if (!start && Chance(rbc / 700f * 1.5f)) { var (x, y) = Spot(w, c, 7, loc, 8); Put(w, loc, Chance(0.65f) ? (Ent)new Leuko(w, x, y) : new Lymphocyte(w, x, y)); }
    }

    public override void Update(World w, float dt, float gdt)
    {
        UpdateBite(w, gdt);
        if (!w.EverySecond(gdt)) return;
        // odporność: ekspozycja na każdy wariant → miano przeciwciał (IgM, potem IgG) narasta po kilku dobach i zostaje
        var cnt = new Dictionary<int, int>(); Pop = 0;
        foreach (var e in w.Ents) if (e is Trypanosome t && t.Alive) { Pop++; cnt[t.Vsg] = (cnt.TryGetValue(t.Vsg, out var n) ? n : 0) + 1; }
        float hBio = w.Bio / 3600f;
        foreach (var (v, n) in cnt) _exp[v] = (_exp.TryGetValue(v, out var x) ? x : 0) + n * hBio;
        foreach (var v in _exp.Keys.ToList())
        {
            if (_exp[v] < E0 || Frozen) continue;
            float t0 = Titer(v), t1 = Math.Min(3, t0 + hBio / 8 * (0.1f + t0) * (1 - t0 / 3));
            _tit[v] = t1;
            if (t0 < 0.5f && t1 >= 0.5f)
            {
                bool mine = w.Player is Trypanosome pt && pt.Vsg == v;
                w.Banner("Przeciwciała!", $"przeciw VSG-{v + 1}" + (mine ? " – to Twój płaszcz" : ""));
                w.Log(mine ? $"Limfocyty B wytworzyły przeciwciała przeciw VSG-{v + 1} – Twojemu płaszczowi. Pływaj naprzód bez przerwy: opływ spycha je do kieszonki wiciowej. Ale gdy miano urośnie, przetrwają tylko pasożyty z nowym VSG." : $"Przeciwciała przeciw VSG-{v + 1} – ten wariant zaraz zniknie z krwi.", mine ? "bad" : "info");
            }
        }
        Sif = Pop / 55f; Room = Pop < 90;
        if (w.Player is Trypanosome p)
        {
            if (w.Stat("t_wave") == 0 && Titer(0) >= 1.2f) { w.AddStat("t_wave", 1); w.Log("Twoja linia przetrwała pierwszą falę – odporność niszczy pasożyty z pierwotnym płaszczem.", "good"); }
            if (w.Stat("t_switch") == 0 && (p.Vsg != 0 || w.CountAll(e => e is Trypanosome t && t.Lineage && t.Vsg != 0) > 0)) w.AddStat("t_switch", 1);
            if (w.Stat("t_stumpy") == 0 && p.IsStumpy) w.AddStat("t_stumpy", 1);
            if (Sif > 0.6f) w.LogOnce("sif", "SIF rośnie – gęsta populacja: część form smukłych przechodzi w krępe.", "info", 60);
            // ukłucie tse-tse – tylko w naczyniach skóry
            if (!Bite && w.ChunkAt(p.X, p.Y).Biome == 1)
            {
                _biteWait -= 1;
                if (_biteWait <= 0) StartBite(w, p);
                else w.LogOnce("skin", "Naczynia skóry: tu mucha tse-tse rozcina skórę i ssie krew. Czekaj na ukłucie (jako forma krępa).", "info", 120);
            }
        }
    }

    // ------------------------------------------------ mucha tse-tse
    public void StartBite(World w, Ent p, float d = -1)
    {
        float a = Rand(0, TAU); if (d < 0) d = Rand(28, 45);
        BiteX = p.X + MathF.Cos(a) * d; BiteY = p.Y + MathF.Sin(a) * d; BiteA = Rand(0, TAU); BiteT = 0; Bite = true;
        w.Banner("Mucha tse-tse!", "kłujka rozcina naczynia skóry");
        w.Log("Mucha tse-tse wbija kłujkę i ssie krew z rany. Formy krępe wyssane teraz zarażą muchę – płyń do czubka kłujki!", "good");
        w.Sfx("snap", BiteX, BiteY, 0.7f);
    }
    void UpdateBite(World w, float gdt)
    {
        if (!Bite) return;
        BiteT += gdt;
        if (BiteT > BiteDur) { Bite = false; _biteWait = ExpRand(30) + 15; w.Log("Mucha odleciała.", "info"); return; }
        if (BiteT < SuckFrom) return;
        foreach (var e in w.Ents)
        {
            if (!e.Alive || e.Fixed || Hypot(e.X - BiteX, e.Y - BiteY) > 6) continue;
            if (e is Trypanosome t && t.Holder == null)
            {
                bool mine = t.Lineage || t.IsPlayer;
                if (t.IsStumpy && mine) { w.AddStat("transmit", 1); w.BornUnseen(1, t.Gen + 1); }
                if (t.IsPlayer)
                {
                    if (t.IsStumpy) { w.Banner("W musze tse-tse!", "Twoje geny ruszają w dalszą drogę"); w.Log("Wyssana forma krępa trafia do jelita środkowego muchy i zmienia się w formę procykliczną – zrzuca płaszcz VSG. Za ~3 tygodnie, jako forma metacykliczna w ślinie, zarazi kolejnego żywiciela.", "good"); }
                    else w.Log("Wyssana forma smukła – w jelicie muchy formy smukłe zwykle giną (klasycznie zarażają tylko krępe).", "bad");
                    t.Cause = ""; w.Sfx("goal", t.X, t.Y, 1);
                }
                t.Alive = false;
            }
            else if (e is RBC or Platelet) e.Alive = false;
        }
    }
    public override bool CustomFlow(World w, float x, float y, out float fx, out float fy)
    {
        fx = fy = 0;
        if (!Bite || BiteT < SuckFrom || BiteT > BiteDur - 1) return true;
        float dx = BiteX - x, dy = BiteY - y, r = MathF.Sqrt(dx * dx + dy * dy);
        if (r < 0.5f || r > 70) return true;
        // ssanie: napływ ku czubkowi kłujki, prędkość rośnie jak 1/r
        float sp = 70 * Math.Min(1, 9 / r) * Smooth((BiteT - SuckFrom) / 2);
        fx = dx / r * sp; fy = dy / r * sp;
        return true;
    }
    public override void DrawOver(World w, Painter p, View v)
    {
        if (!Bite) return;
        p.Identity();
        float into = Smooth(BiteT / SuckFrom), outk = Smooth((BiteDur - BiteT) / 1.5f), k = Math.Min(into, outk);
        float tx = BiteX - MathF.Cos(BiteA) * 60 * (1 - k), ty = BiteY - MathF.Sin(BiteA) * 60 * (1 - k);
        float ca = MathF.Cos(BiteA), sa = MathF.Sin(BiteA), nx = -sa, ny = ca;
        // kałuża krwi wokół rany
        p.Glow(BiteX, BiteY, 40 * k, Rgb(170, 60, 50, 0.22f * k));
        // kłujka (wargi górna i dolna, kanał pokarmowy ~20 µm) – z czubka ku krawędzi kadru
        Span<Vector2> q = stackalloc Vector2[4];
        float len = 500, w0 = 7, w1 = 26;
        q[0] = new Vector2(tx + nx * w0, ty + ny * w0); q[1] = new Vector2(tx - nx * w0, ty - ny * w0);
        q[2] = new Vector2(tx - ca * len - nx * w1, ty - sa * len - ny * w1); q[3] = new Vector2(tx - ca * len + nx * w1, ty - sa * len + ny * w1);
        p.SoftStroke(q, true, 4, Rgb(255, 250, 230, 0.25f));
        p.FillFan(q, tx - ca * len * 0.5f, ty - sa * len * 0.5f, Rgb(70, 52, 30, 0.85f));
        Span<Vector2> c = stackalloc Vector2[4];
        c[0] = new Vector2(tx + nx * w0 * 0.4f, ty + ny * w0 * 0.4f); c[1] = new Vector2(tx - nx * w0 * 0.4f, ty - ny * w0 * 0.4f);
        c[2] = new Vector2(tx - ca * len - nx * w1 * 0.35f, ty - sa * len - ny * w1 * 0.35f); c[3] = new Vector2(tx - ca * len + nx * w1 * 0.35f, ty - sa * len + ny * w1 * 0.35f);
        p.FillFan(c, tx - ca * len * 0.5f, ty - sa * len * 0.5f, Rgb(150, 110, 80, 0.55f));
        // ząbki na czubku wargi dolnej (labellum)
        for (int i = -3; i <= 3; i++) p.Line(tx + nx * i * 2, ty + ny * i * 2, tx + ca * 3 + nx * i * 2.3f, ty + sa * 3 + ny * i * 2.3f, 0.6f, Rgb(60, 40, 20, 0.9f), true);
    }

    public override string[] KeyRows => new[] { "Stan", "Przeciwciała przeciw Twojemu VSG" };
    public override List<(string, string)> Hud(World w)
    {
        var p = w.Player as Trypanosome;
        if (p == null) return new() { ("Stan", "-") };
        string st = p.Holder != null ? "pochłaniany przez komórkę Kupffera!" : p.IsStumpy ? "forma krępa – czeka na muchę" : p.Stumpy > 0 ? "przemiana w formę krępą" : p.Div > 0 ? "podział podłużny" : p.Reverse ? "forma smukła – pływa wstecz" : "forma smukła – pływa naprzód";
        int variants = w.Ents.OfType<Trypanosome>().Select(t => t.Vsg).Distinct().Count();
        float ti = Titer(p.Vsg);
        return new()
        {
            ("Stan", st), ("Płaszcz", $"VSG-{p.Vsg + 1}"),
            ("Przeciwciała przeciw Twojemu VSG", ti < 0.05f ? "brak" : ti < 0.8f ? "IgM – narastają" : ti < 2 ? "IgM + IgG – wysokie" : "IgG – bardzo wysokie"),
            ("Prędkość", $"{MathF.Abs(p.Fwd):0} µm/s" + (p.Reverse ? " (wstecz)" : "")),
            ("Parazytemia (w kadrze i okolicy)", Pop.ToString()), ("Warianty VSG w populacji", variants.ToString()),
            ("SIF (gęstość)", Sif < 0.6f ? $"{Pct(Sif / 0.6f)} progu" : "powyżej progu – formy krępe"),
            ("Ukłucie tse-tse", Bite ? (BiteT < SuckFrom ? "kłujka wchodzi" : "ssanie!") : w.ChunkAt(p.X, p.Y).Biome == 1 ? "możliwe w każdej chwili" : "tylko w naczyniach skóry"),
            ("Przeniesione do muchy", N(w.Stat("transmit"))),
        };
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is not Trypanosome p) return null;
        var l = new List<(string, float, Color)> { ("Przeciwciała na płaszczu VSG", p.Ab, p.Ab > 0.6f ? Hex("#e05a4a") : Hex("#ffd27a")) };
        if (p.Stumpy <= 0) l.Add(("Cykl komórkowy", p.Div > 0 ? 1 : p.Growth, Hex("#b6d7a8")));
        else if (!p.IsStumpy) l.Add(("Przemiana w formę krępą", p.Stumpy, Hex("#b9c9a8")));
        else l.Add(("Pozostały czas życia formy krępej", 1 - Clamp(p.Age / (60 * 3600f), 0, 1), Hex("#b9c9a8")));
        if (Bite && BiteT >= SuckFrom) l.Add(("Mucha ssie krew", 1 - BiteT / BiteDur, Hex("#c77a5a")));
        return l;
    }
    public override int MapKind(World w, Ent e)
    {
        if (e is Kupffer) return 2;
        return e is Trypanosome t && t.Lineage && e != w.Player ? 3 : 0;
    }
    /// <summary>Wskazówka: kłujka podczas ukłucia; forma krępa – najbliższa strefa skóry.</summary>
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0;
        if (w.Player is not Trypanosome p) return false;
        if (Bite) { x = BiteX; y = BiteY; return true; }
        if (p.Stumpy <= 0) return false;
        float bd = float.MaxValue;
        foreach (var c in w.Active) if (c.Biome == 1) { float cx = c.X0 + c.S / 2, cy = c.Y0 + c.S / 2, d = Hypot(cx - p.X, cy - p.Y); if (d < bd && d > c.S * 0.5f) { bd = d; x = cx; y = cy; } }
        return bd < float.MaxValue;
    }
    /// <summary>Po śmierci – potomek z wariantem VSG, na który jest najmniej przeciwciał.</summary>
    public override bool OnPlayerLost(World w, Ent p)
    {
        Trypanosome best = null; float bs = float.MaxValue;
        foreach (var e in w.Ents)
            if (e is Trypanosome t && t != p && t.Lineage && t.CanControl())
            {
                float s = Titer(t.Vsg) * 1000 + t.Ab * 100 + Hypot(t.X - p.X, t.Y - p.Y) * 0.01f;
                if (s < bs) { bs = s; best = t; }
            }
        if (best == null) return false;
        w.Player = null; w.SetPlayer(best);
        w.Log($"Przejmujesz potomka z Twojej linii (VSG-{best.Vsg + 1}).", "good");
        return true;
    }
    public override float Score(World w) => w.Stat("transmit");
    public override int LinePop(World w) => w.CountAll(e => e is Trypanosome t && t.Lineage && t.Alive);
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new Trypanosome(w, 0, 0, 0)); return (e, 40); }
}

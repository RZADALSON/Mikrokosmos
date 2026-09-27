namespace Mikro;

// =====================================================================
//  Dictyostelium – od pojedynczych ameb do owocnika
// =====================================================================
public sealed class ScDicty : Scenario
{
    public enum Ph { Veg, Starve, Agg, Slug, Fruit }
    public Ph Phase;
    public CampField Camp;
    public float PhaseT;
    int _bact0;
    readonly List<Mound> _mounds = new();
    Slug _slug; FruitBody _fruit;
    const float LightY = 170;
    public ScDicty()
    {
        Id = "dicty"; Name = "Ameba społeczna"; Latin = "Dictyostelium discoideum"; TypeName = "Śluzowiec komórkowy (Amoebozoa)"; Size = "≈ 10 µm → owocnik ~1 mm"; Color = Hex("#cfc6a0");
        Desc = "Zjadaj bakterie i dziel się. Gdy pokarm się skończy, głodujące komórki zaczną nadawać fale cAMP – idź ku ich źródłu, dołącz do agregatu, poprowadź ślimaka ku światłu i zbuduj owocnik.";
        Wd = 1600; Ht = 1600; ViewW = 340; TimeScale = 240; Bio = 1; Style = ScopeStyle.Phase; Modes = LightModes; GridCell = 20;
        Mag = "Obiektyw 10× – time-lapse ×120–240"; ScoreLabel = "komórek w agregacie";
        DebrisN = 300; DebrisS0 = 1; DebrisS1 = 4; DebrisD = 0; DebrisCols = new[] { Rgb(70, 66, 45, 0.3f), Rgb(255, 255, 235, 0.3f) };
        Controls = "WASD / przytrzymaj LPM – pełzanie · w fazie agregacji najszybciej idziesz na czole fali cAMP, w stronę jej źródła · ślimak: prowadź ku światłu (górna krawędź)";
        Goals = new[]
        {
            new Goal("Zjedz 40 bakterii", 40, w => w.Stat("bact")),
            new Goal("Podziel się (Twoja linia: 2 komórki)", 1, w => w.Stat("pop")),
            new Goal("Dołącz do agregatu (idź na czole fal cAMP)", 1, w => w.Stat("joined")),
            new Goal("Doprowadź ślimaka do światła", 1, w => w.Stat("light")),
            new Goal("Zbuduj owocnik", 1, w => w.Stat("fruit")),
        };
        LoseText = "Koniec";
        Facts = new[]
        {
            "Dictyostelium discoideum żyje w ściółce leśnej. Pojedyncza ameba (~10 µm) zjada bakterie, wyczuwając ich kwas foliowy, i dzieli się co kilka godzin.",
            "Gdy bakterii zabraknie, głodujące komórki po kilku godzinach stają się zdolne do agregacji (w hodowli: fale po ~6 h, agregaty po ~9 h, zwarty kopczyk po ~12 h).",
            "Sygnałem jest cykliczny AMP. Centra agregacji pulsują co ~6–10 min, a każda komórka, która odbierze impuls, sama wydziela cAMP – fala biegnie ~300 µm/min jako kręgi lub spirale.",
            "Komórka pełznie ku źródłu tylko na czole fali (około minuty), potem stoi – choć za falą stężenie spada w przeciwną stronę (\"problem tyłu fali\"). Wydłużone komórki łączą się w strumienie.",
            "W ciemnym polu fale widać jako jasne i ciemne pasma – ruchome, wydłużone komórki rozpraszają światło inaczej niż spoczywające.",
            "Z kopczyka (do ~100 000 komórek; w grze – setki) wyrasta czubek, a potem powstaje ślimak (pseudoplazmodium), który pełznie ku światłu i ciepłu, zostawiając ślad śluzu.",
            "Przód ślimaka (~20%) to komórki przedtrzonkowe – w owocniku utworzą martwy trzonek z celulozy. Reszta to komórki przedzarodnikowe, które przetrwają jako zarodniki. To altruizm na poziomie komórek.",
            "Los komórki zależy m.in. od jej stanu: komórki z wyższym poziomem ATP częściej trafiają do trzonka (Hiraoka i in. 2020).",
            "Nie wszystkie komórki agregują – część zostaje jako \"samotnicy\", co jest strategią na wypadek powrotu pokarmu (Dubravcic i in. 2014).",
            "Czas: faza wegetatywna i głód – 1 s gry ≈ 4 min; agregacja i ślimak – 1 s ≈ 2 min. W grze widać co n-tą komórkę; sygnał cAMP przekazuje cała populacja.",
        };
    }
    public override void Init(World w)
    {
        Phase = Ph.Veg; PhaseT = 0; _mounds.Clear(); _slug = null; _fruit = null;
        w.TimeScale = 240;
        Camp = new CampField(Wd);
        w.Bact = new BactField(w, 26000);
        // murawa bakterii na agarze – nierówna, gęstsza w plamach
        for (int i = 0; i < 260; i++) w.Bact.Cloud(Rand(40, Wd - 40), Rand(40, Ht - 40), Rand(40, 110), RandI(40, 120));
        _bact0 = w.Bact.N;
        for (int i = 0; i < 170; i++) w.Add(new DictyCell(w, Rand(30, Wd - 30), Rand(30, Ht - 30), Rand(0.3f, 0.7f)));
        var me = w.Add(new DictyCell(w, Wd * 0.5f, Ht * 0.62f, 0.5f));
        w.SetPlayer(me);
        w.CamX = me.X; w.CamY = me.Y;
        w.Log("Jesteś pojedynczą amebą Dictyostelium na murawie bakterii. Zjadaj bakterie (ciemne pałeczki) i dziel się – dopóki starczy pokarmu.", "info");
    }
    public override void Update(World w, float dt, float gdt)
    {
        PhaseT += gdt;
        switch (Phase)
        {
            case Ph.Veg:
                w.Bact.Update(dt, gdt);
                if (w.Bact.N < _bact0 * 0.12f || PhaseT > 200)
                {
                    Phase = Ph.Starve; PhaseT = 0;
                    w.Banner("Głód!", "Bakterie się skończyły. Komórki przygotowują się do agregacji");
                    w.Log("Głodujące komórki włączają geny agregacji: receptory cAMP, cyklazę adenylanową, fosfodiesterazę i adhezynę csA.", "info");
                    if (w.Player is DictyCell pc) w.Stats["atp"] = Math.Min(1, pc.Energy);
                }
                break;
            case Ph.Starve:
                w.Bact.Update(dt, gdt);
                if (PhaseT > 40) StartAgg(w);
                break;
            case Ph.Agg:
                Camp.Update(dt);
                if (w.Player is Mound m && m.JoinT > 35) StartSlug(w, m);
                if (w.Player is DictyCell && PhaseT > 170 && w.Stat("loner") == 0)
                {
                    w.Stats["loner"] = 1;
                    w.GameOver("Zostałeś samotnikiem: część komórek nie agreguje – to strategia na wypadek powrotu bakterii (Dubravcic i in. 2014). Agregaty odeszły bez Ciebie.");
                }
                break;
            case Ph.Slug:
                Camp.Update(dt);
                foreach (var mm in _mounds) if (mm.Alive && mm.Age > 40 && mm.N > 5) { w.Add(new FruitBody(w, mm.X, mm.Y, mm.N)); mm.Alive = false; }
                if (_slug != null && _slug.Alive && (_slug.Y < LightY || PhaseT > 70))
                {
                    if (_slug.Y < LightY) { w.AddStat("light", 1); w.Log("Ślimak dotarł do światła (powierzchnia ściółki) – zaczyna się kulminacja.", "good"); }
                    Culminate(w);
                }
                break;
            case Ph.Fruit:
                if (_fruit != null && _fruit.Prog >= 1 && w.Stat("fruit") == 0)
                {
                    w.AddStat("fruit", 1);
                    float atp = w.Stat("atp"), pStalk = Clamp(0.12f + 0.2f * atp, 0.1f, 0.32f);
                    bool stalk = Chance(pStalk);
                    int n = (int)w.Stat("aggN");
                    string fate = stalk
                        ? $"Twoja komórka trafiła do trzonka (~20% komórek; wyższe ATP → częściej trzonek). Obumarła, by unieść {n * 4 / 5} zarodników nad ściółkę – altruizm komórkowy."
                        : "Twoja komórka stała się zarodnikiem w otoczce z celulozy. Przetrwa suszę i głód, a gdy wróci pokarm, wykiełkuje z niej nowa ameba.";
                    w.Banner("Owocnik gotowy!", stalk ? "Twoja komórka zbudowała trzonek" : "Twoja komórka jest zarodnikiem");
                    w.After(3, () => w.GameOver(fate, true));
                }
                break;
        }
    }
    /// <summary>Testy: przejście od razu do agregacji.</summary>
    public void DebugAgg(World w) { Phase = Ph.Starve; StartAgg(w); }
    void StartAgg(World w)
    {
        Phase = Ph.Agg; PhaseT = 0; w.TimeScale = 120;
        // 3 centra agregacji, z dala od gracza i od siebie
        var pl = w.Player; var cs = new List<Vector2>();
        for (int k = 0; k < 400 && cs.Count < 3; k++)
        {
            var c = new Vector2(Rand(220, Wd - 220), Rand(360, Ht - 220));
            if (pl != null && Hypot(c.X - pl.X, c.Y - pl.Y) < 280) continue;
            bool ok = true; foreach (var o in cs) if ((o - c).Length() < 480) ok = false;
            if (ok) cs.Add(c);
        }
        for (int i = 0; i < cs.Count; i++) { Camp.AddPacemaker(cs[i].X, cs[i].Y, Rand(0, 400)); _mounds.Add(w.Add(new Mound(w, cs[i].X, cs[i].Y))); }
        w.Banner("Fale cAMP!", "Jasne pasma to impulsy cAMP. Idź na czole fali w stronę jej źródła");
        w.Log("Centra agregacji pulsują cAMP co kilka minut, a komórki przekazują impuls dalej. Pełznij, gdy przechodzi przez Ciebie fala – tak robią prawdziwe komórki.", "info");
    }
    void StartSlug(World w, Mound m)
    {
        Phase = Ph.Slug; PhaseT = 0;
        w.Stats["aggN"] = m.N;
        _slug = w.Add(new Slug(w, m.X, m.Y, -PI / 2, m.N)); m.Alive = false;
        w.Player = null; w.SetPlayer(_slug);
        w.Banner("Ślimak!", $"{m.N} komórek (w naturze do 100 000) pełznie jak jeden organizm. Prowadź go ku światłu (w górę)");
    }
    void Culminate(World w)
    {
        Phase = Ph.Fruit; PhaseT = 0;
        _fruit = w.Add(new FruitBody(w, _slug.X, _slug.Y, _slug.N));
        _slug.Alive = false; w.Player = null; w.Focus = _fruit;
        w.Log("Kulminacja: komórki przedtrzonkowe wciskają się w głąb i budują trzonek z celulozy, unosząc masę zarodników.", "info");
    }
    public override bool OnPlayerLost(World w, Ent p) => Phase == Ph.Fruit || w.Focus != null;
    public override void DrawUnder(World w, Painter p, View v)
    {
        w.Bact?.Draw(p, v);
        if (Phase >= Ph.Agg) Camp.Draw(p, v);
        if (Phase >= Ph.Slug)
        {
            // strefa światła: powierzchnia ściółki
            p.Identity();
            p.Rect(0, 0, Wd, LightY, Rgb(255, 250, 220, 0.12f));
            p.Line(0, LightY, Wd, LightY, Math.Max(v.Px, 1), Rgb(255, 245, 200, 0.4f));
        }
    }
    public override void DrawMinimap(World w, Painter p)
    {
        w.Bact?.DrawMap(p, Rgb(150, 130, 70, 0.7f));
        if (Phase >= Ph.Agg) Camp.DrawMap(p);
    }
    public override bool FoodPoint(World w, out float x, out float y)
    {
        x = y = 0; var pl = w.Player; if (pl == null) return false;
        if (Phase == Ph.Veg) return w.Bact.Densest(pl.X, pl.Y, ViewW * 2, out x, out y);
        if (Phase == Ph.Agg)
        {
            Mound best = null; float bd = 1e9f;
            foreach (var m in _mounds) { float d = Hypot(m.X - pl.X, m.Y - pl.Y); if (m.Alive && d < bd) { bd = d; best = m; } }
            if (best != null) { x = best.X; y = best.Y; return true; }
        }
        if (Phase == Ph.Slug) { x = pl.X; y = LightY * 0.5f; return true; }
        return false;
    }
    public override List<(string, string)> Hud(World w)
    {
        string ph = Phase switch { Ph.Veg => "wegetatywna (jedzenie, podziały)", Ph.Starve => "głód – przygotowanie do agregacji", Ph.Agg => "agregacja (fale cAMP)", Ph.Slug => "ślimak – ku światłu", _ => "owocnik" };
        var l = new List<(string, string)> { ("Faza", ph) };
        if (Phase <= Ph.Starve && w.Bact != null) l.Add(("Bakterie", $"{w.Bact.N} ({Pct((float)w.Bact.N / Math.Max(1, _bact0))})"));
        l.Add(("Zjedzone bakterie", N(w.Stat("bact"))));
        l.Add(("Twoja linia", N(1 + w.Stat("pop"))));
        l.Add(("Ameby", w.Count(e => e is DictyCell).ToString()));
        if (Phase >= Ph.Agg)
        {
            l.Add(("Okres fal cAMP", $"{Camp.Period / 60:0.0} min"));
            int inM = 0; foreach (var m in _mounds) if (m.Alive) inM += m.N;
            l.Add(("W agregatach", inM.ToString()));
        }
        if (_slug != null && _slug.Alive) { l.Add(("Komórki ślimaka", _slug.N.ToString())); l.Add(("Do światła", $"{Math.Max(0, _slug.Y - LightY):0} µm")); }
        return l;
    }
    public override List<(string label, float v, Color col)> Bars(World w)
    {
        if (w.Player is DictyCell c) return new() { ("Zapas energii (ATP)", Math.Min(1, c.Energy), Hex("#cfc6a0")) };
        if (_fruit != null) return new() { ("Kulminacja", _fruit.Prog, Hex("#f0e0a0")) };
        return null;
    }
    public override int MapKind(World w, Ent e) => e is Mound ? 1 : e is DictyCell c && c.Lineage && e != w.Player ? 3 : 0;
    public override float Score(World w) => w.Stat("aggN");
    public override (Ent e, float size) Preview(World w) { var e = w.Add(new DictyCell(w, 0, 0)); return (e, 22); }
}

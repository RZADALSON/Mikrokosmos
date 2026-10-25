namespace Mikro;

/// <summary>Mutacje kierujące (driver) w klonie nowotworowym – każda zmienia fenotyp komórki.</summary>
[Flags]
public enum Muts { None = 0, Kras = 1, Tp53 = 2, Cdh1 = 4, B2m = 8, Pdl1 = 16, Tf = 32 }

public static class MutInfo
{
    public static readonly (Muts m, string gene, string effect)[] All =
    {
        (Muts.Kras, "KRAS G12D", "stały sygnał wzrostu – szybszy cykl komórkowy"),
        (Muts.Tp53, "TP53 (utrata)", "brak apoptozy w niedotlenieniu i niestabilność genomu (więcej mutacji)"),
        (Muts.Cdh1, "CDH1 (utrata E-kadheryny)", "trwałe EMT – komórka nie trzyma się guza"),
        (Muts.B2m, "B2M (utrata MHC-I)", "limfocyty T Cię nie widzą, ale komórki NK – tak („brak swojego”)"),
        (Muts.Pdl1, "amplifikacja CD274 (PD-L1)", "limfocyt T po kontakcie zostaje wyczerpany"),
        (Muts.Tf, "F3 (czynnik tkankowy)", "we krwi płytki szybko okrywają komórkę"),
    };
    public static string List(Muts m)
    {
        if (m == Muts.None) return "brak (tylko mutacje założycielskie)";
        var l = new List<string>();
        foreach (var (f, g, _) in All) if ((m & f) != 0) l.Add(g);
        return string.Join(" · ", l);
    }
}

/// <summary>
/// Komórka raka (carcinoma, ~15–20 µm): duże jądro z wyraźnymi jąderkami, wysoki stosunek jądro/cytoplazma.
/// Fenotyp jest plastyczny: nabłonkowy (E-kadheryna, trzyma się guza) ↔ po EMT inwazyjny – mezenchymalny
/// (wrzecionowaty, MT1-MMP trawi kolagen, ≈ 0,1–1 µm/min) albo ameboidalny (okrągły, pęcherzyki błony, szybszy,
/// ale utyka w ciasnej sieci – Friedl i Wolf 2003; Wolf i in. 2003, 2013). Niedotlenienie (HIF-1α → TWIST) i makrofagi
/// (pętla EGF/CSF-1) pobudzają EMT (Yang i in. 2008; Wyckoff i in. 2004). Z guza: intrawazacja, krążenie z osłoną
/// płytek, przyleganie, ekstrawazacja i – po powrocie do fenotypu nabłonkowego (MET) – kolonia przerzutowa.
/// </summary>
public sealed class TumorCell : Ent
{
    public enum Site { Tissue, Blood, Wall }
    /// <summary>Sposób śmierci: martwica (anoksja) zostawia „cień” komórki, apoptoza (p53, limfocyty) – ciałka apoptotyczne.</summary>
    public enum Death { Other, Necrosis, Apoptosis }
    public override bool Swims => false;
    public override bool Rotates => false;
    public const int NN = 44;
    /// <summary>Prędkości pełzania (µm/s czasu biologicznego): nabłonkowa, mezenchymalna, ameboidalna.</summary>
    public const float VEpi = 0.1f / 60, VMes = 0.5f / 60, VAmo = 2.5f / 60;
    public float R0 = 8.5f;
    /// <summary>
    /// Pleomorfizm (różnorodność komórek raka – to po nim patolog rozpoznaje nowotwór złośliwy): względna wielkość jądra,
    /// nieregularność jego zarysu, gęstość chromatyny (hiperchromazja), liczba jąderek, komórki dwujądrowe.
    /// </summary>
    public float NucK = 1, NucIrr = 0.08f, Chrom = 1; public int Nucleoli = 2; public bool Bi;
    public Death DeathKind;
    public Muts Muts;
    /// <summary>EMT 0..1, cykl komórkowy 0..1, mitoza 0..1, pO₂ (mmHg), HIF-1α 0..1, postęp intrawazacji/ekstrawazacji, osłona płytek, przyleganie.</summary>
    public float Emt, Growth, Mit, O2 = 20, Hif, Intra, Extra, Shield, Adh;
    public bool Amoeboid;
    public Site Where = Site.Tissue;
    /// <summary>Pozycja w przekroju naczynia (−1..1) podczas krążenia.</summary>
    public float Lane;
    /// <summary>Włókno, które komórka właśnie trawi (ustawiane przy kontakcie).</summary>
    public Collagen Cutting;
    public string Cause = "";
    public readonly float[] Rr = new float[NN];
    readonly float _seed; float _wA, _wT, _mitAx, _hit, _tamT; bool _tam, _o2Set;
    readonly Vector3[] _gran;
    public override bool CanControl() => Alive;
    public override float Radius => R0;

    public TumorCell(World w, float x, float y, float a) : base(w, x, y, a)
    {
        Kind = Kind.Tumor; Layer = 2; Soft = 0.6f; Name = "komórka nowotworowa"; Latin = "carcinoma";
        // anizocytoza i anizokarioza: komórki i jądra wyraźnie różnej wielkości; rzadko komórki olbrzymie, wielojądrowe
        R0 = Clamp(8.6f + RandN() * 1.15f, 6.3f, 12f); if (Chance(0.012f)) R0 = Rand(12f, 13.5f);
        NucK = Rand(0.85f, 1.25f); NucIrr = Rand(0.04f, 0.2f); Chrom = Rand(0.85f, 1.25f); Nucleoli = RandI(1, 3); Bi = R0 > 11.8f || Chance(0.035f);
        R = R0 * 1.5f; Mass = 16; FlowK = 0;
        Mem = new Membrane(NN) { MaxDef = 3.5f, Stiff = 8, Damping = 0.6f }; MemK = 4; MemS = 0.45f;
        _seed = Rand(0, 100); _wA = Rand(0, TAU); Growth = Rand(0, 0.8f);
        _gran = new Vector3[20];
        for (int i = 0; i < _gran.Length; i++) _gran[i] = new Vector3(Rand(0, TAU), Rand(0.55f, 0.92f), Rand(0.12f, 0.3f));
        Shape();
        Mem.Follow(x, y, a, 1, 1);
    }
    public bool Has(Muts m) => (Muts & m) != 0;
    public override bool Collides(Ent o) => Where == Site.Tissue ? !(o is Killer { InBlood: true }) : o is Killer { InBlood: true };
    /// <summary>Spłaszczanie błon o sąsiednie komórki guza co drugą klatkę (wgniecenie wygasa powoli, więc różnicy nie widać).</summary>
    public override bool SkipMemPush(Ent o, int frame) => o is TumorCell && !IsPlayer && ((frame + Id) & 1) == 1;
    /// <summary>Kolizje: komórka nabłonkowa w gnieździe jest prawie okrągła – wystarczy koło (szybciej przy setkach komórek);
    /// komórka inwazyjna (wydłużona, z inwadopodium) i gracz – kształt złożony z części.</summary>
    public override void UpdatePrim()
    {
        // kształt złożony tylko, gdy komórka jest wyraźnie wydłużona (EMT) albo w mitozie; gracz – dokładniej (6 wypustek)
        if (IsPlayer || Emt > 0.45f || Mit > 0 || Where != Site.Tissue) { RadialParts(Rr, NN, IsPlayer ? 6 : 4); return; }
        float mn = float.MaxValue, sum = 0; for (int i = 0; i < NN; i++) { mn = Math.Min(mn, Rr[i]); sum += Rr[i]; }
        SetCircle(Lerp(mn, sum / NN, 0.6f));
    }

    /// <summary>Obrażenia od limfocytu (perforyna, granzymy) – kolejne „trafienia” się sumują (Weigelin i in. 2021).</summary>
    public void Hurt(float dmg, string cause)
    {
        _hit = 1.2f; Cause = cause;
        // granzymy uruchamiają kaspazy – komórka ginie przez apoptozę, a nie przez rozpad błony
        DeathKind = Where == Site.Tissue ? Death.Apoptosis : Death.Other;
        Damage(dmg);
    }
    public override void Kill()
    {
        if (!Alive) return;
        if (IsPlayer && !W.Preview) W.Log(Cause.Length > 0 ? Cause : "Komórka nowotworowa zginęła.", "bad");
        if (!W.Preview && Where == Site.Tissue && DeathKind != Death.Other)
        {
            // martwica: komórka puchnie i pęka (onkoza), zostaje blady „cień” z rozpadłym jądrem;
            // apoptoza: komórka obkurcza się i rozpada na ciałka apoptotyczne – bez pęknięcia błony i bez zapalenia
            if (DeathKind == Death.Necrosis) base.Kill(); else ReleaseHeld();
            W.Add(new TumorDebris(W, X, Y, R0, DeathKind == Death.Necrosis, _seed));
            Alive = false; return;
        }
        base.Kill();
    }

    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        _hit = Math.Max(0, _hit - gdt);
        var sc = W.Sc as ScCancer;
        if (Holder != null) { Vx = Vy = 0; return; }
        if (Where != Site.Tissue && sc != null) { StepBlood(sc, dt, gdt); _shapeDue = true; return; }
        // ------------------------------------------------ mikrośrodowisko: tlen z najbliższego drożnego naczynia, zużycie przez sąsiadów
        // (pole tlenu zmienia się powoli – liczone co kilka klatek, gracz co klatkę)
        bool near = IsPlayer || W.Preview || W.OnScreen(this, 1.2f);
        if (IsPlayer || !_o2Set || ((W.Frame + Id) % 6) == 0)
        {
            int crowd = 0; foreach (var o in Nb) if (o is TumorCell) crowd++;
            O2 = (sc?.O2(W, X, Y) ?? 30) * (1 - Math.Min(0.45f, crowd * 0.07f)); _o2Set = true;
        }
        Hif = Lerp(Hif, O2 < 10 ? 1 : 0, Damp(1 / 1800f, bdt));
        if (((W.Frame + Id) % 12) == 0) _tam = W.Near(X, Y, R0 + 16, e => e is Macrophage) != null;
        if (_tam) _tamT += gdt;
        // EMT: HIF-1α indukuje TWIST (Yang i in. 2008), makrofagi – EGF i TGF-β (Wyckoff i in. 2004); utrata CDH1 – trwale
        bool player = IsPlayer && !W.Preview;
        var I = W.Input;
        bool moving = player && I.Mag > 0.05f;
        float up = Hif / (12 * 3600f) + (_tam ? 1 / (5 * 3600f) : 0);
        if (Has(Muts.Cdh1)) Emt = Math.Max(Emt, 0.85f);
        if (up > 0.02f / 3600) Emt = Math.Min(1, Emt + up * bdt);
        else if (!moving) Emt = Math.Max(Has(Muts.Cdh1) ? 0.85f : 0, Emt - bdt / (36 * 3600f)); // MET: bez sygnałów EMT komórka wraca do fenotypu nabłonkowego
        // ------------------------------------------------ przeżycie: martwica w anoksji, apoptoza zależna od p53 w ciężkim niedotlenieniu
        // Umiarkowane niedotlenienie (kilka mmHg – typowe dla raków, Vaupel 2004) komórka przeżywa: działa HIF-1α, podziały zwalniają.
        // p53 stabilizuje się dopiero w ciężkiej hipoksji (0,02% O₂, ale nie przy 2%; Hammond i in. 2002, Hammond i Giaccia 2005;
        // Graeber i in. 1996) – próg ≈ 0,2% O₂ ≈ 1,5 mmHg; martwica zaczyna się w anoksji na granicy dyfuzji tlenu (Thomlinson i Gray 1955).
        if (!W.Preview)
        {
            if (O2 < 0.5f) { Cause = "Martwica: ponad ~150–180 µm od drożnego naczynia nie ma już tlenu (Thomlinson i Gray 1955)."; DeathKind = Death.Necrosis; Damage(bdt / (10 * 3600f)); }
            else if (O2 < 1.5f && !Has(Muts.Tp53)) { Cause = "Apoptoza w ciężkim niedotlenieniu (pO₂ < 1,5 mmHg, ok. 0,2% O₂): białko p53 uruchomiło program śmierci – bez mutacji TP53 komórka tego nie przeżywa."; DeathKind = Death.Apoptosis; Damage(bdt / (20 * 3600f)); }
            else if (O2 > 8 && _hit <= 0) Hp = Math.Min(1, Hp + bdt / (10 * 3600f));
            if (!Alive) return;
        }
        // ------------------------------------------------ cykl komórkowy
        if (Mit > 0)
        {
            Vx = Vy = 0; Mit += bdt / 3600f;
            if (Mit >= 1) Divide();
            if (near || ((W.Frame + Id) & 3) == 0) _shapeDue = true;
            return;
        }
        if (!W.Preview && (player || sc == null || sc.RoomToGrow(W)))
        {
            float rate = 1f / (24 * 3600) * O2 / (O2 + 4) * (Has(Muts.Kras) ? 1.4f : 1) * (1 - 0.75f * Emt);
            Growth += rate * bdt;
            if (Growth >= 1) { Mit = 0.001f; _mitAx = Rand(0, TAU); if (player) W.Log("Mitoza: komórka zaokrągla się, chromosomy ustawiają się w płytce metafazowej.", "info"); }
        }
        // ------------------------------------------------ ruch
        bool on = false; float tgtA = A, mag = 0;
        if (player)
        {
            on = I.Mag > 0.05f; tgtA = I.Ang; mag = I.Mag;
            if (I.Action)
            {
                if (Emt < 0.3f) W.LogOnce("epi", "Komórka jest jeszcze nabłonkowa – E-kadheryna trzyma ją w guzie. Potrzebne EMT: niedotlenienie (głąb guza) albo kontakt z makrofagiem.", "info", 6);
                else
                {
                    Amoeboid = !Amoeboid;
                    W.Log(Amoeboid ? "Tryb ameboidalny: komórka zaokrągla się i pełznie pęcherzykami błony – szybciej, ale jądro utyka w gęstym kolagenie." : "Tryb mezenchymalny: wrzecionowata komórka z inwadopodiami – wolno, ale MT1-MMP trawi włókna kolagenu i otwiera tunel.", "info");
                }
            }
        }
        else if (Emt > 0.5f)
        {
            _wT -= dt; if (_wT <= 0) { _wA += RandN() * 1.2f; _wT = ExpRand(1800); }
            on = true; tgtA = _wA; mag = 0.6f;
        }
        if (on) A = Turn(A, tgtA, (Amoeboid ? 1.2f : 0.6f) * gdt);
        float mode = Amoeboid ? VAmo : VMes;
        float vmax = Lerp(VEpi, mode, Smooth(Clamp((Emt - 0.2f) / 0.6f, 0, 1)));
        float mesh = 1 + Math.Min(Mesh, 6); Mesh = 0;
        float v = vmax * mag * (0.4f + 0.6f * Hp) / mesh;
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
        if (Cutting != null)
        {
            if (Cutting.Alive && mag > 0.05f && !Amoeboid) { Cutting.Degrade(bdt / (1.5f * 3600)); if (player && !Cutting.Alive) W.AddStat("t_cut", 1); }
            Cutting = null;
        }
        // ------------------------------------------------ intrawazacja: napieranie na ścianę żyłki; obok makrofaga (TMEM) – kilka razy szybciej
        if (player && sc != null && Emt > 0.5f)
        {
            float d = sc.WallDist(W, X, Y, out float nx, out float ny);
            bool push = on && MathF.Cos(tgtA) * nx + MathF.Sin(tgtA) * ny > 0.4f;
            if (d < R0 + 2 && push)
            {
                bool tmem = W.Near(X, Y, 40, e => e is Macrophage) != null;
                Intra += bdt / ((tmem ? 1.2f : 5f) * 3600);
                if (tmem) W.LogOnce("tmem", "TMEM: makrofag przy naczyniu (VEGF-A) rozszczelnia śródbłonek – brama dla komórek nowotworowych (Harney i in. 2015).", "good", 30);
                if (Intra >= 1) { Intra = 0; sc.EnterBlood(W, this); }
            }
            else Intra = Math.Max(0, Intra - bdt / (3 * 3600));
        }
        else if (player && sc != null && Emt <= 0.5f && sc.WallDist(W, X, Y, out _, out _) < R0 + 2 && on)
            W.LogOnce("noinv", "Komórka nabłonkowa nie przejdzie przez ścianę naczynia – potrzebne EMT (co najmniej 50%).", "info", 10);
        // kształt poza kadrem przeliczany rzadziej (wydajność – guz liczy setki komórek)
        if (IsPlayer || near && ((W.Frame + Id) & 1) == 0 || ((W.Frame + Id) & 7) == 0) _shapeDue = true;
    }
    /// <summary>Kształt przeliczany w równoległej fazie membran (zależy tylko od stanu tej komórki).</summary>
    bool _shapeDue;
    public override void PreMem() { if (_shapeDue) { _shapeDue = false; Shape(); } }

    void StepBlood(ScCancer sc, float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        if (Where == Site.Blood)
        {
            // marginacja: erytrocyty spychają większe komórki ku ścianie
            float side = Lane >= 0 ? 1 : -1;
            Lane += (side * 0.85f - Lane) * Damp(0.6f, gdt) + RandN() * 0.03f * MathF.Sqrt(gdt);
            Lane = Clamp(Lane, -1, 1);
            float prof = 1 - 0.55f * Lane * Lane;
            Vx = W.BloodV * prof / Math.Max(1, W.TimeScale); Vy = 0;
            // płytki: czynnik tkankowy → trombina → agregaty płytek okrywają komórkę (Palumbo i in. 2005)
            Shield += gdt * (Has(Muts.Tf) ? 0.2f : 0.07f) * (1 - Shield);
            if (!W.Preview)
            {
                Cause = "Naprężenia ścinające w przepływie rozerwały błonę – większość krążących komórek ginie w ciągu minut (Chambers i in. 2002).";
                Damage(gdt * 0.03f * (1 - 0.8f * Shield));
                if (!Alive) return;
            }
            bool hold = IsPlayer && W.Input.ActionHeld;
            if (hold && MathF.Abs(Lane) > 0.6f)
            {
                // łatwiej w rozkurczu, gdy krew zwalnia; płytki pomagają związać się ze śródbłonkiem
                float slow = Clamp(300 / Math.Max(60, W.BloodV), 0.4f, 2);
                Adh += gdt * 0.3f * (0.3f + 0.7f * Shield) * slow;
                if (Adh >= 1) { Adh = 0; sc.Arrest(W, this); }
            }
            else Adh = Math.Max(0, Adh - gdt * 0.4f);
        }
        else
        {
            // przyleganie do śródbłonka i ekstrawazacja (godziny)
            Vx = Vy = 0;
            Shield = Math.Max(0, Shield - bdt / (24 * 3600f));
            Extra += bdt / (8 * 3600f);
            if (Extra >= 1) { Extra = 0; sc.Extravasate(W, this); }
        }
    }

    void Divide()
    {
        Mit = 0; Growth = Rand(0, 0.12f);
        float ox = MathF.Cos(_mitAx) * R0 * 0.5f, oy = MathF.Sin(_mitAx) * R0 * 0.5f;
        // córki nie są identyczne – niestabilność genomu i nierówny podział dają różną wielkość komórek i jąder
        var d = W.Add(new TumorCell(W, X + ox, Y + oy, A + Rand(-1, 1)) { Muts = Muts, Emt = Emt * 0.9f, Amoeboid = Amoeboid, Growth = Rand(0, 0.12f), Hif = Hif, R0 = Clamp(R0 + RandN() * 0.6f, 6.3f, 12.5f) });
        d.R = d.R0 * 1.5f;
        X -= ox; Y -= oy;
        W.Born(this, d);
        if (IsPlayer) W.AddStat("t_div", 1);
        Mutate(this); Mutate(d);
        d.Shape(); d.Mem.Follow(d.X, d.Y, d.A, 1, 1);
    }
    /// <summary>Nowa mutacja kierująca (w grze częściej niż w naturze – zob. okno wiedzy); utrata TP53 zwiększa niestabilność genomu.</summary>
    void Mutate(TumorCell c)
    {
        if (!Chance(c.Has(Muts.Tp53) ? 0.3f : 0.12f)) return;
        var free = new List<(Muts m, string g, string e)>();
        foreach (var x in MutInfo.All) if (!c.Has(x.m)) free.Add(x);
        if (free.Count == 0) return;
        var (m, g, e) = free[RandI(0, free.Count - 1)];
        c.Muts |= m;
        if (c.IsPlayer) { W.Log($"Mutacja w Twojej komórce: {g} – {e}.", "good"); W.Sfx("goal", X, Y, 0.5f); }
        else if (c.Lineage) W.Log($"Mutacja w komórce potomnej: {g}.", "info");
    }

    public void Shape()
    {
        float t = W.T, R = R0;
        float el = Where == Site.Tissue ? Smooth(Clamp((Emt - 0.3f) / 0.5f, 0, 1)) : 0;
        float mes = Amoeboid ? 0 : el, amo = Amoeboid ? el : 0;
        float A2 = R * (1 + 0.42f * mes), B2 = R * (1 - 0.3f * mes), area = 0;
        float rnd = Mit > 0 ? Smooth(Mit * 2.5f) : Where != Site.Tissue ? 1 : 0, fur = Mit > 0 ? Smooth((Mit - 0.55f) / 0.45f) : 0;
        float mitLocal = _mitAx - A;
        for (int i = 0; i < NN; i++)
        {
            float th = i * TAU / NN, c = MathF.Cos(th), s = MathF.Sin(th);
            float r = A2 * B2 / MathF.Sqrt(Sq(B2 * c) + Sq(A2 * s));
            // nabłonkowa: wielokątna („bruk”), mezenchymalna: przednie inwadopodium i ogon, ameboidalna: pęcherzyki błony (blebs)
            r *= 1 + 0.06f * (1 - el) * MathF.Cos(6 * th + _seed);
            r += mes * R * 0.45f * Gauss(WrapA(th), 0.16f) + mes * R * 0.2f * Gauss(WrapA(th - PI), 0.2f);
            r += amo * R * (0.12f * MathF.Pow(Math.Max(0, MathF.Sin(5 * th + t * 1.4f + _seed)), 4) + 0.18f * Gauss(WrapA(th), 0.5f));
            r += R * 0.04f * Noise1(th * 3 + t * 0.3f + _seed);
            if (rnd > 0)
            {
                float cc = MathF.Cos(th - mitLocal), round = R * 0.9f * (1 + 0.02f * Noise1(th * 5 + t * 2));
                round *= 1 + 0.42f * fur * (cc * cc - 0.5f) * 2;
                if (Where != Site.Tissue && Mit <= 0) round = R * 0.88f * (1 + 0.05f * MathF.Cos(2 * th)) * (1 + 0.03f * Noise1(th * 4 + t + _seed));
                r = Lerp(r, round, rnd);
            }
            Rr[i] = r; area += 0.5f * r * r * (TAU / NN);
        }
        float k = MathF.Sqrt(PI * R * R / area);
        for (int i = 0; i < NN; i++) Rr[i] *= k;
        Mem.Radial(Rr);
    }

    static readonly float[] UnitC = new float[28], UnitS = new float[28];
    static TumorCell() { for (int i = 0; i < 28; i++) { UnitC[i] = MathF.Cos(i * TAU / 28); UnitS[i] = MathF.Sin(i * TAU / 28); } }
    static readonly Color Halo = Rgb(255, 255, 240, 0.26f), Edge = Rgb(40, 38, 42, 0.75f), NucF = Rgb(78, 70, 98, 0.46f), NucE = Rgb(42, 36, 58, 0.6f),
        Nucleolus = Rgb(36, 30, 50, 0.75f), Chrom_ = Rgb(50, 44, 70, 0.32f), PltC = Rgb(150, 150, 165, 0.75f), PltG = Rgb(95, 80, 125, 0.7f);

    /// <summary>Jądro komórki raka: duże, o nieregularnym zarysie, z grudkami chromatyny i 1–3 wyraźnymi jąderkami.</summary>
    // zarys jądra (stały dla danej komórki): wstępnie policzone kierunki pomnożone przez nieregularność – bez szumu co klatkę
    float[] _nw;
    void Nucleus(Painter p, float cx, float cy, float nrx, float nry, float so, bool detail)
    {
        int o = so > 0 ? 28 : 0, step = IsPlayer ? 1 : 2;
        if (_nw == null)
        {
            _nw = new float[56];
            for (int i = 0; i < 56; i++) { float th = (i % 28) * TAU / 28, sx = i >= 28 ? 1.7f : 0; _nw[i] = 1 + NucIrr * Noise1(th * 2.3f + _seed + sx) + 0.5f * NucIrr * MathF.Sin(3 * th + _seed + sx); }
        }
        Span<Vector2> nucB = stackalloc Vector2[28]; int nn = 0;
        for (int i = 0; i < 28; i += step) { float wob = _nw[o + i]; nucB[nn++] = new Vector2(cx + UnitC[i] * nrx * wob, cy + UnitS[i] * nry * wob); }
        var nuc = nucB[..nn];
        p.FillFan(nuc, cx, cy, Mul(NucF, Chrom));
        p.Thin(nuc, true, 0.16f, Mul(NucE, Math.Min(1.2f, Chrom)));
        if (detail) for (int i = 0; i < (IsPlayer ? 12 : 4); i++) p.Dot(cx + MathF.Cos(i * 2.4f + _seed + so) * nrx * 0.75f * ((i * 37 % 10) / 10f), cy + MathF.Sin(i * 2.4f + _seed + so) * nry * 0.75f * ((i * 53 % 10) / 10f), 0.16f, Mul(Chrom_, Chrom));
        for (int k = 0; k < Nucleoli; k++)
        {
            float a = _seed * 3 + so + k * 2.2f, rr = k == 0 ? 0.3f : 0.45f;
            p.Dot(cx + MathF.Cos(a) * nrx * rr, cy + MathF.Sin(a) * nry * rr, (k == 0 ? 0.85f : 0.6f) * Math.Min(1.3f, NucK), Nucleolus);
        }
    }

    public override void Draw(Painter p, View v)
    {
        // szczegóły (ziarnistości, grudki chromatyny) tylko gdy komórka jest na ekranie duża; przy oddaleniu – sam zarys i jądro
        bool detail = R0 / Math.Max(v.Px, 1e-6f) > 40;
        // obrys: łamana przez środki 44 odcinków błony odbiega od gładkiej krzywej o < 0,8 px, dopóki promień komórki na ekranie
        // < 300 px – gładszy obrys tylko dla gracza i przy dużym zbliżeniu (np. na ekranie 4K)
        int n = Mem.Smooth(p.Shape, IsPlayer || R0 / Math.Max(v.Px, 1e-6f) > 300 ? 2 : 1);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        // halo kontrastu fazowego powstaje na skoku współczynnika załamania – silne na wolnym brzegu gniazda, między ciasno
        // upakowanymi komórkami słabe (i pomijane, co odciąża rysowanie setek komórek)
        int nbT = 0; if (!IsPlayer) foreach (var o in Nb) if (o is TumorCell) nbT++;
        bool haloOn = IsPlayer || nbT < 4 || _hit > 0 || Where != Site.Tissue;
        Span<float> ts = stackalloc float[] { 0f, 0.6f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(108, 100, 104, 0.6f), Rgb(122, 114, 116, 0.5f), Rgb(180, 174, 170, 0.24f) };
        // komórka wewnątrz gniazda dzieli granice z sąsiadami (rysują ją obie) – cieńszy obrys bez wygładzania wystarczy
        bool inner = !IsPlayer && nbT >= 4 && Where == Site.Tissue;
        var haloC = _hit > 0 ? Rgb(255, 250, 220, 0.26f + 0.25f * _hit) : Halo;
        int csub = IsPlayer || R0 / Math.Max(v.Px, 1e-6f) > 300 ? 2 : 1;
        if (IsPlayer || p.Tex != null) p.Body(Mem, sh, csub, haloOn ? 2.6f : 0, haloC, X, Y, ts, cs);
        else { if (haloOn) p.SoftStroke(sh, true, 2.6f, haloC); p.FillRadial(sh, X, Y, ts[1..], cs[1..], !inner); }
        if (inner) p.Thin(sh, true, 0.16f, Edge); else p.Stroke(sh, true, 0.14f, Edge);
        BodyLocal(p);
        float mk = Where == Site.Tissue && !Amoeboid ? Smooth(Clamp((Emt - 0.3f) / 0.5f, 0, 1)) : 0;
        // ziarnistości i pęcherzyki w cytoplazmie
        int ng = IsPlayer ? _gran.Length : 6;
        if (detail) for (int i = 0; i < ng; i++)
        {
            float th = _gran[i].X, rr = RadialUtil.Interp(Rr, NN, th) * _gran[i].Y;
            p.Dot(MathF.Cos(th) * rr, MathF.Sin(th) * rr, _gran[i].Z, i % 3 == 0 ? Rgb(245, 245, 230, 0.6f) : Rgb(40, 40, 45, 0.45f));
        }
        if (Mit < 0.22f)
        {
            // jądro: duże, nieregularne, hiperchromatyczne; wielkość i kształt różne w różnych komórkach (pleomorfizm)
            float nrx = R0 * (0.52f + 0.12f * mk) * NucK, nry = R0 * (0.44f - 0.1f * mk) * NucK, cx = -R0 * 0.12f;
            if (Bi)
            {
                // komórka dwujądrowa (albo olbrzymia, wielojądrowa) – częsta w nowotworach złośliwych
                Nucleus(p, cx - nrx * 0.45f, -nry * 0.25f, nrx * 0.62f, nry * 0.7f, 0, detail);
                Nucleus(p, cx + nrx * 0.5f, nry * 0.3f, nrx * 0.6f, nry * 0.68f, 1.7f, detail);
            }
            else Nucleus(p, cx, 0, nrx, nry, 0, detail);
        }
        else
        {
            // mitoza: płytka metafazowa → rozchodzące się chromatydy → dwa jądra potomne
            float ml = _mitAx - A, ca = MathF.Cos(ml), sa = MathF.Sin(ml), sep = Smooth((Mit - 0.45f) / 0.4f) * R0 * 0.55f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float ox = ca * sep * sd, oy = sa * sep * sd;
                if (Mit > 0.85f) p.FillEllipse(ox, oy, R0 * 0.32f, R0 * 0.28f, ml, NucF);
                else p.Line(ox - sa * R0 * 0.45f, oy + ca * R0 * 0.45f, ox + sa * R0 * 0.45f, oy - ca * R0 * 0.45f, 1.1f, Rgb(40, 34, 60, 0.7f), true);
                if (sep < 0.3f) break;
            }
        }
        // osłona płytek krwi we krwiobiegu
        if (Where != Site.Tissue && Shield > 0.02f)
        {
            int np = (int)(Shield * 16);
            for (int i = 0; i < np; i++)
            {
                float th = i * TAU / 16 + _seed, rr = RadialUtil.Interp(Rr, NN, th) + 0.9f;
                p.FillCircle(MathF.Cos(th) * rr, MathF.Sin(th) * rr, 1.1f, PltC);
                p.Dot(MathF.Cos(th) * rr, MathF.Sin(th) * rr, 0.4f, PltG);
            }
        }
    }
}

/// <summary>
/// Pozostałość po martwej komórce raka.
/// Martwica (anoksja, ≳ 150–180 µm od drożnego naczynia): komórka puchnie i pęka, zostaje blady „cień” (ghost cell)
/// z rozpadłym jądrem – kariorreksja (okruchy chromatyny); w większych guzach takie komórki tworzą martwicze centrum.
/// Apoptoza (p53 w niedotlenieniu, perforyna i granzymy limfocytów): komórka obkurcza się i rozpada na ciałka apoptotyczne
/// z gęstą chromatyną; makrofagi zjadają je w ciągu ~1–2 h (eferocytoza), więc w tkance widać ich niewiele naraz.
/// </summary>
public sealed class TumorDebris : Ent
{
    public readonly bool Necrotic;
    readonly float _r0; float _age;
    readonly Vector3[] _bits;
    readonly float[] _rr = new float[24];
    public override float Radius => _r0;
    float Life => Necrotic ? 60 * 3600 : 4 * 3600;
    public TumorDebris(World w, float x, float y, float r0, bool necrotic, float seed) : base(w, x, y, Rand(0, TAU))
    {
        Necrotic = necrotic; _r0 = necrotic ? r0 * 1.02f : r0 * 0.62f;
        Kind = Kind.None; Layer = 1; FlowK = 0; Mass = necrotic ? 10 : 2; Soft = 0.8f; R = _r0 + 1;
        // cień martwiczej komórki nadal zajmuje miejsce; drobne ciałka apoptotyczne łatwo minąć
        Solid = necrotic; Name = necrotic ? "komórka martwicza" : "ciałka apoptotyczne"; Latin = necrotic ? "necrosis" : "apoptosis";
        int nb = necrotic ? RandI(7, 12) : RandI(3, 5);
        _bits = new Vector3[nb];
        for (int i = 0; i < nb; i++)
        {
            float a = Rand(0, TAU), q = necrotic ? MathF.Sqrt(R01()) * 0.55f : 0.35f + 0.5f * R01();
            _bits[i] = new Vector3(MathF.Cos(a) * q * _r0, MathF.Sin(a) * q * _r0, necrotic ? Rand(0.25f, 0.6f) : Rand(1.3f, 2.3f));
        }
        for (int i = 0; i < 24; i++) _rr[i] = _r0 * (1 + 0.1f * Noise1(i * 0.7f + seed));
    }
    public override void UpdatePrim() => SetCircle(_r0 * 0.85f);
    public override bool Collides(Ent o) => Holder == null && o is not Killer { InBlood: true };
    public override void Step(float dt, float gdt)
    {
        Vx = Vy = 0;
        if (Holder != null) return;
        _age += dt * W.Bio;
        // nieuprzątnięte ciałka apoptotyczne ulegają martwicy wtórnej i znikają; cień martwiczy utrzymuje się dłużej
        if (_age > Life) Alive = false;
    }
    public override void Draw(Painter p, View v)
    {
        if (Holder is Macrophage) return;
        float al = Math.Min(1, Math.Min(_age / 600f + 0.3f, (Life - _age) / (Life * 0.25f)));
        if (al <= 0.01f) return;
        p.Local(X, Y, A);
        if (Necrotic)
        {
            // blady, obrzęknięty cień komórki z okruchami chromatyny
            Span<Vector2> s = stackalloc Vector2[24];
            for (int i = 0; i < 24; i++) { float th = i * TAU / 24; s[i] = new Vector2(MathF.Cos(th) * _rr[i], MathF.Sin(th) * _rr[i]); }
            p.FillFan(s, 0, 0, Rgb(172, 164, 152, 0.28f * al));
            p.Stroke(s, true, 0.12f, Rgb(95, 88, 84, 0.35f * al));
            foreach (var b in _bits) p.Dot(b.X, b.Y, b.Z, Rgb(55, 45, 70, 0.55f * al));
        }
        else
        {
            // obkurczanie i rozpad na ciałka w ~pół godziny; w kontraście fazowym ciemne kulki z jasnym halo
            float k = Smooth(_age / 1800f);
            foreach (var b in _bits)
            {
                float bx = b.X * k, by = b.Y * k, r = Lerp(_r0 * 0.6f, b.Z, k);
                p.Glow(bx, by, r * 1.6f, Rgb(255, 255, 240, 0.2f * al));
                p.FillCircle(bx, by, r, Rgb(140, 132, 128, 0.5f * al));
                p.Circle(bx, by, r, 0.12f, Rgb(50, 44, 58, 0.6f * al));
                p.FillCircle(bx + r * 0.25f, by - r * 0.15f, r * 0.55f, Rgb(45, 38, 62, 0.7f * al));
            }
        }
    }
}

/// <summary>
/// Limfocyt cytotoksyczny: CD8⁺ (rozpoznaje antygen nowotworowy na MHC-I) albo komórka NK (zabija komórki bez MHC-I –
/// „brak swojego”, Ljunggren i Kärre 1990). Po kontakcie tworzy synapsę immunologiczną i wydziela perforynę z granzymami;
/// pojedyncze trafienie zwykle nie zabija – uszkodzenia się sumują (Weigelin i in. 2021), a in vivo limfocyt T zabija
/// zaledwie kilka komórek na dobę (Halle i in. 2016). PD-L1 na komórce nowotworowej wyłącza limfocyt T (Freeman i in. 2000).
/// </summary>
public sealed class Killer : Ent
{
    public readonly bool NK;
    public bool InBlood, Exhausted;
    public float Lane;
    readonly float _rr0, _seed; float _turnT, _cd, _syn; TumorCell _tgt;
    readonly float[] _rr = new float[40];
    public override bool Swims => false;
    public override bool Collides(Ent o) => InBlood ? o is TumorCell { Where: not TumorCell.Site.Tissue } : !(o is TumorCell { Where: not TumorCell.Site.Tissue });
    public Killer(World w, float x, float y, bool nk, bool blood = false) : base(w, x, y, Rand(0, TAU))
    {
        NK = nk; InBlood = blood;
        Kind = Kind.Killer; Layer = 2; Soft = 0.5f; FlowK = 0;
        Name = nk ? "komórka NK" : "limfocyt T cytotoksyczny (CD8⁺)"; Latin = nk ? "natural killer" : "CTL";
        _rr0 = nk ? Rand(4.2f, 4.6f) : Rand(3.7f, 4.1f); R = _rr0 + 0.8f; Mass = 5;
        Mem = new Membrane(40) { MaxDef = 2f, Stiff = 8, Damping = 0.6f }; MemK = 4; MemS = 0.6f; _seed = Rand(0, 99);
        Shape(); Mem.Follow(x, y, A, 1, 1);
    }
    bool Sees(TumorCell t) => NK ? true : !t.Has(Muts.B2m);
    void Shape()
    {
        float t = W.T;
        for (int i = 0; i < 40; i++)
        {
            float th = i * TAU / 40;
            _rr[i] = _rr0 * (1 + 0.06f * Noise1(th * 3 + t * 0.5f + _seed) + (InBlood ? 0 : 0.18f * Gauss(WrapA(th - PI), 0.35f) + 0.1f * Gauss(WrapA(th), 0.6f)));
        }
        Mem.Radial(_rr);
    }
    public override void UpdatePrim() => SetCircle(_rr0 * 0.9f);
    public override void Step(float dt, float gdt)
    {
        float bdt = dt * W.Bio;
        _cd -= InBlood ? gdt : bdt;
        if (InBlood) { Vx = W.BloodV * 0.12f / Math.Max(1, W.TimeScale); Vy = 0; Shape(); return; }
        if (_syn > 0)
        {
            Vx = Vy = 0;
            if (_tgt == null || !_tgt.Alive || _tgt.Where != TumorCell.Site.Tissue || Hypot(_tgt.X - X, _tgt.Y - Y) > _tgt.R0 + R + 4) { _syn = 0; _tgt = null; }
            else
            {
                A = MathF.Atan2(_tgt.Y - Y, _tgt.X - X);
                _syn += bdt;
                if (_syn > (NK ? 20 : 40) * 60) Hit();
            }
            Shape(); return;
        }
        float v; float tgtA = A;
        if (Exhausted) { v = 1.5f / 60; }
        else
        {
            var t = W.Near(X, Y, 70, e => e is TumorCell tc && tc.Where == TumorCell.Site.Tissue && Sees(tc) && (!NK || tc.Has(Muts.B2m) || Hypot(tc.X - X, tc.Y - Y) < 25));
            if (t != null) { tgtA = MathF.Atan2(t.Y - Y, t.X - X); v = (NK ? 5f : 7f) / 60; }
            else { _turnT -= dt; if (_turnT <= 0) { A += RandN() * 0.9f; _turnT = ExpRand(300); } tgtA = A; v = 4f / 60; }
        }
        // repolaryzacja trwa ~minutę czasu biologicznego; odwrócony od celu limfocyt najpierw się obraca, potem pełznie
        // (stała prędkość przy ograniczonym skręcie dawałaby orbitę wokół celu zamiast kontaktu)
        float da = WrapA(tgtA - A);
        A = Turn(A, tgtA, Math.Max(1.5f * gdt, dt / 40));
        float mesh = 1 + Math.Min(Mesh, 1.5f); Mesh = 0;
        v *= Math.Max(0.15f, MathF.Cos(da)) / mesh;
        Vx = MathF.Cos(A) * v; Vy = MathF.Sin(A) * v;
        Shape();
    }
    public override void OnTouch(Ent o, float nx, float ny)
    {
        if (o is not TumorCell t || _cd > 0 || W.Preview) return;
        if (InBlood)
        {
            if (t.Where == TumorCell.Site.Tissue) return;
            float dmg = (t.Has(Muts.B2m) ? 0.4f : 0.18f) * (1 - 0.85f * t.Shield);
            t.Hurt(dmg, t.Shield > 0.5f ? "Komórka NK przebiła się przez osłonę płytek." : "Komórka NK we krwi zabiła krążącą komórkę nowotworową – płytki krwi chronią przed tym (Nieswandt i in. 1999).");
            if (t.IsPlayer && t.Alive) W.LogOnce("nkb", t.Shield > 0.5f ? "Komórka NK uderzyła, ale płytki krwi osłabiły atak." : "Atak komórki NK we krwi! Płytki krwi (osłona) by chroniły.", "bad", 5);
            W.Sfx("hurt", X, Y, 0.5f);
            _cd = 3; return;
        }
        if (_syn > 0 || Exhausted || !Sees(t) || t.Where != TumorCell.Site.Tissue) return;
        _syn = 0.001f; _tgt = t;
        if (t.IsPlayer) W.LogOnce("syn" + Id, NK ? "Komórka NK tworzy synapsę immunologiczną – sprawdza, czy masz MHC-I." : "Limfocyt T rozpoznał antygen nowotworowy na MHC-I – synapsa immunologiczna, perforyna i granzymy.", "bad", 20);
    }
    void Hit()
    {
        var t = _tgt; _syn = 0; _tgt = null; _cd = (NK ? 30 : 45) * 60;
        if (t == null || !t.Alive) return;
        if (!NK && t.Has(Muts.Pdl1))
        {
            Exhausted = true; W.AddStat("t_exh", 1);
            if (t.IsPlayer || Hypot(t.X - W.CamX, t.Y - W.CamY) < 120) W.Log("PD-L1 → PD-1: limfocyt T zostaje wyczerpany i przestaje zabijać (to na tę „hamulcową” oś działają leki anty-PD-1).", "good");
            return;
        }
        float dmg = NK ? (t.Has(Muts.B2m) ? 0.5f : 0.08f) : 0.3f;
        t.Hurt(dmg, NK ? "Komórka NK zabiła Twoją komórkę – bez MHC-I nie ma sygnału „swój” (Ljunggren i Kärre 1990)." : "Limfocyt T cytotoksyczny zabił Twoją komórkę (perforyna, granzymy). Odporność tępi klony z antygenami – to immunoedycja nowotworu.");
        W.Sfx("snap", t.X, t.Y, 0.6f);
        if (t.IsPlayer && t.Alive) W.Log(NK ? "Komórka NK uszkodziła błonę (perforyna)." : "Trafienie limfocytu T: perforyna i granzymy – uszkodzenia się sumują, uciekaj!", "bad");
    }
    public override void Draw(Painter p, View v)
    {
        int n = Mem.Smooth(p.Shape, 2);
        var sh = new ReadOnlySpan<Vector2>(p.Shape, 0, n);
        float al = Exhausted ? 0.55f : 1;
        p.SoftStroke(sh, true, 2.2f, Rgb(255, 255, 240, 0.28f * al));
        p.FillFan(sh, X, Y, Rgb(150, 158, 150, 0.36f * al));
        p.Stroke(sh, true, 0.12f, Rgb(40, 44, 40, 0.7f * al));
        BodyLocal(p);
        p.FillCircleGrad(-0.4f, 0, _rr0 * 0.72f, Rgb(80, 84, 104, 0.55f * al), Rgb(60, 62, 82, 0.6f * al));
        p.Circle(-0.4f, 0, _rr0 * 0.72f, 0.14f, Rgb(35, 36, 50, 0.5f * al));
        // ziarnistości cytotoksyczne (perforyna, granzymy) – u NK liczne („duże ziarniste limfocyty”)
        int g = NK ? 9 : 4;
        for (int i = 0; i < g; i++) { float th = 0.6f + i * 0.55f + _seed; p.Dot(_rr0 * 0.55f * MathF.Cos(th) + 1.1f, _rr0 * 0.55f * MathF.Sin(th), 0.28f, Rgb(120, 60, 110, 0.7f * al)); }
        if (_syn > 0 && _tgt != null)
        {
            // synapsa: ziarnistości spolaryzowane ku celowi
            float k = Clamp(_syn / ((NK ? 20 : 40) * 60), 0, 1);
            for (int i = 0; i < 4; i++) p.Dot(_rr0 * (0.4f + 0.5f * k), (i - 1.5f) * 0.7f, 0.3f, Rgb(255, 230, 150, 0.8f));
            p.Arc(0, 0, _rr0 + 0.3f, -0.5f, 0.5f, 0.35f, Rgb(255, 240, 190, 0.6f));
        }
    }
}

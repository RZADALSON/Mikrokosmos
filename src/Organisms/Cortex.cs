namespace Mikro;

/// <summary>
/// Kora mózgu zarodka myszy – przekrój czołowy grzbietowej części półkuli, „rozprostowany”: oś y biegnie w głąb, od opony
/// miękkiej (y = 0) do powierzchni komory bocznej (y = 482 µm), oś x – stycznie; linia środkowa (szczelina międzypółkulowa,
/// ciało modzelowate, klin glejowy, przegroda) leży po lewej, w XM. Grubości stref jak w E16,5: cała ściana 482 µm,
/// strefa komorowa 86 µm, podpłytka + strefa pośrednia + podkomorowa ≈ 230 µm, płytka korowa ze strefą brzeżną 160 µm
/// (Tonosaki i in., PLoS One 2023); najbardziej zewnętrzna, niedojrzała część płytki (PCZ) ma ~50 µm (Sekine i in. 2011).
/// W naturze kora przyśrodkowa wygina się w dół ku spoidłu, a odległość do linii środkowej jest ok. 2× większa.
/// </summary>
public static class Cortex
{
    public const float MenT = -22, Pia = 0, MzB = 20, PczB = 70, CpB = 160, SpB = 178, IzB = 336, SvzB = 396, VzB = 482, VenB = 516;
    /// <summary>Linia środkowa (x), połowa szerokości szczeliny międzypółkulowej, górna i dolna granica pasa, którym aksony przechodzą przez linię środkową.</summary>
    public const float XM = -1150, FW = 16, Cc0 = 196, Cc1 = 262;
    public enum Z { Out, Meninges, MZ, CP, SP, IZ, SVZ, VZ, Ventricle, LGE, Fissure, IG, CC, GW, Septum }

    public static Z At(float x, float y)
    {
        float dx = MathF.Abs(x - XM);
        if (dx < 90)
        {
            if (dx < FW + 6 && y < Cc0 - 16) return y < MenT ? Z.Out : Z.Fissure;
            if (dx < 60 && y >= Cc0 - 16 && y < Cc0) return Z.IG;
            if (dx < 60 && y >= Cc0 && y <= Cc1) return Z.CC;
            if (dx < 55 && y > Cc1) return Z.Septum;
        }
        // klin glejowy: pod ciałem modzelowatym, przy grzbietowo-przyśrodkowym rogu komory (Shu i Richards 2001)
        if (y > Cc1 + 6 && y < VzB && dx < 175 && dx < 40 + (y - Cc1) * 0.75f) return Z.GW;
        if (y < MenT) return Z.Out;
        if (y < Pia) return Z.Meninges;
        if (y < MzB) return Z.MZ;
        if (y < CpB) return Z.CP;
        if (y < SpB) return Z.SP;
        if (y < IzB) return Z.IZ;
        if (y < SvzB) return Z.SVZ;
        if (y < VzB) return Z.VZ;
        if (y < VenB) return Z.Ventricle;
        return Z.LGE;
    }
    /// <summary>Tkanka kory, w której może leżeć ciało neuronu.</summary>
    public static bool Tissue(Z z) => z is Z.MZ or Z.CP or Z.SP or Z.IZ or Z.SVZ or Z.VZ;
    public static string Name(Z z) => z switch
    {
        Z.Meninges => "opony (opona miękka)", Z.MZ => "strefa brzeżna (MZ)", Z.CP => "płytka korowa (CP)", Z.SP => "podpłytka (SP)",
        Z.IZ => "strefa pośrednia (IZ)", Z.SVZ => "strefa podkomorowa (SVZ)", Z.VZ => "strefa komorowa (VZ)", Z.Ventricle => "komora boczna",
        Z.LGE => "wyniosłość zwojowa boczna", Z.Fissure => "szczelina międzypółkulowa", Z.IG => "szare pokrywające (indusium griseum)",
        Z.CC => "ciało modzelowate – linia środkowa", Z.GW => "klin glejowy (glial wedge)", Z.Septum => "przegroda przezroczysta", _ => "poza skrawkiem",
    };
    /// <summary>Strefa z uwzględnieniem niedojrzałej części płytki korowej (PCZ) – do HUD.</summary>
    public static string NameAt(float x, float y)
    {
        var z = At(x, y);
        if (z == Z.CP && y < PczB) return "płytka korowa – część zewnętrzna (PCZ)";
        if (z == Z.IZ && y > 290) return "strefa pośrednia – dolna (komórki wielobiegunowe)";
        return Name(z);
    }
}

/// <summary>
/// Stała (deterministyczna z ziarna świata) zawartość skrawka: włókna gleju promienistego, pęczki aksonów w strefie
/// pośredniej, naczynia i pole tysięcy komórek (progenitory ze śródfazową wędrówką jąder, komórki wielobiegunowe,
/// młode neurony płytki korowej, komórki Cajala–Retziusa, opony). Komórki pola nie są encjami – rysowane są jako tekstury,
/// a ruchome komórki (Twój neuron, rodzeństwo, interneurony, mikroglej) rozsuwają je, przeciskając się między nimi.
/// </summary>
public sealed class CortexField
{
    public readonly int Seed;
    public CortexField(int seed) { Seed = seed; }

    // ------------------------------------------------ włókna gleju promienistego
    /// <summary>Odstęp włókien (µm). W korze myszy włókna leżą gęściej (każda komórka gleju ma jedno), ale w płytce korowej
    /// łączą się w pęczki – w grze każde włókno to pęczek, po którym może wspinać się neuron.</summary>
    public const float FS = 8;
    public float FiberBase(int i) => i * FS + (Hash2(i, 11, Seed) - 0.5f) * 3.2f;
    public bool FiberExists(int i) => MathF.Abs(FiberBase(i) - Cortex.XM) > 34;
    public float FiberX(int i, float y)
    {
        float ph = Hash2(i, 12, Seed) * TAU;
        return FiberBase(i) + 1.3f * MathF.Sin(y / 55 + ph) + 0.6f * MathF.Sin(y / 17 + 2 * ph);
    }
    /// <summary>Głębokość ciała komórki gleju promienistego (w strefie komorowej), z którego wychodzi włókno.</summary>
    public float FiberBottom(int i) => 408 + Hash2(i, 13, Seed) * 66;
    /// <summary>Kierunek włókna (styczna do osi) w punkcie – do ustawienia neuronu wzdłuż niego.</summary>
    public float FiberSlope(int i, float y) => (FiberX(i, y + 1) - FiberX(i, y - 1)) / 2;
    public bool NearestFiber(float x, float y, float maxD, out int fi, out float fx)
    {
        fi = int.MinValue; fx = 0; float best = maxD;
        int i0 = (int)MathF.Floor(x / FS);
        for (int i = i0 - 2; i <= i0 + 2; i++)
        {
            if (!FiberExists(i) || y > FiberBottom(i)) continue;
            float xf = FiberX(i, y), d = MathF.Abs(xf - x);
            if (d < best) { best = d; fi = i; fx = xf; }
        }
        return fi != int.MinValue;
    }

    // ------------------------------------------------ pęczki aksonów w strefie pośredniej
    /// <summary>
    /// Pęczki aksonów biegnące stycznie w strefie pośredniej (przyszła istota biała). Górne (na wysokości spoidła) to aksony
    /// spoidłowe idące za aksonami pionierskimi z kory obręczy przez linię środkową; dolne – aksony eferentne głębszych warstw
    /// z TAG-1, o które zaczepiają się komórki wielobiegunowe – kontakt wyznacza, która wypustka stanie się aksonem (Namba i in. 2014).
    /// </summary>
    public const int NF = 18;
    public static float FascBase(int j) => 186 + j * 8.4f;
    public static bool Callosal(int j) { float b = FascBase(j); return b > Cortex.Cc0 + 3 && b < Cortex.Cc1 - 3; }
    public static bool Tag1(int j) => FascBase(j) > 280;
    public float FascY(int j, float x)
    {
        float ph = Hash2(j, 31, Seed) * TAU;
        return FascBase(j) + 2.4f * MathF.Sin(x / 150 + ph) + 1.1f * MathF.Sin(x / 53 + 2 * ph);
    }
    /// <summary>Widoczność pęczka w x: pęczki inne niż spoidłowe skręcają przy linii środkowej w dół (torebka wewnętrzna) i znikają z płaszczyzny.</summary>
    public static float FascAlpha(int j, float x)
    {
        float dx = MathF.Abs(x - Cortex.XM);
        if (Callosal(j)) return dx < 40 ? 0.7f : 1;
        return Smooth((dx - 150) / 140);
    }
    public bool NearestFascicle(float x, float y, float maxD, out int fj, out float dist)
    {
        fj = -1; dist = maxD;
        for (int j = 0; j < NF; j++)
        {
            if (FascAlpha(j, x) < 0.3f) continue;
            float d = MathF.Abs(FascY(j, x) - y);
            if (d < dist) { dist = d; fj = j; }
        }
        return fj >= 0;
    }

    // ------------------------------------------------ naczynia
    /// <summary>
    /// Naczynia zarodkowej kory: splot opony miękkiej na powierzchni, splot okołokomorowy w cienkiej (~20 µm) warstwie nad
    /// strefą komorową i łączące je cienkie naczynia promieniste (Vasudevan i in., Nat Neurosci 2008).
    /// </summary>
    public const float RVS = 190;
    public float RadBase(int k) => k * RVS + (Hash2(k, 22, Seed) - 0.5f) * RVS * 0.45f;
    public bool RadExists(int k) => Hash2(k, 21, Seed) < 0.82f && MathF.Abs(RadBase(k) - Cortex.XM) > 150;
    public float RadX(int k, float y) { float ph = Hash2(k, 23, Seed) * TAU; return RadBase(k) + 5 * MathF.Sin(y / 60 + ph) + 2 * MathF.Sin(y / 23 + 2 * ph); }
    public float RadR(int k) => 2.6f + Hash2(k, 24, Seed) * 0.7f;
    public float PlexY(float x) { float ph = Hash2(3, 25, Seed) * TAU; return 352 + 6 * MathF.Sin(x / 110 + ph) + 3 * MathF.Sin(x / 41 + 2 * ph); }
    public const float PlexR = 2.9f, PialR = 4.3f, FisR = 5.5f;
    public float PialY(float x) { float ph = Hash2(4, 26, Seed) * TAU; return -10 + 2.5f * MathF.Sin(x / 170 + ph); }
    /// <summary>
    /// Splot okołokomorowy to trójwymiarowa sieć naczyń: w płaszczyźnie skrawka widać jego odcinki (~60–90 µm) z przerwami,
    /// w których naczynia schodzą z płaszczyzny – tamtędy przechodzą komórki wędrujące ku korze.
    /// </summary>
    public bool PlexExists(float x)
    {
        if (MathF.Abs(x - Cortex.XM) <= 75) return false;
        const float L = 110; int k = (int)MathF.Floor(x / L); float u = x / L - k;
        float gap = 0.22f + 0.2f * Hash2(k, 27, Seed), start = Hash2(k, 28, Seed) * (1 - gap);
        return u <= start || u >= start + gap;
    }
    public static bool PialExists(float x) => MathF.Abs(x - Cortex.XM) > Cortex.FW + 10;
    public static float FisX(float y) => Cortex.XM + 3 * MathF.Sin(y / 45);
    /// <summary>Odległość od ściany najbliższego naczynia (µm, ujemna w świetle) i kierunek na zewnątrz.</summary>
    public float VesselDist(float x, float y, out float nx, out float ny)
    {
        float best = 1e9f; nx = 0; ny = -1;
        if (PialExists(x)) { float d = y - PialY(x); float s = MathF.Abs(d) - PialR; if (s < best) { best = s; nx = 0; ny = d >= 0 ? 1 : -1; } }
        if (PlexExists(x)) { float d = y - PlexY(x); float s = MathF.Abs(d) - PlexR; if (s < best) { best = s; nx = 0; ny = d >= 0 ? 1 : -1; } }
        if (MathF.Abs(x - Cortex.XM) < 40 && y < Cortex.Cc0 - 20) { float d = x - FisX(y); float s = MathF.Abs(d) - FisR; if (s < best) { best = s; nx = d >= 0 ? 1 : -1; ny = 0; } }
        int k0 = (int)MathF.Floor(x / RVS);
        for (int k = k0 - 1; k <= k0 + 2; k++)
        {
            if (!RadExists(k)) continue;
            float top = PialY(RadBase(k)), bot = PlexY(RadBase(k));
            if (y < top - 4 || y > bot + 4) continue;
            float d = x - RadX(k, y), s = MathF.Abs(d) - RadR(k);
            if (s < best) { best = s; nx = d >= 0 ? 1 : -1; ny = 0; }
        }
        return best;
    }

    // ------------------------------------------------ pole komórek
    public enum CT : byte { RG, IPC, MP, SPN, CPY, CPD, CR, Men, LGE, Glia, Sept }
    /// <summary>Komórka pola: środek (dla gleju promienistego – oś wędrówki jądra), oś długa, wydłużenie, kąt, fazy.</summary>
    public struct FCell { public float X, Y, W, Asp, Ang, P1, P2; public CT T; public int Id; }
    public const float CW = 40;
    readonly Dictionary<int, FCell[]> _cols = new();
    /// <summary>Komórki pola, które zginęły (apoptoza) – nie są już rysowane.</summary>
    public readonly HashSet<int> Dead = new();
    public FCell[] Column(int c)
    {
        lock (_cols)
        {
            if (_cols.TryGetValue(c, out var hit)) return hit;
            if (_cols.Count > 4000) _cols.Clear();
        }
        var arr = Gen(c);
        lock (_cols) _cols[c] = arr;
        return arr;
    }

    FCell[] Gen(int c)
    {
        var L = new List<FCell>(340);
        ulong s = ((ulong)(uint)(c * 73856093) << 32 | (uint)(Seed * 19349663)) | 1UL;
        float R() { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return (s >> 40) * (1f / 16777216f); }
        float x0 = c * CW; int id = 0;
        bool Free(float x, float y, float rr) => VesselDist(x, y, out _, out _) > rr + 0.6f;
        void Add(CT t, float x, float y, float w, float asp, float ang, float p1 = 0, float p2 = 0)
        {
            L.Add(new FCell { X = x, Y = y, W = w, Asp = asp, Ang = ang, P1 = p1, P2 = p2, T = t, Id = (c << 12) ^ id });
            id++;
        }
        // opony: płaskie komórki (fibroblasty opony miękkiej i pajęczynówki); w szczelinie międzypółkulowej – pionowo
        for (int n = R() < 0.6f ? 2 : 1; n > 0; n--)
        {
            float x = x0 + R() * CW, y = -20 + R() * 16;
            if (Cortex.At(x, y) == Cortex.Z.Meninges && Free(x, y, 2)) Add(CT.Men, x, y, 14 + R() * 10, 5, (R() - 0.5f) * 0.2f);
        }
        if (x0 + CW > Cortex.XM - 30 && x0 < Cortex.XM + 30)
            for (float y = -18; y < Cortex.Cc0 - 18; y += 11)
            {
                float x = Cortex.XM + (R() - 0.5f) * Cortex.FW * 1.6f;
                if (x >= x0 && x < x0 + CW && Cortex.At(x, y) == Cortex.Z.Fissure) Add(CT.Men, x, y + R() * 6, 12 + R() * 8, 5, PI / 2 + (R() - 0.5f) * 0.3f);
            }
        // strefa brzeżna: komórki Cajala–Retziusa (duże, poziome, z grubą wypustką; wydzielają reelinę)
        for (int n = R() < 0.75f ? 1 : 0; n > 0; n--)
        {
            float x = x0 + R() * CW, y = 6 + R() * 9;
            if (Cortex.At(x, y) == Cortex.Z.MZ && Free(x, y, 6)) Add(CT.CR, x, y, 11 + R() * 2.5f, 1.7f, (R() - 0.5f) * 0.25f, R(), R() < 0.5f ? -1 : 1);
        }
        // płytka korowa: neurony upakowane ciasno, w promienistych kolumnach; w PCZ – drobne, niedojrzałe, dwubiegunowe;
        // głębiej – starsze neurony warstw VI/V (urodzone ok. E12–E14), większe i bardziej okrągłe (układ „od środka na zewnątrz”)
        const float sx = 6.8f, sy = 9.2f;
        for (float gx = x0 + sx * 0.5f; gx < x0 + CW; gx += sx)
            for (float gy = Cortex.MzB + 4.8f; gy < Cortex.CpB - 2; gy += sy)
            {
                if (R() < 0.08f) continue;
                float x = gx + (R() - 0.5f) * 3.8f, y = gy + (R() - 0.5f) * 4.6f;
                if (Cortex.At(x, y) != Cortex.Z.CP) continue;
                bool young = y < Cortex.PczB;
                float w = young ? 9 + R() : y < 110 ? 9.6f + R() : 10.3f + R() * 1.2f, asp = young ? 1.62f : y < 110 ? 1.45f : 1.25f;
                if (!Free(x, y, w * 0.45f)) continue;
                Add(young || y < 110 ? CT.CPY : CT.CPD, x, y, w, asp, PI / 2 + (R() - 0.5f) * 0.3f, R(), R());
            }
        // podpłytka: większe, często poziome neurony podpłytki między pęczkami włókien
        for (float gx = x0 + 6; gx < x0 + CW; gx += 13)
            foreach (float gy in new[] { 166f, 173f })
            {
                if (R() > 0.55f) continue;
                float x = gx + (R() - 0.5f) * 5, y = gy + (R() - 0.5f) * 3;
                if (Cortex.At(x, y) == Cortex.Z.SP && Free(x, y, 6)) Add(CT.SPN, x, y, 12 + R() * 2, 1.4f, (R() - 0.5f) * 0.6f, R(), R());
            }
        // strefa pośrednia: luźno leżące komórki wielobiegunowe, najgęściej w dolnej części (strefa gromadzenia komórek
        // wielobiegunowych, MAZ – Tabata i Nakajima 2003)
        for (float gx = x0 + 5.5f; gx < x0 + CW; gx += 11)
            for (float gy = Cortex.SpB + 6; gy < Cortex.IzB - 3; gy += 11)
            {
                if (R() > (gy < 290 ? 0.16f : 0.42f)) continue;
                float x = gx + (R() - 0.5f) * 5, y = gy + (R() - 0.5f) * 5;
                if (Cortex.At(x, y) == Cortex.Z.IZ && Free(x, y, 5)) Add(CT.MP, x, y, 8.3f + R(), 1.12f, R() * TAU, R(), R());
            }
        // strefa podkomorowa: progenitory pośrednie (Tbr2+) – dzielą się tu symetrycznie na dwa neurony (Noctor i in. 2004)
        for (float gx = x0 + 4.3f; gx < x0 + CW; gx += 8.6f)
            for (float gy = Cortex.IzB + 4.5f; gy < Cortex.SvzB - 3; gy += 8.6f)
            {
                if (R() > 0.78f) continue;
                float x = gx + (R() - 0.5f) * 3, y = gy + (R() - 0.5f) * 3;
                if (Cortex.At(x, y) == Cortex.Z.SVZ && Free(x, y, 5)) Add(CT.IPC, x, y, 8.8f + R() * 0.8f, 1.08f, R() * TAU, R(), R());
            }
        // strefa komorowa: nabłonek wielorzędowy gleju promienistego; jądra wędrują w cyklu komórkowym (INM): w fazie S przy
        // podstawnym brzegu strefy, mitoza przy powierzchni komory (Takahashi i in. 1995)
        for (float gx = x0 + 2.2f; gx < x0 + CW; gx += 4.4f)
            for (int k = 0; k < 8; k++)
            {
                float x = gx + (R() - 0.5f) * 1.6f;
                var z = Cortex.At(x, 440);
                if (z != Cortex.Z.VZ || !Free(x, 440, 2.5f)) { R(); R(); continue; }
                Add(CT.RG, x, 440, 10 + R() * 1.4f, 2.5f, PI / 2 + (R() - 0.5f) * 0.12f, R(), 398 + R() * 24);
            }
        // wyniosłość zwojowa boczna za komorą (dno komory): jej strefa komorowa i podkomorowa
        for (float gx = x0 + 2.2f; gx < x0 + CW; gx += 4.4f)
            for (int k = 0; k < 5; k++)
            {
                float x = gx + (R() - 0.5f) * 1.6f;
                if (Cortex.At(x, Cortex.VenB + 30) != Cortex.Z.LGE) { R(); R(); continue; }
                Add(CT.LGE, x, Cortex.VenB + 30, 10 + R() * 1.4f, 2.5f, PI / 2 + (R() - 0.5f) * 0.12f, R(), Cortex.VenB + 34 + R() * 26);
            }
        for (float gx = x0 + 4.3f; gx < x0 + CW; gx += 8.6f)
            for (float gy = Cortex.VenB + 66; gy < Cortex.VenB + 130; gy += 8.6f)
            {
                if (R() > 0.8f) continue;
                float x = gx + (R() - 0.5f) * 3, y = gy + (R() - 0.5f) * 3;
                if (Cortex.At(x, y) == Cortex.Z.LGE) Add(CT.IPC, x, y, 8.6f + R() * 0.8f, 1.08f, R() * TAU, R(), R());
            }
        // linia środkowa: glej szarego pokrywającego (nad spoidłem), klinu glejowego (pod nim), rzadkie komórki w samym spoidle,
        // gęsta przegroda
        if (x0 + CW > Cortex.XM - 200 && x0 < Cortex.XM + 200)
            for (float gx = x0 + 3.5f; gx < x0 + CW; gx += 7)
                for (float gy = Cortex.Cc0 - 16; gy < Cortex.VzB + 30; gy += 7)
                {
                    float x = gx + (R() - 0.5f) * 3, y = gy + (R() - 0.5f) * 3, r = R();
                    switch (Cortex.At(x, y))
                    {
                        case Cortex.Z.IG: if (r < 0.8f) Add(CT.Glia, x, y, 7 + R(), 1.3f, R() * TAU, R(), R()); break;
                        case Cortex.Z.CC: if (r < 0.1f) Add(CT.Glia, x, y, 7.5f, 1.8f, R() * 0.4f - 0.2f, R(), R()); break;
                        case Cortex.Z.GW: if (r < 0.7f) Add(CT.Glia, x, y, 9 + R(), 2.2f, MathF.Atan2(Cortex.Cc1 - y, Cortex.XM - x) + (R() - 0.5f) * 0.3f, R(), R()); break;
                        case Cortex.Z.Septum: if (r < 0.75f) Add(CT.Sept, x, y, 8.4f + R(), 1.15f, R() * TAU, R(), R()); break;
                    }
                }
        return L.ToArray();
    }

    // ------------------------------------------------ animacja komórek pola
    /// <summary>Długość cyklu komórkowego progenitorów strefy komorowej w E16 (Takahashi i in. 1995: 8,1 h w E11 → 18,4 h w E16).</summary>
    public const float Tc = 18.4f * 3600;
    /// <summary>
    /// Położenie jądra progenitora w cyklu (wędrówka jądra śródfazowa, INM): mitoza przy powierzchni komory, w G1 jądro
    /// wędruje podstawnie, fazę S przechodzi przy podstawnym brzegu strefy, w G2 szybko wraca do komory. Po zakończeniu
    /// neurogenezy (~E18) cykle ustają. apical – y powierzchni komory; dir = −1: strefa ku górze (kora), +1: ku dołowi (LGE).
    /// </summary>
    public static float Inm(float ph, float apical, float basal, out bool mitosis)
    {
        // proporcje faz komórek szczytowych (E14,5 – Arai i in. 2011): M 0,9 h, G1 11,6 h, S 5,0 h, G2 1,6 h z 19,1 h
        mitosis = ph < 0.047f;
        if (ph < 0.047f) return apical;
        if (ph < 0.654f) return Lerp(apical, basal, Smooth((ph - 0.047f) / 0.607f));
        if (ph < 0.916f) return basal;
        return Lerp(basal, apical, Smooth((ph - 0.916f) / 0.084f));
    }

    // ------------------------------------------------ rysowanie
    /// <summary>Ruchome ciała (x, y, promień), które rozsuwają komórki pola – posortowane po x (ustawia scenariusz co klatkę).</summary>
    public Vector3[] Movers = Array.Empty<Vector3>();
    public int MoverN;
    /// <summary>Wiek (dni od zapłodnienia, E) – steruje neurogenezą, zanikiem włókien itd.</summary>
    public float Age = 15.5f;
    public float SimT;
    static Color Tt(int r, int g, int b, float a) => new(r / 255f / SpriteAtlas.NeuBase, g / 255f / SpriteAtlas.NeuBase, b / 255f / SpriteAtlas.NeuBase, a);
    static readonly Color CRg = Tt(150, 142, 160, 0.52f), CIpc = Tt(152, 146, 160, 0.5f), CMp = Tt(160, 154, 164, 0.46f), CCpY = Tt(146, 140, 156, 0.54f),
        CCpD = Tt(156, 150, 162, 0.52f), CSp = Tt(164, 156, 166, 0.5f), CCr = Tt(166, 158, 162, 0.56f), CMen = Tt(176, 164, 150, 0.45f), CGlia = Tt(162, 156, 150, 0.5f),
        CMit = Tt(170, 164, 172, 0.58f);
    static readonly Color Proc = Rgb(96, 90, 104, 0.34f), ProcL = Rgb(96, 90, 104, 0.22f), CrProc = Rgb(92, 86, 96, 0.4f);

    /// <summary>Przesunięcie komórki przez przeciskające się obok ciało (rozsuwanie tkanki – tylko wygląd).</summary>
    void Push(ref float x, ref float y, float r, int m0, int m1)
    {
        for (int m = m0; m < m1; m++)
        {
            var q = Movers[m]; float dx = x - q.X, dy = y - q.Y, rr = q.Z + r * 0.8f, d2 = dx * dx + dy * dy;
            if (d2 >= rr * rr) continue;
            float d = MathF.Sqrt(d2);
            if (d < 1e-3f) { dx = 1; dy = 0; d = 1; }
            float k = (rr - d) / d; x += dx * k; y += dy * k;
        }
    }
    int Lower(float x)
    {
        int lo = 0, hi = MoverN;
        while (lo < hi) { int mid = (lo + hi) >> 1; if (Movers[mid].X < x) lo = mid + 1; else hi = mid; }
        return lo;
    }

    /// <summary>Bieżące położenie komórki pola (wędrówka jądra w cyklu, drganie komórek wielobiegunowych) i czy jest w mitozie.</summary>
    public void CellPos(in FCell f, out float x, out float y, out float ang, out bool mit)
    {
        float hrs = SimT / 3600f; bool neuro = Age < 18.2f;
        x = f.X; y = f.Y; ang = f.Ang; mit = false;
        switch (f.T)
        {
            case CT.RG: { float ph = neuro ? (hrs / 18.4f + f.P1) % 1 : 0.35f + 0.3f * f.P1; y = Inm(ph, Cortex.VzB - 5, f.P2, out mit); break; }
            case CT.LGE: { float ph = neuro ? (hrs / 18.4f + f.P1) % 1 : 0.35f + 0.3f * f.P1; y = Inm(ph, Cortex.VenB + 5, f.P2, out mit); break; }
            // progenitor pośredni w mitozie ~2% czasu (mitoza 0,5 h na cykl 26,2 h – Arai i in. 2011)
            case CT.IPC: mit = neuro && (hrs / 26.2f + f.P1) % 1 < 0.019f; if (mit) ang = f.P2 * TAU; break;
            // komórka wielobiegunowa: ciało przesuwa się powoli i nieregularnie (Tabata i Nakajima 2003)
            case CT.MP: x += 3.5f * Noise1(hrs / 5 + f.P1 * 40); y += 3.5f * Noise1(hrs / 5 + f.P2 * 40 + 17); break;
        }
    }
    /// <summary>
    /// Po zakończeniu neurogenezy (~E18) komórki wielobiegunowe kolejno wychodzą ze strefy pośredniej do płytki korowej –
    /// od E19 widać ich coraz mniej (zostaje część: ostatnie wędrujące neurony i progenitory gleju istoty białej).
    /// </summary>
    public bool Gone(in FCell f) => f.T == CT.MP && Age > 19 && Hash2(f.Id, 91, Seed) < Math.Min(0.7f, (Age - 19) / 3);
    /// <summary>Komórka pola pod punktem (tryb podglądu – karta atlasu).</summary>
    public bool CellAt(float x, float y, out CT t)
    {
        t = CT.RG; int c0 = (int)MathF.Floor(x / CW);
        float best = 1e9f; bool hit = false;
        for (int c = c0 - 1; c <= c0 + 1; c++)
            foreach (var f in Column(c))
            {
                if (Dead.Contains(f.Id) || Gone(f)) continue;
                CellPos(f, out float cx, out float cy, out float ang, out _);
                float ca = MathF.Cos(ang), sa = MathF.Sin(ang), dx = x - cx, dy = y - cy;
                float u = (dx * ca + dy * sa) / (f.W * 0.5f), w = (-dx * sa + dy * ca) / (f.W * 0.5f / f.Asp), d = u * u + w * w;
                if (d < 1 && d < best) { best = d; t = f.T; hit = true; }
            }
        return hit;
    }

    /// <summary>Komórki pola (co parts-ta kolumna): tekstury ciał malarzem z teksturą, wypustki – wektorowo pod nimi.</summary>
    public void DrawCells(Painter p, View v, int part, int parts)
    {
        int c0 = (int)MathF.Floor(v.X0 / CW) - 1, c1 = (int)MathF.Floor(v.X1 / CW) + 1;
        var tp = p.Tex; float px = v.Px; bool far = px > 0.2f;
        float hrs = SimT / 3600f;
        bool neuro = Age < 18.2f;
        var uvR1 = SpriteAtlas.Uv[SpriteAtlas.NeuR]; var uvE1 = SpriteAtlas.Uv[SpriteAtlas.NeuE]; var uvS = SpriteAtlas.Uv[SpriteAtlas.NeuS];
        var uvR2 = SpriteAtlas.Uv[SpriteAtlas.NeuR2]; var uvE2 = SpriteAtlas.Uv[SpriteAtlas.NeuE2];
        var uvM = SpriteAtlas.Uv[SpriteAtlas.Mitosis]; var uvF = SpriteAtlas.Uv[SpriteAtlas.Flat];
        float lw = Math.Max(px, 0.45f), lwt = Math.Max(px, 0.3f);
        Span<Vector2> q3 = stackalloc Vector2[3];
        Span<float> w3 = stackalloc float[] { Math.Max(lwt, 0.8f), Math.Max(lwt, 0.45f), 0.22f };
        for (int c = c0; c <= c1; c++)
        {
            if (((c % parts) + parts) % parts != part) continue;
            float cx0 = c * CW;
            if (cx0 > v.X1 + 20 || cx0 + CW < v.X0 - 20) continue;
            int m0 = Lower(cx0 - 40), m1 = Lower(cx0 + CW + 40);
            var col = Column(c);
            for (int i = 0; i < col.Length; i++)
            {
                ref var f = ref col[i];
                float w = f.W;
                if (Gone(f)) continue;
                CellPos(f, out float x, out float y, out float ang, out bool mit);
                if (y < v.Y0 - 30 || y > v.Y1 + 30 || x < v.X0 - 30 || x > v.X1 + 30) continue;
                if (Dead.Count > 0 && Dead.Contains(f.Id)) continue;
                if (m1 > m0) Push(ref x, ref y, w * 0.5f / f.Asp + 1, m0, m1);
                if (mit)
                {
                    float d = w * (f.T == CT.IPC ? 1.08f : 0.9f) / 0.94f;
                    tp?.Sprite(x, y, d, d, f.T == CT.IPC ? ang : 0, uvM, CMit);
                    continue;
                }
                float D = w / 0.94f;
                // dwa warianty tekstury (inny układ chromatyny i jąderek) – sąsiedzi nie są identyczni
                var uvR = (f.Id & 1) == 0 ? uvR1 : uvR2; var uvE = (f.Id & 1) == 0 ? uvE1 : uvE2;
                switch (f.T)
                {
                    case CT.RG:
                    case CT.LGE:
                    {
                        // wypustka szczytowa do powierzchni komory; podstawna (włókno) rysowana osobno
                        float ya = f.T == CT.RG ? Cortex.VzB : Cortex.VenB, sgn = f.T == CT.RG ? 1 : -1;
                        if (!far || (f.Id & 1) == 0) p.Seg(x, y + sgn * w * 0.4f, x + 0.3f * sgn, ya, lwt, ProcL);
                        tp?.Sprite(x, y, D, D, ang, uvS, CRg);
                        break;
                    }
                    case CT.IPC: tp?.Sprite(x, y, D, D, ang, uvR, f.T == CT.IPC ? CIpc : CIpc); break;
                    case CT.MP:
                        if (!far)
                            for (int k = 0; k < 3; k++)
                            {
                                float a = f.Ang + k * 2.1f + 0.6f * Noise1(hrs / 3 + f.P1 * 30 + k * 7), l = 3 + 6 * (0.5f + 0.5f * Noise1(hrs / 2 + f.P2 * 30 + k * 5));
                                // cienka, zwężająca się, lekko wygięta wypustka (neuryty komórek wielobiegunowych pełzną i zginają się)
                                float b = 0.55f * Noise1(hrs / 4 + f.P1 * 50 + k * 3), am = a + b * 0.5f, hl = l * 0.5f + 0.5f;
                                q3[0] = new Vector2(x + MathF.Cos(a) * 3.2f, y + MathF.Sin(a) * 3.2f);
                                q3[1] = q3[0] + new Vector2(MathF.Cos(am), MathF.Sin(am)) * hl;
                                q3[2] = q3[1] + new Vector2(MathF.Cos(a + b), MathF.Sin(a + b)) * hl;
                                p.Taper(q3, w3, ProcL);
                            }
                        tp?.Sprite(x, y, D, D, ang, uvR, CMp);
                        break;
                    case CT.CPY:
                    {
                        // młody neuron płytki: wypustka wiodąca (przyszły dendryt szczytowy) ku oponie, cienka wypustka tylna
                        float l = 7 + 9 * f.P1;
                        if (!far || (f.Id & 1) == 0) p.Seg(x, y - w * 0.45f, x + (f.P2 - 0.5f) * 2, y - w * 0.45f - l, lw, Proc);
                        if (!far) p.Seg(x, y + w * 0.45f, x, y + w * 0.45f + 4, lwt, ProcL);
                        tp?.Sprite(x, y, D, D, ang, uvE, CCpY);
                        break;
                    }
                    case CT.CPD:
                    {
                        float l = 14 + 10 * f.P1;
                        if (!far || (f.Id & 1) == 0) p.Seg(x, y - w * 0.4f, x + (f.P2 - 0.5f) * 3, y - w * 0.4f - l, lw * 1.2f, Proc);
                        tp?.Sprite(x, y, D, D, ang, uvE, CCpD);
                        break;
                    }
                    case CT.SPN:
                        for (int k = -1; k <= 1; k += 2) p.Seg(x + k * w * 0.4f, y, x + k * (w * 0.4f + 12 + 8 * f.P1), y + (f.P2 - 0.5f) * 6 * k, lw, Proc);
                        tp?.Sprite(x, y, D, D, ang, uvE, CSp);
                        break;
                    case CT.CR:
                    {
                        // komórka Cajala–Retziusa: gruba pozioma wypustka biegnąca w strefie brzeżnej
                        float s = f.P2, l = 24 + 22 * f.P1;
                        p.Line(x + s * w * 0.4f, y, x + s * (w * 0.4f + l), y + (f.P1 - 0.5f) * 4, Math.Max(px, 1.1f), CrProc);
                        p.Seg(x - s * w * 0.4f, y, x - s * (w * 0.4f + 8), y, lwt, Proc);
                        tp?.Sprite(x, y, D, D, ang, uvE, CCr);
                        break;
                    }
                    case CT.Men: tp?.Sprite(x, y, D, D, ang, uvF, CMen); break;
                    case CT.Glia: tp?.Sprite(x, y, D, D, ang, f.Asp > 1.9f ? uvS : uvE, CGlia); break;
                    case CT.Sept: tp?.Sprite(x, y, D, D, ang, uvR, CGlia); break;
                }
                if (tp == null) p.FillEllipse(x, y, w * 0.5f, w * 0.5f / f.Asp, ang, Rgb(140, 132, 148, 0.4f));
            }
        }
    }

    /// <summary>Włókna gleju promienistego: od ciała komórki w strefie komorowej do stopki na oponie miękkiej. Po urodzeniu
    /// glej promienisty przekształca się w astrocyty, więc włókna zanikają (Schmechel i Rakic 1979).</summary>
    public void DrawFibers(Painter p, View v, float alpha)
    {
        if (alpha < 0.02f) return;
        var q = p.Tex ?? p;
        int i0 = (int)MathF.Floor(v.X0 / FS) - 1, i1 = (int)MathF.Floor(v.X1 / FS) + 1;
        float y0 = Math.Max(v.Y0 - 10, Cortex.Pia), y1 = Math.Min(v.Y1 + 10, Cortex.VzB);
        if (y1 <= y0) return;
        // włókno prawie proste (falowanie ~1 µm) – przy oddaleniu wystarczy rzadszy krok
        float step = Math.Max(4, v.Px * 26), lw = Math.Max(v.Px, 0.55f);
        var col0 = Rgb(92, 88, 100, 0.17f * alpha); var foot = Rgb(92, 88, 100, 0.22f * alpha);
        var b = p.Buf2;
        Span<Vector2> t = stackalloc Vector2[4];
        for (int i = i0; i <= i1; i++)
        {
            if (!FiberExists(i)) continue;
            float yb = Math.Min(y1, FiberBottom(i)), ya = y0;
            if (yb <= ya) continue;
            var col = Mul(col0, 0.55f + 0.45f * Hash2(i, 14, Seed));
            // naczynia, które mogą przeciąć to włókno: splot okołokomorowy (poziomy) i naczynie promieniste tuż obok (jeśli jest)
            float fb = FiberBase(i); int rk = int.MinValue;
            for (int k = (int)MathF.Floor(fb / RVS) - 1; k <= (int)MathF.Floor(fb / RVS) + 1; k++) if (RadExists(k) && MathF.Abs(RadBase(k) - fb) < 14) rk = k;
            bool plex = PlexExists(fb); float ply = PlexY(fb);
            int n = 0;
            for (float y = ya; ; y += step)
            {
                float yy = Math.Min(y, yb), xx = FiberX(i, yy);
                // naczynie przecina płaszczyznę – włókno biegnie za nim
                bool hid = plex && MathF.Abs(yy - ply) < PlexR + 0.3f || rk != int.MinValue && MathF.Abs(xx - RadX(rk, yy)) < RadR(rk) + 0.3f;
                if (hid) { if (n > 1) q.Thin(new ReadOnlySpan<Vector2>(b, 0, n), false, lw, col); n = 0; }
                else if (n < b.Length) b[n++] = new Vector2(xx, yy);
                if (yy >= yb) break;
            }
            if (n > 1) q.Thin(new ReadOnlySpan<Vector2>(b, 0, n), false, lw, col);
            // stopka włókna rozszerza się na błonie podstawnej opony miękkiej
            if (ya <= Cortex.Pia + 1 && FiberBase(i) > v.X0 - 10 && FiberBase(i) < v.X1 + 10)
            {
                float fx = FiberX(i, 0);
                t[0] = new(fx - 2.4f, 0.4f); t[1] = new(fx + 2.4f, 0.4f); t[2] = new(fx + 0.4f, 5); t[3] = new(fx - 0.4f, 5);
                q.FillFan(t, fx, 1.5f, foot, false);
            }
        }
    }

    /// <summary>Pęczki aksonów w strefie pośredniej (przyszła istota biała): wiele cienkich, równoległych włókien.</summary>
    public void DrawFascicles(Painter p, View v)
    {
        if (v.Y1 < 180 || v.Y0 > Cortex.IzB) return;
        float step = Math.Max(6, v.Px * 16), lw = Math.Max(v.Px, 0.35f);
        var b = p.Buf2;
        for (int j = 0; j < NF; j++)
        {
            float by = FascBase(j);
            if (by < v.Y0 - 10 || by > v.Y1 + 10) continue;
            bool tag = Tag1(j), cal = Callosal(j);
            var col = tag ? Rgb(78, 74, 90, 0.22f) : cal ? Rgb(86, 82, 96, 0.17f) : Rgb(90, 86, 98, 0.14f);
            // pęczek = 2 włókna blisko siebie (w DIC – delikatne prążki strefy pośredniej)
            for (int s = 0; s < 2; s++)
            {
                float off = (s - 0.5f) * 1.4f + (Hash2(j, s, Seed) - 0.5f) * 0.6f;
                int n = 0;
                for (float x = MathF.Floor(v.X0 / step) * step - step; x <= v.X1 + step; x += step)
                {
                    float al = FascAlpha(j, x);
                    var z = Cortex.At(x, by);
                    bool ok = al > 0.05f && z is not (Cortex.Z.Fissure or Cortex.Z.Septum or Cortex.Z.GW);
                    if (ok && n < b.Length) b[n++] = new Vector2(x, FascY(j, x) + off + 0.5f * MathF.Sin(x / 9 + s * 2 + j));
                    if ((!ok || x + step > v.X1 + step) && n > 1) { p.Thin(new ReadOnlySpan<Vector2>(b, 0, n), false, lw * (tag ? 1.2f : 1), col); n = 0; }
                    else if (!ok) n = 0;
                }
            }
        }
    }

    /// <summary>Naczynia: ściany ze śródbłonkiem, erytrocyty. W time-lapse (1 klatka ≈ kilka minut) krew przepływa między
    /// klatkami, więc w każdej klatce w świetle są inne krwinki – migoczą, zamiast płynąć.</summary>
    public void DrawVessels(Painter p, View v, float t)
    {
        int frame = (int)(t * 5);
        var wall = Rgb(110, 84, 82, 0.36f); var lum = Rgb(212, 190, 180, 0.2f); var halo = Rgb(255, 255, 240, 0.12f); var endo = Rgb(96, 84, 112, 0.42f);
        var buf = p.Buf2; var top = p.Shape; var bot = p.Shape2;
        void Tube(int n, float r, int salt)
        {
            if (n < 2) return;
            for (int s = 0; s < n; s++)
            {
                var d = buf[Math.Min(n - 1, s + 1)] - buf[Math.Max(0, s - 1)]; float l = Math.Max(d.Length(), 1e-4f);
                var nr = new Vector2(-d.Y / l, d.X / l); top[s] = buf[s] + nr * r; bot[s] = buf[s] - nr * r;
            }
            var c = new ReadOnlySpan<Vector2>(buf, 0, n);
            p.SoftStroke(c, false, r * 2 + 3, halo); p.Stroke(c, false, r * 2, lum);
            p.Stroke(new ReadOnlySpan<Vector2>(top, 0, n), false, 0.4f, wall); p.Stroke(new ReadOnlySpan<Vector2>(bot, 0, n), false, 0.4f, wall);
            // krwinki: ~co 12 µm, położenie losowane na nowo w każdej klatce time-lapse
            float len = 0; for (int s = 1; s < n; s++) len += (buf[s] - buf[s - 1]).Length();
            int nr0 = (int)(len / 12);
            for (int k = 0; k < nr0; k++)
            {
                float u = Hash2(k + salt * 131, frame, Seed) * (n - 1); int s = (int)u; float f = u - s;
                var q = buf[s] + (buf[Math.Min(n - 1, s + 1)] - buf[s]) * f; var d = buf[Math.Min(n - 1, s + 1)] - buf[s];
                if (!v.Vis(q.X, q.Y, 5)) continue;
                float ang = MathF.Atan2(d.Y, d.X), tilt = Hash2(k + 7, frame + salt, Seed);
                if (p.Tex != null) p.Tex.TexEllipse(q.X, q.Y, 3.6f, Math.Min(r * 0.85f, 3.6f * (0.35f + 0.65f * tilt)), ang + (tilt - 0.5f), q.X, q.Y, SpriteAtlas.Pad[SpriteAtlas.Rbc] * 0.5f, SpriteAtlas.Uv[SpriteAtlas.Rbc], new Color(1, 0.93f, 0.92f, 0.85f));
                else p.FillEllipse(q.X, q.Y, 3.6f, r * 0.8f, ang, Rgb(170, 110, 100, 0.5f));
            }
            // spłaszczone jądra śródbłonka
            for (int s = 2; s < n - 1; s += 5)
            {
                var d = buf[s + 1] - buf[s - 1]; float ang = MathF.Atan2(d.Y, d.X), sd = (s / 5) % 2 == 0 ? 1 : -1;
                var e = sd > 0 ? top[s] : bot[s];
                p.FillEllipse(e.X, e.Y, 3.2f, 0.8f, ang, endo);
            }
        }
        float step = Math.Max(3, v.Px * 10);
        // splot opony miękkiej
        if (v.Y0 < 0 && v.Y1 > -25)
        {
            int n = 0;
            for (float x = MathF.Floor(v.X0 / step) * step - step; x <= v.X1 + step && n < buf.Length; x += step)
            {
                if (!PialExists(x)) { Tube(n, PialR, 1); n = 0; continue; }
                buf[n++] = new Vector2(x, PialY(x));
            }
            Tube(n, PialR, 1);
        }
        // splot okołokomorowy
        if (v.Y0 < 370 && v.Y1 > 330)
        {
            int n = 0;
            for (float x = MathF.Floor(v.X0 / step) * step - step; x <= v.X1 + step && n < buf.Length; x += step)
            {
                if (!PlexExists(x)) { Tube(n, PlexR, 2); n = 0; continue; }
                buf[n++] = new Vector2(x, PlexY(x));
            }
            Tube(n, PlexR, 2);
        }
        // naczynia promieniste
        int k0 = (int)MathF.Floor(v.X0 / RVS) - 1, k1 = (int)MathF.Floor(v.X1 / RVS) + 1;
        for (int k = k0; k <= k1; k++)
        {
            if (!RadExists(k)) continue;
            float xb = RadBase(k); if (xb < v.X0 - 20 || xb > v.X1 + 20) continue;
            float ya = Math.Max(PialY(xb), v.Y0 - 10), yb = Math.Min(PlexY(xb), v.Y1 + 10);
            if (yb <= ya) continue;
            int n = 0;
            for (float y = ya; n < buf.Length; y += step) { float yy = Math.Min(y, yb); buf[n++] = new Vector2(RadX(k, yy), yy); if (yy >= yb) break; }
            Tube(n, RadR(k), 10 + k);
        }
        // naczynie w szczelinie międzypółkulowej
        if (v.X0 < Cortex.XM + 20 && v.X1 > Cortex.XM - 20 && v.Y0 < Cortex.Cc0)
        {
            int n = 0;
            for (float y = Math.Max(Cortex.MenT, v.Y0 - 10); y <= Math.Min(Cortex.Cc0 - 22, v.Y1 + 10) && n < buf.Length; y += step) buf[n++] = new Vector2(FisX(y), y);
            Tube(n, FisR, 3);
        }
    }

    /// <summary>Tło tkanki: lekko przyciemniona tkanka (skrawek), jasne światło komory i szczeliny, błona podstawna opony, powierzchnia komory z rzęskami.</summary>
    public void DrawGround(Painter p, View v)
    {
        float x0 = v.X0 - 20, x1 = v.X1 + 20;
        var tis = Rgb(118, 108, 124, 0.07f);
        if (v.Y1 > Cortex.Pia && v.Y0 < Cortex.VzB) { float a = Math.Max(v.Y0 - 10, Cortex.Pia), b = Math.Min(v.Y1 + 10, Cortex.VzB); p.Rect(x0, a, x1 - x0, b - a, tis); }
        if (v.Y1 > Cortex.VenB) { float a = Math.Max(v.Y0 - 10, Cortex.VenB); p.Rect(x0, a, x1 - x0, v.Y1 + 10 - a, tis); }
        if (v.Y1 > Cortex.MenT && v.Y0 < Cortex.Pia) { float a = Math.Max(v.Y0 - 10, Cortex.MenT); p.Rect(x0, a, x1 - x0, Cortex.Pia - a, Rgb(150, 138, 124, 0.06f)); }
        float lw = Math.Max(v.Px, 0.5f);
        // błona podstawna opony miękkiej (granica kory)
        if (v.Y0 < 3 && v.Y1 > -3)
        {
            p.Line(x0, 0, x1, 0, Math.Max(v.Px, 0.6f), Rgb(80, 72, 70, 0.5f));
            p.Line(x0, -0.8f, x1, -0.8f, 2.4f, Rgb(255, 255, 240, 0.12f));
        }
        // powierzchnia komory: szczytowe końce gleju promienistego z rzęskami pierwotnymi; dno komory – wyniosłość zwojowa
        foreach (float ys in new[] { Cortex.VzB, Cortex.VenB })
        {
            if (v.Y0 > ys + 4 || v.Y1 < ys - 4) continue;
            float sg = ys == Cortex.VzB ? 1 : -1;
            p.Line(x0, ys, x1, ys, lw, Rgb(84, 78, 88, 0.5f));
            if (v.Px < 0.25f)
                for (float x = MathF.Floor(x0 / 3.1f) * 3.1f; x < x1; x += 3.1f)
                {
                    float hsh = Hash2((int)(x * 10), (int)ys, Seed); if (hsh > 0.55f) continue;
                    p.Seg(x, ys, x + (hsh - 0.27f) * 3, ys + sg * (1.4f + hsh * 2), Math.Max(v.Px, 0.2f), Rgb(80, 76, 86, 0.4f));
                }
        }
        // linia środkowa: szczelina (światło opon), klin glejowy i szare pokrywające lekko jaśniejsze (glej)
        if (v.X0 < Cortex.XM + 200 && v.X1 > Cortex.XM - 200)
        {
            p.Rect(Cortex.XM - Cortex.FW - 6, Math.Max(v.Y0 - 10, Cortex.MenT), 2 * (Cortex.FW + 6), Cortex.Cc0 - 16 - Math.Max(v.Y0 - 10, Cortex.MenT), Rgb(150, 138, 124, 0.08f));
            p.Rect(Cortex.XM - 55, Cortex.Cc1, 110, Cortex.VenB + 140 - Cortex.Cc1, Rgb(120, 110, 124, 0.08f));
        }
    }
}

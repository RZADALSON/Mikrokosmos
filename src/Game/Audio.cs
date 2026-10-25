namespace Mikro;

/// <summary>
/// Proceduralny dźwięk (bez plików), stereo 44,1 kHz. Mikroorganizmy są w rzeczywistości bezgłośne,
/// więc brzmienie naśladuje nagrania hydrofonowe: spokojny szum wody i krótkie, "mokre" odgłosy zdarzeń.
/// Efekty nie są tonami – to szum przepuszczony przez rezonansowe filtry pasmowe (jak plusk, mlaśnięcie,
/// syk), z miękkim atakiem i szybkim wybrzmieniem. Częstość powtórzeń jest ograniczona.
/// </summary>
public partial class Audio : Node
{
    const int SR = 44100;
    AudioStreamPlayer _player;
    AudioStreamGeneratorPlayback _pb;
    public bool Muted;
    /// <summary>0 = woda (mikroskop świetlny), 1 = próżnia TEM, -1 = menu.</summary>
    public int Ambient = 0;
    float _amb, _t;

    /// <summary>Filtr stanowy (SVF, topologia TPT) – stabilny także przy przestrajaniu w każdej próbce.</summary>
    struct Svf
    {
        float _ic1, _ic2;
        public float Bp, Lp;
        public void Run(float x, float fc, float q)
        {
            float g = MathF.Tan(PI * Math.Min(fc, SR * 0.45f) / SR), k = 1 / q;
            float a1 = 1 / (1 + g * (g + k)), a2 = g * a1, a3 = g * a2;
            float v3 = x - _ic2, v1 = a1 * _ic1 + a2 * v3, v2 = _ic2 + a2 * _ic1 + a3 * v3;
            _ic1 = 2 * v1 - _ic1; _ic2 = 2 * v2 - _ic2;
            Bp = v1; Lp = v2;
        }
    }
    sealed class Voice
    {
        public string K; public float T, Dur, Vol, PanL, PanR, Seed, Ph, Ph2, Sys;
        public Svf F1, F2, F3, F4;
    }
    readonly List<Voice> _voices = new();
    readonly Dictionary<string, float> _last = new();
    Svf _aL, _aR, _aL2, _aR2, _oL, _oR;
    // szum różowy (filtr P. Kelleta, dokładność ±0,05 dB powyżej 9 Hz) i brązowy (całkowanie z upływem) – osobno dla L i P
    float _p0L, _p1L, _p2L, _p3L, _p4L, _p5L, _p6L, _p0R, _p1R, _p2R, _p3R, _p4R, _p5R, _p6R, _bL, _bR;
    float Pink(float w, ref float b0, ref float b1, ref float b2, ref float b3, ref float b4, ref float b5, ref float b6)
    {
        b0 = 0.99886f * b0 + w * 0.0555179f; b1 = 0.99332f * b1 + w * 0.0750759f; b2 = 0.96900f * b2 + w * 0.1538520f;
        b3 = 0.86650f * b3 + w * 0.3104856f; b4 = 0.55000f * b4 + w * 0.5329522f; b5 = -0.7616f * b5 - w * 0.0168980f;
        float o = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362f; b6 = w * 0.115926f;
        return o * 0.11f;
    }
    static float Brown(float w, ref float b) { b = (b + 0.02f * w) / 1.02f; return b * 3.5f; }
    float _drift = 1, _driftT;
    ulong _rng = 0x9E3779B97F4A7C15UL;
    readonly Vector2[] _buf = new Vector2[4096];

    float Wn() { _rng ^= _rng << 13; _rng ^= _rng >> 7; _rng ^= _rng << 17; return (float)((_rng >> 40) * (1.0 / 16777216.0)) * 2 - 1; }

    int _cap;
    /// <summary>
    /// Docelowe wypełnienie bufora (s): zatrzask ma nadążać za kółkiem myszy (~65 ms zamiast ~120 ms przy pełnym buforze),
    /// a zapas wciąż przetrwa pojedynczą długą klatkę bez przerwy w dźwięku.
    /// </summary>
    const float Lead = 0.065f;
    public override void _Ready()
    {
        var gen = new AudioStreamGenerator { MixRate = SR, BufferLength = 0.15f };
        _player = new AudioStreamPlayer { Stream = gen, VolumeDb = 0 };
        AddChild(_player);
        _player.Play();
        _pb = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
        _cap = _pb?.GetFramesAvailable() ?? 0;
    }

    /// <summary>Odtwarzanie generatora trzymane przez C# – zwolnione jawnie (inaczej Godot zgłasza wyciek przy wyjściu).</summary>
    public override void _ExitTree() { _player?.Stop(); _pb?.Dispose(); _pb = null; }

    /// <summary>Efekt dźwiękowy; pan: −1 lewo … 1 prawo.</summary>
    public void Play(string kind, float vol = 1, float pan = 0)
    {
        if (Muted || vol <= 0.03f) return;
        if (kind is "detent_in" or "detent_out" or "detent_stop") { Detent(kind == "detent_in" ? 1 : kind == "detent_out" ? -1 : 0, vol); return; }
        float gap = kind switch { "pop" => 0.12f, "zap" => 0.15f, "snap" => 0.12f, "gulp" => 0.25f, "net" => 0.8f, "hurt" => 0.3f, "divide" => 0.25f, "hatch" => 0.4f, "cyst" => 0.8f, _ => 0.05f };
        if (_last.TryGetValue(kind, out var lt) && _t - lt < gap) return;
        _last[kind] = _t;
        if (_voices.Count > 10) return;
        float dur = kind switch { "pop" => 0.25f, "gulp" => 0.4f, "zap" => 0.12f, "snap" => 0.15f, "tick" => 0.05f, "blip" => 0.6f, "net" => 1.1f, "goal" => 1.4f, "hurt" => 0.3f, "divide" => 0.6f, "hatch" => 0.45f, "cyst" => 0.9f, _ => 0.2f };
        float p = Clamp(pan, -1, 1) * 0.5f + 0.5f;
        _voices.Add(new Voice { K = kind, Dur = dur, Vol = Clamp(vol, 0, 1), PanL = MathF.Cos(p * PI / 2), PanR = MathF.Sin(p * PI / 2), Seed = Rand(0.85f, 1.15f) });
    }

    /// <summary>
    /// Uderzenie serca: S1 ("lub") i po czasie skurczu S2 ("dub"). Słyszane "od środka" tkanki – niskie, stłumione.
    /// S1 ~150 ms, energia 50–130 Hz; S2 ~120 ms, 75–200 Hz, krótszy i nieco cichszy.
    /// </summary>
    /// <param name="force">siła skurczu 0..1 (w gorączce i sepsie krążenie hiperdynamiczne – głośniejszy S1)</param>
    public void Heart(float vol, float systole, float force)
    {
        if (Muted || vol <= 0.01f) return;
        _voices.Add(new Voice { K = "heart", Dur = systole + 0.35f, Vol = Clamp(vol, 0, 1), PanL = 0.707f, PanR = 0.707f, Seed = Rand(0.96f, 1.04f), Sys = systole, Ph2 = force });
    }

    // ------------------------------------------------------------------ pokrętło powiększenia (osobny tor)
    /// <summary>
    /// Zatrzask metalowego pokrętła: kulka jedzie po grzbiecie między wycięciami (cichy, metaliczny szmer), wpada w wycięcie
    /// (krótki, twardy trzask ~1 ms) i dosiada (drugie, słabsze uderzenie po ~1,7 ms), a stalowe kółko dzwoni krótko
    /// kilkoma nieharmonicznymi modami (jak mała płytka: 1 : 1,47 : 2,09 : 2,76 : 3,52; tłumienie stali – dziesiątki ms);
    /// masa gałki daje głuche „tuk” ~200 Hz. Mody od ~3,9 kHz, wybrzmienie ~25 ms – krótki, jasny „tik”, a nie dzwoneczek. Mody liczone obracanymi wskazami (bez sin w każdej próbce).
    /// Kręcenie w drugą stronę: kulka uderza w przeciwne zbocze – brzmienie o włos niższe. Ogranicznik: ciężkie, głuche „klak”.
    /// Tor omija filtr wyjściowy 8 kHz (metaliczny połysk jest wyżej) i limit głosów – szybkie kręcenie daje serię trzasków
    /// co ≥ 24 ms, jak grzechotka, zamiast gubić zatrzaski.
    /// </summary>
    sealed class Click
    {
        public float T, Delay, Amp, Pre, Thump, ThumpF, ThumpT, NoiseF, NoiseA, Settle, Dur;
        public readonly float[] Re = new float[5], Im = new float[5], Cr = new float[5], Ci = new float[5], A = new float[5];
        public Svf F1, F2;
    }
    static readonly float[] ModeR = { 1f, 1.47f, 2.09f, 2.76f, 3.52f }, ModeA = { 1f, 0.72f, 0.52f, 0.34f, 0.2f };
    readonly List<Click> _clicks = new();
    float _clickNext;
    /// <summary>Zatrzask pokrętła powiększenia: dir +1 – przybliżanie, −1 – oddalanie, 0 – skrajne położenie (ogranicznik).</summary>
    public void Detent(int dir, float vol = 0.9f)
    {
        if (Muted || vol <= 0.03f) return;
        float start = Math.Max(_t + 0.002f, _clickNext);
        if (start - _t > 0.15f || _clicks.Count > 8) return; // kółko kręcone szybciej, niż zatrzaski nadążają – bez zaległości
        _clickNext = start + (dir == 0 ? 0.08f : 0.024f);
        bool stop = dir == 0;
        float pitch = (dir < 0 ? 0.965f : 1f) * Rand(0.975f, 1.025f), f0 = (stop ? 1900 : 3900) * pitch;
        var c = new Click
        {
            Delay = start - _t, Amp = Clamp(vol, 0, 1) * Rand(0.9f, 1.05f), Pre = stop ? 0 : Rand(0.6f, 1f),
            Thump = stop ? 0.9f : 0.3f, ThumpF = (stop ? 150 : 205) * Rand(0.95f, 1.05f), ThumpT = stop ? 0.016f : 0.006f,
            NoiseF = (stop ? 3000 : 5600) * pitch, NoiseA = stop ? 1.1f : 1.6f, Settle = stop ? 0 : 0.45f, Dur = stop ? 0.09f : 0.075f,
        };
        float dt = 1f / SR;
        for (int k = 0; k < 5; k++)
        {
            float f = f0 * ModeR[k] * Rand(0.99f, 1.01f), tau = (stop ? 0.014f : 0.024f) / (1 + 0.5f * k);
            float g = MathF.Exp(-dt / tau), w = TAU * f * dt, ph = Rand(0, TAU);
            c.Cr[k] = g * MathF.Cos(w); c.Ci[k] = g * MathF.Sin(w);
            c.Re[k] = MathF.Cos(ph); c.Im[k] = MathF.Sin(ph);
            c.A[k] = ModeA[k] * (stop ? 0.55f : 1f);
        }
        _clicks.Add(c);
    }
    float ClickSample(Click c, float white)
    {
        float t = c.T - c.Delay, o = 0;
        if (t < 0)
        {
            // kulka na grzbiecie: narastający, cichy szmer stali o stal (tylko przy obrocie, nie przy ograniczniku)
            float k = 1 + t / 0.014f;
            if (k > 0 && c.Pre > 0) { c.F1.Run(white, 5400, 2.2f); o = c.F1.Bp * k * k * 0.07f * c.Pre; }
            return o * c.Amp;
        }
        // trzask wpadnięcia w wycięcie i dosiadu kulki
        c.F2.Run(white, c.NoiseF, 1.2f);
        float hit = Env(t, 0.0003f, 0.0012f) + c.Settle * Env(t - 0.0017f, 0.0003f, 0.0009f);
        o += c.F2.Bp * hit * c.NoiseA;
        // dzwonienie stalowego kółka (obracane wskazy z tłumieniem)
        float att = Math.Min(1, t / 0.0003f), ring = 0;
        for (int k = 0; k < 5; k++)
        {
            float re = c.Re[k], im = c.Im[k];
            c.Re[k] = re * c.Cr[k] - im * c.Ci[k]; c.Im[k] = re * c.Ci[k] + im * c.Cr[k];
            ring += c.Im[k] * c.A[k];
        }
        o += ring * att * 0.3f;
        // masa gałki
        o += MathF.Sin(TAU * c.ThumpF * t) * MathF.Exp(-t / c.ThumpT) * att * c.Thump * 0.5f;
        return o * c.Amp;
    }

    static readonly float[] Arp = { 523.3f, 659.3f, 784f };
    // obwiednia z miękkim atakiem (bez trzasków)
    static float Env(float t, float attack, float decay) => t < 0 ? 0 : (t < attack ? t / attack : 1) * MathF.Exp(-Math.Max(0, t - attack) / decay);

    float VoiceSample(Voice v, float white, float dt)
    {
        float t = v.T, s = v.Seed, o = 0;
        switch (v.K)
        {
            case "pop":   // pęknięcie błony: krótkie, mokre "plip" (rezonans opada) + głuchy dół
            {
                v.F1.Run(white, (900 - 520 * Math.Min(1, t / 0.08f)) * s, 4.5f);
                v.F2.Run(white, 140, 0.7f);
                o = v.F1.Bp * Env(t, 0.005f, 0.045f) * 1.8f + v.F2.Lp * Env(t, 0.006f, 0.06f) * 1.4f;
                break;
            }
            case "gulp":  // połknięcie: dwa niskie mlaśnięcia
            {
                v.F1.Run(white, (330 - 150 * Math.Min(1, t / 0.15f)) * s, 5);
                float t2 = t - 0.15f;
                v.F2.Run(white, (270 - 110 * Math.Clamp(t2 / 0.15f, 0, 1)) * s, 5);
                o = v.F1.Bp * Env(t, 0.015f, 0.07f) * 2.6f + v.F2.Bp * Env(t2, 0.015f, 0.07f) * 2.0f;
                break;
            }
            case "zap":   // trichocysty/toksycysty: bardzo krótki, miękki syk
            {
                v.F1.Run(white, 1800 * s, 2.5f);
                o = v.F1.Bp * Env(t, 0.005f, 0.025f) * 0.8f;
                break;
            }
            case "snap":  // błyskawiczny skurcz: głuche "tup"
            {
                v.F1.Run(white, 160 * s, 2.2f);
                o = v.F1.Bp * Env(t, 0.004f, 0.04f) * 3.2f;
                break;
            }
            case "tick":  // interfejs: cichy klik
            {
                v.F1.Run(white, 1300, 4);
                o = v.F1.Bp * Env(t, 0.002f, 0.01f) * 0.9f;
                break;
            }
            case "hurt":  // obrażenie / schwytanie: tępe uderzenie
            {
                v.F1.Run(white, 110, 3);
                v.F2.Run(white, 220, 0.8f);
                o = v.F1.Bp * Env(t, 0.006f, 0.1f) * 3.5f + v.F2.Lp * Env(t, 0.004f, 0.05f) * 1.2f;
                break;
            }
            case "net":   // NEToza: narastający szum wyrzucanej sieci
            {
                v.F1.Run(white, 300 + 1100 * Math.Min(1, t / 0.5f), 1.3f);
                o = v.F1.Bp * 1.2f * MathF.Sin(PI * Math.Min(1, t / v.Dur));
                break;
            }
            case "blip":  // podział: dwa miękkie tony marimby (zdarzenie "dobre")
            {
                v.Ph += TAU * 523.3f * dt; v.Ph2 += TAU * 659.3f * dt;
                o = (MathF.Sin(v.Ph) + 0.12f * MathF.Sin(v.Ph * 4)) * Env(t, 0.005f, 0.16f) * 0.22f
                  + (MathF.Sin(v.Ph2) + 0.12f * MathF.Sin(v.Ph2 * 4)) * Env(t - 0.12f, 0.005f, 0.18f) * 0.22f;
                break;
            }
            case "divide": // podział komórki: błona rozciąga się (narastające, mokre „uuu”), potem miękkie rozejście się córek
            {
                v.F1.Run(white, (170 + 170 * Math.Min(1, t / 0.3f)) * s, 7);
                float t2 = t - 0.3f;
                v.F2.Run(white, 620 * s, 5);
                v.F3.Run(white, 120, 0.8f);
                o = v.F1.Bp * Env(t, 0.09f, 0.11f) * 2.4f + v.F2.Bp * Env(t2, 0.004f, 0.05f) * 2.0f + v.F3.Lp * Env(t2, 0.006f, 0.07f) * 1.2f;
                break;
            }
            case "hatch":  // wyklucie: kilka drobnych, suchych trzasków skorupki
            {
                for (int i = 0; i < 3; i++)
                {
                    float ti = t - i * (0.08f + 0.03f * i) * s; if (ti < 0) continue;
                    if (i == 0) v.F1.Run(white, 1900 * s, 5); else if (i == 1) v.F2.Run(white, 1500 * s, 5); else v.F3.Run(white, 2300 * s, 5);
                    float bp = i == 0 ? v.F1.Bp : i == 1 ? v.F2.Bp : v.F3.Bp;
                    o += bp * Env(ti, 0.004f, 0.02f) * 1.6f;
                }
                break;
            }
            case "cyst":   // encystacja: opadający, głuchy dźwięk zamykającej się ściany
            {
                v.F1.Run(white, (420 - 300 * Math.Min(1, t / 0.5f)) * s, 3);
                v.F2.Run(white, 90, 0.8f);
                o = v.F1.Bp * Env(t, 0.03f, 0.22f) * 2.2f + v.F2.Lp * Env(t, 0.01f, 0.12f) * 1.6f;
                break;
            }
            case "heart":
            {
                float force = v.Ph2;
                // S1: niski, dłuższy – zamknięcie zastawek mitralnej i trójdzielnej (dwie składowe blisko siebie)
                float e1 = Env(t, 0.014f, 0.05f) + 0.55f * Env(t - 0.03f, 0.01f, 0.045f);
                v.F1.Run(white, 78 * s, 2.2f);
                float f1 = (52 - 16 * Math.Min(1, t / 0.12f)) * s;
                v.Ph += TAU * f1 * dt;
                float s1 = (MathF.Sin(v.Ph) * 0.9f + v.F1.Bp * 2.6f) * e1 * (0.8f + 0.5f * force);
                // S2: krótszy, wyższy – zamknięcie zastawek aortalnej i płucnej
                float t2 = t - v.Sys;
                v.F2.Run(white, 125 * s, 2.6f);
                float e2 = Env(t2, 0.006f, 0.032f);
                float s2 = t2 < 0 ? 0 : (MathF.Sin(TAU * (78 - 20 * Math.Clamp(t2 / 0.08f, 0, 1)) * s * t2) * 0.7f + v.F2.Bp * 2.2f) * e2 * 1.0f;
                // tkanka i klatka piersiowa tłumią wysokie tony: dwa stopnie filtra dolnoprzepustowego ~170 Hz
                v.F3.Run(s1 + s2, 170, 0.7f); v.F4.Run(v.F3.Lp, 190, 0.7f);
                o = MathF.Tanh(v.F4.Lp * 1.6f) * 1.2f;
                break;
            }
            case "goal":  // cel: łagodne arpeggio (C–E–G)
            {
                for (int i = 0; i < 3; i++)
                {
                    float ti = t - i * 0.14f; if (ti < 0) continue;
                    float ph = TAU * Arp[i] * ti;
                    o += (MathF.Sin(ph) + 0.1f * MathF.Sin(ph * 4)) * Env(ti, 0.005f, 0.32f) * 0.17f;
                }
                break;
            }
        }
        return o * v.Vol * Gain(v.K);
    }
    /// <summary>
    /// Wyrównanie głośności efektów (zmierzone przez --audio przy pełnej głośności): krótkie zdarzenia ~−8 dBFS szczytu,
    /// melodyjne sygnały (podział, etap) ~−9 dBFS, klik interfejsu ciszej (~−14 dBFS), serce ~−8 dBFS.
    /// </summary>
    static float Gain(string k) => k switch
    {
        "pop" => 0.45f, "gulp" => 0.44f, "zap" => 1f, "snap" => 1f, "tick" => 0.63f, "hurt" => 0.74f, "net" => 0.74f,
        "blip" => 1.55f, "goal" => 1.5f, "divide" => 0.55f, "hatch" => 0.24f, "cyst" => 0.53f, "heart" => 0.75f, _ => 1f,
    };

    public override void _Process(double delta)
    {
        if (_pb == null) return;
        int n = _pb.GetFramesAvailable();
        if (n <= 0) return;
        // bufor napełniany tylko do ~50 ms naprzód (mniejsze opóźnienie efektów), a nie do końca pojemności
        if (_cap > 0) { int queued = Math.Max(0, _cap - n); n = Math.Min(n, Math.Max(0, (int)(Lead * SR) - queued)); if (n <= 0) return; }
        n = Math.Min(n, _buf.Length);
        Mix(_buf, n);
        _pb.PushBuffer(new ReadOnlySpan<Vector2>(_buf, 0, n));
    }

    /// <summary>Głośność zatrzasków (zmierzona przez --audio): szczyt ~−13 dBFS przy pełnej głośności – cichy, wyraźny klik.</summary>
    const float ClickGain = 0.42f;
    public void Mix(Vector2[] buf, int n)
    {
        float dt = 1f / SR;
        float master = Muted ? 0 : Settings.Master;
        int noise = Settings.Noise;
        // szum "do skupienia" jest stały: bez wahań i bez ściszania w menu (zmiany głośności przyciągają uwagę)
        float target = Ambient == -2 ? 0 : noise == 0 && Ambient < 0 ? 0.6f : 1;
        float ambVol = Settings.Ambient * master, sfxVol = Settings.Sfx * master;
        float cut = Ambient == 1 ? 220 : 600, gainA = Ambient == 1 ? 0.5f : 0.55f;
        for (int i = 0; i < n; i++)
        {
            _t += dt;
            _amb += (target - _amb) * 0.00005f;
            // tło: stały, miękki szum wody (dwa niezależne kanały → szerokość stereo), bardzo powolne wahania
            _driftT -= dt; if (_driftT <= 0) { _driftT = 0.5f; _drift = Clamp(_drift + Rand(-0.04f, 0.04f), 0.85f, 1.1f); }
            float wl = Wn(), wr = Wn();
            _aL.Run(wl, cut, 0.6f); _aR.Run(wr, cut, 0.6f);
            _aL2.Run(_aL.Lp, cut * 0.7f, 0.6f); _aR2.Run(_aR.Lp, cut * 0.7f, 0.6f);
            float L = 0, R = 0, nL = 0, nR = 0;
            if (noise == 0) { float k = gainA * _drift * _amb * ambVol; L = _aL2.Lp * k; R = _aR2.Lp * k; }
            else
            {
                // głośności dobrane tak, by trzy barwy brzmiały podobnie głośno (RMS zmierzony przez --audio)
                float k = _amb * ambVol;
                if (noise == 1) { nL = wl * 0.075f * k; nR = wr * 0.075f * k; }
                else if (noise == 2) { nL = Pink(wl, ref _p0L, ref _p1L, ref _p2L, ref _p3L, ref _p4L, ref _p5L, ref _p6L) * 0.25f * k; nR = Pink(wr, ref _p0R, ref _p1R, ref _p2R, ref _p3R, ref _p4R, ref _p5R, ref _p6R) * 0.25f * k; }
                else { nL = Brown(wl, ref _bL) * 0.45f * k; nR = Brown(wr, ref _bR) * 0.45f * k; }
            }
            for (int j = _voices.Count - 1; j >= 0; j--)
            {
                var v = _voices[j];
                float o = VoiceSample(v, wl, dt) * (v.K == "heart" ? ambVol * 1.2f : sfxVol);
                L += o * v.PanL; R += o * v.PanR;
                v.T += dt;
                if (v.T >= v.Dur) _voices.RemoveAt(j);
            }
            _oL.Run(L, 8000, 0.7f); _oR.Run(R, 8000, 0.7f);
            // zatrzaski pokrętła – za filtrem 8 kHz (metaliczny połysk), środek sceny stereo
            float ck = 0;
            for (int j = _clicks.Count - 1; j >= 0; j--)
            {
                var c = _clicks[j];
                ck += ClickSample(c, wr);
                c.T += dt;
                if (c.T - c.Delay >= c.Dur) _clicks.RemoveAt(j);
            }
            ck *= sfxVol * ClickGain;
            // szum dodawany za filtrem wyjściowym 8 kHz – biały ma płaskie widmo w całym paśmie słyszalnym
            buf[i] = new Vector2(MathF.Tanh(_oL.Lp * 1.1f + nL + ck) * 0.9f, MathF.Tanh(_oR.Lp * 1.1f + nR + ck) * 0.9f);
        }
    }

    /// <summary>Test: renderuje każdy efekt do WAV i wypisuje poziomy (szczyt, RMS, największy skok próbki, "jasność").</summary>
    public static void Report(string outDir)
    {
        var a = new Audio();
        // pomiar przy pełnej głośności (niezależnie od zapisanych ustawień gracza)
        float m0 = Settings.Master, s0 = Settings.Sfx, a0 = Settings.Ambient; bool mu = Settings.Muted; int n0 = Settings.Noise;
        Settings.Master = Settings.Sfx = Settings.Ambient = 1; Settings.Muted = false;
        foreach (var k in new[] { "ambient", "white", "pink", "brown", "tem", "detent_in", "detent_out", "detent_stop", "detent_run", "pop", "gulp", "zap", "snap", "tick", "hurt", "net", "blip", "goal", "divide", "hatch", "cyst", "heart", "heart140" })
        {
            a._voices.Clear(); a._last.Clear(); a._clicks.Clear(); a._t = 10; a._clickNext = 0;
            Settings.Noise = k switch { "white" => 1, "pink" => 2, "brown" => 3, _ => 0 };
            bool amb = k is "ambient" or "tem" or "white" or "pink" or "brown";
            a.Ambient = k == "tem" ? 1 : amb ? 0 : -2; a._amb = amb ? 1 : 0;
            if (k == "heart") a.Heart(0.7f, 0.3f, 0);
            else if (k == "heart140") a.Heart(1f, 0.18f, 1);
            else if (k == "detent_run") { a.Detent(1, 1); a.Detent(1, 1); a.Detent(1, 1); a.Detent(-1, 1); a.Detent(-1, 1); }
            else if (a.Ambient == -2) a.Play(k, 1, 0);
            int n = SR * (a.Ambient >= 0 ? 4 : 2); var buf = new Vector2[n];
            for (int o = 0; o < n; o += 4096) { var tmp = new Vector2[Math.Min(4096, n - o)]; a.Mix(tmp, tmp.Length); Array.Copy(tmp, 0, buf, o, tmp.Length); }
            double peak = 0, rms = 0, jump = 0, zc = 0;
            for (int i = 0; i < n; i++) { double s = buf[i].X; peak = Math.Max(peak, Math.Abs(s)); rms += s * s; if (i > 0) { jump = Math.Max(jump, Math.Abs(s - buf[i - 1].X)); if (Math.Sign(s) != Math.Sign(buf[i - 1].X)) zc++; } }
            rms = Math.Sqrt(rms / n);
            GD.Print($"[audio] {k,-8} szczyt {20 * Math.Log10(peak + 1e-9):0.0} dBFS · RMS {20 * Math.Log10(rms + 1e-9):0.0} dBFS · maks. skok {jump:0.000} · przejścia przez zero {zc / (n / (double)SR):0}/s");
            using var f = System.IO.File.Create(System.IO.Path.Combine(outDir, $"sfx_{k}.wav"));
            using var w = new System.IO.BinaryWriter(f);
            w.Write("RIFF"u8.ToArray()); w.Write(36 + n * 4); w.Write("WAVE"u8.ToArray()); w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(SR); w.Write(SR * 4); w.Write((short)4); w.Write((short)16);
            w.Write("data"u8.ToArray()); w.Write(n * 4);
            for (int i = 0; i < n; i++) { w.Write((short)(Clamp(buf[i].X, -1, 1) * 32767)); w.Write((short)(Clamp(buf[i].Y, -1, 1) * 32767)); }
        }
        Settings.Master = m0; Settings.Sfx = s0; Settings.Ambient = a0; Settings.Muted = mu; Settings.Noise = n0;
        a.Free();
    }
}

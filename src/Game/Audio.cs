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
        public string K; public float T, Dur, Vol, PanL, PanR, Seed, Ph, Ph2;
        public Svf F1, F2;
    }
    readonly List<Voice> _voices = new();
    readonly Dictionary<string, float> _last = new();
    Svf _aL, _aR, _aL2, _aR2, _oL, _oR;
    float _drift = 1, _driftT;
    ulong _rng = 0x9E3779B97F4A7C15UL;
    readonly Vector2[] _buf = new Vector2[4096];

    float Wn() { _rng ^= _rng << 13; _rng ^= _rng >> 7; _rng ^= _rng << 17; return (float)((_rng >> 40) * (1.0 / 16777216.0)) * 2 - 1; }

    public override void _Ready()
    {
        var gen = new AudioStreamGenerator { MixRate = SR, BufferLength = 0.12f };
        _player = new AudioStreamPlayer { Stream = gen, VolumeDb = 0 };
        AddChild(_player);
        _player.Play();
        _pb = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    /// <summary>Efekt dźwiękowy; pan: −1 lewo … 1 prawo.</summary>
    public void Play(string kind, float vol = 1, float pan = 0)
    {
        if (Muted || vol <= 0.03f) return;
        float gap = kind switch { "pop" => 0.12f, "zap" => 0.15f, "snap" => 0.12f, "gulp" => 0.25f, "net" => 0.8f, "hurt" => 0.3f, _ => 0.05f };
        if (_last.TryGetValue(kind, out var lt) && _t - lt < gap) return;
        _last[kind] = _t;
        if (_voices.Count > 10) return;
        float dur = kind switch { "pop" => 0.25f, "gulp" => 0.4f, "zap" => 0.12f, "snap" => 0.15f, "tick" => 0.05f, "blip" => 0.6f, "net" => 1.1f, "goal" => 1.4f, "hurt" => 0.3f, _ => 0.2f };
        float p = Clamp(pan, -1, 1) * 0.5f + 0.5f;
        _voices.Add(new Voice { K = kind, Dur = dur, Vol = Clamp(vol, 0, 1), PanL = MathF.Cos(p * PI / 2), PanR = MathF.Sin(p * PI / 2), Seed = Rand(0.85f, 1.15f) });
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
        return o * v.Vol;
    }

    public override void _Process(double delta)
    {
        if (_pb == null) return;
        int n = _pb.GetFramesAvailable();
        if (n <= 0) return;
        n = Math.Min(n, _buf.Length);
        Mix(_buf, n);
        _pb.PushBuffer(new ReadOnlySpan<Vector2>(_buf, 0, n));
    }

    public void Mix(Vector2[] buf, int n)
    {
        float dt = 1f / SR;
        float master = Muted ? 0 : Settings.Master;
        float target = Ambient == -2 ? 0 : Ambient < 0 ? 0.6f : 1;
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
            float k = gainA * _drift * _amb * ambVol;
            float L = _aL2.Lp * k, R = _aR2.Lp * k;
            for (int j = _voices.Count - 1; j >= 0; j--)
            {
                var v = _voices[j];
                float o = VoiceSample(v, wl, dt) * sfxVol * 0.5f;
                L += o * v.PanL; R += o * v.PanR;
                v.T += dt;
                if (v.T >= v.Dur) _voices.RemoveAt(j);
            }
            _oL.Run(L, 8000, 0.7f); _oR.Run(R, 8000, 0.7f);
            buf[i] = new Vector2(MathF.Tanh(_oL.Lp * 1.1f) * 0.9f, MathF.Tanh(_oR.Lp * 1.1f) * 0.9f);
        }
    }

    /// <summary>Test: renderuje każdy efekt do WAV i wypisuje poziomy (szczyt, RMS, największy skok próbki, "jasność").</summary>
    public static void Report(string outDir)
    {
        var a = new Audio();
        foreach (var k in new[] { "ambient", "tem", "pop", "gulp", "zap", "snap", "tick", "hurt", "net", "blip", "goal" })
        {
            a._voices.Clear(); a._last.Clear(); a._t = 10;
            a.Ambient = k == "ambient" ? 0 : k == "tem" ? 1 : -2; a._amb = k is "ambient" or "tem" ? 1 : 0;
            if (a.Ambient == -2) a.Play(k, 1, 0);
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
    }
}

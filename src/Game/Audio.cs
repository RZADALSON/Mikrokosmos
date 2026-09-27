namespace Mikro;

/// <summary>
/// Proceduralny dźwięk (bez plików), stereo 44,1 kHz. Mikroorganizmy są w rzeczywistości bezgłośne,
/// więc brzmienie naśladuje nagrania hydrofonowe i dokumenty przyrodnicze: miękki szum wody,
/// bąbelki, stłumione "puknięcia" pękających błon. Każdy efekt ma łagodny atak (brak trzasków),
/// jest filtrowany dolnoprzepustowo i ograniczony częstością (seria lizy nie brzmi jak karabin).
/// </summary>
public partial class Audio : Node
{
    const int SR = 44100;
    AudioStreamPlayer _player;
    AudioStreamGeneratorPlayback _pb;
    public bool Muted;
    /// <summary>0 = woda (mikroskop świetlny), 1 = próżnia TEM, -1 = menu.</summary>
    public int Ambient = 0;
    float _amb, _t, _bubbleT = 2;

    sealed class Voice
    {
        public string K; public float T, Dur, Vol, PanL, PanR, F, Ph, Ph2, Lp, Lp2, Hp, Seed, Delay;
    }
    readonly List<Voice> _voices = new();
    readonly Dictionary<string, float> _last = new();
    // tło: dwa niezależne kanały szumu (szerokość stereo)
    float _bL, _bR, _lL, _lR, _l2L, _l2R, _mL, _mR, _swell;
    ulong _rng = 0x9E3779B97F4A7C15UL;
    Vector2[] _buf = new Vector2[4096];

    float Wn() { _rng ^= _rng << 13; _rng ^= _rng >> 7; _rng ^= _rng << 17; return (float)((_rng >> 40) * (1.0 / 16777216.0)) * 2 - 1; }
    static float Lp(float cutoff) => 1 - MathF.Exp(-TAU * cutoff / SR);

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
        if (Muted || vol <= 0.02f) return;
        float gap = kind switch { "pop" => 0.07f, "zap" => 0.1f, "snap" => 0.09f, "gulp" => 0.15f, "net" => 0.5f, _ => 0.03f };
        if (_last.TryGetValue(kind, out var lt) && _t - lt < gap) return;
        _last[kind] = _t;
        if (_voices.Count > 14) return;
        float dur = kind switch { "pop" => 0.45f, "gulp" => 0.45f, "zap" => 0.22f, "snap" => 0.2f, "tick" => 0.06f, "blip" => 0.55f, "net" => 1.2f, "goal" => 1.3f, "hurt" => 0.3f, "bubble" => 0.25f, _ => 0.2f };
        float p = Clamp(pan, -1, 1) * 0.5f + 0.5f;
        float seed = Rand(0.85f, 1.18f);
        _voices.Add(new Voice
        {
            K = kind, Dur = dur, Vol = Clamp(vol, 0, 1), PanL = MathF.Cos(p * PI / 2), PanR = MathF.Sin(p * PI / 2), Seed = seed,
            F = kind switch { "pop" => Rand(170, 300), "bubble" => Rand(280, 700), "gulp" => Rand(95, 125), "snap" => Rand(55, 75), _ => 0 },
        });
    }

    // obwiednia z miękkim atakiem (bez trzasków)
    static readonly float[] Arp = { 523.3f, 659.3f, 784f };
    static float Env(float t, float attack, float decay) => (t < attack ? t / attack : 1) * MathF.Exp(-Math.Max(0, t - attack) / decay);

    float VoiceSample(Voice v, float white, float dt)
    {
        float t = v.T - v.Delay; if (t < 0) return 0;
        float o = 0;
        switch (v.K)
        {
            case "pop":   // pęknięcie błony: bąbel (ton rośnie, gdy się kurczy) + miękki plusk
            case "bubble":
            {
                float e = Env(t, 0.006f, v.K == "pop" ? 0.07f : 0.05f);
                v.Ph += TAU * v.F * (1 + 1.6f * t / v.Dur) * dt;
                v.Lp += (white - v.Lp) * Lp(900);
                v.Lp2 += (v.Lp - v.Lp2) * Lp(900);
                float splash = v.K == "pop" ? v.Lp2 * 2.2f * Env(t, 0.01f, 0.12f) : 0;
                o = MathF.Sin(v.Ph) * e * 0.55f + splash;
                break;
            }
            case "gulp":  // połknięcie: dwa niskie "glup"
            {
                float t2 = t > 0.16f ? t - 0.16f : -1;
                v.Ph += TAU * v.F * (1 + 0.5f * Math.Min(t, 0.16f) / 0.16f) * dt;
                v.Ph2 += TAU * v.F * 0.8f * (1 + 0.5f * Math.Max(0, t2) / 0.16f) * dt;
                o = MathF.Sin(v.Ph) * Env(t, 0.012f, 0.06f) * 0.6f + (t2 >= 0 ? MathF.Sin(v.Ph2) * Env(t2, 0.012f, 0.07f) * 0.5f : 0);
                break;
            }
            case "zap":   // wystrzał trichocyst/toksycyst: krótki, stłumiony syk
            {
                v.Lp += (white - v.Lp) * Lp(2200);
                v.Hp += (v.Lp - v.Hp) * Lp(500);
                o = (v.Lp - v.Hp) * Env(t, 0.008f, 0.05f) * 1.1f;
                break;
            }
            case "snap":  // błyskawiczny skurcz: głuche "tup"
            {
                v.Ph += TAU * v.F * (1 - 0.35f * t / v.Dur) * dt;
                v.Lp += (white - v.Lp) * Lp(300);
                o = (MathF.Sin(v.Ph) * 0.7f + v.Lp * 1.5f) * Env(t, 0.005f, 0.05f);
                break;
            }
            case "tick":  // interfejs: cichy, drewniany klik
            {
                v.Ph += TAU * 1150 * dt; v.Ph2 += TAU * 2870 * dt;
                o = (MathF.Sin(v.Ph) + 0.3f * MathF.Sin(v.Ph2)) * Env(t, 0.002f, 0.012f) * 0.25f;
                break;
            }
            case "blip":  // podział: dwa miękkie tony marimby
            {
                v.Ph += TAU * 523.3f * dt; v.Ph2 += TAU * 659.3f * dt;
                float a = Env(t, 0.004f, 0.18f), b = t > 0.11f ? Env(t - 0.11f, 0.004f, 0.2f) : 0;
                o = (MathF.Sin(v.Ph) + 0.15f * MathF.Sin(v.Ph * 4)) * a * 0.3f + (MathF.Sin(v.Ph2) + 0.15f * MathF.Sin(v.Ph2 * 4)) * b * 0.3f;
                break;
            }
            case "goal":  // cel: łagodne arpeggio (C–E–G)
            {
                var f = Arp;
                for (int i = 0; i < 3; i++)
                {
                    float ti = t - i * 0.13f; if (ti < 0) continue;
                    float ph = TAU * f[i] * ti;
                    o += (MathF.Sin(ph) + 0.12f * MathF.Sin(ph * 4)) * Env(ti, 0.004f, 0.35f) * 0.22f;
                }
                break;
            }
            case "net":   // NEToza: narastający szum wyrzucanej sieci
            {
                float cut = 250 + 900 * Math.Min(1, t / 0.4f);
                v.Lp += (white - v.Lp) * Lp(cut);
                o = v.Lp * 1.4f * MathF.Sin(PI * Math.Min(1, t / v.Dur));
                break;
            }
            case "hurt":
            {
                v.Ph += TAU * 70 * dt;
                o = MathF.Sin(v.Ph) * Env(t, 0.01f, 0.08f) * 0.6f;
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
        // odległe bąbelki w wodzie (tylko mikroskop świetlny, rzadko i cicho)
        if (Ambient == 0 && !Muted)
        {
            _bubbleT -= (float)delta;
            if (_bubbleT <= 0) { _bubbleT = Rand(2.5f, 7f); Play("bubble", Rand(0.08f, 0.2f) * Settings.Ambient / Math.Max(0.01f, Settings.Sfx), Rand(-0.8f, 0.8f)); }
        }
        _pb.PushBuffer(new ReadOnlySpan<Vector2>(_buf, 0, n));
    }

    public void Mix(Vector2[] buf, int n)
    {
        float dt = 1f / SR;
        float master = Muted ? 0 : Settings.Master;
        float target = Ambient < 0 ? 0.6f : 1;
        float ambVol = Settings.Ambient * master, sfxVol = Settings.Sfx * master;
        float kLow = Lp(Ambient == 1 ? 140 : 380), kLow2 = Lp(Ambient == 1 ? 90 : 220), kOut = Lp(7000);
        for (int i = 0; i < n; i++)
        {
            _t += dt;
            _amb += (target - _amb) * 0.00005f;
            // tło: szum brązowy → 2× dolnoprzepustowy, powolne "falowanie" głośności
            float wl = Wn(), wr = Wn();
            _bL = (_bL + wl * 0.02f) * 0.999f; _bR = (_bR + wr * 0.02f) * 0.999f;
            _lL += (_bL - _lL) * kLow; _lR += (_bR - _lR) * kLow;
            _l2L += (_lL - _l2L) * kLow2; _l2R += (_lR - _l2R) * kLow2;
            _swell = 0.75f + 0.25f * MathF.Sin(_t * 0.21f) * MathF.Sin(_t * 0.057f + 1);
            float pump = Ambient == 1 ? 0.8f + 0.2f * MathF.Sin(_t * TAU * 1.3f) : 1;
            float aL = _l2L * 0.45f * _swell * pump * _amb * ambVol, aR = _l2R * 0.45f * _swell * pump * _amb * ambVol;
            float sL = 0, sR = 0;
            for (int k = _voices.Count - 1; k >= 0; k--)
            {
                var v = _voices[k];
                float o = VoiceSample(v, wl, dt) * sfxVol * 0.5f;
                sL += o * v.PanL; sR += o * v.PanR;
                v.T += dt;
                if (v.T >= v.Dur + v.Delay) _voices.RemoveAt(k);
            }
            float L = aL + sL, R = aR + sR;
            _mL += (L - _mL) * kOut; _mR += (R - _mR) * kOut;
            // miękki ogranicznik
            buf[i] = new Vector2(MathF.Tanh(_mL * 1.2f) * 0.85f, MathF.Tanh(_mR * 1.2f) * 0.85f);
        }
    }

    /// <summary>Test: renderuje każdy efekt do WAV i wypisuje poziomy (szczyt, RMS, największy skok próbki, "jasność").</summary>
    public static void Report(string outDir)
    {
        var a = new Audio(); a.Ambient = 0;
        foreach (var k in new[] { "ambient", "pop", "bubble", "gulp", "zap", "snap", "tick", "blip", "goal", "net", "hurt" })
        {
            a._voices.Clear(); a._last.Clear(); a.Ambient = k == "ambient" ? 0 : -2; a._amb = k == "ambient" ? 1 : 0;
            if (k != "ambient") a.Play(k, 1, 0);
            int n = SR * (k == "ambient" ? 4 : 2); var buf = new Vector2[n];
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

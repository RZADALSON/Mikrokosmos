namespace Mikro;

/// <summary>
/// Proceduralny dźwięk (bez plików): tło (szum wody / pompy próżniowe mikroskopu elektronowego, brzęczenie lampy)
/// oraz krótkie efekty – pęknięcie błony, połknięcie, wystrzał organelli, cel osiągnięty.
/// </summary>
public partial class Audio : Node
{
    const int SR = 22050;
    AudioStreamPlayer _player;
    AudioStreamGeneratorPlayback _pb;
    public bool Muted;
    /// <summary>0 = woda (mikroskop świetlny), 1 = próżnia TEM, -1 = cisza (menu).</summary>
    public int Ambient = 0;
    float _amb = 0;

    sealed class Voice { public string K; public float T, Dur, Vol, Ph, Seed, Lp; }
    readonly List<Voice> _voices = new();
    float _brown, _lp, _hp, _t;
    ulong _rng = 0x9E3779B97F4A7C15UL;

    float Wn() { _rng ^= _rng << 13; _rng ^= _rng >> 7; _rng ^= _rng << 17; return (float)((_rng >> 40) * (1.0 / 16777216.0)) * 2 - 1; }

    public override void _Ready()
    {
        var gen = new AudioStreamGenerator { MixRate = SR, BufferLength = 0.15f };
        _player = new AudioStreamPlayer { Stream = gen, VolumeDb = -4 };
        AddChild(_player);
        _player.Play();
        _pb = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    public void Play(string kind, float vol = 1)
    {
        if (Muted || vol <= 0.01f || _voices.Count > 20) return;
        float dur = kind switch { "pop" => 0.35f, "gulp" => 0.32f, "zap" => 0.16f, "snap" => 0.05f, "tick" => 0.03f, "blip" => 0.14f, "net" => 0.9f, "goal" => 0.8f, "hurt" => 0.25f, _ => 0.1f };
        _voices.Add(new Voice { K = kind, Dur = dur, Vol = Clamp(vol, 0, 1), Seed = Rand(0.85f, 1.15f) });
    }

    public override void _Process(double delta)
    {
        if (_pb == null) return;
        int n = _pb.GetFramesAvailable();
        if (n <= 0) return;
        n = Math.Min(n, 4096);
        var buf = new Vector2[n];
        float dt = 1f / SR;
        float target = Muted ? 0 : Ambient < 0 ? 0.35f : 1;
        for (int i = 0; i < n; i++)
        {
            _t += dt;
            _amb += (target - _amb) * 0.0002f;
            float white = Wn();
            _brown = (_brown + white * 0.02f) * 0.998f;
            _lp += (_brown - _lp) * 0.05f;
            float s;
            if (Ambient == 1)
            {
                _hp = 0.97f * (_hp + white - _lp);
                s = _lp * 0.25f + white * 0.006f + MathF.Sin(TAU * 38 * _t) * 0.006f * (0.7f + 0.3f * MathF.Sin(_t * 0.7f));
            }
            else s = _lp * 0.55f * (0.8f + 0.2f * MathF.Sin(_t * 0.23f)) + MathF.Sin(TAU * 100 * _t) * 0.0025f;
            s *= _amb * 0.6f;
            for (int k = _voices.Count - 1; k >= 0; k--)
            {
                var v = _voices[k];
                float x = v.T / v.Dur, e, o = 0;
                switch (v.K)
                {
                    case "pop":
                        e = MathF.Exp(-x * 7);
                        v.Ph += TAU * Lerp(420, 90, x) * v.Seed * dt;
                        v.Lp += (white - v.Lp) * 0.25f;
                        o = (MathF.Sin(v.Ph) * 0.55f + v.Lp * 0.7f) * e;
                        break;
                    case "gulp":
                        e = MathF.Sin(PI * Math.Min(1, x));
                        v.Ph += TAU * Lerp(230, 70, x) * v.Seed * dt;
                        o = MathF.Sin(v.Ph) * e * 0.6f;
                        break;
                    case "zap":
                        e = MathF.Exp(-x * 9);
                        o = (Chance(0.25f) ? white : 0) * e * 0.8f;
                        break;
                    case "snap":
                        e = MathF.Exp(-x * 12);
                        v.Lp += (white - v.Lp) * 0.5f;
                        o = v.Lp * e;
                        break;
                    case "tick":
                        e = 1 - x; v.Ph += TAU * 1800 * dt; o = MathF.Sin(v.Ph) * e * 0.25f;
                        break;
                    case "blip":
                        e = MathF.Sin(PI * Math.Min(1, x)); v.Ph += TAU * Lerp(520, 880, x) * dt; o = MathF.Sin(v.Ph) * e * 0.25f;
                        break;
                    case "net":
                        e = MathF.Sin(PI * Math.Min(1, x));
                        v.Lp += (white - v.Lp) * 0.04f;
                        v.Ph += TAU * 140 * dt;
                        o = (v.Lp * 1.5f + MathF.Sin(v.Ph) * 0.2f) * e;
                        break;
                    case "goal":
                        float f = x < 0.35f ? 660 : x < 0.7f ? 880 : 990;
                        e = MathF.Exp(-((x * 3) % 1) * 3) * (1 - x);
                        v.Ph += TAU * f * dt; o = (MathF.Sin(v.Ph) + 0.3f * MathF.Sin(v.Ph * 2)) * e * 0.28f;
                        break;
                    case "hurt":
                        e = MathF.Exp(-x * 5); v.Ph += TAU * 90 * dt; o = MathF.Sin(v.Ph) * e * 0.5f;
                        break;
                }
                s += o * v.Vol * 0.55f;
                v.T += dt;
                if (v.T >= v.Dur) _voices.RemoveAt(k);
            }
            s = MathF.Tanh(s * 1.4f);
            buf[i] = new Vector2(s, s);
        }
        _pb.PushBuffer(buf);
    }
}

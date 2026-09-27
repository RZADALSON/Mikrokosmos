namespace Mikro;

/// <summary>
/// Nanobot – tryb eksploracji. Hipotetyczna maszyna (nie organizm!) skalowana do wybranego świata:
/// napęd niezależny od lepkości, laser dezintegrujący dowolną komórkę, skaner rozpoznający organizmy.
/// Jest odporny na wszystko i przepycha każdą przeszkodę.
/// </summary>
public sealed class Nanobot : Ent
{
    public readonly float Size;
    float _spin, _thr, _beamT, _cool;
    public Ent Beam; float _bx, _by;
    public int Kills;
    public override float Radius => Size / 2;
    public override string Info => $"Nanobot (Ø {Fmt(Size)}) – maszyna, nie organizm";
    public static string Fmt(float s) => s >= 1000 ? $"{s / 1000:0.0#} mm" : s >= 1 ? $"{s:0.##} µm" : $"{s * 1000:0} nm";

    public Nanobot(World w, float x, float y) : base(w, x, y, 0)
    {
        Kind = Kind.Bot; Layer = 6; Name = "nanobot"; Soft = 0; FlowK = 0;
        Size = w.Sc.ViewW * 0.035f; R = Size * 0.8f; Mass = 1e8f; Edible = false;
    }
    public override void UpdatePrim() => SetCircle(Size * 0.45f);
    public override bool CanControl() => true;
    public override void Kill() { }
    public override void Unhold() { Holder = null; Solid = true; State = St.Free; }

    /// <summary>Encja pod celownikiem (dla skanera i lasera).</summary>
    public static Ent Pick(World w, float x, float y, float r, Ent self)
    {
        Ent best = null; float bd = float.MaxValue;
        foreach (var e in w.Ents)
        {
            if (!e.Alive || e == self || e is Net or FlagFrag) continue;
            float d;
            if (e is ChainSeg || e.Prim.Cap) d = Math.Max(0, Geo.PointPrim(x, y, e.Prim).D);
            else d = Math.Max(0, Hypot(e.X - x, e.Y - y) - Math.Max(e.Radius, e.Prim.R));
            if (d < r && d < bd) { bd = d; best = e; }
        }
        return best;
    }

    public override void Step(float dt, float gdt)
    {
        Hp = 1; Holder = null; State = St.Free;
        var I = W.Input;
        float sp = W.Sc.ViewW * 0.5f / Math.Max(W.TimeScale, 1e-4f) * (I.Boost ? 2.6f : 1);
        Vx = I.Dx * sp; Vy = I.Dy * sp;
        if (I.Mag > 0.05f) A = Turn(A, I.Ang, 8 * gdt);
        _spin += gdt * (1.5f + I.Mag * 6); _thr = Lerp(_thr, I.Mag * (I.Boost ? 1.5f : 1), Damp(10, gdt));
        _cool -= gdt;
        // laser: Spacja / PPM – cel pod kursorem albo najbliższy przed dziobem
        if ((I.ActionHeld || I.Action) && _cool <= 0)
        {
            if (Beam == null || !Beam.Alive)
            {
                Beam = I.HasAim ? Pick(W, I.AimX, I.AimY, W.Sc.ViewW * 0.03f, this) : null;
                if (Beam == null)
                {
                    float fx = X + MathF.Cos(A) * W.Sc.ViewW * 0.2f, fy = Y + MathF.Sin(A) * W.Sc.ViewW * 0.2f;
                    Beam = Pick(W, fx, fy, W.Sc.ViewW * 0.25f, this);
                }
                if (Beam != null && Beam.Kind == Kind.None) Beam = null;
                _beamT = 0;
            }
        }
        else if (!I.ActionHeld) Beam = null;
        if (Beam != null)
        {
            if (!Beam.Alive) { Beam = null; return; }
            _bx = Beam.X; _by = Beam.Y;
            _beamT += gdt;
            if (Chance(gdt * 30)) W.Fx.Spill(_bx, _by, 2, new FX.SpillOpt { Speed = Size * 1.5f, R = Size * 0.04f, Life = 0.6f, R0 = Beam.Radius * 0.5f, Cols = new[] { Rgb(120, 230, 255, 0.9f), Rgb(255, 255, 255, 0.9f) } });
            if (_beamT > 0.35f)
            {
                var tgt = Beam; Beam = null; _cool = 0.15f;
                string info = tgt.Info;
                if (tgt is ChainSeg cs) tgt = cs.Owner;
                tgt.Kill();
                if (tgt.Alive) tgt.Alive = false; // odporne na zwykłą śmierć
                W.Fx.RingAt(_bx, _by, Math.Max(tgt.Radius, Size) * 1.6f, Rgb(140, 230, 255), Size * 0.05f, 0.5f);
                Kills++; W.AddStat("botkills", 1);
                W.Sfx("zap", _bx, _by);
                W.Log("Zdezintegrowano: " + info, "info");
            }
        }
    }

    public override void Draw(Painter p, View v)
    {
        float s = Size / 2;
        if (Beam != null && Beam.Alive)
        {
            p.Identity();
            float fl = 0.7f + 0.3f * MathF.Sin(W.T * 60);
            p.Line(X, Y, _bx, _by, s * 0.5f, Rgb(80, 200, 255, 0.25f * fl), true);
            p.Line(X, Y, _bx, _by, s * 0.14f, Rgb(200, 245, 255, 0.95f), true);
            p.Glow(_bx, _by, s * 1.6f, Rgb(120, 220, 255, 0.5f * fl));
        }
        p.Local(X, Y, A);
        // ciąg silników
        for (int k = 0; k < 3; k++)
        {
            float a = PI + (k - 1) * 0.5f, ex = MathF.Cos(a) * s * 0.95f, ey = MathF.Sin(a) * s * 0.95f;
            if (_thr > 0.05f) p.Glow(ex + MathF.Cos(a) * s * 0.5f * _thr, ey + MathF.Sin(a) * s * 0.5f * _thr, s * (0.35f + 0.35f * _thr) * (0.85f + 0.15f * MathF.Sin(W.T * 40 + k)), Rgb(110, 210, 255, 0.55f * _thr));
            p.FillCircle(ex, ey, s * 0.22f, Rgb(70, 78, 88, 0.95f));
            p.Circle(ex, ey, s * 0.22f, s * 0.04f, Rgb(170, 190, 205, 0.9f));
        }
        // kadłub – sześciokąt z gradientem metalicznym
        Span<Vector2> hex = stackalloc Vector2[6];
        for (int i = 0; i < 6; i++) { float t = i * TAU / 6 + PI / 6; hex[i] = new Vector2(MathF.Cos(t) * s * (i == 0 || i == 5 ? 1.15f : 0.9f), MathF.Sin(t) * s * 0.85f); }
        p.Glow(0, 0, s * 1.8f, Rgb(140, 220, 255, 0.12f));
        Span<float> ts = stackalloc float[] { 0f, 0.6f, 1f };
        Span<Color> cs = stackalloc Color[] { Rgb(205, 215, 225), Rgb(130, 142, 155), Rgb(70, 78, 88) };
        p.FillRadial(hex, -s * 0.2f, -s * 0.25f, ts, cs);
        p.Stroke(hex, true, s * 0.06f, Rgb(40, 46, 54, 0.95f));
        // obracający się pierścień sensorów
        for (int k = 0; k < 8; k++) { float t = k * TAU / 8 + _spin; p.FillCircle(MathF.Cos(t) * s * 0.55f, MathF.Sin(t) * s * 0.55f, s * 0.07f, Rgb(90, 220, 255, 0.85f)); }
        // rdzeń
        float pulse = 0.8f + 0.2f * MathF.Sin(W.T * 5);
        p.Glow(0, 0, s * 0.55f * pulse, Rgb(130, 235, 255, 0.8f));
        p.FillCircle(0, 0, s * 0.22f, Rgb(235, 250, 255));
        // dziób z emiterem lasera
        p.Line(s * 0.85f, 0, s * 1.35f, 0, s * 0.12f, Rgb(60, 66, 74), true);
        p.FillCircle(s * 1.35f, 0, s * 0.09f, _cool > 0 ? Rgb(255, 180, 120) : Rgb(150, 235, 255));
    }
}

namespace Mikro;

/// <summary>Podgląd organizmu w karcie menu – mini-symulacja jednej encji.</summary>
public partial class PreviewCanvas : Control
{
    public Scenario Sc;
    World _w; Ent _e; float _size;
    readonly Painter _p = new();
    readonly View _v = new();

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _w = Sc.Create(true);
        _w.Input = new InputState { Dx = 1, Dy = 0, Mag = 1, Ang = 0 };
        (_e, _size) = Sc.Preview(_w);
        _w.Player = _e;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || _e == null) return;
        float fdt = Math.Min((float)delta, 1f / 25);
        _w.T += fdt;
        float dt = fdt * _w.TimeScale;
        _e.Step(dt, fdt);
        _e.X = 0; _e.Y = 0; _e.Vx = _e.Vy = 0;
        if (_e is Didinium d) d.Hunger = 0;
        if (_e is PhageT4) _e.A += fdt * 0.3f;
        _e.UpdatePrim();
        _e.Mem?.Follow(_e.X, _e.Y, _e.A, _e.MemK, fdt);
        _e.Post(dt, fdt);
        _w.Fx.Update(fdt);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_e == null) return;
        var sz = Size;
        float s = sz.X / _size;
        float stretch = GetViewport() is Window win ? DisplayServer.WindowGetSize().X / Math.Max(1f, win.GetVisibleRect().Size.X) : 1f;
        DrawSetTransform(sz / 2, 0, new Vector2(s, s));
        _v.S = s; _v.Px = 1f / (s * Math.Max(0.5f, stretch)); _v.X0 = -sz.X / 2 / s; _v.X1 = -_v.X0; _v.Y0 = -sz.Y / 2 / s; _v.Y1 = -_v.Y0; _v.T = _w.T;
        _p.Begin(_v.Px);
        var st = Sc.Style;
        int n = _p.CirclePts(0, 0, Hypot(sz.X, sz.Y) / 2 / s, _p.Shape, 48);
        Span<float> ts = stackalloc float[] { 0f, 0.6f, 1f };
        Span<Color> cs = stackalloc Color[] { st.BgIn, st.BgMid, st.BgOut };
        _p.FillRadial(new ReadOnlySpan<Vector2>(_p.Shape, 0, n), 0, 0, ts, cs, false);
        _p.Identity();
        _e.Draw(_p, _v);
        _p.Identity();
        _w.Fx.Draw(_p, _v);
        _p.Flush(GetCanvasItem());
    }
}

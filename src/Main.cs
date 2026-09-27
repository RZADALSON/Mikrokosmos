using System.Linq;
using System.Threading.Tasks;

namespace Mikro;

/// <summary>
/// Główny węzeł: warstwy renderowania (tło → świat → filtr trybu → winieta/szum → nakładka → HUD → menu),
/// pętla gry, sterowanie, kamera, menu z podglądami, dialogi oraz tryby testowe (--autotest, --shot).
/// </summary>
public partial class Main : Node
{
    enum Mode { Menu, Game }
    Mode _mode = Mode.Menu;
    Scenario _sc;
    World _world, _demo;
    bool _paused, _mouseFollow, _mouseDown, _actionEdge, _showFps, _testInput, _nano;
    float _timeMul = 1;
    Control _nanoPick, _settings;
    int _modeIdx;
    float _camX, _camY, _zoom = 1, _tz = 1, _s0 = 1, _hudT, _fpsAcc; int _fpsN;
    readonly InputState _input = new();

    WorldView _view; Overlay _overlay;
    ColorRect _bg, _filter, _scope; ShaderMaterial _bgMat, _filterMat, _scopeMat;
    ScopeStyle _curStyle;

    // HUD
    Control _hud, _banner;
    Audio _audio;
    VBoxContainer _barsBox; readonly List<(Label l, ProgressBar b, StyleBoxFlat f)> _bars = new();
    Label _goalHdr, _goalText; ProgressBar _goalBar;
    readonly Dictionary<string, Label> _cardRec = new();
    Label _orgName, _orgLatin, _orgType, _modeLbl, _magLbl, _timeLbl, _ffLbl, _controls, _fps, _toast, _bannerT, _bannerS;
    ColorRect _orgDot; GridContainer _stats; VBoxContainer _log;
    readonly List<(Label k, Label v)> _statRows = new();
    Tween _bannerTw, _toastTw;

    // menu / dialogi
    Control _menu, _info, _over, _pause;
    Label _infoTitle, _overTitle, _overText; RichTextLabel _infoFacts, _infoSources; GridContainer _overStats;

    public override void _Ready()
    {
        Settings.Load();
        var a0 = OS.GetCmdlineUserArgs();
        if (!a0.Any(a => a.StartsWith("--shot") || a == "--check" || a == "--autotest")) Settings.ApplyWindow();
        _showFps = Settings.ShowFps;
        Ui.Init();
        DebrisAtlas.Ensure();
        _audio = new Audio(); AddChild(_audio);
        BuildLayers();
        BuildHud();
        BuildMenu();
        BuildDialogs();
        _audio.Muted = Settings.Muted;
        _fps.Visible = _showFps;
        EnsureDemo();
        ShowMenu();
        var args = OS.GetCmdlineUserArgs();
        if (args.Any(a => a.StartsWith("--audio="))) { Audio.Report(args.First(a => a.StartsWith("--audio="))[8..]); GetTree().Quit(); return; }
        if (args.Contains("--check")) _ = Checks();
        else if (args.Contains("--autotest")) _ = AutoTest(args);
        else if (args.Any(a => a.StartsWith("--shot="))) _ = Shots(args);
    }

    // ================================================================ warstwy
    void BuildLayers()
    {
        var bgL = new CanvasLayer { Layer = -10 }; AddChild(bgL);
        _bgMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/scope_bg.gdshader") };
        _bg = Ui.Full(new ColorRect { Material = _bgMat, MouseFilter = Control.MouseFilterEnum.Ignore }); bgL.AddChild(_bg);

        _view = new WorldView(); AddChild(_view);

        var fL = new CanvasLayer { Layer = 5 }; AddChild(fL);
        _filterMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/scope_filter.gdshader") };
        _filter = Ui.Full(new ColorRect { Material = _filterMat, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false }); fL.AddChild(_filter);

        var sL = new CanvasLayer { Layer = 6 }; AddChild(sL);
        _scopeMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/scope_overlay.gdshader") };
        _scope = Ui.Full(new ColorRect { Material = _scopeMat, MouseFilter = Control.MouseFilterEnum.Ignore }); sL.AddChild(_scope);

        var oL = new CanvasLayer { Layer = 7 }; AddChild(oL);
        _overlay = new Overlay(); oL.AddChild(_overlay);
    }

    void ApplyStyle(ScopeStyle st)
    {
        if (_curStyle == st) return;
        _curStyle = st;
        _bgMat.SetShaderParameter("bg_in", st.BgIn); _bgMat.SetShaderParameter("bg_mid", st.BgMid); _bgMat.SetShaderParameter("bg_out", st.BgOut);
        _bgMat.SetShaderParameter("uneven", st.Uneven); _bgMat.SetShaderParameter("dust", st.Dust ? 1f : 0f); _bgMat.SetShaderParameter("seed", Rand(0, 100));
        _scopeMat.SetShaderParameter("vig", st.Vig); _scopeMat.SetShaderParameter("vig_in", st.VigIn); _scopeMat.SetShaderParameter("grain", st.Grain);
    }

    Vector2 ScreenSize => GetViewport().GetVisibleRect().Size;
    float Stretch => DisplayServer.WindowGetSize().X / Math.Max(1f, ScreenSize.X);

    // ================================================================ HUD
    void BuildHud()
    {
        var L = new CanvasLayer { Layer = 10 }; AddChild(L);
        _hud = Ui.Full(new Control { Theme = Ui.Theme, MouseFilter = Control.MouseFilterEnum.Ignore }); L.AddChild(_hud);

        var tl = new PanelContainer { Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(270, 0) }; _hud.AddChild(tl);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); tl.AddChild(v);
        var hdr = new HBoxContainer(); hdr.AddThemeConstantOverride("separation", 10); v.AddChild(hdr);
        _orgDot = new ColorRect { CustomMinimumSize = new Vector2(12, 12), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; hdr.AddChild(_orgDot);
        _orgName = Ui.L("", 20, null, true); hdr.AddChild(_orgName);
        _orgLatin = Ui.L("", 13, Ui.Dim); v.AddChild(_orgLatin);
        _orgType = Ui.L("", 12, Ui.Dim); v.AddChild(_orgType);
        v.AddChild(new HSeparator());
        _stats = new GridContainer { Columns = 2 }; _stats.AddThemeConstantOverride("h_separation", 18); _stats.AddThemeConstantOverride("v_separation", 3); v.AddChild(_stats);
        _barsBox = new VBoxContainer(); _barsBox.AddThemeConstantOverride("separation", 2); v.AddChild(_barsBox);
        v.AddChild(new HSeparator());
        _goalHdr = Ui.L("CEL", 11, Hex("#ffd27a"), true); v.AddChild(_goalHdr);
        _goalText = Ui.L("", 13, null, false, HorizontalAlignment.Left, true); _goalText.CustomMinimumSize = new Vector2(260, 0); v.AddChild(_goalText);
        _goalBar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5) };
        _goalBar.AddThemeStyleboxOverride("fill", Ui.Box(Hex("#ffd27a"), new Color(0, 0, 0, 0), 3, 0, 0));
        v.AddChild(_goalBar);

        var tr = new PanelContainer(); _hud.AddChild(tr);
        tr.AnchorLeft = 1; tr.AnchorRight = 1; tr.AnchorTop = 0; tr.AnchorBottom = 0;
        tr.GrowHorizontal = Control.GrowDirection.Begin;
        tr.OffsetLeft = -16; tr.OffsetRight = -16; tr.OffsetTop = 16; tr.OffsetBottom = 16;
        var tv = new VBoxContainer(); tr.AddChild(tv);
        _modeLbl = Ui.L("", 14, null, true, HorizontalAlignment.Right); tv.AddChild(_modeLbl);
        _magLbl = Ui.L("", 13, Ui.Dim, false, HorizontalAlignment.Right); tv.AddChild(_magLbl);
        _timeLbl = Ui.L("", 13, Ui.Dim, false, HorizontalAlignment.Right); tv.AddChild(_timeLbl);
        _ffLbl = Ui.L("", 14, Hex("#ffd27a"), true, HorizontalAlignment.Right); tv.AddChild(_ffLbl);

        _log = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
        _log.AddThemeConstantOverride("separation", 6);
        _hud.AddChild(_log);
        _log.AnchorLeft = 0; _log.AnchorRight = 0; _log.AnchorTop = 1; _log.AnchorBottom = 1;
        _log.OffsetLeft = 16; _log.OffsetRight = 590; _log.OffsetTop = -460; _log.OffsetBottom = -58;

        _controls = Ui.L("", 12, new Color(1, 1, 1, 0.8f), false, HorizontalAlignment.Center);
        _controls.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0, 0, 0, 0.35f), new Color(0, 0, 0, 0), 14, 0, 6));
        _hud.AddChild(_controls);
        _controls.AnchorLeft = 0.5f; _controls.AnchorRight = 0.5f; _controls.AnchorTop = 1; _controls.AnchorBottom = 1;
        _controls.GrowHorizontal = Control.GrowDirection.Both; _controls.GrowVertical = Control.GrowDirection.Begin; _controls.OffsetBottom = -12;

        _banner = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        _hud.AddChild(_banner);
        _banner.AnchorLeft = 0; _banner.AnchorRight = 1; _banner.AnchorTop = 0.28f; _banner.AnchorBottom = 0.28f;
        _bannerT = Ui.L("", 54, null, true, HorizontalAlignment.Center); _banner.AddChild(_bannerT);
        _bannerS = Ui.L("", 17, Hex("#e6ece0"), false, HorizontalAlignment.Center); _banner.AddChild(_bannerS);
        _bannerT.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f)); _bannerT.AddThemeConstantOverride("shadow_offset_y", 3);
        _bannerS.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));

        var topL = new CanvasLayer { Layer = 40 }; AddChild(topL);
        var topRoot = Ui.Full(new Control { Theme = Ui.Theme, MouseFilter = Control.MouseFilterEnum.Ignore }); topL.AddChild(topRoot);
        _toast = Ui.L("", 14, null, false, HorizontalAlignment.Center);
        _toast.AddThemeStyleboxOverride("normal", Ui.Box(Ui.PanelBg, Ui.PanelBorder, 10, 1, 8));
        topRoot.AddChild(_toast);
        _toast.AnchorLeft = 0.5f; _toast.AnchorRight = 0.5f; _toast.OffsetTop = 18; _toast.GrowHorizontal = Control.GrowDirection.Both; _toast.Modulate = new Color(1, 1, 1, 0);
        _fps = Ui.L("", 13, Hex("#cceeff"), true, HorizontalAlignment.Right);
        topRoot.AddChild(_fps);
        _fps.AnchorLeft = 1; _fps.AnchorRight = 1; _fps.AnchorTop = 1; _fps.AnchorBottom = 1; _fps.OffsetLeft = -240; _fps.OffsetRight = -16; _fps.OffsetTop = -80; _fps.OffsetBottom = -60; _fps.Visible = false;
    }

    void Log(string text, string cls)
    {
        var pc = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var sb = Ui.Box(Ui.PanelBg, Ui.PanelBorder, 10, 1, 9);
        sb.BorderWidthLeft = 3; sb.BorderColor = cls == "good" ? Ui.Good : cls == "bad" ? Ui.Bad : Ui.Info;
        pc.AddThemeStyleboxOverride("panel", sb);
        var l = Ui.L(text, 14, null, false, HorizontalAlignment.Left, true);
        pc.AddChild(l);
        _log.AddChild(pc);
        while (_log.GetChildCount() > 6) { var c = _log.GetChild(0); _log.RemoveChild(c); c.QueueFree(); }
        pc.Modulate = new Color(1, 1, 1, 0);
        var tw = pc.CreateTween();
        tw.TweenProperty(pc, "modulate:a", 1f, 0.25f);
        tw.TweenInterval(9);
        tw.TweenProperty(pc, "modulate:a", 0f, 1.4f);
        tw.TweenCallback(Callable.From(() => { if (IsInstanceValid(pc)) pc.QueueFree(); }));
    }
    void Banner(string t, string s)
    {
        _bannerT.Text = t; _bannerS.Text = s;
        _bannerTw?.Kill();
        _banner.Modulate = new Color(1, 1, 1, 0); _banner.Scale = new Vector2(1, 1);
        _bannerTw = CreateTween();
        _bannerTw.TweenProperty(_banner, "modulate:a", 1f, 0.3f);
        _bannerTw.TweenInterval(1.7f);
        _bannerTw.TweenProperty(_banner, "modulate:a", 0f, 0.6f);
    }
    void Toast(string t)
    {
        _toast.Text = t;
        _toastTw?.Kill();
        _toastTw = CreateTween();
        _toastTw.TweenProperty(_toast, "modulate:a", 1f, 0.15f);
        _toastTw.TweenInterval(1.4f);
        _toastTw.TweenProperty(_toast, "modulate:a", 0f, 0.3f);
    }

    void UpdateHud(float fdt)
    {
        _hudT -= fdt;
        if (_hudT > 0 || _world == null) return;
        _hudT = 0.12f;
        var rows = _nano ? NanoRows() : _sc.Hud(_world);
        if (_world.Infinite && _world.Player != null)
        {
            var pl = _world.Player;
            rows.Insert(0, ("Strefa", _sc.BiomeName(_world.ChunkAt(pl.X, pl.Y).Biome)));
            float dist = Hypot(pl.X, pl.Y);
            rows.Insert(1, ("Od startu", dist >= 1000 ? $"{dist / 1000:0.00} mm" : dist >= 1 ? $"{dist:0} µm" : $"{dist * 1000:0} nm"));
        }
        while (_statRows.Count < rows.Count)
        {
            var k = Ui.L("", 14, Ui.Dim); var val = Ui.L("", 14, null, true, HorizontalAlignment.Right);
            val.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _stats.AddChild(k); _stats.AddChild(val); _statRows.Add((k, val));
        }
        for (int i = 0; i < _statRows.Count; i++)
        {
            bool vis = i < rows.Count;
            _statRows[i].k.Visible = vis; _statRows[i].v.Visible = vis;
            if (vis) { _statRows[i].k.Text = rows[i].Item1; _statRows[i].v.Text = rows[i].Item2; }
        }
        var bars = _nano ? null : _sc.Bars(_world);
        var plr = _world.Player;
        if (!_nano && plr != null && plr.Alive && plr is not Neutrophil && plr.Hp < 0.999f)
        {
            bars ??= new();
            bars.Insert(0, ("Integralność błony", plr.Hp, plr.Hp < 0.35f ? Hex("#e05a4a") : Hex("#9fd0ff")));
        }
        int nb = bars?.Count ?? 0;
        while (_bars.Count < nb)
        {
            var l = Ui.L("", 11, Ui.Dim);
            var b = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6) };
            var f = Ui.Box(Colors.White, new Color(0, 0, 0, 0), 3, 0, 0); b.AddThemeStyleboxOverride("fill", f);
            _barsBox.AddChild(l); _barsBox.AddChild(b); _bars.Add((l, b, f));
        }
        for (int i = 0; i < _bars.Count; i++)
        {
            bool vis = i < nb; _bars[i].l.Visible = vis; _bars[i].b.Visible = vis;
            if (!vis) continue;
            var (lab, val, col) = bars[i];
            _bars[i].l.Text = lab.ToUpperInvariant(); _bars[i].b.Value = Clamp(val, 0, 1); _bars[i].f.BgColor = col;
        }
        var G = _nano ? null : _sc.Goals;
        if (_nano) { _goalHdr.Text = "TRYB EKSPLORACJI"; _goalText.Text = "Brak celów – zwiedzaj, skanuj i porównuj skale (1–7)."; _goalBar.Visible = false; }
        if (G != null)
        {
            if (_world.GoalIdx < G.Length)
            {
                var g = G[_world.GoalIdx];
                _goalHdr.Text = $"CEL {_world.GoalIdx + 1}/{G.Length}";
                _goalText.Text = g.Text;
                _goalBar.Value = Clamp(g.Val(_world) / g.Target, 0, 1); _goalBar.Visible = true;
            }
            else { _goalHdr.Text = "WSZYSTKIE CELE UKOŃCZONE"; _goalText.Text = $"Tryb swobodny · rekord: {Records.Best(_sc.Id):0} {_sc.ScoreLabel}"; _goalBar.Visible = false; }
        }
        _ffLbl.Visible = _world.Ff > 1; _ffLbl.Text = $"▶▶ ×{_world.Ff}";
    }

    List<(string, string)> NanoRows()
    {
        var w = _world; var I = _input;
        string under = "-";
        if (I.HasAim) { var e = Nanobot.Pick(w, I.AimX, I.AimY, _sc.ViewW * 0.03f, w.Player); if (e != null) under = e.Name; }
        int alive = 0; var kinds = new HashSet<string>();
        foreach (var e in w.Ents) if (e.Alive && e != w.Player && e.Kind != Kind.None && e is not ChainSeg) { alive++; kinds.Add(e.Name); }
        return new()
        {
            ("Kadr", _sc.ViewW >= 1000 ? $"{_sc.ViewW / 1000:0.#} mm" : $"{_sc.ViewW:0.#} µm"),
            ("Tempo czasu", $"×{_timeMul:0.###} ({(_sc.TimeScale >= 1 ? $"time-lapse ×{_sc.TimeScale:0.#}" : $"zwolnione ×{_sc.TimeScale:0.##}")})"),
            ("Pod kursorem", under), ("Organizmy w okolicy", alive.ToString()), ("Gatunki/typy w okolicy", kinds.Count.ToString()),
            ("Zdezintegrowane", N0(w.Stat("botkills"))),
        };
    }
    static string N0(float v) => ((int)MathF.Round(v)).ToString();

    // ================================================================ menu
    void BuildMenu()
    {
        var L = new CanvasLayer { Layer = 20 }; AddChild(L);
        _menu = Ui.Full(new Control { Theme = Ui.Theme }); L.AddChild(_menu);
        _menu.AddChild(Ui.Full(new ColorRect { Color = new Color(0.02f, 0.03f, 0.035f, 0.62f), MouseFilter = Control.MouseFilterEnum.Ignore }));
        // bez przewijania: siatka kart skaluje się tak, by zawsze zmieścić się w oknie
        var margin = Ui.Full(new MarginContainer());
        foreach (var m in new[] { "margin_left", "margin_right" }) margin.AddThemeConstantOverride(m, 24);
        margin.AddThemeConstantOverride("margin_top", 22); margin.AddThemeConstantOverride("margin_bottom", 14);
        _menu.AddChild(margin);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6); margin.AddChild(v);
        var title = Ui.L("M I K R O K O S M O S", 54, Hex("#dcebd6"), true, HorizontalAlignment.Center);
        title.AddThemeColorOverride("font_shadow_color", new Color(0.4f, 0.6f, 0.4f, 0.35f)); title.AddThemeConstantOverride("shadow_outline_size", 12);
        v.AddChild(title);
        v.AddChild(Ui.L("Wybierz organizm – każdy żyje w innej skali, innym czasie i pod innym mikroskopem · od wirusa 0,2 µm (TEM) po amebę 0,5 mm", 15, Hex("#c8d2c2"), false, HorizontalAlignment.Center));
        var holder = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        v.AddChild(holder);
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 18); grid.AddThemeConstantOverride("v_separation", 18);
        holder.AddChild(grid);
        foreach (var sc in Scenario.All) grid.AddChild(MakeCard(sc));
        grid.AddChild(MakeNanoCard());
        void Fit()
        {
            var gs = grid.GetCombinedMinimumSize(); var hs = holder.Size;
            if (gs.X <= 0 || hs.X <= 0 || hs.Y <= 0) return;
            float k = Math.Min(1.15f, Math.Min(hs.X / gs.X, hs.Y / gs.Y));
            grid.Size = gs; grid.Scale = new Vector2(k, k); grid.Position = (hs - gs * k) / 2;
        }
        holder.Resized += Fit;
        grid.MinimumSizeChanged += Fit;
        v.AddChild(Ui.L("WASD / LPM – ruch · C – za kursorem · Shift – szybciej · Spacja – akcja · Kółko – zoom · M – mikroskop · K – radar · I – wiedza · O – ustawienia · F11 – pełny ekran · Esc – pauza", 13, Hex("#aab5a7"), false, HorizontalAlignment.Center, true));
        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; foot.AddThemeConstantOverride("separation", 20); v.AddChild(foot);
        foot.AddChild(Ui.L("Fizyka niskiej liczby Reynoldsa · ruchy Browna · miękkie ciała · Godot 4 + C#", 13, Hex("#9aa598")));
        var set = new LinkButton { Text = "Ustawienia (O)" }; set.AddThemeColorOverride("font_color", Hex("#cfe6c8"));
        set.Pressed += () => ShowSettings(true);
        foot.AddChild(set);
        var src = new LinkButton { Text = "Źródła naukowe" }; src.AddThemeColorOverride("font_color", Hex("#cfe6c8"));
        src.Pressed += () => ShowInfo(null);
        foot.AddChild(src);
    }

    Control MakeCard(Scenario sc)
    {
        var b = new Button { CustomMinimumSize = new Vector2(300, 376), FocusMode = Control.FocusModeEnum.None };
        b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0.063f, 0.078f, 0.086f, 0.78f), new Color(1, 1, 1, 0.09f), 16, 1, 0));
        b.AddThemeStyleboxOverride("hover", Ui.Box(new Color(0.08f, 0.1f, 0.11f, 0.86f), sc.Color, 16, 2, 0));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(0.1f, 0.12f, 0.13f, 0.9f), sc.Color, 16, 2, 0));
        b.Pressed += () => Start(sc);
        var v = Ui.Full(new VBoxContainer()); v.AddThemeConstantOverride("separation", 0); b.AddChild(v);
        var pvBox = new Control { CustomMinimumSize = new Vector2(0, 172), ClipContents = true };
        v.AddChild(pvBox);
        var pv = Ui.Full(new PreviewCanvas { Sc = sc }); pvBox.AddChild(pv);
        var badge = Ui.L(sc.Size, 12, Colors.White);
        badge.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0, 0, 0, 0.55f), new Color(0, 0, 0, 0), 10, 0, 4));
        pvBox.AddChild(badge);
        badge.AnchorLeft = 1; badge.AnchorRight = 1; badge.AnchorTop = 1; badge.AnchorBottom = 1; badge.GrowHorizontal = Control.GrowDirection.Begin; badge.GrowVertical = Control.GrowDirection.Begin; badge.OffsetRight = -10; badge.OffsetBottom = -8;
        var m = new MarginContainer(); foreach (var s in new[] { "margin_left", "margin_right" }) m.AddThemeConstantOverride(s, 16);
        m.AddThemeConstantOverride("margin_top", 12); m.AddThemeConstantOverride("margin_bottom", 14); v.AddChild(m);
        var tv = new VBoxContainer(); tv.AddThemeConstantOverride("separation", 2); m.AddChild(tv);
        tv.AddChild(Ui.L(sc.TypeName.ToUpperInvariant(), 11, sc.Color, true));
        tv.AddChild(Ui.L(sc.Name, 21, null, true));
        tv.AddChild(Ui.L(sc.Latin, 13, Ui.Dim));
        var d = Ui.L(sc.Desc, 13, Hex("#d4dbcf"), false, HorizontalAlignment.Left, true); d.CustomMinimumSize = new Vector2(268, 0);
        tv.AddChild(d);
        var rec = Ui.L("", 12, Hex("#ffd27a"), true); tv.AddChild(rec); _cardRec[sc.Id] = rec;
        Ui.IgnoreMouse(v);
        return b;
    }

    Control MakeNanoCard()
    {
        var col = Hex("#8fe3ff");
        var b = new Button { CustomMinimumSize = new Vector2(300, 376), FocusMode = Control.FocusModeEnum.None };
        b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0.05f, 0.08f, 0.1f, 0.82f), new Color(0.55f, 0.85f, 1, 0.25f), 16, 1, 0));
        b.AddThemeStyleboxOverride("hover", Ui.Box(new Color(0.07f, 0.1f, 0.13f, 0.9f), col, 16, 2, 0));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(0.09f, 0.12f, 0.15f, 0.9f), col, 16, 2, 0));
        b.Pressed += () => { _nanoPick.Visible = true; };
        var v = Ui.Full(new VBoxContainer()); v.AddThemeConstantOverride("separation", 0); b.AddChild(v);
        var pvBox = new Control { CustomMinimumSize = new Vector2(0, 172), ClipContents = true }; v.AddChild(pvBox);
        var sc0 = Scenario.All.First(s => s.Id == "para");
        pvBox.AddChild(Ui.Full(new PreviewCanvas { Sc = sc0, Make = w => { var e = w.Add(new Nanobot(w, 0, 0)); return (e, e.Size * 3.2f); } }));
        var m = new MarginContainer(); foreach (var s in new[] { "margin_left", "margin_right" }) m.AddThemeConstantOverride(s, 16);
        m.AddThemeConstantOverride("margin_top", 12); m.AddThemeConstantOverride("margin_bottom", 14); v.AddChild(m);
        var tv = new VBoxContainer(); tv.AddThemeConstantOverride("separation", 2); m.AddChild(tv);
        tv.AddChild(Ui.L("TRYB EKSPLORACJI", 11, col, true));
        tv.AddChild(Ui.L("Nanobot", 21, null, true));
        tv.AddChild(Ui.L("maszyna – nie organizm", 13, Ui.Dim));
        var d = Ui.L("Zwiedzaj dowolny świat w jego skali – od powierzchni E. coli po staw. Silniejszy od wszystkiego: laser dezintegruje każdą komórkę, skaner rozpoznaje organizmy. Bez celów i bez śmierci.", 13, Hex("#d4dbcf"), false, HorizontalAlignment.Left, true);
        d.CustomMinimumSize = new Vector2(268, 0); tv.AddChild(d);
        Ui.IgnoreMouse(v);
        return b;
    }

    void ShowMenu()
    {
        _mode = Mode.Menu; _world = null; _paused = false;
        _audio.Ambient = -1;
        foreach (var sc in Scenario.All)
        {
            float best = Records.Best(sc.Id); int g = Records.GoalsDone(sc.Id);
            _cardRec[sc.Id].Text = best > 0 || g > 0 ? $"★ Rekord: {best:0} {sc.ScoreLabel} · cele {g}/{sc.Goals?.Length ?? 0}" : "";
        }
        _menu.Visible = true; _hud.Visible = false; _over.Visible = false; _pause.Visible = false; _info.Visible = false; _nanoPick.Visible = false;
        _filter.Visible = false; _overlay.ShowHud = false;
        EnsureDemo();
        _view.World = _demo; _overlay.World = _demo;
        ApplyStyle(_demo.Sc.Style);
    }

    void EnsureDemo()
    {
        if (_demo != null) return;
        var sc = Scenario.All.First(s => s.Id == "bdello");
        var w = sc.Create();
        sc.Init(w);
        w.Player = null;
        _demo = w;
        _camX = w.CamX; _camY = w.CamY; _tz = _zoom = 1;
    }

    // ================================================================ dialogi
    void BuildDialogs()
    {
        var L = new CanvasLayer { Layer = 30 }; AddChild(L);
        // wiedza / źródła
        _info = Ui.Full(new Control { Theme = Ui.Theme, Visible = false }); L.AddChild(_info);
        _info.AddChild(Ui.Full(new ColorRect { Color = new Color(0, 0, 0, 0.5f) }));
        var c = Ui.Full(new CenterContainer()); _info.AddChild(c);
        var pc = new PanelContainer(); c.AddChild(pc);
        var sc = new ScrollContainer { CustomMinimumSize = new Vector2(780, 620), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; pc.AddChild(sc);
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; v.AddThemeConstantOverride("separation", 8); sc.AddChild(v);
        _infoTitle = Ui.L("", 22, null, true, HorizontalAlignment.Left, true); v.AddChild(_infoTitle);
        v.AddChild(Ui.L("JAK TO WYGLĄDA NAPRAWDĘ", 12, Ui.Dim, true));
        _infoFacts = new RichTextLabel { BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(740, 0) };
        _infoFacts.AddThemeFontSizeOverride("normal_font_size", 15); _infoFacts.AddThemeFontSizeOverride("bold_font_size", 15);
        v.AddChild(_infoFacts);
        v.AddChild(Ui.L("ŹRÓDŁA", 12, Ui.Dim, true));
        _infoSources = new RichTextLabel { BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(740, 0) };
        _infoSources.AddThemeFontSizeOverride("normal_font_size", 13);
        _infoSources.MetaClicked += meta => OS.ShellOpen(meta.AsString());
        v.AddChild(_infoSources);
        var close = new Button { Text = "  Zamknij (I)  ", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        close.Pressed += () => ToggleInfo(false);
        v.AddChild(close);

        // ustawienia
        _settings = Ui.Full(new Control { Theme = Ui.Theme, Visible = false }); L.AddChild(_settings);
        _settings.AddChild(Ui.Full(new ColorRect { Color = new Color(0, 0, 0, 0.55f) }));
        var cs2 = Ui.Full(new CenterContainer()); _settings.AddChild(cs2);
        var ps2 = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) }; cs2.AddChild(ps2);
        var vs2 = new VBoxContainer(); vs2.AddThemeConstantOverride("separation", 10); ps2.AddChild(vs2);
        vs2.AddChild(Ui.L("Ustawienia", 26, null, true));
        vs2.AddChild(Ui.L("OBRAZ", 12, Ui.Dim, true));
        var wm = new OptionButton { CustomMinimumSize = new Vector2(0, 34) };
        foreach (var m in Settings.WindowModes) wm.AddItem(m);
        wm.Selected = Settings.WindowMode;
        wm.ItemSelected += i => { Settings.WindowMode = (int)i; Settings.ApplyWindow(); Settings.Save(); };
        vs2.AddChild(Row("Tryb wyświetlania", wm));
        var vsync = new CheckButton { ButtonPressed = Settings.VSync };
        vsync.Toggled += on => { Settings.VSync = on; DisplayServer.WindowSetVsyncMode(on ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled); Settings.Save(); };
        vs2.AddChild(Row("V-Sync (synchronizacja z monitorem)", vsync));
        var fpsC = new CheckButton { ButtonPressed = Settings.ShowFps };
        fpsC.Toggled += on => { Settings.ShowFps = _showFps = on; _fps.Visible = on; Settings.Save(); };
        vs2.AddChild(Row("Licznik FPS", fpsC));
        vs2.AddChild(Ui.L("DŹWIĘK", 12, Ui.Dim, true));
        HSlider Sl(float v0, Action<float> set)
        {
            var sl = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = v0, CustomMinimumSize = new Vector2(220, 24) };
            sl.ValueChanged += x => { set((float)x); Settings.Save(); };
            return sl;
        }
        vs2.AddChild(Row("Głośność ogólna", Sl(Settings.Master, x => Settings.Master = x)));
        vs2.AddChild(Row("Efekty (organizmy)", Sl(Settings.Sfx, x => { Settings.Sfx = x; _audio.Play("pop", 0.8f); })));
        vs2.AddChild(Row("Tło (woda, mikroskop)", Sl(Settings.Ambient, x => Settings.Ambient = x)));
        var mute = new CheckButton { ButtonPressed = Settings.Muted };
        mute.Toggled += on => { Settings.Muted = _audio.Muted = on; Settings.Save(); };
        vs2.AddChild(Row("Wycisz wszystko (N)", mute));
        var closeS = new Button { Text = "  Zamknij (O / Esc)  ", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        closeS.Pressed += () => ShowSettings(false);
        vs2.AddChild(closeS);
        _settingsMute = mute; _settingsMode = wm;

        // wybór skali dla nanobota
        _nanoPick = Ui.Full(new Control { Theme = Ui.Theme, Visible = false }); L.AddChild(_nanoPick);
        _nanoPick.AddChild(Ui.Full(new ColorRect { Color = new Color(0, 0, 0, 0.55f) }));
        var cn = Ui.Full(new CenterContainer()); _nanoPick.AddChild(cn);
        var pn = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) }; cn.AddChild(pn);
        var vn = new VBoxContainer(); vn.AddThemeConstantOverride("separation", 8); pn.AddChild(vn);
        vn.AddChild(Ui.L("Nanobot – wybierz skalę świata", 24, null, true));
        vn.AddChild(Ui.L("Rozmiar bota dopasuje się do świata (≈ 1/30 kadru). W grze klawisze 1–7 przełączają skalę.", 13, Ui.Dim, false, HorizontalAlignment.Left, true));
        int idx = 1;
        foreach (var s in Scenario.All)
        {
            var scn = s; int k = idx++;
            string scale = scn.ViewW >= 1000 ? $"{scn.ViewW / 1000:0.#} mm" : $"{scn.ViewW:0.#} µm";
            var btn = new Button { Text = $"  {k}.  {WorldName(scn)} – kadr ≈ {scale}  ", CustomMinimumSize = new Vector2(0, 40), Alignment = HorizontalAlignment.Left };
            btn.Pressed += () => { _audio.Play("tick"); Start(scn, true); };
            vn.AddChild(btn);
        }
        var cancel = new Button { Text = "  Anuluj  ", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        cancel.Pressed += () => _nanoPick.Visible = false;
        vn.AddChild(cancel);

        // koniec gry
        _over = Ui.Full(new Control { Theme = Ui.Theme, Visible = false }); L.AddChild(_over);
        _over.AddChild(Ui.Full(new ColorRect { Color = new Color(0, 0, 0, 0.45f) }));
        var c2 = Ui.Full(new CenterContainer()); _over.AddChild(c2);
        var p2 = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) }; c2.AddChild(p2);
        var v2 = new VBoxContainer(); v2.AddThemeConstantOverride("separation", 10); p2.AddChild(v2);
        _overTitle = Ui.L("Koniec gry", 26, null, true); v2.AddChild(_overTitle);
        _overText = Ui.L("", 15, Hex("#d6ddd1"), false, HorizontalAlignment.Left, true); v2.AddChild(_overText);
        _overStats = new GridContainer { Columns = 2 }; _overStats.AddThemeConstantOverride("h_separation", 40); v2.AddChild(_overStats);
        var hb = new HBoxContainer(); hb.AddThemeConstantOverride("separation", 12); v2.AddChild(hb);
        var again = new Button { Text = "  Jeszcze raz (R)  " };
        again.AddThemeStyleboxOverride("normal", Ui.Box(Hex("#cfe6c8"), new Color(0, 0, 0, 0), 10, 0, 10));
        again.AddThemeStyleboxOverride("hover", Ui.Box(Hex("#e2f3dc"), new Color(0, 0, 0, 0), 10, 0, 10));
        again.AddThemeColorOverride("font_color", Hex("#10160f")); again.AddThemeColorOverride("font_hover_color", Hex("#10160f"));
        again.Pressed += () => Start(_sc, _nano);
        var menu = new Button { Text = "  Menu  " }; menu.Pressed += ShowMenu;
        hb.AddChild(again); hb.AddChild(menu);

        // pauza
        _pause = Ui.Full(new Control { Theme = Ui.Theme, Visible = false }); L.AddChild(_pause);
        _pause.AddChild(Ui.Full(new ColorRect { Color = new Color(0, 0, 0, 0.4f) }));
        var c3 = Ui.Full(new CenterContainer()); _pause.AddChild(c3);
        var p3 = new PanelContainer { CustomMinimumSize = new Vector2(320, 0) }; c3.AddChild(p3);
        var v3 = new VBoxContainer(); v3.AddThemeConstantOverride("separation", 10); p3.AddChild(v3);
        v3.AddChild(Ui.L("Pauza", 28, null, true, HorizontalAlignment.Center));
        Button PB(string t, Action a) { var b = new Button { Text = t, CustomMinimumSize = new Vector2(0, 38) }; b.Pressed += () => { _audio.Play("tick"); a(); }; v3.AddChild(b); return b; }
        PB("Wznów (P / Esc)", () => SetPause(false));
        PB("Wiedza o organizmie (I)", () => { _pause.Visible = false; ToggleInfo(true); });
        PB("Ustawienia (O)", () => { _pause.Visible = false; ShowSettings(true); });
        PB("Zacznij od nowa", () => Start(_sc, _nano));
        PB("Wyjdź do menu", ShowMenu);
        v3.AddChild(Ui.L("N – dźwięk · K – minimapa · M – mikroskop · F11 – pełny ekran", 12, Ui.Dim, false, HorizontalAlignment.Center));
    }

    CheckButton _settingsMute; OptionButton _settingsMode;
    static Control Row(string label, Control c)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 16);
        var l = Ui.L(label, 15, null); l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; h.AddChild(l);
        h.AddChild(c); return h;
    }
    void ShowSettings(bool on)
    {
        _settings.Visible = on;
        if (on) { _settingsMute.SetPressedNoSignal(Settings.Muted); _settingsMode.Selected = Settings.WindowMode; }
        if (_mode == Mode.Game) { _paused = on || _pause.Visible; if (!on && !_pause.Visible) _paused = false; }
    }

    static string WorldName(Scenario sc) => sc.Id switch
    {
        "t4" => "Powierzchnia szkiełka z E. coli (TEM, nanoskala)",
        "bdello" => "Biofilm E. coli (obiektyw 100×)",
        "ecoli" => "Biofilm z glukozą i antybiotykiem (100×)",
        "neutro" => "Tkanka łączna z żyłkami (40×, time-lapse)",
        "para" => "Staw – orzęski (10×)",
        "didinium" => "Staw – drapieżniki (10×)",
        "amoeba" => "Staw – dno i ameby (10×, time-lapse)",
        _ => sc.Name,
    };

    void ShowInfo(Scenario sc)
    {
        if (sc == null)
        {
            _infoTitle.Text = "Źródła naukowe";
            _infoFacts.Text = "• Parametry organizmów (rozmiary, prędkości, cykle życiowe) zweryfikowano w literaturze poniżej. Tam, gdzie gra upraszcza biologię (sterowanie, skala czasu), jest to zaznaczone w opisie organizmu (klawisz I w grze).";
        }
        else
        {
            _infoTitle.Text = $"{sc.Name} – {sc.Latin}";
            _infoFacts.Text = string.Join("\n", sc.Facts.Select(f => "•  " + f));
        }
        _infoSources.Text = string.Join("\n", Scenario.Sources.Select(s => $"[url={s.Item2}][color=#9cd2ff]{s.Item1}[/color][/url]"));
        _info.Visible = true;
    }
    void ToggleInfo(bool? force = null)
    {
        bool show = force ?? !_info.Visible;
        if (show) ShowInfo(_mode == Mode.Game ? _sc : null); else _info.Visible = false;
        if (_mode == Mode.Game) { _paused = show; _pause.Visible = false; }
        if (!show && _mode == Mode.Game) _paused = false;
    }
    void SetPause(bool p) { _paused = p; _pause.Visible = p && !_info.Visible; }

    void OnGameOver(string text, bool win)
    {
        GetTree().CreateTimer(0.9).Timeout += () =>
        {
            if (_mode != Mode.Game || _world == null) return;
            _overTitle.Text = win ? "Zwycięstwo" : "Koniec gry";
            float score = _sc.Score(_world);
            bool rec = !_testInput && !_nano && Records.Submit(_sc.Id, score, _world.GoalIdx);
            _overText.Text = text + $"\nWynik: {score:0} {_sc.ScoreLabel}" + (rec ? "  ★ NOWY REKORD!" : $"  (rekord: {Records.Best(_sc.Id):0})");
            foreach (var ch in _overStats.GetChildren()) ch.QueueFree();
            var rows = _sc.Hud(_world); rows.Add(("Czas gry", $"{MathF.Round(_world.T)} s"));
            foreach (var r in rows) { _overStats.AddChild(Ui.L(r.Item1, 14, Ui.Dim)); _overStats.AddChild(Ui.L(r.Item2, 14, null, true, HorizontalAlignment.Right)); }
            _over.Visible = true;
        };
    }

    // ================================================================ gra
    void Start(Scenario sc) => Start(sc, false);
    void Start(Scenario sc, bool nano)
    {
        _sc = sc; _mode = Mode.Game; _paused = false; _modeIdx = 0; _nano = nano; _timeMul = 1;
        _nanoPick.Visible = false;
        _world = sc.Create();
        _world.Nano = nano;
        _world.Input = _input;
        _world.OnLog = Log; _world.OnBanner = Banner; _world.OnGameOver = OnGameOver;
        _world.OnSfx = (k, v, pan) => _audio.Play(k, v, pan);
        _world.OnEnterBiome = c => { if (_world.T > 1) Toast("Strefa: " + _sc.BiomeName(c.Biome)); };
        _world.OnGoal = g =>
        {
            Banner("Cel osiągnięty!", g.Text);
            Log("✔ Cel osiągnięty: " + g.Text, "good");
            _audio.Play("goal");
            if (!_testInput) Records.Submit(_sc.Id, _sc.Score(_world), _world.GoalIdx);
            if (_world.GoalIdx >= (_sc.Goals?.Length ?? 0)) _world.After(2.8f, () => Banner("Wszystkie cele ukończone!", "Graj dalej w trybie swobodnym i bij rekord"));
        };
        _audio.Ambient = sc.Em ? 1 : 0;
        sc.Init(_world);
        if (nano)
        {
            // gracz-organizm zastąpiony nanobotem w tym samym miejscu
            var org = _world.Player;
            float bx = org != null ? org.X : _world.CamX, by = org != null ? org.Y : _world.CamY;
            if (org != null) { org.Alive = false; _world.Ents.Remove(org); }
            _world.Player = null; _world.Focus = null;
            _world.SetPlayer(_world.Add(new Nanobot(_world, bx, by)));
            _world.Log($"Nanobot w świecie: {WorldName(sc)}. Spacja / PPM – laser na cel pod kursorem, najedź kursorem na komórkę, by ją zeskanować. 1–7 – zmiana skali, Z/X – tempo czasu.", "info");
        }
        _tz = _zoom = 1;
        var t = _world.CamTarget();
        _camX = t != null ? t.X : _world.W / 2; _camY = t != null ? t.Y : _world.H / 2;
        _view.World = _world; _overlay.World = _world; _overlay.ShowHud = true;
        ApplyStyle(sc.Style); ApplyMode();
        _menu.Visible = false; _over.Visible = false; _pause.Visible = false; _info.Visible = false; _hud.Visible = true;
        foreach (var ch in _log.GetChildren()) ch.QueueFree();
        _orgName.Text = sc.Name; _orgLatin.Text = sc.Latin; _orgType.Text = $"{sc.TypeName} · {sc.Size}"; _orgDot.Color = sc.Color;
        _controls.Text = "  " + sc.Controls + " · M – mikroskop · I – wiedza · Esc – pauza  ";
        if (nano)
        {
            _orgName.Text = "Nanobot"; _orgLatin.Text = WorldName(sc); _orgType.Text = $"Tryb eksploracji · Ø {Nanobot.Fmt(((Nanobot)_world.Player).Size)}"; _orgDot.Color = Hex("#8fe3ff");
            _controls.Text = "  WASD / LPM – lot · Shift – dopalacz · Spacja / PPM – laser · kursor – skaner · 1–7 – skala · Z/X – tempo czasu · M – mikroskop · I – wiedza · Esc – pauza  ";
        }
        _magLbl.Text = sc.Mag;
        _timeLbl.Text = (sc.TimeScale >= 1 ? $"Time-lapse ×{sc.TimeScale}" : $"Ruch zwolniony ×{sc.TimeScale}") + (sc.Bio > 1 ? $" · procesy życiowe ×{sc.Bio}" : "");
        _hudT = 0;
    }
    void ApplyMode()
    {
        var m = _sc.Modes[_modeIdx];
        _filter.Visible = m.Filter != 0;
        _filterMat.SetShaderParameter("mode", m.Filter);
        _modeLbl.Text = m.Name;
    }
    void CycleMode() { _modeIdx = (_modeIdx + 1) % _sc.Modes.Length; ApplyMode(); Toast("Mikroskop: " + _sc.Modes[_modeIdx].Name); }

    // ================================================================ wejście
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            var key = k.PhysicalKeycode;
            if (key == Key.Space) _actionEdge = true;
            if (key == Key.F) { _showFps = !_showFps; _fps.Visible = _showFps; Settings.ShowFps = _showFps; Settings.Save(); }
            if (key == Key.N) { _audio.Muted = !_audio.Muted; Settings.Muted = _audio.Muted; Settings.Save(); Toast(_audio.Muted ? "Dźwięk: WYŁ." : "Dźwięk: WŁ."); }
            if (key == Key.O) { ShowSettings(!_settings.Visible); return; }
            if (key == Key.Escape && _settings.Visible) { ShowSettings(false); return; }
            if (key == Key.V)
            {
                bool on = DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled;
                DisplayServer.WindowSetVsyncMode(on ? DisplayServer.VSyncMode.Disabled : DisplayServer.VSyncMode.Enabled);
                Toast(on ? "V-Sync: WYŁ. (bez limitu klatek)" : "V-Sync: WŁ. (odświeżanie monitora)");
            }
            if (key == Key.F11) { Settings.WindowMode = Settings.WindowMode == 0 ? 1 : 0; Settings.ApplyWindow(); Settings.Save(); Toast("Tryb: " + Settings.WindowModes[Settings.WindowMode]); }
            if (_mode == Mode.Game)
            {
                if (key == Key.Escape) { if (_info.Visible) ToggleInfo(false); else if (!_world.Over) SetPause(!_paused); }
                if (key == Key.K) { _overlay.ShowMap = !_overlay.ShowMap; Toast(_overlay.ShowMap ? "Minimapa: WŁ." : "Minimapa: WYŁ."); }
                if (key == Key.P) SetPause(!_paused);
                if (key == Key.M) CycleMode();
                if (key == Key.G && _world.Nutri != null) { _world.ShowField = !_world.ShowField; Toast(_world.ShowField ? "Mapa glukozy: WŁ." : "Mapa glukozy: WYŁ."); }
                if (key == Key.I || key == Key.Tab) ToggleInfo();
                if (key == Key.C) { _mouseFollow = !_mouseFollow; Toast(_mouseFollow ? "Sterowanie: podążanie za kursorem" : "Sterowanie: WASD / przytrzymaj LPM"); }
                if (key == Key.R && _world.Over) Start(_sc, _nano);
                if (_nano)
                {
                    int ki = key switch { Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, Key.Key5 => 4, Key.Key6 => 5, Key.Key7 => 6, _ => -1 };
                    if (ki >= 0 && ki < Scenario.All.Length) { Start(Scenario.All[ki], true); Toast("Skala: " + WorldName(_sc)); }
                    if (key == Key.Z) { _timeMul = Math.Max(0.125f, _timeMul / 2); Toast($"Tempo czasu ×{_timeMul:0.###}"); }
                    if (key == Key.X) { _timeMul = Math.Min(8, _timeMul * 2); Toast($"Tempo czasu ×{_timeMul:0.###}"); }
                }
            }
            else if (key == Key.Escape && _info.Visible) _info.Visible = false;
            else if (key == Key.Escape && _nanoPick.Visible) _nanoPick.Visible = false;
        }
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left && _mode == Mode.Game) _mouseDown = true;
            if (mb.ButtonIndex == MouseButton.Right) _actionEdge = true;
            if (mb.ButtonIndex == MouseButton.WheelUp) _tz = Clamp(_tz * 1.12f, 0.35f, 3.5f);
            if (mb.ButtonIndex == MouseButton.WheelDown) _tz = Clamp(_tz / 1.12f, 0.35f, 3.5f);
        }
    }
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton mb && !mb.Pressed && mb.ButtonIndex == MouseButton.Left) _mouseDown = false;
    }

    static bool K(Key k) => Input.IsPhysicalKeyPressed(k);

    void ComputeInput()
    {
        if (_testInput) return;
        var I = _input;
        float dx = 0, dy = 0;
        if (K(Key.W) || K(Key.Up)) dy -= 1;
        if (K(Key.S) || K(Key.Down)) dy += 1;
        if (K(Key.A) || K(Key.Left)) dx -= 1;
        if (K(Key.D) || K(Key.Right)) dx += 1;
        float mag = Hypot(dx, dy);
        if (mag > 0) { dx /= mag; dy /= mag; mag = 1; }
        var p = _world?.Player;
        if ((_mouseDown || _mouseFollow) && p != null && mag == 0 && !_paused)
        {
            float s = _s0 * _zoom; var ss = ScreenSize;
            float sx = (p.X - _camX) * s + ss.X / 2, sy = (p.Y - _camY) * s + ss.Y / 2;
            var mp = GetViewport().GetMousePosition();
            float vx = mp.X - sx, vy = mp.Y - sy, d = Hypot(vx, vy);
            if (d > 6) { dx = vx / d; dy = vy / d; mag = Clamp((d - 6) / (Math.Min(ss.X, ss.Y) * 0.2f), 0, 1); }
        }
        I.Mag = mag; I.Dx = dx * mag; I.Dy = dy * mag;
        if (mag > 0) I.Ang = MathF.Atan2(dy, dx);
        if (_world != null && !_paused)
        {
            float s2 = _s0 * _zoom; var ss2 = ScreenSize; var mp2 = GetViewport().GetMousePosition();
            I.AimX = _camX + (mp2.X - ss2.X / 2) / s2; I.AimY = _camY + (mp2.Y - ss2.Y / 2) / s2; I.HasAim = true;
        }
        I.Boost = K(Key.Shift);
        I.Action = _actionEdge; I.ActionHeld = K(Key.Space);
        _actionEdge = false;
    }

    // ================================================================ pętla
    void UpdateCam(World w, float fdt)
    {
        var ss = ScreenSize;
        _s0 = MathF.Sqrt(ss.X * ss.X + ss.Y * ss.Y) / (w.Sc.ViewW * 1.15f);
        _zoom = Lerp(_zoom, _tz, Damp(8, fdt));
        float s = _s0 * _zoom;
        var t = w.CamTarget();
        float tx = t != null ? t.X : _camX, ty = t != null ? t.Y : _camY;
        if (t == null && _mode == Mode.Menu) { tx = w.T * 1.2f; ty = MathF.Sin(w.T * 0.07f) * 25; }
        float hw = ss.X / 2 / s, hh = ss.Y / 2 / s;
        if (!w.Infinite)
        {
            tx = w.W > hw * 2 ? Clamp(tx, hw * 0.6f, w.W - hw * 0.6f) : w.W / 2;
            ty = w.H > hh * 2 ? Clamp(ty, hh * 0.6f, w.H - hh * 0.6f) : w.H / 2;
        }
        float k = Damp(_mode == Mode.Menu ? 0.8f : 6, fdt);
        _camX += (tx - _camX) * k; _camY += (ty - _camY) * k;
        w.CamX = _camX; w.CamY = _camY;
        _view.Stretch = Stretch;
        _view.SetCamera(_camX, _camY, s, ss);
        _overlay.CamX = _camX; _overlay.CamY = _camY; _overlay.S = s; _overlay.ScreenSize = ss; _overlay.Stretch = Stretch;
        w.ViewR = ss.X / 2 / s;
        var res = ss * Stretch;
        _bgMat.SetShaderParameter("resolution", res); _scopeMat.SetShaderParameter("resolution", res);
    }

    public override void _Process(double delta)
    {
        float fdt = Math.Min((float)delta, 1f / 25);
        _fpsAcc += (float)delta; _fpsN++;
        if (_fpsAcc > 0.5f)
        {
            _fps.Text = $"{Math.Round(_fpsN / _fpsAcc)} FPS · {_view.LastVertices / 1000}k wierzch. · {_view.LastBuildMs:0.0} ms";
            _fpsAcc = 0; _fpsN = 0;
        }
        ComputeInput();
        var w = _mode == Mode.Game ? _world : _demo;
        if (w == null) return;
        if (!(_mode == Mode.Game && _paused)) w.Update(_mode == Mode.Game && _nano ? fdt * _timeMul : fdt);
        UpdateCam(w, fdt);
        _view.QueueRedraw();
        _overlay.QueueRedraw();
        if (_mode == Mode.Game) UpdateHud(fdt);
    }

    // ================================================================ testy automatyczne
    async Task AutoTest(string[] args)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _testInput = true;
        World.Prof = args.Contains("--prof");
        int frames = 2400;
        foreach (var a in args) if (a.StartsWith("--frames=")) frames = int.Parse(a[9..]);
        GD.Print($"[autotest] start, {frames} klatek na scenariusz");
        bool ok = true;
        foreach (var sc in Scenario.All)
        {
            try
            {
                Start(sc);
                _world.OnGameOver += (t, _) => GD.Print($"[autotest] {sc.Id}: powód końca: {t}");
                _world.OnLog += (t, c) => { if (c == "bad") GD.Print($"[autotest] {sc.Id} log: {t}"); };
                var sw = System.Diagnostics.Stopwatch.StartNew();
                double upd = 0, build = 0; int builds = 0, maxV = 0;
                for (int f = 0; f < frames; f++)
                {
                    if (f % 120 == 0) { float ang = Rand(0, TAU); _input.Ang = ang; _input.Mag = Chance(0.8f) ? 1 : 0; _input.Dx = MathF.Cos(ang) * _input.Mag; _input.Dy = MathF.Sin(ang) * _input.Mag; }
                    _input.Action = f % 97 == 0; _input.ActionHeld = f % 300 < 60; _input.Boost = f % 400 < 100;
                    var t0 = sw.Elapsed.TotalMilliseconds;
                    _world.Update(1f / 60);
                    upd += sw.Elapsed.TotalMilliseconds - t0;
                    if (f % 10 == 0)
                    {
                        UpdateCam(_world, 1f / 60);
                        var t1 = sw.Elapsed.TotalMilliseconds;
                        maxV = Math.Max(maxV, _view.BuildOnly());
                        build += sw.Elapsed.TotalMilliseconds - t1; builds++;
                    }
                    if (_world.Over) { GD.Print($"[autotest] {sc.Id}: koniec gry po {f} klatkach"); break; }
                }
                if (World.Prof)
                {
                    foreach (var kv in World.ProfT.OrderByDescending(k => k.Value).Take(12)) GD.Print($"      {kv.Key,-28} {kv.Value / frames:0.000} ms/kl");
                    foreach (var kv in World.VertT.OrderByDescending(k => k.Value).Take(5)) GD.Print($"      {kv.Key,-28} {kv.Value / Math.Max(1, builds):0} wierzch./kl");
                    World.ProfT.Clear(); World.VertT.Clear();
                }
                string stats = string.Join(", ", _world.Stats.Where(kv => !kv.Key.StartsWith("amp")).Select(kv => $"{kv.Key}={kv.Value:0.#}"));
                GD.Print("      typy: " + string.Join(", ", _world.Ents.GroupBy(e => e.GetType().Name + (e is PhageT4 q ? ":" + q.State : "")).OrderByDescending(g => g.Count()).Take(6).Select(g => $"{g.Key}={g.Count()}")) + $" · chunki={_world.ChunkCount}");
                GD.Print($"[autotest] {sc.Id,-9} OK  update {upd / frames:0.000} ms/kl · geometria {build / Math.Max(1, builds):0.000} ms · max {maxV} wierzch. · encje {_world.Ents.Count} · t={_world.T:0}s · {stats}");
            }
            catch (Exception ex)
            {
                ok = false;
                GD.PrintErr($"[autotest] {sc.Id} BŁĄD: {ex}");
            }
        }
        GD.Print(ok ? "[autotest] WSZYSTKO OK" : "[autotest] BŁĘDY!");
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>Celowane testy mechanik (NEToza, ampicylina, Acanthamoeba, odporność na T4, ameba vs wrotek).</summary>
    async Task Checks()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _testInput = true;
        int fails = 0;
        void Ok(bool c, string what) { GD.Print((c ? "[check] OK   " : "[check] FAIL ") + what); if (!c) fails++; }
        void Run(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                var ct = _world.CamTarget(); if (ct != null) { _world.CamX = ct.X; _world.CamY = ct.Y; }
                _world.ViewR = _world.Sc.ViewW * 0.6f;
                _world.Update(1f / 60);
            }
        }
        // usuwa ruchome organizmy wokół encji (izolowany test)
        void Clear(Ent keep, float r) { foreach (var e in _world.Ents) if (e != keep && e is not Collagen && Hypot(e.X - keep.X, e.Y - keep.Y) < r + e.R) e.Alive = false; _world.Ents.RemoveAll(e => !e.Alive); }
        Scenario S(string id) => Scenario.All.First(s => s.Id == id);
        try
        {
            // 1. NEToza po przytrzymaniu Spacji
            Start(S("neutro")); var n0 = _world.Player;
            _input.Mag = 0; _input.ActionHeld = true; Run(120); _input.ActionHeld = false;
            Ok(_world.Stat("nets") >= 1 && _world.Ents.Any(e => e is Net && e.Alive), "NEToza tworzy sieć NET");
            Ok(_world.Player != null && _world.Player != n0, "po NETozie gracz przejmuje inny neutrofil");
            var net = _world.Ents.First(e => e is Net);
            _world.Add(new Staph(_world, net.X + 3, net.Y));
            Run(30);
            Ok(_world.Ents.Any(e => e is Staph s && s.State == St.Caught), "bakteria w zasięgu NET zostaje uwięziona");

            // 2. ampicylina → liza przy podziale
            Start(S("ecoli")); var ec = (EColi)_world.Player;
            _world.Amp.Blob(ec.X, ec.Y, 20, 2); ec.L = ec.L0 * 2 + 0.01f;
            _input.Mag = 0; Run(900);
            Ok(_world.Stat("ampkills") >= 1, "ampicylina lizuje dzielącą się komórkę (liza wybrzuszeniowa)");

            // 3. Acanthamoeba pożera Bdellovibrio
            Start(S("bdello")); var bd = _world.Player;
            var ac = _world.Add(new Acanth(_world, bd.X + 12, bd.Y));
            bd.Dr = 0; bd.A = 0;
            for (int i = 0; i < 300 && bd.State == St.Swim; i++) { bd.A = MathF.Atan2(ac.Y - bd.Y, ac.X - bd.X); _input.Mag = 1; _input.Ang = bd.A; _input.Dx = MathF.Cos(bd.A); _input.Dy = MathF.Sin(bd.A); _world.Update(1f / 60); }
            _input.Mag = 0; _input.Dx = _input.Dy = 0;
            Ok(bd.State == St.Cup || bd.State == St.Vac || !bd.Alive, $"Acanthamoeba pochłania Bdellovibrio (stan: {bd.State})");

            // 4. T4 odpada od odpornego mutanta
            Start(S("t4")); var ph = (PhageT4)_world.Player;
            foreach (var e in _world.Ents) { if (e is EColi ce) ce.Resistant = true; if (e is Omv || e is OtherPhage) e.Alive = false; }
            var cell = (EColi)_world.Ents.First(e => e is EColi);
            cell.ToWorld(0, cell.Wd / 2 + 0.2f, out float px, out float py);
            ph.X = px; ph.Y = py; ph.A = cell.A - PI / 2; ph.D = 0; ph.Dr = 0;
            _input.Mag = 0; Run(240);
            Ok(_world.Ents.Any(e => e is EColi ce && ce.KnownResistant) && _world.Stat("infected") == 0, "fag nie infekuje odpornego mutanta (wiązanie odwracalne)");

            // 5. ameba pochłania wrotka
            Start(S("amoeba")); var am = (Amoeba)_world.Player;
            Clear(am, 900);
            var rot = _world.Add(new Rotifer(_world, am.X + 260, am.Y, PI));
            _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0; Run(900); _input.Mag = 0; _input.Dx = 0;
            Ok(rot.State == St.Cup || rot.State == St.Vac || !rot.Alive, $"ameba pochłania wrotka (stan: {rot.State})");
            Run(1800);
            Ok(_world.Stat("rotifers") >= 1, "wrotek strawiony (statystyka)");

            // 6. wirczyk kurczy się przy dotyku
            Start(S("para")); Clear(_world.Player, 1500); var vt = _world.Add(new Vorticella(_world, _world.Player.X + 500, _world.Player.Y, 0, 30));
            _world.Update(1f / 60);
            float bx0 = vt.BellX, by0 = vt.BellY;
            var chl = _world.Add(new Chlamy(_world, vt.BellX, vt.BellY, 0));
            Run(3);
            Ok(Hypot(vt.BellX - bx0, vt.BellY - by0) > 30, "Vorticella zwija trzonek po dotknięciu");

            // 7. chunki: zamrażanie i odmrażanie przy oddaleniu i powrocie
            Start(S("para")); var pl = _world.Player;
            var marker = _world.Ents.FirstOrDefault(e => e.Fixed && e != pl) ?? _world.Ents.First(e => e != pl);
            float sx = pl.X, sy = pl.Y;
            int chunks0 = _world.ChunkCount;
            pl.X += _world.CS * 12; Run(20);
            Ok(marker.ParkedIn != null && !_world.Ents.Contains(marker), "oddalenie zamraża chunki startowe (organizmy zaparkowane)");
            Ok(_world.ChunkCount > chunks0, $"nowe chunki generowane w locie ({chunks0} → {_world.ChunkCount})");
            pl.X = sx; pl.Y = sy; Run(20);
            Ok(marker.ParkedIn == null && _world.Ents.Contains(marker), "powrót odmraża ten sam organizm (stan zachowany)");

            // 8. różnorodność biomów
            var biomes = new HashSet<int>();
            for (int cx = -20; cx <= 20; cx += 2) for (int cy = -20; cy <= 20; cy += 2) biomes.Add(_world.Sc.BiomeAt(_world, cx, cy));
            Ok(biomes.Count >= 3, $"biomy są zróżnicowane ({biomes.Count} typów w próbce)");

            // 9. daleka podróż – brak błędów i stała liczba aktywnych encji
            Start(S("bdello")); var bp = _world.Player;
            int maxEnts = 0;
            for (int i = 0; i < 40; i++) { bp.X += _world.CS * 1.1f; bp.Y += _world.CS * 0.4f; Run(10); maxEnts = Math.Max(maxEnts, _world.Ents.Count); }
            Ok(maxEnts < 900, $"liczba aktywnych encji ograniczona podczas podróży (maks. {maxEnts}, chunków: {_world.ChunkCount})");

            // 10. miękkie ciała: dwa pantofelki wciśnięte w siebie – membrany się spłaszczają, ciała częściowo nachodzą
            Start(S("para")); var pa = (Paramecium)_world.Player; Clear(pa, 700);
            var pb = _world.Add(new Paramecium(_world, pa.X, pa.Y + pa.Wd * 0.95f, 0)); pb.Speed = 0;
            pa.A = 0; _input.Mag = 0;
            float maxDef = 0, minGap = 1e9f;
            for (int i = 0; i < 40; i++)
            {
                pa.Y += 1.5f; pb.Y -= 1.5f; Run(1);
                for (int k = 0; k < pa.Mem.N; k++) maxDef = Math.Max(maxDef, Hypot(pa.Mem.Dx[k], pa.Mem.Dy[k]));
                if (Geo.PrimPrim(pa.Prim, pb.Prim, out float pen2, out _, out _)) minGap = Math.Min(minGap, -pen2);
            }
            Ok(maxDef > 2, $"membrany wgniatają się przy kontakcie (maks. deformacja {maxDef:0.0} µm)");
            Ok(minGap < -1, $"miękkie ciała mogą się częściowo wcisnąć (nachodzenie rdzeni {-minGap:0.0} µm)");

            // 11. Coleps rani pantofelka (gracza)
            Start(S("para")); var pc = _world.Player; Clear(pc, 600);
            for (int k = 0; k < 3; k++) _world.Add(new Protist(_world, Species.Coleps, pc.X + 30 + k * 10, pc.Y + (k - 1) * 30, PI));
            _input.Mag = 0; Run(400);
            Ok(pc.Hp < 0.95f || !pc.Alive, $"Coleps gryzie pantofelka (integralność {pc.Hp * 100:0}%)");

            // 12. blefaryzmina: ameba, która połknie Blepharisma, traci integralność błony
            Start(S("amoeba")); var am2 = (Amoeba)_world.Player; Clear(am2, 900);
            var bl = _world.Add(new Protist(_world, Species.Blepharisma, am2.X + 120, am2.Y, PI)); bl.Speed = 0;
            _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0; Run(600); _input.Mag = 0; _input.Dx = 0;
            Ok(bl.State is St.Cup or St.Vac || !bl.Alive, $"ameba pochłania Blepharisma (stan: {bl.State})");
            Ok(am2.Hp < 0.99f, $"toksyczna blefaryzmina uszkadza drapieżnika (integralność ameby {am2.Hp * 100:0}%)");

            // 13. T4 na wabiku OMV
            Start(S("t4")); var t4 = (PhageT4)_world.Player; Clear(t4, 3);
            var omv = _world.Add(new Omv(_world, t4.X + 0.2f, t4.Y)); omv.D = 0; t4.D = 0; t4.Dr = 0; t4.A = 0;
            _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0;
            for (int i = 0; i < 400 && t4.State == St.Free; i++) { t4.A = MathF.Atan2(omv.Y - t4.Y, omv.X - t4.X); _world.Update(1f / 60); }
            _input.Mag = 0; _input.Dx = 0;
            Ok(t4.State is St.Attach or St.Debris, $"T4 wiąże się nieodwracalnie z pęcherzykiem OMV (stan: {t4.State})");

            // 14. Stentor wciąga i połyka drobne organizmy
            Start(S("para")); var pp = _world.Player; Clear(pp, 2000);
            var stn = _world.Add(new Protist(_world, Species.Stentor, pp.X + 1200, pp.Y, 0, 1));
            stn.ToWorld(stn.L * 0.55f, 0, out float fx, out float fy);
            var eug = _world.Add(new Protist(_world, Species.Euglena, fx + 40, fy, 0)); eug.Speed = 0;
            Run(900);
            Ok(eug.State is St.Cup or St.Vac || !eug.Alive, $"Stentor wciąga wirem i połyka Euglenę (stan: {eug.State}, skurcz {stn.Contr:0.00}, odl. {Hypot(eug.X - fx, eug.Y - fy):0} µm, L={stn.L:0})");

            // 15. nanobot dezintegruje organizm laserem
            Start(S("para"), true); var bot = (Nanobot)_world.Player;
            var victim = _world.Add(new Protist(_world, Species.Blepharisma, bot.X + 150, bot.Y, 0)); victim.Speed = 0;
            _input.HasAim = true; _input.AimX = victim.X; _input.AimY = victim.Y; _input.ActionHeld = true; _input.Mag = 0;
            Run(60); _input.ActionHeld = false; _input.HasAim = false;
            Ok(!victim.Alive && _world.Stat("botkills") >= 1, "nanobot dezintegruje komórkę laserem");
            Ok(_world.Player == bot && bot.Alive && !_world.Over, "nanobot jest nieśmiertelny i gra się nie kończy");

            // 16. nanobot działa w każdej skali
            foreach (var s in Scenario.All) { Start(s, true); Run(30); if (!(_world.Player is Nanobot nb2 && nb2.Alive)) { Ok(false, "nanobot w świecie " + s.Id); } }
            Ok(true, "nanobot uruchamia się we wszystkich 7 skalach");

            // 18. ruch pantofelka: rozpędzanie, płynny obrót, wygięcie ciała na zakręcie, kontrola po schwytaniu
            Start(S("para")); var pm = (Paramecium)_world.Player; Clear(pm, 900);
            pm.A = 0; _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0;
            Run(6); float v01 = MathF.Abs(pm.Sw.V);
            Run(90); float v15 = MathF.Abs(pm.Sw.V);
            Ok(v01 < v15 * 0.6f && v15 > pm.Speed * 0.8f, $"pantofelek rozpędza się stopniowo ({v01:0} → {v15:0} µm/s)");
            float a0 = pm.A; _input.Ang = PI; _input.Dx = -1; Run(6);
            float turned = MathF.Abs(WrapA(pm.A - a0));
            Ok(turned < 0.5f, $"obrót nie jest natychmiastowy (po 0,1 s: {turned:0.00} rad)");
            float maxBend = 0; for (int i = 0; i < 60; i++) { Run(1); maxBend = Math.Max(maxBend, MathF.Abs(pm.Mem.Bend)); }
            Ok(maxBend > 1e-5f, $"ciało wygina się w łuk podczas skrętu (krzywizna {maxBend * 1e4:0.00}·10⁻⁴ /µm)");
            var hel = _world.Add(new Helio(_world, pm.X + 230, pm.Y)); Run(60);
            if (pm.State == St.Caught) { _input.Action = true; Run(1); _input.Action = false; }
            Ok(pm.State != St.Caught || pm.TrichCD > 0, "schwytany pantofelek może wystrzelić trichocysty");
            _input.Mag = 0; _input.Dx = 0;

            // 17. strzępka Candida niszczona przez sieć NET
            Start(S("neutro")); var nn = _world.Player;
            var hy = _world.Add(new CandidaHypha(_world, nn.X + 4, nn.Y, 0));
            _world.Add(new Net(_world, nn.X + 4, nn.Y, 24));
            Run(1500);
            Ok(!hy.Alive, "sieć NET niszczy strzępkę Candida (za dużą do fagocytozy)");
        }
        catch (Exception ex) { fails++; GD.PrintErr("[check] WYJĄTEK " + ex); }
        GD.Print(fails == 0 ? "[check] WSZYSTKO OK" : $"[check] BŁĘDÓW: {fails}");
        GetTree().Quit(fails == 0 ? 0 : 1);
    }

    async Task Shots(string[] args)
    {
        string list = args.First(a => a.StartsWith("--shot="))[7..];
        string outDir = args.FirstOrDefault(a => a.StartsWith("--out="))?[6..] ?? "user://";
        int frames = 240;
        foreach (var a in args) if (a.StartsWith("--frames=")) frames = int.Parse(a[9..]);
        for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (var item in list.Split(','))
        {
            var parts = item.Split(':');
            string id = parts[0]; int mode = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            float zoom = 1; int at = id.IndexOf('@'); if (at > 0) { zoom = float.Parse(id[(at + 1)..], System.Globalization.CultureInfo.InvariantCulture); id = id[..at]; }
            try
            {
                if (id == "menu") { ShowMenu(); for (int i = 0; i < 90; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
                else
                {
                    bool nano = id.StartsWith("nano-"); string bare = nano ? id[5..] : id;
                    if (bare == "turn") bare = "turn";
                    string sid = bare == "turn" ? "para" : bare.StartsWith("sp-") ? (bare is "sp-tetra" or "sp-bodo" or "sp-spirillum" ? "bdello" : bare is "sp-macro" or "sp-candida" or "sp-fat" ? "neutro" : "para") : bare switch { "vort" => "para", "spiro" => "amoeba", "net" => "neutro", "rotifer" => "didinium", _ => bare };
                    Start(Scenario.All.First(s => s.Id == sid), nano);
                    if (bare.StartsWith("sp-"))
                    {
                        var pl = _world.Player; float x = pl.X + _sc.ViewW * 0.12f, y = pl.Y;
                        Ent sp = bare switch
                        {
                            "sp-stentor" => new Protist(_world, Species.Stentor, x, y, -0.4f),
                            "sp-blepharisma" => new Protist(_world, Species.Blepharisma, x, y, 0.3f),
                            "sp-euglena" => new Protist(_world, Species.Euglena, x, y, 0.3f),
                            "sp-spirostomum" => new Protist(_world, Species.Spirostomum, x, y, 0.2f),
                            "sp-coleps" => new Protist(_world, Species.Coleps, x, y, 0.2f),
                            "sp-euplotes" => new Protist(_world, Species.Euplotes, x, y, 0.2f),
                            "sp-bursaria" => new Paramecium(_world, x, y, 0.2f, 0, 0, true),
                            "sp-lacry" => new Lacrymaria(_world, x, y, 0.2f),
                            "sp-helio" => new Helio(_world, x, y),
                            "sp-daphnia" => new Daphnia(_world, x, y, 0.2f),
                            "sp-volvox" => new Volvox(_world, x, y),
                            "sp-nema" => new Chain(_world, Chain.Type.Nematode, x, y, 0.2f, 700),
                            "sp-osc" => new Chain(_world, Chain.Type.Oscillatoria, x, y, 0.2f, 600),
                            "sp-tetra" => new Protist(_world, Species.Tetrahymena, x, y, 0.2f),
                            "sp-bodo" => new Protist(_world, Species.Bodo, x, y, 0.2f),
                            "sp-spirillum" => new Microbe(_world, MicroType.Spirillum, x, y, 0.2f),
                            "sp-macro" => new Macrophage(_world, x, y),
                            "sp-candida" => new CandidaHypha(_world, x, y, 0.2f),
                            "sp-fat" => new Adipocyte(_world, x, y, 40),
                            _ => null,
                        };
                        if (sp != null) { _world.Add(sp); _world.Focus = sp; if (sp is Protist pr) pr.Speed *= 0.2f; }
                    }
                    if (id == "vort") _world.Focus = _world.Ents.OfType<Vorticella>().FirstOrDefault();
                    if (id == "spiro") _world.Focus = _world.Ents.OfType<SpiroCell>().FirstOrDefault();
                    if (id == "rotifer") _world.Focus = _world.Ents.OfType<Rotifer>().FirstOrDefault();
                    if (id == "net") { _input.ActionHeld = true; for (int f = 0; f < 110; f++) _world.Update(1f / 60); _input.ActionHeld = false; _world.Focus = _world.Ents.OfType<Net>().First(); }
                    _modeIdx = mode % _sc.Modes.Length; ApplyMode();
                    _testInput = true;
                    _input.Mag = 1; _input.Ang = 0.3f; _input.Dx = MathF.Cos(0.3f); _input.Dy = MathF.Sin(0.3f);
                    for (int f = 0; f < frames; f++)
                    {
                        if (bare == "turn") { _input.Mag = 1; _input.Ang = f * 0.035f; _input.Dx = MathF.Cos(_input.Ang); _input.Dy = MathF.Sin(_input.Ang); _world.CamX = _world.Player.X; _world.CamY = _world.Player.Y; _world.ViewR = 500; }
                        _world.Update(1f / 60);
                        if (f == frames / 2 && bare != "turn") { _input.Mag = 0; _input.Dx = _input.Dy = 0; }
                    }
                    _testInput = false;
                    _tz = _zoom = zoom;
                    var ct = _world.CamTarget(); if (ct != null) { _camX = ct.X; _camY = ct.Y; }
                    for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var img = GetViewport().GetTexture().GetImage();
                string path = $"{outDir}/shot_{id}_{mode}.png";
                img.SavePng(path);
                GD.Print($"[shot] {path}");
            }
            catch (Exception ex) { GD.PrintErr($"[shot] {id} BŁĄD: {ex}"); }
        }
        GetTree().Quit();
    }
}

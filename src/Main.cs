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
    World _world;
    MenuDna _dna;
    bool _paused, _mouseFollow, _mouseDown, _actionEdge, _showFps, _testInput, _nano;
    /// <summary>Zrzuty i pomiary: wyniki i etapy nie trafiają do rekordów gracza.</summary>
    bool _noRecords;
    float _timeMul = 1;
    double _updMs;
    Control _nanoPick, _settings;
    int _modeIdx;
    float _camX, _camY, _zoom = 1, _tz = 1, _s0 = 1, _hudT, _fpsAcc; int _fpsN;
    readonly InputState _input = new();

    WorldView _view; Overlay _overlay;
    ColorRect _bg, _filter, _scope; ShaderMaterial _bgMat, _filterMat, _scopeMat;
    readonly Backdrop _backdrop = new();
    ScopeStyle _curStyle;

    // HUD
    Control _hud, _banner;
    Audio _audio;
    VBoxContainer _barsBox; readonly List<(Label l, ProgressBar b, StyleBoxFlat f)> _bars = new();
    Label _goalHdr, _goalText; ProgressBar _goalBar;
    Label _lineHdr, _lineText;
    PanelContainer _hudPanel; int _logMax = 6;
    readonly Dictionary<string, Label> _cardRec = new();
    Label _orgName, _orgLatin, _orgType, _modeLbl, _magLbl, _timeLbl, _ffLbl, _fps, _toast, _bannerT, _bannerS;
    ColorRect _orgDot; GridContainer _stats; VBoxContainer _log;
    readonly List<(Label k, Label v)> _statRows = new();
    // minimalny HUD (zawsze) i szczegóły (F8): pełny panel, prawy górny opis, radar, sterowanie, FPS
    VBoxContainer _mini, _miniBars; GridContainer _miniRows; Label _miniName, _miniGoal; ProgressBar _miniGoalBar; PanelContainer _trPanel;
    readonly List<(Label k, Label v)> _miniRowL = new(); readonly List<(Label l, ProgressBar b, StyleBoxFlat f)> _miniBarL = new();
    bool _debugUi;
    Tween _bannerTw, _toastTw;

    // menu / dialogi
    Control _menu, _info, _over, _pause;
    Label _infoTitle, _overTitle, _overText; RichTextLabel _infoFacts, _infoSources; GridContainer _overStats;

    public override void _ExitTree()
    {
        Loc.DumpMisses();
        // tekstury trzymane przez statyczne pola i tło – zwolnione jawnie (inaczej Godot zgłasza wyciek przy wyjściu)
        _backdrop.Free();
        SpriteAtlas.Tex?.Dispose(); SpriteAtlas.Tex = null;
    }

    public override void _Ready()
    {
        Settings.Load();
        var a0 = OS.GetCmdlineUserArgs();
        // --lang=en / --lang=pl nadpisuje ustawienie (testy, zrzuty ekranu)
        var la = a0.FirstOrDefault(x => x.StartsWith("--lang="));
        Loc.Set(la != null ? la[7..] : Settings.Lang);
        if (!a0.Any(a => a.StartsWith("--shot") || a.StartsWith("--trace") || a == "--check" || a == "--autotest" || a == "--physaudit" || a == "--pathaudit" || a == "--windowed")) Settings.ApplyWindow();
        _showFps = Settings.ShowFps;
        Ui.Init();
        DebrisAtlas.Ensure();
        _audio = new Audio(); AddChild(_audio);
        BuildLayers();
        BuildHud();
        BuildMenu();
        BuildDialogs();
        BuildInspect();
        _audio.Muted = Settings.Muted;
        _fps.Visible = false;
        ShowMenu();
        var args = OS.GetCmdlineUserArgs();
        if (args.Any(a => a.StartsWith("--audio="))) { Audio.Report(args.First(a => a.StartsWith("--audio="))[8..]); GetTree().Quit(); return; }
        if (args.Any(a => a.StartsWith("--fpstest="))) { _ = FpsTest(args.First(a => a.StartsWith("--fpstest="))[10..]); return; }
        if (args.Contains("--check")) _ = Checks();
        else if (args.Any(a => a.StartsWith("--trace="))) _ = Trace(args);
        else if (args.Contains("--autotest")) _ = AutoTest(args);
        else if (args.Contains("--physaudit")) _ = PhysAudit(args);
        else if (args.Contains("--pathaudit")) _ = PathAudit(args);
        else if (args.Any(a => a.StartsWith("--shot="))) _ = Shots(args);
    }

    // ================================================================ warstwy
    void BuildLayers()
    {
        var bgL = new CanvasLayer { Layer = -10 }; AddChild(bgL);
        _bgMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/scope_bg.gdshader") };
        _bg = Ui.Full(new ColorRect { Material = _bgMat, MouseFilter = Control.MouseFilterEnum.Ignore }); bgL.AddChild(_bg);

        // tło menu: helisy DNA nad nieruchomym, ciemnym polem widzenia (pod kartami, pod winietą)
        var dL = new CanvasLayer { Layer = -6 }; AddChild(dL);
        _dna = new MenuDna(); dL.AddChild(_dna);

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
        _hud = Ui.Full(new Control { Theme = Ui.ScopeTheme, MouseFilter = Control.MouseFilterEnum.Ignore }); L.AddChild(_hud);

        // minimalny HUD – jak opis na mikrografii: nazwa, 2–3 najważniejsze odczyty, paski życiowe i bieżący etap cyklu.
        // Pełne statystyki, radar, sterowanie i licznik FPS są pod F8 (szczegóły).
        _mini = new VBoxContainer { Position = new Vector2(24, 18), MouseFilter = Control.MouseFilterEnum.Ignore };
        _mini.AddThemeConstantOverride("separation", 3); _hud.AddChild(_mini);
        _miniName = Ui.SL("", 22, null, true); _mini.AddChild(_miniName);
        _miniRows = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        _miniRows.AddThemeConstantOverride("h_separation", 16); _miniRows.AddThemeConstantOverride("v_separation", 0); _mini.AddChild(_miniRows);
        _miniBars = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; _miniBars.AddThemeConstantOverride("separation", 1); _mini.AddChild(_miniBars);
        _miniGoal = Ui.SL("", 15, Hex("#ffd27a"), false, HorizontalAlignment.Left, true); _miniGoal.CustomMinimumSize = new Vector2(400, 0); _mini.AddChild(_miniGoal);
        _miniGoalBar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(400, 3), MouseFilter = Control.MouseFilterEnum.Ignore };
        _miniGoalBar.AddThemeStyleboxOverride("fill", Ui.Box(Hex("#ffd27a"), new Color(0, 0, 0, 0), 1, 0, 0)); _mini.AddChild(_miniGoalBar);

        var tl = new PanelContainer { Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(300, 0), Visible = false }; _hud.AddChild(tl); _hudPanel = tl;
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); tl.AddChild(v);
        var hdr = new HBoxContainer(); hdr.AddThemeConstantOverride("separation", 10); v.AddChild(hdr);
        _orgDot = new ColorRect { CustomMinimumSize = new Vector2(12, 12), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; hdr.AddChild(_orgDot);
        _orgName = Ui.SL("", 20, null, true); hdr.AddChild(_orgName);
        _orgLatin = Ui.SL("", 13, Ui.Dim); v.AddChild(_orgLatin);
        _orgType = Ui.SL("", 12, Ui.Dim); v.AddChild(_orgType);
        v.AddChild(new HSeparator());
        _stats = new GridContainer { Columns = 2 }; _stats.AddThemeConstantOverride("h_separation", 18); _stats.AddThemeConstantOverride("v_separation", 3); v.AddChild(_stats);
        _barsBox = new VBoxContainer(); _barsBox.AddThemeConstantOverride("separation", 2); v.AddChild(_barsBox);
        v.AddChild(new HSeparator());
        // linia: w ewolucji liczy się potomstwo, a nie „wygrana” osobnika
        _lineHdr = Ui.SL("TWOJA LINIA", 11, Hex("#9fe0a8"), true); v.AddChild(_lineHdr);
        _lineText = Ui.SL("", 13, null, false, HorizontalAlignment.Left, true); _lineText.CustomMinimumSize = new Vector2(260, 0); v.AddChild(_lineText);
        v.AddChild(new HSeparator());
        _goalHdr = Ui.SL("ETAP", 11, Hex("#ffd27a"), true); v.AddChild(_goalHdr);
        _goalText = Ui.SL("", 13, null, false, HorizontalAlignment.Left, true); _goalText.CustomMinimumSize = new Vector2(260, 0); v.AddChild(_goalText);
        _goalBar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5) };
        _goalBar.AddThemeStyleboxOverride("fill", Ui.Box(Hex("#ffd27a"), new Color(0, 0, 0, 0), 3, 0, 0));
        v.AddChild(_goalBar);

        var tr = new PanelContainer { Visible = false }; _hud.AddChild(tr); _trPanel = tr;
        tr.AnchorLeft = 1; tr.AnchorRight = 1; tr.AnchorTop = 0; tr.AnchorBottom = 0;
        tr.GrowHorizontal = Control.GrowDirection.Begin;
        tr.OffsetLeft = -16; tr.OffsetRight = -16; tr.OffsetTop = 16; tr.OffsetBottom = 16;
        var tv = new VBoxContainer(); tr.AddChild(tv);
        _modeLbl = Ui.SL("", 14, null, true, HorizontalAlignment.Right); tv.AddChild(_modeLbl);
        _magLbl = Ui.SL("", 13, Ui.Dim, false, HorizontalAlignment.Right); tv.AddChild(_magLbl);
        _timeLbl = Ui.SL("", 13, Ui.Dim, false, HorizontalAlignment.Right); tv.AddChild(_timeLbl);
        _ffLbl = Ui.SL("", 14, Hex("#ffd27a"), true, HorizontalAlignment.Right); tv.AddChild(_ffLbl);

        _log = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = Control.MouseFilterEnum.Ignore };
        _log.AddThemeConstantOverride("separation", 6);
        _hud.AddChild(_log);
        _log.AnchorLeft = 0; _log.AnchorRight = 0; _log.AnchorTop = 1; _log.AnchorBottom = 1;
        _log.OffsetLeft = 16; _log.OffsetRight = 590; _log.OffsetTop = -460; _log.OffsetBottom = -58;

        // lista klawiszy jest tylko w Ustawieniach → Sterowanie (bez podpowiedzi na ekranie gry)

        _banner = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        _hud.AddChild(_banner);
        // baner nad kadrem (nie zasłania organizmu gracza w środku ekranu)
        _banner.AnchorLeft = 0; _banner.AnchorRight = 1; _banner.AnchorTop = 0.1f; _banner.AnchorBottom = 0.1f;
        _bannerT = Ui.SL("", 42, null, true, HorizontalAlignment.Center); _banner.AddChild(_bannerT);
        _bannerS = Ui.SL("", 17, Hex("#e6ece0"), false, HorizontalAlignment.Center); _banner.AddChild(_bannerS);
        _bannerT.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f)); _bannerT.AddThemeConstantOverride("shadow_offset_y", 3);
        _bannerS.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));

        var topL = new CanvasLayer { Layer = 40 }; AddChild(topL);
        var topRoot = Ui.Full(new Control { Theme = Ui.ScopeTheme, MouseFilter = Control.MouseFilterEnum.Ignore }); topL.AddChild(topRoot);
        _toast = Ui.SL("", 14, null, false, HorizontalAlignment.Center);
        _toast.AddThemeStyleboxOverride("normal", Ui.Box(Ui.PanelBg, Ui.PanelBorder, 10, 1, 8));
        topRoot.AddChild(_toast);
        _toast.AnchorLeft = 0.5f; _toast.AnchorRight = 0.5f; _toast.OffsetTop = 18; _toast.GrowHorizontal = Control.GrowDirection.Both; _toast.Modulate = new Color(1, 1, 1, 0);
        _fps = Ui.SL("", 13, Hex("#cceeff"), true, HorizontalAlignment.Right);
        topRoot.AddChild(_fps);
        // licznik FPS u góry na środku – nie nachodzi na radar ani panele
        _fps.HorizontalAlignment = HorizontalAlignment.Center; _fps.AnchorLeft = 0.5f; _fps.AnchorRight = 0.5f; _fps.AnchorTop = 0; _fps.AnchorBottom = 0; _fps.OffsetLeft = -160; _fps.OffsetRight = 160; _fps.OffsetTop = 4; _fps.OffsetBottom = 22; _fps.Visible = false;
    }

    void Log(string text, string cls)
    {
        var pc = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var sb = Ui.Box(Ui.PanelBg, Ui.PanelBorder, 10, 1, 9);
        sb.BorderWidthLeft = 3; sb.BorderColor = cls == "good" ? Ui.Good : cls == "bad" ? Ui.Bad : Ui.Info;
        pc.AddThemeStyleboxOverride("panel", sb);
        var l = Ui.SL(text, 14, null, false, HorizontalAlignment.Left, true);
        pc.AddChild(l);
        _log.AddChild(pc);
        while (_log.GetChildCount() > _logMax) { var c = _log.GetChild(0); _log.RemoveChild(c); c.QueueFree(); }
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
        // dziennik zdarzeń mieści się pod lewym górnym opisem: tyle wpisów, ile starczy miejsca (w widoku podstawowym – do 3)
        if (_hudPanel != null)
        {
            Control topBox = _debugUi ? _hudPanel : _mini;
            float free = ScreenSize.Y - 64 - (topBox.Position.Y + topBox.Size.Y + 12);
            _logMax = Math.Clamp((int)(free / 52), 1, _debugUi ? 6 : 3);
            _log.OffsetTop = -Math.Max(60, free + 64);
            while (_log.GetChildCount() > _logMax) { var c = _log.GetChild(0); _log.RemoveChild(c); c.QueueFree(); }
        }
        var rows = _nano ? NanoRows() : _sc.Hud(_world);
        // minimalny HUD: najważniejsze odczyty wskazane przez tryb (albo dwa pierwsze)
        {
            var keys = _nano ? null : _sc.KeyRows; var kr = new List<(string, string)>();
            if (keys != null) foreach (var key in keys) foreach (var r in rows) if (r.Item1 == key) { kr.Add(r); break; }
            if (kr.Count == 0) kr.AddRange(rows.Take(2));
            while (_miniRowL.Count < kr.Count)
            {
                var k = Ui.SL("", 15, Ui.Dim); var val = Ui.SL("", 15);
                _miniRows.AddChild(k); _miniRows.AddChild(val); _miniRowL.Add((k, val));
            }
            for (int i = 0; i < _miniRowL.Count; i++)
            {
                bool vis = i < kr.Count; _miniRowL[i].k.Visible = vis; _miniRowL[i].v.Visible = vis;
                if (vis) { _miniRowL[i].k.Text = kr[i].Item1; _miniRowL[i].v.Text = kr[i].Item2; }
            }
        }
        if (_world.Infinite && _world.Player != null)
        {
            var pl = _world.Player;
            rows.Insert(0, ("Strefa", _sc.ZoneAt(_world, pl)));
            float dist = Hypot(pl.X, pl.Y);
            rows.Insert(1, ("Od startu", dist >= 1000 ? $"{dist / 1000:0.00} mm" : dist >= 1 ? $"{dist:0} µm" : $"{dist * 1000:0} nm"));
        }
        while (_statRows.Count < rows.Count)
        {
            var k = Ui.SL("", 14, Ui.Dim); var val = Ui.SL("", 14, null, true, HorizontalAlignment.Right);
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
        if (!_nano && plr != null && plr.Alive && plr is not Neutrophil && plr.Hp < 0.999f && !(bars?.Exists(b => b.label.StartsWith("Integralność")) ?? false))
        {
            bars ??= new();
            bars.Insert(0, ("Integralność błony", plr.Hp, plr.Hp < 0.35f ? Hex("#e05a4a") : Hex("#9fd0ff")));
        }
        int nb = bars?.Count ?? 0;
        while (_bars.Count < nb)
        {
            var l = Ui.SL("", 11, Ui.Dim);
            var b = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6) };
            var f = Ui.Box(Colors.White, new Color(0, 0, 0, 0), 3, 0, 0); b.AddThemeStyleboxOverride("fill", f);
            _barsBox.AddChild(l); _barsBox.AddChild(b); _bars.Add((l, b, f));
        }
        for (int i = 0; i < _bars.Count; i++)
        {
            bool vis = i < nb; _bars[i].l.Visible = vis; _bars[i].b.Visible = vis;
            if (!vis) continue;
            var (lab, val, col) = bars[i];
            _bars[i].l.Text = Loc.Up(lab); _bars[i].b.Value = Clamp(val, 0, 1); _bars[i].f.BgColor = col;
        }
        // minimalny HUD: dwa pierwsze paski (integralność, energia, cykl…)
        int mb = Math.Min(2, nb);
        while (_miniBarL.Count < mb)
        {
            var l = Ui.SL("", 12, Ui.Dim);
            var b = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(260, 4), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore };
            var f = Ui.Box(Colors.White, new Color(0, 0, 0, 0), 1, 0, 0); b.AddThemeStyleboxOverride("fill", f);
            _miniBars.AddChild(l); _miniBars.AddChild(b); _miniBarL.Add((l, b, f));
        }
        for (int i = 0; i < _miniBarL.Count; i++)
        {
            bool vis = i < mb; _miniBarL[i].l.Visible = vis; _miniBarL[i].b.Visible = vis;
            if (!vis) continue;
            var (lab, val, col) = bars[i];
            _miniBarL[i].l.Text = Loc.Up(lab); _miniBarL[i].b.Value = Clamp(val, 0, 1); _miniBarL[i].f.BgColor = col;
        }
        _lineHdr.Visible = _lineText.Visible = !_nano;
        if (!_nano) { _lineHdr.Text = _sc.Reproduces ? "TWOJA LINIA" : "TWOJE GENY – W GOSPODARZU"; _lineText.Text = LineSummary(_world, _sc, false); }
        var G = _nano ? null : _sc.Goals;
        if (_nano) { _goalHdr.Text = "TRYB EKSPLORACJI"; _goalText.Text = "Brak celów – zwiedzaj, skanuj i porównuj skale (1–9)."; _goalBar.Visible = false; }
        if (G != null)
        {
            if (_world.GoalIdx < G.Length)
            {
                var g = G[_world.GoalIdx];
                _goalHdr.Text = $"ETAP CYKLU ŻYCIOWEGO {_world.GoalIdx + 1}/{G.Length}";
                _goalText.Text = g.Text;
                _goalBar.Value = Clamp(g.Val(_world) / g.Target, 0, 1); _goalBar.Visible = true;
                _miniGoal.Text = $"{_world.GoalIdx + 1}/{G.Length} · {Loc.T(g.Text)}"; _miniGoalBar.Value = _goalBar.Value; _miniGoalBar.Visible = true;
            }
            else { _goalHdr.Text = "WSZYSTKIE ETAPY ZA TOBĄ"; _goalText.Text = $"Nie ma końca ani wygranej – linia trwa, dopóki się rozmnaża. Rekord: {Records.Best(_sc.Id):0} {_sc.ScoreLabel}"; _goalBar.Visible = false; _miniGoal.Text = _goalHdr.Text; _miniGoalBar.Visible = false; }
        }
        else { _miniGoal.Text = _nano ? "TRYB EKSPLORACJI" : ""; _miniGoalBar.Visible = false; }
        _ffLbl.Visible = _world.Ff > 1; _ffLbl.Text = $"▶▶ ×{_world.Ff}";
        UpdateDataBar();
    }

    /// <summary>Czas w naturze (s czasu organizmu) w czytelnej postaci.</summary>
    static string Dur(float s) => s < 90 ? $"{s:0} s" : s < 5400 ? $"{s / 60:0} min" : s < 172800 ? $"{s / 3600:0.#} h" : $"{s / 86400:0.#} doby";
    /// <summary>Podsumowanie linii gracza (HUD i ekran końca).</summary>
    static string LineSummary(World w, Scenario sc, bool final)
    {
        if (!sc.Reproduces)
        {
            string own = sc.SomaticLine(w);
            if (own != null) return final ? sc.SomaticNote : $"{sc.SomaticShort}\n{own}";
            string host = w.Severity < 0.25f ? "zakażenie opanowane" : w.Severity < 0.6f ? "zakażenie trwa" : "grozi sepsa";
            return final
                ? sc.SomaticNote
                : $"{sc.SomaticShort}\nGospodarz: {host} · {w.TempC:0.0} °C" + (w.LineBorn > 0 ? $"\nTwój klon: {w.LineNow} (szczyt {w.LinePeak}), podziałów: {w.LineBorn / 2}" : "");
        }
        string gt = w.GenTime > 0 ? Dur(w.GenTime) : "–";
        string t = $"Żyje: {w.LineNow} {sc.LineUnit} (szczyt {w.LinePeak})\nPokolenie: {w.MaxGen} · {sc.BirthWord}: {w.LineBorn}\nŚredni czas pokolenia: {gt}";
        if (!final && w.LineNow <= 1 && w.LineBorn == 0) t += "\nPrzeżycie to dopiero początek – liczy się potomstwo.";
        return t;
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
        _menu.AddChild(Ui.Full(new ColorRect { Color = new Color(0.02f, 0.03f, 0.035f, 0.3f), MouseFilter = Control.MouseFilterEnum.Ignore }));
        // bez przewijania: siatka kart skaluje się tak, by zawsze zmieścić się w oknie
        var margin = Ui.Full(new MarginContainer());
        foreach (var m in new[] { "margin_left", "margin_right" }) margin.AddThemeConstantOverride(m, 24);
        margin.AddThemeConstantOverride("margin_top", 22); margin.AddThemeConstantOverride("margin_bottom", 14);
        _menu.AddChild(margin);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6); margin.AddChild(v);
        var title = Ui.L("M I K R O K O S M O S", 54, Hex("#dcebd6"), true, HorizontalAlignment.Center);
        title.AddThemeColorOverride("font_shadow_color", new Color(0.4f, 0.6f, 0.4f, 0.35f)); title.AddThemeConstantOverride("shadow_outline_size", 12);
        v.AddChild(title);
        v.AddChild(Ui.L("Wybierz organizm – każdy żyje w innej skali, innym czasie i pod innym mikroskopem. Nie ma wygranej: jak w naturze liczy się tylko to, ilu potomków zostawisz", 15, Hex("#c8d2c2"), false, HorizontalAlignment.Center));
        var holder = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
        v.AddChild(holder);
        var grid = new GridContainer { Columns = 8 }; // 15 organizmów + nanobot – 2 rzędy
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
        var foot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; foot.AddThemeConstantOverride("separation", 20); v.AddChild(foot);
        foot.AddChild(Ui.L("Fizyka niskiej liczby Reynoldsa · ruchy Browna · miękkie ciała · Godot 4 + C#", 13, Hex("#9aa598")));
        var set = new LinkButton { Text = "Ustawienia" }; set.AddThemeColorOverride("font_color", Hex("#cfe6c8"));
        set.Pressed += () => ShowSettings(true);
        foot.AddChild(set);
        var lng = new LinkButton { Text = Loc.En ? "Polski" : "English", AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
        lng.AddThemeColorOverride("font_color", Hex("#cfe6c8"));
        lng.Pressed += () => SetLanguage(Loc.En ? "pl" : "en");
        foot.AddChild(lng);
        var src = new LinkButton { Text = "Źródła naukowe" }; src.AddThemeColorOverride("font_color", Hex("#cfe6c8"));
        src.Pressed += () => ShowInfo(null);
        foot.AddChild(src);
    }

    Control MakeCard(Scenario sc)
    {
        var b = new Button { CustomMinimumSize = new Vector2(300, 420), FocusMode = Control.FocusModeEnum.None };
        b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0.063f, 0.078f, 0.086f, 0.78f), new Color(1, 1, 1, 0.09f), 16, 1, 0));
        b.AddThemeStyleboxOverride("hover", Ui.Box(new Color(0.08f, 0.1f, 0.11f, 0.86f), sc.Color, 16, 2, 0));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(0.1f, 0.12f, 0.13f, 0.9f), sc.Color, 16, 2, 0));
        b.Pressed += () => { _audio.Play("tick"); Start(sc); };
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
        tv.AddChild(Ui.L(Loc.Up(sc.TypeName), 11, sc.Color, true));
        tv.AddChild(Ui.L(sc.Name, 21, null, true));
        tv.AddChild(Ui.L(sc.Latin, 13, Ui.Dim));
        var d = Ui.L(sc.Desc, 13, Hex("#d4dbcf"), false, HorizontalAlignment.Left, true); d.CustomMinimumSize = new Vector2(268, 0); d.MaxLinesVisible = 7; d.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        tv.AddChild(d);
        var rec = Ui.L("", 12, Hex("#ffd27a"), true); tv.AddChild(rec); _cardRec[sc.Id] = rec;
        Ui.IgnoreMouse(v);
        return b;
    }

    Control MakeNanoCard()
    {
        var col = Hex("#8fe3ff");
        var b = new Button { CustomMinimumSize = new Vector2(300, 420), FocusMode = Control.FocusModeEnum.None };
        b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0.05f, 0.08f, 0.1f, 0.82f), new Color(0.55f, 0.85f, 1, 0.25f), 16, 1, 0));
        b.AddThemeStyleboxOverride("hover", Ui.Box(new Color(0.07f, 0.1f, 0.13f, 0.9f), col, 16, 2, 0));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(0.09f, 0.12f, 0.15f, 0.9f), col, 16, 2, 0));
        b.Pressed += () => { _audio.Play("tick"); _nanoPick.Visible = true; };
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
        d.CustomMinimumSize = new Vector2(268, 0); d.MaxLinesVisible = 7; tv.AddChild(d);
        Ui.IgnoreMouse(v);
        return b;
    }

    void ShowMenu()
    {
        _mode = Mode.Menu; _world = null; _paused = false;
        _inspect = false; if (_insp != null) _insp.Visible = false; if (_overlay != null) _overlay.Inspect = false;
        _audio.Ambient = -1;
        foreach (var sc in Scenario.All)
        {
            float best = Records.Best(sc.Id); int g = Records.GoalsDone(sc.Id);
            _cardRec[sc.Id].Text = best > 0 ? $"★ Rekord: {best:0} {sc.ScoreLabel} · etapy {g}/{sc.Goals?.Length ?? 0}" : g > 0 ? $"Etapy cyklu: {g}/{sc.Goals?.Length ?? 0}" : "";
        }
        _menu.Visible = true; _hud.Visible = false; _over.Visible = false; _pause.Visible = false; _info.Visible = false; _nanoPick.Visible = false;
        _filter.Visible = false; _overlay.ShowHud = false;
        // menu: bez symulacji w tle – nieruchome pole widzenia i przepływające helisy DNA
        _view.World = null; _overlay.World = null; _dna.Visible = true; _overlay.QueueRedraw();
        ApplyStyle(ScopeStyle.Menu); _backdrop.Apply(_bgMat, null);
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
        _infoFacts = new RichTextLabel { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(740, 0) };
        _infoFacts.AddThemeFontSizeOverride("normal_font_size", 15); _infoFacts.AddThemeFontSizeOverride("bold_font_size", 15);
        v.AddChild(_infoFacts);
        v.AddChild(Ui.L("ŹRÓDŁA", 12, Ui.Dim, true));
        _infoSources = new RichTextLabel { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(740, 0) };
        _infoSources.AddThemeFontSizeOverride("normal_font_size", 13);
        _infoSources.MetaClicked += meta => OS.ShellOpen(meta.AsString());
        v.AddChild(_infoSources);
        var close = new Button { Text = "  Zamknij  ", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        close.Pressed += () => ToggleInfo(false);
        v.AddChild(close);

        // ustawienia
        _settings = Ui.Full(new Control { Theme = Ui.Theme, Visible = false }); L.AddChild(_settings);
        _settings.AddChild(Ui.Full(new ColorRect { Color = new Color(0, 0, 0, 0.55f) }));
        var cs2 = Ui.Full(new CenterContainer()); _settings.AddChild(cs2);
        var ps2 = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) }; cs2.AddChild(ps2);
        var vs2 = new VBoxContainer(); vs2.AddThemeConstantOverride("separation", 10); ps2.AddChild(vs2);
        vs2.AddChild(Ui.L("Ustawienia", 26, null, true));
        vs2.AddChild(Ui.L("JĘZYK / LANGUAGE", 12, Ui.Dim, true));
        var lang = new OptionButton { CustomMinimumSize = new Vector2(0, 34), AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled };
        foreach (var n in Loc.LangNames) lang.AddItem(n);
        lang.Selected = Math.Max(0, Array.IndexOf(Loc.Langs, Loc.Lang));
        lang.ItemSelected += i => SetLanguage(Loc.Langs[i]);
        vs2.AddChild(Row("Język / Language", lang));
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
        fpsC.Toggled += on => { Settings.ShowFps = _showFps = on; _fps.Visible = on && _debugUi; Settings.Save(); };
        vs2.AddChild(Row("Licznik FPS (w szczegółach – F8)", fpsC));
        vs2.AddChild(Ui.L("DŹWIĘK", 12, Ui.Dim, true));
        HSlider Sl(float v0, Action<float> set)
        {
            var sl = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = v0, CustomMinimumSize = new Vector2(220, 24) };
            sl.ValueChanged += x => { set((float)x); Settings.Save(); };
            return sl;
        }
        vs2.AddChild(Row("Głośność ogólna", Sl(Settings.Master, x => Settings.Master = x)));
        vs2.AddChild(Row("Efekty (organizmy)", Sl(Settings.Sfx, x => { Settings.Sfx = x; _audio.Play("pop", 0.8f); })));
        vs2.AddChild(Row("Tło (szum, serce)", Sl(Settings.Ambient, x => Settings.Ambient = x)));
        var noise = new OptionButton { CustomMinimumSize = new Vector2(0, 34) };
        foreach (var m in Settings.Noises) noise.AddItem(m);
        noise.Selected = Settings.Noise;
        noise.ItemSelected += i => { Settings.Noise = (int)i; Settings.Save(); };
        vs2.AddChild(Row("Rodzaj szumu tła", noise));
        var mute = new CheckButton { ButtonPressed = Settings.Muted };
        mute.Toggled += on => { Settings.Muted = _audio.Muted = on; Settings.Save(); };
        vs2.AddChild(Row("Wycisz wszystko", mute));
        // sterowanie: jedyne miejsce z listą klawiszy (ogólne + bieżącego trybu), wypełniane przy otwarciu ustawień
        vs2.AddChild(Ui.L("STEROWANIE", 12, Ui.Dim, true));
        var ctrlScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 250), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _ctrlGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _ctrlGrid.AddThemeConstantOverride("h_separation", 18); _ctrlGrid.AddThemeConstantOverride("v_separation", 3);
        ctrlScroll.AddChild(_ctrlGrid); vs2.AddChild(ctrlScroll);
        var closeS = new Button { Text = "  Zamknij  ", SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
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
        vn.AddChild(Ui.L("Rozmiar bota dopasuje się do świata (≈ 1/30 kadru).", 13, Ui.Dim, false, HorizontalAlignment.Left, true));
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
        var again = new Button { Text = "  Jeszcze raz  " };
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
        PB("Wznów", () => SetPause(false));
        PB("Wiedza o organizmie", () => { _pause.Visible = false; ToggleInfo(true); });
        PB("Ustawienia", () => { _pause.Visible = false; ShowSettings(true); });
        PB("Zacznij od nowa", () => Start(_sc, _nano));
        PB("Wyjdź do menu", ShowMenu);
    }

    CheckButton _settingsMute; OptionButton _settingsMode; GridContainer _ctrlGrid;
    /// <summary>Klawisze wspólne dla wszystkich trybów (lista w Ustawieniach → Sterowanie).</summary>
    static readonly (string key, string what)[] CommonKeys =
    {
        ("WASD / LPM", "ruch"), ("C", "ruch za kursorem"), ("Shift", "szybciej"), ("Spacja", "akcja organizmu"), ("PPM", "akcja (jak Spacja)"),
        ("Kółko myszy", "powiększenie (zatrzaski)"), ("F", "podgląd: zdjęcia i opisy"), ("M", "tryb mikroskopu"), ("G", "mapa pola (jeśli jest)"),
        ("I / Tab", "wiedza o organizmie"), ("F8", "szczegóły: statystyki, zdarzenia, radar"), ("K", "radar (w szczegółach)"),
        ("F3", "licznik FPS (w szczegółach)"), ("P / Esc", "pauza"), ("R", "jeszcze raz (po końcu gry)"), ("O", "ustawienia"),
        ("N", "dźwięk wł./wył."), ("V", "V-Sync"), ("F11", "pełny ekran"), ("L", "język (w menu)"),
    };
    void FillControls()
    {
        foreach (var ch in _ctrlGrid.GetChildren()) ch.QueueFree();
        void Add(string k, string w, bool hdr = false)
        {
            _ctrlGrid.AddChild(Ui.L(k, hdr ? 13 : 14, hdr ? Hex("#ffd27a") : Hex("#cfe6c8"), true));
            var l = Ui.L(w, hdr ? 13 : 14, hdr ? Hex("#ffd27a") : null, false, HorizontalAlignment.Left, true); _ctrlGrid.AddChild(l);
        }
        foreach (var (k, w) in CommonKeys) Add(k, w);
        if (_mode != Mode.Game || _sc == null) return;
        Add("", ""); Add(Loc.Up(_nano ? "Nanobot" : _sc.Name), "", true);
        string cs = _nano ? "WASD / LPM – lot · Shift – dopalacz · Spacja / PPM – laser · kursor – skaner · 1–9 – skala świata · Z / X – tempo czasu" : _sc.Controls;
        foreach (var part in Loc.T(cs).Split(" · "))
        {
            int i = part.IndexOf(" – ");
            if (i > 0) Add(part[..i], part[(i + 3)..]); else Add("", part);
        }
    }
    /// <summary>Zmiana języka: zapis w ustawieniach i przebudowa całej sceny (wszystkie etykiety powstają od nowa).</summary>
    void SetLanguage(string lang)
    {
        if (lang == Loc.Lang) return;
        Settings.Lang = lang; Settings.Save();
        Loc.Set(lang);
        GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
    }
    static Control Row(string label, Control c)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 16);
        var l = Ui.L(label, 15, null); l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; h.AddChild(l);
        h.AddChild(c); return h;
    }
    void ShowSettings(bool on)
    {
        _settings.Visible = on;
        if (on) { _settingsMute.SetPressedNoSignal(Settings.Muted); _settingsMode.Selected = Settings.WindowMode; FillControls(); }
        if (_mode == Mode.Game) { _paused = on || _pause.Visible; if (!on && !_pause.Visible) _paused = false; }
    }

    static string WorldName(Scenario sc) => sc.Id switch
    {
        "t4" => "Powierzchnia szkiełka z E. coli (TEM, nanoskala)",
        "bdello" => "Biofilm E. coli (obiektyw 100×)",
        "ecoli" => "Biofilm z glukozą i antybiotykiem (100×)",
        "neutro" => "Tkanka łączna z żyłkami (40×, time-lapse)",
        "macro" => "Tkanka – sprzątanie po zapaleniu (40×, time-lapse ×90)",
        "euglena" => "Staw – światło i cień, wiciowce (40×)",
        "para" => "Staw – orzęski (10×)",
        "didinium" => "Staw – drapieżniki (10×)",
        "amoeba" => "Staw – dno i ameby (10×, time-lapse)",
        "sperm" => "Drogi rodne kobiety: pochwa → szyjka → macica → jajowód (40×)",
        "cancer" => "Guz, zrąb i żyłki – kaskada przerzutowania (40×, time-lapse ×240)",
        "tryp" => "Krew żywiciela – świdrowce, erytrocyty i mucha tse-tse (40×)",
        "neuron" => "Kora mózgu zarodka myszy – od komory do ciała modzelowatego (40×)",
        _ => sc.Name,
    };

    void ShowInfo(Scenario sc)
    {
        if (sc == null)
        {
            _infoTitle.Text = "Źródła naukowe";
            _infoFacts.Text = Loc.T("• Parametry organizmów (rozmiary, prędkości, cykle życiowe) zweryfikowano w literaturze poniżej. Tam, gdzie gra upraszcza biologię (sterowanie, skala czasu), jest to zaznaczone w opisie organizmu (klawisz I w grze).");
        }
        else
        {
            _infoTitle.Text = $"{Loc.T(sc.Name)} – {Loc.T(sc.Latin)}";
            var facts = new List<string>();
            if (sc.ReproFact.Length > 0) facts.Add($"[b]{Loc.T("Rozmnażanie:")}[/b] " + Loc.T(sc.ReproFact));
            if (!sc.Reproduces && sc.SomaticNote.Length > 0) facts.Add($"[b]{Loc.T("Dlaczego tu nie chodzi o potomstwo komórki:")}[/b] " + Loc.T(sc.SomaticNote));
            facts.AddRange(sc.Facts.Select(Loc.T));
            if (sc.Fluid && !sc.Em) facts.AddRange(Scenario.PhysicsFacts.Select(Loc.T));
            _infoFacts.Text = string.Join("\n", facts.Select(f => "•  " + f));
        }
        _infoSources.Text = string.Join("\n", Scenario.Sources.Select(s => $"[url={s.Item2}][color=#9cd2ff]{Loc.T(s.Item1)}[/color][/url]"));
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
            // nie ma „zwycięstwa”: albo cykl życiowy się zamknął (np. zarodniki rozsiane), albo linia wygasła
            _overTitle.Text = _world.OverTitle.Length > 0 ? _world.OverTitle : win ? "Cykl życiowy zamknięty" : _sc.Reproduces ? "Twoja linia wygasła" : "Koniec";
            float score = _sc.Score(_world);
            bool rec = !_testInput && !_noRecords && !_nano && Records.Submit(_sc.Id, score, _world.GoalIdx);
            _overText.Text = Loc.T(text) + "\n\n" + string.Join("\n", LineSummary(_world, _sc, true).Split('\n').Select(Loc.T))
                + "\n\n" + Loc.T($"Dostosowanie: {score:0} {Loc.T(_sc.ScoreLabel)}") + (rec ? Loc.T("  ★ NOWY REKORD!") : Loc.T($"  (rekord: {Records.Best(_sc.Id):0})"));
            foreach (var ch in _overStats.GetChildren()) ch.QueueFree();
            var rows = _sc.Hud(_world); rows.Add(("Czas gry", $"{MathF.Round(_world.T)} s"));
            if (_sc.Reproduces) { rows.Insert(0, ("Najgłębsze pokolenie", _world.MaxGen.ToString())); rows.Insert(1, ($"Łącznie {_sc.BirthWord}", _world.LineBorn.ToString())); }
            foreach (var r in rows) { _overStats.AddChild(Ui.L(r.Item1, 14, Ui.Dim)); _overStats.AddChild(Ui.L(r.Item2, 14, null, true, HorizontalAlignment.Right)); }
            _over.Visible = true;
        };
    }

    // ================================================================ gra
    void Start(Scenario sc) => Start(sc, false);
    void Start(Scenario sc, bool nano)
    {
        _sc = sc; _mode = Mode.Game; _paused = false; _modeIdx = 0; _nano = nano; _timeMul = 1;
        _inspect = false; if (_insp != null) _insp.Visible = false; _overlay.Inspect = false;
        _nanoPick.Visible = false;
        // baner poprzedniego świata (np. etap cyklu życiowego) nie może przejść do nowego
        _bannerTw?.Kill(); _banner.Modulate = new Color(1, 1, 1, 0); _bannerT.Text = _bannerS.Text = "";
        _world = sc.Create();
        _world.Nano = nano;
        _world.Input = _input;
        _world.OnLog = Log; _world.OnBanner = Banner; _world.OnGameOver = OnGameOver;
        _world.OnSfx = (k, v, pan) => _audio.Play(k, v, pan);
        _beats = 0;
        _world.OnEnterBiome = c => { if (_world.T > 1) Toast("Strefa: " + _sc.BiomeName(c.Biome)); };
        _world.OnGoal = g =>
        {
            Banner("Etap cyklu życiowego", g.Text);
            Log("✔ " + Loc.T(g.Text), "good");
            _audio.Play("goal");
            if (!_testInput && !_noRecords) Records.Submit(_sc.Id, _sc.Score(_world), _world.GoalIdx);
            if (_world.GoalIdx >= (_sc.Goals?.Length ?? 0)) _world.After(2.8f, () => Banner("Cały cykl za Tobą", _sc.Reproduces ? "Nie ma wygranej – ewolucja liczy tylko potomków. Rozmnażaj się dalej." : "Gospodarz walczy dalej – broń go, by jego geny (i Twoje) przetrwały."));
        };
        _audio.Ambient = sc.Em ? 1 : 0;
        sc.Init(_world);
        _world.LineNow = _world.LinePeak = sc.LinePop(_world);
        if (nano)
        {
            // gracz-organizm zastąpiony nanobotem w tym samym miejscu
            var org = _world.Player;
            float bx = org != null ? org.X : _world.CamX, by = org != null ? org.Y : _world.CamY;
            if (org != null) { org.Alive = false; _world.Ents.Remove(org); }
            _world.Player = null; _world.Focus = null;
            _world.SetPlayer(_world.Add(new Nanobot(_world, bx, by)));
            _world.Log($"Nanobot w świecie: {WorldName(sc)}. Spacja / PPM – laser na cel pod kursorem, najedź kursorem na komórkę, by ją zeskanować. 1–9 – zmiana skali, Z/X – tempo czasu.", "info");
        }
        _tz = _zoom = 1;
        var t = _world.CamTarget();
        _camX = t != null ? t.X : _world.W / 2; _camY = t != null ? t.Y : _world.H / 2;
        _view.World = _world; _overlay.World = _world; _overlay.ShowHud = true; _dna.Visible = false;
        ApplyStyle(sc.Style); ApplyMode(); _backdrop.Apply(_bgMat, _world);
        _menu.Visible = false; _over.Visible = false; _pause.Visible = false; _info.Visible = false; _hud.Visible = true;
        foreach (var ch in _log.GetChildren()) ch.QueueFree();
        _orgName.Text = sc.Name; _orgLatin.Text = sc.Latin; _orgType.Text = $"{sc.TypeName} · {sc.Size}"; _orgDot.Color = sc.Color;
        _miniName.Text = Loc.Up(nano ? "Nanobot" : sc.Name);
        SetDebugUi(_debugUi, false);
        if (nano)
        {
            _orgName.Text = "Nanobot"; _orgLatin.Text = WorldName(sc); _orgType.Text = $"Tryb eksploracji · Ø {Nanobot.Fmt(((Nanobot)_world.Player).Size)}"; _orgDot.Color = Hex("#8fe3ff");
        }
        _magLbl.Text = sc.Mag;
        _timeLbl.Text = (sc.TimeScale >= 1 ? $"Time-lapse ×{sc.TimeScale}" : $"Ruch zwolniony ×{sc.TimeScale}") + (sc.Bio > 1 ? $" · procesy życiowe ×{sc.Bio}" : "");
        _hudT = 0; UpdateDataBar();
    }
    void ApplyMode()
    {
        var m = _sc.Modes[_modeIdx];
        _filter.Visible = m.Filter != 0;
        _filterMat.SetShaderParameter("mode", m.Filter);
        _modeLbl.Text = m.Name;
        // tryb może zmieniać tło (np. czarne w fluorescencji) i materię poza ostrością
        if (_world != null) { _world.ScopeFilter = m.Filter; ApplyStyle(_sc.StyleFor(m.Filter)); _backdrop.Apply(_bgMat, _world); }
        UpdateDataBar();
    }

    // ================================================================ szczegóły (F8), pasek danych, powiększenie
    void SetDebugUi(bool on, bool toast = true)
    {
        _debugUi = on;
        _hudPanel.Visible = on; _trPanel.Visible = on; _mini.Visible = !on;
        _overlay.Debug = on;
        _fps.Visible = on && _showFps;
        _log.Visible = on; // zdarzenia (dziennik) – tylko w szczegółach
        if (toast) Toast(on ? "Szczegóły (F8): WŁ." : "Szczegóły (F8): WYŁ.");
        _hudT = 0;
    }
    /// <summary>
    /// Pasek danych jak na mikrografii z mikroskopu elektronowego (np. „5.0 kV X15.0K”): tryb (PH – kontrast fazowy,
    /// DF – ciemne pole, BF – jasne pole, TEM), obiektyw albo napięcie przyspieszające, powiększenie, tempo czasu.
    /// </summary>
    void UpdateDataBar()
    {
        if (_sc == null || _overlay == null) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var m = _sc.Modes[_modeIdx % _sc.Modes.Length];
        string code = _nano ? "NANO" : _sc.Em ? (m.Filter == 4 ? "TEM NEG" : "TEM") : m.Filter switch { 5 => "PH", 1 => "DF", 6 => "DIC", 7 => "FL GFP", _ => "BF" };
        string opt = _sc.Em ? "80.0kV" : _sc.Objective > 0 ? $"{_sc.Objective}X" : "";
        _overlay.DataCode = opt.Length > 0 ? $"{code}  {opt}" : code;
        _overlay.DataMag = Scenario.MagStr(MagNow());
        float ts = _world?.TimeScale ?? 1;
        string t = ts > 1.001f ? "TL×" + ts.ToString("0.#", inv) : ts < 0.999f ? "SLOW×" + ts.ToString("0.###", inv) : "";
        if (_world != null && _world.Ff > 1) t += (t.Length > 0 ? "  " : "") + $"▶▶×{_world.Ff}";
        _overlay.DataTime = t;
    }
    // Powiększenie zmienia się skokami jak pokrętło zoomu z zatrzaskami: wartości z szeregu liczb normalnych R20 (co ~12%),
    // np. 400 → 450 → 500 → 560 → 630 …, więc na pasku danych są „okrągłe” powiększenia (X450, X1.12K).
    static readonly float[] R20 = { 1f, 1.12f, 1.25f, 1.4f, 1.6f, 1.8f, 2f, 2.24f, 2.5f, 2.8f, 3.15f, 3.55f, 4f, 4.5f, 5f, 5.6f, 6.3f, 7.1f, 8f, 9f };
    static float R20Val(int k) { int d = (int)MathF.Floor(k / 20f); return R20[k - d * 20] * MathF.Pow(10, d); }
    static int R20Idx(float v) => (int)MathF.Round(20 * MathF.Log10(Math.Max(v, 1e-6f)));
    float MagNow() => _sc == null ? 0 : R20Val(R20Idx(_sc.MagX * _tz));
    void ZoomStep(int dir)
    {
        int k = R20Idx(_sc.MagX * _tz) + dir;
        float z = R20Val(k) / _sc.MagX;
        if (z < 0.34f || z > 3.6f) { _audio.Play("detent_stop", 0.9f); return; } // skrajne położenie pokrętła
        _tz = z;
        _audio.Play(dir > 0 ? "detent_in" : "detent_out", 0.9f);
        _overlay.MagFlash = 1; UpdateDataBar();
    }
    void CycleMode() { _modeIdx = (_modeIdx + 1) % _sc.Modes.Length; ApplyMode(); Toast("Mikroskop: " + _sc.Modes[_modeIdx].Name); }

    // ================================================================ wejście
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            var key = k.PhysicalKeycode;
            if (key == Key.Space) _actionEdge = true;
            if (key == Key.L && _mode == Mode.Menu && !_settings.Visible) { SetLanguage(Loc.En ? "pl" : "en"); return; }
            if (key == Key.F3) { _showFps = !_showFps; _fps.Visible = _showFps && _debugUi; Settings.ShowFps = _showFps; Settings.Save(); }
            if (key == Key.F8 && _mode == Mode.Game) { SetDebugUi(!_debugUi); return; }
            if (key == Key.F && _mode == Mode.Game) { SetInspect(!_inspect); return; }
            if (key == Key.Escape && _inspect) { SetInspect(false); return; }
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
                else if (key == Key.G && _sc.FieldName.Length > 0) { _world.ShowField = !_world.ShowField; Toast(_sc.FieldName + (_world.ShowField ? ": WŁ." : ": WYŁ.")); }
                if (key == Key.I || key == Key.Tab) ToggleInfo();
                if (key == Key.C) { _mouseFollow = !_mouseFollow; Toast(_mouseFollow ? "Sterowanie: podążanie za kursorem" : "Sterowanie: klawiatura i przeciąganie myszą"); }
                if (key == Key.R && _world.Over) Start(_sc, _nano);
                if (_nano)
                {
                    int ki = key switch { Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, Key.Key5 => 4, Key.Key6 => 5, Key.Key7 => 6, Key.Key8 => 7, Key.Key9 => 8, _ => -1 };
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
            if (_inspect && _mode == Mode.Game && mb.ButtonIndex is MouseButton.Left or MouseButton.Right) { if (mb.ButtonIndex == MouseButton.Left) InspectClick(); return; }
            if (mb.ButtonIndex == MouseButton.Left && _mode == Mode.Game) _mouseDown = true;
            if (mb.ButtonIndex == MouseButton.Right) _actionEdge = true;
            if (mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && _mode == Mode.Game) ZoomStep(mb.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
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
        if (_inspect && _mode == Mode.Game) { tx = _inspX; ty = _inspY; }
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
        w.ViewR = ss.X / 2 / s; w.PxUm = 1f / (s * Stretch);
        var res = ss * Stretch;
        _bgMat.SetShaderParameter("resolution", res); _scopeMat.SetShaderParameter("resolution", res);
        _backdrop.Update(_bgMat, _camX, _camY, s * Stretch, fdt);
    }

    int _beats;
    double _procMs; long _procT0, _procT1;
    public override void _Process(double delta)
    {
        long tp0 = _procT0 = System.Diagnostics.Stopwatch.GetTimestamp();
        try { ProcessFrame(delta); } finally { _procT1 = System.Diagnostics.Stopwatch.GetTimestamp(); _procMs = (_procT1 - tp0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
    }
    void ProcessFrame(double delta)
    {
        float fdt = Math.Min((float)delta, 1f / 25);
        _fpsAcc += (float)delta; _fpsN++;
        if (_fpsAcc > 0.5f)
        {
            _fps.Text = $"{Math.Round(_fpsN / _fpsAcc)} FPS · {_view.LastVertices / 1000}k wierzch. · {_view.LastBuildMs:0.0} ms";
            _fpsAcc = 0; _fpsN = 0;
        }
        ComputeInput();
        if (_inspect) { _input.Mag = 0; _input.Dx = _input.Dy = 0; _input.Action = false; UpdateInspect(fdt); }
        if (_mode == Mode.Menu)
        {
            var res0 = ScreenSize * Stretch;
            _bgMat.SetShaderParameter("resolution", res0); _scopeMat.SetShaderParameter("resolution", res0);
            _dna.Px = 1f / Math.Max(0.01f, Stretch);
            _view.QueueRedraw();
            return;
        }
        var w = _world;
        if (w == null) return;
        long tu = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!(_mode == Mode.Game && (_paused || _inspect))) w.Update(_mode == Mode.Game && _nano ? fdt * _timeMul : fdt);
        _updMs = (System.Diagnostics.Stopwatch.GetTimestamp() - tu) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        // bicie serca (światy tkankowe): każde uderzenie w świecie → tony S1/S2
        if (_mode == Mode.Game && w.HeartBpm > 0 && w.Beats != _beats) { _beats = w.Beats; if (!w.Over) _audio.Heart(0.5f + 0.4f * w.Severity, w.Systole, w.Severity); }
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
        string only = args.FirstOrDefault(a => a.StartsWith("--only="))?[7..];
        foreach (var sc in Scenario.All)
        {
            if (only != null && !only.Split(',').Contains(sc.Id)) continue;
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
                    World.ProfT.Clear(); World.VertT.Clear(); Physics.CntN = Physics.CntY = Physics.CntPair = Physics.CntHit = 0;
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

    /// <summary>
    /// Przebieg rozgrywki bez okna (strojenie równowagi): `-- --trace=neutro,sperm [--frames=N] [--every=S] [--idle]`.
    /// Co S sekund gry wypisuje wiersze HUD scenariusza i najliczniejsze typy encji; --idle – gracz stoi w miejscu.
    /// </summary>
    async Task Trace(string[] args)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _testInput = true;
        int frames = 36000; float every = 30;
        foreach (var a in args) { if (a.StartsWith("--frames=")) frames = int.Parse(a[9..]); if (a.StartsWith("--every=")) every = float.Parse(a[8..], System.Globalization.CultureInfo.InvariantCulture); }
        bool idle = args.Contains("--idle");
        foreach (var id in args.First(a => a.StartsWith("--trace="))[8..].Split(','))
        {
            Start(Scenario.All.First(s => s.Id == id));
            _world.OnGameOver += (t, _) => GD.Print($"[trace] {id}: KONIEC: {t}");
            var sw = System.Diagnostics.Stopwatch.StartNew(); double upd = 0; float next = every;
            for (int f = 0; f < frames && !_world.Over; f++)
            {
                if (!idle && f % 120 == 0) { float ang = Rand(0, TAU); _input.Ang = ang; _input.Mag = Chance(0.8f) ? 1 : 0; _input.Dx = MathF.Cos(ang) * _input.Mag; _input.Dy = MathF.Sin(ang) * _input.Mag; }
                if (idle) { _input.Mag = 0; _input.Dx = _input.Dy = 0; }
                var ct = _world.CamTarget(); if (ct != null) { _world.CamX = ct.X; _world.CamY = ct.Y; }
                _world.ViewR = _world.Sc.ViewW * 0.6f;
                double t0 = sw.Elapsed.TotalMilliseconds; _world.Update(1f / 60); upd += sw.Elapsed.TotalMilliseconds - t0;
                if (_world.T >= next)
                {
                    next += every;
                    string rows = string.Join(" | ", _sc.Hud(_world).Select(r => $"{r.Item1}: {r.Item2}"));
                    string types = string.Join(", ", _world.Ents.Where(e => e.Alive).GroupBy(e => e.GetType().Name).OrderByDescending(g => g.Count()).Take(7).Select(g => $"{g.Key}={g.Count()}"));
                    GD.Print($"[trace] {id} t={_world.T:0}s ({f} kl., {upd / Math.Max(1, f):0.00} ms/kl) · {rows}\n        typy: {types} · etap {_world.GoalIdx}");
                }
            }
            GD.Print($"[trace] {id} koniec po t={_world.T:0}s, etapy {_world.GoalIdx}/{_sc.Goals?.Length ?? 0}, średnio {upd / Math.Max(1, frames):0.00} ms/kl");
        }
        GetTree().Quit();
    }

    /// <summary>
    /// Audyt kolizji: w każdym świecie losowe sterowanie, co 10 klatek wszystkie pary ciał w kadrze – nachodzenie ponad
    /// zamierzoną miękkość (część wgniecenia miękkich ciał jest fizyczna), znormalizowane mniejszym promieniem.
    /// </summary>
    async Task PhysAudit(string[] args)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _testInput = true;
        int frames = 1800;
        foreach (var a in args) if (a.StartsWith("--frames=")) frames = int.Parse(a[9..]);
        string only = args.FirstOrDefault(a => a.StartsWith("--only="))?[7..];
        var near = new List<Ent>();
        foreach (var sc in Scenario.All)
        {
            if (only != null && !only.Split(',').Contains(sc.Id)) continue;
            Start(sc);
            var pairs = new Dictionary<string, (int n, float sum, float max)>();
            int samples = 0, bad = 0; float worst = 0; string worstPair = "";
            for (int f = 0; f < frames; f++)
            {
                if (f % 120 == 0) { float ang = Rand(0, TAU); _input.Ang = ang; _input.Mag = Chance(0.8f) ? 1 : 0; _input.Dx = MathF.Cos(ang) * _input.Mag; _input.Dy = MathF.Sin(ang) * _input.Mag; }
                _input.Action = f % 97 == 0; _input.ActionHeld = f % 300 < 60; _input.Boost = f % 400 < 100;
                var ct = _world.CamTarget(); if (ct != null) { _world.CamX = ct.X; _world.CamY = ct.Y; }
                _world.ViewR = _world.Sc.ViewW * 0.6f;
                _world.Update(1f / 60);
                if (_world.Over) break;
                if (f % 10 != 0 || f < 120) continue;
                near.Clear();
                foreach (var e in _world.Ents) if (e.Alive && e.Solid && e.Holder == null && _world.OnScreen(e, 1.2f)) near.Add(e);
                for (int i = 0; i < near.Count; i++)
                    for (int j = i + 1; j < near.Count; j++)
                    {
                        var a = near[i]; var b = near[j];
                        if (a.Fixed && b.Fixed) continue;
                        if (Hypot(a.X - b.X, a.Y - b.Y) > a.R + b.R) continue;
                        if (!a.Collides(b) || !b.Collides(a) || a.Permeable(b) || b.Permeable(a)) continue;
                        if (!Geo.EntEnt(a, b, out float pen, out _, out _)) continue;
                        samples++;
                        float give = (a.Soft * a.Prim.R + b.Soft * b.Prim.R) * 0.3f, ex = pen - give;
                        float size = Math.Max(0.05f, Math.Min(Math.Min(a.Prim.R, b.Prim.R), Math.Min(a.Radius, b.Radius)));
                        float rel = ex / size;
                        if (rel <= 0.25f) continue;
                        bad++;
                        string k = string.CompareOrdinal(a.GetType().Name, b.GetType().Name) < 0 ? $"{a.GetType().Name}–{b.GetType().Name}" : $"{b.GetType().Name}–{a.GetType().Name}";
                        var v = pairs.TryGetValue(k, out var o) ? o : (n: 0, sum: 0f, max: 0f);
                        pairs[k] = (v.n + 1, v.sum + rel, Math.Max(v.max, rel));
                        if (rel > worst) { worst = rel; worstPair = k; }
                    }
            }
            GD.Print($"[phys] {sc.Id,-9} kontakty {samples,6} · nachodzenie > 25% promienia: {bad,5} ({(samples > 0 ? 100.0 * bad / samples : 0):0.0}%) · najgorsze {worst:0.00}× ({worstPair})");
            foreach (var kv in pairs.OrderByDescending(k => k.Value.n).Take(5)) GD.Print($"         {kv.Key,-34} {kv.Value.n,5}× · śr. {kv.Value.sum / kv.Value.n:0.00} · maks. {kv.Value.max:0.00}");
        }
        GetTree().Quit();
    }

    /// <summary>
    /// Audyt drożności: `-- --pathaudit [--only=id,..] [--secs=S] [--seed=N]`. W każdym świecie gracz idzie prosto w 8 kierunkach
    /// (świat od nowa dla każdego) przez S sekund gry. Okna 2 s, w których przeszedł &lt; 15% drogi swobodnej, to „utknięcia” –
    /// liczone są wtedy ciała, których dotyka (kto blokuje drogę). Drogę swobodną wyznacza najszybsze okno przebiegu.
    /// </summary>
    async Task PathAudit(string[] args)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _testInput = true;
        float secs = 40; int seed = 7;
        foreach (var a in args) { if (a.StartsWith("--secs=")) secs = float.Parse(a[7..], System.Globalization.CultureInfo.InvariantCulture); if (a.StartsWith("--seed=")) seed = int.Parse(a[7..]); }
        string only = args.FirstOrDefault(a => a.StartsWith("--only="))?[7..];
        bool verbose = args.Contains("--v"); string dirs = args.FirstOrDefault(a => a.StartsWith("--dirs="))?[7..];
        foreach (var sc in Scenario.All)
        {
            if (only != null && !only.Split(',').Contains(sc.Id)) continue;
            var block = new Dictionary<string, int>(); int winAll = 0, stuckAll = 0; var line = new System.Text.StringBuilder();
            for (int d = 0; d < 8; d++)
            {
                if (dirs != null && !dirs.Split(',').Contains((d * 45).ToString())) continue;
                M.Seed((ulong)(seed + d * 101));
                Start(sc);
                // --emt: komórka raka od razu po przejściu EMT (etap inwazji przez zrąb)
                if (args.Contains("--emt") && _world.Player is TumorCell tc0) { tc0.Emt = 1; tc0.Amoeboid = args.Contains("--amo"); }
                float ang = d * TAU / 8, ux = MathF.Cos(ang), uy = MathF.Sin(ang);
                var wins = new List<(float adv, Dictionary<string, int> touch)>();
                Ent pl = null; float wx = 0, wy = 0, wt = 0, x0 = 0, y0 = 0; var touch = new Dictionary<string, int>();
                int frames = (int)(secs * 60);
                for (int f = 0; f < frames && !_world.Over; f++)
                {
                    _input.Ang = ang; _input.Mag = 1; _input.Dx = ux; _input.Dy = uy; _input.Boost = false; _input.Action = false; _input.ActionHeld = false; _input.HasAim = false;
                    var ct = _world.CamTarget(); if (ct != null) { _world.CamX = ct.X; _world.CamY = ct.Y; }
                    _world.ViewR = _world.Sc.ViewW * 0.6f;
                    _world.Update(1f / 60);
                    var p = _world.CamTarget() ?? _world.Player; if (p == null) continue;
                    if (p != pl) { pl = p; wx = p.X; wy = p.Y; wt = _world.T; touch = new(); if (f == 0) { x0 = p.X; y0 = p.Y; } continue; }
                    if (f % 6 == 0)
                        foreach (var o in p.Nb)
                            if (o.Alive && o.Solid && o.Holder == null && p.Collides(o) && o.Collides(p) && Geo.EntEnt(p, o, out _, out _, out _))
                            { string k = o.GetType().Name + (o.Permeable(p) || p.Permeable(o) ? "~" : ""); touch[k] = touch.GetValueOrDefault(k) + 1; }
                    if (_world.T - wt >= 2)
                    {
                        float adv0 = (p.X - wx) * ux + (p.Y - wy) * uy;
                        if (verbose) GD.Print($"   {sc.Id} {d * 45}° t={_world.T:0} {p.GetType().Name} st={p.State} x={p.X:0.#} y={p.Y:0.#} adv={adv0:0.##} v=({p.Vx:0.###},{p.Vy:0.###}) dotyka: {string.Join(",", touch.Select(k => k.Key + "×" + k.Value))}");
                        wins.Add((adv0, touch));
                        wx = p.X; wy = p.Y; wt = _world.T; touch = new();
                    }
                }
                if (wins.Count == 0) { line.Append($" {d * 45,3}°:–"); continue; }
                float best = wins.Max(w => w.adv); int stuck = 0; float total = wins.Sum(w => w.adv);
                foreach (var (adv, t) in wins)
                {
                    if (best <= 0 || adv >= 0.15f * best) continue;
                    stuck++;
                    foreach (var kv in t) block[kv.Key] = block.GetValueOrDefault(kv.Key) + 1;
                    if (t.Count == 0) block["(nic)"] = block.GetValueOrDefault("(nic)") + 1;
                }
                winAll += wins.Count; stuckAll += stuck;
                line.Append($" {d * 45,3}°:{(best > 0 ? total / (best * wins.Count) : 0) * 100,3:0}%/{stuck}");
            }
            GD.Print($"[path] {sc.Id,-9} utknięcia {stuckAll}/{winAll} okien 2 s · kierunek: postęp względem swobodnego / okna utknięcia:{line}");
            if (block.Count > 0) GD.Print("         blokują: " + string.Join(", ", block.OrderByDescending(k => k.Value).Take(8).Select(k => $"{k.Key}×{k.Value}")));
        }
        GetTree().Quit();
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
        static string Pct0(float v) => $"{v * 100:0}%";
        try
        {
            if (System.Environment.GetEnvironmentVariable("MIKRO_CHECK_ONLY") == "new") goto newModes;
            if (System.Environment.GetEnvironmentVariable("MIKRO_CHECK_ONLY") == "sperm") goto spermTests;
            if (System.Environment.GetEnvironmentVariable("MIKRO_CHECK_ONLY") == "neuron") goto neuronTests;
            // 1. NEToza po przytrzymaniu Spacji
            Start(S("neutro")); var n0 = _world.Player;
            _input.Mag = 0; _input.ActionHeld = true; Run(120); _input.ActionHeld = false;
            Ok(_world.Stat("nets") >= 1 && _world.Ents.Any(e => e is Net && e.Alive), "NEToza tworzy sieć NET");
            Ok(_world.Player != null && _world.Player != n0, "po NETozie gracz przejmuje inny neutrofil");
            var net = _world.Ents.First(e => e is Net);
            _world.Add(new Staph(_world, net.X + 3, net.Y));
            Run(30);
            Ok(_world.Ents.Any(e => e is Staph s && s.State == St.Caught), "bakteria w zasięgu NET zostaje uwięziona");

            // 1b. zakażenie → rekrutacja: z żyłki wychodzą kolejne neutrofile i zbierają się przy bakteriach (rojenie, LTB4)
            Start(S("neutro")); var nr0 = _world.Player; var nsc0 = (ScNeutro)_sc; float bxs = nr0.X + 120, bys = nr0.Y;
            // ognisko w tkance, nie w świetle żyłki (żyłka jest falista – przesunięcie o 120 µm mogłoby w nią trafić)
            for (int k = 0; k < 40 && nsc0.InVessel(_world, bxs, bys, 30); k++) bys += (k % 2 == 0 ? 1 : -1) * (15 + 15 * k); // na przemian w górę i w dół, coraz dalej
            int nn0 = _world.Count(e => e is Neutrophil);
            for (int i = 0; i < 60; i++) _world.Add(new Staph(_world, bxs + Rand(-10, 10), bys + Rand(-10, 10)));
            // rój przy ognisku: największa liczba neutrofili przy nim w ciągu 40 s (po wybiciu bakterii rój się rozchodzi,
            // a świat ma też własne ogniska zakażenia, więc stan w ostatniej chwili nie mówi, czy komórki się zebrały)
            _input.Mag = 0; int nearB = 0;
            for (int t = 0; t < 80; t++) { Run(30); nearB = Math.Max(nearB, _world.Count(e => e is Neutrophil n && !n.Apoptotic && Hypot(e.X - bxs, e.Y - bys) < 70)); }
            int nn1 = _world.Count(e => e is Neutrophil n && !n.Apoptotic);
            Ok(nn1 >= nn0 + 6 && nearB >= 3, $"zakażenie ściąga neutrofile z krwi ({nn0} → {nn1} w okolicy, przy ognisku naraz do {nearB}; chemokiny {((ScNeutro)_sc).Chemo:0})");

            // 2. ampicylina → liza przy podziale
            Start(S("ecoli")); var ec = (EColi)_world.Player; Clear(ec, 40);
            _world.Amp.Blob(ec.X, ec.Y, 20, 2); ec.L = ec.DivL + 0.01f;
            _input.Mag = 0;
            for (int i = 0; i < 30; i++) { Run(30); foreach (var e in _world.Ents) if (e is Protist or Bdello or PhageT4 or Acanth or OtherPhage) e.Alive = false; }
            Ok(_world.Stat("ampkills") >= 1, "ampicylina lizuje dzielącą się komórkę (liza wybrzuszeniowa)");

            // 3. Acanthamoeba pożera Bdellovibrio
            Start(S("bdello")); var bd = _world.Player; Clear(bd, 40);
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
            // biofilm ma mikrokolonie osiadłych komórek (25 aktywnych chunków) – ważne, by liczba nie rosła bez końca
            Ok(maxEnts < 1400, $"liczba aktywnych encji ograniczona podczas podróży (maks. {maxEnts}, chunków: {_world.ChunkCount})");

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
            var eug = _world.Add(new Protist(_world, Species.Euglena, fx + 40, fy, 0)); eug.Speed = 0; stn.Habit = 0.95f; // przyzwyczajony: przypadkowe trącenia nie wywołują skurczu
            Run(900);
            Ok(eug.State is St.Cup or St.Vac || !eug.Alive, $"Stentor wciąga wirem i połyka Euglenę (stan: {eug.State}, skurcz {stn.Contr:0.00}, odl. {Hypot(eug.X - fx, eug.Y - fy):0} µm, L={stn.L:0})");

            // 15. nanobot dezintegruje organizm laserem
            Start(S("para"), true); var bot = (Nanobot)_world.Player; Clear(bot, 400);
            var victim = _world.Add(new Protist(_world, Species.Blepharisma, bot.X + 150, bot.Y, 0)); victim.Speed = 0;
            _input.HasAim = true; _input.AimX = victim.X; _input.AimY = victim.Y; _input.ActionHeld = true; _input.Mag = 0;
            Run(60); _input.ActionHeld = false; _input.HasAim = false;
            Ok(!victim.Alive && _world.Stat("botkills") >= 1, "nanobot dezintegruje komórkę laserem");
            Ok(_world.Player == bot && bot.Alive && !_world.Over, "nanobot jest nieśmiertelny i gra się nie kończy");

            // 16. nanobot działa w każdej skali
            foreach (var s in Scenario.All) { Start(s, true); Run(30); if (!(_world.Player is Nanobot nb2 && nb2.Alive)) { Ok(false, "nanobot w świecie " + s.Id); } }
            Ok(true, $"nanobot uruchamia się we wszystkich {Scenario.All.Length} światach");

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

            // 19. neutrofil przeciska się przez tkankę; inne neutrofile patrolują
            Start(S("neutro")); var np = (Neutrophil)_world.Player;
            var others = _world.Ents.OfType<Neutrophil>().Where(n => n != np).ToList();
            var start = others.Select(n => new Vector2(n.X, n.Y)).ToList();
            float sx0 = np.X, sy0 = np.Y; float dir = np.Y > ((ScNeutro)_sc).VesselY(_world, np.X, np.Y) ? PI / 2 : -PI / 2; // od żyłki w głąb tkanki
            // test dotyczy przeciskania się przez kolagen – inne komórki tkanki (mastocyty, fibroblasty, adipocyty) usuwamy z drogi
            foreach (var e in _world.Ents) if (e is MastCell or Fibroblast or Adipocyte or Macrophage or Neutrophil && e != np && MathF.Abs(e.X - sx0) < 60 && (e.Y - sy0) * MathF.Sin(dir) > -20) e.Alive = false;
            _world.Ents.RemoveAll(e => !e.Alive);
            _input.Mag = 1; _input.Ang = dir; _input.Dx = MathF.Cos(dir); _input.Dy = MathF.Sin(dir);
            for (int sec = 0; sec < 10; sec++)
            {
                Run(60);
                if (System.Environment.GetEnvironmentVariable("MIKRO_DEBUG") == "1")
                    GD.Print($"  t={sec} pos=({np.X:0.0},{np.Y:0.0}) v=({np.Vx:0.00},{np.Vy:0.00}) A={np.A:0.00} pol={np.Pol:0.00} hp={np.Hp:0.00} nb=" + string.Join(",", np.Nb.Select(o => o.GetType().Name)));
            }
            float moved = Hypot(np.X - sx0, np.Y - sy0), ideal = np.Vmax * _world.TimeScale * 10;
            Ok(moved > ideal * 0.45f, $"neutrofil przeciska się przez kolagen (przebył {moved:0} µm z ~{ideal:0} µm możliwych)");
            float om = 0; for (int i = 0; i < others.Count; i++) if (others[i].Alive) om = Math.Max(om, Hypot(others[i].X - start[i].X, others[i].Y - start[i].Y));
            Ok(om > 15, $"pozostałe neutrofile patrolują tkankę (maks. przesunięcie {om:0} µm w 10 s)");
            // gęsta tkanka (strefa „kolagen”, 18 włókien na chunk 100 µm): leukocyty wybierają największe oczka i nie utykają
            // (Lämmermann i in. 2008 – ruch niezależny od integryn i proteaz; Renkawitz i in. 2019 – droga najmniejszego oporu)
            foreach (var e in _world.Ents) if (e != np && !(e is Collagen) && Hypot(e.X - np.X, e.Y - np.Y) < 260) e.Alive = false;
            _world.Ents.RemoveAll(e => !e.Alive);
            for (int i = 0; i < 90; i++) _world.Add(new Collagen(_world, np.X + Rand(-130, 130), np.Y + MathF.Sin(dir) * Rand(10, 230), Rand(0, TAU), Rand(25, 80), Rand(0.5f, 0.9f)));
            float cx0 = np.X, cy0 = np.Y, stuck = 0;
            for (int sec = 0; sec < 10; sec++)
            {
                float qx = np.X, qy = np.Y; Run(60);
                if (Hypot(np.X - qx, np.Y - qy) < np.Vmax * _world.TimeScale * 0.25f) stuck++;
            }
            float dmoved = Hypot(np.X - cx0, np.Y - cy0);
            Ok(dmoved > ideal * 0.5f && stuck <= 1, $"neutrofil przechodzi przez gęsty kolagen ({dmoved:0} µm z ~{ideal:0} µm, utknięcia: {stuck} s z 10)");
            _input.Mag = 0; _input.Dx = _input.Dy = 0;

            // 20. rozwielitka: nie da się wpłynąć w jej pancerz
            Start(S("para")); var pd = (Paramecium)_world.Player; Clear(pd, 3000);
            var dap = _world.Add(new Daphnia(_world, pd.X + 1400, pd.Y, 0)); dap.FlowK = 0;
            float minD = 1e9f;
            _input.Mag = 1;
            for (int i = 0; i < 900; i++)
            {
                float toward = MathF.Atan2(dap.Y - 0.04f * dap.Length - pd.Y, dap.X - 0.1f * dap.Length - pd.X);
                _input.Ang = toward; _input.Dx = MathF.Cos(toward); _input.Dy = MathF.Sin(toward);
                Run(1); if (!pd.Alive) break;
                var h = Geo.PointEnt(pd.X, pd.Y, dap); minD = Math.Min(minD, h.D);
            }
            _input.Mag = 0; _input.Dx = _input.Dy = 0;
            Ok(minD > -pd.Wd * 0.5f || !pd.Alive, $"pantofelek nie wnika w pancerz rozwielitki (najgłębiej {-minD:0} µm)");

            // 21. przepływ wokół pływaka (squirmer, siła netto 0): przed nim woda odpychana do przodu, z boku płynie do tyłu,
            //     a drobina nie „jedzie” razem z komórką jak za holowaną kulą; rozpad komórki
            Start(S("para")); var sp2 = (Paramecium)_world.Player; Clear(sp2, 1200);
            sp2.A = 0; sp2.Sw.V = sp2.Speed;
            var det = _world.Add(new Detritus(_world, sp2.X + sp2.L * 1.1f, sp2.Y, 8)); det.D = 0; det.FlowK = 1;
            var det2 = _world.Add(new Detritus(_world, sp2.X, sp2.Y + sp2.Wd * 1.3f, 8)); det2.D = 0; det2.FlowK = 1;
            float dx0 = det.X, dx1 = det2.X, px0 = sp2.X;
            // staw ma własny wolny prąd (~8 µm/s) – przesunięcie drobin liczone względem niego, żeby zostało samo pole pływaka
            float bgA = 0, bgS = 0;
            _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0;
            for (int f = 0; f < 12; f++)
            {
                _world.FlowAt(det.X, det.Y, out float bfa, out _); _world.FlowAt(det2.X, det2.Y, out float bfs, out _);
                bgA += bfa * _world.TimeScale / 60f; bgS += bfs * _world.TimeScale / 60f; Run(1);
            }
            _input.Mag = 0; _input.Dx = 0;
            float ahead = det.X - dx0 - bgA, side = det2.X - dx1 - bgS, self = sp2.X - px0;
            Ok(ahead > 0.05f && side < -0.1f && ahead < self * 0.5f, $"pole squirmera: drobina przed pantofelkiem odpychana naprzód ({ahead:0.0} µm), obok – do tyłu ({side:0.0} µm), komórka przebyła {self:0} µm");
            var vic = _world.Add(new Protist(_world, Species.Blepharisma, sp2.X - 400, sp2.Y, 0)); Run(2); vic.Kill();
            Ok(!vic.Alive && _world.Ents.Any(e => e is Lysis), "śmierć komórki uruchamia rozpad (pęcherze → pęknięcie → wypływ cytoplazmy)");
            Run(150); int bits = _world.Fx.Bits.Count;
            Ok(bits > 10, $"cytoplazma wypływa przez otwór ({bits} drobin)");

            // 22. krętek porusza się płynnie (bez skoków) także przy zderzeniach
            Start(S("bdello")); var bp2 = _world.Player; Clear(bp2, 80);
            var spir = _world.Add(new Microbe(_world, MicroType.Spirillum, bp2.X + 30, bp2.Y, 0));
            for (int k = 0; k < 6; k++) _world.Add(new EColi(_world, spir.X + Rand(-15, 15), spir.Y + Rand(-4, 4), Rand(0, TAU)));
            float maxStep = 0, px2 = spir.X, py2 = spir.Y;
            for (int i = 0; i < 400; i++) { Run(1); maxStep = Math.Max(maxStep, Hypot(spir.X - px2, spir.Y - py2)); px2 = spir.X; py2 = spir.Y; }
            float allowed = spir.Speed * _world.TimeScale / 60 * 4 + 0.5f;
            Ok(maxStep < allowed, $"krętek porusza się płynnie (maks. krok {maxStep:0.00} µm/klatkę, limit {allowed:0.00})");

            // 23. zderzenie mimo środka obraca pałeczkę (moment siły w cieczy lepkiej)
            Start(S("ecoli")); var ep = _world.Player; Clear(ep, 60);
            var rod = _world.Add(new EColi(_world, ep.X + 20, ep.Y, 0, 2.6f, 0, 0, true)); rod.D = 0; rod.Dr = 0;
            var hit = _world.Add(new EColi(_world, rod.X + 1.1f, rod.Y - 2.2f, PI / 2, 2.2f, 0, 0, true)); hit.D = 0; hit.Dr = 0;
            float a00 = rod.A;
            for (int i = 0; i < 120; i++) { hit.Y += 0.02f; hit.A = PI / 2; Run(1); }
            Ok(MathF.Abs(WrapA(rod.A - a00)) > 0.1f, $"pałeczka uderzona w koniec obraca się ({MathF.Abs(WrapA(rod.A - a00)) * 57.3f:0} °)");

            // 17. strzępka Candida niszczona przez sieć NET
            Start(S("neutro")); var nn = _world.Player;
            var hy = _world.Add(new CandidaHypha(_world, nn.X + 4, nn.Y, 0));
            _world.Add(new Net(_world, nn.X + 4, nn.Y, 24));
            Run(1500);
            Ok(!hy.Alive, "sieć NET niszczy strzępkę Candida (za dużą do fagocytozy)");

            // 31–32. serce: przepływ pulsuje z każdym skurczem; zakażenie → gorączka i tachykardia
            Start(S("neutro")); var hp0 = _world.Player;
            float vmin = 1e9f, vmax = 0; int b0 = _world.Beats;
            for (int i = 0; i < 300; i++) { Run(1); vmin = Math.Min(vmin, _world.BloodV); vmax = Math.Max(vmax, _world.BloodV); }
            int nb = _world.Beats - b0;
            // 70/min to 5,8 uderzenia na 5 s: zależnie od fazy i zmienności rytmu (HRV ±4%) liczy się 5 albo 6; 4–7 = 50–84/min
            Ok(nb >= 4 && nb <= 7 && vmax > vmin * 2, $"serce bije ~70/min ({nb} uderzeń w 5 s), krew przyspiesza po skurczu ({vmin:0}–{vmax:0} µm/s)");
            float hr0 = _world.HeartBpm;
            // odpowiedź ogólnoustrojowa sprawdzana osobno od rekrutacji: bez fagocytów i bez napływu neutrofili z krwi zakażenie narasta
            foreach (var e in _world.Ents) if (e != hp0 && (e is Neutrophil || e is Macrophage)) e.Alive = false;
            ((ScNeutro)_sc).NoRecruit = true;
            for (int i = 0; i < 260; i++) { var st = _world.Add(new Staph(_world, hp0.X + 150 + Rand(-40, 40), hp0.Y + 60 + Rand(-40, 40))); st.CycK = 1000; }
            float tmax = 0, hmax = 0;
            for (int i = 0; i < 80; i++) { Run(60); tmax = Math.Max(tmax, _world.TempC); hmax = Math.Max(hmax, _world.HeartBpm); }
            Ok(tmax > 38.3f && hmax > 95, $"zakażenie → gorączka {tmax:0.0} °C i tętno {hmax:0}/min (było {hr0:0})");

            // 33–35. niesporczak: wysysanie glonów, beczułka przeżywa wyschnięcie, bez niej – śmierć
            Start(S("tardi")); var td = (Tardigrade)_world.Player; var tsc = (ScTardi)_world.Sc; Clear(td, 400);
            tsc.Algae.Patch(td.X + MathF.Cos(td.A) * (td.L / 2 + 5), td.Y + MathF.Sin(td.A) * (td.L / 2 + 5), 3, 20);
            _input.Mag = 0; Run(300);
            Ok(_world.Stat("algae") >= 3, $"niesporczak przebija sztyletami i wysysa glony ({_world.Stat("algae"):0})");
            var td2 = _world.Add(new Tardigrade(_world, td.X + 150, td.Y + 150, 0)); td2.Energy = 0.8f;
            // drapieżny Milnesium z dalszej części mchu mógłby w trakcie testu dopaść gracza, zanim ten się zwinie – usuwamy go
            foreach (var e in _world.Ents) if (e is Tardigrade { Predator: true }) e.Alive = false;
            td.TunWant = true; td.Energy = 0.8f; Run(600);
            Ok(td.IsTun, $"Spacja zwija niesporczaka w beczułkę (tun {td.Tun:0.00})");
            // przeżycie wyschnięcia jest wysoce prawdopodobne, ale nie pewne (97% z zapasami energii) – sprawdzane na 10 beczułkach
            var tuns = new List<Tardigrade> { td };
            for (int i = 0; i < 9; i++) { var tt = _world.Add(new Tardigrade(_world, td.X - 160 + i * 36, td.Y - 220, 0)); tt.Energy = 0.8f; tt.Tun = 1; tuns.Add(tt); }
            tsc.Moisture = 0; typeof(ScTardi).GetMethod("Desiccate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(tsc, new object[] { _world });
            td2.TunWant = false;
            int tunsAlive = tuns.Count(t => t.Alive);
            Ok(tunsAlive >= 8 && td.Alive == (_world.Stat("survived") >= 1), $"beczułki przeżywają wyschnięcie (anhydrobioza): {tunsAlive} z {tuns.Count}{(td.Alive ? ", w tym gracz" : "")}");
            Ok(!td2.Alive, "niesporczak bez beczułki wysycha na śmierć");

            // 36a. Dictyostelium: faza wegetatywna – zjadanie bakterii
            Start(S("dicty")); var dp = _world.Player;
            for (int f = 0; f < 1500; f++) { if (((ScDicty)_world.Sc).FoodPoint(_world, out float bx2, out float by2)) { float a3 = MathF.Atan2(by2 - dp.Y, bx2 - dp.X); _input.Mag = 1; _input.Ang = a3; _input.Dx = MathF.Cos(a3); _input.Dy = MathF.Sin(a3); } Run(1); }
            _input.Mag = 0;
            Ok(_world.Stat("bact") >= 15, $"ameba zjada bakterie z murawy ({_world.Stat("bact"):0})");

            // 36. Dictyostelium: fale cAMP rozchodzą się od centrów agregacji
            Start(S("dicty")); var dsc = (ScDicty)_world.Sc; dsc.DebugAgg(_world);
            int maxEx = 0; for (int i = 0; i < 40; i++) { Run(30); maxEx = Math.Max(maxEx, dsc.Camp.ExcitedCount()); }
            Ok(maxEx > 300, $"fale cAMP rozchodzą się od centrów (maks. {maxEx} z 4096 próbek w fazie pobudzenia)");
            // 37–39. pełny cykl: agregacja → ślimak → światło → owocnik (gracz idzie za wskaźnikiem)
            bool fin = false; string fend = ""; _world.OnGameOver += (t, win) => { fin = true; fend = t; };
            for (int f = 0; f < 60 * 400 && !fin; f++)
            {
                if (_world.Player != null && dsc.FoodPoint(_world, out float fpx, out float fpy))
                {
                    float a2 = MathF.Atan2(fpy - _world.Player.Y, fpx - _world.Player.X);
                    _input.Mag = 1; _input.Ang = a2; _input.Dx = MathF.Cos(a2); _input.Dy = MathF.Sin(a2);
                }
                Run(1);
            }
            _input.Mag = 0;
            Ok(_world.Stat("joined") >= 1, $"komórka gracza dołącza do agregatu (w agregacie {_world.Stat("aggN"):0})");
            Ok(_world.Stat("light") >= 1, "ślimak dociera do światła (fototaksja)");
            Ok(_world.Stat("fruit") >= 1 && fin, $"powstaje owocnik – koniec: {fend}");

            // 24–25. makrofag: eferocytoza apoptotycznego neutrofila i erytrofagocytoza (hemosyderyna)
            Start(S("macro")); var mac = (Macrophage)_world.Player; Clear(mac, 70);
            _input.Mag = 0; _input.Dx = _input.Dy = 0;
            var apoN = _world.Add(new Neutrophil(_world, mac.X + mac.R0 + 4, mac.Y, 0)); apoN.StartApoptosis(); apoN.Apo = 1000;
            var rbc = _world.Add(new RBC(_world, mac.X - mac.R0 - 3, mac.Y));
            Run(5400);
            Ok(_world.Stat("m_effero") >= 1 && mac.M2 > 0.5f, $"makrofag pochłania apoptotyczny neutrofil – eferocytoza, fenotyp naprawczy (M2 {mac.M2:0.00})");
            Ok(_world.Stat("m_rbc") >= 1 && mac.Hemo >= 3, $"strawiony erytrocyt zostawia ziarna hemosyderyny ({mac.Hemo})");

            // 26. neutrofil w tkance starzeje się → apoptoza → bez sprzątania martwica wtórna
            var old = _world.Add(new Neutrophil(_world, mac.X + 60, mac.Y + 40, 0)); old.Age = 1e9f;
            Run(2);
            Ok(old.Apoptotic, "stary neutrofil przechodzi apoptozę (piknoza, zaokrąglenie)");
            old.Apo = 1e6f; Run(2);
            Ok(!old.Alive && _world.Stat("necro") >= 1, "nieuprzątnięta komórka apoptotyczna ulega martwicy wtórnej");

            // 27–30. Euglena: światło, fotosynteza, fotouszkodzenia, metabolia w detrytusie, podział podłużny
            Start(S("euglena")); var eu = (Protist)_world.Player; Clear(eu, 400);
            void Safe() { foreach (var e in _world.Ents) if (e != eu && !(e is Protist { S.Photo: true, Lineage: true }) && e is not Detritus && Hypot(e.X - eu.X, e.Y - eu.Y) < 7000) { e.ReleaseHeld(); e.Alive = false; } _world.Ents.RemoveAll(e => !e.Alive); }
            void RunSafe(int f) { for (int i = 0; i < f; i += 5) { Run(5); Safe(); } }
            float lmin = 1e9f, lmax = 0;
            for (int i = 0; i < 3000; i++) { float l = _world.Sc.Light(_world, eu.X + Rand(-9000, 9000), eu.Y + Rand(-9000, 9000)); lmin = Math.Min(lmin, l); lmax = Math.Max(lmax, l); }
            Ok(lmax > 1000 && lmin < 150, $"światło w stawie zróżnicowane ({lmin:0}–{lmax:0} µmol/m²/s)");
            var dtr = _world.Add(new Detritus(_world, eu.X + 30, eu.Y, 50));
            _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0; _input.ActionHeld = true;
            eu.A = 0; RunSafe(300); _input.ActionHeld = false; _input.Mag = 0; _input.Dx = 0;
            Ok(eu.Hidden, $"metabolia: Euglena wpełza w kłaczek detrytusu i chowa się (Meta {eu.Meta:0.00})");
            dtr.Alive = false; RunSafe(120);
            (float, float) Find(Func<float, bool> ok)
            {
                for (int i = 0; i < 4000; i++) { float x = eu.X + Rand(-6000, 6000), y = eu.Y + Rand(-6000, 6000); if (ok(_world.Sc.Light(_world, x, y))) return (x, y); }
                return (eu.X, eu.Y);
            }
            // teleport + wygenerowanie nowych chunków + usunięcie drapieżników (test izolowany)

            void Tp(float x, float y) { eu.X = x; eu.Y = y; Safe(); RunSafe(150); }
            var (mx, my) = Find(l => l > 280 && l < 520);
            Tp(mx, my); eu.Paramylon = 0.3f; eu.PhotoDmg = 0;
            // cienie rzęsy wodnej i chmury przesuwają się – co 2,5 s Euglena wraca w świeży półcień (test mierzy fotosyntezę, nie szukanie światła)
            for (int k = 0; k < 8; k++) { var (qx, qy) = Find(l => l > 280 && l < 520); eu.X = qx; eu.Y = qy; RunSafe(150); }
            Ok(eu.Paramylon > 0.4f, $"fotosynteza w półcieniu wypełnia zapas paramylonu (0,30 → {eu.Paramylon:0.00}, światło {eu.Lux:0}, stan {eu.State}, zjadacz {(eu.Holder is Protist hp ? hp.S.Id : eu.Holder?.GetType().Name)})");
            var (hx, hy2) = Find(l => l > 1300);
            Tp(hx, hy2); RunSafe(600);
            Ok(eu.PhotoDmg > 0.05f, $"silne światło uszkadza fotoukład II (fotouszkodzenie {eu.PhotoDmg * 100:0}%, światło {eu.Lux:0})");
            Tp(mx, my); eu.PhotoDmg = 0; eu.Paramylon = 1; RunSafe(1500);
            int pop = _world.CountAll(e => e is Protist q && q.S.Photo && q.Lineage);
            Ok(pop >= 2 && _world.Player != eu && _world.Player is Protist, $"pełny zapas → podział podłużny (populacja {pop}, stan {eu.State}, Div {eu.Div:0.00})");
            Ok(_world.LineBorn >= 2 && _world.MaxGen >= 1 && _world.Player.Gen == 1, $"linia: córki dziedziczą przynależność i pokolenie (urodzone {_world.LineBorn}, pokolenie gracza {_world.Player.Gen})");

            // 31. linia E. coli: kilka pokoleń, zmierzony czas pokolenia
            Start(S("ecoli")); var ecl = (EColi)_world.Player; Clear(ecl, 60);
            void SafeE() { foreach (var e in _world.Ents) if (e is not EColi && e is not Grain && Hypot(e.X - ecl.X, e.Y - ecl.Y) < 80) e.Alive = false; _world.Ents.RemoveAll(e => !e.Alive); }
            for (int i = 0; i < 40 && _world.MaxGen < 2; i++)
            {
                if (_world.Player is EColi pe) { ecl = pe; ecl.L = ecl.DivL; }
                for (int f = 0; f < 60; f++) { Run(5); SafeE(); }
            }
            Ok(_world.MaxGen >= 2 && _world.GenTime > 0 && _world.LineNow >= 2, $"E. coli: kolejne pokolenia ({_world.MaxGen}), czas pokolenia {_world.GenTime / 60:0.0} min, żyje {_world.LineNow}");

            // 32. Didinium z głodu tworzy cystę – to nie koniec gry; gdy wrócą pantofelki, ekscystuje
            Start(S("didinium")); var dd = (Didinium)_world.Player; Clear(dd, 3000);
            foreach (var e in _world.Ents) if (e is Paramecium) e.Alive = false; _world.Ents.RemoveAll(e => !e.Alive);
            dd.Hunger = 0.999f; Run(60);
            bool cyst = dd.State == St.Cyst && dd.Alive && !_world.Over;
            _world.Add(new Paramecium(_world, dd.X + 300, dd.Y, 0)).Speed = 1;
            for (int i = 0; i < 150 && dd.State == St.Cyst; i++) Run(10);
            Ok(cyst && dd.State == St.Swim && !_world.Over, $"głód → cysta spoczynkowa, gra trwa; ofiara w pobliżu → ekscystacja (stan {dd.State})");

            // 33. makrofag: po eferocytozie wchodzi w mitozę i dzieli się (lokalna proliferacja)
            Start(S("macro")); var mc = (Macrophage)_world.Player; Clear(mc, 200);
            mc.M2 = 1; mc.Nutr = 30; Run(10);
            bool mit = mc.Mit > 0 || _world.LineBorn > 0;
            // mitoza trwa ~3600 klatek, ale zaczyna się dopiero, gdy w aktywnym obszarze jest < 9 makrofagów (bywa to po ~50 s gry) – zapas czasu
            for (int i = 0; i < 1200 && _world.LineBorn == 0; i++) { Run(10); mit |= mc.Mit > 0; }
            Ok(mit && _world.LineBorn >= 1 && _world.CountAll(e => e is Macrophage m && m.Lineage) >= 2, $"makrofag po eferocytozie zaokrągla się i dzieli (mitoza: {mit}, podziały: {_world.LineBorn})");

            // 34. membrana: nieściśliwe wnętrze – wgnieciona komórka wybrzusza się gdzie indziej, pole prawie stałe
            {
                var mem = new Membrane(48) { MaxDef = 5, Fluid = 0.9f };
                var rr = new float[48]; for (int i = 0; i < 48; i++) rr[i] = 10;
                mem.Radial(rr); mem.Follow(0, 0, 0, 5, 1f / 60);
                float ar0 = MathF.Abs(mem.SignedArea());
                var wall = new Prim { Cap = false, X = 14, Y = 0, R = 7 };
                for (int f = 0; f < 30; f++) { mem.Follow(0, 0, 0, 5, 1f / 60); mem.PushOut(wall, 0.6f); mem.ConserveArea(); }
                float a1 = MathF.Abs(mem.SignedArea()), left = -mem.Px[24];
                Ok(a1 > ar0 * 0.95f && left > 10.3f, $"płynne ciało: po wgnieceniu pole {a1 / ar0 * 100:0}% pierwotnego, przeciwna strona wybrzuszona ({left:0.0} µm > 10)");
            }

            // 35. ślimak Dictyostelium: ciało sunie po torze czubka (zakręt „przepływa” wzdłuż ciała)
            Start(S("dicty"));
            var slug = _world.Add(new Slug(_world, 800, 900, 0, 200)); _world.Player = null; _world.SetPlayer(slug);
            _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0;
            for (int i = 0; i < 40; i++) Run(10);
            _input.Ang = -PI / 2; _input.Dx = 0; _input.Dy = -1;
            float bendMax = 0;
            for (int i = 0; i < 60; i++)
            {
                Run(5); slug.UpdatePrim();
                var sp0 = slug.Part(0); var spl = slug.Part(slug.NParts - 1);
                float ah = MathF.Atan2(sp0.Ay - sp0.By, sp0.Ax - sp0.Bx), at = MathF.Atan2(spl.Ay - spl.By, spl.Ax - spl.Bx);
                bendMax = Math.Max(bendMax, MathF.Abs(WrapA(ah - at)));
            }
            _input.Mag = 0; _input.Dx = _input.Dy = 0;
            Ok(bendMax > 0.6f, $"ślimak: w zakręcie przód i tył ciała mają różne kierunki (ciało sunie po torze czubka, maks. {bendMax * 180 / PI:0}°)");

        spermTests:
            // 35a. liczebność: w puli nasienia plemników w kadrze są dziesiątki (ok. ⅓ nieruchomych, wiele z wadami budowy)
            {
                Start(S("sperm")); var spl = _world.Player; _input.Mag = 0; Run(30);
                int inV = 0, imm = 0, abn = 0;
                foreach (var e in _world.Ents) if (e is Sperm s && s != spl && MathF.Abs(s.X - spl.X) < 160 && MathF.Abs(s.Y - spl.Y) < 90) { inV++; if (s.Mot == Sperm.Motil.Immotile) imm++; if (s.Form != Sperm.Morph.Normal) abn++; }
                Ok(inV >= 30 && imm > inV / 6 && abn > inV / 5, $"pula nasienia: {inV} plemników w polu widzenia 400× (nieruchome {imm}, z wadami budowy {abn})");
                // śluz szyjki: po wejściu do kanału plemników w kadrze jest tyle, ile w teście po stosunku (~10–50)
                var sps = (ScSperm)_sc; var (cx, cy, ca) = sps.Center(ScSperm.Reg.Cervix, 1);
                spl.X = cx; spl.Y = cy; spl.A = ca; var sp0 = (Sperm)spl; sp0.Energy = 1;
                for (int i = 0; i < 8; i++) { Run(30); _world.CamX = spl.X; _world.CamY = spl.Y; }
                int inC = _world.Count(e => e is Sperm s && s != spl && s.State == St.Swim && MathF.Abs(s.X - spl.X) < 160 && MathF.Abs(s.Y - spl.Y) < 90);
                // liczba w jednym kadrze to próbka Poissona (~10 ± 3), więc gęstość mierzona na większym odcinku kanału (r = 300 µm)
                // i przeliczana na pole widzenia 400× (320 × 180 µm)
                const float CR = 300; int lumHit = 0;
                for (int i = 0; i < 3000; i++) { float qa = Rand(0, TAU), qd = CR * MathF.Sqrt(R01()); if (sps.Sdf(spl.X + MathF.Cos(qa) * qd, spl.Y + MathF.Sin(qa) * qd) < 0) lumHit++; }
                float lumA = lumHit / 3000f * PI * CR * CR;
                int inR = _world.Count(e => e is Sperm s && s != spl && s.State == St.Swim && Hypot(s.X - spl.X, s.Y - spl.Y) < CR && sps.Sdf(s.X, s.Y) < 0);
                float perField = inR / Math.Max(1, lumA) * 57600;
                Ok(perField >= 6 && perField <= 45, $"śluz szyjki: {perField:0.0} plemnika na pole widzenia (średnio w kanale w promieniu {CR:0} µm: {inR}; w samym kadrze {inC}) – w teście po stosunku zwykle ~10–50");
                // filtr śluzu: plemnik z wadą budowy grzęźnie, prawidłowy płynie dalej
                foreach (var e in _world.Ents) if (e is Sperm s && s != spl) s.Alive = false;
                sps.NoRefill = true;
                var good = _world.Add(new Sperm(_world, cx + 20, cy, ca)); var bad = _world.Add(new Sperm(_world, cx - 20, cy, ca)); bad.Form = Sperm.Morph.Amorphous;
                good.Energy = bad.Energy = 1; float gs0 = sps.RouteS(good.X, good.Y), bs0 = sps.RouteS(bad.X, bad.Y);
                Run(240);
                float gd = sps.RouteS(good.X, good.Y) - gs0, bdd = sps.RouteS(bad.X, bad.Y) - bs0;
                Ok(gd > bdd * 1.8f && gd > 25, $"śluz szyjki zatrzymuje plemniki o wadliwej budowie (prawidłowy +{gd:0} µm, bezkształtna główka +{bdd:0} µm w 4 s)");
            }
            // 36. plemnik: przy ścianie obraca się pod prąd (reotaksja) i płynie wzdłuż ściany. Reotaksja to tendencja populacji
            //     (większość, nie każdy plemnik – Miki i Clapham 2013; u gracza bez sterowania ~80% prób), a wynik pojedynczej próby
            //     zależy od losowej chiralności toru – dlatego 8 prób: ≥ 4 muszą popłynąć pod prąd, a średni udział czasu przy ścianie
            //     (< 25 µm) ma wyraźnie przewyższać udział pasa przyściennego w świetle cieśni (tyle dałoby pływanie bez preferencji).
            ScSperm ssc = null; Sperm sp = null; int rheoUp = 0; float wallSum = 0, laneFrac = 0; string rheoTxt = "";
            const int RheoN = 8;
            for (int trial = 0; trial < RheoN; trial++)
            {
                Start(S("sperm")); ssc = (ScSperm)_sc; sp = (Sperm)_world.Player; ssc.NoRefill = true;
                foreach (var e in _world.Ents) if (e != sp && e is Sperm or Leuko) e.Alive = false; _world.Ents.RemoveAll(e => !e.Alive);
                var (tsx, tsy, tsa) = ssc.TestSpot(ScSperm.Reg.Isthmus, 4);
                if (trial == 0)
                {
                    // udział pasa ≤ 25 µm od ściany w świetle wokół miejsca próby (losowe punkty w obrębie ±150 µm)
                    int inL = 0, nearL = 0;
                    for (int i = 0; i < 4000; i++) { float qx = tsx + Rand(-150, 150), qy = tsy + Rand(-150, 150); if (ssc.Sdf(qx, qy) >= 0 || ssc.RegionAt(qx, qy) != ScSperm.Reg.Isthmus) continue; inL++; ssc.Wall(qx, qy, out float dq, out _, out _); if (dq < 25) nearL++; }
                    laneFrac = nearL / (float)Math.Max(1, inL);
                }
                sp.X = tsx; sp.Y = tsy; sp.A = tsa + 0.15f; sp.Cap = 1; sp.Energy = 1;
                float rs0 = ssc.RouteS(sp.X, sp.Y); int nearW = 0, nW = 0;
                for (int i = 0; i < 50; i++) { Run(10); if (i == 15) rs0 = ssc.RouteS(sp.X, sp.Y); ssc.Wall(sp.X, sp.Y, out float dw, out _, out _); if (i > 10) { nW++; if (dw < 25) nearW++; } }
                ssc.Axis(sp.X, sp.Y, out float rax, out float ray, out _, out _);
                float up = MathF.Cos(sp.A) * rax + MathF.Sin(sp.A) * ray, prog = ssc.RouteS(sp.X, sp.Y) - rs0, wf = nearW / (float)Math.Max(1, nW);
                if (up > 0.3f && prog > 20) rheoUp++;
                wallSum += wf;
                rheoTxt += $"{(trial > 0 ? "; " : "")}{up:0.00}/{prog:0} µm/{wf * 100:0}%";
            }
            float wallMean = wallSum / RheoN;
            Ok(rheoUp >= 4 && wallMean > Math.Max(0.45f, laneFrac * 1.4f), $"reotaksja: {rheoUp} z {RheoN} plemników płynie pod prąd w górę jajowodu; przy ścianie średnio {wallMean * 100:0}% czasu (pas przyścienny to {laneFrac * 100:0}% światła) – zgodność/postęp/przy ścianie: {rheoTxt}");

            // 36b. tor krzywoliniowy: VCL ≈ 2× VSL (kołysanie główki, ALH), liniowość jak w pomiarach CASA
            Start(S("sperm")); ssc = (ScSperm)_sc; sp = (Sperm)_world.Player; ssc.NoRefill = true;
            foreach (var e in _world.Ents) if (e != sp && e is Sperm or Leuko) e.Alive = false; _world.Ents.RemoveAll(e => !e.Alive);
            var (ux0, uy0, ua0) = ssc.TestSpot(ScSperm.Reg.Uterus, 300);
            sp.X = ux0; sp.Y = uy0; sp.A = ua0 + PI; sp.Energy = 1; _input.Mag = 0;
            Run(120);
            float lin = sp.Vcl > 1 ? sp.Vsl / sp.Vcl : 0;
            Ok(sp.Vcl > sp.Vsl * 1.3f && lin > 0.3f && lin < 0.8f && sp.Vcl < 160, $"tor główki jest krzywoliniowy: VCL {sp.Vcl:0} µm/s, VSL {sp.Vsl:0} µm/s, LIN {lin:0.00} (u ludzi ≈ 0,4–0,7)");

            // 37. zapłodnienie: kapacytowany plemnik przy osłonce – reakcja akrosomalna, przejście, fuzja; blokada polispermii
            Start(S("sperm")); ssc = (ScSperm)_sc; sp = (Sperm)_world.Player; ssc.NoRefill = true;
            foreach (var e in _world.Ents) if (e != sp && e is Sperm or Leuko) e.Alive = false; _world.Ents.RemoveAll(e => !e.Alive);
            var egg = ssc.Egg; sp.Cap = 1; sp.Energy = 1; sp.X = egg.X - egg.ZonaR - 3; sp.Y = egg.Y; sp.A = 0;
            var rival = _world.Add(new Sperm(_world, egg.X + egg.ZonaR + 3, egg.Y, PI)); rival.Cap = 1;
            _input.ActionHeld = true;
            for (int i = 0; i < 120 && ssc.Fertilized == null; i++) Run(10);
            _input.ActionHeld = false;
            Run(30);
            Ok(ssc.Fertilized != null && sp.Acrosome, $"reakcja akrosomalna i przejście przez osłonkę kończą się fuzją (zapłodnił: {(ssc.Fertilized == sp ? "gracz" : ssc.Fertilized != null ? "rywal" : "nikt")})");
            var loser = ssc.Fertilized == sp ? rival : sp;
            Ok(loser.State == St.Dead || !loser.Alive, $"blokada polispermii: drugi plemnik w osłonce nie wnika (stan {loser.State})");

        newModes:
            // 37c. atlas (tryb podglądu F): każdy obiekt i każde tło we wszystkich światach ma kartę (nazwa PL/EN, opis, dane)
            {
                var missing = new SortedSet<string>(); int seen = 0;
                foreach (var s in Scenario.All)
                {
                    Start(s); Run(120);
                    foreach (var e in _world.Ents) { if (!e.Alive) continue; seen++; string k = Atlas.KeyOf(e); var a = Atlas.Get(k); if (a == null || a.En.Name.Length == 0 || a.Pl.Desc.Length == 0) missing.Add($"{s.Id}:{k}"); }
                    for (int i = 0; i < 60; i++)
                    {
                        float x = _world.CamX + Rand(-1, 1) * s.ViewW, y = _world.CamY + Rand(-1, 1) * s.ViewW * 0.6f;
                        string k = s.InspectAt(_world, x, y); if (k == null || Atlas.Get(k) == null) missing.Add($"{s.Id}:tło:{k ?? "null"}");
                    }
                }
                Ok(missing.Count == 0 && Atlas.Count >= 80, $"atlas: {Atlas.Count} kart, sprawdzono {seen} obiektów we wszystkich światach" + (missing.Count > 0 ? " – brak: " + string.Join(", ", missing) : ""));
            }
            // 37a. niesporczak: stopa w fazie podporu stoi na podłożu (nie ślizga się), gdy ciało idzie naprzód
            {
                Start(S("tardi")); var tg = (Tardigrade)_world.Player; Clear(tg, 700);
                _input.Mag = 1; _input.Ang = tg.A; _input.Dx = MathF.Cos(tg.A); _input.Dy = MathF.Sin(tg.A);
                Run(60);
                float slip = 0, body = 0; int n = 0;
                for (int f = 0; f < 120; f++)
                {
                    tg.Foot(1, 1, out _, out _, out float fx0, out float fy0, out bool st0); tg.ToWorld(fx0, fy0, out float wx0, out float wy0); float x0 = tg.X, y0 = tg.Y;
                    Run(1);
                    tg.Foot(1, 1, out _, out _, out float fx1, out float fy1, out bool st1); tg.ToWorld(fx1, fy1, out float wx1, out float wy1);
                    if (st0 && st1) { slip += Hypot(wx1 - wx0, wy1 - wy0); body += Hypot(tg.X - x0, tg.Y - y0); n++; }
                }
                _input.Mag = 0; _input.Dx = _input.Dy = 0;
                Ok(n > 20 && slip < body * 0.15f, $"niesporczak: stopa w podporze stoi na podłożu (poślizg {slip:0.0} µm przy {body:0} µm drogi ciała)");
            }
            // 37b. wnętrze ciała nadąża za obrysem: przy ostrych skrętach przód obrysu (wygięty, opóźniony) a rysowane wnętrze
            //      (ryjek, rowek gębowy) – rozjazd w układzie sztywnym vs w układzie odkształconym (Painter.Warp)
            foreach (var zid in new[] { "para", "didinium", "euglena" })
            {
                Start(S(zid)); var zp = _world.Player; Clear(zp, 600);
                var zm = zp.Mem; int zk = 0; for (int i = 1; i < zm.N; i++) if (zm.Rx[i] > zm.Rx[zk]) zk = i;
                float rigid = 0, warped = 0;
                for (int f = 0; f < 360; f++)
                {
                    float zz = (f / 60) % 2 == 0 ? 1.3f : -1.3f; _input.Mag = 1; _input.Ang = 0.3f + zz; _input.Dx = MathF.Cos(_input.Ang); _input.Dy = MathF.Sin(_input.Ang);
                    Run(1);
                    float c = MathF.Cos(zm.Ang), s = MathF.Sin(zm.Ang), rx = zm.Rx[zk], ry = zm.Ry[zk];
                    float ax = zm.Px[zk] - zm.Dx[zk] - zm.Cx, ay = zm.Py[zk] - zm.Dy[zk] - zm.Cy, bx = ax * c + ay * s, by = -ax * s + ay * c; // rzeczywisty przód w układzie ciała
                    float wy = ry + zm.BendEff * rx * rx, wx2 = rx * MathF.Cos(zm.LagA) - wy * MathF.Sin(zm.LagA), wy2 = rx * MathF.Sin(zm.LagA) + wy * MathF.Cos(zm.LagA);
                    rigid = Math.Max(rigid, Hypot(bx - rx, by - ry)); warped = Math.Max(warped, Hypot(bx - wx2, by - wy2));
                }
                _input.Mag = 0; _input.Dx = _input.Dy = 0;
                Ok(warped < rigid * 0.5f, $"{zid}: wnętrze nadąża za przodem ciała przy skrętach (rozjazd {warped:0.0} µm, bez odkształcenia byłoby {rigid:0.0} µm)");
            }
            // 38. komórka nowotworowa: gradient tlenu, niedotlenienie → EMT, mitoza z dziedziczeniem linii
            Start(S("cancer")); var csc = (ScCancer)_sc; var tc = (TumorCell)_world.Player; csc.NoImmune = true;
            foreach (var e in _world.Ents) if (e is Killer) e.Alive = false;
            float yv = csc.VesselY(_world, tc.X, tc.Y), sgn = MathF.Sign(tc.Y - yv);
            // zdrowy zrąb: naczynia włosowate co kilkadziesiąt µm – tlenu nie brakuje nigdzie; w gnieździe naczynia są ściśnięte
            float oNear = csc.O2(_world, tc.X, yv + sgn * 30), oStroma = 1e9f, oCore = csc.O2(_world, csc.TumX, csc.TumY);
            for (int i = 0; i < 400; i++) { float qsx = csc.TumX + Rand(-900, 900), qsy = csc.TumY + Rand(-900, 900); if (!csc.InNest(qsx, qsy, 60)) oStroma = Math.Min(oStroma, csc.O2(_world, qsx, qsy)); }
            Ok(oNear > 30 && oStroma > 12 && oCore < 20, $"tlen: przy żyłce {oNear:0} mmHg, w zdrowym zrębie ≥ {oStroma:0} mmHg (naczynia włosowate), w środku gniazda {oCore:0} mmHg (ściśnięte naczynia)");
            float dCapOut = csc.CapDist(_world, csc.TumX + 400, csc.TumY, true), dCapIn = csc.CapDist(_world, csc.TumX, csc.TumY, true);
            Ok(dCapOut < 70 && dCapIn > csc.TumR * 0.8f, $"naczynia włosowate: w zrębie najbliższe drożne {dCapOut:0} µm, w środku guza {dCapIn:0} µm (ściśnięte przez komórki)");
            // niedotlenienie bez anoksji: szukamy w gnieździe miejsca o pO₂ 2,5–7 mmHg i stawiamy tam komórkę (bez sąsiadów)
            csc.TumR = 150; csc.FixedR = true;
            float hpx = csc.TumX, hpy = csc.TumY; bool found = false;
            for (int i = 0; i < 3000 && !found; i++) { float qx = csc.TumX + Rand(-140, 140), qy = csc.TumY + Rand(-140, 140); float o = csc.O2(_world, qx, qy); if (o > 2.5f && o < 7 && csc.InNest(qx, qy, -10)) { hpx = qx; hpy = qy; found = true; } }
            tc.X = hpx; tc.Y = hpy; Clear(tc, 40); tc.Emt = 0; tc.Muts = Muts.Tp53; tc.Growth = 0.2f;
            foreach (var e in _world.Ents) if (e is Macrophage) e.Alive = false; // EMT tylko z niedotlenienia
            for (int i = 0; i < 40 && tc.Emt < 0.6f; i++) Run(60);
            Ok(found && tc.Emt >= 0.6f && tc.Hif > 0.5f, $"niedotlenienie (pO₂ {tc.O2:0.0} mmHg, HIF-1α {tc.Hif:0.00}) uruchamia EMT ({tc.Emt:0.00})" + (tc.Emt < 0.6f ? $" [żywa {tc.Alive}, gracz {_world.Player == tc}, O₂ tu {csc.O2(_world, tc.X, tc.Y):0.0}, uchwyt {tc.Holder?.GetType().Name}, mit {tc.Mit:0.00}, kiełki {csc.SproutCount}]" : ""));
            // pleomorfizm: komórki guza różnej wielkości; śmierć z braku tlenu zostawia cień martwiczy, apoptoza – ciałka apoptotyczne
            {
                var rs = _world.Ents.OfType<TumorCell>().Select(t => t.R0).ToList();
                float mean = rs.Average(), sd = MathF.Sqrt(rs.Select(r => (r - mean) * (r - mean)).Average());
                Ok(rs.Count > 60 && sd / mean > 0.08f, $"pleomorfizm: {rs.Count} komórek guza, średnica {2 * mean:0.0} µm ± {2 * sd:0.0} (CV {sd / mean * 100:0}%)");
                var nec = _world.Ents.OfType<TumorCell>().First(t => t != _world.Player); nec.DeathKind = TumorCell.Death.Necrosis; nec.Kill();
                var apo = _world.Ents.OfType<TumorCell>().First(t => t != _world.Player && t.Alive); apo.DeathKind = TumorCell.Death.Apoptosis; apo.Kill();
                Run(2);
                Ok(_world.Ents.Any(e => e is TumorDebris { Necrotic: true }) && _world.Ents.Any(e => e is TumorDebris { Necrotic: false }), "martwica zostawia cień komórki, apoptoza – ciałka apoptotyczne");
            }
            tc.Y = yv + sgn * 60; tc.Growth = 0.999f; int line0 = _world.Count(e => e is TumorCell && e.Lineage);
            Run(400);
            Ok(_world.Count(e => e is TumorCell && e.Lineage) > line0 && _world.Stat("t_div") >= 1, $"mitoza: córka należy do linii gracza (linia {line0} → {_world.Count(e => e is TumorCell && e.Lineage)})");

            // 39. inwazja przez gęsty kolagen: tryb mezenchymalny trawi włókna (MT1-MMP), ameboidalny bez trawienia utyka
            float Invade(bool amo)
            {
                Start(S("cancer")); csc = (ScCancer)_sc; tc = (TumorCell)_world.Player; Clear(tc, 400); csc.NoImmune = true;
                foreach (var e in _world.Ents) if (e is Collagen && Hypot(e.X - tc.X, e.Y - tc.Y) < 400) e.Alive = false;
                _world.Ents.RemoveAll(e => !e.Alive);
                float yv2 = csc.VesselY(_world, tc.X, tc.Y); tc.Y = yv2 + 120; tc.Emt = 1; tc.Amoeboid = amo; tc.Muts = Muts.Tp53; tc.A = 0;
                var rng = new Random(7);
                for (int i = 0; i < 70; i++) _world.Add(new Collagen(_world, tc.X + 15 + (float)rng.NextDouble() * 110, tc.Y - 60 + (float)rng.NextDouble() * 120, (float)rng.NextDouble() * TAU, 30 + (float)rng.NextDouble() * 40, 0.7f));
                float x0 = tc.X; _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0;
                Run(600); _input.Mag = 0; _input.Dx = 0;
                return tc.X - x0;
            }
            float dMes = Invade(false); int cut = (int)_world.Stat("t_cut");
            float dAmo = Invade(true);
            float vAmoFree = TumorCell.VAmo * 240 * 10, vMesFree = TumorCell.VMes * 240 * 10;
            Ok(cut >= 1 && dMes > vMesFree * 0.4f, $"tryb mezenchymalny trawi kolagen (przecięte włókna: {cut}, przebył {dMes:0} µm z ~{vMesFree:0} możliwych)");
            Ok(dAmo < vAmoFree * 0.5f, $"tryb ameboidalny w gęstym kolagenie traci prędkość (przebył {dAmo:0} µm z ~{vAmoFree:0} możliwych bez włókien)");

            // 40. intrawazacja obok makrofaga (TMEM) → krążenie w czasie rzeczywistym → przyleganie → ekstrawazacja ≥ 1 mm
            Start(S("cancer")); csc = (ScCancer)_sc; tc = (TumorCell)_world.Player; Clear(tc, 120); csc.NoImmune = true;
            foreach (var e in _world.Ents) if (e is Killer) e.Alive = false;
            yv = csc.VesselY(_world, tc.X, tc.Y); sgn = MathF.Sign(tc.Y - yv);
            tc.Y = yv + sgn * 27; tc.Emt = 1; tc.Amoeboid = true; tc.Muts = Muts.Tf | Muts.Tp53;
            // test bramy TMEM, nie przeciskania się przez zrąb: przy ścianie żyłki usuwamy inne komórki i włókna (CAF, TAM, kolagen)
            Clear(tc, 60); foreach (var e in _world.Ents) if (e is Collagen && Hypot(e.X - tc.X, e.Y - tc.Y) < 60 + e.R) e.Alive = false; _world.Ents.RemoveAll(e => !e.Alive);
            _world.Add(new Macrophage(_world, tc.X + 22, tc.Y + sgn * 8));
            _input.Mag = 1; _input.Ang = -sgn * PI / 2; _input.Dx = 0; _input.Dy = -sgn;
            for (int i = 0; i < 40 && tc.Where == TumorCell.Site.Tissue; i++) Run(30);
            _input.Mag = 0; _input.Dy = 0;
            Ok(tc.Where == TumorCell.Site.Blood && _world.TimeScale == 1, $"intrawazacja (stan {tc.Where}, skala czasu ×{_world.TimeScale})");
            float cbx0 = tc.X; Run(60);
            Ok(tc.X - cbx0 > 80, $"krew niesie komórkę ({tc.X - cbx0:0} µm w 1 s)");
            void NoKillers() { tc.Hp = Math.Max(tc.Hp, 0.6f); foreach (var e in _world.Ents) if (e is Killer) e.Alive = false; }
            for (int i = 0; i < 60 && Hypot(tc.X - csc.TumX, tc.Y - csc.TumY) < 1100; i++) { Run(10); NoKillers(); }
            _input.ActionHeld = true;
            for (int i = 0; i < 60 && tc.Where == TumorCell.Site.Blood; i++) { Run(10); NoKillers(); }
            _input.ActionHeld = false;
            Ok(tc.Where == TumorCell.Site.Wall && _world.TimeScale > 1, $"przyleganie do śródbłonka (stan {tc.Where}, osłona płytek {tc.Shield:0.00})");
            for (int i = 0; i < 80 && tc.Where == TumorCell.Site.Wall; i++) Run(30);
            Ok(tc.Where == TumorCell.Site.Tissue && _world.Stat("t_extra") >= 1, $"ekstrawazacja {Hypot(tc.X - csc.TumX, tc.Y - csc.TumY) / 1000:0.00} mm od guza (stan {tc.Where})");

            // 41. odporność: limfocyt T zabija komórkę z MHC-I, pomija utratę B2M; PD-L1 go wyczerpuje; NK zabija komórkę bez MHC-I
            float Attack(Muts m, bool nk, out Killer kl)
            {
                Start(S("cancer")); var t = (TumorCell)_world.Player; Clear(t, 200); ((ScCancer)_sc).NoImmune = true;
                foreach (var e in _world.Ents) if (e is Killer) e.Alive = false;
                _world.Ents.RemoveAll(e => !e.Alive);
                float y0 = ((ScCancer)_sc).VesselY(_world, t.X, t.Y); t.Y = y0 + 70; t.Muts = m; t.Emt = 0;
                kl = _world.Add(new Killer(_world, t.X + 16, t.Y, nk));
                float hp0 = t.Hp, hpMin = t.Hp; _input.Mag = 0;
                for (int i = 0; i < 20; i++) { Run(30); if (!t.Alive) { hpMin = 0; break; } hpMin = Math.Min(hpMin, t.Hp); t.Hp = Math.Max(t.Hp, 0.05f); }
                return hp0 - hpMin;
            }
            float dT = Attack(Muts.None, false, out _), dB2m = Attack(Muts.B2m, false, out _);
            Attack(Muts.Pdl1, false, out var exh);
            float dNk = Attack(Muts.B2m, true, out _);
            Ok(dT > 0.25f && dB2m < 0.05f, $"limfocyt T niszczy komórkę z MHC-I (ubytek {dT:0.00}), a bez MHC-I jej nie widzi ({dB2m:0.00})");
            Ok(exh.Exhausted || _world.Stat("t_exh") >= 1, "PD-L1 wyczerpuje limfocyt T");
            Ok(dNk > 0.4f, $"komórka NK zabija komórkę bez MHC-I – „brak swojego” (ubytek {dNk:0.00})");

            // 42. świdrowiec: pływanie naprzód usuwa przeciwciała z płaszcza VSG (Engstler i in. 2007), wsteczne – nie
            float Coat(bool back)
            {
                Start(S("tryp")); var ts = (ScTryp)_sc; var tr = (Trypanosome)_world.Player; ts.SetTiter(0, 1.2f); ts.Frozen = true; tr.Growth = -100; // bez podziału (zmiana VSG zresetowałaby płaszcz)
                _input.Mag = 1; _input.Ang = 0; _input.Dx = 1; _input.Dy = 0; _input.ActionHeld = back;
                // pływanie w kółko wokół startu (strefa krwi, bez komórek Kupffera) – test dotyczy tylko wiązania i usuwania przeciwciał
                for (int i = 0; i < 360; i++)
                {
                    _input.Ang = i * 0.033f; _input.Dx = MathF.Cos(_input.Ang); _input.Dy = MathF.Sin(_input.Ang); Run(10);
                    foreach (var e in _world.Ents) if (e is Kupffer) e.Alive = false;
                    if (tr.Holder is Kupffer) { tr.Holder = null; tr.State = St.Swim; tr.Dig = 0; }
                }
                _input.ActionHeld = false; _input.Mag = 0; _input.Dx = 0;
                return tr.Alive ? tr.Ab : 1;
            }
            float abF = Coat(false), abB = Coat(true);
            Ok(abF < 0.65f && abB > abF + 0.25f, $"pływanie naprzód zdziera przeciwciała (płaszcz {Pct0(abF)} przy pływaniu naprzód, {Pct0(abB)} wstecz)");

            // 43. podział podłużny, dziedziczenie linii i zmiana wariantu VSG
            Start(S("tryp")); var trs = (ScTryp)_sc; var tp = (Trypanosome)_world.Player;
            for (int i = 0; i < 60 && _world.Stat("t_switch") == 0; i++) { foreach (var e in _world.Ents) if (e is Trypanosome t && t.Lineage && t.Div <= 0 && t.Stumpy <= 0) t.Growth = 0.999f; Run(120); }
            int ltr = _world.Count(e => e is Trypanosome t && t.Lineage);
            Ok(_world.Stat("t_div") >= 1 && ltr >= 2, $"świdrowiec dzieli się (podziały gracza {_world.Stat("t_div")}, linia {ltr})");
            Ok(_world.Stat("t_switch") >= 1, $"zmiana płaszcza VSG w linii ({_world.Ents.OfType<Trypanosome>().Select(t => t.Vsg).Distinct().Count()} wariantów)");

            // 44. SIF: gęsta populacja → formy krępe
            Start(S("tryp")); trs = (ScTryp)_sc;
            for (int i = 0; i < 100; i++) _world.Add(new Trypanosome(_world, Rand(-70, 70), Rand(-70, 70), Rand(0, TAU)));
            Run(60 * 12);
            int stumpy = _world.Count(e => e is Trypanosome t && t.Stumpy > 0);
            Ok(stumpy >= 10 && trs.Sif > 0.6f, $"gęstość (SIF {trs.Sif:0.00}) → przemiana w formy krępe ({stumpy})");

            // 45. przeniesienie: forma krępa wyssana przez muchę tse-tse
            Start(S("tryp")); trs = (ScTryp)_sc; tp = (Trypanosome)_world.Player; tp.Stumpy = 1;
            trs.StartBite(_world, tp, 14);
            _input.Mag = 0;
            for (int i = 0; i < 30 && _world.Stat("transmit") == 0; i++) Run(30);
            Ok(_world.Stat("transmit") >= 1, $"forma krępa wyssana przez tse-tse (przeniesione: {_world.Stat("transmit")})");

            // 46. komórka Kupffera pochłania świdrowca opłaszczonego przeciwciałami
            Start(S("tryp")); tp = (Trypanosome)_world.Player; Clear(tp, 60);
            var kc = _world.Add(new Kupffer(_world, tp.X + 22, tp.Y)); tp.A = 0; tp.Ab = 0.8f; ((ScTryp)_sc).SetTiter(0, 2f);
            _input.Mag = 1; _input.Ang = 0; _input.Dx = 1;
            bool eaten = false; for (int i = 0; i < 40 && !eaten; i++) { Run(10); eaten = tp.Holder == kc; }
            _input.Mag = 0; _input.Dx = 0;
            Ok(eaten, "komórka Kupffera pochłania opłaszczonego świdrowca");

        neuronTests:
            // 47–55. neuron kory (mysz, E15,5): podział progenitora, polaryzacja, przejście w komórkę dwubiegunową, lokomocja,
            //        translokacja, stożek wzrostu (prędkości, Slit2, linia środkowa), fala śmierci P2–P5, interneurony, los neuronu
            void Steer(float ang, float mag = 1) { _input.Mag = mag; _input.Ang = ang; _input.Dx = MathF.Cos(ang) * mag; _input.Dy = MathF.Sin(ang) * mag; }
            {
                Start(S("neuron")); var ns = (ScNeuron)_sc; var me = ns.Me; ns.DebugJump(_world, 1); Steer(0, 0);
                for (int i = 0; i < 40 && _world.Stat("n_div") == 0; i++) Run(30);
                Ok(_world.Stat("n_div") >= 1 && ns.SisterN is { Gfp: true } && me.S == CortNeuron.Stage.Multipolar, $"neuron: progenitor pośredni dzieli się w SVZ na dwa neurony (etap {me.S}, siostra {(ns.SisterN != null ? "z GFP" : "brak")}, {ScNeuron.AgeStr(ns.Age)})");
            }
            {
                Start(S("neuron")); var ns = (ScNeuron)_sc; ns.DebugJump(_world, 2); var me = ns.Me; me.Ax = null; me.Y = 350; _world.Stats["n_axon"] = 0;
                Steer(-PI / 2);
                for (int i = 0; i < 80 && me.Ax == null; i++) Run(30);
                Steer(0, 0);
                Ok(me.Ax != null && _world.Stat("n_axon") >= 1 && me.Cone != null, $"neuron: neuryt komórki wielobiegunowej dotyka aksonu TAG-1 i staje się aksonem (ciało na {me.Y:0} µm)");
            }
            {
                Start(S("neuron")); var ns = (ScNeuron)_sc; ns.DebugJump(_world, 3); var me = ns.Me; me.Syn = 0; Steer(0, 0);
                float t0 = _world.SimT;
                for (int i = 0; i < 80 && me.S != CortNeuron.Stage.Locomotion; i++) Run(30);
                Ok(me.S == CortNeuron.Stage.Locomotion && me.Fiber != CortNeuron.NoFiber && _world.Stat("n_bipolar") >= 1, $"neuron: pod podpłytką synapsy (NMDA) przełączają komórkę w dwubiegunową na włóknie gleju (etap {me.S}, po {(_world.SimT - t0) / 3600:0.0} h)");
            }
            {
                // lokomocja w strefie pośredniej (średnio ~9–12 µm/h; Tabata i Nakajima 2003) – bez naczyń na drodze
                Start(S("neuron")); var ns = (ScNeuron)_sc; ns.DebugJump(_world, 4); var me = ns.Me;
                float y0 = me.Y, t0 = _world.SimT, maxV = 0, yPrev = me.Y;
                Steer(-PI / 2);
                for (int i = 0; i < 600; i++) { Run(1); maxV = Math.Max(maxV, (yPrev - me.Y) / (_world.TimeScale / 60f / 3600)); yPrev = me.Y; }
                Steer(0, 0);
                float vAvg = (y0 - me.Y) / ((_world.SimT - t0) / 3600);
                Ok(vAvg > 8 && vAvg < 14 && maxV > 25, $"neuron: lokomocja po glej – średnio {vAvg:0.0} µm/h, w skoku jądra do {maxV:0} µm/h");
            }
            {
                Start(S("neuron")); var ns = (ScNeuron)_sc; ns.DebugJump(_world, 5); var me = ns.Me;
                float tTr = -1, yTr = 0; Steer(-PI / 2);
                for (int i = 0; i < 900 && me.S != CortNeuron.Stage.Settled; i++) { Run(2); if (me.S == CortNeuron.Stage.Translocation && tTr < 0) { tTr = _world.SimT; yTr = me.Y; } }
                Steer(0, 0);
                float vTr = tTr >= 0 ? (yTr - me.Y) / Math.Max(1e-3f, (_world.SimT - tTr) / 3600) : 0;
                Ok(me.S == CortNeuron.Stage.Settled && me.Y < Cortex.MzB + me.R0 + 2.5f && _world.Player is GrowthCone && _world.Stat("n_settle") >= 1, $"neuron: translokacja końcowa i osiedlenie u szczytu płytki (ciało na {me.Y:0} µm, gracz: {_world.Player?.GetType().Name})");
                Ok(vTr > 40 && vTr < 75 && yTr - me.Y > 35, $"neuron: translokacja końcowa {yTr - me.Y:0} µm z prędkością {vTr:0} µm/h (Sekine i in. 2011: ~50 µm w ≤ 1 h)");
            }
            {
                // stożek wzrostu: ~37 µm/h przed linią środkową (Hutchins i in. 2011)
                Start(S("neuron")); var ns = (ScNeuron)_sc; ns.DebugJump(_world, 6); var a = ns.Me.Ax;
                float x0 = a.Tip.X, t0 = _world.SimT; Steer(PI);
                Run(240); Steer(0, 0);
                float v1 = (x0 - a.Tip.X) / ((_world.SimT - t0) / 3600);
                Ok(v1 > 31 && v1 < 40, $"neuron: stożek wzrostu przed linią środkową {v1:0.0} µm/h");
                // klin glejowy (Slit2): stożek sterowany w dół przy linii środkowej zapada się i cofa
                a.Advance(Cortex.XM + 64 - a.Tip.X, 280 - a.Tip.Y); a.Dir = PI / 2; float lenMax = a.Length();
                Steer(PI / 2);
                for (int i = 0; i < 60 && a.Collapse <= 0; i++) { Run(10); lenMax = Math.Max(lenMax, a.Length()); }
                Steer(0, 0);
                Ok(a.Collapse > 0 && _world.Stat("n_collapse") >= 1 && a.Length() < lenMax - 2, $"neuron: stożek zapada się w klinie glejowym (Slit2/Robo) (zapadnięcie {a.Collapse:0.00}, strefa {Cortex.At(a.Tip.X, a.Tip.Y + 3)}, stat {_world.Stat("n_collapse")}, długość {a.Length():0.0} / maks. {lenMax:0.0}, gracz {_world.Player?.GetType().Name})");
                // przejście przez linię środkową w ciele modzelowatym i szybszy wzrost za nią (~55 µm/h)
                Start(S("neuron")); ns = (ScNeuron)_sc; ns.DebugJump(_world, 7); a = ns.Me.Ax; Steer(PI);
                for (int i = 0; i < 60 && !a.Crossed; i++) Run(10);
                float x1 = a.Tip.X, t1 = _world.SimT; Run(240); Steer(0, 0);
                float v2 = (x1 - a.Tip.X) / ((_world.SimT - t1) / 3600);
                Ok(a.Crossed && _world.Stat("n_cross") >= 1 && v2 > 47 && v2 < 60, $"neuron: akson przechodzi linię środkową i za nią rośnie {v2:0.0} µm/h");
            }
            {
                // fala śmierci: ~12% neuronów płytki korowej między P2 a P5 (Wong i in. 2018)
                Start(S("neuron")); var ns = (ScNeuron)_sc; ns.DebugJump(_world, 8); ns.NoFate = true; ns.Me.Ax.AtTarget = true;
                _world.SimT = (ScNeuron.Birth + 1.2f - 15.5f) * 86400; Steer(0, 0);
                int dead0 = ns.DeadPost;
                for (int i = 0; i < 200 && ns.Age < ScNeuron.Birth + 7; i++) Run(60);
                float frac = (ns.DeadPost - dead0) / (float)ns.CpCount;
                Ok(frac > 0.08f && frac < 0.17f, $"neuron: w fali P2–P5 ginie {frac * 100:0.0}% neuronów płytki korowej w oknie ({ns.DeadPost - dead0} z {ns.CpCount}; Wong i in. 2018: ~12%)");
                Ok(_world.Ents.OfType<Microglia>().Sum(m => m.Meals) > 0, $"neuron: mikroglej pochłania komórki apoptotyczne ({_world.Ents.OfType<Microglia>().Sum(m => m.Meals):0} posiłków; mikroglej: {_world.Ents.OfType<Microglia>().Count()}, w płytce {_world.Ents.OfType<Microglia>().Count(m => m.Y < 160)}, umierające teraz {ns.Dead.Count})");
            }
            {
                // interneurony w strumieniach: średnio ~12 µm/h (Yanagida i in. 2012)
                Start(S("neuron")); Steer(0, 0); Run(10);
                var ins = _world.Ents.OfType<Interneuron>().Where(e => e.Alive).ToList(); var p0 = ins.Select(e => new Vector2(e.X, e.Y)).ToList(); float t0 = _world.SimT;
                Run(480);
                float h = (_world.SimT - t0) / 3600, sum = 0; int n = 0;
                for (int i = 0; i < ins.Count; i++) if (ins[i].Alive && _world.Ents.Contains(ins[i])) { sum += (new Vector2(ins[i].X, ins[i].Y) - p0[i]).Length() / h; n++; }
                float mean = n > 0 ? sum / n : 0;
                Ok(n >= 10 && mean > 7 && mean < 17, $"neuron: interneurony wędrują stycznie średnio {mean:0.0} µm/h (n = {n})");
            }
            {
                // pełna rozgrywka automatem: sterowanie ku wskazówce (zielona strzałka), bez skoków – od E15,5 do końca
                Start(S("neuron")); var ns = (ScNeuron)_sc; ns.NoFate = true;
                var when = new List<string>(); int gi = 0;
                for (int f = 0; f < 60 * 60 * 12 && !_world.Over; f++)
                {
                    if (f % 6 == 0)
                    {
                        var ap = _world.Player;
                        if (ap != null && _sc.FoodPoint(_world, out float afx, out float afy)) Steer(MathF.Atan2(afy - ap.Y, afx - ap.X));
                        else if (ap is CortNeuron { S: CortNeuron.Stage.Multipolar or CortNeuron.Stage.Locomotion }) Steer(-PI / 2);
                        else Steer(0, 0);
                        // w lokomocji: naczynie na drodze – przejście na sąsiednie włókno
                        if (ap is CortNeuron { S: CortNeuron.Stage.Locomotion } cl && f % 120 == 0 && ns.F.VesselDist(cl.X, cl.Y - 8, out _, out _) < 2) Steer(0);
                    }
                    Run(1);
                    while (gi < _world.GoalIdx) { when.Add($"{gi + 1}:{ScNeuron.AgeStr(ns.Age)}"); gi++; }
                }
                Steer(0, 0);
                GD.Print($"[check]      przebieg: {string.Join(" · ", when)} · koniec {(_world.Over ? _world.OverTitle : "–")} w {ScNeuron.AgeStr(ns.Age)}, t gry {_world.T:0} s, akson {ns.Me?.Ax?.Length() ?? 0:0} µm");
                Ok(_world.GoalIdx >= 8 && _world.Over && ns.Me.SettledAge <= ScNeuron.Birth + 0.5f, $"neuron: pełny cykl automatem – {_world.GoalIdx}/8 etapów, osiadł w {ScNeuron.AgeStr(ns.Me.SettledAge)}");
            }
            {
                // los w oknie śmierci: osiadły na czas przeżywa; heterotopia – duże ryzyko
                Start(S("neuron")); var ns = (ScNeuron)_sc; ns.DebugJump(_world, 8); var me = ns.Me; me.SettledAge = 18.4f; me.Ax.AtTarget = true;
                float r0 = ns.Risk(); me.S = CortNeuron.Stage.Stranded; float r1 = ns.Risk(); me.S = CortNeuron.Stage.Settled;
                _world.SimT = (ScNeuron.Birth + 4.8f - 15.5f) * 86400; Steer(0, 0);
                for (int i = 0; i < 400 && !_world.Over; i++) Run(10);
                Ok(r0 == 0 && r1 >= 0.5f && _world.Stat("n_survive") >= 1 && _world.Over, $"neuron: neuron osiadły na czas przeżywa falę śmierci (ryzyko {r0:0.00}, heterotopia {r1:0.00}), koniec: {(_world.Over ? _world.OverTitle : "–")}");
            }
        }
        catch (Exception ex) { fails++; GD.PrintErr("[check] WYJĄTEK " + ex); }
        GD.Print(fails == 0 ? "[check] WSZYSTKO OK" : $"[check] BŁĘDÓW: {fails}");
        GetTree().Quit(fails == 0 ? 0 : 1);
    }

    /// <summary>Pomiar rzeczywistej płynności (renderowanie na ekranie, zwykła pętla gry).</summary>
    async Task FpsTest(string id)
    {
        _noRecords = true;
        for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        World.Prof = OS.GetCmdlineUserArgs().Contains("--prof");
        var seedArg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--seed="));
        if (seedArg != null) M.Seed(ulong.Parse(seedArg[7..]));
        Start(Scenario.All.First(s => s.Id == id));
        // --stage=N: tryb neuronu od razu w etapie N (pomiar w płytce korowej, przy linii środkowej…)
        var stArg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--stage="));
        if (stArg != null && _sc is ScNeuron snf) snf.DebugJump(_world, int.Parse(stArg[8..]));
        _testInput = true; _input.Mag = 1; _input.Ang = 0.3f; _input.Dx = MathF.Cos(0.3f); _input.Dy = MathF.Sin(0.3f);
        // --advance=S: najpierw S sekund gry bez rysowania (stan w połowie rozgrywki: rozwinięte zakażenie, większy guz)
        var advArg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--advance="));
        if (advArg != null) { int steps = (int)(float.Parse(advArg[10..], System.Globalization.CultureInfo.InvariantCulture) * 60); for (int i = 0; i < steps && !_world.Over; i++) { if (i % 120 == 0) { float a = Rand(0, TAU); _input.Ang = a; _input.Dx = MathF.Cos(a); _input.Dy = MathF.Sin(a); } _world.Update(1f / 60); } GD.Print($"[fps] po {advArg[10..]} s gry: {_world.Ents.Count(e => e.Alive)} encji{(_world.Over ? " (koniec gry)" : "")}"); }
        var zArg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--zoom="));
        if (zArg != null) _tz = _zoom = float.Parse(zArg[7..], System.Globalization.CultureInfo.InvariantCulture);
        // oś czasu klatki: _Process → _Draw świata → FramePreDraw → FramePostDraw → sygnał process_frame (tu wraca await) → sonda → _Process
        long tPre = 0, tPost = 0, prevNow = 0; double[] seg = new double[8]; int segN = 0, segM = 0, nPs = 0;
        Action onPre = () => tPre = System.Diagnostics.Stopwatch.GetTimestamp(), onPost = () => tPost = System.Diagnostics.Stopwatch.GetTimestamp();
        RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePreDraw, Callable.From(onPre));
        RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw, Callable.From(onPost));
        var probe = new TimeProbe { ProcessPriority = -1000000 }; GetTree().Root.AddChild(probe);
        GD.Print($"[fps] karta: {RenderingServer.GetVideoAdapterName()} · {RenderingServer.GetVideoAdapterApiVersion()} · okno {DisplayServer.WindowGetSize()} · tryb {DisplayServer.WindowGetMode()} · vsync {DisplayServer.WindowGetVsyncMode()} · odświeżanie {DisplayServer.ScreenGetRefreshRate():0.#} Hz");
        for (int i = 0; i < 120; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        bool measure = !OS.GetCmdlineUserArgs().Contains("--nomeasure");
        var vp = GetViewport().GetViewportRid(); if (measure) RenderingServer.ViewportSetMeasureRenderTime(vp, true);
        World.ProfT.Clear(); World.VertT.Clear(); Physics.CntN = Physics.CntY = Physics.CntPair = Physics.CntHit = 0;
        double rcpu = 0, rgpu = 0, tproc = 0, ovl = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew(); int n = 0, spikes = 0; double worst = 0, last = 0, upd = 0, bld = 0; var spikeLog = new List<string>();
        // --spikeprof: przy skoku czasu klatki – które sekcje (profil World.PAdd) urosły najbardziej
        bool spikeProf = OS.GetCmdlineUserArgs().Contains("--spikeprof"); var profPrev = new Dictionary<string, double>();
        if (spikeProf) World.Prof = true;
        while (sw.Elapsed.TotalSeconds < 6)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); n++; upd += _updMs; bld += _view.LastBuildMs;
            // sygnał process_frame przychodzi przed _Process bieżącej klatki – poprzednia klatka jest już kompletna
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_procT0 < _view.LastDrawStart && tPre > _view.LastDrawEnd && tPost > tPre && now > tPost)
            {
                double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                seg[0] += (_view.LastDrawStart - _procT0) * f; seg[1] += (_view.LastDrawEnd - _view.LastDrawStart) * f;
                seg[2] += (tPre - _view.LastDrawEnd) * f; seg[3] += (tPost - tPre) * f; seg[4] += (now - tPost) * f; segN++;
                if (prevNow != 0 && _procT0 > prevNow) { seg[5] += (_procT0 - prevNow) * f; if (probe.T > prevNow && probe.T < _procT0) { seg[6] += (probe.T - prevNow) * f; seg[7] += (_procT0 - probe.T) * f; nPs++; } segM++; }
            }
            prevNow = now;
            if (measure) { rcpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(vp); rgpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp); }
            tproc += _procMs; ovl += _overlay.LastMs;
            double t = sw.Elapsed.TotalMilliseconds;
            // wypisywanie w trakcie pomiaru samo kosztuje kilka ms na klatkę (konsola) – skoki tylko zliczane, lista na końcu
            if (t - last > 20) { spikes++; if (spikeLog.Count < 12) spikeLog.Add($"  [spike] klatka {n}: {t - last:0.0} ms (symulacja {_updMs:0.0}, geometria {_view.LastBuildMs:0.0}, t={t / 1000:0.00}s)" + (spikeProf ? " · " + string.Join(", ", World.ProfT.Select(kv => (kv.Key, d: kv.Value - (profPrev.TryGetValue(kv.Key, out var pv) ? pv : 0))).OrderByDescending(q => q.d).Take(5).Select(q => $"{q.Key} {q.d:0.0}")) : "")); }
            if (spikeProf) { profPrev.Clear(); foreach (var kv in World.ProfT) profPrev[kv.Key] = kv.Value; }
            worst = Math.Max(worst, t - last); last = t;
            if (n % 120 == 0) { float a = Rand(0, TAU); _input.Ang = a; _input.Dx = MathF.Cos(a); _input.Dy = MathF.Sin(a); }
        }
        foreach (var sl in spikeLog) GD.Print(sl);
        if (spikes > 0) GD.Print($"  [spike] klatek > 20 ms: {spikes} z {n}");
        if (World.Prof)
        {
            foreach (var kv in World.PartT.OrderByDescending(k => k.Value).Take(6)) GD.Print($"      część {kv.Key,-40} średnio {kv.Value / n:0.00} ms");
            GD.Print($"      kolizje/kl: ciała {Physics.CntN / n}, testy Y {Physics.CntY / n}, pary {Physics.CntPair / n}, trafienia {Physics.CntHit / n}");
            foreach (var kv in World.ProfT.OrderByDescending(k => k.Value).Take(40)) GD.Print($"      {kv.Key,-24} {kv.Value / n:0.000} ms/kl");
            foreach (var kv in World.VertT.OrderByDescending(k => k.Value).Take(14)) GD.Print($"      {kv.Key,-30} {kv.Value / n:0} wierzch./kl");
        }
        if (segN > 0) GD.Print($"[fps] oś czasu klatki (ms): proces→draw {seg[0] / segN:0.00} · draw {seg[1] / segN:0.00} · draw→render {seg[2] / segN:0.00} · render {seg[3] / segN:0.00} · render→skrypty {seg[4] / segN:0.00} · skrypty→następny proces {(segM > 0 ? seg[5] / segM : 0):0.00} (→pierwszy węzeł {(nPs > 0 ? seg[6] / nPs : 0):0.00}, pierwszy węzeł→Main._Process {(nPs > 0 ? seg[7] / nPs : 0):0.00}) (n={segN}, {segM}, {nPs})");
        GD.Print($"[fps] {id}: średnio {n / sw.Elapsed.TotalSeconds:0} FPS, najdłuższa klatka {worst:0.0} ms, symulacja {upd / n:0.00} ms, geometria {bld / n:0.00} ms, render CPU {rcpu / n:0.00} ms / GPU {rgpu / n:0.00} ms, Main._Process {tproc / n:0.00} ms, nakładka {ovl / n:0.00} ms, wierzchołki {_view.LastVertices}, GC0 {GC.CollectionCount(0)}");
        GetTree().Quit();
    }

    async Task Shots(string[] args)
    {
        _noRecords = true;
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
                else if (id == "settings")
                {
                    ShowMenu(); ShowSettings(true);
                    for (int i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"{outDir}/shot_settings_0.png");
                    ShowSettings(false); continue;
                }
                else if (id.StartsWith("info-"))
                {
                    Start(Scenario.All.First(s => s.Id == id[5..]));
                    for (int f = 0; f < 60; f++) _world.Update(1f / 60);
                    ToggleInfo(true);
                    for (int i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"{outDir}/shot_{id}_0.png");
                    ToggleInfo(false);
                    continue;
                }
                else
                {
                    bool nano = id.StartsWith("nano-"); string bare = nano ? id[5..] : id;
                    if (bare == "turn") bare = "turn";
                    string sid = bare == "turn" ? "para" : bare.StartsWith("sp-") ? (bare is "sp-tetra" or "sp-bodo" or "sp-spirillum" or "sp-yeast" ? "bdello" : bare is "sp-macro" or "sp-candida" or "sp-fat" or "sp-fibro" or "sp-mast" ? "neutro" : "para") : bare switch { "vort" => "para", "spiro" => "amoeba", "net" => "neutro", "rotifer" => "didinium", "effero" => "macro", "eudiv" => "euglena", "dagg" => "dicty", "slug" => "dicty", _ => bare.StartsWith("cancer-") ? "cancer" : bare.StartsWith("tryp-") ? "tryp" : bare.StartsWith("neuron-") ? "neuron" : bare.StartsWith("neutro-") ? "neutro" : bare.StartsWith("sperm-") ? "sperm" : bare.StartsWith("zz-") ? bare[3..] : bare.StartsWith("insp-") ? bare[5..] : bare.StartsWith("inspbg-") ? bare[7..] : bare.StartsWith("f8-") ? bare[3..] : bare.StartsWith("set-") ? bare[4..] : bare };
                    Start(Scenario.All.First(s => s.Id == sid), nano);
                    if (bare.StartsWith("sp-"))
                    {
                        var pl = _world.Player; float x = pl.X + _sc.ViewW * 0.12f, y = pl.Y;
                        Ent sp = bare switch
                        {
                            "sp-stentor" => new Protist(_world, Species.Stentor, x, y, -0.4f),
                            "sp-blepharisma" or "sp-lysis" => new Protist(_world, Species.Blepharisma, x, y, 0.3f),
                            "sp-euglena" => new Protist(_world, Species.Euglena, x, y, 0.3f),
                            "sp-loxodes" => new Protist(_world, Species.Loxodes, x, y, 0.3f),
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
                            "sp-yeast" => new Microbe(_world, MicroType.Yeast, x, y, 0.2f),
                            "sp-macro" => new Macrophage(_world, x, y),
                            "sp-fibro" => new Fibroblast(_world, x, y, 0.4f),
                            "sp-mast" => new MastCell(_world, x, y),
                            "sp-candida" => new CandidaHypha(_world, x, y, 0.2f),
                            "sp-fat" => new Adipocyte(_world, x, y, 40),
                            _ => null,
                        };
                        if (sp != null) { _world.Add(sp); _world.Focus = sp; if (sp is Protist pr) pr.Speed *= 0.2f; }
                    }
                    if (bare == "effero" && _world.Player is Macrophage mcp)
                    {
                        var an = _world.Add(new Neutrophil(_world, mcp.X + mcp.R0 + 4, mcp.Y + 3, 0)); an.StartApoptosis(); an.Apo = 1200;
                        _world.Add(new RBC(_world, mcp.X - mcp.R0 - 3, mcp.Y - 2)); _world.Add(new RBC(_world, mcp.X - 2, mcp.Y + mcp.R0 + 3));
                        _world.Add(new Staph(_world, mcp.X + 3, mcp.Y - mcp.R0 - 1)); _world.Add(new Staph(_world, mcp.X + 4, mcp.Y - mcp.R0 - 1.5f));
                    }
                    if (bare == "eudiv" && _world.Player is Protist ep) ep.Paramylon = 1;
                    if (bare.StartsWith("cancer-") && _world.Player is TumorCell tcs && _sc is ScCancer csh)
                    {
                        tcs.Emt = 1; tcs.Amoeboid = bare == "cancer-amo"; tcs.Muts = Muts.Tp53 | Muts.Kras;
                        if (bare == "cancer-blood") { tcs.Y = csh.VesselY(_world, tcs.X, tcs.Y) + 20; csh.EnterBlood(_world, tcs); tcs.Shield = 0.6f; }
                        if (bare == "cancer-mit") { tcs.Emt = 0; tcs.Growth = 0.999f; }
                    }
                    if (bare.StartsWith("tryp-") && _world.Player is Trypanosome tps && _sc is ScTryp trs)
                    {
                        if (bare == "tryp-ab") { trs.SetTiter(0, 1.2f); tps.Ab = 0.5f; }
                        if (bare == "tryp-div") { tps.Growth = 1; tps.Div = 0.55f; }
                        if (bare == "tryp-bite") { tps.Stumpy = 1; trs.StartBite(_world, tps, 30); }
                    }
                    if (bare == "dagg" && _sc is ScDicty sd) sd.DebugAgg(_world);
                    // neuron-N: gracz od razu w etapie N (1–8), np. neuron-4:1 – lokomocja w fluorescencji
                    if (bare.StartsWith("neuron-") && _sc is ScNeuron sn && int.TryParse(bare[7..], out int nst)) sn.DebugJump(_world, nst);
                    // plemnik w dalszych odcinkach: szyjka, cieśń jajowodu, bańka przy komórce jajowej
                    if (bare.StartsWith("sperm-") && _sc is ScSperm ss && _world.Player is Sperm spp)
                    {
                        var reg = bare switch { "sperm-cervix" => ScSperm.Reg.Cervix, "sperm-uterus" => ScSperm.Reg.Uterus, "sperm-isthmus" => ScSperm.Reg.Isthmus, _ => ScSperm.Reg.Ampulla };
                        var (tx, ty, ta) = reg == ScSperm.Reg.Cervix ? ss.Center(reg, 1) : ss.TestSpot(reg, 60);
                        if (bare == "sperm-egg") { tx = ss.Egg.X - ss.Egg.CumR - 40; ty = ss.Egg.Y; ta = 0; }
                        spp.X = tx; spp.Y = ty; spp.A = reg == ScSperm.Reg.Cervix ? ta : ta + PI; spp.Cap = bare == "sperm-egg" ? 1 : 0.3f;
                        for (int f = 0; f < 240; f++) { _input.Mag = 0; _world.CamX = spp.X; _world.CamY = spp.Y; _world.ViewR = _sc.ViewW * 0.6f; _world.Update(1f / 60); }
                    }
                    if (bare == "slug") { var pl0 = _world.Player; var sl = _world.Add(new Slug(_world, pl0.X, pl0.Y, 0, 240)); _world.Player = null; _world.SetPlayer(sl); }
                    if (id == "vort") _world.Focus = _world.Ents.OfType<Vorticella>().FirstOrDefault();
                    if (id == "spiro") _world.Focus = _world.Ents.OfType<SpiroCell>().FirstOrDefault();
                    if (id == "rotifer") _world.Focus = _world.Ents.OfType<Rotifer>().FirstOrDefault();
                    if (id == "net") { _input.ActionHeld = true; for (int f = 0; f < 110; f++) _world.Update(1f / 60); _input.ActionHeld = false; _world.Focus = _world.Ents.OfType<Net>().First(); }
                    _modeIdx = mode % _sc.Modes.Length; ApplyMode();
                    _testInput = true;
                    _input.Mag = 1; _input.Ang = 0.3f; _input.Dx = MathF.Cos(0.3f); _input.Dy = MathF.Sin(0.3f);
                    for (int f = 0; f < frames; f++)
                    {
                        if (bare == "slug") { _input.Mag = 1; _input.Ang = -0.3f + 1.6f * MathF.Sin(f * 0.012f); _input.Dx = MathF.Cos(_input.Ang); _input.Dy = MathF.Sin(_input.Ang); }
                        // zygzak: ostre skręty w lewo i w prawo co 1,5 s (kontrola, czy wnętrze ciała nadąża za obrysem)
                        if (bare.StartsWith("zz-")) { float a0 = 0.3f, zz = (f / 90) % 2 == 0 ? 1.3f : -1.3f; _input.Mag = 1; _input.Ang = a0 + zz; _input.Dx = MathF.Cos(_input.Ang); _input.Dy = MathF.Sin(_input.Ang); }
                        if (bare.StartsWith("neuron-")) { _input.Mag = 0; _input.Dx = _input.Dy = 0; }
                        if (bare == "turn") { _input.Mag = 1; _input.Ang = f * 0.035f; _input.Dx = MathF.Cos(_input.Ang); _input.Dy = MathF.Sin(_input.Ang); _world.CamX = _world.Player.X; _world.CamY = _world.Player.Y; _world.ViewR = 500; }
                        _world.Update(1f / 60);
                        if ((f == frames / 2 && bare is not ("turn" or "slug") && !bare.StartsWith("zz-")) || (f == 0 && bare is "effero" or "eudiv")) { _input.Mag = 0; _input.Dx = _input.Dy = 0; }
                        if (bare == "sp-lysis" && f == frames - 150 && _world.Focus != null) { var fo = _world.Focus; fo.Kill(); _world.Focus = _world.Ents.OfType<Lysis>().LastOrDefault(); }
                    }
                    _testInput = false;
                    // rój: kamera na neutrofilu z największą liczbą sąsiednich neutrofili i bakterii
                    if (bare == "neutro-swarm")
                    {
                        Ent best = null; int bn = -1;
                        foreach (var e in _world.Ents) if (e is Neutrophil nn && !nn.Apoptotic) { int c = _world.Count(o => (o is Neutrophil || o is Staph { State: St.Free }) && Hypot(o.X - e.X, o.Y - e.Y) < 45); if (c > bn) { bn = c; best = e; } }
                        if (best != null) _world.Focus = best;
                    }
                    // f8-<id>: widok szczegółów; set-<id>: ustawienia w trakcie gry (lista sterowania trybu)
                    if (bare.StartsWith("f8-")) SetDebugUi(true, false);
                    if (bare.StartsWith("set-")) ShowSettings(true);
                    if (bare.StartsWith("insp-") && _world.Player != null) { SetInspect(true); var ip = _world.Player; _overlay.InspSel = ip; ShowCard(Atlas.KeyOf(ip), ip); }
                    if (bare.StartsWith("inspbg-")) { SetInspect(true); float bx = _world.CamX + _sc.ViewW * 0.2f, by = _world.CamY - _sc.ViewW * 0.12f; _overlay.InspPoint = new Vector2(bx, by); ShowCard(_sc.InspectAt(_world, bx, by), null); }
                    _tz = _zoom = zoom;
                    var ct = _world.CamTarget(); if (ct != null) { _camX = ct.X; _camY = ct.Y; }
                    for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var img = GetViewport().GetTexture().GetImage();
                string path = $"{outDir}/shot_{id}_{mode}.png";
                img.SavePng(path);
                GD.Print($"[shot] {path}");
                if (_debugUi) SetDebugUi(false, false);
                if (_settings.Visible) ShowSettings(false);
            }
            catch (Exception ex) { GD.PrintErr($"[shot] {id} BŁĄD: {ex}"); }
        }
        GetTree().Quit();
    }
}

/// <summary>Sonda czasu (test płynności): znacznik początku przetwarzania węzłów w klatce.</summary>
public partial class TimeProbe : Node
{
    public long T;
    public override void _Process(double delta) => T = System.Diagnostics.Stopwatch.GetTimestamp();
}

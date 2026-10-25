namespace Mikro;

/// <summary>
/// Tryb podglądu (klawisz F): gra zatrzymuje się, kamerę można przesuwać (WASD, kółko – zoom), a kliknięcie dowolnego
/// obiektu – organizmu, włókna, naczynia, ściany, wody, podłoża – otwiera kartę z prawdziwym zdjęciem, krótkim opisem
/// i najważniejszymi danymi (atlas: res://atlas/atlas.json). Ponowne F wraca do gry.
/// </summary>
public partial class Main
{
    bool _inspect;
    float _inspX, _inspY;
    Control _insp; PanelContainer _inspCard;
    TextureRect _inspPhoto; Label _inspNoPhoto, _inspName, _inspSub, _inspHint;
    RichTextLabel _inspDesc, _inspCredit; GridContainer _inspFacts;

    void BuildInspect()
    {
        var L = new CanvasLayer { Layer = 25 }; AddChild(L);
        _insp = Ui.Full(new Control { Theme = Ui.Theme, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore }); L.AddChild(_insp);
        // pasek z podpowiedzią u góry
        var top = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        top.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.07f, 0.08f, 0.8f), new Color(0.6f, 0.9f, 1f, 0.35f), 12, 1, 10));
        top.AnchorLeft = 0.5f; top.AnchorRight = 0.5f; top.OffsetTop = 14; top.GrowHorizontal = Control.GrowDirection.Both;
        _insp.AddChild(top);
        _inspHint = Ui.L("TRYB PODGLĄDU · kliknij dowolny obiekt", 14, Hex("#bfe8ff"), true, HorizontalAlignment.Center);
        top.AddChild(_inspHint);
        // karta obiektu po prawej
        _inspCard = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(440, 0) };
        _inspCard.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.04f, 0.055f, 0.06f, 0.93f), new Color(1, 1, 1, 0.14f), 16, 1, 16));
        _inspCard.AnchorLeft = 1; _inspCard.AnchorRight = 1; _inspCard.AnchorTop = 0.5f; _inspCard.AnchorBottom = 0.5f;
        _inspCard.OffsetRight = -18; _inspCard.GrowHorizontal = Control.GrowDirection.Begin; _inspCard.GrowVertical = Control.GrowDirection.Both;
        _insp.AddChild(_inspCard);
        var sc = new ScrollContainer { CustomMinimumSize = new Vector2(440, 640), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _inspCard.AddChild(sc);
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; v.AddThemeConstantOverride("separation", 8); sc.AddChild(v);
        var ph = new PanelContainer { CustomMinimumSize = new Vector2(0, 270) };
        ph.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0, 0, 0, 0.35f), new Color(1, 1, 1, 0.08f), 12, 1, 6));
        v.AddChild(ph);
        _inspPhoto = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(0, 258), MouseFilter = Control.MouseFilterEnum.Ignore };
        ph.AddChild(_inspPhoto);
        _inspNoPhoto = Ui.L("", 13, Ui.Dim, false, HorizontalAlignment.Center, true); _inspNoPhoto.VerticalAlignment = VerticalAlignment.Center;
        ph.AddChild(_inspNoPhoto);
        _inspCredit = new RichTextLabel { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _inspCredit.AddThemeFontSizeOverride("normal_font_size", 11); _inspCredit.AddThemeColorOverride("default_color", Ui.Dim);
        _inspCredit.MetaClicked += meta => OS.ShellOpen(meta.AsString());
        v.AddChild(_inspCredit);
        _inspName = Ui.L("", 24, null, true, HorizontalAlignment.Left, true); v.AddChild(_inspName);
        _inspSub = Ui.L("", 14, Hex("#bfe8ff"), false, HorizontalAlignment.Left, true); v.AddChild(_inspSub);
        _inspDesc = new RichTextLabel { AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _inspDesc.AddThemeFontSizeOverride("normal_font_size", 15);
        v.AddChild(_inspDesc);
        _inspFacts = new GridContainer { Columns = 2 }; _inspFacts.AddThemeConstantOverride("h_separation", 14); _inspFacts.AddThemeConstantOverride("v_separation", 5);
        v.AddChild(_inspFacts);
    }

    void SetInspect(bool on)
    {
        if (_mode != Mode.Game || _world == null) return;
        if (on && (_info.Visible || _settings.Visible || _pause.Visible)) return;
        _inspect = on;
        _insp.Visible = on; _inspCard.Visible = false;
        _overlay.Inspect = on; _overlay.InspSel = null; _overlay.InspHover = null; _overlay.InspPoint = null;
        _mouseDown = false;
        if (on) { _inspX = _camX; _inspY = _camY; }
        if (!on) Toast("Powrót do gry");
    }

    Vector2 MouseWorld()
    {
        float s = _s0 * _zoom; var ss = ScreenSize; var mp = GetViewport().GetMousePosition();
        return new Vector2(_camX + (mp.X - ss.X / 2) / s, _camY + (mp.Y - ss.Y / 2) / s);
    }

    /// <summary>Obiekt pod kursorem: najpierw ten, w którego wnętrzu jest punkt (najwyższa warstwa, potem najmniejszy), inaczej najbliższy w promieniu kilku pikseli.</summary>
    Ent InspectPick(float x, float y)
    {
        float tol = 7 / (_s0 * _zoom);
        Ent best = null; float bestScore = float.MaxValue;
        foreach (var e in _world.Ents)
        {
            if (!e.Alive) continue;
            float d;
            if (e is ChainSeg || e.Prim.Cap || e.NParts > 0) d = Geo.PointEnt(x, y, e).D;
            else d = Hypot(e.X - x, e.Y - y) - Math.Max(e.Radius, e.Prim.R);
            if (e.Mem != null && e.Mem.N > 2 && InPoly(e.Mem, x, y)) d = Math.Min(d, -0.01f);
            if (d > tol) continue;
            float size = Math.Max(e.Radius, e.Length * 0.5f);
            // wnętrze przed otoczeniem; wyższa warstwa przed niższą; mniejszy obiekt przed większym (np. bakteria w fagosomie)
            float score = (d > 0 ? 1e7f + d * 1e4f : 0) - e.Layer * 1e5f + size;
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }
    static bool InPoly(Membrane m, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = m.N - 1; i < m.N; j = i++)
            if ((m.Py[i] > y) != (m.Py[j] > y) && x < (m.Px[j] - m.Px[i]) * (y - m.Py[i]) / (m.Py[j] - m.Py[i] + 1e-12f) + m.Px[i]) inside = !inside;
        return inside;
    }

    void InspectClick()
    {
        var p = MouseWorld();
        var e = InspectPick(p.X, p.Y);
        string key = e != null ? Atlas.KeyOf(e) : _sc.InspectAt(_world, p.X, p.Y);
        _overlay.InspSel = e; _overlay.InspPoint = e == null ? p : null;
        ShowCard(key, e);
    }

    void ShowCard(string key, Ent e)
    {
        var entry = Atlas.Get(key);
        if (entry == null && e == null) { _inspCard.Visible = false; return; }
        var t = entry?.Cur;
        _inspName.Text = t != null ? t.Name : Loc.T(e.Name);
        _inspSub.Text = t != null ? t.Sub : Loc.T(e.Latin);
        _inspDesc.Text = t != null ? t.Desc : "";
        foreach (var c in _inspFacts.GetChildren()) c.QueueFree();
        void Row(string k, string val)
        {
            var kl = Ui.L(k, 13, Ui.Dim, true); kl.CustomMinimumSize = new Vector2(120, 0); kl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            var vl = Ui.L(val, 14, null, false, HorizontalAlignment.Left, true); vl.CustomMinimumSize = new Vector2(270, 0);
            _inspFacts.AddChild(kl); _inspFacts.AddChild(vl);
        }
        if (t != null) foreach (var (k, val) in t.Facts) Row(k, val);
        // ten konkretny obiekt w grze
        if (e != null)
        {
            float s = Math.Max(e.Length, e.Radius * 2);
            string sz = s >= 1000 ? $"{s / 1000:0.0#} mm" : s >= 1 ? $"{s:0.#} µm" : $"{s * 1000:0} nm";
            string who = e == _world.Player ? Loc.T("Ty") + " · " : e.Lineage ? Loc.T("Twoja linia") + " · " : "";
            string hp = e.Hp < 0.999f ? $" · {Loc.T("integralność")} {e.Hp * 100:0}%" : "";
            string note = e.Note.Length > 0 ? " · " + Loc.T(e.Note) : "";
            Row(Loc.T("Ten osobnik"), who + sz + hp + note);
        }
        var tex = Atlas.Photo(entry);
        _inspPhoto.Texture = tex;
        _inspNoPhoto.Visible = tex == null;
        _inspNoPhoto.Text = Loc.T(entry == null || entry.Photo.Length == 0 ? "Brak zdjęcia w atlasie" : "Zdjęcie jeszcze nie pobrane");
        _inspCredit.Visible = entry != null && entry.Credit.Length > 0;
        if (_inspCredit.Visible)
            _inspCredit.Text = (Loc.En ? "Photo: " : "Zdjęcie: ") + (entry.Url.Length > 0 ? $"[url={entry.Url}]{entry.Credit}[/url]" : entry.Credit);
        _inspCard.Visible = true;
    }

    /// <summary>Co klatkę w trybie podglądu: przesuwanie widoku i podświetlenie obiektu pod kursorem.</summary>
    void UpdateInspect(float fdt)
    {
        if (!_inspect || _world == null) return;
        float sp = _world.Sc.ViewW * 0.8f / _zoom * fdt;
        if (K(Key.W) || K(Key.Up)) _inspY -= sp;
        if (K(Key.S) || K(Key.Down)) _inspY += sp;
        if (K(Key.A) || K(Key.Left)) _inspX -= sp;
        if (K(Key.D) || K(Key.Right)) _inspX += sp;
        var p = MouseWorld();
        var h = InspectPick(p.X, p.Y);
        _overlay.InspHover = h;
        _overlay.InspHoverName = h != null ? (Atlas.Get(Atlas.KeyOf(h))?.Cur.Name ?? Loc.T(h.Name)) : (Atlas.Get(_sc.InspectAt(_world, p.X, p.Y))?.Cur.Name ?? "");
        _overlay.InspMouse = GetViewport().GetMousePosition();
    }
}

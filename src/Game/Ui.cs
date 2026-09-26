namespace Mikro;

/// <summary>Czcionki, style i fabryki kontrolek UI.</summary>
public static class Ui
{
    public static Font Font, FontBold;
    public static Theme Theme;
    public static readonly Color Txt = Hex("#eef2ea"), Dim = Hex("#a9b3a6"), Good = Hex("#8fe0a0"), Bad = Hex("#ff8a78"), Info = Hex("#9cd2ff");
    public static readonly Color PanelBg = new(0.047f, 0.063f, 0.07f, 0.62f), PanelBorder = new(1, 1, 1, 0.10f);

    static readonly string[] Names = { "Segoe UI", "Inter", "Roboto", "Noto Sans", "Arial", "sans-serif" };

    public static void Init()
    {
        if (Theme != null) return;
        Font = new SystemFont { FontNames = Names, FontWeight = 400, Antialiasing = TextServer.FontAntialiasing.Gray };
        FontBold = new SystemFont { FontNames = Names, FontWeight = 700, Antialiasing = TextServer.FontAntialiasing.Gray };
        Theme = new Theme { DefaultFont = Font, DefaultFontSize = 15 };
        Theme.SetColor("font_color", "Label", Txt);
        Theme.SetStylebox("panel", "PanelContainer", Box(PanelBg, PanelBorder, 14, 1, 14));
        var btn = Box(new Color(1, 1, 1, 0.1f), new Color(1, 1, 1, 0.18f), 10, 1, 10);
        var btnH = Box(new Color(1, 1, 1, 0.16f), new Color(1, 1, 1, 0.3f), 10, 1, 10);
        Theme.SetStylebox("normal", "Button", btn);
        Theme.SetStylebox("hover", "Button", btnH);
        Theme.SetStylebox("pressed", "Button", btnH);
        Theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        Theme.SetColor("font_color", "Button", Txt);
        Theme.SetColor("font_hover_color", "Button", Colors.White);
        Theme.SetFont("font", "Button", FontBold);
        Theme.SetColor("default_color", "RichTextLabel", Hex("#dfe6da"));
        Theme.SetFont("bold_font", "RichTextLabel", FontBold);
        Theme.SetFont("normal_font", "RichTextLabel", Font);
        var sb = Box(new Color(1, 1, 1, 0.12f), new Color(0, 0, 0, 0), 3, 0, 0);
        Theme.SetStylebox("background", "ProgressBar", sb);
        Theme.SetStylebox("scroll", "VScrollBar", Box(new Color(1, 1, 1, 0.05f), new Color(0, 0, 0, 0), 4, 0, 0));
        Theme.SetStylebox("grabber", "VScrollBar", Box(new Color(1, 1, 1, 0.25f), new Color(0, 0, 0, 0), 4, 0, 0));
    }

    public static StyleBoxFlat Box(Color bg, Color border, int radius = 14, int bw = 1, int margin = 14)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = true };
        s.SetBorderWidthAll(bw); s.SetCornerRadiusAll(radius); s.SetContentMarginAll(margin);
        return s;
    }

    public static Label L(string text, int size = 15, Color? col = null, bool bold = false, HorizontalAlignment align = HorizontalAlignment.Left, bool wrap = false)
    {
        var l = new Label { Text = text, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        if (bold) l.AddThemeFontOverride("font", FontBold);
        if (col.HasValue) l.AddThemeColorOverride("font_color", col.Value);
        if (wrap) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; }
        return l;
    }

    public static T Full<T>(T c) where T : Control { c.SetAnchorsPreset(Control.LayoutPreset.FullRect); return c; }

    public static void IgnoreMouse(Node n)
    {
        if (n is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var ch in n.GetChildren()) IgnoreMouse(ch);
    }
}

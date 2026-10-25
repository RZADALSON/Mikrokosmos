namespace Mikro;

/// <summary>Ustawienia gracza (tryb okna, dźwięk, V-Sync) zapisywane w user://settings.cfg.</summary>
public static class Settings
{
    const string PATH = "user://settings.cfg";
    /// <summary>0 = okno, 1 = pełny ekran w oknie (bez ramki), 2 = pełny ekran wyłączny.</summary>
    public static int WindowMode = 1;
    public static float Master = 0.8f, Sfx = 0.7f, Ambient = 0.5f;
    public static bool Muted, VSync = true, ShowFps;
    /// <summary>Rodzaj szumu tła: 0 = woda (hydrofon), 1 = biały, 2 = różowy, 3 = brązowy.</summary>
    public static int Noise = 1;
    public static readonly string[] Noises = { "Woda (hydrofon, zmienna)", "Szum biały (stały)", "Szum różowy (stały, łagodniejszy)", "Szum brązowy (stały, głęboki)" };
    /// <summary>Język interfejsu; przy pierwszym uruchomieniu – język systemu (polski albo angielski).</summary>
    public static string Lang = OS.GetLocaleLanguage() == "pl" ? "pl" : "en";
    public static readonly string[] WindowModes = { "Okno", "Pełny ekran w oknie (bez ramki)", "Pełny ekran (wyłączny)" };

    public static void Load()
    {
        var c = new ConfigFile();
        if (c.Load(PATH) != Error.Ok) return;
        Lang = (string)c.GetValue("ui", "lang", Lang);
        WindowMode = (int)c.GetValue("video", "window_mode", WindowMode);
        VSync = (bool)c.GetValue("video", "vsync", VSync);
        ShowFps = (bool)c.GetValue("video", "fps", ShowFps);
        Master = (float)c.GetValue("audio", "master", Master);
        Sfx = (float)c.GetValue("audio", "sfx", Sfx);
        Ambient = (float)c.GetValue("audio", "ambient", Ambient);
        Muted = (bool)c.GetValue("audio", "muted", Muted);
        Noise = Math.Clamp((int)c.GetValue("audio", "noise", Noise), 0, 3);
    }
    public static void Save()
    {
        var c = new ConfigFile();
        c.SetValue("video", "window_mode", WindowMode); c.SetValue("video", "vsync", VSync); c.SetValue("video", "fps", ShowFps);
        c.SetValue("ui", "lang", Lang);
        c.SetValue("audio", "master", Master); c.SetValue("audio", "sfx", Sfx); c.SetValue("audio", "ambient", Ambient); c.SetValue("audio", "muted", Muted); c.SetValue("audio", "noise", Noise);
        c.Save(PATH);
    }

    /// <summary>Stosuje tryb okna. Okno zawsze mieści się w obszarze roboczym ekranu (bez pasków przewijania i ucinania).</summary>
    public static void ApplyWindow()
    {
        if (DisplayServer.GetName() == "headless") return;
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        switch (WindowMode)
        {
            case 2: DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen); break;
            case 1: DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen); break;
            default:
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                int scr = DisplayServer.WindowGetCurrentScreen();
                var ur = DisplayServer.ScreenGetUsableRect(scr);
                float k = Math.Min(1, Math.Min(ur.Size.X * 0.9f / 1600f, ur.Size.Y * 0.88f / 900f));
                var size = new Vector2I((int)(1600 * k), (int)(900 * k));
                DisplayServer.WindowSetSize(size);
                DisplayServer.WindowSetPosition(ur.Position + (ur.Size - size) / 2);
                break;
            }
        }
    }
}

namespace Mikro;

/// <summary>
/// Rekordy i ukończone etapy (user://records.cfg). Rekord to dostosowanie linii (klucz „fit”) – dawne wyniki
/// („best”: zjedzone, zabite) liczyły co innego, więc nie są z nim porównywane.
/// </summary>
public static class Records
{
    const string Path = "user://records.cfg";
    static ConfigFile _cfg;
    static ConfigFile Cfg { get { if (_cfg == null) { _cfg = new ConfigFile(); _cfg.Load(Path); } return _cfg; } }
    public static float Best(string id) => (float)Cfg.GetValue(id, "fit", 0f).AsDouble();
    public static int GoalsDone(string id) => Cfg.GetValue(id, "goals", 0).AsInt32();
    public static bool Submit(string id, float score, int goals)
    {
        bool rec = score > Best(id);
        if (rec) Cfg.SetValue(id, "fit", score);
        if (goals > GoalsDone(id)) Cfg.SetValue(id, "goals", goals);
        Cfg.Save(Path);
        return rec;
    }
}

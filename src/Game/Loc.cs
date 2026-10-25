using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Mikro;

/// <summary>
/// Lokalizacja. Tekst w kodzie jest po polsku; dla innych języków tłumaczy się go w chwili wyświetlenia
/// (tabela <c>res://lang/&lt;język&gt;.tsv</c>: „polski⇥tłumaczenie”, z ucieczkami jak w C#: \n, \t, \", \\).
/// Wpisy z miejscami {0}, {1}… to szablony dla tekstów składanych w locie – wstawki same też są tłumaczone.
/// Tekst, którego nie ma w tabeli, dzielony jest na wiersze i na części rozdzielone „ · ”.
/// Etykiety i przyciski tłumaczy automatycznie Godot (TranslationServer → <see cref="Tr"/>).
/// </summary>
public static class Loc
{
    public static string Lang { get; private set; } = "pl";
    public static bool En => Lang != "pl";
    public static readonly string[] Langs = { "pl", "en" };
    public static readonly string[] LangNames = { "Polski", "English" };

    static readonly Dictionary<string, string> _exact = new();
    static readonly List<(Regex re, string tpl)> _tpl = new();
    static readonly Dictionary<string, string> _cache = new();
    /// <summary>Teksty bez tłumaczenia (tryb deweloperski: MIKRO_LOC_MISS=plik).</summary>
    public static readonly HashSet<string> Miss = new();
    static LocTranslation _tr;

    public static void Set(string lang)
    {
        Lang = Array.IndexOf(Langs, lang) >= 0 ? lang : "pl";
        _exact.Clear(); _tpl.Clear(); _cache.Clear();
        // liczby: przecinek dziesiętny po polsku, kropka po angielsku
        var ci = CultureInfo.GetCultureInfo(En ? "en-US" : "pl-PL");
        CultureInfo.DefaultThreadCurrentCulture = ci; CultureInfo.CurrentCulture = ci;
        if (En) Load($"res://lang/{Lang}.tsv");
        if (_tr == null) { _tr = new LocTranslation { Locale = "en" }; TranslationServer.AddTranslation(_tr); }
        TranslationServer.SetLocale(En ? "en" : "pl");
    }

    static void Load(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) { GD.PrintErr($"[loc] brak pliku {path}"); return; }
        while (!f.EofReached())
        {
            string line = f.GetLine();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int tab = line.IndexOf('\t'); if (tab <= 0) continue;
            string pl = Unescape(line[..tab]), en = Unescape(line[(tab + 1)..]);
            if (en.Length == 0) continue;
            Add(pl, en);
            // wersja bez białych znaków na brzegach (np. nowy wiersz przed „Przeżycie…” albo „  Menu  ”)
            string pt = pl.Trim(), et = en.Trim();
            if (pt != pl && pt.Length > 0) Add(pt, et);
        }
        // dłuższe szablony najpierw (bardziej szczegółowe)
        _tpl.Sort((a, b) => b.tpl.Length.CompareTo(a.tpl.Length));
    }

    static void Add(string pl, string en)
    {
        if (Regex.IsMatch(pl, @"\{\d+\}")) _tpl.Add((Compile(pl), en));
        else _exact.TryAdd(pl, en);
    }

    static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n switch { 'n' => '\n', 't' => '\t', '"' => '"', '\\' => '\\', _ => n });
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    static Regex Compile(string pl)
    {
        var sb = new StringBuilder("^");
        int i = 0;
        foreach (Match m in Regex.Matches(pl, @"\{(\d+)\}"))
        {
            sb.Append(Regex.Escape(pl[i..m.Index].Replace("{{", "{").Replace("}}", "}")));
            sb.Append($"(?<g{m.Groups[1].Value}>.*?)");
            i = m.Index + m.Length;
        }
        sb.Append(Regex.Escape(pl[i..].Replace("{{", "{").Replace("}}", "}"))).Append('$');
        return new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    /// <summary>Tłumaczenie tekstu do wyświetlenia.</summary>
    public static string T(string s)
    {
        if (!En || string.IsNullOrEmpty(s)) return s;
        if (_cache.TryGetValue(s, out var r)) return r;
        r = Translate(s, 0);
        if (_cache.Count > 20000) _cache.Clear();
        _cache[s] = r;
        return r;
    }

    static string Translate(string s, int depth)
    {
        if (_exact.TryGetValue(s, out var e)) return e;
        if (depth > 3 || s.Length == 0) return s;
        if (TryTemplates(s, depth, out var tr)) return tr;
        // białe znaki na brzegach zachowujemy (np. „  Menu  ” na przyciskach)
        int a = 0, b = s.Length;
        while (a < b && char.IsWhiteSpace(s[a])) a++;
        while (b > a && char.IsWhiteSpace(s[b - 1])) b--;
        if (a > 0 || b < s.Length)
        {
            if (a == b) return s;
            return s[..a] + Translate(s[a..b], depth + 1) + s[b..];
        }
        if (s.Contains('\n')) return string.Join("\n", s.Split('\n').Select(x => Translate(x, depth + 1)));
        if (s.Contains(" · ")) return string.Join(" · ", s.Split(" · ").Select(x => Translate(x, depth + 1)));
        if (s.StartsWith("•  ")) return "•  " + Translate(s[3..], depth + 1);
        // „nazwa (łacina)” – tłumaczymy część przed nawiasem
        var mm = Regex.Match(s, @"^(.+?) \(([^()]*)\)$");
        if (mm.Success) return Translate(mm.Groups[1].Value, depth + 1) + " (" + Translate(mm.Groups[2].Value, depth + 1) + ")";
        if (s.Length > 2 && LooksPolish(s) && !s.StartsWith("[")) Miss.Add(s);
        return s;
    }

    static bool TryTemplates(string s, int depth, out string r)
    {
        foreach (var (re, tpl) in _tpl)
        {
            var m = re.Match(s);
            if (!m.Success) continue;
            r = Regex.Replace(tpl, @"\{(\d+)\}", g =>
            {
                var grp = m.Groups["g" + g.Groups[1].Value];
                return grp.Success ? Translate(grp.Value, depth + 1) : g.Value;
            });
            return true;
        }
        r = s; return false;
    }

    /// <summary>Czy tekst wygląda na polski (do listy braków): same liczby z jednostkami i nagłówki ASCII pomijamy.</summary>
    static bool LooksPolish(string s)
    {
        string t = Regex.Replace(s, @"(µm|mm|nm|ms|°C|/min|kpz|FPS)", "");
        if (!Regex.IsMatch(t, @"\p{L}{2}")) return false;
        if (Regex.IsMatch(s, "[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]")) return true;
        return !(s == s.ToUpperInvariant() && Regex.IsMatch(s, "^[\x00-\x7F]+$"));
    }

    /// <summary>Wielkie litery zgodne z językiem (nagłówki).</summary>
    public static string Up(string s) => T(s).ToUpper(CultureInfo.CurrentCulture);

    public static void DumpMisses()
    {
        var path = System.Environment.GetEnvironmentVariable("MIKRO_LOC_MISS");
        if (string.IsNullOrEmpty(path) || Miss.Count == 0) return;
        System.IO.File.WriteAllLines(path, Miss.OrderBy(x => x));
        GD.Print($"[loc] {Miss.Count} tekstów bez tłumaczenia → {path}");
    }
}

/// <summary>Most do Godota: każda kontrolka z auto-tłumaczeniem pyta tu o swój tekst.</summary>
public partial class LocTranslation : Translation
{
    public override StringName _GetMessage(StringName srcMessage, StringName context) => Loc.T(srcMessage);
    public override StringName _GetPluralMessage(StringName srcMessage, StringName srcPluralMessage, int n, StringName context) => Loc.T(srcMessage);
}

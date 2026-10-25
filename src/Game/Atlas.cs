using System.Text.Json;
namespace Mikro;

/// <summary>
/// Atlas (tryb podglądu, klawisz F): karta obiektu – prawdziwe zdjęcie, krótki opis i najważniejsze dane.
/// Dane w <c>res://atlas/atlas.json</c> (po polsku i angielsku), zdjęcia w <c>res://atlas/photos/</c>
/// (głównie Wikimedia Commons – autor i licencja zapisane przy każdym zdjęciu i pokazywane na karcie).
/// </summary>
public static class Atlas
{
    public sealed class Text { public string Name = "", Sub = "", Desc = ""; public List<(string k, string v)> Facts = new(); }
    public sealed class Entry
    {
        public string Key = "", Photo = "", Credit = "", Url = "";
        public Text Pl = new(), En = new();
        public Text Cur => Loc.En && En.Name.Length > 0 ? En : Pl;
    }
    static Dictionary<string, Entry> _db;
    static readonly Dictionary<string, Texture2D> _tex = new();

    public static Entry Get(string key)
    {
        if (_db == null) Load();
        return key != null && _db.TryGetValue(key, out var e) ? e : null;
    }
    public static int Count { get { if (_db == null) Load(); return _db.Count; } }
    public static IEnumerable<string> Keys { get { if (_db == null) Load(); return _db.Keys; } }

    static void Load()
    {
        _db = new();
        using var f = FileAccess.Open("res://atlas/atlas.json", FileAccess.ModeFlags.Read);
        if (f == null) { GD.PrintErr("[atlas] brak res://atlas/atlas.json"); return; }
        using var doc = JsonDocument.Parse(f.GetAsText());
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            var o = p.Value; var e = new Entry { Key = p.Name };
            if (o.TryGetProperty("photo", out var ph)) e.Photo = ph.GetString() ?? "";
            if (o.TryGetProperty("credit", out var cr)) e.Credit = cr.GetString() ?? "";
            if (o.TryGetProperty("url", out var u)) e.Url = u.GetString() ?? "";
            if (o.TryGetProperty("pl", out var pl)) e.Pl = ReadText(pl);
            if (o.TryGetProperty("en", out var en)) e.En = ReadText(en);
            _db[p.Name] = e;
        }
    }
    static Text ReadText(JsonElement o)
    {
        var t = new Text();
        if (o.TryGetProperty("name", out var n)) t.Name = n.GetString() ?? "";
        if (o.TryGetProperty("sub", out var s)) t.Sub = s.GetString() ?? "";
        if (o.TryGetProperty("desc", out var d)) t.Desc = d.GetString() ?? "";
        if (o.TryGetProperty("facts", out var fs))
            foreach (var row in fs.EnumerateArray())
                if (row.GetArrayLength() >= 2) t.Facts.Add((row[0].GetString() ?? "", row[1].GetString() ?? ""));
        return t;
    }

    /// <summary>Zdjęcie z <c>res://atlas/photos/</c>; działa bez importu Godota (surowe bajty PNG/JPG/WebP) i po imporcie.</summary>
    public static Texture2D Photo(Entry e)
    {
        if (e == null || e.Photo.Length == 0) return null;
        if (_tex.TryGetValue(e.Photo, out var t)) return t;
        string path = "res://atlas/photos/" + e.Photo;
        t = null;
        if (FileAccess.FileExists(path))
        {
            var bytes = FileAccess.GetFileAsBytes(path);
            var img = new Image();
            string ext = e.Photo[(e.Photo.LastIndexOf('.') + 1)..].ToLowerInvariant();
            var err = ext switch { "png" => img.LoadPngFromBuffer(bytes), "jpg" or "jpeg" => img.LoadJpgFromBuffer(bytes), "webp" => img.LoadWebpFromBuffer(bytes), _ => Error.FileUnrecognized };
            if (err == Error.Ok) { img.GenerateMipmaps(); t = ImageTexture.CreateFromImage(img); }
        }
        if (t == null && ResourceLoader.Exists(path)) t = GD.Load<Texture2D>(path);
        _tex[e.Photo] = t;
        return t;
    }

    /// <summary>Klucz atlasu dla encji (gatunek, typ, wariant).</summary>
    public static string KeyOf(Ent e) => e switch
    {
        ChainSeg cs => KeyOf(cs.Owner),
        Protist p => p.S.Id,
        Microbe m => m.T switch { MicroType.Bacillus => "bacillus", MicroType.Pseudomonas => "pseudomonas", MicroType.Caulobacter => "caulobacter", MicroType.Spirillum => "spirillum", _ => "yeast" },
        Chain c => c.T == Chain.Type.Nematode ? "nematode" : "oscillatoria",
        OtherPhage op => op.Lambda ? "lambda" : "t7",
        Staph s => s.Strep ? "streptococcus" : "staphylococcus",
        Killer k => k.NK ? "nk" : "ctl",
        Tardigrade t => t.Predator ? "milnesium" : "tardigrade",
        Paramecium pm => pm.Green ? "paramecium-bursaria" : "paramecium",
        Leuko => "neutrophil",
        PhageT4 => "t4",
        EColi => "ecoli",
        CortNeuron cn => cn.S <= CortNeuron.Stage.Mitosis ? "ipc" : "cortneuron",
        _ => e.GetType().Name.ToLowerInvariant(),
    };
}

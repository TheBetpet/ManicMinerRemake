using System.Text.Json;
using System.Text.Json.Nodes;
using ManicMiner.Core;

namespace ManicMiner.Remake;

/// <summary>
/// A group of caverns that can be played as an expansion / alternative to the original 20.
/// Stored as a single <c>.mmpack</c> JSON file: { Name, Author, Description, Levels: [ ... ] }.
/// </summary>
public sealed class ExpansionPack
{
    public const string Extension = ".mmpack";

    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public List<LevelData> Levels { get; set; } = new();
    public string? Path { get; set; }

    public static string UserFolder =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ManicMinerPacks");

    /// <summary>Folders searched for packs: the user's Documents folder and a Packs folder next to the program.</summary>
    public static IEnumerable<string> SearchFolders()
    {
        yield return UserFolder;
        yield return System.IO.Path.Combine(AppContext.BaseDirectory, "Packs");
    }

    public static string FileNameFor(string name)
    {
        var s = new string(name.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        return (s.Length == 0 ? "pack" : s) + Extension;
    }

    public void Save(string path)
    {
        if (Levels.Count == 0) throw new InvalidOperationException("A pack needs at least one cavern.");
        var arr = new JsonArray();
        foreach (var l in Levels) arr.Add(JsonNode.Parse(l.ToJson()));
        var root = new JsonObject { ["Name"] = Name, ["Author"] = Author, ["Description"] = Description, ["Levels"] = arr };
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Path = path;
    }

    public static ExpansionPack Load(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException("Not a pack file.");
        var p = new ExpansionPack
        {
            Name = (string?)root["Name"] ?? System.IO.Path.GetFileNameWithoutExtension(path),
            Author = (string?)root["Author"] ?? "",
            Description = (string?)root["Description"] ?? "",
            Path = path,
        };
        foreach (var n in root["Levels"]?.AsArray() ?? new JsonArray())
        {
            p.Levels.Add(LevelData.FromJson(n!.ToJsonString()));
        }
        if (p.Levels.Count == 0) throw new InvalidDataException("The pack contains no caverns.");
        return p;
    }

    /// <summary>Find every readable pack; unreadable files are reported through <paramref name="errors"/>.</summary>
    public static List<ExpansionPack> Discover(List<string>? errors = null)
    {
        var list = new List<ExpansionPack>();
        foreach (var dir in SearchFolders().Where(Directory.Exists))
            foreach (var f in Directory.GetFiles(dir, "*" + Extension).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                try { list.Add(Load(f)); }
                catch (Exception ex) { errors?.Add($"{System.IO.Path.GetFileName(f)}: {ex.Message}"); }
            }
        return list;
    }
}

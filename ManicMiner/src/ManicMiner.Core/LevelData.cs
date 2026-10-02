using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManicMiner.Core;

public enum SpecialHandler { None = 0, Eugene, Kong, Skylab, SolarGenerator, FinalBarrier }

public sealed class GuardianData
{
    public int Steps { get; set; }
    public int Reload { get; set; }
    public int SpriteSet { get; set; }
    public int Velocity { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Flags { get; set; }
}

/// <summary>One cavern in the form stored in the original game's level table.</summary>
public sealed class LevelData
{
    public int Index { get; set; }
    public string Name { get; set; } = new string(' ', 32);
    public int Border { get; set; }
    public int Aux { get; set; }
    public int Pen2 { get; set; }
    public int Pen1 { get; set; }
    public int Pen0 { get; set; }
    public int[] TileGfx { get; set; } = new int[15];
    public int[][] Layout { get; set; } = Enumerable.Range(0, 16).Select(_ => new int[32]).ToArray();
    public List<GuardianData> Guardians { get; set; } = new();
    public int WillyX { get; set; }
    public int WillyY { get; set; }
    public int PortalX { get; set; }
    public int PortalY { get; set; }
    public int Air { get; set; } = 0xDF;
    /// <summary>Index (0..19) of the portal picture at 0x2B00; -1 = same as the level index.</summary>
    public int PortalGfx { get; set; } = -1;
    public SpecialHandler Special { get; set; }

    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static LevelData FromJson(string json) => JsonSerializer.Deserialize<LevelData>(json, Opts)!;
    public string ToJson() => JsonSerializer.Serialize(this, Opts);

    public static List<LevelData> LoadOriginal(string levelsJsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(levelsJsonPath));
        var list = new List<LevelData>();
        var specials = new Dictionary<int, SpecialHandler>();
        foreach (var p in doc.RootElement.GetProperty("specialHandlers").EnumerateObject())
            specials[int.Parse(p.Name)] = Enum.Parse<SpecialHandler>(p.Value.GetString()!);
        foreach (var e in doc.RootElement.GetProperty("levels").EnumerateArray())
        {
            var l = JsonSerializer.Deserialize<LevelData>(e.GetRawText(), Opts)!;
            l.Special = specials.TryGetValue(l.Index, out var s) ? s : SpecialHandler.None;
            list.Add(l);
        }
        return list;
    }

    public LevelData Clone() => FromJson(ToJson());
}

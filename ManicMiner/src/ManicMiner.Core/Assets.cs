namespace ManicMiner.Core;

/// <summary>Binary assets extracted from the original game (see tools/extract.py).</summary>
public sealed class AssetPack
{
    public byte[] Sprites = Array.Empty<byte>();   // RAM 0x0100..0x3000
    public byte[] Tiles = Array.Empty<byte>();     // RAM 0x5386..0x58E6
    public byte[] Tables = Array.Empty<byte>();    // RAM 0x5200..0x5386
    public byte[] Data6704 = Array.Empty<byte>();  // RAM 0x6704..0x6E3F
    public byte[] Title = Array.Empty<byte>();     // RAM 0x80D1..0x8200
    public byte[] Keys = Array.Empty<byte>();      // RAM 0xA000..0xA500 (title key graphics)
    public byte[] Font = Array.Empty<byte>();      // 256 * 8 (CPC firmware font)
    public List<LevelData> Levels = new();

    public static AssetPack Load(string dir)
    {
        var a = new AssetPack
        {
            Sprites = File.ReadAllBytes(Path.Combine(dir, "sprites.bin")),
            Tiles = File.ReadAllBytes(Path.Combine(dir, "tiles.bin")),
            Tables = File.ReadAllBytes(Path.Combine(dir, "tables_5200.bin")),
            Data6704 = File.ReadAllBytes(Path.Combine(dir, "data_6704.bin")),
            Title = File.ReadAllBytes(Path.Combine(dir, "data_80D1.bin")),
            Keys = File.ReadAllBytes(Path.Combine(dir, "data_A000.bin")),
            Font = File.ReadAllBytes(Path.Combine(dir, "font.bin")),
            Levels = LevelData.LoadOriginal(Path.Combine(dir, "levels.json")),
        };
        return a;
    }
}

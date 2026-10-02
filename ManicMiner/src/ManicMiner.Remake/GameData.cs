using ManicMiner.Core;

namespace ManicMiner.Remake;

/// <summary>
/// The read-only data the original game kept in RAM: sprites, tiles, tunes, texts and tables, plus the
/// cavern definitions. It is never modified at run time - all changing state lives in <see cref="MinerGame"/>.
/// Addresses are those of the original program so they can be traced against the disassembly.
/// </summary>
public sealed class GameData
{
    readonly byte[] _rom = new byte[65536];
    public byte[] Font { get; }
    /// <summary>The 8x8 tile graphics (16 bytes each), as stored at 0x5386.</summary>
    public byte[] Tiles { get; private set; } = Array.Empty<byte>();
    public List<LevelData> Levels { get; }

    GameData(byte[] font, List<LevelData> levels) { Font = font; Levels = levels; }

    public static GameData Load(string dir)
    {
        byte[] F(string n) => File.ReadAllBytes(Path.Combine(dir, n));
        var d = new GameData(F("font.bin"), LevelData.LoadOriginal(Path.Combine(dir, "levels.json")));
        void Put(int at, byte[] b) => Array.Copy(b, 0, d._rom, at, b.Length);
        Put(0x0100, F("sprites.bin"));
        Put(0x5200, F("tables_5200.bin"));
        d.Tiles = F("tiles.bin");
        Put(0x5386, d.Tiles);
        Put(0x6704, F("data_6704.bin"));
        Put(0x80D1, F("data_80D1.bin"));
        Put(0xA000, F("data_A000.bin"));
        return d;
    }

    public byte this[int a] => _rom[a & 0xFFFF];
    public int Word(int a) => _rom[a & 0xFFFF] | (_rom[(a + 1) & 0xFFFF] << 8);
    public ReadOnlySpan<byte> Span(int a, int n) => new(_rom, a, n);

    // ---- graphics ---------------------------------------------------------------------
    /// <summary>Willy's eight 16x16 frames (1..4 facing right, 5..8 facing left).</summary>
    public static int WillyFrame(int n) => 0x600 + 0x40 * n;
    public const int Blank = 0x69FA;            // an all-zero sprite
    public static int Tile(int n) => 0x5386 + 16 * n;
    public static int PortalFrame(int n) => 0x2B00 + 0x40 * n;
    public int GuardianSet(int set) => Word(0x5348 + 2 * set);
    public int KongFrame => Word(0x5384);
    public const int Switch1 = 0x5606, Switch2 = 0x5616;     // the Kong switch tiles
    public const int TitleStrip = 0x2800;                    // 12 pixel rows of the title piano strip
    public const int FootA = 0x27C0, FootB = 0x06C0, FootC = 0x0C80;   // game-over Foot sprites
    public const int HudTiles = 0x6718, HudColours = 0x6731, TitleTiles = 0x80D1;
    public const int ConveyorA = 0x679C, ConveyorB = 0x67A4;
    public const int ScrollText = 0x6A78, ScrollLength = 0x3A7 + 0x20;
    public const int GameTune = 0x6938, TitleTune = 0x67FB;
    public const int SkylabTable = 0x6A45;
    public const int PaletteCycle = 0x815B;

    public ReadOnlySpan<byte> Sprite(int addr) => Span(addr, 64);
    public ReadOnlySpan<byte> TileGfx(int tile) => Span(Tile(tile), 16);

    /// <summary>Pen colours (0..3 as bit patterns) used by guardians: 0x6798 table.</summary>
    public int GuardianColour(int n) => this[0x6798 + (n & 3)];
}

using ManicMiner.Core;
using ManicMiner.Remake;

namespace ManicMiner.Tests;

static class TestAssets
{
    public static readonly string Folder = Find();
    public static readonly GameData Data = GameData.Load(Folder);
    public static AssetPack Core => AssetPack.Load(Folder);

    static string Find()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string p = Path.Combine(d.FullName, "Assets");
            if (File.Exists(Path.Combine(p, "levels.json"))) return p;
        }
        throw new DirectoryNotFoundException("Assets folder not found above " + AppContext.BaseDirectory);
    }

    public const byte Up = 0, Right = 0;   // keyboard matrix line 0
    /// <summary>Hold the given matrix bits on a game (line 0: 0x01 up, 0x02 right-arrow; line 1: 0x01 left-arrow).</summary>
    public static void Hold(MinerGame g, int line, byte bits)
    {
        Array.Clear(g.KeyMatrix);
        g.KeyMatrix[line] = bits;
    }

    public static MinerGame TestPlay(int level)
    {
        var g = new MinerGame(Data) { TestLevel = Data.Levels[level].Clone() };
        g.Start();
        return g;
    }

    public static byte[] Snapshot(MinerGame g) => (byte[])g.Screen.Mem.Clone();
}

using ManicMiner.Core;
using ManicMiner.Remake;
using Xunit;

namespace ManicMiner.Tests;

/// <summary>The Remake must match the literal Z80 port (Core) pixel for pixel when given the same input.</summary>
public class ParityTests
{
    static void Compare(Func<Engine> mkCore, Func<MinerGame> mkRemake, int ticks, int seed, bool sparse)
    {
        using var c = mkCore(); using var r = mkRemake();
        c.Start(); r.Start();
        var rng = new Random(seed);
        var pc = new uint[Cpc.Width * Cpc.Height]; var pr = new uint[pc.Length];
        for (int t = 0; t < ticks; t++)
        {
            if (t % 40 == 0)
                for (int i = 0; i < 10; i++)
                    c.KeyMatrix[i] = r.KeyMatrix[i] = sparse ? (byte)(rng.Next(3) == 0 ? (1 << rng.Next(8)) & rng.Next(256) : 0) : (byte)(rng.Next(4) == 0 ? rng.Next(256) : 0xFF);
            c.LatchKeys(); r.LatchKeys();
            c.Tick(); r.Tick();
            c.RenderScreen(pc); r.RenderScreen(pr);
            Assert.True(pc.AsSpan().SequenceEqual(pr), $"screens differ at tick {t}");
            if (c.TestPlayEnded) { Assert.True(r.TestPlayEnded); break; }
        }
    }

    [Fact]
    public void TitleScreenMatches() =>
        Compare(() => new Engine(TestAssets.Core), () => new MinerGame(TestAssets.Data), 2000, 1, true);

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(4)] [InlineData(7)] [InlineData(11)]
    [InlineData(13)] [InlineData(14)] [InlineData(16)] [InlineData(18)] [InlineData(19)]
    public void CavernMatches(int level) =>
        Compare(() => new Engine(TestAssets.Core) { TestLevel = TestAssets.Core.Levels[level] },
                () => new MinerGame(TestAssets.Data) { TestLevel = TestAssets.Data.Levels[level].Clone() }, 3000, level + 1, level % 2 == 0);
}

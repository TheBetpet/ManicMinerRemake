using ManicMiner.Core;
using ManicMiner.Remake;
using Xunit;

namespace ManicMiner.Tests;

public class GameTests
{
    static void Run(MinerGame g, int ticks, int line = -1, byte bits = 0)
    {
        for (int t = 0; t < ticks; t++)
        {
            if (line >= 0) TestAssets.Hold(g, line, bits);
            g.LatchKeys();
            g.Tick();
        }
    }

    [Fact]
    public void GameStartsOnTheTitleScreen()
    {
        using var g = new MinerGame(TestAssets.Data);
        g.Start();
        g.Advance(40);
        Assert.True(g.Running);
        Assert.True(g.ScreenOn);
        Assert.Contains(g.Screen.Mem, b => b != 0);
    }

    [Fact]
    public void TickAdvancesTime()
    {
        using var g = new MinerGame(TestAssets.Data);
        g.Start();
        g.Advance(10);
        Assert.Equal(10, g.Time);
    }

    [Fact]
    public void TestPlayShowsTheCavern()
    {
        using var g = TestAssets.TestPlay(0);
        g.Advance(100);
        Assert.True(g.ScreenOn);
        Assert.False(g.TestPlayEnded);
        Assert.Equal(TestAssets.Data.Levels[0].Border, g.BorderInk);
    }

    [Fact]
    public void LevelStartedCallbackReportsTheIndex()
    {
        using var g = TestAssets.TestPlay(3);
        int seen = -1;
        g.LevelStarted = i => seen = i;
        g.Advance(100);
        Assert.Equal(0, seen);      // test play always runs a single-cavern list
    }

    [Fact]
    public void SameInputGivesTheSameScreen()
    {
        byte[] Play()
        {
            using var g = TestAssets.TestPlay(1);
            for (int t = 0; t < 1500; t++)
            {
                if (t % 100 == 0) TestAssets.Hold(g, 0, (byte)((t / 100) % 2 == 0 ? 0x02 : 0x01));
                g.LatchKeys(); g.Tick();
            }
            return TestAssets.Snapshot(g);
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void HoldingRightMovesWilly()
    {
        using var g = TestAssets.TestPlay(0);
        g.Advance(200);
        var before = TestAssets.Snapshot(g);
        Run(g, 150, 0, 0x02);
        Assert.NotEqual(before, TestAssets.Snapshot(g));
    }

    [Fact]
    public void WillyDoesNotMoveWithoutInput()
    {
        using var g = TestAssets.TestPlay(0);
        g.Advance(200);
        // only the conveyor, items, guardians and air animate; Willy's own rows must stay put
        var l = TestAssets.Data.Levels[0];
        int xb = (l.WillyX >> 2) * 2;
        byte[] Row(int y) => Enumerable.Range(0, 4).Select(i => g.Screen[Screen.RowAddr(y) + xb + i]).ToArray();
        var a = Row(l.WillyY + 8);
        Run(g, 200);
        Assert.Equal(a, Row(l.WillyY + 8));
    }

    [Fact]
    public void JumpingChangesTheScreenAndThenWillyLands()
    {
        using var g = TestAssets.TestPlay(0);
        g.Advance(200);
        var start = TestAssets.Snapshot(g);
        Run(g, 25 * 6, 0, 0x01);              // hold jump (Up) for a few frames
        Assert.NotEqual(start, TestAssets.Snapshot(g));
        Run(g, 25 * 40);                      // let him land
        Assert.True(g.Running);
        Assert.False(g.TestPlayEnded);
    }

    [Fact]
    public void AirSupplyRunsOut_EndingTestPlayEventually()
    {
        var l = TestAssets.Data.Levels[0].Clone();
        l.Air = 10;                           // a tiny air supply: three lives run out quickly
        using var g = new MinerGame(TestAssets.Data) { TestLevel = l };
        g.Start();
        Run(g, 12000);
        Assert.True(g.TestPlayEnded);
    }

    [Fact]
    public void EscapeQuitsTestPlay()
    {
        using var g = TestAssets.TestPlay(0);
        g.Advance(200);
        g.EscKey = true;
        g.Advance(100);
        Assert.True(g.TestPlayEnded);
    }

    [Fact]
    public void EveryOriginalCavernSurvivesRandomPlay()
    {
        for (int i = 0; i < TestAssets.Data.Levels.Count; i++)
        {
            using var g = TestAssets.TestPlay(i);
            var rng = new Random(i);
            for (int t = 0; t < 3000; t++)
            {
                if (t % 20 == 0) for (int k = 0; k < 10; k++) g.KeyMatrix[k] = (byte)(rng.Next(4) == 0 ? rng.Next(256) : 0);
                g.LatchKeys(); g.Tick();
            }
        }
    }

    [Fact]
    public void RenderScreenFillsTheWholeFrame()
    {
        using var g = TestAssets.TestPlay(0);
        g.Advance(100);
        var px = new uint[Cpc.Width * Cpc.Height];
        g.RenderScreen(px);
        Assert.All(px, p => Assert.Equal(0xFFu, p >> 24));
        Assert.True(px.Distinct().Count() > 2);
    }

    [Fact]
    public void PreviewDrawsACavernWithoutRunning()
    {
        using var g = new MinerGame(TestAssets.Data);
        var px = new uint[Cpc.Width * Cpc.Height];
        g.RenderPreview(TestAssets.Data.Levels[4].Clone(), px);
        Assert.False(g.Running);
        Assert.True(px.Distinct().Count() > 2);
    }

    [Fact]
    public void PreviewRefusesWhileRunning()
    {
        using var g = new MinerGame(TestAssets.Data);
        g.Start();
        Assert.Throws<InvalidOperationException>(() => g.RenderPreview(TestAssets.Data.Levels[0], new uint[Cpc.Width * Cpc.Height]));
    }

    [Fact]
    public void EditedCavernIsPlayable()
    {
        var l = TestAssets.Data.Levels[0].Clone();
        l.Layout[10][5] = 1;
        l.Guardians.Clear();
        using var g = new MinerGame(TestAssets.Data) { TestLevel = l };
        g.Start();
        Run(g, 1000, 0, 0x02);
        Assert.True(g.Running);
    }
}

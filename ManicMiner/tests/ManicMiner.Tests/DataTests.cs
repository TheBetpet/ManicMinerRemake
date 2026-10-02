using ManicMiner.Core;
using ManicMiner.Remake;
using Xunit;

namespace ManicMiner.Tests;

public class DataTests
{
    [Fact]
    public void Loads20OriginalCaverns()
    {
        Assert.Equal(20, TestAssets.Data.Levels.Count);
        Assert.All(TestAssets.Data.Levels, l => Assert.Equal(32, l.Name.Length));
    }

    [Fact]
    public void FirstCavernIsCentralCavern()
    {
        Assert.StartsWith("Central Cavern", TestAssets.Data.Levels[0].Name.Trim());
    }

    [Fact]
    public void EveryCavernLayoutIsSixteenBy32WithValidTiles()
    {
        foreach (var l in TestAssets.Data.Levels)
        {
            Assert.Equal(16, l.Layout.Length);
            Assert.All(l.Layout, row => { Assert.Equal(32, row.Length); Assert.All(row, c => Assert.InRange(c & 0x0F, 0, 10)); });
        }
    }

    [Fact]
    public void NoCavernExceedsItemAndGuardianLimits()
    {
        foreach (var l in TestAssets.Data.Levels)
        {
            int items = l.Layout.Sum(r => r.Count(c => (c & 0x0F) == 2));
            Assert.InRange(items, 1, 8);
            Assert.InRange(l.Guardians.Count, 0, 8);
        }
    }

    [Fact]
    public void BlankSpriteIsAllZero()
    {
        Assert.True(TestAssets.Data.Sprite(GameData.Blank).ToArray().All(b => b == 0));
    }

    [Fact]
    public void WillyFramesAreNotBlank()
    {
        for (int n = 1; n <= 8; n++)
            Assert.Contains(TestAssets.Data.Sprite(GameData.WillyFrame(n)).ToArray(), b => b != 0);
    }

    [Fact]
    public void FontHasEightBytesPerCharacter()
    {
        Assert.True(TestAssets.Data.Font.Length >= 256 * 8);
    }

    [Fact]
    public void LevelJsonRoundTrips()
    {
        var l = TestAssets.Data.Levels[5];
        var back = LevelData.FromJson(l.ToJson());
        Assert.Equal(l.ToJson(), back.ToJson());
        Assert.Equal(l.Special, back.Special);
    }

    [Fact]
    public void CloneIsIndependent()
    {
        var l = TestAssets.Data.Levels[0];
        int before = l.Layout[3][3];
        var c = l.Clone();
        c.Layout[3][3] = before + 1;
        c.Name = "changed".PadRight(32);
        Assert.Equal(before, l.Layout[3][3]);
        Assert.NotEqual(c.Name, l.Name);
    }

    [Fact]
    public void SpecialHandlersAreAssignedToSpecialCaverns()
    {
        Assert.Contains(TestAssets.Data.Levels, l => l.Special == SpecialHandler.Kong);
        Assert.Contains(TestAssets.Data.Levels, l => l.Special == SpecialHandler.SolarGenerator);
        Assert.Contains(TestAssets.Data.Levels, l => l.Special == SpecialHandler.Skylab);
    }
}

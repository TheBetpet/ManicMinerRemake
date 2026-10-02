using ManicMiner.Remake;
using Xunit;

namespace ManicMiner.Tests;

public class ScreenTests
{
    static Screen New() => new(TestAssets.Data.Font);

    [Fact]
    public void RowAddressesFollowCpcLayout()
    {
        Assert.Equal(0xC000, Screen.RowAddr(0));
        Assert.Equal(0xC800, Screen.RowAddr(1));
        Assert.Equal(0xC040, Screen.RowAddr(8));
        Assert.Equal(0xC000 + 15 * 64 + 7 * 0x800, Screen.RowAddr(127));
    }

    [Fact]
    public void SpriteRowsBeyondTheCavernSpillIntoTheHud()
    {
        // The original's row table continues at pixel row 152 after row 127.
        Assert.Equal(Screen.RowAddr(127), Screen.TableRow(127));
        Assert.Equal(Screen.RowAddr(152), Screen.TableRow(128));
        Assert.Equal(Screen.RowAddr(167), Screen.TableRow(143));
    }

    [Fact]
    public void XorTwiceRestoresTheScreen()
    {
        var s = New();
        var gfx = TestAssets.Data.Sprite(GameData.WillyFrame(1)).ToArray();
        s.Xor(gfx, 10, 40, 0xFF, false);
        Assert.Contains(s.Mem, b => b != 0);
        s.Xor(gfx, 10, 40, 0xFF, false);
        Assert.All(s.Mem, b => Assert.Equal(0, b));
    }

    [Fact]
    public void XorDetectsOverlapOnlyOnPenThreePixels()
    {
        var s = New();
        var gfx = new byte[64];
        gfx[0] = 0xFF;
        Assert.False(s.Xor(gfx, 4, 8, 0xFF, true));    // empty screen: nothing to hit
        Assert.True(s.Xor(gfx, 4, 8, 0xFF, true));     // same pixels now set (pen 3): collision
    }

    [Fact]
    public void XorOfBlankSpriteChangesNothing()
    {
        var s = New();
        s.Xor(TestAssets.Data.Sprite(GameData.Blank), 0, 0, 0xFF, true);
        Assert.All(s.Mem, b => Assert.Equal(0, b));
    }

    [Fact]
    public void TextDrawsGlyphsAndClearWipesThem()
    {
        var s = New();
        s.Text(new[] { (byte)'A' }, 0xC000, 0);
        Assert.Contains(s.Mem, b => b != 0);
        s.Clear();
        Assert.All(s.Mem, b => Assert.Equal(0, b));
    }

    [Fact]
    public void AddressesWrapInsideSixteenKilobytes()
    {
        var s = New();
        s[0xC000] = 0x5A;
        Assert.Equal(0x5A, s.Mem[0]);
    }

    [Fact]
    public void RenderProducesOpaquePixelsOfThePaletteColours()
    {
        var s = New();
        s[0xC000] = 0xFF;
        var argb = new uint[Screen.Width * Screen.Height];
        s.Render(argb, new[] { 0, 1, 2, 3 }, new[] { 0, 1, 2, 3 });
        Assert.All(argb, p => Assert.Equal(0xFFu, p >> 24));
    }
}

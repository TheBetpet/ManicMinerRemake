using ManicMiner.Remake;
using Xunit;

namespace ManicMiner.Tests;

public class SoundTests
{
    [Fact]
    public void BlockSerialisesToNineBytes()
    {
        var b = new SoundBlock { Status = 1, AmpEnv = 2, ToneEnv = 3, Period = 0x1234, Noise = 5, Vol = 6, Duration = 0x0708 };
        Assert.Equal(new byte[] { 1, 2, 3, 0x34, 0x12, 5, 6, 0x08, 0x07 }, b.ToBytes());
    }

    [Fact]
    public void BlocksAreReadFromTheOriginalData()
    {
        var sfx = new Sfx(TestAssets.Data);
        Assert.Equal(TestAssets.Data[0x6A3B], sfx.Jump.Status);
    }

    [Fact]
    public void JumpSoundIsAudibleWhenWillyJumps()
    {
        using var g = TestAssets.TestPlay(0);
        g.Sound.CaptureRate = 44100;
        g.Advance(200);
        TestAssets.Hold(g, 0, 0x01);                       // Up = jump
        var pcm = new short[44100 / 60];
        bool heard = false;
        for (int f = 0; f < 60 && !heard; f++)
        {
            g.LatchKeys();
            g.Advance(5);
            g.Sound.Render(pcm, pcm.Length, 44100);
            heard = pcm.Any(s => s != 0);
        }
        Assert.True(heard);
    }
}

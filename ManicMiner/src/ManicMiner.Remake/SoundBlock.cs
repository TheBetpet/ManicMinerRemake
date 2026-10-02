using ManicMiner.Core;

namespace ManicMiner.Remake;

/// <summary>A CPC firmware SOUND parameter block (channel status, envelopes, period, noise, volume, duration).</summary>
public sealed class SoundBlock
{
    public byte Status, AmpEnv, ToneEnv, Noise, Vol;
    public int Period, Duration;

    public static SoundBlock At(GameData d, int a) => new()
    {
        Status = d[a], AmpEnv = d[a + 1], ToneEnv = d[a + 2], Period = d.Word(a + 3),
        Noise = d[a + 5], Vol = d[a + 6], Duration = d.Word(a + 7)
    };

    public byte[] ToBytes() => new[]
    {
        Status, AmpEnv, ToneEnv, (byte)Period, (byte)(Period >> 8), Noise, Vol, (byte)Duration, (byte)(Duration >> 8)
    };

    public void PlayOn(CpcSound s) => s.Queue(ToBytes(), 0);
}

/// <summary>All the sound effects and music blocks of the game, loaded from the original data.</summary>
public sealed class Sfx
{
    public readonly SoundBlock Note, Kong, TitleA, TitleB, Skylab, Collect, Death, SolarHit, GameOver, Portal, Jump;

    public Sfx(GameData d)
    {
        Note = SoundBlock.At(d, 0x699D);       // in-game music note (period and duration patched per note)
        Kong = SoundBlock.At(d, 0x69A6);
        TitleA = SoundBlock.At(d, 0x69AF);     // title tune, two voices
        TitleB = SoundBlock.At(d, 0x69B8);
        Skylab = SoundBlock.At(d, 0x69C1);
        Collect = SoundBlock.At(d, 0x69CA);
        Death = SoundBlock.At(d, 0x69D3);
        SolarHit = SoundBlock.At(d, 0x69DC);
        GameOver = SoundBlock.At(d, 0x69E5);
        Portal = SoundBlock.At(d, 0x69EE);
        Jump = SoundBlock.At(d, 0x6A3B);       // period is rewritten every frame while Willy is airborne
    }

    /// <summary>Install the amplitude/tone envelopes the sound blocks refer to.</summary>
    public static void DefineEnvelopes(GameData d, CpcSound s)
    {
        byte[] Env(int a) => d.Span(a, 1 + d[a] * 3).ToArray();
        s.DefineAmpEnvelope(1, Env(0x6919), 0);
        s.DefineAmpEnvelope(2, Env(0x6920), 0);
        s.DefineAmpEnvelope(3, Env(0x6924), 0);
        s.DefineAmpEnvelope(4, Env(0x6928), 0);
        s.DefineToneEnvelope(1, Env(0x6930), 0);
        s.DefineToneEnvelope(2, Env(0x6934), 0);
        s.DefineToneEnvelope(3, Env(0x692C), 0);
    }
}

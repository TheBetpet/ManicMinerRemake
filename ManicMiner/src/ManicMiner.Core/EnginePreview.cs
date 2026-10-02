namespace ManicMiner.Core;

public sealed partial class Engine
{
    /// <summary>
    /// Render a static picture of a cavern (map, items, guardians, Willy, portal, name) without running
    /// the game. Used by the level editor. Must not be called while the engine thread is running.
    /// </summary>
    public void RenderPreview(LevelData level, uint[] argb)
    {
        if (_thread != null) throw new InvalidOperationException("Preview needs a non-started engine.");
        Levels = new List<LevelData> { level };
        ClearScreen();
        foreach (int v in new[] { 0x678E, 0x678F, 0x6709, 0x678D, 0x678B, 0x67AF, 0x67B4, 0x67B2, 0x67B5, 0x67B3, 0x67B1, 0x8288 }) M[v] = 0;
        M[0x6740] = 0; M[0x6796] = 0xFF; M[0x6786] = 2;
        Ww(0x67B6, 0x67B9);
        M[0x67AF] = 0;
        M[0x6782] = (byte)level.WillyX; M[0x6783] = (byte)level.WillyY;
        M[0x6785] = 1;
        Ww(0x6794, 0x69FA);
        LoadLevel();
        R7704();
        int xx = M[0x6782], yy = M[0x6783];
        int ix = 0x5200 + ((yy * 2) & 0xFF);
        int off = (((xx >> 2) & 0x1F) * 2);
        M[0x6A3A] = (byte)off;
        R7648(ix, 0x600 + 0x40 * ((xx & 3) + 1), 0xFF);
        M[0x67B1] = 0;
        ScreenOn = true;
        RenderScreen(argb);
    }
}

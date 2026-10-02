using System.Text;

namespace ManicMiner.Remake;

// Title screen, attract mode, HUD backdrop and the game-over sequence.
public sealed partial class MinerGame
{
    const int HudBarAddr = 0xC400, HudRows2 = 0xC480, HudRows3 = 0xC4C0;
    static readonly byte[] GameOverText = Encoding.ASCII.GetBytes("GAME      OVER");

    int _pianoA, _pianoB;           // 677E / 6780: video addresses of the lit piano keys
    int _dance = 1;                 // 6794 reused as the title Willy's animation counter
    int _cycleStep, _cycleIndex;    // 8177 / 8176: title colour cycling

    /// <summary>7DE8: fill the 8 pixel rows of one byte column, then step to the next column.</summary>
    void FillColumn(ref int addr, int value) { Screen.Column(addr, value); addr++; }

    /// <summary>7DDB: B pairs of white/grey columns (the piano keyboard).</summary>
    void PianoClear(int addr, int pairs = 1)
    {
        for (; pairs > 0; pairs--) { FillColumn(ref addr, 0xFF); FillColumn(ref addr, 0xEE); }
    }

    /// <summary>7DBF: the HUD backdrop.</summary>
    void DrawHudBackground()
    {
        _dance = 1;
        int hl = HudRows2, p = DataHudColours;
        for (int b = 0x40; b > 0;)
        {
            int a = Data[p];
            if (a == 0) { p = DataHudColours; continue; }
            FillColumn(ref hl, a); p++; b--;
        }
        PianoClear(hl, 0x20);
    }
    const int DataHudColours = GameData.HudColours;

    /// <summary>7E94: blank the display and clear the lower HUD rows.</summary>
    void BlankHud()
    {
        ScreenOn = false;
        int hl = HudRows2;
        for (int i = 0; i < 0x80; i++) FillColumn(ref hl, 0);
    }

    /// <summary>7FE3: draw a run-length-coded list of tiles.</summary>
    void DrawTileList(int p, int addr)
    {
        while (true)
        {
            int a = Data[p];
            if (a == 0xFF) return;
            int b = 1;
            if ((a & 0x80) == 0) { b = a; p++; }
            for (; b > 0; b--) { Screen.Tile(Data.TileGfx(Data[p] & 0x7F), addr); addr += 2; }
            p++;
        }
    }

    /// <summary>8002: the title screen.</summary>
    void DrawTitleScreen()
    {
        DrawTileList(GameData.TitleTiles, 0xC0E3);
        Screen.Text(Data.Span(0x8178, 5), 0xC331, 0);     // "press"
        Screen.Text(Data.Span(0x817D, 5), 0xCB71, 0);     // "ENTER"
        Screen.Text(Data.Span(0x8182, 2), 0xCBB4, 0);     // "to"
        Screen.Text(Data.Span(0x8184, 5), 0xCBF1, 0);     // "START"
        Screen.Text(Data.Span(GameData.ScrollText, 0x20), 0xCD40, 0);
        for (int i = 0; i < 12; i++)
        {
            int dst = Screen.RowAddr(4 + i);
            var strip = Data.Span(GameData.TitleStrip + i * 0x40, 0x40);
            for (int k = 0; k < 0x40; k++) Screen[dst + k] = strip[k];
        }
        for (int i = 0; i < 16; i++)      // the 4x4 block logo
            Screen.Block(Data.Sprite(Data.Word(0x8189 + 2 * i)), 0x2F + 4 * (i & 3), 32 + 16 * (i >> 2));
        for (int i = 0; i < 4; i++)       // the 2x2 block picture
            Screen.Block(Data.Sprite(Data.Word(0x81A9 + 2 * i)), 4 * (i & 1), 88 + 16 * (i >> 1));
        Screen.Block(Data.Sprite(Data.Word(0x81B1)), 0x0C, 104);
        _pal[2] = 0x12;
        _pal[1] = 0x0C;
    }

    /// <summary>7E86: piano key position for a note.</summary>
    static int PianoKey(int note)
    {
        int rr = ((note >> 2) | ((note & 3) << 6)) & 0x3E;
        int l = (0x40 - rr) | 0xC0;
        return HudBarAddr | (l & 0xFF);
    }

    /// <summary>7DF8: light the piano keys for a title-tune note and animate the dancing Willy.</summary>
    void ShowNote(int noteA, int noteB)
    {
        if (_pianoA != 0) { PianoClear(_pianoA); PianoClear(_pianoB); }
        _pianoA = PianoKey(noteA);
        int p = _pianoA;
        FillColumn(ref p, 0xF0); FillColumn(ref p, 0xF0);
        _pianoB = PianoKey(noteB);
        p = _pianoB;
        FillColumn(ref p, 0x0F); FillColumn(ref p, 0x0F);
        _dance = (_dance + 2) & 0xFF;
        Screen.Block(Data.Sprite(GameData.WillyFrame((_dance & 7) + 1)), 0x3A, 2);
    }

    /// <summary>7857: the title tune. Sets _flag when a key was pressed.</summary>
    IEnumerator<object?> TitleTune()
    {
        _tunePtr = GameData.TitleTune;
        while (true)
        {
            while ((Sound.Check(1) & 0x87) != 4) yield return null;
            int a = Data[_tunePtr];
            if (a == 0xFF) { _flag = false; yield break; }
            _sfx.TitleA.Duration = (_sfx.TitleA.Duration & 0xFF00) | (a >> 1);
            _sfx.TitleB.Duration = (_sfx.TitleB.Duration & 0xFF00) | (a >> 1);
            _sfx.TitleA.Period = Data[_tunePtr + 1] << 1;
            _sfx.TitleB.Period = Data[_tunePtr + 2] << 1;
            ShowNote(Data[_tunePtr + 1], Data[_tunePtr + 2]);
            _tunePtr += 3;
            Play(_sfx.TitleA);
            Play(_sfx.TitleB);
            ScanKeys();
            if (_kAny != 0) { _flag = true; yield break; }
        }
    }

    /// <summary>6F1B: the scrolling message. Sets _flag when a key started the game.</summary>
    IEnumerator<object?> Scroller()
    {
        for (int i = 0; i < 0x3A7; i++)
        {
            int ix = GameData.ScrollText + i;
            yield return WaitFlyback();
            Screen.Text(Data.Span(ix, 0x1F), 0xCD41, 0);
            CycleTitleColours();
            yield return WaitFlyback();
            Screen.Text(Data.Span(ix, 0x20), 0xCD40, 0);
            ScanKeys();
            if (_kAny != 0) { _flag = true; yield break; }
        }
        _flag = false;
    }

    /// <summary>7E53: title colour cycling.</summary>
    void CycleTitleColours()
    {
        _cycleStep = (_cycleStep + 1) & 0x0F;
        if (_cycleStep != 0) return;
        int a = _cycleIndex + 1;
        if (a == 0x1A) a = 0;
        _cycleIndex = a;
        int p = GameData.PaletteCycle + a;
        for (int i = 0; i < 3; i++)
        {
            int v = Data[p++];
            if (v == 0xFF) { p = GameData.PaletteCycle; v = Data[p++]; }
            _pal[i] = (byte)v;
        }
    }

    /// <summary>7EAC: fade-in used between demo caverns.</summary>
    IEnumerator<object?> FadeIn()
    {
        PixelMerge(0x0F);
        for (int b = 0; b < 15; b++)
        {
            int a = (_pal[1] + 1) & 0x1F;
            _pal[1] = (byte)a;
            _pal[3] = (byte)(a + 1);
            yield return Wait(9);
        }
    }

    /// <summary>80B9: collapse each pixel pair in the cavern area to a single pen (the "dissolve" effect).</summary>
    void PixelMerge(int mask)
    {
        int hl = 0xBC00;
        for (int round = 0; round < 8; round++)
        {
            hl += 0x400;
            for (int n = 0; n < 0x400; n++, hl++)
            {
                int t = Screen[hl];
                Screen[hl] = (byte)((((t >> 4) * 0x11) | ((t & 15) * 0x11)) & mask);
            }
        }
    }

    /// <summary>7EF3: the game-over sequence (Willy squashed by the Foot) and high-score update.</summary>
    IEnumerator<object?> GameOverSequence()
    {
        yield return WaitFlyback();
        ScreenOn = false;
        _pal[1] = _pal[2] = _pal[3] = 0x14;
        PixelMerge(0);
        Screen.Text(GameOverText, 0xC1D1, 0x0F);
        ScreenOn = true;
        Play(_sfx.GameOver);
        Screen.Block(Data.Sprite(GameData.FootA), 0x1E, 112);
        Screen.Block(Data.Sprite(GameData.FootB), 0x1E, 96);
        for (int i = 0, y = 0; i < 0x31; i++, y += 2)
        {
            Screen.Block(Data.Sprite(GameData.FootC), 0x1E, y);
            yield return Wait(13);
        }
        Sound.Reset();
        for (int n = 0; n < 256; n++)
        {
            _pal[2] = (byte)((_pal[2] + 1) & 0x1F);
            yield return Wait(2);
        }
        for (int i = 0; i < 6; i++)
        {
            if (_hiScore[i] == _score[i]) continue;
            if (_hiScore[i] < _score[i]) Array.Copy(_score, _hiScore, 6);
            break;
        }
        ResetScore();
    }

    void ResetScore() => Array.Fill(_score, (byte)'0');
}

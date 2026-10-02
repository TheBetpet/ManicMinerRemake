namespace ManicMiner.Core;

public sealed partial class Engine
{
    int _textHl;   // HL left by DrawText (quirk used at 7379)

    /// <summary>75CA: draw B characters from IX at screen address DE, XORing each glyph byte with C.</summary>
    void DrawText(int ix, int de, int b, int c)
    {
        var font = _assets.Font;
        for (; b > 0; b--)
        {
            int ch = M[ix & 0xFFFF]; ix++;
            int d = (de >> 8) & 0xFF, e = de & 0xFF;
            for (int row = 0; row < 8; row++)
            {
                int g = font[ch * 8 + row];
                int addr = (d << 8) | e;
                M[addr & 0xFFFF] = (byte)(((g >> 4) * 0x11) ^ c);
                M[(addr + 1) & 0xFFFF] = (byte)(((g & 15) * 0x11) ^ c);
                int sum = d + 8;
                d = sum & 0xFF;
                if (sum > 0xFF)
                {
                    d |= 0xC0;
                    int se = e + 0x40;
                    e = se & 0xFF;
                    if (se > 0xFF) d = (d + 1) & 0xFF;
                }
            }
            de += 2;
            _textHl = 0x3800 + ch * 8 + 8;
        }
    }

    /// <summary>760B: draw tile idx (8 rows x 2 bytes) at screen address de.</summary>
    void DrawTile(int idx, int de)
    {
        int src = 0x5386 + idx * 16;
        for (int r = 0; r < 8; r++)
        {
            M[de & 0xFFFF] = M[src]; M[(de + 1) & 0xFFFF] = M[src + 1];
            src += 2; de += 0x800;
        }
    }

    /// <summary>7648: XOR a 16x4-byte sprite onto the screen. Returns the advanced IX.</summary>
    int R7648(int ix, int de, int c)
    {
        for (int r = 0; r < 16; r++)
        {
            int hl = (M[(ix + 1) & 0xFFFF] << 8) | (M[0x6A3A] | M[ix & 0xFFFF]);
            ix += 2;
            for (int k = 0; k < 4; k++)
            {
                M[hl & 0xFFFF] = (byte)((M[de & 0xFFFF] & c) ^ M[hl & 0xFFFF]);
                hl++; de++;
            }
        }
        return ix;
    }

    /// <summary>7682: as 7648 but flags collisions in 0x67B1. Returns the advanced IX.</summary>
    int R7682(int ix, int de, int c)
    {
        M[0x67B1] = 0;
        for (int r = 0; r < 16; r++)
        {
            int hl = (M[(ix + 1) & 0xFFFF] << 8) | (M[0x6A3A] | M[ix & 0xFFFF]);
            ix += 2;
            for (int k = 0; k < 4; k++)
            {
                int s = M[de & 0xFFFF];
                if ((s & M[hl & 0xFFFF]) != 0) R7672(hl);
                M[hl & 0xFFFF] = (byte)((s & c) ^ M[hl & 0xFFFF]);
                hl++; de++;
            }
        }
        return ix;
    }

    void R7672(int hl)
    {
        int s = M[hl & 0xFFFF], hi = s >> 4, lo = s & 15;
        M[0x677E] = (byte)(lo * 0x11);
        int a = (hi * 0x11) & (lo * 0x11);
        if (a != 0) M[0x67B1] = (byte)a;
    }

    /// <summary>76C7: scan the keyboard (5 mask blocks at 0x6747, results at 0x6751, 0x675C, 0x6767, 0x6772, 0x677D).</summary>
    void R76C7()
    {
        int hl = 0x6747;
        for (int blk = 0; blk < 5; blk++)
        {
            int c = 0;
            for (int k = 0; k < 10; k++) c |= (KeyMatrix[k] | KeyLatch[k]) & M[hl + k];
            M[hl + 10] = (byte)c;
            hl += 11;
        }
        Array.Clear(KeyLatch);
    }

    /// <summary>76E7: look up the map cell under Willy for pixel row y. Returns the cell type; hl receives its address.</summary>
    int Cell(int y, out int hl)
    {
        int idx = 0x5200 + ((2 * y) & 0xFF);
        int e = M[idx] | (((M[0x6782] >> 2) & 0x1F) * 2);
        int h = M[idx + 1] & 0x43;
        hl = ((h << 8) | e) + 1;
        return M[hl & 0xFFFF];
    }

    /// <summary>7DE8: fill 8 screen lines at hl with c; hl advances one byte.</summary>
    void R7DE8(ref int hl, int c)
    {
        int a = hl;
        for (int i = 0; i < 8; i++) { M[a & 0xFFFF] = (byte)c; a += 0x800; }
        hl++;
    }

    /// <summary>7DDB: B pairs of FF/EE columns.</summary>
    void R7DDB(int hl, int b)
    {
        for (; b > 0; b--) { R7DE8(ref hl, 0xFF); R7DE8(ref hl, 0xEE); }
    }

    /// <summary>7DBF: HUD background fill.</summary>
    void R7DBF()
    {
        M[0x6791] = 0; M[0x6794] = 1;
        int hl = 0xC480, de = 0x6731;
        for (int b = 0x40; b > 0;)
        {
            int a = M[de];
            if (a == 0) { de = 0x6731; continue; }
            R7DE8(ref hl, a); de++; b--;
        }
        R7DDB(hl, 0x20);
    }

    /// <summary>7DF8: the title "piano" display and dancing Willy. hl points just after the note record.</summary>
    void R7DF8(int hl)
    {
        hl -= 2;
        if (M[0x677F] != 0)
        {
            R7DDB(Rw(0x677E), 1);
            R7DDB(Rw(0x6780), 1);
        }
        int p = R7E86(M[hl]);
        Ww(0x677E, p);
        R7DE8(ref p, 0xF0); R7DE8(ref p, 0xF0);
        p = R7E86(M[hl + 1]);
        Ww(0x6780, p);
        R7DE8(ref p, 0x0F); R7DE8(ref p, 0x0F);
        M[0x6794] += 2;
        int b = (M[0x6794] & 7) + 1;
        R7FA7(0x600 + 0x40 * b, 0x5204, 0x3A);
    }

    int R7E86(int a)
    {
        int rr = ((a >> 2) | ((a & 3) << 6)) & 0x3E;
        int l = (0x40 - rr) | 0xC0;
        return 0xC400 | (l & 0xFF);
    }

    /// <summary>7E53: title colour cycling.</summary>
    void R7E53()
    {
        int a = (M[0x8177] + 1) & 0x0F;
        M[0x8177] = (byte)a;
        if (a != 0) return;
        a = M[0x8176] + 1;
        if (a == 0x1A) a = 0;
        M[0x8176] = (byte)a;
        int de = 0x815B + a;
        for (int i = 0; i < 3; i++)
        {
            int v = M[de++];
            if (v == 0xFF) { de = 0x815B; v = M[de++]; }
            M[0x670D + i] = (byte)v;
        }
    }

    /// <summary>7E94: blank the display and clear the HUD rows.</summary>
    void R7E94()
    {
        ScreenOn = false;
        int hl = 0xC480;
        for (int i = 0; i < 0x80; i++) R7DE8(ref hl, 0);
    }

    /// <summary>7EAC: fade-in effect used between demo levels.</summary>
    void R7EAC()
    {
        R80B9(0x0F);
        for (int b = 0; b < 15; b++)
        {
            int a = (M[0x670E] + 1) & 0x1F;
            M[0x670E] = (byte)a;
            M[0x6710] = (byte)(a + 1);
            Wait(9);
        }
    }

    /// <summary>7ECA: draw/erase the portal.</summary>
    void R7ECA(int c)
    {
        M[0x6A3A] = (byte)(M[0x6743] >> 1);
        int ix = 0x5200 | M[0x6744];
        int pg = Cur.PortalGfx < 0 ? M[0x6740] % 20 : Cur.PortalGfx;
        int e = (pg == 1 || pg == 9) ? 0xF0 : 0xFF;
        R7648(ix, Rw(0x6741), e ^ c);
    }

    /// <summary>80B9: pixel-merge effect over the cavern area.</summary>
    void R80B9(int c)
    {
        int hl = 0xBC00;
        for (int round = 0; round < 8; round++)
        {
            hl += 0x400;
            for (int n = 0; n < 0x400; n++)
            {
                int t = M[hl & 0xFFFF];
                M[hl & 0xFFFF] = (byte)((((t >> 4) * 0x11) | ((t & 15) * 0x11)) & c);
                hl++;
            }
        }
    }

    /// <summary>7FA7: copy a 16x4 byte block from de to the screen lines at ix, offset c.</summary>
    void R7FA7(int de, int ix, int c)
    {
        for (int r = 0; r < 16; r++)
        {
            int hl = (M[ix + 1] << 8) | (M[ix] | c);
            for (int k = 0; k < 4; k++) M[(hl + k) & 0xFFFF] = M[(de++) & 0xFFFF];
            ix += 2;
        }
    }

    /// <summary>7FE3: draw a run-length tile list at screen address de.</summary>
    void R7FE3(int hl, int de)
    {
        while (true)
        {
            int a = M[hl];
            if (a == 0xFF) return;
            int b = 1;
            if ((a & 0x80) == 0) { b = a; hl++; }
            for (; b > 0; b--) { DrawTile(M[hl] & 0x7F, de); de += 2; }
            hl++;
        }
    }

    /// <summary>8094: draw a list of 16x4-byte blocks.</summary>
    void R8094(int ix, int c, int e, int hl, int b)
    {
        int d = c;
        while (true)
        {
            int de = M[hl] | (M[hl + 1] << 8);
            hl += 2;
            R7FA7(de, ix, c);
            c += 4;
            b--;
            if (b == 0) return;
            if ((e & b) != 0) continue;
            c = d;
            ix += 0x20;
        }
    }

    /// <summary>8002: draw the title screen.</summary>
    void R8002()
    {
        R7FE3(0x80D1, 0xC0E3);
        DrawText(0x8178, 0xC331, 5, 0);
        DrawText(0x817D, 0xCB71, 5, 0);
        DrawText(0x8182, 0xCBB4, 2, 0);
        DrawText(0x8184, 0xCBF1, 5, 0);
        DrawText(0x6A78, 0xCD40, 0x20, 0);
        int src = 0x2800;
        for (int i = 0; i < 12; i++)
        {
            int dst = Rw(0x5208 + 2 * i);
            for (int k = 0; k < 0x40; k++) M[(dst + k) & 0xFFFF] = M[src++];
        }
        R8094(0x5240, 0x2F, 3, 0x8189, 0x10);
        R8094(0x52B0, 0x00, 1, 0x81A9, 4);
        R7FA7(Rw(0x81B1), 0x52D0, 0x0C);
        M[0x670F] = 0x12;
        M[0x670E] = 0x0C;
    }

    /// <summary>741C: switch the display on.</summary>
    void R741C() { ScreenOn = true; }

    /// <summary>7F9C: zero the score.</summary>
    void R7F9C() { for (int i = 0; i < 6; i++) M[0x67E7 + i] = 0x30; }

    /// <summary>7EF3: game over sequence.</summary>
    void R7EF3()
    {
        WaitFlyback();
        ScreenOn = false;
        M[0x670E] = M[0x670F] = M[0x6710] = 0x14;
        R80B9(0);
        DrawText(0x67ED, 0xC1D1, 0x0E, 0x0F);
        R741C();
        SoundQueue(0x69E5);
        R7FA7(0x27C0, 0x52E0, 0x1E);
        R7FA7(0x06C0, 0x52C0, 0x1E);
        int ix = 0x5200;
        for (int b = 0x31; b > 0; b--)
        {
            R7FA7(0x0C80, ix, 0x1E);
            Wait(13);
            M[0x670C]++;
            ix += 4;
        }
        SoundReset();
        for (int n = 0; n < 256; n++)
        {
            M[0x670F] = (byte)((M[0x670F] + 1) & 0x1F);
            Wait(2);
        }
        for (int i = 0; i < 6; i++)
        {
            int hi = M[0x67D8 + i], sc = M[0x67E7 + i];
            if (hi == sc) continue;
            if (hi < sc) for (int k = 0; k < 6; k++) M[0x67D8 + k] = M[0x67E7 + k];
            break;
        }
        R7F9C();
    }
}

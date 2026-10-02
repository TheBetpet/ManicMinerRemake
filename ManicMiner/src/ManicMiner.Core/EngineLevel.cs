namespace ManicMiner.Core;

public sealed partial class Engine
{
    static int Sra(int v) => ((sbyte)v) >> 1;

    /// <summary>7440: build the cavern from the LevelData and draw it.</summary>
    void LoadLevel()
    {
        var L = Cur;
        for (int i = 0; i < 4; i++) M[0x670D + i] = 0x0B;
        for (int r = 0; r < 16; r++)
            for (int c = 0; c < 32; c++)
            {
                int a = 0x4000 + (r * 32 + c) * 2;
                M[a] = M[a + 1] = (byte)L.Layout[r][c];
            }
        int hl = 0x4000, de = 0xC000;
        for (int n = 0; n < 0x200; n++)
        {
            int a = M[hl];
            if (a == 7) R77DD(de);
            if (a == 8) R77D9(de);
            if (a == 2) { R79B0(de); a = 0; }
            DrawTile(L.TileGfx[a & 0xF], de);
            de += 2; hl += 2;
        }
        Ww(0x67B6, 0x5386 + L.TileGfx[2] * 16);
        M[0x670B] = (byte)L.Border;
        M[0x6711] = M[0x670C] = (byte)L.Aux;
        M[0x670E] = (byte)L.Pen2; M[0x670F] = (byte)L.Pen1; M[0x6710] = (byte)L.Pen0;
        SetBorder(L.Border);
        R7FE3(0x6718, 0xC400);
        var name = L.Name.PadRight(32).Substring(0, 32);
        for (int i = 0; i < 32; i++) M[0x9000 + i] = (byte)name[i];
        DrawText(0x9000, 0xE400, 0x20, 0xFF);

        Array.Clear(M, 0x8200, 0x88);
        int cnt = Math.Min(L.Guardians.Count, 8);
        M[0x678C] = (byte)cnt;
        for (int i = 0; i < cnt; i++)
        {
            var g = L.Guardians[i];
            int iy = 0x8200 + i * 0x11;
            int f = g.Flags;
            M[iy + 8] = (byte)f;
            M[iy] = M[iy + 1] = (byte)((f >> 4) & 3);
            M[iy + 16] = M[0x6798 + ((f >> 2) & 3)];
            M[iy + 2] = (byte)g.Steps;
            M[iy + 3] = (byte)g.Reload;
            if ((f & 0x80) != 0) M[iy + 4] = (byte)g.Velocity; else M[iy + 6] = (byte)g.Velocity;
            M[iy + 5] = (byte)g.X;
            M[iy + 7] = (byte)g.Y;
            Ww(iy + 9, Rw(0x5348 + 2 * g.SpriteSet));
            Ww(iy + 11, 0x69FA);
        }
        M[0x6743] = (byte)L.PortalX;
        M[0x6744] = (byte)L.PortalY;
        Ww(0x6741, 0x2B00 + PortalGfx() * 0x40);
        R7ECA(0);
        DrawText(0x67CD, 0xCD40, 0x20, 0);
    }

    int PortalGfx() => Cur.PortalGfx < 0 ? M[0x6740] % 20 : Cur.PortalGfx;

    void R77D9(int de) => RegConveyor(de, 0);
    void R77DD(int de) => RegConveyor(de, 1);
    void RegConveyor(int de, int dir)
    {
        M[0x67B0] = (byte)dir;
        int a = M[0x67AF];
        if (a == 0) Ww(0x67AD, de);
        M[0x67AF] = (byte)(a + 1);
    }

    /// <summary>77F3: animate conveyors.</summary>
    void R77F3()
    {
        if (M[0x67AF] != 0)
        {
            bool z = M[0x67B0] == 0;
            Conv(Rw(0x67AD), z ? 0x679C : 0x67A4, 0);
            Conv(Rw(0x67AD), z ? 0x67A4 : 0x679C, 0x1000);
        }
        M[0x67AC]++;
    }

    void Conv(int hl, int ix, int de)
    {
        hl += de;
        ix += (M[0x67AC] * 2) & 6;
        for (int b = M[0x67AF]; b > 0; b--)
        {
            M[hl & 0xFFFF] = (byte)(M[ix] & 0xF0); hl++;
            M[hl & 0xFFFF] = (byte)(M[ix + 1] & 0xF0); hl++;
        }
    }

    /// <summary>79B0: register a collectable item at screen address de.</summary>
    void R79B0(int de)
    {
        int a = M[0x67B4] + 1;
        if (a == 9) return;
        M[0x67B4] = (byte)a;
        M[0x67B5] = (byte)((M[0x67B5] >> 1) | 0x80);
        int hl = Rw(0x67B6);
        Ww(hl, de);
        Ww(0x67B6, hl + 2);
    }

    /// <summary>79D0: flash items.</summary>
    void R79D0()
    {
        R79DA();
        int a = M[0x67B8];
        M[0x67B8] = (byte)((a >> 1) | ((a & 1) << 7));
        R79DA();
    }

    void R79DA()
    {
        int c = M[0x67B8];
        int n = M[0x67B4];
        if (n == 0) return;
        int hl = 0x67B9, a = M[0x67B5];
        for (int b = n; b > 0; b--)
        {
            int de = Rw(hl); hl += 2;
            R79EE(de, a, c);
            a = ((a << 1) | (a >> 7)) & 0xFF;
        }
    }

    /// <summary>79EE: draw one item if the rotated mask's carry is set (a is rotated by the caller's loop).</summary>
    void R79EE(int screen, int mask, int c)
    {
        if ((mask & 0x80) != 0) R7A01(screen, Rw(0x67B6), c);
    }

    void R7A01(int hl, int de, int c)
    {
        for (int r = 0; r < 8; r++)
        {
            M[hl & 0xFFFF] = (byte)((M[de] & c) ^ M[hl & 0xFFFF]);
            M[(hl + 1) & 0xFFFF] = (byte)((M[de + 1] & c) ^ M[(hl + 1) & 0xFFFF]);
            de += 2; hl += 0x800;
        }
    }

    /// <summary>7A16: collect the item at map cell hl.</summary>
    void R7A16(int hl)
    {
        hl |= 1;
        M[hl & 0xFFFF] = 0;
        hl--;
        M[hl & 0xFFFF] = 0;
        int scr = (hl + 0x8000) & 0xFFFF;
        int a = M[0x67B5];
        int p = 0x67B9;
        for (int b = 8; b > 0; b--)
        {
            int ent = Rw(p); p += 2;
            if (ent == scr)
            {
                a &= 0x7F;
                for (int k = 0; k < b; k++) a = ((a << 1) | (a >> 7)) & 0xFF;
                M[0x67B5] = (byte)a;
                R7A01(scr, Rw(0x67B6), M[0x67B8]);
                R7A66(0x67EA, 1);
                SoundQueue(0x69CA);
                return;
            }
            a = ((a << 1) | (a >> 7)) & 0xFF;
        }
    }

    /// <summary>7A66: add c to the ASCII score at hl and redraw it.</summary>
    void R7A66(int hl, int c)
    {
        int a;
        while (true)
        {
            a = M[hl] + c;
            if (a < 0x3A) break;
            a -= 10;
            M[hl] = (byte)a;
            c = 1;
            hl--;
        }
        M[hl] = (byte)a;
        if (hl == 0x67E8) M[0x6709] = (byte)a;
        DrawText(0x67E7, 0xCD74, 6, 0);
    }

    /// <summary>7A8E: a crumbling floor cell is stepped.</summary>
    void R7A8E(int hl)
    {
        hl |= 1;
        int a = (M[hl] + 0x10) & 0xFF;
        M[hl] = (byte)a;
        hl--;
        M[hl] = (byte)a;
        if ((a & 0xF0) != 0x90) FloorBit(2);
        else { M[hl] = 0; M[hl + 1] = 0; }
        R7AAF(hl);
    }

    /// <summary>7AAF: shift the cell's 8 pixel rows down by one and clear the top row.</summary>
    void R7AAF(int hl)
    {
        int basea = (hl + 0x8000) & 0xFFFF;
        for (int line = 6; line >= 0; line--)
        {
            int src = basea + line * 0x800, dst = src + 0x800;
            M[dst & 0xFFFF] = M[src & 0xFFFF];
            M[(dst + 1) & 0xFFFF] = M[(src + 1) & 0xFFFF];
        }
        M[basea & 0xFFFF] = 0;
        M[(basea + 1) & 0xFFFF] = 0;
    }

    void FloorBit(int bit) => M[0x678E] |= (byte)bit;

    /// <summary>7978: consume air. Returns true (carry) when the air has run out.</summary>
    bool R7978()
    {
        int a = M[0x6A44] - 1;
        if (a == 0) return true;
        M[0x6A44] = (byte)a;
        int e = a, b = a & 3, c;
        if (b == 0) c = 0;
        else
        {
            int v = 0x0F;
            for (int i = 0; i < b; i++) v = ((v >> 1) | ((v & 1) << 7)) & 0xFF;
            c = v & 0xF0;
        }
        c |= (c >> 4) | ((c & 15) << 4);
        c &= 0xFF;
        int hl = 0xCC88 + (e >> 2);
        c |= M[hl];
        for (int i = 0; i < 4; i++) { hl += 0x800; M[hl] = (byte)c; }
        return false;
    }

    bool R7972() => (M[0x67AC] & 7) != 0 ? false : R7978();

    /// <summary>78E3: draw the remaining lives.</summary>
    void R78E3()
    {
        int hl = 0x640 + ((M[0x67AC] & 0x0C) << 4);
        int a = M[0x6786];
        if (a == 0) { hl = 0x69FA; a = 1; }
        Ww(0x6787, hl);
        int ix = 0x5300;
        Ww(0x6789, ((a - 1) * 4) & 0xFF);
        for (int b = 16; b > 0; b--)
        {
            int de = Rw(ix);
            hl = Rw(0x6787);
            for (int i = 0; i < 4; i++) M[(de++) & 0xFFFF] = M[(hl++) & 0xFFFF];
            Ww(0x6787, hl);
            int n = Rw(0x6789);
            if (n != 0)
            {
                int s = Rw(ix);
                for (int i = 0; i < n; i++) M[(de++) & 0xFFFF] = M[(s++) & 0xFFFF];
            }
            for (int i = 0; i < 4; i++) M[(de++) & 0xFFFF] = 0;
            ix += 2;
        }
    }

    void R7850() => Ww(0x69F7, 0x6938);

    /// <summary>7857: the title tune. Returns true (carry) when a key was pressed.</summary>
    bool R7857()
    {
        Ww(0x69F7, 0x67FB);
        while (true)
        {
            while ((SoundCheck(1) & 0x87) != 4) WaitTick();
            int hl = Rw(0x69F7);
            int a = M[hl];
            if (a == 0xFF) return false;
            M[0x69B6] = M[0x69BF] = (byte)(a >> 1);
            Ww(0x69B2, M[hl + 1] << 1);
            Ww(0x69BB, M[hl + 2] << 1);
            hl += 3;
            Ww(0x69F7, hl);
            R7DF8(hl);
            SoundQueue(0x69AF);
            SoundQueue(0x69B8);
            R76C7();
            if (M[0x6772] != 0) return true;
        }
    }

    /// <summary>78AD: in-game background music.</summary>
    void R78AD()
    {
        if (M[0x6796] != 0) return;
        if ((SoundCheck(1) & 0x87) != 4) return;
        int hl = Rw(0x69F7);
        int a = M[hl];
        if (a == 0xFF) { hl = 0x6938; a = M[hl]; }
        M[0x69A0] = (byte)(a << 1);
        M[0x69A1] = (byte)(a >> 7);
        hl++;
        M[0x69A4] = M[hl];
        hl++;
        Ww(0x69F7, hl);
        SoundQueue(0x699D);
    }

    /// <summary>7704: move and draw the guardians. Returns true if Willy was hit.</summary>
    bool R7704()
    {
        int iy = 0x8200;
        for (int b = M[0x678C]; b > 0; b--, iy += 0x11)
        {
            int d, e;
            if (--M[iy] != 0)
            {
                d = M[iy + 5]; e = M[iy + 7];
            }
            else
            {
                M[iy] = M[iy + 1];
                if (--M[iy + 2] == 0)
                {
                    M[iy + 2] = M[iy + 3];
                    M[iy + 4] = (byte)(-M[iy + 4]);
                    M[iy + 6] = (byte)(-M[iy + 6]);
                }
                M[iy + 5] += M[iy + 4]; d = M[iy + 5];
                M[iy + 7] += M[iy + 6]; e = M[iy + 7];
            }
            int flags = M[iy + 8];
            int a = (flags & 0x80) != 0 ? d : M[iy + 2];
            a &= flags & 3;
            int cc = (a << 6) & 0xFF;
            int dir = (M[iy + 4] >> 7) & 1;
            if ((flags & 0x40) == 0) dir = 0;
            int ixl = (e << 1) & 0xFF;
            int off = ((d >> 1) | ((d & 1) << 7)) & 0x3E;
            M[0x6745] = (byte)off;
            int frame = (Rw(iy + 9) + ((dir << 8) | cc)) & 0xFFFF;
            int ixNew = 0x5200 | ixl;
            int c = M[iy + 16];
            M[0x6A3A] = M[iy + 15];
            R7648(Rw(iy + 13), Rw(iy + 11), c);
            Ww(iy + 11, frame);
            Ww(iy + 13, ixNew);
            M[0x6A3A] = (byte)off;
            M[iy + 15] = (byte)off;
            R7682(ixNew, frame, c);
            if (M[0x67B1] != 0) return true;
            if ((b & 1) != 0) R78AD();
        }
        return false;
    }
}

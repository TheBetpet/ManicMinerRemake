namespace ManicMiner.Core;

public sealed partial class Engine
{
    /// <summary>
    /// 6FB2..7416: play one cavern. Returns 0 = Willy died / demo timed out, 1 = returned with carry
    /// (demo key press, or air ran out), 2 = cavern completed (level already advanced), 3 = ESC to title.
    /// </summary>
    int Level()
    {
        int a, b, c, hl;
        long frameStart;

        R7850();
        var L = Cur;
        M[0x6782] = (byte)L.WillyX;
        M[0x6783] = (byte)L.WillyY;
        if (M[0x6709] != 0 && M[0x6786] != 7) M[0x6786]++;
        Ww(0x67B6, 0x67B9);
        foreach (int v in new[] { 0x678E, 0x678F, 0x6709, 0x678D, 0x678B, 0x67AF, 0x67B4, 0x67B2, 0x67B5, 0x67B3, 0x8288 })
            M[v] = 0;
        M[0x6785] = 1;
        Ww(0x6794, 0x69FA);
        M[0x6A44] = (byte)L.Air;
        LevelStarted?.Invoke(M[0x6740]);
        LoadLevel();
        R78E3();
        R79DA();
        R77F3();
        R741C();

    frame:
        frameStart = Time;
        R76C7();
        if (M[0x6708] != 0)
        {
            a = M[0x6708] - 1;
            if (a == 0) return 0;
            M[0x6708] = (byte)a;
            if (M[0x6772] != 0) return 1;
            goto L730B;
        }

        // 7042: start of a jump
        if (M[0x678D] == 0 && M[0x678E] != 0 && M[0x6767] != 0)
        {
            a = Cell((M[0x6783] + 0x10) & 0xFF, out hl);
            b = 2;
            while (true)
            {
                hl++;
                if (a == 7 || a == 8) goto L7081;
                a = M[hl];
                if (--b == 0) break;
            }
            // The original only tests the right-hand cell here (7066-706C) and refuses to jump when it is empty,
            // even though Willy is standing on the left-hand one. M[0x678E] (floor found) already proves support.
            M[0x6784] = 0;
            if (M[0x675C] != 0) M[0x6784]++;
            if (M[0x6751] != 0) M[0x6784]--;
        L7081:
            M[0x6A3E] = 0x5A;
            M[0x678B] = 0xF8;
            M[0x678D] = 0xF8;
        }

    L708E:
        b = Sra(M[0x678B]);
        c = M[0x6783];
        M[0x6783] = (byte)(c + b);
        if ((c & 7) != 0) goto L7158;
        if ((M[0x678B] & 0x80) != 0) goto L7158;
        b = 2;
        M[0x678E] = 0;
        a = Cell((M[0x6783] + 0x10) & 0xFF, out hl);
        while (true)
        {
            a &= 0x0F;
            switch (a)
            {
                case 1: case 5: case 9: FloorBit(2); break;
                case 3: case 4: return 0;
                case 6: R7A8E(hl); break;
                case 7: FloorBit(4); break;
                case 8: FloorBit(8); break;
            }
            hl += 2;
            a = M[hl];
            if (--b == 0) break;
        }
        {
            int keyA = 0x675C, keyB = 0x6751;
            int bb = 0xFF;
            int f = M[0x678E];
            if ((f & 4) != 0) { (keyA, keyB) = (keyB, keyA); bb = 1; goto L7115; }
            if ((f & 8) != 0) goto L7115;
            if ((f & 2) != 0) goto L712E;
            if (M[0x678B] != 9) goto L7158;
            M[0x6784] = 0;
            M[0x678F] += 8;
            goto L7158;

        L7115:
            a = M[0x6784];
            if (a == 0) { M[keyA] = 1; goto L712E; }
            if (a == bb)
            {
                a = M[keyB] | M[keyA];
                if (a != 0)
                {
                    a = M[keyA];
                    if (a == 0) goto L712E;
                }
            }
            M[keyA] = 0xFF;
            M[keyB] = 0;
        }

    L712E:
        b = M[0x678D] != 0 ? 0x14 : 0;
        if (((M[0x678F] + b) & 0xFF) >= 0x28) return 0;
        M[0x6783] = (byte)(M[0x6783] - Sra(M[0x678B]));
        M[0x678D] = 0;
        M[0x678F] = 0;
        M[0x678B] = 9;

    L7158:
        if (M[0x678D] == 0) goto L71B5;
        if ((M[0x678B] & 0x80) == 0) goto L71B5;
        c = (M[0x6783] - Sra(M[0x678B])) & 0xFF;
        if ((c & 7) != 0) goto L71B5;
        b = 2;
        a = Cell(M[0x6783], out hl);
        while (true)
        {
            a &= 0x0F;
            if (a == 1)
            {
                M[0x6783] = (byte)(M[0x6783] - Sra(M[0x678B]));
                M[0x678B] = 7;
                break;
            }
            if (a == 3 || a == 4) return 0;
            if (a == 2) { R7A16(hl); a = 0; }
            if (a == 0x0A) M[0x67B2] |= 1;
            hl += 2;
            a = M[hl];
            if (--b == 0) break;
        }

    L71B5:
        if (M[0x678D] == 0 && M[0x678E] != 0)
        {
            M[0x6784] = 0;
            if (M[0x675C] != 0) M[0x6784]++;
            if (M[0x6751] != 0) M[0x6784]--;
        }
        M[0x6782] += M[0x6784];
        b = 2;
        if ((M[0x6783] & 7) != 0) b++;
        a = Cell(M[0x6783], out hl);
        while (true)
        {
            a &= 0x0F;
            if (a == 1) goto L722F;
            if (a == 2) { R7A16(hl); a = 0; }
            if (a == 3 || a == 4) return 0;
            if (a == 0x0A) M[0x67B2] |= 1;
            hl += 2;
            a = M[hl];
            if (a == 1) goto L722F;
            if (a == 2) { R7A16(hl); a = 0; }
            if (a == 3 || a == 4) return 0;
            if (a == 0x0A) M[0x67B2] |= 1;
            hl += 0x3E;
            a = M[hl];
            if (--b == 0) break;
        }
        goto L7239;
    L722F:
        M[0x6782] -= M[0x6784];

    L7239:
        a = M[0x678B] + 1;
        if (a != 0x0A) M[0x678B] = (byte)a;
        a = M[0x6783] & 0x7F;
        if (a >= 0x6E) a = 0x50;
        M[0x6783] = (byte)a;

        if (M[0x678E] == 0 || M[0x678D] != 0)
        {
            if ((SoundCheck(2) & 0x80) == 0) SoundQueue(0x6A3B);
            int d = M[0x678F];
            a = M[0x678B];
            if ((a & 0x80) != 0) a = (-a) & 0xFF;
            a = ((a & 0x0F) * 2 + 0x32 + d) & 0xFF;
            M[0x6A3E] = (byte)a;
        }
        if (M[0x6784] != 0) M[0x6785] = M[0x6784];
        {
            int face = M[0x6785];
            int bk = face == 0xFF ? 1 : 0;
            int xx = M[0x6782], yy = M[0x6783];
            int ixNew = 0x5200 + ((yy * 2) & 0xFF);
            int off = ((((xx + bk) & 0xFF) >> 2) & 0x1F) * 2;
            int fn = ((xx + bk) & 3) + 1 + (face == 0xFF ? 4 : 0);
            int frameNew = 0x600 + 0x40 * fn;
            int frameOld = Rw(0x6794);
            Ww(0x6794, frameNew);
            int ixOld = Rw(0x6790);
            M[0x6A3A] = M[0x6792];
            M[0x6792] = (byte)off;
            Ww(0x6790, ixNew);
            R7648(ixOld, frameOld, 0xFF);
            M[0x6A3A] = (byte)off;
            R7648(ixNew, frameNew, 0xFF);
        }

    L730B:
        R77F3();
        if (R7972()) return 1;
        R79D0();
        if (R7704()) return 0;
        Special();
        R78E3();
        if (M[0x67B5] == 0)
        {
            R7ECA((M[0x67AC] & 1) != 0 ? 0x0F : 0xF0);
            if ((M[0x6743] & 0xFC) == (M[0x6782] & 0xFC))
            {
                a = (((M[0x6783] * 2) & 0xFF) - M[0x6744] + 8) & 0xFF;
                if (a < 0x20)
                {
                    SoundQueue(0x69EE);
                    R80B9(0x0F);
                    bool out_;
                    do
                    {
                        out_ = R7978();
                        R7A66(0x67EC, 4);
                        Wait(5);
                    } while (!out_);
                    SoundReset();
                    if (TestLevel != null) return 2;
                    M[0x6740]++;
                    if (M[0x6740] == Levels.Count) M[0x6740] = 0;
                    return 2;
                }
            }
        }

        while (Time - frameStart < 25) WaitTick();
        if (M[0x6708] != 0) goto frame;

        a = M[0x6772];
        c = M[0x6797];
        M[0x6797] = (byte)a;
        if (a != 0 && a != c)
        {
            M[0x6796] = (byte)~M[0x6796];
            DrawText(M[0x6796] != 0 ? 0x6717 : 0x6716, 0xECF8, 1, 0);
        }
        if (M[0x677D] != 0)
        {
            Wait(16);
            do
            {
                R76C7();
                WaitTick();
            } while (M[0x6751] == 0 && M[0x675C] == 0 && M[0x6772] == 0);
        }
        if (EscKey)
        {
            R7F9C();
            return 3;
        }
        goto frame;
    }
}

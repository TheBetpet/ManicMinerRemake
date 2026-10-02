namespace ManicMiner.Core;

public sealed partial class Engine
{
    int rnd = 0x5A;

    void Special()
    {
        switch (Cur.Special)
        {
            case SpecialHandler.Eugene: R7B72(); break;
            case SpecialHandler.Kong: R7BA0(); break;
            case SpecialHandler.Skylab: R7AFB(); break;
            case SpecialHandler.SolarGenerator: R7C87(); break;
            case SpecialHandler.FinalBarrier: R7FC2(); break;
        }
    }

    // 7AFB: Skylab Landing Bay
    void R7AFB()
    {
        int iy = 0x8200;
        for (int b = 3; b > 0; b--, iy += 0x11)
        {
            if (M[iy + 6] == 0)
            {
                int e = M[iy + 5];
                if ((e & 3) != 0) continue;
                M[iy + 5] = (byte)(e + 0x20);
                int hl = 0x6A45 + (b - 1) * 3;
                M[iy + 7] = M[hl];
                M[iy + 2] = M[hl + 1];
                M[iy + 6] = M[hl + 2];
                M[iy + 4] = 0;
                M[iy + 8] = 0x10;
            }
            else
            {
                if (M[iy + 2] != 1) continue;
                M[iy + 6] = 0;
                M[iy + 2] = 4;
                M[iy + 3] = 4;
                M[iy + 4] = 1;
                M[iy + 8] = (byte)((b << 2) | 0xD3);
                rnd = (rnd * 5 + 17 + M[0x67AC]) & 0xFF;
                M[0x69C6] = (byte)((rnd & 0x0F) | 0x01);
                SoundQueue(0x69C1);
            }
        }
    }

    // 7B72: Eugene's Lair
    void R7B72()
    {
        if (M[0x67B5] != 0) return;
        int iy = 0x8200;
        if (M[0x67B2] == 0)
        {
            M[0x67B2]++;
            M[iy + 6] = 1;
            M[iy + 2] = 0xFF;
        }
        if (M[iy + 7] == 0x58) M[iy + 6] = 0;
        (M[0x670E], M[0x670F]) = (M[0x670F], M[0x670E]);
    }

    // 7BA0: Kong Beast levels
    void R7BA0()
    {
        int a = M[0x67B2];
        if (a == 0) return;
        if (a == 1)
        {
            a = 2;
            M[0x67B2] = 2;
            R7C69(0xC00C);
            M[0x67B3] = 0x10;
        }
        if (a == 2)
        {
            int v = M[0x67B3];
            if (v == 0xFF) return;
            if (v != 0)
            {
                M[0x67B3]--;
                R7AAF(v >= 9 ? 0x42E2 : 0x4322);
                return;
            }
            M[0x67B3] = 0xFF;
            M[0x42E2] = M[0x42E3] = M[0x4322] = M[0x4323] = 0;
            int g = 0x8222;
            M[g + 3] = 0x1F;
            if ((M[g + 4] & 0x80) == 0) M[g + 2] += 0x0C;
        }
        a = M[0x67B2];
        if (a == 3)
        {
            int iy = 0x8200;
            int p = Rw(0x5384);
            M[iy + 9] = (byte)p; M[iy + 10] = (byte)(p >> 8);
            M[iy + 6] = 4;
            M[iy + 2] = 0xFF;
            M[iy + 4] = 0;
            M[iy + 8] = 0x11;
            M[iy + 1] = 1;
            SoundQueue(0x69A6);
            R7C69(0xC024);
            a++;
            M[0x67B2] = (byte)a;
        }
        if (a != 4) return;
        {
            int iy = 0x8200;
            if (M[iy + 7] != 0x68) return;
            M[iy + 9] = 0xFA; M[iy + 10] = 0x69;
            M[iy + 8] = 0;
            M[0x67B2] = 0;
            SoundQueue(0x69D3);
            R7A66(0x67E9, 1);
            R7A66(0x67EA, 5);
        }
    }

    void R7C69(int hl)
    {
        R7A01(hl, 0x5606, 0xFF);
        R7A01(hl, 0x5616, 0xFF);
        int h = (hl ^ 0x8000) & 0xFFFF;
        M[h] = 0; M[h + 1] = 0;
    }

    // 7C87: Solar Power Generator light beam
    void R7C87()
    {
        const int c = 0x0F;
        int iy = 0x8288, ix = 0x828D, hl = 0xC02E;
        M[iy + 2] = 0;
        if (M[iy] == 0)
        {
            M[iy]++;
            int p = 0x5200;
            for (int b = 0x78; b > 0; b--)
            {
                int e = M[p++] | 0x2E;
                int d = M[p++];
                if ((b & 7) == 0) { M[ix++] = (byte)e; M[ix++] = (byte)d; }
                int de = (d << 8) | e;
                M[de] = c; M[de + 1] = c;
            }
            M[iy + 1] = 0x0F;
            return;
        }
        M[iy] ^= 0x80;
        if ((M[iy] & 0x80) != 0)
        {
            int b = M[iy + 1];
            int pl = Rw(0x6790);
            int e = M[0x6792] | M[pl];
            int d = M[pl + 1] & 0xC3;
            int de = (d << 8) | e;
            Probe(ix, b, de);
            Probe(ix, b, de + 2);
            Probe(ix, b, de + 2 + 0x3E);
            Probe(ix, b, de + 4 + 0x3E);
            return;
        }

        bool BeamDraw()
        {
            if (M[iy + 1] == 0) return false;
            M[iy + 1]--;
            int h = Rw(ix);
            for (int i = 0; i < 8; i++)
            {
                M[h] ^= c; M[h + 1] ^= c;
                h += 0x800;
            }
            return true;
        }

        bool BeamStore()
        {
            M[ix++] = (byte)hl; M[ix++] = (byte)(hl >> 8);
            M[iy + 3] = 0;
            int h = hl;
            for (int i = 0; i < 8; i++)
            {
                int o = M[h]; M[h] = (byte)(o ^ c);
                if (o != 0) M[iy + 3] = (byte)o;
                o = M[h + 1]; M[h + 1] = (byte)(o ^ c);
                if (o != 0) M[iy + 3] = (byte)o;
                h += 0x800;
            }
            return M[iy + 3] == 0;
        }

        int step = 0;
        while (true)
        {
            switch (step)
            {
                case 0: // 7D2D
                    BeamDraw();
                    if (BeamStore()) { step = 1; continue; }
                    hl = (M[ix - 1] & 0x43) << 8 | (hl & 0xFF);
                    if (M[hl] == 0) { step = 2; continue; }
                    step = 3; continue;
                case 1: // 7D23: move one row down
                    hl = ((M[ix - 1] << 8) | (hl & 0xFF)) + 0x40;
                    M[iy + 2]++;
                    step = 0; continue;
                case 2: // 7D52: move one cell left
                    hl = (((M[ix - 1] << 8) | (hl & 0xFF)) - 2) & 0xFFFF;
                    M[iy + 2]++;
                    BeamDraw();
                    if (BeamStore()) { step = 2; continue; }
                    hl = (M[ix - 1] & 0x43) << 8 | (hl & 0xFF);
                    if (M[hl] != 0) { step = 3; continue; }
                    step = 1; continue;
                default: // 7D3F
                    bool drew;
                    do { drew = BeamDraw(); ix += 2; } while (drew);
                    M[iy + 1] = (byte)((M[iy + 2] & 0x3F) + 1);
                    return;
            }
        }
    }

    void Probe(int ix, int b, int de)
    {
        int n = b == 0 ? 256 : b;
        int hl = ix;
        for (; n > 0; n--, hl += 2)
        {
            if ((de & 0xFF) != M[hl]) continue;
            if (((de >> 8) & 0xFF) != M[hl + 1]) continue;
            SoundQueue(0x69DC);
            R7978();
            return;
        }
    }

    // 7FC2: Final Barrier
    void R7FC2()
    {
        int a = 0, e = 8;
        if ((M[0x67AC] & 0x40) == 0) { a = 1; e = 7; }
        M[0x67B0] = (byte)a;
        int hl = Rw(0x67AD);
        int b = M[0x67AF] * 2;
        hl = ((hl & 0xFF) | (((hl >> 8) & 0x43) << 8));
        for (; b > 0; b--, hl++) M[hl & 0xFFFF] = (byte)e;
    }
}

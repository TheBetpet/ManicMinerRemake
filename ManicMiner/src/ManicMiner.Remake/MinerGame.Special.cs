using ManicMiner.Core;

namespace ManicMiner.Remake;

// The per-cavern special behaviours (Skylabs, Eugene, Kong, the Solar Power Generator beam, the Final Barrier).
public sealed partial class MinerGame
{
    int _skylabRnd = 0x5A;

    /// <summary>The beam's working storage (originally RAM from 0x8288): state bytes then the beam path.</summary>
    readonly byte[] _beam = new byte[0x400];

    void RunSpecial(LevelData L)
    {
        switch (L.Special)
        {
            case SpecialHandler.Eugene: Eugene(); break;
            case SpecialHandler.Kong: Kong(); break;
            case SpecialHandler.Skylab: Skylabs(); break;
            case SpecialHandler.SolarGenerator: SolarBeam(); break;
            case SpecialHandler.FinalBarrier: FinalBarrier(); break;
        }
    }

    // 7AFB: Skylab Landing Bay
    void Skylabs()
    {
        for (int n = 3, i = 0; n > 0; n--, i++)
        {
            var g = _guardians[i];
            if (g.Dy == 0)
            {
                int e = g.X;
                if ((e & 3) != 0) continue;
                g.X = (byte)(e + 0x20);
                int hl = GameData.SkylabTable + (n - 1) * 3;
                g.Y = Data[hl];
                g.Steps = Data[hl + 1];
                g.Dy = Data[hl + 2];
                g.Dx = 0;
                g.Flags = 0x10;
            }
            else
            {
                if (g.Steps != 1) continue;
                g.Dy = 0;
                g.Steps = 4;
                g.StepsReload = 4;
                g.Dx = 1;
                g.Flags = (byte)((n << 2) | 0xD3);
                _skylabRnd = (_skylabRnd * 5 + 17 + _frame) & 0xFF;
                _sfx.Skylab.Noise = (byte)((_skylabRnd & 0x0F) | 0x01);
                Play(_sfx.Skylab);
            }
        }
    }

    // 7B72: Eugene's Lair
    void Eugene()
    {
        if (ItemsLeft) return;
        var g = _guardians[0];
        if (_sw == 0)
        {
            _sw++;
            g.Dy = 1;
            g.Steps = 0xFF;
        }
        if (g.Y == 0x58) g.Dy = 0;
        (_pal[1], _pal[2]) = (_pal[2], _pal[1]);
    }

    // 7BA0: Kong Beast levels
    void Kong()
    {
        int a = _sw;
        if (a == 0) return;
        if (a == 1)
        {
            a = 2;
            _sw = 2;
            ToggleSwitch(0xC00C);
            _kongCount = 0x10;
        }
        if (a == 2)
        {
            int v = _kongCount;
            if (v == 0xFF) return;
            if (v != 0)
            {
                _kongCount--;
                SinkCell(v >= 9 ? CellOfAddr(0x42E2) : CellOfAddr(0x4322));
                return;
            }
            _kongCount = 0xFF;
            _map[CellOfAddr(0x42E2)] = 0;
            _map[CellOfAddr(0x4322)] = 0;
            var gr = _guardians[2];
            gr.StepsReload = 0x1F;
            if ((gr.Dx & 0x80) == 0) gr.Steps += 0x0C;
        }
        a = _sw;
        var k = _guardians[0];
        if (a == 3)
        {
            k.SpriteBase = Data.KongFrame;
            k.Dy = 4;
            k.Steps = 0xFF;
            k.Dx = 0;
            k.Flags = 0x11;
            k.Reload = 1;
            Play(_sfx.Kong);
            ToggleSwitch(0xC024);
            a++;
            _sw = a;
        }
        if (a != 4) return;
        if (k.Y != 0x68) return;
        k.SpriteBase = GameData.Blank;
        k.Flags = 0;
        _sw = 0;
        Play(_sfx.Death);
        AddScore(2, 1);
        AddScore(3, 5);
    }

    void ToggleSwitch(int addr)
    {
        Screen.XorTile(Data.Span(GameData.Switch1, 16), addr, 0xFF);
        Screen.XorTile(Data.Span(GameData.Switch2, 16), addr, 0xFF);
        _map[CellOfAddr(addr)] = 0;
    }

    // 7C87: Solar Power Generator light beam. B[0]=state, B[1]=beam length, B[2]=steps taken, B[3]=scratch, path from B[5].
    void SolarBeam()
    {
        const int c = 0x0F;
        var B = _beam;
        int ix = 5, hl = 0xC02E;
        B[2] = 0;
        if (B[0] == 0)
        {
            B[0]++;
            for (int b = 0x78; b > 0; b--)
            {
                int row = Screen.TableRow(0x78 - b);
                int e = (row & 0xFF) | 0x2E, d = row >> 8;
                if ((b & 7) == 0) { B[ix++] = (byte)e; B[ix++] = (byte)d; }
                int de = (d << 8) | e;
                Screen[de] = c; Screen[de + 1] = c;
            }
            B[1] = 0x0F;
            return;
        }
        B[0] ^= 0x80;
        if ((B[0] & 0x80) != 0)
        {
            int e = _w.XOff | _w.OldRowLo;
            int d = _w.OldRowHi & 0xC3;
            int de = (d << 8) | e;
            Probe(B[1], de);
            Probe(B[1], de + 2);
            Probe(B[1], de + 2 + 0x3E);
            Probe(B[1], de + 4 + 0x3E);
            return;
        }

        bool BeamDraw()
        {
            if (B[1] == 0) return false;
            B[1]--;
            int h = B[ix] | (B[ix + 1] << 8);
            for (int i = 0; i < 8; i++)
            {
                Screen[h] ^= c; Screen[h + 1] ^= c;
                h += 0x800;
            }
            return true;
        }

        bool BeamStore()
        {
            B[ix++] = (byte)hl; B[ix++] = (byte)(hl >> 8);
            B[3] = 0;
            int h = hl;
            for (int i = 0; i < 8; i++)
            {
                int o = Screen[h]; Screen[h] = (byte)(o ^ c);
                if (o != 0) B[3] = (byte)o;
                o = Screen[h + 1]; Screen[h + 1] = (byte)(o ^ c);
                if (o != 0) B[3] = (byte)o;
                h += 0x800;
            }
            return B[3] == 0;
        }

        int step = 0;
        while (true)
        {
            switch (step)
            {
                case 0:
                    BeamDraw();
                    if (BeamStore()) { step = 1; continue; }
                    hl = (B[ix - 1] & 0x43) << 8 | (hl & 0xFF);
                    if (Cell(CellOfAddr(hl)) == 0) { step = 2; continue; }
                    step = 3; continue;
                case 1:
                    hl = ((B[ix - 1] << 8) | (hl & 0xFF)) + 0x40;
                    B[2]++;
                    step = 0; continue;
                case 2:
                    hl = (((B[ix - 1] << 8) | (hl & 0xFF)) - 2) & 0xFFFF;
                    B[2]++;
                    BeamDraw();
                    if (BeamStore()) { step = 2; continue; }
                    hl = (B[ix - 1] & 0x43) << 8 | (hl & 0xFF);
                    if (Cell(CellOfAddr(hl)) != 0) { step = 3; continue; }
                    step = 1; continue;
                default:
                    bool drew;
                    do { drew = BeamDraw(); ix += 2; } while (drew);
                    B[1] = (byte)((B[2] & 0x3F) + 1);
                    return;
            }
        }
    }

    void Probe(int b, int de)
    {
        int n = b == 0 ? 256 : b;
        int hl = 5;
        for (; n > 0; n--, hl += 2)
        {
            if ((de & 0xFF) != _beam[hl & 0x3FF]) continue;
            if (((de >> 8) & 0xFF) != _beam[(hl + 1) & 0x3FF]) continue;
            Play(_sfx.SolarHit);
            UseAir();
            return;
        }
    }

    // 7FC2: Final Barrier
    void FinalBarrier()
    {
        int dir = 0, e = 8;
        if ((_frame & 0x40) == 0) { dir = 1; e = 7; }
        _convDir = dir;
        int idx = CellOfAddr(_convAddr);
        for (int b = _convCount; b > 0; b--, idx++) if ((uint)idx < (uint)_map.Length) _map[idx] = (byte)e;
    }
}

using System.Text;
using ManicMiner.Core;

namespace ManicMiner.Remake;

// Playing a cavern: loading, Willy's movement, the per-frame loop and the cavern HUD.
public sealed partial class MinerGame
{
    // ---- cavern cell types (low nibble of a map cell) --------------------------------------------
    const int Empty = 0, Wall = 1, Item = 2, NastyA = 3, NastyB = 4, Floor = 5, Crumble = 6,
        ConveyorRight = 7, ConveyorLeft = 8, Invisible = 9, Switch = 0x0A;
    const int FloorSolid = 2, FloorConveyorRight = 4, FloorConveyorLeft = 8;   // 678E flags

    /// <summary>The cavern map: 16 rows of 32 cells, plus padding (the original reads past the edges).</summary>
    readonly byte[] _map = new byte[16 * 32 + 128];

    sealed class WillyState
    {
        public int X, Y;                  // 6782 / 6783 (x in 2-pixel units, y in pixels)
        public int Dir;                   // 6784: -1, 0, +1
        public int Facing = 1;            // 6785: +1 right, -1 left
        public int Phase;                 // 678B: jump arc position (-8..9), 9 = not jumping / falling
        public bool Airborne;             // 678D
        public int Floor;                 // 678E
        public int Fall;                  // 678F: fall distance
        public int OldFrame = GameData.Blank, XOff, OldRowLo, OldRowHi, OldY;   // last drawn sprite
    }
    readonly WillyState _w = new();

    // cavern working state
    int _frame;                           // 67AC
    int _air;                             // 6A44
    int _sw;                              // 67B2: switch flag / Kong state
    int _kongCount;                       // 67B3
    int _itemCount, _itemMask = 0x0F, _itemGfx;      // 67B4 / 67B8 flash mask / 67B6 picture
    readonly int[] _itemAddr = new int[8];           // video address of each item cell
    readonly bool[] _itemActive = new bool[8];
    int _convCount, _convAddr, _convDir;  // 67AF / 67AD / 67B0
    int _portalFrame;
    byte[] _name = new byte[32];

    bool ItemsLeft { get { foreach (bool a in _itemActive) if (a) return true; return false; } }

    static int CellAddr(int idx) => Screen.Base + idx * 2;
    static int CellOfAddr(int addr) => ((addr & 0x7FF) >> 1);

    int Cell(int idx) => (uint)idx < (uint)_map.Length ? _map[idx] : 0;

    /// <summary>76E7: index of the map cell under Willy's x at pixel row y.</summary>
    int CellIndex(int y) => (((y & 0x7F) >> 3) * 32) + ((_w.X >> 2) & 0x1F);

    // ---- loading (7440) -----------------------------------------------------------------------------
    void LoadCavern(LevelData L)
    {
        for (int i = 0; i < 4; i++) _pal[i] = 0x0B;
        Array.Clear(_map);
        for (int r = 0; r < 16; r++)
            for (int c = 0; c < 32; c++) _map[r * 32 + c] = (byte)L.Layout[r][c];

        for (int idx = 0; idx < 512; idx++)
        {
            int a = _map[idx], addr = CellAddr(idx);
            if (a == ConveyorRight) RegisterConveyor(addr, 1);
            if (a == ConveyorLeft) RegisterConveyor(addr, 0);
            if (a == Item) { RegisterItem(addr); a = 0; }     // items are drawn by the flashing routine
            Screen.Tile(Data.TileGfx(L.TileGfx[a & 0xF]), addr);
        }
        _itemGfx = GameData.Tile(L.TileGfx[2]);
        BorderInk = L.Border;
        _pal[1] = (byte)L.Pen2; _pal[2] = (byte)L.Pen1; _pal[3] = (byte)L.Pen0;
        DrawTileList(GameData.HudTiles, HudBarAddr);
        string nm = L.Name.PadRight(32).Substring(0, 32);
        for (int i = 0; i < 32; i++) _name[i] = (byte)nm[i];
        Screen.Text(_name, 0xE400, 0xFF);

        int count = Math.Min(L.Guardians.Count, 8);
        _guardianCount = count;
        for (int i = 0; i < _guardians.Length; i++) _guardians[i] = new Guardian();
        for (int i = 0; i < count; i++) _guardians[i].Init(L.Guardians[i], Data);

        _portalX = (byte)L.PortalX;
        _portalY = (byte)L.PortalY;
        _portalIndex = L.PortalGfx < 0 ? _levelIndex % 20 : L.PortalGfx;
        _portalFrame = GameData.PortalFrame(_portalIndex);
        DrawPortal(0);
        DrawHudLine();
    }

    int _portalX, _portalY, _portalIndex;

    /// <summary>7ECA: draw / flash the portal.</summary>
    void DrawPortal(int c)
    {
        int e = (_portalIndex == 1 || _portalIndex == 9) ? 0xF0 : 0xFF;
        Screen.Xor(Data.Sprite(_portalFrame), _portalX >> 1, _portalY >> 1, e ^ c, false);
    }

    void DrawHudLine()
    {
        var line = new byte[32];
        Encoding.ASCII.GetBytes("High Score ").CopyTo(line, 0);
        _hiScore.CopyTo(line, 11);
        Encoding.ASCII.GetBytes("   Score ").CopyTo(line, 17);
        _score.CopyTo(line, 26);
        Screen.Text(line, 0xCD40, 0);
    }

    void RegisterConveyor(int addr, int dir)
    {
        _convDir = dir;
        if (_convCount == 0) _convAddr = addr;
        _convCount++;
    }

    void RegisterItem(int addr)
    {
        if (_itemCount == 8) return;
        _itemAddr[_itemCount] = addr;
        _itemActive[_itemCount] = true;
        _itemCount++;
    }

    // ---- conveyors, items, floors -----------------------------------------------------------------------
    /// <summary>77F3: animate the conveyor and advance the frame counter.</summary>
    void AnimateConveyors()
    {
        if (_convCount != 0)
        {
            bool z = _convDir == 0;
            ConveyorRow(_convAddr, z ? GameData.ConveyorA : GameData.ConveyorB);
            ConveyorRow(_convAddr + 0x1000, z ? GameData.ConveyorB : GameData.ConveyorA);
        }
        _frame = (_frame + 1) & 0xFF;
    }

    void ConveyorRow(int addr, int pattern)
    {
        pattern += (_frame * 2) & 6;
        for (int i = 0; i < _convCount; i++)
        {
            Screen[addr++] = (byte)(Data[pattern] & 0xF0);
            Screen[addr++] = (byte)(Data[pattern + 1] & 0xF0);
        }
    }

    /// <summary>79DA: draw (XOR) every uncollected item with the current flash colours.</summary>
    void DrawItems()
    {
        for (int i = 0; i < _itemCount; i++)
            if (_itemActive[i]) Screen.XorTile(Data.Span(_itemGfx, 16), _itemAddr[i], _itemMask);
    }

    /// <summary>79D0: flash the items.</summary>
    void FlashItems()
    {
        DrawItems();
        _itemMask = (_itemMask >> 1) | ((_itemMask & 1) << 7);
        DrawItems();
    }

    /// <summary>7A16: Willy touched an item.</summary>
    void CollectItem(int idx)
    {
        _map[idx] = 0;
        int scr = CellAddr(idx);
        for (int j = 0; j < 8; j++)
        {
            if (_itemAddr[j] != scr) continue;
            _itemActive[j] = false;
            Screen.XorTile(Data.Span(_itemGfx, 16), scr, _itemMask);
            AddScore(3, 1);
            Play(_sfx.Collect);
            return;
        }
    }

    /// <summary>7A66: add to the ASCII score (digit 0 is the 100,000s) and redraw it.</summary>
    void AddScore(int digit, int amount)
    {
        int a;
        while (true)
        {
            a = _score[digit] + amount;
            if (a < 0x3A || digit == 0) break;
            _score[digit] = (byte)(a - 10);
            amount = 1;
            digit--;
        }
        _score[digit] = (byte)a;
        if (digit == 1) _bonusLife = a;
        Screen.Text(_score, 0xCD74, 0);
    }

    /// <summary>7A8E: a crumbling floor cell has been stepped on.</summary>
    void StepCrumble(int idx)
    {
        int a = (_map[idx] + 0x10) & 0xFF;
        _map[idx] = (byte)a;
        if ((a & 0xF0) != 0x90) _w.Floor |= FloorSolid;
        else _map[idx] = 0;
        SinkCell(idx);
    }

    /// <summary>7AAF: shift a cell's 8 pixel rows down by one and clear the top row.</summary>
    void SinkCell(int idx)
    {
        int basea = CellAddr(idx);
        for (int line = 6; line >= 0; line--)
        {
            int src = basea + line * 0x800;
            Screen[src + 0x800] = Screen[src];
            Screen[src + 0x801] = Screen[src + 1];
        }
        Screen[basea] = 0;
        Screen[basea + 1] = 0;
    }

    // ---- HUD: air, lives ------------------------------------------------------------------------------------
    /// <summary>7978: use up some air. Returns true when the air has run out.</summary>
    bool UseAir()
    {
        int a = _air - 1;
        if (a == 0) return true;
        _air = a;
        int b = a & 3, c;
        if (b == 0) c = 0;
        else
        {
            int v = 0x0F;
            for (int i = 0; i < b; i++) v = ((v >> 1) | ((v & 1) << 7)) & 0xFF;
            c = v & 0xF0;
        }
        c |= (c >> 4) | ((c & 15) << 4);
        c &= 0xFF;
        int hl = 0xCC88 + (a >> 2);
        c |= Screen[hl];
        for (int i = 0; i < 4; i++) { hl += 0x800; Screen[hl] = (byte)c; }
        return false;
    }

    /// <summary>78E3: draw the remaining lives as a row of little Willys.</summary>
    void DrawLives()
    {
        int frame = GameData.WillyFrame(1) + ((_frame & 0x0C) << 4);
        int n = _lives;
        if (n == 0) { frame = GameData.Blank; n = 1; }
        int copies = ((n - 1) * 4) & 0xFF;
        for (int r = 0; r < 16; r++)
        {
            int start = Screen.TableRow(128 + r), de = start;
            for (int i = 0; i < 4; i++) Screen[de++] = Data[frame++];
            int s = start;
            for (int i = 0; i < copies; i++) Screen[de++] = Screen[s++];
            for (int i = 0; i < 4; i++) Screen[de++] = 0;
        }
    }

    // ---- music ---------------------------------------------------------------------------------------------------
    /// <summary>78AD: queue the next in-game music note when the channel is free.</summary>
    void PlayMusic()
    {
        if (_musicOff) return;
        if ((Sound.Check(1) & 0x87) != 4) return;
        int a = Data[_tunePtr];
        if (a == 0xFF) { _tunePtr = GameData.GameTune; a = Data[_tunePtr]; }
        _sfx.Note.Period = a << 1;
        _sfx.Note.Duration = (_sfx.Note.Duration & 0xFF00) | Data[_tunePtr + 1];
        _tunePtr += 2;
        Play(_sfx.Note);
    }

    // ---- guardians ---------------------------------------------------------------------------------------------------
    sealed class Guardian
    {
        public byte Tick, Reload, Steps, StepsReload, Dx, X, Dy, Y, Flags, Colour;
        public int SpriteBase, Frame = GameData.Blank, OldXByte, OldY;

        public void Init(GuardianData g, GameData d)
        {
            Flags = (byte)g.Flags;
            Tick = Reload = (byte)((Flags >> 4) & 3);
            Colour = (byte)d.GuardianColour(Flags >> 2);
            Steps = (byte)g.Steps;
            StepsReload = (byte)g.Reload;
            if ((Flags & 0x80) != 0) Dx = (byte)g.Velocity; else Dy = (byte)g.Velocity;
            X = (byte)g.X;
            Y = (byte)g.Y;
            SpriteBase = d.GuardianSet(g.SpriteSet);
        }
    }

    readonly Guardian[] _guardians = new Guardian[8];
    int _guardianCount;

    /// <summary>7704: move and draw the guardians. Returns true if one touched Willy.</summary>
    bool MoveGuardians()
    {
        for (int b = _guardianCount; b > 0; b--)
        {
            var g = _guardians[_guardianCount - b];
            if (unchecked(--g.Tick) == 0)
            {
                g.Tick = g.Reload;
                if (unchecked(--g.Steps) == 0)
                {
                    g.Steps = g.StepsReload;
                    g.Dx = unchecked((byte)-g.Dx);
                    g.Dy = unchecked((byte)-g.Dy);
                }
                g.X = unchecked((byte)(g.X + g.Dx));
                g.Y = unchecked((byte)(g.Y + g.Dy));
            }
            int flags = g.Flags;
            int a = (flags & 0x80) != 0 ? g.X : g.Steps;
            a &= flags & 3;
            int cc = (a << 6) & 0xFF;
            int dir = (g.Dx >> 7) & 1;
            if ((flags & 0x40) == 0) dir = 0;
            int xByte = (((g.X >> 1) | ((g.X & 1) << 7)) & 0x3E);
            int frame = (g.SpriteBase + ((dir << 8) | cc)) & 0xFFFF;
            Screen.Xor(Data.Sprite(g.Frame), g.OldXByte, g.OldY, g.Colour, false);   // erase the old image
            g.Frame = frame; g.OldXByte = xByte; g.OldY = g.Y;
            if (Screen.Xor(Data.Sprite(frame), xByte, g.Y, g.Colour, true)) return true;
            if ((b & 1) != 0) PlayMusic();
        }
        return false;
    }
}

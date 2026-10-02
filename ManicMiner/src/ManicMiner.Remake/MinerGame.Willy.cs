using ManicMiner.Core;

namespace ManicMiner.Remake;

// One cavern from start to finish, and Miner Willy's movement.
public sealed partial class MinerGame
{
    static int Sra(int v) => (sbyte)v >> 1;

    /// <summary>6FB2: play one cavern; the result is left in _outcome.</summary>
    IEnumerator<object?> PlayCavern()
    {
        _tunePtr = GameData.GameTune;
        var L = Cur;
        _w.X = L.WillyX;
        _w.Y = L.WillyY;
        if (_bonusLife != 0 && _lives != 7) _lives++;
        _w.Floor = 0; _w.Fall = 0; _bonusLife = 0; _w.Airborne = false; _w.Phase = 0;
        _convCount = 0; _itemCount = 0; _sw = 0; _kongCount = 0; _beam[0] = 0;
        Array.Clear(_itemActive);
        _w.Facing = 1;
        _w.OldFrame = GameData.Blank;
        _air = L.Air;
        LevelStarted?.Invoke(_levelIndex);
        LoadCavern(L);
        DrawLives();
        DrawItems();
        AnimateConveyors();
        ScreenOn = true;

        while (true)
        {
            long frameStart = Time;
            ScanKeys();
            bool skipWilly = false;
            if (_demoTimer != 0)
            {
                int a = _demoTimer - 1;
                if (a == 0) { _outcome = Outcome.Died; yield break; }
                _demoTimer = a;
                if (_kAny != 0) { _outcome = Outcome.Interrupted; yield break; }
                skipWilly = true;
            }
            if (!skipWilly && StepWilly()) { _outcome = Outcome.Died; yield break; }

            AnimateConveyors();
            if ((_frame & 7) == 0 && UseAir()) { _outcome = Outcome.Interrupted; yield break; }
            FlashItems();
            if (MoveGuardians()) { _outcome = Outcome.Died; yield break; }
            RunSpecial(L);
            DrawLives();
            if (!ItemsLeft)
            {
                DrawPortal((_frame & 1) != 0 ? 0x0F : 0xF0);
                if ((_portalX & 0xFC) == (_w.X & 0xFC) && ((((_w.Y * 2) & 0xFF) - _portalY + 8) & 0xFF) < 0x20)
                {
                    Play(_sfx.Portal);
                    PixelMerge(0x0F);
                    bool outOfAir;
                    do
                    {
                        outOfAir = UseAir();
                        AddScore(5, 4);
                        yield return Wait(5);
                    } while (!outOfAir);
                    Sound.Reset();
                    if (TestLevel != null) { _outcome = Outcome.Completed; yield break; }
                    _levelIndex++;
                    if (_levelIndex == Levels.Count) _levelIndex = 0;
                    _outcome = Outcome.Completed;
                    yield break;
                }
            }

            while (Time - frameStart < 25) yield return null;
            if (_demoTimer != 0) continue;

            int k = _kAny, prev = _kAnyPrev;
            _kAnyPrev = (byte)k;
            if (k != 0 && k != prev)
            {
                _musicOff = !_musicOff;
                Screen.Text(new[] { Data[_musicOff ? 0x6717 : 0x6716] }, 0xECF8, 0);
            }
            if (_kPause != 0)
            {
                yield return Wait(16);
                do { ScanKeys(); yield return null; } while (_kLeft == 0 && _kRight == 0 && _kAny == 0);
            }
            if (EscKey)
            {
                ResetScore();
                _outcome = Outcome.Quit;
                yield break;
            }
        }
    }

    /// <summary>Willy's movement, collisions and drawing for one frame. Returns true if he died.</summary>
    bool StepWilly()
    {
        var w = _w;
        int a, c;

        // start of a jump
        if (!w.Airborne && w.Floor != 0 && _kJump != 0)
        {
            int idx = CellIndex((w.Y + 0x10) & 0xFF);
            a = Cell(idx);
            bool conveyor = false;
            for (int b = 2; b > 0; b--)
            {
                if (a == ConveyorRight || a == ConveyorLeft) { conveyor = true; break; }
                idx++;
                a = Cell(idx);
            }
            if (!conveyor)
            {
                w.Dir = 0;
                if (_kRight != 0) w.Dir++;
                if (_kLeft != 0) w.Dir--;
            }
            _sfx.Jump.Period = (_sfx.Jump.Period & 0xFF00) | 0x5A;
            w.Phase = -8;
            w.Airborne = true;
        }

        // vertical movement
        int step = Sra(w.Phase);
        c = w.Y;
        w.Y = (w.Y + step) & 0xFF;
        if ((c & 7) == 0 && w.Phase >= 0)
        {
            int idx = CellIndex((w.Y + 0x10) & 0xFF);
            w.Floor = 0;
            a = Cell(idx);
            for (int b = 2; b > 0; b--)
            {
                switch (a & 0x0F)
                {
                    case Wall: case Floor: case Invisible: w.Floor |= FloorSolid; break;
                    case NastyA: case NastyB: return true;
                    case Crumble: StepCrumble(idx); break;
                    case ConveyorRight: w.Floor |= FloorConveyorRight; break;
                    case ConveyorLeft: w.Floor |= FloorConveyorLeft; break;
                }
                idx++;
                a = Cell(idx);
            }

            bool land = false;
            int f = w.Floor;
            bool conv = false;
            ref byte keyA = ref _kRight;
            ref byte keyB = ref _kLeft;
            int bb = -1;
            if ((f & FloorConveyorRight) != 0) { keyA = ref _kLeft; keyB = ref _kRight; bb = 1; conv = true; }
            else if ((f & FloorConveyorLeft) != 0) conv = true;
            else if ((f & FloorSolid) != 0) land = true;
            else if (w.Phase == 9) { w.Dir = 0; w.Fall = (w.Fall + 8) & 0xFF; }

            if (conv)
            {
                land = true;
                bool skip = false;
                if (w.Dir == 0) { keyA = 1; skip = true; }
                else if (w.Dir == bb && (keyA | keyB) != 0 && keyA == 0) skip = true;
                if (!skip) { keyA = 0xFF; keyB = 0; }
            }

            if (land)
            {
                int extra = w.Airborne ? 0x14 : 0;
                if (((w.Fall + extra) & 0xFF) >= 0x28) return true;
                w.Y = (w.Y - Sra(w.Phase)) & 0xFF;
                w.Airborne = false;
                w.Fall = 0;
                w.Phase = 9;
            }
        }

        // head check while rising
        if (w.Airborne && w.Phase < 0 && (((w.Y - Sra(w.Phase)) & 7) == 0))
        {
            int idx = CellIndex(w.Y);
            a = Cell(idx);
            for (int b = 2; b > 0; b--)
            {
                int t = a & 0x0F;
                if (t == Wall)
                {
                    w.Y = (w.Y - Sra(w.Phase)) & 0xFF;
                    w.Phase = 7;
                    break;
                }
                if (t == NastyA || t == NastyB) return true;
                if (t == Item) { CollectItem(idx); t = 0; }
                if (t == Switch) _sw |= 1;
                idx++;
                a = Cell(idx);
            }
        }

        // horizontal movement
        if (!w.Airborne && w.Floor != 0)
        {
            w.Dir = 0;
            if (_kRight != 0) w.Dir++;
            if (_kLeft != 0) w.Dir--;
        }
        w.X = (w.X + w.Dir) & 0xFF;
        {
            int rows = 2;
            if ((w.Y & 7) != 0) rows++;
            int idx = CellIndex(w.Y);
            bool blocked = false;
            for (int b = rows; b > 0 && !blocked; b--)
            {
                for (int k = 0; k < 2; k++)
                {
                    int t = Cell(idx + k) & 0x0F;
                    if (t == Wall) { blocked = true; break; }
                    if (t == Item) CollectItem(idx + k);
                    if (t == NastyA || t == NastyB) return true;
                    if (t == Switch) _sw |= 1;
                }
                idx += 32;
            }
            if (blocked) w.X = (w.X - w.Dir) & 0xFF;
        }

        if (w.Phase + 1 != 10) w.Phase++;
        a = w.Y & 0x7F;
        if (a >= 0x6E) a = 0x50;
        w.Y = a;

        if (w.Floor == 0 || w.Airborne)
        {
            if ((Sound.Check(2) & 0x80) == 0) Play(_sfx.Jump);
            int p = w.Phase < 0 ? -w.Phase : w.Phase;
            _sfx.Jump.Period = (_sfx.Jump.Period & 0xFF00) | (((p & 0x0F) * 2 + 0x32 + w.Fall) & 0xFF);
        }
        if (w.Dir != 0) w.Facing = w.Dir;

        bool left = w.Facing < 0;
        int bk = left ? 1 : 0;
        int xByte = ((((w.X + bk) & 0xFF) >> 2) & 0x1F) * 2;
        int frame = GameData.WillyFrame((((w.X + bk) & 3)) + 1 + (left ? 4 : 0));
        int row = Screen.TableRow(w.Y & 0x7F);
        Screen.Xor(Data.Sprite(w.OldFrame), w.XOff, w.OldY, 0xFF, false);
        w.OldFrame = frame; w.XOff = xByte; w.OldY = w.Y;
        w.OldRowLo = row & 0xFF; w.OldRowHi = row >> 8;
        Screen.Xor(Data.Sprite(frame), xByte, w.Y, 0xFF, false);
        return false;
    }
}

public sealed partial class MinerGame
{
    /// <summary>Render a static picture of a cavern (used by the level editor). Must not be called while the game is running.</summary>
    public void RenderPreview(LevelData level, uint[] argb)
    {
        if (Running) throw new InvalidOperationException("Preview needs a non-started game.");
        Levels = new List<LevelData> { level };
        Screen.Clear();
        _w.Floor = 0; _w.Fall = 0; _bonusLife = 0; _w.Airborne = false; _w.Phase = 0;
        _convCount = 0; _itemCount = 0; _sw = 0; _kongCount = 0; Array.Clear(_itemActive); _beam[0] = 0;
        _levelIndex = 0; _musicOff = true; _lives = 2;
        _w.X = level.WillyX; _w.Y = level.WillyY; _w.Facing = 1; _w.OldFrame = GameData.Blank;
        LoadCavern(level);
        MoveGuardians();
        int row = Screen.TableRow(_w.Y & 0x7F);
        Screen.Xor(Data.Sprite(GameData.WillyFrame((_w.X & 3) + 1)), ((_w.X >> 2) & 0x1F) * 2, _w.Y, 0xFF, false);
        ScreenOn = true;
        RenderScreen(argb);
    }
}

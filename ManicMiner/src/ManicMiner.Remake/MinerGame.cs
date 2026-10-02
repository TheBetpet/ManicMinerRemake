using System.Text;
using ManicMiner.Core;

namespace ManicMiner.Remake;

/// <summary>
/// Manic Miner (Amstrad CPC) re-implemented in C#. The logic is a direct translation of the original
/// program - same physics, timing (a 300 Hz tick, 25 ticks per game frame), screen memory layout and
/// collision rules - but it is expressed with ordinary objects and fields instead of a 64K memory image,
/// and nothing is read from the CPC disk image at run time.
/// The program flow is written as coroutines: every <c>yield return null</c> waits for the next 1/300s tick.
/// </summary>
public sealed partial class MinerGame : IDisposable
{
    public const int TicksPerSecond = 300;

    public readonly GameData Data;
    public readonly Screen Screen;
    public readonly CpcSound Sound = new();
    readonly Sfx _sfx;

    /// <summary>CPC keyboard matrix, one byte per line, bit set = key down.</summary>
    public readonly byte[] KeyMatrix = new byte[10];
    /// <summary>Keys seen at any time since the game last scanned the keyboard, so short taps are not lost between its 12.5 Hz scans.</summary>
    readonly byte[] _keyLatch = new byte[10];
    public void LatchKeys() { for (int i = 0; i < 10; i++) _keyLatch[i] |= KeyMatrix[i]; }
    public bool EscKey;

    public bool ScreenOn;
    public int BorderInk;
    public long Time;
    public List<LevelData> Levels;
    public Action<int>? LevelStarted;
    /// <summary>When set the game starts directly in this level (editor test-play) and exits to the title on death/finish.</summary>
    public LevelData? TestLevel;
    public bool TestPlayEnded;
    public bool Running { get; private set; }

    // game state shared between the title screen, the caverns and the HUD
    int _raster = 0;                // 670A: 300 Hz interrupt phase 1..6
    int _levelIndex;                // 6740
    int _lives = 3;                     // 6786
    int _demoTimer;                 // 6708: non-zero while the attract-mode demo plays
    int _bonusLife;                 // 6709: set when the score passes a 10,000 boundary
    bool _musicOff;                 // 6796
    readonly byte[] _score = Digits(), _hiScore = Digits();
    int _rnd = 0x5A;
    int _tunePtr;                   // 69F7
    // keyboard scan results (OR of the pressed keys inside each mask)
    byte _kLeft, _kRight, _kJump, _kAny, _kPause, _kAnyPrev;
    // palette: pen 3..0 as hardware inks (670D..6710); the HUD palette is fixed
    readonly byte[] _pal = new byte[4];
    static readonly byte[] HudPal = { 0x0B, 0x0C, 0x17, 0x14 };

    static byte[] Digits() => Enumerable.Repeat((byte)'0', 6).ToArray();

    LevelData Cur => Levels[_levelIndex];

    public MinerGame(GameData data, IEnumerable<LevelData>? levels = null)
    {
        Data = data;
        Screen = new Screen(data.Font);
        Levels = new List<LevelData>(levels ?? data.Levels);
        _sfx = new Sfx(data);
        Sfx.DefineEnvelopes(data, Sound);
        _itemMask = 0x0F;
        for (int i = 0; i < _guardians.Length; i++) _guardians[i] = new Guardian();
    }

    // ---- scheduler ------------------------------------------------------------------------
    readonly Stack<IEnumerator<object?>> _stack = new();
    bool _finished;

    public void Start()
    {
        if (Running) return;
        Running = true;
        _stack.Push(Main());
    }

    /// <summary>Advance the machine by one 300 Hz tick (the CPC raster interrupt).</summary>
    public void Tick()
    {
        if (!Running || _finished) return;
        Time++;
        _raster = _raster == 6 ? 1 : _raster + 1;
        Sound.Step(1.0 / TicksPerSecond);
        while (_stack.Count > 0)
        {
            var top = _stack.Peek();
            if (!top.MoveNext()) { _stack.Pop(); continue; }
            if (top.Current is IEnumerator<object?> sub) { _stack.Push(sub); continue; }
            return;
        }
        _finished = true;
    }

    public void Advance(int ticks) { for (int i = 0; i < ticks; i++) Tick(); }
    public void Dispose() { }

    IEnumerator<object?> Wait(int ticks) { while (ticks-- > 0) yield return null; }

    /// <summary>MC WAIT FLYBACK: wait for raster phase 1.</summary>
    IEnumerator<object?> WaitFlyback() { do yield return null; while (_raster != 1); }

    // ---- display ----------------------------------------------------------------------------
    /// <summary>Convert the current screen to ARGB (Cpc.Width x Cpc.Height).</summary>
    public void RenderScreen(uint[] argb)
    {
        var game = new int[4]; var hud = new int[4];
        for (int i = 0; i < 4; i++) { game[3 - i] = _pal[i] & 31; hud[3 - i] = HudPal[i] & 31; }
        Screen.Render(argb, game, hud);
        if (!ScreenOn) Array.Fill(argb, 0xFF000000u | Cpc.Hw[Cpc.FwToHw[BorderInk % 27]]);
    }

    // ---- keyboard -----------------------------------------------------------------------------
    // Which keys do what: five groups of CPC keyboard-matrix masks (one byte per matrix line).
    static readonly byte[][] KeyMasks =
    {
        new byte[] { 0x00, 0x01, 0x00, 0x04, 0x04, 0x04, 0x08, 0x04, 0x08, 0x04 },   // left
        new byte[] { 0x02, 0x00, 0x02, 0x08, 0x08, 0x08, 0x04, 0x08, 0x00, 0x08 },   // right
        new byte[] { 0x01, 0x02, 0xE0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0x80, 0x30 },   // jump
        new byte[] { 0x00, 0x00, 0x0C, 0x30, 0x30, 0x30, 0x00, 0x00, 0x00, 0x00 },   // any key / music toggle
        new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x30, 0x20, 0x00 },   // pause
    };

    byte ScanGroup(int g)
    {
        int c = 0;
        for (int k = 0; k < 10; k++) c |= (KeyMatrix[k] | _keyLatch[k]) & KeyMasks[g][k];
        return (byte)c;
    }

    /// <summary>76C7: read the keyboard.</summary>
    void ScanKeys()
    {
        _kLeft = ScanGroup(0); _kRight = ScanGroup(1); _kJump = ScanGroup(2); _kAny = ScanGroup(3); _kPause = ScanGroup(4);
        Array.Clear(_keyLatch);
    }

    // ---- sound -----------------------------------------------------------------------------------
    void Play(SoundBlock b) => b.PlayOn(Sound);

    // ---- main flow (6ED0) --------------------------------------------------------------------------
    IEnumerator<object?> Main()
    {
        if (TestLevel != null) { yield return RunTestPlay(); yield break; }
        bool gameOver = false;
        while (true)
        {
            if (gameOver) { yield return GameOverSequence(); gameOver = false; }

            // title screen
            BorderInk = 1;
            yield return WaitFlyback();
            ScreenOn = false;
            for (int i = 0; i < 4; i++) _pal[i] = HudPal[i];
            _musicOff = false; _levelIndex = 0; _pianoA = 0;
            Screen.Clear();
            DrawTitleScreen();
            DrawHudBackground();
            ScreenOn = true;
            yield return TitleTune();
            bool start = _flag;
            if (!start)
            {
                PianoClear(_pianoA); PianoClear(_pianoB);
                yield return Scroller();
                start = _flag;
            }
            if (!start)
            {
                BlankHud();
                // attract-mode demo
                bool toTitle = false;
                while (true)
                {
                    _demoTimer = 0x3C;
                    if (_levelIndex != 0) yield return FadeIn();
                    yield return RunLevel();
                    if (_outcome == Outcome.Interrupted) { start = true; break; }
                    if (_outcome == Outcome.Quit) { toTitle = true; break; }
                    _levelIndex++;
                    if (_levelIndex == Levels.Count) { toTitle = true; break; }
                }
                if (toTitle) continue;
            }

            // new game
            _levelIndex = 0; _demoTimer = 0; _bonusLife = 0; _lives = 2; _musicOff = true;
            BlankHud();
            while (true)
            {
                yield return RunLevel();
                if (_outcome == Outcome.Quit) break;
                Play(_sfx.Death);
                if (_lives == 0) { gameOver = true; break; }
                _lives--;
            }
        }
    }

    IEnumerator<object?> RunTestPlay()
    {
        BorderInk = 1;
        _levelIndex = 0;
        Levels = new List<LevelData> { TestLevel! };
        _demoTimer = 0; _bonusLife = 0; _lives = 2; _musicOff = true;
        ScreenOn = false;
        Screen.Clear();
        DrawHudBackground();
        BlankHud();
        while (true)
        {
            yield return PlayCavern();
            if (_outcome == Outcome.Completed || _outcome == Outcome.Quit) { TestPlayEnded = true; yield break; }
            Play(_sfx.Death);
            if (_lives == 0) { TestPlayEnded = true; yield break; }
            _lives--;
        }
    }

    /// <summary>Play caverns until Willy dies, the demo ends or the player quits (completed caverns just continue).</summary>
    IEnumerator<object?> RunLevel()
    {
        while (true)
        {
            yield return PlayCavern();
            if (_outcome != Outcome.Completed) yield break;
        }
    }

    enum Outcome { Died, Interrupted, Completed, Quit }
    Outcome _outcome;
    bool _flag;
}

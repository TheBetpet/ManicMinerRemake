namespace ManicMiner.Core;

/// <summary>
/// Faithful C# port of the Amstrad CPC Manic Miner (Software Projects, 1985).
/// The game state lives in a 64K byte array that uses the original addresses (variables at
/// 0x6700.., cavern map at 0x4000, screen at 0xC000...), and every routine below is a direct
/// translation of the Z80 routine at the address named in its comment, so behaviour
/// (physics, collision, timing in 1/300s ticks) matches the original.
/// </summary>
public sealed partial class Engine : IDisposable
{
    public const int TicksPerSecond = 300;

    public readonly byte[] M = new byte[65536];
    /// <summary>CPC keyboard matrix, one byte per line, bit set = key down.</summary>
    public readonly byte[] KeyMatrix = new byte[10];
    /// <summary>Keys seen at any time since the game last scanned the keyboard, so short taps are not lost between its 12.5 Hz scans.</summary>
    public readonly byte[] KeyLatch = new byte[10];
    /// <summary>Call every host frame after filling KeyMatrix.</summary>
    public void LatchKeys() { for (int i = 0; i < 10; i++) KeyLatch[i] |= KeyMatrix[i]; }
    public bool EscKey;
    public readonly CpcSound Sound = new();
    public bool ScreenOn;
    public int BorderInk;
    public long Time;

    readonly AssetPack _assets;
    public List<LevelData> Levels;
    LevelData Cur => Levels[M[0x6740]];

    Thread? _thread;
    readonly SemaphoreSlim _go = new(0), _done = new(0);
    volatile bool _quit;
    volatile bool _finished;
    public bool Running => _thread != null;

    /// <summary>Raised when a game level begins (level index) - used by hosts, optional.</summary>
    public Action<int>? LevelStarted;
    /// <summary>When set the engine starts directly in this level (editor test-play) and exits to the title on death/finish.</summary>
    public LevelData? TestLevel;
    public bool TestPlayEnded;

    public Engine(AssetPack assets, IEnumerable<LevelData>? levels = null)
    {
        _assets = assets;
        Levels = new List<LevelData>(levels ?? assets.Levels);
        Boot();
    }

    // ---- memory helpers -------------------------------------------------------------
    int Rw(int a) => M[a & 0xFFFF] | (M[(a + 1) & 0xFFFF] << 8);
    void Ww(int a, int v) { M[a & 0xFFFF] = (byte)v; M[(a + 1) & 0xFFFF] = (byte)(v >> 8); }

    void Boot()
    {
        Array.Clear(M);
        Array.Copy(_assets.Sprites, 0, M, 0x100, _assets.Sprites.Length);
        Array.Copy(_assets.Tables, 0, M, 0x5200, _assets.Tables.Length);
        Array.Copy(_assets.Tiles, 0, M, 0x5386, _assets.Tiles.Length);
        Array.Copy(_assets.Data6704, 0, M, 0x6704, _assets.Data6704.Length);
        Array.Copy(_assets.Title, 0, M, 0x80D1, _assets.Title.Length);
        Array.Copy(_assets.Keys, 0, M, 0xA000, _assets.Keys.Length);
        // 6E93..6EC8: envelope definitions
        Sound.DefineAmpEnvelope(1, M, 0x6919);
        Sound.DefineAmpEnvelope(2, M, 0x6920);
        Sound.DefineAmpEnvelope(3, M, 0x6924);
        Sound.DefineAmpEnvelope(4, M, 0x6928);
        Sound.DefineToneEnvelope(1, M, 0x6930);
        Sound.DefineToneEnvelope(2, M, 0x6934);
        Sound.DefineToneEnvelope(3, M, 0x692C);
    }

    // ---- scheduler: the engine runs on its own thread, advanced in 1/300s ticks -------
    public void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(() =>
        {
            try { _go.Wait(); if (_quit) return; Main(); }
            catch (OperationCanceledException) { }
            finally { _finished = true; _done.Release(); }
        }) { IsBackground = true, Name = "ManicMiner.Engine" };
        _thread.Start();
    }

    /// <summary>Advance the machine by one 300 Hz tick (the CPC raster interrupt).</summary>
    public void Tick()
    {
        if (_thread == null || _quit || _finished) return;
        Time++;
        Interrupt();
        Sound.Step(1.0 / TicksPerSecond);
        _go.Release();
        _done.Wait();
    }

    public void Advance(int ticks) { for (int i = 0; i < ticks; i++) Tick(); }

    public void Dispose()
    {
        _quit = true;
        _go.Release();
        _thread?.Join(500);
        _thread = null;
    }

    void WaitTick()
    {
        _done.Release();
        _go.Wait();
        if (_quit) throw new OperationCanceledException();
    }

    void Wait(int ticks) { while (ticks-- > 0) WaitTick(); }

    /// <summary>0xBD19 MC WAIT FLYBACK.</summary>
    void WaitFlyback() { do WaitTick(); while (M[0x670A] != 1); }

    /// <summary>793D: 300Hz interrupt, cycles the raster counter.</summary>
    void Interrupt()
    {
        int a = M[0x670A];
        if (a == 6) a = 0;
        M[0x670A] = (byte)(a + 1);
    }

    /// <summary>Game-area and HUD palettes as hardware ink numbers, pen 0..3.</summary>
    public void GetPalette(bool hud, int[] pens)
    {
        int b = hud ? 0x6712 : 0x670D;
        for (int i = 0; i < 4; i++) pens[3 - i] = M[b + i] & 31;
    }

    /// <summary>Convert the current 0xC000 screen to ARGB (Cpc.Width x Cpc.Height).</summary>
    public void RenderScreen(uint[] argb)
    {
        var game = new int[4]; var hud = new int[4];
        GetPalette(false, game); GetPalette(true, hud);
        for (int y = 0; y < Cpc.Height; y++)
        {
            var pal = y < 128 ? game : hud;
            int a = Cpc.ScreenAddr(y);
            for (int bx = 0; bx < 64; bx++)
            {
                byte b = M[a + bx];
                for (int p = 0; p < 4; p++)
                    argb[y * Cpc.Width + bx * 4 + p] = 0xFF000000u | Cpc.Hw[pal[Cpc.Pixel(b, p)] & 31];
            }
        }
        if (!ScreenOn) Array.Fill(argb, 0xFF000000u | Cpc.Hw[Cpc.FwToHw[BorderInk % 27]]);
    }

    // ---- sound firmware calls ---------------------------------------------------------
    void SoundQueue(int blockAddr) { Sound.Queue(M, blockAddr); }
    int SoundCheck(int ch) => Sound.Check(ch);
    void SoundReset() => Sound.Reset();
    void SetBorder(int fwInk) => BorderInk = fwInk;
    void ClearScreen() => Array.Clear(M, 0xC000, 0x4000);

    // ---- 6ED0: main flow -------------------------------------------------------------
    void Main()
    {
        if (TestLevel != null) { RunTestPlay(); return; }
        bool gameOver = false;
        while (true)
        {
            if (gameOver) { R7EF3(); gameOver = false; }
            // 6ED0 title screen
            SetBorder(1);
            WaitFlyback();
            ScreenOn = false;
            for (int i = 0; i < 5; i++) M[0x670C + i] = M[0x6711 + i];
            M[0x6796] = 0; M[0x6740] = 0; M[0x677F] = 0;
            ClearScreen();
            R8002();
            R7DBF();
            R741C();
            bool start = R7857();
            if (!start)
            {
                R7DDB(Rw(0x677E), 1);
                R7DDB(Rw(0x6780), 1);
                start = Scroller();
            }
            if (!start)
            {
                R7E94();
                // 6F63 demo mode
                while (true)
                {
                    M[0x6708] = 0x3C;
                    if (M[0x6740] != 0) R7EAC();
                    int r = RunLevel();
                    if (r == 1) { start = true; break; }
                    if (r == 3) goto title;
                    M[0x6740]++;
                    if (M[0x6740] == Levels.Count) goto title;
                }
            }
            // 6F82 new game
            M[0x6740] = 0; M[0x6708] = 0; M[0x6709] = 0; M[0x6786] = 2; M[0x6796] = 0xFF;
            R7E94();
            while (true)
            {
                int r = RunLevel();
                if (r == 3) goto title;
                SoundQueue(0x69D3);
                int lives = M[0x6786];
                if (lives == 0) { gameOver = true; break; }
                M[0x6786] = (byte)(lives - 1);
            }
        title:;
        }
    }

    /// <summary>6F1B..6F5E: the scrolling message.  Returns true if a key started the game.</summary>
    bool Scroller()
    {
        Ww(0x6A76, 0x6A77);
        for (int bc = 0x3A7; bc != 0; bc--)
        {
            int ix = Rw(0x6A76) + 1;
            Ww(0x6A76, ix);
            WaitFlyback();
            DrawText(ix, 0xCD41, 0x1F, 0);
            R7E53();
            WaitFlyback();
            DrawText(ix, 0xCD40, 0x20, 0);
            R76C7();
            if (M[0x6772] != 0) return true;
        }
        return false;
    }

    /// <summary>6FB2 and what follows it: returns 0 = returned (death / timeout), 1 = returned with carry
    /// (key pressed in demo / out of air), 3 = quit to title.  Level completion loops internally.</summary>
    int RunLevel()
    {
        while (true)
        {
            int r = Level();
            if (r != 2) return r;
        }
    }

    void RunTestPlay()
    {
        SetBorder(1);
        M[0x6740] = 0;
        Levels = new List<LevelData> { TestLevel! };
        M[0x6708] = 0; M[0x6709] = 0; M[0x6786] = 2; M[0x6796] = 0xFF;
        ScreenOn = false;
        ClearScreen();
        R7DBF();
        R7E94();
        while (true)
        {
            int r = Level();
            if (r == 2 || r == 3) { TestPlayEnded = true; return; }
            SoundQueue(0x69D3);
            int lives = M[0x6786];
            if (lives == 0) { TestPlayEnded = true; return; }
            M[0x6786] = (byte)(lives - 1);
        }
    }
}

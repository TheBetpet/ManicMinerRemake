using ManicMiner.Core;
using ManicMiner.Remake;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ManicMiner.Game;

public sealed class Game1 : Microsoft.Xna.Framework.Game
{
    const int Scale = 4, BorderX = 32, BorderY = 24;
    const int AudioRate = 44100;
    readonly GraphicsDeviceManager _gdm;
    readonly string? _levelFile, _levelsDir, _packFile;
    GameData _assets = null!;
    List<ExpansionPack> _packs = new();
    bool _inMenu, _waitRelease;
    int _menuSel;
    List<string> _menuItems = new();
    KeyboardState _prevKs;
    SpriteBatch _sb = null!;
    Texture2D _screen = null!;
    Texture2D _pixel = null!;
    MinerGame _engine = null!;
    readonly uint[] _argb = new uint[Cpc.Width * Cpc.Height];
    DynamicSoundEffectInstance? _audio;
    short[] _samples = null!;
    byte[] _bytes = null!;

    public Game1(string? levelFile, string? levelsDir = null, string? packFile = null)
    {
        _packFile = packFile;
        _levelFile = levelFile;
        _levelsDir = levelsDir;
        _gdm = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = (Cpc.Width + 2 * BorderX) * Scale,
            PreferredBackBufferHeight = (Cpc.Height + 2 * BorderY) * Scale,
        };
        Window.AllowUserResizing = true;
        Window.Title = "Manic Miner (CPC remake)";
        IsMouseVisible = false;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60);
    }

    protected override void LoadContent()
    {
        _sb = new SpriteBatch(GraphicsDevice);
        _screen = new Texture2D(GraphicsDevice, Cpc.Width, Cpc.Height, false, SurfaceFormat.Color);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _assets = GameData.Load(Path.Combine(AppContext.BaseDirectory, "Assets"));
        List<LevelData>? custom = null;
        if (_levelsDir != null)
            custom = Directory.GetFiles(_levelsDir, "*.json").OrderBy(f => f).Select(f => LevelData.FromJson(File.ReadAllText(f))).ToList();
        if (_packFile != null) custom = ExpansionPack.Load(_packFile).Levels;
        _packs = ExpansionPack.Discover();
        if (_levelFile == null && custom == null && _packs.Count > 0) OpenMenu();
        else StartGame(custom, _levelFile != null ? LevelData.FromJson(File.ReadAllText(_levelFile)) : null);

        int perFrame = AudioRate / 60;
        _samples = new short[perFrame];
        _bytes = new byte[perFrame * 2];
        try
        {
            _audio = new DynamicSoundEffectInstance(AudioRate, AudioChannels.Mono);
            _audio.Play(); _engine.Sound.CaptureRate = AudioRate;
        }
        catch (Exception) { _audio = null; }
    }

    void StartGame(List<LevelData>? levels, LevelData? test = null)
    {
        _engine?.Dispose();
        _engine = levels is { Count: > 0 } ? new MinerGame(_assets, levels) : new MinerGame(_assets);
        _engine.TestLevel = test;
        if (_audio != null) _engine.Sound.CaptureRate = AudioRate;
        _engine.Start();
        _inMenu = false;
        _waitRelease = true;
    }

    void OpenMenu()
    {
        _inMenu = true;
        _menuSel = 0;
        _menuItems = new List<string> { "Manic Miner (original, 20)" };
        _menuItems.AddRange(_packs.Select(p => $"{p.Name} ({p.Levels.Count})"));
        if (_engine == null) { _engine = new MinerGame(_assets); }
    }

    void UpdateMenu(KeyboardState ks)
    {
        bool Hit(Keys k) => ks.IsKeyDown(k) && _prevKs.IsKeyUp(k);
        if (Hit(Keys.Up) && _menuSel > 0) _menuSel--;
        if (Hit(Keys.Down) && _menuSel < _menuItems.Count - 1) _menuSel++;
        if (Hit(Keys.Enter) || Hit(Keys.Space))
            StartGame(_menuSel == 0 ? null : _packs[_menuSel - 1].Levels);
        if (Hit(Keys.Escape)) Exit();
    }

    protected override void Update(GameTime gameTime)
    {
        var ks = Keyboard.GetState();
        if (_inMenu) { UpdateMenu(ks); _prevKs = ks; return; }
        if (_packs.Count > 0 && _levelFile == null && ks.IsKeyDown(Keys.F9) && _prevKs.IsKeyUp(Keys.F9)) { _prevKs = ks; OpenMenu(); return; }
        _prevKs = ks;
        if (_waitRelease) { if (ks.GetPressedKeys().Length == 0) _waitRelease = false; Array.Clear(_engine.KeyMatrix); }
        else CpcKeyboard.Fill(ks, _engine.KeyMatrix);
        _engine.EscKey = ks.IsKeyDown(Keys.Escape);
        if (_levelFile != null && (_engine.TestPlayEnded || !_engine.Running)) { Exit(); return; }

        _engine.LatchKeys();
        _engine.Advance(MinerGame.TicksPerSecond / 60);

        if (_audio != null && _audio.PendingBufferCount < 4)
        {
            _engine.Sound.Render(_samples, _samples.Length, AudioRate);
            Buffer.BlockCopy(_samples, 0, _bytes, 0, _bytes.Length);
            _audio.SubmitBuffer(_bytes);
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_inMenu) { MenuScreen.Render(_assets, _argb, "SELECT GAME", _menuItems, _menuSel, "Up/Down Enter=play Esc=quit"); }
        else _engine.RenderScreen(_argb);
        var data = new Color[_argb.Length];
        for (int i = 0; i < data.Length; i++)
        {
            uint v = _argb[i];
            data[i] = new Color((int)((v >> 16) & 255), (int)((v >> 8) & 255), (int)(v & 255));
        }
        _screen.SetData(data);

        uint b = Cpc.Hw[Cpc.FwToHw[(_inMenu ? 1 : _engine.BorderInk) % 27]];
        var border = new Color((int)((b >> 16) & 255), (int)((b >> 8) & 255), (int)(b & 255));
        GraphicsDevice.Clear(border);

        int w = GraphicsDevice.Viewport.Width, h = GraphicsDevice.Viewport.Height;
        float s = Math.Min(w / (float)(Cpc.Width + 2 * BorderX), h / (float)(Cpc.Height + 2 * BorderY));
        var size = new Vector2(Cpc.Width * s, Cpc.Height * s);
        var pos = new Vector2((w - size.X) / 2, (h - size.Y) / 2);
        _sb.Begin(samplerState: SamplerState.PointClamp);
        _sb.Draw(_screen, new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y), Color.White);
        _sb.End();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _engine.Dispose();
        _audio?.Dispose();
    }
}

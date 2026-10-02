using ManicMiner.Core;
using ManicMiner.Remake;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ManicMiner.Editor;

enum Mode { Tiles, Guardians, Willy, Portal, Props }

sealed class Entry
{
    public LevelData Data = new();
    public string? Path;
    public bool Original;
    public bool Dirty;
    public bool InPack;
}

public sealed class EditorGame : Microsoft.Xna.Framework.Game
{
    const int MapScale = 4, MapX = 16, MapY = 40, MapW = 256, MapH = 128;
    const int AudioRate = 44100;
    static readonly string[] TileNames =
        { "Air", "Wall", "Item", "Hazard A", "Hazard B", "Floor", "Crumble", "Conveyor >", "Conveyor <" };
    static readonly string[] ModeHelp =
    {
        "LMB paint  RMB erase  0-8 pick tile  [ ] change tile graphic",
        "LMB select/add/drag  Del remove  Up/Down row  Left/Right change (Shift x8)",
        "LMB place Willy's start position",
        "LMB place the exit portal",
        "Up/Down row  Left/Right change (Shift x8)  Enter edits the name",
    };

    readonly GraphicsDeviceManager _gdm;
    SpriteBatch _sb = null!;
    Texture2D _px = null!, _font = null!, _map = null!;
    GameData _assets = null!;
    MinerGame _preview = null!;
    readonly uint[] _argb = new uint[Cpc.Width * Cpc.Height];
    readonly Color[] _col = new Color[Cpc.Width * Cpc.Height];
    bool _dirtyPreview = true;

    readonly List<Entry> _levels = new();
    int _cur;
    Entry E => _levels[_cur];
    LevelData L => E.Data;
    string _dir = "";
    string _status = "";
    Mode _mode = Mode.Tiles;
    int _tile = 1, _row, _selG;
    bool _editName;
    bool _editPack;
    string _packName = "My Expansion";
    int _packImport;
    bool _dragging;

    KeyboardState _ks, _pks;
    MouseState _ms, _pms;

    // test play
    MinerGame? _play;
    DynamicSoundEffectInstance? _audio;
    short[] _samples = null!;
    byte[] _bytes = null!;

    public EditorGame()
    {
        _gdm = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1400, PreferredBackBufferHeight = 760 };
        Window.Title = "Manic Miner level editor";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        Window.TextInput += OnText;
    }

    protected override void LoadContent()
    {
        _sb = new SpriteBatch(GraphicsDevice);
        _px = new Texture2D(GraphicsDevice, 1, 1); _px.SetData(new[] { Color.White });
        _map = new Texture2D(GraphicsDevice, Cpc.Width, Cpc.Height);
        _assets = GameData.Load(Path.Combine(AppContext.BaseDirectory, "Assets"));
        _preview = new MinerGame(_assets);

        var fd = new Color[128 * 128];
        for (int ch = 0; ch < 256; ch++)
            for (int r = 0; r < 8; r++)
                for (int b = 0; b < 8; b++)
                    if ((_assets.Font[ch * 8 + r] & (0x80 >> b)) != 0)
                        fd[((ch / 16) * 8 + r) * 128 + (ch % 16) * 8 + b] = Color.White;
        _font = new Texture2D(GraphicsDevice, 128, 128); _font.SetData(fd);

        _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ManicMinerLevels");
        Directory.CreateDirectory(_dir);
        foreach (var l in _assets.Levels) _levels.Add(new Entry { Data = l.Clone(), Original = true });
        foreach (var f in Directory.GetFiles(_dir, "*.json").OrderBy(x => x))
        {
            try { _levels.Add(new Entry { Data = LevelData.FromJson(File.ReadAllText(f)), Path = f }); }
            catch (Exception ex) { _status = $"Skipped {Path.GetFileName(f)}: {ex.Message}"; }
        }
        if (_status == "") _status = $"Custom levels folder: {_dir}";
        _samples = new short[AudioRate / 60]; _bytes = new byte[_samples.Length * 2];
    }

    // ---------------------------------------------------------------- input / update
    bool Pressed(Keys k) => _ks.IsKeyDown(k) && _pks.IsKeyUp(k);
    bool Ctrl => _ks.IsKeyDown(Keys.LeftControl) || _ks.IsKeyDown(Keys.RightControl);
    bool Shift => _ks.IsKeyDown(Keys.LeftShift) || _ks.IsKeyDown(Keys.RightShift);
    bool Click => _ms.LeftButton == ButtonState.Pressed && _pms.LeftButton == ButtonState.Released;

    protected override void Update(GameTime gameTime)
    {
        _pks = _ks; _pms = _ms;
        _ks = Keyboard.GetState(); _ms = Mouse.GetState();
        if (_play != null) { UpdatePlay(); return; }
        if (!IsActive) return;

        if (_editPack)
        {
            if (Pressed(Keys.Enter)) { _editPack = false; ExportPack(); }
            else if (Pressed(Keys.Escape)) { _editPack = false; _status = "Pack export cancelled."; }
            return;
        }
        if (_editName) { if (Pressed(Keys.Enter) || Pressed(Keys.Escape)) _editName = false; return; }

        if (Ctrl && Pressed(Keys.S)) Save();
        else if (Ctrl && Pressed(Keys.N)) NewLevel();
        else if (Ctrl && Pressed(Keys.D)) Duplicate();
        else if (Ctrl && Pressed(Keys.G)) { E.InPack = !E.InPack; _status = E.InPack ? "Cavern added to the pack selection." : "Cavern removed from the pack selection."; }
        else if (Ctrl && Pressed(Keys.E)) { _editPack = true; _status = "Type a pack name, Enter to export, Esc to cancel."; }
        else if (Ctrl && Pressed(Keys.I)) ImportPack();
        else if (Ctrl && Pressed(Keys.Delete)) DeleteFile();
        else if (Pressed(Keys.F5)) StartPlay();
        else if (Pressed(Keys.PageUp) && _cur > 0) { _cur--; ResetSel(); }
        else if (Pressed(Keys.PageDown) && _cur < _levels.Count - 1) { _cur++; ResetSel(); }
        else if (Pressed(Keys.Tab)) { _mode = (Mode)(((int)_mode + (Shift ? 4 : 1)) % 5); _row = 0; }

        if (!Ctrl)
        {
            for (int i = 0; i <= 8; i++)
                if (Pressed(Keys.D0 + i) || Pressed(Keys.NumPad0 + i)) { _tile = i; _mode = Mode.Tiles; }
            if (_mode == Mode.Tiles)
            {
                if (Pressed(Keys.OemOpenBrackets)) ChangeTileGfx(-1);
                if (Pressed(Keys.OemCloseBrackets)) ChangeTileGfx(1);
            }
            if (Pressed(Keys.F1)) _mode = Mode.Tiles;
            if (Pressed(Keys.F2)) _mode = Mode.Guardians;
            if (Pressed(Keys.F3)) _mode = Mode.Willy;
            if (Pressed(Keys.F4)) _mode = Mode.Portal;
            if (Pressed(Keys.F6)) _mode = Mode.Props;
        }

        UpdateMouse();
        if (_mode is Mode.Guardians or Mode.Props) UpdateGrid();
        base.Update(gameTime);
    }

    void ResetSel() { _selG = 0; _row = 0; _dirtyPreview = true; }
    void Touch() { E.Dirty = true; _dirtyPreview = true; }

    bool MouseInMap(out int px, out int py)
    {
        px = (_ms.X - MapX) / MapScale; py = (_ms.Y - MapY) / MapScale;
        return _ms.X >= MapX && _ms.Y >= MapY && px < MapW && py < MapH;
    }

    void UpdateMouse()
    {
        if (!MouseInMap(out int px, out int py)) { if (_ms.LeftButton == ButtonState.Released) _dragging = false; return; }
        bool left = _ms.LeftButton == ButtonState.Pressed, right = _ms.RightButton == ButtonState.Pressed;
        switch (_mode)
        {
            case Mode.Tiles:
                if (left || right)
                {
                    int t = right ? 0 : _tile;
                    ref int cell = ref L.Layout[py / 8][px / 8];
                    if (cell != t)
                    {
                        if (t == 2 && CountItems() >= 8) { _status = "A cavern can hold at most 8 items."; break; }
                        cell = t; Touch();
                    }
                }
                break;
            case Mode.Willy:
                if (left) { L.WillyX = Math.Clamp((px - 4) / 2, 0, 120); L.WillyY = Math.Clamp(py - 8, 0, 112); Touch(); }
                break;
            case Mode.Portal:
                if (left) { L.PortalX = Math.Clamp((px - 8) / 2, 0, 120); L.PortalY = Math.Clamp(py - 8, 0, 112); Touch(); }
                break;
            case Mode.Guardians:
                if (Click)
                {
                    int hit = -1;
                    for (int i = 0; i < L.Guardians.Count; i++)
                    {
                        var g = L.Guardians[i];
                        if (px >= g.X * 2 && px < g.X * 2 + 16 && py >= g.Y && py < g.Y + 16) hit = i;
                    }
                    if (hit >= 0) { _selG = hit; _dragging = true; }
                    else if (L.Guardians.Count < 8)
                    {
                        var proto = L.Guardians.Count > 0 ? L.Guardians[Math.Min(_selG, L.Guardians.Count - 1)] : null;
                        L.Guardians.Add(new GuardianData
                        {
                            Steps = proto?.Steps ?? 20, Reload = proto?.Reload ?? 19, SpriteSet = proto?.SpriteSet ?? 0,
                            Velocity = proto?.Velocity ?? 1, Flags = proto?.Flags ?? 0xDF,
                            X = Math.Clamp((px - 8) / 2, 0, 120), Y = Math.Clamp(py - 8, 0, 112),
                        });
                        _selG = L.Guardians.Count - 1; _dragging = true; Touch();
                    }
                    else _status = "A cavern can hold at most 8 guardians.";
                }
                if (_dragging && left && _selG < L.Guardians.Count)
                {
                    var g = L.Guardians[_selG];
                    g.X = Math.Clamp((px - 8) / 2, 0, 120); g.Y = Math.Clamp(py - 8, 0, 112); Touch();
                }
                break;
        }
        if (_ms.LeftButton == ButtonState.Released) _dragging = false;
    }

    int CountItems() => L.Layout.Sum(r => r.Count(c => c == 2));

    void ChangeTileGfx(int d)
    {
        int max = _assets.Tiles.Length / 16 - 1;
        L.TileGfx[_tile] = Math.Clamp(L.TileGfx[_tile] + d, 0, max); Touch();
    }

    string[] GridRows()
    {
        if (_mode == Mode.Guardians)
        {
            if (_selG >= L.Guardians.Count) return Array.Empty<string>();
            var g = L.Guardians[_selG];
            return new[]
            {
                $"Steps      {g.Steps}", $"Reload     {g.Reload}", $"Sprite set {g.SpriteSet}",
                $"Velocity   {(sbyte)g.Velocity}", $"X          {g.X}", $"Y          {g.Y}", $"Flags      {g.Flags:X2}",
            };
        }
        return new[]
        {
            $"Name       {L.Name.TrimEnd()}", $"Border ink {L.Border}", $"Pen 0      {L.Pen0}", $"Pen 1      {L.Pen1}",
            $"Pen 2      {L.Pen2}", $"Aux ink    {L.Aux}", $"Air        {L.Air}", $"Special    {L.Special}",
            $"Portal gfx {(L.PortalGfx < 0 ? "auto" : L.PortalGfx.ToString())}",
        };
    }

    void UpdateGrid()
    {
        int rows = GridRows().Length;
        if (rows == 0) return;
        if (Pressed(Keys.Up)) _row = (_row + rows - 1) % rows;
        if (Pressed(Keys.Down)) _row = (_row + 1) % rows;
        int d = Pressed(Keys.Right) ? 1 : Pressed(Keys.Left) ? -1 : 0;
        if (_mode == Mode.Guardians && Pressed(Keys.Delete) && _selG < L.Guardians.Count)
        {
            L.Guardians.RemoveAt(_selG); _selG = Math.Max(0, _selG - 1); _row = 0; Touch(); return;
        }
        if (_mode == Mode.Props && _row == 0 && Pressed(Keys.Enter)) { _editName = true; return; }
        if (d == 0) return;
        if (Shift) d *= 8;
        if (_mode == Mode.Guardians)
        {
            var g = L.Guardians[_selG];
            switch (_row)
            {
                case 0: g.Steps = Math.Clamp(g.Steps + d, 1, 255); break;
                case 1: g.Reload = Math.Clamp(g.Reload + d, 1, 255); break;
                case 2: g.SpriteSet = Math.Clamp(g.SpriteSet + d, 0, 30); break;
                case 3: g.Velocity = (byte)Math.Clamp((sbyte)g.Velocity + d, -127, 127); break;
                case 4: g.X = Math.Clamp(g.X + d, 0, 120); break;
                case 5: g.Y = Math.Clamp(g.Y + d, 0, 112); break;
                case 6: g.Flags = (g.Flags + d) & 0xFF; break;
            }
        }
        else
        {
            switch (_row)
            {
                case 1: L.Border = ((L.Border + d) % 27 + 27) % 27; break;
                case 2: L.Pen0 = ((L.Pen0 + d) & 31); break;
                case 3: L.Pen1 = ((L.Pen1 + d) & 31); break;
                case 4: L.Pen2 = ((L.Pen2 + d) & 31); break;
                case 5: L.Aux = ((L.Aux + d) & 31); break;
                case 6: L.Air = Math.Clamp(L.Air + d, 1, 255); break;
                case 7: L.Special = (SpecialHandler)(((int)L.Special + Math.Sign(d) + 6) % 6); break;
                case 8: L.PortalGfx = Math.Clamp(L.PortalGfx + d, -1, 19); break;
            }
        }
        Touch();
    }

    void OnText(object? s, TextInputEventArgs e)
    {
        if (_editPack)
        {
            if (e.Key == Keys.Back) { if (_packName.Length > 0) _packName = _packName[..^1]; }
            else if (e.Character >= 32 && e.Character < 127 && _packName.Length < 40) _packName += e.Character;
            return;
        }
        if (!_editName || _play != null) return;
        string n = L.Name.TrimEnd();
        if (e.Key == Keys.Back) { if (n.Length > 0) n = n[..^1]; }
        else if (e.Character >= 32 && e.Character < 127 && n.Length < 32) n += e.Character;
        else return;
        L.Name = n.PadRight(32);
        Touch();
    }

    // ---------------------------------------------------------------- file ops
    static string Sanitize(string n)
    {
        var s = new string(n.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        return s.Length == 0 ? "level" : s;
    }

    void Save()
    {
        var e = E;
        if (e.Path == null)
        {
            string p;
            int k = 0;
            do p = Path.Combine(_dir, Sanitize(e.Data.Name) + (k++ == 0 ? "" : "_" + k) + ".json");
            while (File.Exists(p));
            if (e.Original)
            {
                var copy = new Entry { Data = e.Data.Clone(), Path = p };
                _levels.Add(copy);
                _levels[_cur] = new Entry { Data = _assets.Levels[_cur].Clone(), Original = true };
                _cur = _levels.Count - 1; e = copy;
            }
            else e.Path = p;
        }
        File.WriteAllText(e.Path!, e.Data.ToJson());
        e.Dirty = false;
        _status = $"Saved {e.Path}";
    }

    /// <summary>Ctrl+E: write the marked caverns (or every custom cavern if none are marked) to an expansion pack file.</summary>
    void ExportPack()
    {
        var chosen = _levels.Where(x => x.InPack).ToList();
        if (chosen.Count == 0) chosen = _levels.Where(x => !x.Original).ToList();
        if (chosen.Count == 0) { _status = "Nothing to export: mark caverns with Ctrl+G or create custom ones."; return; }
        string name = _packName.Trim().Length == 0 ? "My Expansion" : _packName.Trim();
        var pack = new ExpansionPack { Name = name, Levels = chosen.Select(x => x.Data.Clone()).ToList() };
        try
        {
            string path = Path.Combine(ExpansionPack.UserFolder, ExpansionPack.FileNameFor(name));
            pack.Save(path);
            foreach (var x in _levels) x.InPack = false;
            _status = $"Exported {pack.Levels.Count} caverns to {path}";
        }
        catch (Exception ex) { _status = "Export failed: " + ex.Message; }
    }

    /// <summary>Ctrl+I: load the next pack found on disk into the editor as new (unsaved) custom caverns, marked for the pack.</summary>
    void ImportPack()
    {
        var errs = new List<string>();
        var packs = ExpansionPack.Discover(errs);
        if (packs.Count == 0) { _status = "No expansion packs found in " + ExpansionPack.UserFolder; return; }
        var p = packs[_packImport++ % packs.Count];
        _packName = p.Name;
        int first = _levels.Count;
        foreach (var l in p.Levels) _levels.Add(new Entry { Data = l.Clone(), Dirty = true, InPack = true });
        _cur = first; ResetSel();
        _status = $"Imported pack '{p.Name}' ({p.Levels.Count} caverns). Edit, then Ctrl+E to export again.";
    }

    void NewLevel()
    {
        var l = new LevelData { Name = "New Cavern".PadRight(32), Border = 0, Aux = 12, Pen2 = 12, Pen1 = 18, Pen0 = 20, WillyX = 8, WillyY = 104, PortalX = 116, PortalY = 16 };
        l.TileGfx = _assets.Levels[0].TileGfx.ToArray();
        for (int r = 0; r < 16; r++) for (int c = 0; c < 32; c++)
                l.Layout[r][c] = (c == 0 || c == 31 || r == 15) ? 1 : 0;
        _levels.Add(new Entry { Data = l, Dirty = true });
        _cur = _levels.Count - 1; ResetSel(); _mode = Mode.Tiles;
        _status = "New cavern created (Ctrl+S to save).";
    }

    void Duplicate()
    {
        var c = L.Clone(); c.Name = (c.Name.TrimEnd() + " 2").PadRight(32)[..32];
        _levels.Add(new Entry { Data = c, Dirty = true });
        _cur = _levels.Count - 1; ResetSel();
        _status = "Duplicated.";
    }

    void DeleteFile()
    {
        if (E.Original) { _status = "Original caverns can't be deleted."; return; }
        if (E.Path != null && File.Exists(E.Path)) File.Delete(E.Path);
        _levels.RemoveAt(_cur); _cur = Math.Min(_cur, _levels.Count - 1); ResetSel();
        _status = "Deleted.";
    }

    // ---------------------------------------------------------------- test play
    void StartPlay()
    {
        _play = new MinerGame(_assets) { TestLevel = L.Clone() };
        _play.Start();
        try { _audio = new DynamicSoundEffectInstance(AudioRate, AudioChannels.Mono); _audio.Play(); _play.Sound.CaptureRate = AudioRate; }
        catch (Exception) { _audio = null; }
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60);
        IsMouseVisible = false;
    }

    void StopPlay()
    {
        _play?.Dispose(); _play = null;
        _audio?.Dispose(); _audio = null;
        IsMouseVisible = true; _dirtyPreview = true;
    }

    void UpdatePlay()
    {
        CpcKeyboard.Fill(_ks, _play!.KeyMatrix);
        _play.EscKey = _ks.IsKeyDown(Keys.Escape);
        if (_play.TestPlayEnded || !_play.Running || Pressed(Keys.F5)) { StopPlay(); return; }
        _play.LatchKeys();
        _play.Advance(MinerGame.TicksPerSecond / 60);
        if (_audio != null && _audio.PendingBufferCount < 4)
        {
            _play.Sound.Render(_samples, _samples.Length, AudioRate);
            Buffer.BlockCopy(_samples, 0, _bytes, 0, _bytes.Length);
            _audio.SubmitBuffer(_bytes);
        }
    }

    // ---------------------------------------------------------------- drawing
    static Color Rgb(uint v) => new((int)((v >> 16) & 255), (int)((v >> 8) & 255), (int)(v & 255));

    void Rect(int x, int y, int w, int h, Color c) => _sb.Draw(_px, new Rectangle(x, y, w, h), c);

    void Text(string s, int x, int y, Color c, int scale = 2)
    {
        foreach (char ch in s)
        {
            var src = new Rectangle((ch % 16) * 8, (ch / 16) * 8, 8, 8);
            _sb.Draw(_font, new Rectangle(x, y, 8 * scale, 8 * scale), src, c);
            x += 8 * scale;
        }
    }

    Color[] PenColors()
    {
        return new[] { Rgb(Cpc.Hw[L.Pen0 & 31]), Rgb(Cpc.Hw[L.Pen1 & 31]), Rgb(Cpc.Hw[L.Pen2 & 31]), Rgb(Cpc.Hw[0x0B]) };
    }

    void DrawTileIcon(int gfx, int x, int y, int scale)
    {
        var pens = PenColors();
        for (int r = 0; r < 8; r++)
            for (int b = 0; b < 2; b++)
            {
                byte v = _assets.Tiles[gfx * 16 + r * 2 + b];
                for (int p = 0; p < 4; p++)
                    Rect(x + (b * 4 + p) * scale, y + r * scale, scale, scale, pens[Cpc.Pixel(v, p)]);
            }
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_play != null)
        {
            _play.RenderScreen(_argb);
            for (int i = 0; i < _col.Length; i++) _col[i] = Rgb(_argb[i]);
            _map.SetData(_col);
            GraphicsDevice.Clear(Rgb(Cpc.Hw[Cpc.FwToHw[_play.BorderInk % 27]]));
            int w = GraphicsDevice.Viewport.Width, h = GraphicsDevice.Viewport.Height;
            float s = Math.Min(w / 320f, h / 232f);
            _sb.Begin(samplerState: SamplerState.PointClamp);
            _sb.Draw(_map, new Rectangle((int)((w - Cpc.Width * s) / 2), (int)((h - Cpc.Height * s) / 2), (int)(Cpc.Width * s), (int)(Cpc.Height * s)), Color.White);
            Text("Test play - Esc / F5 returns to the editor", 8, 4, Color.White, 1);
            _sb.End();
            return;
        }

        if (_dirtyPreview)
        {
            _preview.RenderPreview(L, _argb);
            for (int i = 0; i < _col.Length; i++) _col[i] = Rgb(_argb[i]);
            _map.SetData(_col);
            _dirtyPreview = false;
        }

        GraphicsDevice.Clear(new Color(32, 32, 40));
        _sb.Begin(samplerState: SamplerState.PointClamp);

        Text($"{_cur + 1}/{_levels.Count} {(E.Original ? "[original]" : "[custom]")}{(E.Dirty ? " *" : "")}{(E.InPack ? " [P]" : "")}  {L.Name.TrimEnd()}", MapX, 12, Color.White);
        _sb.Draw(_map, new Rectangle(MapX, MapY, MapW * MapScale, MapH * MapScale), new Rectangle(0, 0, MapW, MapH), Color.White);

        // grid
        var grid = new Color(255, 255, 255, 28);
        for (int c = 0; c <= 32; c++) Rect(MapX + c * 8 * MapScale, MapY, 1, MapH * MapScale, grid);
        for (int r = 0; r <= 16; r++) Rect(MapX, MapY + r * 8 * MapScale, MapW * MapScale, 1, grid);

        // overlays
        void Box(int px, int py, int w, int h, Color c)
        {
            int x = MapX + px * MapScale, y = MapY + py * MapScale, ww = w * MapScale, hh = h * MapScale;
            Rect(x, y, ww, 2, c); Rect(x, y + hh - 2, ww, 2, c); Rect(x, y, 2, hh, c); Rect(x + ww - 2, y, 2, hh, c);
        }
        for (int i = 0; i < L.Guardians.Count; i++)
        {
            var g = L.Guardians[i];
            Box(g.X * 2, g.Y, 16, 16, i == _selG && _mode == Mode.Guardians ? Color.Yellow : new Color(255, 80, 80));
            if (_mode == Mode.Guardians)
            {
                bool horiz = (g.Flags & 0x80) != 0;
                int span = g.Steps * (horiz ? Math.Abs((sbyte)g.Velocity) * 2 : Math.Abs((sbyte)g.Velocity));
                if (horiz) Rect(MapX + g.X * 2 * MapScale, MapY + (g.Y + 18) * MapScale, span * MapScale, 2, Color.Orange);
                else Rect(MapX + (g.X * 2 + 18) * MapScale, MapY + g.Y * MapScale, 2, span * MapScale, Color.Orange);
            }
        }
        Box(L.WillyX * 2, L.WillyY, 8, 16, _mode == Mode.Willy ? Color.Yellow : Color.Cyan);
        Box(L.PortalX * 2, L.PortalY, 16, 16, _mode == Mode.Portal ? Color.Yellow : Color.Magenta);
        if (_mode == Mode.Tiles && MouseInMap(out int mx, out int my))
            Box(mx / 8 * 8, my / 8 * 8, 8, 8, Color.Yellow);

        // palette
        int py0 = MapY + MapH * MapScale + 16;
        for (int t = 0; t <= 8; t++)
        {
            int x = MapX + t * 110;
            Rect(x - 4, py0 - 4, 104, 76, t == _tile ? new Color(90, 90, 40) : new Color(50, 50, 60));
            DrawTileIcon(L.TileGfx[t], x + 4, py0, 6);
            Text($"{t}", x + 60, py0, Color.White);
            Text(TileNames[t], x, py0 + 54, Color.LightGray, 1);
            Text($"gfx {L.TileGfx[t]}", x + 60, py0 + 24, Color.Gray, 1);
            if (MouseInRect(x - 4, py0 - 4, 104, 76) && Click) { _tile = t; _mode = Mode.Tiles; }
        }

        // side panel
        int sx = MapX + MapW * MapScale + 24;
        Text("Mode", sx, MapY, Color.Gray);
        string[] mn = { "F1 Tiles", "F2 Guardians", "F3 Willy start", "F4 Exit portal", "F6 Properties" };
        for (int i = 0; i < 5; i++)
        {
            Color c = (int)_mode == i ? Color.Yellow : Color.LightGray;
            int idx = i;
            Text(mn[i], sx, MapY + 24 + i * 22, c);
            if (MouseInRect(sx, MapY + 24 + i * 22, 220, 18) && Click) { _mode = (Mode)idx; _row = 0; }
        }
        var rows = _mode is Mode.Guardians or Mode.Props ? GridRows() : Array.Empty<string>();
        for (int i = 0; i < rows.Length; i++)
        {
            int y = MapY + 150 + i * 22;
            if (i == _row) Rect(sx - 4, y - 2, 330, 20, new Color(70, 70, 110));
            Text(rows[i] + (_editName && i == 0 && _mode == Mode.Props ? "_" : ""), sx, y, Color.White);
            if (MouseInRect(sx - 4, y - 2, 330, 20) && Click) _row = i;
            DrawSwatch(sx + 300, y, i);
        }
        if (_mode == Mode.Guardians)
        {
            for (int i = 0; i < L.Guardians.Count; i++)
            {
                var g = L.Guardians[i];
                Text($"{i + 1}: set {g.SpriteSet} at {g.X},{g.Y}", sx, MapY + 150 + 8 * 22 + i * 18, i == _selG ? Color.Yellow : Color.Gray, 1);
            }
            if (_selG < L.Guardians.Count)
            {
                var g = L.Guardians[_selG];
                Text($"{((g.Flags & 0x80) != 0 ? "horizontal" : "vertical")}, speed {(g.Flags >> 4) & 3}, colour {(g.Flags >> 2) & 3}, frames {(g.Flags & 3) + 1}{((g.Flags & 0x40) != 0 ? ", faces dir" : "")}",
                    sx, MapY + 150 + 7 * 22 + 4, Color.Gray, 1);
            }
        }

        int by = GraphicsDevice.Viewport.Height - 52;
        Text(ModeHelp[(int)_mode], MapX, by, Color.LightGray, 1);
        Text("PgUp/PgDn level  Ctrl+S save  Ctrl+N new  Ctrl+D duplicate  Ctrl+Del delete  F5 test play  Tab next mode", MapX, by + 14, Color.Gray, 1);
        Text(_editPack ? $"Pack name: {_packName}_" : $"Packs: Ctrl+G mark cavern ({_levels.Count(x => x.InPack)} marked)  Ctrl+E export pack  Ctrl+I import pack", MapX, by + 42, _editPack ? Color.Yellow : Color.Gray, 1);
        Text(_status, MapX, by + 28, Color.LightGreen, 1);
        int items = CountItems();
        Text($"Items {items}/8   Guardians {L.Guardians.Count}/8", sx, MapY + 110, items > 8 ? Color.Red : Color.Gray, 1);
        _sb.End();
        base.Draw(gameTime);
    }

    void DrawSwatch(int x, int y, int row)
    {
        if (_mode != Mode.Props || row < 1 || row > 5) return;
        uint v = row == 1 ? Cpc.Hw[Cpc.FwToHw[L.Border % 27]] : Cpc.Hw[(row switch { 2 => L.Pen0, 3 => L.Pen1, 4 => L.Pen2, _ => L.Aux }) & 31];
        Rect(x, y, 24, 16, Rgb(v));
    }

    bool MouseInRect(int x, int y, int w, int h) => _ms.X >= x && _ms.X < x + w && _ms.Y >= y && _ms.Y < y + h;

    protected override void UnloadContent()
    {
        _play?.Dispose(); _preview.Dispose(); _audio?.Dispose();
    }
}

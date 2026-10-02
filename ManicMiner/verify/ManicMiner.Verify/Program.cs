using ManicMiner.Core;

string root = args.Length > 0 ? args[0] : "../../..";
var assets = AssetPack.Load(Path.Combine(root, "Assets"));
string vdir = Path.Combine(root, "verify");

int Compare(Engine e, string name, int ticks)
{
    var snap = File.ReadAllBytes(Path.Combine(vdir, name + "_ram.bin"));
    int bad = 0;
    var rows = new SortedDictionary<int, int>();
    for (int y = 0; y < Cpc.Height; y++)
    {
        int a = Cpc.ScreenAddr(y);
        for (int x = 0; x < 64; x++)
            if (e.M[a + x] != snap[a + x]) { bad++; if (Environment.GetEnvironmentVariable("V") != null) Console.WriteLine($"  y={y} bx={x} got={e.M[a+x]:X2} want={snap[a+x]:X2}"); rows[y / 8] = rows.GetValueOrDefault(y / 8) + 1; }
    }
    Console.WriteLine($"{name}: {bad} differing screen bytes after {ticks} ticks; per char-row: " + string.Join(" ", rows.Select(k => $"{k.Key}:{k.Value}")));
    return bad;
}

string mode = args.Length > 1 ? args[1] : "all";
if (mode is "all" or "level1")
{
    using var e = new Engine(assets) { TestLevel = assets.Levels[0] };
    e.Start();
    for (int t = 0; t < 5000 && e.M[0x67AC] != 0x31; t++) e.Tick();
    Compare(e, "level1", 0);
}
if (mode is "all" or "menu")
{
    using var e = new Engine(assets);
    e.Start();
    e.Advance(40);
    Compare(e, "menu", 40);
}
if (mode is "smoke")
{
    var rng = new Random(1);
    void Fuzz(Engine e, int ticks)
    {
        for (int t = 0; t < ticks; t++)
        {
            if (t % 20 == 0) for (int i = 0; i < 10; i++) e.KeyMatrix[i] = (byte)(rng.Next(4) == 0 ? rng.Next(256) : 0xFF);
            e.Tick();
        }
    }
    for (int i = int.Parse(Environment.GetEnvironmentVariable("LO") ?? "0"); i < int.Parse(Environment.GetEnvironmentVariable("HI") ?? "20"); i++)
    {
        using var e = new Engine(assets) { TestLevel = assets.Levels[i] };
        e.Start();
        Fuzz(e, 6000);
        Console.WriteLine($"level {i} ended={e.TestPlayEnded} ok, frame={e.M[0x67AC]} air={e.M[0x6A44]} lives={e.M[0x6786]}");
    }
    if (Environment.GetEnvironmentVariable("HI") != null) return;
    using var g = new Engine(assets);
    g.Start();
    Fuzz(g, 60000);
    Console.WriteLine("full game flow ok");
}
if (mode == "jump3")
{
    using var e = new Engine(assets) { TestLevel = assets.Levels[2] };
    e.Start();
    for (int t = 0; t < 100; t++) e.Tick();
    foreach (var dirn in new[] { "none", "right", "left" })
    for (int trial = 0; trial < 8; trial++)
    {
        Array.Clear(e.KeyMatrix);
        for (int t = 0; t < 600; t++) e.Tick();
        e.M[0x6782] = (byte)(56 + trial * 3); e.M[0x6783] = 72; e.M[0x678D] = 0; e.M[0x678E] = 0; e.M[0x678B]=0;
        for (int t = 0; t < 25; t++) e.Tick();
        int x0 = e.M[0x6782];
        if (dirn == "right") e.KeyMatrix[7] = 0x08;
        if (dirn == "left") e.KeyMatrix[8] = 0x08; // Q placeholder
        for (int t = 0; t < 7; t++) { e.LatchKeys(); e.Tick(); }
        e.KeyMatrix[5] |= 0x80;
        bool jumped = false;
        for (int f = 0; f < 3; f++) { e.LatchKeys(); for (int t = 0; t < 5; t++) { e.Tick(); if (e.M[0x678D] != 0) jumped = true; } }
        if (!jumped) Console.WriteLine($"  dbg x={e.M[0x6782]} y={e.M[0x6783]} 678B={e.M[0x678B]} 678D={e.M[0x678D]} 6767={e.M[0x6767]} 6708={e.M[0x6708]}");
        e.KeyMatrix[5] = 0;
        for (int t = 0; t < 60; t++) { e.Tick(); if (e.M[0x678D] != 0) jumped = true; }
        Console.WriteLine($"{dirn} x0={x0} jumped={jumped} flags={e.M[0x678E]}");
    }
}
if (mode == "jump2")
{
    // Menagerie central platform: row 11, cols 14-18 (x=56..76), stand at y=72
    using var e = new Engine(assets) { TestLevel = assets.Levels[2] };
    e.Start();
    for (int t = 0; t < 100; t++) e.Tick();
    for (int trial = 0; trial < 12; trial++)
    {
        Array.Clear(e.KeyMatrix);
        for (int t = 0; t < 400; t++) e.Tick();
        e.M[0x6782] = (byte)(58 + trial); e.M[0x6783] = 72; e.M[0x678D] = 0; e.M[0x678E] = 0; e.M[0x678B]=0;
        for (int t = 0; t < 25; t++) e.Tick();
        Console.Write($"trial {trial} pre x={e.M[0x6782]} y={e.M[0x6783]} lives={e.M[0x6786]} ");
        e.KeyMatrix[7] = 0x08; e.KeyMatrix[5] = 0x80;
        bool jumped = false;
        for (int t = 0; t < 100; t++) { e.Tick(); if (e.M[0x678D] != 0) jumped = true; }
        Console.WriteLine($"jumped={jumped} x={e.M[0x6782]} y={e.M[0x6783]} lives={e.M[0x6786]}");
    }
}
if (mode == "jump")
{
    int lv = int.Parse(Environment.GetEnvironmentVariable("LV") ?? "2");
    using var e = new Engine(assets) { TestLevel = assets.Levels[lv] };
    e.Start();
    for (int t = 0; t < 200; t++) e.Tick();
    for (int trial = 0; trial < 60; trial++)
    {
        // walk right for a while, then hold right+jump for 2 frames
        Array.Clear(e.KeyMatrix); e.KeyMatrix[0] = 2;
        for (int t = 0; t < 25 * (trial % 7 + 1); t++) e.Tick();
        int x = e.M[0x6782], y = e.M[0x6783], fl = e.M[0x678E], st = e.M[0x678D];
        e.KeyMatrix[0] = 2; e.KeyMatrix[5] = 0x80;
        bool jumped = false;
        for (int t = 0; t < 50; t++) { e.Tick(); if (e.M[0x678D] != 0) jumped = true; }
        Console.WriteLine($"trial {trial} x={x} y={y} floorflags={fl} wasJumping={st} jumped={jumped}");
        Array.Clear(e.KeyMatrix);
        for (int t = 0; t < 200; t++) e.Tick();
    }
}
if (mode == "preview")
{
    var e = new Engine(assets);
    var px = new uint[Cpc.Width * Cpc.Height];
    for (int i = 0; i < assets.Levels.Count; i++)
    {
        e.RenderPreview(assets.Levels[i], px);
        using var f = File.Create($"/tmp/prev_{i}.ppm");
        var hdr = System.Text.Encoding.ASCII.GetBytes($"P6\n{Cpc.Width} {Cpc.Height}\n255\n");
        f.Write(hdr);
        foreach (var v in px) { f.WriteByte((byte)(v >> 16)); f.WriteByte((byte)(v >> 8)); f.WriteByte((byte)v); }
    }
}

if (mode == "parity")
{
    // Run the original-logic engine (Core) and the clean Remake side by side with identical input and compare every tick.
    var data = ManicMiner.Remake.GameData.Load(Path.Combine(root, "Assets"));
    int Parity(string name, Func<Engine> mkCore, Func<ManicMiner.Remake.MinerGame> mkRemake, int ticks, int seed, int keyEvery, bool sparse)
    {
        using var c = mkCore(); using var r = mkRemake();
        c.Start(); r.Start();
        var rng = new Random(seed);
        var pc = new uint[Cpc.Width * Cpc.Height]; var pr = new uint[pc.Length];
        for (int t = 0; t < ticks; t++)
        {
            if (t % keyEvery == 0)
                for (int i = 0; i < 10; i++)
                {
                    byte v = sparse ? (byte)(rng.Next(3) == 0 ? (1 << rng.Next(8)) & rng.Next(256) : 0) : (byte)(rng.Next(4) == 0 ? rng.Next(256) : 0xFF);
                    if (Environment.GetEnvironmentVariable("KEYS") == "none") v = 0;
                    c.KeyMatrix[i] = r.KeyMatrix[i] = v;
                }
            c.LatchKeys(); r.LatchKeys();
            c.Tick(); r.Tick();
            c.RenderScreen(pc); r.RenderScreen(pr);
            int bad = 0, first = -1;
            for (int i = 0; i < pc.Length; i++) if (pc[i] != pr[i]) { bad++; if (first < 0) first = i; }
            if (bad != 0 || c.TestPlayEnded != r.TestPlayEnded)
            {
                Console.WriteLine($"{name}: DIVERGED at tick {t}: {bad} pixels differ, first at ({first % Cpc.Width},{first / Cpc.Width}); ended core={c.TestPlayEnded} remake={r.TestPlayEnded}");
                int sd = 0;
                for (int y = 0; y < Cpc.Height; y++) { int a = Cpc.ScreenAddr(y); for (int x = 0; x < 64; x++) if (c.M[a + x] != r.Screen[a + x]) { if (sd++ < 6) Console.WriteLine($"   y={y} bx={x} core={c.M[a + x]:X2} remake={r.Screen[a + x]:X2}"); } }
                Console.WriteLine($"   core: frame={c.M[0x67AC]} willy=({c.M[0x6782]},{c.M[0x6783]}) lives={c.M[0x6786]} air={c.M[0x6A44]}  screenOn={c.ScreenOn}/{r.ScreenOn} border={c.BorderInk}/{r.BorderInk}");
                return 1;
            }
            if (c.TestPlayEnded) break;
        }
        Console.WriteLine($"{name}: identical for {ticks} ticks");
        return 0;
    }
    int fails = 0;
    string only = Environment.GetEnvironmentVariable("ONLY") ?? "";
    int ticks = int.Parse(Environment.GetEnvironmentVariable("TICKS") ?? "4000");
    if (only is "" or "menu") fails += Parity("menu", () => new Engine(assets), () => new ManicMiner.Remake.MinerGame(data), 3000, 1, 50, true);
    for (int i = 0; i < assets.Levels.Count; i++)
    {
        if (only != "" && only != "L" && only != "L" + i) continue;
        int li = i;
        for (int seed = 1; seed <= 2; seed++)
            fails += Parity($"level {i} seed {seed}", () => new Engine(assets) { TestLevel = assets.Levels[li] }, () => new ManicMiner.Remake.MinerGame(data) { TestLevel = data.Levels[li] }, ticks, seed, seed == 1 ? 40 : 120, seed == 2);
    }
    if (only is "" or "game") fails += Parity("full game", () => new Engine(assets), () => new ManicMiner.Remake.MinerGame(data), int.Parse(Environment.GetEnvironmentVariable("GAMETICKS") ?? "60000"), 3, 150, true);
    Console.WriteLine(fails == 0 ? "PARITY OK" : $"PARITY FAILED ({fails})");
}

if (mode == "pack")
{
    var data = ManicMiner.Remake.GameData.Load(Path.Combine(root, "Assets"));
    var pack = new ManicMiner.Remake.ExpansionPack { Name = "Test Pack", Levels = data.Levels.Skip(3).Take(4).Select(l => l.Clone()).ToList() };
    string path = Path.Combine(Path.GetTempPath(), "test" + ManicMiner.Remake.ExpansionPack.Extension);
    pack.Save(path);
    var back = ManicMiner.Remake.ExpansionPack.Load(path);
    bool same = back.Levels.Count == 4 && back.Name == "Test Pack" && Enumerable.Range(0, 4).All(i => back.Levels[i].ToJson() == pack.Levels[i].ToJson());
    using var g = new ManicMiner.Remake.MinerGame(data, back.Levels);
    g.Start();
    var rng = new Random(5);
    for (int t = 0; t < 30000; t++) { if (t % 30 == 0) for (int i = 0; i < 10; i++) g.KeyMatrix[i] = (byte)(rng.Next(4) == 0 ? rng.Next(256) : 0); g.Tick(); }
    Console.WriteLine($"pack round-trip {(same ? "OK" : "MISMATCH")}; game with pack ran, running={g.Running}");
    File.Delete(path);
}

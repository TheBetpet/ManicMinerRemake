using ManicMiner.Remake;
using Xunit;

namespace ManicMiner.Tests;

public class PackTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "mmtests_" + Guid.NewGuid().ToString("N"));
    public PackTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    static ExpansionPack Sample(int n = 4) => new()
    {
        Name = "Test Pack", Author = "Me", Description = "desc",
        Levels = TestAssets.Data.Levels.Skip(2).Take(n).Select(l => l.Clone()).ToList()
    };

    [Fact]
    public void SaveThenLoadPreservesEverything()
    {
        string path = Path.Combine(_dir, "a" + ExpansionPack.Extension);
        var p = Sample();
        p.Save(path);
        var q = ExpansionPack.Load(path);
        Assert.Equal("Test Pack", q.Name);
        Assert.Equal("Me", q.Author);
        Assert.Equal("desc", q.Description);
        Assert.Equal(p.Levels.Select(l => l.ToJson()), q.Levels.Select(l => l.ToJson()));
    }

    [Fact]
    public void SavingAnEmptyPackFails()
    {
        Assert.Throws<InvalidOperationException>(() => new ExpansionPack { Name = "x" }.Save(Path.Combine(_dir, "e.mmpack")));
    }

    [Fact]
    public void LoadingAFileWithNoCavernsFails()
    {
        string path = Path.Combine(_dir, "empty.mmpack");
        File.WriteAllText(path, "{\"Name\":\"x\",\"Levels\":[]}");
        Assert.Throws<InvalidDataException>(() => ExpansionPack.Load(path));
    }

    [Fact]
    public void LoadingGarbageFails()
    {
        string path = Path.Combine(_dir, "bad.mmpack");
        File.WriteAllText(path, "not json");
        Assert.ThrowsAny<Exception>(() => ExpansionPack.Load(path));
    }

    [Fact]
    public void SaveCreatesMissingFolders()
    {
        string path = Path.Combine(_dir, "x", "y", "z.mmpack");
        Sample(1).Save(path);
        Assert.True(File.Exists(path));
    }

    [Theory]
    [InlineData("My Expansion", "My_Expansion.mmpack")]
    [InlineData("  ", "pack.mmpack")]
    [InlineData("a/b:c", "a_b_c.mmpack")]
    public void FileNamesAreSanitised(string name, string expected)
    {
        Assert.Equal(expected, ExpansionPack.FileNameFor(name));
    }

    [Fact]
    public void GameCanPlayFromAPack()
    {
        string path = Path.Combine(_dir, "g.mmpack");
        Sample().Save(path);
        using var g = new MinerGame(TestAssets.Data, ExpansionPack.Load(path).Levels);
        Assert.Equal(4, g.Levels.Count);
        g.Start();
        g.Advance(3000);
        Assert.True(g.Running);
    }
}

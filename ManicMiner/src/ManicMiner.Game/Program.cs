using ManicMiner.Game;

string? levelFile = null, levelsDir = null, packFile = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--level") levelFile = args[i + 1];
    if (args[i] == "--levels") levelsDir = args[i + 1];
    if (args[i] == "--pack") packFile = args[i + 1];
}
using var game = new Game1(levelFile, levelsDir, packFile);
game.Run();

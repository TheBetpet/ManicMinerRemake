using Microsoft.Xna.Framework.Input;

namespace ManicMiner.Game;

/// <summary>Maps PC keys to the CPC keyboard matrix (line, bit). The game's own key masks decide what each key does.</summary>
public static class CpcKeyboard
{
    static readonly Dictionary<Keys, (int line, int bit)> Map = new()
    {
        [Keys.Up] = (0, 0), [Keys.Right] = (0, 1), [Keys.Down] = (0, 2),
        [Keys.Left] = (1, 0), [Keys.Insert] = (1, 1),
        [Keys.OemOpenBrackets] = (2, 1), [Keys.Enter] = (2, 2), [Keys.OemCloseBrackets] = (2, 3),
        [Keys.LeftShift] = (2, 5), [Keys.RightShift] = (2, 5), [Keys.OemPipe] = (2, 6), [Keys.LeftControl] = (2, 7), [Keys.RightControl] = (2, 7),
        [Keys.OemPlus] = (3, 0), [Keys.OemMinus] = (3, 1), [Keys.P] = (3, 3), [Keys.OemSemicolon] = (3, 4),
        [Keys.OemQuotes] = (3, 5), [Keys.OemQuestion] = (3, 6), [Keys.OemPeriod] = (3, 7),
        [Keys.D0] = (4, 0), [Keys.D9] = (4, 1), [Keys.O] = (4, 2), [Keys.I] = (4, 3), [Keys.L] = (4, 4), [Keys.K] = (4, 5), [Keys.M] = (4, 6), [Keys.OemComma] = (4, 7),
        [Keys.D8] = (5, 0), [Keys.D7] = (5, 1), [Keys.U] = (5, 2), [Keys.Y] = (5, 3), [Keys.H] = (5, 4), [Keys.J] = (5, 5), [Keys.N] = (5, 6), [Keys.Space] = (5, 7),
        [Keys.D6] = (6, 0), [Keys.D5] = (6, 1), [Keys.R] = (6, 2), [Keys.T] = (6, 3), [Keys.G] = (6, 4), [Keys.F] = (6, 5), [Keys.B] = (6, 6), [Keys.V] = (6, 7),
        [Keys.D4] = (7, 0), [Keys.D3] = (7, 1), [Keys.E] = (7, 2), [Keys.W] = (7, 3), [Keys.S] = (7, 4), [Keys.D] = (7, 5), [Keys.C] = (7, 6), [Keys.X] = (7, 7),
        [Keys.D1] = (8, 0), [Keys.D2] = (8, 1), [Keys.Q] = (8, 3), [Keys.Tab] = (8, 4), [Keys.A] = (8, 5), [Keys.CapsLock] = (8, 6), [Keys.Z] = (8, 7),
    };

    public static void Fill(KeyboardState ks, byte[] matrix)
    {
        Array.Clear(matrix);
        foreach (var k in ks.GetPressedKeys())
            if (Map.TryGetValue(k, out var m)) matrix[m.line] |= (byte)(1 << m.bit);
    }
}

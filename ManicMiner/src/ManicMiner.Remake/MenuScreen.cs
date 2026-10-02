using ManicMiner.Core;

namespace ManicMiner.Remake;

/// <summary>Draws a simple list menu into an ARGB frame using the CPC font (used for choosing a game / expansion).</summary>
public static class MenuScreen
{
    public static void Render(GameData data, uint[] argb, string title, IReadOnlyList<string> items, int selected, string footer)
    {
        uint bg = 0xFF000080, fg = 0xFFFFFF00, dim = 0xFFC0C0C0, sel = 0xFFFFFFFF;
        Array.Fill(argb, bg);
        Text(data, argb, title, (Cpc.Width - title.Length * 8) / 2, 12, fg);
        int visible = 14, first = Math.Clamp(selected - visible / 2, 0, Math.Max(0, items.Count - visible));
        for (int i = 0; i < visible && first + i < items.Count; i++)
        {
            int idx = first + i, y = 40 + i * 12;
            string s = (idx == selected ? "> " : "  ") + items[idx];
            if (s.Length > Cpc.Width / 8 - 1) s = s[..(Cpc.Width / 8 - 1)];
            Text(data, argb, s, 8, y, idx == selected ? sel : dim);
        }
        Text(data, argb, footer, 8, Cpc.Height - 16, fg);
    }

    static void Text(GameData d, uint[] argb, string s, int x, int y, uint colour)
    {
        foreach (char ch in s)
        {
            int c = ch < 32 || ch > 126 ? '?' : ch;
            for (int r = 0; r < 8; r++)
            {
                int bits = d.Font[c * 8 + r];
                for (int b = 0; b < 8; b++)
                {
                    int px = x + b, py = y + r;
                    if ((bits & (0x80 >> b)) != 0 && px >= 0 && px < Cpc.Width && py >= 0 && py < Cpc.Height) argb[py * Cpc.Width + px] = colour;
                }
            }
            x += 8;
        }
    }
}

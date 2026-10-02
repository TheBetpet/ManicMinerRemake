using ManicMiner.Core;

namespace ManicMiner.Remake;

/// <summary>
/// The 16K CPC mode 1 video memory (four 2-bit pixels per byte, interleaved rows). Addresses are CPC video
/// addresses (0xC000..0xFFFF). The game draws sprites by XOR, and reads the pixels back for collision
/// detection, so the exact memory layout is part of its behaviour.
/// </summary>
public sealed class Screen
{
    public const int Base = 0xC000;
    public const int Width = Cpc.Width, Height = Cpc.Height;
    public readonly byte[] Mem = new byte[0x4000];
    readonly byte[] _font;

    public Screen(byte[] font) { _font = font; }

    public byte this[int addr]
    {
        get => Mem[addr & 0x3FFF];
        set => Mem[addr & 0x3FFF] = value;
    }

    public void Clear() => Array.Clear(Mem);

    /// <summary>Video address of the first byte of pixel row y.</summary>
    public static int RowAddr(int y) => Base + (y >> 3) * 64 + (y & 7) * 0x800;

    /// <summary>
    /// Row-address table lookup used by the sprite routines: entries 0..127 are pixel rows 0..127 and the
    /// entries after them continue at pixel row 152 (the HUD), so sprites that overhang the cavern spill there.
    /// </summary>
    public static int TableRow(int i) => RowAddr(i < 128 ? i : i + 24);

    /// <summary>Video address of pixel (x byte, y).</summary>
    public static int At(int xByte, int y) => RowAddr(y) + xByte;

    /// <summary>Draw characters (each 8 rows x 2 bytes, 4 pixels per byte) at a video address; <paramref name="xor"/> inverts them.</summary>
    public void Text(ReadOnlySpan<byte> chars, int addr, int xor)
    {
        foreach (byte ch in chars)
        {
            int a = addr;
            for (int row = 0; row < 8; row++)
            {
                int g = _font[ch * 8 + row];
                this[a] = (byte)(((g >> 4) * 0x11) ^ xor);
                this[a + 1] = (byte)(((g & 15) * 0x11) ^ xor);
                a += 0x800;
                if (a > 0xFFFF) a = (((a & 0xFFFF) | 0xC000) + 0x40) & 0xFFFF;   // into the next character row
            }
            addr += 2;
        }
    }

    /// <summary>Blit an 8x8 tile (16 bytes) at a video address.</summary>
    public void Tile(ReadOnlySpan<byte> gfx, int addr)
    {
        for (int r = 0; r < 8; r++)
        {
            this[addr] = gfx[r * 2];
            this[addr + 1] = gfx[r * 2 + 1];
            addr += 0x800;
        }
    }

    /// <summary>Fill the 8 pixel rows of one byte column with a value.</summary>
    public void Column(int addr, int value)
    {
        for (int i = 0; i < 8; i++) { this[addr] = (byte)value; addr += 0x800; }
    }

    /// <summary>Copy a 16 row x 4 byte block to (xByte, y).</summary>
    public void Block(ReadOnlySpan<byte> gfx, int xByte, int y)
    {
        for (int r = 0; r < 16; r++)
        {
            int a = TableRow(y + r) | xByte;
            for (int k = 0; k < 4; k++) this[a + k] = gfx[r * 4 + k];
        }
    }

    /// <summary>XOR a masked 16x16 sprite (4 bytes per row) at (xByte, y). Returns true if it touched pen-3 pixels.</summary>
    public bool Xor(ReadOnlySpan<byte> gfx, int xByte, int y, int mask, bool detect)
    {
        bool hit = false;
        y &= 0x7F;
        for (int r = 0; r < 16; r++)
        {
            int row = TableRow(y + r);
            int a = (row & 0xFF00) | ((row & 0xFF) | xByte);
            for (int k = 0; k < 4; k++, a++)
            {
                int s = gfx[r * 4 + k];
                if (detect && (s & this[a]) != 0)
                {
                    int px = this[a];
                    if (((px >> 4) * 0x11 & (px & 15) * 0x11) != 0) hit = true;
                }
                this[a] = (byte)((s & mask) ^ this[a]);
            }
        }
        return hit;
    }

    /// <summary>XOR an 8x8 tile-sized image (an item) at a video address.</summary>
    public void XorTile(ReadOnlySpan<byte> gfx, int addr, int mask)
    {
        for (int r = 0; r < 8; r++)
        {
            this[addr] = (byte)((gfx[r * 2] & mask) ^ this[addr]);
            this[addr + 1] = (byte)((gfx[r * 2 + 1] & mask) ^ this[addr + 1]);
            addr += 0x800;
        }
    }

    /// <summary>Convert to ARGB (Width x Height). Rows below the cavern use the HUD palette.</summary>
    public void Render(uint[] argb, int[] gamePens, int[] hudPens)
    {
        for (int y = 0; y < Height; y++)
        {
            var pal = y < 128 ? gamePens : hudPens;
            int a = RowAddr(y) - Base;
            for (int bx = 0; bx < 64; bx++)
            {
                byte b = Mem[a + bx];
                for (int p = 0; p < 4; p++)
                    argb[y * Width + bx * 4 + p] = 0xFF000000u | Cpc.Hw[pal[Cpc.Pixel(b, p)] & 31];
            }
        }
    }
}

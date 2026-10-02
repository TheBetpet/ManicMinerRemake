"""Extracts all assets from the Manic Miner CPC disk image into ManicMiner/Assets."""
import os, json, sys
sys.path.insert(0, os.path.dirname(__file__))
from gameimg import pristine_full
from render import HW, pix
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'ManicMiner', 'Assets')
ROM = '/usr/share/caprice32/rom/cpc464.rom'   # firmware font (not on the disk)
m = pristine_full()
w16 = lambda a: m[a] | m[a + 1] << 8

def pens(h): return [h[4], h[3], h[2], h[1]]  # pen0..pen3 from header bytes 16..19 -> see below

def level(i):
    p = w16(0x5320 + 2 * i)
    cells = []; a = p
    while len(cells) < 512:
        b = m[a]; a += 1; cells += [b & 15] * ((b >> 4) + 1)
    h = list(m[a:a + 20]); name = bytes(m[a + 20:a + 52]).decode('latin1'); g = a + 52
    n = m[g]
    guards = [dict(zip(['steps', 'reload', 'spriteSet', 'velocity', 'x', 'y', 'flags'], m[g + 1 + 7 * k:g + 8 + 7 * k])) for k in range(n)]
    return dict(index=i, name=name, border=h[15], aux=h[16], pen2=h[17], pen1=h[18], pen0=h[19],
                tileGfx=h[:15], layout=[cells[r * 32:(r + 1) * 32] for r in range(16)], guardians=guards,
                willyX=m[0x58E6 + 2 * i], willyY=m[0x58E7 + 2 * i], portalX=m[0x590E + 2 * i], portalY=m[0x590F + 2 * i],
                air=0xDF), g + 1 + 7 * n

levels = [level(i)[0] for i in range(20)]
json.dump(dict(levels=levels, specialHandlers={4: 'Eugene', 7: 'Kong', 11: 'Kong', 13: 'Skylab', 18: 'SolarGenerator', 19: 'FinalBarrier'}),
          open(os.path.join(OUT, 'levels.json'), 'w'), indent=1)

def draw(im, ox, oy, data, off, w_bytes, rows, pal, transparent0=False):
    for y in range(rows):
        for bx in range(w_bytes):
            b = data[off + y * w_bytes + bx]
            for p in range(4):
                c = pix(b, p)
                if transparent0 and c == 0: continue
                im.putpixel((ox + bx * 4 + p, oy + y), HW[pal[c]] + (255,))

PAL0 = [0x14, 0x12, 0x0C, 0x0B]
# sprite pool
open(os.path.join(OUT, 'sprites.bin'), 'wb').write(bytes(m[0x0100:0x3000]))
n = (0x3000 - 0x100) // 0x40
im = Image.new('RGBA', (16 * 16, ((n + 15) // 16) * 16), (0, 0, 0, 0))
for f in range(n): draw(im, (f % 16) * 16, (f // 16) * 16, m, 0x100 + f * 0x40, 4, 16, PAL0, True)
im.save(os.path.join(OUT, 'Sheets', 'sprites.png'))
# tiles
open(os.path.join(OUT, 'tiles.bin'), 'wb').write(bytes(m[0x5386:0x58E6]))
nt = (0x58E6 - 0x5386) // 16
im = Image.new('RGBA', (16 * 8, ((nt + 15) // 16) * 8), (0, 0, 0, 255))
for t in range(nt): draw(im, (t % 16) * 8, (t // 16) * 8, m, 0x5386 + t * 16, 2, 8, PAL0)
im.save(os.path.join(OUT, 'Sheets', 'tiles.png'))
# guardian tables
open(os.path.join(OUT, 'spriteSets.json'), 'w').write(json.dumps([w16(0x5348 + 2 * i) for i in range(31)]))
# font from CPC firmware ROM
rom = open(ROM, 'rb').read()
font = rom[0x3800:0x3800 + 256 * 8]
open(os.path.join(OUT, 'font.bin'), 'wb').write(font)
im = Image.new('RGBA', (16 * 8, 16 * 8), (0, 0, 0, 255))
for c in range(256):
    for y in range(8):
        for x in range(8):
            if font[c * 8 + y] >> (7 - x) & 1: im.putpixel(((c % 16) * 8 + x, (c // 16) * 8 + y), (255, 255, 255, 255))
im.save(os.path.join(OUT, 'Sheets', 'font.png'))
# raw tables needed by the engine
open(os.path.join(OUT, 'data_6900.bin'), 'wb').write(bytes(m[0x6900:0x6A80]))  # sound params, music, strings
open(os.path.join(OUT, 'data_80D1.bin'), 'wb').write(bytes(m[0x80D1:0x81C0]))  # title draw list/strings
# level previews
def preview(L):
    im = Image.new('RGB', (256, 128))
    pal = [L['pen0'], L['pen1'], L['pen2'], 0x0B]
    for r in range(16):
        for c in range(32):
            t = L['layout'][r][c]; g = L['tileGfx'][t] if t < 15 else 0
            for y in range(8):
                for bx in range(2):
                    b = m[0x5386 + g * 16 + y * 2 + bx]
                    for p in range(4): im.putpixel((c * 8 + bx * 4 + p, r * 8 + y), HW[pal[pix(b, p)]])
    return im
for L in levels: preview(L).resize((512, 256), 0).save(os.path.join(OUT, 'Previews', 'level%02d.png' % L['index']))
print('done', n, nt)

# engine tables (variables, strings, tune data) and title tables
open(os.path.join(OUT, 'data_6704.bin'), 'wb').write(bytes(m[0x6704:0x6E3F]))
open(os.path.join(OUT, 'data_80D1.bin'), 'wb').write(bytes(m[0x80D1:0x8200]))
os.remove(os.path.join(OUT, 'data_6900.bin'))
open(os.path.join(OUT, 'tables_5200.bin'), 'wb').write(bytes(m[0x5200:0x5386]))
open(os.path.join(OUT, 'data_A000.bin'), 'wb').write(bytes(m[0xA000:0xA500]))

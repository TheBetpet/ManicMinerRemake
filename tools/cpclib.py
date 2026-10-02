import struct, os
SRC = os.path.join(os.path.dirname(__file__), '..', 'Amstrad Source Media')

def load_sna(path):
    d = open(path, 'rb').read()
    ver = d[0x10]
    regs = d[0x11:0x11+0x2F]
    ram = bytearray(65536)
    hdr = d[:0x100]
    info = {'version': ver, 'ga_pen': hdr[0x2E], 'ga_pal': list(hdr[0x2F:0x40]), 'ga_mode': hdr[0x40],
            'crtc': list(hdr[0x43:0x43+18]), 'ram_cfg': hdr[0x41]}
    if ver >= 3:
        pos = 0x100; mem = bytearray()
        while pos + 8 <= len(d):
            tag = d[pos:pos+4]; ln = struct.unpack('<I', d[pos+4:pos+8])[0]; body = d[pos+8:pos+8+ln]; pos += 8+ln
            if tag.startswith(b'MEM'):
                bank = tag[3]-0x30
                if len(body) < 2: continue
                total = struct.unpack('<H', body[:2])[0] if False else None
                # chunk data: RLE if length < 65536
                if ln == 65536 and False: pass
                out = bytearray(); i = 0
                comp = ln != 0x10000
                if not comp: out = bytearray(body)
                else:
                    while i < len(body):
                        b = body[i]
                        if b == 0xE5:
                            n = body[i+1]; i += 2
                            if n == 0: out.append(0xE5)
                            else: out += bytes([body[i]])*n; i += 1
                        else: out.append(b); i += 1
                if bank == 0: ram = out + bytearray(65536 - len(out))
            info.setdefault('chunks', []).append((tag, ln))
    else:
        ram = bytearray(d[0x100:0x100+65536])
    info['regs'] = regs
    return bytes(ram[:65536]), info

def load_dsk(path):
    d = open(path, 'rb').read()
    ntr, nsd = d[0x30], d[0x31]
    ext = d.startswith(b'EXTENDED')
    sizes = list(d[0x34:0x34+ntr*nsd]) if ext else None
    pos = 0x100; tracks = {}
    for t in range(ntr*nsd):
        if ext:
            if sizes[t] == 0: continue
            tl = sizes[t]*256
        else: tl = struct.unpack('<H', d[0x32:0x34])[0]
        th = d[pos:pos+0x100]
        assert th.startswith(b'Track-Info'), (t, pos)
        trk, side, ssz, nsec = th[0x10], th[0x11], th[0x14], th[0x15]
        p = pos+0x100; secs = []
        for s in range(nsec):
            e = th[0x18+s*8:0x20+s*8]
            C,H,R,N,st1,st2,dl = e[0],e[1],e[2],e[3],e[4],e[5],struct.unpack('<H', e[6:8])[0]
            if dl == 0: dl = 128 << N
            secs.append((C,H,R,N,d[p:p+dl])); p += dl
        tracks[(trk, side)] = secs
        pos += tl
    return tracks

def dsk_image(tracks):
    """Return dict ((track,side,R)->data) ordered sectors as linear block stream."""
    blocks = bytearray()
    for k in sorted(tracks):
        for s in sorted(tracks[k], key=lambda s: s[2]): blocks += s[4]
    return bytes(blocks)

def dsk_files(tracks):
    img = dsk_image(tracks)  # 1024-byte blocks, dir = blocks 0,1
    out = {}
    for i in range(64):
        e = img[i*32:i*32+32]
        if e[0] == 0xE5: continue
        user = e[0]; name = e[1:9].decode('latin1').rstrip(' ')+'.'+bytes(b&0x7f for b in e[9:12]).decode('latin1').rstrip(' ')
        ext = e[12]; rc = e[15]; alloc = [b for b in e[16:32] if b]
        out.setdefault((user,name),[]).append((ext, rc, alloc))
    files = {}
    for k, exts in out.items():
        data = bytearray()
        for ext, rc, alloc in sorted(exts):
            chunk = b''.join(img[b*1024:(b+1)*1024] for b in alloc)
            data += chunk[:rc*128]
        files[k] = bytes(data)
    return files

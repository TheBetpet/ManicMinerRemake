import os
from cpclib import *
def pristine():
    b=open(os.path.join(os.path.dirname(__file__),'..','extracted','MANIC.BIN'),'rb').read()[128:]
    mem=bytearray(65536); mem[0x0FF0:0x0FF0+len(b)]=b
    hl,de=0x62FF,0xA4FF
    for _ in range(0x5400): mem[de]=mem[hl]; de-=1; hl-=1
    # 6E3F init
    mem[0x0100:0x0100+0x540]=mem[0x8200:0x8200+0x540]
    hl=0x8200+0x540; de=0x0100+0x540
    for _ in range(0x14E0):
        v=mem[hl]
        mem[de]=(v&0xF0)|(v>>4); de+=1
        mem[de]=(v&0x0F)*0x11; de+=1
        hl+=1
        # LDI copies (HL) AFTER RLD changed (HL): (HL)=(v<<4|A&0xF) with A=v
    return mem
def pristine_full():
    mem=pristine()
    for a in range(0x2B00,0x3000): mem[a]^=0x0F
    return mem

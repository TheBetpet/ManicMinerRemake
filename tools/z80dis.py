R8=['B','C','D','E','H','L','(HL)','A']; RP=['BC','DE','HL','SP']; RP2=['BC','DE','HL','AF']
CC=['NZ','Z','NC','C','PO','PE','P','M']; ALU=['ADD A,','ADC A,','SUB ','SBC A,','AND ','XOR ','OR ','CP ']
ROT=['RLC','RRC','RL','RR','SLA','SRA','SLL','SRL']
def dis(m,pc):
    """returns (text,length)"""
    p=pc; pre=None
    def b():
        nonlocal p; v=m[p&0xFFFF]; p+=1; return v
    def w():
        lo=b(); hi=b(); return lo|hi<<8
    def s8(): 
        v=b(); return v-256 if v>127 else v
    op=b()
    ix=None
    if op in(0xDD,0xFD):
        ix='IX' if op==0xDD else 'IY'; op=b()
        if op in (0xDD,0xFD,0xED): return ('db 0x%02X'%m[pc],1)
    def hl(): return ix or 'HL'
    def mem():
        if ix:
            d=s8(); return '(%s%+d)'%(ix,d)
        return '(HL)'
    def r(i):
        if i==6: return mem()
        if ix and i==4: return ix+'H'
        if ix and i==5: return ix+'L'
        return R8[i]
    def rp(i): return hl() if i==2 else RP[i]
    x=op>>6;y=(op>>3)&7;z=op&7;q=y&1;pp=y>>1
    if op==0xCB:
        if ix:
            d=s8(); op=b(); x=op>>6;y=(op>>3)&7;z=op&7
            t='(%s%+d)'%(ix,d)
            if x==0: s='%s %s'%(ROT[y],t)
            else: s='%s %d,%s'%(['','BIT','RES','SET'][x],y,t)
            if z!=6 and x!=1: s+=',%s'%R8[z]
            return (s,p-pc)
        op=b(); x=op>>6;y=(op>>3)&7;z=op&7
        if x==0: s='%s %s'%(ROT[y],R8[z])
        else: s='%s %d,%s'%(['','BIT','RES','SET'][x],y,R8[z])
        return (s,p-pc)
    if op==0xED:
        op=b(); x=op>>6;y=(op>>3)&7;z=op&7;q=y&1;pp=y>>1
        if x==1:
            if z==0: s='IN %s,(C)'%('F' if y==6 else R8[y])
            elif z==1: s='OUT (C),%s'%('0' if y==6 else R8[y])
            elif z==2: s='%s HL,%s'%('SBC' if q==0 else 'ADC',RP[pp])
            elif z==3:
                a=w(); s='LD (0x%04X),%s'%(a,RP[pp]) if q==0 else 'LD %s,(0x%04X)'%(RP[pp],a)
            elif z==4: s='NEG'
            elif z==5: s='RETN' if y!=1 else 'RETI'
            elif z==6: s='IM %s'%['0','0','1','2','0','0','1','2'][y]
            else: s=['LD I,A','LD R,A','LD A,I','LD A,R','RRD','RLD','NOP','NOP'][y]
            return (s,p-pc)
        if x==2 and z<4 and y>=4:
            return (['LDI','CPI','INI','OUTI','LDD','CPD','IND','OUTD','LDIR','CPIR','INIR','OTIR','LDDR','CPDR','INDR','OTDR'][(y-4)*4+z] if False else
              [['LDI','CPI','INI','OUTI'],['LDD','CPD','IND','OUTD'],['LDIR','CPIR','INIR','OTIR'],['LDDR','CPDR','INDR','OTDR']][y-4][z],p-pc)
        return ('db 0xED,0x%02X'%op,2)
    if x==0:
        if z==0:
            if y==0: s='NOP'
            elif y==1: s="EX AF,AF'"
            else:
                d=s8(); t=(p+d)&0xFFFF
                s='DJNZ 0x%04X'%t if y==2 else 'JR 0x%04X'%t if y==3 else 'JR %s,0x%04X'%(CC[y-4],t)
        elif z==1:
            if q==0: s='LD %s,0x%04X'%(rp(pp),w())
            else: s='ADD %s,%s'%(hl(),rp(pp))
        elif z==2:
            if y==0: s='LD (BC),A'
            elif y==1: s='LD A,(BC)'
            elif y==2: s='LD (DE),A'
            elif y==3: s='LD A,(DE)'
            elif y==4: s='LD (0x%04X),%s'%(w(),hl())
            elif y==5: s='LD %s,(0x%04X)'%(hl(),w())
            elif y==6: s='LD (0x%04X),A'%w()
            else: s='LD A,(0x%04X)'%w()
        elif z==3: s='%s %s'%('INC' if q==0 else 'DEC',rp(pp))
        elif z==4: t=r(y); s='INC %s'%t
        elif z==5: t=r(y); s='DEC %s'%t
        elif z==6:
            t=r(y); s='LD %s,0x%02X'%(t,b())
        else: s=['RLCA','RRCA','RLA','RRA','DAA','CPL','SCF','CCF'][y]
    elif x==1:
        if op==0x76: s='HALT'
        else:
            if ix and (y==6 or z==6):
                if y==6: s='LD %s,%s'%(mem(),R8[z])
                else: s='LD %s,%s'%(R8[y],mem())
            else: s='LD %s,%s'%(r(y),r(z))
    elif x==2: s='%s%s'%(ALU[y],r(z))
    else:
        if z==0: s='RET %s'%CC[y]
        elif z==1:
            if q==0: s='POP %s'%(hl() if pp==2 else RP2[pp])
            else: s=['RET','EXX','JP %s'%hl() if not ix else 'JP (%s)'%ix,'LD SP,%s'%hl()][pp]
            if q==1 and pp==2 and not ix: s='JP (HL)'
        elif z==2: s='JP %s,0x%04X'%(CC[y],w())
        elif z==3:
            if y==0: s='JP 0x%04X'%w()
            elif y==2: s='OUT (0x%02X),A'%b()
            elif y==3: s='IN A,(0x%02X)'%b()
            elif y==4: s='EX (SP),%s'%hl()
            elif y==5: s='EX DE,HL'
            elif y==6: s='DI'
            else: s='EI'
        elif z==4: s='CALL %s,0x%04X'%(CC[y],w())
        elif z==5:
            if q==0: s='PUSH %s'%(hl() if pp==2 else RP2[pp])
            else: s='CALL 0x%04X'%w()
        elif z==6: s='%s0x%02X'%(ALU[y],b())
        else: s='RST 0x%02X'%(y*8)
    return (s,p-pc)
def listing(m,start,end):
    pc=start;out=[]
    while pc<end:
        t,l=dis(m,pc); out.append('%04X  %-12s %s'%(pc,m[pc:pc+l].hex(),t)); pc+=l
    return out

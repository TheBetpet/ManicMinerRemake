import sys,re;sys.path.insert(0,'.')
from z80dis import dis
m=open('/tmp/pristine.bin','rb').read()
# snapshot has runtime-patched code too; use pristine
seen={};work=[0x6E3F]
extra=[int(x,16) for x in sys.argv[1:]]
work+=extra
while work:
    a=work.pop()
    while 0<=a<0xFFFF and a not in seen:
        t,l=dis(m,a); seen[a]=(t,l)
        mm=re.match(r'(JP|CALL|JR|DJNZ)\s+(?:\w+,)?0x([0-9A-F]{4})$',t)
        if mm and not (t.startswith('JP (')):
            tgt=int(mm.group(2),16)
            if tgt>=0x5100 and tgt<0xA500 or tgt<0x5100 and tgt>=0x100: work.append(tgt)
        if t.startswith(('JP 0x','RET','JR 0x','HALT','JP (')) and not ',' in t.split(' ')[1:2].__str__() : 
            if t.startswith('RET') and t!='RET': a+=l; continue
            break
        if t.startswith('RETI') or t.startswith('RETN'): break
        a+=l
cov=sorted(seen)
# print ranges
rs=[];s=cov[0];p=cov[0]
for a in cov[1:]:
    if a!=p+seen[p][1]: rs.append((s,p+seen[p][1]));s=a
    p=a
rs.append((s,p+seen[p][1]))
for r in rs: print('%04X-%04X'%r)
import pickle;pickle.dump(seen,open('/tmp/seen.pkl','wb'))

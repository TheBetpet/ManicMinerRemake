import pickle,re
seen=pickle.load(open('/tmp/seen.pkl','rb'))
m=open('/tmp/pristine.bin','rb').read()
labels=set()
for a,(t,l) in seen.items():
    mm=re.search(r'0x([0-9A-F]{4})',t)
    if mm and t.split()[0] in('JP','CALL','JR','DJNZ'): labels.add(int(mm.group(1),16))
out=[];a=0x6E3F
while a<0x8200:
    if a in seen:
        t,l=seen[a]
        out.append('%s%04X  %-10s %s'%('L' if a in labels else ' ',a,m[a:a+l].hex(),t)); a+=l
    else:
        b=a
        while b<0x8200 and b not in seen and b-a<16: b+=1
        out.append(' %04X  DATA %s   |%s|'%(a,m[a:b].hex(),''.join(chr(c) if 32<=c<127 else '.' for c in m[a:b])));a=b
open('/tmp/code.asm','w').write('\n'.join(out))
print(len(out))

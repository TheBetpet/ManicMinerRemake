import sys
sys.path.insert(0,'/home/sgodrich/RiderProjects/Manic Miner/tools')
from z80dis import dis
m=open('/tmp/pristine.bin','rb').read()
def d(a,b):
    pc=a
    while pc<b:
        r=dis(m,pc)
        n,t=(r[0],r[1]) if isinstance(r[0],int) else (r[1],r[0])
        print('%04X %-14s %s'%(pc,m[pc:pc+n].hex(),t)); pc+=n
if __name__=='__main__': d(int(sys.argv[1],16),int(sys.argv[2],16))

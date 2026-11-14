import json,struct,sys
def boxes(f):
    b=open(f,'rb').read(); n=struct.unpack('<I',b[12:16])[0]; j=json.loads(b[20:20+n])
    off=20+n; blen=struct.unpack('<I',b[off:off+4])[0]; bin_=b[off+8:off+8+blen]
    out=[]
    for prim in j['meshes'][0]['primitives']:
        a=j['accessors'][prim['attributes']['POSITION']]; bv=j['bufferViews'][a['bufferView']]
        st=bv.get('byteOffset',0)+a.get('byteOffset',0); cnt=a['count']
        pts=[struct.unpack('<3f',bin_[st+i*12:st+i*12+12]) for i in range(cnt)]
        # vertices de caixas: 24 por caixa
        k=24 if cnt%24==0 else 8
        for i in range(0,cnt,k):
            ch=pts[i:i+k]; mn=[min(p[c] for p in ch) for c in range(3)]; mx=[max(p[c] for p in ch) for c in range(3)]
            out.append((mn,mx))
    return out
if __name__=='__main__':
    for mn,mx in boxes(sys.argv[1]): print(' '.join(f'{v:6.2f}' for v in mn+mx))

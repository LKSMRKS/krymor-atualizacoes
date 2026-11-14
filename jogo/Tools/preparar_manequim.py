#!/usr/bin/env python3
"""
Deixa o manequim (Meshy + Mixamo) leve para o jogo, sem mexer no esqueleto nem nas animações:
- reduz a malha (UV preservado) e copia os pesos de cada vértice novo do vértice original mais próximo;
- reduz as texturas (cor 2048, normal e metal/rugosidade 1024);
- regrava o .glb só com os dados usados (ossos, animações, pele);
- grava em COLOR_0 a "máscara de roupa": R = região do corpo (osso dominante, ver REGIOES), G = altura na pose de
  referência (0..1 de 1,7 m), B = lado (0 = direita do boneco, 1 = esquerda). O shader de roupa do cliente
  (MannequinVisual) usa isso para vestir camadas que acompanham a animação.
Uso: python3 Tools/preparar_manequim.py entrada.glb saida.glb [triangulos=24000]
"""
import io, json, os, struct, sys, tempfile
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rig_dragao import load_glb, accessor, decimate, normals  # mesmas rotinas do draco

COMP = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}
NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def raw_accessor(j, b, i):
    a = j['accessors'][i]; bv = j['bufferViews'][a['bufferView']]
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    n = NCOMP[a['type']]
    arr = np.frombuffer(b, dtype=COMP[a['componentType']], count=a['count'] * n, offset=off)
    return arr.reshape(a['count'], n) if n > 1 else arr


# região por osso: 0 cabeça, 1 peito/ombros, 2 barriga/quadril, 3 braço, 4 antebraço, 5 mão, 6 coxa, 7 canela, 8 pé
REGIOES = {'Hips': 2, 'Spine': 2, 'Spine1': 1, 'Spine2': 1, 'Neck': 0, 'Head': 0, 'HeadTop_End': 0, 'headfront': 0,
           'Shoulder': 1, 'Arm': 3, 'ForeArm': 4, 'Hand': 5, 'UpLeg': 6, 'Leg': 7, 'Foot': 8, 'ToeBase': 8}


def region_of(name):
    n = name.split(':')[-1]
    for side in ('Left', 'Right'):
        if n.startswith(side): n = n[len(side):]
    return REGIOES.get(n, 2)


def nearest(src, dst, chunk=2048):
    out = np.empty(len(dst), np.int64)
    s2 = (src ** 2).sum(1)
    for k in range(0, len(dst), chunk):
        d = dst[k:k + chunk]
        dist = s2[None, :] - 2 * d @ src.T + (d ** 2).sum(1)[:, None]
        out[k:k + chunk] = dist.argmin(1)
    return out


def main():
    src, out = sys.argv[1], sys.argv[2]
    target = int(sys.argv[3]) if len(sys.argv) > 3 else 24000
    j, b = load_glb(src)
    pr = j['meshes'][0]['primitives'][0]
    P = accessor(j, b, pr['attributes']['POSITION']).astype(np.float32)
    UV = accessor(j, b, pr['attributes']['TEXCOORD_0']).astype(np.float32)
    F = accessor(j, b, pr['indices']).reshape(-1, 3).astype(np.int64)
    J0 = raw_accessor(j, b, pr['attributes']['JOINTS_0']).astype(np.uint16)
    W0 = raw_accessor(j, b, pr['attributes']['WEIGHTS_0']).astype(np.float32)
    print('original', len(P), 'vértices', len(F), 'triângulos')
    P2, UV2, F2 = decimate(P, UV, F, target)
    near = nearest(P, P2)
    J2 = J0[near]; W2 = W0[near]
    W2 = W2 / np.maximum(W2.sum(1, keepdims=True), 1e-6)
    N2 = normals(P2, F2)
    names = [j['nodes'][i]['name'] for i in j['skins'][0]['joints']]
    dom = J2[np.arange(len(J2)), W2.argmax(1)]
    reg = np.array([region_of(names[k]) for k in dom], np.float32)
    height = np.clip(P2[:, 1] / max(P2[:, 1].max(), 1e-6), 0, 1)
    side = np.clip(0.5 + P2[:, 0] / 1.7, 0, 1)
    COL = np.stack([reg / 15.0, height, side, np.ones(len(P2))], 1).astype(np.float32)
    print('reduzido', len(P2), 'vértices', len(F2), 'triângulos')

    # ---- regrava: novo binário só com o que é usado
    newbin = bytearray(); views = []; accs = []

    def put_bytes(data, target_=None):
        while len(newbin) % 4: newbin.append(0)
        off = len(newbin); newbin.extend(data)
        v = {'buffer': 0, 'byteOffset': off, 'byteLength': len(data)}
        if target_: v['target'] = target_
        views.append(v); return len(views) - 1

    def put(arr, typ, comp, target_=None, minmax=False):
        arr = np.ascontiguousarray(arr)
        a = {'bufferView': put_bytes(arr.tobytes(), target_), 'componentType': comp, 'count': len(arr), 'type': typ}
        if minmax: a['min'] = arr.min(0).tolist(); a['max'] = arr.max(0).tolist()
        accs.append(a); return len(accs) - 1

    def copy_accessor(i):
        a = dict(j['accessors'][i]); arr = raw_accessor(j, b, i)
        bv = put_bytes(np.ascontiguousarray(arr).tobytes())
        a['bufferView'] = bv; a.pop('byteOffset', None); accs.append(a); return len(accs) - 1

    prim = {'attributes': {
        'POSITION': put(P2, 'VEC3', 5126, 34962, True), 'NORMAL': put(N2, 'VEC3', 5126, 34962),
        'TEXCOORD_0': put(UV2, 'VEC2', 5126, 34962), 'JOINTS_0': put(J2, 'VEC4', 5123, 34962),
        'WEIGHTS_0': put(W2.astype(np.float32), 'VEC4', 5126, 34962), 'COLOR_0': put(COL, 'VEC4', 5126, 34962)},
        'indices': put(F2.reshape(-1).astype(np.uint32), 'SCALAR', 5125, 34963), 'material': pr.get('material', 0)}
    j['meshes'][0]['primitives'] = [prim]
    for s in j.get('skins', []):
        if 'inverseBindMatrices' in s: s['inverseBindMatrices'] = copy_accessor(s['inverseBindMatrices'])
    for an in j.get('animations', []):
        for sm in an['samplers']:
            sm['input'] = copy_accessor(sm['input']); sm['output'] = copy_accessor(sm['output'])
    mat = j['materials'][0]
    sizes = {}
    base = mat['pbrMetallicRoughness']['baseColorTexture']['index']
    for tex_i, tex in enumerate(j['textures']):
        sizes[tex['source']] = 2048 if tex_i == base else 1024
    new_images = []
    for i, im in enumerate(j['images']):
        bv = j['bufferViews'][im['bufferView']]
        img = Image.open(io.BytesIO(b[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']])).convert('RGB')
        img = img.resize((sizes.get(i, 1024),) * 2, Image.LANCZOS)
        buf = io.BytesIO(); img.save(buf, 'JPEG', quality=88)
        new_images.append({'bufferView': put_bytes(buf.getvalue()), 'mimeType': 'image/jpeg', 'name': im.get('name', f'img{i}')})
    j['images'] = new_images
    j['accessors'] = accs; j['bufferViews'] = views; j['buffers'] = [{'byteLength': len(newbin)}]
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(newbin) % 4: newbin.append(0)
    with open(out, 'wb') as f:
        f.write(struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(newbin)))
        f.write(struct.pack('<II', len(js), 0x4E4F534A)); f.write(js)
        f.write(struct.pack('<II', len(newbin), 0x004E4942)); f.write(newbin)
    print('gravado', out, os.path.getsize(out) // 1024, 'KB')


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""
Junta animações exportadas do Meshy (ou Mixamo, em .glb) num arquivo leve para o jogo: tira malha, pele, texturas e
canais de escala; mantém só a árvore de ossos e as animações (rotação de todos os ossos + posição do quadril).
O jogo lê o arquivo em tempo de execução e aplica as rotações no manequim (mesmo esqueleto do Meshy/Mixamo).

Uso (raiz do projeto):
  python3 Tools/importar_animacoes.py Client/Assets/Models/anim_guerreiro.glb nome1=arquivo1.glb nome2=arquivo2.glb ...
  Arquivo com várias animações (Mixamo "All animation"): nome=arquivo.glb#NomeDaAnimacao
  (10/11: anim_soco.glb = soco=Meshy_..._All_animation_1.glb#Punch_Combo)
"""
import json, struct, sys
import numpy as np

COMP = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def load(path):
    b = open(path, 'rb').read()
    l = struct.unpack('<I', b[12:16])[0]
    j = json.loads(b[20:20 + l])
    bl = struct.unpack('<I', b[20 + l:24 + l])[0]
    return j, b[28 + l:28 + l + bl]


def accessor(j, binary, i):
    a = j['accessors'][i]
    bv = j['bufferViews'][a['bufferView']]
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    n = NC[a['type']]
    arr = np.frombuffer(binary, dtype=COMP[a['componentType']], count=a['count'] * n, offset=off)
    return (arr.reshape(a['count'], n) if n > 1 else arr).astype(np.float32)


def main():
    out = sys.argv[1]
    pairs = [a.split('=', 1) for a in sys.argv[2:]]
    base_j = None
    blob = bytearray()
    views, accs, anims = [], [], []

    def add(arr, typ, minmax=False):
        arr = np.ascontiguousarray(arr, dtype=np.float32)
        while len(blob) % 4: blob.append(0)
        off = len(blob)
        blob.extend(arr.tobytes())
        views.append({'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes})
        a = {'bufferView': len(views) - 1, 'componentType': 5126, 'count': int(arr.shape[0]), 'type': typ}
        if minmax:
            a['min'] = [float(arr.min())]; a['max'] = [float(arr.max())]
        accs.append(a)
        return len(accs) - 1

    for name, path in pairs:
        path, _, wanted = path.partition('#')
        j, binary = load(path)
        if base_j is None:
            base_j = j
        an = next(a for a in j['animations'] if a.get('name') == wanted) if wanted else j['animations'][0]
        hips = next(i for i, n in enumerate(j['nodes']) if n.get('name', '').split(':')[-1] == 'Hips')
        channels, samplers = [], []
        for c in an['channels']:
            node, prop = c['target']['node'], c['target']['path']
            if prop == 'scale' or (prop == 'translation' and node != hips):
                continue
            s = an['samplers'][c['sampler']]
            t = accessor(j, binary, s['input'])
            v = accessor(j, binary, s['output'])
            if prop == 'translation':
                # o servidor é quem anda: tira o deslocamento horizontal do quadril (fica só o sobe-desce e o balanço)
                v = v.copy()
                v[:, 0] = v[0, 0]
                v[:, 2] = v[0, 2]
            ti = add(t, 'SCALAR', minmax=True)
            vi = add(v, 'VEC4' if prop == 'rotation' else 'VEC3')
            samplers.append({'input': ti, 'output': vi, 'interpolation': s.get('interpolation', 'LINEAR')})
            channels.append({'sampler': len(samplers) - 1, 'target': {'node': node, 'path': prop}})
        anims.append({'name': name, 'channels': channels, 'samplers': samplers})
        print(f'{name}: {len(channels)} canais, {float(accessor(j, binary, an["samplers"][0]["input"])[-1]):.2f} s')

    nodes = []
    for n in base_j['nodes']:
        m = {k: v for k, v in n.items() if k in ('name', 'children', 'translation', 'rotation', 'scale')}
        if 'name' in m: m['name'] = m['name'].split(':')[-1]   # "mixamorig:LeftArm" → "LeftArm" (como no Meshy)
        nodes.append(m)
    gltf = {
        'asset': {'version': '2.0', 'generator': 'Cinzas da Maré importar_animacoes.py'},
        'scene': 0, 'scenes': [{'nodes': base_j['scenes'][base_j.get('scene', 0)]['nodes']}],
        'nodes': nodes, 'animations': anims, 'accessors': accs, 'bufferViews': views,
        'buffers': [{'byteLength': len(blob)}],
    }
    js = json.dumps(gltf, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(blob) % 4: blob.append(0)
    total = 12 + 8 + len(js) + 8 + len(blob)
    with open(out, 'wb') as f:
        f.write(struct.pack('<III', 0x46546C67, 2, total))
        f.write(struct.pack('<II', len(js), 0x4E4F534A)); f.write(js)
        f.write(struct.pack('<II', len(blob), 0x004E4942)); f.write(bytes(blob))
    print(out, total // 1024, 'KB')


if __name__ == '__main__':
    main()

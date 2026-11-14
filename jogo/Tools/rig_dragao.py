#!/usr/bin/env python3
"""
Prepara o pequeno draco (modelo Meshy, sem esqueleto) para o jogo:
1) reduz a malha (com UV preservado) para ~N triângulos; 2) põe os pés em y=0;
3) cria um esqueleto (corpo, pescoço, cabeça, braços, pernas, asas, cauda) e pesos rígidos por região;
4) gera animações (Pairar, Voar, Ataque) e grava um .glb com texturas reduzidas.
Uso: python3 Tools/rig_dragao.py entrada.glb saida.glb [triangulos=24000] [textura=1024]
Regras de pesos e posições dos ossos foram tiradas das projeções do modelo (frente +Z, cima +Y).
"""
import io, json, math, os, struct, sys, tempfile
import numpy as np
from PIL import Image

def load_glb(p):
    b = open(p, 'rb').read()
    off = 12; js = None; binc = None
    while off < len(b):
        cl, ct = struct.unpack('<II', b[off:off + 8]); c = b[off + 8:off + 8 + cl]
        if ct == 0x4E4F534A: js = json.loads(c)
        else: binc = c
        off += 8 + cl
    return js, binc

def accessor(j, b, i):
    a = j['accessors'][i]; bv = j['bufferViews'][a['bufferView']]
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    comp = {5126: np.float32, 5125: np.uint32, 5123: np.uint16}[a['componentType']]
    n = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
    arr = np.frombuffer(b, dtype=comp, count=a['count'] * n, offset=off)
    return arr.reshape(a['count'], n) if n > 1 else arr

def decimate(P, UV, F, target):
    import pymeshlab
    tmp = tempfile.mkdtemp()
    src = os.path.join(tmp, 'in.obj'); dst = os.path.join(tmp, 'out.obj')
    with open(src, 'w') as f:
        for p in P: f.write(f'v {p[0]:.6f} {p[1]:.6f} {p[2]:.6f}\n')
        for t in UV: f.write(f'vt {t[0]:.6f} {1 - t[1]:.6f}\n')
        for a, b_, c in F: f.write(f'f {a+1}/{a+1} {b_+1}/{b_+1} {c+1}/{c+1}\n')
    ms = pymeshlab.MeshSet(); ms.load_new_mesh(src)
    ms.meshing_decimation_quadric_edge_collapse_with_texture(targetfacenum=target, preserveboundary=True, planarquadric=True)
    ms.save_current_mesh(dst, save_wedge_texcoord=True)
    vs, vts, faces = [], [], []
    for line in open(dst):
        if line.startswith('v '): vs.append([float(x) for x in line.split()[1:4]])
        elif line.startswith('vt '): vts.append([float(x) for x in line.split()[1:3]])
        elif line.startswith('f '):
            faces.append([tuple(int(x) - 1 for x in w.split('/')[:2]) for w in line.split()[1:4]])
    vs = np.array(vs, np.float32); vts = np.array(vts, np.float32)
    # um vértice por par (posição, uv)
    key = {}; outP = []; outT = []; outF = []
    for f in faces:
        tri = []
        for vi, ti in f:
            k = (vi, ti)
            if k not in key:
                key[k] = len(outP); outP.append(vs[vi]); outT.append([vts[ti][0], 1 - vts[ti][1]])
            tri.append(key[k])
        outF.append(tri)
    return np.array(outP, np.float32), np.array(outT, np.float32), np.array(outF, np.uint32)

def normals(P, F):
    n = np.zeros_like(P)
    fn = np.cross(P[F[:, 1]] - P[F[:, 0]], P[F[:, 2]] - P[F[:, 0]])
    for k in range(3): np.add.at(n, F[:, k], fn)
    l = np.linalg.norm(n, axis=1, keepdims=True); l[l == 0] = 1
    return (n / l).astype(np.float32)

# ------------------------------------------------------------------ esqueleto (coordenadas originais do modelo)
FOOT_Y = -0.72
B = {  # nome: (pai, posição da cabeça do osso)
    'raiz': (None, (0, -0.72, 0.05)),
    'pelve': ('raiz', (0, -0.33, 0.05)),
    'coluna': ('pelve', (0, -0.12, 0.12)),
    'peito': ('coluna', (0, 0.02, 0.18)),
    'pescoco': ('peito', (0, 0.12, 0.24)),
    'cabeca': ('pescoco', (0, 0.30, 0.32)),
    'coxa.E': ('pelve', (0.14, -0.38, 0.12)), 'canela.E': ('coxa.E', (0.16, -0.55, 0.15)), 'pe.E': ('canela.E', (0.2, -0.68, 0.22)),
    'coxa.D': ('pelve', (-0.14, -0.38, 0.12)), 'canela.D': ('coxa.D', (-0.16, -0.55, 0.15)), 'pe.D': ('canela.D', (-0.2, -0.68, 0.22)),
    'braco.E': ('peito', (0.18, -0.10, 0.25)), 'antebraco.E': ('braco.E', (0.28, -0.28, 0.30)),
    'braco.D': ('peito', (-0.18, -0.10, 0.25)), 'antebraco.D': ('braco.D', (-0.28, -0.28, 0.30)),
    'asa.E': ('peito', (0.13, 0.02, 0.0)), 'asa2.E': ('asa.E', (0.45, 0.08, 0.02)),
    'asa.D': ('peito', (-0.13, 0.02, 0.0)), 'asa2.D': ('asa.D', (-0.45, 0.08, 0.02)),
    'cauda1': ('pelve', (0.02, -0.36, -0.15)), 'cauda2': ('cauda1', (0.12, -0.45, -0.38)),
    'cauda3': ('cauda2', (0.24, -0.50, -0.62)), 'cauda4': ('cauda3', (0.36, -0.46, -0.85)),
}
TIPS = {'cabeca': (0, 0.25, 0.9), 'pe.E': (0.25, -0.72, 0.35), 'pe.D': (-0.25, -0.72, 0.35),
        'antebraco.E': (0.34, -0.42, 0.36), 'antebraco.D': (-0.34, -0.42, 0.36),
        'asa2.E': (0.85, -0.2, 0.0), 'asa2.D': (-0.85, -0.2, 0.0), 'cauda4': (0.45, -0.18, -0.95),
        'raiz': (0, -0.33, 0.05)}
NAMES = list(B.keys())

def segment(name):
    head = np.array(B[name][1], np.float32)
    kids = [k for k, (p, _) in B.items() if p == name]
    tail = np.array(TIPS[name] if name in TIPS else B[kids[0]][1], np.float32)
    return head, tail

def seg_dist(P, a, b):
    ab = b - a; t = np.clip(((P - a) @ ab) / max(1e-9, ab @ ab), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)

def weights(P):
    """Peso rígido (1 osso por vértice): metal em placas; a região decide os candidatos, o mais perto vence."""
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    D = np.stack([seg_dist(P, *segment(n)) for n in NAMES], axis=1)
    idx = {n: i for i, n in enumerate(NAMES)}
    allow = np.zeros_like(D, bool)
    def allow_set(mask, names):
        for n in names: allow[mask, idx[n]] = True
    head = (y > 0.33) | ((z > 0.42) & (y > 0.05))
    tail = (z < -0.18) & (y < -0.2) & ~head
    wing = ~head & ~tail & (((np.abs(x) > 0.40) & (y > -0.32)) | ((np.abs(x) > 0.10) & (y > -0.06) & (z < 0.12)))
    leg = ~head & ~tail & ~wing & (y < -0.30) & (z > -0.18)
    rest = ~(head | tail | wing | leg)
    allow_set(head, ['cabeca'])
    allow_set(tail, ['cauda1', 'cauda2', 'cauda3', 'cauda4'])
    allow_set(wing & (x > 0), ['asa.E', 'asa2.E']); allow_set(wing & (x <= 0), ['asa.D', 'asa2.D'])
    allow_set(leg, ['pelve', 'coxa.E', 'canela.E', 'pe.E', 'coxa.D', 'canela.D', 'pe.D'])
    allow_set(rest, ['pelve', 'coluna', 'peito', 'pescoco', 'cabeca', 'braco.E', 'antebraco.E', 'braco.D', 'antebraco.D'])
    D[~allow] = 1e9
    j = np.argmin(D, axis=1)
    J = np.zeros((len(P), 4), np.uint16); J[:, 0] = j
    W = np.zeros((len(P), 4), np.float32); W[:, 0] = 1
    return J, W

# ------------------------------------------------------------------ animações
def quat(axis, deg):
    a = np.array(axis, float); a /= np.linalg.norm(a); h = math.radians(deg) / 2
    return [*(a * math.sin(h)), math.cos(h)]

def qmul(a, b):
    x1, y1, z1, w1 = a; x2, y2, z2, w2 = b
    return [w1*x2 + x1*w2 + y1*z2 - z1*y2, w1*y2 - x1*z2 + y1*w2 + z1*x2, w1*z2 + x1*y2 - y1*x2 + z1*w2, w1*w2 - x1*x2 - y1*y2 - z1*z2]

def clip(name, length, fps, fn):
    """fn(t) -> {osso: quat}; devolve (nome, tempos, {osso: [quats]})."""
    n = int(length * fps) + 1; times = [i / fps for i in range(n)]
    tracks = {}
    for t in times:
        for bone, q in fn(t, length).items(): tracks.setdefault(bone, []).append(q)
    return name, times, tracks

def flap(t, L, amp, rate, tail_amp, head_amp, legs_tuck):
    s = math.sin(2 * math.pi * t / L * rate)
    c = math.cos(2 * math.pi * t / L * rate)
    return {
        'asa.E': quat((0, 0, 1), amp * s), 'asa.D': quat((0, 0, 1), -amp * s),
        'asa2.E': quat((0, 0, 1), amp * 0.6 * math.sin(2 * math.pi * t / L * rate - 0.8)),
        'asa2.D': quat((0, 0, 1), -amp * 0.6 * math.sin(2 * math.pi * t / L * rate - 0.8)),
        'cauda1': quat((0, 1, 0), tail_amp * math.sin(2 * math.pi * t / L)),
        'cauda2': quat((0, 1, 0), tail_amp * 1.2 * math.sin(2 * math.pi * t / L - 0.7)),
        'cauda3': quat((0, 1, 0), tail_amp * 1.4 * math.sin(2 * math.pi * t / L - 1.4)),
        'cauda4': quat((0, 1, 0), tail_amp * 1.6 * math.sin(2 * math.pi * t / L - 2.1)),
        'cabeca': quat((1, 0, 0), head_amp * c), 'pescoco': quat((1, 0, 0), -head_amp * 0.5 * c),
        'peito': quat((1, 0, 0), 3 * s),
        'coxa.E': quat((1, 0, 0), -legs_tuck), 'coxa.D': quat((1, 0, 0), -legs_tuck),
        'canela.E': quat((1, 0, 0), legs_tuck * 1.3), 'canela.D': quat((1, 0, 0), legs_tuck * 1.3),
        'raiz': [0, 0, 0, 1],
    }

def attack(t, L):
    k = t / L
    if k < 0.45: rear = math.sin(k / 0.45 * math.pi / 2)            # empina e puxa a cabeça
    elif k < 0.6: rear = 1 - (k - 0.45) / 0.15 * 1.8                 # bote/sopro
    else: rear = -0.8 * (1 - (k - 0.6) / 0.4)                        # volta
    base = flap(t, L, 30, 2, 8, 0, 25)
    base['cabeca'] = quat((1, 0, 0), -18 * rear)
    base['pescoco'] = quat((1, 0, 0), -12 * rear)
    base['peito'] = quat((1, 0, 0), -6 * rear)
    base['asa.E'] = qmul(base['asa.E'], quat((0, 1, 0), 15 * max(0, rear)))
    base['asa.D'] = qmul(base['asa.D'], quat((0, 1, 0), -15 * max(0, rear)))
    return base

# ------------------------------------------------------------------ gravação
def build(P, N, UV, F, J, W, tex, clips, out):
    bin_ = bytearray(); views = []; accs = []
    def add(arr, target=None, typ='VEC3', comp=5126, minmax=False):
        arr = np.ascontiguousarray(arr)
        while len(bin_) % 4: bin_.append(0)
        off = len(bin_); bin_.extend(arr.tobytes())
        v = {'buffer': 0, 'byteOffset': off, 'byteLength': arr.nbytes}
        if target: v['target'] = target
        views.append(v)
        a = {'bufferView': len(views) - 1, 'componentType': comp, 'count': len(arr), 'type': typ}
        if minmax: a['min'] = arr.min(axis=0).tolist(); a['max'] = arr.max(axis=0).tolist()
        accs.append(a); return len(accs) - 1
    def add_image(img):
        b = io.BytesIO(); img.save(b, 'JPEG', quality=88); data = b.getvalue()
        while len(bin_) % 4: bin_.append(0)
        off = len(bin_); bin_.extend(data); views.append({'buffer': 0, 'byteOffset': off, 'byteLength': len(data)})
        return {'bufferView': len(views) - 1, 'mimeType': 'image/jpeg'}
    pos = add(P, 34962, minmax=True); nor = add(N, 34962); uv = add(UV, 34962, 'VEC2')
    jo = add(J.astype(np.uint16), 34962, 'VEC4', 5123); we = add(W, 34962, 'VEC4')
    ind = add(F.reshape(-1).astype(np.uint32), 34963, 'SCALAR', 5125)
    # nós: 0 = malha, 1 = Armature, 2.. = ossos
    nodes = [{'name': 'Draco', 'mesh': 0, 'skin': 0}, {'name': 'Armature', 'children': []}]
    bone_node = {}
    for n in NAMES: bone_node[n] = len(nodes); nodes.append({'name': n})
    glob = {n: np.array(B[n][1], np.float32) for n in NAMES}
    for n in NAMES:
        parent = B[n][0]
        local = glob[n] - (glob[parent] if parent else 0)
        nodes[bone_node[n]]['translation'] = local.tolist()
        if parent: nodes[bone_node[parent]].setdefault('children', []).append(bone_node[n])
        else: nodes[1]['children'].append(bone_node[n])
    ibm = np.stack([np.array([[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [-glob[n][0], -glob[n][1], -glob[n][2], 1]], np.float32).reshape(16) for n in NAMES])
    ibm_acc = add(ibm, typ='MAT4')
    anims = []
    for name, times, tracks in clips:
        tacc = add(np.array(times, np.float32), typ='SCALAR')
        accs[tacc]['min'] = [times[0]]; accs[tacc]['max'] = [times[-1]]
        samplers = []; channels = []
        for bone, qs in tracks.items():
            qa = add(np.array(qs, np.float32), typ='VEC4')
            samplers.append({'input': tacc, 'output': qa, 'interpolation': 'LINEAR'})
            channels.append({'sampler': len(samplers) - 1, 'target': {'node': bone_node[bone], 'path': 'rotation'}})
        anims.append({'name': name, 'samplers': samplers, 'channels': channels})
    imgs = [add_image(t) for t in tex]
    gl = {
        'asset': {'version': '2.0', 'generator': 'Cinzas da Maré rig_dragao.py'},
        'scene': 0, 'scenes': [{'nodes': [0, 1]}], 'nodes': nodes,
        'meshes': [{'name': 'Draco', 'primitives': [{'attributes': {'POSITION': pos, 'NORMAL': nor, 'TEXCOORD_0': uv, 'JOINTS_0': jo, 'WEIGHTS_0': we}, 'indices': ind, 'material': 0}]}],
        'skins': [{'name': 'Esqueleto', 'joints': [bone_node[n] for n in NAMES], 'inverseBindMatrices': ibm_acc, 'skeleton': 1}],
        'animations': anims,
        'materials': [{'name': 'Metal oxidado', 'doubleSided': True,
                       'pbrMetallicRoughness': {'baseColorTexture': {'index': 0}, 'metallicRoughnessTexture': {'index': 1}, 'metallicFactor': 1.0, 'roughnessFactor': 1.0},
                       'normalTexture': {'index': 2}}],
        'textures': [{'source': i, 'sampler': 0} for i in range(len(imgs))],
        'samplers': [{'magFilter': 9729, 'minFilter': 9987}],
        'images': imgs, 'accessors': accs, 'bufferViews': views, 'buffers': [{'byteLength': len(bin_)}],
    }
    js = json.dumps(gl, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    while len(bin_) % 4: bin_.append(0)
    total = 12 + 8 + len(js) + 8 + len(bin_)
    with open(out, 'wb') as f:
        f.write(struct.pack('<III', 0x46546C67, 2, total))
        f.write(struct.pack('<II', len(js), 0x4E4F534A)); f.write(js)
        f.write(struct.pack('<II', len(bin_), 0x004E4942)); f.write(bin_)

def main():
    src, out = sys.argv[1], sys.argv[2]
    target = int(sys.argv[3]) if len(sys.argv) > 3 else 24000
    texsize = int(sys.argv[4]) if len(sys.argv) > 4 else 1024
    j, b = load_glb(src)
    pr = j['meshes'][0]['primitives'][0]
    P = accessor(j, b, pr['attributes']['POSITION']).astype(np.float32)
    UV = accessor(j, b, pr['attributes']['TEXCOORD_0']).astype(np.float32)
    F = accessor(j, b, pr['indices']).reshape(-1, 3).astype(np.int64)
    print('original', len(P), 'vértices', len(F), 'triângulos')
    P2, UV2, F2 = decimate(P, UV, F, target)
    print('reduzido', len(P2), 'vértices', len(F2), 'triângulos')
    J, W = weights(P2)
    used = sorted(set(J[:, 0].tolist())); print('ossos com peso', [NAMES[i] for i in used])
    # pés no chão: desloca a malha e os ossos juntos
    shift = np.array([0, -FOOT_Y, 0], np.float32)
    P2 = P2 + shift
    for n in NAMES: B[n] = (B[n][0], tuple((np.array(B[n][1]) + shift).tolist()))
    for n in TIPS: TIPS[n] = tuple((np.array(TIPS[n]) + shift).tolist())
    N2 = normals(P2, F2)
    tex = []
    for i in range(3):
        bv = j['bufferViews'][j['images'][i]['bufferView']]
        img = Image.open(io.BytesIO(b[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']])).convert('RGB')
        tex.append(img.resize((texsize, texsize), Image.LANCZOS))
    # material original: base=img0, metal/rugosidade=img1, normal=img2
    order = [j['materials'][0]['pbrMetallicRoughness']['baseColorTexture']['index'],
             j['materials'][0]['pbrMetallicRoughness']['metallicRoughnessTexture']['index'],
             j['materials'][0]['normalTexture']['index']]
    tex = [tex[j['textures'][k]['source']] for k in order]
    clips = [
        clip('Pairar', 2.0, 15, lambda t, L: flap(t, L, 28, 2, 10, 4, 20)),
        clip('Voar', 1.2, 20, lambda t, L: flap(t, L, 50, 2, 6, 3, 45)),
        clip('Ataque', 0.9, 30, attack),
    ]
    build(P2, N2, UV2, F2.astype(np.uint32), J, W, tex, clips, out)
    print('gravado', out, os.path.getsize(out) // 1024, 'KB')

if __name__ == '__main__':
    main()

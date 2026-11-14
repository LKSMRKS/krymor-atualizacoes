"""Armadura inteira do Meshy (boneco oco em pose A) -> 4 peças vestíveis no manequim do jogo (11/11).
Uso: python3.11 armadura_meshy.py manequim_masc.glb entrada.glb saida.glb joelho cotovelo_x cotovelo_z mao_x mao_z [topo]
(precisa do módulo bpy: pip install bpy==4.2.0 para Python 3.11)

Passos:
1. Encaixa a armadura nas medidas do manequim: altura remapeada por trechos (chão, joelho, base do pescoço, topo da
   cabeça) e largura/profundidade reduzidas (o manequim é mais magro que o boneco do Meshy).
2. Dobra os braços do manequim até a pose A da armadura (cotovelo e mão medidos na foto de frente) e copia os pesos
   da pele do manequim para cada vértice da armadura (vizinhos mais próximos). Pano solto entre as pernas (saia,
   manto) puxa o peso para o quadril, para não rasgar ao andar.
3. Desfaz a pose (de A para T): a armadura fica na pose de referência do manequim, presa ao mesmo esqueleto.
4. Separa em 4 malhas pelo osso dominante: "capacete" (cabeça), "luvas" (antebraço e mão), "botas" (canela e pé,
   perto da perna) e "peitoral" (todo o resto: tronco, ombros, braço, coxas, saia).
5. Exporta um .glb com o esqueleto do manequim e as 4 malhas com pele; texturas reduzidas para 1024 px.
O cliente (MannequinVisual.SetArmorPiece) pega a malha da peça vestida e liga ao esqueleto do personagem.
"""
import sys, math, bpy, bmesh, numpy as np
from mathutils import Vector, Matrix, kdtree

argv = sys.argv[sys.argv.index("--") + 1:]
MAN, SRC, DST = argv[0], argv[1], argv[2]
KNEE, EX, EZ, HX, HZ = map(float, argv[3:8])
TOP = float(argv[8]) if len(argv) > 8 else 1.80
NECK_S = float(argv[9]) if len(argv) > 9 else 1.58
TRIS = 70000

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=MAN)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o != body: bpy.data.objects.remove(o)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
if arm.animation_data: arm.animation_data_clear()
for pb in arm.pose.bones: pb.matrix_basis.identity()   # o importador deixa o 1º quadro do Idle na pose
bpy.context.view_layer.update()
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=SRC)
suit = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
for o in [o for o in bpy.data.objects if o not in before and o != suit]: bpy.data.objects.remove(o)
suit.parent = None
bpy.ops.object.select_all(action='DESELECT')
bpy.context.view_layer.objects.active = suit; suit.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# ---- 0. reduz a malha (Meshy vem com ~1,4 milhão de triângulos)
tris = sum(len(p.vertices) - 2 for p in suit.data.polygons)
dec = suit.modifiers.new("dec", 'DECIMATE'); dec.ratio = min(1.0, TRIS / max(1, tris))
bpy.ops.object.modifier_apply(modifier="dec")
print("triângulos:", tris, "->", sum(len(p.vertices) - 2 for p in suit.data.polygons))

# ---- 1. encaixe nas medidas do manequim
MW = arm.matrix_world
def bone_head(name):
    return MW @ arm.data.bones["mixamorig:" + name].head_local
NECK_M = bone_head("Neck").z
TOP_M = bone_head("HeadTop_End").z + 0.015
KNEE_M = bone_head("LeftLeg").z
V = np.array([v.co[:] for v in suit.data.vertices])
V[:, 2] -= V[:, 2].min()
zs = [0.0, KNEE, NECK_S, TOP, TOP + 1.0]
zm = [0.0, KNEE_M, NECK_M, TOP_M, TOP_M + 1.0 * (TOP_M - NECK_M) / (TOP - NECK_S)]
def remap_z(z): return np.interp(z, zs, zm)
def widen(zm_):
    # largura por altura (no espaço do manequim): pernas 0,78, tronco 0,82, cabeça 0,92
    return np.interp(zm_, [0.0, 0.85, 1.0, NECK_M, NECK_M + 0.08, 3.0], [0.78, 0.78, 0.82, 0.82, 0.92, 0.92])
def remap(P):
    P = np.array(P, dtype=float)
    z = remap_z(P[..., 2]); k = widen(z)
    out = P.copy(); out[..., 0] *= k; out[..., 1] = P[..., 1] * k * 0.95 + bone_head("Hips").y * (1 - k * 0.95); out[..., 2] = z
    return out
V2 = remap(V)
# cabeça/capacete: centraliza na cabeça do manequim (frente-trás), sumindo aos poucos até a base do pescoço
hm = V2[:, 2] > NECK_M + 0.10
if hm.any():
    hy = (V2[hm, 1].min() + V2[hm, 1].max()) / 2
    dy = (bone_head("Head").y + 0.005) - hy
    V2[:, 1] += dy * np.clip((V2[:, 2] - NECK_M) / 0.10, 0, 1)
    print("cabeça deslocada %.3f m" % dy)
for v, p in zip(suit.data.vertices, V2): v.co = Vector(p)
suit.data.update()

# ---- 2. braços do manequim na pose A da armadura
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
inv = MW.inverted()
def aim(bone, child, target_world):
    pb = arm.pose.bones["mixamorig:" + bone]
    bpy.context.view_layer.update()
    h = MW @ pb.head; c = MW @ arm.pose.bones["mixamorig:" + child].head
    rot = (c - h).rotation_difference(target_world - h)
    # rotação no espaço da armadura, em volta da cabeça do osso
    R = (inv.to_3x3() @ rot.to_matrix() @ MW.to_3x3()).to_4x4()
    hl = pb.head.copy()
    pb.matrix = Matrix.Translation(hl) @ R @ Matrix.Translation(-hl) @ pb.matrix
    bpy.context.view_layer.update()
for side, sx in (("Left", 1), ("Right", -1)):
    e = Vector(remap([sx * EX, 0.0, EZ])); w = Vector(remap([sx * HX, 0.0, HZ]))
    e.y = bone_head(side + "Arm").y; w.y = e.y
    aim(side + "Arm", side + "ForeArm", e)
    # a mão medida é a ponta da luva: o pulso fica ~0,09 m antes
    aim(side + "ForeArm", side + "Hand", w)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()

# ---- pesos: vizinhos mais próximos na pele já dobrada
dg = bpy.context.evaluated_depsgraph_get()
ev = body.evaluated_get(dg); me = ev.to_mesh()
BW = body.matrix_world
BP = [BW @ v.co for v in me.vertices]
gnames = [g.name for g in body.vertex_groups]
bw = np.zeros((len(body.data.vertices), len(gnames)))
for v in body.data.vertices:
    for g in v.groups: bw[v.index, g.group] = g.weight
ev.to_mesh_clear()
kd = kdtree.KDTree(len(BP))
for i, p in enumerate(BP): kd.insert(p, i)
kd.balance()
gi = {n: i for i, n in enumerate(gnames)}
HIPS = gi["mixamorig:Hips"]
LEGS = [gi["mixamorig:" + n] for n in ("LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg")]
SW = np.zeros((len(suit.data.vertices), len(gnames)))
dist = np.zeros(len(suit.data.vertices))
for v in suit.data.vertices:
    near = kd.find_n(v.co, 6)
    acc = np.zeros(len(gnames)); tot = 0.0
    for (co, idx, d) in near:
        f = 1.0 / (d + 0.004); acc += bw[idx] * f; tot += f
    SW[v.index] = acc / tot
    dist[v.index] = near[0][2]
# pano solto (longe da pele) na altura das coxas: peso vai para o quadril
z = np.array([v.co.z for v in suit.data.vertices])
legw = SW[:, LEGS].sum(axis=1)
loose = np.clip((dist - 0.05) / 0.08, 0, 1) * (z > 0.45) * (legw > 0.2)
for j in LEGS: SW[:, HIPS] += SW[:, j] * loose * 0.85; SW[:, j] *= 1 - loose * 0.85
SW /= np.maximum(SW.sum(axis=1, keepdims=True), 1e-9)

# ---- 3. de A para T (skinning ao contrário: pose -> repouso)
mats = []
for n in gnames:
    if n not in arm.pose.bones: mats.append(Matrix.Identity(4)); continue
    pb = arm.pose.bones[n]
    mats.append((MW @ pb.bone.matrix_local) @ (MW @ pb.matrix).inverted())
for v in suit.data.vertices:
    w = SW[v.index]; idx = np.argsort(-w)[:4]
    acc = Vector((0, 0, 0)); tot = 0
    for j in idx:
        if w[j] < 1e-4: continue
        acc += float(w[j]) * (mats[j] @ v.co); tot += float(w[j])
    v.co = acc / tot
suit.data.update()
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.select_all(action='SELECT'); bpy.ops.pose.transforms_clear()
bpy.ops.object.mode_set(mode='OBJECT')

# ---- 4. peças pelo osso dominante
dom = [gnames[i] for i in SW.argmax(axis=1)]
Z = np.array([v.co.z for v in suit.data.vertices]); X = np.array([v.co.x for v in suit.data.vertices]); Y = np.array([v.co.y for v in suit.data.vertices])
def leg_axis_dist(i, side):
    a = bone_head(side + "Leg"); b = bone_head(side + "Foot")
    p = Vector((X[i], Y[i], Z[i])); t = max(0, min(1, (p - a).dot(b - a) / (b - a).length_squared))
    return (p - (a + (b - a) * t)).length
cat = []
KNEE_TOP = KNEE_M - 0.04
for i, d in enumerate(dom):
    n = d.replace("mixamorig:", "")
    if n in ("Head", "HeadTop_End", "headfront") or (n == "Neck" and Z[i] > NECK_M + 0.06): cat.append(0)        # capacete
    elif n in ("LeftForeArm", "RightForeArm", "LeftHand", "RightHand"): cat.append(1)                               # luvas
    elif Z[i] < KNEE_TOP and n in ("LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "LeftUpLeg", "RightUpLeg") \
            and leg_axis_dist(i, "Left" if X[i] > 0 else "Right") < 0.13: cat.append(2)                           # botas
    else: cat.append(3)                                                                                             # peitoral
cat = np.array(cat)
NAMES = ["capacete", "luvas", "botas", "peitoral"]
# vértice da cabeça só com o osso da cabeça (capacete rígido)
for i in np.where(cat == 0)[0]: SW[i] = 0; SW[i, gi["mixamorig:Head"]] = 1
for g in gnames: suit.vertex_groups.new(name=g)
for v in suit.data.vertices:
    w = SW[v.index]; idx = np.argsort(-w)[:4]; tot = w[idx].sum()
    for j in idx:
        if w[j] > 1e-3: suit.vertex_groups[j].add([v.index], float(w[j] / tot), 'REPLACE')
# face vai para a peça da maioria dos vértices
mesh = suit.data
fcat = np.array([np.bincount(cat[list(p.vertices)], minlength=4).argmax() for p in mesh.polygons])
pieces = []
for k, nm in enumerate(NAMES):
    if not (fcat == k).any(): continue
    o = suit.copy(); o.data = suit.data.copy(); o.name = nm; o.data.name = nm
    bpy.context.collection.objects.link(o)
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if fcat[f.index] != k], context='FACES')
    loose_v = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose_v, context='VERTS')
    bm.to_mesh(o.data); bm.free()
    o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = o.modifiers.new("arm", 'ARMATURE'); mod.object = arm
    pieces.append(o)
    print(nm, len(o.data.vertices), "vértices")
bpy.data.objects.remove(suit); bpy.data.objects.remove(body)
for img in bpy.data.images:
    if img.size[0] > 1024: img.scale(1024, 1024)
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for o in pieces: o.select_set(True)
bpy.ops.export_scene.gltf(filepath=DST, export_format='GLB', use_selection=True, export_skins=True, export_animations=False,
                          export_image_format='JPEG', export_jpeg_quality=82, export_apply=False)
print("ok", DST)

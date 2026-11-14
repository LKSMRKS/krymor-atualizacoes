"""Armaduras do Meshy (corpo inteiro, em pose A) encaixadas no esqueleto do manequim_masc.
Passos: alinha (pés no chão, frente -Y) -> posa o manequim igual à armadura (braços abaixados) -> transfere os pesos do corpo
posado -> desfaz a pose (LBS inverso) para a pose de repouso do jogo (T) -> separa em peças pelo osso dominante
(capacete, peitoral, braçadeiras, luvas, perneiras, botas) -> reduz malha/texturas -> exporta 1 glb por armadura (cada peça um nó).
Uso: python3 build_armaduras.py <saida> <id> [--previa]"""
import sys, os, math, json
sys.path.insert(0, "/home/claude/cinzas/tools")
OUT = sys.argv[1]; AID = sys.argv[2]; PREVIA = "--previa" in sys.argv
sys.argv = [sys.argv[0], "/tmp/claude-0/arm_tmp"]
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, Quaternion, kdtree
import build_mortos_cal as B
import itemkit as K
B.OUT = OUT

U = "/mnt/user-data/uploads/"
ARM = {
    "armadura_abissal":   dict(f="Meshy_AI_abyssal_teal_armor_3d_1111170629_image-to-3d-texture.glb", nome="Armadura Abissal"),
    "armadura_anjo":      dict(f="Meshy_AI_angel_winged_armor_3d_1111170604_image-to-3d-texture.glb", nome="Armadura do Anjo Alado", asas=True),
    "armadura_lobo":      dict(f="Meshy_AI_dark_wolf_armor_3d_1111170607_image-to-3d-texture.glb", nome="Armadura do Lobo Sombrio"),
    "armadura_fenix":     dict(f="Meshy_AI_phoenix_valkyrie_armo_1111170621_image-to-3d-texture.glb", nome="Armadura da Valquíria Fênix", asas=True),
    "armadura_dragao":    dict(f="Meshy_AI_red_dragon_armor_3d_1111170617_image-to-3d-texture.glb", nome="Armadura do Dragão Rubro"),
    "armadura_serpente":  dict(f="Meshy_AI_serpent_green_armor_3_1111170613_image-to-3d-texture.glb", nome="Armadura da Serpente"),
    "armadura_acolchoada":     dict(f="Meshy_AI_padded_tunic_3d_1111183412_image-to-3d-texture__1_.glb", nome="Gibão Acolchoado", nivel="1–10"),
    "armadura_couro_bronze":   dict(f="Meshy_AI_viking_bronze_armor_3_1111183514_image-to-3d-texture.glb", nome="Couraça de Couro e Bronze", nivel="8–18"),
    "armadura_soldado_ferro":  dict(f="Meshy_AI_iron_soldier_armor_3d_1111183521_image-to-3d-texture.glb", nome="Armadura de Soldado de Ferro", nivel="15–25"),
    "armadura_escamas_verdes": dict(f="Meshy_AI_green_scale_armor_3d_1111183502_image-to-3d-texture.glb", nome="Cota de Escamas Verdes", nivel="20–30"),
    "armadura_barbaro_lobo":   dict(f="Meshy_AI_wolf_fur_barbarian_3d_1111183426_image-to-3d-texture__1_.glb", nome="Pele de Lobo do Bárbaro", nivel="25–35"),
    "armadura_cavaleiro_aco":  dict(f="Meshy_AI_winged_steel_knight_3_1111183529_image-to-3d-texture.glb", nome="Armadura do Cavaleiro de Aço", nivel="30–40"),
}
cfg = ARM[AID]
BRACO = cfg.get("braco", 57.0); ANTEBRACO = cfg.get("antebraco", 6.0); PERNA = cfg.get("perna", 5.0); PESCOCO = cfg.get("pescoco", float(os.environ.get("PESC", 0)))

PECAS = {   # padrão MU (pedido do dono): 5 peças
    "capacete": ("Head", "HeadTop_End", "headfront"),
    "peitoral": ("Neck", "Spine", "Spine1", "Spine2", "LeftShoulder", "RightShoulder", "LeftArm", "RightArm"),
    "luvas": ("LeftForeArm", "RightForeArm", "LeftHand", "RightHand"),
    "calca": ("Hips", "LeftUpLeg", "RightUpLeg"),
    "botas": ("LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "LeftToe_End", "RightToe_End"),
}
ALVO_TRIS = {"capacete": 6000, "peitoral": 10000, "luvas": 5000, "calca": 7000, "botas": 5000}   # mesmo orçamento em todos os conjuntos
def peca_de(bone):
    b = bone.replace("mixamorig:", "")
    if "Hand" in b: return "luvas"
    for p, bs in PECAS.items():
        if b in bs: return p
    return "peitoral"

# ------------------------------------------------------------------ 1. manequim + armadura
arm, body, P, T = B.importar()
before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=U + cfg["f"])
a = [o for o in bpy.data.objects if o not in before and o.type == 'MESH'][0]; a.name = AID
for o in [o for o in bpy.data.objects if o not in before and o.type != 'MESH']: bpy.data.objects.remove(o)
bpy.context.view_layer.update(); a.data.transform(a.matrix_world); a.parent = None; a.matrix_world = Matrix()
zmin = min(v.co.z for v in a.data.vertices); a.data.transform(Matrix.Translation((0, 0, -zmin)))
# reduz antes (Meshy vem com 1,5-2 milhões de triângulos)
m = a.modifiers.new("dec", 'DECIMATE'); m.ratio = 120000 / len(a.data.polygons)
bpy.context.view_layer.objects.active = a; bpy.ops.object.modifier_apply(modifier="dec")

# ------------------------------------------------------------------ 2. posa o manequim igual à armadura (pose A)
RW = arm.matrix_world.to_3x3().normalized()
def gira(bone, eixo_mundo, graus):
    pb = arm.pose.bones["mixamorig:" + bone]; bl = arm.data.bones["mixamorig:" + bone]
    ax = (bl.matrix_local.to_3x3().inverted() @ (RW.inverted() @ Vector(eixo_mundo))).normalized()
    pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion(ax, math.radians(graus)) @ pb.rotation_quaternion
if arm.animation_data: arm.animation_data.action = None
for t in (arm.animation_data.nla_tracks if arm.animation_data else []): t.mute = True
arm.data.pose_position = 'POSE'
for pb in arm.pose.bones: pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)
gira("LeftArm", (0, 1, 0), BRACO); gira("RightArm", (0, 1, 0), -BRACO)
gira("LeftForeArm", (0, 1, 0), ANTEBRACO); gira("RightForeArm", (0, 1, 0), -ANTEBRACO)
gira("LeftUpLeg", (0, 1, 0), -PERNA); gira("RightUpLeg", (0, 1, 0), PERNA); gira("Neck", (1, 0, 0), PESCOCO)
bpy.context.view_layer.update()

if PREVIA:
    # sobreposição: manequim vermelho + armadura translúcida, frente e lado
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'OBJECT'
    sc.display.shading.show_xray = True; sc.display.shading.xray_alpha = 0.55
    body.color = (1, 0.1, 0.1, 1); a.color = (0.6, 0.8, 1, 1)
    sc.render.resolution_x = 500; sc.render.resolution_y = 600
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.05
    for nm, r in (("f", 0), ("s", 90)):
        ang = math.radians(r); cam.location = (5 * math.sin(ang), -5 * math.cos(ang), 0.95); cam.rotation_euler = (math.radians(90), 0, ang)
        sc.render.filepath = f"/tmp/claude-0/fit_{AID}_{nm}.png"; bpy.ops.render.render(write_still=True)
    print("PREVIA", AID); sys.exit(0)

# ------------------------------------------------------------------ 3. pesos do corpo posado -> armadura
dg = bpy.context.evaluated_depsgraph_get()
be = body.evaluated_get(dg); bm_ = be.to_mesh()
names = {g.index: g.name for g in body.vertex_groups}
bw = body.matrix_world
pos = [bw @ v.co for v in bm_.vertices]
wts = [{names[g.group]: g.weight for g in v.groups if g.weight > 0.01 and g.group in names} for v in body.data.vertices]
be.to_mesh_clear()
kd = kdtree.KDTree(len(pos))
for i, p in enumerate(pos): kd.insert(p, i)
kd.balance()
Wv = []   # pesos por vértice da armadura
spine2 = "mixamorig:Spine2"
# segmentos dos ossos POSADOS (mundo): cada vértice da armadura segue o osso cujo segmento está mais perto
# (mais confiável que o ponto mais próximo do corpo: punho, ombreira e chifre não "grudam" no tronco)
_AW = arm.matrix_world; _AWi = _AW.inverted()
_skin = {pb.name: pb.matrix @ arm.data.bones[pb.name].matrix_local.inverted() for pb in arm.pose.bones}
def _posado(bone, p): return _AW @ (_skin["mixamorig:" + bone] @ (_AWi @ p))
def _seg(bone, a_, b_): return (bone, _posado(bone, a_), _posado(bone, b_))
SEGS = [_seg("Hips", P["Hips"] + Vector((0, 0, -0.08)), P["Spine"]), _seg("Spine", P["Spine"], P["Spine1"]), _seg("Spine1", P["Spine1"], P["Spine2"]),
        _seg("Spine2", P["Spine2"], P["Neck"]), _seg("Neck", P["Neck"], P["Head"]), _seg("Head", P["Head"], P["Head"] + Vector((0, -0.02, 0.2)))]
for L in ("Left", "Right"):
    dh = (P[L + "Hand"] - P[L + "ForeArm"]).normalized()
    SEGS += [_seg(L + "Shoulder", P[L + "Shoulder"], P[L + "Arm"]), _seg(L + "Arm", P[L + "Arm"], P[L + "ForeArm"]), _seg(L + "ForeArm", P[L + "ForeArm"], P[L + "Hand"]),
             _seg(L + "Hand", P[L + "Hand"], P[L + "Hand"] + dh * 0.17), _seg(L + "UpLeg", P[L + "UpLeg"], P[L + "Leg"]), _seg(L + "Leg", P[L + "Leg"], P[L + "Foot"]),
             _seg(L + "Foot", P[L + "Foot"], P[L + "ToeBase"]), _seg(L + "ToeBase", P[L + "ToeBase"], P[L + "ToeBase"] + Vector((0, -0.09, 0)))]
# raio de "corpo" de cada segmento: o tronco é largo, braço/perna são finos (sem isso o peito puxa o braço e vice-versa)
RAIO = {"Hips": 0.12, "Spine": 0.11, "Spine1": 0.115, "Spine2": 0.12, "Neck": 0.05, "Head": 0.1}
def _raio(b):
    b0 = b.replace("Left", "").replace("Right", "")
    return RAIO.get(b0, {"Shoulder": 0.06, "Arm": 0.065, "ForeArm": 0.045, "Hand": 0.04, "UpLeg": 0.08, "Leg": 0.06, "Foot": 0.045, "ToeBase": 0.035}.get(b0, 0.05))
for v in a.data.vertices:
    co = v.co
    ds = sorted((max(0.0, org_seg(co, a_, b_) - _raio(bn)), bn) for bn, a_, b_ in SEGS) if False else None
    ds = []
    for bn, a_, b_ in SEGS:
        ab = b_ - a_; t = max(0.0, min(1.0, (co - a_).dot(ab) / max(ab.length_squared, 1e-9)))
        ds.append((max(0.0, (co - (a_ + ab * t)).length - _raio(bn)), bn))
    ds.sort()
    d0 = ds[0][0]
    acc = {}
    for d, bn in ds[:3]:
        w_ = math.exp(-(d - d0) / 0.025)
        if w_ > 0.05: acc["mixamorig:" + bn] = acc.get("mixamorig:" + bn, 0) + w_
    if cfg.get("asas") and d0 > 0.09 and co.y > P["Spine2"].y + 0.05 and co.z > P["Spine"].z:
        acc = {spine2: 1.0}      # asas: presas às costas, não aos braços
    BR = ("Arm", "Hand", "Shoulder")
    d_braco = min(d for d, bn in ds if any(b in bn for b in ("ForeArm", "Hand")))
    if abs(co.x) < 0.3 and co.z < P["Hips"].z + 0.12 and d_braco > 0.05:   # placa de saia perto da mão: não segue o braço
        acc = {g: x for g, x in acc.items() if not any(b in g for b in BR)} or {"mixamorig:Hips": 1.0}
    s = sum(acc.values()) or 1.0
    acc = {g: x / s for g, x in sorted(acc.items(), key=lambda kv: -kv[1])[:4] if x / s > 0.03}
    s = sum(acc.values()); Wv.append({g: x / s for g, x in acc.items()})

# peças soltas da armadura (braçadeira, placa, fivela, pena...) seguem UM osso só, inteiras: não rasgam nem esticam
import collections
bmI = bmesh.new(); bmI.from_mesh(a.data); bmI.verts.ensure_lookup_table()
vis = set(); nV = len(bmI.verts)
for v0 in bmI.verts:
    if v0.index in vis: continue
    ilha, pilha = [], [v0]; vis.add(v0.index)
    while pilha:
        u = pilha.pop(); ilha.append(u.index)
        for e in u.link_edges:
            w_ = e.other_vert(u)
            if w_.index not in vis: vis.add(w_.index); pilha.append(w_)
    if 3 < len(ilha) < 0.04 * nV:
        votos = collections.Counter()
        for vi in ilha:
            if Wv[vi]: votos[max(Wv[vi].items(), key=lambda kv: kv[1])[0]] += 1
        if votos:
            b_ = votos.most_common(1)[0][0]
            for vi in ilha: Wv[vi] = {b_: 1.0}
bmI.free()

# ------------------------------------------------------------------ 4. desfaz a pose (LBS inverso) -> pose T do jogo
AW = arm.matrix_world; AWi = AW.inverted()
skin_m = {pb.name: pb.matrix @ arm.data.bones[pb.name].matrix_local.inverted() for pb in arm.pose.bones}
for v, w in zip(a.data.vertices, Wv):
    M = Matrix(((0, 0, 0, 0),) * 4)
    for g, x in w.items():
        Mb = skin_m.get(g)
        if Mb is None: continue
        M = M + Mb * x if False else Matrix([[M[r][c] + Mb[r][c] * x for c in range(4)] for r in range(4)])
    if abs(M.determinant()) < 1e-6: continue
    v.co = AW @ (M.inverted() @ (AWi @ v.co))
for pb in arm.pose.bones: pb.rotation_quaternion = (1, 0, 0, 0)
arm.data.pose_position = 'REST'; bpy.context.view_layer.update()
# capacete centrado na cabeça (o Meshy põe a cabeça mais à frente que o manequim): desloca pelo peso da cabeça
hidx = {g.index for g in body.vertex_groups if g.name in ("mixamorig:Head", "headfront", "mixamorig:HeadTop_End")}
hv = [bw @ v.co for v in body.data.vertices if v.groups and max(v.groups, key=lambda g: g.weight).group in hidx]
hz0, hz1 = min(v.z for v in hv), max(v.z for v in hv); hy = (min(v.y for v in hv) + max(v.y for v in hv)) / 2
cap = [v.co for v, w in zip(a.data.vertices, Wv) if w.get("mixamorig:Head", 0) > 0.5 and hz0 < v.co.z < hz0 + 0.6 * (hz1 - hz0)]
if cap:
    cy = (min(c.y for c in cap) + max(c.y for c in cap)) / 2; DY = max(-0.15, min(0.15, hy - cy)); print("capacete dy", round(DY, 3), flush=True)
    for v, w in zip(a.data.vertices, Wv):
        k = w.get("mixamorig:Head", 0) + w.get("mixamorig:Neck", 0) * 0.5
        if k: v.co.y += DY * min(1.0, k)
for g in set(x for w in Wv for x in w): a.vertex_groups.new(name=g)
for v, w in zip(a.data.vertices, Wv):
    for g, x in w.items(): a.vertex_groups[g].add([v.index], x, 'REPLACE')

# ------------------------------------------------------------------ 5. separa em peças pelo osso dominante de cada face
gi = {g.index: g.name for g in a.vertex_groups}
def dom(vi):
    w = Wv[vi]; return max(w.items(), key=lambda kv: kv[1])[0] if w else spine2
mesh = a.data
JOELHO = P["LeftLeg"].z + 0.02; CINTURA = P["Hips"].z + 0.09
face_peca = []
for f in mesh.polygons:
    votos = {}
    for vi in f.vertices: pc = peca_de(dom(vi)); votos[pc] = votos.get(pc, 0) + 1
    pc = max(votos.items(), key=lambda kv: kv[1])[0]
    # cortes por altura (padrão MU, pedido do dono): peitoral acima da cintura, calça da cintura ao joelho, botas do joelho para baixo
    if pc in ("peitoral", "calca", "botas"):
        c = f.center
        if abs(c.x) < 0.32 or pc != "peitoral":
            pc = "botas" if c.z < JOELHO else ("calca" if c.z < CINTURA else "peitoral")
    face_peca.append(pc)
# limpeza: pedacinhos soltos (ilhas pequenas) que caíram na peça errada voltam para a peça onde realmente encostam
bmL = bmesh.new(); bmL.from_mesh(mesh); bmL.faces.ensure_lookup_table()
def ilhas_da_peca(pc):
    vis, comps = set(), []
    for f in bmL.faces:
        if face_peca[f.index] != pc or f.index in vis: continue
        pilha, comp = [f], []; vis.add(f.index)
        while pilha:
            g = pilha.pop(); comp.append(g.index)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in vis and face_peca[h.index] == pc: vis.add(h.index); pilha.append(h)
        comps.append(comp)
    return comps
# capacete = só o que está na cabeça: abaixo do queixo e para o lado (topo de ombreira, gola, pena do ombro) vai pro peitoral
QUEIXO = P["Neck"].z + 0.06
for f in bmL.faces:
    if face_peca[f.index] == "capacete":
        c = f.calc_center_median()
        if (c.z < QUEIXO and abs(c.x) > 0.11) or (abs(c.x) > 0.15 and c.z < P["Head"].z + 0.07): face_peca[f.index] = "peitoral"
        elif c.z < P["Neck"].z - 0.01: face_peca[f.index] = "peitoral"
for passo in range(2):
    pequenas, grandes = [], []
    for pc in PECAS:
        comps = ilhas_da_peca(pc)
        if not comps: continue
        tot = sum(len(c) for c in comps); maior = max(len(c) for c in comps)
        for c in comps:
            (pequenas if (len(c) < 0.12 * maior and len(c) < 0.06 * tot) else grandes).append((pc, c))
    kdF = kdtree.KDTree(sum(len(c) for _, c in grandes)); dono = []
    for pc, c in grandes:
        for fi in c: kdF.insert(bmL.faces[fi].calc_center_median(), len(dono)); dono.append(pc)
    kdF.balance()
    for pc, c in pequenas:
        votos = {}
        for fi in c:
            _, idx, _ = kdF.find(bmL.faces[fi].calc_center_median())
            if idx is not None: votos[dono[idx]] = votos.get(dono[idx], 0) + 1
        if votos:
            novo = max(votos.items(), key=lambda kv: kv[1])[0]
            for fi in c: face_peca[fi] = novo
# ilhas do capacete que não encostam no meio da cabeça (pena/espinho/gola soltos ao lado ou embaixo) vão pro peitoral
for c in ilhas_da_peca("capacete"):
    cs = [bmL.faces[fi].calc_center_median() for fi in c]
    if (min(abs(q.x) for q in cs) > 0.1 and max(q.z for q in cs) < P["Head"].z + 0.08) or max(q.z for q in cs) < P["Head"].z + 0.02:
        for fi in c: face_peca[fi] = "peitoral"
# de novo a regra do capacete (a limpeza pode ter devolvido algum pedaço)
for f in bmL.faces:
    if face_peca[f.index] == "capacete":
        c = f.calc_center_median()
        if (c.z < QUEIXO and abs(c.x) > 0.11) or (abs(c.x) > 0.15 and c.z < P["Head"].z + 0.07) or c.z < P["Neck"].z - 0.01: face_peca[f.index] = "peitoral"
bmL.free()
pecas = []
for pc in PECAS:
    ids = [i for i, p in enumerate(face_peca) if p == pc]
    if not ids: continue
    o = a.copy(); o.data = a.data.copy(); bpy.context.collection.objects.link(o); o.name = f"{AID}_{pc}"
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    keep = set(ids); bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    # migalhas: ilhas minúsculas longe de tudo são apagadas
    bm.faces.ensure_lookup_table(); vis_, ilhas_ = set(), []
    for f0 in bm.faces:
        if f0.index in vis_: continue
        pil, il = [f0], []; vis_.add(f0.index)
        while pil:
            g = pil.pop(); il.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in vis_: vis_.add(h.index); pil.append(h)
        ilhas_.append(il)
    TF = len(bm.faces); apagar = []
    bm.verts.ensure_lookup_table()
    kda = kdtree.KDTree(len(bm.verts))
    for v in bm.verts: kda.insert(v.co, v.index)
    kda.balance()
    for il in ilhas_:
        if len(il) >= max(30, 0.006 * TF): continue
        meus = {v.index for f_ in il for v in f_.verts}
        amostra = list(meus)[::max(1, len(meus) // 12)]
        perto = any(idx not in meus for vi in amostra for (_, idx, _) in kda.find_range(bm.verts[vi].co, 0.025))
        if not perto: apagar += il      # só some se não tiver NADA por perto (pelo/penas em tufo ficam)
    if apagar: bmesh.ops.delete(bm, geom=list(set(apagar)), context='FACES_ONLY')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free()
    alvo = ALVO_TRIS[pc]
    K._decimate(o, alvo)
    o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
    md = o.modifiers.new("arm", 'ARMATURE'); md.object = arm
    pecas.append(o)
bpy.data.objects.remove(a)
# texturas 4k -> 2048 (cor/normal) para pesar menos
for im in bpy.data.images:
    if im.size[0] > 2048: im.scale(2048, 2048)
    if im.packed_file is None and im.source == 'FILE': pass
info = dict(nome=cfg["nome"], nivel=cfg.get("nivel"), pecas={o.name.replace(AID + "_", ""): sum(len(p.vertices) - 2 for p in o.data.polygons) for o in pecas},
            pose_da_fonte=dict(braco_graus=BRACO, antebraco_graus=ANTEBRACO), esqueleto="manequim_masc (mixamorig, mesma pose de repouso)")
B.exportar(AID, arm, pecas, info)

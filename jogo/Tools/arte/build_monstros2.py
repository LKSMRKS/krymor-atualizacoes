"""Monstros da lista do dono, no corpo do personagem principal (manequim_masc, mesmo esqueleto e clipes Mixamo):
  mo_rastejante_gesso  - Rastejante de Gesso: corpo magro de gesso escorrido, cabeca cega alongada com boca vertical de dentes,
                         lascas de gesso nas costas, dedos-garra compridos. (pequeno: 1,4 m no jogo)
  mo_gargulito         - Gargulito de Estalagmite: pedra cinza de caverna, chifres de estalagmite, asinhas de pedra, rabo, olhos vermelhos. (1,1 m)
  mo_golem_cal_viva    - Golem de Cal Viva: corpo reforcado, blocos de cal nos ombros/bracos/peito/joelhos, rachaduras incandescentes. (2,6 m)
  mo_espantalho        - Espantalho: corpo de estopa, cabeca de saco costurada, chapeu de palha, tufos de palha nos pulsos/tornozelos/pescoco. (1,9 m)
Uso: python3 build_monstros2.py <saida> [ids...]"""
import sys, os, math, random, json
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh
from mathutils import Vector, Matrix
import itemkit as K, org
import build_mortos_cal as B

OUT = sys.argv[1]; SEL = sys.argv[2:]
B.OUT = OUT

def fix(o):
    """assa a transformacao na malha (evita o bug do cubo gigante)"""
    bpy.context.view_layer.update(); mw = o.matrix_world.copy(); o.parent = None; o.matrix_parent_inverse = Matrix(); o.matrix_world = Matrix()
    o.data.transform(mw); return o

def rigid(o, arm, bone, mat=None):
    if mat is not None and not o.data.materials: o.data.materials.append(mat)
    K.apply_all(o); fix(o); B.skin_to(o, arm, lambda p: {f"mixamorig:{bone}": 1.0}); return o

def finalizar(vid, arm, body, parts, info, tex=1024, emit=False):
    """junta as pecas opacas numa malha, assa texturas e exporta (pecas especiais - emissivas/vidro - ficam separadas)"""
    opac = [o for o in parts if o.data.materials and (emit or not K.is_special(o.data.materials[0]))]
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in opac: fix(o)
    for o in opac: o.select_set(True)
    bpy.context.view_layer.objects.active = opac[0]; bpy.ops.object.join(); ex = bpy.context.view_layer.objects.active; ex.name = vid + "_partes"
    ex.parent = arm; ex.matrix_parent_inverse = arm.matrix_world.inverted()
    while len([m for m in ex.modifiers if m.type == 'ARMATURE']) > 1: ex.modifiers.remove([m for m in ex.modifiers if m.type == 'ARMATURE'][-1])
    K._decimate(ex, 14000)
    if not any(m.type == 'ARMATURE' for m in ex.modifiers): m = ex.modifiers.new("arm", 'ARMATURE'); m.object = arm
    K._bake(body, tex, emit); K._bake(ex, tex, emit)
    resto = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o not in (body, ex)]
    for o in resto:
        if o.parent is None: fix(o); B.skin_to(o, arm, lambda p: {"mixamorig:Head": 1.0})
    B.exportar(vid, arm, [body, ex] + resto, info)

def sem_cabeca(body): B.keep_faces(body, lambda f, doms: not any(d in B.HEAD for d in doms))

def garras(P, T, arm, mat, n=4, L=0.13, r=0.009):
    out = []
    for side in ("Left", "Right"):
        hd, tl = P[side + "Hand"], T[side + "Hand"]; d = (tl - hd).normalized()
        for k in range(n):
            off = Vector((0, -0.03 + k * 0.02, -0.005 * abs(k - 1.5)))
            base = tl + d * 0.05 + off; tip = base + (d * L + Vector((0, 0, -L * 0.4)))
            c = org.spike(f"garra{side}{k}", base, tip, r); out.append(rigid(c, arm, side + "Hand", mat))
    return out

def olhos(h, arm, cor, sep=0.035, fwd=-0.095, up=0.01, r=0.018, forca=6.0):
    em = K.m_emit("olho_" + cor, cor, forca); out = []
    for s in (-1, 1):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=h + Vector((s * sep, fwd, up)), segments=12, ring_count=6)
        out.append(rigid(bpy.context.object, arm, "Head", em))
    return out

# ======================================================================= 1. Rastejante de Gesso
def rastejante():
    arm, body, P, T = B.importar(); sem_cabeca(body)
    B.offset_normal(body, -0.01)
    k = body.matrix_world.to_scale().x
    pele = B.M_gesso("gesso_rastejante", k); pp = B.M_gesso("gesso_rastejante_p", 1.0)
    body.data.materials.clear(); body.data.materials.append(pele)
    for p in body.data.polygons: p.use_smooth = True
    parts = []
    # cabeca cega, alongada para frente e para cima, com boca vertical
    h = P["Head"] + Vector((0, -0.02, 0.09))
    hd = org.fuse("cabeca_r", [org.blob("c", h + Vector((0, 0.01, 0.02)), (0.075, 0.12, 0.1)),
                               org.blob("focinho", h + Vector((0, -0.09, -0.02)), (0.06, 0.07, 0.085)),
                               org.limb("pesc", [(P["Neck"], 0.045), (h + Vector((0, 0.02, -0.07)), 0.04)])], voxel=0.005)
    org.displace(hd, 0.004, 0.02, 'CLOUDS'); parts.append(rigid(hd, arm, "Head", pp))
    escuro = K.m_plain("boca_rastejante", "#120806", 1.0); dente = K.m_bone("dente_rastejante", "#e8e0cc", "#7a6040", 30)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.03, location=h + Vector((0, -0.15, -0.03)), segments=16, ring_count=8); bo = bpy.context.object
    bo.data.transform(Matrix.Diagonal((0.55, 0.35, 1.6, 1))); parts.append(rigid(bo, arm, "Head", escuro))
    for i in range(10):     # dentes em volta da fenda, apontando para dentro
        a = i / 10 * math.tau; c = h + Vector((0, -0.158, -0.03))
        base = c + Vector((math.cos(a) * 0.022, 0, math.sin(a) * 0.05)); tip = c + Vector((math.cos(a) * 0.004, -0.008, math.sin(a) * 0.015))
        parts.append(rigid(org.spike(f"dente{i}", base, tip, 0.005), arm, "Head", dente))
    # lascas de gesso nas costas (espinha)
    for i, bn in enumerate(("Spine", "Spine1", "Spine2", "Spine2", "Neck")):
        b0 = P[bn] + Vector((0, 0.09 - 0.01 * i, 0.03 * (i == 3))); tip = b0 + Vector((0, 0.09 + 0.02 * (i % 2), 0.06))
        parts.append(rigid(org.spike(f"lasca{i}", b0, tip, 0.025), arm, bn, pp))
        for s in (-1, 1):
            b1 = b0 + Vector((s * 0.06, -0.01, -0.02)); parts.append(rigid(org.spike(f"lascaL{i}{s}", b1, b1 + Vector((s * 0.05, 0.06, 0.03)), 0.015), arm, bn, pp))
    parts += garras(P, T, arm, K.m_bone("garra_gesso", "#efe9dc", "#8a8070", 20), n=4, L=0.16, r=0.008)
    # escorrido de gesso pingando dos antebracos
    for side in ("Left", "Right"):
        a = P[side + "ForeArm"].lerp(P[side + "Hand"], 0.5)
        for j in range(3):
            b0 = a + Vector((0, -0.01 + j * 0.02, -0.03)); parts.append(rigid(K.tube(f"pingo{side}{j}", [b0, b0 + Vector((0, 0, -0.04 - 0.02 * j))], 0.008, pp, taper=[1, 0.3]), arm, side + "ForeArm"))
    finalizar("mo_rastejante_gesso", arm, body, parts, dict(nome="Rastejante de Gesso", altura_m=1.4, corpo="manequim_masc afinado",
              notas="cego (sem olhos), boca vertical com anel de dentes, lascas nas costas, garras de 16 cm. Sugestão: anda agachado (Speed 1.2, escala 0.78)"))

# ======================================================================= 2. Gargulito de Estalagmite
def gargulito():
    arm, body, P, T = B.importar(); sem_cabeca(body)
    B.offset_normal(body, 0.012)
    k = body.matrix_world.to_scale().x
    pedra = K.m_rock("pedra_gargulito", "#55545a", "#8c8a8e", scale=k * 3, flecks="#9fb0b8")
    pp = K.m_rock("pedra_gargulito_p", "#55545a", "#8c8a8e", scale=3, flecks="#9fb0b8")
    body.data.materials.clear(); body.data.materials.append(pedra)
    for p in body.data.polygons: p.use_smooth = True
    parts = []
    h = P["Head"] + Vector((0, -0.02, 0.1))
    hd = org.fuse("cabeca_g", [org.blob("c", h, (0.1, 0.1, 0.095)), org.blob("focinho", h + Vector((0, -0.09, -0.04)), (0.07, 0.07, 0.05)),
                               org.blob("queixo", h + Vector((0, -0.07, -0.085)), (0.06, 0.05, 0.03)),
                               org.blob("testa", h + Vector((0, -0.06, 0.04)), (0.085, 0.04, 0.03)),
                               org.limb("pesc", [(P["Neck"], 0.06), (h + Vector((0, 0.02, -0.07)), 0.055)])], voxel=0.005)
    org.displace(hd, 0.006, 0.025, 'CLOUDS'); parts.append(rigid(hd, arm, "Head", pp))
    for s in (-1, 1):
        parts.append(rigid(org.spike(f"orelha{s}", h + Vector((s * 0.09, 0.0, 0.02)), h + Vector((s * 0.17, 0.04, 0.09)), 0.03), arm, "Head", pp))
        # chifres de estalagmite (varios, tortos)
        for j, (dx, L) in enumerate(((0.04, 0.2), (0.075, 0.13))):
            b0 = h + Vector((s * dx, -0.02 + j * 0.03, 0.07)); tip = b0 + Vector((s * 0.03, 0.06, L))
            c = org.spike(f"chifre{s}{j}", b0, tip, 0.028 - j * 0.008); org.displace(c, 0.004, 0.02, 'CLOUDS'); parts.append(rigid(c, arm, "Head", pp))
        # presas
        parts.append(rigid(org.spike(f"presa{s}", h + Vector((s * 0.035, -0.13, -0.06)), h + Vector((s * 0.04, -0.14, -0.0)), 0.009), arm, "Head", K.m_bone("presa_garg", "#d8d2c4", "#6a6458", 20)))
    parts += olhos(h + Vector((0, 0.0, 0.0)), arm, "#ff2a10", sep=0.042, fwd=-0.085, up=0.0, r=0.016, forca=8.0)
    # estalagmites nos ombros e nas costas
    for side in ("Left", "Right"):
        sh = P[side + "Arm"]; s = 1 if side == "Left" else -1
        for j in range(3):
            b0 = sh + Vector((s * 0.02 * j, 0.01 * j, 0.04)); parts.append(rigid(org.spike(f"ombro{side}{j}", b0, b0 + Vector((s * 0.03, 0.02, 0.1 - 0.025 * j)), 0.025), arm, side + "Arm", pp))
    # asinhas de pedra (membrana grossa em leque) presas no Spine2
    sp = P["Spine2"] + Vector((0, 0.1, 0.05))
    for s in (-1, 1):
        pts = [sp + Vector((s * 0.05, 0, 0)), sp + Vector((s * 0.25, 0.12, 0.25)), sp + Vector((s * 0.45, 0.16, 0.1)), sp + Vector((s * 0.42, 0.16, -0.08)),
               sp + Vector((s * 0.3, 0.13, -0.05)), sp + Vector((s * 0.22, 0.1, -0.16)), sp + Vector((s * 0.06, 0.03, -0.12))]
        bm = bmesh.new(); vs = [bm.verts.new(p) for p in pts]; bm.faces.new(vs if s > 0 else list(reversed(vs)))
        me = bpy.data.meshes.new(f"asa{s}"); bm.to_mesh(me); bm.free(); w = bpy.data.objects.new(f"asa{s}", me); bpy.context.collection.objects.link(w)
        K.mod(w, 'SOLIDIFY', thickness=0.025); K.mod(w, 'SUBSURF', levels=2); K.displace(w, 0.01, 0.04, 'CLOUDS')
        parts.append(rigid(w, arm, "Spine2", pp))
        for j in (1, 2, 3):   # nervuras
            parts.append(rigid(K.tube(f"nervura{s}{j}", [pts[0], pts[j]], 0.014, pp, taper=[1, 0.4]), arm, "Spine2"))
    # rabo de pedra
    hp = P["Hips"]
    parts.append(rigid(org.limb("rabo", [(hp + Vector((0, 0.08, -0.05)), 0.04), (hp + Vector((0, 0.25, -0.25)), 0.03), (hp + Vector((0.05, 0.38, -0.5)), 0.02), (hp + Vector((0.08, 0.42, -0.62)), 0.006)]), arm, "Hips", pp))
    parts += garras(P, T, arm, K.m_bone("garra_garg", "#3a3a3e", "#1a1a1c", 20), n=3, L=0.07, r=0.009)
    finalizar("mo_gargulito", arm, body, parts, dict(nome="Gargulito de Estalagmite", altura_m=1.1, corpo="manequim_masc reforçado leve, em pedra",
              notas="chifres de estalagmite, asinhas de pedra (não voa), rabo, olhos vermelhos brilhando. Usa os clipes do manequim"))

# ======================================================================= 3. Golem de Cal Viva
def M_cal_viva(name, k=1.0):
    b = K.NB(name)
    n1 = b.noise(3 * k, 6, 0.6); cr = b.voronoi(6 * k, 'DISTANCE_TO_EDGE'); cr2 = b.voronoi(15 * k, 'DISTANCE_TO_EDGE')
    crack = b.math('ADD', b.math('LESS_THAN', cr, 0.035), b.math('MULTIPLY', b.math('LESS_THAN', cr2, 0.02), 0.5))
    c = b.ramp(n1, [(0.25, "#8f877a"), (0.6, "#d8d0bc"), (0.9, "#f2ecdc")])
    c = b.mix(b.math('MULTIPLY', K.cavity(b, 0.08), 1.6, clamp=True), c, "#4a4236")
    c = b.mix(b.math('MULTIPLY', crack, 0.9, clamp=True), c, "#ff9a2a")
    h = b.math('SUBTRACT', b.math('MULTIPLY', b.noise(25 * k, 5), 0.4), b.math('MULTIPLY', crack, 0.8))
    return b.set(base=c, rough=0.9, normal=b.bump(h, 0.8, 0.01), emit_col="#ff8a20", emit=b.math('MULTIPLY', crack, 4.0))

def golem():
    arm, body, P, T = B.importar(); sem_cabeca(body)
    B.offset_normal(body, 0.035)
    k = body.matrix_world.to_scale().x
    pele = M_cal_viva("cal_viva", k); pp = M_cal_viva("cal_viva_p", 1.0)
    body.data.materials.clear(); body.data.materials.append(pele)
    for p in body.data.polygons: p.use_smooth = True
    parts = []; rnd = random.Random(11)
    def bloco(nome, c, r, bone, sq=(1, 1, 1)):
        o = org.blob(nome, c, (r * sq[0], r * sq[1], r * sq[2]), seg=12, rings=8)
        org.displace(o, r * 0.25, r * 0.9, 'VORONOI' if False else 'CLOUDS'); K.mod(o, 'DECIMATE', ratio=0.35)
        parts.append(rigid(o, arm, bone, pp))
    # cabeca: pedregulho afundado entre os ombros
    h = P["Neck"] + Vector((0, -0.04, 0.1))
    bloco("cabeca", h, 0.11, "Head", (1.1, 1.0, 0.85))
    parts += olhos(h, arm, "#ffcf5a", sep=0.045, fwd=-0.1, up=0.01, r=0.02, forca=10.0)
    for side, s in (("Left", 1), ("Right", -1)):
        bloco(f"ombro{side}", P[side + "Arm"] + Vector((s * 0.03, 0.0, 0.07)), 0.15, side + "Arm", (1.0, 1.0, 0.8))
        bloco(f"ombro2{side}", P[side + "Arm"] + Vector((s * 0.12, 0.03, 0.12)), 0.08, side + "Arm")
        f0 = P[side + "ForeArm"]; f1 = P[side + "Hand"]
        bloco(f"antebraco{side}", f0.lerp(f1, 0.55) + Vector((0, 0, 0.03)), 0.1, side + "ForeArm", (1.5, 0.9, 0.9))
        bloco(f"punho{side}", f1 + Vector((s * 0.05, 0, 0)), 0.09, side + "Hand")
        bloco(f"joelho{side}", P[side + "Leg"] + Vector((0, -0.06, 0)), 0.08, side + "Leg")
        bloco(f"canela{side}", P[side + "Leg"].lerp(P[side + "Foot"], 0.5) + Vector((0, -0.04, 0)), 0.08, side + "Leg", (1, 0.9, 1.6))
        bloco(f"coxa{side}", P[side + "UpLeg"].lerp(P[side + "Leg"], 0.5) + Vector((s * 0.03, -0.02, 0)), 0.09, side + "UpLeg", (1, 1, 1.4))
    bloco("peito", P["Spine2"] + Vector((0, -0.1, 0.02)), 0.14, "Spine2", (1.4, 0.6, 0.9))
    bloco("costas", P["Spine1"] + Vector((0, 0.11, 0.06)), 0.15, "Spine1", (1.3, 0.7, 1.0))
    bloco("ventre", P["Spine"] + Vector((0, -0.08, 0)), 0.1, "Spine", (1.3, 0.7, 0.8))
    # cristais de cal saindo das costas
    cr = K.m_crystal("cristal_cal", "#fff0c8", "#ffb040", 2.0)
    for j in range(5):
        b0 = P["Spine2"] + Vector((rnd.uniform(-0.12, 0.12), 0.15, rnd.uniform(-0.05, 0.12)))
        c = K.crystal(f"cristal{j}", cr, rnd.uniform(0.025, 0.04), rnd.uniform(0.12, 0.22), seed=j)
        c.location = b0; c.rotation_euler = (math.radians(rnd.uniform(-60, -30)), math.radians(rnd.uniform(-25, 25)), 0)
        rigid(c, arm, "Spine2")
    finalizar("mo_golem_cal_viva", arm, body, parts, dict(nome="Golem de Cal Viva", altura_m=2.6, corpo="manequim_masc reforçado (+3,5 cm) com blocos de cal",
              notas="chefe da caverna de cal. Rachaduras laranja emissivas (cal viva quente), cristais de cal nas costas. OmniLight laranja fraco no peito fica ótimo"), emit=True)

# ======================================================================= 4. Espantalho
def espantalho():
    arm, body, P, T = B.importar(); sem_cabeca(body)
    B.offset_normal(body, 0.014)
    k = body.matrix_world.to_scale().x
    estopa = K.m_plain("estopa", "#8a7450", 0.95, bumpscale=180, scale=k)
    estopa_p = K.m_plain("estopa_p", "#9a8460", 0.95, bumpscale=180)
    body.data.materials.clear(); body.data.materials.append(estopa)
    for p in body.data.polygons: p.use_smooth = True
    parts = []
    # remendos (copias de pedacos do corpo, outra cor)
    remendo = K.m_plain("remendo", "#5a3a2a", 0.95, bumpscale=1.5)
    rnd = random.Random(8)
    for (bn, off) in (("Spine1", Vector((0.06, -0.12, 0))), ("LeftUpLeg", Vector((0.0, -0.08, -0.12))), ("RightArm", Vector((-0.12, -0.03, 0)))):
        bpy.ops.mesh.primitive_plane_add(size=0.09, location=P[bn] + off, rotation=(math.radians(90), 0, 0)); r_ = bpy.context.object
        K.mod(r_, 'SOLIDIFY', thickness=0.004); parts.append(rigid(r_, arm, bn, remendo))
    # cabeca de saco
    h = P["Head"] + Vector((0, -0.01, 0.1))
    hd = org.fuse("saco", [org.blob("c", h, (0.11, 0.1, 0.12)), org.blob("dobra", h + Vector((0, 0.0, -0.12)), (0.07, 0.07, 0.04)),
                           org.limb("pesc", [(P["Neck"], 0.05), (h + Vector((0, 0, -0.09)), 0.05)])], voxel=0.006)
    org.displace(hd, 0.006, 0.03, 'CLOUDS'); parts.append(rigid(hd, arm, "Head", estopa_p))
    linha = K.m_plain("linha_preta", "#0e0a08", 0.8)
    for s in (-1, 1):      # olhos em X costurados
        c = h + Vector((s * 0.04, -0.103, 0.02))
        for d in (1, -1): parts.append(rigid(K.tube(f"x{s}{d}", [c + Vector((-0.016, 0, -0.016 * d)), c + Vector((0.016, 0, 0.016 * d))], 0.0035, linha), arm, "Head"))
    for i in range(7):     # boca costurada
        x = -0.045 + i * 0.015; c = h + Vector((x, -0.1 + abs(x) * 0.3, -0.045 + abs(x) * 0.2))
        parts.append(rigid(K.tube(f"ponto{i}", [c + Vector((0, 0, 0.012)), c + Vector((0, 0, -0.012))], 0.003, linha), arm, "Head"))
    parts.append(rigid(K.tube("boca", [h + Vector((-0.05, -0.098, -0.035)), h + Vector((0, -0.106, -0.048)), h + Vector((0.05, -0.098, -0.035))], 0.003, linha), arm, "Head"))
    corda = K.m_plain("corda", "#6a5232", 0.95, bumpscale=60)
    am = K.lathe("amarra", [(0.056, -0.012), (0.063, 0.0), (0.056, 0.012)], 16, corda, close_top=False, close_bot=False); am.location = h + Vector((0, 0, -0.11)); parts.append(rigid(am, arm, "Head"))
    # chapeu de palha largo e torto
    palha = K.m_plain("palha", "#c8a85a", 0.9, bumpscale=90)
    ch = K.lathe("chapeu", [(0.0, 0.16), (0.07, 0.155), (0.1, 0.12), (0.11, 0.04), (0.13, 0.02), (0.26, 0.0), (0.27, -0.012), (0.12, 0.012), (0.095, 0.11), (0.0, 0.14)], 24, palha)
    K.displace(ch, 0.012, 0.08, 'CLOUDS'); ch.location = h + Vector((0, 0.01, 0.08)); ch.rotation_euler = (math.radians(-8), math.radians(10), 0); parts.append(rigid(ch, arm, "Head"))
    # tufos de palha: pescoco, pulsos, tornozelos, cintura
    def tufo(c, d, bone, n=10, L=0.09, spread=0.05):
        for i in range(n):
            a = rnd.uniform(0, math.tau); b0 = c + Vector((math.cos(a) * spread * 0.6, math.sin(a) * spread * 0.6, 0))
            dd = (d + Vector((math.cos(a), math.sin(a), 0)) * 0.6).normalized()
            parts.append(rigid(org.spike(f"palha{bone}{i}", b0, b0 + dd * rnd.uniform(L * 0.6, L), 0.006), arm, bone, palha))
    tufo(P["Neck"] + Vector((0, 0, 0.02)), Vector((0, 0, -0.4)), "Neck", 12, 0.07, 0.06)
    for side in ("Left", "Right"):
        hd_, tl_ = P[side + "Hand"], T[side + "Hand"]; d = (tl_ - hd_).normalized()
        tufo(hd_ - d * 0.02, d, side + "Hand", 10, 0.1, 0.04)
        tufo(P[side + "Foot"] + Vector((0, 0, 0.06)), Vector((0, 0, -1)), side + "Leg", 10, 0.08, 0.06)
    tufo(P["Hips"] + Vector((0, 0, 0.08)), Vector((0, 0, -1)), "Hips", 16, 0.1, 0.16)
    # corda na cintura
    cnt = K.tube("cinto", [P["Hips"] + Vector((math.cos(a) * 0.17, math.sin(a) * 0.12, 0.1)) for a in [i / 16 * math.tau for i in range(17)]], 0.012, corda)
    parts.append(rigid(cnt, arm, "Hips"))
    parts += olhos(h, arm, "#ffb020", sep=0.04, fwd=-0.1, up=0.02, r=0.008, forca=4.0)   # brilho fraquinho por tras do X
    finalizar("mo_espantalho", arm, body, parts, dict(nome="Espantalho", altura_m=1.9, corpo="manequim_masc em estopa",
              notas="cabeça de saco costurada (olhos em X com brilho laranja fraco atrás), chapéu de palha, tufos de palha. Fica de pé parado no campo até alguém chegar perto"))

if __name__ == "__main__":
    FEITOS = dict(mo_rastejante_gesso=rastejante, mo_gargulito=gargulito, mo_golem_cal_viva=golem, mo_espantalho=espantalho)
    for vid, fn in FEITOS.items():
        if SEL and vid not in SEL: continue
        try: fn()
        except Exception as e:
            import traceback; traceback.print_exc(); print("FALHOU", vid, e, flush=True)

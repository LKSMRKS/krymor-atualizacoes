"""Regiao 1 (Porto/Costa/Galeria, niveis 1-14): humanoides refeitos no corpo do manequim, cada um com silhueta propria.
Uso: python3 build_regiao1.py <saida> [ids...]"""
import sys, os, math, random
sys.path.insert(0, "/home/claude/cinzas/tools")
OUT = sys.argv[1]; SEL = sys.argv[2:]
sys.argv = [sys.argv[0], OUT]
import bpy, bmesh
from mathutils import Vector, Matrix
import itemkit as K, org
import build_mortos_cal as B
import kit_humanoide as H
B.OUT = OUT

def base(grossura=0.0, sem_cabeca=False):
    arm, body, P, T = B.importar()
    bm = bmesh.new(); bm.from_mesh(body.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.05); bm.to_mesh(body.data); bm.free()   # solda (vem com faces soltas)
    if sem_cabeca: B.keep_faces(body, lambda f, doms: not any(d in B.HEAD for d in doms))
    # alisa as juntas de boneco de madeira (anéis dos cotovelos/joelhos) antes de engrossar
    sm = body.modifiers.new("liso", 'SMOOTH'); sm.factor = 0.9; sm.iterations = 12
    bpy.context.view_layer.objects.active = body; bpy.ops.object.modifier_move_to_index(modifier="liso", index=0); bpy.ops.object.modifier_apply(modifier="liso")
    if grossura: B.offset_normal(body, grossura)
    k = body.matrix_world.to_scale().x
    for p in body.data.polygons: p.use_smooth = True
    return arm, body, P, T, k

def pele(body, mat):
    body.data.materials.clear(); body.data.materials.append(mat)

def tecido(nome, cor, k=1.0, bump=1.6): return K.m_plain(nome, cor, 0.92, bumpscale=bump * 100 if k < 0.1 else bump, scale=k)

# ===================================================================== 1. Saqueador da Vigilia
def saqueador():
    arm, body, P, T, k = base(0.004)
    pele(body, K.m_plain("pano_preto_corpo", "#1a1716", 0.9, bumpscale=160, scale=k))
    parts = []
    escuro_k = tecido("pano_escuro_k", "#24201e", k)
    parts.append(H.roupa(body, arm, ("LeftArm", "RightArm", "LeftForeArm", "RightForeArm"), escuro_k, 0.008, nome="mangas"))
    parts.append(H.roupa(body, arm, ("Hips", "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg"), escuro_k, 0.008, nome="calca"))
    bordo = tecido("bordo", "#5a1a22"); bordo_k = tecido("bordo_k", "#5a1a22", k)
    couro = K.m_leather("couro_marrom", "#4a2e1c", "#160c06", scale=6); couro_k = K.m_leather("couro_marrom_k", "#4a2e1c", "#160c06", scale=k * 6)
    parts.append(H.roupa(body, arm, ("LeftForeArm", "RightForeArm"), couro_k, 0.014, nome="bracadeira"))
    parts.append(H.roupa(body, arm, ("LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase"), couro_k, 0.012, zmax=P["LeftLeg"].z - 0.05, nome="botas"))
    parts.append(H.roupa(body, arm, ("Spine", "Spine1", "Spine2", "Hips"), bordo_k, 0.008, zmin=P["Hips"].z - 0.12, rasgo=0.04, nome="tunica"))
    parts.append(H.capuz(arm, P, bordo, ponta=0.02))
    parts.append(H.capa(arm, P, bordo, 0.36, 1.05, desfiada=True))
    cinto = K.tube("cinto", [P["Hips"] + Vector((math.cos(a) * 0.135, math.sin(a) * 0.1, 0.07)) for a in [i / 20 * math.tau for i in range(21)]], 0.018, couro)
    parts.append(H.rigid(cinto, arm, "Hips"))
    parts += H.olhos(arm, P, "#ff5a1a", 8.0, up=0.115)
    arma = H.marcar_pronta(H.arma(arm, P, "arma_machado_mao_enferrujada"))
    H.finalizar("mo_saqueador_vigilia", arm, body, parts + arma, dict(nome="Saqueador da Vigília", altura_m=1.85, arma="machado de mão enferrujado (mão direita)",
                notas="capuz e capa bordô desfiada, couro preto, olhos de brasa sob o capuz"))

# ===================================================================== 2. Acolito das Cinzas
def acolito():
    arm, body, P, T, k = base(0.0)
    pele(body, K.m_plain("pele_cinza", "#9a948c", 0.8, scale=k))
    parts = []
    cinza = tecido("veste_cinza", "#5a5650"); cinza_k = tecido("veste_cinza_k", "#5a5650", k)
    parts.append(H.roupa(body, arm, ("Spine", "Spine1", "Spine2", "Hips", "LeftShoulder", "RightShoulder", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm"), cinza_k, 0.016, nome="tunica"))
    parts.append(H.saia(arm, P, cinza, P["Hips"].z + 0.08, 0.05, 0.17, 0.3, nome="manto"))
    parts.append(H.capuz(arm, P, cinza, ponta=0.06))
    brasa = K.m_plain("brasa_faixa", "#7a3010", 0.7)
    parts.append(H.rigid(K.tube("corda", [P["Hips"] + Vector((math.cos(a) * 0.15, math.sin(a) * 0.12, 0.08)) for a in [i / 20 * math.tau for i in range(21)]], 0.014, brasa), arm, "Hips"))
    # cinzas penduradas: tiras de pano queimado no peito
    for i in range(5):
        b0 = P["Spine2"] + Vector((-0.08 + i * 0.04, -0.13, 0.02)); parts.append(H.rigid(K.tube(f"tira{i}", [b0, b0 + Vector((0, -0.01, -0.22 - 0.04 * (i % 2)))], 0.012, brasa, taper=[1, 0.6]), arm, "Spine2"))
    parts += H.olhos(arm, P, "#ff8a2a", 8.0, up=0.115)
    arma = H.marcar_pronta(H.arma(arm, P, "arma_varinha_bruxa_n1"))
    H.finalizar("mo_acolito_cinzas", arm, body, parts + arma, dict(nome="Acólito das Cinzas", altura_m=1.8, arma="varinha (mão direita)",
                notas="manto cinza até os pés, capuz pontudo, faixas queimadas, olhos de brasa"))

# ===================================================================== 3. Capataz da Pedreira
def capataz():
    arm, body, P, T, k = base(0.035)
    pele(body, K.m_plain("pele_queimada", "#3a2c24", 0.75, bumpscale=150, scale=k))
    parts = []
    couro_k = K.m_leather("avental_k", "#5a3a22", "#1a0e06", scale=k * 4); couro = K.m_leather("avental", "#5a3a22", "#1a0e06", scale=4)
    ferro = K.m_iron("ferro_capataz", 3)
    parts.append(H.roupa(body, arm, ("Hips", "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg"), tecido("calca_k", "#3a3026", k), 0.012, zmin=P["LeftLeg"].z - 0.15, nome="calca"))
    parts.append(H.roupa(body, arm, ("LeftLeg", "RightLeg", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase"), couro_k, 0.02, zmax=P["LeftLeg"].z - 0.08, nome="botas"))
    parts.append(H.saia(arm, P, couro, P["Hips"].z + 0.1, P["LeftLeg"].z - 0.02, 0.2, 0.23, nome="avental"))
    # ombreira de ferro na esquerda + correias
    o = K.lathe("ombreira", [(0.0, 0.12), (0.1, 0.1), (0.16, 0.04), (0.17, -0.02), (0.15, -0.03)], 20, ferro); o.location = P["LeftArm"] + Vector((0.04, 0, 0.04)); o.rotation_euler = (0, math.radians(-25), 0)
    parts.append(H.rigid(o, arm, "LeftArm"))
    parts.append(H.rigid(K.tube("correia", [P["LeftArm"] + Vector((0, -0.1, 0.0)), P["Spine1"] + Vector((-0.12, -0.15, -0.05)), P["Hips"] + Vector((-0.17, -0.1, 0.1))], 0.03, couro), arm, "Spine1"))
    # capacete de mineiro amassado
    cap = K.lathe("capacete", [(0.0, 0.14), (0.08, 0.13), (0.125, 0.08), (0.135, 0.0), (0.17, -0.005), (0.17, -0.02), (0.13, -0.01)], 24, ferro)
    cap.location = P["Head"] + Vector((0, 0.0, 0.11)); K.displace(cap, 0.006, 0.05, 'CLOUDS'); parts.append(H.rigid(cap, arm, "Head"))
    parts += H.olhos(arm, P, "#ff3010", 7.0, up=0.105)
    parts += H.rosto(arm, P, K.m_plain("pele_queimada_p", "#3a2c24", 0.75), nariz=1.4, olho_cor=None)
    arma = H.marcar_pronta(H.arma(arm, P, "arma_maca_comum", escala=1.15))
    H.finalizar("mo_capataz_pedreira", arm, body, parts + arma, dict(nome="Capataz da Pedreira", altura_m=2.0, arma="maça (mão direita)",
                notas="corpo reforçado, avental de couro, ombreira de ferro, capacete de mineiro amassado, olhos vermelhos"))

# ===================================================================== 4. Escavador
def picareta(arm, P):
    madeira = K.m_wood("cabo_pica", "#6a4a2a", "#2a1a0c", 6); ferro = K.m_iron("ferro_pica", 4)
    c = K.tube("cabo", [(0, 0, -0.12), (0, 0, 0.7)], 0.018, madeira)
    hd = K.tube("cabeca", [(-0.32, 0, 0.6), (-0.15, 0, 0.66), (0, 0, 0.68), (0.15, 0, 0.66), (0.32, 0, 0.58)], 0.026, ferro, taper=[0.3, 0.9, 1.2, 0.9, 0.3])
    d = (P["RightHand"] - P["RightForeArm"]).normalized(); cc = P["RightHand"] + d * 0.075 + Vector((0, 0, -0.012))
    M = Matrix.Translation(cc) @ Matrix.Rotation(math.radians(90), 4, 'X')
    out = []
    for o in (c, hd):
        K.apply_all(o); o.data.transform(M); out.append(H.rigid(o, arm, "RightHand"))
    return out

def escavador():
    arm, body, P, T, k = base(0.0)
    pele(body, K.m_plain("pele_suja", "#2e2a26", 0.85, bumpscale=150, scale=k))
    parts = []
    trapo_k = tecido("trapo_k", "#5a4a34", k)
    parts.append(H.roupa(body, arm, ("Hips", "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg"), trapo_k, 0.012, rasgo=0.08, nome="calca"))
    parts.append(H.roupa(body, arm, ("Spine", "Spine1", "Spine2", "LeftShoulder", "RightShoulder", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm"), tecido("camisa_k", "#6a6050", k), 0.008, rasgo=0.05, seed=4, nome="camisa"))
    parts.append(H.roupa(body, arm, ("LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "LeftLeg", "RightLeg"), K.m_leather("bota_k", "#3a2416", "#100804", scale=k * 4), 0.018, zmax=P["LeftLeg"].z - 0.2, nome="botas"))
    # oculos de protecao com lentes azuis acesas + tira
    lente = K.m_emit("lente_azul", "#5ad8ff", 3.0); latao = K.m_bronze("latao", 4)
    h = P["Head"] + Vector((0, 0, 0.11))
    for s in (-1, 1):
        aro = K.lathe(f"aro{s}", [(0.026, -0.02), (0.03, -0.015), (0.03, 0.015), (0.026, 0.02)], 16, latao, close_top=False, close_bot=False)
        aro.location = h + Vector((s * 0.037, -0.1, 0.012)); aro.rotation_euler = (math.radians(90), 0, 0); parts.append(H.rigid(aro, arm, "Head"))
        bpy.ops.mesh.primitive_cylinder_add(radius=0.025, depth=0.006, location=h + Vector((s * 0.037, -0.118, 0.012)), rotation=(math.radians(90), 0, 0))
        parts.append(H.rigid(bpy.context.object, arm, "Head", lente))
    tira = K.lathe("tira", [(0.103, -0.012), (0.106, 0.0), (0.103, 0.012)], 24, K.m_leather("tira", "#2a1a10", "#0a0604", 4), close_top=False, close_bot=False)
    tira.location = h + Vector((0, 0, 0.012)); tira.scale = (1, 1.05, 1); parts.append(H.rigid(tira, arm, "Head"))
    # saco de terra nas costas
    saco = org.blob("saco", P["Spine1"] + Vector((0.04, 0.17, -0.02)), (0.12, 0.08, 0.15)); org.displace(saco, 0.01, 0.05, 'CLOUDS')
    parts.append(H.rigid(saco, arm, "Spine1", tecido("saco", "#7a6a4a")))
    pica = picareta(arm, P)
    H.finalizar("mo_escavador", arm, body, parts + pica, dict(nome="Escavador", altura_m=1.75, arma="picareta (mão direita)",
                notas="óculos de proteção com lentes azuis acesas, roupa rasgada, saco de terra nas costas"))

# ===================================================================== 5. Sentinela (pedra)
def sentinela():
    arm, body, P, T, k = base(0.03)
    pele(body, K.m_rock("pedra_sentinela", "#a8a296", "#d8d2c4", scale=k * 3, flecks="#c8d8e0"))
    parts = []
    pedra = K.m_rock("pedra_sent_p", "#a8a296", "#d8d2c4", scale=3, flecks="#c8d8e0")
    runa = K.m_emit("runa_azul", "#5ad8ff", 4.0)
    def laje(nome, c, s, bone, rot=(0, 0, 0)):
        bpy.ops.mesh.primitive_cube_add(size=1, location=c, rotation=rot); o = bpy.context.object; o.name = nome
        o.data.transform(Matrix.Diagonal((s[0], s[1], s[2], 1))); o.scale = (1, 1, 1); K.bevel(o, 0.015, 2); K.displace(o, 0.008, 0.06, 'CLOUDS')
        parts.append(H.rigid(o, arm, bone, pedra))
    for side, s in (("Left", 1), ("Right", -1)):
        laje(f"ombro{side}", P[side + "Arm"] + Vector((s * 0.05, 0, 0.07)), (0.2, 0.22, 0.07), side + "Arm", (0, math.radians(-20 * s), 0))
        laje(f"antebraco{side}", P[side + "ForeArm"].lerp(P[side + "Hand"], 0.5) + Vector((0, 0, 0.02)), (0.2, 0.11, 0.1), side + "ForeArm")
        laje(f"canela{side}", P[side + "Leg"].lerp(P[side + "Foot"], 0.45) + Vector((0, -0.06, 0)), (0.11, 0.05, 0.3), side + "Leg")
    laje("peito", P["Spine2"] + Vector((0, -0.1, 0.0)), (0.34, 0.06, 0.24), "Spine2")
    laje("elmo", P["Head"] + Vector((0, 0.0, 0.12)), (0.2, 0.22, 0.2), "Head")
    laje("viseira", P["Head"] + Vector((0, -0.11, 0.1)), (0.19, 0.03, 0.05), "Head")
    for i in range(3):   # runas no peito
        b0 = P["Spine2"] + Vector((-0.06 + i * 0.06, -0.135, -0.04)); parts.append(H.rigid(K.tube(f"runa{i}", [b0, b0 + Vector((0.01, 0, 0.1))], 0.008, runa), arm, "Spine2"))
    parts += H.olhos(arm, P, "#5ad8ff", 8.0, fwd=-0.13, up=0.105, r=0.012)
    arma = H.marcar_pronta(H.arma(arm, P, "arma_maca_refinada", escala=1.2))
    H.finalizar("mo_sentinela", arm, body, parts + arma, dict(nome="Sentinela", altura_m=2.4, arma="maça (mão direita)",
                notas="estátua de calcário viva: lajes nos ombros/braços/canelas, elmo quadrado, runas azuis acesas"))

# ===================================================================== 6. Guardiao da Galeria
def guardiao():
    arm, body, P, T, k = base(0.045)
    pele(body, K.m_rock("pedra_escura", "#4a4844", "#7a766e", scale=k * 3, flecks="#5ad8ff"))
    parts = []
    bronze = K.m_metal("bronze_verde", "#3a6a60", 0.45, wear="#c89a5a", dirt="#0a1410", scratch=0.6, scale=3)
    runa = K.m_emit("runa_azul_g", "#5ad8ff", 5.0)
    def placa(nome, c, raio, bone, sq):
        o = org.blob(nome, c, (raio * sq[0], raio * sq[1], raio * sq[2]), seg=16, rings=8); K.mod(o, 'DECIMATE', ratio=0.5); parts.append(H.rigid(o, arm, bone, bronze))
    for side, s in (("Left", 1), ("Right", -1)):
        o = K.lathe(f"ombreira{side}", [(0.0, 0.14), (0.12, 0.12), (0.2, 0.05), (0.21, -0.03), (0.17, -0.05)], 20, bronze); o.location = P[side + "Arm"] + Vector((s * 0.06, 0, 0.06)); o.rotation_euler = (0, math.radians(-30 * s), 0)
        parts.append(H.rigid(o, arm, side + "Arm"))
        for j in range(3):
            b0 = P[side + "Arm"] + Vector((s * (0.04 + 0.05 * j), 0.0, 0.17)); parts.append(H.rigid(org.spike(f"chifre_ombro{side}{j}", b0, b0 + Vector((s * 0.04, 0.03, 0.14 - 0.03 * j)), 0.03), arm, side + "Arm", bronze))
        placa(f"manopla{side}", P[side + "Hand"] + Vector((s * 0.02, 0, 0.0)), 0.07, side + "Hand", (1.2, 1, 0.9))
        placa(f"grev{side}", P[side + "Leg"].lerp(P[side + "Foot"], 0.4) + Vector((0, -0.05, 0)), 0.08, side + "Leg", (1, 0.7, 2.2))
    placa("peitoral", P["Spine2"] + Vector((0, -0.07, 0.0)), 0.2, "Spine2", (1.2, 0.6, 0.9))
    elmo = K.lathe("elmo", [(0.0, 0.17), (0.1, 0.16), (0.14, 0.08), (0.145, -0.04), (0.12, -0.12)], 24, bronze); elmo.location = P["Head"] + Vector((0, 0, 0.11)); parts.append(H.rigid(elmo, arm, "Head"))
    crista = org.spike("crista", P["Head"] + Vector((0, 0.05, 0.26)), P["Head"] + Vector((0, 0.22, 0.38)), 0.04); parts.append(H.rigid(crista, arm, "Head", bronze))
    for i in range(4):
        b0 = P["Spine2"] + Vector((-0.09 + i * 0.06, -0.19, -0.06)); parts.append(H.rigid(K.tube(f"runa{i}", [b0, b0 + Vector((0, 0, 0.14))], 0.01, runa), arm, "Spine2"))
    parts += H.olhos(arm, P, "#5ad8ff", 10.0, fwd=-0.14, up=0.1, r=0.013)
    parts.append(H.saia(arm, P, bronze, P["Hips"].z + 0.06, P["LeftLeg"].z + 0.1, 0.22, 0.26, nome="saiote", seg=16))
    arma = H.marcar_pronta(H.arma(arm, P, "arma_lanca_refinada", escala=1.3))
    H.finalizar("mo_guardiao_galeria", arm, body, parts + arma, dict(nome="Guardião da Galeria", altura_m=3.6, arma="lança (mão direita)",
                notas="gigante de pedra escura com armadura de bronze esverdeado, chifres nas ombreiras, runas azuis no peito"))

# ===================================================================== 7. Gnomo do Areal
def gnomo():
    arm, body, P, T, k = base(0.02)
    pele(body, K.m_plain("pele_gnomo", "#c89a7a", 0.6, scale=k))
    parts = []
    azul_k = tecido("tunica_azul_k", "#3a5a7a", k); azul = tecido("tunica_azul", "#3a5a7a")
    vermelho = tecido("gorro_vermelho", "#9a2018")
    parts.append(H.roupa(body, arm, ("Spine", "Spine1", "Spine2", "Hips", "LeftShoulder", "RightShoulder", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm"), azul_k, 0.014, nome="tunica"))
    parts.append(H.saia(arm, P, azul, P["Hips"].z + 0.04, P["LeftLeg"].z + 0.14, 0.18, 0.21, nome="barra"))
    parts.append(H.roupa(body, arm, ("LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "LeftLeg", "RightLeg"), K.m_leather("bota_g_k", "#4a2e1a", "#140a04", scale=k * 4), 0.02, zmax=P["LeftLeg"].z - 0.2, nome="botas"))
    # gorro pontudo comprido caido para tras
    h = P["Head"] + Vector((0, 0.0, 0.14))
    pts = [h + Vector((0, 0, 0)), h + Vector((0, 0.03, 0.12)), h + Vector((0, 0.1, 0.24)), h + Vector((0, 0.22, 0.3)), h + Vector((0, 0.32, 0.26))]
    g = K.tube("gorro", pts, 0.115, vermelho, taper=[1.0, 0.75, 0.45, 0.22, 0.05]); parts.append(H.rigid(g, arm, "Head"))
    parts += H.rosto(arm, P, K.m_plain("pele_gnomo_p", "#c89a7a", 0.6), nariz=1.8, orelhas=0.5, barba=K.m_plain("barba_branca", "#e8e4dc", 0.9, bumpscale=80), sobrancelha=K.m_plain("sob_branca", "#e8e4dc", 0.9))
    # bolsa de pedrinhas
    parts.append(H.rigid(org.blob("bolsa", P["Hips"] + Vector((0.17, -0.02, 0.0)), (0.06, 0.05, 0.08)), arm, "Hips", K.m_leather("bolsa", "#6a4428", "#20120a", 4)))
    H.finalizar("mo_gnomo_areal", arm, body, parts, dict(nome="Gnomo do Areal", altura_m=0.95, arma="nenhuma (atira pedrinhas da bolsa)",
                notas="gorro vermelho comprido, barba branca, narigão, túnica azul, bolsa de pedrinhas na cintura"))

# ===================================================================== 8. Saci
def saci():
    arm, body, P, T, k = base(-0.004)
    # uma perna so: tira a perna esquerda inteira
    B.keep_faces(body, lambda f, doms: not any(d in ("mixamorig:LeftUpLeg", "mixamorig:LeftLeg", "mixamorig:LeftFoot", "mixamorig:LeftToeBase") for d in doms))
    pele(body, K.m_plain("pele_saci", "#3a2418", 0.55, scale=k))
    parts = []
    vermelho = tecido("gorro_saci", "#b01810"); vermelho_k = tecido("calcao_k", "#b01810", k)
    parts.append(H.roupa(body, arm, ("Hips", "RightUpLeg"), vermelho_k, 0.012, zmin=P["RightLeg"].z + 0.12, nome="calcao"))
    h = P["Head"] + Vector((0, 0.0, 0.15))
    pts = [h, h + Vector((0, 0.02, 0.1)), h + Vector((0.02, 0.07, 0.2)), h + Vector((0.06, 0.15, 0.25))]
    parts.append(H.rigid(K.tube("gorro", pts, 0.11, vermelho, taper=[1.0, 0.7, 0.35, 0.05]), arm, "Head"))
    parts += H.rosto(arm, P, K.m_plain("pele_saci_p", "#3a2418", 0.55), nariz=1.0, orelhas=0.35, olho_cor=None)
    parts += H.olhos(arm, P, "#ffc040", 6.0, up=0.112, r=0.012)
    # cachimbo aceso no canto da boca
    madeira = K.m_wood("cachimbo", "#5a3a1a", "#1a0c04", 8)
    b0 = P["Head"] + Vector((0.03, -0.11, 0.06)); parts.append(H.rigid(K.tube("piteira", [b0, b0 + Vector((0.05, -0.08, -0.02))], 0.006, madeira), arm, "Head"))
    fo = K.lathe("fornilho", [(0.0, 0.0), (0.02, 0.0), (0.022, 0.04), (0.016, 0.042), (0.0, 0.02)], 12, madeira); fo.location = b0 + Vector((0.05, -0.085, -0.02)); parts.append(H.rigid(fo, arm, "Head"))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.013, location=b0 + Vector((0.05, -0.085, 0.022)), segments=10, ring_count=6)
    parts.append(H.rigid(bpy.context.object, arm, "Head", K.m_emit("brasa_cachimbo", "#ff6a10", 8.0)))
    H.finalizar("mo_saci", arm, body, parts, dict(nome="Saci", altura_m=1.3, arma="nenhuma",
                notas="uma perna só (a esquerda some — nos clipes ele 'pula'), gorro vermelho, cachimbo aceso, olhos amarelos. No jogo: redemoinho de poeira/fumaça em volta (partículas)"))

# ===================================================================== 9. Anao Minerador
def anao():
    arm, body, P, T, k = base(0.03)
    pele(body, K.m_plain("pele_anao", "#b88a6a", 0.6, scale=k))
    parts = []
    couro_k = K.m_leather("colete_k", "#5a3a20", "#1a0e06", scale=k * 4); couro = K.m_leather("colete", "#5a3a20", "#1a0e06", scale=4)
    calca_k = tecido("calca_anao_k", "#4a4a52", k)
    parts.append(H.roupa(body, arm, ("Spine", "Spine1", "Spine2", "LeftShoulder", "RightShoulder", "LeftArm", "RightArm"), tecido("camisa_anao_k", "#7a6a4a", k), 0.008, nome="camisa"))
    parts.append(H.roupa(body, arm, ("Spine", "Spine1", "Spine2"), couro_k, 0.02, nome="colete"))
    parts.append(H.roupa(body, arm, ("Hips", "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg"), calca_k, 0.014, nome="calca"))
    parts.append(H.roupa(body, arm, ("LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "LeftLeg", "RightLeg"), couro_k, 0.022, zmax=P["LeftLeg"].z - 0.15, nome="botas"))
    parts.append(H.roupa(body, arm, ("LeftForeArm", "RightForeArm", "LeftHand", "RightHand"), couro_k, 0.016, nome="luvas"))
    cinto = K.tube("cinto", [P["Hips"] + Vector((math.cos(a) * 0.17, math.sin(a) * 0.13, 0.08)) for a in [i / 20 * math.tau for i in range(21)]], 0.03, couro)
    parts.append(H.rigid(cinto, arm, "Hips"))
    fivela = org.blob("fivela", P["Hips"] + Vector((0, -0.16, 0.08)), (0.05, 0.015, 0.04)); parts.append(H.rigid(fivela, arm, "Hips", K.m_gold("fivela", 4)))
    barba = K.m_plain("barba_ruiva", "#9a4a1a", 0.9, bumpscale=80)
    parts += H.rosto(arm, P, K.m_plain("pele_anao_p", "#b88a6a", 0.6), nariz=1.6, barba=barba, sobrancelha=barba)
    # trancas da barba
    h = P["Head"] + Vector((0, 0, 0.1))
    for s in (-1, 1):
        b0 = h + Vector((s * 0.03, -0.1, -0.24)); parts.append(H.rigid(K.tube(f"tranca{s}", [b0, b0 + Vector((s * 0.01, -0.01, -0.12))], 0.016, barba, taper=[1, 0.6]), arm, "Head"))
    # capacete de mineiro com vela
    ferro = K.m_iron("capacete_anao", 4)
    cap = K.lathe("capacete", [(0.0, 0.15), (0.09, 0.14), (0.13, 0.08), (0.14, 0.0), (0.16, -0.01), (0.16, -0.025), (0.13, -0.015)], 24, ferro); cap.location = P["Head"] + Vector((0, 0, 0.12))
    parts.append(H.rigid(cap, arm, "Head"))
    vela = K.lathe("vela", [(0.0, 0.0), (0.018, 0.0), (0.018, 0.06), (0.0, 0.06)], 10, K.m_plain("cera", "#e8dcc0", 0.6)); vela.location = P["Head"] + Vector((0, -0.08, 0.25))
    parts.append(H.rigid(vela, arm, "Head"))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.012, location=P["Head"] + Vector((0, -0.08, 0.325)), segments=10, ring_count=6); ch = bpy.context.object; ch.scale = (1, 1, 1.8)
    parts.append(H.rigid(ch, arm, "Head", K.m_emit("chama_vela", "#ffb040", 10.0)))
    arma = H.marcar_pronta(H.arma(arm, P, "arma_machado_guerra_comum", escala=0.95))
    H.finalizar("mo_anao_minerador", arm, body, parts + arma, dict(nome="Anão Minerador", altura_m=1.35, arma="machado de guerra (mão direita)",
                notas="corpo largo, barba ruiva com tranças, capacete com vela acesa, colete e luvas de couro, fivela dourada"))

FEITOS = dict(mo_saqueador_vigilia=saqueador, mo_acolito_cinzas=acolito, mo_capataz_pedreira=capataz, mo_escavador=escavador, mo_sentinela=sentinela,
              mo_guardiao_galeria=guardiao, mo_gnomo_areal=gnomo, mo_saci=saci, mo_anao_minerador=anao)
for vid, fn in FEITOS.items():
    if SEL and vid not in SEL: continue
    try: fn()
    except Exception as e:
        import traceback; traceback.print_exc(); print("FALHOU", vid, e, flush=True)

"""Peças de destaque dos 14 NPCs (Cal lote 2), presas aos ossos do manequim do jogo. Uso: python3 build_npcs.py <id|all>
Cada npc_<id>.glb tem nós vazios com o NOME DO OSSO (Head, Spine2, Hips, LeftHand, RightHand, LeftForeArm...) e, dentro de
cada um, a peça já no espaço do encaixe (metros, +Y cima, +Z frente, origem na cabeça do osso, pose T) — o mesmo do
MannequinVisual.Socket(). O cliente só precisa: para cada filho do .glb, Socket(nome).AddChild(filho).
Itens de mão ficam ALINHADOS AO BRAÇO na pose T: com o braço baixado (Idle) eles pendem/ficam em pé naturalmente."""
import sys, math, json, random, os
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy
from cz import Vector, Matrix
import build_props as bp
from build_props import Z, WOOD, DWOOD, IRON, ROPE, HIDE, BONE, CAL, PAINT, COPPER, STONE, GLASS, EMBER, GRASS

OUT = "/home/claude/cinzas/calL2/npcs/"
P = {k: Vector(v) for k, v in json.load(open("/home/claude/cinzas/goblins/bones_masc_all.json")).items()}
AZUL, VERDE, VERM, ROXO, PALHA, OURO, PRETO, BARBA, GATO, AMAR, LILAS = range(60, 71)
bp.PAL.update({AZUL: ("#2a4a8a", "#14244a"), VERDE: ("#4a7a3a", "#24401c"), VERM: ("#a82a24", "#5a1410"), ROXO: ("#5a2a7a", "#2a1240"),
               PALHA: ("#d8b870", "#9a7a3a"), OURO: ("#e8b840", "#8a6a20"), PRETO: ("#2a2628", "#121012"), BARBA: ("#eeeae2", "#b8b2a6"),
               GATO: ("#2a2a2e", "#121214"), AMAR: ("#e8c030", "#a07a10"), LILAS: ("#8a6ac0", "#4a3470")})
bp.ROUGH.update({AZUL: 0.9, VERDE: 0.9, VERM: 0.88, ROXO: 0.9, PALHA: 0.9, OURO: 0.3, PRETO: 0.85, BARBA: 0.95, GATO: 0.9, AMAR: 0.8, LILAS: 0.88})
bp.METAL.update({OURO: 1.0})
for z in (AZUL, VERDE, VERM, ROXO, PRETO, LILAS): bp.DET[z] = dict(grain=0.0015, grain_scale=90)
bp.DET[PALHA] = dict(grain=0.003, grain_scale=60); bp.DET[BARBA] = dict(grain=0.004, grain_scale=50); bp.DET[GATO] = dict(grain=0.002, grain_scale=120)
bp.OUT = OUT

H = P["Head"]; HC = Vector((0.0, 0.045, 1.62))    # centro da cabeca (malha), topo em 1,70
S2 = P["Spine2"]; HIP = P["Hips"]; LH, RH = P["LeftHand"], P["RightHand"]

def cyls(name, r, h, n, scale=(1, 1, 1), **kw):
    o = cz.cyl(name, r, h, n, **kw); o.scale = scale; cz.apply_tf(o); return o

def ring(name, c, r, tube, z, scale=(1, 1, 1)):
    o = cz.cyl(name, r, tube, 20, loc=c, cap=False); o.scale = scale; cz.apply_tf(o); return Z(o, z)

def brim_hat(prefix, c, brim_r, crown_r, crown_h, zb, zc, tilt=(0, 0, 0), band=None):
    parts = [Z(cz.cyl(prefix + "aba", brim_r, 0.02, 24, loc=c, rot=tilt), zb),
             Z(cz.cyl(prefix + "copa", crown_r, crown_h, 20, r2=crown_r * 0.85, loc=c + Vector((0, 0, crown_h / 2)), rot=tilt), zc)]
    if band: parts.append(Z(cz.cyl(prefix + "faixa", crown_r * 1.02, 0.03, 20, loc=c + Vector((0, 0, 0.025)), rot=tilt, cap=False), band))
    return parts

# ------------------------------------------------------------------ definicoes por NPC (coordenadas do mundo, pose T)
def ferreiro():
    up = [Z(cz.box("peitilho", (0.26, 0.025, 0.3), loc=(0, -0.085, 1.22)), HIDE),
          Z(cz.seg("alca_l", (0.11, -0.08, 1.36), (0.08, 0.1, 1.42), 0.012, 0.012, 5), HIDE), Z(cz.seg("alca_r", (-0.11, -0.08, 1.36), (-0.08, 0.1, 1.42), 0.012, 0.012, 5), HIDE)]
    for x in (-0.09, 0.09): up.append(Z(cz.ico("rebite", 0.012, 1, loc=(x, -0.1, 1.34)), IRON))
    low = [Z(cz.box("avental", (0.34, 0.025, 0.55), loc=(0, -0.07, 0.8)), HIDE), Z(cz.box("bolso", (0.16, 0.02, 0.1), loc=(0.05, -0.09, 0.86)), DWOOD),
           Z(cz.seg("pinca", (0.08, -0.095, 0.93), (0.1, -0.1, 0.78), 0.008, 0.006, 4), IRON),
           Z(cz.box("cinto", (0.3, 0.13, 0.05), loc=(0, 0.0, 1.0)), DWOOD)]
    g = RH + Vector((-0.07, 0, 0))     # martelo: cabo ao longo do braco, cabeca na ponta
    ham = [Z(cz.cyl("cabo", 0.018, 0.34, 8, loc=g + Vector((-0.13, 0, 0)), rot=(0, math.pi / 2, 0)), WOOD),
           Z(cz.box("cabeca", (0.07, 0.07, 0.17), loc=g + Vector((-0.3, 0, 0))), IRON), Z(cz.box("cunha", (0.04, 0.05, 0.05), loc=g + Vector((-0.3, 0, 0.1))), IRON)]
    return {"Spine2": up, "Hips": low, "RightHand": ham}

def curandeira():
    xale = []
    for i in range(14):
        a = i / 14 * math.tau
        p = Vector((math.cos(a) * 0.19, 0.045 + math.sin(a) * 0.14, 1.38 - 0.06 * max(0, -math.sin(a))))
        xale.append(Z(cz.ball(f"xale{i}", 0.07, 10, 6, loc=p, scale=(1.2, 1.0, 0.55)), VERDE))
    xale.append(Z(cz.cyl("ponta", 0.09, 0.3, 6, r2=0.0, loc=(0, 0.14, 1.2), rot=(math.pi, 0, 0)), VERDE))
    for i in range(6): xale.append(Z(cz.seg(f"franja{i}", (-0.15 + i * 0.06, -0.06, 1.33), (-0.15 + i * 0.06, -0.08, 1.25), 0.006, 0.004, 4), AMAR))
    bolsa = [Z(cz.ball("bolsa", 0.09, 12, 8, loc=(0.15, -0.02, 0.88), scale=(0.8, 0.6, 1.0)), HIDE), Z(cz.seg("cordao", (0.12, -0.02, 0.97), (0.06, -0.02, 1.02), 0.008, 0.008, 4), ROPE)]
    for i in range(5):
        a = i * 1.3; b = Vector((0.15 + math.cos(a) * 0.03, -0.02 + math.sin(a) * 0.02, 0.96))
        bolsa.append(Z(cz.seg(f"erva{i}", b, b + Vector((math.cos(a) * 0.05, math.sin(a) * 0.03, 0.12)), 0.012, 0.0, 4), VERDE))
    return {"Spine2": xale, "Hips": bolsa}

def capita():
    c = HC + Vector((0, 0, 0.065))
    hat = [Z(cz.cyl("copa", 0.11, 0.1, 16, r2=0.1, loc=c + Vector((0, 0, 0.04))), PRETO)]
    for k in range(3):       # aba levantada em 3 pontas (tricorne)
        a = k / 3 * math.tau - math.pi / 2
        p = c + Vector((math.cos(a) * 0.12, math.sin(a) * 0.12, 0.03))
        hat.append(Z(cz.box(f"aba{k}", (0.24, 0.03, 0.09), loc=p, rot=(0.35, 0, a + math.pi / 2)), PRETO))
        hat.append(Z(cz.box(f"debrum{k}", (0.24, 0.035, 0.012), loc=p + Vector((0, 0, 0.045)), rot=(0.35, 0, a + math.pi / 2)), OURO))
    hat.append(Z(cz.ball("pena", 0.03, 8, 6, loc=c + Vector((0.08, 0.02, 0.11)), scale=(0.6, 0.6, 3)), VERM))
    ep = []
    for s in (-1, 1):
        sh = Vector((s * 0.17, 0.045, 1.42))
        ep.append(Z(cyls(f"dragona{s}", 0.065, 0.03, 16, loc=sh, scale=(1, 1.2, 1)), OURO))
        for j in range(7): ep.append(Z(cz.seg(f"franja{s}{j}", sh + Vector((s * 0.06, -0.05 + j * 0.016, 0)), sh + Vector((s * 0.075, -0.05 + j * 0.016, -0.06)), 0.005, 0.004, 4), OURO))
    ep.append(Z(cyls("gola", 0.1, 0.06, 16, loc=(0, 0.045, 1.43), cap=False, scale=(1.1, 1, 1)), AZUL))
    return {"Head": hat, "Spine2": ep}

def pescador():
    c = HC + Vector((0, 0, 0.06))
    hat = [Z(cz.cyl("aba", 0.24, 0.02, 24, loc=c, rot=(0.05, 0, 0)), PALHA), Z(cz.ball("copa", 0.1, 14, 8, loc=c + Vector((0, 0, 0.04)), scale=(1, 1, 0.8)), PALHA),
           Z(cz.cyl("fita", 0.1, 0.025, 20, loc=c + Vector((0, 0, 0.025)), cap=False), VERM)]
    rede = []
    rnd = random.Random(3)
    for i in range(10):     # rede embolada no ombro esquerdo caindo nas costas
        p = Vector((0.12 + rnd.uniform(-0.05, 0.08), 0.1 + rnd.uniform(0, 0.08), 1.4 - i * 0.05))
        rede.append(Z(cz.ball(f"rede{i}", 0.08, 8, 6, loc=p, scale=(1.2, 0.7, 0.9)), ROPE))
    for i in range(4): rede.append(Z(cz.ball(f"boia{i}", 0.035, 8, 6, loc=(0.1 + i * 0.04, 0.17, 1.25 - i * 0.07)), CAL if i % 2 else VERM))
    return {"Head": hat, "Spine2": rede}

def mentor():
    barba = [Z(org.limb("barba", [(HC + Vector((0, -0.08, -0.05)), (0.07, 0.04)), (HC + Vector((0, -0.1, -0.14)), (0.08, 0.04)), (HC + Vector((0, -0.09, -0.28)), (0.05, 0.03)), (HC + Vector((0, -0.07, -0.36)), 0.01)], subdiv=2), BARBA),
             Z(cz.ball("bigode_l", 0.035, 8, 6, loc=HC + Vector((0.04, -0.09, -0.02)), scale=(1.6, 0.6, 0.6)), BARBA),
             Z(cz.ball("bigode_r", 0.035, 8, 6, loc=HC + Vector((-0.04, -0.09, -0.02)), scale=(1.6, 0.6, 0.6)), BARBA),
             Z(cz.ball("cabelo", 0.1, 12, 8, loc=HC + Vector((0, 0.03, 0.0)), scale=(1.0, 1.0, 0.95)), BARBA)]
    manto = [Z(cz.box("capa", (0.44, 0.03, 0.95), loc=(0, 0.17, 1.0), rot=(0.06, 0, 0)), ROXO), Z(cz.cyl("gola", 0.13, 0.08, 16, loc=(0, 0.05, 1.43), cap=False), ROXO)]
    for k, zc in enumerate((PAINT, AZUL, VERDE, LILAS)):     # 4 emblemas das classes
        manto.append(Z(cz.cyl(f"emblema{k}", 0.045, 0.012, 12, loc=(-0.12 + k * 0.08, 0.195, 1.12), rot=(math.pi / 2, 0, 0)), zc))
        manto.append(Z(cz.cyl(f"aro{k}", 0.05, 0.012, 12, loc=(-0.12 + k * 0.08, 0.193, 1.12), rot=(math.pi / 2, 0, 0), cap=False), OURO))
    return {"Head": barba, "Spine2": manto}

def arcanista():
    b = LH + Vector((0.12, -0.24, 0.08))     # livro aberto flutuando a frente da mao
    livro = [Z(cz.box("capa_l", (0.13, 0.17, 0.015), loc=b + Vector((-0.065, 0, 0)), rot=(0, 0.2, 0)), AZUL), Z(cz.box("capa_r", (0.13, 0.17, 0.015), loc=b + Vector((0.065, 0, 0)), rot=(0, -0.2, 0)), AZUL),
             Z(cz.box("folhas_l", (0.12, 0.16, 0.02), loc=b + Vector((-0.06, 0, 0.012)), rot=(0, 0.2, 0)), CAL), Z(cz.box("folhas_r", (0.12, 0.16, 0.02), loc=b + Vector((0.06, 0, 0.012)), rot=(0, -0.2, 0)), CAL)]
    luvas = {}
    for side, hp_ in (("LeftHand", LH), ("RightHand", RH)):
        s = 1 if side == "LeftHand" else -1
        luvas[side] = [Z(cyls("luva", 0.045, 0.16, 12, loc=hp_ + Vector((s * 0.04, 0, 0)), rot=(0, math.pi / 2, 0), scale=(1, 1.3, 1)), AZUL),
                       Z(cyls("punho", 0.05, 0.04, 12, loc=hp_ + Vector((-s * 0.04, 0, 0)), rot=(0, math.pi / 2, 0), scale=(1, 1.3, 1)), OURO)]
    luvas["LeftHand"] += livro
    return luvas, [("LeftHand", cz.ico("runa", 0.03, 1, loc=b + Vector((0, 0, 0.08))), "#7ab8ff", 6.0)]

def bruxa():
    c = HC + Vector((0, 0, 0.07))
    hat = [Z(cz.cyl("aba", 0.22, 0.02, 24, loc=c, rot=(0.08, 0.1, 0)), ROXO)]
    pts = [(c + Vector((0, 0, 0.0)), 0.1), (c + Vector((0.01, 0.0, 0.12)), 0.075), (c + Vector((0.05, 0.03, 0.24)), 0.045), (c + Vector((0.14, 0.06, 0.3)), 0.025), (c + Vector((0.2, 0.05, 0.26)), 0.008)]
    hat.append(Z(org.limb("cone", pts, subdiv=2), ROXO)); hat.append(Z(cz.cyl("faixa", 0.1, 0.03, 20, loc=c + Vector((0, 0, 0.02)), cap=False), VERDE))
    hat.append(Z(cz.box("fivela", (0.05, 0.01, 0.035), loc=c + Vector((0, -0.1, 0.02))), OURO))
    # gato preto sentado no ombro esquerdo
    sh = Vector((0.16, 0.06, 1.44))
    gato = [Z(org.fuse("gato", [org.blob("corpo", sh + Vector((0, 0.02, 0.07)), (0.06, 0.08, 0.075), 16, 8), org.blob("cabeca", sh + Vector((0, -0.04, 0.16)), (0.05, 0.045, 0.045), 16, 8),
                                org.spike("orelha_l", sh + Vector((0.025, -0.04, 0.19)), sh + Vector((0.035, -0.04, 0.24)), 0.018), org.spike("orelha_r", sh + Vector((-0.025, -0.04, 0.19)), sh + Vector((-0.035, -0.04, 0.24)), 0.018),
                                org.limb("rabo", [(sh + Vector((0, 0.09, 0.04)), 0.015), (sh + Vector((0.04, 0.14, -0.06)), 0.013), (sh + Vector((0.02, 0.1, -0.16)), 0.01)])], voxel=0.006), GATO)]
    return {"Head": hat, "Spine2": gato}, [("Spine2", cz.ball("olhos", 0.008, 6, 4, loc=sh + Vector((0.018, -0.085, 0.165))), "#ffd23a", 4.0),
                                           ("Spine2", cz.ball("olhos2", 0.008, 6, 4, loc=sh + Vector((-0.018, -0.085, 0.165))), "#ffd23a", 4.0)]

def batedor():
    capa = [Z(cz.box("capa", (0.46, 0.03, 0.9), loc=(0, 0.17, 1.02), rot=(0.08, 0, 0)), VERDE),
            Z(org.fuse("capuz", [org.blob("capuz", (0, 0.16, 1.47), (0.13, 0.08, 0.08), 16, 8)], voxel=0.01), VERDE),
            Z(cz.ico("broche", 0.02, 1, loc=(0.1, -0.06, 1.38)), OURO)]
    lun = [Z(cz.cyl("luneta", 0.025, 0.24, 12, loc=(-0.16, 0.0, 0.92), rot=(0.2, 0, 0)), COPPER), Z(cz.cyl("luneta2", 0.032, 0.06, 12, loc=(-0.16, 0.02, 1.03), rot=(0.2, 0, 0)), OURO)]
    return {"Spine2": capa, "Hips": lun}

def agricultora():
    g = RH + Vector((-0.06, 0, 0))
    enx = [Z(cz.cyl("cabo", 0.02, 1.25, 8, loc=g + Vector((-0.25, 0, 0)), rot=(0, math.pi / 2, 0)), WOOD), Z(cz.box("lamina", (0.04, 0.2, 0.16), loc=g + Vector((-0.86, 0, -0.07))), IRON)]
    c = LH + Vector((0.18, 0, 0))
    cesto = [Z(cz.cyl("cesto", 0.13, 0.15, 14, r2=0.1, loc=c + Vector((0.06, 0, 0)), rot=(0, math.pi / 2, 0)), PALHA),
             Z(cz.cyl("alca", 0.11, 0.025, 14, loc=c + Vector((-0.02, 0, 0)), rot=(math.pi / 2, 0, 0), cap=False), PALHA)]
    for i in range(5): cesto.append(Z(cz.ball(f"fruta{i}", 0.04, 8, 6, loc=c + Vector((0.0 + i * 0.012, -0.05 + i * 0.025, 0.02 + (i % 2) * 0.03))), VERM if i % 2 else AMAR))
    lenco = [Z(cz.ball("lenco", 0.1, 12, 8, loc=HC + Vector((0, 0.02, 0.03)), scale=(1.02, 1.05, 0.8)), VERM), Z(cz.ball("no", 0.03, 8, 6, loc=HC + Vector((0, 0.12, -0.04))), VERM)]
    return {"RightHand": enx, "LeftHand": cesto, "Head": lenco}

def bobo():
    c = HC + Vector((0, 0, 0.06))
    hat = [Z(cz.cyl("base", 0.1, 0.05, 16, loc=c), VERM)]
    for k, (a, col) in enumerate(((-0.9, VERM), (0.0, AMAR), (0.9, AZUL))):
        tip = c + Vector((math.sin(a) * 0.2, 0.05, 0.12 + 0.05 * math.cos(a)))
        hat.append(Z(org.limb(f"ponta{k}", [(c + Vector((0, 0, 0.03)), 0.06), (c.lerp(tip, 0.6) + Vector((0, 0, 0.08)), 0.035), (tip, 0.01)], subdiv=1), col))
        hat.append(Z(cz.ball(f"guizo{k}", 0.022, 8, 6, loc=tip + Vector((0, 0, -0.02))), OURO))
    gola = []
    for i in range(12):
        a = i / 12 * math.tau
        gola.append(Z(cz.cyl(f"retalho{i}", 0.055, 0.03, 3, r2=0.0, loc=(math.cos(a) * 0.12, 0.045 + math.sin(a) * 0.1, 1.42), rot=(0, math.pi / 2, a)), (VERM, AMAR, AZUL, VERDE)[i % 4]))
    return {"Head": hat, "Spine2": gola}

def batedora():
    g0 = LH + Vector((0.05, 0, 0))
    arco = [Z(org.limb("arco", [(g0 + Vector((-0.4, 0.07, 0)), 0.008), (g0 + Vector((-0.2, -0.03, 0)), 0.016), (g0, 0.02), (g0 + Vector((0.2, -0.03, 0)), 0.016), (g0 + Vector((0.4, 0.07, 0)), 0.008)], subdiv=1), WOOD),
            Z(cz.seg("corda", g0 + Vector((-0.4, 0.07, 0)), g0 + Vector((0.4, 0.07, 0)), 0.003, 0.003, 4), ROPE)]
    q0 = Vector((-0.07, 0.15, 1.3))
    alj = [Z(cz.cyl("aljava", 0.05, 0.4, 10, loc=q0, rot=(0, -0.35, 0)), HIDE)]
    for j in range(5):
        tip = q0 + Vector((-0.08 - j * 0.01, (j - 2) * 0.012, 0.26))
        alj.append(Z(cz.seg(f"flecha{j}", tip - Vector((0.05, 0, 0.18)), tip, 0.004, 0.004, 4), WOOD)); alj.append(Z(cz.box(f"pena{j}", (0.004, 0.025, 0.05), loc=tip), VERDE))
    faixa = [Z(cz.cyl("faixa", 0.098, 0.03, 20, loc=HC + Vector((0, 0, 0.03)), cap=False), VERM), Z(cz.box("ponta", (0.02, 0.01, 0.12), loc=HC + Vector((0.02, 0.11, -0.03)), rot=(0.2, 0.2, 0)), VERM)]
    return {"LeftHand": arco, "Spine2": alj, "Head": faixa}

def mineiro():
    c = HC + Vector((0, 0, 0.04))
    cap = [Z(cz.ball("capacete", 0.12, 14, 8, loc=c, scale=(1, 1.05, 0.75)), DWOOD), Z(cz.cyl("aba", 0.135, 0.015, 20, loc=c + Vector((0, 0, -0.02))), DWOOD),
           Z(cz.cyl("lanterna", 0.03, 0.05, 10, loc=c + Vector((0, -0.12, 0.04)), rot=(math.pi / 2, 0, 0)), COPPER)]
    g = RH + Vector((-0.06, 0, 0))
    pic = [Z(cz.cyl("cabo", 0.02, 0.8, 8, loc=g + Vector((-0.2, 0, 0)), rot=(0, math.pi / 2, 0)), WOOD)]
    for s in (-1, 1): pic.append(Z(cz.seg(f"bico{s}", g + Vector((-0.58, 0, 0)), g + Vector((-0.6, 0, s * 0.28)), 0.03, 0.006, 6), IRON))
    return {"Head": cap, "RightHand": pic}, [("Head", cz.cyl("luz", 0.024, 0.01, 10, loc=c + Vector((0, -0.145, 0.04)), rot=(math.pi / 2, 0, 0)), "#ffe2a0", 8.0)]

def mascate():
    b = Vector((0, 0.32, 1.25))
    moch = [Z(cz.box("mochila", (0.42, 0.34, 0.6), loc=b), HIDE), Z(cz.box("tampa", (0.44, 0.36, 0.08), loc=b + Vector((0, 0, 0.32))), DWOOD),
            Z(cz.seg("alca_l", (0.12, -0.06, 1.4), (0.12, 0.15, 1.45), 0.015, 0.015, 5), HIDE), Z(cz.seg("alca_r", (-0.12, -0.06, 1.4), (-0.12, 0.15, 1.45), 0.015, 0.015, 5), HIDE),
            Z(cz.cyl("rolo", 0.08, 0.5, 12, loc=b + Vector((0, 0, 0.42)), rot=(0, math.pi / 2, 0)), VERM)]
    for k, (dx, dz) in enumerate(((0.24, 0.1), (-0.24, 0.05), (0.24, -0.15))):
        moch.append(Z(cz.cyl(f"panela{k}", 0.08, 0.08, 14, loc=b + Vector((dx, 0.0, dz)), rot=(0, math.pi / 2, 0)), IRON))
    moch.append(Z(cz.cyl("frigideira", 0.1, 0.02, 14, loc=b + Vector((0, 0.18, 0.05)), rot=(math.pi / 2, 0, 0)), IRON))
    moch.append(Z(cz.seg("pa", b + Vector((-0.15, 0.17, -0.2)), b + Vector((-0.2, 0.18, 0.55)), 0.015, 0.015, 6), WOOD))
    moch.append(Z(cz.cyl("lanterna", 0.045, 0.1, 8, loc=b + Vector((0.27, 0.1, -0.3))), COPPER))
    for k in range(4): moch.append(Z(cz.ball(f"saquinho{k}", 0.05, 8, 6, loc=b + Vector((-0.24, -0.05 + k * 0.06, -0.2 + k * 0.08))), (PALHA, VERDE, VERM, CAL)[k]))
    return {"Spine2": moch}, [("Spine2", cz.cyl("chama", 0.025, 0.05, 8, loc=b + Vector((0.27, 0.1, -0.3))), "#ffb860", 4.0)]

def guarda():
    c = HC + Vector((0, 0, 0.02))
    elmo = [Z(cz.ball("elmo", 0.118, 16, 10, loc=c + Vector((0, 0, 0.02)), scale=(1, 1.08, 0.95)), IRON), Z(cz.cyl("aba", 0.13, 0.03, 20, loc=c + Vector((0, 0, -0.03))), IRON),
            Z(cz.box("nasal", (0.02, 0.02, 0.1), loc=c + Vector((0, -0.12, -0.04))), IRON), Z(cz.box("crista", (0.015, 0.2, 0.05), loc=c + Vector((0, 0.0, 0.13))), VERM)]
    pt = [Z(cz.box("peitoral", (0.32, 0.05, 0.36), loc=(0, -0.075, 1.2)), IRON), Z(cz.box("costas", (0.32, 0.04, 0.36), loc=(0, 0.15, 1.2)), IRON)]
    for s in (-1, 1):
        for j in range(3):
            pt.append(Z(cz.ball(f"ombreira{s}{j}", 0.1 - j * 0.012, 12, 8, loc=(s * (0.18 + j * 0.03), 0.045, 1.42 - j * 0.045), scale=(1.1, 1.2, 0.5)), IRON))
    pt.append(Z(cz.box("tabardo", (0.24, 0.03, 0.5), loc=(0, -0.105, 1.0)), VERM))
    g = RH + Vector((-0.06, 0, 0))
    lanca = [Z(cz.cyl("haste", 0.022, 2.0, 8, loc=g + Vector((-0.4, 0, 0)), rot=(0, math.pi / 2, 0)), WOOD),
             Z(cz.seg("ponta", g + Vector((-1.4, 0, 0)), g + Vector((-1.68, 0, 0)), 0.045, 0.0, 6), IRON), Z(cz.cyl("virola", 0.03, 0.05, 8, loc=g + Vector((-1.38, 0, 0)), rot=(0, math.pi / 2, 0)), IRON)]
    return {"Head": elmo, "Spine2": pt, "RightHand": lanca}

NPCS = dict(ferreiro=ferreiro, curandeira=curandeira, capita=capita, pescador=pescador, mentor=mentor, arcanista=arcanista, bruxa=bruxa,
            batedor=batedor, agricultora=agricultora, bobo=bobo, batedora=batedora, mineiro=mineiro, mascate=mascate, guarda=guarda)

def build(nid):
    cz.reset(); cz.clear_mats()
    r = NPCS[nid]()
    groups, emits = (r if isinstance(r, tuple) else (r, []))
    objs = []
    for bone, parts in groups.items():
        low = bp.finish(f"npc_{nid}_{bone}", parts, size=512, subdiv=1, extr=0.01, export=False)
        es = [(o, c, s) for (b_, o, c, s) in emits if b_ == bone]
        for o, c, s in es:
            cz.apply_tf(o); o.data.materials.clear(); o.data.materials.append(cz.mat(f"npc_{nid}_emit{c}", c, rough=0.2, emit=c, strength=s))
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active = low; bpy.ops.object.join()
        # para o espaco do encaixe: origem na cabeca do osso
        head = P[bone]
        low.data.transform(Matrix.Translation(-head))
        emp = bpy.data.objects.new(bone, None); bpy.context.scene.collection.objects.link(emp)
        low.parent = emp; low.name = f"{nid}_{bone}_peca"
        objs += [emp, low]
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=OUT + f"npc_{nid}.glb", export_format='GLB', export_yup=True, use_selection=True, export_materials='EXPORT')
    # previa: devolve as pecas ao lugar do mundo para conferir no manequim
    bpy.ops.wm.save_as_mainfile(filepath=OUT + f"npc_{nid}.blend")
    print("OK", nid, list(groups), sum(cz.tri_count([o]) for o in objs if o.type == 'MESH'))

if __name__ == "__main__":
    w = sys.argv[-1]
    for k in (NPCS if w == "all" else [w]): build(k)

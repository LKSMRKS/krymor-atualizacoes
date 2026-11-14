"""Humanoides do lote 3 no esqueleto Mixamo do manequim (mesmos 24 ossos, mesma pose T, mesmos clipes Idle/Walking/Running).
Uso: python3 build_goblin.py <id|all>
Corpo esculpido inteiro (malha unica fundida + bake PBR), pecas duras (armas, aljava, escudo) presas rigidas a um osso.
Saida em metros, tamanho real (o rig e escalado por SIZE/1,674)."""
import sys, math, json, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy
from cz import Vector, Matrix
from mathutils import noise

MANEQUIM = "/mnt/user-data/uploads/CinzasDaMare - Copia/Client/Assets/Models/manequim_masc.glb"
ROOT = "/home/claude/cinzas/lote3/humanoides/"
BASE_H = 1.674

V = {
 "mo_goblin_saqueador": dict(nome="Goblin Saqueador", size=1.22, build="magro", head=1.38,
    skin=("#5f7f30", "#2c4216", "#8a9a4a"), eyes=("#ffd22a", "#ffb000", 3.0),
    cloth=dict(tunic="#6a4a2a", rag="#4a3220", sack="#a08a60", strap="#3a2414"),
    kit=["trapos", "saco", "faca", "bandana"],
    notas="faca curta na mao direita, saco de saque nas costas; clipe extra pedido: dodge (pose do jogo)"),
 "mo_goblin_arqueiro": dict(nome="Goblin Arqueiro", size=1.18, build="magro", head=1.38,
    skin=("#56782e", "#264012", "#86964a"), eyes=("#ffd22a", "#ffb000", 3.0),
    cloth=dict(tunic="#5e3e24", rag="#3a2616", hood="#7a2a1c", strap="#2a1a10"),
    kit=["capuz", "aljava", "arco", "trapos"],
    notas="arco curto na mao esquerda, aljava nas costas; bow_draw/bow_shoot sao poses do jogo (MannequinVisual)"),
 "mo_goblin_chefe": dict(nome="Chefe Goblin", size=1.78, build="forte", head=1.22,
    skin=("#4f6e26", "#223a10", "#7a8a40"), eyes=("#ff6a1a", "#ff3000", 4.0),
    cloth=dict(tunic="#6a2018", rag="#3a120e", strap="#2a1a10", iron="#5a5650", fur="#5a4630"),
    kit=["ombreiras", "elmo", "presas", "clava", "escudo_barril", "trapos"],
    notas="elite: clava de deriva com pregos (mao dir.) e escudo de tampa de barril (antebraco esq.); roar/slam sao poses do jogo"),
 "mo_esqueleto_goblin": dict(nome="Esqueleto de Goblin", size=1.18, build="osso", head=1.3,
    skin=("#d8ccae", "#6a5e48", "#f0e6cc"), eyes=("#6ae0ff", "#20b8ff", 4.0),
    cloth=dict(rag="#2a2420", strap="#3a2a1a"),
    kit=["tanga", "machadinha"],
    notas="osso aparente (costelas, coluna, bacia), cranio rachado, olhos azuis; rise (levantar do chao) e pose do jogo"),
}

def load_rig():
    cz.reset(); cz.clear_mats()
    bpy.ops.import_scene.gltf(filepath=MANEQUIM)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    for o in list(bpy.data.objects):
        if o.type == 'MESH': bpy.data.objects.remove(o, do_unlink=True)
    P = {b.name.replace("mixamorig:", ""): (arm.matrix_world @ b.head_local) for b in arm.data.bones}
    return arm, P

def build(vid, v):
    arm, P = load_rig()
    rnd = random.Random(len(vid))
    k = v["build"]; fat = {"magro": 1.0, "forte": 1.45, "osso": 0.45}[k]
    H = P["Head"] + Vector((0, 0, 0.02 * (v["head"] - 1))); hs = v["head"]
    parts, cloth_vol, rigid = [], [], []          # cloth_vol: (centro, raios, nome_cor) elipsoides que pintam roupa
    bone = lambda n: "mixamorig:" + n
    def hp(x, y, z): return H + Vector((x, y, z)) * hs
    # ---------------------------------------------------------------- tronco (barriga de goblin, peito magro)
    if k != "osso":
        parts.append(org.blob("peito", P["Spine2"] + Vector((0, 0.0, -0.01)), (0.15 * fat, 0.10 * fat, 0.12)))
        parts.append(org.blob("barriga", P["Spine"] + Vector((0, -0.03 * fat, 0.02)), (0.14 * fat, 0.13 * fat, 0.13)))
        parts.append(org.blob("bacia", P["Hips"] + Vector((0, 0.01, -0.02)), (0.14 * fat, 0.1 * fat, 0.1)))
        parts.append(org.limb("coluna", [(P["Hips"], 0.11 * fat), (P["Spine1"], 0.12 * fat), (P["Spine2"], 0.11 * fat), (P["Neck"], 0.06 * fat)]))
        parts.append(org.limb("pescoco", [(P["Neck"] + Vector((0, 0.01, 0)), 0.05 * fat), (H + Vector((0, 0.0, 0.03)), 0.055 * fat)]))
        for s, nm in ((1, "Left"), (-1, "Right")):
            parts.append(org.blob(f"ombro{s}", P[nm + "Arm"] + Vector((-s * 0.02, 0, 0.0)), (0.07 * fat, 0.07 * fat, 0.06 * fat)))
    else:
        # ---- esqueleto: coluna em vertebras, costelas em arco, esterno, bacia
        sp = [P["Hips"], P["Spine"], P["Spine1"], P["Spine2"], P["Neck"], H]
        for i in range(len(sp) - 1):
            for t in (0.0, 0.33, 0.66):
                c = sp[i].lerp(sp[i + 1], t) + Vector((0, 0.03, 0))
                parts.append(org.blob(f"vert{i}{t}", c, (0.028, 0.024, 0.016), 12, 6))
        parts.append(org.limb("medula", [(p + Vector((0, 0.03, 0)), 0.014) for p in sp]))
        for j in range(5):
            zc = P["Spine2"].z + 0.02 - j * 0.045; w = 0.13 - abs(j - 1.2) * 0.012
            for s in (-1, 1):
                pts = []
                for a in range(9):
                    t = a / 8; ang = math.pi * t
                    pts.append((Vector((s * math.sin(ang) * w, 0.03 - (1 - math.cos(ang)) * 0.075 - 0.0, zc - t * 0.035)), 0.011))
                parts.append(org.limb(f"costela{j}{s}", pts, subdiv=1))
        parts.append(org.limb("esterno", [(Vector((0, -0.12, P["Spine2"].z + 0.03)), 0.016), (Vector((0, -0.125, P["Spine2"].z - 0.15)), 0.012)]))
        for s in (-1, 1):
            parts.append(org.limb(f"clavicula{s}", [(Vector((0.02 * s, -0.08, P["Spine2"].z + 0.04)), 0.012), (P["LeftArm" if s > 0 else "RightArm"] + Vector((0, -0.01, 0.01)), 0.016)]))
            parts.append(org.blob(f"ilio{s}", P["Hips"] + Vector((s * 0.07, 0.0, 0.0)), (0.06, 0.04, 0.055), 16, 8))
            parts.append(org.blob(f"escapula{s}", P["Spine2"] + Vector((s * 0.08, 0.07, 0.0)), (0.05, 0.015, 0.06), 12, 6))
        parts.append(org.blob("sacro", P["Hips"] + Vector((0, 0.03, -0.04)), (0.05, 0.03, 0.05), 12, 6))
    # ---------------------------------------------------------------- bracos (maos grandes, garras)
    for s, nm in ((1, "Left"), (-1, "Right")):
        sh, ar, fo, ha = P[nm + "Shoulder"], P[nm + "Arm"], P[nm + "ForeArm"], P[nm + "Hand"]
        d = (ha - fo).normalized(); he = ha + d * 0.11
        if k == "osso":
            parts.append(org.limb(f"umero{s}", [(ar, 0.026), (ar.lerp(fo, 0.5), 0.015), (fo, 0.024)]))
            for o_ in (-0.012, 0.012):
                parts.append(org.limb(f"radio{s}{o_}", [(fo + Vector((0, o_, 0)), 0.014), (ha + Vector((0, o_ * 1.3, 0)), 0.012)]))
        else:
            parts.append(org.limb(f"braco{s}", [(ar, 0.06 * fat), (ar.lerp(fo, 0.5), 0.047 * fat), (fo, 0.04 * fat), (fo.lerp(ha, 0.4), 0.05 * fat), (ha, 0.034 * fat)]))
            parts.append(org.blob(f"cotovelo{s}", fo + Vector((0, 0.015, 0)), (0.03, 0.03, 0.03), 12, 6))
        palm = ha + d * 0.05
        parts.append(org.blob(f"mao{s}", palm, (0.055, 0.045 * (0.6 if k == "osso" else 1), 0.022), 16, 8))
        for f in range(4):
            off = Vector((0, -0.03 + f * 0.02, 0)); base = palm + d * 0.035 + off
            tip = base + d * (0.07 - abs(f - 1.5) * 0.008) + Vector((0, 0, -0.025))
            parts.append(org.limb(f"dedo{s}{f}", [(base, 0.012 * (0.8 if k == "osso" else 1)), (base.lerp(tip, 0.6), 0.01), (tip, 0.003)], subdiv=1))
        th = palm + Vector((0, -0.045, -0.01)); parts.append(org.limb(f"polegar{s}", [(th, 0.012), (th + d * 0.03 + Vector((0, -0.03, -0.02)), 0.004)], subdiv=1))
    # ---------------------------------------------------------------- pernas (joelho ossudo, pes grandes)
    for s, nm in ((1, "Left"), (-1, "Right")):
        up, lg, ft, to = P[nm + "UpLeg"], P[nm + "Leg"], P[nm + "Foot"], P[nm + "ToeBase"]
        te = to + Vector((0, -0.07, -0.0))
        if k == "osso":
            parts.append(org.limb(f"femur{s}", [(up, 0.03), (up.lerp(lg, 0.5), 0.017), (lg, 0.03)]))
            parts.append(org.limb(f"tibia{s}", [(lg, 0.026), (lg.lerp(ft, 0.5), 0.015), (ft, 0.022)]))
            parts.append(org.blob(f"patela{s}", lg + Vector((0, -0.03, 0.01)), (0.025, 0.015, 0.03), 12, 6))
        else:
            parts.append(org.limb(f"coxa{s}", [(up + Vector((0, 0, 0.03)), 0.09 * fat), (up.lerp(lg, 0.5), 0.075 * fat), (lg, 0.052 * fat)]))
            parts.append(org.blob(f"joelho{s}", lg + Vector((0, -0.02, 0.0)), (0.045, 0.04, 0.045), 12, 6))
            parts.append(org.limb(f"canela{s}", [(lg, 0.055 * fat), (lg.lerp(ft, 0.35), 0.064 * fat), (ft, 0.038 * fat)]))
        parts.append(org.limb(f"pe{s}", [(ft, 0.035), (to + Vector((0, 0.01, 0.0)), (0.05, 0.03)), (te + Vector((0, 0, -0.0)), (0.045, 0.02))], flat=0.6))
        for f in range(3):
            b0 = te + Vector((s * (-0.025 + f * 0.025), 0.0, 0.0)); parts.append(org.limb(f"artelho{s}{f}", [(b0, 0.012), (b0 + Vector((0, -0.04, -0.01)), 0.003)], subdiv=1))
    # ---------------------------------------------------------------- cabeca
    if k != "osso":
        parts += [org.blob("cranio", hp(0, 0.015, 0.11), (0.125, 0.135, 0.12)), org.blob("rosto", hp(0, -0.065, 0.06), (0.1, 0.08, 0.075)),
                  org.blob("mandibula", hp(0, -0.075, 0.0), (0.085, 0.07, 0.045)), org.blob("queixo", hp(0, -0.115, 0.005), (0.05, 0.03, 0.03), 16, 8),
                  org.limb("nariz", [(hp(0, -0.12, 0.1), 0.028 * hs), (hp(0, -0.175, 0.078), 0.022 * hs), (hp(0, -0.2, 0.048), 0.012 * hs)])]
        for s in (-1, 1):
            parts.append(org.blob(f"supercilio{s}", hp(s * 0.05, -0.105, 0.128), (0.055 * hs, 0.032 * hs, 0.028 * hs), 16, 8))
            parts.append(org.blob(f"bochecha{s}", hp(s * 0.075, -0.07, 0.06), (0.04 * hs, 0.04 * hs, 0.035 * hs), 16, 8))
            if "capuz" not in v["kit"] and "elmo" not in v["kit"] or True:
                ear = org.limb(f"orelha{s}", [(hp(s * 0.11, 0.0, 0.11), 0.055 * hs), (hp(s * 0.22, 0.04, 0.16), 0.05 * hs), (hp(s * 0.3, 0.08, 0.185), 0.027 * hs), (hp(s * 0.37, 0.12, 0.2), 0.004)])
                ear.data.transform(Matrix.Translation(hp(s * 0.2, 0.04, 0.15)) @ Matrix.Scale(0.4, 4, Vector((0, 1, 0.3)).normalized()) @ Matrix.Translation(-hp(s * 0.2, 0.04, 0.15)))
                parts.append(ear)
    else:
        parts += [org.blob("cranio", hp(0, 0.015, 0.11), (0.115, 0.125, 0.11)), org.blob("maxila", hp(0, -0.07, 0.05), (0.085, 0.07, 0.055)),
                  org.blob("mandibula", hp(0, -0.075, -0.005), (0.075, 0.065, 0.03))]
        for s in (-1, 1):
            parts.append(org.blob(f"zigoma{s}", hp(s * 0.075, -0.06, 0.06), (0.03, 0.04, 0.02), 12, 6))
            parts.append(org.blob(f"arcada{s}", hp(s * 0.048, -0.1, 0.12), (0.045, 0.025, 0.02), 12, 6))
    # ---------------------------------------------------------------- roupa / equipamento fundido (pintado por volume)
    kit = v["kit"]
    def cloth(name, loc, sc, col, seg=24):
        parts.append(org.blob(name, loc, sc, seg, seg // 2)); cloth_vol.append((Vector(loc), Vector(sc) * 1.12, col))
    if "trapos" in kit:
        cloth("tunica", P["Spine"] + Vector((0, -0.01, 0.04)), (0.16 * fat, 0.14 * fat, 0.2), "tunic")
        for i in range(7):   # tiras penduradas da cintura
            a = (i / 7) * math.tau + 0.3; r_ = 0.14 * fat
            b0 = P["Hips"] + Vector((math.cos(a) * r_, math.sin(a) * r_ * 0.8, -0.04))
            tip = b0 + Vector((math.cos(a) * 0.03, math.sin(a) * 0.03, -0.17 - rnd.uniform(0, 0.08)))
            parts.append(org.limb(f"tira{i}", [(b0, (0.045, 0.012)), (tip, (0.03, 0.008))], subdiv=1)); cloth_vol.append(((b0 + tip) / 2, Vector((0.06, 0.06, 0.13)), "rag"))
    if "tanga" in kit:
        cloth("tanga", P["Hips"] + Vector((0, 0, -0.05)), (0.12, 0.09, 0.07), "rag")
        for i in range(5):
            a = (i / 5) * math.tau; b0 = P["Hips"] + Vector((math.cos(a) * 0.1, math.sin(a) * 0.08, -0.08))
            tip = b0 + Vector((0, 0, -0.16 - rnd.uniform(0, 0.06)))
            parts.append(org.limb(f"farrapo{i}", [(b0, (0.035, 0.01)), (tip, (0.02, 0.006))], subdiv=1)); cloth_vol.append(((b0 + tip) / 2, Vector((0.05, 0.05, 0.12)), "rag"))
    if "bandana" in kit:
        cloth("bandana", hp(0, 0.01, 0.14), (0.13, 0.14, 0.07), "rag")
        parts.append(org.limb("no_bandana", [(hp(0, 0.13, 0.13), 0.025), (hp(0.02, 0.2, 0.08), (0.03, 0.008))], subdiv=1)); cloth_vol.append((hp(0.01, 0.17, 0.1), Vector((0.05, 0.06, 0.06)), "rag"))
    if "saco" in kit:
        cloth("saco", P["Spine1"] + Vector((0.02, 0.17, 0.0)), (0.13, 0.1, 0.16), "sack")
        cloth("boca_saco", P["Spine2"] + Vector((0.03, 0.16, 0.08)), (0.05, 0.05, 0.04), "strap")
        parts.append(org.limb("alca", [(P["Spine2"] + Vector((0.1, 0.12, 0.08)), 0.018), (P["Spine2"] + Vector((0.08, -0.1, 0.05)), 0.018), (P["Spine"] + Vector((-0.12, -0.12, -0.02)), 0.018), (P["Spine"] + Vector((-0.1, 0.12, 0.0)), 0.018)]))
        cloth_vol.append((P["Spine1"] + Vector((0, -0.11, 0.0)), Vector((0.17, 0.05, 0.16)), "strap"))
    if "capuz" in kit:
        cloth("capuz", hp(0, 0.03, 0.12), (0.145, 0.155, 0.14), "hood")
        cloth("gola", P["Neck"] + Vector((0, 0.01, 0.0)), (0.13, 0.11, 0.06), "hood")
        cloth("capa", P["Spine1"] + Vector((0, 0.1, 0.02)), (0.17, 0.05, 0.2), "hood")
    if "ombreiras" in kit:
        for s, nm in ((1, "Left"), (-1, "Right")):
            cloth(f"ombreira{s}", P[nm + "Arm"] + Vector((-s * 0.01, 0, 0.06)), (0.11, 0.1, 0.06), "iron")
            for j in range(3):
                b0 = P[nm + "Arm"] + Vector((s * (0.0 + j * 0.04), -0.02 + j * 0.02, 0.1)); parts.append(org.spike(f"cravo{s}{j}", b0, b0 + Vector((s * 0.02, 0, 0.07)), 0.018))
                cloth_vol.append((b0 + Vector((0, 0, 0.035)), Vector((0.03, 0.03, 0.05)), "iron"))
        cloth("pele_ombro", P["Spine2"] + Vector((0, 0.03, 0.07)), (0.2, 0.13, 0.06), "fur")
        cloth("cinturao", P["Hips"] + Vector((0, -0.01, 0.06)), (0.15 * fat, 0.13 * fat, 0.04), "strap")
    if "elmo" in kit:
        cloth("elmo", hp(0, 0.02, 0.15), (0.135, 0.145, 0.1), "iron")
        cloth("nasal", hp(0, -0.12, 0.13), (0.02, 0.02, 0.05), "iron")
        for s in (-1, 1):
            parts.append(org.limb(f"chifre{s}", [(hp(s * 0.1, 0.0, 0.2), 0.035), (hp(s * 0.2, 0.0, 0.27), 0.025), (hp(s * 0.24, -0.03, 0.38), 0.004)]))
            cloth_vol.append((hp(s * 0.19, -0.01, 0.29), Vector((0.08, 0.06, 0.11)), "horn"))

    mesh = org.fuse(vid, parts, voxel=0.0055)
    org.displace(mesh, 0.004, 0.06, 'CLOUDS', 2)
    org.smooth(mesh, 0.6, 2)
    hi = pbr.make_hi(mesh)
    skin_mask = lambda p, n: 0.0 if in_cloth(p) else 1.0
    def in_cloth(p):
        for c, r, nm in cloth_vol:
            q = ((p.x - c.x) / r.x) ** 2 + ((p.y - c.y) / r.y) ** 2 + ((p.z - c.z) / r.z) ** 2
            if q < 1.0: return nm
        return None
    if k == "osso":
        pbr.detail(hi, grain=0.0006, grain_scale=120, pits=0.0012, seed=3)
    else:
        pbr.detail(hi, grain=0.0007, grain_scale=110, barnacles=18 if k != "forte" else 30, barn_r=(0.004, 0.009),
                   barn_mask=lambda p, n: in_cloth(p) is None, pits=0.0006, seed=5)
    org.decimate(mesh, 14000 if k != "forte" else 20000)

    skc = [org.lin(h) for h in v["skin"]]; col = {kk: org.lin(h) for kk, h in v["cloth"].items()}
    col["horn"] = org.lin("#d8c8a0"); ey = [hp(s * 0.045, -0.118, 0.095) for s in (-1, 1)]
    def cor(p, n, c):
        cl = in_cloth(p)
        if cl:
            base = col[cl]; tex = noise.noise(Vector((p.x * 160, p.y * 160, p.z * 40)))     # trama do tecido
            out = base * (0.85 + 0.25 * tex)
            if cl in ("iron",): out = base * (0.9 + 0.2 * noise.noise(p * 40))
            if noise.noise(p * 12 + Vector((4, 4, 4))) > 0.35 and cl != "horn": out = org.mix(out, col.get("rag", base) * 0.5, 0.5)   # mancha/sujeira
            return out
        top = org.sstep(-0.5, 0.6, n.z)
        out = org.mix(skc[1], skc[0], 0.55 + 0.45 * top)
        out = out * (0.88 + 0.22 * noise.noise(p * 18))
        if noise.noise(p * 7 + Vector((1, 2, 3))) > 0.3: out = org.mix(out, skc[2], 0.35)     # manchas claras
        for e in ey:
            de = (p - e).length
            if de < 0.04 * hs: out = org.mix(out, skc[1] * 0.25, 1 - org.sstep(0.02 * hs, 0.04 * hs, de))   # orbitas fundas
        if p.z < P["LeftToeBase"].z + 0.03 or (p - P["LeftHand"]).length > 0 and False: pass
        return out
    def rug(p, n, c):
        cl = in_cloth(p)
        if cl == "iron": return 0.38
        if cl == "horn": return 0.45
        if cl: return 0.9
        return 0.55 if k != "osso" else 0.62
    metal = lambda p, n, c: 0.85 if in_cloth(p) == "iron" else 0.0
    pbr.bake(mesh, hi, cor, rug, vid, 1024, extrusion=0.012, wear_col=(skc[2] if k == "osso" else None), dirt_col=skc[1] * 0.3, metal_fn=metal)

    # ---------------------------------------------------------------- pecas duras rigidas
    hard = []
    def rig(o, bn):
        cz.apply_tf(o); g = o.vertex_groups.new(name=bone(bn)); g.add([vv.index for vv in o.data.vertices], 1.0, 'REPLACE'); hard.append(o); return o
    em = cz.mat(vid + "_olho_emit", v["eyes"][0], rough=0.2, emit=v["eyes"][1], strength=v["eyes"][2])
    for e in ey: rig(cz.ball("olho", 0.017 * hs, 8, 6, loc=e + Vector((0, 0.004, 0)), m=em), "Head")
    bonem = cz.mat(vid + "_dente", "#efe2c4", rough=0.4)
    for s in (-1, 1):
        rig(cz.seg("dente", hp(s * 0.035, -0.112, 0.012), hp(s * 0.042, -0.122, 0.055 if "presas" in kit else 0.04), 0.013 * (1.4 if "presas" in kit else 1), 0.0, 6, m=bonem), "Head")
    RH, LH = P["RightHand"], P["LeftHand"]
    dR = (RH - P["RightForeArm"]).normalized(); dL = (LH - P["LeftForeArm"]).normalized()
    grip = RH + dR * 0.07 + Vector((0, -0.01, -0.02))
    wood = cz.mat(vid + "_madeira", "#5a3c22", rough=0.85); steel = cz.mat(vid + "_aco", "#8a8c90", rough=0.3, metal=0.9); leather = cz.mat(vid + "_couro", "#3a2416", rough=0.8)
    if "faca" in kit:    # faca: cabo enrolado em couro, lamina curva lascada, apontando para frente (-Y)
        rig(cz.cyl("cabo", 0.016, 0.1, 8, loc=grip, rot=(math.pi / 2, 0, 0), m=leather), "RightHand")
        rig(cz.box("guarda", (0.06, 0.012, 0.018), loc=grip + Vector((0, -0.055, 0)), m=steel), "RightHand")
        bl = cz.box("lamina", (0.012, 0.22, 0.04), loc=grip + Vector((0, -0.17, 0.005)), m=steel)
        for vv in bl.data.vertices:
            t = (grip.y - 0.06 - vv.co.y) / 0.22
            vv.co.z += -0.03 * t * t; vv.co.x *= 1.0
            if vv.co.z > grip.z + 0.005: vv.co.z -= 0.022 * t ** 1.5
        rig(bl, "RightHand")
    if "machadinha" in kit:
        rig(cz.cyl("cabo", 0.018, 0.42, 8, loc=grip + Vector((0, -0.12, 0)), rot=(math.pi / 2, 0, 0), m=wood), "RightHand")
        hd = cz.box("cunha", (0.02, 0.07, 0.13), loc=grip + Vector((0, -0.3, 0.07)), m=steel)
        for vv in hd.data.vertices:
            if vv.co.z > grip.z + 0.07: vv.co.y += 0.02 * (1 if vv.co.y > grip.y - 0.3 else -1)
        rig(hd, "RightHand")
    if "clava" in kit:   # tronco de deriva com pregos e cracas
        cl_ = org.limb("clava", [(grip + Vector((0, 0.06, 0)), 0.03), (grip + Vector((0, -0.2, 0.01)), 0.04), (grip + Vector((0, -0.5, 0.02)), 0.075), (grip + Vector((0, -0.62, 0.0)), 0.06)])
        cl_.data.materials.append(wood); rig(cl_, "RightHand")
        for j in range(8):
            a = j * 2.4; c0 = grip + Vector((math.cos(a) * 0.06, -0.38 - j * 0.03, math.sin(a) * 0.06))
            rig(cz.seg("prego", c0, c0 + Vector((math.cos(a), 0, math.sin(a))) * 0.07, 0.008, 0.0, 5, m=steel), "RightHand")
    if "escudo_barril" in kit:   # tampa de barril presa ao antebraco esquerdo
        fl = P["LeftForeArm"].lerp(LH, 0.5) + Vector((0, -0.09, 0))
        sh = cz.cyl("tampa", 0.24, 0.035, 20, loc=fl, rot=(math.pi / 2, 0, 0), m=wood); rig(sh, "LeftForeArm")
        for j in range(5):
            rig(cz.box("tabua", (0.012, 0.004, 0.46 * math.sqrt(max(0.05, 1 - ((j - 2) * 0.19) ** 2))), loc=fl + Vector((-0.18 + j * 0.09, -0.02, 0)), m=cz.mat(vid + "_madeira_escura", "#3a2414", rough=0.9)), "LeftForeArm")
        rig(cz.cyl("aro", 0.25, 0.04, 20, loc=fl, rot=(math.pi / 2, 0, 0), m=cz.mat(vid + "_ferro", "#4a4844", rough=0.5, metal=0.8), cap=False), "LeftForeArm")
        rig(cz.ico("umbo", 0.05, 1, loc=fl + Vector((0, -0.03, 0)), scale=(1, 0.5, 1), m=cz.mat(vid + "_ferro", "#4a4844", rough=0.5, metal=0.8)), "LeftForeArm")
    if "arco" in kit:    # arco curto na mao esquerda (vertical), corda
        g0 = LH + dL * 0.06
        bow = org.limb("arco", [(g0 + Vector((0, 0.06, -0.42)), 0.008), (g0 + Vector((0, -0.04, -0.22)), 0.018), (g0, 0.022), (g0 + Vector((0, -0.04, 0.22)), 0.018), (g0 + Vector((0, 0.06, 0.42)), 0.008)], subdiv=1)
        bow.data.materials.append(wood); rig(bow, "LeftHand")
        rig(cz.seg("corda", g0 + Vector((0, 0.06, -0.42)), g0 + Vector((0, 0.06, 0.42)), 0.003, 0.003, 4, m=cz.mat(vid + "_corda", "#d8ccb0", rough=0.8)), "LeftHand")
    if "aljava" in kit:
        q0 = P["Spine1"] + Vector((0.06, 0.13, 0.02))
        rig(cz.cyl("aljava", 0.055, 0.42, 10, loc=q0, rot=(0.0, 0.35, 0), m=leather), "Spine2")
        for j in range(5):
            tip = q0 + Vector((0.08 + j * 0.012, 0.0 + (j - 2) * 0.012, 0.28))
            rig(cz.seg("flecha", tip - Vector((0.05, 0, 0.18)), tip, 0.004, 0.004, 4, m=wood), "Spine2")
            rig(cz.box("pena", (0.004, 0.025, 0.05), loc=tip, m=cz.mat(vid + "_pena", "#b8402a", rough=0.8)), "Spine2")

    # ---------------------------------------------------------------- pesos (mundo) e rig
    segs = []
    pairs = [("Hips", "Spine"), ("Spine", "Spine1"), ("Spine1", "Spine2"), ("Spine2", "Neck"), ("Neck", "Head"), ("Head", "HeadTop_End")]
    for nm in ("Left", "Right"):
        pairs += [(nm + "Shoulder", nm + "Arm"), (nm + "Arm", nm + "ForeArm"), (nm + "ForeArm", nm + "Hand"), (nm + "Hand", None),
                  (nm + "UpLeg", nm + "Leg"), (nm + "Leg", nm + "Foot"), (nm + "Foot", nm + "ToeBase"), (nm + "ToeBase", None)]
    for a, b in pairs:
        pa = P[a]
        if b is None:
            prev = {"LeftHand": "LeftForeArm", "RightHand": "RightForeArm", "LeftToeBase": "LeftFoot", "RightToeBase": "RightFoot"}[a]
            pb = pa + (pa - P[prev]).normalized() * (0.16 if "Hand" in a else 0.08)
        else: pb = P[b]
        segs.append((bone(a), pa, pb))
    groups = {n: mesh.vertex_groups.new(name=n) for n, _, _ in segs}
    hz = H.z - 0.035
    for vv in mesh.data.vertices:
        p = vv.co
        if p.z > hz and abs(p.x) < 0.7 and abs(p.y) < 0.45:
            groups[bone("Head")].add([vv.index], 1.0, 'REPLACE'); continue
        ds = sorted((org.seg_dist(p, a, b), n) for n, a, b in segs)
        (d1, n1), (d2, n2) = ds[0], ds[1]
        w2 = math.exp(-(d2 - d1) / 0.03) * 0.5
        groups[n1].add([vv.index], 1.0 - w2, 'REPLACE')
        if w2 > 0.02: groups[n2].add([vv.index], w2, 'REPLACE')
    bpy.ops.object.select_all(action='DESELECT')
    for e in hard: e.select_set(True)
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.join()
    mesh.data.shade_smooth()
    mesh.parent = arm; mesh.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = mesh.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
    # tamanho real
    k_ = v["size"] / BASE_H
    arm.scale = arm.scale * k_
    return arm, mesh

def run(vid):
    v = V[vid]
    arm, mesh = build(vid, v)
    bpy.context.view_layer.update()
    import os
    os.makedirs(ROOT + vid, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=ROOT + vid + "/" + vid + ".blend")
    bpy.ops.export_scene.gltf(filepath=ROOT + vid + "/" + vid + ".glb", export_format='GLB', export_yup=True, export_apply=False,
                              export_animations=True, export_animation_mode='ACTIONS', export_skins=True, export_materials='EXPORT')
    info = dict(nome=v["nome"], altura_m=v["size"], tris=cz.tri_count([mesh]), ossos=len(arm.data.bones), clips=[a.name for a in bpy.data.actions], notas=v["notas"],
                esqueleto="mixamorig (mesmo do manequim_masc), pose T", kit=v["kit"])
    json.dump(info, open(ROOT + vid + "/" + vid + "_info.json", "w"), ensure_ascii=False, indent=1)
    print("OK", vid, info["tris"], [round(d, 2) for d in mesh.dimensions])

if __name__ == "__main__":
    w = sys.argv[-1]
    for vid in (V if w == "all" else [w]): run(vid)

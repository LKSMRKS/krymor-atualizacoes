"""Caranguejos organicos (malha unica esculpida + textura pintada). Uso: python3 build_crab_organic.py <variante|all>
Mesmo esqueleto e mesmos nomes de clipes da familia anterior (compativel com a logica do jogo).
Tudo e construido em escala 1 e escalado por S no fim (malha + ossos)."""
import sys, math, json, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, bpy
from cz import Vector, Matrix
from mathutils import noise
import build_crab_family as fam

ROOT = "/home/claude/cinzas/assets/inimigos/caranguejos/"
F = fam.VAR
V = {
 "caranguejo_de_cal": dict(F["caranguejo_de_cal"], kR=1.22, kL=0.6, legs=0.95, leg_r=1.0, BZ=0.36, cara=(0.56, 0.42, 0.19), lobes=1.0,
     col=dict(shell="#7d2a1a", shell2="#2e0e09", belly="#d6b487", tip="#1a1210", crust="#e8dfcc"), crust=True, crystals=True,
     eyes=("#ffb12e", "#ff7a00", 3.0), eye_r=1.0, brow="angry", spikes=1.35, tub=10, claw="normal", rough=1.0, target=15000),
 "caranguejinho": dict(F["caranguejinho"], kR=0.78, kL=0.72, legs=1.32, leg_r=0.72, BZ=0.42, cara=(0.46, 0.40, 0.25), lobes=0.5,
     col=dict(shell="#d0652a", shell2="#6a2a12", belly="#f0d2a0", tip="#2a1810", spot="#f6e2b8"), spots=True,
     eyes=("#1c1410", None, 0), eye_r=1.9, brow="none", spikes=0.6, tub=4, claw="normal", rough=0.6, target=10000, pupils=True),
 "caranguejo_ermitao_barril": dict(F["caranguejo_ermitao_barril"], kR=1.05, kL=0.7, legs=0.82, leg_r=1.3, BZ=0.34, cara=(0.36, 0.30, 0.17), lobes=0.6,
     col=dict(shell="#a8402a", shell2="#4e1a10", belly="#e6b27a", tip="#24140e", spot="#e8b06a"), spots=True, barrel=True,
     eyes=("#1c1410", None, 0), eye_r=1.45, brow="sleepy", spikes=0.5, tub=3, claw="normal", rough=0.8, target=13000, pupils=True, body_y=-0.16),
 "caranguejo_pinca_de_ferro": dict(F["caranguejo_pinca_de_ferro"], kR=1.42, kL=1.18, legs=0.85, leg_r=1.35, BZ=0.31, cara=(0.6, 0.45, 0.21), lobes=1.2,
     col=dict(shell="#5a1a1c", shell2="#1e0a0b", belly="#a8805c", tip="#140c0c"), iron=True,
     eyes=("#ff5020", "#ff3000", 4.0), eye_r=0.9, brow="heavy", spikes=1.1, tub=8, claw="iron", rough=1.1, target=15000),
 "caranguejo_de_pedra": dict(F["caranguejo_de_pedra"], kR=0.95, kL=0.85, legs=0.8, leg_r=1.45, BZ=0.33, cara=(0.56, 0.46, 0.32), lobes=0.4,
     col=dict(shell="#6e6862", shell2="#2e2a28", belly="#b8a486", tip="#1e1c1a", moss="#4a6a2e"), rocks=True, moss=True, spike_bones=True,
     eyes=("#ffd23a", "#ffb000", 3.0), eye_r=0.95, brow="sleepy", spikes=0.8, tub=0, claw="normal", rough=1.9, target=15000),
 "caranguejo_escavador": dict(F["caranguejo_escavador"], kR=1.15, kL=1.15, legs=1.5, leg_r=1.2, BZ=0.5, cara=(0.6, 0.44, 0.18), lobes=1.3,
     col=dict(shell="#4e1a2c", shell2="#1a0810", belly="#c49a72", tip="#120608", crust="#d8c8b0"), ridges=True,
     eyes=("#ff3a3a", "#ff1010", 4.0), eye_r=1.0, brow="angry", spikes=1.6, tub=16, claw="shovel", rough=1.1, target=15000),
 "rainha_das_cascas": dict(F["caranguejo_pinca_de_ferro"], nome="Rainha das Cascas", S=1.3, notas="chefinha venenosa (nv 9): casco verde-escuro coberto de espinhos e cristais de veneno que brilham",
     kR=1.3, kL=1.3, legs=1.0, leg_r=1.25, BZ=0.37, cara=(0.62, 0.48, 0.24), lobes=1.5,
     col=dict(shell="#24401a", shell2="#0a1406", belly="#b8bc7a", tip="#0a1006", crust="#a6e04a"), crust=True, crystals=True,
     eyes=("#c8ff3a", "#9aff10", 4.0), eye_r=1.0, brow="heavy", spikes=2.3, tub=20, claw="normal", rough=1.2, target=16000),
}

def surf_z(C0, cx_, cy_, cz_, x, y, k=1.0):
    q2 = (x / cx_) ** 2 + ((y - C0.y) / cy_) ** 2
    return C0.z + cz_ * math.sqrt(max(0.0, 1 - q2)) * k

def oriented_blob(name, loc, scale, direction, seg=24, rings=12):
    d = Vector(direction).normalized()
    yaw = math.atan2(d.x, -d.y); pitch = math.asin(max(-1, min(1, d.z)))
    o = cz.ball(name, 1, seg // 2, rings // 2, loc=loc, scale=scale, rot=(pitch, 0, yaw))
    cz.apply_tf(o); return o

def vgroup(o, bone):
    cz.apply_tf(o)
    g = o.vertex_groups.new(name=bone); g.add([vv.index for vv in o.data.vertices], 1.0, 'REPLACE'); return o

def build(vid, v):
    cz.reset(); cz.clear_mats()
    S, BZ = v["S"], v["BZ"]
    cx_, cy_, cz_ = v["cara"]
    C0 = Vector((0, v.get("body_y", 0.02), BZ + 0.02))
    parts, bones, tips, joints, crusts, extras = [], [], [], [], [], []
    X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
    LB = v["lobes"]

    # ------------------------------------------------ carapaca esculpida
    c = org.blob("carapaca", C0, (cx_, cy_, cz_), 48, 24)
    for p in c.data.vertices:
        q = Vector(((p.co.x - C0.x) / cx_, (p.co.y - C0.y) / cy_, (p.co.z - C0.z) / cz_))
        k = 1.0
        if q.z < -0.05: k = 1.0 - 0.24 * org.sstep(-0.05, -0.55, q.z)
        k *= 1.0 + 0.06 * math.exp(-((q.z + 0.02) ** 2) / 0.01)
        if q.y > 0.35: k *= 1.0 - 0.12 * (q.y - 0.35)
        x, y = q.x * k, q.y * k
        if y < -0.35: y = -0.35 + (y + 0.35) * 0.55
        z = q.z
        if q.z > 0:
            z += LB * 0.22 * math.exp(-((abs(q.x) - 0.42) ** 2) / 0.03) * math.exp(-((q.y + 0.05) ** 2) / 0.2)
            z += LB * 0.18 * math.exp(-(q.x ** 2) / 0.05) * math.exp(-((q.y + 0.3) ** 2) / 0.05)
            z -= LB * 0.16 * math.exp(-(q.x ** 2) / 0.006) * (1 if q.y > -0.25 else 0.3)
            z -= LB * 0.10 * math.exp(-((q.y - 0.12) ** 2) / 0.004) * math.exp(-(q.x ** 2) / 0.12)
        p.co = Vector((C0.x + x * cx_, C0.y + y * cy_, C0.z + z * cz_))
    parts.append(c)
    parts.append(org.blob("torax", C0 + Vector((0, 0.02, -0.10)), (cx_ * 0.72, cy_ * 0.72, 0.10)))
    parts.append(org.blob("abdomen", C0 + Vector((0, 0.10, -0.16)), (0.17, 0.22, 0.045)))
    Rx, Ry = cx_ * 1.04, cy_ * 0.86
    for s in (-1, 1):
        for i, phi in enumerate((28, 42, 56, 70, 84)):
            a = math.radians(phi)
            base = Vector((s * math.sin(a) * Rx * 0.97, C0.y - math.cos(a) * Ry, C0.z - 0.01))
            out = Vector((s * math.sin(a), -math.cos(a) * 0.6, 0.15)).normalized()
            L = (0.07 + 0.05 * (i == 2) + 0.03 * (i == 3)) * v["spikes"]
            parts.append(org.spike(f"esp{s}{i}", base - out * 0.02, base + out * L, 0.032)); tips.append(base + out * L)
    for x in (-0.13, -0.05, 0.05, 0.13):
        base = Vector((x, C0.y - Ry, C0.z + 0.03))
        parts.append(org.spike(f"rost{x}", base + Vector((0, 0.03, 0)), base + Vector((x * 0.25, -0.07 * v["spikes"], -0.02)), 0.025))
    rnd = random.Random(3)
    for i in range(v["tub"]):
        x, y = rnd.uniform(-cx_ * 0.7, cx_ * 0.7), C0.y + rnd.uniform(-cy_ * 0.6, cy_ * 0.7)
        q2 = (x / cx_) ** 2 + ((y - C0.y) / cy_) ** 2
        if q2 > 0.75: continue
        zb = surf_z(C0, cx_, cy_, cz_, x, y, 0.95)
        h = 0.045 * (2.2 if v.get("ridges") else 1.0)
        parts.append(org.spike(f"tub{i}", (x, y, zb - 0.02), (x * 1.05, y + (0.03 if v.get("ridges") else 0), zb + h), 0.022 * (1.4 if v.get("ridges") else 1)))
    if v.get("ridges"):   # cristas dorsais longitudinais + espinhos traseiros longos
        for x in (-0.17, 0.17):
            for j in range(5):
                y = C0.y - 0.2 + j * 0.11
                zb = surf_z(C0, cx_, cy_, cz_, x, y, 0.98)
                parts.append(org.blob(f"crista{x}{j}", (x, y, zb), (0.035, 0.07, 0.035), 12, 6))
        for x in (-0.25, -0.1, 0.1, 0.25):
            b0 = Vector((x, C0.y + cy_ * 0.75, C0.z + 0.02))
            parts.append(org.spike(f"espT{x}", b0, b0 + Vector((x * 0.6, 0.16, 0.1)), 0.035)); tips.append(b0 + Vector((x * 0.6, 0.16, 0.1)))
    if v.get("crust"):
        for i, (x, y, r) in enumerate(((0.0, 0.08, 0.13), (-0.25, -0.05, 0.09), (0.24, -0.08, 0.1), (-0.16, 0.22, 0.08), (0.18, 0.2, 0.07), (0.05, -0.2, 0.06))):
            zb = surf_z(C0, cx_, cy_, cz_, x, y) + 0.01
            parts.append(org.blob(f"crosta{i}", (x, y, zb), (r, r * 0.85, 0.035), 16, 8)); crusts.append((Vector((x, y, zb)), r * 1.15))
    if v.get("rocks"):    # pedregulhos fundidos na carapaca
        for i, (x, y, r) in enumerate(((0.0, 0.05, 0.17), (-0.26, -0.06, 0.12), (0.25, -0.04, 0.13), (-0.15, 0.24, 0.11), (0.17, 0.25, 0.1), (0.05, -0.22, 0.09), (-0.33, 0.12, 0.08), (0.33, 0.14, 0.08))):
            zb = surf_z(C0, cx_, cy_, cz_, x, y) - 0.02
            rk = cz.ico(f"rocha{i}", r, 2, loc=(x, y, zb), scale=(1.15, 1.0, 0.7), rot=(0.2 * i, 0.1, i)); cz.apply_tf(rk); cz.jitter(rk, 0.05, i)
            parts.append(rk)
    for s in (-1, 1):
        parts.append(org.blob(f"maxi{s}", (s * 0.045, C0.y - Ry * 0.93, C0.z - 0.08), (0.05, 0.03, 0.075), 16, 8))
        parts.append(org.spike(f"mand{s}", (s * 0.03, C0.y - Ry * 0.98, C0.z - 0.1), (s * 0.015, C0.y - Ry * 1.05, C0.z - 0.19), 0.022))

    bones += [("root", (0, 0, 0), (0, 0, 0.15), None), ("body", tuple(C0), tuple(C0 + Vector((0, -0.2, 0))), "root")]
    # ------------------------------------------------ olhos
    eyes = []
    er = v["eye_r"]
    for s, nm in ((1, "L"), (-1, "R")):
        h = Vector((s * 0.10, C0.y - Ry * 0.82, C0.z + 0.08)); t = Vector((s * (0.13 + 0.02 * (er - 1)), C0.y - Ry * 0.98, C0.z + 0.17 + 0.03 * (er - 1)))
        parts.append(org.limb(f"haste{nm}", [(h, 0.035), (h.lerp(t, 0.6), 0.03), (t, 0.042 * min(er, 1.3))]))
        bones.append((f"eye_{nm}", tuple(h), tuple(t), "body"))
        ec = t + Vector((0, -0.025 * er, 0.012))
        if v.get("pupils"):   # olho grande claro com pupila escura (caricato)
            eb = cz.ball(f"olho{nm}", 0.037 * er, 10, 8, loc=ec, scale=(1, 0.92, 1.05))
            pu = cz.ball(f"pupila{nm}", 0.017 * er, 8, 6, loc=ec + Vector((-s * 0.004, -0.031 * er, 0.004)), scale=(1, 0.5, 1.2))
            eyes += [(eb, f"eye_{nm}", "branco"), (pu, f"eye_{nm}", "pupila")]
        else:
            eb = cz.ball(f"olho{nm}", 0.035 * er, 8, 6, loc=ec, scale=(1, 0.9, 1.1)); eyes.append((eb, f"eye_{nm}", "emit"))
        br = v["brow"]
        if br != "none":
            tilt = {"angry": s * 0.55, "heavy": s * 0.7, "sleepy": -s * 0.25}[br]
            dz = {"angry": 0.038, "heavy": 0.034, "sleepy": 0.022}[br] * er
            sc = (0.05, 0.045, 0.026 if br != "heavy" else 0.035)
            lid = org.blob(f"sobrolho{nm}", ec + Vector((-s * 0.005, 0.0, dz)), tuple(x_ * er for x_ in sc), 16, 8)
            lid.data.transform(Matrix.Translation(ec) @ Matrix.Rotation(tilt, 4, 'Y') @ Matrix.Rotation(0.2, 4, 'X') @ Matrix.Translation(-ec)); parts.append(lid)

    # ------------------------------------------------ patas
    LEGS = {}
    dirs = [(1, -0.30), (1, 0.0), (1, 0.32), (0.85, 0.65)]
    ys = [C0.y - 0.12, C0.y - 0.02, C0.y + 0.08, C0.y + 0.17]
    LL, LR = v["legs"], v["leg_r"]
    for s, nm in ((1, "L"), (-1, "R")):
        for i in range(4):
            d = Vector((s * dirs[i][0], dirs[i][1], 0)).normalized(); LEGS[(nm, i)] = d
            a = Vector((s * cx_ * 0.72, ys[i], BZ - 0.06))
            up = 0.17 * LL + (BZ - 0.36) * 0.5
            n1 = a + d * 0.08 * LL + Z * 0.03
            n2 = a + d * 0.21 * LL + Z * up * 0.65
            kn = a + d * 0.34 * LL + Z * up
            n4 = a + d * 0.41 * LL + Z * up * 0.75
            n5 = a + d * 0.47 * LL + Z * up * 0.3
            ft = a + d * 0.56 * LL; ft.z = 0.012
            n6 = n5.lerp(ft, 0.5) + d * 0.01
            parts.append(org.limb(f"pata{nm}{i}", [(a, 0.055 * LR), (n1, 0.045 * LR), (n2, 0.085 * LR), (kn, 0.04 * LR), (n4, 0.06 * LR), (n5, 0.032 * LR), (n6, 0.038 * LR), (ft, 0.008)], flat=0.72))
            for j, t_ in enumerate((0.35, 0.65)):
                pb = n1.lerp(kn, t_) + Z * 0.05 * LR
                parts.append(org.spike(f"espP{nm}{i}{j}", pb - Z * 0.02, pb + Z * 0.05 * max(1, v["spikes"] * 0.8) + d * 0.02, 0.018 * LR))
            tips.append(ft); joints += [kn, n5]
            bones += [(f"leg_{nm}{i + 1}_a", tuple(a), tuple(kn), "body"), (f"leg_{nm}{i + 1}_b", tuple(kn), tuple(ft), f"leg_{nm}{i + 1}_a", True)]

    # ------------------------------------------------ pincas
    CL = {}
    style = v["claw"]
    def claw(nm, s, k):
        sh = Vector((s * cx_ * 0.62, C0.y - cy_ * 0.72, BZ - 0.02))
        el = sh + Vector((s * 0.22, -0.08, 0.06)) * k
        wr = el + Vector((-s * 0.06, -0.24, 0.07)) * k
        pc = wr + Vector((-s * 0.05, -0.15, 0.0)) * k
        fb = wr + Vector((-s * 0.09, -0.31, -0.035)) * k
        hg = wr + Vector((-s * 0.075, -0.29, 0.10)) * k
        f_tip = fb + Vector((-s * 0.10, -0.32, 0.06)) * k
        d_tip = hg + Vector((-s * 0.09, -0.32, -0.07)) * k
        parts.append(org.limb(f"braco{nm}", [(sh, 0.06 * k), (sh.lerp(el, 0.5), 0.09 * k), (el, 0.05 * k), (el.lerp(wr, 0.5), 0.095 * k), (wr, 0.075 * k)], flat=0.85))
        if style == "shovel":   # pa achatada e larga
            dirv = (fb - wr)
            parts.append(oriented_blob(f"pa{nm}", pc + Vector((0, -0.04, -0.02)) * k, (0.2 * k, 0.24 * k, 0.07 * k), dirv))
            fd = (f_tip - fb)
            parts.append(oriented_blob(f"dedoPa{nm}", fb.lerp(f_tip, 0.45) + Vector((0, 0, -0.02)) * k, (0.12 * k, 0.2 * k, 0.045 * k), fd))
            parts.append(oriented_blob(f"dactPa{nm}", hg.lerp(d_tip, 0.45), (0.1 * k, 0.19 * k, 0.04 * k), d_tip - hg))
            for j in range(4):   # garras da borda da pa
                tb = fb + Vector((-s * (0.0 + j * 0.05), -0.12 - 0.02 * j, -0.02)) * k
                parts.append(org.spike(f"unhaPa{nm}{j}", tb, tb + Vector((-s * 0.02, -0.1, -0.03)) * k, 0.03 * k)); tips.append(tb + Vector((-s * 0.02, -0.1, -0.03)) * k)
        else:
            parts.append(org.limb(f"palma{nm}", [(wr, 0.08 * k), (pc, (0.20 * k, 0.15 * k)), (fb, 0.125 * k),
                                                 (fb + Vector((-s * 0.05, -0.12, 0.0)) * k, 0.09 * k), (fb + Vector((-s * 0.09, -0.24, 0.03)) * k, 0.05 * k), (f_tip, 0.01 * k)]))
            parts.append(org.limb(f"dactilo{nm}", [(hg, 0.09 * k), (hg + Vector((-s * 0.05, -0.12, 0.02)) * k, 0.075 * k),
                                                   (hg + Vector((-s * 0.08, -0.24, -0.02)) * k, 0.035 * k), (d_tip, 0.01 * k)]))
            for j in range(4):
                tf = fb.lerp(f_tip, 0.2 + j * 0.18); parts.append(org.spike(f"denteF{nm}{j}", tf - Z * 0.01 * k, tf + Z * 0.05 * k, 0.017 * k))
                td = hg.lerp(d_tip, 0.25 + j * 0.17); parts.append(org.spike(f"denteD{nm}{j}", td + Z * 0.01 * k, td - Z * 0.045 * k, 0.015 * k))
            if style != "iron":
                for j in range(5):
                    pb = wr.lerp(fb, 0.15 + j * 0.17) + Vector((s * 0.09, 0, 0.09)) * k
                    parts.append(org.spike(f"nod{nm}{j}", pb - Vector((s * 0.03, 0, 0.03)) * k, pb + Vector((s * 0.02, 0, 0.04)) * k, 0.024 * k))
                for j, (u, w_) in enumerate(((0.3, 0.07), (0.55, 0.06), (0.45, 0.05))):
                    pb = wr.lerp(fb, u) + Vector((s * 0.11, 0.02 * j, 0.03 - 0.06 * j)) * k
                    parts.append(org.blob(f"calombo{nm}{j}", pb, (w_ * k, w_ * 1.3 * k, w_ * 0.9 * k), 16, 8))
        for j in range(3):
            pb = el.lerp(wr, 0.25 + j * 0.25) + Vector((s * 0.06, 0, 0.06)) * k
            parts.append(org.spike(f"espB{nm}{j}", pb - Z * 0.02, pb + Vector((s * 0.03, 0, 0.05)) * k, 0.02 * k))
        tips.extend([f_tip, d_tip]); joints.extend([el, wr])
        CL[nm] = dict(s=s, k=k, wr=wr, pc=pc, fb=fb, hg=hg, f_tip=f_tip, d_tip=d_tip)
        bones.extend([(f"claw_{nm}_upper", tuple(sh), tuple(el), "body"), (f"claw_{nm}_fore", tuple(el), tuple(wr), f"claw_{nm}_upper", True),
                      (f"claw_{nm}_hand", tuple(wr), tuple(fb), f"claw_{nm}_fore", True), (f"claw_{nm}_finger", tuple(hg), tuple(d_tip), f"claw_{nm}_hand")])
    claw("R", -1, v["kR"]); claw("L", 1, v["kL"])

    # ------------------------------------------------ pecas duras (nao fundidas)
    if v.get("spike_bones"):
        sm = cz.mat(vid + "_espinho", "#3a3532", rough=0.7); gm = cz.mat(vid + "_espinho_ponta_emit", "#c8ff7a", rough=0.2, emit="#8cf03a", strength=2.5)
        for i, (x, y) in enumerate(((-0.27, 0.06), (-0.1, 0.24), (0.1, 0.24), (0.27, 0.06), (-0.18, -0.14), (0.18, -0.14))):
            base = Vector((x, C0.y + y, surf_z(C0, cx_, cy_, cz_, x, C0.y + y) + 0.02))
            d = Vector((x * 1.6, y * 1.2, 0.55)).normalized()
            bn = f"spike_{i + 1}"; bones.append((bn, tuple(base), tuple(base + d * 0.18), "body"))
            sp = cz.shard(f"espinho{i}", 0.05, 0.26, 6, m=sm, seed=i); sp.rotation_mode = 'QUATERNION'; sp.rotation_quaternion = d.to_track_quat('Z', 'Y'); sp.location = base - d * 0.05
            tp = cz.shard(f"espinhoP{i}", 0.022, 0.1, 5, loc=base + d * 0.15, m=gm, seed=i + 7); tp.rotation_mode = 'QUATERNION'; tp.rotation_quaternion = d.to_track_quat('Z', 'Y')
            extras += [vgroup(sp, bn), vgroup(tp, bn)]
    if v.get("iron"):
        im = cz.mat(vid + "_ferro", "#6e6a66", rough=0.5, metal=0.8); im2 = cz.mat(vid + "_ferro_escuro", "#2e2c2c", rough=0.55, metal=0.7)
        hot = cz.mat(vid + "_brasa_emit", "#ffb347", rough=0.3, emit="#ff5a10", strength=6)
        for nm, c_ in CL.items():
            s, k = c_["s"], c_["k"]; dirv = (c_["fb"] - c_["wr"]).normalized()
            for j, u in enumerate((0.3, 0.55, 0.8)):      # cintas da manopla abracando a palma
                p = c_["wr"].lerp(c_["fb"], u) + Vector((-s * 0.04, 0, 0)) * k
                band = cz.cyl(f"cinta{nm}{j}", 0.215 * k * (1.02 - 0.35 * abs(u - 0.5)), 0.06 * k, 16, m=im2, cap=False)
                band.rotation_mode = 'QUATERNION'; band.rotation_quaternion = dirv.to_track_quat('Z', 'Y'); band.location = p
                band.scale = (1.0, 0.82, 1.0); extras.append(vgroup(band, f"claw_{nm}_hand"))
            plate = oriented_blob(f"placaM{nm}", c_["pc"] + Vector((s * 0.03, 0, 0.11)) * k, (0.15 * k, 0.22 * k, 0.05 * k), dirv)
            plate.data.materials.append(im); extras.append(vgroup(plate, f"claw_{nm}_hand"))
            for j in range(4):
                pb = c_["wr"].lerp(c_["fb"], 0.1 + j * 0.25) + Vector((s * 0.03, 0, 0.16)) * k
                sp = cz.seg(f"cravoM{nm}{j}", pb - Z * 0.02 * k, pb + Vector((s * 0.02, 0, 0.08)) * k, 0.028 * k, 0.0, 8, m=im); extras.append(vgroup(sp, f"claw_{nm}_hand"))
            for tip, bn, base in ((c_["f_tip"], f"claw_{nm}_hand", c_["fb"]), (c_["d_tip"], f"claw_{nm}_finger", c_["hg"])):
                dd = (tip - base).normalized()
                cap = cz.seg(f"brasa{nm}{bn}", tip - dd * 0.12 * k, tip + dd * 0.02, 0.03 * k, 0.0, 8, m=hot); extras.append(vgroup(cap, bn))
        for i, (x, y, r) in enumerate(((0.0, 0.0, 0.16), (-0.27, -0.06, 0.1), (0.27, -0.06, 0.1))):   # placas aparafusadas
            zb = surf_z(C0, cx_, cy_, cz_, x, C0.y + y) + 0.005
            pl = cz.ball(f"placaC{i}", 1, 12, 6, loc=(x, C0.y + y, zb), scale=(r, r * 0.9, 0.03), m=im); extras.append(vgroup(pl, "body"))
            for j in range(4):
                a = j * math.tau / 4 + 0.4
                rv = cz.ico(f"reb{i}{j}", 0.016, 1, loc=(x + math.cos(a) * r * 0.75, C0.y + y + math.sin(a) * r * 0.7, zb + 0.03), m=im2); extras.append(vgroup(rv, "body"))
        crack = cz.box("fenda", (0.03, 0.26, 0.02), loc=(0, C0.y + 0.16, surf_z(C0, cx_, cy_, cz_, 0, C0.y + 0.16) - 0.005), m=hot); extras.append(vgroup(crack, "body"))
    if v.get("barrel"):
        wm = cz.mat(vid + "_madeira", "#6b4527", rough=0.85); wm2 = cz.mat(vid + "_madeira_escura", "#3e2717", rough=0.85)
        fm = cz.mat(vid + "_ferro", "#4b4e55", rough=0.45, metal=0.85); dm = cz.mat(vid + "_interior", "#120a06", rough=0.9)
        bc = Vector((0, C0.y + 0.42, BZ + 0.16)); R_, L_ = 0.38, 0.66
        bones.append(("barrel", tuple(bc), tuple(bc + Vector((0, 0.3, 0))), "root"))
        brl = cz.cyl("barril", R_, L_, 14, loc=bc, rot=(math.pi / 2, 0, 0), m=wm); cz.apply_tf(brl)
        for vv in brl.data.vertices:
            t = abs(vv.co.y - bc.y) / (L_ / 2); kk = 1 + 0.12 * (1 - t * t)
            vv.co.x *= kk; vv.co.z = bc.z + (vv.co.z - bc.z) * kk
        cz.jitter(brl, 0.008, 5); extras.append(vgroup(brl, "barrel"))
        extras.append(vgroup(cz.cyl("boca", R_ * 0.93, 0.02, 14, loc=bc + Vector((0, -L_ / 2 - 0.004, 0)), rot=(math.pi / 2, 0, 0), m=dm), "barrel"))
        for i in range(9):
            a = i * math.tau / 9
            extras.append(vgroup(cz.box(f"aduela{i}", (0.035, L_ * 0.98, 0.02), loc=bc + Vector((math.cos(a) * R_ * 1.09, 0, math.sin(a) * R_ * 1.09)), rot=(0, -a + math.pi / 2, 0), m=wm2), "barrel"))
        for j, yy in enumerate((-0.27, 0.0, 0.27)):
            extras.append(vgroup(cz.cyl(f"aro{j}", R_ * (1.15 if j == 1 else 1.1), 0.05, 14, loc=bc + Vector((0, yy, 0)), rot=(math.pi / 2, 0, 0), m=fm, cap=False), "barrel"))
        for i in range(10):
            t = i / 9; a = math.radians(35 + t * 120)
            p = bc + Vector((math.cos(a) * R_ * 1.2, -0.3 + t * 0.6, math.sin(a) * R_ * 1.2))
            extras.append(vgroup(cz.cyl(f"elo{i}", 0.045, 0.018, 8, loc=p, rot=((0.0 if i % 2 else math.pi / 2), a, 0.4), m=fm, cap=False), "barrel"))
        extras.append(vgroup(cz.box("tampa", (0.22, 0.14, 0.04), loc=bc + Vector((0.0, 0.12, R_ * 1.12)), rot=(0, 0.1, 0.2), m=wm2), "barrel"))
        for i in range(3):   # cracas no barril (vida marinha)
            a = math.radians(60 + i * 40)
            p = bc + Vector((math.cos(a) * R_ * 1.12, -0.15 + i * 0.12, math.sin(a) * R_ * 1.12))
            extras.append(vgroup(cz.cyl(f"craca{i}", 0.04, 0.05, 10, r2=0.02, loc=p, rot=(0, math.pi / 2 - a, 0), m=cz.mat(vid + "_craca", "#d8d0c0", rough=0.9)), "barrel"))

    # ------------------------------------------------ fusao, relevo, reducao
    mesh = org.fuse(vid, parts, voxel=0.0085)
    rgh = v["rough"]
    org.displace(mesh, 0.022 * rgh, 0.10, 'CLOUDS', 2)
    org.displace(mesh, -0.007 * rgh, 0.03, 'VORONOI')
    org.smooth(mesh, 0.5, 1)
    org.decimate(mesh, v["target"])

    # ------------------------------------------------ pintura procedural
    col = {k: org.lin(h) for k, h in v["col"].items()}
    def fn(p, n):
        top = org.sstep(-0.35, 0.45, n.z)
        c = org.mix(col["belly"], col["shell"], top)
        if p.z < BZ - 0.12 and abs(p.x) < cx_ * 0.8 and abs(p.y - C0.y) < cy_ * 0.8: c = org.mix(c, col["belly"], 0.7)
        n1 = noise.noise(p * 9.0); n2 = noise.noise(p * 38.0)
        c = c * (1.0 - 0.28 * max(0, n1)) * (1.0 + 0.12 * min(0, n1))
        if n2 > 0.42: c = org.mix(c, col["shell2"], 0.55)
        if v.get("spots") and n.z > -0.1:
            ns = noise.noise(p * 15.0 + Vector((3, 1, 7)))
            if ns > 0.32: c = org.mix(c, col["spot"], org.sstep(0.32, 0.42, ns) * 0.85)
        if v.get("moss") and n.z > 0.35:
            nm_ = noise.noise(p * 5.0 + Vector((9, 2, 1)))
            c = org.mix(c, col["moss"], org.sstep(0.0, 0.3, nm_) * org.sstep(0.35, 0.7, n.z) * 0.9)
        if v.get("rocks"):
            cr = abs(noise.noise(p * 22.0 + Vector((1, 5, 2))))
            if cr < 0.05: c = org.mix(c, col["shell2"], 0.7)   # rachaduras
        dj = min((p - j).length for j in joints)
        c = org.mix(c, col["shell2"], 1 - org.sstep(0.015, 0.06, dj))
        dt = min((p - t).length for t in tips)
        c = org.mix(c, col["tip"], 1 - org.sstep(0.03, 0.16, dt))
        for cc, r in crusts:
            dc = (p - cc).length
            if dc < r and n.z > 0.2:
                c = org.mix(c, col["crust"], (1 - org.sstep(r * 0.6, r, dc)) * (0.75 + 0.25 * noise.noise(p * 25)))
        return c
    org.paint(mesh, fn)
    m = org.vc_material(vid + "_pele", 0.55); mesh.data.materials.clear(); mesh.data.materials.append(m)
    org.bake_to_texture(mesh, vid + "_cor", 1024)

    # ------------------------------------------------ rig + pesos
    arm = cz.armature(vid, [(b[0], Vector(b[1]), Vector(b[2]), *b[3:]) for b in bones])
    def in_body(p):
        q = ((p.x - C0.x) / (cx_ * 0.98)) ** 2 + ((p.y - C0.y) / (cy_ * 0.98)) ** 2 + ((p.z - C0.z) / (cz_ * 1.6)) ** 2
        return q < 1.0
    excl = ["root"] + [b[0] for b in bones if b[0].startswith("spike_") or b[0] == "barrel"]
    org.weights_by_bones(mesh, arm, regions=[("body", in_body)], exclude=tuple(excl))
    # olhos
    if v.get("pupils"):
        wmat = cz.mat(vid + "_olho", "#f4ecd8", rough=0.25); pmat = cz.mat(vid + "_pupila", "#120c0a", rough=0.2)
    em = cz.mat(vid + "_olho_emit", v["eyes"][0], rough=0.25, emit=v["eyes"][1], strength=v["eyes"][2]) if v["eyes"][1] else None
    for eb, bn, kind in eyes:
        eb.data.materials.append({"branco": wmat if v.get("pupils") else None, "pupila": pmat if v.get("pupils") else None, "emit": em}[kind])
        extras.append(vgroup(eb, bn))
    if v.get("crystals"):
        sm = cz.mat(vid + "_sal_emit", "#7fe8d8", rough=0.15, emit="#2fe0c8", strength=2.2)
        for i, (x, y, h, t) in enumerate([(-0.06, 0.13, 0.13, (0.35, -0.25)), (0.05, 0.16, 0.10, (0.4, 0.3)), (0.0, 0.25, 0.09, (0.7, 0.0)), (-0.2, 0.12, 0.07, (0.3, -0.6))]):
            zb = surf_z(C0, cx_, cy_, cz_, x, y) - 0.01
            sh_ = cz.shard(f"sal{i}", 0.03 + h * 0.15, h, 5, loc=(x, y, zb), rot=(t[0], t[1], i), m=sm, seed=i); extras.append(vgroup(sh_, "body"))
    bpy.ops.object.select_all(action='DESELECT')
    for e in extras: e.select_set(True)
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.join()
    mesh.data.shade_smooth(); mesh.data.set_sharp_from_angle(angle=math.radians(50))
    # escala final
    if abs(S - 1) > 1e-6:
        mesh.data.transform(Matrix.Scale(S, 4))
        bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='EDIT')
        for eb_ in arm.data.edit_bones: eb_.head *= S; eb_.tail *= S
        bpy.ops.object.mode_set(mode='OBJECT')
    return arm, mesh, LEGS

def run(vid):
    v = V[vid]
    arm, mesh, LEGS = build(vid, v)
    skip = fam.anims(vid, v, arm, mesh, LEGS)
    cz.ground_fix(arm, mesh, skip)
    chk = cz.check_ground(arm, mesh, skip)
    bpy.context.scene.frame_set(1)
    cz.save(ROOT + vid + "/" + vid, anim=True)
    info = dict(nome=v["nome"], S=v["S"], tris=cz.tri_count([mesh]), dims_m=[round(d, 3) for d in mesh.dimensions], ossos=len(arm.data.bones),
                clips=[t.name for t in arm.animation_data.nla_tracks], chao_min_z=chk, notas=v["notas"], metodo="malha organica unica + textura pintada")
    old = {}
    try: old = json.load(open(ROOT + "_familia_info.json"))
    except Exception: pass
    old[vid] = info; json.dump(old, open(ROOT + "_familia_info.json", "w"), ensure_ascii=False, indent=1)
    print(vid, info["tris"], info["dims_m"], "pior_z", min(chk.values()))

if __name__ == "__main__":
    w = sys.argv[-1]
    for vid in (V if w == "all" else [w]):
        run(vid)

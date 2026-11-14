"""Familia de caranguejos. Uso: python3 build_crab_family.py <variante|all>
Rig comum: root, body, eye_L/R, claw_{L,R}_{upper,fore,hand,finger}, leg_{L,R}{1-4}_{a,b}
+ ossos extras por variante (spike_N, barrel). Frente -Y (Blender) => +Z glTF. Origem nos pes."""
import sys, math, json
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, bpy
from cz import Vector, Matrix

ROOT = "/home/claude/cinzas/assets/inimigos/caranguejos/"
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
HEXA = [(math.cos(a), math.sin(a)) for a in [i * math.tau / 6 for i in range(6)]]

VAR = {
 "caranguejinho": dict(nome="Caranguejinho", S=0.55, deco="spots", style="normal", kR=1.15, kL=1.0, eye=1.7, leg=0.85, stance=1.4, dome=1.55, leg_len=1.4, brow="wide", idle="feisty", hand=1.15,
    pal=dict(shell="#d0702f", shell2="#7a3518", plate="#f0d9a8", belly="#e8c48c", accent=("#ffd27a", "#ffb02e", 1.2), pupil=("#1a1210", None, 0)),
    tw=8, ts=5, tr=14, notas="pequeno; pincada rapida, sem areia"),
 "caranguejo_de_cal": dict(nome="Caranguejo de Cal", S=1.0, deco="cal", style="normal", kR=1.6, kL=0.9, eye=1.0, leg=1.2, stance=1.05, dome=1.35, leg_len=1.05, brow="angry", idle="normal", hand=1.1,
    pal=dict(shell="#8f3a1f", shell2="#4f2014", plate="#e2d8c3", belly="#d0aa74", accent=("#7fe8d8", "#2fe0c8", 1.6), pupil=("#ff8a10", "#ff7a00", 2.2)),
    tw=18, ts=8, tr=30, notas="medio; pincada + arremesso de areia"),
 "caranguejo_pinca_de_ferro": dict(nome="Caranguejo Pinça de Ferro", S=1.15, deco="iron", style="iron", kR=1.95, kL=1.55, eye=0.9, leg=1.6, stance=0.82, dome=1.3, leg_len=0.82, brow="heavy", idle="heavy", hand=1.15,
    pal=dict(shell="#5a1e22", shell2="#2c1214", plate="#5c5f66", belly="#a08060", accent=("#ffb347", "#ff5a10", 5.0), pupil=("#ff4a10", "#ff3000", 3.0)),
    tw=26, ts=8, tr=42, notas="golpe duplo lento e ultra forte; pincas de ferro em brasa"),
 "caranguejo_de_pedra": dict(nome="Caranguejo de Pedra", S=1.25, deco="stone", style="normal", kR=1.3, kL=1.15, eye=0.95, leg=1.75, stance=0.85, dome=2.4, leg_len=0.78, brow="sleepy", idle="still", hand=1.2,
    pal=dict(shell="#77706a", shell2="#3e3a38", plate="#9b948a", belly="#c9b48f", accent=("#c8ff7a", "#8cf03a", 2.0), pupil=("#ffd23a", "#ffb000", 2.5)),
    tw=18, ts=8, tr=30, notas="fecha-se invulneravel, dispara espinhos, fica exposto ao levantar"),
 "caranguejo_escavador": dict(nome="Caranguejo Escavador", S=1.45, deco="ridges", style="shovel", kR=1.5, kL=1.5, eye=1.0, leg=1.45, stance=1.45, dome=1.15, leg_len=1.5, brow="angry", idle="normal", hand=1.0,
    pal=dict(shell="#5b2433", shell2="#2a1018", plate="#d9cdb4", belly="#b9926a", accent=("#ff6a5a", "#ff2a2a", 3.0), pupil=("#ff3a3a", "#ff1010", 3.0)),
    tw=14, ts=8, tr=34, notas="forte; entra no chao, viaja oculto e emerge embaixo do alvo"),
 "caranguejo_ermitao_barril": dict(nome="Ermitão do Barril", S=1.05, deco="barrel", style="normal", kR=1.55, kL=1.05, eye=1.35, leg=1.5, stance=0.85, dome=1.2, leg_len=0.8, brow="sleepy", idle="normal", hand=1.1,
    pal=dict(shell="#a8452a", shell2="#5e2416", plate="#e8c08a", belly="#d9a56e", accent=("#ffd27a", "#ffb02e", 1.0), pupil=("#1a1210", None, 0)),
    tw=16, ts=8, tr=28, notas="esconde-se no barril (defesa frontal) e faz investida com o barril"),
}

def build(vid, v):
    cz.reset(); cz.clear_mats()
    S, pal = v["S"], v["pal"]
    M = dict(shell=cz.mat(f"{vid}_casco", pal["shell"], rough=0.55),
             shell2=cz.mat(f"{vid}_casco_escuro", pal["shell2"], rough=0.6),
             plate=cz.mat(f"{vid}_placa", pal["plate"], rough=0.85, metal=(0.7 if v["deco"] == "iron" else 0)),
             belly=cz.mat(f"{vid}_ventre", pal["belly"], rough=0.8),
             joint=cz.mat(f"{vid}_junta", "#2a1719", rough=0.7),
             tip=cz.mat(f"{vid}_ponta", "#1c1311", rough=0.4),
             eye=cz.mat(f"{vid}_olho", "#efe4c6", rough=0.35),
             pupil=cz.mat(f"{vid}_pupila" + ("_emit" if pal["pupil"][1] else ""), pal["pupil"][0], rough=0.3, emit=pal["pupil"][1], strength=pal["pupil"][2]),
             accent=cz.mat(f"{vid}_destaque_emit", pal["accent"][0], rough=0.2, emit=pal["accent"][1], strength=pal["accent"][2]),
             iron=cz.mat(f"{vid}_ferro", "#77706b", rough=0.55, metal=0.75),
             wood=cz.mat(f"{vid}_madeira", "#6b4527", rough=0.85), wood2=cz.mat(f"{vid}_madeira_escura", "#3e2717", rough=0.85))
    P, bones = [], []
    def add(o, b): P.append((o, b)); return o
    BZ = 0.32 * v["stance"]
    deco = v["deco"]
    small_body = deco == "barrel"

    # ------------------------------------------------ corpo
    sc = (0.34, 0.24, 0.15 * v["dome"]) if small_body else (0.47, 0.36, 0.17 * v["dome"])
    cy = -0.06 if small_body else 0.02
    c = cz.ball("casco", 1, 12, 6, loc=(0, cy, BZ), scale=sc, m=M["shell"]); cz.apply_tf(c)
    for vv in c.data.vertices:
        if vv.co.y < cy - 0.17: vv.co.y = cy - 0.17 + (vv.co.y - cy + 0.17) * 0.55
        if vv.co.z < BZ - 0.06: vv.co.z = BZ - 0.06
    cz.jitter(c, 0.008, 3); add(c, "body")
    add(cz.ball("aro", 1, 12, 4, loc=(0, cy, BZ - 0.04), scale=(sc[0] * 1.03, sc[1] * 1.03, 0.05), m=M["shell2"]), "body")
    add(cz.ball("ventre", 1, 10, 4, loc=(0, cy + 0.01, BZ - 0.08), scale=(sc[0] * 0.8, sc[1] * 0.78, 0.11), m=M["belly"]), "body")
    fx = sc[0] / 0.47
    for i, x in enumerate((-0.36, -0.25, 0.25, 0.36)):
        add(cz.seg(f"esp{i}", (x * fx, cy - 0.19, BZ), (x * fx * 1.18, cy - 0.29, BZ + 0.03), 0.035, 0.0, 4, m=M["shell2"]), "body")
    if not small_body:
        for s_ in (-1, 1):
            for i, (y, x) in enumerate(((-0.13, 0.40), (-0.06, 0.45), (0.02, 0.47), (0.10, 0.45))):
                p0 = Vector((s_ * (x - 0.05), y, BZ + 0.01))
                add(cz.seg(f"serr{s_}{i}", p0, p0 + Vector((s_ * 0.10, -0.04, 0.02)), 0.032, 0.0, 4, m=M["shell2"]), "body")
    add(cz.box("boca", (0.14, 0.05, 0.08), loc=(0, cy - 0.24, BZ - 0.03), bevel=0.01, m=M["shell2"]), "body")

    def top_z(x, y):   # superficie real do elipsoide da carapaca
        u = (x / sc[0]) ** 2 + ((y - cy) / sc[1]) ** 2
        return BZ + sc[2] * math.sqrt(max(0.0, 1 - u)) - 0.025
    extra_bones = []
    if deco == "cal":
        for i, (x, y, s, rz) in enumerate([(0, 0.02, 0.13, 0.2), (-0.18, -0.06, 0.09, 0.6), (0.17, -0.05, 0.10, 1.1),
                                           (-0.14, 0.15, 0.08, 0.3), (0.15, 0.16, 0.085, 0.9), (0.0, 0.2, 0.07, 0.1)]):
            pl = cz.prism(f"placa{i}", [(px * s, py * s) for px, py in HEXA], 0.045, loc=(x, y, top_z(x, y)), rot=(x * 0.6, -y * 0.5, rz), m=M["plate"], top_scale=0.78)
            cz.jitter(pl, 0.006, 10 + i); add(pl, "body")
        for i, (x, y, h, t) in enumerate([(-0.06, 0.13, 0.15, (0.35, -0.25)), (0.05, 0.16, 0.12, (0.4, 0.3)), (0.0, 0.25, 0.10, (0.7, 0.0)),
                                          (-0.2, 0.12, 0.08, (0.3, -0.6)), (0.21, 0.1, 0.09, (0.2, 0.6))]):
            add(cz.shard(f"sal{i}", 0.035 + h * 0.15, h, 5, loc=(x, y, BZ + 0.1), rot=(t[0], t[1], i), m=M["accent"], seed=i), "body")
    elif deco == "spots":
        for i, (x, y, s) in enumerate([(0, 0.0, 0.08), (-0.17, -0.04, 0.05), (0.16, -0.05, 0.055), (-0.1, 0.17, 0.045), (0.12, 0.15, 0.05)]):
            add(cz.prism(f"pinta{i}", [(px * s, py * s) for px, py in HEXA], 0.02, loc=(x, y, top_z(x, y) + 0.01), rot=(x * 0.6, -y * 0.5, i), m=M["plate"], top_scale=0.8), "body")
    elif deco == "iron":
        for i, (x, y, s) in enumerate([(0, 0.03, 0.15), (-0.2, -0.04, 0.1), (0.2, -0.04, 0.1), (-0.13, 0.17, 0.09), (0.13, 0.17, 0.09)]):
            add(cz.prism(f"placaF{i}", [(px * s, py * s) for px, py in HEXA], 0.04, loc=(x, y, top_z(x, y)), rot=(x * 0.6, -y * 0.5, 0.5), m=M["plate"], top_scale=0.85), "body")
            for j in range(3):
                a = j * math.tau / 3 + 0.3
                add(cz.ico(f"rebite{i}{j}", 0.014, 1, loc=(x + math.cos(a) * s * 0.65, y + math.sin(a) * s * 0.65, top_z(x, y) + 0.045), m=M["iron"]), "body")
        add(cz.box("fenda", (0.04, 0.34, 0.03), loc=(0, 0.04, BZ + 0.155), m=M["accent"]), "body")   # rachadura incandescente
    elif deco == "ridges":
        for x in (-0.16, 0, 0.16):
            add(cz.profile(f"crista{x}", [(-0.03, 0), (0.0, 0.05), (0.03, 0)], 0.38, m=M["plate"], loc=(x, 0.03, top_z(x, 0.1) + 0.0), rot=(0, 0, 0)), "body")
            for j, y in enumerate((-0.1, 0.05, 0.2)):
                add(cz.seg(f"espT{x}{j}", (x, y, top_z(x, y) + 0.03), (x * 1.1, y + 0.1, top_z(x, y) + 0.12), 0.025, 0.0, 4, m=M["shell2"]), "body")
    elif deco == "stone":
        for i, (x, y, r) in enumerate([(0, 0.02, 0.12), (-0.19, -0.03, 0.08), (0.19, -0.02, 0.085), (-0.12, 0.17, 0.08), (0.12, 0.18, 0.075), (0.0, -0.14, 0.07)]):
            rk = cz.ico(f"rocha{i}", r, 1, loc=(x, y, top_z(x, y)), scale=(1.1, 1, 0.6), m=M["plate"] if i % 2 else M["shell2"]); cz.jitter(rk, 0.012, i); add(rk, "body")
        for i, (x, y) in enumerate([(-0.24, 0.08), (-0.08, 0.20), (0.08, 0.20), (0.24, 0.08), (-0.16, -0.10), (0.16, -0.10)]):
            base = Vector((x, y, top_z(x, y) + 0.04))
            d = Vector((x * 1.4, y * 1.2, 0.5)).normalized()
            bn = f"spike_{i + 1}"
            extra_bones.append((bn, base, base + d * 0.16, "body"))
            sp = cz.shard(f"espinho{i}", 0.04, 0.2, 5, m=M["shell2"], seed=i)
            sp.rotation_mode = 'QUATERNION'; sp.rotation_quaternion = d.to_track_quat('Z', 'Y'); sp.location = base - d * 0.04
            add(sp, bn)
            add(cz.shard(f"espinhoP{i}", 0.018, 0.08, 4, loc=base + d * 0.1, m=M["accent"], seed=i + 7), bn)   # ponta viva (brilho)
    elif deco == "barrel":
        bc = Vector((0, 0.22, BZ + 0.14)); R, L = 0.36, 0.62
        extra_bones.append(("barrel", bc, bc + Vector((0, 0.3, 0)), "root"))
        brl = cz.cyl("barril", R, L, 14, loc=bc, rot=(math.pi / 2, 0, 0), m=M["wood"]); cz.apply_tf(brl)
        for vv in brl.data.vertices:          # barriga do barril
            t = abs(vv.co.y - bc.y) / (L / 2)
            k = 1 + 0.12 * (1 - t * t)
            vv.co.x *= k; vv.co.z = bc.z + (vv.co.z - bc.z) * k
        cz.jitter(brl, 0.008, 5); add(brl, "barrel")
        add(cz.cyl("boca", R * 0.92, 0.02, 14, loc=bc + Vector((0, -L / 2 - 0.005, 0)), rot=(math.pi / 2, 0, 0), m=M["wood2"]), "barrel")
        for i in range(7):                    # aduelas escuras
            a = i * math.tau / 7
            add(cz.box(f"aduela{i}", (0.04, L * 0.98, 0.02), loc=bc + Vector((math.cos(a) * R * 1.08, 0, math.sin(a) * R * 1.08)), rot=(0, -a + math.pi / 2, 0), m=M["wood2"]), "barrel")
        for j, y in enumerate((-0.26, 0.0, 0.26)):
            add(cz.cyl(f"aro{j}", R * (1.15 if j == 1 else 1.1), 0.05, 14, loc=bc + Vector((0, y, 0)), rot=(math.pi / 2, 0, 0), m=M["iron"], cap=False), "barrel")
        for i in range(9):                    # corrente diagonal
            t = i / 8
            a = math.radians(40 + t * 110)
            p = bc + Vector((math.cos(a) * R * 1.2, -0.28 + t * 0.56, math.sin(a) * R * 1.2))
            add(cz.cyl(f"elo{i}", 0.04, 0.018, 6, loc=p, rot=((0.0 if i % 2 else math.pi / 2), a, 0.4), m=M["iron"], cap=False), "barrel")
        add(cz.box("tampa", (0.2, 0.12, 0.04), loc=bc + Vector((0.0, 0.12, R * 1.12)), rot=(0, 0.1, 0.2), m=M["wood2"]), "barrel")

    # ------------------------------------------------ olhos
    bones += [("root", (0, 0, 0), (0, 0, 0.15), None), ("body", (0, cy, BZ), (0, cy - 0.2, BZ), "root")]
    ek = v["eye"]
    for s, nm in ((1, "L"), (-1, "R")):
        h, t = Vector((s * 0.09, cy - 0.22, BZ + 0.08)), Vector((s * 0.12, cy - 0.28, BZ + 0.08 + 0.16 * min(ek, 1.2)))
        bones.append((f"eye_{nm}", h, t, "body"))
        add(cz.seg(f"haste{nm}", h, t, 0.022 * ek, 0.018 * ek, 5, m=M["shell2"]), f"eye_{nm}")
        add(cz.ball(f"olho{nm}", 0.045 * ek, 7, 5, loc=t + Vector((0, -0.005, 0.02)), m=M["eye"]), f"eye_{nm}")
        add(cz.ball(f"pupila{nm}", 0.022 * ek, 6, 4, loc=t + Vector((0, -0.04 * ek, 0.025)), scale=(1, 0.6, 1.3), m=M["pupil"]), f"eye_{nm}")
        ec = t + Vector((0, -0.005, 0.02))
        br = v["brow"]
        def lid(name, r, cut, ty, tx, thick=1.0):
            o = cz.ball(name, r, 10, 8, loc=ec, m=M["shell2"]); cz.apply_tf(o)
            for vv in o.data.vertices:
                if vv.co.z < ec.z + cut: vv.co.z = ec.z + cut
                else: vv.co.z = ec.z + cut + (vv.co.z - ec.z - cut) * thick
            R = Matrix.Translation(ec) @ (Matrix.Rotation(ty, 4, 'Y') @ Matrix.Rotation(tx, 4, 'X')) @ Matrix.Translation(-ec)
            o.data.transform(R)
            return o
        r_e = 0.045 * ek * 1.14
        if br in ("angry", "heavy"):    # canto interno mais baixo = cara brava
            add(lid(f"palp{nm}", r_e, 0.008 * ek, s * 0.55, 0.25, 1.3 if br == "heavy" else 1.0), f"eye_{nm}")
            if br == "heavy":
                add(cz.seg(f"cicatriz{nm}", ec + Vector((-s * 0.035, -0.045, 0.045)) * 1.0, ec + Vector((s * 0.02, -0.045, -0.03)), 0.007, 0.007, 6, m=M["plate"]), f"eye_{nm}")
        elif br == "sleepy":            # cobre a metade de cima, canto externo caido
            add(lid(f"palp{nm}", r_e, -0.004 * ek, -s * 0.22, 0.1), f"eye_{nm}")

    # ------------------------------------------------ pincas
    style = v["style"]
    def claw(nm, s, k):
        y0 = cy - 0.19
        sh = Vector((s * 0.30 * fx, y0, BZ))
        el = Vector((s * (0.30 * fx + 0.15 * k), y0 - 0.06 * k, BZ + 0.03))
        wr = Vector((s * (0.30 * fx + 0.09 * k), y0 - 0.21 * k, BZ + 0.09))
        hand_c = wr + Vector((-s * 0.035 * k, -0.08 * k, 0))
        tip = wr + Vector((-s * 0.10 * k, -0.24 * k, -0.03 * k))
        hinge = wr + Vector((-s * 0.04 * k, -0.13 * k, 0.05 * k))
        bones.extend([(f"claw_{nm}_upper", sh, el, "body"), (f"claw_{nm}_fore", el, wr, f"claw_{nm}_upper", True),
                      (f"claw_{nm}_hand", wr, wr + Vector((0, -0.2 * k, 0)), f"claw_{nm}_fore", True),
                      (f"claw_{nm}_finger", hinge, tip + Vector((0, 0, 0.06 * k)), f"claw_{nm}_hand")])
        hm = M["iron"] if style == "iron" else M["shell"]
        fm = M["iron"] if style == "iron" else M["shell2"]
        tm = M["accent"] if style == "iron" else M["tip"]
        add(cz.seg(f"brA{nm}", sh, el, 0.05 * k, 0.045 * k, 6, m=M["shell"]), f"claw_{nm}_upper")
        add(cz.ball(f"jA{nm}", 0.05 * k, 6, 4, loc=el, m=M["joint"]), f"claw_{nm}_upper")
        add(cz.seg(f"brB{nm}", el, wr, 0.05 * k, 0.06 * k, 6, m=M["shell"]), f"claw_{nm}_fore")
        add(cz.ball(f"jB{nm}", 0.055 * k, 6, 4, loc=wr, m=M["joint"]), f"claw_{nm}_fore")
        hs = (0.16 * k, 0.13 * k, 0.065 * k) if style == "shovel" else ((0.10 * k, 0.17 * k, 0.095 * k) if style == "iron" else (0.11 * k, 0.13 * k, 0.10 * k))
        hs = tuple(h * v["hand"] for h in hs)
        hand = cz.ball(f"mao{nm}", 1, 8, 5, loc=hand_c, scale=hs, m=hm); cz.jitter(hand, 0.004 * k, 7); add(hand, f"claw_{nm}_hand")
        if style == "iron":
            for j, yy in enumerate((0.05, -0.04)):
                add(cz.cyl(f"cinta{nm}{j}", 1, 0.035 * k, 8, loc=hand_c + Vector((0, yy * k, 0)), rot=(math.pi / 2, 0, 0), m=M["shell2"], cap=False), f"claw_{nm}_hand")
                P[-1][0].scale = (hs[0] * 1.06, hs[2] * 1.06, 1)
            add(cz.prism(f"blind{nm}", [(-hs[0] * 0.8, -hs[1] * 0.9), (hs[0] * 0.8, -hs[1] * 0.9), (hs[0] * 0.9, hs[1] * 0.8), (-hs[0] * 0.9, hs[1] * 0.8)], 0.03 * k,
                         loc=hand_c + Vector((0, 0, hs[2] * 0.8)), m=M["shell2"], top_scale=0.85), f"claw_{nm}_hand")
            for j in range(3):   # cravos no dorso da pinca
                pj = hand_c + Vector((0, (j - 1) * hs[1] * 0.55, hs[2] * 1.05))
                add(cz.seg(f"cravo{nm}{j}", pj, pj + Vector((s * 0.015, 0.0, 0.06 * k)), 0.022 * k, 0.0, 6, m=M["iron"]), f"claw_{nm}_hand")
        else:
            add(cz.prism(f"placaMao{nm}", [(px * 0.07 * k, py * 0.09 * k) for px, py in HEXA], 0.03 * k, loc=hand_c + Vector((s * 0.02 * k, 0, hs[2] * 0.75)),
                         rot=(0, s * 0.3, 0.3), m=M["plate"], top_scale=0.7), f"claw_{nm}_hand")
        fr = 0.065 if style == "shovel" else 0.05
        fb = hand_c + Vector((-s * 0.02 * k, -0.09 * k, -0.035 * k))
        add(cz.seg(f"dedoF{nm}", fb, tip, fr * k, 0.014 * k, 4 if style == "shovel" else 5, m=fm), f"claw_{nm}_hand")
        add(cz.seg(f"pontaF{nm}", tip, tip + Vector((0, -0.035 * k, 0.03 * k)), 0.016 * k, 0.0, 4, m=tm), f"claw_{nm}_hand")
        ftip = tip + Vector((0, 0.0, 0.06 * k))
        add(cz.seg(f"dedoM{nm}", hinge, ftip, (fr - 0.005) * k, 0.014 * k, 4 if style == "shovel" else 5, m=fm), f"claw_{nm}_finger")
        add(cz.seg(f"pontaM{nm}", ftip, ftip + Vector((0, -0.035 * k, -0.035 * k)), 0.016 * k, 0.0, 4, m=tm), f"claw_{nm}_finger")
        for i in range(3):
            t0 = fb.lerp(tip, 0.25 + i * 0.22)
            add(cz.seg(f"dente{nm}{i}", t0, t0 + Vector((0, 0, 0.03 * k)), 0.012 * k, 0.0, 4, m=(M["accent"] if style == "iron" else M["plate"])), f"claw_{nm}_hand")
    claw("R", -1, v["kR"]); claw("L", 1, v["kL"])

    # ------------------------------------------------ patas
    LEGS = {}
    lk = v["leg"]
    dirs = [(1, -0.30), (1, 0.0), (1, 0.32), (0.85, 0.65)]
    ys = [-0.07, 0.03, 0.12, 0.20] if not small_body else [-0.10, -0.02, 0.06, 0.13]
    for s, nm in ((1, "L"), (-1, "R")):
        for i in range(4):
            d = Vector((s * dirs[i][0], dirs[i][1], 0)).normalized()
            a = Vector((s * 0.37 * fx, ys[i], BZ - 0.02))
            LL = v["leg_len"]
            kn = a + d * 0.21 * LL + Vector((0, 0, 0.10 * LL)); ft = a + d * 0.38 * LL; ft.z = 0.0
            b1, b2 = f"leg_{nm}{i + 1}_a", f"leg_{nm}{i + 1}_b"
            bones.extend([(b1, a, kn, "body"), (b2, kn, ft, b1, True)])
            LEGS[(nm, i)] = d
            add(cz.seg(f"coxa{nm}{i}", a, kn, 0.048 * lk, 0.040 * lk, 5, m=M["shell"]), b1)
            add(cz.ball(f"joelho{nm}{i}", 0.042 * lk, 6, 4, loc=kn, m=M["joint"]), b1)
            add(cz.ball(f"musculo{nm}{i}", 0.058 * lk, 8, 5, loc=a.lerp(kn, 0.4), scale=(1, 1, 0.85), m=M["shell"]), b1)
            mid = kn.lerp(ft, 0.55)
            add(cz.seg(f"canela{nm}{i}", kn, mid, 0.040 * lk, 0.032 * lk, 5, m=M["shell"]), b2)
            add(cz.seg(f"pe{nm}{i}", mid, ft, 0.032 * lk, 0.006, 5, m=M["tip"]), b2)
            add(cz.seg(f"faixa{nm}{i}", a.lerp(kn, 0.45), a.lerp(kn, 0.6), 0.053 * lk, 0.05 * lk, 5, m=M["plate"]), b1)
    bones += extra_bones

    # ------------------------------------------------ escala S e montagem
    SM = Matrix.Scale(S, 4)
    for o, _ in P:
        cz.apply_tf(o); o.data.transform(SM)
    bones = [(b[0], Vector(b[1]) * S, Vector(b[2]) * S, *b[3:]) for b in bones]
    arm = cz.armature(vid, bones)
    mesh = cz.skin_rigid(vid, P, arm)
    return arm, mesh, LEGS, M

# ======================================================== animacao
def anims(vid, v, arm, mesh, LEGS):
    S = v["S"]
    def C(name, n, ground=True): return cz.Clip(arm, name, n, mesh=mesh, S=S, ground=ground)
    def lift_axis(k): return tuple(LEGS[k].cross(Vector(Z)))
    def legs(lift=None, swing=None, fold=0.0):
        p = {}
        for k in LEGS:
            nm, i = k; r = []
            if lift is not None: r.append((lift_axis(k), lift.get(k, 0) if isinstance(lift, dict) else lift))
            if swing: r.append((Z, swing.get(k, 0)))
            p[f"leg_{nm}{i + 1}_a"] = {'r': r}
            if fold: p[f"leg_{nm}{i + 1}_b"] = {'r': [(lift_axis(k), fold)]}
        return p
    def merge(*ds):
        out = {}
        for d in ds:
            for k, val in d.items():
                cur = out.setdefault(k, {})
                cur['r'] = cur.get('r', []) + val.get('r', [])
                if 'l' in val: cur['l'] = val['l']
        return out
    front = lambda deg_front, deg_back: {k: (deg_front if k[1] < 2 else deg_back) for k in LEGS}

    # IDLE com personalidade (so sobe; trava de chao corrige o resto)
    mood = v["idle"]
    if mood == "feisty":     # agitado: pincas erguidas, quica e vira os olhos
        cl = C("idle", 24)
        for f, t in ((1, 0), (7, 1), (13, 0), (19, 1), (24, 0)):
            cl.key(f, merge(legs({k: -3 * t for k in LEGS}),
                            {"root": {'l': (0, 0, 0.025 * t)}, "body": {'r': [(Y, 4 * (1 if f < 12 else -1) * t)]},
                             "claw_R_upper": {'r': [(X, -30 - 8 * t)]}, "claw_L_upper": {'r': [(X, -30 + 6 * t)]},
                             "claw_R_finger": {'r': [(X, -25 * t)]}, "claw_L_finger": {'r': [(X, -25 * (1 - t))]},
                             "eye_L": {'r': [(Y, 18 * t)]}, "eye_R": {'r': [(Y, -18 * t)]}}))
    elif mood == "heavy":    # bruto: respira devagar, pincas pesadas quase no chao
        cl = C("idle", 72)
        for f, t in ((1, 0), (24, 1), (48, 0.3), (72, 0)):
            cl.key(f, merge(legs({k: 4 * t for k in LEGS}),
                            {"root": {'l': (0, 0, 0.02 * t)}, "body": {'r': [(X, -4 * t)]},
                             "claw_R_upper": {'r': [(X, 14 - 6 * t)]}, "claw_L_upper": {'r': [(X, 12 - 5 * t)]},
                             "claw_R_finger": {'r': [(X, -10 * t)]}, "eye_L": {'r': [(X, 6 * t)]}, "eye_R": {'r': [(X, 6 * t)]}}))
    elif mood == "still":    # pedra: quase imovel, so os olhos sonolentos e um suspiro
        cl = C("idle", 64)
        for f, t in ((1, 0), (30, 1), (40, 1), (64, 0)):
            cl.key(f, merge(legs({k: 0 for k in LEGS}),
                            {"root": {'l': (0, 0, 0.008 * t)}, "eye_L": {'r': [(X, 10 * t), (Y, 6 * t)]}, "eye_R": {'r': [(X, 10 * t)]},
                             "claw_R_finger": {'r': [(X, -6 * t)]}}))
    else:
        cl = C("idle", 48)
        for f, t in ((1, 0), (13, 1), (25, 0), (37, -1), (48, 0)):
            cl.key(f, merge(legs({k: -2 * abs(t) for k in LEGS}),
                            {"root": {'l': (0, 0, 0.012 * abs(t))}, "body": {'r': [(Y, 1.5 * t)]},
                             "claw_R_upper": {'r': [(X, -4 * t)]}, "claw_L_upper": {'r': [(X, 3 * t)]},
                             "claw_R_finger": {'r': [(X, -8 if t > 0 else 0)]},
                             "eye_L": {'r': [(Y, 8 * t)]}, "eye_R": {'r': [(Y, -6 * t), (X, 4 * t)]}}))
    cl.done()
    # WALK
    A = {("L", 0), ("R", 1), ("L", 2), ("R", 3)}
    cl = C("walk", 24)
    for f, ph in ((1, 0), (7, 1), (13, 2), (19, 3), (24, 0)):
        lift, sw = {}, {}
        for k in LEGS:
            a = ph if k in A else (ph + 2) % 4
            lift[k] = [0, 22, 0, 0][a]; sw[k] = [12, 0, -12, 0][a] * (1 if k[0] == "L" else -1)
        cl.key(f, merge(legs(lift, sw), {"root": {'l': (0, 0, [0, 0.02, 0, 0.02][ph])}, "body": {'r': [(Y, [3, 0, -3, 0][ph])]},
                                         "claw_R_upper": {'r': [(Z, [4, 0, -4, 0][ph])]}, "claw_L_upper": {'r': [(Z, [-5, 0, 5, 0][ph])]}}))
    cl.done()

    heavy = vid == "caranguejo_pinca_de_ferro"
    both = ["R", "L"] if heavy else ["R"]
    WIND = merge(legs(front(-8, 6)), {"root": {'l': (0, 0.05, 0.05)}, "body": {'r': [(X, -14 if not heavy else -20)]},
                                     "eye_L": {'r': [(X, 15)]}, "eye_R": {'r': [(X, 15)]}})
    STRIKE = merge(legs(front(8, -6)), {"root": {'l': (0, -0.10, -0.02)}, "body": {'r': [(X, 9 if not heavy else 14)]}})
    STUCK = merge(legs(front(4, -2)), {"root": {'l': (0, -0.08, -0.04)}, "body": {'r': [(X, 7), (Y, 5)]},
                                       "eye_L": {'r': [(Y, 12)]}, "eye_R": {'r': [(Y, 12)]}})
    for c in both:
        sgn = 1 if c == "R" else -1
        WIND = merge(WIND, {f"claw_{c}_upper": {'r': [(X, -38 if heavy else -32), (Y, -10 * sgn)]}, f"claw_{c}_fore": {'r': [(X, -18)]},
                            f"claw_{c}_hand": {'r': [(X, -15)]}, f"claw_{c}_finger": {'r': [(X, -38)]}})
        STRIKE = merge(STRIKE, {f"claw_{c}_upper": {'r': [(X, 22), (Y, 4 * sgn)]}, f"claw_{c}_fore": {'r': [(X, 12)]},
                                f"claw_{c}_hand": {'r': [(X, 18)]}, f"claw_{c}_finger": {'r': [(X, 4)]}})
        STUCK = merge(STUCK, {f"claw_{c}_upper": {'r': [(X, 26)]}, f"claw_{c}_fore": {'r': [(X, 14)]}, f"claw_{c}_hand": {'r': [(X, 22)]}})
    if not heavy:
        WIND = merge(WIND, {"claw_L_upper": {'r': [(X, -15)]}}); STRIKE = merge(STRIKE, {"claw_L_upper": {'r': [(X, 10)]}})
    tw, ts, tr = v["tw"], v["ts"], v["tr"]
    cl = C("attack_windup", tw); cl.key(1, {}); cl.key(max(2, tw * 2 // 3), WIND); cl.key(tw, merge(WIND, {"claw_R_upper": {'r': [(X, -5)]}})); cl.done()
    cl = C("attack_strike", ts); cl.key(1, WIND); cl.key(max(2, ts * 2 // 3), STRIKE); cl.key(ts, STUCK); cl.done()
    cl = C("attack_recover", tr); cl.key(1, STUCK); cl.key(max(2, tr * 3 // 5), STUCK); cl.key(tr, {}); cl.done()
    tot = tw + ts + tr
    cl = C("attack_full", tot); cl.key(1, {}); cl.key(tw, WIND); cl.key(tw + ts * 2 // 3, STRIKE); cl.key(tw + ts, STUCK)
    cl.key(tw + ts + tr * 3 // 5, STUCK); cl.key(tot, {}); cl.done()

    HIT = merge(legs({k: 6 for k in LEGS}), {"root": {'l': (0, 0.06, 0.02)}, "body": {'r': [(X, -8), (Z, 7)]},
                                             "claw_R_upper": {'r': [(X, -15)]}, "claw_L_upper": {'r': [(X, -18)]},
                                             "eye_L": {'r': [(X, 40)]}, "eye_R": {'r': [(X, 40)]}})
    cl = C("hit", 14); cl.key(1, {}); cl.key(4, HIT); cl.key(14, {}); cl.done()
    D2 = merge(legs({k: 35 for k in LEGS}, fold=-60),
               {"root": {'l': (0, 0, -0.20)}, "body": {'r': [(Y, 12), (X, 4)]},
                "claw_R_upper": {'r': [(X, 30), (Z, 10)]}, "claw_R_finger": {'r': [(X, -20)]},
                "claw_L_upper": {'r': [(X, 35), (Z, -15)]}, "eye_L": {'r': [(X, 75)]}, "eye_R": {'r': [(X, 70)]}})
    cl = C("death", 40); cl.key(1, {}); cl.key(6, merge(HIT, {"root": {'l': (0, 0.04, 0.05)}})); cl.key(22, D2); cl.key(40, D2); cl.done()

    skip = []
    # ------------------------------------------------ especificos
    if vid == "caranguejo_de_cal":   # arremesso de areia com a pinca pequena
        SCOOP = merge(legs(front(10, -4)), {"body": {'r': [(X, 10), (Z, -12)]}, "claw_L_upper": {'r': [(X, 28), (Z, -10)]},
                                           "claw_L_fore": {'r': [(X, 20)]}, "claw_L_finger": {'r': [(X, -30)]}, "claw_R_upper": {'r': [(X, -10)]}})
        FLING = merge(legs(front(-6, 4)), {"root": {'l': (0, 0.03, 0.02)}, "body": {'r': [(X, -10), (Z, 8)]},
                                          "claw_L_upper": {'r': [(X, -55), (Z, 10)]}, "claw_L_fore": {'r': [(X, -25)]}, "claw_L_finger": {'r': [(X, -35)]}})
        cl = C("sand_windup", 16); cl.key(1, {}); cl.key(10, SCOOP); cl.key(16, merge(SCOOP, {"claw_L_fore": {'r': [(X, 24)]}})); cl.done()
        cl = C("sand_throw", 14); cl.key(1, SCOOP); cl.key(5, FLING); cl.key(14, {}); cl.done()

    if vid == "caranguejo_pinca_de_ferro":   # arrasta as pincas (giro lateral)
        SW = merge(legs(front(6, 6)), {"body": {'r': [(Z, 35)]}, "claw_R_upper": {'r': [(Z, -40), (X, 15)]}, "claw_L_upper": {'r': [(Z, -20), (X, 15)]}})
        cl = C("sweep", 30); cl.key(1, {}); cl.key(10, merge(SW, {"body": {'r': [(Z, -25)]}})); cl.key(18, SW); cl.key(30, {}); cl.done()

    if vid == "caranguejo_de_pedra":
        HUNK = merge(legs({k: 40 for k in LEGS}, fold=-35),
                     {"root": {'l': (0, 0, -0.30)}, "body": {'r': [(X, 6)]},
                      "claw_R_upper": {'r': [(Z, 35), (X, 10)]}, "claw_R_fore": {'r': [(Z, 20)]},
                      "claw_L_upper": {'r': [(Z, -35), (X, 10)]}, "claw_L_fore": {'r': [(Z, -20)]},
                      "eye_L": {'r': [(X, 80)]}, "eye_R": {'r': [(X, 80)]}})
        spikes_out = {f"spike_{i}": {'l': tuple(Vector(arm.data.bones[f"spike_{i}"].vector).normalized() * 0.07 / S)} for i in range(1, 7)}
        cl = C("hunker_down", 16); cl.key(1, {}); cl.key(12, HUNK); cl.key(16, HUNK); cl.done()
        cl = C("hunker_loop", 32); cl.key(1, HUNK); cl.key(16, merge(HUNK, {"body": {'r': [(Y, 1.5)]}})); cl.key(32, HUNK); cl.done()
        cl = C("spike_volley", 24)   # espinhos estufam e disparam (projeteis saem dos ossos spike_N)
        cl.key(1, HUNK); cl.key(10, merge(HUNK, {"body": {'r': [(X, -3)]}}, {f"spike_{i}": {'l': tuple(-Vector(spikes_out[f'spike_{i}']['l']) * 0.4)} for i in range(1, 7)}))
        cl.key(13, merge(HUNK, spikes_out)); cl.key(24, HUNK); cl.done()
        RISE = merge(legs(front(-14, 8)), {"root": {'l': (0, 0.04, 0.10)}, "body": {'r': [(X, -20)]},
                                          "claw_R_upper": {'r': [(Z, -25), (X, -20)]}, "claw_R_finger": {'r': [(X, -30)]},
                                          "claw_L_upper": {'r': [(Z, 25), (X, -20)]}, "claw_L_finger": {'r': [(X, -30)]}})
        cl = C("rise_exposed", 40)   # janela de ataque: ventre exposto
        cl.key(1, HUNK); cl.key(10, RISE); cl.key(30, merge(RISE, {"body": {'r': [(Y, 3)]}})); cl.key(40, {}); cl.done()

    if vid == "caranguejo_escavador":
        DIG = merge(legs(front(-10, 15)), {"body": {'r': [(X, 25)]}, "claw_R_upper": {'r': [(X, 30)]}, "claw_L_upper": {'r': [(X, 30)]},
                                          "claw_R_fore": {'r': [(X, 25)]}, "claw_L_fore": {'r': [(X, 25)]}})
        UNDER = merge(legs({k: -22 for k in LEGS}, fold=18), {"root": {'l': (0, 0, -0.64)}, "eye_L": {'r': [(X, -10)]}, "eye_R": {'r': [(X, -10)]}})
        DEEP = merge(UNDER, {"root": {'l': (0, 0, -0.85)}})
        cl = C("burrow", 30, ground=False); cl.key(1, {}); cl.key(8, DIG, ground=True)
        cl.key(14, merge(DIG, {"claw_R_upper": {'r': [(X, -10)]}, "claw_L_upper": {'r': [(X, -10)]}}), ground=True)
        cl.key(22, merge(DIG, {"root": {'l': (0, 0, -0.35)}})); cl.key(30, UNDER); cl.done()
        cl = C("underground_loop", 24, ground=False)   # so os olhos aparecem; o jogo mostra o monte de terra
        cl.key(1, UNDER); cl.key(12, merge(UNDER, {"eye_L": {'r': [(Y, 20)]}, "eye_R": {'r': [(Y, -20)]}})); cl.key(24, UNDER); cl.done()
        UP = merge(legs(front(-12, 10)), {"root": {'l': (0, 0, 0.14)}, "body": {'r': [(X, -24)]},
                                         "claw_R_upper": {'r': [(X, -45), (Y, 15)]}, "claw_L_upper": {'r': [(X, -45), (Y, -15)]},
                                         "claw_R_finger": {'r': [(X, -40)]}, "claw_L_finger": {'r': [(X, -40)]}})
        SNAP = merge(STRIKE, {"claw_L_upper": {'r': [(X, 22)]}, "claw_L_hand": {'r': [(X, 18)]}})
        cl = C("emerge_attack", 26, ground=False)    # telegrafo: monte de terra treme antes (efeito do integrador)
        cl.key(1, DEEP); cl.key(8, merge(UNDER, {"root": {'l': (0, 0, -0.45)}})); cl.key(13, UP, ground=True)
        cl.key(19, SNAP, ground=True); cl.key(26, STUCK, ground=True); cl.done()
        cl = C("emerge_dazed", 36); cl.key(1, STUCK); cl.key(12, merge(STUCK, {"body": {'r': [(Z, 8)]}, "eye_L": {'r': [(Y, 25)]}, "eye_R": {'r': [(Y, -25)]}}))
        cl.key(24, merge(STUCK, {"body": {'r': [(Z, -8)]}})); cl.key(36, {}); cl.done()
        skip = ["burrow", "underground_loop", "emerge_attack"]

    if vid == "caranguejo_ermitao_barril":
        HIDE = merge(legs({k: 30 for k in LEGS}, fold=-45),
                     {"root": {'l': (0, 0, -0.12)}, "body": {'l': (0, 0.18, 0.02)},
                      "claw_R_upper": {'r': [(Z, 40)]}, "claw_L_upper": {'r': [(Z, -40)]},
                      "eye_L": {'r': [(X, 70)]}, "eye_R": {'r': [(X, 70)]}})
        cl = C("hide", 14); cl.key(1, {}); cl.key(10, HIDE); cl.key(14, HIDE); cl.done()
        cl = C("hide_loop", 32); cl.key(1, HIDE); cl.key(20, merge(HIDE, {"eye_L": {'r': [(X, 40)]}})); cl.key(32, HIDE); cl.done()
        cl = C("unhide", 16); cl.key(1, HIDE); cl.key(16, {}); cl.done()
        RAMW = merge(legs(front(-12, 14)), {"root": {'l': (0, 0.12, 0.02)}, "body": {'r': [(X, 10)]}, "barrel": {'r': [(X, 12)]}})
        RAMS = merge(legs(front(12, -10)), {"root": {'l': (0, -0.30, 0.0)}, "body": {'r': [(X, 14)]}, "barrel": {'r': [(X, 20)]},
                                          "claw_R_upper": {'r': [(Z, 30)]}, "claw_L_upper": {'r': [(Z, -30)]}})
        cl = C("ram_windup", 18); cl.key(1, {}); cl.key(14, RAMW); cl.key(18, RAMW); cl.done()
        cl = C("ram_strike", 22); cl.key(1, RAMW); cl.key(6, RAMS); cl.key(14, RAMS); cl.key(22, {}); cl.done()
    arm.animation_data.action = None
    return skip

# ======================================================== props
def props():
    cz.reset(); cz.clear_mats()
    sand = cz.mat("areia", "#d9b77a", rough=0.95); sand2 = cz.mat("areia_escura", "#a9874f", rough=0.95)
    dirt = cz.mat("terra", "#5a4636", rough=0.95); rock = cz.mat("pedra", "#77706a", rough=0.9)
    glow = cz.mat("espinho_ponta_emit", "#c8ff7a", rough=0.2, emit="#8cf03a", strength=2)
    out = []
    o = [cz.ico(f"g{i}", 0.05 + i * 0.008, 1, loc=(math.cos(i * 2.3) * 0.05, math.sin(i * 2.3) * 0.05, 0.06 + (i % 2) * 0.03), m=sand if i % 2 else sand2) for i in range(6)]
    for p in o: cz.jitter(p, 0.01, 3)
    out.append(("proj_bola_areia", cz.join("proj_bola_areia", o), "projetil do Caranguejo de Cal; origem no centro aprox."))
    sp = cz.shard("proj_espinho_pedra", 0.05, 0.32, 5, rot=(-math.pi / 2, 0, 0), m=rock, seed=3)
    tip = cz.shard("pt", 0.022, 0.1, 4, loc=(0, -0.24, 0), rot=(-math.pi / 2, 0, 0), m=glow, seed=4)
    out.append(("proj_espinho_pedra", cz.join("proj_espinho_pedra", [sp, tip]), "projetil do Caranguejo de Pedra; ponta para -Y (frente)"))
    o = []
    for i in range(10):
        a = i * math.tau / 10
        r = cz.ico(f"t{i}", 0.16, 1, loc=(math.cos(a) * 0.42, math.sin(a) * 0.42, 0.02), scale=(1.3, 1, 0.45), m=dirt); cz.jitter(r, 0.03, i); o.append(r)
    o.append(cz.ball("cupula", 1, 10, 4, loc=(0, 0, 0), scale=(0.5, 0.5, 0.16), m=dirt))
    for i in range(5):
        a = i * 1.3
        o.append(cz.ico(f"p{i}", 0.06, 1, loc=(math.cos(a) * 0.3, math.sin(a) * 0.3, 0.1), m=rock))
    out.append(("prop_monte_terra_escavador", cz.join("prop_monte_terra_escavador", o), "marca o Escavador sob o chao; o jogo desloca e treme antes da emersao"))
    info = []
    for aid, ob, n in out:
        cz.export_objs([ob], ROOT + "props/" + aid)
        info.append(dict(asset_id=aid, tris=cz.tri_count([ob]), dims_m=[round(d, 3) for d in ob.dimensions], notas=n))
    bpy.ops.wm.save_as_mainfile(filepath=ROOT + "props/props_caranguejos.blend")
    return info

if __name__ == "__main__":
    which = sys.argv[-1]
    rep = {}
    ids = list(VAR) if which == "all" else ([] if which == "props" else [which])
    for vid in ids:
        v = VAR[vid]
        arm, mesh, LEGS, M = build(vid, v)
        skip = anims(vid, v, arm, mesh, LEGS)
        cz.ground_fix(arm, mesh, skip)
        chk = cz.check_ground(arm, mesh, skip)
        bpy.context.scene.frame_set(1)
        cz.save(ROOT + vid + "/" + vid, anim=True)
        rep[vid] = dict(nome=v["nome"], S=v["S"], tris=cz.tri_count([mesh]), dims_m=[round(d, 3) for d in mesh.dimensions],
                        clips=[t.name for t in arm.animation_data.nla_tracks], chao_min_z=chk, ossos=len(arm.data.bones), notas=v["notas"])
        print(vid, json.dumps(rep[vid], ensure_ascii=False))
    if which in ("all", "props"):
        rep["_props"] = props(); print(rep["_props"])
    old = {}
    try: old = json.load(open(ROOT + "_familia_info.json"))
    except Exception: pass
    old.update(rep); json.dump(old, open(ROOT + "_familia_info.json", "w"), ensure_ascii=False, indent=1)

"""Cão de três cabeças (Cérbero do fim do jogo, nível 55): mastim negro e magro, musculoso, com três cabeças em leque,
olhos de brasa, coleira de ferro com espetos e correntes partidas, baba que brilha. Mesmo rig e clipes do javali/ratos
(root, body, neck, head, tail, leg_*); as cabeças laterais seguem o pescoço. Uso: python3 build_cerbero.py"""
import sys, math, json, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy
from cz import Vector
from mathutils import noise
import build_quadruped as Q

ROOT = "/home/claude/cinzas/tapa/criaturas/"
vid = "cao_tres_cabecas"
v = dict(nome="Cão de Três Cabeças", k=1.45, S=1.45, target=28000, morte="deitar", morte_altura=0.62, notas="cão morto-vivo de três cabeças-caveira, corpo magro com carne exposta, espinha de osso, coleira com corrente partida; mesmo rig e clipes do javali (BoarClips)")

ZONAS_OSSO, ZONAS_BOCA, OLHOS = [], [], []

def head(parts, extras_spec, k, P, ox, oy, oz, yaw, aberta):
    """cabeça-caveira: focinho longo e estreito de osso, órbitas fundas, mandíbula aberta com gengiva e língua vermelhas"""
    c = math.cos(math.radians(yaw)); s = math.sin(math.radians(yaw))
    def R(x, y, z): return P(ox + x * c - y * s, oy + x * s + y * c, oz + z)
    parts.append(org.limb(f"cranio{yaw}", [(R(0, 0.04, 0.03), (0.15 * k, 0.16 * k)), (R(0, -0.16, 0.0), (0.12 * k, 0.12 * k)), (R(0, -0.34, -0.03), (0.075 * k, 0.07 * k)), (R(0, -0.46, -0.05), (0.055 * k, 0.05 * k))]))
    for sd in (-1, 1):
        parts.append(org.blob(f"arcada{yaw}{sd}", R(sd * 0.075, -0.12, 0.07), (0.055 * k, 0.06 * k, 0.04 * k)))
        parts.append(org.spike(f"orelha{yaw}{sd}", R(sd * 0.09, 0.06, 0.1), R(sd * 0.14, 0.14, 0.27), 0.045 * k))
    drop = 0.2 if aberta else 0.1
    parts.append(org.limb(f"mandibula{yaw}", [(R(0, -0.02, -0.11), (0.09 * k, 0.07 * k)), (R(0, -0.22, -0.11 - drop * 0.5), (0.065 * k, 0.05 * k)), (R(0, -0.4, -0.12 - drop), (0.04 * k, 0.035 * k))]))
    parts.append(org.limb(f"lingua{yaw}", [(R(0, -0.06, -0.09), 0.05 * k), (R(0, -0.24, -0.1 - drop * 0.45), 0.04 * k), (R(0.02, -0.42, -0.14 - drop * 0.9), 0.03 * k)]))
    for t in (0.0, 0.16, 0.34, 0.46): ZONAS_OSSO.append((R(0, -t, 0.0), (0.17 - 0.2 * t) * k))
    for t in (0.0, 0.22, 0.4): ZONAS_OSSO.append((R(0, -t, -0.11 - drop * t * 2.2), 0.08 * k))
    ZONAS_BOCA.append((R(0, -0.25, -0.08 - drop * 0.5), 0.07 * k)); ZONAS_BOCA.append((R(0.02, -0.4, -0.12 - drop * 0.85), 0.05 * k))
    for sd in (-1, 1):
        OLHOS.append(R(sd * 0.07, -0.14, 0.04))
        for i, t in enumerate((0.26, 0.33, 0.4, 0.46)):   # presas de cima (para baixo) e de baixo (para cima)
            top = R(sd * (0.05 - 0.012 * i), -t, -0.05); extras_spec.append(("dente", top, top + (R(0, 0, -0.07 - 0.03 * (i == 0)) - R(0, 0, 0)), 0.016 * k))
            bot = R(sd * (0.045 - 0.01 * i), -t + 0.02, -0.12 - drop * t * 2.2); extras_spec.append(("dente", bot, bot + (R(0, 0, 0.06) - R(0, 0, 0)), 0.013 * k))
    return R

def build():
    cz.reset(); cz.clear_mats(); k = v["k"]
    def P(x, y, z): return Vector((x * k, y * k, z * k))
    parts, extras, bones, spec = [], [], [], []
    # corpo magro e corcunda: ombros altos, ancas baixas, costelas marcadas
    parts.append(org.blob("ombros", P(0, -0.28, 0.8), (0.27 * k, 0.3 * k, 0.3 * k)))
    parts.append(org.blob("tronco", P(0, 0.08, 0.7), (0.2 * k, 0.42 * k, 0.2 * k)))
    parts.append(org.blob("ancas", P(0, 0.46, 0.62), (0.2 * k, 0.22 * k, 0.21 * k)))
    for i in range(5):
        for sd in (-1, 1): parts.append(org.blob(f"costela{i}{sd}", P(sd * 0.16, -0.1 + i * 0.09, 0.62), (0.05 * k, 0.03 * k, 0.12 * k)))
    Rs = []
    for yaw, ox, aberta in ((0, 0.0, True), (58, -0.58, False), (-58, 0.58, True)):
        base = P(ox * 0.42, -0.4, 0.92); top = P(ox, -0.66 + abs(ox) * 0.4, 1.0 - abs(ox) * 0.12)
        parts.append(org.limb(f"pesc{yaw}", [(base, 0.16 * k), (base.lerp(top, 0.5), 0.12 * k), (top, 0.11 * k)]))
        Rs.append((yaw, head(parts, spec, k, P, ox, -0.66 + abs(ox) * 0.4, 1.0 - abs(ox) * 0.12, yaw, aberta)))
    for s_, nm in ((1, "L"), (-1, "R")):
        hipF, kneeF, footF = P(s_ * 0.18, -0.32, 0.72), P(s_ * 0.2, -0.38, 0.34), P(s_ * 0.21, -0.42, 0.0)
        hipB, kneeB, footB = P(s_ * 0.16, 0.48, 0.6), P(s_ * 0.18, 0.62, 0.3), P(s_ * 0.17, 0.52, 0.0)
        for tag, (h, kk, f) in (("F", (hipF, kneeF, footF)), ("B", (hipB, kneeB, footB))):
            parts.append(org.limb(f"pata{tag}{nm}", [(h, 0.13 * k), (h.lerp(kk, 0.5), 0.085 * k), (kk, 0.055 * k), (f + Vector((0, 0, 0.05 * k)), 0.05 * k), (f + Vector((0, -0.08 * k, 0.025 * k)), 0.06 * k)]))
            bn = f"leg_{tag}{nm}"; bones += [(bn + "_a", tuple(h), tuple(kk), "body"), (bn + "_b", tuple(kk), tuple(f), bn + "_a", True)]
            for j in (-1, 0, 1):   # garras longas
                g0 = f + Vector((j * 0.035 * k, -0.12 * k, 0.03 * k)); spec.append(("garra" + bn, g0, g0 + Vector((j * 0.01 * k, -0.08 * k, -0.04 * k)), 0.018 * k))
    for i in range(13):   # espinha de osso saltada
        t = i / 12; b0 = P(0, -0.42 + t * 0.95, 1.04 - 0.38 * t)
        spec.append(("espinha", b0, b0 + P(0, 0.05, 0.1 + 0.07 * math.sin(t * math.pi)), (0.035 - 0.012 * t) * k))
    mesh = org.fuse(vid, parts, voxel=0.008 * k)
    org.displace(mesh, 0.01 * k, 0.1 * k, 'CLOUDS', 2); org.smooth(mesh, 0.5, 1)
    hi = pbr.make_hi(mesh); pbr.detail(hi, grain=0.003 * k, grain_scale=150 / k, pits=0.0, seed=4)
    org.decimate(mesh, v["target"])
    pelo, pelo2, carne, osso, boca = org.lin("#161211"), org.lin("#2e2522"), org.lin("#5a2622"), org.lin("#cdbfa3"), org.lin("#7e2228")
    def cor(p, n, c_):
        for (zc, r) in ZONAS_BOCA:
            if (p - zc).length < r: return boca * (0.7 + 0.4 * noise.noise(p * 30 / k))
        for e in OLHOS:
            if (p - e).length < 0.05 * k: return org.lin("#120808")
        for (zc, r) in ZONAS_OSSO:
            if (p - zc).length < r:
                return org.mix(osso, org.lin("#8d7e66"), max(0, noise.noise(p * 18 / k)) * 0.8) * (0.85 + 0.2 * noise.noise(p * 60 / k))
        kk = org.mix(pelo2, pelo, org.sstep(-0.5, 0.6, n.z))
        fio = noise.noise(Vector((p.x * 50 / k, p.y * 10 / k, p.z * 50 / k))); kk = kk * (0.8 + 0.35 * fio)
        if noise.noise(p * 3.5 / k) > 0.25: kk = org.mix(kk, carne, 0.55)      # carne exposta/podre
        return kk
    def rug(p, n, c_): return 0.75
    pbr.bake(mesh, hi, cor, rug, vid, 1024, extrusion=0.03 * k, wear_col=org.lin("#3a2e28"), dirt_col=org.lin("#070505"))
    bones = [("root", (0, 0, 0), (0, 0, 0.15 * k), None), ("body", tuple(P(0, 0.5, 0.62)), tuple(P(0, -0.3, 0.8)), "root"),
             ("neck", tuple(P(0, -0.4, 0.9)), tuple(P(0, -0.66, 1.0)), "body"), ("head", tuple(P(0, -0.7, 1.02)), tuple(P(0, -1.15, 0.95)), "neck"),
             ("tail", tuple(P(0, 0.66, 0.66)), tuple(P(0.07, 1.18, 0.44)), "body")] + bones
    arm = cz.armature(vid, [(b[0], Vector(b[1]), Vector(b[2]), *b[3:]) for b in bones])
    org.weights_by_bones(mesh, arm, regions=[("head", lambda p: abs(p.x) < 0.16 * k and p.y < -0.66 * k and p.z > 0.6 * k),
                                             ("neck", lambda p: p.y < -0.4 * k and p.z > 0.7 * k)], exclude=("root",), falloff=0.05 * k)
    om = cz.mat(vid + "_osso", "#cdbfa3", rough=0.55)
    for nm, a0, a1, r in spec:
        o = org.spike(f"{nm}{len(extras)}", a0, a1, r); o.data.materials.append(om)
        grp = "body" if nm == "espinha" else (nm[5:] + "_b" if nm.startswith("garra") else ("head" if abs(a0.x) < 0.16 * k and a0.y < -0.66 * k else "neck"))
        g = o.vertex_groups.new(name=grp); g.add([vv.index for vv in o.data.vertices], 1.0, 'REPLACE'); extras.append(o)
    em = cz.mat(vid + "_olho", "#ff3a1a", rough=0.2, emit="#ff2000", strength=5.0)
    for e in OLHOS:
        o = cz.ball(f"olho{len(extras)}", 0.018 * k, 8, 6, loc=tuple(e + Vector((0, -0.02 * k, 0))), m=em); cz.apply_tf(o)
        grp = "head" if abs(e.x) < 0.16 * k else "neck"
        g = o.vertex_groups.new(name=grp); g.add([vv.index for vv in o.data.vertices], 1.0, 'REPLACE'); extras.append(o)
    fe = cz.mat(vid + "_ferro", "#2c2c30", rough=0.4, metal=0.9)
    ctr = P(0, -0.52, 0.96)
    ring = cz.cyl("coleira", 0.17 * k, 0.07 * k, 18, loc=tuple(ctr), rot=(math.radians(60), 0, 0), m=fe); cz.apply_tf(ring)
    g = ring.vertex_groups.new(name="neck"); g.add([vv.index for vv in ring.data.vertices], 1.0, 'REPLACE'); extras.append(ring)
    for i in range(6):   # corrente partida pendurada
        l = cz.cyl(f"elo{i}", 0.032 * k, 0.012 * k, 8, loc=tuple(P(0.06, -0.6, 0.86 - i * 0.075)), rot=(0, math.radians(90 * (i % 2)), 0), m=fe); cz.apply_tf(l)
        g = l.vertex_groups.new(name="neck"); g.add([vv.index for vv in l.data.vertices], 1.0, 'REPLACE'); extras.append(l)
    rabo = org.limb("rabo", [(P(0, 0.66, 0.66), 0.055 * k), (P(0, 0.88, 0.62), 0.04 * k), (P(0.05, 1.06, 0.5), 0.028 * k), (P(0.07, 1.16, 0.44), 0.042 * k)], subdiv=1)
    rabo.data.materials.append(cz.mat(vid + "_rabo", "#1a1514", rough=0.8)); g = rabo.vertex_groups.new(name="tail"); g.add([vv.index for vv in rabo.data.vertices], 1.0, 'REPLACE'); extras.append(rabo)
    for j in range(5):   # ponta do rabo com espetos de osso
        a = j * math.tau / 5; c0 = P(0.07, 1.16, 0.44)
        o = org.spike(f"rabesp{j}", c0, c0 + Vector((math.cos(a) * 0.08 * k, 0.03 * k, math.sin(a) * 0.08 * k)), 0.015 * k); o.data.materials.append(om)
        g = o.vertex_groups.new(name="tail"); g.add([vv.index for vv in o.data.vertices], 1.0, 'REPLACE'); extras.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for e in extras: e.select_set(True)
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh; bpy.ops.object.join(); mesh.data.shade_smooth()
    return arm, mesh

arm, mesh = build()
Q.anims(vid, v, arm, mesh)
cz.ground_fix(arm, mesh, ("death",)); chk = cz.check_ground(arm, mesh, ("death",))
bpy.context.scene.frame_set(1); cz.save(ROOT + vid + "/" + vid, anim=True)
info = dict(nome=v["nome"], tris=cz.tri_count([mesh]), dims_m=[round(d, 3) for d in mesh.dimensions], clips=[t.name for t in arm.animation_data.nla_tracks], notas=v["notas"])
json.dump(info, open(ROOT + vid + "/" + vid + "_info.json", "w"), ensure_ascii=False, indent=1); print("OK", vid, info["tris"], info["dims_m"], flush=True)

"""Quadrupedes organicos PBR (lote 3). Uso: python3 build_quadruped.py <id|all>
Primeira especie: javali_da_duna. Frente -Y no Blender (+Z no glTF), pes no chao, 30 fps.
Clipes: idle, walk, attack_windup, attack_strike, attack_recover, hit, death + charge_windup, charge."""
import sys, math, json, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy
from cz import Vector, Matrix
from mathutils import noise

ROOT = "/home/claude/cinzas/lote3/criaturas/"
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)

V = {
 "javali_da_duna": dict(nome="Javali da Duna", S=1.0, target=16000,
    col=dict(hide="#3b2a22", hide2="#1c120e", belly="#6a5446", mane="#9a3c16", mane2="#4a1a08", snout="#7a5a54", hoof="#16100c",
             wear="#8a6e5a", dirt="#0e0806"),
    tusk="#efe2c2", eyes=("#ff6a2a", "#ff3a00", 2.5),
    notas="quadrupede, presas grandes; investida em linha reta (charge_windup cava o chao, charge galopa em laco)"),
}

def build(vid, v):
    cz.reset(); cz.clear_mats()
    parts, extras, bones = [], [], []
    # ---------------- corpo: corcova alta nos ombros, quadril baixo (silhueta de javali)
    parts.append(org.blob("tronco", (0, 0.05, 0.6), (0.32, 0.6, 0.3)))
    parts.append(org.blob("corcova", (0, -0.26, 0.74), (0.36, 0.36, 0.32)))
    parts.append(org.blob("ancas", (0, 0.42, 0.6), (0.29, 0.27, 0.27)))
    parts.append(org.blob("barriga", (0, 0.0, 0.46), (0.22, 0.42, 0.16)))
    parts.append(org.limb("pescoco", [((0, -0.4, 0.74), 0.24), ((0, -0.58, 0.64), 0.2)]))
    # cabeca em cunha, focinho comprido e disco do nariz
    parts.append(org.limb("cabeca", [((0, -0.62, 0.66), (0.2, 0.23)), ((0, -0.78, 0.57), (0.165, 0.17)), ((0, -0.92, 0.47), (0.095, 0.09)), ((0, -1.03, 0.41), (0.08, 0.075))]))
    parts.append(org.blob("nariz", (0, -1.06, 0.40), (0.085, 0.035, 0.07)))
    for s in (-1, 1):
        parts.append(org.blob(f"bochecha{s}", (s * 0.1, -0.78, 0.5), (0.08, 0.12, 0.08)))
        parts.append(org.blob(f"orelha{s}", (s * 0.13, -0.6, 0.82), (0.05, 0.03, 0.09)))
        parts.append(org.blob(f"sobrancelha{s}", (s * 0.085, -0.8, 0.66), (0.06, 0.05, 0.035)))
    # patas: coxas grossas, canelas curtas, cascos
    legs = {}
    for s, nm in ((1, "L"), (-1, "R")):
        hipF, kneeF, footF = Vector((s * 0.17, -0.3, 0.6)), Vector((s * 0.19, -0.33, 0.3)), Vector((s * 0.19, -0.34, 0.0))
        hipB, kneeB, footB = Vector((s * 0.16, 0.42, 0.62)), Vector((s * 0.18, 0.52, 0.3)), Vector((s * 0.18, 0.45, 0.0))
        for tag, (h, k, f) in (("F", (hipF, kneeF, footF)), ("B", (hipB, kneeB, footB))):
            parts.append(org.limb(f"pata{tag}{nm}", [(h, 0.15), (h.lerp(k, 0.5), 0.12), (k, 0.07), (k.lerp(f, 0.6), 0.055), (f + Vector((0, -0.02, 0.05)), 0.065), (f + Vector((0, -0.04, 0.02)), 0.05)]))
            bn = f"leg_{tag}{nm}"
            bones += [(bn + "_a", tuple(h), tuple(k), "body"), (bn + "_b", tuple(k), tuple(f), bn + "_a", True)]
            legs[(tag, nm)] = (h, k, f)
    # crina: espinhos de pelo duro ao longo da espinha (cor de destaque)
    rnd = random.Random(4); mane_pts = []
    for i in range(16):
        t = i / 15; y = -0.62 + t * 0.95
        z = 0.74 + 0.32 * math.exp(-((y + 0.26) ** 2) / 0.09) + 0.2 * (1 - t) * 0.3 - 0.05 * t
        for s in (-0.045, 0.045):
            b = Vector((s, y, z - 0.03)); tip = b + Vector((s * 1.6, 0.08 + 0.05 * t, 0.12 + 0.07 * math.sin(t * math.pi) + rnd.uniform(-0.02, 0.03)))
            parts.append(org.spike(f"crina{i}{s}", b, tip, 0.04)); mane_pts.append(tip)
    parts.append(org.limb("rabo", [((0, 0.66, 0.66), 0.035), ((0, 0.74, 0.58), 0.025), ((0.02, 0.78, 0.48), 0.03)]))

    mesh = org.fuse(vid, parts, voxel=0.009)
    org.displace(mesh, 0.018, 0.12, 'CLOUDS', 2)
    org.smooth(mesh, 0.5, 1)
    hi = pbr.make_hi(mesh)
    # pelo: granulado forte e "fios" (riscos alongados pelo voronoi esticado)
    pbr.detail(hi, plates=0.0, grain=0.0022, grain_scale=140, barnacles=0, pits=0.0015, seed=7)
    org.decimate(mesh, v["target"])

    col = {k: org.lin(h) for k, h in v["col"].items()}
    def is_mane(p):
        return min((p - m).length for m in mane_pts) < 0.12 and p.z > 0.8
    def cor(p, n, c):
        top = org.sstep(-0.4, 0.5, n.z)
        k = org.mix(col["belly"], col["hide"], top)
        stripe = noise.noise(Vector((p.x * 30, p.y * 6, p.z * 30)))          # pelo correndo no sentido do corpo
        k = k * (0.85 + 0.25 * stripe)
        if noise.noise(p * 5) > 0.25: k = org.mix(k, col["hide2"], 0.4)
        if p.z > 0.78 and abs(p.x) < 0.14 or is_mane(p):
            k = org.mix(k, org.mix(col["mane"], col["mane2"], 0.5 + 0.5 * stripe), 0.85)
        if p.y < -0.9: k = org.mix(k, col["snout"], org.sstep(-0.9, -1.0, p.y))
        if p.z < 0.08: k = org.mix(k, col["hoof"], org.sstep(0.08, 0.03, p.z))
        return k
    def rug(p, n, c):
        r = 0.82 + 0.1 * noise.noise(p * 20)
        if p.y < -1.0: r = 0.35       # focinho umido
        if p.z < 0.06: r = 0.5        # casco
        return r
    pbr.bake(mesh, hi, cor, rug, vid, 1024, extrusion=0.03, wear_col=col["wear"], dirt_col=col["dirt"])

    # ---------------- rig
    bones = [("root", (0, 0, 0), (0, 0, 0.15), None), ("body", (0, 0.45, 0.64), (0, -0.3, 0.74), "root"),
             ("neck", (0, -0.36, 0.74), (0, -0.6, 0.64), "body"), ("head", (0, -0.6, 0.64), (0, -1.04, 0.42), "neck"),
             ("tail", (0, 0.64, 0.66), (0, 0.78, 0.48), "body")] + bones
    arm = cz.armature(vid, [(b[0], Vector(b[1]), Vector(b[2]), *b[3:]) for b in bones])
    def in_head(p): return p.y < -0.62 and p.z > 0.3
    def in_trunk(p): return abs(p.x) < 0.2 and p.z > 0.5 and -0.4 < p.y < 0.55
    org.weights_by_bones(mesh, arm, regions=[("head", in_head), ("body", in_trunk)], exclude=("root",), falloff=0.05)

    # ---------------- pecas duras: presas e olhos
    tm = cz.mat(vid + "_presa", v["tusk"], rough=0.35)
    for s in (-1, 1):
        a = Vector((s * 0.075, -0.97, 0.4)); b = Vector((s * 0.15, -1.05, 0.5)); c = Vector((s * 0.15, -0.98, 0.64)); d = Vector((s * 0.1, -0.92, 0.7))
        t = org.limb(f"presa{s}", [(a, 0.034), (b, 0.03), (c, 0.02), (d, 0.004)], subdiv=1); t.data.materials.append(tm)
        for p_ in t.data.polygons: p_.use_smooth = True
        g = t.vertex_groups.new(name="head"); g.add([vv.index for vv in t.data.vertices], 1.0, 'REPLACE'); extras.append(t)
        lt = org.limb(f"presinha{s}", [(Vector((s * 0.06, -1.0, 0.38)), 0.018), (Vector((s * 0.09, -1.04, 0.44)), 0.004)], subdiv=1); lt.data.materials.append(tm)
        g = lt.vertex_groups.new(name="head"); g.add([vv.index for vv in lt.data.vertices], 1.0, 'REPLACE'); extras.append(lt)
    em = cz.mat(vid + "_olho_emit", v["eyes"][0], rough=0.2, emit=v["eyes"][1], strength=v["eyes"][2])
    for s in (-1, 1):
        e = cz.ball(f"olho{s}", 0.022, 8, 6, loc=(s * 0.1, -0.86, 0.62), m=em); cz.apply_tf(e)
        g = e.vertex_groups.new(name="head"); g.add([vv.index for vv in e.data.vertices], 1.0, 'REPLACE'); extras.append(e)
    bpy.ops.object.select_all(action='DESELECT')
    for e in extras: e.select_set(True)
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.join()
    mesh.data.shade_smooth()
    return arm, mesh

def anims(vid, v, arm, mesh):
    S = v["S"]
    def C(name, n, ground=True, snap=True): return cz.Clip(arm, name, n, mesh=mesh, S=S, ground=ground, snap=snap)
    def L(fl=0, fr=0, bl=0, br=0, kfl=0, kfr=0, kbl=0, kbr=0):
        """rotacao das coxas em X (negativo = pata para frente) e dobra do joelho/jarrete."""
        d = {}
        for nm, a, k in (("FL", fl, kfl), ("FR", fr, kfr), ("BL", bl, kbl), ("BR", br, kbr)):
            d[f"leg_{nm}_a"] = {'r': [(X, a)]}
            if k: d[f"leg_{nm}_b"] = {'r': [(X, k if nm[0] == "B" else -k)]}
        return d
    def m(*ds):
        out = {}
        for d in ds:
            for kk, val in d.items():
                cur = out.setdefault(kk, {}); cur['r'] = cur.get('r', []) + val.get('r', [])
                if 'l' in val: cur['l'] = val['l']
        return out
    # idle: respira, fareja, abana o rabo
    cl = C("idle", 60)
    for f, t in ((1, 0), (15, 1), (30, 0.2), (45, 1), (60, 0)):
        cl.key(f, m({"body": {'r': [(X, -1.5 * t)]}, "neck": {'r': [(X, 4 * t)]}, "head": {'r': [(X, -6 * t + 3), (Z, 5 * math.sin(f / 9))]},
                     "tail": {'r': [(Z, 25 * math.sin(f / 4))]}}))
    cl.done()
    # walk: trote em diagonais (FL+BR / FR+BL), 24 quadros
    cl = C("walk", 24)
    for f in range(1, 25, 2):
        ph = (f - 1) / 24 * math.tau; a = 22 * math.sin(ph); b = -a
        lift1 = max(0, math.sin(ph + math.pi / 2)) * 35; lift2 = max(0, -math.sin(ph + math.pi / 2)) * 35
        cl.key(f, m(L(fl=-a, br=a * 0.9, fr=-b, bl=b * 0.9, kfl=lift1, kbr=lift1 * 0.8, kfr=lift2, kbl=lift2 * 0.8),
                    {"body": {'r': [(Y, 3 * math.sin(ph))]}, "head": {'r': [(X, 4 * math.sin(2 * ph))]}, "tail": {'r': [(Z, 15 * math.sin(ph))]}}))
    cl.key(25, m(L(fl=0, br=0, fr=0, bl=0))); cl.done()
    # ataque: abaixa a cabeca e recua (aviso), estocada com as presas para cima, recupera
    LOW = m(L(fl=-14, fr=-14, bl=10, br=10, kbl=12, kbr=12), {"root": {'l': (0, 0.12, 0)}, "body": {'r': [(X, 6)]}, "neck": {'r': [(X, 18)]}, "head": {'r': [(X, 12)]}})
    GORE = m(L(fl=-30, fr=-22, bl=28, br=24), {"root": {'l': (0, -0.38, 0.04)}, "body": {'r': [(X, -6)]}, "neck": {'r': [(X, -14)]}, "head": {'r': [(X, -32)]}})
    cl = C("attack_windup", 18); cl.key(1, {}); cl.key(10, LOW)
    cl.key(14, m(LOW, {"head": {'r': [(Z, 6)]}})); cl.key(18, LOW); cl.done()
    cl = C("attack_strike", 8); cl.key(1, LOW); cl.key(5, GORE); cl.key(8, GORE); cl.done()
    cl = C("attack_recover", 16); cl.key(1, GORE); cl.key(9, m(L(fl=-6, fr=-4), {"root": {'l': (0, -0.2, 0)}, "head": {'r': [(X, -8)]}})); cl.key(16, {}); cl.done()
    cl = C("attack_full", 42); cl.key(1, {}); cl.key(14, LOW); cl.key(18, LOW); cl.key(22, GORE); cl.key(26, GORE); cl.key(42, {}); cl.done()
    # hit
    HIT = m(L(fl=6, fr=6, bl=-4, br=-4), {"root": {'l': (0, 0.08, 0)}, "body": {'r': [(Y, 8), (X, -4)]}, "neck": {'r': [(Z, 12)]}, "head": {'r': [(X, -18), (Z, 10)]}})
    cl = C("hit", 10); cl.key(1, {}); cl.key(3, HIT); cl.key(10, {}); cl.done()
    # death: tomba de lado
    D1 = m(L(fl=10, fr=-10, bl=-8, br=8, kfl=30, kbl=20), {"body": {'r': [(Y, 30)]}, "neck": {'r': [(X, 20)]}, "head": {'r': [(X, 15)]}})
    D2 = m(L(fl=-25, fr=-15, bl=25, br=15), {"root": {'l': (0.1, 0, 0)}, "body": {'r': [(Y, 86)]}, "neck": {'r': [(X, 10), (Y, 10)]}, "head": {'r': [(X, -10)]}, "tail": {'r': [(Z, 20)]}})
    cl = C("death", 40, snap=False); cl.key(1, {}); cl.key(10, HIT); cl.key(22, D1); cl.key(32, D2); cl.key(40, D2); cl.done()
    # charge_windup: cava o chao com a pata dianteira direita, cabeca baixa, bufando
    BASE = m(L(bl=8, br=8, kbl=10, kbr=10), {"root": {'l': (0, 0.08, 0)}, "neck": {'r': [(X, 16)]}, "head": {'r': [(X, 10)]}, "tail": {'r': [(X, -30)]}})
    cl = C("charge_windup", 30); cl.key(1, {})
    for f, k in ((6, 0), (10, 1), (14, 0), (18, 1), (22, 0), (26, 1), (30, 0)):
        cl.key(f, m(BASE, L(fr=-18 if k else 18, kfr=40 if k else 5), {"body": {'r': [(X, 2 * k)]}, "head": {'r': [(Z, 4 * k)]}}))
    cl.done()
    # charge: galope (pares juntos), corpo balancando
    cl = C("charge", 14)
    for f in range(1, 15, 1):
        ph = (f - 1) / 14 * math.tau; a = 34 * math.sin(ph); b = 30 * math.sin(ph + 2.2)
        cl.key(f, m(L(fl=-a, fr=-a * 0.85, bl=b, br=b * 0.85, kfl=max(0, math.cos(ph)) * 45, kfr=max(0, math.cos(ph)) * 40, kbl=max(0, -math.cos(ph + 2.2)) * 35, kbr=max(0, -math.cos(ph + 2.2)) * 30),
                    {"body": {'r': [(X, 6 * math.sin(ph + 1))]}, "neck": {'r': [(X, 14)]}, "head": {'r': [(X, 8 + 4 * math.sin(ph))]}, "tail": {'r': [(X, -40)]}}))
    cl.key(15, m(L(fl=0), {"neck": {'r': [(X, 14)]}, "head": {'r': [(X, 8)]}, "tail": {'r': [(X, -40)]}})); cl.done()
    return ("death",)

def run(vid):
    v = V[vid]
    arm, mesh = build(vid, v)
    skip = anims(vid, v, arm, mesh)
    cz.ground_fix(arm, mesh, ())
    chk = cz.check_ground(arm, mesh, ())
    bpy.context.scene.frame_set(1)
    cz.save(ROOT + vid + "/" + vid, anim=True)
    info = dict(nome=v["nome"], tris=cz.tri_count([mesh]), dims_m=[round(d, 3) for d in mesh.dimensions], clips=[t.name for t in arm.animation_data.nla_tracks],
                chao_min_z=chk, notas=v["notas"])
    json.dump(info, open(ROOT + vid + "/" + vid + "_info.json", "w"), ensure_ascii=False, indent=1)
    print("OK", vid, info["tris"], info["dims_m"], min(chk.values()))

if __name__ == "__main__":
    w = sys.argv[-1]
    for vid in (V if w == "all" else [w]): run(vid)

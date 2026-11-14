"""Vida e marcos (Cal lote 2). Uso: python3 build_vida.py <gaivota|fogueira|ossada|all>
- vida_gaivota.glb: ave de 1,3 m de envergadura; clipes fly (bater asas, laco), glide (planar inclinando, laco), idle (em pe, laco),
  walk (gingado, laco), takeoff (decolar). Pose de repouso = asas abertas; em pe as asas dobram por animacao.
- cenario_fogueira.glb: roda de pedras, lenha cruzada, brasas (emissivo) e espeto; o fogo/fumaca vem de particulas (VidaAmbiente.cs).
- marco_ossada_gigante.glb: esqueleto de baleia/monstro marinho de ~30 m meio enterrado (marco visto de longe), com colisao."""
import sys, math, random, os, json
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy, bmesh
from cz import Vector, Matrix
from mathutils import noise
import build_props as bp
from build_props import Z, WOOD, DWOOD, IRON, ROPE, BONE, CAL, STONE, EMBER
OUT = "/home/claude/cinzas/calL2/modelos/"
bp.OUT = OUT
X, Y, ZA = (1, 0, 0), (0, 1, 0), (0, 0, 1)

def gaivota():
    cz.reset(); cz.clear_mats()
    parts = []
    body = org.blob("corpo", (0, 0.0, 0.26), (0.085, 0.2, 0.085)); parts.append(body)
    parts.append(org.blob("peito", (0, -0.08, 0.25), (0.08, 0.1, 0.08)))
    parts.append(org.limb("pescoco", [((0, -0.12, 0.28), 0.06), ((0, -0.17, 0.33), 0.05)]))
    parts.append(org.blob("cabeca", (0, -0.2, 0.355), (0.055, 0.07, 0.055)))
    parts.append(org.limb("cauda", [((0, 0.14, 0.27), (0.07, 0.025)), ((0, 0.26, 0.28), (0.075, 0.012))], flat=0.3))
    for s in (-1, 1):   # asas: braco grosso -> ponta fina, achatadas
        w = org.limb(f"asa{s}", [((s * 0.06, -0.02, 0.29), (0.06, 0.02)), ((s * 0.3, 0.0, 0.3), (0.075, 0.016)), ((s * 0.52, 0.05, 0.3), (0.06, 0.012)), ((s * 0.66, 0.11, 0.3), (0.025, 0.006))], flat=0.25)
        parts.append(w)
    mesh = org.fuse("vida_gaivota", parts, voxel=0.007)
    org.smooth(mesh, 0.5, 2)
    hi = pbr.make_hi(mesh); pbr.detail(hi, grain=0.0006, grain_scale=200, seed=3)
    org.decimate(mesh, 5000)
    W, G, K, Y_ = org.lin("#f4f2ee"), org.lin("#9aa4ae"), org.lin("#1a1a1e"), org.lin("#e8b830")
    def cor(p, n, c):
        k = W
        if abs(p.x) > 0.09 and n.z > -0.2: k = G                                 # dorso das asas cinza
        if abs(p.x) > 0.5: k = org.mix(G, K, org.sstep(0.5, 0.58, abs(p.x)))     # pontas pretas
        if p.y > 0.2 and n.z > 0.2: k = org.mix(k, G, 0.5)
        return k * (0.92 + 0.1 * noise.noise(p * 60))
    pbr.bake(mesh, hi, cor, lambda p, n, c: 0.6, "vida_gaivota", 512, extrusion=0.01, dirt_col=org.lin("#5a5a5a"))
    hard = []
    def rig(o, bn):
        cz.apply_tf(o); g = o.vertex_groups.new(name=bn); g.add([v.index for v in o.data.vertices], 1.0, 'REPLACE'); hard.append(o)
    bm_ = cz.mat("gaivota_bico", "#e8b830", rough=0.4)
    rig(cz.seg("bico", (0, -0.24, 0.35), (0, -0.33, 0.335), 0.016, 0.004, 6, m=bm_), "head")
    rig(cz.ball("mancha", 0.007, 6, 4, loc=(0, -0.3, 0.33), m=cz.mat("gaivota_mancha", "#c0301a", rough=0.5)), "head")
    for s in (-1, 1):
        rig(cz.ball(f"olho{s}", 0.009, 6, 4, loc=(s * 0.035, -0.225, 0.37), m=cz.mat("gaivota_olho", "#101010", rough=0.2)), "head")
        pm = cz.mat("gaivota_pata", "#d89a80", rough=0.6)
        rig(cz.seg(f"perna{s}", (s * 0.035, 0.02, 0.2), (s * 0.04, 0.0, 0.03), 0.009, 0.007, 5, m=pm), f"leg_{'L' if s > 0 else 'R'}")
        rig(cz.box(f"pe{s}", (0.045, 0.05, 0.008), loc=(s * 0.04, -0.015, 0.012), m=pm), f"leg_{'L' if s > 0 else 'R'}")
    bones = [("root", (0, 0, 0), (0, 0, 0.1), None), ("body", (0, 0.12, 0.26), (0, -0.1, 0.27), "root"),
             ("head", (0, -0.15, 0.31), (0, -0.25, 0.36), "body"), ("tail", (0, 0.13, 0.27), (0, 0.27, 0.28), "body"),
             ("wing_L1", (0.06, 0.0, 0.29), (0.32, 0.01, 0.3), "body"), ("wing_L2", (0.32, 0.01, 0.3), (0.66, 0.11, 0.3), "wing_L1", True),
             ("wing_R1", (-0.06, 0.0, 0.29), (-0.32, 0.01, 0.3), "body"), ("wing_R2", (-0.32, 0.01, 0.3), (-0.66, 0.11, 0.3), "wing_R1", True),
             ("leg_L", (0.035, 0.02, 0.21), (0.04, 0.0, 0.02), "body"), ("leg_R", (-0.035, 0.02, 0.21), (-0.04, 0.0, 0.02), "body")]
    arm = cz.armature("vida_gaivota", [(b[0], Vector(b[1]), Vector(b[2]), *b[3:]) for b in bones])
    org.weights_by_bones(mesh, arm, regions=[("head", lambda p: p.y < -0.15 and p.z > 0.29), ("body", lambda p: abs(p.x) < 0.08)], exclude=("root", "leg_L", "leg_R"), falloff=0.02)
    bpy.ops.object.select_all(action='DESELECT')
    for e in hard: e.select_set(True)
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh; bpy.ops.object.join(); mesh.data.shade_smooth()
    def C(name, n, ground=True): return cz.Clip(arm, name, n, mesh=mesh, S=1.0, ground=ground)
    def wings(a1, a2, fold=0.0, fold2=0.0):
        return {"wing_L1": {'r': [(Y, -a1), (ZA, fold)]}, "wing_L2": {'r': [(Y, -a2), (ZA, fold2)]},
                "wing_R1": {'r': [(Y, a1), (ZA, -fold)]}, "wing_R2": {'r': [(Y, a2), (ZA, -fold2)]}}
    FOLD = wings(-10, 0, 82, 150)
    TUCK = {"leg_L": {'r': [(X, -70)]}, "leg_R": {'r': [(X, -70)]}}
    def m(*ds):
        out = {}
        for d in ds:
            for k, v in d.items():
                cur = out.setdefault(k, {}); cur['r'] = cur.get('r', []) + v.get('r', [])
                if 'l' in v: cur['l'] = v['l']
        return out
    cl = C("fly", 18, ground=False)                  # bater asas (no lugar; o jogo move o no)
    for f, a1, a2 in ((1, 35, 15), (5, 10, -5), (10, -40, -25), (14, -10, 5), (18, 35, 15)):
        cl.key(f, m(wings(a1, a2), TUCK, {"body": {'r': [(X, -4 + 0.1 * a1)]}}))
    cl.done()
    cl = C("glide", 60, ground=False)
    for f, bank in ((1, 0), (20, 12), (40, -10), (60, 0)):
        cl.key(f, m(wings(4, 6), TUCK, {"root": {'r': [(Y, bank)]}, "head": {'r': [(ZA, bank * 0.6)]}}))
    cl.done()
    cl = C("idle", 60)
    for f, hh in ((1, 0), (15, 30), (30, 0), (45, -25), (60, 0)):
        cl.key(f, m(FOLD, {"head": {'r': [(ZA, hh)]}, "tail": {'r': [(X, 3 * math.sin(f / 9))]}}))
    cl.done()
    cl = C("walk", 20)
    for f in range(1, 21, 2):
        ph = (f - 1) / 20 * math.tau
        cl.key(f, m(FOLD, {"leg_L": {'r': [(X, 22 * math.sin(ph))]}, "leg_R": {'r': [(X, -22 * math.sin(ph))]},
                           "body": {'r': [(Y, 7 * math.sin(ph))]}, "head": {'r': [(X, 10 * math.sin(2 * ph))]}}))
    cl.key(21, FOLD); cl.done()
    cl = C("takeoff", 20, ground=False)
    cl.key(1, FOLD); cl.key(5, m(wings(50, 30), {"body": {'r': [(X, -20)]}})); cl.key(9, m(wings(-35, -20), TUCK, {"root": {'l': (0, 0, 0.3)}}))
    cl.key(14, m(wings(40, 15), TUCK, {"root": {'l': (0, 0, 0.7)}})); cl.key(20, m(wings(-30, -20), TUCK, {"root": {'l': (0, 0, 1.2)}})); cl.done()
    bpy.context.scene.frame_set(1)
    os.makedirs(OUT + "vida_gaivota", exist_ok=True)
    cz.save(OUT + "vida_gaivota/vida_gaivota", anim=True)
    print("OK vida_gaivota", cz.tri_count([mesh]), [t.name for t in arm.animation_data.nla_tracks])

def fogueira():
    cz.reset(); cz.clear_mats(); rnd = random.Random(5); parts = []
    for k in range(11):
        a = k / 11 * math.tau
        p = cz.ico(f"pedra{k}", rnd.uniform(0.13, 0.19), 2, loc=(math.cos(a) * 0.55, math.sin(a) * 0.55, 0.07), scale=(1.3, 1.0, 0.75), rot=(0, 0, a)); cz.apply_tf(p); cz.jitter(p, 0.02, k)
        parts.append(Z(p, STONE if k % 3 else CAL))
    for k in range(6):   # lenha em piramide
        a = k / 6 * math.tau + 0.2
        b0 = Vector((math.cos(a) * 0.42, math.sin(a) * 0.42, 0.06)); t0 = Vector((math.cos(a) * 0.06, math.sin(a) * 0.06, 0.5))
        parts.append(Z(org.limb(f"lenha{k}", [(b0, 0.055), (t0, 0.04)], subdiv=1), DWOOD))
    cinza = cz.cyl("cinzas", 0.38, 0.04, 16, loc=(0, 0, 0.02)); parts.append(Z(cinza, STONE))
    for s in (-1, 1):    # forquilhas + espeto
        parts.append(Z(org.limb(f"forq{s}", [((s * 0.75, 0, 0), 0.03), ((s * 0.75, 0, 0.7), 0.025), ((s * 0.68, 0, 0.82), 0.015)], subdiv=1), WOOD))
        parts.append(Z(org.limb(f"forq2{s}", [((s * 0.75, 0, 0.66), 0.02), ((s * 0.82, 0, 0.82), 0.012)], subdiv=1), WOOD))
    parts.append(Z(cz.seg("espeto", (-0.85, 0, 0.77), (0.85, 0, 0.77), 0.016, 0.016, 6), IRON))
    parts.append(Z(cz.ball("peixe", 0.09, 10, 6, loc=(0.1, 0, 0.74), scale=(2.2, 0.6, 0.8)), BONE))
    brasas = []
    for k in range(9):
        b = cz.ico(f"brasa{k}", rnd.uniform(0.04, 0.07), 1, loc=(rnd.uniform(-0.2, 0.2), rnd.uniform(-0.2, 0.2), 0.06)); brasas.append(b)
    em = cz.join("brasas", brasas)
    bp.finish("cenario_fogueira", parts, size=1024, subdiv=1, extr=0.02, emit=[(em, "#ff7a20", 7.0)], collision=[((1.2, 1.2, 0.5), (0, 0, 0.25), 0)])

def ossada():
    """30 m: cranio comprido com dentes, coluna arqueada saindo da areia, costelas em arco (algumas quebradas), nadadeira."""
    cz.reset(); cz.clear_mats(); rnd = random.Random(12); parts = []
    L = 30.0
    spine = []
    for i in range(26):
        t = i / 25; y = -L / 2 + t * L
        z = 7.0 * math.sin(math.pi * min(1, t * 1.12)) + 0.4 - 3.0 * max(0, t - 0.78) * 4      # cauda entra na areia
        spine.append(Vector((0.3 * math.sin(t * 3), y, z)))
    for i, p in enumerate(spine[2:], 2):
        r = 0.8 * (1 - i / 32)
        parts.append(org.blob(f"vert{i}", p, (r * 1.2, r * 0.7, r)))
        parts.append(org.spike(f"proc{i}", p, p + Vector((0, 0.1, r * 1.8)), r * 0.35))
    parts.append(org.limb("medula", [(p, 0.25 * (1 - i / 30)) for i, p in enumerate(spine)]))
    # cranio comprido na frente, meio enterrado e inclinado
    sk0 = spine[1] + Vector((0, -1.5, -0.8))
    parts.append(org.blob("cranio", sk0 + Vector((0, 0, 1.0)), (2.3, 3.4, 1.9)))
    parts.append(org.limb("maxila", [(sk0 + Vector((0, -2.0, 0.4)), (1.1, 0.5)), (sk0 + Vector((0, -5.0, 0.1)), (0.6, 0.35)), (sk0 + Vector((0, -6.5, -0.1)), (0.3, 0.2))]))
    parts.append(org.limb("mandibula", [(sk0 + Vector((0, -1.8, -0.5)), (0.9, 0.35)), (sk0 + Vector((0.2, -5.2, -0.9)), (0.5, 0.25)), (sk0 + Vector((0.3, -6.3, -1.0)), (0.25, 0.15))]))
    for k in range(10):
        y = -2.6 - k * 0.38
        for s in (-1, 1):
            b = sk0 + Vector((s * (0.55 - k * 0.03), y, -0.05))
            parts.append(org.spike(f"dente{k}{s}", b, b + Vector((s * 0.05, -0.05, -0.55)), 0.09))
    for s in (-1, 1):
        parts.append(org.blob(f"orbita{s}", sk0 + Vector((s * 1.1, -1.0, 1.1)), (0.5, 0.5, 0.45)))
    # costelas
    for i in range(4, 17):
        p = spine[i]; span = 5.2 * math.sin(math.pi * (i - 2) / 17) + 0.8
        for s in (-1, 1):
            if rnd.random() < 0.15: continue                 # costela que falta
            broken = rnd.random() < 0.25
            pts = []
            for a in range(10):
                t = a / 9
                if broken and t > 0.6: break
                ang = t * math.pi * 0.85
                pts.append((p + Vector((s * math.sin(ang) * span, 0.15 * t, -(1 - math.cos(ang)) * (p.z + 0.4) * 0.95)), 0.28 * (1 - t * 0.5)))
            if len(pts) > 1: parts.append(org.limb(f"costela{i}{s}", pts, subdiv=1))
    for s in (-1, 1):    # nadadeira (ossos de "mao")
        b = spine[6] + Vector((s * 1.2, 0, -1.6))
        for f in range(4):
            parts.append(org.limb(f"dedo{s}{f}", [(b, 0.22), (b + Vector((s * (1.4 + f * 0.2), -0.8 + f * 0.5, -0.9)), 0.12), (b + Vector((s * (2.4 + f * 0.3), -1.2 + f * 0.7, -1.3)), 0.06)]))
    o = org.fuse("ossada", parts, voxel=0.07)
    org.displace(o, 0.06, 0.6, 'CLOUDS', 3); org.smooth(o, 0.4, 1)
    for v in o.data.vertices: v.co.z = max(v.co.z, -0.4)
    org.decimate(o, 24000)
    sand = []
    for k in range(9):   # montes de areia onde os ossos entram no chao
        p = spine[min(25, k * 3)]; sand.append(org.blob(f"areia{k}", (p.x, p.y, -0.2), (rnd.uniform(1.5, 2.6), rnd.uniform(1.4, 2.4), 0.55)))
    sand.append(org.blob("areia_cranio", (sk0.x, sk0.y - 2, -0.3), (2.6, 4.0, 0.7)))
    duna = org.fuse("duna", sand, voxel=0.12); org.displace(duna, 0.1, 1.0, 'CLOUDS', 2)
    for v in duna.data.vertices: v.co.z = max(v.co.z, -0.05)
    org.decimate(duna, 3000)
    bp.DET[BONE] = dict(grain=0.004, grain_scale=12, pits=0.006, barnacles=60, barn_r=(0.04, 0.12))
    bp.PAL[BONE] = ("#e8dcc0", "#8a7a5c")
    bp.PAL[44] = ("#c8b48c", "#9a8664"); bp.ROUGH[44] = 0.95; bp.DET[44] = dict(grain=0.02, grain_scale=3)
    bp.finish("marco_ossada_gigante", [Z(o, BONE), Z(duna, 44)], size=2048, subdiv=1, extr=0.12,
              collision=[((4.6, 8.0, 3.6), (0, spine[1].y - 3.5, 1.2), 0), ((2.0, L * 0.7, 8.0), (0, 0, 4.0), 0)])

G = dict(gaivota=gaivota, fogueira=fogueira, ossada=ossada)
if __name__ == "__main__":
    w = sys.argv[-1]
    for k, f in G.items():
        if w in ("all", k): f()

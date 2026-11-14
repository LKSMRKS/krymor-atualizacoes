"""Criaturas da cal que nao sao humanoides (rig proprio, clipes no padrao GlbCreatureVisual):
  verme_broca   - Verme-Broca: verme segmentado de 2,4 m, placas de quitina calcaria, cabeca-broca em espiral que GIRA (osso 'broca').
  inseto_lodo   - Inseto do Lodo: besouro de 0,55 m que anda junto do slime; carapaca esverdeada brilhante, mandibulas.
Frente -Y no Blender (+Z no glTF), pes no chao, 30 fps. Clipes: idle, walk, attack_windup, attack_strike, attack_recover, hit, death.
Uso: python3 build_criaturas_cal.py <id|all>"""
import sys, math, json, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy
from cz import Vector, Matrix
from mathutils import noise

ROOT = "/home/claude/cinzas/criaturas_cal/"
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)

def m(*ds):
    out = {}
    for d in ds:
        for kk, val in d.items():
            cur = out.setdefault(kk, {}); cur['r'] = cur.get('r', []) + val.get('r', [])
            if 'l' in val: cur['l'] = val['l']
    return out

def juntar(mesh, extras):
    bpy.ops.object.select_all(action='DESELECT')
    for e in extras: e.select_set(True)
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh; bpy.ops.object.join(); mesh.data.shade_smooth()

def rigido(o, bone):
    cz.apply_tf(o); g = o.vertex_groups.new(name=bone); g.add([v.index for v in o.data.vertices], 1.0, 'REPLACE'); return o

# ============================================================== VERME-BROCA
NSEG = 8; L = 2.4; R = 0.2
def verme_y(t): return -L / 2 + t * L          # t=0 cabeca (frente, -Y), t=1 cauda
def verme_r(t): return R * (0.75 + 0.35 * math.sin(math.pi * min(1, t * 1.15))) * (1 - 0.6 * max(0, t - 0.75) / 0.25)

def verme(vid="verme_broca", L_=2.4, R_=0.2, broca=True, cores=("#8a6a58", "#d8ccb4"), nome="Verme-Broca", notas=None):
    global L, R
    L, R = L_, R_
    cz.reset(); cz.clear_mats()
    parts = []
    for i in range(24):
        t = i / 23; y = verme_y(t); r = verme_r(t)
        parts.append(org.blob(f"anel{i}", (0, y, r * 0.95), (r, L / 23 * 0.75, r * 0.95), seg=24, rings=12))
    mesh = org.fuse(vid, parts, voxel=0.012)
    org.displace(mesh, 0.01, 0.08, 'CLOUDS', 2); org.smooth(mesh, 0.4, 1)
    hi = pbr.make_hi(mesh); pbr.detail(hi, plates=0.006, plate_scale=9, grain=0.0015, grain_scale=120, pits=0.002, seed=3)
    org.decimate(mesh, 14000)
    def cor(p, n, c):
        t = (p.y + L / 2) / L; top = org.sstep(-0.2, 0.7, n.z)
        k = org.mix(org.lin(cores[0]), org.lin(cores[1]), top)            # barriga rosada, costas de cal
        ring = 0.5 + 0.5 * math.cos((t * 23) * math.tau)                      # sulcos entre aneis
        k = org.mix(k, org.lin("#3a2a22"), org.sstep(0.85, 1.0, ring) * 0.7)
        if noise.noise(p * 6) > 0.3: k = org.mix(k, org.lin("#f2ece0"), 0.35 * top)
        return k
    def rug(p, n, c): return 0.7 - 0.2 * org.sstep(-0.2, 0.7, n.z)
    pbr.bake(mesh, hi, cor, rug, vid, 1024, extrusion=0.03, wear_col=org.lin("#f4efe2"), dirt_col=org.lin("#1a120c"))
    # ---- rig: corrente de segmentos (cabeca na frente) + osso da broca
    bones = [("root", (0, 0, 0), (0, 0, 0.15), None)]
    prev = "root"
    for k in range(NSEG):
        y0 = verme_y(1 - k / NSEG); y1 = verme_y(1 - (k + 1) / NSEG)   # cauda -> cabeca
        nm = f"seg{k}"; bones.append((nm, (0, y0, R), (0, y1, R), prev, k > 0)); prev = nm
    hy = verme_y(0)
    bones.append(("broca", (0, hy, R), (0, hy - 0.35, R), prev, True))
    arm = cz.armature(vid, [(b[0], Vector(b[1]), Vector(b[2]), *b[3:]) for b in bones])
    org.weights_by_bones(mesh, arm, exclude=("root", "broca"), falloff=0.06)
    # ---- broca: cone em espiral (3 cristas) que gira + anel de dentes atras dela
    pedra = cz.mat(vid + "_broca", "#cfc6b2", rough=0.45, metal=0.15)
    dente = cz.mat(vid + "_dente", "#efe6d0", rough=0.3)
    extras = []
    r0 = verme_r(0) * 1.05; base = Vector((0, hy + 0.05, R))
    if not broca:
        for s_ in (-1, 1):
            b0 = Vector((s_ * r0 * 0.5, hy + 0.02, R)); mb = org.limb(f"mandibula{s_}", [(b0, 0.035 * R / 0.2), (b0 + Vector((s_ * 0.06, -0.12, 0.0)) * (R / 0.2), 0.02 * R / 0.2), (b0 + Vector((-s_ * 0.02, -0.2, 0.0)) * (R / 0.2), 0.004)], subdiv=1)
            mb.data.materials.append(dente); extras.append(rigido(mb, "broca"))
    for s in (range(3) if broca else []):
        pts = []
        for j in range(25):
            u = j / 24; a = s * math.tau / 3 + u * math.tau * 1.6; rr = r0 * (1 - u) + 0.012
            pts.append((math.cos(a) * rr, hy + 0.05 - u * 0.42, R + math.sin(a) * rr))
        for j in range(24):
            sg = cz.seg(f"crista{s}_{j}", pts[j], pts[j + 1], 0.022 * (1 - j / 26), 0.02 * (1 - (j + 1) / 26), 5, m=pedra); extras.append(rigido(sg, "broca"))
    for j in (range(14) if broca else []):
        a = j / 14 * math.tau; b0 = base + Vector((math.cos(a) * r0 * 1.05, 0.04, math.sin(a) * r0 * 1.05))
        tip = b0 + Vector((math.cos(a) * 0.05, -0.12, math.sin(a) * 0.05))
        extras.append(rigido(org.spike(f"dente{j}", b0, tip, 0.022), f"seg{NSEG - 1}"))
        extras[-1].data.materials.append(dente)
    # cerdas duras ao longo das costas
    for i in range(1, 22):
        t = i / 22; y = verme_y(t); r = verme_r(t); seg_i = min(NSEG - 1, int((1 - t) * NSEG))
        for s in (-1, 1):
            b0 = Vector((s * r * 0.5, y, r * 1.75)); sp = org.spike(f"cerda{i}{s}", b0, b0 + Vector((s * 0.06, 0.05, 0.06)), 0.01)
            sp.data.materials.append(dente); extras.append(rigido(sp, f"seg{seg_i}"))
    juntar(mesh, extras)
    # ---- clipes
    S = 1.0
    def C(name, n, snap=True): return cz.Clip(arm, name, n, mesh=mesh, S=S, ground=True, snap=snap)
    def onda(ph, amp=18, lift=0.0, spin=0.0, ergue=0.0):
        d = {}
        for k in range(NSEG):
            d[f"seg{k}"] = {'r': [(Z, amp * math.sin(ph - k * 0.9) / NSEG * 2.2)]}
            if ergue and k >= NSEG - 3: d[f"seg{k}"]['r'].append((X, ergue))
        if spin: d["broca"] = {'r': [(Y, spin)]} if broca else {'r': [(X, 8 * math.sin(math.radians(spin)))]}
        return d
    cl = C("idle", 60)
    for f in range(1, 61, 3): cl.key(f, onda(f / 60 * math.tau, 6, spin=(f * 12) % 360 - 180))
    cl.done()
    cl = C("walk", 30)
    for f in range(1, 31, 2): cl.key(f, onda((f - 1) / 30 * math.tau, 26, spin=((f - 1) * 24) % 360 - 180))
    cl.key(31, onda(math.tau, 26, spin=-180)); cl.done()
    UP = onda(0, 4, ergue=-22); UP["root"] = {'l': (0, 0.25, 0)}
    HIT_ = onda(0, 4, ergue=14); HIT_["root"] = {'l': (0, -0.45, 0)}
    cl = C("attack_windup", 18); cl.key(1, {}); cl.key(14, UP); cl.key(18, m(UP, {"broca": {'r': [(Y, 120)] if broca else [(X, 15)]}})); cl.done()
    cl = C("attack_strike", 8)
    for f, a in ((1, 0), (3, 120), (5, -120), (8, 0)): cl.key(f, m(HIT_, {"broca": {'r': [(Y, a)] if broca else [(X, a / 8)]}}))
    cl.done()
    cl = C("attack_recover", 16); cl.key(1, HIT_); cl.key(16, {}); cl.done()
    cl = C("hit", 10); cl.key(1, {}); cl.key(3, onda(1.0, 30)); cl.key(10, {}); cl.done()
    D = onda(0.5, 40); D["root"] = {'r': [(Y, 80)]}
    cl = C("death", 40, snap=False); cl.key(1, {}); cl.key(12, onda(1.0, 35)); cl.key(30, D); cl.key(40, D); cl.done()
    fim(vid, arm, mesh, dict(nome=nome, notas=notas or "verme de 2,4 m; a broca da cabeça GIRA nos clipes (osso 'broca'). Sugestão: sai do chão (attack_windup = ergue a frente), investe e volta. Exceção à regra do manequim: verme não tem corpo humano"))

# ============================================================== INSETO DO LODO
def inseto():
    vid = "inseto_lodo"; cz.reset(); cz.clear_mats()
    S = 1.0; parts = []
    parts.append(org.blob("abdomen", (0, 0.12, 0.2), (0.15, 0.2, 0.11)))
    parts.append(org.blob("torax", (0, -0.08, 0.2), (0.11, 0.09, 0.085)))
    parts.append(org.blob("cabeca", (0, -0.2, 0.18), (0.075, 0.07, 0.06)))
    mesh = org.fuse(vid, parts, voxel=0.006)
    hi = pbr.make_hi(mesh); pbr.detail(hi, plates=0.002, plate_scale=12, grain=0.0006, grain_scale=200, seed=5)
    org.decimate(mesh, 6000)
    def cor(p, n, c):
        top = org.sstep(-0.3, 0.8, n.z)
        k = org.mix(org.lin("#1a2412"), org.lin("#3e6a2a"), top)
        irid = 0.5 + 0.5 * noise.noise(p * 14)
        k = org.mix(k, org.lin("#6ad06a"), 0.35 * irid * top)          # brilho de lodo esverdeado
        if p.y > 0.0 and abs(p.x) < 0.006: k = org.lin("#0a0f08")         # risca entre as asas
        return k
    def rug(p, n, c): return 0.25 + 0.2 * (1 - org.sstep(-0.3, 0.8, n.z))
    pbr.bake(mesh, hi, cor, rug, vid, 512, extrusion=0.015, wear_col=org.lin("#9ae08a"), dirt_col=org.lin("#060a04"))
    bones = [("root", (0, 0, 0), (0, 0, 0.08), None), ("body", (0, 0.3, 0.2), (0, -0.12, 0.2), "root"), ("head", (0, -0.13, 0.19), (0, -0.28, 0.17), "body")]
    legs = []
    for s, nm in ((1, "L"), (-1, "R")):
        for j, yy in enumerate((-0.12, -0.04, 0.05)):
            h = Vector((s * 0.08, yy, 0.17)); k = Vector((s * 0.22, yy + (j - 1) * 0.06, 0.24)); f = Vector((s * 0.32, yy + (j - 1) * 0.12, 0.0))
            bn = f"leg_{nm}{j}"; bones += [(bn + "_a", tuple(h), tuple(k), "body"), (bn + "_b", tuple(k), tuple(f), bn + "_a", True)]
            legs.append((bn, h, k, f))
    arm = cz.armature(vid, [(b[0], Vector(b[1]), Vector(b[2]), *b[3:]) for b in bones])
    org.weights_by_bones(mesh, arm, regions=[("head", lambda p: p.y < -0.15), ("body", lambda p: True)], exclude=("root",))
    quit_ = cz.mat(vid + "_perna", "#121a0e", rough=0.4)
    extras = []
    for bn, h, k, f in legs:
        extras.append(rigido(cz.seg(bn + "c", h, k, 0.016, 0.012, 6, m=quit_), bn + "_a"))
        extras.append(rigido(cz.seg(bn + "p", k, f, 0.011, 0.004, 6, m=quit_), bn + "_b"))
    for s in (-1, 1):   # mandibulas e antenas
        b0 = Vector((s * 0.035, -0.26, 0.16))
        extras.append(rigido(org.spike(f"mand{s}", b0, b0 + Vector((-s * 0.03, -0.07, -0.01)), 0.014), "head")); extras[-1].data.materials.append(quit_)
        extras.append(rigido(cz.seg(f"ant{s}", b0 + Vector((0, 0.02, 0.04)), b0 + Vector((s * 0.09, -0.12, 0.16)), 0.004, 0.002, 4, m=quit_), "head"))
    em = cz.mat(vid + "_olho", "#c8ff60", rough=0.2, emit="#a8ff40", strength=3.0)
    for s in (-1, 1):
        e = cz.ball(f"olho{s}", 0.018, 8, 6, loc=(s * 0.05, -0.24, 0.2), m=em); extras.append(rigido(e, "head"))
    juntar(mesh, extras)
    def C(name, n, snap=True): return cz.Clip(arm, name, n, mesh=mesh, S=S, ground=True, snap=snap)
    def pernas(ph, amp=22, lift=25):
        d = {}
        for s, nm in ((1, "L"), (-1, "R")):
            for j in range(3):
                g = (j + (0 if nm == "L" else 1)) % 2       # tripe alternado
                a = amp * math.sin(ph + g * math.pi); up = max(0, math.cos(ph + g * math.pi)) * lift
                d[f"leg_{nm}{j}_a"] = {'r': [(Z, a * s), (Y, -up * s)]}
        return d
    cl = C("idle", 40)
    for f in (1, 10, 20, 30, 40): cl.key(f, m(pernas(0, 0, 0), {"head": {'r': [(Z, 6 * math.sin(f / 6))]}, "body": {'r': [(X, 1.5 * math.sin(f / 7))]}}))
    cl.done()
    cl = C("walk", 16)
    for f in range(1, 17): cl.key(f, pernas((f - 1) / 16 * math.tau))
    cl.key(17, pernas(math.tau)); cl.done()
    LOW = {"body": {'r': [(X, 8)]}, "head": {'r': [(X, 15)]}, "root": {'l': (0, 0.06, 0)}}
    BITE = {"body": {'r': [(X, -6)]}, "head": {'r': [(X, -20)]}, "root": {'l': (0, -0.18, 0.03)}}
    cl = C("attack_windup", 12); cl.key(1, {}); cl.key(12, LOW); cl.done()
    cl = C("attack_strike", 6); cl.key(1, LOW); cl.key(4, BITE); cl.key(6, BITE); cl.done()
    cl = C("attack_recover", 10); cl.key(1, BITE); cl.key(10, {}); cl.done()
    cl = C("hit", 8); cl.key(1, {}); cl.key(3, {"body": {'r': [(Y, 12)]}, "root": {'l': (0, 0.05, 0)}}); cl.key(8, {}); cl.done()
    D = m(pernas(0, 0, 0), {"root": {'r': [(Y, 175)], 'l': (0, 0, 0.2)}})
    for s, nm in ((1, "L"), (-1, "R")):
        for j in range(3): D[f"leg_{nm}{j}_b"] = {'r': [(X, 40)]}
    cl = C("death", 30, snap=False); cl.key(1, {}); cl.key(12, m(pernas(1, 30, 30), {"root": {'r': [(Y, 90)], 'l': (0, 0, 0.12)}})); cl.key(24, D); cl.key(30, D); cl.done()
    fim(vid, arm, mesh, dict(nome="Inseto do Lodo", notas="besouro de 0,55 m que acompanha o slime (2-3 em volta de cada slime). Olhos verdes brilhando"))

def fim(vid, arm, mesh, info):
    cz.ground_fix(arm, mesh, ("death",)); chk = cz.check_ground(arm, mesh, ("death",))
    bpy.context.scene.frame_set(1)
    cz.save(ROOT + vid + "/" + vid, anim=True)
    info.update(asset_id=vid, tris=cz.tri_count([mesh]), dims_m=[round(d, 3) for d in mesh.dimensions], clips=[t.name for t in arm.animation_data.nla_tracks])
    json.dump(info, open(ROOT + vid + "/" + vid + "_info.json", "w"), ensure_ascii=False, indent=1)
    print("OK", vid, info["tris"], info["dims_m"], flush=True)

if __name__ == "__main__":
    w = sys.argv[-1]
    larva = lambda: verme("larva_de_cal", 1.15, 0.14, False, ("#a89a80", "#efe8d8"), "Larva de Cal", "larva de 1,15 m da caverna de cal, mandíbulas em pinça (osso 'broca' abre/fecha), cerdas de cal")
    for vid, fn in (("verme_broca", verme), ("inseto_lodo", inseto), ("larva_de_cal", larva)):
        if w in ("all", vid): fn()

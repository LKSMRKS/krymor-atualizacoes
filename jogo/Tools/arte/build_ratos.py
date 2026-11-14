"""Ratos (mesmo rig e clipes do javali: root, body, neck, head, tail, leg_*). Uso: python3 build_ratos.py <id|all>
  rato_do_cais  - rato de 0,75 m, pelo marrom-acinzentado, rabo comprido rosado, orelhas grandes, olhos vermelhos.
  rato_gigante  - 1,5 m, pelo preto sarnento com falhas, cicatrizes, dentes amarelos enormes, espinhos de pelo duro nas costas."""
import sys, math, json, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy
from cz import Vector, Matrix
from mathutils import noise
import build_quadruped as Q

ROOT = "/home/claude/cinzas/criaturas_r1/"
V = {
 "rato_do_cais": dict(nome="Rato do Cais", k=0.5, S=0.5, pelo="#5a4c42", pelo2="#2e2620", barriga="#a08a78", pele="#c88a80", olho=("#ff2a10", "#ff1000", 3.0),
                      dentes="#efe4c8", sarna=False, target=9000, notas="rato do cais, ágil, pelo marrom-acinzentado, rabo rosado"),
 "rato_gigante": dict(nome="Rato Gigante", k=0.95, S=0.95, pelo="#2a2624", pelo2="#100e0d", barriga="#6a5a50", pele="#a87a70", olho=("#ff2a10", "#ff1000", 4.0),
                      dentes="#d8b860", sarna=True, target=14000, notas="ratazana do tamanho de um cão: pelo preto sarnento com falhas de pele, cicatrizes, dentes amarelos"),
}

def build(vid, v):
    cz.reset(); cz.clear_mats(); k = v["k"]
    def P(x, y, z): return Vector((x * k, y * k, z * k))
    parts, extras = [], []
    parts.append(org.blob("tronco", P(0, 0.15, 0.55), (0.3 * k, 0.55 * k, 0.28 * k)))
    parts.append(org.blob("ancas", P(0, 0.45, 0.5), (0.32 * k, 0.32 * k, 0.3 * k)))
    parts.append(org.blob("peito", P(0, -0.25, 0.5), (0.24 * k, 0.28 * k, 0.24 * k)))
    parts.append(org.limb("cabeca", [(P(0, -0.42, 0.55), (0.19 * k, 0.2 * k)), (P(0, -0.62, 0.48), (0.14 * k, 0.13 * k)), (P(0, -0.8, 0.4), (0.07 * k, 0.065 * k)), (P(0, -0.88, 0.37), (0.04 * k, 0.04 * k))]))
    for s in (-1, 1):
        parts.append(org.blob(f"orelha{s}", P(s * 0.13, -0.45, 0.72), (0.09 * k, 0.03 * k, 0.1 * k)))
        parts.append(org.blob(f"bochecha{s}", P(s * 0.1, -0.62, 0.44), (0.07 * k, 0.09 * k, 0.07 * k)))
    bones = []
    for s, nm in ((1, "L"), (-1, "R")):
        hipF, kneeF, footF = P(s * 0.15, -0.25, 0.45), P(s * 0.18, -0.3, 0.2), P(s * 0.19, -0.36, 0.0)
        hipB, kneeB, footB = P(s * 0.18, 0.45, 0.45), P(s * 0.22, 0.32, 0.18), P(s * 0.2, 0.5, 0.0)
        for tag, (h, kk, f) in (("F", (hipF, kneeF, footF)), ("B", (hipB, kneeB, footB))):
            parts.append(org.limb(f"pata{tag}{nm}", [(h, 0.11 * k if tag == "B" else 0.08 * k), (kk, 0.05 * k), (f + Vector((0, 0, 0.03 * k)), 0.035 * k), (f + Vector((0, -0.07 * k, 0.015 * k)), 0.03 * k)]))
            bn = f"leg_{tag}{nm}"; bones += [(bn + "_a", tuple(h), tuple(kk), "body"), (bn + "_b", tuple(kk), tuple(f), bn + "_a", True)]
    mesh = org.fuse(vid, parts, voxel=0.008 * k)
    org.displace(mesh, 0.012 * k, 0.1 * k, 'CLOUDS', 2); org.smooth(mesh, 0.5, 1)
    hi = pbr.make_hi(mesh); pbr.detail(hi, grain=0.0025 * k, grain_scale=160 / k, pits=0.001 * k if v["sarna"] else 0.0, seed=9)
    org.decimate(mesh, v["target"])
    col = {kk: org.lin(h) for kk, h in v.items() if isinstance(h, str) and h.startswith("#")}
    rnd = random.Random(3)
    falhas = [P(rnd.uniform(-0.25, 0.25), rnd.uniform(-0.3, 0.6), rnd.uniform(0.5, 0.8)) for _ in range(9)] if v["sarna"] else []
    def cor(p, n, c):
        top = org.sstep(-0.4, 0.6, n.z)
        kk = org.mix(col["barriga"], col["pelo"], top)
        fio = noise.noise(Vector((p.x * 60 / k, p.y * 12 / k, p.z * 60 / k)))
        kk = kk * (0.8 + 0.3 * fio)
        if noise.noise(p * 6 / k) > 0.2: kk = org.mix(kk, col["pelo2"], 0.45)
        if p.y < -0.82 * k: kk = org.mix(kk, col["pele"], org.sstep(-0.82 * k, -0.88 * k, p.y))      # focinho rosado
        if p.z < 0.05 * k: kk = org.mix(kk, col["pele"], 0.7)                                         # patinhas
        for f in falhas:
            d = (p - f).length
            if d < 0.08 * k: kk = org.mix(kk, col["pele"], 0.85 * (1 - d / (0.08 * k)))              # falhas de sarna
        return kk
    def rug(p, n, c): return 0.85 if p.y > -0.82 * k else 0.4
    pbr.bake(mesh, hi, cor, rug, vid, 1024, extrusion=0.03 * k, wear_col=org.mix(col["pelo"], col["barriga"], 0.5), dirt_col=col["pelo2"])
    bones = [("root", (0, 0, 0), (0, 0, 0.15 * k), None), ("body", tuple(P(0, 0.5, 0.55)), tuple(P(0, -0.25, 0.55)), "root"),
             ("neck", tuple(P(0, -0.25, 0.55)), tuple(P(0, -0.45, 0.55)), "body"), ("head", tuple(P(0, -0.45, 0.55)), tuple(P(0, -0.88, 0.37)), "neck"),
             ("tail", tuple(P(0, 0.72, 0.45)), tuple(P(0, 1.1, 0.25)), "body")] + bones
    arm = cz.armature(vid, [(b[0], Vector(b[1]), Vector(b[2]), *b[3:]) for b in bones])
    org.weights_by_bones(mesh, arm, regions=[("head", lambda p: p.y < -0.45 * k and p.z > 0.25 * k)], exclude=("root",), falloff=0.05 * k)
    # rabo comprido (rigido em 'tail' + ponta), dentes, olhos, bigodes
    pele = cz.mat(vid + "_rabo", v["pele"], rough=0.5)
    rabo = org.limb("rabo", [(P(0, 0.72, 0.45), 0.045 * k), (P(0, 1.0, 0.3), 0.032 * k), (P(0.05, 1.3, 0.12), 0.022 * k), (P(0.15, 1.6, 0.04), 0.012 * k), (P(0.3, 1.8, 0.03), 0.004 * k)], subdiv=1)
    rabo.data.materials.append(pele); g = rabo.vertex_groups.new(name="tail"); g.add([vv.index for vv in rabo.data.vertices], 1.0, 'REPLACE'); extras.append(rabo)
    dm = cz.mat(vid + "_dente", v["dentes"], rough=0.3)
    for s in (-1, 1):
        d = org.limb(f"dente{s}", [(P(s * 0.015, -0.86, 0.33), 0.014 * k), (P(s * 0.015, -0.88, 0.26), 0.008 * k)], subdiv=1); d.data.materials.append(dm)
        g = d.vertex_groups.new(name="head"); g.add([vv.index for vv in d.data.vertices], 1.0, 'REPLACE'); extras.append(d)
    em = cz.mat(vid + "_olho", v["olho"][0], rough=0.2, emit=v["olho"][1], strength=v["olho"][2])
    for s in (-1, 1):
        e = cz.ball(f"olho{s}", 0.025 * k, 8, 6, loc=tuple(P(s * 0.1, -0.62, 0.56)), m=em); cz.apply_tf(e)
        g = e.vertex_groups.new(name="head"); g.add([vv.index for vv in e.data.vertices], 1.0, 'REPLACE'); extras.append(e)
    bm_ = cz.mat(vid + "_bigode", "#d8d0c4", rough=0.5)
    for s in (-1, 1):
        for j in range(3):
            w = cz.seg(f"bigode{s}{j}", tuple(P(s * 0.04, -0.82, 0.38 + j * 0.015)), tuple(P(s * 0.25, -0.78 + j * 0.03, 0.36 + j * 0.04)), 0.003 * k, 0.001 * k, 3, m=bm_)
            cz.apply_tf(w); g = w.vertex_groups.new(name="head"); g.add([vv.index for vv in w.data.vertices], 1.0, 'REPLACE'); extras.append(w)
    if v["sarna"]:
        for i in range(10):   # pelo duro espetado nas costas
            t = i / 9; b0 = P(0, -0.2 + t * 0.7, 0.8 - 0.1 * t)
            sp = org.spike(f"espeto{i}", b0, b0 + P(rnd.uniform(-0.04, 0.04), 0.08, 0.1), 0.02 * k); sp.data.materials.append(cz.mat(vid + "_espeto", v["pelo2"], rough=0.9))
            g = sp.vertex_groups.new(name="body"); g.add([vv.index for vv in sp.data.vertices], 1.0, 'REPLACE'); extras.append(sp)
    bpy.ops.object.select_all(action='DESELECT')
    for e in extras: e.select_set(True)
    mesh.select_set(True); bpy.context.view_layer.objects.active = mesh; bpy.ops.object.join(); mesh.data.shade_smooth()
    return arm, mesh

def run(vid):
    v = V[vid]; arm, mesh = build(vid, v)
    Q.anims(vid, v, arm, mesh)
    cz.ground_fix(arm, mesh, ("death",)); chk = cz.check_ground(arm, mesh, ("death",))
    bpy.context.scene.frame_set(1); cz.save(ROOT + vid + "/" + vid, anim=True)
    info = dict(nome=v["nome"], tris=cz.tri_count([mesh]), dims_m=[round(d, 3) for d in mesh.dimensions], clips=[t.name for t in arm.animation_data.nla_tracks], notas=v["notas"])
    json.dump(info, open(ROOT + vid + "/" + vid + "_info.json", "w"), ensure_ascii=False, indent=1); print("OK", vid, info["tris"], info["dims_m"], flush=True)

if __name__ == "__main__":
    w = sys.argv[-1]
    for vid in (V if w == "all" else [w]): run(vid)

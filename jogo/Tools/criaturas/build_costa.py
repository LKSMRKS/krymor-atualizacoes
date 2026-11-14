"""Kit de cenario da Costa do Cal (tapa artistico). Uso: python3 build_costa.py <id|grupo|all>
Mesmo pipeline PBR do lote 3 (build_props.finish). Saida: /home/claude/cinzas/costa/modelos/<id>/<id>.glb
Pecas normalizadas quando o mapa da a medida: rochas = raio 1 m e altura 1 m (o jogo escala por r e height),
penhasco = trecho de 10 m de comprimento (X), 3 m de fundo e 8 m de altura (o jogo repete ao longo do muro)."""
import sys, math, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy, bmesh
from cz import Vector, Matrix
from mathutils import noise
import build_props as bp
from build_props import Z, WOOD, DWOOD, IRON, ROPE, HIDE, BONE, CAL, PAINT, COPPER, STONE, GRASS

bp.OUT = "/home/claude/cinzas/costa/modelos/"
# zonas extras da Costa
FOLHA, FOLHA2, CASCA, ALGA, CONCHA, AGUA, JUNCO, FLOR_R, FLOR_A, AREIA, AREIA_M, DERIVA, LODO = range(20, 33)
bp.PAL.update({FOLHA: ("#4e6a2e", "#22341a"), FOLHA2: ("#7a8a3a", "#3a4a1c"), CASCA: ("#6a5444", "#2e241c"), ALGA: ("#3a4a26", "#1a2410"),
               CONCHA: ("#f2e6d8", "#c8a890"), AGUA: ("#3a8a8a", "#1a4a5a"), JUNCO: ("#8a8a4a", "#4a4a24"),
               FLOR_R: ("#c04a8a", "#7a2a5a"), FLOR_A: ("#e8c040", "#a07a20"), AREIA: ("#cdbfa3", "#a8987a"), AREIA_M: ("#8a7e68", "#5e5444"), DERIVA: ("#b8b0a2", "#6e665a"), LODO: ("#3e3a2c", "#1e1c14")})
bp.ROUGH.update({FOLHA: 0.75, FOLHA2: 0.8, CASCA: 0.9, ALGA: 0.35, CONCHA: 0.4, AGUA: 0.05, JUNCO: 0.8, FLOR_R: 0.6, FLOR_A: 0.6, AREIA: 0.95, AREIA_M: 0.35, DERIVA: 0.85, LODO: 0.5})
bp.DET.update({FOLHA: dict(grain=0.01, grain_scale=14), FOLHA2: dict(grain=0.008, grain_scale=16), CASCA: dict(grain=0.006, grain_scale=30, plates=0.006, plate_scale=6),
               ALGA: dict(grain=0.004, grain_scale=30), CONCHA: dict(grain=0.0015, grain_scale=50), AREIA: dict(grain=0.004, grain_scale=20), AREIA_M: dict(grain=0.003, grain_scale=20), DERIVA: dict(grain=0.006, grain_scale=25, plates=0.004, plate_scale=4)})

def strata(o, step, tilt=(0.12, 0.05), amt=0.75, warp=0.35, seed=0):
    """estratos inclinados e irregulares: degraus parciais num plano inclinado, deformados por ruido (nao vira bolo nem tijolo)"""
    off = Vector((seed * 3.1, seed * 1.7, 0))
    for v in o.data.vertices:
        p = v.co
        zw = p.z + tilt[0] * p.x + tilt[1] * p.y + warp * step * noise.noise(p * 0.6 + off)
        t = zw / step; f = math.floor(t); fr = t - f
        q = (f + org.sstep(0.65, 1.0, fr)) * step
        k = 1.0 + 0.12 * noise.noise(Vector((f * 1.7 + seed, 0.3, 0.9)))     # cada camada avanca/recua
        v.co.z = p.z + (q - zw) * amt
        v.co.x *= k; v.co.y *= 1.0 + 0.08 * noise.noise(Vector((f * 2.3 + seed, 1.1, 0.2)))
    o.data.update()

def rock(name, blobs, step, seed, voxel=0.035, target=2500, moss=True):
    o = org.fuse(name, [org.blob(f"b{i}", c, s) for i, (c, s) in enumerate(blobs)], voxel=voxel)
    org.displace(o, 0.22, 0.9, 'CLOUDS', 2)                 # quebra o volume redondo antes dos estratos
    for v in o.data.vertices:                              # faces planas de fratura (cortes em angulo)
        for nx, ny, nz, d in ((0.8, -0.5, 0.3, 0.75), (-0.6, -0.7, 0.4, 0.8), (0.1, 0.9, 0.5, 0.85)):
            nv = Vector((nx, ny, nz)).normalized(); e = v.co.dot(nv) - d
            if e > 0: v.co -= nv * e * 0.85
    strata(o, step, (0.18, 0.08), 0.6, 0.4, seed)
    org.displace(o, 0.05, 0.4, 'CLOUDS', 3); org.displace(o, -0.035, 0.16, 'VORONOI')
    for v in o.data.vertices: v.co.z = max(v.co.z, -0.08)
    org.decimate(o, target)
    return Z(o, CAL)

def norm1(objs, r=1.0, h=1.0):
    """escala o conjunto para caber em raio r (XY) e altura h"""
    for o in objs: cz.apply_tf(o)
    xs = [abs(v.co.x) for o in objs for v in o.data.vertices] + [abs(v.co.y) for o in objs for v in o.data.vertices]
    zs = [v.co.z for o in objs for v in o.data.vertices]
    kr, kh = r / max(xs), h / max(zs)
    for o in objs: o.data.transform(Matrix.Diagonal((kr, kr, kh, 1)))

def rochas():
    defs = {
     "costa_rocha_a": ([((0, 0, 0.3), (1.0, 0.85, 0.55)), ((0.25, 0.1, 0.8), (0.65, 0.55, 0.45)), ((-0.3, -0.15, 0.55), (0.5, 0.5, 0.45))], 0.16),   # pilha em camadas
     "costa_rocha_b": ([((0, 0, 0.2), (1.1, 0.8, 0.35)), ((0.4, 0.2, 0.35), (0.6, 0.5, 0.3)), ((-0.5, -0.1, 0.3), (0.5, 0.45, 0.25))], 0.12),          # laje larga e baixa
     "costa_rocha_c": ([((-0.35, 0, 0.45), (0.6, 0.55, 0.6)), ((0.45, 0.15, 0.35), (0.5, 0.5, 0.45)), ((0.05, -0.45, 0.2), (0.35, 0.3, 0.25))], 0.2),   # 3 pedras juntas
    }
    for i, (rid, (bl, st)) in enumerate(defs.items()):
        cz.reset(); cz.clear_mats()
        o = rock(rid, bl, st, i)
        parts = [o]
        rnd = random.Random(i)
        for k in range(5):     # pedrinhas soltas no pe
            a = rnd.uniform(0, math.tau); rr = rnd.uniform(0.9, 1.2)
            p = cz.ico(f"seixo{k}", rnd.uniform(0.06, 0.12), 2, loc=(math.cos(a) * rr, math.sin(a) * rr, 0.03), scale=(1.2, 1, 0.6)); cz.apply_tf(p); cz.jitter(p, 0.01, k)
            parts.append(Z(p, STONE))
        norm1(parts, 1.15, 1.0)
        bp.finish(rid, parts, size=1024, subdiv=1, extr=0.04)

def penhascos():
    for vi, seed in enumerate((3, 8)):
        cz.reset(); cz.clear_mats(); rnd = random.Random(seed)
        bl = []
        for k in range(9):
            x = -5 + k * 1.25 + rnd.uniform(-0.3, 0.3)
            bl.append(org.blob(f"m{k}", (x, rnd.uniform(-0.2, 1.0), rnd.uniform(2.5, 4.5)), (rnd.uniform(1.4, 2.0), rnd.uniform(1.3, 2.0), rnd.uniform(3.2, 4.2))))
        for k in range(4):     # contrafortes que avancam e quebram a linha reta
            x = -4 + k * 2.6 + rnd.uniform(-0.6, 0.6)
            bl.append(org.blob(f"c{k}", (x, -1.0, rnd.uniform(1.5, 3.5)), (rnd.uniform(0.8, 1.3), 1.1, rnd.uniform(1.8, 3.6))))
        for k in range(5):
            x = -4 + k * 2.0 + rnd.uniform(-0.4, 0.4)
            bl.append(org.blob(f"t{k}", (x, rnd.uniform(0.3, 1.2), rnd.uniform(6.3, 8.2)), (rnd.uniform(1.0, 1.8), 1.3, rnd.uniform(0.9, 1.9))))
        o = org.fuse("penhasco", bl, voxel=0.09)
        for v in o.data.vertices:
            if abs(v.co.x) > 5.4: v.co.x = math.copysign(5.4 + 0.15 * noise.noise(v.co), v.co.x)   # emenda com o trecho vizinho
            if v.co.y < -1.6: v.co.y = -1.6 - 0.6 * math.tanh((-1.6 - v.co.y) / 0.6)
            v.co.y = min(1.5, v.co.y); v.co.z = max(-0.3, min(8.0 + 0.6 * noise.noise(v.co * 0.4), v.co.z))
        org.displace(o, 0.45, 2.4, 'CLOUDS', 2)
        strata(o, 0.7, (0.07, 0.0), 0.7, 0.5, seed)
        org.displace(o, 0.18, 1.2, 'CLOUDS', 3); org.displace(o, -0.16, 0.45, 'VORONOI')
        for v in o.data.vertices:
            if abs(v.co.x) > 5.4: v.co.x = math.copysign(5.4, v.co.x)        # 0,4 m de sobra de cada lado: trechos a cada 10 m se sobrepoem
            if v.co.z > 3.0: v.co.z = 3.0 + (v.co.z - 3.0) * (1.0 + 0.35 * noise.noise(Vector((v.co.x * 0.35 + seed, 0.5, 0.2))))   # topo recortado
        org.decimate(o, 7000)
        parts = [Z(o, CAL)]
        for k in range(10):    # entulho no pe do paredao
            p = cz.ico(f"entulho{k}", rnd.uniform(0.2, 0.5), 2, loc=(rnd.uniform(-4.6, 4.6), rnd.uniform(-1.9, -1.4), 0.1), scale=(1.2, 1, 0.7)); cz.apply_tf(p); cz.jitter(p, 0.05, k)
            parts.append(Z(p, CAL if k % 3 else STONE))
        for k in range(6):     # tufos de capim nos degraus
            b = Vector((rnd.uniform(-4.5, 4.5), -1.2, rnd.choice([0.0, 2.2, 4.4])))
            for j in range(5):
                a = j / 5 * math.tau
                parts.append(Z(cz.seg(f"capim{k}{j}", b + Vector((math.cos(a) * 0.05, math.sin(a) * 0.05, 0)), b + Vector((math.cos(a) * 0.3, math.sin(a) * 0.3 - 0.1, 0.55)), 0.025, 0.0, 4), FOLHA2))
        bp.finish(f"costa_penhasco_{'ab'[vi]}", parts, size=2048, subdiv=1, extr=0.08, collision=[((10.0, 3.0, 8.0), (0, 0, 4.0), 0)])

# ------------------------------------------------------------------ vegetacao
def ribbon(name, pts, widths, side=None, serr=0.0, thick=0.008):
    """fita (folha, lamina de capim, folha de coqueiro) ao longo de pts; serr>0 recorta em foliolos (alterna largura)."""
    bm = bmesh.new(); rows = []
    for i, (p, w) in enumerate(zip(pts, widths)):
        p = Vector(p)
        t = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
        sd = (side or t.cross(Vector((0, 0, 1)))).normalized() if (side or t.cross(Vector((0, 0, 1)))).length > 1e-4 else Vector((1, 0, 0))
        if serr and i % 2: w *= serr
        rows.append((bm.verts.new(p - sd * w), bm.verts.new(p), bm.verts.new(p + sd * w)))
    for a, b in zip(rows, rows[1:]):
        bm.faces.new((a[0], b[0], b[1], a[1])); bm.faces.new((a[1], b[1], b[2], a[2]))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = cz._link(bpy.data.objects.new(name, me))
    if thick:
        so = o.modifiers.new("s", 'SOLIDIFY'); so.thickness = thick; org._apply_all(o)
    return o

def curve_pts(base, d, length, n, droop=0.0, lift=0.0):
    d = Vector(d).normalized(); out = []
    for i in range(n + 1):
        t = i / n
        out.append(Vector(base) + d * length * t + Vector((0, 0, lift * t - droop * t * t)))
    return out

def crown_mass(name, centers, seed, voxel=0.06):
    """copa em tufos: blobs achatados fundidos e esburacados (le como folhagem, nao como bola lisa)"""
    o = org.fuse(name, [org.blob(f"t{i}", c, s, 20, 10) for i, (c, s) in enumerate(centers)], voxel=voxel)
    org.displace(o, 0.35, 0.7, 'CLOUDS', 2); org.displace(o, 0.22, 0.35, 'VORONOI')
    org.smooth(o, 0.4, 1); org.decimate(o, 2500)
    return o

def pinheiros():
    for vi in range(3):
        cz.reset(); cz.clear_mats(); rnd = random.Random(40 + vi)
        H = (6.0, 7.5, 5.2)[vi]; parts = []
        lean = Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), 0))
        trunk = [(Vector((0, 0, 0)), 0.28), (Vector((0, 0, H * 0.3)) + lean * 0.3, 0.2), (Vector((0, 0, H * 0.62)) + lean * 0.8, 0.15), (Vector((0, 0, H * 0.82)) + lean, 0.1)]
        parts.append(Z(org.limb("tronco", trunk, subdiv=1), CASCA))
        tops = []
        for k in range(4 + vi):     # galhos que abrem em guarda-chuva (pinheiro-manso da costa)
            a = k / (4 + vi) * math.tau + rnd.uniform(-0.3, 0.3); zb = H * rnd.uniform(0.55, 0.78)
            b0 = Vector((0, 0, zb)) + lean * (zb / H)
            tip = b0 + Vector((math.cos(a) * H * 0.28, math.sin(a) * H * 0.28, H * rnd.uniform(0.12, 0.22)))
            parts.append(Z(org.limb(f"galho{k}", [(b0, 0.09), (b0.lerp(tip, 0.6) + Vector((0, 0, 0.15)), 0.06), (tip, 0.04)], subdiv=1), CASCA))
            tops.append(tip)
        cents = [(t + Vector((0, 0, 0.35)), (H * 0.2, H * 0.2, H * 0.09)) for t in tops]
        top = Vector((0, 0, H * 0.92)) + lean
        cents.append((top, (H * 0.24, H * 0.24, H * 0.1)))
        cr = crown_mass("copa", cents, vi); parts.append(Z(cr, FOLHA))
        for k in range(3):   # raizes saindo da areia
            a = k * 2.1 + vi
            parts.append(Z(org.limb(f"raiz{k}", [(Vector((0, 0, 0.25)), 0.16), (Vector((math.cos(a) * 0.6, math.sin(a) * 0.6, 0.02)), 0.06)], subdiv=1), CASCA))
        bp.finish(f"costa_pinheiro_{'abc'[vi]}", parts, size=1024, subdiv=1, extr=0.06, collision=[((0.6, 0.6, H * 0.7), (0, 0, H * 0.35), 0)])

def coqueiros():
    for vi in range(2):
        cz.reset(); cz.clear_mats(); rnd = random.Random(60 + vi)
        H = (7.0, 5.5)[vi]; parts = []
        bend = Vector((rnd.uniform(0.8, 1.4), rnd.uniform(-0.4, 0.4), 0))
        pts = [Vector((0, 0, 0)) + bend * (t * t) + Vector((0, 0, H * t)) for t in [i / 8 for i in range(9)]]
        parts.append(Z(org.limb("tronco", [(p, 0.2 - 0.08 * i / 8) for i, p in enumerate(pts)], subdiv=1), CASCA))
        for i in range(1, 16):   # aneis do tronco
            t = i / 16; p = Vector((0, 0, 0)) + bend * (t * t) + Vector((0, 0, H * t))
            parts.append(Z(cz.cyl(f"anel{i}", 0.2 - 0.08 * t + 0.02, 0.06, 10, loc=p, cap=False), DWOOD))
        crown = pts[-1]
        for k in range(15):
            a = k / 15 * math.tau + rnd.uniform(-0.2, 0.2); up = rnd.uniform(-0.2, 0.7) if k % 2 else rnd.uniform(0.3, 0.9)
            d = Vector((math.cos(a), math.sin(a), up))
            fp = curve_pts(crown, d, rnd.uniform(2.4, 3.2), 14, droop=rnd.uniform(1.2, 2.0), lift=0.6)
            w = [0.06 + 0.75 * math.sin(math.pi * min(1, i / 14 * 1.1)) * (1 - i / 17) for i in range(15)]
            parts.append(Z(ribbon(f"folha{k}", fp, w, serr=0.18, thick=0.012), FOLHA2 if k % 3 else FOLHA))
        for k in range(4):
            a = k * 1.6
            parts.append(Z(cz.ball(f"coco{k}", 0.13, 10, 8, loc=crown + Vector((math.cos(a) * 0.2, math.sin(a) * 0.2, -0.22))), CASCA))
        bp.finish(f"costa_coqueiro_{'ab'[vi]}", parts, size=1024, subdiv=1, extr=0.03, collision=[((0.5, 0.5, H * 0.7), (0, 0, H * 0.35), 0)])

def arbustos():
    for vi in range(3):
        cz.reset(); cz.clear_mats(); rnd = random.Random(80 + vi); parts = []; tips = []
        for k in range(9 + vi * 2):
            a = rnd.uniform(0, math.tau); el = rnd.uniform(0.6, 1.2); L = rnd.uniform(0.6, 1.1)
            d = Vector((math.cos(a) * math.cos(el), math.sin(a) * math.cos(el), math.sin(el)))
            tip = d * L
            parts.append(Z(org.limb(f"ramo{k}", [(Vector((0, 0, 0)), 0.04), (d * L * 0.5 + Vector((0, 0, 0.05)), 0.025), (tip, 0.01)], subdiv=1), CASCA))
            tips.append(tip)
        if vi < 2:
            cents = [(t * 0.75 + Vector((0, 0, 0.12)), (0.36, 0.36, 0.3)) for t in tips] + [(Vector((0, 0, 0.45)), (0.5, 0.5, 0.38))]
            parts.append(Z(crown_mass("folhas", cents, vi, voxel=0.03), FOLHA2 if vi == 0 else FOLHA))
        else:   # arbusto seco espinhento (sem folha): galhos finos extras
            for k, t in enumerate(tips):
                for j in range(3):
                    dd = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(0, 1))).normalized()
                    parts.append(Z(cz.seg(f"graveto{k}{j}", t * 0.8, t * 0.8 + dd * 0.3, 0.012, 0.0, 4), CASCA))
        bp.finish(f"costa_arbusto_{'abc'[vi]}", parts, size=512, subdiv=1, extr=0.02, target=3500)

def capins():
    for vi in range(3):
        cz.reset(); cz.clear_mats(); rnd = random.Random(100 + vi); parts = []
        n = (40, 52, 28)[vi]; Hh = (1.0, 1.25, 0.7)[vi]
        for k in range(n):
            a = rnd.uniform(0, math.tau); r0 = rnd.uniform(0, 0.12)
            b = Vector((math.cos(a) * r0, math.sin(a) * r0, 0))
            d = Vector((math.cos(a) * rnd.uniform(0.2, 0.6), math.sin(a) * rnd.uniform(0.2, 0.6), 1.0))
            L = Hh * rnd.uniform(0.6, 1.1)
            pts = curve_pts(b, d, L, 4, droop=L * rnd.uniform(0.1, 0.45))
            parts.append(Z(ribbon(f"lamina{k}", pts, [0.024, 0.022, 0.016, 0.009, 0.001], thick=0.003), JUNCO if (k % 4 == 0 or vi == 2) else FOLHA2))
            if vi == 1 and k % 7 == 0:   # espigas de capim-da-praia
                parts.append(Z(cz.ball(f"espiga{k}", 0.025, 6, 4, loc=pts[-1], scale=(1, 1, 3)), JUNCO))
        bp.finish(f"costa_capim_{'abc'[vi]}", parts, size=512, subdiv=0, extr=0.01)

def flores():
    for vi in range(2):
        cz.reset(); cz.clear_mats(); rnd = random.Random(120 + vi); parts = []
        for k in range(12):
            a = rnd.uniform(0, math.tau); r0 = rnd.uniform(0, 0.3)
            b = Vector((math.cos(a) * r0, math.sin(a) * r0, 0)); top = b + Vector((rnd.uniform(-0.08, 0.08), rnd.uniform(-0.08, 0.08), rnd.uniform(0.25, 0.45)))
            parts.append(Z(cz.seg(f"haste{k}", b, top, 0.008, 0.005, 4), FOLHA))
            for j in range(5):
                aa = j / 5 * math.tau
                parts.append(Z(cz.ball(f"petala{k}{j}", 0.03, 6, 4, loc=top + Vector((math.cos(aa) * 0.03, math.sin(aa) * 0.03, 0)), scale=(1, 0.6, 0.25)), FLOR_R if vi == 0 else FLOR_A))
            parts.append(Z(cz.ball(f"miolo{k}", 0.015, 6, 4, loc=top + Vector((0, 0, 0.01))), FLOR_A if vi == 0 else CASCA))
        for k in range(8):
            a = rnd.uniform(0, math.tau)
            pts = curve_pts(Vector((0, 0, 0)), Vector((math.cos(a), math.sin(a), 0.6)), 0.25, 3, droop=0.08)
            parts.append(Z(ribbon(f"folha{k}", pts, [0.02, 0.035, 0.025, 0.002], thick=0.003), FOLHA))
        bp.finish(f"costa_flores_{'ab'[vi]}", parts, size=512, subdiv=0, extr=0.01, target=3000)

# ------------------------------------------------------------------ praia, ponte, lago, ravina, horizonte
def praia():
    # algas encalhadas (monte molhado e brilhante)
    for vi in range(2):
        cz.reset(); cz.clear_mats(); rnd = random.Random(140 + vi); parts = []
        mound = org.fuse("monte", [org.blob(f"m{i}", (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.3, 0.3), 0.0), (rnd.uniform(0.3, 0.5), rnd.uniform(0.25, 0.4), 0.12)) for i in range(4)], voxel=0.03)
        org.displace(mound, 0.06, 0.15, 'CLOUDS', 2); org.decimate(mound, 1500)
        for v in mound.data.vertices: v.co.z = max(v.co.z, -0.02)
        parts.append(Z(mound, ALGA))
        for k in range(14):
            a = rnd.uniform(0, math.tau); L = rnd.uniform(0.5, 1.1)
            pts = [Vector((math.cos(a) * 0.2 * t + math.cos(a + 0.8 * math.sin(t * 5)) * L * t, math.sin(a) * 0.2 * t + math.sin(a + 0.8 * math.sin(t * 5)) * L * t, 0.08 * (1 - t) + 0.01)) for t in [i / 7 for i in range(8)]]
            parts.append(Z(ribbon(f"fita{k}", pts, [0.05, 0.06, 0.05, 0.05, 0.04, 0.03, 0.02, 0.005], side=Vector((0, 0, 1)).cross(pts[-1] - pts[0]).normalized() if False else None, thick=0.004), ALGA))
        for k in range(3):
            parts.append(Z(cz.ball(f"bolha{k}", 0.04, 6, 4, loc=(rnd.uniform(-0.4, 0.4), rnd.uniform(-0.3, 0.3), 0.12)), ALGA))
        bp.finish(f"costa_alga_{'ab'[vi]}", parts, size=512, subdiv=1, extr=0.02)
    # concha grande (espiral) e conchas pequenas
    cz.reset(); cz.clear_mats(); parts = []
    pts, rad = [], []
    for i in range(30):
        t = i / 29; a = t * math.tau * 2.2; r = 0.28 * (1 - t) ** 1.2 + 0.01
        pts.append((Vector((math.cos(a) * r * 0.6, t * 0.55 - 0.2, 0.18 + math.sin(a) * r * 0.5)), 0.2 * (1 - t) ** 1.1 + 0.01))
    sh = org.limb("espiral", pts, subdiv=1); parts.append(Z(sh, CONCHA))
    for i in range(8):
        a = i / 8 * math.tau * 2.2; r = 0.28 * (1 - i / 8 * 0.4)
        parts.append(Z(cz.seg(f"ponta{i}", (math.cos(a) * r * 0.6, i / 29 * 0.55 * 3 - 0.2, 0.18 + math.sin(a) * r * 0.5), (math.cos(a) * r * 0.95, i / 29 * 0.55 * 3 - 0.2, 0.18 + math.sin(a) * r * 0.8), 0.04, 0.0, 5), CONCHA))
    parts.append(Z(cz.ball("boca", 0.16, 10, 8, loc=(0.0, -0.3, 0.12), scale=(1.1, 0.4, 0.8)), FLOR_R))
    bp.finish("costa_concha_grande", parts, size=512, subdiv=1, extr=0.02)
    # troncos de deriva (madeira lavada, clara)
    for vi in range(2):
        cz.reset(); cz.clear_mats(); rnd = random.Random(160 + vi); parts = []
        L = (3.2, 2.2)[vi]
        main = [(Vector((-L / 2, 0, 0.18)), 0.2), (Vector((-L / 6, 0.1, 0.2)), 0.17), (Vector((L / 6, -0.05, 0.17)), 0.15), (Vector((L / 2, 0.05, 0.12)), 0.09)]
        parts.append(Z(org.limb("tronco", main, subdiv=1), DERIVA))
        for k in range(3 + vi):
            b = Vector((rnd.uniform(-L / 3, L / 3), 0, 0.2)); d = Vector((rnd.uniform(-0.6, 0.6), rnd.choice([-1, 1]) * rnd.uniform(0.5, 1), rnd.uniform(0.0, 0.8))).normalized()
            parts.append(Z(org.limb(f"galho{k}", [(b, 0.07), (b + d * rnd.uniform(0.5, 0.9), 0.02)], subdiv=1), DERIVA))
        for k in range(4):   # raiz exposta numa ponta
            a = k * 1.4
            parts.append(Z(org.limb(f"raiz{k}", [(main[0][0], 0.1), (main[0][0] + Vector((-0.4, math.cos(a) * 0.5, math.sin(a) * 0.4 + 0.1)), 0.02)], subdiv=1), DERIVA))
        bp.finish(f"costa_tronco_{'ab'[vi]}", parts, size=512, subdiv=1, extr=0.03)
    # seixos
    cz.reset(); cz.clear_mats(); parts = []; rnd = random.Random(170)
    for k in range(14):
        a = rnd.uniform(0, math.tau); rr = rnd.uniform(0, 0.8)
        p = cz.ico(f"s{k}", rnd.uniform(0.06, 0.2), 2, loc=(math.cos(a) * rr, math.sin(a) * rr, 0.02), scale=(1.3, 1, 0.55), rot=(0, 0, a)); cz.apply_tf(p); cz.jitter(p, 0.01, k)
        parts.append(Z(p, STONE if k % 3 else CAL))
    bp.finish("costa_seixos", parts, size=512, subdiv=1, extr=0.02)

def ponte():
    """costa_ponte: tabuado de 9,6 m (X, largura do vao do portao 'ponte_ravina') por 5 m (Y, atravessa a ravina de 2,4 m)."""
    cz.reset(); cz.clear_mats(); parts = []; rnd = random.Random(180)
    W, L = 9.6, 5.0
    for i in range(int(L / 0.32)):
        y = -L / 2 + 0.16 + i * 0.32
        parts.append(Z(cz.box(f"tabua{i}", (W * rnd.uniform(0.94, 1.0), 0.28, 0.07), loc=(rnd.uniform(-0.1, 0.1), y, 0.32 + rnd.uniform(-0.01, 0.01)), rot=(0, rnd.uniform(-0.01, 0.01), rnd.uniform(-0.008, 0.008))), WOOD if i % 3 else DWOOD))
    for x in (-W / 2 + 0.3, -1.6, 1.6, W / 2 - 0.3):
        parts.append(Z(cz.box(f"viga{x}", (0.25, L + 0.4, 0.22), loc=(x, 0, 0.17)), DWOOD))
    for sx in (-1, 1):
        for y in (-L / 2 + 0.2, 0, L / 2 - 0.2):
            parts.append(Z(cz.cyl(f"poste{sx}{y}", 0.1, 1.3, 8, loc=(sx * (W / 2 - 0.15), y, 0.9)), DWOOD))
        parts.append(Z(cz.seg(f"corda{sx}", (sx * (W / 2 - 0.15), -L / 2 + 0.2, 1.4), (sx * (W / 2 - 0.15), L / 2 - 0.2, 1.4), 0.03, 0.03, 6), ROPE))
        parts.append(Z(cz.seg(f"corda2{sx}", (sx * (W / 2 - 0.15), -L / 2 + 0.2, 0.95), (sx * (W / 2 - 0.15), L / 2 - 0.2, 0.95), 0.025, 0.025, 6), ROPE))
    for sy in (-1, 1):     # cabeceiras de pedra de cal
        parts.append(Z(cz.box(f"cabeceira{sy}", (W + 0.6, 0.8, 0.4), loc=(0, sy * (L / 2 + 0.2), 0.12)), CAL))
    bp.finish("costa_ponte", parts, size=1024, subdiv=2, extr=0.02)

def ravina():
    """costa_ravina_trecho: 10 m (X) x 2,4 m (Y). Fenda escura com bordas de cal quebradas, desenhada SOBRE o chao (o chao do jogo e um plano)."""
    cz.reset(); cz.clear_mats(); parts = []; rnd = random.Random(190)
    for sy in (-1, 1):
        for k in range(14):
            x = -5.2 + k * 0.8 + rnd.uniform(-0.2, 0.2)
            p = cz.ico(f"borda{sy}{k}", rnd.uniform(0.5, 0.85), 2, loc=(x, sy * rnd.uniform(1.3, 1.6), 0.0), scale=(1.3, 0.9, 0.6), rot=(0, 0, rnd.uniform(-0.3, 0.3))); cz.apply_tf(p); cz.jitter(p, 0.06, k + sy)
            parts.append(Z(p, CAL))
    fundo = cz.box("fundo", (10.6, 2.3, 0.02), loc=(0, 0, 0.012))
    paredes = []
    for sy in (-1, 1):
        w_ = cz.box(f"parede{sy}", (10.6, 0.05, 0.6), loc=(0, sy * 1.12, -0.25), rot=(sy * 0.5, 0, 0)); paredes.append(w_)
    ink = cz.mat("ravina_fundo", "#1c140e", rough=1.0)
    bp.finish("costa_ravina_trecho", parts, size=1024, subdiv=1, extr=0.04, extra=[(fundo, ink)] + [(w_, ink) for w_ in paredes])

def lago():
    """costa_lago: lagoa de 14 x 10 m. Agua translucida (sem shader proprio), margem de areia molhada, pedras e juncos.
    Colisao: elipse aproximada por 3 caixas (agua nao se atravessa)."""
    cz.reset(); cz.clear_mats(); parts = []; rnd = random.Random(200)
    A, B = 7.0, 5.0
    def edge(a, k=1.0):
        r = 1 + 0.12 * math.sin(3 * a + 1) + 0.07 * math.sin(5 * a + 2)
        return Vector((math.cos(a) * A * r * k, math.sin(a) * B * r * k, 0))
    bm = bmesh.new(); n = 64
    inner = [bm.verts.new(edge(i / n * math.tau, 1.0) + Vector((0, 0, 0.02))) for i in range(n)]
    outer = [bm.verts.new(edge(i / n * math.tau, 1.25) + Vector((0, 0, 0.005))) for i in range(n)]
    for i in range(n): bm.faces.new((inner[i], outer[i], outer[(i + 1) % n], inner[(i + 1) % n]))
    me = bpy.data.meshes.new("margem"); bm.to_mesh(me); bm.free()
    margem = cz._link(bpy.data.objects.new("margem", me)); parts.append(Z(margem, AREIA_M))
    bm = bmesh.new(); rings = []
    for kk, (sc, zz) in enumerate(((1.0, 0.022), (0.8, 0.016), (0.55, 0.012), (0.3, 0.01))):
        rings.append([bm.verts.new(edge(i / n * math.tau, sc) + Vector((0, 0, zz))) for i in range(n)])
    for ra, rb in zip(rings, rings[1:]):
        for i in range(n): bm.faces.new((ra[i], ra[(i + 1) % n], rb[(i + 1) % n], rb[i]))
    c = bm.verts.new((0, 0, 0.01))
    for i in range(n): bm.faces.new((c, rings[-1][(i + 1) % n], rings[-1][i]))
    me = bpy.data.meshes.new("leito"); bm.to_mesh(me); bm.free()
    leito = cz._link(bpy.data.objects.new("leito", me)); parts.append(Z(leito, LODO))
    for k in range(16):
        a = rnd.uniform(0, math.tau); p0 = edge(a, rnd.uniform(1.02, 1.2))
        p = cz.ico(f"pedra{k}", rnd.uniform(0.2, 0.55), 2, loc=p0, scale=(1.3, 1, 0.6), rot=(0, 0, a)); cz.apply_tf(p); cz.jitter(p, 0.04, k)
        parts.append(Z(p, CAL if k % 3 else STONE))
    for k in range(11):    # touceiras de junco na beira
        a = rnd.uniform(0, math.tau); b0 = edge(a, rnd.uniform(0.92, 1.05))
        for j in range(12):
            aa = rnd.uniform(0, math.tau); L = rnd.uniform(0.9, 1.7)
            d = Vector((math.cos(aa) * 0.15, math.sin(aa) * 0.15, 1)); pts = curve_pts(b0 + Vector((math.cos(aa) * 0.1, math.sin(aa) * 0.1, 0)), d, L, 4, droop=L * 0.12)
            parts.append(Z(ribbon(f"junco{k}{j}", pts, [0.02, 0.019, 0.016, 0.01, 0.002], thick=0.003), JUNCO))
            if j % 4 == 0: parts.append(Z(cz.ball(f"taboa{k}{j}", 0.03, 6, 4, loc=pts[-2], scale=(1, 1, 3.2)), CASCA))
    bm = bmesh.new(); c = bm.verts.new((0, 0, 0.06)); ring = [bm.verts.new(edge(i / n * math.tau, 1.0) + Vector((0, 0, 0.06))) for i in range(n)]
    for i in range(n): bm.faces.new((c, ring[i], ring[(i + 1) % n]))
    me = bpy.data.meshes.new("agua"); bm.to_mesh(me); bm.free(); agua = cz._link(bpy.data.objects.new("agua", me))
    wm = bpy.data.materials.new("costa_agua"); wm.use_nodes = True; bs = wm.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = (0.03, 0.13, 0.15, 1); bs.inputs["Roughness"].default_value = 0.03; bs.inputs["Alpha"].default_value = 0.62
    wm.blend_method = 'BLEND' if hasattr(wm, "blend_method") else None
    try: wm.surface_render_method = 'BLENDED'
    except Exception: pass
    bp.finish("costa_lago", parts, size=1024, subdiv=1, extr=0.03, extra=[(agua, wm)],
              collision=[((11.0, 6.0, 1.0), (0, 0, 0.5), 0), ((7.5, 9.0, 1.0), (0, 0, 0.5), 0)])

def horizonte():
    """costa_horizonte_a/b: silhuetas distantes de mesas de cal (60 x 30 m), para o anel alem das bordas (120-200 m do centro)."""
    for vi in range(2):
        cz.reset(); cz.clear_mats(); rnd = random.Random(210 + vi)
        bl = []
        for k in range(10):
            x = -28 + k * 6.2 + rnd.uniform(-2, 2); hh = rnd.uniform(8, 22) * (1.3 if k in (3, 6) else 1)
            bl.append(org.blob(f"m{k}", (x, rnd.uniform(-2, 4), hh * 0.45), (rnd.uniform(5, 8), rnd.uniform(4, 7), hh * 0.55)))
        o = org.fuse("serra", bl, voxel=0.7)
        org.displace(o, 2.5, 14, 'CLOUDS', 2)
        strata(o, 3.2, (0.04, 0.0), 0.8, 0.4, vi)
        org.displace(o, 0.8, 5, 'CLOUDS', 2)
        for v in o.data.vertices: v.co.z = max(v.co.z, -1.0)
        org.decimate(o, 3500)
        bp.finish(f"costa_horizonte_{'ab'[vi]}", [Z(o, CAL)], size=1024, subdiv=0, extr=0.5)


G = dict(rochas=rochas, penhascos=penhascos, pinheiros=pinheiros, coqueiros=coqueiros, arbustos=arbustos, capins=capins, flores=flores, praia=praia, ponte=ponte, ravina=ravina, lago=lago, horizonte=horizonte)
if __name__ == "__main__":
    w = sys.argv[-1]
    for k, f in G.items():
        if w in ("all", k): f()

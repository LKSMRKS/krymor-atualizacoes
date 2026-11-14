"""Peças que eram formas geométricas cruas: tocha de chão, moita de erva, veio de ferro, árvore de tábua, placa,
tronco de praia e tenda de lona. Materiais compartilhados (madeira, casca, pedra, ferro, lona) assados a 512."""
import sys; sys.path.insert(0, "/home/claude/cinzas/tapa"); sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh, math, random
from mathutils import Vector
import itemkit as K, kit_folha as F
OUT = "/home/claude/cinzas/tapa/out/"
def rv(r, a, b): return r.uniform(a, b)

# ---------------- materiais do kit (os mesmos para todas as peças)
def madeira(): return K.m_wood("madeira", "#7a5434", "#2e1b0e", 3.0)
def madeira_velha(name="madeira_velha", base="#7d6248", dark="#3b2a1c", light="#a98d6c"):
    b = K.NB(name)
    vec = b.n("ShaderNodeMapping"); b.l(b.obj(), vec.inputs["Vector"]); vec.inputs["Scale"].default_value = (0.08, 1.6, 1.6)
    grain = b.noise(14, 8, .62, vec=vec.outputs[0], dist=0.6); n1 = b.noise(6, 4)
    fenda = b.math('LESS_THAN', b.noise(9, 3, .5, vec=vec.outputs[0]), .3)
    col = b.ramp(b.math('ADD', b.math('MULTIPLY', grain, .75), b.math('MULTIPLY', n1, .25)), [(.3, dark), (.55, base), (.8, light)])
    col = b.mix(b.math('MULTIPLY', fenda, .7), col, "#1c120a")
    col = b.mix(b.math('MULTIPLY', K.edge_mask(b, .5, .55), .5, clamp=True), col, light)
    col = b.mix(b.math('MULTIPLY', K.cavity(b, .05), 1.5, clamp=True), col, "#140c06")
    return b.set(base=col, rough=.85, normal=b.bump(b.math('SUBTRACT', grain, b.math('MULTIPLY', fenda, .6)), .8, .01))
def casca(): return F.m_casca("casca", "#4f3e30", "#1f1610", "#80705f", 2.2)
def casca_branca(): return madeira_velha("madeira_praia", "#827a6e", "#3c3731", "#b7ae9f")
def pedra(): return K.m_rock("pedra", "#6c655c", "#a59d90", 2.5, flecks="#8a8378")
def ferro(): return K.m_iron("ferro", 2.0)
def lona():
    b = K.NB("lona")
    trama = b.wave(160, 1, 1, 'BANDS', 'X'); trama2 = b.wave(160, 1, 1, 'BANDS', 'Y')
    n1 = b.noise(5, 6); suj = b.noise(2.5, 4)
    col = b.ramp(n1, [(.3, "#8d7c5e"), (.7, "#b8a682")])
    col = b.mix(b.math('MULTIPLY', b.math('SUBTRACT', suj, .45), 2.2, clamp=True), col, "#5a4a36")
    col = b.mix(b.math('MULTIPLY', K.cavity(b, .08), 1.4, clamp=True), col, "#2a2117")
    return b.set(base=col, rough=.95, normal=b.bump(b.math('ADD', trama, trama2), .25, .003))
def corda(): return K.m_plain("corda", "#9b8358", .9, bumpscale=60)

def disp(o, s, size, seed=0, kind='CLOUDS'):
    K.subsurf(o, 2); K.displace(o, s, size, kind=kind, seed=seed); K.apply_all(o)

def bloco(name, size, loc, rot, mat, bev=.02):
    bpy.ops.mesh.primitive_cube_add(size=1); o = bpy.context.object; o.name = name
    o.scale = size; o.location = loc; o.rotation_euler = [math.radians(a) for a in rot]
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    K.bevel(o, bev, 2); o.data.materials.append(mat); return o

def salvar(nome, objs, folhas=(), tex=512, tris=4000, emit=False):
    F.exportar(OUT + nome + ".glb", objs, list(folhas), tex=tex, tris_tronco=tris, emit=emit)

# ---------------- peças
def tocha_chao():
    K.reset(); mw, mf, mp = madeira_velha(), ferro(), pedra(); objs = []
    pole = F.tronco("haste", [(0, 0, 0), (0.01, 0, .9), (0, 0.01, 1.74)], .07, .05, F.m_casca("casca_tocha", "#5a4430", "#21170f", "#8a735c", 4.0), res=10)
    objs.append(pole)
    for z in (.55, 1.2, 1.62):   # cintas de ferro
        o = K.lathe(f"cinta{z}", [(.072, z - .025), (.078, z - .02), (.078, z + .02), (.072, z + .025)], 16, mf); objs.append(o)
    # cesto de ferro: anel + 6 garras abertas
    objs.append(K.lathe("taça", [(.05, 1.70), (.11, 1.76), (.13, 1.80), (.12, 1.81), (.09, 1.76), (.04, 1.73)], 16, mf, smooth=False))
    for i in range(6):
        a = i * math.tau / 6
        objs.append(K.tube(f"garra{i}", [(math.cos(a) * .11, math.sin(a) * .11, 1.78), (math.cos(a) * .16, math.sin(a) * .16, 1.9), (math.cos(a) * .13, math.sin(a) * .13, 1.97)], 1, mf, res=4, taper=[.012, .01, .004]))
    # pano com piche enrolado (escuro, brilho de brasa embaixo da chama)
    b = K.NB("piche"); n = b.noise(18, 6)
    em = b.math('MULTIPLY', b.math('GREATER_THAN', b.noise(9, 3), .62), 3.0)
    pano = b.set(base=b.ramp(n, [(.3, "#120c08"), (.8, "#3a2a1a")]), rough=.6, emit_col="#ff6a1a", emit=em)
    o = K.lathe("pano", [(.0, 1.76), (.09, 1.77), (.105, 1.84), (.09, 1.92), (.0, 1.94)], 12, pano); disp(o, .015, .05, 3); objs.append(o)
    # pedras na base
    r = random.Random(3)
    for i in range(6):
        a = i * math.tau / 6 + rv(r, -.3, .3)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=rv(r, .08, .13), location=(math.cos(a) * .17, math.sin(a) * .17, .04))
        s = bpy.context.object; s.scale = (1, rv(r, .7, 1), rv(r, .5, .7)); s.data.materials.append(mp); disp(s, .03, .1, i); objs.append(s)
    salvar("cenario_tocha_chao", objs, tris=3000, emit=True)

def moita_erva():
    K.reset(); r = random.Random(5)
    terra = K.m_plain("terra_erva", "#4a3826", .95, bumpscale=25)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.5, location=(0, 0, -.32)); m = bpy.context.object; m.scale = (1.1, 1.0, .75)
    m.data.materials.append(terra); disp(m, .05, .15, 2)
    fo = F.Folhas()
    for i in range(16):
        a = rv(r, 0, math.tau); rr = rv(r, 0, .3)
        p = Vector((math.cos(a) * rr, math.sin(a) * rr, -.02))
        d = Vector((math.cos(a) * rv(r, .1, .5), math.sin(a) * rv(r, .1, .5), 1)).normalized()
        fo.card(p, d, rv(r, .75, 1.0), roll=rv(r, 0, math.tau), centro=(0, 0, .2))
    f = fo.objeto("erva_folhas", F.m_folha("erva_tonico", "erva_tonico.png"), (0, 0, .15), up_mix=.55)
    salvar("cenario_moita_erva", [m], [f], tris=800)

def veio_ferro():
    """veio de minério (igual ao ícone do item): rocha escura lascada com cristais azul-gelo saindo da superfície"""
    from mathutils.bvhtree import BVHTree
    K.reset(); r = random.Random(8)
    mat = K.m_rock("rocha_veio", "#2c2e36", "#5b5f6c", 2.2, flecks="#7fb8e0")
    objs = []
    for i, (x, y, s, h_) in enumerate([(0, 0, .55, .6), (.5, .22, .34, .38), (-.46, .24, .3, .3), (.12, -.44, .28, .24)]):
        bm = bmesh.new()
        for k in range(26):
            u = rv(r, 0, math.tau); v = math.acos(rv(r, -.3, 1))
            bm.verts.new((x + math.sin(v) * math.cos(u) * s * rv(r, .8, 1.1), y + math.sin(v) * math.sin(u) * s * rv(r, .8, 1.1), max(-.05, math.cos(v) * h_ * rv(r, .85, 1.1))))
        bmesh.ops.convex_hull(bm, input=bm.verts)
        o = K.obj_from_bm(bm, f"rocha{i}", mat, smooth=False); K.bevel(o, .02, 1, 30); K.apply_all(o); objs.append(o)
    deps = bpy.context.evaluated_depsgraph_get()
    trees = [BVHTree.FromObject(o, deps) for o in objs]
    def hit(x, y):
        best = None
        for t in trees:
            h = t.ray_cast(Vector((x, y, 3)), Vector((0, 0, -1)))
            if h[0] is not None and (best is None or h[0].z > best[0].z): best = h
        return best
    cr = K.m_crystal("cristal_minerio", "#9fdcff", "#5ab8ff", 2.2)
    cristais = []
    grupos = [((.0, .0), .28, 1.0, 7), ((.5, .22), .16, .65, 4), ((-.46, .24), .14, .55, 3), ((.12, -.44), .12, .45, 3)]
    for gi, (c, spread, k, n) in enumerate(grupos):
        for j in range(n):
            px, py = c[0] + rv(r, -spread, spread) * (0 if j == 0 else 1), c[1] + rv(r, -spread, spread) * (0 if j == 0 else 1)
            h = hit(px, py)
            if h is None: continue
            pos, nrm = h[0], h[1]
            hh = rv(r, .22, .42) * k * (1.5 if j == 0 else 1); rr = hh * rv(r, .17, .23)
            o = K.crystal(f"cr{gi}{j}", cr, rr, hh, .35, 6, seed=gi * 10 + j)
            d = (nrm * .6 + Vector((0, 0, 1)) * .4 + Vector((rv(r, -.3, .3), rv(r, -.3, .3), 0))).normalized()
            o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d)
            o.location = pos - d * hh * .15; cristais.append(o)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in cristais: o.select_set(True)
    bpy.context.view_layer.objects.active = cristais[0]; bpy.ops.object.join(); cj = bpy.context.view_layer.objects.active; cj.name = "cenario_veio_ferro_cristais"
    m2 = K.simple_copy(cr); m2.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = K.lin("#8fd2ff"); cj.data.materials.clear(); cj.data.materials.append(m2)
    F.exportar(OUT + "cenario_veio_ferro.glb", objs, [], tex=512, tris_tronco=1500, extra=[cj])

def arvore_tabua():
    K.reset(); r = random.Random(13); mc, mw, mf = casca(), madeira(), ferro(); objs = []
    H = 4.2
    pts = [(0, 0, 0), (.1, .05, 1.2), (-.1, .12, 2.4), (.15, 0, 3.4), (.05, -.1, H)]
    objs.append(F.tronco("tronco", pts, .3, .1, mc, res=12))
    for i in range(4):   # raízes
        a = i * math.tau / 4 + .4
        objs.append(F.tronco(f"r{i}", [(0, 0, .5), (math.cos(a) * .4, math.sin(a) * .4, .12), (math.cos(a) * .8, math.sin(a) * .8, -.06)], .16, .04, mc, res=6))
    for i in range(6):   # galhos tortos (alguns quebrados)
        z = rv(r, 2.2, 3.9); a = i * math.tau / 6 + rv(r, -.4, .4); L = rv(r, .9, 1.7) * (.5 if i % 3 == 2 else 1)
        p0 = Vector((0, 0, z)); p1 = p0 + Vector((math.cos(a) * L * .5, math.sin(a) * L * .5, L * .45))
        p2 = p1 + Vector((math.cos(a + .5) * L * .5, math.sin(a + .5) * L * .5, L * rv(r, -.05, .35)))
        objs.append(F.tronco(f"g{i}", [tuple(p0), tuple(p1), tuple(p2)], .09, .015, mc, res=6))
    # toco com machado cravado
    toco = K.lathe("toco", [(.38, 0), (.36, .1), (.33, .45), (.0, .47)], 16, mc); disp(toco, .02, .1, 2); objs.append(toco)
    toco.location = (1.05, .4, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=.33, depth=.02, location=(1.05, .4, .475)); topo = bpy.context.object
    topo.data.materials.append(K.m_wood("anel_toco", "#b08458", "#5a3a20", 6.0)); objs.append(topo)
    cabo = F.tronco("cabo", [(1.0, .4, .5), (.95, .4, .85), (.88, .4, 1.15)], .028, .024, mw, res=6); objs.append(cabo)
    lam = bloco("lamina", (.24, .03, .14), (1.06, .4, .52), (0, 0, -18), mf, .008); objs.append(lam)
    for i in range(5):   # cavacos no chão
        objs.append(bloco(f"cavaco{i}", (rv(r, .08, .14), rv(r, .04, .07), .015), (1.05 + rv(r, -.6, .6), .4 + rv(r, -.6, .6), .01), (0, 0, rv(r, 0, 180)), mw, .004))
    salvar("cenario_arvore_tabua", objs, tex=1024, tris=6000)

def placa():
    K.reset(); mw, mf = madeira_velha(), ferro(); objs = []
    objs.append(F.tronco("poste", [(0, 0, 0), (0, 0, 1.5)], .065, .055, F.m_casca("casca_placa", "#5a4430", "#21170f", "#8a735c", 4), res=8))
    objs.append(K.lathe("ponta", [(.06, 1.5), (.0, 1.6)], 8, mw))
    t = bloco("tabua", (.95, .06, .34), (0, .08, 1.2), (0, 0, 2), mw, .012)
    # ranhuras: fendas na tábua
    bm = bmesh.new(); bm.from_mesh(t.data); bm.free(); disp(t, .006, .04, 7); objs.append(t)
    t2 = bloco("tabua2", (.7, .05, .16), (.08, .07, .9), (0, 0, -4), mw, .01); disp(t2, .005, .04, 8); objs.append(t2)
    for (x, z) in ((-.38, 1.32), (.38, 1.28), (-.38, 1.1), (.38, 1.1), (-.22, .92), (.36, .92)):   # pregos
        objs.append(K.lathe(f"p{x}{z}", [(.0, 0), (.014, 0), (.014, .006), (.0, .008)], 8, mf)); objs[-1].rotation_euler = (math.radians(-90), 0, 0); objs[-1].location = (x, .115, z)
    salvar("cenario_placa", objs, tris=2500)

def tronco_praia(nome, seed, L=3.6):
    K.reset(); r = random.Random(seed); m = casca_branca(); objs = []
    pts = [(-L / 2, 0, .3), (-L / 6, rv(r, -.2, .2), .34), (L / 6, rv(r, -.2, .2), .3), (L / 2, rv(r, -.3, .3), .22)]
    o = F.tronco("tronco", pts, .32, .16, m, res=12); disp(o, .05, .25, seed); objs.append(o)
    for i in range(5):   # raízes abertas na ponta grossa
        a = i * math.tau / 5 + rv(r, -.3, .3); ln = rv(r, .5, 1.0)
        p0 = Vector((-L / 2 + .1, 0, .3)); p1 = p0 + Vector((-ln * .5, math.cos(a) * ln * .5, math.sin(a) * ln * .5))
        p2 = p1 + Vector((-ln * .5, math.cos(a) * ln * .7, max(-.25, math.sin(a) * ln * .6)))
        objs.append(F.tronco(f"r{i}", [tuple(p0), tuple(p1), tuple(p2)], .1, .015, m, res=6))
    for i in range(2):   # galhos quebrados
        x = rv(r, -.3, 1.2); objs.append(F.tronco(f"g{i}", [(x, 0, .45), (x + .3, rv(r, -.3, .3), .9)], .06, .03, m, res=6))
    for o in objs: o.location.z -= .06
    salvar(nome, objs, tex=1024, tris=3500)

def tenda():
    K.reset(); ml, mw, mc = lona(), madeira_velha(), corda(); objs = []
    W, L, H = 2.4, 2.8, 2.1
    bm = bmesh.new()
    nx, ny = 10, 12
    rows = []
    for j in range(ny + 1):
        y = -L / 2 + L * j / ny; row = []
        for i in range(nx + 1):
            t = i / nx; x = -W / 2 + W * t; z = H * (1 - abs(2 * t - 1))
            sag = .06 * math.sin(math.pi * t) * math.sin(math.pi * j / ny)   # lona cede entre as varas
            row.append(bm.verts.new((x * (1 + .04 * math.sin(j)), y, max(.02, z - sag))))
        rows.append(row)
    for j in range(ny):
        for i in range(nx): bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    # fundo fechado
    back = [rows[0][i] for i in range(nx + 1)]; bm.faces.new(back)
    o = K.obj_from_bm(bm, "lona", ml); mod = o.modifiers.new("s", 'SOLIDIFY'); mod.thickness = .02; K.apply_all(o)
    disp(o, .03, .3, 4); objs.append(o)
    # aba da frente meio aberta
    bm = bmesh.new(); a0 = bm.verts.new((0, L / 2, H)); a1 = bm.verts.new((-W / 2, L / 2, 0.02)); a2 = bm.verts.new((-.15, L / 2 + .55, .02)); bm.faces.new((a0, a1, a2))
    ab = K.obj_from_bm(bm, "aba", ml); mod = ab.modifiers.new("s", 'SOLIDIFY'); mod.thickness = .02; K.apply_all(ab); objs.append(ab)
    for y in (-L / 2 - .05, L / 2 + .05):   # varas em A + cumeeira
        for s in (-1, 1): objs.append(F.tronco(f"v{y}{s}", [(s * W / 2 * .95, y, 0), (0, y, H + .12)], .04, .035, mw, res=6))
    objs.append(F.tronco("cumeeira", [(0, -L / 2 - .15, H + .05), (0, L / 2 + .15, H + .05)], .035, .035, mw, res=6))
    for y in (-L / 2 - .05, L / 2 + .05):   # cordas e estacas
        for s in (-1, 1):
            e = (s * .4, y + (1 if y > 0 else -1) * 1.0, 0)
            objs.append(F.tronco(f"c{y}{s}", [(0, y, H + .1), e], .008, .008, mc, res=4))
            objs.append(F.tronco(f"e{y}{s}", [(e[0], e[1], -.05), (e[0], e[1], .22)], .025, .02, mw, res=5))
    remendo = bloco("remendo", (.5, .02, .4), (W / 4 + .05, -.2, H / 2 + .05), (0, -42, 0), F.m_casca("remendo_m", "#6e4e30", "#3a2816", "#8c6a46", 9, True), .005)
    objs.append(remendo)
    salvar("cenario_tenda_lona", objs, tex=1024, tris=5000)

if __name__ == "__main__":
    which = sys.argv[sys.argv.index("--") + 1]
    {"tocha": tocha_chao, "erva": moita_erva, "ferro": veio_ferro, "tabua": arvore_tabua, "placa": placa,
     "tronco_a": lambda: tronco_praia("costa_tronco_a", 31, 3.6), "tronco_b": lambda: tronco_praia("costa_tronco_b", 32, 2.8),
     "tenda": tenda}[which]()

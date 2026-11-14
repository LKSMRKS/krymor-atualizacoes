"""Peças cruas restantes (Porto e Costa): muro/muralha/ruína modulares, coluna, barricada, oficinas (caldeirão, panela,
bancada, armazém), horta (espantalho, feno, regadores) e enfeites da Costa (ossada, crânios, redes, barco encalhado,
acampamento abandonado, cerca quebrada). Mesmo kit de materiais do build_props."""
import sys; sys.path.insert(0, "/home/claude/cinzas/tools"); sys.path.insert(0, "/home/claude/cinzas/tapa")
import bpy, bmesh, math, random
from mathutils import Vector
import itemkit as K, kit_folha as F
from tapa_props import madeira_velha, casca, ferro, lona, corda, disp, bloco, rv
OUT = "/home/claude/cinzas/tapa/out/"

def cal(H=3.2):
    """calcário gasto: cor varia por bloco (atributo 'tinta'), sujeira e musgo subindo do chão, escorridos verticais"""
    b = K.NB("cal_bloco")
    vc = b.n("ShaderNodeVertexColor", layer_name="tinta")
    sep = b.n("ShaderNodeSeparateXYZ"); b.l(b.obj(), sep.inputs[0])
    z = b.math('DIVIDE', sep.outputs["Z"], H)
    vec = b.n("ShaderNodeMapping"); b.l(b.obj(), vec.inputs["Vector"]); vec.inputs["Scale"].default_value = (6, 6, .5)
    escorre = b.noise(3, 4, .5, vec=vec.outputs[0])
    n1 = b.noise(5, 8, .65); grao = b.noise(60, 3)
    col = b.ramp(n1, [(.3, "#716a5d"), (.7, "#a89f8c")])
    col = b.mix(1.0, col, vc.outputs["Color"], 'MULTIPLY')
    col = b.mix(b.math('MULTIPLY', b.math('SUBTRACT', 1, b.math('MULTIPLY', z, 2.2), clamp=True), .8), col, "#4a4234")       # barro no pé
    musgo = b.math('MULTIPLY', b.math('GREATER_THAN', b.noise(2.2, 4), .54), b.math('SUBTRACT', 1, b.math('MULTIPLY', z, 1.6), clamp=True))
    col = b.mix(b.math('MULTIPLY', musgo, .85), col, "#4a5628")
    col = b.mix(b.math('MULTIPLY', b.math('SUBTRACT', escorre, .5, clamp=True), .9), col, "#3c372e")                       # escorridos
    h = b.math('ADD', b.math('MULTIPLY', n1, .6), b.math('MULTIPLY', grao, .25))
    return b.set(base=col, rough=.9, normal=b.bump(h, .9, .015))

def tingir(objs, seed):
    r = random.Random(seed)
    for o in objs:
        k = r.uniform(.6, 1.08); w = r.uniform(-.05, .05)
        ca = o.data.color_attributes.new("tinta", 'FLOAT_COLOR', 'POINT')
        for d in ca.data: d.color = (k + w, k, k - w, 1)
def osso(): return K.m_bone("osso", "#ddd0b0", "#7a5e38", 3.0)
def palha():
    b = K.NB("palha")
    vec = b.n("ShaderNodeMapping"); b.l(b.obj(), vec.inputs["Vector"]); vec.inputs["Scale"].default_value = (1, 1, 0.06)
    fio = b.noise(60, 4, .5, vec=vec.outputs[0]); n1 = b.noise(6, 4)
    col = b.ramp(b.math('ADD', b.math('MULTIPLY', fio, .7), b.math('MULTIPLY', n1, .3)), [(.3, "#6e5a2a"), (.55, "#b89a52"), (.8, "#e2c87e")])
    col = b.mix(b.math('MULTIPLY', K.cavity(b, .05), 1.4, clamp=True), col, "#2a1f0c")
    return b.set(base=col, rough=.95, normal=b.bump(fio, 1.0, .01))
def saco(): return K.m_plain("saco", "#a08a62", .95, bumpscale=90)

def exp(nome, objs, tex=1024, tris=5000, emit=False, extra=()):
    F.exportar(OUT + nome + ".glb", objs, [], tex=tex, tris_tronco=tris, emit=emit, extra=list(extra))

def esp(o, mat_emit_name, color, strength):
    """malha emissiva/especial fora do bake"""
    m = bpy.data.materials.new(mat_emit_name); m.use_nodes = True; bs = m.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = K.lin(color); bs.inputs["Emission Color"].default_value = K.lin(color); bs.inputs["Emission Strength"].default_value = strength
    o.data.materials.clear(); o.data.materials.append(m); return o

# ---------------------------------------------------------------- muros (módulo 4 m x 3,2 m x 1,2 m)
def parede(nome, seed, ruina=False, ameias=True):
    K.reset(); r = random.Random(seed); m = cal(); objs = []
    L, H, D = 4.0, 3.2, 1.2; ch = .4; nc = int(H / ch)
    for k in range(nc):
        top = H if not ruina else H * (0.45 + 0.55 * abs(math.sin(k * 1.7 + seed)))
        x = -L / 2 + (0 if k % 2 == 0 else -.35)
        while x < L / 2:
            bl = rv(r, .55, 1.0); x1 = min(L / 2, x + bl); xa = max(-L / 2, x)
            z = k * ch
            if ruina:
                cut = H * (0.35 + 0.65 * (0.5 + 0.5 * math.sin((xa + seed) * 1.3)))
                if z + ch > cut: x = x1; continue
            if x1 - xa > .08:
                o = bloco(f"b{k}_{x:.2f}", ((x1 - xa) - .025, D * rv(r, .96, 1.02), ch - .025), ((xa + x1) / 2, rv(r, -.015, .015), z + ch / 2), (0, 0, 0), m, .035)
                for v in o.data.vertices: v.co += Vector((rv(r, -.012, .012), rv(r, -.02, .02), rv(r, -.01, .01)))
                objs.append(o)
            x = x1
    if not ruina:
        objs.append(bloco("capa", (L, D + .12, .14), (0, 0, H + .07), (0, 0, 0), m, .03))
        if ameias:
            for i in range(3):
                x = -L / 2 + .55 + i * 1.45
                objs.append(bloco(f"am{i}", (.8, D * .9, .5), (x, 0, H + .14 + .25), (0, 0, 0), m, .04))
    else:
        for i in range(4):   # blocos caídos no pé
            objs.append(bloco(f"q{i}", (rv(r, .4, .8), rv(r, .4, .7), .35), (rv(r, -1.6, 1.6), rv(r, .8, 1.3) * (1 if i % 2 else -1), .17), (rv(r, -8, 8), rv(r, -8, 8), rv(r, 0, 90)), m, .04))
    # musgo/sujeira escorrida vem do material (cavidade); juntas escuras pelas frestas entre blocos
    nucleo = bloco("nucleo", (L - .02, D * .9, H * (0.98 if not ruina else 0.3)), (0, 0, H * (0.49 if not ruina else 0.15)), (0, 0, 0), m, .01)
    objs.append(nucleo)
    tingir(objs, seed)
    ca = nucleo.data.color_attributes["tinta"]
    for d in ca.data: d.color = (.32, .3, .27, 1)   # argamassa escura nas frestas
    exp(nome, objs, tex=1024, tris=4500)

def coluna():
    K.reset(); m = cal(4.2); objs = []
    objs.append(bloco("plinto", (1.6, 1.6, .35), (0, 0, .175), (0, 0, 0), m, .04))
    objs.append(K.lathe("base", [(.72, .35), (.72, .45), (.62, .5), (.6, .6)], 32, m))
    # fuste com caneluras: perfil em estrela extrudado
    bm = bmesh.new(); n = 40; rings = []
    for zi in range(9):
        z = .6 + zi * 3.0 / 8; rr = .55 - .05 * (zi / 8)
        ring = [bm.verts.new((math.cos(a) * rr * (1 - .06 * (i % 2)), math.sin(a) * rr * (1 - .06 * (i % 2)), z)) for i, a in enumerate(j * math.tau / n for j in range(n))]
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(n): bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    o = K.obj_from_bm(bm, "fuste", m, smooth=False); objs.append(o)
    objs.append(K.lathe("capitel", [(.5, 3.6), (.62, 3.7), (.75, 3.82), (.75, 3.9)], 32, m))
    objs.append(bloco("abaco", (1.6, 1.6, .2), (0, 0, 4.0), (0, 0, 0), m, .03))
    tingir(objs, 5)
    exp("cenario_coluna", objs, tex=1024, tris=4000)

def barricada():
    """módulo de 2 m: troncos apontados cruzados em X sobre uma viga, amarrados"""
    K.reset(); r = random.Random(4); mw = F.m_casca("casca_barr", "#5e4834", "#241a12", "#8f7a64", 3.0); mc = corda(); objs = []
    objs.append(F.tronco("viga", [(-1.05, 0, .55), (1.05, 0, .58)], .12, .11, mw, res=8))
    for i in range(4):
        x = -.8 + i * .53
        for s in (-1, 1):
            p0 = Vector((x, -s * .9, 0)); p1 = Vector((x + rv(r, -.1, .1), s * .9, 2.0 + rv(r, -.2, .2)))
            o = F.tronco(f"e{i}{s}", [tuple(p0), tuple(p0 + (p1 - p0) * .85)], .085, .07, mw, res=7); objs.append(o)
            tip = p0 + (p1 - p0) * .85; dirv = (p1 - p0).normalized()
            pt = K.lathe(f"ponta{i}{s}", [(.07, 0), (.0, .32)], 7, madeira_velha()); pt.rotation_mode = 'QUATERNION'
            pt.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(dirv); pt.location = tip; objs.append(pt)
        objs.append(F.tronco(f"c{i}", [(x - .05, -.1, .6), (x + .05, .1, .62)], .02, .02, mc, res=4))
    exp("cenario_barricada", objs, tex=1024, tris=4000)

# ---------------------------------------------------------------- oficinas do Porto
def caldeirao():
    K.reset(); r = random.Random(2); mf, mp = ferro(), K.m_rock("pedra_fogo", "#4e4a45", "#8a847b", 3, flecks="#6a645c"); objs = []
    objs.append(K.lathe("caldeirao", [(.25, .2), (.45, .26), (.56, .45), (.55, .7), (.48, .86), (.5, .9), (.47, .9), (.44, .84), (.5, .7), (.5, .45), (.4, .3), (.0, .26)], 32, mf))
    for i in range(3):
        a = i * math.tau / 3
        objs.append(F.tronco(f"pe{i}", [(math.cos(a) * .3, math.sin(a) * .3, .28), (math.cos(a) * .42, math.sin(a) * .42, 0)], .04, .03, mf, res=5))
    for s in (-1, 1):   # alças
        objs.append(K.tube(f"alca{s}", [(s * .5, -.08, .8), (s * .62, 0, .78), (s * .5, .08, .8)], 1, mf, res=5, taper=[.02, .02, .02]))
    for i in range(9):   # roda de pedras e lenha
        a = i * math.tau / 9
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=rv(r, .12, .17), location=(math.cos(a) * .72, math.sin(a) * .72, .06))
        s = bpy.context.object; s.scale = (1, 1, .6); s.data.materials.append(mp); disp(s, .03, .1, i); objs.append(s)
    for i in range(4):
        a = i * math.tau / 4 + .4
        objs.append(F.tronco(f"lenha{i}", [(math.cos(a) * .55, math.sin(a) * .55, .05), (math.cos(a) * .05, math.sin(a) * .05, .14)], .05, .04, casca(), res=6))
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.47, depth=.02, location=(0, 0, .8)); caldo = bpy.context.object
    esp(caldo, "caldo", "#6fe05a", 2.0)
    exp("cenario_caldeirao", objs, tex=1024, tris=4500, extra=[caldo])

def panela():
    K.reset(); mf = ferro(); mw = F.m_casca("casca_tripe", "#5e4834", "#241a12", "#8f7a64", 4.0); objs = []
    for i in range(3):   # tripé
        a = i * math.tau / 3
        objs.append(F.tronco(f"vara{i}", [(math.cos(a) * .75, math.sin(a) * .75, 0), (0, 0, 1.55)], .04, .03, mw, res=6))
    objs.append(F.tronco("corrente", [(0, 0, 1.5), (0, 0, 1.0)], .01, .01, mf, res=4))
    objs.append(K.lathe("panela", [(.0, .55), (.28, .58), (.35, .7), (.34, .88), (.3, .92), (.0, .92)], 24, mf))
    objs.append(K.tube("alca", [(-.32, 0, .9), (0, 0, 1.02), (.32, 0, .9)], 1, mf, res=4, taper=[.012, .012, .012]))
    r = random.Random(3)
    for i in range(7):
        a = i * math.tau / 7
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=.11, location=(math.cos(a) * .45, math.sin(a) * .45, .05))
        s = bpy.context.object; s.scale = (1, 1, .6); s.data.materials.append(K.m_rock("pedra_p", "#4e4a45", "#8a847b", 3)); objs.append(s)
    for i in range(3):
        a = i * math.tau / 3 + .5
        objs.append(F.tronco(f"lenha{i}", [(math.cos(a) * .35, math.sin(a) * .35, .04), (-math.cos(a) * .1, -math.sin(a) * .1, .12)], .04, .035, casca(), res=6))
    exp("cenario_panela", objs, tex=1024, tris=4000)

def bancada():
    K.reset(); r = random.Random(5); mw, mf = madeira_velha(), ferro(); objs = []
    objs.append(bloco("tampo", (2.0, .9, .1), (0, 0, .88), (0, 0, 0), mw, .015))
    for cx in (-.88, .88):
        for cz in (-.36, .36): objs.append(bloco(f"pe{cx}{cz}", (.1, .1, .84), (cx, cz, .42), (0, 0, 0), mw, .01))
        objs.append(bloco(f"trav{cx}", (.08, .8, .08), (cx, 0, .2), (0, 0, 0), mw, .01))
    objs.append(bloco("prat", (1.8, .7, .04), (0, 0, .22), (0, 0, 0), mw, .01))
    # morsa de ferro, serrote, martelo, tábuas e serragem
    objs.append(bloco("morsa", (.16, .2, .14), (.75, -.38, .99), (0, 0, 0), mf, .01))
    objs.append(F.tronco("fuso", [(.75, -.5, 1.0), (.75, -.62, 1.0)], .012, .012, mf, res=5))
    objs.append(bloco("serrote", (.55, .005, .12), (-.2, .1, .94), (90, 0, 15), mf, .002))
    objs.append(bloco("cabo_serrote", (.12, .03, .1), (-.5, .02, .94), (90, 0, 15), mw, .01))
    objs.append(F.tronco("cabo_martelo", [(.2, .2, .945), (.5, .3, .945)], .015, .015, mw, res=5))
    objs.append(bloco("cabeca_martelo", (.05, .14, .05), (.52, .31, .95), (0, 0, 18), mf, .006))
    for i in range(3): objs.append(bloco(f"tabua{i}", (1.4, .2, .03), (-.1, -.3, 1.0 + i * .035), (0, 0, rv(r, -4, 4)), mw, .006))
    for i in range(3): objs.append(bloco(f"apoio{i}", (.08, .7, 1.1), (1.05 + i * .06, .1, .55), (0, -14, 0), mw, .006))
    exp("cenario_bancada", objs, tex=1024, tris=4500)

def bau(o_name, loc, mw, mf, objs, rot=0):
    c = Vector(loc)
    objs.append(bloco(o_name + "c", (1.0, .62, .5), c + Vector((0, 0, .25)), (0, 0, rot), mw, .02))
    tampa = K.lathe(o_name + "t", [(.0, -.5), (.31, -.5), (.31, .5), (.0, .5)], 4, mw)   # placeholder, substituído abaixo
    bpy.data.objects.remove(tampa)
    bm = bmesh.new()
    for i in range(9):
        a = math.pi * i / 8
        for x in (-.5, .5): bm.verts.new((x, math.cos(a) * .31, math.sin(a) * .16))
    bm.verts.ensure_lookup_table()
    for i in range(8):
        v = bm.verts; bm.faces.new((v[i * 2], v[i * 2 + 1], v[i * 2 + 3], v[i * 2 + 2]))
    t = K.obj_from_bm(bm, o_name + "tampa", mw); mod = t.modifiers.new("s", 'SOLIDIFY'); mod.thickness = .03; K.apply_all(t)
    t.location = c + Vector((0, 0, .5)); t.rotation_euler = (0, 0, math.radians(rot)); objs.append(t)
    for x in (-.35, .35):
        objs.append(bloco(o_name + f"cinta{x}", (.06, .64, .52), c + Vector((x, 0, .26)), (0, 0, rot), mf, .005))
    objs.append(bloco(o_name + "fecho", (.12, .03, .14), c + Vector((0, -.32, .45)), (0, 0, rot), mf, .005))

def armazem():
    K.reset(); mw, mf, ms = madeira_velha(), ferro(), saco(); objs = []
    bau("b1", (-.7, 0, 0), mw, mf, objs); bau("b2", (.7, 0, 0), mw, mf, objs)
    objs.append(bloco("balcao", (2.6, .85, .06), (0, 0, .82 + .2), (0, 0, 0), mw, .01)) if False else None
    r = random.Random(7)
    for i in range(3):   # sacos de grão
        bpy.ops.mesh.primitive_uv_sphere_add(segments=14, ring_count=10, radius=.25, location=(-1.5 + i * .25, .55 + rv(r, -.1, .1), .25))
        s = bpy.context.object; s.scale = (1, .85, 1.25); s.data.materials.append(ms); disp(s, .03, .12, i); objs.append(s)
    for i in range(2):
        objs.append(K.lathe(f"barril{i}", [(.0, 0), (.24, 0), (.29, .3), (.24, .62), (.0, .62)], 18, mw)); objs[-1].location = (1.55, .5 - i * .55, 0)
    exp("cenario_armazem", objs, tex=1024, tris=5000)

# ---------------------------------------------------------------- horta
def espantalho():
    K.reset(); r = random.Random(9); mw, mp, ms = madeira_velha(), palha(), saco()
    trapo = K.m_plain("trapo", "#6b4a34", .95, bumpscale=80); xadrez = K.m_plain("camisa", "#7a3a2a", .95, bumpscale=70); objs = []
    objs.append(F.tronco("poste", [(0, 0, -.2), (0, 0, 2.2)], .05, .045, F.m_casca("casca_esp", "#5a4430", "#21170f", "#8a735c", 4), res=6))
    objs.append(F.tronco("trave", [(-.75, 0, 1.6), (.75, 0, 1.62)], .035, .035, mw, res=6))
    # camisa (casca de pano) e mangas
    cam = K.lathe("camisa", [(.0, 1.0), (.24, 1.02), (.28, 1.3), (.27, 1.6), (.12, 1.72), (.0, 1.72)], 14, xadrez); cam.scale = (1, .6, 1); disp(cam, .02, .15, 1); objs.append(cam)
    for s in (-1, 1):
        objs.append(F.tronco(f"manga{s}", [(s * .2, 0, 1.6), (s * .5, 0, 1.6), (s * .66, 0, 1.58)], .09, .07, xadrez, res=8))
        for k in range(5):   # palha saindo da manga
            objs.append(F.tronco(f"pm{s}{k}", [(s * .66, 0, 1.58), (s * (.8 + rv(r, 0, .08)), rv(r, -.06, .06), 1.58 + rv(r, -.08, .04))], .015, .003, mp, res=3))
    for k in range(8):   # palha na barra
        a = k * math.tau / 8
        objs.append(F.tronco(f"pb{k}", [(math.cos(a) * .2, math.sin(a) * .12, 1.02), (math.cos(a) * .26, math.sin(a) * .16, .82 + rv(r, -.05, .05))], .02, .004, mp, res=3))
    cab = K.lathe("cabeca", [(.0, 1.74), (.12, 1.76), (.17, 1.88), (.15, 2.0), (.0, 2.06)], 14, ms); disp(cab, .015, .08, 2); objs.append(cab)
    for s in (-1, 1): objs.append(bloco(f"olho{s}", (.05, .01, .05), (s * .06, -.155, 1.92), (0, 0, 45), K.m_plain("linha", "#1a120a", .9), .002))
    objs.append(bloco("boca", (.12, .01, .015), (0, -.15, 1.85), (0, 0, 0), K.m_plain("linha2", "#1a120a", .9), .002))
    objs.append(K.lathe("chapeu", [(.0, 2.0), (.36, 1.99), (.36, 2.01), (.14, 2.04), (.13, 2.2), (.0, 2.22)], 18, mp))
    exp("cenario_espantalho", objs, tex=1024, tris=5000)

def feno():
    K.reset(); mp, mc = palha(), corda(); objs = []
    for (x, z, rot) in ((0, .28, 0), (.12, .82, 16), (1.05, .28, 85)):
        b = bloco(f"fardo{x}{z}", (1.1, .6, .55), (x, 0, z), (0, 0, rot), mp, .06); disp(b, .015, .05, int(x * 10)); objs.append(b)
        for dx in (-.3, .3):
            c = bloco(f"fio{x}{z}{dx}", (.03, .62, .57), (x, 0, z), (0, 0, rot), mc, .008)
            c.location = Vector((x, 0, z)) + Vector((math.cos(math.radians(rot)) * dx, math.sin(math.radians(rot)) * dx, 0)); objs.append(c)
    exp("cenario_feno", objs, tex=1024, tris=4000)

def regadores():
    K.reset(); lata = K.m_metal("lata", "#b0a68c", .5, wear="#c9c2b0", dirt="#2b261c", scratch=.8, scale=4); objs = []
    for (x, y, rot) in ((0, 0, 0), (.48, .22, 50)):
        c = Vector((x, y, 0))
        corpo = K.lathe(f"corpo{x}", [(.0, 0), (.15, 0), (.16, .05), (.15, .32), (.12, .34), (.0, .34)], 18, lata); corpo.location = c; objs.append(corpo)
        a = math.radians(rot); d = Vector((math.cos(a), math.sin(a), 0))
        objs.append(K.tube(f"bico{x}", [tuple(c + d * .12 + Vector((0, 0, .1))), tuple(c + d * .3 + Vector((0, 0, .28))), tuple(c + d * .38 + Vector((0, 0, .38)))], 1, lata, res=6, taper=[.03, .022, .02]))
        crivo = K.lathe(f"crivo{x}", [(.0, 0), (.04, 0), (.045, .03), (.0, .035)], 10, lata); crivo.location = c + d * .4 + Vector((0, 0, .38)); objs.append(crivo)
        objs.append(K.tube(f"alca{x}", [tuple(c - d * .1 + Vector((0, 0, .3))), tuple(c + Vector((0, 0, .46))), tuple(c + d * .1 + Vector((0, 0, .3)))], 1, lata, res=4, taper=[.012, .012, .012]))
    exp("cenario_regadores", objs, tex=512, tris=3000)

# ---------------------------------------------------------------- enfeites da Costa
def cranio(name, mat, loc, rot, s=1.0):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=12, radius=.1 * s, location=loc)
    c = bpy.context.object; c.scale = (1, 1.2, 1); K.apply_all(c)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for v in c.data.vertices:
        if v.co.y < -.04 * s and v.co.z < 0: v.co.z *= .6; v.co.y *= .95   # face mais curta
    for (x, nm) in ((-.035 * s, "oe"), (.035 * s, "od"), (0, "nariz")):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=8, radius=(.03 if nm != "nariz" else .016) * s,
                                             location=(loc[0] + x, loc[1] - .105 * s, loc[2] + (.01 if nm != "nariz" else -.03) * s))
        cut = bpy.context.object
        mb = c.modifiers.new("b" + nm, 'BOOLEAN'); mb.object = cut; mb.operation = 'DIFFERENCE'; K.apply_all(c); bpy.data.objects.remove(cut)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(loc[0], loc[1] - .06 * s, loc[2] - .072 * s)); j = bpy.context.object
    j.scale = (.1 * s, .08 * s, .035 * s); K.bevel(j, .014 * s, 2)
    c.data.materials.clear(); c.data.materials.append(mat); j.data.materials.append(mat)
    for p in c.data.polygons: p.material_index = 0
    for o in (c, j): o.rotation_euler = [math.radians(a) for a in rot]
    return [c, j]

def cranios():
    K.reset(); mb = osso(); objs = []
    for i, (x, y, z, rz) in enumerate(((0, 0, .1, .0), (.22, .08, .1, .1), (-.18, .12, .1, -.12), (.05, .1, .27, .05))):
        objs += cranio(f"c{i}", mb, (x, y, z), (rv(random.Random(i), -15, 15), 0, rz * 300), 1.0)
    exp("cenario_cranios", objs, tex=512, tris=3000)

def ossada():
    K.reset(); r = random.Random(11); mb = osso(); objs = []
    objs += cranio("cr", mb, (.62, 0, .09), (0, 0, 80))
    objs.append(F.tronco("espinha", [(.5, 0, .05), (0, 0, .06), (-.4, 0, .05)], .03, .025, mb, res=6))
    for i in range(6):   # costelas curvas
        x = .38 - i * .1
        for s in (-1, 1):
            objs.append(K.tube(f"cost{i}{s}", [(x, 0, .07), (x - .02, s * .14, .14), (x - .04, s * .22, .03)], 1, mb, res=4, taper=[.014, .012, .008]))
    objs.append(bloco("pelve", (.16, .26, .06), (-.45, 0, .05), (0, 10, 0), mb, .03))
    for s in (-1, 1):
        objs.append(F.tronco(f"femur{s}", [(-.5, s * .1, .04), (-.9, s * .18 + rv(r, -.05, .05), .04)], .028, .025, mb, res=6))
        objs.append(F.tronco(f"tibia{s}", [(-.92, s * .18, .04), (-1.3, s * .25, .03)], .022, .02, mb, res=6))
        objs.append(F.tronco(f"umero{s}", [(.4, s * .2, .04), (.1, s * .38, .03)], .022, .02, mb, res=6))
    objs.append(bloco("trapo", (.5, .32, .01), (0, .3, .01), (0, 0, 20), K.m_plain("trapo_os", "#4a3626", .95, bumpscale=50), .003))
    mf = ferro()
    objs.append(bloco("espada", (.85, .05, .012), (-.1, -.42, .02), (0, 0, 60), mf, .004))
    objs.append(bloco("guarda", (.03, .2, .03), (.13, -.02, .03), (0, 0, 60), mf, .006))
    exp("cenario_ossada", objs, tex=1024, tris=5000)

def redes():
    K.reset(); mw = F.m_casca("casca_red", "#5e4834", "#241a12", "#8f7a64", 4.0); objs = []
    for x in (-1.4, 1.4): objs.append(F.tronco(f"poste{x}", [(x, 0, -.1), (x, 0, 2.0)], .06, .05, mw, res=6))
    objs.append(F.tronco("trave", [(-1.5, 0, 1.95), (1.5, 0, 1.95)], .035, .035, mw, res=6))
    # rede: textura de malha com alpha (pintada aqui)
    import numpy as np
    from PIL import Image, ImageDraw
    S = 512; img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    for i in range(0, S + 32, 32):
        d.line([(i, 0), (i - S, S)], fill=(150, 125, 85, 255), width=3); d.line([(i - S, 0), (i, S)], fill=(150, 125, 85, 255), width=3)
    img.save(F.TEX + "rede.png")
    fo = F.Folhas()
    pts = []; ws = []; ups = []
    for k in range(9):
        t = k / 8; x = -1.35 + 2.7 * t
        pts.append((x, .02, 1.25 - .18 * math.sin(math.pi * t))); ws.append(1.35); ups.append((0, .1, 1))
    fo.strip(pts, ws, ups)
    rede = fo.objeto("rede", F.m_folha("rede_m", "rede.png"), (0, -1, 1.2), up_mix=.2)
    boias = K.m_plain("cortica", "#c08a40", .8)
    for i in range(5):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=8, radius=.07, location=(-1.1 + i * .55, .05, 1.9)); b = bpy.context.object
        b.scale = (1.3, 1, 1); b.data.materials.append(boias); objs.append(b)
    F.exportar(OUT + "cenario_redes.glb", objs, [rede], tex=512, tris_tronco=2500)

def barco():
    K.reset(); mw, mf = madeira_velha("madeira_barco", "#6a5440", "#2e2318", "#9a8266"), ferro(); objs = []
    # casco: lathe achatado e aberto em cima
    bm = bmesh.new(); n = 12; secs = []
    for i in range(n + 1):
        t = i / n; y = -1.8 + 3.6 * t; w = .75 * math.sin(math.pi * (.08 + .84 * t)) ** .7; dep = .55 * math.sin(math.pi * (.05 + .9 * t)) ** .5
        sec = [bm.verts.new((w * math.cos(a), y, .6 - dep * math.sin(a) - .0)) for a in (math.pi * j / 8 for j in range(9))]
        secs.append(sec)
    for a, b in zip(secs, secs[1:]):
        for j in range(8): bm.faces.new((a[j], a[j + 1], b[j + 1], b[j]))
    casco = K.obj_from_bm(bm, "casco", mw); mod = casco.modifiers.new("s", 'SOLIDIFY'); mod.thickness = .05; K.apply_all(casco)
    disp(casco, .01, .1, 3); objs.append(casco)
    for i in range(3): objs.append(bloco(f"banco{i}", (1.2, .22, .05), (0, -1.0 + i * 1.0, .5), (0, 0, 0), mw, .01))
    objs.append(bloco("buraco", (.35, .3, .02), (.45, .2, .2), (0, 50, 0), K.m_plain("escuro", "#120c08", .9), .01))
    objs.append(F.tronco("remo", [(1.3, -.6, .05), (1.5, 1.2, .08)], .03, .03, mw, res=5))
    objs.append(bloco("pa", (.18, .5, .02), (1.52, 1.35, .08), (0, 0, 6), mw, .01))
    root = bpy.data.objects.new("tomb", None); bpy.context.collection.objects.link(root)
    for o in objs:
        if o.name.startswith(("remo", "pa")): continue
        o.parent = root
    root.rotation_euler = (0, math.radians(-18), 0); root.location = (0, 0, .05)
    bpy.context.view_layer.update()
    for o in objs:
        if o.parent:
            mw_ = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw_
    bpy.data.objects.remove(root)
    exp("cenario_barco_encalhado", objs, tex=1024, tris=5000)

def acampamento_abandonado():
    K.reset(); r = random.Random(13); ml, mw = lona(), madeira_velha(); objs = []
    # lona caída sobre uma vara quebrada
    bm = bmesh.new(); nx, ny = 8, 8; rows = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            x = -1.2 + 2.4 * i / nx; y = -1.3 + 2.6 * j / ny
            z = max(.03, 1.1 * (1 - abs(x) / 1.2) * (1 - .7 * j / ny) + rv(r, -.04, .04))
            row.append(bm.verts.new((x, y, z)))
        rows.append(row)
    for j in range(ny):
        for i in range(nx): bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
    o = K.obj_from_bm(bm, "lona_caida", ml); disp(o, .02, .3, 2); mod = o.modifiers.new("s", 'SOLIDIFY'); mod.thickness = .02; K.apply_all(o); objs.append(o)
    objs.append(F.tronco("vara", [(0, -1.4, 0), (0, -.2, 1.1)], .035, .03, mw, res=5))
    objs.append(F.tronco("vara2", [(.1, .3, .05), (.9, 1.4, .05)], .035, .03, mw, res=5))
    mp = K.m_rock("pedra_fog", "#4e4a45", "#8a847b", 3); cinza = K.m_plain("cinza", "#2a2622", .95, bumpscale=40)
    for i in range(7):
        a = i * math.tau / 7
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=.13, location=(2.0 + math.cos(a) * .45, math.sin(a) * .45, .05))
        s = bpy.context.object; s.scale = (1, 1, .6); s.data.materials.append(mp); objs.append(s)
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=.38, depth=.04, location=(2.0, 0, .02)); c = bpy.context.object; c.data.materials.append(cinza); disp(c, .02, .1, 5); objs.append(c)
    for a in (20, 80, 140):
        objs.append(F.tronco(f"tição{a}", [(2.0 - .25 * math.cos(math.radians(a)), -.25 * math.sin(math.radians(a)), .06), (2.0 + .25 * math.cos(math.radians(a)), .25 * math.sin(math.radians(a)), .06)], .045, .035, K.m_plain(f"carvao{a}", "#151210", .9, bumpscale=50), res=6))
    objs.append(bloco("caixote", (.5, .4, .35), (-1.4, .9, .17), (0, 8, 25), mw, .02))
    exp("cenario_acampamento_abandonado", objs, tex=1024, tris=5000)

def cerca_quebrada():
    K.reset(); r = random.Random(17); mw = madeira_velha(); objs = []
    objs.append(F.tronco("m1", [(-1.2, 0, -.1), (-1.2, 0, 1.0)], .06, .055, mw, res=6))
    objs.append(F.tronco("m2", [(0, 0, -.1), (.12, .05, .85)], .06, .055, mw, res=6))
    objs.append(F.tronco("m3", [(1.25, .35, .06), (1.6, 1.2, .06)], .06, .055, mw, res=6))
    t1 = bloco("tabua1", (1.35, .04, .14), (-.6, .07, .75), (0, 3, 0), mw, .008); disp(t1, .004, .04, 1); objs.append(t1)
    t2 = bloco("tabua2", (1.3, .04, .14), (-.35, .07, .3), (0, -24, 0), mw, .008); disp(t2, .004, .04, 2); objs.append(t2)
    t3 = bloco("tabua3", (1.1, .14, .04), (.8, -.4, .03), (0, 0, 30), mw, .008); objs.append(t3)
    exp("cenario_cerca_quebrada", objs, tex=512, tris=2500)

if __name__ == "__main__":
    jobs = {"muro": lambda: parede("cenario_muro_cal", 1), "muro_b": lambda: parede("cenario_muro_cal_b", 2),
            "ruina": lambda: parede("cenario_ruina_cal", 3, ruina=True), "coluna": coluna, "barricada": barricada,
            "caldeirao": caldeirao, "panela": panela, "bancada": bancada, "armazem": armazem,
            "espantalho": espantalho, "feno": feno, "regadores": regadores,
            "cranios": cranios, "ossada": ossada, "redes": redes, "barco": barco, "acampamento": acampamento_abandonado,
            "cerca": cerca_quebrada}
    jobs[sys.argv[sys.argv.index("--") + 1]]()

"""Árvore da serpente, Máscara do Pandemônio (item e peça do rosto) e mesa de Vinte-e-Um do bar."""
import sys; sys.path.insert(0, "/home/claude/cinzas/tools"); sys.path.insert(0, "/home/claude/cinzas/tapa")
import bpy, bmesh, math, random
from mathutils import Vector
import itemkit as K, kit_folha as F
from tapa_props import madeira_velha, ferro, bloco, disp, rv
OUT = "/home/claude/cinzas/tapa/out/"

def escamas(name, base="#2f5a2a", dark="#0f1f0c", gold="#c8a53a"):
    b = K.NB(name)
    cel = b.voronoi(90, 'DISTANCE_TO_EDGE'); band = b.wave(3, 2, 2, 'BANDS', 'Z')
    losango = b.math('GREATER_THAN', b.noise(5, 2, .5), .52)
    col = b.ramp(b.noise(8, 4), [(.3, b_(base, .7)), (.7, base)])
    col = b.mix(losango, col, dark)
    col = b.mix(b.math('MULTIPLY', b.math('GREATER_THAN', band, .82), 1), col, gold)
    col = b.mix(b.math('LESS_THAN', cel, .06), col, dark)
    return b.set(base=col, rough=.35, coat=.4, normal=b.bump(cel, .6, .004))
def b_(h, k): return K.b_l(h, k)

def arvore_serpente():
    K.reset(); r = random.Random(23)
    casca = F.m_casca("casca_serp", "#4a3a2e", "#1a120c", "#7d6a58", 1.4)
    tr = []; fo = F.Folhas()
    H = 6.0
    trunk = [(0, 0, 0), (.25, .1, 1.2), (-.15, .2, 2.4), (.2, -.1, 3.4), (0, 0, 4.0)]
    tr.append(F.tronco("tronco", trunk, .55, .28, casca, res=14)); disp(tr[-1], .05, .35, 2)
    for i in range(5):
        a = i * math.tau / 5 + .3
        tr.append(F.tronco(f"raiz{i}", [(0, 0, .6), (math.cos(a) * .7, math.sin(a) * .7, .15), (math.cos(a) * 1.3, math.sin(a) * 1.3, -.08)], .25, .06, casca, res=8))
    clumps = []; galhos = []
    for i in range(6):
        a = i * math.tau / 6 + rv(r, -.3, .3); z0 = rv(r, 3.0, 3.9)
        end = Vector((math.cos(a) * 2.4, math.sin(a) * 2.4, z0 + rv(r, .8, 1.6)))
        mid = Vector((math.cos(a) * 1.1, math.sin(a) * 1.1, z0 + .5))
        tr.append(F.tronco(f"g{i}", [(0, 0, z0), tuple(mid), tuple(end)], .18, .05, casca, res=8)); galhos.append((Vector((0, 0, z0)), mid, end))
        clumps += [end, mid + Vector((0, 0, .6))]
    cc = Vector((0, 0, 5.2))
    for c in clumps:
        for k in range(16):
            d = Vector((rv(r, -1, 1), rv(r, -1, 1), rv(r, -.2, 1))).normalized(); s = rv(r, 1.0, 1.4)
            fo.card(c - d * s * .4, d, s, roll=rv(r, 0, math.tau), centro=cc)
    folhas = fo.objeto("serpente_folhas", F.m_folha("folha_serp", "folha_larga.png"), cc)
    # frutos vermelhos (sem bake: brilho de cera)
    fruto = []
    for i in range(14):
        c = clumps[i % len(clumps)] + Vector((rv(r, -.6, .6), rv(r, -.6, .6), rv(r, -.7, -.2)))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=.11, location=c); f = bpy.context.object; f.scale = (1, 1, .9); fruto.append(f)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for f in fruto: f.select_set(True)
    bpy.context.view_layer.objects.active = fruto[0]; bpy.ops.object.join(); fj = bpy.context.view_layer.objects.active; fj.name = "serpente_frutos"
    mf = bpy.data.materials.new("fruto"); mf.use_nodes = True; bs = mf.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = K.lin("#b0141c"); bs.inputs["Roughness"].default_value = .25; bs.inputs["Coat Weight"].default_value = 1; fj.data.materials.append(mf)
    # serpente: espiral no tronco subindo até um galho, cabeça pendurada olhando para quem passa
    me = escamas("escamas")
    pts = []
    for i in range(30):
        t = i / 29; a = t * math.tau * 2.4; rr = .62 - .25 * t
        pts.append((math.cos(a) * rr + .02, math.sin(a) * rr, .3 + t * 3.1))
    g0, gm, ge = galhos[1]
    for t in (.25, .5, .75):
        p = g0.lerp(gm, t * 2) if t < .5 else gm.lerp(ge, (t - .5) * 2)
        pts.append(tuple(p + Vector((0, 0, .2))))
    ponta = Vector(pts[-1]); cabeca_pos = ponta + Vector((.15, -.1, -.9))
    pts += [tuple(ponta + Vector((.1, -.05, -.4))), tuple(cabeca_pos)]
    n = len(pts)
    corpo = K.tube("serpente", pts, 1, me, res=10, taper=[.06 + .1 * math.sin(math.pi * min(1, i / (n - 1) * 1.15)) for i in range(n)])
    tr.append(corpo)
    cab = K.lathe("cabeca", [(.0, -.02), (.09, .0), (.11, .1), (.09, .2), (.05, .27), (.0, .29)], 12, me)
    cab.scale = (1, .75, 1); cab.rotation_euler = (math.radians(200), 0, math.radians(20)); cab.location = cabeca_pos; tr.append(cab)
    olhos = []
    for s in (-1, 1):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=8, radius=.025, location=cabeca_pos + Vector((s * .07, -.02, -.12))); olhos.append(bpy.context.object)
    lingua = K.tube("lingua", [tuple(cabeca_pos + Vector((0, 0, -.28))), tuple(cabeca_pos + Vector((0, -.02, -.42))), tuple(cabeca_pos + Vector((.03, -.03, -.48)))], 1, None, res=4, taper=[.01, .008, .004])
    olhos.append(lingua)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in olhos: o.select_set(True)
    bpy.context.view_layer.objects.active = olhos[0]; bpy.ops.object.join(); oj = bpy.context.view_layer.objects.active; oj.name = "serpente_olhos"
    mo = bpy.data.materials.new("olho_serp"); mo.use_nodes = True; bs = mo.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = K.lin("#ffd23a"); bs.inputs["Emission Color"].default_value = K.lin("#ffb000"); bs.inputs["Emission Strength"].default_value = 3
    oj.data.materials.clear(); oj.data.materials.append(mo)
    F.exportar(OUT + "cenario_arvore_serpente.glb", tr, [folhas], tex=2048, tris_tronco=9000, extra=[fj, oj])

def mascara():
    """Máscara do Pandemônio (v2, referência do dono): entalhada num pedaço de casca da Árvore da Serpente.
    Placa de tronco levemente curva, casca grossa rachada, bordas de madeira viva cortada (laranja), olhos quadrados
    e boca em fenda; por dentro dos buracos, um brilho verde (o feitiço). Tamanho real (0,24 x 0,34 m), frente -Y."""
    K.reset(); r = random.Random(4)
    casca_m = F.m_casca("casca_mascara", "#5b5148", "#1e1813", "#9a8e80", 9.0)
    # placa: cilindro cortado (pedaço de tronco), curva acompanhando o rosto
    bm = bmesh.new(); nx, nz = 14, 20; W, H, R = .24, .34, .22
    grid = []
    for j in range(nz + 1):
        z = -H / 2 + H * j / nz; row = []
        for i in range(nx + 1):
            t = -W / 2 + W * i / nx; a = t / R
            borda = .012 * math.sin(j * 1.7 + i) if i in (0, nx) or j in (0, nz) else 0
            row.append(bm.verts.new((math.sin(a) * R + borda, -math.cos(a) * R + R - .02, z + (borda if j in (0, nz) else 0))))
        grid.append(row)
    for j in range(nz):
        for i in range(nx): bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    m = K.obj_from_bm(bm, "placa", casca_m); sol = m.modifiers.new("s", 'SOLIDIFY'); sol.thickness = .035; sol.offset = 1; K.apply_all(m)
    K.subsurf(m, 1); K.displace(m, .006, .03, seed=3); K.apply_all(m)
    def furo(nome, loc, size, rot=0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc); c = bpy.context.object; c.scale = size; c.rotation_euler = (0, math.radians(rot), 0)
        K.subsurf(c, 0); md = m.modifiers.new(nome, 'BOOLEAN'); md.object = c; md.operation = 'DIFFERENCE'; K.apply_all(m); bpy.data.objects.remove(c)
    furo("olho_e", (-.055, -.02, .045), (.05, .2, .036), 6)
    furo("olho_d", (.06, -.02, .05), (.052, .2, .04), -4)
    furo("boca", (.005, -.02, -.09), (.12, .2, .02), 2)
    m.data.materials.clear(); m.data.materials.append(casca_m)
    for p in m.data.polygons: p.material_index = 0
    # bordas cortadas: madeira viva laranja nas faces de corte (laterais da casca e beiradas dos buracos)
    viva = K.m_wood("madeira_viva", "#c8742e", "#7a3e14", 10.0)
    m.data.materials.append(viva)
    for p in m.data.polygons:
        if abs(p.normal.y) < .55: p.material_index = 1
    objs = [m]
    # brilho verde atrás dos furos (o feitiço do riso)
    luz = []
    for (x, z, sx, sz) in ((-.055, .045, .045, .03), (.06, .05, .046, .034), (.005, -.09, .11, .014)):
        bpy.ops.mesh.primitive_plane_add(size=1, location=(x, .03, z)); p = bpy.context.object; p.scale = (sx, sz, 1); p.rotation_euler = (math.radians(90), 0, 0); luz.append(p)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in luz: o.select_set(True)
    bpy.context.view_layer.objects.active = luz[0]; bpy.ops.object.join(); lj = bpy.context.view_layer.objects.active; lj.name = "mascara_brilho"
    ml = bpy.data.materials.new("brilho_masc"); ml.use_nodes = True; bs = ml.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = K.lin("#7dff2a"); bs.inputs["Emission Color"].default_value = K.lin("#6dff1a"); bs.inputs["Emission Strength"].default_value = 4
    ml.use_backface_culling = False; lj.data.materials.append(ml)
    F.exportar(OUT + "item_mascara_pandemonio.glb", objs, [], tex=1024, tris_tronco=4000, extra=[lj])

def mesa_cartas():
    K.reset(); r = random.Random(31); mw, mf = madeira_velha(), ferro(); objs = []
    feltro = K.m_plain("feltro", "#1f4a2c", .95, bumpscale=120)
    objs.append(K.lathe("tampo", [(.0, .78), (.75, .78), (.78, .8), (.78, .86), (.74, .88), (.0, .88)], 40, mw))
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=.66, depth=.012, location=(0, 0, .885)); fe = bpy.context.object; fe.data.materials.append(feltro); objs.append(fe)
    objs.append(K.lathe("pe", [(.28, 0), (.3, .04), (.08, .1), (.07, .7), (.15, .78)], 20, mw))
    for i in range(3):   # banquinhos
        a = i * math.tau / 3 + .5; c = Vector((math.cos(a) * 1.15, math.sin(a) * 1.15, 0))
        st = K.lathe(f"banco{i}", [(.0, .48), (.2, .48), (.21, .52), (.0, .53)], 20, mw); st.location = c; objs.append(st)
        for j in range(3):
            b2 = j * math.tau / 3
            objs.append(F.tronco(f"perna{i}{j}", [tuple(c + Vector((math.cos(b2) * .2, math.sin(b2) * .2, 0))), tuple(c + Vector((math.cos(b2) * .12, math.sin(b2) * .12, .48)))], .025, .02, mw, res=5))
    # pilhas de moedas e cartas (cartas como placas finas com o verso bordô)
    ouro = K.m_gold("moeda", 30)
    for i in range(5):
        c = Vector((rv(r, -.4, .4), rv(r, -.4, .4), .89)); h = r.randint(3, 9)
        for j in range(h):
            m = K.lathe(f"moeda{i}{j}", [(.0, 0), (.022, 0), (.022, .005), (.0, .005)], 14, ouro); m.location = c + Vector((rv(r, -.002, .002), rv(r, -.002, .002), j * .005)); objs.append(m)
    verso = K.m_plain("verso_carta", "#6a1c24", .6)
    for i in range(7):
        a = rv(r, 0, math.tau); rr = rv(r, .1, .5)
        objs.append(bloco(f"carta{i}", (.06, .085, .002), (math.cos(a) * rr, math.sin(a) * rr, .892 + i * .0005), (0, 0, rv(r, 0, 180)), verso, .001))
    objs.append(bloco("sapato", (.12, .2, .09), (.45, .1, .935), (0, 0, 20), mw, .01))   # caixa de onde saem as cartas
    vela = K.m_plain("vela", "#e8dcc0", .6)
    v = K.lathe("vela", [(.0, .89), (.025, .89), (.025, 1.0), (.0, 1.0)], 12, vela); v.location = (-.2, .3, 0); objs.append(v)
    pr = K.lathe("pires", [(.0, .888), (.07, .888), (.08, .9), (.0, .895)], 16, mf); pr.location = (-.2, .3, 0); objs.append(pr)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=8, radius=.015, location=(-.2, .3, 1.02)); ch = bpy.context.object; ch.scale = (1, 1, 1.8)
    mc = bpy.data.materials.new("chama_vela"); mc.use_nodes = True; bs = mc.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = K.lin("#ffb347"); bs.inputs["Emission Color"].default_value = K.lin("#ff9a2a"); bs.inputs["Emission Strength"].default_value = 6
    ch.data.materials.append(mc)
    F.exportar(OUT + "cenario_mesa_cartas.glb", objs, [], tex=1024, tris_tronco=6000, extra=[ch])

if __name__ == "__main__":
    {"serpente": arvore_serpente, "mascara": mascara, "mesa": mesa_cartas}[sys.argv[sys.argv.index("--") + 1]]()

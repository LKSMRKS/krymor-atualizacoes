"""Itens v2 (acabamento de verdade). Uso: python3 build_items2.py <saida> [ids...] [--sem-glb] [--rapido]
Saida: <saida>/icones/ico_<id>.png (128) + _256.png ; <saida>/modelos/item_<id>.glb ; <saida>/_itens_info.json"""
import sys, os, math, random, json
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh
from mathutils import Vector, Matrix, Euler
import itemkit as K

args = [a for a in sys.argv[1:] if not a.startswith("--")]; OUT = args[0]; SEL = args[1:]
NOGLB = "--sem-glb" in sys.argv; FAST = "--rapido" in sys.argv; NOICO = "--so-glb" in sys.argv
os.makedirs(OUT + "/icones", exist_ok=True); os.makedirs(OUT + "/modelos", exist_ok=True)

def orient(o, d):
    """gira o objeto (que aponta +Z) para a direcao d"""
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(Vector(d).normalized())

# ======================================================================= itens
def minerio():
    rock = K.m_rock("rocha_minerio", "#34333b", "#76727c", flecks="#e8c56a")
    cr = K.m_crystal("cristal_ciano", "#1ec8f0", "#18c8ff", 3.0)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=4, radius=0.5); r = bpy.context.object; r.data.materials.append(rock)
    r.scale = (1.15, 0.9, 0.62); K.apply_all(r)
    K.displace(r, 0.32, 0.55, 'VORONOI'); K.displace(r, 0.08, 0.25, 'CLOUDS', 3); K.apply_all(r)
    for p in r.data.polygons: p.use_smooth = False
    for v in r.data.vertices:
        if v.co.z < -0.12: v.co.z = -0.12 + (v.co.z + 0.12) * 0.2
    rnd = random.Random(4)
    spots = [((0.05, 0.0, 0.25), (0.1, -0.1, 1), 0.13, 0.75), ((-0.18, 0.1, 0.2), (-0.5, 0.3, 1), 0.1, 0.55), ((0.25, -0.08, 0.15), (0.7, -0.3, 0.9), 0.09, 0.5),
             ((0.12, 0.2, 0.18), (0.3, 0.7, 0.9), 0.08, 0.42), ((-0.05, -0.2, 0.2), (-0.2, -0.7, 0.9), 0.085, 0.45), ((-0.32, -0.05, 0.08), (-0.9, -0.2, 0.5), 0.07, 0.35),
             ((0.32, 0.15, 0.05), (0.8, 0.5, 0.4), 0.06, 0.3), ((-0.1, 0.05, 0.27), (-0.15, 0.2, 1), 0.06, 0.38), ((0.18, -0.25, 0.1), (0.4, -0.9, 0.5), 0.05, 0.25)]
    for i, (p, d, rr, h) in enumerate(spots):
        c = K.crystal(f"cr{i}", cr, rr, h, 0.32, 6, seed=i); orient(c, d); c.location = p
    return dict(cam=(58, 0, 22), glow=(40, 200, 255), size=0.22, nome="Minério Brilhante", notas="veio de cristal de maré numa rocha escura")

def pocao(cor, glow, kind):
    glass = K.m_glass("vidro_" + kind); liq = K.m_liquid("liquido_" + kind, cor, glow, 2.0)
    cork = K.m_plain("rolha", "#8a5c33", 0.85, bumpscale=60); wax = K.m_plain("lacre_" + kind, "#9e1a1a" if kind == "vida" else "#1b3e9e", 0.35, bumpscale=20)
    metal = K.m_gold("ouro_pocao") if kind == "vida" else K.m_silver("prata_pocao"); rope = K.m_plain("barbante", "#b89a68", 0.9, bumpscale=120)
    if kind == "vida":
        prof = [(0.0, 0), (0.22, 0.0), (0.36, 0.05), (0.45, 0.17), (0.48, 0.32), (0.44, 0.47), (0.32, 0.59), (0.15, 0.68), (0.11, 0.74), (0.11, 0.9), (0.15, 0.93), (0.15, 0.97), (0.1, 0.98)]
        fill = 0.52
    else:
        prof = [(0.0, 0), (0.36, 0.0), (0.42, 0.04), (0.42, 0.1), (0.3, 0.38), (0.17, 0.6), (0.1, 0.7), (0.1, 0.92), (0.14, 0.95), (0.14, 0.99), (0.09, 1.0)]
        fill = 0.45
    b = K.lathe("frasco", prof, 64, glass, close_top=False); K.mod(b, 'SOLIDIFY', thickness=0.018, offset=-1)
    inner = [(r * 0.92, z) for r, z in prof if z <= fill] + [(0.0, fill)]
    inner = [(max(0, r - 0.025), max(0.02, z)) for r, z in inner]
    li = K.lathe("liquido", [(0, 0.02)] + inner[1:], 64, liq)
    ck = K.lathe("rolha", [(0.0, 0.86), (0.095, 0.86), (0.1, 0.98), (0.12, 1.06), (0.0, 1.07)], 32, cork)
    K.displace(ck, 0.01, 0.05, 'CLOUDS')
    wx = K.lathe("lacre", [(0.0, 1.0), (0.13, 1.0), (0.155, 0.985), (0.16, 0.95), (0.15, 0.93), (0.13, 1.06), (0.12, 1.09), (0.0, 1.1)], 40, wax)
    K.displace(wx, 0.025, 0.08, 'CLOUDS'); K.subsurf(wx, 1)
    # gotas de lacre escorrendo
    for i, a in enumerate((0.3, 2.2, 4.1)):
        d = K.tube(f"gota{i}", [(0.155 * math.cos(a), 0.155 * math.sin(a), 0.95), (0.16 * math.cos(a), 0.16 * math.sin(a), 0.9 - 0.04 * i)], 0.018, wax, taper=[1, 1.3])
    col = K.lathe("colar", [(0.11, 0.72), (0.15, 0.72), (0.16, 0.75), (0.15, 0.79), (0.11, 0.79)], 48, metal, close_top=False, close_bot=False); K.mod(col, 'SOLIDIFY', thickness=0.01)
    # garras de metal abracando o bojo
    for i in range(4):
        a = i * math.tau / 4 + math.radians(30)
        pts = []
        for r, z in [(0.15, 0.73), (0.33, 0.6), (0.45, 0.47), (0.49, 0.32), (0.46, 0.18)] if kind == "vida" else [(0.15, 0.71), (0.2, 0.55), (0.3, 0.38), (0.44, 0.1)]:
            pts.append(((r + 0.012) * math.cos(a), (r + 0.012) * math.sin(a), z))
        K.tube(f"garra{i}", pts, 0.022, metal, taper=[1, 1.1, 1.0, 0.8, 0.4][:len(pts)])
        tip = pts[-1]; bpy.ops.mesh.primitive_uv_sphere_add(radius=0.035, location=tip, segments=16, ring_count=8); bpy.context.object.data.materials.append(metal)
    rp = K.tube("barbante", [(0.12 * math.cos(a), 0.12 * math.sin(a), 0.82 + 0.01 * math.sin(3 * a)) for a in [i * math.tau / 24 for i in range(25)]], 0.013, rope)
    K.tube("ponta_barbante", [(0.12, 0.0, 0.82), (0.2, -0.05, 0.78), (0.22, -0.08, 0.66)], 0.011, rope)
    if kind == "vida":   # coracao de ouro no centro
        sh = [(0, -0.1), (0.07, -0.02), (0.1, 0.05), (0.06, 0.1), (0, 0.06), (-0.06, 0.1), (-0.1, 0.05), (-0.07, -0.02)]
        bm = bmesh.new(); vs = [bm.verts.new((x, 0, z)) for x, z in sh]; f = bm.faces.new(vs)
        ext = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(0, 0.04, 0))
        h = K.obj_from_bm(bm, "coracao", metal, smooth=False); K.bevel(h, 0.012, 3, 30); K.subsurf(h, 1); h.location = (0, -0.5, 0.32)
    else:   # lua crescente de prata
        bm = bmesh.new(); pts = [(0.11 * math.cos(a), 0, 0.11 * math.sin(a)) for a in [math.radians(40 + i * 14) for i in range(21)]]
        pts += [(0.085 * math.cos(a) + 0.035, 0, 0.085 * math.sin(a)) for a in [math.radians(320 - i * 14) for i in range(21)]]
        f = bm.faces.new([bm.verts.new(p) for p in pts]); ext = bmesh.ops.extrude_face_region(bm, geom=[f])
        bmesh.ops.translate(bm, verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(0, 0.03, 0))
        h = K.obj_from_bm(bm, "lua", metal, smooth=False); K.bevel(h, 0.008, 2, 30); h.location = (0, -0.335, 0.27); h.rotation_euler = (math.radians(-12), 0, 0)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH': o.rotation_euler.z += math.radians(-15)
    return dict(cam=(68, 0, 18), glow=(255, 40, 50) if kind == "vida" else (50, 110, 255), size=0.2,
                nome="Poção de Vida" if kind == "vida" else "Poção de Mana", notas="vidro soprado, lacre e garras de " + ("ouro" if kind == "vida" else "prata"))

def joia(kind):
    if kind == "brasa":
        gm = K.m_crystal("gema_brasa", "#ff4a10", "#ff3000", 3.0); setm = K.m_gold("ouro_brasa"); acc = K.m_crystal("chama_brasa", "#ff7a18", "#ff4a00", 4.0); ring_m = K.m_iron("ferro_brasa")
        g = K.gem("gema", gm, 0.42, n=8); g.location.z = 0.2
        petal_n, petal_len = 9, 0.62
    elif kind == "mare":
        gm = K.m_crystal("gema_mare", "#14d6c0", "#00c8b4", 2.4); setm = K.m_silver("prata_mare"); acc = K.m_plain("perola", "#f4eee6", 0.25, bumpscale=0)
        g = K.gem("gema", gm, 0.4, n=10, squash=(1.0, 0.72)); g.location.z = 0.2
    else:
        gm = K.m_crystal("gema_vazio", "#9440f0", "#8a2cff", 3.0); setm = K.m_iron("ferro_vazio"); acc = K.m_crystal("estilhaco_vazio", "#3a1060", "#7a2cff", 1.2)
        g = K.gem("gema", gm, 0.45, n=3, h_crown=0.25, h_pav=0.55, table=0.45, rot=math.pi / 2); g.location.z = 0.2
    sq = (1.0, 0.72) if kind == "mare" else (1, 1)
    # taca (bezel) sob a gema + garras
    cup = K.lathe("taca", [(0.0, -0.18), (0.18, -0.17), (0.36, 0.0), (0.45, 0.14), (0.46, 0.2), (0.42, 0.2), (0.3, 0.05), (0.0, -0.05)], 48, setm)
    cup.scale = (sq[0], sq[1], 1); K.bevel(cup, 0.01, 2)
    nprong = 6 if kind != "vazio" else 3
    for i in range(nprong):
        a = i * math.tau / nprong + (math.pi / 2 if kind == "vazio" else 0.26)
        rx, ry = (0.47 * sq[0], 0.47 * sq[1]) if kind != "vazio" else (0.5, 0.5)
        K.tube(f"garra{i}", [(rx * 0.85 * math.cos(a), ry * 0.85 * math.sin(a), 0.0), (rx * 1.04 * math.cos(a), ry * 1.04 * math.sin(a), 0.17), (rx * 0.9 * math.cos(a), ry * 0.9 * math.sin(a), 0.29)],
               0.035, setm, taper=[1.2, 1.0, 0.6])
    # ornamento atras (personalidade de cada joia)
    if kind == "brasa":
        for i in range(9):   # petalas de chama
            a = i * math.tau / 9; L = 0.88 + 0.16 * (i % 2)
            bm = bmesh.new(); pts = []
            for t in [j / 10 for j in range(11)]:
                w = 0.13 * math.sin(math.pi * t) ** 0.7 * (1 - 0.4 * t)
                pts.append((w, 0.3 + t * (L - 0.3) , 0.12 * math.sin(math.pi * t) * 0.5 + 0.05 * t))
            pts += [(-x + 0.05 * math.sin(math.pi * (1 - (y - 0.3) / (L - 0.3))), y, z) for x, y, z in reversed(pts[1:-1])]
            f = bm.faces.new([bm.verts.new(p) for p in pts]); ext = bmesh.ops.extrude_face_region(bm, geom=[f])
            bmesh.ops.translate(bm, verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(0, 0, 0.04))
            p = K.obj_from_bm(bm, f"chama{i}", setm if i % 2 == 0 else acc, smooth=False); K.bevel(p, 0.008, 2, 30)
            p.rotation_euler = (math.radians(14), 0, a - math.pi / 2); p.location.z = 0.02
            # curva a ponta da chama
            K.mod(p, 'SIMPLE_DEFORM', deform_method='TWIST', angle=math.radians(25 * (1 if i % 2 else -1)), deform_axis='Y')
        bpy.ops.mesh.primitive_torus_add(major_radius=0.5, minor_radius=0.035, major_segments=64, minor_segments=12, location=(0, 0, 0.0)); bpy.context.object.data.materials.append(ring_m)
    elif kind == "mare":
        for i in range(2):   # ondas em espiral dos lados
            s = 1 if i == 0 else -1
            pts = [(s * (0.42 + 0.06 * t + 0.13 * math.cos(t * 5.5)) , 0.16 * math.sin(t * 5.5) * s, -0.02 + 0.03 * t) for t in [j / 20 for j in range(21)]]
            K.tube(f"onda{i}", pts, 0.03, setm, taper=[1.3 - 0.05 * j for j in range(21)])
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.07, location=(s * 0.62, 0, 0.02), segments=32, ring_count=16); bpy.context.object.data.materials.append(acc)
        for j, a in enumerate((math.pi / 2, -math.pi / 2)):
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.055, location=(0.0, 0.42 * math.sin(a), 0.05), segments=32, ring_count=16); bpy.context.object.data.materials.append(acc)
        bpy.ops.mesh.primitive_torus_add(major_radius=0.5, minor_radius=0.03, major_segments=64, minor_segments=12); t = bpy.context.object; t.scale = (1, 0.74, 1); t.data.materials.append(setm)
    else:
        rnd = random.Random(2)
        for i in range(7):   # espinhos de ferro e cacos de vazio
            a = i * math.tau / 7 + 0.2
            sp = K.crystal(f"esp{i}", setm, 0.06, 0.35 + 0.15 * (i % 2), 0.6, 4, seed=i); orient(sp, (math.cos(a), math.sin(a), 0.15))
            sp.location = (0.42 * math.cos(a), 0.42 * math.sin(a), -0.02)
        for i in range(5):
            a = i * math.tau / 5 + 0.9
            sh = K.crystal(f"caco{i}", acc, 0.04, 0.16, 0.5, 4, seed=10 + i); orient(sh, (math.cos(a), math.sin(a), 0.6))
            sh.location = (0.36 * math.cos(a), 0.36 * math.sin(a), 0.02)
        bpy.ops.mesh.primitive_cylinder_add(vertices=3, radius=0.58, depth=0.06, location=(0, 0, -0.06), rotation=(0, 0, math.pi / 2)); p = bpy.context.object; p.data.materials.append(setm); K.bevel(p, 0.02, 2)
    nomes = dict(brasa="Joia de Brasa", mare="Joia da Maré", vazio="Joia do Vazio")
    glow = dict(brasa=(255, 110, 20), mare=(30, 230, 200), vazio=(170, 70, 255))[kind]
    return dict(cam=(40, 0, 15), glow=glow, size=0.12, nome=nomes[kind], notas="gema lapidada em engaste de garras")

def moedas():
    gold = K.m_gold("ouro_moeda", scale=2.0)
    def coin(name):
        c = K.lathe(name, [(0.0, 0.0), (0.4, 0.0), (0.42, 0.012), (0.42, 0.068), (0.4, 0.08), (0.34, 0.08), (0.33, 0.06), (0.0, 0.06)], 64, gold)
        K.bevel(c, 0.006, 2, 30)
        # serrilha da borda
        bm = bmesh.new()
        for i in range(48):
            a = i * math.tau / 48
            bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((0.42 * math.cos(a), 0.42 * math.sin(a), 0.04)) @ Matrix.Rotation(a, 4, 'Z') @ Matrix.Diagonal((0.012, 0.018, 0.05, 1)))
        s = K.obj_from_bm(bm, name + "_serrilha", gold, smooth=False)
        # ancora em relevo
        parts = [((0, 0, 0.06), (0.035, 0.035, 0.24), (0, 0.07)), ((0, 0, 0.06), (0.17, 0.035, 0.035), (0, 0.13)), ((0, 0, 0.06), (0.035, 0.035, 0.035), (0, 0.2))]
        bm = bmesh.new()
        for (o, sz, (x, y)) in parts:
            bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((x, y - 0.06, 0.07)) @ Matrix.Diagonal((sz[0], sz[2], 0.03, 1)))
        an = K.obj_from_bm(bm, name + "_ancora", gold, smooth=False)
        arc = K.tube(name + "_arco", [(-0.17 + 0.34 * t, -0.13 - 0.12 * math.sin(math.pi * t), 0.07) for t in [j / 10 for j in range(11)]], 0.02, gold)
        ring = K.tube(name + "_aro", [(0.05 * math.cos(a), 0.2 + 0.05 * math.sin(a), 0.07) for a in [j * math.tau / 16 for j in range(17)]], 0.015, gold)
        parts = [c, s, an, arc, ring]
        for p in parts: K.apply_all(p)
        for x in bpy.context.view_layer.objects: x.select_set(False)
        for p in parts: p.select_set(True)
        bpy.context.view_layer.objects.active = c; bpy.ops.object.join(); return bpy.context.view_layer.objects.active
    base = coin("moeda")
    stacks = [((0, 0), 6), ((0.75, 0.25), 4), ((-0.6, 0.45), 3)]
    rnd = random.Random(3); allc = []
    for (x, y), n in stacks:
        for k in range(n):
            c = base.copy(); bpy.context.collection.objects.link(c)
            c.location = (x + rnd.uniform(-0.03, 0.03), y + rnd.uniform(-0.03, 0.03), k * 0.082); c.rotation_euler = (rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), rnd.uniform(0, 6.28)); allc.append(c)
    for (x, y, rx, rz) in ((0.55, -0.55, 0, 1.0), (-0.3, -0.6, 25, 0.2), (0.2, 0.95, 70, 2.0)):
        c = base.copy(); bpy.context.collection.objects.link(c); c.location = (x, y, 0.0 if rx == 0 else 0.15); c.rotation_euler = (math.radians(rx), 0, rz)
    bpy.data.objects.remove(base)
    return dict(cam=(55, 0, 30), glow=None, size=0.12, nome="Moedas de Ouro", notas="moedas cunhadas com âncora, pilhas soltas")

def bolsa():
    lea = K.m_leather("couro_bolsa", "#7a4426", "#2a140a"); rope = K.m_plain("cordao", "#c9a66b", 0.85, bumpscale=150)
    gold = K.m_gold("ouro_bolsa", 2.0); brz = K.m_bronze("bronze_bolsa")
    prof = [(0.0, 0.0), (0.22, 0.01), (0.38, 0.07), (0.48, 0.2), (0.5, 0.34), (0.46, 0.5), (0.34, 0.63), (0.19, 0.72), (0.15, 0.76), (0.17, 0.8), (0.27, 0.9), (0.33, 0.97), (0.3, 0.99)]
    b = K.lathe("bolsa", prof, 72, lea, close_top=False)
    for v in b.data.vertices:
        a = math.atan2(v.co.y, v.co.x); z = v.co.z
        f = max(0, 1 - abs(z - 0.78) / 0.35)   # pregas perto da boca
        k = 1 + 0.09 * f * math.sin(11 * a) + 0.05 * (1 - f) * math.sin(5 * a + 1.3) * math.sin(z * 6)
        v.co.x *= k; v.co.y *= k
        if z > 0.9: v.co.z += 0.03 * math.sin(7 * a)    # borda irregular
    K.mod(b, 'SOLIDIFY', thickness=0.025, offset=1); K.displace(b, 0.04, 0.35, 'CLOUDS', 3); K.subsurf(b, 1)
    b.scale = (1, 0.88, 1)
    # cordao de amarrar
    K.tube("cordao", [(0.185 * math.cos(a), 0.165 * math.sin(a), 0.77 + 0.012 * math.sin(4 * a)) for a in [i * math.tau / 32 for i in range(33)]], 0.026, rope)
    K.tube("ponta1", [(0.18, -0.07, 0.77), (0.27, -0.16, 0.7), (0.3, -0.22, 0.55), (0.28, -0.25, 0.42)], 0.02, rope)
    K.tube("ponta2", [(0.17, -0.09, 0.77), (0.2, -0.2, 0.68), (0.16, -0.3, 0.55), (0.12, -0.32, 0.46)], 0.02, rope)
    for p in ((0.28, -0.25, 0.42), (0.12, -0.32, 0.46)):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.04, location=p, segments=16, ring_count=8); bpy.context.object.data.materials.append(rope)
    # moedas saindo pela boca
    for i, (x, y, rx, ry) in enumerate(((0.05, 0.02, 60, 10), (-0.08, 0.06, 40, -30), (0.1, -0.06, 75, 20))):
        c = K.lathe(f"m{i}", [(0, 0), (0.12, 0), (0.125, 0.008), (0.12, 0.022), (0.1, 0.022), (0.0, 0.018)], 40, gold)
        c.location = (x, y, 0.93); c.rotation_euler = (math.radians(rx), math.radians(ry), 0)
    # costura lateral e pingente de bronze no cordao
    seam = K.m_leather("costura", "#3a1d0e", "#140803")
    K.tube("costura", [(0.44 * math.cos(-1.2), 0.88 * 0.44 * math.sin(-1.2), z) for z in [0.12 + j * 0.05 for j in range(10)]], 0.012, seam)
    md = K.lathe("pingente", [(0, -0.012), (0.06, -0.012), (0.07, 0.0), (0.06, 0.012), (0, 0.016)], 32, brz)
    md.rotation_euler = (math.radians(80), 0, math.radians(20)); md.location = (0.29, -0.27, 0.36)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.025, minor_radius=0.008, location=(0.285, -0.255, 0.43), rotation=(math.radians(90), 0, 0)); bpy.context.object.data.materials.append(brz)
    return dict(cam=(64, 0, 20), glow=None, size=0.24, nome="Bolsa de Moedas", notas="couro costurado, cordão e moedas saindo")

def osso():
    bone = K.m_bone("osso_v2")
    shaft = K.tube("diafise", [(-0.42, 0, 0), (-0.2, 0.02, 0.01), (0.0, 0.03, 0.02), (0.2, 0.02, 0.01), (0.4, 0, 0)], 0.075, bone, taper=[1.15, 0.95, 0.85, 0.95, 1.15])
    def sph(loc, r, sc=(1, 1, 1)):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=loc, segments=32, ring_count=16); o = bpy.context.object; o.scale = sc; o.data.materials.append(bone); return o
    sph((-0.5, 0.06, 0.04), 0.1); sph((-0.46, -0.05, 0.0), 0.085, (1.1, 1, 0.9))       # cabeca + trocanter
    K.tube("colo", [(-0.42, 0, 0), (-0.48, 0.05, 0.03)], 0.06, bone)
    sph((0.5, 0.06, -0.01), 0.095, (1.1, 1, 0.95)); sph((0.5, -0.07, -0.01), 0.09, (1.1, 1, 0.95))   # condilos
    sph((0.44, 0, 0.0), 0.11, (0.9, 1.2, 0.85))
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in objs: K.apply_all(o)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = shaft; bpy.ops.object.join(); j = bpy.context.view_layer.objects.active
    K.mod(j, 'REMESH', mode='VOXEL', voxel_size=0.012); K.mod(j, 'SMOOTH', factor=0.8, iterations=6)
    K.displace(j, 0.012, 0.15, 'CLOUDS', 3)
    K.apply_all(j)
    for p in j.data.polygons: p.use_smooth = True
    K.mod(j, 'DECIMATE', ratio=0.25)
    j.rotation_euler = (0, 0, math.radians(-30))
    return dict(cam=(60, 0, 15), glow=None, size=0.32, nome="Osso", notas="fêmur com rachaduras e manchas de terra")

def presa():
    bone = K.m_bone("marfim", "#efe2c2", "#9b7442", scale=2.0); root = K.m_bone("raiz_presa", "#7d5a36", "#2e1d10", scale=2.0)
    lea = K.m_leather("tira", "#5b3018", "#1f0e05", 2.0); brz = K.m_bronze("conta", 2.0)
    pts, tp = [], []
    for i in range(13):
        t = i / 12; a = t * math.radians(130)
        pts.append((0.55 * math.sin(a) - 0.3, 0.0, 0.55 * (1 - math.cos(a)) + 0.04 * math.sin(math.pi * t))); tp.append(max(0.03, (1 - t) ** 0.8))
    tk = K.tube("presa", pts, 0.13, bone, taper=tp)
    tk.scale = (1, 0.8, 1)
    K.displace(tk, 0.006, 0.08, 'CLOUDS', 2)
    # amarracao de couro com conta de bronze e pena de tira pendurada
    for k in range(3):
        x0 = pts[3][0] + 0.03 * k
        K.tube(f"volta{k}", [(x0 + 0.005 * math.sin(a), 0.125 * math.cos(a) * 0.82, pts[3][2] + 0.01 * k + 0.12 * math.sin(a)) for a in [j * math.tau / 20 for j in range(21)]], 0.018, lea)
    K.tube("tira", [(pts[3][0], -0.1, pts[3][2] - 0.08), (pts[3][0] + 0.05, -0.16, pts[3][2] - 0.22), (pts[3][0] + 0.02, -0.18, pts[3][2] - 0.38)], 0.016, lea)
    c = K.lathe("conta", [(0, -0.04), (0.04, -0.035), (0.055, 0), (0.04, 0.035), (0, 0.04)], 24, brz); c.location = (pts[3][0] + 0.04, -0.17, pts[3][2] - 0.26)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH': o.rotation_euler.z += math.radians(15)
    return dict(cam=(25, 0, 10), glow=None, size=0.18, nome="Presa", notas="presa de javali com amarração de couro e conta de bronze")

def pinca():
    sh = K.m_chitin("quitina_pinca", "#b23a1a", "#3d0e07", "#e07a4c", scale=1.5); tip = K.m_chitin("ponta_pinca", "#24130f", "#0b0504", "#6d4a3a", crust=False, scale=1.5)
    meat = K.m_plain("carne", "#6e1f18", 0.35, bumpscale=30)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, segments=48, ring_count=24); p = bpy.context.object; p.data.materials.append(sh)
    p.scale = (0.42, 0.62, 0.36); K.apply_all(p)
    for v in p.data.vertices:
        if v.co.y > 0: v.co.x *= 1 + 0.4 * v.co.y; v.co.z *= 1 + 0.35 * v.co.y
    K.displace(p, 0.06, 0.18, 'VORONOI'); K.subsurf(p, 1)
    def finger(name, base, pts, r, m, teeth_dir):
        tb = K.tube(name, pts, r, m, taper=[1.0, 0.85, 0.6, 0.35, 0.08][:len(pts)])
        for i in range(5):
            t = 0.2 + i * 0.15; idx = t * (len(pts) - 1); a = int(idx); f = idx - a
            q = Vector(pts[a]).lerp(Vector(pts[min(a + 1, len(pts) - 1)]), f)
            th = K.crystal(f"{name}_dente{i}", tip, 0.035 * (1.2 - t), 0.06 * (1.3 - t), 0.8, 4, seed=i); orient(th, teeth_dir); th.location = q + Vector(teeth_dir) * r * 0.6 * (1.1 - t)
        return tb
    fx = finger("dedo_fixo", None, [(0.0, 0.3, -0.06), (0.03, 0.55, -0.1), (0.06, 0.78, -0.08), (0.05, 0.96, 0.0), (0.02, 1.06, 0.1)], 0.14, sh, (0, 0, 1))
    mv = finger("dedo_movel", None, [(0.0, 0.25, 0.16), (-0.02, 0.5, 0.28), (-0.03, 0.75, 0.3), (-0.02, 0.95, 0.22), (0.0, 1.05, 0.12)], 0.12, sh, (0, 0, -1))
    for nm, pt in (("pf", (0.02, 1.06, 0.1)), ("pm", (0.0, 1.05, 0.12))):
        t = K.crystal(nm, tip, 0.05, 0.12, 0.7, 6, seed=3); orient(t, (0, 1, 0.3 if nm == "pf" else -0.3)); t.location = Vector(pt) - Vector((0, 0.05, 0))
    # espinhos no dorso
    for i in range(6):
        y = -0.15 + i * 0.1
        s = K.crystal(f"esp{i}", tip, 0.035, 0.12, 0.8, 5, seed=20 + i); orient(s, (0.4 * (1 if i % 2 else -1), 0.1, 1)); s.location = (0.06 * (1 if i % 2 else -1), y, 0.17 + 0.05 * (y + 0.2))
    # corte da carne atras
    c = K.lathe("carne", [(0, 0), (0.13, 0), (0.14, 0.02), (0.0, 0.025)], 32, meat); c.rotation_euler = (math.radians(90), 0, 0); c.location = (0, -0.3, 0.0)
    K.displace(c, 0.02, 0.05, 'CLOUDS')
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            o.rotation_euler.rotate(Euler((0, 0, math.radians(-50)))); o.location = Matrix.Rotation(math.radians(-50), 3, 'Z') @ o.location
    return dict(cam=(55, 0, 0), glow=None, size=0.36, nome="Pinça de Caranguejo", notas="pinça com dentes, espinhos e crosta de cal")

def carapaca():
    sh = K.m_chitin("quitina_carapaca", "#a83a1c", "#3a0d06", "#f2dcb6", scale=1.5); salt = K.m_crystal("sal", "#7fe8dc", "#40e0d0", 2.0)
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=80, y_subdivisions=80, size=1.2); g = bpy.context.object; g.data.materials.append(sh)
    rnd = random.Random(5)
    bm = bmesh.new(); bm.from_mesh(g.data)
    def edge_r(a): return 0.42 + 0.07 * math.sin(3 * a + 1) + 0.05 * math.sin(7 * a) + 0.03 * math.sin(13 * a + 2)
    dead = [v for v in bm.verts if math.hypot(v.co.x, v.co.y * 1.25) > edge_r(math.atan2(v.co.y, v.co.x))]
    bmesh.ops.delete(bm, geom=dead, context='VERTS'); bm.to_mesh(g.data); bm.free()
    for v in g.data.vertices:
        d = math.hypot(v.co.x, v.co.y)
        v.co.z = 0.32 * (1 - (d / 0.5) ** 2) + 0.03 * math.cos(v.co.x * 14) * math.exp(-abs(v.co.y) * 6)
    K.mod(g, 'SOLIDIFY', thickness=0.05, offset=-1); K.displace(g, 0.04, 0.12, 'VORONOI'); K.bevel(g, 0.01, 2, 50)
    for p in g.data.polygons: p.use_smooth = True
    K.subsurf(g, 1)
    for i in range(7):
        a = rnd.uniform(0, 6.28); r = rnd.uniform(0.0, 0.18)
        c = K.crystal(f"sal{i}", salt, rnd.uniform(0.025, 0.05), rnd.uniform(0.08, 0.17), 0.4, 6, seed=i)
        orient(c, (math.cos(a) * 0.6, math.sin(a) * 0.6, 1)); c.location = (0.1 + r * math.cos(a), -0.05 + r * math.sin(a), 0.3 - r * 0.4)
    return dict(cam=(60, 0, 25), glow=None, size=0.28, nome="Fragmento de Carapaça", notas="placa quebrada de casco com crosta de cal e cristais de sal")

def erva():
    leaf = K.m_leaf("folha_erva", "#3f6e22", "#a6cf5a"); stem = K.m_plain("caule", "#4f6d2a", 0.6, bumpscale=50)
    twine = K.m_plain("barbante_erva", "#b89660", 0.9, bumpscale=150); berry = K.m_plain("baga", "#c3122a", 0.12)
    root = K.m_plain("raiz", "#6b4a2c", 0.9, bumpscale=40)
    rnd = random.Random(8)
    def make_leaf(name, L, W):
        bm = bmesh.new(); left, right = [], []
        N = 14
        for i in range(N + 1):
            t = i / N; w = W * math.sin(math.pi * t ** 0.85) * (1 + 0.12 * ((i % 2) * 2 - 1) * (0.2 < t < 0.95))
            bend = 0.18 * L * t * t
            left.append(bm.verts.new((-w, t * L, bend - 0.02 * w / max(W, 1e-3)))); right.append(bm.verts.new((w, t * L, bend - 0.02 * w / max(W, 1e-3))))
        mid = [bm.verts.new((0, i / N * L, 0.18 * L * (i / N) ** 2 + 0.01)) for i in range(N + 1)]
        for i in range(N):
            bm.faces.new((left[i], mid[i], mid[i + 1], left[i + 1])); bm.faces.new((mid[i], right[i], right[i + 1], mid[i + 1]))
        o = K.obj_from_bm(bm, name, leaf); K.mod(o, 'SOLIDIFY', thickness=0.006); return o
    stems = []
    for s in range(6):
        a = s * math.tau / 6 + rnd.uniform(-0.3, 0.3); sp = rnd.uniform(0.1, 0.22)
        top = (sp * math.cos(a) * 1.6, sp * math.sin(a) * 1.6, 1.0 + rnd.uniform(-0.12, 0.08))
        pts = [(0.02 * math.cos(a), 0.02 * math.sin(a), 0.0), (0.03 * math.cos(a), 0.03 * math.sin(a), 0.35), (top[0] * 0.5, top[1] * 0.5, 0.65), top]
        K.tube(f"caule{s}", pts, 0.018, stem, taper=[1, 1, 0.8, 0.5]); stems.append(pts)
        for k in range(4):
            t = 0.45 + k * 0.17; P = Vector(pts[2]).lerp(Vector(pts[3]), (t - 0.45) / 0.55) if t > 0.45 else Vector(pts[2])
            lf = make_leaf(f"folha{s}_{k}", rnd.uniform(0.22, 0.32), rnd.uniform(0.05, 0.08))
            lf.location = P; lf.rotation_euler = (math.radians(rnd.uniform(-60, -25)), 0, a + math.radians(rnd.uniform(-70, 70) + 90))
        for b_ in range(rnd.randint(2, 4)):
            bpy.ops.mesh.primitive_uv_sphere_add(radius=rnd.uniform(0.025, 0.035), location=(top[0] + rnd.uniform(-0.04, 0.04), top[1] + rnd.uniform(-0.04, 0.04), top[2] + rnd.uniform(-0.02, 0.04)), segments=16, ring_count=8)
            bpy.context.object.data.materials.append(berry)
    K.tube("amarra", [(0.06 * math.cos(a), 0.06 * math.sin(a), 0.32 + 0.01 * math.sin(3 * a)) for a in [i * math.tau / 20 for i in range(21)]], 0.016, twine)
    K.tube("amarra2", [(0.065 * math.cos(a), 0.065 * math.sin(a), 0.36) for a in [i * math.tau / 20 for i in range(21)]], 0.014, twine)
    K.tube("laco", [(0.06, 0, 0.33), (0.14, -0.05, 0.3), (0.16, -0.1, 0.2)], 0.012, twine)
    for r_ in range(5):
        a = rnd.uniform(0, 6.28)
        K.tube(f"raiz{r_}", [(0.02 * math.cos(a), 0.02 * math.sin(a), 0.02), (0.06 * math.cos(a), 0.06 * math.sin(a), -0.06), (0.1 * math.cos(a + 0.4), 0.1 * math.sin(a + 0.4), -0.12)], 0.01, root, taper=[1, 0.7, 0.2])
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            o.location = Matrix.Rotation(math.radians(-62), 3, 'Y') @ o.location; o.rotation_euler.rotate(Euler((0, math.radians(-62), 0)))
    return dict(cam=(60, 0, 10), glow=None, size=0.3, nome="Erva da Costa", notas="maço de erva com bagas, amarrado com barbante")

def essencia():
    brz = K.m_bronze("bronze_relicario"); core = K.m_emit("nucleo", "#e9d3ff", 9.0); wisp = K.m_crystal("fumaca_espirito", "#b77bff", "#9a3cff", 3.5)
    glass = K.m_glass("redoma", "#f1ecff", 0.02)
    # redoma de vidro
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.36, segments=48, ring_count=24, location=(0, 0, 0.5)); gl = bpy.context.object; gl.data.materials.append(glass); gl.scale = (1, 1, 1.2)
    K.mod(gl, 'SOLIDIFY', thickness=0.01)
    # espirito: chama torcida dentro
    fl = K.lathe("espirito", [(0, 0.24), (0.1, 0.27), (0.15, 0.36), (0.14, 0.48), (0.09, 0.6), (0.04, 0.7), (0.0, 0.78)], 32, wisp)
    K.mod(fl, 'SIMPLE_DEFORM', deform_method='TWIST', angle=math.radians(160), deform_axis='Z'); K.displace(fl, 0.05, 0.1, 'CLOUDS')
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.06, location=(0, 0, 0.42)); bpy.context.object.data.materials.append(core)
    # gaiola de filigrana
    for i in range(6):
        a = i * math.tau / 6
        pts = [(0.385 * math.sin(t) * math.cos(a), 0.385 * math.sin(t) * math.sin(a), 0.5 - 0.46 * math.cos(t)) for t in [j * math.pi / 14 for j in range(15)]]
        K.tube(f"meridiano{i}", pts, 0.018, brz)
        # voluta no meio de cada gomo
        b0 = a + math.pi / 6
        K.tube(f"voluta{i}", [(0.39 * math.cos(b0 + 0.18 * math.cos(t)), 0.39 * math.sin(b0 + 0.18 * math.cos(t)), 0.5 + 0.12 * math.sin(t)) for t in [j * math.tau / 12 for j in range(13)]], 0.01, brz)
    for z, r in ((0.5, 0.39), (0.3, 0.33), (0.7, 0.33)):
        K.tube(f"anel{z}", [(r * math.cos(a), r * math.sin(a), z) for a in [j * math.tau / 40 for j in range(41)]], 0.014 if z == 0.5 else 0.01, brz)
    K.lathe("base", [(0, -0.02), (0.2, -0.02), (0.24, 0.02), (0.18, 0.07), (0.1, 0.06), (0.06, 0.1), (0, 0.1)], 48, brz)
    K.lathe("topo", [(0, 0.9), (0.08, 0.9), (0.1, 0.94), (0.04, 0.98), (0.03, 1.04), (0, 1.06)], 32, brz)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.07, minor_radius=0.016, location=(0, 0, 1.12), rotation=(math.radians(90), 0, 0)); bpy.context.object.data.materials.append(brz)
    rnd = random.Random(6)
    for i in range(6):
        bpy.ops.mesh.primitive_ico_sphere_add(radius=rnd.uniform(0.012, 0.02), location=(rnd.uniform(-0.18, 0.18), rnd.uniform(-0.18, 0.18), rnd.uniform(0.3, 0.75)))
        bpy.context.object.data.materials.append(core)
    return dict(cam=(78, 0, 20), glow=(170, 90, 255), size=0.16, nome="Essência", notas="espírito preso num relicário de bronze com redoma")

def seda():
    silk = K.m_silk("seda_v2"); wood = K.m_wood("madeira_carretel"); brz = K.m_bronze("bronze_carretel")
    K.lathe("carretel", [(0, -0.05), (0.32, -0.05), (0.34, -0.02), (0.34, 0.02), (0.18, 0.05), (0.13, 0.08), (0.13, 0.92), (0.18, 0.95), (0.34, 0.98), (0.34, 1.02), (0.32, 1.05), (0, 1.05)], 64, wood)
    prof = [(0.13, 0.07), (0.25, 0.08)] + [(0.27 + 0.03 * math.sin(math.pi * t) + 0.01 * math.sin(t * 40), 0.08 + 0.84 * t) for t in [j / 30 for j in range(31)]] + [(0.13, 0.93)]
    s = K.lathe("seda", prof, 96, silk, close_top=False, close_bot=False)
    for nm, z in (("aro1", 0.0), ("aro2", 1.0)):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.34, minor_radius=0.018, major_segments=64, location=(0, 0, z)); bpy.context.object.data.materials.append(brz)
    K.tube("fio", [(0.29, 0.0, 0.6), (0.38, -0.08, 0.55), (0.45, -0.25, 0.4), (0.38, -0.45, 0.25), (0.42, -0.6, 0.1)], 0.008, silk)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH' and o.name in ("carretel", "seda", "aro1", "aro2", "fio"):
            pass
    for o in [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name.split(".")[0] in ("carretel", "seda", "Torus", "fio")]:
        o.location = Matrix.Rotation(math.radians(80), 3, 'Y') @ (o.location - Vector((0, 0, 0))) + Vector((0, 0, 0.36)); o.rotation_euler.rotate(Euler((0, math.radians(80), 0)))
    return dict(cam=(60, 0, 35), glow=None, size=0.24, nome="Carretel de Seda", notas="seda brilhante enrolada em carretel de madeira e bronze")

ITENS = dict(minerio=minerio, pocao_vida=lambda: pocao("#ff2236", "#ff0a20", "vida"), pocao_mana=lambda: pocao("#2c6bff", "#1050ff", "mana"),
             joia_brasa=lambda: joia("brasa"), joia_mare=lambda: joia("mare"), joia_vazio=lambda: joia("vazio"), moedas=moedas, bolsa=bolsa,
             osso=osso, presa=presa, pinca_caranguejo=pinca, carapaca=carapaca, erva=erva, essencia=essencia, seda=seda)

info_path = OUT + "/_itens_info.json"
info = json.load(open(info_path)) if os.path.exists(info_path) else {}
for iid, fn in ITENS.items():
    if SEL and iid not in SEL: continue
    K.reset(); cfg = fn()
    if not NOICO: K.render_icon(f"{OUT}/icones/ico_{iid}", cam_rot=cfg["cam"], glow=cfg.get("glow"), samples=24 if FAST else 96, res=320 if FAST else 448)
    print("icone ok", iid, flush=True)
    if not NOGLB:
        r = K.export_item(f"{OUT}/modelos/item_{iid}.glb", cfg["size"], 512)
        info[iid] = dict(asset_id=f"item_{iid}", nome=cfg["nome"], notas=cfg["notas"], **r); print("glb ok", iid, r, flush=True)
    json.dump(info, open(info_path, "w"), indent=1, ensure_ascii=False)

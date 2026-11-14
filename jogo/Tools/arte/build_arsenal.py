"""Arsenal: cada forma de arma em 4 qualidades (0 enferrujada, 1 comum, 2 refinada, 3 excelente) + graveto, aljava, varinha e talisma da Bruxa.
Convencao do jogo (GearFactory): EMPUNHADURA NA ORIGEM, lamina para +Z aqui (= +Y do Godot), largura em X, espessura em Y.
Machado: cabeca para +X. Arco: igual ao BowModel (pontas em Z, barriga para -Y aqui = +Z Godot, corda em y=+0,02, pegada em y=-0,14).
Aljava: origem na alca (vai no socket das costas), boca para +Z.
Uso: python3 build_arsenal.py <saida> [ids...] [--rapido] [--sem-glb]"""
import sys, os, math, random, json
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh
from mathutils import Vector, Matrix, Euler
import itemkit as K, org

args = [a for a in sys.argv[1:] if not a.startswith("--")]; OUT = args[0]; SEL = args[1:]
FAST = "--rapido" in sys.argv; NOGLB = "--sem-glb" in sys.argv
os.makedirs(OUT + "/icones", exist_ok=True); os.makedirs(OUT + "/modelos", exist_ok=True)

TIER_NOME = ["Enferrujada", "Comum", "Refinada", "Excelente"]
TIER_NIVEL = ["1–5", "5–15", "15–30", "30+"]

def M_rust(name):
    b = K.NB(name)
    n1 = b.noise(8, 8, 0.7); pits = b.voronoi(60, 'F1')
    rust = b.math('GREATER_THAN', b.math('ADD', n1, b.math('MULTIPLY', b.noise(30, 4), 0.3)), 0.52)
    c = b.mix(rust, b.ramp(n1, [(0.3, "#3a3634"), (0.7, "#5c5652")]), b.ramp(b.noise(20, 6), [(0.2, "#4a1e0c"), (0.6, "#8a3e16"), (0.9, "#b8622a")]))
    c = b.mix(b.math('MULTIPLY', K.cavity(b, 0.04), 1.6, clamp=True), c, "#1a0c06")
    c = b.mix(b.math('MULTIPLY', K.edge_mask(b, 0.5, 0.56), 0.5, clamp=True), c, "#8a8276")
    return b.set(base=c, metal=b.math('SUBTRACT', 0.85, b.math('MULTIPLY', rust, 0.8)), rough=b.math('ADD', 0.5, b.math('MULTIPLY', rust, 0.45)),
                 normal=b.bump(b.math('ADD', b.math('MULTIPLY', pits, 0.5), b.math('MULTIPLY', rust, 0.4)), 0.6, 0.004))

def M_steel(name, polish):
    if polish: return K.m_metal(name, "#c8ccd4", 0.18, wear="#ffffff", dirt="#3a3e46", scratch=0.4, scale=4)
    return K.m_metal(name, "#6e7076", 0.38, wear="#c6c4bf", dirt="#16120f", scratch=0.9, hammered=0.2, scale=4)

def M_cloth(name, col):
    b = K.NB(name); n1 = b.noise(14, 6)
    c = b.ramp(n1, [(0.2, K.b_l(col, 0.5)), (0.8, col)]); c = b.mix(b.math('MULTIPLY', K.cavity(b, 0.02), 1.5, clamp=True), c, "#120c08")
    return b.set(base=c, rough=0.92, sheen=0.5, normal=b.bump(b.wave(300, 2, 2, 'BANDS', 'Z'), 0.3, 0.002))

def mats(t, gem="#ff3a1a"):
    """jogo de materiais por qualidade"""
    if t == 0:
        return dict(blade=M_rust("lamina_ferrugem"), fit=M_rust("guarda_ferrugem"), grip=M_cloth("trapo", "#7a7262"),
                    wood=K.m_wood("madeira_gasta", "#6a6258", "#2a2620", 6), accent=M_rust("acento_ferrugem"), gem=None, glow=None)
    if t == 1:
        return dict(blade=M_steel("lamina_ferro", False), fit=K.m_iron("guarda_ferro", 4), grip=K.m_leather("couro_cabo", "#5a3420", "#1c0e06", 6),
                    wood=K.m_wood("madeira_cabo", "#6e4526", "#2a160a", 6), accent=K.m_iron("acento_ferro", 4), gem=None, glow=None)
    if t == 2:
        return dict(blade=M_steel("lamina_aco", True), fit=K.m_bronze("guarda_bronze", 4), grip=K.m_leather("couro_escuro", "#2e1a10", "#0c0604", 6),
                    wood=K.m_wood("madeira_laca", "#3a2010", "#120804", 6), accent=K.m_bronze("acento_bronze", 4), gem=None, glow=None)
    return dict(blade=M_steel("lamina_aco_claro", True), fit=K.m_gold("guarda_ouro", 4), grip=K.m_leather("couro_vermelho", "#6a1a14", "#200605", 6),
                wood=K.m_wood("madeira_ebano", "#2a1a12", "#0a0504", 6), accent=K.m_gold("acento_ouro", 4),
                gem=K.m_crystal("gema_arma", gem, gem, 3.0), glow=K.m_emit("runa_arma", gem, 5.0))

# ------------------------------------------------------------------ pecas basicas
def blade(name, L, w0, w1, thick, m, tip=0.12, curve=0.0, single=False, z0=0.0, fuller=None):
    """lamina: perfil losango (gume dos dois lados ou um so), estreitando de w0 a w1, ponta"""
    bm = bmesh.new(); N = 16; rows = []
    for i in range(N + 1):
        t = i / N; z = z0 + t * L
        w = w0 + (w1 - w0) * t
        if t > 1 - tip: w *= max(0.0, (1 - t) / tip) ** 0.8
        cx = curve * (t ** 2) * L
        if single:   # costas grossas em -X, gume em +X
            pts = [(cx - w * 0.5, -thick / 2, z), (cx - w * 0.5, thick / 2, z), (cx + w * 0.5, 0.0, z)]
        else:
            pts = [(cx - w / 2, 0, z), (cx, -thick / 2, z), (cx + w / 2, 0, z), (cx, thick / 2, z)]
        rows.append([bm.verts.new(p) for p in pts])
    n = len(rows[0])
    for a, b_ in zip(rows, rows[1:]):
        for k in range(n): bm.faces.new((a[k], a[(k + 1) % n], b_[(k + 1) % n], b_[k]))
    bm.faces.new(list(reversed(rows[0])))
    tipv = bm.verts.new((curve * L, 0, z0 + L + 0.002))
    for k in range(n): bm.faces.new((rows[-1][k], rows[-1][(k + 1) % n], tipv))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = K.obj_from_bm(bm, name, m, smooth=False)
    if fuller is not None:          # runa/brilho no sulco (so na excelente)
        K.tube(name + "_runa", [(curve * ((z0 + L * f) / max(L, 1e-3) - z0 / max(L, 1e-3)) ** 2 * L, -thick / 2 - 0.0015, z0 + L * f) for f in (0.1, 0.3, 0.5)], 0.0028, fuller)
        K.tube(name + "_runa2", [(curve * 0.0, thick / 2 + 0.0015, z0 + L * 0.08), (0, thick / 2 + 0.0015, z0 + L * 0.62)], 0.004, fuller)
    return o

def wrap(name, z0, z1, r, m, turns=7):
    """cabo enrolado (tiras)"""
    c = K.lathe(name, [(0, z0), (r, z0), (r * 1.04, (z0 + z1) / 2), (r, z1), (0, z1)], 16, m)
    pts = [((r + 0.003) * math.cos(a), (r + 0.003) * math.sin(a), z0 + (z1 - z0) * a / (turns * math.tau)) for a in [j * 0.35 for j in range(int(turns * math.tau / 0.35) + 1)]]
    K.tube(name + "_tira", pts, 0.0035, m)
    return c

def pommel(name, z, r, m, gem=None):
    p = K.lathe(name, [(0, z - r * 1.2), (r * 0.8, z - r), (r, z - r * 0.3), (r * 0.7, z + r * 0.4), (r * 0.45, z + r * 0.7), (0, z + r * 0.75)], 16, m)
    if gem is not None:
        g = K.gem(name + "_gema", gem, r * 0.55, n=8); g.rotation_euler = (math.radians(90), 0, 0); g.location = (0, -r * 0.7, z - r * 0.2)
    return p

def guard(name, w, z, m, curl=0.0, t=0.03, gem=None):
    pts = [(-w / 2, 0, z + curl), (-w / 4, 0, z + curl * 0.2), (0, 0, z), (w / 4, 0, z + curl * 0.2), (w / 2, 0, z + curl)]
    g = K.tube(name, pts, t * 0.5, m, taper=[0.8, 1.0, 1.25, 1.0, 0.8])
    g.scale = (1, 1.3, 1)
    for s in (-1, 1): bpy.ops.mesh.primitive_uv_sphere_add(radius=t * 0.6, location=(s * w / 2, 0, z + curl), segments=12, ring_count=6); bpy.context.object.data.materials.append(m)
    if gem is not None:
        gg = K.gem(name + "_gema", gem, t * 0.65, n=8); gg.rotation_euler = (math.radians(90), 0, 0); gg.location = (0, -t * 0.55, z)
    return g

def shaft(name, z0, z1, r, m, rings=None, ring_m=None):
    s = K.lathe(name, [(0, z0), (r * 1.05, z0), (r, z0 + 0.03), (r * 0.95, (z0 + z1) / 2), (r, z1 - 0.03), (r * 1.05, z1), (0, z1)], 12, m)
    for z in (rings or []): K.lathe(name + f"_anel{z:.2f}", [(r * 1.0, z - 0.012), (r * 1.18, z - 0.006), (r * 1.18, z + 0.006), (r * 1.0, z + 0.012)], 12, ring_m, close_top=False, close_bot=False)
    return s

# ------------------------------------------------------------------ armas
def faca(t):
    M = mats(t, "#ff4a2a")
    blade("lamina", 0.2, 0.034, 0.03, 0.006, M["blade"], tip=0.35, single=True, z0=0.07, fuller=M["glow"])
    box_guard = guard("guarda", 0.06, 0.065, M["fit"], 0.0, 0.016)
    wrap("cabo", -0.06, 0.06, 0.014, M["grip"]); pommel("pomo", -0.072, 0.016, M["fit"], M["gem"])
    return dict(nome="Faquinha", tipo="faca", grip="uma mão", size=0.34)

def adaga(t):
    M = mats(t, "#5ab4ff")
    blade("lamina", 0.28, 0.045, 0.03, 0.008, M["blade"], tip=0.3, z0=0.075, fuller=M["glow"])
    guard("guarda", 0.12 + 0.02 * t, 0.07, M["fit"], 0.015 * (t >= 2), 0.02, M["gem"])
    wrap("cabo", -0.065, 0.065, 0.015, M["grip"]); pommel("pomo", -0.08, 0.02, M["fit"], M["gem"])
    return dict(nome="Adaga", tipo="adaga", grip="uma mão", size=0.45)

def espada_curta(t):
    M = mats(t, "#ff3a1a")
    blade("lamina", 0.62, 0.055, 0.04, 0.009, M["blade"], tip=0.12, z0=0.11, fuller=M["glow"])
    guard("guarda", 0.2 + 0.03 * t, 0.105, M["fit"], 0.02 * (t >= 2), 0.026, M["gem"])
    wrap("cabo", -0.09, 0.095, 0.017, M["grip"]); pommel("pomo", -0.11, 0.026, M["fit"], M["gem"])
    return dict(nome="Espada Curta", tipo="espada", grip="uma mão", size=0.86)

def espada_longa(t):
    M = mats(t, "#9a5aff")
    blade("lamina", 0.88, 0.06, 0.042, 0.01, M["blade"], tip=0.1, z0=0.13, fuller=M["glow"])
    guard("guarda", 0.26 + 0.04 * t, 0.125, M["fit"], 0.035 * (t >= 2), 0.03, M["gem"])
    wrap("cabo", -0.17, 0.115, 0.018, M["grip"], 10); pommel("pomo", -0.19, 0.03, M["fit"], M["gem"])
    return dict(nome="Espada Longa", tipo="espada", grip="duas mãos", size=1.25)

def katana(t):
    M = mats(t, "#ff2a5a")
    blade("lamina", 0.72, 0.034, 0.03, 0.008, M["blade"], tip=0.08, curve=0.09, single=True, z0=0.09, fuller=M["glow"])
    K.lathe("habaki", [(0.0, 0.07), (0.02, 0.07), (0.021, 0.095), (0, 0.095)], 8, M["fit"])
    tsuba = K.lathe("tsuba", [(0, 0.062), (0.045, 0.062), (0.048, 0.07), (0.045, 0.078), (0, 0.078)], 24 if t < 3 else 8, M["fit"]); tsuba.scale = (1, 0.8, 1)
    wrap("tsuka", -0.2, 0.06, 0.015, M["grip"], 9)
    for k in range(7):     # losangos do ito-maki
        z = -0.18 + k * 0.035
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.006, location=(0, -0.016, z), segments=6, ring_count=4); bpy.context.object.data.materials.append(M["accent"])
    K.lathe("kashira", [(0, -0.225), (0.016, -0.222), (0.017, -0.2), (0, -0.2)], 12, M["fit"])
    return dict(nome="Katana", tipo="katana", grip="duas mãos", size=1.05)

def clava(t):
    M = mats(t, "#ff8a1a")
    c = K.lathe("clava", [(0, -0.12), (0.022, -0.12), (0.024, 0.0), (0.03, 0.2), (0.045, 0.4), (0.06, 0.55), (0.062, 0.6), (0.04, 0.64), (0, 0.65)], 16, M["wood"])
    K.displace(c, 0.006, 0.04, 'CLOUDS')
    wrap("cabo", -0.11, 0.08, 0.026, M["grip"], 6)
    rnd = random.Random(t)
    if t >= 1:
        for k in range(10 + 4 * t):
            a = rnd.uniform(0, 6.28); z = rnd.uniform(0.35, 0.6); r = 0.03 + (z - 0.2) * 0.075
            d = Vector((math.cos(a), math.sin(a), 0.1))
            if t >= 2: org.spike(f"cravo{k}", Vector((r * math.cos(a), r * math.sin(a), z)), Vector((r * math.cos(a), r * math.sin(a), z)) + d * 0.05, 0.012).data.materials.append(M["fit"])
            else: bpy.ops.mesh.primitive_uv_sphere_add(radius=0.01, location=(r * math.cos(a), r * math.sin(a), z), segments=8, ring_count=4); bpy.context.object.data.materials.append(M["fit"])
    if t >= 2: K.lathe("aro_ponta", [(0.058, 0.5), (0.066, 0.51), (0.066, 0.56), (0.058, 0.57)], 16, M["accent"], close_top=False, close_bot=False)
    if M["gem"] is not None:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.03, location=(0, 0, 0.655), segments=16, ring_count=8); bpy.context.object.data.materials.append(M["accent"])
    return dict(nome="Clava", tipo="clava", grip="uma mão", size=0.8)

def maca(t):
    M = mats(t, "#5aff9a")
    shaft("haste", -0.1, 0.5, 0.018, M["fit"] if t >= 2 else M["wood"], [0.12, 0.3], M["accent"])
    wrap("cabo", -0.1, 0.09, 0.02, M["grip"], 6)
    K.lathe("nucleo", [(0, 0.46), (0.035, 0.47), (0.045, 0.53), (0.035, 0.6), (0, 0.62)], 16, M["blade"])
    nf = 6 + 2 * (t >= 2)
    for k in range(nf):
        a = k * math.tau / nf; bm = bmesh.new()
        pts = [(0.03, 0.47), (0.075, 0.5), (0.085, 0.54), (0.07, 0.58), (0.03, 0.6)]
        vs = [bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for r, z in pts] + [bm.verts.new((0.03 * math.cos(a), 0.03 * math.sin(a), 0.535))]
        f = bm.faces.new(vs); ext = bmesh.ops.extrude_face_region(bm, geom=[f])
        bmesh.ops.translate(bm, verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(-math.sin(a) * 0.012, math.cos(a) * 0.012, 0))
        o = K.obj_from_bm(bm, f"aleta{k}", M["blade"], smooth=False); K.bevel(o, 0.002, 1, 30)
    org.spike("ponta", Vector((0, 0, 0.6)), Vector((0, 0, 0.66)), 0.018).data.materials.append(M["blade"])
    pommel("pomo", -0.11, 0.024, M["fit"], M["gem"])
    return dict(nome="Maça de Aletas", tipo="maca", grip="uma mão", size=0.8)

def machado_mao(t):
    M = mats(t, "#ffd21a")
    shaft("cabo_madeira", -0.12, 0.5, 0.017, M["wood"], [0.4] if t >= 1 else [], M["fit"])
    wrap("cabo", -0.11, 0.06, 0.019, M["grip"], 5)
    bm = bmesh.new()
    prof = [(-0.025, 0.42), (0.03, 0.43), (0.09, 0.36), (0.15, 0.33), (0.16, 0.42), (0.155, 0.52), (0.13, 0.56), (0.08, 0.5), (0.03, 0.52), (-0.025, 0.52)]
    vs = [bm.verts.new((x, 0, z)) for x, z in prof]; f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(0, 0.024, 0))
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, -0.012, 0))
    for v in bm.verts:
        if v.co.x > 0.13: v.co.y *= 0.25          # gume afinado
    h = K.obj_from_bm(bm, "cabeca", M["blade"], smooth=False); K.bevel(h, 0.003, 1, 30)
    if t >= 2: org.spike("esporao", Vector((-0.02, 0, 0.47)), Vector((-0.09, 0, 0.45)), 0.015).data.materials.append(M["blade"])
    if M["glow"] is not None: K.tube("runa_mach", [(0.07, -0.0125, 0.45), (0.11, -0.0125, 0.42), (0.13, -0.0125, 0.47)], 0.004, M["glow"])
    return dict(nome="Machadinha", tipo="machado", grip="uma mão", size=0.7)

def machado_guerra(t):
    M = mats(t, "#ff3a1a")
    shaft("cabo_madeira", -0.35, 0.82, 0.021, M["wood"], [0.55, 0.72] if t >= 1 else [], M["fit"])
    wrap("cabo", -0.33, 0.05, 0.023, M["grip"], 8)
    for s in ((1,) if t < 2 else (1, -1)):
        bm = bmesh.new()
        edge = [(0.07 + 0.22 * math.cos(math.radians(g)), 0.67 + 0.22 * math.sin(math.radians(g))) for g in range(-62, 63, 12)]
        prof = [(0.025, 0.61), (0.07, 0.585), (0.12, 0.53)] + edge + [(0.13, 0.8), (0.07, 0.755), (0.025, 0.75)]
        vs = [bm.verts.new((x * s, 0, z)) for x, z in prof]; f = bm.faces.new(vs if s > 0 else list(reversed(vs)))
        ext = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(0, 0.03, 0))
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0, -0.015, 0))
        for v in bm.verts:
            if abs(v.co.x) > 0.22: v.co.y *= 0.25
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        h = K.obj_from_bm(bm, f"lamina{s}", M["blade"], smooth=False); K.bevel(h, 0.003, 1, 30)
    K.lathe("olho", [(0, 0.6), (0.032, 0.6), (0.035, 0.78), (0, 0.78)], 8, M["fit"])
    org.spike("ponta_topo", Vector((0, 0, 0.8)), Vector((0, 0, 0.95)), 0.022).data.materials.append(M["blade"])
    if M["gem"] is not None: g = K.gem("gema_mach", M["gem"], 0.025, n=8); g.rotation_euler = (math.radians(90), 0, 0); g.location = (0, -0.036, 0.69)
    pommel("pomo", -0.37, 0.026, M["fit"], None)
    return dict(nome="Machado de Guerra", tipo="machado", grip="duas mãos", size=1.32)

def lanca(t):
    M = mats(t, "#5ab4ff")
    shaft("haste", -0.55, 1.25, 0.017, M["wood"], [0.0, 1.05] if t >= 1 else [], M["fit"])
    wrap("cabo", -0.12, 0.12, 0.019, M["grip"], 6)
    blade("ponta", 0.3, 0.06, 0.03, 0.01, M["blade"], tip=0.45, z0=1.26, fuller=M["glow"])
    K.lathe("alvado", [(0, 1.2), (0.022, 1.2), (0.018, 1.27), (0, 1.27)], 10, M["fit"])
    if t >= 2:
        for s in (-1, 1): org.spike(f"orelha{s}", Vector((0, 0, 1.25)), Vector((s * 0.07, 0, 1.3)), 0.012).data.materials.append(M["blade"])
    if t == 3:
        rib = M_cloth("fita", "#9a1a14"); K.tube("fita", [(0.02, 0, 1.2), (0.06, 0.01, 1.12), (0.05, 0.02, 1.0), (0.08, 0.0, 0.9)], 0.008, rib)
    K.lathe("conto", [(0, -0.6), (0.012, -0.58), (0.02, -0.55), (0, -0.55)], 10, M["fit"])
    return dict(nome="Lança", tipo="lanca", grip="duas mãos", size=1.9)

def arco(t, longo):
    M = mats(t, "#7affd8")
    L = 1.35 if longo else 1.1; N = 14; pts, tp = [], []
    for i in range(N + 1):
        u = i / N * 2 - 1; z = u * L / 2
        y = -((1 - u * u) * 0.16 - 0.02)                    # barriga para -Y (= +Z do Godot), como o BowModel
        if t >= 2: y += 0.035 * (abs(u) ** 3)            # recurvo nas pontas
        pts.append((0, y, z)); tp.append(1.0 - 0.55 * abs(u))
    K.tube("braco_arco", pts, 0.017, M["wood"], taper=tp)
    K.tube("corda", [(0, 0.02 + (0.035 if t >= 2 else 0) * 0, -L / 2 + 0.01), (0, 0.02, L / 2 - 0.01)], 0.0025, K.m_plain("corda_arco", "#d8ccb0", 0.7))
    wrap("pegada", -0.07, 0.07, 0.024, M["grip"], 5).location = (0, -0.14, 0)
    for s in (-1, 1):
        K.lathe(f"ponteira{s}", [(0, 0), (0.012, 0.0), (0.008, 0.05), (0, 0.06)], 8, M["fit"]).location = (0, pts[0 if s < 0 else -1][1], s * L / 2)
    if t >= 1:
        for s in (-1, 1): K.lathe(f"reforco{s}", [(0.02, -0.01), (0.023, 0), (0.02, 0.01)], 10, M["accent"], close_top=False, close_bot=False).location = (0, -0.14, s * 0.09)
    if M["gem"] is not None: g = K.gem("gema_arco", M["gem"], 0.025, n=8); g.rotation_euler = (math.radians(90), 0, 0); g.location = (0, -0.17, 0.0)
    if M["glow"] is not None:
        for s in (-1, 1): K.tube(f"runa_arco{s}", [(0.0, pts[N // 2 + s * 3][1] - 0.012, pts[N // 2 + s * 3][2]), (0.0, pts[N // 2 + s * 5][1] - 0.01, pts[N // 2 + s * 5][2])], 0.004, M["glow"])
    return dict(nome="Arco Longo" if longo else "Arco Curto", tipo="arco", grip="mão esquerda", size=L)

def graveto(t):
    w = K.m_wood("graveto", "#6a5038", "#2a1a0e", 8)
    pts = [(0, 0, -0.12), (0.01, 0.0, 0.1), (-0.015, 0.01, 0.35), (0.02, 0.0, 0.55), (0.0, 0.01, 0.72)]
    g = K.tube("graveto", pts, 0.025, w, taper=[1.1, 1.0, 0.9, 1.0, 0.8]); K.displace(g, 0.005, 0.03, 'CLOUDS')
    for k, (z, a) in enumerate(((0.3, 0.6), (0.5, 3.0))):
        org.spike(f"no{k}", Vector((0, 0, z)), Vector((0.06 * math.cos(a), 0.06 * math.sin(a), z + 0.04)), 0.012).data.materials.append(w)
    if t >= 1:
        i = K.m_iron("prego", 6)
        for k in range(3): K.tube(f"prego{k}", [(0.0, 0.0, 0.55 + k * 0.05), (0.05 * math.cos(k * 2.1), 0.05 * math.sin(k * 2.1), 0.56 + k * 0.05)], 0.004, i)
        wrap("cabo_trapo", -0.1, 0.05, 0.027, M_cloth("trapo_graveto", "#8a7a62"), 4)
    return dict(nome="Graveto" if t == 0 else "Graveto com Pregos", tipo="clava", grip="uma mão", size=0.85)

def aljava(t):
    M = mats(t, "#7affd8"); lea = K.m_leather("couro_aljava", ["#5a3420", "#5a3420", "#2e1a10", "#5a1a14"][t], "#1c0e06", 4)
    a = K.lathe("tubo", [(0, -0.3), (0.06, -0.3), (0.065, -0.28), (0.07, 0.2), (0.075, 0.25), (0.07, 0.26), (0.0, 0.26)], 20, lea)
    a.scale = (1, 0.8, 1)
    for z in ((-0.25, 0.18) if t >= 1 else (0.18,)): K.lathe(f"aro{z}", [(0.068, z - 0.012), (0.074, z), (0.068, z + 0.012)], 20, M["fit"], close_top=False, close_bot=False)
    rnd = random.Random(t); fl = K.m_plain("pena", ["#d8ccb0", "#d8ccb0", "#2a2a2a", "#9a1a14"][t], 0.8)
    for k in range(8 + 2 * t):
        x, y = rnd.uniform(-0.04, 0.04), rnd.uniform(-0.03, 0.03); z = 0.25 + rnd.uniform(0.05, 0.12)
        K.tube(f"flecha{k}", [(x, y, 0.1), (x, y, z)], 0.004, K.m_wood("haste_flecha", "#a07a4a", "#5a3a1a", 10))
        for s in range(3):
            a_ = s * math.tau / 3; bm = bmesh.new()
            vs = [bm.verts.new((x, y, z - 0.07)), bm.verts.new((x + 0.016 * math.cos(a_), y + 0.016 * math.sin(a_), z - 0.05)), bm.verts.new((x + 0.016 * math.cos(a_), y + 0.016 * math.sin(a_), z - 0.01)), bm.verts.new((x, y, z))]
            bm.faces.new(vs); K.obj_from_bm(bm, f"pena{k}{s}", fl)
    K.tube("alca", [(0.0, 0.06, -0.25), (0.02, 0.09, 0.0), (0.0, 0.06, 0.2)], 0.012, lea)
    if M["gem"] is not None: g = K.gem("gema_aljava", M["gem"], 0.02, n=8); g.rotation_euler = (math.radians(90), 0, 0); g.location = (0, -0.06, -0.05)
    return dict(nome="Aljava", tipo="aljava", grip="costas", size=None)

def varinha(n):
    if n == 1:
        w = K.m_wood("galho_bruxa", "#5a4030", "#20140a", 8)
        g = K.tube("varinha", [(0, 0, -0.1), (0.005, 0.0, 0.1), (-0.008, 0.005, 0.22), (0.01, 0.0, 0.32)], 0.012, w, taper=[1.2, 1.0, 0.8, 0.5]); K.displace(g, 0.002, 0.02, 'CLOUDS')
        c = K.crystal("cristal", K.m_crystal("cristal_v1", "#7ae88a", "#4aff6a", 2.5), 0.012, 0.05, 0.4, 6, 1); c.location = (0.01, 0, 0.31)
        K.tube("cipo", [(0.012 * math.cos(a), 0.012 * math.sin(a), 0.2 + a * 0.012) for a in [j * 0.5 for j in range(14)]], 0.003, K.m_leaf("cipo", "#3a5a22", "#7aaa3a", 8))
        nome = "Varinha de Galho"
    elif n == 2:
        bone = K.m_bone("osso_varinha", scale=6); g = K.m_crystal("cristal_v2", "#b06aff", "#9a3aff", 3.5)
        K.lathe("varinha", [(0, -0.11), (0.012, -0.1), (0.014, 0.0), (0.011, 0.15), (0.009, 0.28), (0, 0.29)], 12, bone)
        for z in (-0.05, 0.05, 0.15): K.lathe(f"anel{z}", [(0.013, z - 0.006), (0.016, z), (0.013, z + 0.006)], 12, K.m_silver("prata_varinha", 6), close_top=False, close_bot=False)
        for k in range(3): org.spike(f"garra{k}", Vector((0.009 * math.cos(k * 2.1), 0.009 * math.sin(k * 2.1), 0.27)), Vector((0.025 * math.cos(k * 2.1), 0.025 * math.sin(k * 2.1), 0.33)), 0.005).data.materials.append(K.m_silver("prata_garra", 6))
        c = K.crystal("cristal", g, 0.016, 0.06, 0.45, 6, 2); c.location = (0, 0, 0.29)
        nome = "Varinha de Osso Entalhado"
    else:
        gold = K.m_gold("ouro_varinha", 6); g = K.m_crystal("cristal_v3", "#ff6ad8", "#ff3ac0", 4.5); dark = K.m_wood("ebano_varinha", "#1a1210", "#050302", 8)
        K.lathe("varinha", [(0, -0.12), (0.013, -0.11), (0.015, 0.0), (0.012, 0.2), (0.016, 0.26), (0, 0.27)], 16, dark)
        K.tube("espiral", [(0.016 * math.cos(a), 0.016 * math.sin(a), -0.1 + a * 0.011) for a in [j * 0.4 for j in range(32)]], 0.003, gold)
        for k in range(4):
            a = k * math.pi / 2; K.tube(f"garra{k}", [(0.012 * math.cos(a), 0.012 * math.sin(a), 0.26), (0.035 * math.cos(a), 0.035 * math.sin(a), 0.3), (0.012 * math.cos(a), 0.012 * math.sin(a), 0.36)], 0.004, gold)
        c = K.crystal("cristal", g, 0.02, 0.05, 0.6, 6, 3); c.location = (0, 0, 0.3)
        for k in range(3):
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.006, location=(0.05 * math.cos(k * 2.1), 0.05 * math.sin(k * 2.1), 0.33 + 0.02 * k)); bpy.context.object.data.materials.append(g)
        nome = "Varinha do Cristal Flutuante"
    return dict(nome=nome, tipo="varinha", grip="uma mão (Bruxa)", size=None)

def talisma(n):
    chain = [K.m_iron("corrente_t1", 8), K.m_silver("corrente_t2", 8), K.m_gold("corrente_t3", 8)][n - 1]
    for k in range(9):
        z = 0.02 + k * 0.022
        bpy.ops.mesh.primitive_torus_add(major_radius=0.008, minor_radius=0.0022, location=(0, 0, z), rotation=(0, math.radians(90) if k % 2 else 0, 0)); bpy.context.object.data.materials.append(chain)
    if n == 1:
        bone = K.m_bone("osso_talisma", scale=8)
        o = K.lathe("dente", [(0, -0.1), (0.006, -0.09), (0.014, -0.04), (0.016, 0.0), (0, 0.01)], 10, bone); o.rotation_euler = (0, math.radians(8), 0)
        K.tube("amarra", [(0.017 * math.cos(a), 0.017 * math.sin(a), 0.0) for a in [j * math.tau / 12 for j in range(13)]], 0.003, K.m_leather("couro_talisma", "#5a3420", "#1c0e06", 8))
        nome = "Talismã de Dente"
    elif n == 2:
        s = K.m_silver("prata_talisma", 8); g = K.m_crystal("pedra_lua", "#9ad8ff", "#5ab8ff", 2.5)
        d = K.lathe("disco", [(0, -0.004), (0.035, -0.004), (0.04, 0), (0.035, 0.004), (0, 0.004)], 32, s); d.rotation_euler = (math.radians(90), 0, 0); d.location = (0, 0, -0.03)
        gg = K.gem("pedra", g, 0.018, n=10); gg.rotation_euler = (math.radians(90), 0, 0); gg.location = (0, -0.006, -0.03)
        for k in range(8):
            a = k * math.tau / 8; org.spike(f"raio{k}", Vector((0.04 * math.cos(a), 0, -0.03 + 0.04 * math.sin(a))), Vector((0.052 * math.cos(a), 0, -0.03 + 0.052 * math.sin(a))), 0.004).data.materials.append(s)
        nome = "Talismã da Lua Prateada"
    else:
        gold = K.m_gold("ouro_talisma", 8); g = K.m_crystal("olho_talisma", "#ff3a6a", "#ff1a4a", 4.5)
        bm = bmesh.new(); pts = [(0.0, 0.03), (0.035, 0.0), (0.0, -0.05), (-0.035, 0.0)]
        f = bm.faces.new([bm.verts.new((x, 0, z - 0.03)) for x, z in pts]); ext = bmesh.ops.extrude_face_region(bm, geom=[f])
        bmesh.ops.translate(bm, verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(0, 0.012, 0)); bmesh.ops.translate(bm, verts=bm.verts, vec=(0, -0.006, 0))
        o = K.obj_from_bm(bm, "losango", gold, smooth=False); K.bevel(o, 0.003, 2, 30)
        gg = K.gem("olho", g, 0.014, n=8, squash=(1.0, 0.6)); gg.rotation_euler = (math.radians(90), 0, 0); gg.location = (0, -0.008, -0.035)
        for s in (-1, 1): K.tube(f"asa{s}", [(s * 0.03, 0, -0.03), (s * 0.06, 0, -0.01), (s * 0.075, 0, 0.015)], 0.004, gold, taper=[1, 0.8, 0.3])
        nome = "Talismã do Olho Carmesim"
    return dict(nome=nome, tipo="talisma", grip="mão esquerda (Bruxa)", size=None)

FORMAS = dict(faca=faca, adaga=adaga, espada_curta=espada_curta, espada_longa=espada_longa, katana=katana, clava=clava, maca=maca,
              machado_mao=machado_mao, machado_guerra=machado_guerra, lanca=lanca, arco_curto=lambda t: arco(t, False), arco_longo=lambda t: arco(t, True))
TIER_SUF = ["enferrujada", "comum", "refinada", "excelente"]
ITENS = {}
for f, fn in FORMAS.items():
    for t in range(4): ITENS[f"arma_{f}_{TIER_SUF[t]}"] = (fn, t)
ITENS["arma_graveto"] = (graveto, 0); ITENS["arma_graveto_pregos"] = (graveto, 1)
for t in (1, 2, 3): ITENS[f"arma_aljava_{TIER_SUF[t]}"] = (aljava, t)
for n in (1, 2, 3): ITENS[f"arma_varinha_bruxa_n{n}"] = (varinha, n); ITENS[f"arma_talisma_bruxa_n{n}"] = (talisma, n)

info_path = OUT + "/_arsenal_info.json"
info = json.load(open(info_path)) if os.path.exists(info_path) else {}
for aid, (fn, t) in ITENS.items():
    if SEL and not any(aid.startswith(s) for s in SEL): continue
    K.reset(); cfg = fn(t)
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    # icone na diagonal (ponta para cima a direita)
    root = bpy.data.objects.new("giro", None); bpy.context.collection.objects.link(root)
    for o in objs: o.parent = root
    root.rotation_mode = 'ZYX'
    root.rotation_euler = (0, math.radians(-45 if cfg["tipo"] not in ("aljava", "talisma") else -20), math.radians(90 if cfg["tipo"] == "arco" else 0))
    glow = {3: (255, 190, 80), 2: None}.get(t) if cfg["tipo"] not in ("varinha", "talisma") else (200, 120, 255)
    K.render_icon(f"{OUT}/icones/ico_{aid[5:]}", cam_rot=(90, 0, 0), glow=glow, samples=24 if FAST else 80, res=320 if FAST else 448, pad=1.08)
    root.rotation_mode = 'XYZ'; root.rotation_euler = (0, 0, 0); bpy.context.view_layer.update()
    for o in objs: mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    bpy.data.objects.remove(root)
    rar = TIER_NOME[t] if cfg["tipo"] not in ("varinha", "talisma") else f"Nível {t}"
    print("icone ok", aid, flush=True)
    if not NOGLB:
        r = K.export_item(f"{OUT}/modelos/{aid}.glb", None, 512, 3500)
        nome = cfg["nome"] if cfg["tipo"] in ("varinha", "talisma") or aid.startswith("arma_graveto") else f"{cfg['nome']} {rar}"
        info[aid] = dict(asset_id=aid, nome=nome, tipo=cfg["tipo"], qualidade=rar, nivel_sugerido=TIER_NIVEL[min(t, 3)] if cfg["tipo"] not in ("varinha", "talisma") else ["", "1–10", "10–25", "25+"][t],
                         empunhadura=cfg["grip"], **r)
        json.dump(info, open(info_path, "w"), indent=1, ensure_ascii=False); print("glb ok", aid, r, flush=True)

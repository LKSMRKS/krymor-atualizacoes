"""Moveis de interior (casas, taverna, ferraria) + pecas de puzzle. Uso: python3 build_interior.py <saida> [ids...] [--rapido]
Origem no chao no centro; frente = +Z do Godot (-Y do Blender). Pecas moveis do puzzle saem separadas, com posicao_no_conjunto_godot no _info.json."""
import sys, os, math, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh
from mathutils import Vector, Matrix
import itemkit as K, org, cz
import build_praca as BP
from build_praca import box, cyl, M_stone

def W(name="madeira", base="#7a5030", dark="#2e1a0c", s=2): return K.m_wood(name, K.b_l(base, 0.7) if isinstance(base, str) else base, dark, s)
def IRON(name="ferro"): return K.m_metal(name, "#2e2e33", 0.5, wear="#8d8780", dirt="#0d0b0a", scratch=0.7, scale=3)
def CLOTH(name, col):
    b = K.NB(name); n1 = b.noise(10, 6)
    c = b.ramp(n1, [(0.25, K.b_l(col, 0.6)), (0.75, col)]); c = b.mix(b.math('MULTIPLY', K.cavity(b, 0.06), 1.5, clamp=True), c, "#120c08")
    weave = b.math('MULTIPLY', b.wave(260, 0, 0, 'BANDS', 'X'), b.wave(260, 0, 0, 'BANDS', 'Y'))
    return b.set(base=c, rough=0.9, sheen=0.6, normal=b.bump(weave, 0.25, 0.002))

def plank_top(name, L, Wd, T, z, mat, n=5, gap=0.008, x0=0, y0=0):
    w = (Wd - gap * (n - 1)) / n
    for i in range(n): box(f"{name}{i}", (L, w, T), (x0, y0 - Wd / 2 + w / 2 + i * (w + gap), z), mat, 0.006)

def turned_leg(name, x, y, h, mat, r=0.045):
    K.lathe(name, [(r * 1.1, 0), (r * 0.9, 0.05), (r * 0.7, 0.12), (r, 0.2), (r * 0.65, h * 0.5), (r, h * 0.8), (r * 1.05, h - 0.04), (r * 1.1, h)], 16, mat).location = (x, y, 0)

def caneca(x, y, z, mat_w, mat_i):
    c = K.lathe("caneca", [(0, 0), (0.045, 0), (0.05, 0.02), (0.048, 0.11), (0.04, 0.11), (0.04, 0.015), (0, 0.015)], 16, mat_w); c.location = (x, y, z)
    for zz in (0.025, 0.09): K.lathe("aro", [(0.05, zz - 0.006), (0.054, zz), (0.05, zz + 0.006)], 16, mat_i, close_top=False, close_bot=False).location = (x, y, z)
    K.tube("alca", [(x + 0.05, y, z + 0.09), (x + 0.085, y, z + 0.075), (x + 0.085, y, z + 0.035), (x + 0.05, y, z + 0.025)], 0.008, mat_i)

def vela(x, y, z):
    cyl("vela", 0.022, 0.12, (x, y, z + 0.06), K.m_plain("cera", "#e8dcc0", 0.6), n=12)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.012, location=(x, y, z + 0.135), segments=8, ring_count=6); o = bpy.context.object; o.scale = (1, 1, 1.8)
    o.data.materials.append(K.m_emit("chama", "#ffb347", 8))

# ------------------------------------------------------------------ moveis
def mesa():
    w = W("madeira_mesa", "#6e4526", "#2a160a"); i = IRON()
    plank_top("tampo", 1.6, 0.9, 0.05, 0.755, w, 4)
    for x in (-0.68, 0.68):
        for y in (-0.34, 0.34): turned_leg(f"perna{x}{y}", x, y, 0.73, w)
    box("travessa", (1.36, 0.06, 0.06), (0, 0, 0.2), w, 0.01)
    for x in (-0.68, 0.68): box(f"trav_lado{x}", (0.06, 0.68, 0.06), (x, 0, 0.2), w, 0.01)
    box("saia1", (1.4, 0.03, 0.09), (0, -0.37, 0.68), w, 0.006); box("saia2", (1.4, 0.03, 0.09), (0, 0.37, 0.68), w, 0.006)
    caneca(-0.4, -0.15, 0.78, W("madeira_caneca", "#8a6038", "#3a240e", 6), i); caneca(0.35, 0.2, 0.78, W("madeira_caneca2", "#7a5230", "#3a240e", 6), i)
    K.lathe("prato", [(0, 0), (0.12, 0), (0.14, 0.02), (0.13, 0.025), (0, 0.01)], 24, K.m_plain("estanho", "#8a8a86", 0.35, metal=0.8)).location = (0.0, -0.1, 0.78)
    vela(0.1, 0.25, 0.78)
    return dict(tris=7000, tex=1024, colisao=[dict(caixa_m=[1.6, 0.9])], nome="Mesa de taverna (com canecas, prato e vela)", luz=dict(pos_godot=[0.1, 0.92, -0.25], cor="#ffb347", alcance=2.5))

def cadeira():
    w = W("madeira_cadeira", "#6e4526", "#2a160a")
    plank_top("assento", 0.46, 0.44, 0.035, 0.45, w, 3)
    for x in (-0.19, 0.19):
        for y in (-0.18, 0.18): turned_leg(f"perna{x}{y}", x, y, 0.44, w, 0.025)
        box(f"montante{x}", (0.04, 0.04, 0.55), (x, 0.2, 0.72), w, 0.008)
    for k in range(3): box(f"ripa{k}", (0.38, 0.025, 0.06), (0, 0.2, 0.62 + k * 0.14), w, 0.006)
    box("trav", (0.38, 0.03, 0.03), (0, -0.18, 0.15), w, 0.005)
    return dict(tris=3000, tex=512, colisao=[dict(caixa_m=[0.5, 0.5])], nome="Cadeira de madeira", notas="sentar: assento a 0,47 m; virada para +Z")

def banqueta():
    w = W("madeira_banqueta", "#7a5030", "#2e1a0c")
    cyl("assento", 0.18, 0.05, (0, 0, 0.5), w, n=20)
    for k in range(3):
        a = k * math.tau / 3; K.tube(f"perna{k}", [(0.12 * math.cos(a), 0.12 * math.sin(a), 0.48), (0.2 * math.cos(a), 0.2 * math.sin(a), 0.0)], 0.022, w)
    K.tube("anel", [(0.15 * math.cos(a), 0.15 * math.sin(a), 0.2) for a in [j * math.tau / 20 for j in range(21)]], 0.012, w)
    return dict(tris=2000, tex=512, colisao=[dict(raio_m=0.22)], nome="Banqueta de três pés")

def cama():
    w = W("madeira_cama", "#5e3a20", "#22120a"); lin = CLOTH("lencol", "#d8ccb0"); cob = CLOTH("cobertor", "#7a2a22"); pal = CLOTH("palha", "#b89a5a")
    for x in (-0.5, 0.5):
        box(f"lateral{x}", (0.06, 2.0, 0.18), (x, 0, 0.32), w, 0.01)
    box("cabeceira", (1.1, 0.07, 0.9), (0, 1.0, 0.45), w, 0.015); box("pes", (1.1, 0.07, 0.5), (0, -1.0, 0.25), w, 0.015)
    for x in (-0.5, 0.5):
        for y in (-1.0, 1.0): box(f"pe{x}{y}", (0.09, 0.09, 1.0 if y > 0 else 0.6), (x, y, 0.5 if y > 0 else 0.3), w, 0.01)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.47)); col = bpy.context.object; col.scale = (0.94, 1.94, 0.18); col.data.materials.append(pal); K.apply_all(col)
    K.bevel(col, 0.06, 4); K.subsurf(col, 1)
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=30, y_subdivisions=40, size=1, location=(0, -0.25, 0.575)); c = bpy.context.object; c.scale = (1.08, 1.45, 1); c.data.materials.append(cob); K.apply_all(c)
    for v in c.data.vertices:
        if abs(v.co.x) > 0.44: v.co.z -= (abs(v.co.x) - 0.44) * 2.4
        v.co.z += 0.02 * math.sin(v.co.y * 9 + v.co.x * 3) * (abs(v.co.x) < 0.44)
    K.mod(c, 'SOLIDIFY', thickness=0.025); K.subsurf(c, 1)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.3, location=(0, 0.72, 0.62)); tr = bpy.context.object; tr.scale = (1.3, 0.55, 0.32); tr.data.materials.append(lin); K.displace(tr, 0.02, 0.1, 'CLOUDS')
    return dict(tris=8000, tex=1024, colisao=[dict(caixa_m=[1.1, 2.1])], nome="Cama de madeira com colchão de palha e cobertor", notas="dormir: deitar em z 0,6, cabeça para -Z do Godot (+Y aqui é a cabeceira)")

def balcao():
    w = W("madeira_balcao", "#5a3820", "#1e1008", 1.5); i = IRON("ferro_balcao")
    plank_top("tampo", 2.8, 0.7, 0.06, 1.05, W("madeira_tampo", "#7a5232", "#2a170a", 1.5), 3)
    box("corpo", (2.7, 0.55, 1.0), (0, 0.05, 0.5), w, 0.02)
    for k in range(7): box(f"painel{k}", (0.32, 0.03, 0.75), (-1.2 + k * 0.4, -0.24, 0.5), W("madeira_painel", "#6a4426", "#22120a", 3), 0.01)
    box("rodape", (2.75, 0.08, 0.1), (0, -0.26, 0.05), w, 0.01)
    box("apoio_pe", (2.6, 0.04, 0.04), (0, -0.38, 0.25), i, 0.005)
    for k in range(4): caneca(-1.0 + k * 0.6 + random.Random(k).uniform(-0.1, 0.1), -0.05, 1.08, W(f"caneca_b{k}", "#8a6038", "#3a240e", 6), i)
    b = K.lathe("barril_torneira", [(0, 0), (0.22, 0), (0.25, 0.1), (0.26, 0.25), (0.25, 0.4), (0.22, 0.5), (0, 0.5)], 24, W("madeira_barrilzinho", "#7b4f2c", "#2b170a", 4))
    b.rotation_euler = (0, math.radians(90), 0); b.location = (1.0, 0.1, 1.33)
    K.tube("torneira", [(1.0, -0.15, 1.33), (1.0, -0.25, 1.33), (1.0, -0.28, 1.27)], 0.015, K.m_bronze("bronze_torneira"))
    return dict(tris=10000, tex=1024, colisao=[dict(caixa_m=[2.8, 0.75])], nome="Balcão de taverna (canecas e barril com torneira)", notas="o taverneiro fica atrás (+Y aqui = -Z do Godot)")

def prateleira():
    w = W("madeira_prat", "#6a4426", "#22120a"); rnd = random.Random(5)
    for x in (-0.7, 0.7): box(f"lado{x}", (0.04, 0.36, 1.9), (x, 0, 0.95), w, 0.008)
    box("fundo", (1.42, 0.02, 1.9), (0, 0.17, 0.95), W("madeira_fundo", "#4a2e18", "#1a0e06", 3), 0)
    for k, z in enumerate((0.05, 0.5, 0.95, 1.4, 1.86)): box(f"tabua{k}", (1.4, 0.36, 0.03), (0, 0, z), w, 0.006)
    glass = K.m_plain("vidro_garrafa", "#2a5a3a", 0.15); glass2 = K.m_plain("vidro_garrafa2", "#6a3a1a", 0.15); pot = K.m_plain("ceramica_prat", "#a5583a", 0.6, bumpscale=30)
    for k, z in enumerate((0.065, 0.515, 0.965, 1.415)):
        x = -0.62
        while x < 0.6:
            kind = rnd.choice(["livro", "livro", "garrafa", "pote", "jarro"])
            if kind == "livro":
                n = rnd.randint(3, 6)
                for j in range(n):
                    h = rnd.uniform(0.2, 0.3); t = rnd.uniform(0.03, 0.05)
                    box(f"livro{k}_{x:.2f}_{j}", (t, 0.22, h), (x + j * (t + 0.003), 0.02, z + h / 2), K.m_plain(f"capa{rnd.randint(0,5)}", rnd.choice(["#6a2a22", "#2a4a6a", "#4a5a2a", "#5a3a5a", "#7a5a2a", "#3a3a3a"]), 0.7), 0.004)
                x += n * 0.05 + 0.04
            elif kind == "garrafa":
                g = K.lathe("garrafa", [(0, 0), (0.04, 0), (0.042, 0.15), (0.015, 0.21), (0.015, 0.26), (0, 0.26)], 12, rnd.choice([glass, glass2])); g.location = (x + 0.04, 0, z); x += 0.11
            elif kind == "pote":
                p = K.lathe("pote", [(0, 0), (0.07, 0), (0.08, 0.06), (0.07, 0.13), (0.05, 0.14), (0.055, 0.16), (0, 0.16)], 16, pot); p.location = (x + 0.08, 0, z); x += 0.18
            else:
                p = K.lathe("jarro", [(0, 0), (0.06, 0), (0.09, 0.1), (0.07, 0.2), (0.04, 0.25), (0.05, 0.28), (0, 0.28)], 16, pot); p.location = (x + 0.09, 0, z); x += 0.2
    return dict(tris=12000, tex=1024, colisao=[dict(caixa_m=[1.45, 0.4])], nome="Estante com livros, garrafas e potes", notas="encostar na parede: fundo em +Y aqui (-Z do Godot)")

def lareira():
    st = M_stone("pedra_lareira", "#8a8278", "#3a352f", 2); soot = K.m_plain("fuligem", "#151210", 0.95, bumpscale=20)
    w = W("madeira_lenha", "#5a3a20", "#1e1008", 4); ember = K.m_emit("brasa_lareira", "#ff6a1a", 5); fire = K.m_emit("fogo_lareira", "#ffb347", 7)
    rnd = random.Random(2)
    def blocks(x0, x1, z0, z1, y, d, nm):
        z = z0; row = 0
        while z < z1 - 1e-3:
            h = min(rnd.uniform(0.16, 0.22), z1 - z); x = x0 - (0.1 if row % 2 else 0)
            while x < x1 - 1e-3:
                L = min(rnd.uniform(0.25, 0.4), x1 - x)
                if L > 0.05: box(f"{nm}{row}_{x:.2f}", (L - 0.012, d, h - 0.012), (x + L / 2, y, z + h / 2), st, 0.02)
                x += L
            z += h; row += 1
    blocks(-0.9, -0.45, 0, 1.2, 0, 0.7, "pil_e"); blocks(0.45, 0.9, 0, 1.2, 0, 0.7, "pil_d")
    blocks(-0.9, 0.9, 1.2, 1.45, 0, 0.7, "verga"); box("console", (2.0, 0.85, 0.1), (0, -0.05, 1.5), st, 0.02)
    blocks(-0.6, 0.6, 1.55, 2.6, 0.1, 0.5, "chamine")
    box("fundo_fogo", (0.9, 0.05, 1.2), (0, 0.32, 0.6), soot, 0); box("chao_fogo", (0.95, 0.65, 0.05), (0, 0, 0.025), soot, 0)
    for k in range(4):
        a = k * 0.9; L = K.tube(f"lenha{k}", [(-0.3, -0.05 + 0.05 * k, 0.1 + 0.03 * (k % 2)), (0.3, 0.05 - 0.05 * k, 0.1 + 0.03 * (k % 2))], 0.06, w)
        L.rotation_euler.z = math.radians(-20 + k * 15)
    for k in range(10):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=rnd.uniform(0.03, 0.06), location=(rnd.uniform(-0.3, 0.3), rnd.uniform(-0.15, 0.15), 0.06)); bpy.context.object.data.materials.append(ember)
    for k in range(5):
        x = -0.2 + k * 0.1; f = K.lathe(f"chama{k}", [(0, 0), (0.06, 0.03), (0.05, 0.15), (0.02, 0.3), (0, 0.38)], 8, fire); f.location = (x, 0, 0.12); f.scale = (1, 0.5, rnd.uniform(0.7, 1.2))
    for k, x in enumerate((-0.7, -0.5, 0.55, 0.75)):
        K.lathe(f"pote_cons{k}", [(0, 0), (0.05, 0), (0.06, 0.07), (0.04, 0.12), (0, 0.12)], 12, K.m_plain("ceramica_cons", "#8a5a3a", 0.6)).location = (x, -0.1, 1.55)
    return dict(tris=14000, tex=1024, colisao=[dict(caixa_m=[2.0, 0.85])], nome="Lareira de pedra com fogo",
                luz=dict(pos_godot=[0, 0.5, 0.2], cor="#ff8a3a", alcance=6, piscar=True), notas="chamas e brasas emissivas; pôr OmniLight piscando + partículas de fagulha")

def tapete():
    b = K.NB("tapete_trama")
    u = b.n("ShaderNodeSeparateXYZ"); b.l(b.obj(), u.inputs[0])
    bx = b.math('ABSOLUTE', u.outputs[0]); by = b.math('ABSOLUTE', u.outputs[1])
    borda = b.math('GREATER_THAN', b.math('MAXIMUM', b.math('DIVIDE', bx, 1.2), b.math('DIVIDE', by, 0.8)), 0.82)
    losango = b.math('LESS_THAN', b.math('ADD', b.math('DIVIDE', bx, 0.8), b.math('DIVIDE', by, 0.5)), 0.7)
    faixa = b.math('GREATER_THAN', b.wave(14, 0, 0, 'BANDS', 'X'), 0.8)
    c = b.mix(borda, "#7a2a1a", "#d8b060"); c = b.mix(losango, c, "#2a3a5a"); c = b.mix(b.math('MULTIPLY', faixa, borda), c, "#2a1a10")
    c = b.mix(b.math('MULTIPLY', b.noise(12, 5), 0.35), c, "#3a2a20")
    weave = b.math('MULTIPLY', b.wave(500, 0, 0, 'BANDS', 'X'), b.wave(500, 0, 0, 'BANDS', 'Y'))
    m = b.set(base=c, rough=0.95, sheen=0.8, normal=b.bump(weave, 0.3, 0.002))
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=48, y_subdivisions=32, size=1); t = bpy.context.object; t.scale = (2.4, 1.6, 1); t.data.materials.append(m); K.apply_all(t)
    rnd = random.Random(1)
    for v in t.data.vertices: v.co.z = 0.012 + 0.006 * math.sin(v.co.x * 5 + 1) * math.sin(v.co.y * 4) + (0.02 if abs(v.co.x) > 1.12 and rnd.random() < 0.3 else 0)
    K.mod(t, 'SOLIDIFY', thickness=0.01)
    fr = K.m_plain("franja", "#d8c8a0", 0.9)
    for s in (-1, 1):
        for k in range(26):
            y = -0.75 + k * 0.06; K.tube(f"franja{s}{k}", [(s * 1.2, y, 0.008), (s * 1.3, y + rnd.uniform(-0.02, 0.02), 0.004)], 0.005, fr)
    return dict(tris=5000, tex=1024, colisao=None, nome="Tapete tecido com losango e franjas", notas="sem colisão; só visual")

def caixas():
    w = W("madeira_caixa", "#8a6038", "#3a240e", 3); i = IRON("ferro_caixa"); sack = K.m_plain("saco_merc", "#a99369", 0.95, bumpscale=80); rope = K.m_plain("corda_merc", "#b89a68", 0.9, bumpscale=120)
    def crate(c, s):
        x, y, z = c
        box("caixa", (s, s, s), (x, y, z + s / 2), w, 0.01)
        for dz in (0.04, s - 0.04): box("cinta", (s + 0.012, s + 0.012, 0.035), (x, y, z + dz), W("madeira_cinta", "#6a4426", "#2a160a", 3), 0.004)
        K.tube("diag", [(x - s / 2 + 0.03, y - s / 2 - 0.006, z + 0.06), (x + s / 2 - 0.03, y - s / 2 - 0.006, z + s - 0.06)], 0.014, w)
        for dx in (-s / 2 + 0.03, s / 2 - 0.03):
            for dz in (0.04, s - 0.04): bpy.ops.mesh.primitive_uv_sphere_add(radius=0.01, location=(x + dx, y - s / 2 - 0.012, z + dz), segments=8, ring_count=4); bpy.context.object.data.materials.append(i)
    crate((-0.35, 0, 0), 0.7); crate((0.4, 0.05, 0), 0.6); crate((-0.3, 0.05, 0.7), 0.5)
    for k, (x, y) in enumerate(((0.45, -0.55), (0.9, -0.2))):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.3, location=(x, y, 0.25), segments=20, ring_count=10); o = bpy.context.object; o.scale = (0.8, 0.65, 0.9); o.data.materials.append(sack); K.displace(o, 0.05, 0.12, 'CLOUDS')
    K.tube("corda", [(-0.7, -0.36, 0.69), (-0.35, -0.37, 0.72), (0.0, -0.36, 0.69)], 0.015, rope)
    return dict(tris=6000, tex=1024, colisao=[dict(caixa_m=[1.6, 0.9])], nome="Pilha de caixas de mercadoria com sacos")

def bau():
    w = W("madeira_bau", "#6a4426", "#22120a", 3); i = IRON("ferro_bau"); g = K.m_gold("ouro_bau", 3)
    box("corpo", (0.9, 0.55, 0.45), (0, 0, 0.225), w, 0.02)
    tampa = K.lathe("tampa", [(0, 0), (0.275, 0), (0.275, 0.02)] + [(0.275 * math.cos(t), 0.02 + 0.12 * math.sin(t)) for t in [j * math.pi / 16 for j in range(1, 17)]], 4, w)
    bpy.data.objects.remove(tampa)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.275, depth=0.9, location=(0, 0, 0.45), rotation=(0, math.radians(90), 0)); t = bpy.context.object
    bm = bmesh.new(); bm.from_mesh(t.data); bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.x > 0.01], context='VERTS'); bm.to_mesh(t.data); bm.free()
    t.data.materials.append(w); t.scale = (1, 1, 1); K.mod(t, 'SOLIDIFY', thickness=0.03)
    for x in (-0.35, 0.0, 0.35):
        box(f"cinta{x}", (0.06, 0.57, 0.46), (x, 0, 0.23), i, 0.005)
        K.tube(f"cinta_t{x}", [(x, 0.28 * math.cos(t_), 0.45 + 0.28 * math.sin(t_)) for t_ in [j * math.pi / 12 for j in range(13)]], 0.02, i)
    box("fecho", (0.1, 0.03, 0.14), (0, -0.29, 0.42), g, 0.008)
    return dict(tris=5000, tex=512, colisao=[dict(caixa_m=[0.95, 0.6])], nome="Baú de madeira com cintas de ferro")

# ------------------------------------------------------------------ puzzle
def alavanca(parte):
    st = M_stone("pedra_alavanca", "#8a8278", "#3a352f", 3); i = IRON("ferro_alavanca"); w = W("madeira_cabo", "#5a3a20", "#1e1008", 6)
    if parte in ("base", "todo"):
        box("bloco", (0.5, 0.4, 0.45), (0, 0, 0.225), st, 0.03)
        box("trilho", (0.08, 0.3, 0.06), (0, 0, 0.47), i, 0.01)
        for s in (-1, 1): box(f"orelha{s}", (0.04, 0.12, 0.14), (s * 0.07, 0, 0.52), i, 0.008)
        K.tube("eixo", [(-0.1, 0, 0.55), (0.1, 0, 0.55)], 0.018, i)
    if parte in ("cabo", "todo"):
        z0 = 0.55 if parte == "todo" else 0.0
        K.tube("haste", [(0, 0, z0), (0, 0, z0 + 0.6)], 0.022, i)
        cyl("pegador", 0.04, 0.16, (0, 0, z0 + 0.66), w, n=12)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.045, location=(0, 0, z0 + 0.76), segments=12, ring_count=6); bpy.context.object.data.materials.append(i)
    nome = dict(base="Alavanca: base de pedra", cabo="Alavanca: cabo (gira em X, de -35° a +35°)", todo="Alavanca (montada, para prévia)")[parte]
    return dict(tris=2500, tex=512, colisao=[dict(caixa_m=[0.5, 0.4])] if parte == "base" else None, nome=nome, sem_previa=parte != "todo",
                notas="cabo: origem no eixo; pôr em (0, 0,55, 0) da base e girar RotationDegrees.X de -35 (desligada) a +35 (ligada)" if parte == "cabo" else None)

def placa(parte):
    st = M_stone("pedra_placa_p", "#8a8278", "#3a352f", 3); rune = K.m_emit("runa_placa", "#5ad8ff", 2.5)
    if parte in ("base", "todo"):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.04)); f = bpy.context.object; f.scale = (1.2, 1.2, 0.08); f.data.materials.append(st); K.apply_all(f)
        bm = bmesh.new(); bm.from_mesh(f.data); bm.to_mesh(f.data); bm.free()
        for (x, y, sx, sy) in ((0, 0.52, 1.2, 0.16), (0, -0.52, 1.2, 0.16), (0.52, 0, 0.16, 0.88), (-0.52, 0, 0.16, 0.88)): box(f"moldura{x}{y}", (sx, sy, 0.1), (x, y, 0.05), st, 0.02)
    if parte in ("tampa", "todo"):
        z0 = 0.06 if parte == "todo" else 0.0
        box("tampa", (0.84, 0.84, 0.08), (0, 0, z0 + 0.04), M_stone("pedra_tampa", "#9a9288", "#4a453f", 4), 0.02)
        for k in range(4):
            a = k * math.pi / 2
            K.tube(f"sulco{k}", [(0.3 * math.cos(a + 0.4), 0.3 * math.sin(a + 0.4), z0 + 0.081), (0.15 * math.cos(a), 0.15 * math.sin(a), z0 + 0.081), (0.3 * math.cos(a - 0.4), 0.3 * math.sin(a - 0.4), z0 + 0.081)], 0.012, rune)
        K.tube("circulo", [(0.1 * math.cos(a), 0.1 * math.sin(a), z0 + 0.081) for a in [j * math.tau / 20 for j in range(21)]], 0.012, rune)
    nome = dict(base="Placa de pressão: moldura", tampa="Placa de pressão: tampa (desce 6 cm quando pisada; runas acendem)", todo="Placa de pressão (montada)")[parte]
    return dict(tris=2500, tex=512, colisao=None, nome=nome, sem_previa=parte != "todo",
                notas="tampa: pôr em (0, 0,06, 0) e animar Y até 0,0 ao pisar; trocar EmissionEnergy das runas de 0,3 para 3" if parte == "tampa" else None)

def runa(parte):
    st = M_stone("pedra_runa", "#7a7470", "#2a2622", 2)
    if parte in ("pedra", "todo"):
        p = K.lathe("pilar", [(0, 0), (0.42, 0), (0.42, 0.12), (0.34, 0.18), (0.3, 1.6), (0.36, 1.68), (0.36, 1.78), (0, 1.8)], 6, st)
        for pp in p.data.polygons: pp.use_smooth = False
        K.displace(p, 0.015, 0.08, 'CLOUDS')
    if parte in ("brilho", "todo"):
        g = K.m_emit("runa_brilho", "#7affd8", 6)
        y = -0.27
        strokes = [[(0, 1.4), (0, 0.6)], [(0, 1.3), (0.13, 1.15)], [(0, 1.15), (-0.13, 1.0)], [(0, 0.95), (0.12, 0.8)], [(-0.12, 0.7), (0.12, 0.7)]]
        for k, st_ in enumerate(strokes):
            K.tube(f"traco{k}", [(x, y, z) for x, z in st_], 0.018, g)
        K.tube("anel_runa", [(0.2 * math.cos(a), y, 1.0 + 0.45 * math.sin(a)) for a in [j * math.tau / 32 for j in range(33)]], 0.012, g)
    nome = dict(pedra="Pilar de runa (pedra)", brilho="Runa que acende (traços emissivos)", todo="Pilar de runa (montado, aceso)")[parte]
    return dict(tris=3000, tex=512, colisao=[dict(raio_m=0.42)] if parte == "pedra" else None, nome=nome, sem_previa=parte != "todo",
                luz=dict(pos_godot=[0, 1.0, 0.5], cor="#7affd8", alcance=3) if parte != "pedra" else None,
                notas="brilho: usar posicao_no_conjunto_godot; apagada = Visible false ou EmissionEnergy 0; acender com tween 0→6" if parte == "brilho" else None)

PECAS = dict(movel_mesa=mesa, movel_cadeira=cadeira, movel_banqueta=banqueta, movel_cama=cama, movel_balcao=balcao, movel_prateleira=prateleira,
             movel_lareira=lareira, movel_tapete=tapete, movel_caixas=caixas, movel_bau=bau,
             puzzle_alavanca=lambda: alavanca("todo"), puzzle_alavanca_base=lambda: alavanca("base"), puzzle_alavanca_cabo=lambda: alavanca("cabo"),
             puzzle_placa=lambda: placa("todo"), puzzle_placa_base=lambda: placa("base"), puzzle_placa_tampa=lambda: placa("tampa"),
             puzzle_runa=lambda: runa("todo"), puzzle_runa_pedra=lambda: runa("pedra"), puzzle_runa_brilho=lambda: runa("brilho"))
if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    os.makedirs(args[0], exist_ok=True); BP.construir(PECAS, args[0], args[1:])

"""Armas lendárias (referência do dono 14/11: "Armas Lendárias"): 10 tipos x 5 elementos = 50 armas.
Tipos: espada, machado_gigante, foice, garfo (tridente), espada_dupla (lâmina em cima e embaixo), foco_natural, cajado, arco, lanca, manopla.
Elementos: sagrado (prata/azul), fogo (rubro/ouro), vazio (negro/roxo), gelo (prata/ciano, lâmina de cristal), celestial (ouro/branco).
Convenção do jogo (GearFactory), igual ao build_arsenal: EMPUNHADURA NA ORIGEM, ponta para +Z (= +Y Godot), largura em X, espessura em Y.
Machado e foice: cabeça para +X. Arco: barriga para -Y, corda em y=+0,02, pegada em y=-0,14 (igual ao BowModel).
Manopla: origem no punho (osso da mão), dedos para +Z, costas da mão para -Y.
Uso: python3 build_armas_lendarias.py <saida> [ids...] [--rapido] [--sem-glb]"""
import sys, os, math, random, json
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh
from mathutils import Vector, Matrix, Quaternion
import itemkit as K, org

args = [a for a in sys.argv[1:] if not a.startswith("--")]; OUT = args[0]; SEL = args[1:]
FAST = "--rapido" in sys.argv; NOGLB = "--sem-glb" in sys.argv
os.makedirs(OUT + "/icones", exist_ok=True); os.makedirs(OUT + "/modelos", exist_ok=True)
V = Vector

# ====================================================================== elementos
def M_lamina_metal(name, base, wear, rough=0.16):
    return K.m_metal(name, base, rough, wear=wear, dirt="#20222a", scratch=0.25, scale=4)

def elemento(e):
    if e == "sagrado":
        return dict(nome="Sagrada", lam=M_lamina_metal("lam_sag", "#d4dae6", "#ffffff"), trim=K.m_silver("prata_sag", 4), trim2=K.m_gold("ouro_sag", 4),
                    gem=K.m_crystal("gema_sag", "#bfe6ff", "#3aa8ff", 3.5), glow=K.m_emit("luz_sag", "#7ac8ff", 5), grip=K.m_leather("couro_sag", "#1d3f8a", "#0a1a40", 6),
                    cor=(120, 190, 255), acento="asas")
    if e == "fogo":
        return dict(nome="de Fogo", lam=M_lamina_metal("lam_fogo", "#3a1612", "#c05a30", 0.3), trim=K.m_gold("ouro_fogo", 4), trim2=K.m_metal("ferro_fogo", "#2a1a16", 0.35, wear="#a06040", scale=4),
                    gem=K.m_crystal("gema_fogo", "#ffb060", "#ff3a00", 5), glow=K.m_emit("chama_fogo", "#ff5a10", 4), grip=K.m_leather("couro_fogo", "#4a0c08", "#140302", 6),
                    cor=(255, 110, 40), acento="chamas")
    if e == "vazio":
        return dict(nome="do Vazio", lam=M_lamina_metal("lam_vazio", "#1a1426", "#8a70c0", 0.22), trim=K.m_metal("prata_vazio", "#6a6080", 0.22, wear="#d8c8ff", scale=4),
                    trim2=K.m_metal("negro_vazio", "#120c18", 0.3, wear="#5a4a70", scale=4),
                    gem=K.m_crystal("gema_vazio", "#f0c8ff", "#a020ff", 5), glow=K.m_emit("luz_vazio", "#b040ff", 5), grip=K.m_leather("couro_vazio", "#1a0a2a", "#06020c", 6),
                    cor=(190, 90, 255), acento="espinhos")
    if e == "gelo":
        return dict(nome="de Gelo", lam=K.m_crystal("lam_gelo", "#c8f4ff", "#40c8ff", 1.6), trim=K.m_silver("prata_gelo", 4), trim2=K.m_metal("aco_gelo", "#8aa8c8", 0.2, wear="#ffffff", scale=4),
                    gem=K.m_crystal("gema_gelo", "#e8fbff", "#5ad8ff", 4), glow=K.m_emit("luz_gelo", "#8ae8ff", 4), grip=K.m_leather("couro_gelo", "#cfe6f5", "#6a90b0", 6),
                    cor=(140, 230, 255), acento="cristais")
    return dict(nome="Celestial", lam=M_lamina_metal("lam_cel", "#f0d080", "#fffbe6", 0.18), trim=K.m_gold("ouro_cel", 4), trim2=K.m_metal("ouro2_cel", "#f0c862", 0.18, wear="#ffffff", scale=4),
                gem=K.m_crystal("gema_cel", "#fff4c0", "#ffb020", 5), glow=K.m_emit("luz_cel", "#ffd060", 5), grip=K.m_leather("couro_cel", "#f4efe2", "#b8ae98", 6),
                cor=(255, 210, 110), acento="sol")

# ====================================================================== peças
def perfil(name, L, wfn, thick, m, z0=0.0, single=False, curve=0.0, N=28, xoff=None):
    """lâmina pelo perfil: wfn(t) -> meia-largura; diamante (dois gumes) ou cunha (um gume em +X)"""
    bm = bmesh.new(); rows = []
    for i in range(N + 1):
        t = i / N; z = z0 + t * L; w = max(0.0005, wfn(t)); cx = curve * (t ** 2) * L + (xoff(t) if xoff else 0.0)
        th = thick * (0.35 + 0.65 * min(1.0, w / max(1e-4, wfn(0.0))))
        if single: pts = [(cx - w, -th / 2, z), (cx - w, th / 2, z), (cx + w, 0.0, z)]
        else: pts = [(cx - w, 0, z), (cx, -th / 2, z), (cx + w, 0, z), (cx, th / 2, z)]
        rows.append([bm.verts.new(p) for p in pts])
    n = len(rows[0])
    for a, b_ in zip(rows, rows[1:]):
        for k in range(n): bm.faces.new((a[k], a[(k + 1) % n], b_[(k + 1) % n], b_[k]))
    bm.faces.new(list(reversed(rows[0])))
    tipv = bm.verts.new((curve * L + (xoff(1.0) if xoff else 0.0), 0, z0 + L + 0.003))
    for k in range(n): bm.faces.new((rows[-1][k], rows[-1][(k + 1) % n], tipv))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return K.obj_from_bm(bm, name, m, smooth=False)

def gume_aceso(name, L, wfn, thick, m, z0=0.0, curve=0.0, lados=(-1, 1), t0=0.05, t1=0.93, xoff=None):
    """filete brilhando rente ao gume (lado -X e/ou +X)"""
    for s in lados:
        pts = []
        for i in range(17):
            t = t0 + (t1 - t0) * i / 16
            x = curve * (t ** 2) * L + (xoff(t) if xoff else 0.0) + s * (wfn(t) * 0.82)
            pts.append((x, -thick * 0.28, z0 + t * L))
        K.tube(f"{name}{s}", pts, 0.0028, m)
        K.tube(f"{name}{s}b", [(p[0], -p[1], p[2]) for p in pts], 0.0028, m)

def runa_central(name, L, m, z0, thick, t0=0.08, t1=0.6, xoff=None):
    for s in (-1, 1):
        K.tube(f"{name}{s}", [((xoff(t) if xoff else 0.0), s * thick * 0.42, z0 + L * t) for t in (t0, (t0 + t1) / 2, t1)], 0.0035, m)

def wrap(name, z0, z1, r, m, turns=7):
    c = K.lathe(name, [(0, z0), (r, z0), (r * 1.04, (z0 + z1) / 2), (r, z1), (0, z1)], 16, m)
    pts = [((r + 0.003) * math.cos(a), (r + 0.003) * math.sin(a), z0 + (z1 - z0) * a / (turns * math.tau)) for a in [j * 0.35 for j in range(int(turns * math.tau / 0.35) + 1)]]
    K.tube(name + "_tira", pts, 0.0035, m)
    return c

def pomo(name, z, r, m, gem=None, ponta=0.0):
    K.lathe(name, [(0, z - r * 1.2 - ponta), (r * 0.5, z - r * 1.1), (r, z - r * 0.3), (r * 0.7, z + r * 0.4), (r * 0.45, z + r * 0.7), (0, z + r * 0.75)], 20, m)
    if gem is not None:
        for s in (-1, 1):
            g = K.gem(f"{name}_gema{s}", gem, 1.0, n=8); g.scale = (r * 0.5,) * 3; g.rotation_euler = (math.radians(90 * s), 0, 0); g.location = (0, s * r * 0.75, z - r * 0.2)

def anel(name, z, r, m, esp=0.007):
    K.lathe(name, [(r, z - esp), (r * 1.18, z - esp * 0.5), (r * 1.18, z + esp * 0.5), (r, z + esp)], 20, m, close_top=False, close_bot=False)

def haste(name, z0, z1, r, m, aneis=(), m_anel=None):
    K.lathe(name, [(0, z0), (r * 1.05, z0), (r, z0 + 0.03), (r * 0.94, (z0 + z1) / 2), (r, z1 - 0.03), (r * 1.05, z1), (0, z1)], 16, m)
    for z in aneis: anel(f"{name}_anel{z:.2f}", z, r, m_anel or m)

def gema(name, m, r, loc, normal=(0, -1, 0), moldura=None):
    g = K.gem(name, m, 1.0, n=10); g.scale = (r, r, r * 0.8); g.rotation_mode = 'QUATERNION'; g.rotation_quaternion = V((0, 0, 1)).rotation_difference(V(normal)); g.location = loc
    if moldura is not None:
        bpy.ops.mesh.primitive_torus_add(major_radius=r * 1.08, minor_radius=r * 0.22, location=loc, major_segments=24, minor_segments=8)
        t = bpy.context.active_object; t.rotation_mode = 'QUATERNION'; t.rotation_quaternion = V((0, 0, 1)).rotation_difference(V(normal)); t.data.materials.append(moldura)
    return g

def placa(name, pts2d, esp, m, y=0.0, bevel=0.002, eixo="XZ"):
    """chapa recortada: contorno 2D (x,z) extrudado em Y"""
    bm = bmesh.new(); vs = [bm.verts.new((x, y - esp / 2, z)) for x, z in pts2d]; f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, verts=[e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)], vec=(0, esp, 0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = K.obj_from_bm(bm, name, m, smooth=False)
    if bevel: K.bevel(o, bevel, 1, 30)
    return o

def lasca(name, base, eixo, comp, larg, m, curva=0.0, forma="pena", lado=None, esp=0.006, seg=8):
    """lâmina fina afinando (pena, chama, espinho chato, folha) — enfeites de guarda e cabeça"""
    eixo = V(eixo).normalized(); lado = (V(lado) if lado else (V((0, 1, 0)).cross(eixo) if abs(eixo.y) < 0.9 else V((1, 0, 0)))).normalized()
    nrm = eixo.cross(lado).normalized(); bm = bmesh.new(); rows = []
    for i in range(seg + 1):
        t = i / seg
        if forma == "pena": w = larg * (math.sin(math.pi * min(1.0, t * 0.92 + 0.08)) ** 0.55) * (1 - 0.35 * t)
        elif forma == "chama": w = larg * (1 - t) ** 0.9 * (1 + 0.3 * math.sin(t * 9))
        elif forma == "folha": w = larg * math.sin(math.pi * t) ** 0.8
        else: w = larg * (1 - t) ** 1.1
        d = eixo * comp * t + nrm * curva * comp * t * t
        if forma == "chama": d += lado * larg * 0.7 * math.sin(t * 6) * t
        rows.append([bm.verts.new(V(base) + d + lado * w * k) for k in (-1, 0, 1)])
    for i in range(seg):
        for j in range(2): bm.faces.new([rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    o.data.materials.append(m); K.mod(o, 'SOLIDIFY', thickness=esp); K.apply_all(o)
    return o

def espinho(name, a, b, r, m):
    o = org.spike(name, V(a), V(b), r); o.data.materials.append(m); return o

def cristal(name, base, eixo, h, m, r=None, seed=0):
    o = K.crystal(name, m, r or h * 0.2, h, tip=0.45, sides=6, seed=seed)
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = V((0, 0, 1)).rotation_difference(V(eixo).normalized()); o.location = base
    return o

def acentos(E, nome, centro, eixo_out, tam=1.0, n=3, plano_y=0.0, seed=1):
    """enfeite do elemento saindo de um ponto (guarda, cabeça): asas, chamas, espinhos curvos, cristais, raios de sol"""
    rnd = random.Random(seed); c = V(centro); out = V(eixo_out).normalized()
    up = V((0, 0, 1))
    for k in range(n):
        f = (k / max(1, n - 1) - 0.5) if n > 1 else 0.0
        d = (out + up * (0.35 + f * 1.1)).normalized()
        if E["acento"] == "asas":
            lasca(f"{nome}{k}", c, d, (0.09 + 0.03 * (n - k)) * tam, 0.022 * tam, E["trim"] if k % 2 else E["trim2"], forma="pena", lado=(0, 1, 0), esp=0.006)
        elif E["acento"] == "chamas":
            lasca(f"{nome}{k}", c + V((0, 0, 0.005 * k)), (d + up * 0.6).normalized(), rnd.uniform(0.07, 0.12) * tam, 0.02 * tam, E["glow"], forma="chama", lado=(0, 1, 0), esp=0.004)
        elif E["acento"] == "espinhos":
            p1 = c + d * 0.06 * tam; p2 = p1 + (d + up * 0.5).normalized() * 0.05 * tam
            o = K.tube(f"{nome}{k}", [tuple(c), tuple(p1), tuple(p2)], 0.009 * tam, E["trim2"], taper=[1, 0.6, 0.05])
        elif E["acento"] == "cristais":
            cristal(f"{nome}{k}", c, d, rnd.uniform(0.05, 0.1) * tam, E["lam"], seed=seed * 9 + k)
        else:
            lasca(f"{nome}{k}", c, d, (0.08 + 0.025 * ((k + 1) % 2)) * tam, 0.018 * tam, E["trim"], forma="espinho", lado=(0, 1, 0), esp=0.007)

def guarda_ornada(E, z, larg, tam=1.0, nome="guarda"):
    """guarda: barra central com frisos + enfeites do elemento para os dois lados + gema no meio"""
    pts = [(-larg / 2, 0, z + 0.025 * tam), (-larg / 4, 0, z + 0.004), (0, 0, z), (larg / 4, 0, z + 0.004), (larg / 2, 0, z + 0.025 * tam)]
    g = K.tube(nome, pts, 0.013 * tam, E["trim"], taper=[0.6, 1.0, 1.4, 1.0, 0.6]); g.scale = (1, 1.4, 1)
    K.lathe(nome + "_centro", [(0, z - 0.03 * tam), (0.03 * tam, z - 0.02 * tam), (0.034 * tam, z + 0.01 * tam), (0.02 * tam, z + 0.03 * tam), (0, z + 0.035 * tam)], 16, E["trim2"])
    gema(nome + "_gema", E["gem"], 0.018 * tam, (0, -0.026 * tam, z + 0.004), moldura=E["trim"])
    gema(nome + "_gema_b", E["gem"], 0.018 * tam, (0, 0.026 * tam, z + 0.004), normal=(0, 1, 0), moldura=E["trim"])
    for s in (-1, 1):
        acentos(E, f"{nome}_ac{s}", (s * larg * 0.42, 0, z + 0.02 * tam), (s, 0, 0.15), tam=tam * 1.1, n=3, seed=3 + s)

def lamina_do_elemento(E, nome, L, w0, z0, thick=0.012):
    """perfil por elemento: fogo = flamberge ondulada; gelo = cristal serrilhado; vazio = espinhos no gume; sagrado/celestial = folha larga elegante"""
    a = E["acento"]
    if a == "chamas":   wfn = lambda t: w0 * (1 - 0.35 * t) * (1 + 0.16 * math.sin(t * 22)) * (min(1.0, (1 - t) / 0.16) ** 0.8)
    elif a == "cristais": wfn = lambda t: w0 * (1 - 0.3 * t) * (1 + 0.22 * ((t * 9) % 1.0)) * (min(1.0, (1 - t) / 0.2) ** 0.9)
    elif a == "espinhos": wfn = lambda t: w0 * (1 - 0.2 * t) * (1 + 0.3 * max(0.0, math.sin(t * 15)) ** 6) * (min(1.0, (1 - t) / 0.22) ** 0.9)
    elif a == "sol":    wfn = lambda t: w0 * (1.25 - 0.55 * t + 0.25 * math.sin(t * math.pi) ) * (min(1.0, (1 - t) / 0.18) ** 0.85)
    else:               wfn = lambda t: w0 * (1.1 - 0.4 * t + 0.15 * math.sin(t * math.pi)) * (min(1.0, (1 - t) / 0.15) ** 0.85)
    perfil(nome, L, wfn, thick, E["lam"], z0=z0)
    if a != "cristais": gume_aceso(nome + "_gume", L, wfn, thick, E["glow"], z0=z0)
    runa_central(nome + "_runa", L, E["glow"], z0, thick)
    return wfn

# ====================================================================== tipos
def espada(E):
    lamina_do_elemento(E, "lamina", 0.98, 0.068, 0.17, 0.018)
    guarda_ornada(E, 0.15, 0.42, 1.6)
    wrap("cabo", -0.22, 0.12, 0.022, E["grip"], 10); anel("anel_cabo", 0.0, 0.022, E["trim"])
    pomo("pomo", -0.25, 0.042, E["trim"], E["gem"], ponta=0.03)
    return dict(nome="Espada", tipo="espada", grip="uma mão", size=1.35)

def machado_gigante(E):
    haste("cabo", -0.45, 1.05, 0.03, E["trim2"], aneis=(0.0, 0.55, 0.95), m_anel=E["trim"])
    wrap("cabo_couro", -0.42, 0.08, 0.026, E["grip"], 9)
    for s in (1, -1):    # lâmina dupla em meia-lua enorme
        R = 0.3; zc = 0.83
        edge = [(0.09 + R * math.cos(math.radians(g)), zc + R * 1.05 * math.sin(math.radians(g))) for g in range(-70, 71, 7)]
        prof = [(0.03, zc - 0.08), (0.08, zc - 0.11), (0.13, zc - 0.2)] + edge + [(0.14, zc + 0.24), (0.08, zc + 0.12), (0.03, zc + 0.09)]
        prof = [(x * s, z) for x, z in prof]
        if s < 0: prof = prof[::-1]
        o = placa(f"lamina{s}", prof, 0.022, E["lam"]);
        for v in o.data.vertices:
            if abs(v.co.x) > 0.2: v.co.y *= 0.3 + 0.7 * max(0.0, 1 - (abs(v.co.x) - 0.2) / 0.2)
        K.tube(f"gume{s}", [(s * (0.09 + (R + 0.004) * math.cos(math.radians(g))), -0.004, zc + (R + 0.004) * 1.05 * math.sin(math.radians(g))) for g in range(-66, 67, 12)], 0.004, E["glow"])
        acentos(E, f"ac_mach{s}", (s * 0.1, 0, zc + 0.12), (s, 0, 0.6), tam=1.3, n=3, seed=10 + s)
    K.lathe("olho", [(0, 0.7), (0.042, 0.7), (0.05, 0.83), (0.042, 0.95), (0, 0.95)], 16, E["trim"])
    gema("gema_olho", E["gem"], 0.03, (0, -0.048, 0.83), moldura=E["trim2"]); gema("gema_olho_b", E["gem"], 0.03, (0, 0.048, 0.83), normal=(0, 1, 0), moldura=E["trim2"])
    lamina_do_elemento(E, "ponta_topo", 0.2, 0.025, 0.95, 0.012)
    pomo("pomo", -0.47, 0.032, E["trim"], None, ponta=0.05)
    return dict(nome="Machado Gigante", tipo="machado", grip="duas mãos", size=1.6)

def foice(E):
    haste("cabo", -0.7, 1.05, 0.028, E["trim2"], aneis=(-0.2, 0.4, 0.95), m_anel=E["trim"])
    wrap("cabo_couro", -0.15, 0.2, 0.024, E["grip"], 6)
    # lâmina curva saindo do topo para +X e caindo
    N = 22; L = 0.85
    def ponto(t): return V((0.03 + 0.58 * (1 - math.cos(math.radians(150 * t))) * 0.95, 0, 1.0 + 0.42 * math.sin(math.radians(150 * t)) - 0.3 * t))
    bm = bmesh.new(); rows = []
    for i in range(N + 1):
        t = i / N; p = ponto(t); w = 0.13 * (1 - t) ** 0.7 + 0.005
        nx = (ponto(min(1, t + 0.02)) - ponto(max(0, t - 0.02))).normalized(); ny = V((0, 1, 0)).cross(nx).normalized()
        th = 0.02 * (1 - 0.6 * t)
        rows.append([bm.verts.new(p - ny * w * 0.1 + V((0, -th / 2, 0))), bm.verts.new(p - ny * w * 0.1 + V((0, th / 2, 0))), bm.verts.new(p - ny * w)])
    for a, b_ in zip(rows, rows[1:]):
        for k in range(3): bm.faces.new((a[k], a[(k + 1) % 3], b_[(k + 1) % 3], b_[k]))
    bm.faces.new(list(reversed(rows[0]))); bm.faces.new(rows[-1]); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    K.obj_from_bm(bm, "lamina", E["lam"], smooth=False)
    pts = []
    for i in range(18):
        t = 0.04 + 0.9 * i / 17; p = ponto(t); nx = (ponto(min(1, t + 0.02)) - ponto(max(0, t - 0.02))).normalized(); ny = V((0, 1, 0)).cross(nx).normalized()
        pts.append(tuple(p - ny * (0.13 * (1 - t) ** 0.7) * 0.92 + V((0, -0.006, 0))))
    K.tube("gume", pts, 0.0035, E["glow"])
    K.lathe("encaixe", [(0, 0.92), (0.035, 0.92), (0.04, 1.02), (0.03, 1.08), (0, 1.1)], 16, E["trim"])
    gema("gema_encaixe", E["gem"], 0.024, (0, -0.04, 1.0), moldura=E["trim2"])
    acentos(E, "ac_foice", (-0.02, 0, 1.06), (-1, 0, 0.8), tam=1.9, n=4, seed=21)
    espinho("ponta_baixo", (0, 0, -0.7), (0, 0, -0.86), 0.02, E["trim"])
    return dict(nome="Foice", tipo="foice", grip="duas mãos", size=1.8)

def garfo(E):
    haste("cabo", -0.6, 1.25, 0.027, E["trim2"], aneis=(-0.1, 0.5, 1.2), m_anel=E["trim"])
    wrap("cabo_couro", -0.12, 0.18, 0.022, E["grip"], 6)
    K.lathe("base_dentes", [(0, 1.22), (0.03, 1.22), (0.04, 1.28), (0.0, 1.3)], 16, E["trim"])
    K.tube("travessa", [(-0.18, 0, 1.37), (-0.09, 0, 1.3), (0, 0, 1.28), (0.09, 0, 1.3), (0.18, 0, 1.37)], 0.022, E["trim"], taper=[0.7, 1, 1.2, 1, 0.7])
    for x, h in ((-0.17, 0.38), (0.0, 0.52), (0.17, 0.38)):
        wfn = lambda t: 0.034 * (1 - 0.3 * t) * min(1.0, (1 - t) / 0.3)
        perfil(f"dente{x:.2f}", h, wfn, 0.012, E["lam"], z0=1.33 if x else 1.28, xoff=lambda t, x=x: x * (1 - 0.15 * t))
        if x: espinho(f"farpa{x:.2f}", (x * 0.98, 0, 1.33 + h * 0.75), (x * 1.25, 0, 1.33 + h * 0.62), 0.008, E["lam"])
        gume_aceso(f"gume{x:.2f}", h, wfn, 0.012, E["glow"], z0=1.33 if x else 1.28, xoff=lambda t, x=x: x * (1 - 0.15 * t))
    gema("gema_garfo", E["gem"], 0.036, (0, -0.03, 1.27), moldura=E["trim2"])
    for s in (-1, 1): acentos(E, f"ac_garfo{s}", (s * 0.18, 0, 1.37), (s, 0, 0.2), tam=1.6, n=3, seed=30 + s)
    espinho("conto", (0, 0, -0.6), (0, 0, -0.72), 0.018, E["trim"])
    return dict(nome="Garfo de Guerra", tipo="lanca", grip="duas mãos", size=1.9)

def espada_dupla(E):
    for s, nome in ((1, "cima"), (-1, "baixo")):
        z0 = 0.2 if s > 0 else -0.2
        wfn = lamina_do_elemento(E, f"lamina_{nome}", 0.72, 0.058, 0.0, 0.016)
        o = bpy.data.objects[f"lamina_{nome}"]
        for nm in [x.name for x in bpy.data.objects if x.name.startswith(f"lamina_{nome}")]:
            ob = bpy.data.objects[nm]
            if s < 0: ob.rotation_euler = (math.pi, 0, 0)
            ob.location = (0, 0, z0)
        guarda_ornada(E, s * 0.18, 0.26, 1.25, nome=f"guarda_{nome}")
    wrap("cabo", -0.17, 0.17, 0.019, E["grip"], 8); anel("anel_meio", 0.0, 0.019, E["trim"])
    gema("gema_meio", E["gem"], 0.02, (0, -0.024, 0.0), moldura=E["trim"])
    return dict(nome="Espada Dupla", tipo="espada_dupla", grip="duas mãos", size=1.8)

def cabeca_cajado(E, z, foco=False):
    """topo do cajado/foco por elemento"""
    a = E["acento"]
    if foco:     # foco natural: galhos enroscando um orbe/flor
        mad = K.m_wood("madeira_foco", "#5a3a20", "#1c1008", 6); folha = K.m_leaf("folha_foco", "#3f7a22", "#9cd84a")
        for k in range(4):
            ang = k / 4 * math.tau
            pts = [(math.cos(ang) * 0.02, math.sin(ang) * 0.02, z - 0.05)]
            for i in range(1, 6):
                t = i / 5; aa = ang + t * 2.2
                pts.append((math.cos(aa) * (0.02 + 0.07 * math.sin(t * math.pi)), math.sin(aa) * (0.02 + 0.07 * math.sin(t * math.pi)), z - 0.05 + 0.22 * t))
            K.tube(f"galho{k}", pts, 0.009, mad, taper=[1, 0.9, 0.8, 0.6, 0.4, 0.15])
        for k in range(6):
            ang = k / 6 * math.tau
            lasca(f"folha{k}", (math.cos(ang) * 0.05, math.sin(ang) * 0.05, z + 0.02), (math.cos(ang), math.sin(ang), 0.6), 0.09, 0.03, folha, curva=-0.3, forma="folha", esp=0.003)
        if a in ("sol", "asas"):      # flor de lótus
            for k in range(10):
                ang = k / 10 * math.tau; d = V((math.cos(ang), math.sin(ang), 1.4 if k % 2 else 0.9))
                lasca(f"petala{k}", (0, 0, z + 0.08), d, 0.08 if k % 2 else 0.065, 0.032, K.m_silk("petala", "#ffb8d8" if a == "asas" else "#fff0c0", "#d07898" if a == "asas" else "#e0b060"), curva=0.3, forma="folha", esp=0.003)
            gema("miolo", E["gem"], 0.025, (0, 0, z + 0.1), normal=(0, 0, 1))
        else:
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.045, location=(0, 0, z + 0.1), segments=24, ring_count=12); bpy.context.object.data.materials.append(E["gem"])
            for k in range(3): cristal(f"brot{k}", (math.cos(k * 2.1) * 0.03, math.sin(k * 2.1) * 0.03, z + 0.12), (math.cos(k * 2.1), math.sin(k * 2.1), 1.5), 0.05, E["gem"], seed=k)
        return
    if a == "sol":
        bpy.ops.mesh.primitive_torus_add(major_radius=0.1, minor_radius=0.012, location=(0, 0, z + 0.12), rotation=(math.radians(90), 0, 0)); bpy.context.object.data.materials.append(E["trim"])
        for k in range(12):
            ang = k / 12 * math.tau; d = V((math.cos(ang), 0, math.sin(ang)))
            lasca(f"raio{k}", V((0, 0, z + 0.12)) + d * 0.105, d, 0.07 if k % 2 == 0 else 0.045, 0.014, E["trim"], forma="espinho", lado=(0, 1, 0), esp=0.006)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.05, location=(0, 0, z + 0.12), segments=24, ring_count=12); bpy.context.object.data.materials.append(E["gem"])
    elif a == "espinhos":   # orbe do vazio preso por garras + anel
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.055, location=(0, 0, z + 0.12), segments=24, ring_count=12); bpy.context.object.data.materials.append(K.m_plain("vazio_orbe", "#020004", 1.0))
        bpy.ops.mesh.primitive_torus_add(major_radius=0.085, minor_radius=0.006, location=(0, 0, z + 0.12), rotation=(math.radians(70), 0, math.radians(20))); bpy.context.object.data.materials.append(E["glow"])
        for k in range(4):
            ang = k / 4 * math.tau + 0.4
            K.tube(f"garra{k}", [(math.cos(ang) * 0.025, math.sin(ang) * 0.025, z), (math.cos(ang) * 0.08, math.sin(ang) * 0.08, z + 0.1), (math.cos(ang) * 0.04, math.sin(ang) * 0.04, z + 0.21)], 0.009, E["trim2"], taper=[1, 0.7, 0.1])
    elif a == "cristais":
        for k in range(5):
            ang = k / 5 * math.tau
            cristal(f"cris{k}", (math.cos(ang) * 0.02, math.sin(ang) * 0.02, z), (math.cos(ang) * 0.3, math.sin(ang) * 0.3, 1), 0.12 + 0.05 * (k % 2), E["lam"], seed=k)
        cristal("cris_meio", (0, 0, z), (0, 0, 1), 0.24, E["lam"], r=0.035, seed=9)
    elif a == "chamas":
        K.tube("anel_fogo", [(math.cos(k / 8 * math.tau) * 0.07, 0, z + 0.12 + math.sin(k / 8 * math.tau) * 0.07) for k in range(9)], 0.01, E["trim"])
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.045, location=(0, 0, z + 0.12), segments=24, ring_count=12); bpy.context.object.data.materials.append(E["gem"])
        for k in range(6): lasca(f"chama{k}", (0, 0, z + 0.14), ((k - 2.5) * 0.25, 0.1 * (k % 2), 1), 0.12 + 0.03 * (k % 3), 0.025, E["glow"], forma="chama", esp=0.004)
    else:   # sagrado: lua crescente com gema
        N = 16
        pts = [(math.cos(math.radians(200 + 280 * i / N)) * 0.1, 0, z + 0.12 + math.sin(math.radians(200 + 280 * i / N)) * 0.1) for i in range(N + 1)]
        K.tube("crescente", pts, 0.016, E["trim"], taper=[0.2 + 0.8 * math.sin(math.pi * i / N) for i in range(N + 1)])
        gema("gema_lua", E["gem"], 0.035, (0, 0, z + 0.12), moldura=E["trim2"])
        for s in (-1, 1): lasca(f"asa_cajado{s}", (s * 0.03, 0, z + 0.02), (s, 0, 0.7), 0.1, 0.025, E["trim2"], forma="pena", lado=(0, 1, 0), esp=0.005)

def escala_novos(antes, piv, k):
    """aumenta (k) tudo o que foi criado depois de 'antes', em volta do ponto piv"""
    piv = V(piv); bpy.context.view_layer.update()
    for o in bpy.context.scene.objects:
        if o.type == 'MESH' and o.name not in antes:
            K.apply_all(o); o.data.transform(o.matrix_world); o.matrix_world = Matrix()
            o.data.transform(Matrix.Translation(piv) @ Matrix.Scale(k, 4) @ Matrix.Translation(-piv))

def cajado(E):
    haste("cabo", -0.75, 0.75, 0.026, E["trim2"], aneis=(-0.4, 0.2, 0.7), m_anel=E["trim"])
    wrap("cabo_couro", -0.1, 0.25, 0.02, E["grip"], 6)
    K.lathe("colar", [(0, 0.72), (0.03, 0.72), (0.036, 0.78), (0.028, 0.82), (0, 0.83)], 16, E["trim"])
    antes = {o.name for o in bpy.context.scene.objects}; cabeca_cajado(E, 0.8); escala_novos(antes, (0, 0, 0.8), 1.8)
    espinho("conto", (0, 0, -0.75), (0, 0, -0.85), 0.016, E["trim"])
    return dict(nome="Cajado", tipo="cajado", grip="duas mãos", size=1.75)

def foco_natural(E):
    mad = K.m_wood("madeira_foco_cabo", "#6a4426", "#22140a", 6)
    pts = [(0.012 * math.sin(i * 1.3), 0.01 * math.cos(i * 1.7), -0.7 + 1.45 * i / 10) for i in range(11)]
    K.tube("cabo", pts, 0.02, mad, taper=[1.1, 1, 0.95, 0.95, 0.9, 0.9, 0.95, 1.0, 1.05, 1.1, 1.15])
    wrap("cabo_couro", -0.1, 0.2, 0.023, E["grip"], 5)
    folha = K.m_leaf("folha_cabo", "#4a8a2a", "#a8e060")
    for k in range(4): lasca(f"folha_cabo{k}", (0.02, 0, 0.35 + 0.08 * k), ((-1) ** k, 0.3, 0.5), 0.07, 0.022, folha, forma="folha", esp=0.003)
    antes = {o.name for o in bpy.context.scene.objects}; cabeca_cajado(E, 0.75, foco=True); escala_novos(antes, (0, 0, 0.72), 1.7)
    return dict(nome="Foco Natural", tipo="cajado", grip="duas mãos", size=1.6)

def arco(E):
    L = 1.4; N = 18; pts, tp = [], []
    for i in range(N + 1):
        u = i / N * 2 - 1; z = u * L / 2
        y = -((1 - u * u) * 0.17 - 0.02) + 0.05 * (abs(u) ** 3)
        pts.append((0, y, z)); tp.append(1.15 - 0.6 * abs(u))
    K.tube("braco_arco", pts, 0.02, E["trim2"], taper=tp)
    for s in (-1, 1):    # lâminas nas pontas (arco de guerra) + filete aceso
        i0 = N if s > 0 else 0; p = V(pts[i0]); q = V(pts[i0 - s * 3])
        d = (p - q).normalized()
        lasca(f"lamina_ponta{s}", p - d * 0.02 + V((0, -0.01, 0)), (d + V((0, -0.6, 0))).normalized(), 0.13, 0.03, E["lam"], forma="espinho", lado=(1, 0, 0), esp=0.008)
        K.tube(f"filete{s}", [(0, pts[N // 2 + s * k][1] - 0.019, pts[N // 2 + s * k][2]) for k in range(2, 8)], 0.004, E["glow"])
        acentos(E, f"ac_arco{s}", (0, pts[N // 2 + s * 5][1] - 0.01, pts[N // 2 + s * 5][2]), (0, -1, s * 0.6), tam=0.9, n=2, seed=40 + s)
    K.tube("corda", [(0, 0.02, -L / 2 + 0.02), (0, 0.02, L / 2 - 0.02)], 0.0025, E["glow"])
    wrap("pegada", -0.08, 0.08, 0.026, E["grip"], 5).location = (0, -0.14, 0)
    for s in (-1, 1): anel(f"reforco{s}", s * 0.095, 0.026, E["trim"]); bpy.data.objects[f"reforco{s}"].location = (0, -0.14, 0)
    gema("gema_arco", E["gem"], 0.028, (0, -0.172, 0.0), moldura=E["trim"])
    return dict(nome="Arco", tipo="arco", grip="mão esquerda", size=L)

def lanca(E):
    haste("haste", -0.65, 1.3, 0.026, E["trim2"], aneis=(0.0, 0.6, 1.25), m_anel=E["trim"])
    wrap("cabo", -0.14, 0.14, 0.02, E["grip"], 6)
    K.lathe("alvado", [(0, 1.24), (0.026, 1.24), (0.03, 1.3), (0.02, 1.36), (0, 1.37)], 16, E["trim"])
    lamina_do_elemento(E, "ponta", 0.5, 0.065, 1.34, 0.018)
    for s in (-1, 1):
        lasca(f"orelha{s}", (s * 0.025, 0, 1.33), (s, 0, 0.35), 0.17, 0.034, E["lam"], forma="espinho", lado=(0, 1, 0), esp=0.01)
        acentos(E, f"ac_lanca{s}", (s * 0.03, 0, 1.3), (s, 0, -0.2), tam=1.6, n=3, seed=50 + s)
    gema("gema_lanca", E["gem"], 0.022, (0, -0.03, 1.3), moldura=E["trim2"])
    espinho("conto", (0, 0, -0.65), (0, 0, -0.78), 0.018, E["trim"])
    return dict(nome="Lança", tipo="lanca", grip="duas mãos", size=2.05)

def manopla(E):
    """manopla de combate: luva de placas sobre a mão + garras/espinhos do elemento (origem no punho, dedos +Z)"""
    K.lathe("punho", [(0.0, -0.13), (0.048, -0.13), (0.052, -0.06), (0.045, 0.0), (0.0, 0.0)], 20, E["trim2"])
    for z in (-0.12, -0.065): anel(f"aro{z:.2f}", z, 0.05, E["trim"])
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1, location=(0, 0, 0.055), segments=24, ring_count=12); o = bpy.context.object; o.scale = (0.052, 0.03, 0.06); o.data.materials.append(E["trim2"])
    pl = placa("costas", [(-0.05, 0.0), (0.05, 0.0), (0.055, 0.08), (0.03, 0.11), (-0.03, 0.11), (-0.055, 0.08)], 0.012, E["trim"], y=-0.03)
    gema("gema_costas", E["gem"], 0.02, (0, -0.042, 0.06), moldura=E["trim2"])
    for k, x in enumerate((-0.033, -0.011, 0.011, 0.033)):
        segs = [(x, -0.01, 0.1), (x * 1.05, -0.02, 0.14), (x * 1.05, -0.022, 0.175), (x * 1.0, -0.012, 0.205)]
        for j in range(3):
            K.lathe(f"falange{k}{j}", [(0, 0), (0.011, 0.0), (0.012, 0.03), (0, 0.032)], 10, E["trim2"]).location = segs[j]
        tip = V(segs[-1]); espinho(f"garra{k}", tip, tip + V((0, -0.025, 0.06)), 0.009, E["lam"])
    K.lathe("polegar", [(0, 0), (0.012, 0), (0.012, 0.05), (0, 0.055)], 10, E["trim2"]).location = (0.055, -0.005, 0.03)
    bpy.data.objects["polegar"].rotation_euler = (0, math.radians(40), 0)
    acentos(E, "ac_manopla", (0, -0.03, -0.04), (0, -1, 0.3), tam=1.0, n=3, seed=60)
    for s in (-1, 1): espinho(f"esp_punho{s}", (s * 0.045, -0.01, -0.08), (s * 0.09, -0.02, -0.05), 0.01, E["lam"])
    return dict(nome="Manopla", tipo="manopla", grip="mão (as duas)", size=0.36)

TIPOS = dict(espada=espada, machado_gigante=machado_gigante, foice=foice, garfo=garfo, espada_dupla=espada_dupla, foco_natural=foco_natural,
             cajado=cajado, arco=arco, lanca=lanca, manopla=manopla)
ELEMENTOS = ["sagrado", "fogo", "vazio", "gelo", "celestial"]
NOME_TIPO = dict(espada="Espada", machado_gigante="Machado Gigante", foice="Foice", garfo="Garfo de Guerra", espada_dupla="Espada Dupla",
                 foco_natural="Foco Natural", cajado="Cajado", arco="Arco", lanca="Lança", manopla="Manopla")
FEM = {"Espada", "Foice", "Espada Dupla", "Lança", "Manopla"}

def nome_arma(tipo, e):
    E = {"sagrado": ("Sagrado", "Sagrada"), "fogo": ("de Fogo", "de Fogo"), "vazio": ("do Vazio", "do Vazio"), "gelo": ("de Gelo", "de Gelo"), "celestial": ("Celestial", "Celestial")}[e]
    n = NOME_TIPO[tipo]; return f"{n} {E[1] if n in FEM else E[0]}"

info_path = OUT + "/_armas_lendarias_info.json"
info = json.load(open(info_path)) if os.path.exists(info_path) else {}
for tipo, fn in TIPOS.items():
    for e in ELEMENTOS:
        aid = f"arma_lend_{tipo}_{e}"
        if SEL and not any(aid.startswith(s) or aid == s for s in SEL): continue
        if "--refazer" not in sys.argv and os.path.exists(f"{OUT}/modelos/{aid}.glb") and os.path.exists(f"{OUT}/icones/ico_{aid[5:]}.png"): continue
        K.reset(); E = elemento(e); cfg = fn(E)
        objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        root = bpy.data.objects.new("giro", None); bpy.context.collection.objects.link(root)
        for o in objs: o.parent = root
        root.rotation_mode = 'ZYX'
        root.rotation_euler = (0, math.radians(-45 if cfg["tipo"] != "manopla" else -15), math.radians(90 if cfg["tipo"] == "arco" else 0))
        K.render_icon(f"{OUT}/icones/ico_{aid[5:]}", cam_rot=(90, 0, 0), glow=E["cor"], samples=24 if FAST else 56, res=320 if FAST else 448, pad=1.08)
        root.rotation_mode = 'XYZ'; root.rotation_euler = (0, 0, 0); bpy.context.view_layer.update()
        for o in objs: mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
        bpy.data.objects.remove(root)
        print("icone ok", aid, flush=True)
        if not NOGLB:
            r = K.export_item(f"{OUT}/modelos/{aid}.glb", None, 512, 7000)
            info[aid] = dict(asset_id=aid, nome=nome_arma(tipo, e), tipo=cfg["tipo"], forma=tipo, elemento=e, empunhadura=cfg["grip"], **r)
            json.dump(info, open(info_path, "w"), indent=1, ensure_ascii=False); print("glb ok", aid, r, flush=True)

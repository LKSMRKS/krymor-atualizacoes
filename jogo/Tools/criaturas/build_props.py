"""Cenario e marcos do lote 3 (Costa jogavel), padrao PBR. Uso: python3 build_props.py <id|all>
Cada peca: malha de jogo + malha alta (subdivisao + relevo por zona) -> bake cor/AO, rugosidade/metal, normal.
Origem no chao, frente -Y no Blender (+Z no glTF), metros. Pecas com colisao geram tambem <id>_colisao.glb (caixas simples)."""
import sys, math, json, random, os
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy, bmesh
from cz import Vector, Matrix
from mathutils import noise

OUT = "/home/claude/cinzas/lote3/cenario/"
# zonas de material
WOOD, DWOOD, IRON, ROPE, HIDE, BONE, CAL, PAINT, COPPER, STONE, GLASS, EMBER, GRASS = range(1, 14)
PAL = {WOOD: ("#7a5434", "#4a3020"), DWOOD: ("#4a3222", "#24180e"), IRON: ("#5e5c5a", "#2a2826"), ROPE: ("#b8a070", "#6a5638"),
       HIDE: ("#7a5636", "#3a2414"), BONE: ("#e6dcc0", "#9a8a6a"), CAL: ("#ece4d2", "#8c8270"), PAINT: ("#a82a1a", "#5a120a"),
       COPPER: ("#4a9a80", "#2a5a4a"), STONE: ("#8a8478", "#4a463e"), GLASS: ("#ffd890", "#c88a30"), EMBER: ("#ff9a30", "#ff5a10"), GRASS: ("#6a7a3a", "#3a4a1e")}
ROUGH = {WOOD: 0.8, DWOOD: 0.85, IRON: 0.42, ROPE: 0.9, HIDE: 0.8, BONE: 0.55, CAL: 0.88, PAINT: 0.7, COPPER: 0.5, STONE: 0.85, GLASS: 0.15, EMBER: 0.4, GRASS: 0.9}
METAL = {IRON: 0.85, COPPER: 0.6}
DET = {WOOD: dict(grain=0.004, grain_scale=40), DWOOD: dict(grain=0.004, grain_scale=40), IRON: dict(grain=0.001, grain_scale=60, pits=0.002),
       ROPE: dict(grain=0.003, grain_scale=120), HIDE: dict(grain=0.003, grain_scale=50, plates=0.004, plate_scale=3.0), BONE: dict(grain=0.0015, grain_scale=80, pits=0.002),
       CAL: dict(grain=0.012, grain_scale=6, pits=0.01, plates=0.03, plate_scale=1.2), STONE: dict(grain=0.008, grain_scale=8, plates=0.02, plate_scale=1.5, pits=0.006),
       PAINT: dict(grain=0.002, grain_scale=40), COPPER: dict(grain=0.002, grain_scale=30), GRASS: dict(grain=0.01, grain_scale=20)}

def Z(o, z): return pbr.zone(o, z)

def finish(pid, objs, size=1024, emit=(), scale_det=1.0, subdiv=2, extr=0.03, collision=None, extra=(), target=None, export=True):
    """objs: pecas ja zoneadas. emit: [(obj, cor, forca)] pecas que brilham (nao vao para o bake)."""
    low = cz.join(pid, objs, flat_shade=False)
    for p in low.data.polygons: p.use_smooth = True
    low.data.set_sharp_from_angle(angle=math.radians(40)) if hasattr(low.data, "set_sharp_from_angle") else None
    hi = pbr.make_hi(low, subdiv=subdiv)
    pbr.detail_zoned(hi, {z: {k: (v * scale_det if k in ("grain", "plates", "pits") else v) for k, v in d.items()} for z, d in DET.items()})
    if target: org.decimate(low, target)          # malha de jogo leve; o detalhe fica no normal assado da malha alta
    col = {z: (org.lin(a), org.lin(b)) for z, (a, b) in PAL.items()}
    def cor(p, n, c, z):
        a, b = col.get(z, col[WOOD])
        if z in (WOOD, DWOOD):     # veio da madeira (alongado no eixo de maior extensao local ~ Z) e nos
            g = noise.noise(Vector((p.x * 18, p.y * 18, p.z * 1.5)))
            k = org.mix(b, a, 0.55 + 0.45 * g)
            if noise.noise(p * 3.0) > 0.45: k = k * 0.75
            return k
        if z in (CAL, STONE):      # estratos horizontais + manchas
            st = noise.noise(Vector((p.x * 0.6, p.y * 0.6, p.z * 5.0)))
            k = org.mix(b, a, 0.6 + 0.4 * st) * (0.9 + 0.15 * noise.noise(p * 7))
            if z == CAL and n.z > 0.6 and noise.noise(p * 0.8 + Vector((3, 1, 2))) > 0.0: k = org.mix(k, col[GRASS][0], 0.35)  # liquen/capim no topo
            if p.z < 0.4: k = org.mix(k, col[STONE][1], 0.4 * (1 - p.z / 0.4))     # sujeira na base
            sk = noise.noise(Vector((p.x * 2.2, p.y * 2.2, p.z * 0.12)))                  # escorridos verticais de agua/ferrugem
            if sk > 0.35 and n.z < 0.5: k = org.mix(k, org.lin("#9a8a6e"), org.sstep(0.35, 0.6, sk) * 0.45)
            return k
        if z == HIDE:              # retalhos costurados (celulas com tons diferentes)
            dist, pts = noise.voronoi(p * 2.2)
            t = (noise.noise(Vector(pts[0]) * 3.1) + 1) / 2
            k = org.mix(b, a, 0.3 + 0.7 * t)
            if dist[1] - dist[0] < 0.04: k = col[ROPE][1] * 0.6           # costura
            return k
        if z == IRON:
            k = org.mix(b, a, 0.5 + 0.5 * noise.noise(p * 9))
            if noise.noise(p * 4 + Vector((7, 7, 7))) > 0.3: k = org.mix(k, org.lin("#7a3a1a"), 0.55)   # ferrugem
            return k
        return org.mix(b, a, 0.6 + 0.4 * noise.noise(p * 10))
    rug = lambda p, n, c, z: max(0.05, min(1.0, ROUGH.get(z, 0.8) + 0.08 * noise.noise(p * 12)))
    met = lambda p, n, c, z: METAL.get(z, 0.0)
    pbr.bake(low, hi, cor, rug, pid, size, extrusion=extr, wear_col=None, dirt_col=org.lin("#1a140e"), metal_fn=met)
    parts = [low]
    for o, c, s in emit:
        cz.apply_tf(o); o.data.materials.clear(); o.data.materials.append(cz.mat(pid + "_emit" + c, c, rough=0.2, emit=c, strength=s)); parts.append(o)
    for o, m_ in extra:
        cz.apply_tf(o); o.data.materials.clear(); o.data.materials.append(m_); parts.append(o)
    if len(parts) > 1:
        bpy.ops.object.select_all(action='DESELECT')
        for o in parts: o.select_set(True)
        bpy.context.view_layer.objects.active = low; bpy.ops.object.join()
    if not export: return low
    os.makedirs(OUT + pid, exist_ok=True)
    cz.export_objs([low], OUT + pid + "/" + pid)
    bpy.ops.wm.save_as_mainfile(filepath=OUT + pid + "/" + pid + ".blend")
    info = dict(asset_id=pid, tris=cz.tri_count([low]), dims_m=[round(d, 2) for d in low.dimensions])
    if collision:
        cols = []
        for i, (sz, loc, yaw) in enumerate(collision):
            b = cz.box(f"col{i}", sz, loc=loc, rot=(0, 0, math.radians(yaw))); cz.apply_tf(b); cols.append(b)
        cj = cz.join(pid + "_colisao", cols)
        low.hide_set(True)
        cz.export_objs([cj], OUT + pid + "/" + pid + "_colisao")
        info["colisao"] = [dict(caixa_m=list(sz), centro_m=list(loc), yaw=yaw) for sz, loc, yaw in collision]
    json.dump(info, open(OUT + pid + "/" + pid + "_info.json", "w"), ensure_ascii=False, indent=1)
    print("OK", pid, info["tris"], info["dims_m"])
    return low

# ------------------------------------------------------------------------------------------------ pecas
def terrace(o, step, jag=0.08):
    """Estratos de rocha: degraus horizontais com bordas irregulares (le como penhasco de cal, nao como nuvem)."""
    for v in o.data.vertices:
        t = v.co.z / step; f = math.floor(t); fr = t - f
        z = (f + org.sstep(0.7, 1.0, fr)) * step
        k = 1.0 + jag * noise.noise(Vector((f * 1.7, 0.3, 0.9)))
        v.co.z = z; v.co.x *= k; v.co.y = v.co.y * (1.0 + jag * 0.5 * noise.noise(Vector((f * 2.3, 1.1, 0.2))))
    o.data.update()

def barril():
    """proj_barril_explosivo: 0,6 m, pavio com brasa; origem no CENTRO (projetil gira no ar)."""
    R, L = 0.24, 0.6; o = []
    b = cz.cyl("corpo", R, L, 20); cz.apply_tf(b)
    for v in b.data.vertices:
        t = v.co.z / (L / 2); k = 1 + 0.13 * (1 - t * t); v.co.x *= k; v.co.y *= k
    o.append(Z(b, WOOD))
    for zz in (-0.22, -0.12, 0.12, 0.22):
        rr = R * (1 + 0.13 * (1 - (zz / (L / 2)) ** 2)) + 0.008
        o.append(Z(cz.cyl("aro", rr, 0.035, 20, loc=(0, 0, zz), cap=False), IRON))
    for i in range(10):    # sulcos das aduelas
        a = i * math.tau / 10
        o.append(Z(cz.box("aduela", (0.012, 0.012, L * 0.98), loc=(math.cos(a) * R * 1.1, math.sin(a) * R * 1.1, 0), rot=(0, 0, a)), DWOOD))
    o.append(Z(cz.cyl("tampa", R * 0.95, 0.02, 20, loc=(0, 0, L / 2)), DWOOD))
    o.append(Z(cz.box("x1", (0.32, 0.03, 0.006), loc=(0, -R * 1.12, 0.02), rot=(math.pi / 2, 0.7, 0)), PAINT))     # marca de perigo pintada
    o.append(Z(cz.box("x2", (0.32, 0.03, 0.006), loc=(0, -R * 1.12, 0.02), rot=(math.pi / 2, -0.7, 0)), PAINT))
    fuse = org.limb("pavio", [((0.05, 0, L / 2), 0.012), ((0.07, 0.02, L / 2 + 0.06), 0.011), ((0.02, 0.05, L / 2 + 0.11), 0.01), ((-0.03, 0.04, L / 2 + 0.14), 0.009)], subdiv=1)
    o.append(Z(fuse, ROPE))
    ember = cz.ico("brasa", 0.022, 1, loc=(-0.03, 0.04, L / 2 + 0.15))
    finish("proj_barril_explosivo", o, size=512, emit=[(ember, "#ff7a20", 8.0)], extr=0.01)

def tenda():
    """cenario_tenda_goblin: tenda de couro remendado em duas aguas, varas cruzadas, cranios e fitas."""
    o = []; L, W, Hh = 3.2, 2.6, 2.1
    me = bpy.data.meshes.new("lona"); bm = bmesh.new()
    nx, ny = 10, 14
    grid = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            u, v = i / nx, j / ny
            x = (u - 0.5) * W * 1.15; y = (v - 0.5) * L
            z = Hh * (1 - abs(u - 0.5) * 2) ** 0.9
            sag = 0.1 * math.sin(u * math.pi * 2) ** 2 * math.sin(v * math.pi)
            z -= sag; z = max(0.02, z)
            if abs(u - 0.5) > 0.45: z = 0.02 + 0.08 * (abs(u - 0.5) - 0.45) / 0.05 * 0
            grid[i, j] = bm.verts.new((x, y, z + 0.05 * noise.noise(Vector((x, y, 0)) * 2)))
    for i in range(nx):
        for j in range(ny): bm.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
    bm.to_mesh(me); lona = cz._link(bpy.data.objects.new("lona", me))
    sol = lona.modifiers.new("s", 'SOLIDIFY'); sol.thickness = 0.04; org._apply_all(lona)
    o.append(Z(lona, HIDE))
    for yy in (-L / 2 - 0.1, L / 2 + 0.1):    # varas cruzadas nas pontas
        for s in (-1, 1):
            o.append(Z(cz.seg("vara", (s * W * 0.55, yy, 0), (-s * 0.25, yy, Hh + 0.55), 0.05, 0.035, 7), WOOD))
        o.append(Z(cz.ball("cranio", 0.12, 10, 7, loc=(0, yy - 0.05 * (1 if yy < 0 else -1), Hh + 0.45), scale=(0.9, 1.1, 0.85)), BONE))
    o.append(Z(cz.seg("cumeeira", (0, -L / 2 - 0.2, Hh + 0.02), (0, L / 2 + 0.2, Hh + 0.02), 0.045, 0.045, 7), DWOOD))
    for s in (-1, 1):
        for yy in (-1.0, 0.0, 1.0):
            o.append(Z(cz.seg("estaca", (s * W * 0.72, yy, 0.25), (s * W * 0.8, yy, -0.05), 0.03, 0.01, 5), WOOD))
    # entrada: aba aberta e fita vermelha
    o.append(Z(cz.box("aba", (0.9, 0.04, 1.4), loc=(0.55, -L / 2 - 0.25, 0.7), rot=(0, 0.25, -0.5)), HIDE))
    o.append(Z(cz.box("fita", (0.08, 0.02, 0.7), loc=(-0.2, -L / 2 - 0.2, Hh - 0.1), rot=(0, 0.2, 0)), PAINT))
    finish("cenario_tenda_goblin", o, size=1024, extr=0.03)

def palicada():
    """cenario_palicada_trecho: 4 m de troncos apontados amarrados; colisao 4 x 0,5 x 2,8."""
    o = []; rnd = random.Random(5); n = 10
    for i in range(n):
        x = -1.85 + i * 3.7 / (n - 1) + rnd.uniform(-0.03, 0.03); r = rnd.uniform(0.17, 0.22); h = rnd.uniform(2.4, 3.0)
        lean = rnd.uniform(-0.04, 0.04)
        t = cz.cyl("tronco", r, h, 10, loc=(x, 0, h / 2), rot=(lean, rnd.uniform(-0.03, 0.03), rnd.uniform(0, 3))); cz.apply_tf(t); cz.jitter(t, 0.015, i)
        o.append(Z(t, WOOD))
        o.append(Z(cz.cyl("ponta", r, 0.45, 10, r2=0.01, loc=(x + lean * h, 0, h + 0.22), rot=(lean, 0, 0)), DWOOD))
    for zz in (0.7, 1.9):
        o.append(Z(cz.seg("travessa", (-2.0, -0.2, zz), (2.0, -0.2, zz + 0.05), 0.08, 0.08, 8), DWOOD))
        for i in range(n):
            x = -1.85 + i * 3.7 / (n - 1)
            o.append(Z(cz.cyl("amarra", 0.21, 0.06, 10, loc=(x, 0, zz), cap=False), ROPE))
    o.append(Z(cz.ball("cranio", 0.13, 10, 7, loc=(0.5, -0.24, 2.0), scale=(0.9, 1.0, 0.85)), BONE))
    finish("cenario_palicada_trecho", o, size=1024, collision=[((4.0, 0.5, 2.8), (0, 0, 1.4), 0)])

def totem():
    """cenario_totem_goblin: tronco entalhado com 3 caras, chifres, cranio no topo, tinta vermelha e fitas."""
    o = []
    parts = [org.limb("tronco", [((0, 0, -0.1), 0.34), ((0, 0, 1.2), 0.3), ((0, 0, 2.5), 0.27), ((0, 0, 3.0), 0.25)], subdiv=2)]
    for k, zz in enumerate((0.7, 1.55, 2.4)):     # caras: testa, nariz, boca aberta, olhos fundos
        parts.append(org.blob(f"testa{k}", (0, -0.25, zz + 0.18), (0.26, 0.1, 0.07), 16, 8))
        parts.append(org.blob(f"nariz{k}", (0, -0.33, zz + 0.02), (0.07, 0.1, 0.12), 12, 6))
        for s in (-1, 1): parts.append(org.blob(f"boch{k}{s}", (s * 0.15, -0.24, zz - 0.06), (0.1, 0.08, 0.08), 12, 6))
    tr = org.fuse("totem_madeira", parts, voxel=0.02); org.displace(tr, 0.02, 0.15, 'CLOUDS', 2)
    for k, zz in enumerate((0.7, 1.55, 2.4)):     # cavar olhos e boca
        for v in tr.data.vertices:
            for c, r_, d in (((-0.11, -0.3, zz + 0.08), 0.06, 0.05), ((0.11, -0.3, zz + 0.08), 0.06, 0.05), ((0, -0.31, zz - 0.17), 0.09, 0.07)):
                dd = (v.co - Vector(c)).length
                if dd < r_ * 1.4: v.co += v.normal * (-d * (1 - dd / (r_ * 1.4)))
    org.decimate(tr, 6000)
    o.append(Z(tr, WOOD))
    o.append(Z(cz.ball("cranio", 0.2, 12, 8, loc=(0, -0.02, 3.18), scale=(0.9, 1.15, 0.85)), BONE))
    for s in (-1, 1):
        o.append(Z(org.limb(f"chifre{s}", [((s * 0.15, 0, 3.2), 0.06), ((s * 0.4, 0.05, 3.35), 0.045), ((s * 0.5, -0.02, 3.65), 0.005)]), BONE))
        o.append(Z(cz.box("fita", (0.06, 0.02, 0.8), loc=(s * 0.3, -0.05, 2.75), rot=(0.1, s * 0.4, 0)), PAINT))
    for zz in (1.15, 2.0):
        o.append(Z(cz.cyl("faixa", 0.31, 0.08, 14, loc=(0, 0, zz), cap=False), PAINT))
    o.append(Z(cz.cyl("base", 0.6, 0.25, 10, r2=0.45, loc=(0, 0, 0.12)), STONE))
    finish("cenario_totem_goblin", o, size=1024, collision=[((0.8, 0.8, 3.0), (0, 0, 1.5), 0)])

def farol():
    """cenario_farol: marco da Costa; base de rocha r 3 (= obstaculo do servidor: circulo r 3, altura 16), torre ~19 m com lanterna acesa."""
    o = []
    rocks = [org.blob(f"rocha{i}", (math.cos(a) * 2.1, math.sin(a) * 2.1, 0.5), (1.35, 1.2, 1.05)) for i, a in enumerate([j * math.tau / 7 for j in range(7)])]
    rocks.append(org.blob("rocha_c", (0, 0, 0.7), (2.4, 2.4, 1.0)))
    base = org.fuse("base", rocks, voxel=0.08); terrace(base, 0.45); org.displace(base, 0.2, 1.2, 'CLOUDS', 3); org.displace(base, -0.12, 0.5, 'VORONOI'); org.decimate(base, 5000)
    for v in base.data.vertices:
        r_ = math.hypot(v.co.x, v.co.y)
        if r_ > 3.0: f = 3.0 / r_; v.co.x *= f; v.co.y *= f
        v.co.z = max(v.co.z, -0.2)
    o.append(Z(base, STONE))
    H0, Ht = 1.4, 13.5
    tower = cz.cyl("torre", 2.1, Ht, 24, r2=1.5, loc=(0, 0, H0 + Ht / 2)); o.append(Z(tower, CAL))
    for k in range(5):    # faixas de pedra escura (leitura de longe: listras)
        zz = H0 + 1.5 + k * 2.6; rr = 2.1 - (zz - H0) / Ht * 0.6 + 0.05
        o.append(Z(cz.cyl("faixa", rr, 0.55, 24, r2=rr - 0.02, loc=(0, 0, zz), cap=False), PAINT if k % 2 else STONE))
    o.append(Z(cz.box("porta", (1.1, 0.25, 2.0), loc=(0, -2.05, H0 + 1.0)), DWOOD))
    o.append(Z(cz.cyl("arco", 0.6, 0.3, 12, loc=(0, -2.05, H0 + 2.0), rot=(math.pi / 2, 0, 0)), STONE))
    for k, zz in enumerate((5.0, 8.5, 11.5)):
        a = 0.8 + k * 2.1; rr = 2.1 - (zz - H0) / Ht * 0.6
        o.append(Z(cz.box("janela", (0.45, 0.2, 0.8), loc=(math.cos(a) * rr, math.sin(a) * rr, zz), rot=(0, 0, a + math.pi / 2)), DWOOD))
    top = H0 + Ht
    o.append(Z(cz.cyl("galeria", 2.2, 0.3, 24, loc=(0, 0, top + 0.15)), STONE))
    for i in range(20):
        a = i * math.tau / 20
        o.append(Z(cz.box("grade", (0.06, 0.06, 0.8), loc=(math.cos(a) * 2.1, math.sin(a) * 2.1, top + 0.7)), IRON))
    o.append(Z(cz.cyl("corrimao", 2.12, 0.06, 24, loc=(0, 0, top + 1.1), cap=False), IRON))
    lamp = cz.cyl("lanterna", 1.05, 1.9, 12, loc=(0, 0, top + 1.25))
    for i in range(12):
        a = i * math.tau / 12
        o.append(Z(cz.box("caixilho", (0.08, 0.08, 1.9), loc=(math.cos(a) * 1.07, math.sin(a) * 1.07, top + 1.25)), COPPER))
    o.append(Z(cz.cyl("cupula", 1.25, 1.3, 16, r2=0.15, loc=(0, 0, top + 2.85)), COPPER))
    o.append(Z(cz.cyl("ponta", 0.12, 0.9, 8, r2=0.0, loc=(0, 0, top + 3.9)), COPPER))
    o.append(Z(cz.ball("cata", 0.18, 8, 6, loc=(0, 0, top + 3.55)), COPPER))
    # escada/rampa na rocha ate a porta
    for k in range(5):
        o.append(Z(cz.box("degrau", (1.3, 0.45, 0.28), loc=(0, -2.55 - k * 0.4, H0 - 0.15 - k * 0.28)), STONE))
    finish("cenario_farol", o, size=2048, emit=[(lamp, "#ffd27a", 6.0)], scale_det=1.0, subdiv=1, extr=0.06,
           collision=[((4.2, 4.2, 16.0), (0, 0, 8.0), 0), ((4.2, 4.2, 16.0), (0, 0, 8.0), 45)])

def entrada_galeria():
    """edificio_entrada_galeria: paredao de cal com boca de mina escorada (abertura 4 x 3,6 m), trilhos entrando e lanternas.
    Colisao: dois blocos laterais e o teto; a abertura fica livre (passagem para o portal da Galeria Funda)."""
    o = []; rnd = random.Random(9)
    blobs = []
    for i in range(16):      # massa de cal em volta da abertura
        x = rnd.choice([-1, 1]) * rnd.uniform(2.6, 6.5); z = rnd.uniform(0.5, 6.5)
        blobs.append(org.blob(f"m{i}", (x, rnd.uniform(0.5, 2.5), z), (rnd.uniform(1.6, 2.6), rnd.uniform(1.5, 2.2), rnd.uniform(1.4, 2.2))))
    for i in range(6):
        x = -3.5 + i * 1.4
        blobs.append(org.blob(f"t{i}", (x, rnd.uniform(0.6, 2.0), rnd.uniform(5.4, 7.5)), (1.9, 1.8, 1.6)))
    cliff = org.fuse("paredao", blobs, voxel=0.1)
    for v in cliff.data.vertices:      # paredao: achata a frente e empurra o topo para tras (face de penhasco)
        if v.co.y < 0.6: v.co.y = 0.6 + (v.co.y - 0.6) * 0.35
    terrace(cliff, 0.55)
    org.displace(cliff, 0.25, 1.4, 'CLOUDS', 3); org.displace(cliff, -0.22, 0.45, 'VORONOI')
    for v in cliff.data.vertices:     # abre a boca (arco) e corta a base no chao
        if abs(v.co.x) < 2.15 and v.co.z < 3.7 + 0.4 * math.cos(v.co.x / 2.15 * math.pi / 2) and v.co.y < 3.6:
            v.co.x = math.copysign(2.15 + 0.1 * abs(noise.noise(v.co)), v.co.x) if v.co.z < 3.5 else v.co.x
            if v.co.z >= 3.5: v.co.z = 3.75 + 0.3 * abs(noise.noise(v.co))
        v.co.z = max(v.co.z, -0.3)
    org.decimate(cliff, 9000); o.append(Z(cliff, CAL))
    # tunel escuro (paredes internas)
    tun = cz.box("tunel", (4.2, 5.0, 3.8), loc=(0, 3.0, 1.9)); cz.apply_tf(tun)
    for f in tun.data.polygons: f.flip() if False else None
    bm = bmesh.new(); bm.from_mesh(tun.data); bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    for f in [f for f in bm.faces if f.normal.y > 0.9]: bm.faces.remove(f)       # tira a parede da frente (normal invertida aponta +Y)
    bm.to_mesh(tun.data); bm.free(); o.append(Z(tun, STONE))
    # escoramento
    for s in (-1, 1):
        o.append(Z(cz.box("esteio", (0.32, 0.32, 3.7), loc=(s * 1.85, -0.1, 1.85), rot=(0, s * 0.03, 0)), DWOOD))
        o.append(Z(cz.box("esteio2", (0.28, 0.28, 3.5), loc=(s * 1.85, 1.8, 1.75)), DWOOD))
        o.append(Z(cz.box("calco", (0.6, 0.5, 0.25), loc=(s * 1.85, -0.1, 0.12)), STONE))
    o.append(Z(cz.box("verga", (4.4, 0.36, 0.38), loc=(0, -0.1, 3.75), rot=(0, 0.02, 0)), DWOOD))
    o.append(Z(cz.box("verga2", (4.0, 0.3, 0.3), loc=(0, 1.8, 3.55)), DWOOD))
    o.append(Z(cz.box("placa", (1.4, 0.08, 0.5), loc=(0, -0.32, 4.2), rot=(0, 0.06, 0)), WOOD))
    # trilhos e dormentes entrando
    for s in (-1, 1): o.append(Z(cz.box("trilho", (0.07, 9.0, 0.08), loc=(s * 0.5, 0.5, 0.12)), IRON))
    for k in range(13): o.append(Z(cz.box("dormente", (1.5, 0.2, 0.1), loc=(0, -3.8 + k * 0.7, 0.05), rot=(0, 0, rnd.uniform(-0.05, 0.05))), DWOOD))
    # vagonete tombado e caixotes na entrada
    o.append(Z(cz.box("vagonete", (1.1, 1.4, 0.75), loc=(-2.8, -1.8, 0.55), rot=(0.0, 0.25, 0.4)), IRON))
    o.append(Z(cz.box("caixote", (0.7, 0.7, 0.7), loc=(2.9, -1.4, 0.35), rot=(0, 0, 0.3)), WOOD))
    o.append(Z(cz.box("caixote2", (0.55, 0.55, 0.55), loc=(3.2, -1.0, 0.98), rot=(0, 0, 0.9)), WOOD))
    lamps = []
    for s in (-1, 1):
        o.append(Z(cz.box("suporte", (0.06, 0.4, 0.06), loc=(s * 2.25, -0.3, 2.9)), IRON))
        o.append(Z(cz.cyl("lanterna", 0.13, 0.32, 8, loc=(s * 2.25, -0.5, 2.7)), IRON))
        lamps.append((cz.cyl("luz", 0.09, 0.22, 8, loc=(s * 2.25, -0.5, 2.7)), "#ffb860", 5.0))
    # fundo escuro do tunel (sem textura, preto)
    fundo = cz.box("fundo", (4.1, 0.05, 3.7), loc=(0, 5.4, 1.85))
    finish("edificio_entrada_galeria", o, size=2048, subdiv=1, emit=lamps + [(fundo, "#000000", 0.0)], extr=0.08,
           collision=[((5.0, 4.0, 7.5), (-4.6, 1.5, 3.75), 0), ((5.0, 4.0, 7.5), (4.6, 1.5, 3.75), 0), ((4.2, 4.0, 3.2), (0, 1.5, 5.6), 0)])

B = dict(proj_barril_explosivo=barril, cenario_tenda_goblin=tenda, cenario_palicada_trecho=palicada, cenario_totem_goblin=totem,
         cenario_farol=farol, edificio_entrada_galeria=entrada_galeria)

if __name__ == "__main__":
    w = sys.argv[-1]
    for k_, f in B.items():
        if w in ("all", k_):
            cz.reset(); cz.clear_mats(); f()

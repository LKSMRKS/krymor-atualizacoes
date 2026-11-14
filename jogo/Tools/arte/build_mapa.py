"""Mapa ilustrado da Costa do Cal a partir do map_costa.json (posicoes reais), visto de cima com os modelos do jogo.
Uso: python3 build_mapa.py <map.json> <saida_dir>
Saida: mapa_costa_do_cal.png (2048², cobre x,y do mapa de -130 a +130; norte = y negativo = topo da imagem),
       mapa_costa_do_cal_nevoa.png (mascara da nevoa das areas nao exploradas, exemplo)."""
import sys, os, json, math, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, pbr
from mathutils import Vector

mp, out = sys.argv[1], sys.argv[2]
M = json.load(open(mp))
EXT = 130.0
CM = "/home/claude/cinzas/costa/modelos/"; L3 = "/home/claude/cinzas/lote3/"; L2 = "/home/claude/cinzas/calL2/"
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
rnd = random.Random(7)
P = lambda x, y, z=0.0: Vector((x, -y, z))          # mapa (x, y) -> Blender (x, -y): y do mapa cresce para baixo na imagem

CACHE = {}
def inst(path, x, y, yaw=0.0, s=(1, 1, 1)):
    from mathutils import Matrix
    if path not in CACHE:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=path)
        new = [o for o in bpy.data.objects if o not in before]
        for o in new: o.hide_render = True; o.hide_viewport = True
        CACHE[path] = [(o, o.matrix_world.copy()) for o in new if o.type == 'MESH']
    T = Matrix.Translation(P(x, y)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Diagonal((s[0], s[1], s[2], 1))
    for src, mw in CACHE[path]:
        o = src.copy(); sc.collection.objects.link(o); o.hide_render = False; o.hide_viewport = False
        o.parent = None; o.matrix_world = T @ mw

def mat(name, rgb, rough=0.9):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough; return m

# chao: as texturas da Costa, misturadas como no jogo
gm = bpy.data.materials.new("chao"); gm.use_nodes = True
pbr._ground_tex_mat(gm, "/home/claude/cinzas/costa/art/ter_areia.png", "/home/claude/cinzas/costa/art/ter_costa_cal.png", 6.0, 2 * EXT)
bpy.ops.mesh.primitive_plane_add(size=2 * EXT); g = bpy.context.object; g.data.materials.append(gm)
# mar ao norte (alem de y = -110): faixa de areia molhada + agua escura
sea = mat("mar", (0.02, 0.07, 0.1), 0.15); wet = mat("areia_molhada", (0.18, 0.15, 0.11), 0.4); foam = mat("espuma", (0.8, 0.82, 0.8), 0.6)
for (y0, y1, m, z) in ((-EXT - 40, -113.0, sea, 0.05), (-113.0, -110.5, foam, 0.04), (-110.5, -104.0, wet, 0.02)):
    bpy.ops.mesh.primitive_plane_add(size=1); p = bpy.context.object; p.scale = (2 * EXT + 80, y1 - y0, 1); p.location = P(0, (y0 + y1) / 2, z); p.data.materials.append(m)
# estradas
road = mat("estrada", (0.22, 0.17, 0.12), 0.95); edge = mat("borda", (0.32, 0.26, 0.19), 0.95)
for r in M["roads"]:
    pts = r["points"]; w = r.get("width", 3.0)
    cu = bpy.data.curves.new("estrada", 'CURVE'); cu.dimensions = '3D'; sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for i, (x, y) in enumerate(pts): sp.points[i].co = (x, -y, 0.06, 1)
    cu.extrude = 0.0; cu.bevel_depth = 0;
    o = bpy.data.objects.new("estrada", cu); sc.collection.objects.link(o)
    cu.bevel_mode = 'OBJECT' if False else 'ROUND'; cu.bevel_depth = w / 2; cu.bevel_resolution = 0
    o.scale.z = 0.02; o.data.materials.append(road)
# obstaculos
for o in M["obstacles"]:
    k, x, y, h = o["kind"], o["x"], o["y"], o.get("height", 2)
    if k == "rocha":
        r = o["r"]; n = "costa_rocha_" + "abc"[rnd.randrange(3)]; inst(CM + f"{n}/{n}.glb", x, y, rnd.uniform(0, 360), (r, r, h))
    elif k == "arvore":
        if rnd.random() < 0.55: n = f"costa_coqueiro_{'ab'[rnd.randrange(2)]}"; base = 7.3
        else: n = f"costa_pinheiro_{'abc'[rnd.randrange(3)]}"; base = 6.6
        sc_ = max(h, 5) / base * rnd.uniform(0.9, 1.15); inst(CM + f"{n}/{n}.glb", x, y, rnd.uniform(0, 360), (sc_, sc_, sc_))
    elif k == "penhasco":
        w, d = o["w"], o["h"]; along = w >= d; L = w if along else d; n = max(1, int(math.ceil(L / 10)))
        for i in range(n):
            t = -L / 2 + L / n * (i + 0.5)
            px, py = (x + t, y) if along else (x, y + t)
            yaw = (0 if y > 0 else 180) if along else (90 if x > 0 else -90)
            inst(CM + f"costa_penhasco_{'ab'[i % 2]}/costa_penhasco_{'ab'[i % 2]}.glb", px, py, yaw, (L / n / 10, max(1, (d if along else w) / 3), h / 8))
    elif k == "farol":
        inst(L3 + "cenario/cenario_farol/cenario_farol.glb", x, y, 0)
    elif k == "ravina":
        w, d = o["w"], o["h"]; n = max(1, int(math.ceil(w / 10)))
        for i in range(n): inst(CM + "costa_ravina_trecho/costa_ravina_trecho.glb", x - w / 2 + w / n * (i + 0.5), y, 0, (w / n / 10, d / 2.4, 1))
    elif k in ("ruina", "muro", "barricada", "muralha_ilusoria"):
        w, d = o.get("w", 2), o.get("h", 2)
        bpy.ops.mesh.primitive_cube_add(size=1); b = bpy.context.object; b.scale = (w, d, h); b.location = P(x, y, h / 2)
        b.data.materials.append(mat("pedra", (0.42, 0.4, 0.36)))
    elif k == "coluna":
        bpy.ops.mesh.primitive_cylinder_add(radius=o["r"], depth=h, location=P(x, y, h / 2)); bpy.context.object.data.materials.append(mat("pedra2", (0.5, 0.48, 0.44)))
# props
for p in M["props"]:
    k, x, y = p["kind"], p["x"], p["y"]
    if k == "pinheiros":
        for j in range(3): n = f"costa_pinheiro_{'abc'[j]}"; inst(CM + f"{n}/{n}.glb", x + rnd.uniform(-2, 2), y + rnd.uniform(-2, 2), rnd.uniform(0, 360), (0.8,) * 3)
    elif k == "tenda":
        inst(L3 + "cenario/cenario_tenda_goblin/cenario_tenda_goblin.glb", x, y, p.get("yaw", 0))
    elif k == "fogueira":
        inst(L2 + "modelos/cenario_fogueira/cenario_fogueira.glb", x, y, 0)
    elif k in ("redes", "ossada"):
        inst(CM + "costa_alga_a/costa_alga_a.glb", x, y, rnd.uniform(0, 360))
    elif k == "barco_encalhado":
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1, location=P(x, y, 0.3)); b = bpy.context.object; b.scale = (1.3, 3.2, 0.6); b.rotation_euler.z = math.radians(p.get("yaw", 0)); b.data.materials.append(mat("barco", (0.25, 0.16, 0.1)))
# marcos especiais
inst(L3 + "cenario/edificio_entrada_galeria/edificio_entrada_galeria.glb", -103.0, -64.0, -90)
inst(L2 + "modelos/marco_ossada_gigante/marco_ossada_gigante.glb", 88, -88, 60, (0.8, 0.8, 0.8))
for x, y in ((-6, 14), (6, 22)): inst(L3 + "cenario/cenario_tenda_goblin/cenario_tenda_goblin.glb", x, y, rnd.uniform(0, 360))
# luz baixa (sombras longas = leitura de relevo no mapa) e camera de cima
sun = bpy.data.objects.new("sol", bpy.data.lights.new("sol", 'SUN')); sc.collection.objects.link(sun)
sun.data.energy = 3.0; sun.data.color = (1, 0.93, 0.82); sun.rotation_euler = (math.radians(55), 0, math.radians(135)); sun.data.angle = math.radians(2)
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.55, 0.65, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.6
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2 * EXT; cam.location = (0, 0, 120); cam.rotation_euler = (0, 0, 0)
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 24
try: sc.cycles.use_denoising = True
except Exception: pass
sc.render.resolution_x = sc.render.resolution_y = 2048; sc.view_settings.view_transform = 'Standard'
os.makedirs(out, exist_ok=True); sc.render.filepath = os.path.join(out, "_mapa_raw.png")
bpy.ops.render.render(write_still=True)
print("RENDER OK")

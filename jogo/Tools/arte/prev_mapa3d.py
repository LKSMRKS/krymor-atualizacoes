"""Prévia 3D de um pedaço de mapa ambientado: importa os .glb das peças (região + cenario_* do jogo) nas posições do JSON.
python3 prev_mapa3d.py <mapa.json> <cx> <cy> <raio> <saida.png> [cor_chao]"""
import sys, json, math, glob, os, random
import bpy
from mathutils import Vector
a = [x for x in sys.argv if not x.startswith("--")]
mapa, cx, cy, raio, out = a[-6], float(a[-5]), float(a[-4]), float(a[-3]), a[-2]
chao = a[-1]
m = json.load(open(mapa))
idx = {}
for f in glob.glob("/home/claude/cinzas/**/*.glb", recursive=True):
    if "colisao" in f: continue
    idx.setdefault(os.path.basename(f)[:-4], f)
for f in glob.glob("/home/claude/cinzas/acervo/out/**/*.glb", recursive=True): idx[os.path.basename(f)[:-4]] = f
GEN = {"tocha_poste": "cenario_tocha_poste", "estandarte": "cenario_estandarte_tridente", "caixotes": "cenario_caixote", "caixote": "cenario_caixote",
       "barril": "cenario_barril", "ossada": "cenario_ossada", "cranios": "cenario_cranios", "fogueira": "cenario_fogueira", "cerca_quebrada": "cenario_cerca_quebrada",
       "coluna_quebrada": "cenario_coluna", "tenda": "cenario_tenda_lona", "lapide": "vespera_lapide_a", "feno": "cenario_feno", "estatua_guardiao": "cenario_estatua_guardiao",
       "placa": "cenario_placa", "acampamento_abandonado": "cenario_acampamento_abandonado", "tenda_goblin": "cenario_tenda_goblin", "carroca": "cenario_carroca_quebrada",
       "vagonete": "cenario_vagonete", "trilhos": "cenario_trilhos", "poste": "cenario_lanterna_rua", "banco_praca": "cenario_banco_praca", "forja": "cenario_forja",
       "redes": "cenario_redes", "barco_encalhado": "cenario_barco_encalhado", "rocha": "cenario_rocha_2", "cristal_gelo": "geada_runa_gelo"}
TEMA_ARV = {"neve": ["geada_arvore_neve_a", "geada_arvore_neve_b", "geada_arvore_neve_c"], "vale": ["vale_arvore_queimada_a", "vale_arvore_queimada_c"],
            "mata": ["mata_arvore_forca_a", "mata_arvore_forca_b"], "brejo": ["brejo_mangue"], "fantasma": ["vale_arvore_queimada_a"]}
bpy.ops.wm.read_factory_settings(use_empty=True)
cache = {}
def coloca(nome, x, y, yaw, esc):
    f = idx.get(nome)
    if not f: return
    if nome not in cache:
        before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=f)
        novos = [o for o in bpy.data.objects if o not in before]
        root = bpy.data.objects.new("src_" + nome, None); bpy.context.scene.collection.objects.link(root)
        for o in novos:
            if o.parent is None: o.parent = root
        root.hide_render = True; root.location = (0, 0, -1000)
        cache[nome] = root
    src = cache[nome]
    inst = bpy.data.objects.new(nome, None); inst.instance_type = 'COLLECTION'
    col = bpy.data.collections.get("C_" + nome)
    if col is None:
        col = bpy.data.collections.new("C_" + nome)
        for o in [src] + list(src.children_recursive): col.objects.link(o)
        src.location = (0, 0, 0); src.hide_render = False
    inst.instance_collection = col
    bpy.context.scene.collection.objects.link(inst)
    inst.location = (x - cx, -(y - cy), 0); inst.rotation_euler = (0, 0, math.radians(yaw)); inst.scale = (esc,) * 3
rnd = random.Random(1)
tema = m.get("theme", "")
for p in m["props"]:
    if math.hypot(p["x"] - cx, p["y"] - cy) > raio: continue
    k = p["kind"]; nome = k[len("glb:"):].split("/")[-1] if k.startswith("glb:") else GEN.get(k)
    if k == "pinheiros" and tema in TEMA_ARV: nome = rnd.choice(TEMA_ARV[tema])
    if nome: coloca(nome, p["x"], p["y"], p.get("yaw", 0), p.get("scale", 1))
for o in m["obstacles"]:
    if math.hypot(o["x"] - cx, o["y"] - cy) > raio: continue
    if o["kind"] in ("arvore", "arvore_seca") and tema in TEMA_ARV: coloca(rnd.choice(TEMA_ARV[tema]), o["x"], o["y"], rnd.uniform(0, 360), rnd.uniform(0.85, 1.15))
    elif o["kind"] == "rocha": coloca(rnd.choice(["cenario_rocha_1", "cenario_rocha_2", "cenario_rocha_3"]), o["x"], o["y"], rnd.uniform(0, 360), max(0.6, o.get("r", 1)))
# chão e estrada
bpy.ops.mesh.primitive_plane_add(size=raio * 2.6, location=(0, 0, 0)); g = bpy.context.object
mt = bpy.data.materials.new("chao"); mt.use_nodes = True; bs = mt.node_tree.nodes["Principled BSDF"]
c = tuple(int(chao[i:i + 2], 16) / 255 for i in (0, 2, 4)); bs.inputs["Base Color"].default_value = (c[0] ** 2.2, c[1] ** 2.2, c[2] ** 2.2, 1); bs.inputs["Roughness"].default_value = 0.95
g.data.materials.append(mt)
sc = bpy.context.scene
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 28; d = raio * 1.05
cam.location = (0, d * 0.95, d * 0.68); cam.rotation_euler = (Vector((0, 0, 0)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
sun = bpy.data.objects.new("sol", bpy.data.lights.new("sol", 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 3.5; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
sun.data.color = (1, 0.93, 0.85)
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.56, 0.68, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'CYCLES'
if sc.render.engine == 'CYCLES': sc.cycles.samples = 16; sc.cycles.use_denoising = True
sc.view_settings.view_transform = 'AgX'
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
sc.render.filepath = out; bpy.ops.render.render(write_still=True)
print("PREVIA", out, len(cache), "modelos")

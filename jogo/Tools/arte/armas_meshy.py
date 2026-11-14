"""Armas do Meshy (11/11, pacote do usuário) -> Client/Assets/Models/armas/arma_<id>.glb, prontas para a mão do manequim.
Uso: python3.11 Tools/arte/armas_meshy.py <entrada.glb> <saida.glb> <comprimento_m> <pegada_fração> [raw_icone.png]

- junta as malhas; o eixo comprido vira o eixo da arma; a ponta mais larga (lâmina/cabeça) vai para +Y no Godot;
- escala para o comprimento real; a origem fica na pegada (fração do comprimento a partir do pé do cabo);
- reduz para ~9 mil triângulos e as texturas para 1024 px (o Meshy manda 300-800 mil e 2048);
- opcional: renderiza o ícone (na diagonal, fundo transparente) para o acabamento em acabar_icones.py."""
import sys, os, math, bpy, bmesh
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
src, dst, length, grip = argv[0], argv[1], float(argv[2]), float(argv[3])
icon = argv[4] if len(argv) > 4 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for o in bpy.data.objects:
    o.select_set(o.type == 'MESH')
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
for o in list(bpy.data.objects):
    if o is not ob and o.type != 'MESH': bpy.data.objects.remove(o)
ob.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

vs = [v.co.copy() for v in ob.data.vertices]
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
axis = max(range(3), key=lambda i: mx[i] - mn[i])
# eixo comprido -> Z (Blender) = Y (Godot)
if axis == 0: ob.data.transform(Matrix.Rotation(math.radians(-90), 4, 'Y'))
elif axis == 1: ob.data.transform(Matrix.Rotation(math.radians(90), 4, 'X'))
vs = [v.co.copy() for v in ob.data.vertices]
z0 = min(v.z for v in vs); z1 = max(v.z for v in vs); L = z1 - z0
def spread(lo, hi):
    sel = [v for v in vs if lo <= v.z <= hi]
    if not sel: return 0
    return max(max(v.x for v in sel) - min(v.x for v in sel), max(v.y for v in sel) - min(v.y for v in sel))
top, bot = spread(z1 - L * 0.3, z1), spread(z0, z0 + L * 0.3)
if bot > top: ob.data.transform(Matrix.Rotation(math.pi, 4, 'X'))   # a cabeça (mais larga) para cima
vs = [v.co.copy() for v in ob.data.vertices]
z0 = min(v.z for v in vs); z1 = max(v.z for v in vs)
cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2; cy = (min(v.y for v in vs) + max(v.y for v in vs)) / 2
k = length / (z1 - z0)
ob.data.transform(Matrix.Translation(Vector((-cx, -cy, -z0))))
ob.data.transform(Matrix.Scale(k, 4))
ob.data.transform(Matrix.Translation(Vector((0, 0, -grip * length))))
# o cabo pode não estar no centro da largura (machado): centra pelo cabo (fatia da pegada)
vs = [v.co for v in ob.data.vertices]
sl = [v for v in vs if -0.06 <= v.z <= 0.06]
if len(sl) > 20:
    ox = (min(v.x for v in sl) + max(v.x for v in sl)) / 2; oy = (min(v.y for v in sl) + max(v.y for v in sl)) / 2
    ob.data.transform(Matrix.Translation(Vector((-ox, -oy, 0))))
faces = len(ob.data.polygons)
if faces > 9000:
    mod = ob.modifiers.new("dec", 'DECIMATE'); mod.ratio = 9000 / faces
    bpy.ops.object.modifier_apply(modifier=mod.name)
for img in bpy.data.images:
    if img.size[0] > 1024: img.scale(1024, 1024)
    if img.packed_file is None and img.size[0] > 0: img.pack()
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', use_selection=False, export_yup=True, export_apply=True,
                          export_image_format='JPEG', export_jpeg_quality=88)
print("ok", os.path.basename(dst), "tris", sum(len(p.vertices) - 2 for p in ob.data.polygons), "k", round(k, 3))

if icon:
    sc = bpy.context.scene
    ob.rotation_euler = (0, math.radians(-45), 0)   # na diagonal, cabo embaixo à esquerda
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ v.co for v in ob.data.vertices]
    c = sum(pts, Vector()) / len(pts)
    ext = max(max(abs(p.x - c.x) for p in pts), max(abs(p.z - c.z) for p in pts)) * 2 * 1.1
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = ext; cam.location = (c.x, c.y - 10, c.z); cam.rotation_euler = (math.radians(90), 0, 0)
    for nm, e, col, rot in (("key", 4.5, (1, 0.92, 0.8), (math.radians(60), math.radians(-40), math.radians(-30))),
                            ("rim", 6.0, (0.7, 0.8, 1.0), (math.radians(-120), 0, math.radians(160))), ("fill", 1.4, (1, 1, 1), (math.radians(80), 0, math.radians(20)))):
        l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); sc.collection.objects.link(l); l.data.energy = e; l.data.color = col; l.rotation_euler = rot
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 0.4
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 24; sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 384; sc.view_settings.view_transform = 'Standard'
    sc.render.filepath = icon; bpy.ops.render.render(write_still=True); print("icone", icon)

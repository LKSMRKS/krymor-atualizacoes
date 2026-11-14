"""Gargula do muro do portao: a estatua do Meshy (a mesma da barra) reduzida, sobre uma misula de pedra presa na parede.
Origem: no pe da parede, no meio; a parede fica atras (+Z do Godot = frente da gargula). Uso: python3 build_gargula_muro.py"""
import sys, math, bpy
sys.path.insert(0, "/home/claude/cinzas/tools")
import itemkit as K
from mathutils import Vector, Matrix
OUT = "/home/claude/cinzas/gargula_muro/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath="/mnt/user-data/uploads/Meshy_AI_Gargoyle_Statue_1110050159_image-to-3d-texture.glb")
g = [o for o in bpy.data.objects if o.type == 'MESH'][0]
for o in [o for o in bpy.data.objects if o.type != 'MESH']: bpy.data.objects.remove(o)
bpy.context.view_layer.update(); g.data.transform(g.matrix_world); g.parent = None; g.matrix_world = Matrix()
vs = [v.co for v in g.data.vertices]
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))); mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
k = 1.3 / (mx.z - mn.z)
g.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation((-(mn.x + mx.x) / 2, -(mn.y + mx.y) / 2, -mn.z)))
d = g.modifiers.new("d", 'DECIMATE'); d.ratio = 22000 / len(g.data.polygons); bpy.context.view_layer.objects.active = g; bpy.ops.object.modifier_apply(modifier="d")
for im in bpy.data.images:
    if im.size[0] > 2048: im.scale(2048, 2048)
dy = (mx.y - mn.y) * k
H = 3.2       # altura da misula (o alto do muro do portao)
g.location = (0, -0.05, H)
pedra = K.m_rock("misula", "#6a665e", "#9a958a", scale=2)
bpy.ops.mesh.primitive_cube_add(size=1); mi = bpy.context.object; mi.scale = (0.9, dy + 0.25, 0.3); mi.location = (0, dy / 2 - 0.15 + 0.05 - 0.05, H - 0.15); mi.data.materials.append(pedra)
K.bevel(mi, 0.03, 2); K.apply_all(mi)
bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.45, depth=0.6, location=(0, dy / 2 + 0.05, H - 0.6), rotation=(math.pi, 0, math.pi / 4)); cn = bpy.context.object
cn.scale = (1, 0.6, 1); cn.data.materials.append(pedra); K.apply_all(cn)
K._bake(mi, 512) if False else None
for x in bpy.context.view_layer.objects: x.select_set(False)
mi.select_set(True); cn.select_set(True); bpy.context.view_layer.objects.active = mi; bpy.ops.object.join(); K._bake(mi, 512)
# parede: fica em y = dy/2 + 0.2 (atras); move tudo para a origem no pe da parede
off = Vector((0, -(mi.location.y + dy / 2 + 0.05), 0))
for o in (g, mi): o.location += Vector((0, 0, 0))
for x in bpy.context.view_layer.objects: x.select_set(False)
g.select_set(True); mi.select_set(True)
bpy.ops.export_scene.gltf(filepath=OUT + "cenario_gargula_muro.glb", use_selection=True, export_format='GLB', export_yup=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT + "cenario_gargula_muro.blend")
print("OK", round(dy, 2), sum(len(p.vertices) - 2 for p in g.data.polygons))

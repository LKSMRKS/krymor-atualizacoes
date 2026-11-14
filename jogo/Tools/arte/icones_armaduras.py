"""Icones das pecas de armadura (estilo carta do MU): python3 icones_armaduras.py <blend> <saida_dir> <tier 0-11>"""
import sys, math, os
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy
from mathutils import Vector, Quaternion
import itemkit as K
blend, out, tier = sys.argv[-3], sys.argv[-2], int(sys.argv[-1])
os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=blend)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if arm.animation_data:
    arm.animation_data.action = None
    for t in arm.animation_data.nla_tracks: t.mute = True
arm.data.pose_position = 'POSE'
RW = arm.matrix_world.to_3x3().normalized()
def gira(bone, eixo, graus):
    pb = arm.pose.bones["mixamorig:" + bone]; bl = arm.data.bones["mixamorig:" + bone]
    ax = (bl.matrix_local.to_3x3().inverted() @ (RW.inverted() @ Vector(eixo))).normalized()
    pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion(ax, math.radians(graus))
for pb in arm.pose.bones: pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0)
gira("LeftArm", (0, 1, 0), 72); gira("RightArm", (0, 1, 0), -72)
bpy.context.view_layer.update()
aid = os.path.basename(blend)[:-6]
pecas = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(aid + "_")]
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_render = True
glow = None if tier < 6 else ((255, 200, 90) if tier < 10 else (255, 150, 60))
# objetos avaliados (com a pose) precisam de bound_box atualizado: aplica copia avaliada
dg = bpy.context.evaluated_depsgraph_get()
for p in pecas:
    me = bpy.data.meshes.new_from_object(p.evaluated_get(dg)); tmp = bpy.data.objects.new("icone_tmp", me); bpy.context.collection.objects.link(tmp)
    tmp.matrix_world = p.matrix_world.copy(); tmp.hide_render = False
    nome = p.name[len(aid) + 1:]
    K.render_icon(f"{out}/ico_{aid}_{nome}", cam_rot=(82, 0, 28), pad=1.12, res=384, samples=40, glow=glow)
    bpy.data.objects.remove(tmp); print("ICONE", aid, nome, flush=True)

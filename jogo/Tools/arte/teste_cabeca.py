"""Encaixa cabecas no manequim (osso Head, pose T) e renderiza frente/lado + Walking. Uso: python3 teste_cabeca.py saida.png cab1.glb ..."""
import sys, bpy, math
from mathutils import Vector, Matrix
sys.path.insert(0, "/home/claude/cinzas/tools"); import cz
from PIL import Image
out = sys.argv[1]; cabs = sys.argv[2:]; tiles = []
for cab in cabs:
    for act, view in ((None, "front"), (None, "side"), ("Walking", "34")):
        bpy.ops.wm.open_mainfile(filepath="/home/claude/cinzas/goblins2/manequim.blend"); cz._MATS.clear()
        arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE'); body = bpy.data.objects["char1"]
        # some com a cabeca-ovo do manequim (como o jogo faz ao mostrar a cabeca de bicho)
        import bmesh
        hg = {body.vertex_groups[n].index for n in ("mixamorig:Head", "mixamorig:HeadTop_End", "headfront") if n in body.vertex_groups}
        bm = bmesh.new(); bm.from_mesh(body.data); dl = bm.verts.layers.deform.active; bm.verts.ensure_lookup_table()
        dead = [v for v in bm.verts if v[dl] and max(v[dl].items(), key=lambda kv: kv[1])[0] in hg]
        bmesh.ops.delete(bm, geom=dead, context='VERTS'); bm.to_mesh(body.data); bm.free()
        before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=cab)
        h = [o for o in bpy.data.objects if o not in before and o.type == 'MESH'][0]
        h.parent = arm; h.parent_type = 'BONE'; h.parent_bone = "mixamorig:Head"
        # o socket do jogo fica na CABECA do osso, alinhado ao mundo na pose T (Y-up do Godot = Z aqui)
        pb = arm.pose.bones["mixamorig:Head"]
        bpy.context.view_layer.update()
        bone_w = arm.matrix_world @ pb.matrix          # matriz do osso no mundo (cabeca do osso)
        h.matrix_world = Matrix.Translation(bone_w.translation)   # pose T: socket ~alinhado ao mundo
        arm.animation_data_create(); arm.animation_data.action = bpy.data.actions[act] if act else None
        if not act:
            for p in arm.pose.bones: p.rotation_quaternion = (1, 0, 0, 0); p.location = (0, 0, 0)
        bpy.context.scene.frame_set(6)
        paths = cz.render_views(f"/tmp/claude-0/_tc{len(tiles)}", (0, 0, 0.85), 1.75, views=(view,), res=420, samples=16)
        tiles += paths
im = Image.new("RGB", (420 * len(tiles), 420))
for i, p in enumerate(tiles): im.paste(Image.open(p).convert("RGB").resize((420, 420)), (420 * i, 0))
im.save(out)

"""Ícones 256x256 (depois: python3 Tools/arte/acabar_icones.py <pasta> <destino>) das peças das armaduras lendárias (pacote do trabalhador, 11/11). Uso:
python3.11 Tools/arte/render_icones_armadura.py <pasta_saida> <armadura.glb...>
Gera ico_<id>_<capacete|peitoral|luvas|botas>.png (o peitoral mostra peito + braçadeiras + perneiras), de frente e um
pouco de lado, fundo transparente, mesma luz dos outros ícones."""
import sys, os, math, bpy
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else sys.argv[1]
files = sys.argv[sys.argv.index("--") + 2:] if "--" in sys.argv else sys.argv[2:]
os.makedirs(out, exist_ok=True)
# 5 partes como no MU (11/11); "calca" = perneiras nas lendárias, malha "calca" nos conjuntos arm_*
GRUPOS = {"capacete": ["capacete"], "peitoral": ["peitoral"], "calca": ["calca", "perneiras"], "luvas": ["luvas", "bracadeiras"], "botas": ["botas"]}
for f in files:
    aid = os.path.basename(f)[:-4]
    for part, keep in GRUPOS.items():
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=f)
        sc = bpy.context.scene
        meshes = [o for o in bpy.data.objects if o.type == 'MESH']
        for o in meshes:
            if not any(o.name.split(".")[0] in (k, aid + "_" + k) for k in keep): bpy.data.objects.remove(o)
        meshes = [o for o in bpy.data.objects if o.type == 'MESH']
        if not meshes: print("sem", aid, part); continue
        for o in bpy.data.objects:   # pose de repouso (T): o glb vem com animação, que baixa os braços
            if o.type == 'ARMATURE': o.data.pose_position = 'REST'
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        pts = []
        for o in meshes:
            ev = o.evaluated_get(dg); me = ev.to_mesh()
            pts += [o.matrix_world @ v.co for v in me.vertices]
            ev.to_mesh_clear()
        mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        c = (mn + mx) / 2; size = mx - mn
        ext = max(size.x, size.z) * 1.18
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
        cam.data.type = 'ORTHO'; cam.data.ortho_scale = ext
        ang = math.radians(-22)
        cam.location = (c.x + math.sin(ang) * 10, c.y - math.cos(ang) * 10, c.z + 1.5)
        d = c - cam.location; cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        for nm, e, col, rot in (("key", 4.5, (1, 0.92, 0.8), (math.radians(60), math.radians(-40), math.radians(-30))),
                                ("rim", 6.0, (0.7, 0.8, 1.0), (math.radians(-120), 0, math.radians(160))), ("fill", 1.4, (1, 1, 1), (math.radians(80), 0, math.radians(20)))):
            l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); sc.collection.objects.link(l); l.data.energy = e; l.data.color = col; l.rotation_euler = rot
        w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 0.4
        sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 24; sc.render.film_transparent = True
        sc.render.resolution_x = sc.render.resolution_y = 384; sc.view_settings.view_transform = 'Standard'
        sc.render.filepath = os.path.join(out, f"raw_{aid}_{part}.png"); bpy.ops.render.render(write_still=True); print("ok", aid, part)

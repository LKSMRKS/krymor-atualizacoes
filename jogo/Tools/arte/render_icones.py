"""Icones 2D 128x128 dos itens/armas (inventario e loja): fundo transparente, luz de cima a esquerda, contorno de luz.
Armas na diagonal (cabo embaixo a esquerda). Uso: python3 render_icones.py <saida> <glb...>"""
import sys, os, math, bpy
from mathutils import Vector, Matrix
from PIL import Image, ImageFilter
import numpy as np
out = sys.argv[1]; files = sys.argv[2:]
os.makedirs(out, exist_ok=True)
for f in files:
    name = "ico_" + os.path.basename(f)[:-4].replace("arma_", "").replace("item_", "")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=f)
    sc = bpy.context.scene
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    root = bpy.data.objects.new("raiz", None); sc.collection.objects.link(root)
    for o in bpy.data.objects:
        if o.parent is None and o is not root: o.parent = root
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (mn + mx) / 2; size = mx - mn
    root.location = -c; bpy.context.view_layer.update()
    long_axis = max(range(3), key=lambda i: size[i])
    # gira para o eixo comprido ficar na diagonal da tela (camera olha de -Y)
    if size[long_axis] > 1.8 * sorted(size)[1]:
        if long_axis == 1: root.rotation_euler = (math.radians(90), 0, 0)
        elif long_axis == 0: root.rotation_euler = (0, math.radians(90), 0)
        bpy.context.view_layer.update()
        root.rotation_euler.rotate(Matrix.Rotation(math.radians(-45), 3, 'Y').to_euler())
    else:
        root.rotation_euler.rotate(Matrix.Rotation(math.radians(25), 3, 'Z').to_euler()); root.rotation_euler.rotate(Matrix.Rotation(math.radians(20), 3, 'X').to_euler())
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(cc) for o in meshes for cc in o.bound_box]
    ext = max(max(abs(p.x) for p in pts), max(abs(p.z) for p in pts)) * 2 * 1.12
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = ext; cam.location = (0, -10, 0); cam.rotation_euler = (math.radians(90), 0, 0)
    for nm, e, col, rot in (("key", 4.5, (1, 0.92, 0.8), (math.radians(60), math.radians(-40), math.radians(-30))),
                            ("rim", 6.0, (0.7, 0.8, 1.0), (math.radians(-120), 0, math.radians(160))), ("fill", 1.2, (1, 1, 1), (math.radians(80), 0, math.radians(20)))):
        l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); sc.collection.objects.link(l); l.data.energy = e; l.data.color = col; l.rotation_euler = rot
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 0.35
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 32; sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 256; sc.view_settings.view_transform = 'Standard'
    raw = os.path.join(out, "_raw.png"); sc.render.filepath = raw; bpy.ops.render.render(write_still=True)
    im = Image.open(raw).convert("RGBA").resize((128, 128), Image.LANCZOS)
    a = np.array(im).astype(float)
    rgb = np.clip((a[..., :3] / 255) ** 1.15 * 1.12, 0, 1); lum = rgb.mean(2, keepdims=True); rgb = np.clip(lum + (rgb - lum) * 1.25, 0, 1)
    a[..., :3] = rgb * 255
    im = Image.fromarray(a.astype("uint8"), "RGBA")
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0)); sh.putalpha(im.split()[3].filter(ImageFilter.GaussianBlur(3)).point(lambda v: int(v * 0.7)))
    final = Image.new("RGBA", im.size); final.alpha_composite(sh, (2, 3)); final.alpha_composite(im)
    final.save(os.path.join(out, name + ".png")); os.remove(raw); print("ok", name)

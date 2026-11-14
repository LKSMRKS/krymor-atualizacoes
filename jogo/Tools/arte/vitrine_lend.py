"""Vitrine dos 15 conjuntos lendários (igual à arte de referência do dono: cada set de frente num pedestal, nome embaixo).
python3 vitrine_lend.py <saida.png>"""
import sys, math, os
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy
from mathutils import Vector, Quaternion
from PIL import Image, ImageDraw, ImageFont
out = [a for a in sys.argv if not a.startswith("--")][-1]
ORDEM = [("arm_cavaleiro_sagrado", "CAVALEIRO SAGRADO"), ("arm_dragao_fogo", "DRAGÃO DE FOGO"), ("arm_abismo", "ABISMO"), ("arm_gelo_eterno", "GELO ETERNO"),
         ("arm_celestial", "CELESTIAL"), ("arm_floresta", "FLORESTA"), ("arm_necromante", "NECROMANTE"), ("arm_demoniaco", "DEMONÍACO"),
         ("arm_runico_ancestral", "RÚNICO"), ("arm_tempo", "TEMPO"), ("arm_abissal_profundo", "ABISSAL"), ("arm_faraonico", "FARAÔNICO"),
         ("arm_pedra", "PEDRA VIVA"), ("arm_vazio", "VAZIO"), ("arm_anjo_caido", "ANJO CAÍDO")]
W, H = 420, 560
tiles = []
for aid, nome in ORDEM:
    blend = f"/home/claude/cinzas/conjuntos/out_lend/{aid}/{aid}.blend"
    bpy.ops.wm.open_mainfile(filepath=blend)
    sc = bpy.context.scene
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    if arm.animation_data:
        arm.animation_data.action = None
        for t in arm.animation_data.nla_tracks: t.mute = True
    arm.data.pose_position = 'POSE'; RW = arm.matrix_world.to_3x3().normalized()
    def gira(bone, eixo, graus):
        pb = arm.pose.bones["mixamorig:" + bone]; bl = arm.data.bones["mixamorig:" + bone]
        ax = (bl.matrix_local.to_3x3().inverted() @ (RW.inverted() @ Vector(eixo))).normalized()
        pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion(ax, math.radians(graus)) @ pb.rotation_quaternion
    for pb in arm.pose.bones: pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0)
    gira("LeftArm", (0, 1, 0), 62); gira("RightArm", (0, 1, 0), -62); gira("LeftUpLeg", (0, 1, 0), 4); gira("RightUpLeg", (0, 1, 0), -4)
    bpy.context.view_layer.update()
    body = next((o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("char1")), None)
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.parent is None and o is not body and not o.modifiers: o.hide_render = True
    if body is not None:
        m = bpy.data.materials.new("pele"); m.use_nodes = True; m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.42, 0.32, 0.26, 1)
        body.data.materials.clear(); body.data.materials.append(m)
    # pedestal
    bpy.ops.mesh.primitive_cylinder_add(radius=0.62, depth=0.16, location=(0, 0.03, -0.08), vertices=48)
    p = bpy.context.object; pm = bpy.data.materials.new("ped"); pm.use_nodes = True; b = pm.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.05, 0.04, 0.05, 1); b.inputs["Metallic"].default_value = 0.6; b.inputs["Roughness"].default_value = 0.3; p.data.materials.append(pm)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.62, minor_radius=0.02, location=(0, 0.03, 0.0)); t = bpy.context.object
    tm = bpy.data.materials.new("aro"); tm.use_nodes = True; bb = tm.node_tree.nodes["Principled BSDF"]
    bb.inputs["Base Color"].default_value = (0.9, 0.62, 0.22, 1); bb.inputs["Metallic"].default_value = 1; bb.inputs["Roughness"].default_value = 0.25; t.data.materials.append(tm)
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 50
    a = math.radians(14); c = Vector((0, 0.05, 1.0)); dist = 4.2
    cam.location = (math.sin(a) * dist, -math.cos(a) * dist, 1.35); cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    def luz(nm, e, col, rot, ang=3):
        l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); sc.collection.objects.link(l); l.data.energy = e; l.data.color = col
        l.rotation_euler = [math.radians(x) for x in rot]; l.data.angle = math.radians(ang)
    luz("key", 4.5, (1, 0.9, 0.78), (40, 0, -30)); luz("rim", 6.0, (0.75, 0.6, 1.0), (70, 0, 170)); luz("rim2", 3.0, (1.0, 0.75, 0.45), (65, 0, 200))
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; bg = w.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.03, 0.02, 0.05, 1); bg.inputs[1].default_value = 1.0
    sc.render.film_transparent = True
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 28; sc.cycles.device = 'CPU'; sc.cycles.use_denoising = True
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Punchy'
    sc.render.resolution_x = W; sc.render.resolution_y = H
    p = f"/tmp/claude-0/_vit_{aid}.png"; sc.render.filepath = p; bpy.ops.render.render(write_still=True); tiles.append((p, nome))
# montagem: fundo roxo escuro, moldura dourada, nome numa placa
cols, rows = 5, 3
fundo = Image.new("RGB", (cols * W + 80, rows * (H + 50) + 140), (18, 10, 28))
d = ImageDraw.Draw(fundo)
for yy in range(fundo.height):
    k = yy / fundo.height; d.line([(0, yy), (fundo.width, yy)], fill=(int(30 + 25 * k), int(14 + 6 * k), int(48 + 20 * k)))
F = next((f for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf") if os.path.exists(f)), None)
try:
    fonte = ImageFont.truetype(F, 26); titulo = ImageFont.truetype(F, 54)
except Exception:
    fonte = titulo = ImageFont.load_default()
d.text((fundo.width / 2, 60), "SETS LENDÁRIOS", font=titulo, fill=(240, 196, 110), anchor="mm")
for i, (p, nome) in enumerate(tiles):
    x0 = 40 + (i % cols) * W; y0 = 110 + (i // cols) * (H + 50)
    d.rounded_rectangle([x0 + 6, y0, x0 + W - 6, y0 + H + 40], radius=14, fill=(26, 18, 34), outline=(150, 105, 50), width=3)
    im = Image.open(p).convert("RGBA"); fundo.paste(im, (x0, y0), im)
    d.rounded_rectangle([x0 + 40, y0 + H - 6, x0 + W - 40, y0 + H + 32], radius=8, fill=(40, 26, 18), outline=(200, 150, 70), width=2)
    d.text((x0 + W / 2, y0 + H + 13), nome, font=fonte, fill=(245, 225, 180), anchor="mm")
fundo.save(out); print("VITRINE", out)

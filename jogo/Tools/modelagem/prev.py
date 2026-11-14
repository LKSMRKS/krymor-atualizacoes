import bpy, sys, math, mathutils
args = sys.argv[sys.argv.index('--') + 1:]
out = args[0]; files = args[1:]
pitch = 32
if files and files[0].startswith('pitch='): pitch = float(files[0][6:]); files = files[1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene; x = 0; tot_h = 0
for f in files:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=f)
    new = [o for o in bpy.context.scene.objects if o not in before]
    roots = [o for o in new if o.parent is None]
    mn = mathutils.Vector((1e9,) * 3); mx = -mn
    bpy.context.view_layer.update()
    for o in new:
        if o.type != 'MESH': continue
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c); mn = mathutils.Vector(map(min, mn, w)); mx = mathutils.Vector(map(max, mx, w))
    w = mx.x - mn.x
    for r in roots: r.location.x += x - mn.x
    x += w + 0.6; tot_h = max(tot_h, mx.z)
span = x - 0.6
g = bpy.data.materials.new('chao'); g.use_nodes = True
nt = g.node_tree; bs = nt.nodes['Principled BSDF']; nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3
rp = nt.nodes.new('ShaderNodeValToRGB'); rp.color_ramp.elements[0].color = (0.45, 0.38, 0.28, 1); rp.color_ramp.elements[1].color = (0.62, 0.55, 0.42, 1)
nt.links.new(nz.outputs[0], rp.inputs[0]); nt.links.new(rp.outputs[0], bs.inputs['Base Color'])
bpy.ops.mesh.primitive_plane_add(size=max(span, tot_h) * 4, location=(span / 2, 0, 0)); bpy.context.object.data.materials.append(g)
sc.render.engine = 'CYCLES'; sc.cycles.samples = 48; sc.cycles.device = 'CPU'
sc.render.resolution_x = 1600; sc.render.resolution_y = 800
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.55, 0.62, 0.75, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.9
s = bpy.data.objects.new('s', bpy.data.lights.new('s', 'SUN')); s.data.energy = 3.8; s.data.angle = 0.08
s.rotation_euler = (math.radians(48), 0, math.radians(35)); sc.collection.objects.link(s)
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 40
tgt = mathutils.Vector((span / 2, 0, tot_h * 0.4))
d = max(span * 1.0, tot_h * 2.0) + 2
p = math.radians(pitch)
cam.location = tgt + mathutils.Vector((0, -d * math.cos(p), d * math.sin(p)))
cam.rotation_euler = (tgt - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.view_settings.view_transform = 'AgX'
sc.render.filepath = out; bpy.ops.render.render(write_still=True)

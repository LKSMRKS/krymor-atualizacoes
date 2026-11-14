"""Pipeline organico: membros por Skin Modifier, fusao por remesh voxel, relevo por displace,
reducao por decimate, pintura procedural em cor de vertice, bake para textura, pesos por proximidade de osso."""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, noise
import cz

def _apply_all(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True); bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)

def limb(name, nodes, edges=None, subdiv=2, flat=1.0):
    """nodes: [(co, raio)] ou [(co, (rx, ry))]; edges: lista de pares (padrao: corrente)."""
    me = bpy.data.meshes.new(name)
    vs = [Vector(n[0]) for n in nodes]
    es = edges or [(i, i + 1) for i in range(len(nodes) - 1)]
    me.from_pydata(vs, es, [])
    o = cz._link(bpy.data.objects.new(name, me))
    mod = o.modifiers.new("skin", 'SKIN')
    sv = me.skin_vertices[0].data
    for i, n in enumerate(nodes):
        r = n[1] if isinstance(n[1], tuple) else (n[1], n[1] * flat)
        sv[i].radius = r
    sv[0].use_root = True
    o.modifiers.new("sub", 'SUBSURF').levels = subdiv
    _apply_all(o)
    return o

def blob(name, loc, scale, seg=32, rings=16):
    o = cz.ball(name, 1, seg // 2, rings // 2, loc=loc, scale=scale)   # cz.ball dobra resolucao
    cz.apply_tf(o)
    return o

def spike(name, base, tip, r):
    return cz.seg(name, base, tip, r, 0.0, 8)

def fuse(name, objs, voxel=0.012):
    for o in objs:
        if o.matrix_basis != Matrix.Identity(4): cz.apply_tf(o)
    o = cz.join(name, objs, flat_shade=False)
    for p in o.data.polygons: p.use_smooth = True
    r = o.modifiers.new("remesh", 'REMESH'); r.mode = 'VOXEL'; r.voxel_size = voxel; r.adaptivity = 0.0
    _apply_all(o)
    return o

def displace(o, strength, size, kind='CLOUDS', depth=2):
    tex = bpy.data.textures.new(o.name + kind, kind)
    if kind == 'CLOUDS': tex.noise_scale = size; tex.noise_depth = depth
    else: tex.noise_scale = size; tex.distance_metric = 'DISTANCE'
    d = o.modifiers.new("disp", 'DISPLACE'); d.texture = tex; d.strength = strength; d.mid_level = 0.5
    d.texture_coords = 'GLOBAL'
    _apply_all(o)

def smooth(o, factor=0.5, reps=2):
    s = o.modifiers.new("sm", 'SMOOTH'); s.factor = factor; s.iterations = reps
    _apply_all(o)

def decimate(o, target_tris):
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris <= target_tris: return
    d = o.modifiers.new("dec", 'DECIMATE'); d.ratio = target_tris / tris; d.use_collapse_triangulate = True
    _apply_all(o)
    o.data.shade_smooth()

# ------------------------------------------------------------------ pintura
def paint(o, fn, layer="Col"):
    """fn(co, normal) -> (r,g,b) linear. Grava em atributo de cor por canto (corner)."""
    me = o.data
    if layer in me.color_attributes: me.color_attributes.remove(me.color_attributes[layer])
    ca = me.color_attributes.new(layer, 'FLOAT_COLOR', 'POINT')
    me.calc_loop_triangles() if hasattr(me, "calc_loop_triangles") else None
    for v in me.vertices:
        c = fn(v.co, v.normal)
        ca.data[v.index].color = (c[0], c[1], c[2], 1.0)
    me.color_attributes.active_color = ca
    return ca

def lin(hexs):
    hexs = hexs.lstrip('#'); c = [int(hexs[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return Vector([((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c])

def mix(a, b, t):
    t = max(0.0, min(1.0, t)); return a.lerp(b, t)

def sstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)

def vc_material(name, rough=0.6):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    ca = nt.nodes.new("ShaderNodeVertexColor"); ca.layer_name = "Col"
    nt.links.new(ca.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough
    return m

def bake_to_texture(o, img_name, size=1024, rough=0.6):
    """UV automatico + bake da cor de vertice para imagem; troca o material por textura (exporta em qualquer motor)."""
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    if not o.data.uv_layers: o.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.006)
    bpy.ops.object.mode_set(mode='OBJECT')
    img = bpy.data.images.new(img_name, size, size)
    m = o.data.materials[0]; nt = m.node_tree
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img
    for n in nt.nodes: n.select = False
    tn.select = True; nt.nodes.active = tn
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 4; sc.cycles.device = 'CPU'
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=4)
    img.pack()
    b = nt.nodes["Principled BSDF"]
    nt.links.new(tn.outputs["Color"], b.inputs["Base Color"])
    for n in [n for n in nt.nodes if n.type == 'VERTEX_COLOR']: nt.nodes.remove(n)
    return img

# ------------------------------------------------------------------ pesos
def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length

def weights_by_bones(o, arm, regions=None, falloff=0.035, exclude=()):
    """Peso pelo osso mais proximo (com mistura suave entre os 2 mais proximos).
    regions: lista de (bone, funcao(co)->bool) que forcam o osso (ex.: corpo dentro do elipsoide)."""
    bones = [(b.name, Vector(b.head_local), Vector(b.tail_local)) for b in arm.data.bones if b.name not in exclude]
    groups = {n: o.vertex_groups.new(name=n) for n, _, _ in bones}
    for v in o.data.vertices:
        p = v.co
        forced = None
        for bn, fn in (regions or []):
            if fn(p): forced = bn; break
        if forced:
            groups[forced].add([v.index], 1.0, 'REPLACE'); continue
        ds = sorted((seg_dist(p, a, b), n) for n, a, b in bones)
        (d1, n1), (d2, n2) = ds[0], ds[1]
        w2 = math.exp(-(d2 - d1) / falloff) * 0.5
        groups[n1].add([v.index], 1.0 - w2, 'REPLACE')
        if w2 > 0.02: groups[n2].add([v.index], w2, 'REPLACE')
    mod = o.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
    o.parent = arm

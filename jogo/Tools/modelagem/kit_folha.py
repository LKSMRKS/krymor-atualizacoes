"""Folhagem com cartões (alpha recortado) + tronco assado. Exporta glb com 2 malhas: <nome>_tronco e <nome>_folhas."""
import sys; sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Quaternion
import itemkit as K
TEX = "/home/claude/cinzas/tapa/tex/"

def m_casca(name, base="#5a4634", dark="#241a12", light="#8f7b66", scale=1.0, liso=False):
    b = K.NB(name)
    vec = b.n("ShaderNodeMapping"); b.l(b.obj(), vec.inputs["Vector"]); vec.inputs["Scale"].default_value = (1.0, 1.0, 0.25 if not liso else 1.0)
    fis = b.voronoi(9 * scale, 'DISTANCE_TO_EDGE', vec=vec.outputs[0])
    n1 = b.noise(14 * scale, 6, 0.7, vec=vec.outputs[0])
    col = b.ramp(n1, [(0.3, dark), (0.55, base), (0.85, light)])
    col = b.mix(b.math('LESS_THAN', fis, 0.06), col, dark)
    col = b.mix(b.math('MULTIPLY', K.cavity(b, 0.05), 1.4, clamp=True), col, "#100a06")
    h = b.math('ADD', b.math('MULTIPLY', fis, 1.0), b.math('MULTIPLY', n1, 0.4))
    return b.set(base=col, rough=0.88, normal=b.bump(h, 0.9, 0.02))

def m_folha(name, png, tint=(1, 1, 1, 1), trans=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes["Principled BSDF"]
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(TEX + png); t.image.alpha_mode = 'STRAIGHT'
    nt.links.new(t.outputs["Color"], bs.inputs["Base Color"])
    rnd = nt.nodes.new("ShaderNodeMath"); rnd.operation = 'ROUND'
    nt.links.new(t.outputs["Alpha"], rnd.inputs[0]); nt.links.new(rnd.outputs[0], bs.inputs["Alpha"])
    bs.inputs["Roughness"].default_value = 0.6
    m.use_backface_culling = False
    try: m.surface_render_method = 'DITHERED'
    except Exception: pass
    return m

class Folhas:
    """acumula cartões num bmesh só; normais apontam para fora da copa (luz macia de copa)"""
    def __init__(s): s.bm = bmesh.new(); s.uv = s.bm.loops.layers.uv.new("UVMap"); s.norm = []
    def card(s, base, direction, size, roll=0.0, width=None, centro=None, u0=0, u1=1, v0=0, v1=1):
        d = Vector(direction).normalized(); w = width or size
        side = d.cross(Vector((0, 0, 1)));
        if side.length < 1e-3: side = Vector((1, 0, 0))
        side.normalize(); side = Quaternion(d, roll) @ side
        b = Vector(base); top = b + d * size
        vs = [s.bm.verts.new(p) for p in (b - side * w / 2, b + side * w / 2, top + side * w / 2, top - side * w / 2)]
        f = s.bm.faces.new(vs)
        for lp, uv in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))): lp[s.uv].uv = uv
        c = Vector(centro) if centro is not None else b
        for v in vs: s.norm.append((v, c))
        return f
    def strip(s, pts, widths, ups, u_along=True):
        """fita curva (folha de palmeira): pts ao longo, largura e vetor lateral por ponto; textura comprida no eixo u"""
        n = len(pts); prev = None
        for i in range(n):
            a = Vector(pts[i]) - Vector(ups[i]) * widths[i] / 2; bb = Vector(pts[i]) + Vector(ups[i]) * widths[i] / 2
            cur = (s.bm.verts.new(a), s.bm.verts.new(bb))
            for v in cur: s.norm.append((v, None))
            if prev:
                f = s.bm.faces.new((prev[0], cur[0], cur[1], prev[1])); u_a, u_b = (i - 1) / (n - 1), i / (n - 1)
                for lp, uv in zip(f.loops, ((u_a, 0), (u_b, 0), (u_b, 1), (u_a, 1))): lp[s.uv].uv = uv
            prev = cur
    def objeto(s, name, mat, centro_copa, up_mix=0.45):
        me = bpy.data.meshes.new(name); s.bm.to_mesh(me)
        o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o); o.data.materials.append(mat)
        cc = Vector(centro_copa)
        ns = []
        for lp in me.loops:
            co = me.vertices[lp.vertex_index].co
            n = (co - cc); n.z *= 0.6
            n = (n.normalized() * (1 - up_mix) + Vector((0, 0, 1)) * up_mix).normalized()
            ns.append(n)
        for p in me.polygons: p.use_smooth = True
        me.normals_split_custom_set(ns)
        return o

def tronco(name, pts, r0, r1, mat, res=10):
    n = len(pts)
    return K.tube(name, pts, 1.0, mat, res=res, taper=[r0 + (r1 - r0) * i / max(1, n - 1) for i in range(n)])

def exportar(path, tronco_objs, folha_objs, tex=1024, tris_tronco=3000, emit=False, extra=()):
    sc = bpy.context.scene; groups = []
    if tronco_objs:
        for o in tronco_objs: K.apply_all(o)
        for x in bpy.context.view_layer.objects: x.select_set(False)
        for o in tronco_objs: o.select_set(True)
        bpy.context.view_layer.objects.active = tronco_objs[0]; bpy.ops.object.join(); ob = bpy.context.view_layer.objects.active
        ob.name = os.path.basename(path)[:-4] + "_tronco"
        real = next((m for m in ob.data.materials if m is not None), None)
        for i, m in enumerate(ob.data.materials):
            if m is None and real is not None: ob.data.materials[i] = real   # boolean deixa espaço de material vazio
        K._decimate(ob, tris_tronco); K._bake(ob, tex, emit=emit); groups.append(ob)
    for o in folha_objs: groups.append(o)
    for o in extra: groups.append(o)
    root = bpy.data.objects.new(os.path.basename(path)[:-4], None); sc.collection.objects.link(root)
    for o in groups: o.parent = root
    for x in bpy.context.view_layer.objects: x.select_set(False)
    root.select_set(True)
    for o in groups: o.select_set(True)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=path, use_selection=True, export_format='GLB', export_yup=True)
    tris = sum(len(p.vertices) - 2 for o in groups for p in o.data.polygons)
    print("EXPORT", os.path.basename(path), "tris", tris)

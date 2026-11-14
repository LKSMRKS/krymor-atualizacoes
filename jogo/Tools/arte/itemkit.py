"""Kit para itens com acabamento: malhas esculpidas (lathe, cristal, gema lapidada, tubo), materiais procedurais
(metal gasto, couro, osso, quitina, rocha, vidro, liquido, cristal, madeira, seda, folha), icone renderizado e glb assado.
Modelagem em ~1 unidade (maior medida ~1); export escala para o tamanho do mundo."""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Euler
import numpy as np

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for m in list(bpy.data.materials): bpy.data.materials.remove(m)

def lin(h):
    h = h.lstrip("#"); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c) + (1.0,)

# ---------------------------------------------------------------- nos
class NB:
    def __init__(s, name):
        s.m = bpy.data.materials.new(name); s.m.use_nodes = True; s.nt = s.m.node_tree; s.N = s.nt.nodes; s.L = s.nt.links
        s.bs = s.N["Principled BSDF"]; s.out = s.N["Material Output"]
        s.tc = s.n("ShaderNodeTexCoord")
    def n(s, t, **kw):
        nd = s.N.new(t)
        for k, v in kw.items():
            if k.startswith("i_"):
                key = k[2:].replace("_", " ")
                nd.inputs[key].default_value = v
            else: setattr(nd, k, v)
        return nd
    def l(s, a, b): s.L.new(a, b); return b
    def obj(s): return s.tc.outputs["Object"]
    def noise(s, scale, detail=6, rough=0.6, vec=None, dist=0.0):
        nz = s.n("ShaderNodeTexNoise", i_Scale=scale, i_Detail=detail, i_Roughness=rough, i_Distortion=dist)
        s.l(vec or s.obj(), nz.inputs["Vector"]); return nz.outputs["Fac"]
    def voronoi(s, scale, feature='F1', out="Distance", vec=None, rnd=1.0):
        v = s.n("ShaderNodeTexVoronoi", feature=feature, i_Scale=scale, i_Randomness=rnd); s.l(vec or s.obj(), v.inputs["Vector"]); return v.outputs[out]
    def wave(s, scale, dist=4, detail=3, kind='BANDS', axis='Z', vec=None, prof='SIN'):
        w = s.n("ShaderNodeTexWave", wave_type=kind, bands_direction=axis, rings_direction=axis if axis != 'DIAGONAL' else 'Z', wave_profile=prof,
                i_Scale=scale, i_Distortion=dist, i_Detail=detail)
        s.l(vec or s.obj(), w.inputs["Vector"]); return w.outputs["Fac"]
    def math(s, op, a, b=None, clamp=False):
        m = s.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, v in enumerate((a, b)):
            if v is None: continue
            if isinstance(v, (int, float)): m.inputs[i].default_value = v
            else: s.l(v, m.inputs[i])
        return m.outputs[0]
    def ramp(s, fac, stops):
        r = s.n("ShaderNodeValToRGB"); cr = r.color_ramp
        while len(cr.elements) < len(stops): cr.elements.new(0.5)
        for e, (p, c) in zip(cr.elements, stops):
            e.position = p; e.color = lin(c) if isinstance(c, str) else (tuple(c) if isinstance(c, tuple) else (c, c, c, 1))
        s.l(fac, r.inputs["Fac"]); return r.outputs["Color"]
    def mix(s, fac, a, b, blend='MIX'):
        m = s.n("ShaderNodeMix", data_type='RGBA', blend_type=blend)
        for sock, v in ((m.inputs[0], fac), (m.inputs[6], a), (m.inputs[7], b)):
            if isinstance(v, (int, float)): sock.default_value = v
            elif isinstance(v, str): sock.default_value = lin(v)
            elif isinstance(v, tuple): sock.default_value = v
            else: s.l(v, sock)
        return m.outputs[2]
    def ao(s, dist=0.15):
        a = s.n("ShaderNodeAmbientOcclusion", i_Distance=dist, samples=16); return a.outputs["AO"]
    def edge(s):                      # quina convexa (desgaste) via pointiness
        g = s.n("ShaderNodeNewGeometry"); return g.outputs["Pointiness"]
    def bump(s, h, strength=0.5, dist=0.02, normal=None):
        b = s.n("ShaderNodeBump", i_Strength=strength, i_Distance=dist); s.l(h, b.inputs["Height"])
        if normal is not None: s.l(normal, b.inputs["Normal"])
        return b.outputs["Normal"]
    def set(s, **kw):
        names = dict(base="Base Color", metal="Metallic", rough="Roughness", normal="Normal", trans="Transmission Weight", ior="IOR",
                     emit_col="Emission Color", emit="Emission Strength", alpha="Alpha", coat="Coat Weight", sheen="Sheen Weight",
                     sss="Subsurface Weight", spec="Specular IOR Level", coat_rough="Coat Roughness", sss_rad="Subsurface Radius")
        for k, v in kw.items():
            sock = s.bs.inputs[names[k]]
            if isinstance(v, str): sock.default_value = lin(v)
            elif isinstance(v, (int, float, tuple)): sock.default_value = v
            else: s.l(v, sock)
        return s.m

def edge_mask(b, lo=0.5, hi=0.56):
    return b.math('MULTIPLY', b.math('SUBTRACT', b.edge(), lo), 1.0 / (hi - lo), clamp=True)

def cavity(b, dist=0.12):
    return b.math('SUBTRACT', 1.0, b.ao(dist))

# ---------------------------------------------------------------- materiais
def m_metal(name, base, rough=0.35, wear="#d8c9a8", dirt="#1a1410", scratch=0.6, hammered=0.0, scale=1.0):
    b = NB(name)
    n1 = b.noise(6 * scale, 8, 0.65)
    col = b.ramp(n1, [(0.3, base), (0.75, b_l(base, 1.15))])
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.08), 1.6, clamp=True), col, dirt)
    col = b.mix(b.math('MULTIPLY', edge_mask(b, 0.5, 0.545), 0.85, clamp=True), col, wear)
    sc = b.wave(40 * scale, 25, 2, 'BANDS', 'X', prof='SAW')
    scr = b.math('GREATER_THAN', b.math('MULTIPLY', sc, b.noise(3 * scale, 2)), 0.62)
    rg = b.math('ADD', rough, b.math('MULTIPLY', b.math('SUBTRACT', n1, 0.5), 0.35))
    rg = b.math('SUBTRACT', rg, b.math('MULTIPLY', scr, 0.18 * scratch))
    h = b.math('ADD', b.math('MULTIPLY', b.noise(30 * scale, 4), 0.4), b.math('MULTIPLY', scr, -0.25 * scratch))
    if hammered: h = b.math('ADD', h, b.math('MULTIPLY', b.voronoi(14 * scale), hammered))
    return b.set(base=col, metal=1.0, rough=rg, normal=b.bump(h, 0.35, 0.01))

def b_l(h, k):
    c = lin(h); g = [min(1, x * k) for x in c[:3]]
    return tuple(g) + (1,)

def m_gold(name="ouro", scale=1.0): return m_metal(name, "#d9a640", 0.28, wear="#fff0bf", dirt="#3a2208", scratch=0.4, scale=scale)
def m_bronze(name="bronze", scale=1.0): return m_metal(name, "#9c6431", 0.38, wear="#e6b47a", dirt="#1d140c", scratch=0.6, scale=scale)
def m_iron(name="ferro", scale=1.0): return m_metal(name, "#55555a", 0.45, wear="#b9b7b0", dirt="#120f0d", scratch=1.0, hammered=0.25, scale=scale)
def m_silver(name="prata", scale=1.0): return m_metal(name, "#b8bcc4", 0.25, wear="#f5f7ff", dirt="#22252c", scratch=0.6, scale=scale)

def m_leather(name, base="#6b3a22", dark="#2e160c", scale=1.0):
    b = NB(name)
    n1 = b.noise(4 * scale, 6, 0.6); pores = b.voronoi(120 * scale, 'F1')
    grain = b.noise(60 * scale, 3)
    col = b.ramp(n1, [(0.25, dark), (0.7, base)])
    col = b.mix(b.math('MULTIPLY', edge_mask(b, 0.5, 0.54), 0.7, clamp=True), col, "#b07a4c")
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.1), 1.4, clamp=True), col, "#140904")
    h = b.math('ADD', b.math('MULTIPLY', pores, 0.4), b.math('MULTIPLY', grain, 0.3))
    rg = b.math('ADD', 0.55, b.math('MULTIPLY', n1, 0.3))
    return b.set(base=col, rough=rg, normal=b.bump(h, 0.45, 0.006), sheen=0.3)

def m_bone(name="osso", base="#e3d6b8", stain="#8a6a3e", scale=1.0):
    b = NB(name)
    n1 = b.noise(3 * scale, 8, 0.6); n2 = b.noise(18 * scale, 6, 0.7)
    col = b.ramp(n1, [(0.3, stain), (0.55, b_l(base, 0.85)), (0.8, base)])
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.1), 1.8, clamp=True), col, "#3a2a16")
    pores = b.voronoi(80 * scale, 'F1'); cracks = b.voronoi(3.5 * scale, 'DISTANCE_TO_EDGE')
    crk = b.math('MULTIPLY', b.math('LESS_THAN', cracks, 0.012), b.math('GREATER_THAN', b.noise(2.5 * scale, 2), 0.5))
    col = b.mix(b.math('MULTIPLY', crk, 0.7), col, "#3b2c1a")
    h = b.math('ADD', b.math('MULTIPLY', n2, 0.4), b.math('ADD', b.math('MULTIPLY', pores, 0.25), b.math('MULTIPLY', crk, -0.6)))
    return b.set(base=col, rough=b.math('ADD', 0.5, b.math('MULTIPLY', n2, 0.3)), normal=b.bump(h, 0.5, 0.008), sss=0.15, sss_rad=(0.3, 0.2, 0.1))

def m_chitin(name, base="#a33a1e", dark="#3a0f08", tip="#f1d9b0", crust=True, scale=1.0, zgrad=None):
    b = NB(name)
    n1 = b.noise(5 * scale, 8, 0.6); spots = b.voronoi(14 * scale, 'F1')
    col = b.ramp(n1, [(0.2, dark), (0.6, base), (0.85, b_l(base, 1.25))])
    col = b.mix(b.math('LESS_THAN', spots, 0.18), col, dark)
    col = b.mix(b.math('MULTIPLY', edge_mask(b, 0.5, 0.55), 0.9, clamp=True), col, tip)
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.1), 1.5, clamp=True), col, "#180605")
    bumps = b.voronoi(26 * scale, 'SMOOTH_F1')
    h = b.math('ADD', b.math('MULTIPLY', bumps, -0.6), b.math('MULTIPLY', b.noise(50 * scale, 4), 0.2))
    rg = b.math('ADD', 0.28, b.math('MULTIPLY', n1, 0.22))
    if crust:
        cm = b.math("GREATER_THAN", b.noise(3.2 * scale, 6, 0.7), 0.64)
        col = b.mix(cm, col, b.ramp(b.noise(40 * scale, 4), [(0.3, "#bfb5a0"), (0.7, "#efe9da")]))
        rg = b.math('ADD', rg, b.math('MULTIPLY', cm, 0.5))
        h = b.math('ADD', h, b.math('MULTIPLY', cm, b.math('MULTIPLY', b.noise(70 * scale, 3), 0.9)))
    return b.set(base=col, rough=rg, normal=b.bump(h, 0.55, 0.01), coat=0.35, coat_rough=0.25)

def m_rock(name, base="#3b3a40", light="#6d6a70", scale=1.0, flecks="#d5c27a"):
    b = NB(name)
    n1 = b.noise(4 * scale, 8, 0.7); fac = b.voronoi(7 * scale, 'F1')
    col = b.ramp(n1, [(0.25, b_l(base, 0.6)), (0.55, base), (0.85, light)])
    col = b.mix(b.math('MULTIPLY', edge_mask(b, 0.5, 0.56), 0.6, clamp=True), col, light)
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.1), 1.6, clamp=True), col, "#0c0b0d")
    fl = b.math('GREATER_THAN', b.noise(90 * scale, 2), 0.72)
    col = b.mix(fl, col, flecks)
    h = b.math('ADD', b.math('MULTIPLY', fac, 0.5), b.math('MULTIPLY', b.noise(30 * scale, 6), 0.4))
    return b.set(base=col, rough=b.math('SUBTRACT', 0.85, b.math('MULTIPLY', fl, 0.5)), metal=b.math('MULTIPLY', fl, 0.9), normal=b.bump(h, 0.7, 0.02))

def m_crystal(name, color, glow, strength=2.5, scale=1.0):
    b = NB(name)
    n1 = b.noise(3 * scale, 4)
    lw = b.n("ShaderNodeLayerWeight", i_Blend=0.35)
    fres = lw.outputs["Fresnel"]
    em = b.math('MULTIPLY', b.math('ADD', 0.15, b.math('MULTIPLY', n1, 0.5)), strength * 0.45)
    col = b.mix(fres, b_l(color, 0.55), b_l(color, 1.35))
    h = b.math('MULTIPLY', b.voronoi(20 * scale, 'DISTANCE_TO_EDGE'), 0.2)
    return b.set(base=col, trans=0.35, rough=0.04, ior=1.75, emit_col=glow, emit=em, coat=1.0, normal=b.bump(h, 0.06, 0.01))

def m_glass(name, tint="#e9f3f6", rough=0.03):
    b = NB(name); return b.set(base=tint, trans=1.0, rough=rough, ior=1.48, spec=0.7)

def m_liquid(name, color, glow, strength=1.6):
    b = NB(name)
    sw = b.noise(4, 6, 0.6, dist=3.5)
    c = b.ramp(sw, [(0.25, b_l(color, 0.35)), (0.6, color), (0.9, b_l(color, 1.6))])
    em = b.math('MULTIPLY', b.math('ADD', 0.25, sw), strength)
    return b.set(base=c, rough=0.05, trans=0.4, ior=1.33, emit_col=glow, emit=em)

def m_wood(name, base="#6e4526", dark="#2c170b", scale=1.0):
    b = NB(name)
    ring = b.wave(5 * scale, 6, 4, 'RINGS', 'Z'); n1 = b.noise(30 * scale, 4)
    col = b.ramp(b.math('ADD', b.math('MULTIPLY', ring, 0.6), b.math('MULTIPLY', n1, 0.4)), [(0.2, dark), (0.75, base)])
    col = b.mix(b.math('MULTIPLY', edge_mask(b, 0.5, 0.55), 0.6, clamp=True), col, "#c39462")
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.06), 1.5, clamp=True), col, "#120804")
    return b.set(base=col, rough=0.7, normal=b.bump(b.math('ADD', ring, n1), 0.25, 0.005))

def m_silk(name, base="#f1ece2", shade="#b9b0a2", scale=1.0):
    b = NB(name)
    thr = b.wave(220 * scale, 1.5, 2, 'BANDS', 'Z')
    col = b.ramp(b.noise(5 * scale, 4), [(0.3, shade), (0.7, base)])
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.05), 1.3, clamp=True), col, "#5e574c")
    return b.set(base=col, rough=0.32, sheen=1.0, coat=0.2, normal=b.bump(thr, 0.35, 0.003))

def m_leaf(name, base="#4d7a2a", light="#9cc24f", scale=1.0):
    b = NB(name)
    vein = b.voronoi(12 * scale, 'DISTANCE_TO_EDGE'); n1 = b.noise(8 * scale, 5)
    col = b.ramp(n1, [(0.3, b_l(base, 0.7)), (0.7, base), (0.95, light)])
    col = b.mix(b.math('LESS_THAN', vein, 0.03), col, light)
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.05), 1.2, clamp=True), col, "#0f1a06")
    return b.set(base=col, rough=0.45, sss=0.25, sss_rad=(0.2, 0.5, 0.1), normal=b.bump(b.math('MULTIPLY', vein, -1), 0.3, 0.004))

def m_plain(name, color, rough=0.6, metal=0.0, emit=None, strength=0.0, bumpscale=0.0, scale=1.0):
    b = NB(name)
    n1 = b.noise(6 * scale, 6)
    col = b.ramp(n1, [(0.3, b_l(color, 0.8)), (0.75, color)])
    col = b.mix(b.math('MULTIPLY', cavity(b, 0.08), 1.4, clamp=True), col, b_l(color, 0.25))
    kw = dict(base=col, rough=rough, metal=metal)
    if emit: kw.update(emit_col=emit, emit=strength)
    if bumpscale: kw["normal"] = b.bump(b.noise(bumpscale * scale, 4), 0.3, 0.01)
    return b.set(**kw)

def m_emit(name, color, strength=6.0):
    b = NB(name); return b.set(base=color, emit_col=color, emit=strength, rough=0.2)

# ---------------------------------------------------------------- malhas
def obj_from_bm(bm, name, mat=None, smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    if mat: o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = smooth
    return o

def lathe(name, prof, segs=48, mat=None, smooth=True, close_top=True, close_bot=True):
    """prof = [(r, z), ...] de baixo para cima"""
    bm = bmesh.new(); rings = []
    for (r, z) in prof:
        ring = [bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for a in (i * math.tau / segs for i in range(segs))]
        rings.append(ring)
    for a, bb in zip(rings, rings[1:]):
        for i in range(segs): bm.faces.new((a[i], a[(i + 1) % segs], bb[(i + 1) % segs], bb[i]))
    if close_bot and prof[0][0] > 1e-4: bm.faces.new(list(reversed(rings[0])))
    if close_top and prof[-1][0] > 1e-4: bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(bm, name, mat, smooth)

def hull(name, pts, mat=None, smooth=False):
    bm = bmesh.new()
    for p in pts: bm.verts.new(p)
    bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(bm, name, mat, smooth)

def gem(name, mat, r=1.0, h_crown=0.38, h_pav=0.62, table=0.5, n=8, squash=(1, 1), rot=0.0):
    pts = []
    for i in range(n):
        a = i * math.tau / n + rot; pts.append((r * math.cos(a) * squash[0], r * math.sin(a) * squash[1], 0))
    for i in range(n):
        a = (i + 0.5) * math.tau / n + rot; pts.append((0.78 * r * math.cos(a) * squash[0], 0.78 * r * math.sin(a) * squash[1], h_crown * 0.62))
        a2 = i * math.tau / n + rot; pts.append((table * r * math.cos(a2) * squash[0], table * r * math.sin(a2) * squash[1], h_crown))
        pts.append((0.5 * r * math.cos(a) * squash[0], 0.5 * r * math.sin(a) * squash[1], -h_pav * 0.6))
    pts.append((0, 0, -h_pav))
    # cintura com espessura
    pts += [(p[0] * 1.0, p[1] * 1.0, -0.05 * r) for p in pts[:n]]
    return hull(name, pts, mat, smooth=False)

def crystal(name, mat, r, h, tip=0.35, sides=6, seed=0):
    rnd = random.Random(seed); bm = bmesh.new()
    base = [bm.verts.new((r * math.cos(a) * rnd.uniform(0.85, 1.1), r * math.sin(a) * rnd.uniform(0.85, 1.1), 0)) for a in (i * math.tau / sides for i in range(sides))]
    top = [bm.verts.new((v.co.x * 0.92, v.co.y * 0.92, h)) for v in base]
    apex = bm.verts.new((rnd.uniform(-0.15, 0.15) * r, rnd.uniform(-0.15, 0.15) * r, h + tip * h * rnd.uniform(0.7, 1.2)))
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new((base[i], base[j], top[j], top[i])); bm.faces.new((top[i], top[j], apex))
    bm.faces.new(list(reversed(base)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(bm, name, mat, smooth=False)

def tube(name, pts, radius, mat=None, res=12, taper=None, twist=0.0, closed=False):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'; cu.resolution_u = 12
    sp = cu.splines.new('NURBS' if len(pts) > 3 else 'POLY'); sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts): sp.points[i].co = (*p, 1)
    if sp.type == 'NURBS': sp.use_endpoint_u = True; sp.order_u = 3
    sp.use_cyclic_u = closed
    cu.bevel_depth = radius; cu.bevel_resolution = max(2, res // 4); cu.use_fill_caps = True
    if taper:
        for i, p in enumerate(sp.points): p.radius = taper[i]
    o = bpy.data.objects.new(name, cu); bpy.context.collection.objects.link(o)
    if mat: cu.materials.append(mat)
    bpy.context.view_layer.objects.active = o
    for x in bpy.context.view_layer.objects: x.select_set(False)
    o.select_set(True); bpy.ops.object.convert(target='MESH')
    o = bpy.context.view_layer.objects.active
    for p in o.data.polygons: p.use_smooth = True
    return o

def mod(o, kind, **kw):
    m = o.modifiers.new(kind.lower(), kind)
    for k, v in kw.items(): setattr(m, k, v)
    return m

def subsurf(o, lv=2): return mod(o, 'SUBSURF', levels=lv, render_levels=lv)
def bevel(o, w=0.02, seg=2, angle=40):
    return mod(o, 'BEVEL', width=w, segments=seg, limit_method='ANGLE', angle_limit=math.radians(angle))

def displace(o, strength, size, kind='CLOUDS', depth=2, mid=0.5, coords='LOCAL', seed=0):
    t = bpy.data.textures.new(f"{o.name}_d{len(o.modifiers)}", kind)
    if kind == 'CLOUDS': t.noise_scale = size; t.noise_depth = depth
    elif kind == 'VORONOI': t.noise_scale = size; t.distance_metric = 'DISTANCE'
    m = mod(o, 'DISPLACE', strength=strength, mid_level=mid, texture_coords=coords)
    m.texture = t
    return m

def apply_all(o):
    bpy.context.view_layer.objects.active = o
    for x in bpy.context.view_layer.objects: x.select_set(False)
    o.select_set(True)
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)

def place(o, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    o.location = loc; o.rotation_euler = [math.radians(a) for a in rot]; o.scale = scale; return o

def jitter(o, amt, seed=1):
    rnd = random.Random(seed)
    for v in o.data.vertices: v.co += Vector((rnd.uniform(-amt, amt), rnd.uniform(-amt, amt), rnd.uniform(-amt, amt)))

# ---------------------------------------------------------------- icone
def _bbox(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx

def render_icon(path, cam_rot=(62, 0, 28), pad=1.1, res=448, samples=96, glow=None, key=5.0, extra_light=None):
    """camera ortho; luz quente de cima-esquerda, recorte frio atras, preenchimento fraco; fundo transparente"""
    sc = bpy.context.scene
    objs = [o for o in sc.objects if o.type == 'MESH' and not o.hide_render]
    bpy.context.view_layer.update()
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.rotation_euler = [math.radians(a) for a in cam_rot]
    bpy.context.view_layer.update()
    R = cam.matrix_world.to_3x3(); Ri = R.inverted()
    pts = [Ri @ (o.matrix_world @ Vector(c)) for o in objs for c in o.bound_box]
    mnx, mxx = min(p.x for p in pts), max(p.x for p in pts); mny, mxy = min(p.y for p in pts), max(p.y for p in pts)
    cx, cy = (mnx + mxx) / 2, (mny + mxy) / 2; zf = max(p.z for p in pts)
    cam.location = R @ Vector((cx, cy, zf + 10)); cam.data.ortho_scale = max(mxx - mnx, mxy - mny) * pad
    cam.data.clip_end = 100
    def sun(nm, e, col, rot, ang=4):
        l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); sc.collection.objects.link(l)
        l.data.energy = e; l.data.color = col; l.rotation_euler = [math.radians(a) for a in rot]; l.data.angle = math.radians(ang)
    sun("key", key, (1, 0.9, 0.76), (40, -32, -40))
    sun("rim", 7.0, (0.62, 0.78, 1.0), (-115, 10, 150), 2)
    sun("fill", 0.9, (0.85, 0.9, 1.0), (60, 30, 160), 20)
    if extra_light: extra_light(sc)
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.17, 0.2, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.5
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples; sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 8; sc.cycles.transmission_bounces = 8; sc.cycles.transparent_max_bounces = 8
    sc.render.film_transparent = True; sc.render.resolution_x = sc.render.resolution_y = res
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Punchy'
    raw = path + "_raw.png"; sc.render.filepath = raw; bpy.ops.render.render(write_still=True)
    post_icon(raw, path, glow); os.remove(raw)
    bpy.data.objects.remove(cam)
    for nm in ("key", "rim", "fill"): bpy.data.objects.remove(bpy.data.objects[nm])

def post_icon(raw, path, glow=None):
    from PIL import Image, ImageFilter
    im = Image.open(raw).convert("RGBA")
    a = np.array(im).astype(np.float32) / 255
    rgb = a[..., :3]; lum = rgb.mean(2, keepdims=True); rgb = np.clip(lum + (rgb - lum) * 1.12, 0, 1)
    rgb = np.clip(rgb * 1.05, 0, 1); a[..., :3] = rgb
    im = Image.fromarray((a * 255).astype("uint8"), "RGBA")
    A = im.split()[3]
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    if glow:   # aura colorida para itens magicos
        g = Image.new("RGBA", im.size, glow + (0,)); g.putalpha(A.filter(ImageFilter.GaussianBlur(14)).point(lambda v: int(v * 0.85)))
        out.alpha_composite(g)
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0)); sh.putalpha(A.filter(ImageFilter.GaussianBlur(6)).point(lambda v: int(v * 0.75)))
    out.alpha_composite(sh, (4, 7))
    st = Image.new("RGBA", im.size, (6, 4, 3, 0)); st.putalpha(A.filter(ImageFilter.MaxFilter(5)).point(lambda v: int(v * 0.9)))
    out.alpha_composite(st); out.alpha_composite(im)
    out.resize((256, 256), Image.LANCZOS).save(path + "_256.png")
    out.resize((128, 128), Image.LANCZOS).filter(ImageFilter.UnsharpMask(1, 60, 1)).save(path + ".png")

# ---------------------------------------------------------------- export assado
def is_special(mat):
    bs = mat.node_tree.nodes.get("Principled BSDF")
    if bs is None: return True
    tw = bs.inputs["Transmission Weight"]; es = bs.inputs["Emission Strength"]
    return (tw.default_value > 0.3 and not tw.is_linked) or es.is_linked or es.default_value > 0.5

def simple_copy(mat):
    """material leve para glTF a partir de um especial (vidro/cristal/liquido/emissivo)"""
    bs = mat.node_tree.nodes["Principled BSDF"]
    m = bpy.data.materials.new(mat.name + "_gl"); m.use_nodes = True; b = m.node_tree.nodes["Principled BSDF"]
    def val(k):
        s = bs.inputs[k]
        if s.is_linked:
            nd = s.links[0].from_node
            if nd.type == 'VALTORGB': return tuple(nd.color_ramp.elements[-1].color)
            return None
        return s.default_value
    bc = val("Base Color"); b.inputs["Base Color"].default_value = bc if bc is not None else (0.8, 0.8, 0.8, 1)
    b.inputs["Roughness"].default_value = 0.1
    tw = bs.inputs["Transmission Weight"].default_value
    if tw > 0.3:
        b.inputs["Alpha"].default_value = 0.35 if tw > 0.9 else 0.75
        try: m.surface_render_method = 'BLENDED'
        except Exception: pass
    ec = bs.inputs["Emission Color"]; es = bs.inputs["Emission Strength"]
    if es.is_linked or es.default_value > 0.5:
        b.inputs["Emission Color"].default_value = ec.default_value; b.inputs["Emission Strength"].default_value = 2.5
    return m

def _decimate(o, target):
    t = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if t > target:
        m = o.modifiers.new("dec", 'DECIMATE'); m.ratio = max(0.02, target / t); apply_all(o)

def export_item(path_glb, size_m, tex=512, tris=4000):
    """junta as pecas opacas, assa os materiais procedurais numa textura, mantem vidro/brilho como material simples; escala ao tamanho real"""
    sc = bpy.context.scene
    objs = [o for o in sc.objects if o.type == 'MESH' and not o.hide_render]
    for o in objs: apply_all(o)
    mn, mx = _bbox(objs); k = 1.0 if size_m is None else size_m / max(mx - mn)
    opaque, special = [], []
    for o in objs:
        (special if o.data.materials and all(is_special(m) for m in o.data.materials) else opaque).append(o)
    groups = []
    if opaque:
        for x in bpy.context.view_layer.objects: x.select_set(False)
        for o in opaque: o.select_set(True)
        bpy.context.view_layer.objects.active = opaque[0]; bpy.ops.object.join(); ob = bpy.context.view_layer.objects.active
        ob.name = os.path.basename(path_glb)[:-4]
        _decimate(ob, tris)
        _bake(ob, tex); groups.append(ob)
    for o in special:
        _decimate(o, max(300, tris // 4))
        mats = [simple_copy(m) for m in o.data.materials]; o.data.materials.clear()
        for m in mats: o.data.materials.append(m)
        groups.append(o)
    root = bpy.data.objects.new(os.path.basename(path_glb)[:-4] + "_raiz", None); sc.collection.objects.link(root)
    c = (mn + mx) / 2
    for o in groups:
        o.parent = root
        if size_m is not None: o.location -= Vector((c.x, c.y, mn.z))   # size_m=None: mantem a origem (empunhadura)
    root.scale = (k, k, k)
    for x in bpy.context.view_layer.objects: x.select_set(False)
    root.select_set(True)
    for o in groups: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path_glb, use_selection=True, export_format='GLB', export_yup=True)
    tris = sum(len(p.vertices) - 2 for o in groups for p in o.data.polygons)
    return dict(tris=tris, dims_m=[round((mx - mn)[i] * k, 3) for i in range(3)])

def _bake(ob, size, emit=False):
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'
    for o in bpy.context.view_layer.objects: o.select_set(False)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    while ob.data.uv_layers: ob.data.uv_layers.remove(ob.data.uv_layers[0])
    ob.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(55), island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    imgs = {}
    def img(kk, noncolor):
        im = bpy.data.images.new(f"{ob.name}_{kk}", size, size)
        if noncolor: im.colorspace_settings.name = 'Non-Color'
        imgs[kk] = im; return im
    def target(im):
        for m in ob.data.materials:
            nt = m.node_tree; t = nt.nodes.get("BAKE") or nt.nodes.new("ShaderNodeTexImage"); t.name = "BAKE"; t.image = im
            for n in nt.nodes: n.select = False
            t.select = True; nt.nodes.active = t
    sc.cycles.samples = 4
    target(img("cor", False)); bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=8)
    target(img("rug", True)); bpy.ops.object.bake(type='ROUGHNESS', margin=8)
    target(img("normal", True)); bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', margin=8)
    sc.cycles.samples = 24; target(img("ao", True)); bpy.ops.object.bake(type='AO', margin=8)
    if emit: sc.cycles.samples = 1; target(img("emit", False)); bpy.ops.object.bake(type='EMIT', margin=8); imgs["emit"].pack()
    saved = []
    for m in ob.data.materials:
        bs = m.node_tree.nodes["Principled BSDF"]; s = bs.inputs["Metallic"]
        lk = s.links[0].from_socket if s.is_linked else None; v = s.default_value
        ec = bs.inputs["Emission Color"]; es = bs.inputs["Emission Strength"]
        saved.append((m, ec.default_value[:], es.default_value, [l.from_socket for l in ec.links], [l.from_socket for l in es.links]))
        for l in list(ec.links) + list(es.links): m.node_tree.links.remove(l)
        if lk is not None: m.node_tree.links.new(lk, ec)
        else: ec.default_value = (v, v, v, 1)
        es.default_value = 1.0
    sc.cycles.samples = 1; target(img("metal", True)); bpy.ops.object.bake(type='EMIT', margin=8)
    c = np.array(imgs["cor"].pixels[:], dtype=np.float32).reshape(-1, 4)
    a = np.array(imgs["ao"].pixels[:], dtype=np.float32).reshape(-1, 4)[:, :1]
    c[:, :3] *= 0.5 + 0.5 * a; imgs["cor"].pixels[:] = c.ravel().tolist()
    r = np.array(imgs["rug"].pixels[:], dtype=np.float32).reshape(-1, 4); mt = np.array(imgs["metal"].pixels[:], dtype=np.float32).reshape(-1, 4)
    orm = np.ones_like(r); orm[:, 1] = r[:, 0]; orm[:, 2] = mt[:, 0]; imgs["rug"].pixels[:] = orm.ravel().tolist()
    for kk in ("cor", "rug", "normal"): imgs[kk].pack()
    m = bpy.data.materials.new(ob.name + "_pbr"); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage"); tc.image = imgs["cor"]
    tr = nt.nodes.new("ShaderNodeTexImage"); tr.image = imgs["rug"]; sp = nt.nodes.new("ShaderNodeSeparateColor")
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = imgs["normal"]; nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tc.outputs["Color"], bs.inputs["Base Color"]); nt.links.new(tr.outputs["Color"], sp.inputs["Color"])
    nt.links.new(sp.outputs["Green"], bs.inputs["Roughness"]); nt.links.new(sp.outputs["Blue"], bs.inputs["Metallic"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], bs.inputs["Normal"])
    if emit:
        te = nt.nodes.new("ShaderNodeTexImage"); te.image = imgs["emit"]; nt.links.new(te.outputs["Color"], bs.inputs["Emission Color"]); bs.inputs["Emission Strength"].default_value = 1.0
    ob.data.materials.clear(); ob.data.materials.append(m)

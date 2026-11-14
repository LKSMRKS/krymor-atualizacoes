"""Cinzas da Mare - utilitarios de modelagem procedural low-poly (Blender bpy).
Convencoes: metros, Z para cima no Blender, frente da criatura para -Y no Blender
(=> +Z no glTF exportado com +Y up), origem no chao.
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Quaternion, Euler

# ---------------------------------------------------------------- estilo
SMOOTH = True        # formas arredondadas + sombreamento suave (direcao atual do Lukas)
SHARP_ANGLE = 38     # graus: acima disso a aresta fica viva (laminas, cristais, placas)
def _r(n, minimum=12):
    return max(n * 2, minimum) if SMOOTH else n

# ---------------------------------------------------------------- cena
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.render.fps = 30
    return sc

# ---------------------------------------------------------------- materiais
_MATS = {}
def mat(name, color, rough=0.75, metal=0.0, emit=None, strength=0.0, alpha=1.0):
    if name in _MATS and _MATS[name].name in bpy.data.materials:
        return _MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    def hx(c):
        if isinstance(c, str):
            c = c.lstrip('#'); c = tuple(int(c[i:i+2], 16)/255 for i in (0, 2, 4))
        # sRGB -> linear
        return tuple(((x+0.055)/1.055)**2.4 if x > 0.04045 else x/12.92 for x in c) + (1.0,)
    b.inputs["Base Color"].default_value = hx(color)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit is not None:
        b.inputs["Emission Color"].default_value = hx(emit)
        b.inputs["Emission Strength"].default_value = strength
    if alpha < 1:
        b.inputs["Alpha"].default_value = alpha
    m.diffuse_color = hx(color)
    _MATS[name] = m
    return m

def clear_mats():
    _MATS.clear()

# ---------------------------------------------------------------- primitivas
def _link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj

def _from_bm(bm, name):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    return _link(bpy.data.objects.new(name, me))

def box(name, size, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0, m=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x*size[0], v.co.y*size[1], v.co.z*size[2]))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=(3 if SMOOTH else 1), affect='EDGES')
    o = _from_bm(bm, name)
    o.location = loc; o.rotation_euler = rot
    if m: o.data.materials.append(m)
    return o

def cyl(name, r1, depth, verts=6, loc=(0, 0, 0), rot=(0, 0, 0), r2=None, m=None, cap=True):
    bm = bmesh.new()
    verts = _r(verts) if verts >= 5 else verts
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=verts,
                          radius1=r1, radius2=(r1 if r2 is None else r2), depth=depth)
    o = _from_bm(bm, name)
    o.location = loc; o.rotation_euler = rot
    if m: o.data.materials.append(m)
    return o

def ball(name, r, seg=8, rings=5, loc=(0, 0, 0), scale=(1, 1, 1), rot=(0, 0, 0), m=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=_r(seg, 14), v_segments=_r(rings, 8), radius=r)
    o = _from_bm(bm, name)
    o.location = loc; o.scale = scale; o.rotation_euler = rot
    if m: o.data.materials.append(m)
    return o

def ico(name, r, sub=1, loc=(0, 0, 0), scale=(1, 1, 1), rot=(0, 0, 0), m=None):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub + (1 if SMOOTH else 0), radius=r)
    o = _from_bm(bm, name)
    o.location = loc; o.scale = scale; o.rotation_euler = rot
    if m: o.data.materials.append(m)
    return o

def seg(name, p1, p2, r1, r2=None, verts=6, m=None, twist=0.0):
    """Prisma afunilado entre dois pontos (membros articulados)."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    o = cyl(name, r1, d.length, verts, r2=(r1 if r2 is None else r2), m=m)
    # cilindro do bmesh vai de -depth/2 a +depth/2 em Z; r1 embaixo
    q = d.normalized().to_track_quat('Z', 'Y')
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = q @ Quaternion((0, 0, 1), twist)
    o.location = (p1 + p2) / 2
    return o

def prism(name, pts2d, height, loc=(0, 0, 0), rot=(0, 0, 0), m=None, top_scale=1.0):
    """Extrusao de poligono 2D (XY) em Z; top_scale afunila o topo."""
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, 0)) for x, y in pts2d]
    top = [bm.verts.new((x*top_scale, y*top_scale, height)) for x, y in pts2d]
    n = len(pts2d)
    bm.faces.new(list(reversed(bot)))
    bm.faces.new(top)
    for i in range(n):
        j = (i+1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _from_bm(bm, name)
    o.location = loc; o.rotation_euler = rot
    if m: o.data.materials.append(m)
    return o

def shard(name, r, h, verts=5, loc=(0, 0, 0), rot=(0, 0, 0), m=None, seed=0):
    """Cristal facetado: cone duplo irregular."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    ring = []
    for i in range(verts):
        a = i/verts*math.tau + rnd.uniform(-.2, .2)
        rr = r*rnd.uniform(.8, 1.15)
        ring.append(bm.verts.new((math.cos(a)*rr, math.sin(a)*rr, h*0.25)))
    top = bm.verts.new((rnd.uniform(-r*.2, r*.2), rnd.uniform(-r*.2, r*.2), h))
    bot = bm.verts.new((0, 0, 0))
    for i in range(verts):
        j = (i+1) % verts
        bm.faces.new((ring[i], ring[j], top))
        bm.faces.new((ring[j], ring[i], bot))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _from_bm(bm, name)
    o.location = loc; o.rotation_euler = rot
    if m: o.data.materials.append(m)
    return o

def jitter(o, amt=0.01, seed=1):
    rnd = random.Random(seed)
    if SMOOTH: amt *= 0.35
    for v in o.data.vertices:
        v.co += Vector((rnd.uniform(-amt, amt), rnd.uniform(-amt, amt), rnd.uniform(-amt, amt)))

def mirror_x(o, name=None):
    c = o.copy(); c.data = o.data.copy(); _link(c)
    c.name = name or (o.name + "_mx")
    apply_tf(c)
    for v in c.data.vertices:
        v.co.x = -v.co.x
    c.data.flip_normals()
    return c

def apply_tf(o):
    o.data.transform(o.matrix_basis.copy())
    o.matrix_basis = Matrix.Identity(4)

def flat(o):
    """Acabamento de sombreamento: suave com arestas vivas por angulo (ou facetado se SMOOTH=False)."""
    if SMOOTH:
        o.data.shade_smooth()
        o.data.set_sharp_from_angle(angle=math.radians(SHARP_ANGLE))
    else:
        for p in o.data.polygons:
            p.use_smooth = False

# ---------------------------------------------------------------- rig rigido
def armature(name, bones):
    """bones: lista (nome, head, tail, parent, connect=False)."""
    arm = bpy.data.armatures.new(name + "_rig")
    ob = _link(bpy.data.objects.new(name + "_rig", arm))
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    for b in bones:
        nm, h, t, par = b[:4]
        eb = arm.edit_bones.new(nm)
        eb.head = h; eb.tail = t
        if par:
            eb.parent = arm.edit_bones[par]
            eb.use_connect = (len(b) > 4 and b[4])
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in ob.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return ob

def skin_rigid(name, parts, arm):
    """parts: lista (obj, bone). Junta tudo num mesh com pesos 1.0 por osso."""
    objs = []
    for o, bone in parts:
        apply_tf(o)
        vg = o.vertex_groups.new(name=bone)
        vg.add([v.index for v in o.data.vertices], 1.0, 'REPLACE')
        objs.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    me = objs[0]; me.name = name; me.data.name = name
    flat(me)
    mod = me.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
    me.parent = arm
    return me

def join(name, objs, flat_shade=True):
    for o in objs: apply_tf(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = objs[0]; o.name = name; o.data.name = name
    if flat_shade: flat(o)
    return o

# ---------------------------------------------------------------- animacao
def rest_axis(arm, bone, world_axis):
    """Converte eixo do mundo para o espaco local de repouso do osso."""
    m = arm.data.bones[bone].matrix_local.to_3x3().inverted()
    return (m @ Vector(world_axis)).normalized()

def min_z(mesh):
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    eo = mesh.evaluated_get(dg)
    me = eo.to_mesh()
    mw = eo.matrix_world
    z = min((mw @ v.co).z for v in me.vertices)
    eo.to_mesh_clear()
    return z

class Clip:
    """mesh + ground=True: trava de chao (nenhum vertice abaixo de z=0; ergue o 'root').
    S: escala da criatura (deslocamentos 'l' sao multiplicados)."""
    def __init__(self, arm, name, frames, mesh=None, S=1.0, ground=True, root="root", snap=False):
        self.arm, self.name, self.frames, self.snap = arm, name, frames, snap
        self.mesh, self.S, self.ground, self.root = mesh, S, ground, root
        self.act = bpy.data.actions.new(name)
        self.act.use_fake_user = True
        arm.animation_data_create()
        for t in arm.animation_data.nla_tracks: t.mute = True
        arm.animation_data.action = self.act
        for pb in arm.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)

    def _apply(self, pose, lift=0.0):
        for pb in self.arm.pose.bones:
            q = Quaternion()
            loc = Vector()
            p = pose.get(pb.name, {})
            for ax, deg in p.get('r', []):
                q = Quaternion(rest_axis(self.arm, pb.name, ax), math.radians(deg)) @ q
            wl = Vector(p.get('l', (0, 0, 0))) * self.S
            if pb.name == self.root: wl.z += lift
            if wl.length:
                m = self.arm.data.bones[pb.name].matrix_local.to_3x3().inverted()
                loc = m @ wl
            pb.rotation_quaternion = q; pb.location = loc

    def key(self, f, pose, ground=None):
        """pose: {bone: {'r': [(world_axis, graus),...], 'l': (x,y,z) mundo}}; ossos ausentes = repouso."""
        self._apply(pose)
        g = self.ground if ground is None else ground
        if g and self.mesh is not None:
            z = min_z(self.mesh)
            if z < -0.002 or (self.snap and z > 0.002):
                self._apply(pose, lift=-z)
        for pb in self.arm.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=f)
            pb.keyframe_insert("location", frame=f)

    def done(self):
        tr = self.arm.animation_data.nla_tracks.new()
        tr.name = self.name
        st = tr.strips.new(self.name, 1, self.act)
        self.arm.animation_data.action = None
        return self.act

# ---------------------------------------------------------------- export / render
def tri_count(objs):
    t = 0
    for o in objs:
        if o.type == 'MESH':
            for p in o.data.polygons:
                t += len(p.vertices) - 2
    return t

def save(path_noext, anim=False):
    os.makedirs(os.path.dirname(path_noext), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path_noext + ".blend")
    kw = dict(filepath=path_noext + ".glb", export_format='GLB', export_yup=True,
              export_apply=False, export_materials='EXPORT')
    if anim:
        kw.update(export_animations=True, export_animation_mode='NLA_TRACKS',
                  export_force_sampling=True, export_skins=True)
    else:
        kw.update(export_animations=False)
    bpy.ops.export_scene.gltf(**kw)

def _setup_render(res=640, samples=24):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    try: sc.cycles.use_denoising = True
    except Exception: pass
    sc.render.resolution_x = res; sc.render.resolution_y = res
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.045, 0.042, 0.05, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    for nm, rot, en in (("key", (math.radians(50), 0, math.radians(35)), 3.2),
                        ("rim", (math.radians(65), 0, math.radians(200)), 1.6)):
        l = bpy.data.lights.new(nm, 'SUN'); l.energy = en
        lo = _link(bpy.data.objects.new(nm, l)); lo.rotation_euler = rot
    # chao
    g = bpy.data.meshes.new("ground")
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20); bm.to_mesh(g); bm.free()
    go = _link(bpy.data.objects.new("_ground", g))
    go.data.materials.append(mat("_ground", "#2a2622", rough=0.95))
    return go

def render_views(prefix, center, size, views=("front", "back", "side", "34", "game"), res=640, samples=24):
    """center: ponto alvo; size: maior dimensao aproximada."""
    ground = _setup_render(res, samples)
    sc = bpy.context.scene
    cam = _link(bpy.data.objects.new("cam", bpy.data.cameras.new("cam")))
    sc.camera = cam
    c = Vector(center)
    dirs = {"front": (0, -1, 0.08), "back": (0, 1, 0.08), "side": (1, 0, 0.08),
            "34": (0.75, -0.9, 0.55), "game": (0.35, -1.0, 0.95)}
    out = []
    for v in views:
        d = Vector(dirs[v]).normalized()
        cam.location = c + d * size * 4
        cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        if v == "game":
            cam.data.type = 'PERSP'; cam.data.lens = 35
            cam.location = c + d * size * 3.2
        else:
            cam.data.type = 'ORTHO'; cam.data.ortho_scale = size * 1.35
        sc.render.filepath = f"{prefix}_{v}.png"
        bpy.ops.render.render(write_still=True)
        out.append(sc.render.filepath)
    bpy.data.objects.remove(cam); bpy.data.objects.remove(ground)
    for nm in ("key", "rim"):
        if nm in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[nm])
    return out

def contact_sheet(paths, out, cols=3, cell=420, labels=None):
    """Monta folha de previews usando PIL se disponivel."""
    from PIL import Image, ImageDraw
    ims = [Image.open(p).convert("RGB").resize((cell, cell)) for p in paths]
    rows = math.ceil(len(ims)/cols)
    sheet = Image.new("RGB", (cols*cell, rows*cell), (18, 17, 20))
    d = ImageDraw.Draw(sheet)
    for i, im in enumerate(ims):
        x, y = (i % cols)*cell, (i//cols)*cell
        sheet.paste(im, (x, y))
        if labels: d.text((x+8, y+6), labels[i], fill=(235, 220, 190))
    sheet.save(out)
    return out

# ---------------------------------------------------------------- itens rigidos
def blade(name, length, w, t, tip, m=None, w_top=None, z0=0.0):
    """Lamina de secao losangular: base em z0, ombro em z0+length-tip, ponta em z0+length."""
    w_top = w if w_top is None else w_top
    bm = bmesh.new()
    def ring(z, ww):
        return [bm.verts.new(p) for p in ((ww, 0, z), (0, t, z), (-ww, 0, z), (0, -t, z))]
    r0 = ring(z0, w); r1 = ring(z0 + length - tip, w_top)
    tipv = bm.verts.new((0, 0, z0 + length))
    bm.faces.new(list(reversed(r0)))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
        bm.faces.new((r1[i], r1[j], tipv))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _from_bm(bm, name)
    if m: o.data.materials.append(m)
    return o

def profile(name, pts_xz, thick, m=None, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0):
    """Placa a partir de poligono no plano XZ com espessura em Y (cabecas de machado, ornamentos)."""
    o = prism(name, [(x, z) for x, z in pts_xz], thick, m=m)
    for v in o.data.vertices:
        x, y, z = v.co
        v.co = Vector((x, z - thick / 2, y))   # (x,y)->(x,z), extrusao -> y
    o.data.flip_normals()
    o.location = loc; o.rotation_euler = rot
    return o

def export_objs(objs, path_noext):
    os.makedirs(os.path.dirname(path_noext), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path_noext + ".glb", export_format='GLB', export_yup=True,
                              use_selection=True, export_animations=False, export_materials='EXPORT')

def lineup_render(path, objs_groups, spacing, view=(0.35, -1, 0.25), res=(1600, 700), samples=24, height=None, ortho_pad=1.15):
    """Espalha grupos em X e renderiza uma fileira."""
    ground = _setup_render(64, samples)
    sc = bpy.context.scene; sc.render.resolution_x, sc.render.resolution_y = res
    n = len(objs_groups)
    for i, g in enumerate(objs_groups):
        for o in g: o.location.x += (i - (n - 1) / 2) * spacing
    cam = _link(bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))); sc.camera = cam
    h = height or spacing
    c = Vector((0, 0, h / 2)); d = Vector(view).normalized()
    cam.location = c + d * 30
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = n * spacing * ortho_pad
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path

def check_ground(arm, mesh, skip=()):
    """Varre todos os quadros de cada clipe e retorna {clipe: menor z}."""
    res = {}
    ad = arm.animation_data
    for t in ad.nla_tracks: t.mute = True
    for tr in ad.nla_tracks:
        act = tr.strips[0].action
        if act.name in skip: continue
        ad.action = act
        a, b = map(int, act.frame_range)
        res[act.name] = round(min(min_z_at(mesh, f) for f in range(a, b + 1)), 4)
    ad.action = None
    for t in ad.nla_tracks: t.mute = False
    return res

def min_z_at(mesh, f):
    bpy.context.scene.frame_set(f)
    return min_z(mesh)

def ground_fix(arm, mesh, skip=(), root="root", tol=-0.002, passes=4):
    """Varre os quadros de cada clipe e insere chaves de correcao no 'root' onde algo atravessa o chao."""
    ad = arm.animation_data
    for t in ad.nla_tracks: t.mute = True
    pb = arm.pose.bones[root]
    inv = arm.data.bones[root].matrix_local.to_3x3().inverted()
    for tr in ad.nla_tracks:
        act = tr.strips[0].action
        if act.name in skip: continue
        ad.action = act
        a, b = map(int, act.frame_range)
        for _ in range(passes):
            bad = 0
            for f in range(a, b + 1):
                z = min_z_at(mesh, f)
                if z < tol:
                    bad += 1
                    pb.location = pb.location + inv @ Vector((0, 0, -z + 0.001))
                    pb.keyframe_insert("location", frame=f)
            if not bad: break
    ad.action = None
    for t in ad.nla_tracks: t.mute = False

"""Padrao de estilo PBR (lote 3): malha alta detalhada -> bake para a malha de jogo.
Gera 3 texturas por peca: cor (com AO, sujeira nas cavidades e desgaste nas quinas), rugosidade e normal.
Uso tipico:
    hi = pbr.make_hi(low)                         # copia da malha antes do decimate
    pbr.detail(hi, plates=.., grain=.., barnacles=..)
    org.decimate(low, alvo)
    pbr.bake(low, hi, cor_fn, rug_fn, nome, 1024)
cor_fn(p, n, curv) -> Vector linear; rug_fn(p, n, curv) -> float 0..1.  curv: >0 quina (convexo), <0 cavidade."""
import bpy, bmesh, math, random
from mathutils import Vector, noise, kdtree
import cz, org

def make_hi(low, name=None, subdiv=1):
    hi = low.copy(); hi.data = low.data.copy(); hi.name = name or low.name + "_hi"
    bpy.context.collection.objects.link(hi)
    for m in list(hi.modifiers): hi.modifiers.remove(m)
    if subdiv:
        s = hi.modifiers.new("sd", 'SUBSURF'); s.levels = subdiv; s.render_levels = subdiv; s.subdivision_type = 'SIMPLE'
        org._apply_all(hi)
    hi.data.shade_smooth()
    return hi

def detail(hi, plates=0.0, plate_scale=7.0, grain=0.0, grain_scale=90.0, barnacles=0, barn_r=(0.012, 0.03),
           barn_mask=None, seed=1, pits=0.0, mask=None):
    """Relevo fino so na malha alta. Tudo em metros (escala 1 da construcao).
    plates: profundidade dos sulcos entre placas (voronoi); grain: granulado; barnacles: n de cracas;
    pits: furinhos (porosidade de cal/pedra); mask(p,n)->0..1 limita onde ha relevo."""
    me = hi.data
    me.calc_normals_split() if hasattr(me, "calc_normals_split") else None
    rnd = random.Random(seed)
    cos = [v.co.copy() for v in me.vertices]; nos = [v.normal.copy() for v in me.vertices]
    off = [0.0] * len(cos)
    for i, (p, n) in enumerate(zip(cos, nos)):
        w = 1.0 if mask is None else mask(p, n)
        if w <= 0: continue
        d = 0.0
        if plates:
            dist, _ = noise.voronoi(p * plate_scale, distance_metric='DISTANCE', exponent=2.5)
            f = dist[1] - dist[0]
            d -= plates * (1 - org.sstep(0.0, 0.09, f))          # sulco entre placas
            d += plates * 0.35 * org.sstep(0.1, 0.5, f)           # placa levemente abaulada
        if grain:
            d += grain * noise.noise(p * grain_scale) + grain * 0.5 * noise.noise(p * grain_scale * 2.3)
        if pits:
            pn = noise.noise(p * 140.0 + Vector((5, 1, 3)))
            if pn > 0.45: d -= pits * org.sstep(0.45, 0.6, pn)
        off[i] = d * w
    if barnacles:
        kd = kdtree.KDTree(len(cos))
        for i, p in enumerate(cos): kd.insert(p, i)
        kd.balance()
        cand = [i for i, (p, n) in enumerate(zip(cos, nos)) if n.z > 0.1 and (barn_mask is None or barn_mask(p, n))]
        for _ in range(barnacles if cand else 0):
            c = cos[rnd.choice(cand)]; r = rnd.uniform(*barn_r); h = r * rnd.uniform(0.8, 1.3)
            for (co, idx, dd) in kd.find_range(c, r * 1.25):
                t = dd / r
                if t < 0.35: prof = h * (0.55 + 0.3 * t)                 # boca afundada
                elif t < 1.0: prof = h * (1 - ((t - 0.35) / 0.65) ** 2) * 1.05   # cone
                else: prof = 0.0
                off[idx] = max(off[idx], prof)
    for v, n, d in zip(me.vertices, nos, off):
        v.co = v.co + n * d
    me.update()

def curvature(o):
    """Curvatura por vertice (positivo = quina/convexo), normalizada ~-1..1 e suavizada."""
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    raw = [0.0] * len(bm.verts)
    for v in bm.verts:
        if not v.link_edges: continue
        s = 0.0; L = 0.0
        for e in v.link_edges:
            w = e.other_vert(v); dv = w.co - v.co
            s += -dv.dot(v.normal); L += dv.length
        raw[v.index] = s / max(L, 1e-9)
    for _ in range(2):
        new = raw[:]
        for v in bm.verts:
            nb = [raw[e.other_vert(v).index] for e in v.link_edges]
            if nb: new[v.index] = 0.5 * raw[v.index] + 0.5 * sum(nb) / len(nb)
        raw = new
    srt = sorted(abs(x) for x in raw); k = srt[int(len(srt) * 0.97)] or 1.0
    bm.free()
    return [max(-1.0, min(1.0, x / k)) for x in raw]

def _paint_attr(o, name, vals):
    me = o.data
    if name in me.color_attributes: me.color_attributes.remove(me.color_attributes[name])
    ca = me.color_attributes.new(name, 'FLOAT_COLOR', 'POINT')
    for i, c in enumerate(vals): ca.data[i].color = (c[0], c[1], c[2], 1.0)
    return ca

def _emit_mat(name, attr):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission"); ca = nt.nodes.new("ShaderNodeVertexColor")
    ca.layer_name = attr; nt.links.new(ca.outputs["Color"], em.inputs["Color"]); nt.links.new(em.outputs[0], out.inputs[0])
    return m

def _bake(low, hi, kind, img, extrusion, margin=8):
    for o in bpy.context.view_layer.objects: o.select_set(False)
    hi.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active = low
    nt = low.data.materials[0].node_tree
    tn = nt.nodes.get("BAKE") or nt.nodes.new("ShaderNodeTexImage"); tn.name = "BAKE"; tn.image = img
    for n in nt.nodes: n.select = False
    tn.select = True; nt.nodes.active = tn
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'
    sc.cycles.samples = 16 if kind == 'AO' else 1
    bpy.ops.object.bake(type=kind, use_selected_to_active=True, cage_extrusion=extrusion, max_ray_distance=extrusion * 2.5,
                        margin=margin, normal_space='TANGENT')

def bake(low, hi, cor_fn, rug_fn, name, size=1024, extrusion=0.03, ao_strength=0.55, wear_col=None, dirt_col=None, metal_fn=None):
    """Pinta cor/rugosidade na malha alta (com curvatura), faz bake de cor, rugosidade, normal e AO na baixa,
    e monta o material PBR final na baixa (exporta como baseColor + metallicRoughness + normal no glTF)."""
    curv = curvature(hi)
    me = hi.data
    cols, rgs = [], []
    zones = None
    if "Zona" in me.color_attributes:
        za = me.color_attributes["Zona"]; zones = [int(round(za.data[i].color[0] * 100)) for i in range(len(me.vertices))]
    for v, c in zip(me.vertices, curv):
        if zones is not None:
            z_ = zones[v.index]
            col = Vector(cor_fn(v.co, v.normal, c, z_)); r = rug_fn(v.co, v.normal, c, z_)
        else:
            col = Vector(cor_fn(v.co, v.normal, c)); r = rug_fn(v.co, v.normal, c)
        if wear_col is not None and c > 0.25: col = org.mix(col, wear_col, org.sstep(0.25, 0.85, c) * 0.6)      # desgaste nas quinas
        if dirt_col is not None and c < -0.2: col = org.mix(col, dirt_col, org.sstep(-0.2, -0.8, c) * 0.7)       # sujeira nas cavidades
        if c < -0.2: r = min(1.0, r + 0.25 * org.sstep(-0.2, -0.8, c))
        cols.append(col); m_ = (metal_fn(v.co, v.normal, c, zones[v.index]) if zones is not None else metal_fn(v.co, v.normal, c)) if metal_fn else 0.0
        rgs.append((r, r, m_))
    _paint_attr(hi, "Col", cols); _paint_attr(hi, "Rgh", rgs)
    # UV da baixa
    for o in bpy.context.view_layer.objects: o.select_set(False)
    low.select_set(True); bpy.context.view_layer.objects.active = low
    if not low.data.uv_layers: low.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    low.data.materials.clear(); tmp = bpy.data.materials.new(name + "_tmp"); tmp.use_nodes = True; low.data.materials.append(tmp)
    imgs = {}
    def new_img(k, noncolor):
        im = bpy.data.images.new(f"{name}_{k}", size, size, float_buffer=False)
        if noncolor: im.colorspace_settings.name = 'Non-Color'
        imgs[k] = im; return im
    hi.data.materials.clear(); hi.data.materials.append(_emit_mat(name + "_emCol", "Col"))
    _bake(low, hi, 'EMIT', new_img("cor", False), extrusion)
    hi.data.materials[0] = _emit_mat(name + "_emRgh", "Rgh")
    _bake(low, hi, 'EMIT', new_img("rug", True), extrusion)
    _bake(low, hi, 'NORMAL', new_img("normal", True), extrusion)
    _bake(low, hi, 'AO', new_img("ao", True), extrusion)
    # AO multiplicada na cor
    import numpy as np
    c = np.array(imgs["cor"].pixels[:], dtype=np.float32).reshape(-1, 4)
    a = np.array(imgs["ao"].pixels[:], dtype=np.float32).reshape(-1, 4)[:, :1]
    c[:, :3] *= (1 - ao_strength) + ao_strength * a
    imgs["cor"].pixels[:] = c.ravel().tolist()
    # rug (R) e metal (B) -> imagem ORM no padrao glTF (G=rug, B=metal)
    r = np.array(imgs["rug"].pixels[:], dtype=np.float32).reshape(-1, 4)
    orm = np.ones_like(r); orm[:, 1] = r[:, 0]; orm[:, 2] = r[:, 2]
    imgs["rug"].pixels[:] = orm.ravel().tolist()
    for k in ("cor", "rug", "normal"): imgs[k].pack()
    bpy.data.images.remove(imgs["ao"])
    # material final
    m = bpy.data.materials.new(name + "_pbr"); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage"); tc.image = imgs["cor"]
    tr = nt.nodes.new("ShaderNodeTexImage"); tr.image = imgs["rug"]
    sp = nt.nodes.new("ShaderNodeSeparateColor")
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = imgs["normal"]
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tc.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(tr.outputs["Color"], sp.inputs["Color"])
    nt.links.new(sp.outputs["Green"], b.inputs["Roughness"]); nt.links.new(sp.outputs["Blue"], b.inputs["Metallic"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    low.data.materials.clear(); low.data.materials.append(m)
    bpy.data.objects.remove(hi, do_unlink=True)
    return m

# ------------------------------------------------------------------ previa na camera do jogo
def _ground_tex_mat(gm, tex_a, tex_b, tile, size):
    """chao texturizado como o TerrainShader do jogo: A e B misturadas por ruido grande, com relevo."""
    nt = gm.node_tree; bsdf = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (size / tile, size / tile, 1)
    nt.links.new(tc.outputs["Generated"], mp.inputs["Vector"])
    def img(path, noncolor=False):
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(path)
        if noncolor: n.image.colorspace_settings.name = 'Non-Color'
        nt.links.new(mp.outputs["Vector"], n.inputs["Vector"]); return n
    ca = img(tex_a); na = img(tex_a[:-4] + "_n.png", True)
    col, nrm = ca.outputs["Color"], na.outputs["Color"]
    if tex_b:
        cb = img(tex_b); nb = img(tex_b[:-4] + "_n.png", True)
        nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = size * 0.03; nz.inputs["Detail"].default_value = 3
        nt.links.new(tc.outputs["Generated"], nz.inputs["Vector"])
        ramp = nt.nodes.new("ShaderNodeValToRGB"); ramp.color_ramp.elements[0].position = 0.48; ramp.color_ramp.elements[1].position = 0.56
        nt.links.new(nz.outputs["Fac"], ramp.inputs["Fac"])
        m1 = nt.nodes.new("ShaderNodeMix"); m1.data_type = 'RGBA'; nt.links.new(ramp.outputs["Color"], m1.inputs[0])
        nt.links.new(col, m1.inputs[6]); nt.links.new(cb.outputs["Color"], m1.inputs[7]); col = m1.outputs[2]
        m2 = nt.nodes.new("ShaderNodeMix"); m2.data_type = 'RGBA'; nt.links.new(ramp.outputs["Color"], m2.inputs[0])
        nt.links.new(nrm, m2.inputs[6]); nt.links.new(nb.outputs["Color"], m2.inputs[7]); nrm = m2.outputs[2]
    nm = nt.nodes.new("ShaderNodeNormalMap"); nm.inputs["Strength"].default_value = 1.2
    nt.links.new(nrm, nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    nt.links.new(col, bsdf.inputs["Base Color"])

def game_shot(path, objs, ground_hex="#c9b48c", res=(960, 540), samples=24, dist=8.5, pitch_deg=35.5, fov=62.0, focus_h=1.55,
              ahead=2.2, crop64=None, ground_tex=None, sky=None, sun_cfg=None):
    """Imita a ThirdPersonCamera: alvo a 1,55 m (heroi), 8,5 m atras, 35,5 graus de cima; criaturas 'ahead' m a frente do heroi.
    objs: lista de (objetos, x_offset, yaw_graus)."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples
    try: sc.cycles.use_denoising = True
    except Exception: pass
    sc.render.resolution_x, sc.render.resolution_y = res; sc.render.film_transparent = False
    sc.view_settings.view_transform = 'AgX' if 'AgX' in [x.identifier for x in sc.view_settings.bl_rna.properties['view_transform'].enum_items] else 'Standard'
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.62, 0.75, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    gm = bpy.data.materials.new("chao"); gm.use_nodes = True; gb = gm.node_tree.nodes["Principled BSDF"]
    gc = org.lin(ground_hex); gb.inputs["Base Color"].default_value = (*gc, 1); gb.inputs["Roughness"].default_value = 0.95
    ns = gm.node_tree.nodes.new("ShaderNodeTexNoise"); ns.inputs["Scale"].default_value = 1.5
    mr = gm.node_tree.nodes.new("ShaderNodeMix"); mr.data_type = 'RGBA'; mr.inputs[0].default_value = 0.25
    mr.inputs[6].default_value = (*gc, 1); mr.inputs[7].default_value = (*(gc * 0.8), 1)
    gm.node_tree.links.new(ns.outputs["Fac"], mr.inputs[0]); gm.node_tree.links.new(mr.outputs[2], gb.inputs["Base Color"])
    gsize = 200 if ground_tex else 80
    if ground_tex: _ground_tex_mat(gm, ground_tex[0], ground_tex[1], ground_tex[2], gsize)
    bpy.ops.mesh.primitive_plane_add(size=gsize); g = bpy.context.object; g.data.materials.append(gm)
    if sky: w.node_tree.nodes["Background"].inputs[0].default_value = (*sky, 1)
    sun = bpy.data.objects.new("sol", bpy.data.lights.new("sol", 'SUN')); bpy.context.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.rotation_euler = (math.radians(42), 0, math.radians(-35)); sun.data.angle = math.radians(3)
    if sun_cfg:
        sun.data.energy = sun_cfg.get("energy", 3.2); sun.data.color = sun_cfg.get("color", (1, 1, 1))
        sun.rotation_euler = (math.radians(sun_cfg.get("elev", 42)), 0, math.radians(sun_cfg.get("az", -35)))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); bpy.context.collection.objects.link(cam); sc.camera = cam
    cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(fov)
    pitch = math.radians(pitch_deg)
    focus = Vector((0, 0, focus_h))
    cam.location = focus + Vector((0, math.cos(pitch) * dist, math.sin(pitch) * dist))     # camera atras (+Y), olhando para -Y
    cam.rotation_euler = (math.radians(90) - pitch, 0, math.radians(180))
    for group, x, yaw in objs:
        for o in group:
            if o.parent is None:
                o.location = Vector((x, -ahead, 0)); o.rotation_euler = (0, 0, math.radians(yaw))
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
    return path


def zone(o, z):
    """Marca todos os vertices de uma peca com a zona z (0..99) antes de juntar; o bake passa a zona para cor/rug/metal."""
    me = o.data
    ca = me.color_attributes.get("Zona") or me.color_attributes.new("Zona", 'FLOAT_COLOR', 'POINT')
    for i in range(len(me.vertices)): ca.data[i].color = (z / 100.0, 0, 0, 1)
    return o

def detail_zoned(hi, per_zone, seed=1):
    """Relevo por zona: per_zone = {z: dict(grain=.., plates=.., pits=.., barnacles=..)}; vertices de outras zonas ficam intactos."""
    me = hi.data; za = me.color_attributes["Zona"]
    zones = [int(round(za.data[i].color[0] * 100)) for i in range(len(me.vertices))]
    for z, opts in per_zone.items():
        keep = [v.co.copy() for v in me.vertices]
        detail(hi, seed=seed + z, **opts)
        for i, v in enumerate(me.vertices):
            if zones[i] != z: v.co = keep[i]
    me.update()

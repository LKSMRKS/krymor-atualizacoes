"""Refaz o material dos predios/props do lote 2 com PBR (sem mudar forma, nos, nomes nem marcadores spawn_*).
Uso: python3 rebake_lote2.py <entrada.glb> <saida.glb> [--ruina]
Arquitetura tem paredes retas com poucos vertices: em vez de pintar por vertice, cada material vira um MATERIAL PROCEDURAL
(alvenaria com argamassa, reboco manchado com tijolo a mostra, telha em fiadas, madeira com veio, ferro com ferrugem...)
em coordenadas do mundo (metros) e e ASSADO na textura do proprio predio: cor, rugosidade, metal, normal (relevo) e AO.
Faces emissivas (janelas acesas, velas, fogo) e materiais desconhecidos ficam como estao.
--ruina: remove cacos soltos pequenos flutuando (defeito apontado na lista de arte) e suja/escurece o conjunto."""
import sys, math, os
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh, cz, org
from mathutils import Vector

src, dst = sys.argv[1], sys.argv[2]; RUINA = "--ruina" in sys.argv
KEYS = [("pedra_escura", "pedra2"), ("pedra_clara", "pedra_clara"), ("pedra", "pedra"), ("assoalho", "assoalho"), ("madeira_escura", "madeira2"),
        ("madeira", "madeira"), ("tabua", "madeira"), ("viga", "madeira2"), ("ferro", "ferro"), ("metal", "ferro"), ("reboco", "reboco"), ("parede", "reboco"),
        ("telha", "telha"), ("tapete", "pano"), ("lencol", "pano_claro"), ("cobertor", "pano"), ("pano", "pano"), ("tecido", "pano"), ("lona", "pano_claro"),
        ("ouro", "ouro"), ("corda", "corda"), ("couro", "couro"), ("osso", "osso"), ("cal", "pedra_clara"), ("estandarte", "pano")]

def kind_of(mat):
    if mat is None: return None
    n = mat.name.lower()
    if "emit" in n or "luz" in n or "vidro" in n or "garrafa" in n or "agua" in n or "fogo" in n: return None
    for k, z in KEYS:
        if k in n: return z
    return None

def rgb(h): c = org.lin(h); return (c[0], c[1], c[2], 1)

class NB:
    """atalho para montar nos"""
    def __init__(s, mat): s.nt = mat.node_tree; s.N = s.nt.nodes; s.L = s.nt.links
    def n(s, t, **kw):
        x = s.N.new(t)
        for k, v in kw.items():
            if k.startswith("i_"): x.inputs[k[2:].replace("_", " ")].default_value = v
            else: setattr(x, k, v)
        return x
    def l(s, a, b): s.L.new(a, b)
    def math(s, op, a, b=None, v2=None):
        m = s.n("ShaderNodeMath", operation=op)
        (s.l(a, m.inputs[0]) if not isinstance(a, (int, float)) else m.inputs[0].__setattr__("default_value", a))
        if b is not None: (s.l(b, m.inputs[1]) if not isinstance(b, (int, float)) else m.inputs[1].__setattr__("default_value", b))
        return m.outputs[0]
    def mixc(s, fac, a, b):
        m = s.n("ShaderNodeMix", data_type='RGBA')
        for slot, v in ((0, fac), (6, a), (7, b)):
            if isinstance(v, (int, float)): m.inputs[slot].default_value = v
            elif isinstance(v, tuple): m.inputs[slot].default_value = v
            else: s.l(v, m.inputs[slot])
        return m.outputs[2]
    def ramp(s, fac, p0, p1):
        r = s.n("ShaderNodeMapRange", clamp=True); s.l(fac, r.inputs[0]); r.inputs[1].default_value = p0; r.inputs[2].default_value = p1
        return r.outputs[0]
    def noise(s, vec, scale, detail=3, rough=0.55):
        x = s.n("ShaderNodeTexNoise", i_Scale=scale, i_Detail=detail, i_Roughness=rough); s.l(vec, x.inputs["Vector"]); return x.outputs["Fac"]

def proc_material(kind):
    """material procedural em metros (posicao do mundo). Devolve (material, metal)"""
    m = bpy.data.materials.new("proc_" + kind); m.use_nodes = True
    b = NB(m); bsdf = b.N["Principled BSDF"]
    geo = b.n("ShaderNodeNewGeometry"); P = geo.outputs["Position"]; Nn = geo.outputs["Normal"]
    sep = b.n("ShaderNodeSeparateXYZ"); b.l(P, sep.inputs[0])
    X, Y, Zc = sep.outputs[0], sep.outputs[1], sep.outputs[2]
    nsep = b.n("ShaderNodeSeparateXYZ"); b.l(Nn, nsep.inputs[0])
    wall = b.ramp(b.math('ABSOLUTE', nsep.outputs[2]), 0.7, 0.4)          # 1 = parede vertical, 0 = piso/teto
    H = b.math('ADD', X, b.math('MULTIPLY', Y, 0.83))                       # coordenada horizontal ao longo da parede
    uv = b.n("ShaderNodeCombineXYZ"); b.l(H, uv.inputs[0]); b.l(Zc, uv.inputs[1])
    UV = uv.outputs[0]
    base_dirt = b.ramp(Zc, 0.8, 0.0)                                         # barro na base das paredes
    height = None; metal = 0.0; rough = 0.85
    if kind in ("pedra", "pedra2", "pedra_clara"):
        c1, c2, mortar = {"pedra": ("#8a8276", "#a89e8c", "#4e4a42"), "pedra2": ("#5e574e", "#76705f", "#2e2a26"),
                          "pedra_clara": ("#c8c0ae", "#e0d8c6", "#7a7262")}[kind]
        br = b.n("ShaderNodeTexBrick", offset=0.5, i_Scale=1.0, i_Mortar_Size=0.022, i_Mortar_Smooth=0.3, i_Brick_Width=0.62, i_Row_Height=0.34, i_Bias=0.0)
        br.inputs["Color1"].default_value = rgb(c1); br.inputs["Color2"].default_value = rgb(c2); br.inputs["Mortar"].default_value = rgb(mortar)
        b.l(UV, br.inputs["Vector"])
        var = b.noise(P, 1.6, 4)
        col = b.mixc(b.math('MULTIPLY', var, 0.35), br.outputs["Color"], rgb("#3a362e"))
        moss = b.math('MULTIPLY', b.ramp(b.noise(P, 0.9, 3), 0.55, 0.7), b.math('MAXIMUM', base_dirt, b.ramp(nsep.outputs[2], 0.6, 1.0)))
        col = b.mixc(b.math('MULTIPLY', moss, 0.75), col, rgb("#4e6a2e"))
        col = b.mixc(b.math('MULTIPLY', base_dirt, 0.45), col, rgb("#3e3628"))
        height = b.math('SUBTRACT', 1.0, br.outputs["Fac"]); rough = 0.88
        chips = b.ramp(b.noise(P, 6, 2), 0.62, 0.72)
        height = b.math('SUBTRACT', height, b.math('MULTIPLY', chips, 0.4))
    elif kind == "reboco":
        cal = b.mixc(b.noise(P, 0.8, 2), rgb("#d4c8ae"), rgb("#e8e0cc"))
        stain_v = b.n("ShaderNodeTexNoise", i_Scale=1.0, i_Detail=2); vs = b.n("ShaderNodeMapping"); vs.inputs["Scale"].default_value = (6, 6, 0.6)
        b.l(P, vs.inputs[0]); b.l(vs.outputs[0], stain_v.inputs["Vector"])
        cal = b.mixc(b.math('MULTIPLY', b.ramp(stain_v.outputs["Fac"], 0.55, 0.75), 0.55), cal, rgb("#8a7a5c"))   # escorridos
        br = b.n("ShaderNodeTexBrick", offset=0.5, i_Scale=1.0, i_Mortar_Size=0.012, i_Brick_Width=0.24, i_Row_Height=0.085)
        br.inputs["Color1"].default_value = rgb("#8e4a30"); br.inputs["Color2"].default_value = rgb("#b06a48"); br.inputs["Mortar"].default_value = rgb("#cfc4ae")
        b.l(UV, br.inputs["Vector"])
        expo = b.math('MULTIPLY', b.ramp(b.noise(P, 0.7, 4, 0.65), 0.6, 0.64), wall)            # reboco caido -> tijolo
        col = b.mixc(expo, cal, br.outputs["Color"])
        col = b.mixc(b.math('MULTIPLY', base_dirt, 0.55), col, rgb("#6a5a40"))
        crack = b.ramp(b.math('ABSOLUTE', b.math('SUBTRACT', b.noise(P, 1.4, 3), 0.5)), 0.008, 0.0)
        col = b.mixc(b.math('MULTIPLY', crack, 0.7), col, rgb("#4a4034"))
        height = b.math('ADD', b.math('MULTIPLY', b.math('SUBTRACT', 1.0, expo), 0.6), b.math('MULTIPLY', b.math('SUBTRACT', 1.0, br.outputs["Fac"]), b.math('MULTIPLY', expo, 0.3)))
        height = b.math('SUBTRACT', height, b.math('MULTIPLY', crack, 0.3)); rough = 0.92
    elif kind == "telha":
        wv = b.n("ShaderNodeTexWave", wave_type='BANDS', bands_direction='Z', wave_profile='SAW', i_Scale=1.0, i_Distortion=0.4)
        sc = b.n("ShaderNodeMapping"); sc.inputs["Scale"].default_value = (0.0, 0.0, 1 / 0.16 / 6.28 * 6.28); b.l(P, sc.inputs[0])
        sc.inputs["Scale"].default_value = (0.02, 0.02, 1.0); b.l(sc.outputs[0], wv.inputs["Vector"]); wv.inputs["Scale"].default_value = 6.25
        cols_ = b.n("ShaderNodeTexWave", wave_type='BANDS', bands_direction='X', wave_profile='SIN', i_Scale=1.0)
        b.l(UV, cols_.inputs["Vector"]); cols_.inputs["Scale"].default_value = 18.0
        row_var = b.noise(P, 3.0, 2)
        col = b.mixc(wv.outputs["Fac"], rgb("#5e2618"), rgb("#a84e32"))
        col = b.mixc(b.math('MULTIPLY', row_var, 0.4), col, rgb("#7a3a22"))
        col = b.mixc(b.math('MULTIPLY', b.ramp(b.noise(P, 0.8, 3), 0.58, 0.72), 0.6), col, rgb("#5a6a3a"))      # liquen
        height = b.math('ADD', wv.outputs["Fac"], b.math('MULTIPLY', cols_.outputs["Fac"], 0.35)); rough = 0.7
    elif kind in ("madeira", "madeira2", "assoalho"):
        c1, c2 = {"madeira": ("#5e3e24", "#8e6440"), "madeira2": ("#2e1e14", "#4e3420"), "assoalho": ("#5a3c24", "#7e5634")}[kind]
        wv = b.n("ShaderNodeTexWave", wave_type='BANDS', bands_direction='Z', wave_profile='SIN', i_Distortion=6.0, i_Detail=3, i_Detail_Scale=1.5)
        mp = b.n("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (3, 3, 0.25); b.l(P, mp.inputs[0]); b.l(mp.outputs[0], wv.inputs["Vector"])
        wv.inputs["Scale"].default_value = 4.0
        col = b.mixc(wv.outputs["Fac"], rgb(c1), rgb(c2))
        col = b.mixc(b.math('MULTIPLY', b.ramp(b.noise(P, 0.6, 3), 0.55, 0.75), 0.35), col, rgb("#8a8478"))      # madeira acinzentada
        if kind == "assoalho":
            pl = b.n("ShaderNodeTexBrick", offset=0.0, i_Scale=1.0, i_Mortar_Size=0.006, i_Brick_Width=2.2, i_Row_Height=0.18)
            pl.inputs["Color1"].default_value = (1, 1, 1, 1); pl.inputs["Color2"].default_value = (0.8, 0.8, 0.8, 1); pl.inputs["Mortar"].default_value = (0.2, 0.2, 0.2, 1)
            xy = b.n("ShaderNodeCombineXYZ"); b.l(X, xy.inputs[0]); b.l(Y, xy.inputs[1]); b.l(xy.outputs[0], pl.inputs["Vector"])
            col = b.mixc(0.5, col, b.mixc(1.0, col, col)); mulc = b.n("ShaderNodeMix", data_type='RGBA', blend_type='MULTIPLY'); mulc.inputs[0].default_value = 1.0
            b.l(col, mulc.inputs[6]); b.l(pl.outputs["Color"], mulc.inputs[7]); col = mulc.outputs[2]
        height = b.math('MULTIPLY', wv.outputs["Fac"], 0.5); rough = 0.82
    elif kind == "ferro":
        col = b.mixc(b.noise(P, 8, 3), rgb("#2a2826"), rgb("#5e5c5a"))
        rust = b.ramp(b.noise(P, 3, 4), 0.55, 0.68)
        col = b.mixc(rust, col, rgb("#7a3a1a")); height = b.math('MULTIPLY', rust, 0.3); rough = 0.5; metal = 0.75
    elif kind in ("pano", "pano_claro"):
        col = b.mixc(b.noise(P, 30, 2), rgb("#6a2a2a" if kind == "pano" else "#c8bca0"), rgb("#8a3a34" if kind == "pano" else "#e0d6c0"))
        col = b.mixc(b.math('MULTIPLY', b.ramp(b.noise(P, 1.5, 3), 0.6, 0.75), 0.5), col, rgb("#3a2a20"))
        height = b.noise(P, 60, 1); rough = 0.95
    elif kind == "ouro":
        col = b.mixc(b.noise(P, 10, 2), rgb("#8a6a20"), rgb("#e8b840")); rough = 0.3; metal = 1.0; height = b.noise(P, 20, 2)
    else:   # corda, couro, osso
        c1, c2 = {"corda": ("#6a5638", "#b8a070"), "couro": ("#3a2416", "#6a4a2e"), "osso": ("#9a8a6a", "#e6dcc0")}[kind]
        col = b.mixc(b.noise(P, 12, 3), rgb(c1), rgb(c2)); height = b.noise(P, 30, 2); rough = 0.8
    if RUINA:   # ruina: tudo mais sujo e escuro, fuligem
        soot = b.ramp(b.noise(P, 0.5, 4), 0.45, 0.65)
        col = b.mixc(b.math('MULTIPLY', soot, 0.6), col, rgb("#1e1a16"))
    b.l(col, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = rough; bsdf.inputs["Metallic"].default_value = metal
    if height is not None:
        bump = b.n("ShaderNodeBump", i_Strength=0.6, i_Distance=0.02); b.l(height, bump.inputs["Height"]); b.l(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return m, metal, rough

def islands(me):
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen = set(); out = []
    for v in bm.verts:
        if v.index in seen: continue
        stack = [v]; isl = []; seen.add(v.index)
        while stack:
            x = stack.pop(); isl.append(x.index)
            for e in x.link_edges:
                w = e.other_vert(x)
                if w.index not in seen: seen.add(w.index); stack.append(w)
        out.append(isl)
    bm.free(); return out

def bake_obj(ob, size):
    """assa os materiais procedurais do objeto numa textura unica e troca pelo material PBR final"""
    import numpy as np
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'
    for o in bpy.context.view_layer.objects: o.select_set(False)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    if not ob.data.uv_layers: ob.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')
    imgs = {}
    def img(k, noncolor):
        im = bpy.data.images.new(f"{ob.name}_{k}", size, size)
        if noncolor: im.colorspace_settings.name = 'Non-Color'
        imgs[k] = im; return im
    def target(im):
        for m in ob.data.materials:
            nt = m.node_tree; t = nt.nodes.get("BAKE") or nt.nodes.new("ShaderNodeTexImage"); t.name = "BAKE"; t.image = im
            for n in nt.nodes: n.select = False
            t.select = True; nt.nodes.active = t
    target(img("cor", False)); sc.cycles.samples = 1
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=6)
    target(img("rug", True)); bpy.ops.object.bake(type='ROUGHNESS', margin=6)
    target(img("normal", True)); bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', margin=6)
    target(img("ao", True)); sc.cycles.samples = 12; bpy.ops.object.bake(type='AO', margin=6)
    # metal: emissao temporaria com o valor de metal de cada material
    for m in ob.data.materials:
        bs = m.node_tree.nodes["Principled BSDF"]; v = bs.inputs["Metallic"].default_value
        bs.inputs["Emission Color"].default_value = (v, v, v, 1); bs.inputs["Emission Strength"].default_value = 1.0
    target(img("metal", True)); sc.cycles.samples = 1; bpy.ops.object.bake(type='EMIT', margin=6)
    c = np.array(imgs["cor"].pixels[:], dtype=np.float32).reshape(-1, 4)
    a = np.array(imgs["ao"].pixels[:], dtype=np.float32).reshape(-1, 4)[:, :1]
    c[:, :3] *= 0.45 + 0.55 * a; imgs["cor"].pixels[:] = c.ravel().tolist()
    r = np.array(imgs["rug"].pixels[:], dtype=np.float32).reshape(-1, 4); mt = np.array(imgs["metal"].pixels[:], dtype=np.float32).reshape(-1, 4)
    orm = np.ones_like(r); orm[:, 1] = r[:, 0]; orm[:, 2] = mt[:, 0]; imgs["rug"].pixels[:] = orm.ravel().tolist()
    for k in ("cor", "rug", "normal"): imgs[k].pack()
    m = bpy.data.materials.new(ob.name + "_pbr"); m.use_nodes = True; nt = m.node_tree; bs = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage"); tc.image = imgs["cor"]
    tr = nt.nodes.new("ShaderNodeTexImage"); tr.image = imgs["rug"]; sp = nt.nodes.new("ShaderNodeSeparateColor")
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = imgs["normal"]; nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tc.outputs["Color"], bs.inputs["Base Color"]); nt.links.new(tr.outputs["Color"], sp.inputs["Color"])
    nt.links.new(sp.outputs["Green"], bs.inputs["Roughness"]); nt.links.new(sp.outputs["Blue"], bs.inputs["Metallic"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], bs.inputs["Normal"])
    ob.data.materials.clear(); ob.data.materials.append(m)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
PROC = {}
report = []
for ob in meshes:
    name, parent, mw = ob.name, ob.parent, ob.matrix_world.copy()
    if RUINA:
        # caco = peca pequena que NAO encosta em nenhuma outra (o predio e feito de muitas pecas pequenas encostadas)
        me = ob.data; rm = []
        isls = islands(me); boxes = []
        for isl in isls:
            cs = [mw @ me.vertices[i].co for i in isl]
            boxes.append((Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs))), Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))))
        cell = 1.0; grid = {}
        for k, (mn, mx) in enumerate(boxes):
            for gx in range(int(mn.x // cell), int(mx.x // cell) + 1):
                for gy in range(int(mn.y // cell), int(mx.y // cell) + 1):
                    for gz in range(int(mn.z // cell), int(mx.z // cell) + 1): grid.setdefault((gx, gy, gz), []).append(k)
        for k, (mn, mx) in enumerate(boxes):
            if (mx - mn).length > 0.45 or mn.z < 0.25: continue
            e = 0.03; touch = False
            for gx in range(int((mn.x - e) // cell), int((mx.x + e) // cell) + 1):
                for gy in range(int((mn.y - e) // cell), int((mx.y + e) // cell) + 1):
                    for gz in range(int((mn.z - e) // cell), int((mx.z + e) // cell) + 1):
                        for j in grid.get((gx, gy, gz), ()):
                            if j == k: continue
                            a0, a1 = boxes[j]
                            if a0.x <= mx.x + e and a1.x >= mn.x - e and a0.y <= mx.y + e and a1.y >= mn.y - e and a0.z <= mx.z + e and a1.z >= mn.z - e:
                                touch = True; break
                        if touch: break
                    if touch: break
                if touch: break
            if not touch: rm += isls[k]
        if rm:
            bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
            bmesh.ops.delete(bm, geom=[bm.verts[i] for i in set(rm)], context='VERTS'); bm.to_mesh(me); bm.free()
            report.append(f"{name}: {len(set(rm))} vertices de cacos soltos removidos")
    kinds = [kind_of(m) for m in ob.data.materials]
    if all(k is None for k in kinds): continue
    keep_idx = [i for i, k in enumerate(kinds) if k is None]
    for o in bpy.context.view_layer.objects: o.select_set(False)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    keep = None
    if keep_idx and any(p.material_index in keep_idx for p in ob.data.polygons):
        keep = ob.copy(); keep.data = ob.data.copy(); bpy.context.collection.objects.link(keep)
        for tgt, drop_keep in ((keep, False), (ob, True)):      # keep: so faces emissivas; ob: o resto
            bm = bmesh.new(); bm.from_mesh(tgt.data)
            dead = [f for f in bm.faces if (f.material_index in keep_idx) == drop_keep]
            bmesh.ops.delete(bm, geom=dead, context='FACES'); bm.to_mesh(tgt.data); bm.free()
        # tira os slots que nao sao usados mais (procedurais ficam so no corpo principal)
    # troca cada material conhecido pelo procedural correspondente
    ob.parent = None; ob.matrix_world = mw; cz.apply_tf(ob)        # coordenadas do mundo (a textura depende da altura real)
    for i, k in enumerate(kinds):
        if k is None: continue
        if k not in PROC: PROC[k] = proc_material(k)[0]
        ob.data.materials[i] = PROC[k]
    used = sorted(set(p.material_index for p in ob.data.polygons))
    # remove slots vazios (sem faces) para o bake nao tocar materiais emissivos
    for i in reversed(range(len(ob.data.materials))):
        if i not in used:
            ob.active_material_index = i; bpy.ops.object.material_slot_remove()
    nf = len(ob.data.polygons)
    size = 2048 if nf > 5000 else 1024 if nf > 600 else 512
    bake_obj(ob, size)
    if keep is not None:
        keep.parent = None; keep.matrix_world = mw; cz.apply_tf(keep)
        for o in bpy.context.view_layer.objects: o.select_set(False)
        keep.select_set(True); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.object.join()
    ob.data.transform(mw.inverted()); ob.matrix_world = mw
    if parent is not None: ob.parent = parent; ob.matrix_world = mw
    ob.name = name
    report.append(f"{name}: {nf} faces, textura {size}")
for o in bpy.data.objects: o.select_set(True)
os.makedirs(os.path.dirname(dst) or ".", exist_ok=True)
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_yup=True, export_apply=False, export_materials='EXPORT')
print("REBAKE", os.path.basename(dst)); print("\n".join(report))

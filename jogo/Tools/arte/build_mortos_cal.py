"""Monstros da cal no corpo do personagem principal (manequim_masc, mesmo esqueleto/clipes):
  mo_carnical_cal  - Carnical Caustico: corpo seco, pele de gesso rachada, cranio com orbitas fundas e dentes, garras longas,
                     restos de calca de mineiro e capacete amassado.
  mo_fantasma_cal  - Fantasma de Calcario: silhueta de poeira branca translucida que some da cintura para baixo, dois olhos vazios.
Uso: python3 build_mortos_cal.py <saida> [ids...]"""
import sys, os, math, random, json
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh
from mathutils import Vector, Matrix
import itemkit as K, org

OUT = sys.argv[1]; SEL = sys.argv[2:]
MAN = "/mnt/user-data/uploads/CinzasDaMare - Copia/Client/Assets/Models/manequim_masc.glb"
HEAD = {"mixamorig:Head", "mixamorig:HeadTop_End", "headfront"}

def dominant(v, dl, names):
    if not v[dl]: return None
    gi, _ = max(v[dl].items(), key=lambda kv: kv[1]); return names.get(gi)

def keep_faces(o, pred):
    names = {g.index: g.name for g in o.vertex_groups}
    bm = bmesh.new(); bm.from_mesh(o.data); dl = bm.verts.layers.deform.active
    dead = [f for f in bm.faces if not pred(f, [dominant(v, dl, names) for v in f.verts])]
    bmesh.ops.delete(bm, geom=dead, context='FACES'); bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free()

def skin_to(o, arm, weights_fn):
    mw = o.matrix_world.copy(); o.parent = arm; o.matrix_world = mw; groups = {}
    for v in o.data.vertices:
        for b, w in weights_fn(mw @ v.co).items():
            if b not in groups: groups[b] = o.vertex_groups.get(b) or o.vertex_groups.new(name=b)
            groups[b].add([v.index], w, 'REPLACE')
    m = o.modifiers.new("arm", 'ARMATURE'); m.object = arm; return o

def offset_normal(o, d_world, vg=None):
    ls = 1.0 / o.matrix_world.to_scale().x
    m = o.modifiers.new("off", 'DISPLACE'); m.direction = 'NORMAL'; m.mid_level = 0.0; m.strength = d_world * ls
    if vg: m.vertex_group = vg
    bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_move_to_index(modifier="off", index=0); bpy.ops.object.modifier_apply(modifier="off")

def M_gesso(name, k=1.0):
    b = K.NB(name)
    n1 = b.noise(6 * k, 8, 0.6); cr = b.voronoi(18 * k, 'DISTANCE_TO_EDGE'); cr2 = b.voronoi(55 * k, 'DISTANCE_TO_EDGE')
    crack = b.math('ADD', b.math('LESS_THAN', cr, 0.025), b.math('MULTIPLY', b.math('LESS_THAN', cr2, 0.02), 0.6))
    c = b.ramp(n1, [(0.25, "#9a958a"), (0.6, "#d6d0c2"), (0.9, "#efeae0")])
    c = b.mix(b.math('MULTIPLY', crack, 0.9, clamp=True), c, "#2a2620")
    c = b.mix(b.math('MULTIPLY', K.cavity(b, 0.05), 1.7, clamp=True), c, "#3a352c")
    dirt = b.math('GREATER_THAN', b.noise(3 * k, 5), 0.62); c = b.mix(b.math('MULTIPLY', dirt, 0.45), c, "#7a6a54")
    h = b.math('SUBTRACT', b.math('MULTIPLY', b.noise(80 * k, 4), 0.3), b.math('MULTIPLY', crack, 0.8))
    return b.set(base=c, rough=0.92, normal=b.bump(h, 0.6, 0.003))

def M_poeira(name):
    b = K.NB(name)
    n1 = b.noise(4, 6, 0.6, dist=2.0); grain = b.noise(160, 2)
    c = b.ramp(n1, [(0.3, "#b8b4ac"), (0.8, "#f4f2ec")])
    # transparencia: mais denso no tronco e na cabeca, some embaixo (coordenada Z do objeto) e nas bordas (fresnel)
    xyz = b.n("ShaderNodeSeparateXYZ"); b.l(b.obj(), xyz.inputs[0])
    lw = b.n("ShaderNodeLayerWeight", i_Blend=0.5)
    a = b.math('MULTIPLY', b.math('SUBTRACT', 1.0, lw.outputs["Facing"]), 1.3)
    a = b.math('MULTIPLY', a, b.math('ADD', 0.45, b.math('MULTIPLY', n1, 0.7)))
    a = b.math('MULTIPLY', a, b.math('ADD', 0.6, b.math('MULTIPLY', grain, 0.5)))
    m = b.set(base=c, rough=1.0, emit_col="#d8dce8", emit=0.25, alpha=a)
    try: m.surface_render_method = 'BLENDED'
    except Exception: pass
    return m

def importar():
    K.reset(); bpy.ops.import_scene.gltf(filepath=MAN)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE'); body = next(o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("char1"))
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and o.name.startswith("Ico"): bpy.data.objects.remove(o)
    bm_ = bmesh.new(); bm_.from_mesh(body.data); bmesh.ops.remove_doubles(bm_, verts=bm_.verts, dist=0.05); bm_.to_mesh(body.data); bm_.free()   # solda: o glb vem com faces soltas
    arm.data.pose_position = 'REST'; bpy.context.view_layer.update()
    P = {b.name.replace("mixamorig:", ""): arm.matrix_world @ b.head_local for b in arm.data.bones}
    T = {b.name.replace("mixamorig:", ""): arm.matrix_world @ b.tail_local for b in arm.data.bones}
    return arm, body, P, T

def exportar(vid, arm, meshes, info):
    arm.data.pose_position = 'POSE'
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in [arm] + meshes: o.select_set(True)
    os.makedirs(f"{OUT}/{vid}", exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=f"{OUT}/{vid}/{vid}.glb", use_selection=True, export_format='GLB', export_yup=True, export_apply=False,
                              export_animations=True, export_animation_mode='ACTIONS', export_skins=True)
    bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/{vid}/{vid}.blend")
    info.update(asset_id=vid, tris=sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons), clips=[a.name for a in bpy.data.actions])
    json.dump(info, open(f"{OUT}/{vid}/{vid}_info.json", "w"), indent=1, ensure_ascii=False); print("OK", vid, info, flush=True)

def cranio(P, mat, dark, teeth_m):
    """cabeca de caveira-mumia no lugar da cabeca-ovo (rigida no osso Head)"""
    h = P["Head"] + Vector((0, -0.01, 0.1)); parts = []
    parts += [org.blob("cranio", h + Vector((0, 0.01, 0.03)), (0.092, 0.11, 0.105)), org.blob("face", h + Vector((0, -0.06, -0.03)), (0.07, 0.06, 0.08)),
              org.blob("mandibula", h + Vector((0, -0.055, -0.1)), (0.055, 0.05, 0.035))]
    for s in (-1, 1): parts.append(org.blob(f"maca{s}", h + Vector((s * 0.05, -0.07, -0.02)), (0.028, 0.025, 0.022)))
    parts.append(org.limb("pescoco_seco", [(P["Neck"] + Vector((0, 0.0, 0.0)), 0.045), (h + Vector((0, 0.0, -0.08)), 0.04)]))
    sk = org.fuse("cranio", parts, voxel=0.005); org.displace(sk, 0.003, 0.02, 'CLOUDS'); sk.data.materials.append(mat)
    extra = []
    for s in (-1, 1):      # orbitas fundas
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.024, location=h + Vector((s * 0.033, -0.105, 0.0)), segments=16, ring_count=8); e = bpy.context.object; e.data.transform(Matrix.Diagonal((1, 0.6, 0.85, 1.0))); e.data.materials.append(dark); extra.append(e)
    bpy.ops.mesh.primitive_cone_add(vertices=3, radius1=0.012, depth=0.02, location=h + Vector((0, -0.125, -0.04)), rotation=(math.radians(-90), 0, 0)); n = bpy.context.object; n.data.materials.append(dark); extra.append(n)
    for k in range(8):
        x = -0.03 + k * 0.0086
        bpy.ops.mesh.primitive_cube_add(size=1, location=h + Vector((x, -0.108 + 0.004 * abs(k - 3.5) / 3.5, -0.075))); t_ = bpy.context.object; t_.data.transform(Matrix.Diagonal((0.0072, 0.006, 0.016, 1.0))); t_.data.materials.append(teeth_m); extra.append(t_)
    return [sk] + extra

def carnical():
    arm, body, P, T = importar()
    keep_faces(body, lambda f, doms: not any(d in HEAD for d in doms))
    # corpo seco: afina tudo (menos maos), costela marcada pelo material
    offset_normal(body, -0.012)
    pele = M_gesso("pele_gesso", body.matrix_world.to_scale().x); pele_p = M_gesso("pele_gesso_partes", 1.0)
    body.data.materials.clear(); body.data.materials.append(pele)
    for p in body.data.polygons: p.use_smooth = True
    dark = K.m_plain("orbita", "#050403", 1.0); teeth = K.m_bone("dente_carnical", "#d8ccb0", "#6a5a40", 30)
    head_parts = cranio(P, pele_p, dark, teeth)
    for o in head_parts:
        K.apply_all(o); skin_to(o, arm, lambda p: {"mixamorig:Head": 1.0})
    parts = list(head_parts)
    # garras longas de pedra calcaria saindo das pontas das maos
    claw = K.m_bone("garra_cal", "#e8e2d4", "#7a7060", 20)
    for side in ("Left", "Right"):
        hd, tl = P[side + "Hand"], T[side + "Hand"]; d = (tl - hd).normalized(); sgn = 1 if side == "Left" else -1
        for k in range(4):
            off = Vector((0, -0.03 + k * 0.02, -0.005 * abs(k - 1.5)))
            base = tl + d * 0.06 + off; tip = base + (d * 0.13 + Vector((0, 0, -0.05)))
            c = org.spike(f"garra{side}{k}", base, tip, 0.009); c.data.materials.append(claw); K.apply_all(c)
            skin_to(c, arm, lambda p, side=side: {f"mixamorig:{side}Hand": 1.0}); parts.append(c)
    # restos de calca de mineiro (copia do quadril/coxa, rasgada) e capacete amassado
    pano = K.m_plain("calca_mineiro", "#4a4236", 0.95, bumpscale=1.2)
    calca = body.copy(); calca.data = body.data.copy(); bpy.context.collection.objects.link(calca)
    hz = P["Hips"].z; rnd = random.Random(3)
    keep_faces(calca, lambda f, doms: all(d in ("mixamorig:Hips", "mixamorig:LeftUpLeg", "mixamorig:RightUpLeg", "mixamorig:Spine") for d in doms)
               and (body.matrix_world @ f.calc_center_median()).z > hz - 0.33 - 0.06 * math.sin(f.index * 1.7) and rnd.random() > 0.06)
    offset_normal(calca, 0.012 + 0.012)
    calca.data.materials.clear(); calca.data.materials.append(pano); parts.append(calca)
    iron = K.m_metal("capacete_ferrugem", "#5a4a3a", 0.6, wear="#9a8a70", dirt="#1a120a", scratch=0.4, scale=4)
    hc = P["Head"] + Vector((0, 0.0, 0.17))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.12, location=hc, segments=24, ring_count=12); cap = bpy.context.object
    bm = bmesh.new(); bm.from_mesh(cap.data); bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.01], context='VERTS'); bm.to_mesh(cap.data); bm.free()
    cap.scale = (1.0, 1.15, 0.85); cap.rotation_euler = (math.radians(-12), math.radians(14), 0); cap.data.materials.append(iron)
    K.mod(cap, 'SOLIDIFY', thickness=0.008); K.displace(cap, 0.008, 0.05, 'CLOUDS'); K.apply_all(cap)
    aba = K.lathe("aba", [(0.12, -0.005), (0.16, 0.0), (0.16, 0.008), (0.12, 0.008)], 24, iron, close_top=False, close_bot=False); aba.location = hc; aba.rotation_euler = cap.rotation_euler; K.apply_all(aba)
    lamp = K.m_emit("lampada_apagada", "#3a2a10", 0.2)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.025, depth=0.04, location=hc + Vector((0, -0.12, 0.05)), rotation=(math.radians(80), 0, 0)); lp = bpy.context.object; lp.data.materials.append(lamp)
    for o in (cap, aba, lp):
        bpy.context.view_layer.update(); mw = o.matrix_world.copy(); o.data.transform(mw); o.matrix_world = Matrix()
        skin_to(o, arm, lambda p: {"mixamorig:Head": 1.0}); parts.append(o)
    # assa e junta
    opac = [o for o in parts if o.data.materials and not K.is_special(o.data.materials[0])]
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in opac:
        bpy.context.view_layer.update(); mw = o.matrix_world.copy(); o.parent = None; o.matrix_parent_inverse = Matrix(); o.matrix_world = Matrix()
        o.data.transform(mw)
    for o in opac: o.select_set(True)
    bpy.context.view_layer.objects.active = opac[0]; bpy.ops.object.join(); extra = bpy.context.view_layer.objects.active; extra.name = "mo_carnical_cal_partes"
    extra.parent = arm; extra.matrix_parent_inverse = arm.matrix_world.inverted()
    while len([m for m in extra.modifiers if m.type == 'ARMATURE']) > 1: extra.modifiers.remove([m for m in extra.modifiers if m.type == 'ARMATURE'][-1])
    K._bake(body, 1024); K._bake(extra, 1024)
    resto = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o not in (body, extra)]
    exportar("mo_carnical_cal", arm, [body, extra] + resto, dict(nome="Carniçal Cáustico", altura_m=1.75, corpo="manequim_masc afinado",
             notas="pele de gesso rachada, crânio, garras de 13 cm, calça rasgada e capacete de mineiro amassado"))

def fantasma():
    arm, body, P, T = importar()
    keep_faces(body, lambda f, doms: not any(d in HEAD for d in doms))
    hz = P["Hips"].z
    # da cintura para baixo afina e some (pernas viram rastro de poeira)
    for v in body.data.vertices:
        w = body.matrix_world @ v.co
        if w.z < hz:
            t = (hz - w.z) / hz
            v.co += (body.matrix_world.inverted().to_3x3() @ Vector((-w.x * 0.35 * t, -w.y * 0.35 * t, 0)))
    offset_normal(body, 0.01)
    K.displace(body, 2.5, 6.0, 'CLOUDS', 3); bpy.context.view_layer.objects.active = body; bpy.ops.object.modifier_move_to_index(modifier=body.modifiers[-1].name, index=0); bpy.ops.object.modifier_apply(modifier=body.modifiers[0].name)
    po = M_poeira("poeira_cal"); body.data.materials.clear(); body.data.materials.append(po)
    for p in body.data.polygons: p.use_smooth = True
    # cabeca lisa de poeira com dois olhos vazios
    h = P["Head"] + Vector((0, -0.01, 0.1))
    hd = org.fuse("cabeca_poeira", [org.blob("c", h, (0.09, 0.105, 0.115)), org.blob("q", h + Vector((0, -0.04, -0.07)), (0.06, 0.05, 0.05)),
                                     org.limb("pesc", [(P["Neck"], 0.05), (h + Vector((0, 0, -0.08)), 0.045)])], voxel=0.005)
    org.displace(hd, 0.006, 0.03, 'CLOUDS'); hd.data.materials.append(po); K.apply_all(hd); skin_to(hd, arm, lambda p: {"mixamorig:Head": 1.0})
    olho = K.m_plain("olho_vazio", "#000000", 1.0)
    eyes = []
    for s in (-1, 1):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.026, location=h + Vector((s * 0.035, -0.095, 0.01)), segments=16, ring_count=8); e = bpy.context.object
        e.scale = (0.9, 0.5, 1.2); e.data.materials.append(olho); bpy.context.view_layer.update(); mw = e.matrix_world.copy(); e.data.transform(mw); e.matrix_world = Matrix()
        skin_to(e, arm, lambda p: {"mixamorig:Head": 1.0}); eyes.append(e)
    # fiapos de poeira soltos (rastro) presos ao quadril e as maos
    fi = []
    rnd = random.Random(5)
    for k in range(0):
        a = rnd.uniform(0, 6.28); r = rnd.uniform(0.05, 0.16); z0 = hz - rnd.uniform(0.1, 0.5)
        pts = [(r * math.cos(a), 0.02 + r * math.sin(a), z0), (r * 1.4 * math.cos(a + 0.4), 0.06 + r * math.sin(a + 0.4), z0 - 0.18), (r * 1.8 * math.cos(a + 0.9), 0.12 + r * math.sin(a + 0.9), z0 - 0.35)]
        f_ = K.tube(f"fiapo{k}", pts, 0.02, po, taper=[1, 0.6, 0.1]); K.apply_all(f_); skin_to(f_, arm, lambda p: {"mixamorig:Hips": 1.0}); fi.append(f_)
    for o in [body, hd] + fi:
        mats = [K.simple_copy(m) for m in o.data.materials]; o.data.materials.clear()
        for m in mats:
            b = m.node_tree.nodes["Principled BSDF"]; b.inputs["Base Color"].default_value = (0.92, 0.91, 0.88, 1); b.inputs["Alpha"].default_value = 0.3; b.inputs["Roughness"].default_value = 1.0; b.inputs["Specular IOR Level"].default_value = 0.0
            b.inputs["Emission Color"].default_value = (0.85, 0.87, 0.92, 1); b.inputs["Emission Strength"].default_value = 0.35
            try: m.surface_render_method = 'BLENDED'
            except Exception: pass
            o.data.materials.append(m)
    exportar("mo_fantasma_cal", arm, [body, hd] + eyes + fi, dict(nome="Fantasma de Calcário", altura_m=1.75, corpo="manequim_masc em poeira",
             notas="material translúcido (alpha 0,42) + emissão leve; olhos pretos opacos. No jogo: GPUParticles3D de poeira branca saindo do corpo e 'esfarelar' ao levar golpe"))

if __name__ == "__main__":
    FEITOS = dict(mo_carnical_cal=carnical, mo_fantasma_cal=fantasma)
    for vid, fn in FEITOS.items():
        if SEL and vid not in SEL: continue
        fn()

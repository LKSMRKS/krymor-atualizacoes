"""Goblins v2: corpo do personagem principal (manequim_masc, mesmo esqueleto/animacoes) + cabeca e armas do goblin v1
+ roupa refeita encaixada no corpo novo. Uso: python3 build_goblin2.py <saida_dir> [ids...]
Fonte: calL2/humanoides/<id>.glb (rig com Idle/Walking/Running/Sentado/Mexendo/Conversando)."""
import sys, os, math, random, json
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh
from mathutils import Vector, Matrix
import itemkit as K

OUT = sys.argv[1]; SEL = sys.argv[2:]
SRC = "/home/claude/cinzas/calL2/humanoides/"
MAN = "/mnt/user-data/uploads/CinzasDaMare - Copia/Client/Assets/Models/manequim_masc.glb"
HEAD = {"mixamorig:Head", "mixamorig:HeadTop_End", "headfront"}
TORSO = {"mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2"}

V = {
 "mo_goblin_saqueador": dict(skin=("#6a8a34", "#2f4818"), tunic="#6a4a2a", rag="#4a3220", belt="#3a2414", bulk=0.0, kit=["saco"]),
 "mo_goblin_arqueiro": dict(skin=("#5e8030", "#284414"), tunic="#4e3620", rag="#3a2616", belt="#2a1a10", bulk=0.0, kit=["capa", "bracadeiras"], cape="#6e2a1c"),
 "mo_goblin_chefe": dict(skin=("#557629", "#223a10"), tunic="#6a2018", rag="#3a120e", belt="#2a1a10", bulk=0.03, kit=["ombreiras", "pele", "bracadeiras_ferro"]),
}

def dominant(v, dl, names):
    if not v[dl]: return None
    gi, _ = max(v[dl].items(), key=lambda kv: kv[1]); return names.get(gi)

def keep_faces(o, pred):
    """apaga faces que nao passam em pred(face, nomes_dominantes_dos_vertices, material)"""
    names = {g.index: g.name for g in o.vertex_groups}
    bm = bmesh.new(); bm.from_mesh(o.data); dl = bm.verts.layers.deform.active
    dead = [f for f in bm.faces if not pred(f, [dominant(v, dl, names) for v in f.verts], o.data.materials[f.material_index].name if o.data.materials else "")]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]; bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(o.data); bm.free()

def skin_to(o, arm, weights_fn):
    """parenta ao rig mantendo o mundo, cria grupos e pesos: weights_fn(co_mundo) -> {osso: peso}"""
    mw = o.matrix_world.copy(); o.parent = arm; o.matrix_world = mw
    groups = {}
    for v in o.data.vertices:
        for b, w in weights_fn(mw @ v.co).items():
            if b not in groups: groups[b] = o.vertex_groups.get(b) or o.vertex_groups.new(name=b)
            groups[b].add([v.index], w, 'REPLACE')
    m = o.modifiers.new("arm", 'ARMATURE'); m.object = arm
    return o

def cloth_mat(name, col, scale=1.0):
    b = K.NB(name)
    weave = b.math('MULTIPLY', b.wave(420 * scale, 0, 0, 'BANDS', 'X'), b.wave(420 * scale, 0, 0, 'BANDS', 'Z'))
    n1 = b.noise(9 * scale, 6, 0.6)
    c = b.ramp(n1, [(0.25, K.b_l(col, 0.55)), (0.7, col), (0.95, K.b_l(col, 1.2))])
    c = b.mix(b.math('MULTIPLY', K.edge_mask(b, 0.5, 0.55), 0.6, clamp=True), c, K.b_l(col, 1.6))
    c = b.mix(b.math('MULTIPLY', K.cavity(b, 0.05), 1.5, clamp=True), c, "#0d0805")
    dirt = b.math('GREATER_THAN', b.noise(4 * scale, 5, 0.7), 0.62); c = b.mix(b.math('MULTIPLY', dirt, 0.55), c, "#2b2116")
    return b.set(base=c, rough=0.85, sheen=0.4, normal=b.bump(b.math('ADD', weave, b.math('MULTIPLY', n1, 0.5)), 0.25, 0.002))

def skin_mat(name, light, dark):
    b = K.NB(name)
    n1 = b.noise(14, 6, 0.6); warts = b.voronoi(90, 'F1')
    c = b.ramp(n1, [(0.3, dark), (0.7, light)])
    c = b.mix(b.math('MULTIPLY', K.cavity(b, 0.04), 1.6, clamp=True), c, K.b_l(dark, 0.4))
    c = b.mix(b.math('MULTIPLY', K.edge_mask(b, 0.5, 0.54), 0.5, clamp=True), c, K.b_l(light, 1.3))
    h = b.math('ADD', b.math('MULTIPLY', b.math('LESS_THAN', warts, 0.12), 0.6), b.math('MULTIPLY', b.noise(120, 3), 0.3))
    return b.set(base=c, rough=0.55, sss=0.12, sss_rad=(0.4, 0.6, 0.2), normal=b.bump(h, 0.35, 0.002))

def build(vid, v):
    K.reset()
    bpy.ops.import_scene.gltf(filepath=SRC + vid + ".glb")
    garm = next(o for o in bpy.data.objects if o.type == 'ARMATURE'); G = next(o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(vid))
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and o.name.startswith("Icosphere"): bpy.data.objects.remove(o)
    acts = set(bpy.data.actions)
    before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=MAN)
    new = [o for o in bpy.data.objects if o not in before]
    body = next(o for o in new if o.type == 'MESH' and o.name.startswith("char1")); marm = next(o for o in new if o.type == 'ARMATURE')
    mpi, mb = body.matrix_parent_inverse.copy(), body.matrix_basis.copy()
    body.parent = garm; body.matrix_parent_inverse = mpi; body.matrix_basis = mb
    for m in body.modifiers:
        if m.type == 'ARMATURE': m.object = garm
    for o in new:
        if o is not body: bpy.data.objects.remove(o)
    for a in list(bpy.data.actions):
        if a not in acts: bpy.data.actions.remove(a)
    garm.data.pose_position = 'REST'; bpy.context.view_layer.update()
    P = {b.name.replace("mixamorig:", ""): garm.matrix_world @ b.head_local for b in garm.data.bones}
    s = (P["Head"].z) / 1.47           # escala do goblin em relacao ao manequim (Head do manequim ~1,47 m)
    # ---- goblin v1: so a cabeca (com capuz/elmo/bandana, olhos, dentes) + armas rigidas
    keep_faces(G, lambda f, doms, mat: ("_pbr" not in mat) or all(d in HEAD for d in doms))
    # ---- corpo do manequim: sem a cabeca-ovo, pele de goblin, mais forte se for o chefe
    keep_faces(body, lambda f, doms, mat: not any(d in HEAD for d in doms))
    ls = 1.0 / body.matrix_world.to_scale().x
    if v["bulk"]:
        d = body.modifiers.new("bulk", 'DISPLACE'); d.direction = 'NORMAL'; d.mid_level = 0.0; d.strength = v["bulk"] * ls
        vg = body.vertex_groups.new(name="_bulk")
        names = {g.index: g.name for g in body.vertex_groups}
        for vv in body.data.vertices:
            w = 0.0
            for g in vv.groups:
                n = names[g.group]
                if any(k in n for k in ("Spine", "Arm", "ForeArm", "Shoulder", "UpLeg", "Neck")): w += g.weight
                elif any(k in n for k in ("Leg", "Hips")): w += 0.6 * g.weight
            vg.add([vv.index], min(1.0, w), 'REPLACE')
        d.vertex_group = "_bulk"
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.modifier_move_to_index(modifier="bulk", index=0); bpy.ops.object.modifier_apply(modifier="bulk")
    body.data.materials.clear(); body.data.materials.append(skin_mat(vid + "_pele", *v["skin"]))
    for p in body.data.polygons: p.use_smooth = True
    # medidas do quadril no mundo (pose T)
    def section(z, dz=0.03):
        pts = [body.matrix_world @ vv.co for vv in body.data.vertices]
        sl = [p for p in pts if abs(p.z - z) < dz and abs(p.x) < 0.25 * s / 1.0 + 0.1]
        return (max(abs(p.x) for p in sl), max(p.y for p in sl), min(p.y for p in sl)) if sl else (0.15, 0.1, -0.1)
    hz = P["Hips"].z; rx, ry1, ry0 = section(hz); cy = (ry1 + ry0) / 2; ryy = (ry1 - ry0) / 2
    parts = []
    # ---- tunica: copia do tronco do proprio corpo, afastada da pele (deforma igual ao corpo)
    tun = body.copy(); tun.data = body.data.copy(); bpy.context.collection.objects.link(tun); tun.name = vid + "_tunica"
    keep_faces(tun, lambda f, doms, mat: all(d in TORSO or (d and "UpLeg" in d and (body.matrix_world @ f.calc_center_median()).z > hz - 0.12 * s) for d in doms))
    m = tun.modifiers.new("off", 'DISPLACE'); m.direction = 'NORMAL'; m.mid_level = 0.0; m.strength = (0.012 + v["bulk"] * 0.3) * ls
    bpy.context.view_layer.objects.active = tun; bpy.ops.object.modifier_move_to_index(modifier="off", index=0); bpy.ops.object.modifier_apply(modifier="off")
    sm = tun.modifiers.new("sol", 'SOLIDIFY'); sm.thickness = 0.006 * ls; sm.offset = 1
    bpy.ops.object.modifier_move_to_index(modifier="sol", index=0); bpy.ops.object.modifier_apply(modifier="sol")
    tun.data.materials.clear(); tun.data.materials.append(cloth_mat(vid + "_tecido", v["tunic"])); parts.append(tun)
    # ---- tiras de pano penduradas no cinto (presas ao quadril e a coxa do lado)
    rnd = random.Random(len(vid)); rag = cloth_mat(vid + "_trapo", v["rag"])
    for i in range(10):
        a = i / 10 * math.tau + rnd.uniform(-0.15, 0.15)
        x0, y0 = (rx + 0.02 * s) * math.cos(a), cy + (ryy + 0.02 * s) * math.sin(a)
        L = rnd.uniform(0.16, 0.26) * s; w = rnd.uniform(0.05, 0.08) * s
        bm = bmesh.new(); tx, ty = -math.sin(a), math.cos(a) * (ryy / max(rx, 1e-3))
        tl = Vector((tx, ty, 0)).normalized()
        rows = []
        for j in range(5):
            t = j / 4; c0 = Vector((x0 * (1 + 0.15 * t), y0 + (y0 - cy) * 0.15 * t, hz - 0.01 * s - L * t))
            ww = w * (1 - 0.35 * t)
            rows.append((bm.verts.new(c0 - tl * ww / 2 + Vector((0, 0, rnd.uniform(-0.01, 0.01) * s * (j == 4)))), bm.verts.new(c0 + tl * ww / 2)))
        for j in range(4): bm.faces.new((rows[j][0], rows[j][1], rows[j + 1][1], rows[j + 1][0]))
        o = K.obj_from_bm(bm, f"{vid}_tira{i}", rag); K.mod(o, 'SOLIDIFY', thickness=0.006 * s); K.apply_all(o)
        side = "Left" if x0 > 0 else "Right"; fy = abs(math.cos(a))
        skin_to(o, garm, lambda p, side=side, fy=fy: {"mixamorig:Hips": 1 - 0.6 * fy * min(1, (hz - p.z) / (0.2 * s)), f"mixamorig:{side}UpLeg": 0.6 * fy * min(1, (hz - p.z) / (0.2 * s))})
        parts.append(o)
    # ---- cinto com fivela
    belt = K.m_leather(vid + "_cinto", v["belt"], "#120804", 3.0); iron = K.m_iron(vid + "_ferro", 3.0)
    o = K.tube(f"{vid}_cinto", [((rx + 0.016 * s) * math.cos(a), cy + (ryy + 0.016 * s) * math.sin(a), hz + 0.03 * s) for a in [j * math.tau / 40 for j in range(41)]], 0.018 * s, belt)
    o.scale.z = 1.6; K.apply_all(o); skin_to(o, garm, lambda p: {"mixamorig:Hips": 1.0}); parts.append(o)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, ry0 - 0.02 * s, hz + 0.03 * s)); o = bpy.context.object; o.scale = (0.06 * s, 0.015 * s, 0.05 * s); o.data.materials.append(iron)
    K.bevel(o, 0.004 * s, 2); K.apply_all(o); skin_to(o, garm, lambda p: {"mixamorig:Hips": 1.0}); parts.append(o)
    kit = v["kit"]
    def zw(p, chain=("Hips", "Spine", "Spine1", "Spine2")):   # peso pela altura entre ossos da coluna
        zs = [(n, P[n].z) for n in chain]
        for (a, za), (b, zb) in zip(zs, zs[1:]):
            if p.z <= zb:
                t = max(0, min(1, (p.z - za) / max(zb - za, 1e-4))); return {f"mixamorig:{a}": 1 - t, f"mixamorig:{b}": t}
        return {f"mixamorig:{chain[-1]}": 1.0}
    if "saco" in kit:
        sack = cloth_mat(vid + "_saco", "#a08a60")
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=4, radius=0.12 * s, location=(0.03 * s, P["Spine1"].y + 0.17 * s, P["Spine1"].z)); o = bpy.context.object
        o.scale = (1.0, 0.75, 1.25); o.data.materials.append(sack); K.displace(o, 0.03 * s, 0.06 * s, 'CLOUDS'); K.apply_all(o)
        for p_ in o.data.polygons: p_.use_smooth = True
        skin_to(o, garm, lambda p: {"mixamorig:Spine1": 0.5, "mixamorig:Spine2": 0.5}); parts.append(o)
        tie = K.tube(f"{vid}_boca", [(0.03 * s + 0.04 * s * math.cos(a), P["Spine1"].y + 0.17 * s + 0.03 * s * math.sin(a), P["Spine1"].z + 0.14 * s) for a in [j * math.tau / 16 for j in range(17)]], 0.012 * s, belt)
        skin_to(tie, garm, lambda p: {"mixamorig:Spine2": 1.0}); parts.append(tie)
        st = K.tube(f"{vid}_alca", [(0.12 * s, P["Spine2"].y + 0.09 * s, P["Spine2"].z + 0.08 * s), (0.09 * s, P["Spine2"].y - 0.11 * s, P["Spine2"].z + 0.04 * s),
                                    (-0.04 * s, P["Spine1"].y - 0.13 * s, P["Spine1"].z), (-0.13 * s, P["Spine"].y - 0.1 * s, P["Spine"].z - 0.02 * s)], 0.014 * s, belt)
        skin_to(st, garm, zw); parts.append(st)
    if "capa" in kit:
        cm = cloth_mat(vid + "_capa", v["cape"]); bm = bmesh.new(); rows = []
        top, bot = P["Spine2"].z + 0.1 * s, hz - 0.12 * s
        for j in range(9):
            t = j / 8; z = top + (bot - top) * t; half = (0.15 + 0.06 * t) * s
            row = []
            for i in range(9):
                u = i / 8 * 2 - 1; x = u * half
                yb = P["Spine1"].y + 0.09 * s + 0.05 * s * (1 - u * u) + 0.06 * s * t
                row.append(bm.verts.new((x, yb + (0.015 * s if j == 8 and i % 2 else 0), z)))
            rows.append(row)
        for j in range(8):
            for i in range(8): bm.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
        o = K.obj_from_bm(bm, f"{vid}_capa", cm); K.mod(o, 'SOLIDIFY', thickness=0.006 * s); K.apply_all(o)
        skin_to(o, garm, zw); parts.append(o)
    for kk in ("bracadeiras", "bracadeiras_ferro"):
        if kk in kit:
            mat = K.m_leather(vid + "_bracadeira", "#5a3420", "#1c0e06", 3.0) if kk == "bracadeiras" else iron
            for side in ("Left", "Right"):
                br = body.copy(); br.data = body.data.copy(); bpy.context.collection.objects.link(br)
                fa, ha = P[side + "ForeArm"], P[side + "Hand"]
                keep_faces(br, lambda f, doms, m_, side=side, fa=fa, ha=ha: all(d == f"mixamorig:{side}ForeArm" for d in doms) and
                           abs((body.matrix_world @ f.calc_center_median()).x - fa.x) > 0.35 * abs(ha.x - fa.x))
                md = br.modifiers.new("off", 'DISPLACE'); md.direction = 'NORMAL'; md.mid_level = 0; md.strength = 0.014 * ls
                bpy.context.view_layer.objects.active = br; bpy.ops.object.modifier_move_to_index(modifier="off", index=0); bpy.ops.object.modifier_apply(modifier="off")
                sm = br.modifiers.new("sol", 'SOLIDIFY'); sm.thickness = 0.008 * ls; bpy.ops.object.modifier_move_to_index(modifier="sol", index=0); bpy.ops.object.modifier_apply(modifier="sol")
                br.data.materials.clear(); br.data.materials.append(mat); br.name = f"{vid}_bracadeira_{side}"; parts.append(br)
    if "ombreiras" in kit:
        for side, sg in (("Left", 1), ("Right", -1)):
            c = P[side + "Arm"] + Vector((0, 0, 0.05 * s))
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.11 * s, segments=32, ring_count=16, location=c); o = bpy.context.object
            bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.delete(bm, geom=[vv for vv in bm.verts if vv.co.z < -0.02 * s], context='VERTS'); bm.to_mesh(o.data); bm.free()
            o.scale = (1.15, 1.0, 0.75); o.data.materials.append(iron); K.mod(o, 'SOLIDIFY', thickness=0.012 * s); K.bevel(o, 0.004 * s, 2); K.apply_all(o)
            for p_ in o.data.polygons: p_.use_smooth = True
            skin_to(o, garm, lambda p, side=side: {f"mixamorig:{side}Arm": 0.7, f"mixamorig:{side}Shoulder": 0.3}); parts.append(o)
            for j in range(3):
                sp = K.crystal(f"{vid}_cravo{side}{j}", iron, 0.022 * s, 0.07 * s, 0.9, 6, seed=j)
                sp.location = c + Vector((sg * (-0.04 + j * 0.045) * s, (-0.02 + j * 0.02) * s, 0.07 * s - abs(j - 1) * 0.015 * s))
                sp.rotation_euler = (0, sg * math.radians(15 + j * 12), 0); K.apply_all(sp)
                mw = sp.matrix_world.copy(); sp.data.transform(mw); sp.matrix_world = Matrix()
                skin_to(sp, garm, lambda p, side=side: {f"mixamorig:{side}Arm": 0.7, f"mixamorig:{side}Shoulder": 0.3}); parts.append(sp)
    if "pele" in kit:
        fur = K.m_plain(vid + "_pele_bicho", "#5a4630", 0.95, bumpscale=80)
        bpy.ops.mesh.primitive_torus_add(major_radius=0.13 * s, minor_radius=0.07 * s, major_segments=48, minor_segments=16, location=(0, P["Spine2"].y + 0.01 * s, P["Neck"].z - 0.03 * s))
        o = bpy.context.object; o.scale = (1.35, 1.05, 0.6); o.data.materials.append(fur); K.displace(o, 0.03 * s, 0.025 * s, 'CLOUDS', 2); K.apply_all(o)
        for p_ in o.data.polygons: p_.use_smooth = True
        skin_to(o, garm, lambda p: {"mixamorig:Spine2": 0.8, "mixamorig:Neck": 0.2}); parts.append(o)
    # ---- assa materiais procedurais (pose de descanso) e exporta com todos os clipes
    for o in [body] + parts:
        K._bake(o, 1024 if o is body else 512)
    garm.data.pose_position = 'POSE'
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in [garm, body, G] + parts: o.select_set(True)
    os.makedirs(f"{OUT}/{vid}", exist_ok=True)
    path = f"{OUT}/{vid}/{vid}.glb"
    bpy.ops.export_scene.gltf(filepath=path, use_selection=True, export_format='GLB', export_yup=True, export_apply=False, export_animations=True,
                              export_animation_mode='ACTIONS', export_skins=True, export_materials='EXPORT')
    bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/{vid}/{vid}.blend")
    tris = sum(len(p.vertices) - 2 for o in [body, G] + parts for p in o.data.polygons)
    info = dict(asset_id=vid, altura_m=round(P["HeadTop_End"].z if "HeadTop_End" in P else P["Head"].z + 0.2, 2), tris=tris, clips=[a.name for a in bpy.data.actions],
                corpo="manequim_masc (personagem principal)" + (" reforçado" if v["bulk"] else ""), kit=kit)
    json.dump(info, open(f"{OUT}/{vid}/{vid}_info.json", "w"), indent=1, ensure_ascii=False); print("OK", vid, info, flush=True)

for vid, v in V.items():
    if SEL and vid not in SEL: continue
    build(vid, v)

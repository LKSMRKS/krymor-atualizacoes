"""Kit para vestir/equipar monstros no corpo do manequim_masc (mesmo esqueleto e clipes).
Roupas a partir de copias do proprio corpo (seguem a pele), saia/capa/capuz com pesos misturados, arma do arsenal presa
na mao com a pegada certa (origem da arma = empunhadura, lamina para a frente com o braco abaixado)."""
import sys, math, random
sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, bmesh
from mathutils import Vector, Matrix
import itemkit as K, org
import build_mortos_cal as B

ARSENAL = "/home/claude/cinzas/arsenal/modelos/"

def fix(o):
    bpy.context.view_layer.update(); mw = o.matrix_world.copy(); o.parent = None; o.matrix_parent_inverse = Matrix(); o.matrix_world = Matrix()
    o.data.transform(mw); return o

def rigid(o, arm, bone, mat=None):
    if mat is not None and not o.data.materials: o.data.materials.append(mat)
    K.apply_all(o); fix(o); B.skin_to(o, arm, lambda p: {f"mixamorig:{bone}": 1.0}); return o

def pesos(o, arm, fn):
    """fn(p_mundo) -> {osso: peso}"""
    fix(o); B.skin_to(o, arm, lambda p: {("mixamorig:" + k if not k.startswith("mixamorig") else k): v for k, v in fn(p).items()}); return o

# ------------------------------------------------------------------ roupas a partir do corpo
def roupa(body, arm, ossos, mat, grossura=0.01, zmin=None, zmax=None, rasgo=0.0, seed=1, nome="roupa"):
    """pinta a roupa no PROPRIO corpo (slot de material nas faces dos ossos dados) e engorda essas faces um pouco.
    Sem malha extra: nada de z-fighting nem de malha picotada ao reduzir."""
    me = body.data
    if mat.name not in [m.name for m in me.materials]: me.materials.append(mat)
    mi = [m.name for m in me.materials].index(mat.name)
    rnd = random.Random(seed); bw = body.matrix_world; S = set("mixamorig:" + o for o in ossos)
    names = {g.index: g.name for g in body.vertex_groups}
    bm = bmesh.new(); bm.from_mesh(me); dl = bm.verts.layers.deform.active
    mexe = set()
    for f in bm.faces:
        doms = [B.dominant(v, dl, names) for v in f.verts]
        if not all(d in S for d in doms if d): continue
        z = (bw @ f.calc_center_median()).z
        if (zmin is not None and z < zmin) or (zmax is not None and z > zmax): continue
        if rasgo and rnd.random() < rasgo: continue
        f.material_index = mi; mexe.update(f.verts)
    ls = grossura / bw.to_scale().x
    bm.normal_update()
    for v in mexe: v.co += v.normal * ls
    bm.to_mesh(me); bm.free()
    return None

def saia(arm, P, mat, z_top, z_bot, r_top, r_bot, nome="saia", aberta_frente=False, seg=24):
    """saia/manto em cone do quadril para baixo; em cima segue o quadril, embaixo cada lado segue a coxa/perna"""
    prof = [(r_top, z_top), (r_bot, z_bot)]
    bm = bmesh.new(); rows = 10
    vs = []
    for i in range(rows + 1):
        t = i / rows; z = z_top + (z_bot - z_top) * t; r = r_top + (r_bot - r_top) * (t ** 0.8)
        row = []
        for j in range(seg):
            a = j / seg * math.tau
            if aberta_frente and abs(math.atan2(math.sin(a), math.cos(a)) + math.pi / 2) < 0.35 * t: row.append(None); continue
            row.append(bm.verts.new((math.cos(a) * r, P["Hips"].y + math.sin(a) * r * 0.85, z)))
        vs.append(row)
    for i in range(rows):
        for j in range(seg):
            q = [vs[i][j], vs[i][(j + 1) % seg], vs[i + 1][(j + 1) % seg], vs[i + 1][j]]
            if all(q): bm.faces.new(q)
    me = bpy.data.meshes.new(nome); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(nome, me); bpy.context.collection.objects.link(o)
    K.mod(o, 'SOLIDIFY', thickness=0.012); o.data.materials.append(mat); K.apply_all(o)
    for p in o.data.polygons: p.use_smooth = True
    zk = P["LeftLeg"].z
    def fn(p):
        t = max(0.0, min(1.0, (z_top - p.z) / max(0.01, z_top - zk)))
        lado = "Left" if p.x > 0 else "Right"; f = min(1.0, abs(p.x) / 0.12)
        perna = f"{lado}UpLeg" if p.z > zk else f"{lado}Leg"
        w_h = 1 - t * f
        return {"Hips": max(0.05, w_h), perna: max(0.0, 1 - w_h)}
    return pesos(o, arm, fn)

def capa(arm, P, mat, largura=0.42, comprimento=1.0, nome="capa", desfiada=True, seed=2):
    """capa presa nos ombros, caindo pelas costas"""
    top = P["Spine2"].z + 0.06; y0 = P["Spine2"].y + 0.13
    bm = bmesh.new(); nx, nz = 10, 14; rnd = random.Random(seed); rows = []
    for i in range(nz + 1):
        t = i / nz; z = top - t * comprimento; w = largura * (0.75 + 0.5 * t)
        row = []
        for j in range(nx + 1):
            u = j / nx - 0.5; x = u * 2 * w
            y = y0 + 0.08 * t + 0.06 * (1 - (2 * u) ** 2) * (0.4 + t)
            if desfiada and i == nz: z_ = z + rnd.uniform(0, 0.12)
            else: z_ = z
            row.append(bm.verts.new((x, y, z_)))
        rows.append(row)
    for i in range(nz):
        for j in range(nx): bm.faces.new([rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]])
    me = bpy.data.meshes.new(nome); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(nome, me); bpy.context.collection.objects.link(o)
    K.mod(o, 'SOLIDIFY', thickness=0.01); K.mod(o, 'SUBSURF', levels=1); o.data.materials.append(mat); K.apply_all(o)
    for p in o.data.polygons: p.use_smooth = True
    def fn(p):
        t = max(0.0, min(1.0, (top - p.z) / comprimento))
        if t < 0.25: return {"Spine2": 1.0}
        if t < 0.55: return {"Spine2": 1 - (t - 0.25) / 0.3, "Spine": (t - 0.25) / 0.3}
        return {"Spine": max(0.0, 1 - (t - 0.55) / 0.3), "Hips": min(1.0, (t - 0.55) / 0.3)}
    return pesos(o, arm, fn)

def capuz(arm, P, mat, nome="capuz", ponta=0.0):
    h = P["Head"] + Vector((0, 0.0, 0.1))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.135, location=h, segments=24, ring_count=14); o = bpy.context.object; o.name = nome
    bm = bmesh.new(); bm.from_mesh(o.data)
    dead = [v for v in bm.verts if (v.co.y < -0.06 and v.co.z < 0.07 and abs(v.co.x) < 0.1) or v.co.z < -0.11]
    bmesh.ops.delete(bm, geom=dead, context='VERTS')
    for v in bm.verts:
        if v.co.z < -0.05: v.co.x *= 1.25; v.co.y = v.co.y * 1.2 + 0.02     # cai sobre os ombros
        if ponta and v.co.z > 0.1 and v.co.y > 0.0: v.co.y += ponta * (v.co.z - 0.1) * 6; v.co.z += ponta * 0.5
    bm.to_mesh(o.data); bm.free()
    o.data.transform(Matrix.Diagonal((1, 1.05, 1.08, 1)))
    K.mod(o, 'SOLIDIFY', thickness=0.012); K.displace(o, 0.006, 0.05, 'CLOUDS'); o.data.materials.append(mat); K.apply_all(o)
    for p in o.data.polygons: p.use_smooth = True
    def fn(p): return {"Head": 1.0} if p.z > P["Neck"].z + 0.06 else {"Head": 0.5, "Neck": 0.3, "Spine2": 0.2}
    return pesos(o, arm, fn)

def olhos(arm, P, cor, forca=6.0, sep=0.035, fwd=-0.095, up=0.11, r=0.014):
    em = K.m_emit("olhos_" + cor, cor, forca); out = []
    for s in (-1, 1):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=P["Head"] + Vector((s * sep, fwd, up)), segments=12, ring_count=6)
        out.append(rigid(bpy.context.object, arm, "Head", em))
    return out

def rosto(arm, P, pele, nariz=0.0, orelhas=0.0, barba=None, sobrancelha=None, olho_cor="#0a0806"):
    """traços na cabeça-ovo do manequim: nariz, orelhas, barba (material), olhos escuros"""
    h = P["Head"] + Vector((0, 0, 0.1)); out = []
    if nariz:
        n = org.fuse("nariz", [org.blob("n", h + Vector((0, -0.11, -0.01)), (0.022 * nariz, 0.03 * nariz, 0.03 * nariz)),
                               org.blob("p", h + Vector((0, -0.125, -0.03)), (0.026 * nariz, 0.024 * nariz, 0.02 * nariz))], voxel=0.003)
        out.append(rigid(n, arm, "Head", pele))
    if orelhas:
        for s in (-1, 1):
            out.append(rigid(org.spike(f"orelha{s}", h + Vector((s * 0.085, 0.0, 0.0)), h + Vector((s * (0.085 + 0.09 * orelhas), 0.04, 0.05 * orelhas)), 0.03), arm, "Head", pele))
    if barba is not None:
        bb = org.fuse("barba", [org.blob("b", h + Vector((0, -0.085, -0.09)), (0.08, 0.05, 0.08)), org.blob("b2", h + Vector((0, -0.09, -0.17)), (0.06, 0.04, 0.07)),
                                org.blob("bigode", h + Vector((0, -0.115, -0.045)), (0.06, 0.02, 0.015))], voxel=0.004)
        org.displace(bb, 0.008, 0.015, 'CLOUDS'); out.append(rigid(bb, arm, "Head", barba))
    if sobrancelha is not None:
        for s in (-1, 1): out.append(rigid(org.blob(f"sob{s}", h + Vector((s * 0.033, -0.1, 0.035)), (0.025, 0.012, 0.009)), arm, "Head", sobrancelha))
    if olho_cor:
        ol = K.m_plain("olho_escuro", olho_cor, 0.3)
        for s in (-1, 1):
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.011, location=h + Vector((s * 0.033, -0.102, 0.012)), segments=10, ring_count=6)
            out.append(rigid(bpy.context.object, arm, "Head", ol))
    return out

def arma(arm, P, glb, lado="Right", escala=1.0, gira=0.0):
    """importa a arma do arsenal (origem = pegada, lamina +Z) e prende na mao: no T a lamina aponta para a frente (-Y)"""
    before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=ARSENAL + glb + ".glb")
    novos = [o for o in bpy.data.objects if o not in before]
    ms = [o for o in novos if o.type == 'MESH']
    bpy.context.view_layer.update()
    for o in ms: o.data.transform(o.matrix_world); o.parent = None; o.matrix_world = Matrix()
    for o in novos:
        if o.type != 'MESH': bpy.data.objects.remove(o)
    d = (P[lado + "Hand"] - P[lado + "ForeArm"]).normalized()
    c = P[lado + "Hand"] + d * 0.075 + Vector((0, 0, -0.012))
    M = Matrix.Translation(c) @ Matrix.Rotation(math.radians(90), 4, 'X') @ Matrix.Rotation(math.radians(gira), 4, 'Z') @ Matrix.Scale(escala, 4)
    out = []
    for o in ms:
        o.data.transform(M)
        fix(o); B.skin_to(o, arm, lambda p: {f"mixamorig:{lado}Hand": 1.0}); out.append(o)
    return out

def finalizar(vid, arm, body, parts, info, tex=1024, emit=False, extra_mats_ok=True):
    """junta peças opacas (texturas assadas), mantém armas importadas (já têm PBR) e especiais, exporta"""
    parts = [o for o in parts if o is not None]
    especiais = [o for o in parts if o.get("pronta")]
    opac = [o for o in parts if o not in especiais and o.data.materials and (emit or not K.is_special(o.data.materials[0]))]
    for x in bpy.context.view_layer.objects: x.select_set(False)
    for o in opac: fix(o)
    for o in opac: o.select_set(True)
    bpy.context.view_layer.objects.active = opac[0]; bpy.ops.object.join(); ex = bpy.context.view_layer.objects.active; ex.name = vid + "_partes"
    for m_ in list(ex.modifiers): ex.modifiers.remove(m_)
    K._decimate(ex, 16000)
    ex.parent = arm; ex.matrix_parent_inverse = arm.matrix_world.inverted()
    md = ex.modifiers.new("arm", 'ARMATURE'); md.object = arm
    K._bake(body, tex, emit); K._bake(ex, tex, emit)
    resto = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o not in (body, ex)]
    for o in resto:
        if not any(m_.type == 'ARMATURE' for m_ in o.modifiers):
            if o.parent is None: fix(o); B.skin_to(o, arm, lambda p: {"mixamorig:Head": 1.0})
            else: m_ = o.modifiers.new("arm", 'ARMATURE'); m_.object = arm
    B.exportar(vid, arm, [body, ex] + resto, info)

def marcar_pronta(objs):
    for o in objs: o["pronta"] = True
    return objs

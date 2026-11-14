"""Armadura em 5 partes (11/11, pedido: "igual MU"). Tira a calça (coxas) da malha "peitoral" dos conjuntos arm_*.glb:
as faces cujo osso dominante é a coxa (UpLeg) viram a malha "calca". Não mexe no desenho nem nos pesos.
Uso: python3.11 Tools/arte/separar_calca.py <entrada.glb> <saida.glb>"""
import sys, bpy, bmesh
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
src, dst = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
chest = next(o for o in bpy.data.objects if o.type == 'MESH' and o.name.split('.')[0] == "peitoral")
groups = {g.index: g.name for g in chest.vertex_groups}
def dominant(v):
    best = max(v.groups, key=lambda g: g.weight, default=None)
    return groups.get(best.group, "") if best else ""
# perna = coxa ou canela/joelho (o que sobrou de perna no peitoral: joelheiras etc.)
legv = {v.index for v in chest.data.vertices if "Leg" in dominant(v)}
bpy.context.view_layer.objects.active = chest
for o in bpy.data.objects: o.select_set(o is chest)
bpy.ops.object.mode_set(mode='EDIT')
bm = bmesh.from_edit_mesh(chest.data)
bm.faces.ensure_lookup_table()
# 11/11 (refino do corte, pedido do usuário): decide por PEDAÇO inteiro (ilha de faces ligadas) — placa de metal, saia,
# joelheira vão inteiras para onde a maior parte delas está; só a ilha gigante (o corpo todo de uma vez) é cortada face a face
seen, islands = set(), []
for f in bm.faces:
    if f.index in seen: continue
    stack, isl = [f], []
    seen.add(f.index)
    while stack:
        g = stack.pop(); isl.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen: seen.add(h.index); stack.append(h)
    islands.append(isl)
total = len(bm.faces)
# limite relativo à altura do modelo (alguns sets vêm em outra escala)
ZTOP = max(v.co.z for v in bm.verts)
ZLOW = 0.65 * ZTOP / 1.75
for isl in islands:
    verts = {v.index for f in isl for v in f.verts}
    frac = sum(1 for v in verts if v in legv) / max(1, len(verts))
    if len(isl) < total * 0.35:
        for f in isl: f.select = frac > 0.5
    else:
        for f in isl: f.select = sum(1 for v in f.verts if v.index in legv) * 2 > len(f.verts)
print("ilhas:", len(islands))
bmesh.update_edit_mesh(chest.data)
n = sum(1 for f in bm.faces if f.select)
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
def mesh(name): return next((o for o in bpy.data.objects if o.type == 'MESH' and o.name.split('.')[0] == name and o is not chest), None)
new = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("peitoral") and o is not chest]
if new: new[0].name = "calca"; new[0].data.name = "calca"
calca, boots = mesh("calca"), mesh("botas")
# 11/11 refino: pedacinhos soltos que ficaram no peitoral ABAIXO da cintura vão para a peça certa, medindo pelas
# próprias peças (vale para qualquer escala): abaixo do topo da bota (joelho) -> botas; entre joelho e cintura -> calça
def zs(o): return [(o.matrix_world @ v.co).z for v in o.data.vertices]
knee = max(zs(boots)) if boots else None
waist = (max(zs(calca)) * 0.6 + min(zs(calca)) * 0.4) if calca else None
def move(target, test):
    if target is None: return 0
    bpy.context.view_layer.objects.active = chest
    for o in bpy.data.objects: o.select_set(o is chest)
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(chest.data); bm.faces.ensure_lookup_table()
    for f in bm.faces: f.select = False
    seen, cnt, total = set(), 0, len(bm.faces)
    mw = chest.matrix_world
    for f in bm.faces:
        if f.index in seen: continue
        stack, isl = [f], []; seen.add(f.index)
        while stack:
            g = stack.pop(); isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen: seen.add(h.index); stack.append(h)
        vs = {v for g in isl for v in g.verts}
        cz = sum((mw @ v.co).z for v in vs) / len(vs)
        if len(isl) < total * 0.05 and test(cz):
            for g in isl: g.select = True
            cnt += 1
    bmesh.update_edit_mesh(chest.data)
    if cnt: bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')
    extra = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("peitoral") and o is not chest]
    if extra:
        for o in bpy.data.objects: o.select_set(o in (target, extra[0]))
        bpy.context.view_layer.objects.active = target
        bpy.ops.object.join()
    return cnt
if knee is not None: print("pedaços baixos para as botas:", move(boots, lambda z: z < knee * 0.97))
if waist is not None: print("pedaços da cintura para a calça:", move(calca, lambda z: z < waist))
print("faces na calça:", n)
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_skins=True, export_yup=True, export_animations=False)

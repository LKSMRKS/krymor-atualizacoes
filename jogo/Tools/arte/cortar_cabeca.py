"""Recorta a cabeca de um personagem inteiro (Meshy, em T) e salva como cabeca para o socket Head do manequim.
Uso: python3 cortar_cabeca.py entrada.glb saida.glb [tris_alvo] [ajuste_corte_m]
- escala: altura total do personagem -> 1,674 m (mesma do manequim_masc), entao a cabeca fica na proporcao certa do corpo
- corte: no ponto mais fino do pescoco (varredura de largura de cima para baixo)
- origem: centro do pescoco no corte + 0,02 m (onde fica o osso Head do mixamo); frente = -Y do Blender = +Z do Godot"""
import sys, bpy, bmesh, numpy as np, math
from mathutils import Vector
src, dst = sys.argv[1], sys.argv[2]; TRIS = int(sys.argv[3]) if len(sys.argv) > 3 else 9000; ADJ = float(sys.argv[4]) if len(sys.argv) > 4 else 0.0
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
o = next(x for x in bpy.data.objects if x.type == 'MESH')
o.parent = None; bpy.context.view_layer.objects.active = o; o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
V = np.array([v.co[:] for v in o.data.vertices]); zmin, zmax = V[:, 2].min(), V[:, 2].max()
k = 1.674 / (zmax - zmin); o.scale = (k, k, k); bpy.ops.object.transform_apply(scale=True)
V = np.array([v.co[:] for v in o.data.vertices]); V[:, 2] -= V[:, 2].min()
for v, p in zip(o.data.vertices, V): v.co = p
H = V[:, 2].max()
# largura do corte (so a parte central, para ignorar bracos em T) de 4 em 4 mm
best = None
for z in np.arange(H * 0.93, H * 0.72, -0.004):
    s = V[(np.abs(V[:, 2] - z) < 0.003) & (np.abs(V[:, 0]) < 0.2)]
    if len(s) < 8: continue
    w = (s[:, 0].max() - s[:, 0].min()) + 0.5 * (s[:, 1].max() - s[:, 1].min())
    if best is None or w < best[1]: best = (z, w, s[:, 1].mean())
zc = best[0] + ADJ; yc = best[2]
print("corte em z=%.3f (altura %.3f), largura %.3f" % (zc, H, best[1]))
bm = bmesh.new(); bm.from_mesh(o.data)
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, zc), plane_no=(0, 0, 1), clear_inner=True)
# fecha o buraco do pescoco
edges = [e for e in r["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
try: bmesh.ops.holes_fill(bm, edges=edges, sides=0)
except Exception as ex: print("tampa:", ex)
# so a ilha principal (descarta pontas soltas de bracos/juba cortadas)
bm.verts.ensure_lookup_table(); seen = set(); isl = []
for v in bm.verts:
    if v.index in seen: continue
    st = [v]; cur = []; seen.add(v.index)
    while st:
        x = st.pop(); cur.append(x)
        for e in x.link_edges:
            w_ = e.other_vert(x)
            if w_.index not in seen: seen.add(w_.index); st.append(w_)
    isl.append(cur)
isl.sort(key=len, reverse=True)
far = []
for c in isl[1:]:
    cen = sum((v.co for v in c), Vector()) / len(c)
    if abs(cen.x) > 0.22: far += c
bmesh.ops.delete(bm, geom=list(set(far)), context='VERTS')
bm.to_mesh(o.data); bm.free()
for v in o.data.vertices: v.co -= Vector((0, yc, zc + 0.02))
tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
if tris > TRIS:
    m = o.modifiers.new("dec", 'DECIMATE'); m.ratio = TRIS / tris; bpy.ops.object.modifier_apply(modifier="dec")
# texturas 4096 -> 1024 para o jogo
for mt in o.data.materials:
    for n in mt.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and n.image.size[0] > 1024: n.image.scale(1024, 1024); n.image.pack()
mt = o.data.materials[0]; mt.name = o.name = dst.split("/")[-1][:-4]
V = np.array([v.co[:] for v in o.data.vertices])
print("tris", sum(len(p.vertices) - 2 for p in o.data.polygons), "caixa", V.min(0).round(3), V.max(0).round(3))
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_yup=True, use_selection=False, export_image_format='JPEG' if False else 'AUTO')

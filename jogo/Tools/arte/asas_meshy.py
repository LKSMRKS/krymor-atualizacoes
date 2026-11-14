"""Asas do usuário (11/11): forma do Meshy (sem cor) + imagem de referência -> asa pintada, em duas metades que batem.
Uso: python3.11 Tools/arte/asas_meshy.py <forma.glb> <referencia.png> <saida.glb> [altura_m]

- reduz para ~24 mil triângulos;
- pinta projetando a imagem de frente (a silhueta da imagem casa com a da forma: mesmo enquadramento);
- vira o lado pintado para TRÁS (quem vê as costas do herói vê a pintura);
- separa em "asa_esq" e "asa_dir", cada uma com a origem na dobradiça (a ponta de dentro, na altura do ombro),
  para o jogo girar e bater (MannequinVisual.SetWings)."""
import sys, math, bpy, bmesh
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
src, ref, dst = argv[0], argv[1], argv[2]
height = float(argv[3]) if len(argv) > 3 else 1.6

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob = next(o for o in bpy.data.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = ob
for o in bpy.data.objects: o.select_set(o is ob)
ob.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
faces = len(ob.data.polygons)
if faces > 24000:
    mod = ob.modifiers.new("dec", 'DECIMATE'); mod.ratio = 24000 / faces
    bpy.ops.object.modifier_apply(modifier=mod.name)

# Blender: X = lado, Z = alto, -Y = frente (a pintura). UV pela frente, casando a caixa da forma com a da imagem.
img = bpy.data.images.load(ref)
w, h = img.size
px = list(img.pixels)
xs, ys = [], []
for y in range(h):
    row = y * w * 4
    for x in range(0, w, 2):
        if px[row + x * 4 + 3] > 0.08: xs.append(x); ys.append(y)
x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)   # y da imagem no Blender: 0 = embaixo
vs = [v.co for v in ob.data.vertices]
mnx, mxx = min(v.x for v in vs), max(v.x for v in vs)
mnz, mxz = min(v.z for v in vs), max(v.z for v in vs)
uvl = ob.data.uv_layers.new(name="UV")
for loop in ob.data.loops:
    co = ob.data.vertices[loop.vertex_index].co
    u = (x0 + (co.x - mnx) / (mxx - mnx) * (x1 - x0)) / w
    v = (y0 + (co.z - mnz) / (mxz - mnz) * (y1 - y0)) / h
    uvl.data[loop.index].uv = (u, v)

mat = bpy.data.materials.new("asa"); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.65
mat.use_backface_culling = False
ob.data.materials.clear(); ob.data.materials.append(mat)
img.pack()

# tamanho e pintura para trás (glTF -Z = costas do herói no Godot)
k = height / (mxz - mnz)
ob.data.transform(Matrix.Scale(k, 4))
ob.data.transform(Matrix.Rotation(math.pi, 4, 'Z'))
ob.data.update()

# duas metades pelo lado do centro de cada face
bpy.ops.object.mode_set(mode='EDIT')
bm = bmesh.from_edit_mesh(ob.data)
for f in bm.faces: f.select = f.calc_center_median().x < 0
bmesh.update_edit_mesh(ob.data)
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.data.objects if o.type == 'MESH']
for o in parts:
    cs = [v.co for v in o.data.vertices]
    cx = sum(c.x for c in cs) / len(cs)
    side = "dir" if cx > 0 else "esq"   # depois de virar: +X do Blender = direita do herói (glTF +X)
    o.name = o.data.name = "asa_" + side
    # dobradiça: os pontos mais perto do centro (5% de dentro), na altura média deles
    inner = sorted(cs, key=lambda c: abs(c.x))[: max(10, len(cs) // 20)]
    hz = sum(c.z for c in inner) / len(inner); hx = sum(c.x for c in inner) / len(inner); hy = sum(c.y for c in inner) / len(inner)
    o.data.transform(Matrix.Translation(Vector((-hx, -hy, -hz))))
    o.location = (hx, hy, hz)
    print("lado", side, "dobradiça", round(hx, 3), round(hz, 3), "faces", len(o.data.polygons))
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_yup=True, export_image_format='JPEG', export_jpeg_quality=90)
print("ok", dst)

"""Acrescenta clipes de acampamento aos humanoides Mixamo (goblins do lote 3), sem mexer nos clipes existentes.
Uso: python3 add_camp_clips.py <arquivo.blend> <saida.glb>
Clipes (lacos): Sentado (sentado no chao perto do fogo, respira, olha em volta), Mexendo (em pe, mexe a panela com a mao direita),
Conversando (em pe, gesticula e balanca a cabeca). Pes/quadril sempre no chao (ajuste automatico do Hips por quadro)."""
import sys, math, bpy
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz
from mathutils import Vector

blend, out = sys.argv[1], sys.argv[2]
bpy.ops.wm.open_mainfile(filepath=blend)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
K = 1.0 / arm.scale.x
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
B = lambda n: "mixamorig:" + n
for a in list(bpy.data.actions):
    if a.name in ("Sentado", "Mexendo", "Conversando"): bpy.data.actions.remove(a)

def pose_of(d):
    return {B(k): v for k, v in d.items()}

class Grounded(cz.Clip):
    """chave com o Hips ajustado para o ponto mais baixo tocar o chao (z=0)"""
    def key(self, f, pose, ground=None):
        p = {k: dict(v) for k, v in pose.items()}
        h = p.setdefault(B("Hips"), {}); h['l'] = (0, 0, 0)
        self._apply(p)
        z0 = cz.min_z(self.mesh)
        h['l'] = (0, 0, -z0)
        self._apply(p)
        for pb in self.arm.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=f); pb.keyframe_insert("location", frame=f)

def C(name, n): return Grounded(arm, name, n, mesh=mesh, S=K, ground=False, root=B("Hips"))
def m(*ds):
    o = {}
    for d in ds:
        for k, v in d.items():
            cur = o.setdefault(k, {}); cur['r'] = cur.get('r', []) + v.get('r', [])
    return o

ARMS_DOWN = {"LeftArm": {'r': [(Y, 68), (Z, -12)]}, "RightArm": {'r': [(Y, -68), (Z, 12)]}}
# Sentado de pernas cruzadas
SIT = m({"LeftUpLeg": {'r': [(X, -82), (Z, -38)]}, "RightUpLeg": {'r': [(X, -82), (Z, 38)]},
         "LeftLeg": {'r': [(X, 125), (Z, 55)]}, "RightLeg": {'r': [(X, 125), (Z, -55)]},
         "Spine": {'r': [(X, 8)]}, "Spine1": {'r': [(X, 6)]},
         "LeftArm": {'r': [(Y, 58), (Z, -32)]}, "RightArm": {'r': [(Y, -58), (Z, 32)]},
         "LeftForeArm": {'r': [(Z, -40)]}, "RightForeArm": {'r': [(Z, 40)]}})
cl = C("Sentado", 90)
for f, look, br in ((1, 0, 0), (25, 25, 1), (50, 0, 0), (70, -30, 1), (90, 0, 0)):
    cl.key(f, pose_of(m(SIT, {"Head": {'r': [(Z, look), (X, 6 * br)]}, "Spine2": {'r': [(X, 2 * br)]}})))
cl.done()
# Mexendo a panela: inclinado para a frente, braco direito faz um circulo
STIR = m(ARMS_DOWN, {"Spine": {'r': [(X, 12)]}, "Spine1": {'r': [(X, 8)]}, "Head": {'r': [(X, 10)]},
                     "LeftUpLeg": {'r': [(Z, -6)]}, "RightUpLeg": {'r': [(Z, 6)]}})
cl = C("Mexendo", 48)
for i, f in enumerate(range(1, 49, 6)):
    a = i / 8 * math.tau
    arm_r = {"RightArm": {'r': [(Y, -45), (Z, 35 + 10 * math.cos(a)), (X, 8 * math.sin(a))]}, "RightForeArm": {'r': [(Z, 40 + 8 * math.sin(a))]}}
    base = {k: v for k, v in STIR.items() if k != "RightArm"}
    cl.key(f, pose_of(m(base, arm_r, {"Spine1": {'r': [(Z, 4 * math.sin(a))]}})))
cl.key(49, pose_of(m({k: v for k, v in STIR.items() if k != "RightArm"}, {"RightArm": {'r': [(Y, -45), (Z, 45)]}, "RightForeArm": {'r': [(Z, 40)]}})))
cl.done()
# Conversando: gestos alternados e cabeca balancando
cl = C("Conversando", 72)
for f, l, r, nod in ((1, 0, 0, 0), (12, 1, 0, 1), (24, 0.3, 0, -1), (36, 0, 1, 1), (48, 0, 0.4, 0), (60, 0.8, 0.8, -1), (72, 0, 0, 0)):
    cl.key(f, pose_of(m({"LeftArm": {'r': [(Y, 68 - 40 * l), (Z, -12 - 30 * l)]}, "RightArm": {'r': [(Y, -68 + 40 * r), (Z, 12 + 30 * r)]},
                         "LeftForeArm": {'r': [(Z, -60 * l)]}, "RightForeArm": {'r': [(Z, 60 * r)]},
                         "Head": {'r': [(X, 8 * nod), (Z, 10 * (l - r))]}, "Spine1": {'r': [(Z, 5 * (l - r))]}})))
cl.done()
bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=blend)
for o in bpy.data.objects: o.select_set(True)
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', export_yup=True, export_apply=False, export_animations=True,
                          export_animation_mode='ACTIONS', export_skins=True, export_materials='EXPORT')
print("OK", out, [a.name for a in bpy.data.actions])

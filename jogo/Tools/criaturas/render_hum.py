"""Previa de humanoide Mixamo: python3 render_hum.py arquivo.blend altura  -> folha (pose T, Walking, Idle) + camera de jogo"""
import sys, bpy
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz
blend, h = sys.argv[1], float(sys.argv[2])
bpy.ops.wm.open_mainfile(filepath=blend)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
arm.animation_data_create()
pre = blend[:-6]; paths = []; labels = []
for act, fr, views in ((None, 1, ("front", "34", "side")), ("Walking", 6, ("34",)), ("Idle", 40, ("front",))):
    arm.animation_data.action = bpy.data.actions[act] if act else None
    if not act:
        for pb in arm.pose.bones: pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)
    bpy.context.scene.frame_set(fr)
    paths += cz.render_views(f"{pre}_{act or 'T'}", (0, 0, h / 2), h * 1.05, views=views, res=520, samples=20)
    labels += [f"{act or 'pose T'} {v}" for v in views]
cz.contact_sheet(paths, pre + "_folha.png", cols=5, cell=360, labels=labels)
print("SHEET", pre + "_folha.png")

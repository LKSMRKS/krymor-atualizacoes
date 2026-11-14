"""Previa de criatura com clipes em NLA: python3 render_criatura.py arquivo.blend tamanho"""
import sys, bpy
sys.path.insert(0, "/home/claude/cinzas/tools"); import cz
blend, h = sys.argv[1], float(sys.argv[2])
bpy.ops.wm.open_mainfile(filepath=blend)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for t in arm.animation_data.nla_tracks: t.mute = True
paths, labels = [], []
for act, fr, v in (("idle", 1, "34"), ("walk", 8, "side"), ("attack_windup", 14, "34"), ("attack_strike", 4, "34"), ("death", 40, "34")):
    arm.animation_data.action = bpy.data.actions[act]; bpy.context.scene.frame_set(fr)
    paths += cz.render_views(blend[:-6] + "_" + act, (0, 0, h * 0.3), h * 1.1, views=(v,), res=420, samples=16); labels.append(act)
cz.contact_sheet(paths, blend[:-6] + "_folha.png", cols=5, cell=360, labels=labels); print("SHEET")

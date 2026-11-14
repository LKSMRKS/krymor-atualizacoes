"""Previa na camera do jogo. Uso: python3 render_game.py saida.png yaw blend1 [blend2 ...]  (lado a lado, 2,2 m entre si)"""
import sys, bpy
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, pbr
a = sys.argv[1:]
out, yaw, blends = a[0], float(a[1]), a[2:]
bpy.ops.wm.read_factory_settings(use_empty=True)
groups = []
for b in blends:
    with bpy.data.libraries.load(b) as (src, dst): dst.objects = src.objects
    objs = [o for o in dst.objects if o is not None and o.type in ('MESH', 'ARMATURE')]
    for o in objs:
        bpy.context.collection.objects.link(o)
        if o.type == 'ARMATURE' and o.animation_data:
            for t in o.animation_data.nla_tracks: t.mute = True
            o.animation_data.action = None
    groups.append(objs)
n = len(groups); sp = float(__import__("os").environ.get("SPACING", "2.4"))
pbr.game_shot(out, [(g, (i - (n - 1) / 2) * sp, yaw) for i, g in enumerate(groups)], samples=int(__import__("os").environ.get("SAMPLES", "24")))

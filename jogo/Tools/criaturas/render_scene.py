"""Monta uma cena com .glb e renderiza na camera do jogo (ou mais longe). Uso: python3 render_scene.py cena.json saida.png"""
import sys, json, math, bpy
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, pbr
from mathutils import Vector
cfg = json.load(open(sys.argv[1])); out = sys.argv[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
groups = []
for it in cfg["itens"]:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=it["glb"])
    new = [o for o in bpy.data.objects if o not in before]
    roots = [o for o in new if o.parent is None or o.parent not in new]
    for r in roots:
        r.location = Vector((it["x"], it["y"], 0)); r.rotation_mode = 'XYZ'; r.rotation_euler = (0, 0, math.radians(it.get("yaw", 0)))
        if "s" in it: r.scale = (it["s"],) * 3
        if "sxyz" in it: r.scale = tuple(it["sxyz"])
    groups.append(new)
pbr.game_shot(out, [], dist=cfg.get("dist", 8.5), pitch_deg=cfg.get("pitch", 35.5), focus_h=cfg.get("focus_h", 1.55), ground_hex=cfg.get("chao", "#c9b48c"),
              samples=cfg.get("samples", 32), res=tuple(cfg.get("res", (1280, 720))), ground_tex=cfg.get("chao_tex"), sky=cfg.get("ceu"), sun_cfg=cfg.get("sol"))

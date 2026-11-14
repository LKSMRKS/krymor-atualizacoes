#!/usr/bin/env python3
"""
12/11 (pendência "dungeon no modo difícil a partir do 40"): ao lado da entrada de cada dungeon do meio/fim do jogo
(entrada com nível 30+), uma segunda entrada "(DIFÍCIL)" com trava no nível max(40, nível + 5). Lá dentro: outra instância,
monstros +5 níveis, ×1,8 vida, ×1,35 dano, ×1,6 XP e um sorteio de loot a mais (regras no servidor). Idempotente; rodar
depois de gerar_travas.py.
"""
import json, glob, os, math
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
mapas = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}
inst = {}
for mid, f in mapas.items():
    m = json.load(open(f, encoding="utf-8"))
    inst[mid] = m.get("instanced", False) and m.get("instance_scope", "party") != "personal"

def livre(m, x, y, r=2.2):
    if abs(x) > m["half_size"] - 3 or abs(y) > m["half_size"] - 3: return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r: return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r: return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 1.6 + r for p in m["props"]): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 5 for p in m["portals"]): return False
    if any(math.hypot(x - s["x"], y - s["y"]) < 10 for s in m["spawns"]): return False
    return True

chegadas = {}
for mid, f in mapas.items():
    for p in json.load(open(f, encoding="utf-8"))["portals"]:
        chegadas.setdefault(p["to_map"], []).append((p["to_x"], p["to_y"]))

feitos = []
for mid, f in mapas.items():
    m = json.load(open(f, encoding="utf-8"))
    m["portals"] = [p for p in m["portals"] if not p.get("hard")]
    novos = []
    for p in m["portals"]:
        if not inst.get(p["to_map"]) or p.get("min_level", 0) < 30 or p.get("requires_item"): continue
        lv = min(60, max(40, p["min_level"] + 5))
        for raio in range(4, 30):
            achou = None
            for k in range(0, 360, 15):
                x, y = round(p["x"] + raio * math.cos(math.radians(k)), 1), round(p["y"] + raio * math.sin(math.radians(k)), 1)
                if livre(m, x, y) and all(math.hypot(x - a, y - b) > 6 for a, b in chegadas.get(mid, [])): achou = (x, y); break
            if achou: break
        if not achou: continue
        novos.append({**p, "id": p["id"] + "_dificil", "x": achou[0], "y": achou[1], "label": p["label"] + " (DIFÍCIL)", "min_level": lv, "level_lock": True, "hard": True})
        feitos.append((mid, p["to_map"], lv))
    if novos or any(True for _ in []):
        m["portals"] += novos
    json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
for x in feitos: print(x)

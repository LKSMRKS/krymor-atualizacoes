"""Monstros da noite na Costa do Cal (11/11). Uso: python3 Tools/noite_costa.py (pode rodar de novo: troca o que gerou)

Carniçais Cáusticos e Fantasmas de Calcário saem das 19h às 5h (relógio do jogo) perto das ruínas, do farol, do
cemitério de navios e das trilhas — sempre ao lado de um ponto de monstro que já existe, com o mesmo nível dele (nunca
na zona inicial: só onde o nível já é 5 ou mais). Somem ao amanhecer se não estiverem lutando (GameServer: IsNight).
"""
import json, math, os, random

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "Content", "map_costa.json")
d = json.load(open(PATH, encoding="utf-8"))
rng = random.Random(1911)
d["spawns"] = [s for s in d["spawns"] if not s.get("night") and not s.get("cal_dia")]

def free(x, y, r=1.4):
    if abs(x) > d["half_size"] - 4 or abs(y) > d["half_size"] - 4: return False
    for o in d["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o.get("r", 0) + r: return False
        elif abs(x - o["x"]) < o.get("w", 0) / 2 + r and abs(y - o["y"]) < o.get("h", 0) / 2 + r: return False
    if any(math.hypot(x - s["x"], y - s["y"]) < 2.5 for s in d["spawns"]): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 2.5 for p in d["props"] if not p.get("decor")): return False
    return math.hypot(x - d["spawn_x"], y - d["spawn_y"]) > 30

base = [s for s in d["spawns"] if 5 <= s["level"] <= 12 and "boss" not in s["id"] and not s.get("grant_flag")]
rng.shuffle(base)
made, used = 0, []
for s in base:
    if made >= 10: break
    if any(math.hypot(s["x"] - ux, s["y"] - uy) < 18 for ux, uy in used): continue   # espalhados
    for _ in range(12):
        a = rng.uniform(0, math.tau); r = rng.uniform(3, 6)
        x, y = round(s["x"] + math.cos(a) * r, 1), round(s["y"] + math.sin(a) * r, 1)
        if free(x, y):
            kind = "carnical" if made % 2 == 0 else "fantasma"
            d["spawns"].append({"id": f"noite{made + 1}", "monster": f"monster_cal_{kind}", "x": x, "y": y,
                                "level": max(6, s["level"]), "group": f"noite{made + 1}", "respawn_seconds": 60, "night": True})
            used.append((s["x"], s["y"])); made += 1
            break

# 11/11 (pedido: "variar o bioma"): alguns também de dia, em duplas, perto dos monstros de nível 6+ (longe dos da noite)
dia = 0
for s in base:
    if dia >= 6: break
    if any(math.hypot(s["x"] - ux, s["y"] - uy) < 14 for ux, uy in used): continue
    for _ in range(12):
        a = rng.uniform(0, math.tau); r = rng.uniform(3.5, 6)
        x, y = round(s["x"] + math.cos(a) * r, 1), round(s["y"] + math.sin(a) * r, 1)
        if free(x, y):
            kind = "carnical" if dia % 2 == 0 else "fantasma"
            d["spawns"].append({"id": f"caldia{dia + 1}", "monster": f"monster_cal_{kind}", "x": x, "y": y,
                                "level": max(6, s["level"]), "group": f"caldia{dia + 1}", "respawn_seconds": 45, "cal_dia": True})
            used.append((s["x"], s["y"])); dia += 1
            break
print("de dia:", dia)

json.dump(d, open(PATH, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
open(PATH, "a").write("\n")
print("monstros da noite:", made)

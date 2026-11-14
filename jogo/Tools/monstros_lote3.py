"""Monstros do pacote "Monstros e Armaduras" do trabalhador (11/11). Uso: python3 Tools/monstros_lote3.py (pode rodar de novo)

- Galeria Funda (cal, nv 7-10): Rastejante de Gesso (sozinho, bote rápido) e Gargulitos (bando de 3).
- Guardião da Galeria passa a ter o corpo do Golem de Cal Viva (só a aparência; a luta é a mesma).
- Vale das Cinzas (nv 11-16): Espantalhos parados nos campos (acordam quando alguém chega) e Verme-Broca perto dos anões.
- Costa do Cal: Insetos do Lodo em volta das melecas (2-3 por grupo, do mesmo nível).
Pontos sempre livres (longe de obstáculo, outro monstro, enfeite com colisão e da chegada do mapa).
"""
import json, math, os, random

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(ROOT, "Content")
def load(f): return json.load(open(os.path.join(C, f), encoding="utf-8"))
def save(f, d):
    with open(os.path.join(C, f), "w", encoding="utf-8") as h: h.write(json.dumps(d, ensure_ascii=False, indent=2) + "\n")
rng = random.Random(311)

NEW = [
    dict(id="monster_cal_rastejante", species_name="Rastejante de Gesso", archetype="agil", level=9, behavior_id="beast_lunge",
         loot_table_id="loot_escavador", visual_asset_id="rastejante_gesso", health_mult=0.9, damage_mult=1.7, xp_mult=1.2, radius=0.5, coins_min=8, coins_max=14),
    dict(id="monster_cal_gargulito", species_name="Gargulito de Estalagmite", archetype="enxame", level=8, behavior_id="swarm_bite",
         loot_table_id="loot_larva", visual_asset_id="gargulito", health_mult=1.1, damage_mult=1.2, xp_mult=0.7, radius=0.45, coins_min=3, coins_max=6),
    dict(id="monster_vale_espantalho", species_name="Espantalho", archetype="combatente", level=12, behavior_id="melee_windup_swing",
         loot_table_id="loot_bst_planta_carnivora", visual_asset_id="espantalho", health_mult=1.0, damage_mult=1.8, xp_mult=1.1, radius=0.5, coins_min=12, coins_max=22),
    dict(id="monster_vale_verme_broca", species_name="Verme-Broca", archetype="tanque", level=14, behavior_id="digger",
         loot_table_id="loot_bst_anao_minerador", visual_asset_id="verme_broca", health_mult=1.5, damage_mult=1.9, xp_mult=2.0, radius=0.8, elite=True, coins_min=25, coins_max=45),
    dict(id="monster_bst_inseto_lodo", species_name="Inseto do Lodo", archetype="enxame", level=4, behavior_id="swarm_bite",
         loot_table_id="loot_bst_meleca", visual_asset_id="inseto_lodo", health_mult=0.5, damage_mult=0.8, xp_mult=0.4, radius=0.35, coins_min=1, coins_max=3),
]
ms = [m for m in load("monsters_novos.json") if m["id"] not in {n["id"] for n in NEW}]
save("monsters_novos.json", ms + NEW)

mons = load("monsters.json")
for m in mons:
    if m["id"] == "monster_r2_guardiao_galeria": m["visual_asset_id"] = "golem_cal_viva"
save("monsters.json", mons)

def place(mapfile, tag, wants, avoid_center=12):
    d = load(mapfile)
    d["spawns"] = [s for s in d["spawns"] if not s.get(tag)]
    def free(x, y, r=1.3):
        if abs(x) > d["half_size"] - 3 or abs(y) > d["half_size"] - 3: return False
        for o in d["obstacles"]:
            if o.get("shape") == "circle":
                if math.hypot(x - o["x"], y - o["y"]) < o.get("r", 0) + r: return False
            elif abs(x - o["x"]) < o.get("w", 0) / 2 + r and abs(y - o["y"]) < o.get("h", 0) / 2 + r: return False
        if any(math.hypot(x - s["x"], y - s["y"]) < 2.0 for s in d["spawns"]): return False
        if any(math.hypot(x - p["x"], y - p["y"]) < 2.2 for p in d.get("props", []) if not p.get("decor")): return False
        if any(math.hypot(x - p["x"], y - p["y"]) < 6 for p in d.get("portals", [])): return False
        return math.hypot(x - d["spawn_x"], y - d["spawn_y"]) > avoid_center
    n = 0
    for monster, level, cx, cy, count, spread in wants:
        group = f"{tag}{n}"
        for i in range(count):
            for _ in range(40):
                x = round(cx + rng.uniform(-spread, spread), 1); y = round(cy + rng.uniform(-spread, spread), 1)
                if free(x, y):
                    d["spawns"].append({"id": f"{tag}{n}_{i}", "monster": monster, "x": x, "y": y, "level": level, "group": group,
                                        "respawn_seconds": 45, tag: True})
                    break
        n += 1
    save(mapfile, d)
    return sum(1 for s in d["spawns"] if s.get(tag))

print("galeria:", place("map_galeria.json", "lote3", [
    ("monster_cal_rastejante", 9, -26, 6, 1, 2), ("monster_cal_rastejante", 9, 26, 10, 1, 2),
    ("monster_cal_gargulito", 8, -5, 9, 3, 2.5), ("monster_cal_gargulito", 9, 6, -15, 3, 2.5),
    ("monster_cal_rastejante", 10, -8, -22, 1, 2)]))
print("vale:", place("map_vale.json", "lote3", [
    ("monster_vale_espantalho", 12, -32, 18, 1, 3), ("monster_vale_espantalho", 12, 33, 19, 1, 3), ("monster_vale_espantalho", 13, -36, -16, 1, 3),
    ("monster_vale_verme_broca", 14, 14, -20, 1, 3), ("monster_vale_verme_broca", 15, 22, 8, 1, 3)]))
costa = load("map_costa.json")
mel = {}
for s in costa["spawns"]:
    if "meleca" in s["monster"]: mel.setdefault(s["group"], s)
print("costa:", place("map_costa.json", "lote3", [("monster_bst_inseto_lodo", max(4, s["level"] - 1), s["x"], s["y"] + 1.5, 3, 3.5) for s in mel.values()], 30))

# gárgula do muro: dois lados do portão do Porto, viradas para quem chega da Costa
porto = load("map_porto.json")
porto["props"] = [p for p in porto["props"] if p["kind"] != "gargula_muro"]
porto["props"] += [{"kind": "gargula_muro", "x": -4.6, "y": -29.0, "yaw": 180}, {"kind": "gargula_muro", "x": 4.6, "y": -29.0, "yaw": 180}]
save("map_porto.json", porto)

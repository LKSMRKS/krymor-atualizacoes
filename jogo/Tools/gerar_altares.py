#!/usr/bin/env python3
"""
13/11 ("todos os lugares têm um monstro para invocar? consegui pegar o nível 10 invocando a Rainha com 15 cascas"):
um Altar de Oferenda em cada mapa de campo (a Costa já tem o das Cascas). Gasta 15 do material mais comum da região e chama
o elite/chefe de campo dela (nível dele). Idempotente: refaz os objetos "altar_oferenda_*". Rodar depois do preenchimento.
"""
import json, os, glob, math, collections

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
mons, loot, itens = {}, {}, {}
for f in glob.glob(os.path.join(C, "monsters*.json")):
    for m in json.load(open(f, encoding="utf-8")): mons[m["id"]] = m
for f in glob.glob(os.path.join(C, "loot*.json")):
    for t in json.load(open(f, encoding="utf-8")): loot[t["id"]] = t
for f in glob.glob(os.path.join(C, "items*.json")):
    for i in json.load(open(f, encoding="utf-8")): itens[i["id"]] = i
PULA = {"laboratorio", "santuario_arena", "costa_do_cal"}
EVITA = {"monster_mundo_kraken"}   # chefe semanal não se invoca

def livre(m, x, y, r=1.6):
    if abs(x) > m["half_size"] - 5 or abs(y) > m["half_size"] - 5: return False
    if math.hypot(x - m["spawn_x"], y - m["spawn_y"]) < m["safe_radius"] + 4: return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r: return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r: return False
    for lst, d in ((m["props"], 2.0), (m["portals"], 6), (m["npcs"], 4), (m["objects"], 3), (m["spawns"], 3), (m.get("waypoints", []), 5)):
        if any(math.hypot(x - p["x"], y - p["y"]) < d for p in lst): return False
    return True

total = 0
for f in sorted(glob.glob(os.path.join(C, "map_*.json"))):
    m = json.load(open(f, encoding="utf-8"))
    if m.get("instanced") or m.get("kind") != "campo" or m["id"] in PULA: continue
    m["objects"] = [o for o in m["objects"] if not o["id"].startswith("altar_oferenda_")]
    fortes = [s for s in m["spawns"] if s["monster"] in mons and (mons[s["monster"]].get("elite") or mons[s["monster"]].get("boss"))
              and s["monster"] not in EVITA and not s["id"].startswith("portoes_")]
    if not fortes: continue
    alvo = sorted(fortes, key=lambda s: (not mons[s["monster"]].get("elite"), s["level"]))[0]
    c = collections.Counter()
    for s in m["spawns"]:
        d = mons.get(s["monster"])
        for e in (loot.get(d.get("loot_table_id"), {}).get("entries", []) if d else []):
            if e.get("item", "").startswith("mat_") and e["item"] in itens: c[e["item"]] += 1
    if not c: continue
    mat = c.most_common(1)[0][0]
    ponto = None
    for raio in range(18, 60, 3):
        for k in range(0, 360, 20):
            x = round(m["spawn_x"] + raio * math.cos(math.radians(k)), 1); y = round(m["spawn_y"] + raio * math.sin(math.radians(k)), 1)
            if livre(m, x, y): ponto = (x, y); break
        if ponto: break
    if not ponto: print("sem lugar:", m["id"]); continue
    nome = mons[alvo["monster"]]["species_name"]
    m["objects"].append({"id": f"altar_oferenda_{m['id']}", "kind": "altar", "x": ponto[0], "y": ponto[1],
                         "label": f"Altar de Oferenda ({nome})", "consumes": mat, "consume_qty": 15,
                         "summons": alvo["monster"], "summon_level": alvo["level"]})
    json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    total += 1
    print(f"  {m['id']}: 15× {itens[mat]['display_name']} → {nome} (nv {alvo['level']}) em {ponto}")
print("altares:", total)

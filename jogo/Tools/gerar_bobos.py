#!/usr/bin/env python3
"""
13/11 (pedido do dono): "as missões do bobo da corte têm que acompanhar o nível... cada cidade com o seu bobo, pedindo
coisas pareadas; cada bobo com 3 variações (até o 10: bichos até o 3, até o 7, até o 10) e mais XP, pra ter graça".
Um Bobo por região (Tião no Porto cuida da Costa). Cada bobo tem 3 VARIAÇÕES pela faixa de nível da região; cada variação
traz 2 desafios diários (caçar e juntar). A variação some quando você passa do teto dela (max_level) e a próxima aparece.
XP pareada: ~um terço de nível de bicho por desafio. Escreve Content/quests_bobos.json e põe os bobos nos acampamentos.
Idempotente. Rodar depois dos outros geradores de conteúdo.
"""
import json, os, glob, math

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}
mons, loot, itens = {}, {}, {}
for f in glob.glob(os.path.join(C, "monsters*.json")):
    for m in json.load(open(f, encoding="utf-8")): mons[m["id"]] = m
for f in glob.glob(os.path.join(C, "loot*.json")):
    for t in json.load(open(f, encoding="utf-8")): loot[t["id"]] = t
for f in glob.glob(os.path.join(C, "items*.json")):
    for i in json.load(open(f, encoding="utf-8")): itens[i["id"]] = i

# (mapa dos bichos, npc, nome, mapa onde fica o bobo, faixa)
BOBOS = [
    ("costa_do_cal", "bobo_tiao", None, "porto_das_cinzas", (1, 10)),
    ("vale_das_cinzas", "bobo_vale", "Zeca, o Bobo do Vale", "vale_das_cinzas", (11, 16)),
    ("mata_dos_enforcados", "bobo_mata", "Pipoca, o Bobo da Mata", "mata_dos_enforcados", (17, 26)),
    ("canions_suplicantes", "bobo_canions", "Eco, o Bobo dos Cânions", "canions_suplicantes", (24, 34)),
    ("brejo_da_viuva", "bobo_brejo", "Lodo, o Bobo do Brejo", "brejo_da_viuva", (30, 40)),
    ("vespera", "bobo_vespera", "Sino, o Bobo de Véspera", "vespera", (37, 47)),
    ("coroa_da_geada", "bobo_geada", "Gelinho, o Bobo da Caravana", "coroa_da_geada", (44, 54)),
    ("mar_de_escoria", "bobo_escoria", "Fagulha, o Bobo da Forja", "mar_de_escoria", (50, 58)),
    ("acropole_das_cinzas", "bobo_acropole", "Coro, o Bobo do Juramento", "acropole_das_cinzas", (56, 60)),
]
FALAS = ["«Aposto meu chapéu que você não consegue!»", "«Ha! Esse eu quero ver!»", "«Faça e eu canto seu nome na praça!»"]

def livre(m, x, y, r=1.6):
    if abs(x) > m["half_size"] - 4 or abs(y) > m["half_size"] - 4: return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r: return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r: return False
    for lst, d in ((m["props"], 2.0), (m["portals"], 5), (m["npcs"], 3), (m["objects"], 2.5), (m.get("waypoints", []), 4)):
        if any(math.hypot(x - p["x"], y - p["y"]) < d for p in lst): return False
    return True

def perto(m, tx, ty):
    for raio in range(0, 30):
        for k in range(0, 360, 20):
            x, y = round(tx + raio * math.cos(math.radians(k)), 1), round(ty + raio * math.sin(math.radians(k)), 1)
            if livre(m, x, y): return x, y
    raise SystemExit("sem lugar para o bobo em " + m["id"])

Q = []
ALVO_FIXO = {("costa_do_cal", 1): "monster_bst_cao_sarnento", ("costa_do_cal", 2): "monster_r1_gnomo_do_areal"}

for mapa, npc, nome, casa, (lo, hi) in BOBOS:
    m = json.load(open(ARQ[mapa], encoding="utf-8"))
    especies = {}
    for s in m["spawns"]:
        d = mons.get(s["monster"])
        if not d or d.get("boss") or d.get("elite") or d.get("rare") or s.get("chance") or s["id"].startswith(("portoes_", "costa_caranguejo")): continue
        especies.setdefault(s["monster"], []).append(s["level"])
    if not especies: continue
    passo = max(1, (hi - lo + 1) // 3)
    faixas = [(lo, lo + passo - 1), (lo + passo, lo + 2 * passo - 1), (lo + 2 * passo, hi)]
    for v, (a, b) in enumerate(faixas, start=1):
        # bicho da faixa (o nível médio mais perto do topo da faixa)
        alvo = min(especies, key=lambda k: (abs(sum(especies[k]) / len(especies[k]) - b), k))
        # 13/11 (dono: "o bobo pede gnomo arqueiro logo no começo, mas eles só liberam quando abre o muro e o portão do
        # castelo — coloca mais pra frente e no lugar põe cachorro"): alvo fixo por mapa/variação
        alvo = ALVO_FIXO.get((mapa, v), alvo) if ALVO_FIXO.get((mapa, v)) in especies else alvo
        lv_alvo = max(1, min(especies[alvo]))
        mat = None
        for e in loot.get(mons[alvo].get("loot_table_id"), {}).get("entries", []):
            if e.get("item", "").startswith("mat_") and e["item"] in itens and e.get("chance", 0) >= 0.25: mat = e["item"]; break
        xp = int(b * b * 60)
        base = {"giver": npc, "repeatable": True, "daily": True, "category": "diaria", "min_level": a, "max_level": b + 2, "map": mapa}
        nomebicho = mons[alvo]["species_name"]
        Q.append({**base, "id": f"q_bobo_{mapa}_{v}_caca", "name": f"Desafio do Bobo {v}/3: {nomebicho}", "kind": "kill", "targets": [alvo],
                  "count": 10, "description": f"{FALAS[v - 1]} Dez {nomebicho} até amanhã! (variação {v} de 3, para os níveis {a}–{b})",
                  "hint": f"{nomebicho} em {m['display_name']}. Volte ao bobo.", "reward_xp": xp, "reward_coins": b * 20,
                  "reward_items": ["consumable_c02_tonico_medio*2" if b >= 10 else "consumable_c01_tonico_fraco*2"]})
        if mat:
            Q.append({**base, "id": f"q_bobo_{mapa}_{v}_junta", "name": f"Chapéu do Bobo {v}/3: {itens[mat]['display_name']}", "kind": "collect",
                      "targets": [mat], "count": 8, "description": f"«Quero um chapéu novo de {itens[mat]['display_name']}! Traga oito e eu pago melhor que o mercador.»",
                      "hint": f"{itens[mat]['display_name']} cai de {nomebicho} ({m['display_name']}). Leve 8 ao bobo.",
                      "reward_xp": int(xp * 0.8), "reward_coins": b * 25, "reward_items": []})
    if nome:
        cm = json.load(open(ARQ[casa], encoding="utf-8"))
        cm["npcs"] = [n for n in cm["npcs"] if n["id"] != npc]
        x, y = perto(cm, cm["spawn_x"] + 6, cm["spawn_y"] - 3)
        cm["npcs"].append({"id": npc, "name": nome, "role": "missoes", "x": x, "y": y, "facing": 180, "look": "bobo",
                           "greeting": "Desafios diários do bobo! Três variações — conforme você cresce, eu peço coisa mais difícil e pago mais."})
        json.dump(cm, open(ARQ[casa], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"  {npc}: {[f'{a}-{b}' for a, b in faixas]}")

# o Tião antigo: os 3 desafios de antes ficam para o comecinho (até o nível 6)
d = json.load(open(os.path.join(C, "quests_diarias.json"), encoding="utf-8"))
for q in d:
    if q["giver"] == "bobo_tiao": q["max_level"] = 6
json.dump(d, open(os.path.join(C, "quests_diarias.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
json.dump(Q, open(os.path.join(C, "quests_bobos.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("desafios dos bobos:", len(Q))

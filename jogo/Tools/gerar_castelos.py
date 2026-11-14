#!/usr/bin/env python3
"""
12/11 (pedido do dono: "depois do 40 a moeda sobra — quests de entrar num castelo com um item específico, e esse item custa
caro"): três castelos do fim de jogo. Cada um copia o traçado da dungeon da região (salas, corredores, checkpoint), sobe os
monstros 2 níveis, troca o chefe pelo Senhor do Castelo (mais forte) e fica atrás de um portão no campo da região que
CONSOME um selo caro (vendido pelo Arauto no acampamento). O Arauto dá a missão de derrubar o senhor.
Idempotente. Rodar depois de Tools/gerar_regioes.py.
"""
import json, os, glob, copy, math

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")

CASTELOS = [
    # id, nome, dungeon-base, campo, tema, selo (id, nome, preço), senhor (id, nome), arauto (id, nome), nível da missão
    ("castelo_vespera", "Castelo de Véspera", "mercado_mortos", "vespera", "fantasma",
     ("selo_castelo_vespera", "Selo do Castelo de Véspera", 60000), ("monster_castelo_lorde_vespera", "Lorde Vesperino, o Insone"),
     ("arauto_vespera", "Arauto Vesperino"), 42),
    ("fortaleza_geada", "Fortaleza do Rei de Gelo", "salao_geada", "coroa_da_geada", "neve",
     ("selo_fortaleza_geada", "Selo da Fortaleza do Rei de Gelo", 150000), ("monster_castelo_rei_gelo", "O Rei de Gelo"),
     ("arauto_geada", "Arauta Runhild"), 50),
    ("cidadela_escoria", "Cidadela de Escória", "fornalha", "mar_de_escoria", "lava",
     ("selo_cidadela_escoria", "Selo da Cidadela de Escória", 300000), ("monster_castelo_senhor_escoria", "O Senhor da Escória"),
     ("arauto_escoria", "Arauto de Cinzas"), 56),
]


def L(name):
    return json.load(open(os.path.join(C, name), encoding="utf-8"))


def D(name, obj):
    json.dump(obj, open(os.path.join(C, name), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


def livre(m, x, y, r):
    if abs(x) > m["half_size"] - 3 or abs(y) > m["half_size"] - 3:
        return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r:
                return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r:
            return False
    for p in m["props"]:
        if math.hypot(x - p["x"], y - p["y"]) < 1.5 + r:
            return False
    return True


def lugar_portao(campo, cid):
    """Portão perto do canto leste-sul (62, -46): livre, longe de bichos (12 m) e com um ponto de chegada livre ao lado."""
    for raio in range(0, 45):
        for k in range(0, 360, 10):
            x = round(62 + raio * math.cos(math.radians(k)), 1)
            y = round(-46 + raio * math.sin(math.radians(k)), 1)
            if not livre(campo, x, y, 2.5):
                continue
            if any(math.hypot(x - s["x"], y - s["y"]) < 13 for s in campo["spawns"]):
                continue
            if any(math.hypot(x - p["x"], y - p["y"]) < 8 for p in campo["portals"] if p["id"] != cid + "_portao"):
                continue
            cx, cy = x - 3.2 * (1 if x > 0 else -1), y
            if livre(campo, cx, cy, 1.2):
                return x, y, cx, cy
    raise SystemExit("sem lugar para o portão de " + cid)


def main():
    mons = {}
    for f in glob.glob(os.path.join(C, "monsters*.json")):
        if not f.endswith("monsters_castelos.json"):
            for m in json.load(open(f, encoding="utf-8")):
                mons[m["id"]] = m
    world = L("world.json")
    shops = L("shops.json")
    itens, senhores, quests = [], [], []
    for cid, nome, base_id, campo_id, tema, (sid, snome, preco), (lid, lnome), (aid, anome), qlvl in CASTELOS:
        base = L(f"map_{base_id}.json")
        chefe_base = mons[base["first_clear_boss"]]
        senhores.append({**chefe_base, "id": lid, "species_name": lnome, "level": chefe_base["level"] + 3,
                         "health_mult": round(chefe_base["health_mult"] * 1.5, 2), "damage_mult": round(chefe_base["damage_mult"] * 1.15, 2),
                         "xp_mult": round(chefe_base["xp_mult"] * 1.6, 1), "coins_min": chefe_base["coins_min"] * 3, "coins_max": chefe_base["coins_max"] * 3,
                         "boss": True})
        m = copy.deepcopy(base)
        m.update(id=cid, display_name=nome, theme=tema, first_clear_boss=lid, exit_map=campo_id, exit_x=58, exit_y=-46,
                 xp_multiplier=round(base.get("xp_multiplier", 1) * 1.25, 2))
        for s in m["spawns"]:
            s["id"] = cid + "_" + s["id"]
            s["level"] += 2
            if s["monster"] == base["first_clear_boss"]:
                s["monster"] = lid
            s["group"] = cid + "_" + s.get("group", s["id"])
        for p in m["portals"]:
            p["id"] = p["id"].replace(base_id, cid)
            p["to_map"], p["to_x"], p["to_y"] = campo_id, 58, -46
        for z in m["zones"]:
            if z["kind"] == "chefe":
                z["name"] = "Salão do Trono: " + lnome
        D(f"map_{cid}.json", m)
        if f"map_{cid}.json" not in world["map_files"]:
            world["map_files"].append(f"map_{cid}.json")
        reg = next(r for r in world["regions"] if campo_id in r.get("maps", []))
        if cid not in reg["maps"]:
            reg["maps"].append(cid)
        campo = L(f"map_{campo_id}.json")
        campo["portals"] = [p for p in campo["portals"] if p["id"] != cid + "_portao"]
        px, py, cx, cy = lugar_portao(campo, cid)
        campo["portals"].append({"id": cid + "_portao", "x": px, "y": py, "radius": 1.8, "to_map": cid, "to_x": m["spawn_x"],
                                 "to_y": m["spawn_y"], "label": nome + " (selo)", "requires_item": sid, "min_level": qlvl})
        for p in m["portals"]:
            p["to_x"], p["to_y"] = cx, cy
        m["exit_x"], m["exit_y"] = cx, cy
        D(f"map_{cid}.json", m)
        if not any(n["id"] == aid for n in campo["npcs"]):
            campo["npcs"].append({"id": aid, "name": anome, "role": "missoes", "x": 14.0, "y": 62, "facing": 180, "look": "guarda",
                                  "shop": "loja_" + sid,
                                  "greeting": f"O {nome} não abre para qualquer um. O selo custa caro — e vale cada moeda."})
        D(f"map_{campo_id}.json", campo)
        itens.append({"id": sid, "display_name": snome, "category": "material", "subtype": "selo", "min_level": qlvl,
                      "allowed_classes": [], "handedness": 0, "base_stats": {}, "affix_pool": "none", "temperable": False,
                      "trade_rule": "free", "stack_limit": 10, "icon_asset_id": "icon_" + sid, "model_asset_id": "mat_selo",
                      "summary": f"Abre o portão do {nome} uma vez (some ao entrar). O castelo tem monstros 2 níveis acima, o {lnome} no salão do trono e drop de chefe.",
                      "price": preco, "source": f"{anome}, no acampamento da região."})
        if not any(s["id"] == "loja_" + sid for s in shops):
            shops.append({"id": "loja_" + sid, "name": anome, "items": [sid]})
        quests.append({"id": "q_" + cid, "name": f"O Senhor do {nome.split(' de ')[0].split(' do ')[0]}", "giver": aid, "kind": "boss",
                       "targets": [lid], "count": 1, "map": cid, "min_level": qlvl,
                       "description": f"«Compre o selo, atravesse o portão a leste e derrube {lnome} no salão do trono. Quem volta de lá volta rico — de fama, pelo menos.»",
                       "hint": f"Compre o {snome} com o {anome} ({preco:,} moedas) e entre pelo portão a leste do campo. Derrube {lnome}.".replace(",", "."),
                       "reward_xp": qlvl * qlvl * 120, "reward_coins": preco // 5, "category": "historia"})
    D("monsters_castelos.json", senhores)
    D("items_castelos.json", itens)
    D("quests_castelos.json", quests)
    D("shops.json", shops)
    D("world.json", world)
    print("castelos:", [c[0] for c in CASTELOS])


if __name__ == "__main__":
    main()

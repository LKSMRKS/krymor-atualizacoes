#!/usr/bin/env python3
"""
Reinos ocultos das transformações (12/11/2026, pedido do dono):
  Lua (Lobisomem, Guerreiro, 35) · Transilvânia (Vampiro, Arqueiro, 35) · Inferno (Ceifador, Mago, 35)
  Cidade Celestial (Anjo, Bruxa, 35) · Floresta Oculta (Máscara do Pandemônio, qualquer classe, 20)
Cada reino: chegada segura com portal de volta ao Porto, bolsões de monstros da forma, chefe no fundo,
obstáculos e enfeites do tema. Entrada pela viagem da Sibila (ou do Tião, na floresta) depois de ler o livro.
Uso: python3 Tools/gerar_reinos.py   (reescreve Content/map_reino_*.json e Content/monsters_reinos.json)
"""
import json
import os
import random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
H = 50

REINOS = [
    dict(id="reino_lua", nome="A Lua Vermelha", tema="lua", livro="livro:lobisomem", lvl=(35, 37),
         mobs=[("monster_bst_lobisomem", 10), ("monster_bst_chupa_cabra", 4)],
         chefe=("monster_lua_lobisomem_alfa", "Lobisomem Alfa", "werewolf", 38, "beast_lunge", "loot_bst_lobisomem"),
         obst=[("rocha", 1.2, 2.6), ("cristal_gelo", 0.8, 1.0)], props=["ossada", "estandarte"]),
    dict(id="reino_transilvania", nome="Transilvânia", tema="fantasma", livro="livro:vampiro", lvl=(35, 37),
         mobs=[("monster_bst_vampiro", 10), ("monster_bst_morcego_gigante", 0)],
         chefe=("monster_trans_conde", "O Conde da Transilvânia", "vampire", 38, "beast_lunge", "loot_bst_vampiro"),
         obst=[("lapide", 1.0, 1.0), ("coluna_quebrada", 0.5, 0.5), ("arvore_seca", 0.6, 0.8)], props=["lapide", "poste", "estandarte"]),
    dict(id="reino_inferno", nome="O Inferno", tema="lava", livro="livro:ceifador", lvl=(35, 37),
         mobs=[("monster_bst_a_morte", 8), ("monster_bst_demonio", 3)],
         chefe=("monster_inferno_ceifador_rei", "O Rei Ceifador", "death", 38, None, "loot_bst_a_morte"),
         obst=[("rocha_lava", 1.2, 1.2), ("rocha", 1.2, 2.2)], props=["tocha", "ossada"]),
    dict(id="reino_celeste", nome="A Cidade Celestial", tema="ceu", livro="livro:anjo", lvl=(35, 37),
         mobs=[("monster_bst_anjo", 10)],
         chefe=("monster_ceu_demonio_invasor", "O Demônio Invasor", "demon", 38, None, "loot_bst_demonio"),
         obst=[("coluna_quebrada", 0.5, 0.5), ("estatua_bronze", 0.9, 0.9)], props=["estandarte", "coluna_quebrada"]),
    dict(id="floresta_oculta", nome="A Floresta Oculta", tema="mata", livro="livro:mascara", lvl=(20, 22),
         mobs=[("monster_bst_saci", 12), ("monster_bst_lobisomem", 3)],
         chefe=None,
         obst=[("arvore", 0.7, 1.0), ("arvore_seca", 0.6, 0.8), ("rocha", 1.0, 1.6)], props=["pinheiros", "lapide"]),
]


# 12/11 ("reinos e arena não têm nenhum NPC — coloca alguns"): um morador em cada reino, ao lado da chegada
MORADORES = {
    "reino_lua": ("selene_eremita", "Selene, a Eremita da Lua", "bruxa", "Faz cem luas que eu não vejo gente. Os chupa-cabras daqui roem as minhas pedras de prata."),
    "reino_transilvania": ("igor_criado", "Igor, o Criado do Castelo", "aldeao", "O Conde não recebe visitas. Mas eu... eu recebo. Precisa de alguma coisa? Eu preciso."),
    "reino_inferno": ("caronte_barqueiro", "Caronte, o Barqueiro", "mentor", "Uma moeda na ida, outra na volta. Aqui embaixo até a morte cobra pedágio."),
    "reino_celeste": ("serafina_porteira", "Serafina, a Porteira do Céu", "curandeira", "Os portões estão arrombados. Quem tem coragem de lutar aqui em cima merece entrar."),
    "floresta_oculta": ("curupira", "Curupira, o Guardião da Mata", "guarda", "Pés virados para trás, é. Quem segue meu rastro se perde. Você me achou — então me ajuda."),
}


def circle(kind, x, y, r, h=4.0):
    return {"kind": kind, "shape": "circle", "x": round(x, 2), "y": round(y, 2), "w": 0, "h": 0, "r": round(r, 2), "height": h}


def longe(x, y, pts, d):
    return all((x - a) ** 2 + (y - b) ** 2 >= d * d for a, b in pts)


def montar(r, monstros_existentes):
    rnd = random.Random(r["id"])
    lo, hi = r["lvl"]
    zones = [{"id": "chegada", "name": "Chegada", "kind": "seguro", "x": 0, "y": 40, "w": 16, "h": 12},
             {"id": "trilha", "name": "Trilha", "kind": "caminho", "x": 0, "y": 6, "w": 5, "h": 60}]
    keep = [(0, 40), (0, 46), (0, -38)]
    spawns = []
    bolsoes = [(-28, 18), (28, 18), (-28, -14), (28, -14), (0, 0)]
    mobs = [(m, n) for m, n in r["mobs"] if m in monstros_existentes and n > 0]
    i = 0
    for m, n in mobs:
        for k in range(n):
            bx, by = bolsoes[i % len(bolsoes)]
            x, y = bx + rnd.uniform(-7, 7), by + rnd.uniform(-6, 6)
            spawns.append({"id": f"{r['id']}_{i}", "monster": m, "x": round(x, 1), "y": round(y, 1), "level": rnd.randint(lo, hi),
                           "group": f"{r['id']}_g{i // 2}", "respawn_seconds": 45})
            keep.append((x, y))
            i += 1
    if r["chefe"]:
        zones.append({"id": "covil", "name": f"Covil: {r['chefe'][1]}", "kind": "chefe", "x": 0, "y": -36, "w": 24, "h": 16})
        spawns.append({"id": f"{r['id']}_chefe", "monster": r["chefe"][0], "x": 0, "y": -38, "level": r["chefe"][3],
                       "group": f"{r['id']}_chefe", "respawn_seconds": 600})
    obst = []
    tries = 0
    while len(obst) < 34 and tries < 3000:
        tries += 1
        x, y = rnd.uniform(-H + 5, H - 5), rnd.uniform(-H + 5, H - 5)
        if abs(x) < 6 or (abs(x) < 12 and y > 32) or (abs(x) < 16 and abs(y + 37) < 10):
            continue
        if not longe(x, y, keep, 5) or not longe(x, y, [(o["x"], o["y"]) for o in obst], 4.5):
            continue
        k, r0, r1 = r["obst"][rnd.randrange(len(r["obst"]))]
        obst.append(circle(k, x, y, rnd.uniform(r0, r1)))
    for t in range(-H + 3, H - 2, 7):
        for (x, y) in [(t, -H + 2), (t, H - 2), (-H + 2, t), (H - 2, t)]:
            k, r0, _ = r["obst"][0]
            obst.append(circle(k, x + rnd.uniform(-1, 1), y + rnd.uniform(-1, 1), max(r0, 0.9)))
    props = [{"kind": "fogueira", "x": 0, "y": 38, "yaw": 0}, {"kind": "tocha", "x": -5, "y": 44, "yaw": 0}, {"kind": "tocha", "x": 5, "y": 44, "yaw": 0}]
    for j in range(16):
        x, y = rnd.uniform(-H + 8, H - 8), rnd.uniform(-H + 8, H - 8)
        if abs(x) < 6 or not longe(x, y, keep + [(o["x"], o["y"]) for o in obst], 3):
            continue
        props.append({"kind": r["props"][j % len(r["props"])], "x": round(x, 1), "y": round(y, 1), "yaw": rnd.randrange(360)})
    objects = []
    if r["id"] == "floresta_oculta":
        props.append({"kind": "arvore_serpente", "x": 22, "y": -30, "yaw": 200})
        objects.append({"id": "floresta_casca_serpente", "kind": "veio", "x": 22, "y": -28.4, "label": "Casca da Árvore da Serpente",
                        "yields": "mat_casca_serpente", "yield_min": 1, "yield_max": 1, "respawn_seconds": 300})
    return {
        "id": r["id"], "display_name": r["nome"], "half_size": H, "spawn_x": 0, "spawn_y": 42, "safe_radius": 7,
        "xp_multiplier": 1.15, "kind": "campo", "region": "reinos", "theme": r["tema"],
        "zones": zones, "obstacles": obst, "gates": [], "chests": [], "spawns": spawns, "props": props,
        "portals": [{"id": f"{r['id']}_volta", "x": 0, "y": 47, "radius": 2.0, "to_map": "porto_das_cinzas", "to_x": 16.0, "to_y": 10.0,
                     "label": "Voltar ao Porto"}],
        "waypoints": [], "objects": objects, "checkpoints": [],
        "npcs": [{"id": MORADORES[r["id"]][0], "name": MORADORES[r["id"]][1], "role": "missoes", "x": 6.0, "y": 40.0, "facing": 200,
                  "look": MORADORES[r["id"]][2], "greeting": MORADORES[r["id"]][3]}] if r["id"] in MORADORES else [],
    }


def main():
    existentes = set()
    for f in os.listdir(C):
        if f.startswith("monsters") and f.endswith(".json") and f != "monsters_reinos.json":
            existentes |= {m["id"] for m in json.load(open(os.path.join(C, f), encoding="utf-8"))}
    base = {m["id"]: m for m in json.load(open(os.path.join(C, "monsters_bestiario.json"), encoding="utf-8"))}
    chefes = []
    for r in REINOS:
        if not r["chefe"]:
            continue
        mid, nome, vis, lvl, beh, loot = r["chefe"]
        src = next(m for m in base.values() if m["visual_asset_id"] == vis)
        chefes.append({**src, "id": mid, "species_name": nome, "level": lvl, "behavior_id": beh or src["behavior_id"], "loot_table_id": loot,
                       "health_mult": round(src["health_mult"] * 7, 2), "damage_mult": round(src["damage_mult"] * 1.25, 2), "xp_mult": 12,
                       "radius": max(src["radius"], 1.0), "boss": True, "coins_min": lvl * 6, "coins_max": lvl * 10})
    json.dump(chefes, open(os.path.join(C, "monsters_reinos.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    existentes |= {c["id"] for c in chefes}
    for r in REINOS:
        json.dump(montar(r, existentes), open(os.path.join(C, f"map_{r['id']}.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        print("mapa", r["id"])


if __name__ == "__main__":
    main()

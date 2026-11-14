#!/usr/bin/env python3
"""
12/11 (pedido do dono: "sem chefe de campo do 20 em diante — tem que fazer mais chefes; e a Rainha das Cascas"):
chefes de campo das regiões geradas (Cânions, Brejo, Véspera, Acrópole) e a Rainha das Cascas na Costa.
Cada chefe é derivado de uma espécie da própria região (mesmo visual e comportamento, muito mais vida e dano), nasce num
canto do mapa longe do refúgio, respawn de 12 min, e solta o loot do elite da região (com a regra de chefe para peças).
Idempotente: pode rodar de novo (reescreve Content/monsters_chefes_campo.json e só acrescenta o spawn que faltar).
Rodar DEPOIS de Tools/gerar_regioes.py (que reescreve os mapas gerados).
"""
import json, math, os, glob

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")

CHEFES = [
    # mapa, id, nome, espécie-base, nível, alvo (x, y), loot
    ("map_canions_suplicantes.json", "monster_canions_rei_dos_canions", "Rei dos Cânions", "monster_canions_troll", 35, (-42, -48), "loot_canions_escorpiao_rei"),
    ("map_brejo_da_viuva.json", "monster_brejo_viuva_negra", "A Viúva Negra do Brejo", "monster_brejo_tecela", 41, (-42, -48), None),
    ("map_vespera.json", "monster_vespera_prefeito_sem_rosto", "O Prefeito Sem Rosto", "monster_vespera_cavaleiro_sem_rosto", 49, (-42, -48), None),
    ("map_acropole_das_cinzas.json", "monster_acropole_tita_bronze", "Titã de Bronze", "monster_acropole_minotauro_jovem", 64, (-42, -48), None),
]


def todos_monstros():
    out = {}
    for f in glob.glob(os.path.join(C, "monsters*.json")):
        if f.endswith("monsters_chefes_campo.json"):
            continue
        for m in json.load(open(f, encoding="utf-8")):
            out[m["id"]] = m
    return out


def livre(m, x, y, r=2.2):
    if abs(x) > m["half_size"] - 4 or abs(y) > m["half_size"] - 4:
        return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r:
                return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r:
            return False
    for p in m["props"]:
        if math.hypot(x - p["x"], y - p["y"]) < 2.0 + r:
            return False
    for s in m["spawns"]:
        if math.hypot(x - s["x"], y - s["y"]) < 6:
            return False
    return math.hypot(x - m["spawn_x"], y - m["spawn_y"]) > 40


def perto(m, tx, ty):
    for raio in range(0, 40):
        for k in range(0, 360, 15):
            x = round(tx + raio * math.cos(math.radians(k)), 1)
            y = round(ty + raio * math.sin(math.radians(k)), 1)
            if livre(m, x, y):
                return x, y
    raise SystemExit("sem lugar livre perto de %s,%s" % (tx, ty))


def main():
    base = todos_monstros()
    novos = []
    for arq, mid, nome, esp, lvl, (tx, ty), loot in CHEFES:
        b = base[esp]
        elite_loot = loot or next((m["loot_table_id"] for m in base.values() if m.get("elite") and m["id"].split("_")[1] == esp.split("_")[1]), b["loot_table_id"])
        novos.append({**b, "id": mid, "species_name": nome, "level": lvl, "loot_table_id": elite_loot,
                      "health_mult": round(b["health_mult"] * 8, 2), "damage_mult": round(b["damage_mult"] * 1.35, 2), "xp_mult": 14.0,
                      "radius": max(b["radius"], 1.3), "boss": True, "elite": False, "coins_min": lvl * 30, "coins_max": lvl * 60})
        p = os.path.join(C, arq)
        m = json.load(open(p, encoding="utf-8"))
        if not any(s["monster"] == mid for s in m["spawns"]):
            x, y = perto(m, tx, ty)
            m["spawns"].append({"id": mid.replace("monster_", "cc_"), "monster": mid, "x": x, "y": y, "level": lvl,
                                "group": mid.replace("monster_", "cc_"), "respawn_seconds": 720})
            m["zones"].append({"id": "trono_" + mid.split("_")[1], "name": "Trono: " + nome, "kind": "chefe", "x": x, "y": y, "w": 18, "h": 18})
            json.dump(m, open(p, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
            print("chefe de campo", nome, "em", arq, x, y)
    json.dump(novos, open(os.path.join(C, "monsters_chefes_campo.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    # Rainha das Cascas: chefe de campo raro da Costa (Ruínas do Farol), sem mudar o id
    p = os.path.join(C, "map_costa.json")
    m = json.load(open(p, encoding="utf-8"))
    if not any(s["monster"] == "monster_r1_rainha_das_cascas" for s in m["spawns"]):
        x, y = perto(m, 48, -48)
        m["spawns"].append({"id": "rainha_cascas", "monster": "monster_r1_rainha_das_cascas", "x": x, "y": y, "level": 11,
                            "group": "rainha_cascas", "respawn_seconds": 900})
        json.dump(m, open(p, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        print("Rainha das Cascas na Costa", x, y)


if __name__ == "__main__":
    main()

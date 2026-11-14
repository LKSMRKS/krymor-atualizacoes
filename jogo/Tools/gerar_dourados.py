#!/usr/bin/env python3
"""
13/11 ("assim como o caranguejo que aparece e dá um drop legal, faz o goblin de ouro também, e outros pelos mapas"):
versão DOURADA (rara) de um bicho comum de cada mapa de campo. Foge de você (comportamento "fugitive"), aparece em
qualquer canto (o servidor sorteia o ponto metade das vezes), solta muita moeda e SEMPRE uma Caixa das Cinzas.
Escreve Content/monsters_dourados.json e o spawn "<mapa>_dourado" (chance 35%, volta a sortear a cada 5 min). Idempotente.
"""
import json, os, glob

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
mons = {}
for f in glob.glob(os.path.join(C, "monsters*.json")):
    if f.endswith("monsters_dourados.json"): continue
    for m in json.load(open(f, encoding="utf-8")): mons[m["id"]] = m
PULA = {"laboratorio", "santuario_arena"}
FEM = ("Aranha", "Harpia", "Sereia", "Enguia", "Raiz", "Tecelã", "Serpente", "Sanguessuga", "Esfera", "Meleca", "Cabra", "Larva")
NOVOS = []
for f in sorted(glob.glob(os.path.join(C, "map_*.json"))):
    m = json.load(open(f, encoding="utf-8"))
    if m.get("instanced") or m.get("kind") != "campo" or m["id"] in PULA: continue
    m["spawns"] = [s for s in m["spawns"] if not s["id"].endswith("_dourado")]
    comuns = {}
    for s in m["spawns"]:
        d = mons.get(s["monster"])
        if not d or d.get("boss") or d.get("elite") or d.get("rare") or s.get("chance") or s["id"].startswith(("portoes_", "costa_caranguejo")): continue
        comuns.setdefault(s["monster"], []).append(s)
    if not comuns: continue
    # Costa: o Goblin de Ouro (pedido do dono); nos outros, a espécie mais comum
    alvo = "monster_r1_goblin_saqueador" if m["id"] == "costa_do_cal" and "monster_r1_goblin_saqueador" in comuns \
        else sorted(comuns, key=lambda k: (-len(comuns[k]), k))[0]
    base = mons[alvo]
    nome = base["species_name"]
    w = nome.split()[0]
    fem = w == "A" or w in FEM or w.split("-")[0].endswith(("a", "ã"))
    dourado = "de Ouro" if "Goblin" in nome else ("Dourada" if fem else "Dourado")
    lv = max(s["level"] for s in comuns[alvo])
    mid = f"monster_{m['id']}_dourado"
    NOVOS.append({**base, "id": mid, "species_name": f"{nome} {dourado}", "behavior_id": "fugitive", "rare": True, "elite": False, "boss": False,
                  "level": lv, "health_mult": round(base["health_mult"] * 1.6, 2), "xp_mult": 5.0,
                  "coins_min": lv * 12, "coins_max": lv * 25})
    hs = m["half_size"] * 0.6
    pts = [[round(x, 1), round(y, 1)] for x, y in ((hs, hs * 0.4), (-hs, hs * 0.3), (hs * 0.5, -hs), (-hs * 0.6, -hs * 0.8))]
    s0 = comuns[alvo][0]
    m["spawns"].append({"id": f"{m['id']}_dourado", "monster": mid, "x": s0["x"], "y": s0["y"], "level": lv, "group": f"{m['id']}_dourado",
                        "respawn_seconds": 300, "chance": 0.35, "alt_points": [[s0["x"], s0["y"]]] + pts})
    json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"  {m['id']}: {nome} {dourado} (nv {lv})")
json.dump(NOVOS, open(os.path.join(C, "monsters_dourados.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("dourados:", len(NOVOS))

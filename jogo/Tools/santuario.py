"""Santuário do Céu (11/11, pedido do usuário): área de teste só para o adm (sem portal; entra pelo painel de Deus →
Viajar). Praça de mármore com lojas de TUDO (armas, armaduras, asas e joias, consumíveis), Máquina do Caos e forja; ao
sul a arena (anel de colunas) para chamar qualquer monstro pelo painel de Deus e testar armas e armaduras.
Uso: python3 Tools/santuario.py (refaz o mapa e as lojas "santuario_*")."""
import json, math, os
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(ROOT, "Content")
def load(f): return json.load(open(os.path.join(C, f), encoding="utf-8"))
def save(f, d):
    with open(os.path.join(C, f), "w", encoding="utf-8") as h: h.write(json.dumps(d, ensure_ascii=False, indent=2) + "\n")

items = {}
for f in sorted(os.listdir(C)):
    if f.startswith("items") and f.endswith(".json"):
        for it in load(f): items[it["id"]] = it
cons = {}
for f in sorted(os.listdir(C)):
    if f.startswith("consumables") and f.endswith(".json"):
        for it in load(f): cons[it["id"]] = it
def by_level(ids): return sorted(ids, key=lambda i: (items[i].get("min_level", 1), i))
armas = by_level([i for i, d in items.items() if d["category"] in ("weapon", "offhand")])
armaduras = by_level([i for i, d in items.items() if d["category"] in ("armor", "helmet", "gloves", "boots", "pants")])
extras = by_level([i for i, d in items.items() if d["category"] in ("wings", "cape", "ring", "amulet", "pet")]) + \
    [i for i in ("joia_mare", "joia_brasa", "joia_vazio", "portal_viagem") if i in items]
consumiveis = sorted(i for i in cons if not i.startswith("tomo_") and cons[i].get("price", 0) > 0)[:60]

shops = [s for s in load("shops.json") if not s["id"].startswith("santuario_")]
shops += [{"id": "santuario_armas", "name": "Arsenal do Santuário", "items": armas},
          {"id": "santuario_armaduras", "name": "Armaduras do Santuário", "items": armaduras},
          {"id": "santuario_asas", "name": "Asas, Joias e Companheiros", "items": extras},
          {"id": "santuario_pocoes", "name": "Poções do Santuário", "items": consumiveis},
          # 11/11 noite (teste do Excelente e do +1..+15 como no MU): tudo sai Excelente; as joias de refino junto
          {"id": "santuario_excelente", "name": "Ourives Celeste (tudo Excelente)", "items": armas + armaduras + [i for i in ("joia_mare", "joia_brasa", "joia_vazio") if i in items]},
          # 14/11 tarde (pedido: "aumenta a cidade celestial e coloca os NPCs com os itens novos lá — quero testar, info e
          # comprando; e NPCs com a Caixa de Pandora +1 até +5"): uma loja por coleção
          {"id": "santuario_col_asas", "name": "Coleção de Asas (todas)", "items": by_level([i for i, d in items.items() if d["category"] == "wings"])},
          {"id": "santuario_col_capas", "name": "Coleção de Capas (todas)", "items": by_level([i for i, d in items.items() if d["category"] == "cape"])},
          {"id": "santuario_col_skins", "name": "Skins de Bracelete (todas)", "items": by_level([i for i, d in items.items() if d["category"] == "bracelet"])},
          {"id": "santuario_col_bichos", "name": "Mascotes e Montarias", "items": by_level([i for i, d in items.items() if d["category"] in ("pet", "mount")])},
          {"id": "santuario_pandora", "name": "Caixas de Pandora e das Cinzas", "items": [f"caixa_pandora_{n}" for n in range(1, 6)] + [f"caixa_cinzas_{n}" for n in range(1, 6)]}]
save("shops.json", shops)

# 11/11 (noite, pedido: "faz o santuário tipo a cidade, com NPCs e TPs; arena em outra área, assim não pesa"):
# praça pequena e simples (tema cidade) só com as lojas e portais; a arena virou um mapa à parte.
# 14/11 tarde: praça maior (60 x 60) em dois anéis — dentro as lojas de teste antigas, fora as coleções novas e a Pandora
npcs = [
    {"id": "sant_armas", "name": "Arcanjo Ferreiro", "role": "ferreiro", "x": -9, "y": -2, "facing": 90, "greeting": "Toda arma do mundo, para testar.", "look": "ferreiro", "shop": "santuario_armas"},
    {"id": "sant_armaduras", "name": "Guardiã das Armaduras", "role": "armeira", "x": -3.5, "y": 3, "facing": 180, "greeting": "Vista, troque, compare: aqui nada se perde.", "look": "capita", "shop": "santuario_armaduras"},
    {"id": "sant_asas", "name": "Serafim das Asas", "role": "mercador", "x": 3.5, "y": 3, "facing": 180, "greeting": "Asas, capas, anéis, joias e bichinhos.", "look": "arcanista", "shop": "santuario_asas"},
    {"id": "sant_pocoes", "name": "Curandeira Celeste", "role": "curandeira", "x": 9, "y": -2, "facing": -90, "greeting": "Poções e comidas para os testes.", "look": "curandeira", "shop": "santuario_pocoes"},
    {"id": "sant_ourives", "name": "Ourives Celeste", "role": "ferreiro", "x": 0, "y": 6, "facing": 180, "greeting": "Aqui tudo sai Excelente. Arraste uma Joia em cima do equipamento para subir +1, +2... até +15 e ver o brilho crescer.", "look": "arcanista", "shop": "santuario_excelente"},
    {"id": "sant_col_asas", "name": "Querubina, Guardiã das Asas", "role": "mercador", "x": -20, "y": 12, "facing": 135, "greeting": "As 25 asas da coleção, de pano a Criação — e as antigas. Passe o mouse para ver de onde vem cada uma.", "look": "arcanista", "shop": "santuario_col_asas"},
    {"id": "sant_col_capas", "name": "Celina, a Costureira do Céu", "role": "mercador", "x": -20, "y": -10, "facing": 45, "greeting": "Todas as capas: do Herói ao Tempo. Vista e gire a câmera para ver as costas.", "look": "aldeao", "shop": "santuario_col_capas"},
    {"id": "sant_col_skins", "name": "Bento Celeste, o Mascate das Skins", "role": "mercador", "x": 20, "y": -10, "facing": -45, "greeting": "Todas as skins de bracelete: macaco, tubarão, fantasma, lava... o olhinho no bracelete esconde.", "look": "aldeao", "shop": "santuario_col_skins"},
    {"id": "sant_col_bichos", "name": "Tratadora Celeste", "role": "mercador", "x": 20, "y": 12, "facing": -135, "greeting": "Mascotes e montarias, todos de uma vez.", "look": "curandeira", "shop": "santuario_col_bichos"},
    {"id": "sant_pandora", "name": "Pandora, a Guardiã das Caixas", "role": "mercador", "x": 0, "y": 22, "facing": 180, "greeting": "Caixa de Pandora +1 a +5: não abre na mão — JOGUE NO CHÃO e ela estoura em itens (como a do Kundun). Afaste-se de quem você não quer que pegue!", "look": "arcanista", "shop": "santuario_pandora"},
]
H = 30
def walls(h, kind="muralha"): return [{"kind": kind, "shape": "box", "x": 0, "y": -h, "w": 2 * h, "h": 1.2, "r": 0, "height": 4},
                      {"kind": kind, "shape": "box", "x": 0, "y": h, "w": 2 * h, "h": 1.2, "r": 0, "height": 4},
                      {"kind": kind, "shape": "box", "x": -h, "y": 0, "w": 1.2, "h": 2 * h, "r": 0, "height": 4},
                      {"kind": kind, "shape": "box", "x": h, "y": 0, "w": 1.2, "h": 2 * h, "r": 0, "height": 4}]
colunas = [{"kind": "coluna_quebrada", "shape": "circle", "x": round(26 * math.cos(a), 2), "y": round(26 * math.sin(a), 2), "w": 0, "h": 0, "r": 0.5, "height": 4.0}
           for a in [k * math.tau / 16 for k in range(16)] if abs(math.cos(a)) < 0.9 or abs(math.sin(a)) > 0.2]
props = [{"kind": "glb:cenario/acropole_braseiro", "x": x, "y": y, "yaw": 0} for x, y in ((-14, 17), (14, 17), (-14, -17), (14, -17))] + \
        [{"kind": k, "x": x, "y": y, "yaw": yaw} for k, x, y, yaw in (("estandarte", -6, 25, 0), ("estandarte", 6, 25, 0), ("canteiro_flores", -26, 0, 90),
                                                                      ("canteiro_flores", 26, 0, 90), ("banco_praca", -12, 0, 90), ("banco_praca", 12, 0, -90),
                                                                      ("floreira", -24, 22, 0), ("floreira", 24, 22, 0), ("floreira", -24, -22, 0), ("floreira", 24, -22, 0))]
m = {"id": "santuario_ceu", "display_name": "Santuário (teste)", "half_size": H, "spawn_x": 0, "spawn_y": -8,
     "safe_radius": 45, "xp_multiplier": 1, "kind": "cidade", "region": "porto", "theme": "ceu", "no_mount": False,
     "zones": [], "obstacles": walls(H - 0.6, "invisivel") + colunas, "gates": [], "chests": [], "spawns": [], "props": props,
     "portals": [
         {"id": "sant_porto", "x": -12, "y": -24, "radius": 1.6, "to_map": "porto_das_cinzas", "to_x": 0, "to_y": -24, "label": "Porto das Cinzas"},
         {"id": "sant_arena", "x": 12, "y": -24, "radius": 1.6, "to_map": "santuario_arena", "to_x": 0, "to_y": -20, "label": "Arena de Teste"},
         {"id": "sant_costa", "x": 0, "y": -27, "radius": 1.6, "to_map": "costa_do_cal", "to_x": 0, "to_y": 100, "label": "Costa do Cal"}],
     "waypoints": [], "npcs": npcs,
     "objects": [{"id": "sant_caos", "kind": "caos", "x": -6, "y": 12, "label": "Máquina do Caos"},
                 {"id": "sant_forja", "kind": "forja", "x": 6, "y": 12, "label": "Forja"}],
     "checkpoints": []}
save("map_santuario_ceu.json", m)

# arena: campo aberto com um bando de cada faixa de nível (renasce em 8 s) para testar armas e armaduras
mons = {}
for f in sorted(os.listdir(C)):
    if f.startswith("monsters") and f.endswith(".json"):
        for x in load(f): mons[x["id"]] = x
lv = sorted((x.get("level", 1), i) for i, x in mons.items() if not x.get("boss"))
pick = [lv[k * (len(lv) - 1) // 11][1] for k in range(12)]
spawns = []
for k, mid in enumerate(pick):
    gx, gy = (k % 4) - 1.5, (k // 4) - 1
    spawns.append({"id": f"ar{k}", "monster": mid, "x": round(gx * 12, 1), "y": round(4 + gy * 12, 1),
                   "level": mons[mid].get("level", 1), "group": f"ar{k}", "respawn_seconds": 8})
A = 34
arena = {"id": "santuario_arena", "display_name": "Arena de Teste", "half_size": A, "spawn_x": 0, "spawn_y": -24,
         "safe_radius": 4, "xp_multiplier": 1, "kind": "campo", "region": "porto", "theme": "cidade", "no_mount": False,
         "zones": [{"name": "Arena", "kind": "medio", "x": 0, "y": 4, "w": 56, "h": 44}],
         "obstacles": walls(A - 0.6), "gates": [], "chests": [], "spawns": spawns, "props": [],
         "portals": [{"id": "arena_sant", "x": 0, "y": -30, "radius": 1.6, "to_map": "santuario_ceu", "to_x": 0, "to_y": -8, "label": "Santuário"}],
         "waypoints": [], "npcs": [], "objects": [], "checkpoints": []}
if not os.path.exists(os.path.join(C, "map_santuario_arena.json")): save("map_santuario_arena.json", arena)   # 14/11: outros geradores põem NPCs nela
w = load("world.json")
for f, mid in (("map_santuario_ceu.json", "santuario_ceu"), ("map_santuario_arena.json", "santuario_arena")):
    if f not in w["map_files"]: w["map_files"].append(f)
    for r in w["regions"]:
        if r["id"] == "porto" and mid not in r["maps"]: r["maps"].append(mid)
save("world.json", w)
print("arena:", pick)
print("armas", len(armas), "armaduras", len(armaduras), "extras", len(extras), "consumíveis", len(consumiveis))

# 14/11: lojas lendárias do trabalhador (7 NPCs sant_lend_*) — repõe depois de refazer o Santuário
import subprocess, sys
subprocess.run([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), "instalar_lojas_lendarias.py"), "--aplicar"], check=False)

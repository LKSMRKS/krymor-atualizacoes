#!/usr/bin/env python3
"""
13/11 (análise do jogo, Leva 1 aprovada): conteúdo da rotina e do começo. Rodar por último (idempotente).
• Quadro de Contratos no Porto (NPC "Escrivã Odila" + placa): 3 contratos por mapa de caça, diários, na faixa do mapa.
  O servidor oferece só 3 por dia por personagem (sorteio por dia) — GameServer.Rotina.ContratosDoDia.
• Mini-chefe do começo: Caranguejo-Mãe do Cal (nível 5) no Areal Fundo/Dunas, com aviso para todos (chefe de campo de 5 min).
"""
import json, os, glob, math, random
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
def L(n): return json.load(open(os.path.join(C, n), encoding="utf-8"))
def D(n, o): json.dump(o, open(os.path.join(C, n), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: os.path.basename(f) for f in glob.glob(os.path.join(C, "map_*.json"))}
mons = {}
for f in glob.glob(os.path.join(C, "monsters*.json")):
    if not f.endswith("monsters_leva1.json"):
        for m in json.load(open(f, encoding="utf-8")): mons[m["id"]] = m

def livre(m, x, y, r=1.5):
    if abs(x) > m["half_size"] - 3 or abs(y) > m["half_size"] - 3: return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r: return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r: return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 1.6 + r for p in m["props"]): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 4 for p in m["portals"]): return False
    if any(math.hypot(x - n["x"], y - n["y"]) < 2.5 for n in m["npcs"]): return False
    return True

def perto(m, tx, ty, r=1.5):
    for raio in range(0, 40):
        for k in range(0, 360, 15):
            x, y = round(tx + raio * math.cos(math.radians(k)), 1), round(ty + raio * math.sin(math.radians(k)), 1)
            if livre(m, x, y, r): return x, y
    raise SystemExit("sem lugar")

# ---------------- quadro de contratos
porto = L(ARQ["porto_das_cinzas"])
porto["npcs"] = [n for n in porto["npcs"] if n["id"] != "escriva_contratos"]
porto["props"] = [p for p in porto["props"] if not (p.get("kind") == "placa" and p.get("tag") == "quadro")]
nx, ny = perto(porto, 4.5, -15.5)
porto["npcs"].append({"id": "escriva_contratos", "name": "Odila, a Escrivã do Quadro", "role": "contratos", "x": nx, "y": ny, "facing": 180,
                      "look": "aldeao", "greeting": "«Três contratos por dia, escolhidos para o seu nível. Cumpra e volte — o quadro paga melhor que muito chefe.»"})
px, py = perto(porto, nx + 1.8, ny, 0.8)
porto["props"].append({"kind": "placa", "x": px, "y": py, "yaw": 180, "tag": "quadro"})
porto["npcs"] = [n for n in porto["npcs"] if n["id"] != "arauto_placar"]
ax, ay = perto(porto, nx - 3.5, ny)
porto["npcs"].append({"id": "arauto_placar", "name": "Bartô, o Arauto do Placar", "role": "placar", "x": ax, "y": ay, "facing": 180,
                      "look": "guarda", "greeting": "«Aqui se escreve quem é quem no Porto. Fale comigo e eu leio o placar em voz alta!»"})
D(ARQ["porto_das_cinzas"], porto)

Q = []
xp = lambda l: int(max(1, l) ** 2 * 45)
for mid, f in sorted(ARQ.items()):
    m = L(f)
    if m.get("instanced") or not m["spawns"] or mid in ("santuario_arena", "laboratorio"): continue
    comuns = {}
    for s in m["spawns"]:
        d = mons.get(s["monster"])
        if not d or d.get("boss") or s["id"].startswith(("portoes_", "chefe_semanal")): continue
        comuns.setdefault(s["monster"], []).append(s["level"])
    if not comuns: continue
    esp = sorted(comuns.items(), key=lambda kv: (-len(kv[1]), kv[0]))[:3]
    for i, (mon, lvs) in enumerate(esp):
        lv = max(3, min(lvs))
        nome = mons[mon]["species_name"]
        n = 6 if mons[mon].get("elite") else 12
        Q.append({"id": f"q_contrato_{mid}_{i + 1}", "name": f"Contrato: {nome}", "giver": "escriva_contratos", "kind": "kill",
                  "targets": [mon], "count": n, "map": mid, "min_level": lv,
                  "description": f"«Pedido afixado no quadro: {n} {nome} a menos em {m['display_name']}. Pagamento na entrega, hoje.»",
                  "hint": f"{nome} em {m['display_name']}. Volte à Odila (quadro do Porto).",
                  "reward_xp": xp(lv), "reward_coins": lv * 25, "reward_items": ["consumable_c02_tonico_medio*2"] if lv >= 10 else ["consumable_c01_tonico_fraco*2"],
                  "category": "contrato", "repeatable": True, "daily": True, "board": "contratos"})
D("quests_leva1.json", Q)

# ---------------- mini-chefe do começo
base = mons["monster_r1_caranguejo_de_cal"]
mae = {**base, "id": "monster_costa_caranguejo_mae", "species_name": "Caranguejo-Mãe do Cal", "level": 5, "boss": True, "elite": False,
       "health_mult": round(base["health_mult"] * 5, 2), "damage_mult": round(base["damage_mult"] * 1.15, 2), "xp_mult": 8.0,
       "radius": max(base["radius"] * 1.6, 1.0), "coins_min": 60, "coins_max": 120}
D("monsters_leva1.json", [mae])
costa = L(ARQ["costa_do_cal"])
costa["spawns"] = [s for s in costa["spawns"] if s["id"] != "costa_caranguejo_mae"]
cx, cy = perto(costa, -66, 50, 1.6)
costa["spawns"].append({"id": "costa_caranguejo_mae", "monster": "monster_costa_caranguejo_mae", "x": cx, "y": cy, "level": 5,
                        "group": "costa_caranguejo_mae", "respawn_seconds": 300})
D(ARQ["costa_do_cal"], costa)
print("contratos:", len(Q), "· quadro em", (nx, ny), "· Caranguejo-Mãe em", (cx, cy))

# ---------------- 13/11 ("coloca caranguejo maior pra dropar 'carne maior'"): a Caranguejo-Mãe sempre solta Carne Maior;
# o Caranguejo de Cal às vezes. Na panela do Porto, 2 Carnes Maiores viram Espetinho de Caranguejo (cura média).
carne = {"id": "mat_carne_maior", "display_name": "Carne Maior", "category": "material", "subtype": "material", "min_level": 1,
         "allowed_classes": [], "handedness": 0, "base_stats": {}, "affix_pool": "none", "temperable": False, "trade_rule": "free",
         "stack_limit": 50, "icon_asset_id": "icon_mat_carne_maior", "model_asset_id": "mat_ash",
         "summary": "Carne branca e doce da pinça grande. Na panela do Porto vira espetinho.", "price": 18,
         "source": "Caranguejo-Mãe do Cal (sempre) e Caranguejos de Cal (às vezes)."}
D("items_leva1.json", [carne])
D("recipes_leva1.json", [{"id": "rec_espetinho_caranguejo", "name": "Espetinho de Caranguejo", "station": "panela",
                          "inputs": [{"item": "mat_carne_maior", "qty": 2}], "output": "consumable_c02_tonico_medio", "output_qty": 1, "min_level": 3}])
loot = L("loot.json")
base_l = next(t for t in loot if t["id"] == "loot_caranguejo")
base_l["entries"] = [e for e in base_l["entries"] if e.get("item") != "mat_carne_maior"] + [{"chance": 0.15, "item": "mat_carne_maior"}]
D("loot.json", loot)
D("loot_leva1.json", [{"id": "loot_caranguejo_mae", "entries": [{"chance": 1.0, "item": "mat_carne_maior"}, {"chance": 1.0, "item": "mat_carne_maior"},
    {"chance": 0.6, "item": "mat_carne_maior"}] + [e for e in base_l["entries"] if e.get("item") != "mat_carne_maior"]}])
mm = L("monsters_leva1.json"); mm[0]["loot_table_id"] = "loot_caranguejo_mae"; D("monsters_leva1.json", mm)
print("carne maior: Caranguejo-Mãe (2–3) e Caranguejo de Cal (15%)")

# ---------------- 13/11 ("a Inês abre um monte de info e a missão fica em último; põe isso DENTRO da Biblioteca do Cais,
# em livros ou na bibliotecária atrás do balcão — a do Cais é mais simples, menorzinha"):
# Inês vira só a guardiã (missões da Crônica em primeiro); a lista de criaturas vai para o Registro do Cais (estante que
# se consulta com F), e as histórias do Porto viram 3 livros nos atris. Balcão, estantes e velas do pacote de DG.
porto = L(ARQ["porto_das_cinzas"])
ines = next(n for n in porto["npcs"] if n["id"] == "bibliotecaria_ines")
ines["role"] = "missoes"
ines["greeting"] = "«Bem-vindo à Biblioteca do Cais. Os registros das criaturas estão na estante; as histórias do Porto, nos atris. E a Crônica de KRYMOR… essa eu escrevo com você.»"
porto["npcs"] = [n for n in porto["npcs"] if n["id"] != "registro_cais"]
porto["props"] = [p for p in porto["props"] if p.get("tag") != "biblioteca_cais"]
porto["objects"] = [o for o in porto["objects"] if not o["id"].startswith("livro_cais_")]
ix, iy = ines["x"], ines["y"]
def pp(kind, tx, ty, yaw, r=0.9, **extra):
    x, y = perto(porto, tx, ty, r)
    porto["props"].append({"kind": kind, "x": x, "y": y, "yaw": yaw, "tag": "biblioteca_cais", **extra})
    return x, y
pp("movel_balcao", ix, iy - 1.3, 0, 0.6)
pp("glb:dg/dg_estante_livros", ix - 1.6, iy + 1.2, 180, 0.6)
pp("glb:dg/dg_candelabro", ix + 1.5, iy + 0.9, 0, 0.4)
pp("glb:dg/dg_pilha_livros", ix - 2.6, iy - 0.4, 30, 0.4)
rx, ry = perto(porto, ix + 2.4, iy + 1.0, 0.8)
porto["npcs"].append({"id": "registro_cais", "name": "Registro das Criaturas (Biblioteca do Cais)", "role": "biblioteca", "x": rx, "y": ry,
                      "facing": 180, "look": "quadro",
                      "greeting": "Fichas presas na estante, com a letra caprichada da Inês: as criaturas da Costa e das terras vizinhas, onde vivem e o que deixam cair."})
LIVROS = [
    ("livro_cais_naufragio", "O Naufrágio que Fundou o Porto",
     "O Porto das Cinzas nasceu de um naufrágio. Um navio de colonos bateu nas pedras de cal numa noite de cinza caindo. "
     "Quem sobreviveu usou as tábuas do casco para as primeiras casas — por isso as vigas do cais ainda têm pregos de navio."),
    ("livro_cais_farol", "O Farol que Apagou",
     "O farol a leste apagou numa noite sem vento. O faroleiro sumiu e ninguém sabe por quê. Há quem diga que, nas noites "
     "de neblina, o facho ainda gira sozinho por um instante — e que é melhor não estar na praia quando isso acontece."),
    ("livro_cais_triangulo", "O Triângulo onde as Bússolas Giram",
     "O barqueiro Ezequiel jura que existe um triângulo no mar onde as bússolas giram e os navios somem. Ele diz que os "
     "marinheiros jogam garrafas com mapas antes de desaparecer. Se você achar uma, leve a ele."),
]
for i, (oid, titulo, texto) in enumerate(LIVROS):
    lx, ly = perto(porto, ix - 3.0 - 1.4 * i, iy + 0.6, 0.6)
    porto["objects"].append({"id": oid, "kind": "livro", "x": lx, "y": ly, "label": titulo, "text": texto})
D(ARQ["porto_das_cinzas"], porto)
print("Biblioteca do Cais: Inês com missões, registro na estante e 3 livros")

# ---------------- 13/11 (vila): Quadro de Recados na praça do Porto (o servidor guarda os recados; /recado no chat)
porto = L(ARQ["porto_das_cinzas"])
porto["npcs"] = [n for n in porto["npcs"] if n["id"] != "quadro_recados"]
qx, qy = perto(porto, 2.0, 2.0, 0.8)
porto["npcs"].append({"id": "quadro_recados", "name": "Quadro de Recados", "role": "recados", "x": qx, "y": qy, "facing": 180, "look": "quadro",
                      "greeting": "Papéis presos com pregos, uns por cima dos outros. Recados de quem passou pelo Porto:"})
D(ARQ["porto_das_cinzas"], porto)
print("quadro de recados em", (qx, qy))

# ---------------- 13/11 (vila: "entrar nas casas pequenas"): interior simples (uma sala com lareira, mesa, cama e o
# morador). Portas com "porta:entra" levam para dentro (cópia pessoal); a saída devolve você à porta por onde entrou.
casa = {"id": "casa_pequena", "display_name": "Casa de Morador", "half_size": 7, "spawn_x": 0, "spawn_y": 3.5, "safe_radius": 20,
        "xp_multiplier": 1, "kind": "cidade", "region": "porto", "theme": "casarao", "instanced": True, "instance_scope": "personal",
        "no_mount": True, "exit_map": "porto_das_cinzas", "exit_x": 0, "exit_y": 10,
        "zones": [{"id": "casa", "name": "Casa de Morador", "kind": "seguro", "x": 0, "y": 0, "w": 12, "h": 12}],
        "obstacles": [{"kind": "muro", "shape": "box", "x": 0, "y": -6, "w": 12, "h": 1, "r": 0, "height": 3.6},
                      {"kind": "muro", "shape": "box", "x": 0, "y": 6, "w": 12, "h": 1, "r": 0, "height": 3.6},
                      {"kind": "muro", "shape": "box", "x": -6, "y": 0, "w": 1, "h": 12, "r": 0, "height": 3.6},
                      {"kind": "muro", "shape": "box", "x": 6, "y": 0, "w": 1, "h": 12, "r": 0, "height": 3.6}],
        "gates": [], "chests": [], "spawns": [], "waypoints": [], "checkpoints": [], "objects": [],
        "props": [{"kind": "movel_lareira", "x": 1.8, "y": -5.2, "yaw": 0}, {"kind": "movel_tapete", "x": 0, "y": -1.5, "yaw": 0},
                  {"kind": "movel_mesa", "x": 0, "y": -1.5, "yaw": 0}, {"kind": "movel_cadeira", "x": -1.2, "y": -1.5, "yaw": 90},
                  {"kind": "movel_cadeira", "x": 1.2, "y": -1.5, "yaw": -90}, {"kind": "movel_cama", "x": 4.4, "y": 2.8, "yaw": 0},
                  {"kind": "movel_prateleira", "x": -5.2, "y": -2.0, "yaw": 90}, {"kind": "movel_caixas", "x": -4.4, "y": 4.2, "yaw": 0},
                  {"kind": "movel_banqueta", "x": 3.0, "y": -3.6, "yaw": 0}, {"kind": "glb:dg/dg_candelabro", "x": -4.6, "y": -4.6, "yaw": 0},
                  {"kind": "glb:dg/dg_pilha_livros", "x": -5.0, "y": 0.8, "yaw": 30}],
        "portals": [{"id": "casa_pequena_saida", "x": 0, "y": 5.2, "radius": 1.2, "to_map": "porto_das_cinzas", "to_x": 0, "to_y": 10, "label": "Rua"}],
        "npcs": [{"id": "morador_casa", "name": "Dona Zuza, a Moradora", "role": "morador", "x": -1.6, "y": -3.4, "facing": 0, "look": "aldeao",
                  "greeting": "«Entra, entra, mas limpa o pé! Aqui no Porto a gente vive de pouco: um fogo, uma mesa e as histórias de quem passa. Senta um pouco, viajante.»"}]}
D("map_casa_pequena.json", casa)
w = L("world.json")
if "map_casa_pequena.json" not in w["map_files"]: w["map_files"].append("map_casa_pequena.json")
for r in w["regions"]:
    if r["id"] == "porto" and "casa_pequena" not in r["maps"]: r["maps"].append("casa_pequena")
D("world.json", w)
porto = L(ARQ["porto_das_cinzas"])
ABREM = {"porta_sobrado_0", "porta_casa_pequena_3", "porta_casa_pequena_4"}
for o in porto["objects"]:
    if o.get("kind") != "porta" or o.get("grant_flag") == "porta:brava": continue
    if o["id"] in ABREM:
        o["grant_flag"] = "porta:entra"
        o["text"] = "A porta range e abre. «Pode entrar!»"
    else:   # os sobrados do pescador continuam trancados (ele foi pescar)
        o.pop("grant_flag", None)
        o["text"] = "Trancada. Um bilhete: «Fui pescar. — T.»"
D(ARQ["porto_das_cinzas"], porto)
print("casas pequenas: interior pronto; portas que abrem:", sum(1 for o in porto["objects"] if o.get("grant_flag") == "porta:entra"))

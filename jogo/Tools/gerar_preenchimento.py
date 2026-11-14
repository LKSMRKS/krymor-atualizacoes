#!/usr/bin/env python3
"""
12/11 (auditoria da progressão — tarefas de preenchimento aprovadas pelo dono). Rodar por último (depois de gerar_reinos,
gerar_ramificacoes, gerar_cronica, gerar_travas, gerar_dificil). Idempotente.
• Reinos: ponto de viagem + 2 missões novas em cada; a Floresta Oculta ganha o chefe Saci-Rei.
• Cidade Celestial: "Portões do Céu" — bolsões de nível 50+ onde o Caminho da Serpente chega (quem vem voando no 48+ tem o que caçar).
• Ramificações: 2 missões paralelas em cada (caça da 2ª espécie + caça de elite).
• Dungeons de missão única (Galerias, Mercado, Salão, Fornalha): +1 missão de caça lá dentro.
• Ezequiel (barqueiro do Porto): a história secreta do Triângulo começa no 20 (garrafas com mapas na Mata e nos Cânions),
  passa pelo 33 (afogados da Véspera) e termina no 44 com o Ozório — o barco continua só no 44 (não quebra a progressão).
"""
import json, os, glob, math, random
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
def L(n): return json.load(open(os.path.join(C, n), encoding="utf-8"))
def D(n, o): json.dump(o, open(os.path.join(C, n), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: os.path.basename(f) for f in glob.glob(os.path.join(C, "map_*.json"))}
mons = {}
for f in glob.glob(os.path.join(C, "monsters*.json")):
    if not f.endswith("monsters_preenchimento.json"):
        for m in json.load(open(f, encoding="utf-8")): mons[m["id"]] = m
xp = lambda l: int(l * l * 70)

def livre(m, x, y, r=1.5):
    if abs(x) > m["half_size"] - 4 or abs(y) > m["half_size"] - 4: return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r: return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r: return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 1.6 + r for p in m["props"]): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 4 for p in m["portals"]): return False
    return True

def perto(m, tx, ty, r=1.5, longe_spawn=0):
    for raio in range(0, 30):
        for k in range(0, 360, 20):
            x, y = round(tx + raio * math.cos(math.radians(k)), 1), round(ty + raio * math.sin(math.radians(k)), 1)
            if livre(m, x, y, r) and all(math.hypot(x - s["x"], y - s["y"]) >= longe_spawn for s in m["spawns"]): return x, y
    raise SystemExit(f"sem lugar em {m['id']} perto de {tx},{ty}")

Q = []
def add(**k): Q.append(k)

# ---------------- monstros novos: Saci-Rei e o povo dos Portões do Céu
saci = mons["monster_bst_saci"]
novos = [{**saci, "id": "monster_floresta_saci_rei", "species_name": "Saci-Rei", "level": 23, "boss": True, "elite": False,
          "health_mult": round(saci["health_mult"] * 7, 2), "damage_mult": round(saci["damage_mult"] * 1.25, 2), "xp_mult": 12.0,
          "radius": max(saci["radius"], 1.0), "coins_min": 23 * 30, "coins_max": 23 * 60}]
D("monsters_preenchimento.json", novos)

# ---------------- reinos
REINOS = {
  "reino_lua": ("selene_eremita", "monster_bst_lobisomem", "Lobisomens", "monster_lua_lobisomem_alfa", "Lobisomem Alfa", "A Lua Vermelha"),
  "reino_transilvania": ("igor_criado", "monster_bst_vampiro", "Vampiros", "monster_trans_conde", "O Conde", "Transilvânia"),
  "reino_inferno": ("caronte_barqueiro", "monster_bst_demonio", "Demônios da Forja", "monster_inferno_ceifador_rei", "O Rei Ceifador", "O Inferno"),
  "reino_celeste": ("serafina_porteira", "monster_bst_anjo", "Anjos do Juramento", "monster_ceu_demonio_invasor", "O Demônio Invasor", "A Cidade Celestial"),
  "floresta_oculta": ("curupira", "monster_bst_saci", "Sacis das Cinzas", "monster_floresta_saci_rei", "O Saci-Rei", "A Floresta Oculta"),
}
for rid, (npc, mon, plural, chefe, nchefe, nome) in REINOS.items():
    m = L(ARQ[rid])
    lv = 20 if rid == "floresta_oculta" else 35
    wx, wy = perto(m, 6, 44, 2.6)
    m["waypoints"] = [{"id": "wp_" + rid, "name": nome, "x": wx, "y": wy}]
    if rid == "floresta_oculta":
        m["spawns"] = [s for s in m["spawns"] if s["id"] != "floresta_saci_rei"]
        bx, by = perto(m, 0, -36, 1.2)
        m["spawns"].append({"id": "floresta_saci_rei", "monster": "monster_floresta_saci_rei", "x": bx, "y": by, "level": 23, "group": "floresta_saci_rei", "respawn_seconds": 600})
        m["zones"] = [z for z in m["zones"] if z["id"] != "covil"] + [{"id": "covil", "name": "Covil: O Saci-Rei", "kind": "chefe", "x": bx, "y": by, "w": 20, "h": 14}]
    if rid == "reino_celeste":
        # Portões do Céu: onde o Caminho da Serpente chega; bolsões 50+
        m["spawns"] = [s for s in m["spawns"] if not s["id"].startswith("portoes_")]
        rnd = random.Random("portoes")
        for i, (cx, cy, mm, lv2) in enumerate([(-34, -34, "monster_serp_anjo", 51), (34, -34, "monster_serp_centauro", 52), (-36, 30, "monster_serp_harpia", 50), (36, 30, "monster_serp_anjo", 52)]):
            for k in range(5):
                x, y = perto(m, cx + rnd.uniform(-5, 5), cy + rnd.uniform(-5, 5), 1.0)
                m["spawns"].append({"id": f"portoes_{i}_{k}", "monster": mm, "x": x, "y": y, "level": lv2 + rnd.randint(0, 1), "group": f"portoes_{i}_{k // 2}", "respawn_seconds": 45})
        m["zones"] = [z for z in m["zones"] if not z["id"].startswith("portoes")] + [
            {"id": f"portoes{i}", "name": "Portões do Céu (50+)", "kind": "medio", "x": cx, "y": cy, "w": 18, "h": 16}
            for i, (cx, cy) in enumerate([(-34, -34), (34, -34), (-36, 30), (36, 30)])]
    D(ARQ[rid], m)
    add(id=f"q_{rid}_caca", name=f"Limpeza: {plural}", giver=npc, kind="kill", targets=[mon], count=12, map=rid, min_level=lv + 1,
        description=f"«Doze {plural.lower()}, e este lugar respira de novo. Você veio de longe — aproveite a viagem.»",
        hint=f"{plural} em {nome}. Volte a quem pediu, na chegada.", reward_xp=xp(lv + 1), reward_coins=(lv + 1) * 40, category="regiao")
    add(id=f"q_{rid}_chefe", name=f"O Senhor de {nome}", giver=npc, kind="boss", targets=[chefe], count=1, map=rid, min_level=lv + 2,
        description=f"«{nchefe} manda aqui. Enquanto ele estiver de pé, ninguém sai deste lugar inteiro.»",
        hint=f"{nchefe} no covil, no fundo de {nome}.", reward_xp=xp(lv + 3) * 2, reward_coins=(lv + 3) * 70,
        reward_items=["consumable_c02_tonico_medio*5"], category="historia")
add(id="q_reino_celeste_portoes", name="Os Portões do Céu", giver="serafina_porteira", kind="kill", targets=["monster_serp_anjo"], count=10, map="reino_celeste", min_level=50,
    description="«Quem chega pela ponte das nuvens encontra os Portões tomados por anjos caídos. Dez deles — e os portões voltam a ser nossos.»",
    hint="Anjos Caídos (50+) nos cantos da Cidade Celestial.", reward_xp=xp(51), reward_coins=51 * 40, category="regiao")

# ---------------- ramificações: 2 paralelas cada
RAM = {
  "ruinas_automatos": ("engenheira_ada", "monster_auto_aranha", "Aranhas de Engrenagem", "monster_auto_guardiao", "Guardiões Autômatos", 41),
  "triangulo_demonio": ("vigia_marujo", "monster_tri_homem_peixe", "Homens-Peixe", "monster_tri_caranguejo", "Caranguejos do Triângulo", 46),
  "fundo_triangulo": ("sereia_lia", "monster_fundo_enguia", "Enguias Elétricas", "monster_fundo_sereia", "Sereias de Guerra", 50),
  "ilha_serpente": ("chefe_tribo", "monster_ilha_harpia", "Harpias da Ilha", "monster_ilha_centauro", "Xamãs da Ilha", 56),
  "pico_vento": ("eremita_vento", "monster_pico_harpia", "Harpias das Nevascas", "monster_pico_lobo", "Lobos do Vento", 47),
  "caminho_serpente": ("guardia_ponte", "monster_serp_anjo", "Anjos Caídos", "monster_serp_centauro", "Arautos do Trovão", 51),
}
for rid, (npc, m1, n1, m2, n2, lv) in RAM.items():
    add(id=f"q_{rid}_par1", name=f"Caça: {n1}", giver=npc, kind="kill", targets=[m1], count=10, map=rid, min_level=lv,
        description=f"«{n1} cercam o acampamento. Dez a menos, e eu durmo uma noite inteira.»", hint=f"{n1} em volta do acampamento.",
        reward_xp=xp(lv), reward_coins=lv * 30, category="regiao")
    add(id=f"q_{rid}_par2", name=f"Os Mais Fortes: {n2}", giver=npc, kind="kill", targets=[m2], count=8, map=rid, min_level=lv + 2,
        description=f"«Os {n2.lower()} são os que mais custam. Oito — e traga histórias.»", hint=f"{n2}, mais para o fundo do mapa.",
        reward_xp=xp(lv + 2), reward_coins=(lv + 2) * 35, reward_items=["consumable_c02_tonico_medio*3"], category="regiao")

# ---------------- dungeons de missão única
DUN = {
  "galerias_piedade": ("zelador_ascanio", "monster_canions_troll_carcereiro", "Trolls Carcereiros", 26),
  "mercado_mortos": ("guia_aurelio", "monster_vespera_necromante_mercado", "Necromantes do Mercado", 39),
  "salao_geada": ("artesa_ilse", "monster_geada_campeao_draugr", "Campeões Draugr", 46),
  "fornalha": ("mestre_forja", "monster_escoria_golem_fornalha", "Golens da Fornalha", 52),
}
for did, (npc, mon, nome, lv) in DUN.items():
    add(id=f"q_{did}_caca", name=f"Dentro da Dungeon: {nome}", giver=npc, kind="kill", targets=[mon], count=6, map=did, min_level=lv,
        description=f"«Lá embaixo, os {nome.lower()} guardam o caminho do chefe. Seis — e a próxima ida fica mais fácil.»",
        hint=f"{nome} dentro de {did.replace('_', ' ').title()} (dungeon).", reward_xp=xp(lv) * 2, reward_coins=lv * 45, category="regiao")

# ---------------- Ezequiel: a história secreta do Triângulo (20 → 33 → 44)
for mid in ("mata_dos_enforcados", "canions_suplicantes"):
    m = L(ARQ[mid])
    m["objects"] = [o for o in m["objects"] if not o["id"].startswith("garrafa_triangulo")]
    D(ARQ[mid], m)
gar = []
for mid, pts in [("mata_dos_enforcados", [(-50, 50), (55, -50)]), ("canions_suplicantes", [(-55, -55)])]:
    m = L(ARQ[mid])
    for (tx, ty) in pts:
        x, y = perto(m, tx, ty, 1.2, longe_spawn=6)
        oid = f"garrafa_triangulo_{len(gar) + 1}"
        m["objects"].append({"id": oid, "kind": "reliquia", "x": x, "y": y, "label": "Garrafa com mapa"})
        gar.append(oid)
    D(ARQ[mid], m)
add(id="q_ezequiel_1", name="Garrafas do Triângulo", giver="barqueiro_ezequiel", kind="interact", targets=gar, count=3, min_level=20,
    description="«Todo marinheiro que some no Triângulo joga uma garrafa antes. Três delas foram parar terra adentro — duas na Mata, uma nos Cânions. Traga o que estiver escrito.»",
    hint="Garrafas com mapa: duas nos cantos da Mata dos Enforcados, uma no canto dos Cânions. F em cada uma.", reward_xp=xp(20), reward_coins=600,
    reward_flags=["triangulo:mapa"], category="historia")
add(id="q_ezequiel_2", name="Os Que Afundaram", giver="barqueiro_ezequiel", kind="kill", targets=["monster_vespera_afogado"], count=10, min_level=34,
    requires="q_ezequiel_1", map="vespera",
    description="«Os mapas apontam para Véspera: os afogados de lá vieram do Triângulo. Dez deles carregam o rumo do naufrágio na pele.»",
    hint="Afogados em Véspera.", reward_xp=xp(35), reward_coins=1400, category="historia")
add(id="q_ezequiel_3", name="O Último Marujo", giver="barqueiro_ezequiel", kind="talk", targets=["vigia_marujo"], count=1, min_level=44,
    requires="q_ezequiel_2",
    description="«Agora você aguenta o mar de lá. Pegue o barco do Tibério e procure o Ozório — ele é o último que voltou do Triângulo.»",
    hint="Barco do Tibério (nível 44) até o Triângulo; fale com o Ozório no acampamento.", reward_xp=xp(44), reward_coins=3000,
    reward_flags=["triangulo:segredo"], category="historia")
D("quests_preenchimento.json", Q)
print("preenchimento:", len(Q), "missões; garrafas:", gar)

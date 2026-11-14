#!/usr/bin/env python3
"""
12/11 (pedido do dono: "as áreas têm que se conectar, com ramificações, encaixadas na progressão de nível — não chegar
antes da hora"): gera as regiões novas, cada uma ligada ao mapa da sua faixa por uma passagem com nível recomendado:

  Vale (11–16) ──► Monte do Prefeito (10–16): Prefeito, lojas de decoração; as missões dele liberam a CASA no 20.
  Véspera (34–44) ──► Ruínas dos Autômatos (38–46): robôs antigos (sentinelas, aranhas de engrenagem, guardiões, Centurião).
  Porto (barco, 44+) ──► Triângulo do Demônio (44–50) ──► Fundo do Triângulo (48–54, debaixo d'água: homens-peixe, tritões,
      sereias, Leviatã) ──► passagem secreta ──► Ilha da Serpente Marinha (54–60) ──► Porto.
  Coroa da Geada (42–56) ──► Pico do Vento (46–50) ──(só com ASAS)──► Caminho da Serpente (48–54, sobre as nuvens) ──►
      Cidade Celestial.

Cada região: chegada segura com pedra de viagem e morador(es), 4–5 bolsões de monstros da faixa (crescendo para o fundo),
chefe de campo no fim, achados de exploração e uma cadeia de missões. Monstros novos são derivados de espécies existentes
(mesmo visual/comportamento, nome e nível próprios). Idempotente; rodar depois dos outros geradores.
"""
import json, math, os, random, glob

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
H = 58


ARQ = {}
def arq(mid):
    if not ARQ:
        for f in glob.glob(os.path.join(C, "map_*.json")):
            ARQ[json.load(open(f, encoding="utf-8"))["id"]] = os.path.basename(f)
    return ARQ.get(mid, f"map_{mid}.json")


def L(n): return json.load(open(os.path.join(C, n), encoding="utf-8"))
def D(n, o): json.dump(o, open(os.path.join(C, n), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


# ---------------------------------------------------------------- monstros novos (derivados)
MON = {  # id: (base, nome, nível-padrão, chefe?)
    "monster_monte_cabra": ("monster_r1_javali_duna", "Cabra-da-Serra", 11, False),
    "monster_monte_gaviao": ("monster_canions_morcego", "Gavião-Pedreiro", 12, False),
    "monster_monte_ermitao": ("monster_r1_saqueador_da_vigilia", "Ermitão da Serra", 14, False),
    "monster_monte_urso": ("monster_canions_troll", "Urso das Pedras", 16, True),
    "monster_auto_sentinela": ("monster_r2_sentinela", "Esfera-Sentinela", 39, False),
    "monster_auto_aranha": ("monster_brejo_tecela", "Aranha de Engrenagem", 41, False),
    "monster_auto_guardiao": ("monster_acropole_guardiao_bronze", "Guardião Autômato", 44, False),
    "monster_auto_centuriao": ("monster_escoria_colosso_escoria", "Centurião a Vapor", 47, True),
    "monster_tri_afogado": ("monster_vespera_afogado", "Marujo Afogado", 45, False),
    "monster_tri_homem_peixe": ("monster_vespera_afogado", "Homem-Peixe", 47, False),
    "monster_tri_caranguejo": ("monster_r1_tirano_da_mare", "Caranguejo do Triângulo", 48, False),
    "monster_tri_capitao": ("monster_vespera_cavaleiro_sem_rosto", "Capitão do Navio Fantasma", 51, True),
    "monster_fundo_tritao": ("monster_vespera_afogado", "Tritão Guerreiro", 50, False),
    "monster_fundo_sereia": ("monster_acropole_harpia", "Sereia de Guerra", 51, False),
    "monster_fundo_enguia": ("monster_brejo_serpente", "Enguia Elétrica", 50, False),
    "monster_fundo_leviata": ("monster_escoria_colosso_escoria", "Leviatã das Profundezas", 55, True),
    "monster_ilha_guerreiro": ("monster_mata_saqueador_mata", "Guerreiro da Ilha", 55, False),
    "monster_ilha_harpia": ("monster_acropole_harpia", "Harpia da Ilha", 57, False),
    "monster_ilha_centauro": ("monster_acropole_centauro", "Xamã da Ilha", 58, False),
    "monster_ilha_serpente": ("monster_brejo_serpente", "Serpente Marinha", 61, True),
    "monster_pico_lobo": ("monster_geada_lobo_gelo", "Lobo do Vento", 46, False),
    "monster_pico_harpia": ("monster_acropole_harpia", "Harpia das Nevascas", 48, False),
    "monster_pico_gigante": ("monster_geada_gigante", "Gigante do Pico", 50, True),
    "monster_serp_harpia": ("monster_acropole_harpia", "Harpia das Nuvens", 49, False),
    "monster_serp_anjo": ("monster_bst_anjo", "Anjo Caído", 51, False),
    "monster_serp_centauro": ("monster_acropole_centauro", "Arauto do Trovão", 52, False),
    "monster_serp_serpente": ("monster_brejo_serpente", "Serpente Celeste", 56, True),
}

# ---------------------------------------------------------------- regiões
REG = [
  dict(id="monte_prefeito", nome="Monte do Prefeito", tema="vale", lv=(10, 16), de=("vale_das_cinzas", 40, 12), min=10,
       bolsoes=[("monster_monte_cabra", 6, (10, 12)), ("monster_monte_gaviao", 6, (11, 13)), ("monster_monte_cabra", 5, (12, 13)), ("monster_monte_ermitao", 6, (13, 15))],
       chefe="monster_monte_urso", obst=[("rocha", 1.2, 2.8), ("arvore", 0.7, 1.0)], props=["pinheiros", "cerca_quebrada", "placa"],
       volta=("vale_das_cinzas", None)),
  dict(id="ruinas_automatos", nome="Ruínas dos Autômatos", tema="canion", lv=(38, 47), de=("vespera", 60, 30), min=38,
       bolsoes=[("monster_auto_sentinela", 6, (38, 40)), ("monster_auto_aranha", 6, (40, 42)), ("monster_auto_sentinela", 5, (41, 43)), ("monster_auto_guardiao", 6, (43, 45))],
       chefe="monster_auto_centuriao", obst=[("coluna_quebrada", 0.7, 0.9), ("estatua_bronze", 0.9, 1.0), ("rocha", 1.2, 2.2)], props=["estandarte", "vagonete", "trilhos"],
       volta=("vespera", None)),
  dict(id="triangulo_demonio", nome="Triângulo do Demônio", tema="praia", lv=(44, 51), de=None, min=44,
       bolsoes=[("monster_tri_afogado", 6, (44, 46)), ("monster_tri_caranguejo", 5, (46, 48)), ("monster_tri_homem_peixe", 6, (46, 48)), ("monster_tri_afogado", 5, (48, 49))],
       chefe="monster_tri_capitao", obst=[("rocha", 1.2, 2.6), ("coluna_quebrada", 0.7, 0.9)], props=["barco_encalhado", "redes", "ossada", "barril"],
       volta=("porto_das_cinzas", (0, -24))),
  dict(id="fundo_triangulo", nome="Fundo do Triângulo", tema="praia", lv=(48, 55), de=("triangulo_demonio", 0, -44), min=48,
       bolsoes=[("monster_fundo_enguia", 6, (48, 50)), ("monster_fundo_tritao", 6, (50, 52)), ("monster_fundo_sereia", 6, (51, 53)), ("monster_fundo_tritao", 5, (52, 54))],
       chefe="monster_fundo_leviata", obst=[("rocha", 1.4, 3.0), ("coluna_quebrada", 0.7, 0.9)], props=["ossada", "barco_encalhado", "cranios"],
       volta=("triangulo_demonio", None)),
  dict(id="ilha_serpente", nome="Ilha da Serpente Marinha", tema="mata", lv=(54, 61), de=("fundo_triangulo", 0, -44), min=54,
       bolsoes=[("monster_ilha_guerreiro", 6, (54, 56)), ("monster_ilha_harpia", 6, (55, 57)), ("monster_ilha_centauro", 5, (57, 59)), ("monster_ilha_guerreiro", 5, (58, 59))],
       chefe="monster_ilha_serpente", obst=[("arvore", 0.8, 1.1), ("rocha", 1.2, 2.4)], props=["fogueira", "totem_goblin", "estacas", "barril"],
       volta=("porto_das_cinzas", (0, -24))),
  dict(id="pico_vento", nome="Pico do Vento", tema="neve", lv=(46, 51), de=("coroa_da_geada", -62, 30), min=46,
       bolsoes=[("monster_pico_lobo", 6, (46, 47)), ("monster_pico_harpia", 6, (47, 49)), ("monster_pico_lobo", 5, (48, 49)), ("monster_pico_harpia", 5, (49, 50))],
       chefe="monster_pico_gigante", obst=[("rocha", 1.4, 3.0), ("cristal_gelo", 0.8, 1.1)], props=["estandarte", "ossada"],
       volta=("coroa_da_geada", None)),
  dict(id="caminho_serpente", nome="Caminho da Serpente", tema="ceu", lv=(48, 56), de=("pico_vento", 0, -46), min=48, asas=True,
       bolsoes=[("monster_serp_harpia", 6, (48, 50)), ("monster_serp_anjo", 6, (50, 52)), ("monster_serp_centauro", 5, (51, 53)), ("monster_serp_anjo", 5, (53, 54))],
       chefe="monster_serp_serpente", obst=[("coluna_quebrada", 0.6, 0.8), ("estatua_bronze", 0.9, 1.0)], props=["estandarte", "coluna_quebrada"],
       volta=("pico_vento", None), fim=("reino_celeste", "Portões da Cidade Celestial")),
]

NPCS = {
  "monte_prefeito": [("prefeito_belmiro", "Belmiro, o Prefeito do Monte", "mentor", None, "Bem-vindo ao Monte! Aqui em cima a vista é boa e o terreno é barato — para quem merece."),
                     ("zuleica_decoracao", "Zuleica, Decorações Finas", "curandeira", "loja_decoracao_1", "Tapete, quadro, lustre: casa sem decoração é só um teto."),
                     ("ramires_moveis", "Ramires, Móveis e Curiosidades", "ferreiro", "loja_decoracao_2", "Estátua de leão? Aquário? Uma jukebox que toca sozinha? Tenho de tudo.")],
  "ruinas_automatos": [("engenheira_ada", "Ada, a Engenheira", "arcanista", None, "Essas máquinas não foram feitas por gente. Mas obedecem a quem entende as engrenagens.")],
  "triangulo_demonio": [("vigia_marujo", "Ozório, o Último Marujo", "aldeao", None, "Todo navio que entra aqui afunda. O meu afundou três vezes. Eu continuo voltando.")],
  "fundo_triangulo": [("sereia_lia", "Ondina das Profundezas", "curandeira", None, "Respire devagar, terrestre. Aqui embaixo o Leviatã escuta até o seu coração.")],
  "ilha_serpente": [("chefe_tribo", "Tupã-Iara, Chefe da Ilha", "mentor", None, "Estrangeiro que vem pelo fundo do mar é estrangeiro de coragem. A Serpente come os corajosos primeiro.")],
  "pico_vento": [("eremita_vento", "Bóreas, o Eremita do Vento", "aldeao", None, "Daqui de cima só se vai para as nuvens. Quem não tem asas, cai.")],
  "caminho_serpente": [("guardia_ponte", "Celina, Guardiã da Ponte de Nuvem", "curandeira", None, "Este caminho é o corpo de uma serpente antiga. Ande no meio — e não acorde a cabeça.")],
}


def circle(kind, x, y, r, h=4.0):
    return {"kind": kind, "shape": "circle", "x": round(x, 2), "y": round(y, 2), "w": 0, "h": 0, "r": round(r, 2), "height": h}


def longe(x, y, pts, d):
    return all((x - a) ** 2 + (y - b) ** 2 >= d * d for a, b in pts)


def montar(r):
    rnd = random.Random(r["id"])
    zones = [{"id": "chegada", "name": "Acampamento", "kind": "seguro", "x": 0, "y": 44, "w": 18, "h": 12},
             {"id": "trilha", "name": "Trilha", "kind": "caminho", "x": 0, "y": 6, "w": 5, "h": 72}]
    keep = [(0, 44), (0, 50), (0, -44), (0, 40)]
    bolsoes_xy = [(-30, 24), (30, 22), (-32, -10), (32, -12)]
    spawns = []
    i = 0
    for (mon, n, (lo, hi)), (bx, by) in zip(r["bolsoes"], bolsoes_xy):
        zones.append({"id": f"bolsao{i}", "name": "Território: " + MON[mon][1], "kind": "medio", "x": bx, "y": by, "w": 22, "h": 20})
        for k in range(n):
            x, y = bx + rnd.uniform(-8, 8), by + rnd.uniform(-7, 7)
            spawns.append({"id": f"{r['id']}_{i}_{k}", "monster": mon, "x": round(x, 1), "y": round(y, 1), "level": rnd.randint(lo, hi),
                           "group": f"{r['id']}_g{i}_{k // 2}", "respawn_seconds": 45})
            keep.append((x, y))
        i += 1
    chefe = r["chefe"]
    zones.append({"id": "covil", "name": "Covil: " + MON[chefe][1], "kind": "chefe", "x": 0, "y": -30, "w": 22, "h": 14})
    spawns.append({"id": f"{r['id']}_chefe", "monster": chefe, "x": 0, "y": -30, "level": MON[chefe][2], "group": f"{r['id']}_chefe", "respawn_seconds": 720})
    keep.append((0, -30))
    obst = []
    tries = 0
    while len(obst) < 46 and tries < 4000:
        tries += 1
        x, y = rnd.uniform(-H + 5, H - 5), rnd.uniform(-H + 5, H - 5)
        if abs(x) < 6 or (abs(x) < 12 and y > 36) or (abs(x) < 14 and abs(y + 30) < 10) or (abs(x) < 8 and y < -38):
            continue
        if not longe(x, y, keep, 5) or not longe(x, y, [(o["x"], o["y"]) for o in obst], 4.5):
            continue
        k, r0, r1 = r["obst"][rnd.randrange(len(r["obst"]))]
        obst.append(circle(k, x, y, rnd.uniform(r0, r1)))
    for t in range(-H + 3, H - 2, 7):
        for (x, y) in [(t, -H + 2), (t, H - 2), (-H + 2, t), (H - 2, t)]:
            k, r0, _ = r["obst"][0]
            obst.append(circle(k, x + rnd.uniform(-1, 1), y + rnd.uniform(-1, 1), max(r0, 1.0)))
    props = [{"kind": "fogueira", "x": 0, "y": 40, "yaw": 0}, {"kind": "tocha", "x": -6, "y": 47, "yaw": 0}, {"kind": "tocha", "x": 6, "y": 47, "yaw": 0}]
    for j in range(18):
        x, y = rnd.uniform(-H + 8, H - 8), rnd.uniform(-H + 8, H - 8)
        if abs(x) < 6 or not longe(x, y, keep + [(o["x"], o["y"]) for o in obst], 3.5):
            continue
        props.append({"kind": r["props"][j % len(r["props"])], "x": round(x, 1), "y": round(y, 1), "yaw": rnd.randrange(360)})
    # achados de exploração (3), nos cantos
    objects = []
    for j, (tx, ty) in enumerate([(-44, 40), (44, 2), (-44, -40)]):
        for _ in range(200):
            x, y = tx + rnd.uniform(-6, 6), ty + rnd.uniform(-6, 6)
            if longe(x, y, [(o["x"], o["y"]) for o in obst], 3) and longe(x, y, keep, 4):
                objects.append({"id": f"{r['id']}_achado_{j + 1}", "kind": "reliquia", "x": round(x, 1), "y": round(y, 1), "label": ACHADO[r["id"]]})
                break
    portals = []
    vm, vxy = r["volta"]
    portals.append({"id": f"{r['id']}_volta", "x": 0, "y": 53, "radius": 2.0, "to_map": vm, "to_x": 0.0, "to_y": 0.0, "label": "Voltar"})
    if "fim" in r:
        fm, fl = r["fim"]
        portals.append({"id": f"{r['id']}_fim", "x": 0, "y": -50, "radius": 2.0, "to_map": fm, "to_x": 0.0, "to_y": 42.0, "label": fl})
    npcs = []
    for k, (nid, nome, look, shop, gr) in enumerate(NPCS.get(r["id"], [])):
        n = {"id": nid, "name": nome, "role": "missoes", "x": [-6.0, 6.0, 0.0][k % 3], "y": [40.0, 40.0, 46.0][k % 3], "facing": 180, "look": look, "greeting": gr}
        if shop: n["shop"] = shop
        npcs.append(n)
    region = {"monte_prefeito": "vale", "ruinas_automatos": "vespera", "triangulo_demonio": "mar", "fundo_triangulo": "mar", "ilha_serpente": "mar",
              "pico_vento": "geada", "caminho_serpente": "ceus"}[r["id"]]
    return {
        "id": r["id"], "display_name": r["nome"], "half_size": H, "spawn_x": 0, "spawn_y": 46, "safe_radius": 8,
        "xp_multiplier": 1.1, "kind": "campo", "region": region, "theme": r["tema"],
        "zones": zones, "obstacles": obst, "gates": [], "chests": [], "spawns": spawns, "props": props, "portals": portals,
        "waypoints": [{"id": "wp_" + r["id"], "name": r["nome"], "x": 0, "y": 49}],
        "npcs": npcs, "objects": objects, "checkpoints": [],
    }


ACHADO = {"monte_prefeito": "Marco de divisa", "ruinas_automatos": "Engrenagem-mestra", "triangulo_demonio": "Diário de bordo encharcado",
          "fundo_triangulo": "Concha que canta", "ilha_serpente": "Escama de serpente", "pico_vento": "Pena de grifo", "caminho_serpente": "Pluma dourada"}

# ---------------------------------------------------------------- missões
def missoes():
    q = []
    def add(**k): q.append(k)
    xp = lambda l: int(l * l * 70)
    # Monte do Prefeito → casa
    add(id="q_pref_1", name="As Cabras do Prefeito", giver="prefeito_belmiro", kind="kill", targets=["monster_monte_cabra"], count=8, map="monte_prefeito", min_level=10,
        description="«As cabras-da-serra comeram o jardim da prefeitura. Oito a menos e eu começo a gostar de você.»", hint="Cabras-da-serra no Monte do Prefeito. Volte ao Belmiro.",
        reward_xp=xp(10), reward_coins=200, category="historia")
    add(id="q_pref_2", name="Os Marcos de Divisa", giver="prefeito_belmiro", kind="interact", targets=[f"monte_prefeito_achado_{i}" for i in (1, 2, 3)], count=3, map="monte_prefeito",
        min_level=12, requires="q_pref_1", description="«Para vender um terreno, preciso saber onde ele termina. Ache os três marcos de divisa do Monte.»",
        hint="Três marcos de divisa nos cantos do Monte. Aperte F em cada um.", reward_xp=xp(12), reward_coins=300, category="historia")
    add(id="q_pref_3", name="Ermitões Grileiros", giver="prefeito_belmiro", kind="kill", targets=["monster_monte_ermitao"], count=6, map="monte_prefeito", min_level=14, requires="q_pref_2",
        description="«Os ermitões da serra arrancaram os marcos e dizem que o terreno é deles. Seis argumentos contrários, por favor.»", hint="Ermitões da Serra (14–15). Volte ao Belmiro.",
        reward_xp=xp(14), reward_coins=400, category="historia")
    add(id="q_pref_4", name="O Urso das Pedras", giver="prefeito_belmiro", kind="boss", targets=["monster_monte_urso"], count=1, map="monte_prefeito", min_level=15, requires="q_pref_3",
        description="«Último problema: um urso do tamanho de uma casa dorme bem onde eu queria vender casas. Acorde-o — e não deixe ele acordar de novo.»",
        hint="O Urso das Pedras (16) no covil do Monte.", reward_xp=xp(16) * 2, reward_coins=800, category="historia")
    add(id="q_pref_casa", name="A Escritura", giver="prefeito_belmiro", kind="talk", targets=["zuleica_decoracao"], count=1, map="monte_prefeito", min_level=15, requires="q_pref_4",
        cost_coins=15000, description="«Você limpou o Monte; eu cumpro a palavra. Por 15 mil moedas, a casa do Porto (a porta atrás da praça) é sua — e lá você planta, guarda, decora e recebe o grupo. Pegue as chaves com a Zuleica.»",
        hint="Aceite com o Belmiro (paga 15.000 moedas, nível 15) e pegue as chaves com a Zuleica, ao lado dele.", reward_xp=0, reward_coins=0, reward_flags=["casa:propria"], forbid_flag="casa:propria", category="casa")
    # demais regiões: caça → exploração → chefe
    cad = {
      "ruinas_automatos": ("engenheira_ada", "monster_auto_sentinela", 39, "Sentinelas Acordadas", "«As esferas-sentinela acordaram e varrem as ruínas com raios. Desligue oito — de qualquer jeito.»",
                           "As Engrenagens-Mestras", "«Três engrenagens-mestras controlam as máquinas. Tire as três do lugar e elas param de se consertar.»", "O Centurião a Vapor"),
      "triangulo_demonio": ("vigia_marujo", "monster_tri_afogado", 45, "Marujos Que Não Descansam", "«Meus colegas afogados ainda tentam embarcar. Mande oito de volta para o fundo.»",
                            "Os Diários de Bordo", "«Três diários de bordo contam onde fica a passagem para o fundo. Recolha os três.»", "O Capitão do Navio Fantasma"),
      "fundo_triangulo": ("sereia_lia", "monster_fundo_tritao", 50, "Lanças de Tritão", "«Os tritões guardam a passagem para a ilha. Oito deles, e eu abro o caminho.»",
                          "As Conchas que Cantam", "«Três conchas cantam o caminho até a ilha. Encontre as três e escute.»", "O Leviatã das Profundezas"),
      "ilha_serpente": ("chefe_tribo", "monster_ilha_guerreiro", 55, "Prova de Coragem", "«Oito dos meus guerreiros querem testar você. Não precisa matar — mas eles não vão facilitar.»",
                        "Escamas da Serpente", "«Três escamas da Serpente Marinha estão pela ilha. Traga as três e eu mostro onde ela dorme.»", "A Serpente Marinha"),
      "pico_vento": ("eremita_vento", "monster_pico_lobo", 46, "Lobos do Vento", "«Os lobos do vento uivam a noite inteira. Oito, e eu durmo.»",
                     "Penas de Grifo", "«Três penas de grifo, presas nas pedras. Com elas eu teço um amuleto que te leva às nuvens — se você tiver asas.»", "O Gigante do Pico"),
      "caminho_serpente": ("guardia_ponte", "monster_serp_harpia", 49, "Harpias na Ponte", "«As harpias derrubam quem atravessa. Oito delas a menos e a ponte volta a ser ponte.»",
                           "As Plumas Douradas", "«Três plumas douradas marcam o caminho da cabeça da serpente. Ache as três.»", "A Serpente Celeste"),
    }
    for rid, (giver, alvo, lv, t1, d1, t2, d2, t3) in cad.items():
        r = next(x for x in REG if x["id"] == rid)
        chefe = r["chefe"]
        q1, q2, q3 = f"q_{rid}_1", f"q_{rid}_2", f"q_{rid}_3"
        add(id=q1, name=t1, giver=giver, kind="kill", targets=[alvo], count=8, map=rid, min_level=lv, description=d1,
            hint=f"No(a) {r['nome']}. Volte a {giver.split('_')[0].title()}, no acampamento.", reward_xp=xp(lv), reward_coins=lv * 30, category="historia")
        add(id=q2, name=t2, giver=giver, kind="interact", targets=[f"{rid}_achado_{i}" for i in (1, 2, 3)], count=3, map=rid, min_level=lv + 1, requires=q1,
            description=d2, hint=f"Três achados nos cantos de {r['nome']}. Aperte F em cada um.", reward_xp=xp(lv + 1), reward_coins=lv * 35, category="historia")
        add(id=q3, name=t3, giver=giver, kind="boss", targets=[chefe], count=1, map=rid, min_level=MON[chefe][2] - 1, requires=q2,
            description=f"«Agora o pior: {MON[chefe][1]}. Leve tônicos — e amigos, se tiver.»", hint=f"{MON[chefe][1]} no covil de {r['nome']}.",
            reward_xp=xp(MON[chefe][2]) * 2, reward_coins=MON[chefe][2] * 70, reward_items=["consumable_c02_tonico_medio*5"], category="historia")
    return q


def decoracoes():
    base = {"heal_percent_max_health": 0, "heal_duration_ms": 0, "cooldown_group": "decor", "cooldown_ms": 1000, "stack_limit": 1}
    lst = [("decor_tapete_real", "Tapete Real", 1500, 10), ("decor_quadro_mar", "Quadro do Mar Bravo", 2500, 12), ("decor_lustre", "Lustre de Cristal", 6000, 18),
           ("decor_aquario", "Aquário com Peixe-Lanterna", 9000, 22), ("decor_estatua_leao", "Estátua de Leão Dourado", 15000, 28),
           ("decor_jukebox", "Jukebox de Corda", 12000, 20), ("decor_trofeu", "Troféu de Chefe Empalhado", 25000, 35), ("decor_trono", "Trono de Carvalho", 40000, 45)]
    out = [{"id": i, "display_name": n, "price": p, "decor_points": pts, **base,
            "summary": f"Decoração da casa (+{pts} de conforto). Use DENTRO da sua casa para colocar."} for i, n, p, pts in lst]
    for k, (nome, pw, arm, hp, rg) in enumerate([("Conforto do Lar", 0.03, 20, 40, 0.1), ("Conforto do Lar II", 0.05, 50, 90, 0.15),
                                                  ("Conforto do Lar III", 0.07, 90, 160, 0.2), ("Conforto do Lar IV", 0.09, 140, 240, 0.25)], 1):
        out.append({"id": f"buff_casa_{k}", "display_name": nome, "price": 0, **{**base, "cooldown_group": "buff_casa"},
                    "buff": "casa", "buff_ms": 3600000, "buff_power_pct": pw, "buff_armor": arm, "buff_max_health": hp, "buff_regen_pct": rg,
                    "summary": f"Saiu de uma casa bem decorada: +{pw*100:.0f}% de poder, +{arm} de armadura, +{hp} de vida e regeneração (1 h)."})
    return out


def main():
    mons = {}
    for f in glob.glob(os.path.join(C, "monsters*.json")):
        if not f.endswith("monsters_ramificacoes.json"):
            for m in json.load(open(f, encoding="utf-8")):
                mons[m["id"]] = m
    COMUM_DE_ESPECIAL = {"monster_tri_caranguejo": "loot_vespera_afogado", "monster_auto_sentinela": "loot_acropole_guardiao_bronze"}
    novos = []
    for mid, (base, nome, lvl, boss) in MON.items():
        b = mons[base]
        d = {**b, "id": mid, "species_name": nome, "level": lvl}
        d.pop("rare", None)
        if boss:
            d.update(boss=True, elite=False, health_mult=round(b["health_mult"] * (1.6 if b.get("boss") else 7), 2),
                     damage_mult=round(b["damage_mult"] * 1.25, 2), xp_mult=14.0, radius=max(b["radius"], 1.3), coins_min=lvl * 30, coins_max=lvl * 60)
        else:
            d.update(boss=False, elite=False, xp_mult=round(max(1.0, b.get("xp_mult", 1)), 2), coins_min=lvl * 2, coins_max=lvl * 4)
            # 14/11 (auditoria do trabalhador, problema 1): comum copiado de raro/elite/chefe herdava a tabela de saque e a XP dele
            if b.get("boss") or b.get("elite") or b.get("rare") or b.get("xp_mult", 1) > 1.5:
                d.update(xp_mult=1.0, health_mult=round(min(b["health_mult"], 1.5), 2))
            if mid in COMUM_DE_ESPECIAL: d["loot_table_id"] = COMUM_DE_ESPECIAL[mid]
        if mid == "monster_monte_cabra": d["visual_asset_id"] = "goat"   # 13/11: "a Cabra-da-Serra está como javali"
        if mid == "monster_monte_cabra": d["loot_table_id"] = "loot_monte_cabra"   # 14/11: "cabra dá carne de javali": chifre + couro
        if mid == "monster_monte_urso": d["behavior_id"] = "stone_bear"   # 13/11: pulo para a frente + arremesso de pedra legível
        novos.append(d)
    D("monsters_ramificacoes.json", novos)

    world = L("world.json")
    regs = {"monte_prefeito": "vale", "ruinas_automatos": "vespera", "pico_vento": "geada"}
    for r in REG:
        m = montar(r)
        D(f"map_{r['id']}.json", m)
        if f"map_{r['id']}.json" not in world["map_files"]:
            world["map_files"].append(f"map_{r['id']}.json")
        # passagem de quem chega (no mapa de origem) e a de volta
        if r["de"]:
            om, ox, oy = r["de"]
            o = L(arq(om))
            o["portals"] = [p for p in o["portals"] if p["id"] != f"{om}_para_{r['id']}"]
            px, py = livre_perto(o, ox, oy)
            o["portals"].append({"id": f"{om}_para_{r['id']}", "x": px, "y": py, "radius": 2.0, "to_map": r["id"], "to_x": 0.0, "to_y": 46.0,
                                 "label": r["nome"] + ("" if not r.get("asas") else " (só com asas)"), "min_level": r["min"], "level_lock": True,
                                 **({"requires_flag": "asas"} if r.get("asas") else {})})
            D(arq(om), o)
            for p in m["portals"]:
                if p["id"] == f"{r['id']}_volta":
                    p["to_x"], p["to_y"] = chegada_ao_lado(o, px, py)
        else:
            for p in m["portals"]:
                if p["id"] == f"{r['id']}_volta" and r["volta"][1]:
                    p["to_x"], p["to_y"] = r["volta"][1]
        if r["volta"][1] and r["de"] is None:
            pass
        D(f"map_{r['id']}.json", m)
    # Ilha volta ao Porto; Fundo/Ilha/Triângulo: volta do triângulo é ao Porto
    for rid in ("ilha_serpente", "triangulo_demonio"):
        m = L(f"map_{rid}.json")
        for p in m["portals"]:
            if p["id"] == f"{rid}_volta":
                p["to_map"], p["to_x"], p["to_y"] = "porto_das_cinzas", 0.0, -24.0
                p["label"] = "Barco de volta ao Porto"
        D(f"map_{rid}.json", m)
    # regiões novas no mapa do mundo
    novas_reg = [
        ("monte", "Monte do Prefeito", "regiao", 62, 42, "10–16", ["monte_prefeito"], "Ramificação do Vale: o Prefeito Belmiro, lojas de decoração e a escritura da sua casa."),
        ("automatos", "Ruínas dos Autômatos", "regiao", -75, 10, "38–47", ["ruinas_automatos"], "Ramificação da Véspera: máquinas antigas que ainda obedecem a ordens esquecidas."),
        ("mar", "Triângulo do Demônio", "regiao", 45, 85, "44–61", ["triangulo_demonio", "fundo_triangulo", "ilha_serpente"], "De barco pelo Porto (44+): navios afundados, o fundo do mar e a Ilha da Serpente."),
        ("ceus", "Pico do Vento e Caminho da Serpente", "regiao", -85, -68, "46–56", ["pico_vento", "caminho_serpente"], "Do alto da Coroa da Geada às nuvens — só com asas — até a Cidade Celestial."),
    ]
    for rid, nome, kind, x, y, lv, maps, desc in novas_reg:
        if not any(r["id"] == rid for r in world["regions"]):
            world["regions"].append({"id": rid, "name": nome, "kind": kind, "x": x, "y": y, "levels": lv, "maps": maps, "description": desc})
    D("world.json", world)
    D("quests_ramificacoes.json", missoes())
    D("consumables_decoracao.json", decoracoes())
    shops = L("shops.json")
    for sid, nome, itens in [("loja_decoracao_1", "Zuleica, Decorações Finas", ["decor_tapete_real", "decor_quadro_mar", "decor_lustre", "decor_aquario"]),
                             ("loja_decoracao_2", "Ramires, Móveis e Curiosidades", ["decor_estatua_leao", "decor_jukebox", "decor_trofeu", "decor_trono"])]:
        shops = [s for s in shops if s["id"] != sid] + [{"id": sid, "name": nome, "items": itens}]
    D("shops.json", shops)
    # barco do Tibério para o Triângulo (nível 44: só com o mapa de Véspera descoberto)
    porto = L("map_porto.json")
    world["links"] = [l for l in world["links"] if l["to"] not in ("monte", "automatos", "mar", "ceus")] + [
        {"from": "vale", "to": "monte"}, {"from": "vespera", "to": "automatos"}, {"from": "porto", "to": "mar"}, {"from": "geada", "to": "ceus"}]
    D("world.json", world)
    for n in porto["npcs"]:
        if n["id"] == "pescador_tiberio":
            n["travel"] = [t for t in n.get("travel", []) if t["id"] != "barco_triangulo"] + [
                {"id": "barco_triangulo", "label": "Barco até o Triângulo do Demônio (nível 44+)", "to_map": "triangulo_demonio", "to_x": 0.0, "to_y": 46.0,
                 "price": 2000, "min_level": 44}]
    D("map_porto.json", porto)
    print("ramificações:", [r["id"] for r in REG])


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
        if math.hypot(x - p["x"], y - p["y"]) < 1.6 + r:
            return False
    return True


def livre_perto(m, tx, ty):
    for raio in range(0, 50):
        for k in range(0, 360, 10):
            x, y = round(tx + raio * math.cos(math.radians(k)), 1), round(ty + raio * math.sin(math.radians(k)), 1)
            if not livre(m, x, y, 2.6):
                continue
            if any(math.hypot(x - s["x"], y - s["y"]) < 13 for s in m["spawns"]):
                continue
            if any(math.hypot(x - p["x"], y - p["y"]) < 8 for p in m["portals"]):
                continue
            cx, cy = chegada_ao_lado(m, x, y)
            if livre(m, cx, cy, 1.2):
                return x, y
    raise SystemExit("sem lugar para passagem perto de %s,%s" % (tx, ty))


def chegada_ao_lado(m, x, y):
    # chega 3,5 m para o lado do centro do mapa (fora do círculo da passagem)
    d = math.hypot(x, y) or 1
    return round(x - 3.5 * x / d, 1), round(y - 3.5 * y / d, 1)


if __name__ == "__main__":
    main()

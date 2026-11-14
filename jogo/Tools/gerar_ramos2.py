#!/usr/bin/env python3
"""
14/11 (dono: "o jogo está muito linear — mais ramificações"; "dois monstros ultra gigantes, impossíveis de matar, para a galera
ficar tentando — um dia, talvez, com itens e atualizações — em dois lugares ramificados, nas extremidades"):

  Mata dos Enforcados (17–20) ──► Bosque das Corujas (19–24) ──► atalho para os Cânions (sai na outra ponta)
  Cânions dos Suplicantes (20–30) ──► Garganta do Colosso (30–36) ··· no fundo: GORATH, O COLOSSO QUE NÃO CAI (titã)
  Mar de Escória (48–56) ──► Caldeira do Titã (54–60) ··· no fundo: IGNAROTH, O TITÃ DE MAGMA (titã)

Os titãs: nível 99, gigantes (13–14 m), vida absurda (dezenas de milhões) e golpes que derrubam qualquer um. Não dão XP; quem
um dia derrubar ganha a marca "tita_vencido:<id>" (e o nome fica no placar). Mesma estrutura das ramificações do 12/11
(Tools/gerar_ramificacoes.py: acampamento seguro, 4 bolsões, chefe, achados e missões) — reaproveita o montador de lá.
Idempotente. Rodar depois do gerar_ramificacoes.py.
"""
import json, os, glob

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ns = {"__name__": "ramos", "__file__": os.path.join(RAIZ, "Tools", "gerar_ramificacoes.py")}
src = open(os.path.join(RAIZ, "Tools", "gerar_ramificacoes.py"), encoding="utf-8").read()
src = src.replace('''    region = {"monte_prefeito": "vale",''', '''    region = r.get("regiao") or {"monte_prefeito": "vale",''')
exec(compile(src, "gerar_ramificacoes.py", "exec"), ns)
L, D, montar, livre_perto, chegada_ao_lado, arq = ns["L"], ns["D"], ns["montar"], ns["livre_perto"], ns["chegada_ao_lado"], ns["arq"]

MON = {
    "monster_bosque_coruja": ("monster_canions_morcego", "Coruja-Fantasma", 19, False),
    "monster_bosque_lobo": ("monster_mata_lobo_cinzento", "Lobo das Sombras", 20, False),
    "monster_bosque_saci": ("monster_bst_saci", "Saci do Bosque", 21, False),
    "monster_bosque_raiz": ("monster_mata_raiz_andante", "Raiz que Anda", 22, False),
    "monster_bosque_corujao": ("monster_acropole_harpia", "O Corujão de Mil Olhos", 24, True),
    "monster_garganta_escorpiao": ("monster_canions_escorpiao", "Escorpião da Garganta", 30, False),
    "monster_garganta_morcego": ("monster_canions_morcego_albino", "Morcego Albino", 31, False),
    "monster_garganta_troll": ("monster_canions_troll", "Troll das Correntes", 33, False),
    "monster_garganta_espectro": ("monster_canions_espectro", "Espectro do Colosso", 34, False),
    "monster_garganta_carcereiro": ("monster_canions_carcereiro_almas", "Carcereiro do Colosso", 36, True),
    "monster_caldeira_salamandra": ("monster_escoria_salamandra", "Salamandra da Caldeira", 54, False),
    "monster_caldeira_elemental": ("monster_escoria_elemental_fornalha", "Elemental da Caldeira", 55, False),
    "monster_caldeira_golem": ("monster_escoria_golem_fornalha", "Golem de Basalto Vivo", 57, False),
    "monster_caldeira_besouro": ("monster_escoria_besouro_brasa", "Besouro de Magma", 56, False),
    "monster_caldeira_guardiao": ("monster_escoria_coracao_escoria", "O Guardião da Caldeira", 60, True),
}
ns["MON"].update(MON)

REG = [
    dict(id="bosque_corujas", nome="Bosque das Corujas", tema="mata", lv=(19, 24), de=("mata_dos_enforcados", 60, 20), min=18, regiao="mata",
         bolsoes=[("monster_bosque_coruja", 6, (19, 20)), ("monster_bosque_lobo", 6, (20, 21)), ("monster_bosque_saci", 5, (21, 22)), ("monster_bosque_raiz", 6, (22, 23))],
         chefe="monster_bosque_corujao", obst=[("arvore", 0.8, 1.2), ("rocha", 1.0, 2.0)], props=["pinheiros", "ossada", "estacas"],
         volta=("mata_dos_enforcados", None), fim=("canions_suplicantes", "Trilha das corujas (atalho para os Cânions)")),
    dict(id="garganta_colosso", nome="Garganta do Colosso", tema="canion", lv=(30, 36), de=("canions_suplicantes", 60, 25), min=29, regiao="canions",
         bolsoes=[("monster_garganta_escorpiao", 6, (30, 31)), ("monster_garganta_morcego", 6, (31, 32)), ("monster_garganta_troll", 5, (32, 34)), ("monster_garganta_espectro", 6, (34, 35))],
         chefe="monster_garganta_carcereiro", obst=[("rocha", 1.4, 3.0), ("coluna_quebrada", 0.7, 0.9)], props=["vagonete", "trilhos", "ossada", "estandarte"],
         volta=("canions_suplicantes", None), tita=("monster_tita_colosso", "Leito do Colosso")),
    dict(id="caldeira_tita", nome="Caldeira do Titã", tema="lava", lv=(54, 60), de=("mar_de_escoria", 60, 25), min=53, regiao="escoria",
         bolsoes=[("monster_caldeira_salamandra", 6, (54, 55)), ("monster_caldeira_elemental", 6, (55, 56)), ("monster_caldeira_besouro", 5, (56, 57)), ("monster_caldeira_golem", 6, (57, 59))],
         chefe="monster_caldeira_guardiao", obst=[("rocha_lava", 1.4, 3.0), ("rocha", 1.2, 2.4)], props=["ossada", "cranios", "estandarte"],
         volta=("mar_de_escoria", None), tita=("monster_tita_magma", "Coração da Caldeira")),
]
ns["NPCS"].update({
    "bosque_corujas": [("guarda_florestal_iris", "Íris, a Guarda-Florestal", "batedora", None, "Shh. As corujas daqui contam tudo pro Corujão. E ele nunca dorme.")],
    "garganta_colosso": [("velho_minerador_galdino", "Galdino, o Minerador Velho", "aldeao", None,
                          "Lá no fundo da garganta tem um gigante de pedra acorrentado. Ninguém nunca derrubou. Ninguém nunca vai. Mas todo mundo tenta.")],
    "caldeira_tita": [("forjadora_ignea", "Brasa, a Forjadora Exilada", "ferreiro", None,
                       "O Titã de Magma dorme no coração da caldeira. Os anões de Brasaforte juram que um dia alguém vai forjar a arma que o fere de verdade.")],
})
ns["ACHADO"].update({"bosque_corujas": "Pena de coruja prateada", "garganta_colosso": "Elo de corrente gigante", "caldeira_tita": "Escama de magma"})

TITAS = [
    dict(id="monster_tita_colosso", nome="Gorath, o Colosso Que Não Cai", visual="tita_colosso", base_beh="boss_golem", arq="tanque", raio=4.5),
    dict(id="monster_tita_magma", nome="Ignaroth, o Titã de Magma", visual="tita_magma", base_beh="boss_chimera", arq="tanque", raio=5.0),
]


def comportamentos():
    behs = {}
    for f in glob.glob(os.path.join(C, "behaviors*.json")):
        for b in json.load(open(f, encoding="utf-8")): behs[b["id"]] = b
    out = []
    for t in TITAS:
        b = json.loads(json.dumps(behs[t["base_beh"]]))
        b["id"] = "tita_" + t["id"].split("_")[-1]
        b["move_speed"] = 1.6; b["aggro_radius"] = 20; b["leash_radius"] = 18
        for a in b["attacks"]:
            for k in ("range", "radius", "aoe_radius", "length", "width"):
                if isinstance(a.get(k), (int, float)): a[k] = round(a[k] * 2.4, 2)
            a["windup_ms"] = max(int(a.get("windup_ms", 800) * 1.2), 900)   # gigante: lento para preparar (dá para ver e fugir)
        out.append(b)
    return out


def main():
    mons = {}
    for f in glob.glob(os.path.join(C, "monsters*.json")):
        if not f.endswith(("monsters_ramos2.json",)):
            for m in json.load(open(f, encoding="utf-8")): mons[m["id"]] = m
    COMUM_DE_ESPECIAL = {"monster_garganta_morcego": "loot_canions_morcego", "monster_caldeira_salamandra": "loot_escoria_elemental_fornalha"}
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
        novos.append(d)
    for t in TITAS:
        novos.append({"id": t["id"], "species_name": t["nome"], "archetype": t["arq"], "level": 99, "resistances": {}, "behavior_id": "tita_" + t["id"].split("_")[-1],
                      "loot_table_id": "loot_tita", "visual_asset_id": t["visual"], "health_mult": 6000.0, "damage_mult": 0.7, "xp_mult": 0.0,
                      "radius": t["raio"], "coins_min": 0, "coins_max": 0, "boss": True, "elite": False})
    D("monsters_ramos2.json", novos)
    D("behaviors_ramos2.json", comportamentos())

    world = L("world.json")
    for r in REG:
        m = montar(r)
        if "tita" in r:
            tid, zona = r["tita"]
            m["zones"].append({"id": "tita", "name": zona + " — não volte aqui sem amigos (nem com eles)", "kind": "chefe", "x": 0, "y": -47, "w": 30, "h": 18})
            m["spawns"].append({"id": f"{r['id']}_tita", "monster": tid, "x": 0, "y": -47, "level": 99, "group": f"{r['id']}_tita", "respawn_seconds": 600})
            m["obstacles"] = [o for o in m["obstacles"] if not (abs(o["x"]) < 15 and o["y"] < -38 and o["y"] > -56)]
            m["objects"].append({"id": f"{r['id']}_placa_tita", "kind": "contemplar", "x": 6, "y": -34, "label": "Aviso dos que tentaram",
                                 "text": "Riscos na pedra, centenas deles: nomes, datas, e um único recado repetido — «quase». Ninguém derrubou o titã. Ainda."})
        om, ox, oy = r["de"]
        o = L(arq(om))
        o["portals"] = [p for p in o["portals"] if p["id"] != f"{om}_para_{r['id']}"]
        px, py = livre_perto(o, ox, oy)
        o["portals"].append({"id": f"{om}_para_{r['id']}", "x": px, "y": py, "radius": 2.0, "to_map": r["id"], "to_x": 0.0, "to_y": 46.0,
                             "label": r["nome"], "min_level": r["min"], "level_lock": True})
        D(arq(om), o)
        for p in m["portals"]:
            if p["id"] == f"{r['id']}_volta":
                p["to_x"], p["to_y"] = chegada_ao_lado(o, px, py)
            if p["id"] == f"{r['id']}_fim":
                fm = L(arq(p["to_map"]))
                fx, fy = livre_perto(fm, -60, 30)
                p["to_x"], p["to_y"] = fx, fy
        D(f"map_{r['id']}.json", m)
        if f"map_{r['id']}.json" not in world["map_files"]: world["map_files"].append(f"map_{r['id']}.json")
        for reg in world["regions"]:
            if reg["id"] == r["regiao"] and r["id"] not in reg["maps"]: reg["maps"].append(r["id"])
    D("world.json", world)
    # missões: caça → achados → chefe (mesmo molde) e, nos titãs, a "missão impossível" (sem prazo)
    q = []
    xp = lambda l: int(l * l * 70)
    cad = {"bosque_corujas": ("guarda_florestal_iris", "monster_bosque_coruja", 19, "Olhos no Escuro", "«Oito corujas-fantasma a menos e o Corujão fica cego de um lado.»",
                              "Penas de Prata", "«Três penas de coruja prateada caíram pelo bosque. Com elas eu faço um apito que espanta o Corujão.»"),
           "garganta_colosso": ("velho_minerador_galdino", "monster_garganta_escorpiao", 30, "Ferrões na Mina", "«Os escorpiões tomaram a mina velha. Oito, e a gente volta a cavar.»",
                                "Os Elos da Corrente", "«Três elos da corrente do Colosso caíram pela garganta. Traga e eu te conto quem acorrentou ele.»"),
           "caldeira_tita": ("forjadora_ignea", "monster_caldeira_salamandra", 54, "Salamandras no Fole", "«As salamandras entram no fole e explodem a forja. Oito delas, por favor.»",
                             "Escamas de Magma", "«Três escamas que caíram do Titã quando ele se mexeu. Quem sabe um dia viram a lâmina que o fere.»")}
    for r in REG:
        rid = r["id"]; giver, alvo, lv, t1, d1, t2, d2 = cad[rid]; chefe = r["chefe"]
        q1, q2, q3 = f"q_{rid}_1", f"q_{rid}_2", f"q_{rid}_3"
        q.append(dict(id=q1, name=t1, giver=giver, kind="kill", targets=[alvo], count=8, map=rid, min_level=lv, description=d1,
                      hint=f"No(a) {r['nome']}. Volte ao acampamento.", reward_xp=xp(lv), reward_coins=lv * 30, category="historia"))
        q.append(dict(id=q2, name=t2, giver=giver, kind="interact", targets=[f"{rid}_achado_{i}" for i in (1, 2, 3)], count=3, map=rid, min_level=lv + 1, requires=q1,
                      description=d2, hint=f"Três achados nos cantos de {r['nome']}. Aperte F em cada um.", reward_xp=xp(lv + 1), reward_coins=lv * 35, category="historia"))
        q.append(dict(id=q3, name=MON[chefe][1], giver=giver, kind="boss", targets=[chefe], count=1, map=rid, min_level=MON[chefe][2] - 1, requires=q2,
                      description=f"«Agora o pior: {MON[chefe][1]}. Leve tônicos — e amigos.»", hint=f"{MON[chefe][1]} no covil de {r['nome']}.",
                      reward_xp=xp(MON[chefe][2]) * 2, reward_coins=MON[chefe][2] * 70, reward_items=["consumable_c05_tonico_forte*4"], category="historia"))
        if "tita" in r:
            tid = r["tita"][0]; tn = next(t["nome"] for t in TITAS if t["id"] == tid)
            q.append(dict(id=f"q_{rid}_tita", name=f"O Impossível: {tn.split(',')[0]}", giver=giver, kind="boss", targets=[tid], count=1, map=rid,
                          min_level=60, requires=q3, description=f"«{tn}. Ninguém derrubou. Talvez ninguém derrube. Se um dia você conseguir, volta aqui — "
                          "e eu escrevo o seu nome na pedra, por cima de todos os 'quase'.»", hint=f"{tn}, no fundo de {r['nome']}. Nível 99.",
                          reward_xp=0, reward_coins=100000, reward_flags=[f"tita_vencido:{tid}"], category="desafio"))
    D("quests_ramos2.json", q)
    print("ramificações novas:", [r["id"] for r in REG], "| titãs:", [t["id"] for t in TITAS])


main()

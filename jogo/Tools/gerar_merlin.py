#!/usr/bin/env python3
"""
MERLIN, o Mago das Estrelas (13/11, pedidos do dono). Mora no Observatório de Astrália (cidade 2, Tools/gerar_cidades.py),
não no Porto. Boneco padrão + pacote npcs/npc_merlin.glb (look "merlin").

ENREDO — "O Grande Arranjo" (12 missões em 3 atos, nível 20 a 35)
Há muito tempo, o jovem Merlin, astrônomo da corte de Astrália, lançou o Grande Arranjo para pôr o céu em ordem. Deu certo
demais: quatro reinos (a Lua Vermelha, a Transilvânia, o Inferno e a Cidade Celestial) foram dobrados para fora do mapa, e
sumiram quatro estrelas dos céus. Antes, ele tinha ensaiado o feitiço numa floresta — a Floresta Oculta. Desde então tudo
anda torto: a lua fora do lugar (lobisomens enlouquecidos), estrelas caindo (um visitante de outro céu no fundo de um poço),
a torre dele entortada, e um dragão antigo que dorme segurando a dobra do mundo no sonho. A Sibila, no Porto, foi aprendiz
dele e achou outro jeito de espiar os reinos (os livros). O Merlin nunca contou a ninguém. Agora está velho e quer desfazer.
  Ato I   — A Torre que Entortou   (20–23): Dália confirma o estrago · lobisomens · a floresta do ensaio · a Torre Torta
  Ato II  — O que Caiu do Céu       (24–28): ouvir as estrelas · o arquivo confiscado · a página roubada · o Poço das Estrelas
  Ato III — O Sonho do Dragão       (30–35): a Sibila cobra a verdade · a ampulheta · o Sonho do Dragão · desfazer o Arranjo
No fim o Merlin desfaz a dobra e leva o jogador aos quatro reinos (viagem nele). A Sibila e o Tião continuam levando também.

Cria/reescreve: Content/map_torre_merlin.json (interior do Observatório), map_merlin_*.json (3 dungeons),
monsters_merlin.json, quests_merlin.json. Mexe (idempotente): map_astralia.json (Merlin + porta do Observatório),
map_porto.json (só TIRA um Merlin antigo, se houver) e world.json (mapas na região de Astrália).
Rodar depois de Tools/gerar_cidades.py. Uso (raiz do projeto): python3 Tools/gerar_merlin.py
"""
import copy
import glob
import json
import os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
MERLIN = "merlin_mago"
TORRE = "torre_merlin"
CIDADE = "astralia"


def L(name):
    return json.load(open(os.path.join(C, name), encoding="utf-8"))


def D(name, obj):
    json.dump(obj, open(os.path.join(C, name), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


# ------------------------------------------------------------------------------------------------ as três dungeons
DGS = [
    dict(id="merlin_torre_torta", nome="A Torre Torta de Merlin", base="cripta_do_sino", tema="biblioteca", desvio=2, flag="merlin:dg1", nivel=21,
         # bichos da Torre do programador (13/11, gerar_merlin.py dele): os mesmos, nesta dungeon
         troca={"monster_mata_esqueleto_cripta": ("monster_merlin_livro_mordedor", None, None),
                "monster_mata_acolito_espectral": ("monster_merlin_aprendiz_fantasma", None, None),
                "monster_mata_cavaleiro_sepulcral": ("monster_merlin_armadura_encantada", None, None),
                "monster_mata_guardiao_sino": ("monster_merlin_reflexo", None, None)},
         chefe_sala="Observatório Torto: o Reflexo de Merlin", primeira="primeira_torre_merlin", bau="bau_torre_merlin",
         enfeites=["glb:dg/dg_estante_livros", "glb:dg/dg_pilha_livros", "glb:dg/dg_candelabro", "glb:dg/dg_atril", "glb:dg/dg_mesa_estudo"]),
    dict(id="merlin_poco_estrelas", nome="O Poço das Estrelas", base="galerias_piedade", tema="lua", desvio=1, flag="merlin:dg2", nivel=27,
         troca={"monster_canions_mineiro_oco": ("monster_merlin_lobo_lua_nova", "Lobo da Lua Nova", "werewolf"),
                "monster_canions_espectro_galeria": ("monster_merlin_estrela_cadente", "Estrela Cadente Perdida", "ghost"),
                "monster_canions_troll_carcereiro": ("monster_merlin_morcego_lunar", "Morcego Lunar", "bat_white"),
                "monster_canions_carcereiro_almas": ("monster_merlin_visitante_caido", "O Visitante que Caiu do Céu", "alien")},
         chefe_sala="Fundo do Poço: o Visitante que Caiu",
         enfeites=["glb:dg/dg_cristais_cal", "glb:dg/dg_astrolabio", "glb:dg/dg_estalagmites", "glb:dg/dg_candelabro"]),
    dict(id="merlin_sonho_dragao", nome="O Sonho do Dragão Adormecido", base="mosteiro_afundado", tema="ceu", desvio=1, flag="merlin:dg3", nivel=33,
         troca={"monster_brejo_carnical": ("monster_merlin_cavaleiro_ontem", "Cavaleiro de Ontem", "knight"),
                "monster_brejo_tecela_mosteiro": ("monster_merlin_eco_amanha", "Eco de Amanhã", "ghost_knight"),
                "monster_brejo_viuva_tecela": ("monster_merlin_sentinela_ampulheta", "Sentinela-Ampulheta", "sentinel"),
                "monster_brejo_abadessa_lodo": ("monster_merlin_dragao_sonho", "O Dragão do Sonho", "dragon")},
         chefe_sala="Ninho do Sonho: o Dragão Adormecido",
         enfeites=["glb:dg/dg_coluna_gotica", "glb:dg/dg_sino_rachado", "glb:dg/dg_estatua_sabio", "glb:dg/dg_candelabro", "glb:dg/dg_urnas"]),
]
TORRE_PORTAIS = {"merlin:dg1": (-8.0, -9.5), "merlin:dg2": (0.0, -11.5), "merlin:dg3": (8.0, -9.5)}
ASTROLABIO = (0.0, -3.0)


# ---- da Torre do programador (13/11): bichos, chefe de 2 fases, Pó de Estrela e saque — iguais aos dele
def mon(id_, nome, arq, lvl, beh, loot, vis, hp, dmg, xp_=1.0, raio=0.5, boss=False, elite=False, moedas=None):
    c0, c1 = moedas or (lvl * 2, lvl * 4)
    d = {"id": id_, "species_name": nome, "archetype": arq, "level": lvl, "resistances": {}, "behavior_id": beh, "loot_table_id": loot,
         "visual_asset_id": vis, "health_mult": hp, "damage_mult": dmg, "xp_mult": xp_, "radius": raio, "coins_min": c0, "coins_max": c1}
    if boss: d["boss"] = True
    if elite: d["elite"] = True
    return d


MONSTROS_TORRE = [
    mon("monster_merlin_livro_mordedor", "Livro Mordedor", "agil", 21, "swarm_bite", "loot_merlin_torre", "bat", 0.8, 1.6, 0.9, 0.35),
    mon("monster_merlin_aprendiz_fantasma", "Aprendiz Fantasma", "conjurador", 22, "ghost_caster", "loot_merlin_torre", "ghost", 1.1, 1.7, 1.2, 0.45),
    mon("monster_merlin_armadura_encantada", "Armadura Encantada", "tanque", 23, "elite_knight", "loot_merlin_elite", "knight", 3.4, 1.9, 4.0, 0.6, elite=True),
    mon("monster_merlin_reflexo", "O Reflexo de Merlin", "conjurador", 24, "merlin_reflexo", "loot_merlin_chefe", "fab_merlin_sombra", 5.0, 1.9, 11, 0.6, boss=True, moedas=(160, 320)),
]
A = lambda **k: k
COMPORTAMENTOS = [
    {"id": "merlin_reflexo", "move_speed": 3.4, "aggro_radius": 14, "leash_radius": 18, "keep_distance": 6.0, "exposed_mult": 1.35, "staggerable": False,
     "summon": "monster_merlin_livro_mordedor", "summon_count": 3, "summon_every_ms": 16000, "phase_two_at": 0.5, "phase_two_attacks": ["chuva_de_estrelas"], "attacks": [
        A(id="bola_estrela", shape="projectile", range=12, min_range=2.0, windup_ms=700, active_ms=100, recovery_ms=700, cooldown_ms=1400, damage_mult=0.7, radius=0.35, projectile_speed=10, count=5, spread_deg=40, track_fraction=0.8),
        A(id="raio_lunar", shape="beam", range=13, windup_ms=1100, active_ms=900, recovery_ms=900, cooldown_ms=5200, damage_mult=0.6, radius=0.7, hits=3, hit_interval_ms=280, track_fraction=0.5),
        A(id="chuva_de_estrelas", shape="circle", range=12, windup_ms=1200, active_ms=300, recovery_ms=1000, cooldown_ms=6500, damage_mult=1.4, radius=2.8, at_target=True, track_fraction=0.5, status="atordoado", status_ms=700),
        A(id="empurrao_arcano", shape="circle", range=2.6, windup_ms=700, active_ms=200, recovery_ms=900, cooldown_ms=4000, damage_mult=0.9, radius=3.0, offset=0, track_fraction=0)]},
]
ITENS = [
    {"id": "mat_po_estrela", "display_name": "Pó de Estrela", "summary": "Brilha amarelo na palma da mão e faz cócegas. O Merlin diz que é o que sobra quando um feitiço termina.",
     "category": "material", "subtype": "material", "min_level": 1, "allowed_classes": [], "handedness": 0, "base_stats": {}, "affix_pool": "none",
     "temperable": False, "trade_rule": "free", "stack_limit": 30, "icon_asset_id": "icon_mat_po_estrela", "model_asset_id": "mat_vein", "price": 35,
     "source": "Torre Torta de Merlin e Poço das Estrelas (Observatório de Astrália)."},
]
GEAR = ["weapon_guerreiro_espada_canion", "staff_mago_cajado_canion", "talisma_bruxa_canion", "arco_arqueiro_canion",
        "armor_guerreiro_canion", "armor_mago_canion", "armor_bruxa_canion", "armor_arqueiro_canion", "helmet_canion"]
SAQUE = [
    {"id": "loot_merlin_torre", "entries": [{"chance": 0.55, "item": "mat_po_estrela"}, {"chance": 0.04, "pick": GEAR, "match_class": True},
                                            {"chance": 0.12, "pick": ["consumable_c03_frasco_mana", "consumable_c01_tonico_fraco"], "match_class": True}]},
    {"id": "loot_merlin_elite", "entries": [{"chance": 1.0, "item": "mat_po_estrela", "min_qty": 2, "max_qty": 3}, {"chance": 1.0, "pick": GEAR, "match_class": True},
                                            {"chance": 1.0, "item": "consumable_c02_tonico_medio", "min_qty": 1, "max_qty": 2}]},
    {"id": "loot_merlin_chefe", "entries": [{"chance": 1.0, "item": "mat_po_estrela", "min_qty": 3, "max_qty": 5}, {"chance": 1.0, "pick": GEAR, "match_class": True, "quality": "chefe"},
                                            {"chance": 1.0, "item": "consumable_c02_tonico_medio", "min_qty": 2, "max_qty": 3}]},
    {"id": "bau_torre_merlin", "entries": [{"chance": 1.0, "item": "mat_po_estrela", "min_qty": 2, "max_qty": 4}, {"chance": 0.6, "pick": GEAR, "match_class": True},
                                           {"chance": 1.0, "item": "consumable_c04_frasco_mana_grande", "min_qty": 1, "max_qty": 2}]},
    {"id": "primeira_torre_merlin", "entries": [{"chance": 1.0, "pick": GEAR, "match_class": True, "quality": "chefe"}, {"chance": 1.0, "item": "mat_po_estrela", "min_qty": 4, "max_qty": 6}]},
]


def monstros(mons):
    radius_por_visual = {}
    for m in mons.values():
        radius_por_visual.setdefault(m["visual_asset_id"], m.get("radius", 0.6))
    out = list(MONSTROS_TORRE)
    for dg in DGS:
        for base_id, (nid, nome, vis) in dg["troca"].items():
            if nome is None:
                continue                      # bicho da Torre do programador (já em MONSTROS_TORRE)
            b = mons[base_id]
            n = {**b, "id": nid, "species_name": nome, "visual_asset_id": vis, "level": b["level"] + dg["desvio"],
                 "radius": max(b.get("radius", 0.6), radius_por_visual.get(vis, 0.6)) if b.get("boss") else radius_por_visual.get(vis, b.get("radius", 0.6))}
            if b.get("boss"):
                n["health_mult"] = round(b.get("health_mult", 1) * 1.25, 2)
                n["xp_mult"] = round(b.get("xp_mult", 1) * 1.2, 2)
            out.append(n)
    return out


def dungeon(dg):
    base = L(f"map_{dg['base']}.json")
    m = copy.deepcopy(base)
    px, py = TORRE_PORTAIS[dg["flag"]]
    volta_x, volta_y = px, py + 3.0
    chefe_base = base["first_clear_boss"]
    m.update(id=dg["id"], display_name=dg["nome"], theme=dg["tema"], region=CIDADE, exit_map=TORRE, exit_x=volta_x, exit_y=volta_y,
             first_clear_boss=dg["troca"][chefe_base][0])
    if dg.get("primeira"):
        m["first_clear_loot"] = dg["primeira"]
    if dg.get("bau"):
        for i, c in enumerate(m.get("chests", [])):
            c["loot_table"] = dg["bau"]
    for s in m["spawns"]:
        s["id"] = dg["id"] + "_" + s["id"]
        s["group"] = dg["id"] + "_" + s.get("group", s["id"])
        s["level"] += dg["desvio"]
        if s["monster"] in dg["troca"]:
            s["monster"] = dg["troca"][s["monster"]][0]
    for p in m["portals"]:
        p["id"] = p["id"].replace(dg["base"], dg["id"])
        p["to_map"], p["to_x"], p["to_y"] = TORRE, volta_x, volta_y
        p["label"] = "Voltar ao Observatório"
    for z in m["zones"]:
        if z["kind"] == "chefe":
            z["name"] = dg["chefe_sala"]
    for c in m.get("chests", []):
        c["id"] = dg["id"] + "_" + c["id"]
    for g in m.get("gates", []):
        g["id"] = dg["id"] + "_" + g["id"]
        if g.get("flag"):
            g["flag"] = dg["id"] + "_" + g["flag"]
    k = 0
    for p in m["props"]:
        if p["kind"] in ("tocha", "ossada", "estandarte") and k % 2 == 0:
            p["kind"] = dg["enfeites"][(k // 2) % len(dg["enfeites"])]
        k += 1
    return m


def torre(saida):
    """Interior do Observatório: sala redonda de livros com os 3 portais do Merlin e o astrolábio grande."""
    w = h = 15
    walls = [dict(kind="muralha", shape="box", x=0, y=-h + 0.5, w=2 * w, h=1, r=0, height=5.5), dict(kind="muralha", shape="box", x=0, y=h - 0.5, w=2 * w, h=1, r=0, height=5.5),
             dict(kind="muralha", shape="box", x=-w + 0.5, y=0, w=1, h=2 * h, r=0, height=5.5), dict(kind="muralha", shape="box", x=w - 0.5, y=0, w=1, h=2 * h, r=0, height=5.5)]
    props = [{"kind": "glb:dg/dg_astrolabio", "x": ASTROLABIO[0], "y": ASTROLABIO[1], "yaw": 0, "scale": 1.4},
             {"kind": "movel_tapete", "x": 0, "y": 3, "yaw": 0}, {"kind": "glb:dg/dg_mesa_estudo", "x": -6, "y": 3, "yaw": 90},
             {"kind": "glb:dg/dg_atril", "x": 6, "y": 3, "yaw": -90}, {"kind": "glb:dg/dg_pilha_livros", "x": -7.5, "y": 5, "yaw": 30},
             {"kind": "glb:dg/dg_pilha_livros", "x": 7.6, "y": 6, "yaw": 200}, {"kind": "glb:dg/dg_estatua_sabio", "x": 0, "y": -13, "yaw": 0}]
    for y in (-6, 0, 6, 11):
        props += [{"kind": "glb:dg/dg_estante_livros", "x": -13.6, "y": y, "yaw": 90}, {"kind": "glb:dg/dg_estante_livros", "x": 13.6, "y": y, "yaw": -90}]
    for x, y in ((-4, 9), (4, 9), (-11, -11), (11, -11), (-4, -6), (4, -6)):
        props.append({"kind": "glb:dg/dg_candelabro", "x": x, "y": y, "yaw": 0})
    obst = walls + [dict(kind="estante", shape="box", x=s * 13.6, y=y, w=1.2, h=2.4, r=0, height=2.6) for s in (-1, 1) for y in (-6, 0, 6, 11)]
    obst.append(dict(kind="astrolabio", shape="circle", x=ASTROLABIO[0], y=ASTROLABIO[1], w=0, h=0, r=1.3, height=2.2))
    portais = [{"id": "torre_merlin_saida", "x": 0, "y": 13.0, "radius": 1.4, "to_map": CIDADE, "to_x": saida[0], "to_y": saida[1],
                "label": "Sair para Astrália", "por_porta": True}]
    for dg in DGS:
        x, y = TORRE_PORTAIS[dg["flag"]]
        portais.append({"id": f"torre_para_{dg['id']}", "x": x, "y": y, "radius": 1.6, "to_map": dg["id"], "to_x": 0, "to_y": 33,
                        "label": dg["nome"], "requires_flag": dg["flag"], "min_level": dg["nivel"]})
    livros = [
        ("merlin_livro_diario", -6.0, 4.6, "Diário do Merlin (página solta)",
         "Dia 1 do Grande Arranjo. O céu está bagunçado: estrelas tortas, uma lua que sobra, quatro reinos atrapalhando as constelações. "
         "Hoje eu arrumo tudo. Ensaiei na floresta e deu certíssimo — a floresta sumiu inteirinha. Perfeito."),
        ("merlin_livro_estrelas", 6.0, 4.6, "Almanaque das Estrelas (riscado)",
         "Quatro estrelas faltando nos mapas novos. Nos antigos, cada uma está em cima de um reino: a Lua Vermelha, a Transilvânia, "
         "o Inferno e a Cidade Celestial. Embaixo, na letra do Merlin: «culpa minha».") ,
        ("merlin_livro_portas", 0.0, -5.4, "As Quatro Portas",
         "Quem dobra o mundo precisa de alguém que segure a dobra. Pedi ao dragão mais velho que existe. Ele aceitou, desde que pudesse "
         "dormir. Enquanto ele sonhar, as portas ficam fechadas. Para abrir, é preciso entrar no sonho — e sair acordado."),
    ]
    objetos = [{"id": i, "kind": "livro", "x": x, "y": y, "label": l, "text": t} for i, x, y, l, t in livros]
    objetos.append({"id": "porta_torre_saida", "kind": "porta", "x": 0, "y": 13.0, "label": "Porta (sair)", "grant_flag": "porta:portal:torre_merlin_saida"})
    return {
        "id": TORRE, "display_name": "Observatório de Merlin", "half_size": w, "spawn_x": 0, "spawn_y": 10.5, "safe_radius": 40, "xp_multiplier": 1,
        "kind": "cidade", "region": CIDADE, "theme": "biblioteca", "no_mount": True, "exit_map": CIDADE, "exit_x": saida[0], "exit_y": saida[1],
        "zones": [{"id": "salao", "name": "Observatório de Merlin", "kind": "seguro", "x": 0, "y": 0, "w": 2 * w, "h": 2 * h}],
        "obstacles": obst, "gates": [], "chests": [], "spawns": [], "waypoints": [], "checkpoints": [], "objects": objetos, "props": props,
        "npcs": [{"id": "coruja_arquimedes", "name": "Arquimedes, a Coruja da Torre", "role": "missoes", "x": 3.0, "y": -3.0, "facing": 180, "look": "aldeao",
                  "greeting": "Hu-hu. Os portais acendem conforme o Merlin confia em você. Ele confia em pouca gente. Em mim, por exemplo, não."}],
        "portals": portais,
    }


def missoes():
    q = []

    def M(id_, nome, kind, nivel, desc, hint, xp, moedas, **kw):
        d = {"id": "q_merlin_" + id_, "name": nome, "giver": MERLIN, "kind": kind, "targets": kw.pop("targets", []), "count": kw.pop("count", 1),
             "description": desc, "hint": hint, "min_level": nivel, "reward_xp": xp, "reward_coins": moedas, "category": "historia"}
        if q:
            d["requires"] = q[-1]["id"]
        d.update(kw)
        q.append(d)

    dg1, dg2, dg3 = DGS
    boss = {dg["id"]: dg["troca"][L(f"map_{dg['base']}.json")["first_clear_boss"]][0] for dg in DGS}
    # ---------------------------------------------------------------- Ato I — A Torre que Entortou
    M("01", "Um Palmo Fora do Lugar", "talk", 20,
      "«Ah, você! Tem cara de quem enxerga. Eu não — perdi os óculos de novo. Faz um favor a um velho: pergunte à Dália, a astrônoma-chefe, "
      "quanto a lua andou este mês. Se ela disser 'um palmo', é pior do que eu pensava.»",
      "Fale com Dália, a Astrônoma-Chefe, em Astrália (perto da casa dela, ao norte da praça).",
      30000, 400, targets=["astronoma_dalia"], reward_items=["consumable_c02_tonico_medio*3"])
    M("02", "Uivos Fora de Hora", "kill", 21,
      "«Um palmo. Eu sabia. Com a lua fora do lugar os lobisomens da Mata uivam a qualquer hora e mordem qualquer um. Derrube 5. "
      "E… já que vai ficar sabendo de qualquer jeito: fui eu que tirei a lua do lugar. Há muito tempo. Volte que eu explico.»",
      "Derrube 5 Lobisomens na Mata dos Enforcados. Depois volte ao Merlin, em Astrália.",
      42000, 550, targets=["monster_bst_lobisomem"], count=5, map="mata_dos_enforcados", reward_flags=["merlin:floresta"])
    M("03", "A Floresta do Ensaio", "kill", 21,
      "«Quando eu era jovem lancei o Grande Arranjo, para pôr o céu em ordem. Antes, ensaiei numa floresta — e ela sumiu do mapa. É a Floresta "
      "Oculta. Eu deixei uma garrafa de sacis lá, para guardar o lugar. A garrafa quebrou. Eu te levo: derrube 8 sacis.»",
      "Peça ao Merlin a viagem para a Floresta Oculta e derrube 8 Sacis das Cinzas.",
      48000, 600, targets=["monster_bst_saci"], count=8, map="floresta_oculta", reward_items=["consumable_c05_tonico_forte*3"])
    M("04", "A Torre Torta", "boss", 22,
      "«O Arranjo entortou até a minha torre: a parte de cima virou outro lugar. Os livros mordem, meus aprendizes viraram fantasmas e "
      "tem alguém lá no alto usando a minha cara — o meu Reflexo, que saiu do espelho no dia do feitiço. Ele guarda o Caderno do Grande "
      "Arranjo. Derrube-o e traga o caderno. Cuidado: quando ele cansa, faz chover estrela.»",
      "Entre no Observatório (porta da torre, em Astrália), pegue o portal da esquerda e derrube o Reflexo de Merlin no alto da Torre Torta.",
      70000, 800, targets=[boss[dg1["id"]]], map=dg1["id"], accept_flags=[dg1["flag"]], reward_items=["consumable_c05_tonico_forte*3"])
    # ---------------------------------------------------------------- Ato II — O que Caiu do Céu
    M("05", "Ouvir as Estrelas", "defend", 24,
      "«O caderno está escrito em estrela — só dá para ler ouvindo o céu. Montei um círculo de pedras nos Cânions. Os bichos de lá acham que "
      "é um restaurante. Fique no círculo e segure três investidas enquanto eu escuto.»",
      "Cânions dos Suplicantes: fique no círculo marcado e aguente 3 ondas. Sair para longe cancela a onda.",
      60000, 750, targets=["monster_canions_escorpiao", "monster_canions_morcego"], count=3, map="canions_suplicantes",
      x="__DEF_X__", y="__DEF_Y__", radius=6, reward_items=["consumable_c05_tonico_forte*3"])
    M("06", "O Arquivo Confiscado", "escort", 25,
      "«A Academia de Astrália confiscou minhas anotações anos atrás — acharam que eu estava louco. Estavam certos, mas as anotações são "
      "minhas. Uma carroça com o arquivo vem pelos Cânions. Ande junto dela: papel de mago atrai bicho como queijo atrai rato.»",
      "Cânions dos Suplicantes: a carroça só anda com você perto e com o caminho limpo. Limpe as 3 emboscadas.",
      65000, 800, targets=["monster_canions_esqueleto_mineiro", "monster_canions_escorpiao", "monster_canions_esqueleto_mineiro"], count=3,
      map="canions_suplicantes", x="__ESC_X__", y="__ESC_Y__", to_x="__ESC_TX__", to_y="__ESC_TY__", reward_items=["consumable_c05_tonico_forte*3"])
    M("07", "A Página Lida ao Contrário", "hunt", 26,
      "«Falta uma página no arquivo: a que diz onde caíram as quatro estrelas. Uma múmia dos Cânions arrancou e está lendo de trás para "
      "frente. Se ela chegar no começo, desfaz o feitiço errado. Ache a múmia grande — ela deixa faixas pelo caminho.»",
      "Cânions dos Suplicantes: siga o rastro de faixas até a Múmia alfa e derrube-a.",
      80000, 900, targets=["monster_bst_mumia"], map="canions_suplicantes", x="__CACA_X__", y="__CACA_Y__", radius=14,
      reward_items=["consumable_c05_tonico_forte*4"])
    M("08", "O Poço das Estrelas", "boss", 28,
      "«A página diz: uma das estrelas que sumiram caiu num poço embaixo do meu observatório. Só que estrela não tem olhos — e aquela tem. "
      "É alguém do céu de um dos reinos, jogado para fora quando eu dobrei tudo. Desça pelo portal do meio e… derrube-o com carinho.»",
      "No Observatório, entre no portal do meio (O Poço das Estrelas) e derrube o Visitante que Caiu do Céu.",
      120000, 1200, targets=[boss[dg2["id"]]], map=dg2["id"], accept_flags=[dg2["flag"]], reward_flags=["merlin:fabula"],
      reward_items=["consumable_c05_tonico_forte*5"])
    # ---------------------------------------------------------------- Ato III — O Sonho do Dragão
    M("09", "A Aprendiz que Sabia", "talk", 30,
      "«Agora vem a parte difícil, e não é luta. A Sibila, a vidente do Porto, foi minha aprendiz. Ela descobriu o Arranjo sozinha e achou "
      "outro jeito de espiar os reinos, pelos livros. Eu nunca pedi desculpas. Vá até ela. Diga que o velho quer desfazer.»",
      "Fale com a Sibila, a Vidente, no Porto das Cinzas.",
      60000, 500, targets=["sibila_vidente"])
    M("10", "Pó de Estrela para a Ampulheta", "collect", 31,
      "«A Sibila disse… muita coisa. A útil: para entrar num sonho sem ficar preso lá, é preciso parar o tempo — e a minha ampulheta só "
      "corre com pó de estrela, o que sobra quando um feitiço termina. A Torre Torta está cheia disso. Dez pitadas. Não espirre perto.»",
      "Junte 10 Pós de Estrela (caem na Torre Torta: livros, fantasmas, armadura e o Reflexo) e entregue ao Merlin.",
      95000, 1000, targets=["mat_po_estrela"], count=10, reward_items=["consumable_c05_tonico_forte*4"])
    M("11", "O Sonho do Dragão Adormecido", "boss", 33,
      "«Quem dobra o mundo precisa de alguém que segure a dobra. Pedi ao dragão mais velho que existe. Ele segura — dormindo. Enquanto "
      "ele sonhar, as quatro portas ficam fechadas. O portal da direita entra no sonho. Lá dentro ontem e amanhã brigam. Acorde o dragão.»",
      "No Observatório, entre no portal da direita (O Sonho do Dragão Adormecido) e derrube o Dragão do Sonho.",
      220000, 2000, targets=[boss[dg3["id"]]], map=dg3["id"], accept_flags=[dg3["flag"]], reward_items=["consumable_c05_tonico_forte*6"])
    M("12", "Desfazer o Grande Arranjo", "visit", 35,
      "«O dragão acordou e soltou a dobra. Falta o último giro: o astrolábio grande do Observatório. Gire-o você — eu já girei uma vez, e "
      "olha no que deu. Depois disso, é só me pedir: eu te levo à Lua Vermelha, à Transilvânia, ao Inferno ou à Cidade Celestial.»",
      "Observatório de Merlin: chegue ao astrolábio grande no meio da sala. Depois peça a viagem ao Merlin, na porta da torre.",
      150000, 1500, map=TORRE, x=ASTROLABIO[0], y=ASTROLABIO[1] + 1.6, radius=2.6, reward_flags=["merlin:reinos"],
      reward_items=["consumable_c05_tonico_forte*10"])
    return q


def npc_merlin(x, y):
    t = [{"id": "merlin_floresta", "label": "Feitiço de caminho: a Floresta do Ensaio (Floresta Oculta)", "to_map": "floresta_oculta", "to_x": 0.0, "to_y": 42.0,
          "price": 100, "requires_flag": "merlin:floresta", "min_level": 20}]
    t.append({"id": "merlin_reino_fabula", "label": "O Livro dos Contos: Era Uma Vez (o reino que caiu junto com a estrela)", "to_map": "reino_fabula",
              "to_x": 0.0, "to_y": 60.0, "price": 300, "requires_flag": "merlin:fabula", "min_level": 24})
    for rid, label in (("reino_lua", "Portal das estrelas: a Lua Vermelha"), ("reino_transilvania", "Portal das estrelas: a Transilvânia"),
                       ("reino_inferno", "Portal das estrelas: o Inferno"), ("reino_celeste", "Portal das estrelas: a Cidade Celestial")):
        t.append({"id": "merlin_" + rid, "label": label, "to_map": rid, "to_x": 0.0, "to_y": 42.0, "price": 500, "requires_flag": "merlin:reinos",
                  "min_level": 35})
    return {"id": MERLIN, "name": "Merlin, o Mago das Estrelas", "role": "missoes", "x": x, "y": y, "facing": 200, "look": "merlin",
            "greeting": "«Ah! Você chegou na hora certa. Ou errada. Com estrelas é difícil saber. Preciso de alguém que enxergue — perdi os óculos.»",
            "travel": t}


def livre(m, x, y, r):
    if abs(x) > m["half_size"] - 3 or abs(y) > m["half_size"] - 3:
        return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if ((x - o["x"]) ** 2 + (y - o["y"]) ** 2) ** 0.5 < o["r"] + r:
                return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r:
            return False
    return True


def perto_livre(m, x0, y0, r, longe_de_bicho=0.0):
    import math
    for raio in range(0, 40):
        for k in range(0, 360, 15):
            x = round(x0 + raio * math.cos(math.radians(k)), 1)
            y = round(y0 + raio * math.sin(math.radians(k)), 1)
            if not livre(m, x, y, r):
                continue
            if longe_de_bicho and any(math.hypot(x - s["x"], y - s["y"]) < longe_de_bicho for s in m.get("spawns", [])):
                continue
            return x, y
    raise SystemExit(f"sem lugar livre perto de {x0},{y0} em {m['id']}")


def main():
    mons = {}
    for f in glob.glob(os.path.join(C, "monsters*.json")):
        if not f.endswith("monsters_merlin.json"):
            for m in json.load(open(f, encoding="utf-8")):
                mons[m["id"]] = m
    D("monsters_merlin.json", monstros(mons))
    D("behaviors_merlin.json", COMPORTAMENTOS)
    D("items_merlin.json", ITENS)
    D("loot_merlin.json", SAQUE)
    # sobras da 1ª versão do programador (Merlin no Porto, objetos de missão espalhados): saem
    for f in glob.glob(os.path.join(C, "map_*.json")):
        m = json.load(open(f, encoding="utf-8"))
        n0 = (len(m.get("npcs", [])), len(m.get("portals", [])), len(m.get("objects", [])))
        m["npcs"] = [n for n in m.get("npcs", []) if n.get("id") not in ("merlin",)]
        m["portals"] = [p for p in m.get("portals", []) if p.get("id") != "merlin_torre"]
        m["objects"] = [o for o in m.get("objects", []) if not o.get("merlin")]
        if n0 != (len(m["npcs"]), len(m["portals"]), len(m["objects"])):
            json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    world = L("world.json")
    for dg in DGS:
        D(f"map_{dg['id']}.json", dungeon(dg))

    # Merlin sai do Porto (se uma versão antiga o pôs lá) e mora na porta do Observatório de Astrália
    porto = L("map_porto.json")
    if any(n["id"] == MERLIN for n in porto["npcs"]):
        porto["npcs"] = [n for n in porto["npcs"] if n["id"] != MERLIN]
        D("map_porto.json", porto)
    cid = L(f"map_{CIDADE}.json")
    try:
        fx, fy = json.load(open(os.path.join(C, ".porta_observatorio.json")))
    except FileNotFoundError:
        fx, fy = 0.0, 13.1
    porta_y = fy + 1.25                                         # a porta (portal) encostada na torre
    saida = (fx, round(fy - 1.6, 2))                            # quem sai da torre aparece 2,8 m à frente da porta
    cid["npcs"] = [n for n in cid["npcs"] if n["id"] != MERLIN] + [npc_merlin(round(fx + 3.2, 2), round(fy - 0.4, 2))]
    cid["portals"] = [p for p in cid["portals"] if p["id"] != "astralia_observatorio"] + [
        {"id": "astralia_observatorio", "x": fx, "y": round(porta_y, 2), "radius": 1.2, "to_map": TORRE, "to_x": 0, "to_y": 10.5,
         "label": "Observatório de Merlin", "por_porta": True}]
    cid["objects"] = [o for o in cid["objects"] if o["id"] != "porta_observatorio"] + [
        {"id": "porta_observatorio", "kind": "porta", "x": fx, "y": round(porta_y, 2), "label": "Observatório de Merlin",
         "grant_flag": "porta:portal:astralia_observatorio", "text": "A porta azul abre sozinha. Lá de cima, alguém grita: «Limpe os pés! E não mexa no astrolábio!»"}]
    D(f"map_{CIDADE}.json", cid)
    D(f"map_{TORRE}.json", torre(saida))

    for f in [f"map_{TORRE}.json"] + [f"map_{dg['id']}.json" for dg in DGS]:
        if f not in world["map_files"]:
            world["map_files"].append(f)
    world["regions"] = [r for r in world["regions"] if r["id"] != TORRE]          # (região antiga, da 1ª versão)
    for r in world["regions"]:
        if r["id"] != CIDADE and TORRE in r.get("maps", []):
            r["maps"].remove(TORRE)                                                   # (a versão do programador punha a torre no Porto)
    world["links"] = [l for l in world["links"] if TORRE not in (l["from"], l["to"])]
    reg = next(r for r in world["regions"] if r["id"] == CIDADE)
    for mid in [TORRE] + [dg["id"] for dg in DGS]:
        if mid not in reg["maps"]:
            reg["maps"].append(mid)
    D("world.json", world)

    can = L("map_canions_suplicantes.json")
    dx, dy = perto_livre(can, 34, 36, 3.0, 9)
    cx, cy = perto_livre(can, 36, -30, 2.0)
    ex, ey = perto_livre(can, 12, 44, 2.0)
    tx, ty = perto_livre(can, 52, 2, 2.0)
    q = json.dumps(missoes(), ensure_ascii=False)
    for k, v in (("__DEF_X__", dx), ("__DEF_Y__", dy), ("__ESC_X__", ex), ("__ESC_Y__", ey), ("__ESC_TX__", tx), ("__ESC_TY__", ty),
                 ("__CACA_X__", cx), ("__CACA_Y__", cy)):
        q = q.replace(f'"{k}"', str(v))
    D("quests_merlin.json", json.loads(q))
    print("merlin em", CIDADE, (round(fx + 3.2, 2), round(fy - 0.4, 2)), "| porta", (fx, porta_y), "| dungeons:", [d["id"] for d in DGS])


if __name__ == "__main__":
    main()

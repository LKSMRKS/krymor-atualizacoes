#!/usr/bin/env python3
"""
14/11 noite (pranchas do dono: "Coleção de Asas" 25, capas 15, skins 6). Cria/atualiza os itens (idempotente: troca o
item de mesmo id, nunca renomeia id), põe os comuns nas lojas e escreve a lista de onde vem cada um.
  * Asas "asas_*" (todas as classes; voam com G como as outras): comuns 25–29, incomuns 30–38, raras 40–48,
    épicas 50–56, lendárias 58–60. Comuns e incomuns: Aurélio, o Mestre das Asas (Astrália). Raras/épicas/lendárias: chefes da
    faixa (GameServer.Colecao.cs) e a Loja de Cristais.
  * Capas (12 novas + Real, Gelo e Floresta repaginadas): baixas nas lojas, altas de chefe e na Loja de Cristais.
  * Skins (braceletes): 4 no Mascate, Tubarão e Lava de chefe (e cristais).
Preços: Tools/regular_economia.py (roda depois). Ícones: recortados das pranchas (Client/Assets/Art/ico_*).
"""
import json, os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
TODAS = ["guerreiro", "mago", "bruxa", "arqueiro"]

ASAS = [  # id, nome, nível, raridade, atributo extra, descrição
    ("pano", "Asas de Pano", 25, "Comum", {}, "Retalhos de lona costurados em varetas, puídos nas pontas."),
    ("penas", "Asas de Penas", 26, "Comum", {"dodge_pct": 0.02}, "Penas marrons de gavião, simples e leves."),
    ("morcego", "Asas de Morcego", 27, "Comum", {"crit_pct": 0.02}, "Membrana vermelha esticada em dedos finos."),
    ("inseto", "Asas de Inseto", 28, "Comum", {"attack_speed_pct": 0.03}, "Quatro lâminas transparentes e azuladas, com veias."),
    ("anjo", "Asas de Anjo Simples", 29, "Comum", {"hp_regen_pct": 0.001}, "Penas brancas, limpas, de anjo de capela."),
    ("folhas", "Asas de Folhas", 30, "Incomum", {"hp_regen_pct": 0.0015}, "Folhas presas num galho, com florzinhas brancas; soltam folhas pelo caminho."),
    ("cristal", "Asas de Cristal", 32, "Incomum", {"magic_resist_pct": 0.05}, "Lâminas de cristal azul que brilham por dentro."),
    ("vapor", "Asas de Vapor", 34, "Incomum", {"dodge_pct": 0.03}, "Nuvens enroladas em redemoinho, macias como fumaça."),
    ("borboleta", "Asas de Borboleta", 36, "Incomum", {"move_speed_pct": 0.03}, "Laranja e roxo com pintas brancas na borda."),
    ("ossos", "Asas de Ossos", 38, "Incomum", {"crit_pct": 0.03}, "Só o esqueleto da asa: úmero e quatro dedos com garra."),
    ("tempestade", "Asas de Tempestade", 40, "Rara", {"attack_speed_pct": 0.05}, "Raios azuis no lugar das penas, estalando sem parar."),
    ("gelo", "Asas de Gelo", 42, "Rara", {"magic_resist_pct": 0.07}, "Penas de gelo com brilho de geada."),
    ("fogo", "Asas de Fogo", 44, "Rara", {"power_pct": 0.02}, "Penas em brasa que pulsam e soltam faíscas."),
    ("sombrias", "Asas Sombrias", 46, "Rara", {"dodge_pct": 0.04}, "Penas roxo-escuras que deixam um rastro de sombra."),
    ("celestiais", "Asas Celestiais", 48, "Rara", {"resource_regen_pct": 0.12}, "Penas douradas com uma auréola de luz por cima."),
    ("draconicas", "Asas Dracônicas", 50, "Épica", {"damage_reduction_pct": 0.03}, "Couro de dragão vermelho com bordas douradas."),
    ("demoniacas", "Asas Demoníacas", 52, "Épica", {"crit_pct": 0.05}, "Membrana negra com veias de fogo e o selo do olho vermelho."),
    ("vazio", "Asas do Vazio", 54, "Épica", {"cdr_pct": 0.05}, "Um céu estrelado roxo dentro da asa e um buraco negro nas costas."),
    ("runicas", "Asas Rúnicas", 55, "Épica", {"resource_regen_pct": 0.18}, "Pedra escura com runas ciano que correm pela asa."),
    ("arcanjo", "Asas do Arcanjo", 56, "Épica", {"healing_received_pct": 0.1, "hp_regen_pct": 0.002}, "Grandes, brancas e douradas, com auréola e uma gema azul."),
    ("dragao_anciao", "Asas de Dragão Ancião", 58, "Lendária", {"power_pct": 0.03, "damage_reduction_pct": 0.03}, "Escamas negras rachadas com lava viva por dentro."),
    ("fenrir", "Asas do Fenrir", 58, "Lendária", {"attack_speed_pct": 0.06, "move_speed_pct": 0.02}, "Gelo em forma de asa com duas cabeças de lobo uivando."),
    ("eclipse", "Asas do Eclipse", 59, "Lendária", {"crit_pct": 0.06, "power_pct": 0.02}, "Metade sol dourado, metade noite roxa, e o sol negro no meio."),
    ("tempo", "Asas do Tempo", 60, "Lendária", {"cdr_pct": 0.08}, "Penas de ouro e azul com engrenagens e o relógio parado nas costas."),
    ("criacao", "Asas da Criação", 60, "Lendária", {"xp_pct": 0.05, "hp_regen_pct": 0.002, "resource_regen_pct": 0.15}, "Todas as cores da luz, auréola e o sol nascendo nas costas."),
]

ASAS_DOS_SETS = [   # (vinham presas no peitoral do conjunto — Tools/separar_asas_dos_sets.py)
    ("cavaleiro_sagrado", "Asas do Cavaleiro Sagrado", 45, {"healing_received_pct": 0.06}, "Penas brancas e douradas do conjunto Sagrado."),
    ("gelo_eterno", "Asas de Gelo Eterno", 45, {"magic_resist_pct": 0.06}, "Lascas de gelo do conjunto Gelo Eterno."),
    ("dragao_fogo", "Asas do Dragão de Fogo", 48, {"power_pct": 0.02}, "Membrana de lava rachada do conjunto Dragão de Fogo."),
    ("celestial", "Asas Celestiais do Arqueiro", 50, {"crit_pct": 0.03}, "Penas de prata e ouro do conjunto Celestial."),
    ("demoniaco", "Asas do Demoníaco", 50, {"crit_pct": 0.04}, "Membrana negra com veias de fogo do conjunto Demoníaco."),
    ("anjo_caido", "Asas do Anjo Caído", 50, {"dodge_pct": 0.04}, "Penas negras do conjunto Anjo Caído."),
]

CAPAS = [  # id, nome, nível, extra, descrição, origem
    ("heroi", "Capa do Herói", 8, {"power_pct": 0.02}, "Vermelha, rasgada na barra, filete de ouro e o leão dourado nas costas.", "Forja do Brás (Porto)."),
    ("mago", "Capa do Mago", 10, {"resource_regen_pct": 0.06}, "Roxa com estrelas e lua douradas e uma gema violeta.", "Forja do Brás (Porto)."),
    ("corvo", "Capa do Corvo", 14, {"dodge_pct": 0.02}, "Penas azul-noite em camadas e o crânio de corvo na gola.", "Aurélio, o Mestre das Asas (Astrália, perto da Praça das Lunetas)."),
    ("pirata", "Capa do Pirata", 20, {"move_speed_pct": 0.03}, "Preta e rasgada, com a caveira e os ossos cruzados.", "Aurélio, o Mestre das Asas (Astrália)."),
    ("sombria", "Capa Sombria", 24, {"dodge_pct": 0.03}, "Fumaça cinza que se mexe, caveira acorrentada.", "Aurélio, o Mestre das Asas (Astrália)."),
    ("amaldicoada", "Capa Amaldiçoada", 26, {"resource_regen_pct": 0.08}, "Pano pálido e furado com runas azuis que acendem.", "RARA: chefes do nível 24 ao 36."),
    ("lava", "Capa de Lava", 34, {"power_pct": 0.04}, "Rocha rachada com lava acesa por dentro, soltando brasas.", "RARA: chefes do nível 32 ao 44."),
    ("dragao", "Capa do Dragão", 44, {"power_pct": 0.04, "damage_reduction_pct": 0.02}, "Escamas vermelhas e o elmo dourado de dragão com chifres.", "RARA: chefes do nível 42 ao 52; Loja de Cristais."),
    ("celestial", "Capa Celestial", 46, {"resource_regen_pct": 0.1, "crit_pct": 0.02}, "Um céu de galáxia com estrelas que piscam e a lua dourada.", "RARA: chefes do nível 44 ao 54; Loja de Cristais."),
    ("morte", "Capa da Morte", 50, {"crit_pct": 0.04, "power_pct": 0.03}, "Roxa rasgada, fumaça viva e a caveira acesa presa em correntes.", "RARA: chefes do nível 48 ao 58; Loja de Cristais."),
    ("luz", "Capa da Luz", 54, {"damage_reduction_pct": 0.03, "max_health_pct": 0.03}, "Raios de sol em ouro e creme, com o sol no peito.", "RARA: chefes do nível 52 ao 60; Loja de Cristais."),
    ("tempo", "Capa do Tempo", 58, {"cdr_pct": 0.05, "move_speed_pct": 0.04}, "Couro marrom com engrenagens gravadas e o relógio de bolso.", "RARA: chefes do nível 56+; Loja de Cristais."),
]
CAPAS_REPAGINADAS = {"capa_real": "Capa Real", "capa_gelo": "Capa de Gelo", "capa_folhas": "Capa da Floresta"}

SKINS = [  # id, nome, nível, armadura, vida, descrição, origem
    ("macaco_vermelho", "Homem Macaco Vermelho", 16, "Juba vermelha espetada, olhos amarelos bravos e um rabo enorme.", "Carroça do Bento (Mascate)."),
    ("caveira", "Esqueleto", 22, "Esqueleto inteiro: crânio de olhos laranja acesos e costelas.", "Carroça do Bento (Mascate)."),
    ("fantasma", "Fantasma (Lençol)", 26, "Um lençol com dois olhos acesos e a barra rasgada.", "Carroça do Bento (Mascate)."),
    ("alien", "Alien Grey Verde", 30, "Cabeção verde e olhos pretos amendoados.", "Carroça do Bento (Mascate)."),
    ("tubarao", "Tubarão", 38, "Cabeça de tubarão com a bocarra de dentes, barbatana e rabo.", "RARO: chefes do nível 36 ao 48; Loja de Cristais."),
    ("lava", "Boneco de Lava", 52, "Corpo de rocha com lava acesa nas rachaduras, soltando brasas.", "RARO: chefes do nível 50+; Loja de Cristais."),
]


def ler(f):
    return json.load(open(os.path.join(C, f), encoding="utf-8")) if os.path.exists(os.path.join(C, f)) else []


def salvar(f, d):
    json.dump(d, open(os.path.join(C, f), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


def por_id(lista, novos):
    idx = {d["id"]: i for i, d in enumerate(lista)}
    for n in novos:
        if n["id"] in idx:
            velho = lista[idx[n["id"]]]
            n["price"] = velho.get("price", n.get("price", 0))   # o preço é da régua (regular_economia)
            lista[idx[n["id"]]] = n
        else: lista.append(n)
    return lista


def texto_stats(s):
    nomes = {"power_pct": "ataque", "move_speed_pct": "velocidade", "crit_pct": "crítico", "dodge_pct": "esquiva", "attack_speed_pct": "vel. de ataque",
             "hp_regen_pct": "regeneração de vida", "magic_resist_pct": "resistência mágica", "resource_regen_pct": "regeneração de recurso",
             "damage_reduction_pct": "redução de dano", "cdr_pct": "recarga", "healing_received_pct": "cura recebida", "xp_pct": "XP",
             "armor_pct": "armadura", "max_health_pct": "vida"}
    out = []
    for k, v in s.items():
        if k == "armor": out.append(f"armadura +{v}")
        elif k == "max_health": out.append(f"vida +{v}")
        elif k in nomes: out.append(f"{nomes[k]} +{v * 100:g}%")
    return ", ".join(out)


def main():
    asas = []
    for key, nome, L, rar, extra, desc in ASAS:
        # 14/11: mesma curva das asas antigas (alinhar_poder.py) × raridade: comum 0,75 · incomum 0,85 · rara 1 · épica 1,1 · lendária 1,2
        t = {"Comum": 0.75, "Incomum": 0.85, "Rara": 1.0, "Épica": 1.1, "Lendária": 1.2}[rar]
        s = {"power_pct": round(min(0.30, 0.0046 * L * t), 3), "move_speed_pct": round(min(0.22, (0.004 * L + 0.01) * t), 3),
             "armor": round(1.7 * L * t), "max_health": round(5.4 * L * t)}
        for k, v in extra.items(): s[k] = round(s.get(k, 0) + v, 4)
        fonte = ("Aurélio, o Mestre das Asas (Astrália, perto da Praça das Lunetas)." if rar in ("Comum", "Incomum")
                 else f"{rar.upper()}: chefes do nível {L - 4} ao {L + 6}; Loja de Cristais (menu do TAB).")
        asas.append({"id": f"asas_{key}", "display_name": nome, "category": "wings", "subtype": f"asa_{key}", "min_level": L,
                     "allowed_classes": TODAS, "handedness": 0, "base_stats": s, "affix_pool": "", "temperable": False, "trade_rule": "free",
                     "stack_limit": 1, "icon_asset_id": f"ico_asas_{key}", "model_asset_id": f"asas_{key}",
                     "summary": f"{rar}. {desc} {texto_stats(s)}. G = voar.", "price": 0, "source": fonte})
    # 14/11: as asas que vinham presas em 6 conjuntos lendários do trabalhador viram itens próprios (épicas, do nível do conjunto)
    for key, nome, L, extra, desc in ASAS_DOS_SETS:
        t = 1.1
        s = {"power_pct": round(min(0.30, 0.0046 * L * t), 3), "move_speed_pct": round(min(0.22, (0.004 * L + 0.01) * t), 3),
             "armor": round(1.7 * L * t), "max_health": round(5.4 * L * t)}
        for k, v in extra.items(): s[k] = round(s.get(k, 0) + v, 4)
        asas.append({"id": f"asas_set_{key}", "display_name": nome, "category": "wings", "subtype": f"pack:arm_{key}_asas", "min_level": L,
                     "allowed_classes": TODAS, "handedness": 0, "base_stats": s, "affix_pool": "", "temperable": False, "trade_rule": "free",
                     "stack_limit": 1, "icon_asset_id": f"ico_asas_set_{key}", "model_asset_id": f"asas_set_{key}",
                     "summary": f"Épica. {desc} {texto_stats(s)}. G = voar.", "price": 0,
                     "source": f"ÉPICA: chefes do nível {L - 4} ao {L + 6} (junto do conjunto); Loja de Cristais."})
    salvar("items_asas_colecao.json", asas)

    capas = ler("items_capas.json")
    novas = []
    for key, nome, L, extra, desc, fonte in CAPAS:
        s = {"armor": round(1.7 * L + 6), "max_health": round(3.1 * L + 10)}
        s.update(extra)
        novas.append({"id": f"capa_{key}", "display_name": nome, "category": "cape", "subtype": key, "min_level": L, "allowed_classes": TODAS,
                      "handedness": 0, "base_stats": s, "affix_pool": "", "temperable": False, "trade_rule": "free", "stack_limit": 1,
                      "icon_asset_id": f"ico_capa_{key}", "model_asset_id": f"capa_{key}",
                      "summary": f"{desc} Vai no espaço de asas (asa OU capa).", "price": 0, "source": fonte})
    capas = por_id(capas, novas)
    for d in capas:
        if d["id"] in CAPAS_REPAGINADAS: d["display_name"] = CAPAS_REPAGINADAS[d["id"]]
    salvar("items_capas.json", capas)

    brac = ler("items_braceletes.json")
    novos = []
    for key, nome, L, desc, fonte in SKINS:
        arm, vida = round(0.46 * L + 3), round(3.4 * L + 20)
        novos.append({"id": f"bracelete_{key}", "display_name": f"Bracelete: {nome}", "category": "bracelet", "subtype": key, "min_level": L,
                      "allowed_classes": [], "handedness": 0, "base_stats": {"armor": arm, "max_health": vida}, "affix_pool": "", "temperable": False,
                      "trade_rule": "free", "stack_limit": 1, "icon_asset_id": f"ico_bracelete_{key}", "model_asset_id": f"bracelete_{key}",
                      "summary": f"SKIN: só a aparência, por cima da armadura (espaço Skin; o olhinho esconde). {desc} +{arm} de armadura e +{vida} de vida.",
                      "price": 0, "source": fonte})
    salvar("items_braceletes.json", por_id(brac, novos))

    # lojas: comuns e incomuns
    shops = ler("shops.json")
    quer = {"loja_ferreiro": ["capa_heroi", "capa_mago"],

            "loja_brasaforte_forja": ["joia_mare", "joia_brasa", "joia_vazio"],   # 14/11: joias à venda na cidade do fim do jogo
            "loja_mascate": ["bracelete_macaco_vermelho", "bracelete_caveira", "bracelete_fantasma", "bracelete_alien"]}
    # 14/11 tarde: as asas comuns/incomuns e as capas do meio ficam numa loja DE VERDADE (o Santuário é só do adm)
    asas_loja = [f"asas_{k}" for k, _, _, r, _, _ in ASAS if r in ("Comum", "Incomum")] + ["capa_corvo", "capa_pirata", "capa_sombria"]
    shops = [x for x in shops if x["id"] != "loja_astralia_asas"] + [{"id": "loja_astralia_asas", "name": "Asas e Capas do Aurélio", "items": asas_loja}]
    m = ler("map_astralia.json")
    if not any(n["id"] == "aurelio_asas" for n in m["npcs"]):
        m["npcs"].append({"id": "aurelio_asas", "name": "Aurélio, o Mestre das Asas", "role": "mercador", "x": -9, "y": 0, "facing": 90,
                          "greeting": "Asas de pano, de penas, de cristal... e capas para quem ainda não voa. As raras? Essas só os chefes e os cristais.",
                          "look": "arcanista", "shop": "loja_astralia_asas"})
        salvar("map_astralia.json", m)
    for s in shops:
        for i in quer.get(s["id"], []):
            if i not in s["items"]: s["items"].append(i)
    salvar("shops.json", shops)
    print(f"coleção: {len(asas)} asas, {len(novas)} capas novas (+3 repaginadas), {len(novos)} skins")


main()

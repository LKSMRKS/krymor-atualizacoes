"""Conjuntos em peças (11/11): capacete, peitoral, luvas e botas, um conjunto por classe (não serve em outra classe),
mais anéis, colares e capas. Gera Content/items_conjuntos.json. Uso: python3 Tools/gerar_conjuntos.py

A matemática: uma roupa inteira de antes (armadura + elmo do mesmo nível) = TOTAL. As 4 peças somam ~TOTAL x 1,1:
peitoral 55%, capacete 20%, luvas 12%, botas 13% (de armadura e de vida). Só o peitoral fica com ~55%: quem veste
só ele e um elmo velho continua parecido com antes; o conjunto completo vale um pouco mais e ainda ganha o bônus de set.
Luvas dão um pouco de ataque; botas, um pouco de velocidade.
"""
import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# 11/11 (pedido: "5 partes igual MU"): cabeça, mãos/braços, tronco, calça (coxas) e botas (pernas e pés)
SPLIT = {"peitoral": 0.40, "calca": 0.18, "capacete": 0.16, "botas": 0.14, "luvas": 0.12}
CAT = {"peitoral": "armor", "capacete": "helmet", "luvas": "gloves", "botas": "boots", "calca": "pants"}
NOME = {"peitoral": "Peitoral", "capacete": "Capacete", "luvas": "Luvas", "botas": "Botas", "calca": "Calça"}
PARTES = ("capacete", "peitoral", "calca", "luvas", "botas")
GRID = {"peitoral": "", "capacete": "", "luvas": "", "botas": ""}

# (modelo, classe, nível, nome do conjunto, armadura total, vida total, extra no peitoral, extra no capacete, preço base, origem)
SETS = [
    ("arm_guerreiro", "guerreiro", 8, "do Recruta", 240, 120, {"move_speed_pct": -0.04}, {}, 900,
     "Brás, o Ferreiro (Porto)."),
    ("arm_cavaleiro", "guerreiro", 20, "do Cavaleiro Azul", 430, 230, {"move_speed_pct": -0.03}, {}, 9000,
     "Raro: chefes (Capataz, Rei das Pinças, Sentinela, Guardião, Rainha das Cascas, Tirano) e Caranguejo-Geodo."),
    ("arm_patrulheiro", "arqueiro", 8, "do Patrulheiro", 170, 110, {"move_speed_pct": 0.01}, {}, 900,
     "Brás, o Ferreiro (Porto)."),
    ("arm_elfico", "arqueiro", 20, "Élfico de Folhas", 300, 210, {"move_speed_pct": 0.02}, {}, 9000,
     "Raro: chefes (Capataz, Rei das Pinças, Sentinela, Guardião, Rainha das Cascas, Tirano) e Caranguejo-Geodo."),
    ("arm_mago", "mago", 10, "do Aprendiz Arcano", 130, 110, {"resource_regen_pct": 0.4}, {"resource_regen_pct": 0.2}, 1100,
     "Brás, o Ferreiro (Porto)."),
    ("arm_feiticeiro", "bruxa", 10, "da Feiticeira Sombria", 130, 110, {"resource_regen_pct": 0.4}, {"resource_regen_pct": 0.2}, 1100,
     "Brás, o Ferreiro (Porto)."),
]

def piece(model, cls, lvl, setname, armor, hp, chest_x, helm_x, price, source, part):
    k = SPLIT[part]
    stats = {"armor": round(armor * k), "max_health": round(hp * k)}
    if part == "peitoral": stats.update(chest_x)
    if part == "capacete": stats.update(helm_x)
    # 11/11: atributos exclusivos de cada peça (como Diablo/MU): luva = ataque; bota = mobilidade; peito = tanque;
    # capacete = recursos; calça = estabilidade. Crescem com o nível do conjunto (3 faixas).
    t = 0 if lvl < 15 else 1 if lvl < 35 else 2
    if part == "luvas": stats.update({"power_pct": (0.02, 0.04, 0.05)[t], "attack_speed_pct": (0.04, 0.06, 0.08)[t], "crit_pct": (0.01, 0.02, 0.03)[t]})
    if part == "botas": stats.update({"move_speed_pct": (0.03, 0.04, 0.05)[t], "dodge_pct": (0.02, 0.03, 0.04)[t]})
    if part == "peitoral": stats.update({"thorns_pct": (0.05, 0.08, 0.12)[t], "damage_reduction_pct": (0.02, 0.03, 0.05)[t]})
    if part == "capacete": stats.update({"cdr_pct": (0.03, 0.05, 0.06)[t], "xp_pct": (0.03, 0.05, 0.06)[t]})
    if part == "calca": stats.update({"stamina_regen_pct": (0.10, 0.18, 0.25)[t], "max_health": round(hp * k * 1.15)})
    desc = ", ".join(f"{label} {fmt}" for label, fmt in [
        ("armadura +%d" % stats["armor"], ""), ("vida +%d" % stats["max_health"], "")]).replace(" ,", ",").strip()
    extra = []
    if stats.get("power_pct"): extra.append("ataque +%d%%" % round(stats["power_pct"] * 100))
    if stats.get("move_speed_pct"): extra.append("velocidade %+d%%" % round(stats["move_speed_pct"] * 100))
    if stats.get("resource_regen_pct"): extra.append("regeneração +%d%%" % round(stats["resource_regen_pct"] * 100))
    for key, label in (("attack_speed_pct", "vel. de ataque"), ("crit_pct", "crítico"), ("dodge_pct", "esquiva"), ("thorns_pct", "espinhos (devolve dano)"),
                       ("damage_reduction_pct", "redução de dano"), ("cdr_pct", "recarga"), ("xp_pct", "experiência"), ("stamina_regen_pct", "fôlego")):
        if stats.get(key): extra.append("%s %s%d%%" % (label, "-" if key == "cdr_pct" else "+", round(stats[key] * 100)))
    summary = ("Armadura +%d, vida +%d" % (stats["armor"], stats["max_health"])) + ("".join(", " + e for e in extra)) + "."
    return {
        "id": f"{model}_{part}", "display_name": f"{NOME[part]} {setname}", "category": CAT[part], "subtype": part,
        "min_level": lvl, "allowed_classes": [cls], "handedness": 0, "base_stats": stats,
        "affix_pool": "armor", "temperable": True, "trade_rule": "free", "stack_limit": 1,
        "icon_asset_id": f"ico_{model}_{part}", "model_asset_id": f"{model}:{part}",
        "summary": summary + " Só para " + {"guerreiro": "Guerreiro", "arqueiro": "Arqueiro", "mago": "Mago", "bruxa": "Bruxa"}[cls] + ".",
        "price": round(price * (0.35 if part == "peitoral" else 0.18 if part == "calca" else 0.16)), "source": source,
        "set": "set_" + model,
        # 11/11 (referência do MU): o bônus cresce a cada peça — 2 vida e ataque, 3 dano dobrado e armadura, 4 crítico
        "set_bonus": {"2": {"max_health": round(hp * 0.08), "power_pct": 0.03}, "3": {"double_pct": 0.04, "armor": round(armor * 0.05)},
                      "4": {"armor": round(armor * 0.08), "power_pct": 0.04, "crit_pct": 0.03},
                      "5": {"armor": round(armor * 0.06), "power_pct": 0.04, "crit_pct": 0.03, "double_pct": 0.03}},   # conjunto completo
    }

items = []
for s in SETS:
    for part in PARTES:
        items.append(piece(*s, part))

# 11/11 (pacote "Monstros e Armaduras"): 6 armaduras lendárias do Meshy, já separadas pelo trabalhador em 6 malhas
# (capacete, peitoral, braçadeiras, luvas, perneiras, botas). No jogo são 4 peças que juntas montam o conjunto inteiro:
# capacete; peitoral (com ombreiras/asas); luvas + braçadeiras; botas + perneiras (perna toda, com a saia). Valores pela curva das armaduras (≈ 1,32 × a armadura do nível; vida ≈ 1,64 ×); Mago e Bruxa com menos
# armadura e mais regeneração. Caem raramente de chefes/elites do nível (GameServer: LegendarySetDrop).
LENDARIAS = [
    ("armadura_dragao", "guerreiro", 30, "do Dragão Rubro", {"move_speed_pct": -0.02}, {}),
    ("armadura_abissal", "guerreiro", 50, "Abissal", {"move_speed_pct": -0.02}, {}),
    ("armadura_lobo", "arqueiro", 30, "do Lobo Sombrio", {"move_speed_pct": 0.03}, {}),
    ("armadura_fenix", "arqueiro", 50, "da Valquíria Fênix", {"move_speed_pct": 0.03}, {}),
    ("armadura_anjo", "mago", 40, "do Anjo Alado", {"resource_regen_pct": 0.5}, {"resource_regen_pct": 0.25}),
    ("armadura_serpente", "bruxa", 40, "da Serpente", {"resource_regen_pct": 0.5}, {"resource_regen_pct": 0.25}),
]
MALHAS = {"calca": "perneiras", "luvas": "luvas+bracadeiras"}   # 5 partes como no MU: a calça é a perneira (com a saia)
for model, cls, lvl, nome, chest_x, helm_x in LENDARIAS:
    caster = cls in ("mago", "bruxa")
    armor = round(1.32 * (200 + (lvl - 11) * 14) * (0.6 if caster else 1.0))
    hp = round(1.64 * (80 + lvl * 3) * (1.1 if caster else 1.0))
    for part in PARTES:
        it = piece(model, cls, lvl, nome, armor, hp, chest_x, helm_x, round(12000 * 1.11 ** (lvl - 20)),
                   "Lendário: chefes e elites a partir do nível %d." % lvl, part)
        it["model_asset_id"] = f"{model}:{MALHAS.get(part, part)}"
        it["display_name"] = it["display_name"]
        it["lendario"] = True
        it["set_bonus"] = {"2": {"max_health": round(hp * 0.10), "power_pct": 0.04}, "3": {"double_pct": 0.06, "armor": round(armor * 0.08)},
                           "4": {"armor": round(armor * 0.10), "power_pct": 0.05, "crit_pct": 0.05},
                           "5": {"armor": round(armor * 0.08), "power_pct": 0.06, "crit_pct": 0.05, "double_pct": 0.04}}   # lendário completo
        items.append(it)

def jewel(id_, name, cat, lvl, stats, price, summary, source):
    return {"id": id_, "display_name": name, "category": cat, "subtype": cat, "min_level": lvl,
            "allowed_classes": ["guerreiro", "mago", "bruxa", "arqueiro"], "handedness": 0, "base_stats": stats,
            "affix_pool": "", "temperable": False, "trade_rule": "free", "stack_limit": 1,
            "icon_asset_id": "ico_" + id_, "model_asset_id": id_, "summary": summary, "price": price, "source": source}

items += [
    jewel("anel_cobre", "Anel de Cobre", "ring", 3, {"max_health": 20}, 250, "Vida +20. Dá para usar dois anéis.", "Brás, o Ferreiro (Porto)."),
    jewel("anel_mare", "Anel da Maré", "ring", 8, {"resource_regen_pct": 0.10}, 900, "Regeneração do recurso +10%.", "Raro: chefes e Caranguejo-Geodo."),
    jewel("anel_brasa", "Anel de Brasa", "ring", 15, {"power_pct": 0.03, "max_health": 25}, 2600, "Ataque +3%, vida +25.", "Raro: chefes e Caranguejo-Geodo."),
    jewel("colar_conchas", "Colar de Conchas", "amulet", 3, {"armor": 10, "max_health": 15}, 300, "Armadura +10, vida +15.", "Brás, o Ferreiro (Porto)."),
    jewel("colar_farol", "Colar do Farol", "amulet", 12, {"power_pct": 0.03, "max_health": 30}, 2400, "Ataque +3%, vida +30.", "Raro: chefes e Caranguejo-Geodo."),
    jewel("capa_viajante", "Capa de Viajante", "cape", 5, {"armor": 8, "move_speed_pct": 0.02}, 500,
          "Armadura +8, velocidade +2%. Vai no espaço de asas (asa OU capa).", "Brás, o Ferreiro (Porto)."),
    jewel("capa_rubra", "Capa Rubra", "cape", 15, {"armor": 20, "max_health": 40}, 3000,
          "Armadura +20, vida +40. Vai no espaço de asas (asa OU capa).", "Raro: chefes e Caranguejo-Geodo."),
]
for it in items:
    if it["category"] == "cape": it["subtype"] = it["id"].split("_", 1)[1]

out = os.path.join(ROOT, "Content", "items_conjuntos.json")
json.dump(items, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(items), "itens ->", out)

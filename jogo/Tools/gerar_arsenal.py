"""Arsenal da noite (11/11): os modelos do trabalhador (Assets/Models/armas/arma_*.glb) viram itens de verdade.
Gera Content/items_arsenal.json. Uso: python3 Tools/gerar_arsenal.py

Valores: poder da arma segue a mesma curva das armas que já existem (≈ 7,4 + 2,6 × nível — igual a gerar_regioes.py),
corrigido pela forma (faca rápida e fraca, machado de guerra lento e forte): o dano por segundo fica parecido entre as
formas do mesmo nível; as de duas mãos ganham ~15% (não usam escudo). Quatro qualidades por forma:
Enferrujada (nv 2) → Comum (nv 8) → Refinada (nv 20) → Primorosa (nv 34) — "Primorosa" para não confundir com o
item Excelente (qualidade com atributos). Caem de qualquer monstro do nível certo (GameServer.Map: ArsenalDrop).
"""
import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
QUAL = [("enferrujada", "Enferrujada", 2, 0.35), ("comum", "", 8, 1.0), ("refinada", "Refinada", 20, 3.2), ("excelente", "Primorosa", 34, 9.0)]

# forma: (nome, classe, subtipo, mãos, intervalo ms, multiplicador de poder, extras, gênero do adjetivo)
FORMAS = {
    "faca": ("Faca", "guerreiro", "dagger", 1, 650, 0.68, {}, "a"),
    "adaga": ("Adaga", "guerreiro", "dagger", 1, 750, 0.78, {}, "a"),
    "espada_curta": ("Espada Curta", "guerreiro", "sword", 1, 950, 0.98, {}, "a"),
    "espada_longa": ("Espada Longa", "guerreiro", "sword", 2, 1150, 1.38, {"reach_m": 0.4}, "a"),
    "katana": ("Katana", "guerreiro", "sword", 2, 1000, 1.22, {"reach_m": 0.3}, "a"),
    "clava": ("Clava", "guerreiro", "club", 1, 1000, 1.0, {"basic_stagger": 1}, "a"),
    "maca": ("Maça de Aletas", "guerreiro", "hammer", 1, 1100, 1.12, {"basic_stagger": 1}, "a"),
    "machado_mao": ("Machadinha", "guerreiro", "axe", 1, 1100, 1.1, {"basic_stagger": 1}, "a"),
    "machado_guerra": ("Machado de Guerra", "guerreiro", "axe", 2, 1300, 1.6, {"basic_stagger": 1, "reach_m": 0.3}, "o"),
    "lanca": ("Lança", "guerreiro", "spear", 2, 1050, 1.2, {"reach_m": 1.3, "basic_arc": 55}, "a"),
    "arco_curto": ("Arco Curto", "arqueiro", "bow", 2, 850, 0.92, {"projectile_speed_pct": 0.1}, "o"),
    "arco_longo": ("Arco Longo", "arqueiro", "bow", 2, 1000, 1.12, {"projectile_speed_pct": 0.3}, "o"),
}
VARINHAS = [("n1", "Varinha de Galho", 3), ("n2", "Varinha de Osso Entalhado", 14), ("n3", "Varinha do Cristal Flutuante", 30)]


def power(lvl):
    return 7.4 + 2.6 * lvl


def adj(word, g):
    if not word: return ""
    return " " + (word[:-1] + "o" if g == "o" and word.endswith("a") else word)


items = []
for key, (nome, cls, sub, hands, interval, mult, extra, g) in FORMAS.items():
    for q, qname, lvl, pmult in QUAL:
        stats = {"weapon_power": round(power(lvl) * mult), "attack_interval_ms": interval, **extra}
        items.append({
            "id": f"arsenal_{key}_{q}", "display_name": nome + adj(qname, g), "category": "weapon", "subtype": sub,
            "min_level": lvl, "allowed_classes": [cls], "handedness": hands, "base_stats": stats,
            "affix_pool": "physical_weapon", "temperable": True, "trade_rule": "free", "stack_limit": 1,
            "icon_asset_id": f"ico_{key}_{q}", "model_asset_id": f"arma_{key}_{q}",
            "summary": f"{'Duas mãos' if hands == 2 else 'Uma mão'}, ataque a cada {interval / 1000:.2f} s.".replace(".", ",", 1),
            "price": round(120 * pmult * (1.11 ** max(0, lvl - 11)) if lvl > 11 else 120 * pmult), "source": "Cai de monstros do nível certo.",
        })
items.append({"id": "arsenal_graveto", "display_name": "Graveto", "category": "weapon", "subtype": "club", "min_level": 1,
              "allowed_classes": ["guerreiro"], "handedness": 1, "base_stats": {"weapon_power": 8, "attack_interval_ms": 1000},
              "affix_pool": "physical_weapon", "temperable": True, "trade_rule": "free", "stack_limit": 1,
              "icon_asset_id": "ico_graveto", "model_asset_id": "arma_graveto", "summary": "Melhor que nada.", "price": 10,
              "source": "Cai de monstros do nível certo."})
items.append({"id": "arsenal_graveto_pregos", "display_name": "Graveto com Pregos", "category": "weapon", "subtype": "club", "min_level": 4,
              "allowed_classes": ["guerreiro"], "handedness": 1, "base_stats": {"weapon_power": 18, "attack_interval_ms": 1000, "basic_stagger": 1},
              "affix_pool": "physical_weapon", "temperable": True, "trade_rule": "free", "stack_limit": 1,
              "icon_asset_id": "ico_graveto_pregos", "model_asset_id": "arma_graveto_pregos", "summary": "Pregos tortos. Machuca.", "price": 60,
              "source": "Cai de monstros do nível certo."})
for n, nome, lvl in VARINHAS:
    items.append({"id": f"arsenal_varinha_{n}", "display_name": nome, "category": "weapon", "subtype": "talisman", "min_level": lvl,
                  "allowed_classes": ["bruxa"], "handedness": 1,
                  "base_stats": {"weapon_power": round(power(lvl) * 1.03), "attack_interval_ms": 1000, "projectile_speed_pct": 0.2},
                  "affix_pool": "physical_weapon", "temperable": True, "trade_rule": "free", "stack_limit": 1,
                  "icon_asset_id": f"ico_varinha_bruxa_{n}", "model_asset_id": f"arma_varinha_bruxa_{n}",
                  "summary": "Varinha da Bruxa (no lugar do talismã).", "price": 150 if lvl < 10 else 1500 if lvl < 25 else 9000,
                  "source": "Cai de monstros do nível certo."})

# 11/11 (pacote de armas do Meshy do usuário, Tools/arte/armas_meshy_lote.sh): armas lendárias em 3 faixas pelos detalhes —
# início (ferro e osso), meio (bicho e veneno), fim (ouro, lava, dragão). Caem raramente de chefes/elites do nível
# (GameServer.LegendarySetDrop, junto das armaduras lendárias). Poder +12% sobre a curva (são as melhores do nível).
LENDARIAS = [
    # id, nome, classe, subtipo, mãos, intervalo, mult, extras, nível
    ("machado_presa_javali", "Machado de Presa de Javali", "guerreiro", "axe", 2, 1300, 1.6, {"basic_stagger": 1, "reach_m": 0.3}, 12),
    ("martelo_chifre_carneiro", "Martelo de Chifre de Carneiro", "guerreiro", "hammer", 2, 1250, 1.55, {"basic_stagger": 1, "reach_m": 0.3}, 15),
    ("maca_escorpiao", "Maça do Escorpião", "guerreiro", "hammer", 1, 1100, 1.12, {"basic_stagger": 1}, 18),
    ("machado_lobo_sombrio", "Machado do Lobo Sombrio", "guerreiro", "axe", 2, 1300, 1.6, {"basic_stagger": 1, "reach_m": 0.3}, 26),
    ("cajado_cobra_verde", "Cajado da Cobra Verde", "mago", "staff", 2, 1000, 0.95, {"aoe_radius_bonus": 0.6, "resource_regen_pct": 0.15}, 28),
    ("lanca_valquiria", "Lança Alada da Valquíria", "guerreiro", "spear", 2, 1050, 1.25, {"reach_m": 1.3, "basic_arc": 55}, 32),
    ("glaive_carmesim", "Glaive Gêmea Carmesim", "guerreiro", "spear", 2, 1000, 1.25, {"reach_m": 1.0, "basic_arc": 80}, 36),
    ("cutelo_abissal", "Cutelo Abissal do Tubarão", "guerreiro", "sword", 1, 950, 1.05, {"reach_m": 0.3}, 44),
    ("machado_leao_dourado", "Machado do Leão Dourado", "guerreiro", "axe", 2, 1300, 1.65, {"basic_stagger": 1, "reach_m": 0.3}, 48),
    ("martelo_carneiro_fundido", "Martelo Fundido do Carneiro", "guerreiro", "hammer", 2, 1250, 1.65, {"basic_stagger": 1, "reach_m": 0.3}, 53),
    ("montante_dragao_rubro", "Montante do Dragão Rubro", "guerreiro", "sword", 2, 1150, 1.45, {"reach_m": 0.5}, 58),
]
for key, nome, cls, sub, hands, interval, mult, extra, lvl in LENDARIAS:
    faixa = "início" if lvl < 22 else "meio" if lvl < 40 else "fim de jogo"
    items.append({"id": f"lendaria_{key}", "display_name": nome, "category": "weapon", "subtype": sub, "min_level": lvl,
                  "allowed_classes": [cls], "handedness": hands,
                  "base_stats": {"weapon_power": round(power(lvl) * mult * 1.12), "attack_interval_ms": interval, **extra},
                  "affix_pool": "physical_weapon", "temperable": True, "trade_rule": "free", "stack_limit": 1,
                  "icon_asset_id": f"ico_{key}", "model_asset_id": f"arma_{key}",
                  "summary": f"Arma lendária ({faixa}). {'Duas mãos' if hands == 2 else 'Uma mão'}.",
                  "price": round(2000 * (1.11 ** max(0, lvl - 11))), "source": f"Lendária: chefes e elites a partir do nível {lvl}."})

out = os.path.join(ROOT, "Content", "items_arsenal.json")
json.dump(items, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(items), "armas ->", out)

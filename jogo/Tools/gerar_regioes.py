#!/usr/bin/env python3
"""
Gera o conteúdo da expansão até o midgame (níveis 17–60) a partir de tabelas compactas:
mapas das regiões R02–R08 e dungeons D02–D08, monstros, comportamentos, itens, materiais, receitas, plantas,
consumíveis, loot, lojas e missões. Também acrescenta (de forma idempotente) estações, horta, armazém, veios e
portais nos mapas feitos à mão (Porto, Costa, Vale) e atualiza world.json.

Os arquivos gerados têm sufixo _midgame (o jogo soma "nome_*.json" ao arquivo principal). Rode de novo após mudar
as tabelas: python3 Tools/gerar_regioes.py   (os mapas feitos à mão só ganham as peças marcadas como geradas).
IDs são estáveis: mudar NOMES aqui é seguro; mudar IDs não (ficam gravados no banco).
"""
import json, math, os, random

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Content")
GEN = "gerado"  # marca em props/objetos/portais acrescentados aos mapas feitos à mão
rnd = random.Random(20261108)


def load(name):
    with open(os.path.join(ROOT, name), encoding="utf-8") as f:
        return json.load(f)


def save(name, data):
    with open(os.path.join(ROOT, name), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


# =====================================================================================================================
# Materiais, ferramentas, sementes e itens de missão
# =====================================================================================================================
MATERIALS = [
    # id, nome, preço de venda base, fonte (texto), resumo
    ("mat_ferro_raso", "Ferro Raso", 6, "Veios rasos da Costa do Cal (picareta).", "Ferro comum: rebites, placas, ligas."),
    ("mat_ferro_eco", "Ferro de Eco", 18, "Veios dos Cânions dos Suplicantes.", "Ferro que zumbe; base do ressonador."),
    ("mat_ferro_geada", "Ferro de Geada", 40, "Veios protegidos por draugr na Coroa da Geada.", "Liga de frio para as asas."),
    ("mat_basalto", "Basalto Férreo", 60, "Veios do Mar de Escória.", "Pedra que vira metal na forja."),
    ("mat_bronze", "Bronze Antigo", 80, "Veios da Acrópole das Cinzas.", "Para a chave do Labirinto."),
    ("mat_erva_tonico", "Erva de Tônico", 3, "Moitas perto dos caminhos; horta (semente de erva).", "Base de tônicos e frascos."),
    ("mat_residuo_larva", "Resíduo de Larva", 4, "Larvas da Galeria Funda.", "Engrossa o tônico."),
    ("mat_frasco", "Frasco Vazio", 2, "Ilma e mascates vendem.", "Para poções do caldeirão."),
    ("mat_carne_javali", "Carne de Javali", 8, "Javalis da Mata dos Enforcados.", "Ensopados e refeições."),
    ("mat_couro", "Couro Grosso", 10, "Lobos e javalis.", "Sela e ataduras de couro."),
    ("mat_seda", "Seda de Toca", 10, "Aranhas e tecelãs.", "Ataduras e estrutura das asas."),
    ("mat_resina", "Resina de Raiz", 9, "Raízes andantes da Mata.", "Cola das tábuas da ponte."),
    ("mat_rebites", "Rebites", 12, "Forja (2 Ferro Raso → 3).", "Ponte, sela e equipamento."),
    ("mat_placa_chapa", "Placa de Chapa", 30, "Forja (Ferro Raso + Casca de Cal).", "Peça inicial de classe (missão do Brás)."),
    ("mat_ferrao", "Ferrão", 14, "Escorpiões e serpentes.", "Antídoto."),
    ("mat_folha_amarga", "Folha Amarga", 6, "Horta (semente de folha); moitas do Brejo.", "Antídoto."),
    ("mat_raiz_vigilia", "Raiz da Vigília", 5, "Horta (semente de raiz).", "Ensopados."),
    ("mat_cogumelo_cinza", "Cogumelo de Cinza", 12, "Horta (semente de cogumelo); Mar de Escória.", "Caldo de Cinzas."),
    ("mat_flor_vinculo", "Flor de Vínculo", 20, "Horta (semente de flor).", "Refeição de vínculo do companheiro."),
    ("mat_memoria_selada", "Memória Selada", 40, "Missão 'Um Pedido de Piedade'.", "Ressonador de lamentos."),
    ("mat_ressonador", "Ressonador de Lamentos", 60, "Bancada (Ferro de Eco + Memória).", "Abre as Galerias da Piedade."),
    ("mat_sela", "Sela Simples", 50, "Bancada (couro, tecido, rebites).", "Para a primeira montaria."),
    ("mat_reagente", "Reagente Arcano", 15, "Necromantes e tecelãs; Isaura vende.", "Preparos de vínculo."),
    ("mat_refeicao_vinculo", "Refeição de Vínculo", 40, "Panela (carne, flor, reagente).", "Para criar vínculo com o pequeno draco."),
    ("mat_liga_geada", "Liga de Geada", 90, "Forja (2 Ferro de Geada + Ferro Raso).", "Estrutura das asas."),
    ("mat_membrana", "Membrana Gelada", 45, "Gigantes e lobos de gelo.", "Estrutura das asas."),
    ("mat_estrutura_asas", "Estrutura das Asas", 300, "Bancada (ligas, membranas, tecido).", "Asas incompletas."),
    ("mat_nucleo_sustentacao", "Núcleo de Sustentação", 400, "Jötunn Acorrentado (garantido na 1ª vitória).", "Completa as asas."),
    ("mat_erva_inverno", "Erva de Inverno", 25, "Moitas da Coroa da Geada.", "Refeição da travessia."),
    ("mat_fragmento_fornalha", "Fragmento da Fornalha", 120, "Fornalha dos Nascidos do Fogo.", "Liga lendária."),
    ("mat_liga_lendaria", "Liga Lendária", 500, "Forja (basalto, geada, fragmento).", "Melhora relíquias."),
    ("mat_brasa_vinculo", "Brasa de Vínculo", 200, "Coração de Escória (1ª vitória).", "Vínculo com a fênix."),
    ("mat_alimento_brasa", "Alimento de Brasa", 250, "Caldeirão (cogumelo, reagente, brasa).", "Para a fênix."),
    ("mat_ficha_mercado", "Ficha do Mercado", 30, "Soldados espectrais de Véspera.", "Moeda dos mortos (missões)."),
    ("mat_carta_perdida", "Carta Nunca Entregue", 0, "Afogados de Véspera.", "Entregue aos moradores."),
    ("mat_fragmento_reliquia", "Fragmento de Relíquia", 80, "Mercado dos Mortos.", "Base da relíquia lendária."),
    ("mat_base_reliquia", "Base de Relíquia", 300, "Forja de Véspera (fragmentos + ferro).", "Primeiro estágio do lendário."),
    ("mat_nucleo_lendario", "Núcleo Lendário", 600, "Regente de Véspera (1ª vitória).", "Coração da relíquia."),
    ("mat_chave_bronze", "Chave de Bronze", 0, "Bancada (bronze + memória).", "Abre o Labirinto do Juramento."),
    ("mat_nucleo_superior", "Núcleo Superior", 900, "Quimera de Bronze.", "Reacende a relíquia."),
    ("mat_insignia", "Insígnia do Juramento", 100, "Bancada (ferro + couro + memória).", "Prova de evolução."),
    ("mat_foco_juramento", "Foco do Segundo Juramento", 400, "Forja de Véspera.", "Segunda evolução."),
    ("mat_quitina", "Quitina Escura", 20, "Besouros de brasa e escorpiões.", "Bancada: placa de quitina (vira Placa de Chapa)."),
    ("mat_osso_antigo", "Osso Antigo", 10, "Esqueletos e draugr.", "Vende bem; algumas missões."),
    ("mat_estandarte", "Estandarte da Acrópole", 120, "Guardião do Ritual (sempre) e guardiões de bronze (raro).", "Bancada: Chave de Bronze sem memória selada."),
    ("mat_cera", "Cera de Colmeia", 12, "Colmeias da Mata.", "Vela ritual (Véspera)."),
    ("mat_espada_quebrada", "Espada sem Dono", 30, "Cripta do Sino: esqueletos (raro) e Cavaleiro Sepulcral.", "Forja: reparar com ferro raso = Espada da Mata."),
    ("mat_pluma_neve", "Pluma de Neve", 60, "Harpias e penhascos da Geada.", "Visual alternativo das asas."),
    ("mat_regador", "Regador de Latão", 60, "Rosa, a agricultora (Porto).", "Na horta: o que você rega cresce 30% mais rápido."),
    ("mat_molde_ferreiro", "Molde do Ferreiro", 150, "Ferreiro Corrompido (sempre) e Ferreiro de Brasa (às vezes).", "Forja: Liga Lendária sem fragmento da fornalha."),
]
SEEDS = [
    # semente, nome da planta, colheita, minutos, mín, máx, preço da semente
    ("semente_raiz", "Raiz da Vigília", "mat_raiz_vigilia", 3, 2, 3, 3),
    ("semente_erva", "Erva de Tônico", "mat_erva_tonico", 4, 3, 4, 3),
    ("semente_folha", "Folha Amarga", "mat_folha_amarga", 6, 2, 3, 6),
    ("semente_cogumelo", "Cogumelo de Cinza", "mat_cogumelo_cinza", 12, 2, 3, 12),
    ("semente_flor", "Flor de Vínculo", "mat_flor_vinculo", 15, 1, 2, 20),
]

# ---- consumíveis novos (poções, ataduras, antídoto) e refeições (um efeito ativo) ----
CONSUMABLES = [
    {"id": "consumable_c05_tonico_forte", "display_name": "Tônico Forte", "heal_percent_max_health": 0.45, "heal_duration_ms": 4000,
     "cooldown_group": "potion", "cooldown_ms": 12000, "stack_limit": 20, "price": 160},
    {"id": "consumable_c06_ataduras", "display_name": "Ataduras de Seda", "heal_percent_max_health": 0.25, "heal_duration_ms": 6000,
     "cooldown_group": "potion", "cooldown_ms": 10000, "stack_limit": 20, "price": 40},
    {"id": "consumable_c07_antidoto", "display_name": "Antídoto do Brejo", "heal_percent_max_health": 0.3, "heal_duration_ms": 2000,
     "cooldown_group": "potion", "cooldown_ms": 10000, "stack_limit": 20, "price": 70,
     "summary": "Cura 30% da vida em 2 s e limpa o amargo do brejo. Recarga 10 s."},
    {"id": "food_ensopado_vigilia", "display_name": "Ensopado da Vigília", "heal_percent_max_health": 0.1, "heal_duration_ms": 3000,
     "cooldown_group": "food", "cooldown_ms": 3000, "stack_limit": 20, "price": 30,
     "buff": "ensopado", "buff_ms": 600000, "buff_power_pct": 0.06, "buff_max_health": 60},
    {"id": "food_caldo_cinzas", "display_name": "Caldo de Cinzas", "heal_percent_max_health": 0.1, "heal_duration_ms": 3000,
     "cooldown_group": "food", "cooldown_ms": 3000, "stack_limit": 20, "price": 90,
     "buff": "caldo", "buff_ms": 900000, "buff_power_pct": 0.10, "buff_armor": 40},
    {"id": "food_refeicao_travessia", "display_name": "Refeição da Travessia", "heal_percent_max_health": 0.1, "heal_duration_ms": 3000,
     "cooldown_group": "food", "cooldown_ms": 3000, "stack_limit": 20, "price": 120,
     "buff": "travessia", "buff_ms": 900000, "buff_armor": 120, "buff_max_health": 150, "buff_regen_pct": 0.15},
    {"id": "food_banquete_ausentes", "display_name": "Banquete dos Ausentes", "heal_percent_max_health": 0.15, "heal_duration_ms": 3000,
     "cooldown_group": "food", "cooldown_ms": 3000, "stack_limit": 20, "price": 200,
     "buff": "banquete", "buff_ms": 1200000, "buff_power_pct": 0.12, "buff_max_health": 120},
]

# ---- receitas (estação, entradas, saída); requires = marca dada por missão (None = conhecida) ----
RECIPES = [
    ("rec_tonico_simples", "Tônico Simples", "caldeirao", [("mat_erva_tonico", 2), ("mat_residuo_larva", 1), ("mat_frasco", 1)], "consumable_c01_tonico_fraco", 2, "receita:tonico", 1),
    ("rec_frasco_mana", "Frasco de Mana", "caldeirao", [("mat_erva_tonico", 2), ("mat_frasco", 1)], "consumable_c03_frasco_mana", 2, None, 1),
    ("rec_tonico_medio", "Tônico Médio", "caldeirao", [("mat_erva_tonico", 3), ("mat_cinza_viva", 1), ("mat_frasco", 1)], "consumable_c02_tonico_medio", 2, "receita:tonico", 8),
    ("rec_tonico_forte", "Tônico Forte", "caldeirao", [("mat_erva_tonico", 4), ("mat_ferrao", 1), ("mat_frasco", 1)], "consumable_c05_tonico_forte", 2, "receita:antidoto", 28),
    ("rec_antidoto", "Antídoto do Brejo", "caldeirao", [("mat_folha_amarga", 2), ("mat_ferrao", 1), ("mat_frasco", 1)], "consumable_c07_antidoto", 2, "receita:antidoto", 28),
    ("rec_alimento_brasa", "Alimento de Brasa", "caldeirao", [("mat_cogumelo_cinza", 2), ("mat_reagente", 1), ("mat_brasa_vinculo", 1)], "mat_alimento_brasa", 1, "receita:brasa", 50),
    ("rec_rebites", "Rebites", "forja", [("mat_ferro_raso", 2)], "mat_rebites", 3, None, 1),
    ("rec_placa_chapa", "Placa de Chapa", "forja", [("mat_ferro_raso", 3), ("mat_casca_cal", 2)], "mat_placa_chapa", 1, "receita:placa", 1),
    ("rec_liga_geada", "Liga de Geada", "forja", [("mat_ferro_geada", 2), ("mat_ferro_raso", 1)], "mat_liga_geada", 1, "receita:liga_geada", 42),
    ("rec_liga_lendaria", "Liga Lendária", "forja", [("mat_basalto", 3), ("mat_ferro_geada", 2), ("mat_fragmento_fornalha", 1)], "mat_liga_lendaria", 1, "receita:liga_lendaria", 50),
    ("rec_base_reliquia", "Base de Relíquia", "forja", [("mat_fragmento_reliquia", 4), ("mat_ferro_eco", 3)], "mat_base_reliquia", 1, "receita:reliquia", 36),
    ("rec_foco_juramento", "Foco do Segundo Juramento", "forja", [("mat_liga_geada", 2), ("mat_memoria_selada", 1), ("mat_osso_antigo", 6)], "mat_foco_juramento", 1, "receita:foco", 48),
    ("rec_ataduras", "Ataduras de Seda", "bancada", [("mat_seda", 2), ("mat_retalho", 1)], "consumable_c06_ataduras", 2, None, 10),
    ("rec_sela", "Sela Simples", "bancada", [("mat_couro", 4), ("mat_retalho", 2), ("mat_rebites", 2)], "mat_sela", 1, "receita:sela", 20),
    ("rec_ressonador", "Ressonador de Lamentos", "bancada", [("mat_ferro_eco", 3), ("mat_memoria_selada", 1)], "mat_ressonador", 1, "receita:ressonador", 22),
    ("rec_insignia", "Insígnia do Juramento", "bancada", [("mat_ferro_raso", 4), ("mat_couro", 2), ("mat_osso_antigo", 3)], "mat_insignia", 1, "receita:insignia", 18),
    ("rec_estrutura_asas", "Estrutura das Asas", "bancada", [("mat_liga_geada", 3), ("mat_membrana", 3), ("mat_retalho", 2)], "mat_estrutura_asas", 1, "receita:asas", 45),
    ("rec_chave_bronze", "Chave de Bronze", "bancada", [("mat_bronze", 4), ("mat_memoria_selada", 1)], "mat_chave_bronze", 1, "receita:chave", 56),
    # materiais que não tinham uso/origem (pendência B)
    ("rec_espada_reforjada", "Espada Reforjada", "forja", [("mat_espada_quebrada", 1), ("mat_ferro_raso", 4)], "weapon_guerreiro_espada_mata", 1, None, 18),
    ("rec_placa_quitina", "Placa de Quitina", "bancada", [("mat_quitina", 3), ("mat_rebites", 1)], "mat_placa_chapa", 2, None, 21),
    ("rec_chave_estandarte", "Chave de Bronze (estandarte)", "bancada", [("mat_bronze", 4), ("mat_estandarte", 1)], "mat_chave_bronze", 1, "receita:chave", 56),
    ("rec_liga_molde", "Liga Lendária (molde)", "forja", [("mat_molde_ferreiro", 1), ("mat_basalto", 2), ("mat_ferro_geada", 2)], "mat_liga_lendaria", 1, "receita:liga_lendaria", 50),
    ("rec_ensopado", "Ensopado da Vigília", "panela", [("mat_carne_javali", 1), ("mat_raiz_vigilia", 2)], "food_ensopado_vigilia", 2, "receita:ensopado", 12),
    ("rec_vinculo", "Refeição de Vínculo", "panela", [("mat_carne_javali", 1), ("mat_flor_vinculo", 1), ("mat_reagente", 1)], "mat_refeicao_vinculo", 1, "receita:vinculo", 30),
    ("rec_travessia", "Refeição da Travessia", "panela", [("mat_carne_javali", 1), ("mat_raiz_vigilia", 1), ("mat_erva_inverno", 1)], "food_refeicao_travessia", 2, "receita:travessia", 42),
    ("rec_caldo", "Caldo de Cinzas", "panela", [("mat_cogumelo_cinza", 1), ("mat_raiz_vigilia", 1)], "food_caldo_cinzas", 2, "receita:caldo", 34),
    ("rec_banquete", "Banquete dos Ausentes", "panela", [("mat_cogumelo_cinza", 2), ("mat_carne_javali", 1), ("mat_flor_vinculo", 1)], "food_banquete_ausentes", 1, "receita:banquete", 36),
]

# =====================================================================================================================
# Equipamento por faixa (min_level) — números crescem com o nível; fontes por região/dungeon
# =====================================================================================================================
TIERS = [
    # sufixo, nível, nome do conjunto do guerreiro, do mago, fonte
    ("mata", 17, "do Enforcado", "da Raiz", "Mata dos Enforcados e Cripta do Sino"),
    ("canion", 24, "do Suplicante", "do Lamento", "Cânions dos Suplicantes e Galerias da Piedade"),
    ("brejo", 30, "da Viúva", "do Lodo", "Brejo da Viúva e Mosteiro Afundado"),
    ("vespera", 36, "de Véspera", "das Vozes", "Véspera e Mercado dos Mortos"),
    ("geada", 44, "da Geada", "do Inverno", "Coroa da Geada e Salão da Geada"),
    ("escoria", 50, "da Escória", "da Brasa", "Mar de Escória e Fornalha"),
    ("acropole", 56, "do Juramento", "do Bronze", "Acrópole das Cinzas e Labirinto do Juramento"),
]


def tier_items():
    out = []
    for suf, lvl, gname, mname, src in TIERS:
        power = round(36 + (lvl - 11) * 2.6)
        req = round(30 + (lvl - 11) * 2.4)
        armor = round(200 + (lvl - 11) * 14)
        hp = 80 + lvl * 3
        price = int(900 * (1.11 ** (lvl - 11)))
        sword_model = "sword_vein" if lvl >= 30 else "sword_claw"
        out += [
            {"id": f"weapon_guerreiro_espada_{suf}", "display_name": f"Espada {gname}", "category": "weapon", "subtype": "sword",
             "min_level": lvl, "allowed_classes": ["guerreiro"], "handedness": 1,
             "base_stats": {"weapon_power": power, "attack_interval_ms": 950}, "model_asset_id": sword_model,
             "summary": f"Poder {power}, rápida. Permite escudo.", "price": price, "req_primary": req, "source": src + "."},
            {"id": f"weapon_guerreiro_machado_{suf}", "display_name": f"Machado {gname}", "category": "weapon", "subtype": "axe",
             "min_level": lvl + 2, "allowed_classes": ["guerreiro"], "handedness": 2,
             "base_stats": {"weapon_power": round(power * 1.25), "attack_interval_ms": 1250, "basic_stagger": 1}, "model_asset_id": "axe_quarry",
             "summary": f"Poder {round(power * 1.25)}, lento; o básico interrompe preparações. Duas mãos.", "price": round(price * 1.2),
             "req_primary": req + 4, "source": src + " (elites e chefes)."},
            {"id": f"staff_mago_cajado_{suf}", "display_name": f"Cajado {mname}", "category": "weapon", "subtype": "staff",
             "min_level": lvl, "allowed_classes": ["mago"], "handedness": 2,
             "base_stats": {"weapon_power": round(power * 1.05), "attack_interval_ms": 1000, "aoe_radius_bonus": round(0.3 + lvl / 100, 2)},
             "model_asset_id": "staff_vein" if lvl >= 30 else "staff_ember",
             "summary": f"Poder {round(power * 1.05)}, explosão +{round(0.3 + lvl / 100, 2)} m.", "price": price, "req_primary": req + 2, "source": src + "."},
            {"id": f"armor_guerreiro_{suf}", "display_name": f"Couraça {gname}", "category": "armor", "subtype": "plate",
             "min_level": lvl, "allowed_classes": ["guerreiro"], "handedness": 0,
             "base_stats": {"armor": armor, "max_health": hp, "move_speed_pct": -0.05}, "model_asset_id": "body_plate" if lvl >= 36 else "body_mail",
             "summary": f"Armadura +{armor}, vida +{hp}, 5% mais lento.", "price": round(price * 1.1), "req_primary": req - 2, "source": src + "."},
            {"id": f"armor_mago_{suf}", "display_name": f"Manto {mname}", "category": "armor", "subtype": "robe",
             "min_level": lvl, "allowed_classes": ["mago"], "handedness": 0,
             "base_stats": {"armor": round(armor * 0.45), "max_health": round(hp * 0.9), "resource_regen_pct": 0.1},
             "model_asset_id": "body_robe_vein" if lvl >= 36 else "body_robe",
             "summary": f"Armadura +{round(armor * 0.45)}, vida +{round(hp * 0.9)}, mana volta 10% mais rápido.", "price": round(price * 1.1),
             "req_primary": req, "source": src + "."},
            {"id": f"helmet_{suf}", "display_name": f"Elmo {gname}", "category": "helmet", "subtype": "plate",
             "min_level": lvl, "allowed_classes": ["guerreiro", "mago", "bruxa", "arqueiro"], "handedness": 0,
             "base_stats": {"armor": round(armor * 0.35), "max_health": round(hp * 0.6)}, "model_asset_id": "head_guardian" if lvl >= 36 else "head_iron",
             "summary": f"Armadura +{round(armor * 0.35)}, vida +{round(hp * 0.6)}.", "price": round(price * 0.8), "source": src + "."},
            # Bruxa (mãos nuas + talismã; sem cajado) e Arqueiro (arco)
            {"id": f"talisma_bruxa_{suf}", "display_name": f"Talismã {mname}", "category": "weapon", "subtype": "talisman",
             "min_level": lvl, "allowed_classes": ["bruxa"], "handedness": 1,
             "base_stats": {"weapon_power": round(power * 0.85), "attack_interval_ms": 1000, "resource_regen_pct": 0.1},
             "model_asset_id": "talisman_vein" if lvl >= 30 else "talisman_bone",
             "summary": f"Poder {round(power * 0.85)} (cura e escudo usam o poder); éter volta 10% mais rápido.", "price": price,
             "req_primary": req + 2, "source": src + "."},
            {"id": f"arco_arqueiro_{suf}", "display_name": f"Arco {gname}", "category": "weapon", "subtype": "bow",
             "min_level": lvl, "allowed_classes": ["arqueiro"], "handedness": 2,
             "base_stats": {"weapon_power": round(power * 0.95), "attack_interval_ms": 900, "projectile_speed_pct": 0.2},
             "model_asset_id": "bow_vein" if lvl >= 30 else "bow_yew",
             "summary": f"Poder {round(power * 0.95)}, flechas 20% mais rápidas. Duas mãos.", "price": price, "req_primary": req + 1,
             "source": src + "."},
            {"id": f"armor_bruxa_{suf}", "display_name": f"Vestes {mname}", "category": "armor", "subtype": "robe",
             "min_level": lvl, "allowed_classes": ["bruxa"], "handedness": 0,
             "base_stats": {"armor": round(armor * 0.5), "max_health": hp, "resource_regen_pct": 0.1},
             "model_asset_id": "body_witch_vein" if lvl >= 36 else "body_witch",
             "summary": f"Armadura +{round(armor * 0.5)}, vida +{hp}, éter volta 10% mais rápido.", "price": round(price * 1.1),
             "req_primary": req, "source": src + "."},
            {"id": f"armor_arqueiro_{suf}", "display_name": f"Gibão {gname}", "category": "armor", "subtype": "leather",
             "min_level": lvl, "allowed_classes": ["arqueiro"], "handedness": 0,
             "base_stats": {"armor": round(armor * 0.7), "max_health": hp, "move_speed_pct": 0.04},
             "model_asset_id": "body_ranger_vein" if lvl >= 36 else "body_ranger",
             "summary": f"Armadura +{round(armor * 0.7)}, vida +{hp}, 4% mais rápido.", "price": round(price * 1.1),
             "req_primary": req - 1, "source": src + "."},
            {"id": f"shield_guerreiro_{suf}", "display_name": f"Escudo {gname}", "category": "offhand", "subtype": "shield",
             "min_level": lvl, "allowed_classes": ["guerreiro"], "handedness": 0,
             "base_stats": {"armor": round(armor * 0.6), "guard_bonus": 0.05}, "model_asset_id": "shield_stone",
             "summary": f"Armadura +{round(armor * 0.6)}; Guarda segura mais.", "price": round(price * 0.8), "req_primary": req - 4, "source": src + "."},
        ]
    return out


LEGENDARIES = [
    {"id": "legend_espada_sino_partido", "display_name": "Espada do Sino Partido", "category": "weapon", "subtype": "sword", "min_level": 38,
     "allowed_classes": ["guerreiro"], "handedness": 1, "base_stats": {"weapon_power": 118, "attack_interval_ms": 900, "proc_every": 4},
     "model_asset_id": "sword_vein", "proc_skill": "skill_lend_onda_sino",
     "effect": "A cada 4 golpes básicos que acertam, o sino ressoa: onda curta em volta do alvo.", "req_primary": 92,
     "source": "Cadeia de Véspera (Uma Arma com História) — escolha da classe."},
    {"id": "legend_cajado_ultimas_vozes", "display_name": "Cajado das Últimas Vozes", "category": "weapon", "subtype": "staff", "min_level": 38,
     "allowed_classes": ["mago"], "handedness": 2, "base_stats": {"weapon_power": 108, "attack_interval_ms": 1000, "aoe_radius_bonus": 0.8, "proc_every": 4},
     "model_asset_id": "staff_vein", "proc_skill": "skill_lend_vozes",
     "effect": "A cada 4 setas que acertam, vozes espectrais explodem em volta do alvo.", "req_primary": 94,
     "source": "Cadeia de Véspera (Uma Arma com História) — escolha da classe."},
    {"id": "legend_escudo_vigilia_morta", "display_name": "Escudo da Vigília Morta", "category": "offhand", "subtype": "shield", "min_level": 40,
     "allowed_classes": ["guerreiro"], "handedness": 0, "base_stats": {"armor": 520, "guard_bonus": 0.12, "max_health": 120},
     "model_asset_id": "shield_stone", "effect": "Guarda muito mais firme.", "req_primary": 88,
     "source": "Regente de Véspera (Mercado dos Mortos), chance rara + fragmentos."},
    {"id": "legend_manto_segunda_sombra", "display_name": "Manto da Segunda Sombra", "category": "armor", "subtype": "robe", "min_level": 42,
     "allowed_classes": ["mago"], "handedness": 0, "base_stats": {"armor": 360, "max_health": 260, "resource_regen_pct": 0.35},
     "model_asset_id": "body_robe_vein", "effect": "Mana volta 35% mais rápido.", "req_primary": 96,
     "source": "Mercado dos Mortos e Salão da Geada (raro)."},
    {"id": "legend_machado_colosso", "display_name": "Machado do Colosso", "category": "weapon", "subtype": "axe", "min_level": 52,
     "allowed_classes": ["guerreiro"], "handedness": 2, "base_stats": {"weapon_power": 190, "attack_interval_ms": 1250, "basic_stagger": 1, "proc_every": 3},
     "model_asset_id": "axe_quarry", "proc_skill": "skill_lend_colosso",
     "effect": "A cada 3 golpes que acertam, impacto de postura em volta (interrompe).", "req_primary": 132,
     "source": "Colosso de Escória (raro) + fragmentos."},
    {"id": "legend_foco_brasa_viva", "display_name": "Foco da Brasa Viva", "category": "weapon", "subtype": "staff", "min_level": 52,
     "allowed_classes": ["mago"], "handedness": 2, "base_stats": {"weapon_power": 168, "attack_interval_ms": 1000, "aoe_radius_bonus": 1.0, "proc_every": 4},
     "model_asset_id": "staff_ember", "proc_skill": "skill_lend_brasa",
     "effect": "A cada 4 setas que acertam, pequena explosão de brasa.", "req_primary": 134,
     "source": "Fornalha dos Nascidos do Fogo (raro) + fragmentos."},
    {"id": "legend_elmo_juramento", "display_name": "Elmo do Juramento", "category": "helmet", "subtype": "plate", "min_level": 57,
     "allowed_classes": ["guerreiro", "mago", "bruxa", "arqueiro"], "handedness": 0, "base_stats": {"armor": 420, "max_health": 380},
     "model_asset_id": "head_guardian", "effect": "Elmo dos guardiões da Acrópole.", "source": "Labirinto do Juramento (raro)."},
    {"id": "legend_espada_sino_reacesa", "display_name": "Espada do Sino Reacesa", "category": "weapon", "subtype": "sword", "min_level": 58,
     "allowed_classes": ["guerreiro"], "handedness": 1, "base_stats": {"weapon_power": 175, "attack_interval_ms": 880, "proc_every": 3},
     "model_asset_id": "sword_vein", "proc_skill": "skill_lend_onda_sino",
     "effect": "A relíquia reacesa: a onda sai a cada 3 golpes.", "req_primary": 140, "source": "Relíquia Reacesa (Q62)."},
    {"id": "legend_cajado_vozes_reaceso", "display_name": "Cajado das Vozes Reaceso", "category": "weapon", "subtype": "staff", "min_level": 58,
     "allowed_classes": ["mago"], "handedness": 2, "base_stats": {"weapon_power": 160, "attack_interval_ms": 1000, "aoe_radius_bonus": 1.2, "proc_every": 3},
     "model_asset_id": "staff_vein", "proc_skill": "skill_lend_vozes",
     "effect": "A relíquia reacesa: as vozes explodem a cada 3 setas.", "req_primary": 142, "source": "Relíquia Reacesa (Q62)."},
]

LEGENDARIES += [
    {"id": "legend_talisma_ultimas_vozes", "display_name": "Talismã das Últimas Vozes", "category": "weapon", "subtype": "talisman", "min_level": 38,
     "allowed_classes": ["bruxa"], "handedness": 1, "base_stats": {"weapon_power": 100, "attack_interval_ms": 1000, "resource_regen_pct": 0.2, "proc_every": 4},
     "model_asset_id": "talisman_vein", "proc_skill": "skill_lend_vozes",
     "effect": "A cada 4 lampejos que acertam, vozes espectrais explodem em volta do alvo.", "req_primary": 94,
     "source": "Cadeia de Véspera (Uma Arma com História) — escolha da classe."},
    {"id": "legend_arco_sino_partido", "display_name": "Arco do Sino Partido", "category": "weapon", "subtype": "bow", "min_level": 38,
     "allowed_classes": ["arqueiro"], "handedness": 2, "base_stats": {"weapon_power": 112, "attack_interval_ms": 900, "proc_every": 4},
     "model_asset_id": "bow_vein", "proc_skill": "skill_lend_onda_sino",
     "effect": "A cada 4 flechas que acertam, o sino ressoa: onda curta em volta do alvo.", "req_primary": 92,
     "source": "Cadeia de Véspera (Uma Arma com História) — escolha da classe."},
    {"id": "legend_talisma_reaceso", "display_name": "Talismã das Vozes Reaceso", "category": "weapon", "subtype": "talisman", "min_level": 58,
     "allowed_classes": ["bruxa"], "handedness": 1, "base_stats": {"weapon_power": 150, "attack_interval_ms": 1000, "resource_regen_pct": 0.25, "proc_every": 3},
     "model_asset_id": "talisman_vein", "proc_skill": "skill_lend_vozes",
     "effect": "A relíquia reacesa: as vozes explodem a cada 3 lampejos.", "req_primary": 142, "source": "Relíquia Reacesa (Q62)."},
    {"id": "legend_arco_sino_reaceso", "display_name": "Arco do Sino Reaceso", "category": "weapon", "subtype": "bow", "min_level": 58,
     "allowed_classes": ["arqueiro"], "handedness": 2, "base_stats": {"weapon_power": 166, "attack_interval_ms": 880, "proc_every": 3},
     "model_asset_id": "bow_vein", "proc_skill": "skill_lend_onda_sino",
     "effect": "A relíquia reacesa: a onda sai a cada 3 flechas.", "req_primary": 140, "source": "Relíquia Reacesa (Q62)."},
]

SKILLS = [
    {"id": "skill_pet_dragao", "class_id": "guerreiro", "display_name": "Sopro do Pequeno Draco", "unlock_level": 1, "target_mode": "single",
     "range": 9, "radius": 0, "cooldown_ms": 1600, "resource_cost": 0, "resource_generated": 0, "coefficient": 0.22,
     "effects": {}, "cast_ms": 0, "recovery_ms": 0, "description": "Companheiro: projétil curto (cerca de 10% do dano do dono)."},
    {"id": "skill_pet_fenix", "class_id": "guerreiro", "display_name": "Brasa da Fênix", "unlock_level": 1, "target_mode": "single",
     "range": 10, "radius": 0, "cooldown_ms": 2000, "resource_cost": 0, "resource_generated": 0, "coefficient": 0.30,
     "effects": {}, "cast_ms": 0, "recovery_ms": 0, "description": "Companheiro: fogo (cerca de 12% do dano do dono)."},
    {"id": "skill_lend_onda_sino", "class_id": "guerreiro", "display_name": "Ressoar do Sino", "unlock_level": 1, "target_mode": "nova",
     "range": 0, "radius": 2.8, "cooldown_ms": 0, "resource_cost": 0, "resource_generated": 0, "coefficient": 0.7,
     "effects": {}, "cast_ms": 0, "recovery_ms": 0, "description": "Efeito lendário."},
    {"id": "skill_lend_colosso", "class_id": "guerreiro", "display_name": "Impacto do Colosso", "unlock_level": 1, "target_mode": "nova",
     "range": 0, "radius": 3.2, "cooldown_ms": 0, "resource_cost": 0, "resource_generated": 0, "coefficient": 0.8,
     "effects": {"stagger": 1}, "cast_ms": 0, "recovery_ms": 0, "description": "Efeito lendário."},
    {"id": "skill_lend_vozes", "class_id": "mago", "display_name": "Últimas Vozes", "unlock_level": 1, "target_mode": "nova",
     "range": 0, "radius": 2.6, "cooldown_ms": 0, "resource_cost": 0, "resource_generated": 0, "coefficient": 0.6,
     "effects": {}, "cast_ms": 0, "recovery_ms": 0, "description": "Efeito lendário."},
    {"id": "skill_lend_brasa", "class_id": "mago", "display_name": "Brasa Viva", "unlock_level": 1, "target_mode": "nova",
     "range": 0, "radius": 3.0, "cooldown_ms": 0, "resource_cost": 0, "resource_generated": 0, "coefficient": 0.75,
     "effects": {}, "cast_ms": 0, "recovery_ms": 0, "description": "Efeito lendário."},
]

# =====================================================================================================================
# Comportamentos novos (mesmo padrão dos existentes: preparação visível, golpe, brecha)
# =====================================================================================================================
def atk(i, shape, rng, wind, act, rec, cd, dmg, **kw):
    d = {"id": i, "shape": shape, "range": rng, "windup_ms": wind, "active_ms": act, "recovery_ms": rec, "cooldown_ms": cd, "damage_mult": dmg}
    d.update(kw)
    return d


# Chefes com identidade (cada um tem um "golpe assinatura"; a 2ª fase acrescenta o mais perigoso)
def boss(i, speed, attacks, two, caster=False, **kw):
    d = {"id": i, "move_speed": speed, "aggro_radius": 14 if caster else 13, "leash_radius": 16, "exposed_mult": 1.35, "staggerable": False,
         "attacks": attacks, "phase_two_at": 0.55, "phase_two_attacks": two}
    if caster: d.update(keep_distance=6, flee_distance=3)
    d.update(kw)
    return d


BOSS_BEHAVIORS = [
    # Guardião Retorcido (raiz): raízes que irrompem em linha até o jogador, chicote largo, enraizar sob os pés
    boss("boss_root", 3.4, [atk("raizes_em_marcha", "circle", 9, 1000, 1700, 1300, 6000, 1.4, radius=1.6, offset=1.8, hits=5, hit_interval_ms=340, step_m=1.6, track_fraction=0.5),
                            atk("chicote", "cone", 4.2, 850, 200, 900, 1300, 1.3, arc=170, windup_jitter_ms=450, track_fraction=0.45),
                            atk("enraizar", "circle", 11, 1200, 200, 1200, 5200, 1.8, radius=2.0, at_target=True, track_fraction=0.65),
                            atk("espinhos", "circle", 3.0, 1400, 200, 1500, 7000, 2.2, radius=5.0, offset=0, track_fraction=0)], ["espinhos"]),
    # Guardião do Sino: badaladas (pulsos repetidos), martelada atrasada, ondas sonoras em leque
    boss("boss_bell", 3.5, [atk("badaladas", "circle", 3.5, 1100, 1300, 1400, 7000, 1.2, radius=4.2, offset=0, hits=3, hit_interval_ms=550, track_fraction=0),
                            atk("martelada", "circle", 4.0, 1100, 200, 1300, 2600, 2.0, radius=2.6, offset=2.0, windup_jitter_ms=700, track_fraction=0.35),
                            atk("ondas_do_sino", "projectile", 13, 1000, 100, 1000, 4200, 1.1, radius=0.8, projectile_speed=10, count=5, spread_deg=80, min_range=4, track_fraction=0.7),
                            atk("varredura", "cone", 3.6, 800, 200, 900, 1200, 1.2, arc=160, track_fraction=0.45)], ["badaladas"]),
    # Carcereiro das Almas / Abadessa / Regente: raio de almas, correntes que caem no jogador, leque
    boss("boss_necro", 3.4, [atk("raio_das_almas", "beam", 12, 1250, 900, 1200, 5200, 0.9, radius=0.6, hits=3, hit_interval_ms=300, track_fraction=0.75),
                             atk("correntes", "circle", 11, 1100, 1100, 1100, 6000, 1.1, radius=1.8, at_target=True, hits=3, hit_interval_ms=380, track_fraction=0.7),
                             atk("almas_em_leque", "projectile", 14, 900, 100, 900, 3000, 1.0, radius=0.55, projectile_speed=11, count=5, spread_deg=60, track_fraction=0.8),
                             atk("repulsa", "circle", 3.0, 800, 150, 900, 3500, 1.0, radius=3.2, offset=0, track_fraction=0)], ["correntes"], caster=True),
    # Rei Draugr: esgrimista — estocadas em sequência, golpe atrasado, recua e solta leque de gelo
    boss("boss_draugr", 4.0, [atk("estocadas_do_rei", "cone", 3.2, 650, 1300, 1100, 3600, 0.8, arc=60, hits=4, hit_interval_ms=300, step_m=0.9, track_fraction=0.6),
                              atk("golpe_atrasado", "cone", 3.6, 950, 200, 1000, 1500, 1.6, arc=120, windup_jitter_ms=800, track_fraction=0.45),
                              atk("salto_para_tras", "cone", 3.0, 500, 150, 600, 5200, 1.0, arc=100, backstep_m=4.0, track_fraction=0.6, followup="leque_de_gelo"),
                              atk("leque_de_gelo", "projectile", 12, 600, 100, 1000, 5200, 1.1, radius=0.6, projectile_speed=12, count=5, spread_deg=56, track_fraction=0.7)], ["salto_para_tras"]),
    # Jötunn: pisões em marcha, clava atrasada enorme, rochas que caem no jogador
    boss("boss_giant", 3.2, [atk("pisoes_do_gigante", "circle", 8, 1100, 1600, 1400, 6500, 1.6, radius=2.0, offset=2.2, hits=4, hit_interval_ms=420, step_m=2.0, track_fraction=0.5),
                             atk("clava", "cone", 4.6, 1200, 250, 1300, 2200, 2.0, arc=120, windup_jitter_ms=800, track_fraction=0.4),
                             atk("rochas", "circle", 13, 1200, 1000, 1200, 6000, 1.4, radius=2.0, at_target=True, hits=3, hit_interval_ms=420, min_range=4, track_fraction=0.7),
                             atk("tremor", "circle", 3.0, 1500, 200, 1600, 8000, 2.3, radius=6.0, offset=0, track_fraction=0)], ["tremor"]),
    # Colosso de Escória: raio do núcleo derretido, punhos que marcham, leque de escória
    boss("boss_golem", 3.0, [atk("raio_do_nucleo", "beam", 12, 1300, 1000, 1300, 5600, 1.0, radius=0.8, hits=3, hit_interval_ms=320, track_fraction=0.75),
                             atk("punhos", "circle", 7, 1000, 1300, 1300, 4800, 1.5, radius=1.8, offset=1.8, hits=3, hit_interval_ms=420, step_m=1.8, track_fraction=0.5),
                             atk("escoria", "projectile", 12, 950, 100, 1000, 4000, 1.1, radius=0.7, projectile_speed=9, count=4, spread_deg=50, track_fraction=0.7),
                             atk("pulso", "circle", 3.0, 1400, 200, 1500, 7000, 2.3, radius=5.5, offset=0, track_fraction=0)], ["raio_do_nucleo"]),
    # Coração de Escória: chuva de brasas sobre o jogador, raio giratório curto, leque
    boss("boss_heart", 3.2, [atk("chuva_de_brasas", "circle", 12, 1000, 1500, 1100, 5200, 1.2, radius=1.7, at_target=True, hits=4, hit_interval_ms=380, track_fraction=0.7),
                             atk("labareda", "beam", 10, 1100, 800, 1100, 4200, 1.0, radius=0.7, hits=2, hit_interval_ms=380, track_fraction=0.75),
                             atk("brasas_em_leque", "projectile", 14, 900, 100, 900, 2800, 1.0, radius=0.55, projectile_speed=11, count=5, spread_deg=64, track_fraction=0.8),
                             atk("repulsa", "circle", 3.0, 800, 150, 900, 3500, 1.0, radius=3.4, offset=0, track_fraction=0)], ["labareda"], caster=True),
    # Quimera de Bronze: sopro de fogo (raio), bote triplo, coice de cauda
    boss("boss_chimera", 4.2, [atk("sopro", "beam", 9, 1100, 900, 1200, 4800, 1.0, radius=1.0, hits=3, hit_interval_ms=300, track_fraction=0.7),
                               atk("botes", "lunge", 7, 800, 900, 1100, 4200, 0.9, radius=1.0, lunge_m=6.0, hits=3, hit_interval_ms=300, min_range=2.5, track_fraction=0.4),
                               atk("garras", "cone", 3.4, 600, 650, 900, 1600, 0.9, arc=120, hits=2, hit_interval_ms=320, step_m=0.6, track_fraction=0.55),
                               atk("coice", "cone", 3.4, 700, 150, 900, 3600, 1.4, arc=140, backstep_m=3.0, track_fraction=0.5)], ["botes"]),
]


BEHAVIORS = [
    # lobos: bote, mordida dupla e recuo (caçam em matilha, batem e saem)
    {"id": "beast_lunge", "move_speed": 4.7, "aggro_radius": 11, "leash_radius": 24, "exposed_mult": 1.4,
     "attacks": [atk("bote_lobo", "lunge", 4.0, 600, 220, 760, 600, 1.0, radius=0.8, lunge_m=3.4, track_fraction=0.5),
                 atk("mordidas", "cone", 2.0, 480, 600, 700, 2600, 0.6, arc=80, hits=2, hit_interval_ms=300, step_m=0.5, backstep_m=2.2, track_fraction=0.6),
                 atk("mordida_lobo", "cone", 2.0, 450, 150, 520, 500, 0.7, arc=90, track_fraction=0.6)]},
    # javalis: investida longa; de perto, presas para cima e recuo para tomar distância
    {"id": "boar_charge", "move_speed": 3.9, "aggro_radius": 10, "leash_radius": 22, "exposed_mult": 1.5,
     "attacks": [atk("investida", "lunge", 7.0, 900, 380, 1100, 2600, 1.3, radius=1.0, lunge_m=6.5, track_fraction=0.35, min_range=2.5),
                 atk("presas", "cone", 2.2, 600, 150, 650, 600, 0.9, arc=100, backstep_m=2.4, track_fraction=0.55)]},
    # aranhas: salto, picada e teia em leque
    {"id": "spider_leap", "move_speed": 4.4, "aggro_radius": 10, "leash_radius": 20, "exposed_mult": 1.5,
     "attacks": [atk("salto", "circle", 6.0, 750, 200, 900, 2200, 1.1, radius=1.6, offset=4.5, track_fraction=0.4, min_range=2.5),
                 atk("teia", "projectile", 9, 800, 100, 900, 5000, 0.6, radius=0.45, projectile_speed=8.5, count=3, spread_deg=40, min_range=3, track_fraction=0.7),
                 atk("picada", "cone", 2.0, 500, 150, 600, 500, 0.8, arc=80, track_fraction=0.6)]},
    # brutos (raízes, trolls): pancada com atraso e pisões que marcham
    {"id": "brute_slam", "move_speed": 3.3, "aggro_radius": 10, "leash_radius": 20, "exposed_mult": 1.4,
     "attacks": [atk("pisoes", "circle", 6, 950, 1200, 1200, 7000, 1.1, radius=1.5, offset=1.6, hits=3, hit_interval_ms=400, step_m=1.6, track_fraction=0.5),
                 atk("pancada", "circle", 3.0, 1000, 200, 1100, 1800, 1.5, radius=2.4, offset=1.6, windup_jitter_ms=500, track_fraction=0.3),
                 atk("varrida", "cone", 2.8, 750, 180, 800, 900, 1.0, arc=150, track_fraction=0.5, followup="pancada")]},
    # fantasmas: lamento, três lamentos em leque, chão que gela sob o jogador e grito
    {"id": "ghost_caster", "move_speed": 3.6, "aggro_radius": 14, "leash_radius": 24, "keep_distance": 7, "flee_distance": 4, "exposed_mult": 1.6,
     "attacks": [atk("lamento_triplo", "projectile", 12, 950, 100, 900, 5200, 0.8, radius=0.5, projectile_speed=9.5, count=3, spread_deg=32, min_range=3.5, track_fraction=0.8),
                 atk("lamento", "projectile", 13, 850, 100, 900, 700, 1.0, radius=0.55, projectile_speed=10.5, track_fraction=0.8),
                 atk("assombro", "circle", 11, 1100, 200, 800, 6500, 1.2, radius=1.7, at_target=True, min_range=3, track_fraction=0.7),
                 atk("grito", "circle", 2.8, 650, 150, 800, 4000, 0.7, radius=2.8, offset=0, track_fraction=0)]},
    # elite pesado genérico
    {"id": "elite_bruiser", "move_speed": 3.8, "aggro_radius": 11, "leash_radius": 18, "exposed_mult": 1.5,
     "attacks": [atk("golpe_pesado", "cone", 3.0, 850, 200, 600, 1200, 1.3, arc=120, windup_jitter_ms=450, track_fraction=0.45, followup="esmagar"),
                 atk("esmagar", "circle", 3.0, 700, 200, 1300, 3000, 1.6, radius=2.4, offset=1.2, track_fraction=0.2),
                 atk("avanco", "lunge", 6.0, 800, 300, 900, 4000, 1.1, radius=1.0, lunge_m=5.0, min_range=3.0, track_fraction=0.4)]},
    # cavaleiros, draugr, guardas de bronze, carbonizados: esgrima — estocadas que avançam, golpe atrasado, bate e volta
    {"id": "elite_knight", "move_speed": 4.0, "aggro_radius": 11, "leash_radius": 18, "exposed_mult": 1.5,
     "attacks": [atk("estocadas", "cone", 2.8, 650, 1000, 1000, 3600, 0.65, arc=50, hits=3, hit_interval_ms=290, step_m=0.8, track_fraction=0.6),
                 atk("golpe_atrasado", "cone", 3.0, 900, 180, 900, 1400, 1.4, arc=110, windup_jitter_ms=700, track_fraction=0.45),
                 atk("golpe_e_recuo", "cone", 2.6, 550, 150, 700, 4200, 1.0, arc=100, backstep_m=3.0, track_fraction=0.6),
                 atk("avanco", "lunge", 6.5, 800, 300, 900, 4500, 1.1, radius=0.9, lunge_m=5.5, min_range=3.2, track_fraction=0.4)]},
    # feras grandes (javali ancião, escorpião-rei, basilisco, viúva, minotauro, troll carcereiro): investida, golpe duplo, cuspe em leque
    {"id": "elite_beast", "move_speed": 4.0, "aggro_radius": 11, "leash_radius": 18, "exposed_mult": 1.5,
     "attacks": [atk("investida", "lunge", 8.0, 900, 400, 1100, 3600, 1.4, radius=1.1, lunge_m=7.0, min_range=3.0, track_fraction=0.35),
                 atk("golpe_duplo", "cone", 2.8, 650, 700, 950, 2400, 0.9, arc=110, hits=2, hit_interval_ms=340, step_m=0.6, track_fraction=0.55),
                 atk("cuspe", "projectile", 10, 900, 100, 950, 5000, 0.8, radius=0.5, projectile_speed=10, count=3, spread_deg=36, min_range=3.5, track_fraction=0.75),
                 atk("esmagar", "circle", 3.0, 800, 200, 1200, 3200, 1.5, radius=2.4, offset=1.2, track_fraction=0.25)]},
    {"id": "boss_generic", "move_speed": 3.8, "aggro_radius": 13, "leash_radius": 16, "exposed_mult": 1.35, "staggerable": False,
     "attacks": [atk("varredura", "cone", 3.8, 900, 200, 900, 1300, 1.3, arc=160, track_fraction=0.45),
                 atk("pancada_vertical", "circle", 4.0, 1200, 200, 1400, 3600, 2.1, radius=2.6, offset=2.0, track_fraction=0.3),
                 atk("onda", "projectile", 13, 1000, 100, 900, 3200, 1.3, radius=0.8, projectile_speed=11, track_fraction=0.7, min_range=5),
                 atk("pulso", "circle", 3.0, 1400, 200, 1500, 7000, 2.3, radius=5.0, offset=0, track_fraction=0)],
     "phase_two_at": 0.5, "phase_two_attacks": ["pulso"]},
    {"id": "boss_caster", "move_speed": 3.4, "aggro_radius": 14, "leash_radius": 16, "keep_distance": 6, "flee_distance": 3, "exposed_mult": 1.4,
     "staggerable": False,
     "attacks": [atk("rajada", "projectile", 14, 800, 100, 850, 900, 1.2, radius=0.6, projectile_speed=11, track_fraction=0.8),
                 atk("circulo_sombrio", "circle", 9, 1300, 200, 1300, 4200, 2.0, radius=3.0, offset=5.0, track_fraction=0.3),
                 atk("repulsa", "circle", 3.0, 800, 150, 900, 3500, 1.0, radius=3.2, offset=0, track_fraction=0),
                 atk("chuva", "circle", 10, 1500, 200, 1400, 8000, 2.2, radius=4.5, offset=6.0, track_fraction=0.2)],
     "phase_two_at": 0.5, "phase_two_attacks": ["chuva"]},
] + BOSS_BEHAVIORS


# =====================================================================================================================
# Regiões R02–R08 e dungeons D02–D08 (ligação linear: Vale → Mata → Cânions → Brejo → Véspera → Geada → Escória → Acrópole)
# espécie: (chave, nome, comportamento, visual, nível, vida, dano, arquétipo, materiais [(item, chance)])
# =====================================================================================================================
def sp(key, name, beh, vis, lvl, hp=1.0, dmg=2.0, arch="combatente", mats=(), radius=0.6):
    return dict(key=key, name=name, beh=beh, vis=vis, lvl=lvl, hp=hp, dmg=dmg, arch=arch, mats=list(mats), radius=radius)


REGIONS = [
    dict(id="mata", map="mata_dos_enforcados", name="Mata dos Enforcados", band=(17, 20), theme="mata", xp=2.2, world=(70, 0),
         hub="Vila do Moinho", tier="mata", ore=("mat_ferro_raso", "Veio de ferro raso"), herb=("mat_erva_tonico", "Moita de erva"),
         exit_flag="selo:mata", exit_label="Cânions dos Suplicantes",
         desc="Árvores ocas com galhos como braços; a Vila do Moinho resiste. Lobos, javalis, aranhas e raízes andantes. A Cripta do Sino fica sob a torre.",
         species=[sp("lobo_cinzento", "Lobo Cinzento", "beast_lunge", "wolf", 17, 0.9, 1.9, "agil", [("mat_couro", 0.4)]),
                  sp("javali_ferro", "Javali de Ferro", "boar_charge", "boar", 18, 1.3, 2.1, "tanque", [("mat_carne_javali", 0.55), ("mat_couro", 0.3)], 0.8),
                  sp("aranha_toca", "Aranha de Toca", "spider_leap", "spider", 18, 0.9, 2.0, "agil", [("mat_seda", 0.45)]),
                  sp("raiz_andante", "Raiz Andante", "brute_slam", "root", 19, 1.4, 2.0, "tanque", [("mat_resina", 0.5)], 0.8),
                  sp("saqueador_mata", "Saqueador da Mata", "melee_windup_swing", "raider", 19, 1.0, 2.3, "combatente", [("mat_retalho", 0.4)])],
         elite=sp("javali_anciao", "Javali Ancião", "elite_beast", "boar_big", 20, 3.4, 2.4, "tanque", [("mat_carne_javali", 1.0)], 1.2),
         rare=sp("lobo_branco", "Lobo Branco", "beast_lunge", "wolf_white", 19, 1.8, 1.6, "agil", [("mat_couro", 1.0)]),
         gate=sp("guardiao_retorcido", "Guardião Retorcido", "boss_root", "root_boss", 21, 2.2, 2.2, "tanque", [("mat_resina", 1.0)], 1.4),
         npcs=[("agricultora_benta", "Benta, a Agricultora", "aldeao", "missoes", "Os javalis comeram a horta toda. Ajuda a gente a recomeçar?"),
               ("cozinheiro_ivo", "Ivo, o Cozinheiro", "ferreiro", "missoes", "Ensopado bom começa com carne de javali e raiz da vigília."),
               ("carpinteiro_jorge", "Jorge, o Carpinteiro", "guarda", "missoes", "A ponte velha caiu. Com resina e rebites eu levanto outra."),
               ("mentora_selene", "Selene, a Guardiã do Sino", "arcanista", "missoes", "O sino enterrado ainda chama. Quem quer jurar, desce à cripta."),
               ("quadro_mata", "Quadro de Contratos", "quadro", "contratos", "Pedidos da vila — pagos ao entregar.")],
         dungeon=dict(id="cripta_do_sino", name="Cripta do Sino", band=(18, 22), theme="gruta",
                      mobs=[sp("esqueleto_cripta", "Esqueleto da Cripta", "melee_windup_swing", "skeleton", 19, 1.0, 2.2, "combatente", [("mat_osso_antigo", 0.5), ("mat_espada_quebrada", 0.05)]),
                            sp("acolito_espectral", "Acólito Espectral", "ghost_caster", "ghost", 20, 0.8, 2.0, "conjurador", [("mat_reagente", 0.25)])],
                      mini=sp("cavaleiro_sepulcral", "Cavaleiro Sepulcral", "elite_knight", "knight", 21, 4.5, 2.4, "tanque", [("mat_osso_antigo", 1.0), ("mat_espada_quebrada", 1.0)], 1.0),
                      boss=sp("guardiao_sino", "Guardião do Sino", "boss_bell", "bellguard", 22, 2.4, 2.2, "tanque", [("mat_osso_antigo", 1.0)], 1.5))),
    dict(id="canions", map="canions_suplicantes", name="Cânions dos Suplicantes", band=(20, 30), theme="canion", xp=2.5, world=(40, -20),
         hub="Posto do Elevador", tier="canion", ore=("mat_ferro_eco", "Veio de ferro de eco"), herb=("mat_erva_tonico", "Moita de erva"),
         exit_flag="selo:elevador", exit_label="Brejo da Viúva",
         desc="Desfiladeiros com nichos de almas pedindo piedade. O elevador só desce para quem calar o Carcereiro das Almas.",
         species=[sp("escorpiao", "Escorpião das Fendas", "melee_frontal_claw", "scorpion", 21, 1.0, 2.0, "combatente", [("mat_ferrao", 0.4), ("mat_quitina", 0.3)]),
                  sp("morcego", "Morcego de Caverna", "swarm_bite", "bat", 22, 0.7, 1.8, "agil", [("mat_membrana", 0.08)]),
                  sp("esqueleto_mineiro", "Esqueleto Mineiro", "melee_windup_swing", "skeleton", 24, 1.0, 2.2, "combatente", [("mat_osso_antigo", 0.5)]),
                  sp("espectro", "Espectro Suplicante", "ghost_caster", "ghost", 26, 0.8, 2.1, "conjurador", [("mat_reagente", 0.25)]),
                  sp("troll", "Troll das Fendas", "brute_slam", "troll", 28, 1.6, 2.2, "tanque", [("mat_couro", 0.4)], 0.9)],
         elite=sp("escorpiao_rei", "Escorpião-Rei", "elite_beast", "scorpion_big", 29, 3.6, 2.4, "tanque", [("mat_ferrao", 1.0), ("mat_quitina", 1.0)], 1.2),
         rare=sp("morcego_albino", "Morcego Albino", "swarm_bite", "bat_white", 27, 1.8, 1.5, "agil", [("mat_membrana", 1.0)]),
         gate=None,
         npcs=[("zelador_ascanio", "Ascânio, o Zelador", "aldeao", "missoes", "Cada nicho guarda uma alma. Algumas só querem ser lembradas."),
               ("mineiro_bras2", "Teodoro, o Mineiro", "ferreiro", "missoes", "O ferro daqui escuta. Leve a picareta e ouça."),
               ("tratador_dimas", "Dimas, o Tratador", "guarda", "missoes", "Um corcel preso no cânion. Se alguém o soltar, ele segue quem o salvou."),
               ("alma_ligia", "Lígia, alma do nicho", "arcanista", "missoes", "Meu medalhão... os mortos levaram."),
               ("quadro_canion", "Quadro de Contratos", "quadro", "contratos", "Pedidos do posto — pagos ao entregar.")],
         dungeon=dict(id="galerias_piedade", name="Galerias da Piedade", band=(23, 31), theme="gruta", requires_flag="acesso:galerias",
                      mobs=[sp("mineiro_oco", "Mineiro Oco", "melee_windup_swing", "skeleton", 25, 1.1, 2.2, "combatente", [("mat_ferro_eco", 0.3)]),
                            sp("espectro_galeria", "Espectro do Lamento", "ghost_caster", "ghost", 27, 0.9, 2.1, "conjurador", [("mat_reagente", 0.3)])],
                      mini=sp("troll_carcereiro", "Troll Carcereiro", "elite_beast", "troll", 29, 4.6, 2.4, "tanque", [("mat_couro", 1.0)], 1.1),
                      boss=sp("carcereiro_almas", "Carcereiro das Almas", "boss_necro", "necro_boss", 31, 2.4, 2.2, "conjurador", [("mat_memoria_selada", 1.0)], 1.3),
                      boss_grant="selo:elevador")),
    dict(id="brejo", map="brejo_da_viuva", name="Brejo da Viúva", band=(28, 36), theme="brejo", xp=2.7, world=(0, -15),
         hub="Vila Suspensa", tier="brejo", ore=("mat_ferro_eco", "Veio de ferro de eco"), herb=("mat_folha_amarga", "Moita de folha amarga"),
         exit_flag="rito:vespera", exit_label="Véspera",
         desc="Vila sobre estacas, água rasa e igrejas afundadas. Sapos, serpentes, sanguessugas e tecelãs. O rito para Véspera vem do Mosteiro.",
         species=[sp("sapo", "Sapo do Lodo", "swarm_bite", "frog", 29, 0.9, 1.9, "agil", [("mat_ferrao", 0.2)]),
                  sp("serpente", "Serpente do Brejo", "melee_frontal_claw", "serpent", 31, 1.0, 2.1, "combatente", [("mat_ferrao", 0.45)]),
                  sp("sanguessuga", "Sanguessuga Gigante", "swarm_bite", "leech", 32, 1.2, 2.0, "tanque", [("mat_reagente", 0.15)]),
                  sp("tecela", "Tecelã do Brejo", "spider_leap", "spider_weaver", 34, 1.0, 2.2, "agil", [("mat_seda", 0.6)])],
         elite=sp("basilisco", "Basilisco", "elite_beast", "basilisk", 35, 3.8, 2.5, "tanque", [("mat_ferrao", 1.0)], 1.3),
         rare=sp("sapo_dourado", "Sapo Dourado", "fugitive", "frog_gold", 33, 1.6, 1.0, "agil", []),
         gate=None,
         npcs=[("curandeira_odete", "Odete, a Curandeira", "curandeira", "missoes", "A água está amarga e o povo adoece. Preciso de amostras."),
               ("agricultor_caio", "Caio, o Agricultor", "aldeao", "missoes", "O canteiro alagou. Se a comporta abrir, ainda planto."),
               ("costureira_alba", "Alba, a Costureira", "curandeira", "missoes", "Seda de tecelã vira atadura boa."),
               ("escrivao_tadeu", "Tadeu, o Escrivão", "aldeao", "casas", "Há uma casa sem dono no Porto. Com a escritura e as moedas, é sua."),
               ("tratadora_nina", "Nina, a Tratadora", "guarda", "missoes", "Um pequeno draco caiu no brejo. Ferido, mas vivo."),
               ("quadro_brejo", "Quadro de Contratos", "quadro", "contratos", "Pedidos da vila — pagos ao entregar.")],
         dungeon=dict(id="mosteiro_afundado", name="Mosteiro Afundado", band=(30, 38), theme="gruta",
                      mobs=[sp("carnical", "Carniçal", "beast_lunge", "ghoul", 33, 1.0, 2.2, "agil", [("mat_osso_antigo", 0.4)]),
                            sp("tecela_mosteiro", "Tecelã do Mosteiro", "spider_leap", "spider_weaver", 35, 1.0, 2.2, "agil", [("mat_seda", 0.6)])],
                      mini=sp("viuva_tecela", "Viúva Tecelã", "elite_beast", "spider_queen", 36, 4.8, 2.5, "tanque", [("mat_seda", 1.0)], 1.3),
                      boss=sp("abadessa_lodo", "Abadessa do Lodo", "boss_necro", "necro_boss", 38, 2.5, 2.3, "conjurador", [("mat_reagente", 1.0)], 1.3))),
    dict(id="vespera", map="vespera", name="Véspera, a Cidade do Outro Lado", band=(34, 44), theme="fantasma", xp=3.1, world=(-40, -10),
         hub="Praça dos Sinos", tier="vespera", ore=("mat_ferro_eco", "Veio de ferro de eco"), herb=("mat_folha_amarga", "Moita de folha amarga"),
         exit_flag="passo:geada", exit_label="Coroa da Geada",
         desc="Cidade fantasma reconhecível: mercados vazios, sinos e moradores presos em lembranças. Afogados, carniçais, soldados espectrais e necromantes.",
         species=[sp("afogado", "Afogado", "melee_windup_swing", "drowned", 36, 1.0, 2.1, "combatente", [("mat_carta_perdida", 0.35)]),
                  sp("carnical_vespera", "Carniçal de Véspera", "beast_lunge", "ghoul", 38, 1.0, 2.2, "agil", [("mat_osso_antigo", 0.4)]),
                  sp("soldado_espectral", "Soldado Espectral", "melee_windup_swing", "ghost_knight", 40, 1.2, 2.3, "combatente", [("mat_ficha_mercado", 0.45)]),
                  sp("necromante", "Necromante", "ghost_caster", "necro", 42, 0.9, 2.2, "conjurador", [("mat_reagente", 0.4)])],
         elite=sp("cavaleiro_sem_rosto", "Cavaleiro Sem Rosto", "elite_knight", "knight", 43, 4.0, 2.5, "tanque", [("mat_ficha_mercado", 1.0)], 1.1),
         rare=sp("sineiro", "Sineiro Perdido", "fugitive", "ghost", 41, 1.6, 1.0, "agil", []),
         gate=None,
         npcs=[("guia_aurelio", "Aurélio, o Guia", "arcanista", "missoes", "Véspera não amanhece. Fique perto das luzes."),
               ("carteiro_espectral", "Carteiro Espectral", "aldeao", "missoes", "Cartas que nunca chegaram... ajuda um morto a terminar o turno?"),
               ("mercador_espectral", "Mercador Espectral", "aldeao", "loja", "Aceito fichas. Os vivos pagam em moedas."),
               ("cozinheiro_espectral", "Cozinheiro Espectral", "ferreiro", "missoes", "Um banquete para quem não pode mais comer."),
               ("ferreiro_espectral", "Ferreiro Espectral", "ferreiro", "missoes", "Ferro que lembra vira relíquia. Traga fragmentos."),
               ("quadro_vespera", "Quadro de Contratos", "quadro", "contratos", "Pedidos dos mortos — pagos ao entregar.")],
         dungeon=dict(id="mercado_mortos", name="Mercado dos Mortos", band=(37, 45), theme="gruta",
                      mobs=[sp("soldado_mercado", "Soldado do Mercado", "melee_windup_swing", "ghost_knight", 40, 1.2, 2.3, "combatente", [("mat_fragmento_reliquia", 0.25)]),
                            sp("necromante_mercado", "Necromante do Mercado", "ghost_caster", "necro", 42, 0.9, 2.2, "conjurador", [("mat_fragmento_reliquia", 0.25)])],
                      mini=sp("cobrador_sem_rosto", "Cobrador Sem Rosto", "elite_knight", "knight", 43, 5.0, 2.5, "tanque", [("mat_fragmento_reliquia", 1.0)], 1.1),
                      boss=sp("regente_vespera", "Regente de Véspera", "boss_caster", "necro_boss", 45, 2.6, 2.3, "conjurador", [("mat_fragmento_reliquia", 1.0)], 1.4))),
    dict(id="geada", map="coroa_da_geada", name="Coroa da Geada", band=(42, 50), theme="neve", xp=3.3, world=(-60, -40),
         hub="Caravana da Fortaleza", tier="geada", ore=("mat_ferro_geada", "Veio de ferro de geada"), herb=("mat_erva_inverno", "Moita de erva de inverno"),
         exit_flag="selo:geada", exit_label="Mar de Escória",
         desc="Fortaleza de montanha, pontes e ruínas nórdicas. Lobos de gelo, draugr, trolls e gigantes. O Rei Draugr guarda o passo.",
         species=[sp("lobo_gelo", "Lobo de Gelo", "beast_lunge", "wolf_ice", 43, 0.9, 2.0, "agil", [("mat_membrana", 0.3), ("mat_couro", 0.3)]),
                  sp("draugr", "Draugr", "melee_windup_swing", "draugr", 45, 1.1, 2.3, "combatente", [("mat_osso_antigo", 0.5)]),
                  sp("troll_gelo", "Troll de Gelo", "brute_slam", "troll_ice", 47, 1.6, 2.3, "tanque", [("mat_couro", 0.4)], 0.9),
                  sp("gigante", "Gigante da Neve", "brute_slam", "giant", 49, 2.0, 2.4, "tanque", [("mat_membrana", 0.6)], 1.2)],
         elite=sp("draugr_campeao", "Draugr Campeão", "elite_knight", "draugr", 49, 4.2, 2.6, "tanque", [("mat_ferro_geada", 1.0)], 1.0),
         rare=sp("lobo_lendario", "Lobo de Neve Antigo", "beast_lunge", "wolf_white", 47, 2.0, 1.7, "agil", [("mat_pluma_neve", 1.0)]),
         gate=sp("rei_draugr", "Rei Draugr", "boss_draugr", "draugr_king", 51, 2.6, 2.4, "tanque", [("mat_ferro_geada", 1.0)], 1.5),
         npcs=[("batedora_runa", "Runa, a Batedora", "guarda", "missoes", "Gente perdida na neve. E o Rei Draugr fechou o passo."),
               ("ferreiro_caravana", "Gunnar, ferreiro da caravana", "ferreiro", "missoes", "Ferro de geada não quebra no frio."),
               ("cozinheira_hild", "Hild, a Cozinheira", "curandeira", "missoes", "Ninguém atravessa a neve de barriga vazia."),
               ("artesa_ilse", "Ilse, a Artesã", "arcanista", "missoes", "Liga, membrana e um núcleo que sustente. Assim nascem asas."),
               ("mentor_harald", "Harald, o Segundo Juramento", "mentor", "missoes", "No nível 50 a classe se divide. Escolha o seu caminho."),
               ("quadro_geada", "Quadro de Contratos", "quadro", "contratos", "Pedidos da caravana — pagos ao entregar.")],
         dungeon=dict(id="salao_geada", name="Salão da Geada", band=(45, 52), theme="gruta",
                      mobs=[sp("draugr_salao", "Draugr do Salão", "melee_windup_swing", "draugr", 47, 1.1, 2.3, "combatente", [("mat_osso_antigo", 0.5)]),
                            sp("troll_salao", "Troll do Salão", "brute_slam", "troll_ice", 49, 1.6, 2.3, "tanque", [("mat_membrana", 0.4)], 0.9)],
                      mini=sp("campeao_draugr", "Campeão Draugr", "elite_knight", "draugr", 50, 5.0, 2.6, "tanque", [("mat_ferro_geada", 1.0)], 1.0),
                      boss=sp("jotunn", "Jötunn Acorrentado", "boss_giant", "giant_boss", 52, 2.8, 2.4, "tanque", [("mat_membrana", 1.0)], 1.8))),
    dict(id="escoria", map="mar_de_escoria", name="Mar de Escória", band=(48, 56), theme="lava", xp=3.5, world=(-20, -55),
         hub="Refúgio dos Ferreiros", tier="escoria", ore=("mat_basalto", "Veio de basalto férreo"), herb=("mat_cogumelo_cinza", "Cogumelos de cinza"),
         exit_flag="selo:escoria", exit_label="Acrópole das Cinzas",
         desc="Ilhas de basalto, forjas e pontes de corrente sobre lava. Besouros de brasa, carbonizados, elementais e golems. O Colosso guarda a ponte.",
         species=[sp("besouro_brasa", "Besouro de Brasa", "melee_frontal_claw", "beetle", 49, 1.2, 2.1, "tanque", [("mat_quitina", 0.5)], 0.8),
                  sp("carbonizado", "Carbonizado", "melee_windup_swing", "charred", 51, 1.0, 2.3, "combatente", [("mat_osso_antigo", 0.3)]),
                  sp("elemental", "Elemental de Escória", "ghost_caster", "elemental", 53, 1.0, 2.3, "conjurador", [("mat_basalto", 0.2)]),
                  sp("golem", "Golem de Basalto", "brute_slam", "golem", 55, 2.0, 2.4, "tanque", [("mat_basalto", 0.5)], 1.1)],
         elite=sp("ferreiro_brasa", "Ferreiro de Brasa", "elite_knight", "charred", 54, 4.2, 2.6, "combatente", [("mat_basalto", 1.0), ("mat_molde_ferreiro", 0.4)], 1.0),
         rare=sp("salamandra", "Salamandra Antiga", "beast_lunge", "lizard", 52, 2.0, 1.7, "agil", [("mat_cogumelo_cinza", 1.0)]),
         gate=sp("colosso_escoria", "Colosso de Escória", "boss_golem", "golem_boss", 57, 2.8, 2.5, "tanque", [("mat_basalto", 1.0)], 1.8),
         npcs=[("mestre_forja", "Mestre da Forja", "mentor", "missoes", "Onde a pedra ferve, o ferro aprende a não ceder."),
               ("cozinheira_brasa", "Brasa, a Cozinheira", "curandeira", "missoes", "Caldo de cinzas protege do calor de dentro."),
               ("tratador_fenix", "Ícaro, o Tratador", "guarda", "missoes", "Há um ninho de fênix nas brasas. Ela renasce, mas não sozinha."),
               ("caravaneira_lu", "Lu, a Caravaneira", "aldeao", "missoes", "Suprimentos para a Acrópole — se alguém abrir a rota."),
               ("quadro_escoria", "Quadro de Contratos", "quadro", "contratos", "Pedidos do refúgio — pagos ao entregar.")],
         dungeon=dict(id="fornalha", name="Fornalha dos Nascidos do Fogo", band=(51, 58), theme="gruta",
                      mobs=[sp("elemental_fornalha", "Elemental da Fornalha", "ghost_caster", "elemental", 54, 1.0, 2.3, "conjurador", [("mat_fragmento_fornalha", 0.15)]),
                            sp("golem_fornalha", "Golem da Fornalha", "brute_slam", "golem", 56, 2.0, 2.4, "tanque", [("mat_fragmento_fornalha", 0.15)], 1.1)],
                      mini=sp("ferreiro_corrompido", "Ferreiro Corrompido", "elite_knight", "charred", 57, 5.0, 2.6, "combatente", [("mat_fragmento_fornalha", 1.0), ("mat_molde_ferreiro", 1.0)], 1.0),
                      boss=sp("coracao_escoria", "Coração de Escória", "boss_heart", "elemental_boss", 58, 2.8, 2.5, "conjurador", [("mat_fragmento_fornalha", 1.0)], 1.6))),
    dict(id="acropole", map="acropole_das_cinzas", name="Acrópole das Cinzas", band=(55, 60), theme="acropole", xp=3.6, world=(25, -75),
         hub="Posto da Sentinela", tier="acropole", ore=("mat_bronze", "Veio de bronze antigo"), herb=("mat_erva_tonico", "Moita de erva"),
         exit_flag=None, exit_label=None,
         desc="Cidade-templo quebrada sobre penhascos: bronze, mármore queimado e céu carregado. Guardiões de bronze, harpias, centauros e minotauros.",
         species=[sp("guardiao_bronze", "Guardião de Bronze", "melee_windup_swing", "bronze_guard", 56, 1.4, 2.3, "tanque", [("mat_bronze", 0.4), ("mat_estandarte", 0.04)]),
                  sp("harpia", "Harpia", "swarm_bite", "harpy", 57, 0.9, 2.2, "agil", [("mat_pluma_neve", 0.2)]),
                  sp("centauro", "Centauro", "ghost_caster", "centaur", 58, 1.2, 2.3, "conjurador", [("mat_couro", 0.4)], 0.9),
                  sp("minotauro_jovem", "Minotauro Jovem", "boar_charge", "minotaur", 59, 1.8, 2.4, "tanque", [("mat_couro", 0.4)], 1.0)],
         elite=sp("guardiao_ritual", "Guardião do Ritual", "elite_knight", "bronze_guard", 59, 4.4, 2.6, "tanque", [("mat_bronze", 1.0), ("mat_estandarte", 1.0)], 1.1),
         rare=sp("harpia_rainha", "Harpia Rainha", "swarm_bite", "harpy", 58, 2.0, 1.8, "agil", [("mat_pluma_neve", 1.0)]),
         gate=None,
         npcs=[("sentinela_acropole", "A Sentinela", "guarda", "missoes", "Os juramentos daqui ainda valem. Prove o seu."),
               ("artesa_acropole", "Dora, a Artesã do Bronze", "ferreiro", "missoes", "Bronze antigo e memória: assim se faz a chave."),
               ("quadro_acropole", "Quadro de Contratos", "quadro", "contratos", "Pedidos do posto — pagos ao entregar.")],
         dungeon=dict(id="labirinto_juramento", name="Labirinto do Juramento", band=(57, 60), theme="gruta",
                      mobs=[sp("guardiao_labirinto", "Guardião do Labirinto", "melee_windup_swing", "bronze_guard", 58, 1.4, 2.3, "tanque", [("mat_bronze", 0.4)]),
                            sp("centauro_labirinto", "Centauro do Labirinto", "ghost_caster", "centaur", 59, 1.2, 2.3, "conjurador", [("mat_couro", 0.3)], 0.9)],
                      mini=sp("minotauro_juramento", "Minotauro do Juramento", "elite_beast", "minotaur_big", 60, 5.2, 2.7, "tanque", [("mat_bronze", 1.0)], 1.3),
                      boss=sp("quimera_bronze", "Quimera de Bronze", "boss_chimera", "chimera", 60, 3.0, 2.6, "tanque", [("mat_nucleo_superior", 1.0)], 1.8))),
]

# =====================================================================================================================
# Construtores: monstros, loot, mapas de campo e dungeons
# =====================================================================================================================
OBST = {  # (kind, forma, raio mín, raio máx) espalhados pelo mapa (com colisão no servidor)
    "mata": [("arvore_seca", 0.6, 0.8), ("arvore", 0.7, 1.0), ("rocha", 1.0, 1.8)],
    "canion": [("rocha", 1.2, 2.6), ("coluna_quebrada", 0.5, 0.5), ("nicho_almas", 1.0, 1.0)],
    "brejo": [("arvore_seca", 0.6, 0.8), ("rocha", 0.9, 1.6), ("estacas", 1.8, 1.8)],
    "fantasma": [("coluna_quebrada", 0.5, 0.5), ("rocha", 0.9, 1.4), ("lapide", 1.0, 1.0)],
    "neve": [("rocha", 1.2, 2.4), ("arvore", 0.7, 1.0), ("cristal_gelo", 0.9, 0.9)],
    "lava": [("rocha_lava", 1.2, 1.2), ("rocha", 1.2, 2.2)],
    "acropole": [("coluna_quebrada", 0.5, 0.5), ("estatua_bronze", 0.9, 0.9), ("rocha", 1.0, 1.8)],
}
PROPS = {
    "mata": ["pinheiros", "lapide", "estandarte"], "canion": ["cranios", "tocha"], "brejo": ["estacas", "barril"],
    "fantasma": ["lapide", "poste", "estandarte"], "neve": ["pinheiros", "estandarte"], "lava": ["tocha", "barril"],
    "acropole": ["estandarte", "coluna_quebrada"],
}


def mid(rid, key): return f"monster_{rid}_{key}"


def coins_for(lvl, mult=1.0):
    return int(lvl * 1.4 * mult), int(lvl * 2.8 * mult)


def tier_pick(tier, kinds=("weapon", "armor", "helmet", "shield")):
    ids = []
    if "weapon" in kinds: ids += [f"weapon_guerreiro_espada_{tier}", f"weapon_guerreiro_machado_{tier}", f"staff_mago_cajado_{tier}",
                                  f"talisma_bruxa_{tier}", f"arco_arqueiro_{tier}"]
    if "armor" in kinds: ids += [f"armor_guerreiro_{tier}", f"armor_mago_{tier}", f"armor_bruxa_{tier}", f"armor_arqueiro_{tier}"]
    if "helmet" in kinds: ids += [f"helmet_{tier}"]
    if "shield" in kinds: ids += [f"shield_guerreiro_{tier}"]
    return ids


def potion_for(lvl):
    return "consumable_c05_tonico_forte" if lvl >= 30 else "consumable_c02_tonico_medio"


MONSTERS, LOOT = [], []


def add_monster(rid, s, kind, tier, extra_loot=()):
    """kind: comum | elite | raro | mini | chefe | passagem."""
    mult = {"comum": 1, "elite": 6, "raro": 8, "mini": 8, "chefe": 18, "passagem": 20}[kind]
    cmin, cmax = coins_for(s["lvl"], mult)
    xp = {"comum": 1.0, "elite": 3.5, "raro": 4.0, "mini": 5.0, "chefe": 9.0, "passagem": 10.0}[kind]
    m = {"id": mid(rid, s["key"]), "species_name": s["name"], "archetype": s["arch"], "level": s["lvl"], "resistances": {},
         "behavior_id": s["beh"], "loot_table_id": f"loot_{rid}_{s['key']}", "visual_asset_id": s["vis"],
         "health_mult": s["hp"], "damage_mult": s["dmg"], "xp_mult": xp, "radius": s["radius"], "coins_min": cmin, "coins_max": cmax}
    if kind in ("elite", "mini"): m["elite"] = True
    if kind == "raro": m["rare"] = True
    if kind in ("chefe", "passagem"): m["boss"] = True
    MONSTERS.append(m)
    entries = [{"chance": c, "item": it} for it, c in s["mats"]]
    lvl = s["lvl"]
    if kind == "comum":
        entries += [{"chance": 0.05, "pick": tier_pick(tier), "match_class": True},
                    {"chance": 0.12, "pick": ["consumable_c03_frasco_mana", potion_for(lvl)], "match_class": True},
                    {"chance": 0.03, "pick": [x[0] for x in SEEDS]}]
    elif kind == "raro":
        entries += [{"chance": 1.0, "pick": tier_pick(tier), "match_class": True, "quality": "raro"}]
    elif kind in ("elite", "mini"):
        entries += [{"chance": 1.0, "pick": tier_pick(tier), "match_class": True},
                    {"chance": 1.0, "item": potion_for(lvl), "min_qty": 1, "max_qty": 2},
                    {"chance": 0.3, "pick": ["consumable_c04_frasco_mana_grande", potion_for(lvl)], "match_class": True}]
    else:
        entries += [{"chance": 1.0, "pick": tier_pick(tier, ("weapon",)), "match_class": True, "quality": "chefe"},
                    {"chance": 1.0, "pick": tier_pick(tier, ("armor", "helmet", "shield")), "match_class": True},
                    {"chance": 1.0, "item": potion_for(lvl), "min_qty": 2, "max_qty": 3},
                    {"chance": 0.5, "pick": ["consumable_c04_frasco_mana_grande", potion_for(lvl)], "match_class": True}]
    entries += list(extra_loot)
    LOOT.append({"id": f"loot_{rid}_{s['key']}", "entries": entries})
    return m["id"]


def box(kind, x, y, w, h, height=4.0):
    return {"kind": kind, "shape": "box", "x": round(x, 2), "y": round(y, 2), "w": round(w, 2), "h": round(h, 2), "r": 0, "height": height}


def circle(kind, x, y, r, height=3.0):
    return {"kind": kind, "shape": "circle", "x": round(x, 2), "y": round(y, 2), "w": 0, "h": 0, "r": round(r, 2), "height": height}


def far_from(x, y, pts, d):
    return all((x - px) ** 2 + (y - py) ** 2 >= d * d for px, py in pts)


def field_map(reg, prev_map, prev_point, next_map, legend_extra=()):
    rid, theme, (lo, hi) = reg["id"], reg["theme"], reg["band"]
    H = 75
    tier = reg["tier"]
    zones = [
        {"id": "hub", "name": reg["hub"], "kind": "refugio", "x": 0, "y": 58, "w": 24, "h": 14},
        {"id": "estrada", "name": "Estrada", "kind": "caminho", "x": 0, "y": 4, "w": 5, "h": 96},
    ]
    pockets = [(-42, 34, "fraco"), (42, 34, "medio"), (-42, -10, "medio"), (42, -10, "forte")]
    species = reg["species"]
    spawns, keep = [], [(0, 58), (0, 64), (0, -50), (0, -66)]
    for i, (px, py, kind) in enumerate(pockets):
        pair = [species[i % len(species)], species[(i + 1) % len(species)]]
        lvl = lo + round((hi - lo) * i / 3)
        zones.append({"id": f"bolsao{i}", "name": f"{plural(pair[0]['name'].split()[0])} e {plural(pair[1]['name'].split()[0])}", "kind": kind,
                      "x": px, "y": py, "w": 34, "h": 30})
        for g in range(3):
            gx, gy = px + (g - 1) * 10, py + (6 if g % 2 else -6)
            for k, s in enumerate(pair):
                spawns.append({"id": f"{rid}_{i}_{g}_{k}", "monster": mid(rid, s["key"]), "x": gx + k * 2.2, "y": gy + k * 1.5,
                               "level": max(s["lvl"] - 1, min(hi, lvl + (k % 2))), "group": f"{rid}_{i}_{g}", "respawn_seconds": 35})
                keep.append((gx + k * 2.2, gy + k * 1.5))
    zones.append({"id": "covil", "name": f"Covil do {reg['elite']['name']}", "kind": "perigoso", "x": 0, "y": 14, "w": 24, "h": 18})
    spawns.append({"id": f"{rid}_elite", "monster": mid(rid, reg["elite"]["key"]), "x": 0, "y": 14, "level": reg["elite"]["lvl"],
                   "group": f"{rid}_elite", "respawn_seconds": 180})
    for k in range(2):
        s = species[-1]
        spawns.append({"id": f"{rid}_elite_add{k}", "monster": mid(rid, s["key"]), "x": -4 + 8 * k, "y": 17, "level": hi,
                       "group": f"{rid}_elite", "respawn_seconds": 60})
    keep += [(0, 14), (-4, 17), (4, 17)]
    spawns.append({"id": f"{rid}_raro", "monster": mid(rid, reg["rare"]["key"]), "x": 62, "y": 50, "level": reg["rare"]["lvl"],
                   "group": f"{rid}_raro", "respawn_seconds": 600, "chance": 0.35, "alt_points": [[62, 50], [-62, 50], [60, -30], [-60, 15]]})
    keep += [(62, 50), (-62, 50), (60, -30), (-60, 15)]
    obstacles, gates, portals = [], [], []
    # saída norte: muralha com portão (selo/rito/passo) — só para quem tem a marca
    if reg["exit_flag"]:
        obstacles += [box("muralha", -40, -62, 70, 1.4, 5), box("muralha", 40, -62, 70, 1.4, 5)]
        gates.append({"id": f"passagem_{rid}", "flag": reg["exit_flag"], "x": 0, "y": -62, "w": 10, "h": 1.4,
                      "lever_x": 0, "lever_y": -58, "kind": "selo", "lever": False})
        portals.append({"id": f"{rid}_saida_norte", "x": 0, "y": -71, "radius": 2.2, "to_map": next_map, "to_x": 0, "to_y": 62,
                        "label": reg["exit_label"]})
    if reg["gate"]:
        zones.append({"id": "passagem", "name": f"Domínio do {reg['gate']['name']}", "kind": "chefe", "x": 0, "y": -48, "w": 34, "h": 18})
        spawns.append({"id": f"{rid}_guardiao", "monster": mid(rid, reg["gate"]["key"]), "x": 0, "y": -50, "level": reg["gate"]["lvl"],
                       "group": f"{rid}_guardiao", "respawn_seconds": 600, "grant_flag": reg["exit_flag"], "health_mult": 2.5, "damage_mult": 1.1})
    portals.append({"id": f"{rid}_volta", "x": 0, "y": 72, "radius": 2.2, "to_map": prev_map, "to_x": prev_point[0], "to_y": prev_point[1],
                    "label": "Voltar"})
    d = reg["dungeon"]
    portals.append({"id": f"{rid}_dungeon", "x": -62, "y": -46, "radius": 2.0, "to_map": d["id"], "to_x": 0, "to_y": 33,
                    "label": d["name"], "min_level": d["band"][0], **({"requires_flag": d["requires_flag"]} if d.get("requires_flag") else {})})
    keep += [(-62, -46), (0, -71), (0, 72)]
    objects = []
    for i, (ox, oy) in enumerate([(-30, 46), (30, 46), (-55, -25), (55, -28)]):
        objects.append({"id": f"{rid}_veio{i}", "kind": "veio", "x": ox, "y": oy, "label": reg["ore"][1], "yields": reg["ore"][0],
                        "yield_min": 1, "yield_max": 2, "respawn_seconds": 120, "requires_flag": "tool:picareta"})
    for i, (ox, oy) in enumerate([(-14, 48), (14, 48), (-20, 0), (22, 2)]):
        objects.append({"id": f"{rid}_erva{i}", "kind": "veio", "x": ox, "y": oy, "label": reg["herb"][1], "yields": reg["herb"][0],
                        "yield_min": 1, "yield_max": 2, "respawn_seconds": 90})
    keep += [(o["x"], o["y"]) for o in objects]
    # estações e canteiros no refúgio
    objects += [{"id": f"{rid}_panela", "kind": "panela", "x": -9, "y": 54, "label": "Panela"},
                {"id": f"{rid}_caldeirao", "kind": "caldeirao", "x": 9, "y": 54, "label": "Caldeirão"}]
    if rid in ("mata", "brejo"):
        objects += [{"id": f"{rid}_canteiro{i}", "kind": "canteiro", "x": -6 + 4 * i, "y": 52, "label": "Canteiro comunitário"} for i in range(4)]
    if rid in ("vespera", "geada", "escoria"):
        objects += [{"id": f"{rid}_forja", "kind": "forja", "x": 13, "y": 61, "label": "Forja"},
                    {"id": f"{rid}_bancada", "kind": "bancada", "x": -13, "y": 61, "label": "Bancada"}]
    keep += [(o["x"], o["y"]) for o in objects]
    npcs = []
    for i, (nid, name, look, role, greet) in enumerate(reg["npcs"]):
        nx = -10 + i * (20 / max(1, len(reg["npcs"]) - 1))
        npc = {"id": nid, "name": name, "role": role, "x": round(nx, 1), "y": 62, "facing": 180, "greeting": greet, "look": look}
        if role == "loja": npc["shop"] = f"loja_{rid}"
        npcs.append(npc)
    # obstáculos espalhados (sem bloquear estrada, refúgio, bolsões e passagens) + borda
    kinds = OBST[theme]
    placed = 0
    tries = 0
    while placed < 46 and tries < 4000:
        tries += 1
        x, y = rnd.uniform(-H + 6, H - 6), rnd.uniform(-H + 10, H - 6)
        if abs(x) < 7 or (abs(x) < 14 and y > 48) or abs(y + 62) < 5 or abs(y + 48) < 11 and abs(x) < 20:
            continue
        if not far_from(x, y, keep, 5.5) or not far_from(x, y, [(o["x"], o["y"]) for o in obstacles if o["shape"] == "circle"], 4.5):
            continue
        k, r0, r1 = kinds[rnd.randrange(len(kinds))]
        obstacles.append(circle(k, x, y, rnd.uniform(r0, r1), 4))
        placed += 1
    for t in range(-H + 4, H - 3, 9):
        for (x, y) in [(t, -H + 3), (t, H - 3), (-H + 3, t), (H - 3, t)]:
            if abs(x) < 6 and abs(y) > 60:
                continue
            k, r0, r1 = kinds[0]
            obstacles.append(circle(k, x + rnd.uniform(-1.5, 1.5), y + rnd.uniform(-1.5, 1.5), max(r0, 0.8), 4))
    props = [{"kind": "fogueira", "x": 0, "y": 57, "yaw": 0}]
    for x in (-12, 12):
        props.append({"kind": "poste", "x": x, "y": 56, "yaw": 90 if x < 0 else -90})
    for i in range(14):
        x, y = rnd.uniform(-H + 8, H - 8), rnd.uniform(-H + 12, H - 8)
        if abs(x) < 7 or not far_from(x, y, keep, 4):
            continue
        k = PROPS[theme][i % len(PROPS[theme])]
        props.append({"kind": k, "x": round(x, 1), "y": round(y, 1), "yaw": rnd.randrange(360)})
    chests = [{"id": f"bau_{rid}_segredo", "x": -66, "y": 64, "loot_table": f"bau_{rid}", "facing": 135, "secret": True}]
    LOOT.append({"id": f"bau_{rid}", "entries": [{"chance": 1.0, "pick": tier_pick(tier), "match_class": True, "quality": "raro"},
                                                    {"chance": 1.0, "item": potion_for(lo), "min_qty": 2, "max_qty": 3},
                                                    {"chance": 0.6, "pick": [x[0] for x in SEEDS]}]})
    return {
        "id": reg["map"], "display_name": reg["name"], "half_size": H, "spawn_x": 0, "spawn_y": 62, "safe_radius": 6,
        "xp_multiplier": reg["xp"], "kind": "campo", "region": rid, "theme": theme,
        "zones": zones, "obstacles": obstacles, "gates": gates, "chests": chests, "spawns": spawns, "props": props,
        "portals": portals, "waypoints": [{"id": f"wp_{rid}", "name": reg["hub"], "x": 0, "y": 55.5}], "npcs": npcs,
        "objects": objects, "checkpoints": [],
    }


def room_walls(cx, cy, w, h, doors, height=5.0):
    """Paredes de uma sala retangular com vãos (doors: {'n': [(offset, largura)], ...}); norte = y menor."""
    out = []
    sides = {"n": (cx, cy - h / 2, w, True), "s": (cx, cy + h / 2, w, True), "w": (cx - w / 2, cy, h, False), "e": (cx + w / 2, cy, h, False)}
    for side, (sx, sy, length, horiz) in sides.items():
        gaps = sorted(doors.get(side, []))
        start = -length / 2
        for off, gw in gaps + [(length / 2 + 1, 0)]:
            a, b = start, min(off - gw / 2, length / 2)
            if b - a > 0.2:
                c = (a + b) / 2
                out.append(box("muralha", sx + c, sy, b - a, 1.2, height) if horiz else box("muralha", sx, sy + c, 1.2, b - a, height))
            start = off + gw / 2
    return out


def dungeon_map(reg, field_map_id, exit_point):
    d = reg["dungeon"]
    did, (lo, hi) = d["id"], d["band"]
    tier = reg["tier"]
    rid = reg["id"]
    obstacles, spawns, zones = [], [], []
    rooms = [  # (id, nome, cx, cy, w, h, portas, tipo)
        ("entrada", "Vestíbulo", 0, 33, 16, 12, {"n": [(0, 4)]}, "seguro"),
        ("corredor1", "Corredor", 0, 23.5, 6, 7, {"n": [(0, 4)], "s": [(0, 4)]}, "caminho"),
        ("ala", "Ala Baixa", 0, 11, 26, 18, {"n": [(0, 4)], "s": [(0, 4)], "w": [(0, 4)]}, "medio"),
        ("lateral", "Câmara Lateral", -25, 11, 14, 14, {"e": [(0, 4)]}, "segredo"),
        ("corredor2", "Passagem", 0, -2.5, 6, 8, {"n": [(0, 4)], "s": [(0, 4)]}, "caminho"),
        ("guarda", "Sala da Guarda", 0, -14, 22, 15, {"n": [(0, 4)], "s": [(0, 4)]}, "forte"),
        ("antessala", "Antessala", 0, -25, 8, 7, {"n": [(0, 4)], "s": [(0, 4)]}, "seguro"),
        ("trono", "Salão do Chefe", 0, -38, 30, 19, {"s": [(0, 4)], "n": [(0, 4)]}, "chefe"),
    ]
    for rid2, name, cx, cy, w, h, doors, kind in rooms:
        obstacles += room_walls(cx, cy, w, h, doors)
        zones.append({"id": rid2, "name": name, "kind": kind, "x": cx, "y": cy, "w": w, "h": h})
    mob_a, mob_b = d["mobs"]
    pts = [(-7, 14, mob_a), (-3, 8, mob_b), (5, 15, mob_a), (8, 7, mob_b), (0, 11, mob_a)]
    for i, (x, y, s) in enumerate(pts):
        spawns.append({"id": f"{did}_ala{i}", "monster": mid(rid, s["key"]), "x": x, "y": y, "level": min(hi, s["lvl"]), "group": "ala", "respawn_seconds": 30})
    for i, (x, y, s) in enumerate([(-27, 8, mob_b), (-23, 14, mob_a), (-27, 14, mob_a)]):
        spawns.append({"id": f"{did}_lat{i}", "monster": mid(rid, s["key"]), "x": x, "y": y, "level": min(hi, s["lvl"] + 1), "group": "lateral", "respawn_seconds": 30})
    spawns.append({"id": "mini", "monster": mid(rid, d["mini"]["key"]), "x": 0, "y": -15, "level": d["mini"]["lvl"], "group": "guarda", "respawn_seconds": 30})
    for i, (x, s) in enumerate([(-6, mob_a), (6, mob_b)]):
        spawns.append({"id": f"{did}_guarda{i}", "monster": mid(rid, s["key"]), "x": x, "y": -12, "level": min(hi, s["lvl"] + 1), "group": "guarda", "respawn_seconds": 30})
    boss = {"id": "boss", "monster": mid(rid, d["boss"]["key"]), "x": 0, "y": -33, "level": d["boss"]["lvl"], "group": "boss", "respawn_seconds": 30}
    if d.get("boss_grant"): boss["grant_flag"] = d["boss_grant"]
    spawns.append(boss)
    props = [{"kind": "tocha", "x": x, "y": y, "yaw": 0} for x, y in [(-6, 30), (6, 30), (-11, 18), (11, 18), (-9, -8), (9, -8), (-13, -32), (13, -32), (-13, -44), (13, -44)]]
    props += [{"kind": "cranios", "x": -20, "y": 6, "yaw": 0}, {"kind": "sino", "x": 10, "y": -45, "yaw": 0}] if rid == "mata" else [{"kind": "cranios", "x": -20, "y": 6, "yaw": 0}]
    chests = [{"id": f"bau_{did}", "x": -28, "y": 8, "loot_table": f"bau_{did}", "facing": 90}]
    LOOT.append({"id": f"bau_{did}", "entries": [{"chance": 1.0, "pick": tier_pick(tier), "match_class": True},
                                                 {"chance": 1.0, "item": potion_for(lo), "min_qty": 2, "max_qty": 2}]})
    first = [{"chance": 1.0, "pick": tier_pick(tier, ("weapon",)), "match_class": True, "quality": "chefe"},
             {"chance": 1.0, "pick": tier_pick(tier, ("armor",)), "match_class": True},
             {"chance": 1.0, "item": potion_for(hi), "min_qty": 3, "max_qty": 3}] + list(d.get("first_extra", []))
    LOOT.append({"id": f"primeira_{did}", "entries": first})
    return {
        "id": did, "display_name": d["name"], "half_size": 45, "spawn_x": 0, "spawn_y": 33, "safe_radius": 5,
        "xp_multiplier": round(reg["xp"] * 1.15, 2), "kind": "dungeon", "region": rid, "theme": d["theme"], "instanced": True, "no_mount": True,
        "exit_map": field_map_id, "exit_x": exit_point[0], "exit_y": exit_point[1],
        "zones": zones, "obstacles": obstacles, "gates": [], "chests": chests, "spawns": spawns, "props": props,
        "portals": [{"id": f"{did}_saida", "x": 0, "y": 38, "radius": 1.8, "to_map": field_map_id, "to_x": exit_point[0], "to_y": exit_point[1], "label": "Saída"},
                    {"id": f"{did}_fim", "x": 0, "y": -46, "radius": 1.8, "to_map": field_map_id, "to_x": exit_point[0], "to_y": exit_point[1], "label": "Saída"}],
        "waypoints": [], "npcs": [], "objects": [],
        "checkpoints": [{"id": "antessala", "x": 0, "y": -25, "radius": 3, "label": "Antessala"}],
        "first_clear_loot": f"primeira_{did}", "first_clear_boss": mid(rid, d["boss"]["key"]),
    }

# =====================================================================================================================
# Missões: Q03–Q64 (cadeias), opcionais S01–S10 e contratos repetíveis por região
# =====================================================================================================================
PLURAIS = {"Raiz": "Raízes", "Saqueador": "Saqueadores", "Escorpião": "Escorpiões", "Guardião": "Guardiões", "Draugr": "Draugar",
           "Carniçal": "Carniçais", "Elemental": "Elementais", "Golem": "Golens"}


def plural(word):
    """Plural em português para nomes de bolsão ("Raiz" -> "Raízes"); o padrão é +s."""
    return PLURAIS.get(word, word if word.endswith("s") else word + "s")


def xp_to_next(L): return 100 * L * (30 + 18 * L)


QUESTS = []


def q(qid, name, giver, kind, targets, count, desc, hint, lvl, frac=0.06, coins=None, items=(), flags=(), requires=None,
      accept_items=(), accept_flags=(), cat="regiao", qmap=None, x=0, y=0, radius=5, cls=None, repeatable=False,
      requires_flag=None, forbid_flag=None, cost=0):
    d = {"id": qid, "name": name, "giver": giver, "kind": kind, "targets": list(targets), "count": count, "description": desc,
         "hint": hint, "min_level": lvl, "reward_xp": int(xp_to_next(max(1, lvl)) * frac), "reward_coins": coins if coins is not None else lvl * 20,
         "reward_items": list(items), "reward_flags": list(flags), "category": cat, "repeatable": repeatable}
    if requires: d["requires"] = requires
    if accept_items: d["accept_items"] = list(accept_items)
    if accept_flags: d["accept_flags"] = list(accept_flags)
    if qmap: d.update({"map": qmap, "x": x, "y": y, "radius": radius})
    if cls: d["class"] = cls
    if requires_flag: d["requires_flag"] = requires_flag
    if forbid_flag: d["forbid_flag"] = forbid_flag
    if cost: d["cost_coins"] = cost
    QUESTS.append(d)
    return qid


def build_quests():
    M = lambda r, k: mid(r, k)
    # ---------------- R01 (Porto/Costa): ofícios básicos ----------------
    q("q_m03", "Remédio do Areal", "curandeira_ilma", "mine", ["mat_erva_tonico"], 8,
      "Colha 8 Ervas de Tônico nas moitas espalhadas pelo Areal e pelas Dunas (segure F perto da moita). Cada moita só volta depois de 1 hora. Ilma ensina o tônico.",
      "Colha Ervas de Tônico nas moitas da Costa (perto do portão).", 2, 0.12, items=["mat_frasco*3", "mat_residuo_larva*2"], flags=["receita:tonico"], cat="oficio")
    q("q_m04", "Primeira Mistura", "curandeira_ilma", "craft", ["rec_tonico_simples"], 1,
      "No caldeirão da praça (F), prepare o Tônico Simples: 2 ervas, 1 resíduo de larva e 1 frasco.",
      "Prepare um Tônico Simples no caldeirão do Porto.", 2, 0.12, requires="q_m03", cat="oficio")
    q("q_m05", "Ferramenta Emprestada", "ferreiro_bras", "mine", ["mat_ferro_raso"], 3,
      "Leve minha picareta (já é sua) e tire 3 Ferros Rasos dos veios da Costa. Levar golpe interrompe a coleta.",
      "Minere Ferro Raso nos veios da Costa do Cal.", 4, 0.12, flags=["receita:placa"], accept_flags=["tool:picareta"], cat="oficio")
    q("q_m06", "Placa de Chapa", "ferreiro_bras", "collect", ["mat_placa_chapa"], 1,
      "Na forja (F), faça uma Placa de Chapa com 3 Ferros Rasos e 2 Cascas de Cal. Traga e faço sua peça.",
      "Forje uma Placa de Chapa e entregue ao Brás.", 4, 0.10, requires="q_m05",
      items=["@classe:shield_guerreiro_tabuas|staff_mago_coral_p1|talisma_bruxa_coral_p1|arco_arqueiro_coral_p1"], cat="oficio")
    q("q_m07", "Sob as Hortas", "agricultora_rosa", "harvest", ["mat_raiz_vigilia"], 2,
      "Plante as sementes na horta comunitária (F), regue e volte quando crescer (uns 3 minutos; vá caçar enquanto isso).",
      "Plante, regue e colha Raiz da Vigília na horta do Porto.", 3, 0.10, accept_items=["semente_raiz*2"],
      items=["semente_erva*2", "semente_raiz*2"], cat="oficio")
    q("q_s01", "O Anel do Poço", "pescador_tiberio", "kill", ["monster_r1_goblin_esqueleto"], 8,
      "Um esqueleto de goblin arrancou o anel da minha filha e sumiu no areal. Quebre 8 desses ossos andantes; um deles há de largar.", "Esqueletos de Goblin rondam o Areal (portão norte), entre os caranguejos.", 2, 0.08,
      items=["consumable_c01_tonico_fraco*3"], cat="opcional")

    # ---------------- R02 Mata dos Enforcados ----------------
    r = "mata"
    q("q_m09", "A Colheita Não Volta", "agricultora_benta", "kill", [M(r, "saqueador_mata")], 6,
      "Saqueadores da Mata destruíram a horta da vila. Derrube 6.", "Saqueadores da Mata (bolsões a leste).", 17)
    q("q_m10", "Plantar para Recomeçar", "agricultora_benta", "harvest", ["mat_raiz_vigilia"], 3,
      "Plante as sementes nos canteiros da vila, regue e colha 3 Raízes da Vigília.", "Plante, regue e colha na Vila do Moinho.", 17,
      requires="q_m09", accept_items=["semente_raiz*3"], flags=["receita:ensopado"], cat="oficio")
    q("q_m11", "Jantar da Vigília", "cozinheiro_ivo", "craft", ["rec_ensopado"], 1,
      "Na panela da vila: 1 Carne de Javali e 2 Raízes. O Ensopado dá força por 10 minutos (um efeito por vez).",
      "Cozinhe o Ensopado da Vigília na panela.", 17, requires="q_m10", items=["food_ensopado_vigilia*2"], cat="oficio")
    q("q_m12", "Madeira para a Ponte", "carpinteiro_jorge", "collect", ["mat_resina"], 4,
      "Raízes andantes soltam resina. Traga 4 para colar as tábuas.", "Resina das Raízes Andantes.", 18)
    q("q_m13", "Rebites que Faltam", "carpinteiro_jorge", "craft", ["mat_rebites"], 3,
      "Forje 3 rebites (2 Ferros Rasos cada fornada) — a forja do Porto serve. A ponte leste fica pronta para você.",
      "Forje Rebites.", 18, requires="q_m12", flags=["ponte:mata"], cat="oficio")
    q("q_m14", "O Sino Enterrado", "mentora_selene", "visit", ["cripta_do_sino"], 1,
      "Desça à Cripta do Sino (entrada a sudoeste da Mata) e veja a inscrição.", "Entre na Cripta do Sino.", 18,
      qmap="cripta_do_sino", x=0, y=26, radius=7, flags=["receita:insignia"])
    q("q_m15", "Insígnia do Juramento", "mentora_selene", "craft", ["mat_insignia"], 1,
      "Na bancada do Porto, faça a Insígnia do Juramento. É o que o mentor da sua classe vai pedir.", "Faça a Insígnia na bancada.",
      19, requires="q_m14", cat="historia")
    q("q_m16", "A Raiz na Estrada", "mentora_selene", "boss", [M(r, "guardiao_retorcido")], 1,
      "O Guardião Retorcido fecha a estrada dos Cânions. Vença-o (com grupo ou bem preparado) e o selo é seu.",
      "Vença o Guardião Retorcido no norte da Mata.", 20, 0.15, cat="historia")
    q("q_s02", "Uma Lâmina sem Dono", "carpinteiro_jorge", "interact", ["mata_espada"], 1,
      "Há uma espada cravada numa árvore oca, no oeste. Talvez ainda corte.", "Examine a espada na árvore oca.", 18,
      items=["@classe:weapon_guerreiro_espada_mata|staff_mago_cajado_mata|talisma_bruxa_mata|arco_arqueiro_mata"], cat="opcional")
    q("q_s03", "O Lobo que Não Uiva", "agricultora_benta", "kill", [M(r, "lobo_branco")], 1,
      "Um lobo branco ronda a mata sem uivar. Dizem que aparece em cantos diferentes.", "Encontre e vença o Lobo Branco (raro).", 18,
      0.08, items=["mat_couro*4"], flags=["receita:sela"], cat="opcional")
    q("q_s04", "A Colmeia no Caminho", "cozinheiro_ivo", "collect", ["mat_cera"], 3,
      "Aranhas de toca guardam cera de colmeia. Traga 3 — serve de vela em Véspera.", "Cera das Aranhas de Toca.", 18, 0.05, cat="opcional")

    # ---------------- R03 Cânions ----------------
    r = "canions"
    q("q_m17", "Vozes na Pedra", "zelador_ascanio", "visit", ["nicho"], 1,
      "Visite o grande nicho do leste e escute a história do elevador.", "Visite o Nicho Grande (leste dos Cânions).", 20,
      qmap="canions_suplicantes", x=55, y=8, radius=6)
    q("q_m18", "O Ferro que Escuta", "mineiro_bras2", "mine", ["mat_ferro_eco"], 4,
      "Ferro de Eco nos veios do cânion. Quatro pedaços.", "Minere Ferro de Eco.", 21, flags=["receita:ressonador"], cat="oficio")
    q("q_m19", "Um Pedido de Piedade", "alma_ligia", "kill", [M(r, "esqueleto_mineiro")], 6,
      "Os esqueletos mineiros levaram meu medalhão. Seis deles...", "Esqueletos Mineiros.", 22, items=["mat_memoria_selada"])
    q("q_m20", "Sela de Estrada", "tratador_dimas", "craft", ["rec_sela"], 1,
      "Couro de lobo ou javali, retalhos e rebites: faça uma sela na bancada.", "Faça a Sela Simples na bancada.", 21,
      accept_flags=["receita:sela"], cat="oficio")
    # 12/11 (auditoria): o corcel já vem da Rosa no 10 — aqui a sela vai para o lobo cinzento (montaria do 25)
    q("q_m21", "Um Companheiro de Viagem", "tratador_dimas", "collect", ["mat_sela"], 1,
      "O lobo cinzento só aceita sela feita à mão. Traga a sua e ele segue você — tecla V monta (fora de combate), mais rápido que o corcel.", "Entregue a Sela ao Dimas.", 25,
      0.10, requires="q_m20", flags=["mount:lobo"], cat="historia")
    q("q_m22", "Ressonador de Lamentos", "zelador_ascanio", "craft", ["rec_ressonador"], 1,
      "Ferro de eco e a memória selada: o ressonador abre as Galerias da Piedade.", "Faça o Ressonador na bancada.", 23,
      requires="q_m19", flags=["acesso:galerias"], cat="historia")
    q("q_m23", "O Carcereiro das Almas", "zelador_ascanio", "boss", [M(r, "carcereiro_almas")], 1,
      "No fundo das Galerias, o Carcereiro prende as almas e o elevador. Vença-o.", "Vença o Carcereiro (Galerias da Piedade).", 26,
      0.15, requires="q_m22", cat="historia")
    q("q_m24", "Entrega à Vila Suspensa", "zelador_ascanio", "talk", ["curandeira_odete"], 1,
      "Desça pelo elevador até o Brejo e leve notícias à curandeira Odete.", "Fale com Odete na Vila Suspensa (Brejo).", 27,
      requires="q_m23", cat="historia")
    q("q_s05", "Um Nome no Cânion", "alma_ligia", "collect", ["mat_osso_antigo"], 8,
      "Junte os ossos espalhados e eu lembro de um nome.", "Ossos Antigos (esqueletos).", 23, 0.05, items=["helmet_canion"], cat="opcional")
    q("q_s06", "A Mina Esquecida", "mineiro_bras2", "mine", ["mat_ferro_eco"], 8,
      "Uma veia funda que ninguém tira faz anos.", "Minere Ferro de Eco.", 24, 0.05, items=["mat_ferro_eco*4"], cat="opcional")

    # ---------------- R04 Brejo ----------------
    r = "brejo"
    q("q_m25", "Água Amarga", "curandeira_odete", "mine", ["mat_folha_amarga"], 3,
      "Folhas das moitas perto das fontes: são as amostras que preciso.", "Colha Folha Amarga nas moitas do Brejo.", 28, cat="oficio")
    q("q_m26", "Ferrão e Folha", "curandeira_odete", "collect", ["mat_ferrao"], 4,
      "Ferrões de serpente e escorpião. Quatro.", "Ferrões (serpentes do Brejo).", 29, requires="q_m25", flags=["receita:antidoto"])
    q("q_m27", "Antídoto para os Vivos", "curandeira_odete", "craft", ["rec_antidoto"], 1,
      "No caldeirão: 2 Folhas Amargas, 1 Ferrão, 1 Frasco. A vila agradece.", "Prepare o Antídoto do Brejo.", 29,
      requires="q_m26", items=["consumable_c07_antidoto*2"], cat="oficio")
    q("q_m28", "O Canteiro Alagado", "agricultor_caio", "harvest", ["mat_folha_amarga"], 2,
      "Abri a comporta. Plante a folha amarga nos canteiros da vila e colha.", "Plante e colha Folha Amarga.", 29,
      accept_items=["semente_folha*2"], items=["semente_cogumelo*2", "semente_flor*2"], flags=["receita:caldo"], cat="oficio")
    q("q_m29", "Tecidos para os Feridos", "costureira_alba", "craft", ["rec_ataduras"], 1,
      "Duas sedas e um retalho na bancada viram ataduras.", "Faça Ataduras na bancada.", 30, cat="oficio")
    # 12/11 (auditoria): a escritura é do Prefeito (nível 20); o Tadeu faz a REFORMA de quem já tem a casa
    q("q_m30", "A Reforma da Casa", "escrivao_tadeu", "visit", ["casa_porto"], 1,
      "Sua casa no Porto precisa de reforma: mais canteiros nos fundos e o telhado novo. Vá até a porta; ao voltar, pague 6000 moedas e a obra começa.",
      "Vá até a porta da sua casa no Porto e volte ao Tadeu.", 30, 0.06, coins=0, qmap="porto_das_cinzas", x=26, y=8, radius=4,
      flags=["casa:canteiros4"], cost=6000, cat="casa", requires_flag="casa:propria", forbid_flag="casa:canteiros4")
    q("q_m31a", "O Draco Ferido", "tratadora_nina", "interact", ["brejo_draco"], 1,
      "O pequeno draco está entre as raízes, ao norte da vila. Acalme-o (F).", "Encontre o draco ferido.", 30, cat="companheiro")
    q("q_m31", "A Centelha que Respira", "tratadora_nina", "collect", ["mat_refeicao_vinculo"], 1,
      "Ele só aceita comida feita por quem o salvou: carne, flor de vínculo e reagente, na panela.", "Prepare a Refeição de Vínculo.", 31,
      0.10, requires="q_m31a", accept_flags=["receita:vinculo"], flags=["pet:dragao"], cat="companheiro")
    q("q_m32", "O Mosteiro Afundado", "curandeira_odete", "boss", [M(r, "abadessa_lodo")], 1,
      "A Abadessa do Lodo envenena a água. Vença-a no Mosteiro e traga o sinete: ele abre o rito para Véspera.",
      "Vença a Abadessa (Mosteiro Afundado).", 33, 0.15, flags=["rito:vespera"], cat="historia")

    # ---------------- R05 Véspera ----------------
    r = "vespera"
    q("q_m33", "Véspera Não Amanhece", "guia_aurelio", "visit", ["praca_sinos"], 1,
      "Ande até o grande sino no centro da cidade. Lá você entende onde está.", "Vá ao Sino Grande de Véspera.", 34,
      qmap="vespera", x=0, y=14, radius=6)
    q("q_m34", "Cartas Nunca Entregues", "carteiro_espectral", "collect", ["mat_carta_perdida"], 5,
      "Os afogados guardam cartas. Traga cinco.", "Cartas dos Afogados.", 35)
    q("q_m35", "O Mercado Ainda Cobra", "mercador_espectral", "collect", ["mat_ficha_mercado"], 6,
      "Fichas de soldado espectral. Com elas, abro a banca para você.", "Fichas dos Soldados Espectrais.", 36)
    q("q_m36", "O Banquete dos Ausentes", "cozinheiro_espectral", "craft", ["rec_banquete"], 1,
      "Cogumelo, carne e flor de vínculo: um banquete memorial.", "Cozinhe o Banquete na panela.", 36,
      accept_flags=["receita:banquete"], items=["food_banquete_ausentes*2"], cat="oficio")
    q("q_m37", "Ferro que Lembra", "ferreiro_espectral", "craft", ["rec_base_reliquia"], 1,
      "Fragmentos de relíquia (Mercado dos Mortos) e ferro de eco viram a base.", "Forje a Base de Relíquia.", 37,
      accept_flags=["receita:reliquia"], cat="historia")
    q("q_m38", "O Regente Não Parte", "guia_aurelio", "boss", [M(r, "regente_vespera")], 1,
      "O Regente prende a cidade. Vença-o no Mercado dos Mortos; a 1ª vitória dá o Núcleo Lendário.", "Vença o Regente.", 39,
      0.15, requires="q_m37", cat="historia")
    q("q_m39", "Uma Arma com História", "ferreiro_espectral", "collect", ["mat_nucleo_lendario"], 1,
      "Base, núcleo e o que sua classe pede. Sai a sua primeira relíquia.", "Entregue o Núcleo Lendário.", 39,
      0.20, requires="q_m38", items=["@classe:legend_espada_sino_partido|legend_cajado_ultimas_vozes|legend_talisma_ultimas_vozes|legend_arco_sino_partido"], cat="historia")
    q("q_m40", "O Passo dos Mortos", "guia_aurelio", "interact", ["vespera_marco"], 1,
      "Ative o marco do passo, ao norte. A sentinela liberta abre o caminho para a Geada.", "Ative o Marco do Passo.", 40,
      flags=["passo:geada"], cat="historia")
    q("q_s07", "O Banquete de Ninguém", "carteiro_espectral", "collect", ["food_ensopado_vigilia"], 2,
      "Dois ensopados para os moradores que esqueceram o gosto.", "Leve 2 Ensopados.", 36, 0.05, items=["semente_flor*2"], cat="opcional")
    q("q_s08", "O Cavaleiro que Espera", "guia_aurelio", "kill", [M(r, "cavaleiro_sem_rosto")], 1,
      "Um cavaleiro sem rosto espera um duelo no covil do centro.", "Vença o Cavaleiro Sem Rosto.", 42, 0.08,
      items=["@classe:weapon_guerreiro_machado_vespera|staff_mago_cajado_vespera|talisma_bruxa_vespera|arco_arqueiro_vespera"], cat="opcional")

    # ---------------- R06 Geada ----------------
    r = "geada"
    q("q_m41", "Gente Perdida na Neve", "batedora_runa", "interact", ["geada_sobrevivente"], 1,
      "Um grupo se perdeu a oeste. Encontre o sobrevivente e mostre o caminho.", "Encontre o sobrevivente.", 42)
    q("q_m42", "Minério Sob o Gelo", "ferreiro_caravana", "mine", ["mat_ferro_geada"], 4,
      "Ferro de geada nos veios guardados por draugr.", "Minere Ferro de Geada.", 43, flags=["receita:liga_geada"], cat="oficio")
    q("q_m43", "Comida para a Travessia", "cozinheira_hild", "craft", ["rec_travessia"], 1,
      "Carne, raiz e erva de inverno. Esquenta até o osso.", "Cozinhe a Refeição da Travessia.", 43, accept_flags=["receita:travessia"], cat="oficio")
    q("q_m44", "O Rei do Passo", "batedora_runa", "boss", [M(r, "rei_draugr")], 1,
      "O Rei Draugr fecha o passo para o Mar de Escória. Com ele vencido, o selo é seu.", "Vença o Rei Draugr.", 48, 0.15, cat="historia")
    q("q_m45", "Membrana e Metal", "artesa_ilse", "craft", ["rec_estrutura_asas"], 1,
      "Três ligas de geada, três membranas e retalhos: a estrutura das asas.", "Faça a Estrutura das Asas.", 45, accept_flags=["receita:asas"], cat="historia")
    q("q_m46", "O Jötunn Acorrentado", "artesa_ilse", "boss", [M(r, "jotunn")], 1,
      "O núcleo de sustentação está no Salão da Geada, preso ao Jötunn.", "Vença o Jötunn (Salão da Geada).", 47, 0.15, requires="q_m45", cat="historia")
    # 12/11 (auditoria): a asa de ferro é do 25 (Máquina do Caos); no 48 a Ilse monta as ASAS DE NEVE (item do 45)
    q("q_m47", "Asas de Neve", "artesa_ilse", "collect", ["mat_nucleo_sustentacao"], 1,
      "Com o núcleo do Jötunn, as asas de gelo ficam inteiras — mais fortes que as de ferro. Tecla G: voar.", "Entregue o Núcleo de Sustentação.", 48,
      0.20, requires="q_m46", flags=["wings:neve"], cat="historia")
    q("q_m48", "O Segundo Juramento", "mentor_harald", "craft", ["mat_foco_juramento"], 1,
      "Forje o Foco do Segundo Juramento. Depois escolha seu caminho — só um.", "Forje o Foco do Segundo Juramento.", 50,
      0.10, accept_flags=["receita:foco"], flags=["juramento:50"], cat="historia")
    for spec, name, cls, other, prova in [
        ("vanguarda", "Caminho da Vanguarda", "guerreiro", "quebra_selos", "proteção e postura (armadura +25%, mais vida)"),
        ("quebra_selos", "Caminho do Quebra-Selos", "guerreiro", "vanguarda", "impacto e pressão (+12% de dano em tudo)"),
        ("piromante", "Caminho do Piromante", "mago", "tecelao", "explosões (+30% de dano da Explosão)"),
        ("tecelao", "Caminho do Tecelão Espectral", "mago", "piromante", "projéteis (+20% de dano de projéteis)"),
        ("guardia", "Caminho da Guardiã", "bruxa", "maldicao", "proteção (cura e barreira +30%)"),
        ("maldicao", "Caminho da Maldição", "bruxa", "guardia", "maldição (+12% de dano; a Aura enfraquece mais)"),
        ("atirador", "Caminho do Atirador", "arqueiro", "sombra", "precisão (+20% de dano de flechas)"),
        ("sombra", "Caminho da Sombra", "arqueiro", "atirador", "emboscada (emboscada +50%, Passos Fantasma +2 s)")]:
        q(f"q_spec_{spec}", name, "mentor_harald", "kill", [M(r, "draugr_campeao")], 1,
          f"Prova pessoal: vença o Draugr Campeão. Ao entregar, você vira {name.split(' do ')[-1].split(' da ')[-1]} — {prova}. A escolha não volta atrás.",
          "Vença o Draugr Campeão (covil central).", 50, 0.10, requires_flag="juramento:50", forbid_flag=f"spec:{other}",
          flags=[f"spec:{spec}"], cls=cls, cat="historia")
    q("q_s09", "Plumas na Neve", "artesa_ilse", "collect", ["mat_pluma_neve"], 3,
      "Plumas brancas para forrar as asas de neve contra o vento do passo. A Ilse paga em tônicos.", "Plumas (lobos antigos e harpias).", 48, 0.05,
      items=["consumable_c05_tonico_forte*5"], requires="q_m47", cat="opcional")

    # ---------------- R07 Escória ----------------
    r = "escoria"
    q("q_m49", "Onde a Pedra Ferve", "mestre_forja", "visit", ["passagem_calor"], 1,
      "Investigue a passagem de calor a leste do refúgio.", "Vá à passagem de calor (leste).", 48, qmap="mar_de_escoria", x=58, y=10, radius=6)
    q("q_m50", "Ferro que Não Cede", "mestre_forja", "mine", ["mat_basalto"], 4,
      "Basalto férreo, quatro. Com geada, vira liga.", "Minere Basalto Férreo.", 49, cat="oficio")
    q("q_m51", "Caldo de Cinzas", "cozinheira_brasa", "craft", ["rec_caldo"], 1,
      "Cogumelo de cinza e raiz: caldo que protege do calor.", "Cozinhe o Caldo de Cinzas.", 49, accept_flags=["receita:caldo"], cat="oficio")
    q("q_m52", "Um Ninho nas Brasas", "tratador_fenix", "interact", ["escoria_ninho"], 1,
      "O ninho fica numa ilha a oeste. Veja se o ovo ainda pulsa.", "Encontre o ninho de fênix.", 50, cat="companheiro")
    q("q_m53", "A Brasa que Renasce", "tratador_fenix", "collect", ["mat_alimento_brasa"], 1,
      "Com a brasa de vínculo (Fornalha) e cogumelo, prepare o alimento no caldeirão. A fênix escolhe você.",
      "Prepare o Alimento de Brasa.", 52, 0.10, requires="q_m52", accept_flags=["receita:brasa"], flags=["pet:fenix"], cat="companheiro")
    q("q_m54", "A Liga do Juramento", "mestre_forja", "craft", ["rec_liga_lendaria"], 1,
      "Basalto, geada e um fragmento da Fornalha: a liga que reacende relíquias.", "Forje a Liga Lendária.", 52,
      accept_flags=["receita:liga_lendaria"], cat="historia")
    q("q_m55", "O Colosso de Escória", "mestre_forja", "boss", [M(r, "colosso_escoria")], 1,
      "O Colosso guarda a ponte de corrente para a Acrópole.", "Vença o Colosso de Escória.", 54, 0.15, cat="historia")
    q("q_m56", "Caravana Entre Cinzas", "caravaneira_lu", "talk", ["sentinela_acropole"], 1,
      "Com a ponte aberta, leve a notícia à Sentinela da Acrópole. A caravana segue você.", "Fale com a Sentinela (Acrópole).", 55,
      requires="q_m55", cat="historia")
    q("q_s10", "A Dívida do Ferreiro", "mestre_forja", "kill", [M(r, "ferreiro_corrompido")], 1,
      "O Ferreiro Corrompido (Fornalha) levou meu molde.", "Vença o Ferreiro Corrompido.", 53, 0.08,
      items=["@classe:weapon_guerreiro_machado_escoria|staff_mago_cajado_escoria|talisma_bruxa_escoria|arco_arqueiro_escoria"], cat="opcional")

    # ---------------- R08 Acrópole + volta ao Porto ----------------
    r = "acropole"
    q("q_m57", "Juramentos em Ruínas", "sentinela_acropole", "kill", [M(r, "guardiao_bronze")], 6,
      "Os guardiões de bronze ainda cumprem ordens velhas. Seis.", "Guardiões de Bronze.", 55)
    q("q_m58", "Bronze e Memória", "artesa_acropole", "craft", ["rec_chave_bronze"], 1,
      "Bronze antigo e uma memória: a chave do Labirinto.", "Faça a Chave de Bronze.", 56, accept_flags=["receita:chave"],
      flags=["chave:labirinto"], cat="historia")
    q("q_m59", "Três Provas da Acrópole", "sentinela_acropole", "kill", [M(r, "guardiao_ritual")], 1,
      "O Guardião do Ritual protege o estandarte. Vença-o.", "Vença o Guardião do Ritual.", 57, 0.10,
      items=["@classe:weapon_guerreiro_espada_acropole|staff_mago_cajado_acropole|talisma_bruxa_acropole|arco_arqueiro_acropole"])
    q("q_m60", "O Minotauro do Juramento", "sentinela_acropole", "kill", [M(r, "minotauro_juramento")], 1,
      "No Labirinto, o Minotauro guarda o atalho.", "Vença o Minotauro do Juramento.", 58, 0.12, requires="q_m58", cat="historia")
    q("q_m61", "A Quimera de Bronze", "sentinela_acropole", "boss", [M(r, "quimera_bronze")], 1,
      "O fim do Labirinto. A Quimera de Bronze.", "Vença a Quimera de Bronze.", 59, 0.20, requires="q_m60", cat="historia")
    q("q_m62", "Relíquia Reacesa", "ferreiro_bras", "collect", ["mat_nucleo_superior"], 1,
      "Traga o Núcleo Superior: reacendo a sua relíquia (a nova substitui a antiga).", "Leve o Núcleo Superior ao Brás (Porto).", 59,
      0.15, requires="q_m61", items=["@classe:legend_espada_sino_reacesa|legend_cajado_vozes_reaceso|legend_talisma_reaceso|legend_arco_sino_reaceso"], cat="historia")
    q("q_m63", "Um Lar Depois da Guerra", "agricultora_rosa", "collect", ["mat_liga_lendaria"], 1,
      "Uma liga lendária vira ferragem para o baú grande da sua casa.", "Leve uma Liga Lendária à Rosa.", 52,
      0.05, requires_flag="casa:propria", flags=["casa:bau48"], cat="casa")
    q("q_m64", "O Horizonte Ainda Chama", "capita_ondina", "visit", ["praca"], 1,
      "Volte à praça do Porto e registre seus feitos. O mundo além da Acrópole ainda não foi mapeado.",
      "Volte à praça do Porto.", 60, 0.10, requires="q_m61", qmap="porto_das_cinzas", x=0, y=2, radius=5, cat="historia")

    # ---------------- contratos repetíveis (quadro de cada refúgio) ----------------
    for reg in REGIONS:
        rid, (lo, hi) = reg["id"], reg["band"]
        board = f"quadro_{rid if rid not in ('canions',) else 'canion'}"
        s0, s1 = reg["species"][0], reg["species"][-1]
        q(f"q_ct_{rid}_caca", f"Contrato: Patrulha ({reg['name'].split(',')[0]})", board, "kill", [mid(rid, s0["key"]), mid(rid, s1["key"])], 8,
          f"Elimine 8 entre {s0['name']} e {s1['name']}.", "Contrato de caça.", (lo + hi) // 2, 0.025, coins=lo * 12, repeatable=True, cat="contrato")
        q(f"q_ct_{rid}_veio", f"Contrato: Minério ({reg['name'].split(',')[0]})", board, "mine", [reg["ore"][0]], 4,
          f"Minere 4 {dict((m[0], m[1]) for m in MATERIALS).get(reg['ore'][0], 'minério')}.", "Contrato de mineração.", lo, 0.02,
          coins=lo * 10, repeatable=True, cat="contrato")
        q(f"q_ct_{rid}_elite", f"Contrato: {reg['elite']['name']}", board, "kill", [mid(rid, reg["elite"]["key"])], 1,
          f"Vença o {reg['elite']['name']} do covil central.", "Contrato de elite.", reg["elite"]["lvl"] - 1, 0.04, coins=lo * 25, repeatable=True, cat="contrato")
        q(f"q_ct_{rid}_cozinha", f"Contrato: Suprimentos ({reg['name'].split(',')[0]})", board, "craft",
          ["consumable_c01_tonico_fraco", "consumable_c03_frasco_mana", "consumable_c06_ataduras", "food_ensopado_vigilia"], 2,
          "Prepare 2 suprimentos (tônico, frasco de mana, ataduras ou ensopado).", "Contrato de preparo.", lo, 0.02,
          coins=lo * 8, repeatable=True, cat="contrato")


# =====================================================================================================================
# Peças acrescentadas aos mapas feitos à mão (idempotente: tudo marcado com "gerado")
# =====================================================================================================================
PROP_R = {"pinheiros": 1.1, "tenda": 1.8, "caixotes": 0.8, "barril": 0.45, "coluna_quebrada": 0.7, "estacas": 0.7, "lapide": 0.45,
          "banca": 1.1, "forja": 1.3, "sino": 0.9, "bau_armazem": 0.7}   # mesmos raios de MapGeometry.PropRadius


def free_spot(layout, x, y, r=2.5):
    for p in layout.get("props", []):
        pr = PROP_R.get(p["kind"], 0)
        if pr and (p["x"] - x) ** 2 + (p["y"] - y) ** 2 < (pr + r) ** 2: return False
    for o in layout["obstacles"]:
        if o["shape"] == "circle":
            if (o["x"] - x) ** 2 + (o["y"] - y) ** 2 < (o["r"] + r) ** 2: return False
        elif abs(o["x"] - x) < o["w"] / 2 + r and abs(o["y"] - y) < o["h"] / 2 + r:
            return False
    return True


def find_free(layout, candidates, r=2.5):
    for x, y in candidates:
        if free_spot(layout, x, y, r): return x, y
    raise SystemExit(f"sem lugar livre em {layout['id']}")


def strip_generated(layout):
    for key in ("objects", "props", "portals", "npcs", "obstacles"):
        layout[key] = [o for o in layout.get(key, []) if not o.get(GEN) and not o.get("deco")]


def patch_porto():
    p = load("map_porto.json")
    strip_generated(p)
    tag = {GEN: True}
    p["objects"] += [
        {"id": "porto_forja", "kind": "forja", "x": -13, "y": -7.2, "label": "Forja do Brás", **tag},
        {"id": "porto_bancada", "kind": "bancada", "x": -19, "y": -3, "label": "Bancada", **tag},
        {"id": "porto_caldeirao", "kind": "caldeirao", "x": 16, "y": -5, "label": "Caldeirão da Ilma", **tag},
        {"id": "porto_panela", "kind": "panela", "x": 6, "y": 13, "label": "Panela", **tag},
        {"id": "porto_armazem", "kind": "armazem", "x": -6, "y": 13, "label": "Armazém comunitário", **tag},
    ] + [{"id": f"porto_canteiro{i}", "kind": "canteiro", "x": x, "y": y, "label": "Horta comunitária", **tag}
         for i, (x, y) in enumerate([(-18, 7), (-14, 7), (-18, 3), (-14, 3)])]
    p["props"] += [{"kind": "bau_armazem", "x": -6, "y": 14.2, "yaw": 0, **tag},
                   # cara de fazenda em volta da horta (espantalho, feno, regadores — não bloqueiam a passagem)
                   {"kind": "espantalho", "x": -16, "y": 9.6, "yaw": 0, **tag},
                   {"kind": "feno", "x": -21, "y": 9.2, "yaw": 20, **tag},
                   {"kind": "feno", "x": -21.4, "y": 1.2, "yaw": -15, **tag},
                   {"kind": "regadores", "x": -11.8, "y": 8.4, "yaw": 0, **tag}]
    p["npcs"] += [{"id": "agricultora_rosa", "name": "Rosa, a Agricultora", "role": "missoes", "x": -10, "y": 5, "facing": 180,
                   "greeting": "A horta é de todos, mas cada canteiro tem dono. Pegue sementes e ponha a mão na terra.",
                   "look": "aldeao", "shop": "loja_rosa", **tag},
                  # Bobo da praça: desafios diários (Content/quests_diarias.json)
                  {"id": "bobo_tiao", "name": "Tião, o Bobo da Praça", "role": "missoes", "x": 6, "y": 4.5, "facing": 230,
                   "greeting": "Hahá! Um herói! Aposto que não aguenta meus desafios do dia. Volte amanhã que eu invento outros!",
                   "look": "bobo", **tag}]
    p["portals"] += [{"id": "porto_casa", "x": 20, "y": 21, "radius": 1.6, "to_map": "casa_jogador", "to_x": 0, "to_y": 6,
                      "label": "Sua casa", "requires_flag": "casa:propria", **tag}]
    save("map_porto.json", p)


def patch_costa():
    c = load("map_costa.json")
    strip_generated(c)
    tag = {GEN: True}
    k = c.get("escala", 1)   # a Costa foi ampliada (Tools/expandir_costa.py): mesmas posições relativas
    # ervas espalhadas: cada moita só volta 1 hora depois (por jogador) — é preciso procurar
    herb_spots = [(9, 52), (-9, 52), (15, 44), (-15, 44), (-30, 30), (30, 30), (-44, 50), (44, 50), (0, 26), (-56, 18), (52, 10), (-20, 60)]
    herbs = [find_free(c, [(x * k, y * k), (x * k + 3, y * k), (x * k, y * k + 3), (x * k - 3, y * k)], 1.5) for x, y in herb_spots]
    ores = [find_free(c, [(x * k, y * k), (x * k + 4, y * k), (x * k, y * k + 4), (x * k - 4, y * k)], 2.0) for x, y in [(-28, 40), (28, 40), (-46, 12), (46, -4)]]
    c["objects"] += [{"id": f"costa_erva{i}", "kind": "veio", "x": x, "y": y, "label": "Moita de erva", "yields": "mat_erva_tonico",
                      "yield_min": 1, "yield_max": 1, "respawn_seconds": 3600, **tag} for i, (x, y) in enumerate(herbs)]
    c["objects"] += [{"id": f"costa_veio{i}", "kind": "veio", "x": x, "y": y, "label": "Veio de ferro raso", "yields": "mat_ferro_raso",
                      "yield_min": 1, "yield_max": 2, "respawn_seconds": 90, "requires_flag": "tool:picareta", **tag} for i, (x, y) in enumerate(ores)]
    save("map_costa.json", c)


def patch_vale():
    v = load("map_vale.json")
    strip_generated(v)
    v["xp_multiplier"] = 1.8   # faixa 11–17 (14/11: 1,5→1,8, o Vale era pulado — auditoria do trabalhador): ritmo do plano (simulação, 08/11)
    # o canto sudeste (onde fica a passagem para a Mata) era fechado por muros: abre uma brecha de 4 m no muro
    # das Ruínas (y = -19), longe do salão do chefe. Idempotente: só divide o muro inteiro original.
    for o in list(v["obstacles"]):
        if o["kind"] == "muralha" and o["shape"] == "box" and o["x"] == 21.5 and o["y"] == -19 and o["w"] == 37:
            v["obstacles"].remove(o)
            v["obstacles"] += [dict(o, x=13.0, w=20.0), dict(o, x=35.0, w=10.0)]   # 3..23 e 30..40: brecha 23..30
    x, y = find_free(v, [(34, -34), (33, -33), (36, -30), (36, 6)], 3)
    v["portals"] += [{"id": "vale_mata", "x": x, "y": y, "radius": 2.2, "to_map": "mata_dos_enforcados", "to_x": 0, "to_y": 64,
                      "label": "Mata dos Enforcados", "min_level": 17, GEN: True}]
    save("map_vale.json", v)
    # chegada no Vale vindo da Mata: um pouco para dentro do mapa
    return (x - 3 if x > 0 else x + 3, y + 3 if y < 0 else y - 3)


def casa_map():
    walls = [box("muro", 0, -10, 20, 1, 4), box("muro", 0, 10, 20, 1, 4), box("muro", -10, 0, 1, 20, 4), box("muro", 10, 0, 1, 20, 4)]
    return {
        "id": "casa_jogador", "display_name": "Sua Casa", "half_size": 12, "spawn_x": 0, "spawn_y": 6, "safe_radius": 30,
        "xp_multiplier": 1, "kind": "cidade", "region": "porto", "theme": "cidade", "instanced": True, "instance_scope": "personal",
        "no_mount": True, "exit_map": "porto_das_cinzas", "exit_x": 20, "exit_y": 24.5,
        "zones": [{"id": "casa", "name": "Sua Casa", "kind": "seguro", "x": 0, "y": 0, "w": 20, "h": 20}],
        "obstacles": walls, "gates": [], "chests": [], "spawns": [],
        "props": [{"kind": "barril", "x": -7, "y": -7, "yaw": 0}, {"kind": "poste", "x": 7, "y": -8, "yaw": 0}, {"kind": "estandarte", "x": 0, "y": -9, "yaw": 0}],
        "portals": [{"id": "casa_saida", "x": 0, "y": 9, "radius": 1.4, "to_map": "porto_das_cinzas", "to_x": 20, "to_y": 24.5, "label": "Porto"}],
        "waypoints": [], "npcs": [],
        "objects": [{"id": "casa_bau", "kind": "bau_casa", "x": -6, "y": -6, "label": "Baú da casa"},
                    {"id": "casa_bancada", "kind": "bancada", "x": 6, "y": -6, "label": "Bancada"},
                    {"id": "casa_caldeirao", "kind": "caldeirao", "x": 6, "y": 0, "label": "Caldeirão"},
                    {"id": "casa_canteiro0", "kind": "canteiro", "x": -6, "y": 1, "label": "Canteiro da casa"},
                    {"id": "casa_canteiro1", "kind": "canteiro", "x": -3, "y": 1, "label": "Canteiro da casa"},
                    {"id": "casa_canteiro2", "kind": "canteiro", "x": -6, "y": 4, "label": "Canteiro extra", "requires_flag": "casa:canteiros4"},
                    {"id": "casa_canteiro3", "kind": "canteiro", "x": -3, "y": 4, "label": "Canteiro extra", "requires_flag": "casa:canteiros4"}],
        "checkpoints": [],
    }


def special_objects(maps):
    """Objetos de missão (interagir) nos mapas gerados."""
    add = {
        "mata_dos_enforcados": [{"id": "mata_espada", "kind": "reliquia", "x": -58, "y": 24, "label": "Espada cravada", "quest": "q_s02"}],
        "brejo_da_viuva": [{"id": "brejo_draco", "kind": "reliquia", "x": 24, "y": 30, "label": "Draco ferido", "quest": "q_m31a"}],
        "vespera": [{"id": "vespera_marco", "kind": "reliquia", "x": 8, "y": -56, "label": "Marco do Passo", "quest": "q_m40"}],
        "coroa_da_geada": [{"id": "geada_sobrevivente", "kind": "reliquia", "x": -60, "y": 30, "label": "Sobrevivente", "quest": "q_m41"}],
        "mar_de_escoria": [{"id": "escoria_ninho", "kind": "reliquia", "x": -58, "y": 6, "label": "Ninho de fênix", "quest": "q_m52"}],
    }
    for m in maps:
        for o in add.get(m["id"], []):
            m["objects"].append(o)
            m["obstacles"] = [ob for ob in m["obstacles"] if not (ob["shape"] == "circle" and (ob["x"] - o["x"]) ** 2 + (ob["y"] - o["y"]) ** 2 < 16)]
    # Mata: ravina com ponte (atalho pessoal da missão Rebites que Faltam)
    mata = next(m for m in maps if m["id"] == "mata_dos_enforcados")
    mata["obstacles"] += [box("ravina", 25, 22, 22, 2.4, 1.2), box("ravina", 63, 22, 24, 2.4, 1.2)]
    mata["gates"].append({"id": "ponte_mata", "flag": "ponte:mata", "x": 45, "y": 22, "w": 18, "h": 2.4, "lever_x": 45, "lever_y": 26,
                          "kind": "ponte", "lever": False})
    mata["obstacles"] = [o for o in mata["obstacles"] if not (o["shape"] == "circle" and 12 < o["x"] < 76 and abs(o["y"] - 22) < 3.5)]


# =====================================================================================================================
def main():
    build_quests()
    # ---- itens ----
    items = []
    for mid_, name, price, src, summary in MATERIALS:
        items.append({"id": mid_, "display_name": name, "category": "material", "subtype": "material", "min_level": 1, "allowed_classes": [],
                      "handedness": 0, "base_stats": {}, "affix_pool": "none", "temperable": False, "trade_rule": "free", "stack_limit": 50,
                      "icon_asset_id": "icon_" + mid_, "model_asset_id": "mat_vein" if "ferro" in mid_ or mid_ in ("mat_basalto", "mat_bronze") else "mat_ash",
                      "summary": summary, "price": price * 4, "source": src})
    for seed, name, crop, mins, a, b, price in SEEDS:
        items.append({"id": seed, "display_name": f"Semente de {name}", "category": "material", "subtype": "seed", "min_level": 1,
                      "allowed_classes": [], "handedness": 0, "base_stats": {}, "affix_pool": "none", "temperable": False, "trade_rule": "free",
                      "stack_limit": 50, "icon_asset_id": "icon_" + seed, "model_asset_id": "mat_ash",
                      "summary": f"Plante num canteiro (F), regue e colha em ~{mins} min.", "price": price, "source": "Rosa (Porto), mascates, drops."})
    for it in tier_items() + LEGENDARIES:
        it = dict(it)
        it.setdefault("affix_pool", "physical_weapon" if it["category"] == "weapon" else "armor")
        it.setdefault("temperable", True)
        it.setdefault("trade_rule", "free")
        it.setdefault("stack_limit", 1)
        it.setdefault("icon_asset_id", "icon_" + it["id"])
        if it["id"].startswith("legend_"):
            it["quality"] = "lendario"
            it.setdefault("price", 20000)
            it["summary"] = it.get("effect", "")
        items.append(it)
    save("items_midgame.json", items)
    save("consumables_midgame.json", CONSUMABLES)
    save("skills_midgame.json", SKILLS)
    save("behaviors_midgame.json", BEHAVIORS)
    save("recipes.json", [{"id": i, "name": n, "station": st, "inputs": [{"item": a, "qty": b} for a, b in ins], "output": out,
                           "output_qty": oq, **({"requires_flag": rf} if rf else {}), "min_level": lvl}
                          for i, n, st, ins, out, oq, rf, lvl in RECIPES])
    save("crops.json", [{"seed": s, "name": n, "crop": c, "grow_minutes": m, "min_qty": a, "max_qty": b} for s, n, c, m, a, b, _ in SEEDS])

    # ---- monstros, mapas ----
    maps = []
    vale_back = patch_vale()
    prev_map, prev_point = "vale_das_cinzas", vale_back
    for i, reg in enumerate(REGIONS):
        rid = reg["id"]
        for s in reg["species"]: add_monster(rid, s, "comum", reg["tier"])
        add_monster(rid, reg["elite"], "elite", reg["tier"])
        add_monster(rid, reg["rare"], "raro", reg["tier"])
        if reg["gate"]:
            leg = {"geada": "legend_manto_segunda_sombra", "escoria": "legend_machado_colosso"}.get(rid)
            add_monster(rid, reg["gate"], "passagem", reg["tier"], [{"chance": 0.04, "item": leg, "quality": "lendario"}] if leg else [])
        d = reg["dungeon"]
        for s in d["mobs"]: add_monster(rid, s, "comum", reg["tier"])
        add_monster(rid, d["mini"], "mini", reg["tier"])
        boss_extra = {
            "vespera": [{"chance": 0.04, "pick": ["legend_escudo_vigilia_morta", "legend_manto_segunda_sombra"], "match_class": True, "quality": "lendario"}],
            "escoria": [{"chance": 0.04, "pick": ["legend_machado_colosso", "legend_foco_brasa_viva"], "match_class": True, "quality": "lendario"}],
            "acropole": [{"chance": 0.04, "item": "legend_elmo_juramento", "quality": "lendario"}],
            "geada": [{"chance": 0.03, "item": "legend_manto_segunda_sombra", "quality": "lendario"}],
        }.get(rid, [])
        add_monster(rid, d["boss"], "chefe", reg["tier"], boss_extra)
        nxt = REGIONS[i + 1]["map"] if i + 1 < len(REGIONS) else None
        fm = field_map(reg, prev_map, prev_point, nxt)
        maps.append(fm)
        maps.append(dungeon_map(reg, reg["map"], (-58, -46)))
        prev_map, prev_point = reg["map"], (0, -66)
    # recompensas especiais de primeira vitória (núcleos e brasa)
    firsts = {"mercado_mortos": ("mat_nucleo_lendario", 1), "salao_geada": ("mat_nucleo_sustentacao", 1),
              "fornalha": ("mat_brasa_vinculo", 1), "labirinto_juramento": ("mat_nucleo_superior", 1), "cripta_do_sino": ("mat_osso_antigo", 4)}
    for t in LOOT:
        did = t["id"].replace("primeira_", "")
        if t["id"].startswith("primeira_") and did in firsts:
            it, n = firsts[did]
            t["entries"].append({"chance": 1.0, "item": it, "min_qty": n, "max_qty": n})
    # o Labirinto exige a chave
    for m in maps:
        for p in m["portals"]:
            if p["to_map"] == "labirinto_juramento": p["requires_flag"] = "chave:labirinto"
    special_objects(maps)
    # cera para a missão opcional da colmeia
    for t in LOOT:
        if t["id"] == "loot_mata_aranha_toca": t["entries"].append({"chance": 0.3, "item": "mat_cera"})
    maps.append(casa_map())
    for m in maps:
        save(f"map_{m['id']}.json", m)
    save("monsters_midgame.json", MONSTERS)
    save("loot_midgame.json", LOOT)
    save("quests_midgame.json", QUESTS)
    save("shops_midgame.json", [
        {"id": "loja_rosa", "name": "Sementes da Rosa", "items": [s[0] for s in SEEDS] + ["mat_frasco", "mat_regador"]},
        {"id": "loja_vespera", "name": "Banca do Mercador Espectral", "items": ["consumable_c05_tonico_forte", "consumable_c04_frasco_mana_grande",
                                                                                 "mat_frasco", "mat_reagente", "semente_cogumelo", "semente_flor"]},
    ])
    patch_porto()
    patch_costa()

    # ---- world.json ----
    w = load("world.json")
    files = [f for f in w["map_files"] if f in ("map_porto.json", "map_costa.json", "map_vale.json", "map_galeria.json")]
    files += [f"map_{m['id']}.json" for m in maps]
    w["map_files"] = files
    keep = {r["id"]: r for r in w["regions"] if r["id"] in ("porto", "costa", "vale", "pantano", "estrada_leste")}
    keep["porto"].update({"x": 0, "y": 75})
    keep["porto"]["maps"] = ["porto_das_cinzas", "casa_jogador"]
    keep["costa"].update({"x": 0, "y": 45})
    keep["vale"].update({"x": 40, "y": 25})
    if "pantano" in keep: keep["pantano"].update({"x": -50, "y": 40})
    if "estrada_leste" in keep: keep["estrada_leste"].update({"x": 70, "y": 60})
    regions = [keep["porto"], keep["costa"], keep["vale"]]
    for reg in REGIONS:
        regions.append({"id": reg["id"], "name": reg["name"].split(",")[0], "kind": "regiao", "x": reg["world"][0], "y": reg["world"][1],
                        "levels": f"{reg['band'][0]}–{reg['band'][1]}", "maps": [reg["map"], reg["dungeon"]["id"]], "description": reg["desc"]})
    # "Pântano Cinzento" e "Estrada do Leste" ("em breve") saíram do mapa do mundo (pendência A: lugar vazio confunde)
    w["regions"] = regions
    chain = ["vale"] + [r["id"] for r in REGIONS]
    links = [{"from": "porto", "to": "costa"}, {"from": "costa", "to": "vale"}]
    links += [{"from": a, "to": b} for a, b in zip(chain, chain[1:])]
    w["links"] = links
    save("world.json", w)
    print(f"{len(maps)} mapas, {len(MONSTERS)} monstros, {len(items)} itens, {len(QUESTS)} missões, {len(RECIPES)} receitas, {len(LOOT)} tabelas")


def povoar():
    """Árvores, rochas, cercas e estradas nos mapas abertos (Tools/povoar_mapas.py; idempotente)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("povoar_mapas", os.path.join(os.path.dirname(os.path.abspath(__file__)), "povoar_mapas.py"))
    mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod)
    mod.main()


if __name__ == "__main__":
    main()
    povoar()

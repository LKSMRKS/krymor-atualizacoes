#!/usr/bin/env python3
"""
Bestiário (pedido do usuário, 09/11): criaturas novas espalhadas pelos mapas abertos, cada uma no lugar e na faixa de
nível que combina com ela. Gera Content/monsters_bestiario.json, loot_bestiario.json e behaviors_bestiario.json.

Os pontos de nascimento NÃO são escritos aqui: Tools/povoar_mapas.py (fill_spawns) usa a tabela BESTIARIO para trocar
parte dos bichos de preenchimento dos buracos do mapa por estas espécies (idempotente, refeito a cada execução).
Rode: python3 Tools/gerar_bestiario.py && python3 Tools/povoar_mapas.py

IDs gravados no banco: nunca renomeie o "id" de uma criatura já publicada (só o nome).
"""
import json, os, sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Content")
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))


def save(name, data):
    with open(os.path.join(ROOT, name), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


# mapa → tier de equipamento (o mesmo das regiões geradas) e materiais típicos
TIER = {"costa_do_cal": None, "vale_das_cinzas": None, "mata_dos_enforcados": "mata", "canions_suplicantes": "canion",
        "brejo_da_viuva": "brejo", "vespera": "vespera", "coroa_da_geada": "geada", "mar_de_escoria": "escoria",
        "acropole_das_cinzas": "acropole"}

# (chave, nome, mapa, nível, comportamento, visual, vida, dano, arquétipo, raio, materiais, peso no mapa)
# peso: quantas vezes entra no rodízio de preenchimento daquele mapa (1 = raro de ver, 2 = comum)
def b(key, name, mapa, lvl, beh, vis, hp=1.0, dmg=2.0, arch="combatente", radius=0.6, mats=(), peso=2):
    return dict(key=key, name=name, map=mapa, lvl=lvl, beh=beh, vis=vis, hp=hp, dmg=dmg, arch=arch, radius=radius, mats=list(mats), peso=peso)


BESTIARIO = [
    # Costa do Cal (1–14)
    b("rato_do_cais", "Rato do Cais", "costa_do_cal", 2, "swarm_bite", "rat", 0.5, 1.0, "agil", 0.4, [("mat_retalho", 0.3)]),
    b("meleca", "Meleca", "costa_do_cal", 5, "slime_hop", "slime", 1.1, 1.2, "tanque", 0.55, [("mat_casca_cal", 0.3)]),
    b("rato_gigante", "Ratazana", "costa_do_cal", 9, "beast_lunge", "rat_big", 0.9, 1.5, "agil", 0.55, [("mat_retalho", 0.4)], 1),
    # Vale das Cinzas (11–16)
    b("planta_carnivora", "Planta Carnívora", "vale_das_cinzas", 12, "plant_snap", "plant", 1.2, 1.8, "tanque", 0.7, [("mat_fragmento_veio", 0.2)]),
    b("saci", "Saci das Cinzas", "vale_das_cinzas", 13, "goblin_skirmisher", "saci", 0.8, 1.7, "agil", 0.4, [("mat_retalho", 0.3), ("mat_gorro_saci", 0.02)]),
    b("anao_minerador", "Anão Minerador", "vale_das_cinzas", 14, "dwarf_bomber", "dwarf", 1.3, 1.8, "tanque", 0.5, [("mat_fragmento_veio", 0.4)]),
    # Mata dos Enforcados (17–26)
    b("zumbi", "Zumbi Enforcado", "mata_dos_enforcados", 18, "melee_windup_swing", "zombie", 1.1, 2.0, "combatente", 0.5, [("mat_osso_antigo", 0.35)]),
    b("lobisomem", "Lobisomem", "mata_dos_enforcados", 21, "beast_lunge", "werewolf", 1.4, 2.3, "agil", 0.7, [("mat_couro", 0.5), ("mat_sangue_lunar", 0.25)]),
    b("chupa_cabra", "Chupa-cabra", "mata_dos_enforcados", 20, "spider_leap", "chupacabra", 0.9, 2.2, "agil", 0.55, [("mat_couro", 0.4)], 1),
    # Cânions (20–36)
    b("mumia", "Múmia do Nicho", "canions_suplicantes", 26, "melee_windup_swing", "mummy", 1.2, 2.2, "tanque", 0.55, [("mat_retalho", 0.4), ("mat_reagente", 0.15)]),
    b("verme_gigante", "Verme da Fenda", "canions_suplicantes", 24, "swarm_bite", "worm_giant", 1.4, 2.0, "tanque", 0.8, [("mat_quitina", 0.3)]),
    b("visitante", "Visitante do Céu", "canions_suplicantes", 30, "ghost_caster", "alien", 0.9, 2.2, "conjurador", 0.5, [("mat_reagente", 0.35)], 1),
    # Brejo (28–42)
    b("sapo_gigante", "Sapo Gigante", "brejo_da_viuva", 32, "brute_slam", "frog_giant", 1.6, 2.2, "tanque", 1.0, [("mat_ferrao", 0.3)]),
    b("meleca_brejo", "Meleca do Brejo", "brejo_da_viuva", 30, "slime_hop", "slime_bog", 1.3, 2.0, "tanque", 0.7, [("mat_folha_amarga", 0.3)]),
    b("planta_brejo", "Boca-de-Lodo", "brejo_da_viuva", 34, "plant_snap", "plant_bog", 1.4, 2.3, "tanque", 0.8, [("mat_folha_amarga", 0.35)]),
    # Véspera (35–50)
    b("vampiro", "Vampiro", "vespera", 42, "beast_lunge", "vampire", 1.2, 2.4, "agil", 0.5, [("mat_ficha_mercado", 0.35), ("mat_cinza_vampirica", 0.25)]),
    b("a_morte", "A Morte", "vespera", 46, "ghost_caster", "death", 2.2, 2.5, "conjurador", 0.7, [("mat_fragmento_reliquia", 0.5), ("mat_osso_lunar", 0.2)], 1),
    # Coroa da Geada (42–56)
    b("wendigo", "Wendigo", "coroa_da_geada", 47, "beast_lunge", "wendigo", 1.3, 2.4, "agil", 0.6, [("mat_osso_antigo", 0.4), ("mat_pluma_neve", 0.1)]),
    b("ogro_montanha", "Ogro da Montanha", "coroa_da_geada", 50, "brute_slam", "ogre", 2.0, 2.4, "tanque", 1.1, [("mat_couro", 0.5)]),
    b("anao_montanha", "Anão da Montanha", "coroa_da_geada", 46, "dwarf_bomber", "dwarf_mountain", 1.4, 2.3, "tanque", 0.5, [("mat_ferro_geada", 0.3)]),
    # Mar de Escória (48–62)
    b("demonio", "Demônio da Forja", "mar_de_escoria", 53, "melee_windup_swing", "demon", 1.4, 2.5, "combatente", 0.7, [("mat_basalto", 0.35)]),
    b("cao_tres_cabecas", "Cão de Três Cabeças", "mar_de_escoria", 55, "beast_lunge", "cerberus", 1.8, 2.5, "agil", 1.0, [("mat_couro", 0.5)], 1),
    b("dragao", "Dragão de Escória", "mar_de_escoria", 58, "brute_slam", "dragon", 3.0, 2.6, "tanque", 1.3, [("mat_basalto", 1.0), ("mat_membrana", 0.6)], 1),
    # Acrópole (55–66)
    b("anjo", "Anjo do Juramento", "acropole_das_cinzas", 58, "ghost_caster", "angel", 1.3, 2.4, "conjurador", 0.6, [("mat_bronze", 0.4)]),
    b("genio", "Gênio da Lâmpada Partida", "acropole_das_cinzas", 59, "ghost_caster", "genie", 1.5, 2.4, "conjurador", 0.7, [("mat_bronze", 0.4), ("mat_reagente", 0.3)], 1),
]

# criaturas que não combinam com nenhum comportamento existente
BEHAVIORS = [
    # meleca: lenta, pula em cima (golpe em área curta) e cospe gosma que deixa lento
    {"id": "slime_hop", "move_speed": 2.4, "aggro_radius": 8, "leash_radius": 16, "exposed_mult": 1.5,
     "attacks": [{"id": "esmagar_gosma", "shape": "circle", "range": 2.4, "windup_ms": 850, "active_ms": 200, "recovery_ms": 950, "cooldown_ms": 900,
                  "damage_mult": 1.0, "radius": 1.4, "offset": 1.0, "track_fraction": 0.5},
                 {"id": "cuspe_gosma", "shape": "projectile", "range": 8, "min_range": 3, "windup_ms": 900, "active_ms": 100, "recovery_ms": 900,
                  "cooldown_ms": 4800, "damage_mult": 0.7, "radius": 0.5, "projectile_speed": 7.5, "count": 1, "track_fraction": 0.6,
                  "status": "lentidao", "status_ms": 2500}]},
    # planta carnívora: não sai do lugar; bote rápido de perto e cuspe venenoso de longe
    {"id": "plant_snap", "move_speed": 0.0, "aggro_radius": 7, "leash_radius": 9, "exposed_mult": 1.6,
     "attacks": [{"id": "bote_planta", "shape": "cone", "range": 3.2, "windup_ms": 600, "active_ms": 150, "recovery_ms": 900, "cooldown_ms": 700,
                  "damage_mult": 1.2, "arc": 80, "track_fraction": 0.6},
                 {"id": "cuspe_veneno", "shape": "projectile", "range": 9, "min_range": 3.2, "windup_ms": 800, "active_ms": 100, "recovery_ms": 800,
                  "cooldown_ms": 3600, "damage_mult": 0.75, "radius": 0.45, "projectile_speed": 9, "count": 3, "spread_deg": 24, "track_fraction": 0.5}]},
]


def coins_for(lvl, mult=1.0):
    return max(1, int(lvl * 1.4 * mult)), max(2, int(lvl * 2.8 * mult))


def loot_entries(s):
    entries = [{"chance": c, "item": it} for it, c in s["mats"]]
    tier = TIER[s["map"]]
    if tier is None:   # começo do jogo: mesmas peças e poções dos bichos vizinhos
        if s["lvl"] < 8:
            entries += [{"chance": 0.15, "item": "consumable_c01_tonico_fraco"},
                        {"chance": 0.06, "pick": ["weapon_guerreiro_machadinha_p1", "staff_mago_coral_p1", "talisma_bruxa_coral_p1", "arco_arqueiro_coral_p1"], "match_class": True},
                        {"chance": 0.05, "pick": ["armor_gibao_acolchoado", "helmet_gorro_couro", "armor_bruxa_ervas", "armor_arqueiro_ervas", "armor_manto_acolito"], "match_class": True}]
        else:
            entries += [{"chance": 0.15, "item": "consumable_c02_tonico_medio"},
                        {"chance": 0.08, "pick": ["weapon_guerreiro_axe_p2", "weapon_guerreiro_spear_p2", "staff_mago_brasa", "staff_mago_vara_gemea", "armor_cota_de_malha",
                                                  "armor_manto_acolito", "talisma_bruxa_brasa", "arco_arqueiro_brasa", "armor_bruxa_ervas", "armor_arqueiro_ervas"], "match_class": True},
                        {"chance": 0.14, "pick": ["consumable_c03_frasco_mana", "consumable_c01_tonico_fraco"], "match_class": True}]
    else:
        import gerar_regioes as gr
        big = s["peso"] == 1   # as raras de ver dão mais
        entries += [{"chance": 0.12 if big else 0.05, "pick": gr.tier_pick(tier), "match_class": True},
                    {"chance": 0.14, "pick": ["consumable_c03_frasco_mana", gr.potion_for(s["lvl"])], "match_class": True}]
    return entries


def main():
    monsters, loot = [], []
    for s in BESTIARIO:
        big = s["peso"] == 1
        cmin, cmax = coins_for(s["lvl"], 2.5 if big else 1.0)
        monsters.append({"id": f"monster_bst_{s['key']}", "species_name": s["name"], "archetype": s["arch"], "level": s["lvl"], "resistances": {},
                         "behavior_id": s["beh"], "loot_table_id": f"loot_bst_{s['key']}", "visual_asset_id": s["vis"],
                         "health_mult": s["hp"], "damage_mult": s["dmg"], "xp_mult": 2.0 if big else 1.0, "radius": s["radius"],
                         "coins_min": cmin, "coins_max": cmax})
        loot.append({"id": f"loot_bst_{s['key']}", "entries": loot_entries(s)})
    save("monsters_bestiario.json", monsters)
    save("loot_bestiario.json", loot)
    save("behaviors_bestiario.json", BEHAVIORS)
    print(len(monsters), "criaturas do bestiário")


def por_mapa():
    """mapa → [(id do monstro, nível, peso)] — usado pelo povoar_mapas.py."""
    out = {}
    for s in BESTIARIO:
        out.setdefault(s["map"], []).append((f"monster_bst_{s['key']}", s["lvl"], s["peso"]))
    return out


if __name__ == "__main__":
    main()

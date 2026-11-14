#!/usr/bin/env python3
"""
13/11 (pedido do dono, 17:33) — conteúdo novo:
  * Reino "Era Uma Vez" (mundo de fantasia, níveis 24–29, pelo Tião): "Aquela Família" (5 monstros grotescos ORIGINAIS),
    Dona Fungilda e os 5 gnomos de jardim enfeitiçados pelo cogumelo (lutar, juntar esporos, curar com o antídoto, chefe),
    Castelo do Riso com dois bobos: Agostinho "Chora-Agora" (missões normais) e Malaquias (tarefa impossível, três enigmas,
    depois tenta te matar) — matando o Malaquias, o líder dele, o Grão-Bobo, fica furioso (chefe).
  * Macaco-Coqueiro (Mata dos Enforcados): fica no galho, desce quando te vê e joga coco.
  * Dentuço e Dentuço-Pai (Cânions): esfrega a pata no chão, dispara, dá cabeçada e morde várias vezes para os lados.
  * Dama Urtiga (Floresta Oculta): bruxa-planta (visual próprio, não é cópia de personagem nenhum) e os brotos dela.
  * Família Vlakor (Transilvânia): quatro vampiros elite.
  * Tritão Corrompido (Fundo do Triângulo): enlouquecido pela poluição; depois da luta você escolhe salvá-lo ou acabar com ele.
Idempotente: tira o que é dele (ids "fab_", "monster_fab_", marcas "fab": true) e põe de novo.
Rodar DEPOIS de todos os geradores de mapa (e antes do ajustes_logica.py, que não mexe nisto).
"""
import json, os, glob, math, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}


def L(mid): return json.load(open(ARQ[mid], encoding="utf-8"))
def D(mid, m): json.dump(m, open(ARQ[mid], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
def W(nome, dados): json.dump(dados, open(os.path.join(C, nome), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


def dentro(o, x, y, folga):
    if o.get("shape") == "circle": return math.hypot(x - o["x"], y - o["y"]) < o["r"] + folga
    return abs(x - o["x"]) < o["w"] / 2 + folga and abs(y - o["y"]) < o["h"] / 2 + folga


def livre(m, x, y, r=1.6):
    hs = m["half_size"] - 3
    if abs(x) > hs or abs(y) > hs: return False
    if any(dentro(o, x, y, r) for o in m["obstacles"]): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < r + 0.8 for p in m["props"]): return False
    if any(math.hypot(x - o["x"], y - o["y"]) < r + 1 for o in m.get("objects", [])): return False
    if any(math.hypot(x - n["x"], y - n["y"]) < r + 1 for n in m.get("npcs", [])): return False
    if any(abs(x - g["x"]) < g["w"] / 2 + r and abs(y - g["y"]) < g["h"] / 2 + r for g in m.get("gates", [])): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < r + 3 for p in m.get("portals", [])): return False
    return True


def perto(m, x, y, rnd, r=1.6, raio=6.0, tentativas=400):
    """Ponto livre perto de (x, y)."""
    if livre(m, x, y, r): return round(x, 1), round(y, 1)
    for i in range(tentativas):
        a = rnd.uniform(0, math.tau); d = rnd.uniform(0.5, raio * (1 + i / 120))
        nx, ny = x + math.cos(a) * d, y + math.sin(a) * d
        if livre(m, nx, ny, r): return round(nx, 1), round(ny, 1)
    raise SystemExit(f"sem lugar livre perto de {x},{y} em {m['id']}")


def limpar(m):
    for k in ("spawns", "props", "objects", "npcs", "zones", "obstacles"):
        m[k] = [e for e in m.get(k, []) if not e.get("fab") and not str(e.get("id", "")).startswith("fab_")]


# ------------------------------------------------------------------ monstros, comportamentos, itens, saque

def mon(id_, nome, arq, lvl, beh, loot, vis, hp, dmg, xp=1.0, raio=0.5, boss=False, elite=False, moedas=None):
    c0, c1 = moedas or (lvl * 2, lvl * 4)
    d = {"id": id_, "species_name": nome, "archetype": arq, "level": lvl, "resistances": {}, "behavior_id": beh, "loot_table_id": loot,
         "visual_asset_id": vis, "health_mult": hp, "damage_mult": dmg, "xp_mult": xp, "radius": raio, "coins_min": c0, "coins_max": c1}
    if boss: d["boss"] = True
    if elite: d["elite"] = True
    return d


MONSTROS = [
    # --- Aquela Família (grotescos ORIGINAIS: pele cinza-esverdeada, nada de amarelo, nada de cabelo azul alto) ---
    mon("monster_fab_pai_pancudo", "Seu Lourenço Pança-Mole (o pai)", "tanque", 25, "fab_pai", "loot_fab_familia", "fab_pai", 3.0, 2.2, 3.0, 0.8, elite=True),
    mon("monster_fab_mae_cabeleira", "Dona Clotilde Trança-Longa (a mãe)", "combatente", 25, "fab_mae", "loot_fab_familia", "fab_mae", 2.4, 2.1, 2.5, 0.55, elite=True),
    mon("monster_fab_filho_estilingue", "Tonico Estilingue (o filho)", "atirador", 25, "fab_filho", "loot_fab_castelo", "fab_filho", 1.8, 2.0, 2.0, 0.45),
    mon("monster_fab_filha_rabeca", "Lili Desafinada (a filha)", "conjurador", 25, "fab_filha", "loot_fab_castelo", "fab_filha", 1.7, 2.0, 2.0, 0.45),
    mon("monster_fab_bebe_birra", "Bebê Birra (o caçula)", "agil", 25, "fab_bebe", "loot_fab_castelo", "fab_bebe", 1.3, 1.6, 1.8, 0.35),
    # --- jardim ---
    mon("monster_fab_gnomo_enfeiticado", "Gnomo de Jardim Enfeitiçado", "agil", 24, "fab_gnomo", "loot_fab_gnomo", "fab_gnomo", 1.0, 1.9, 1.1, 0.35),
    mon("monster_fab_fungilda", "Dona Fungilda, a Jardineira Má", "conjurador", 27, "fab_fungilda", "loot_fab_chefe", "fab_fungilda", 5.0, 2.2, 11, 0.6, boss=True, moedas=(160, 300)),
    # --- castelo ---
    mon("monster_fab_soldadinho", "Soldadinho de Chumbo Encantado", "combatente", 26, "melee_windup_swing", "loot_fab_castelo", "fab_soldadinho", 1.1, 2.1, 1.0, 0.45),
    mon("monster_fab_malaquias", "Malaquias, o Bobo Ruim", "agil", 27, "fab_malaquias", "loot_fab_castelo_elite", "fab_bobo_mau", 4.2, 2.4, 5.0, 0.5, elite=True),
    mon("monster_fab_grao_bobo", "O Grão-Bobo, Senhor do Riso", "tanque", 29, "fab_grao_bobo", "loot_fab_chefe", "fab_grao_bobo", 5.6, 2.3, 12, 1.0, boss=True, moedas=(200, 360)),
    # --- floresta do reino ---
    mon("monster_fab_lobo_mau", "Lobo Mau de Capuz Rasgado", "agil", 24, "beast_lunge", "loot_fab_gnomo", "wolf", 1.2, 2.1, 1.0, 0.5),
    # --- Mata dos Enforcados ---
    mon("monster_fab_macaco_coqueiro", "Macaco-Coqueiro", "agil", 20, "fab_macaco", "loot_fab_macaco", "fab_macaco", 1.1, 1.9, 1.1, 0.4),
    # --- Cânions ---
    mon("monster_fab_dentuco", "Dentuço (dinossauro-mirim)", "combatente", 24, "fab_dentuco", "loot_fab_dentuco", "fab_dentuco", 1.3, 2.1, 1.3, 0.55),
    mon("monster_fab_dentuco_pai", "Dentuço-Pai, o Espinhudo", "tanque", 27, "fab_dentuco_pai", "loot_fab_dentuco_pai", "fab_dentuco_pai", 4.2, 2.4, 4.5, 1.0, elite=True),
    # --- Floresta Oculta ---
    mon("monster_fab_broto_urtiga", "Broto de Urtiga", "tanque", 21, "plant_snap", "loot_fab_urtiga", "plant", 0.9, 1.9, 0.9, 0.6),
    mon("monster_fab_dama_urtiga", "A Dama Urtiga", "conjurador", 23, "fab_urtiga", "loot_fab_urtiga_chefe", "fab_urtiga", 5.2, 2.2, 11, 0.6, boss=True, moedas=(140, 260)),
    # --- Transilvânia: Família Vlakor (elite) ---
    mon("monster_fab_barao_vlakor", "Barão Vlakor", "combatente", 37, "vampire_f", "loot_fab_vlakor", "fab_vamp_barao", 4.4, 2.6, 5.0, 0.6, elite=True),
    mon("monster_fab_baronesa_vlakor", "Baronesa Lucrécia Vlakor", "conjurador", 37, "vampire_f", "loot_fab_vlakor", "vampire_f", 3.8, 2.5, 4.5, 0.55, elite=True),
    mon("monster_fab_gemeo_vlakor_a", "Dimitri Vlakor (gêmeo)", "agil", 36, "beast_lunge", "loot_fab_vlakor", "fab_vamp_jovem", 3.4, 2.4, 4.0, 0.5, elite=True),
    mon("monster_fab_gemeo_vlakor_b", "Doroteia Vlakor (gêmea)", "agil", 36, "vampire_f", "loot_fab_vlakor", "fab_vamp_jovem_f", 3.4, 2.4, 4.0, 0.5, elite=True),
    # --- Fundo do Triângulo ---
    mon("monster_fab_tritao_corrompido", "Nereu, o Tritão Corrompido", "tanque", 54, "fab_tritao", "loot_fab_tritao", "fab_tritao", 4.6, 3.0, 14, 1.0, boss=True, moedas=(500, 900)),
]

A = lambda **k: k   # atalho para ataques

COMPORTAMENTOS = [
    {"id": "fab_pai", "move_speed": 3.2, "aggro_radius": 10, "leash_radius": 18, "exposed_mult": 1.5, "attacks": [
        A(id="barrigada", shape="lunge", range=5.5, min_range=2.0, windup_ms=900, active_ms=350, recovery_ms=1100, cooldown_ms=3600, damage_mult=1.4, radius=1.2, lunge_m=4.5, track_fraction=0.4),
        A(id="tapao", shape="cone", range=2.4, windup_ms=650, active_ms=150, recovery_ms=800, cooldown_ms=600, damage_mult=1.0, arc=120, track_fraction=0.55),
        A(id="sentada", shape="circle", range=2.5, windup_ms=1100, active_ms=200, recovery_ms=1300, cooldown_ms=6000, damage_mult=1.6, radius=2.6, offset=0.5, track_fraction=0.2, status="atordoado", status_ms=900)]},
    {"id": "fab_mae", "move_speed": 4.2, "aggro_radius": 11, "leash_radius": 20, "exposed_mult": 1.4, "attacks": [
        A(id="chicote_tranca", shape="cone", range=3.6, windup_ms=600, active_ms=500, recovery_ms=800, cooldown_ms=1800, damage_mult=0.75, arc=150, hits=2, hit_interval_ms=280, track_fraction=0.55),
        A(id="laco_tranca", shape="beam", range=7.5, windup_ms=850, active_ms=200, recovery_ms=900, cooldown_ms=4200, damage_mult=1.1, radius=0.6, min_range=2.5, track_fraction=0.6, status="lentidao", status_ms=1800),
        A(id="rolo_de_massa", shape="cone", range=2.0, windup_ms=480, active_ms=150, recovery_ms=650, cooldown_ms=500, damage_mult=0.9, arc=90, track_fraction=0.6)]},
    {"id": "fab_filho", "move_speed": 4.6, "aggro_radius": 12, "leash_radius": 22, "keep_distance": 6.5, "flee_distance": 3.0, "exposed_mult": 1.5, "attacks": [
        A(id="estilingue", shape="projectile", range=11, min_range=2.0, windup_ms=650, active_ms=100, recovery_ms=600, cooldown_ms=900, damage_mult=0.75, radius=0.25, projectile_speed=14, track_fraction=0.8),
        A(id="rajada_estilingue", shape="projectile", range=10, min_range=2.0, windup_ms=900, active_ms=100, recovery_ms=900, cooldown_ms=5000, damage_mult=0.55, radius=0.25, projectile_speed=13, count=3, spread_deg=30, track_fraction=0.75),
        A(id="caneladas", shape="cone", range=1.8, windup_ms=420, active_ms=150, recovery_ms=600, cooldown_ms=500, damage_mult=0.7, arc=90, backstep_m=2.6, track_fraction=0.6)]},
    {"id": "fab_filha", "move_speed": 3.8, "aggro_radius": 12, "leash_radius": 22, "keep_distance": 7.0, "flee_distance": 3.0, "exposed_mult": 1.5, "attacks": [
        A(id="nota_desafinada", shape="projectile", range=11, min_range=2.0, windup_ms=700, active_ms=100, recovery_ms=650, cooldown_ms=1100, damage_mult=0.8, radius=0.35, projectile_speed=9, track_fraction=0.8),
        A(id="acorde_estridente", shape="circle", range=10, windup_ms=1100, active_ms=200, recovery_ms=1100, cooldown_ms=5200, damage_mult=1.3, radius=2.2, at_target=True, track_fraction=0.65, status="atordoado", status_ms=700),
        A(id="rabecada", shape="cone", range=1.8, windup_ms=450, active_ms=150, recovery_ms=600, cooldown_ms=500, damage_mult=0.7, arc=90, backstep_m=2.4, track_fraction=0.6)]},
    {"id": "fab_bebe", "move_speed": 3.4, "aggro_radius": 10, "leash_radius": 18, "keep_distance": 5.0, "exposed_mult": 1.6, "attacks": [
        A(id="chocalho", shape="projectile", range=9, min_range=1.5, windup_ms=600, active_ms=100, recovery_ms=500, cooldown_ms=800, damage_mult=0.6, radius=0.3, projectile_speed=8, count=2, spread_deg=24, track_fraction=0.8),
        A(id="birra", shape="circle", range=2.0, windup_ms=900, active_ms=900, recovery_ms=900, cooldown_ms=5200, damage_mult=0.6, radius=2.2, offset=0, hits=3, hit_interval_ms=300, track_fraction=0)]},
    {"id": "fab_gnomo", "move_speed": 4.3, "aggro_radius": 10, "leash_radius": 20, "exposed_mult": 1.5, "attacks": [
        A(id="esporos", shape="projectile", range=8, min_range=2.0, windup_ms=700, active_ms=100, recovery_ms=700, cooldown_ms=2400, damage_mult=0.5, radius=0.35, projectile_speed=8, count=3, spread_deg=26, track_fraction=0.8, status="veneno", status_ms=3000),
        A(id="ancinho", shape="cone", range=1.9, windup_ms=460, active_ms=130, recovery_ms=600, cooldown_ms=500, damage_mult=0.8, arc=90, track_fraction=0.6, backstep_m=2.0)]},
    {"id": "fab_fungilda", "move_speed": 3.3, "aggro_radius": 13, "leash_radius": 16, "keep_distance": 6.0, "exposed_mult": 1.35, "staggerable": False,
     "summon": "monster_fab_gnomo_enfeiticado", "summon_count": 2, "summon_every_ms": 18000, "phase_two_at": 0.5, "phase_two_attacks": ["chuva_de_esporos"], "attacks": [
        A(id="esporo_grande", shape="projectile", range=12, windup_ms=800, active_ms=100, recovery_ms=800, cooldown_ms=1600, damage_mult=0.9, radius=0.5, projectile_speed=9, count=3, spread_deg=30, track_fraction=0.8, status="veneno", status_ms=3500),
        A(id="cogumelos_brotam", shape="circle", range=11, windup_ms=1100, active_ms=1500, recovery_ms=1200, cooldown_ms=6000, damage_mult=1.1, radius=1.6, offset=1.8, hits=4, hit_interval_ms=360, step_m=1.7, track_fraction=0.5),
        A(id="tesourada", shape="cone", range=2.6, windup_ms=600, active_ms=150, recovery_ms=800, cooldown_ms=700, damage_mult=1.1, arc=110, track_fraction=0.55),
        A(id="chuva_de_esporos", shape="circle", range=12, windup_ms=1300, active_ms=200, recovery_ms=1400, cooldown_ms=7000, damage_mult=1.6, radius=3.0, at_target=True, track_fraction=0.7, status="veneno", status_ms=4000)]},
    {"id": "fab_malaquias", "move_speed": 5.0, "aggro_radius": 13, "leash_radius": 22, "exposed_mult": 1.45, "attacks": [
        A(id="malabares_faca", shape="projectile", range=10, min_range=2.5, windup_ms=750, active_ms=100, recovery_ms=700, cooldown_ms=2600, damage_mult=0.7, radius=0.3, projectile_speed=13, count=3, spread_deg=40, track_fraction=0.75),
        A(id="cambalhota", shape="lunge", range=7, min_range=2.5, windup_ms=550, active_ms=300, recovery_ms=800, cooldown_ms=3400, damage_mult=1.2, radius=0.8, lunge_m=6, track_fraction=0.5),
        A(id="torta_na_cara", shape="projectile", range=9, windup_ms=900, active_ms=100, recovery_ms=900, cooldown_ms=6000, damage_mult=0.5, radius=0.6, projectile_speed=8, track_fraction=0.85, status="lentidao", status_ms=2500),
        A(id="cetro_guizo", shape="cone", range=2.2, windup_ms=450, active_ms=450, recovery_ms=700, cooldown_ms=700, damage_mult=0.65, arc=100, hits=2, hit_interval_ms=260, track_fraction=0.6)]},
    {"id": "fab_grao_bobo", "move_speed": 3.6, "aggro_radius": 14, "leash_radius": 18, "exposed_mult": 1.35, "staggerable": False,
     "summon": "monster_fab_soldadinho", "summon_count": 2, "summon_every_ms": 16000, "phase_two_at": 0.5, "phase_two_attacks": ["gargalhada"], "attacks": [
        A(id="martelada_de_brinquedo", shape="circle", range=3.2, windup_ms=1000, active_ms=200, recovery_ms=1200, cooldown_ms=3200, damage_mult=1.6, radius=2.4, offset=1.6, track_fraction=0.3),
        A(id="tortas_em_leque", shape="projectile", range=12, windup_ms=900, active_ms=100, recovery_ms=900, cooldown_ms=3600, damage_mult=0.8, radius=0.5, projectile_speed=10, count=5, spread_deg=60, track_fraction=0.75),
        A(id="pulo_de_mola", shape="circle", range=11, windup_ms=1200, active_ms=200, recovery_ms=1300, cooldown_ms=6500, damage_mult=1.8, radius=2.6, at_target=True, track_fraction=0.7),
        A(id="cetro_giratorio", shape="cone", range=3.0, windup_ms=700, active_ms=900, recovery_ms=900, cooldown_ms=2200, damage_mult=0.7, arc=200, hits=3, hit_interval_ms=300, track_fraction=0.5),
        A(id="gargalhada", shape="circle", range=4.0, windup_ms=1400, active_ms=200, recovery_ms=1500, cooldown_ms=8000, damage_mult=2.0, radius=5.5, offset=0, track_fraction=0, status="atordoado", status_ms=1000)]},
    {"id": "fab_macaco", "move_speed": 5.0, "aggro_radius": 11, "leash_radius": 20, "keep_distance": 5.5, "exposed_mult": 1.5, "attacks": [
        A(id="mordida_macaco", shape="cone", range=1.9, windup_ms=420, active_ms=140, recovery_ms=600, cooldown_ms=500, damage_mult=0.9, arc=90, track_fraction=0.6, backstep_m=2.4),
        A(id="coco", shape="projectile", range=10, min_range=2.2, windup_ms=650, active_ms=100, recovery_ms=650, cooldown_ms=1300, damage_mult=0.85, radius=0.35, projectile_speed=11, track_fraction=0.8),
        A(id="chuva_de_coco", shape="circle", range=10, windup_ms=1000, active_ms=200, recovery_ms=900, cooldown_ms=5200, damage_mult=1.1, radius=1.6, at_target=True, track_fraction=0.7, status="atordoado", status_ms=600)]},
    {"id": "fab_dentuco", "move_speed": 4.6, "aggro_radius": 11, "leash_radius": 22, "exposed_mult": 1.5, "attacks": [
        # esfrega a pata no chão (preparação longa, dá para ver vindo), dispara, emenda a cabeçada e as mordidas para os lados
        A(id="esfrega_pata", shape="lunge", range=9, min_range=3.0, windup_ms=1300, active_ms=420, recovery_ms=400, cooldown_ms=5200, damage_mult=1.2, radius=0.9, lunge_m=7.5, track_fraction=0.55, followup="cabecada"),
        A(id="cabecada", shape="lunge", range=2.2, windup_ms=300, active_ms=200, recovery_ms=300, cooldown_ms=2000, damage_mult=1.1, radius=0.9, lunge_m=1.4, track_fraction=0.6, followup="mordidas_laterais", status="atordoado", status_ms=500),
        A(id="mordidas_laterais", shape="cone", range=2.4, windup_ms=350, active_ms=1000, recovery_ms=1000, cooldown_ms=2600, damage_mult=0.55, arc=170, hits=4, hit_interval_ms=260, step_m=0.3, track_fraction=0.5),
        A(id="mordida_dentuco", shape="cone", range=2.0, windup_ms=480, active_ms=150, recovery_ms=650, cooldown_ms=600, damage_mult=0.85, arc=100, track_fraction=0.6)]},
    {"id": "fab_dentuco_pai", "move_speed": 4.2, "aggro_radius": 12, "leash_radius": 22, "exposed_mult": 1.45, "staggerable": False, "attacks": [
        A(id="esfrega_pata", shape="lunge", range=11, min_range=3.0, windup_ms=1500, active_ms=480, recovery_ms=400, cooldown_ms=4800, damage_mult=1.5, radius=1.3, lunge_m=9.5, track_fraction=0.55, followup="cabecada"),
        A(id="cabecada", shape="lunge", range=2.8, windup_ms=320, active_ms=220, recovery_ms=320, cooldown_ms=2000, damage_mult=1.3, radius=1.3, lunge_m=1.8, track_fraction=0.6, followup="mordidas_laterais", status="atordoado", status_ms=700),
        A(id="mordidas_laterais", shape="cone", range=3.0, windup_ms=380, active_ms=1300, recovery_ms=1100, cooldown_ms=2600, damage_mult=0.6, arc=190, hits=5, hit_interval_ms=260, step_m=0.35, track_fraction=0.5),
        A(id="rabada_espinhos", shape="circle", range=3.0, windup_ms=900, active_ms=200, recovery_ms=1100, cooldown_ms=4200, damage_mult=1.4, radius=3.0, offset=0, track_fraction=0),
        A(id="mordida_dentuco", shape="cone", range=2.4, windup_ms=500, active_ms=150, recovery_ms=650, cooldown_ms=600, damage_mult=1.0, arc=110, track_fraction=0.6)]},
    {"id": "fab_urtiga", "move_speed": 3.2, "aggro_radius": 13, "leash_radius": 16, "keep_distance": 5.5, "exposed_mult": 1.35, "staggerable": False,
     "summon": "monster_fab_broto_urtiga", "summon_count": 2, "summon_every_ms": 17000, "phase_two_at": 0.5, "phase_two_attacks": ["jardim_de_espinhos"], "attacks": [
        A(id="espinho_venenoso", shape="projectile", range=12, windup_ms=700, active_ms=100, recovery_ms=700, cooldown_ms=1500, damage_mult=0.8, radius=0.35, projectile_speed=13, count=3, spread_deg=24, track_fraction=0.8, status="veneno", status_ms=3500),
        A(id="cipo_chicote", shape="cone", range=4.4, windup_ms=800, active_ms=200, recovery_ms=900, cooldown_ms=1500, damage_mult=1.2, arc=160, windup_jitter_ms=350, track_fraction=0.45),
        A(id="abraco_da_hera", shape="circle", range=11, windup_ms=1100, active_ms=200, recovery_ms=1100, cooldown_ms=5000, damage_mult=1.5, radius=2.0, at_target=True, track_fraction=0.65, status="lentidao", status_ms=2200),
        A(id="jardim_de_espinhos", shape="circle", range=3.0, windup_ms=1400, active_ms=200, recovery_ms=1500, cooldown_ms=7000, damage_mult=2.0, radius=5.0, offset=0, track_fraction=0, status="veneno", status_ms=4000)]},
    {"id": "fab_tritao", "move_speed": 3.8, "aggro_radius": 14, "leash_radius": 18, "exposed_mult": 1.35, "staggerable": False,
     "phase_two_at": 0.5, "phase_two_attacks": ["mare_de_lodo"], "attacks": [
        A(id="tridente_estocadas", shape="lunge", range=4.5, windup_ms=700, active_ms=800, recovery_ms=900, cooldown_ms=1800, damage_mult=0.8, radius=0.7, lunge_m=1.2, hits=3, hit_interval_ms=270, step_m=0.6, track_fraction=0.55),
        A(id="jato_de_lodo", shape="beam", range=11, min_range=3.0, windup_ms=1000, active_ms=300, recovery_ms=1000, cooldown_ms=4200, damage_mult=1.4, radius=0.8, track_fraction=0.6, status="veneno", status_ms=4000),
        A(id="bolhas_toxicas", shape="projectile", range=12, windup_ms=850, active_ms=100, recovery_ms=850, cooldown_ms=3600, damage_mult=0.8, radius=0.5, projectile_speed=7, count=5, spread_deg=70, track_fraction=0.7, status="veneno", status_ms=3000),
        A(id="mare_de_lodo", shape="circle", range=4.0, windup_ms=1500, active_ms=200, recovery_ms=1500, cooldown_ms=8000, damage_mult=2.2, radius=6.0, offset=0, track_fraction=0)]},
]

ITENS = [
    {"id": "mat_esporo_lunar", "display_name": "Esporo Lunar", "summary": "Pó roxo que brilha no escuro. Foi com isto que a Fungilda enfeitiçou os gnomos — e é com isto que se faz o antídoto."},
    {"id": "mat_guizo_castelo", "display_name": "Guizo do Castelo do Riso", "summary": "Sininho de chumbo que os soldadinhos levam no peito. O Agostinho chora toda vez que um toca."},
    {"id": "mat_coco_duro", "display_name": "Coco Duro", "summary": "Duro como pedra. O Macaco-Coqueiro tem uma pontaria assustadora."},
    {"id": "mat_dente_dentuco", "display_name": "Dente de Dentuço", "summary": "Dente curvo, ainda afiado. Os Dentuços trocam de dente como quem troca de meia."},
    {"id": "mat_folha_urtiga", "display_name": "Folha de Urtiga Encantada", "summary": "Coça só de olhar. A Dama Urtiga faz vestidos com isto."},
    {"id": "mat_escama_nereu", "display_name": "Escama do Tritão Nereu", "summary": "Escama azul-esverdeada, ainda manchada de lodo negro."},
]
for it in ITENS:
    it.update({"category": "material", "subtype": "material", "min_level": 1, "allowed_classes": [], "handedness": 0, "base_stats": {},
               "affix_pool": "none", "temperable": False, "trade_rule": "free", "stack_limit": 20, "icon_asset_id": "icon_" + it["id"],
               "model_asset_id": "mat_vein", "price": 40, "source": "Conteúdo de 13/11 (Era Uma Vez e arredores)."})

CANION = ["weapon_guerreiro_espada_canion", "weapon_guerreiro_machado_canion", "staff_mago_cajado_canion", "talisma_bruxa_canion", "arco_arqueiro_canion",
          "armor_guerreiro_canion", "armor_mago_canion", "armor_bruxa_canion", "armor_arqueiro_canion", "helmet_canion", "shield_guerreiro_canion"]
MATA = ["weapon_guerreiro_espada_mata", "weapon_guerreiro_machado_mata", "staff_mago_cajado_mata", "talisma_bruxa_mata", "arco_arqueiro_mata",
        "armor_guerreiro_mata", "armor_mago_mata", "armor_bruxa_mata", "armor_arqueiro_mata", "helmet_mata", "shield_guerreiro_mata"]
VESP = ["weapon_guerreiro_espada_vespera", "weapon_guerreiro_machado_vespera", "staff_mago_cajado_vespera", "talisma_bruxa_vespera", "arco_arqueiro_vespera",
        "armor_guerreiro_vespera", "armor_mago_vespera", "armor_bruxa_vespera", "armor_arqueiro_vespera", "helmet_vespera", "shield_guerreiro_vespera"]


def comum(mat, gear, chance_mat=0.3, chance_gear=0.05):
    return [{"chance": chance_mat, "item": mat}, {"chance": chance_gear, "pick": gear, "match_class": True},
            {"chance": 0.12, "pick": ["consumable_c03_frasco_mana", "consumable_c02_tonico_medio"], "match_class": True}]


def chefe(mat, gear, tonico="consumable_c02_tonico_medio", mana="consumable_c04_frasco_mana_grande"):
    return [{"chance": 1.0, "item": mat, "min_qty": 2, "max_qty": 4}, {"chance": 1.0, "pick": gear[:5], "match_class": True, "quality": "chefe"},
            {"chance": 1.0, "pick": gear[5:], "match_class": True}, {"chance": 1.0, "item": tonico, "min_qty": 2, "max_qty": 3},
            {"chance": 0.5, "pick": [mana, tonico], "match_class": True}]


def elite(mat, gear, tonico="consumable_c02_tonico_medio"):
    return [{"chance": 1.0, "item": mat, "min_qty": 1, "max_qty": 2}, {"chance": 1.0, "pick": gear, "match_class": True},
            {"chance": 1.0, "item": tonico, "min_qty": 1, "max_qty": 2}]


SAQUE = [
    {"id": "loot_fab_familia", "entries": elite("mat_guizo_castelo", CANION)[1:] + [{"chance": 0.5, "item": "mat_retalho", "min_qty": 1, "max_qty": 3}]},
    {"id": "loot_fab_gnomo", "entries": comum("mat_esporo_lunar", CANION, 0.55)},
    {"id": "loot_fab_castelo", "entries": comum("mat_guizo_castelo", CANION, 0.45)},
    {"id": "loot_fab_castelo_elite", "entries": elite("mat_guizo_castelo", CANION)},
    {"id": "loot_fab_chefe", "entries": chefe("mat_esporo_lunar", CANION)},
    {"id": "loot_fab_macaco", "entries": comum("mat_coco_duro", MATA, 0.4)},
    {"id": "loot_fab_dentuco", "entries": comum("mat_dente_dentuco", CANION, 0.4)},
    {"id": "loot_fab_dentuco_pai", "entries": elite("mat_dente_dentuco", CANION)},
    {"id": "loot_fab_urtiga", "entries": comum("mat_folha_urtiga", MATA, 0.4)},
    {"id": "loot_fab_urtiga_chefe", "entries": chefe("mat_folha_urtiga", MATA)},
    {"id": "loot_fab_vlakor", "entries": elite("mat_cinza_vampirica", VESP, "consumable_c05_tonico_forte")},
    {"id": "loot_fab_tritao", "entries": chefe("mat_escama_nereu", VESP, "consumable_c05_tonico_forte")},
]


# ------------------------------------------------------------------ missões

def q(id_, nome, giver, kind, targets, count, desc, hint, xp, coins, lvl, mapa, **extra):
    d = {"id": id_, "name": nome, "giver": giver, "kind": kind, "targets": targets, "count": count, "description": desc, "hint": hint,
         "reward_xp": xp, "reward_coins": coins, "min_level": lvl, "map": mapa, "category": "regiao"}
    d.update(extra)
    return d


def xp(lvl, k=1.0):
    # mesma ordem de grandeza das missões de região (q_cad_mata_2, nível 22: ~34 mil)
    return int(round(1500 * lvl * (1 + lvl / 20) * k, -1))


FAMILIA = ["monster_fab_pai_pancudo", "monster_fab_mae_cabeleira", "monster_fab_filho_estilingue", "monster_fab_filha_rabeca", "monster_fab_bebe_birra"]

MISSOES = [
    # Aquela Família
    q("q_fab_familia", "Aquela Família", "vizinho_florencio", "kill", FAMILIA, 5,
      "«Cinco! Cinco na mesma casa! O pai come a minha cerca, a mãe me enforca com a trança, o menino quebra minha janela, a menina toca rabeca às três da manhã e o bebê... o bebê joga as coisas. Me livra DAQUELA FAMÍLIA.»",
      "Derrote os cinco da casa torta, a oeste: o pai, a mãe, o filho, a filha e o bebê.", xp(25, 1.3), 700, 24, "reino_fabula",
      reward_items=["consumable_c02_tonico_medio*4"], category="historia"),
    # jardim
    q("q_fab_gnomos_1", "Gnomos Enfeitiçados", "jardineiro_lucio", "kill", ["monster_fab_gnomo_enfeiticado"], 5,
      "«Meus gnomos de jardim! Eram de barro, eram quietos, eram meus! A Fungilda soprou o pó do cogumelo neles e agora mordem. Não mata, não — derruba. Derrubado, o feitiço afrouxa.»",
      "Derrube 5 gnomos enfeitiçados no jardim, a leste.", xp(24), 420, 24, "reino_fabula", category="historia"),
    q("q_fab_gnomos_2", "O Antídoto do Jardineiro", "jardineiro_lucio", "collect", ["mat_esporo_lunar"], 5,
      "«O veneno é o remédio, diz minha avó. Me traga o pó roxo que cai deles — cinco punhados — e eu faço o antídoto.»",
      "Esporo Lunar cai dos gnomos enfeitiçados. Leve 5 ao Lúcio.", xp(24, 0.8), 380, 24, "reino_fabula", requires="q_fab_gnomos_1", category="historia"),
    q("q_fab_gnomos_3", "Curar os Gnomos", "jardineiro_lucio", "interact", [f"fab_gnomo_caido_{i}" for i in range(1, 6)], 5,
      "«Pronto, o antídoto! Os cinco que você derrubou estão caídos no canteiro. Uma gota na boca de cada um. Cuidado: espirram.»",
      "Aperte F nos 5 gnomos caídos no canteiro do jardim.", xp(24, 1.1), 500, 24, "reino_fabula", requires="q_fab_gnomos_2",
      reward_flags=["fab:gnomos_salvos"], category="historia"),
    q("q_fab_fungilda", "Dona Fungilda", "jardineiro_lucio", "boss", ["monster_fab_fungilda"], 1,
      "«Meus meninos estão de volta! Mas enquanto aquela mulher tiver o cogumelo, ela enfeitiça outros. Ela fica na estufa, atrás do cogumelo gigante. Cuidado: ela chama gnomos para ajudar.»",
      "Derrote Dona Fungilda na estufa do jardim (leste).", xp(27, 1.6), 1200, 25, "reino_fabula", requires="q_fab_gnomos_3",
      reward_items=["consumable_c02_tonico_medio*5"], category="historia"),
    # castelo — Agostinho (normais)
    q("q_fab_agostinho_1", "Soldadinhos sem Corda", "agostinho_bobo", "kill", ["monster_fab_soldadinho"], 8,
      "«*snif* Os soldadinhos do castelo eram de brincar. Alguém deu corda demais neles e agora não param. *buáá* Para eles para mim? Chora agora, ri depois, é o meu lema.»",
      "Derrote 8 soldadinhos de chumbo no pátio do Castelo do Riso (norte).", xp(26), 520, 25, "reino_fabula"),
    q("q_fab_agostinho_2", "Os Guizos Perdidos", "agostinho_bobo", "collect", ["mat_guizo_castelo"], 6,
      "«Cada soldadinho levou um guizo do meu chapéu. Sem guizo, bobo não é bobo, é só um homem triste de pijama listrado. *snif*»",
      "Guizo do Castelo cai dos soldadinhos. Leve 6 ao Agostinho.", xp(26, 0.8), 480, 25, "reino_fabula", requires="q_fab_agostinho_1"),
    # castelo — Malaquias (tarefas impossíveis, enigmas, traição)
    q("q_fab_malaquias_1", "A Tarefa Impossível", "bobo_malaquias", "talk", ["agostinho_bobo"], 1,
      "«Hehehe! Quer servir ao castelo? Então me traga um balde de ÁGUA SECA, a sombra de um fantasma e o barulho de uma palma batendo sozinha. Vá, vá! Pergunte ao chorão se não acredita.»",
      "Pergunte ao Agostinho sobre a 'água seca'.", xp(26, 0.5), 200, 25, "reino_fabula", requires="q_fab_agostinho_1"),
    q("q_fab_malaquias_2", "Os Três Enigmas", "bobo_malaquias", "interact", ["fab_enigma_1", "fab_enigma_2", "fab_enigma_3"], 3,
      "«O chorão contou, foi? Tudo bem, tudo bem... Então resolva os três enigmas das estátuas do pátio. Quem resolver, eu deixo entrar no salão. Hehehe.»",
      "Aperte F nas três estátuas do pátio e leia os enigmas. Depois volte ao Malaquias.", xp(26, 0.9), 400, 25, "reino_fabula", requires="q_fab_malaquias_1",
      reward_flags=["fab:malaquias_revelado"]),
    q("q_fab_malaquias_3", "O Riso que Mata", "agostinho_bobo", "boss", ["monster_fab_malaquias"], 1,
      "«*soluço* Você resolveu os enigmas e ele... ele riu e sumiu para o picadeiro com as facas! Ele nunca quis que ninguém entrasse — quer o castelo só para ele. Ele vai tentar te matar. Mata ele antes. *buáá*»",
      "Derrote o Malaquias no picadeiro, dentro do castelo (lado leste do pátio).", xp(27, 1.3), 900, 25, "reino_fabula", requires="q_fab_malaquias_2",
      reward_flags=["fab:malaquias_morto"], category="historia"),
    q("q_fab_grao_bobo", "A Fúria do Grão-Bobo", "agostinho_bobo", "boss", ["monster_fab_grao_bobo"], 1,
      "«Ouviu esse trovão? É o Grão-Bobo, o chefe do Malaquias. Ele ficou FURIOSO. Está no salão do trono com o cetro de mola. Chora agora... luta agora!»",
      "Derrote o Grão-Bobo no Salão do Riso (fundo do castelo).", xp(29, 1.8), 1800, 26, "reino_fabula", requires="q_fab_malaquias_3",
      reward_items=["consumable_c02_tonico_medio*5"], reward_flags=["fab:castelo_livre"], category="historia"),
    # Mata dos Enforcados: macacos
    q("q_fab_macacos", "Chuva de Cocos", "carpinteiro_jorge", "kill", ["monster_fab_macaco_coqueiro"], 6,
      "«Tem macaco nas árvores da clareira jogando coco em quem passa. Já me acertaram três vezes. TRÊS. Desce eles de lá.»",
      "Macacos-Coqueiro ficam nos galhos perto da Clareira dos Enforcados (oeste). Chegue perto: eles descem.", xp(20), 380, 18, "mata_dos_enforcados"),
    # Cânions: dentuços
    q("q_fab_dentucos", "Os Dentuços", "tratador_dimas", "kill", ["monster_fab_dentuco"], 6,
      "«Já viu bicho esfregar a pata no chão antes de correr? Quando ver, sai da frente. Os Dentuços desceram da Ruína da Escavação. Seis, por favor.»",
      "Dentuços na Ruína da Escavação (oeste dos Cânions). Quando ele esfregar a pata, desvie da corrida.", xp(24), 460, 22, "canions_suplicantes"),
    q("q_fab_dentuco_pai", "O Dentuço-Pai", "tratador_dimas", "boss", ["monster_fab_dentuco_pai"], 1,
      "«Derrubou os filhotes? Então o pai vai aparecer. Maior, mais espinho, mais dente. A corrida dele vai longe.»",
      "Derrote o Dentuço-Pai na Ruína da Escavação.", xp(27, 1.3), 900, 24, "canions_suplicantes", requires="q_fab_dentucos"),
    # Floresta Oculta: Dama Urtiga
    q("q_fab_urtiga", "A Dama Urtiga", "curupira", "boss", ["monster_fab_dama_urtiga"], 1,
      "«Tem uma dona aqui que se diz rainha da mata. Fala com as plantas, e as plantas obedecem. Enrosca, espinha, envenena. Minha mata não tem rainha — tem guardião. Tira ela daqui.»",
      "A Dama Urtiga fica no Jardim de Espinhos (leste da Floresta Oculta). Ela chama brotos e envenena.", xp(23, 1.5), 900, 20, "floresta_oculta",
      category="historia"),
    q("q_fab_brotos", "Brotos de Urtiga", "curupira", "kill", ["monster_fab_broto_urtiga"], 6,
      "«Ela plantou brotos que mordem. Arranca seis pra mim.»", "Brotos de Urtiga no Jardim de Espinhos.", xp(21), 360, 20, "floresta_oculta"),
    # Transilvânia: Família Vlakor
    q("q_fab_vlakor", "A Família Vlakor", "igor_criado", "kill",
      ["monster_fab_barao_vlakor", "monster_fab_baronesa_vlakor", "monster_fab_gemeo_vlakor_a", "monster_fab_gemeo_vlakor_b"], 4,
      "«O Conde é o Conde. Mas os Vlakor... os Vlakor são PIORES. O Barão, a Baronesa e os gêmeos. Jantam no mausoléu do oeste todas as noites. Eu sirvo a sopa. Eu QUERO parar de servir a sopa.»",
      "Derrote os quatro Vlakor (elite) no Mausoléu dos Vlakor, a oeste.", xp(37, 1.3), 1600, 35, "reino_transilvania", category="historia"),
    # Fundo do Triângulo: Tritão Corrompido (escolha)
    q("q_fab_tritao_1", "O Tritão Corrompido", "sereia_lia", "boss", ["monster_fab_tritao_corrompido"], 1,
      "«Nereu era o guardião do recife. O lodo negro que os navios despejam entrou nas guelras dele e ele enlouqueceu — ataca até a gente. Derruba ele. Depois... depois a gente decide.»",
      "Derrote Nereu, o Tritão Corrompido, no Recife Envenenado (sudoeste do Fundo).", xp(54, 1.4), 4000, 50, "fundo_triangulo", category="historia"),
    q("q_fab_tritao_salvar", "Salvar o Nereu", "sereia_lia", "interact", ["fab_fonte_lodo_1", "fab_fonte_lodo_2", "fab_fonte_lodo_3"], 3,
      "«Ele está caído e respira. Se a gente tapar as três fontes de lodo do recife, a água limpa e o Nereu volta a ser ele. Escolha difícil, mas é a minha.»",
      "Tape as três fontes de lodo do Recife Envenenado (F em cada uma). ESCOLHA: quem salva não pode acabar com ele.", xp(54, 1.2), 3000, 50, "fundo_triangulo",
      requires="q_fab_tritao_1", forbid_flag="fab:tritao_morto", reward_flags=["fab:tritao_salvo"], category="historia"),
    q("q_fab_tritao_acabar", "Acabar com o Sofrimento", "sereia_lia", "interact", ["fab_tritao_caido"], 1,
      "«Você acha que não tem volta? Que ele vai enlouquecer de novo? ...Então faça rápido. Ele merece não sofrer.»",
      "Aperte F no Nereu caído, no recife. ESCOLHA: quem acaba com ele não pode mais salvá-lo.", xp(54, 1.2), 3000, 50, "fundo_triangulo",
      requires="q_fab_tritao_1", forbid_flag="fab:tritao_salvo", reward_flags=["fab:tritao_morto"],
      reward_items=["mat_escama_nereu*4"], category="historia"),
]

# 13/11 ("o Zeca Requebra vai ter missões? alguma função?"): sim — três missões curtas de começo (níveis 2–6) e, no fim,
# ele dança na praça e o Porto inteiro ganha o "Ritmo do Zeca" (marca que libera a fala nova dele)
MISSOES += [
    q("q_zeca_1", "Roupa de Palco", "oficio_dancarino_porto", "collect", ["mat_retalho"], 6,
      "«Um, dois, três... e RASGOU! Minha calça de dança não aguentou o giro. Me traz uns retalhos que eu costuro uma roupa de palco — os cachorros sarnentos da Costa vivem com pano preso no pelo.»",
      "Retalho de Saqueador cai do Cão Sarnento (Costa do Cal, perto do portão). Leve 6 ao Zeca, no tablado do Porto.", 700, 60, 2, "costa_do_cal",
      reward_items=["consumable_c01_tonico_fraco*3"]),
    q("q_zeca_2", "Plateia de Ratos", "oficio_dancarino_porto", "kill", ["monster_bst_rato_do_cais"], 8,
      "«Ensaiei a noite inteira e a única plateia foi de RATO. Roeram a madeira do tablado! Espanta oito deles lá na Costa, que a madeira é cara.»",
      "Ratos do Cais na Costa do Cal. Derrote 8 e volte ao Zeca.", 1100, 90, 3, "costa_do_cal", requires="q_zeca_1"),
    q("q_zeca_3", "O Baile da Gilda", "oficio_dancarino_porto", "talk", ["taverneira_gilda"], 1,
      "«Roupa pronta, tablado inteiro... falta público de gente! Vai lá no casarão e convida a Gilda para o baile. Se ela vier, a cidade inteira vem atrás.»",
      "Fale com a Gilda, a Taverneira (casarão, norte da praça) e volte ao Zeca.", 900, 120, 4, "porto_das_cinzas", requires="q_zeca_2",
      reward_flags=["zeca:baile"], reward_items=["consumable_c02_tonico_medio*2"]),
]

# ------------------------------------------------------------------ o reino "Era Uma Vez"

H = 70


def caixa(kind, x, y, w, h, altura=5.0):
    return {"kind": kind, "shape": "box", "x": round(x, 2), "y": round(y, 2), "w": round(w, 2), "h": round(h, 2), "r": 0, "height": altura}


def circ(kind, x, y, r, altura=4.0):
    return {"kind": kind, "shape": "circle", "x": round(x, 2), "y": round(y, 2), "w": 0, "h": 0, "r": round(r, 2), "height": altura}


def spawn(id_, monster, x, y, lvl, respawn=60, grupo=None, **extra):
    d = {"id": id_, "monster": monster, "x": x, "y": y, "level": lvl, "group": grupo or id_, "respawn_seconds": respawn}
    d.update(extra)
    return d


def npc(id_, nome, x, y, look, fala, facing=180, role="missoes", **extra):
    d = {"id": id_, "name": nome, "role": role, "x": x, "y": y, "facing": facing, "look": look, "greeting": fala}
    d.update(extra)
    return d


def reino():
    rnd = random.Random("fabula")
    m = {"id": "reino_fabula", "display_name": "Era Uma Vez — o Reino dos Contos", "half_size": H, "spawn_x": 0, "spawn_y": 60, "safe_radius": 7,
         "xp_multiplier": 1.15, "kind": "campo", "region": "reinos", "theme": "mata",
         "zones": [], "obstacles": [], "gates": [], "chests": [], "spawns": [], "props": [], "portals": [], "waypoints": [], "objects": [],
         "checkpoints": [], "npcs": [], "roads": []}
    Z = m["zones"]; O = m["obstacles"]; P = m["props"]; S = m["spawns"]; N = m["npcs"]; OB = m["objects"]
    Z += [{"id": "chegada", "name": "Página Um (chegada)", "kind": "seguro", "x": 0, "y": 59, "w": 18, "h": 14},
          {"id": "estrada", "name": "Estrada de Tijolos", "kind": "caminho", "x": 0, "y": 12, "w": 5, "h": 80},
          {"id": "familia", "name": "A Casa Torta Daquela Família", "kind": "medio", "x": -42, "y": 20, "w": 30, "h": 24},
          {"id": "jardim", "name": "Jardim de Dona Fungilda", "kind": "medio", "x": 42, "y": 18, "w": 30, "h": 26},
          {"id": "estufa", "name": "Estufa: Dona Fungilda", "kind": "chefe", "x": 50, "y": 4, "w": 14, "h": 10},
          {"id": "bosque", "name": "Bosque do Lobo Mau", "kind": "fraco", "x": -30, "y": -14, "w": 26, "h": 18},
          {"id": "portao_castelo", "name": "Portão do Castelo do Riso", "kind": "seguro", "x": 0, "y": -20, "w": 16, "h": 9},
          {"id": "patio", "name": "Pátio do Castelo do Riso", "kind": "medio", "x": 0, "y": -38, "w": 40, "h": 18},
          {"id": "picadeiro", "name": "Picadeiro do Malaquias", "kind": "perigoso", "x": 15, "y": -50, "w": 12, "h": 10},
          {"id": "salao", "name": "Salão do Riso: o Grão-Bobo", "kind": "chefe", "x": -6, "y": -54, "w": 22, "h": 10}]
    m["roads"] = [{"id": "fab_estrada", "width": 3.4, "points": [[0, 64], [0, 30], [0, -22]]},
                  {"id": "fab_oeste", "width": 2.6, "points": [[0, 30], [-14, 28], [-26, 26]]},
                  {"id": "fab_leste", "width": 2.6, "points": [[0, 30], [14, 28], [26, 26]]}]
    # 14/11: a volta do Reino dos Contos cai no Coração dos Reinos (hub), na frente da porta dele
    m["portals"] = [{"id": "reino_fabula_volta", "x": 0, "y": 66, "radius": 2.0, "to_map": "reinos_ocultos", "to_x": -10.63, "to_y": 2.85,
                     "label": "Voltar ao Coração dos Reinos"}]
    m["waypoints"] = [{"id": "wp_fabula", "name": "Página Um", "x": 4.5, "y": 56}]
    P += [{"kind": "fogueira", "x": -4, "y": 56, "yaw": 0}, {"kind": "tocha", "x": -6, "y": 62, "yaw": 0}, {"kind": "tocha", "x": 6, "y": 62, "yaw": 0},
          {"kind": "estandarte", "x": -3, "y": 66, "yaw": 0}, {"kind": "estandarte", "x": 3, "y": 66, "yaw": 0}]
    N.append(npc("contadora_ondina", "Ondina, a Contadora de Histórias", -6, 58, "bruxa",
                 "Bem-vindo ao livro! Aqui cada caminho é um conto. A oeste, aquela família... a leste, o jardim de uma senhora nada gentil. Ao norte, o Castelo do Riso. Escolha a sua página.", 160, role="morador"))

    # --- casa torta (oeste) ---
    O.append(caixa("invisivel", -48, 14, 5.6, 5.2, 7))
    P.append({"kind": "sobrado", "x": -48, "y": 14, "yaw": 90, "kit": True})
    for i, (x, y) in enumerate([(-58, 28), (-54, 32), (-46, 33), (-38, 33), (-30, 32)]):
        P.append({"kind": "cerca", "x": x, "y": y, "yaw": 0 if i else 30})
    P += [{"kind": "caixotes", "x": -42, "y": 10, "yaw": 20}, {"kind": "barris_polvora", "x": -52, "y": 22, "yaw": 0}, {"kind": "canteiro", "x": -36, "y": 14, "yaw": 0}]
    N.append(npc("vizinho_florencio", "Florêncio, o Vizinho de Cerca", -24, 32, "aldeao",
                 "Ali, ó. A casa torta. NÃO olhe nos olhos do bebê.", 250))
    for mid, (x, y) in zip(FAMILIA, [(-44, 20), (-40, 24), (-36, 18), (-46, 26), (-40, 15)]):
        x, y = perto(m, x, y, rnd, 1.2, 3)
        S.append(spawn("fab_" + mid[12:], mid, x, y, 25, 90))

    # --- jardim (leste) ---
    for i in range(6):
        P.append({"kind": "canteiro", "x": 32 + (i % 3) * 7, "y": 22 + (i // 3) * 7, "yaw": 0})
    P += [{"kind": "horta", "x": 54, "y": 26, "yaw": 0}, {"kind": "cogumelo_gigante", "x": 50, "y": 12, "yaw": 0}]
    O.append(circ("invisivel", 50, 12, 2.0, 6))
    O.append(caixa("muralha", 50, -0.5, 14, 0.8, 2.6))   # fundo da estufa
    N.append(npc("jardineiro_lucio", "Lúcio, o Jardineiro", 26, 32, "aldeao",
                 "Meus gnomos! Ai, meus gnomos! Você tem cara de quem sabe derrubar sem quebrar.", 110))
    for i, (x, y) in enumerate([(30, 16), (36, 12), (44, 20), (38, 26), (46, 28)]):
        x, y = perto(m, x, y, rnd, 1.2, 3)
        S.append(spawn(f"fab_gnomo_{i}", "monster_fab_gnomo_enfeiticado", x, y, 24, 45))
    x, y = perto(m, 50, 5, rnd, 1.4, 3)
    S.append(spawn("fab_fungilda", "monster_fab_fungilda", x, y, 27, 600))
    for i, (x, y) in enumerate([(31, 24), (36, 31), (43, 24), (47, 31), (52, 22)], 1):
        x, y = perto(m, x, y, rnd, 0.8, 3)
        OB.append({"id": f"fab_gnomo_caido_{i}", "kind": "prisioneiro" if False else "reliquia", "x": x, "y": y, "label": "Gnomo caído (dar o antídoto)",
                   "quest": "q_fab_gnomos_3", "text": "Uma gota de antídoto... o gnomo espirra roxo, pisca e sorri. Curado!"})
    for i, (x, y) in enumerate([(22, 30), (22, 34), (24, 36), (28, 36), (30, 34)]):
        N.append(npc(f"fab_gnomo_salvo_{i}", "Gnomo de Jardim (curado)", x, y, "gnomo",
                     ["Atchim! Obrigado!", "Eu era de barro, sabia? Agora sou de barro FELIZ.", "Nunca mais chego perto de cogumelo.",
                      "O Lúcio chorou. Eu vi.", "Quer que eu guarde seu jardim? Eu guardo."][i], 200, role="morador", requires_flag="fab:gnomos_salvos"))

    # --- bosque do lobo (sudoeste do castelo) ---
    for i in range(4):
        x, y = perto(m, -30 + rnd.uniform(-8, 8), -14 + rnd.uniform(-6, 6), rnd, 1.2, 4)
        S.append(spawn(f"fab_lobo_{i}", "monster_fab_lobo_mau", x, y, 24, 50, grupo="fab_lobos"))

    # --- Castelo do Riso (norte): muralha com portão no sul ---
    x0, x1, y0, y1 = -22, 22, -60, -27
    O += [caixa("muralha", (x0 + x1) / 2, y0, x1 - x0, 1.4, 6), caixa("muralha", x0, (y0 + y1) / 2, 1.4, y1 - y0, 6),
          caixa("muralha", x1, (y0 + y1) / 2, 1.4, y1 - y0, 6),
          caixa("muralha", (x0 - 3.5) / 2, y1, -3.5 - x0, 1.4, 6), caixa("muralha", (x1 + 3.5) / 2, y1, x1 - 3.5, 1.4, 6)]
    P += [{"kind": "portao_fortificado", "x": 0, "y": y1, "yaw": 0}, {"kind": "torre_partida", "x": x0, "y": y0, "yaw": 0},
          {"kind": "torre_partida", "x": x1, "y": y0, "yaw": 90}, {"kind": "torre_partida", "x": x0, "y": y1, "yaw": 180}, {"kind": "torre_partida", "x": x1, "y": y1, "yaw": 270},
          {"kind": "estandarte", "x": -5, "y": -24, "yaw": 0}, {"kind": "estandarte", "x": 5, "y": -24, "yaw": 0},
          {"kind": "estandarte", "x": -10, "y": -58.5, "yaw": 0}, {"kind": "estandarte", "x": -2, "y": -58.5, "yaw": 0}]
    O += [circ("invisivel", x0, y0, 2.0, 8), circ("invisivel", x1, y0, 2.0, 8), circ("invisivel", x0, y1, 2.0, 8), circ("invisivel", x1, y1, 2.0, 8)]
    N.append(npc("agostinho_bobo", "Agostinho \"Chora-Agora\", o Bobo Triste", -6, -19, "bobo",
                 "*snif* Bem-vindo ao Castelo do Riso. Aqui ninguém ri há anos. Chora agora, ri depois — é o meu lema.", 180))
    N.append(npc("bobo_malaquias", "Malaquias, o Bobo de Sorriso Torto", 6, -19, "bobo",
                 "Hehehe! Visita! Adoro visitas. Elas nunca voltam. Hehehe.", 180, forbid_flag="fab:malaquias_revelado"))
    for i in range(8):
        x, y = perto(m, rnd.uniform(-16, 16), rnd.uniform(-44, -32), rnd, 1.2, 3)
        S.append(spawn(f"fab_soldadinho_{i}", "monster_fab_soldadinho", x, y, 26, 45, grupo=f"fab_sold_{i // 2}"))
    x, y = perto(m, 15, -50, rnd, 1.4, 2)
    S.append(spawn("fab_malaquias", "monster_fab_malaquias", x, y, 27, 300))
    x, y = perto(m, -6, -54, rnd, 1.6, 2)
    S.append(spawn("fab_grao_bobo", "monster_fab_grao_bobo", x, y, 29, 600))
    ENIGMAS = [
        ("Estátua do Enigma I", "«Quanto mais se tira, maior fica. O que é?» — Embaixo, riscado a faca: «UM BURACO». Alguém já resolveu... ou alguém quer que você pense que sim."),
        ("Estátua do Enigma II", "«Tem dentes e não morde, tem cabeça e não pensa.» A resposta brilha na pedra quando você diz em voz alta: «O ALHO». A estátua faz um som de risada abafada."),
        ("Estátua do Enigma III", "«O que o bobo diz quando ninguém ri?» Você responde «A VERDADE» — e a estátua se racha ao meio. Lá dentro, um bilhete: «Quem chegar até aqui, morre no picadeiro. — M.»"),
    ]
    for i, ((lbl, txt), (x, y)) in enumerate(zip(ENIGMAS, [(-14, -32), (0, -46), (14, -32)]), 1):
        x, y = perto(m, x, y, rnd, 1.0, 3)
        P.append({"kind": "estatua", "x": x, "y": round(y - 1.4, 1), "yaw": 0})
        OB.append({"id": f"fab_enigma_{i}", "kind": "reliquia", "x": x, "y": y, "label": lbl, "text": txt})

    # --- árvores e pedras (sem cobrir caminhos e lugares) e borda ---
    reservas = [(0, 59, 12), (-42, 20, 17), (42, 18, 18), (0, -42, 26), (0, 12, 5), (-30, -14, 6)]
    n = 0
    for _ in range(4000):
        if n >= 60: break
        x, y = rnd.uniform(-H + 6, H - 6), rnd.uniform(-H + 6, H - 6)
        if abs(x) < 5 or any(math.hypot(x - a, y - b) < r for a, b, r in reservas): continue
        if not livre(m, x, y, 2.4) or any(math.hypot(x - s["x"], y - s["y"]) < 4 for s in S): continue
        O.append(circ(rnd.choice(["arvore", "arvore", "arvore", "rocha"]), x, y, rnd.uniform(0.7, 1.3))); n += 1
    for t in range(-H + 3, H - 2, 6):
        for (x, y) in [(t, -H + 2), (t, H - 2), (-H + 2, t), (H - 2, t)]:
            if abs(x) < 4 and y > 60: continue
            O.append(circ("arvore", x + rnd.uniform(-1, 1), y + rnd.uniform(-1, 1), 1.1))
    return m


# ------------------------------------------------------------------ mapas que já existem

def mata():
    m = L("mata_dos_enforcados"); limpar(m); rnd = random.Random("macacos")
    arvores = sorted([o for o in m["obstacles"] if o["kind"] == "arvore" and o.get("shape") == "circle" and math.hypot(o["x"] + 40, o["y"] - 19) < 22],
                     key=lambda o: math.hypot(o["x"] + 40, o["y"] - 19))
    usados = []
    for o in arvores:
        if len(usados) >= 6: break
        if any(math.hypot(o["x"] - u["x"], o["y"] - u["y"]) < 6 for u in usados): continue
        # no galho: colado no tronco, do lado de fora (o validador não aceita dentro do obstáculo)
        for a in range(0, 360, 30):
            x = o["x"] + math.cos(math.radians(a)) * (o["r"] + 0.7); y = o["y"] + math.sin(math.radians(a)) * (o["r"] + 0.7)
            if livre(m, x, y, 0.5) and not any(math.hypot(x - s["x"], y - s["y"]) < 3 for s in m["spawns"]):
                usados.append(o)
                m["spawns"].append(spawn(f"fab_macaco_{len(usados)}", "monster_fab_macaco_coqueiro", round(x, 2), round(y, 2), 19 + len(usados) % 3, 60, perch=3.0, fab=True))
                break
    print("  mata: macacos", len(usados))
    D("mata_dos_enforcados", m)


def canions():
    m = L("canions_suplicantes"); limpar(m); rnd = random.Random("dentucos")
    for i in range(6):
        x, y = perto(m, -37 + rnd.uniform(-7, 7), 46 + rnd.uniform(-6, 6), rnd, 1.3, 5)
        m["spawns"].append(spawn(f"fab_dentuco_{i}", "monster_fab_dentuco", x, y, 23 + i % 3, 50, grupo=f"fab_dent_{i // 2}", fab=True))
    x, y = perto(m, -44, 52, rnd, 1.8, 5)
    m["spawns"].append(spawn("fab_dentuco_pai", "monster_fab_dentuco_pai", x, y, 27, 240, fab=True))
    D("canions_suplicantes", m)


def floresta():
    m = L("floresta_oculta"); limpar(m); rnd = random.Random("urtiga")
    m["zones"].append({"id": "fab_jardim_espinhos", "name": "Jardim de Espinhos: a Dama Urtiga", "kind": "chefe", "x": 30, "y": -30, "w": 16, "h": 14, "fab": True})
    x, y = perto(m, 30, -31, rnd, 1.6, 4)
    m["spawns"].append(spawn("fab_dama_urtiga", "monster_fab_dama_urtiga", x, y, 23, 600, fab=True))
    for i in range(6):
        x, y = perto(m, 30 + rnd.uniform(-9, 9), -24 + rnd.uniform(-5, 5), rnd, 1.2, 4)
        m["spawns"].append(spawn(f"fab_broto_{i}", "monster_fab_broto_urtiga", x, y, 21, 45, grupo=f"fab_brotos_{i // 2}", fab=True))
    D("floresta_oculta", m)


def transilvania():
    m = L("reino_transilvania"); limpar(m); rnd = random.Random("vlakor")
    m["zones"].append({"id": "fab_mausoleu", "name": "Mausoléu dos Vlakor", "kind": "perigoso", "x": -32, "y": -4, "w": 16, "h": 14, "fab": True})
    for mid, lvl, (x, y) in [("monster_fab_barao_vlakor", 37, (-32, -8)), ("monster_fab_baronesa_vlakor", 37, (-29, -4)),
                             ("monster_fab_gemeo_vlakor_a", 36, (-35, -1)), ("monster_fab_gemeo_vlakor_b", 36, (-30, 1))]:
        x, y = perto(m, x, y, rnd, 1.3, 4)
        m["spawns"].append(spawn("fab_" + mid[12:], mid, x, y, lvl, 240, fab=True))
    P = m["props"]
    for (x, y) in [(-38, -10), (-26, -10), (-38, 2), (-26, 2)]:
        if livre(m, x, y, 1.0): P.append({"kind": "lapide", "x": x, "y": y, "yaw": rnd.randrange(360), "fab": True})
    D("reino_transilvania", m)


def fundo():
    m = L("fundo_triangulo"); limpar(m); rnd = random.Random("nereu")
    m["zones"].append({"id": "fab_recife", "name": "Recife Envenenado: Nereu, o Tritão", "kind": "chefe", "x": -36, "y": -38, "w": 20, "h": 14, "fab": True})
    x, y = perto(m, -36, -38, rnd, 1.6, 5)
    m["spawns"].append(spawn("fab_tritao", "monster_fab_tritao_corrompido", x, y, 54, 600, fab=True))
    for i, (fx, fy) in enumerate([(-44, -34), (-30, -42), (-40, -44)], 1):
        fx, fy = perto(m, fx, fy, rnd, 1.0, 4)
        m["objects"].append({"id": f"fab_fonte_lodo_{i}", "kind": "reliquia", "x": fx, "y": fy, "label": "Fonte de lodo negro (tapar)", "quest": "q_fab_tritao_salvar",
                             "text": "Você empurra pedras e coral sobre a fenda. O lodo para de subir. A água em volta clareia um pouco."})
        m["props"].append({"kind": "cranios", "x": round(fx + 1.2, 1), "y": fy, "yaw": 0, "fab": True})
    tx, ty = perto(m, -34, -36, rnd, 1.0, 3)
    m["objects"].append({"id": "fab_tritao_caido", "kind": "reliquia", "x": tx, "y": ty, "label": "Nereu, caído no recife", "quest": "q_fab_tritao_acabar",
                         "text": "Nereu abre os olhos, entende e fecha de novo. Um último suspiro de bolhas sobe para a superfície."})
    m["npcs"].append(npc("fab_nereu_curado", "Nereu, o Tritão (curado)", -10, 42, "guarda",
                         "Eu lembro de tudo. Das guelras queimando. De você. Obrigado. O recife é seu também, agora.", 200, role="morador", requires_flag="fab:tritao_salvo", fab=True))
    D("fundo_triangulo", m)


def porto_e_mundo():
    m = L("porto_das_cinzas")
    # a Sibila só leva aos reinos 35+; o Era Uma Vez (24+) é com o Tião — bobo leva ao Castelo do Riso, faz sentido
    sib = next(n for n in m["npcs"] if n["id"] == "sibila_vidente")
    sib["travel"] = [t for t in sib["travel"] if t["id"] != "reino_fabula"]
    tiao = next(n for n in m["npcs"] if n["id"] == "bobo_tiao")
    tiao["travel"] = [t for t in tiao.get("travel", []) if t["id"] != "reino_fabula"] + [
        {"id": "reino_fabula", "label": "Seguir o Tião para dentro do Livro dos Contos — Era Uma Vez (nível 24+)", "to_map": "reino_fabula", "to_x": 0.0, "to_y": 60.0,
         "price": 400, "min_level": 24}]
    D("porto_das_cinzas", m)
    wp = os.path.join(C, "world.json")
    w = json.load(open(wp, encoding="utf-8"))
    if "map_reino_fabula.json" not in w["map_files"]: w["map_files"].append("map_reino_fabula.json")
    for r in w["regions"]:
        if r["id"] == "reinos" and "reino_fabula" not in r["maps"]:
            r["maps"].append("reino_fabula")
            r["description"] = r["description"].replace("e a Floresta Oculta.", "a Floresta Oculta e o Reino dos Contos (Era Uma Vez).")
    json.dump(w, open(wp, "w", encoding="utf-8"), ensure_ascii=False, indent=2)


def main():
    W("monsters_fabula.json", MONSTROS)
    W("behaviors_fabula.json", COMPORTAMENTOS)
    W("items_fabula.json", ITENS)
    W("loot_fabula.json", SAQUE)
    W("quests_fabula.json", MISSOES)
    caminho = os.path.join(C, "map_reino_fabula.json")
    json.dump(reino(), open(caminho, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    ARQ["reino_fabula"] = caminho
    mata(); canions(); floresta(); transilvania(); fundo(); porto_e_mundo()
    print("fábula: ok")


main()

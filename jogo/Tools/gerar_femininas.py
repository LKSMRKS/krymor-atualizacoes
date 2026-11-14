#!/usr/bin/env python3
"""
13/11 (pedido do dono: "vamos colocar mobs femininas: harpias, vampiras, esfinge, medusa"): quatro inimigas no NOSSO
manequim (corpo feminino; visual em VisualFactory: harpy_f, vampire_f, medusa, sphinx), cada uma com golpes próprios que
aplicam os efeitos novos (garra = sangramento, grito/sombra = maldição, olhar = petrifica, raio solar = queimadura).
  - Harpia (Cânions dos Suplicantes, 26–32): voa baixo, mergulha com as garras, grito que amaldiçoa.
  - Medusa (Brejo da Viúva, 33–39): cuspe venenoso, olhar que petrifica por um instante, chicote da cauda.
  - Vampira (Véspera, 40–46): avanço rápido, mordida que sangra, sombra amaldiçoada à distância.
  - Esfinge (Acrópole, 57–60, ELITE): garras, rugido que atordoa e raio solar que queima.
Escreve monsters_femininas.json, behaviors_femininas.json, loot_femininas.json e põe os pontos nos bolsões certos.
Idempotente (spawns com id "fem_*").
"""
import json, os, glob, math, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}

def atk(id, shape, rng, wu, act, rec, cd, dm, **kw):
    a = {"id": id, "shape": shape, "range": rng, "windup_ms": wu, "active_ms": act, "recovery_ms": rec, "cooldown_ms": cd, "damage_mult": dm}
    a.update(kw); return a

BEHAVIORS = [
    {"id": "harpy_dive", "move_speed": 5.2, "aggro_radius": 15, "leash_radius": 30, "exposed_mult": 1.35, "attacks": [
        atk("garras_harpia", "cone", 2.3, 450, 160, 700, 600, 0.9, arc=80, track_fraction=0.6),
        atk("mergulho_garra", "lunge", 7.0, 650, 300, 900, 4200, 1.4, min_range=3.0, radius=0.8, lunge_m=6.0, track_fraction=0.55),
        atk("grito_harpia", "circle", 3.4, 700, 150, 900, 7000, 0.6, radius=3.4, offset=0, track_fraction=0)]},
    {"id": "medusa_gaze", "move_speed": 3.4, "aggro_radius": 13, "leash_radius": 26, "exposed_mult": 1.3, "attacks": [
        atk("cauda_chicote", "cone", 2.8, 550, 180, 750, 700, 1.0, arc=120, track_fraction=0.5),
        atk("cuspe_medusa", "projectile", 11, 700, 100, 700, 2400, 0.8, radius=0.4, projectile_speed=11, track_fraction=0.85),
        atk("olhar_de_pedra", "beam", 10, 1300, 600, 1000, 9000, 0.5, radius=0.6, hits=2, hit_interval_ms=280, track_fraction=0.7, min_range=2,
            status="atordoado", status_ms=1100)]},
    {"id": "vampire_f", "move_speed": 4.8, "aggro_radius": 14, "leash_radius": 28, "exposed_mult": 1.35, "attacks": [
        atk("dentada_vampira", "cone", 2.1, 420, 150, 650, 500, 1.0, arc=70, track_fraction=0.65),
        atk("avanco_sombrio", "lunge", 6.5, 500, 250, 800, 3800, 1.2, min_range=2.8, radius=0.7, lunge_m=5.5, track_fraction=0.6),
        atk("sombra_vampirica", "projectile", 12, 800, 100, 800, 3200, 0.9, radius=0.5, projectile_speed=12, track_fraction=0.8)]},
    {"id": "sphinx", "move_speed": 3.8, "aggro_radius": 15, "leash_radius": 30, "exposed_mult": 1.25, "attacks": [
        atk("garras_esfinge", "cone", 3.0, 550, 180, 800, 700, 1.1, arc=100, track_fraction=0.55),
        atk("rugido_enigma", "circle", 4.2, 900, 150, 1000, 8000, 0.7, radius=4.2, offset=0, track_fraction=0, status="atordoado", status_ms=900),
        atk("raio_solar", "beam", 12, 1200, 900, 1100, 6000, 0.9, radius=0.6, hits=3, hit_interval_ms=300, track_fraction=0.75, min_range=2,
            status="queimadura", status_ms=3000)]},
]

# (id, nome, visual, behavior, mapa, nível, quantos, elite, material)
INIMIGAS = [
    ("monster_harpia", "Harpia dos Cânions", "harpy_f", "harpy_dive", "canions_suplicantes", 29, 5, False, "mat_pena_harpia", "Pena de Harpia"),
    ("monster_medusa", "Medusa do Brejo", "medusa", "medusa_gaze", "brejo_da_viuva", 36, 4, False, "mat_escama_medusa", "Escama de Medusa"),
    ("monster_vampira", "Vampira de Véspera", "vampire_f", "vampire_f", "vespera", 43, 5, False, "mat_presa_vampira", "Presa de Vampira"),
    ("monster_esfinge", "Esfinge da Acrópole", "sphinx", "sphinx", "acropole_das_cinzas", 58, 2, True, "mat_po_esfinge", "Pó Dourado da Esfinge"),
]

def main():
    monstros, loots, itens = [], [], []
    for mid, nome, vis, beh, mapa, lv, n, elite, mat, matnome in INIMIGAS:
        monstros.append({"id": mid, "species_name": nome, "archetype": "combatente", "level": lv, "resistances": {}, "behavior_id": beh,
                         "loot_table_id": "loot_" + mid, "visual_asset_id": vis, "health_mult": 3.2 if elite else 1.15, "damage_mult": 1.6 if elite else 1.15,
                         "xp_mult": 3.0 if elite else 1.25, "radius": 0.6 if not elite else 0.9, "coins_min": lv * 3, "coins_max": lv * 6,
                         "boss": False, "elite": elite})
        itens.append({"id": mat, "display_name": matnome, "category": "material", "subtype": "material", "min_level": 1, "allowed_classes": [],
                      "handedness": 0, "base_stats": {}, "affix_pool": "none", "temperable": False, "trade_rule": "free", "stack_limit": 99,
                      "icon_asset_id": "ico_" + mat, "model_asset_id": "item_" + mat, "price": 4 * lv,
                      "summary": f"Cai de {nome}. Material raro: vale bom dinheiro no mercador.", "source": f"{nome} ({mapa.replace('_', ' ')})."})
        loots.append({"id": "loot_" + mid, "entries": [
            {"chance": 0.5 if not elite else 1.0, "item": mat},
            {"chance": 0.12, "item": "consumable_bandagem" if "harpia" in mid or "vampira" in mid else "consumable_agua_benta"},
            {"chance": 0.04 if not elite else 0.25, "item": "consumable_panaceia"}]})
        # pontos: no bolsão mais perto do nível, espalhados
        f = ARQ[mapa]
        m = json.load(open(f, encoding="utf-8"))
        m["spawns"] = [s for s in m["spawns"] if not s["id"].startswith("fem_" + mid)]
        bols = [z for z in m["zones"] if z["kind"] in ("fraco", "medio", "forte", "perigoso")]
        rnd = random.Random(mid)
        alvo = sorted(bols, key=lambda z: {"fraco": 0, "medio": 1, "forte": 2, "perigoso": 3}[z["kind"]])[-2 if len(bols) > 1 and not elite else -1]
        feitos = 0
        for _ in range(400):
            if feitos >= n: break
            x = alvo["x"] + rnd.uniform(-alvo["w"] * 0.42, alvo["w"] * 0.42); y = alvo["y"] + rnd.uniform(-alvo["h"] * 0.42, alvo["h"] * 0.42)
            if any((math.hypot(x - o["x"], y - o["y"]) < o["r"] + 1.2) if o.get("shape") == "circle" else (abs(x - o["x"]) < o["w"] / 2 + 1.2 and abs(y - o["y"]) < o["h"] / 2 + 1.2) for o in m["obstacles"]): continue
            if any(math.hypot(x - s["x"], y - s["y"]) < 3 for s in m["spawns"]): continue
            m["spawns"].append({"id": f"fem_{mid}_{feitos}", "monster": mid, "x": round(x, 1), "y": round(y, 1), "level": lv + rnd.randint(-1, 1),
                                "group": f"fem_{mid}", "respawn_seconds": 90 if elite else 40})
            feitos += 1
        json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        print(f"  {nome}: {feitos} em {alvo['name']} ({mapa})")
    for nome, dados in (("monsters_femininas.json", monstros), ("behaviors_femininas.json", BEHAVIORS), ("loot_femininas.json", loots), ("items_femininas.json", itens)):
        json.dump(dados, open(os.path.join(C, nome), "w", encoding="utf-8"), ensure_ascii=False, indent=2)

main()

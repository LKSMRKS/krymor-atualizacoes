#!/usr/bin/env python3
"""
Simulação da progressão (E9). Usa as MESMAS fórmulas do servidor (Server/Combat/CombatFormulas.cs, Progression.cs,
MonsterScaling) e os dados de Content/*.json. Não é jogo: é estimativa de ritmo para escolher números.

Modelo por abate: tempo para matar (DPS real do ataque básico + crítico, contra armadura) + deslocamento/procura +
recuperação de vida (dano recebido / regeneração fora de combate), com uma taxa de acerto dos monstros que representa
um jogador mediano (esquiva parte das preparações). XP = 100 x nível do monstro x multiplicadores, com o fator de
diferença de nível do GDD. O jogador farma onde o ritmo de XP é melhor entre as áreas que já consegue enfrentar.

Uso: python3 Tools/simular_progressao.py [--classe guerreiro|mago] [--acerto 0.5]
"""
import json, math, os, sys, argparse

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
def C(f):
    """Lê Content/f e junta os extras nome_*.json (como o ContentLoader faz: ex. items_midgame.json)."""
    data = json.load(open(os.path.join(ROOT, "Content", f), encoding="utf-8"))
    if isinstance(data, list):
        base = f[:-5]
        for extra in sorted(os.listdir(os.path.join(ROOT, "Content"))):
            if extra.startswith(base + "_") and extra.endswith(".json"):
                data = data + json.load(open(os.path.join(ROOT, "Content", extra), encoding="utf-8"))
    return data

ARCH = {"combatente": (1.0, 1.0, 1.0), "agil": (0.7, 0.8, 1.4), "tanque": (1.7, 0.8, 0.7),
        "atirador": (0.85, 1.0, 1.0), "conjurador": (0.8, 1.2, 1.0), "enxame": (0.35, 0.45, 1.0)}

def k(level): return 60 if level <= 19 else 150 if level <= 39 else 280
CURVE_A, CURVE_B = 30, 18   # E9 (modo mapa): Progression.MapXpToNext = 100 x L x (A + B x L)
def xp_to_next(level): return 100 * level * (CURVE_A + CURVE_B * level)
def level_factor(pl, ml):
    # E9 (modo mapa): Progression.MapLevelDifferenceFactor
    below = pl - ml
    return {3: 0.8, 4: 0.6, 5: 0.45, 6: 0.3}.get(below, 1.0 if below <= 2 else 0.15)
def max_health(level, vigor, bonus=0): return 180 + 14 * level + 9 * vigor + bonus
def power(weapon, primary, level): return weapon + 2.2 * primary + 1.2 * level
def armor_red(armor, attacker_level):
    return 0 if armor <= 0 else min(armor / (armor + 180 + 18 * attacker_level), 0.65)
def gap_player(pl, ml):
    g = ml - pl
    return 1.0 if g <= 0 else max(0.3, 1 - 0.12 * g)
def gap_monster(ml, pl):
    g = ml - pl
    return min(2.5, 1 + 0.15 * g) if g >= 0 else max(0.5, 1 + 0.08 * g)
def mon_profile(level, arch):
    h, d, s = ARCH[arch]
    m = level
    return (180 + 18 * m + 0.45 * m * m) * h, (12 + 2 * m + 0.12 * m * m) * d, 20 + 5 * m

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--classe", default="guerreiro")
    ap.add_argument("--acerto", type=float, default=0.45, help="fração das preparações que acertam o jogador")
    ap.add_argument("--ate", type=int, default=13)
    a = ap.parse_args()

    classes = {c["id"]: c for c in C("classes.json")}
    skills = {s["id"]: s for s in C("skills.json")}
    items = {i["id"]: i for i in C("items.json")}
    monsters = {m["id"]: m for m in C("monsters.json")}
    behaviors = {b["id"]: b for b in C("behaviors.json")}
    world = C("world.json")
    maps = [C(f) for f in world["map_files"]]
    prog = C("progressao_simulada.json")  # equipamento esperado por nível e áreas (dados só da simulação)
    cls = classes[a.classe]

    # áreas: (nome, mapa, multiplicador do mapa, lista de (monstro, nível)) a partir dos mapas reais
    areas = []
    for mp in maps:
        if not mp.get("spawns"): continue   # casa do jogador
        if mp.get("instanced"):
            # dungeon: uma "volta" inteira (todos os monstros, sem respawn) + entrada/saída
            areas.append((f'{mp["display_name"]} (volta completa)', mp["xp_multiplier"],
                          [s for s in mp["spawns"]], True, "dungeon"))
            continue
        for z in mp.get("zones", []):
            if z["kind"] in ("seguro", "caminho", "segredo"): continue
            inside = [s for s in mp["spawns"] if z["x"] - z["w"] / 2 <= s["x"] <= z["x"] + z["w"] / 2
                      and z["y"] - z["h"] / 2 <= s["y"] <= z["y"] + z["h"] / 2 and s.get("chance", 1) >= 1]
            if not inside: continue
            areas.append((f'{mp["display_name"]} / {z["name"]}', mp["xp_multiplier"], inside, mp.get("instanced", False),
                          z["kind"]))

    level, xp, t = 1, 0, 0.0
    primary, vigor = cls["starting_primary"], cls["starting_vigor"]
    print(f"Classe {cls['display_name']}, acerto dos monstros {a.acerto:.0%}\n")
    print(f"{'nv':>3} {'min no nv':>9} {'total':>8}  {'kills':>5}  área escolhida (ttk s / dano por abate / xp)")
    while level < a.ate:
        keys = sorted(int(x) for x in prog["equipamento"][a.classe])
        gear = prog["equipamento"][a.classe][str(max(x for x in keys if x <= level))]
        weapon = items[gear["arma"]]["base_stats"] if gear.get("arma") else None
        wpow = weapon["weapon_power"] if weapon else 4
        interval = weapon.get("attack_interval_ms", 1000) if weapon else 1000
        basic = skills[cls["loadout"]["basic"] if weapon else cls["unarmed_basic"]]
        cycle = (basic["cast_ms"] + basic["recovery_ms"]) * (interval / 1000.0 if basic["effects"].get("weapon_speed") else 1.0)
        cycle += prog["pausa_entre_golpes_ms"]
        armor = sum(items[x]["base_stats"].get("armor", 0) for x in gear.get("armaduras", []))
        hp_bonus = sum(items[x]["base_stats"].get("max_health", 0) for x in gear.get("armaduras", []))
        hp = max_health(level, vigor, hp_bonus)
        pw = power(wpow, primary, level)

        best = None
        for name, mult, spawns, inst, kind in areas:
            ttk_sum = dmg_sum = xp_sum = 0.0
            ok = True
            resp_limit = 0.0
            for sp in spawns:
                mdef = monsters[sp["monster"]]
                mh, md, marm = mon_profile(sp["level"], mdef["archetype"])
                mh *= mdef.get("health_mult", 1)
                beh = behaviors[mdef["behavior_id"]]
                atk = beh["attacks"][0]
                red = armor_red(marm, level)
                hit = pw * basic["coefficient"] * (1 - red) * (1 + 0.05 * 0.5) * gap_player(level, sp["level"])
                dps = hit / (cycle / 1000.0) * (1.0 if basic["target_mode"] != "projectile" else 0.85)
                ttk = mh / dps
                period = (atk["windup_ms"] + atk["active_ms"] + atk["recovery_ms"] + atk["cooldown_ms"]) / 1000.0
                mdmg = md * mdef.get("damage_mult", 1) * atk["damage_mult"] * (1 - armor_red(armor, sp["level"])) * gap_monster(sp["level"], level)
                taken = (ttk / period) * a.acerto * mdmg
                elite = mdef.get("boss") or mdef.get("elite")
                if taken > hp * (1.3 if elite else 0.6): ok = False   # elite/chefe: com tônicos; comum: até 60% da vida
                if not inst: resp_limit = max(resp_limit, sp.get("respawn_seconds", 30) / len(spawns))
                ttk_sum += ttk; dmg_sum += taken
                xp_sum += 100 * sp["level"] * mult * mdef.get("xp_mult", 1) * level_factor(level, sp["level"])
            if not ok: continue
            n = len(spawns)
            ttk, dmg, xpk = ttk_sum / n, dmg_sum / n, xp_sum / n
            travel = prog["deslocamento_s"]
            regen = dmg / (hp * 0.01) * prog["fracao_recuperacao_parada"]   # 1%/s fora de combate
            per_kill = max(ttk + travel + regen, resp_limit)  # não dá para matar mais rápido do que reaparecem
            if inst: per_kill += prog["entrada_dungeon_s"] / n
            rate = xpk / per_kill
            if best is None or rate > best[0]: best = (rate, name, ttk, dmg, xpk, per_kill)
        if best is None:
            print("nenhuma área viável"); return
        rate, name, ttk, dmg, xpk, per_kill = best
        need = xp_to_next(level) - xp
        secs = need / rate
        t += secs
        kills = math.ceil(need / xpk)
        print(f"{level:>3} {secs/60:>9.1f} {t/60:>7.0f}m  {kills:>5}  {name} ({ttk:.1f} s / {dmg:.0f} de {hp:.0f} / {xpk:.0f})")
        level += 1
        xp = 0
        # pontos: 2 no principal, 1 no vigor (jogador típico)
        primary += 2; vigor += 1

if __name__ == "__main__":
    main()

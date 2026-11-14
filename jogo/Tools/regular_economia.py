#!/usr/bin/env python3
"""
14/11 (pedido do dono: "itens, missões, fabricados, lojinha — tudo progressivo, matematicamente, de acordo com o nível; o
item tem que valer o que cobra. Todo o jogo deve seguir essa premissa: itens, drops"). Uma régua só para a economia:

  MOEDA POR ABATE  C(L) = 3 + 2,2·L + 0,025·L²            (monstro comum; mín. 70% e máx. 130% de C)
                   × vida do monstro^0,6 × (elite 2,5 · raro 5 · chefe 8)
  ARMA DE REFERÊNCIA  P(L) = C(L) × (8 + 4,5·L)            (≈ abates de farm no próprio nível para comprar a arma da faixa)
  PREÇO DO ITEM    P(L) × peso do espaço × força^1,5 × (1 + extras) × (conjunto 1,25 · lendário 1,6)
                   força = atributo principal ÷ curva do nível (a mesma do PisoDeNivel, 100%); extras = % de poder,
                   crítico, velocidade... convertidos em valor
  VENDA            1/4 do preço (Venda.cs multiplica pela qualidade, variação e refino)
  MISSÃO           moedas = C(L) × trabalho (mate N: N+6 · chefe: 25 · coleta: 0,8N+6 · caçada/defesa: 14 ·
                   conversa/visita: 4 ...); repetível ×0,6
  FABRICAÇÃO       só relatório: valor dos materiais ÷ valor do que sai (bom: 0,3 a 1,5)

Itens sem preço (só de drop / missão) ganham VALOR (para vender e desmanchar); entrar na loja continua sendo só pela
lista da loja. Cosméticos (bracelete, mascote, montaria) seguem a mesma régua como luxo. Idempotente: tudo sai da
fórmula; rodar de novo dá o mesmo resultado. Rodar por último (gerar_tudo.py). Uso: python3 Tools/regular_economia.py [--relatorio]
"""
import json, os, glob, sys, math, collections

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
RELATORIO = "--relatorio" in sys.argv


def moeda(L):
    return 3 + 2.2 * L + 0.025 * L * L


def ref(L):
    return moeda(L) * (8 + 4.5 * L)


PESO = {"weapon": 1.0, "offhand": 0.6, "armor": 0.8, "helmet": 0.45, "pants": 0.5, "gloves": 0.35, "boots": 0.4,
        "ring": 0.7, "amulet": 0.8, "cape": 0.9, "wings": 1.5, "relic": 1.0, "bracelet": 1.0, "pet": 0.8, "mount": 2.0}
EXTRA = {"power_pct": 6, "crit_pct": 4, "double_pct": 4, "move_speed_pct": 3, "dodge_pct": 4, "damage_reduction_pct": 5,
         "resource_regen_pct": 1.5, "xp_pct": 5, "attack_speed_pct": 5, "cdr_pct": 4, "thorns_pct": 3, "hp_regen_pct": 40,
         "magic_resist_pct": 3, "tenacity_pct": 2, "hit_pct": 3, "lifesteal_pct": 6, "heal_on_kill_pct": 4,
         "healing_received_pct": 2, "mana_cost_pct": 3, "max_health_pct": 6, "armor_pct": 4, "damage_pct": 6}
LUXO = ("bracelet", "pet", "mount")
JOIAS_PRECO = {"joia_mare": 1500, "joia_brasa": 6000, "joia_vazio": 25000, "joia_forja": 40000}
PISO_PRECO = {"bracelet": 1200, "pet": 400, "mount": 5000, "wings": 3000, "cape": 400}


def curva_arma(d, L):
    s = d["base_stats"]
    iv = s.get("attack_interval_ms", 1000) or 1000
    conj = d.get("subtype") in ("staff", "talisman", "wand", "orb")
    setas = max(1, s.get("projectile_count", 1))
    return ((2.2 if conj else 2.6) * L + 8) * iv / 1000 / setas


def curva_armadura(d, L):
    w = {"armor": (1.0, 1.0), "helmet": (0.35, 0.55), "pants": (0.5, 0.45), "gloves": (0.33, 0.3), "boots": (0.4, 0.35)}.get(d["category"])
    if not w: return None
    cls = d.get("allowed_classes") or []
    pesada = d.get("subtype") == "plate" or (cls and all(c == "guerreiro" for c in cls))
    leve = d.get("subtype") in ("robe", "cloth") or (cls and all(c in ("mago", "bruxa") for c in cls))
    a = (14.3 * L + 30) if pesada else (6.7 * L + 30) if leve else (9.7 * L + 40)
    return a * w[0], (3 * L + 80) * w[1]


def forca(d):
    L = max(1, d.get("min_level", 1)); s = d.get("base_stats", {}); cat = d.get("category")
    if cat == "weapon" and "weapon_power" in s:
        alvo = curva_arma(d, L)
        return max(s["weapon_power"], round(alvo * 0.9)) / alvo if L >= 3 else s["weapon_power"] / alvo
    ca = curva_armadura(d, L)
    if ca and "armor" in s:
        a = max(s["armor"], round(ca[0] * 0.9)) if L >= 3 else s["armor"]
        return a / ca[0]
    if cat == "offhand" and "armor" in s:
        return s["armor"] / max(1.0, 14.3 * L * 0.6 + 20)
    return 1.0


def extras(d):
    s = d.get("base_stats", {}); L = max(1, d.get("min_level", 1))
    e = sum(abs(v) * EXTRA.get(k, 0) for k, v in s.items() if k.endswith("_pct") and k not in ("armor_pct",) and v > 0)
    if d["category"] in ("weapon", "ring", "amulet", "cape", "wings", "relic", "bracelet", "pet", "mount"):
        e += s.get("max_health", 0) / (3 * L + 80) * 0.5 + s.get("armor", 0) / (9.7 * L + 40) * 0.5
    return min(e, 2.0)


def preco_item(d):
    cat = d.get("category")
    if cat not in PESO: return None
    L = max(1, d.get("min_level", 1))
    r = min(2.2, max(0.85, forca(d)))
    mult = 1.0
    i = d["id"]
    if d.get("set"): mult *= 1.25
    if i.startswith(("lendaria_", "legend_")) or "lendari" in (d.get("source") or "").lower(): mult *= 1.6
    p = ref(L) * PESO[cat] * r ** 1.5 * (1 + extras(d)) * mult
    p = max(PISO_PRECO.get(cat, 10), p)
    # números redondos (loja de verdade): 2 algarismos significativos acima de 100
    if p >= 100:
        mag = 10 ** (int(math.log10(p)) - 1)
        p = round(p / mag) * mag
    return int(round(p))


def carregar(prefixo):
    out = []
    for f in sorted(glob.glob(os.path.join(C, prefixo + "*.json"))):
        d = json.load(open(f, encoding="utf-8"))
        if isinstance(d, list): out.append((f, d))
    return out


def salvar(f, d):
    json.dump(d, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    open(f, "a", encoding="utf-8").write("\n")


def main():
    rel = []
    # ---------------------------------------------------------------- moedas dos monstros
    nivel_monstro = {}
    for f, ms in carregar("monsters"):
        mudou = False
        for m in ms:
            nivel_monstro[m["id"]] = m.get("level", 1)
            if m.get("coins_max", 0) <= 0: continue
            k = max(0.5, min(3.0, m.get("health_mult", 1.0))) ** 0.6
            k *= 8 if m.get("boss") else 5 if m.get("rare") else 2.5 if m.get("elite") else 1
            c = moeda(m.get("level", 1)) * k
            lo, hi = max(1, round(c * 0.7)), max(2, round(c * 1.3))
            if (m["coins_min"], m["coins_max"]) != (lo, hi):
                m["coins_min"], m["coins_max"] = lo, hi; mudou = True
        if mudou and not RELATORIO: salvar(f, ms)
    # ---------------------------------------------------------------- preços dos itens
    itens = {}
    mudados = 0
    fora = []
    for f, its in carregar("items"):
        mudou = False
        for d in its:
            itens[d["id"]] = d
            novo = preco_item(d)
            if d["id"] in JOIAS_PRECO: novo = JOIAS_PRECO[d["id"]]   # 14/11: joia é a "moeda forte" (MU); na loja do fim do jogo, cara
            if novo is None: continue
            velho = d.get("price", 0)
            if d.get("category") in LUXO and velho > novo: novo = velho   # cosmético: a aparência também vale (nunca abaixo da régua)
            if velho and (novo / velho > 2.5 or velho / novo > 2.5): fora.append((d["id"], d.get("min_level"), velho, novo))
            if velho != novo:
                d["price"] = novo; mudou = True; mudados += 1
        if mudou and not RELATORIO: salvar(f, its)
    rel.append(f"itens: {mudados} preços pela régua; {len(fora)} estavam a mais de 2,5x da régua")
    for x in sorted(fora, key=lambda t: t[1] or 0)[:40]: rel.append(f"   {x[0]} nv{x[1]}: {x[2]} -> {x[3]}")
    # ---------------------------------------------------------------- missões
    mapa_nivel = {}
    for f in glob.glob(os.path.join(C, "map_*.json")):
        m = json.load(open(f, encoding="utf-8"))
        lv = [nivel_monstro.get(s["monster"], 0) for s in m.get("spawns", [])]
        if lv: mapa_nivel[m["id"]] = sorted(lv)[len(lv) // 2]
    trab = {"kill": None, "boss": 25, "collect": None, "hunt": 14, "defend": 14, "interact": 5, "talk": 4, "visit": 4,
            "craft": 8, "mine": None, "harvest": None, "escort": 12, "use": 4, "evolve": 20, "reset": 4}
    nq = 0
    for f, qs in carregar("quests"):
        mudou = False
        for q in qs:
            if "reward_coins" not in q or q.get("cost_coins") or q.get("reward_coins", 0) == 0: continue
            alvos = [nivel_monstro[t] for t in q.get("targets") or [] if t in nivel_monstro]
            L = max(alvos) if alvos and q["kind"] in ("kill", "boss", "hunt", "defend") else q.get("min_level") or mapa_nivel.get(q.get("map"), 1)
            L = max(1, L)
            n = max(1, q.get("count", 1))
            t = trab.get(q["kind"])
            if t is None: t = n + 6 if q["kind"] == "kill" else 0.8 * n + 6
            if q.get("repeatable") or q.get("daily") or q.get("board"): t *= 0.6
            novo = max(5, int(round(moeda(L) * t / 5.0)) * 5)
            if novo != q["reward_coins"]: q["reward_coins"] = novo; mudou = True; nq += 1
        if mudou and not RELATORIO: salvar(f, qs)
    rel.append(f"missões: {nq} recompensas em moedas pela régua")
    # ---------------------------------------------------------------- 14/11: receitas do Caos pela régua
    # Lendária (30%): a arma-base é a do arsenal da MESMA família no degrau do nível (enferrujada 2 · comum 8 · refinada 20 ·
    # excelente 34) e as joias completam até ~50% do valor da lendária (com 30% de chance, a média gasta ≈ o que ela vale).
    # Outras receitas do Caos muito baratas (< 25% do produto) têm as joias multiplicadas até 50%.
    familia = {"axe": "machado_mao", "hammer": "maca", "spear": "lanca", "sword": "espada_curta", "club": "clava", "dagger": "adaga", "bow": "arco_curto"}
    preco = {i: d.get("price", 0) for i, d in itens.items()}
    JOIAS = [("joia_vazio", 5000), ("joia_brasa", 1500), ("joia_mare", 400)]
    nrc = 0
    for f, rs in carregar("recipes"):
        mudou = False
        for r in rs:
            if r.get("station") != "caos": continue
            out = itens.get(r["output"])
            if not out: continue
            V = out.get("price", 0); L = out.get("min_level", 1)
            if r["output"].startswith("lendaria_"):
                base = r["inputs"][0]["item"]
                fam = familia.get(out.get("subtype"))
                if fam:
                    degrau = "excelente" if L >= 34 else "refinada" if L >= 20 else "comum" if L >= 8 else "enferrujada"
                    if f"arsenal_{fam}_{degrau}" in itens: base = f"arsenal_{fam}_{degrau}"
                resto = 0.5 * V - preco.get(base, 0)
                q = {}
                if L >= 30: q["joia_vazio"] = max(1, int(resto * 0.4 // 5000))
                resto -= q.get("joia_vazio", 0) * 5000
                q["joia_brasa"] = max(1, int(resto * 0.55 // 1500)) if L >= 10 else 0
                resto -= q["joia_brasa"] * 1500
                q["joia_mare"] = min(12, max(2, int(resto // 400)))
                resto -= q["joia_mare"] * 400
                if resto > 5000 and L >= 30: q["joia_vazio"] += int(resto // 5000)
                novo = [{"item": base, "qty": 1}] + [{"item": j, "qty": q[j]} for j, _ in JOIAS if q.get(j)]
            else:
                ent = sum(preco.get(x["item"], 0) * x.get("qty", 1) for x in r["inputs"])
                if V <= 0 or ent <= 0 or ent / V >= 0.25: continue
                k = 0.5 * V / ent
                novo = [{"item": x["item"], "qty": max(1, round(x["qty"] * k)) if x["item"].startswith("joia_") else x["qty"]} for x in r["inputs"]]
            if novo != r["inputs"]: r["inputs"] = novo; mudou = True; nrc += 1
        if mudou and not RELATORIO: salvar(f, rs)
    rel.append(f"caos: {nrc} receitas ajustadas pela régua")
    # ---------------------------------------------------------------- fabricação (relatório)
    cons = {}
    for f, cs in carregar("consumables"):
        for c in cs: cons[c["id"]] = c
    def valor(i):
        if i in itens: return itens[i].get("price", 0)
        if i in cons: return cons[i].get("price", 0)
        return 0
    ruins = []
    for f, rs in carregar("recipes"):
        for r in rs:
            out = valor(r["output"]) * r.get("output_qty", 1)
            ent = sum(valor(x["item"]) * x.get("qty", 1) for x in r["inputs"])
            if out > 0 and ent > 0 and (ent / out < 0.15 or ent / out > 3):
                ruins.append(f"   {r['id']}: entra {ent} sai {out} (x{ent / out:.2f})")
    rel.append(f"fabricação: {len(ruins)} receitas fora da faixa (materiais valem muito menos ou muito mais que o produto)")
    rel += ruins[:30]
    print("\n".join(rel))


main()

"""Itens dos 6 conjuntos novos NO FORMATO do items_conjuntos.json do programador (mesmas chaves e categorias),
com armadura/vida/preço/bônus ajustados pela curva dos conjuntos que ele já tem (ajuste em potência do nível)."""
import json, math, sys
SRC = sys.argv[1]; OUT = sys.argv[2]
L = json.load(open(SRC))
P = ["capacete", "peitoral", "luvas", "calca", "botas"]
CAT = {"capacete": "helmet", "peitoral": "armor", "luvas": "gloves", "calca": "pants", "botas": "boots"}
NOME = {"capacete": "Elmo", "peitoral": "Peitoral", "luvas": "Luvas", "calca": "Calça", "botas": "Botas"}
MODELO = {"capacete": "capacete", "peitoral": "peitoral", "luvas": "luvas", "calca": "perneiras", "botas": "botas"}
def fit(pares):   # y = a * lvl^b
    xs = [math.log(l) for l, _ in pares]; ys = [math.log(max(v, 1e-6)) for _, v in pares]
    n = len(xs); mx = sum(xs) / n; my = sum(ys) / n
    b = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / max(1e-9, sum((x - mx) ** 2 for x in xs))
    return lambda lvl: math.exp(my + b * (math.log(max(lvl, 1)) - mx))
dados = {p: {"armor": [], "max_health": [], "price": []} for p in P}
bonus = {k: [] for k in ("2", "3", "4", "5")}
for it in L:
    s = it.get("subtype")
    if s in dados and it.get("set"):
        lv = it["min_level"]
        for k in ("armor", "max_health"):
            if k in it["base_stats"]: dados[s][k].append((lv, it["base_stats"][k]))
        dados[s]["price"].append((lv, it["price"]))
        for n_, b in (it.get("set_bonus") or {}).items(): bonus[n_].append((lv, b))
F = {p: {k: fit(v) for k, v in d.items() if v} for p, d in dados.items()}
def bonus_de(lvl):
    out = {}
    for n_, lst in bonus.items():
        keys = set(k for _, b in lst for k in b); out[n_] = {}
        for k in sorted(keys):
            f = fit([(l, b[k]) for l, b in lst if k in b])
            v = f(lvl); out[n_][k] = round(v) if all(isinstance(b.get(k, 0), int) for _, b in lst) else round(v, 3)
    return out
TODAS = ["guerreiro", "mago", "bruxa", "arqueiro"]
CLASSE_NOME = {"guerreiro": "Guerreiro", "mago": "Mago", "bruxa": "Bruxa", "arqueiro": "Arqueiro"}
SETS = [  # id, nome do conjunto (como sufixo), nível, classes, fonte, exclusivos por peça
 ("armadura_acolchoada", {"capacete": "Capuz Padded", "peitoral": "Gibão Padded", "luvas": "Luvas Padded", "calca": "Calça Padded", "botas": "Botas Padded"},
  1, TODAS, "Brás, o Ferreiro (Porto) e drops comuns da costa.", {"botas": {"move_speed_pct": 0.02}, "luvas": {"attack_speed_pct": 0.02}}),
 ("armadura_couro_bronze", {"capacete": "Elmo Bronzehide", "peitoral": "Peitoral Bronzehide", "luvas": "Luvas Bronzehide", "calca": "Calça Bronzehide", "botas": "Botas Bronzehide"}, 5, TODAS, "Brás, o Ferreiro (Porto) e Saqueadores da Vigília.", {"peitoral": {"thorns_pct": 0.02}, "botas": {"move_speed_pct": 0.03}}),
 ("armadura_soldado_ferro", "do Soldado de Ferro", 12, ["guerreiro"], "Raro: Capataz da Pedreira, Sentinela e Guardião da Galeria.", {"peitoral": {"damage_reduction_pct": 0.02}, "calca": {"stamina_regen_pct": 0.08}}),
 ("armadura_escamas_verdes", "de Escamas Verdes", 18, ["arqueiro", "bruxa"], "Raro: Rainha das Cascas e elites do brejo.", {"botas": {"dodge_pct": 0.03}, "luvas": {"crit_pct": 0.02}}),
 ("armadura_barbaro_lobo", "do Bárbaro Lobo", 24, ["guerreiro", "arqueiro"], "Raro: lobos alfa e chefes da Mata dos Enforcados.", {"luvas": {"attack_speed_pct": 0.05}, "peitoral": {"thorns_pct": 0.04}}),
 ("armadura_cavaleiro_aco", "do Cavaleiro de Aço", 28, ["guerreiro"], "Raro: chefes e elites a partir do nível 28.", {"peitoral": {"damage_reduction_pct": 0.03}, "capacete": {"cdr_pct": 0.03}, "botas": {"move_speed_pct": 0.03}}),
]
ROTULO = {"armor": "Armadura", "max_health": "vida", "move_speed_pct": "velocidade", "attack_speed_pct": "vel. de ataque", "thorns_pct": "espinhos (devolve dano)",
          "damage_reduction_pct": "redução de dano", "stamina_regen_pct": "fôlego", "dodge_pct": "esquiva", "crit_pct": "crítico", "cdr_pct": "recarga"}
saida = []
for sid, nome, lvl, classes, fonte, exc in SETS:
    sb = bonus_de(lvl)
    for p in P:
        st = {"armor": max(3, round(F[p]["armor"](lvl))), "max_health": max(2, round(F[p]["max_health"](lvl)))}
        st.update(exc.get(p, {}))
        txt = ", ".join((f"{ROTULO[k]} +{round(v * 100)}%" if isinstance(v, float) else f"{ROTULO[k]} +{v}") for k, v in st.items())
        txt = txt[0].upper() + txt[1:] + "." + ("" if classes == TODAS else " Só para " + " e ".join(CLASSE_NOME[c] for c in classes) + ".")
        dn = nome[p] if isinstance(nome, dict) else f"{NOME[p]} {nome}"
        saida.append({"id": f"{sid}_{p}", "display_name": dn, "category": CAT[p], "subtype": p, "min_level": lvl, "allowed_classes": classes,
                      "handedness": 0, "base_stats": st, "affix_pool": "armor", "temperable": True, "trade_rule": "free", "stack_limit": 1,
                      "icon_asset_id": f"ico_{sid}_{p}", "model_asset_id": f"{sid}:{MODELO[p]}", "summary": txt,
                      "price": max(20, round(F[p]["price"](lvl))), "source": fonte, "set": "set_" + sid, "set_bonus": sb})
ids_antigos = {it["id"] for it in L}
assert not ids_antigos & {it["id"] for it in saida}, "id repetido!"
json.dump(saida, open(OUT, "w"), ensure_ascii=False, indent=1)
print(len(saida), "itens novos; nenhum id repetido com os", len(ids_antigos), "existentes")
for it in saida[::5]: print(it["id"], it["min_level"], it["base_stats"], it["price"])

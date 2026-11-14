"""Auditoria de balanceamento (14/11): compara TODO item de arma e armadura do Content com as curvas do guia
(Docs/GUIA_BALANCEAMENTO.md) e escreve Docs/auditoria_balanceamento.csv — um item por linha, com o valor atual, o esperado
para o nível/classe/qualidade e a razão. Razão entre 0,85 e 1,20 = dentro; fora disso = revisar.
Não muda nada no jogo. Uso: python Tools/auditar_balanceamento.py"""
import json, os, glob, csv, sys
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
sys.path.insert(0, os.path.join(R, "Tools"))
from gerar_itens_lendarios import dps_base, armadura_peito, vida_peito, FRACAO, RARIDADE

SLOT = {"armor": "peitoral", "helmet": "capacete", "gloves": "luvas", "pants": "calca", "boots": "botas"}

def qualidade(i):
    q = (i.get("quality") or "").lower(); iid = i["id"]
    if "lend" in q or iid.startswith(("legend_", "lendaria_", "arma_lend_", "hist_")): return "lendaria"
    if i.get("set"): return "conjunto"
    for k in ("enferrujada", "comum", "refinada", "excelente"):
        if iid.endswith(k): return k
    return "comum"

def main():
    itens = []
    for f in sorted(glob.glob(os.path.join(R, "Content", "items*.json"))):
        d = json.load(open(f, encoding="utf-8")); d = d if isinstance(d, list) else d.get("items", [])
        for i in d: i["_arquivo"] = os.path.basename(f); itens.append(i)
    linhas = []
    for i in itens:
        L = max(1, int(i.get("min_level", 1))); cls = (i.get("allowed_classes") or ["guerreiro"])[0]; st = i.get("base_stats", {}); q = qualidade(i)
        if i.get("category") == "weapon" and st.get("weapon_power"):
            iv = st.get("attack_interval_ms", 1000)
            dps = st["weapon_power"] / iv * 1000; esp = dps_base(L) * RARIDADE[q]
            linhas.append([i["id"], i["_arquivo"], "arma", cls, L, q, round(dps, 1), round(esp, 1), round(dps / esp, 2), i.get("price", "")])
        elif i.get("category") in SLOT and st.get("armor"):
            pc = SLOT[i["category"]]; fa, fh = FRACAO[pc]
            esp = armadura_peito(cls, L) * fa * RARIDADE[q]
            linhas.append([i["id"], i["_arquivo"], pc, cls, L, q, st["armor"], round(esp, 1), round(st["armor"] / esp, 2), i.get("price", "")])
    os.makedirs(os.path.join(R, "Docs"), exist_ok=True)
    out = os.path.join(R, "Docs", "auditoria_balanceamento.csv")
    with open(out, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh, delimiter=";"); w.writerow(["id", "arquivo", "tipo", "classe", "nivel", "qualidade", "atual (dps/armadura)", "esperado", "razao", "preco"])
        for l in sorted(linhas, key=lambda l: (l[2], l[3], l[4])): w.writerow(l)
    fora = [l for l in linhas if not (0.85 <= l[8] <= 1.20)]
    print(f"{len(linhas)} itens auditados; {len(fora)} fora da faixa 0,85–1,20 -> {out}")
    for l in sorted(fora, key=lambda l: -abs(l[8] - 1))[:25]: print("  %-38s %-9s %-9s nv%2d %-10s razão %.2f" % (l[0], l[2], l[3], l[4], l[5], l[8]))
    return linhas, fora

if __name__ == "__main__":
    main()

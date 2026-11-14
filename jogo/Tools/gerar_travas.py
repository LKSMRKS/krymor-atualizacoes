#!/usr/bin/env python3
"""
12/11 (pedido do dono: "o jogo inteiro é progressão de nível — não chegar num lugar antes da hora"): toda passagem que leva
a uma região/dungeon de faixa MAIS ALTA vira trava (level_lock). Nível = o já marcado na passagem ou (início da faixa do
destino − 1). Voltar para trás nunca trava. Os níveis 1–8 não mudam (a Costa não ganha trava). Idempotente; rodar por último.
"""
import json, glob, os, re
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
w = json.load(open(os.path.join(C, "world.json"), encoding="utf-8"))
faixa = {}
for r in w["regions"]:
    m = re.match(r"(\d+)", str(r["levels"]))
    for mp in r["maps"]:
        faixa[mp] = int(m.group(1)) if m else 0
mudou = []
for f in glob.glob(os.path.join(C, "map_*.json")):
    m = json.load(open(f, encoding="utf-8"))
    alterou = False
    for p in m["portals"]:
        de, para = faixa.get(m["id"], 0), faixa.get(p["to_map"], 0)
        alvo = p.get("min_level") or (para - 1 if para > 1 else 0)
        sobe = para > de or (para == de and p.get("min_level", 0) >= 10)   # mesma faixa: só dungeon/castelo com nível marcado
        if not sobe or alvo < 10 or p.get("level_lock") or p["id"].endswith("_volta"):
            continue
        p["min_level"] = alvo
        p["level_lock"] = True
        alterou = True
        mudou.append((m["id"], p["id"], alvo))
    if alterou:
        json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
for x in sorted(mudou): print(x)

# 12/11 (auditoria): viagens pagas de NPC (Sibila → reinos, Tião → Floresta Oculta, barco ao Vale) também travam no nível
# dos monstros do destino (ler o livro cedo não leva mais a um reino de nível 35)
minmon = {}
for f in glob.glob(os.path.join(C, "map_*.json")):
    mm = json.load(open(f, encoding="utf-8"))
    lv = [s["level"] for s in mm["spawns"] if not s["id"].startswith("chefe_semanal")]
    minmon[mm["id"]] = min(lv) if lv else 0
for f in glob.glob(os.path.join(C, "map_*.json")):
    mm = json.load(open(f, encoding="utf-8"))
    alterou = False
    for n in mm["npcs"]:
        for t in n.get("travel", []):
            alvo = minmon.get(t["to_map"], 0)
            alvo = 10 if t["to_map"] == "vale_das_cinzas" else alvo
            if alvo >= 10 and not t.get("min_level"):
                t["min_level"] = alvo; alterou = True; print("viagem", n["id"], t["id"], "→ nível", alvo)
    if alterou:
        json.dump(mm, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

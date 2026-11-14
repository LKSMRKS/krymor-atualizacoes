#!/usr/bin/env python3
"""
13/11 (pedido do dono: "à direita do portal, logo na saída, tem uma elevação massa, mas em cima não tem nada — nenhum item
pra farmar, nenhum spawn. Não precisa ADD mais, apenas remaneja pra ter aproveitamento maior"):
para cada morro/platô (elevations) dos mapas de campo cujo topo está vazio (sem spawn fixo, sem objeto, sem baú), PUXA para
o topo o que já existe por perto:
  1. o ponto de coleta ("veio": árvore, erva, minério) mais perto (até 30 m) que não esteja em cima de outro morro;
  2. senão, o grupo de monstros comum (não chefe, não elite de evento) mais perto (até 30 m), inteiro, mantendo o arranjo.
Nada é criado. Idempotente (topo já ocupado não mexe). Rodar depois de gerar_elevacoes.py e gerar_vida_cenario.py.
"""
import json, os, glob, math

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")

def no_topo(e, x, y, folga=0.6):
    return abs(x - e["x"]) < e["w"] / 2 - folga and abs(y - e["y"]) < e["h"] / 2 - folga

def livre(m, x, y, r=1.6):
    if any(math.hypot(x - p["x"], y - p["y"]) < 2.0 for p in m.get("props", [])): return False
    if any(abs(x - g["x"]) < g["w"] / 2 + r and abs(y - g["y"]) < g["h"] / 2 + r for g in m.get("gates", [])): return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r: return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r: return False
    return True

def em_morro(m, x, y):
    return any(abs(x - e["x"]) < e["w"] / 2 + e["ramp"] and abs(y - e["y"]) < e["h"] / 2 + e["ramp"] for e in m.get("elevations", []))

def main():
    total = 0
    for f in sorted(glob.glob(os.path.join(C, "map_*.json"))):
        m = json.load(open(f, encoding="utf-8"))
        if not m.get("elevations"): continue
        mexeu = 0
        for e in m["elevations"]:
            fixos = [s for s in m["spawns"] if not s.get("extra") and not s.get("night")]
            if any(no_topo(e, s["x"], s["y"]) for s in fixos): continue
            if any(no_topo(e, o["x"], o["y"]) for o in m.get("objects", [])): continue
            if any(no_topo(e, c["x"], c["y"]) for c in m.get("chests", [])): continue
            # 1) ponto de coleta
            veios = [o for o in m.get("objects", []) if o.get("kind") == "veio" and not em_morro(m, o["x"], o["y"])
                     and math.hypot(o["x"] - e["x"], o["y"] - e["y"]) < 30]
            if veios and livre(m, e["x"], e["y"]):
                v = min(veios, key=lambda o: math.hypot(o["x"] - e["x"], o["y"] - e["y"]))
                v["x"], v["y"] = round(e["x"], 1), round(e["y"], 1)
                mexeu += 1
                continue
            # 2) grupo de monstros comum
            grupos = {}
            for s in m["spawns"]:
                if s.get("perch") or s.get("night") or "chefe" in s["monster"] or "boss" in s["monster"] or s["monster"].endswith("_rei"): continue
                grupos.setdefault(s.get("group", s["id"]), []).append(s)
            perto = []
            for g, ss in grupos.items():
                cx = sum(s["x"] for s in ss) / len(ss); cy = sum(s["y"] for s in ss) / len(ss)
                d = math.hypot(cx - e["x"], cy - e["y"])
                if d < 30 and not em_morro(m, cx, cy) and len(ss) <= 6: perto.append((d, cx, cy, ss))
            if perto:
                _, cx, cy, ss = min(perto, key=lambda t: t[0])
                k = min(1.0, (min(e["w"], e["h"]) / 2 - 1.2) / max(0.1, max(math.hypot(s["x"] - cx, s["y"] - cy) for s in ss)))
                if not all(livre(m, e["x"] + (s["x"] - cx) * k, e["y"] + (s["y"] - cy) * k) for s in ss): continue
                for s in ss:
                    s["x"] = round(e["x"] + (s["x"] - cx) * k, 1); s["y"] = round(e["y"] + (s["y"] - cy) * k, 1)
                mexeu += 1
        if mexeu:
            json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
            print(f"  {m['id']}: {mexeu} topos aproveitados")
            total += mexeu
    print("topos de morro aproveitados:", total)

main()

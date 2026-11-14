#!/usr/bin/env python3
"""
13/11 (pacote de assets do trabalhador: "24 peças de decoração das DGs"): espalha as peças Models/dg/dg_*.glb pela Galeria
Funda, pela Biblioteca do Continente e pela Cripta do Sino. Idempotente: tira as peças "glb:dg/" antes de pôr de novo.
Regras: peça de parede (escora, estante, ossuário, lápides...) encostada na parede, virada para dentro; peça solta no
chão longe de portal, baú, objeto e spawn; nada no meio do corredor. Peças grandes bloqueiam (campo "solid").
Rodar depois de gerar_biblioteca.py e do editor (o editor pode mexer nelas depois, à mão).
"""
import json, os, glob, math, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}

# (peça, quantas, encostada na parede?, raio que bloqueia, perto de que espécie/ponto)
PLANO = {
  "galeria_funda": [("escora_mina", 12, True, 0.35, None), ("lanterna_mina", 6, True, 0, None), ("cristais_cal", 6, True, 0, None),
                    ("estalagmites", 6, False, 0.6, None), ("entulho", 5, True, 0, None), ("ferramentas_mina", 3, True, 0, None),
                    ("esqueleto_mineiro", 3, True, 0, None), ("casulos_larva", 3, True, 0, "larva"), ("portao_chefe", 1, True, 0, "chefe")],
  "biblioteca_continente": [("estante_livros", 4, True, 0.6, None), ("pilha_livros", 6, True, 0, None), ("candelabro", 6, True, 0, None),
                    ("atril", 2, False, 0.3, None), ("astrolabio", 1, False, 0.4, None), ("estatua_sabio", 2, False, 0.5, None),
                    ("coluna_gotica", 2, False, 0.45, None), ("mesa_estudo", 2, False, 0.7, None)],
  "cripta_do_sino": [("parede_ossuario", 8, True, 0, None), ("lapides", 6, True, 0, None), ("urnas", 6, True, 0, None),
                    ("correntes", 4, True, 0, None), ("sarcofago", 3, False, 0.9, None), ("altar_cripta", 1, True, 0.9, "chefe"),
                    ("sino_rachado", 1, False, 1.1, "chefe")],
}

def dentro(o, x, y, folga):
    if o.get("shape") == "circle":
        return math.hypot(x - o["x"], y - o["y"]) < o["r"] + folga
    return abs(x - o["x"]) < o["w"] / 2 + folga and abs(y - o["y"]) < o["h"] / 2 + folga

def livre(m, x, y, folga, postos):
    if abs(x) > m["half_size"] - 2 or abs(y) > m["half_size"] - 2: return False
    if any(dentro(o, x, y, folga) for o in m["obstacles"]): return False
    for lst, d in ((m.get("portals", []), 5), (m.get("chests", []), 3), (m.get("objects", []), 3), (m.get("gates", []), 4),
                   (m.get("npcs", []), 3), (m["spawns"], 2.5), (m.get("waypoints", []), 4)):
        if any(math.hypot(x - p["x"], y - p["y"]) < d for p in lst): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 1.4 for p in m["props"]): return False
    if any(math.hypot(x - p[0], y - p[1]) < 3.5 for p in postos): return False
    if math.hypot(x - m.get("spawn_x", 0), y - m.get("spawn_y", 0)) < 4: return False
    return True

def pontos_parede(m):
    """Pontos 0,9 m para fora de cada parede (os dois lados), a cada 2 m, com o ângulo virado para fora da parede."""
    pts = []
    for o in m["obstacles"]:
        if o.get("shape") == "circle" or o.get("kind") in ("muralha_ilusoria",): continue
        w, h = o["w"], o["h"]
        if max(w, h) < 2.5: continue
        if w >= h:
            n = int(w // 2)
            for i in range(n):
                x = o["x"] - w / 2 + 1 + i * 2
                pts.append((x, o["y"] - h / 2 - 0.9, 180.0)); pts.append((x, o["y"] + h / 2 + 0.9, 0.0))
        else:
            n = int(h // 2)
            for i in range(n):
                y = o["y"] - h / 2 + 1 + i * 2
                pts.append((o["x"] - w / 2 - 0.9, y, 270.0)); pts.append((o["x"] + w / 2 + 0.9, y, 90.0))
    return pts

def main():
    total = 0
    for mid, plano in PLANO.items():
        f = ARQ[mid]
        m = json.load(open(f, encoding="utf-8"))
        m["props"] = [p for p in m["props"] if not p.get("kind", "").startswith("glb:dg/")]
        rnd = random.Random(mid)
        parede = pontos_parede(m)
        rnd.shuffle(parede)
        postos = []
        chefe = next(((s["x"], s["y"]) for s in m["spawns"] if "guardiao" in s["monster"] or "chefe" in s["id"] or "boss" in s["id"]), None)
        larvas = [(s["x"], s["y"]) for s in m["spawns"] if "larva" in s["monster"]]
        for peca, n, encostada, solido, perto in plano:
            alvo = chefe if perto == "chefe" else (larvas[0] if perto == "larva" and larvas else None)
            if encostada:
                cands = sorted(parede, key=lambda p: math.hypot(p[0] - alvo[0], p[1] - alvo[1])) if alvo else parede
            else:
                hs = m["half_size"] - 4
                cands = [(rnd.uniform(-hs, hs), rnd.uniform(-hs, hs), rnd.uniform(0, 360)) for _ in range(800)]
                if alvo: cands = sorted(cands, key=lambda p: math.hypot(p[0] - alvo[0], p[1] - alvo[1]))
            feitos = 0
            for x, y, yaw in cands:
                if feitos >= n: break
                folga = max(0.6, solido + 0.4) if encostada else max(1.6, solido + 1.2)   # solta: longe da parede (corredor livre)
                if not livre(m, x, y, folga if not encostada else 0.5, postos): continue
                if not encostada and any(dentro(o, x, y, 2.2) for o in m["obstacles"]): continue
                p = {"kind": f"glb:dg/dg_{peca}", "x": round(x, 1), "y": round(y, 1), "yaw": round(yaw, 0) if encostada else round(yaw)}
                if solido > 0: p["solid"] = solido
                m["props"].append(p); postos.append((x, y)); feitos += 1
            total += feitos
            if feitos < n: print(f"  {mid}: {peca} só coube {feitos}/{n}")
        json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("peças de DG colocadas:", total)

main()

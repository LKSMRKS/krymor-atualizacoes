"""Trilhas e enfeites da Costa do Cal (11/11). Uso: python3 Tools/decorar_costa.py  (pode rodar de novo: troca o que gerou)

Só visual — não mexe na jogabilidade:
- trilhas de terra batida ("roads" estreitas, pintadas no chão) saindo da estrada principal para o que vale a pena
  achar: acampamento goblin, capim alto do javali, muro rachado da Nara, caranguejos-geodo, ruínas, farol, enseada,
  Rei das Pinças, praia dos ratos;
- placa no começo de cada trilha; cerca quebrada, poste com lanterna e tocha ao longo do caminho;
- montes de areia espalhados no Areal (volume no chão).
Todos os enfeites usam tipos sem colisão (MapGeometry.PropRadius = 0) e ficam longe de monstros, NPCs e obstáculos.
"""
import json, math, os, random

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "Content", "map_costa.json")
d = json.load(open(PATH, encoding="utf-8"))
rng = random.Random(1111)

d["roads"] = [r for r in d["roads"] if not r.get("trilha")]
d["props"] = [p for p in d["props"] if not p.get("decor")]

TRILHAS = {
    "acampamento goblin": [(-4, 20.5), (-14, 22), (-26, 21.5), (-38, 19), (-48, 16), (-55, 13.5), (-58.5, 12)],
    "capim alto": [(-48, 16), (-58, 21), (-70, 24.5), (-80, 24.5), (-85, 24)],
    "muro da Nara": [(-14, 22), (-20, 15), (-25, 10.5), (-28.5, 7.5)],
    "geodos": [(-48, 16), (-53, 7), (-59, 0), (-65, -5), (-68, -7)],
    "estrada dos cães": [(11.6, 13.9), (16, 19), (21, 24), (25, 27)],
    "ruínas e farol": [(25, -6), (36, -16), (46, -28), (58, -44), (70, -60), (80, -72), (83, -75)],
    "enseada": [(24.5, 7.8), (38, 10), (55, 12), (72, 17), (88, 24), (100, 29), (104.5, 31)],
    "Rei das Pinças": [(-4.5, -60.5), (-4, -68), (-2, -77), (0, -85)],
    "praia das pedras": [(1.9, 66.7), (16, 69), (34, 73), (54, 76), (70, 78), (79, 80)],
    "praia dos ratos": [(0.6, 82.7), (-16, 85), (-34, 89), (-52, 93), (-66, 96)],
}
for pts in TRILHAS.values():
    # um pouco de curva natural entre os pontos
    out = []
    for i, (x, y) in enumerate(pts):
        out.append([round(x, 2), round(y, 2)])
        if i < len(pts) - 1:
            nx, ny = pts[i + 1]
            mx, my = (x + nx) / 2, (y + ny) / 2
            dx, dy = nx - x, ny - y; L = math.hypot(dx, dy) or 1
            k = rng.uniform(-0.12, 0.12) * L
            out.append([round(mx - dy / L * k, 2), round(my + dx / L * k, 2)])
    d["roads"].append({"points": out, "width": 1.7, "trilha": True})

# pontos ocupados (enfeite não encosta)
busy = [(s["x"], s["y"], 1.8) for s in d["spawns"]] + [(n["x"], n["y"], 2.5) for n in d["npcs"]]
busy += [(o["x"], o["y"], 1.5) for o in d.get("objects", [])] + [(c["x"], c["y"], 2.0) for c in d["chests"]]
busy += [(p["x"], p["y"], 1.6) for p in d["props"]] + [(w["x"], w["y"], 3.0) for w in d["waypoints"]] + [(p["x"], p["y"], 4.0) for p in d["portals"]]
boxes = []
for o in d["obstacles"]:
    if o.get("shape") == "circle": busy.append((o["x"], o["y"], o.get("r", 0) + 0.8))
    else: boxes.append((o["x"], o["y"], o.get("w", 0) / 2 + 0.8, o.get("h", 0) / 2 + 0.8))
for g in d["gates"]:
    boxes.append((g["x"], g["y"], g["w"] / 2 + 1.5, g["h"] / 2 + 1.5))

def road_dist(x, y):
    best = 1e9
    for r in d["roads"]:
        P = r["points"]
        for i in range(len(P) - 1):
            ax, ay = P[i]; bx, by = P[i + 1]
            vx, vy = bx - ax, by - ay; L2 = vx * vx + vy * vy or 1
            t = max(0, min(1, ((x - ax) * vx + (y - ay) * vy) / L2))
            best = min(best, math.hypot(x - ax - vx * t, y - ay - vy * t) - r["width"] / 2)
    return best

def free(x, y, r=1.0):
    if abs(x) > d["half_size"] - 3 or abs(y) > d["half_size"] - 3: return False
    if any(abs(x - bx) < hw + r * 0.5 and abs(y - by) < hh + r * 0.5 for bx, by, hw, hh in boxes): return False
    return all(math.hypot(x - bx, y - by) > br + r for bx, by, br in busy)

def put(kind, x, y, yaw, r=1.0):
    if not free(x, y, r): return False
    d["props"].append({"kind": kind, "x": round(x, 2), "y": round(y, 2), "yaw": round(yaw) % 360, "decor": True})
    busy.append((x, y, r))
    return True

for name, pts in TRILHAS.items():
    # placa no começo, virada para a estrada
    (x0, y0), (x1, y1) = pts[0], pts[1]
    ang = math.degrees(math.atan2(x1 - x0, y1 - y0))
    side = 1 if rng.random() < 0.5 else -1
    nx, ny = (y1 - y0), -(x1 - x0); L = math.hypot(nx, ny) or 1
    put("placa", x0 + (x1 - x0) / math.hypot(x1 - x0, y1 - y0) * 2.5 + nx / L * 1.9 * side, y0 + (y1 - y0) / math.hypot(x1 - x0, y1 - y0) * 2.5 + ny / L * 1.9 * side, ang + 90)
    # ao longo: cerca quebrada e postes alternando de lado
    walked = 0.0
    for i in range(len(pts) - 1):
        ax, ay = pts[i]; bx, by = pts[i + 1]
        seg = math.hypot(bx - ax, by - ay); ux, uy = (bx - ax) / seg, (by - ay) / seg
        t = 6.0
        while t < seg - 2:
            walked += 1
            s = 1 if int(walked) % 2 else -1
            px, py = ax + ux * t + uy * 1.8 * s, ay + uy * t - ux * 1.8 * s
            yaw = math.degrees(math.atan2(ux, uy))
            kind = "cerca_quebrada" if int(walked) % 3 else ("poste" if rng.random() < 0.6 else "tocha_poste")
            put(kind, px, py, yaw + (90 if kind == "cerca_quebrada" else 0) + rng.uniform(-12, 12), 0.9)
            t += rng.uniform(9, 15)
    # fim da trilha: duas tochas (dá para ver de longe à noite)
    ex, ey = pts[-1]; px, py = pts[-2]; L = math.hypot(ex - px, ey - py) or 1
    for s in (1, -1):
        put("tocha_poste", ex - (ex - px) / L * 2 + (ey - py) / L * 2.2 * s, ey - (ey - py) / L * 2 - (ex - px) / L * 2.2 * s, 0, 0.8)

# tochas na entrada da dungeon e no muro rachado
for (x, y) in [(-96.5, -61.5), (-96.5, -66.5), (-27.5, 5.5), (-32.5, 5.5)]:
    put("tocha_poste", x, y, 0, 0.6)

# montes de areia no Areal e nas dunas (volume no chão)
placed = 0
for _ in range(3000):
    if placed >= 70: break
    x, y = rng.uniform(-100, 100), rng.uniform(-30, 100)
    if road_dist(x, y) < 2.0: continue
    if put("monte_areia", x, y, rng.uniform(0, 360), 2.2): placed += 1

json.dump(d, open(PATH, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
open(PATH, "a").write("\n")
print("trilhas:", len(TRILHAS), "enfeites:", sum(1 for p in d["props"] if p.get("decor")), "montes de areia:", placed)

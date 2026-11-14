#!/usr/bin/env python3
"""
13/11 (pedido do dono: "elementos no cenário — rastro de sangue, corpo morto de decoração, uma placa com seta... algo que
deixa mais vivo"): em cada mapa de campo põe, sem atrapalhar caminho nem monstro:
  - em cada bolsão de monstros: um aventureiro caído ("cadaver", o nosso boneco deitado) com rastro de sangue saindo dele;
  - no covil: dois caídos, sangue e crânios;
  - placas com seta ("placa" + texto) a ~9 m de cada portal, do lado de dentro, dizendo para onde ele leva e o nível.
Os bichinhos e as partículas no ar ficam no cliente (MapView.VidaDoTema). Idempotente (peças marcadas "poi": "vida").
Rodar depois de gerar_lugares.py e gerar_elevacoes.py.
"""
import json, os, glob, math, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
MAPAS = {json.load(open(f, encoding="utf-8"))["id"]: json.load(open(f, encoding="utf-8")).get("display_name", "") for f in glob.glob(os.path.join(C, "map_*.json"))}

def dentro(o, x, y, folga):
    if o.get("shape") == "circle": return math.hypot(x - o["x"], y - o["y"]) < o["r"] + folga
    return abs(x - o["x"]) < o["w"] / 2 + folga and abs(y - o["y"]) < o["h"] / 2 + folga

def livre(m, x, y, folga=1.2, dprop=1.5):
    hs = m["half_size"]
    if abs(x) > hs - 6 or abs(y) > hs - 6: return False
    if any(dentro(o, x, y, folga) for o in m["obstacles"]): return False
    for lst, d in ((m.get("portals", []), 6), (m.get("npcs", []), 4), (m.get("objects", []), 2.5), (m.get("chests", []), 2.5),
                   (m.get("waypoints", []), 5), (m.get("gates", []), 6), (m.get("spawns", []), 2.0)):
        if any(math.hypot(x - p["x"], y - p["y"]) < d for p in lst): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < dprop for p in m["props"] if p.get("kind") != "cadaver"): return False
    for z in m["zones"]:
        if z["kind"] in ("refugio", "seguro", "cidade") and abs(x - z["x"]) < z["w"] / 2 + 3 and abs(y - z["y"]) < z["h"] / 2 + 3: return False
    return True

def na_estrada(m, x, y, larg=2.5):
    for r in m.get("roads", []):
        pts = r["points"]
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            dx, dy = bx - ax, by - ay
            L = dx * dx + dy * dy or 1
            t = max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / L))
            if math.hypot(x - (ax + t * dx), y - (ay + t * dy)) < r.get("width", 3) / 2 + larg: return True
    return False

def main():
    tot = {"cadaver": 0, "sangue": 0, "placa": 0}
    for f in sorted(glob.glob(os.path.join(C, "map_*.json"))):
        m = json.load(open(f, encoding="utf-8"))
        m["props"] = [p for p in m["props"] if p.get("poi") != "vida"]
        if m.get("instanced") or m["kind"] != "campo" or m["half_size"] < 40:
            json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2); continue
        rnd = random.Random("vida:" + m["id"])
        def por(kind, x, y, yaw, **extra):
            m["props"].append({"kind": kind, "x": round(x, 1), "y": round(y, 1), "yaw": round(yaw % 360), "poi": "vida", **extra})
            tot[kind] = tot.get(kind, 0) + 1
        def cena(z, caidos):
            for _ in range(caidos):
                for _t in range(80):
                    x = z["x"] + rnd.uniform(-z["w"] * 0.4, z["w"] * 0.4); y = z["y"] + rnd.uniform(-z["h"] * 0.4, z["h"] * 0.4)
                    if not livre(m, x, y, 1.6) or na_estrada(m, x, y): continue
                    yaw = rnd.uniform(0, 360)
                    por("cadaver", x, y, yaw)
                    # rastro: sai do corpo para o lado de onde ele veio rastejando
                    a = math.radians(rnd.uniform(0, 360))
                    sx, sy = x + 1.2 * math.cos(a), y + 1.2 * math.sin(a)
                    if livre(m, sx, sy, 0.4, 1.0): por("sangue", sx, sy, math.degrees(math.atan2(math.cos(a), math.sin(a))))
                    break
        for z in m["zones"]:
            if z["kind"] in ("fraco", "medio", "forte"): cena(z, 1)
            elif z["kind"] == "perigoso":
                cena(z, 2)
                for _ in range(2):
                    x = z["x"] + rnd.uniform(-z["w"] * 0.35, z["w"] * 0.35); y = z["y"] + rnd.uniform(-z["h"] * 0.35, z["h"] * 0.35)
                    if livre(m, x, y, 0.8): por("cranios", x, y, rnd.uniform(0, 360))
        # placas com seta para cada portal (do lado de dentro do mapa)
        for p in m.get("portals", []):
            if p["to_map"] not in MAPAS: continue
            d = math.hypot(p["x"], p["y"]) or 1
            ux, uy = -p["x"] / d, -p["y"] / d
            for dist in (9, 11, 13, 7):
                for lado in (2.2, -2.2, 0):
                    x = p["x"] + ux * dist - uy * lado; y = p["y"] + uy * dist + ux * lado
                    if not livre(m, x, y, 0.8): continue
                    nivel = f" · nível {p['min_level']}+" if p.get("min_level") else ""
                    texto = f"→ {MAPAS[p['to_map']] or p['label']}{nivel}"
                    yaw = math.degrees(math.atan2(ux, uy)) + 180   # a face da placa olha para quem vem do centro
                    por("placa", x, y, yaw, texto=texto)
                    break
                else: continue
                break
        json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("vida no cenário:", tot)

main()

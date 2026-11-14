#!/usr/bin/env python3
"""
Povoa os mapas abertos (rodada 3: "o mundo está maior mas vazio"): estradas de terra ligando acampamento, portais e
pedras de viagem (desviando de muros e penhascos), árvores e rochas em bosques (com colisão no servidor: obstáculos
"arvore"/"rocha"), cercas de pasto perto dos acampamentos (obstáculo "cerca").
Tufos, flores, arbustos e pedrinhas (sem colisão) o cliente espalha sozinho.

Idempotente: tudo o que este script põe leva "deco": true e é refeito do zero; estradas ficam em "roads".
Determinístico (semente pelo id do mapa). Não mexe em spawns, NPCs, portais, baús, portões.
Rode depois de gerar_regioes.py (ele chama este script no fim):  python3 Tools/povoar_mapas.py
"""
import json, math, os, heapq, random, sys

CONTENT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Content")

# tema → (árvores por 100 m², rochas por 100 m², fração de bosque, altura das árvores)
THEMES = {
    "praia":    (0.22, 0.16, 0.40, (4.5, 6.5)),
    "vale":     (0.40, 0.14, 0.50, (5.0, 7.5)),
    "mata":     (0.55, 0.08, 0.65, (6.0, 9.0)),
    "canion":   (0.04, 0.22, 0.30, (4.0, 5.5)),
    "brejo":    (0.24, 0.06, 0.50, (4.5, 6.5)),
    "fantasma": (0.16, 0.08, 0.45, (4.5, 6.5)),
    "neve":     (0.26, 0.12, 0.50, (5.5, 8.0)),
    "lava":     (0.00, 0.20, 0.30, (4.0, 5.0)),
    "acropole": (0.08, 0.14, 0.35, (4.5, 6.0)),
}
SKIP = {"porto_das_cinzas", "casa_jogador"}


def load(f):
    with open(os.path.join(CONTENT, f), encoding="utf-8") as h:
        return json.load(h)


def save(f, data):
    with open(os.path.join(CONTENT, f), "w", encoding="utf-8") as h:
        json.dump(data, h, ensure_ascii=False, indent=2)


PROP_R = {"pinheiros": 1.1, "tenda": 1.8, "barraca": 1.8, "caixotes": 0.8, "barril": 0.45, "coluna_quebrada": 0.7,
          "estacas": 0.7, "lapide": 0.45, "banca": 1.1, "forja": 1.3, "sino": 0.9, "bau_armazem": 0.7}


# adereços com história por tema (MapKit: barco_encalhado, redes, ossada, acampamento_abandonado)
STORY = {
    "praia": [("barco_encalhado", 4), ("redes", 4), ("ossada", 5), ("acampamento_abandonado", 2)],
    "vale": [("ossada", 4), ("acampamento_abandonado", 2)],
    "mata": [("ossada", 5), ("acampamento_abandonado", 3)],
    "canion": [("ossada", 7), ("acampamento_abandonado", 2)],
    "brejo": [("barco_encalhado", 3), ("redes", 2), ("ossada", 4)],
    "fantasma": [("ossada", 5), ("acampamento_abandonado", 2)],
    "neve": [("ossada", 4), ("acampamento_abandonado", 3)],
    "lava": [("ossada", 6)],
    "acropole": [("ossada", 4), ("acampamento_abandonado", 2)],
}


class Geo:
    def __init__(self, m, gates=True, deco=False):
        self.half = m["half_size"]
        self.boxes, self.circles = [], []
        for o in m["obstacles"]:
            if (o.get("deco") and not deco) or o.get("solid") is False: continue
            if o["shape"] == "box": self.boxes.append((o["x"], o["y"], o["w"] / 2, o["h"] / 2))
            else: self.circles.append((o["x"], o["y"], o["r"]))
        for g in (m.get("gates", []) if gates else []):
            self.boxes.append((g["x"], g["y"], g["w"] / 2, g["h"] / 2))
        # adereços sólidos (mesma tabela de MapGeometry.PropRadius)
        for p in m.get("props", []):
            r = PROP_R.get(p.get("kind"), 0)
            if r > 0: self.circles.append((p["x"], p["y"], r))

    def blocked(self, x, y, pad):
        if abs(x) > self.half - 1.5 - pad or abs(y) > self.half - 1.5 - pad: return True
        for bx, by, hw, hh in self.boxes:
            if abs(x - bx) <= hw + pad and abs(y - by) <= hh + pad: return True
        for cx, cy, r in self.circles:
            if (x - cx) ** 2 + (y - cy) ** 2 <= (r + pad) ** 2: return True
        return False


def astar(geo, a, b, cell=2.0):
    """Caminho em grade evitando obstáculos (com folga), de a até b; None se não houver."""
    n = int(geo.half * 2 / cell) + 1
    def idx(p): return (min(n - 1, max(0, int((p[0] + geo.half) / cell))), min(n - 1, max(0, int((p[1] + geo.half) / cell))))
    def pos(i, j): return (-geo.half + (i + 0.5) * cell, -geo.half + (j + 0.5) * cell)
    free = {}
    def ok(i, j):
        k = (i, j)
        if k not in free: free[k] = not geo.blocked(*pos(i, j), 0.9)
        return free[k]
    s, t = idx(a), idx(b)
    free[s] = True; free[t] = True
    openq = [(0, s)]; came = {s: None}; g = {s: 0}
    while openq:
        _, cur = heapq.heappop(openq)
        if cur == t: break
        for di in (-1, 0, 1):
            for dj in (-1, 0, 1):
                if di == dj == 0: continue
                nx = (cur[0] + di, cur[1] + dj)
                if not (0 <= nx[0] < n and 0 <= nx[1] < n) or not ok(*nx): continue
                cost = g[cur] + (1.414 if di and dj else 1.0)
                if cost < g.get(nx, 1e9):
                    g[nx] = cost; came[nx] = cur
                    heapq.heappush(openq, (cost + math.hypot(nx[0] - t[0], nx[1] - t[1]), nx))
    if t not in came: return None
    path = []; c = t
    while c is not None: path.append(pos(*c)); c = came[c]
    path.reverse()
    path[0] = a; path[-1] = b
    return path


def simplify(path, geo):
    """Corta cantos onde dá (linha livre) e suaviza (Chaikin) para a estrada não sair em escada."""
    out = [path[0]]; i = 0
    while i < len(path) - 1:
        j = len(path) - 1
        while j > i + 1:
            ax, ay = path[i]; bx, by = path[j]
            steps = int(math.hypot(bx - ax, by - ay) / 1.0) + 1
            if all(not geo.blocked(ax + (bx - ax) * k / steps, ay + (by - ay) * k / steps, 0.9) for k in range(steps + 1)): break
            j -= 1
        out.append(path[j]); i = j
    for _ in range(2):
        sm = [out[0]]
        for (x0, y0), (x1, y1) in zip(out, out[1:]):
            sm.append((x0 * 0.75 + x1 * 0.25, y0 * 0.75 + y1 * 0.25)); sm.append((x0 * 0.25 + x1 * 0.75, y0 * 0.25 + y1 * 0.75))
        sm.append(out[-1]); out = sm
    return [[round(x, 2), round(y, 2)] for x, y in out]


def dist_seg(px, py, a, b):
    ax, ay = a; bx, by = b
    dx, dy = bx - ax, by - ay
    L = dx * dx + dy * dy
    t = 0 if L == 0 else max(0, min(1, ((px - ax) * dx + (py - ay) * dy) / L))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def grove_noise(rng, half):
    """Manchas de bosque: alguns centros com raio; a densidade cai longe deles."""
    centers = [(rng.uniform(-half, half), rng.uniform(-half, half), rng.uniform(10, 26)) for _ in range(int(half / 9) + 3)]
    def f(x, y):
        v = 0
        for cx, cy, r in centers:
            d = math.hypot(x - cx, y - cy)
            if d < r: v = max(v, 1 - d / r)
        return v
    return f


def populate(fname):
    m = load(fname)
    if m["id"] in SKIP or m.get("instanced") or m.get("theme") not in THEMES: return None
    rng = random.Random(m["id"])
    m["obstacles"] = [o for o in m["obstacles"] if not o.get("deco")]
    m["props"] = [p for p in m.get("props", []) if not p.get("deco")]
    geo = Geo(m)
    road_geo = Geo(m, gates=False)
    half = m["half_size"]
    trees_d, rocks_d, grove_frac, (hmin, hmax) = THEMES[m["theme"]]

    # ---- estradas: acampamento ↔ pedras de viagem ↔ portais (árvore geradora mínima) ----
    keys = [(m["spawn_x"], m["spawn_y"])]
    keys += [(w["x"], w["y"]) for w in m.get("waypoints", [])]
    keys += [(p["x"], p["y"]) for p in m.get("portals", [])]
    keys = [k for i, k in enumerate(keys) if all(math.hypot(k[0] - q[0], k[1] - q[1]) > 6 for q in keys[:i])]
    roads = []
    linked = [keys[0]]; rest = keys[1:]
    while rest:
        best = min(((a, b) for a in linked for b in rest), key=lambda ab: math.hypot(ab[0][0] - ab[1][0], ab[0][1] - ab[1][1]))
        a, b = best
        # a estrada passa pelos portões/pontes (é por onde as pessoas iriam); árvores e rochas não
        path = astar(road_geo, a, b)
        if path: roads.append({"points": simplify(path, road_geo), "width": 3.2 if a == keys[0] or b == keys[0] else 2.6})
        else: print(f"  {m['id']}: sem caminho {a} -> {b}")
        linked.append(b); rest.remove(b)
    m["roads"] = roads
    segs = [(r["points"][i], r["points"][i + 1]) for r in roads for i in range(len(r["points"]) - 1)]

    def near_road(x, y, d): return any(dist_seg(x, y, a, b) < d for a, b in segs)

    keep_out = [(m["spawn_x"], m["spawn_y"], m.get("safe_radius", 8) + 4)]
    keep_out += [(p["x"], p["y"], 6) for p in m.get("portals", [])]
    keep_out += [(w["x"], w["y"], 6) for w in m.get("waypoints", [])]
    keep_out += [(n["x"], n["y"], 4) for n in m.get("npcs", [])]
    keep_out += [(c["x"], c["y"], 4) for c in m.get("chests", [])]
    keep_out += [(o["x"], o["y"], 4) for o in m.get("objects", [])]
    keep_out += [(s["x"], s["y"], 4.5) for s in m.get("spawns", [])]
    for g in m.get("gates", []):
        keep_out.append((g["x"], g["y"], max(g["w"], g["h"]) / 2 + 5)); keep_out.append((g["lever_x"], g["lever_y"], 4))
    for c in m.get("checkpoints", []) or []:
        keep_out.append((c["x"], c["y"], 4))
    for z in m.get("zones", []):
        if z["kind"] in ("seguro", "chefe"):
            keep_out.append((z["x"], z["y"], max(z["w"], z["h"]) / 2 + 2))

    def free(x, y, r, gap):
        if geo.blocked(x, y, r + gap): return False
        if near_road(x, y, r + 2.4): return False
        return all(math.hypot(x - kx, y - ky) > kr + r for kx, ky, kr in keep_out)

    grove = grove_noise(rng, half)
    placed = []
    area = (2 * half) ** 2 / 100.0
    added = []

    def try_place(kind, count, rmin, rmax, spacing, groves):
        n = 0; tries = 0
        while n < count and tries < count * 30:
            tries += 1
            x = rng.uniform(-half + 3, half - 3); y = rng.uniform(-half + 3, half - 3)
            if groves:
                g = grove(x, y)
                if rng.random() > (grove_frac * 0.25 + g) : continue
            r = rng.uniform(rmin, rmax)
            if not free(x, y, r, 1.6): continue
            if any(math.hypot(x - px, y - py) < spacing + r + pr for px, py, pr in placed): continue
            placed.append((x, y, r))
            h = round(rng.uniform(hmin, hmax), 1) if kind == "arvore" else round(r * rng.uniform(1.0, 1.7), 1)
            o = {"kind": kind, "shape": "circle", "x": round(x, 2), "y": round(y, 2), "w": 0, "h": 0, "r": round(r, 2), "height": h, "deco": True}
            added.append(o); n += 1
        return n

    nt = try_place("arvore", int(area * trees_d), 0.45, 0.6, 2.2, True)
    nr = try_place("rocha", int(area * rocks_d), 0.5, 1.4, 2.6, False)

    # ---- cercas: pastos perto do acampamento (retângulo com uma abertura), alinhados aos eixos ----
    fences = 0
    sx, sy = m["spawn_x"], m["spawn_y"]
    for k in range(60):
        if fences >= 3: break
        ang = rng.uniform(0, math.tau); d = m.get("safe_radius", 8) + rng.uniform(6, 26)
        cx, cy = sx + math.cos(ang) * d, sy + math.sin(ang) * d
        w, h = rng.uniform(6, 10), rng.uniform(4, 7)
        corners_ok = all(free(cx + dx * w / 2, cy + dy * h / 2, 0.3, 0.8) for dx in (-1, 0, 1) for dy in (-1, 0, 1))
        if not corners_ok: continue
        gap_side = rng.randrange(4)
        sides = [("h", cx, cy - h / 2, w), ("h", cx, cy + h / 2, w), ("v", cx - w / 2, cy, h), ("v", cx + w / 2, cy, h)]
        for i, (o, x, y, L) in enumerate(sides):
            if i == gap_side:
                # abertura de 2,4 m no meio do lado
                part = (L - 2.4) / 2
                for sgn in (-1, 1):
                    off = sgn * (2.4 / 2 + part / 2)
                    ox, oy = (x + off, y) if o == "h" else (x, y + off)
                    added.append({"kind": "cerca", "shape": "box", "x": round(ox, 2), "y": round(oy, 2),
                                  "w": round(part if o == "h" else 0.2, 2), "h": round(0.2 if o == "h" else part, 2), "r": 0, "height": 1.1, "deco": True})
            else:
                added.append({"kind": "cerca", "shape": "box", "x": round(x, 2), "y": round(y, 2),
                              "w": round(L if o == "h" else 0.2, 2), "h": round(0.2 if o == "h" else L, 2), "r": 0, "height": 1.1, "deco": True})
        keep_out.append((cx, cy, max(w, h) / 2 + 1))
        fences += 1

    # ---- adereços com história (pedido: "barracas, redes, barcos, ossadas"): não bloqueiam, só contam o lugar ----
    story = STORY.get(m["theme"], [])
    ns = 0
    for kind, count in story:
        for _ in range(count):
            for _try in range(40):
                x = rng.uniform(-half + 6, half - 6); y = rng.uniform(-half + 6, half - 6)
                if not free(x, y, 1.2, 1.5) or any(math.hypot(x - o["x"], y - o["y"]) < 3 for o in added): continue
                m["props"].append({"kind": kind, "x": round(x, 2), "y": round(y, 2), "yaw": round(rng.uniform(0, 360)), "deco": True})
                keep_out.append((x, y, 3)); ns += 1
                break

    m["obstacles"] += added
    nf = fill_spawns(m, rng)
    save(fname, m)
    return f"{m['id']}: {len(roads)} estradas, {nt} árvores, {nr} rochas, {fences} cercados, {nf} monstros de preenchimento"


_MONSTERS = None


def monsters():
    global _MONSTERS
    if _MONSTERS is None:
        _MONSTERS = {}
        for f in os.listdir(CONTENT):
            if f.startswith("monsters") and f.endswith(".json"):
                for d in load(f): _MONSTERS[d["id"]] = d
    return _MONSTERS


def bestiario():
    try:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import gerar_bestiario
        return gerar_bestiario.por_mapa()
    except ImportError:
        return {}


def fill_spawns(m, rng):
    """
    Feedback (08/11): "cantos sem monstro" e "quanto mais longe, mais nível". Acha os buracos do mapa (células de 14 m
    sem nenhum monstro a menos de 20 m, fora de área segura/refúgio) e põe ali 1–2 bichos da mesma espécie dos
    vizinhos mais próximos, com nível maior quanto mais longe do acampamento (+0 a +6). Sem lotar: no máximo metade do
    no máximo um terço dos pontos que o mapa já tinha. Idempotente: os de preenchimento levam "fill": true e são refeitos.
    """
    defs = monsters()
    m["spawns"] = [sp for sp in m["spawns"] if not sp.get("fill")]
    base = [sp for sp in m["spawns"] if sp.get("monster") in defs and not defs[sp["monster"]].get("boss")
            and not defs[sp["monster"]].get("elite") and sp.get("chance", 1.0) >= 1.0]
    if not base: return 0
    geo = Geo(m, deco=True)   # árvores/rochas recém-postas também ocupam lugar
    half = m["half_size"]
    sx, sy = m["spawn_x"], m["spawn_y"]
    safe = [(z["x"], z["y"], z["w"] / 2 + 4, z["h"] / 2 + 4) for z in m.get("zones", []) if z.get("kind") in ("seguro", "refugio", "cidade")]
    portals = [(p["x"], p["y"], p.get("radius", 2) + 14) for p in m.get("portals", [])]   # +14: o espalhar não chega na passagem
    portals += [(gt["x"], gt["y"], max(gt.get("w", 2), gt.get("h", 2)) / 2 + 12) for gt in m.get("gates", [])]   # nem em ponte/portão
    portals += [(o["x"], o["y"], 12) for o in m.get("objects", [])]   # nem em cima de veio/erva/baú (coletar em paz)
    def in_safe(x, y): return (any(abs(x - zx) < hw and abs(y - zy) < hh for zx, zy, hw, hh in safe)
                              or math.hypot(x - sx, y - sy) < m.get("safe_radius", 8) + 10
                              or any(math.hypot(x - px, y - py) < pr for px, py, pr in portals))
    # alcançável a pé a partir do acampamento (portões contam como abertos): inunda a grade de 2 m
    rgeo = Geo(m, gates=False)
    g = 2.0; gn = int(2 * half / g) + 1
    def gi(x, y): return (min(gn - 1, max(0, int((x + half) / g))), min(gn - 1, max(0, int((y + half) / g))))
    reach = set(); stack = [gi(sx, sy)]
    while stack:
        c = stack.pop()
        if c in reach: continue
        cx, cy = -half + (c[0] + 0.5) * g, -half + (c[1] + 0.5) * g
        if rgeo.blocked(cx, cy, 0.6) and c != gi(sx, sy): continue
        reach.add(c)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nb = (c[0] + dx, c[1] + dy)
            if 0 <= nb[0] < gn and 0 <= nb[1] < gn and nb not in reach: stack.append(nb)
    cell = 14.0
    holes = []
    n = int(2 * half / cell)
    for i in range(n):
        for j in range(n):
            x = -half + (i + 0.5) * cell + rng.uniform(-3, 3); y = -half + (j + 0.5) * cell + rng.uniform(-3, 3)
            if geo.blocked(x, y, 1.2) or in_safe(x, y) or gi(x, y) not in reach: continue
            gap = min(math.hypot(x - sp["x"], y - sp["y"]) for sp in m["spawns"])
            if gap >= 20: holes.append((gap, x, y))
    holes.sort(reverse=True)
    limit = max(4, len(base) // 3)
    far_max = max(30.0, max(math.hypot(sp["x"] - sx, sp["y"] - sy) for sp in base))
    added = 0
    for gap, x, y in holes:
        if added >= limit: break
        if any(math.hypot(x - sp["x"], y - sp["y"]) < 18 for sp in m["spawns"] if sp.get("fill")): continue
        near = sorted(base, key=lambda sp: math.hypot(x - sp["x"], y - sp["y"]))[:3]
        kinds = [sp["monster"] for sp in near]
        kind = max(set(kinds), key=kinds.count)
        ref = next(sp for sp in near if sp["monster"] == kind)
        d = math.hypot(x - sx, y - sy)
        bonus = int(round(max(0.0, min(1.0, (d - 25) / max(1.0, far_max - 25))) * 6))
        level = ref.get("level", 1) + bonus
        respawn = ref.get("respawn_seconds", 30)
        count = rng.choice((1, 1, 2))
        group = f"fill{added}"
        for k in range(count):
            px, py = x + rng.uniform(-2.5, 2.5) * k, y + rng.uniform(-2.5, 2.5) * k
            if geo.blocked(px, py, 1.0) or in_safe(px, py): px, py = x, y
            m["spawns"].append({"id": f"f{added}_{k}", "monster": kind, "x": round(px, 1), "y": round(py, 1),
                                "level": level, "group": group, "respawn_seconds": respawn, "fill": True})
        added += 1
    added += bestiary_spawns(m, rng, defs, base, geo, in_safe, lambda x, y: gi(x, y) in reach)
    return added


def bestiary_spawns(m, rng, defs, base, geo, in_safe, reachable):
    """
    Criaturas do bestiário (Tools/gerar_bestiario.py) no mapa: cada espécie comum em 3 pontos, a rara de ver em 1,
    onde o nível dos vizinhos mais combina com o dela, longe de outros bichos (≥ 9 m) e umas das outras (≥ 16 m).
    """
    species = [(mid, lvl, peso) for mid, lvl, peso in bestiario().get(m["id"], []) if mid in defs]
    if not species or not base: return 0
    half = m["half_size"]
    small = half < 60          # mapa pequeno (Vale): aperta as distâncias para caberem todas
    gap, sep = (6.0, 10.0) if small else (9.0, 16.0)
    cands = []
    step = 5.0 if small else 7.0
    n = int(2 * half / step)
    for i in range(n):
        for j in range(n):
            x = -half + (i + 0.5) * step + rng.uniform(-2, 2); y = -half + (j + 0.5) * step + rng.uniform(-2, 2)
            if abs(x) > half - 4 or abs(y) > half - 4: continue
            if geo.blocked(x, y, 1.4) or in_safe(x, y) or not reachable(x, y): continue
            if min(math.hypot(x - sp["x"], y - sp["y"]) for sp in m["spawns"]) < gap: continue
            near = min(base, key=lambda sp: math.hypot(x - sp["x"], y - sp["y"]))
            cands.append((x, y, near.get("level", 1)))
    placed, added = [], 0
    for mid, lvl, peso in species:
        want = 3 if peso >= 2 else 1
        for _ in range(want):
            ok = [c for c in cands if all(math.hypot(c[0] - px, c[1] - py) >= sep for px, py in placed)
                  and min(math.hypot(c[0] - sp["x"], c[1] - sp["y"]) for sp in m["spawns"]) >= gap]
            if not ok: break
            x, y, near_lvl = min(ok, key=lambda c: abs(c[2] - lvl) + rng.uniform(0, 2.5))
            big = defs[mid].get("health_mult", 1) >= 2 or peso == 1
            count = 1 if big else rng.choice((1, 2))
            group = f"bst{added}"
            for k in range(count):
                px, py = x + rng.uniform(-2.0, 2.0) * k, y + rng.uniform(-2.0, 2.0) * k
                if geo.blocked(px, py, 1.0) or in_safe(px, py): px, py = x, y
                m["spawns"].append({"id": f"b{added}_{k}", "monster": mid, "x": round(px, 1), "y": round(py, 1),
                                    "level": lvl + (2 if peso == 1 else rng.choice((0, 0, 1))), "group": group,
                                    "respawn_seconds": 120 if peso == 1 else 45, "fill": True})
            placed.append((x, y))
            added += 1
    return added


def main():
    world = load("world.json")
    for f in world["map_files"]:
        r = populate(f)
        if r: print(r)


if __name__ == "__main__":
    main()

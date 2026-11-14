#!/usr/bin/env python3
"""
13/11 (pedido do dono: "tem que fazer novas plantas pras DGs, crie formatos aleatórios e confusos, com labirintos"):
troca a planta de TODAS as dungeons que usavam o mesmo traçado (vestíbulo → corredor → ala → sala da guarda → antessala →
salão do chefe) por um LABIRINTO próprio de cada uma — formato do contorno, tamanho da célula, quantidade de atalhos e de
salas mudam por dungeon (semente fixa: a mesma dungeon sai sempre igual; rodar de novo não muda nada).

O que é mantido de cada dungeon: id, nome, tema, monstros, níveis, grupos, ids dos spawns, chefe, loot de primeira vez,
baú, portais (ids e destinos), checkpoint e os enfeites temáticos (só mudam de lugar). O que muda: paredes, zonas,
posições de tudo e o ponto de entrada.
  * a entrada fica numa ponta; o chefe, na sala mais longe pelo caminho (busca em largura);
  * a "ala" (5 bichos) e a "sala da guarda" (mini-chefe) ficam em salas abertas no meio do caminho;
  * a "câmara lateral" (3 bichos + baú) fica num beco sem saída — quem explora acha;
  * atalhos (paredes a menos) só entre trechos de mesma profundidade: voltas para se perder, sem encurtar a ida ao chefe.
Rodar por último (depois de gerar_regioes, gerar_castelos, gerar_cidades e gerar_merlin, que copiam essas plantas).
Uso (raiz do projeto): python3 Tools/gerar_plantas_dg.py
"""
import json
import math
import os
import random
from collections import deque

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")

# dungeon: (tamanho da célula em m, contorno, atalhos 0..1, salas extras)
ESTILOS = {
    "cripta_do_sino": (8.0, "cruz", 0.10, 1),
    "galerias_piedade": (7.0, "diagonal", 0.14, 1),
    "mosteiro_afundado": (8.0, "anel", 0.12, 2),
    "mercado_mortos": (7.0, "quadrado", 0.22, 3),
    "salao_geada": (9.0, "circulo", 0.10, 2),
    "fornalha": (8.0, "ilhas", 0.16, 2),
    "labirinto_juramento": (6.0, "quadrado", 0.06, 0),      # o mais confuso: célula pequena, quase sem atalho
    "castelo_vespera": (8.0, "anel", 0.18, 2),
    "fortaleza_geada": (8.0, "cruz", 0.16, 2),
    "cidadela_escoria": (7.0, "circulo", 0.14, 2),
    "merlin_torre_torta": (7.0, "espiral", 0.08, 1),
    "merlin_poco_estrelas": (8.0, "circulo", 0.12, 2),
    "merlin_sonho_dragao": (7.0, "ilhas", 0.20, 2),
}
PAREDE = 1.2          # espessura
ALTURA = 5.0


def L(f):
    return json.load(open(os.path.join(C, f), encoding="utf-8"))


def D(f, obj):
    json.dump(obj, open(os.path.join(C, f), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


def mascara(forma, n, rnd):
    """Quais células da grade n×n existem (o resto é rocha)."""
    c = (n - 1) / 2
    ok = set()
    buracos = [(rnd.uniform(1, n - 2), rnd.uniform(1, n - 2), rnd.uniform(0.9, 1.6)) for _ in range(3)]
    for i in range(n):
        for j in range(n):
            dx, dy = i - c, j - c
            r = math.hypot(dx, dy)
            if forma == "quadrado":
                v = True
            elif forma == "circulo":
                v = r <= c + 0.35
            elif forma == "cruz":
                v = abs(dx) <= n * 0.2 or abs(dy) <= n * 0.2 or r <= c * 0.55
            elif forma == "anel":
                v = c * 0.32 <= r <= c + 0.35
            elif forma == "diagonal":
                v = abs(dx - dy) <= n * 0.42 or r <= c * 0.45
            elif forma == "espiral":
                a = math.atan2(dy, dx)
                v = r <= c + 0.35 and not (abs(((a + math.pi) / math.tau * c * 2 + r * 1.0) % 3.2 - 1.6) < 0.35 and r > 1.2)
            elif forma == "ilhas":
                v = r <= c + 0.35 and not any(math.hypot(i - bx, j - by) < br for bx, by, br in buracos)
            else:
                v = True
            if v:
                ok.add((i, j))
    # só o maior pedaço conectado
    melhor = set()
    vistos = set()
    for s in ok:
        if s in vistos:
            continue
        comp, q = set([s]), deque([s])
        vistos.add(s)
        while q:
            a = q.popleft()
            for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                b = (a[0] + d[0], a[1] + d[1])
                if b in ok and b not in vistos:
                    vistos.add(b)
                    comp.add(b)
                    q.append(b)
        if len(comp) > len(melhor):
            melhor = comp
    return melhor


def labirinto(celulas, rnd, atalhos):
    """Paredes abertas entre células vizinhas: backtracker (labirinto perfeito) + atalhos aleatórios."""
    aberto = set()
    inicio = rnd.choice(sorted(celulas))
    pilha, vis = [inicio], {inicio}
    while pilha:
        a = pilha[-1]
        viz = [(a[0] + d[0], a[1] + d[1]) for d in ((1, 0), (-1, 0), (0, 1), (0, -1))]
        viz = [b for b in viz if b in celulas and b not in vis]
        if not viz:
            pilha.pop()
            continue
        b = rnd.choice(viz)
        aberto.add(frozenset((a, b)))
        vis.add(b)
        pilha.append(b)
    for a in sorted(celulas):
        for d in ((1, 0), (0, 1)):
            b = (a[0] + d[0], a[1] + d[1])
            if b in celulas and frozenset((a, b)) not in aberto and rnd.random() < atalhos:
                aberto.add(frozenset((a, b)))
    return aberto


def bfs(celulas, aberto, origem):
    dist, pai, q = {origem: 0}, {origem: None}, deque([origem])
    while q:
        a = q.popleft()
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            b = (a[0] + d[0], a[1] + d[1])
            if b in celulas and b not in dist and frozenset((a, b)) in aberto:
                dist[b] = dist[a] + 1
                pai[b] = a
                q.append(b)
    return dist, pai


def sala(celulas, aberto, canto, w, h):
    """Abre as paredes internas de um bloco w×h (vira sala). Devolve as células da sala (as que existem)."""
    cs = [(canto[0] + i, canto[1] + j) for i in range(w) for j in range(h) if (canto[0] + i, canto[1] + j) in celulas]
    for a in cs:
        for d in ((1, 0), (0, 1)):
            b = (a[0] + d[0], a[1] + d[1])
            if b in cs:
                aberto.add(frozenset((a, b)))
    return cs


def conexo(celulas, aberto, origem):
    return len(bfs(celulas, aberto, origem)[0]) == len(celulas)


def reparar(celulas, aberto, entrada, alvo, minimo, salas):
    """Fecha paredes no caminho mais curto até `alvo` enquanto ele for menor que `minimo` células, sem nunca isolar uma
    parte do labirinto e sem mexer no lado de dentro das salas."""
    dentro = set()
    for sl in salas:
        for a in sl:
            for b in sl:
                dentro.add(frozenset((a, b)))
    for _ in range(400):
        dist, pai = bfs(celulas, aberto, entrada)
        if dist.get(alvo, 0) >= minimo:
            return
        cam, c = [], alvo
        while pai[c] is not None:
            cam.append(frozenset((c, pai[c])))
            c = pai[c]
        fechou = False
        for e in cam:
            if e in dentro:
                continue
            aberto.discard(e)
            if conexo(celulas, aberto, entrada):
                fechou = True
                break
            aberto.add(e)
        if not fechou:
            return


def principal_set(caminho):
    return set(caminho)


def planta(m, estilo, seed):
    cel, forma, atalhos, extras = estilo
    rnd = random.Random(seed)
    H = m["half_size"]
    util = 2 * (H - 3)
    n = max(5, int(util // cel))
    x0 = -n * cel / 2
    centro = lambda c: (round(x0 + (c[0] + 0.5) * cel, 2), round(x0 + (c[1] + 0.5) * cel, 2))
    celulas = mascara(forma, n, rnd)
    aberto = labirinto(celulas, rnd, 0.0)
    # entrada: a célula mais ao norte (y maior), no meio
    entrada = max(celulas, key=lambda c: (c[1], -abs(c[0] - (n - 1) / 2)))
    # atalhos só entre células de profundidade parecida: criam voltas para se perder, mas não encurtam a ida ao chefe
    dist, pai = bfs(celulas, aberto, entrada)
    D0 = max(dist.values())                             # profundidade do labirinto perfeito
    for a in sorted(celulas):
        for d in ((1, 0), (0, 1)):
            b = (a[0] + d[0], a[1] + d[1])
            if b in celulas and frozenset((a, b)) not in aberto and abs(dist[a] - dist[b]) <= 2 and rnd.random() < atalhos * 2.5:
                aberto.add(frozenset((a, b)))
    dist, pai = bfs(celulas, aberto, entrada)
    # salão do chefe: bloco 2×2 em volta da célula mais longe
    # bloco 2×2 em que TODAS as células são fundas (senão a sala liga o fundo a um trecho perto da entrada)
    def bloco(k):
        return [(k[0] + i, k[1] + j) for i in range(2) for j in range(2) if (k[0] + i, k[1] + j) in celulas]
    cands = {(c[0] - dx, c[1] - dy) for c in celulas for dx in (0, 1) for dy in (0, 1)}
    canto = max(cands, key=lambda k: (len(bloco(k)) >= 3, min((dist[c] for c in bloco(k)), default=-1)))
    longe = max(bloco(canto), key=lambda c: dist[c])
    trono = sala(celulas, aberto, canto, 2, 2)
    salas = [trono]
    minimo = max(4, int(D0 * 0.7))
    reparar(celulas, aberto, entrada, min(trono, key=lambda t: bfs(celulas, aberto, entrada)[0].get(t, 9999)), minimo, salas)
    dist, pai = bfs(celulas, aberto, entrada)
    alvo = max(trono, key=lambda c: dist.get(c, -1))
    caminho = []
    c = min(trono, key=lambda t: dist.get(t, 9999))
    while c is not None:
        caminho.append(c)
        c = pai[c]
    caminho.reverse()                                   # entrada → porta do salão
    antessala = caminho[-2] if len(caminho) > 2 else caminho[0]
    # salas no meio do caminho: ala (~40%) e guarda (~75%)
    perto_entrada = {c for c in celulas if abs(c[0] - entrada[0]) + abs(c[1] - entrada[1]) <= 2} | {antessala}
    def bloco_em(c, w=2, h=2):
        melhor, mk = None, -1
        for dx in range(w):
            for dy in range(h):
                k = (c[0] - dx, c[1] - dy)
                q = sum(1 for i in range(w) for j in range(h) if (k[0] + i, k[1] + j) in celulas and (k[0] + i, k[1] + j) not in trono and (k[0] + i, k[1] + j) != entrada)
                if q > mk:
                    melhor, mk = k, q
        cs = sala(celulas, aberto, melhor, w, h)
        salas.append(cs)
        return [x for x in cs if x not in trono and x not in perto_entrada] or [c]
    meio = [c for c in caminho if c not in perto_entrada and c not in trono] or caminho[1:-1] or caminho
    ala = bloco_em(meio[int(len(meio) * 0.35)])
    guarda = bloco_em(meio[min(len(meio) - 1, int(len(meio) * 0.8))])
    for _ in range(extras):                              # salas extras soltas (mais espaço, mais confusão)
        c = rnd.choice(sorted(celulas))
        bloco_em(c, rnd.choice((2, 3)), 2)
    # as salas também abrem atalhos: fecha de novo até a ida ao salão voltar a ser longa
    for _ in range(3):
        d_ = bfs(celulas, aberto, entrada)[0]
        porta_trono = min(trono, key=lambda t: d_.get(t, 9999))
        reparar(celulas, aberto, entrada, porta_trono, minimo, salas)
    dist, pai = bfs(celulas, aberto, entrada)
    caminho, c = [], min(trono, key=lambda t: dist.get(t, 9999))
    while c is not None:
        caminho.append(c)
        c = pai[c]
    caminho.reverse()
    antessala = caminho[-2] if len(caminho) > 2 else caminho[0]
    # câmara lateral: beco sem saída longe do caminho principal
    principal = set(caminho)
    becos = [c for c in celulas if c not in principal and c not in trono and c not in ala and c not in guarda and c != entrada
             and sum(1 for d in ((1, 0), (-1, 0), (0, 1), (0, -1)) if frozenset((c, (c[0] + d[0], c[1] + d[1]))) in aberto) == 1]
    def longe_do_caminho(c):
        return min(abs(c[0] - p[0]) + abs(c[1] - p[1]) for p in principal)
    lateral = max(becos, key=lambda c: (longe_do_caminho(c), dist.get(c, 0))) if becos else max(
        (c for c in celulas if c not in principal and c not in trono), key=lambda c: dist.get(c, 0))
    # saída do fim: célula atrás do salão, a mais de 12 m do centro do salão (o chefe), ligada por um corredor aberto
    tc = (sum(c[0] for c in trono) / len(trono), sum(c[1] for c in trono) / len(trono))
    vis, pai2, q = set(trono), {}, deque(trono)
    fundo = None
    while q:
        a = q.popleft()
        if a not in trono and a not in principal_set(caminho) and a != antessala and math.hypot(a[0] - tc[0], a[1] - tc[1]) * cel >= 12.5:
            fundo = a
            break
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            b = (a[0] + d[0], a[1] + d[1])
            if b in celulas and b not in vis and b not in principal_set(caminho):
                vis.add(b)
                pai2[b] = a
                q.append(b)
    if fundo is None:                       # sem célula livre atrás: usa a mais próxima (pelo labirinto) a 12,5 m do salão
        vis, q = set(trono), deque(trono)
        while q and fundo is None:
            a = q.popleft()
            for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                b = (a[0] + d[0], a[1] + d[1])
                if b in celulas and b not in vis and frozenset((a, b)) in aberto:
                    vis.add(b)
                    q.append(b)
                    if b != antessala and b != entrada and math.hypot(b[0] - tc[0], b[1] - tc[1]) * cel >= 12.5:
                        fundo = b
                        break
    elif fundo is not None:
        c, corredor = fundo, list(trono)
        while c in pai2:
            aberto.add(frozenset((c, pai2[c])))
            corredor.append(c)
            c = pai2[c]
        # o corredor dos fundos pode ter ligado o salão a um trecho raso: fecha outra parede até a ida voltar a ser longa
        salas.append(corredor)
        for _ in range(3):
            d_ = bfs(celulas, aberto, entrada)[0]
            reparar(celulas, aberto, entrada, min(trono, key=lambda t: d_.get(t, 9999)), minimo, salas)
        dist, pai = bfs(celulas, aberto, entrada)
        caminho, c = [], min(trono, key=lambda t: dist.get(t, 9999))
        while c is not None:
            caminho.append(c)
            c = pai[c]
        caminho.reverse()
        antessala = caminho[-2] if len(caminho) > 2 else caminho[0]
    return dict(n=n, cel=cel, celulas=celulas, aberto=aberto, entrada=entrada, trono=trono, antessala=antessala, ala=ala, fundo=fundo,
                guarda=guarda, lateral=lateral, caminho=caminho, centro=centro, dist=dist, x0=x0, alvo=alvo)


def paredes(P):
    """Caixas de parede (juntando segmentos em linha) entre células sem passagem e na borda do labirinto."""
    cel, x0, celulas, aberto = P["cel"], P["x0"], P["celulas"], P["aberto"]
    horiz, vert = {}, {}         # linha → lista de (início, fim)
    for (i, j) in celulas:
        for d in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            b = (i + d[0], j + d[1])
            if b in celulas and frozenset(((i, j), b)) in aberto:
                continue
            if b in celulas and (b < (i, j)):
                continue        # a parede entre duas células só uma vez
            if d[0] != 0:       # parede vertical em x = borda
                x = x0 + (i + (1 if d[0] > 0 else 0)) * cel
                vert.setdefault(round(x, 2), []).append((x0 + j * cel, x0 + (j + 1) * cel))
            else:
                y = x0 + (j + (1 if d[1] > 0 else 0)) * cel
                horiz.setdefault(round(y, 2), []).append((x0 + i * cel, x0 + (i + 1) * cel))
    out = []
    def juntar(segs):
        segs = sorted(segs)
        res = []
        for a, b in segs:
            if res and abs(res[-1][1] - a) < 1e-6:
                res[-1] = (res[-1][0], b)
            else:
                res.append((a, b))
        return res
    for y, segs in horiz.items():
        for a, b in juntar(segs):
            out.append({"kind": "muralha", "shape": "box", "x": round((a + b) / 2, 2), "y": y, "w": round(b - a + PAREDE, 2), "h": PAREDE, "r": 0, "height": ALTURA})
    for x, segs in vert.items():
        for a, b in juntar(segs):
            out.append({"kind": "muralha", "shape": "box", "x": x, "y": round((a + b) / 2, 2), "w": PAREDE, "h": round(b - a + PAREDE, 2), "r": 0, "height": ALTURA})
    return out


def caixa_de(cs, P, folga=0.0):
    xs = [P["centro"](c)[0] for c in cs]
    ys = [P["centro"](c)[1] for c in cs]
    h = P["cel"] / 2 - folga
    return min(xs) - h, max(xs) + h, min(ys) - h, max(ys) + h


def aplicar(m, P, rnd):
    centro, cel = P["centro"], P["cel"]
    m["obstacles"] = paredes(P)
    ex, ey = centro(P["entrada"])
    # porta de entrada encostada na parede norte da célula de entrada; o jogador nasce 2,5 m para dentro
    m["spawn_x"], m["spawn_y"] = ex, round(ey - cel * 0.3, 2)
    zonas = []
    def zona(id_, nome, kind, cs, folga=0.6):
        a, b, c, d = caixa_de(cs, P, folga)
        zonas.append({"id": id_, "name": nome, "kind": kind, "x": round((a + b) / 2, 2), "y": round((c + d) / 2, 2), "w": round(b - a, 2), "h": round(d - c, 2)})
    nomes = {z["id"]: z["name"] for z in m["zones"]}
    zona("entrada", nomes.get("entrada", "Vestíbulo"), "seguro", [P["entrada"]], 1.2)
    zona("ala", nomes.get("ala", "Ala"), "medio", P["ala"])
    zona("lateral", nomes.get("lateral", "Câmara Lateral"), "segredo", [P["lateral"]])
    zona("guarda", nomes.get("guarda", "Sala da Guarda"), "forte", P["guarda"])
    zona("antessala", nomes.get("antessala", "Antessala"), "seguro", [P["antessala"]], 1.4)
    zona("trono", nomes.get("trono", "Salão do Chefe"), "chefe", P["trono"])
    zona("labirinto", "Labirinto", "caminho", list(P["celulas"]), 0)
    m["zones"] = zonas[:-1] + [zonas[-1]]
    # portais primeiro (os bichos precisam ficar longe deles)
    tx = [centro(c) for c in P["trono"]]
    porta = centro(P["antessala"])
    if P.get("fundo") is not None:
        fim = centro(P["fundo"])
    else:
        fim = max(tx, key=lambda p: math.hypot(p[0] - porta[0], p[1] - porta[1]))
    for p in m["portals"]:
        if p["id"].endswith("_fim"):
            p["x"], p["y"] = round(fim[0], 2), round(fim[1], 2)
        else:
            p["x"], p["y"] = ex, round(ey + cel / 2 - 1.4, 2)
    seguras = [z for z in m["zones"] if z["kind"] == "seguro"]
    def ok(x, y, r=1.25):
        for o in m["obstacles"]:
            if abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r:
                return False
        if any(abs(x - z["x"]) <= z["w"] / 2 + 0.5 and abs(y - z["y"]) <= z["h"] / 2 + 0.5 for z in seguras):
            return False
        return all(math.hypot(x - p["x"], y - p["y"]) >= p["radius"] + 8.6 for p in m["portals"])
    # bichos
    def pontos(cs, k, raio):
        pts = []
        for t in range(2000):
            if len(pts) >= k:
                break
            c = rnd.choice(cs)
            x, y = centro(c)
            x += rnd.uniform(-raio, raio)
            y += rnd.uniform(-raio, raio)
            if ok(x, y) and all(math.hypot(x - a, y - b) > 1.8 for a, b in pts):
                pts.append((round(x, 1), round(y, 1)))
        todas = sorted(P["celulas"], key=lambda c: -P["dist"].get(c, 0))
        for c in todas:                       # sem lugar na sala: a célula livre mais funda do labirinto
            if len(pts) >= k:
                break
            x, y = centro(c)
            if ok(x, y) and all(math.hypot(x - a, y - b) > 1.8 for a, b in pts):
                pts.append((round(x, 1), round(y, 1)))
        while len(pts) < k:
            pts.append(centro(cs[len(pts) % len(cs)]))
        return pts
    grupos = {}
    for s in m["spawns"]:
        g = s.get("group", "")
        chave = "boss" if g.endswith("boss") or s["id"].endswith("boss") else "guarda" if "guarda" in g or s["id"].endswith("mini") else \
            "lateral" if "lateral" in g else "ala"
        grupos.setdefault(chave, []).append(s)
    lugar = {"ala": (P["ala"], cel * 0.3), "guarda": (P["guarda"], cel * 0.3), "lateral": ([P["lateral"]], cel * 0.22),
             "boss": (P["trono"], 0.0)}
    for chave, ss in grupos.items():
        cs, raio = lugar[chave]
        if chave == "boss":
            a, b, c, d = caixa_de(P["trono"], P)
            cand = [((a + b) / 2, (c + d) / 2)] + [centro(t) for t in P["trono"]]
            cand += [(x + dx, y + dy) for x, y in cand for dx in (-2, 0, 2) for dy in (-2, 0, 2)]
            for t in P["trono"]:                  # também pontos espalhados pelas células do salão
                x0, y0 = centro(t)
                cand += [(x0 + dx * cel / 3, y0 + dy * cel / 3) for dx in (-1, 0, 1) for dy in (-1, 0, 1)]
            boa = [q for q in cand if ok(q[0], q[1], 2.0)] or [q for q in cand if ok(q[0], q[1])]
            if not boa:
                livres = [q for q in cand if ok(q[0], q[1], 1.25) or all(not (abs(q[0] - o["x"]) < o["w"] / 2 + 1.3 and abs(q[1] - o["y"]) < o["h"] / 2 + 1.3) for o in m["obstacles"])]
                boa = sorted(livres or cand, key=lambda q: -min(math.hypot(q[0] - p["x"], q[1] - p["y"]) for p in m["portals"]))
                print("aviso", m.get("id"), "chefe sem lugar ideal", boa[0])
            q = boa[0]
            pts = [(round(q[0], 1), round(q[1], 1))] * len(ss)
        else:
            pts = pontos(cs, len(ss), raio)
        for s, (x, y) in zip(ss, pts):
            s["x"], s["y"] = x, y
            s.pop("alt_points", None)
    for c in m.get("checkpoints", []):
        c["x"], c["y"] = porta
    lx, ly = centro(P["lateral"])
    for b in m.get("chests", []):
        b["x"], b["y"] = round(lx, 2), round(ly - cel * 0.25, 2)
    # enfeites: tochas nas paredes do caminho principal, o resto espalhado nas salas e becos
    tochas = [p for p in m["props"] if p["kind"] == "tocha"]
    outros = [p for p in m["props"] if p["kind"] != "tocha"]
    for k, p in enumerate(tochas):
        c = P["caminho"][int(k * (len(P["caminho"]) - 1) / max(1, len(tochas) - 1))]
        x, y = centro(c)
        p["x"], p["y"] = round(x + cel * 0.38 * (1 if k % 2 else -1), 2), round(y, 2)
    lugares = P["ala"] + P["guarda"] + P["trono"] + [P["lateral"]] + rnd.sample(sorted(P["celulas"]), min(10, len(P["celulas"])))
    ocupados = [(s["x"], s["y"]) for s in m["spawns"]] + [(p["x"], p["y"]) for p in m["portals"]] + [(ex, ey)]
    for k, p in enumerate(outros):
        for _ in range(60):
            c = lugares[(k + _) % len(lugares)]
            x, y = centro(c)
            x += rnd.uniform(-cel * 0.32, cel * 0.32)
            y += rnd.uniform(-cel * 0.32, cel * 0.32)
            if all(math.hypot(x - a, y - b) > 2.2 for a, b in ocupados):
                break
        p["x"], p["y"] = round(x, 2), round(y, 2)
        ocupados.append((x, y))


def main():
    feitos = []
    for f in sorted(os.listdir(C)):
        if not (f.startswith("map_") and f.endswith(".json")):
            continue
        m = L(f)
        if m["id"] not in ESTILOS or not m.get("instanced"):
            continue
        P = planta(m, ESTILOS[m["id"]], "planta:" + m["id"])
        aplicar(m, P, random.Random("enfeites:" + m["id"]))
        D(f, m)
        # quem volta da dungeon pelo portal do campo cai no mesmo lugar de sempre (só mudou o lado de dentro)
        feitos.append((m["id"], len(P["celulas"]), len(P["caminho"]), len(m["obstacles"])))
    for x in feitos:
        print("planta", x[0], "células", x[1], "caminho até o chefe", x[2], "paredes", x[3])
    # portais dos campos que entram nas dungeons: chegada no novo ponto de entrada
    entradas = {}
    for f in os.listdir(C):
        if f.startswith("map_") and f.endswith(".json"):
            m = L(f)
            if m["id"] in ESTILOS and m.get("instanced"):
                entradas[m["id"]] = (m["spawn_x"], m["spawn_y"])
    for f in os.listdir(C):
        if not (f.startswith("map_") and f.endswith(".json")):
            continue
        m = L(f)
        mudou = False
        for p in m.get("portals", []):
            if p.get("to_map") in entradas and m["id"] != p["to_map"]:
                x, y = entradas[p["to_map"]]
                if (p.get("to_x"), p.get("to_y")) != (x, y):
                    p["to_x"], p["to_y"] = x, y
                    mudou = True
        if mudou:
            D(f, m)


if __name__ == "__main__":
    main()

"""Casaroes de 2 andares visitaveis (cidade e abandonado). Origem no chao (centro), frente -Y (Blender) => +Z glTF.
Nos filhos: '<id>_andar2' (piso de cima + paredes + moveis do 2o andar) e '<id>_telhado'.
Marcadores (empties): spawn_npc_N, spawn_monstro_N, spawn_bau_N. Colisao em <id>_colisao.glb."""
import sys, math, json, random
sys.path.insert(0, "/home/claude/cinzas/tools")
src = open("/home/claude/cinzas/tools/build_buildings.py").read()
exec(src[:src.index("OUT = []")])          # reaproveita materiais, Bld, banner, lantern, table_round, barrel, rubble
ROOT = "/home/claude/cinzas/assets/cenario/edificios/"
M.update(dict(
    plaster_d=cz.mat("ed_reboco_manchado", "#8a7a62", rough=0.95), wood_d=cz.mat("ed_madeira_podre", "#3a2a1e", rough=0.9),
    web=cz.mat("ed_teia", "#d8d4cc", rough=0.9), bed=cz.mat("ed_lencol", "#c8b89a", rough=0.95), blanket=cz.mat("ed_cobertor", "#5a2a3a", rough=0.95),
    violet=cz.mat("ed_vela_violeta_emit", "#d0a0ff", rough=0.3, emit="#9a3cff", strength=6)))

def furniture_bed(b, x, y, rot, g, broken=False):
    r = Matrix.Rotation(rot, 4, 'Z')
    def P(dx, dy): v = r @ Vector((dx, dy, 0)); return x + v.x, y + v.y
    px, py = P(0, 0)
    b.add(bx("cama", (1.3, 2.2, 0.35), (px, py, 4.4 + 0.45), M["wood2"], 0.04, rot=(0.15 if broken else 0, 0, rot)), g)
    b.add(bx("colchao", (1.2, 2.0, 0.18), (px, py, 4.4 + 0.72), M["bed"], 0.08, rot=(0.15 if broken else 0, 0, rot)), g)
    qx, qy = P(0, 0.3); b.add(bx("cobertor", (1.26, 1.3, 0.08), (qx, qy, 4.4 + 0.83), M["blanket"], 0.04, rot=(0, 0, rot)), g)
    hx, hy = P(0, -1.05); b.add(bx("cabeceira", (1.4, 0.12, 1.2), (hx, hy, 4.4 + 0.9), M["wood2"], 0.04, rot=(0, 0, rot)), g)
    tx, ty = P(0, -0.8); b.add(bx("travesseiro", (0.9, 0.35, 0.14), (tx, ty, 4.4 + 0.86), M["bed"], 0.06, rot=(0, 0, rot)), g)

def chest(b, x, y, z, rot, g):
    b.add(bx("bau", (1.0, 0.6, 0.5), (x, y, z + 0.25), M["wood"], 0.04, rot=(0, 0, rot)), g)
    b.add(cz.cyl("tampaBau", 0.3, 1.02, 12, loc=(x, y, z + 0.5), rot=(0, math.pi / 2, rot), m=M["wood2"]), g)
    b.add(bx("fechoBau", (0.12, 0.66, 0.12), (x, y, z + 0.6), M["gold"], 0.02, rot=(0, 0, rot)), g)

def marker(name, loc, root_list):
    e = bpy.data.objects.new(name, None); cz._link(e); e.empty_display_type = 'ARROWS'; e.empty_display_size = 0.6
    e.location = loc; root_list.append(e); return e

def casarao(aid, nome, style):
    ab = style == "abandonado"
    rnd = random.Random(11 if ab else 5)
    b = Bld(aid); markers = []
    W, D, TH, H1, H2 = 16.0, 12.0, 0.55, 4.0, 3.4
    Z0 = 0.4; Z1 = Z0 + H1           # piso terreo e piso do 2o andar
    wallm = M["stone3"] if ab else M["stone"]
    # ---------------------------------------------------------- base e piso
    b.add(bx("embasamento", (W + 0.8, D + 0.8, Z0), (0, 0, Z0 / 2), M["stone3"], 0.1)); b.colbox((W, D, Z0), (0, 0, Z0 / 2))
    b.add(bx("assoalho", (W - 2 * TH, D - 2 * TH, 0.06), (0, 0, Z0 + 0.03), M["wood_d"] if ab else M["plank"], 0.01))
    for i in range(int((W - 1) / 0.45)):
        if ab and rnd.random() < 0.08: continue
        b.add(bx("tabua", (0.035, D - 2 * TH - 0.1, 0.02), (-W / 2 + TH + 0.22 + i * 0.45, 0, Z0 + 0.065), M["wood2"], 0.0))
    for k, (w_, d_) in enumerate(((4.2, 1.2), (3.6, 0.7))):
        b.add(bx(f"degrau{k}", (w_, d_, Z0 / 2), (0, -D / 2 - 0.3 - d_ / 2 + 0.4 * k, Z0 / 4 + Z0 / 2 * k), M["stone2"], 0.06))
    # ---------------------------------------------------------- terreo (pedra)
    door = (0.0, 3.2, Z0, Z0 + 3.4)
    b.wall('x', -W / 2, W / 2, -D / 2 + TH / 2, Z0, Z0 + H1, TH, wallm, [door, (-4.6, 1.4, Z0 + 1.2, Z0 + 2.9), (4.6, 1.4, Z0 + 1.2, Z0 + 2.9)])
    b.arch('x', 0.0, -D / 2 + TH / 2, Z0 + 3.4 - 1.6, 3.2, TH, M["stone2"], n=11)
    b.wall('x', -W / 2, W / 2, D / 2 - TH / 2, Z0, Z0 + H1, TH, wallm, [(-3.0, 1.3, Z0 + 1.2, Z0 + 2.9), (3.8, 1.3, Z0 + 1.2, Z0 + 2.9)])
    b.wall('y', -D / 2 + TH, D / 2 - TH, -W / 2 + TH / 2, Z0, Z0 + H1, TH, wallm, [(2.5, 1.3, Z0 + 1.2, Z0 + 2.9)])
    b.wall('y', -D / 2 + TH, D / 2 - TH, W / 2 - TH / 2, Z0, Z0 + H1, TH, wallm, [(-2.8, 1.3, Z0 + 1.2, Z0 + 2.9)])
    for (axis, c, fx) in (('x', -4.6, -D / 2), ('x', 4.6, -D / 2), ('x', -3.0, D / 2), ('x', 3.8, D / 2), ('y', 2.5, -W / 2), ('y', -2.8, W / 2)):
        f_ = fx + (TH / 2 if fx < 0 else -TH / 2)
        b.arch(axis, c, f_, Z0 + 2.9 - 0.65, 1.4, TH, M["stone2"], n=7)
        if ab:   # janelas tapadas com tabuas
            for k in range(3):
                loc = (c, f_ - 0.3 * (1 if fx < 0 else -1), Z0 + 1.6 + k * 0.45) if axis == 'x' else (f_ - 0.3 * (1 if fx < 0 else -1), c, Z0 + 1.6 + k * 0.45)
                b.add(bx("tabuaJ", (1.7, 0.06, 0.22) if axis == 'x' else (0.06, 1.7, 0.22), loc, M["wood_d"], 0.02, rot=((0, rnd.uniform(-0.25, 0.25), 0) if axis == 'x' else (rnd.uniform(-0.25, 0.25), 0, 0))))
        else:
            for k in range(3):
                loc = (c - 0.4 + k * 0.4, f_, Z0 + 2.05) if axis == 'x' else (f_, c - 0.4 + k * 0.4, Z0 + 2.05)
                b.add(cz.cyl("grade", 0.025, 1.7, 6, loc=loc, m=M["iron"]))
    for sx in (-1, 1):
        for sy in (-1, 1):
            for k in range(7):
                big = k % 2 == 0
                b.add(bx("cunhal", (0.8 if big else 0.6, 0.6 if big else 0.8, 0.52), (sx * (W / 2 - 0.2), sy * (D / 2 - 0.2), Z0 + 0.3 + k * 0.56), M["stone2"], 0.06))
            b.add(bx("contraforte", (0.8, 1.0, 2.6), (sx * (W / 2 + 0.25), sy * (D / 2 - 1.5), Z0 + 1.2), M["stone3"], 0.1, rot=(0, sx * 0.12, 0)))
    for (sz, lc) in (((W + 0.4, 0.75, 0.32), (0, -D / 2 + 0.2, Z1 - 0.1)), ((W + 0.4, 0.75, 0.32), (0, D / 2 - 0.2, Z1 - 0.1)),
                     ((0.75, D + 0.4, 0.32), (-W / 2 + 0.2, 0, Z1 - 0.1)), ((0.75, D + 0.4, 0.32), (W / 2 - 0.2, 0, Z1 - 0.1))):
        b.add(bx("cinta", sz, lc, M["stone2"], 0.06))
    # colunas internas que sustentam o 2o andar
    for x in (-2.6, 2.6):
        b.add(cz.cyl("pilar", 0.24, H1, 12, loc=(x, 0.8, Z0 + H1 / 2), m=M["wood2"])); b.colbox((0.5, 0.5, H1), (x, 0.8, Z0 + H1 / 2))
        b.add(bx("baseP", (0.7, 0.7, 0.3), (x, 0.8, Z0 + 0.15), M["stone2"], 0.05))
        b.add(bx("mao_francesa", (0.18, 1.6, 0.18), (x, 0.8, Z1 - 0.5), M["wood2"], 0.03), "andar2")
    for x in (-5.4, -2.6, 0.0, 2.6, 5.4):
        b.add(bx("viga_teto", (0.28, D - 2 * TH, 0.3), (x, 0, Z1 - 0.28), M["wood2"], 0.03), "andar2")
    # alpendre + sacada no 2o andar
    for x in (-2.0, 2.0):
        b.add(cz.cyl("colunaAlp", 0.2, H1, 12, loc=(x, -D / 2 - 1.9, Z0 / 2 + H1 / 2), m=M["wood2"])); b.colbox((0.45, 0.45, H1), (x, -D / 2 - 1.9, H1 / 2))
        b.add(bx("baseAlp", (0.55, 0.55, 0.35), (x, -D / 2 - 1.9, 0.35), M["stone2"], 0.05))
    b.add(bx("pisoSacada", (5.0, 2.4, 0.25), (0, -D / 2 - 1.1, Z1 + 0.0), M["wood2"], 0.04))
    b.colbox((5.0, 2.4, 0.25), (0, -D / 2 - 1.1, Z1))
    # ---------------------------------------------------------- interior do terreo
    if not ab:
        b.add(bx("balcao", (4.2, 0.9, 1.15), (-4.2, D / 2 - 2.0, Z0 + 0.58), M["wood"], 0.05)); b.colbox((4.2, 0.9, 1.15), (-4.2, D / 2 - 2.0, Z0 + 0.58))
        b.add(bx("tampoBalcao", (4.4, 1.05, 0.1), (-4.2, D / 2 - 2.0, Z0 + 1.2), M["wood2"], 0.03))
        for k in range(3):
            z = Z0 + 1.5 + k * 0.6
            b.add(bx("prat", (4.0, 0.4, 0.06), (-4.2, D / 2 - TH - 0.2, z), M["wood2"], 0.01))
            for j in range(10):
                b.add(cz.cyl("garrafa", 0.07, 0.3, 8, r2=0.03, loc=(-6.0 + j * 0.4, D / 2 - TH - 0.2, z + 0.18), m=[M["bottle"], M["bottle2"], M["wood2"]][(j + k) % 3]))
        b.add(bx("mesaLonga", (1.4, 4.2, 0.12), (3.0, -1.2, Z0 + 0.85), M["wood"], 0.04)); b.colbox((1.4, 4.2, 0.9), (3.0, -1.2, Z0 + 0.45))
        for dy in (-2.8, 0.4):
            b.add(bx("pernaMesa", (1.2, 0.15, 0.8), (3.0, -1.2 + dy / 2 + 0.6, Z0 + 0.4), M["wood2"], 0.02))
        for sx in (-1, 1):
            b.add(bx("banco", (0.4, 4.0, 0.1), (3.0 + sx * 1.1, -1.2, Z0 + 0.5), M["wood"], 0.03))
        for j in range(4):
            b.add(cz.cyl("caneca", 0.07, 0.14, 8, loc=(3.0 + (0.3 if j % 2 else -0.3), -2.8 + j * 1.0, Z0 + 0.98), m=M["wood2"]))
            b.add(cz.cyl("vela", 0.04, 0.14, 8, loc=(3.0, -2.5 + j * 1.0, Z0 + 0.98), m=M["candle"]))
        table_round(b, -1.5, -2.6); b.colbox((1.3, 1.3, 0.9), (-1.5, -2.6, 0.8))
        for x in (-1.4, -0.7):
            barrel(b, x, D / 2 - 1.0)
        b.add(bx("tapete", (4.0, 3.0, 0.02), (-0.5, -0.4, Z0 + 0.08), M["rug"], 0.01))
    else:
        b.add(bx("balcaoQ", (4.2, 0.9, 1.15), (-4.2, D / 2 - 2.0, Z0 + 0.4), M["wood_d"], 0.05, rot=(0.3, 0, 0.1))); b.colbox((4.2, 0.9, 1.15), (-4.2, D / 2 - 2.0, Z0 + 0.58))
        b.add(bx("mesaTombada", (1.4, 4.2, 0.12), (3.2, -1.0, Z0 + 0.7), M["wood_d"], 0.04, rot=(1.3, 0, 0.2))); b.colbox((1.4, 0.6, 1.4), (3.2, -1.0, Z0 + 0.7))
        table_round(b, -1.5, -2.6, broken=True)
        barrel(b, -1.0, D / 2 - 1.2, lying=True); barrel(b, 4.5, 3.5, lying=True)
        rubble(b, 5.5, 3.0, 1.2, 10, 3); rubble(b, -3.0, -3.5, 0.9, 7, 4)
        for (p0, p1) in (((-6.5, 4.0, Z1 - 0.2), (-5.5, 3.2, Z0 + 0.3)), ((6.8, -4.5, Z1 - 0.3), (5.6, -3.2, Z0 + 0.2))):
            b.add(cz.seg("vigaCaida", p0, p1, 0.14, 0.14, 6, m=M["wood_d"]))
        for (x, y) in ((-7.4, 5.4), (7.4, 5.4), (7.4, -5.4), (-2.6, 0.8)):   # teias nos cantos
            for k in range(3):
                b.add(cz.prism("teia", [(0, 0), (1.2, 0), (0, 1.2)], 0.01, loc=(x, y, Z1 - 0.3 - k * 0.15), rot=(math.pi / 2 * 0.0, 0.4, rnd.uniform(0, 6)), m=M["web"]))
        for (x, y) in ((-4.0, 4.6), (-3.2, 4.6), (2.0, 3.4), (-6.8, -1.5)):
            b.add(cz.cyl("velaV", 0.05, 0.25, 8, loc=(x, y, Z0 + (1.27 if y > 4 else 0.15)), m=M["violet"]))
    # lareira (parede direita) com chamine que atravessa os andares
    fx_ = W / 2 - TH - 0.5
    b.add(bx("lareira", (1.0, 2.8, 2.4), (fx_, 1.6, Z0 + 1.2), M["stone2"], 0.06)); b.colbox((1.0, 2.8, 2.4), (fx_, 1.6, Z0 + 1.2))
    b.add(bx("bocaL", (0.5, 1.6, 1.1), (fx_ - 0.3, 1.6, Z0 + 0.6), M["stone3"], 0.04))
    b.add(bx("fogo", (0.35, 1.1, 0.25), (fx_ - 0.4, 1.6, Z0 + 0.2), M["violet"] if ab else M["fire"], 0.05))
    b.add(bx("consolo", (1.2, 3.0, 0.16), (fx_ - 0.05, 1.6, Z0 + 2.5), M["wood2"], 0.03))
    if not ab:
        b.add(cz.cyl("lustre", 1.1, 0.08, 16, loc=(0, -1.0, Z1 - 1.0), m=M["iron"], cap=False))
        for i in range(10):
            a = i * math.tau / 10; b.add(cz.cyl("velaL", 0.04, 0.16, 8, loc=(math.cos(a) * 1.1, -1.0 + math.sin(a) * 1.1, Z1 - 0.9), m=M["candle"]))
        for i in range(3):
            a = i * math.tau / 3; b.add(cz.seg("correnteL", (math.cos(a) * 1.1, -1.0 + math.sin(a) * 1.1, Z1 - 0.95), (0, -1.0, Z1 - 0.45), 0.015, 0.015, 6, m=M["iron"]))
    # escada larga (frente-esquerda subindo para o fundo)
    SX, SW, N = -W / 2 + TH + 0.95, 1.7, 14
    y0 = -D / 2 + TH + 0.5; rise, run = H1 / N, 0.31
    for k in range(N):
        b.add(bx("degrau", (SW, run + 0.02, 0.14), (SX, y0 + k * run + run / 2, Z0 + (k + 1) * rise - 0.07), M["wood_d"] if ab else M["wood"], 0.02))
        b.add(bx("espelho", (SW, 0.04, rise), (SX, y0 + k * run, Z0 + k * rise + rise / 2), M["wood2"], 0.0))
    ang = math.atan2(H1, N * run); Ls = math.hypot(H1, N * run)
    b.add(bx("longarina", (0.14, Ls, 0.35), (SX + SW / 2 + 0.07, y0 + N * run / 2, Z0 + H1 / 2 - 0.1), M["wood2"], 0.03, rot=(ang, 0, 0)))
    for k in range(0, N, 2):   # balaustres
        b.add(cz.cyl("balaustre", 0.04, 0.95, 8, loc=(SX + SW / 2 + 0.07, y0 + k * run + run / 2, Z0 + (k + 1) * rise + 0.45), m=M["wood2"]))
    b.add(bx("corrimao", (0.1, Ls, 0.1), (SX + SW / 2 + 0.07, y0 + N * run / 2, Z0 + H1 / 2 + 0.95), M["wood"], 0.03, rot=(ang, 0, 0)))
    b.colbox((SW, Ls, 0.3), (SX, y0 + N * run / 2, Z0 + H1 / 2 - 0.05), rot=(ang, 0, 0))
    stair_top_y = y0 + N * run
    # ---------------------------------------------------------- 2o ANDAR (no 'andar2')
    G2 = "andar2"
    hole = (SX - SW / 2 - 0.05, SX + SW / 2 + 0.1, y0 - 0.1, stair_top_y + 0.05)   # vao da escada
    xA, xB = -W / 2, W / 2; yA, yB = -D / 2, D / 2
    for (x0, x1, yy0, yy1) in ((xA, hole[0], yA, yB), (hole[1], xB, yA, yB), (hole[0], hole[1], hole[3], yB), (hole[0], hole[1], yA, hole[2])):
        if x1 - x0 < 0.05 or yy1 - yy0 < 0.05: continue
        sz = (x1 - x0, yy1 - yy0, 0.3); lc = ((x0 + x1) / 2, (yy0 + yy1) / 2, Z1)
        b.add(bx("piso2", sz, lc, M["wood2"], 0.03), G2); b.colbox(sz, lc)
    for i in range(int(W / 0.45)):
        x = -W / 2 + 0.3 + i * 0.45
        if hole[0] - 0.05 < x < hole[1] + 0.05:
            for (yy0, yy1) in ((yA + 0.3, hole[2]), (hole[3], yB - 0.3)):
                if yy1 - yy0 > 0.1: b.add(bx("tabua2", (0.4, yy1 - yy0, 0.02), (x, (yy0 + yy1) / 2, Z1 + 0.16), M["plank"] if not ab else M["wood_d"], 0.0), G2)
        else:
            if ab and rnd.random() < 0.1: continue
            b.add(bx("tabua2", (0.4, D - 0.6, 0.02), (x, 0, Z1 + 0.16), M["plank"] if not ab else M["wood_d"], 0.0), G2)
    # guarda-corpo do vao
    for (p0, p1) in (((hole[1], hole[2]), (hole[1], hole[3] - 1.0)), ((hole[0], hole[2]), (hole[1], hole[2]))):
        L_ = math.hypot(p1[0] - p0[0], p1[1] - p0[1]); n_ = max(2, int(L_ / 0.35))
        for k in range(n_ + 1):
            t = k / n_; b.add(cz.cyl("balaustre2", 0.04, 0.95, 8, loc=(p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, Z1 + 0.62), m=M["wood2"]), G2)
        b.add(cz.seg("corrimao2", (p0[0], p0[1], Z1 + 1.12), (p1[0], p1[1], Z1 + 1.12), 0.05, 0.05, 8, m=M["wood"]), G2)
        b.colbox((abs(p1[0] - p0[0]) + 0.1, abs(p1[1] - p0[1]) + 0.1, 1.0), ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, Z1 + 0.65))
    # paredes do 2o andar (enxaimel) com porta da sacada
    Z2 = Z1 + 0.15; plast = M["plaster_d"] if ab else M["plaster"]
    walls2 = (('x', -W / 2, W / 2, -D / 2 + 0.15, [(-5.0, 1.1, Z2 + 0.9, Z2 + 2.3), (5.0, 1.1, Z2 + 0.9, Z2 + 2.3), (0.0, 1.6, Z2, Z2 + 2.6)]),
              ('x', -W / 2, W / 2, D / 2 - 0.15, [(-4.0, 1.1, Z2 + 0.9, Z2 + 2.3), (4.0, 1.1, Z2 + 0.9, Z2 + 2.3)]),
              ('y', -D / 2, D / 2, -W / 2 + 0.15, [(3.0, 1.1, Z2 + 0.9, Z2 + 2.3)]), ('y', -D / 2, D / 2, W / 2 - 0.15, [(-3.0, 1.1, Z2 + 0.9, Z2 + 2.3), (3.5, 1.1, Z2 + 0.9, Z2 + 2.3)]))
    for axis, a0, a1, fx, ops in walls2:
        b.wall(axis, a0, a1, fx, Z2, Z2 + H2, 0.3, plast, ops, group=G2, quoin=M["wood2"])
        for (c, w_, zb, zt) in ops:
            if zb > Z2 + 0.1:
                loc = (c, fx, (zb + zt) / 2) if axis == 'x' else (fx, c, (zb + zt) / 2)
                if ab:
                    b.add(bx("vidroQ", (w_, 0.05, zt - zb) if axis == 'x' else (0.05, w_, zt - zb), loc, M["wood_d"], 0.01, rot=(0, 0.1, 0)), G2)
                else:
                    b.add(bx("vidro", (w_, 0.06, zt - zb) if axis == 'x' else (0.06, w_, zt - zb), loc, M["glow"], 0.01), G2)
        n = int((a1 - a0) / 1.6)
        for i in range(n + 1):
            t = a0 + i * (a1 - a0) / n
            loc = (t, fx, Z2 + H2 / 2) if axis == 'x' else (fx, t, Z2 + H2 / 2)
            b.add(bx("montante", (0.22, 0.4, H2) if axis == 'x' else (0.4, 0.22, H2), loc, M["wood2"], 0.03), G2)
        for zz in (Z2 + 0.1, Z2 + H2 - 0.1):
            loc = ((a0 + a1) / 2, fx, zz) if axis == 'x' else (fx, (a0 + a1) / 2, zz)
            b.add(bx("travessa", (a1 - a0, 0.42, 0.2) if axis == 'x' else (0.42, a1 - a0, 0.2), loc, M["wood2"], 0.03), G2)
    # divisoria: quarto ao fundo-direita com porta
    b.wall('y', 1.2, D / 2 - 0.3, 0.6, Z2, Z2 + H2, 0.2, plast, [(3.4, 1.1, Z2, Z2 + 2.3)], group=G2, quoin=M["wood2"])
    b.wall('x', 0.6, W / 2 - 0.3, 1.2, Z2, Z2 + H2, 0.2, plast, [(3.0, 1.1, Z2, Z2 + 2.3)], group=G2, quoin=M["wood2"])
    # sacada: guarda-corpo
    for x in [(-2.4 + i * 0.4) for i in range(13)]:
        b.add(cz.cyl("balSac", 0.045, 1.0, 8, loc=(x, -D / 2 - 2.2, Z1 + 0.62), m=M["wood2"]), G2)
    b.add(bx("corrSac", (5.0, 0.14, 0.12), (0, -D / 2 - 2.2, Z1 + 1.15), M["wood"], 0.03), G2)
    b.colbox((5.0, 0.2, 1.1), (0, -D / 2 - 2.25, Z1 + 0.65))
    # moveis do 2o andar
    if not ab:
        furniture_bed(b, 5.6, 4.2, 0.0, G2); furniture_bed(b, 3.0, 4.2, 0.0, G2)
        b.add(bx("armario", (1.4, 0.6, 2.2), (6.9, 2.0, Z1 + 1.25), M["wood"], 0.04, rot=(0, 0, math.pi / 2)), G2)
        b.add(bx("mesa2", (1.6, 0.9, 0.08), (-3.0, 4.2, Z1 + 0.95), M["wood"], 0.03), G2)
        b.add(cz.cyl("vela2", 0.04, 0.16, 8, loc=(-3.0, 4.2, Z1 + 1.07), m=M["candle"]), G2)
        b.add(bx("tapete2", (3.0, 2.0, 0.02), (-2.0, -2.0, Z1 + 0.18), M["rug"], 0.01), G2)
        for k in range(2):
            barrel(b, -6.8 + k * 0.8, 5.2, z=Z1 + 0.15, group=G2)
    else:
        furniture_bed(b, 5.6, 4.2, 0.3, G2, broken=True)
        b.add(bx("armarioCaido", (1.4, 0.6, 2.2), (6.0, 2.2, Z1 + 0.5), M["wood_d"], 0.04, rot=(1.45, 0, 0.4)), G2)
        rubble(b, -3.5, 3.5, 1.0, 8, 9, group=G2)
        for o in [o for o in b.groups[G2][-8:]]: o.location.z += Z1
        for (x, y) in ((-6.5, 4.5), (6.8, -4.8)):
            for k in range(3):
                b.add(cz.prism("teia2", [(0, 0), (1.3, 0), (0, 1.3)], 0.01, loc=(x, y, Z2 + H2 - 0.3 - k * 0.12), rot=(0, 0.4, rnd.uniform(0, 6)), m=M["web"]), G2)
        b.add(cz.cyl("velaV2", 0.05, 0.25, 8, loc=(4.6, 3.2, Z1 + 0.3), m=M["violet"]), G2)
    chest(b, 6.3, 5.0, Z1 + 0.15, 0.0, G2)
    # ---------------------------------------------------------- TELHADO (no 'telhado')
    GT = "telhado"
    ridge = b.roof(W + 0.8, D + 0.8, Z2 + H2, 4.2, 0.8, M["roof2"] if ab else M["roof"], M["roof2"], group=GT, along='x')
    if ab:   # rombos no telhado: remove telhas aleatorias
        tiles = [o for o in b.groups[GT] if o.name.startswith("telha")]
        for o in tiles:
            if rnd.random() < 0.12 or (abs(o.location.x - 3.0) < 1.5 and o.location.y < 0):
                b.groups[GT].remove(o); bpy.data.objects.remove(o)
    for x in (-W / 2 - 0.45, W / 2 + 0.45):
        o = cz.prism("oitao", [(-(D + 0.8) / 2, 0), ((D + 0.8) / 2, 0), (0, 4.2)], 0.3, m=plast); cz.apply_tf(o)
        for vv in o.data.vertices: vv.co = Vector((x - 0.15 + vv.co.z, vv.co.x, Z2 + H2 + vv.co.y))
        b.add(o, GT)
        b.add(bx("janOitao", (0.1, 1.0, 1.0), (x + (0.12 if x < 0 else -0.12) * -1, 0, Z2 + H2 + 1.4), M["wood_d"] if ab else M["glow"], 0.02, rot=(0.785, 0, 0)), GT)
    for x in (-4.0, 4.0):
        zb = Z2 + H2 + 0.9
        b.add(bx("mans", (1.8, 1.8, 1.5), (x, -D / 2 + 0.7, zb + 0.6), plast, 0.04), GT)
        b.add(bx("mansJ", (0.9, 0.08, 0.9), (x, -D / 2 - 0.22, zb + 0.6), M["wood_d"] if ab else M["glow"], 0.02), GT)
        b.add(bx("mansT1", (1.4, 2.2, 0.12), (x - 0.52, -D / 2 + 0.6, zb + 1.62), M["roof2"], 0.03, rot=(0, -0.7, 0)), GT)
        b.add(bx("mansT2", (1.4, 2.2, 0.12), (x + 0.52, -D / 2 + 0.6, zb + 1.62), M["roof2"], 0.03, rot=(0, 0.7, 0)), GT)
    b.add(bx("chamine", (1.1, 1.3, Z2 + H2 + 5.5 - Z1), (W / 2 - 0.8, 1.6, (Z1 + Z2 + H2 + 5.5) / 2), M["stone"], 0.06), GT)
    b.add(bx("chamTopo", (1.4, 1.6, 0.3), (W / 2 - 0.8, 1.6, Z2 + H2 + 5.5), M["stone2"], 0.05), GT)
    if not ab:
        b.add(bx("chamBrasa", (0.6, 0.8, 0.05), (W / 2 - 0.8, 1.6, Z2 + H2 + 5.66), M["fire"], 0.02), GT)
    # fachada: estandartes e lanternas (terreo, no corpo principal)
    banner(b, -2.4, -D / 2, Z0 + 3.4, 2.0, M["cloth2"] if ab else M["cloth"]); banner(b, 2.4, -D / 2, Z0 + 3.4, 2.0, M["cloth2"] if ab else M["cloth"])
    if not ab:
        lantern(b, -2.0, -D / 2 - 1.9, Z0 + 2.4, face=0); lantern(b, 2.0, -D / 2 - 1.9, Z0 + 2.4, face=0)
    # marcadores de spawn
    if not ab:
        mk = [("spawn_npc_1", (-4.2, D / 2 - 3.0, Z0)), ("spawn_npc_2", (5.5, 1.6, Z0)), ("spawn_npc_3", (-2.0, -2.0, Z1 + 0.18)),
              ("spawn_bau_1", (6.3, 5.0, Z1 + 0.15)), ("spawn_monstro_1", (0.0, 3.5, Z0))]
    else:
        mk = [("spawn_monstro_1", (0.0, 0.0, Z0)), ("spawn_monstro_2", (4.0, -2.5, Z0)), ("spawn_monstro_3", (-2.0, -2.0, Z1 + 0.18)),
              ("spawn_monstro_4", (3.6, 3.6, Z1 + 0.18)), ("spawn_bau_1", (6.3, 5.0, Z1 + 0.15)), ("spawn_npc_1", (-4.5, 3.2, Z0))]
    for nm_, loc in mk: marker(nm_, loc, markers)
    root, groups, col, nome_, notas = b.finish(nome, "")
    for e in markers: e.parent = root
    return root, groups, col, markers, nome

info = []
for aid, nome, style in (("edificio_casarao_cidade", "Casarão da Cidade", "cidade"), ("ruina_casarao_abandonado", "Casarão Abandonado", "abandonado")):
    root, groups, col, markers, nome = casarao(aid, nome, style)
    objs = [root] + list(root.children)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=ROOT + root.name + ".glb", export_format='GLB', export_yup=True, use_selection=True,
                              export_animations=False, export_materials='EXPORT')
    cz.export_objs([col], ROOT + root.name + "_colisao")
    meshes = [o for o in objs if o.type == 'MESH']
    info.append(dict(asset_id=root.name, nome=nome, tris=cz.tri_count(meshes), dims_m=[16.8, 15.2, round(max((o.matrix_world @ Vector(c)).z for o in meshes for c in o.bound_box), 2)],
                     nos=[c.name for c in root.children if c.type == 'MESH'], marcadores=[m.name for m in markers], caixas_colisao=len(col.data.polygons) // 6,
                     notas="2 andares visitaveis; esconder '_telhado' quando o jogador estiver no 2o andar e '_andar2' + '_telhado' quando estiver no terreo"))
    col.hide_render = True; col.hide_set(True)
    for m_ in markers: m_.name = aid[:6] + "_" + m_.name    # libera os nomes para a proxima casa (so no .blend)
for i in info: print(json.dumps(i, ensure_ascii=False))
json.dump(info, open(ROOT + "_casaroes_info.json", "w"), ensure_ascii=False, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=ROOT + "casaroes_lote1.blend")
print("DONE")

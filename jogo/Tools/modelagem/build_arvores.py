import sys; sys.path.insert(0, "/home/claude/cinzas/tapa"); sys.path.insert(0, "/home/claude/cinzas/tools")
import bpy, math, random
from mathutils import Vector
import itemkit as K, kit_folha as F
OUT = "/home/claude/cinzas/tapa/out/"

def rv(rng, a, b): return rng.uniform(a, b)

def arbusto(nome, seed, W=1.8, H=1.25):
    K.reset(); rng = random.Random(seed)
    casca = F.m_casca("casca", "#5b4a39", "#2a1f16", "#867461", 2.0)
    tr = []; fo = F.Folhas()
    cc = Vector((0, 0, H * 0.45))
    for i in range(9):
        a = rng.uniform(0, math.tau); r = rng.uniform(0.15, 0.5) * W / 2
        tip = Vector((math.cos(a) * r * 1.6, math.sin(a) * r * 1.2, H * rng.uniform(0.55, 0.85)))
        mid = tip * 0.5 + Vector((rv(rng, -.1, .1), rv(rng, -.1, .1), 0))
        tr.append(F.tronco(f"g{i}", [(0, 0, 0), tuple(mid), tuple(tip)], 0.045, 0.012, casca, res=6))
    for i in range(150):
        # ponto na superfície de um elipsoide achatado, mais denso em cima
        u = rng.uniform(0, math.tau); v = math.acos(rng.uniform(-0.35, 1))
        p = Vector((math.sin(v) * math.cos(u) * W / 2, math.sin(v) * math.sin(u) * W * 0.38, H * 0.42 + math.cos(v) * H * 0.55))
        p *= rng.uniform(0.7, 1.0); p.z = max(p.z, 0.08)
        out = (p - cc); out.z += 0.25; out.normalize()
        d = (out + Vector((rv(rng, -.5, .5), rv(rng, -.5, .5), rv(rng, -.2, .6)))).normalized()
        s = rng.uniform(0.42, 0.62)
        fo.card(p - d * s * 0.45, d, s, roll=rng.uniform(0, math.tau), centro=cc)
    folhas = fo.objeto(nome + "_folhas", F.m_folha("folha_arbusto", "folha_arbusto.png"), cc)
    F.exportar(OUT + nome + ".glb", tr, [folhas], tex=512, tris_tronco=1200)

def pinheiro(nome, seed, H=6.6, R=2.9):
    K.reset(); rng = random.Random(seed)
    casca = F.m_casca("casca_pinho", "#6a4b36", "#2b1b12", "#a07a5a", 1.0)
    tr = []; fo = F.Folhas()
    lean = Vector((rv(rng, -.6, .6), rv(rng, -.6, .6), 0))
    top = Vector((0, 0, H * 0.62)) + lean
    tr.append(F.tronco("tronco", [(0, 0, 0), tuple(Vector((0, 0, H * .25)) + lean * .2), tuple(Vector((0, 0, H * .45)) + lean * .6), tuple(top)], 0.32, 0.17, casca, res=12))
    # raízes
    for i in range(4):
        a = i * math.tau / 4 + rv(rng, -.4, .4)
        tr.append(F.tronco(f"r{i}", [(0, 0, 0.35), (math.cos(a) * .45, math.sin(a) * .45, 0.08), (math.cos(a) * .75, math.sin(a) * .75, -0.05)], 0.14, 0.04, casca, res=6))
    clumps = []
    nl = rng.randint(4, 5)
    for i in range(nl):
        a = i * math.tau / nl + rv(rng, -.35, .35); r = R * rv(rng, 0.55, 0.85)
        end = top + Vector((math.cos(a) * r, math.sin(a) * r, H * rv(0.2, 0.3) if False else H * rng.uniform(0.2, 0.3)))
        mid = top + (end - top) * 0.5 + Vector((0, 0, 0.35))
        tr.append(F.tronco(f"l{i}", [tuple(top - Vector((0, 0, 0.3))), tuple(mid), tuple(end)], 0.16, 0.06, casca, res=8))
        for j in range(rng.randint(3, 4)):
            b = a + rv(rng, -.9, .9); rr = rng.uniform(0.6, 1.4)
            c = end + Vector((math.cos(b) * rr, math.sin(b) * rr, rv(rng, -.2, .35)))
            tr.append(F.tronco(f"t{i}{j}", [tuple(end), tuple(c)], 0.05, 0.02, casca, res=5)); clumps.append(c)
        clumps.append(end + Vector((0, 0, 0.25)))
    cc = top + Vector((0, 0, H * 0.3))
    for c in clumps:
        for k in range(14):
            d = Vector((rv(rng, -1, 1), rv(rng, -1, 1), rv(rng, 0.0, 1.1))).normalized()
            s = rng.uniform(1.0, 1.5)
            fo.card(c - d * s * 0.35 + Vector((0, 0, rv(rng, -.1, .15))), d, s, roll=rng.uniform(0, math.tau), centro=cc)
    folhas = fo.objeto(nome + "_folhas", F.m_folha("agulha", "agulha_pinheiro.png"), cc, up_mix=0.55)
    F.exportar(OUT + nome + ".glb", tr, [folhas], tex=1024, tris_tronco=3500)

def coqueiro(nome, seed, H=8.2):
    K.reset(); rng = random.Random(seed)
    b = K.NB("casca_coco")
    rings = b.wave(26, 0.5, 2, 'BANDS', 'Z'); n1 = b.noise(9, 6)
    col = b.ramp(b.math('ADD', b.math('MULTIPLY', rings, 0.6), b.math('MULTIPLY', n1, 0.4)), [(0.25, "#3a2c20"), (0.6, "#7a6650"), (0.9, "#a89378")])
    casca = b.set(base=col, rough=0.9, normal=b.bump(rings, 0.8, 0.02))
    tr = []; fo = F.Folhas()
    bend = Vector((rv(rng, 1.0, 1.8), rv(rng, -.5, .5), 0)); 
    pts = [Vector((0, 0, 0)), Vector((0, 0, H * .3)) + bend * 0.15, Vector((0, 0, H * .65)) + bend * 0.55, Vector((0, 0, H * 0.97)) + bend]
    tr.append(F.tronco("tronco", [tuple(p) for p in pts], 0.26, 0.15, casca, res=12))
    top = pts[-1]
    # cocos
    coco = K.m_plain("coco", "#5e4a24", 0.5)
    for i in range(6):
        a = i * math.tau / 6 + rv(rng, -.3, .3)
        o = K.lathe(f"c{i}", [(0.0, -0.13), (0.11, -0.09), (0.14, 0.0), (0.11, 0.1), (0.0, 0.14)], 10, coco)
        o.location = top + Vector((math.cos(a) * 0.2, math.sin(a) * 0.2, -0.25)); tr.append(o)
    fv = F.Folhas(); seca = F.Folhas()
    n = 13
    for i in range(n):
        a = i * math.tau / n + rv(rng, -.2, .2); dry = i % 6 == 5
        L = rng.uniform(3.0, 3.8) * (0.8 if dry else 1)
        rise = rng.uniform(0.4, 0.9) if not dry else -0.6
        dirh = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-math.sin(a), math.cos(a), 0))
        pts = []; ws = []; ups = []
        for k in range(9):
            t = k / 8
            p = top + dirh * L * t + Vector((0, 0, rise * t * 1.4 - (1.8 if not dry else 1.0) * t * t * L / 3.2))
            pts.append(tuple(p)); ws.append(1.25 * math.sin(math.pi * min(1, 0.15 + t * 0.95)) + 0.15)
            ups.append(tuple((side + Vector((0, 0, 0.25 if k else 0))).normalized()))
        (seca if dry else fv).strip(pts, ws, ups)
    cc = top + Vector((0, 0, -0.6))
    folhas = fv.objeto(nome + "_folhas", F.m_folha("palma", "folha_palmeira.png"), cc, up_mix=0.6)
    sec = seca.objeto(nome + "_seca", F.m_folha("palma_seca", "folha_palmeira_seca.png"), cc, up_mix=0.6)
    F.exportar(OUT + nome + ".glb", tr, [folhas, sec], tex=1024, tris_tronco=3500)

def arvore_praca(nome="cenario_arvore_praca", seed=21, H=4.9):
    K.reset(); rng = random.Random(seed)
    casca = F.m_casca("casca_praca", "#6b5644", "#2c2018", "#9a8670", 1.6)
    pedra = K.m_rock("pedra_canteiro", "#9c958a", "#d6d0c4", 2.0, flecks="#b8b0a0")
    terra = K.m_plain("terra", "#3b2a1c", 0.95, bumpscale=30)
    tr = []
    tr.append(K.lathe("canteiro", [(0.95, 0), (1.0, 0.05), (1.0, 0.36), (0.94, 0.42), (0.78, 0.42), (0.78, 0.30)], 16, pedra, smooth=False, close_top=False))
    tr.append(K.lathe("terra", [(0.0, 0.32), (0.8, 0.32)], 16, terra, close_bot=False))
    fo = F.Folhas()
    top = Vector((rv(rng, -.2, .2), rv(rng, -.2, .2), H * 0.45))
    tr.append(F.tronco("tronco", [(0, 0, 0.3), (0.05, 0, 1.2), tuple(top)], 0.17, 0.11, casca, res=10))
    clumps = []
    for i in range(6):
        a = i * math.tau / 6 + rv(rng, -.3, .3)
        end = top + Vector((math.cos(a) * 1.2, math.sin(a) * 1.2, rv(rng, 0.9, 1.7)))
        tr.append(F.tronco(f"g{i}", [tuple(top), tuple(top + (end - top) * 0.5 + Vector((0, 0, .2))), tuple(end)], 0.08, 0.025, casca, res=6))
        clumps.append(end)
    cc = top + Vector((0, 0, 1.4))
    for i in range(200):
        u = rng.uniform(0, math.tau); v = math.acos(rng.uniform(-0.5, 1))
        p = cc + Vector((math.sin(v) * math.cos(u) * 1.75, math.sin(v) * math.sin(u) * 1.75, math.cos(v) * 1.3)) * rng.uniform(0.75, 1.0)
        out = (p - cc).normalized(); d = (out + Vector((rv(rng, -.5, .5), rv(rng, -.5, .5), rv(rng, 0, .6)))).normalized()
        s = rng.uniform(0.8, 1.1)
        fo.card(p - d * s * 0.45, d, s, roll=rng.uniform(0, math.tau), centro=cc)
    # flores no canteiro
    fl = F.Folhas()
    for i in range(14):
        a = rng.uniform(0, math.tau); r = rng.uniform(0.35, 0.72)
        p = Vector((math.cos(a) * r, math.sin(a) * r, 0.3))
        fl.card(p, Vector((rv(rng, -.2, .2), rv(rng, -.2, .2), 1)), 0.35, roll=rng.uniform(0, math.tau), centro=(0, 0, 0))
    folhas = fo.objeto(nome + "_folhas", F.m_folha("folha_larga", "folha_larga.png"), cc)
    flores = fl.objeto(nome + "_flores", F.m_folha("erva_flor", "erva_tonico.png"), (0, 0, 0), up_mix=0.8)
    F.exportar(OUT + nome + ".glb", tr, [folhas, flores], tex=1024, tris_tronco=3500)

if __name__ == "__main__":
    which = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "all"
    jobs = {
        "arbusto_a": lambda: arbusto("costa_arbusto_a", 1, 1.84, 1.2),
        "arbusto_b": lambda: arbusto("costa_arbusto_b", 2, 1.6, 1.35),
        "arbusto_c": lambda: arbusto("costa_arbusto_c", 3, 2.0, 1.1),
        "pinheiro_a": lambda: pinheiro("costa_pinheiro_a", 4, 6.2, 2.8),
        "pinheiro_b": lambda: pinheiro("costa_pinheiro_b", 5, 8.2, 3.4),
        "pinheiro_c": lambda: pinheiro("costa_pinheiro_c", 6, 7.0, 3.0),
        "coqueiro_a": lambda: coqueiro("costa_coqueiro_a", 7, 8.2),
        "coqueiro_b": lambda: coqueiro("costa_coqueiro_b", 8, 6.5),
        "praca": lambda: arvore_praca(),
    }
    for k, f in jobs.items():
        if which in ("all", k): f()

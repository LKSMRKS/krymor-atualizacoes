"""Cenario dos estudos visuais 01/02 (Costa do Cal e Galeria Funda). Uso: python3 build_estudo.py <id|all>
cenario_estandarte_tridente, cenario_tocha_parede, cenario_tocha_poste, cenario_cais (trecho 8 m), marco_torre_partida,
cenario_ponte_arcos (trecho 12 m), cenario_vagonete, cenario_trilhos (trecho 4 m), cenario_carroca_quebrada, cenario_blocos_cal (3 variacoes)."""
import sys, math, random, os
sys.path.insert(0, "/home/claude/cinzas/tools")
import cz, org, pbr, bpy, bmesh
from cz import Vector, Matrix
from mathutils import noise
import build_props as bp
from build_props import Z, WOOD, DWOOD, IRON, ROPE, BONE, CAL, PAINT, STONE, EMBER
bp.OUT = "/home/claude/cinzas/estudo/modelos/"
PANO, OURO = 80, 81
bp.PAL.update({PANO: ("#8a1c1a", "#4a0c0a"), OURO: ("#d8a840", "#7a5a1a")}); bp.ROUGH.update({PANO: 0.9, OURO: 0.3}); bp.METAL.update({OURO: 1.0})
bp.DET[PANO] = dict(grain=0.002, grain_scale=70); bp.DET[OURO] = dict(grain=0.001, grain_scale=60)

def estandarte():
    """bandeira vermelha com tridente dourado, rasgada embaixo, pendurada numa trave de ferro (2,4 m de altura)"""
    parts = []; W, Hh = 0.9, 1.9
    me = bpy.data.meshes.new("pano"); bm = bmesh.new(); nx, ny = 10, 18; grid = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            u, v = i / nx, j / ny
            x = (u - 0.5) * W; z = 2.3 - v * Hh
            if v > 0.82: z -= 0.12 * abs(math.sin(u * 7))       # barra rasgada em pontas
            y = 0.04 * math.sin(v * 5 + u * 2) + 0.03 * noise.noise(Vector((u * 3, v * 3, 0)))
            grid[i, j] = bm.verts.new((x, y, z))
    for i in range(nx):
        for j in range(ny): bm.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
    bm.to_mesh(me); pano = cz._link(bpy.data.objects.new("pano", me)); so = pano.modifiers.new("s", 'SOLIDIFY'); so.thickness = 0.015; org._apply_all(pano)
    parts.append(Z(pano, PANO))
    # tridente em relevo (frente = -Y)
    yb = -0.035
    parts.append(Z(cz.box("haste", (0.05, 0.03, 0.9), loc=(0, yb, 1.4)), OURO))
    parts.append(Z(cz.box("travessa", (0.42, 0.03, 0.05), loc=(0, yb, 1.78)), OURO))
    for x in (-0.19, 0, 0.19):
        parts.append(Z(cz.box(f"dente{x}", (0.045, 0.03, 0.26 if x else 0.34), loc=(x, yb, 1.9 if x else 1.94)), OURO))
        parts.append(Z(cz.seg(f"ponta{x}", (x, yb, 2.03 if x else 2.11), (x, yb, 2.13 if x else 2.21), 0.04, 0.0, 4), OURO))
    parts.append(Z(cz.box("borda", (W + 0.02, 0.03, 0.06), loc=(0, yb, 2.27)), OURO))
    parts.append(Z(cz.cyl("trave", 0.03, W + 0.3, 8, loc=(0, 0, 2.35), rot=(0, math.pi / 2, 0)), IRON))
    for s in (-1, 1): parts.append(Z(cz.ico(f"remate{s}", 0.05, 1, loc=(s * (W / 2 + 0.17), 0, 2.35)), IRON))
    bp.finish("cenario_estandarte_tridente", parts, size=1024, subdiv=1, extr=0.01)

def tochas():
    for kind in ("parede", "poste"):
        cz.reset(); cz.clear_mats(); parts = []
        if kind == "parede":   # suporte de ferro para fixar na parede (parede em +Y)
            parts += [Z(cz.box("placa", (0.16, 0.03, 0.26), loc=(0, 0.02, 1.8)), IRON), Z(cz.seg("braco", (0, 0.0, 1.75), (0, -0.28, 1.86), 0.025, 0.02, 6), IRON)]
            base = Vector((0, -0.3, 1.86))
        else:
            parts += [Z(cz.cyl("poste", 0.07, 2.0, 8, r2=0.05, loc=(0, 0, 1.0)), DWOOD), Z(cz.cyl("pe", 0.18, 0.12, 8, loc=(0, 0, 0.06)), STONE)]
            for k in range(3):
                a = k / 3 * math.tau; parts.append(Z(cz.seg(f"esteio{k}", (math.cos(a) * 0.35, math.sin(a) * 0.35, 0), (0, 0, 0.6), 0.03, 0.025, 6), DWOOD))
            base = Vector((0, 0, 2.05))
        parts.append(Z(cz.cyl("cesto", 0.09, 0.16, 8, r2=0.06, loc=base, cap=False), IRON))
        for k in range(6):
            a = k / 6 * math.tau; parts.append(Z(cz.seg(f"grade{k}", base + Vector((math.cos(a) * 0.06, math.sin(a) * 0.06, -0.08)), base + Vector((math.cos(a) * 0.1, math.sin(a) * 0.1, 0.12)), 0.008, 0.008, 4), IRON))
        parts.append(Z(cz.cyl("pano", 0.07, 0.1, 8, loc=base + Vector((0, 0, 0.04))), ROPE))
        brasa = cz.ico("brasa", 0.06, 1, loc=base + Vector((0, 0, 0.1)))
        bp.finish(f"cenario_tocha_{kind}", parts, size=512, subdiv=1, extr=0.01, emit=[(brasa, "#ff8a30", 9.0)])

def cais():
    """trecho de cais de 8 m (X) x 3 m (Y), tabuas sobre estacas, corda e argolas; colisao do tabuado"""
    cz.reset(); cz.clear_mats(); parts = []; rnd = random.Random(4); L, Wd, Hd = 8.0, 3.0, 1.2
    for i in range(int(L / 0.3)):
        x = -L / 2 + 0.15 + i * 0.3
        if rnd.random() < 0.06: continue     # tabua faltando
        parts.append(Z(cz.box(f"tabua{i}", (0.27, Wd * rnd.uniform(0.95, 1.02), 0.06), loc=(x, rnd.uniform(-0.04, 0.04), Hd + rnd.uniform(-0.01, 0.01)), rot=(rnd.uniform(-0.02, 0.02), 0, rnd.uniform(-0.01, 0.01))), WOOD if i % 4 else DWOOD))
    for sy in (-1, 1):
        parts.append(Z(cz.box(f"viga{sy}", (L, 0.2, 0.2), loc=(0, sy * (Wd / 2 - 0.1), Hd - 0.13)), DWOOD))
        for x in (-L / 2 + 0.2, -1.3, 1.3, L / 2 - 0.2):
            parts.append(Z(cz.cyl(f"estaca{sy}{x}", 0.13, Hd + 1.6, 9, loc=(x, sy * (Wd / 2 - 0.05), (Hd - 1.6) / 2 + 0.2)), DWOOD))
            parts.append(Z(cz.cyl(f"amarra{sy}{x}", 0.14, 0.08, 9, loc=(x, sy * (Wd / 2 - 0.05), Hd - 0.35), cap=False), ROPE))
    parts.append(Z(cz.cyl("argola", 0.08, 0.02, 12, loc=(1.3, -Wd / 2 - 0.05, Hd - 0.2), rot=(math.pi / 2, 0, 0), cap=False), IRON))
    parts.append(Z(cz.cyl("cabeco", 0.12, 0.35, 10, loc=(-2.6, -Wd / 2 + 0.25, Hd + 0.17)), DWOOD))
    for k in range(6):
        a = k / 6 * math.tau; parts.append(Z(cz.cyl(f"rolo{k}", 0.16 - k * 0.012, 0.04, 14, loc=(-2.0, 0.6, Hd + 0.05 + k * 0.035), cap=False), ROPE))
    bp.finish("cenario_cais", parts, size=1024, subdiv=1, extr=0.02, collision=[((L, Wd, 0.3), (0, 0, Hd), 0)])

def arc_wall(name, r, thick, z0, h, a0, a1, m, seg=40):
    """trecho de parede curva (anel parcial) de a0 a a1 (radianos)"""
    bm = bmesh.new(); n = max(3, int(seg * (a1 - a0) / math.tau) + 1); ring = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        ring.append([bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)) for rr, z in ((r, z0), (r, z0 + h), (r - thick, z0 + h), (r - thick, z0))])
    for q0, q1 in zip(ring, ring[1:]):
        for k in range(4): bm.faces.new((q0[k], q1[k], q1[(k + 1) % 4], q0[(k + 1) % 4]))
    if a1 - a0 < math.tau - 1e-3:
        bm.faces.new(list(reversed(ring[0]))); bm.faces.new(ring[-1])
    bm.normal_update()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = cz._link(bpy.data.objects.new(name, me)); o.data.materials.append(m); return o

def torre():
    """Torre Partida (~13 m): fiadas de pedra de cal, topo quebrado irregular, janelas e porta escuras, entulho. Material por nome
    (ed_pedra_clara / ed_madeira_escura) -> rebake_lote2.py faz a alvenaria procedural."""
    cz.reset(); cz.clear_mats(); rnd = random.Random(9); R, fi = 2.6, 0.55
    pedra = cz.mat("ed_pedra_clara", "#d0c8b8"); pedra2 = cz.mat("ed_pedra", "#a09888"); mad = cz.mat("ed_madeira_escura", "#2a1c12")
    objs = []
    n = 24
    for k in range(n):
        z = k * fi; r = R * (1 - 0.1 * z / (n * fi))
        if k < 15: a0, a1 = 0.0, math.tau
        else:     # quebra: cada fiada de cima perde um pedaco maior, de forma irregular
            gap = (k - 14) * 0.42 + rnd.uniform(-0.15, 0.25); c = 1.2 + rnd.uniform(-0.2, 0.2)
            a0, a1 = c + gap / 2, c - gap / 2 + math.tau
            if a1 - a0 < 0.8: break
        objs.append(arc_wall(f"fiada{k}", r + (0.04 if k % 3 == 0 else 0), 0.6, z, fi, a0, a1, pedra2 if k % 3 == 0 else pedra))
    for k, (z, a) in enumerate(((3.3, 0.3), (6.0, 2.4), (7.8, 4.4))):
        p = Vector((math.cos(a) * R * 0.97, math.sin(a) * R * 0.97, z))
        o = cz.box(f"janela{k}", (0.5, 0.25, 0.95), loc=p, rot=(0, 0, a + math.pi / 2), m=mad); objs.append(o)
    objs.append(cz.box("porta", (1.0, 0.3, 1.9), loc=(0, -R + 0.05, 0.95), m=mad))
    objs.append(cz.cyl("base", R + 0.25, 0.45, 24, loc=(0, 0, 0.22), m=pedra2))
    for k in range(20):
        a = rnd.uniform(0, math.tau); r = rnd.uniform(R + 0.4, R + 3.5)
        b = cz.box(f"bloco{k}", (rnd.uniform(0.4, 0.9), rnd.uniform(0.3, 0.6), rnd.uniform(0.25, 0.5)), loc=(math.cos(a) * r, math.sin(a) * r, 0.18), rot=(rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), a), m=pedra)
        objs.append(b)
    for o in objs: cz.apply_tf(o)
    low = cz.join("marco_torre_partida", objs, flat_shade=True)
    os.makedirs(bp.OUT + "marco_torre_partida", exist_ok=True)
    cz.export_objs([low], bp.OUT + "marco_torre_partida/_bruto")
    c = [cz.box(f"c{k}", (5.4, 5.4, 10.0), loc=(0, 0, 5.0), rot=(0, 0, k * math.pi / 4)) for k in range(2)]
    for o in c: cz.apply_tf(o)
    cz.export_objs([cz.join("col", c)], bp.OUT + "marco_torre_partida/marco_torre_partida_colisao")
    print("OK bruto torre")

def ponte_arcos():
    """trecho de 12 m de ponte/aqueduto de pedra com 2 arcos (estudo 02): repete para vencer o canion; altura 7 m"""
    cz.reset(); cz.clear_mats(); parts = []; L, Wd, Hp = 12.0, 3.4, 7.0
    me = bpy.data.meshes.new("arcos"); bm = bmesh.new()
    n = 48; prof = []
    for i in range(n + 1):
        x = -L / 2 + L * i / n
        xr = ((x + L / 2) % 6.0) - 3.0             # dois arcos de 6 m
        top_arch = 1.0 + math.sqrt(max(0.0, 2.2 ** 2 - xr ** 2)) * 2.0 if abs(xr) < 2.2 else 0.0
        prof.append((x, top_arch))
    def face_y(y):
        vs = []
        for x, za in prof: vs.append((bm.verts.new((x, y, za)), bm.verts.new((x, y, Hp))))
        for (a0, a1), (b0, b1) in zip(vs, vs[1:]): bm.faces.new((a0, b0, b1, a1))
        return vs
    f1 = face_y(-Wd / 2); f2 = face_y(Wd / 2)
    for (a0, a1), (b0, b1) in zip(f1, f2): pass
    for i in range(len(f1) - 1):
        bm.faces.new((f1[i][0], f2[i][0], f2[i + 1][0], f1[i + 1][0])); bm.faces.new((f1[i][1], f1[i + 1][1], f2[i + 1][1], f2[i][1]))
    bm.faces.new((f1[0][0], f1[0][1], f2[0][1], f2[0][0])); bm.faces.new((f1[-1][0], f2[-1][0], f2[-1][1], f1[-1][1]))
    bm.normal_update(); bm.to_mesh(me); bm.free()
    ob = cz._link(bpy.data.objects.new("arcos", me)); parts.append(Z(ob, CAL))
    for sy in (-1, 1):                               # parapeito com ameias baixas
        parts.append(Z(cz.box(f"parapeito{sy}", (L, 0.35, 0.7), loc=(0, sy * (Wd / 2 - 0.17), Hp + 0.35)), CAL))
        for k in range(8): parts.append(Z(cz.box(f"ameia{sy}{k}", (0.6, 0.38, 0.35), loc=(-L / 2 + 0.75 + k * 1.5, sy * (Wd / 2 - 0.17), Hp + 0.85)), CAL))
    parts.append(Z(cz.box("piso", (L, Wd - 0.7, 0.1), loc=(0, 0, Hp + 0.05)), STONE))
    pedra = cz.mat("ed_pedra_clara", "#d0c8b8")
    for o in parts:
        cz.apply_tf(o); o.data.materials.clear(); o.data.materials.append(pedra)
    low = cz.join("cenario_ponte_arcos", parts, flat_shade=True)
    os.makedirs(bp.OUT + "cenario_ponte_arcos", exist_ok=True); cz.export_objs([low], bp.OUT + "cenario_ponte_arcos/_bruto")
    c = [cz.box("c1", (L, 0.35, 1.0), loc=(0, -(Wd / 2 - 0.17), Hp + 0.5)), cz.box("c2", (L, 0.35, 1.0), loc=(0, Wd / 2 - 0.17, Hp + 0.5)), cz.box("c3", (1.6, Wd, Hp), loc=(0, 0, Hp / 2))]
    for o in c: cz.apply_tf(o)
    cz.export_objs([cz.join("col", c)], bp.OUT + "cenario_ponte_arcos/cenario_ponte_arcos_colisao"); print("OK bruto ponte")

def mina():
    """vagonete de mina com minerio de cal e trecho de trilhos (4 m)"""
    cz.reset(); cz.clear_mats(); parts = []
    caixa = cz.box("caixa", (1.0, 1.4, 0.6), loc=(0, 0, 0.62)); cz.apply_tf(caixa)
    for v in caixa.data.vertices:
        if v.co.z > 0.6: v.co.x *= 1.15; v.co.y *= 1.1
    parts.append(Z(caixa, DWOOD))
    for z in (0.4, 0.85):
        for sy in (-1, 1): parts.append(Z(cz.box(f"cinta{z}{sy}", (1.12, 0.04, 0.06), loc=(0, sy * 0.72, z)), IRON))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(Z(cz.cyl(f"roda{sx}{sy}", 0.17, 0.08, 12, loc=(sx * 0.55, sy * 0.45, 0.17), rot=(0, math.pi / 2, 0)), IRON))
    rnd = random.Random(2)
    for k in range(10):
        b = cz.ico(f"minerio{k}", rnd.uniform(0.12, 0.2), 1, loc=(rnd.uniform(-0.35, 0.35), rnd.uniform(-0.5, 0.5), 0.95 + rnd.uniform(0, 0.12))); cz.apply_tf(b); cz.jitter(b, 0.03, k); parts.append(Z(b, CAL))
    bp.finish("cenario_vagonete", parts, size=1024, subdiv=1, extr=0.02, collision=[((1.2, 1.6, 1.1), (0, 0, 0.55), 0)])
    cz.reset(); cz.clear_mats(); parts = []
    for sx in (-0.5, 0.5): parts.append(Z(cz.box(f"trilho{sx}", (0.07, 4.0, 0.09), loc=(sx, 0, 0.13)), IRON))
    for k in range(6): parts.append(Z(cz.box(f"dormente{k}", (1.4, 0.2, 0.09), loc=(0, -1.75 + k * 0.7, 0.05), rot=(0, 0, random.Random(k).uniform(-0.04, 0.04))), DWOOD))
    bp.finish("cenario_trilhos", parts, size=512, subdiv=1, extr=0.02)

def carroca():
    """carroca de duas rodas tombada, com uma roda solta e caixotes (estudo 01)"""
    cz.reset(); cz.clear_mats(); parts = []
    parts.append(Z(cz.box("caixa", (1.5, 2.4, 0.5), loc=(0.1, 0, 0.55), rot=(0, 0.28, 0)), WOOD))
    for sy in (-1, 1): parts.append(Z(cz.box(f"lateral{sy}", (1.5, 0.06, 0.4), loc=(0.15, sy * 1.17, 0.85), rot=(0, 0.28, 0)), DWOOD))
    parts.append(Z(cz.seg("lanca_l", (0.4, -1.1, 0.4), (0.5, -3.2, 0.05), 0.05, 0.04, 6), DWOOD)); parts.append(Z(cz.seg("lanca_r", (-0.3, -1.1, 0.2), (-0.4, -3.0, 0.02), 0.05, 0.04, 6), DWOOD))
    def roda(c, rot):
        parts.append(Z(cz.cyl("aro", 0.62, 0.08, 20, loc=c, rot=rot, cap=False), DWOOD)); parts.append(Z(cz.cyl("cubo", 0.1, 0.16, 10, loc=c, rot=rot), IRON))
        for k in range(8):
            a = k / 8 * math.tau
            d = Matrix.Rotation(rot[0], 3, 'X') @ Matrix.Rotation(rot[1], 3, 'Y') @ Vector((math.cos(a) * 0.58, 0, math.sin(a) * 0.58))
            parts.append(Z(cz.seg(f"raio{k}", c, Vector(c) + d, 0.022, 0.018, 4), WOOD))
    roda((0.95, 0.4, 0.55), (0, math.pi / 2 + 0.1, 0)); roda((-1.6, 1.2, 0.05), (math.pi / 2, 0, 0))
    for k, (x, y) in enumerate(((-1.4, -0.8), (-1.0, -1.5))): parts.append(Z(cz.box(f"caixote{k}", (0.5, 0.5, 0.5), loc=(x, y, 0.25), rot=(0, 0, 0.4 * k)), WOOD))
    bp.finish("cenario_carroca_quebrada", parts, size=1024, subdiv=1, extr=0.02, collision=[((2.6, 3.0, 1.2), (0, 0, 0.6), 0)])

def blocos():
    for vi in range(3):
        cz.reset(); cz.clear_mats(); rnd = random.Random(30 + vi); parts = []
        for k in range((3, 5, 2)[vi]):
            sz = (rnd.uniform(0.7, 1.4), rnd.uniform(0.6, 1.1), rnd.uniform(0.5, 0.9))
            z = sz[2] / 2 + (0.5 if (vi == 1 and k >= 3) else 0)
            b = cz.box(f"bloco{k}", sz, loc=(rnd.uniform(-0.8, 0.8), rnd.uniform(-0.7, 0.7), z), rot=(rnd.uniform(-0.12, 0.12), rnd.uniform(-0.12, 0.12), rnd.uniform(0, 3)))
            cz.apply_tf(b); cz.jitter(b, 0.05, k); bv = b.modifiers.new("bv", 'BEVEL'); bv.width = 0.05; bv.segments = 2; org._apply_all(b); parts.append(Z(b, CAL))
        bp.finish(f"cenario_blocos_cal_{'abc'[vi]}", parts, size=1024, subdiv=2, extr=0.03, collision=[((1.8, 1.6, 1.0), (0, 0, 0.5), 0)])

G = dict(estandarte=estandarte, tochas=tochas, cais=cais, torre=torre, ponte=ponte_arcos, mina=mina, carroca=carroca, blocos=blocos)
if __name__ == "__main__":
    w = sys.argv[-1]
    for k, f in G.items():
        if w in ("all", k): cz.reset(); cz.clear_mats(); f()

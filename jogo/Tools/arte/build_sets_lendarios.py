"""15 conjuntos lendários (referência do dono 14/11: "Sets de Armaduras" — mesmo físico, profundidade e detalhe).
python3 build_sets_lendarios.py <id> [<id>...]   (sem id = todos)
Usa o kit dos conjuntos (5 peças no manequim, pesos do esqueleto do jogo) e acrescenta o que deixa o set com cara de lendário:
asas grandes de verdade (penas em 3 fileiras / membrana de morcego / lascas de cristal), auréola com raios, relógio, buraco negro,
chamas, cristais, folhas, caveiras, nemes, engrenagens, rochas, barbatanas; placas com rachaduras acesas (não pontilhado)."""
import sys, math, random
sys.path.insert(0, "/home/claude/cinzas/conjuntos"); sys.path.insert(0, "/home/claude/cinzas/tools")
_args = [a for a in sys.argv[1:] if not a.startswith("-")]
sys.argv = [sys.argv[0], "/tmp/claude-0/conj_tmp"]
import bpy, bmesh
from mathutils import Vector, Matrix
import kit_conjunto as KC
from kit_conjunto import m_pano, m_metal_gravado, m_couro, m_brilho, m_cristal, m_quitina, m_pelo, m_pena
import itemkit as K
import build_conjuntos as BC
from build_conjuntos import (bases, peitoral_placa, lamelas, emblema, ombreiras, gola, cinto, bracadeiras, manopla, grevas, bota,
                             coxotes, tabardo, capa, capuz, elmo, coroa, chifres_cabeca, mascara, penas_ombro)
V = Vector
_pano_kc = m_pano
def m_pano(name, base, dark, **kw):
    """tecido liso (sem o bordado pontilhado, que de perto parece tracejado); o ouro vem dos frisos e das peças"""
    return _pano_kc(name, base, dark)
OUT = "/home/claude/cinzas/conjuntos/out_lend"
_omb = BC.ombreiras
def ombreiras(c, mat, friso_m, camadas=3, tam=1.0, espinhos=0, m_esp=None, curva_esp=0.0, inclina=28, extra=None):
    """ombreiras de lendário: 15% maiores que as dos conjuntos comuns"""
    return _omb(c, mat, friso_m, camadas=camadas, tam=tam * 1.15, espinhos=espinhos, m_esp=m_esp, curva_esp=curva_esp * 0.7, inclina=inclina, extra=extra)
_friso = KC.Ctx.friso
def _friso_grosso(s, base, nome, raio, mat, peca, so_externo=True):
    return _friso(s, base, nome, raio * 1.7, mat, peca, so_externo)      # frisos de ouro grossos, como na referência
KC.Ctx.friso = _friso_grosso

# ====================================================================== materiais novos
def m_ouro(n): return m_metal_gravado(n, "#d9a640", "#fff3c8", "#3a2208", rough=0.22, esc=12)
def m_prata(n): return m_metal_gravado(n, "#c9cdd6", "#ffffff", "#20232a", rough=0.2, esc=10)

def m_rachado(name, base, dark, glow, forca=4.0, esc=7.0, metal=0.85, rough=0.35, linha=0.02):
    """placa com rachaduras acesas (lava, gelo, vazio): linhas finas contínuas — no lugar do pontilhado antigo"""
    b = K.NB(name); esc *= 2.2; forca *= 0.6
    n1 = b.noise(10, 6, 0.6); ed = b.voronoi(esc, 'DISTANCE_TO_EDGE'); ed2 = b.voronoi(esc * 2.7, 'DISTANCE_TO_EDGE')
    col = b.ramp(n1, [(0.2, dark), (0.75, base)])
    col = b.mix(b.math('MULTIPLY', K.edge_mask(b, 0.5, 0.55), 0.6, clamp=True), col, K.b_l(base, 1.6))
    l1 = b.math('LESS_THAN', ed, linha); l2 = b.math('MULTIPLY', b.math('LESS_THAN', ed2, linha * 0.6), b.math('GREATER_THAN', b.noise(3, 2), 0.55))
    vl = b.math('MAXIMUM', l1, l2)
    col = b.mix(vl, col, glow)
    h = b.math('SUBTRACT', b.math('MULTIPLY', n1, 0.4), b.math('MULTIPLY', vl, 0.9))
    return b.set(base=col, metal=metal, rough=b.math('ADD', rough, b.math('MULTIPLY', n1, 0.15)), normal=b.bump(h, 0.7, 0.006),
                 emit_col=b.mix(vl, "#000000", glow), emit=forca)

def m_listrado(name, a, b_, esc=40.0, metal=0.9):
    """faixas (nemes, saiote faraônico): ouro e lápis-lazúli alternados"""
    b = K.NB(name)
    w = b.wave(esc, 0.3, 1, 'BANDS', 'Z'); f = b.math('GREATER_THAN', w, 0.5); n1 = b.noise(14, 4)
    col = b.mix(f, a, b_)
    col = b.mix(b.math('MULTIPLY', n1, 0.25), col, "#0a0604")
    return b.set(base=col, metal=b.math('MULTIPLY', f, metal), rough=b.math('ADD', 0.25, b.math('MULTIPLY', f, -0.05)), normal=b.bump(w, 0.25, 0.004))

def m_casca_arvore(name, base="#4a3420", dark="#1c1208", musgo="#5f8a2a"):
    b = K.NB(name)
    st = b.wave(30, 12, 6, 'BANDS', 'X'); n1 = b.noise(6, 6, 0.6); m = b.math('GREATER_THAN', b.noise(3, 4), 0.6)
    col = b.ramp(st, [(0.2, dark), (0.8, base)]); col = b.mix(b.math('MULTIPLY', m, 0.8), col, musgo)
    return b.set(base=col, rough=0.85, normal=b.bump(b.math('ADD', st, b.math('MULTIPLY', n1, 0.4)), 0.9, 0.012))

def m_pedra(name, base="#5a5650", light="#9a948a", glow="#3ad6ff", forca=4.0):
    b = K.NB(name)
    n1 = b.noise(5, 8, 0.65); ed = b.voronoi(6, 'DISTANCE_TO_EDGE'); vl = b.math('LESS_THAN', ed, 0.03)
    col = b.ramp(n1, [(0.25, K.b_l(base, 0.6)), (0.6, base), (0.9, light)])
    col = b.mix(vl, col, glow)
    return b.set(base=col, rough=0.9, normal=b.bump(b.math('SUBTRACT', n1, vl), 1.0, 0.015), emit_col=b.mix(vl, "#000000", glow), emit=forca)

def m_escama(name, base, dark, brilho=None, forca=2.0, esc=55.0):
    b = K.NB(name)
    f = b.voronoi(esc, 'F1'); n1 = b.noise(8, 5)
    col = b.ramp(f, [(0.0, K.b_l(base, 1.35)), (0.7, base), (1.0, dark)])
    kw = {}
    if brilho: kw.update(emit_col=b.mix(b.math('MULTIPLY', b.math('GREATER_THAN', f, 0.75), 1.0), "#000000", brilho), emit=forca)
    return b.set(base=col, metal=0.6, rough=b.math('ADD', 0.3, b.math('MULTIPLY', n1, 0.2)), coat=0.5, normal=b.bump(f, 0.6, 0.006), **kw)

def m_placa(name, base, wear, glow=None, forca=1.2):
    """placa limpa (metal gravado sem pontilhado); glow = leve brilho da cor do elemento no metal todo"""
    m = m_metal_gravado(name, base, wear, "#050304", rough=0.26, esc=9)
    if glow:
        bs = m.node_tree.nodes["Principled BSDF"]
        for l in list(bs.inputs["Emission Color"].links): m.node_tree.links.remove(l)
        bs.inputs["Emission Color"].default_value = K.lin(glow); bs.inputs["Emission Strength"].default_value = forca * 0.15
    return m

def m_friso_aceso(name, base, glow, forca=1.5):
    """friso de metal que brilha de leve (ouro do Abismo, prata do Vazio)"""
    m = m_metal_gravado(name, base, "#ffffff", "#100808", rough=0.2, esc=12)
    bs = m.node_tree.nodes["Principled BSDF"]
    for l in list(bs.inputs["Emission Color"].links): m.node_tree.links.remove(l)
    bs.inputs["Emission Color"].default_value = K.lin(glow); bs.inputs["Emission Strength"].default_value = forca
    return m

def m_vazio(name):
    """buraco negro: preto absoluto, sem reflexo"""
    return K.m_plain(name, "#010003", rough=1.0)

# ====================================================================== formas novas
def pena(c, nome, base, eixo, lado, comp, larg, mat, peca, pesos, curva=0.0, forma="pena", seg=7, grossura=0.006, so=None, balanco=None, rnd=None):
    """lâmina afinando (pena, chama, folha, lasca, barbatana): base, eixo (comprimento), lado (largura), curva (dobra para a normal)"""
    eixo = V(eixo).normalized(); lado = V(lado).normalized(); nrm = eixo.cross(lado).normalized()
    bm = bmesh.new(); rows = []
    for i in range(seg + 1):
        t = i / seg
        if forma == "pena":   w = larg * (math.sin(math.pi * min(1.0, t * 0.92 + 0.08)) ** 0.55) * (1 - 0.35 * t)
        elif forma == "folha": w = larg * math.sin(math.pi * t) ** 0.8
        elif forma == "chama": w = larg * (1 - t) ** 0.9 * (1 + 0.25 * math.sin(t * 9))
        elif forma == "lasca": w = larg * (1 - t) ** 1.2
        else:                  w = larg * (1 - 0.6 * t)                                   # barbatana
        dx = eixo * (comp * t) + nrm * (curva * comp * t * t)
        if forma == "chama": dx += lado * (larg * 0.6 * math.sin(t * 6.0)) * t
        row = []
        for k in (-1.0, 0.0, 1.0):
            off = lado * (w * k)
            z = nrm * (0.07 * w * (1 - abs(k))) if forma in ("pena", "folha") else V((0, 0, 0))
            row.append(bm.verts.new(V(base) + dx + off + z))
        rows.append(row)
    for i in range(seg):
        for j in range(2): bm.faces.new([rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]])
    me = bpy.data.meshes.new(nome); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(nome, me); bpy.context.collection.objects.link(o)
    o.data.materials.append(mat); K.mod(o, 'SOLIDIFY', thickness=grossura)
    for p in o.data.polygons: p.use_smooth = True
    if balanco:
        K.apply_all(o); b0 = V(base)
        c.balanco(o, lambda p: balanco[0] + balanco[1] * min(1.0, (p - b0).length / max(0.05, comp)))
    return c.add(o, peca, pesos, so=so)

def asas(c, tipo, m1, m_osso, tam=1.0, m2=None, abre=1.0, brilho_borda=None):
    """asas de verdade nas costas (peitoral, rígidas na Spine2, balanço de asa para o shader):
    'penas' 3 fileiras (primárias, secundárias, coberteiras); 'morcego' membrana entre dedos; 'lascas' (cristal/osso)"""
    P = c.P; R0 = P["Spine2"] + V((0, 0.13, 0.03))
    for s in (-1, 1):
        R = R0 + V((s * 0.06, 0, 0))
        J1 = R + V((s * 0.20, 0.12, 0.20)) * tam
        J2 = R + V((s * 0.52 * abre, 0.20, 0.44)) * tam
        J3 = R + V((s * 0.92 * abre, 0.26, 0.30)) * tam
        c.tubo(f"asa_osso{s}", [R, J1, J2, J3], 0.02 * tam, m_osso, "peitoral", taper=[1.0, 0.9, 0.7, 0.25], pesos="Spine2")
        c.esfera(f"asa_junta{s}", J2, (0.028 * tam,) * 3, m_osso, "peitoral", pesos="Spine2")
        bal = (0.53, 0.47)
        if tipo in ("penas", "lascas"):
            forma = "pena" if tipo == "penas" else "lasca"
            def ao_longo(a, b, n, comp0, comp1, larg, dirz, mat, nome, frente=0.0):
                for k in range(n):
                    t = k / max(1, n - 1); p = a.lerp(b, t)
                    out = V((s * (0.25 + 0.75 * t), 0.05, 0)).normalized()
                    eixo = (out * (0.15 + 0.85 * t) + V((0, 0.08 + frente, -dirz * (1 - 0.6 * t)))).normalized()
                    lado = V((0, 1, 0)).cross(eixo).normalized()
                    comp = (comp0 + (comp1 - comp0) * t) * tam
                    pena(c, f"{nome}{s}{k}", p + V((0, 0.01 * k % 0.02, 0)), eixo, lado, comp, larg * tam, mat if (k % 2 == 0 or m2 is None) else m2,
                         "peitoral", "Spine2", curva=0.12 * s * 0, forma=forma, grossura=0.007, balanco=bal)
            ao_longo(J2, J3, 11, 0.44, 0.76, 0.068, 0.55, m1, "prim")                    # primárias: na ponta, longas
            ao_longo(J1, J2, 10, 0.4, 0.48, 0.07, 1.0, m1, "sec", frente=0.01)            # secundárias: caem para baixo
            ao_longo(R.lerp(J1, 0.4), J3.lerp(J2, 0.35), 11, 0.17, 0.26, 0.058, 0.8, m2 or m1, "cob", frente=-0.02)   # coberteiras
        else:   # morcego: dedos a partir do pulso + membrana entre eles
            dedos = []
            for k in range(4):
                a = -0.15 - 0.42 * k
                tip = J2 + V((s * math.cos(a) * 0.55 * abre, 0.08, math.sin(a) * 0.62)) * tam
                mid = J2.lerp(tip, 0.5) + V((0, 0.03, 0.04 * tam))
                c.tubo(f"dedo{s}{k}", [J2, mid, tip], 0.012 * tam, m_osso, "peitoral", taper=[1.0, 0.6, 0.15], pesos="Spine2")
                c.chifre(f"garra{s}{k}", tip, [tip + V((s * 0.03, 0, -0.04)) * tam], 0.008 * tam, m_osso, "peitoral", pesos="Spine2")
                dedos.append((J2, mid, tip))
            raiz = R + V((s * 0.05, 0.03, -0.32)) * tam
            linhas = [[J1, J1.lerp(J2, 0.5), J2]] + [list(d) for d in dedos] + [[J2, J2.lerp(raiz, 0.5), raiz]]
            linhas = [[J3, J3.lerp(J2, 0.5), J2][::-1]] + linhas[1:]
            bm = bmesh.new(); N = 8
            def amostra(l, t):
                if t <= 0.5: return l[0].lerp(l[1], t * 2)
                return l[1].lerp(l[2], (t - 0.5) * 2)
            curvas = [[amostra(l, i / N) for i in range(N + 1)] for l in linhas]
            vs = [[bm.verts.new(p) for p in cv] for cv in curvas]
            for a in range(len(vs) - 1):
                for i in range(N):
                    if i == N - 1:  # borda recortada (vieira): o meio da ponta afunda
                        m = bm.verts.new((curvas[a][N].lerp(curvas[a + 1][N], 0.5)).lerp(J2, 0.22))
                        bm.faces.new([vs[a][i], vs[a + 1][i], vs[a + 1][i + 1], m]); bm.faces.new([vs[a][i], m, vs[a][i + 1]])
                    else: bm.faces.new([vs[a][i], vs[a + 1][i], vs[a + 1][i + 1], vs[a][i + 1]])
            me = bpy.data.meshes.new(f"membrana{s}"); bm.to_mesh(me); bm.free()
            o = bpy.data.objects.new(f"membrana{s}", me); bpy.context.collection.objects.link(o); o.data.materials.append(m1)
            K.mod(o, 'SUBSURF', levels=1); K.mod(o, 'SOLIDIFY', thickness=0.006)
            for p in o.data.polygons: p.use_smooth = True
            K.apply_all(o); c.balanco(o, lambda p, R=R: 0.53 + 0.47 * min(1.0, (p - R).length / (0.9 * tam)))
            c.add(o, "peitoral", "Spine2")

def aureola(c, m_anel, r=0.17, raios=0, m_raio=None, tipo="sol", m_centro=None, z=0.07, atras=0.17):
    """auréola atrás da cabeça (capacete, rígida no Head): 'sol' com raios, 'relogio' com marcas e ponteiros, 'quebrada'"""
    h = c.cabeca + V((0, atras, z))
    rot = (math.radians(90), 0, 0)
    if tipo == "quebrada":
        for k in range(3):
            a0 = k * 2.2 + 0.3; pts = [h + V((math.cos(a0 + t * 1.5) * r, 0, math.sin(a0 + t * 1.5) * r)) for t in (0, 0.33, 0.66, 1.0)]
            c.tubo(f"aur_frag{k}", pts, 0.009, m_anel, "capacete", pesos="Head")
    else:
        bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=0.011, location=h, rotation=rot, major_segments=64, minor_segments=10)
        t = bpy.context.active_object; t.data.materials.append(m_anel); c.add(t, "capacete", "Head")
        bpy.ops.mesh.primitive_torus_add(major_radius=r * 0.82, minor_radius=0.005, location=h + V((0, 0.005, 0)), rotation=rot, major_segments=48, minor_segments=6)
        t = bpy.context.active_object; t.data.materials.append(m_raio or m_anel); c.add(t, "capacete", "Head")
    for k in range(raios):
        a = k / raios * math.tau
        d = V((math.cos(a), 0, math.sin(a)))
        if tipo == "relogio":
            ln = 0.035 if k % 3 == 0 else 0.018
            c.tubo(f"marca{k}", [h + d * (r * 0.82 - 0.004), h + d * (r * 0.82 - ln)], 0.0045, m_raio or m_anel, "capacete", pesos="Head")
        else:
            ln = (0.11 if k % 2 == 0 else 0.065) * (r / 0.17)
            c.chifre(f"raio{k}", h + d * (r + 0.006), [h + d * (r + ln * 0.5), h + d * (r + ln)], 0.009, m_raio or m_anel, "capacete", pesos="Head")
    if tipo == "relogio":
        c.tubo("ponteiro_h", [h, h + V((0.05, -0.006, 0.06))], 0.006, m_raio or m_anel, "capacete", pesos="Head")
        c.tubo("ponteiro_m", [h, h + V((-0.09, -0.006, 0.07))], 0.004, m_raio or m_anel, "capacete", pesos="Head")
    if m_centro is not None: c.gema("aur_gema", h + V((0, -0.006, 0)), 0.022, m_centro, "capacete", normal=(0, -1, 0), pesos="Head")

def buraco_negro(c, m_anel, m_vaz, r=0.075, atras=0.2, z=0.1):
    h = c.cabeca + V((0, atras, z))
    c.esfera("buraco", h, (r, r, r), m_vaz, "capacete", pesos="Head")
    for k, (rx, inc) in enumerate(((1.75, 78), (1.4, 62), (2.1, 95))):
        bpy.ops.mesh.primitive_torus_add(major_radius=r * rx, minor_radius=r * (0.12 - 0.03 * k), location=h, rotation=(math.radians(inc), math.radians(12 * k), 0),
                                         major_segments=56, minor_segments=8)
        t = bpy.context.active_object; t.scale = (1, 1, 0.35); t.data.materials.append(m_anel); c.add(t, "capacete", "Head")

def chamas(c, mat, base, n=5, tam=1.0, peca="peitoral", pesos="prox", so=None, sobe=V((0, 0, 1)), abre=0.6, seed=1):
    rnd = random.Random(seed)
    for k in range(n):
        a = (k / max(1, n - 1) - 0.5) * 2
        eixo = (V(sobe) + V((a * abre, rnd.uniform(-0.2, 0.3), 0))).normalized()
        lado = eixo.cross(V((0, 1, 0))).normalized() if abs(eixo.y) < 0.9 else V((1, 0, 0))
        comp = tam * rnd.uniform(0.12, 0.2) * (1.25 - 0.5 * abs(a))
        pena(c, f"chama{peca[:3]}{seed}{k}", V(base) + V((a * 0.025 * tam, 0, 0)), eixo, lado, comp, 0.028 * tam, mat, peca, pesos, so=so,
             curva=rnd.uniform(-0.3, 0.3), forma="chama", seg=8, grossura=0.004)

def cristais(c, mat, base, n=5, tam=1.0, peca="peitoral", pesos="prox", so=None, sobe=V((0, 0, 1)), abre=0.7, seed=2):
    rnd = random.Random(seed)
    for k in range(n):
        h = tam * rnd.uniform(0.07, 0.17); r = h * rnd.uniform(0.16, 0.24)
        o = K.crystal(f"cris{peca[:3]}{seed}{k}", mat, r, h, tip=0.45, sides=6, seed=seed * 31 + k)
        d = (V(sobe) + V((rnd.uniform(-abre, abre), rnd.uniform(-abre, abre) * 0.6, 0))).normalized()
        o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = d.to_track_quat('Z', 'Y')
        o.location = V(base) + V((rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), 0)) * tam
        c.add(o, peca, pesos, so=so)

def folhas(c, mat, base, n=7, tam=1.0, peca="peitoral", pesos="prox", so=None, sobe=V((0, 0, 1)), abre=1.0, seed=3, mat2=None):
    rnd = random.Random(seed)
    for k in range(n):
        ang = (k / n) * math.tau
        eixo = (V(sobe) * 0.6 + V((math.cos(ang) * abre, math.sin(ang) * abre * 0.6, 0))).normalized()
        lado = eixo.cross(V((0, 0, 1))).normalized() if abs(eixo.z) < 0.95 else V((1, 0, 0))
        pena(c, f"folha{peca[:3]}{seed}{k}", base, eixo, lado, tam * rnd.uniform(0.09, 0.15), tam * rnd.uniform(0.03, 0.045), mat if (mat2 is None or k % 3) else mat2,
             peca, pesos, so=so, curva=-0.35, forma="folha", seg=6, grossura=0.004)

def caveira(c, nome, pos, esc, m_osso, m_olho, peca, pesos="prox", so=None, olha=V((0, -1, 0))):
    """caveirinha ornamental (ombreira, cinto, elmo)"""
    olha = V(olha).normalized(); q = olha.to_track_quat('-Y', 'Z'); M = q.to_matrix()
    def L(v): return V(pos) + M @ (V(v) * esc)
    c.esfera(nome + "_cranio", L((0, 0.1, 0.25)), (0.9 * esc, 1.0 * esc, 0.85 * esc), m_osso, peca, pesos=pesos)
    c.esfera(nome + "_face", L((0, -0.35, -0.15)), (0.62 * esc, 0.5 * esc, 0.6 * esc), m_osso, peca, pesos=pesos)
    c.esfera(nome + "_queixo", L((0, -0.3, -0.72)), (0.45 * esc, 0.38 * esc, 0.25 * esc), m_osso, peca, pesos=pesos)
    for s in (-1, 1): c.esfera(f"{nome}_olho{s}", L((s * 0.3, -0.72, -0.05)), (0.2 * esc, 0.1 * esc, 0.17 * esc), m_olho, peca, pesos=pesos)

def engrenagem(c, nome, centro, r, mat, normal=V((0, -1, 0)), dentes=12, peca="peitoral", pesos="prox", so=None, gema=None):
    bm = bmesh.new(); pts = []
    for k in range(dentes * 4):
        a = k / (dentes * 4) * math.tau; rr = r * (1.18 if (k % 4) in (1, 2) else 1.0)
        pts.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, 0)))
    inn = [bm.verts.new((math.cos(k / 24 * math.tau) * r * 0.45, math.sin(k / 24 * math.tau) * r * 0.45, 0)) for k in range(24)]
    f = bm.faces.new(pts); bmesh.ops.triangulate(bm, faces=[f])
    me = bpy.data.meshes.new(nome); bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(nome, me); bpy.context.collection.objects.link(o)
    o.data.materials.append(mat); K.mod(o, 'SOLIDIFY', thickness=r * 0.18)
    K.apply_all(o)
    o.rotation_mode = 'QUATERNION'; o.rotation_quaternion = V(normal).to_track_quat('Z', 'Y'); o.location = centro
    c.add(o, peca, pesos, so=so)
    bpy.ops.mesh.primitive_torus_add(major_radius=r * 0.6, minor_radius=r * 0.1, location=centro, major_segments=32, minor_segments=8)
    t = bpy.context.active_object; t.rotation_mode = 'QUATERNION'; t.rotation_quaternion = V(normal).to_track_quat('Z', 'Y'); t.data.materials.append(mat)
    c.add(t, peca, pesos, so=so)
    if gema is not None: c.gema(nome + "_g", V(centro) + V(normal) * r * 0.12, r * 0.38, gema, peca, normal=tuple(normal), pesos=pesos)

def rochas(c, mat, base, n=4, tam=1.0, peca="peitoral", pesos="prox", so=None, seed=4, espalha=0.05):
    rnd = random.Random(seed)
    for k in range(n):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1, location=(0, 0, 0)); o = bpy.context.active_object; o.name = f"rocha{peca[:3]}{seed}{k}"
        K.displace(o, 0.35, 0.6, seed=seed * 7 + k); K.apply_all(o)
        sc = tam * rnd.uniform(0.04, 0.075); o.scale = (sc * rnd.uniform(0.9, 1.3), sc, sc * rnd.uniform(0.7, 1.1))
        o.rotation_euler = (rnd.random() * 6, rnd.random() * 6, rnd.random() * 6)
        o.location = V(base) + V((rnd.uniform(-1, 1), rnd.uniform(-0.7, 0.7), rnd.uniform(-0.2, 0.8))) * espalha * 0.12 * tam   # grudadas na peça
        o.data.materials.append(mat); bm = bmesh.new(); bm.from_mesh(o.data); bm.to_mesh(o.data); bm.free()
        c.add(o, peca, pesos, so=so)

def nemes(c, m_listra, m_ouro, m_gema):
    """toucado faraônico: capuz listrado + abas que caem na frente dos ombros + naja (uraeus) na testa"""
    capuz(c, m_listra, folga=1.12, caimento=1.6, friso_m=m_ouro)
    h = c.cabeca; P = c.P
    for s in (-1, 1):
        b0 = h + V((s * 0.11, -0.02, -0.05))
        c.lamina(f"aba{s}", [b0 + V((0, -0.03, 0)), b0 + V((s * 0.03, 0.02, 0))], 0.05, m_listra, "capacete", grossura=0.008,
                 pesos=lambda p: {"Head": 1.0} if p.z > P["Neck"].z + 0.03 else {"Neck": 0.5, "Spine2": 0.5},
                 cair=[(s * 0.035, -0.01, -0.09), (s * 0.05, -0.03, -0.2), (s * 0.05, -0.04, -0.3)])
    u = h + V((0, -0.125, 0.06))
    c.tubo("naja", [u + V((0, 0.02, -0.03)), u + V((0, -0.005, 0.0)), u + V((0, -0.01, 0.04)), u + V((0, -0.025, 0.06))], 0.008, m_ouro, "capacete", taper=[1, 1, 0.9, 0.8], pesos="Head")
    c.esfera("naja_cab", u + V((0, -0.03, 0.068)), (0.018, 0.008, 0.02), m_ouro, "capacete", pesos="Head")
    c.gema("naja_g", u + V((0, -0.038, 0.066)), 0.007, m_gema, "capacete", pesos="Head")

def colar_largo(c, m_listra, m_ouro, m_gema, r=0.2, n_gemas=7):
    """usekh: colar largo em disco nos ombros"""
    P = c.P; zc = P["Neck"].z - 0.02; yc = P["Neck"].y
    c.saia("usekh", m_listra, zc + 0.02, zc - 0.11, 0.06, r * 0.75, peca="peitoral", y_c=yc)
    parts = [o for o in c.parts if o.name == "usekh"]
    for o in parts:   # o 'saia' pesa como quadril; aqui o colar é do peito
        c.pesos_fn(o, lambda p: {"Spine2": 0.7, "Neck": 0.3}); c.balanco(o, lambda p: 0.0)
    for k in range(n_gemas):
        a = math.pi * (-0.75 + 1.5 * k / (n_gemas - 1)) - math.pi / 2
        p = V((math.cos(a) * r * 0.82, yc + math.sin(a) * r * 0.74, zc - 0.07))
        c.gema(f"usekh_g{k}", p, 0.013, m_gema, "peitoral", normal=(math.cos(a), math.sin(a), 0.6), pesos="Spine2", mold=m_ouro)

def tentaculos(c, mat, n=4, tam=1.0, seed=5):
    """filetes de sombra saindo das costas (Abismo/Vazio)"""
    P = c.P; R = P["Spine1"] + V((0, 0.13, 0.0)); rnd = random.Random(seed)
    for k in range(n):
        s = -1 if k % 2 else 1; a = 0.25 + 0.2 * (k // 2)
        pts = [R + V((s * 0.04, 0, 0.02 * k))]
        for i in range(1, 5):
            t = i / 4
            pts.append(R + V((s * (0.06 + 0.12 * t + a * 0.6 * t * t) * tam, (0.06 + 0.16 * t - 0.05 * t * t) * tam, (0.12 * t + (0.28 + 0.1 * (k // 2)) * t * t) * tam)))
        c.tubo(f"tent{k}", pts, 0.032 * tam, mat, "peitoral", taper=[1, 0.85, 0.6, 0.35, 0.04], pesos="Spine2")

def galhada(c, mat, tam=1.0):
    """chifres de galhos (Floresta)"""
    h = c.cabeca
    for s in (-1, 1):
        b0 = h + V((s * 0.07, 0.01, 0.08)); m1 = b0 + V((s * 0.07, 0.03, 0.12)) * tam; t1 = b0 + V((s * 0.12, 0.06, 0.26)) * tam
        c.tubo(f"galho{s}", [b0, m1, t1], 0.014 * tam, mat, "capacete", taper=[1, 0.7, 0.2], pesos="Head")
        for k, (f, d) in enumerate(((0.4, V((s * 0.09, -0.02, 0.06))), (0.7, V((s * -0.02, 0.0, 0.1))), (0.55, V((s * 0.1, 0.05, 0.02))))):
            p = b0.lerp(t1, f)
            c.tubo(f"galho{s}{k}", [p, p + d * 0.5 * tam, p + d * tam], 0.008 * tam, mat, "capacete", taper=[1, 0.6, 0.15], pesos="Head")

def ampulheta(c, m_ouro, m_vidro, m_areia, pos):
    for s in (-1, 1):
        bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=0.032, radius2=0.004, depth=0.05, location=pos + V((0, 0, s * 0.026)), rotation=(0 if s < 0 else math.pi, 0, 0))
        o = bpy.context.active_object; o.data.materials.append(m_vidro); c.add(o, "peitoral", "Spine2")
        bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.038, depth=0.008, location=pos + V((0, 0, s * 0.054)))
        o = bpy.context.active_object; o.data.materials.append(m_ouro); c.add(o, "peitoral", "Spine2")
    c.esfera("areia", pos + V((0, 0, -0.03)), (0.02, 0.02, 0.012), m_areia, "peitoral", pesos="Spine2")
    for k in range(3):
        a = k / 3 * math.tau
        c.tubo(f"amp_haste{k}", [pos + V((math.cos(a) * 0.036, math.sin(a) * 0.036, -0.054)), pos + V((math.cos(a) * 0.036, math.sin(a) * 0.036, 0.054))], 0.0035, m_ouro, "peitoral", pesos="Spine2")

def ombro(c, s):
    lado = "Left" if s > 0 else "Right"; return c.P[lado + "Arm"]

def enfeite_ombros(c, fn):
    """chama fn(s, o0) para cada ombreira (o0 = junta do ombro)"""
    for s in (-1, 1): fn(s, ombro(c, s))

def gemas_ombro(c, gema_m, mold_m, r=0.022):
    def f(s, o0): c.gema(f"gomb{s}", o0 + V((s * 0.075, -0.06, 0.05)), r, gema_m, "peitoral", normal=(s * 0.4, -1, 0.5), mold=mold_m, pesos="prox")
    enfeite_ombros(c, f)

def aletas_ombro(c, mat, friso, tam=1.0, n=3):
    """lâminas em leque saindo para cima das ombreiras (Cavaleiro Sagrado / Celestial)"""
    def f(s, o0):
        for k in range(n):
            eixo = V((s * (0.35 + 0.28 * k), 0.08, 1.0)).normalized()
            pena(c, f"aleta{s}{k}", o0 + V((s * (0.05 + 0.025 * k), 0.03 - 0.025 * k, 0.08)), eixo, V((0, 1, 0)), (0.17 + 0.05 * (n - k)) * tam, 0.05 * tam, mat if k % 2 == 0 else friso,
                 "peitoral", "prox", so={"peitoral"}, forma="pena", grossura=0.012)
    enfeite_ombros(c, f)

def joias_cinto(c, m_gema, m_ouro):
    P = c.P
    for s in (-1, 1):
        c.gema(f"cinto_g{s}", V((s * 0.09, P["Hips"].y - 0.125, P["Hips"].z + 0.07)), 0.013, m_gema, "calca", normal=(s * 0.4, -1, 0), mold=m_ouro)

def saia_placas(c, mat, friso_m, n=7, comp=0.26, larg=0.06):
    """faldas de placas ao redor do quadril (abaixo do cinto)"""
    P = c.P; z0 = P["Hips"].z + 0.03
    for k in range(n):
        a = math.pi * (-0.95 + 1.9 * k / (n - 1)) - math.pi / 2
        r = 0.165; b0 = V((math.cos(a) * r, P["Hips"].y + math.sin(a) * r * 0.9, z0))
        d = V((math.cos(a), math.sin(a) * 0.9, 0)) * 0.06
        c.lamina(f"faldaplaca{k}", [b0 + V((-math.sin(a), math.cos(a), 0)) * larg, b0 - V((-math.sin(a), math.cos(a), 0)) * larg], 0.0, mat, "calca", grossura=0.008,
                 pesos="prox", so={"calca"}, cair=[d * 0.6 + V((0, 0, -comp * 0.5)), d + V((0, 0, -comp))], balanco="pano")

# ====================================================================== conjuntos
# cada um: (classe, nível, nome, nomes das peças) em SETS; função monta o modelo
def set_cavaleiro_sagrado():
    c = KC.Ctx(fem=False)
    prata = m_prata("prata_sag"); ouro = m_ouro("ouro_sag"); azul = m_cristal("gema_sag", "#bfe6ff", "#3aa8ff", 4.0)
    pano = m_pano("pano_sag", "#1d3f8a", "#0a1a40", bordado="#e8c060", bordado_esc=6); couro = m_couro("couro_sag", "#3a2a1c", "#120a04")
    pena_b = m_pena("pena_sag", "#f2efe6", "#ffffff", "#b8b0a0"); luz = m_brilho("luz_sag", "#ffe9a8", 6)
    bases(c, couro, prata, couro, prata)
    peitoral_placa(c, prata, ouro, off=0.03, larg=0.2, relevo=0.02, friso_r=0.009)
    lamelas(c, prata, ouro, n=3, larg=0.15, off0=0.024); emblema(c, azul, ouro, r=0.03)
    ombreiras(c, prata, ouro, camadas=3, tam=1.45, inclina=24); gemas_ombro(c, azul, ouro); aletas_ombro(c, ouro, prata, tam=1.1)
    gola(c, prata, alto=0.1, r1=0.13, frente_aberta=False)
    cinto(c, ouro, fivela=(azul, ouro), larg=0.032); joias_cinto(c, azul, ouro)
    saia_placas(c, prata, ouro, n=7, comp=0.24); tabardo(c, pano, costas=False, larg=0.11, comp=0.6)
    capa(c, pano, largura=0.4, comprimento=1.2)
    bracadeiras(c, prata, ouro, gema=(azul, ouro)); manopla(c, prata, off=0.012)
    grevas(c, prata, ouro, off=0.022, joelheira=ouro, so_frente=False); bota(c, prata, ouro, cano=0.16, bico=ouro)
    elmo(c, prata, ouro, off=0.024, abertura="fechado", fenda=luz)
    BC.asinhas_elmo(c, pena_b, tam=1.0, n=4)
    aureola(c, ouro, r=0.15, raios=16, m_raio=luz, tipo="sol", m_centro=azul)
    asas(c, "penas", pena_b, ouro, tam=0.95)
    return c, "Armadura do Cavaleiro Sagrado"

def set_dragao_fogo():
    c = KC.Ctx(fem=False)
    placa = m_placa("placa_df", "#4a1a12", "#c06a40")
    lava = m_rachado("lava_df", "#3a1410", "#120404", "#ff5a10", forca=5.0, esc=6)
    ouro = m_ouro("ouro_df"); osso = K.m_bone("chifre_df", base="#2a1c18", stain="#0a0604")
    fogo = m_brilho("fogo_df", "#ff6a10", 3); brasa = m_cristal("brasa_df", "#ffb060", "#ff3a00", 6)
    membrana = m_rachado("membrana_df", "#5a1408", "#1a0402", "#ff6a10", forca=3.0, esc=4, metal=0.0, rough=0.7)
    couro = m_couro("couro_df", "#2a0e08", "#0a0302")
    bases(c, couro, placa, couro, placa)
    peitoral_placa(c, lava, ouro, off=0.032, larg=0.21, relevo=0.022)
    lamelas(c, placa, ouro, n=3, larg=0.15, off0=0.024)
    c.gema("coracao", c.P["Spine2"] + V((0, -0.15, -0.03)), 0.035, brasa, "peitoral", mold=ouro)
    ombreiras(c, placa, ouro, camadas=3, tam=1.45, espinhos=2, m_esp=osso, curva_esp=0.03, inclina=22)
    enfeite_ombros(c, lambda s, o0: chamas(c, fogo, o0 + V((s * 0.06, 0.0, 0.08)), n=5, tam=1.3, so={"peitoral"}, sobe=V((s * 0.3, 0.1, 1)), seed=3 + s))
    gola(c, placa, alto=0.1, r1=0.13, frente_aberta=False)
    cinto(c, ouro, fivela=(brasa, ouro), larg=0.034); saia_placas(c, placa, ouro, n=7, comp=0.26)
    bracadeiras(c, placa, ouro, espinhos=3, m_esp=osso, gema=(brasa, ouro)); manopla(c, placa, off=0.012)
    grevas(c, placa, ouro, off=0.022, joelheira=ouro, espinho_joelho=1, m_esp=osso, so_frente=False); bota(c, placa, ouro, cano=0.16, bico=ouro)
    elmo(c, placa, ouro, off=0.024, abertura="fechado", fenda=fogo)
    chifres_cabeca(c, osso, tam=1.35, tipo="dragao", larg=0.026)
    chamas(c, fogo, c.cabeca + V((0, 0.02, 0.16)), n=5, tam=1.0, peca="capacete", pesos="Head", seed=9)
    asas(c, "morcego", membrana, osso, tam=1.0)
    return c, "Armadura do Dragão de Fogo"

def set_abismo():
    c = KC.Ctx(fem=False)
    placa = m_placa("placa_ab", "#2e1c48", "#9a80c8")
    ouro = m_friso_aceso("ouro_ab", "#d9a640", "#b060ff", 1.2); roxo = m_cristal("cris_ab", "#e0b0ff", "#9a2aff", 5); vaz = m_vazio("vazio_ab")
    anel = m_brilho("anel_ab", "#c070ff", 6); pano = m_pano("pano_ab", "#1a0a2a", "#06020c", bordado="#b070ff", bordado_esc=6, brilho="#a040ff", forca=3)
    sombra = m_rachado("sombra_ab", "#140a20", "#020104", "#8a2aff", forca=3.0, esc=5, metal=0.0, rough=0.6)
    bases(c, pano, placa, pano, placa)
    peitoral_placa(c, placa, ouro, off=0.028, larg=0.19, relevo=0.018); lamelas(c, placa, ouro, n=3, larg=0.14)
    emblema(c, roxo, ouro, r=0.03)
    ombreiras(c, placa, ouro, camadas=3, tam=1.5, inclina=26)
    enfeite_ombros(c, lambda s, o0: cristais(c, roxo, o0 + V((s * 0.07, 0.0, 0.07)), n=5, tam=1.3, so={"peitoral"}, sobe=V((s * 0.4, 0, 1)), seed=10 + s))
    gola(c, placa, alto=0.11, r1=0.14, frente_aberta=False)
    cinto(c, ouro, fivela=(roxo, ouro)); c.saia("manto", pano, c.P["Hips"].z + 0.08, c.P["LeftFoot"].z + 0.02, 0.12, 0.3, aberta=0.4, pregas=0.05)
    capa(c, pano, largura=0.42, comprimento=1.25, rasgada=0.12)
    bracadeiras(c, placa, ouro, gema=(roxo, ouro)); manopla(c, placa)
    grevas(c, placa, ouro, joelheira=ouro, so_frente=False); bota(c, placa, ouro, cano=0.16, bico=ouro)
    capuz(c, pano, ponta=0.02, folga=1.06, friso_m=ouro); mascara(c, placa, anel, off=0.03)
    buraco_negro(c, anel, vaz, r=0.075)
    tentaculos(c, sombra, n=6, tam=1.1)
    return c, "Armadura do Abismo"

def set_gelo_eterno():
    c = KC.Ctx(fem=True)
    gelo = m_rachado("placa_ge", "#9cc8e8", "#4a7aa8", "#bff4ff", forca=3.0, esc=8, rough=0.18)
    prata = m_prata("prata_ge"); cris = m_cristal("cris_ge", "#e8fbff", "#5ad8ff", 3.5)
    pano = m_pano("pano_ge", "#cfe6f5", "#6a90b0", bordado="#ffffff", bordado_esc=7, brilho="#8ae8ff", forca=2)
    pelo = m_pelo("pelo_ge", "#e6edf2", "#ffffff", "#9aaab8")
    bases(c, pano, gelo, pano, gelo)
    peitoral_placa(c, gelo, prata, off=0.024, larg=0.16, relevo=0.014); lamelas(c, gelo, prata, n=3, larg=0.12)
    emblema(c, cris, prata, r=0.028)
    ombreiras(c, gelo, prata, camadas=3, tam=1.3, inclina=28)
    enfeite_ombros(c, lambda s, o0: cristais(c, cris, o0 + V((s * 0.06, 0.0, 0.06)), n=7, tam=1.5, so={"peitoral"}, sobe=V((s * 0.5, 0.1, 1)), seed=20 + s))
    gola(c, pelo, alto=0.08, r1=0.14, frente_aberta=False)
    cinto(c, prata, fivela=(cris, prata))
    c.saia("saia", pano, c.P["Hips"].z + 0.07, c.P["LeftFoot"].z + 0.03, 0.13, 0.28, aberta=0.5, rasgo=0.05, pregas=0.05, frente_curta=0.2)
    capa(c, pano, largura=0.36, comprimento=1.2, rasgada=0.08)
    bracadeiras(c, gelo, prata, gema=(cris, prata)); manopla(c, gelo)
    grevas(c, gelo, prata, joelheira=prata, so_frente=False); bota(c, gelo, prata, cano=0.18, bico=prata)
    elmo(c, gelo, prata, off=0.016, abertura="rosto")
    cristais(c, cris, c.cabeca + V((0, 0.02, 0.1)), n=9, tam=1.6, peca="capacete", pesos="Head", sobe=V((0, 0.15, 1)), abre=0.9, seed=27)
    asas(c, "lascas", cris, prata, tam=0.9, abre=0.95)
    return c, "Armadura do Gelo Eterno"

def set_celestial():
    c = KC.Ctx(fem=False)
    ouro = m_ouro("ouro_cel"); ouro2 = m_metal_gravado("ouro2_cel", "#f0c862", "#fffbe6", "#5a3a10", rough=0.18, esc=8)
    branco = m_pano("pano_cel", "#f4efe2", "#b8ae98", bordado="#e0b050", bordado_esc=7, brilho="#ffd070", forca=2)
    sol = m_cristal("sol_cel", "#fff4c0", "#ffb020", 5.5); luz = m_brilho("luz_cel", "#fff0b0", 7)
    pena_o = m_pena("pena_cel", "#f6e7b8", "#fffaf0", "#c8a050"); pena_b = m_pena("pena2_cel", "#ffffff", "#ffffff", "#d8d0c0")
    bases(c, branco, ouro, branco, ouro)
    peitoral_placa(c, ouro, ouro2, off=0.03, larg=0.2, relevo=0.022); lamelas(c, ouro, ouro2, n=3, larg=0.15)
    c.gema("sol", c.P["Spine2"] + V((0, -0.15, -0.03)), 0.036, sol, "peitoral", mold=ouro2)
    ombreiras(c, ouro, ouro2, camadas=3, tam=1.5, inclina=22); aletas_ombro(c, ouro2, ouro, tam=1.3, n=4); gemas_ombro(c, sol, ouro2)
    gola(c, ouro, alto=0.1, r1=0.13, frente_aberta=False)
    cinto(c, ouro2, fivela=(sol, ouro)); saia_placas(c, ouro, ouro2, n=9, comp=0.3); tabardo(c, branco, costas=True, larg=0.11, comp=0.62)
    capa(c, branco, largura=0.38, comprimento=1.15)
    bracadeiras(c, ouro, ouro2, gema=(sol, ouro2)); manopla(c, ouro)
    grevas(c, ouro, ouro2, joelheira=ouro2, so_frente=False); bota(c, ouro, ouro2, cano=0.17, bico=ouro2)
    elmo(c, ouro, ouro2, off=0.024, abertura="olhos"); coroa(c, ouro2, n=9, alt=0.08, r=0.09, z=0.08, frente_maior=1.8)
    aureola(c, ouro2, r=0.19, raios=20, m_raio=luz, tipo="sol", m_centro=sol, atras=0.19)
    asas(c, "penas", pena_o, ouro2, tam=1.2, m2=pena_b)
    return c, "Armadura Celestial"

def set_floresta():
    c = KC.Ctx(fem=True)
    casca = m_casca_arvore("casca_fl"); ouro = m_ouro("ouro_fl"); folha = K.m_leaf("folha_fl", "#3f7a22", "#9cd84a")
    folha2 = K.m_leaf("folha2_fl", "#6aa82a", "#d0f070"); verde = m_cristal("gema_fl", "#c8ffa0", "#4aff3a", 4.0)
    couro = m_couro("couro_fl", "#3a2a14", "#120a04"); pano = m_pano("pano_fl", "#2a4a1a", "#0c1806", bordado="#c8e070", bordado_esc=6)
    galho = m_casca_arvore("galho_fl", base="#5a4026", dark="#22160a", musgo="#7aa83a")
    bases(c, couro, casca, couro, casca)
    peitoral_placa(c, casca, ouro, off=0.026, larg=0.17, relevo=0.016); lamelas(c, casca, ouro, n=3, larg=0.12)
    emblema(c, verde, ouro, r=0.03)
    ombreiras(c, casca, ouro, camadas=2, tam=1.3, inclina=30)
    enfeite_ombros(c, lambda s, o0: folhas(c, folha, o0 + V((s * 0.05, 0.0, 0.05)), n=14, tam=2.3, so={"peitoral"}, sobe=V((s * 0.4, 0, 1)), seed=30 + s, mat2=folha2))
    folhas(c, folha, c.P["Neck"] + V((0, 0.02, -0.04)), n=16, tam=1.9, so={"peitoral"}, sobe=V((0, 0.3, 0.6)), abre=1.2, seed=37, mat2=folha2)
    cinto(c, ouro, fivela=(verde, ouro))
    c.saia("saia", pano, c.P["Hips"].z + 0.07, c.P["LeftFoot"].z + 0.06, 0.13, 0.28, aberta=0.5, rasgo=0.1, pregas=0.06, frente_curta=0.2)
    for k, z in enumerate((0.0, -0.15)):
        folhas(c, folha2 if k else folha, V((0, c.P["Hips"].y - 0.05, c.P["Hips"].z + z)), n=14, tam=1.5, peca="calca", so={"calca"}, sobe=V((0, 0, -1)), abre=1.4, seed=40 + k, mat2=folha)
    bracadeiras(c, casca, ouro, gema=(verde, ouro)); manopla(c, casca)
    grevas(c, casca, ouro, joelheira=ouro, so_frente=False); bota(c, casca, ouro, cano=0.2)
    elmo(c, casca, ouro, off=0.016, abertura="rosto"); galhada(c, galho, tam=1.3)
    folhas(c, folha, c.cabeca + V((0, 0.03, 0.09)), n=10, tam=1.1, peca="capacete", pesos="Head", sobe=V((0, 0.2, 1)), seed=45, mat2=folha2)
    return c, "Armadura da Floresta"

def set_necromante():
    c = KC.Ctx(fem=True)
    osso = K.m_bone("osso_nec", base="#d8ccb2", stain="#5a4a30"); negro = m_metal_gravado("negro_nec", "#221a28", "#6a5a74", "#050306", rough=0.3, esc=8)
    pano = m_pano("pano_nec", "#2a1238", "#0a040e", bordado="#9a5ad0", bordado_esc=6, brilho="#a050ff", forca=2.5)
    olho = m_brilho("olho_nec", "#b06aff", 7); alma = m_cristal("alma_nec", "#e0c0ff", "#9a3aff", 5); fogo = m_brilho("fogo_nec", "#9a40ff", 3)
    bases(c, pano, negro, pano, negro)
    peitoral_placa(c, negro, osso, off=0.022, larg=0.15, relevo=0.012); lamelas(c, osso, negro, n=4, larg=0.12)
    c.gema("alma", c.P["Spine2"] + V((0, -0.14, -0.04)), 0.03, alma, "peitoral", mold=osso)
    ombreiras(c, negro, osso, camadas=2, tam=1.25, espinhos=2, m_esp=osso, inclina=30)
    enfeite_ombros(c, lambda s, o0: caveira(c, f"cav_omb{s}", o0 + V((s * 0.1, -0.05, 0.05)), 0.055, osso, olho, "peitoral", so={"peitoral"}, olha=V((s * 0.5, -1, 0.2))))
    enfeite_ombros(c, lambda s, o0: chamas(c, fogo, o0 + V((s * 0.08, 0.02, 0.09)), n=4, tam=1.0, so={"peitoral"}, sobe=V((s * 0.2, 0.2, 1)), seed=50 + s))
    gola(c, negro, alto=0.12, r1=0.14)
    cinto(c, osso, fivela=(alma, negro))
    caveira(c, "cav_cinto", V((0, c.P["Hips"].y - 0.14, c.P["Hips"].z + 0.05)), 0.045, osso, olho, "calca", so={"calca"})
    c.saia("manto", pano, c.P["Hips"].z + 0.07, c.P["LeftFoot"].z + 0.0, 0.13, 0.32, aberta=0.35, rasgo=0.12, pregas=0.06)
    capa(c, pano, largura=0.38, comprimento=1.25, rasgada=0.2)
    bracadeiras(c, negro, osso, espinhos=2, m_esp=osso, gema=(alma, osso)); manopla(c, osso)
    grevas(c, negro, osso, joelheira=osso, so_frente=False); bota(c, negro, osso, cano=0.15)
    capuz(c, pano, ponta=0.05, folga=1.1, friso_m=osso); mascara(c, osso, olho, off=0.03)
    coroa(c, osso, n=7, alt=0.06, r=0.1, z=0.1, frente_maior=1.4)
    return c, "Vestes do Necromante"

def set_demoniaco():
    c = KC.Ctx(fem=False)
    placa = m_placa("placa_dm", "#2e0c0c", "#a04030")
    lava = m_rachado("lava_dm", "#2a0a0a", "#080202", "#ff2a10", forca=5.0, esc=6)
    ouro = m_metal_gravado("ouro_dm", "#8a5a2a", "#e0a060", "#1a0804", rough=0.3, esc=9)
    chifre = K.m_bone("chifre_dm", base="#1a1010", stain="#000000"); fogo = m_brilho("fogo_dm", "#ff3a10", 3)
    rubi = m_cristal("rubi_dm", "#ff8a8a", "#ff1a1a", 5); membrana = m_rachado("asa_dm", "#3a0606", "#0a0101", "#ff2a10", forca=2.5, esc=4, metal=0.0, rough=0.7)
    couro = m_couro("couro_dm", "#1a0606", "#050101")
    bases(c, couro, placa, couro, placa)
    peitoral_placa(c, lava, ouro, off=0.032, larg=0.21, relevo=0.024); lamelas(c, placa, ouro, n=3, larg=0.15, off0=0.024)
    c.gema("olho_peito", c.P["Spine2"] + V((0, -0.15, -0.03)), 0.034, rubi, "peitoral", mold=ouro)
    ombreiras(c, placa, ouro, camadas=3, tam=1.45, espinhos=3, m_esp=chifre, curva_esp=0.04, inclina=20)
    enfeite_ombros(c, lambda s, o0: chamas(c, fogo, o0 + V((s * 0.05, 0.02, 0.07)), n=6, tam=1.4, so={"peitoral"}, sobe=V((s * 0.25, 0.15, 1)), seed=60 + s))
    gola(c, placa, alto=0.11, r1=0.13, frente_aberta=False)
    cinto(c, ouro, fivela=(rubi, ouro), larg=0.034); saia_placas(c, placa, ouro, n=7, comp=0.28)
    bracadeiras(c, placa, ouro, espinhos=4, m_esp=chifre, gema=(rubi, ouro)); manopla(c, placa, off=0.012)
    grevas(c, placa, ouro, off=0.022, joelheira=ouro, espinho_joelho=2, m_esp=chifre, so_frente=False); bota(c, placa, ouro, cano=0.16, bico=ouro)
    elmo(c, placa, ouro, off=0.024, abertura="fechado", fenda=fogo)
    chifres_cabeca(c, chifre, tam=1.6, tipo="demonio", larg=0.03)
    asas(c, "morcego", membrana, chifre, tam=1.15)
    return c, "Armadura Demoníaca"

def set_runico_ancestral():
    c = KC.Ctx(fem=False)
    aco = m_metal_gravado("aco_ru", "#3a4a5e", "#a8c0d8", "#0a0e14", rough=0.3, esc=9, brilho="#5ad0ff", forca=4.0)
    prata = m_prata("prata_ru"); runa = m_cristal("runa_ru", "#c8f4ff", "#2ab8ff", 5)
    pedra = m_pedra("pedra_ru", base="#4a5058", light="#8a96a4", glow="#4ad8ff", forca=4)
    pano = m_pano("pano_ru", "#14304a", "#06101a", bordado="#7ad8ff", bordado_esc=6, brilho="#3ac0ff", forca=2.5)
    bases(c, pano, aco, pano, aco)
    peitoral_placa(c, aco, prata, off=0.028, larg=0.19, relevo=0.018); lamelas(c, aco, prata, n=3, larg=0.14)
    emblema(c, runa, prata, r=0.03)
    ombreiras(c, aco, prata, camadas=3, tam=1.45, inclina=26); gemas_ombro(c, runa, prata)
    enfeite_ombros(c, lambda s, o0: rochas(c, pedra, o0 + V((s * 0.13, 0.03, 0.16)), n=3, tam=1.0, so={"peitoral"}, seed=70 + s, espalha=0.6))
    gola(c, aco, alto=0.1, r1=0.13)
    cinto(c, prata, fivela=(runa, prata)); c.saia("manto", pano, c.P["Hips"].z + 0.08, c.P["LeftFoot"].z + 0.03, 0.12, 0.3, aberta=0.45, pregas=0.05)
    tabardo(c, aco, costas=True, larg=0.1, comp=0.55); capa(c, pano, largura=0.38, comprimento=1.2)
    bracadeiras(c, aco, prata, gema=(runa, prata)); manopla(c, aco)
    grevas(c, aco, prata, joelheira=prata, so_frente=False); bota(c, aco, prata, cano=0.16, bico=prata)
    elmo(c, aco, prata, off=0.022, abertura="olhos"); coroa(c, prata, n=5, alt=0.06, r=0.09, z=0.09, frente_maior=1.6)
    aureola(c, prata, r=0.16, raios=0, m_raio=runa, tipo="sol", m_centro=runa)
    return c, "Armadura Rúnica Ancestral"

def set_tempo():
    c = KC.Ctx(fem=False)
    ouro = m_ouro("ouro_tp"); marfim = m_metal_gravado("marfim_tp", "#e8e0cc", "#ffffff", "#5a4a30", rough=0.25, esc=10)
    turq = m_cristal("turq_tp", "#c0fff4", "#2ae0d0", 4.5); vidro = K.m_glass("vidro_tp")
    areia = m_brilho("areia_tp", "#ffd27a", 3); pano = m_pano("pano_tp", "#16484a", "#061818", bordado="#e0c070", bordado_esc=7)
    bases(c, pano, ouro, pano, ouro)
    peitoral_placa(c, marfim, ouro, off=0.028, larg=0.19, relevo=0.018); lamelas(c, ouro, marfim, n=3, larg=0.14)
    ampulheta(c, ouro, vidro, areia, c.P["Spine2"] + V((0, -0.16, -0.06)))
    ombreiras(c, marfim, ouro, camadas=3, tam=1.45, inclina=24)
    enfeite_ombros(c, lambda s, o0: engrenagem(c, f"engr{s}", o0 + V((s * 0.12, -0.02, 0.07)), 0.055, ouro, normal=V((s * 1, -0.4, 0.5)).normalized(), dentes=12, so={"peitoral"}, gema=turq))
    enfeite_ombros(c, lambda s, o0: engrenagem(c, f"engr_b{s}", o0 + V((s * 0.09, 0.06, 0.13)), 0.035, marfim, normal=V((s * 1, 0.3, 0.6)).normalized(), dentes=9, so={"peitoral"}))
    gola(c, ouro, alto=0.1, r1=0.13, frente_aberta=False)
    cinto(c, ouro, fivela=(turq, ouro)); saia_placas(c, marfim, ouro, n=7, comp=0.26); tabardo(c, pano, costas=True, larg=0.1, comp=0.6)
    capa(c, pano, largura=0.38, comprimento=1.2)
    bracadeiras(c, marfim, ouro, gema=(turq, ouro)); manopla(c, ouro)
    grevas(c, marfim, ouro, joelheira=ouro, so_frente=False); bota(c, marfim, ouro, cano=0.16, bico=ouro)
    elmo(c, marfim, ouro, off=0.022, abertura="rosto")
    aureola(c, ouro, r=0.19, raios=12, m_raio=turq, tipo="relogio", m_centro=turq, atras=0.18)
    return c, "Armadura do Tempo"

def set_abissal_profundo():
    c = KC.Ctx(fem=False)
    esc = m_escama("escama_ap", "#1a5a6a", "#04161c", esc=90)
    placa = m_placa("placa_ap", "#1e4a58", "#8ad0d8")
    ouro = m_metal_gravado("ouro_ap", "#b8a060", "#f0e0a0", "#1a1408", rough=0.25, esc=10)
    coral = m_rachado("coral_ap", "#2ab0b8", "#0a3a44", "#8affff", forca=3.0, esc=10, metal=0.0, rough=0.5)
    perola = m_cristal("perola_ap", "#e8ffff", "#40e8ff", 4.0); barb = m_escama("barb_ap", "#2a8a9a", "#062a30", brilho="#60fff0", forca=2.0, esc=25)
    bases(c, esc, placa, esc, placa)
    peitoral_placa(c, placa, ouro, off=0.028, larg=0.19, relevo=0.02); lamelas(c, esc, ouro, n=3, larg=0.14)
    emblema(c, perola, ouro, r=0.03)
    ombreiras(c, placa, ouro, camadas=3, tam=1.45, inclina=26)
    def coral_ombro(s, o0):
        b = o0 + V((s * 0.08, 0.01, 0.07))
        for k in range(4):
            d = V((s * (0.3 + 0.25 * k), 0.15 * (k - 1.5), 1)).normalized()
            p1 = b + d * 0.09; c.tubo(f"coral{s}{k}", [b, p1, p1 + (d + V((s * 0.3, 0, 0.2))).normalized() * 0.07], 0.012, coral, "peitoral", taper=[1, 0.6, 0.15], pesos="prox")
            c.tubo(f"coralr{s}{k}", [p1, p1 + (d + V((-s * 0.4, 0.2, 0.3))).normalized() * 0.05], 0.007, coral, "peitoral", taper=[1, 0.1], pesos="prox")
    enfeite_ombros(c, coral_ombro)
    gola(c, placa, alto=0.1, r1=0.13)
    cinto(c, ouro, fivela=(perola, ouro)); saia_placas(c, esc, ouro, n=9, comp=0.28)
    capa(c, barb, largura=0.36, comprimento=1.1, rasgada=0.15)
    bracadeiras(c, placa, ouro, gema=(perola, ouro)); manopla(c, placa)
    for s in (-1, 1):   # barbatanas nos antebraços
        fa = c.P[("Left" if s > 0 else "Right") + "ForeArm"]; hd = c.P[("Left" if s > 0 else "Right") + "Hand"]
        pena(c, f"barb_braco{s}", fa.lerp(hd, 0.2) + V((s * 0.04, 0.02, 0)), (hd - fa).normalized(), V((s, 0, 0)), 0.2, 0.05, barb, "luvas", "prox", so={"luvas"}, forma="barbatana", grossura=0.005)
    grevas(c, placa, ouro, joelheira=ouro, so_frente=False); bota(c, placa, ouro, cano=0.16, bico=ouro)
    elmo(c, placa, ouro, off=0.022, abertura="olhos")
    for s in (-1, 1):
        pena(c, f"barb_elmo{s}", c.cabeca + V((s * 0.1, 0.0, 0.03)), V((s * 0.4, 0.5, 0.6)).normalized(), V((0, 0, 1)), 0.17, 0.06, barb, "capacete", "Head", forma="barbatana", grossura=0.005)
    pena(c, "crista", c.cabeca + V((0, 0.0, 0.12)), V((0, 0.8, 0.5)).normalized(), V((0, 0, 1)).cross(V((0, 0.8, 0.5))).normalized() if False else V((0, 0.5, -0.8)).normalized(),
         0.22, 0.06, barb, "capacete", "Head", forma="barbatana", grossura=0.005)
    return c, "Armadura Abissal"

def set_faraonico():
    c = KC.Ctx(fem=True)
    ouro = m_ouro("ouro_fa"); listra = m_listrado("listra_fa", "#d9a640", "#1a3a9a", esc=45)
    lapis = m_cristal("lapis_fa", "#7aa0ff", "#2a5aff", 3.5); turq = m_cristal("turq_fa", "#a0fff0", "#20d8c0", 3.5)
    linho = m_pano("linho_fa", "#efe6d0", "#a89a7c", bordado="#d9a640", bordado_esc=8)
    bases(c, linho, ouro, linho, ouro)
    peitoral_placa(c, ouro, listra, off=0.02, larg=0.15, relevo=0.012)
    colar_largo(c, listra, ouro, turq, r=0.22)
    emblema(c, lapis, ouro, r=0.03)
    ombreiras(c, ouro, listra, camadas=2, tam=1.15, inclina=34)
    enfeite_ombros(c, lambda s, o0: pena(c, f"asa_esc{s}", o0 + V((s * 0.05, 0.0, 0.03)), V((s, 0.05, -0.15)).normalized(), V((0, 1, 0)), 0.22, 0.06, listra, "peitoral", "prox", so={"peitoral"}, forma="pena", grossura=0.006))
    cinto(c, ouro, fivela=(lapis, ouro), larg=0.036)
    c.saia("saiote", listra, c.P["Hips"].z + 0.07, c.P["LeftLeg"].z + 0.02, 0.13, 0.22, pregas=0.08)
    tabardo(c, listra, costas=False, larg=0.07, comp=0.5)
    capa(c, linho, largura=0.34, comprimento=1.15)
    bracadeiras(c, ouro, listra, gema=(turq, ouro)); manopla(c, ouro)
    grevas(c, ouro, listra, joelheira=ouro, so_frente=True); bota(c, ouro, listra, cano=0.14)
    nemes(c, listra, ouro, lapis)
    return c, "Vestes Faraônicas"

def set_pedra():
    c = KC.Ctx(fem=False)
    pedra = m_pedra("pedra_pd", base="#5e5850", light="#a39c90", glow="#40d0ff", forca=4.5)
    pedra2 = m_pedra("pedra2_pd", base="#4a4640", light="#86807a", glow="#40d0ff", forca=3.0)
    bronze = m_metal_gravado("bronze_pd", "#7a5a34", "#d0a870", "#1a1006", rough=0.38, esc=8)
    gema = m_cristal("gema_pd", "#c8f4ff", "#2ac0ff", 5)
    bases(c, pedra2, pedra2, pedra2, pedra2)
    peitoral_placa(c, pedra, bronze, off=0.04, larg=0.22, relevo=0.03, friso_r=0.01)
    lamelas(c, pedra, bronze, n=3, larg=0.16, off0=0.03)
    c.gema("nucleo", c.P["Spine2"] + V((0, -0.17, -0.04)), 0.04, gema, "peitoral", mold=bronze)
    ombreiras(c, pedra, bronze, camadas=2, tam=1.5, inclina=18)
    enfeite_ombros(c, lambda s, o0: rochas(c, pedra, o0 + V((s * 0.09, 0.0, 0.1)), n=5, tam=1.6, so={"peitoral"}, seed=80 + s, espalha=0.7))
    rochas(c, pedra2, c.P["Spine2"] + V((0, 0.16, 0.05)), n=6, tam=1.5, so={"peitoral"}, seed=85, espalha=0.9)
    gola(c, pedra, alto=0.12, r1=0.15, frente_aberta=False)
    cinto(c, bronze, fivela=(gema, bronze), larg=0.04); saia_placas(c, pedra, bronze, n=7, comp=0.24, larg=0.07)
    bracadeiras(c, pedra, bronze, gema=(gema, bronze), off=0.02)
    enfeite_ombros(c, lambda s, o0: rochas(c, pedra2, c.P[("Left" if s > 0 else "Right") + "ForeArm"] + V((s * 0.04, 0, 0.03)), n=3, tam=1.0, peca="luvas", so={"luvas"}, seed=90 + s, espalha=0.6))
    manopla(c, pedra, off=0.016)
    grevas(c, pedra, bronze, off=0.03, joelheira=pedra, so_frente=False); bota(c, pedra, bronze, cano=0.18, off=0.02, bico=pedra)
    elmo(c, pedra, bronze, off=0.03, abertura="fechado", fenda=m_brilho("fenda_pd", "#5ad8ff", 7))
    rochas(c, pedra, c.cabeca + V((0, 0.03, 0.1)), n=4, tam=1.1, peca="capacete", pesos="Head", seed=95, espalha=0.6)
    return c, "Armadura de Pedra Viva"

def set_vazio():
    c = KC.Ctx(fem=False)
    negro = m_placa("negro_vz", "#1a1226", "#7a6a9a")
    prata = m_friso_aceso("prata_vz", "#8a80a8", "#c070ff", 1.5)
    roxo = m_cristal("cris_vz", "#f0c8ff", "#a020ff", 6); vaz = m_vazio("vazio_vz"); anel = m_brilho("anel_vz", "#d080ff", 7)
    pano = m_pano("pano_vz", "#100818", "#020104", bordado="#c070ff", bordado_esc=7, brilho="#a040ff", forca=3)
    sombra = m_rachado("sombra_vz", "#0a0610", "#000000", "#b050ff", forca=3.5, esc=5, metal=0.0, rough=0.6)
    bases(c, pano, negro, pano, negro)
    peitoral_placa(c, negro, prata, off=0.028, larg=0.19, relevo=0.02)
    lamelas(c, negro, prata, n=3, larg=0.14)
    c.esfera("vazio_peito", c.P["Spine2"] + V((0, -0.15, -0.035)), (0.032, 0.02, 0.032), vaz, "peitoral")
    bpy.ops.mesh.primitive_torus_add(major_radius=0.045, minor_radius=0.006, location=c.P["Spine2"] + V((0, -0.155, -0.035)), rotation=(math.radians(90), 0, 0))
    t = bpy.context.active_object; t.data.materials.append(anel); c.add(t, "peitoral", "Spine2")
    ombreiras(c, negro, prata, camadas=3, tam=1.35, espinhos=3, m_esp=prata, curva_esp=0.04, inclina=24)
    enfeite_ombros(c, lambda s, o0: cristais(c, roxo, o0 + V((s * 0.08, 0.02, 0.08)), n=4, tam=1.6, so={"peitoral"}, sobe=V((s * 0.3, 0.2, 1)), seed=100 + s))
    gola(c, negro, alto=0.12, r1=0.14, frente_aberta=False)
    cinto(c, prata, fivela=(roxo, prata)); c.saia("manto", pano, c.P["Hips"].z + 0.08, c.P["LeftFoot"].z + 0.0, 0.12, 0.3, aberta=0.42, rasgo=0.1, pregas=0.05)
    capa(c, pano, largura=0.42, comprimento=1.3, rasgada=0.18)
    bracadeiras(c, negro, prata, espinhos=2, m_esp=prata, gema=(roxo, prata)); manopla(c, negro)
    grevas(c, negro, prata, joelheira=prata, so_frente=False); bota(c, negro, prata, cano=0.16, bico=prata)
    elmo(c, negro, prata, off=0.024, abertura="fechado", fenda=anel)
    coroa(c, prata, n=7, alt=0.08, r=0.09, z=0.09, frente_maior=1.7, curva=0.02)
    buraco_negro(c, anel, vaz, r=0.09, atras=0.22, z=0.12)
    tentaculos(c, sombra, n=4, tam=1.0, seed=7)
    return c, "Armadura do Vazio"

def set_anjo_caido():
    c = KC.Ctx(fem=False)
    aco = m_metal_gravado("aco_ac", "#5a5c66", "#d8dae4", "#0a0a0e", rough=0.22, esc=10)
    escuro = m_metal_gravado("escuro_ac", "#24222a", "#8a8494", "#050406", rough=0.3, esc=8)
    rubi = m_cristal("rubi_ac", "#ff9a9a", "#e01030", 5); vermelho = m_pano("pano_ac", "#7a0a14", "#200204", bordado="#d0a060", bordado_esc=6)
    pena_n = m_pena("pena_ac", "#2a262c", "#8a8490", "#0a080c"); pena_b = m_pena("pena2_ac", "#e8e4e0", "#ffffff", "#9a9090")
    luz = m_brilho("luz_ac", "#ff3040", 5)
    bases(c, vermelho, escuro, vermelho, escuro)
    peitoral_placa(c, aco, escuro, off=0.03, larg=0.2, relevo=0.022); lamelas(c, escuro, aco, n=3, larg=0.15)
    c.gema("coracao", c.P["Spine2"] + V((0, -0.15, -0.03)), 0.034, rubi, "peitoral", mold=escuro)
    ombreiras(c, aco, escuro, camadas=3, tam=1.5, espinhos=2, m_esp=escuro, inclina=24); aletas_ombro(c, escuro, aco, tam=1.1); gemas_ombro(c, rubi, escuro)
    gola(c, escuro, alto=0.11, r1=0.13, frente_aberta=False)
    cinto(c, escuro, fivela=(rubi, aco)); saia_placas(c, aco, escuro, n=7, comp=0.26); tabardo(c, vermelho, costas=False, larg=0.1, comp=0.6, rasgo=0.06)
    capa(c, vermelho, largura=0.4, comprimento=1.2, rasgada=0.2)
    bracadeiras(c, aco, escuro, gema=(rubi, escuro)); manopla(c, escuro)
    grevas(c, aco, escuro, joelheira=escuro, espinho_joelho=1, m_esp=escuro, so_frente=False); bota(c, aco, escuro, cano=0.16, bico=escuro)
    elmo(c, aco, escuro, off=0.024, abertura="olhos")
    aureola(c, luz, r=0.15, tipo="quebrada")
    asas(c, "penas", pena_b, escuro, tam=1.1, m2=pena_n)
    return c, "Armadura do Anjo Caído"

SETS = {
    # id: (função, classe, nível, sufixo do nome, nomes das 5 peças)
    "arm_cavaleiro_sagrado": (set_cavaleiro_sagrado, "guerreiro", 60, dict(capacete="Elmo Sagrado", peitoral="Couraça Sagrada", luvas="Manoplas Sagradas", calca="Coxotes Sagrados", botas="Grevas Sagradas")),
    "arm_dragao_fogo": (set_dragao_fogo, "guerreiro", 70, dict(capacete="Elmo do Dragão de Fogo", peitoral="Couraça do Dragão de Fogo", luvas="Garras do Dragão de Fogo", calca="Coxotes do Dragão de Fogo", botas="Grevas do Dragão de Fogo")),
    "arm_abismo": (set_abismo, "mago", 70, dict(capacete="Capuz do Abismo", peitoral="Couraça do Abismo", luvas="Manoplas do Abismo", calca="Manto do Abismo", botas="Botas do Abismo")),
    "arm_gelo_eterno": (set_gelo_eterno, "bruxa", 60, dict(capacete="Coroa de Gelo Eterno", peitoral="Corpete de Gelo Eterno", luvas="Braçadeiras de Gelo Eterno", calca="Saia de Gelo Eterno", botas="Botas de Gelo Eterno")),
    "arm_celestial": (set_celestial, "arqueiro", 80, dict(capacete="Elmo Celestial", peitoral="Couraça Celestial", luvas="Braçadeiras Celestiais", calca="Faldas Celestiais", botas="Botas Celestiais")),
    "arm_floresta": (set_floresta, "bruxa", 55, dict(capacete="Galhada da Floresta", peitoral="Corpete da Floresta", luvas="Braçadeiras da Floresta", calca="Saia de Folhas", botas="Botas de Raiz")),
    "arm_necromante": (set_necromante, "bruxa", 65, dict(capacete="Capuz do Necromante", peitoral="Corpete de Ossos do Necromante", luvas="Garras do Necromante", calca="Manto do Necromante", botas="Botas do Necromante")),
    "arm_demoniaco": (set_demoniaco, "guerreiro", 80, dict(capacete="Elmo Demoníaco", peitoral="Couraça Demoníaca", luvas="Manoplas Demoníacas", calca="Coxotes Demoníacos", botas="Grevas Demoníacas")),
    "arm_runico_ancestral": (set_runico_ancestral, "mago", 55, dict(capacete="Elmo Rúnico Ancestral", peitoral="Couraça Rúnica Ancestral", luvas="Manoplas Rúnicas Ancestrais", calca="Manto Rúnico Ancestral", botas="Botas Rúnicas Ancestrais")),
    "arm_tempo": (set_tempo, "mago", 80, dict(capacete="Elmo do Tempo", peitoral="Couraça do Tempo", luvas="Manoplas do Tempo", calca="Faldas do Tempo", botas="Botas do Tempo")),
    "arm_abissal_profundo": (set_abissal_profundo, "arqueiro", 65, dict(capacete="Elmo Abissal", peitoral="Couraça Abissal", luvas="Braçadeiras Abissais", calca="Faldas Abissais", botas="Botas Abissais")),
    "arm_faraonico": (set_faraonico, "bruxa", 75, dict(capacete="Nemes Faraônico", peitoral="Peitoral Faraônico", luvas="Braceletes Faraônicos", calca="Saiote Faraônico", botas="Sandálias Faraônicas")),
    "arm_pedra": (set_pedra, "guerreiro", 55, dict(capacete="Elmo de Pedra Viva", peitoral="Couraça de Pedra Viva", luvas="Punhos de Pedra Viva", calca="Coxotes de Pedra Viva", botas="Pés de Pedra Viva")),
    "arm_vazio": (set_vazio, "mago", 90, dict(capacete="Elmo do Vazio", peitoral="Couraça do Vazio", luvas="Manoplas do Vazio", calca="Manto do Vazio", botas="Botas do Vazio")),
    "arm_anjo_caido": (set_anjo_caido, "arqueiro", 90, dict(capacete="Elmo do Anjo Caído", peitoral="Couraça do Anjo Caído", luvas="Braçadeiras do Anjo Caído", calca="Faldas do Anjo Caído", botas="Botas do Anjo Caído")),
}

if __name__ == "__main__":
    import os; os.makedirs(OUT, exist_ok=True)
    for aid in (_args or list(SETS)):
        fn = SETS[aid][0]
        c, nome = fn()
        alvo = {"capacete": 10000, "peitoral": 30000, "luvas": 8000, "calca": 15000, "botas": 8000}
        for o in c.parts:     # tubos, esferas e placas grandes entram na redução; penas, gemas e lascas ficam inteiras
            if o.name in bpy.data.objects and sum(len(p.vertices) - 2 for p in o.data.polygons) > 700: o["grande"] = True
        c.exportar(aid, nome, tex=2048, alvo=alvo, out_dir=f"{OUT}/{aid}")

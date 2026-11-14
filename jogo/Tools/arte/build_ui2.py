"""HUD v2 (estudo visual 02, estilo Diablo IV): ferro escuro gotico com quinas de aco gasto, ouro so em destaque.
Formas recortadas (espinhos, lancas, placas em angulo) extrudadas e chanfradas em 3D. Uso: python3 build_ui2.py <saida> [peca...]"""
import sys, os, math
OUT = sys.argv[1]; WANT = sys.argv[2:]
sys.argv = [sys.argv[0], OUT, "__nada__"]
sys.path.insert(0, "/home/claude/cinzas/tools")
import build_ui as B
import bpy, bmesh
from mathutils import Vector
from PIL import Image
U = B.U

_ORIG = B.mat
def mat(kind):
    if kind in B.MATS: return B.MATS[kind]
    if kind not in ("aco", "aco2", "prata", "brasa"): return _ORIG(kind)
    m = bpy.data.materials.new(kind); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    g = nt.nodes.new("ShaderNodeNewGeometry")
    ramp = nt.nodes.new("ShaderNodeMapRange"); ramp.inputs[1].default_value = 0.5; ramp.inputs[2].default_value = 0.55
    nt.links.new(g.outputs["Pointiness"], ramp.inputs[0])
    mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = 'RGBA'
    base, worn, rough, metal = {"aco": ((0.016, 0.017, 0.019, 1), (0.42, 0.42, 0.44, 1), 0.38, 0.95),
                                "aco2": ((0.008, 0.008, 0.009, 1), (0.12, 0.12, 0.13, 1), 0.55, 0.8),
                                "prata": ((0.35, 0.35, 0.37, 1), (0.75, 0.75, 0.78, 1), 0.25, 1.0),
                                "brasa": ((0.9, 0.35, 0.05, 1), (1, 0.7, 0.3, 1), 0.3, 0.0)}[kind]
    mx.inputs[6].default_value = base; mx.inputs[7].default_value = worn
    nt.links.new(ramp.outputs[0], mx.inputs[0]); nt.links.new(mx.outputs[2], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    tc = nt.nodes.new("ShaderNodeTexCoord"); n = nt.nodes.new("ShaderNodeTexVoronoi"); n.inputs["Scale"].default_value = 18
    nt.links.new(tc.outputs["Object"], n.inputs["Vector"])
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.3; bp.inputs["Distance"].default_value = 0.002
    nt.links.new(n.outputs["Distance"], bp.inputs["Height"]); nt.links.new(bp.outputs["Normal"], b.inputs["Normal"])
    if kind == "brasa": b.inputs["Emission Color"].default_value = (1, 0.5, 0.1, 1); b.inputs["Emission Strength"].default_value = 2.0
    B.MATS[kind] = m; return m
B.mat = mat   # as funcoes do build_ui passam a enxergar os materiais novos

def shape(pts, z0, z1, m, bevel=2.0, name="forma"):
    """poligono 2D em px (x, y para baixo) extrudado de z0 a z1 e chanfrado"""
    bm = bmesh.new()
    vs = [bm.verts.new((x * U, -y * U, 0)) for x, y in pts]
    f = bm.faces.new(vs)
    if f.normal.z < 0: f.normal_flip()
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); o.location.z = z0 * U
    so = o.modifiers.new("s", 'SOLIDIFY'); so.thickness = (z1 - z0) * U; so.offset = 1
    if bevel:
        bv = o.modifiers.new("bv", 'BEVEL'); bv.width = bevel * U; bv.segments = 2; bv.limit_method = 'ANGLE'
    o.data.materials.append(mat(m)); return o

def spike(cx, cy, ang, length, width, z0, z1, m, curve=0.0):
    """espinho/lanca: triangulo saindo de (cx, cy) na direcao ang (graus, 0 = direita, 90 = cima)"""
    a = math.radians(ang); dx, dy = math.cos(a), -math.sin(a); px, py = -dy, dx
    tip = (cx + dx * length + px * curve, cy + dy * length + py * curve)
    return shape([(cx + px * width / 2, cy + py * width / 2), tip, (cx - px * width / 2, cy - py * width / 2), (cx - dx * width * 0.3, cy - dy * width * 0.3)], z0, z1, m, bevel=1.5)

def lanca(x0, y, x1, m="aco", h=13, ponta=56):
    """braco em lanca: haste fina terminando em ponta de seta com farpas"""
    s = 1 if x1 > x0 else -1
    shape([(x0, y - h / 2), (x1 - s * ponta, y - h / 2), (x1 - s * ponta, y - h / 2 - 12), (x1, y), (x1 - s * ponta, y + h / 2 + 12), (x1 - s * ponta, y + h / 2), (x0, y + h / 2)], 0, 9, m, bevel=1.8)
    shape([(x1 - s * (ponta + 6), y - 5), (x1 - s * (ponta + 30), y - 18), (x1 - s * (ponta + 22), y - 3)], 0, 7, m, bevel=1)    # farpa
    shape([(x1 - s * (ponta + 6), y + 5), (x1 - s * (ponta + 30), y + 18), (x1 - s * (ponta + 22), y + 3)], 0, 7, m, bevel=1)

# ------------------------------------------------------------------ pecas
def p_orbe():
    """aro do orbe com espinhos. Quadro de 640 px com o vidro de raio 196 (OrbeHud.FracRaio = 196/640)"""
    B.reset(); S, C, R = 640, 320, 196
    B.ring(C, C, R + 38, R, 0, 18, "aco", bevel=7); B.ring(C, C, R + 5, R - 3, 10, 21, "prata", bevel=1.5)
    B.ring(C, C, R + 38, R + 32, 8, 20, "prata", bevel=1)
    for ang, L, W in ((90, 64, 70), (270, 88, 76), (25, 40, 40), (155, 40, 40), (205, 50, 44), (335, 50, 44), (0, 30, 50), (180, 30, 50)):
        a = math.radians(ang); spike(C + math.cos(a) * (R + 32), C - math.sin(a) * (R + 32), ang, L, W, 0, 18, "aco")
        spike(C + math.cos(a) * (R + 36), C - math.sin(a) * (R + 36), ang, L * 0.55, W * 0.35, 14, 22, "prata")      # nervura
    for ang in (90, 270):
        a = math.radians(ang); x, y = C + math.cos(a) * (R + 22), C - math.sin(a) * (R + 22)
        shape([(x - 26, y), (x, y - 18), (x + 26, y), (x, y + 18)], 12, 24, "aco", bevel=2); B.gem(x, y, 10, 25, "rubi")
    for k in range(32):
        a = k / 32 * math.tau; B.sphere(C + math.cos(a) * (R + 19), C + math.sin(a) * (R + 19), 3.6, 18, "prata", 0.6)
    B.render("ui_orbe_moldura", S, S)

def p_barra():
    """barra de baixo (1800 x 220): placa central dos atalhos (x 540..1260) + bracos em lanca.
    Orbes centrados em x = 380 e x = 1420, y = 112 (o OrbeHud com ~330 px de quadro nessa escala cobre o encaixe)."""
    B.reset(); W, H = 1800, 220; cy = 150
    shape([(560, 92), (1240, 92), (1262, 112), (1262, 208), (538, 208), (538, 112)], 0, 10, "aco2", bevel=4)
    shape([(560, 92), (1240, 92), (1252, 104), (548, 104)], 8, 14, "aco", bevel=2)
    shape([(548, 196), (1252, 196), (1262, 208), (538, 208)], 8, 14, "aco", bevel=2)
    B.box(548, 104, 704, 2, 13, 15, "prata", bevel=0.6)
    for x0, x1 in ((498, 538), (1262, 1302)): shape([(x0, cy - 16), (x1, cy - 20), (x1, cy + 20), (x0, cy + 16)], 0, 10, "aco", bevel=2)    # ligacao orbe-placa
    for x0, x1 in ((262, 20), (1538, 1780)): lanca(x0, cy, x1)
    for x0, x1 in ((270, 110), (1530, 1690)): shape([(x0, cy - 36), (x1, cy - 16), (x1, cy - 12), (x0, cy - 30)], 0, 6, "aco2", bevel=1)
    B.render("ui_barra_inferior", W, H)

def slot(name, gold=False):
    B.reset(); S = 128
    for (x, y, w, h) in ((0, 0, S, 9), (0, S - 9, S, 9), (0, 0, 9, S), (S - 9, 0, 9, S)): B.box(x, y, w, h, 0, 8, "aco", bevel=3)
    for (x, y, w, h) in ((8, 8, S - 16, 2), (8, S - 10, S - 16, 2), (8, 8, 2, S - 16), (S - 10, 8, 2, S - 16)): B.box(x, y, w, h, 5, 9, "ouro" if gold else "prata", bevel=0.6)
    for (cx, cy, a) in ((0, 0, 135), (S, 0, 45), (0, S, 225), (S, S, 315)): spike(cx + (8 if cx == 0 else -8), cy + (8 if cy == 0 else -8), a, 10, 12, 4, 11, "ouro" if gold else "aco")
    if gold:
        g = B.MATS["ouro"].node_tree.nodes["Principled BSDF"]; g.inputs["Emission Color"].default_value = (1, 0.65, 0.2, 1); g.inputs["Emission Strength"].default_value = 0.6
    B.render(name, S, S)

def p_slots(): slot("ui_slot_hab"); slot("ui_slot_hab_ativo", gold=True)

def p_xp():
    B.reset(); W, H = 1024, 40
    shape([(0, 20), (16, 6), (W - 16, 6), (W, 20), (W - 16, 34), (16, 34)], 0, 8, "aco", bevel=2)
    shape([(18, 12), (W - 18, 12), (W - 18, 28), (18, 28)], 6, 9, "aco2", bevel=0)     # trilho (o codigo pinta o roxo por cima)
    for k in range(1, 10): B.box(18 + k * (W - 36) / 10 - 1, 12, 2, 16, 8, 11, "aco", bevel=0.4)
    B.render("ui_xp_moldura", W, H)

def p_zona():
    """placa do nome da area (3 fatias, pontas de 64 px): pontas em angulo + encaixe do icone"""
    B.reset(); W, H = 512, 64
    shape([(0, 10), (W - 30, 10), (W, 32), (W - 30, 54), (0, 54)], 0, 8, "aco2", bevel=3)
    shape([(4, 12), (W - 32, 12), (W - 4, 32), (W - 32, 52), (4, 52)], 6, 9, "aco2", bevel=0)
    shape([(0, 8), (W - 31, 8), (W - 27, 12), (0, 12)], 6, 12, "prata", bevel=0.6); shape([(0, 52), (W - 27, 52), (W - 31, 56), (0, 56)], 6, 12, "prata", bevel=0.6)
    shape([(6, 4), (58, 4), (64, 32), (58, 60), (6, 60)], 0, 14, "aco", bevel=3)       # encaixe do icone
    B.render("ui_placa_zona", W, H)

def p_bussola():
    B.reset(); W, H = 512, 40
    B.box(0, 19, W, 2, 0, 6, "prata", bevel=0.5)
    for k in range(0, 17):
        x = 8 + k * (W - 16) / 16; h = 12 if k % 4 == 0 else 6
        B.box(x - 1, 20 - h / 2, 2, h, 0, 7, "prata", bevel=0.3)
    shape([(256, 6), (270, 20), (256, 34), (242, 20)], 2, 12, "aco", bevel=2); shape([(256, 12), (263, 20), (256, 28), (249, 20)], 10, 14, "prata", bevel=1)
    B.render("ui_bussola", W, H)

def p_chefe():
    """barra de vida de chefe (1024 x 64, 3 fatias de 120 px): pontas de lanca e brasao no meio"""
    B.reset(); W, H = 1024, 64; y = 34
    shape([(60, y - 12), (W - 60, y - 12), (W - 60, y + 12), (60, y + 12)], 0, 8, "aco", bevel=3)
    shape([(64, y - 8), (W - 64, y - 8), (W - 64, y + 8), (64, y + 8)], 6, 9, "aco2", bevel=0)
    for s in (1, -1):
        x0 = 60 if s > 0 else W - 60
        shape([(x0, y - 14), (x0 - s * 30, y - 14), (x0 - s * 58, y), (x0 - s * 30, y + 14), (x0, y + 14)], 0, 12, "aco", bevel=2.5)
        B.gem(x0 - s * 26, y, 6, 13, "rubi")
    shape([(W / 2 - 34, y - 14), (W / 2 + 34, y - 14), (W / 2 + 18, y - 30), (W / 2, y - 34), (W / 2 - 18, y - 30)], 0, 12, "aco", bevel=2)
    B.gem(W / 2, y - 22, 7, 13, "rubi")
    B.render("ui_barra_chefe", W, H)

def p_alvo():
    B.reset(); W, H = 512, 28; y = 14
    shape([(14, y - 8), (W - 14, y - 8), (W, y), (W - 14, y + 8), (14, y + 8), (0, y)], 0, 7, "aco", bevel=2)
    shape([(16, y - 5), (W - 16, y - 5), (W - 16, y + 5), (16, y + 5)], 5, 8, "aco2", bevel=0)
    B.render("ui_barra_alvo", W, H)

def frame(name, W, H, band, cap, gold=False, center=True):
    """moldura gotica fina: faixa de aco, filete de prata, cantos em ponta (9 fatias)"""
    B.reset()
    if center: B.box(band - 2, band - 2, W - 2 * band + 4, H - 2 * band + 4, -2, 0, "couro")
    for (x, y, w, h) in ((0, 0, W, band), (0, H - band, W, band), (0, 0, band, H), (W - band, 0, band, H)): B.box(x, y, w, h, 0, 8, "aco", bevel=band * 0.3)
    t = max(2, band // 6)
    for (x, y, w, h) in ((band - t, band - t, W - 2 * band + 2 * t, t), (band - t, H - band, W - 2 * band + 2 * t, t), (band - t, band - t, t, H - 2 * band + 2 * t), (W - band, band - t, t, H - 2 * band + 2 * t)):
        B.box(x, y, w, h, 6, 10, "prata", bevel=t * 0.4)
    for (cx, cy) in ((0, 0), (W - cap, 0), (0, H - cap), (W - cap, H - cap)):
        shape([(cx, cy + cap * 0.25), (cx + cap * 0.25, cy), (cx + cap * 0.75, cy), (cx + cap, cy + cap * 0.25), (cx + cap, cy + cap * 0.75), (cx + cap * 0.75, cy + cap), (cx + cap * 0.25, cy + cap), (cx, cy + cap * 0.75)], 0, 13, "aco", bevel=2)
        B.gem(cx + cap / 2, cy + cap / 2, cap * 0.2, 13, "esmeralda" if gold else "rubi")
    def vig(im):
        return B.vignette()(im)
    B.render(name, W, H, vig)

def p_molduras():
    frame("ui_moldura", 256, 256, 18, 26)
    frame("ui_moldura_fina", 96, 96, 7, 10)
    frame("ui_tooltip", 256, 256, 10, 18)

def p_aba():
    """aba de titulo de painel (ex.: 'Inventario'): 3 fatias, ponta direita inclinada"""
    B.reset(); W, H = 400, 48
    shape([(0, 4), (W - 40, 4), (W, 44), (0, 44)], 0, 8, "aco2", bevel=3)
    shape([(0, 2), (W - 41, 2), (W - 38, 6), (0, 6)], 6, 11, "prata", bevel=0.6)
    shape([(0, 42), (W - 2, 42), (W, 46), (0, 46)], 6, 11, "prata", bevel=0.6)
    B.render("ui_aba", W, H)

def p_mapa():
    """moldura da tela de mapa (9 fatias, margem 48): grossa, cantos com espinho e rubi"""
    frame("ui_mapa_moldura", 512, 512, 30, 48, center=False)

def icon(name, build, S=64):
    B.reset(); build(S)
    g0 = B.grade; B.grade = lambda im: g0(im, gamma=1.1, gain=1.25, sat=1.2)
    B.render(name, S, S); B.grade = g0

def p_icones():
    def ancora(S):
        c = S / 2; B.box(c - 3, 10, 6, 40, 0, 8, "prata", bevel=1.5); B.ring(c, 12, 7, 3.5, 0, 8, "prata", bevel=1)
        B.box(c - 14, 20, 28, 5, 0, 8, "prata", bevel=1.5)
        shape([(c - 22, 38), (c - 18, 50), (c, 56), (c + 18, 50), (c + 22, 38), (c + 16, 44), (c, 49), (c - 16, 44)], 0, 8, "prata", bevel=1.2)
    def picareta(S):
        c = S / 2; shape([(12, 48), (16, 52), (48, 20), (44, 16)], 0, 8, "bronze", bevel=1.2)
        shape([(30, 8), (54, 22), (58, 34), (46, 22), (22, 14)], 0, 9, "prata", bevel=1.2)
    def cidade(S):
        shape([(14, 54), (14, 22), (20, 22), (20, 14), (26, 14), (26, 22), (38, 22), (38, 14), (44, 14), (44, 22), (50, 22), (50, 54)], 0, 8, "prata", bevel=1.2)
        shape([(28, 54), (28, 40), (32, 36), (36, 40), (36, 54)], 6, 10, "aco", bevel=0.6)
    def cacada(S):
        for s in (1, -1):
            x0 = 32 - s * 20
            shape([(x0, 54), (x0 + s * 4, 58), (32 + s * 22, 12), (32 + s * 18, 8)], 0, 8, "prata", bevel=1)
            B.box(x0 + s * 6 - 6, 44, 12, 4, 0, 9, "bronze", bevel=1)
    def masmorra(S):
        shape([(10, 56), (10, 30), (18, 16), (32, 10), (46, 16), (54, 30), (54, 56), (42, 56), (42, 34), (38, 26), (32, 24), (26, 26), (22, 34), (22, 56)], 0, 9, "prata", bevel=1.2)
    def passagem(S):
        shape([(8, 50), (8, 40), (56, 40), (56, 50), (48, 50), (44, 46), (20, 46), (16, 50)], 0, 8, "prata", bevel=1.2)
        for x in (14, 50): B.box(x - 3, 50, 6, 8, 0, 8, "prata", bevel=1)
    def posicao(S):
        shape([(32, 6), (52, 56), (32, 44), (12, 56)], 0, 10, "ouro", bevel=1.5)
    for n, f in (("ico_zona_ancora", ancora), ("ico_zona_picareta", picareta), ("ico_mapa_cidade", cidade), ("ico_mapa_cacada", cacada),
                 ("ico_mapa_masmorra", masmorra), ("ico_mapa_passagem", passagem), ("ico_mapa_posicao", posicao)):
        icon(n, f)

ALL = dict(orbe=p_orbe, barra=p_barra, slots=p_slots, xp=p_xp, zona=p_zona, bussola=p_bussola, chefe=p_chefe, alvo=p_alvo,
           molduras=p_molduras, aba=p_aba, mapa=p_mapa, icones=p_icones)
os.makedirs(OUT, exist_ok=True)
for k, f in ALL.items():
    if not WANT or k in WANT: f()

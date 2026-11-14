"""Prova do HUD v2 (estudo 02) sobre o print do jogo. Uso: python3 mock_hud2.py <print.png> <arte_v2> <arte_v1> <saida.png>"""
import sys, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
src, art, art1, out = sys.argv[1:5]
A = lambda n: Image.open(f"{art}/{n}.png").convert("RGBA")
F = lambda s: ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", s)
FR = lambda s: ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerifCondensed.ttf", s)
GOLD, CREAM, DIM, SILVER = (226, 186, 110), (236, 228, 212), (160, 150, 135), (200, 200, 205)
shot = Image.open(src).convert("RGBA")
img = shot.copy()
# o print tem a janela antiga na direita e o HUD antigo: troca pelo campo de jogo desfocado/estendido
field = shot.crop((0, 120, 715, 640)).resize((1600, 900), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.2))
img = field.copy(); img.alpha_composite(shot.crop((0, 120, 715, 640)).resize((1600 * 715 // 715, 900)), (0, 0)) if False else None
img = field

def nine(tex, w, h, m):
    tw, th = tex.size; o = Image.new("RGBA", (w, h))
    for sx0, sx1, dx0, dx1 in ((0, m, 0, m), (m, tw - m, m, w - m), (tw - m, tw, w - m, w)):
        for sy0, sy1, dy0, dy1 in ((0, m, 0, m), (m, th - m, m, h - m), (th - m, th, h - m, h)):
            if dx1 > dx0 and dy1 > dy0: o.alpha_composite(tex.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS), (dx0, dy0))
    return o
def three(tex, w, h, cap):
    tw, th = tex.size; c = int(cap * h / th); o = Image.new("RGBA", (w, h))
    o.alpha_composite(tex.crop((0, 0, cap, th)).resize((c, h)), (0, 0)); o.alpha_composite(tex.crop((cap, 0, tw - cap, th)).resize((max(1, w - 2 * c), h)), (c, 0))
    o.alpha_composite(tex.crop((tw - cap, 0, tw, th)).resize((c, h)), (w - c, 0)); return o
def T(d, xy, s, size, col, anchor="la", font=F):
    d.text((xy[0] + 1, xy[1] + 2), s, font=font(size), fill=(0, 0, 0, 230), anchor=anchor); d.text(xy, s, font=font(size), fill=col, anchor=anchor)
def liquid(D, nivel, cor, t=1.0):
    yy, xx = np.mgrid[0:D, 0:D] / D * 2 - 1; r = np.sqrt(xx ** 2 + yy ** 2); rng = np.random.default_rng(4); n = np.zeros((D, D))
    for k in range(5):
        f = 2 ** k * 1.6; ph = rng.uniform(0, 6, 2); n += np.sin(xx * f * 3 + ph[0] + np.sin(yy * f * 2 + t)) * np.cos(yy * f * 3 + ph[1]) * 0.5 ** k
    n = (n - n.min()) / (n.max() - n.min()); cor = np.array(cor) / 255; funda = cor * 0.15
    c = (funda + (cor - funda) * (0.3 + 0.8 * n)[..., None]) * (1.3 - 0.85 * np.clip((r - 0.1) / 0.9, 0, 1))[..., None]
    topo = 1 - 2 * nivel + 0.02 * np.sin(xx * 6 + t); c += cor * np.clip(1 - np.abs(yy - topo) / 0.04, 0, 1)[..., None] * 0.8
    col = np.where((yy > topo)[..., None], c, np.ones((D, D, 3)) * 0.025)
    return Image.fromarray(np.dstack([np.clip(col, 0, 1) * 255, (r < 1) * 255.0]).astype("uint8"), "RGBA")
def orbe(size, nivel, cor):
    o = Image.new("RGBA", (size, size)); r = int(size * 196 / 640)
    o.alpha_composite(liquid(2 * r, nivel, cor), (size // 2 - r, size // 2 - r))
    o.alpha_composite(A("ui_orbe_vidro").resize((size, size), Image.LANCZOS)); o.alpha_composite(A("ui_orbe_moldura").resize((size, size), Image.LANCZOS)); return o

d = ImageDraw.Draw(img)
# placa da zona + icone
img.alpha_composite(three(A("ui_placa_zona"), 300, 46, 64), (18, 16)); img.alpha_composite(A("ico_zona_ancora").resize((34, 34), Image.LANCZOS), (26, 22))
d = ImageDraw.Draw(img); T(d, (70, 39), "COSTA DO CAL", 18, SILVER, "lm")
# bussola
img.alpha_composite(A("ui_bussola").resize((420, 33), Image.LANCZOS), (590, 18)); d = ImageDraw.Draw(img)
for x, s in ((597, "O"), (800, "N"), (1004, "L")): T(d, (x, 8), s, 15, SILVER, "mm")
# placa de alvo
img.alpha_composite(A("ui_barra_alvo").resize((200, 12), Image.LANCZOS), (210, 265)); d = ImageDraw.Draw(img)
ImageDraw.Draw(img).rectangle((215, 269, 360, 273), fill=(170, 30, 25, 255)); T(d, (310, 252), "Caranguejinho  Nv 2", 14, CREAM, "mm")
# barra do chefe (exemplo)
img.alpha_composite(three(A("ui_barra_chefe"), 620, 40, 120), (490, 70)); d = ImageDraw.Draw(img)
ImageDraw.Draw(img).rectangle((538, 87, 1062, 96), fill=(160, 22, 20, 255)); T(d, (800, 64), "REI DAS PINÇAS", 17, CREAM, "mm")
# barra de baixo: 1800x220 -> 1440x176 centrada
bar = A("ui_barra_inferior").resize((1440, 176), Image.LANCZOS); bx, by = 80, 900 - 176; img.alpha_composite(bar, (bx, by))
k = 1440 / 1800
for cx, nivel, cor in ((380, 0.72, (200, 18, 16)), (1420, 0.85, (25, 70, 220))):
    S = 260; img.alpha_composite(orbe(S, nivel, cor), (int(bx + cx * k - S / 2), int(by + 112 * k - S / 2) + 4))
d = ImageDraw.Draw(img)
T(d, (int(bx + 380 * k), by + 94), "341/473", 17, CREAM, "mm"); T(d, (int(bx + 1420 * k), by + 94), "100/100", 17, CREAM, "mm")
icons = [shot.crop((532, 782, 590, 838)), shot.crop((598, 782, 656, 838))]
slot, ativo = A("ui_slot_hab").resize((60, 60), Image.LANCZOS), A("ui_slot_hab_ativo").resize((60, 60), Image.LANCZOS)
keys = ["Mouse", "Q", "E", "R", "Espaço", "1"]
for i in range(6):
    x = int(bx + 560 * k) + 8 + i * 92; y = by + 82
    cell = Image.new("RGBA", (60, 60), (6, 6, 7, 240))
    if i < 2: cell.alpha_composite(icons[i].resize((52, 52)), (4, 4))
    if i == 3: cell.alpha_composite(shot.crop((842, 652, 898, 708)).resize((52, 52)), (4, 4))
    img.alpha_composite(cell, (x, y)); img.alpha_composite(ativo if i == 0 else slot, (x, y))
    T(ImageDraw.Draw(img), (x + 30, y + 72), keys[i], 13, SILVER, "mm", FR)
xp = A("ui_xp_moldura").resize((560, 22), Image.LANCZOS); img.alpha_composite(xp, (520, 896 - 26 - 140))
ImageDraw.Draw(img).rectangle((531, 737, 531 + 120, 741), fill=(140, 90, 210, 255)); d = ImageDraw.Draw(img)
T(d, (510, 740), "Nv. 2", 13, SILVER, "rm", FR); T(d, (1090, 740), "EXP 300 / 4.800", 13, SILVER, "lm", FR)
# inventario com aba e tooltip
px, py, pw, ph = 1060, 120, 520, 560
img.alpha_composite(nine(A("ui_moldura"), pw, ph, 26), (px, py)); img.alpha_composite(three(A("ui_aba"), 240, 40, 48), (px + 14, py - 22))
d = ImageDraw.Draw(img); T(d, (px + 40, py - 2), "Inventário", 18, CREAM, "lm")
cell = A("ui_slot_hab").resize((70, 70), Image.LANCZOS)
items = [shot.crop((904, 652, 960, 708)), shot.crop((906, 100, 986, 180)), shot.crop((842, 652, 898, 708))]
for r in range(4):
    for c in range(6):
        x = px + 30 + c * 76; y = py + 40 + r * 76
        img.alpha_composite(Image.new("RGBA", (70, 70), (8, 8, 9, 235)), (x, y))
        if r == 0 and c < 3: img.alpha_composite(items[c].resize((58, 58)), (x + 6, y + 6))
        img.alpha_composite(ativo.resize((70, 70)) if (r, c) == (0, 0) else cell, (x, y))
d = ImageDraw.Draw(img); T(d, (px + 30 + 2 * 76 + 62, py + 40 + 58), "3", 13, CREAM, "rm")
tx, ty = px + 30, py + 370
img.alpha_composite(nine(A("ui_tooltip"), 460, 160, 18), (tx, ty)); img.alpha_composite(A("ui_slot_hab").resize((90, 90), Image.LANCZOS), (tx + 20, ty + 34))
img.alpha_composite(items[0].resize((74, 74)), (tx + 28, ty + 42)); img.alpha_composite(A("ui_slot_hab").resize((90, 90), Image.LANCZOS), (tx + 20, ty + 34))
d = ImageDraw.Draw(img); T(d, (tx + 130, ty + 30), "Arco de Teixo", 20, CREAM); T(d, (tx + 130, ty + 60), "Excelente", 16, GOLD, font=FR)
T(d, (tx + 130, ty + 90), "Dano 18–26 · Alcance longo", 14, DIM, font=FR); T(d, (tx + 130, ty + 114), "+12% de chance de crítico", 14, (120, 170, 255), font=FR)
img.convert("RGB").save(out); print("ok")

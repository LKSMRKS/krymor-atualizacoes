#!/usr/bin/env python3
"""
Interface em ferro forjado, séria (pedido 09/11 22:48, com a prancha "Proposta visual": "uma HUD mais séria"),
feita por código (CC0). Sem ouro brilhante nem joias: ferro escuro gasto, arestas de aço polidas pelo uso, rebites,
um friso de bronze apagado. Saídas em Client/Assets/Art:
- ui_moldura.png       janela grande (9 fatias, margem 26 px): ferro com chanfro, cantoneiras rebitadas, miolo de pedra escura;
- ui_moldura_fina.png  painel pequeno/cartão (9 fatias, margem 10 px);
- ui_botao*.png        placa de ferro (normal, em cima, apertado) (9 fatias, margem 8 px);
- ui_placa_barra.png   placa comprida da barra de baixo (9 fatias na horizontal, margem 60 px);
- ui_berco.png         berço de ferro atrás de cada pote (aro com garras);
- ui_bussola.png       faixa da bússola no topo (9 fatias na horizontal, margem 40 px).
Usa as funções de relevo do gerar_ui.py. Rode:  python3 Tools/gerar_moldura.py
"""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gerar_ui import noise, shade, bevel_mask, OUT  # noqa: E402

IRON = (0.115, 0.11, 0.105)          # ferro escuro
STEEL = (0.42, 0.41, 0.39)           # aresta gasta
BRONZE = (0.36, 0.28, 0.18)          # friso apagado


def to_img(rgb, a):
    return Image.fromarray((np.clip(np.dstack([rgb, a]), 0, 1) * 255).astype(np.uint8), "RGBA")


def scratches(h, w, n, rng):
    """Riscos finos claros (desgaste do metal)."""
    im = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(im)
    for _ in range(n):
        x, y = rng.random() * w, rng.random() * h
        a = rng.random() * math.pi
        l = 4 + rng.random() * 18
        d.line([(x, y), (x + math.cos(a) * l, y + math.sin(a) * l)], fill=int(60 + rng.random() * 90), width=1)
    return np.asarray(im.filter(ImageFilter.GaussianBlur(0.4)), dtype=float) / 255


def iron(h, w, hgt, seed=3):
    rng = np.random.default_rng(seed)
    grime = noise(h, w, 20)[:h, :w]
    rgb = shade(hgt + grime * 0.06, IRON, spec=0.22, rough=12)
    rgb = rgb * (0.82 + 0.3 * grime)[..., None]                 # manchas de ferrugem/sujeira
    rust = np.clip((noise(h, w, 14)[:h, :w] - 0.62) * 3, 0, 1)
    rgb = rgb * (1 - rust[..., None] * 0.35) + rust[..., None] * np.array([0.16, 0.08, 0.04]) * 0.35
    rgb = rgb + scratches(h, w, int(h * w / 900), rng)[..., None] * 0.10
    return rgb


def interior(S, base=(0.055, 0.052, 0.05)):
    """Pedra/couro quase preto com veios e vinheta."""
    n = noise(S, S, 48) * 0.6 + noise(S, S, 6) * 0.4
    rgb = shade(n * 0.6, base, spec=0.03, rough=6) * 1.2
    y, x = np.mgrid[0:S, 0:S] / S
    vig = 1.0 - 0.4 * np.clip(((x - 0.5) ** 2 + (y - 0.5) ** 2) * 2.2, 0, 1)
    return rgb * vig[..., None]


def rivet(d, cx, cy, r):
    d.ellipse([cx - r - 1, cy - r - 1, cx + r + 1, cy + r + 1], fill=(8, 8, 8, 255))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(70, 68, 64, 255))
    d.ellipse([cx - r * 0.55, cy - r * 0.7, cx + r * 0.15, cy - r * 0.05], fill=(150, 146, 138, 255))


def frame(S, b, name, corners=True, inner_alpha=0.94):
    y, x = np.mgrid[0:S, 0:S].astype(float)
    dist = np.minimum(np.minimum(x, S - 1 - x), np.minimum(y, S - 1 - y))
    ring = (dist < b).astype(float)
    hgt = bevel_mask(ring, max(2, b // 3)) * 0.8
    rgb = iron(S, S, hgt)
    # aresta de aço gasta (fora e dentro) e um friso de bronze apagado no meio
    edge = np.exp(-((dist - 0.8) ** 2) / 0.8) + np.exp(-((dist - (b - 1.0)) ** 2) / 0.9)
    groove = np.exp(-((dist - b * 0.5) ** 2) / (2 * 0.6 ** 2))
    rgb = rgb * (1 - edge[..., None] * 0.7) + np.array(STEEL)[None, None, :] * edge[..., None] * 0.7
    rgb = rgb * (1 - groove[..., None] * 0.6) + np.array(BRONZE)[None, None, :] * groove[..., None] * 0.6
    inner = interior(S)
    sh = np.clip(1 - (dist - b) / (b * 0.7 + 2), 0, 1) * (dist >= b)
    inner = inner * (1 - 0.6 * sh[..., None])
    out = np.where(ring[..., None] > 0, rgb, inner)
    img = to_img(out, np.where(ring > 0, 1.0, inner_alpha))
    d = ImageDraw.Draw(img)
    if corners:
        # cantoneira de ferro dobrada com 3 rebites em cada canto
        for sx, sy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            ox = 0 if sx == 0 else S - 1
            oy = 0 if sy == 0 else S - 1
            dx = 1 if sx == 0 else -1
            dy = 1 if sy == 0 else -1
            L = b * 1.9
            pts = [(ox, oy), (ox + dx * L, oy), (ox + dx * L, oy + dy * b * 0.55), (ox + dx * b * 0.55, oy + dy * b * 0.55),
                   (ox + dx * b * 0.55, oy + dy * L), (ox, oy + dy * L)]
            d.polygon(pts, fill=(34, 33, 31, 255), outline=(96, 93, 87, 255))
            for (u, v) in ((b * 0.28, b * 0.28), (L - b * 0.35, b * 0.28), (b * 0.28, L - b * 0.35)):
                rivet(d, ox + dx * u, oy + dy * v, max(1.5, b * 0.11))
    else:
        for cx, cy in ((b * 0.5, b * 0.5), (S - 1 - b * 0.5, b * 0.5), (b * 0.5, S - 1 - b * 0.5), (S - 1 - b * 0.5, S - 1 - b * 0.5)):
            rivet(d, cx, cy, max(1.2, b * 0.16))
    img.save(os.path.join(OUT, name))


def button(name, edge, glow=0.0, pressed=False, W=128, H=44, b=6):
    y, x = np.mgrid[0:H, 0:W].astype(float)
    dist = np.minimum(np.minimum(x, W - 1 - x), np.minimum(y, H - 1 - y))
    rgb = iron(H, W, noise(H, W, 8)[:H, :W] * 0.05, seed=7)
    grad = (0.8 + 0.5 * (y / H)) if pressed else (1.35 - 0.6 * (y / H))
    rgb = rgb * grad[..., None]
    e = np.exp(-((dist - 1.2) ** 2) / 1.2)[..., None]
    rgb = rgb * (1 - e) + np.array(edge)[None, None, :] * e
    if glow > 0:
        gy = np.exp(-((y - H * 0.5) ** 2) / (2 * (H * 0.4) ** 2))[..., None]
        rgb = rgb + glow * gy * np.array([0.10, 0.08, 0.05])[None, None, :]
    to_img(rgb, np.ones((H, W))).save(os.path.join(OUT, name))


def bar_plate(W=1024, H=96):
    """Placa da barra de baixo: ferro grosso com chanfro, aresta gasta, friso e rebites; pontas cortadas em ângulo."""
    y, x = np.mgrid[0:H, 0:W].astype(float)
    cut = 34.0
    inside = (x > cut * (1 - y / H)) & (x < W - 1 - cut * (1 - y / H))   # trapézio: mais estreito em cima
    mask = inside.astype(float)
    hgt = bevel_mask(mask, 8) * 0.9
    rgb = iron(H, W, hgt, seed=11)
    top = np.exp(-((y - 3) ** 2) / 3.0)[..., None] * mask[..., None]
    rgb = rgb * (1 - top * 0.6) + np.array(STEEL)[None, None, :] * top * 0.6
    groove = np.exp(-((y - 12) ** 2) / 0.8)[..., None] * mask[..., None]
    rgb = rgb * (1 - groove * 0.5) + np.array(BRONZE)[None, None, :] * groove * 0.5
    img = to_img(rgb, mask)
    d = ImageDraw.Draw(img)
    for i in range(10):
        rivet(d, 60 + i * (W - 120) / 9, 22, 2.6)
    img.save(os.path.join(OUT, "ui_placa_barra.png"))


def cradle(S=256):
    """Berço do pote: aro grosso de ferro com quatro garras e rebites (fica atrás do frasco)."""
    y, x = np.mgrid[0:S, 0:S].astype(float)
    c = S / 2
    r = np.sqrt((x - c) ** 2 + (y - c) ** 2)
    a = np.arctan2(y - c, x - c)
    R0, R1 = S * 0.36, S * 0.47
    ring = ((r > R0) & (r < R1)).astype(float)
    claws = np.zeros_like(ring)
    for k in range(4):
        ang = math.pi / 4 + k * math.pi / 2
        da = np.abs(np.angle(np.exp(1j * (a - ang))))
        claws = np.maximum(claws, ((da < 0.16 * (1 - (r - R1) / (S * 0.04))) & (r >= R1 - 2) & (r < S * 0.5)).astype(float))
    mask = np.clip(ring + claws, 0, 1)
    hgt = bevel_mask(mask, 6) * 0.9
    rgb = iron(S, S, hgt, seed=21)
    rim = np.exp(-((r - R1 + 2) ** 2) / 2.0) + np.exp(-((r - R0 - 1.5) ** 2) / 1.5)
    rgb = rgb * (1 - rim[..., None] * 0.55) + np.array(STEEL)[None, None, :] * rim[..., None] * 0.55
    groove = np.exp(-((r - (R0 + R1) / 2) ** 2) / 1.0)
    rgb = rgb * (1 - groove[..., None] * 0.5) + np.array(BRONZE)[None, None, :] * groove[..., None] * 0.5
    img = to_img(rgb, mask)
    d = ImageDraw.Draw(img)
    for k in range(12):
        ang = k * math.pi / 6 + math.pi / 12
        rivet(d, c + math.cos(ang) * (R0 + R1) / 2, c + math.sin(ang) * (R0 + R1) / 2, 2.8)
    img.save(os.path.join(OUT, "ui_berco.png"))


def compass(W=512, H=40):
    y, x = np.mgrid[0:H, 0:W].astype(float)
    mask = np.ones((H, W))
    dist = np.minimum(np.minimum(x, W - 1 - x), np.minimum(y, H - 1 - y))
    hgt = bevel_mask(mask, 4) * 0.6
    rgb = iron(H, W, hgt, seed=31) * 0.8
    edge = np.exp(-((dist - 0.8) ** 2) / 0.8)
    rgb = rgb * (1 - edge[..., None] * 0.7) + np.array(STEEL)[None, None, :] * edge[..., None] * 0.7
    img = to_img(rgb, np.full((H, W), 0.92))
    d = ImageDraw.Draw(img)
    for cx in (10, W - 11):
        rivet(d, cx, H / 2, 2.6)
    img.save(os.path.join(OUT, "ui_bussola.png"))


def main():
    frame(256, 26, "ui_moldura.png")
    frame(96, 10, "ui_moldura_fina.png", corners=False, inner_alpha=0.9)
    button("ui_botao.png", (0.36, 0.35, 0.33))
    button("ui_botao_hover.png", (0.62, 0.58, 0.50), glow=0.5)
    button("ui_botao_press.png", (0.55, 0.52, 0.46), pressed=True)
    bar_plate(); cradle(); compass()
    print("interface de ferro gerada em", os.path.abspath(OUT))


if __name__ == "__main__":
    main()

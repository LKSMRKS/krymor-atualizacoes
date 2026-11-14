#!/usr/bin/env python3
"""
Texturas de TERRENO detalhadas (1024 px, repetíveis) no nível do chão da vila (chao_pedra.jpg):
  ter_areia  — areia fina com grãos, marcas de vento, conchinhas e pedrisco
  ter_grama  — chão de capim: milhares de folhinhas em vários verdes/palha, terra aparecendo
  ter_terra  — terra batida com pedrisco, rachaduras e torrões
  ter_pedra  — chão pedregoso: pedras meio enterradas na terra, com sombra de contato
Cada uma gera também *_n.png (mapa de normais a partir da altura) — o MapView usa como relevo.
Folhagem com transparência para o capim 3D (cartões): fol_capim.png, fol_capim_seco.png, fol_moita.png
Tudo original deste script (CC0). Rode: python3 Tools/gerar_terreno.py
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

N = 1024
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Client", "Assets", "Art")
rng = np.random.default_rng(11)


def noise(cells, octaves=5, persist=0.5):
    """Ruído de valor repetível (bordas casam), 0..1."""
    out = np.zeros((N, N)); amp = 1.0; total = 0.0
    for o in range(octaves):
        c = cells * (2 ** o)
        g = rng.random((c, c))
        xs = np.linspace(0, c, N, endpoint=False)
        x0 = xs.astype(int); f = xs - x0; x1 = (x0 + 1) % c
        f = f * f * (3 - 2 * f)
        a = g[x0][:, x0]; b = g[x0][:, x1]; cc = g[x1][:, x0]; d = g[x1][:, x1]
        top = a * (1 - f)[None, :] + b * f[None, :]
        bot = cc * (1 - f)[None, :] + d * f[None, :]
        out += amp * (top * (1 - f)[:, None] + bot * f[:, None])
        total += amp; amp *= persist
    return out / total


def blur_wrap(a, r):
    """Desfoque repetível (pad circular)."""
    # gaussiano separável com rolagem circular (repetível)
    k = int(r * 3) + 1
    w = np.exp(-0.5 * (np.arange(-k, k + 1) / max(r, 1e-3)) ** 2); w /= w.sum()
    out = sum(wi * np.roll(a, i - k, 0) for i, wi in enumerate(w))
    return sum(wi * np.roll(out, i - k, 1) for i, wi in enumerate(w))


def normals(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.stack([-dx, -dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n


def lit(albedo, h, strength, ao=0.5):
    """Sombreamento suave embutido (luz difusa de cima-esquerda) + oclusão nas partes baixas."""
    n = normals(h, strength)
    l = np.array([-0.45, -0.5, 0.74]); l /= np.linalg.norm(l)
    d = np.clip((n * l).sum(-1), 0, 1)
    occ = 1 - ao * (1 - (h - h.min()) / (np.ptp(h) + 1e-6))
    return np.clip(albedo * (0.55 + 0.6 * d)[..., None] * occ[..., None], 0, 1)


def save(name, rgb, h, strength):
    Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"))
    n = normals(h, strength)
    Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(OUT, name + "_n.png"))


def stamp_wrap(canvas, img, x, y, mode="max"):
    """Cola img (h,w) em canvas com repetição nas bordas."""
    h, w = img.shape
    for ox in (-N, 0, N):
        for oy in (-N, 0, N):
            x0, y0 = x + ox, y + oy
            if x0 + w <= 0 or y0 + h <= 0 or x0 >= N or y0 >= N: continue
            sx0, sy0 = max(0, -x0), max(0, -y0)
            ex, ey = min(w, N - x0), min(h, N - y0)
            region = canvas[y0 + sy0:y0 + ey, x0 + sx0:x0 + ex]
            part = img[sy0:ey, sx0:ex]
            if mode == "max": np.maximum(region, part, out=region)
            else: region += part


def pebbles(count, rmin, rmax):
    """Campo de altura com pedrinhas arredondadas e irregulares; devolve (altura, máscara, tom por pedra)."""
    hgt = np.zeros((N, N)); mask = np.zeros((N, N)); tone = np.zeros((N, N))
    for _ in range(count):
        r = rng.uniform(rmin, rmax)
        s = int(r * 2.6) + 2
        yy, xx = np.mgrid[0:s, 0:s] - s / 2
        ang = rng.uniform(0, np.pi)
        ex = rng.uniform(0.6, 1.0)
        xr = xx * np.cos(ang) + yy * np.sin(ang); yr = (-xx * np.sin(ang) + yy * np.cos(ang)) / ex
        th = np.arctan2(yr, xr)
        jag = 1 + 0.10 * np.sin(2 * th + rng.uniform(0, 6)) + 0.06 * np.sin(3 * th + rng.uniform(0, 6)) + 0.03 * np.sin(5 * th + rng.uniform(0, 6))
        d = np.sqrt(xr ** 2 + yr ** 2) / (r * jag)
        bump = np.clip(1 - d ** 2, 0, 1) ** 0.6 * r
        m = (d < 1).astype(float)
        x, y = rng.integers(0, N, 2)
        stamp_wrap(hgt, bump, x, y)
        stamp_wrap(mask, m, x, y)
        t = np.full_like(bump, rng.uniform(-1, 1)) * m
        stamp_wrap(tone, t, x, y, mode="max") if t.max() > 0 else None
    return hgt, mask, tone


def blades(img_draw, count, colors, length, width, wrap=True, angle_spread=0.9, base=None):
    """Folhinhas de capim vistas de cima/inclinadas (traços curvos afinando)."""
    for _ in range(count):
        x, y = rng.uniform(0, N, 2) if base is None else base()
        L = rng.uniform(*length); ang = rng.uniform(-np.pi, np.pi)
        bend = rng.uniform(-0.6, 0.6)
        col = colors[rng.integers(len(colors))]
        jitter = rng.uniform(-18, 18, 3).astype(int)
        c = tuple(int(np.clip(col[i] + jitter[i], 0, 255)) for i in range(3))
        pts = []
        for k in range(6):
            t = k / 5
            a = ang + bend * t
            pts.append((x + np.cos(a) * L * t, y + np.sin(a) * L * t))
        for ox in ((-N, 0, N) if wrap else (0,)):
            for oy in ((-N, 0, N) if wrap else (0,)):
                p = [(px + ox, py + oy) for px, py in pts]
                for k in range(5):
                    w = max(1, int(width * (1 - k / 5)))
                    img_draw.line([p[k], p[k + 1]], fill=c, width=w)


# ---------------------------------------------------------------- areia
def areia():
    base = noise(4, 6)
    ripples = np.sin((np.mgrid[0:N, 0:N][0] / N * 2 * np.pi * 22) + noise(3, 3) * 9) * 0.5 + 0.5
    grains = rng.random((N, N))
    ph, pm, pt = pebbles(260, 2.5, 7)
    h = base * 1.2 + ripples * 0.35 + grains * 0.25 + ph * 0.12
    col = np.stack([0.80, 0.70, 0.52])[None, None, :] * (0.86 + 0.18 * base)[..., None]
    col = col * (0.92 + 0.12 * grains)[..., None]
    col = col * (0.95 + 0.06 * ripples)[..., None]
    peb = np.stack([0.55 + 0.1 * pt, 0.50 + 0.08 * pt, 0.44 + 0.06 * pt], -1)
    col = col * (1 - pm[..., None]) + peb * pm[..., None]
    # manchas úmidas mais escuras e conchinhas claras
    wet = np.clip((noise(3, 4) - 0.55) * 4, 0, 1)
    col *= (1 - 0.18 * wet)[..., None]
    save("ter_areia", lit(col, blur_wrap(h, 0.7), 2.2, 0.25), blur_wrap(h, 0.8), 2.0)


# ---------------------------------------------------------------- terra
def terra():
    base = noise(4, 6)
    grains = rng.random((N, N))
    ph, pm, pt = pebbles(1400, 1.5, 6.5)
    cracks = (np.abs(noise(6, 3) - 0.5) < 0.006) & (noise(3, 2) > 0.55)
    cr = blur_wrap(cracks.astype(float), 0.9) * 0.6
    h = base * 1.5 + grains * 0.3 + ph * 0.18 - cr * 1.2
    dirt = np.stack([0.42, 0.32, 0.22])[None, None, :] * (0.8 + 0.35 * base)[..., None] * (0.9 + 0.15 * grains)[..., None]
    peb = np.stack([0.40 + 0.08 * pt, 0.35 + 0.07 * pt, 0.29 + 0.06 * pt], -1) * (0.85 + 0.3 * grains)[..., None]
    col = dirt * (1 - 0.7 * pm[..., None]) + peb * 0.7 * pm[..., None]
    col *= (1 - 0.35 * cr)[..., None]
    save("ter_terra", lit(col, blur_wrap(h, 0.6), 2.6, 0.35), blur_wrap(h, 0.7), 2.4)


# ---------------------------------------------------------------- pedra (chão pedregoso)
def pedra():
    base = noise(4, 6)
    grains = rng.random((N, N))
    big_h, big_m, big_t = pebbles(110, 16, 46)
    small_h, small_m, small_t = pebbles(900, 2, 7)
    h = base * 1.0 + big_h * 0.06 + small_h * 0.12 + grains * 0.2
    dirt = np.stack([0.36, 0.31, 0.25])[None, None, :] * (0.8 + 0.3 * base)[..., None] * (0.9 + 0.15 * grains)[..., None]
    stone_var = noise(16, 3)
    speck = noise(64, 2)
    stone = np.stack([0.40 + 0.07 * big_t, 0.38 + 0.06 * big_t, 0.35 + 0.05 * big_t], -1) * (0.75 + 0.35 * stone_var)[..., None] * (0.85 + 0.3 * speck)[..., None]
    col = dirt * (1 - big_m[..., None]) + stone * big_m[..., None]
    sp = np.stack([0.42 + 0.08 * small_t, 0.39 + 0.07 * small_t, 0.35 + 0.06 * small_t], -1)
    col = col * (1 - small_m[..., None] * (1 - big_m[..., None])) + sp * (small_m * (1 - big_m))[..., None]
    # sombra de contato ao redor das pedras grandes
    edge = np.clip(blur_wrap(big_m, 4) - big_m, 0, 1)
    col *= (1 - 0.45 * edge)[..., None]
    save("ter_pedra", lit(col, blur_wrap(h, 0.6), 3.0, 0.4), blur_wrap(h, 0.7), 2.8)


# ---------------------------------------------------------------- grama
def grama():
    soil = noise(4, 5)
    img = Image.new("RGB", (N, N))
    ground = (np.stack([0.30, 0.25, 0.16])[None, None, :] * (0.8 + 0.4 * soil)[..., None] * 255).astype(np.uint8)
    img = Image.fromarray(ground)
    d = ImageDraw.Draw(img)
    density = noise(5, 4)
    greens = [(70, 96, 42), (88, 112, 48), (58, 82, 36), (104, 120, 56), (120, 116, 62), (76, 104, 40), (140, 128, 70)]
    def where():
        while True:
            x, y = rng.uniform(0, N, 2)
            if rng.random() < 0.35 + 0.65 * density[int(y) % N, int(x) % N]: return x, y
    blades(d, 26000, greens, (8, 22), 3, base=where)
    blades(d, 9000, [(150, 140, 80), (160, 150, 92), (98, 120, 52)], (6, 14), 2, base=where)
    rgb = np.asarray(img).astype(float) / 255
    h = blur_wrap(rgb.mean(-1), 0.8)
    rgb = rgb * (0.85 + 0.25 * noise(3, 4))[..., None]
    save("ter_grama", lit(rgb, h, 3.0, 0.3), h, 3.0)


# ---------------------------------------------------------------- folhagem (cartões com transparência)
def folhagem(name, colors, count, length, width, size=256):
    S = size * 2
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(count):
        x = rng.uniform(S * 0.12, S * 0.88)
        L = rng.uniform(*length) * S
        lean = rng.uniform(-0.35, 0.35)
        col = colors[rng.integers(len(colors))]
        pts = []
        for k in range(8):
            t = k / 7
            pts.append((x + lean * L * t * t, S - 2 - L * t))
        for k in range(7):
            w = max(1, int(width * S / 256 * (1 - k / 7)))
            shade = 0.65 + 0.35 * (k / 7)
            c = tuple(int(col[i] * shade) for i in range(3)) + (255,)
            d.line([pts[k], pts[k + 1]], fill=c, width=w)
    img = img.resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name + ".png"))


def moita(name, size=256):
    S = size * 2
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(900):
        # folhas em elipse, mais densas no meio e embaixo
        a = rng.uniform(0, np.pi * 2); r = rng.uniform(0, 1) ** 0.6
        x = S / 2 + np.cos(a) * r * S * 0.42; y = S * 0.58 + np.sin(a) * r * S * 0.36
        lw, lh = rng.uniform(10, 22), rng.uniform(5, 10)
        g = rng.uniform(0.6, 1.15) * (1.15 - 0.4 * (y / S))
        col = (int(60 * g), int(92 * g), int(38 * g), 255)
        d.ellipse((x - lw, y - lh, x + lw, y + lh), fill=col)
    img = img.filter(ImageFilter.GaussianBlur(0.8)).resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name + ".png"))


if __name__ == "__main__":
    areia(); print("areia")
    terra(); print("terra")
    pedra(); print("pedra")
    grama(); print("grama")
    folhagem("fol_capim", [(74, 102, 44), (92, 118, 50), (62, 88, 38), (110, 126, 58)], 140, (0.45, 0.95), 7)
    folhagem("fol_capim_seco", [(160, 146, 92), (176, 160, 104), (138, 122, 76), (120, 116, 70)], 120, (0.4, 0.85), 6)
    moita("fol_moita")
    print("ok")

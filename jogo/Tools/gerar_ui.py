#!/usr/bin/env python3
"""
Kit de interface "AAA" feito por código (CC0, original): moldura de metal trabalhado com relevo (mapa de altura → luz),
barra de baixo, anel dos frascos, fundo de painel e ícones pintados das habilidades.
Saída em Client/Assets/Art:  ui_slot.png, ui_barra.png, ui_anel.png, ui_painel.png, hab_<id>.png
Rode:  python3 Tools/gerar_ui.py
"""
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Client", "Assets", "Art")
rng = np.random.default_rng(7)


def noise(h, w, scale):
    """Ruído suave (soma de oitavas de ruído de valor) 0..1."""
    acc = np.zeros((h, w)); amp = 1.0; tot = 0
    for o in range(5):
        s = max(1, int(scale / (2 ** o)))
        g = rng.random((h // s + 2, w // s + 2))
        img = Image.fromarray((g * 255).astype(np.uint8)).resize((w + 2 * s, h + 2 * s), Image.BICUBIC)
        acc += np.asarray(img, dtype=float)[:h, :w] / 255 * amp
        tot += amp; amp *= 0.5
    return acc / tot


def shade(height, base_rgb, light=(-0.6, -0.7, 0.9), spec=0.55, rough=18):
    """Mapa de altura → cor com luz (relevo de metal): difusa + brilho especular."""
    gy, gx = np.gradient(height)
    n = np.dstack([-gx * 3, -gy * 3, np.ones_like(height)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    l = np.array(light, dtype=float); l /= np.linalg.norm(l)
    diff = np.clip((n * l).sum(2), 0, 1)
    h = l + np.array([0, 0, 1.0]); h /= np.linalg.norm(h)
    sp = np.clip((n * h).sum(2), 0, 1) ** rough
    rgb = np.array(base_rgb, dtype=float)[None, None, :] * (0.25 + 0.85 * diff[..., None]) + spec * sp[..., None]
    return np.clip(rgb, 0, 1)


def bevel_mask(mask, width):
    """Altura em rampa a partir da borda de uma máscara (relevo chanfrado)."""
    m = Image.fromarray((mask * 255).astype(np.uint8))
    acc = np.zeros(mask.shape)
    for i in range(1, width + 1):
        er = np.asarray(m.filter(ImageFilter.MinFilter(2 * i + 1)), dtype=float) / 255
        acc += er
    return acc / width


def save(name, rgba):
    Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name))


GOLD = (0.60, 0.43, 0.20)
IRON = (0.17, 0.16, 0.16)


def frame_slot(size=128):
    """Moldura do quadrado de habilidade: bronze trabalhado com chanfro duplo, rebites nos cantos, miolo vazado."""
    S = size * 2
    y, x = np.mgrid[0:S, 0:S] / S
    outer = np.ones((S, S))
    inner = ((x > 0.085) & (x < 0.915) & (y > 0.085) & (y < 0.915)).astype(float)
    ring = outer - inner
    hgt = bevel_mask(ring, 7) * 0.8 + noise(S, S, 24) * 0.12
    # friso: linha gravada no meio da moldura
    groove = (np.abs(np.maximum(np.abs(x - 0.5), np.abs(y - 0.5)) - 0.457) < 0.006).astype(float)
    hgt -= groove * 0.25
    rgb = shade(hgt, GOLD, spec=0.6)
    # rebites
    img = np.dstack([rgb, ring])
    pil = Image.fromarray((img * 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(pil)
    for cx, cy in ((0.045, 0.045), (0.955, 0.045), (0.045, 0.955), (0.955, 0.955)):
        r = 0.03 * S
        d.ellipse([cx * S - r, cy * S - r, cx * S + r, cy * S + r], fill=(60, 40, 22, 255))
        d.ellipse([cx * S - r * 0.6, cy * S - r * 0.8, cx * S + r * 0.3, cy * S - r * 0.1], fill=(240, 210, 150, 255))
    # sombra interna (o ícone parece afundado)
    sh = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ds = ImageDraw.Draw(sh)
    ds.rectangle([0.085 * S, 0.085 * S, 0.915 * S, 0.915 * S], outline=(0, 0, 0, 220), width=int(0.04 * S))
    sh = sh.filter(ImageFilter.GaussianBlur(S * 0.015))
    pil = Image.alpha_composite(sh, pil)
    pil.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, "ui_slot.png"))


def bar(w=1400, h=150):
    """Barra de baixo: placa de ferro escuro com friso dourado em cima, gravuras e pontas em forma de garra."""
    S = 1
    y, x = np.mgrid[0:h, 0:w].astype(float)
    xn = x / w; yn = y / h
    # silhueta: placa central com topo levemente arqueado e pontas que sobem
    top = 0.18 + 0.05 * (1 - np.cos(np.pi * (xn * 2 - 1))) ** 0.5 * 0
    mask = (yn > 0.22) & (xn > 0.015) & (xn < 0.985)
    ends = ((xn < 0.03) | (xn > 0.97)) & (yn > 0.10)
    mask = (mask | ends).astype(float)
    hgt = bevel_mask(mask, 8) * 0.6 + noise(h, w, 30) * 0.25 + noise(h, w, 4) * 0.08
    # placas verticais rebitadas (divisões da armadura) a cada ~7% da largura
    ribs = (np.abs(((xn * 14) % 1) - 0.5) > 0.485) & (yn > 0.3) & (yn < 0.9)
    hgt -= ribs * 0.25
    rivets = (np.hypot((((xn * 14) % 1) - 0.5) * w / 14, (yn - 0.36) * h) < 3.2) | (np.hypot((((xn * 14) % 1) - 0.5) * w / 14, (yn - 0.84) * h) < 3.2)
    hgt += rivets * 0.4
    # frisos horizontais (dourado) em cima e embaixo
    band_top = (np.abs(yn - 0.27) < 0.025) & (mask > 0)
    band_bot = (np.abs(yn - 0.93) < 0.018) & (mask > 0)
    hgt += band_top * 0.35 + band_bot * 0.25
    # gravuras: losangos repetidos na faixa de cima
    diam = (np.abs(((xn * 60) % 1) - 0.5) + np.abs(yn - 0.27) * 30 < 0.35) & band_top
    hgt -= diam * 0.2
    iron = shade(hgt, IRON, spec=0.35, rough=12)
    gold = shade(hgt, GOLD, spec=0.6)
    sel = (band_top | band_bot | ends | rivets)[..., None]
    gold_end = shade(hgt, (0.42, 0.30, 0.15), spec=0.45)
    rgb = np.where(ends[..., None], gold_end, np.where(sel, gold, iron))
    # escurece o centro (onde ficam os atalhos) para dar contraste
    rgb *= (0.75 + 0.25 * np.abs(xn - 0.5)[..., None] * 2)
    save("ui_barra.png", np.dstack([rgb, mask]))


def ring(size=320):
    """Anel do frasco: aro de ferro com friso dourado e 8 cravos, centro vazado."""
    S = size
    y, x = np.mgrid[0:S, 0:S].astype(float)
    r = np.hypot(x - S / 2, y - S / 2) / (S / 2)
    mask = ((r > 0.80) & (r < 1.0)).astype(float)
    hgt = bevel_mask(mask, 9) * 0.7 + noise(S, S, 20) * 0.12
    goldband = (np.abs(r - 0.835) < 0.018)
    hgt += goldband * 0.3
    ang = np.arctan2(y - S / 2, x - S / 2)
    studs = (np.abs(((ang / (2 * np.pi) * 8) % 1) - 0.5) < 0.06) & (np.abs(r - 0.92) < 0.04)
    hgt += studs * 0.5
    rgb = np.where((goldband | studs)[..., None], shade(hgt, GOLD), shade(hgt, IRON, spec=0.35, rough=12))
    save("ui_anel.png", np.dstack([rgb, mask]))


def panel(size=512):
    """Fundo de painel: couro/pedra escura com veios, ladrilhável."""
    n1 = noise(size, size, 64); n2 = noise(size, size, 8)
    hgt = n1 * 0.6 + n2 * 0.4
    rgb = shade(hgt, (0.16, 0.13, 0.11), spec=0.08, rough=6)
    save("ui_painel.png", np.dstack([rgb, np.ones((size, size))]))


# ------------------------------------------------------------------ ícones de habilidade
CLASS_COL = {"guerreiro": (0.85, 0.32, 0.15), "campeao": (0.9, 0.45, 0.15), "mago": (0.35, 0.45, 1.0), "arcanista": (0.55, 0.4, 1.0),
             "bruxa": (0.25, 0.85, 0.45), "matriarca": (0.35, 0.95, 0.6), "arqueiro": (0.95, 0.75, 0.25), "batedor": (0.85, 0.85, 0.35),
             "arma": (0.75, 0.75, 0.8), "lend": (1.0, 0.75, 0.2), "pet": (1.0, 0.5, 0.2), "geral": (0.7, 0.7, 0.75)}


def motif(d, kind, S):
    """Desenha a forma (branca) no canvas S×S."""
    c = S / 2
    W = (255, 255, 255, 255)
    def poly(pts): d.polygon([(c + px * S, c + py * S) for px, py in pts], fill=W)
    def line(pts, w): d.line([(c + px * S, c + py * S) for px, py in pts], fill=W, width=int(w * S), joint="curve")
    def circ(px, py, r, width=None):
        box = [c + (px - r) * S, c + (py - r) * S, c + (px + r) * S, c + (py + r) * S]
        if width: d.ellipse(box, outline=W, width=int(width * S))
        else: d.ellipse(box, fill=W)
    if kind == "espada":
        poly([(-0.30, 0.30), (0.22, -0.22), (0.30, -0.30), (0.25, -0.18), (-0.26, 0.34)])
        line([(-0.33, 0.18), (-0.18, 0.33)], 0.05); line([(-0.36, 0.36), (-0.27, 0.27)], 0.06)
    elif kind == "golpe":
        poly([(-0.30, 0.30), (0.22, -0.22), (0.30, -0.30), (0.25, -0.18), (-0.26, 0.34)])
        for i in range(3): line([(0.0 + i * 0.08, -0.36 + i * 0.05), (0.30, -0.06 + i * 0.05)], 0.025)
    elif kind == "soco":
        circ(0, 0, 0.2); poly([(-0.2, 0.05), (0.2, 0.05), (0.18, 0.32), (-0.18, 0.32)])
        for i in range(3): line([(-0.36, -0.2 + i * 0.12), (-0.24, -0.2 + i * 0.12)], 0.03)
    elif kind == "escudo":
        poly([(-0.28, -0.32), (0.28, -0.32), (0.28, 0.0), (0.0, 0.36), (-0.28, 0.0)])
    elif kind == "giro":
        for i in range(3):
            a0 = i * 120
            d.arc([c - 0.33 * S, c - 0.33 * S, c + 0.33 * S, c + 0.33 * S], a0, a0 + 80, fill=W, width=int(0.06 * S))
        circ(0, 0, 0.08)
    elif kind == "salto":
        poly([(-0.12, -0.36), (0.12, -0.36), (0.12, -0.05), (0.24, -0.05), (0, 0.18), (-0.24, -0.05), (-0.12, -0.05)])
        for a in (-0.35, -0.15, 0.15, 0.35): line([(0, 0.24), (a, 0.36)], 0.025)
    elif kind == "furia":
        pts = []
        for i in range(16):
            r = 0.36 if i % 2 == 0 else 0.16
            a = i * math.pi / 8
            pts.append((math.cos(a) * r, math.sin(a) * r))
        poly(pts)
    elif kind == "investida":
        poly([(-0.05, -0.15), (0.32, 0), (-0.05, 0.15)])
        for i in range(3): line([(-0.36, -0.12 + i * 0.12), (-0.1, -0.12 + i * 0.12)], 0.035)
    elif kind in ("fogo", "meteoro", "cometa"):
        if kind == "fogo":
            poly([(0, -0.36), (0.12, -0.12), (0.26, -0.2), (0.24, 0.1), (0.14, 0.32), (-0.14, 0.32), (-0.24, 0.1), (-0.2, -0.16), (-0.08, -0.06)])
        else:
            circ(0.14, 0.14, 0.17)
            for i in range(3): line([(0.04 - i * 0.05, 0.02 + i * 0.06), (-0.34 + i * 0.02, -0.32 + i * 0.1)], 0.05 - i * 0.012)
            if kind == "meteoro": circ(-0.22, 0.24, 0.07); circ(0.3, -0.24, 0.05)
    elif kind == "gelo":
        poly([(0, -0.38), (0.1, -0.05), (0.04, 0.36), (-0.04, 0.36), (-0.1, -0.05)])
        poly([(-0.3, -0.1), (-0.08, -0.02), (-0.08, 0.04), (-0.3, 0.0)]); poly([(0.3, -0.1), (0.08, -0.02), (0.08, 0.04), (0.3, 0.0)])
    elif kind in ("passo", "sombra"):
        for i, (px, py) in enumerate(((-0.14, 0.2), (0.12, -0.02), (-0.1, -0.24))):
            d.ellipse([c + (px - 0.08) * S, c + (py - 0.12) * S, c + (px + 0.08) * S, c + (py + 0.12) * S], fill=(255, 255, 255, 255 - i * 60))
        if kind == "sombra": d.arc([c - 0.36 * S, c - 0.36 * S, c + 0.36 * S, c + 0.36 * S], 200, 340, fill=W, width=int(0.04 * S))
    elif kind == "cura":
        poly([(-0.08, -0.32), (0.08, -0.32), (0.08, -0.08), (0.32, -0.08), (0.32, 0.08), (0.08, 0.08), (0.08, 0.32), (-0.08, 0.32), (-0.08, 0.08), (-0.32, 0.08), (-0.32, -0.08), (-0.08, -0.08)])
    elif kind == "barreira":
        circ(0, 0, 0.32, width=0.05)
        pts = [(math.cos(a) * 0.2, math.sin(a) * 0.2) for a in np.linspace(0, 2 * math.pi, 7)[:-1]]
        line(pts + [pts[0]], 0.035)
    elif kind == "aura":
        for r in (0.12, 0.22, 0.33): circ(0, 0, r, width=0.035)
    elif kind == "espinhos":
        for px in (-0.24, -0.08, 0.08, 0.24):
            hgt = 0.5 if abs(px) < 0.1 else 0.36
            poly([(px - 0.07, 0.32), (px, 0.32 - hgt), (px + 0.07, 0.32)])
    elif kind == "floresta":
        poly([(0, -0.36), (0.26, 0.1), (0.08, 0.1), (0.08, 0.34), (-0.08, 0.34), (-0.08, 0.1), (-0.26, 0.1)])
    elif kind in ("flecha", "rajada", "chuva", "perfurante"):
        n = 3 if kind in ("rajada", "chuva") else 1
        for i in range(n):
            off = (i - (n - 1) / 2) * 0.16
            if kind == "chuva":
                line([(off, -0.36), (off, 0.2)], 0.035); poly([(off - 0.07, 0.16), (off + 0.07, 0.16), (off, 0.34)])
            else:
                line([(-0.32 + off * 0.3, 0.32 + off), (0.22 + off * 0.3, -0.22 + off)], 0.035)
                poly([(0.32 + off * 0.3, -0.32 + off), (0.12 + off * 0.3, -0.22 + off), (0.22 + off * 0.3, -0.12 + off)])
        if kind == "perfurante": circ(0, 0, 0.3, width=0.03)
    elif kind == "olho":
        d.ellipse([c - 0.34 * S, c - 0.18 * S, c + 0.34 * S, c + 0.18 * S], outline=W, width=int(0.05 * S)); circ(0, 0, 0.1)
    elif kind == "fenix":
        poly([(0, -0.1), (0.36, -0.3), (0.2, 0.0), (0.34, 0.06), (0.06, 0.12), (0, 0.36), (-0.06, 0.12), (-0.34, 0.06), (-0.2, 0.0), (-0.36, -0.3)])
    elif kind == "serpente":
        pts = [(-0.36 + i * 0.04, math.sin(i * 0.55) * 0.18) for i in range(19)]
        line(pts, 0.07); circ(0.36, math.sin(18 * 0.55) * 0.18, 0.07)
    elif kind == "sino":
        poly([(-0.06, -0.34), (0.06, -0.34), (0.22, 0.12), (0.28, 0.24), (-0.28, 0.24), (-0.22, 0.12)]); circ(0, 0.3, 0.06)
    elif kind == "esquiva":
        d.arc([c - 0.32 * S, c - 0.32 * S, c + 0.32 * S, c + 0.32 * S], 120, 380, fill=W, width=int(0.06 * S))
        poly([(0.18, -0.36), (0.36, -0.2), (0.14, -0.14)])
    elif kind == "comida":
        # 13/11 ("no meu ícone 2 tem tipo uma frigideira branca com um 0"): coxa assada (carne + osso), não frigideira
        d.ellipse([c - 0.30 * S, c - 0.30 * S, c + 0.12 * S, c + 0.10 * S], fill=W)
        line([(0.02, 0.0), (0.24, 0.22)], 0.07)
        circ(0.27, 0.19, 0.06); circ(0.21, 0.27, 0.06)
    elif kind == "mana":
        poly([(-0.06, -0.34), (0.06, -0.34), (0.06, -0.14), (0.24, 0.06), (0.2, 0.3), (-0.2, 0.3), (-0.24, 0.06), (-0.06, -0.14)])
    else:
        circ(0, 0, 0.25)


def icon(name, kind, col, size=128):
    S = size * 2
    y, x = np.mgrid[0:S, 0:S].astype(float)
    r = np.hypot(x - S / 2, y - S / 2) / (S / 2)
    col = np.array(col)
    # fundo: brilho da cor da classe no centro, quase preto nas bordas, com textura de pergaminho/pedra
    bg = col[None, None, :] * np.clip(0.55 - 0.5 * r, 0.05, 1)[..., None] + 0.03
    bg *= (0.8 + 0.4 * noise(S, S, 20))[..., None]
    base = Image.fromarray((np.clip(bg, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")
    shape = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    motif(ImageDraw.Draw(shape), kind, S)
    alpha = shape.split()[3]
    glow_col = tuple(int(255 * min(1, v * 1.2 + 0.1)) for v in col)
    for blur, strength in ((S * 0.06, 0.9), (S * 0.025, 0.9)):
        g = Image.new("RGBA", (S, S), glow_col + (0,))
        g.putalpha(alpha.filter(ImageFilter.GaussianBlur(blur)).point(lambda v: int(min(255, v * strength * 1.6))))
        base = Image.alpha_composite(base, g)
    # forma: degradê branco→cor (metal iluminado), contorno escuro fino
    grad = np.dstack([np.linspace(1, 0.55, S)[:, None] * np.ones((1, S)) * (0.6 + 0.4 * c) + 0.4 * (1 - c) for c in col])
    fill = Image.fromarray((np.clip(grad, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")
    fill.putalpha(alpha)
    edge = Image.new("RGBA", (S, S), (20, 12, 8, 0)); edge.putalpha(alpha.filter(ImageFilter.MaxFilter(5)))
    base = Image.alpha_composite(base, edge)
    base = Image.alpha_composite(base, fill)
    # vinheta
    vig = Image.fromarray((np.clip((r - 0.55) * 1.4, 0, 0.85) * 255).astype(np.uint8))
    dark = Image.new("RGBA", (S, S), (0, 0, 0, 0)); dark.putalpha(vig)
    base = Image.alpha_composite(base, dark)
    base.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, f"hab_{name}.png"))


KIND = [  # (palavra no id, motivo)
    ("soco", "soco"), ("arremesso", "soco"), ("corte_avancante", "investida"), ("corte", "espada"), ("golpe_de_cunha", "golpe"),
    ("guarda", "escudo"), ("giro", "giro"), ("salto", "salto"), ("colosso", "furia"), ("investida", "investida"),
    ("explosao", "fogo"), ("seta_de_brasa", "fogo"), ("brasa", "fogo"), ("faisca", "fogo"), ("centelha", "fogo"), ("lampejo", "fogo"),
    ("meteoros", "meteoro"), ("cometa", "cometa"), ("lanca_gelo", "gelo"), ("lanca", "gelo"), ("estilhaco", "gelo"),
    ("passo_etereo", "passo"), ("passos_fantasma", "sombra"), ("toque", "cura"), ("renovo", "cura"), ("barreira", "barreira"),
    ("aura", "aura"), ("espinhos", "espinhos"), ("floresta", "floresta"), ("chuva", "chuva"), ("rajada", "rajada"),
    ("perfurante", "perfurante"), ("emboscada", "flecha"), ("disparo", "flecha"), ("arco", "flecha"), ("olhar", "olho"),
    ("fenix", "fenix"), ("serpente", "serpente"), ("onda_sino", "sino"), ("vozes", "sino"), ("onda", "aura"), ("dragao", "fenix"),
]


def skill_ids():
    import json
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Content")
    ids = []
    for f in os.listdir(root):
        if f.startswith("skills") and f.endswith(".json"):
            ids += [s["id"] for s in json.load(open(os.path.join(root, f), encoding="utf-8"))]
    return sorted(set(ids))


def main():
    frame_slot(); bar(); ring(); panel()
    for sid in skill_ids():
        kind = next((k for w, k in KIND if w in sid), "geral")
        owner = sid.split("_")[1]
        icon(sid, kind, CLASS_COL.get(owner, CLASS_COL["geral"]))
    icon("esquiva", "esquiva", (0.75, 0.78, 0.85)); icon("comida", "comida", (0.85, 0.55, 0.25)); icon("mana", "mana", (0.3, 0.45, 1.0))
    print("ui gerada em", os.path.abspath(OUT))


if __name__ == "__main__":
    main()

"""Texturas de folhagem com alpha (pintadas proceduralmente): cacho de folha larga, tufo de pinheiro-manso,
folha de palmeira, folha de erva com flor, folha seca."""
import numpy as np, math, random
from PIL import Image, ImageDraw, ImageFilter
OUT = "/home/claude/cinzas/tapa/tex/"
SS = 2

def hexc(h, k=1.0, a=255):
    h = h.lstrip('#'); c = [int(h[i:i+2], 16) for i in (0, 2, 4)]
    return tuple(max(0, min(255, int(x * k))) for x in c) + (a,)

def leaf_poly(cx, cy, ang, L, W, n=14, tip=0.85):
    pts_l, pts_r = [], []
    ca, sa = math.cos(ang), math.sin(ang)
    for i in range(n + 1):
        t = i / n
        w = W * math.sin(math.pi * (t ** tip)) * (1 - 0.15 * t)
        x = t * L
        for side, arr in ((1, pts_l), (-1, pts_r)):
            px, py = x, side * w / 2
            arr.append((cx + px * ca - py * sa, cy + px * sa + py * ca))
    return pts_l + pts_r[::-1]

def draw_leaf(d, cx, cy, ang, L, W, base, rng, vein=True):
    k = rng.uniform(0.75, 1.2)
    col = hexc(base, k)
    poly = leaf_poly(cx, cy, ang, L, W)
    d.polygon(poly, fill=col)
    # metade sombreada
    half = poly[:len(poly)//2] + [(cx + L*math.cos(ang), cy + L*math.sin(ang))]
    d.polygon(leaf_poly(cx, cy, ang, L, W*0.5)[:15] + [(cx, cy)], fill=hexc(base, k * 0.82))
    if vein:
        d.line([(cx, cy), (cx + L*0.92*math.cos(ang), cy + L*0.92*math.sin(ang))], fill=hexc(base, k*1.35), width=max(1, int(W*0.06)))

def finish(img, name, blur=0.6):
    img = img.resize((img.width // SS, img.height // SS), Image.LANCZOS)
    a = np.asarray(img).astype(np.float32)
    # sangra a cor para fora do alpha (sem franja escura no mipmap)
    rgb = a[..., :3]; al = a[..., 3:] / 255
    acc = rgb * al; w = al.copy()
    for r in (2, 4, 8, 16, 32):
        b = Image.fromarray(np.clip(acc, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
        bw = Image.fromarray((w[..., 0] * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
        bb = np.asarray(b).astype(np.float32); bww = np.asarray(bw).astype(np.float32)[..., None] / 255
        fill = bb / np.maximum(bww, 1e-3)
        rgb = np.where(al > 0.02, rgb, np.where(w > 0.02, rgb, fill))
        w = np.maximum(w, (bww > 0.01).astype(np.float32))
    out = np.concatenate([np.clip(rgb, 0, 255), a[..., 3:]], -1).astype(np.uint8)
    Image.fromarray(out).save(OUT + name + ".png")

def cacho(name, base, twig="#4a3524", seed=1, n=34, size=1024, Lr=(0.16, 0.24), Wr=0.42):
    rng = random.Random(seed); S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    cx, cy = S / 2, S * 0.97
    # galho principal e ramos
    tips = []
    d.line([(cx, cy), (cx, S * 0.35)], fill=hexc(twig), width=int(S * 0.012))
    for i in range(6):
        y = cy - (i + 1) * S * 0.1; a = (-1) ** i * rng.uniform(0.6, 1.0) - math.pi / 2
        ex, ey = cx + math.cos(a) * S * 0.28, y + math.sin(a) * S * 0.28
        d.line([(cx, y), (ex, ey)], fill=hexc(twig), width=int(S * 0.007)); tips.append((cx, y, ex, ey))
    tips.append((cx, cy, cx, S * 0.3))
    leaves = []
    for (x0, y0, x1, y1) in tips:
        for j in range(n // len(tips) + 2):
            t = rng.uniform(0.25, 1.0)
            px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            base_ang = math.atan2(y1 - y0, x1 - x0)
            ang = base_ang + rng.choice((-1, 1)) * rng.uniform(0.5, 1.3)
            L = S * rng.uniform(*Lr); leaves.append((py, px, ang, L))
    leaves.sort(key=lambda q: -q[0])
    for (py, px, ang, L) in leaves:
        draw_leaf(d, px, py, ang, L, L * Wr, base, rng)
    finish(img, name)

def pinha(name, base="#2f4d2a", seed=3, size=1024):
    """tufo de agulhas do pinheiro-manso: leques densos saindo de vários pontos"""
    rng = random.Random(seed); S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    centers = [(S * rng.uniform(0.25, 0.75), S * rng.uniform(0.3, 0.75)) for _ in range(9)]
    d.line([(S / 2, S * 0.98)] + sorted(centers, key=lambda c: -c[1])[:3], fill=hexc("#4a3322"), width=int(S * 0.01))
    for (cx, cy) in centers:
        for _ in range(160):
            a = rng.uniform(0, math.tau); L = S * rng.uniform(0.08, 0.2)
            k = rng.uniform(0.7, 1.35)
            ex, ey = cx + math.cos(a) * L, cy + math.sin(a) * L * 0.8 - L * 0.25
            d.line([(cx, cy), (ex, ey)], fill=hexc(base, k), width=max(2, int(S * 0.0035)))
    finish(img, name)

def palma(name, base="#4f7a2c", seed=5, W=1024, H=256):
    rng = random.Random(seed); Sw, Sh = W * SS, H * SS
    img = Image.new("RGBA", (Sw, Sh), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    y0 = Sh / 2
    for i in range(70):
        t = i / 70; x = Sw * (0.03 + 0.95 * t)
        L = Sh * 0.48 * (1 - t * 0.7) * rng.uniform(0.85, 1.05)
        for side in (-1, 1):
            a = side * math.radians(rng.uniform(55, 70))
            ex, ey = x + math.cos(a) * L * 0.55, y0 + math.sin(a) * L
            w = max(3, int(Sh * 0.05 * (1 - t * 0.5)))
            k = rng.uniform(0.8, 1.2)
            poly = [(x, y0 - side * w * 0.2), (ex, ey), (x + w * 0.9, y0)]
            d.polygon(poly, fill=hexc(base, k))
            d.line([(x, y0), (ex, ey)], fill=hexc(base, k * 1.3), width=max(1, w // 4))
    d.line([(0, y0), (Sw, y0)], fill=hexc("#8a7a3e"), width=int(Sh * 0.025))
    finish(img, name)

def erva(name, base="#55803a", flor="#f2cf3a", seed=7, size=1024):
    rng = random.Random(seed); S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    cx, cy = S / 2, S * 0.98
    for i in range(22):
        a = -math.pi / 2 + rng.uniform(-1.0, 1.0); L = S * rng.uniform(0.35, 0.6)
        draw_leaf(d, cx + rng.uniform(-S*0.05, S*0.05), cy, a, L, L * 0.22, base, rng)
    for i in range(7):   # hastes com flor
        a = -math.pi / 2 + rng.uniform(-0.5, 0.5); L = S * rng.uniform(0.6, 0.85)
        ex, ey = cx + math.cos(a) * L, cy + math.sin(a) * L
        d.line([(cx, cy), (ex, ey)], fill=hexc("#5f7a34"), width=int(S * 0.006))
        for j in range(14):
            r = S * rng.uniform(0.008, 0.016); fx, fy = ex + rng.gauss(0, S*0.025), ey + rng.gauss(0, S*0.03)
            d.ellipse([fx - r, fy - r, fx + r, fy + r], fill=hexc(flor, rng.uniform(0.85, 1.1)))
            d.ellipse([fx - r*0.35, fy - r*0.35, fx + r*0.35, fy + r*0.35], fill=hexc("#b06a1a"))
    finish(img, name)

def capim(name, base="#a39a55", seed=9, size=1024):
    rng = random.Random(seed); S = size * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    for i in range(90):
        x0 = S * rng.uniform(0.2, 0.8); L = S * rng.uniform(0.5, 0.95); bend = rng.uniform(-0.25, 0.25) * S
        k = rng.uniform(0.6, 1.25); w = S * rng.uniform(0.008, 0.016)
        pts = [(x0 + bend * (t ** 2) - w * (1 - t), S - L * t) for t in np.linspace(0, 1, 10)] + \
              [(x0 + bend * (t ** 2) + w * (1 - t), S - L * t) for t in np.linspace(1, 0, 10)]
        d.polygon(pts, fill=hexc(base, k))
    finish(img, name)

if __name__ == "__main__":
    cacho("folha_arbusto", "#56702f", seed=11, n=90, Lr=(0.11, 0.17), Wr=0.5)       # arbusto da costa: oliva
    cacho("folha_larga", "#4f8a32", seed=12, n=80, Lr=(0.15, 0.22), Wr=0.45)        # árvore da praça
    pinha("agulha_pinheiro", "#35562c", seed=3)
    palma("folha_palmeira", "#5a8a2e", seed=5)
    palma("folha_palmeira_seca", "#9b8a4a", seed=6)
    erva("erva_tonico", "#4f7f3c", "#f0c93a", seed=7)
    capim("capim_seco", "#a5995a", seed=9)
    print("ok")

"""Baralho do bar (Vinte-e-Um): 52 cartas + verso, desenhadas por código (arte própria, CC0).
Saída: Client/Assets/Art/cartas/<codigo>.png (256x358) com o mesmo código da engine (AE, 10C, QO, KP...) e verso.png."""
import math, random, os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
OUT = sys.argv[1] if len(sys.argv) > 1 else "Client/Assets/Art/cartas/"
os.makedirs(OUT, exist_ok=True)
W, H, SS = 256, 358, 3
FB = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"
NAIPES = {"P": ("♣", (28, 26, 30)), "O": ("♦", (150, 30, 34)), "C": ("♥", (150, 30, 34)), "E": ("♠", (28, 26, 30))}
VAL = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"]
PIPS = {  # posições (x, y) em 0..1 dentro da área central
 2: [(.5, .12), (.5, .88)], 3: [(.5, .12), (.5, .5), (.5, .88)], 4: [(.25, .12), (.75, .12), (.25, .88), (.75, .88)],
 5: [(.25, .12), (.75, .12), (.5, .5), (.25, .88), (.75, .88)], 6: [(.25, .12), (.75, .12), (.25, .5), (.75, .5), (.25, .88), (.75, .88)],
 7: [(.25, .12), (.75, .12), (.5, .31), (.25, .5), (.75, .5), (.25, .88), (.75, .88)],
 8: [(.25, .12), (.75, .12), (.5, .31), (.25, .5), (.75, .5), (.5, .69), (.25, .88), (.75, .88)],
 9: [(.25, .1), (.75, .1), (.25, .37), (.75, .37), (.5, .5), (.25, .63), (.75, .63), (.25, .9), (.75, .9)],
 10: [(.25, .1), (.75, .1), (.5, .24), (.25, .37), (.75, .37), (.25, .63), (.75, .63), (.5, .76), (.25, .9), (.75, .9)],
}

def papel(seed):
    rng = random.Random(seed); im = Image.new("RGB", (W * SS, H * SS), (236, 226, 202)); px = im.load()
    noise = Image.effect_noise((W * SS // 4, H * SS // 4), 18).resize((W * SS, H * SS), Image.BICUBIC).convert("L")
    base = Image.new("RGB", im.size, (236, 226, 202)); dark = Image.new("RGB", im.size, (214, 198, 166))
    im = Image.composite(dark, base, noise.point(lambda v: max(0, min(255, (v - 110) * 3))))
    return im

def moldura(d, cor):
    m = 10 * SS
    d.rounded_rectangle([m, m, W * SS - m, H * SS - m], radius=14 * SS, outline=cor + (255,), width=2 * SS)

def canto(d, txt, simb, cor, font, fsmb):
    for flip in (False, True):
        x, y = (16 * SS, 14 * SS)
        if flip: x, y = W * SS - 16 * SS, H * SS - 14 * SS
        anchor_t = "la" if not flip else "rd"
        d.text((x, y), txt, font=font, fill=cor, anchor="lt" if not flip else "rb")
        oy = 34 * SS if not flip else -34 * SS
        d.text((x + (2 * SS if not flip else -2 * SS), y + oy), simb, font=fsmb, fill=cor, anchor="lt" if not flip else "rb")

def figura(im, d, v, simb, cor, fbig):
    """valete, dama e rei: retrato em medalhão (símbolo próprio: elmo, coroa de folhas, coroa de pontas)"""
    cx, cy = W * SS // 2, H * SS // 2; r = 78 * SS
    d.ellipse([cx - r, cy - r * 1.25, cx + r, cy + r * 1.25], fill=(120, 36, 40) if cor[0] > 100 else (40, 44, 58), outline=(196, 160, 90), width=4 * SS)
    gold = (214, 178, 96)
    if v == "K":
        pts = [(cx - 48 * SS, cy + 10 * SS), (cx - 48 * SS, cy - 40 * SS), (cx - 24 * SS, cy - 10 * SS), (cx, cy - 56 * SS), (cx + 24 * SS, cy - 10 * SS), (cx + 48 * SS, cy - 40 * SS), (cx + 48 * SS, cy + 10 * SS)]
        d.polygon(pts, fill=gold); d.rectangle([cx - 48 * SS, cy + 10 * SS, cx + 48 * SS, cy + 26 * SS], fill=gold)
        for x in (-30, 0, 30): d.ellipse([cx + (x - 6) * SS, cy + 12 * SS, cx + (x + 6) * SS, cy + 24 * SS], fill=(150, 30, 34))
    elif v == "Q":
        for i in range(9):
            a = math.pi + i * math.pi / 8; x = cx + math.cos(a) * 50 * SS; y = cy + 20 * SS + math.sin(a) * 50 * SS
            d.ellipse([x - 12 * SS, y - 20 * SS, x + 12 * SS, y + 20 * SS], fill=gold)
        d.ellipse([cx - 16 * SS, cy - 50 * SS, cx + 16 * SS, cy - 18 * SS], fill=(230, 220, 240))
    else:
        d.polygon([(cx - 40 * SS, cy + 30 * SS), (cx - 40 * SS, cy - 20 * SS), (cx, cy - 50 * SS), (cx + 40 * SS, cy - 20 * SS), (cx + 40 * SS, cy + 30 * SS)], fill=(170, 172, 180))
        d.rectangle([cx - 30 * SS, cy - 8 * SS, cx + 30 * SS, cy], fill=(30, 30, 34)); d.line([(cx, cy - 50 * SS), (cx, cy + 30 * SS)], fill=(120, 122, 130), width=3 * SS)
    d.text((cx, cy + 62 * SS), simb, font=fbig, fill=gold, anchor="mm")

def carta(v, n):
    simb, cor = NAIPES[n]; im = papel(hash(v + n) & 0xffff).convert("RGBA"); d = ImageDraw.Draw(im)
    moldura(d, (160, 128, 70))
    f = ImageFont.truetype(FB, 34 * SS); fs = ImageFont.truetype(FB, 28 * SS); fp = ImageFont.truetype(FB, 46 * SS); fa = ImageFont.truetype(FB, 120 * SS)
    canto(d, v, simb, cor, f, fs)
    if v == "A":
        d.text((W * SS // 2, H * SS // 2), simb, font=fa, fill=cor, anchor="mm")
        d.ellipse([W * SS // 2 - 92 * SS, H * SS // 2 - 92 * SS, W * SS // 2 + 92 * SS, H * SS // 2 + 92 * SS], outline=(196, 160, 90), width=3 * SS)
    elif v in ("J", "Q", "K"):
        figura(im, d, v, simb, cor, ImageFont.truetype(FB, 40 * SS))
    else:
        x0, y0, x1, y1 = 62 * SS, 58 * SS, W * SS - 62 * SS, H * SS - 58 * SS
        for (px, py) in PIPS[int(v)]:
            x = x0 + (x1 - x0) * px; y = y0 + (y1 - y0) * py
            if py > .55:
                t = Image.new("RGBA", (60 * SS, 60 * SS), (0, 0, 0, 0)); ImageDraw.Draw(t).text((30 * SS, 30 * SS), simb, font=fp, fill=cor, anchor="mm")
                t = t.rotate(180); im.alpha_composite(t, (int(x - 30 * SS), int(y - 30 * SS)))
            else: d.text((x, y), simb, font=fp, fill=cor, anchor="mm")
    return finaliza(im)

def verso():
    im = Image.new("RGBA", (W * SS, H * SS), (92, 28, 34, 255)); d = ImageDraw.Draw(im)
    for i in range(-H * SS, W * SS, 18 * SS):
        d.line([(i, 0), (i + H * SS, H * SS)], fill=(110, 36, 42), width=3 * SS); d.line([(i + H * SS, 0), (i, H * SS)], fill=(110, 36, 42), width=3 * SS)
    moldura(d, (200, 164, 92)); d.rounded_rectangle([22 * SS, 22 * SS, W * SS - 22 * SS, H * SS - 22 * SS], radius=10 * SS, outline=(200, 164, 92), width=SS)
    cx, cy = W * SS // 2, H * SS // 2; gold = (214, 178, 96)
    d.ellipse([cx - 62 * SS, cy - 62 * SS, cx + 62 * SS, cy + 62 * SS], fill=(70, 20, 26), outline=gold, width=3 * SS)
    d.line([(cx, cy - 46 * SS), (cx, cy + 50 * SS)], fill=gold, width=5 * SS)   # tridente (marca do Porto das Cinzas)
    for x in (-26, 26):
        d.line([(cx + x * SS, cy - 36 * SS), (cx + x * SS, cy - 4 * SS)], fill=gold, width=5 * SS)
        d.polygon([(cx + (x - 7) * SS, cy - 36 * SS), (cx + x * SS, cy - 50 * SS), (cx + (x + 7) * SS, cy - 36 * SS)], fill=gold)
    d.polygon([(cx - 7 * SS, cy - 46 * SS), (cx, cy - 60 * SS), (cx + 7 * SS, cy - 46 * SS)], fill=gold)
    d.arc([cx - 26 * SS, cy - 26 * SS, cx + 26 * SS, cy + 18 * SS], 0, 180, fill=gold, width=5 * SS)
    return finaliza(im)

def finaliza(im):
    mask = Image.new("L", im.size, 0); ImageDraw.Draw(mask).rounded_rectangle([0, 0, im.size[0] - 1, im.size[1] - 1], radius=18 * SS, fill=255)
    im.putalpha(mask)
    return im.resize((W, H), Image.LANCZOS)

for n in NAIPES:
    for v in VAL: carta(v, n).save(os.path.join(OUT, f"{v}{n}.png"))
verso().save(os.path.join(OUT, "verso.png"))
print("ok", len(os.listdir(OUT)))

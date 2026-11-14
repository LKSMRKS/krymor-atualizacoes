"""Acabamento do mapa ilustrado + tela de mapa (prova). Uso: python3 mapa_post.py <map.json> <dir_mapa> <arte_ui2>"""
import sys, json, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
mp, d, art = sys.argv[1:4]
M = json.load(open(mp)); EXT = 130.0; N = 2048
raw = Image.open(f"{d}/_mapa_raw.png").convert("RGB")
# pintura: suaviza detalhe fino, realca contorno, mais contraste e cor
im = raw.filter(ImageFilter.ModeFilter(5)).filter(ImageFilter.SMOOTH_MORE)
im = Image.blend(im, raw, 0.35).filter(ImageFilter.UnsharpMask(3, 120, 2))
im = ImageEnhance.Color(im).enhance(1.18); im = ImageEnhance.Contrast(im).enhance(1.12)
a = np.array(im).astype(float)
yy, xx = np.mgrid[0:N, 0:N] / N
# fora das muralhas (alem de |112| m) escurece como pergaminho queimado
mx = (np.abs(xx - 0.5) * 2 * EXT); my = (np.abs(yy - 0.5) * 2 * EXT)
fora = np.clip((np.maximum(mx, np.where(yy > 0.5, my, 0)) - 112) / 14, 0, 1)
a *= (1 - 0.55 * fora)[..., None]
vin = 1 - 0.35 * np.clip((np.maximum(np.abs(xx - 0.5), np.abs(yy - 0.5)) - 0.35) / 0.15, 0, 1)
a *= vin[..., None]
Image.fromarray(a.clip(0, 255).astype("uint8")).save(f"{d}/mapa_costa_do_cal.png", optimize=True)
# nevoa de exemplo (areas nao exploradas): mascara alfa de nuvens escuras
rng = np.random.default_rng(3)
def nuvem(n, s):
    f = np.fft.fftfreq(n) * n; fx, fy = np.meshgrid(f, f); k = np.sqrt(fx ** 2 + fy ** 2); k[0, 0] = 1
    x = np.real(np.fft.ifft2(np.exp(1j * rng.uniform(0, 6.28, (n, n))) / k ** s)); return (x - x.min()) / (x.max() - x.min())
c = np.array(Image.fromarray((nuvem(512, 1.5) * 255).astype("uint8")).resize((N, N), Image.BICUBIC)) / 255
Image.fromarray((np.clip((c - 0.25) * 2.2, 0, 1) * 255).astype("uint8"), "L").save(f"{d}/mapa_nevoa_textura.png")
print("ok mapa")

# ---------------- tela de mapa (prova), como no estudo 02
A = lambda n: Image.open(f"{art}/{n}.png").convert("RGBA")
F = lambda s: ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", s)
FR = lambda s: ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerifCondensed.ttf", s)
def nine(tex, w, h, m):
    tw, th = tex.size; o = Image.new("RGBA", (w, h))
    for sx0, sx1, dx0, dx1 in ((0, m, 0, m), (m, tw - m, m, w - m), (tw - m, tw, w - m, w)):
        for sy0, sy1, dy0, dy1 in ((0, m, 0, m), (m, th - m, m, h - m), (th - m, th, h - m, h)):
            if dx1 > dx0 and dy1 > dy0: o.alpha_composite(tex.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS), (dx0, dy0))
    return o
def three(tex, w, h, cap):
    tw, th = tex.size; c = int(cap * h / th); o = Image.new("RGBA", (w, h))
    o.alpha_composite(tex.crop((0, 0, cap, th)).resize((c, h)), (0, 0)); o.alpha_composite(tex.crop((cap, 0, tw - cap, th)).resize((w - 2 * c, h)), (c, 0))
    o.alpha_composite(tex.crop((tw - cap, 0, tw, th)).resize((c, h)), (w - c, 0)); return o
def T(dr, xy, s, size, col, anchor="mm", font=F):
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1), (0, 2)): dr.text((xy[0] + dx, xy[1] + dy), s, font=font(size), fill=(0, 0, 0, 235), anchor=anchor)
    dr.text(xy, s, font=font(size), fill=col, anchor=anchor)
W, H = 1600, 1000; S = 940
tela = Image.new("RGBA", (W, H), (14, 13, 13, 255))
mapa = Image.open(f"{d}/mapa_costa_do_cal.png").convert("RGBA").resize((S, S), Image.LANCZOS)
# nevoa: o canto leste ainda nao explorado
nv = Image.open(f"{d}/mapa_nevoa_textura.png").resize((S, S))
m = np.array(nv).astype(float) / 255; yy, xx = np.mgrid[0:S, 0:S] / S
area = np.clip((xx - 0.62) / 0.12, 0, 1) * np.clip((0.55 - yy) / 0.12, 0, 1)
alpha = np.clip(area * (0.55 + 0.6 * m), 0, 0.96)
fog = np.dstack([np.full((S, S), 18), np.full((S, S), 17), np.full((S, S), 20), alpha * 255]).astype("uint8")
mapa.alpha_composite(Image.fromarray(fog, "RGBA"))
ox, oy = 30, 40; tela.alpha_composite(mapa, (ox, oy)); tela.alpha_composite(nine(A("ui_mapa_moldura"), S + 24, S + 24, 48), (ox - 12, oy - 12))
dr = ImageDraw.Draw(tela)
def px(x, y): return (ox + (x + EXT) / (2 * EXT) * S, oy + (y + EXT) / (2 * EXT) * S)
tela.alpha_composite(three(A("ui_placa_zona"), 400, 54, 64), (ox + 20, oy + 22)); tela.alpha_composite(A("ico_mapa_cidade").resize((38, 38)), (ox + 30, oy + 30))
dr = ImageDraw.Draw(tela); T(dr, (ox + 80, oy + 49), "COSTA DO CAL", 22, (215, 215, 220), "lm")
icons = {"cidade": A("ico_mapa_cidade"), "cacada": A("ico_mapa_cacada"), "masmorra": A("ico_mapa_masmorra"), "passagem": A("ico_mapa_passagem"), "pos": A("ico_mapa_posicao")}
marks = [("Porto das Cinzas", 0, 112, "cidade"), ("Galeria Funda", -99, -64, "masmorra"), ("Vale das Cinzas", 48, -112, "passagem"),
         ("Acampamento das Dunas", 0, 19, "cacada"), ("Ruínas do Farol", 60, -32, "cacada"), ("Enseada do Tirano", 88, -88, "cacada"), ("Baía Quebrada", 0, -86, "cacada")]
labels = [("Areal da Maré", 0, 70), ("Dunas Salgadas", -64, 10), ("Praia dos Barcos", -84, 90), ("Rochedo do Cal", 86, 76), ("Areal Fundo", -82, -32), ("Acampamento da Vigília", 34, -90)]
for name, x, y, ic in marks:
    p = px(x, y); tela.alpha_composite(icons[ic].resize((40, 40), Image.LANCZOS), (int(p[0] - 20), int(p[1] - 20)))
    T(ImageDraw.Draw(tela), (p[0], p[1] + 30), name, 17, (236, 226, 205))
for name, x, y in labels:
    p = px(x, y); T(ImageDraw.Draw(tela), p, name, 15, (200, 190, 168), font=FR)
p = px(-12, 40); tela.alpha_composite(icons["pos"].resize((34, 34), Image.LANCZOS).rotate(-30, expand=True), (int(p[0] - 20), int(p[1] - 20)))
# rosa dos ventos simples
dr = ImageDraw.Draw(tela); cx, cy = ox + S - 70, oy + S - 90
dr.polygon([(cx, cy - 40), (cx + 8, cy), (cx, cy + 40), (cx - 8, cy)], fill=(210, 205, 195)); dr.polygon([(cx - 40, cy), (cx, cy - 8), (cx + 40, cy), (cx, cy + 8)], fill=(150, 145, 138))
T(dr, (cx, cy - 54), "N", 18, (220, 215, 205))
# legenda
lx, ly = 1010, 40; tela.alpha_composite(nine(A("ui_moldura"), 560, 420, 26), (lx, ly)); tela.alpha_composite(three(A("ui_aba"), 220, 40, 48), (lx + 14, ly - 18))
dr = ImageDraw.Draw(tela); T(dr, (lx + 40, ly + 2), "Legenda", 18, (236, 226, 205), "lm")
for i, (ic, txt) in enumerate((("cidade", "Cidade"), ("cacada", "Caçada"), ("masmorra", "Masmorra"), ("passagem", "Passagem"), ("pos", "Posição atual"))):
    y = ly + 60 + i * 66; tela.alpha_composite(icons[ic].resize((44, 44), Image.LANCZOS), (lx + 40, y)); T(ImageDraw.Draw(tela), (lx + 104, y + 22), txt, 18, (220, 212, 196), "lm", FR)
dr = ImageDraw.Draw(tela)
T(dr, (lx + 280, ly + 460), "M — abrir / fechar mapa", 15, (150, 142, 128), "mm", FR)
tela.convert("RGB").save(f"{d}/tela_mapa.png"); print("ok tela")

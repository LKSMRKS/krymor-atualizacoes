"""Barra compacta (orbe + 6 slots numerados + orbe) a partir das pecas do HUD v2. Saida: ui_barra_compacta.png + previa."""
import sys, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
art, icos, out = sys.argv[1:4]
A = lambda n: Image.open(f"{art}/{n}.png").convert("RGBA")
bar = A("ui_barra_inferior"); W0, H0 = bar.size
a = np.array(bar)[..., 3]; cols = np.where(a.max(0) > 200)[0]
# placa central: maior trecho continuo opaco
runs, st = [], cols[0]
for p, q in zip(cols, cols[1:]):
    if q != p + 1: runs.append((st, p)); st = q
runs.append((st, cols[-1])); x0, x1 = max(runs, key=lambda r: r[1] - r[0])
rows = np.where(a[:, x0:x1].max(1) > 200)[0]; plate = bar.crop((x0, rows[0], x1 + 1, rows[-1] + 1))
SL, GAP, N = 92, 10, 6                       # slot 92 px
pw = N * SL + (N + 1) * GAP + 60; ph = 128
plate = plate.resize((pw, ph), Image.LANCZOS)
O = 236; ov = 52                             # orbe e quanto ele entra na placa
W, H = pw + 2 * O - 2 * ov, O
img = Image.new("RGBA", (W, H))
img.alpha_composite(plate, (O - ov, H - ph - 18))
slot = A("ui_slot_hab").resize((SL, SL), Image.LANCZOS)
sx0 = O - ov + 30 + GAP; sy = H - ph - 18 + (ph - SL) // 2 - 6
for i in range(N): img.alpha_composite(slot, (sx0 + i * (SL + GAP), sy))
om = A("ui_orbe_moldura").resize((O, O), Image.LANCZOS)
for x in (0, W - O): img.alpha_composite(om, (x, 0))
img.save(f"{art}/ui_barra_compacta.png")
meta = dict(largura=W, altura=H, orbe_esq=(O // 2, O // 2), orbe_dir=(W - O // 2, O // 2), orbe_raio_liquido=round(O * 196 / 640),
            slots=[(sx0 + i * (SL + GAP), sy, SL) for i in range(N)])
print(meta)
# ---- previa com liquido e icones (como a referencia)
def liquid(D, nivel, cor):
    yy, xx = np.mgrid[0:D, 0:D] / D * 2 - 1; r = np.sqrt(xx ** 2 + yy ** 2)
    n = (np.sin(xx * 5 + np.sin(yy * 4)) * np.cos(yy * 5) + 1) / 2; cor = np.array(cor) / 255
    c = (cor * 0.2 + cor * (0.35 + 0.7 * n)[..., None]) * (1.35 - 0.9 * np.clip(r, 0, 1))[..., None]
    topo = 1 - 2 * nivel; c = np.where((yy > topo)[..., None], c, 0.03)
    return Image.fromarray(np.dstack([np.clip(c, 0, 1) * 255, (r < 1) * 255.0]).astype("uint8"), "RGBA")
pv = Image.new("RGBA", (W + 40, H + 40), (58, 56, 54, 255)); pv.alpha_composite(img, (20, 20))
R = meta["orbe_raio_liquido"]
for (cx, cy), nv, cor in ((meta["orbe_esq"], 0.8, (200, 20, 18)), (meta["orbe_dir"], 0.9, (30, 80, 230))):
    lq = liquid(2 * R, nv, cor); g = Image.new("RGBA", pv.size); g.alpha_composite(lq, (20 + cx - R, 20 + cy - R))
    base = pv.copy(); base.alpha_composite(g); pv = base
    pv.alpha_composite(A("ui_orbe_vidro").resize((O, O), Image.LANCZOS), (20 + cx - O // 2, 20 + cy - O // 2))
    pv.alpha_composite(A("ui_orbe_moldura").resize((O, O), Image.LANCZOS), (20 + cx - O // 2, 20 + cy - O // 2))
F = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", 22); d = ImageDraw.Draw(pv)
ic = ["pocao_vida", "pocao_mana", "espada_t2_bronze_ritual", "espada_t3_brasa_bifurcada", "escudo_t2_pipa_ferro", "bolsa"]
for i, (x, y, s) in enumerate(meta["slots"]):
    f = f"{icos}/ico_{ic[i]}_256.png"
    import os
    if not os.path.exists(f): f = f"{icos}/ico_{ic[i]}.png"
    if os.path.exists(f): pv.alpha_composite(Image.open(f).convert("RGBA").resize((s - 14, s - 14), Image.LANCZOS), (20 + x + 7, 20 + y + 7))
    if i == 2: pv.alpha_composite(A("ui_slot_hab_ativo").resize((s, s), Image.LANCZOS), (20 + x, 20 + y))
    d = ImageDraw.Draw(pv); t = str(i + 1)
    for dx, dy in ((-1, -1), (1, 1), (1, -1), (-1, 1)): d.text((20 + x + s // 2 + dx, 20 + y + s + 14 + dy), t, font=F, fill=(0, 0, 0), anchor="mm")
    d.text((20 + x + s // 2, 20 + y + s + 14), t, font=F, fill=(236, 228, 212), anchor="mm")
pv.convert("RGB").save(out)

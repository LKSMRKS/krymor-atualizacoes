"""Texturas de efeito (Cal lote 2). Uso: python3 tex_fx.py <saida>
fx_agua_n.png (512, normal de marolas, emenda) · fx_espuma.png (512, mascara de espuma, emenda)
fx_fumaca.png (256, baforada macia com alfa) · fx_poeira.png (256, rastro de areia com alfa) · fx_folha.png (128, folha seca com alfa)"""
import sys, os, numpy as np
sys.path.insert(0, "/home/claude/cinzas/tools")
import tex_costa as T
from PIL import Image, ImageDraw, ImageFilter

def tile_noise(n, beta, seed, lo=1):
    T.N = n; return T.fnoise(beta, seed, lo)

def agua(out):
    n = 512
    h = 0.6 * tile_noise(n, 1.4, 1, 3) + 0.4 * tile_noise(n, 1.1, 2, 10)
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 6; dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 6
    nn = np.dstack([-dx, dy, np.ones_like(h)]); nn /= np.linalg.norm(nn, axis=2, keepdims=True)
    Image.fromarray(((nn * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(out, "fx_agua_n.png"))
    f = tile_noise(n, 1.2, 3, 4); g = tile_noise(n, 0.8, 4, 16)
    foam = np.clip((f - 0.45) * 4, 0, 1) * np.clip((g - 0.3) * 3, 0, 1)
    Image.fromarray((foam * 255).astype(np.uint8), "L").save(os.path.join(out, "fx_espuma.png"))

def puff(out, name, size, seed, streak=False):
    r = np.random.default_rng(seed); n = size
    yy, xx = np.mgrid[0:n, 0:n] / n - 0.5
    if streak: d = np.sqrt((xx / 0.48) ** 2 + (yy / 0.12) ** 2)
    else: d = np.sqrt(xx ** 2 + yy ** 2) / 0.48
    base = np.clip(1 - d, 0, 1) ** 1.6
    T.N = n; nz = T.fnoise(1.3, seed, 2)
    a = np.clip(base * (0.5 + 0.9 * nz) - 0.08, 0, 1)
    rgb = np.ones((n, n, 3)) * (0.92 + 0.08 * nz[..., None])
    img = np.dstack([rgb, a[..., None]])
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(os.path.join(out, name))

def folha(out):
    n = 128; im = Image.new("RGBA", (n, n), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.ellipse((24, 44, 104, 84), fill=(150, 112, 52, 255)); d.line((20, 64, 108, 64), fill=(90, 62, 28, 255), width=3)
    for k in range(5): d.line((40 + k * 14, 64, 48 + k * 14, 50), fill=(110, 78, 36, 255), width=2); d.line((40 + k * 14, 64, 48 + k * 14, 78), fill=(110, 78, 36, 255), width=2)
    im.filter(ImageFilter.SMOOTH).save(os.path.join(out, "fx_folha.png"))

if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    agua(out); puff(out, "fx_fumaca.png", 256, 11); puff(out, "fx_poeira.png", 256, 12, streak=True); folha(out); print("ok")

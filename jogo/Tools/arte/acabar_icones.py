"""Acabamento dos ícones renderizados no Blender (raw_*.png): reduz para 256, contraste e sombra. Uso: acabar_icones.py <pasta> <destino>"""
import os, sys
from PIL import Image, ImageFilter
import numpy as np
src, dst = sys.argv[1], sys.argv[2]
for f in sorted(os.listdir(src)):
    if not f.startswith("raw_"): continue
    im = Image.open(os.path.join(src, f)).convert("RGBA").resize((256, 256), Image.LANCZOS)
    a = np.array(im).astype(float)
    rgb = np.clip((a[..., :3] / 255) ** 1.1 * 1.12, 0, 1); lum = rgb.mean(2, keepdims=True); rgb = np.clip(lum + (rgb - lum) * 1.2, 0, 1)
    a[..., :3] = rgb * 255
    im = Image.fromarray(a.astype("uint8"), "RGBA")
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0)); sh.putalpha(im.split()[3].filter(ImageFilter.GaussianBlur(4)).point(lambda v: int(v * 0.7)))
    out = Image.new("RGBA", im.size); out.alpha_composite(sh, (3, 4)); out.alpha_composite(im)
    out.save(os.path.join(dst, "ico_" + f[4:])); print("ok", f)

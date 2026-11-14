"""Chão em estilo pintado e suave (referência do dono: campo do remake do Ocarina): grama clara com manchas grandes e
pinceladas, terra batida lisa com pedrinhas. Ruído por FFT, então a textura emenda sozinha (sem costura)."""
import numpy as np, sys, os
from PIL import Image, ImageFilter
N = 1024
OUT = sys.argv[1] if len(sys.argv) > 1 else "."
rng = np.random.default_rng(7)

def ruido(freq_lo, freq_hi, seed, aniso=None):
    r = np.random.default_rng(seed); w = r.standard_normal((N, N))
    fy = np.fft.fftfreq(N)[:, None] * N; fx = np.fft.fftfreq(N)[None, :] * N
    if aniso is not None:
        c, s = np.cos(aniso), np.sin(aniso); fx, fy = fx * c - fy * s, (fx * s + fy * c) * 3.0
    f = np.sqrt(fx ** 2 + fy ** 2)
    band = np.exp(-((np.log(f + 1e-6) - np.log((freq_lo * freq_hi) ** .5)) ** 2) / (2 * (np.log(freq_hi / freq_lo) / 2.5) ** 2))
    band[0, 0] = 0
    out = np.real(np.fft.ifft2(np.fft.fft2(w) * band))
    out -= out.min(); out /= out.max(); return out

def hexc(h): return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], np.float32) / 255

def ramp(t, stops):
    t = np.clip(t, 0, 1); out = np.zeros(t.shape + (3,), np.float32)
    ps = [p for p, _ in stops]; cs = [hexc(c) for _, c in stops]
    for i in range(3): out[..., i] = np.interp(t, ps, [c[i] for c in cs])
    return out

def normal(h, k):
    gy, gx = np.gradient(h); n = np.dstack([-gx * k, -gy * k, np.ones_like(h)]); n /= np.linalg.norm(n, axis=2, keepdims=True)
    return ((n * .5 + .5) * 255).astype(np.uint8)

def salva(nome, cor, h, k):
    Image.fromarray((np.clip(cor, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, nome + ".png"))
    Image.fromarray(normal(h, k)).save(os.path.join(OUT, nome + "_n.png"))

# ---------------- grama
macro = ruido(1, 3, 1); meio = ruido(4, 10, 2); seco = ruido(5, 11, 3)
pincel = np.zeros((N, N), np.float32)
for i, a in enumerate((0.3, 1.2, 2.1, 2.8)): pincel += ruido(30, 70, 10 + i, aniso=a)
pincel /= 4; pincel = (pincel - pincel.min()) / (pincel.max() - pincel.min())
grao = ruido(80, 200, 4)
fino = ruido(60, 140, 6)
t = .12 * macro + .28 * meio + .4 * pincel + .2 * fino
cor = ramp(t, [(0, "#4c6629"), (.3, "#61803a"), (.55, "#7c9a45"), (.8, "#98ad55"), (1, "#b2bd6c")])
mdry = np.clip((seco - .6) * 4, 0, 1)[..., None] * .35
cor = cor * (1 - mdry) + ramp(pincel, [(0, "#8c8a4a"), (1, "#b3a565")]) * mdry       # manchas de capim seco
cor *= (.88 + .22 * grao)[..., None]
flor = (ruido(120, 260, 5) > .86) & (macro > .45)
cor[flor] = cor[flor] * .4 + hexc("#e8e2b0") * .6                                       # pontinhos claros (florzinhas)
g = cor.mean(2, keepdims=True); cor = cor * .85 + g * .15
salva("ter_grama", cor, .6 * pincel + .4 * grao, 3.0)

# ---------------- terra batida
macro = ruido(1, 3, 21); meio = ruido(5, 12, 22); grao = ruido(90, 220, 23)
cor = ramp(.2 * macro + .45 * meio + .35 * ruido(25, 60, 26), [(0, "#6d5038"), (.4, "#86664a"), (.75, "#a1825f"), (1, "#b39572")])
cor *= (.9 + .18 * grao)[..., None]
h = .5 * meio + .3 * grao
# pedrinhas: discos claros com sombra embaixo (emendam porque o ruído é periódico)
pd = ruido(28, 55, 24); pedra = pd > .84
som = np.roll(pedra, 5, axis=0) & ~pedra
cor[som] *= .7
cor[pedra] = cor[pedra] * .35 + ramp((pd[pedra] - .84) / .16, [(0, "#9a8c78"), (1, "#c8bba4")]) * .65
h = h + pedra * .6
# rastro de rodas/pés: leve escurecido em faixas
faixa = ruido(2, 6, 25, aniso=0.0); cor *= (1 - .08 * np.clip((faixa - .6) * 4, 0, 1))[..., None]
salva("ter_terra", cor, h, 4.0)
print("ok")

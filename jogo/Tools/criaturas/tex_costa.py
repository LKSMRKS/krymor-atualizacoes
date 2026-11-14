"""Texturas de chao da Costa do Cal (2048, emendam nas 4 bordas): cor + relevo (_n.png, normal OpenGL/Godot) + rugosidade (_r.png).
Uso: python3 tex_costa.py <pasta_saida>
- ter_areia.png        areia clara de cal (pouco saturada: os monstros coloridos destacam), ondulacao de vento, cascalho e lascas de concha
- ter_costa_cal.png    chao de cal: placas de calcario rachadas com areia e capim seco nas frestas
- ter_areia_molhada.png  areia escura e umida (beira d'agua, lago, poças), brilho maior"""
import sys, os, numpy as np
from PIL import Image

N = 2048
rng = np.random.default_rng(7)
u = np.arange(N) / N
U, V = np.meshgrid(u, u, indexing="xy")

def fnoise(beta, seed, lo=1):
    """ruido periodico por FFT (espectro 1/f^beta) normalizado 0..1"""
    r = np.random.default_rng(seed)
    f = np.fft.fftfreq(N) * N
    fx, fy = np.meshgrid(f, f, indexing="xy")
    k = np.sqrt(fx ** 2 + fy ** 2); k[0, 0] = 1
    amp = 1 / k ** beta; amp[k < lo] = 0
    ph = r.uniform(0, 2 * np.pi, (N, N))
    x = np.real(np.fft.ifft2(amp * np.exp(1j * ph)))
    x -= x.min(); return x / x.max()

def lin2srgb(c): c = np.clip(c, 0, 1); return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)
def hexl(h): h = h.lstrip('#'); c = np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]); return np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
def mix(a, b, t): t = np.clip(t, 0, 1)[..., None]; return a * (1 - t) + b * t
def sstep(e0, e1, x): t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def blobs(count, rmin, rmax, seed, elong=1.0):
    """discos/elipses espalhados com emenda; devolve altura (0..1) e id de cada pedra"""
    r = np.random.default_rng(seed)
    h = np.zeros((N, N)); ids = np.zeros((N, N))
    for i in range(count):
        cx, cy = r.uniform(0, N, 2); rad = r.uniform(rmin, rmax); ang = r.uniform(0, np.pi); e = r.uniform(1.0, elong)
        R = int(rad * e) + 2
        xs = (np.arange(int(cx) - R, int(cx) + R)) % N; ys = (np.arange(int(cy) - R, int(cy) + R)) % N
        gx, gy = np.meshgrid(np.arange(-R, R) + (int(cx) - cx), np.arange(-R, R) + (int(cy) - cy), indexing="xy")
        ca, sa = np.cos(ang), np.sin(ang)
        lx = (gx * ca + gy * sa) / (rad * e); ly = (-gx * sa + gy * ca) / rad
        d = np.sqrt(lx ** 2 + ly ** 2)
        bump = np.clip(1 - d ** 2, 0, 1) ** 0.6
        sub = h[np.ix_(ys, xs)]
        mask = bump > sub
        sub[mask] = bump[mask]; h[np.ix_(ys, xs)] = sub
        idsub = ids[np.ix_(ys, xs)]; idsub[mask] = (i % 97) / 97 + 0.005; ids[np.ix_(ys, xs)] = idsub
    return h, ids

def normal_from(hgt, strength):
    dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5 * strength
    dy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5 * strength
    n = np.dstack([-dx, dy, np.ones_like(hgt)])      # OpenGL (Y+): Godot
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)

def save(out, name, col_lin, hgt, strength, rough):
    if name != "ter_areia_molhada": col_lin = col_lin * np.array([0.74, 0.69, 0.62])   # valor medio e mais quente (nao estoura com o sol da praia)
    Image.fromarray((lin2srgb(col_lin) * 255).astype(np.uint8)).save(os.path.join(out, name + ".png"), optimize=True)
    Image.fromarray(normal_from(hgt, strength)).save(os.path.join(out, name + "_n.png"), optimize=True)
    Image.fromarray((np.clip(rough, 0, 1) * 255).astype(np.uint8)).save(os.path.join(out, name + "_r.png"), optimize=True)
    print("ok", name)

def areia(out, wet=False):
    low = fnoise(1.6, 1); mid = fnoise(1.2, 2, 8); fine = fnoise(0.6, 3, 64)
    warp = fnoise(1.8, 4) - 0.5
    # ondulacao de vento: frentes diagonais (coeficientes inteiros = emenda), deformadas pelo ruido
    ph = 2 * np.pi * (13 * U + 5 * V + 0.9 * warp)
    rip = (np.sin(ph + 0.6 * np.sin(ph)) * 0.5 + 0.5) ** 1.3
    rip *= sstep(0.25, 0.7, fnoise(1.5, 5))          # so em manchas: sem listras no mapa inteiro
    peb, pid = blobs(2600, 2.5, 7, 11, 1.4)          # cascalho
    sh, sid = blobs(500, 4, 10, 12, 2.4)              # lascas de concha (alongadas)
    sh *= (fnoise(1.0, 13, 4) > 0.55)
    hgt = 0.35 * low + 0.25 * mid + 0.5 * rip * (0.6 + 0.4 * mid) + 0.08 * fine + 0.9 * peb + 0.5 * sh
    if not wet:
        base = mix(hexl("#cdbfa3"), hexl("#e2d8c4"), sstep(0.3, 0.75, low))      # areia de cal clara, pouco saturada
        base = mix(base, hexl("#b8a888"), sstep(0.55, 0.9, mid) * 0.6)
        base = base * (0.95 + 0.06 * rip[..., None]) * (0.94 + 0.1 * fine[..., None])
        pc = mix(hexl("#8a8174"), hexl("#d8d2c6"), pid); pc = mix(pc, hexl("#6a5a48"), (pid > 0.8))
        col = mix(base, pc, sstep(0.05, 0.25, peb))
        col = mix(col, hexl("#f4efe6"), sstep(0.05, 0.3, sh) * 0.9)           # conchas brancas
        col = mix(col, hexl("#a89a80"), sstep(0.0, 0.08, peb) * (1 - sstep(0.08, 0.25, peb)) * 0.6)   # sombra de contato
        rough = 0.9 - 0.15 * sstep(0.05, 0.3, sh)
        save(out, "ter_areia", col, hgt, 3.5, rough)
    else:
        base = mix(hexl("#7a6e5a"), hexl("#958a74"), sstep(0.3, 0.75, low)) * (0.92 + 0.1 * fine[..., None])
        pc = mix(hexl("#4e4840"), hexl("#a29a8c"), pid)
        col = mix(base, pc, sstep(0.05, 0.25, peb))
        poca = sstep(0.62, 0.7, fnoise(1.7, 21))
        col = mix(col, hexl("#5a5448"), poca * 0.6)
        rough = 0.45 - 0.35 * poca + 0.15 * sstep(0.05, 0.3, peb)
        save(out, "ter_areia_molhada", col, hgt * (1 - 0.7 * poca), 2.5, rough)

def cal(out):
    """afloramentos de calcario em placas irregulares no meio da areia (so em manchas), rachaduras tortas"""
    r = np.random.default_rng(31); M = 40
    pts = r.uniform(0, 1, (M, 2))
    small = 1024; uu = np.arange(small) / small; UU, VV = np.meshgrid(uu, uu, indexing="xy")
    wsm = lambda a: np.array(Image.fromarray(a.astype(np.float32), mode="F").resize((small, small), Image.BILINEAR))
    wx = wsm(fnoise(1.6, 41) - 0.5) * 0.06; wy = wsm(fnoise(1.6, 42) - 0.5) * 0.06     # rachaduras tortas
    X, Y = UU + wx, VV + wy
    d1 = np.full((small, small), 9.0); d2 = np.full((small, small), 9.0); cid = np.zeros((small, small))
    for i, (px, py) in enumerate(pts):
        for ox in (-1, 0, 1):
            for oy in (-1, 0, 1):
                d = np.sqrt((X - px - ox) ** 2 + (Y - py - oy) ** 2)
                m1 = d < d1; d2 = np.where(m1, d1, np.minimum(d2, d)); cid = np.where(m1, i / M, cid); d1 = np.where(m1, d, d1)
    up = lambda a: np.array(Image.fromarray(a.astype(np.float32), mode="F").resize((N, N), Image.BILINEAR))
    edge = up(d2 - d1); cid = up(cid)
    fine = fnoise(0.7, 33, 48); mid = fnoise(1.2, 34, 6); low = fnoise(1.7, 39)
    outcrop = sstep(0.12, 0.3, low)                         # quase tudo laje; a areia vem da mistura do jogo                        # onde ha laje; resto e areia com cascalho
    crack = 1 - sstep(0.002, 0.007, edge)
    slab = outcrop * sstep(0.0, 0.03, edge) * (0.75 + 0.25 * mid) * (1 - crack)
    peb, pid = blobs(2600, 2, 7, 36, 1.4)
    sandh = 0.3 * fnoise(1.2, 43, 8) + 0.6 * peb
    hgt = 1.4 * slab + 0.12 * fine * outcrop + (1 - outcrop) * sandh * 0.6
    stone = mix(hexl("#cfc8b8"), hexl("#ebe5d8"), cid) * (0.88 + 0.14 * fine[..., None])
    stone = mix(stone, hexl("#a8a090"), sstep(0.6, 0.95, mid) * 0.45)
    stone = mix(stone, hexl("#7a7466"), crack * outcrop)
    sand = mix(hexl("#c4b496"), hexl("#d8ccb2"), fnoise(1.3, 44, 3)) * (0.94 + 0.1 * fine[..., None])
    sand = mix(sand, mix(hexl("#8a8174"), hexl("#d0cabe"), pid), sstep(0.05, 0.25, peb))
    col = mix(sand, stone, outcrop * (1 - crack * 0.0) * sstep(0.0, 0.02, slab + crack * 0.02))
    grass = sstep(0.7, 0.82, fnoise(1.6, 37)) * (1 - slab)
    col = mix(col, mix(hexl("#7c7a44"), hexl("#a8a060"), fine), grass * 0.8)          # capim seco
    lich = sstep(0.68, 0.82, fnoise(1.1, 38, 6)) * slab
    col = mix(col, hexl("#b8ae70"), lich * 0.4)                                         # liquen amarelado
    rough = 0.9 - 0.08 * slab
    save(out, "ter_costa_cal", col, hgt, 6.0, rough)

if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    areia(out); areia(out, wet=True); cal(out)

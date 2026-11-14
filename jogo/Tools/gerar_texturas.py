#!/usr/bin/env python3
"""
Gera texturas simples e repetíveis (512 px) para o kit visual em Client/Assets/Art/gen_*.png.
Originais deste script (domínio público). Paleta do pacote visual v1 (carvão, ferro, osso, pinho, âmbar).
Rode de novo para recriar: python3 Tools/gerar_texturas.py
"""
import os
import numpy as np
from PIL import Image

N = 512
OUT = os.path.join(os.path.dirname(__file__), "..", "Client", "Assets", "Art")
rng = np.random.default_rng(3)


def tile_noise(scale, octaves=4):
    """Ruído de valor repetível (as bordas casam)."""
    out = np.zeros((N, N))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cells = scale * (2 ** o)
        grid = rng.random((cells, cells))
        xs = np.linspace(0, cells, N, endpoint=False)
        x0 = xs.astype(int); fx = xs - x0; x1 = (x0 + 1) % cells
        fx = fx * fx * (3 - 2 * fx)
        a = grid[x0][:, x0]; b = grid[x0][:, x1]; c = grid[x1][:, x0]; d = grid[x1][:, x1]
        top = a * (1 - fx)[None, :] + b * fx[None, :]
        bot = c * (1 - fx)[None, :] + d * fx[None, :]
        out += amp * (top * (1 - fx)[:, None] + bot * fx[:, None])
        total += amp; amp *= 0.5
    return out / total


def voronoi(count, jitter=0.85):
    """Distâncias F1, F2 e id da célula, repetível (para pedras de calçamento)."""
    pts = rng.random((count, 2)) * N
    yy, xx = np.mgrid[0:N, 0:N]
    f1 = np.full((N, N), 1e9); f2 = np.full((N, N), 1e9); cid = np.zeros((N, N), int)
    for i, (px, py) in enumerate(pts):
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                d = np.hypot(xx - (px + ox), yy - (py + oy))
                closer = d < f1
                f2 = np.where(closer, f1, np.minimum(f2, d))
                cid = np.where(closer, i, cid)
                f1 = np.where(closer, d, f1)
    return f1, f2, cid


def save(name, rgb):
    img = np.clip(rgb, 0, 255).astype(np.uint8)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(img, "RGB").save(os.path.join(OUT, name + ".png"))


def save_normal(name, height, strength):
    """Mapa de normais (convenção OpenGL, Y para cima, a do Godot) a partir de uma altura 0..1 repetível."""
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    nx = -dx * strength; ny = dy * strength; nz = np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    rgb = np.stack([nx / length, ny / length, nz / length], axis=-1) * 127.5 + 127.5
    save(name + "_n", rgb)


def blur(v, r=1):
    out = v.copy()
    for _ in range(r):
        out = (out + np.roll(out, 1, 0) + np.roll(out, -1, 0) + np.roll(out, 1, 1) + np.roll(out, -1, 1)) / 5.0
    return out


def colorize(v, c0, c1):
    c0 = np.array(c0, float); c1 = np.array(c1, float)
    return c0[None, None, :] * (1 - v[..., None]) + c1[None, None, :] * v[..., None]


def main():
    n = tile_noise(8)
    fine = tile_noise(32, 3)
    # calçamento da cidade: pedras escuras com juntas
    f1, f2, cid = voronoi(70)
    edge = np.clip((f2 - f1) / 6.0, 0, 1)
    tone = rng.random(70)[cid] * 0.35 + 0.55
    stone = colorize(np.clip(tone * (0.8 + 0.3 * fine), 0, 1), (38, 38, 44), (112, 108, 112))
    joint = np.array([22, 20, 22], float)
    cob = stone * edge[..., None] + joint * (1 - edge[..., None])
    save("gen_calcamento", cob)
    save_normal("gen_calcamento", blur(np.sqrt(edge) * (0.85 + 0.15 * fine), 2), 28)
    # grama de pinho (mata, campo)
    g = np.clip(0.55 * n + 0.45 * fine, 0, 1)
    grass = colorize(g, (34, 46, 36), (78, 92, 62))
    blades = (rng.random((N, N)) > 0.985)
    grass[blades] = grass[blades] * 1.35
    save("gen_grama", grass)
    save_normal("gen_grama", blur(g + blades * 0.4, 1), 10)
    # areia
    sand = colorize(np.clip(0.6 * n + 0.4 * fine, 0, 1), (120, 104, 80), (176, 156, 122))
    save("gen_areia", sand)
    save_normal("gen_areia", blur(0.6 * n + 0.4 * fine, 1), 6)
    # terra batida (caminhos de farm)
    dirt = colorize(np.clip(0.5 * n + 0.5 * fine, 0, 1), (58, 45, 36), (104, 84, 64))
    pebbles = rng.random((N, N)) > 0.992
    dirt[pebbles] = np.array([130, 120, 108])
    save("gen_terra", dirt)
    save_normal("gen_terra", blur(0.5 * n + 0.5 * fine + pebbles * 0.6, 1), 10)
    # telhado de ardósia: fiadas de placas
    yy, xx = np.mgrid[0:N, 0:N]
    row = (yy // 32)
    shift = (row % 2) * 24
    col = ((xx + shift) // 48)
    plate_tone = (np.sin(row * 12.9898 + col * 78.233) * 43758.5453) % 1
    slate = colorize(np.clip(0.4 + plate_tone * 0.35 + (fine - 0.5) * 0.3, 0, 1), (36, 34, 42), (88, 84, 96))
    gaps = ((yy % 32) < 3) | (((xx + shift) % 48) < 2)
    slate[gaps] = np.array([20, 18, 22])
    save("gen_ardosia", slate)
    save_normal("gen_ardosia", blur(np.where(gaps, 0.0, 0.6 + (yy % 32) / 32.0 * 0.4), 1), 14)
    # tábuas de madeira escura
    plank = (xx // 64)
    grain = np.sin(yy * 0.08 + plank * 3 + tile_noise(16, 2) * 9) * 0.5 + 0.5
    wood = colorize(np.clip(0.3 + 0.4 * grain + (rng.random(N)[plank] - 0.5) * 0.3, 0, 1), (44, 30, 22), (96, 68, 46))
    wood[(xx % 64) < 3] = np.array([24, 16, 12])
    save("gen_madeira", wood)
    save_normal("gen_madeira", blur(np.where((xx % 64) < 3, 0.0, 0.7 + 0.3 * grain), 1), 10)
    # reboco claro (paredes de casa)
    plaster = colorize(np.clip(0.6 * n + 0.4 * fine, 0, 1), (130, 118, 100), (186, 172, 148))
    save("gen_reboco", plaster)
    save_normal("gen_reboco", blur(0.6 * n + 0.4 * fine, 1), 5)
    # pedra de muralha (blocos)
    brow = yy // 64
    bcol = (xx + (brow % 2) * 64) // 128
    btone = (np.sin(brow * 7.1 + bcol * 13.7) * 999.1) % 1
    block = colorize(np.clip(0.35 + btone * 0.35 + (fine - 0.5) * 0.4, 0, 1), (44, 44, 50), (104, 102, 108))
    block[((yy % 64) < 4) | (((xx + (brow % 2) * 64) % 128) < 4)] = np.array([26, 25, 28])
    save("gen_bloco", block)
    mortar = ((yy % 64) < 4) | (((xx + (brow % 2) * 64) % 128) < 4)
    save_normal("gen_bloco", blur(np.where(mortar, 0.0, 0.75 + 0.25 * fine), 2), 18)
    print("texturas geradas em", os.path.abspath(OUT))



def criaturas():
    """Texturas de bicho (09/11, "monstros no nível do boneco"): quitina pintalgada com anéis de crescimento, pele com
    poros e dobras, couro escamado. Em cinza claro: o material de cada criatura dá a cor (tinta × textura)."""
    global rng
    rng = np.random.default_rng(11)
    yy, xx = np.mgrid[0:N, 0:N]
    # quitina: placas (voronoi) com anéis concêntricos, pintas escuras e borda gasta
    f1, f2, cid = voronoi(26)
    edge = np.clip((f2 - f1) / 10.0, 0, 1)
    rings = 0.5 + 0.5 * np.sin(f1 / 4.5)
    spots = (tile_noise(24, 2) > 0.68).astype(float)
    fine = tile_noise(48, 2)
    v = np.clip(0.62 + 0.18 * rings * edge - 0.35 * spots + 0.15 * (fine - 0.5) - 0.35 * (1 - edge), 0, 1)
    save("gen_quitina", colorize(v, (70, 66, 60), (235, 230, 220)))
    save_normal("gen_quitina", blur(0.6 * edge + 0.25 * rings + 0.15 * fine, 1), 9)
    # pele: poros finos, manchas suaves e dobras
    n = tile_noise(6); pores = tile_noise(96, 1); folds = np.abs(np.sin((tile_noise(4) * 9.0)))
    v = np.clip(0.72 + 0.18 * (n - 0.5) - 0.12 * (pores > 0.72) - 0.12 * (folds < 0.12), 0, 1)
    save("gen_pele", colorize(v, (120, 110, 104), (240, 236, 232)))
    save_normal("gen_pele", blur(0.5 * n + 0.3 * pores + 0.2 * folds, 1), 5)
    # escamas: losangos sobrepostos
    u = (xx / 32.0); w = (yy / 32.0) + (np.floor(u) % 2) * 0.5
    sc = np.hypot(u % 1 - 0.5, (w % 1) - 0.3)
    v = np.clip(0.85 - sc * 0.9 + 0.12 * (tile_noise(16, 2) - 0.5), 0, 1)
    save("gen_escama", colorize(v, (80, 78, 72), (232, 228, 220)))
    save_normal("gen_escama", blur(1 - sc, 1), 10)
    print("texturas de criatura geradas")


if __name__ == "__main__":
    import sys
    criaturas() if "--criaturas" in sys.argv else main()

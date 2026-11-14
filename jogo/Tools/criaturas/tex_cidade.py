"""Texturas de parede/chao da cidade (1024, emendam): mesmos nomes das atuais, entram sem codigo.
gen_reboco (reboco caiado com manchas, rachaduras e tijolo aparecendo), gen_bloco (alvenaria de pedra com argamassa e musgo),
gen_madeira (tabuas com veio, nos e pregos), gen_calcamento (pedras irregulares com junta e marcas de agua). Uso: python3 tex_cidade.py <saida>"""
import sys, os, numpy as np
sys.path.insert(0, "/home/claude/cinzas/tools")
import tex_costa as T
from PIL import Image
T.N = N = 1024
u = np.arange(N) / N; U, V = np.meshgrid(u, u, indexing="xy")
fn, mix, ss, hx = T.fnoise, T.mix, T.sstep, T.hexl

def save(out, name, col, hgt, strength):
    Image.fromarray((T.lin2srgb(col) * 255).astype(np.uint8)).save(os.path.join(out, name + ".png"), optimize=True)
    Image.fromarray(T.normal_from(hgt, strength)).save(os.path.join(out, name + "_n.png"), optimize=True)
    print("ok", name)

def bricks(rows, cols, mortar, jitter, seed, offset=0.5):
    """tijolos/blocos periodicos: devolve (altura do bloco 0..1, id, junta)"""
    r = np.random.default_rng(seed)
    row = np.floor(V * rows).astype(int); fy = V * rows - row
    shift = (row % 2) * offset + r.uniform(0, jitter, rows)[row % rows]
    x = U * cols + shift; col = np.floor(x).astype(int); fx = x - col
    ids = r.uniform(0, 1, (rows, cols + 2))[row % rows, col % cols]
    edge = np.minimum(np.minimum(fx, 1 - fx) / cols * rows, np.minimum(fy, 1 - fy))
    joint = 1 - ss(mortar * 0.5, mortar, edge)
    bevel = ss(0.0, mortar * 2.5, edge)
    return bevel, ids, joint

def reboco(out):
    low = fn(1.5, 51); mid = fn(1.1, 52, 6); fine = fn(0.6, 53, 64)
    expo = ss(0.66, 0.72, fn(1.7, 54))                                     # reboco caido -> tijolo aparece
    bb, bid, bj = bricks(16, 8, 0.06, 0.2, 55)
    crack = (np.abs(fn(1.4, 56, 8) - 0.5) < 0.004).astype(float) * (1 - expo)
    hgt = 0.25 * low + 0.2 * mid + 0.12 * fine + (1 - expo) * 0.4 - expo * (0.3 - 0.3 * bb) - crack * 0.3
    cal = mix(hx("#d8cdb6"), hx("#ece4d0"), low) * (0.9 + 0.12 * fine[..., None])
    cal = mix(cal, hx("#a89a7e"), ss(0.55, 0.85, mid) * 0.5)                # manchas de umidade
    drip = ss(0.55, 0.8, fn(1.2, 57, 3)) * ss(0.25, 0.0, np.abs(np.sin(U * 24 * np.pi + 4 * fn(1.6, 58))))
    cal = mix(cal, hx("#8a7a62"), drip * 0.5)                               # escorridos
    brick = mix(hx("#8a4a32"), hx("#b06a48"), bid) * (0.85 + 0.2 * fine[..., None])
    brick = mix(brick, hx("#cfc4ae"), bj)
    col = mix(cal, brick, expo)
    col = mix(col, hx("#5a4e40"), crack)
    save(out, "gen_reboco", col, hgt, 4.0)

def bloco(out):
    bb, bid, bj = bricks(5, 2, 0.045, 0.6, 61, offset=0.37)
    fine = fn(0.7, 62, 48); mid = fn(1.2, 63, 6); low = fn(1.6, 64)
    chip = ss(0.6, 0.75, fn(1.0, 65, 16))
    hgt = 0.8 * bb * (0.8 + 0.2 * mid) + 0.15 * fine - 0.25 * chip * bb
    st = mix(hx("#7a6e60"), hx("#b0a48e"), bid) * (0.8 + 0.3 * fine[..., None]) * (0.85 + 0.25 * fn(1.3, 66, 2)[..., None])
    st = mix(st, hx("#5e5850"), ss(0.55, 0.9, mid) * 0.5)
    st = mix(st, hx("#c8c0b0"), (1 - bb) * 0.5 * (1 - bj))                  # quinas gastas mais claras
    moss = ss(0.62, 0.8, low) * (0.4 + 0.6 * bj)
    col = mix(st, hx("#6a6458"), bj)
    col = mix(col, hx("#4e6a2e"), moss * 0.8)
    save(out, "gen_bloco", col, hgt, 5.0)

def madeira(out):
    r = np.random.default_rng(71); planks = 6
    p = np.floor(U * planks).astype(int); fx = U * planks - p
    off = r.uniform(0, 1, planks)[p]
    grain = np.sin((V + off) * np.pi * 2 * 3 + 6 * fn(1.6, 72, 2) + fx * 2) * 0.5 + 0.5
    rings = np.sin(2 * np.pi * (fx * 7 + 2.5 * fn(1.4, 73, 3))) * 0.5 + 0.5
    fine = fn(0.5, 74, 64)
    gap = 1 - ss(0.0, 0.03, np.minimum(fx, 1 - fx))
    knots, kid = T.blobs(18, 8, 18, 75, 1.6) if False else (np.zeros((N, N)), None)
    nails = np.zeros((N, N))
    for k in range(planks):
        for vv in (0.12, 0.62):
            cx, cy = int((k + 0.5) / planks * N), int(vv * N)
            yy, xx = np.ogrid[-N // 2:N // 2, -N // 2:N // 2]
            d = np.sqrt(((xx + N // 2 - cx + N // 2) % N - N // 2) ** 2 + ((yy + N // 2 - cy + N // 2) % N - N // 2) ** 2)
            nails = np.maximum(nails, (d < 5).astype(float))
    tone = r.uniform(0.75, 1.15, planks)[p][..., None]
    w = mix(hx("#5a3a22"), hx("#8a6040"), 0.4 * grain + 0.4 * rings + 0.2 * fine) * tone
    w = mix(w, hx("#2a1a10"), gap)
    w = mix(w, hx("#3a3a3a"), nails)
    w = mix(w, hx("#7a7468"), ss(0.65, 0.85, fn(1.5, 76)) * 0.35)           # madeira acinzentada pelo tempo
    hgt = 0.3 * grain + 0.15 * rings + 0.1 * fine - 0.8 * gap + 0.2 * nails
    save(out, "gen_madeira", w, hgt, 4.0)

def calcamento(out):
    # pedras irregulares: voronoi periodico com junta
    r = np.random.default_rng(81); M = 90; pts = []
    while len(pts) < M:                       # pontos com distancia minima (sem pedras coladas -> sem cunhas)
        c = r.uniform(0, 1, 2)
        if all(min(abs(c[0] - q[0]), 1 - abs(c[0] - q[0])) ** 2 + min(abs(c[1] - q[1]), 1 - abs(c[1] - q[1])) ** 2 > 0.075 ** 2 for q in pts): pts.append(c)
    pts = np.array(pts)
    d1 = np.full((N, N), 9.0); d2 = np.full((N, N), 9.0); cid = np.zeros((N, N))
    wx = (fn(1.6, 82) - 0.5) * 0.02; wy = (fn(1.6, 83) - 0.5) * 0.02
    X, Y = U + wx, V + wy
    for i, (px, py) in enumerate(pts):
        for ox in (-1, 0, 1):
            for oy in (-1, 0, 1):
                d = np.sqrt((X - px - ox) ** 2 + (Y - py - oy) ** 2)
                m1 = d < d1; d2 = np.where(m1, d1, np.minimum(d2, d)); cid = np.where(m1, i / M, cid); d1 = np.where(m1, d, d1)
    e = d2 - d1
    joint = 1 - ss(0.006, 0.016, e)
    dome = ss(0.0, 0.05, e)
    fine = fn(0.6, 84, 48); low = fn(1.6, 85)
    hgt = 0.9 * dome + 0.12 * fine
    st = mix(hx("#6e6860"), hx("#a09888"), cid) * (0.85 + 0.2 * fine[..., None])
    st = mix(st, hx("#c0b8a8"), ss(0.02, 0.06, e) * 0.25)
    wet = ss(0.62, 0.72, low)
    col = mix(st, hx("#3e3a34"), joint)
    col = mix(col, col * 0.7, wet)                                          # manchas de agua (mais escuras)
    col = mix(col, hx("#5a6a3a"), joint * ss(0.4, 0.7, fn(1.3, 86, 4)) * 0.6)   # mato nas juntas
    save(out, "gen_calcamento", col, hgt, 5.0)

if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    reboco(out); bloco(out); madeira(out); calcamento(out)

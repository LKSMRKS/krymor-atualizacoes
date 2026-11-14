"""Sons sintetizados do Cinzas da Mare (sem download): passos, golpes, arco, magia, interface, ambiente em loop e musica calma.
Saida: <dir>/<categoria>/<nome>.ogg (44,1 kHz, mono; ambiente/musica em estereo, loop sem emenda). Uso: python3 sons_sintese.py <dir>"""
import sys, os, subprocess, numpy as np
from scipy import signal
OUT = sys.argv[1]; SR = 44100
rng = np.random.default_rng(7)

def t(d): return np.arange(int(SR * d)) / SR
def noise(d): return rng.standard_normal(int(SR * d))
def bp(x, lo, hi, o=2): return signal.sosfilt(signal.butter(o, [lo, hi], 'bandpass', fs=SR, output='sos'), x)
def lp(x, f, o=2): return signal.sosfilt(signal.butter(o, f, 'lowpass', fs=SR, output='sos'), x)
def hp(x, f, o=2): return signal.sosfilt(signal.butter(o, f, 'highpass', fs=SR, output='sos'), x)
def env(n, a, d, curve=4.0):
    e = np.ones(n); na = max(1, int(SR * a)); e[:na] = np.linspace(0, 1, na)
    rest = n - na; e[na:] = np.exp(-curve * np.linspace(0, 1, rest) * (rest / SR) / max(d, 1e-3)); return e
def norm(x, peak=0.85): m = np.max(np.abs(x)) or 1; return x / m * peak
def reverb(x, size=0.35, mix=0.25):
    ir_len = int(SR * size * 3); ir = noise(ir_len / SR) * np.exp(-np.linspace(0, 6, ir_len)); ir = lp(ir, 6000)
    w = signal.fftconvolve(x, ir)[:len(x) + ir_len // 2]; y = np.zeros(len(w)); y[:len(x)] = x
    return y * (1 - mix) + norm(w, np.max(np.abs(x))) * mix
def save(cat, name, x, stereo=None):
    os.makedirs(f"{OUT}/{cat}", exist_ok=True); wav = f"{OUT}/{cat}/_{name}.wav"
    from scipy.io import wavfile
    if stereo is not None: data = np.stack([x, stereo], 1)
    else: data = x
    wavfile.write(wav, SR, (np.clip(data, -1, 1) * 32767).astype(np.int16))
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", "5", f"{OUT}/{cat}/{name}.ogg"], check=True); os.remove(wav)

# ------------------------------------------------------------------ passos
def passo(kind, k):
    d = 0.22
    if kind == "areia":
        x = bp(noise(d), 300, 4500) * env(int(SR * d), 0.01, 0.08, 3) + 0.4 * bp(noise(d), 2000, 9000) * env(int(SR * d), 0.02, 0.05, 6)
    elif kind == "pedra":
        x = bp(noise(d), 600 + 80 * k, 5000) * env(int(SR * d), 0.002, 0.04, 8); x += 0.6 * lp(noise(d), 300) * env(int(SR * d), 0.002, 0.03, 10)
    else:   # madeira
        f0 = 140 + 15 * k; x = np.sin(2 * np.pi * f0 * t(d)) * env(int(SR * d), 0.002, 0.06, 7) + 0.5 * bp(noise(d), 800, 3000) * env(int(SR * d), 0.001, 0.02, 10)
    return norm(reverb(x, 0.15, 0.12), 0.6)
for kind in ("areia", "pedra", "madeira"):
    for k in range(4): save("passos", f"passo_{kind}_{k+1}", passo(kind, k))

# ------------------------------------------------------------------ golpes
def whoosh(d=0.32, f0=400, f1=2400):
    n = noise(d); n_ = len(n); out = np.zeros(n_)
    for i, (a, b) in enumerate(zip(np.linspace(f0, f1, 12), np.linspace(f0 * 1.6, f1 * 1.6, 12))):
        seg = slice(i * n_ // 12, (i + 1) * n_ // 12); out[seg] = bp(n, a, min(b, 18000))[seg]
    return out * np.sin(np.linspace(0, np.pi, n_)) ** 2
def metal(d=0.6, base=1800):
    tt = t(d); x = sum(np.sin(2 * np.pi * base * r * tt + p) * a for r, a, p in ((1, 1, 0), (2.76, 0.6, 1), (5.4, 0.4, 2), (8.9, 0.25, 3)))
    return x * env(len(tt), 0.001, 0.25, 5)
def thud(d=0.35, f=90):
    tt = t(d); fr = f * (1 + 1.5 * np.exp(-tt * 30)); return np.sin(2 * np.pi * np.cumsum(fr) / SR) * env(len(tt), 0.002, 0.12, 6)
save("golpes", "espada_corte", norm(reverb(whoosh(0.3, 500, 3500), 0.2, 0.15), 0.7))
save("golpes", "espada_pesada", norm(reverb(whoosh(0.45, 250, 1800), 0.25, 0.2), 0.75))
x = np.concatenate([np.zeros(int(SR * 0.0)), metal(0.7, 1650)]); x[:len(noise(0.03))] += bp(noise(0.03), 2000, 9000) * 2
save("golpes", "impacto_metal", norm(reverb(x, 0.3, 0.25), 0.8))
x = thud(0.3, 80) + 0.6 * lp(noise(0.3), 900) * env(int(SR * 0.3), 0.001, 0.05, 8)
save("golpes", "impacto_carne", norm(x, 0.8))
x = thud(0.3, 160) + 0.7 * bp(noise(0.3), 500, 2500) * env(int(SR * 0.3), 0.001, 0.04, 9)
save("golpes", "impacto_madeira", norm(reverb(x, 0.15, 0.1), 0.8))
x = thud(0.6, 55) * 1.3 + 0.5 * lp(noise(0.6), 400) * env(int(SR * 0.6), 0.003, 0.25, 4)
save("golpes", "impacto_pesado", norm(reverb(x, 0.4, 0.2), 0.9))
x = bp(noise(0.25), 1500, 6000) * env(int(SR * 0.25), 0.001, 0.05, 6) + 0.6 * thud(0.25, 220)
save("golpes", "impacto_casco_caranguejo", norm(x, 0.75))
x = thud(0.5, 70) + bp(noise(0.5), 200, 2000) * env(int(SR * 0.5), 0.005, 0.2, 4) * 0.8
save("golpes", "esquiva_rolamento", norm(lp(x, 2500), 0.6))

# ------------------------------------------------------------------ arco
tt = t(0.5); tw = np.sin(2 * np.pi * 110 * tt) * env(len(tt), 0.001, 0.12, 7) + 0.4 * np.sin(2 * np.pi * 220 * tt) * env(len(tt), 0.001, 0.08, 9)
save("arco", "arco_disparo", norm(tw + 0.5 * whoosh(0.5, 1500, 5000), 0.75))
save("arco", "flecha_voando", norm(whoosh(0.6, 2000, 7000) * 0.8, 0.5))
x = bp(noise(0.2), 1000, 5000) * env(int(SR * 0.2), 0.001, 0.03, 9) + 0.5 * thud(0.2, 300)
save("arco", "flecha_acerto", norm(x, 0.75))
x = sum(bp(noise(0.08), 3000, 8000) * env(int(SR * 0.08), 0.001, 0.02, 8) for _ in range(1)); x = np.concatenate([x, np.zeros(int(SR * 0.05)), x * 0.7])
save("arco", "pegar_flecha_aljava", norm(x, 0.5))

# ------------------------------------------------------------------ magia
tt = t(0.6); sp = sum(np.sin(2 * np.pi * f * tt * (1 + 0.3 * tt)) for f in (880, 1320, 1760)) * env(len(tt), 0.005, 0.25, 5)
save("magia", "faisca", norm(reverb(sp + 0.3 * bp(noise(0.6), 4000, 12000) * env(len(tt), 0.001, 0.1, 6), 0.4, 0.35), 0.7))
x = lp(noise(1.2), 1200) * env(int(SR * 1.2), 0.02, 0.5, 3) + 0.8 * thud(1.2, 45)
save("magia", "explosao_fogo", norm(reverb(x, 0.6, 0.3), 0.9))
tt = t(0.9); ice = sum(np.sin(2 * np.pi * f * tt) * env(len(tt), 0.001, 0.3 + 0.1 * i, 6) for i, f in enumerate((2400, 3100, 3900, 5200)))
save("magia", "gelo", norm(reverb(ice + 0.4 * hp(noise(0.9), 6000) * env(len(tt), 0.001, 0.08, 9), 0.5, 0.4), 0.7))
tt = t(1.4); cura = sum(np.sin(2 * np.pi * f * tt) * np.sin(np.pi * np.clip(tt / 1.4, 0, 1)) for f in (523.25, 659.25, 783.99, 1046.5))
save("magia", "cura", norm(reverb(cura, 0.8, 0.45), 0.6))
tt = t(1.0); voidx = np.sin(2 * np.pi * 70 * tt + 6 * np.sin(2 * np.pi * 3 * tt)) * np.sin(np.pi * tt) + 0.4 * lp(noise(1.0), 500)
save("magia", "vazio", norm(reverb(voidx, 0.7, 0.4), 0.75))

# ------------------------------------------------------------------ interface
tt = t(0.08); save("interface", "clique", norm(np.sin(2 * np.pi * 1800 * tt) * env(len(tt), 0.001, 0.02, 10) + 0.3 * bp(noise(0.08), 2000, 8000) * env(len(tt), 0.001, 0.01, 12), 0.5))
x = bp(noise(0.35), 300, 3000) * env(int(SR * 0.35), 0.01, 0.12, 4); save("interface", "abrir_painel", norm(x, 0.45))
coins = np.zeros(int(SR * 0.7))
for k in range(5):
    s = int(SR * (0.03 + k * 0.09 + rng.uniform(0, 0.03))); c = metal(0.3, 2600 + rng.uniform(-300, 300)) * (0.9 - k * 0.12); coins[s:s + len(c)] += c[:len(coins) - s]
save("interface", "moedas", norm(reverb(coins, 0.2, 0.15), 0.6))
tt = t(0.25); save("interface", "pegar_item", norm(np.sin(2 * np.pi * (700 + 900 * tt / 0.25) * tt) * env(len(tt), 0.005, 0.1, 5), 0.5))
tt = t(1.6); lvl = sum(np.sin(2 * np.pi * f * tt) * (tt > d0) * np.exp(-np.clip(tt - d0, 0, None) * 2.5) for f, d0 in ((523.25, 0), (659.25, 0.12), (783.99, 0.24), (1046.5, 0.36)))
save("interface", "subir_nivel", norm(reverb(lvl, 0.6, 0.35), 0.7))
tt = t(0.5); save("interface", "missao_nova", norm(reverb(sum(np.sin(2 * np.pi * f * tt) * (tt > d0) * np.exp(-np.clip(tt - d0, 0, None) * 4) for f, d0 in ((659.25, 0), (987.77, 0.1))), 0.4, 0.3), 0.6))
tt = t(0.3); save("interface", "erro", norm(np.sign(np.sin(2 * np.pi * 140 * tt)) * env(len(tt), 0.005, 0.12, 5) * 0.5, 0.35))

# ------------------------------------------------------------------ ambiente (loops sem emenda: cruzamento das pontas)
def loopify(x, fade=2.0):
    n = int(SR * fade); a = x[:n] * np.linspace(0, 1, n); b = x[-n:] * np.linspace(1, 0, n); y = x[n:].copy(); y[-n:] = b + a; return y
def ondas(d=24):
    tt = t(d); x = np.zeros(len(tt)); pos = 0.0
    while pos < d:
        dur = rng.uniform(4.5, 7.5); s = int(pos * SR); n = int(dur * SR); seg = noise(dur)
        shape = np.sin(np.linspace(0, np.pi, n)) ** 1.5 * np.exp(-np.linspace(0, 2.5, n)) * rng.uniform(0.7, 1.0)
        w = lp(seg, 900) * shape + 0.5 * bp(seg, 1500, 7000) * shape ** 2
        x[s:s + n] += w[:max(0, len(x) - s)]; pos += dur * rng.uniform(0.55, 0.8)
    return x + 0.15 * lp(noise(d), 300)
L, R = ondas(), ondas(); save("ambiente", "ambiente_mar_ondas_loop", norm(loopify(L), 0.6), norm(loopify(R), 0.6))
def vento(d=20):
    n = noise(d); g = lp(noise(d), 0.4, 1); g = (g - g.min()) / (g.max() - g.min()); return bp(n, 200, 1200) * (0.3 + 0.7 * g) + 0.3 * bp(n, 1800, 3500) * g ** 2
save("ambiente", "ambiente_vento_loop", norm(loopify(vento()), 0.5), norm(loopify(vento()), 0.5))
def fogo(d=12):
    x = lp(noise(d), 600) * 0.3
    for _ in range(int(d * 14)):
        s = int(rng.uniform(0, d) * SR); c = bp(noise(0.03), 1500, 7000) * env(int(SR * 0.03), 0.0005, 0.01, 10) * rng.uniform(0.2, 1); x[s:s + len(c)] += c[:max(0, len(x) - s)]
    return x
save("ambiente", "ambiente_fogueira_loop", norm(loopify(fogo(), 1.0), 0.55), norm(loopify(fogo(), 1.0), 0.55))
def caverna(d=20):
    x = lp(noise(d), 200) * 0.25
    for _ in range(int(d * 0.8)):
        s = int(rng.uniform(0, d - 1) * SR); f = rng.uniform(900, 1600); tt_ = t(0.4)
        dr = np.sin(2 * np.pi * f * tt_ * (1 + 0.6 * tt_)) * env(len(tt_), 0.001, 0.06, 8); x[s:s + len(dr)] += dr * rng.uniform(0.3, 0.8)
    return reverb(x, 1.2, 0.5)[:int(SR * d)]
save("ambiente", "ambiente_caverna_gotejar_loop", norm(loopify(caverna()), 0.55), norm(loopify(caverna()), 0.55))
def gaivota():
    tt = t(0.55); f = 1500 + 900 * np.sin(np.pi * tt / 0.55) ** 0.5 - 600 * tt
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * env(len(tt), 0.02, 0.2, 4) * (1 + 0.3 * np.sin(2 * np.pi * 30 * tt))
g = np.concatenate([gaivota(), np.zeros(int(SR * 0.12)), gaivota() * 0.8, np.zeros(int(SR * 0.1)), gaivota() * 0.6])
save("ambiente", "gaivota", norm(reverb(bp(g, 600, 6000), 0.5, 0.3), 0.5))

# ------------------------------------------------------------------ musica calma do Porto (loop de 64 s)
BPM = 60; beat = 60 / BPM; bars = 16; D = bars * 4 * beat
tt = t(D); L = np.zeros(len(tt)); R = np.zeros(len(tt))
def nota(m): return 440 * 2 ** ((m - 69) / 12)
prog = [(50, [50, 57, 62, 65]), (46, [46, 53, 58, 62]), (41, [41, 48, 53, 57]), (48, [48, 55, 60, 64])]   # Dm Bb F C
for b in range(bars):
    root, ch = prog[b % 4]; s = int(b * 4 * beat * SR); n = int(4 * beat * SR); tb = np.arange(n) / SR
    pad = sum(np.sin(2 * np.pi * nota(m) * tb + 0.3 * np.sin(2 * np.pi * 0.3 * tb)) * (0.5 if m == ch[0] else 0.25) for m in ch)
    pad *= np.minimum(1, tb / 1.2) * np.minimum(1, (4 * beat - tb) / 1.2)
    L[s:s + n] += pad * 0.35; R[s:s + n] += pad * 0.35
    for k in range(8):              # dedilhado de alaude (corda pinçada)
        m = ch[[0, 2, 1, 3, 2, 1, 3, 2][k]] + 12; st = s + int(k * beat / 2 * SR); nn = int(1.5 * SR); tk = np.arange(nn) / SR
        f = nota(m); pl = (np.sin(2 * np.pi * f * tk) + 0.5 * np.sin(4 * np.pi * f * tk) + 0.25 * np.sin(6 * np.pi * f * tk)) * np.exp(-tk * 3.2)
        pan = 0.35 + 0.3 * (k % 2); e = min(len(L), st + nn) - st
        L[st:st + e] += pl[:e] * 0.18 * (1 - pan); R[st:st + e] += pl[:e] * 0.18 * pan
L = reverb(L, 1.0, 0.35)[:len(tt)]; R = reverb(R, 1.0, 0.35)[:len(tt)]
save("musica", "musica_porto_calma_loop", norm(lp(loopify(L, 3.0), 7000), 0.6), norm(lp(loopify(R, 3.0), 7000), 0.6))
print("ok sons")

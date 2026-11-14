#!/usr/bin/env python3
"""
Gera os efeitos sonoros do jogo por síntese (numpy) em Client/Assets/Sounds/*.wav.
Sons 100% originais deste script: domínio público (CC0), sem licença de terceiros.
Rode de novo para recriar: python3 Tools/gerar_sons.py
"""
import os, sys, wave
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np

SR = 22050
OUT = os.path.join(os.path.dirname(__file__), "..", "Client", "Assets", "Sounds")
rng = np.random.default_rng(7)


def t(d): return np.arange(int(SR * d)) / SR


def env(n, a=0.005, d=0.1, s=0.0, r=0.05, total=None):
    """Envelope ADSR simples (segundos)."""
    total = total or n / SR
    tt = np.arange(n) / SR
    e = np.zeros(n)
    for i, x in enumerate(tt):
        if x < a: e[i] = x / a
        elif x < a + d: e[i] = 1 - (1 - s) * (x - a) / d
        elif x < total - r: e[i] = s
        else: e[i] = s * max(0.0, (total - x) / r)
    return e


def fast_env(n, attack=0.004, decay=6.0):
    tt = np.arange(n) / SR
    return np.minimum(1, tt / attack) * np.exp(-tt * decay)


def tone(freq, d, kind="sine"):
    tt = t(d)
    if callable(freq): ph = 2 * np.pi * np.cumsum(freq(tt)) / SR
    else: ph = 2 * np.pi * freq * tt
    if kind == "sine": return np.sin(ph)
    if kind == "square": return np.sign(np.sin(ph)) * 0.6
    if kind == "saw": return ((ph / (2 * np.pi)) % 1) * 2 - 1
    if kind == "tri": return 2 * np.abs(((ph / (2 * np.pi)) % 1) * 2 - 1) - 1


def noise(d): return rng.uniform(-1, 1, int(SR * d))


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x); prev = 0.0
    for i, v in enumerate(x):
        prev = (1 - a) * v + a * prev; y[i] = prev
    return y


def highpass(x, cutoff): return x - lowpass(x, cutoff)


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts: out[:len(p)] += p
    return out


def seq(*parts, gap=0.0):
    out = []
    for p in parts:
        out.append(p); out.append(np.zeros(int(SR * gap)))
    return np.concatenate(out)


def save(name, x, gain=0.8):
    x = np.asarray(x, dtype=float)
    peak = np.max(np.abs(x)) or 1
    x = x / peak * gain
    fade = min(200, len(x))
    x[-fade:] *= np.linspace(1, 0, fade)
    data = (x * 32767).astype(np.int16)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())


def bell(f, d=0.6, decay=5):
    tt = t(d)
    return (np.sin(2 * np.pi * f * tt) + 0.5 * np.sin(2 * np.pi * f * 2.76 * tt) + 0.25 * np.sin(2 * np.pi * f * 5.4 * tt)) * np.exp(-tt * decay)


def main():
    # ---- interface ----
    save("ui_abrir", tone(lambda x: 300 + 500 * x / 0.12, 0.12, "tri") * fast_env(int(SR * 0.12), decay=18) + 0.3 * lowpass(noise(0.12), 2500) * fast_env(int(SR * 0.12), decay=30), 0.5)
    save("ui_fechar", tone(lambda x: 700 - 450 * x / 0.11, 0.11, "tri") * fast_env(int(SR * 0.11), decay=20), 0.45)
    save("ui_clique", tone(1200, 0.04, "sine") * fast_env(int(SR * 0.04), decay=80), 0.4)
    save("ui_erro", seq(tone(220, 0.08, "square") * fast_env(int(SR * 0.08), decay=20), tone(180, 0.12, "square") * fast_env(int(SR * 0.12), decay=15), gap=0.02), 0.4)
    save("ui_aviso", seq(bell(880, 0.25, 9), bell(1320, 0.4, 7), gap=0.0), 0.45)
    # ---- moedas e loja ----
    save("moeda", mix(bell(2400, 0.25, 14), np.concatenate([np.zeros(int(SR * 0.06)), bell(3200, 0.3, 12)])), 0.55)
    save("comprar", seq(bell(1800, 0.12, 20), bell(2400, 0.12, 20), bell(3000, 0.35, 10)), 0.55)
    save("vender", seq(bell(2600, 0.12, 20), bell(2000, 0.3, 12)), 0.5)
    save("gastar", mix(lowpass(noise(0.18), 3000) * fast_env(int(SR * 0.18), decay=16), bell(1500, 0.2, 18) * 0.5), 0.5)
    # ---- itens ----
    save("pegar", seq(tone(lambda x: 500 + 900 * x / 0.1, 0.1, "sine") * fast_env(int(SR * 0.1), decay=15), bell(1600, 0.2, 14)), 0.5)
    save("equipar", mix(lowpass(noise(0.2), 1800) * fast_env(int(SR * 0.2), decay=18), bell(700, 0.3, 16) * 0.6, tone(160, 0.15) * fast_env(int(SR * 0.15), decay=25)), 0.6)
    save("pocao", mix(lowpass(noise(0.45), 900) * env(int(SR * 0.45), 0.05, 0.2, 0.4, 0.15), tone(lambda x: 300 + 200 * np.sin(x * 40), 0.45) * 0.3 * env(int(SR * 0.45), 0.05, 0.2, 0.4, 0.15)), 0.5)
    save("mana", mix(tone(lambda x: 400 + 800 * x / 0.5, 0.5, "sine") * env(int(SR * 0.5), 0.02, 0.2, 0.4, 0.2), bell(1760, 0.5, 6) * 0.4), 0.5)
    save("bau", mix(tone(lambda x: 90 + 30 * x, 0.5, "saw") * 0.3 * env(int(SR * 0.5), 0.02, 0.3, 0.2, 0.1), lowpass(noise(0.5), 600) * env(int(SR * 0.5), 0.01, 0.4, 0.1, 0.1), np.concatenate([np.zeros(int(SR * 0.35)), bell(1200, 0.5, 6) * 0.6])), 0.6)
    save("alavanca", mix(lowpass(noise(0.35), 1200) * env(int(SR * 0.35), 0.01, 0.3, 0.0, 0.05), tone(110, 0.35, "saw") * 0.3 * fast_env(int(SR * 0.35), decay=8)), 0.6)
    save("portal", mix(tone(lambda x: 200 + 300 * x, 0.9, "sine") * env(int(SR * 0.9), 0.2, 0.3, 0.5, 0.3), lowpass(noise(0.9), 1500) * 0.4 * env(int(SR * 0.9), 0.3, 0.3, 0.5, 0.3)), 0.5)
    # ---- combate ----
    save("golpe", highpass(noise(0.16), 900) * fast_env(int(SR * 0.16), attack=0.03, decay=20), 0.5)
    save("acerto", mix(lowpass(noise(0.12), 2500) * fast_env(int(SR * 0.12), decay=35), tone(lambda x: 220 - 120 * x / 0.12, 0.12) * fast_env(int(SR * 0.12), decay=30)), 0.65)
    save("dano", mix(lowpass(noise(0.22), 900) * fast_env(int(SR * 0.22), decay=18), tone(lambda x: 140 - 60 * x, 0.22, "saw") * 0.5 * fast_env(int(SR * 0.22), decay=14)), 0.75)
    save("bloqueio", mix(bell(520, 0.35, 12), bell(780, 0.3, 14) * 0.6, highpass(noise(0.05), 3000) * fast_env(int(SR * 0.05), decay=60)), 0.6)
    save("esquiva", highpass(noise(0.25), 600) * env(int(SR * 0.25), 0.06, 0.1, 0.3, 0.1) * np.linspace(0.6, 1, int(SR * 0.25)), 0.4)
    save("magia", mix(tone(lambda x: 600 + 900 * x / 0.25, 0.25, "tri") * fast_env(int(SR * 0.25), attack=0.02, decay=10), highpass(noise(0.25), 2500) * 0.4 * fast_env(int(SR * 0.25), decay=12)), 0.45)
    # Bruxa e Arqueiro
    save("cura", mix(bell(1320, 0.6, 5) * 0.5, bell(1980, 0.6, 6) * 0.35, tone(lambda x: 520 + 260 * x / 0.6, 0.6, "sine") * 0.4 * env(int(SR * 0.6), 0.08, 0.2, 0.4, 0.25)), 0.45)
    save("flecha", mix(highpass(noise(0.18), 1800) * fast_env(int(SR * 0.18), attack=0.005, decay=18), tone(lambda x: 180 - 80 * x / 0.06, 0.06, "tri") * fast_env(int(SR * 0.06), decay=40) * 0.6), 0.5)
    save("furtivo", lowpass(noise(0.5), 1200) * env(int(SR * 0.5), 0.15, 0.1, 0.3, 0.2) * np.linspace(1, 0.3, int(SR * 0.5)), 0.35)
    save("explosao", mix(lowpass(noise(0.8), 500) * fast_env(int(SR * 0.8), attack=0.005, decay=5), tone(lambda x: 90 - 50 * x, 0.8, "sine") * fast_env(int(SR * 0.8), decay=6)), 0.8)
    save("monstro_morre", mix(tone(lambda x: 300 - 220 * x / 0.4, 0.4, "saw") * 0.6 * fast_env(int(SR * 0.4), attack=0.01, decay=6), lowpass(noise(0.4), 700) * fast_env(int(SR * 0.4), decay=8)), 0.6)
    save("rugido", mix(lowpass(tone(lambda x: 85 + 25 * np.sin(x * 9), 1.2, "saw"), 900) * env(int(SR * 1.2), 0.15, 0.4, 0.6, 0.4), lowpass(noise(1.2), 400) * 0.5 * env(int(SR * 1.2), 0.1, 0.4, 0.6, 0.4)), 0.8)
    save("preparo", tone(lambda x: 260 + 260 * x / 0.35, 0.35, "square") * 0.4 * env(int(SR * 0.35), 0.05, 0.2, 0.6, 0.05), 0.35)
    save("morte", seq(tone(330, 0.3, "tri") * fast_env(int(SR * 0.3), decay=5), tone(262, 0.3, "tri") * fast_env(int(SR * 0.3), decay=5), tone(196, 0.8, "tri") * fast_env(int(SR * 0.8), decay=3)), 0.55)
    # ---- progresso ----
    save("nivel", seq(bell(523, 0.15, 10), bell(659, 0.15, 10), bell(784, 0.15, 10), bell(1047, 0.8, 4)), 0.6)
    save("missao_aceita", seq(bell(660, 0.15, 12), bell(990, 0.4, 6)), 0.5)
    save("missao_completa", seq(bell(784, 0.12, 12), bell(988, 0.12, 12), bell(1175, 0.12, 12), mix(bell(1568, 0.9, 3.5), bell(1175, 0.9, 3.5) * 0.6)), 0.6)
    # ---- ofícios ----
    save("minerar", mix(bell(1900, 0.25, 22), highpass(noise(0.08), 2000) * fast_env(int(SR * 0.08), decay=50), tone(300, 0.1) * fast_env(int(SR * 0.1), decay=40)), 0.6)
    save("forja", seq(mix(bell(1400, 0.3, 14), highpass(noise(0.05), 2500) * fast_env(int(SR * 0.05), decay=60)), mix(bell(1350, 0.3, 14), highpass(noise(0.05), 2500) * fast_env(int(SR * 0.05), decay=60)), gap=0.08), 0.6)
    bub = np.zeros(int(SR * 0.9))
    for k in range(9):
        st = int(SR * (0.05 + k * 0.09 + rng.uniform(0, 0.03)))
        b = tone(lambda x, f=rng.uniform(300, 600): f + 900 * x, 0.06) * fast_env(int(SR * 0.06), decay=50)
        bub[st:st + len(b)] += b[:len(bub) - st]
    save("caldeirao", mix(bub, lowpass(noise(0.9), 500) * 0.3), 0.5)
    save("plantar", mix(lowpass(noise(0.3), 700) * env(int(SR * 0.3), 0.02, 0.2, 0.2, 0.08), tone(180, 0.2) * fast_env(int(SR * 0.2), decay=20) * 0.5), 0.5)
    save("colher", seq(lowpass(noise(0.12), 1500) * fast_env(int(SR * 0.12), decay=25), bell(1300, 0.25, 12)), 0.5)
    save("montar", mix(lowpass(noise(0.5), 600) * env(int(SR * 0.5), 0.02, 0.3, 0.2, 0.1), tone(lambda x: 140 + 60 * np.sin(x * 30), 0.5, "saw") * 0.25 * env(int(SR * 0.5), 0.05, 0.3, 0.3, 0.1)), 0.55)
    save("asas", highpass(noise(0.45), 400) * env(int(SR * 0.45), 0.03, 0.1, 0.6, 0.25) * (0.6 + 0.4 * np.sin(t(0.45) * 2 * np.pi * 9)), 0.5)
    save("passo", lowpass(noise(0.07), 900) * fast_env(int(SR * 0.07), decay=45), 0.25)
    # ---- ambiente (laços de 8 s; começo e fim casam por cross-fade) ----
    def loop(x):
        n = len(x); f = int(SR * 0.5)
        y = x[:n - f].copy(); y[:f] = y[:f] * np.linspace(0, 1, f) + x[n - f:] * np.linspace(1, 0, f)
        return y
    d = 8.5
    wind = lowpass(noise(d), 400) * (0.6 + 0.4 * np.sin(t(d) * 2 * np.pi * 0.17)) + 0.3 * lowpass(noise(d), 1200) * (0.5 + 0.5 * np.sin(t(d) * 2 * np.pi * 0.11 + 1))
    save("amb_vento", loop(wind), 0.35)
    town = wind * 0.5
    for k in range(5):
        st = int(SR * rng.uniform(0.3, 7.5)); b = bell(rng.choice([1200, 1500, 1800]), 0.3, 18) * 0.25
        town[st:st + len(b)] += b[:len(town) - st]
    save("amb_cidade", loop(town), 0.3)
    drone = lowpass(tone(55, d, "saw"), 200) * 0.6 + lowpass(noise(d), 250) * 0.5
    for k in range(6):
        st = int(SR * rng.uniform(0.3, 7.8)); b = bell(rng.uniform(1800, 2600), 0.2, 30) * 0.35
        drone[st:st + len(b)] += b[:len(drone) - st]
    save("amb_masmorra", loop(drone), 0.35)
    extras()
    print("sons gerados em", os.path.abspath(OUT))
    # ambiente por região, laços de objeto e o borbulhar do caldeirão (substitui o "caldeirao" acima)
    import gerar_ambiente
    gerar_ambiente.main()


def extras():
    """Ritual do drop, acerto por arma, passos por chão e vozes por família de criatura (depois do resto: não muda os antigos)."""
    r = np.random.default_rng(77)
    def nz(d): return r.uniform(-1, 1, int(SR * d))
    def fe(d, **k): return fast_env(int(SR * d), **k)
    # ---- drop ----
    save("drop_comum", mix(lowpass(nz(0.12), 1400) * fe(0.12, decay=30), tone(lambda x: 260 - 90 * x / 0.1, 0.1) * fe(0.1, decay=35) * 0.6), 0.45)
    save("drop_raro", mix(seq(bell(1175, 0.18, 14), bell(1568, 0.5, 6)), lowpass(nz(0.1), 1500) * fe(0.1, decay=35) * 0.5), 0.5)
    shimmer = sum(bell(f, 1.4, 2.6) * a for f, a in [(784, 0.5), (1175, 0.45), (1568, 0.4), (2349, 0.25), (3136, 0.15)])
    sweep = highpass(nz(1.4), 3000) * env(int(SR * 1.4), 0.25, 0.4, 0.3, 0.6) * 0.18
    save("drop_lendario", mix(seq(np.zeros(int(SR * 0.05)), shimmer), sweep, tone(98, 1.0) * fe(1.0, attack=0.01, decay=4) * 0.6), 0.6)
    # ---- acerto por arma ----
    save("acerto_lamina", mix(highpass(nz(0.14), 2200) * fe(0.14, attack=0.002, decay=28), lowpass(nz(0.1), 1800) * fe(0.1, decay=40) * 0.6, bell(2600, 0.12, 30) * 0.15), 0.6)
    save("acerto_contundente", mix(lowpass(nz(0.2), 500) * fe(0.2, decay=22), tone(lambda x: 120 - 60 * x / 0.2, 0.2) * fe(0.2, decay=18) * 0.9), 0.75)
    save("acerto_flecha", mix(lowpass(nz(0.08), 2200) * fe(0.08, decay=45), tone(lambda x: 320 - 160 * x / 0.15, 0.15, "tri") * fe(0.15, decay=22) * 0.5), 0.55)
    save("acerto_magia", mix(tone(lambda x: 900 - 500 * x / 0.18, 0.18, "sine") * fe(0.18, decay=16) * 0.6, highpass(nz(0.18), 3500) * fe(0.18, decay=20) * 0.5, lowpass(nz(0.18), 600) * fe(0.18, decay=25) * 0.4), 0.55)
    # ---- passos por chão ----
    save("passo_terra", lowpass(nz(0.08), 700) * fe(0.08, decay=40), 0.3)
    save("passo_areia", mix(lowpass(highpass(nz(0.13), 900), 3500) * env(int(SR * 0.13), 0.02, 0.08, 0.2, 0.03)), 0.22)
    save("passo_pedra", mix(lowpass(nz(0.06), 1800) * fe(0.06, decay=60), bell(900, 0.05, 70) * 0.25), 0.3)
    save("passo_neve", mix(lowpass(nz(0.16), 1600) * env(int(SR * 0.16), 0.03, 0.1, 0.3, 0.03) * (0.6 + 0.4 * np.sign(np.sin(t(0.16) * 2 * np.pi * 70)))), 0.25)
    save("passo_lama", mix(lowpass(nz(0.18), 500) * fe(0.18, decay=14), tone(lambda x: 200 + 300 * x / 0.18, 0.18) * fe(0.18, decay=20) * 0.3), 0.3)
    # ---- vozes ----
    def clicks(d, n, f0):
        x = np.zeros(int(SR * d))
        for k in range(n):
            st = int(SR * (k * d / n + r.uniform(0, 0.02))); b = highpass(nz(0.02), f0) * fe(0.02, decay=120)
            x[st:st + len(b)] += b[:len(x) - st]
        return x
    save("voz_casco", clicks(0.35, 7, 2500), 0.5)
    save("voz_ossos", mix(clicks(0.4, 9, 1200), bell(700, 0.08, 60) * 0.2), 0.5)
    save("voz_goblin", mix(tone(lambda x: 520 + 180 * np.sin(x * 55) + 200 * x, 0.32, "saw") * env(int(SR * 0.32), 0.02, 0.1, 0.5, 0.08) * 0.35, lowpass(nz(0.32), 2500) * 0.1), 0.45)
    save("voz_inseto", tone(lambda x: 220 + 25 * np.sin(x * 140), 0.45, "saw") * env(int(SR * 0.45), 0.05, 0.1, 0.6, 0.12) * (0.7 + 0.3 * np.sin(t(0.45) * 2 * np.pi * 32)) * 0.4, 0.4)
    save("voz_espirito", mix(tone(lambda x: 330 + 60 * np.sin(x * 3), 0.9, "sine") * env(int(SR * 0.9), 0.3, 0.2, 0.5, 0.3) * 0.4, highpass(nz(0.9), 2500) * env(int(SR * 0.9), 0.3, 0.2, 0.5, 0.3) * 0.25), 0.4)
    save("voz_fera", mix(lowpass(tone(lambda x: 110 + 30 * np.sin(x * 14), 0.6, "saw"), 700) * env(int(SR * 0.6), 0.06, 0.2, 0.5, 0.2), lowpass(nz(0.6), 500) * 0.4 * env(int(SR * 0.6), 0.05, 0.2, 0.5, 0.2)), 0.6)
    save("voz_bruto", mix(lowpass(tone(lambda x: 70 + 10 * np.sin(x * 6), 0.7, "saw"), 450) * env(int(SR * 0.7), 0.05, 0.2, 0.6, 0.2), lowpass(nz(0.7), 300) * 0.4 * env(int(SR * 0.7), 0.05, 0.2, 0.6, 0.2)), 0.7)
    save("voz_guincho", tone(lambda x: 2400 + 900 * np.sin(x * 30), 0.18, "tri") * env(int(SR * 0.18), 0.01, 0.05, 0.5, 0.05) * 0.4, 0.35)
    save("passo_metal", mix(bell(2100, 0.09, 45) * 0.5, bell(3150, 0.07, 55) * 0.3, highpass(nz(0.05), 3000) * fe(0.05, decay=60) * 0.3), 0.22)
    # ---- habilidades (som próprio por família de poder) ----
    save("hab_fogo", mix(lowpass(nz(0.5), 1800) * env(int(SR * 0.5), 0.02, 0.15, 0.4, 0.2), tone(lambda x: 140 + 80 * x, 0.5, "saw") * 0.25 * fe(0.5, decay=6)), 0.55)
    save("hab_gelo", mix(seq(bell(2600, 0.12, 25), bell(3300, 0.18, 18), bell(2900, 0.3, 12)) * 0.6, highpass(nz(0.45), 4000) * fe(0.45, decay=8) * 0.4), 0.5)
    save("hab_raio", mix(highpass(nz(0.3), 1500) * (0.5 + 0.5 * np.sign(np.sin(t(0.3) * 2 * np.pi * 37))) * fe(0.3, decay=10), tone(lambda x: 60 + 40 * x, 0.3, "square") * 0.2 * fe(0.3, decay=9)), 0.45)
    save("hab_cura", mix(bell(880, 0.8, 4) * 0.4, bell(1320, 0.8, 4) * 0.35, bell(1760, 0.8, 5) * 0.25, highpass(nz(0.8), 5000) * env(int(SR * 0.8), 0.2, 0.2, 0.3, 0.3) * 0.1), 0.45)
    save("hab_escudo", mix(bell(330, 0.6, 7), bell(495, 0.5, 8) * 0.5, lowpass(nz(0.2), 900) * fe(0.2, decay=20) * 0.6), 0.55)
    save("hab_giro", highpass(nz(0.45), 900) * env(int(SR * 0.45), 0.05, 0.1, 0.6, 0.12) * (0.5 + 0.5 * np.abs(np.sin(t(0.45) * 2 * np.pi * 7))), 0.5)
    save("hab_impacto", mix(lowpass(nz(0.6), 300) * fe(0.6, attack=0.003, decay=5), tone(lambda x: 70 - 30 * x, 0.6) * fe(0.6, decay=5) * 0.9), 0.85)
    save("hab_investida", mix(highpass(nz(0.35), 700) * env(int(SR * 0.35), 0.02, 0.05, 0.6, 0.1) * np.linspace(0.4, 1, int(SR * 0.35)), lowpass(nz(0.15), 500) * 0.0), 0.5)
    save("hab_sombra", mix(lowpass(nz(0.6), 900) * env(int(SR * 0.6), 0.2, 0.2, 0.4, 0.2), tone(lambda x: 220 - 100 * x, 0.6, "sine") * 0.2 * env(int(SR * 0.6), 0.2, 0.2, 0.4, 0.2)), 0.35)
    save("hab_espinhos", mix(clicks(0.35, 10, 900), lowpass(nz(0.35), 600) * fe(0.35, decay=9) * 0.6), 0.55)
    save("hab_chuva_flechas", mix(*[np.concatenate([np.zeros(int(SR * 0.05 * k)), highpass(nz(0.12), 2000) * fe(0.12, decay=25) * 0.5]) for k in range(8)]), 0.5)
    save("hab_arco_tenso", mix(tone(lambda x: 300 + 500 * x / 0.25, 0.25, "tri") * env(int(SR * 0.25), 0.1, 0.05, 0.6, 0.05) * 0.3, lowpass(nz(0.25), 2000) * 0.15), 0.4)
    save("voz_feitico", mix(seq(tone(196, 0.25, "tri") * env(int(SR * 0.25), 0.05, 0.1, 0.6, 0.08), tone(185, 0.3, "tri") * env(int(SR * 0.3), 0.05, 0.1, 0.6, 0.1)) * 0.5, highpass(nz(0.55), 3000) * 0.08), 0.4)


if __name__ == "__main__":
    main()

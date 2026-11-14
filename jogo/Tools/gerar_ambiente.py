#!/usr/bin/env python3
"""
Sons de ambiente por região (estilo MU: sem trilha, só o lugar) e laços de objetos do mundo.
Síntese 100% original (CC0), como Tools/gerar_sons.py. Rode: python3 Tools/gerar_ambiente.py

Camadas (o cliente toca até 3 juntas, ver Client/Sfx.cs e Main.AmbienceFor):
  amb2_vento, amb2_vento_forte, amb2_mar, amb2_gaivotas, amb2_passaros, amb2_grilos, amb2_sapos,
  amb2_floresta (pássaros + folhas), amb2_caverna (gotas + eco), amb2_criaturas (uivos/rosnados distantes),
  amb2_lava (borbulha grossa), amb2_sinos (sinos e vozes distantes de cidade),
  amb2_coro (coro de igreja grave/angelical, tocado sempre bem baixo por baixo de tudo)
Laços de objeto (tocados no lugar, somem com a distância): loop_caldeirao, loop_forja, loop_fogueira
E o efeito "caldeirao" (preparar poção) refeito: borbulhar de líquido, não bipes.
"""
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import gerar_sons as g
from gerar_sons import SR, t, noise, lowpass, highpass, save

rng = np.random.default_rng(42)
D = 12.0
N = int(SR * D)


def loop(x, fade=0.8):
    """Laço sem emenda: o fim entra por cima do começo (cross-fade)."""
    f = int(SR * fade)
    y = x[:len(x) - f].copy()
    y[:f] = y[:f] * np.linspace(0, 1, f) + x[len(x) - f:] * np.linspace(1, 0, f)
    return y


def save_loop(name, x, gain):
    """Como save(), mas sem o fade de saída (quebraria o laço)."""
    import wave
    x = np.asarray(x, dtype=float)
    peak = np.max(np.abs(x)) or 1
    data = (x / peak * gain * 32767).astype(np.int16)
    with wave.open(os.path.join(g.OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())


def place(track, snd, at):
    st = int(SR * at)
    if st >= len(track): return
    n = min(len(snd), len(track) - st)
    track[st:st + n] += snd[:n]


def slow_noise(d, rate):
    """Ruído suave (0..1) para modular volume: rajadas de vento, ondas."""
    k = max(2, int(d * rate) + 2)
    pts = rng.uniform(0, 1, k)
    x = np.linspace(0, k - 1, int(SR * d))
    return np.interp(x, np.arange(k), pts)


def chirp(f0, f1, d, vib=0.0, vib_rate=30.0):
    tt = t(d)
    f = f0 + (f1 - f0) * (tt / d) + vib * np.sin(2 * np.pi * vib_rate * tt)
    ph = 2 * np.pi * np.cumsum(f) / SR
    e = np.sin(np.pi * np.clip(tt / d, 0, 1)) ** 1.5
    return np.sin(ph) * e


def bird_phrase():
    kind = rng.integers(0, 3)
    parts = []
    base = rng.uniform(2600, 4200)
    for _ in range(rng.integers(3, 7)):
        if kind == 0:   # piados curtos descendo
            parts.append(chirp(base * 1.15, base * 0.85, rng.uniform(0.05, 0.09)))
        elif kind == 1: # trinado
            parts.append(chirp(base, base * 1.05, rng.uniform(0.12, 0.2), vib=280, vib_rate=rng.uniform(35, 55)))
        else:           # assobio subindo
            parts.append(chirp(base * 0.8, base * 1.2, rng.uniform(0.08, 0.14)))
        parts.append(np.zeros(int(SR * rng.uniform(0.03, 0.09))))
    return np.concatenate(parts)


def wind(strength=1.0):
    gust = 0.35 + 0.65 * slow_noise(D, 0.35)
    low = lowpass(noise(D), 300) * gust
    whistle = highpass(lowpass(noise(D), 1400), 600) * (slow_noise(D, 0.5) ** 2) * 0.35 * strength
    return (low + whistle) * (0.8 + 0.4 * strength)


def waves():
    out = np.zeros(N)
    at = 0.0
    while at < D:
        d = rng.uniform(4.5, 7.0)
        tt = t(d)
        swell = np.sin(np.pi * np.clip(tt / d, 0, 1)) ** 2
        # a espuma cresce devagar quando a onda quebra e some aos poucos (sem estalo)
        crash = np.clip((tt - d * 0.35) / (d * 0.25), 0, 1) ** 2 * np.exp(-np.clip(tt - d * 0.6, 0, None) * 2.0)
        body = lowpass(noise(d), 500) * swell
        fizz = highpass(noise(d), 2500) * crash * 0.35
        place(out, body + fizz, at)
        at += d * rng.uniform(0.6, 0.8)
    return out


def gull():
    d = rng.uniform(0.35, 0.55)
    tt = t(d)
    f = 1500 - 500 * (tt / d) + 120 * np.sin(2 * np.pi * 9 * tt)
    ph = 2 * np.pi * np.cumsum(f) / SR
    nasal = np.sign(np.sin(ph)) * 0.5 + np.sin(2 * ph) * 0.3
    return lowpass(nasal, 2600) * np.sin(np.pi * np.clip(tt / d, 0, 1)) ** 0.7


def crickets():
    out = np.zeros(N)
    for voice in range(3):
        f = rng.uniform(4200, 5200)
        at = rng.uniform(0, 1)
        while at < D:
            burst = rng.uniform(0.25, 0.6)
            tt = t(burst)
            pulse = (np.sin(2 * np.pi * rng.uniform(25, 35) * tt) > 0.2).astype(float)
            place(out, np.sin(2 * np.pi * f * tt) * pulse * 0.4, at)
            at += burst + rng.uniform(0.3, 1.2)
    return out


def frog():
    d = rng.uniform(0.18, 0.32)
    tt = t(d)
    f = rng.uniform(110, 200)
    ph = 2 * np.pi * f * tt
    saw = ((ph / (2 * np.pi)) % 1) * 2 - 1
    am = 0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(25, 40) * tt)
    return lowpass(saw * am, 900) * np.sin(np.pi * np.clip(tt / d, 0, 1))


def drip():
    d = 0.5
    tt = t(d)
    f0 = rng.uniform(900, 2200)
    ph = 2 * np.pi * np.cumsum(f0 * (1 + 0.6 * np.exp(-tt * 40))) / SR
    x = np.sin(ph) * np.exp(-tt * 22)
    # eco de caverna: duas repetições mais fracas
    y = x.copy()
    for k, a in ((int(SR * 0.11), 0.35), (int(SR * 0.23), 0.18)):
        y[k:] += x[:len(x) - k] * a
    return y


def howl():
    d = rng.uniform(1.4, 2.2)
    tt = t(d)
    f = 380 + 220 * np.sin(np.pi * np.clip(tt / d, 0, 1)) + 8 * np.sin(2 * np.pi * 5 * tt)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(ph) + 0.3 * np.sin(2 * ph)
    return lowpass(x, 1200) * np.sin(np.pi * np.clip(tt / d, 0, 1)) ** 1.2


def growl():
    d = rng.uniform(0.8, 1.3)
    tt = t(d)
    f = rng.uniform(70, 95) + 15 * np.sin(2 * np.pi * 11 * tt)
    ph = 2 * np.pi * np.cumsum(f) / SR
    saw = ((ph / (2 * np.pi)) % 1) * 2 - 1
    rough = lowpass(noise(d), 400)
    return lowpass(saw * 0.7 + rough * 0.6, 700) * np.sin(np.pi * np.clip(tt / d, 0, 1))


def bubble(f0, decay):
    d = 0.12
    tt = t(d)
    f = f0 * (1 + 2.5 * tt / d)   # bolha que estoura: o tom sobe (Minnaert)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * np.minimum(1, tt / 0.003) * np.exp(-tt * decay)


def boiling(d, density, fmin, fmax, rumble=0.5):
    out = np.zeros(int(SR * d))
    for _ in range(int(d * density)):
        place(out, bubble(rng.uniform(fmin, fmax), rng.uniform(35, 70)) * rng.uniform(0.25, 1.0), rng.uniform(0, d - 0.13))
    base = lowpass(noise(d), 180) * (0.6 + 0.4 * slow_noise(d, 2.0)) * rumble
    return out + base


def anvil(at_track, at):
    for k, f in enumerate((1450, 1380)):
        tt = t(0.6)
        ring = (np.sin(2 * np.pi * f * tt) + 0.45 * np.sin(2 * np.pi * f * 2.71 * tt) + 0.2 * np.sin(2 * np.pi * f * 4.2 * tt)) * np.exp(-tt * 9)
        hit = highpass(noise(0.04), 2500) * np.exp(-t(0.04) * 80)
        place(at_track, ring * 0.6, at + k * 0.28)
        place(at_track, hit, at + k * 0.28)


def fire(d):
    out = lowpass(noise(d), 250) * 0.5 * (0.7 + 0.3 * slow_noise(d, 1.5))
    for _ in range(int(d * 14)):
        c = highpass(noise(0.015), 1800) * np.exp(-t(0.015) * 200) * rng.uniform(0.3, 1.2)
        place(out, c, rng.uniform(0, d - 0.02))
    return out


# ---------------------------------------------------------------- coro (vozes graves e angelicais, bem baixo)
def choir(d=48.0, sr=SR):
    """Coro de igreja ao longe: vozes masculinas graves e femininas suaves cantando vogais "aa"/"oo" em acordes lentos
    (modo dórico), com vibrato, respiração e reverberação de catedral. Feito para tocar baixinho por baixo do ambiente."""
    from scipy.signal import butter, lfilter
    n = int(sr * d)
    tt = np.arange(n) / sr
    out = np.zeros(n)
    # progressão (graus em semitons a partir de Ré): Dm - Bb - F - C - Dm - Gm - A - Dm
    chords = [[0, 7, 12, 15], [-4, 3, 8, 12], [3, 10, 15, 19], [-2, 5, 10, 14], [0, 7, 12, 17], [5, 10, 14, 17], [7, 11, 14, 19], [0, 7, 12, 15]]
    seg = d / len(chords)
    base = 73.42   # Ré2
    vowels = {"a": [(730, 1.0, 90), (1090, 0.5, 110), (2440, 0.18, 160)], "o": [(450, 1.0, 80), (800, 0.45, 100), (2830, 0.08, 160)]}

    def formant(x, vowel):
        y = np.zeros_like(x)
        for f, g_, bw in vowels[vowel]:
            lo, hi = max(40, f - bw) / (sr / 2), min(sr / 2 - 100, f + bw) / (sr / 2)
            b, a = butter(2, [lo, hi], btype="band")
            y += lfilter(b, a, x) * g_
        return y

    for ci, ch in enumerate(chords):
        st = int(ci * seg * sr)
        ln = int((seg + 3.0) * sr)              # sobrepõe 3 s com o próximo acorde (legato)
        ln = min(ln, n - st)
        tl = np.arange(ln) / sr
        env = np.minimum(1, tl / 2.2) * np.minimum(1, (ln / sr - tl) / 2.8)
        for vi, semi in enumerate(ch):
            for voice in range(3):               # 3 cantores por nota (coro, não solista)
                octave = 1 if vi >= 2 else 0       # notas de cima: vozes femininas (oitava acima)
                f0 = base * 2 ** ((semi + 12 * octave) / 12) * (1 + rng.uniform(-0.004, 0.004))
                vib = 1 + 0.006 * np.sin(2 * np.pi * rng.uniform(4.6, 5.6) * tl + rng.uniform(0, 6)) * np.minimum(1, tl / 1.5)
                ph = 2 * np.pi * np.cumsum(f0 * vib) / sr
                # fonte glotal: dente de serra suavizado (harmônicos que os formantes moldam)
                src = sum(np.sin(k * ph) / k ** 1.15 for k in range(1, 24))
                src += lowpass(rng.normal(0, 1, ln), 3000) * 0.05      # sopro/respiração
                v = formant(src, "o" if (ci + vi) % 3 == 0 else "a")
                gain = (1.0 if octave == 0 else 0.45) * rng.uniform(0.8, 1.1)
                out[st:st + ln] += v * env * gain
    # reverberação de catedral: pente de atrasos com realimentação + passa-tudo (Schroeder), cauda longa
    def comb(x, delay, fb):
        dl = int(delay * sr)
        y = x.copy()
        for i in range(dl, len(y), dl):
            y[i:i + dl] += y[i - dl:i][: len(y[i:i + dl])] * fb
        return y
    wet = sum(comb(out, dly, 0.83) for dly in (0.0297, 0.0371, 0.0411, 0.0437)) / 4
    mix = lowpass(out * 0.35 + wet * 0.9, 3800)
    return mix


def main():
    # ---- camadas de região ----
    save_loop("amb2_vento", loop(wind(0.6)), 0.45)
    save_loop("amb2_vento_forte", loop(wind(1.4)), 0.6)
    save_loop("amb2_mar", loop(waves()), 0.55)
    gulls = np.zeros(N)
    for at in rng.uniform(0.5, D - 2, 4): place(gulls, gull() * rng.uniform(0.4, 1.0), at); place(gulls, gull() * 0.5, at + 0.6)
    save_loop("amb2_gaivotas", loop(gulls + 0.02 * noise(D)), 0.35)
    birds = np.zeros(N)
    for at in rng.uniform(0.2, D - 1.5, 9): place(birds, bird_phrase() * rng.uniform(0.3, 1.0), at)
    save_loop("amb2_passaros", loop(birds + 0.01 * noise(D)), 0.35)
    forest = birds * 0.7 + highpass(lowpass(noise(D), 3000), 900) * (slow_noise(D, 0.4) ** 2) * 0.25   # folhas
    save_loop("amb2_floresta", loop(forest), 0.4)
    save_loop("amb2_grilos", loop(crickets()), 0.25)
    frogs = np.zeros(N)
    for at in rng.uniform(0.2, D - 1, 16):
        for k in range(rng.integers(1, 4)): place(frogs, frog() * rng.uniform(0.4, 1.0), at + k * 0.35)
    save_loop("amb2_sapos", loop(frogs + crickets() * 0.3), 0.45)
    cave = lowpass(noise(D), 120) * 0.5 + lowpass(g.tone(48, D, "saw"), 150) * 0.25
    for at in rng.uniform(0.2, D - 0.6, 10): place(cave, drip() * rng.uniform(0.3, 1.0), at)
    save_loop("amb2_caverna", loop(cave), 0.45)
    beasts = np.zeros(N)
    place(beasts, howl() * 0.6, rng.uniform(0.5, 3))
    place(beasts, growl() * 0.5, rng.uniform(5, 7))
    place(beasts, howl() * 0.35, rng.uniform(8.5, 9.5))
    save_loop("amb2_criaturas", loop(lowpass(beasts, 1500) + 0.01 * noise(D)), 0.3)
    save_loop("amb2_lava", loop(boiling(D, 6, 60, 160, rumble=1.2)), 0.5)
    town = np.zeros(N)
    for at in rng.uniform(0.3, D - 1, 3): place(town, g.bell(rng.choice([520, 660, 780]), 1.2, 3) * 0.25, at)
    murmur = lowpass(noise(D), 700) * (0.5 + 0.5 * slow_noise(D, 3)) * 0.35   # vozes ao longe
    for at in rng.uniform(0.3, D - 1, 3): anvil(town, at)   # ferraria ao longe
    save_loop("amb2_sinos", loop(lowpass(town, 3000) * 0.5 + murmur), 0.3)

    save_loop("amb2_coro", loop(choir(), 3.0), 0.5)

    # ---- laços de objeto ----
    save_loop("loop_caldeirao", loop(boiling(6.0, 22, 160, 650)[: int(SR * 6.0)], 0.5), 0.5)
    forge = np.zeros(int(SR * 4.4))
    anvil(forge, 0.2); anvil(forge, 2.4)
    forge += fire(4.4) * 0.35
    save_loop("loop_forja", loop(forge, 0.3), 0.55)
    save_loop("loop_fogueira", loop(fire(6.0), 0.5), 0.45)
    # efeito único ao preparar poção: borbulhar curto (antes soava como bipe de chamada)
    save("caldeirao", boiling(1.1, 30, 180, 700, rumble=0.4), 0.55)
    print("ambiente gerado em", os.path.abspath(g.OUT))


if __name__ == "__main__":
    main()

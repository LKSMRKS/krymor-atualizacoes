#!/usr/bin/env python3
"""
Vozes dos NPCs por síntese de formantes (pedido 10/11: "sons que são apenas barulhos — rindo, hum, bravo, haha, oouu
(tipo um olá) — cada um com seu timbre, mulher também"). Sem palavras: só a "música" da fala, estilo Animal Crossing/
Zelda. Dois conjuntos-base (masculino e feminino); o cliente ainda muda a altura por NPC (timbre próprio).
Saída: Client/Assets/Sounds/voz_<m|f>_<tipo>.wav (CC0, feitos por código).  Rode: python3 Tools/gerar_vozes.py
"""
import os, wave
import numpy as np
from scipy.signal import lfilter

SR = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Client", "Assets", "Sounds")
rng = np.random.default_rng(11)

# formantes (Hz, largura) por vogal — voz masculina; a feminina sobe ~17%
VOWELS = {
    "a": [(730, 90), (1090, 110), (2440, 160)],
    "e": [(530, 80), (1840, 100), (2480, 160)],
    "i": [(300, 70), (2200, 120), (2950, 180)],
    "o": [(500, 80), (850, 90), (2400, 160)],
    "u": [(320, 70), (720, 90), (2300, 160)],
    "m": [(260, 60), (1300, 300), (2600, 300)],      # boca fechada (hum): quase só o grave
    "eh": [(600, 90), (1700, 110), (2500, 160)],
}


def resonator(x, f, bw):
    r = np.exp(-np.pi * bw / SR)
    th = 2 * np.pi * f / SR
    b = [1 - r]
    a = [1, -2 * r * np.cos(th), r * r]
    return lfilter(b, a, x)


def glottal(f0, rough=0.0, breath=0.05):
    """Trem de pulsos glotais (dente de serra suavizado) seguindo a curva de altura f0 (array por amostra)."""
    jitter = 1 + rough * 0.06 * rng.standard_normal(len(f0))
    ph = np.cumsum(f0 * jitter) / SR
    saw = (ph % 1.0)
    src = (1 - saw) ** 3 - 0.25                         # pulso com queda rápida (mais "voz" que serra pura)
    src = src + breath * rng.standard_normal(len(f0))
    if rough > 0:                                       # aspereza (rosnado): modulação subharmônica
        src *= 1 + rough * 0.5 * np.sin(np.cumsum(f0 * 0.5) / SR * 2 * np.pi)
    return src


def voice(segments, female, base, rough=0.0, breath=0.05):
    """segments: lista de (vogal, duração s, f0 inicial rel., f0 final rel., volume). Junta com transição suave."""
    k = 1.17 if female else 1.0
    out = []
    for vowel, dur, r0, r1, vol in segments:
        n = int(SR * dur)
        if vowel == "_":
            out.append(np.zeros(n)); continue
        tt = np.linspace(0, 1, n)
        f0 = base * (r0 + (r1 - r0) * (3 * tt ** 2 - 2 * tt ** 3))
        f0 *= 1 + 0.012 * np.sin(2 * np.pi * 5.5 * tt * dur)        # vibrato leve
        src = glottal(f0, rough, breath)
        y = sum(resonator(src, f * k, bw * k) * g for (f, bw), g in zip(VOWELS[vowel], (1.0, 0.55, 0.25)))
        a = min(0.03, dur / 4); r = min(0.06, dur / 3)
        env = np.minimum(1, np.minimum(tt * dur / a, (1 - tt) * dur / r)) ** 1.5
        out.append(y * env * vol)
    sig = np.concatenate(out)
    sig = sig / (np.max(np.abs(sig)) + 1e-9) * 0.8
    return sig


def breath_burst(dur, vol=0.3):
    n = int(SR * dur)
    x = rng.standard_normal(n)
    x = resonator(x, 1400, 900) + resonator(x, 3000, 1200) * 0.4
    tt = np.linspace(0, 1, n)
    return x / (np.max(np.abs(x)) + 1e-9) * vol * np.sin(np.pi * tt) ** 2


def save(name, x):
    os.makedirs(OUT, exist_ok=True)
    x = np.clip(x, -1, 1)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())


def laugh(female, base, syll="a", n=4, step=0.95):
    segs = []
    r = 1.15
    for i in range(n):
        segs += [(syll, 0.11, r, r * 0.92, 1.0 - i * 0.12), ("_", 0.055, 1, 1, 0)]
        r *= step
    x = voice(segs, female, base, breath=0.18)
    # o "h" de cada sílaba: sopro curto antes
    out = np.copy(x)
    pos = 0
    for i in range(n):
        b = breath_burst(0.05, 0.25)
        end = min(len(out), pos + len(b))
        out[pos:end] += b[: end - pos]
        pos += int(SR * (0.11 + 0.055))
    return out / (np.max(np.abs(out)) + 1e-9) * 0.8


def main():
    for g, female, base in (("m", False, 118.0), ("f", True, 215.0)):
        # olá: "ou-ê!" sobe e desce, simpático
        save(f"voz_{g}_ola", voice([("o", 0.16, 1.0, 1.25, 1.0), ("u", 0.12, 1.25, 1.35, 0.9), ("eh", 0.2, 1.4, 1.05, 0.95)], female, base))
        # hum: pensativo, boca fechada, sobe no fim
        save(f"voz_{g}_hum", voice([("m", 0.22, 1.0, 0.95, 1.0), ("m", 0.2, 0.95, 1.15, 0.9)], female, base, breath=0.02))
        # hum curto de concordância ("aham")
        save(f"voz_{g}_aham", voice([("a", 0.1, 1.05, 1.1, 0.9), ("m", 0.18, 1.0, 0.9, 1.0)], female, base))
        # risada: "ha-ha-ha-ha"
        save(f"voz_{g}_haha", laugh(female, base, "a", 4))
        # risadinha: "he-he-he"
        save(f"voz_{g}_risada", laugh(female, base * 1.1, "e", 3, 0.97))
        # bravo: "hrrmph!" grave e áspero
        save(f"voz_{g}_bravo", voice([("o", 0.08, 0.85, 0.95, 0.8), ("m", 0.28, 0.95, 0.75, 1.0)], female, base * 0.9, rough=1.0, breath=0.12))
        # pergunta: "hã?" sobe
        save(f"voz_{g}_hein", voice([("eh", 0.12, 0.95, 1.0, 0.9), ("a", 0.2, 1.0, 1.45, 1.0)], female, base))
        # surpresa/alegria: "oh!"
        save(f"voz_{g}_oh", voice([("o", 0.22, 1.35, 1.0, 1.0)], female, base))
        # suspiro/cansado: "aaai"
        save(f"voz_{g}_ai", voice([("a", 0.22, 1.1, 0.9, 0.9), ("i", 0.16, 0.9, 0.8, 0.7)], female, base, breath=0.2))
    print("vozes geradas em", os.path.abspath(OUT))


if __name__ == "__main__":
    main()

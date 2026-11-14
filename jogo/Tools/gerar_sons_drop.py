#!/usr/bin/env python3
"""14/11 (dono: "OLOCO, olha o que eu dropei"): sons do drop bom (CC0, feitos por código).
drop_raro: dois sininhos subindo. drop_lendario: arpejo de sinos subindo com brilho e um grave por baixo."""
import os, numpy as np, wave
SR = 44100
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Client", "Assets", "Sounds")
def sino(f, d, dec=4.0, vol=1.0):
    t = np.arange(int(SR * d)) / SR
    x = (np.sin(2*np.pi*f*t) + 0.45*np.sin(2*np.pi*f*2.01*t) + 0.2*np.sin(2*np.pi*f*3.02*t)) * np.exp(-dec*t)
    a = np.minimum(1, t / 0.004)
    return x * a * vol
def junta(partes, total):
    out = np.zeros(int(SR * total))
    for ini, x in partes:
        i = int(SR * ini); out[i:i+len(x)] += x[:len(out)-i]
    return out
def salvar(nome, x, ganho=0.7):
    x = x / (np.max(np.abs(x)) + 1e-9) * ganho
    with wave.open(os.path.join(OUT, nome + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
salvar("drop_raro", junta([(0, sino(880, 0.9)), (0.09, sino(1318.5, 1.1))], 1.3), 0.55)
notas = [523.25, 659.25, 783.99, 1046.5, 1318.5, 1567.98]
partes = [(i * 0.075, sino(f, 1.6, 3.0, 0.9)) for i, f in enumerate(notas)]
t = np.arange(int(SR * 1.8)) / SR
grave = np.sin(2*np.pi*130.8*t) * np.exp(-2.2*t) * 0.8
brilho = np.random.default_rng(7).uniform(-1, 1, len(t)) * np.exp(-3*t) * 0.05 * np.sin(2*np.pi*6*t)**2
partes += [(0, grave), (0.3, brilho)]
salvar("drop_lendario", junta(partes, 2.3), 0.75)
print("ok")

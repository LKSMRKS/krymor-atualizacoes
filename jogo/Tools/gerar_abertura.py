#!/usr/bin/env python3
"""
Abertura do KRYMOR (12/11/2026). Pega o vídeo do dono (KRYMOR_Krypta_v2.mp4, 7 s) e monta a abertura do jogo:
 - brilho (bloom) nos azuis dos cristais, para deixar tudo mais vivo;
 - um cristal escuro (obsidiana) flutuando no preto do meio, com uma sombra que cresce até escurecer a tela;
 - o texto da Krypta no centro, palavra por palavra, na fonte dos nomes (EB Garamond), e na última palavra o título KRYMOR;
 - a câmera avança para a escuridão entre as paredes; o título some no escuro.
Saída: abertura.mp4 (para ver) e abertura.ogv (Theora, o formato que o Godot toca).
Uso: python3 Tools/gerar_abertura.py <video_origem.mp4> <pasta_saida>
"""
import math
import os
import subprocess
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ORIGEM, SAIDA = sys.argv[1], sys.argv[2]
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTES = os.path.join(RAIZ, "Client", "Assets", "Fonts")
W, H, FPS, DUR = 1920, 1080, 30, 16.0
N = int(FPS * DUR)

TEXTO = ("No princípio havia apenas a Krypta, o abismo de cristal onde os primeiros despertaram. "
         "Aqueles que dominaram seus segredos ficaram conhecidos como os Kryptor, e a vastidão inexplorada "
         "que reivindicaram recebeu o nome eterno de KRYMOR.")
T_TEXTO0, T_TEXTO1 = 1.2, 10.6          # primeira e última palavra aparecem
T_TITULO = 10.8                          # título entra na última palavra
T_TITULO_SAI0, T_TITULO_SAI1 = 13.4, 15.2

fonte = ImageFont.truetype(os.path.join(FONTES, "eb-garamond-latin-600-normal.woff"), 50)
fonte_titulo = ImageFont.truetype(os.path.join(FONTES, "cinzel-latin-700-normal.woff"), 190)


def ler_quadros():
    """Vídeo original esticado para DUR segundos (quadros intermediários misturados) em 1920x1080."""
    cap = cv2.VideoCapture(ORIGEM)
    src = []
    while True:
        ok, f = cap.read()
        if not ok:
            break
        src.append(cv2.resize(f, (W, H), interpolation=cv2.INTER_AREA))
    cap.release()
    return src


def amostra(src, t):
    # o vídeo original cobre o tempo todo, mais devagar (câmera lenta com mistura de quadros)
    x = t / DUR * (len(src) - 1)
    i = int(math.floor(x))
    j = min(i + 1, len(src) - 1)
    a = x - i
    return cv2.addWeighted(src[i], 1 - a, src[j], a, 0).astype(np.float32) / 255.0


def bloom(img):
    """Brilho nos azuis: só o que é azul e claro vaza luz; dois raios de desfoque."""
    b, g, r = img[..., 0], img[..., 1], img[..., 2]
    azul = np.clip((b - np.maximum(r, g) * 0.85) * 2.2, 0, 1) * np.clip((b - 0.25) * 2.0, 0, 1)
    fonte_luz = img * azul[..., None]
    perto = cv2.GaussianBlur(fonte_luz, (0, 0), 9)
    longe = cv2.GaussianBlur(fonte_luz, (0, 0), 38)
    out = img + perto * 1.15 + longe * 0.9
    # azul um pouco mais saturado
    out[..., 0] = out[..., 0] * 1.08
    return out


def cristal_e_sombra(img, t):
    """Cristal de obsidiana flutuando no preto do meio + sombra que cresce e escurece a tela."""
    cx, cy = W * 0.5, H * 0.33 + 10 * math.sin(t * 1.3)
    k = 0.42 + 0.04 * t                        # cresce devagar (a câmera chega perto)
    pts = np.array([(0, -260), (62, -90), (48, 120), (0, 210), (-52, 110), (-66, -80)], np.float32) * k
    rot = 0.08 * math.sin(t * 0.7)
    c, s = math.cos(rot), math.sin(rot)
    pts = np.array([(cx + x * c - y * s, cy + x * s + y * c) for x, y in pts], np.int32)
    # sombra radial: um círculo escuro que nasce no cristal e vai tomando a tela
    raio = 120 + (t / DUR) ** 1.6 * 1500
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    sombra = np.clip(1.0 - (d - raio * 0.55) / (raio * 0.6), 0, 1) * min(1.0, 0.35 + t / DUR * 0.75)
    img = img * (1 - 0.85 * sombra[..., None])
    # cristal: corpo quase preto, borda azul fina que pulsa
    mask = np.zeros((H, W), np.uint8)
    cv2.fillPoly(mask, [pts], 255)
    m = mask.astype(np.float32)[..., None] / 255.0
    corpo = np.array([0.035, 0.02, 0.025], np.float32)   # BGR
    img = img * (1 - m) + corpo * m
    borda = np.zeros((H, W), np.uint8)
    cv2.polylines(borda, [pts], True, 255, 2, cv2.LINE_AA)
    cv2.line(borda, tuple(pts[0]), tuple(pts[3]), 120, 1, cv2.LINE_AA)
    bf = borda.astype(np.float32)[..., None] / 255.0
    pulso = 0.55 + 0.45 * math.sin(t * 2.4)
    luz = np.array([1.0, 0.45, 0.15], np.float32) * pulso    # azul (BGR)
    img = img + bf * luz + cv2.GaussianBlur(bf, (0, 0), 6)[..., None] * luz * 0.8
    return img


def zoom(img, t):
    """Câmera avançando para o escuro do meio: devagar no começo, acelera depois do título."""
    z = 1.0 + 0.012 * t + (max(0.0, t - T_TITULO) / (DUR - T_TITULO)) ** 2 * 0.9
    cx, cy = W * 0.5, H * 0.38
    m = np.float32([[z, 0, cx - z * cx], [0, z, cy - z * cy]])
    return cv2.warpAffine(img, m, (W, H), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT)


def quebrar(palavras, largura):
    linhas, atual = [], []
    for p in palavras:
        teste = " ".join(atual + [p])
        if atual and fonte.getlength(teste) > largura:
            linhas.append(atual)
            atual = [p]
        else:
            atual.append(p)
    if atual:
        linhas.append(atual)
    return linhas


PALAVRAS = TEXTO.split(" ")
LINHAS = quebrar(PALAVRAS, 1380)
ALTURA_LINHA = 70


def texto(img_pil, t):
    d = ImageDraw.Draw(img_pil, "RGBA")
    total = len(PALAVRAS)
    sai = np.clip((t - T_TITULO) / 0.8, 0, 1)            # o parágrafo some quando o título entra
    y0 = H * 0.66 - len(LINHAS) * ALTURA_LINHA / 2
    idx = 0
    for li, linha in enumerate(LINHAS):
        frase = " ".join(linha)
        x = (W - fonte.getlength(frase)) / 2
        for p in linha:
            t_p = T_TEXTO0 + (T_TEXTO1 - T_TEXTO0) * idx / (total - 1)
            a = np.clip((t - t_p) / 0.45, 0, 1) * (1 - sai)
            if a > 0:
                cor = (232, 222, 196) if p.strip(".,") != "KRYMOR" else (150, 200, 255)
                # sombra suave + texto
                d.text((x + 2, y0 + li * ALTURA_LINHA + 3), p, font=fonte, fill=(0, 0, 0, int(200 * a)))
                d.text((x, y0 + li * ALTURA_LINHA), p, font=fonte, fill=cor + (int(255 * a),))
            x += fonte.getlength(p + " ")
            idx += 1


def titulo(img, t):
    if t < T_TITULO:
        return img
    entra = np.clip((t - T_TITULO) / 0.9, 0, 1)
    sai = 1 - np.clip((t - T_TITULO_SAI0) / (T_TITULO_SAI1 - T_TITULO_SAI0), 0, 1)
    a = entra * sai
    if a <= 0:
        return img
    camada = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(camada)
    txt = "KRYMOR"
    esp = 18
    larg = sum(fonte_titulo.getlength(c) for c in txt) + esp * (len(txt) - 1)
    x = (W - larg) / 2
    y = H * 0.56 - 95 + (1 - entra) * 25
    for c in txt:
        d.text((x, y), c, font=fonte_titulo, fill=255)
        x += fonte_titulo.getlength(c) + esp
    m = np.asarray(camada, np.float32) / 255.0
    brilho = cv2.GaussianBlur(m, (0, 0), 22)
    cor = np.array([0.98, 0.93, 0.86], np.float32)     # BGR: branco frio
    azul = np.array([1.0, 0.55, 0.2], np.float32)
    img = img * (1 - m[..., None] * a) + m[..., None] * cor * a + brilho[..., None] * azul * a * 0.9
    return img


def main():
    os.makedirs(SAIDA, exist_ok=True)
    src = ler_quadros()
    mp4 = os.path.join(SAIDA, "abertura.mp4")
    ff = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "bgr24", "-s", f"{W}x{H}", "-r", str(FPS),
                           "-i", "-", "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p", mp4],
                          stdin=subprocess.PIPE)
    for n in range(N):
        t = n / FPS
        img = amostra(src, t)
        img = bloom(img)
        img = cristal_e_sombra(img, t)
        img = zoom(img, t)
        img = np.clip(img, 0, 1)
        fade = min(1.0, t / 0.8) * min(1.0, (DUR - t) / 0.6)
        img = img * fade
        pil = Image.fromarray(cv2.cvtColor((img * 255).astype(np.uint8), cv2.COLOR_BGR2RGB))
        texto(pil, t)
        img = cv2.cvtColor(np.asarray(pil), cv2.COLOR_RGB2BGR).astype(np.float32) / 255.0
        img = titulo(img, t) * fade
        ff.stdin.write((np.clip(img, 0, 1) * 255).astype(np.uint8).tobytes())
    ff.stdin.close()
    ff.wait()
    ogv = os.path.join(SAIDA, "abertura.ogv")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", mp4, "-c:v", "libtheora", "-q:v", "8", "-an", ogv], check=True)
    print(mp4, ogv)


if __name__ == "__main__":
    main()

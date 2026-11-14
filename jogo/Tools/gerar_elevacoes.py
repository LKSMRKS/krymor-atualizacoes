#!/usr/bin/env python3
"""
13/11 (pedido do dono: "assim como a rampinha do cais do Porto, ter elevações e rampas pelos mapas — lugares um pouco mais
altos, onde tem inimigos, onde tem NPC — pra dar imersão"): escreve "elevations" nos mapas de campo.
  - platô de 1,2–1,8 m debaixo dos lugares com nome que pedem altura (mirante, ruína, altar, túmulo, pouso);
  - 1 ou 2 morros suaves (0,6–1,3 m) em cada bolsão de monstros;
  - um morro largo no covil (arena um pouco acima do resto);
  - morros soltos pelo resto do mapa até ~14 por mapa grande.
Só visual (o servidor segue plano): o cliente sobe o chão (TerrainShader) e põe pés, bichos, árvores e peças em cima
(EscadaCasa.Elev). Regras: nada na vila/área segura, longe de muro/cerca/penhasco/ravina (caixas não sobem bem), de
portal, de portão/ponte e da borda; morros não se encostam.
Idempotente (reescreve a lista). Rodar depois de gerar_lugares.py.
"""
import json, os, glob, math, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
TEMAS = {"praia", "vale", "mata", "brejo", "canion", "lava", "neve", "acropole", "fantasma"}   # MapView.TerrainMaterialAvailable
ALTO = ("Mirante", "Ruína", "Altar", "Túmulo", "Pouso", "Colunata", "Pedra", "Forca", "Bigorna")

def caixa_perto(m, x0, x1, y0, y1, folga):
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if o.get("kind") in ("lago", "farol", "invisivel") and x0 - folga - o["r"] < o["x"] < x1 + folga + o["r"] and y0 - folga - o["r"] < o["y"] < y1 + folga + o["r"]:
                return True
            continue
        if o["x"] + o["w"] / 2 > x0 - folga and o["x"] - o["w"] / 2 < x1 + folga and o["y"] + o["h"] / 2 > y0 - folga and o["y"] - o["h"] / 2 < y1 + folga:
            return True
    return False

def cabe(m, e, feitas, especiais):
    hx, hy, r = e["w"] / 2 + e["ramp"], e["h"] / 2 + e["ramp"], e["ramp"]
    x0, x1, y0, y1 = e["x"] - hx, e["x"] + hx, e["y"] - hy, e["y"] + hy
    hs = m["half_size"]
    if x0 < -hs + 10 or x1 > hs - 10 or y0 < -hs + 10 or y1 > hs - 10: return False
    if caixa_perto(m, x0, x1, y0, y1, 1.0): return False
    for z in m["zones"]:
        if z["kind"] in ("refugio", "seguro", "cidade", "caminho" if not m.get("roads") else "_") and \
           abs(e["x"] - z["x"]) < z["w"] / 2 + hx + 2 and abs(e["y"] - z["y"]) < z["h"] / 2 + hy + 2: return False
    for lst, d in ((m.get("portals", []), 3), (m.get("gates", []), 6)):
        for p in lst:
            if x0 - d < p["x"] < x1 + d and y0 - d < p["y"] < y1 + d: return False
    if math.hypot(e["x"] - m["spawn_x"], e["y"] - m["spawn_y"]) < max(hx, hy) + m.get("safe_radius", 8) + 4: return False
    for f in feitas:
        if abs(e["x"] - f["x"]) < hx + f["w"] / 2 + f["ramp"] + 2 and abs(e["y"] - f["y"]) < hy + f["h"] / 2 + f["ramp"] + 2: return False
    for sx, sy, sr in especiais:
        if math.hypot(e["x"] - sx, e["y"] - sy) < sr + max(hx, hy): return False
    return True

def main():
    total = 0
    for f in sorted(glob.glob(os.path.join(C, "map_*.json"))):
        m = json.load(open(f, encoding="utf-8"))
        if m.get("instanced") or m["kind"] != "campo" or m["theme"] not in TEMAS:
            if "elevations" in m: m.pop("elevations"); json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
            continue
        rnd = random.Random("elev:" + m["id"])
        hs = m["half_size"]
        especiais = []
        if m["id"] == "costa_do_cal": especiais += [(-31, 49, 26)]          # cena do lago (CenaPrintCosta)
        if m["theme"] == "praia": especiais += [(hs, y, hs * 0.45) for y in range(int(-hs), int(hs) + 1, 10)]   # mar a leste
        feitas = []
        def tenta(cx, cy, w, h, alt, ramp, jitter=0.0, tentativas=1):
            for _ in range(tentativas):
                x = cx + rnd.uniform(-jitter, jitter); y = cy + rnd.uniform(-jitter, jitter)
                e = {"x": round(x, 1), "y": round(y, 1), "w": round(w, 1), "h": round(h, 1), "height": round(alt, 2), "ramp": round(ramp, 1)}
                if cabe(m, e, feitas, especiais): feitas.append(e); return True
            return False
        # 1) platôs debaixo dos lugares altos
        for z in m["zones"]:
            if z["kind"] == "lugar" and any(k in z["name"] for k in ALTO):
                tenta(z["x"], z["y"], 13, 13, rnd.uniform(1.8, 2.6), 6.5)
        # 2) covil: arena um pouco acima
        for z in m["zones"]:
            if z["kind"] == "perigoso":
                tenta(z["x"], z["y"], z["w"] * 0.6, z["h"] * 0.6, 1.1, 5.0)
        # 3) bolsões: 1–2 morros
        for z in m["zones"]:
            if z["kind"] in ("fraco", "medio", "forte"):
                for _ in range(2 if z["w"] * z["h"] > 700 else 1):
                    tenta(z["x"], z["y"], rnd.uniform(6, 12), rnd.uniform(6, 12), rnd.uniform(0.9, 1.8), rnd.uniform(4.5, 6.5), jitter=min(z["w"], z["h"]) * 0.35, tentativas=25)
        # 4) soltos pelo resto do mapa
        alvo = 14 if hs >= 75 else 10
        for _ in range(600):
            if len(feitas) >= min(alvo, 32): break
            tenta(rnd.uniform(-hs, hs), rnd.uniform(-hs, hs), rnd.uniform(7, 16), rnd.uniform(7, 16), rnd.uniform(0.9, 2.4), rnd.uniform(5.5, 8.5))
        m["elevations"] = feitas[:32]
        json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        total += len(feitas)
        print(f"  {m['id']}: {len(feitas)} elevações")
    print("elevações:", total)

main()

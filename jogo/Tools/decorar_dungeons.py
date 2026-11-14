#!/usr/bin/env python3
"""
13/11 22:26 ("estamos usando tudo que foi feito?"): o pacote de arte de 13/11 trouxe peças de dungeon por tema que nunca
entraram (as dungeons do meio do jogo eram só muralha e tocha). Este script espalha essas peças em cada dungeon, pelo tema:
  templo (Mosteiro Afundado): coluna com musgo, piso alagado, altar submerso
  forja (Fornalha, Cidadela de Escória): caldeirão de forja, grade de ferro, poça de lava
  gelo (Salão e Fortaleza da Geada): pilar rúnico, trono de gelo (no chefe)
  labirinto (Labirinto do Juramento): sebe, parede de bronze, estátua do minotauro, porta de bronze
  todas: placa do chefe perto do chefe e placa de saída perto da saída
A colisão sai sozinha do tamanho de cada modelo (Shared/World/PecasMedidas.cs). Idempotente: o que este script põe leva
"acervo": true e é refeito a cada rodada. Rodar depois de gerar_regioes.py.
"""
import json, math, os, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
TEMAS = {
    "mosteiro_afundado": [("dg/dg_coluna_musgo", 10, 1.0), ("dg/dg_piso_alagado", 6, 1.6), ("dg/dg_altar_submerso", 1, 1.5)],
    "fornalha": [("dg/dg_caldeirao_forja", 4, 1.2), ("dg/dg_grade_ferro", 4, 1.4), ("dg/dg_poca_lava", 5, 1.4)],
    "cidadela_escoria": [("dg/dg_caldeirao_forja", 3, 1.2), ("dg/dg_grade_ferro", 5, 1.4), ("dg/dg_poca_lava", 6, 1.4)],
    "salao_geada": [("dg/dg_pilar_runico", 8, 1.0)],
    "fortaleza_geada": [("dg/dg_pilar_runico", 8, 1.0)],
    "labirinto_juramento": [("dg/dg_parede_sebe", 8, 2.2), ("dg/dg_parede_bronze", 5, 2.2), ("dg/dg_estatua_minotauro", 2, 1.4)],
}
TRONO = {"salao_geada": "dg/dg_trono_gelo", "fortaleza_geada": "dg/dg_trono_gelo", "labirinto_juramento": "dg/dg_porta_bronze"}


def livre(m, x, y, r):
    H = m["half_size"]
    if abs(x) > H - 3 or abs(y) > H - 3:
        return False
    folga = r + 1.6   # 13/11: dungeons em labirinto — a peça nunca fecha corredor (sobra 1,6 m em volta)
    for o in m.get("obstacles", []):
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + folga: return False
        elif abs(x - o["x"]) < o["w"] / 2 + folga and abs(y - o["y"]) < o["h"] / 2 + folga:
            return False
    perto = [(p["x"], p["y"], 4.5) for p in m.get("portals", [])] + [(s["x"], s["y"], 2.5) for s in m.get("spawns", [])] \
        + [(c["x"], c["y"], 2.0) for c in m.get("chests", [])] + [(o["x"], o["y"], 2.0) for o in m.get("objects", [])] \
        + [(p["x"], p["y"], 1.6) for p in m.get("props", [])] + [(m.get("spawn_x", 0), m.get("spawn_y", 0), 4)]
    return all(math.hypot(x - a, y - b) >= rr + r for a, b, rr in perto)


def perto_de(m, x0, y0, r, rnd):
    for raio in [1.5 + i * 0.5 for i in range(20)]:
        for _ in range(16):
            a = rnd.uniform(0, 2 * math.pi)
            x, y = round(x0 + math.cos(a) * raio, 2), round(y0 + math.sin(a) * raio, 2)
            if livre(m, x, y, r):
                return x, y
    return None


def main():
    for f in sorted(os.listdir(C)):
        if not f.startswith("map_"): continue
        caminho = os.path.join(C, f)
        m = json.load(open(caminho, encoding="utf-8"))
        if m.get("kind") != "dungeon" or m["id"].startswith("merlin_"): continue
        m["props"] = [p for p in m.get("props", []) if not p.get("acervo")]
        rnd = random.Random(m["id"])
        H = m["half_size"]
        postos = 0
        for arq, n, r in TEMAS.get(m["id"], []):
            k = 0
            for _ in range(n * 200):
                if k >= n: break
                x, y = round(rnd.uniform(-H + 4, H - 4), 2), round(rnd.uniform(-H + 4, H - 4), 2)
                if not livre(m, x, y, r): continue
                m["props"].append({"kind": "glb:" + arq, "x": x, "y": y, "yaw": rnd.randrange(0, 360, 90), "acervo": True})
                k += 1
            postos += k
        chefe = next((s for s in m.get("spawns", []) if "chefe" in s["monster"] or s.get("boss")), None) or (m["spawns"][-1] if m.get("spawns") else None)
        if chefe and (p := perto_de(m, chefe["x"], chefe["y"], 0.6, rnd)):
            m["props"].append({"kind": "glb:dg/dg_placa_chefe", "x": p[0], "y": p[1], "yaw": 0, "acervo": True}); postos += 1
            if m["id"] in TRONO and (q := perto_de(m, chefe["x"], chefe["y"], 1.4, rnd)):
                m["props"].append({"kind": "glb:" + TRONO[m["id"]], "x": q[0], "y": q[1], "yaw": 0, "acervo": True}); postos += 1
        saida = next((p for p in m.get("portals", []) if p["id"].endswith("_saida")), None)
        if saida and (p := perto_de(m, saida["x"], saida["y"], 0.6, rnd)):
            m["props"].append({"kind": "glb:dg/dg_placa_saida", "x": p[0], "y": p[1], "yaw": 0, "acervo": True}); postos += 1
        json.dump(m, open(caminho, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        print(f"  {m['id']}: {postos} peças do acervo")


main()

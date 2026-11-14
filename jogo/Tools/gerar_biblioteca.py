#!/usr/bin/env python3
"""
12/11 (pedido do dono, com referência: "Biblioteca do Continente como a da foto — salão comprido, estantes altas dos dois
lados, vitrines no meio, clarabóias no teto"). Reescreve Content/map_biblioteca_continente.json mantendo ids (livros, NPCs,
saída). Salão 16 × 30 m: entrada ao sul (y=7), mesa do arquivista no fundo (norte). Estantes encostadas nas paredes e
alcovas de leitura (estantes perpendiculares) com um atril de livro cada; vitrines e mesas de leitura no corredor central.
O cliente desenha paredes de pedra, pilares, teto com clarabóias e feixes de luz (MapView.BibliotecaGrande).
"""
import json, os
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
P = os.path.join(RAIZ, "Content", "map_biblioteca_continente.json")
m = json.load(open(P, encoding="utf-8"))

def box(kind, x, y, w, h, alt=4.5):
    return {"kind": kind, "shape": "box", "x": x, "y": y, "w": w, "h": h, "r": 0, "height": alt}

X0, X1, Y0, Y1 = -8.0, 8.0, -23.0, 7.0
obst = [box("parede_bib", X0 - 0.5, (Y0 + Y1) / 2, 1.0, Y1 - Y0 + 1, 7), box("parede_bib", X1 + 0.5, (Y0 + Y1) / 2, 1.0, Y1 - Y0 + 1, 7),
        box("parede_bib", 0, Y0 - 0.5, X1 - X0 + 2, 1.0, 7),
        box("parede_bib", (X0 - 1 + -1.6) / 2, Y1 + 0.5, (-1.6) - (X0 - 1), 1.0, 7), box("parede_bib", (1.6 + X1 + 1) / 2, Y1 + 0.5, (X1 + 1) - 1.6, 1.0, 7)]
# estantes nas paredes (sem cobrir a alcova inteira) e estantes perpendiculares que formam alcovas
alcovas = [-19.0, -13.0, -7.0, -1.0]
for lado in (-1, 1):
    for y in [-21.5, -16.0, -10.0, -4.0, 2.0]:
        obst.append(box("estante", lado * 7.5, y, 0.9, 2.4))
    for y in [-16.0, -10.0, -4.0]:
        obst.append(box("estante", lado * 5.6, y, 2.8, 0.7))
obst.append(box("estante", 0, -22.4, 5.0, 0.8))          # estante grande atrás da mesa do arquivista
for x in (-2.8, 2.8):
    for y in (-14.0, -6.0):
        obst.append(box("vitrine", x, y, 1.2, 0.8, 1.3))
props = [{"kind": "movel_balcao", "x": 0, "y": -19.6, "yaw": 0},
         {"kind": "movel_mesa", "x": 0, "y": -10.0, "yaw": 90}, {"kind": "movel_cadeira", "x": -1.1, "y": -10.0, "yaw": 90}, {"kind": "movel_cadeira", "x": 1.1, "y": -10.0, "yaw": -90},
         {"kind": "movel_mesa", "x": 0, "y": -2.0, "yaw": 90}, {"kind": "movel_cadeira", "x": -1.1, "y": -2.0, "yaw": 90}, {"kind": "movel_cadeira", "x": 1.1, "y": -2.0, "yaw": -90},
         {"kind": "movel_tapete", "x": 0, "y": 3.2, "yaw": 0}, {"kind": "movel_tapete", "x": 0, "y": -16.5, "yaw": 0}]
# livros: um atril em cada alcova (8) + 3 no corredor; o lado de "x + 0,8" fica livre (o teste anda até lá)
lugares = [(-6.6, -19.0), (6.0, -19.0), (-6.6, -13.0), (6.0, -13.0), (-6.6, -7.0), (6.0, -7.0), (-6.6, -1.0), (6.0, -1.0), (-4.2, 4.0), (3.6, 4.0), (-1.6, -16.0)]
for o, (x, y) in zip(m["objects"], lugares):
    o["x"], o["y"] = x, y
for n in m["npcs"]:
    if n["id"] == "arquivista_severo": n.update(x=0.0, y=-20.9, facing=0)
    if n["id"] == "irma_clarabela": n.update(x=3.4, y=1.6, facing=270)
m.update(half_size=26, spawn_x=0, spawn_y=5.0, obstacles=obst, props=props)
m["portals"] = [{"id": "biblioteca_saida", "x": 0, "y": 7.6, "radius": 1.1, "to_map": "vale_das_cinzas", "to_x": m["exit_x"], "to_y": m["exit_y"], "label": "Sair para o Vale"}]
m["zones"] = [{"id": "salao", "name": "Grande Salão", "kind": "seguro", "x": 0, "y": -8, "w": 16, "h": 30}]
json.dump(m, open(P, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("biblioteca: salão", X1 - X0, "x", Y1 - Y0, "m,", len(obst), "obstáculos")

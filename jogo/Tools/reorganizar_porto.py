#!/usr/bin/env python3
"""
Porto das Cinzas com os prédios do lote 2 (pedido 10/11: "cidade mais viva, com as casas de 2 andares"):
- Taverna Grande (noroeste) e Casarão da Cidade (nordeste): térreo visitável — a colisão vem das caixas do
  `Tools/colisao/<id>_colisao.glb` (só o que bate na altura do corpo: piso, vergas e o andar de cima ficam de fora);
- sobrados e casas pequenas no lugar das casas antigas (bloco cheio: por dentro ainda não tem nada);
- Portão Fortificado na saída para a Costa (grade levantada); cercas em volta da horta.
Idempotente: tudo o que este script põe leva "kit": true e é refeito; as casas antigas ("casa") saem.
Uso (raiz do projeto): python3 Tools/reorganizar_porto.py
"""
import json, math, os, shutil, sys

R = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
sys.path.insert(0, os.path.join(R, "Tools"))
from caixas_colisao import boxes  # noqa: E402

F = os.path.join(R, "Content", "map_porto.json")
backup = os.path.join(R, "backups", "map_porto_antes_lote2.json")
if not os.path.exists(backup):
    os.makedirs(os.path.dirname(backup), exist_ok=True)
    shutil.copy2(F, backup)

with open(F, encoding="utf-8") as h:
    m = json.load(h)

K = {"kit": True}
m["obstacles"] = [o for o in m["obstacles"] if not o.get("kit") and o["kind"] != "casa"]
m["props"] = [p for p in m["props"] if not p.get("kit") and p["kind"] != "portao_cidade"]
# pinheiros dos cantos de cima ficavam dentro da taverna e do casarão
m["props"] = [p for p in m["props"] if not (p["kind"] == "pinheiros" and p["y"] < -20)]
# árvore que ficava na quina do casarão: um pouco para dentro da praça
for o in m["obstacles"]:
    if o["kind"] == "arvore" and o["x"] == 12 and o["y"] == -14:
        o["x"] = 9.5


def prop(kind, x, y, yaw=0):
    m["props"].append({"kind": kind, "x": x, "y": y, "yaw": yaw, **K})


def block(x, y, w, h, height=4):
    m["obstacles"].append({"kind": "invisivel", "shape": "box", "x": round(x, 2), "y": round(y, 2), "w": round(w, 2),
                           "h": round(h, 2), "r": 0, "height": height, **K})


def rot(x, z, yaw):
    """Giro do Godot em Y (graus): x' = x·cos + z·sen; z' = −x·sen + z·cos."""
    a = math.radians(yaw)
    return x * math.cos(a) + z * math.sin(a), -x * math.sin(a) + z * math.cos(a)


def building(kind, model, x, y, yaw, skip=lambda mn, mx: False):
    """Prédio com colisão do arquivo: caixas que pegam o corpo (0,6–1,8 m) viram obstáculos do servidor."""
    prop(kind, x, y, yaw)
    for mn, mx in boxes(os.path.join(R, "Tools", "colisao", model + "_colisao.glb")):
        if mn[1] >= 1.8 or mx[1] <= 0.6 or skip(mn, mx):
            continue
        cx, cz = rot((mn[0] + mx[0]) / 2, (mn[2] + mx[2]) / 2, yaw)
        w, d = mx[0] - mn[0], mx[2] - mn[2]
        if round(yaw) % 180 != 0:
            w, d = d, w
        block(x + cx, y + cz, max(w, 0.3), max(d, 0.3), round(mx[1], 1))


def solid(kind, x, y, yaw, w, d):
    """Casa fechada: a colisão é a planta inteira."""
    prop(kind, x, y, yaw)
    if round(yaw) % 180 != 0:
        w, d = d, w
    block(x, y, w, d, 6)


# ---- noroeste: Taverna Grande, entrada virada para a praça ----
building("taverna_grande", "edificio_taverna_grande", -20.0, -17.5, 0)
# ---- nordeste: Casarão da Cidade (2 andares; térreo visitável) ----
# 11/11 (escada de verdade, GUIA_ESCADA_CASA): a caixa da escada sai (o boneco sobe por ela); o corrimão (x local -5,58) fica
building("casarao_cidade", "edificio_casarao_cidade", 20.0, -17.0, 0,
         skip=lambda mn, mx: abs((mn[0] + mx[0]) / 2 + 6.5) < 0.2 and (mx[0] - mn[0]) < 2.0 and (mx[2] - mn[2]) > 3.5)
block(20.0 - 5.55, -17.0 + 2.75, 0.15, 4.3, 4.5)
# ---- sul (do lado da praça): sobrados e casas pequenas, fachada para o centro ----
solid("sobrado", -21.0, 16.0, 180, 5.3, 4.8)
solid("casa_pequena", -14.5, 17.5, 180, 4.7, 4.4)
solid("sobrado", 20.0, 15.5, 180, 5.3, 4.8)        # a casa do jogador (portal atrás, em 20; 21)
solid("casa_pequena", 13.5, 17.5, 180, 4.7, 4.4)
solid("casa_pequena", -7.0, 21.0, 180, 4.7, 4.4)
solid("casa_pequena", 7.0, 21.0, 180, 4.7, 4.4)
# ---- oeste: sobrado de frente para a praça ----
solid("sobrado", -24.5, 0.0, 90, 5.3, 4.8)
# ---- saída para a Costa: Portão Fortificado (grade em cima: passa por baixo) ----
building("portao_fortificado", "edificio_portao_fortificado", 0.0, -28.0, 0,
         skip=lambda mn, mx: abs(mn[0]) <= 2.0 and abs(mx[0]) <= 2.0)   # a grade fica levantada
# ---- horta: cerca baixa no sul e no oeste (entrada pelo leste) ----
for i in range(3):
    prop("cerca", -18.4 + i * 2.4, 1.0, 0)
    prop("cerca", -20.4, 2.2 + i * 2.4, 90)
block(-16.0, 1.0, 7.2, 0.3, 1.1)
block(-20.4, 4.6, 0.3, 7.2, 1.1)

with open(F, "w", encoding="utf-8") as h:
    h.write(json.dumps(m, ensure_ascii=False, indent=2) + "\n")
print("Porto com o lote 2:", sum(1 for p in m["props"] if p.get("kit")), "peças,",
      sum(1 for o in m["obstacles"] if o.get("kit")), "caixas de colisão")

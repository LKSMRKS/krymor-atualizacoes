#!/usr/bin/env python3
"""
Costa do Cal reorganizada por territórios (direção "mapas, leitura visual e gameplay", 09/11):
- círculo seguro colado no portão do Porto (quem chega já está protegido);
- tira os monstros espalhados ao acaso ("fill") e põe bolsões de caça com identidade, do mais fraco (perto do portão)
  ao mais forte (atrás da ravina);
- família de caranguejos do lote 2 em territórios que combinam com o tamanho e a força de cada um:
  Rochedo do Cal (Caranguejo de Pedra, nv 6) · Praia dos Destroços (Ermitão do Barril, nv 7) ·
  Areal Fundo (Caranguejo Escavador, nv 9) · Enseada do Tirano (Tirano da Maré, chefe de campo nv 12, sul).
Idempotente: tudo o que este script põe leva "territorio": true e é refeito do zero.
Uso (raiz do projeto): python3 Tools/reorganizar_costa.py
"""
import json, os

C = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Content")


def load(f):
    with open(os.path.join(C, f), encoding="utf-8") as h:
        return json.load(h)


def save(f, d):
    with open(os.path.join(C, f), "w", encoding="utf-8") as h:
        h.write(json.dumps(d, ensure_ascii=False, indent=2) + "\n")


m = load("map_costa.json")

# ---- círculo seguro no portão (pedido do usuário): quem chega do Porto (0, 96) e quem renasce fica dentro ----
m["spawn_x"], m["spawn_y"], m["safe_radius"] = 0.0, 98.4, 5.5

# ---- limpa o que é deste script e os monstros espalhados ao acaso ----
m["spawns"] = [s for s in m["spawns"] if not s.get("fill") and not s.get("territorio")]
m["zones"] = [z for z in m["zones"] if not z.get("territorio")]
m["props"] = [p for p in m["props"] if not p.get("territorio")]
m["obstacles"] = [o for o in m["obstacles"] if not o.get("territorio")]

T = {"territorio": True}


def zone(id, name, kind, x, y, w, h):
    m["zones"].append({"id": id, "name": name, "kind": kind, "x": x, "y": y, "w": w, "h": h, **T})


def pack(id, mon, lvl, pts, resp=30, n=1):
    """Um bolsão: n monstros em cada ponto (grupo pequeno que se ajuda)."""
    for i, (x, y) in enumerate(pts):
        for k in range(n):
            ox = (k % 2) * 1.6 - 0.8 if n > 1 else 0
            oy = (k // 2) * 1.6 if n > 1 else 0
            m["spawns"].append({"id": f"{id}{i}_{k}", "monster": mon, "x": round(x + ox, 1), "y": round(y + oy, 1),
                                "level": lvl, "group": f"{id}{i}", "respawn_seconds": resp, **T})


def prop(kind, x, y, yaw=0):
    m["props"].append({"kind": kind, "x": x, "y": y, "yaw": yaw, **T})


def rock(x, y, r, h):
    m["obstacles"].append({"kind": "rocha", "shape": "circle", "x": x, "y": y, "w": 0, "h": 0, "r": r, "height": h, **T})


# ---- perto do portão: só o muito fraco (Caranguejinho nv 1–2), espaço livre para aprender ----
pack("tp_cr", "monster_r0_caranguejinho", 2, [(44, 101), (60, 97), (-44, 101)], resp=20, n=2)

# ---- noroeste: Praia dos Barcos — Caranguejo de Cal (nv 3) e Ratos do Cais; casco de barco como marco ----
zone("praia_barcos", "Praia dos Barcos", "fraco", -84, 90, 40, 26)
pack("tb_cal", "monster_r1_caranguejo_de_cal", 3, [(-90, 95), (-78, 86), (-96, 82)], n=2)
pack("tb_rato", "monster_bst_rato_do_cais", 2, [(-70, 99)], resp=24, n=2)
prop("barco_encalhado", -93, 101, 200)
prop("redes", -82, 99, 30)
prop("caixotes", -72, 94, 0)

# ---- nordeste: Rochedo do Cal — Caranguejo de Pedra (nv 6): lento, fecha a carapaça e dispara espinhos ----
zone("rochedo", "Rochedo do Cal", "medio", 86, 76, 40, 34)
pack("tr_pedra", "monster_r1_caranguejo_de_pedra", 6, [(82, 87), (96, 74), (77, 66)], resp=40, n=2)
for (x, y, r, h) in [(88, 81, 1.8, 2.4), (100, 88, 1.5, 2.0), (71, 75, 1.4, 1.8), (92, 64, 2.0, 2.6), (80, 94, 1.2, 1.5)]:
    rock(x, y, r, h)
prop("ossada", 86, 70, 40)

# ---- leste: Praia dos Destroços — Ermitão do Barril (nv 7): barris de um naufrágio, investida e barril explosivo ----
zone("destrocos", "Praia dos Destroços", "medio", 93, 22, 26, 36)
pack("te_erm", "monster_r1_caranguejo_ermitao", 7, [(96, 36), (90, 18), (100, 8)], resp=40, n=2)
prop("barco_encalhado", 101, 27, 100)
for (x, y) in [(97, 30), (88, 26), (99, 14), (86, 10)]:
    prop("barril", x, y, 0)
prop("caixotes", 92, 31, 15)
prop("redes", 84, 16, 70)

# ---- sudoeste, junto do Altar das Cascas: Areal Fundo — Caranguejo Escavador (nv 9): some no chão e sai embaixo ----
zone("areal_fundo", "Areal Fundo", "forte", -82, -32, 46, 26)
pack("tf_esc", "monster_r1_caranguejo_escavador", 9, [(-98, -38), (-66, -42), (-62, -22)], resp=50)
prop("rachadura", -86, -40, 0)
prop("rachadura", -66, -30, 60)
prop("estacas", -74, -22, 20)

# ---- sul (atrás da ravina): Acampamento da Vigília — saqueadores nv 11–13 a caminho do Vale ----
zone("vigilia_sul", "Acampamento da Vigília", "forte", 34, -90, 46, 30)
pack("tv_saq", "monster_r1_saqueador_da_vigilia", 11, [(28, -78)], resp=40, n=3)
pack("tv_saq2", "monster_r1_saqueador_da_vigilia", 12, [(60, -80)], resp=40, n=2)
pack("tv_saq3", "monster_r1_saqueador_da_vigilia", 13, [(22, -101)], resp=45, n=2)
prop("tenda", 38, -92, 20)
prop("tenda", 46, -86, 300)
prop("fogueira", 40, -88, 0)
prop("estandarte", 30, -84, 0)

# ---- Praia da Baía: Caranguejos de Cal maiores (nv 10) antes do Rei das Pinças ----
pack("tbb_cal", "monster_r1_caranguejo_de_cal", 10, [(-36, -74), (-30, -96)], resp=35, n=2)

# ---- sudeste: Enseada do Tirano — chefe de campo (nv 12). Ossos e destroços avisam de longe ----
zone("enseada_tirano", "Enseada do Tirano", "chefe", 88, -88, 36, 30)
m["spawns"].append({"id": "tirano", "monster": "monster_r1_tirano_da_mare", "x": 90, "y": -91, "level": 12,
                    "group": "tirano", "respawn_seconds": 900, **T})
# ossada gigante (Cal Lote 2): ~37 m ao comprido (norte–sul), meio enterrada; a colisão cobre crânio e coluna
m["obstacles"].append({"kind": "ossada_gigante", "shape": "box", "x": 80.0, "y": -89.5, "w": 4.6, "h": 30.0, "r": 0, "height": 8, **T})
for (k, x, y, yaw) in [("cranios", 98, -84, 0), ("ossada", 96, -98, 200),
                       ("redes", 76, -96, 120), ("estacas", 72, -80, 0), ("cranios", 84, -100, 90)]:
    prop(k, x, y, yaw)
for (x, y, r, h) in [(102, -78, 2.4, 3.2), (74, -102, 2.0, 2.6), (104, -100, 1.8, 2.2)]:
    rock(x, y, r, h)

# ---- criaturas do bestiário que já viviam aqui, agora em bolsões fixos ----
pack("tm_mel", "monster_bst_meleca", 5, [(-40, -33), (-18, -33)], resp=30, n=2)
pack("tm_rato", "monster_bst_rato_do_cais", 3, [(25, 38), (-10, 32)], resp=24, n=2)
pack("tm_ratz", "monster_bst_rato_gigante", 11, [(-44, -100)], resp=60)


# ---- lote 3 / kit da Costa ----
def block(x, y, w, h):
    """Colisão do servidor para peças do kit (o desenho vem do prop)."""
    m["obstacles"].append({"kind": "invisivel", "shape": "box", "x": x, "y": y, "w": w, "h": h, "r": 0, "height": 3, **T})


# lagoa numa clareira ao norte do Acampamento das Dunas (água: não se atravessa)
m["obstacles"].append({"kind": "lago", "shape": "circle", "x": -20.0, "y": 37.0, "w": 0, "h": 0, "r": 5.5, "height": 1, **T})

# acampamento goblin das Dunas (Chefe Goblin em -64; 8,6): tendas goblin, paliçada com abertura a leste e totem
for pr in m["props"]:
    if pr["kind"] == "tenda" and pr.get("extra") and pr["x"] < -40:
        pr["kind"] = "tenda_goblin"
for (x, y, yaw, w, h) in [(-66, 17, 0, 4, 0.5), (-62, 17, 0, 4, 0.5), (-66, -1, 180, 4, 0.5), (-62, -1, 180, 4, 0.5),
                          (-72.5, 4, 90, 0.5, 4), (-72.5, 12, 90, 0.5, 4)]:
    prop("palicada", x, y, yaw)
    block(x, y, w, h)
prop("totem_goblin", -60.0, 12.5, 200)
block(-60.0, 12.5, 0.8, 0.8)

# entrada da Galeria Funda: paredão de cal com boca de mina em volta do portal (-99,2; -64), virada para leste
prop("entrada_galeria", -97.2, -64.0, 90)
block(-98.7, -69.15, 3.0, 6.3)
block(-98.7, -58.85, 3.0, 6.3)

# ---- cenário dos estudos (atualização de arte 10/11): marcos e peças contando história ----
prop("torre_partida", 60.0, -40.0, 20)                 # torre partida: marco visto de longe, perto das ruínas do farol
block(60.0, -40.0, 6.4, 6.4)
prop("trilhos", -88.0, -61.0, 90)                       # trilhos e vagonete saindo da boca da Galeria
prop("vagonete", -88.0, -61.0, 90)
block(-88.0, -61.0, 1.6, 1.2)
prop("carroca", 26.0, 64.0, 35)                         # carroça quebrada à beira da estrada
block(26.0, 64.0, 2.6, 3.0)
for (x, y, k, yaw) in [(70, 84, "a", 10), (99, 62, "b", 70), (79, 57, "c", 200)]:   # blocos de cal cortados no Rochedo
    prop("blocos_cal_" + k, x, y, yaw)
    block(x, y, 1.8, 1.6)
for (x, y) in [(31, -86), (45, -95), (25, -97)]:        # tochas no Acampamento da Vigília
    prop("tocha_poste", x, y, 0)
prop("estandarte_tridente", 50.0, -84.0, 200)

# ---- nenhum monstro nasce dentro de árvore/rocha: empurra para fora ----
def inside(x, y):
    for o in m["obstacles"]:
        if o.get("solid") is False: continue
        if o["shape"] == "circle":
            if (x - o["x"]) ** 2 + (y - o["y"]) ** 2 < (o["r"] + 1.2) ** 2: return o
        elif abs(x - o["x"]) < o["w"] / 2 + 1.2 and abs(y - o["y"]) < o["h"] / 2 + 1.2: return o
    for p in m["props"]:   # objetos com colisão (MapGeometry.PropRadius)
        r = {"pinheiros": 1.1, "tenda": 1.8, "caixotes": 0.8, "barril": 0.45, "estacas": 0.7, "tocha_poste": 0.6, "trilhos": 1.0}.get(p["kind"], 0)
        if r and (x - p["x"]) ** 2 + (y - p["y"]) ** 2 < (r + 1.2) ** 2: return p
    return None


for sp in m["spawns"]:
    if not sp.get("territorio"): continue
    for step in range(40):
        o = inside(sp["x"], sp["y"])
        if o is None: break
        # empurra para longe do centro do que está atrapalhando
        dx, dy = sp["x"] - o["x"], sp["y"] - o["y"]
        d = (dx * dx + dy * dy) ** 0.5 or 1.0
        sp["x"] = round(sp["x"] + 0.6 * dx / d, 1)
        sp["y"] = round(sp["y"] + 0.6 * dy / d, 1)

save("map_costa.json", m)
print("Costa reorganizada:", len(m["spawns"]), "monstros;", sum(1 for s in m["spawns"] if s.get("territorio")), "nos territórios novos")

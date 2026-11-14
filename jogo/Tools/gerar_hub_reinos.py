#!/usr/bin/env python3
"""
14/11 (dono: "vamos fazer um HUB nos Reinos Ocultos, igual o print do mapa"): o Coração dos Reinos — ilha de ruínas violeta
flutuando sobre o abismo, com o Anel de luz no meio, a cerejeira pálida, a cascata e os arcos de aqueduto (como na arte).
Em volta do Anel, os 6 portais dos reinos (cada um acende quando você já pode ir: livro lido na Biblioteca OU a marca do
Merlin), a pedra de viagem e as saídas para o Porto e para Astrália. A volta de cada reino agora cai aqui.
Também põe a viagem "Coração dos Reinos" na Sibila, no Merlin e no Tião (a partir do nível 20).
Idempotente. Rodar depois do gerar_merlin.py.
"""
import json, math, os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
HUB = "reinos_ocultos"


def L(n): return json.load(open(os.path.join(C, n), encoding="utf-8"))
def D(n, o): json.dump(o, open(os.path.join(C, n), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


def arquivo(mid):
    for f in os.listdir(C):
        if f.startswith("map_") and f.endswith(".json") and L(f)["id"] == mid:
            return f
    raise SystemExit("mapa não achado: " + mid)


# (reino, rótulo, marca exigida — uma das opções basta, nível, ângulo em graus)
REINOS = [
    ("reino_lua", "A Lua Vermelha", "livro:lobisomem|merlin:reinos", 35, 210),
    ("reino_transilvania", "Transilvânia", "livro:vampiro|merlin:reinos", 35, 255),
    ("reino_inferno", "O Inferno", "livro:ceifador|merlin:reinos", 35, 300),
    ("reino_celeste", "A Cidade Celestial", "livro:anjo|merlin:reinos", 35, 345),
    ("floresta_oculta", "A Floresta Oculta", "livro:mascara|merlin:floresta", 20, 120),
    ("reino_fabula", "Era Uma Vez — o Reino dos Contos", None, 24, 165),
]


def main():
    H = 28
    m = {"id": HUB, "display_name": "Coração dos Reinos", "half_size": H, "spawn_x": 0, "spawn_y": 9, "safe_radius": 2 * H,
         "xp_multiplier": 1, "kind": "cidade", "region": "reinos", "theme": "reinos", "no_mount": False,
         "zones": [{"id": "coracao", "name": "Coração dos Reinos", "kind": "seguro", "x": 0, "y": 0, "w": 2 * H, "h": 2 * H}],
         "obstacles": [], "gates": [], "chests": [], "spawns": [], "props": [], "portals": [], "waypoints": [], "npcs": [],
         "objects": [], "checkpoints": []}
    P = m["props"]
    P.append({"kind": "anel_reinos", "x": 0, "y": 0, "yaw": 0})
    m["obstacles"].append({"kind": "invisivel", "shape": "circle", "x": 0, "y": 0, "w": 0, "h": 0, "r": 1.2, "height": 2})
    m["objects"].append({"id": "reinos_anel", "kind": "contemplar", "x": 0, "y": 2.6, "label": "O Anel",
                         "text": "O Anel guarda o que o Grande Arranjo dobrou para fora do mapa. Cada porta em volta leva a um reino — "
                                 "acende quando você já leu o livro dele na Biblioteca (ou quando o Merlin desfaz o Arranjo)."})
    m["waypoints"].append({"id": "wp_reinos_ocultos", "name": "Coração dos Reinos", "x": 5.5, "y": 9.0})
    # portais dos reinos num círculo de 15 m
    for rid, rot, marca, nivel, ang in REINOS:
        a = math.radians(ang)
        x, y = round(math.cos(a) * 15, 2), round(math.sin(a) * 15, 2)
        alvo = L(arquivo(rid))
        p = {"id": f"hub_{rid}", "x": x, "y": y, "radius": 1.8, "to_map": rid, "to_x": alvo.get("spawn_x", 0), "to_y": alvo.get("spawn_y", 0),
             "label": rot, "min_level": nivel, "level_lock": True}
        if marca: p["requires_flag"] = marca
        m["portals"].append(p)
        # dois pilares rúnicos ladeando cada porta
        for lado in (-1, 1):
            b = a + lado * 0.16
            P.append({"kind": "glb:dg/dg_pilar_runico", "x": round(math.cos(b) * 15.6, 2), "y": round(math.sin(b) * 15.6, 2), "yaw": round(-ang + 90)})
    # saídas: Porto (sul) e Astrália (norte, perto do Observatório do Merlin)
    m["portals"].append({"id": "hub_porto", "x": 0, "y": 24, "radius": 2.0, "to_map": "porto_das_cinzas", "to_x": 16.0, "to_y": 10.0, "label": "Porto das Cinzas"})
    m["portals"].append({"id": "hub_astralia", "x": 0, "y": -24, "radius": 2.0, "to_map": "astralia", "to_x": 0, "to_y": 8.5, "label": "Astrália (Observatório)"})
    # cenário da arte: cerejeira pálida, cascata na borda, arcos de aqueduto, colunas partidas, estátuas de sábio
    P.append({"kind": "cerejeira_reinos", "x": -9, "y": 6, "yaw": 0}); m["obstacles"].append({"kind": "invisivel", "shape": "circle", "x": -9, "y": 6, "w": 0, "h": 0, "r": 0.7, "height": 3})
    P.append({"kind": "cascata_reinos", "x": 38, "y": 0, "yaw": 90})   # na beira da ilha, caindo no abismo
    for x, y, yaw in ((26, -23, 45), (-26, -23, -45), (-26, 23, 45)):
        P.append({"kind": "glb:cenario_ponte_arcos", "x": x, "y": y, "yaw": yaw})
    for i, (x, y) in enumerate(((-6, -7), (6, -7), (-8, 3), (8, 3), (-22, 6), (22, -8), (-23, -9), (12, 22), (-12, 22))):
        P.append({"kind": "glb:cenario/acropole_coluna_" + ("a" if i % 2 else "b"), "x": x, "y": y, "yaw": (i * 47) % 360})
    for x, y in ((-4, -21), (4, -21)):
        P.append({"kind": "glb:dg/dg_estatua_sabio", "x": x, "y": y, "yaw": 0})
    # a Guardiã do Anel
    m["npcs"].append({"id": "guardia_anel", "name": "Aurora, a Guardiã do Anel", "role": "morador", "x": 3.0, "y": 5.0, "facing": 180, "look": "arcanista",
                      "greeting": "«Bem-vindo ao Coração dos Reinos. Seis portas, seis lugares que não cabem em mapa nenhum. "
                                  "As que estão vermelhas ainda não reconhecem você: leia o livro certo na Biblioteca do Continente — "
                                  "ou termine o que o Merlin começou.»"})
    D("map_reinos_ocultos.json", m)
    # volta de cada reino -> o hub (na frente da porta daquele reino)
    for rid, rot, marca, nivel, ang in REINOS:
        f = arquivo(rid); r = L(f)
        a = math.radians(ang)
        for p in r["portals"]:
            if p["id"].endswith("_volta"):
                p["to_map"] = HUB; p["to_x"] = round(math.cos(a) * 11, 2); p["to_y"] = round(math.sin(a) * 11, 2)
                p["label"] = "Voltar ao Coração dos Reinos"
        D(f, r)
    # viagem ao hub: Sibila (Porto), Merlin (Astrália), Tião (Porto)
    for mid, npc in (("porto_das_cinzas", "sibila_vidente"), ("astralia", "merlin_mago"), ("porto_das_cinzas", "bobo_tiao")):
        f = arquivo(mid); mm = L(f)
        for n in mm["npcs"]:
            if n["id"] != npc: continue
            n["travel"] = [t for t in n.get("travel", []) if t.get("id") != "coracao_reinos"]
            n["travel"].insert(0, {"id": "coracao_reinos", "label": "Ir ao Coração dos Reinos (os Reinos Ocultos)", "to_map": HUB, "to_x": 0, "to_y": 11,
                                   "price": 150, "min_level": 20})
        D(f, mm)
    w = L("world.json")
    if "map_reinos_ocultos.json" not in w["map_files"]: w["map_files"].append("map_reinos_ocultos.json")
    for r in w["regions"]:
        if r["id"] == "reinos" and HUB not in r["maps"]:
            r["maps"].insert(0, HUB)
            r["description"] = ("O Coração dos Reinos: uma ilha de ruínas violeta sobre o abismo, com o Anel no meio e seis portas — a Lua Vermelha, "
                                "a Transilvânia, o Inferno, a Cidade Celestial, a Floresta Oculta e o Reino dos Contos. Chega-se pela Sibila, pelo Tião ou pelo Merlin.")
    D("world.json", w)
    print("hub dos reinos:", len(m["portals"]), "portais,", len(P), "peças")


main()

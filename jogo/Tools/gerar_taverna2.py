#!/usr/bin/env python3
"""
13/11 (pedido do dono: "olhando para a saída, a casa da esquerda ainda não tem escada pra subir"; "a Gilda... coloca ela na
casa embaixo onde tem o jogador de carta em cima"):
  - TAVERNA GRANDE (Porto, centro -20,-17.5, giro 0): escada interna encostada na parede direita (desenhada pelo cliente,
    EscadaCasa.ConstruirEscadaTaverna: x local 4,3..5,5, base z +3,9, topo z -0,2); corrimão invisível do lado de dentro;
    passagem no topo -> mapa "taverna_andar2" (quartos de hóspede, 2º andar no mesmo lugar, modelo 4,7 m abaixo).
  - Gilda vai para o térreo do casarão (o Dado joga cartas no 2º andar).
Idempotente. Rodar depois de gerar_interiores.py.
"""
import json, os

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
TX, TY = -20.0, -17.5          # centro da taverna no Porto (giro 0: local x -> mapa x, local z -> mapa y)
EX0, EX1, ZB, ZT = 3.85, 5.05, 3.5, -0.3   # 13/11: saiu da quina (era 4,3..5,5 / 3,9 / -0,2)
CX = (EX0 + EX1) / 2

def L(path): return json.load(open(path, encoding="utf-8"))
def D(path, obj): json.dump(obj, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

def main():
    pp = os.path.join(C, "map_porto.json")
    porto = L(pp)
    porto["obstacles"] = [o for o in porto["obstacles"] if not str(o.get("id", "")).startswith("taverna_escada")]
    porto["portals"] = [p for p in porto["portals"] if p["id"] != "taverna_subir"]
    # corrimão do lado de dentro (a escada fica entre ele, a parede direita e o armário do fundo): só se entra pela base
    porto["obstacles"].append({"id": "taverna_escada_corrimao", "kind": "invisivel", "shape": "box", "x": round(TX + EX0 - 0.12, 2),
                               "y": round(TY + (ZT + 2.9) / 2, 2), "w": 0.15, "h": round(2.9 - ZT, 2), "r": 0, "height": 1.2})
    porto["portals"].append({"id": "taverna_subir", "x": round(TX + CX, 2), "y": round(TY + ZT + 0.35, 2), "radius": 0.55,
                             "to_map": "taverna_andar2", "to_x": 3.4, "to_y": -1.6, "label": "Subir ao 2º andar"})
    # 13/11 ("só alguns bancos têm a mecânica de sentar; coloca em todos"): os 4 bancos da fonte (PracaDoPorto.cs) viram
    # props "banco_praca" de verdade (o cliente parou de desenhá-los à parte), virados para a fonte (0, 2)
    import math
    porto["props"] = [p for p in porto["props"] if p.get("poi") != "banco_fonte"]
    for bx, by in ((-4.6, -1.6), (4.6, -1.6), (-4.6, 6.4), (4.6, 6.4)):
        yaw = round(math.degrees(math.atan2(0 - bx, 2 - by)), 1)
        porto["props"].append({"kind": "banco_praca", "x": bx, "y": by, "yaw": yaw, "poi": "banco_fonte"})
    # 13/11 ("os bonecos atrás do Mestre Dorval: coloca a interação de ataque, pra testar o dano"): os 3 bonecos viram
    # monstros parados (monster_boneco_treino: muita vida, não bate, não dá XP nem saque, renasce em 3 s)
    bonecos = [(p["x"], p["y"], p.get("yaw", 90)) for p in porto["props"] if p["kind"] == "boneco_treino"]
    porto["props"] = [p for p in porto["props"] if p["kind"] != "boneco_treino"]
    porto["spawns"] = [s for s in porto["spawns"] if not s["id"].startswith("boneco_treino")]
    if not bonecos:
        bonecos = [(-24.6, -4.6, 90), (-24.9, -6.6, 80), (-24.6, -8.6, 100)]
    porto["bonecos_treino"] = [{"x": x, "y": y, "yaw": yaw} for x, y, yaw in bonecos] if "bonecos_treino" not in porto else porto["bonecos_treino"]
    for i, b in enumerate(porto["bonecos_treino"]):
        porto["spawns"].append({"id": f"boneco_treino_{i}", "monster": "monster_boneco_treino", "x": b["x"], "y": b["y"], "level": 1,
                                "group": f"boneco_treino_{i}", "respawn_seconds": 3})
    for n in porto["npcs"]:
        if n["id"] == "taverneira_gilda":
            n["x"], n["y"], n["facing"] = 23.5, -19.6, 180
    D(pp, porto)

    w = 6.6; d = 4.85
    andar = {
        "id": "taverna_andar2", "display_name": "Taverna — 2º andar", "half_size": 10, "spawn_x": 3.4, "spawn_y": -1.6,
        "safe_radius": 30, "xp_multiplier": 1, "kind": "cidade", "region": "porto", "theme": "casarao", "no_mount": True,
        "exit_map": "porto_das_cinzas", "exit_x": round(TX + 3.3, 2), "exit_y": round(TY + 3.0, 2),
        "zones": [{"id": "taverna2", "name": "Taverna — quartos de hóspede", "kind": "seguro", "x": 0, "y": 0, "w": 13, "h": 9.6}],
        "gates": [], "chests": [], "spawns": [], "waypoints": [], "objects": [{"id": "taverna2_cama1", "kind": "cama", "x": -3.8, "y": -3.4, "label": "Dormir no quarto de hóspedes"}, {"id": "taverna2_cama2", "kind": "cama", "x": -1.1, "y": -3.2, "label": "Dormir no quarto de hóspedes"}],   # 14/11 "checkpoints": [],
        "obstacles": [
            {"kind": "invisivel", "shape": "box", "x": 0, "y": -d - 0.25, "w": 2 * w + 1, "h": 0.5, "r": 0, "height": 3},
            {"kind": "invisivel", "shape": "box", "x": 0, "y": d + 0.25, "w": 2 * w + 1, "h": 0.5, "r": 0, "height": 3},
            {"kind": "invisivel", "shape": "box", "x": -w - 0.25, "y": 0, "w": 0.5, "h": 2 * d + 1, "r": 0, "height": 3},
            {"kind": "invisivel", "shape": "box", "x": w + 0.25, "y": 0, "w": 0.5, "h": 2 * d + 1, "r": 0, "height": 3},
            # guarda-corpo do vão da escada (lado de dentro e a frente)
            {"kind": "invisivel", "shape": "box", "x": round(EX0 - 0.12, 2), "y": round((ZT + 0.6 + ZB) / 2, 2), "w": 0.15, "h": round(ZB - ZT - 0.6, 2), "r": 0, "height": 1.2},
            {"kind": "invisivel", "shape": "box", "x": CX, "y": round(ZB + 0.12, 2), "w": round(EX1 - EX0 + 0.3, 2), "h": 0.15, "r": 0, "height": 1.2},
            # camas e baús (sólidos)
            {"kind": "invisivel", "shape": "box", "x": -5.2, "y": -3.2, "w": 1.3, "h": 2.2, "r": 0, "height": 0.8},
            {"kind": "invisivel", "shape": "box", "x": -2.4, "y": -3.2, "w": 1.3, "h": 2.2, "r": 0, "height": 0.8},
            {"kind": "invisivel", "shape": "box", "x": -4.0, "y": 2.4, "w": 1.4, "h": 1.0, "r": 0, "height": 0.9},
        ],
        "portals": [{"id": "taverna_descer", "x": CX, "y": round(ZT + 1.0, 2), "radius": 0.55, "to_map": "porto_das_cinzas",
                     "to_x": round(TX + 3.3, 2), "to_y": round(TY + 3.0, 2), "label": "Descer a escada"}],
        "props": [
            {"kind": "movel_cama", "x": -5.2, "y": -3.2, "yaw": 0}, {"kind": "movel_cama", "x": -2.4, "y": -3.2, "yaw": 0},
            {"kind": "movel_bau", "x": -5.2, "y": -1.6, "yaw": 0}, {"kind": "movel_tapete", "x": -1.5, "y": 0.8, "yaw": 0},
            {"kind": "movel_mesa", "x": -4.0, "y": 2.4, "yaw": 180}, {"kind": "movel_cadeira", "x": -4.0, "y": 3.4, "yaw": 180},
            {"kind": "movel_prateleira", "x": 1.5, "y": -4.4, "yaw": 0}, {"kind": "movel_caixas", "x": 6.0, "y": -4.2, "yaw": 0},
        ],
        "npcs": [{"id": "hospede_otavio", "name": "Otávio, o Hóspede", "role": "morador", "x": -1.0, "y": -1.2, "facing": 180,
                  "look": "aldeao",
                  "greeting": "Alugo este quarto há três invernos. Lá embaixo é barulho a noite toda... mas daqui se ouve tudo o que os marinheiros contam. Ouvi falar de uma esfera alaranjada perto da Costa."}],
    }
    D(os.path.join(C, "map_taverna_andar2.json"), andar)

    wp = os.path.join(C, "world.json")
    world = L(wp)
    if "map_taverna_andar2.json" not in world["map_files"]:
        world["map_files"].append("map_taverna_andar2.json")
    for r in world.get("regions", []):
        if r.get("id") == "porto" and "taverna_andar2" not in r.get("maps", []):
            r.setdefault("maps", []).append("taverna_andar2")
    D(wp, world)
    print("taverna: escada + 2º andar; Gilda no casarão")

main()

#!/usr/bin/env python3
"""
12/11 (pedido do dono: "ao clicar no modo lab, o jogo já leva para uma área DIFERENTE para testar as coisas, e não abrir o
lab ali onde você está"): mapa próprio "laboratorio" — campo aberto e vazio (monstros só pelo F1), chão com RELEVO de teste
(ondulação de até 0,5 m: só aqui por enquanto), praça de bonecos de treino, pista de corrida e uma passagem de volta ao Porto.
"""
import json, os
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
H = 44
def box(k, x, y, w, h, alt=4.0): return {"kind": k, "shape": "box", "x": x, "y": y, "w": w, "h": h, "r": 0, "height": alt}
obst = [box("muralha", 0, -H + 0.6, 2 * H, 1.2), box("muralha", 0, H - 0.6, 2 * H, 1.2), box("muralha", -H + 0.6, 0, 1.2, 2 * H), box("muralha", H - 0.6, 0, 1.2, 2 * H)]
props = [{"kind": "boneco_treino", "x": x, "y": -14.0, "yaw": 180} for x in (-9, -6, -3, 3, 6, 9)]
props += [{"kind": "placa", "x": -12, "y": 30, "yaw": 0}, {"kind": "placa", "x": 12, "y": 30, "yaw": 0},
          {"kind": "tocha", "x": -4, "y": 36, "yaw": 0}, {"kind": "tocha", "x": 4, "y": 36, "yaw": 0},
          {"kind": "estandarte", "x": 0, "y": 39, "yaw": 0}]
m = {"id": "laboratorio", "display_name": "Laboratório de Testes", "half_size": H, "spawn_x": 0, "spawn_y": 32, "safe_radius": 5,
     "xp_multiplier": 1, "kind": "campo", "region": "porto", "theme": "vale", "no_mount": False,
     "zones": [{"id": "chegada", "name": "Chegada", "kind": "seguro", "x": 0, "y": 32, "w": 16, "h": 10},
               {"id": "bonecos", "name": "Bonecos de treino", "kind": "medio", "x": 0, "y": -14, "w": 26, "h": 8},
               {"id": "campo", "name": "Campo de testes (chame monstros pelo F1)", "kind": "medio", "x": 0, "y": 6, "w": 70, "h": 30}],
     "obstacles": obst, "gates": [], "chests": [], "spawns": [], "props": props,
     "portals": [{"id": "lab_volta", "x": 0, "y": 40.5, "radius": 1.6, "to_map": "porto_das_cinzas", "to_x": 0.0, "to_y": -9.5, "label": "Voltar ao Porto"}],
     "waypoints": [], "npcs": [], "objects": [], "checkpoints": []}
json.dump(m, open(os.path.join(C, "map_laboratorio.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
w = json.load(open(os.path.join(C, "world.json"), encoding="utf-8"))
if "map_laboratorio.json" not in w["map_files"]: w["map_files"].append("map_laboratorio.json")
for r in w["regions"]:
    if r["id"] == "porto" and "laboratorio" not in r["maps"]: r["maps"].append("laboratorio")
json.dump(w, open(os.path.join(C, "world.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("laboratório pronto")

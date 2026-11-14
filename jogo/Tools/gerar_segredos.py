#!/usr/bin/env python3
"""
13/11 (pedidos: "interação com o mapa — quebrar parede, mecânicas secretas, salas secretas" e "missão das 5 esferas
escondidas: vira um lutador Macaco Vermelho"):
  - SALA SECRETA em cada mapa aberto: um quartinho de ruína 6×6 m com um lado RACHADO (portão "rachadura" sem alavanca,
    marca "parede:<mapa>"). Na frente fica a "Parede rachada" (objeto): 3 golpes (F) e ela desaba — só para você. Dentro,
    um baú secreto com o saque da sala (loot_salas_secretas.json, pela faixa de nível do mapa).
  - 5 ESFERAS DO MACACO (objeto "esfera", marca "esfera:N") escondidas nas áreas secretas de 5 mapas, do nível 10 ao 45.
    Juntou as 5: ganha a Relíquia do Rei Macaco (vira o Lutador Macaco). O Velho Wu, no Porto, conta a lenda e as pistas.
Idempotente. Rodar depois de gerar_lugares.py e ANTES de gerar_elevacoes.py (os morros desviam das salas).
"""
import json, os, glob, math, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}
ESFERAS = [("costa_do_cal", 1), ("mata_dos_enforcados", 2), ("canions_suplicantes", 3), ("brejo_da_viuva", 4), ("vespera", 5)]

def dentro(o, x, y, folga):
    if o.get("shape") == "circle": return math.hypot(x - o["x"], y - o["y"]) < o["r"] + folga
    return abs(x - o["x"]) < o["w"] / 2 + folga and abs(y - o["y"]) < o["h"] / 2 + folga

def livre(m, x, y, raio):
    hs = m["half_size"]
    if abs(x) > hs - raio - 6 or abs(y) > hs - raio - 6: return False
    if any(dentro(o, x, y, raio) for o in m["obstacles"] if o.get("id", "").find("sala_secreta") < 0): return False
    for lst, d in ((m.get("portals", []), 8), (m.get("npcs", []), 6), (m.get("objects", []), 4), (m.get("chests", []), 4),
                   (m.get("waypoints", []), 8), (m.get("gates", []), 6), (m.get("spawns", []), 3), (m.get("props", []), 2)):
        if any(math.hypot(x - p["x"], y - p["y"]) < d + raio for p in lst if not str(p.get("id", "")).startswith(("sala_secreta", "esfera_")) and p.get("poi") != "sala"): return False
    for z in m["zones"]:
        if z["kind"] in ("refugio", "seguro", "cidade", "perigoso", "chefe") and abs(x - z["x"]) < z["w"] / 2 + raio + 4 and abs(y - z["y"]) < z["h"] / 2 + raio + 4: return False
    if math.hypot(x - m["spawn_x"], y - m["spawn_y"]) < 25: return False
    return True

ANTIGOS = {"bau_acropole_segredo", "bau_brejo_segredo", "bau_canions_segredo", "bau_geada_segredo", "bau_escoria_segredo",
           "bau_mata_segredo", "bau_vespera_segredo"}   # (bau_segredo do Vale fica: câmara de muralha ilusória, feita à mão)

def caverna(m, x, y, suf, flag, bau_id, loot):
    """Amontoado de rochas com o baú no miolo, laje por cima (cliente) e a rocha da frente que quebra (portão "pedra_secreta")."""
    for nome, dx, dy, r, h in [("fundo", 0, -2.4, 2.0, 3.2), ("esq", -2.3, -0.6, 1.5, 2.4), ("dir", 2.3, -0.6, 1.5, 2.4),
                               ("fe", -1.85, 1.7, 1.1, 1.8), ("fd", 1.85, 1.7, 1.1, 1.8)]:
        # tira do caminho obstáculos que estavam bem em cima (o canto secreto antigo tinha muro/arbusto)
        m["obstacles"].append({"id": f"sala_secreta{suf}_{nome}", "kind": "rocha", "shape": "circle", "x": round(x + dx, 2), "y": round(y + dy, 2),
                               "w": 0, "h": 0, "r": r, "height": h})
    m.setdefault("gates", []).append({"id": f"sala_secreta_racha{suf}", "flag": flag, "x": x, "y": round(y + 1.9, 2), "w": 1.8, "h": 1.6,
                                      "lever_x": x, "lever_y": round(y + 3.2, 2), "kind": "pedra_secreta", "lever": False})
    m.setdefault("objects", []).append({"id": f"sala_secreta_parede{suf}", "kind": "pedra_oca", "x": x, "y": round(y + 3.1, 2),
                                        "label": "Rocha", "grant_flag": flag, "text": "cheia"})
    if bau_id:
        m.setdefault("chests", []).append({"id": bau_id, "x": x, "y": round(y - 0.3, 2), "loot_table": loot, "facing": 180, "secret": True})

def faixa(m):
    lv = [s["level"] for s in m["spawns"]] or [1]
    return max(1, min(5, 1 + int(sum(lv) / len(lv)) // 12))

def main():
    loots = []
    for t in range(1, 6):
        loots.append({"id": f"loot_sala_secreta_{t}", "entries": [
            {"chance": 1.0, "item": f"caixa_cinzas_{t}"},
            {"chance": 1.0, "item": "joia_mare", "min_qty": t, "max_qty": t + 1},
            {"chance": 0.5, "item": "joia_brasa", "min_qty": 1, "max_qty": max(1, t - 1)},
            {"chance": 0.15 * t, "item": "joia_vazio"},
            {"chance": 0.25, "item": "consumable_panaceia"}]})
    json.dump(loots, open(os.path.join(C, "loot_salas_secretas.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    feitas = 0
    for mid, f in sorted(ARQ.items()):
        m = json.load(open(f, encoding="utf-8"))
        # limpa o que este gerador pôs
        m["obstacles"] = [o for o in m["obstacles"] if not o.get("id", "").startswith("sala_secreta")]
        m["gates"] = [g for g in m.get("gates", []) if not g["id"].startswith("sala_secreta")]
        m["objects"] = [o for o in m.get("objects", []) if not o["id"].startswith(("sala_secreta", "esfera_"))]
        m["chests"] = [c for c in m.get("chests", []) if not c["id"].startswith("sala_secreta")]
        m["props"] = [p for p in m["props"] if p.get("poi") != "sala"]
        m["obstacles"] = [o for o in m["obstacles"] if not o.get("id", "").startswith("sala_secreta")]
        if m.get("instanced") or m["kind"] != "campo" or m["half_size"] < 46:
            json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2); continue
        rnd = random.Random("sala:" + mid)
        hs = m["half_size"]
        sec = next((z for z in m["zones"] if z["kind"] == "secreto"), None)
        cands = [(rnd.uniform(-hs, hs), rnd.uniform(-hs, hs)) for _ in range(3000)]
        if sec: cands.sort(key=lambda p: math.hypot(p[0] - sec["x"], p[1] - sec["y"]))
        lugar = next(((x, y) for x, y in cands if livre(m, x, y, 6.5)), None)
        if lugar:
            x, y = round(lugar[0], 1), round(lugar[1], 1)
            # 14/11 tarde (dono: "o segredo está muito destacado e bugado — coloca o baú ao redor de pedras, formando uma
            # caverninha no meio do nada; batendo na pedra da frente ela abre e dá para pegar o baú; e põe outras pedras
            # para a galera ficar batendo e caçar as que têm algo dentro"): um amontoado de rochas com o baú no miolo; a da
            # frente é uma rocha igual às outras (portão "pedra_secreta"), 3 golpes e ela se desfaz — só para você
            caverna(m, x, y, "", f"parede:{mid}", "sala_secreta_bau", f"loot_sala_secreta_{faixa(m)}")
            # 14/11 18h (dono: "cria a mesma mecânica para outros lugares do mapa, e tira aqueles segredos de caixa mal
            # feitos"): os baús secretos antigos (canto "secreto" de cada região, nicho do Vale) viram cavernas também
            # (mesmo baú, mesma marca de aberto) e cada mapa ganha mais uma caverna nova longe da primeira
            for c in [c for c in m.get("chests", []) if c.get("secret") and c["id"] in ANTIGOS]:
                c["x"], c["y"] = round(c["x"], 1), round(c["y"], 1)
                m["props"] = [p for p in m["props"] if math.hypot(p["x"] - c["x"], p["y"] - c["y"]) > 4.5]   # tira a decoração antiga do canto
                caverna(m, c["x"], round(c["y"] + 0.3, 2), "_" + c["id"], f"caverna:{mid}:{c['id']}", None, None)
                print(f"  {mid}: baú antigo {c['id']} agora numa caverna")
            seg = next(((cx, cy) for cx, cy in reversed(cands) if math.hypot(cx - x, cy - y) > 30 and livre(m, cx, cy, 6.5)), None)
            if seg:
                caverna(m, round(seg[0], 1), round(seg[1], 1), "2", f"caverna:{mid}:2", "sala_secreta_bau2", f"loot_sala_secreta_{faixa(m)}")
            # iscas: rochas iguais espalhadas — a maioria só pedra (às vezes uma joia)
            iscas = 0
            for cx, cy in cands[::37]:
                if iscas >= 5: break
                if math.hypot(cx - x, cy - y) < 14 or not livre(m, cx, cy, 2.5): continue
                k = iscas + 1
                fl = f"pedra:{mid}:{k}"
                m["gates"].append({"id": f"sala_secreta_isca{k}", "flag": fl, "x": round(cx, 1), "y": round(cy, 1), "w": 1.8, "h": 1.6,
                                   "lever_x": round(cx, 1), "lever_y": round(cy + 1.3, 1), "kind": "pedra_secreta", "lever": False})
                m["objects"].append({"id": f"sala_secreta_isca{k}_obj", "kind": "pedra_oca", "x": round(cx, 1), "y": round(cy + 1.3, 1),
                                     "label": "Rocha", "grant_flag": fl, "text": "vazia"})
                iscas += 1
            feitas += 1
            print(f"  {mid}: caverna secreta em ({x}, {y}) + {iscas} iscas")
        # esfera do macaco
        for emap, n in ESFERAS:
            if emap != mid: continue
            alvo = sec or {"x": 0, "y": 0}
            ponto = next(((px, py) for px, py in sorted(cands, key=lambda p: math.hypot(p[0] - alvo["x"], p[1] - alvo["y"])) if livre(m, px, py, 1.5)), None)
            if ponto:
                m["objects"].append({"id": f"esfera_macaco_{n}", "kind": "esfera", "x": round(ponto[0], 1), "y": round(ponto[1], 1),
                                     "label": f"Esfera do Macaco ({n} estrela{'s' if n > 1 else ''})", "grant_flag": f"esfera:{n}",
                                     "text": "Uma esfera alaranjada, quente, com estrelas vermelhas dentro."})
                print(f"  {mid}: esfera {n}")
        json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    # o Velho Wu no Porto (pistas das esferas)
    p = json.load(open(ARQ["porto_das_cinzas"], encoding="utf-8"))
    p["npcs"] = [n for n in p["npcs"] if n["id"] != "velho_wu"]
    ocup = [(n["x"], n["y"]) for n in p["npcs"]]
    for raio in range(0, 20):
        x, y = round(p["spawn_x"] + 20 - raio * 0.7, 1), round(p["spawn_y"] + 6 + (raio % 4), 1)
        if all(math.hypot(x - a, y - b) > 3.2 for a, b in ocup) and not any(dentro(o, x, y, 1.4) for o in p["obstacles"]): break
    p["npcs"].append({"id": "velho_wu", "name": "Velho Wu, o Monge", "role": "aldeao", "x": x, "y": y, "facing": 180, "look": "mentor",
                      "greeting": "Cinco esferas caíram do céu do Rei Macaco. Uma na Costa, uma na Mata, uma nos Cânions, uma no Brejo e uma em Véspera — sempre perto dos lugares secretos. Junte as cinco e o bastão é seu."})
    json.dump(p, open(ARQ["porto_das_cinzas"], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("salas secretas:", feitas)

main()

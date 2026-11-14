#!/usr/bin/env python3
"""
13/11 (pedidos do Barto/dono): NPCs de ofício no Porto — vendedor de balão, dentista, açougueiro, o químico careca de
cavanhaque, dançarino — e a Rosa, vendedora de montarias (Estábulo da Rosa: apitos do nível 10 ao 55). Também põe o
prefeito Belmiro "numa casa, pra parecer prefeito mesmo": casa enxaimel atrás dele, varanda coberta, mesa e estandartes.
Visual: o id "oficio_<ofício>_..." liga os enfeites do ofício no nosso boneco (MannequinVisual.SetOficio); o dançarino
dança sem parar. Idempotente.
"""
import json, os, glob, math

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}

def livre(m, x, y, r=1.4):
    if abs(x) > m["half_size"] - 4 or abs(y) > m["half_size"] - 4: return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r: return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r: return False
    for lst, d in ((m["props"], 2.0), (m["portals"], 5), (m["npcs"], 3.2), (m["objects"], 2.5), (m.get("waypoints", []), 4), (m.get("chests", []), 2.5)):
        if any(math.hypot(x - p["x"], y - p["y"]) < d for p in lst): return False
    return True

def perto(m, tx, ty):
    for raio in range(0, 26):
        for k in range(0, 360, 15):
            x, y = round(tx + raio * math.cos(math.radians(k)), 1), round(ty + raio * math.sin(math.radians(k)), 1)
            if livre(m, x, y): return x, y
    raise SystemExit("sem lugar")

OFICIOS = [
    ("oficio_balao_porto", "Pipo, o Vendedor de Balões", "aldeao", (6, 4), "Balão! Balão de toda cor! Pra criança, pra namorada, pra pendurar no elmo!", None),
    ("oficio_dentista_porto", "Doutor Arcanjo, o Dentista", "aldeao", (-6, 4), "Abre a boca... hmm. Quem mastiga carne de caranguejo de cal assim? Volte em seis meses.", None),
    ("oficio_acougueiro_porto", "Tonho, o Açougueiro", "aldeao", (-14, 2), "Carne fresquinha de javali da duna! Quem caça, come bem — e cura melhor.", "loja_acougue"),
    ("oficio_quimico_porto", "Mestre Walmor, o Químico", "aldeao", (14, 2), "Química é a ciência da mudança. Meus frascos curam tudo — veneno, sangue, maldição. Pureza de 99%.", "loja_quimico"),
    ("oficio_dancarino_porto", "Zeca Requebra, o Dançarino", "aldeao", (0, 8), "Um, dois, três e gira! A vida é curta, herói — dança comigo!", None),
    ("rosa_montarias", "Rosa, a Tratadora de Montarias", "aldeao", (-18, 10), "Estábulo da Rosa! Cavalo, lince, avestruz, urso... até condor. Quanto mais forte o bicho, mais nível ele pede.", "loja_montarias"),
]

def main():
    m = json.load(open(ARQ["porto_das_cinzas"], encoding="utf-8"))
    ids = {o[0] for o in OFICIOS}
    m["npcs"] = [n for n in m["npcs"] if n["id"] not in ids]
    for nid, nome, look, (tx, ty), fala, loja in OFICIOS:
        x, y = perto(m, m["spawn_x"] + tx, m["spawn_y"] + ty)
        n = {"id": nid, "name": nome, "role": "aldeao", "x": x, "y": y, "facing": 180, "look": look, "greeting": fala}
        if loja: n["shop"] = loja
        m["npcs"].append(n)
        print(f"  {nome}: ({x}, {y})")
    json.dump(m, open(ARQ["porto_das_cinzas"], "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    # lojas novas
    shops = json.load(open(os.path.join(C, "shops.json"), encoding="utf-8"))
    shops = [s for s in shops if s["id"] not in ("loja_acougue", "loja_quimico")]
    shops.append({"id": "loja_acougue", "name": "Açougue do Tonho", "items": ["food_caldo_cinzas", "food_ensopado_vigilia", "food_refeicao_travessia", "food_banquete_ausentes"]})
    shops.append({"id": "loja_quimico", "name": "Laboratório do Walmor", "items": ["consumable_c07_antidoto", "consumable_bandagem", "consumable_agua_benta", "consumable_panaceia"]})
    json.dump(shops, open(os.path.join(C, "shops.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    # o prefeito numa casa: casa atrás dele, varanda coberta (tenda), mesa na frente e estandartes
    mp = json.load(open(ARQ["monte_prefeito"], encoding="utf-8"))
    bel = next(n for n in mp["npcs"] if n["id"] == "prefeito_belmiro")
    # 13/11 (dono: "o prefeito está bem perto de onde se surge; coloca em outro lugar, com um caminho até lá"): a
    # Prefeitura vai para um lugar livre a 25–40 m da chegada, com estrada de terra e área segura em volta
    import math
    def livre(x, y):
        for o in mp["obstacles"]:
            if o.get("id") == "prefeitura_casa": continue
            hw = o.get("r", 0) if o.get("shape") == "circle" else o["w"] / 2; hh = o.get("r", 0) if o.get("shape") == "circle" else o["h"] / 2
            if abs(x - o["x"]) < hw + 6 and abs(y - o["y"]) < hh + 7: return False
        if any(math.hypot(x - s["x"], y - s["y"]) < 14 for s in mp["spawns"]): return False
        if any(math.hypot(x - p["x"], y - p["y"]) < 6 for p in mp["props"] if p.get("poi") != "prefeitura"): return False
        if any(math.hypot(x - o["x"], y - o["y"]) < 6 for o in mp.get("objects", [])): return False
        if any(abs(x - e["x"]) < e["w"] / 2 + e["ramp"] + 3 and abs(y - e["y"]) < e["h"] / 2 + e["ramp"] + 3 for e in mp.get("elevations", [])): return False
        if any(math.hypot(x - p["x"], y - p["y"]) < 10 for p in mp["portals"]): return False
        return abs(x) < mp["half_size"] - 10 and abs(y) < mp["half_size"] - 12
    cands = sorted(((x, y) for x in range(-40, 41, 2) for y in range(-10, 31, 2) if 25 <= math.hypot(x - mp["spawn_x"], y - mp["spawn_y"]) <= 42),
                   key=lambda p: (abs(math.hypot(p[0] - mp["spawn_x"], p[1] - mp["spawn_y"]) - 32), p))
    ponto = next((p for p in cands if livre(*p)), (bel["x"], bel["y"]))
    bel["x"], bel["y"] = float(ponto[0]), float(ponto[1])
    bx, by = bel["x"], bel["y"]
    mp["zones"] = [z for z in mp["zones"] if z["id"] != "prefeitura"]
    mp["zones"].append({"id": "prefeitura", "name": "Prefeitura do Monte", "kind": "seguro", "x": bx, "y": by - 1.5, "w": 16, "h": 14})
    sx, sy = mp["spawn_x"], mp["spawn_y"] - 3
    pts = []
    for i in range(13):
        t = i / 12
        cx = sx + (bx - sx) * t + math.sin(t * math.pi) * 6 * (1 if bx < sx else -1)   # curva suave
        cy = sy + (by + 3 - sy) * t
        pts.append([round(cx, 2), round(cy, 2)])
    mp["roads"] = [r for r in (mp.get("roads") or []) if r.get("id") != "estrada_prefeitura"]
    mp["roads"].append({"id": "estrada_prefeitura", "points": pts, "width": 2.6})
    mp["props"] = [p for p in mp["props"] if p.get("poi") != "prefeitura"]
    mp["obstacles"] = [o for o in mp["obstacles"] if o.get("id") != "prefeitura_casa"]
    mp["props"] += [
        {"kind": "casa_enxaimel", "x": bx, "y": by - 5.2, "yaw": 0, "poi": "prefeitura"},
        {"kind": "movel_mesa", "x": bx, "y": by + 1.4, "yaw": 0, "poi": "prefeitura"},
        {"kind": "estandarte", "x": bx - 2.6, "y": by + 0.6, "yaw": 0, "poi": "prefeitura"},
        {"kind": "estandarte", "x": bx + 2.6, "y": by + 0.6, "yaw": 0, "poi": "prefeitura"},
        {"kind": "placa", "x": bx + 3.6, "y": by + 2.2, "yaw": 180, "poi": "prefeitura", "texto": "Prefeitura do Monte"},
    ]
    mp["obstacles"].append({"id": "prefeitura_casa", "kind": "invisivel", "x": bx, "y": by - 5.2, "w": 6.6, "h": 5.1})
    bel["greeting"] = "Bem-vindo à Prefeitura do Monte! Sente-se — aqui em cima a vista é boa e o terreno é barato, para quem merece."
    json.dump(mp, open(ARQ["monte_prefeito"], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print("  prefeitura do Belmiro montada")

main()

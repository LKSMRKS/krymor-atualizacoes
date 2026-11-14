#!/usr/bin/env python3
"""
13/11 (pedido do dono: "lugares com nome, waypoints, atalhos e áreas secretas — o jogo com mais exploração"):
em cada mapa de campo põe 4 a 6 LUGARES COM NOME (zona kind "lugar", 14x14 m) montados com peças que já existem
(acampamento, ossada, colunas, lápides, estandartes...), um SEGUNDO ponto de viagem no lugar mais longe da vila (o atalho)
e uma ÁREA SECRETA (zona kind "secreto"): em volta do baú secreto que o mapa já tem, ou num canto escondido.
O servidor anuncia o nome ao entrar e dá XP na primeira visita (secreto: + Caixa das Cinzas) — GameServer.Lugares.cs.
Idempotente: tira o que ele mesmo pôs (zonas lugar/secreto, peças com "poi", waypoint *_2) antes de pôr de novo.
Rodar depois do editor de mapas (sync) e dos outros geradores.
"""
import json, os, glob, math, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")

NOMES = {
    "praia": ["Acampamento dos Náufragos", "Mirante das Gaivotas", "Cemitério dos Barcos", "Ruína do Vigia", "Fogueira dos Contrabandistas", "Altar da Maré"],
    "mata": ["Clareira dos Enforcados", "Acampamento do Lenhador", "Altar do Musgo", "Ruína do Eremita", "Covas sem Nome", "Pouso dos Caçadores"],
    "vale": ["Ruína do Pedreiro", "Mirante do Vale", "Altar das Cinzas", "Acampamento dos Peregrinos", "Covas do Vale", "Pedra do Guerreiro"],
    "canion": ["Pouso dos Mineiros", "Altar do Suplicante", "Mirante do Abismo", "Ruína da Escavação", "Covas dos Escravos", "Acampamento dos Ladrões"],
    "brejo": ["Ilha dos Afogados", "Acampamento do Caniço", "Túmulo da Noiva", "Ruína da Palafita", "Altar do Lodo", "Pouso dos Barqueiros"],
    "fantasma": ["Cemitério dos Sinos", "Ruína da Capela", "Acampamento dos Coveiros", "Altar Sem Rosto", "Praça Esquecida", "Forca Velha"],
    "neve": ["Acampamento da Caravana", "Altar Rúnico", "Mirante da Geada", "Ruína do Juramento", "Covas Congeladas", "Pouso dos Trenós"],
    "lava": ["Bigorna Abandonada", "Altar da Brasa", "Acampamento dos Fundidores", "Ruína Derretida", "Covas de Escória", "Mirante da Fornalha"],
    "acropole": ["Colunata Partida", "Altar do Juramento", "Acampamento dos Hoplitas", "Ruína do Templo", "Covas dos Heróis", "Mirante da Acrópole"],
    "ceu": ["Mirante das Nuvens", "Altar do Vento", "Ruína Flutuante", "Pouso das Águias"],
    "lua": ["Cratera Silenciosa", "Altar Prateado", "Ruína Lunar", "Pouso dos Uivos"],
}
SECRETO = {"praia": "Gruta do Contrabando", "mata": "Toca do Enforcado", "vale": "Câmara Soterrada", "canion": "Veio Esquecido",
           "brejo": "Ninho da Viúva", "fantasma": "Cripta Sem Nome", "neve": "Caverna de Gelo Azul", "lava": "Forja Escondida",
           "acropole": "Tesouro dos Heróis", "ceu": "Nicho do Vento", "lua": "Lado Escuro"}
# receita de peças por tipo de lugar (palavra no nome -> peças em volta do centro)
RECEITA = [
    ("Acampamento", ["acampamento_abandonado", "fogueira", "barril", "caixotes"]),
    ("Pouso", ["acampamento_abandonado", "fogueira", "caixotes"]),
    ("Fogueira", ["fogueira", "barril", "caixotes", "estacas"]),
    ("Altar", ["coluna_quebrada", "coluna_quebrada", "tocha", "tocha", "cranios"]),
    ("Ruína", ["coluna_quebrada", "cerca_quebrada", "coluna_quebrada", "ossada"]),
    ("Covas", ["lapide", "lapide", "lapide", "ossada", "tocha"]),
    ("Cemitério", ["lapide", "lapide", "barco_encalhado", "ossada"]),
    ("Túmulo", ["lapide", "estandarte", "tocha"]),
    ("Mirante", ["placa", "estandarte", "cerca_quebrada", "tocha"]),
    ("Forca", ["estacas", "ossada", "cranios", "tocha"]),
]
PADRAO = ["estandarte", "ossada", "tocha", "caixotes"]

def receita(nome):
    for k, r in RECEITA:
        if k in nome: return r
    return PADRAO

def dentro(o, x, y, folga):
    if o.get("shape") == "circle": return math.hypot(x - o["x"], y - o["y"]) < o["r"] + folga
    return abs(x - o["x"]) < o["w"] / 2 + folga and abs(y - o["y"]) < o["h"] / 2 + folga

def livre(m, x, y, folga=1.5):
    if abs(x) > m["half_size"] - 8 or abs(y) > m["half_size"] - 8: return False
    if any(dentro(o, x, y, folga) for o in m["obstacles"]): return False
    for lst, d in ((m.get("portals", []), 8), (m.get("npcs", []), 6), (m.get("objects", []), 3), (m.get("chests", []), 3),
                   (m.get("waypoints", []), 10), (m.get("gates", []), 8), (m.get("spawns", []), 3.5)):
        if any(math.hypot(x - p["x"], y - p["y"]) < d for p in lst): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 1.6 for p in m["props"]): return False
    if math.hypot(x - m["spawn_x"], y - m["spawn_y"]) < 18: return False
    return True

def na_zona_proibida(m, x, y, margem=8):
    for z in m["zones"]:
        if z["kind"] in ("refugio", "seguro", "perigoso", "chefe", "cidade") and abs(x - z["x"]) < z["w"] / 2 + margem and abs(y - z["y"]) < z["h"] / 2 + margem:
            return True
    return False

def main():
    total, secretos, wps = 0, 0, 0
    for f in sorted(glob.glob(os.path.join(C, "map_*.json"))):
        m = json.load(open(f, encoding="utf-8"))
        if m.get("instanced") or m["half_size"] < 46 or m["kind"] != "campo" or m["theme"] not in NOMES: continue
        # limpa o que este gerador pôs antes
        velhas = [z for z in m["zones"] if z["kind"] in ("lugar", "secreto")]
        m["zones"] = [z for z in m["zones"] if z["kind"] not in ("lugar", "secreto")]
        kinds = {k for _, r in RECEITA for k in r} | set(PADRAO)
        m["props"] = [p for p in m["props"] if not p.get("poi") and not (p["kind"] in kinds and any(
            abs(p["x"] - z["x"]) < z["w"] / 2 and abs(p["y"] - z["y"]) < z["h"] / 2 for z in velhas if z["kind"] == "lugar"))]
        m["waypoints"] = [w for w in m.get("waypoints", []) if not w["id"].endswith("_atalho")]
        rnd = random.Random(m["id"])
        hs = m["half_size"]
        quantos = 6 if hs > 100 else (5 if hs >= 75 else 4)
        nomes = NOMES[m["theme"]][:]
        rnd.shuffle(nomes)
        postos = [(w["x"], w["y"]) for w in m.get("waypoints", [])]
        cands = [(rnd.uniform(-hs + 12, hs - 12), rnd.uniform(-hs + 12, hs - 12)) for _ in range(4000)]
        lugares = []
        for x, y in cands:
            if len(lugares) >= quantos: break
            if na_zona_proibida(m, x, y) or not livre(m, x, y, 3.0): continue
            dmin = 0.55 * hs * 2 / math.sqrt(quantos)
            if any(math.hypot(x - a, y - b) < dmin for a, b in postos): continue
            # o centro e um anel de 4 m livres (as peças vão em volta)
            if sum(livre(m, x + 4 * math.cos(a), y + 4 * math.sin(a), 1.0) for a in (0, 1.57, 3.14, 4.71)) < 3: continue
            nome = nomes[len(lugares)]
            zid = f"lugar{len(lugares)}"
            m["zones"].append({"id": zid, "name": nome, "kind": "lugar", "x": round(x, 1), "y": round(y, 1), "w": 14, "h": 14})
            pecas = receita(nome)
            ang0 = rnd.uniform(0, 6.28)
            for i, k in enumerate(pecas):
                for tent in range(12):
                    a = ang0 + i * 6.28 / len(pecas) + tent * 0.4
                    r = 3.6 + (tent % 3) * 1.2
                    px, py = x + r * math.cos(a), y + r * math.sin(a)
                    if livre(m, px, py, 0.8):
                        m["props"].append({"kind": k, "x": round(px, 1), "y": round(py, 1), "yaw": round(math.degrees(-a) % 360), "poi": zid})
                        break
            lugares.append((x, y, nome)); postos.append((x, y)); total += 1
        # atalho: ponto de viagem no lugar mais longe da vila (o primeiro waypoint)
        if lugares and m.get("waypoints"):
            w0 = m["waypoints"][0]
            x, y, nome = max(lugares, key=lambda p: math.hypot(p[0] - w0["x"], p[1] - w0["y"]))
            if math.hypot(x - w0["x"], y - w0["y"]) > hs * 0.6:
                m["waypoints"].append({"id": f"wp_{m['id']}_atalho", "name": nome, "x": round(x, 1), "y": round(y, 1)})
                wps += 1
        # área secreta: em volta do baú secreto, senão num canto livre
        bau = next((c for c in m.get("chests", []) if c.get("secret")), None)
        if bau: sx, sy = bau["x"], bau["y"]
        else:
            cantos = sorted(((x, y) for x, y in cands if abs(x) > hs * 0.62 and abs(y) > hs * 0.62 and livre(m, x, y, 2.0)
                             and not na_zona_proibida(m, x, y) and all(math.hypot(x - a, y - b) > 20 for a, b in postos)), key=lambda p: -abs(p[0]) - abs(p[1]))
            if not cantos: print("  sem canto secreto em", m["id"]); sx = None
            else: sx, sy = cantos[0]
        if sx is not None:
            m["zones"].append({"id": "secreto", "name": SECRETO[m["theme"]], "kind": "secreto", "x": round(sx, 1), "y": round(sy, 1), "w": 10, "h": 10})
            if not bau:
                for i, k in enumerate(["ossada", "cranios", "tocha"]):
                    a = i * 2.1
                    px, py = sx + 2.5 * math.cos(a), sy + 2.5 * math.sin(a)
                    if livre(m, px, py, 0.6): m["props"].append({"kind": k, "x": round(px, 1), "y": round(py, 1), "yaw": 0, "poi": "secreto"})
            secretos += 1
        json.dump(m, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        print(f"  {m['id']}: {len(lugares)} lugares")
    print(f"lugares: {total}, áreas secretas: {secretos}, atalhos (2º ponto de viagem): {wps}")

main()

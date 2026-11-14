#!/usr/bin/env python3
"""
13/11 (pedido do dono: "a Ilha da Serpente Marinha é terra — como tem uma serpente MARINHA num lugar de terra? valida a
lógica dos outros lugares também"; "a maioria dos TPs fica no limite de baixo dos mapas, varia um pouco"):
  - Ilha da Serpente Marinha: a serpente mora numa ENSEADA — lagoa de água salgada no norte da ilha, com a arena na beira.
  - Fundo do Triângulo (debaixo d'água): sem tocha nem fogueira (viram ossadas/destroços); o "Acampamento" é um bolsão de ar.
  - Nomes de bolsão das ramificações ("Guerreiro da Ilhas", "Harpia das Nevascass") viram "Território: ...".
  - Passagens: a saída norte dos mapas de campo grandes sai do meio da borda e vai para um dos lados (sorteio fixo por
    mapa), e a chegada no mapa seguinte acompanha; a entrada sul fica onde está (é a chegada da estrada).
Idempotente (marcas "ajuste" nos itens que este script mexe). Rodar depois de todos os geradores de mapa.
"""
import json, os, glob, math, random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}

def L(mid): return json.load(open(ARQ[mid], encoding="utf-8"))
def D(mid, m): json.dump(m, open(ARQ[mid], "w", encoding="utf-8"), ensure_ascii=False, indent=2)

def dentro(o, x, y, folga):
    if o.get("shape") == "circle": return math.hypot(x - o["x"], y - o["y"]) < o["r"] + folga
    return abs(x - o["x"]) < o["w"] / 2 + folga and abs(y - o["y"]) < o["h"] / 2 + folga

def livre(m, x, y, r):
    if any(dentro(o, x, y, r) for o in m["obstacles"]): return False
    if any(math.hypot(x - s["x"], y - s["y"]) < r + 3 for s in m["spawns"]): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < r + 1.5 for p in m["props"]): return False
    if any(math.hypot(x - o["x"], y - o["y"]) < r + 2 for o in m.get("objects", [])): return False
    if any(abs(x - g["x"]) < g["w"] / 2 + r and abs(y - g["y"]) < g["h"] / 2 + r for g in m.get("gates", [])): return False
    return True

def main():
    # 1) Ilha da Serpente Marinha: lagoa salgada no norte, a serpente na beira
    m = L("ilha_serpente")
    m["obstacles"] = [o for o in m["obstacles"] if o.get("id") != "enseada_serpente"]
    chefe = next(z for z in m["zones"] if z["kind"] == "chefe")
    boss = next(s for s in m["spawns"] if s["monster"] == "monster_ilha_serpente")
    lx, ly, lr = boss["x"], boss["y"] - 11.5, 8.5
    m["obstacles"] = [o for o in m["obstacles"] if not (o.get("kind") in ("arvore", "rocha") and math.hypot(o["x"] - lx, o["y"] - ly) < lr + 2.5)]
    m["props"] = [p for p in m["props"] if math.hypot(p["x"] - lx, p["y"] - ly) > lr + 1]
    m["obstacles"].append({"id": "enseada_serpente", "kind": "lago", "shape": "circle", "x": lx, "y": ly, "w": 0, "h": 0, "r": lr})
    chefe["name"] = "Enseada da Serpente Marinha"
    D("ilha_serpente", m)

    # 2) Fundo do Triângulo: nada de fogo debaixo d'água
    m = L("fundo_triangulo")
    for p in m["props"]:
        if p["kind"] in ("tocha", "tocha_poste", "fogueira", "estandarte"):
            p["kind"] = "cranios" if p["kind"] == "tocha" else "barco_encalhado" if p["kind"] == "fogueira" else "ossada"
    for z in m["zones"]:
        if z["kind"] == "seguro" and z["name"] == "Acampamento": z["name"] = "Bolsão de Ar"
    D("fundo_triangulo", m)

    # 3) nomes de bolsão
    for mid in ARQ:
        m = L(mid); mudou = False
        for z in m["zones"]:
            if z.get("id", "").startswith("bolsao") and not z["name"].startswith("Território") and z["name"].endswith("s"):
                z["name"] = "Território: " + z["name"][:-1]; mudou = True
        if mudou: D(mid, m)

    # 4) saída norte fora do meio da borda (mapas de campo de 150 m)
    for mid in sorted(ARQ):
        m = L(mid)
        if m.get("instanced") or m["kind"] != "campo" or m["half_size"] < 70: continue
        norte = [p for p in m["portals"] if p["y"] < -60 and abs(p["x"]) < 1 and not p.get("ajuste")]
        if not norte: continue
        p = norte[0]
        rnd = random.Random("tp:" + mid)
        for _ in range(60):
            nx = round(rnd.choice([-1, 1]) * rnd.uniform(22, 48), 1)
            if livre(m, nx, p["y"], 3.0) and livre(m, nx, p["y"] + 3.5, 1.2): break
        else:
            continue
        velho = p["x"]
        p["x"] = nx; p["ajuste"] = True
        # a estrada que chegava no portal segue até o lugar novo
        for r in m.get("roads") or []:
            pts = r.get("points") or []
            for pt in pts:
                if math.hypot(pt[0] - velho, pt[1] - p["y"]) < 6: pt[0] = round(pt[0] + (nx - velho) * (1 - abs(pt[1] - p["y"]) / 6), 2)
        D(mid, m)
        # no mapa de destino, a volta (to_x/to_y) cai na frente do portal novo
        dest = L(p["to_map"])
        for q in dest["portals"]:
            if q["to_map"] == mid:
                q["to_x"], q["to_y"] = nx, round(p["y"] + 3.5, 1)
        D(p["to_map"], dest)
        print(f"  {mid}: saída norte em x {nx}")
    # 4b) o muro do selo (y −62, vão de 10 m no meio) acompanha: o vão e o portão de selo vão para a frente do portal novo
    for mid in sorted(ARQ):
        m = L(mid)
        norte = [p for p in m["portals"] if p.get("ajuste") and p["y"] < -60]
        if not norte: continue
        px = norte[0]["x"]
        g = next((g for g in m.get("gates", []) if abs(g["y"] + 62) < 1 and g.get("kind") == "selo"), None)
        if g is None or abs(g["x"] - px) < 0.1: continue
        dx = px - g["x"]
        g["x"] = round(px, 2); g["lever_x"] = round(g.get("lever_x", 0) + dx, 2)
        muros = [o for o in m["obstacles"] if o["kind"] == "muralha" and abs(o["y"] + 62) < 1]
        if len(muros) == 2:
            hs = m["half_size"]
            m["obstacles"] = [o for o in m["obstacles"] if o not in muros]
            esq = (-hs, px - 5); dir_ = (px + 5, hs)
            for a0, a1 in (esq, dir_):
                if a1 - a0 > 0.5:
                    m["obstacles"].append({"kind": "muralha", "x": round((a0 + a1) / 2, 2), "y": -62, "w": round(a1 - a0, 2), "h": 1.4, "r": 0})
        # árvores/rochas que ficaram no vão novo saem do caminho
        m["obstacles"] = [o for o in m["obstacles"] if not (o["kind"] in ("arvore", "arvore_seca", "rocha") and abs(o["x"] - px) < 6 and -74 < o["y"] < -56)]
        D(mid, m)
        print(f"  {mid}: muro do selo com o vão em x {px}")
    # 5) 13/11 ("o bartinho nível 4 de elmo e gibão mata gárgula nível 5 sem tomar quase nada — os monstros ficaram fracos?"):
    # não ficaram — o Gargulito é do arquétipo "enxame" (35% da vida, 45% do dano, feito para vir em bando, como na Galeria).
    # As gárgulas do poleiro da Costa ficam sozinhas: ganham vida e dano de bicho solto (~ágil).
    m = L("costa_do_cal")
    for sp in m["spawns"]:
        if sp["monster"] == "monster_cal_gargulito" and sp.get("perch", 0) > 0:
            sp["health_mult"] = 2.1; sp["damage_mult"] = 1.3
    D("costa_do_cal", m)
    # 6) 13/11 ("cada boneco de treino num nível: um 1, um 10 e um 20, para comparar o dano"): só o nível (o lugar é do dono)
    m = L("porto_das_cinzas")
    bonecos = sorted([sp for sp in m["spawns"] if sp["monster"] == "monster_boneco_treino"], key=lambda sp: sp["id"])
    for sp, nv in zip(bonecos, [1, 10, 20]):
        sp["level"] = nv
    D("porto_das_cinzas", m)
    # 7) 13/11 ("no editor não consigo pegar alguns canteiros, o chafariz da praça, vasos, terras"): o que era desenho fixo
    # no código (MapView.AddVillageDressing e PracaDoPorto) vira peça do mapa, uma vez só — depois é do dono (editor)
    m = L("porto_das_cinzas")
    if not m.get("praca_em_pecas"):
        pecas = [("fonte_praca", 0, 2, 0), ("glb:cenario_barraca_feira_a", -5.2, -7.2, 0), ("glb:cenario_barraca_feira_b", 5.2, -7.2, 0),
                 ("carrinho_feira", 9.2, 10.2, -35), ("glb:cenario_barril", 7.4, -7.6, 20), ("glb:cenario_barril", 8.2, -7.0, 75), ("glb:cenario_barril", -7.4, -7.8, 140),
                 ("glb:cenario_arvore_praca", -8.0, 8.6, 30), ("glb:cenario_arvore_praca", 8.0, 8.6, 200),
                 ("floreira", -2.9, -3.6, 0), ("floreira", 2.9, -3.6, 0), ("floreira", 0, 8.4, 180)]
        for (x, y, yaw, ln) in [(-23.6, 13.0, 0, 2.6), (-12.5, 14.7, 0, 2.2), (17.6, 12.6, 0, 2.4), (11.5, 14.7, 0, 2.2), (-21.6, 3.4, 90, 2.4), (-14, -11.0, 0, 2.8), (26, -8.0, 0, 2.6)]:
            m["props"].append({"kind": "canteiro_flores", "x": x, "y": y, "yaw": yaw, "scale": round(ln / 2.4, 2)})
        for (x, y) in [(-19.2, 13.2), (-16.2, 14.9), (22.3, 12.9), (15.6, 14.9), (-8.9, 18.4), (-5.1, 18.4), (5.1, 18.4), (8.9, 18.4), (-21.5, -2.6), (-17.3, -11.4), (-22.7, -11.4), (17.5, -9.2), (22.5, -9.2)]:
            pecas.append(("vaso_planta", x, y, 0))
        for k, x, y, yaw in pecas:
            m["props"].append({"kind": k, "x": x, "y": y, "yaw": yaw})
        m["praca_em_pecas"] = True
        D("porto_das_cinzas", m)
    # 8) 13/11 ("lugares que forem casas: não usa o portal, fica estranho — usa uma porta, só interage no F"): a entrada da
    # sua casa e as saídas dos interiores viram portas; o portal continua no mapa (destino), mas só a porta o usa
    for mid in sorted(ARQ):
        m = L(mid); mudou = False
        for p in m.get("portals", []):
            # 13/11 22:26 (dono): a entrada da sua casa no Porto volta a ser o PORTAL completo (arco de pedra) — vermelho enquanto
            # não dá para entrar (regra); lá não tem prédio, então a porta ficava solta no chão
            if mid == "porto_das_cinzas" and p["id"] == "porto_casa":
                if p.get("por_porta"):
                    p.pop("por_porta"); mudou = True
                antes = len(m.get("objects", []))
                m["objects"] = [o for o in m.get("objects", []) if o["id"] != "porta_porto_casa"]
                mudou = mudou or len(m["objects"]) != antes
                continue
            casa = False
            saida = p["id"].endswith("_saida") and m.get("kind") == "cidade" and m["half_size"] <= 12
            if not (casa or saida) or p.get("por_porta"): continue
            p["por_porta"] = True
            oid = "porta_" + p["id"]
            if not any(o["id"] == oid for o in m.get("objects", [])):
                m.setdefault("objects", []).append({"id": oid, "kind": "porta", "x": p["x"], "y": p["y"], "label": "Sua casa" if casa else "Porta (sair)",
                                                    "grant_flag": "porta:portal:" + p["id"]})
            mudou = True
        if mudou: D(mid, m); print("  portas:", mid)
    # 8c) 13/11 22:35: a oficina da Forja do Brás é essencial (missões de forja) — se o objeto de uso sumir (apagado sem querer
    # no editor), volta ao lado da forja
    m = L("porto_das_cinzas")
    if not any(o["kind"] == "forja" for o in m.get("objects", [])):
        fj = next((p for p in m["props"] if p["kind"] == "forja"), None)
        if fj:
            m["objects"].append({"id": "porto_forja", "kind": "forja", "x": round(fj["x"] - 1.82, 2), "y": round(fj["y"] - 0.53, 2),
                                 "label": "Forja do Brás", "gerado": True})
            D("porto_das_cinzas", m); print("  forja do Porto voltou (o objeto de uso tinha sumido)")
    # 9) 13/11: o dono mexe no Porto pelo editor — objeto de uso que ficou dentro de parede/prédio sai para o lado livre
    m = L("porto_das_cinzas"); mudou = False
    for o in m.get("objects", []):
        if not any(dentro(ob, o["x"], o["y"], 0) for ob in m["obstacles"]): continue
        for raio in [x * 0.25 for x in range(1, 40)]:
            achou = None
            for k in range(16):
                a = k * math.pi / 8
                nx, ny = o["x"] + math.cos(a) * raio, o["y"] + math.sin(a) * raio
                if not any(dentro(ob, nx, ny, 0.3) for ob in m["obstacles"]): achou = (round(nx, 2), round(ny, 2)); break
            if achou: print("  objeto saiu de dentro da parede:", o["id"], (o["x"], o["y"]), "->", achou); o["x"], o["y"] = achou; mudou = True; break
    # 9b) oficina (forja, bancada...) cujo objeto de uso ficou em cima da peça (a peça é sólida): o objeto vai para a frente
    raios = {"forja": 1.3, "banca": 1.1, "sino": 0.9, "bau_armazem": 0.7}
    for o in m.get("objects", []):
        for p in m["props"]:
            r = raios.get(p["kind"])
            if r is None or p["kind"] != o["kind"]: continue
            d = math.hypot(o["x"] - p["x"], o["y"] - p["y"])
            if d >= r + 0.45 and not any(dentro(ob, o["x"], o["y"], 0.3) for ob in m["obstacles"]): continue
            base = math.atan2(o["y"] - p["y"], o["x"] - p["x"]) if d > 0.05 else math.pi / 2
            for k in range(24):
                a = base + (k + 1) // 2 * (1 if k % 2 else -1) * math.pi / 12
                nx, ny = p["x"] + math.cos(a) * (r + 0.6), p["y"] + math.sin(a) * (r + 0.6)
                if not any(dentro(ob, nx, ny, 0.45) for ob in m["obstacles"]) and \
                   not any(q is not p and math.hypot(nx - q["x"], ny - q["y"]) < raios.get(q["kind"], 0) + 0.45 for q in m["props"]):
                    break
            o["x"], o["y"] = round(nx, 2), round(ny, 2)
            print("  objeto da oficina foi para a frente da peça:", o["id"], (o["x"], o["y"])); mudou = True
    if mudou: D("porto_das_cinzas", m)

    # (10) 13/11 ("a escada da casa da esquerda está muito perto da quina da parede"): a escada da taverna saiu da quina —
    # x local 3,85..5,05 (era 4,3..5,5, colada na parede direita), base z +3,5 (era +3,9, colada na parede da frente),
    # topo z -0,3. Corrimão, passagem de subir e a de descer acompanham (EscadaCasa.TEsc* no cliente tem os mesmos números).
    # 13/11 23:39 ("ainda muito perto da parede"): 3,35..4,6 / base +3,0 / topo -0,8; corrimão só na metade de cima
    TX, TY, EX0, EX1, ZB, ZT = -20.0, -17.5, 3.35, 4.6, 3.0, -0.8
    m = L("porto_das_cinzas"); mudou = False
    for o in m["obstacles"]:
        if o.get("id") == "taverna_escada_corrimao":
            zm = ZT + (ZB - ZT) * 0.55
            novo = {"x": round(TX + EX0 - 0.12, 2), "y": round(TY + (ZT + zm) / 2, 2), "h": round(zm - ZT, 2)}
            if any(o.get(k) != v for k, v in novo.items()): o.update(novo); mudou = True
    for pt in m["portals"]:
        if pt["id"] == "taverna_subir":
            novo = {"x": round(TX + (EX0 + EX1) / 2, 2), "y": round(TY + ZT + 0.35, 2)}
            if any(pt.get(k) != v for k, v in novo.items()): pt.update(novo); mudou = True
    # a escada que vinha no modelo (encostada na parede esquerda) não é mais desenhada: a caixa de colisão dela sai
    antes = len(m["obstacles"])
    m["obstacles"] = [o for o in m["obstacles"] if not (o.get("kind") == "invisivel" and abs(o["x"] - (TX - 4.9)) < 0.05 and abs(o["y"] - (TY + 1.9)) < 0.05)]
    mudou = mudou or len(m["obstacles"]) != antes
    if mudou: D("porto_das_cinzas", m); print("  escada da taverna saiu da quina")
    if "taverna_andar2" in ARQ:
        a = L("taverna_andar2"); mudou = False
        for pt in a["portals"]:
            if pt["id"] == "taverna_descer":
                novo = {"x": round((EX0 + EX1) / 2, 2), "y": round(ZT + 1.0, 2), "to_x": round(TX + EX0 - 0.9, 2), "to_y": round(TY + ZB - 0.5, 2)}
                if any(pt.get(k) != v for k, v in novo.items()): pt.update(novo); mudou = True
        if mudou: D("taverna_andar2", a)
    # (9c) 13/11: o dono deu colisão redonda a prédios no editor ("solid"); porta que ficou dentro do círculo vai para a borda
    m = L("porto_das_cinzas"); mudou = False
    for o in m.get("objects", []):
        if o.get("kind") != "porta": continue
        for p in m["props"]:
            r = p.get("solid") or 0
            if r <= 0: continue
            d = math.hypot(o["x"] - p["x"], o["y"] - p["y"])
            if d < r + 0.35:
                ux, uy = ((o["x"] - p["x"]) / d, (o["y"] - p["y"]) / d) if d > 0.05 else (0.0, 1.0)
                o["x"], o["y"] = round(p["x"] + ux * (r + 0.5), 2), round(p["y"] + uy * (r + 0.5), 2)
                print("  porta saiu de dentro da colisão do prédio:", o["id"], (o["x"], o["y"])); mudou = True
    if mudou: D("porto_das_cinzas", m)

    # (11) 13/11 ("a biblioteca ainda está sem colisão"): o prédio passou a ter paredes próprias (MapGeometry, gira e anda
    # com a peça); a caixa invisível antiga ficou no lugar velho depois que o dono moveu o prédio — sai
    m = L("porto_das_cinzas")
    if any(p["kind"] == "predio_biblioteca" for p in m["props"]) and any(o.get("id") == "biblioteca_predio" for o in m["obstacles"]):
        m["obstacles"] = [o for o in m["obstacles"] if o.get("id") != "biblioteca_predio"]
        D("porto_das_cinzas", m); print("  caixa velha da biblioteca saiu (o prédio tem paredes próprias)")
    # a saída da biblioteca cai na frente da porta (do lado de fora da parede nova), siga o prédio onde ele estiver
    pb = next((p for p in m["props"] if p["kind"] == "predio_biblioteca"), None)
    if pb is not None and "biblioteca_cais" in ARQ:
        a = math.radians(pb.get("yaw", 0)); fx, fy = math.sin(a), math.cos(a)
        porta = next((o for o in m.get("objects", []) if o.get("id") == "porta_biblioteca"), None)
        bx, by = (porta["x"], porta["y"]) if porta else (pb["x"] + fx * 3.6, pb["y"] + fy * 3.6)
        ex, ey = round(bx + fx * 1.3, 2), round(by + fy * 1.3, 2)
        b = L("biblioteca_cais"); mud = False
        for pt in b["portals"]:
            if pt["id"] == "biblioteca_cais_saida" and (pt["to_x"], pt["to_y"]) != (ex, ey): pt["to_x"], pt["to_y"] = ex, ey; mud = True
        if (b.get("exit_x"), b.get("exit_y")) != (ex, ey): b["exit_x"], b["exit_y"] = ex, ey; mud = True
        if mud: D("biblioteca_cais", b); print("  saída da biblioteca na frente da porta:", ex, ey)
    print("ajustes de lógica feitos")

main()

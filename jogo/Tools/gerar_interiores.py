#!/usr/bin/env python3
"""
13/11 (pedido URGENTE do dono: "a biblioteca está toda espalhada na praça — põe dentro de uma casa com aparência diferente;
ao interagir com a porta você entra; muitos NPCs novos bagunçaram a cidade, põe alguns dentro de casas, tipo o açougueiro;
espalha os outros nos cantos vazios; o pescador precisa de coisas de trabalho perto"):
  - Biblioteca do Cais: prédio novo de pedra com colunas no canto sudeste da praça (prop "predio_biblioteca"); a porta leva
    ao interior "biblioteca_cais" (Inês atrás do balcão, o Registro das Criaturas e os 3 livros nos atris, estantes).
    As missões da Inês continuam iguais (mesmo NPC, mesmo id) — só mudou o lugar.
  - Açougue do Tonho, Consultório do Doutor Arcanjo e Laboratório do Walmor: interiores atrás de 3 portinhas do Porto.
  - Zeca Requebra num tablado no meio da rua de baixo; Pipo perto do portão norte; Bartô do Placar encostado no muro oeste
    com o painel do placar do lado; secador de peixe e cesto ao lado do Tibério.
Interiores: mapa "cidade" de todos (não é cópia), saída "<mapa>_saida" devolve à porta. Idempotente.
Rodar DEPOIS de gerar_leva1.py e gerar_oficios.py (eles põem essas coisas na praça; este tira e arruma).
"""
import json, os, glob, math

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: f for f in glob.glob(os.path.join(C, "map_*.json"))}
L = lambda f: json.load(open(f, encoding="utf-8"))
def D(f, d): json.dump(d, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

def sala(mid, nome, half, porta_xy, npcs, props, objects=(), theme="casarao"):
    """Interior retangular (2·half) com paredes, saída no sul (y = half-1,8) que devolve para a porta."""
    s = half * 2
    return {"id": mid, "display_name": nome, "half_size": half, "spawn_x": 0, "spawn_y": half - 2.6, "safe_radius": 30,
            "xp_multiplier": 1, "kind": "cidade", "region": "porto", "theme": theme, "no_mount": True,
            "exit_map": "porto_das_cinzas", "exit_x": porta_xy[0], "exit_y": porta_xy[1] + 1.4,
            "zones": [{"id": "sala", "name": nome, "kind": "seguro", "x": 0, "y": 0, "w": s, "h": s}],
            "obstacles": [{"kind": "muro", "shape": "box", "x": 0, "y": -half + 0.5, "w": s, "h": 1, "r": 0, "height": 3.8},
                          {"kind": "muro", "shape": "box", "x": 0, "y": half - 0.5, "w": s, "h": 1, "r": 0, "height": 3.8},
                          {"kind": "muro", "shape": "box", "x": -half + 0.5, "y": 0, "w": 1, "h": s, "r": 0, "height": 3.8},
                          {"kind": "muro", "shape": "box", "x": half - 0.5, "y": 0, "w": 1, "h": s, "r": 0, "height": 3.8}],
            "gates": [], "chests": [], "spawns": [], "waypoints": [], "checkpoints": [], "objects": list(objects),
            "props": props, "npcs": npcs,
            "portals": [{"id": mid + "_saida", "x": 0, "y": half - 1.6, "radius": 1.2, "to_map": "porto_das_cinzas",
                         "to_x": porta_xy[0], "to_y": porta_xy[1], "label": "Rua"}]}

def main():
    porto = L(ARQ["porto_das_cinzas"])
    npcs = {n["id"]: n for n in porto["npcs"]}
    ines = npcs.get("bibliotecaria_ines")
    registro = npcs.get("registro_cais")
    livros = [o for o in porto["objects"] if o["id"].startswith("livro_cais_")]
    # ---- tira da praça o que vai para dentro
    sai = {"bibliotecaria_ines", "registro_cais", "oficio_acougueiro_porto", "oficio_dentista_porto", "oficio_quimico_porto"}
    guard = {k: npcs[k] for k in sai if k in npcs}
    porto["npcs"] = [n for n in porto["npcs"] if n["id"] not in sai]
    porto["props"] = [p for p in porto["props"] if p.get("tag") != "biblioteca_cais" and p.get("poi") != "interiores"]
    porto["objects"] = [o for o in porto["objects"] if not o["id"].startswith("livro_cais_") and o["id"] != "porta_biblioteca"]
    porto["obstacles"] = [o for o in porto["obstacles"] if o.get("id") not in ("biblioteca_predio", "tablado_zeca", "placar_painel", "secador_tiberio")]
    # se este gerador já rodou, os livros/NPCs estão nos interiores: pega de lá
    if not livros and "biblioteca_cais" in ARQ:
        b = L(ARQ["biblioteca_cais"])
        livros = [o for o in b["objects"] if o["id"].startswith("livro_cais_")]
        bn = {n["id"]: n for n in b["npcs"]}
        ines = ines or bn.get("bibliotecaria_ines"); registro = registro or bn.get("registro_cais")
    for mid in ("acougue_tonho", "consultorio_arcanjo", "laboratorio_walmor"):
        if mid in ARQ:
            for n in L(ARQ[mid])["npcs"]: guard.setdefault(n["id"], n)

    # ---- Biblioteca do Cais: prédio no canto sudeste
    bx, by = -23.5, 23.5   # canto sudoeste, longe da passagem da casa do jogador (20, 21)
    porto["props"].append({"kind": "predio_biblioteca", "x": bx, "y": by, "yaw": 180, "poi": "interiores"})
    porto["obstacles"].append({"id": "biblioteca_predio", "kind": "invisivel", "shape": "box", "x": bx, "y": by, "w": 9.0, "h": 6.6, "r": 0})
    porto["props"] = [p for p in porto["props"] if not (p["kind"] in ("barril_v2", "pinheiros") and abs(p["x"] - bx) < 6 and abs(p["y"] - by) < 5)]
    porto["objects"].append({"id": "porta_biblioteca", "kind": "porta", "x": bx, "y": round(by - 3.75, 2), "label": "Biblioteca do Cais",
                             "grant_flag": "porta:para:biblioteca_cais", "text": "Biblioteca do Cais — entre em silêncio."})
    porta_bib = (bx, by - 3.75 - 0.75)   # sai na frente da porta, do lado de fora
    ines = dict(ines or {"id": "bibliotecaria_ines", "name": "Inês, da Biblioteca do Cais", "role": "missoes", "look": "aldeao"})
    ines.update({"x": 0.0, "y": -3.2, "facing": 0})
    registro = dict(registro or {"id": "registro_cais", "name": "Registro das Criaturas (Biblioteca do Cais)", "role": "biblioteca", "look": "quadro",
                                 "greeting": "Fichas presas na estante, com a letra caprichada da Inês."})
    registro.update({"x": -6.6, "y": -2.0, "facing": 90})
    for i, o in enumerate(livros):
        o.update({"x": 4.2 + i * 1.6 - 1.6, "y": 1.6})
    bib_props = [{"kind": "movel_balcao", "x": 0, "y": -2.0, "yaw": 0},
                 {"kind": "glb:dg/dg_candelabro", "x": -2.0, "y": -2.0, "yaw": 0}, {"kind": "glb:dg/dg_candelabro", "x": 2.0, "y": -2.0, "yaw": 0},
                 {"kind": "movel_tapete", "x": 0, "y": 2.4, "yaw": 0}, {"kind": "glb:dg/dg_mesa_estudo", "x": -3.6, "y": 3.0, "yaw": 90},
                 {"kind": "glb:dg/dg_astrolabio", "x": -5.8, "y": 5.0, "yaw": 0}, {"kind": "glb:dg/dg_pilha_livros", "x": 6.0, "y": 5.4, "yaw": 20}]
    for x in (-6.0, -3.0, 3.0, 6.0):
        bib_props.append({"kind": "glb:dg/dg_estante_livros", "x": x, "y": -7.1, "yaw": 0, "solid": 0.6})
    for y in (-4.0, 0.5):
        bib_props.append({"kind": "glb:dg/dg_estante_livros", "x": 7.1, "y": y, "yaw": 270, "solid": 0.6})
    for i in range(len(livros)):
        bib_props.append({"kind": "glb:dg/dg_atril", "x": 4.2 + i * 1.6 - 1.6, "y": 1.0, "yaw": 0})
    D(os.path.join(C, "map_biblioteca_cais.json"), sala("biblioteca_cais", "Biblioteca do Cais", 8, porta_bib, [ines, registro], bib_props, livros, theme="biblioteca"))

    # ---- interiores atrás das portinhas
    portas = {o["id"]: o for o in porto["objects"] if o["kind"] == "porta"}
    INTER = [("acougue_tonho", "Açougue do Tonho", "porta_casa_pequena_4", "oficio_acougueiro_porto",
              [{"kind": "movel_balcao", "x": 0, "y": -1.6, "yaw": 0}, {"kind": "carnes_penduradas", "x": 0, "y": -4.4, "yaw": 0},
               {"kind": "carnes_penduradas", "x": -3.0, "y": -4.4, "yaw": 0}, {"kind": "barril", "x": 4.0, "y": -3.8, "yaw": 0},
               {"kind": "movel_caixas", "x": -4.2, "y": 2.8, "yaw": 0}, {"kind": "movel_mesa", "x": 3.5, "y": 1.0, "yaw": 0}]),
             ("consultorio_arcanjo", "Consultório do Doutor Arcanjo", "porta_casa_pequena_5", "oficio_dentista_porto",
              [{"kind": "cadeira_dentista", "x": 1.6, "y": -2.6, "yaw": 0}, {"kind": "movel_prateleira", "x": -4.8, "y": -2.0, "yaw": 90},
               {"kind": "movel_mesa", "x": -2.6, "y": -3.4, "yaw": 0}, {"kind": "glb:dg/dg_candelabro", "x": 4.2, "y": -4.2, "yaw": 0},
               {"kind": "movel_tapete", "x": 0, "y": 1.6, "yaw": 0}]),
             ("laboratorio_walmor", "Laboratório do Walmor", "porta_sobrado_6", "oficio_quimico_porto",
              [{"kind": "movel_mesa", "x": 0, "y": -2.0, "yaw": 0}, {"kind": "frascos_quimica", "x": 0, "y": -2.0, "yaw": 0},
               {"kind": "movel_prateleira", "x": -4.8, "y": -1.0, "yaw": 90}, {"kind": "glb:dg/dg_astrolabio", "x": 4.0, "y": -3.8, "yaw": 0},
               {"kind": "barril", "x": 4.4, "y": 2.8, "yaw": 0}, {"kind": "glb:dg/dg_candelabro", "x": -4.0, "y": -4.4, "yaw": 0}])]
    w = L(os.path.join(C, "world.json"))
    for mid, nome, pid, nid, props in INTER:
        p = portas.get(pid)
        if p is None: continue
        p["grant_flag"] = "porta:para:" + mid
        p["text"] = f"{nome} — aberto."
        p["label"] = nome
        n = dict(guard.get(nid) or {"id": nid, "name": nome, "role": "aldeao", "look": "aldeao"})
        n.update({"x": 0.0, "y": -3.2, "facing": 0})
        D(os.path.join(C, f"map_{mid}.json"), sala(mid, nome, 6, (p["x"], round(p["y"] - 1.2, 2)), [n], props))
    for mid in ("biblioteca_cais", "acougue_tonho", "consultorio_arcanjo", "laboratorio_walmor"):
        if f"map_{mid}.json" not in w["map_files"]: w["map_files"].append(f"map_{mid}.json")
        for r in w["regions"]:
            if r["id"] == "porto" and mid not in r["maps"]: r["maps"].append(mid)
    D(os.path.join(C, "world.json"), w)

    # ---- espalha quem ficou embolado e põe as coisas de trabalho
    def move(nid, x, y, facing=180):
        for n in porto["npcs"]:
            if n["id"] == nid: n.update({"x": x, "y": y, "facing": facing})
    move("oficio_dancarino_porto", 0.0, 22.6, 180)            # tablado na rua de baixo, entre as casas
    porto["props"].append({"kind": "tablado", "x": 0.0, "y": 22.6, "yaw": 0, "poi": "interiores"})
    porto["obstacles"].append({"id": "tablado_zeca", "kind": "invisivel", "shape": "box", "x": 0.0, "y": 22.6, "w": 2.6, "h": 2.6, "r": 0})
    move("oficio_balao_porto", 9.5, -24.0, 180)               # perto do portão norte
    move("arauto_placar", -27.6, -6.0, 90)                     # encostado no muro oeste, com o painel do lado
    porto["props"].append({"kind": "painel_placar", "x": -28.6, "y": -8.4, "yaw": 90, "poi": "interiores"})
    porto["obstacles"].append({"id": "placar_painel", "kind": "invisivel", "shape": "box", "x": -28.6, "y": -8.4, "w": 0.6, "h": 2.8, "r": 0})
    tib = next((n for n in porto["npcs"] if n["id"] == "pescador_tiberio"), None)
    if tib:
        porto["props"].append({"kind": "secador_peixe", "x": tib["x"] - 2.6, "y": tib["y"] - 0.4, "yaw": 90, "poi": "interiores"})
        porto["props"].append({"kind": "cesto_peixe", "x": tib["x"] + 1.0, "y": tib["y"] - 0.8, "yaw": 0, "poi": "interiores"})
        porto["obstacles"].append({"id": "secador_tiberio", "kind": "invisivel", "shape": "box", "x": tib["x"] - 2.6, "y": tib["y"] - 0.4, "w": 0.8, "h": 2.4, "r": 0})
    for o in porto["objects"]:   # a porta brava volta (o morador bate em quem insiste)
        if o["id"] == "porta_casa_pequena_1":
            o["grant_flag"] = "porta:brava"; o["text"] = "«JÁ DISSE QUE NÃO TEM NINGUÉM!» — PÁ! A porta abre e fecha na sua cara."; o["label"] = "Porta"
    D(ARQ["porto_das_cinzas"], porto)
    print("interiores: biblioteca + açougue + consultório + laboratório; praça arrumada")

main()

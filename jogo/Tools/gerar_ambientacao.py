"""Ambientação dos mapas depois da Praia (pedido do dono 14/11: "todos os próximos monstros e lugares estão bem simples comparado
com o Porto, a Praça e a Praia do Cal — tudo deve seguir esse nível: mapa, decoração, distribuição, acontecimentos").

O que faz, por mapa de campo (tema do mapa decide o kit de peças da região, Client/Assets/Models/cenario/<região>_*.glb):
  1. CENAS nos lugares que já têm nome (zonas "lugar", "secreto", "refugio", "chefe"): acampamento, altar, ruína, mirante,
     cemitério, clareira da forca, bigorna, cratera, praça, escavação, vila do refúgio e arena do chefe — cada uma montada com
     as peças da região, com colisão nas peças grandes, um ponto de "contemplar"/"livro" contando a história do lugar e, em
     algumas, um veio de coleta ou uma placa de pressão (armadilha).
  2. MARCO: uma peça grande da região que se vê de longe (moinho, elevador da mina, ninho da fênix, estátua do juramento...).
  3. ESTRADAS: postes/lamparinas a cada ~16 m, placas de seta perto das saídas, trilha de tábuas no Brejo.
  4. MATA DA BORDA: árvores da região com colisão numa faixa perto da borda do mapa (fecha o horizonte como na Costa) e
     árvores soltas pelo meio, fora dos territórios dos monstros (eles andam sem desvio: nada sólido onde eles caçam).
  5. CHÃO: enfeites sem colisão (capim, ossos, crânios, pedras chatas da região) espalhados, mais densos nos territórios.
  6. COLETA: veios e moitas do tema perto das cenas (o que o jogador faz entre um monstro e outro).

Tudo o que este script põe leva "amb": true — rodar de novo tira o que ele pôs antes e refaz igual (mesma semente).
Não mexe em spawns, portais, NPCs, missões nem em nada que já existia. Validação: ContentValidator (0 erros) + AmbientacaoTests.
Uso: python Tools/gerar_ambientacao.py [mapa ...]"""
import json, math, os, random, sys

C = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Content")


def _raios():
    import glob
    r = {}
    for f in glob.glob(os.path.join(C, "monsters*.json")):
        d = json.load(open(f, encoding="utf-8")); d = d if isinstance(d, list) else d.get("monsters", [])
        for mo in d: r[mo["id"]] = float(mo.get("radius", 0.5))
    return r


RAIO_MONSTRO = _raios()
def L(f): return json.load(open(os.path.join(C, f), encoding="utf-8"))
def D(f, d): json.dump(d, open(os.path.join(C, f), "w", encoding="utf-8"), indent=2, ensure_ascii=False)

G = "glb:cenario/"
# tamanho (largura X, profundidade Z do .glb, em m) das peças grandes — vem do bbox dos modelos (tools/glb_bbox.py)
TAM = {
    "acropole_braseiro": (1.0, 1.0), "acropole_coluna_a": (1.7, 2.7), "acropole_coluna_b": (4.0, 2.0), "acropole_escadaria": (4.8, 6.3),
    "acropole_estatua_juramento": (2.4, 2.4), "brejo_barco_afundado": (1.9, 3.5), "brejo_mangue": (5.3, 4.6), "brejo_palafita": (5.1, 5.5),
    "brejo_tumulo_viuva": (1.4, 2.7), "canion_elevador_mina": (4.2, 3.6), "canion_suplicante_a": (1.3, 1.4), "canion_suplicante_b": (1.3, 1.8),
    "canion_vagonete_tombado": (1.9, 1.8), "costa_barraca_pescador": (3.5, 2.7), "costa_farol_quebrado": (6.7, 6.6), "costa_poco_balde": (2.2, 2.1),
    "costa_ruina_L_g": (6.4, 5.5), "costa_ruina_L_p": (4.3, 3.7), "escoria_basalto_brasa_a": (2.8, 2.2), "escoria_basalto_brasa_b": (2.2, 2.4),
    "escoria_bigorna": (1.9, 1.3), "escoria_ninho_fenix": (3.0, 2.7), "geada_arvore_neve_a": (8.1, 7.9), "geada_arvore_neve_b": (6.8, 6.4),
    "geada_arvore_neve_c": (8.7, 8.3), "geada_muralha_gelo": (4.8, 2.1), "geada_runa_gelo": (2.4, 2.0), "geada_tenda_peles": (5.1, 5.0),
    "geada_treno": (1.0, 3.0), "mata_arvore_forca_a": (4.1, 2.9), "mata_arvore_forca_b": (4.1, 2.9), "mata_moinho": (6.9, 4.3),
    "mata_poco_tampado": (2.2, 1.9), "mata_teia_arco": (5.9, 1.9), "vale_altar_runas": (3.7, 2.1), "vale_arvore_queimada_a": (3.5, 2.5),
    "vale_arvore_queimada_b": (5.4, 2.7), "vale_arvore_queimada_c": (3.5, 2.2), "vale_carroca_pedreiro": (2.6, 4.5), "vale_estatua_guerreiro": (4.2, 4.5),
    "vespera_banca_rasgada": (2.9, 1.9), "vespera_casa_tapada_g": (7.5, 7.6), "vespera_casa_tapada_p": (5.5, 5.9),
}
TRONCO = {"geada_arvore_neve_a": 0.7, "geada_arvore_neve_b": 0.6, "geada_arvore_neve_c": 0.75, "brejo_mangue": 0.9, "mata_arvore_forca_a": 0.45,
          "mata_arvore_forca_b": 0.45, "vale_arvore_queimada_a": 0.4, "vale_arvore_queimada_b": 0.45, "vale_arvore_queimada_c": 0.4}

# ---------------------------------------------------------------------------- kits por tema
def kit(tema):
    K = dict(
        arvores=["arvore_seca"], arvore_borda=["pinheiros"], chao=["capim_alto", "ossada", "cranios"], chao_terr=["ossada", "cranios", "sangue", "capim_alto"],
        tenda="tenda", carroca="carroca", altar=None, estatua="estatua_guardiao", poste="tocha_poste", lapides=["lapide"], ruinas=[G + "costa_ruina_L_p", "coluna_quebrada"],
        casa=["tenda"], pilar="coluna_quebrada", marco=None, fogo="fogueira", caixas=["caixotes", "barril"],
        veios=[("mat_erva_tonico", None, "Moita de erva")], placa_seta=[G + "costa_placa_seta_a", G + "costa_placa_seta_b"],
        lore={},
    )
    if tema == "vale":
        K.update(arvores=[G + "vale_arvore_queimada_a", G + "vale_arvore_queimada_b", G + "vale_arvore_queimada_c", "arvore_seca"],
                 arvore_borda=[G + "vale_arvore_queimada_a", G + "vale_arvore_queimada_c", "pinheiros"], chao=["capim_alto", "feno", "cranios", "capim_alto"],
                 carroca=G + "vale_carroca_pedreiro", altar=G + "vale_altar_runas", estatua=G + "vale_estatua_guerreiro", marco=G + "vale_estatua_guerreiro",
                 veios=[("mat_ferro_raso", "tool:picareta", "Veio de ferro raso"), ("mat_erva_tonico", None, "Moita de erva")])
    elif tema == "mata":
        K.update(arvores=[G + "mata_arvore_forca_a", G + "mata_arvore_forca_b", "arvore_seca", "pinheiros"], arvore_borda=["pinheiros", "pinheiros", G + "mata_arvore_forca_b"],
                 chao=["capim_alto", "capim_alto", "ossada", "cranios"], altar=G + "mata_poco_tampado", marco=G + "mata_moinho", casa=[G + "costa_barraca_pescador", "tenda"],
                 lapides=[G + "vespera_lapide_a", G + "vespera_lapide_b", G + "vespera_lapide_c", G + "vespera_lapide_d"],
                 veios=[("mat_tabua", "tool:machado", "Árvore seca (machado)"), ("mat_folha_amarga", None, "Moita de folha amarga"), ("mat_erva_tonico", None, "Moita de erva")])
    elif tema == "canion":
        K.update(arvores=["arvore_seca"], arvore_borda=["rocha", "arvore_seca"], chao=["capim_alto", "ossada", "cranios", G + "canion_suplicante_b"],
                 tenda="tenda_goblin", carroca=G + "canion_vagonete_tombado", altar=G + "canion_suplicante_a", estatua=G + "canion_suplicante_a",
                 marco=G + "canion_elevador_mina", ruinas=[G + "costa_ruina_L_p", "coluna_quebrada", "vagonete"],
                 veios=[("mat_ferro_eco", "tool:picareta", "Veio de ferro de eco"), ("mat_ferro_raso", "tool:picareta", "Veio de ferro raso")])
    elif tema == "brejo":
        K.update(arvores=[G + "brejo_mangue", "arvore_seca"], arvore_borda=[G + "brejo_mangue", G + "brejo_mangue", "arvore_seca"], chao=["capim_alto", "capim_alto", "redes", "estacas"],
                 tenda="tenda", carroca=G + "brejo_barco_afundado", altar=G + "brejo_tumulo_viuva", marco=G + "brejo_palafita", casa=[G + "brejo_palafita"],
                 lapides=[G + "vespera_lapide_a", G + "vespera_lapide_c", "lapide"],
                 veios=[("mat_cogumelo_cinza", None, "Cogumelos de cinza"), ("mat_folha_amarga", None, "Moita de folha amarga")])
    elif tema == "fantasma":
        K.update(arvores=[G + "vale_arvore_queimada_a", G + "vale_arvore_queimada_c", "arvore_seca"], arvore_borda=["arvore_seca", G + "vale_arvore_queimada_b"],
                 chao=["capim_alto", "cranios", "ossada", G + "vespera_lapide_d"], poste=G + "vespera_poste_apagado",
                 lapides=[G + "vespera_lapide_a", G + "vespera_lapide_b", G + "vespera_lapide_c", G + "vespera_lapide_d"],
                 casa=[G + "vespera_casa_tapada_p", G + "vespera_casa_tapada_g"], marco=G + "vespera_casa_tapada_g", carroca="carroca",
                 ruinas=[G + "vespera_casa_tapada_p", G + "vespera_banca_rasgada", "coluna_quebrada"],
                 veios=[("mat_cogumelo_cinza", None, "Cogumelos de cinza"), ("mat_erva_tonico", None, "Moita de erva")])
    elif tema == "neve":
        K.update(arvores=[G + "geada_arvore_neve_a", G + "geada_arvore_neve_b", G + "geada_arvore_neve_c"], arvore_borda=[G + "geada_arvore_neve_a", G + "geada_arvore_neve_c", "pinheiros"],
                 chao=["cristal_gelo", "ossada", "cranios", "capim_alto"], tenda=G + "geada_tenda_peles", carroca=G + "geada_treno", altar=G + "geada_runa_gelo",
                 marco=G + "geada_muralha_gelo", casa=[G + "geada_tenda_peles"], pilar=G + "geada_runa_gelo",
                 veios=[("mat_ferro_geada", "tool:picareta", "Veio de ferro de geada"), ("mat_erva_inverno", None, "Moita de erva de inverno")])
    elif tema == "lava":
        K.update(arvores=["arvore_seca", "rocha_lava"], arvore_borda=["rocha_lava", G + "escoria_basalto_brasa_a", G + "escoria_basalto_brasa_b"],
                 chao=[G + "escoria_basalto_brasa_c", "ossada", "cranios", "rachadura"], chao_terr=[G + "escoria_basalto_brasa_c", "ossada", "cranios", "rachadura"],
                 altar=G + "escoria_bigorna", marco=G + "escoria_ninho_fenix", pilar="rocha_lava", casa=["forja", "tenda"],
                 veios=[("mat_basalto", "tool:picareta", "Veio de basalto férreo")])
    elif tema == "acropole":
        K.update(arvores=["arvore_seca", "pinheiros"], arvore_borda=["pinheiros", "arvore_seca", G + "acropole_coluna_a"], chao=["capim_alto", "cranios", "ossada"],
                 altar=G + "acropole_braseiro", estatua=G + "acropole_estatua_juramento", marco=G + "acropole_estatua_juramento", pilar=G + "acropole_coluna_a",
                 ruinas=[G + "acropole_coluna_b", G + "acropole_coluna_a", "coluna_quebrada"], poste=G + "acropole_braseiro",
                 veios=[("mat_bronze", "tool:picareta", "Veio de bronze antigo")])
    elif tema == "praia":
        K.update(arvores=["pinheiros", "arvore_seca"], arvore_borda=["pinheiros", "pinheiros", "arvore_seca"], chao=["capim_alto", "monte_areia", "redes", "ossada"],
                 carroca="barco_encalhado", altar=G + "costa_poco_balde", marco=G + "costa_farol_quebrado", casa=[G + "costa_barraca_pescador"],
                 ruinas=[G + "costa_ruina_L_g", G + "costa_ruina_L_p", "coluna_quebrada"], veios=[("mat_erva_tonico", None, "Moita de erva")])
    elif tema in ("ceu", "lua"):
        K.update(arvores=["arvore_seca"], arvore_borda=["coluna_quebrada", "arvore_seca"], chao=["capim_alto", "cranios", "cristal_gelo" if tema == "lua" else "capim_alto"],
                 pilar=G + "acropole_coluna_a", estatua=G + "acropole_estatua_juramento", altar=G + "acropole_braseiro", marco=G + "acropole_coluna_b",
                 ruinas=[G + "acropole_coluna_b", G + "acropole_coluna_a", "coluna_quebrada"], veios=[("mat_erva_tonico", None, "Moita de erva")])
    return K

# ---------------------------------------------------------------------------- textos (contemplar / livro) por tipo de lugar
LORE = {
    "acampamento": ["Brasas ainda mornas e três sacos de dormir. Quem estava aqui saiu correndo — a panela ficou no fogo.",
                    "Um mapa riscado a carvão está preso numa estaca: um X vermelho e a palavra 'NÃO'."],
    "altar": ["As pedras do altar estão gastas no lugar das mãos. Gente demais pediu coisa demais aqui.",
              "Há oferendas recentes: um dente, uma moeda partida, uma fita. Alguém ainda acredita."],
    "ruina": ["Paredes de uma casa grande. No batente, marcas de altura de criança — a última bem baixinha.",
              "Entre os escombros, um diário encharcado. Só dá para ler a última linha: 'eles voltam quando escurece'."],
    "mirante": ["Daqui se vê a região inteira. Lá longe, uma coluna de fumaça — e mais longe ainda, o Porto.",
                "O vento é tão forte que assobia nas pedras. Os antigos diziam que é a região respirando."],
    "covas": ["Lápides sem nome, todas viradas para o mesmo lado. Ninguém sabe para onde elas olham.",
              "Uma cova está aberta — de dentro para fora."],
    "clareira": ["A árvore tem marcas de corda em todos os galhos. A terra embaixo não deixa crescer nem capim.",
                 "Um sapato pequeno pendurado num galho. Ninguém teve coragem de tirar."],
    "bigorna": ["A bigorna ainda está quente. O martelo sumiu, mas o eco das batidas continua."],
    "cratera": ["O chão desce em tigela. No fundo, pedras que brilham de leve quando a lua passa."],
    "praca": ["A praça da vila: o poço secou, as bancas rasgaram, o sino não tem mais corda."],
    "escavacao": ["Trilhos que entram na rocha e não saem. Os mineiros contavam que a montanha mastiga."],
    "refugio": ["O refúgio dos que ficaram. Cada tocha acesa aqui é uma promessa de que alguém volta."],
    "chefe": ["Ossos em círculo, todos roídos do mesmo jeito. O dono deste lugar não divide."],
    "secreto": ["Um esconderijo de verdade: alguém guardou coisas aqui e nunca voltou para buscar."],
}

def tipo_do_lugar(z):
    n = z.get("name", "").lower()
    if z["kind"] == "refugio": return "refugio"
    if z["kind"] == "chefe": return "chefe"
    if z["kind"] == "secreto": return "secreto"
    for chave, t in (("acampamento", "acampamento"), ("pouso", "acampamento"), ("caravana", "acampamento"), ("altar", "altar"), ("ruína", "ruina"),
                     ("ruina", "ruina"), ("mirante", "mirante"), ("covas", "covas"), ("cemit", "covas"), ("túmulo", "covas"), ("cripta", "covas"),
                     ("clareira", "clareira"), ("forca", "clareira"), ("bigorna", "bigorna"), ("forja", "bigorna"), ("cratera", "cratera"),
                     ("praça", "praca"), ("escava", "escavacao"), ("veio", "escavacao"), ("ninho", "chefe"), ("toca", "secreto")):
        if chave in n: return t
    return "ruina"

# ---------------------------------------------------------------------------- ocupação
class Mapa:
    def __init__(s, m, chegadas):
        s.m = m; s.hs = m["half_size"]; s.rnd = random.Random("amb:" + m["id"])
        s.props, s.obst, s.objs = [], [], []
        s.solidos = []                                  # (x, y, r) já ocupados
        s.caixas = []                                   # (x, y, meia-largura, meia-altura, giro) — muralhas, muros, casas
        for o in m["obstacles"]:
            if o.get("shape", "box") == "circle": s.solidos.append((o["x"], o["y"], o.get("r", 1.0)))
            elif o.get("solid", True): s.caixas.append((o["x"], o["y"], o.get("w", 1) / 2, o.get("h", 1) / 2, o.get("rot", 0)))
        for p in m["props"]: s.solidos.append((p["x"], p["y"], max(0.6, p.get("solid", 0))))
        s.guardas = []                                  # (x, y, r): nada sólido aqui
        for sp in m["spawns"]: s.guardas.append((sp["x"], sp["y"], max(3.0, RAIO_MONSTRO.get(sp.get("monster"), 0) + 1.5)))   # bicho grande (titã) precisa de mais espaço
        for p in m["portals"]: s.guardas.append((p["x"], p["y"], p["radius"] + 4.5))
        for (x, y) in chegadas: s.guardas.append((x, y, 4.0))
        for w in m.get("waypoints", []): s.guardas.append((w["x"] + 1, w["y"], 4.5))
        for n in m.get("npcs", []): s.guardas.append((n["x"], n["y"], 3.0))
        for o in m.get("objects", []): s.guardas.append((o["x"], o["y"], 2.2))
        for c in m.get("chests", []): s.guardas.append((c["x"], c["y"], 2.5))
        for c in m.get("checkpoints", []): s.guardas.append((c["x"], c["y"], 3.0))
        s.seguras = [z for z in m["zones"] if z["kind"] == "seguro"]
        s.territorios = [z for z in m["zones"] if z["kind"] in ("fraco", "medio", "forte", "perigoso")]
        s.elev = m.get("elevations", [])
        s.estradas = []
        for r in m.get("roads", []):
            pts = r["points"]
            for a, b in zip(pts, pts[1:]): s.estradas.append((a, b, r.get("width", 3.2) / 2))

    def dist_estrada(s, x, y):
        best = 1e9
        for (a, b, hw) in s.estradas:
            ax, ay = a; bx, by = b; dx, dy = bx - ax, by - ay; L2 = dx * dx + dy * dy or 1e-9
            t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / L2))
            d = math.hypot(x - (ax + t * dx), y - (ay + t * dy)) - hw
            best = min(best, d)
        return best

    def em_zona(s, zs, x, y, m=0.0):
        return any(abs(x - z["x"]) <= z["w"] / 2 + m and abs(y - z["y"]) <= z["h"] / 2 + m for z in zs)

    def na_rampa(s, x, y, r):
        # a rampa da elevação (faixa em volta do platô) não aceita peça sólida — ficaria torta; em cima do platô pode
        for e in s.elev:
            dx, dy = abs(x - e["x"]) - e["w"] / 2, abs(y - e["y"]) - e["h"] / 2
            fora = max(dx, dy)
            if -0.3 < fora + r and fora < e.get("ramp", 5) and dx < e.get("ramp", 5) and dy < e.get("ramp", 5): return True
        return False

    def dist_caixa(s, x, y):
        best = 1e9
        for (cx, cy, hw, hh, rot) in s.caixas:
            a = math.radians(rot); dx, dy = x - cx, y - cy
            lx, ly = dx * math.cos(a) - dy * math.sin(a), dx * math.sin(a) + dy * math.cos(a)
            qx, qy = abs(lx) - hw, abs(ly) - hh
            best = min(best, math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0))
        return best

    def livre(s, x, y, r, solido=True, estrada=1.0, territorio=True):
        if abs(x) > s.hs - 2.5 - r or abs(y) > s.hs - 2.5 - r: return False
        if s.dist_estrada(x, y) < r + estrada: return False
        for (gx, gy, gr) in s.guardas:
            if math.hypot(x - gx, y - gy) < gr + (r if solido else 0.3): return False
        if s.dist_caixa(x, y) < (r + 0.4 if solido else 0.2): return False
        if solido:
            for (ox, oy, orr) in s.solidos:
                if math.hypot(x - ox, y - oy) < orr + r + 0.4: return False
            if s.em_zona(s.seguras, x, y, r + 0.5): return False
            if not territorio and s.em_zona(s.territorios, x, y, r + 1.0): return False
            if s.na_rampa(x, y, r): return False
        else:
            for (ox, oy, orr) in s.solidos:
                if math.hypot(x - ox, y - oy) < orr * 0.8: return False
        return True

    def prop(s, kind, x, y, yaw=0.0, scale=1.0, solid=0.0, altura=0.0):
        p = {"kind": kind, "x": round(x, 2), "y": round(y, 2), "yaw": round(yaw % 360, 1), "amb": True}
        if abs(scale - 1) > 0.01: p["scale"] = round(scale, 2)
        if solid > 0: p["solid"] = round(solid, 2); s.solidos.append((x, y, solid))
        else: s.solidos.append((x, y, 0.25))
        if altura: p["altura"] = altura
        s.props.append(p); return p

    def caixa(s, x, y, w, h, rot):
        """colisão retangular girada (peças compridas: muralha, casa, escadaria)"""
        s.obst.append({"kind": "invisivel", "shape": "box", "x": round(x, 2), "y": round(y, 2), "w": round(w, 2), "h": round(h, 2), "r": 0,
                       "height": 3, "rot": round(rot % 360, 1), "amb": True})
        s.caixas.append((x, y, w / 2, h / 2, rot))

    def peca(s, kind, x, y, yaw=None, scale=1.0, solido=True, territorio=True, tentativas=6, raio_busca=1.5):
        """coloca uma peça (glb ou tipo do jogo) no primeiro lugar livre perto de (x,y); colisão pelo tamanho do modelo.
        Peça grande que não cabe tenta de novo menor (80% e 65%) — a cena não fica sem a peça principal."""
        nome = kind[len(G):] if kind.startswith(G) else None
        if nome and nome in TAM and max(TAM[nome]) * scale > 3.2 and nome not in TRONCO and not getattr(s, "_reduzindo", False):
            s._reduzindo = True
            try:
                for f in (1.0, 0.8, 0.65):
                    if s.peca(kind, x, y, yaw, scale * f, solido, territorio, max(tentativas, 10), raio_busca + 1.0): return True
                return False
            finally: s._reduzindo = False
        if nome and nome in TAM:
            w, h = TAM[nome]; w *= scale; h *= scale
            r = TRONCO.get(nome, min(w, h) * 0.45) * scale if nome in TRONCO else min(w, h) * 0.45
            grande = max(w, h) > 3.2 and nome not in TRONCO
        else:
            r = {"tenda": 1.8, "tenda_goblin": 1.8, "carroca": 1.5, "pinheiros": 1.1, "caixotes": 0.7, "barril": 0.4, "coluna_quebrada": 0.7,
                 "estatua_guardiao": 1.0, "rocha_lava": 1.2, "lapide": 0.45, "estacas": 0.5, "forja": 1.3, "vagonete": 0.7, "barco_encalhado": 1.6,
                 "fogueira": 0.5, "acampamento_abandonado": 0.9, "arvore_seca": 0.5, "rocha": 1.0}.get(kind, 0.4) * scale
            grande = False; w = h = 0
        for t in range(tentativas):
            px = x + (s.rnd.uniform(-raio_busca, raio_busca) if t else 0); py = y + (s.rnd.uniform(-raio_busca, raio_busca) if t else 0)
            if s.livre(px, py, (max(w, h) / 2 if grande else r) if solido else 0.3, solido=solido, territorio=territorio):
                yw = s.rnd.uniform(0, 360) if yaw is None else yaw
                if grande:
                    yw = round(yw / 90) * 90
                    s.prop(kind, px, py, yw, scale)
                    ww, hh = (w, h) if int(yw) % 180 == 0 else (h, w)
                    s.caixa(px, py, ww * 0.85, hh * 0.85, 0)
                else:
                    s.prop(kind, px, py, yw, scale, solid=(r if solido and r >= 0.3 else 0))
                return True
        return False

    def objeto(s, o):
        o["amb"] = True; s.objs.append(o); s.guardas.append((o["x"], o["y"], 1.8)); return o

# ---------------------------------------------------------------------------- cenas
def anel(cx, cy, r, n, fase=0.0):
    return [(cx + math.cos(fase + k / n * math.tau) * r, cy + math.sin(fase + k / n * math.tau) * r, math.degrees(fase + k / n * math.tau)) for k in range(n)]

def cena(M, K, z, n_lore):
    t = tipo_do_lugar(z); cx, cy = z["x"], z["y"]; rnd = M.rnd
    R = min(z["w"], z["h"]) / 2
    face = math.degrees(math.atan2(-cy, -cx)) if (cx or cy) else 0     # de frente para o meio do mapa
    if t == "acampamento":
        M.peca(K["fogo"], cx, cy, solido=True)
        M.peca("panela", cx + 1.2, cy - 0.6, solido=False); M.peca("carnes_penduradas", cx - 1.6, cy + 1.0)
        for k in range(5): M.peca(rnd.choice(["feno", "caixote", "barril", "ossada", "cesto_peixe"]), cx + rnd.uniform(-R, R) * 0.7, cy + rnd.uniform(-R, R) * 0.7)
        for (x, y, a) in anel(cx, cy, 5.5, 3, rnd.uniform(0, 6)): M.peca(K["tenda"], x, y, yaw=a + 180, raio_busca=2.0, tentativas=12)
        for (x, y, a) in anel(cx, cy, 2.6, 4, rnd.uniform(0, 6)): M.peca(rnd.choice(K["caixas"] + ["feno"]), x, y)
        M.peca(K["carroca"], cx + R * 0.75, cy - R * 0.4, yaw=rnd.choice([0, 90, 180, 270]))
        M.peca("estandarte", cx - R * 0.6, cy + R * 0.6, yaw=face)
        for (x, y, a) in anel(cx, cy, R * 0.9, 2, rnd.uniform(0, 6)): M.peca(K["poste"], x, y, yaw=a)
    elif t == "altar":
        if K["altar"]: M.peca(K["altar"], cx, cy, yaw=face)
        for (x, y, a) in anel(cx, cy, 5.0, 6, rnd.uniform(0, 1)): M.peca(K["pilar"] if rnd.random() < 0.7 else "coluna_quebrada", x, y, yaw=a)
        for (x, y, a) in anel(cx, cy, 2.4, 2, math.radians(face + 90)): M.peca(K["poste"], x, y, yaw=face)
        for (x, y, a) in anel(cx, cy, 3.2, 5, 0.4): M.peca(rnd.choice(["cranios", "ossada"]), x, y, solido=False)
    elif t == "ruina":
        rs = K["ruinas"]
        M.peca(rs[0], cx - 2, cy + 1, yaw=rnd.choice([0, 90, 180, 270]))
        if len(rs) > 1: M.peca(rs[1], cx + 3.5, cy - 2.5, yaw=rnd.choice([0, 90, 180, 270]))
        for (x, y, a) in anel(cx, cy, R * 0.8, 4, rnd.uniform(0, 6)): M.peca("coluna_quebrada", x, y, yaw=a)
        for (x, y, a) in anel(cx, cy, 3.0, 3, 1.0): M.peca(rnd.choice(K["caixas"] + ["ossada"]), x, y)
        M.peca("estandarte", cx + R * 0.7, cy + R * 0.6, yaw=face)
    elif t == "mirante":
        M.peca(K["estatua"] if K["estatua"] else "estatua_guardiao", cx, cy, yaw=face)
        M.peca("estandarte", cx + 3, cy + 2, yaw=face); M.peca("estandarte", cx - 3, cy + 2, yaw=face)
        M.peca("banco_praca", cx, cy - 3.5, yaw=face + 180)
        M.peca(rnd.choice(K["placa_seta"]), cx + 4, cy - 3, yaw=face)
        for (x, y, a) in anel(cx, cy, R * 0.85, 3, 0.7): M.peca(K["poste"], x, y, yaw=a)
    elif t in ("covas", "secreto"):
        if "túmulo" in z.get("name", "").lower() and K["altar"]: M.peca(K["altar"], cx, cy, yaw=face)
        for i in range(-2, 3):
            for j in range(-1, 2):
                if (i, j) == (0, 0) and t == "covas": continue
                M.peca(rnd.choice(K["lapides"]), cx + i * 2.2 + rnd.uniform(-0.3, 0.3), cy + j * 2.6 + rnd.uniform(-0.3, 0.3), yaw=face + rnd.uniform(-12, 12))
        for (x, y, a) in anel(cx, cy, R * 0.95, 6, 0.3): M.peca("cerca_quebrada", x, y, yaw=a + 90)
        M.peca(K["poste"], cx + R * 0.7, cy + R * 0.7, yaw=face)
        for k in range(4): M.peca(rnd.choice(["cranios", "ossada"]), cx + rnd.uniform(-R, R) * 0.7, cy + rnd.uniform(-R, R) * 0.7, solido=False)
    elif t == "clareira":
        for (x, y, a) in anel(cx, cy, 3.5, 3, rnd.uniform(0, 6)): M.peca(rnd.choice([G + "mata_arvore_forca_a", G + "mata_arvore_forca_b"]), x, y, yaw=a)
        for k in range(5): M.peca(rnd.choice(["cadaver", "ossada", "cranios", "sangue"]), cx + rnd.uniform(-R, R) * 0.6, cy + rnd.uniform(-R, R) * 0.6, solido=False)
        M.peca("estacas", cx + R * 0.6, cy - R * 0.5)
    elif t == "bigorna":
        M.peca(G + "escoria_bigorna", cx, cy, yaw=face); M.peca("forja", cx + 3, cy + 1.5, yaw=face)
        M.peca("glb:dg/dg_caldeirao_forja", cx - 3, cy + 1, yaw=face)
        for k in range(3): M.peca(rnd.choice([G + "escoria_basalto_brasa_a", G + "escoria_basalto_brasa_b"]), cx + rnd.uniform(-R, R) * 0.8, cy + rnd.uniform(-R, R) * 0.8)
        for (x, y, a) in anel(cx, cy, 2.6, 3, 2.0): M.peca(rnd.choice(K["caixas"]), x, y)
    elif t == "cratera":
        for (x, y, a) in anel(cx, cy, R * 0.85, 9, rnd.uniform(0, 6)): M.peca("rocha" if K["arvore_borda"][0] != "rocha_lava" else "rocha_lava", x, y, scale=rnd.uniform(0.8, 1.3))
        for k in range(6): M.peca(rnd.choice(["cristal_gelo", "cranios", "ossada"]), cx + rnd.uniform(-R, R) * 0.5, cy + rnd.uniform(-R, R) * 0.5, solido=False)
    elif t == "praca":
        M.peca(G + "vespera_banca_rasgada", cx - 3, cy + 2, yaw=face); M.peca(G + "vespera_banca_rasgada", cx + 3, cy + 2, yaw=face)
        M.peca(G + "costa_poco_balde", cx, cy - 1)
        for (x, y, a) in anel(cx, cy, R * 0.85, 4, 0.78): M.peca(K["poste"], x, y, yaw=a)
        M.peca("banco_praca", cx, cy + 4, yaw=face)
    elif t == "escavacao":
        M.peca("trilhos", cx, cy, yaw=face, solido=False); M.peca(G + "canion_vagonete_tombado", cx + 2.5, cy + 1.5, yaw=face + 30)
        M.peca("vagonete", cx - 2, cy - 2, yaw=face)
        for k in range(3): M.peca(rnd.choice(K["caixas"]), cx + rnd.uniform(-R, R) * 0.7, cy + rnd.uniform(-R, R) * 0.7)
        M.peca(K["poste"], cx + R * 0.7, cy - R * 0.6)
    elif t == "refugio":
        # casas/tendas da região do lado de fora da zona, de frente para o meio; tochas nos cantos, estandartes, caixas
        lados = [(cx - z["w"] / 2 - 4.5, cy, 90), (cx + z["w"] / 2 + 4.5, cy, 270), (cx - z["w"] / 4, cy + z["h"] / 2 + 4.5, 180), (cx + z["w"] / 4, cy + z["h"] / 2 + 4.5, 180)]
        for (x, y, a) in lados: M.peca(rnd.choice(K["casa"]), x, y, yaw=a, raio_busca=2.0, tentativas=10)
        for sx in (-1, 1):
            for sy in (-1, 1): M.peca(K["poste"], cx + sx * (z["w"] / 2 + 1.2), cy + sy * (z["h"] / 2 + 1.2), yaw=0)
        M.peca("estandarte", cx - z["w"] / 2 - 1.5, cy - z["h"] / 2 + 2, yaw=0); M.peca("estandarte", cx + z["w"] / 2 + 1.5, cy - z["h"] / 2 + 2, yaw=0)
        for k in range(6): M.peca(rnd.choice(K["caixas"] + ["feno", "barril"]), cx + rnd.choice([-1, 1]) * (z["w"] / 2 + rnd.uniform(1.5, 3)), cy + rnd.uniform(-z["h"] / 2, z["h"] / 2))
        M.peca(G + "porto_quadro_recados", cx + z["w"] / 2 + 2.2, cy + z["h"] / 2 - 1.5, yaw=270)
    elif t == "chefe":
        # arena: anel de pilares/braseiros na borda (o meio fica livre para a luta), ossos dentro, marco atrás
        for (x, y, a) in anel(cx, cy, R * 0.92, 8, rnd.uniform(0, 1)):
            M.peca(K["pilar"] if (int(a) // 45) % 2 == 0 else K["poste"], x, y, yaw=a + 180, raio_busca=1.2)
        for k in range(8): M.peca(rnd.choice(["ossada", "cranios", "sangue", "ossada"]), cx + rnd.uniform(-R, R) * 0.8, cy + rnd.uniform(-R, R) * 0.8, solido=False)
        trono = {"brejo": G + "mata_teia_arco", "neve": G + "geada_muralha_gelo", "lava": G + "escoria_ninho_fenix", "acropole": G + "acropole_estatua_juramento",
                 "mata": G + "mata_teia_arco", "canion": G + "canion_suplicante_a", "fantasma": G + "vespera_casa_tapada_p", "vale": G + "vale_estatua_guerreiro"}.get(M.tema)
        if trono:
            d = math.hypot(cx, cy) or 1
            M.peca(trono, cx + cx / d * (R + 4.5), cy + cy / d * (R + 4.5), yaw=face, raio_busca=2.0, tentativas=10)
    # história do lugar: um ponto de "contemplar" (ou livro no acampamento/ruína) no canto da cena
    textos = LORE.get(t)
    if textos and t not in ("chefe",):
        for k in range(8):
            ox, oy = cx + rnd.uniform(-R, R) * 0.6, cy + rnd.uniform(-R, R) * 0.6
            if M.livre(ox, oy, 0.6, solido=True):
                kind = "livro" if t in ("acampamento", "ruina", "secreto") else "contemplar"
                M.objeto({"id": f"amb_{M.m['id']}_{z['id']}", "kind": kind, "x": round(ox, 2), "y": round(oy, 2),
                          "label": {"livro": "Diário largado", "contemplar": "Observar"}[kind], "text": textos[n_lore % len(textos)]})
                if kind == "livro": M.peca("caixote", ox + 0.8, oy, solido=False)
                break

# ---------------------------------------------------------------------------- mapa inteiro
TEMA_DO_MAPA = {}   # id -> tema (vem do próprio mapa; "mata" etc.)
NAO = {"costa_do_cal", "porto_das_cinzas", "laboratorio", "santuario_arena", "santuario_ceu"}

def ambientar(m, chegadas):
    tema = m.get("theme", "")
    M = Mapa(m, chegadas); M.tema = tema; K = kit(tema); rnd = M.rnd; hs = M.hs
    # 1) cenas nos lugares com nome
    zs = [z for z in m["zones"] if z["kind"] in ("lugar", "secreto", "refugio", "chefe")]
    for i, z in enumerate(zs): cena(M, K, z, i)
    # 2) marco: no ponto livre mais visto (a 30–45% do caminho entre o refúgio/entrada e o centro), de frente para a entrada
    if K["marco"]:
        ent = next((z for z in m["zones"] if z["kind"] == "refugio"), None)
        ex, ey = (ent["x"], ent["y"]) if ent else (m["spawn_x"], m["spawn_y"])
        for k in range(60):
            a = rnd.uniform(0, math.tau); d = rnd.uniform(14, 26)
            x, y = ex * 0.6 + math.cos(a) * d, ey * 0.6 + math.sin(a) * d
            if M.peca(K["marco"], x, y, yaw=math.degrees(math.atan2(ey - y, ex - x)) - 90, territorio=False, tentativas=1): break
    # 3) estradas: poste a cada ~16 m, lado alternado; placa de seta perto de cada portal
    lado = 1
    for (a, b, hw) in M.estradas:
        ax, ay = a; bx, by = b; L = math.hypot(bx - ax, by - ay)
        if L < 0.5: continue
        nx, ny = -(by - ay) / L, (bx - ax) / L
        M._acum = getattr(M, "_acum", 0.0) + L
        if M._acum >= 16:
            M._acum = 0.0; lado = -lado
            x, y = (ax + bx) / 2 + nx * (hw + 1.3) * lado, (ay + by) / 2 + ny * (hw + 1.3) * lado
            if M.livre(x, y, 0.3, estrada=0.2):
                M.prop(K["poste"], x, y, math.degrees(math.atan2(ny, nx)), solid=0.25)
                if tema == "brejo" and rnd.random() < 0.6:
                    M.peca(G + "brejo_trilha_tabuas_reta", x + nx * 1.8 * lado, y + ny * 1.8 * lado, yaw=math.degrees(math.atan2(by - ay, bx - ax)) + 90, solido=False)
    for p in m["portals"]:
        for k in range(10):
            a = rnd.uniform(0, math.tau); x, y = p["x"] + math.cos(a) * (p["radius"] + 5.5), p["y"] + math.sin(a) * (p["radius"] + 5.5)
            if M.livre(x, y, 0.5, estrada=0.4):
                M.prop(rnd.choice(K["placa_seta"]), x, y, math.degrees(math.atan2(p["y"] - y, p["x"] - x)) + 90, solid=0.3); break
    # 4) mata da borda (faixa de 12 m) e árvores soltas fora dos territórios
    alvo_borda = int(hs * 0.85); feitas = 0
    for k in range(alvo_borda * 8):
        if feitas >= alvo_borda: break
        s = rnd.choice([-1, 1]); u = rnd.uniform(-hs + 4, hs - 4); f = rnd.uniform(hs - 13, hs - 4)
        x, y = (s * f, u) if rnd.random() < 0.5 else (u, s * f)
        if M.peca(rnd.choice(K["arvore_borda"]), x, y, scale=rnd.uniform(0.85, 1.25), territorio=False, tentativas=1): feitas += 1
    alvo_meio = int(hs * 0.35); feitas = 0
    for k in range(alvo_meio * 10):
        if feitas >= alvo_meio: break
        x, y = rnd.uniform(-hs + 14, hs - 14), rnd.uniform(-hs + 14, hs - 14)
        if M.peca(rnd.choice(K["arvores"]), x, y, scale=rnd.uniform(0.8, 1.2), territorio=False, tentativas=1): feitas += 1
    # 5) chão: enfeites sem colisão (mais nos territórios: restos de caça)
    # 5a) grupos de rochas (3–5 juntas) fora dos territórios — quebra o chão liso
    for k in range(int(hs / 5)):
        x, y = rnd.uniform(-hs + 8, hs - 8), rnd.uniform(-hs + 8, hs - 8)
        if not M.livre(x, y, 2.5, territorio=False): continue
        for j in range(rnd.randint(3, 5)):
            M.peca("rocha" if tema != "lava" else "rocha_lava", x + rnd.uniform(-2.2, 2.2), y + rnd.uniform(-2.2, 2.2), scale=rnd.uniform(0.5, 1.2), territorio=False, tentativas=2, raio_busca=0.8)
    alvo_chao = int(hs * 2.6); feitas = 0
    for k in range(alvo_chao * 6):
        if feitas >= alvo_chao: break
        x, y = rnd.uniform(-hs + 4, hs - 4), rnd.uniform(-hs + 4, hs - 4)
        terr = M.em_zona(M.territorios, x, y)
        kind = rnd.choice(K["chao_terr"] if terr else K["chao"])
        if M.peca(kind, x, y, solido=False, tentativas=1): feitas += 1
    # 6) coleta perto das cenas (1 por lugar, alternando o que a região dá)
    n = 0
    for z in zs:
        if z["kind"] not in ("lugar", "secreto"): continue
        it, req, label = K["veios"][n % len(K["veios"])]
        for k in range(12):
            a = rnd.uniform(0, math.tau); d = min(z["w"], z["h"]) / 2 + rnd.uniform(2, 5)
            x, y = z["x"] + math.cos(a) * d, z["y"] + math.sin(a) * d
            if M.livre(x, y, 1.0):
                o = {"id": f"amb_{m['id']}_veio{n}", "kind": "veio", "x": round(x, 2), "y": round(y, 2), "label": label, "yields": it,
                     "yield_min": 1, "yield_max": 2, "respawn_seconds": 600}
                if req: o["requires_flag"] = req
                M.objeto(o); n += 1; break
    # 7) uma armadilha (placa de pressão) na entrada de cada ruína
    for z in zs:
        if tipo_do_lugar(z) == "ruina":
            for k in range(8):
                x, y = z["x"] + rnd.uniform(-3, 3), z["y"] + rnd.uniform(-3, 3)
                if M.livre(x, y, 0.8):
                    M.objeto({"id": f"amb_{m['id']}_{z['id']}_placa", "kind": "placa_pressao", "x": round(x, 2), "y": round(y, 2), "label": "Placa de pressão"}); break
    return M

def main():
    w = L("world.json")
    sel = set(sys.argv[1:])
    mapas = {}
    for f in w["map_files"]:
        m = L(f); mapas[m["id"]] = (f, m)
    # pontos de chegada em cada mapa (portais de outros mapas que levam para ele)
    chegadas = {}
    for f, m in mapas.values():
        for p in m["portals"]: chegadas.setdefault(p["to_map"], []).append((p.get("to_x", 0), p.get("to_y", 0)))
    feitos = []
    for mid, (f, m) in mapas.items():
        # limpa o que este script pôs antes
        m["props"] = [p for p in m["props"] if not p.get("amb")]
        m["obstacles"] = [o for o in m["obstacles"] if not o.get("amb")]
        m["objects"] = [o for o in m.get("objects", []) if not o.get("amb")]
        if m.get("kind") != "campo" or mid in NAO or (sel and mid not in sel):
            D(f, m); continue
        M = ambientar(m, chegadas.get(mid, []))
        m["props"] += M.props; m["obstacles"] += M.obst; m["objects"] = m.get("objects", []) + M.objs
        D(f, m)
        feitos.append((mid, m.get("theme"), len(M.props), len(M.obst), len(M.objs), len(m["props"]), len(m["obstacles"])))
    for x in feitos: print("ambientado %-22s tema %-9s +%3d peças +%2d colisões +%2d objetos  (total %d peças, %d obstáculos)" % x)

if __name__ == "__main__":
    main()

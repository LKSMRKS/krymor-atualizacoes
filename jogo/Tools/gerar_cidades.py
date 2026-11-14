#!/usr/bin/env python3
"""
13/11 (pedido do dono: "gostaria de pelo menos mais 3 cidades, tipo a principal, mas diferente, com outro estilo,
temática, entre os mapas — cidades de interação, não de monstros"): três cidades seguras com moradores, lojas que só
existem ali, missões locais, pedra de viagem e duas saídas (uma para cada região vizinha).

13/11 22:26 (dono: "serão 4 cidades: a 1ª do nível 10, a 2ª do 20, a 3ª do 35 e a 4ª do 50 — vê se os números batem com os
monstros, portais e a progressão dos mapas"):
  Vila das Mós   (nível 10) entre a Costa do Cal (1–10) e o Vale das Cinzas (11–16) — aldeia de moleiros: moinho, horta, feira
  Astrália       (nível 20) entre a Mata dos Enforcados (17–20) e os Cânions (20–30) — cidade dos astrônomos; o Merlin mora aqui
  Candeia        (nível 35) entre o Brejo da Viúva (28–36) e a Véspera (34–44) — vila das lanternas sobre palafitas, no crepúsculo
  Brasaforte     (nível 50) entre a Coroa da Geada (42–50) e o Mar de Escória (48–56) — cidadela-forja na neve

Cria/reescreve: Content/map_vila_das_mos.json, map_astralia.json, map_brasaforte.json, quests_cidades.json.
Mexe (idempotente, só acrescenta): shops.json (3 lojas novas), world.json (3 regiões + mapas + ligações) e os 6 campos
vizinhos (um portal "cidade_*" em cada). Tudo o que este script põe nos campos leva "cidade": true e é refeito.
Rodar depois de Tools/gerar_regioes.py (que reescreve os campos) e antes de Tools/gerar_merlin.py.
Uso (raiz do projeto): python3 Tools/gerar_cidades.py
"""
import json
import math
import os
import random

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")

# colisão dos prédios grandes (copiada do Porto, onde foi tirada das caixas do modelo; yaw 0, relativo ao centro)
CAIXAS = {"taverna_grande": [[-5.28, 4.25, 1.45, 0.5, 3.6], [-3.9, 4.25, 1.3, 0.5, 1.3], [-2.33, 4.25, 1.85, 0.5, 3.6], [2.33, 4.25, 1.85, 0.5, 3.6], [3.9, 4.25, 1.3, 0.5, 1.3], [5.28, 4.25, 1.45, 0.5, 3.6], [-5.05, -4.25, 1.9, 0.5, 3.6], [-3.5, -4.25, 1.2, 0.5, 1.3], [0.0, -4.25, 5.8, 0.5, 3.6], [3.5, -4.25, 1.2, 0.5, 1.3], [5.05, -4.25, 1.9, 0.5, 3.6], [-5.75, 2.33, 0.5, 3.35, 3.6], [-5.75, 0.0, 0.5, 1.3, 1.3], [-5.75, -2.33, 0.5, 3.35, 3.6], [5.75, 3.05, 0.5, 1.9, 3.6], [5.75, 1.5, 0.5, 1.2, 1.3], [5.75, -1.55, 0.5, 4.9, 3.6], [-1.7, 6.0, 0.4, 0.4, 3.4], [1.7, 6.0, 0.4, 0.4, 3.4], [-3.0, 1.0, 1.3, 1.3, 1.2], [0.8, 1.4, 1.3, 1.3, 1.2], [-1.0, -1.6, 1.3, 1.3, 1.2], [3.6, -1.3, 1.3, 1.3, 1.2], [-3.0, -2.9, 3.6, 0.8, 1.5], [5.05, -1.6, 0.9, 2.4, 2.6]], "casarao_cidade": [[-8.0, 8.4, 2.3, 0.9, 3], [-6.65, 5.72, 2.7, 0.55, 4.4], [-4.6, 5.72, 1.4, 0.55, 1.6], [-2.75, 5.72, 2.3, 0.55, 4.4], [2.75, 5.72, 2.3, 0.55, 4.4], [4.6, 5.72, 1.4, 0.55, 1.6], [6.65, 5.72, 2.7, 0.55, 4.4], [-5.82, -5.72, 4.35, 0.55, 4.4], [-3.0, -5.72, 1.3, 0.55, 1.6], [0.4, -5.72, 5.5, 0.55, 4.4], [3.8, -5.72, 1.3, 0.55, 1.6], [6.22, -5.72, 3.55, 0.55, 4.4], [-7.72, 1.8, 0.55, 7.3, 4.4], [-7.72, -2.5, 0.55, 1.3, 1.6], [-7.72, -4.3, 0.55, 2.3, 4.4], [7.72, 4.45, 0.55, 2.0, 4.4], [7.72, 2.8, 0.55, 1.3, 1.6], [7.72, -1.65, 0.55, 7.6, 4.4], [-2.6, -0.8, 0.5, 0.5, 4.4], [2.6, -0.8, 0.5, 0.5, 4.4], [-2.0, 7.9, 0.45, 0.45, 4.0], [2.0, 7.9, 0.45, 0.45, 4.0], [-4.2, -4.0, 4.2, 0.9, 1.6], [3.0, 1.2, 1.4, 4.2, 1.3], [-1.5, 2.6, 1.3, 1.3, 1.2], [6.95, -1.6, 1.0, 2.8, 2.8], [-5.55, 2.33, 0.15, 3.45, 4.5]], "portao_fortificado": [[-3.2, 0.0, 1.6, 2.4, 8], [3.2, 0.0, 1.6, 2.4, 8], [-3.9, 0.0, 3.96, 3.96, 9.0], [3.9, 0.0, 3.96, 3.96, 9.0], [-2.4, 0.0, 0.8, 2.0, 7.0], [2.4, 0.0, 0.8, 2.0, 7.0], [0.0, -6.6, 12, 1.0, 3], [-4.6, -3.3, 1.0, 6.6, 3], [4.6, -3.3, 1.0, 6.6, 3]]}

# tamanho (yaw 0: largura em x, fundo em y) dos prédios de bloco e dos modelos do acervo usados como marco
BLOCO = {"sobrado": (5.3, 4.8), "casa_pequena": (4.7, 4.4), "casa_enxaimel": (6.5, 5.0)}
GLB = {  # nome: (minx, maxx, miny, maxy) no chão, yaw 0
    "cenario/mata_moinho": (-3.45, 3.45, -1.95, 2.37), "cenario/costa_poco_balde": (-1.05, 0.94, -1.79, 0.26),
    "cenario/vale_carroca_pedreiro": (-0.69, 1.83, -3.02, 1.46), "cenario/acropole_estatua_juramento": (-1.2, 1.2, -1.2, 1.2),
    "cenario/acropole_coluna_a": (-0.58, 1.14, -1.69, 1.04), "cenario/acropole_coluna_b": (-1.94, 2.11, -1.01, 0.98),
    "cenario/acropole_braseiro": (-0.5, 0.51, -0.5, 0.5), "cenario/escoria_bigorna": (-0.63, 1.25, -0.63, 0.63),
    "cenario/geada_tenda_peles": (-2.53, 2.6, -2.48, 2.51), "cenario/geada_treno": (-0.52, 0.52, -1.81, 1.22),
    "cenario/geada_runa_gelo": (-1.22, 1.18, -0.96, 1.0), "cenario/costa_barraca_pescador": (-1.75, 1.75, -1.35, 1.36),
    "cenario/porto_quadro_recados": (-1.19, 1.19, -0.2, 0.3), "cenario/astralia_observatorio": (-3.7, 3.7, -3.7, 4.12), "cenario/vale_altar_runas": (-1.84, 1.84, -1.19, 0.9),
    "cenario_ponte_arcos": (-6.0, 6.0, -1.72, 1.72), "cenario/cenario_ponte_reparada": (-1.04, 1.04, -6.14, 0.14),
    "cenario/cenario_arvore_seca": (-0.86, 0.93, -0.59, 0.59), "cenario/cenario_rocha_1": (-0.87, 0.84, -0.61, 0.66),
}


def L(name):
    return json.load(open(os.path.join(C, name), encoding="utf-8"))


def D(name, obj):
    json.dump(obj, open(os.path.join(C, name), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


def rot(x, y, yaw):
    """Mesmo giro do reorganizar_porto.py (yaw do Godot em graus; y do mapa = z do modelo)."""
    a = math.radians(yaw)
    return x * math.cos(a) + y * math.sin(a), -x * math.sin(a) + y * math.cos(a)


def frente(yaw):
    """Para onde a porta olha: yaw 0 = +Y, 90 = +X, 180 = −Y, 270 = −X (casas do Porto)."""
    a = math.radians(yaw)
    return round(math.sin(a), 3), round(math.cos(a), 3)


class Cidade:
    def __init__(self, id_, nome, tema, H=36):
        self.m = {"id": id_, "display_name": nome, "half_size": H, "spawn_x": 0, "spawn_y": 0, "safe_radius": 2 * H, "xp_multiplier": 1,
                  "kind": "cidade", "region": id_, "theme": tema, "no_mount": False,
                  "zones": [{"id": "cidade", "name": nome, "kind": "seguro", "x": 0, "y": 0, "w": 2 * H, "h": 2 * H}],
                  "obstacles": [], "gates": [], "chests": [], "spawns": [], "props": [], "portals": [], "waypoints": [],
                  "npcs": [], "objects": [], "checkpoints": []}
        self.id = id_
        self.livres = []      # áreas que os enfeites espalhados não podem ocupar (ruas, praça, frentes)

    # ---------------------------------------------------------------- peças
    def box(self, kind, x, y, w, h, height=4.0):
        self.m["obstacles"].append({"kind": kind, "shape": "box", "x": round(x, 2), "y": round(y, 2), "w": round(w, 2), "h": round(h, 2), "r": 0, "height": height})

    def circ(self, kind, x, y, r, height=4.0):
        self.m["obstacles"].append({"kind": kind, "shape": "circle", "x": round(x, 2), "y": round(y, 2), "w": 0, "h": 0, "r": round(r, 2), "height": height})

    def prop(self, kind, x, y, yaw=0, scale=None):
        p = {"kind": kind, "x": round(x, 2), "y": round(y, 2), "yaw": yaw}
        if scale:
            p["scale"] = scale
        self.m["props"].append(p)

    def caixa_rot(self, x, y, mnx, mxx, mny, mxy, yaw, kind="invisivel", height=4.0):
        pts = [rot(a, b, yaw) for a in (mnx, mxx) for b in (mny, mxy)]
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        self.box(kind, x + (min(xs) + max(xs)) / 2, y + (min(ys) + max(ys)) / 2, max(xs) - min(xs), max(ys) - min(ys), height)

    def predio(self, kind, x, y, yaw, porta=None):
        """Prédio + colisão. porta = (rótulo, texto) põe uma porta de interagir na frente. Devolve o ponto da calçada."""
        self.prop(kind, x, y, yaw)
        if kind in CAIXAS:
            for (dx, dy, w, h, ht) in CAIXAS[kind]:
                self.caixa_rot(x, y, dx - w / 2, dx + w / 2, dy - h / 2, dy + h / 2, yaw, height=ht or 4)
            prof = {"taverna_grande": 4.5, "casarao_cidade": 6.3, "portao_fortificado": 0}[kind]
        elif kind in BLOCO:
            w, h = BLOCO[kind]
            self.caixa_rot(x, y, -w / 2, w / 2, -h / 2, h / 2, yaw, height=6)
            prof = h / 2
        else:
            mnx, mxx, mny, mxy = GLB[kind[4:]]
            self.caixa_rot(x, y, mnx, mxx, mny, mxy, yaw, height=3)
            prof = mxy
        fx, fy = frente(yaw)
        if porta:
            o = {"id": f"{self.id}_porta_{len(self.m['objects'])}", "kind": "porta", "x": round(x + fx * (prof + 0.35), 2),
                 "y": round(y + fy * (prof + 0.35), 2), "label": porta[0], "text": porta[1]}
            if len(porta) > 2:
                o["grant_flag"] = porta[2]          # "porta:entra" = dá para entrar (sala de casa pequena; a saída volta aqui)
            self.m["objects"].append(o)
        cx, cy = x + fx * (prof + 1.8), y + fy * (prof + 1.8)
        self.livres.append((cx, cy, 1.6))
        return cx, cy

    def npc(self, id_, nome, role, x, y, look, saudacao, shop=None, facing=None):
        n = {"id": id_, "name": nome, "role": role, "x": round(x, 2), "y": round(y, 2), "facing": facing if facing is not None else 180,
             "look": look, "greeting": saudacao}
        if shop:
            n["shop"] = shop
        self.m["npcs"].append(n)
        self.livres.append((x, y, 1.5))

    def rua(self, x0, y0, x1, y1, larg):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) / 2) + 1
        for i in range(n + 1):
            t = i / n
            self.livres.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, larg / 2))
        self.m.setdefault("roads", []).append({"points": [[x0, y0], [x1, y1]], "width": larg})

    def ocupado(self, x, y, r):
        H = self.m["half_size"]
        if abs(x) > H - 3 or abs(y) > H - 3:
            return True
        for o in self.m["obstacles"]:
            if o["shape"] == "circle":
                if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r:
                    return True
            elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r:
                return True
        if any(math.hypot(x - a, y - b) < rr + r for a, b, rr in self.livres):
            return True
        if any(math.hypot(x - p["x"], y - p["y"]) < 1.4 + r for p in self.m["props"]):
            return True
        return any(math.hypot(x - p["x"], y - p["y"]) < 3.5 + r for p in self.m["portals"])

    def espalhar(self, kinds, n, seed, solido=0.0, area=None):
        rnd = random.Random(seed)
        H = self.m["half_size"]
        x0, x1, y0, y1 = area or (-H + 4, H - 4, -H + 4, H - 4)
        k = 0
        for _ in range(n * 40):
            if k >= n:
                break
            x, y = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
            if self.ocupado(x, y, 1.0):
                continue
            self.prop(kinds[k % len(kinds)], x, y, rnd.randrange(0, 360, 15))
            if solido:
                self.circ("invisivel", x, y, solido, 2)
            k += 1

    def borda(self, kind_vis, portoes, passo=6.0, solida=True):
        """Contorno da cidade (muralha visível ou fileira de árvores) com vãos nos portões [(lado, posição)]."""
        H = self.m["half_size"]
        e = H - 2.5
        vao = 4.5
        for lado in ("N", "S", "L", "O"):
            vaos = [p for (l, p) in portoes if l == lado]
            t = -e
            while t < e:
                t2 = min(e, t + passo)
                segs = [(t, t2)]
                for v in vaos:
                    novo = []
                    for a, b in segs:
                        if b <= v - vao or a >= v + vao:
                            novo.append((a, b))
                        else:
                            if a < v - vao:
                                novo.append((a, v - vao))
                            if b > v + vao:
                                novo.append((v + vao, b))
                    segs = novo
                for a, b in segs:
                    if b - a < 0.5:
                        continue
                    c = (a + b) / 2
                    if kind_vis == "muralha":
                        if lado in ("N", "S"):
                            self.box("muralha", c, e if lado == "N" else -e, b - a, 1.2, 5)
                        else:
                            self.box("muralha", e if lado == "L" else -e, c, 1.2, b - a, 5)
                    else:
                        x, y = (c, e) if lado == "N" else (c, -e) if lado == "S" else (e, c) if lado == "L" else (-e, c)
                        self.circ(kind_vis, x, y, 1.1 if kind_vis == "arvore" else 0.9, 5)
                t = t2


# =================================================================================================== Vila das Mós
def vila_das_mos():
    c = Cidade("vila_das_mos", "Vila das Mós", "vale", H=34)
    c.rua(-31, 0, 31, 0, 5)                   # estrada da Costa (oeste) ao Vale (leste)
    c.rua(0, 0, 0, 16, 4)                     # rua do moinho
    # praça do poço e o moinho grande ao norte (marco: a vila inteira vive dele)
    c.predio("glb:cenario/mata_moinho", 0, 21, 180)
    c.predio("glb:cenario/costa_poco_balde", 5, 7, 200)
    c.m["waypoints"].append({"id": "wp_vila_das_mos", "name": "Poço da Vila das Mós", "x": -4.5, "y": 6.5})
    c.livres.append((-4.5, 6.5, 2.2))
    # casas: enxaimel e casinhas, a estalagem do lado sul
    est = c.predio("taverna_grande", 15, -18, 0, ("Estalagem do Moinho", "Cheiro de pão quente e cerveja de cevada. «Entra, que a sopa é de graça pra quem traz notícia!»"))
    c.predio("casa_enxaimel", -15, 17, 180, ("Porta", "Uma cesta de ovos na soleira e um bilhete: «Não pise na galinha.» A porta está só encostada.", "porta:entra"))
    c.predio("casa_pequena", -24, 15, 180, ("Porta", "Fechada. Lá dentro alguém ronca alto."))
    moleira = c.predio("sobrado", 13, 17, 180, ("Casa da Moleira", "Sacos de farinha empilhados até o teto. «Farinha da boa, moída na pedra!»"))
    c.predio("casa_pequena", 23, 15, 180)
    c.predio("casa_enxaimel", -14, -15, 0, ("Porta", "A porta range e abre. «Pode entrar, mas o cachorro morde!»", "porta:entra"))
    c.predio("casa_pequena", -24, -14, 0)
    # horta comunitária (canteiros de verdade) com cerca e espantalho; feira do lado leste
    for i, x in enumerate((-24, -19, -14)):
        c.m["objects"].append({"id": f"vila_canteiro{i}", "kind": "canteiro", "x": x, "y": -6.5, "label": "Horta da Vila"})
        c.livres.append((x, -6.5, 1.8))
    for x in (-26.5, -21.5, -16.5, -11.5):
        c.prop("cerca", x, -9.2, 0)
    c.prop("espantalho", -19, -10.5, 0)
    c.prop("regadores", -11, -5.5, 30)
    for i, x in enumerate((8, 12.5)):
        c.predio("glb:cenario/costa_barraca_pescador", x, -6.5, 0)
    c.prop("carrinho_feira", 17, -5.5, 90)
    c.predio("glb:cenario/vale_carroca_pedreiro", 24, 6, 90)
    c.prop("feno", 7, 13, 0); c.prop("feno", 9, 13.5, 40); c.prop("feno", -7, 14, 10)
    c.predio("glb:cenario/porto_quadro_recados", -9, 3.5, 0)
    c.borda("arvore", [("O", 0), ("L", 0)], passo=5.5)
    c.espalhar(["floreira", "barril", "caixote", "vaso_planta", "banco_praca", "poste", "canteiro_flores"], 26, 11, solido=0.5)
    c.espalhar(["capim_alto", "pinheiros"], 14, 12)
    # moradores
    c.npc("moleira_benta", "Benta, a Moleira", "missoes", moleira[0] - 1.5, moleira[1] - 0.5, "aldeao",
          "«O moinho não para nem domingo! Se você tem braço, eu tenho serviço — e pão quentinho de pagamento.»")
    c.npc("estalajadeiro_otaviano", "Otaviano, o Estalajadeiro", "missoes", est[0], est[1] + 0.5, "aldeao",
          "«Quarto? Tem. Cama? Tem. Silêncio? Só depois que os viajantes do Vale param de contar história.»", facing=0)
    c.npc("mascate_quirino", "Quirino, o Mascate de Mapas", "loja", 3.5, -4.5, "aldeao",
          "«Mapa do tesouro, freguês! Uns são verdadeiros. Os outros… também, de certo jeito.»", shop="loja_vila_mapas", facing=0)
    c.npc("erveira_celeste", "Celeste, a Erveira", "curandeira", -9, -3.5, "curandeira",
          "«Chá de erva-tônica pra quem vai ao Vale. E pra quem volta, um abraço.»", shop="loja_curandeira", facing=0)
    c.npc("ferreiro_ze", "Zé Ferradura, o Ferreiro", "ferreiro", 21, -9, "ferreiro",
          "«Ferradura, foice, faca… e espada também, se você insistir.»", shop="loja_ferreiro", facing=270)
    c.npc("menino_tiquinho", "Tiquinho", "morador", -3, 13, "aldeao",
          "«Quando o vento para, o moinho chora. A Benta diz que é a pedra rangendo. Eu acho que é fantasma.»", facing=180)
    c.npc("guarda_fiel", "Fiel, o Guarda da Estrada", "morador", -27, 3, "guarda",
          "«Daqui pra oeste é a Costa do Cal. Pra leste, o Vale das Cinzas. Eu fico aqui no meio, que é mais seguro.»", facing=90)
    # 14/11 ("bancadas específicas pelo mapa que liberam itens de acordo com a progressão"): bancada própria da cidade
    c.prop("bancada", 18, -12.5, 0); c.circ("invisivel", 18, -12.5, 1.1, 2)
    c.m["objects"].append({"id": "vila_bancada", "kind": "bancada", "x": 18, "y": -14.6, "label": "Bancada do Seleiro"})
    c.livres.append((18, -12.5, 2.2))
    c.m["spawn_x"], c.m["spawn_y"] = 0, 3.5
    return c, [("O", "costa_do_cal", "Costa do Cal", (-31.0, 0.0)), ("L", "vale_das_cinzas", "Vale das Cinzas", (31.0, 0.0))]


# =================================================================================================== Astrália
def astralia():
    c = Cidade("astralia", "Astrália, a Cidade das Lunetas", "acropole", H=36)
    c.rua(-33, 0, 33, 0, 6)
    c.rua(0, 0, 0, 13, 5)
    # o Observatório (a torre do Merlin) domina o norte; porta virada para a praça
    porta_obs = c.predio("glb:cenario/astralia_observatorio", 0, 19, 180)
    c.predio("glb:cenario/acropole_estatua_juramento", 0, 4.5, 180)
    c.m["waypoints"].append({"id": "wp_astralia", "name": "Praça das Lunetas", "x": -5.5, "y": -3.5})
    c.livres.append((-5.5, -3.5, 2.2))
    # colunata ao longo da avenida
    for x in (-26, -19, -12, 12, 19, 26):
        for y in (-4.6, 4.6):
            c.predio("glb:cenario/acropole_coluna_b" if (x + y) % 3 else "glb:cenario/acropole_coluna_a", x, y, 0 if y < 0 else 180)
    for x, y in ((-4, 9), (4, 9), (-4, -8), (4, -8)):
        c.predio("glb:cenario/acropole_braseiro", x, y, 0)
    # prédios
    c.predio("casarao_cidade", -19, 19, 180, ("Academia Estelar", "Corredores cheios de mapas do céu. Alguém riscou um deles: «Faltam quatro estrelas aqui. Ninguém sabe por quê.»"))
    c.predio("sobrado", 17, 18, 180, ("Casa da Astrônoma", "Uma luneta aponta pela janela. Na porta: «Volto quando a lua estiver no lugar.»"))
    c.predio("sobrado", 25, 16, 180)
    tav = c.predio("taverna_grande", 17, -18, 0, ("Taverna do Cometa", "Cantoria e canecas. Na parede, um cometa pintado com a cauda torta."))
    c.predio("sobrado", -20, -17, 0, ("Ateliê da Alfaiate", "Tecidos azuis bordados de estrelas. «Roupa de herói se mede pela coragem, não pela barriga.»"))
    c.predio("casa_pequena", -11, -16, 0)
    c.predio("casa_pequena", -28, -15, 0)
    for x, y in ((-8, 15), (8, 15), (-10, -10), (10, -10)):
        c.m["props"].append({"kind": "glb:dg/dg_astrolabio", "x": x, "y": y, "yaw": 45})
        c.circ("invisivel", x, y, 0.8, 2)
    for i, (x, y) in enumerate(((-30, 10), (30, 10), (-30, -10), (30, -10))):
        c.m["objects"].append({"id": f"astralia_ceu{i}", "kind": "contemplar", "x": x, "y": y, "label": "Olhar o céu",
                               "text": ("Pela luneta pública dá para ver a lua — um palmo fora do lugar de sempre, dizem os astrônomos.",
                                        "Quatro pontos escuros no céu, onde deveria haver estrelas. Os mapas antigos têm. Os novos, não.",
                                        "Uma estrela cadente risca o céu e some para os lados dos Cânions.",
                                        "O Observatório pisca uma luz azul lá no alto. O velho Merlin nunca dorme.")[i]})
    c.borda("muralha", [("O", 0), ("L", 0)], passo=8)
    c.espalhar(["floreira", "banco_praca", "poste", "estandarte", "vaso_planta", "canteiro_flores"], 24, 21, solido=0.5)
    c.npc("astronoma_dalia", "Dália, a Astrônoma-Chefe", "missoes", 14, 12.5, "arcanista",
          "«Os mapas do céu não fecham. Faltam quatro estrelas — e a lua anda um palmo fora do lugar. Alguém mexeu no céu.»")
    c.npc("alfaiate_lorena", "Lorena, a Alfaiate das Estrelas", "loja", -20, -11.5, "aldeao",
          "«Mantos, capuzes e elmos que não se acham em lugar nenhum. Feitos aqui, com linha de estrela.»", shop="loja_astralia_alfaiate", facing=0)
    c.npc("irma_vesper", "Irmã Vésper, a Boticária", "curandeira", 9, -3.5, "curandeira",
          "«Tônicos destilados à luz da lua. Funcionam igual aos outros, só são mais bonitos.»", shop="loja_curandeira", facing=0)
    c.npc("escriba_tobias", "Tobias, o Escriba", "missoes", -14, 11.5, "aldeao",
          "«Copio os mapas do céu desde menino. Os antigos têm quatro reinos desenhados nas bordas. Os novos, não. Curioso, né?»")
    c.npc("capitao_orion", "Capitão Órion, da Guarda", "morador", -29, 3, "guarda",
          "«A oeste, a Mata dos Enforcados. A leste, os Cânions. Astrália fica no meio e olha pro céu, que é mais bonito.»", facing=90)
    c.npc("menina_estela", "Estela", "morador", 5, 1.5, "aldeao",
          "«O Merlin me deixou olhar pela luneta grande! Vi um castelo de cabeça para baixo na lua. Ninguém acredita.»", facing=180)
    c.npc("taverneiro_cometa", "Bóris, do Cometa", "morador", tav[0], tav[1] + 0.5, "aldeao",
          "«Hidromel de cometa: uma caneca e você vê estrelas. Duas, e você vira uma.»", facing=0)
    # 14/11 ("bancadas específicas pelo mapa que liberam itens de acordo com a progressão"): bancada própria da cidade
    c.prop("bancada", -25, -8.5, 0); c.circ("invisivel", -25, -8.5, 1.1, 2)
    c.m["objects"].append({"id": "astralia_bancada", "kind": "bancada", "x": -25, "y": -9.9, "label": "Bancada da Academia"})
    c.livres.append((-25, -8.5, 2.2))
    c.m["spawn_x"], c.m["spawn_y"] = 0, -2.5
    c.m["porta_observatorio"] = porta_obs        # (gerar_merlin.py usa e apaga esta chave)
    return c, [("O", "mata_dos_enforcados", "Mata dos Enforcados", (-33.0, 0.0)), ("L", "canions_suplicantes", "Cânions dos Suplicantes", (33.0, 0.0))]


# =================================================================================================== Brasaforte
def brasaforte():
    c = Cidade("brasaforte", "Brasaforte, a Cidadela-Forja", "neve", H=36)
    c.rua(0, -33, 0, 33, 6)
    c.rua(-14, 6, 14, 6, 4)
    c.predio("portao_fortificado", 0, -27, 0)
    # a Grande Forja no centro (estação de forja de verdade) cercada de braseiros e bigornas
    c.prop("forja", 7, 6, 270)
    c.circ("invisivel", 7, 6, 1.4, 2)
    c.m["objects"].append({"id": "brasaforte_forja", "kind": "forja", "x": 5.2, "y": 6.0, "label": "Grande Forja de Brasaforte"})
    for x, y in ((3, 2.5), (11, 2.5), (3, 9.5), (11, 9.5)):
        c.predio("glb:cenario/acropole_braseiro", x, y, 0)
    for x, y, yaw in ((10.5, 6, 90), (-6, 9, 0)):
        c.predio("glb:cenario/escoria_bigorna", x, y, yaw)
    c.m["waypoints"].append({"id": "wp_brasaforte", "name": "Grande Forja", "x": -5.5, "y": 3.0})
    c.livres.append((-5.5, 3.0, 2.2))
    sal = c.predio("casarao_cidade", -17, 20, 180, ("Salão dos Clãs", "Escudos pendurados por toda parede, um de cada clã. Um lugar vazio: «o clã que foi para o fogo»."))
    tav = c.predio("taverna_grande", 17, 20, 180, ("Taverna do Martelo Frio", "O chão treme com a cantoria. «Aqui a cerveja é quente e o martelo é frio!»"))
    c.predio("sobrado", -20, -12, 90, ("Porta", "Peles de lobo de gelo secando na varanda. «Batem antes de entrar, que aqui é casa de anão.» Alguém abre.", "porta:entra"))
    c.predio("casa_pequena", -21, -21, 90)
    for x, y in ((18, -10), (22, -19)):
        c.predio("glb:cenario/geada_tenda_peles", x, y, 270)
    c.predio("glb:cenario/geada_treno", 13, -15, 30)
    for x, y in ((-10, 15), (10, 15)):
        c.predio("glb:cenario/geada_runa_gelo", x, y, 180)
    c.borda("muralha", [("S", 0), ("N", 0)], passo=8)
    c.espalhar(["tocha_poste", "barril", "caixote", "estandarte", "barril_v2"], 26, 31, solido=0.5)
    c.npc("mestra_bruna", "Bruna Martelo-Frio, a Mestra da Forja", "loja", 0.5, 9.5, "ferreiro",
          "«Armadura de gente grande se forja aqui. Lá fora você arranca dos monstros; aqui você compra pronta — se tiver ouro.»",
          shop="loja_brasaforte_forja", facing=180)
    c.npc("vigia_hrolf", "Velho Hrolf, o Vigia", "missoes", sal[0] + 3, sal[1], "guarda",
          "«Sessenta invernos de vigia. Os lobos de gelo pela frente, o fogo da Escória pelas costas. E agora você. Serve?»")
    c.npc("peleiro_torvi", "Torvi, o Peleiro", "curandeira", 15.5, -6.5, "aldeao",
          "«Tônico forte e casaco de pele. O frio daqui mata mais que draugr.»", shop="loja_curandeira", facing=270)
    c.npc("taverneira_sigrun", "Sigrun, a Taverneira", "missoes", tav[0], tav[1], "aldeao",
          "«Cerveja quente, sopa de raiz e briga só lá fora. Quer trabalho? O Hrolf tem. Quer fofoca? Eu tenho.»")
    c.npc("mineiro_durval", "Durval Barba-de-Brasa, o Mineiro", "morador", -10, -4, "aldeao",
          "«Cavamos até achar o fogo. Achamos. Agora a cidade é quente embaixo e fria em cima.»", facing=90)
    c.npc("guarda_portao_brasa", "Guarda do Portão", "morador", 4, -22, "guarda",
          "«Ao sul, a Coroa da Geada. Ao norte, o Mar de Escória. Brasaforte segura os dois.»", facing=180)
    # 14/11 ("bancadas específicas pelo mapa que liberam itens de acordo com a progressão"): bancada própria da cidade
    c.prop("bancada", -10, 3.5, 0); c.circ("invisivel", -10, 3.5, 1.1, 2)
    c.m["objects"].append({"id": "brasaforte_bancada", "kind": "bancada", "x": -10, "y": 2.1, "label": "Bancada dos Clãs"})
    c.livres.append((-10, 3.5, 2.2))
    c.m["spawn_x"], c.m["spawn_y"] = 0, -16
    return c, [("S", "coroa_da_geada", "Coroa da Geada", (0.0, -33.0)), ("N", "mar_de_escoria", "Mar de Escória", (0.0, 33.0))]


# =================================================================================================== Candeia
def candeia():
    """13/11 22:26: a 3ª cidade (nível 35). Vila das lanternas sobre palafitas, na beira do Brejo, onde a Véspera começa: o sol
    nunca termina de se pôr. Usa as peças que estavam sem uso (ponte de arcos, ponte reparada, árvore seca, rochas)."""
    c = Cidade("candeia", "Candeia, a Vila das Lanternas", "brejo", H=34)
    c.rua(-31, 0, 31, 0, 5)
    c.rua(0, 0, 0, 15, 4)
    # marco: a ponte de arcos velha (aqueduto do tempo dos reis) atravessando o norte da vila
    c.predio("glb:cenario_ponte_arcos", 0, 22, 0)
    c.predio("glb:cenario/cenario_ponte_reparada", -22, -6, 90)
    c.m["waypoints"].append({"id": "wp_candeia", "name": "Lampião da Praça", "x": -5.0, "y": 5.5})
    c.livres.append((-5.0, 5.5, 2.2))
    tav = c.predio("taverna_grande", 16, -18, 0, ("Estalagem da Última Luz", "Cada mesa tem uma lanterna acesa. «Aqui ninguém apaga a luz. Nunca. Nem pra dormir.»"))
    c.predio("sobrado", -16, 15, 180, ("Casa do Lanterneiro", "Centenas de lanternas penduradas no teto, todas acesas. «Entra devagar, que vidro quebra.»", "porta:entra"))
    c.predio("casa_pequena", -26, 14, 180, ("Porta", "Uma vela na janela e um bilhete: «Volto antes de escurecer. (Aqui nunca escurece.)»"))
    c.predio("casa_pequena", 15, 16, 180)
    c.predio("sobrado", 24, 14, 180, ("Capela da Vigília", "Velas por toda parte e um sino sem badalo. «Rezamos pelos que a Véspera levou.»"))
    c.predio("casa_pequena", -15, -15, 0)
    c.predio("casa_pequena", -25, -15, 0)
    for x, y in ((8, -6.5), (-9, -7)):
        c.predio("glb:cenario/costa_barraca_pescador", x, y, 0)
    for x, y in ((-30, 22), (29, 23), (-30, -24), (28, -26), (6, 27), (-7, 28)):
        c.predio("glb:cenario/cenario_arvore_seca", x, y, (x * 7) % 360)
    for x, y in ((21, 5), (-20, 4), (12, 25)):
        c.predio("glb:cenario/cenario_rocha_1", x, y, (y * 11) % 360)
    c.borda("arvore", [("O", 0), ("L", 0)], passo=5.5)
    c.espalhar(["tocha_poste", "poste", "barril", "caixote", "vaso_planta", "estandarte"], 30, 41, solido=0.5)
    c.espalhar(["capim_alto"], 12, 42)
    c.npc("lanterneira_ilda", "Ilda, a Lanterneira", "missoes", -13, 10.5, "aldeao",
          "«Lanterna acesa espanta afogado. Lanterna apagada chama. Por isso aqui ninguém dorme no escuro.»")
    c.npc("vigia_teodoro", "Teodoro, o Vigia da Véspera", "missoes", 25, 2.5, "guarda",
          "«Do lado de lá o sol não se põe nem nasce. Fica parado, vermelho, olhando. E as coisas que andam lá não gostam de luz.»", facing=270)
    c.npc("boticario_anselmo", "Anselmo, o Boticário", "loja", 4, -3.5, "aldeao",
          "«Água benta, antídoto de sapo e panaceia. Quem vai à Véspera leva os três. Quem volta, compra de novo.»", shop="loja_candeia_boticario", facing=0)
    c.npc("curandeira_marta", "Marta, a Rezadeira", "curandeira", -9, -3.5, "curandeira",
          "«Tônico e reza. O tônico fecha a ferida; a reza é pra você não ficar com medo de voltar.»", shop="loja_curandeira", facing=0)
    c.npc("ferreiro_brunilda", "Brunilda, a Ferreira", "ferreiro", 21, -9, "ferreiro",
          "«Prata no fio da lâmina. Vampiro não gosta. Eu também não gosto de vampiro.»", shop="loja_ferreiro", facing=270)
    c.npc("taverneiro_lume", "Lume, da Última Luz", "morador", tav[0], tav[1] + 0.5, "aldeao",
          "«Sopa de raiz do brejo e vinho da Véspera. O vinho é por sua conta — e por seu risco.»", facing=0)
    c.npc("menina_faisca", "Faísca", "morador", 3, 12, "aldeao",
          "«Eu conto lanternas. Ontem eram 412. Hoje 411. Alguém levou uma pra Véspera e não voltou.»", facing=180)
    # 14/11 ("bancadas específicas pelo mapa que liberam itens de acordo com a progressão"): bancada própria da cidade
    c.prop("bancada", -9, -9, 0); c.circ("invisivel", -9, -9, 1.1, 2)
    c.m["objects"].append({"id": "candeia_bancada", "kind": "bancada", "x": -9, "y": -10.4, "label": "Bancada das Lanternas"})
    c.livres.append((-9, -9, 2.2))
    c.m["spawn_x"], c.m["spawn_y"] = 0, 3.5
    return c, [("O", "brejo_da_viuva", "Brejo da Viúva", (-31.0, 0.0)), ("L", "vespera", "Véspera", (31.0, 0.0))]


CIDADES = [vila_das_mos, astralia, candeia, brasaforte]
REGIOES = {
    "vila_das_mos": ("Vila das Mós", 20, 35, "10", "Aldeia de moleiros entre a Costa do Cal e o Vale: o moinho grande, a horta, a feira e a Estalagem do Moinho. "
                     "Mapas do tesouro e o conjunto Rúnico só no mascate daqui."),
    "astralia": ("Astrália", 55, -10, "20", "A cidade branca dos astrônomos, entre a Mata dos Enforcados e os Cânions. No Observatório mora o Merlin, "
                 "o mago das estrelas. A alfaiate vende mantos e elmos que não existem em outro lugar."),
    "candeia": ("Candeia", -20, -12, "35", "A vila das lanternas, em palafitas na beira do Brejo, onde a Véspera começa e o sol nunca termina de se pôr. "
                "O boticário vende água benta, antídoto e panaceia para quem vai enfrentar os mortos."),
    "brasaforte": ("Brasaforte", -41, -49, "50", "Cidadela-forja dos anões, cravada na neve entre a Coroa da Geada e o Mar de Escória: a Grande Forja, "
                   "o Salão dos Clãs e as armaduras de gente grande da Mestra Bruna."),
}
LOJAS = {
    "loja_vila_mapas": ("Quirino, o Mascate de Mapas", ["mapa_tesouro", "consumable_c02_tonico_medio", "arm_runico_capacete", "arm_runico_peitoral",
                                                       "arm_runico_luvas", "arm_runico_calca", "arm_runico_botas"]),
    "loja_astralia_alfaiate": ("Lorena, a Alfaiate das Estrelas", [f"{s}_{p}" for s in ("arm_lendario", "arm_selvagem", "arm_grao_mestre", "arm_quitina")
                                                                    for p in ("capacete", "peitoral", "luvas", "calca", "botas")]),
    "loja_candeia_boticario": ("Anselmo, o Boticário", ["consumable_agua_benta", "consumable_c07_antidoto", "consumable_panaceia",
                                                        "consumable_c05_tonico_forte", "consumable_c02_tonico_medio"]),
    "loja_brasaforte_forja": ("Bruna Martelo-Frio", [f"{s}_{p}" for s in ("arm_dragonico", "arm_musa", "arm_mestre_almas", "arm_rainha_sangue")
                                                     for p in ("capacete", "peitoral", "luvas", "calca", "botas")]),
}


def livre_campo(m, x, y, r):
    if abs(x) > m["half_size"] - 3 or abs(y) > m["half_size"] - 3:
        return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r:
                return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r:
            return False
    for p in m.get("props", []):
        if math.hypot(x - p["x"], y - p["y"]) < 1.2 + r:
            return False
    for n in m.get("npcs", []):
        if math.hypot(x - n["x"], y - n["y"]) < 3 + r:
            return False
    return True


CHEGADAS = {}   # mapa -> pontos onde quem chega por algum portal aparece


def lugar_no_campo(campo, alvo, cid):
    """Portal no campo, perto da borda que olha para a cidade: livre, a 12 m dos bichos e a 8 m de outros portais."""
    x0, y0 = alvo
    for raio in range(0, 50):
        for k in range(0, 360, 10):
            x = round(x0 + raio * math.cos(math.radians(k)), 1)
            y = round(y0 + raio * math.sin(math.radians(k)), 1)
            if not livre_campo(campo, x, y, 2.4):
                continue
            if any(math.hypot(x - s["x"], y - s["y"]) < 12 for s in campo["spawns"]):
                continue
            if any(math.hypot(x - p["x"], y - p["y"]) < 8 for p in campo["portals"] if not p.get("cidade")):
                continue
            # nem em cima de onde quem chega de outro mapa aparece (senão vai e volta)
            if any(math.hypot(x - ax, y - ay) < 7 for ax, ay in CHEGADAS.get(campo["id"], [])):
                continue
            # chegada: 3,5 m para dentro do campo, livre
            dx, dy = -x, -y
            d = math.hypot(dx, dy) or 1
            cx, cy = round(x + dx / d * 3.5, 1), round(y + dy / d * 3.5, 1)
            if livre_campo(campo, cx, cy, 1.0):
                return x, y, cx, cy
    raise SystemExit(f"sem lugar para o portal de {cid} em {campo['id']}")


# onde, em cada campo, procurar o lugar do portal (lado que olha para a cidade)
ALVO_NO_CAMPO = {"coroa_da_geada": (64, 14), "mar_de_escoria": (-64, 14)}


def alvo_no_campo(campo, outro):
    """13/11 22:26: a cidade fica NO CAMINHO entre os dois campos — o portal dela nasce perto da passagem para o outro campo
    (puxado 10 m para dentro), para quem segue a progressão passar por ela."""
    if campo["id"] in ALVO_NO_CAMPO:
        return ALVO_NO_CAMPO[campo["id"]]
    p = next((p for p in campo["portals"] if p.get("to_map") == outro and not p.get("cidade")), None)
    if p is None:
        return (0, 0)
    d = math.hypot(p["x"], p["y"]) or 1
    return (p["x"] - p["x"] / d * 10, p["y"] - p["y"] / d * 10)


def arquivo_do_mapa(mid):
    for f in os.listdir(C):
        if f.startswith("map_") and f.endswith(".json"):
            try:
                if json.load(open(os.path.join(C, f), encoding="utf-8"))["id"] == mid:
                    return f
            except Exception:
                pass
    raise SystemExit("mapa não achado: " + mid)


def missoes():
    q = []

    def M(id_, giver, nome, kind, nivel, desc, hint, xp, moedas, **kw):
        d = {"id": "q_cid_" + id_, "name": nome, "giver": giver, "kind": kind, "targets": kw.pop("targets", []), "count": kw.pop("count", 1),
             "description": desc, "hint": hint, "min_level": nivel, "reward_xp": xp, "reward_coins": moedas, "category": "regiao"}
        d.update(kw)
        q.append(d)

    # Vila das Mós (cidade do nível 10: a primeira ainda na Costa, as outras no Vale e na Mata)
    M("mos_ratos", "estalajadeiro_otaviano", "Ratos no Celeiro", "kill", 9,
      "«Os ratos gigantes da Costa sobem a estrada e roem a farinha toda. Derruba 6 deles lá embaixo, antes que aprendam o caminho do celeiro.»",
      "Derrube 6 Ratos Gigantes na Costa do Cal (a oeste da vila).", 4500, 90, targets=["monster_bst_rato_gigante"], count=6, map="costa_do_cal",
      reward_items=["consumable_c01_tonico_fraco*4"])
    M("mos_sacis", "moleira_benta", "Farinha Roubada", "kill", 11,
      "«Toda noite some um saco de farinha e aparece um gorro vermelho no lugar. Sacis! Lá no Vale tem um bando. Derruba 6 que eles aprendem.»",
      "Derrube 6 Sacis das Cinzas no Vale das Cinzas (a leste da vila).", 9000, 160, targets=["monster_bst_saci"], count=6, map="vale_das_cinzas",
      reward_items=["consumable_c02_tonico_medio*3"])
    M("mos_espantalhos", "moleira_benta", "Espantalhos Acordados", "kill", 12,
      "«Os espantalhos do Vale levantaram e saíram andando. Os meus ainda não. Derruba 5 dos de lá antes que eles venham buscar os primos.»",
      "Derrube 5 Espantalhos no Vale das Cinzas.", 9500, 170, targets=["monster_vale_espantalho"], count=5, map="vale_das_cinzas",
      requires="q_cid_mos_sacis", reward_items=["consumable_c02_tonico_medio*3"])
    M("mos_farinha", "moleira_benta", "Farinha para o Porto", "talk", 13,
      "«A Gilda da taverna do Porto encomendou farinha pro pão de festa. Avisa ela que a carroça sai amanhã — e que o preço subiu.»",
      "Fale com Gilda, a taverneira, no Porto das Cinzas.", 7000, 220, targets=["taverneira_gilda"], requires="q_cid_mos_espantalhos")
    M("mos_lobos", "estalajadeiro_otaviano", "Lobos no Trigal", "kill", 16,
      "«Os viajantes da Mata chegam aqui mordidos. Lobo cinzento, eles dizem. Limpa a beira da Mata pra mim: 8 lobos.»",
      "Derrube 8 Lobos Cinzentos na Mata dos Enforcados (depois do Vale).", 16000, 260, targets=["monster_mata_lobo_cinzento"], count=8,
      map="mata_dos_enforcados", reward_items=["consumable_c02_tonico_medio*4"])
    M("mos_javali", "estalajadeiro_otaviano", "O Javali Ancião", "hunt", 19,
      "«Tem um javali velho, do tamanho de uma carroça, que derruba as cercas da estrada da Mata. Quem trouxer a presa dele come de graça um mês.»",
      "Mata dos Enforcados: siga o rastro de cercas quebradas até o Javali Ancião e derrube-o.", 26000, 400, targets=["monster_mata_javali_anciao"],
      map="mata_dos_enforcados", x=-50, y=24, radius=16, requires="q_cid_mos_lobos", reward_items=["consumable_c05_tonico_forte*2"])
    # Astrália
    M("ast_morcegos", "astronoma_dalia", "Morcegos na Luneta", "kill", 21,
      "«Os morcegos dos Cânions fazem ninho nas nossas lunetas de campo. Toda noite um astrônomo leva um susto. Espanta 10 deles, por favor.»",
      "Derrube 10 Morcegos de Caverna nos Cânions dos Suplicantes (a leste de Astrália).", 30000, 450, targets=["monster_canions_morcego"], count=10,
      map="canions_suplicantes", reward_items=["consumable_c02_tonico_medio*4"])
    M("ast_lentes", "astronoma_dalia", "Quitina para as Lentes", "collect", 22,
      "«Lente boa se pole com pó de quitina de escorpião — os antigos sabiam. Traga 8 Quitinas e eu te deixo olhar pela luneta grande.»",
      "Escorpiões dos Cânions derrubam Quitina. Traga 8 para a Dália, em Astrália.", 32000, 480, targets=["mat_quitina"], count=8,
      requires="q_cid_ast_morcegos", reward_items=["consumable_c02_tonico_medio*4"])
    M("ast_harpias", "escriba_tobias", "As Harpias Roubam Mapas", "kill", 28,
      "«As harpias dos Cânions roubaram três rolos do Atlas Antigo — os que mostram os reinos das bordas. Derrube 5 e veja se algum rolo cai.»",
      "Derrube 5 Harpias dos Cânions.", 52000, 700, targets=["monster_harpia"], count=5, map="canions_suplicantes",
      reward_items=["consumable_c05_tonico_forte*3"])
    M("ast_visitante", "astronoma_dalia", "O Outro Visitante", "boss", 31,
      "«Uma coisa de olhos grandes caiu nos Cânions faz uma semana. O Merlin diz que é parente do que caiu no poço dele. Eu digo que é perigoso.»",
      "Derrube o Visitante do Céu nos Cânions dos Suplicantes.", 70000, 900, targets=["monster_bst_visitante"], map="canions_suplicantes",
      requires="q_cid_ast_lentes", reward_items=["consumable_c05_tonico_forte*4"])
    # Candeia (cidade do nível 35: Brejo de um lado, Véspera do outro)
    M("cand_tecelas", "lanterneira_ilda", "Teias nas Lanternas", "kill", 34,
      "«As tecelãs do Brejo enrolam as lanternas da estrada em teia, e lanterna enrolada não espanta afogado. Derruba 6 delas.»",
      "Derrube 6 Tecelãs no Brejo da Viúva (a oeste de Candeia).", 95000, 1100, targets=["monster_brejo_tecela"], count=6, map="brejo_da_viuva",
      reward_items=["consumable_c05_tonico_forte*3"])
    M("cand_afogados", "lanterneira_ilda", "Os que Saem da Água", "kill", 36,
      "«Na Véspera os afogados saem da água atrás da luz. Já levaram três lanternas e um lanterneiro. Derruba 8 antes que cheguem na ponte.»",
      "Derrube 8 Afogados na Véspera (a leste de Candeia).", 110000, 1250, targets=["monster_vespera_afogado"], count=8, map="vespera",
      requires="q_cid_cand_tecelas", reward_items=["consumable_agua_benta*2", "consumable_c05_tonico_forte*3"])
    M("cand_aviso", "lanterneira_ilda", "Recado para o Vigia", "talk", 36,
      "«O Teodoro, lá na saída da Véspera, precisa saber que a lanterna 411 sumiu junto com quem levou. Avisa ele.»",
      "Fale com Teodoro, o Vigia da Véspera, na saída leste de Candeia.", 60000, 600, targets=["vigia_teodoro"], requires="q_cid_cand_afogados")
    M("cand_sineiros", "vigia_teodoro", "O Sino que Chama", "kill", 39,
      "«Tem sineiros na Véspera tocando sino pros mortos levantarem. Toda vez que toca, um morto a mais. Cala 4 sinos — e os sineiros junto.»",
      "Derrube 4 Sineiros na Véspera.", 150000, 1600, targets=["monster_vespera_sineiro"], count=4, map="vespera",
      requires="q_cid_cand_aviso", reward_items=["consumable_c05_tonico_forte*4"])
    M("cand_viuva", "vigia_teodoro", "A Viúva Negra do Brejo", "boss", 41,
      "«Quem levou a lanterna 411 foi parar na teia da Viúva Negra, no fundo do Brejo. Traz a lanterna de volta. E, se puder, a Viúva não volta.»",
      "Derrube a Viúva Negra no Brejo da Viúva.", 200000, 2200, targets=["monster_brejo_viuva_negra"], map="brejo_da_viuva",
      requires="q_cid_cand_sineiros", reward_items=["consumable_c05_tonico_forte*5", "consumable_panaceia*2"])
    # Brasaforte
    M("bf_lobos", "vigia_hrolf", "Lobos no Passo", "kill", 43,
      "«Os lobos de gelo cercam a estrada do sul. Caravana não passa, cerveja não chega. Derrube 8 — e a primeira caneca é minha.»",
      "Derrube 8 Lobos de Gelo na Coroa da Geada (ao sul).", 140000, 1500, targets=["monster_geada_lobo_gelo"], count=8, map="coroa_da_geada",
      reward_items=["consumable_c05_tonico_forte*4"])
    M("bf_ferro", "vigia_hrolf", "Osso Antigo para o Portão", "collect", 46,
      "«A Bruna tempera o ferro do portão com pó de osso antigo — é segredo de clã, não espalha. Os draugr da Geada são feitos disso. Traga 10.»",
      "Draugr da Coroa da Geada derrubam Osso Antigo. Traga 10 ao Hrolf, em Brasaforte.", 170000, 1800, targets=["mat_osso_antigo"], count=10,
      requires="q_cid_bf_lobos", reward_items=["consumable_c05_tonico_forte*5"])
    M("bf_rei_draugr", "vigia_hrolf", "O Rei de Baixo do Gelo", "boss", 50,
      "«Tem um rei morto embaixo do gelo, juntando os draugr. Enquanto ele mandar, o passo do sul não fica limpo. Acorda ele de vez.»",
      "Derrube o Rei Draugr na Coroa da Geada.", 230000, 2400, targets=["monster_geada_rei_draugr"], map="coroa_da_geada",
      requires="q_cid_bf_ferro", reward_items=["consumable_c05_tonico_forte*6"])
    M("bf_basalto", "taverneira_sigrun", "Basalto para a Lareira", "collect", 52,
      "«A lareira da taverna é de basalto da Escória — e rachou. Traga 12 pedras e eu te sirvo a sopa de raiz que nem o Hrolf prova.»",
      "Elementais e Golens do Mar de Escória (ao norte) derrubam Basalto. Traga 12 à Sigrun.", 250000, 2600, targets=["mat_basalto"], count=12,
      reward_items=["consumable_c05_tonico_forte*6"])
    M("bf_colosso", "taverneira_sigrun", "O Colosso que Engoliu a Bigorna", "boss", 56,
      "«Meu avô jogou a melhor bigorna do clã no Mar de Escória pra fazer um juramento. Um colosso de escória engoliu. Traz de volta a honra — e a bigorna, se der.»",
      "Derrube o Colosso de Escória no Mar de Escória.", 320000, 3200, targets=["monster_escoria_colosso_escoria"], map="mar_de_escoria",
      requires="q_cid_bf_basalto", reward_items=["consumable_c05_tonico_forte*8"])
    return q


def main():
    world = L("world.json")
    shops = L("shops.json")
    for cid, (nome, itens) in LOJAS.items():
        shops = [s for s in shops if s["id"] != cid]
        shops.append({"id": cid, "name": nome, "items": itens})
    D("shops.json", shops)
    # tira os portais de cidade antigos de TODOS os campos (as cidades mudaram de lugar)
    ids_cidades = {"vila_das_mos", "astralia", "candeia", "brasaforte"}
    for f in os.listdir(C):
        if f.startswith("map_") and f.endswith(".json"):
            m0 = L(f)
            antes = len(m0.get("portals", []))
            m0["portals"] = [p for p in m0.get("portals", []) if not (p.get("cidade") and p.get("to_map") in ids_cidades and not p.get("via_cidade"))]
            if len(m0["portals"]) != antes:
                D(f, m0)
    world["links"] = [l for l in world["links"] if l["from"] not in ids_cidades and l["to"] not in ids_cidades]
    # sem atalho direto entre os dois campos de cada cidade (a estrada passa por ela)
    pulos = {frozenset(par) for par in (("costa", "vale"), ("mata", "canions"), ("brejo", "vespera"), ("geada", "escoria"))}
    world["links"] = [l for l in world["links"] if frozenset((l["from"], l["to"])) not in pulos]
    for f in os.listdir(C):
        if f.startswith("map_") and f.endswith(".json"):
            for p in L(f).get("portals", []):
                if p.get("to_map") and p.get("to_map") not in ids_cidades:
                    CHEGADAS.setdefault(p["to_map"], []).append((p.get("to_x", 0), p.get("to_y", 0)))
    for fazer in CIDADES:
        cidade, saidas = fazer()
        m = cidade.m
        for lado, campo_id, rotulo, (px, py) in saidas:
            f = arquivo_do_mapa(campo_id)
            campo = L(f)
            campo["portals"] = [p for p in campo["portals"] if p.get("id") != f"cidade_{m['id']}"]
            outro = next(s[1] for s in saidas if s[1] != campo_id)
            # 14/11 (dono: "tem que fazer o link correto dos TPs, de acordo com a história e a progressão"): a cidade fica NA
            # estrada — a saída do campo que levava direto ao próximo campo agora leva à cidade (e a cidade leva ao próximo)
            estrada = next((p for p in campo["portals"] if p.get("via_cidade") == m["id"] or (p.get("to_map") == outro and not p.get("cidade"))), None)
            if estrada is not None:
                dx0, dy0 = -estrada["x"], -estrada["y"]
                d0 = math.hypot(dx0, dy0) or 1
                cx, cy = round(estrada["x"] + dx0 / d0 * 3.5, 1), round(estrada["y"] + dy0 / d0 * 3.5, 1)
                dx, dy = -px, -py
                d = math.hypot(dx, dy) or 1
                ax, ay = round(px + dx / d * 4.5, 1), round(py + dy / d * 4.5, 1)
                estrada.update({"to_map": m["id"], "to_x": ax, "to_y": ay, "label": m["display_name"].split(",")[0], "via_cidade": m["id"]})
                D(f, campo)
                m["portals"].append({"id": f"{m['id']}_para_{campo_id}", "x": px, "y": py, "radius": 2.2, "to_map": campo_id, "to_x": cx, "to_y": cy,
                                     "label": rotulo})
                continue
            x, y, cx, cy = lugar_no_campo(campo, alvo_no_campo(campo, outro), m["id"])
            # chegada na cidade: 4 m para dentro do portão
            dx, dy = -px, -py
            d = math.hypot(dx, dy) or 1
            ax, ay = round(px + dx / d * 4.5, 1), round(py + dy / d * 4.5, 1)
            campo["portals"].append({"id": f"cidade_{m['id']}", "x": x, "y": y, "radius": 2.0, "to_map": m["id"], "to_x": ax, "to_y": ay,
                                     "label": m["display_name"].split(",")[0], "cidade": True})
            D(f, campo)
            m["portals"].append({"id": f"{m['id']}_para_{campo_id}", "x": px, "y": py, "radius": 2.2, "to_map": campo_id, "to_x": cx, "to_y": cy,
                                 "label": rotulo})
        if "porta_observatorio" in m:
            json.dump(m.pop("porta_observatorio"), open(os.path.join(C, ".porta_observatorio.json"), "w"))
        D(f"map_{m['id']}.json", m)
        if f"map_{m['id']}.json" not in world["map_files"]:
            world["map_files"].append(f"map_{m['id']}.json")
        nome, wx, wy, niveis, desc = REGIOES[m["id"]]
        world["regions"] = [r for r in world["regions"] if r["id"] != m["id"]] + [
            {"id": m["id"], "name": nome, "kind": "cidade", "x": wx, "y": wy, "levels": niveis, "maps": [m["id"]], "description": desc}]
        print("cidade", m["id"], "npcs", len(m["npcs"]), "prédios/peças", len(m["props"]), "obstáculos", len(m["obstacles"]))
    for a, b in (("costa", "vila_das_mos"), ("vila_das_mos", "vale"), ("mata", "astralia"), ("astralia", "canions"),
                 ("brejo", "candeia"), ("candeia", "vespera"), ("geada", "brasaforte"), ("brasaforte", "escoria")):
        if not any(l["from"] == a and l["to"] == b for l in world["links"]):
            world["links"].append({"from": a, "to": b})
    D("world.json", world)
    D("quests_cidades.json", missoes())


if __name__ == "__main__":
    main()

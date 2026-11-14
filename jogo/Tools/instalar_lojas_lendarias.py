"""Instala as LOJAS DE TESTE dos itens lendários (15 conjuntos, 50 armas, asas e capas) SEM substituir nada.

Feito para o programador usar o trabalho já pronto sem bugar o que ele já fez:
  - NÃO sobrescreve mapa nenhum: só ACRESCENTA 7 NPCs novos no fim da lista "npcs" do mapa escolhido;
  - acha sozinho um lugar VAZIO no mapa (longe de NPCs, portais, objetos, spawn e paredes que já existem);
  - lojas num arquivo próprio (Content/shops_lendarios.json), que o jogo já lê junto com o shops.json;
  - antes de gravar, confere conflito de id (item, loja, NPC) e se as skills das armas existem. Se achar, PARA e avisa.

Uso (rodar da raiz do projeto):
  python Tools/instalar_lojas_lendarias.py                         -> só MOSTRA o plano (não grava nada)
  python Tools/instalar_lojas_lendarias.py --aplicar               -> grava (faz backup do mapa em backups/)
  python Tools/instalar_lojas_lendarias.py --mapa map_X.json       -> usar outro mapa (padrão: map_santuario_ceu.json)
  python Tools/instalar_lojas_lendarias.py --perto X Y             -> preferir lugares perto desse ponto
Repetível: ao rodar de novo, tira os NPCs "sant_lend_*" e reescreve shops_lendarios.json; o resto fica igual."""
import argparse, glob, json, math, os, shutil, sys, datetime

RAIZ = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
C = os.path.join(RAIZ, "Content")
PREFIXO_NPC, PREFIXO_LOJA = "sant_lend_", "santuario_lend_"
DIST_MIN_OCUPADO = 3.5   # distância mínima de qualquer coisa que já existe no mapa
DIST_ENTRE_NOVOS = 3.0   # distância entre os NPCs novos
MARGEM_BORDA = 2.5
MARGEM_PAREDE = 1.5

def ler(p): return json.load(open(p, encoding="utf-8"))
def lista(d, chave): return d if isinstance(d, list) else d.get(chave, [])

def todos(padrao, chave):
    out = []
    for f in sorted(glob.glob(os.path.join(C, padrao))):
        for x in lista(ler(f), chave): out.append((os.path.basename(f), x))
    return out

NPCS = [  # id, nome, visual, loja, fala
    ("sets_guerreiro", "Arauto dos Conjuntos — Guerreiro", "capita", "sets_guerreiro",
     "Pedra Viva, Cavaleiro Sagrado, Dragão de Fogo, Demoníaco. Vista e vá à Arena."),
    ("sets_mago", "Arauto dos Conjuntos — Mago", "arcanista", "sets_mago",
     "Rúnico, Abismo, Tempo e Vazio: auréolas, buraco negro e relógio."),
    ("sets_arqueiro", "Arauto dos Conjuntos — Arqueiro", "capita", "sets_arqueiro",
     "Abissal, Celestial e Anjo Caído — as asas abrem quando você corre."),
    ("sets_bruxa", "Arauto dos Conjuntos — Bruxa", "curandeira", "sets_bruxa",
     "Floresta, Gelo Eterno, Necromante e Faraônico."),
    ("armas_guerreiro", "Mestre das Armas Lendárias", "ferreiro", "armas_guerreiro",
     "Espadas, machados gigantes, foices, garfos, espadas duplas, lanças e manoplas — os cinco elementos."),
    ("armas_magia", "Mestra dos Cajados e Arcos", "arcanista", "armas_magia",
     "Cajados, focos naturais e arcos lendários. Teste o efeito na Arena."),
    ("asas", "Serafim das Mil Asas", "arcanista", "asas",
     "Todas as asas e capas à venda, para comparar lado a lado."),
]

def montar_lojas(itens):
    lend = [i for i in itens if i["id"].startswith(("arm_", "arma_lend_")) and i.get("quality") == "lendario"]
    ordem = {"capacete": 0, "peitoral": 1, "luvas": 2, "calca": 3, "botas": 4}
    def sets_da(c):
        xs = [i for i in lend if i.get("set") and i["allowed_classes"] == [c]]
        return [i["id"] for i in sorted(xs, key=lambda i: (i["min_level"], i["set"], ordem.get(i["subtype"], 9)))]
    def armas(cs):
        xs = [i for i in lend if i["category"] == "weapon" and i["allowed_classes"][0] in cs]
        return [i["id"] for i in sorted(xs, key=lambda i: (i["id"].split("_")[2], i["min_level"]))]
    com_preco = lambda p: sorted(i["id"] for i in itens if i["id"].startswith(p) and i.get("price", 0) > 0)
    sem_preco = sorted(i["id"] for i in itens if i["id"].startswith(("wings_", "capa_")) and not i.get("price", 0) > 0)
    lojas = [
        ("sets_guerreiro", "Conjuntos Lendários — Guerreiro", sets_da("guerreiro")),
        ("sets_mago", "Conjuntos Lendários — Mago", sets_da("mago")),
        ("sets_arqueiro", "Conjuntos Lendários — Arqueiro", sets_da("arqueiro")),
        ("sets_bruxa", "Conjuntos Lendários — Bruxa", sets_da("bruxa")),
        ("armas_guerreiro", "Armas Lendárias — Guerreiro", armas({"guerreiro"})),
        ("armas_magia", "Armas Lendárias — Cajados, Focos e Arcos", armas({"mago", "bruxa", "arqueiro"})),
        ("asas", "Todas as Asas e Capas", com_preco("wings_") + com_preco("capa_")),
    ]
    return [{"id": PREFIXO_LOJA + k, "name": n, "items": xs} for k, n, xs in lojas], sem_preco

def ocupado(m, ignorar_ids):
    pts, rets = [], []
    for k, v in m.items():
        if not isinstance(v, list): continue
        for e in v:
            if not isinstance(e, dict) or "x" not in e or "y" not in e: continue
            if e.get("id", "") in ignorar_ids: continue
            if "w" in e and "h" in e:
                rets.append((e["x"], e["y"], e["w"] / 2 + MARGEM_PAREDE, e["h"] / 2 + MARGEM_PAREDE, math.radians(e.get("r", 0))))
            else:
                folga_extra = 6.0 if "to_map" in e else 2.0  # portal: deixa livre a área de chegada/saída
                pts.append((e["x"], e["y"], max(DIST_MIN_OCUPADO, e.get("radius", 0) + folga_extra)))
    pts.append((m.get("spawn_x", 0), m.get("spawn_y", 0), DIST_MIN_OCUPADO + 1))
    return pts, rets

def dentro_ret(x, y, r):
    cx, cy, hw, hh, a = r
    dx, dy = x - cx, y - cy
    lx, ly = dx * math.cos(-a) - dy * math.sin(-a), dx * math.sin(-a) + dy * math.cos(-a)
    return abs(lx) <= hw and abs(ly) <= hh

def achar_lugares(m, n, perto, ignorar_ids):
    hs = float(m.get("half_size", 20)) - MARGEM_BORDA
    pts, rets = ocupado(m, ignorar_ids)
    cand = []
    passo = 0.5
    k = int(hs / passo)
    for i in range(-k, k + 1):
        for j in range(-k, k + 1):
            x, y = i * passo, j * passo
            if any(dentro_ret(x, y, r) for r in rets): continue
            folga = min((math.hypot(x - px, y - py) - rr for px, py, rr in pts), default=99)
            if folga < 0: continue
            cand.append((x, y, folga))
    if perto is None:
        # sem preferência: o maior bolsão vazio (ponto com mais folga) e os vizinhos dele
        bx, by, _ = max(cand, key=lambda c: c[2]) if cand else (0, 0, 0)
    else:
        bx, by = perto
    # primeiro tenta uma fileira encostada na borda (de frente para a praça); se não couber, usa qualquer lugar vazio
    for faixa in (1.0, 3.0, 99):
        escolhidos = []
        lista_c = [c for c in cand if max(abs(c[0]), abs(c[1])) >= hs - faixa]
        for x, y, _ in sorted(lista_c, key=lambda c: math.hypot(c[0] - bx, c[1] - by)):
            if all(math.hypot(x - ex, y - ey) >= DIST_ENTRE_NOVOS for ex, ey in escolhidos):
                escolhidos.append((x, y))
                if len(escolhidos) == n: return escolhidos
    return escolhidos

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mapa", default="map_santuario_ceu.json")
    ap.add_argument("--aplicar", action="store_true")
    ap.add_argument("--perto", nargs=2, type=float)
    a = ap.parse_args()
    erros = []

    if not os.path.exists(os.path.join(C, "items_lendarios.json")):
        sys.exit("Falta Content/items_lendarios.json (vem no pacote).")
    itens_f = todos("items*.json", "items")
    vistos = {}
    for f, i in itens_f:
        if i["id"] in vistos and vistos[i["id"]] != f:
            erros.append("item com id repetido: %s (em %s e %s)" % (i["id"], vistos[i["id"]], f))
        vistos.setdefault(i["id"], f)
    itens = [i for _, i in itens_f]
    skills = {s["id"] for _, s in todos("skills*.json", "skills")}
    for i in itens:
        if i["id"].startswith("arma_lend_") and i.get("proc_skill") and i["proc_skill"] not in skills:
            erros.append("a arma %s usa a skill %s, que não existe em skills*.json" % (i["id"], i["proc_skill"]))

    lojas, sem_preco = montar_lojas(itens)
    for f, s in todos("shops*.json", "shops"):
        if f != "shops_lendarios.json" and s["id"].startswith(PREFIXO_LOJA):
            erros.append("já existe loja %s em %s" % (s["id"], f))

    caminho_mapa = os.path.join(C, a.mapa)
    if not os.path.exists(caminho_mapa): sys.exit("Mapa não encontrado: " + a.mapa)
    m = ler(caminho_mapa)
    antigos = {n["id"] for n in m.get("npcs", []) if n["id"].startswith(PREFIXO_NPC)}
    lugares = achar_lugares(m, len(NPCS), a.perto, antigos)
    if len(lugares) < len(NPCS):
        erros.append("só achei %d lugares vazios no mapa (precisa de %d) — use --perto X Y ou aumente o mapa" % (len(lugares), len(NPCS)))

    novos = []
    for (k, nome, look, loja, fala), (x, y) in zip(NPCS, lugares):
        face = round(math.degrees(math.atan2(0 - x, 0 - y))) % 360  # de frente para o centro do mapa
        novos.append({"id": PREFIXO_NPC + k, "name": nome, "role": "mercador", "x": x, "y": y, "facing": face,
                      "greeting": fala, "look": look, "shop": PREFIXO_LOJA + loja})

    print("Mapa:", a.mapa, "| meio-tamanho", m.get("half_size"), "| NPCs que já estão lá:", len(m.get("npcs", [])) - len(antigos))
    for l in lojas: print("  loja %-32s %3d itens" % (l["id"], len(l["items"])))
    for n in novos: print("  NPC  %-28s em (%5.1f, %5.1f) virado %3d°" % (n["id"], n["x"], n["y"], n["facing"]))
    if sem_preco: print("Fora da loja (sem preço, só recompensa):", ", ".join(sem_preco))
    if erros:
        print("\nPAREI — conflitos:"); [print("  -", e) for e in erros]; sys.exit(1)
    if not a.aplicar:
        print("\nNada foi gravado. Se o plano estiver bom: python Tools/instalar_lojas_lendarias.py --aplicar"); return

    bk = os.path.join(RAIZ, "backups", "lojas_lendarias_" + datetime.datetime.now().strftime("%Y-%m-%d_%H%M"))
    os.makedirs(bk, exist_ok=True); shutil.copy2(caminho_mapa, bk)
    m["npcs"] = [n for n in m.get("npcs", []) if not n["id"].startswith(PREFIXO_NPC)] + novos
    json.dump(m, open(caminho_mapa, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
    json.dump(lojas, open(os.path.join(C, "shops_lendarios.json"), "w", encoding="utf-8"), indent=2, ensure_ascii=False)
    print("\nGravado. Backup do mapa em", os.path.relpath(bk, RAIZ), "\nAgora rode o validador de conteúdo do projeto.")

if __name__ == "__main__":
    main()

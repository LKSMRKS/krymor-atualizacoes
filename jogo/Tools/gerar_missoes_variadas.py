#!/usr/bin/env python3
"""
13/11 (análise do jogo — Missões, Leva 1 aprovada): "gostei das ideias de missões; além disso, vamos dar uma equilibrada
no número de tipos de missões". Rodar por último (idempotente). Escreve Content/quests_variadas.json e põe as pistas nos mapas.
Por região do 17 ao 60:
  • 1 escolta OU defesa (alternando; usa o sistema da "Segure a Barraca" / "Carga do Mineiro");
  • 1 investigação: 3 pistas (F em cada) → caçada do culpado (presa alfa) no lugar da última pista;
  • 1 missão com condição (noite / zona / sem morrer) — o abate só conta assim (GameServer.Condicoes);
Nos mapas menores (ramificações): 1 missão com condição ou 1 caçada.
Escolhas com consequência (4 cadeias): aceitar um lado some com o outro (accept_flags + forbid_flag); a continuação muda.
Recompensas variadas: decoração da casa, títulos (marca "titulo:q_..."), receitas.
Também reescreve as descrições curtas demais das missões antigas (só a descrição; IDs não mudam).
"""
import json, os, glob, math

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = os.path.join(RAIZ, "Content")
def L(n): return json.load(open(os.path.join(C, n), encoding="utf-8"))
def D(n, o): json.dump(o, open(os.path.join(C, n), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
ARQ = {json.load(open(f, encoding="utf-8"))["id"]: os.path.basename(f) for f in glob.glob(os.path.join(C, "map_*.json"))}
mons = {}
for f in glob.glob(os.path.join(C, "monsters*.json")):
    for m in json.load(open(f, encoding="utf-8")): mons[m["id"]] = m

def livre(m, x, y, r=1.5):
    if abs(x) > m["half_size"] - 4 or abs(y) > m["half_size"] - 4: return False
    for o in m["obstacles"]:
        if o.get("shape") == "circle":
            if math.hypot(x - o["x"], y - o["y"]) < o["r"] + r: return False
        elif abs(x - o["x"]) < o["w"] / 2 + r and abs(y - o["y"]) < o["h"] / 2 + r: return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 1.6 + r for p in m["props"]): return False
    if any(math.hypot(x - p["x"], y - p["y"]) < 5 for p in m["portals"]): return False
    if any(math.hypot(x - n["x"], y - n["y"]) < 3 for n in m["npcs"]): return False
    if any(math.hypot(x - o["x"], y - o["y"]) < 3 for o in m["objects"]): return False
    return True

def perto(m, tx, ty, r=1.5):
    s = m["half_size"] / 75.0
    tx, ty = tx * s, ty * s
    for raio in range(0, 40):
        for k in range(0, 360, 15):
            x, y = round(tx + raio * math.cos(math.radians(k)), 1), round(ty + raio * math.sin(math.radians(k)), 1)
            if livre(m, x, y, r): return x, y
    raise SystemExit(f"sem lugar em {m['id']} perto de {tx},{ty}")

def comuns(m):
    c = {}
    for s in m["spawns"]:
        d = mons.get(s["monster"])
        if not d or d.get("boss") or d.get("elite") or s["id"].startswith(("portoes_", "chefe_", "costa_caranguejo")): continue
        c.setdefault(s["monster"], []).append(s)
    return sorted(c.items(), key=lambda kv: (-len(kv[1]), kv[0]))

xp = lambda l: int(l * l * 70)
Q = []
def add(**k):
    k.setdefault("count", 1); k.setdefault("category", "regiao")
    k["reward_xp"] = k.get("reward_xp") or xp(k["min_level"]); k.setdefault("reward_coins", k["min_level"] * 30)
    Q.append(k)

# ------------------------------------------------------------------ regiões grandes
REG = [
  dict(mapa="mata_dos_enforcados", lv=19, ev="escort", ev_npc="cozinheiro_ivo", ev_nome="A Carroça do Moinho",
       ev_txt="«A farinha do moinho precisa chegar à roça do leste antes que os saqueadores sintam o cheiro. Vá junto da carroça — ela só anda com você perto e o caminho limpo.»",
       inv_npc="cozinheiro_ivo", inv_lv=21, inv_nome="Quem Roubou o Moinho?", pistas=["Pegada enlameada", "Saco de farinha rasgado", "Pena preta presa num galho"],
       inv_txt="«Sumiram três sacos do moinho esta noite. Não foi bicho comum — bicho não desamarra nó. Siga as pistas: a pegada, o saco rasgado, a pena.»",
       culpado_txt="«Pena preta, pegada funda, nó desfeito… é o maior dos saqueadores, o que comanda o bando. Ele está escondido onde você achou a pena. Traga a farinha de volta — e o respeito do moinho.»",
       cond="noite", cond_npc="mentora_selene", cond_lv=23, cond_nome="Olhos na Escuridão", cond_n=10,
       cond_txt="«À noite as aranhas da toca descem das árvores. De dia elas fogem; à noite elas caçam — e é aí que se caça elas. Dez, só depois das 19h.»"),
  dict(mapa="canions_suplicantes", lv=26, ev="defend", ev_npc="mineiro_bras2", ev_nome="A Boca da Mina",
       ev_txt="«Vamos reabrir a boca velha da mina. O barulho vai chamar tudo que mora no cânion. Fique na entrada e segure quatro investidas enquanto a gente escora as vigas.»",
       inv_npc="tratador_dimas", inv_lv=29, inv_nome="O Desabamento Não Foi Acidente", pistas=["Pavio queimado", "Picareta com o nome raspado", "Bilhete rasgado"],
       inv_txt="«Todo mundo diz que a mina caiu sozinha. Eu cuido dos bichos e sei o que é bicho: aquilo foi pólvora. Ache o pavio, a picareta e o bilhete que o sabotador deixou.»",
       culpado_txt="«O bilhete fala de um troll que os mineiros pagavam para vigiar a entrada — e que cansou de esperar pelo pagamento. Ele ainda ronda onde você achou o bilhete.»",
       cond="sem_morrer", cond_npc="alma_ligia", cond_lv=31, cond_nome="Sem Cair no Cânion", cond_n=12, cond_titulo="q_intocavel",
       cond_txt="«Eu caí aqui e nunca mais levantei. Prove que você é diferente: doze espectros sem cair nenhuma vez. Caiu, começa do zero.»"),
  dict(mapa="brejo_da_viuva", lv=33, ev="escort", ev_npc="agricultor_caio", ev_nome="O Barco de Mudas",
       ev_txt="«As mudas de folha amarga precisam chegar aos canteiros do sul pela trilha das tábuas. Vá junto do carrinho e não deixe as sanguessugas encostarem nele.»",
       inv_npc="curandeira_odete", inv_lv=36, inv_nome="O Veneno da Fonte", pistas=["Frasco vazio boiando", "Raiz roxa arrancada", "Pegadas de bota fina"],
       inv_txt="«A fonte da vila está amarga e não foi a folha. Alguém envenenou. Procure o frasco, a raiz e as pegadas — quem anda de bota fina num brejo?»",
       culpado_txt="«Bota fina, raiz roxa… é uma tecelã que aprendeu a fazer veneno e quer o brejo só para ela. Ela tece a toca onde você achou as pegadas.»",
       cond="zona", cond_npc="tratadora_nina", cond_lv=38, cond_nome="A Trilha das Tábuas", cond_n=10,
       cond_txt="«A trilha das tábuas, a oeste, é por onde as crianças passam. Limpe dez bichos ALI — não adianta caçar longe, preciso da trilha segura.»"),
  dict(mapa="vespera", lv=41, ev="defend", ev_npc="cozinheiro_espectral", ev_nome="O Último Velório",
       ev_txt="«Hoje velamos o último vivo de Véspera. Os necromantes querem o corpo. Fique junto do caixão e segure quatro investidas até o sino tocar.»",
       inv_npc="guia_aurelio", inv_lv=43, inv_nome="O Assassino de Véspera", pistas=["Luva de couro rasgada", "Vela apagada ao contrário", "Nome riscado no livro de óbitos"],
       inv_txt="«Em Véspera todo mundo já morreu — mas alguém está morrendo DE NOVO. Luva, vela, livro de óbitos. Um detetive acharia o padrão.»",
       culpado_txt="«O nome riscado é de um soldado espectral que se recusa a ficar na lista dos mortos: ele mata para ocupar o lugar de outro. Ele espera onde você achou o livro.»",
       inv_titulo="q_detetive",
       cond="zona", cond_npc="mercador_espectral", cond_lv=45, cond_nome="A Praça das Bancas", cond_n=10,
       cond_txt="«Minha banca era na praça do leste. Dez bichos derrubados DENTRO da praça e eu volto a vender lá — longe dela não conta.»"),
  dict(mapa="coroa_da_geada", lv=47, ev="escort", ev_npc="ferreiro_caravana", ev_nome="A Caravana do Gelo", ev_titulo="q_guardiao_caravana",
       ev_txt="«O trenó com o ferro da caravana precisa atravessar o vale branco. Os lobos sentem o cheiro de carne de longe. Ande junto do trenó e limpe as emboscadas.»",
       inv_npc="artesa_ilse", inv_lv=49, inv_nome="A Runa Falsa", pistas=["Runa entalhada de trás pra frente", "Lasca de gelo azul", "Pegada de gigante"],
       inv_txt="«Alguém está entalhando runas falsas para desviar a caravana para o abismo. Procure a runa errada, a lasca e a pegada.»",
       culpado_txt="«Pegada de gigante e gelo azul: um Gigante da Neve que aprendeu a escrever — e só aprendeu a mentir. Ele está onde a pegada some.»",
       cond="sem_morrer", cond_npc="mentor_harald", cond_lv=50, cond_nome="A Prova de Harald", cond_n=15,
       cond_txt="«Na Coroa, guerreiro bom é guerreiro vivo. Quinze trolls de gelo sem cair uma vez sequer. Caiu? Recomeça. É assim que se aprende.»"),
  dict(mapa="mar_de_escoria", lv=53, ev="defend", ev_npc="mestre_forja", ev_nome="A Forja Não Apaga",
       ev_txt="«Vou acender a forja grande pela primeira vez em cem anos. Os elementais vão vir comer o fogo. Segure quatro ondas no pé da bigorna!»",
       inv_npc="caravaneira_lu", inv_lv=55, inv_nome="Escória Roubada", pistas=["Carroça virada", "Rastro de brasa no chão", "Ferradura derretida"],
       inv_txt="«Roubaram a minha carga de escória refinada no caminho. Carroça virada, rastro de brasa, ferradura derretida — siga o calor.»",
       culpado_txt="«Ferradura derretida assim, só um Golem de Basalto com fome de metal. Ele dorme em cima da minha carga, onde o rastro termina.»",
       cond="noite", cond_npc="cozinheira_brasa", cond_lv=56, cond_nome="Brasas na Noite", cond_n=10, cond_titulo="q_noturno",
       cond_txt="«Besouro de brasa caçado à noite tem a carapaça mais quente — é o que faz o meu caldo. Dez, só depois das 19h.»"),
  dict(mapa="acropole_das_cinzas", lv=58, ev="escort", ev_npc="artesa_acropole", ev_nome="Relíquia para o Templo",
       ev_txt="«A estátua do primeiro juramento precisa voltar ao templo do norte. Ela é pesada e os centauros acham que é deles. Vá junto do carrinho.»",
       inv_npc="sentinela_acropole", inv_lv=59, inv_nome="O Último Sacerdote", pistas=["Estola de sacerdote rasgada", "Incenso ainda quente", "Marca de casco na escada"],
       inv_txt="«Dizem que o último sacerdote da Acrópole fugiu. Mas o incenso ainda queima toda noite. Ache a estola, o incenso e a marca na escada.»",
       culpado_txt="«Marca de casco… o sacerdote não fugiu: virou minotauro, preso no juramento que quebrou. Liberte-o — onde você achou a marca.»",
       cond="zona", cond_npc="sentinela_acropole", cond_lv=60, cond_nome="O Pátio dos Juramentos", cond_n=12,
       cond_txt="«O pátio do sudoeste é sagrado. Doze bichos derrubados DENTRO do pátio, e ele volta a ser solo de juramento.»"),
]

mapas = {}
def mapa(mid):
    if mid not in mapas:
        m = L(ARQ[mid]); m["objects"] = [o for o in m["objects"] if not o["id"].startswith("pista_")]; mapas[mid] = m
    return mapas[mid]

for r in REG:
    m = mapa(r["mapa"]); mid = r["mapa"]; rid = mid.split("_")[0]; nome = m["display_name"]
    esp = comuns(m)
    sp = [e[0] for e in esp]
    # -------- escolta / defesa
    if r["ev"] == "escort":
        x, y = perto(m, 12, 44); tx, ty = perto(m, 52, 2)
        add(id=f"q_var_{rid}_escolta", name=r["ev_nome"], giver=r["ev_npc"], kind="escort", targets=[sp[0], sp[1], sp[0]], count=3,
            map=mid, x=x, y=y, to_x=tx, to_y=ty, min_level=r["lv"], description=r["ev_txt"],
            hint=f"{nome}: a carroça sai a leste do acampamento e vai para o leste/centro. Fique perto e limpe as 3 emboscadas.",
            reward_items=["decor_lustre"], reward_flags=[f"titulo:{r['ev_titulo']}"] if r.get("ev_titulo") else [], category="opcional")
    else:
        x, y = perto(m, -28, 40)
        add(id=f"q_var_{rid}_defesa", name=r["ev_nome"], giver=r["ev_npc"], kind="defend", targets=[sp[0], sp[1]], count=4,
            map=mid, x=x, y=y, radius=6, min_level=r["lv"], description=r["ev_txt"],
            hint=f"{nome}: o ponto fica a oeste do acampamento. Aguente 4 ondas sem sair de perto.",
            reward_items=["decor_quadro_mar"], category="opcional")
    # -------- investigação: 3 pistas → culpado
    lugares = [(-62, 6), (60, -20), (-10, -60)]
    ids = []
    for i, (lx, ly) in enumerate(lugares):
        px, py = perto(m, lx, ly, 1.2)
        oid = f"pista_{rid}_{i + 1}"
        m["objects"].append({"id": oid, "kind": "reliquia", "x": px, "y": py, "label": r["pistas"][i]})
        ids.append(oid)
    add(id=f"q_var_{rid}_pistas", name=r["inv_nome"], giver=r["inv_npc"], kind="interact", targets=ids, count=3, map=mid,
        min_level=r["inv_lv"], description=r["inv_txt"],
        hint=f"Investigação em {nome}: 3 pistas (oeste, leste e sul do mapa). Aperte F em cada uma e volte.",
        reward_items=["consumable_c02_tonico_medio*3"], category="historia")
    culpado = sp[-1] if len(sp) > 1 else sp[0]
    add(id=f"q_var_{rid}_culpado", name=f"{r['inv_nome']} · O Culpado", giver=r["inv_npc"], kind="hunt", targets=[culpado], count=1,
        map=mid, x=px, y=py, radius=20, min_level=r["inv_lv"], requires=f"q_var_{rid}_pistas", description=r["culpado_txt"],
        hint=f"O culpado ({mons[culpado]['species_name']} Alfa) aparece perto da última pista, ao sul de {nome}.",
        reward_xp=xp(r["inv_lv"]) * 2, reward_items=["decor_estatua_leao"] if r["lv"] >= 45 else ["decor_aquario"],
        reward_flags=[f"titulo:{r['inv_titulo']}"] if r.get("inv_titulo") else [], category="historia")
    # -------- missão com condição
    alvo = sp[0]
    cond = r["cond"]
    if cond == "zona":
        cx, cy = sorted([(s["x"], s["y"]) for s in esp[0][1]], key=lambda p: (p[0], p[1]))[0]
        cond = f"zona:{cx},{cy},20"
        cond_hint = f"Só conta dentro do círculo marcado: {mons[alvo]['species_name']} perto de ({cx:.0f}; {cy:.0f})."
    elif cond == "noite":
        cond_hint = f"{mons[alvo]['species_name']} — só conta à noite (19h–5h do relógio do jogo)."
    else:
        cond_hint = f"{mons[alvo]['species_name']} — se você morrer, a contagem volta a zero."
    alvo = sp[1] if r["cond"] == "sem_morrer" and r["mapa"] == "coroa_da_geada" else alvo
    add(id=f"q_var_{rid}_condicao", name=r["cond_nome"], giver=r["cond_npc"], kind="kill", targets=[alvo], count=r["cond_n"], map=mid,
        min_level=r["cond_lv"], condition=cond, description=r["cond_txt"], hint=cond_hint,
        reward_items=["decor_tapete_real"], reward_flags=[f"titulo:{r['cond_titulo']}"] if r.get("cond_titulo") else [], category="opcional")

# ------------------------------------------------------------------ ramificações (mapas menores)
PEQ = [
  ("floresta_oculta", "curupira", 21, "hunt", "O Saci Mais Velho", "«Tem um saci aqui que é mais velho que eu — e eu sou velho. Ele dá nó no rabo dos meus bichos. Ache o redemoinho grande e pegue ele.»"),
  ("ruinas_automatos", "engenheira_ada", 41, "sem_morrer", "Teste de Resistência", "«Preciso medir quanto aguenta um aventureiro. Dez esferas-sentinela sem cair. Caiu, eu zero o medidor.»"),
  ("triangulo_demonio", "vigia_marujo", 46, "noite", "Vigília no Convés", "«À noite os marujos afogados sobem para procurar o navio. É só à noite que dá pra mandar eles de volta. Dez.»"),
  ("pico_vento", "eremita_vento", 47, "zona", "O Ninho das Harpias", "«As harpias fizeram ninho no rochedo do leste. Dez delas DENTRO do ninho — longe de lá elas só voltam.»"),
  ("fundo_triangulo", "sereia_lia", 50, "hunt", "O Tritão Desertor", "«Um tritão desertor rouba as pérolas do recife. Ele é grande e sabe brigar. Ache ele perto das pedras do leste.»"),
  ("caminho_serpente", "guardia_ponte", 51, "hunt", "O Anjo Que Não Caiu Inteiro", "«Um dos anjos caídos ainda brilha — e ataca quem cruza a ponte. Ache o mais forte deles.»"),
  ("ilha_serpente", "chefe_tribo", 56, "sem_morrer", "O Rito da Ilha", "«Na minha tribo, guerreiro é quem derruba dez guerreiros da ilha sem cair. Caiu, começa de novo. É o rito.»"),
]
for mid, npc, lv, tipo, nome_q, txt in PEQ:
    m = mapa(mid); rid = mid.split("_")[0]; esp = comuns(m); alvo = esp[0][0]; nome = m["display_name"]
    if tipo == "hunt":
        s0 = esp[0][1][0]
        add(id=f"q_var_{rid}_caca", name=nome_q, giver=npc, kind="hunt", targets=[alvo], count=1, map=mid, x=s0["x"], y=s0["y"], radius=20,
            min_level=lv, description=txt, hint=f"{mons[alvo]['species_name']} Alfa em {nome}, no bolsão onde eles vivem.",
            reward_xp=xp(lv) * 2, reward_items=["decor_aquario"], category="opcional")
    else:
        cond = tipo
        if tipo == "zona":
            alvo = esp[1][0] if len(esp) > 1 else alvo
            s0 = [s for s in m["spawns"] if s["monster"] == alvo][0]; cond = f"zona:{s0['x']},{s0['y']},20"
        dica = {"noite": "só conta à noite (19h–5h)", "sem_morrer": "se você morrer, recomeça do zero"}.get(tipo, "só conta dentro do ninho marcado")
        add(id=f"q_var_{rid}_condicao", name=nome_q, giver=npc, kind="kill", targets=[alvo], count=10, map=mid, min_level=lv, condition=cond,
            description=txt, hint=f"{mons[alvo]['species_name']} em {nome} — {dica}.", reward_items=["decor_tapete_real"], category="opcional")

# ------------------------------------------------------------------ escolhas com consequência (4 cadeias)
ESC = [
  # (id, npc, lv, mapa, A, B)  — A/B: (sufixo, nome, kind, targets, count, texto, dica, titulo, recompensa, continuação (npc, nome, texto, item/flag))
  dict(id="mata", npc="mentora_selene", lv=22, mapa="mata_dos_enforcados",
       A=dict(s="roca", nome="Escolha: Abrir a Roça", kind="kill", t=["monster_mata_raiz_andante"], n=10,
              txt="«A vila precisa de terra para plantar, e as raízes andantes não deixam. Derrube dez e a roça avança sobre a mata. A Curupira não vai gostar — é uma escolha.»",
              dica="Ficar do lado da vila (a outra escolha some). Raízes Andantes na Mata.", titulo="q_braco_roca",
              cont=("agricultora_benta", "A Roça Agradece", "«A roça cresceu onde era mata. Tome: a primeira receita de ensopado da vila — e o meu obrigado.»", ["receita:ensopado"], ["decor_quadro_mar"])),
       B=dict(s="mata", nome="Escolha: O Pacto da Curupira", kind="talk", t=["curupira"], n=1,
              txt="«A Curupira guarda a Floresta Oculta. Se você for falar com ela em nome da vila, a mata fica como está — e a roça não cresce. É uma escolha.»",
              dica="Ficar do lado da mata (a outra escolha some). Fale com a Curupira na Floresta Oculta.", titulo="q_amigo_mata",
              cont=("curupira", "O Presente da Curupira", "«Você falou por nós. Leve uma muda da floresta para a sua casa — ela cresce onde tem gente boa.»", [], ["decor_aquario", "decor_tapete_real"]))),
  dict(id="canions", npc="zelador_ascanio", lv=30, mapa="canions_suplicantes",
       A=dict(s="resgate", nome="Escolha: Resgatar os Presos", kind="kill", t=["monster_canions_troll"], n=6,
              txt="«Ainda tem mineiros vivos no fundo do cânion, presos pelos trolls. Resgatar é perigoso e lento — mas são vidas. A outra opção é selar a mina para sempre.»",
              dica="Resgate (a outra escolha some). Trolls das Fendas nos Cânions.", titulo=None,
              cont=("mineiro_bras2", "Os Mineiros Voltam", "«Você trouxe meus companheiros de volta. A gente reabre o veio fundo — e o primeiro ferro é seu.»", [], ["decor_estatua_leao"])),
       B=dict(s="selar", nome="Escolha: Selar a Mina", kind="interact", t=["pista_canions_1"], n=1,
              txt="«Selar a mina acaba com os espectros de uma vez — e com qualquer esperança de achar alguém vivo lá dentro. Acenda o pavio velho (F) e o cânion descansa.»",
              dica="Selar (a outra escolha some). Use o pavio queimado (pista a oeste dos Cânions).", titulo="q_voz_mortos",
              cont=("alma_ligia", "O Silêncio do Cânion", "«O cânion calou. Eu posso ir agora. Leve isto — é o que eu tinha de mais bonito.»", [], ["decor_lustre"]))),
  dict(id="vespera", npc="carteiro_espectral", lv=44, mapa="vespera",
       A=dict(s="entregar", nome="Escolha: Entregar a Última Carta", kind="talk", t=["guia_aurelio"], n=1,
              txt="«Esta é a última carta de Véspera: um morto confessando quem o matou. Entregue ao Aurélio e ele faz justiça — ou queime e deixe os mortos em paz.»",
              dica="Entregar (a outra escolha some). Fale com o Aurélio na vila de Véspera.", titulo="q_juiz_vespera",
              cont=("guia_aurelio", "Justiça em Véspera", "«Com a carta, os mortos fizeram o julgamento. Véspera dorme melhor. Leve o martelo do juiz — dizem que dá sorte.»", [], ["decor_trofeu"])),
       B=dict(s="queimar", nome="Escolha: Queimar a Última Carta", kind="kill", t=["monster_vespera_necromante"], n=8,
              txt="«Se queimar a carta, ninguém nunca vai saber. Mas os necromantes querem ela — tire oito do caminho até a fogueira. É uma escolha.»",
              dica="Queimar (a outra escolha some). Necromantes em Véspera.", titulo="q_voz_mortos",
              cont=("carteiro_espectral", "Cinzas de Papel", "«A carta virou cinza e o segredo também. Os mortos esquecem rápido… eu não. Leve isto e não conte a ninguém.»", [], ["decor_jukebox"]))),
  dict(id="escoria", npc="arauto_escoria", lv=54, mapa="mar_de_escoria",
       A=dict(s="livre", nome="Escolha: A Forja Livre", kind="kill", t=["monster_escoria_elemental"], n=10,
              txt="«O Mestre da Forja quer a forja aberta para todo mundo. A Caravaneira quer cobrar pedágio para a guilda. Ajude o mestre: derrube dez elementais que cercam a forja.»",
              dica="Forja livre (a outra escolha some). Elementais de Escória.", titulo="q_forja_livre",
              cont=("mestre_forja", "Martelo de Todos", "«A forja é de todos agora. E o primeiro refino de quem abriu ela é por conta da casa.»", [], ["decor_trono"])),
       B=dict(s="guilda", nome="Escolha: O Pedágio da Guilda", kind="kill", t=["monster_escoria_carbonizado"], n=10,
              txt="«A Caravaneira paga bem para quem limpa a estrada e garante o pedágio. A forja fica com a guilda. Dez carbonizados — e o ouro é seu.»",
              dica="Guilda (a outra escolha some). Carbonizados no Mar de Escória.", titulo="q_forja_guilda",
              cont=("caravaneira_lu", "A Parte da Guilda", "«Pedágio garantido. A sua parte — em ouro, como combinado.»", [], ["decor_trofeu"]))),
]
for e in ESC:
    for lado, outro in (("A", "B"), ("B", "A")):
        c = e[lado]; o = e[outro]
        f_eu, f_outro = f"escolha:{e['id']}:{c['s']}", f"escolha:{e['id']}:{o['s']}"
        q = dict(id=f"q_esc_{e['id']}_{c['s']}", name=c["nome"], giver=e["npc"], kind=c["kind"], targets=c["t"], count=c["n"],
                 min_level=e["lv"], description=c["txt"], hint=c["dica"], accept_flags=[f_eu], forbid_flag=f_outro,
                 reward_flags=[f"titulo:{c['titulo']}"] if c["titulo"] else [], reward_items=["consumable_c02_tonico_medio*2"], category="historia")
        if c["kind"] == "kill": q["map"] = e["mapa"]
        add(**q)
        cn, cnome, ctxt, cflags, citems = c["cont"]
        add(id=f"q_esc_{e['id']}_{c['s']}_2", name=cnome, giver=cn, kind="talk", targets=[cn], count=1, min_level=e["lv"],
            requires=f"q_esc_{e['id']}_{c['s']}", requires_flag=f_eu, description=ctxt,
            hint=f"Consequência da sua escolha. Fale com quem foi afetado.", reward_flags=cflags, reward_items=citems, category="historia")

# arauto_escoria precisa existir; se não, cai no mestre_forja
npcs_todos = {n["id"] for mid in ARQ for n in L(ARQ[mid])["npcs"]}
for q in Q:
    if q["giver"] not in npcs_todos: raise SystemExit(f"NPC inexistente: {q['giver']} em {q['id']}")
    for t in q["targets"]:
        if q["kind"] in ("kill", "hunt", "defend", "escort") and t not in mons: raise SystemExit(f"monstro inexistente {t}")

D("quests_variadas.json", Q)
for mid, m in mapas.items(): D(ARQ[mid], m)

# ------------------------------------------------------------------ descrições curtas das antigas (só o texto)
DESC = {
 "q_m09": "«Os saqueadores da Mata reviraram a horta da vila atrás de sementes — e levaram até as cercas. Derrube seis deles nos bolsões do leste e a gente replanta.»",
 "q_m12": "«As raízes andantes soltam uma resina grossa quando caem. É a melhor cola que existe para tábua. Traga quatro e eu fecho as frestas das casas antes do inverno.»",
 "q_s02": "«Tem uma espada cravada numa árvore oca, no oeste. Ninguém conseguiu tirar. Dizem que era de um cavaleiro enforcado aqui. Talvez ela ainda corte — e talvez escolha você.»",
 "q_m17": "«O nicho grande do leste guarda a história do elevador que levava os mineiros ao fundo. Vá até lá, olhe as marcas na pedra e me conte se ainda dá para usar.»",
 "q_m18": "«Ferro de Eco só nasce nos veios do cânion, onde o som bate e volta. Quatro pedaços e eu forjo ferramentas que não quebram.»",
 "q_m19": "«Os esqueletos mineiros levaram o meu medalhão quando eu caí. Era a única coisa que eu tinha da minha filha. Seis deles… um deve estar com ele.»",
 "q_m24": "«O elevador desce até o Brejo da Viúva. A curandeira Odete, na vila suspensa, precisa saber que o caminho reabriu. Leve a notícia — ela vai ter trabalho para você.»",
 "q_s05": "«Os ossos espalhados pelo cânion são de gente que eu conhecia. Junte-os e eu vou lembrando os nomes, um por um. Alguém precisa lembrar.»",
 "q_s06": "«Tem uma veia funda que ninguém tira faz anos — dizem que geme quando a picareta bate. Eu não tenho coragem. Você tem?»",
 "q_m25": "«As moitas perto das fontes dão a Folha Amarga — a base de todo remédio do brejo. Colha as amostras que eu preciso para descobrir o que está deixando a vila doente.»",
 "q_m26": "«Para o antídoto preciso de ferrões frescos: serpente e escorpião. Quatro. Cuidado — o que tem no ferrão é exatamente o que estamos tentando curar.»",
 "q_m27": "«No caldeirão da vila: duas Folhas Amargas, um Ferrão e um Frasco. Mexa devagar. Se der certo, a vila inteira volta a dormir sem febre.»",
 "q_m28": "«Abri a comporta e a água voltou aos canteiros. Plante a folha amarga, regue e colha — se crescer aqui, a curandeira nunca mais depende das moitas.»",
 "q_m29": "«Duas sedas de tecelã e um retalho viram ataduras na bancada. Nos brejos ninguém anda sem atadura — sanguessuga não perdoa.»",
 "q_m31a": "«Um pequeno draco caiu entre as raízes, ao norte da vila, com a asa machucada. Ele morde quem chega rápido. Vá devagar e acalme-o (F).»",
 "q_m34": "«Os afogados guardam cartas nos bolsos — cartas que nunca foram entregues. Traga cinco. Carteiro que é carteiro entrega, mesmo depois da morte.»",
 "q_m35": "«Os soldados espectrais carregam fichas de pagamento que ninguém nunca cobrou. Traga as fichas e eu reabro a minha banca — com desconto para você.»",
 "q_m36": "«Cogumelo, carne e flor de vínculo: o banquete memorial que Véspera fazia para lembrar os mortos. Faz cem anos que ninguém cozinha. Cozinhe na panela da vila.»",
 "q_m39": "«Base, núcleo e o que a sua classe pede. Na minha bigorna isso vira a sua primeira relíquia — e relíquia não se perde nem na morte.»",
 "q_s07": "«Os moradores de Véspera esqueceram o gosto de comida. Leve dois ensopados para eles provarem. Às vezes a memória volta pelo gosto.»",
 "q_s08": "«Um cavaleiro sem rosto espera no covil do centro. Ele desafia todo vivo que passa e nunca perdeu. Ainda.»",
 "q_m42": "«Ferro de Geada só nos veios guardados pelos draugr — eles vigiam o metal como se fosse ouro. Traga o minério e a caravana segue armada.»",
 "q_m43": "«Carne, raiz e erva de inverno: a refeição da travessia. Quem come isso atravessa a Coroa sem congelar os dedos. Cozinhe na panela da caravana.»",
 "q_m45": "«Três ligas de geada, três membranas e retalhos: essa é a estrutura das asas. Faça na bancada. Ainda não voa — mas já tem forma de céu.»",
 "q_m46": "«O núcleo que sustenta as asas está preso ao Jötunn, no Salão da Geada. Ele não vai entregar por bem.»",
 "q_m49": "«A leste do refúgio sai uma passagem de calor que não estava ali ontem. Vá ver o que se abriu — e não encoste nas paredes.»",
 "q_m50": "«Basalto férreo, quatro pedaços. Junto com o ferro de geada vira a liga que aguenta o calor do Mar de Escória.»",
 "q_m51": "«Cogumelo de cinza e raiz: um caldo que protege do calor por dentro. Cozinhe na panela do refúgio — sem ele, a escória queima até a respiração.»",
 "q_m52": "«O ninho da fênix fica numa ilha de rocha a oeste. Vá ver se o ovo ainda pulsa. Se pulsar, a fênix ainda pode voltar.»",
 "q_m55": "«O Colosso de Escória guarda a ponte de corrente para a Acrópole. Ninguém atravessou ainda. Seja o primeiro.»",
 "q_s10": "«O Ferreiro Corrompido, lá na Fornalha, levou o meu molde mais antigo. Sem ele não faço a lâmina que prometi. Traga de volta.»",
 "q_m57": "«Os guardiões de bronze ainda cumprem as ordens de um rei que morreu faz mil anos: não deixar ninguém subir. Seis deles, e a escada é nossa.»",
 "q_m58": "«Bronze antigo e uma memória: com isso faço a chave do Labirinto. Traga o bronze; a memória é você quem tem.»",
 "q_m59": "«O Guardião do Ritual protege o estandarte da Acrópole. Sem o estandarte, o Labirinto não abre. Vença-o.»",
 "q_m60": "«No Labirinto, o Minotauro guarda o atalho para o centro. Ele conhece cada parede. Você só precisa conhecer ele.»",
 "q_m61": "«No fim do Labirinto espera a Quimera de Bronze, a última guardiã do juramento. É o fim da estrada — e o começo da lenda.»",
 "q_m63": "«Uma liga lendária vira a ferragem do baú grande da sua casa. Traga uma — e nunca mais falta espaço para guardar tesouro.»",
 "q_fragmentos": "«Os bichos da Galeria Funda carregam fragmentos do veio. Eu pago bem por eles — e pago melhor ainda se você trouxer sem arranhão.»",
 "q_evo_g1": "«O primeiro Campeão caiu no Fosso do Capataz, no Vale das Cinzas. O elmo dele ainda está lá. Recupere — é o primeiro passo da sua evolução.»",
 "q_ruinas_automatos_3": "«O Centurião a Vapor comanda todos os autômatos das ruínas. Enquanto ele funcionar, os outros nunca param. Leve tônicos — e amigos, se tiver.»",
 "q_ilha_serpente_3": "«A Serpente Marinha cerca a ilha e afunda todo barco que tenta sair. A tribo depende de você. Leve tônicos — e amigos, se tiver.»",
 "q_pico_vento_1": "«Os lobos do vento uivam a noite inteira no Pico, e o uivo traz nevasca. Oito deles a menos e eu volto a dormir — e o céu volta a abrir.»",
 "q_pico_vento_3": "«O Gigante do Pico segura a nevasca com as próprias mãos. Derrube-o e o caminho para o céu abre. Leve tônicos — e amigos, se tiver.»",
 "q_ilha_serpente_par2": "«Os xamãs da ilha falam com a Serpente e mandam ela atacar os barcos. Oito xamãs a menos e a Serpente fica surda. Eles vivem mais para o fundo da ilha.»",
 "q_pico_vento_par2": "«Os lobos do vento que moram no fundo do Pico são os maiores — e os mais espertos. Oito deles, e me traga as histórias de como foi.»",
}
mud = 0
for f in glob.glob(os.path.join(C, "quests*.json")):
    if f.endswith("quests_variadas.json"): continue
    qs = json.load(open(f, encoding="utf-8")); ok = False
    for q in qs:
        if q["id"] in DESC and q["description"] != DESC[q["id"]]: q["description"] = DESC[q["id"]]; ok = True; mud += 1
    if ok: json.dump(qs, open(f, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("missões novas:", len(Q), "· descrições reescritas:", mud)

# ------------------------------------------------------------------ 13/11: Caixa das Cinzas +1..+5 (o servidor sorteia o prêmio)
CX = []
for t, (faixa, preco) in enumerate([("1–12", 400), ("13–24", 1500), ("25–36", 4000), ("37–48", 9000), ("49–60", 20000)], start=1):
    CX.append({"id": f"caixa_cinzas_{t}", "display_name": f"Caixa das Cinzas +{t}", "price": preco, "caixa": t,
               "heal_percent_max_health": 0, "heal_duration_ms": 0, "cooldown_group": "caixa", "cooldown_ms": 500, "stack_limit": 20,
               "summary": f"Use para abrir. Prêmio da faixa {faixa}: moedas, joias, peça de conjunto da sua classe (V{2 * t}+), arma, decoração… ou a SORTE GRANDE."})
D("consumables_caixas.json", CX)
print("caixas:", len(CX))

# ------------------------------------------------------------------ 13/11 (vila): mais decoração da casa (Ramires vende; caixas sorteiam)
base_dec = {"heal_percent_max_health": 0, "heal_duration_ms": 0, "cooldown_group": "decor", "cooldown_ms": 1000, "stack_limit": 1}
DEC = [("decor_planta", "Vaso de Samambaia", 900, 8), ("decor_bandeira", "Estandarte da Família", 4000, 15),
       ("decor_lareira", "Lareira de Pedra", 18000, 30), ("decor_estante_trofeus", "Estante de Troféus", 30000, 38)]
D("consumables_decoracao2.json", [{"id": i, "display_name": n, "price": p, "decor_points": pts, **base_dec,
      "summary": f"Decoração da casa (+{pts} de conforto). Use DENTRO da sua casa para colocar."} for i, n, p, pts in DEC])
shops = L("shops.json")
for sh in shops:
    if sh["id"] == "loja_decoracao_2":
        sh["items"] = [x for x in sh["items"] if x not in [d[0] for d in DEC]] + [d[0] for d in DEC]
D("shops.json", shops)
print("decoração nova:", len(DEC))

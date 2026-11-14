# E9 — Mundo de farm (cidade, Costa do Cal, Vale das Cinzas, Galeria Funda)

**Data:** 08/11/2026. **Pedidos:** `Docs/PEDIDO_E9_DIRECAO.txt` e a direção consolidada `Docs/PEDIDO_DIRECAO_CONSOLIDADA.txt`
(resumo em `Docs/DECISOES_OFICIAIS.md`, seções 8 e 9). **A seção 12 (direção consolidada) prevalece sobre o resto deste arquivo.**
Tudo abaixo é **provisório até você jogar**; os números vêm de dados (`Content/*.json`) e da simulação
(`python3 Tools/simular_progressao.py --classe guerreiro|mago [--acerto 0.7]`).

## 1. Teclas novas
| Tecla | Ação |
|---|---|
| F | também fala com NPC (missões, loja) |
| J | diário de missões |
| C | atributos (gastar pontos) |
| M | mapa do mundo e viagem rápida |
| P | grupo (convidar por nome ou quem está perto, sair, remover) |
| Y / N | aceitar / recusar convite de grupo |
| I com loja aberta | mochila mostra "Vender" |
| R | habilidade própria da arma equipada (só armas especiais; quadrado "—" quando a arma não tem) |
| T | habilidade da evolução de classe (mostra "20+" bloqueada até evoluir) |
| Shift (segurar) | correr: ×1,45, só fora de combate (3 s sem lutar, sem golpe em andamento, sem guarda) |

## 2. Mundo (mapas separados e conectados)
| Região | Mapa | Nível | O que tem |
|---|---|---|---|
| Porto das Cinzas | `porto_das_cinzas` | cidade | segura; Brás (ferreiro, loja), Ilma (tônicos), Capitã Ondina (missões), Tibério (missões/compra); praça com ponto de viagem; portão norte → Costa; portão leste bloqueado (Estrada do Leste, futura) |
| Costa do Cal | `costa_do_cal` | 1–10 (ver seção 12) | **Areal da Maré** (nv 1–2, um de cada vez, perto do portão) → **Dunas Salgadas** (nv 3–4, em dupla) → **Ruínas do Farol** (nv 5–6, grupos com ataques a evitar; farol visível de longe) → **Gruta da Maré** (elite nv 7, perigosa e opcional, baú) → **Baía Quebrada** (chefe de campo Rei das Pinças, nv 8). Acampamento das Dunas no meio (refúgio seguro + ponto de viagem + Lia). Atalho: portão entre o acampamento e as Ruínas, aberto pela alavanca do lado das Ruínas. Segredo: enseada atrás da parede ilusória leste. Barricada oeste: Pântano Cinzento (futuro). Saída nordeste → Vale |
| Vale das Cinzas | `vale_das_cinzas` (o mapa do E8) | 11–16 (ver seção 12) | mesmos lugares, segredo e atalho do E8; níveis subiram para 6–11, Capataz nv 11 (chefe de campo, 10 min). Fogueira = ponto de viagem; Otávio (missões da dungeon). Passagens: oeste do acampamento → Costa; **leste do acampamento → Galeria Funda (nível 9+)** |
| Galeria Funda | `galeria_funda` (dungeon) | 7–10, entrada na Gruta da Costa (ver seção 12) | Salão de entrada (seguro) → Câmara das Larvas → Veio Partido (oeste, escavadores + baú) e Posto da Sentinela (leste, elite) → Salão do Guardião (chefe). Saída na entrada e depois do chefe |
| (futuras) | — | — | Pântano Cinzento, Estrada do Leste, Cume das Brasas: aparecem no mapa do mundo como "em breve" |

- **Mapa do mundo (M):** regiões visitadas (claras), conhecidas por ligação (cinza), desconhecidas (?), futuras (tracejadas),
  "você está aqui" e a lista de pontos de viagem descobertos. Não marca segredos nem baús.
- **Viagem rápida:** só **parado num ponto de viagem descoberto**, vivo, **10 s sem combate**, para outro ponto descoberto.
  Pontos: Praça do Porto, Acampamento das Dunas, Fogueira do Vale (a dungeon não tem). Descobre ao chegar a 6 m.
- **Portais:** entrar no arco leva ao outro mapa; chega longe da volta (sem vai-e-volta) e longe de monstros
  (o validador recusa monstro a menos de 8 m da chegada). Portal com nível mínimo avisa e não deixa passar.

## 3. Personagem novo
- Nível **1**, **nada equipado**, de cueca opaca (boneco de manequim), 3 Tônicos Fracos, 0 moedas.
- Ataque gratuito: **Soco** do Guerreiro (leque curto, 0,62× poder, gera Fôlego) e **Faísca** do Mago (projétil de 11 m,
  0,6× poder, **sem mana, sem cajado**). O servidor troca o clique pelo ataque da arma assim que algo é equipado.
- **Primeira arma garantida:** missão "Mãos que Aprendem" (Capitã Ondina, perto do portão norte): 6 Caranguejinhos no Areal
  → Espada de Chapa ou Cajado de Aprendiz. Alternativas: loja do Brás (Porrete 35 / Galho 35 / Espada ou Cajado 120 moedas)
  e drop raro dos Caranguejinhos (4%). Não depende de sorte.
- Equipamento aparece no corpo: arma, escudo, **cabeça (slot novo)** e corpo.
- Personagens antigos: **nada retirado**. Ganham 3 pontos por nível que já tinham (migração v3).

## 4. Progressão (valores provisórios, escolhidos pela simulação)
- **Curva de XP (modo mapa):** `100 × L × (30 + 18 × L)` — nível 1 = 4.800; 5 = 60.000; 10 = 210.000.
  Substitui o "×8" do E8 (que fazia subir vários níveis por grupo de monstros). Modo arena (testes do Marco A) não mudou.
- **XP por abate:** `100 × nível do monstro × multiplicador do mapa × xp_mult do monstro`, com **queda gradual** quando o
  monstro é bem mais fraco: até 2 níveis abaixo integral; 3 = 80%; 4 = 60%; 5 = 45%; 6 = 30%; mais = 15%. Bônus de grupo do GDD mantido.
  Multiplicadores de mapa: Costa 1,4 · Vale 1,5 · Galeria 1,6 (região mais alta rende mais, nunca menos).
- **Faixa de poder (sem nivelar monstros):** contra monstro de nível maior você causa −12% por nível (mínimo 30%) e ele
  causa +15% por nível (até 2,5×); contra monstro de nível menor ele causa −8% por nível (mínimo 50%).
- **Atributos:** 3 pontos por nível. Força/Sintonia = +2,2 de poder por ponto; Vigor = +9 de vida. Armas têm mínimo de atributo.
- **Monstros comuns** voltaram perto da vida-base do GDD (eram 0,42–0,62× no E8 por causa do ×8): Caranguejo de Cal 0,8,
  Saqueador 1,0, Acólito 0,85. Os do Areal continuam muito fracos (0,42× vida, 0,55× dano, ataque lento de 950 ms).

**Ritmo estimado (simulação, NÃO jogo real; jogador mediano, 45% das preparações acertam):**
| Nível | Guerreiro (acumulado) | Mago (acumulado) | Onde farma (escolha do simulador) |
|---|---|---|---|
| 2 | 5 min (~24 abates) | 5 min | Areal da Maré |
| 5 | ~40 min | ~43 min | Dunas → Gruta → Trilha do Vale |
| 8 | ~1 h 40 | ~1 h 50 | Vale |
| 10 | ~2 h 30 | ~3 h | Vale / Galeria Funda |
| 12 | ~4 h | ~4 h 40 | Galeria Funda (voltas completas) |
O tempo para matar um comum do nível fica em 3–6 s (combate ágil); o que alonga é o número de abates por nível.
O simulador é ganancioso (vai para onde a XP/minuto é maior); na prática o jogador também faz Ruínas, Gruta e missões.

## 5. Drops (onde procurar cada coisa)
| Fonte | Moedas | Material | Equipamento |
|---|---|---|---|
| Caranguejinho (Areal) | 1–3 | Casca de Cal 50% | Porrete/Galho 4%, Trapos/Gorro 3% |
| Caranguejo de Cal (Dunas; Vale) | 2–5 | Casca 45% | Gibão/Gorro/Escudo de Tábuas/Trapos 7%, Machadinha/Vara de Coral 5% |
| Saqueador (Ruínas do Farol; Vale) | 5–10 | Retalho 40% | armas da região 10%, Elmo de Ferro/Cota/Gibão/Escudo/Manto 9% |
| Acólito (Ruínas do Farol; Vale) | 5–10 | Cinza Viva 40% | Cajado de Brasa/Manto/Capuz/Lança/Coral 10%, Vara Gêmea 3% |
| Caranguejo Couraçado (elite, Gruta) | 20–35 | Casca 2–4 | 1 peça garantida (Cota, Manto, Elmo, Capuz, Lança, Cajado de Brasa) |
| Rei das Pinças (chefe de campo) | 70–110 | Casca 4–6 | **Garra do Rei / Concha Ressonante 70%**, peça de armadura 60% |
| Capataz (chefe do Vale) | 120–180 | Retalho 3–5 | Malho / Cetro (garantido, como no E8) |
| Larva, Escavador (Galeria) | 2–14 | Fragmento do Veio 25–35% | Elmos 3%, armas e armaduras do Vale 8% |
| Sentinela de Cal (elite, Galeria) | 30–50 | Fragmento 2–3 | 1 peça garantida (Escudo de Pedra-cal, Couraça, Túnica, Elmo, Capuz) |
| Guardião da Galeria (chefe) | 200–280 | Fragmento 4–6 | **Lâmina da Galeria / Cajado do Veio 60%**, Elmo do Guardião 45%, armadura da Galeria garantida |
| Baús | — | — | Gruta (elmo), Farol (lança/cajado), Enseada secreta (machadinha/coral + gibão), Veio (armadura da Galeria); os do Vale como no E8 |
| Lojas | — | — | Brás: Porrete, Galho, Espada, Cajado, Trapos, Gorro, Escudo de Tábuas. Ilma: Tônico Fraco (12), Tônico Médio (55) |
| Missões | sim | — | primeira arma, Gibão (cascas), Elmo/Capuz (retalhos), tônicos |
Venda: 1/4 do preço (mínimo 1). Cada item mostra "Onde:" na mochila quando você ainda não pode usar.

## 6. Respawn e encontros (dados em `spawns` de cada mapa)
- Cada ponto: monstro, nível, grupo (chamam-se a 14 m), `respawn_seconds`; perseguição e volta pela `leash_radius` do comportamento.
- Areal 20 s (constante e seguro); demais 30 s; elite da Gruta 120 s; Rei das Pinças **15 min**; Capataz **10 min**; raro 40–45% a cada ciclo.
- **Não nasce em cima de ninguém:** com jogador a menos de 9 m do ponto, adia 3 s; ao surgir, fica 1,5 s sem procurar alvo.
- Chefe de campo avisa o mapa quando volta. Dentro da dungeon **não há respawn**.

## 7. Grupo
- Até 5. Convite por nome (P) ou para quem está perto; Y/N; líder remove; sair a qualquer hora. Aliados: ◆ no nome, barra azul, quadros com vida no canto.
- **XP:** quem bateu nos últimos 30 s + membros do grupo no mesmo mapa a até 30 m, vivos; total × (1 + 0,35 × (N−1)) ÷ N.
- **Loot:** cai no chão; **reservado 45 s** para esses participantes; depois livre. Moedas divididas igualmente. Missões de abate contam para todos.
- **Reconexão:** quem cai continua no grupo por 5 min.
- Sem mínimo de jogadores e sem escalar dificuldade pelo tamanho do grupo (o grupo sempre ajuda).

## 8. Dungeon (Galeria Funda)
- **Entrada:** portal no leste do acampamento do Vale, nível 9+. Cada grupo (ou jogador sozinho) recebe a **sua cópia**; quem é do grupo e entra depois cai na mesma cópia.
- **Morte:** renasce no Salão de Entrada da cópia em 5 s; o que já morreu continua morto.
- **Reinício dos encontros:** monstros vivos sem alvo voltam ao ponto e curam (inclusive o chefe). A cópia some **5 min depois de ficar vazia**; entrar de novo cria uma nova (tudo volta).
- **Reconexão:** se a cópia ainda existe, volta para dentro; senão, aparece na saída (acampamento do Vale).
- **Chefe:** três padrões aprendíveis (pilar em área com marca que fecha, lança de cal em linha desviável, ceifada em leque), sem ataques inevitáveis; ~1 a 1,5 min sozinho bem equipado, bem menos em grupo.

## 9. Moeda, loja e missões
- Moedas caem dos monstros (direto para quem participou) e vêm de missões. Compra e venda numa transação, perto do NPC, idempotentes (repetir o pedido não cobra/paga duas vezes).
- Missões (`Content/quests.json`): abate, entrega (os itens saem da mochila ao concluir), visita e chefe; algumas repetíveis. Ninguém precisa fazer missão para farmar.

## 10. O que foi executado, estimado e o que depende de você
- **Executado aqui:** 236 testes (225 antigos + 11 do E9) — início sem nada, soco/faísca grátis, portais e visibilidade por mapa,
  missão da primeira arma de ponta a ponta, entrega, loja sem duplicar, viagem rápida e suas travas, dungeon por grupo (cópias,
  nível mínimo, fim da cópia, volta pela saída), XP e loot de grupo, pontos de atributo, faixa de nível e respawn seguro.
  Cliente real (Godot, tela virtual): Porto, conversa com a Capitã, Costa, mapa do mundo, personagem equipado, mochila com
  comparação, convite de grupo e entrada dos dois na mesma dungeon (confirmado no log do servidor).
  Migração v2→v3 de uma **cópia do seu e6.db** (com o WAL): alice, bruno e Walter idênticos, 39 itens e marcas iguais, backup criado.
- **Estimado (simulação):** todo o ritmo da seção 4. Não é aprovação de balanceamento.
- **Depende de você jogar:** se o começo é acessível sem ser chato; se os degraus são perceptíveis; vida/dano dos inimigos;
  o chefe da dungeon sozinho e em grupo; a câmera e as telas novas; se a curva ficou arrastada demais ou de menos.

## 11. Decisões provisórias (reversíveis, em dados)
1. Personagens antigos ficam onde estavam (alice volta ao Porto porque estava na arena; bruno e Walter no Vale). A barra de XP deles fica mais longa com a curva nova; nível e XP não mudam.
2. Golpe de Cunha e Explosão continuam liberados no nível 1 (como o GDD/E8); Guarda no nível 5.
3. Itens do chão: reservados 45 s para quem lutou, depois livres (antes: livres na hora).
4. Sem penalidade de morte (pendente de decisão sua).
5. Nomes de lugares, NPCs, monstros e itens novos são provisórios (IDs estáveis).

> Níveis 17–60 (regiões, dungeons, ofícios, casa, montaria, companheiros): `Docs/MIDGAME_M01_M06.md`.

## 12. Direção consolidada (08/11/2026) — o que mudou nesta rodada
Pedido: `Docs/PEDIDO_DIRECAO_CONSOLIDADA.txt`. Suas escolhas: reorganizar faixas, evolução padrão, armas com estilo +
habilidade, mudanças do mundo por personagem.

### 12.1 Faixas de nível (substituem as das seções 2 e 4)
| Faixa | Onde | Níveis dos monstros |
|---|---|---|
| 1–5 | Costa: Areal (1–2) → Dunas (3–5) | aprendizado, primeira arma, primeiras peças |
| 6–10 | Costa: Ruínas do Farol (6–8), Gruta (7–8), Rei das Pinças (10) + **dungeon introdutória Galeria Funda (7–10)** | arredores, elites, dungeon |
| 11–16 | Vale das Cinzas (11–16), Capataz 16 | segunda região |
| 17–20 | **próximo mapa (ainda não existe)** | a evolução já está pronta para quando o conteúdo chegar |
- **Galeria Funda mudou de lugar:** a entrada agora é um arco no fundo da Gruta da Maré (Costa, oeste das Ruínas). O portal do Vale foi retirado.
- **Nível do portal é recomendado, não trava:** abaixo do nível, avisa ("Cuidado: … é para nível 7+") e deixa entrar.
- Equipamento da dungeon a partir do nível 8 (valores reduzidos); Garra/Concha do Rei 9; peças novas do Vale nível 11
  (Espada da Vigília, Cajado de Cinza Viva, Cota Reforçada, Manto de Cinzas) — só caem de monstros nível 11+ (`min_monster_level` no loot).
- Multiplicadores de XP (simulação abaixo): Costa 1,4 · Galeria 1,2 · Vale 1,1.

**Ritmo estimado (simulação, não jogo real):**
| Chega ao nível | Guerreiro | Mago |
|---|---|---|
| 5 | ~41 min | ~43 min |
| 10 | ~2 h 55 | ~3 h 10 |
| 11 (entra no Vale) | ~3 h 30 | ~3 h 50 |
| 16 | ~7 h 50 | ~8 h 50 |
| 20 | estimado ~14–16 h (depende do próximo mapa) | idem |
O simulador às vezes vai cedo para a Gruta (nível 3–5, "perigosa"): é o "aventurar-se cedo"; na prática exige esquiva.

### 12.2 Armas: estilo + habilidade (R)
| Arma | Estilo do ataque básico | Habilidade (R) |
|---|---|---|
| Espada | rápida, alcance médio (padrão) | Garra do Rei: **Corte que Avança** (avança 4,5 m acertando o caminho) |
| Machado / Machadinha / Malho | mais lento, **interrompe** preparações (stagger) | Machado de Guerra: **Onda de Impacto** (anel 3,2 m, interrompe) |
| Lança | alcance maior, **cone estreito 55°** (mais direcional) | — |
| Lâmina da Galeria | — | **Golpe em Arco** (leque 220°) |
| Cajado do Veio | — | **Seta Perfurante** (atravessa vários) |
| Concha Ressonante | — | **Estilhaço** (explode ao acertar, 2,4 m) |
A barra mostra R com a recarga; a mochila diz "Habilidade (R): …" no resumo da arma. O servidor recusa a habilidade sem a arma.

### 12.3 Evolução de classe (nível 20 + missão)
- **Guerreiro → Campeão:** mentor Mestre Dorval (Porto). Missão 1: achar o Elmo do Primeiro Campeão (relíquia no Vale);
  missão 2: vencer o Capataz. Ganha **Investida (T)** — avanço de 6 m que interrompe — e o **Golpe de Cunha passa a atordoar 1,2 s**.
- **Mago → Arcanista:** mentora Isaura (Porto). Missão 1: o Grimório Afogado (Costa); missão 2: vencer o Capataz. Ganha
  **Lança Arcana (T)** — projétil que atravessa e interrompe — e a **Explosão deixa o chão em chamas 3 s**.
- Nome da classe evoluída aparece em aviso; a habilidade T fica "20+" (bloqueada) até lá. Nível 20 ainda não é alcançável (conteúdo vai até 16).

### 12.4 Inimigos
- **Elites com combinação curta:** Caranguejo Couraçado (bote → pinçada emendados, brecha no fim), Sentinela (golpe de escudo → pisão).
- **Chefes com 2ª fase** abaixo de 50% de vida: aviso "X enfurece!", luz vermelha, "ENFURECIDO" na barra; ataques ~30% mais frequentes,
  20% mais rápidos e um golpe a mais (Rei: Maré de Conchas; Guardião: Pilares Gêmeos; Capataz: Terremoto). Volta ao normal se a luta reiniciar.

### 12.5 Mundo que muda (por personagem) e viagens
- **Ponte da Ravina (Costa, norte):** ravina intransponível; a missão "Tábuas para a Ravina" (Lia, no acampamento das Dunas; 8 Cascas) reconstrói a ponte
  **para quem fez** (marca `ponte:ravina`) e traz o **mascate Bento** para o acampamento. Nenhum jogador bloqueia a missão de outro.
- **Barqueiro Tibério (Porto):** viagens pagas — Dunas 10 moedas (depois de descobrir o acampamento), Vale 40 (depois de visitar o Vale).
  Fora de combate; destinos não descobertos não aparecem.
- Missões de **conversar** (ex.: levar recado ao Guarda Rui no Vale) e de **interagir** (relíquias que só aparecem com a missão ativa).
- **Mapa do mundo** mostra "!" nas regiões com missão ativa (onde fazer ou onde entregar) e a lista ao lado.
- Compartilhado entre todos: só o cenário fixo e cosmético. Pontes, NPCs e relíquias são por personagem.

### 12.6 Dungeon
- **Checkpoint** "Antessala do Guardião" antes do chefe: ao passar, quem morre renasce ali (não na entrada).
- **Primeira conclusão garantida** (por personagem, marca `clear:galeria_funda`): Lâmina/Cajado da Galeria + peça + moedas,
  além do loot normal. Caminho secreto: parede ilusória na sala leste → nicho com baú.

### 12.7 Mochila nova (estilo RPG)
Boneco com quadros de **cabeça, arma, corpo e mão secundária** à esquerda (o que está vestido), grade de 40 espaços à direita,
detalhes embaixo (nome na cor da qualidade, requisitos em vermelho se faltar, comparação com o equipado, onde achar, preço de venda).
Clique seleciona; duplo clique ou botão direito equipa/usa; no boneco, tira. Ícones desenhados por tipo de item.

### 12.8 Executado / estimado / depende de você (esta rodada)
- **Executado:** 248 testes (236 + 12 novos: combo de elite, 2ª fase, machado interrompe/espada não, habilidade só com a arma e
  avanço acertando o caminho, seta perfurante, evolução só depois da missão, ponte por personagem e mascate, barqueiro cobra e
  esconde destino, checkpoint + primeira conclusão, corrida só fora de combate, missão de conversar + "!" no mapa, item do Vale só
  de monstro 11+). Cliente 0 erros/0 avisos; capturas: mochila, ponte quebrada/pronta, barra com R/T, diálogo do barqueiro.
- **Estimado:** ritmo (12.1). **Não testado no cliente real:** efeito visual das chamas e da 2ª fase (código pronto, sem captura).
- **Depende de você jogar:** sensação das armas, se a 2ª fase é legível, se a Gruta/Galeria aos 7 está justa sozinho.

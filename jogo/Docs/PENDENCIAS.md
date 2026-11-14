# Pendências — lista única (08/11/2026)

Tudo o que falta, num lugar só. Depois do teste de 30 min do usuário, cruzar os pontos dele com esta lista e priorizar.
Marcar `[x]` quando entregar. Prioridade sugerida: **A** = entra na demo caprichada (Porto → Costa → Galeria, níveis 1–10);
**B** = logo depois; **C** = mais adiante.

## ★ Visão do usuário (09/11 16:35) — guia para todas as entregas
- **Tudo o mais AAA possível**: HUD, menus, equipamento, mapa, atributos, movimento, ataque dos inimigos, interação com o mapa, trilha.
- **MU**: itens brilhantes e chamativos; começa na cidade; sobe de classe ao chegar no nível X; monstros em volta da cidade,
  nos arredores e em lugares secretos; passagens para outros lugares; depois de chegar andando (ou por NPC que te envia),
  viaja direto para lá.
- **Skyrim / Diablo**: dungeons desafiadoras com muitos monstros, mini-chefe e chefe; recompensa boa.
- **Elden Ring**: combate dinâmico; poderes e armas com habilidade única; inimigos com sequências de golpe para decorar
  (meio-termo, não 100% punitivo).
- **Zelda**: jeito de se locomover e duelo fluido.
- Controles: botão do meio trava/destrava alvo; rodinha troca de alvo enquanto travado (feito 09/11).
- [x] **Troca de classe estilo MU** (09/11): nível 20 + falar com o mentor da classe no Porto (botão "Trocar de classe",
  missões `q_troca_g|m|b|a`, tipo `evolve`). A antiga cadeia `q_evo_*` virou prova opcional (dá moedas, não troca mais).
  Classe evoluída ganha cabeça de bicho: Guerreiro leão, Mago corvo, Arqueiro serpente, Bruxa gato (elmo/capuz somem).
- [x] Esferas de nível 2 (09/11): `tomo_*_ii` (gerados por `Tools/gerar_tomos.py`), à venda nos mentores; só classe evoluída e só para poder já aprendido: +40% de força, recarga −20% (marca `up:<skill>`). Falta: cair de monstro e mostrar "II" no Livro de Poderes.
- [x] Feedback 09/11 17h (jogando): poeira do pé virou nuvem macia translúcida; partículas do ar cortadas pela metade;
  movimento liso (extrapola a velocidade entre fotos do servidor — antes andava aos trancos correndo); corte do Guerreiro
  da esquerda p/ direita em arco contínuo com rastro da lâmina; monstro morto afunda e some; corpo não entra no chão;
  monstros com contorno de luz + sombra embaixo; câmera estilo MU (35° de cima); placas/nomes só quando importam;
  moedas numa linha só; item comum sem pilar de luz; metade do capim. **Testar jogando.**
- [ ] Câmera: objetos entre a câmera e o herói devem ficar transparentes (poste tampando o boneco na foto).
- [ ] Itens com acabamento MU (metal polido, pedra verde-esmeralda, volume) — NÃO é brilho no corpo (usuário corrigiu).

## ▶ Ordem de trabalho (09/11, com o usuário no PC)
1. **Movimento e combate** (escolha do usuário): parar/virar com peso (sem "gelo"), poses próprias por habilidade com o braço
   da arma certo, travar alvo melhor (câmera segue, troca rápida), aparar (parry) do Guerreiro, arma muda o poder e não o golpe.
2. **Monstros e chefes**: caranguejos/saqueador/acólito/larva/chefes no nível do manequim; Rei das Pinças e Guardião com 5–6
   golpes e 2ª fase; caracterização por espécie; afixo explosivo.
3. **Roupas e personagem**: capuz redondo e túnica reta do mago; padrão de roupas com variantes; conjuntos (set).
4. **Cidade e vida**: NPCs trabalhando, partículas (faíscas, folhas), luz/cor por lugar, janela de atributos estilo MU.
5. **Itens e ritmo**: ~~afixos visíveis, lojas que importam~~ (feito 09/11), loot do Guerreiro 1–10, ~~missões variadas (escolta/defesa/caça)~~ (feito 09/11).
6. **Com o usuário no PC**: medir FPS na placa real (F10), 2 jogadores (cura/barreira e E6 cenário B), mira, ritmo jogado.
Decisões já tomadas: poderes antigos ficam; classes não travam.

## 1. Demo caprichada (fatia vertical, níveis 1–10)
- [ ] **A** Escolher o trecho final da demo e o tempo-alvo (proposta: Porto → Costa → Galeria Funda, 1–2 h).
- [ ] **A** Calibrar a dificuldade jogando (morrer algumas vezes no chefe sem frustrar; precisar farmar um pouco).
- [ ] **A** Primeira hora sem momento de "não sei o que fazer" (missões, setas, dicas).

## 2. Sensação do combate (Elden Ring / Zelda)
- [x] **A** Peso no golpe: pausa curta no impacto (crítico/pesado/abate), empurrão do alvo, tremor. Falta: som por tipo de arma.
- [x] **A** Rolamento: cambalhota de 360° encolhido.
- [x] **A** Golpes encadeados do básico: diagonal → horizontal de volta → estocada.
- [x] **B** Poses próprias por habilidade (09/11): giro (volta inteira de braços abertos), golpe de cunha (de cima com as
  duas mãos), salto (agacha/sobe/bate), investida (lança baixa), soco, varrida, chuva de flechas (arco para o céu),
  arremesso, espinhos (mãos no chão), lança de gelo (duas mãos). Conferir: `VITRINE_HAB=<id> --vitrine classes`.
- [x] **B** Travar alvo melhor (09/11): Tab trava e, travado, pula para o próximo alvo; botão do meio solta; alvo morreu →
  passa sozinho para o mais perto (até 10 m). A câmera já acompanhava.
- [x] **C** Aparar (parry) do Guerreiro (09/11): Guarda levantada até 0,3 s antes do golpe de frente anula o dano e deixa o
  monstro aberto 1,5 s (chefe 0,8 s) — "APAROU!", faísca, tinido e pausa curta.

## 3. Monstros e chefes
- [x] **A** Os 2 chefes da demo com 5–6 golpes, combos e 2ª fase (09/11): Rei das Pinças — investida emenda esmagada,
  martelo emenda pinça de volta, 2ª fase ganha Fúria da Maré (área grande que atordoa) + ajudantes; Guardião — ceifada
  emenda ceifada reversa, lança de cal emenda pilar no alvo, 2ª fase ganha Sino de Cal (atordoa) + raio e pilares gêmeos.
- [x] **A** Monstros comuns da demo com 2–3 ataques próprios cada (hoje: 87 monstros, só 19 padrões; 12 com 1 ataque).
- [x] **B** Efeitos de status: veneno (picada/presas/teia/cuspe), lentidão (gelo/sopro/raízes), queimadura (brasa/labareda/escória), atordoamento curto (pancada de chefe); aparecem acima do frasco de vida.
- [ ] **B** Movimentos novos: salto de verdade, agarrão, invocar ajudantes, cavar/sumir, voar baixo.
- [x] **B** Chefes do nível 17–60 com identidade (hoje 10 chefes usam só 2 moldes: "genérico" e "conjurador").
- [x] **C** Elites com afixos estilo Diablo (mundo real): Rápido (+35% de passo), Vampírico (cura 35% do dano), Blindado
  (−30% de dano recebido), Feroz (+25% de dano); aparece no nome. Falta: explosivo (estoura ao morrer).

## 4. Visual
- [x] **A** Monstros da demo no nível do manequim (09/11): saqueador/acólito/capataz já saem do manequim; caranguejos, larvas e
  feras ganharam couro com textura e relevo (quitina com brilho molhado, pele com poros, escamas — `gerar_texturas.py --criaturas`).
- [x] **A** HUD compacta conforme a prancha 06 (tarefa pendente da etapa visual 1).
- [ ] **B** Rodada visual 2 — cidade conforme a prancha.
- [ ] **B** Rodada visual 3 — movimento (andar, correr, virar, parar com peso).
- [ ] **B** Rodada visual 4 — personagem (mais conjuntos de roupa, rosto/máscara, capas).
- [x] **B** NPCs no manequim (hoje só Hazel e Kaelen; os outros ainda são bonecos simples).
- [ ] **C** Medir quadros por segundo numa placa de vídeo real (aqui é tudo por software); ajustar F10.

## 5. Recompensa e itens (Diablo / MU)
- [x] **A** Ritual do drop: feixe, cor e **som próprios por raridade** (comum/raro/lendário); nome colorido no chão; o item salta do chão girando ao cair.
- [x] **A** Comparação na hora de pegar ("▲ +12 Poder", texto verde se melhor, vermelho se pior) no aviso de pegar.
- [x] **A** Itens "de história" na faixa 1–10 (`Content/items_historia.json`, nível 7, lendários com efeito a cada 5 acertos):
  Porrete do Afogado, Cajado do Faroleiro, Talismã das Mil Conchas, Arco da Vigia da Duna — raros no Rei das Pinças (5%),
  Chefe Goblin e Caranguejo Dourado (3%).
- [x] **B** Afixos visíveis e mais variados nos itens raros (09/11: `Shared/Content/ItemAffixes.cs` — incomum 1, raro 2, épico 3, chefe 2, lendário 1; poder %, vida, armadura, velocidade, regeneração; valores sobem com o nível do item; sorteio fixo pelo id do exemplar, sem coluna nova no banco). Drop comum de equipamento vira incomum (20%) ou raro (5%) no mundo vivo. Mochila/loja mostram os afixos (◆) e a cor da raridade (verde = incomum).
- [ ] Reforjar/temperar com mais sentido (ex.: reforjar sorteia afixos de novo).
- [ ] **B** Baús e segredos com recompensa que valha a ida (exploração estilo Skyrim).
- [x] **C** Conjuntos (set) com bônus por peças (09/11): 4 conjuntos de história (um por classe: arma + corpo + cabeça),
  2 peças = armadura e vida, 3 peças = +12% de poder (+ recurso/passo). Mochila mostra o conjunto e acende o que já vale.

## 6. Progressão e missões
- [ ] **A** Verificar jogando o ritmo 1–10 (XP por monstro, por missão, moedas, preço das lojas).
- [ ] **B** Verificar o ritmo 10–20 e a evolução (Campeão, Arcanista, Matriarca, Batedor).
- [x] **B** Missões com mais variedade (09/11: três tipos novos em `Server/Net/GameServer.QuestEvents.cs` — **caçada** (presa alfa aparece no rastro), **defesa** (segurar N ondas no ponto; sair cancela a onda), **escolta** (carroça anda com você perto e caminho limpo, emboscada a cada trecho; carroça desenhada no cliente). 5 missões: Javali da Duna, Segure a Barraca, Carga do Mineiro (Costa), Saci Ladrão (Vale), Horta Cercada (Mata). **Testar jogando**: dificuldade das ondas e pontos no mapa.)
- [ ] Puzzle simples (alavancas/runas) como tipo de missão.
- [ ] **C** Ritmo 20–60 jogado de verdade (hoje só estimado: ≈ 63 h só com abates).

## 7. Som (estilo MU: **sem trilha sonora**, só ambiente e efeitos)
Hoje: sem música (certo); só 3 fundos genéricos feitos por código (vento, cidade, masmorra) escolhidos pelo tipo de mapa;
monstros com 2 sons genéricos (rugido e morte).
- [x] **A** Fundo por **região**: Costa (ondas, gaivotas, vento), Vale (vento seco, corvos), Mata (pássaros, galhos,
  corujas à noite), Cânions (vento uivando), Brejo (sapos, insetos, água), Véspera (sinos distantes), Geada (nevasca),
  Escória (lava borbulhando, estalos), Acrópole (vento alto, ecos), dungeons (gotas, ecos, correntes).
- [x] **A** Sons **no lugar** (diminuem com a distância): martelo do ferreiro na forja, fogueira/lanternas, fonte,
  caldeirão, vozes da cidade, água do porto, ondas na praia.
- [x] **A** Sons por **criatura** (voz ao preparar o golpe): casco/pinça, ossos, goblin, inseto, espírito, fera, bruto, guincho,
  feitiço; dragão ruge. Acerto pelo tipo da arma (lâmina, contundente, flecha, magia). Falta: grito próprio por chefe.
- [x] **B** Passos por tipo de chão (terra, areia, pedra, lama, neve) + tilintar de metal com armadura de malha/placa.
- [x] **B** Som de cada habilidade por família: fogo, gelo, raio/lampejo, cura, escudo, giro, impacto, investida, sombra,
  espinhos, chuva de flechas, arco tenso (`hab_*.wav`).
- [ ] **B** Fontes: sons livres CC0 de qualidade (ex.: bibliotecas de domínio público) no lugar dos gerados por código,
  com créditos/licença registrados em `Client/Assets/Sounds/LICENCAS.md`.

## 8. Bugs e furos conhecidos (relatório de 08/11)
- [x] **A** `world.json`: regiões "Pântano Cinzento" e "Estrada do Leste" ("em breve") saíram do mapa do mundo (gerador também).
- [x] **A** Mapa do mundo com caixas sobrepostas (virou pergaminho com pontos e quadro ao passar o mouse).
- [x] **A** Diário vazio sem mensagem ("nenhuma missão ativa — fale com a Capitã Ondina").
- [x] **B** Materiais sem origem: espada sem dono (Cripta do Sino → forja: Espada da Mata), estandarte (Guardião do Ritual →
  Chave de Bronze), molde (ferreiros de brasa → Liga Lendária), quitina (bancada → Placa de Chapa).
- [x] **B** Antídoto cura o veneno.
- [x] Retrato do Mago (erro de desenho), papel "missoes" cru no diálogo, "Criar personagem: ok." — corrigidos.

## 9. Classes novas (Bruxa e Arqueiro)
- [ ] **B** Testar cura e barreira entre dois jogadores reais.
- [ ] **B** Ritmo da Bruxa sozinha (dano baixo?) e do Arqueiro de perto.
- [ ] **C** Habilidades de arma (tecla R) próprias para talismãs e arcos.

## 10. Pendências antigas que não podem sumir
- [ ] **B** E6 cenário B: disputa real de item com 2 clientes.
- [x] **B** Penalidade de morte: decidido pelo usuário (09/11) — morrer no mundo perde 1 nível (mínimo 1) e zera a XP do nível; reconquistar não dá pontos de novo (migração v7 `max_level`).

## 11. Pontos do teste do usuário (08/11, Arqueiro "Legolas", níveis 1–3)
Todos entram como **A** (vieram de jogar), salvo indicação.

**Armas e ícones**
- [x] Cada classe começa com uma arma "fuleira" (a pior do jogo) para fazer sentido: arco torto, talismã de osso,
  graveto/cajado rachado, porrete. Arqueiro sem arco não pode atirar flechas.
- [x] Ícone do básico por classe (o Arqueiro mostra uma espada; deve ser um arco).

**Habilidades por livro/tomo (mecânica nova)**
- [x] Personagem novo começa **só com o ataque básico** (todas as classes). Poderes vêm de **livros/tomos**:
  comprados (caros), achados em baú/chefe ou dados por missão de mentor. O jogo guia: missão que leva ao primeiro tomo.
- [x] Slot sem poder fica **vazio** na barra (não mostrar habilidade "bloqueada").
- [ ] Arma muda o **poder** do ataque (e o visual), não o conjunto de golpes; machado = ataque pesado próprio.
- [x] Novos poderes do Arqueiro: tiro carregado que atravessa vários inimigos; marcar área e cair chuva de flechas;
  rajada rápida de 3 flechas. **Ultimates**: fênix de fogo; serpente do mar com tsunami. (Equivalentes para as outras classes.)
- [x] Arqueiro gasta Foco ao atacar: básico custa pouco, tiro carregado custa mais.

**NPCs vivos**
- [x] NPCs fazendo coisas: ferreiro martelando, curandeira mexendo o caldeirão, pescador escorado, guarda fumando/apoiado — **feito (09/11)**: ferreiro martela, agricultora mexe na terra, curandeira/arcanista conjuram, guarda de braços cruzados olhando ao longe, pescador/capitã vigiam o mar, bobo faz malabares.
  na lança, gente conversando (hoje parecem estátuas).
- [x] Marcador na cabeça: **amarelo** = algo novo (missão ou conversa nova); **verde** = missão pronta para entregar;
  **nenhum** = nada novo (volta quando liberar algo por nível/progresso).

**Inventário, loja e itens (estilo MU / Diablo)**
- [x] Mochila e loja com o **desenho do item** visível; ao passar o mouse, quadro com atributos, requisitos e comparação.
- [x] Visual mais bonito e "real" (moldura, grade de couro/metal, cores de raridade).
- [x] Ao abrir baú/ganhar item, sobe a **imagem do item** junto com o texto.
- [x] Lojas com itens "daorinha" de começo, **caros**, que mudam cor e formato — motivo para farmar e voltar.

**Progressão**
- [x] Explicar/mostrar o que o nível dá (hoje: +3 pontos de atributo por nível, tecla C, manual estilo MU).
- [x] Recompensas por nível (estilo "passe"): a cada 5 níveis novos, moedas (nível × 25) e 3 poções da faixa; no 10 e no 15 o
  tomo da classe daquela faixa. Uma vez só (nível reconquistado não paga de novo). Falta: título/cosmético.
- [x] **Aura ao subir de nível** (estilo Dragon Ball) em volta do personagem.

**Mundo e design de nível**
- [x] Portão com alavanca que dá para contornar pelos lados — fechar as laterais (e revisar todos os portões).
- [x] Ponte quebrada também contornável — fechar.
- [x] Missão de consertar ponte pedindo **tábuas**: cortar árvore com **machado de ofício** (item só de coleta, vendido na loja).
- [x] Mapas **maiores** e com mais caminho: sensação de jornada ("onde fica? achei!"), não tudo no portão de casa.
- [x] **Mais vida**: mais monstros e mais variados (goblins/anões, animais), objetos, detalhes, texturas.
- [x] Mistura de espécies nos grupos (ex.: a cada 3 caranguejos, 1 goblin).
- [x] Pedras, paredes e objetos no meio do mapa **sem colisão** — todos precisam bloquear.

**Dificuldade**
- [x] Monstros do começo **fáceis demais** — subir dano/vida/agressividade.
- [x] Chefe lento e fácil de desviar (principalmente de arco): mais rápido, golpes que alcançam longe, e
  **ajudantes que nascem durante a luta** (caranguejos menores).

**Coleta**
- [x] Colher erva: segurar o botão com **barra de progresso** e animação (hoje só diz "você se mexeu").
- [x] Erva demora **~1 hora** para renascer (por planta) e a missão pede mais ervas — obrigar a procurar.

**Novos pedidos (08/11, 17:48)**
- [x] **Hordas caçadoras**: a cada ~10 min (mapa aberto, fora da cidade) 3 bichos da espécie da área, 2 níveis acima e +30% de
  vida, nascem a 20 m e vêm atrás de você (aviso "vem te caçar"). Não renascem. Vencer a horda inteira paga nível × 20 moedas a quem lutou.
- [x] Janela de atributos (C) mais bonita, estilo MU, com textura de pedra. (09/11: cabeçalho nome/classe/nível, placas por atributo com "+" dourado, ficha de valores em 2 colunas)
- [x] **Bichinho-guia**: Pip, um vagalume que voa em volta do personagem e, fora de combate, dá uma dica por vez (pontos de
  atributo, missão pronta, tomo/item melhor na mochila, sem missão, sem poção, região fácil demais). `Client/GuideHints.cs`.
- [ ] Pequeno draco: hoje sai pela cadeia do Brejo (nível ~31); avaliar um companheiro antes.

**Som**
- [ ] Trocar os sons gerados por código por **sons livres (CC0) de qualidade** — ver seção 7.

## 12. Teste do usuário, 2ª rodada (08/11, 18:57)
**Interface**
- [x] Arrastar itens na mochila (mochila ↔ corpo, mochila ↔ loja para vender).
- [x] **Tela cheia** não funciona (botão de expandir e Alt+Enter) e a HUD fica cortada em janela pequena.
- [x] Mapa do mundo bonito: arte de mapa, pontos (bolinhas) que reagem ao passar o mouse, quadro elegante.
- [x] Diário (J): fixar/desafixar missões na tela.
- [x] Diálogo do NPC sem as missões já concluídas (ou numa aba à parte).
- [x] Contador de "Caranguejo de Cal" não atualiza na tela em algumas missões (no servidor conta).
- [x] Alcance para falar com NPC: 3 m do boneco (antes ~3,8 m no cliente e 4,5 m no servidor).
- [x] Frasco de vida: o globo de vida/mana virou frasco de vidro (bojo, gargalo, rolha), mesmo número e balanço (08/11 22:40).
- [x] Som do caldeirão: borbulhar de líquido (bolhas que sobem de tom + fervura grave), também em laço perto do caldeirão.

**Progressão estilo MU**
- [x] Tomos e itens pedem **atributo** (ex.: Destreza X, Força Y) além de nível — obriga a distribuir pontos com cuidado.
- [x] Poder do Arqueiro liberou sozinho no nível 4 (personagem antigo): tudo pelo livro. **Decisão (09/11): manter** — os antigos
  ficam com o que já têm; personagem novo só aprende pelo livro.
- [x] Classes travadas no começo? Proposta do usuário: só Guerreiro; Arqueiro no 30; Mago no 40; Bruxa depois (a discutir). **Decisão (09/11): não travar** — as 4 classes livres desde o início.
- [ ] Níveis totais e ritmo do companheiro: draco hoje no 31 — trazer um pet antes, com evolução e outros pets.
- [x] Bobo da praça (Tião, perto da fogueira do Porto): 3 desafios **diários** (goblins, melecas/ratos, cascas) — cada um uma
  vez por dia (horário de Brasília). `Content/quests_diarias.json`; campo novo `daily` nas missões.

**Missões e loot**
- [x] Menos "mate caranguejo": provas dos mentores agora pedem esqueletos de goblin, mistura, ou "traga 2 retalhos" (mago);
  "Dunas Infestadas" aceita caranguejos de cal e goblins. (`Tools/gerar_tomos.py` atualizado.)
- [x] Cascas de Cal invocam um chefe: **Altar das Cascas** (Costa, a oeste, abaixo das Dunas) — 15 cascas chamam a Rainha das
  Cascas (nível 9, veneno, cuspe em leque, pisão), que larga raro garantido e tem 8% de item de história. Objeto novo `altar`.
- [x] Loot da Costa: os itens de história acima são os "raros que demoram" (3–5% em chefes/raros). Falta rever se o dourado dá raro cedo demais.
- [x] Baús só abrem com a área limpa (nenhum monstro vivo a 11 m; aviso "limpe a área").
- [x] Horta: cara de fazenda (espantalho, feno, regadores); sementes e **Regador de Latão** na loja da Rosa (rega = cresce 30% mais rápido).
- [x] Necessidade de comprar: lojas com itens que fazem diferença (09/11: arma/armadura comprada vem **incomum, com 1 afixo sorteado** — cada compra é diferente; a loja avisa "vem com 1 afixo sorteado"). Preços: conferir jogando.

**Monstros (urgente)**
- [x] Revisar TODOS os monstros com ataques próprios (feito 08/11, 19:20 — ver `Docs/STATUS.md`, "Ataques com identidade"):
  caranguejo anda de lado, pinçada lateral e areia em leque; espadachins com golpe atrasado (souls), estocadas avançando e
  bate-e-volta; goblins com punhaladas em sequência, três flechas e chuva no jogador; 10 chefes do midgame com golpe assinatura.
- [x] Olho Alado na Galeria Funda: paira, carrega a íris e dispara um raio em linha reta (4 no mapa).
- [x] Sons próprios por ataque/habilidade (09/11). Modelos dos chefes do midgame: ver seção 3/4.

**Sons (08/11, 19:50)**
- [x] Ambiente por região em camadas, sem trilha (estilo MU): mar + gaivotas + vento (Costa), sinos/vozes/ferraria ao longe
  (Porto), pássaros + grilos (Vale), floresta + criaturas (Mata), vento forte (Cânions/Geada), sapos (Brejo), gotas e eco
  (dungeons), lava. Laços no lugar: forja martelando, caldeirão, fogueira. `Tools/gerar_ambiente.py` (CC0, por código).
- [ ] Sons baixados de bancos gratuitos (freesound etc.): a rede deste ambiente não alcança esses sites — se quiser, baixe e
  coloque em `Client/Assets/Sounds` com o mesmo nome (ex.: `amb2_mar.wav`) que o jogo usa no lugar.

**NPCs e mundo**
- [x] NPCs no manequim, roupa encaixada por papel, alturas e larguras variadas (mulheres menores), cabelo; NPCs vivos
  (ferreiro martela com faíscas, agricultora mexe na terra, arcanista conjura, viram para o jogador, acenam, gesticulam).
  Quadro de contratos virou quadro de verdade. Espectrais translúcidos.
- [x] **Partículas sutis** (09/11: faíscas em risco no golpe, pedrinhas e poeira no golpe pesado, ar por lugar — folhas,
  areia, neve, brasas, esporos, cinza — e poeirinha brilhante; `Client/AmbientParticles.cs`): poeira no pé ao correr, faíscas ao golpear, pedrinhas, folhas e poeira no ar, luz.
- [x] Arco "voando" fora do corpo depois do tiro (corrigido: volta às costas em 0,3 s).

## 13. Teste do usuário, 3ª rodada (08/11, 20:48)
- [x] Mago (bruxo): barba branca. Arqueiro: queixo pontudo e nariz. Bruxa: sem tiara, franja fora do rosto.
- [x] Cenário ao fundo (estilo CS 1.6): montanhas/serras/silhuetas no horizonte de cada mapa, nada de campo infinito.
- [x] Contorno leve nos objetos que dá para coletar/usar (erva, árvore, veio, baú) quando estiver pronto para interagir.

## 14. Teste do usuário, 4ª rodada (08/11, 22:06–22:20)
Feito (22:40):
- [x] Lobby: fundo novo (ruína com arcadas, tochas com brasas, céu noturno com lua, piso igual ao da vila); personagens de frente.
- [x] Barba do mago abaixo da boca. Capuz no estilo da foto de referência (moldura no rosto com debrum dourado) e capa de tecido
      mole (simulada) que balança com o andar, presa nos ombros com laço no pescoço; sem saia em cone.
- [x] Erva já colhida: o contorno some, a moita encolhe e o aviso diz "esgotado (volta em X min)".
- [x] Monstros batem mais no mundo real (×1,6, e a diferença de nível pesa mais). Guerreiro nível 1 com 284 de vida é a fórmula
      atual (180 + 14×nível + 9×Vitalidade) — rever junto com a calibração.
- [x] Disparo do Arqueiro gasta 9 de Foco (antes 4): atirando sem parar o Foco acaba em ~7 s.
- [x] Golpe de monstro (bote e área) não atravessa árvore/parede.
- [x] Mundo menos estourado (exposição, sol e luz ambiente mais baixos).
- [x] Andar a 2,25 m/s; animação de andar/correr casada com a velocidade (pé não escorrega); sombra colada no pé.
- [x] FPS: monstros a mais de 60 m não são desenhados nem animados (o log mostrava 60 FPS numa RTX 4090 com 87 monstros).
- [x] Ícone da poção voltou à arte do usuário (o frasco pedido era o da vida).
Falta:
- [ ] **Mira**: voltar como estava antes (não achei a versão antiga aqui; preciso da pasta de backup ou da descrição do que mudou).
- [x] Mago (09/11): capuz redondo (raios iguais, sem ovalar) e túnica de pano mole (`ClothSkirt`) caindo reta do peito até a
  canela, solta do corpo; as pernas empurram o pano ao andar.
- [ ] **Padrão de roupas**: cortes reutilizáveis (túnica, manto, capa com/sem capuz, armaduras) → variantes por cor/forma/efeito,
      com passivas; cada classe com suas armaduras e peças, itens coloridos e "vivos" (estilo MU).
- [x] **Regra de texturas**: todo objeto novo nasce com textura (pedra, madeira, metal, tecido, couro, telha, reboco) de um
      atlas pequeno compartilhado (leve), nunca cor lisa.
- [x] Mapas ambientados e cheios (não "borrão de massinha"): objetos, trilhas, cercas, ruínas, vegetação, com a mesma
      qualidade do chão da vila em cada tema.
- [x] Monstros mal distribuídos: nascem numa região de 16 m em volta do ponto, no lugar mais longe dos outros (bando junto, sem empilhar).
- [x] **Tomos por classe**: Esfera (Guerreiro), Livro (Mago), Grimório (Bruxa), Pergaminho (Arqueiro), cada um com ícone próprio; outra classe vê "só para X" em vermelho (o servidor já recusava). Falta: requisito de atributo (estilo MU).
- [x] Aura de subir de nível: chamas douradas serrilhadas saindo do chão (shader), luz e faíscas.
- [x] **Inventário/personagem** no estilo das referências (Diablo IV / MU): bonecos com espaços de equipamento em volta, grade,
      atributos com "+", dica do item rica.
- [x] Coro de igreja (graves + agudas, eco de catedral) bem baixo em todos os mapas (`amb2_coro`).
- [x] Partículas e interação com o mundo (poeira, folhas, faíscas) — ver acima.

## 15. Teste do usuário, 5ª rodada (08/11, 22:57) — feito
- [x] Inimigos humanoides feitos do manequim (goblin baixinho/cabeção/orelha pontuda/nariz, chefe goblin com chifres e
      presas, saqueador com capa mole, acólito/necromante magros de manto, troll e gigante largos). Vitrine "humanoides".
- [x] Guerreiro forte no próprio corpo (malha engrossada; sem camada que parecia armadura).
- [x] Dica do item quase imediata; barra de vida dos inimigos não fica mais preta.
- [ ] Caracterização de cada inimigo (o "grande diferencial"): roupas/armas/efeitos próprios por espécie, mais braços, chifres etc.
- [x] Guerreiro sem drop de armadura (09/11): armadura/elmo/escudo dos caranguejos de cal e goblins 7%→10% e 6%→10% (goblin
  pode dar Cota de Malha), caranguejinho 3%→5%; e os conjuntos de história nos chefes.

## 16. Teste do usuário, 6ª rodada (08/11, 23:50)
- [x] **Árvore**: "Cortar" e "precisa de machado (missão da batedora Lia)".
- [x] (1ª passada) **Janela do personagem estilo Diablo IV/MU** (I): personagem 3D no meio, espaços em volta, números, atributos com "+", mochila 10×4; barra de baixo com moldura. Falta: ícones das habilidades, menu principal, mapa/diário no mesmo estilo.
- [x] **HUD, menu, distribuição de pontos, inventário e equipamento no estilo das referências (Diablo IV / MU)**:
      painel de personagem com o boneco no meio e os espaços de equipamento em volta, grade da mochila, atributos com "+",
      barra de baixo com orbes/frascos, atalhos e menus bonitos, organizados, visíveis e fáceis de clicar.
- [x] **Itens vivos e brilhantes**: brilho pulsando com faíscas no ícone; no chão, feixe alto, luz e brilhos subindo (raro+).
- [x] **Arco fora do corpo**: agora colado nas costas.
- [x] **Porrete/arma atravessa a cabeça**: andando/correndo a arma vai apontada para trás e para baixo.
- [ ] **Movimento estranho** ("deslizando no gelo"): braços e pernas não acompanham o deslocamento; casar passo e velocidade,
      inclinar ao correr, virar com peso.
- [x] **Aura de nível mais suave** (bordas macias, mais transparente, sem corte no chão).
- [x] **Monstros em todo o mapa**, inclusive cantos (lado direito vazio); **quanto mais longe da cidade, mais nível**
      (o mesmo bicho com nível 5, 7, 9).
- [x] **Preencher o mapa** (variar, sem lotar): objetos, vegetação, ruínas, trilhas.
- [x] **Profundidade** (1ª passada): fileiras de silhuetas do tema além da borda sumindo na névoa; perspectiva aérea mais forte.
- [x] **Capas saindo do pescoço**: gola de pano e capinha nos ombros; o pano desvia também de braços e pernas.

## 17. 09/11, 00:20–00:32
- [x] Corrida voltou a ser a original (a tentativa de casar o passo deixou o boneco "correndo como o Flash").
- [x] Esqueleto de Goblin (nível 2, mais fraco) no Areal junto dos caranguejos.
- [x] Árvores por lugar (1ª passada): coqueiros e arbustos secos na Costa; casca com textura de madeira. 2ª passada (09/11): bétula no Vale/Geada, salgueiro no Brejo, acácia nos Cânions, cipreste na Acrópole/Véspera.
- [x] Partículas no pé (poeira/areia/neve/lama pela cor do chão), mais fortes correndo.
- [x] "Movimento no gelo": parar e virar com peso (09/11) — servidor acelera (~0,2 s) e freia (~0,13 s) em vez de mudar a
  velocidade na hora; o corpo inclina ao arrancar/frear e na curva; giro mais pesado. Ritmo da corrida igual. **Testar no PC.**
- [x] Capa, roupa e arma com física e sem atravessar o corpo (braços e pernas também). (09/11: capa e túnica do mago são pano simulado que desvia de braços/pernas; arma na mão é empurrada para fora do tronco, cabeça e pernas a cada quadro, depois da pose final — `KeepWeaponOutOfBody`. Armadura rígida segue os ossos; se algum pedaço ainda entrar no corpo em pose específica, anotar qual.)
- [x] **Bestiário planejado** (usuário): morcego, rato, demônio, fantasma, aranha, cavaleiro, dragão, vampiro, gênio, saci,
      wendigo, minotauro, chupa-cabra, lobisomem, zumbi, múmia, verme, insetos, criaturas fantasmagóricas,
      planta carnívora, a Morte, ogro da montanha, monstro meleca, alien, harpia, anjo, cão de 3 cabeças, anões, sapos gigantes.
      Já existiam: morcego, aranha, cavaleiro, fantasma, verme/sanguessuga, besouro/escorpião, harpia, minotauro.
      **Feito (09/11)**: 25 criaturas novas em `Tools/gerar_bestiario.py` (rato, ratazana, meleca, planta carnívora, saci, anão,
      zumbi, lobisomem, chupa-cabra, múmia, verme gigante, alien, sapo gigante, vampiro, a Morte, wendigo, ogro da montanha,
      anão da montanha, demônio, cão de 3 cabeças, dragão, anjo, gênio…) espalhadas pelos mapas pela faixa de nível
      (`povoar_mapas.py`). Planta não anda; meleca cospe gosma que deixa lento. Vitrine: `--vitrine bestiario|bestiario2`.

## 18. 09/11, 02:18 — "parece versão chinesa; falta textura, vida, personalidade; barra feia, alinhar, AAA"
- [x] Kit de interface com relevo (Tools/gerar_ui.py): moldura de bronze dos atalhos, placa de ferro rebitada da barra, aro dos
      frascos, ícones pintados de todas as habilidades (hab_*.png, cor por classe, brilho) — fim das siglas "TE/PF/OC".
- [x] Barra de baixo alinhada aos frascos; XP com borda de bronze e brilho.
- [x] Regra de textura aplicada no mapa inteiro (MapView.TexturizeAll: madeira/pedra/reboco/ardósia/terra pela cor).
- [x] Sem retângulos pintados no chão dos mapas abertos (zonas e trilhas antigas viravam "quadrado escuro").
- [x] Props com história nos mapas abertos (`povoar_mapas.py` STORY): barcos encalhados, redes secando, ossadas (às vezes com
  espada largada), acampamentos abandonados com fogueira apagada. Falta: NPCs trabalhando e luz/cor por lugar.
- [x] Janelas (diário, mapa, loja, atributos, diálogos) com o mesmo acabamento da janela do personagem.

## 19. 09/11, 20:34 — fôlego, morte em cinzas, caranguejos com território, companheiro, Costa reorganizada, grama, HUD
- [x] **Fôlego** (barra verde fina sobre as habilidades): correr 13/s, esquivar 24, golpe básico 7, habilidade 12; volta 34/s
      depois de 0,65 s sem gastar (metade com a guarda erguida). Zerou = "sem fôlego": não corre até 33. Correr agora vale em
      combate (o custo é o limite). O recurso do Guerreiro passou a se chamar **Fúria** (laranja) para não confundir.
      Servidor: `GameServer.Stamina.cs`; só no mundo vivo. Testes: `FolegoECaranguejosTests`.
- [x] **Morte**: ajoelha e, 1,3 s depois, vira cinza que o vento leva ("estalo do Thanos"); não afunda mais no chão.
      Monstros também se desfazem em cinza (1 s). `Effects.Disintegrate`.
- [x] **Círculo seguro da Costa no portão** (0; 98,4, raio 5,5): quem chega do Porto (0; 96) e quem renasce já está dentro.
- [x] **Costa por territórios** (`Tools/reorganizar_costa.py`, idempotente; planta: `Docs/planta_costa_antes.png` →
      `Docs/planta_costa_depois.png`, gerada por `Tools/planta_mapa.py`): saíram os 50 monstros espalhados ao acaso; entraram
      bolsões com identidade: Praia dos Barcos (Cal 3 + ratos), Rochedo do Cal (**Caranguejo de Pedra 6**), Praia dos Destroços
      (**Ermitão do Barril 7**), Areal Fundo (**Caranguejo Escavador 9**), Acampamento da Vigília (saqueadores 11–13, depois da
      ponte), Praia da Baía (Cal 10) e **Enseada do Tirano** (sudeste, **chefe de campo nível 12**, 15 min para voltar).
- [x] **Mecânicas novas** (`MonsterAttackDef`: `self_guard_mult`, `guard_frontal`, `burrow`): Pedra fecha a carapaça (dano ×0,1),
      solta 8 espinhos em volta e levanta exposto (×1,9); Ermitão entra no barril (de frente ×0,15, pelas costas normal), investe e
      **arremessa barril explosivo** (marca no chão, queimadura); Escavador some no chão, o monte de terra corre até você e ele sai
      embaixo (atordoa); Tirano: varrida, esmagar, guarda de pinça (escudo de frente + estouro), 2ª fase com Maré Furiosa e filhotes.
- [x] **Missões**: Pedras que Andam (Lia, nv 5), Barris do Naufrágio (Lia, nv 6), O Chão que Morde (Otávio, nv 8),
      O Tirano da Maré (Otávio, nv 11).
- [x] **Slot de companheiro** (janela do personagem, I): botão por companheiro (aceso = com você), "Guardar". O Pip não vem mais
      de começo: missão "Uma Luz no Caminho" (Lia, nv 1, 5 Caranguejinhos) dá o vagalume; ele só dá dicas quando está com você.
      A escolha vale no próximo login.
- [x] **Grama**: touceiras de folhas em geometria (base escura, ponta clara, vento só na ponta, some com a distância) no lugar dos
      cartões recortados ("parecia papel encaixado"). Moitas idem.
- [x] **Interface**: molduras com textura (`Tools/gerar_moldura.py`: ferro escuro, friso dourado, esmeralda nos cantos), botões de
      metal, fontes Cinzel (títulos/botões) e Alegreya Sans (texto), ambas OFL em `Client/Assets/Fonts`.
- [ ] Depende de jogar: sensação do fôlego (números acima), leitura dos caranguejos novos em jogo, barril/monte de terra.

## 20. 09/11, 21:37 — dano pelo equipamento esperado, chão novo, rochas, árvores ao vento, textos na moldura
- [x] **Dano**: o golpe do monstro é calibrado para quem usa a armadura do nível dele (corpo + elmo ≈ 30 + 15/nível). Com ela,
      igual a antes; de cueca, golpe cheio (×1,23 no nível 2 até ×1,7 no fim); armadura acima do nível, menos. Tutorial (nível 1)
      fica igual; quem passou 6+ níveis do monstro deixa de precisar. `GameServer.Map.cs: GearExpectation`.
- [x] Fôlego: golpe básico custa 5 e a barra volta 0,45 s depois — farmar no clique não cansa; habilidade/esquiva/corrida cansam.
- [x] **Chão** (TerrainShader refeito): textura sem ladrilho (amostragem com deslocamento por ruído), 3 tons em manchas grandes,
      relevo macro de dunas só na luz, marcas de vento finas com direção variando e só em manchas, **estradas pintadas no chão**
      com borda irregular (máscara do mapa), sombra de contato junto de árvores/rochas/muros, chão pisado no acampamento e na área segura.
- [x] **Rochas** de verdade (pedra lascada gerada por ruído, textura nos 3 eixos, areia no topo) no lugar das caixas.
- [x] **Árvores e pinheiros balançam** com rajadas de vento.
- [x] Textos dentro da moldura: registro e ganhos quebram a linha; companheiro vazio com texto curto (alargava a janela e
      escondia os números dos atributos). Ao renascer, a câmera vira para dentro do mapa.

## 21. 09/11, 22:48 — interface séria (prancha "Proposta visual")
- [x] Ferro forjado no lugar de ouro/esmeralda (`Tools/gerar_moldura.py`): janelas com cantoneiras rebitadas, painéis finos,
      botões de ferro, placa da barra de baixo ligando os potes, berço de ferro com garras atrás de cada pote, faixa da bússola.
- [x] Serifa clássica EB Garamond (OFL) nos títulos, nomes sobre os monstros, nome da região, bússola e números dos potes.
- [x] Nome sobre o monstro em branco com barra fina de aro de ferro; bússola com marcas de 15° e letras em serifa.
- [x] Janela do personagem, mochila, loja, atributos e quadros de item (StonePanel) com a mesma moldura de ferro.
- Potes de vidro mantidos (pedido), agora com aro de aço.

## 22. 10/11, 00:00 — arte da Costa + lote 3 integrados (pacote `CinzasDaMare_Costa_Arte.zip`)
- [x] Copiado `Client/` do pacote (backup: `backups/arte_antes_costa_2026-11-09/`, `backups/criaturas_lote2/`); fichas em
      `Docs/FICHA_COSTA_ARTE.md` e `Docs/FICHA_LOTE3.md`, prévias em `Docs/previas_lote3/`, scripts do Blender em `Tools/criaturas/`.
- [x] Chão da praia: `ter_areia` nova + lajes de cal `ter_costa_cal` no TerrainShader (sem repetição, tons, trilhas).
- [x] Kit da Costa no MapView: paredão de cal nos penhascos, rochas, coqueiros/pinheiros-mansos/arbustos, grupo de pinheiros,
      serras no horizonte, capim/flores/seixos/algas/troncos/conchas em MultiMesh, ravina e ponte do kit, **lagoa** (obstáculo
      novo `lago`, em `Tools/reorganizar_costa.py`).
- [x] Lote 3: caranguejos novos (`criaturas/`, uma cor por espécie, contorno escuro), **Rei das Pinças** (sweep e urro de 2ª
      fase), **javali** (investida), Ermitão com arremesso de barril próprio + barril novo, Caranguejo Dourado = Cal tingido de ouro.
- [x] Goblin **Chefe** e **Arqueiro** com os modelos novos (corpo próprio no manequim, poses de ataque/morte do jogo);
      goblin pequeno e esqueleto ficaram como estavam (pedido do usuário).
- [x] Acampamento goblin das Dunas com tendas goblin, paliçada (com colisão) e totem; **farol** do lote 3; **entrada da Galeria**
      (paredão com boca de mina e colisão nas laterais).
- [ ] Variações sugeridas na ficha do lote 3 (Goblin Batedor, Atirador, Esqueleto Corredor etc.) — ids novos no servidor.

## 23. 10/11, 00:20 — dia e noite + modo deus (F1)
- **Feito:** relógio do mundo no servidor (um dia = 40 min reais; sol nasce às 6, põe às 18; a hora segue o relógio real
  entre reinícios). Cliente: céu, sol/lua, névoa, estrelas e entardecer misturados a partir do clima de cada tema; à noite
  a lua assume a luz (fria, com sombra) e chamas/lanternas ficam 70% mais fortes. Hora embaixo da bússola. Dungeon, gruta
  e casa não mudam.
- **Feito:** modo deus (F1), só com o servidor aberto com `--deus` (o `Jogar.cmd` liga; `Jogar.cmd -SemDeus` desliga;
  `--deus alice,bruno` restringe). Nível, XP, moedas, pontos, itens (qualidade/quantidade), chamar monstros (nível e
  quantidade), derrotar os de perto, invulnerável, velocidade 1–3×, zerar recargas, hora e ritmo do tempo, viajar para
  qualquer mapa/dungeon, liberar viagens/montarias/asas/companheiros, evoluir classe. Tudo no log `servidor.deus`.
  Testes: `ModoDeusTests` (3).
- **Ideias para a noite (não feitas):** bichos noturnos/raros só de noite, mais XP à noite, lanterna equipável, NPCs indo
  dormir, sons de grilo/coruja.

## 24. 10/11, 01:00 — céu vivo + lote 2 no Porto (casas, portão, forja, banca, baú, armas, itens)
- **Feito (céu):** nuvens andando numa cúpula presa à câmera (não mexe no céu de fundo, então o reflexo não é recalculado),
  sombra das nuvens passando no chão (shader do terreno), gaivotas/pássaros circulando alto (somem à noite).
- **Feito (Porto):** `Tools/reorganizar_porto.py` (idempotente, backup em `backups/map_porto_antes_lote2.json`):
  Taverna Grande e Casarão da Cidade com **térreo visitável** (colisão das caixas de `Tools/colisao/*_colisao.glb`,
  via `Tools/caixas_colisao.py`; andar de cima e telhado somem quando o jogador entra), sobrados e casas pequenas no lugar
  das casas antigas, Portão Fortificado na saída para a Costa (grade levantada), cerca na horta. Forja e banca de mercado
  com os modelos do lote 2. Baús do mapa com o baú do lote 2 (tampa abre na dobradiça).
- **Feito (itens):** armas e escudos do lote 2 na mão e no chão (espada/machado/cajado/escudo, 3 faixas pelo modelo do
  item); 15 itens de chão (poções, joias, carapaça, pinça, presa, osso, erva, minério, seda, essência, moedas, bolsa).
- **Falta:** 2º andar do casarão (o servidor só entende chão plano: precisa de "andar" no servidor); NPCs dentro dos
  prédios (marcadores `spawn_npc_N` do casarão); casarão em ruína e salão vazado (áreas de monstro); ponte quebrada →
  reparada por missão; os 3 golpes do Meshy; móveis de interior (não vieram no lote).

## 25. 10/11, 01:30–03:00 — vila viva, vozes, ícone de missão, pacotes de arte, mochila MU, HUD v2
- **Vila (Porto):** trilhas de lajes e de terra (shader com borda irregular), manchas de grama com capim e flores,
  canteiros com borda de madeira na frente das casas, vasos nas portas, pinheiros com textura no lugar das árvores lisas.
  Texturas de parede/calçamento/madeira com desgaste (Cal Lote 2) e prédios do lote 2 refeitos com PBR.
- **Ícone de missão:** "!" (novo) / "?" (entregar) grande, losango girando, halo pulsando e luz de verdade na cabeça do NPC.
- **Vozes:** `Tools/gerar_vozes.py` (síntese de formantes, CC0): olá, hum, aham, haha, risada, bravo, hein, oh, ai — conjunto
  masculino e feminino; cada NPC com altura própria (`Main.NpcPitch`); toca ao chegar perto (aceno), ao abrir conversa
  (gesticula), ao comprar (ri), ao aceitar/entregar missão e quando recusa (bravo/dúvida).
- **Pacotes de arte integrados:** Cal Lote 2 (14 pacotes de NPC vestidos no manequim, fogueira completa, fumaça do
  acampamento goblin, feixe do farol, gaivotas/siris/vento na praia, ossada gigante na Enseada do Tirano, goblins com clipes
  de acampamento, texturas gen_* 1024) e Atualização 10/11 (cabeças de bicho Meshy na evolução, ícones pintados dos itens e
  armas, mapa ilustrado da Costa na tecla M, cenário dos estudos: torre partida, trilhos+vagonete, carroça, blocos de cal,
  estandarte, tochas). Texturas de .glb limitadas a 1024 px (2048 no Alto) com mipmaps (`GlbCache.Shrink`).
- **Mochila e baú estilo MU:** grade 8×8, cada item ocupa o seu tamanho (espada 1×3, machado/arco 2×3, cajado 1×4,
  armadura 2×3, elmo/escudo 2×2, talismã 1×2, o resto 1×1); arrastar mostra onde cai (verde/vermelho); o servidor valida
  (`GameDatabase.Grid.cs`). Mochila antiga é arrumada ao entrar (o que não couber vai para o armazém). Baú/armazém abre à
  esquerda com a mochila à direita; arrasta entre os dois; botão direito guarda/tira. Testes: `MochilaGradeTests`.
- **HUD v2 (prancha do dono):** barra de baixo `ui_barra_inferior` com orbes (`OrbeHud`), XP com moldura, quadros de
  habilidade `ui_slot_hab`; alvo `ui_barra_alvo`/`ui_barra_chefe`; placa da área; janelas com `ui_moldura` + aba.
- **Não feito / próximo:** mar da Costa (`MarDaCosta`: precisa decidir o lado do mar), `CenaPrintCosta` (duplicaria o
  lago), goblins sentados/mexendo a panela perto do fogo, 2º andar do casarão, ponte que conserta por missão, golpes do Meshy,
  variações do lote 3, tela de Atributos/loja/diálogo no estilo v2 completo, tooltip `ui_tooltip`.

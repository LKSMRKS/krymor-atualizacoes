# STATUS (atualizar a cada entrega)

**Entrega atual:** E8 — protótipo jogável "Vale das Cinzas" (detalhes: `Docs/PROTOTIPO_E8.md`). **Atualizado:** 08/11/2026.
**Mudança de prioridade (pedido do usuário, 07/11/2026):** ciclo de farm jogável primeiro (classe → explorar → matar →
pegar loot → equipar → subir de nível → inimigos mais fortes), com câmera em 3ª pessoa e bonecos 3D simples.
Decoração e modelos detalhados vêm depois. Ver `Docs/DECISOES_OFICIAIS.md`, seção 7.

## Rodada 3 do teste (08/11/2026, 20:30) — **entrega atual**
- **Monstros vivos** (todos os mapas do mundo; chefes de fora): nascem num ponto aleatório da região (bandos ~2,5 m, os
  demais ~7 m), andam à toa, param, alguns dormem ("z z"; só acordam com quem chega a ~3 m ou com barulho), cada um
  agra sozinho (o bando só vem quando um apanha, e só os vizinhos a 6 m), perseguem 1,7× mais longe. Efeito de surgir
  (coluna de luz, estilo MU). `GameServer.Map.cs` (Scatter/Wander), `WorldContent.LivelyMonsters` (só o mundo real).
- **Loja + mochila lado a lado**: abrir loja abre a mochila à direita; botão direito no item vende; arrastar para a loja
  vende. **Arrastar**: mover na mochila (troca de lugar), mochila ↔ corpo, soltar fora das janelas = jogar no chão
  (`DropItemIntent`, `MoveSlotIntent`). Diálogo esconde missões concluídas (mostra quantas).
- **Elmo encaixado** (escala/altura na cabeça do manequim). **Andar × correr**: andar 3 m/s; Shift liga a corrida e ela
  fica ligada enquanto anda (`MoveIntent.Walk`).
- **Atributos estilo MU** para todos: Força, Agilidade, Vitalidade, Energia (+ na tela C). Armadura/elmo/escudo servem a
  qualquer classe, mas pesam (peso = armadura × 0,12; escudo × 0,08): sem Força, esquiva recarrega mais devagar e o
  passo fica pesado (até −25%). Agilidade encurta a esquiva; Energia (fora a principal) regenera recurso. Migração v5
  (só acrescenta colunas). `Shared/Content/Atributos.cs`.
- **Mundo cheio**: `Tools/povoar_mapas.py` (chamado pelo gerador): estradas de terra ligando acampamento, pedras de
  viagem e portais; árvores por região (copa redonda no Vale/Mata, pinheiro na Costa/Geada, seca no Brejo/Véspera),
  rochas e cercados de pasto, todos com colisão; capim, moitas, flores e galhos (sem colisão) proporcionais à área.
  Corrigido: a passagem Vale → Mata ficava fechada por muros (aberta uma brecha no muro das Ruínas).
- **Guerreiro mais forte**: camada de "músculo" no peito, braços e pernas (as outras classes um pouco menos finas).
- **Executado:** 287 testes; cliente 0 erros; capturas (loja+mochila, elmos, corpos, Costa). **Não testado:** arrastar
  com mouse de verdade; desempenho com mapas cheios numa placa real.

## NPCs vivos e sons de ambiente (08/11/2026, 19:50)
- NPCs no manequim (`VisualFactory.NpcMannequin`): gênero pelo nome, roupa por papel (`npc:papel:variante`), cores/altura/
  largura estáveis por id, cabelo; comportamento em `MapView.UpdateNpcLife` (trabalho, olhar em volta, virar e acenar).
  Quadro de contratos é um quadro. Falar com NPC: 3 m.
- Ambiente em camadas por tema (`Sfx.AmbienceFor`), laços 3D em forja/caldeirão/fogueira, caldeirão refeito.
- **Executado:** 278 testes; cliente 0 erros; capturas do Porto. **Não testado:** som (ambiente sem placa de áudio aqui).

## Ataques com identidade (08/11/2026, 19:20)
- Mecânicas novas de ataque de monstro (campos opcionais em `behaviors*.json`): `hits`/`hit_interval_ms`/`step_m`
  (sequência de golpes que avança), `windup_jitter_ms` (golpe atrasado: a marca enche e segura um tempo aleatório),
  `backstep_m` (bate e volta), `count`/`spread_deg` (projéteis em leque), `at_target` (cai em cima do jogador) e a forma
  `beam` (raio reto; marca em faixa; parede corta).
- Costa/Galeria: caranguejo de cal (`crab_side`: anda de lado, pinçada lateral, bote, areia), caranguejinho joga areia,
  couraçado com pinçada dupla, saqueador (talho atrasado, 3 estocadas avançando, golpe-e-recuo), acólito (3 bolas em leque,
  brasa no jogador), capataz (pancadas em marcha), Rei das Pinças (chuva de areia, pinça dupla), escavador (`digger`:
  picaretada atrasada, terra em leque), sentinela (pisões em marcha), guardião (lanças em leque, raio de cal na 2ª fase),
  goblins (punhaladas em sequência, recuo, três flechas, chuva no jogador, giro do chefe), larvas mordem e recuam.
- **Olho Alado** (novo, nível 11, Galeria Funda ×4): paira, carrega a íris e dispara raio de 3 queimaduras; pisca e foge
  de perto. Visual próprio (`Client/Visuals/EyeVisual.cs`), feixe de luz no cliente.
- Midgame (gerador): lobos mordem duas vezes e recuam, javalis recuam, aranhas jogam teia, brutos com pisões em marcha,
  fantasmas com leque e assombro no chão; elites `elite_knight` (esgrima) e `elite_beast` (feras); 9 chefes com golpe
  assinatura (`boss_root`, `boss_bell`, `boss_necro`, `boss_draugr`, `boss_giant`, `boss_golem`, `boss_heart`, `boss_chimera`).
- Cliente: poses por família de golpe para todos os humanoides (antes muitos ataques não tinham pose), cada golpe da
  sequência bate de novo, flechas de goblin, torrões de areia/terra, teia/gelo, raio com luz.
- **Executado:** 278 testes (6 novos em `AtaquesComIdentidadeTests`); cliente 0 erros/0 avisos; capturas do olho (vitrine e
  luta real na Costa com servidor de teste). **Não testado:** sensação de dificuldade em jogo real (ajuste fino de dano).

## Bruxa, Arqueiro, manequim e draco com esqueleto (08/11/2026); detalhes: `Docs/CLASSES_BRUXA_ARQUEIRO.md`
- Duas classes novas (escolha no lobby): **Bruxa** (cura com as mãos, sem cajado: Toque Alquímico, Barreira de Éter, Aura
  Primal na tecla R; evolução Matriarca do Éter) e **Arqueiro** (Tiro de Emboscada, Passos Fantasma, Olhar do Caçador na R;
  evolução Batedor das Sombras). Mentoras Hazel e Kaelen no Porto; evolução no nível 20; caminhos do nível 50.
- Itens próprios do nível 1 ao 60 (talismãs, arcos, vestes, gibões), lendários, loot, lojas e recompensas de missão
  em 4 partes (`@classe:guerreiro|mago|bruxa|arqueiro`).
- **Todos os jogadores usam o manequim** (corpo masculino; Bruxa feminino), de roupa de baixo; roupas são camadas da malha
  que acompanham a animação; arma/elmo/escudo presos aos ossos; poses de golpe/magia/tiro feitas em código.
  `--boneco-classico` volta ao boneco de formas simples. Pequeno draco de companhia usa o modelo do usuário com esqueleto.
- **Executado:** 268 testes (7 novos); cliente 0 erros/0 avisos; vitrine (4 classes, poses, draco) e cliente real
  (Bruxa e Arqueiro criados no lobby, luta na Costa). **Não testado:** dois jogadores reais com cura/barreira um no outro.

## Acabamento visual, etapa 1: luz e atmosfera (08/11/2026)
- Renderizador **Forward+** (antes: compatibilidade/OpenGL); se o vídeo não tiver Vulkan, o Godot volta sozinho ao OpenGL.
- Céu de entardecer próprio (degradê, disco e halo do sol, nuvens), clima por tema (cidade, praia, vale, mata, cânion,
  brejo, Véspera, neve, lava, acrópole, gruta), sol baixo e quente + contraluz frio, tom ACES.
- Sombra de contato (SSAO), luz rebatida (SSIL), névoa com volume (halos nas lanternas), reflexo em poças, sombras suaves;
  relevo (mapas de normais) no chão e nas paredes; lanternas e chamas mais fortes; marcador de NPC em losango dourado.
- **F10** alterna gráficos alto/médio/baixo (grava em `user://graficos.cfg`; ou `--graficos baixo`).
- **Executado:** cliente 0 erros/0 avisos; capturas Forward+ (cidade, forja, Costa, Mata, Cripta) e OpenGL (Costa).
  **Não medido:** quadros por segundo numa placa de vídeo real (aqui a renderização é por software).

## Expansão até o midgame M01–M06 (08/11/2026); detalhes: `Docs/MIDGAME_M01_M06.md`
- Níveis 17–60: 7 regiões novas (Mata → Acrópole) + 7 dungeons + casa do jogador; chefes de passagem (selo por personagem).
- Ofícios (forja, bancada, caldeirão, panela), mineração/coleta, horta, armazém comunitário, casa com baú, refeição,
  mana do mago, montaria (V), asas (G), companheiro (B), lendários, evolução do 50; 104 missões (cadeias + contratos).
- HUD novo (potes de vidro, XP sob os poderes), sons em tudo, corpo-base comum e kit visual do pacote v1.
- Banco v4 (só acréscimos). Limite de quadro de rede 64 → 256 KB (o mapa da Mata passava de 64 KB).
- **Executado:** 261 testes; cliente 0 erros/0 avisos; cliente real (Mata, montaria, draco, selo, forja, armazém).
  **Estimado:** ritmo 1–60 pela simulação (≈ 63 h só com abates). **Depende de você jogar:** sensação 17–60, chefes, horta.

## Direção consolidada (08/11/2026); detalhes: `Docs/MUNDO_E9.md`, seção 12
- Faixas 1–5 / 6–10 (+ Galeria Funda, agora entrada na Gruta da Costa) / 11–16 Vale; 17–20 vem com o próximo mapa.
- Armas com estilo (machado interrompe, lança direcional) e habilidade R; evolução Campeão/Arcanista (nível 20 + missão; tecla T).
- Ponte da Ravina e mascate por personagem; barqueiro pago; missões de conversar/interagir; "!" no mapa do mundo.
- Checkpoint na dungeon, primeira conclusão garantida, 2ª fase dos chefes, combos de elite, corrida (Shift).
- Mochila nova estilo RPG (boneco + grade + detalhes).
- **Executado:** 248 testes; cliente 0 erros/0 avisos; capturas (mochila, ponte, barra R/T, barqueiro). **Estimado:** ritmo
  (nível 10 ≈ 3 h, 16 ≈ 8–9 h). **Depende de você jogar:** sensação das armas/2ª fase, Galeria aos 7 sozinho.

## E9 — mundo de farm (08/11/2026); detalhes e valores: `Docs/MUNDO_E9.md`
- Cidade (Porto das Cinzas) + região inicial (Costa do Cal, 5 sub-áreas crescentes, chefe de campo) + Vale (região 2) +
  dungeon por grupo (Galeria Funda). Mapa do mundo, viagem rápida, NPCs, loja, moeda, missões, grupo, atributos, slot de cabeça.
- Personagem novo: nível 1, de cueca, soco/faísca grátis; primeira arma garantida por missão. Antigos: nada retirado.
- Progressão recalculada por simulação (curva crescente, sem o ×8; faixa de poder por nível).
- Banco v3 (só acréscimos): mapa do item no chão, moedas, pontos de atributo, missões. Migração testada numa cópia do seu e6.db.
- **Executado:** 236 testes; servidor e cliente com 0 erros/0 avisos; cliente real com capturas (cidade, NPC, Costa, mapa,
  equipamento, mochila, grupo, dungeon). **Estimado:** ritmo de níveis. **Depende de você jogar:** sensação e balanceamento.
- Pendências que continuam: E6 cenário B; penalidade de morte (decisão sua); sobras 2, 3 e 5 da auditoria; modelos 3D.

## 0. E8 — o que foi EXECUTADO aqui (ambiente de trabalho, Linux)
| Item | Resultado |
|---|---|
| Testes | **208 aprovados, 0 falhas** (os 166 anteriores + 42 novos). Um teste antigo foi atualizado de propósito: a contagem de conteúdo (`ContentTests`: agora 2 classes, 6 habilidades, 12 itens, 5 monstros) |
| Build servidor + host | 0 erros, 0 avisos |
| Build do cliente | **0 erros, 0 avisos** com o Godot.NET.Sdk **4.7.2 real** (pacotes que vêm no Godot 4.7.2 .NET oficial) |
| Cliente rodando | Godot 4.7.2 .NET (Linux, tela virtual): conectou, lobby, criou Guerreiro e Mago, entrou no mapa, lutou (modo demonstração), loot caiu, inventário abriu, 4 clientes ao mesmo tempo sincronizados. Capturas de tela conferidas |
| Migração do **seu e6.db** (cópia) | v1 → v2 sem perda: alice e bruno, níveis, XP, vida, espada equipada e tônicos iguais; backup `e6.db.pre-v1.bak` criado; alice entrou no mapa com a espada |
| Atalho | `Tools/jogar.ps1`: sintaxe verificada (PowerShell 7) e funções testadas; **não** executado no Windows |

**Diferenças do ambiente (importante):** aqui o nuget.org é bloqueado. Para compilar e rodar os testes usei: .NET SDK
8.0.133 do Ubuntu (o seu é 8.0.427), um substituto mínimo do Microsoft.Data.Sqlite sobre a SQLite do sistema e um
executor mínimo de testes no lugar do xUnit (o mesmo código de teste). Esses substitutos **não** foram para o projeto.
No seu PC o build usa os pacotes reais: rode os comandos do fim deste arquivo.

## 0.05 Correções de 08/11/2026 (após relato "o servidor cai só de andar")
- Desconexão ao andar girando a câmera em monitor ≥ 120 Hz (excesso de mensagens): corrigida no cliente.
- Queda do servidor quando uma seta mata um Acólito com bola em voo: corrigida.
- **Log em arquivo**: `logs\servidor-*.log` e `logs\cliente-*.log`; `Tools\logs.ps1` resume; exceção no tick é registrada e o servidor segue.
- Auditoria itens 2–5 corrigidos + monstro encostado que parava de atacar (ver `Docs/AUDITORIA_E8.md`).
- Testes: **224 aprovados**.
- Sobras 1 e 4 da auditoria: marca da Explosão com o raio real para todos; câmera sobe junto de paredes. Testes: **225 aprovados**.

## 0.1 E8 — o que DEPENDE de você jogar
☐ `Jogar.cmd` acha o Godot, compila, abre o servidor minimizado e o cliente (primeira vez pergunta o Godot).
☐ Controles e sensação: câmera (sensibilidade, zoom, encostar nas paredes), andar relativo à câmera, clique/Q/E/Espaço/Tab/F/I.
☐ Combate: preparação dos inimigos legível a tempo de reagir; esquiva salva; "brecha!" dá vontade de contra-atacar; o chefe é justo.
☐ Recompensa: achar o segredo (muralha oeste), abrir baús, primeira melhoria nos primeiros minutos, subir de nível.
☐ Duas janelas (`Jogar.cmd -Quem bruno`): ver o outro jogador, golpes e itens sincronizados.
☐ Desempenho no seu PC (o mapa tem ~400 peças simples e ~14 luzes).

## Histórico (até a E7b-1)


**Marco A (somente Guerreiro), situação em 02/11/2026** — mantida para referência:
**Ambiente do usuário:** Windows, .NET SDK 8.0.427, Godot 4.7.2 .NET (ver `AMBIENTE.md`).
**O Marco A AINDA NÃO está aceito.** A disputa simultânea pelo item (cenário B da E6) continua pendente.

## 1. Validado pelo usuário (executado no PC)
| Entrega | Resultado |
|---|---|
| E2 | `build` 0 erros; 37 testes aprovados |
| E3 corrigida | 0 erros e 0 avisos; 71 aprovados |
| E4 | 0 erros e 0 avisos; 125 aprovados (com a condição já registrada: cópia manual do `balance.json`; causa corrigida na E5) |
| E5 | cliente Godot 0 erros e 0 avisos; 159 aprovados sem cópia manual; jogo com 1 cliente funcionou |
| `Tools/e6.ps1` (corrigido) | executado pelo usuário; abriu o servidor e as duas janelas |
| E6 (manual, parcial) | Alice e Bruno conectaram simultaneamente, movimentos em tempo real nas duas janelas, combate/morte/renascimento testados; após reiniciar o servidor Alice manteve espada, 3 Tônicos e XP |
| **E7a** | **166 testes aprovados**; build da solução com 0 erros e **1 aviso xUnit2000** (`WorldInfoAndCooldownTests.cs:92`); cliente 0 erros e 0 avisos; jogado: controles e ações funcionaram. Avaliação do usuário: **visual ainda muito simples** |

**Observações da captura de tela da E7a** (enviada pelo usuário): o HUD ficou legível (retrato, barras, barra de habilidades, registro), mas com painéis grandes e espaços vazios e siglas (GC, GE, AA, PV) no lugar de ícones; o chão aparecia como lajes grandes azul-escuras com uma elipse escura enorme no centro; o personagem ficava pequeno na tela; os indicadores de orientação e o anel eram vistosos demais; o anel vermelho do monstro continuava no chão depois de ele morrer.

## 2. Aceite E6: situação por cenário (relato do usuário; nada além do que foi dito está marcado)
| Cenário | Situação |
|---|---|
| A. Sincronização entre as janelas | **Parcial:** movimento em tempo real confirmado; combate, morte e renascimento testados. Não relatados: orientação, escudo da Guarda visto pelo outro, vida do monstro igual nas duas telas |
| B. Disputa simultânea pelo item | ☐ **PENDENTE** (não executada) |
| C. Desconexão e reconexão | ☐ Não relatada |
| D. Persistência após reiniciar o servidor | **Parcial:** Alice manteve espada, 3 Tônicos e XP. Não relatados: Bruno, nível, vida, posição, espada não reaparecer no chão |
| E. Queda brusca do servidor (opcional) | ☐ Não executada |
**E6 não está aprovada.** Para o cenário B é preciso banco novo: `Tools\e6.ps1 -Acao Limpar` e depois `-Acao Preparar`.

## 3. E7b-1: acabamento com os arquivos existentes (escrito; NÃO compilado nem executado)
Pedido do usuário atendido assim (detalhes em `Docs/ASSETS_E7.md`, seção 7):
- **Aviso xUnit2000:** corrigido trocando a ordem dos argumentos (a constante passa a ser o valor esperado); a verificação é a mesma.
- **Câmera e escala:** fov 38°, altura 10,5 m; Guerreiro 2,2 m e monstro 1,6 m.
- **HUD compacto:** cartão do jogador com as três barras, alvo no topo, barra de habilidades, registro de 5 linhas; sem painéis vazios.
- **Ícones:** golpe, guarda, ataque básico e poção recortados do mockup do Lote 7 (provisórios, ~50 px ampliados); espada = arte do Lote 2. Onde faltar o arquivo, volta a sigla.
- **Indicadores discretos:** anel fino, leque de mira bem leve, leque da Guarda leve; o anel some quando a criatura morre.
- **Cenário:** chão e muros com textura procedural, estandartes, caixotes, barris, poças, poças de luz sob os braseiros, tom de cinema, névoa leve.
- **Efeitos sincronizados com o servidor:** risco de golpe, faíscas de impacto, avanço de quem ataca, clarão em quem apanha, poeira e queda na morte, pulsos (Guarda, poção, nível, coleta, renascer), números de dano e tremor leve da câmera.
- **Não alterado:** contas, bancos, progresso, regras, rede e entrada do cliente (os trechos validados foram copiados sem mudança).
- **Testes:** continuam 166 esperados (nenhum teste novo; só a correção do aviso). Os novos efeitos e o HUD só se validam olhando o jogo.

### O que conferir olhando o jogo (E7b-1), ainda sem resultado
☐ O cliente compila com 0 erros e 0 avisos e abre sem erro no painel Output do Godot.
☐ O chão parece pedra de calçamento (não lajes azuis); os muros têm textura; os braseiros deixam poças de luz quente.
☐ O Guerreiro e o monstro aparecem maiores e legíveis; os anéis são finos; o leque branco quase não atrapalha.
☐ HUD: cartão no canto superior esquerdo com as 3 barras; ícones nos 5 slots; registro curto; sem espaços vazios grandes.
☐ Ao atacar: risco de golpe, avanço do atacante, faíscas e número de dano; ao apanhar: clarão, tremor leve.
☐ Ao morrer o monstro: poeira, ele afunda e some, o anel some; ao renascer: pulso.
☐ **E**: pulso azul e leque da Guarda; **1**: pulso verde; coletar item: pulso dourado.
☐ Duas janelas continuam sincronizadas.
☐ O desempenho continua bom (o cenário tem ~600 objetos).

## 4. Modelos 3D e animações (E7b-2): especificação pronta, nenhum arquivo ainda
- **Não existe nenhum modelo 3D.** Os cartazes são ilustrações de uma pose e **não são tratados como modelos**.
- `Docs/ESPECIFICACAO_MODELOS_3D.md` lista exatamente o que falta para **um Guerreiro e um Carrapato de Escória**: formato (`.glb`), unidades, eixos, pose, peças separadas (corpo, elmo, espada, escudo), esqueleto, texturas, 11 animações do Guerreiro e 5 do monstro, com os tempos vindos das regras do servidor.
- `Tools/validar_glb.py` confere o arquivo recebido (tamanho, origem, triângulos, ossos, peças, nomes e durações das animações, materiais e texturas). **Autoteste executado aqui e aprovado** (`python Tools/validar_glb.py --autoteste`), com modelos sintéticos; nenhum modelo real foi conferido.
- **Decisão pendente do usuário:** como obter os modelos (encomenda, ferramenta de imagem para 3D com limpeza, ou pacote provisório só para testar a integração). Recomendação na seção 7 da especificação.
- Depois de receber os arquivos: integração (carregar o `.glb` em tempo de execução, ligar as animações aos eventos do servidor, manter o cartaz como reserva).

## 5. Pendências: nada some entre entregas
| Pendência | Entrega | Situação / validação |
|---|---|---|
| Rede, auth, intenções, coleta | E3 | **Validada** |
| Ataque básico, conjuração, XP/níveis, equipar, poção, regeneração | E4 | **Validada** |
| Cliente Godot conectado; orientação e Guarda no servidor | E5 | **Validados** |
| **Disputa simultânea pelo item com dois clientes reais** | **E6 (cenário B)** | **PENDENTE.** Também sem relato C, E e partes de A e D |
| Primeira área visual (cartazes, cenário, HUD) | E7a | **Validada** (jogada); considerada simples |
| Acabamento com os arquivos existentes | E7b-1 | Escrito; compilar e olhar (lista da seção 3) |
| **Modelos 3D e animações (Guerreiro e monstro)** | **E7b-2** | **Pendente:** faltam os arquivos; especificação e validador prontos |
| Cenário modular 3D com texturas PBR, ícones próprios, VFX, som | E7b-3 | Pendente; dependem de assets que não existem |
| **Renomear nomes de exibição** (Carrapato de Escória, Espada de Vigília) | próxima entrega | **Decisão pendente** (recomendado; só nomes de exibição, sem mexer nos IDs) |
| Colisão com o cenário (servidor) | após E7b | Hoje estátuas, braseiros e estandartes são decoração atravessável |
| **Loot por morte** (tabela de drops, item caindo do monstro) | **E8** | **Pendência explícita.** Hoje o item vem de uma semente fixa. Necessário para fechar o ciclo de farm |
| **Penalidade de morte** (dívida de XP, estado caído, durabilidade; GDD 8) | **E9** | **Pendência explícita.** Hoje renasce no spawn em 5 s, sem perdas. Necessário para fechar o ciclo de farm |
| Pontos de atributo e redistribuição | Marco B | Força/Vigor fixos em 10 |
| Grupos formais (regra de 15 níveis, região) | Marco B | Hoje: XP dividido entre quem causou dano nos últimos 30 s a 30 m |
| Backup automático diário e TLS/exposição à internet | Fora do Marco A | `BackupTo` existe; servidor só em localhost/VPN |
O ciclo de farm só é considerado completo com **loot por morte e penalidade de morte**.

## 6. O que existe
- **Servidor** (`Server/`, `ServerHost/`): TCP com protocolo JSON; tick 50 ms; login por token; movimento, orientação, habilidades com conjuração, ataque básico, XP/níveis, equipar, poção, coleta exclusiva e idempotente, monstro com IA simples, respawn, salvamento (SQLite v1).
- **Compartilhado** (`Shared/`): protocolo, conteúdo, `TcpGameClient`, núcleo do cliente (C# puro).
- **Cliente** (`Client/`): projeto Godot fora da `.sln`: `Main.cs` (rede, entrada, entidades, efeitos), `ArenaBuilder.cs`, `HudView.cs`, `Effects.cs`, `VisualAssets.cs`, `Assets/Art`.
- **Conteúdo** (`Content/*.json`): classe, 2 habilidades, 1 arma, 1 poção, 1 monstro, `balance.json`.
- **Ferramentas:** `Tools/e6.ps1`, `Tools/verificar-ambiente.ps1`, `Tools/gerar_texturas.py`, `Tools/validar_glb.py`.

### Decisão técnica reversível: TCP em vez de ENet
O GDD recomenda ENet. O transporte é **TCP** atrás de `IServerTransport`. Serve para grupo pequeno em rede local/VPN. **Sem criptografia: use apenas localhost ou VPN.**

## 7. Parâmetros provisórios, sujeitos a balanceamento
`Content/balance.json` e `Content/skills.json`: nível inicial 5, Força 10, Vigor 10, inventário 40, **arena 40×40 m**, alcance do Golpe 2,5 m, coleta 3 m, aggro 8 m, respawn 20 s (monstro) e 5 s (jogador), mãos nuas 4 de dano a cada 1 s, ataque básico 0,5 do poder a 2,0 m, 3 Tônicos iniciais, conjuração/recuperação 300/400 ms (Golpe), arco da Guarda 120°.
Cliente: câmera isométrica fixa (fov 38°); cartaz do Guerreiro 2,2 m e do monstro 1,6 m; recargas do HUD são estimativa visual (o servidor é a autoridade).
Vêm do GDD: XP, K(L), fator de nível, bônus de grupo, regeneração 1%/s após 10 s, poção 12 s/4 s, Guarda −40% frontal por até 3 s com movimento a 50%.

## 8. Desvios do contrato 17.4
`SkillDefinition.effects` em vez de `effect_ids`; `MonsterDefinition` com `level` + `archetype` em vez de `base_profile`; item sem afixos, durabilidade e vínculo.

## Próximo comando
```powershell
cd "C:\Users\LKS X\Documents\JOGOS\CinzasDaMare"
dotnet build -c Release
dotnet test -c Release --no-build                  # esperado: 0 erros, 0 avisos, 166 aprovados
dotnet build .\Client\CinzasDaMare.Client.csproj   # esperado: 0 erros, 0 avisos
$env:CDM_GODOT = "C:\caminho\Godot_v4.7.2-stable_mono_win64_console.exe"
# Janela 1 (servidor; mantém o e6.db com alice e bruno):
powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Servidor
# Janela 2:
powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Cliente -Quem alice
powershell -ExecutionPolicy Bypass -File .\Tools\e6.ps1 -Acao Cliente -Quem bruno
```
Depois confira a lista da seção 3 e, se puder, mande uma captura de tela. Opcional (precisa de Python): `python Tools\validar_glb.py --autoteste`.
Para fechar o cenário B da E6: `-Acao Limpar` e `-Acao Preparar` (banco novo), e `Docs/ROTEIRO_E6.md`.

## Pendências após a E8 (nada some)
| Pendência | Situação |
|---|---|
| E6 cenário B (disputa simultânea com dois clientes reais) | **Continua PENDENTE** (o servidor e a coleta são os mesmos; `Tools/e6.ps1` ainda funciona — o cliente agora para no lobby, escolha o personagem) |
| Penalidade de morte (GDD 8) | Pendente (renasce no acampamento sem perda) |
| Previsão local de movimento | Não feita (servidor 20 Hz + suavização) |
| Modelos 3D / 11 animações (`ESPECIFICACAO_MODELOS_3D.md`) | **Adiados** por decisão do usuário; o componente `IEntityVisual` está pronto para recebê-los |
| Arqueira e Sacerdotisa | Fora do protótipo |
| Atributos, grupos formais, backup diário, TLS | Como antes |

## Próximo comando (no seu PC)
```powershell
cd "C:\Users\LKS X\Documents\JOGOS\CinzasDaMare - Copia"
dotnet build -c Release
dotnet test -c Release --no-build                  # esperado: 0 erros, 0 avisos, 236 aprovados
dotnet build .\Client\CinzasDaMare.Client.csproj   # esperado: 0 erros, 0 avisos
.\Jogar.cmd                                        # ou duplo clique; -CriarAtalho cria o ícone
```
Depois confira a lista 0.1 e, se puder, mande capturas de tela e o que pareceu ruim de controlar.

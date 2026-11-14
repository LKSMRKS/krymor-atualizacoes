# CINZAS DA MARÉ — DO PRIMEIRO MONSTRO AO MIDGAME

Documento de direção e implementação para o Claude • Versão 1 • 08/11/2026

## 1. Mandato e prioridade

Continue o projeto existente. O objetivo é entregar uma progressão longa e conectada, do personagem iniciante ao midgame, com farm, missões, regiões, dungeons, evolução de classe, equipamentos, profissões leves, casa, montaria, asas e um companheiro de combate.

A base é MU: cidade, mapas persistentes, monstros que reaparecem, dedicação para ganhar níveis, equipamentos desejáveis e grupos que ajudam a superar desafios. A câmera e o combate atuais permanecem como base. Exploração, missões e dungeons enriquecem esse ciclo. O visual segue o pacote de direção aprovado: fantasia sombria com corpos simples, finos e articulados, equipamentos angulares e criaturas caricatas ameaçadoras.

Este documento amplia e, nos pontos explicitamente alterados, substitui propostas anteriores. Asas, companheiros com atributos, moradia e profissões agora entram no escopo, mesmo que um GDD antigo os excluísse. Não combine indiscriminadamente limites e números de documentos antigos e novos.

Leia o código e os documentos atuais antes de modificar. Reaproveite sistemas e conteúdo já existentes. Preserve IDs e progresso; mapeie nomes equivalentes. Os nomes de trabalho daqui não autorizam apagar regiões antigas ou criar duplicatas do mesmo chefe.

Os códigos R01, Q01, D01 e semelhantes são identificadores de planejamento neste arquivo. Mapear para os IDs reais do projeto e usar namespace novo onde necessário; não sobrescrever uma definição existente só porque seu código coincide.

Nada neste documento foi implementado ou testado no jogo. Níveis, rendimentos e preços relativos são propostas para calibração. Resultados anteriores de builds e testes não validam esta expansão.

## 2. Resultado esperado para quem joga

O jogador começa na cidade apenas de cueca opaca, vence criaturas muito fracas, conquista sua primeira arma e segue recebendo oportunidades: outra missão, equipamento procurado, receita, nova região, desafio de classe ou objetivo de casa.

Durante o early game, aprende a combater, vender, preparar uma poção, minerar um recurso e plantar para uma tarefa. No midgame, possui uma classe evoluída, uma casa modesta com depósito, montaria, asas iniciais e acesso a um companheiro. Já enfrenta dungeons com grupo ou preparação suficiente, procura seu primeiro lendário e se prepara para a segunda evolução de classe.

Não entregar esses sistemas como menus isolados. A missão dá motivo para usar o recurso, o recurso melhora a preparação e a preparação ajuda a vencer o próximo desafio.

Metas de experiência:
- Sempre existir pelo menos um objetivo principal alcançável e objetivos opcionais úteis.
- Missões e exploração não substituírem o farm nem exigirem sequência rígida para toda atividade.
- Equipamento, habilidade do jogador e cooperação contribuírem para vencer.
- Regiões antigas terem usos posteriores sem transformar todos os inimigos em conteúdo escalado.
- Conseguir jogar em sessões curtas e também dedicar expedições longas.

## 3. Limites do midgame e progressão

### 3.1 Faixa deste pacote

Proposta: conteúdo jogável dos níveis 1 a 60. Early: 1–20; transição: 21–35; midgame: 36–60. Nível 60 fecha esta expansão, não é automaticamente o limite global do personagem. Se a estrutura atual suporta 120, mantenha essa capacidade; não simule conteúdo 61–120 que ainda não existe.

| Marco | Desbloqueio principal | Objetivo do jogador |
|---|---|---|
| 1–5 | Primeira arma e roupas básicas | Aprender e sentir progresso imediato |
| 6–10 | Primeira dungeon; preparo de consumível | Concluir uma pequena expedição |
| 11–19 | Mineração, cultivo de missão e fabricação básica | Preparar equipamento e reconstruir ponte |
| 20 | Primeira evolução de classe | Concluir prova e ganhar ferramenta nova |
| 21–29 | Montaria e cânions | Explorar distâncias maiores e reunir materiais |
| 30–35 | Compra de casa, cultivo doméstico e primeiro vínculo de companheiro | Criar objetivos pessoais duradouros |
| 36–44 | Cidade fantasma; rota do primeiro lendário | Vencer novos padrões e concluir cadeia importante |
| 45–49 | Asas iniciais, conteúdo de gelo e dungeon mais exigente | Ampliar mobilidade e preparação |
| 50 | Segunda evolução de classe | Especializar a classe e ampliar combinações |
| 51–60 | Região vulcânica, companheiro aprimorado, dungeons e chefe do midgame | Consolidar equipamento e cooperação |

Desbloqueios não devem todos ser entregues automaticamente ao atingir o nível: nível libera a missão e a conclusão concede o recurso. Exceções de acessibilidade devem ser registradas, não improvisadas silenciosamente.

### 3.2 Ritmo de XP

Quero progressão exigente, com tempo de farm. O combate continua responsivo e monstros fracos não viram sacos de vida. O primeiro equipamento chega cedo, mesmo que níveis posteriores demorem.

Orçamento inicial de duração para um personagem novo, sem ajuda e sem equipamento herdado; são metas de design para testes, não promessas:

| Faixa | Tempo adicional de jogo ativo de referência | Observação |
|---|---:|---|
| 1–10 | 1,5–3 horas | Aprendizado e melhoria rápida |
| 11–20 | 4–7 horas | Farm, primeira dungeon e evolução |
| 21–35 | 10–16 horas | Materiais, montaria, casa e cadeias regionais |
| 36–50 | 16–25 horas | Lendário, asas e desafios de classe |
| 51–60 | 15–25 horas | Preparação e dungeons do midgame |

Total indicativo: 46,5–76 horas ativas. É orçamento de experiência; reajustar após medir o jogo, sem preencher duração com deslocamento vazio, espera de plantio ou uma única tarefa repetida centenas de vezes.

Calibrar a curva existente a partir de XP/minuto medido em pontos de farm, tempo de dungeon, entrega de missões e taxa de mortes. Simular Guerreiro e Mago em equipamento compatível. Conferir bônus de grupo e multiplicadores provisórios antigos, inclusive XP ×8. Não multiplicar novamente um bônus já embutido.

Como referência inicial do XP recebido: cerca de 45% de combate em campo, 30% de entregas de missões e 25% de combates/conclusões de dungeon. São fontes distintas, não bônus somados obrigatórios. Um jogador pode farmar mais e fazer menos missões. Não conceder o XP da morte duas vezes por estar em uma missão.

Missão de história vale mais na primeira conclusão; repetíveis oferecem progresso constante com menor eficiência e requisitos apropriados ao patamar. Não fazer uma repetível inicial tornar toda região posterior desnecessária. Nenhum ganho de XP por ficar parado, clicar em diálogo ou comprar e entregar o mesmo item repetidamente sem custo/proveniência válida.

### 3.3 Evolução das duas classes

Guerreiro → Guardião de Ferro (nível 20) → Vanguarda ou Quebra-Selos (nível 50).
Mago → Arcanista de Cinzas (nível 20) → Piromante ou Tecelão Espectral (nível 50).

Nomes e divisão são propostas. Se já houver evolução implementada, comparar e adaptar antes de duplicar. As duas primeiras evoluções verticais não são quatro classes novas.

Na primeira evolução, acrescentar uma habilidade ativa e um modificador passivo, aproveitando recursos existentes. Na segunda, escolher um ramo: duas modificações de habilidades e um passivo. Não construir dezenas de habilidades novas ou barra com vinte atalhos. A barra suporta um conjunto pequeno de habilidades escolhidas; mudar a configuração em lugar seguro.

Vanguarda: proteção, contra-ataque e postura. Quebra-Selos: impacto, interrupção e pressão. Piromante: explosões e queimaduras limitadas. Tecelão: projéteis, reposicionamento e vulnerabilidade temporária. Todas as especializações têm dano e sobrevivência viáveis sozinhas. Nenhuma dungeon exige uma classe de cura que não existe.

Prova 20: preparar equipamento de missão, recuperar um selo em D1/D2 e enfrentar um adversário de classe. Requisitos de equipamento alternativos, sem depender de drop raro. Prova 50: investigar cidade fantasma, fabricar um foco/insígnia e vencer teste de três padrões. O teste decisivo é pessoal; a expedição de materiais pode ser feita em grupo.

## 4. Mundo: oito regiões conectadas

O mundo cresce por mapas separados com transições claras. Não exigir streaming de um continente inteiro. A dimensão percebida vem de marcos, caminhos alternativos, desníveis suportados, segredos, lugares habitados e retornos úteis.

Porto do Cal é o hub inicial. Outros nomes são propostas a reconciliar com mapas atuais. Regiões 1–2 reaproveitam o primeiro mapa, a mata e a serraria em desenvolvimento. Regiões 3–8 acrescentam conteúdo; não reconstruir a costa do zero.

| ID | Região e faixa | Aparência e lugar habitado | Criaturas / farm | Conexão e desafio |
|---|---|---|---|---|
| R01 | Costa do Cal, 1–10 | Cidade portuária de pedra escura, areal, hortas e ruínas baixas | Larvas, besouros, ratos, caranguejos, pequenas aranhas | Portões de Porto do Cal; D1; estrada à mata |
| R02 | Mata dos Enforcados, 10–20 | Árvores macabras com troncos ocos e silhuetas de braços; vila do moinho | Lobos, javalis, saqueadores, aranhas, raízes andantes | Ponte reconstruída; D2; chefe fecha a estrada dos cânions |
| R03 | Cânions dos Suplicantes, 20–30 | Desfiladeiros com nichos de almas pedindo piedade; posto de elevador | Escorpiões, morcegos, esqueletos, espectros e trolls | Elevador liberado e estrada após chefe; D3 |
| R04 | Brejo da Viúva, 28–36 | Vila sobre estacas, água rasa, igrejas afundadas e estufas abandonadas | Sapos, serpentes, sanguessugas, tecelãs e basilisco elite | Causa comum aos problemas da vila; D4; ruínas para a cidade fantasma |
| R05 | Véspera, a Cidade do Outro Lado, 34–44 | Cidade fantasma reconhecível, mercados vazios, sinos e habitantes presos em lembranças | Afogados, carniçais, espectros, necromantes e cavaleiros | Travessia por rito/guia; D5; acesso ao mentor de evolução |
| R06 | Coroa da Geada, 42–50 | Fortaleza de montanha, pontes, caravana e ruínas nórdicas | Lobos de gelo, draugr, trolls e gigantes | Passagem protegida por rei draugr; D6; componentes das asas |
| R07 | Mar de Escória, 48–56 | Mundo de lava, ilhas de basalto, forjas e pontes de corrente; refúgio de ferreiros | Besouros de brasa, carbonizados, elementais e golems | Acesso por túnel/ferry de minério; D7; fênix e materiais superiores |
| R08 | Acrópole das Cinzas, 55–60 | Cidade-templo quebrada sobre penhascos, bronze, mármore queimado e céu carregado | Guardiões de bronze, harpias, centauros e minotauros | Chefe de passagem; D8; consolidação do midgame |

### Ligações e rotas

R01 ↔ R02 ↔ R03. R03 leva a R04 e a um acesso difícil a R06. R04 ↔ R05. R05 conecta R06 pelo passo restaurado. R06 ↔ R07 ↔ R08. Ao descobrir os hubs, abrir viagens seguras por barqueiro, caravana ou marco ativado.

Conexões não precisam ser invisíveis portais em toda esquina: estrada, elevador, túnel, ponte e travessia com NPC contextualizam mudança de mapa. Transições preservam grupo e não geram monstros sobre o ponto de chegada.

Cada região: três a cinco bolsões de farm com finalidade diferente, um local de elite/raridade, um ponto de interesse opcional, um hub seguro e uma entrada de dungeon. Quantidade de monstros ativos deve ser dimensionada pela carga real do servidor; não ativar oito regiões inteiras permanentemente sem necessidade.

### Chefes que impedem passagem

Existem barreiras de combate reais: o personagem recém-chegado não deve atravessar tudo correndo. Exemplos: Guardião Retorcido na saída da mata, Carcereiro das Almas no elevador, Rei Draugr no passo e Colosso de Escória antes da acrópole.

Eles têm força fixa por encontro e exigem equipamento, execução ou grupo. Não são literalmente invencíveis por um nível abaixo do recomendado. Evitar escalonamento que anule se preparar ou chamar amigos.

Matar o guardião concede um selo de passagem pessoal aos participantes elegíveis. Após o selo, usar o portão/atalho permanece possível mesmo quando o chefe de farm reaparece. Assim um respawn não bloqueia uma região já conquistada. A geometria pode ser compartilhada, mas a autorização de travessia e a missão pertencem ao personagem/grupo conforme regra explícita.

Contribuição inclui dano, proteção e suporte válidos no encontro; não exigir golpe final. Um jogador distante ou desconectado não recebe automaticamente. Não presumir que todo integrante de grupo está presente.

## 5. Dungeons: oito expedições

Começar com layouts feitos à mão e módulos reutilizáveis. Cada dungeon tem entrada contextualizada, encontros mistos, miniboss, chefe, um ramo opcional, atalho/checkpoint e recompensa. Não é necessário acrescentar um puzzle exclusivo por sala.

| ID | Dungeon / faixa | Encontros e desafio | Miniboss → chefe | Fonte característica |
|---|---|---|---|---|
| D01 | Toca da Crosta, 5–10 | Larvas, besouros e teias; romper ninhos limita reforços | Besouro Couraçado → Rainha das Larvas | Primeiras peças e quitina |
| D02 | Cripta do Sino, 15–22 | Esqueletos, acólito e zonas de pulso sonoro | Cavaleiro Sepulcral → Guardião do Sino | Selo de evolução e ferro antigo |
| D03 | Galerias da Piedade, 23–31 | Espectros e mineradores ocos; libertar ou conter emissores de lamento | Troll Carcereiro → Carcereiro das Almas | Minério de eco e equipamento de postura |
| D04 | Mosteiro Afundado, 30–38 | Sapos, carniçais e tecelãs; válvulas abrem rotas por salas com água rasa | Viúva Tecelã → Abadessa do Lodo | Reagentes, receitas e materiais de companheiro |
| D05 | Mercado dos Mortos, 37–45 | Soldados espectrais e necromantes; escolher alas e interromper sinos | Cobrador Sem Rosto → Regente de Véspera | Primeiro núcleo lendário e memória de classe |
| D06 | Salão da Geada, 45–52 | Draugr, trolls e coberturas; alternar avanço e proteção contra ventos anunciados | Campeão Draugr → Jötunn Acorrentado | Estrutura das asas e metais de frio |
| D07 | Fornalha dos Nascidos do Fogo, 51–58 | Elementais, carbonizados e golems; resfriar passagens e limitar calor local | Ferreiro Corrompido → Coração de Escória | Brasa da fênix, liga lendária e armas |
| D08 | Labirinto do Juramento, 57–60 | Guardiões, centauros e harpias terrestres; duas alas e atalhos | Minotauro do Juramento → Quimera de Bronze | Conjunto do midgame e melhoria lendária |

Solo bem equipado ou grupo de dois a quatro. Os números finais dependem de teste. Não exigir quatro pessoas para acessar a primeira metade do jogo. Desafio vem de padrões, pressão e preparo, não só multiplicadores de vida.

Criar instância de dungeon por grupo/solo se a infraestrutura não a tiver. Campos externos continuam persistentes e compartilhados. Se usar limite de quatro, mostrá-lo antes da entrada. Snapshot de participantes e regras de reconexão; reentrada não restaura consumíveis nem reinicia baú individual.

Ao morrer, voltar ao checkpoint da expedição; um wipe reinicia o encontro atual, não exige refazer todo o caminho vazio. Recarga de recompensa e reset de encontro são coisas diferentes. Primeira conclusão, repetição e assistência precisam de mensagens claras.

Não acrescentar bloqueios diários nesta fase. Repetir dungeon para farm é permitido. Recompensas de farm vêm de novas vitórias legítimas, nunca de abrir o mesmo baú por reconexão. Se houver limite técnico de instâncias, informar e oferecer espera/retorno seguro.

## 6. Missões: fluxo contínuo com variedade

### 6.1 Funcionamento

NPCs importantes têm cadeias. Depois de uma entrega, oferecem o próximo objetivo quando os requisitos estão cumpridos. Se a cadeia terminou, apresentam uma repetível útil, rumor ou indicação de outro NPC. Não inventar história infinita por geração automática nem prometer missão que ainda não existe.

Manter de um a três objetivos principais ativos e alguns opcionais. Diário permite filtrar história, região, profissão, casa, companheiro e repetíveis. Destacar no máximo três acompanhadas no HUD.

Cada missão possui ID, pré-requisitos, NPC inicial/final, etapas, critérios de progresso, texto curto, recompensas, repetibilidade, efeitos no mundo e persistência. Diálogos variam conforme etapa. Uma entrega completa uma única transação de recompensas.

Tipos reutilizáveis: derrotar, coletar drop de missão, obter item comum, entregar, visitar, investigar, interagir, minerar, plantar, colher, fabricar, cozinhar, preparar poção, resgatar, defender e vencer chefe. Resgate inicialmente é libertar NPC e liberar retorno seguro; escolta com navegação complexa é opcional posterior.

Variedade também vem de decisões locais: qual fonte de minério buscar, caminho curto perigoso ou longo seguro, fabricar consumível defensivo ou ofensivo, enfrentar boss com grupo ou melhorar equipamento. Não transformar toda missão em seis mortes com outro nome.

### 6.2 Sessenta e quatro missões principais e regionais

Cada linha é uma missão concreta com etapas. São oito cadeias de oito missões. A sequência interna tem pré-requisitos; cadeias laterais podem abrir em paralelo. Quantidades são iniciais para teste, não compromissos absolutos.

| ID / título | NPC e região | Objetivos / transformação | Recompensa principal |
|---|---|---|---|
| Q01 Mãos que Aprendem | Ondina, R01 | Vencer 6 criaturas fracas; retornar | Arma inicial da classe + XP + moeda |
| Q02 Uma Pele Melhor | Costureira, R01 | Obter 4 carapaças danificadas; entregar | Primeira peça de torso |
| Q03 Remédio do Areal | Boticário, R01 | Obter resíduo de larva e colher erva em área segura | Ingredientes + receita básica |
| Q04 Primeira Mistura | Boticário, R01 | Preparar 2 tônicos no caldeirão; entregar 1 | Restante do preparo + XP |
| Q05 Ferramenta Emprestada | Ferreiro, R01 | Receber picareta de missão e minerar 3 veios rasos | Ferro comum + acesso à oficina |
| Q06 Placa de Chapa | Ferreiro, R01 | Fabricar componente com ferro e carapaça; equipar/mostrar peça compatível | Escudo ou foco de classe |
| Q07 Sob as Hortas | Agricultora, R01 | Investigar 2 ninhos e marcar entrada de D01 | Sementes + suprimentos |
| Q08 A Rainha Sob a Terra | Ondina, R01 | Vencer Rainha em D01 e entregar sinal de vitória | Peça incomum garantida + acesso à cadeia da mata |
| Q09 A Colheita Não Volta | Agricultora, R02 | Investigar horta destruída e derrotar saqueadores próximos | XP + sementes |
| Q10 Plantar para Recomeçar | Agricultora, R02 | Plantar 2 canteiros comunitários e colher | Ingredientes + cultivo liberado |
| Q11 Jantar da Vigília | Cozinheiro, R02 | Cozinhar ensopado com colheita e carne de javali; entregar porções | Receita + porção de buff |
| Q12 Madeira para a Ponte | Carpinteiro, R02 | Recuperar tábuas da serraria; obter resina de raízes andantes | Moeda + progresso de ponte |
| Q13 Rebites que Faltam | Carpinteiro, R02 | Minerar ferro; fabricar rebites; entregar materiais previstos | Ponte reconstruída / atalho pessoal |
| Q14 O Sino Enterrado | Mentora, R02 | Encontrar inscrição e entrar em D02 | Preparação da evolução |
| Q15 Juramento de Ferro ou Cinzas | Mentora, R02 | Nível 20; fabricar insígnia; concluir prova individual | Primeira evolução de classe |
| Q16 A Raiz na Estrada | Ondina, R02 | Vencer Guardião Retorcido e ativar portão | Selo de passagem a R03 |
| Q17 Vozes na Pedra | Zelador, R03 | Visitar 3 nichos de almas e descobrir história do elevador | XP + mapa parcial |
| Q18 O Ferro que Escuta | Mineiro, R03 | Minerar veio de eco com ferramenta liberada na missão | Minério e receita de ressonador |
| Q19 Um Pedido de Piedade | Alma, R03 | Recuperar medalhão em grupo de mortos-vivos e devolvê-lo | Memória selada + XP |
| Q20 Sela de Estrada | Tratador, R03 | Obter couro de lobos/javalis e fabricar sela | Sela + próxima missão |
| Q21 Um Companheiro de Viagem | Tratador, R03 | Recuperar animal preso; alimentar e registrar vínculo | Primeira montaria permanente |
| Q22 Ressonador de Lamentos | Zelador, R03 | Fabricar ressonador com minério e memória; ativar 2 marcos | Acesso funcional a D03 |
| Q23 O Carcereiro das Almas | Zelador, R03 | Vencer chefe de D03 e libertar almas retidas | Selo de elevador + equipamento |
| Q24 Entrega à Vila Suspensa | Barqueiro, R03 | Levar carga de missão até hub de R04, com percurso pela primeira vez | Viagem de retorno descoberta |
| Q25 Água Amarga | Curandeira, R04 | Investigar 3 fontes e obter amostras | Receita de antídoto |
| Q26 Ferrão e Folha | Curandeira, R04 | Obter ferrões e colher erva do brejo | Ingredientes + XP |
| Q27 Antídoto para os Vivos | Curandeira, R04 | Preparar antídoto; tratar 3 moradores por interação | Vila melhora visualmente + moeda |
| Q28 O Canteiro Alagado | Agricultor, R04 | Abrir comporta e cultivar planta de missão em canteiro reservado | Sementes domésticas |
| Q29 Tecidos para os Feridos | Costureira, R04 | Obter seda; fabricar ataduras; entregar | Receita utilitária + reputação local |
| Q30 A Casa Sem Dono | Escrivão, R04/R01 | Investigar antiga escritura e pagar valor da casa | Primeira casa e baú privado |
| Q31 A Centelha que Respira | Tratador, R04 | Resgatar pequeno draco ferido; preparar alimento; concluir vínculo | Companheiro inicial: mini-dragão |
| Q32 O Mosteiro Afundado | Curandeira, R04 | Vencer D04 e recuperar sinete do antigo zelador | Equipamento + rito para R05 |
| Q33 Véspera Não Amanhece | Guia, R05 | Entrar pelo rito e visitar 3 bairros reconhecíveis | Ponto de retorno seguro |
| Q34 Cartas Nunca Entregues | Carteiro espectral, R05 | Recuperar cartas e entregá-las a 3 habitantes | Moeda antiga + XP |
| Q35 O Mercado Ainda Cobra | Mercador espectral, R05 | Obter ficha em combate e negociar uma peça de missão | Loja regional liberada |
| Q36 O Banquete dos Ausentes | Cozinheiro espectral, R05 | Cultivar ingrediente próprio, cozinhar e oferecer refeição memorial | Receita de buff regional |
| Q37 Ferro que Lembra | Ferreiro espectral, R05 | Recuperar fragmentos do mercado e fabricar base de relíquia | Primeiro estágio do lendário |
| Q38 O Regente Não Parte | Guia, R05 | Vencer D05 e obter memória do Regente | Núcleo garantido de primeira vitória |
| Q39 Uma Arma com História | Ferreiro espectral, R05 | Combinar base, núcleo e materiais acessíveis da classe | Primeiro lendário por cadeia |
| Q40 O Passo dos Mortos | Guia, R05 | Libertar sentinela, ativar marco do passo e atravessar a R06 | Rota permanente para a geada |
| Q41 Gente Perdida na Neve | Batedora, R06 | Localizar 3 sobreviventes e abrir retorno ao acampamento | XP + suprimentos de frio |
| Q42 Minério Sob o Gelo | Ferreiro da caravana, R06 | Minerar ferro de geada em bolsões protegidos por draugr | Liga e receita de estrutura |
| Q43 Comida para a Travessia | Cozinheiro, R06 | Preparar alimento de frio com itens comuns e colheita doméstica/comunitária | Receita + buff útil |
| Q44 O Rei do Passo | Batedora, R06 | Vencer Rei Draugr de campo e abrir posto avançado | Atalho + selo de passagem |
| Q45 Membrana e Metal | Artesã, R06 | Obter membranas, liga e tecido; fabricar estrutura das asas | Asas incompletas de missão |
| Q46 O Jötunn Acorrentado | Artesã, R06 | Vencer D06 e recuperar núcleo de sustentação | Núcleo pessoal garantido |
| Q47 Primeiras Asas | Artesã, R06 | Finalizar asas e concluir percurso de impulso em plataforma segura | Asas iniciais permanentes |
| Q48 O Segundo Juramento | Mentor, R05/R06 | Nível 50; fabricar foco/insígnia; vencer prova; escolher ramo | Segunda evolução de classe |
| Q49 Onde a Pedra Ferve | Mestre da Forja, R07 | Alcançar refúgio e investigar 2 passagens de calor | Viagem regional e receita |
| Q50 Ferro que Não Cede | Mestre da Forja, R07 | Minerar basalto férreo; refinar liga com minério da geada | Material superior |
| Q51 Caldo de Cinzas | Cozinheira, R07 | Cozinhar refeição com cogumelo de missão e ingrediente da casa/canteiro | Buff contra perigo ambiental |
| Q52 Um Ninho nas Brasas | Tratador, R07 | Encontrar ninho de fênix e apagar 3 focos com reagente fabricado | Etapa de vínculo de fênix |
| Q53 A Brasa que Renasce | Tratador, R07 | Vencer D07; obter brasa pessoal; preparar alimento de vínculo | Companheiro alternativo: fênix |
| Q54 A Liga do Juramento | Ferreiro, R07 | Fabricar liga lendária usando drops, minério e componente de D07 | Receita de melhoria de relíquia |
| Q55 O Colosso de Escória | Mestre da Forja, R07 | Vencer chefe que protege ponte para R08 | Selo de passagem |
| Q56 Caravana Entre Cinzas | Caravaneira, R07 | Preparar caixas de suprimento e ativar 2 postos; resgatar batedor | Rota segura e mercador de R08 |
| Q57 Juramentos em Ruínas | Sentinela, R08 | Investigar 3 inscrições e derrotar guardiões locais | XP + objetivo de dungeon |
| Q58 Bronze e Memória | Artesã, R08 | Minerar bronze antigo; fabricar chave com liga e memória de R05 | Chave permanente de D08 |
| Q59 Três Provas da Acrópole | Sentinela, R08 | Vencer elite, interromper ritual e recuperar estandarte | Peça rara garantida |
| Q60 O Minotauro do Juramento | Sentinela, R08 | Vencer miniboss de D08 e ativar atalho | Acesso ao encontro final |
| Q61 A Quimera de Bronze | Sentinela, R08 | Vencer chefe de D08 | Núcleo superior + equipamento |
| Q62 Relíquia Reacesa | Ferreiro, R08/R01 | Melhorar lendário com materiais e núcleo do desafio final | Evolução da arma escolhida |
| Q63 Um Lar Depois da Guerra | Escrivão/Artesã, R01 | Instalar melhoria de jardim e troféu após concluir expedição | Melhoria da casa e canteiros |
| Q64 O Horizonte Ainda Chama | Ondina, R01 | Retornar à cidade, registrar feitos e consultar mapa futuro | Encerramento do midgame + objetivos de farm ativos |

Tarefas de matéria-prima usam quantidades pequenas inicialmente: ingredientes comuns 3–8, peças fabricadas 1–3, elites 1–2, boss 1. A dificuldade total vem da expedição e progressão, não de números enormes iguais em todas as linhas. Ajustar pelo tempo real e pelas fontes disponíveis.

Q30 é opcional: quem não comprar casa usa canteiros e armazenamento comunitários e continua a campanha. Q39 permite rota alternativa de classe. Q48 só aparece ao cumprir pré-requisitos; enquanto isso, NPC oferece conteúdo de preparação apropriado. Q64 não anuncia regiões futuras como disponíveis.

### 6.3 Dez missões opcionais

| ID | Missão | Objetivo e motivo |
|---|---|---|
| S01 | O Anel do Poço | Investigar poço, enfrentar ratos e devolver anel; escolher moeda ou acessório comum |
| S02 | Uma Lâmina sem Dono | Encontrar espada quebrada e repará-la com minério; arma alternativa |
| S03 | O Lobo que Não Uiva | Rastrear marcas e vencer alfa opcional; couro e receita de sela |
| S04 | A Colmeia no Caminho | Limpar acesso e obter cera; vela ritual útil em R05 |
| S05 | Um Nome no Cânion | Reunir fragmentos de nome e libertar uma alma; acessório com resistência |
| S06 | A Mina Esquecida | Abrir caminho e minerar veio único de missão; molde de ferramenta |
| S07 | O Banquete de Ninguém | Entregar refeições a fantasmas após descobrir suas lembranças; receita |
| S08 | O Cavaleiro que Espera | Duelo opcional com recuperação justa; arma/cosmético de conquista |
| S09 | Plumas na Neve | Recuperar plumas de local perigoso; aspecto alternativo de asas, sem vantagem exclusiva |
| S10 | A Dívida do Ferreiro | Recuperar molde em D07; alternativa de melhoria do equipamento |

### 6.4 Repetíveis que mantêm NPCs úteis

O quadro regional oferece de três a cinco contratos disponíveis, renovados por conclusão e progresso, sem exigir voltar no dia seguinte. Templates finitos com alvos elegíveis; não narrativas infinitas geradas. Oferecer funções distintas: caçar, minerar, fabricar, cozinhar, preparar poção, cultivar, explorar e vencer elite/dungeon.

Exemplos: abastecer tônicos; fabricar três rebites; preparar duas refeições; colher ervas; minerar cinco unidades; eliminar uma patrulha; recuperar um emblema de elite; concluir uma ala de dungeon. NPC pode ter próximo pedido, mas indica claramente quando é contrato repetível.

No máximo um contrato ativo idêntico por personagem. Recompensas calibradas pelo tempo e custo de insumos. Proibir ciclos de comprar barato, entregar e receber moeda ilimitada sem atividade. Materiais comprados podem atender entregas comuns se o preço mantém coerência; objetivos que pedem minerar/cultivar/fabricar exigem evento de ação posterior à aceitação, não só posse do item.

Guardar itens comuns antes da missão pode ajudar quando objetivo é obter/entregar. Não invalidar materiais legitimamente encontrados só para obrigar farm extra. Materiais pessoais de missão não podem ser negociados se isso quebrar a progressão.

## 7. Mineração, fabricação, alquimia e culinária

Profissões leves a serviço de missões e preparo. Não criar outro simulador econômico completo ou árvore de vinte profissões.

### 7.1 Mineração

Picareta utilitária em slot de ferramenta, sem substituir a arma durante toda a sessão. Interação inicia animação curta; dano recebido cancela. Veios específicos por região e respawn persistente. Não há mineração AFK.

Cada personagem recebe quantidade pessoal limitada por ciclo de nó para evitar um amigo apagar o objetivo do outro. O servidor valida proximidade, ferramenta, recarga, disponibilidade e inventário antes de conceder recursos. Cliques simultâneos não multiplicam rendimento. IDs do nó e ciclo de respawn estáveis.

Minérios deste pacote: ferro raso (R01), ferro de eco (R03), ferro de geada (R06), basalto férreo (R07), bronze antigo (R08). Ferro comum atende várias receitas. Não adicionar minério de cada cor sem utilidade.

### 7.2 Receitas iniciais concretas

Todas as quantidades abaixo são propostas; testar disponibilidade e custo. Receitas criam itens de inventário com IDs próprios. Ao entrar na etapa de missão, conceder receita necessária ou indicar fonte garantida.

| Receita | Estação | Ingredientes sugeridos | Uso |
|---|---|---|---|
| Tônico simples | Caldeirão | 2 ervas + 1 resíduo de larva + 1 frasco | Cura segundo regras de poção existentes |
| Rebites | Forja | 2 ferros comuns → 3 rebites | Ponte e equipamento |
| Placa de chapa | Forja | 3 ferros + 2 carapaças | Peça inicial de classe |
| Ataduras | Bancada | 2 sedas + 1 tecido | Entrega e consumível com efeito compatível |
| Ensopado da vigília | Panela | 1 carne de javali + 2 raízes cultivadas | Buff curto de vigor/preparo |
| Antídoto do brejo | Caldeirão | 2 folhas do brejo + 1 ferrão + frasco | Reduz/cura veneno conforme efeito definido |
| Sela simples | Bancada | 4 couros + 2 tecidos + 2 rebites | Missão de montaria |
| Ressonador | Bancada | 3 ferros de eco + 1 memória selada | Missão e interação de D03 |
| Refeição de vínculo | Panela | 1 carne + 1 planta cultivada + reagente de missão | Companheiro; não manutenção diária |
| Liga de geada | Forja | 2 ferros de geada + 1 ferro comum | Estrutura de asas |
| Estrutura de asas | Bancada | 3 ligas + 3 membranas + 2 tecidos | Missão de asas |
| Refeição da travessia | Panela | Carne + raiz + erva de inverno | Buff de exploração, não chave obrigatória |
| Liga lendária | Forja | Basalto férreo + ferro de geada + fragmento de D07 | Melhoria de relíquia |
| Alimento de brasa | Caldeirão | Planta resistente + reagente de missão + brasa de vínculo | Missão de fênix |

Frascos e ingredientes básicos têm fonte acessível em loja; não vender componente que exige boss. Sem qualidade aleatória de fabricação nesta fase: receita mostra exatamente o resultado.

Consumir ingredientes e conceder resultado numa transação. Falta de espaço não destrói material. Requisição com ID repetido retorna o mesmo resultado sem produzir outro item. Cancelamento, desconexão e repetição precisam de comportamento definido.

### 7.3 Comida e poções

Um buff de refeição ativo por personagem; comida nova substitui ou renova, não acumula vinte bônus. Poções mantêm a recarga compartilhada já existente. Buffs de alimento são úteis, mas não devem ser requisito secreto para conseguir vencer qualquer inimigo.

Sem fome, sede, deterioração, alimentação diária de pet ou falha aleatória de receita. Missões usam caldeirão e panela para dar ações variadas e recompensas, sem manutenção cansativa.

## 8. Cultivo comunitário e doméstico

Antes da casa, usar horta comunitária com parcelas pessoais identificadas. Esteticamente pode ser uma horta compartilhada; progresso de planta e colheita pertencem ao personagem. Outros jogadores não roubam nem destroem o objetivo.

Ciclo: obter semente → escolher canteiro → plantar → regar uma vez → fazer outra atividade → colher. Primeira missão fornece sementes e ferramenta. Primeira espécie cresce em 3–5 minutos, suficiente para aprender enquanto realiza outra tarefa. Cultivos de midgame: aproximadamente 10–20 minutos, calibráveis. Nunca exigir esperar horas parado para continuar a história.

Plantas crescem fora da sessão a partir de horário persistido do servidor. Não morrem por ausência; sem clima e fertilidade complexos nesta fase. Objetivos podem pedir duas ou três colheitas com atividades paralelas, sem impor visitas em horário específico.

Espécies: raiz da vigília (comida), erva de tônico (alquimia), folha amarga (antídoto), cogumelo de cinza (preparo regional) e flor de vínculo (pet). Começar com duas, ampliar conforme missões. Cada espécie tem utilidade explícita.

Casa básica tem dois canteiros, expansão para quatro. Horta comunitária oferece acesso aos ingredientes de campanha mesmo para quem não compra casa. Cultivo serve a missões e consumíveis; sem agricultura industrial, animais de fazenda, estações do ano ou economia de exportação.

## 9. Armazenamento e casa

### 9.1 Antes da casa

O personagem começa sem baú próprio. Pode usar o armazém comunitário de Porto do Cal: edifício público, mas conta pessoal de itens, com capacidade inicial proposta de 12 slots. Não é um recipiente aberto a todos. A interface deve dizer claramente: “Seu depósito no armazém comunitário”.

Doações públicas para obra de ponte são outro sistema; não misturar com depósito pessoal. Não criar roubo entre amigos sem pedido específico.

### 9.2 Compra de casa

Casa pequena opcional por volta de 30–35, via Q30, com preço equivalente a aproximadamente 2–4 horas de renda líquida compatível com a região, a calibrar. Não cobrar aluguel, imposto ou manutenção.

Oferecer uma entrada para bairro residencial/interior instanciado por proprietário. Evitar exigir um lote único físico por jogador ou permitir que poucos comprem toda a cidade. Cada personagem pode possuir uma casa nesta fase.

Casa: interior simples, cama visual, baú privado de 32 slots, dois canteiros, bancada e caldeirão. Melhorias opcionais: baú de 48 slots e quatro canteiros. Nada de construção livre ou decoração com centenas de objetos agora.

O depósito comunitário não desaparece ao comprar casa; transferência é explícita. Baú não concede equipamentos grátis e não permite duplicação ao depositar/retirar ao mesmo tempo. Recursos de casa são pessoais; visitas podem ser permitidas depois, sem acesso de terceiros ao baú por padrão.

Primeira compra e ampliação são transações persistentes. Reinício não perde propriedade, plantas ou itens. Se ocorrer migração, backup e testes com cópia do save antes de aplicar aos dados principais.

## 10. Montarias

Primeira montaria conquistada por missão em R03. Proposta visual: Corcel de Estrada, animal angular e sóbrio, pernas articuladas e arreios simples. Variação posterior: Javali Couraçado ou Lagarto de Escória, usando lógica compartilhada e visual próprio.

Objetivo: diminuir deslocamento já dominado e criar conquista, sem eliminar exploração. Velocidade inicial de referência 1,5× corrida normal, a calibrar por tamanho dos mapas. Sem ataque montado nesta fase.

Montar somente fora de combate, após breve preparação. Desmontar ao atacar; receber golpe desmonta com proteção curta contra travamento se necessária, sem longa queda obrigatória. Dungeons, interiores e trechos estreitos podem bloquear montaria com aviso claro antes de usar.

Montaria não atravessa portão de boss, barreira de missão, abismo, água profunda ou parede. O servidor valida velocidade, tamanho navegável, estado e permissões. Visual deve acompanhar o estado real. Morte/desconexão não mata permanentemente o animal. Uma montaria ativa; coleção simples de desbloqueios.

Não criar reprodução, mercado de montarias, treinamento por dias ou fome. Registrar vínculo e seleção por personagem.

## 11. Asas

Asas são uma conquista visual importante, inspirada na sensação de evolução de equipamento de MU. Primeira obtenção por cadeia Q45–Q47, com materiais fabricados e núcleo de dungeon garantido por vitória elegível.

Asas iniciais: estrutura de ferro escuro e membrana discreta, pequena e legível na câmera. Variação mágica com placas/runa e brilho contido. Equipar em slot dedicado e persistente; animação simples por articulações. Não esconder o corpo, os ataques ou a visão de aliados.

Escopo funcional inicial: impulso aéreo curto representado por deslocamento horizontal validado no chão navegável, com recarga própria. O personagem pode elevar visualmente um pouco, mas não atravessa obstáculos ou abismos. Interface chama de impulso, não voo. É uma escolha prática de implementação inicial, não promessa de voo livre.

Melhoria posterior dentro deste pacote, apenas se a arquitetura suportar de forma consistente: rotas de vento específicas entre plataformas, acionadas por interação e com endpoints autorizados. Servidor valida entrada, missão, destino e transição; não permitir pousar em qualquer lugar. Isso abre lugares antes inacessíveis sem implementar voo universal.

Asas não substituem a esquiva de classe nem dão invulnerabilidade permanente. Definir convivência com cooldowns e controles para evitar cadeias infinitas de fuga. Não acumular velocidade de montaria e asas; desmontar/recusar conforme estado. Nada de voo livre sobre chefes e paredes.

Se o usuário futuramente pedir voo livre, tratar como evolução de navegação, altura, colisão e servidor, não só animação.

## 12. Companheiros de combate

Um companheiro ativo por personagem. Obtenção por resgate e vínculo, com missão e fabricação de alimento. Não é uma compra automática no começo. Não requer comida constante ou manutenção real diária.

### Mini-dragão — primeiro vínculo, 30–35

Pequeno draco de pedra escura e placas de bronze. Anda com saltos curtos ou paira baixo apenas visualmente. Ataque básico de projétil curto e uma habilidade de rajada/interrupção leve com recarga. Não interrompe todos os ataques de boss. Papel: ajuda ofensiva estável e leitura de alvo.

### Fênix de fogo — alternativa, 52–55

Ave angular de asas finas e brasa contida. Projétil de fogo e pulso ofensivo com recarga; uma utilidade de recuperação limitada pode substituir parte do dano. Não concede ressurreição infinita ou imunidade ao fogo. Obtida por missão em R07/D07, não aleatoriamente de cada elemental.

Alvo: acompanha alvo escolhido e só ataca após dono entrar em combate. Não inicia lutas sozinho, não puxa boss e não persegue além da zona do dono. Comando simples: acompanhar/passivo e atacar meu alvo. Não exigir controle tático de RTS.

Orçamento inicial de dano: cerca de 8–12% do dano sustentado de referência de um dono do mesmo patamar; alvo de balanceamento, não fórmula final. Pet não substitui jogador, não carrega personagem sem equipamento e não é obrigatório para a primeira evolução.

Companheiro não rouba XP, golpe final ou elegibilidade de drop; contribuição é atribuída ao dono. Não gera XP separado que acelera a progressão sem controle. Poder deriva de patamar de vínculo/dono com limite explícito; duas melhorias por missões bastam, sem outra árvore longa de níveis.

Se cair, fica inativo por breve período e retorna fora de combate; sem perda permanente ou item consumível obrigatório. Dono morto implica pet passivo/inativo. IA pode reposicionar/teleportar visualmente perto do dono quando fora de combate e preso, mas não infligir dano através de parede ou entre mapas.

Servidor calcula alvo, recarga, alcance e dano. Cliente apenas anima. Até quatro jogadores com seus pets precisam ser considerados em carga e clareza visual. Sem pets adicionais invocados em cadeia.

## 13. Equipamentos e lendários

Qualidades de trabalho: comum, incomum, raro, épico e lendário, com texto/ícone além de cor. Reconciliar com o sistema atual de qualidade e têmpera; não criar segunda raridade incompatível. Bases, qualidade e melhoria são dimensões distintas.

O jogador usa equipamentos comuns e raros para progredir. Lendários são conquistas com identidade e habilidades; não precisam ser requisito para atravessar toda a campanha. Bosses difíceis podem ter recomendação de poder e consumíveis, não exigir um lendário específico.

### Catálogo inicial de oito lendários

| ID | Relíquia / classe | Fonte | Efeito de proposta |
|---|---|---|---|
| L01 | Espada do Sino Partido / Guerreiro | Q37–Q39, nível recomendado 38+ | Contra-ataque bem sucedido prepara onda curta; recarga |
| L02 | Cajado das Últimas Vozes / Mago | Mesma cadeia, alternativa à espada | Interromper canalização prepara projétil espectral adicional; recarga |
| L03 | Escudo da Vigília Morta / Guerreiro | D05, drop ou encomenda por fragmentos | Bloqueio válido gera breve proteção; não acumula sem limite |
| L04 | Manto da Segunda Sombra / Mago | D05/D06, fonte definida no catálogo | Após reposicionar com habilidade, reduz custo da próxima magia; recarga |
| L05 | Machado do Colosso / Guerreiro | Colosso R07 e fragmentos pessoais | Ataque pesado concluído causa impacto de postura em área curta |
| L06 | Foco da Brasa Viva / Mago | D07, drop ou fragmentos | Após sequência ativa de conjuração, prepara explosão pequena; sem loop de procs |
| L07 | Anel do Juramento / ambas | D08 e fragmentos | Alternar habilidade e ataque básico concede benefício breve, com limite |
| L08 | Relíquia da Quimera / ambas | Q61–Q62 e D08 | Melhora relíquia da classe, sem multiplicar todos os atributos |

Primeira cadeia entrega escolha apropriada à classe; não concede espada e cajado juntos sem regra. Após decidir, caminho alternativo posterior permite outra arma via farm.

Para raros de boss: proposta inicial de 2–5% por vitória elegível e um fragmento pessoal garantido. Encomenda em aproximadamente 12–20 vitórias do encontro. Ajustar juntos chance, fragmentos, duração e poder; 20 repetições não é requisito para a campanha. Definir uma quantidade fixa por item no dado final. Mostrar progresso e fonte. Não alegar que esses valores já estão balanceados.

Um proc de item não dispara a si mesmo, outro proc equivalente ou geração infinita de recursos. Pet não ativa efeitos de arma do dono por padrão. Regras de item específico precisam de teste, não texto vago.

Ao mudar equipamento, mostrar diferença física e atributo/efeito. Set completo pode ter aparência forte sem exigir bônus complexo de conjunto nesta fase. Inicialmente um bônus de duas peças e um de quatro, no máximo, se a estrutura já permitir; caso contrário, adiar bônus e manter peças individuais funcionais.

## 14. Economia e fontes de recompensa

Uma moeda principal. Materiais regionais não precisam virar dez moedas novas. Itens de missão pessoais separados visualmente de material negociável. Loja inicial atende recuperação e equipamento básico; não vende lendários prontos.

Recompensas de missão: XP e moeda base, mais um item/receita/desbloqueio quando relevante. Não entregar arma melhor em toda missão, pois isso inutiliza drops. Consumíveis e materiais têm espaço real no percurso.

Preços relativos: suprimento comum acessível em minutos de atividade; arma básica alcançável cedo; receita em uma pequena cadeia; casa em algumas horas de renda líquida; lendários por desafio e persistência. Calcular renda após gastos normais, não só moedas brutas.

Sem taxa real, dinheiro real, lootbox paga ou calendário obrigatório. Venda a NPC abaixo do custo de compra/fabricação considerando insumos compráveis. Verificar ciclos entre fabricar, desmontar, vender e entregar contratos. Transporte pode cobrar pouco, mas jamais deixar o jogador preso numa região sem rota gratuita de retorno.

Armazém, casa, montaria, asa e pet são objetivos persistentes; não pagam novamente em toda sessão.

## 15. Mundo compartilhado e persistência

Servidor é autoridade sobre XP, moedas, inventário, receitas, mineração, cultivo, casa, montaria, asas, pet, missões e passagem. Não aceitar quantidade de dano/recompensa declarada pelo cliente.

Progresso pessoal: nível, classe, especialização, missão, receita conhecida, passagem liberada, casa, baú, plantas, montaria, asas e pet. Estado compartilhado: monstros de campo e eventos de combate. Instâncias e recompensas de dungeon pertencem ao grupo/participante conforme registro próprio.

Ponte e vila: conclusão pessoal concede direito de usar rota/serviço e muda apresentação compatível. Preferir portão/transportador de atalho com autorização a criar duas colisões incompatíveis no mesmo campo compartilhado. Se a obra for global, novos jogadores têm versão adaptada da missão e recebem contribuição equivalente sem reconstruir fisicamente a ponte. Não depender de geometria local invisível ao servidor.

Execução idempotente: ações têm IDs vinculados à conta/personagem. Persistir resultado de compras, receitas, depósitos e entregas no mesmo commit da mutação. Nova tentativa recebe resultado anterior; outro personagem não reutiliza o ID.

Separar personagem e conta conforme arquitetura real. Não compartilhar automaticamente casa, baú ou dinheiro entre todos os personagens sem decisão de produto. Neste pacote, assumir propriedade por personagem, com vínculo à conta apenas para autorização.

Salvar temporizadores como dados do servidor e reconstruir após restart: plantas, respawns, contratos, cooldowns de operações quando necessário. Nunca usar relógio do cliente como prova de colheita pronta.

Migrações versionadas, backup e teste com cópia de banco. Personagem existente não perde classe, inventário ou itens. Se novas curvas alterarem nível calculado, preservar nível e registrar reconciliação de XP explicitamente; não reduzir progresso silenciosamente.

## 16. Conteúdo orientado por dados e limites técnicos

Adaptar o formato já utilizado pelo projeto. Novas definições possíveis: regiões, entradas/saídas, pontos de viagem, monstros, habilidades, spawns, drops, missões, receitas, estações, nós de mineração, sementes, casas, montarias, asas e companheiros.

Obrigatório: IDs únicos, referências válidas, recompensas existentes, metas alcançáveis, pré-requisitos sem ciclo, receitas sem ciclos econômicos lucrativos involuntários, estados de missão retomáveis e fallback de localização segura.

Quando uma definição solicita comportamento ainda não implementado, informar erro/desabilitar o conteúdo em desenvolvimento; não fingir funcionalidade com placeholder silencioso.

Navegação: verificar se o servidor atual usa plano 2D ou altura. Cenário pode sugerir desnível com caminhos existentes. Asas de impulso, rotas autorizadas, elevadores por transição e montaria precisam respeitar essa capacidade. Voo livre, água profunda e escalada universal ficam fora desta expansão.

Carregar/ativar entidades por região ocupada e interesse do cliente quando necessário. Medir tick, CPU, memória, tráfego e renderização com múltiplos jogadores e pets. Não prometer mundo gigante baseado só em builds aprovados.

## 17. Plano de implementação em seis entregas verificáveis

| Entrega | Resultado jogável | Dependências e aceite principal |
|---|---|---|
| M01 — Base de progressão | R01/R02, curva, cadeia Q01–Q16, D01/D02 e evolução 20; incluir preparo, mineração e plantio mínimos usados nessa cadeia | Personagem novo percorre o early sem item raro obrigatório; dados antigos preservados |
| M02 — Profissões úteis | Ampliar e consolidar receitas, estações, mineração e horta comunitária para as regiões seguintes | Sem duplicação; objetivos de ação distinguem posse; nenhum bloqueio por casa |
| M03 — Expedição e lar | R03/R04, D03/D04, montaria, compra de casa, depósito e cultivo doméstico | Comprar/depositar/colher/reconectar funciona; montaria respeita colisões |
| M04 — Cidade fantasma | R05, D05, primeiro lendário e mini-dragão | Cadeia concluível sem sorte; pet auxilia sem puxar lutas ou roubar XP |
| M05 — Asas e especialização | R06, D06, asas e evolução 50 | Prova, escolha e impulso persistem; não ultrapassa barreiras |
| M06 — Midgame completo | R07/R08, D07/D08, fênix, lendários superiores e Q64 | Jornada integrada, objetivos repetíveis, grupo e desempenho medidos |

Se houver dependência de grupo/instância ainda inexistente, implementar a base necessária antes de anunciar dungeon pronta. Missões de craft entram junto com o sistema de M02; M01 pode ser trabalhada internamente, mas só deve ser entregue ao jogador com dependências funcionais, não texto bloqueando campanha.

Entregas são checkpoints recuperáveis, não motivo para pedir autorização a cada arquivo. Continue autonomamente dentro da direção. Após cada checkpoint, deixe atalho funcionando, registre builds/testes executados e ofereça roteiro curto para avaliação humana. Não marque como aprovado o que apenas compilou ou foi inspecionado em captura.

## 18. Verificações obrigatórias e critérios de conclusão

### Fluxos e regressões
- Criar personagem de cada classe e jogar até primeira arma com recursos iniciais reais.
- Completar cadeias de profissão e evolução com dados válidos e inventário limitado.
- Verificar que casa e lendários não bloqueiam a campanha de quem ainda não os tem.
- Testar rotas de retorno, ponto seguro e transições sem teleporte para fora do mapa.
- Reiniciar durante missão, cultivo, dungeon e fabricação; retomar coerentemente.
- Testar duas entregas simultâneas, compra/venda, baú, mineração e recompensa repetida.
- Testar dois personagens e duas contas com o mesmo ID de requisição mal usado.
- Verificar entrada tardia em evento e jogador que chega após ponte/vila já resolvida.
- Testar companheiro com dono morto, desconectado, longe e separado por parede.
- Testar montaria e asas contra barreiras, portões, paredes e arenas de boss.
- Confirmar que especialização não duplica concessão de habilidade ou pontos.

### Balanceamento e experiência
- Medir XP/hora e renda líquida em cada faixa com equipamentos compatíveis.
- Medir vantagem de item raro/lendário sem tornar comum inutilizável.
- Medir duração de boss solo preparado, dupla e grupo, sem prometer sucesso sem execução.
- Verificar tempo real de obtenção da casa e receitas; não calibrar com conta de debug rica.
- Testar farm quando jogador está sem missão e quando está acompanhando contratos.
- Observar Guerreiro e Mago frente a ataques aéreos, armaduras e resistências.
- Verificar que telegráficos deixam tempo de reação na latência suportada.

### Fim do pacote

O midgame só está completo quando há conteúdo funcional de 1–60 e o jogador consegue: evoluir classe duas vezes; cumprir cadeias regionais; fazer contratos; explorar oito regiões; vencer dungeons; obter um lendário por cadeia e perseguir outros por farm; minerar; fabricar; preparar poção/comida; plantar; usar depósito comunitário; comprar casa; usar montaria; equipar asas; vincular mini-dragão ou fênix; e retornar com tudo persistido.

Uma execução com comandos administrativos valida navegação e estados, mas não prova ritmo de 46,5–76 horas. Separar teste acelerado de progressão, simulação de economia e sessão humana normal. Não afirmar que todas as horas de campanha foram jogadas se isso não ocorreu.

## 19. Ideias de mundos após o midgame — planejamento, sem promessa de implementação

| Mundo | Identidade | Desafio / criatura central |
|---|---|---|
| Jardim das Estátuas Vivas | Bosque de figuras petrificadas e templos cobertos por raízes | Górgonas e Medusa; linha de visão |
| Ossário dos Gigantes | Vale dentro de esqueletos colossais, tendas nos ossos e pontes de costela | Ciclopes e gigantes; arenas amplas |
| Mar sem Horizonte | Navios presos em mar escuro e plataformas entre naufrágios | Sereias, afogados e Kraken; tentáculos a partir de terra |
| Catedral Invertida | Torre enterrada com salas em profundidade e sinos abaixo da cidade | Cavaleiros espectrais; atalhos e mecanismos simples |
| Deserto de Vidro Negro | Dunas cortadas por obeliscos e ruínas vitrificadas | Escorpiões, serpentes e golems; cobertura contra rajadas |
| Pântano das Mil Bocas | Troncos ocos e ruídos falsos; aldeia que só fala por placas | Plantas e basiliscos; diferenciar pista e ameaça |
| Lago das Luas Mortas | Margens de água espelhada e ruínas com reflexos diferentes | Espectros e hidra; zonas de segurança |
| Raízes do Último Mundo | Estruturas penduradas em raízes gigantes e poços de névoa | Dragão das raízes; objetivo de expedição lendária |
| Fortaleza do Lobo Preso | Correntes atravessam montanhas e uma fortaleza acompanha a prisão | Lobo lendário; perseguição e recuperação |
| Horizonte da Serpente | Costa onde parte de uma criatura monumental parece ser ilha | Serpente lendária; encontro por partes |

Esses mundos reutilizam sistemas existentes, mas cada um precisa de mapa, encontros e recompensa próprios. Não anunciar como disponíveis no mapa principal até existir acesso jogável.

## 20. Instrução inicial para o Claude

Leia este documento e o estado atual do projeto. Faça um inventário curto de sistemas presentes, parciais e ausentes. Identifique conflitos com decisões anteriores e adote as alterações explícitas deste documento. Defina as migrações e a ordem necessária para concluir M01–M06.

Comece a implementar a primeira entrega compatível com o que já existe, incluindo dependências reais. Não entregue só planejamento e não recomece o jogo. Use gráficos simples e o kit visual atual para concentrar esforço na progressão e integração.

Preserve banco, contas, IDs e personagens. Faça backup antes de migrar. Tome decisões pequenas, registre-as e prossiga. Pergunte apenas sobre conflito material de direção ou risco aos dados.

Ao relatar, separe: implementado; compilado; testado automaticamente; executado no jogo; avaliado por pessoa; pendente. Não usar uma contagem de testes como substituto de verificar a jornada completa.

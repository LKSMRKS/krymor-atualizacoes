# Cinzas da Maré

> **[DECISÃO OFICIAL]** `Docs/DECISOES_OFICIAIS.md` prevalece sobre este GDD. Classes oficiais: Guerreiro, Arqueira, Mago, Sacerdotisa (onde o texto diz Bastião/Rastreiro/Caldeiro/Cantor, leia o arquétipo correspondente, com redesenho futuro). Direção visual: lotes 0–8. Marco A: só Guerreiro; PvP fora do protótipo.

## Documento de projeto para um RPG cooperativo de farm difícil

**Destinatário:** Lukas Marques e o agente de implementação no Claude.  
**Versão:** 1.0 — 29 de outubro de 2026.  
**Nome de trabalho:** Cinzas da Maré. Nome proposto, sem pesquisa de disponibilidade de marca.  
**Estado:** projeto de design e catálogo preenchido; não é jogo implementado nem balanceamento validado.

O objetivo é construir um RPG de ação online privado que reproduza a sensação de esforço, perigo, conquista de equipamentos e convivência dos RPGs antigos de evolução lenta. MU é referência de experiência, não fonte de arquivos, personagens ou mapas. Este documento define uma alternativa com mundo, catálogo, regras e direção visual próprios. A primeira entrega deve ser pequena e realmente multiplayer; o catálogo completo é a expansão planejada.

Todos os números de combate, drop, economia e duração abaixo são hipóteses iniciais. Devem ser colocados em dados editáveis e calibrados com sessões reais. Não representam as taxas oficiais do MU nem uma promessa de duração.

## 1. Decisões centrais

- PC Windows como cliente inicial; servidor dedicado Linux; interface em português brasileiro.
- RPG 3D com câmera elevada, combate por alvo, deslocamento por clique e atalho opcional WASD.
- Servidor privado para 8 jogadores simultâneos; grupos de até 4. Não projetar um MMO de milhares de jogadores.
- Personagem persistente, nível máximo 120, quatro classes disponíveis desde a criação.
- Farm e PvE são a atividade principal. PvP fica restrito a uma zona opcional e duelos consentidos.
- Evolução lenta, equipamento valioso, inventário limitado, preparação e retorno à cidade.
- Sem reset de nível, renascimento que soma atributos infinitos, auto-farm ou recompensas por ficar offline.
- Sem loja de poder, assinatura, gacha, passe de temporada ou energia diária.
- Sem leilão global, cerco de castelo, guildas competitivas ou montarias no primeiro lançamento.
- O mundo funciona com 1 jogador; áreas avançadas podem exigir 2–4. Nenhuma missão central exige PvP.
- Conteúdo permanente, sem wipe automático. Backups e migração de saves fazem parte do produto.

**Sessão desejada:** preparar 10 minutos; viajar 5; caçar 25–45; decidir entre insistir, enfrentar um elite ou voltar; melhorar equipamento e trocar materiais por 10 minutos. Uma sessão sem item raro ainda deixa materiais, XP, conhecimento do mapa e progresso de receita.

**Três diferenciais próprios:** marés de cinza alteram rotas e composição dos encontros; expedições acumulam recursos recuperáveis em caso de morte; relíquias mudam decisões de combate em vez de adicionar apenas números maiores.

## 2. Referências e limites de originalidade

A documentação oficial do MU consultada apresenta classes, regiões, monstros, eventos e sistemas de equipamentos, inclusive melhorias com risco e penalidades associadas a PK. Esses elementos ajudam a identificar a experiência buscada, mas não foram usados como catálogo a ser renomeado. A configuração de servidores privados varia; não existe aqui uma taxa universal de “MU difícil”. [S1–S4]

| Sensação ou função desejada | Solução deste projeto | Expressão que não será copiada |
|---|---|---|
| Caçar por horas e reconhecer uma conquista | Equipamentos com bases previsíveis e afixos limitados | Nomes, estatísticas e modelos de itens do MU |
| Avançar para regiões perigosas | Arquipélago petrificado e rotas de maré | Geometria, minimapas e sequência de regiões do MU |
| Melhorar arma assumindo risco | Oficina de têmpera com graus +0 a +8 | Joias, receitas, interface e máquina característica |
| Identificar classe à distância | Ferramentas, postura e silhueta próprias | Trajes e silhuetas características de classes existentes |
| Ganhar aparência prestigiosa | Cerâmica reparada, metal incandescente e mantos de cinza | Asas e conjuntos reconhecíveis do jogo de referência |
| Competir por recurso | Uma região opcional com caravanas de salvamento | Eventos, castelos e narrativa do MU |
| Cultivar comunidade | Grupo, troca direta e mural de ofertas | Interface e terminologia proprietária |

No Brasil, a Lei 9.610 distingue ideias e regras de jogos da expressão protegida. Isso não autoriza copiar arte, textos, código ou o conjunto expressivo de um jogo. Trocar nomes não basta. A marca também exige análise própria; o INPI mantém ferramentas de busca. Estas são orientações para reduzir risco, não certificação jurídica de que o resultado é “100% legal”. [S5–S7]

**Regras de produção:** código novo; nenhum cliente ou servidor vazado; nada extraído do MU; nenhuma reprodução de mapas a partir de imagens; ícones e efeitos próprios; ativos de terceiros apenas com licença verificada; registro de autoria, origem, licença e atribuição de cada arquivo. Referências visuais devem ser múltiplas e gerais. Antes de divulgação comercial, revisar nome, identidade final e direitos dos ativos com profissional adequado.

## 3. Mundo e direção artística

O mar desapareceu durante uma noite de incêndio mineral. Seu leito virou uma massa de sal negro, destroços e organismos petrificados. Periodicamente, correntes de cinza percorrem antigos canais e despertam criaturas presas na crosta. Refúgios humanos sobrevivem em plataformas, diques e fornalhas. Os jogadores são recuperadores licenciados: saem para buscar combustível, ferramentas e fragmentos dos mecanismos que mantêm a cidade viva.

Não há escolhido universal nem império demoníaco genérico comandando todos os inimigos. Existem fauna adaptada, autômatos industriais, saqueadores e pessoas cristalizadas. O conflito principal é entender e conter a próxima maré, enquanto a economia depende de retirar recursos do mesmo fenômeno.

> **[DECISÃO OFICIAL]** Direção visual oficial: fantasia medieval sombria dos lotes 0–8. Leito marinho/sal negro e cerâmica não são mais obrigatórios; podem ficar em regiões específicas. O parágrafo abaixo está superado onde conflitar.

**Visual:** fantasia industrial artesanal; couro encerado, ferro oxidado, tecido pesado, porcelana rachada, cordas e sal. Proporções humanas estilizadas, câmera legível, efeitos curtos. Cores: ferrugem, carvão, osso, verde mineral e âmbar. Brilho forte reservado a ataques perigosos e equipamento muito aprimorado. Interface de placas de cerâmica e papel de levantamento, com barras horizontais; evitar reproduzir HUDs reconhecíveis.

> **[DECISÃO OFICIAL]** Silhuetas das classes serão definidas a partir dos Lotes 1–2 (Guerreiro, Arqueira, Mago, Sacerdotisa). O parágrafo abaixo está superado.

**Silhuetas:** Bastião largo com escudo de comporta; Rastreiro assimétrico com capas curtas e cordame; Caldeiro com tubos cerâmicos e pinça longa; Cantor do Lastro com sinos pequenos e placas suspensas. Sem asas equipáveis.

**Áudio:** vento entre cascos, rangido de metal, sinos abafados, batidas de forja. Música discreta, identidade melódica original. Ataques e avisos devem continuar identificáveis com música desligada.

## 4. Inventário fechado do lançamento completo

| Categoria | Quantidade | Regra de contagem |
|---|---:|---|
| Classes | 4 | Uma identidade e recurso por classe |
| Habilidades ativas | 32 | Oito por classe, além do ataque básico |
| Passivas | 24 | Seis por classe |
| Mapas | 12 | Uma cidade, oito regiões e três masmorras |
| Monstros comuns | 48 | Seis por região; masmorras reutilizam parte deles |
| Elites | 12 | Oito regionais e quatro de masmorra |
| Chefes | 12 | Oito regionais, três de masmorra e um de evento |
| Total de inimigos catalogados | 72 | 48 + 12 + 12; variantes de maré não contam |
| Armas de base | 48 | Oito famílias × seis patamares |
| Armaduras | 100 | Quatro classes × cinco conjuntos × cinco peças |
| Equipamentos de mão secundária | 12 | Três famílias × quatro patamares |
| Joias equipáveis | 18 | Seis anéis, seis amuletos, seis broches |
| Relíquias | 8 | Um espaço por personagem |
| Equipamentos únicos | 12 | Um por chefe, separado das bases |
| Total de equipamentos | 198 | Não conta rolagens, upgrades ou cosméticos |
| Materiais | 40 | 12 locais, oito comuns, oito reagentes, 12 de chefe |
| Consumíveis | 18 | Lista fechada na seção 12 |
| Objetos de missão | 12 | Não negociáveis, separados do inventário |
| Total de definições de item | 268 | 198 + 40 + 18 + 12 |
| NPCs com função | 12 | Aparências próprias; sem contar figurantes |
| Missões | 40 | 12 centrais, 12 secundárias, oito contratos, quatro de classe, quatro finais |
| Eventos | 4 | Todos independentes de horário fixo da vida real |
| Receitas | 32 | 18 consumíveis, oito reagentes, seis relíquias |

Este é o escopo preenchido do nosso jogo, não uma contagem de todo o conteúdo do MU. Uma definição de item não equivale necessariamente a um modelo 3D exclusivo. Peças modulares compartilham malhas e materiais de maneira deliberada.

## 5. Classes e habilidades

> **[DECISÃO OFICIAL]** As quatro classes agora são Guerreiro, Arqueira, Mago e Sacerdotisa. Só o Guerreiro entra no Marco A. As seções 5.2–5.4 são rascunhos a redesenhar para os novos arquétipos, não tradução de nomes.

Atributos: **Força**, **Destreza**, **Vigor** e **Sintonia**. Todos começam em 10; recebem três pontos livres por nível após o primeiro. O nível 120 possui 357 pontos distribuídos, além dos 40 iniciais. Equipamento concede poder adicional. Redistribuir pontos custa moeda, não destrói personagem; uma redistribuição gratuita até o nível 20.

Habilidades desbloqueiam nos níveis 1, 5, 10, 18, 28, 40, 60 e 85, na ordem das tabelas. Todas as oito podem estar na barra. Evoluem automaticamente com nível; não há 32 livros aleatórios obrigatórios. Passivas liberam em 10, 25, 45, 65, 85 e 105, na ordem listada para cada classe, sem pontos adicionais para gastar. Pode-se equipar no máximo três passivas ao mesmo tempo; trocar a seleção exige estar fora de combate. Regenerações expressas em porcentagem multiplicam a taxa, não o estoque. Reduções de custo não afetam habilidades que geram calor ou recurso.

Valores são protótipos: **P** = poder ofensivo apropriado; dano indicado antes de defesa. Cada habilidade informa recarga e custo. Sem custo significa zero. Golpes comuns têm recuperação de animação e não podem ser cancelados para multiplicar dano.

### 5.1 Bastião

> **[DECISÃO OFICIAL]** Esta seção vale para o **Guerreiro**. Efeitos, recargas, custos e passivas mantidos; nomes renomeados conforme a tabela em `DECISOES_OFICIAIS.md` (ex.: Guarda de Comporta → Guarda de Escudo).

Defensor e lutador de curta distância; espadas ou martelos de uma mão e escudo. Principal: Força/Vigor. Recurso Fôlego, 100 máximo, regenera 8/s em combate e 15/s fora. Bloqueio ativo reduz regeneração a zero. Pode jogar solo com dano moderado.

| Habilidade | Efeito inicial | Recarga | Custo |
|---|---|---:|---:|
| Golpe de Cunha | 160% P em um alvo, 10% de redução de armadura por 4 s | 4 s | 15 |
| Guarda de Comporta | Reduz 40% do dano frontal por até 3 s; movimento a 50% | 10 s | 20 |
| Varredura de Corrente | 110% P em cone de 3 m | 7 s | 25 |
| Chamado de Ferro | Provoca comuns por 3 s em 5 m; em chefe apenas aumenta ameaça | 14 s | 15 |
| Passo de Ancoragem | Avança 4 m, 90% P e interrompe um alvo | 12 s | 20 |
| Rebate | Próximo bloqueio em 2 s devolve 180% P; sem refletir dano recebido | 14 s | 20 |
| Muralha de Lastro | Aliados próximos recebem 15% menos dano por 6 s | 40 s | 40 |
| Ruptura de Quilha | 280% P em linha curta; 30% mais contra inimigo interrompido | 24 s | 35 |

Passivas: Rebites Vivos (+8% vida); Postura Firme (+10% resistência a empurrão); Fôlego de Forja (+15% regeneração de Fôlego); Guarda Compartilhada (+5% eficácia de Muralha); Cunha Profunda (+2 s de redução de armadura); Contraimpacto (+12% dano de Rebate). Valores somam apenas dentro do efeito indicado.

### 5.2 Rastreiro

Atirador e controlador de rotas; arcos ou bestas de duas mãos. Principal: Destreza/Vigor. Recurso Concentração, 100 máximo, regenera 10/s parado e 5/s em movimento. Não consome munição por disparo; a logística fica nos consumíveis.

| Habilidade | Efeito inicial | Recarga | Custo |
|---|---|---:|---:|
| Tiro de Estaca | 150% P, alcance 12 m | 4 s | 15 |
| Laço de Cabo | Enraíza comum por 2 s; chefes recebem lentidão de 10% | 12 s | 20 |
| Rajada Rasante | Três projéteis de 55% P, limite de três acertos por alvo | 8 s | 25 |
| Recuo Curto | Move 3 m para trás, sem invulnerabilidade | 9 s | 15 |
| Marca de Caça | Um alvo recebe +8% dano do grupo por 8 s; não acumula | 20 s | 20 |
| Armadilha de Rebite | Arma em 1 s, 180% P em área de 2 m; apenas uma ativa | 16 s | 25 |
| Disparo de Travessia | Linha perfurante de 220% P, máximo quatro alvos | 18 s | 30 |
| Vigília | Por 6 s, +20% velocidade de ataque ao permanecer parado | 40 s | 35 |

Passivas: Olho de Sal (+2 m de alcance); Passada Silenciosa (-15% raio de detecção fora de combate); Fio Tenso (+8% dano em alvo marcado); Recuperar Foco (+15% regeneração de Concentração parado); Trilho Seguro (+1 s de raiz em comuns); Rebite Serrado (+12% dano de armadilha).

### 5.3 Caldeiro

Conjurador de área e desgaste; bastões de duas mãos ou condutores de uma mão com foco. Principal: Sintonia/Vigor. Calor vai de 0 a 100; habilidades geram calor. Dissipa 7/s, após 1 s sem gerar. Ao atingir 100, fica impedido de usar habilidades por 3 s e retorna a 60; ataque básico continua disponível.

| Habilidade | Efeito inicial | Recarga | Calor gerado |
|---|---|---:|---:|
| Jato de Escória | 140% P em linha de 8 m | 3 s | 15 |
| Poça Vítrea | Área de 2 m causa 35% P/s por 4 s; não acumula consigo | 10 s | 25 |
| Estalo Térmico | 120% P e interrupção, alcance 8 m | 12 s | 20 |
| Resfriar Núcleo | Dissipa 35 de calor ao canalizar por 2 s | 18 s | 0 |
| Crosta Protetora | Absorve dano igual a 15% da vida máxima por 5 s | 20 s | 15 |
| Turbilhão de Fuligem | 150% P em área e lentidão de 25% por 3 s | 15 s | 25 |
| Fratura de Vidro | 230% P; +20% em alvo sobre Poça Vítrea | 18 s | 30 |
| Fornalha Aberta | Canal de 4 s, 85% P/s, move-se a 40% | 45 s | 40 |

Passivas: Câmara Ampla (+15 limite de calor; superaquecimento usa esse limite); Dissipadores (+15% dissipação); Vidro Cortante (+12% dano de Poça); Cinza Isolante (+5 pontos percentuais na barreira); Choque de Forja (+15% dano contra alvo interrompido por 2 s); Última Válvula (superaquecimento dura 2 s).

### 5.4 Cantor do Lastro

Suporte, cura limitada e dano contínuo; sinos ou turíbulos de uma mão com selo. Principal: Sintonia/Vigor. Ressonância de 0 a 100, começa em 50 ao sair da cidade; cada ataque básico válido gera 8; fora de combate converge para 50 a 5/s. Não gera recurso atacando objetos ou aliados.

| Habilidade | Efeito inicial | Recarga | Custo |
|---|---|---:|---:|
| Nota Grave | 130% P em alvo e gera 10 Ressonância | 4 s | 0 |
| Remendo Harmônico | Cura aliado em 130% P, alcance 9 m | 7 s | 25 |
| Círculo de Lastro | Zona de 3 m: +12% defesa por 6 s; não acumula | 16 s | 20 |
| Dissonância | 100% P em cone e interrupção | 12 s | 20 |
| Canto Persistente | Cura 25% P/s durante 6 s em um aliado | 15 s | 25 |
| Sino de Retorno | Revive aliado caído com 25% vida; canaliza 6 s interrompíveis | 90 s | 60 |
| Eco Punitivo | 40% P/s por 5 s em alvo; não acumula consigo | 12 s | 20 |
| Acorde de Abrigo | Grupo em 5 m recebe barreira de 12% da vida de cada alvo por 6 s | 45 s | 50 |

Passivas: Voz Firme (+8% cura); Ritmo de Trabalho (+2 recurso por ataque básico); Lastro Profundo (+2 s de círculo); Harmonia Longa (+2 s de Canto Persistente); Bronze Cruel (+15% dano de Eco); Último Acorde (reviver restaura 35% vida). Reviver não remove penalidades já aplicadas após a morte definitiva.

**Composição:** Bastião segura pressão, Rastreiro escolhe alvos, Caldeiro limpa grupos e Cantor sustenta. Nenhuma classe é obrigatória para matar comuns; nenhuma fica sem ataque eficiente. Vida dos chefes de masmorra escala com participantes, mas padrões e certos mecanismos continuam favorecendo cooperação.

## 6. Combate e progressão numérica

### 6.1 Fórmulas iniciais

Usar arredondamento apenas no resultado final, mínimo 1 para dano positivo.

- Vida máxima = 180 + 14 × nível + 9 × Vigor + bônus de equipamento.
- Poder físico = dano base da arma + 2,2 × atributo principal + 1,2 × nível.
- Poder de sintonia = dano base da arma + 2,2 × Sintonia + 1,2 × nível.
- Bastião usa Força; Rastreiro usa Destreza. Cura usa poder de sintonia.
- Redução por armadura = armadura / (armadura + 180 + 18 × nível do atacante), limitada a 65%.
- Dano final = P × coeficiente × (1 - redução) × (1 - resistência pertinente) × multiplicadores.
- Resistências de calor, sal e eco ficam entre -25% e 60%; ataques físicos usam apenas armadura, elementais apenas resistência. Não aplicar as duas defesas ao mesmo dano sem decompor seus componentes.
- Crítico começa em 5%; afixos podem elevar até 35%; dano crítico é 150% fixo no lançamento.
- Roubo de vida total limitado a 3%; aplica apenas ao dano direto efetivo, com 25% de eficácia em chefes. Cura não pode critar.
- Velocidade de ataque adicional limitada a 40%; velocidade de movimento adicional limitada a 15%.
- Não há erro aleatório de acerto entre jogador e monstro. Evasão ocorre pelo deslocamento e pelas áreas telegrafadas.
- Armadura e resistências de monstros também obedecem aos limites desta seção. Vida recuperada nunca ultrapassa a vida máxima. Fora de combate por 10 s, jogadores recuperam 1% da vida máxima por segundo; isso permite retomar farm, mas não sustentar combate contínuo. Comuns sofrem controles; elites têm duração de controle reduzida à metade; chefes não são atordoados, mas habilidades marcadas como interrompíveis podem ser canceladas.

### 6.2 Ritmo de evolução

Definir XP de monstro comum de nível M como `100 × M`. XP necessária para passar de L a L+1 é `100 × L × K(L)`. Nível 120 não acumula nova barra. Valores K abaixo expressam mortes equivalentes de inimigos do mesmo nível, antes de missões, grupo, mortes e bônus.

| Nível atual | K por nível | XP para exemplo | Meta acumulada de horas ativas ao fim da faixa |
|---|---:|---:|---:|
| 1–19 | 60 | Nível 10: 60.000 | 8–15 h |
| 20–39 | 150 | Nível 30: 450.000 | 30–50 h |
| 40–59 | 280 | Nível 50: 1.400.000 | 65–100 h |
| 60–79 | 450 | Nível 70: 3.150.000 | 115–170 h |
| 80–99 | 650 | Nível 90: 5.850.000 | 190–270 h |
| 100–119 | 900 | Nível 110: 9.900.000 | 280–400 h |

As horas são metas de playtest, não derivação exata da fórmula. Medir mortes equivalentes por hora, XP de contratos e efeito dos grupos. Se o jogador precisa repetir uma ação sem decisão por horas, melhorar encontros e objetivos antes de simplesmente aumentar XP.

Monstros até cinco níveis abaixo dão XP integral. De seis a dez abaixo dão 50%; mais de dez abaixo dão 10%. Monstros acima não recebem bônus extra só pela diferença. Grupos: XP total do abate multiplica por `1 + 0,35 × (N-1)` e divide igualmente entre participantes elegíveis. Quatro pessoas recebem 51,25% da XP individual por abate, compensada pela velocidade e segurança. Elegibilidade: mesma região, até 30 m do encontro, participação em dano, cura efetiva ou ameaça nos últimos 30 s; regras de suporte precisam de testes.

Diferença de mais de 15 níveis dentro do grupo reduz em 50% a XP de quem está abaixo do maior nível menos 15. Não aumenta loot nem poder dos monstros de mundo aberto. XP de elite = 4 comuns do seu nível; chefe regional = 15; chefe de masmorra = 25; chefe de evento = 25. Missões únicas de uma faixa devem representar cerca de 10–15% do percurso, não substituir farm.

### 6.3 Alvos de combate

Com equipamento apropriado e sem afixos perfeitos: comum isolado leva 6–12 s; grupo de três leva 15–30 s com risco; elite, 45–90 s solo; chefe regional, 2–4 min com quatro; masmorra completa, 25–45 min. O jogador não deve tankar cinco inimigos da própria faixa sem usar recursos. Chefes telegrafam golpes perigosos por pelo menos 0,8 s, ajustável para latência.

Para gerar protótipos de inimigos de nível M: vida comum `180 + 18M + 0,45M²`; dano por golpe `12 + 2M + 0,12M²`; armadura `20 + 5M`; ataque a cada 2 s. Arquétipos modificam esses valores: ágil 0,7 vida/0,8 dano/1,4 velocidade; tanque 1,7 vida/0,8 dano/0,7 velocidade; conjurador 0,8 vida/1,2 dano; enxame 0,35 vida/0,45 dano; atirador 0,85 vida/1,0 dano; combatente 1,0 em tudo. Elites: vida ×5, dano ×1,4. Chefes regionais: vida ×25, dano ×1,7; masmorra: vida ×35, dano ×1,8; evento: vida ×45, dano ×1,8. São sementes de simulação: confrontar imediatamente com o tempo de morte desejado, especialmente no nível 100+.

## 7. Mundo e rotas

A cidade conecta a R1. R1 leva a R2 e R3; ambas levam a R4. R4 abre R5 e R6; R5/R6 levam a R7. R7 leva a R8. D1 entra por R3; D2 por R6; D3 por R8. Não há corredor copiado de outro jogo. Passagens físicas e atalhos desbloqueados por missão substituem teleporte irrestrito.

| ID | Mapa | Faixa | Função e formato | Recurso exclusivo local |
|---|---|---|---|---|
| H0 | Porto das Brasas | Segura | Cidade vertical curta, praça de oficinas, doca e arquivo | Sucata de Oficina |
| R1 | Baixios de Cal | 1–20 | Canais rasos secos, diques quebrados, rotas largas | Cal Viva |
| R2 | Estaleiro Partido | 15–35 | Cascos inclinados e corredores com saídas laterais | Madeira de Quilha |
| R3 | Jardins de Sal | 25–45 | Terraços de plantas minerais e bolsões de emboscada | Flor Salina |
| R4 | Garganta das Roldanas | 40–60 | Pontes, guinchos e três níveis conectados | Cabo Temperado |
| R5 | Brejo de Vidro | 55–75 | Superfície vitrificada, ilhas e gêiseres previsíveis | Vidro Vivo |
| R6 | Ossário das Máquinas | 65–90 | Autômatos gigantes desmontados e passagens internas | Engrenagem Oca |
| R7 | Planalto da Fuligem | 80–105 | Ruínas e tempestades que alternam coberturas | Carvão Pálido |
| R8 | Margem do Último Sino | 100–120 | Península petrificada, altares e fossos de eco | Sal Noturno |
| D1 | Cisterna das Vozes | 35–50 | Masmorra em três alas; válvulas controlam corredores | Lodo Harmônico |
| D2 | Fornalha Submersa | 75–95 | Masmorra com alternância de calor e resfriamento | Núcleo Cerâmico |
| D3 | Coração do Dique | 110–120 | Masmorra de três mecanismos e chefe final | Ferro de Maré |

Dimensão de protótipo: cidade 100 × 100 m; regiões 180 × 180 m; masmorras com 12–18 salas modulares. Dimensões não precisam gerar terreno quadrado: são orçamento de área navegável. Cada região tem seis pontos principais de farm, duas rotas de retorno, um local de elite, um local de chefe e um atalho. Evitar monstros nas saídas seguras.

**Maré de cinza:** cada 45 minutos de servidor ativo, duração de 10 minutos; contador pausa quando vazio. Em duas regiões sorteadas, muda uma passagem, acrescenta um grupo de elite e concede +25% material local. Vida inimiga +15%, XP sem bônus. Não aprisiona jogadores: saídas sempre acessíveis. Primeira implementação troca grupos e hazards; geometria dinâmica só depois de validar navegação.

**PvP:** somente um setor explicitamente demarcado de R7, com entrada confirmada; há rota PvE paralela e todos os recursos têm fonte PvE. R7 inteiro não é território hostil.

## 8. Bestiário completo

Cada linha regional lista exatamente seis comuns. Usar os arquétipos na ordem: combatente, ágil, tanque, atirador, conjurador, enxame. Para faixa mínima A e máxima B do mapa, os seis níveis são arredondados em A + (B-A) × [0,15; 0,30; 0,45; 0,60; 0,75; 0,90]. O comportamento descrito complementa o arquétipo.

| Região | Seis monstros comuns e comportamento distintivo |
|---|---|
| R1 | **Caranguejo de Cal:** pinça frontal; **Rato de Dique:** foge e retorna pela lateral; **Cascudo de Sal:** carapaça frontal; **Cuspidor de Lodo:** projétil lento; **Limpador Febril:** nuvem irritante; **Larva de Crosta:** grupos pequenos |
| R2 | **Carpinteiro Oco:** machadada carregada; **Cão de Cordame:** investida curta; **Carregador de Quilha:** empurrão; **Arpoador Ferrugento:** linha perfurante; **Sinaleiro Sem Rosto:** fortalece aliados; **Carrapato de Corda:** perseguição em bando |
| R3 | **Jardineiro Mineral:** foice em arco; **Garça de Sal:** salto lateral; **Javali Cristalino:** carga lenta; **Vagem de Agulhas:** dispara parada; **Peregrino Eflorescente:** raiz no solo; **Ácaro de Sal:** explosão fraca ao morrer |
| R4 | **Operário de Gancho:** puxa alvo próximo; **Lagarto de Cabo:** escala lateral decorativa e flanqueia; **Prensa Errante:** golpe pesado; **Besteiro do Pedágio:** cobertura; **Vigia das Roldanas:** ativa zona de lentidão; **Besouro de Graxa:** deixa faixa escorregadia |
| R5 | **Pescador Vitrificado:** golpe de vara; **Enguia de Vidro:** avança entre poças; **Tartaruga de Escória:** armadura alta; **Sapo de Sílica:** projétil em arco; **Viúva do Brejo:** poça corrosiva; **Mosquito de Brasa:** enxame móvel |
| R6 | **Autômato de Rebite:** sequência de dois golpes; **Galgo Mecânico:** persegue alvo ferido; **Escavador Cego:** cone subterrâneo telegrafado; **Torreta Ambulante:** tiro e reposicionamento; **Afinador Defeituoso:** repara autômatos; **Piolho de Engrenagem:** cerca jogadores |
| R7 | **Saqueador de Cinza:** alterna alvo por ameaça; **Corredor de Fuligem:** avanço pelas costas; **Carvoeiro Blindado:** pancada circular; **Fuzileiro de Sucata:** arma de pressão artesanal; **Orador da Fumaça:** obscurece área; **Traça de Carvão:** consome cobertura temporária |
| R8 | **Peregrino de Lastro:** golpe que pune ficar parado; **Cervo de Eco:** teleporte curto anunciado; **Guardião de Basalto:** protege uma direção; **Arpoador do Fim:** marca antes de atirar; **Cantor Vazio:** canal interrompível; **Fragmento Vivo:** orbita e converge |

D1 usa criaturas de R2/R3; D2 de R5/R6; D3 de R7/R8. Seus níveis acompanham a faixa da masmorra, mantendo o mesmo ID de espécie com um perfil de encontro. Não contar recoloração como monstro novo.

### Elites

| ID | Nome | Local | Mecânica adicional |
|---|---|---|---|
| E01 | Pinça de Pedreiro | R1 | Quebra a própria carapaça após errar uma pancada |
| E02 | Capataz de Cabo | R2 | Prende dois pontos com corda que pode ser cortada |
| E03 | Garça Coroada | R3 | Muda de posição após três ataques recebidos |
| E04 | Fiscal da Ponte | R4 | Escudo frontal e zona vulnerável nas costas |
| E05 | Tartaruga Espelhada | R5 | Anuncia uma reflexão parcial de projéteis por 2 s |
| E06 | Afinador-Mor | R6 | Cura um autômato; canal deve ser interrompido |
| E07 | Carvoeiro de Máscara | R7 | Solta carga de combustível antes de explodir |
| E08 | Cervo de Nove Ecos | R8 | Duas imagens falsas sem colisão; original faz som distinto |
| E09 | Zelador da Cisterna | D1 | Alterna válvulas e zonas seguras |
| E10 | Vigia de Porcelana | D2 | Fragiliza após resfriamento |
| E11 | Arauto do Dique | D3 | Alterna resistência de calor e eco |
| E12 | Sentinela de Contrapeso | D3 | Exige afastar-se da borda antes do impacto |

### Chefes e seus equipamentos exclusivos

Todos os chefes possuem aviso de ataque, janela de recuperação e ao menos uma decisão além de bater. O item único tem 8% de chance por vitória elegível; cada vitória também concede um fragmento pessoal. Doze fragmentos do mesmo chefe permitem encomendar seu item, consumindo-os. A encomenda dá o item vinculado; drop direto é negociável até equipado.

| ID | Chefe e local | Mecânica principal | Item único e efeito |
|---|---|---|---|
| B01 | Mãe das Crostas — R1 | Protege crias; destruir crostas abre flancos | **Broche da Ninhada:** ao matar comum, cura 1% vida; recarga 3 s |
| B02 | Mestre da Quilha — R2 | Balança um casco e lança arpões em linhas | **Martelo do Estaleiro:** +15% dano a inimigo sob redução de armadura |
| B03 | A Flor que Caminha — R3 | Alterna raízes e chuva de agulhas | **Arco de Seiva Mineral:** alvo marcado recebe veneno de sal de 15% P/s por 3 s; recarga 8 s |
| B04 | Cobrador de Pontes — R4 | Corta caminhos, exige acionar contrapesos | **Escudo do Pedágio:** bloquear recupera 5 Fôlego; recarga 2 s |
| B05 | Baleia de Vidro — R5 | Corpo enterrado; bater nos cristais expostos | **Amuleto de Sílica:** +12% resistência de calor, -3% movimento |
| B06 | O Motor Faminto — R6 | Invoca sucata que precisa ser afastada | **Condutor de Engrenagens:** Estalo Térmico dissipa 8 calor ao interromper |
| B07 | Viúva da Fornalha — R7 PvE | Fumaça esconde posição; sons indicam ataque | **Botas do Carvoeiro:** ignora lentidão de terreno, sem ignorar controles inimigos |
| B08 | O Último Sineiro — R8 | Sequência de três zonas sonoras | **Sino do Horizonte:** Remendo em alvo abaixo de 30% vida cura 20% a mais |
| B09 | Coro Afogado — D1 | Três emissores compartilham vida; interromper canto | **Selo da Cisterna:** +10% duração de barreiras, sem aumentar absorção |
| B10 | Coração de Cerâmica — D2 | Superaquece; jogadores alternam válvulas | **Bastão da Segunda Queima:** após Resfriar Núcleo, próxima magia custa 10 calor a menos; mínimo zero |
| B11 | O Dique Vivo — D3 | Três fases: pressurização, ruptura e refluxo | **Relíquia do Refluxo:** após interromper, +10% dano por 5 s; recarga 20 s |
| B12 | Peregrino da Maré Negra — evento | Percorre três pontos; tempestade fecha arena gradualmente | **Anel do Retorno:** uma vez por expedição, dívida de XP por morte reduzida à metade |

Chefes regionais reaparecem 45 minutos depois da morte, contados apenas com servidor ocupado. Evento B12 exige materiais, não calendário. Masmorras têm cooldown de recompensa de 30 minutos por personagem após conclusão, mas podem ser reabertas para ajudar amigos; sem recompensa durante cooldown. Mostrar isso antes de entrar. Chefes regionais têm vida fixa para 2–4; masmorras usam fator de vida `0,6 + 0,4N` sobre base de um participante, N fixado ao iniciar o chefe, até quatro. A base das fórmulas da seção 6 é multiplicada por esse fator apenas em masmorra.

## 9. Catálogo dos 198 equipamentos

**Espaços equipados:** arma principal, mão secundária, cabeça, torso, mãos, pernas, pés, dois anéis, amuleto, broche e relíquia. Armas de duas mãos ocupam a mão secundária. Não há cinto, brincos, asas ou mascote com atributos neste escopo.

**Patamar** é a base do item; **qualidade** é a quantidade de afixos; **têmpera** é o upgrade. Essas três dimensões não geram novas definições de item: uma Espada de Cabo +4 continua sendo a mesma base com instância diferente.

### 9.1 As 48 armas

Cada célula abaixo é uma arma. Os níveis mínimos dos patamares são 1, 15, 35, 60, 85 e 110. Não existem requisitos adicionais de atributos no lançamento, evitando personagens presos por distribuição ruim.

| Família e classe | P1 | P2 | P3 | P4 | P5 | P6 |
|---|---|---|---|---|---|---|
| Espada — Bastião, 1 mão | Espada de Chapa | Espada de Cabo | Espada de Dique | Espada de Escória | Espada de Lastro | Espada de Refluxo |
| Martelo — Bastião, 1 mão | Martelo de Oficina | Martelo de Rebite | Martelo de Quilha | Martelo de Vidro | Martelo de Prensa | Martelo de Maré |
| Arco — Rastreiro, 2 mãos | Arco de Junco | Arco de Cordame | Arco de Salgueiro Seco | Arco de Lâmina Vítrea | Arco de Cinza | Arco de Horizonte |
| Besta — Rastreiro, 2 mãos | Besta de Sucata | Besta de Estaleiro | Besta de Contrapeso | Besta de Sílica | Besta de Guindaste | Besta de Ruptura |
| Bastão — Caldeiro, 2 mãos | Bastão de Cobre | Bastão de Tubulação | Bastão de Crosta | Bastão de Fornalha | Bastão de Núcleo | Bastão de Dique |
| Condutor — Caldeiro, 1 mão | Condutor de Fio | Condutor de Bobina | Condutor de Porcelana | Condutor de Escória | Condutor de Fuligem | Condutor de Refluxo |
| Sino — Cantor, 1 mão | Sino de Latão | Sino de Vigia | Sino de Cisterna | Sino de Bronze Negro | Sino de Lastro | Sino de Maré |
| Turíbulo — Cantor, 1 mão | Turíbulo de Lata | Turíbulo de Cais | Turíbulo de Cal | Turíbulo de Vidro | Turíbulo de Carvão | Turíbulo do Horizonte |

Dano base por patamar: 12, 32, 70, 125, 195, 280. Multiplicadores por família: espada 1,00; martelo 1,18; arco 1,00; besta 1,20; bastão 1,25; condutor 0,90; sino 1,00; turíbulo 1,10. Intervalos de ataque: 1,0; 1,3; 1,0; 1,4; 1,3; 0,9; 1,1; 1,3 s, respectivamente. Poder de habilidade deriva do dano base, portanto comparar DPS de habilidades também, não apenas ataque básico. Bastão abre mão do foco defensivo.

### 9.2 As 100 armaduras

Cada conjunto abaixo possui exatamente cinco peças: **Capuz/Elmo, Casaco/Peitoral, Luvas, Calças e Botas**. Para gerar os nomes, usar o tipo de peça seguido de “de” e do nome do conjunto, ajustando contrações em português. Bastião usa Elmo/Peitoral; as demais classes usam Capuz/Casaco. Os 20 conjuntos geram 100 definições, sem contar bônus como itens extras.

| Classe | Nível 1 | Nível 20 | Nível 45 | Nível 75 | Nível 105 |
|---|---|---|---|---|---|
| Bastião | Chapa Batida | Guarda do Cais | Contrapeso | Forja Fria | Dique Negro |
| Rastreiro | Couro de Marisma | Vigia de Cordame | Caça Salina | Andarilho de Vidro | Olho da Maré |
| Caldeiro | Tecido Chamuscado | Isolante de Cobre | Vidreiro Errante | Núcleo Cerâmico | Fornalha Branca |
| Cantor | Pano de Vigília | Bronze Velado | Coro da Cisterna | Lastro Silencioso | Último Horizonte |

Orçamento de armadura total dos cinco patamares: 70, 170, 330, 560, 850. Multiplicador de classe: Bastião 1,40; Rastreiro 1,00; Caldeiro 0,70; Cantor 0,85. Distribuir por peças: cabeça 15%, torso 35%, mãos 10%, pernas 25%, pés 15%. Arredondar cada peça. Não há vida base nas armaduras; vida adicional vem de afixos.

Bônus de conjunto exigem três e cinco peças do mesmo conjunto, não apenas mesma classe. Em todos os patamares, o conjunto de Bastião dá +5% vida / -8% dano frontal recebido; Rastreiro +5% Concentração máxima / +8% dano em alvo marcado; Caldeiro +5% dissipação / +8% dano em área; Cantor +5% cura / -10% custo de Ressonância, arredondado para cima. Não multiplicar bônus entre patamares. Equipamentos únicos podem quebrar o conjunto deliberadamente.

### 9.3 As 12 mãos secundárias

| Família | Nível 1 | Nível 30 | Nível 65 | Nível 100 |
|---|---|---|---|---|
| Escudo — Bastião | Tampa de Ferro | Comporta Portátil | Anteparo de Vidro | Parede de Lastro |
| Foco — Caldeiro | Bobina Simples | Câmara Salina | Núcleo Selado | Condensador Negro |
| Selo — Cantor | Placa de Vigília | Medalha de Bronze | Lacre de Cisterna | Selo do Refluxo |

Escudos dão 25/70/150/260 armadura. Focos dão 5/12/22/35 Sintonia. Selos dão 20/60/120/220 vida. Todos podem receber qualidade e têmpera. Bloqueio ativo vem da habilidade, não de chance aleatória embutida no escudo.

### 9.4 As 18 joias

| Tipo | Nome | Nível mínimo | Efeito base |
|---|---|---:|---|
| Anel | Anel de Cobre Frio | 5 | +25 vida |
| Anel | Anel de Cabo Trançado | 20 | +8 Destreza |
| Anel | Anel de Cal Selada | 35 | +8% resistência de sal |
| Anel | Anel de Vidro Fosco | 55 | +8% resistência de calor |
| Anel | Anel de Ferro Surdo | 80 | +8% resistência de eco |
| Anel | Anel de Horizonte Partido | 105 | +3% crítico |
| Amuleto | Medalhão de Oficina | 5 | +5 Vigor |
| Amuleto | Pêndulo de Quilha | 20 | +8 Força |
| Amuleto | Gota Salina | 35 | +12 Sintonia |
| Amuleto | Dente de Sílica | 55 | +5% dano contra elites |
| Amuleto | Coração de Sucata | 80 | +100 vida |
| Amuleto | Nó de Refluxo | 105 | +6% dano contra chefes |
| Broche | Rebite de Aprendiz | 5 | +10 armadura |
| Broche | Gancho de Retorno | 20 | -5% custo de reparo |
| Broche | Flor de Sal Morta | 35 | +5% recuperação de poção |
| Broche | Olho de Vidreiro | 55 | +2% crítico |
| Broche | Marca de Carvoeiro | 80 | +6% resistência de calor |
| Broche | Insígnia do Dique | 105 | +60 armadura |

Não equipar dois anéis da mesma definição. Joias recebem até dois afixos, mas não recebem têmpera. Descontos de reparo limitados a 15%. Joias não aumentam chance de item único.

### 9.5 As oito relíquias de base

| ID | Nome | Nível | Aquisição | Efeito e contrapartida |
|---|---|---:|---|---|
| RL01 | Âncora de Bolso | 20 | Receita | +15% resistência a empurrão; -3% movimento |
| RL02 | Lente de Maré | 30 | Receita | +2 m de alcance; -5% vida |
| RL03 | Braseiro Mudo | 40 | Receita | +10% dano de calor; -8% resistência de calor |
| RL04 | Sino Rachado | 50 | Receita | +12% cura; -8% dano próprio |
| RL05 | Coração de Cabo | 65 | Receita | +8% dano abaixo de 50% vida; poções recuperam 5% menos |
| RL06 | Ampola de Sal Negro | 80 | Receita | +10% dano de sal; -8% resistência de sal |
| RL07 | Bússola sem Norte | 95 | Elites de R7, chance 2% | +8% material local obtido; -5% vida |
| RL08 | Fragmento de Horizonte | 110 | Elites de R8/D3, chance 2% | +8% dano a chefes; -10% dano a comuns |

Relíquias não recebem afixos, qualidade aleatória ou têmpera. Os 12 únicos da seção 8 completam os 198 equipamentos. Únicos não recebem afixos aleatórios; armas, escudos e botas únicos aceitam têmpera. Seu nível mínimo é o limite superior da região menos cinco, ou D1=45, D2=90, D3=115, B12=110. Arma única usa a base do maior patamar não superior a esse requisito; escudo usa regra equivalente. Botas únicas usam a maior base de armadura disponível ao nível, multiplicador neutro 1,00, equipáveis por todas as classes.

### 9.6 Qualidades e afixos

Qualidades: **Comum** zero afixos; **Trabalhado** um; **Raro** dois; **Primoroso** três. Único é categoria fixa separada. Não usar várias qualidades com nomes quase iguais. Cor deve vir acompanhada de texto e símbolo.

Pool de 16 afixos: Força, Destreza, Vigor, Sintonia, vida fixa, armadura fixa, crítico, velocidade de ataque, resistência de calor, resistência de sal, resistência de eco, dano contra elites, dano contra chefes, eficácia de poção, redução de custo de recurso e roubo de vida.

Afixos numéricos por patamar q=1..6: atributos `[2q, 4q]`; vida `[10q, 20q]`; armadura `[5q, 10q]`; crítico `[1, 3]%`; velocidade `[2, 5]%`; resistências `[3, 8]%`; dano especial `[2, 5]%`; poção `[3, 6]%`; custo de recurso `[2, 4]%`; roubo de vida `[0,3, 0,7]%`. Sorteio uniforme no intervalo, arredondamento a inteiro salvo roubo de vida com uma casa. Cada afixo é único por item. Máximo global de redução de custo: 20%.

Armas: atributos, crítico, velocidade, dano especial, roubo de vida. Armaduras: atributos, vida, armadura, resistências. Mãos secundárias: atributos, vida, resistências, redução de recurso. Joias: atributos, resistências, poção, crítico. Atributos incompatíveis com a classe são removidos do pool dos itens exclusivos de classe. Armaduras e joias usam q aproximado pela faixa de nível mínima nas faixas das armas.

## 10. Têmpera e risco

A melhoria ocorre apenas na cidade, em uma tela que mostra chance, custo, consequência de falha e item resultante. O servidor resolve a tentativa uma única vez e grava antes de responder. Desconectar não repete a rolagem.

Cada grau concede +4% da estatística base ofensiva ou defensiva do item, somado: +8 equivale a +32%. Não aumenta afixos, cura especial, efeitos únicos ou bônus de conjunto. Focos aumentam sua Sintonia base; selos aumentam sua vida base. Arredondar no final.

| Destino | Chance | Custo de reagente | Custo em Marcas | Falha |
|---|---:|---|---:|---|
| +1 | 100% | 1 Resina de Fixação | 300 × q | Não se aplica |
| +2 | 100% | 2 Resinas de Fixação | 500 × q | Não se aplica |
| +3 | 90% | 1 Banho de Têmpera | 900 × q | Mantém grau |
| +4 | 80% | 2 Banhos de Têmpera | 1.500 × q | Mantém grau |
| +5 | 65% | 1 Núcleo de Pressão | 2.500 × q | Cai um grau |
| +6 | 50% | 2 Núcleos de Pressão | 4.000 × q | Cai um grau |
| +7 | 35% | 1 Lacre de Maré | 7.000 × q | Cai dois graus |
| +8 | 25% | 2 Lacres de Maré | 12.000 × q | Cai dois graus |

q é o patamar equivalente de 1 a 6. Materiais e moeda são consumidos em toda tentativa. Não existe proteção paga. **O padrão não destrói o equipamento**, mas perder graus e materiais é significativo. A dureza deve vir da preparação e do custo, sem tornar uma semana de jogo irrecuperável por um clique. Modo adicional de destruição total não faz parte do lançamento.

Não existe bônus de chance oculto. Não somar a chance dos graus como se fosse probabilidade de obter +8; há retornos de estado. Antes de liberar +7/+8, simular pelo menos 100 mil trajetórias e publicar custo mediano, percentil 90 e 99, além de comparar com horas reais de farm. Se o custo mediano de +8 ultrapassar o orçamento decidido nos testes, ajustar chance ou custo; não alegar que a tabela já está equilibrada.

## 11. Morte, expedição e PvP

### 11.1 Morte PvE

Até nível 10: retorno à cidade e perda de durabilidade, sem dívida de XP. Depois: ao chegar a zero vida, estado caído por 20 s; Cantor pode reviver. Expirado o prazo, ocorre morte definitiva. Durante o estado caído, inimigos ignoram o jogador; ele não se move, coleta ou participa de trocas.

Na morte definitiva: dívida de XP equivalente a 4% da barra necessária para o próximo nível; 50% da XP ganha paga a dívida até quitá-la; dívida acumulada limitada a 12% da barra. Não perde nível. No nível 120, não há dívida de XP; os custos materiais continuam. Perde 10 pontos de durabilidade dos itens equipados, de um máximo de 100, e 5% da moeda carregada; moeda no banco não é afetada.

30% das unidades de materiais negociáveis coletadas desde a última saída da cidade ficam em uma bolsa no local. Quantidade arredondada para baixo por pilha. Moeda, equipamentos, fragmentos pessoais de chefes e objetos de missão nunca entram nessa bolsa. A procedência da coleta é registrada pelo servidor; materiais levados da cidade não entram na perda. Banco e retorno à cidade encerram a expedição.

Bolsa pessoal só pode ser recolhida pelo dono, dura 30 minutos de servidor ativo e persiste se o servidor reiniciar. Ao morrer novamente, uma nova bolsa pode ser criada; limite de duas bolsas, com aviso de que a mais antiga será perdida. Desconectar não recupera nem apaga bolsa. Evitar spawn em ponto sem navegação; usar posição segura mais próxima do local.

**Por que é punitivo:** morte custa deslocamento, reparo, moeda, tempo de XP e parte do que foi coletado. **Limite:** equipamento equipado não desaparece na morte padrão. Isso conserva a possibilidade de retomar a aventura com os amigos.

Durabilidade zero reduz somente a estatística base do item em 50%; não destrói. Reparo de um ponto custa `max(1, 2q)` Marcas por item. Desgaste natural: um ponto por 50 eventos de combate válidos do item, contando no máximo uma vez por segundo, além da morte. Não cobrar desgaste por frame ou por alvo de área.

### 11.2 PvP secundário

- Duelo apenas com aceite de ambos; termina em 1 de vida e não impõe perdas.
- Setor de salvamento de R7 é opcional e sinalizado. Entrada dá aviso e proteção por 15 s, cancelada ao atacar; área de entrada não permite dano ou bloqueio corporal.
- PvP usa multiplicador inicial de dano 0,40; cura em PvP multiplicada por 0,60; controle máximo contínuo 2 s seguido de 4 s de imunidade a novo controle rígido.
- Mortes por PvP não geram dívida de XP, moeda do morto para o vencedor ou perda de equipamento. Geram durabilidade e perda apenas de 30% dos materiais obtidos dentro do setor naquela expedição, como pacote contestável.
- Pacote contestável pode ser saqueado por outro jogador após 30 s; somente ele, não a bolsa PvE pessoal.
- Atacar primeiro gera Infâmia por 20 minutos de jogo ativo; matar alvo que não iniciou agressão acrescenta 20 minutos, até duas horas. Um personagem infame não pode iniciar troca direta e paga 20% mais por reparo. Sem prisão, perda de conta ou reset.
- O melhor equipamento PvE não depende desse setor. A região oferece densidade de materiais +15%, sem recurso exclusivo. Não concede XP ou moeda por matar jogadores, para reduzir farm combinado.
- Sair em combate mantém personagem no mundo por 15 s; desconexão por falha de rede segue a mesma regra, anunciada antes. Não matar instantaneamente por desconectar.

## 12. Materiais, consumíveis e receitas

### 12.1 Os 40 materiais

**12 locais:** Sucata de Oficina; Cal Viva; Madeira de Quilha; Flor Salina; Cabo Temperado; Vidro Vivo; Engrenagem Oca; Carvão Pálido; Sal Noturno; Lodo Harmônico; Núcleo Cerâmico; Ferro de Maré. Fontes na tabela dos mapas. Sucata vem de desmontagem na cidade, não de matar NPCs.

**Oito comuns:** Ferro Bruto; Couro Seco; Fibra de Corda; Pó Mineral; Óleo Espesso; Fragmento Cerâmico; Essência de Eco; Carvão Comum. Ferro/Pó caem de combatentes/tanques; Couro/Fibra de ágeis/atiradores; Óleo/Cerâmica de máquinas e elites; Essência de conjuradores; Carvão de enxames. Quando uma criatura não faz sentido narrativo para uma fonte, usar saco de salvamento do cenário com a mesma tabela; não forçar couro em robô.

**Oito reagentes fabricados:** Resina de Fixação; Banho de Têmpera; Núcleo de Pressão; Lacre de Maré; Tinta de Afixo; Pó de Reparo; Liga de Salvamento; Catalisador de Eco.

**12 fragmentos pessoais de chefes:** Fragmento da Ninhada; Fragmento da Quilha; Fragmento da Flor; Fragmento da Ponte; Fragmento da Baleia; Fragmento do Motor; Fragmento da Viúva; Fragmento do Sineiro; Fragmento do Coro; Fragmento da Cerâmica; Fragmento do Dique; Fragmento do Peregrino. Correspondem a B01–B12, nessa ordem; um garantido por vitória elegível, vinculado ao personagem. Não são moeda livre de troca.

### 12.2 As 18 receitas de consumíveis

Todos os resultados abaixo produzem uma unidade; chances 100%; o custo de moeda é adicional. Poções compartilham recarga de 12 s; cura ocorre em 4 s, interrompida apenas por morte. Comida exige 5 s fora de combate, dura 20 minutos e apenas um alimento pode estar ativo. Antídotos têm recarga compartilhada de 15 s. Granada e sinalizador têm suas próprias recargas.

| ID | Consumível | Ingredientes | Marcas | Efeito |
|---|---|---|---:|---|
| C01 | Tônico Fraco | 2 Pó Mineral | 20 | Cura 15% vida máxima |
| C02 | Tônico de Campo | 3 Pó Mineral + 1 Flor Salina | 60 | Cura 25% vida máxima |
| C03 | Tônico Concentrado | 4 Pó Mineral + 2 Flor Salina + 1 Vidro Vivo | 150 | Cura 35% vida máxima |
| C04 | Sais de Fôlego | 2 Cal Viva + 1 Carvão Comum | 30 | Restaura 30 Fôlego; exclusivo Bastião |
| C05 | Extrato de Foco | 2 Flor Salina + 1 Óleo Espesso | 40 | Restaura 30 Concentração; exclusivo Rastreiro |
| C06 | Ampola Refrigerante | 2 Pó Mineral + 1 Fragmento Cerâmico | 40 | Dissipa 25 Calor; exclusivo Caldeiro |
| C07 | Óleo Harmônico | 1 Óleo Espesso + 2 Essência de Eco | 50 | Restaura 30 Ressonância; exclusivo Cantor |
| C08 | Pasta Antissal | 2 Couro Seco + 1 Cal Viva | 30 | Remove efeito negativo de sal |
| C09 | Pomada de Queimadura | 2 Óleo Espesso + 1 Flor Salina | 30 | Remove queimadura |
| C10 | Gota de Silêncio | 2 Essência de Eco + 1 Pó Mineral | 40 | Remove um efeito de eco dissipável |
| C11 | Ração de Dique | 2 Cal Viva + 1 Flor Salina | 30 | +5% vida máxima |
| C12 | Caldo de Forja | 2 Carvão Comum + 2 Flor Salina | 40 | +5% poder físico |
| C13 | Chá de Cinza | 2 Carvão Pálido + 1 Flor Salina | 80 | +5% poder de sintonia |
| C14 | Kit de Remendo | 1 Pó de Reparo + 2 Fibra de Corda | 100 | Recupera 20 durabilidade de um item fora de combate |
| C15 | Farol de Retorno | 1 Liga de Salvamento + 2 Essência de Eco | 120 | Canaliza 8 s para voltar à cidade; dano interrompe |
| C16 | Granada de Cal | 3 Cal Viva + 1 Ferro Bruto | 80 | Lentidão 20% em 3 m por 4 s; recarga 30 s |
| C17 | Sinalizador de Ruína | 1 Óleo Espesso + 2 Carvão Comum | 50 | Marca local para grupo e ilumina 30 s; recarga 30 s |
| C18 | Incenso de Vigília | 2 Essência de Eco + 1 Sal Noturno | 120 | -15% raio de detecção por 5 min; quebra ao atacar |

A composição é alquímica ficcional, não receita culinária real. C01–C10 também podem cair prontos; C01/C04–C07 vendidos na cidade para prevenir bloqueio inicial de classe. Consumo valida a classe antes de gastar o item.

### 12.3 As oito receitas de reagentes

| ID | Resultado | Ingredientes | Marcas |
|---|---|---|---:|
| F01 | 1 Resina de Fixação | 4 Sucata de Oficina + 2 Óleo Espesso | 50 |
| F02 | 1 Banho de Têmpera | 2 Resina de Fixação + 3 Cal Viva + 2 Madeira de Quilha | 150 |
| F03 | 1 Núcleo de Pressão | 2 Banho de Têmpera + 3 Cabo Temperado + 2 Vidro Vivo | 450 |
| F04 | 1 Lacre de Maré | 2 Núcleo de Pressão + 3 Ferro de Maré + 3 Sal Noturno | 1.200 |
| F05 | 1 Tinta de Afixo | 3 Essência de Eco + 3 Lodo Harmônico + 2 Flor Salina | 250 |
| F06 | 1 Pó de Reparo | 3 Ferro Bruto + 2 Fragmento Cerâmico | 60 |
| F07 | 1 Liga de Salvamento | 3 Ferro Bruto + 2 Engrenagem Oca + 1 Carvão Pálido | 200 |
| F08 | 1 Catalisador de Eco | 4 Essência de Eco + 2 Núcleo Cerâmico + 2 Sal Noturno | 500 |

Tinta permite rerrolar um afixo existente; custa 1 Tinta + 500q Marcas. Primeiro uso fixa qual afixo poderá ser alterado naquela instância; cada nova rolagem substitui obrigatoriamente o anterior, com prévia do pool e confirmação antes de gastar. Não permite rerrolar qualidade, criar novo afixo, alterar único ou violar pool.

### 12.4 As seis receitas de relíquias

| ID | Resultado | Ingredientes | Marcas |
|---|---|---|---:|
| F09 | Âncora de Bolso | 10 Ferro Bruto + 6 Madeira de Quilha + 2 Liga de Salvamento | 800 |
| F10 | Lente de Maré | 8 Flor Salina + 6 Fragmento Cerâmico + 2 Banho de Têmpera | 1.400 |
| F11 | Braseiro Mudo | 8 Carvão Comum + 6 Cabo Temperado + 2 Núcleo de Pressão | 2.000 |
| F12 | Sino Rachado | 10 Lodo Harmônico + 8 Essência de Eco + 2 Tinta de Afixo | 2.800 |
| F13 | Coração de Cabo | 12 Cabo Temperado + 6 Vidro Vivo + 3 Liga de Salvamento | 4.000 |
| F14 | Ampola de Sal Negro | 10 Sal Noturno + 6 Núcleo Cerâmico + 2 Catalisador de Eco | 6.000 |

Total: 18 receitas C + 14 receitas F = 32. Têmpera, desmontagem, rerrolagem, encomendas de únicos e invocação de evento são serviços/sistemas separados, não entram nessa contagem.

## 13. Loot e economia

### 13.1 Rolagens por morte

Moeda: comum dá `4 + 2M` Marcas; elite ×4; chefe ×15, dividida igualmente entre participantes elegíveis. Aplicar multiplicador de 0,2 para monstros mais de dez níveis abaixo, evitando farm ótimo em regiões triviais. Moeda recolhida automaticamente em 8 m, com efeito visual discreto.

Para cada comum, rolar independentemente: 12% material local, 18% material comum, 2% consumível C01–C10 adequado à faixa e 1,5% equipamento de base. Material vem em uma unidade. Elite: 60%, 80%, 10%, 20%, respectivamente. Chefes: dois equipamentos de base garantidos por grupo, quatro materiais locais e quatro comuns, além de uma rolagem compartilhada de 8% de seu único e um fragmento pessoal por jogador elegível. Nenhuma qualidade primorosa é garantida por chefe.

Qualidade condicional ao equipamento: comum 82/15/2,8/0,2%; elite 50/35/13/2%; chefe 20/45/30/5% para Comum/Trabalhado/Raro/Primoroso. As probabilidades de cada linha somam 100%. Um Primoroso de comum tem chance total 0,003%, aproximadamente um em 33.333 abates em expectativa, considerando qualquer base. Isso é um prêmio excepcional, não caminho obrigatório de progressão. Em elite, a chance total é 0,4%; em chefe há dois sorteios a 5% cada. Avaliar essa raridade nos testes antes de liberar.

Para equipamento de base, escolher categoria: arma 35%, armadura 45%, mão secundária 10%, joia 10%. Escolher uniformemente uma base cujo nível mínimo não exceda o nível do inimigo e fique no maior patamar elegível, com 80% de peso, ou no patamar anterior com 20%; sem patamar anterior, usar apenas o atual. Joias seguem os seis níveis da própria tabela. Filtrar categorias sem candidatos e renormalizar. Relíquias e únicos não entram nesta tabela genérica.

Em solo, 70% das escolhas de arma/armadura/mão secundária são da classe atual; 30% de qualquer classe, incluindo a atual. Em grupo, o filtro de 70% inclui as classes presentes, sem multiplicar chance por jogadores repetidos. A chance restante mantém comércio e surpresas.

**Distribuição em grupo:** materiais e equipamentos negociáveis usam rodízio de destinatário por categoria, salvo configuração consensual de sorteio antes do combate. Cada destinatário tem 90 s para coletar; depois, o item continua reservado ao grupo por mais 90 s e então desaparece. Quem entra no grupo durante o combate de chefe só participa do próximo encontro. Definir elegibilidade no início e confirmar contribuição ao fim. Fragmento pessoal não depende do rodízio.

### 13.2 Fontes direcionadas

Cada região fornece seu material exclusivo, mas nenhuma classe depende de uma só espécie. R1/R2 priorizam armamento inicial; R3/D1 fornecem materiais de rerrolagem; R4/R5 sustentam têmpera intermediária; R6/D2 dão componentes de relíquias; R7/R8/D3 alimentam o fim de jogo. Todos os conjuntos de base seguem os níveis de drop; não prender as botas de uma classe a um chefe PvP.

Chance independente não garante prazo: para chance p e n tentativas, probabilidade de ao menos um sucesso é `1-(1-p)^n`. Para o único de chefe a 8%, após 12 vitórias há aproximadamente 63% de chance de drop direto; a encomenda por 12 fragmentos cobre os demais. Fragmentos pessoais são inegociáveis e impedem inflar a garantia por contas extras.

### 13.3 Moeda e comércio

Moeda única: **Marcas**. Conta inicial recebe 300 por personagem, uma vez; pacote inicial não negociável. Proteção contra criação infinita: itens iniciais vendem por zero e as 300 Marcas iniciais ficam em saldo de tutorial não transferível, gasto primeiro apenas em reparos e poções até nível 10; saldo restante é removido ao concluir tutorial. Moeda ganha jogando é livre.

Drenos: reparo, poções, têmpera, receitas, rerrolagem, viagem, redistribuição e taxa de 3% sobre moeda enviada em troca, paga pelo remetente. Redistribuição custa `100 × nível`, após uso gratuito. Viagem entre marcos descobertos custa `20 × nível mínimo do destino`, exige 10 s sem combate e não funciona dentro de masmorra ou setor PvP. Deslocamento físico é gratuito.

Venda ao NPC: equipamentos comuns valem 20q, trabalhados 40q, raros 80q e primorosos 160q; únicos 200q com confirmação; materiais comuns 2, locais 3, reagentes 10; fragmentos pessoais não vendem. Consumíveis vendem por 10% do custo de fabricação em moeda. Não vender insumo de receita por valor que permita lucro infinito de compra/fabricação/venda.

Desmontar equipamento de base concede `q + número de afixos` Sucatas de Oficina; únicos concedem 2q, com confirmação. Nenhuma desmontagem devolve reagentes de têmpera; evitar ciclo infinito. Itens comprados no tutorial não rendem sucata. Banco: 120 espaços compartilhados na conta; inventário: 48 espaços; pilhas até 99; equipamentos ocupam um espaço. Materiais e fragmentos não têm peso físico; capacidade é por espaços. Filtros e organizar pilhas são essenciais.

O mural de ofertas é texto com item referenciado e contato, sem custódia ou compra automática. A troca direta exige proximidade de 5 m, fora de combate, dois aceites; qualquer alteração remove ambos os aceites. Inventários e moeda mudam em uma transação atômica. Itens reservados não podem ser vendidos, consumidos, desmontados ou negociados em outra janela. Uma oferta precisa indicar a taxa total; não pode enviar saldo que deixe insuficiente a moeda necessária para pagar a taxa.

**Indicadores:** moeda criada e destruída por hora; riqueza mediana; custo de reparar como proporção de farm; tempo até primeiro raro; distribuição de upgrades; participação em fontes antigas. Alvos iniciais: poções/reparos consumirem 15–30% da renda de uma sessão bem-sucedida; iniciante manter saldo positivo; upgrade alto consumir múltiplas sessões. Nenhuma meta foi medida ainda.

## 14. NPCs e quarenta missões

### 14.1 Os doze NPCs

| Nome | Função | Personalidade e posição |
|---|---|---|
| Mara Rebite | Ferreira, reparo e têmpera | Direta, luvas enormes; oficina central |
| Oren das Ampolas | Alquimia e consumíveis | Meticuloso, conta gotas em voz alta; bancada lateral |
| Tessa do Lastro | Banco e armazenamento | Ex-recuperadora, pragmática; cofre da doca |
| Nilo Três-Cabos | Troca e mural | Intermediário bem-humorado; praça |
| Sora Vela | Cartografia e viagens | Marca caminhos em tecido; torre baixa |
| Ivo Cinzento | Licença de expedição e tutorial | Paciente, cicatriz de sal; portão |
| Bruna Comporta | Instrutora de Bastião | Valoriza proteger sem imobilidade; pátio |
| Cael da Linha | Instrutor de Rastreiro | Observador, fala pouco; passarela |
| Veda Cerâmica | Instrutora de Caldeiro | Experimentadora cuidadosa; forno |
| Lume dos Sinos | Instrutor de Cantor | Memória musical excepcional; abrigo |
| Dário Sondagem | Contratos e chefes | Coleciona amostras; mapa de expedições |
| Ena do Arquivo | História, fragmentos e encomendas | Historiadora desconfiada; arquivo |

Todos permanecem no hub; missões usam pontos de cenário, evitando criar NPCs funcionais extras fora da contagem. Figurantes posteriores não concedem missões.

### 14.2 As doze missões centrais e objetos de missão

Cada missão concede XP equivalente a cinco comuns do nível mínimo, moeda igual a vinte comuns e o desbloqueio indicado. Q01 também entrega equipamento inicial comum de nível 1, sem venda ou desmontagem. Objetos de missão ficam em coleção separada e não podem ser trocados. A sequência Q01–Q12 é linear; desafios opcionais são separados.

| ID / nível | Título | Objetivo concreto | Objeto único | Desbloqueio |
|---|---|---|---|---|
| Q01 / 1 | Licença para Voltar | Conversar com Ivo, equipar arma e derrotar cinco criaturas R1 | Licença de Recuperador | Banco, grupo e expedições |
| Q02 / 8 | O Dique Respira | Visitar três pontos de vazamento R1 | Medidor Rachado | Atalho de R1 |
| Q03 / 15 | Madeira que Não Flutua | Coletar dez Madeiras de Quilha em R2 | Registro do Estaleiro | Oficina avançada e rotas R2/R3 |
| Q04 / 25 | As Flores Cortam | Coletar oito Flores Salinas e examinar uma estufa | Semente Petrificada | Cartografia R3 |
| Q05 / 35 | Vozes na Água Seca | Abrir D1 e derrotar E09 | Chave de Válvula | Acesso permanente a D1 |
| Q06 / 40 | Preço da Travessia | Ativar três guinchos em R4 | Roda de Contrapeso | Acesso R5/R6 |
| Q07 / 55 | Debaixo do Vidro | Examinar três gêiseres e derrotar E05 | Lâmina Translúcida | Atalho R5 |
| Q08 / 65 | Uma Máquina com Fome | Sabotar dois alimentadores e derrotar E06 | Diagrama Incompleto | Cartografia R6 |
| Q09 / 75 | O Forno Ainda Canta | Acionar válvulas de D2 e derrotar B10 | Núcleo Frio | Receitas de catalisador |
| Q10 / 85 | A Cinza Tem Dono | Recuperar três caixas em área PvE de R7 | Lista de Salvamento | Cartografia R7; não exige PvP |
| Q11 / 100 | O Sino Antes da Maré | Ativar três escutas em R8 e sobreviver a uma maré | Partitura Mineral | Entrada D3 |
| Q12 / 110 | Fechar o que Está Vivo | Derrotar B11 e devolver registro à Ena | Registro do Refluxo | Contratos finais e invocação B12 |

### 14.3 As doze secundárias

Recompensa padrão: XP de três comuns do nível mínimo e moeda de dez; as exceções abaixo substituem a moeda. São concluídas uma vez por personagem.

| ID / nível | Título e responsável | Objetivo | Recompensa adicional |
|---|---|---|---|
| S01 / 3 | Pregos para Mara | Desmontar um equipamento elegível | 2 Resinas de Fixação |
| S02 / 5 | Ampolas Contadas — Oren | Fabricar três C01 | 3 C01 |
| S03 / 7 | Espaço no Cofre — Tessa | Depositar e retirar um item | Libera organização automática |
| S04 / 12 | Marca no Tecido — Sora | Descobrir os seis pontos de farm de R1 | Revela minimapa de R1 |
| S05 / 18 | Corda Curta — Nilo | Entregar oito Fibras de Corda | 1 Banho de Têmpera |
| S06 / 25 | Onde a Garça Pisa — Dário | Derrotar dez Garças de Sal | 2 C02 |
| S07 / 35 | O Silêncio da Cisterna — Lume | Interromper cinco Peregrinos Eflorescentes ou emissores D1 | 1 Tinta de Afixo |
| S08 / 45 | Roldana Travada — Bruna | Completar encontro de proteção em R4 | 2 Kits de Remendo |
| S09 / 60 | Calor sob Medida — Veda | Atravessar três gêiseres sem ser atingido | 2 Núcleos de Pressão |
| S10 / 70 | Rastro entre Dentes — Cael | Rastrear e derrotar E06 | 2 Ligas de Salvamento |
| S11 / 90 | Cinza na Margem — Ena | Examinar quatro inscrições na rota PvE R7 | História adicional e 1 Catalisador |
| S12 / 105 | Luz de Volta — Ivo | Recuperar uma bolsa de treino em R8 | 3 Faróis de Retorno |

A bolsa de treino de S12 é objeto de encontro, não um item colecionável adicional; não força suicídio para concluir a missão.

### 14.4 Os oito contratos repetíveis

Um contrato por região; rotativos entre grupos de monstros, mas sem prêmio diário e sem obrigação de entrar em horário fixo. Aceitar no máximo dois simultâneos. Cada contrato exige vinte abates elegíveis da região e três materiais locais; paga moeda de vinte comuns do nível mínimo do mapa e XP de oito comuns. Recarga de 20 minutos após entrega; recompensa sempre no servidor.

K01 Limpar os Baixios; K02 Desobstruir o Estaleiro; K03 Podar os Cristais; K04 Liberar os Guinchos; K05 Resfriar o Brejo; K06 Desligar as Sucatas; K07 Guardar a Rota de Cinza; K08 Calar os Ecos. K07 aceita apenas objetivos PvE. Todos são fornecidos por Dário.

### 14.5 Quatro missões de classe

Todas no nível 28, com instrutor correspondente, sem promoção de classe ou novos itens obrigatórios. Recompensa: título e uma redistribuição gratuita adicional. São alternativas, uma disponível por personagem conforme classe, mas quatro definições no jogo.

CL01 **Segurar a Linha:** Bastião protege três mecanismos por 90 s em encontro R4, com dificuldade reduzida ao nível 28. CL02 **Acertar a Janela:** Rastreiro interrompe três canais com armadilhas ou tiros em alvos de treino. CL03 **Domar a Fornalha:** Caldeiro conclui três ondas sem superaquecer. CL04 **Fazer Voltar:** Cantor mantém três aliados de treino vivos e revive um. Aliados de treino são entidades temporárias, não NPCs de cidade adicionais.

### 14.6 Quatro missões finais

FQ01 **Cartógrafo do Vazio:** visitar os 12 mapas; recompensa cosmética de bandeira pessoal. FQ02 **Ofício Perfeito:** obter um item +6; recompensa título, sem reagentes reembolsados. FQ03 **Quatro Vozes:** concluir D3 em grupo com ao menos dois jogadores; recompensa emblema, sem exigir as quatro classes. FQ04 **A Maré Não Leva Tudo:** derrotar B12; recompensa relato final e visual alternativo de capa sem atributos. Uma vez por personagem, XP de dez comuns do nível 110 quando ainda abaixo de 120; sem conversão da XP em moeda no máximo.

## 15. Quatro eventos e fim de jogo

| Evento | Gatilho | Atividade | Recompensa |
|---|---|---|---|
| Maré de Cinza | Ciclo de 45 min ativos | Rotas e encontros alterados por 10 min em duas regiões | +25% materiais locais durante evento |
| Caravana de Lastro | Grupo inicia na cidade, custo 10 Madeiras de Quilha + 10 Cabos Temperados | Escoltar carga por R2/R4 por 12–18 min | Por jogador: 4 materiais locais e moeda de 20 comuns nível 40 |
| Vigília das Comportas | Depois de Q06, interação em R4, cooldown 30 min | Seis ondas com reparo de mecanismos entre elas | Dois equipamentos de base por grupo, tabela de elite nível 55 |
| Peregrino da Maré Negra | Depois de Q12, grupo entrega 4 Lacres de Maré + 4 Catalisadores de Eco no marco R8 | Invoca B12 em arena PvE, janela de 20 min | Tabela B12, sem obrigação diária |

Caravana e Vigília requerem contribuição de combate/objetivo; ficar parado não dá prêmio. Custos de evento são pagos uma vez pelo iniciador com confirmação. Se servidor falhar antes de surgir o primeiro inimigo, reembolsar; após início, registrar estado para recuperação administrativa, sem reembolso automático explorável por desconexão.

No nível 120, objetivos são completar conjuntos e relíquias, fazer rotas eficientes, enfrentar chefes, atingir +6/+8, ajudar amigos e experimentar builds. Sem atributos infinitos após 120. Masmorras podem receber dois modificadores opcionais após a campanha: **Pressão Alta** (+20% vida inimiga, +20% materiais) e **Vazamento** (hazards adicionais, uma rolagem de equipamento extra por conclusão). Nenhum dá nível novo de item. Modificadores são configurações de encontro, não mapas ou chefes adicionais.

## 16. Interface e experiência de uso

Dezesseis telas/painéis funcionais: entrada no servidor; seleção/criação de personagem; HUD; inventário; ficha e atributos; habilidades e passivas; mapa/minimapa; missões; grupo; chat; banco; loja; oficina; troca; mural; opções. Painéis podem compartilhar componentes e não precisam de dezesseis cenas gigantes.

HUD mostra vida, recurso específico, oito habilidades, dois atalhos de consumível, grupo, objetivo rastreado e estado da maré. XP fica discreta. Aviso de inventário cheio informa item reservado e prazo. Nome/qualidade de drops deve poder ser filtrado, sem esconder itens de missão ou únicos por padrão.

Controles iniciais: clique esquerdo anda/seleciona; clique direito ataca alvo; 1–8 habilidades; Q/E consumíveis; I inventário; C personagem; M mapa; J missões; Enter chat; Esc menu. Atalhos remapeáveis. WASD opcional com clique para mirar, sem aumentar velocidade diagonal. Não exigir ambos os esquemas simultaneamente.

Acessibilidade mínima: escala de texto/interface 100–150%, desligar tremor, intensidade de partículas, contornos de alvo, indicadores não dependentes apenas de cor, volume de alertas separado. Tooltips apresentam fonte do item, chance de têmpera e efeito de falha; não usar texto vago como “chance pequena”.

O códice é uma aba de mapa/inventário, não nova tela: registra monstros encontrados e fontes descobertas. Não precisa revelar todos os segredos ao iniciar. Anotações de fonte são dados próprios; não importar textos de outros jogos. A documentação do MU confirma que fontes de item e localização de monstros podem ser comunicadas ao jogador; aqui o layout e os dados são originais. [S4]

## 17. Arquitetura recomendada para implementação no Claude

### 17.1 Stack e limites

**Escolha de projeto:** Godot 4 edição .NET, C# para cliente e servidor; cliente Windows; servidor Linux headless; ENet para sessão de jogo; SQLite via Microsoft.Data.Sqlite no processo servidor. Usar versão estável compatível e fixar versões de Godot, SDK .NET e dependências no início do projeto; não assumir a versão “latest” de documentação como estável. A documentação confirma exportação desktop em C#, suporte a servidor headless e transações com Microsoft.Data.Sqlite. A escolha da arquitetura é recomendação deste estudo. [S8–S12]

Servidor dedicado continua ligado quando o cliente do anfitrião fecha. Para testes iniciais, pode rodar no mesmo PC em processo separado. Internet residencial pode exigir abertura de porta UDP ou rede privada virtual; CGNAT pode impedir conexão direta. Hospedagem remota evita depender do computador do jogador, mas deve ser escolhida depois do protótipo. Não estimar preço de provedor sem cotação. Cliente de navegador não é alvo desta arquitetura.

Orçamento de teste, não capacidade garantida: servidor com 2–4 núcleos e 4 GB RAM para oito jogadores. Medir antes de contratar. Simulação a 20 ticks/s; snapshots de posição a 10 Hz com interpolação cliente; eventos de inventário e habilidades com transporte confiável quando necessário. Limite inicial: 150 inimigos ativos no total, ativação por proximidade e regiões vazias suspensas. Não simular os 12 mapas integralmente a todo momento.

### 17.2 Autoridade e persistência

Cliente envia intenção, não resultado. Servidor decide movimento permitido, alcance, linha de visão, recarga, recurso, dano, XP, loot, chance de têmpera e comércio. Não aceitar pacote que diga “ganhei 10 mil moedas”, “este chefe morreu” ou “meu item agora é +8”.

Tabelas persistentes: accounts, characters, character_stats, item_instances, item_locations, currencies, quest_progress, discovered_locations, boss_fragments, death_bags, expeditions, reward_cooldowns, trades, transaction_log, world_state, schema_version. Definições imutáveis de conteúdo ficam em JSON versionado; instâncias guardam ID de definição e rolagens. Uma instância só pode ocupar uma localização por vez, garantida por restrição do banco e transação.

ItemInstance: UUID; definition_id; owner_character_id ou owner_account_id; location_kind; slot; quality; temper_level; affixes; durability; binding; version; created_at; origin_event_id. Pilhas têm quantidade e procedência de expedição separada quando necessário. Material recém-coletado não deve se misturar de modo que apague a procedência de perda na morte.

Operações com itens/moeda usam transação curta e ID de requisição idempotente: validar, bloquear reserva lógica, debitar, criar/alterar, gravar resultado, confirmar. Se a resposta se perder, o mesmo ID recebe o resultado já gravado. Nunca realizar a rolagem de upgrade de novo num retry. Autosave a cada 30 s para posição/estado leve; recompensas, trocas, coleta e upgrades persistem no momento da operação.

Backups: usar API de backup consistente do SQLite; não copiar apenas o arquivo principal enquanto há escrita ativa/WAL. Fazer backup diário, manter sete diários e quatro semanais; validar restauração antes de receber saves importantes. Migração de schema exige cópia prévia. Diário de eventos registra ações administrativas e movimentações de valor, sem tokens ou senhas.

Contas: servidor privado usa lista de convidados e tokens aleatórios individuais revogáveis, provisionados pelo administrador e transmitidos fora do jogo. Não inventar criptografia própria. Conectar via rede privada confiável no primeiro teste remoto; não enviar credenciais reutilizáveis em transporte desprotegido. Autenticação exposta à internet e transporte protegido devem passar por revisão específica antes de hospedar abertamente. Convites não transformam automaticamente qualquer RPC em seguro.

### 17.3 Organização de código

Pastas propostas: `Client/`, `Server/`, `Shared/`, `Content/`, `Tests/`, `Tools/`, `Docs/`, `Assets/`. Cliente contém apresentação e entrada; Server contém regras e repositórios; Shared contém tipos e contratos de rede; Content contém catálogos; Tools contém validadores e simuladores; Docs contém decisões e instruções de execução. A solução pode ter um projeto Godot com cenas distintas e bibliotecas C# de domínio, evitando duplicar regras.

Sistemas separados: autenticação/sessão; movimento; combate; IA; spawn; loot; inventário; equipamento; atributos; habilidades; grupo; missões; oficina; comércio; morte; maré; persistência; telemetria. Não criar um arquivo central que controle o jogo inteiro.

### 17.4 Contrato mínimo dos dados

Campos obrigatórios:

- ItemDefinition: id, display_name, category, subtype, min_level, allowed_classes, handedness, base_stats, affix_pool, temperable, trade_rule, stack_limit, icon_asset_id, model_asset_id.
- MonsterDefinition: id, species_name, archetype, base_profile, skills, resistances, behavior_id, loot_table_id, visual_asset_id.
- EncounterDefinition: id, map_id, monster_id, level, count, spawn_points, respawn_seconds, leash_radius, activation_radius.
- SkillDefinition: id, class_id, unlock_level, target_mode, range, radius, cooldown_ms, resource_cost, resource_generated, coefficient, effect_ids, cast_ms, recovery_ms.
- MapDefinition: id, min_level, max_level, safe_zones, exits, spawn_groups, resource_table, pvp_sectors, tide_profile.
- RecipeDefinition: id, output_item_id, output_count, ingredient_ids_and_counts, currency_cost, unlock_condition.
- QuestDefinition: id, prerequisites, min_level, giver_id, objectives, rewards, repeat_rule.
- LootTable: id, independent_rolls, weighted_pools, eligibility_rule, recipient_rule.

Exemplo de dado de intenção para o primeiro conteúdo, não código de produção:

```json
{
  "id": "weapon_bastiao_sword_p1",
  "display_name": "Espada de Chapa",
  "category": "weapon",
  "subtype": "sword",
  "min_level": 1,
  "allowed_classes": ["bastiao"],
  "handedness": 1,
  "base_stats": {"weapon_power": 12, "attack_interval_ms": 1000},
  "affix_pool": "physical_weapon",
  "temperable": true,
  "trade_rule": "until_equipped_if_unique_otherwise_free",
  "stack_limit": 1,
  "icon_asset_id": "icon_sword_p1_placeholder",
  "model_asset_id": "sword_p1_placeholder"
}
```

O item comum permanece negociável mesmo equipado; a regra de vinculação no exemplo só afeta únicos. O validador deve impedir referência inexistente, peso negativo, receita circular sem fonte inicial e uso de item incompatível.

## 18. Orçamento de arte e produção

O catálogo de 198 equipamentos é grande para produção independente. Construir com módulos e validar encaixe antes de produzir acabamento. Números abaixo são orçamento inicial de produção visual, separado da contagem de conteúdo.

| Entrega | Quantidade de partida | Estratégia |
|---|---:|---|
| Corpos de classe | 4 | Um corpo base por classe, sem variações de gênero neste primeiro escopo |
| Conjuntos visuais de roupa | 20 | Cinco por classe, divididos nas cinco peças |
| Peças de roupa | 100 | Compartilhar topologia e variar volumes/detalhes de modo legível |
| Famílias de arma | 8 | Seis variantes de cada, usando malhas modulares |
| Mãos secundárias | 12 | Quatro por família |
| Ícones de itens | 268 | Um por definição; estados de upgrade são sobreposições |
| Ícones de habilidades/passivas | 56 | 32 + 24 |
| Famílias de rig inimigo | 12 | Humanoide, quadrúpede, ave, crustáceo, inseto, serpente, quelônio, anfíbio, torre, autômato pesado, planta, fragmento flutuante |
| Chefes com aparência distinta | 12 | Reutilizar rigs quando possível; padrões próprios |
| Kits de cenário | 6 | Porto, estaleiro, salina, máquinas, vidro, dique |
| Música | 6 faixas originais | Cidade, exploração calma, exploração hostil, masmorra, chefe, maré |
| Efeitos sonoros | Cerca de 70 | Impactos, habilidades, inimigos, interface e ambiente |

Os 72 inimigos precisam de aparência distinguível, mas não de 72 esqueletos de animação. Elites podem compartilhar corpo com espécie-base, acrescentando peças e telegráficos próprios. Joias e relíquias não exigem modelo equipado; usam ícones. Os únicos exigem aparência própria nos casos visíveis: armas, escudo e botas. Reservar oito variações visíveis exclusivas, sem fingir que já estão incluídas nas 48 armas de base.

Animações por jogador: idle, andar, correr, ataque de uma mão, ataque de duas mãos, conjurar, canalizar, impacto, cair, levantar e interação; adaptar conforme classe, não obrigar cada rig a usar animação inadequada. Inimigo comum precisa de idle, deslocamento, ataque, habilidade, impacto e morte; chefes têm animações específicas de fases.

Não pedir que uma única imagem gerada vire automaticamente um conjunto 3D perfeito. O fluxo de arte envolve conceito, modelagem, UV, textura, rig, animação, importação e teste de escala. Usar formas simples no protótipo e trocar por ativos finais depois. Manter arquivo de licenças e fontes. A licença MIT da Godot não cobre automaticamente os ativos externos usados no jogo. [S10]

## 19. Implementação em seis marcos

### Marco A — Dois jogadores e um save

Uma cena de cidade e uma arena cinza; Bastião apenas, duas habilidades; um monstro; um item e uma poção. Servidor dedicado separado do cliente, dois clientes conectados, movimento, dano, morte e item persistente. Sem tela de marketing, mundo grande, história completa ou arte final.

Aceite: cliente A ataca, cliente B vê o resultado correto; ambos podem desconectar, servidor reiniciar e personagem/itens permanecem. Mesmo item não pode ser coletado por dois jogadores. Dano enviado pelo cliente é rejeitado.

### Marco B — Primeira expedição

H0 e R1; Bastião e Caldeiro com quatro habilidades cada; seis comuns de R1, E01 e B01; nível máximo 20. Inventário, banco, poções, reparo, dívida de XP e bolsa recuperável. Até 30 definições de item, selecionadas do catálogo principal, sem renomear IDs depois.

Aceite: sessão de uma hora com dois jogadores, sem intervenção administrativa; retorno à cidade, primeira melhoria +3 e recuperação de bolsa; telemetria suficiente para medir XP/h e custo de consumíveis.

### Marco C — Cooperação e economia

Quatro classes, grupo até quatro, troca atômica, receitas iniciais, R2/R3/D1, nível até 45. Introduzir rodízio de loot e encomenda de único. Refinar sincronia em 100–150 ms de latência simulada.

Aceite: suporte recebe XP sem atacar todos os alvos; nenhuma troca duplica itens após desconexão; D1 completável com duas composições diferentes; backups restaurados em ambiente de teste.

### Marco D — Progressão intermediária

R4/R5/R6/D2, nível até 90, conjunto completo de habilidades até o nível 85, eventos de Caravana e Vigília, maré funcional, têmpera até +6. Ajustar loops antigos para continuarem úteis.

Aceite: três sessões de farm em cada faixa com custo, mortalidade e recompensa medidos; nenhum atributo torna uma classe inútil; rotas não prendem personagens durante maré.

### Marco E — Catálogo final e fim de jogo

R7/R8/D3, nível 120, B12, todos os 268 itens, 40 missões, 32 receitas, 72 inimigos catalogados; têmpera +7/+8 apenas depois de simulação; arte e áudio substituem placeholders prioritários.

Aceite: contagens automáticas e referências válidas; campanha completa; farm de chefes e garantia de fragmentos; oito jogadores em sessão real de duas horas sem perda de estado.

### Marco F — PvP opcional e distribuição privada

Duelos, setor PvP R7, Infâmia, proteções de entrada e desconexão; pacote cliente Windows e servidor Linux com instruções de atualização, restauração e administração. Separar contas de teste e personagens normais.

Aceite: quem ignora PvP consegue obter todo equipamento necessário; mortes seguem sua causa correta; nenhuma entrada permite spawn kill; atualização do servidor preserva um save anterior validado.

**Não começar pelo marco E.** A lista completa evita improvisar o mundo, mas produzir quantidade antes de validar rede e combate torna o retrabalho caro. Não atribuir prazo de dias ou preço fechado sem conhecer experiência do implementador, disponibilidade, padrão visual e hardware. Este documento não mede esses fatores.

## 20. Verificações necessárias

1. Unidade: fórmulas, clamps, XP, rodízio, dívida e perda de materiais com arredondamento.
2. Integração: troca, banco, coleta, venda, desmontagem e upgrade sob duas requisições simultâneas.
3. Persistência: queda antes/depois de commit; requisição duplicada; restart no meio de combate; bolsas e cooldowns restaurados.
4. Rede: pacotes fora de ordem, 150 ms de latência, 2% de perda, reconexão e expiração de sessão.
5. Conteúdo: IDs únicos, referências existentes, 268 itens, 72 inimigos, 32 receitas, 40 missões; todas as classes com rota de equipamento viável.
6. Balanceamento: combate solo de cada classe em 1/20/45/75/105/120, dois grupos diferentes, cura limitada e chefe sem composição obrigatória.
7. Economia: não existe ciclo de fabricar/vender com lucro infinito; duplicação por troca e retries; recurso garantido não se transfere por contas extras.
8. Carga: oito clientes, quatro encontros simultâneos, até 150 inimigos ativos; medir tempo de tick, CPU, RAM e tráfego, sem assumir sucesso antecipado.
9. Recuperação: restaurar backup numa máquina limpa e conectar cliente compatível.
10. Experiência: todo golpe letal importante é antecipável; perda é explicada; fonte de progresso existe mesmo sem drop raro; interface funciona em 1080p e escala 150%.

## 21. Instrução pronta para levar ao Claude

Copie a instrução abaixo e anexe este documento inteiro. A instrução orienta implementação; não substitui os catálogos e regras anteriores.

> Quero desenvolver um RPG de ação online privado chamado provisoriamente Cinzas da Maré, com progressão lenta, farm difícil, PvE prioritário e PvP secundário. O GDD anexado é a fonte de verdade do projeto. A referência emocional é jogar RPGs antigos de loot e risco; não copie código, arquivos, arte, nomes, mapas, personagens, interface ou tabelas do MU.
>
> Primeiro leia o GDD inteiro e registre contradições materiais antes de programar. Use Godot 4 .NET, C#, servidor dedicado autoritativo Linux, cliente Windows e SQLite via Microsoft.Data.Sqlite. Verifique compatibilidade, fixe versões estáveis e documente a instalação. Se o ambiente disponível não executar Godot, prepare os arquivos reais e diga exatamente o que não conseguiu rodar; não substitua por uma página HTML fingindo ser o jogo.
>
> Crie um repositório separado de outros jogos. Organize Client, Server, Shared, Content, Tests, Tools, Docs e Assets. Separe dados de conteúdo e regras; use IDs estáveis. Preserve este GDD em Docs e registre decisões posteriores. Não comece gerando os 12 mapas nem 198 equipamentos.
>
> Implemente apenas o Marco A primeiro: servidor em processo separado; dois clientes; movimento; Bastião com duas habilidades; um monstro; um item e uma poção; dano calculado no servidor; coleta exclusiva; morte; autenticação de teste; inventário e personagem persistentes. Use geometria simples e interface funcional. O servidor precisa continuar rodando quando um cliente fecha.
>
> Inclua instruções executáveis para iniciar servidor e dois clientes, dependências, migração inicial do banco e testes de coleta simultânea, pacote de dano inválido, reinício e reconexão. Não declare multiplayer ou persistência funcionando sem executar a verificação correspondente. Se não puder testar, marque como não testado.
>
> Ao concluir cada marco, entregue arquivos, comandos, resultado dos testes, limitações reais e próximo marco. Só amplie conteúdo após o aceite técnico do marco atual. Todos os valores de balanceamento do GDD são sementes: crie ferramentas de simulação e telemetria para ajustá-los, sem mudar silenciosamente a proposta de evolução lenta. Não acrescente reset, auto-farm, pay-to-win ou PvP obrigatório.

## 22. Referências consultadas e uso

Consulta em 29/10/2026. As fontes sustentam contexto de referência, limites gerais e escolhas de tecnologia. **O mundo, os nomes propostos, os catálogos e todas as tabelas de balanceamento são criação deste projeto**, não dados oficiais extraídos dessas fontes. Não foi feita auditoria jurídica do nome ou de futuros ativos, nem reprodução exaustiva de todos os conteúdos de MU.

- **S1 — Webzen, biblioteca de guias:** estrutura de classes, mapas, monstros e eventos. https://muonline.webzen.com/pt/gameinfo
- **S2 — Webzen, Item Upgrade:** referência de progressão de equipamento com risco. Nenhuma taxa desta página foi adotada como nossa regra. https://muonline.webzen.com/pt/gameinfo/guide/detail/32
- **S3 — Webzen, Season 9:** exemplo documentado de regras de PK e penalidades; é página de uma temporada, não afirmação de regra universal atual. https://muonline.webzen.com/pt/gameinfo/guide/detail/76
- **S4 — Webzen, Item Book:** apresentação de fontes de itens e informações de monstros. https://muonline.webzen.com/en/gameinfo/guide/detail/233
- **S5 — Câmara dos Deputados, Lei 9.610/1998, texto atualizado:** distinção entre ideias/regras e expressão protegida, especialmente artigos 7 e 8. https://www2.camara.leg.br/legin/fed/lei/1998/lei-9610-19-fevereiro-1998-365399-normaatualizada-pl.html
- **S6 — INPI, Marcas:** orientação e ferramentas oficiais. https://www.gov.br/inpi/pt-br/servicos/marcas
- **S7 — INPI, busca de marcas:** ponto de partida para verificação futura; não foi realizada pesquisa de anterioridade do nome proposto. https://servicos.busca.inpi.gov.br/marcas
- **S8 — Godot, High-level multiplayer, documentação estável:** interfaces de rede e necessidade de validar ações remotas. https://docs.godotengine.org/en/stable/tutorials/networking/high_level_multiplayer.html
- **S9 — Godot, Exporting for dedicated servers, documentação estável:** execução headless e exportação dedicada. https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_dedicated_servers.html
- **S10 — Godot, License:** licença MIT e obrigações de aviso. https://godotengine.org/license/
- **S11 — Godot, C# basics, documentação estável:** suporte .NET e exportação desktop. https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html
- **S12 — Microsoft, Transactions — Microsoft.Data.Sqlite:** uso de transações para operações consistentes. https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions

## 23. Limites desta entrega

Este estudo entrega escopo, conteúdo nomeado, regras iniciais, arquitetura, critérios de verificação e instrução de implementação. Não entrega modelos 3D, animações, código de jogo, banco populado, servidor hospedado ou testes de gameplay. IDs completos, JSONs e scripts de importação deverão ser produzidos pelo Claude conforme cada marco, a partir das tabelas e regras de geração acima. Os custos e os tempos de farm precisam de validação; a segurança jurídica depende também da execução concreta e dos ativos efetivamente utilizados.

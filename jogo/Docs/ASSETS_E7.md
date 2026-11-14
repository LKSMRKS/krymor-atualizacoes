# Auditoria dos assets e plano da E7 visual

Conferido em 02/11/2026 no pacote `RPG_Completo_Lotes_00_a_08.zip`: medi as 93 imagens (formato, tamanho, transparência) e
olhei só as necessárias (folhas de contato do Guerreiro, dos monstros e dos cenários, o HUD e o equipamento inicial).

## 1. O que o pacote realmente contém
**93 PNG, 189 MB, todos imagens 2D.** Não há modelo 3D, textura repetível (tile), spritesheet, animação, ícone recortado, fonte nem áudio.

| Lote | Conteúdo | Qtd | Transparência | Natureza |
|---|---|---:|---|---|
| 0 | Direção visual de gameplay | 1 | não | pintura de referência |
| 1 | Bases das 4 classes (Guerreiro sem armadura, etc.) | 4 | sim | recorte, 1 pose frontal |
| 2 | Equipamento inicial (armaduras e armas) | 12 | sim | renders de item/personagem |
| 3 | Conjuntos de armadura | 16 | sim | renders de personagem |
| 4 | Armas | 32 | sim | renders isolados |
| 5 | Monstros da Pedreira | 8 | sim | recorte, 1 pose |
| 6 | Cenários (regiões) | 6 | não | pinturas isométricas |
| 7 | Telas de interface (HUD, inventário, habilidades…) | 6 | não | mockups com texto embutido |
| 8 | Brejo (cenários e monstros) | 8 | 5 de 8 | pinturas e recortes |
Total com transparência real: **77 de 93**.

## 2. O que já foi integrado (E7, primeira área)
| Ativo | Origem | Uso | Limitação |
|---|---|---|---|
| Guerreiro | Lote 2 #01 "Ferro de Vigília" (kit inicial) | **cartaz** (billboard) de 2 m no jogo | 1 pose frontal, sem animação |
| Retrato | recorte da cabeça do mesmo | cartão do jogador no HUD | — |
| Monstro | Lote 5 #01 "Carrapato de Escória" | cartaz de 1,3 m | 1 pose; nome difere do conteúdo (ver seção 6) |
| Espada | Lote 2 #05 "Espada de Vigília" | item no chão e slot "Arma" | nome difere do conteúdo (seção 6) |
| Fundo do login | Lote 0 | pano de fundo da tela de login | só referência visual |
Todos são **derivados reduzidos** (ver `Client/Assets/Art/ORIGEM.md`). O Guerreiro do Lote 1 (sem armadura) não foi usado porque o kit inicial é o do Lote 2.
Usados só como **referência** (não viraram arquivo do jogo): Lote 6 "Câmara do Carcereiro" e "Refúgio da Vigília" (paleta, braseiros, pedra molhada, pinheiros escuros) e Lote 7 "HUD" (layout).

## 3. Feito sem arte, em formas simples (código)
Pátio de lajes de pedra (claras, escuras, molhadas, com musgo), muros em ruínas com blocos quebrados, grades de ferro, torres nos cantos, 4 estátuas encapuzadas,
4 braseiros com luz que pisca, pinheiros e árvores de outono fora dos muros, arbustos, capim, folhas caídas, entulho, luar frio com luz quente dos braseiros, névoa leve.
HUD no layout do lote 7: cartão do jogador, alvo, barras (Vida/Fôlego/XP), barra de habilidades com recarga, registro de combate, nameplates, números de dano, aviso de coleta, tela de morte.
Orientação por **decalques no chão**: leque branco = para onde o personagem olha; leque azul = arco da Guarda (120°, vem do servidor).

## 4. O que ainda exige modelos 3D, animações e outros ativos (não existe no pacote)
| Falta | Por que importa | Caminhos possíveis (a decidir) |
|---|---|---|
| Modelos 3D riggados do Guerreiro e do monstro | hoje são cartazes de uma pose | encomendar/gerar modelos; ou ferramenta de imagem para 3D (qualidade a validar); ou spritesheets animados |
| Animações: parado, andar, atacar, bloquear, sofrer dano, morrer, conjurar | hoje só há clarão de dano | junto com os modelos |
| Vistas em várias direções | o cartaz não gira; a direção do golpe é mostrada pelos decalques | spritesheet de 8 direções ou modelo 3D |
| Cenário modular 3D com texturas | hoje é feito de caixas e cilindros | kit modular (lajes, muros, torres, estátuas, braseiros, árvores) + texturas |
| Ícones de habilidades, poção, atributos; molduras, barras e botões recortados | HUD usa blocos de cor onde não há arte | recortar/gerar a partir dos mockups do Lote 7 ou criar um kit de UI |
| Fonte, cursor, efeitos visuais (golpe, impacto, brasas), som e música | acabamento | à parte |
| Demais classes e monstros | existem como conceito (lotes 1, 3, 5, 8), sem modelos | mesmo caminho dos modelos |

## 5. Limitações conhecidas da primeira área
- O cartaz é uma ilustração conceitual de uma pose; vista de cima pode parecer "papel recortado". É provisório e honesto: **não é modelo pronto**.
- **Sem colisão com o cenário**: o servidor ainda não tem. Muros, torres e estátuas ficam logo depois do limite andável (arena de 20 m), mas as estátuas e os braseiros ficam *dentro* da área e o personagem atravessa. Registrado como pendência.
- A arena passou de 60×60 m para **40×40 m** (`arena_half_size` 20) para ficar mais densa e legível. O servidor envia o tamanho ao cliente (`WorldInfoMessage`), então mudar o número muda o cenário sozinho.
- Este código visual **não foi compilado nem executado** pelo assistente; a primeira compilação no Godot pode revelar erros de API.

## 6. Decisão curta que preciso de você
As artes usam **Carrapato de Escória** (monstro) e **Espada de Vigília** (arma), mas o conteúdo do Marco A chama **Caranguejo de Cal** e **Espada de Chapa**.
Hoje a barra de vida mostra o nome do conteúdo sobre a arte do carrapato. Como a decisão oficial 3 diz que nomes dos lotes podem substituir os do GDD,
**recomendo renomear só os nomes de exibição** (monsters.json `species_name` e a tabela de nomes do cliente), sem mexer nos IDs. Posso fazer isso na próxima entrega, se concordar.

## 7. Atualização E7b-1 (02/11/2026): melhorias com os arquivos existentes
Feito sem esperar modelos 3D. **Escrito, não compilado nem executado pelo assistente.**
| Melhoria | Como |
|---|---|
| Câmera e escala | câmera mais próxima (fov 38°, altura 10,5 m); Guerreiro 2,2 m e monstro 1,6 m |
| Ícones no lugar das siglas | **golpe, guarda, ataque básico e poção** recortados do mockup do Lote 7 (`icone_*.png`, ~50 px ampliados 3x; o algarismo da poção foi apagado); espada = arte do Lote 2. Não há arte própria para esses ícones |
| HUD compacto | cartão do jogador com as três barras, alvo no topo, barra de habilidades, registro de 5 linhas; sem painéis vazios |
| Indicadores discretos | anel fino, leque de mira bem leve, leque da Guarda leve; anel some quando a criatura morre |
| Cenário | chão de pedra **texturizado** (uma peça, com mapa de normais), muros com textura, estandartes vermelhos do Lote 6, caixotes, barris, poças, poças de luz sob os braseiros, tom de cinema e névoa leve |
| Efeitos sincronizados com o servidor | risco de golpe, faíscas de impacto, números de dano, avanço de quem ataca, clarão em quem apanha, poeira e queda na morte, pulsos (Guarda, poção, nível, coleta, renascer), tremor de câmera |
Texturas do chão e do muro são **procedurais** (`Tools/gerar_texturas.py`), não vêm das artes.

**Continua dependendo de assets que não existem:** modelos 3D e animações de verdade (especificados em `Docs/ESPECIFICACAO_MODELOS_3D.md`), cenário modular e texturas PBR próprias, ícones próprios, VFX e som.

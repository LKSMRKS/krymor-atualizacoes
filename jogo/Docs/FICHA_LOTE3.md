# Cinzas da Maré — Lote 3 (Costa jogável) — ficha para integração

Data: 09/11/2026. Copiar a pasta `Client/` deste pacote por cima da pasta `Client/` do projeto.
Arquivos que já existem só são **substituídos** nos 6 caranguejos, que têm os mesmos nomes, ossos e clipes do lote 2.
O backup do lote 2 já está no projeto, em `backups/criaturas_lote2/`. Todo o resto é arquivo novo.

**Nada foi alterado em código (`.cs`), conteúdo (`.json`) ou banco de dados.**
Ids de conteúdo continuam os mesmos. Tudo abaixo é proposta para quem integra.

## Padrão técnico de todas as peças

- `.glb` com textura PBR embutida: cor (com AO), rugosidade/metal (ORM: G = rugosidade, B = metal) e normal.
- 1 unidade = 1 m. Frente +Z. Origem no chão. Exceção: `proj_barril_explosivo`, com origem no **centro**, porque gira no ar.
- Criaturas: 1 malha e 1 esqueleto, com os clipes no mesmo arquivo, no lugar (sem deslocamento).
- Clipes obrigatórios: `idle`, `walk`, `attack_windup`, `attack_strike`, `attack_recover`, `hit`, `death`.
- Nenhum vértice fica abaixo de y = 0 em nenhum quadro, exceto o `death` do javali.
- Humanoides: esqueleto `mixamorig:*` **idêntico** ao do `manequim_masc.glb` (24 ossos, pose T). Os clipes `Idle`, `Walking` e `Running` vêm copiados dentro do arquivo.
- Leitura testada na câmera do jogo (62°, 35° de cima, 8,5 m) sobre areia; os prints estão na pasta `previas/`.

## 1. Criaturas — `Client/Assets/Models/criaturas/`

Durações em segundos.

| Arquivo | O que é | Tamanho | Clipes próprios (s) |
|---|---|---|---|
| `caranguejinho.glb` | turquesa, patas grossas | 1,05 × 0,9 × 0,39 m | — |
| `caranguejo_de_cal.glb` | carapaça branca-cal, pinças vermelhas, cracas | 1,96 × 2,06 × 0,77 m | sand_windup 0,50 · sand_throw 0,43 |
| `caranguejo_ermitao_barril.glb` | laranja, barril | 1,6 × 2,1 × 1,3 m | hide 0,43 · hide_loop 1,03 · unhide 0,50 · ram_windup 0,57 · ram_strike 0,70 · **barrel_throw_windup 0,60 · barrel_throw 0,30 (novos)** |
| `caranguejo_pinca_de_ferro.glb` | cinza-azulado com brasa | 2,1 × 2,5 × 0,92 m | sweep 0,97 |
| `caranguejo_de_pedra.glb` | cinza, musgo, espinhos verdes | 1,7 × 1,6 × 1,08 m | hunker_down 0,50 · hunker_loop 1,03 · spike_volley 0,77 · rise_exposed 1,30 |
| `caranguejo_escavador.glb` | marrom-escuro, baixo e largo, pinças em pá | 3,1 × 3,1 × 1,57 m | burrow 0,97 · underground_loop 0,77 · emerge_attack 0,83 · emerge_dazed 1,17 |
| `rei_das_pincas.glb` **(novo)** | chefe: carmim, coroa de ouro, cracas e algas | 3,2 × 4,0 × 1,4 m | sweep 1,30 · phase2 1,97 |
| `javali_da_duna.glb` **(novo)** | quadrúpede: crina ruiva, presas de marfim, olhos acesos | 0,72 × 1,77 × 1,16 m | charge_windup 0,97 · charge 0,47 (laço) |

Clipes padrão de todos: idle, walk, attack_windup, attack_strike, attack_recover, attack_full, hit e death (1,30).

**Ligação sugerida** (o `VisualFactory` atual já usa `GlbCreatureVisual` para os caranguejos):

- `crab_king` → `rei_das_pincas`. Hoje ele ainda usa o `CrabVisual(2.3)`. Escala 1 equivale a cerca de 3 m de largura.
  - Ataque com "varrida", "lateral" ou "sweep" → `sweep`.
  - Troca de fase → `phase2`.
- `boar` → `javali_da_duna` com escala 1,0. `boar_big` → o mesmo arquivo com escala 1,45.
  - Ataque "investida", "carga" ou "charge": windup → `charge_windup`, active → `charge`.
- Ermitão: ataque "barril" → windup `barrel_throw_windup`, active `barrel_throw`.
  - O projétil sai no 4º quadro do `barrel_throw` (cerca de 0,1 s), entre as pinças.
  - Modelo do projétil: `projeteis/proj_barril_explosivo.glb`.

## 2. Humanoides — `Client/Assets/Models/humanoides/`

| Arquivo | Altura | Tris | Destaque visual |
|---|---|---|---|
| `mo_goblin_saqueador.glb` | 1,22 m | 14,8 mil | faca na mão direita, saco de saque nas costas, trapos, bandana |
| `mo_goblin_arqueiro.glb` | 1,18 m | 15,9 mil | capuz vermelho, arco curto na mão esquerda, aljava com flechas |
| `mo_goblin_chefe.glb` | 1,78 m | 22,4 mil | elmo com chifres, presas, ombreiras com cravos, clava com pregos, escudo de tampa de barril |
| `mo_esqueleto_goblin.glb` | 1,18 m | 14,8 mil | ossos à mostra (costelas, coluna, bacia), olhos azuis, machadinha |

- As armas e o equipamento já estão modelados, presos de forma rígida ao osso (mão, antebraço ou Spine2). Não usar `FixedWeapon` com eles.
- **Pedido do dono do projeto:** os goblins atuais do jogo (o pequeno e o esqueleto, feitos no manequim) **ficam como estão**. Ele gosta deles.
  - Os modelos novos são para as **variações novas** da seção 5 ou para onde ainda não há visual bom (o Chefe e o Arqueiro).
  - Não substituir os atuais sem ele pedir.
- **Como ligar:** o esqueleto e os nomes dos ossos são os mesmos do manequim, então o `MannequinVisual` pode carregar este `.glb` como corpo, no lugar de `manequim_masc`. As poses de ataque, guarda, arco e morte continuam valendo. Ao carregar:
  - pular `BulkMesh`, as roupas por região (`COLOR_0`) e os traços primitivos de `MonsterFeatures`, porque o modelo não tem a máscara de regiões e já vem vestido;
  - pular `SetWeapon`;
  - usar escala 1, porque o arquivo já está no tamanho real.

## 3. Cenário — `Client/Assets/Models/cenario/`

As coordenadas de colisão abaixo estão em Godot: x, y para cima, z para a frente.

| Arquivo | Tamanho | Colisão (`*_colisao.glb`, caixas) |
|---|---|---|
| `cenario_farol.glb` | 6 × 7,4 × 19,5 m, lanterna acesa | octógono de 4,2 m × 16 m de altura. Bate com o obstáculo do servidor (`farol`, círculo r 3, altura 16): **não mexer no servidor** |
| `cenario_palicada_trecho.glb` | trecho de 4 m, até 3,4 m de altura | caixa 4 × 2,8 × 0,5 m, centro (0; 1,4; 0) |
| `cenario_tenda_goblin.glb` | 4,2 × 4 × 2,8 m | sem colisão (é enfeite, igual à `tenda` atual) |
| `cenario_totem_goblin.glb` | 3,7 m | caixa 0,8 × 3 × 0,8 m |
| `edificio_entrada_galeria.glb` | paredão de cal de 16,6 × 9,2 m com boca de mina de 4 × 3,6 m | dois blocos laterais, centros (±4,6; 3,75; −1,5), e teto, centro (0; 5,6; −1,5). A abertura fica livre: o portal da Galeria vai em (0, 0, −2) |

- O acampamento goblin é montado com essas peças: 3 a 4 trechos de paliçada, 2 tendas, 1 totem e barris. Veja `previas/acampamento_jogo.png`.
- Na Costa hoje há `tenda` ×4 e `acampamento_abandonado` ×2 (props). Trocar o desenho do `MapKit` por estes `.glb` não mexe em regra.
- O mapa da Costa vem de `Tools/gerar_regioes.py`: posição nova vai por lá, nunca editando o `.json` à mão.

## 4. Projétil — `Client/Assets/Models/projeteis/proj_barril_explosivo.glb`

- 0,6 m, com pavio e brasa acesa (material emissivo). Origem no centro.
- Sugestão: girar no ar e, ao cair, piscar a brasa por 0,8 s antes de explodir.

## 5. Variações sugeridas (reaproveitando os modelos)

Pedido do dono: aproveitar tudo, com pequenas variações de nome, movimento e ataque. Cada linha seria um **id novo** no conteúdo (aditivo, sem renomear nada). Tamanho e tinta mudam só no cliente (escala e `tint` do `GlbCreatureVisual`); velocidade, vida e ataque mudam no servidor.

| Modelo | Variação | Diferença sugerida |
|---|---|---|
| `caranguejinho` | Caranguejinho Ligeiro | velocidade ×1,4, preparação do ataque 30% mais curta, vida −20% |
| `caranguejo_de_cal` | Caranguejo de Cal Ancião | escala 1,3, mais lento, dois arremessos de areia seguidos |
| `caranguejo_pinca_de_ferro` | Pinça de Brasa | tinta mais escura e quente; o `sweep` deixa chão em brasa |
| `caranguejo_de_pedra` | Caranguejo de Musgo | escala 0,8, `spike_volley` com metade dos espinhos e mais rápido |
| `javali_da_duna` | Javali Jovem | escala 0,8, velocidade ×1,3, sem investida (só presa) |
| `javali_da_duna` | Javali Couraçado (`boar_big`) | escala 1,45, investida mais longa (8 m) e mais lenta para virar |
| `mo_goblin_saqueador` | Goblin Batedor | velocidade ×1,3, dois golpes rápidos de faca (preparação 0,4 s, golpe 0,15 s) |
| `mo_goblin_saqueador` | Goblin Ladrão | foge depois de acertar e solta moedas ao morrer |
| `mo_goblin_arqueiro` | Goblin Atirador | preparação longa (0,9 s), dano ×1,6, distância maior |
| `mo_goblin_arqueiro` | Goblin Flecheiro | rajada de 3 flechas fracas |
| `mo_esqueleto_goblin` | Esqueleto Corredor | velocidade ×1,5, vida baixa, vem em grupo |
| `mo_esqueleto_goblin` | Esqueleto Desenterrado | nasce do chão (pose `rise`) quando o jogador passa perto |
| `mo_goblin_chefe` | (único) | clava: `slam` com onda curta; escudo bloqueia de frente durante a preparação |
| `rei_das_pincas` | (chefe) | `phase2` com 50% de vida: 2 s de urro, depois ataques 25% mais rápidos |

## 6. Comportamentos novos para o servidor (sugestão)

A animação já existe; falta a regra.

- **Javali, investida:** `charge_windup` por 1,0 s, cavando o chão (aviso no chão em linha). Depois corre reto por até 6 m a 2,5× a velocidade. Para ao acertar ou bater em obstáculo e fica 0,8 s vulnerável (`attack_recover`).
- **Ermitão, barril:** preparação de 0,6 s. O barril sai em arco até 8 m. Cai, fica 0,8 s com o pavio aceso e explode em raio de 2 m.
- **Rei das Pinças:** `sweep` em arco frontal de 180° com raio de 3,5 m e preparação de 0,9 s. Troca de fase com 50% de vida (`phase2`, invulnerável enquanto urra).
- O `GlbCreatureVisual` atual estica a preparação e a recuperação conforme o tempo do servidor, então as durações acima podem ser ajustadas no servidor sem refazer arte.

## Estado (regra do projeto)

- **Implementado:**
  - as 22 peças `.glb` deste pacote;
  - os 2 golpes novos do Ermitão;
  - a ficha.
- **Executado:**
  - gerei e conferi todas as peças no Blender;
  - testei a leitura na câmera do jogo (prints em `previas/`);
  - verifiquei que nada atravessa o chão;
  - os 6 caranguejos já foram copiados para o projeto, com backup.
- **Não testado:**
  - a importação no Godot e o jogo rodando. Não há Godot nem .NET no ambiente de geração.
- **Próximo comando:**
  - copiar a pasta `Client/` do pacote para o projeto;
  - abrir o editor do Godot uma vez, para importar os `.glb`;
  - depois rodar `dotnet build .\Client\CinzasDaMare.Client.csproj` e `.\Jogar.cmd`.

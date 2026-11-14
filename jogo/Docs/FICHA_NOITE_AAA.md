# Noite AAA — 14/11 (sets lendários, armas lendárias, ambientação dos mapas, balanceamento)

**Pedido do dono (05:09):**

- sets e armas iguais às referências, com o mesmo físico, profundidade e detalhe;
- orientação para balancear todos os itens matematicamente (alguns vão para a loja, na aba de recompensas extras);
- levar todos os lugares depois da Praia ao nível do Porto, da Praça e da Praia do Cal.

**Para o programador:** a atualização do servidor regrava `Content/` e `Tools/`, então esta entrega precisa entrar no repositório. Nenhum `.cs` do jogo foi mudado.

Tudo aqui é arquivo novo:

- modelos e ícones;
- 3 scripts em `Tools/`;
- 1 arquivo de itens (`Content/items_lendarios.json`);
- 1 teste;
- o guia de balanceamento em `Docs/`.

## 1. Quinze conjuntos lendários (`Client/Assets/Models/armaduras/arm_*.glb`)

Feitos sobre o manequim (o mesmo esqueleto de todo o jogo), 5 peças cada: capacete, peitoral, luvas, calca, botas. É o mesmo formato dos `arm_*` que já existem: nós com o nome da peça, cor de vértice "balanco" para o shader de pano e asa, PBR assado em 2048.

O que eles têm a mais que os conjuntos anteriores:

- **Asas de verdade**, em três tipos:
  - penas em 3 fileiras: Cavaleiro Sagrado, Celestial e Anjo Caído;
  - membrana de morcego com dedos e garras: Dragão de Fogo e Demoníaco;
  - lascas de cristal: Gelo Eterno.

  As asas balançam pelo shader de asa (alfa 0,53–1).
- **Auréolas atrás da cabeça:**
  - sol com raios: Sagrado e Celestial;
  - relógio com marcas e ponteiros: Tempo;
  - quebrada: Anjo Caído;
  - anel rúnico: Rúnico.

  Há também o **buraco negro com anéis acesos**, no Abismo e no Vazio.
- **Elementos do tema:**
  - chamas nos ombros e no elmo;
  - cristais nos ombros e na coroa;
  - folhas e galhada (Floresta);
  - caveiras de olhos acesos (Necromante);
  - nemes listrado com naja e colar largo (Faraônico);
  - engrenagens e ampulheta (Tempo);
  - rochas e núcleo aceso (Pedra);
  - coral e barbatanas (Abissal);
  - espinhos de sombra (Abismo e Vazio).
- **Ombreiras 15% maiores**, frisos de ouro 70% mais grossos e gemas engastadas nos ombros, no peito, no cinto e nas braçadeiras.
- **Placas limpas com o brilho no lugar certo.** O pontilhado de antes saiu. Rachaduras acesas só em membrana, lava e sombra.

| id | Nome | Classe | Nível |
|---|---|---|---|
| arm_runico_ancestral | Rúnico Ancestral | Mago | 45 |
| arm_pedra | Pedra Viva | Guerreiro | 45 |
| arm_floresta | Floresta | Bruxa | 45 |
| arm_cavaleiro_sagrado | Cavaleiro Sagrado | Guerreiro | 50 |
| arm_gelo_eterno | Gelo Eterno | Bruxa | 50 |
| arm_necromante | Necromante | Bruxa | 50 |
| arm_abissal_profundo | Abissal | Arqueiro | 50 |
| arm_dragao_fogo | Dragão de Fogo | Guerreiro | 55 |
| arm_abismo | Abismo | Mago | 55 |
| arm_faraonico | Faraônico | Bruxa | 55 |
| arm_celestial | Celestial | Arqueiro | 60 |
| arm_demoniaco | Demoníaco | Guerreiro | 60 |
| arm_tempo | Tempo | Mago | 60 |
| arm_vazio | Vazio | Mago | 60 |
| arm_anjo_caido | Anjo Caído | Arqueiro | 60 |

- **Ícones:** `Client/Assets/Art/ico_arm_<conjunto>_<peça>.png` (e `_256`).
- **Fonte:** `Tools/arte/build_sets_lendarios.py` (Blender, mesmo kit dos conjuntos de 12/11).
- **Malha por conjunto:** capacete ~8k triângulos, peitoral ~30k (asas inclusas), luvas 8k, calça 15k, botas 8k. Cada .glb tem ~6,5 MB.

## 2. Cinquenta armas lendárias (`Client/Assets/Models/armas/arma_lend_<tipo>_<elemento>.glb`)

São 10 tipos × 5 elementos. Os tipos são das duas referências:

- espada
- machado gigante
- foice
- garfo (tridente)
- espada dupla (lâmina em cima e embaixo)
- lança
- manopla
- cajado
- foco natural
- arco

Os 5 elementos, com o ornamento próprio de cada um:

- **Sagrado** (prata e azul): asas.
- **Fogo** (rubro e ouro): lâmina flamejante e chamas.
- **Vazio** (negro e roxo): espinhos curvos e orbe negro.
- **Gelo** (lâmina de cristal): lascas.
- **Celestial** (ouro): raios de sol e lótus.

As lâminas têm gume aceso e runa central.

- **Convenção** (igual ao arsenal e ao GearFactory): empunhadura na origem, ponta para +Z (+Y no Godot). A cabeça do machado e da foice fica para +X. O arco segue o BowModel. A manopla tem origem no punho, com os dedos para +Z.
- **Ícones:** `Client/Assets/Art/icones/armas/ico_lend_<tipo>_<elemento>.png` (e `_256`).
- **Fonte:** `Tools/arte/build_armas_lendarias.py`.

## 3. Itens (`Content/items_lendarios.json`, gerado por `Tools/gerar_itens_lendarios.py`)

São 125 itens: 75 peças de conjunto e 50 armas. Os atributos saem das fórmulas do guia. As armas usam as `skill_lend_*` que já existem como efeito.

**Os itens nascem sem drop.** Ligar no saque ou na loja conforme o guia, seções 5 e 6.

## 4. Ambientação dos mapas (`Tools/gerar_ambientacao.py`)

São 21 mapas de campo depois da Praia: Vale, Mata, Cânions, Brejo, Véspera, Coroa da Geada, Mar de Escória, Acrópole, os 5 reinos e os 7 mapas de campo extras. Em cada um, o script:

1. **Monta uma cena em cada lugar com nome**, com as peças da região que já estão no jogo (`cenario/<região>_*.glb`), escolhida pelo tipo do lugar:
   - Acampamento ou Pouso: fogueira, tendas da região, panela, carnes, caixas e carroça ou trenó.
   - Altar: altar da região, anel de pilares, tochas e oferendas.
   - Ruína: ruínas, colunas e caixas, com um diário (livro) e uma placa de pressão.
   - Mirante: estátua, estandartes, banco e placa.
   - Covas, Cemitério ou Túmulo: lápides em fileira e cerca.
   - Clareira da Forca: árvores-forca e restos.
   - Bigorna: bigorna, forja e caldeirão.
   - Cratera: anel de rochas.
   - Escavação: trilhos e vagonetes.
   - Refúgio: casas da região em volta, tochas, estandartes e o quadro de recados.
   - Covil do chefe: anel de pilares e braseiros na borda (o meio fica livre para a luta), ossos e a peça-trono da região atrás.
2. **Marco:** uma peça grande da região perto da entrada, que se vê de longe (moinho, elevador da mina, ninho da fênix, estátua do juramento, palafita, casa tapada, muralha de gelo, farol quebrado).
3. **Estradas:** poste a cada ~16 m, alternando o lado, com o poste da região (apagado na Véspera, braseiro na Acrópole). Placa de seta perto de cada saída. Trilha de tábuas no Brejo.
4. **Mata da borda e árvores soltas** da região, com colisão, **fora dos territórios dos monstros**: eles andam sem desvio, então nada sólido onde caçam.
5. **Grupos de rochas e enfeites de chão** (capim, ossos, crânios, cristais, basalto, redes…), mais densos nos territórios.
6. **Coleta e história:**
   - 1 veio ou moita por lugar, com os itens da região que já existem;
   - 1 texto de "contemplar" ou diário por lugar, contando o que aconteceu ali;
   - placas de pressão nas ruínas.

**Resultado:** cada mapa passou de ~60 para **190–410** peças de cenário. A Costa tem 249 e o Porto 149.

O script é **aditivo e repetível:** tudo o que ele põe leva `"amb": true`, e rodar de novo tira e refaz igual. Não mexe em spawns, portais, NPCs nem missões, e a colisão vai pelo campo `solid` ou por caixas `invisivel` com giro.

**Prévias:**

- `previas/amb_*.png`: vista de cima. Amarelo = peças novas, azul-claro = objetos novos, vermelho = monstros.
- `previas/prev_geada_3d.png`: render 3D com os modelos de verdade.
- `previas/vitrine_sets_lendarios.png` e `previas/vitrine_armas_lendarias.png`: os 15 conjuntos e as 50 armas, no formato das referências.
- `previas/set_*.png`: cada conjunto de frente, de costas e andando.

## 5. Balanceamento (`Docs/GUIA_BALANCEAMENTO.md` + `Tools/auditar_balanceamento.py`)

O guia tem:

- as curvas oficiais, tiradas dos itens atuais;
- multiplicadores de qualidade, fração por peça e bônus de conjunto;
- preço, chance de drop e garantia contra azar;
- a aba Recompensas Extras, com a Ficha de Glória;
- o processo para criar item novo.

A auditoria (`Docs/auditoria_balanceamento.csv`) achou:

- **Conjuntos fracos:** os 135 itens de conjunto estão em **0,50** da curva. Um conjunto vale metade de um drop comum do mesmo nível.
- **Arsenal invertido:** a arma Enferrujada sai em 1,13 e a Excelente em 0,80.
- **Itens isolados:** a lista está no guia, item 4.3.

## Implementado / executado / não testado

- **Executado aqui,** numa cópia do jogo atualizado (13/11) com cidades + Merlin + plantas:
  - `gerar_ambientacao.py` e `gerar_itens_lendarios.py` → **ContentValidator: 0 erros**.
  - `AmbientacaoTests`: 20/20 mapas. Andando numa grade de 0,5 m com o raio do jogador, da entrada se chega a todos os portais, NPCs e monstros. A exceção é o que fica atrás do selo norte, que abre depois do chefe por design.
  - Suíte completa (com os testes novos): **494 aprovados, 0 falhas**.
- **Não testado:**
  - Não abri o Godot. Não vi os conjuntos e as armas no boneco animado, nem os mapas com a luz e as partículas do jogo.
  - As armas novas não foram conferidas na mão em todos os clipes (a regra de 10/11). A conferência fica para o próximo passo, no Ateliê.
  - A densidade nova (até 410 peças) não foi medida em FPS.
- **Ficou para o próximo lote:** os **monstros** das regiões depois da Praia. São 131 visuais desenhados por código; o 3D com rig e animação é trabalho de vários lotes. A prioridade sugerida está na resposta ao dono.

## Instalar (programador)

1. Copiar `Client/`, `Tools/`, `Tests/` e `Docs/` por cima do projeto. São só arquivos novos.
2. Rodar, nesta ordem:
   ```
   python Tools/gerar_cidades.py
   python Tools/gerar_merlin.py
   python Tools/gerar_plantas_dg.py
   python Tools/gerar_itens_lendarios.py
   python Tools/gerar_ambientacao.py
   python Tools/auditar_balanceamento.py
   ```
3. `dotnet build -c Release` e depois `dotnet test`.

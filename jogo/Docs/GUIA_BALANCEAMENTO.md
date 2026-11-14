# Guia de balanceamento de itens — Cinzas da Maré (14/11)

**Para:** o programador (e quem criar item novo daqui para a frente).
**Pedido do dono:** "orientar para balancear progressivamente, matematicamente, todos os itens do jogo. Alguns itens vão até para a loja, na aba de recompensas extras."

O guia tem três partes:

1. **As curvas oficiais.** Valem para todo item de 1 a 60.
2. **A auditoria do que existe hoje.** Mostra quem está fora da curva e por quanto.
3. **Como ligar os lendários novos.** Drops, a aba Recompensas Extras e o processo para não desbalancear de novo.

Ferramentas, as duas sem nenhuma mudança em `.cs`:

- `Tools/gerar_itens_lendarios.py`: as fórmulas em código. Gera `Content/items_lendarios.json`.
- `Tools/auditar_balanceamento.py`: compara **todos** os itens com as curvas e escreve `Docs/auditoria_balanceamento.csv`. Rode depois de qualquer mudança de item.

---

## 1. Princípio: um orçamento de poder por nível

Todo item tem um **orçamento** que depende só de quatro coisas:

- o **nível**;
- a **peça** (que fatia do corpo ele ocupa);
- a **classe**;
- a **qualidade**.

O nome, a região e o lugar de onde o item cai **não mudam o orçamento**. Eles só mudam o *sabor*: que atributo extra o item tem e o efeito lendário.

Na prática: um item de nível 40 comum de qualquer região tem o mesmo poder que outro de nível 40 comum. Um lendário de nível 40 é sempre ~18% mais forte e ainda tem um efeito. Isso mata dois problemas:

- o item "melhor que o lendário" que cai de um bicho qualquer;
- o conjunto que é pior que o drop comum, que é exatamente o que a auditoria achou (seção 4).

## 2. As curvas oficiais

As curvas foram tiradas **dos itens que o jogo já tem**, por ajuste linear da progressão principal: as armas `weapon_*`/`arsenal_*` e as armaduras regionais `armor_<classe>_<região>`. Por isso a maior parte do jogo já está nelas, e só o que estava torto aparece na auditoria.

### 2.1 Armas: dano por segundo

```
DPS(L)  = 10 + 2,75 × L                         (arma comum, 1 golpe por segundo)
poder   = DPS(L) × intervalo/1000 × qualidade
```

Os intervalos por subtipo são os que o jogo já usa:

| Subtipo | Intervalo (ms) |
|---|---|
| adaga | 750 |
| arco | 900 |
| espada (1 mão) | 950 |
| cajado / talismã | 1000 |
| lança | 1050 |
| machado (1 mão) / espada de 2 mãos | 1100 |
| martelo | 1250 |
| machado de 2 mãos | 1300 |

**Arma de 2 mãos não ganha DPS extra.** O ganho dela é alcance, arco do golpe e atordoamento. Se ganhar DPS também, o escudo some do jogo.

**Mago e bruxa.** Hoje as armas deles estão em ~0,8 da curva. Isso é aceitável **se** o poder das magias escala por outro atributo. Se a magia usa `weapon_power`, suba essas armas para 1,0.

### 2.2 Armaduras: peitoral comum

A parte fixa da conta entra aos poucos do nível 1 ao 15, porque o começo do jogo é mais leve:

```
entra(L) = mín(1, (L-1)/14)

armadura do peitoral:
  guerreiro   46·entra(L) + 14,0·L
  arqueiro    10·entra(L) + 10,4·L
  mago        20·entra(L) +  6,3·L
  bruxa        6·entra(L) +  7,4·L

vida do peitoral:
  guerreiro   10 + 80·entra(L) + 3,0·L
  mago        10 + 72·entra(L) + 2,7·L
  arqueiro e bruxa   10 + 30·entra(L) + 4,2·L
```

### 2.3 Fração de cada peça (armadura / vida)

| Peça | Fração |
|---|---|
| capacete | 0,40 / 0,40 |
| peitoral | 1,00 / 1,00 |
| luvas | 0,30 / 0,30 |
| calça | 0,45 / 0,52 |
| botas | 0,35 / 0,35 |

Cada peça também tem o **atributo exclusivo** dela (regra do dono, 11/11). Ele cresce de 0 no nível 10 a 1 no nível 60:

- **Capacete:** recarga, XP e, para mago e bruxa, regeneração.
- **Peitoral:** espinhos e redução de dano.
- **Calça:** fôlego e resistência a atordoar.
- **Luvas:** poder, velocidade de ataque e crítico.
- **Botas:** velocidade e esquiva.

### 2.4 Qualidade (multiplica o orçamento)

| Qualidade | × | O que mais tem |
|---|---|---|
| Enferrujada | 0,80 | — |
| Comum | 1,00 | — |
| Refinada | 1,12 | 1 afixo a mais |
| Excelente | 1,25 | 2 afixos a mais |
| Conjunto | 1,25 | bônus de 2/3/4/5 peças |
| **Lendária** | **1,18** | **efeito (proc) + atributo do elemento** |
| Mítica (depois do 60) | 1,35 | efeito + bônus de conjunto |

O lendário fica um pouco abaixo do Excelente em números de propósito, porque o efeito vale ~10–15% de DPS em luta de grupo. Somando os dois, ele é o melhor do nível sem quebrar o teto.

### 2.5 Bônus de conjunto

Ele escala com a armadura do peitoral: `k = armadura do peitoral / 172`.

| Peças | Bônus |
|---|---|
| 2 | vida +18k, poder +3% |
| 3 | golpe duplo +4%, armadura +22k |
| 4 | armadura +34k, poder +4%, crítico +3% |
| 5 | armadura +26k, poder +6%, crítico +5%, duplo +4% e o **atributo do elemento** |

Exemplos do atributo do elemento: Sagrado = cura recebida, Tempo = recarga, Abismo = raio de área.

### 2.6 Preço (o ouro como medida de tempo)

```
peitoral  3150 × (L/20)^2,5
arma      1700 × (L/20)^2,5
```

As outras peças custam uma fração do peitoral: capacete, luvas e botas 0,46; calça 0,51.

- Lendário e conjunto: preço × 1,5.
- Venda ao NPC: 20% do preço. Acima disso, o ouro inflaciona.

### 2.7 Tabela pronta (comum, peitoral)

| Nível | DPS arma | Guerreiro (arm / vida) | Arqueiro | Mago | Bruxa | Preço peitoral | Preço arma |
|---|---|---|---|---|---|---|---|
| 1 | 13 | 14 / 13 | 10 / 14 | 6 / 13 | 7 / 14 | 1 | 0 |
| 5 | 24 | 83 / 48 | 55 / 40 | 37 / 44 | 39 / 40 | 98 | 53 |
| 10 | 38 | 170 / 91 | 110 / 71 | 76 / 83 | 78 / 71 | 556 | 300 |
| 15 | 51 | 256 / 135 | 166 / 103 | 114 / 122 | 117 / 103 | 1.534 | 828 |
| 20 | 65 | 326 / 150 | 218 / 124 | 146 / 136 | 154 / 124 | 3.150 | 1.700 |
| 30 | 92 | 466 / 180 | 322 / 166 | 209 / 163 | 228 / 166 | 8.680 | 4.684 |
| 40 | 120 | 606 / 210 | 426 / 208 | 272 / 190 | 302 / 208 | 17.819 | 9.616 |
| 50 | 148 | 746 / 240 | 530 / 250 | 335 / 217 | 376 / 250 | 31.128 | 16.799 |
| 60 | 175 | 886 / 270 | 634 / 292 | 398 / 244 | 450 / 292 | 49.103 | 26.500 |

## 3. Progressão no tempo (para a curva virar diversão)

- **Troca de equipamento:** um item melhor a cada **2–3 níveis** na peça mais importante (arma ou peitoral). A curva acima dá ~+8% de poder por nível. Pular 3 níveis = +25%: o jogador **sente** a troca.
- **Chance de cair, por qualidade e por monstro do nível certo:**

  | Qualidade | Chance |
  |---|---|
  | Comum | 6% |
  | Refinada | 1,5% |
  | Excelente | 0,4% |
  | Peça de conjunto | 0,15% |
  | Lendário | 0,05% |

  Os chefes multiplicam por 10.
- **Garantia (pity):** cada chefe guarda um contador por jogador. A cada 25 vitórias sem lendário, o próximo saque garante um lendário da faixa dele. Isso evita o "matei 200 vezes e nada".
- **Faixa:** monstros dropam itens de `nível do monstro − 3` até `nível do monstro + 1`. Nada de nível 50 caindo no 30.
- **Tempo para matar** (referência para os monstros): um normal de nível L leva **6–9 golpes básicos** com a arma comum do nível; uma elite 3×; um chefe de região 40–60 s com 3 jogadores. Se mudar a curva das armas, mude a vida dos monstros na mesma proporção.

## 4. Auditoria do que existe hoje

Arquivo: `Docs/auditoria_balanceamento.csv`. São 478 itens: 1 linha por item, com o valor atual, o valor esperado e a razão entre eles.

| Grupo | Itens | Mediana (atual ÷ esperado) |
|---|---|---|
| Armaduras comuns | 60 | **1,00** (a curva bate) |
| Lendários novos | 85 | 1,06 |
| **Peças de conjunto** (`arm_*`, `armadura_*`) | **135** | **0,50** ⚠ |
| Armas comuns | 87 | 0,88 |
| Armas Enferrujadas do arsenal | 12 | **1,13** ⚠ |
| Armas Refinadas do arsenal | 12 | 0,89 ⚠ |
| Armas Excelentes do arsenal | 12 | **0,80** ⚠ |

### 4.1 Os conjuntos valem metade do drop comum (o maior problema)

Todos os conjuntos foram feitos numa curva antiga e mais baixa. A Armadura Dracônica (nível 40) dá 312 de armadura no peitoral. A `armor_guerreiro_geada` comum, do nível 44, dá ~660. Quem junta conjunto **perde** defesa.

**Correção:** multiplique `armor` e `max_health` de todas as peças `set` por **2,5** (0,50 → 1,25, que é o multiplicador de Conjunto). Os bônus de conjunto ficam como estão, porque já escalam por k. É uma linha num script: o jeito mais simples é reaplicar as fórmulas do item 2.2 com qualidade "conjunto".

### 4.2 Arsenal com qualidades invertidas

Hoje a Enferrujada rende mais por nível que a Excelente, porque o poder subiu menos que o nível mínimo entre as qualidades.

**Correção:** recalcular `weapon_power` do `items_arsenal.json` com `poder = DPS(L) × intervalo × qualidade` (0,80 / 1,00 / 1,12 / 1,25).

### 4.3 Itens isolados mais fora da curva

| Item | Peça | Classe | Nível | Razão | Sugestão |
|---|---|---|---|---|---|
| mascara_pandemonio | capacete | guerreiro | 20 | 0,12 | é visual? tirar `armor` ou subir para ~130 |
| helmet_gorro_couro | capacete | guerreiro | 1 | 1,79 | ok (inicial); pode ficar |
| staff_mago_cetro_capataz | arma | mago | 6 | 1,51 | descer para ~25 de poder |
| weapon_guerreiro_malho_capataz | arma | guerreiro | 6 | 1,45 | descer ~30% (drop de chefe: 1,18 no máximo) |
| staff_mago_vara_gemea | arma | mago | 5 | 0,34 | subir para ~24 |
| *_galho_p0 / porrete_p0 / graveto | arma | todas | 1 | 0,47–0,63 | ok (arma inicial é ruim de propósito) |
| hist_* (Costa, lendários de história) | várias | várias | 7 | 0,64–0,68 | subir para 1,18 (são lendários) |
| armor_*_ervas, manto_acolito | peitoral | arq/bru/mago | 4 | 0,64–0,68 | ok, ou subir para 0,85 |

A lista completa (216 fora da faixa 0,85–1,20, sendo 135 só dos conjuntos) está no CSV.

**Meta:** rodar `python Tools/auditar_balanceamento.py` e ver só os itens iniciais (nível 1–5) fora da faixa.

## 5. Os lendários novos (14/11)

- **15 conjuntos × 5 peças = 75 itens:** `arm_<conjunto>_<peça>`. Modelo `arm_<conjunto>:<peça>`, ícone `ico_arm_<conjunto>_<peça>`. Os nós do .glb seguem o padrão: capacete, peitoral, luvas, calca, botas.
- **50 armas** (10 tipos × 5 elementos): `arma_lend_<tipo>_<elemento>`. Modelo de mesmo nome, ícone `ico_lend_<tipo>_<elemento>`.
- Arquivo: `Content/items_lendarios.json`, gerado por `Tools/gerar_itens_lendarios.py`.

| Faixa | Conjuntos | Armas |
|---|---|---|
| 45 | Rúnico Ancestral (mago), Pedra Viva (guerreiro), Floresta (bruxa) | elemento Sagrado |
| 50 | Cavaleiro Sagrado (guerreiro), Gelo Eterno (bruxa), Necromante (bruxa), Abissal (arqueiro) | Gelo |
| 55 | Dragão de Fogo (guerreiro), Abismo (mago), Faraônico (bruxa) | Fogo |
| 60 | Celestial (arqueiro), Demoníaco (guerreiro), Tempo (mago), Vazio (mago), Anjo Caído (arqueiro) | Vazio e Celestial |

As armas, por classe e tipo:

- **Guerreiro:** espada, machado gigante, foice, garfo, espada dupla, lança e manopla.
- **Mago:** cajado.
- **Bruxa:** foco natural.
- **Arqueiro:** arco.

O efeito de cada arma usa as `skill_lend_*` que já existem: espada = corte fantasma, machado = rachar, foice = sangria, garfo/lança = perfurar, manopla = terremoto, cajado = eco, foco = maldição, arco = chuva.

**Os itens nascem sem drop.** Ligar em `loot_*.json` pela tabela do item 3: uma peça por chefe da faixa, com chance baixa e garantia.

## 6. Loja: aba "Recompensas Extras"

Uma moeda própria, a **Ficha de Glória**, que não se compra com ouro. Ela vem do que é *atividade*, não de farm:

| Fonte | Fichas |
|---|---|
| Lugar descoberto | 1 |
| Área secreta | 3 |
| Esfera do Macaco | 5 |
| Chefe de região (1ª vez no dia) | 2 |
| Chefe Errante | 5 |
| Monstro migrante | 5 |
| Evento (invasão, meteoros) | 3 a 8 conforme a participação |
| Conquista | 2 a 10 |

Preços sugeridos:

| Item | Fichas |
|---|---|
| Peça de conjunto lendário, faixa 45 | 40 |
| Peça de conjunto lendário, faixa 50 | 60 |
| Peça de conjunto lendário, faixa 55 | 90 |
| Peça de conjunto lendário, faixa 60 | 130 |
| Arma lendária | 1,5 × a peça da mesma faixa |
| Caixa das Cinzas do seu nível | 8 |
| Pedra de refino | 15 |
| Roupa/visual (sem atributo) | 25 a 80 |

- **Limite:** 1 peça lendária por semana na loja. O drop continua sendo o caminho principal; a loja é a garantia de quem joga muito sem sorte.
- **Nada da loja passa do orçamento do item 2.** A loja só encurta o caminho, não dá poder a mais.

## 7. Processo para item novo (checklist)

1. Escolha o nível, a peça, a classe e a qualidade. **O resto sai das fórmulas** (use as funções de `gerar_itens_lendarios.py`).
2. Afixos e atributo exclusivo: até 15% do orçamento. Efeito lendário: só na qualidade Lendária ou acima.
3. Rode `python Tools/auditar_balanceamento.py`. O item novo tem que ficar entre 0,85 e 1,20.
4. Rode os testes (`dotnet test`).
5. Depois do nível 60 (quando abrir): continuar as mesmas retas e somar a qualidade Mítica (1,35). Não criar curva nova.

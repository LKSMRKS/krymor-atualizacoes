# Lote 5: auditoria dos números de Cinzas da Maré

Data: 14/11/2026. A base `/home/claude/upd1414` não foi tocada. Todo o trabalho está em `/home/claude/lote5/`.

**Como ler.** "Campo" é o farm em mapa aberto, estilo MU. "Ótimo" é a melhor área por XP/s, incluindo repetir dungeon sem limite. Os dois cenários usam o Guerreiro, só com ataque básico (é o pior caso: com habilidades o TTK real é menor). Missões não entram na conta; elas dão 30–50% da XP de cada faixa, então o tempo real fica ~30–35% menor. "Saque/h" é o valor de venda esperado de tudo o que cai.

---

## 1. Os 10 problemas mais graves

| # | Problema (evidência) | Correção | Efeito simulado |
|---|---|---|---|
| 1 | **Monstros comuns herdaram o saque de raro ou chefe.** `gerar_ramos2.py:104` e `gerar_ramificacoes.py:274` copiam a base inteira (`{**b}`). Resultado: o Morcego da Garganta (comum, nv 31–32) solta **100% item raro**. A Salamandra da Caldeira (54–55) também solta 100% raro. O Caranguejo do Triângulo (46–48) usa a tabela do Tirano: arma de chefe 100%, 70% de equipamento e **12% de lendário**, além de `xp_mult` 14 e vida 3,2×. A Sentinela das Ruínas tem `xp_mult` 3,5 e a tabela da elite. Os 3 filhos da Família da Fábula usam a tabela de elite (100% de equipamento). | **A1**: tabela de comum do mesmo tema, `xp_mult` 1,0 e vida normal (ver tabela 5.1). | Saque/h na faixa 41–50 (campo): **1,89 M → 107 mil**. Lendários/h no 41–50: **3,2 → 0,15**. Saque total do 1 ao 60: **32 M → 6,6 M** (só dados) e **4,95 M** com a correção 3. |
| 2 | **Os lendários novos (`items_lendarios.json`) cairiam como peça de conjunto comum.** O `SetPieceDrop` (`GameServer.Map.cs:1216-1219`) sorteia *qualquer* item com `set` a 2,5% por abate comum. O `LegendarySetDrop` (`:1178-1186`) só aceita ids `armadura_*`/`lendaria_*`, e os novos são `arm_*`. Com o arquivo instalado, 50–83% das quedas de conjunto no 45–60 viram peça lendária: **~3–5 lendários por hora de farm**. | Mudança de código, 2 linhas, sem renomear id. No `SetPieceDrop`, acrescentar `&& d.Quality != "lendario"`. No `LegendarySetDrop`, acrescentar `\|\| d.Quality == "lendario"` ao filtro. O `ItemDefinition.Quality` já existe (`Definitions.cs:90`). | Os lendários passam a cair só de elite (1,2%), chefe (8%) e da regra A3. |
| 3 | **A venda paga mais que a loja cobra.** `Venda.cs:19-22` multiplica 1/4 do preço por: lendário ×10, Excelente ×8, chefe ×6, V15 ×15,3. Ou seja, vende por **2,5× / 2× / 1,5× / 3,8× do preço de compra**. Exemplo: a espada da Acrópole custa 57.000; como lendária vende por 142.500 e como "chefe" por 85.500. O Santuário (só admin) vende Excelente pelo preço-base (`World.cs:754`): quem tem acesso compra 1 e vende 2. | Código: teto de 60% do preço (V15: 80%). Incomum 1,2 · raro 1,5 · épico 1,8 · chefe 2,0 · Excelente/lendário 2,4 · V(n) = mín(2,4; 1+0,1(n−1)+0,3·[n≥10]) · V15 3,2. Fica em `conteudo.mult_venda`, com a opção `--codigo` do simulador. | Saque total 1→60 no cenário ótimo: **15,7 M → 7,4 M**. A espada lendária da Acrópole: 142.500 → 34.200. |
| 4 | **Dungeon repetível sem limite é o "meta" de XP e de saque.** Cada volta (~3–5 min) tem um chefe com arma "chefe" 100% e armadura 100% (`gerar_regioes.py:671-672`), mais 35% de conjunto, 35% de caixa, 10% de Pandora e 8% de lendário (`Map.cs:1172,1196`; `Caixas.cs:44`; `Pandora.cs:29`). No original: lendário **1,3/h** na dungeon contra **0/h** no campo (21–60), e 39 h até o 60 contra 46 h no campo. | **A2** (dados): no chefe de instância, arma "chefe" 1,0 → 0,4 e armadura 1,0 → 0,6. A tabela `primeira_<dungeon>` continua garantindo a 1ª vitória. Código (proposta): `LegendaryBossChance` 0,08 → 0,025 dentro de instância. | Lendário/h na dungeon (21–60): **1,29 → 1,04** (só dados) **→ 0,53** (com código). Saque/h no ótimo 51–60: 901 mil → 418 mil. |
| 5 | **No campo, o lendário quase não existe.** Só cai de elite (1,2%) e de chefe (8%). Com farm de campo, o 1º lendário chega depois de **~6.400 abates, ~30 h** (nível 47). Referência AAA: ~1 a cada 1–6 h (seção 4). | **A3**: 0,1% de lendário (cópia da entrada de equipamento da própria tabela) no comum de mapa aberto do nível 15+. Regra idempotente no `regular_economia.py`, que cobre todos os geradores (45 tabelas). | 1º lendário: **30 h → 9 h**. Ritmo de **0,15–0,19/h** no campo, ou 1 a cada 5–7 h. |
| 6 | **O TTK sobe no fim do jogo.** A vida do monstro cresce 0,45·L² (`CombatFormulas.cs:83`); o poder do jogador cresce linear (`:17-18`). O comum leva 6 golpes básicos do 1 ao 30, depois 7,8 no 35, **9,4 no 55 e 9,8 no 60**. | **A8**: vida do comum do 31+ × (1 − 0,012·(L−30)), com mínimo de 0,68. Fica em `vida_tardia()` no `gerar_regioes.py` (69 monstros). | O comum fica entre **6,2 e 6,8 golpes** (3,6–3,9 s) do 30 ao 60. Minutos por nível no 51–60: 81–94 → 68–76. |
| 7 | **O Vale das Cinzas (11–16) é pulado.** O Vale paga `xp_multiplier` 1,5 (`gerar_regioes.py:1241`) e a Mata paga 2,2. Já no nível 12 o jogador vai bater em monstro 19–20 da Mata: **18,7 golpes, 10,8 s por abate**. Isso porque, desde 14/11, o dano contra monstro acima cai só 5% por nível. | **A10**: Vale 1,5 → **1,8**. | O Vale passa a ser usado do 10 ao 12: TTK 3,3–4,1 s em vez de 9,5–10,8 s. A Mata fica do 13 ao 15. |
| 8 | **O simulador oficial está desatualizado.** `Tools/simular_progressao.py:39-41` ainda usa −12% por nível (mínimo 0,3), mas o servidor usa −5% depois de +2 (mínimo 0,5) (`CombatFormulas.cs:35-39`). O simulador também ignora o `PisoDeNivel` (`ContentLoader.cs:20`) e o `--ate 61` simula 60→61, que o servidor não tem (`Progression.cs:6,19`). Com isso, o "45,0 h até o 60" da base vira **40,3 h** com a regra certa. | `patches/simular_progressao.py.diff`. O `simular_lote5.py` já aplica o piso e a regra nova. | O simulador volta a bater com o servidor. |
| 9 | **O Mago é ~30% mais lento que o Guerreiro: 62,9 h contra 46,3 h.** O cajado regional tem 0,92 do poder da espada (`gerar_regioes.py:186`), a seta de brasa tem coeficiente 0,8, e o manto dá 0,45× de armadura. Ressalva: o simulador é só de ataque básico e usa a mesma taxa de acerto do monstro para corpo a corpo e à distância. | **A11**: cajado regional = **1,05 ×** espada (`round(power*0.92)` → `round(power*1.05)`). | Mago à distância (acerto 25%): 49,4 h, contra 47,9 h do Guerreiro. Com acerto 45%: 62,9 → 59,8 h. |
| 10 | **Itens e monstros fora da faixa.** (a) As armas do Capataz têm `min_level` 6, mas o chefe aparece no nível 16 do Vale. O malho (48 de poder, 1.250 ms, 38,4 DPS) empata com a espada do nível 11 (37,9) e é a melhor arma do 6 ao 16. (b) Os Titãs nível 99 não têm `loot_table_id`, e o `DropLoot` sai na 1ª linha (`Map.cs:1102`): matar o "Impossível" não solta nada, nem joia. | (a) `items.json`: `min_level` 6 → **11** nos dois itens (poder mantido). (b) Nova tabela `loot_tita` (lendário 100%, 3–5 Joias do Vazio, 1 Joia da Forja) em `loot_novos.json`, ligada em `gerar_ramos2.py:108`. | (a) A arma deixa de anular as do 7 ao 10. (b) O Titã passa a valer o esforço. |

---

## 2. Fórmulas reais do servidor (com arquivo e linha)

| Sistema | Fórmula / valor | Onde |
|---|---|---|
| XP para o próximo nível (mapa) | 100·L·(30 + 18·L), com nível máximo 60 | `Server/Combat/Progression.cs:69-71`, `:6` |
| XP por abate | 100·nv do monstro × fator de nível × `xp_multiplier` do mapa × `xp_mult` do monstro × (1 + XP do equipamento) × caçada/evento/dungeon em grupo. Fator: até 2 níveis abaixo 1; 3→0,8 · 4→0,6 · 5→0,45 · 6→0,3 · 7+→0,15; monstro acima = 1 | `Progression.cs:36-53`; `GameServer.cs:942-952`; `GameServer.Map.cs:1923`; `Rotina.cs:94-99` |
| Grupo | XP total × (1 + 0,35·(N−1)) ÷ N; em dungeon, mais 10% por membro extra | `Progression.cs:48-53`; `Rotina.cs:98` |
| Modo difícil | monstro +5 níveis, vida ×1,8, dano ×1,35, XP ×1,6, um sorteio de saque a mais | `GameServer.Map.cs:1575-1576`, `:1104` |
| Vida / poder do jogador | vida 180 + 14L + 9·vigor + equipamento; poder = arma + 2,2·principal + 1,2L; 3 pontos por nível | `CombatFormulas.cs:14-18`; `GameDatabase.cs:34` |
| Armadura | red. = A/(A + 180 + 18·nv do atacante), no máximo 65% | `CombatFormulas.cs:20-25` |
| Diferença de nível no dano | jogador contra monstro acima: 1 até +2, depois −5% por nível (mínimo 0,5). Monstro contra jogador: +15% por nível acima (até 2,5), −8% por nível abaixo (mínimo 0,5) | `CombatFormulas.cs:35-49` |
| Monstro | vida (180 + 18L + 0,45L²)·arquétipo·`health_mult`; dano (12 + 2L + 0,12L²)·…; armadura 20 + 5L | `CombatFormulas.cs:78-88`; `Map.cs:1578-1580` |
| Piso de item | todo item nível 3+ tem pelo menos 90% da curva. Arma: (2,6L+8; cajado 2,2L+8) × intervalo. Armadura: pesada 14,3L+30, média 9,7L+40, leve 6,7L+30, × fração da peça | `Shared/Content/PisoDeNivel.cs:13-59`; `ContentLoader.cs:20` |
| Moedas por abate | sorteio entre `coins_min` e `coins_max`, dividido entre os participantes, × (1 + opção Excelente). Régua: C(L) = 3 + 2,2L + 0,025L² × vida^0,6 × (elite 2,5 · raro 5 · chefe 8) | `World.cs:1302-1313`; `Tools/regular_economia.py:25-31` |
| Prêmio de nível | a cada 5 níveis: 25·L moedas + 3 poções (e tomo no 10/15) | `World.cs:1424-1427` |
| Saque da tabela | cada entrada rola sozinha; `Nivelar` troca o equipamento pela peça da faixa do monstro (−15/+2) | `Map.cs:1063-1098`; `Nivelar.cs:18-31` |
| Qualidade do equipamento comum | escala 0 no nv 3 até 1 no nv 18: incomum 22%, raro 9%, épico 3%; Excelente 0,8% (nv 15+) | `Map.cs:1060-1061`, `:1081-1090`, `:1251-1252` |
| Drops genéricos | joias: Maré 1,2% (nv 3+), Brasa 0,5% (10+), Vazio 0,2% (20+), com elite ×3 e chefe ×10. Arsenal 2%. Lendário de conjunto: chefe 8%, elite 1,2%. Peça de conjunto: comum 2,5%, elite 8%, chefe 35%, variação V1–V15 (cada degrau pesa 0,6 do anterior). Excelente de conjunto: 1,6% / 5%. Caixa: raro 100%, chefe 35%, elite 4%, comum 0,3%. Mapa do tesouro: 0,4% / 3%. Pandora: chefe 10%, elite 1%. Coleção: chefe 5% / 2,5% / 0,8% (elite ÷8) | `Map.cs:1110-1124`, `:1143`, `:1172-1190`, `:1196-1250`; `Caixas.cs:41-46`; `Pandora.cs:26-30`; `Colecao.cs:24-38` |
| Loja | preço do JSON; peça de conjunto com ágio de 3,8× (até nv 10) caindo a 1× (nv 28+); loja `*_excelente` entrega qualidade Excelente | `World.cs:628-633`, `:754` |
| Venda | máx(5, preço/4) × qualidade/variação × (1 + 0,25·refino), com piso de 6·nível | `World.cs:640-646`; `Shared/Content/Venda.cs:12-22` |
| Sumidouros | refino 40·n² moedas mais a joia (+1..+9 = 11.400; +1..+15 = 49.600); reforja 2.000·⌊L/5⌋; tomos (guerreiro: 146 mil no total); montarias (855 mil); asas do Aurélio (489 mil); braceletes (456 mil); poções. **Não há reparo.** | `Refine.cs:26`; `Refino.cs:35`; `shops*.json` |

---

## 3. Simulação

### 3.1 Método

Os scripts `simular_lote5.py` e `conteudo.py` usam os JSON reais e as fórmulas da seção 2.

- O equipamento típico por nível vem do `progressao_simulada.json`, com o `PisoDeNivel` aplicado.
- As áreas são as zonas reais dos mapas.
- O tempo por abate é a soma de TTK, deslocamento de 8 s e regeneração (o limite é o tempo de respawn).
- O monstro acerta 45% das preparações.
- O saque é o valor de venda esperado: tabela + `Nivelar` + subida de qualidade + joias, arsenal, lendário, conjunto V1–V15 e caixa.

### 3.2 Resultado por faixa (Guerreiro)

| Faixa | Variante | Horas | Min/nível | Golpes/abate (média da área) | Moedas/h | Saque/h | Poção máx/h | Lendário/h | Excelente/h |
|---|---|---|---|---|---|---|---|---|---|
| 1–10 | original campo | 2,2 | 4–28 | 7,4 | 6.032 | 4.976 | 1.067 | 0,05 | 0 |
| 11–20 | original campo | 5,6 | 30–39 | 10,7 | 10.516 | 27.836 | 3.765 | 0 | 0,15 |
| 21–30 | original campo | 6,6 | 34–44 | 7,4 | 19.388 | **377.981** | 4.356 | 0 | 0,17 |
| 31–40 | original campo | 8,3 | 37–61 | 8,1 | 28.947 | **724.212** | 4.837 | 0 | 0,55 |
| 41–50 | original campo | 10,7 | 50–75 | **19,9** | 27.559 | **1.887.636** | 6.883 | **3,23** | 0,69 |
| 51–60 | original campo | 12,8 | 81–94 | 6,5 | 41.606 | 240.168 | 8.527 | 0 | 0,20 |
| 1–10 | ajustado campo | 2,2 | 4–27 | 8,0 | 5.168 | 4.694 | 1.601 | 0,05 | 0 |
| 11–20 | ajustado campo | 5,5 | 30–39 | 9,7 | 9.911 | 22.506 | 3.816 | 0,12 | 0,14 |
| 21–30 | ajustado campo | 7,3 | 34–52 | 5,3 | 17.975 | 45.872 | 2.879 | 0,19 | 0,24 |
| 31–40 | ajustado campo | 9,7 | 52–77 | 5,5 | 25.486 | 75.050 | 6.607 | 0,18 | 0,21 |
| 41–50 | ajustado campo | 12,5 | 64–88 | 6,4 | 27.489 | 107.050 | 9.216 | 0,15 | 0,17 |
| 51–60 | ajustado campo | 10,7 | 68–76 | 4,6 | 39.742 | 225.617 | 7.549 | 0,16 | 0,23 |
| 41–50 | original ótimo | 9,5 | 46–66 | 17,7 | 35.844 | 1.250.164 | 7.710 | 2,58 | 0,30 |
| 51–60 | original ótimo | 8,4 | 52–61 | 20,0 | 48.792 | 901.206 | 7.053 | 0,95 | 0,27 |
| 41–50 | ajustado ótimo | 9,4 | 45–64 | 12,0 | 40.145 | 220.834 | 6.517 | 0,69 | 0,29 |
| 51–60 | ajustado ótimo | 7,7 | 47–56 | 16,6 | 50.293 | 417.637 | 6.625 | 0,60 | 0,29 |

- **Total até o 60:** original 46,3 h (campo) / 39,2 h (ótimo); ajustado 47,9 h / 39,0 h.
- **Moedas de abate + prêmio:** ~1,2–1,3 M.
- **Saque vendável:** 32,0 M (original, campo) → 6,6 M (só dados) → 4,95 M (com código).
- **Por nível:** está no `progressao.csv`, com 4 variantes × 59 níveis.

**A curva no ajustado (campo)** sobe sem buracos:

| Níveis | Min/nível |
|---|---|
| 1–10 | 4 → 27 |
| 11–23 | 30–39 |
| 24–38 | 41–57 |
| 39–49 | 64–88 |
| 50–59 | 68–77 |

- O pico é no 47–48, na troca Véspera → Escória. A Coroa da Geada rende 90–96% da melhor área no 44–48: é alternativa, não buraco.
- No original, os buracos eram "atalhos": 26–34 no Morcego, 39–43 na Sentinela, 47–50 no Caranguejo.

### 3.3 TTK por faixa (Guerreiro, ataque básico, monstro do próprio nível com a vida mediana real)

| Nível | Comum, original (golpes / s) | Comum, ajustado | Elite (golpes / s / % da vida sofrida) | Chefe solo (golpes / s / % da vida) |
|---|---|---|---|---|
| 1 | 6,5 / 3,9 | igual | 20,7 / 12,4 / 12% | 64,7 / 38,8 / 67% |
| 10 | 6,0 / 3,6 | igual | 8,2 / 4,9 / 5% | 17,5 / 10,5 / 10% |
| 20 | 5,6 / 3,2 | igual | 18,9 / 10,9 / 21% | 29,8 / 17,1 / 30% |
| 30 | 6,5 / 3,7 | 5,9 / 3,4 | 24,8 / 14,3 / 33% | 23,6 / 13,6 / 29% |
| 40 | 8,1 / 4,7 | 6,4 / 3,7 | 28,4 / 16,3 / 54% | 55,4 / 31,9 / 122% |
| 50 | 8,0 / 4,6 | 6,4 / 3,7 | 33,5 / 19,3 / 74% | 33,5 / 19,3 / 86% |
| 60 | 9,8 / 5,7 | 6,8 / 3,9 | 38,5 / 22,2 / 105% | 57,3 / 33,0 / 157% |

As tabelas completas estão em `ttk_original_guerreiro.txt` e `ttk_ajustado_guerreiro.txt`.

- **A elite do 10–15 é fraca demais:** 8 golpes, com `health_mult` 1,5. A partir do 20 ela salta para 3,4–4,7.
- **O chefe do 40+ solo exige poção ou grupo.** É coerente com o MU.

### 3.4 Primeiro item de cada raridade (abates e horas de jogo, campo)

| Raridade | Original | Ajustado |
|---|---|---|
| Peça de conjunto | 33 / 0,3 h | 34 / 0,3 h |
| Incomum | 648 / 2,5 h | 426 / 1,9 h |
| Raro | 818 / 3,5 h | 564 / 2,5 h |
| Épico | 1.138 / 5,6 h | 942 / 4,5 h |
| Excelente | 1.771 / 8,5 h | 1.788 / 8,6 h |
| Lendário | **6.384 / 29,8 h** | **1.913 / 9,1 h** |
| Prata (V7+) | 2.582 / 12,0 h | 2.575 / 11,6 h |
| Ouro (V10+) | 3.229 / 15,5 h | 3.246 / 14,2 h |

### 3.5 Economia (moedas ganhas e gastas)

- **Moedas por hora no campo:** 5 mil (1–10) → 40 mil (51–60).
- **Venda:** sem as correções 1 e 3, o saque vendável chega a ser 5–25× as moedas. Com elas, fica 2–6×, na faixa de D2/MU, onde a venda ao NPC é a maior fonte de ouro.
- **Poções:** curar tudo com poção custa no máximo 1–9 mil por hora (10–25% das moedas). É sustentável.
- **Compras grandes, em horas de moeda pura no nível:**

| Compra | Preço | Horas |
|---|---|---|
| Montaria do 55 | 260 mil | ~6,5 h |
| Asas do 38 | 85 mil | ~3 h |
| Peitoral Brasaforte do 50 | 85 mil | ~3 h |
| Tomos do Guerreiro (todos) | 146 mil | ~5 h no 40 |

- **Fraco:** o refino (+1..+9 = 11.400 moedas, ~20 min no 50; o que trava é a joia, como no MU). Também não há reparo. Proposta (código, não aplicada): `Refine.Coins = 40·n²·(1 + L/20)`, o que deixa o refino 3–4× mais caro no 40–60.

---

## 4. Referências AAA e MMO de farm (aproximado, de memória)

| Medida | Referência | Cinzas (ajustado) |
|---|---|---|
| Tempo até o "nível de conteúdo final" | D2: ~15–25 h até o 70–80. D4 no lançamento: ~25–35 h até o 50. PoE: ~10–15 h na campanha até o 68–70. Lost Ark: ~15 h até o 50. MU (servidor oficial): centenas de horas. | 39–48 h solo, só básico, sem missões (~30 h com missões). É lento como MU e longo, mas razoável para "farm". |
| Golpes no comum | D3/PoE/Lost Ark: 1–3 (área). D2/MU: 2–5. | 6–7 básicos, menos com habilidade de área. |
| Elite / chefe | elite 10–20 s; chefe de mapa 30–90 s solo, ou grupo | elite 11–22 s; chefe 17–33 s solo (40+ com poção ou grupo). |
| Item lendário/único | D3/D4: vários por hora. D2: ~1 único/h em MF. MU: Excelente a cada várias horas. | campo: 1 a cada 5–7 h; dungeon: ~1 a cada 1,5–2 h. |
| Excelente (MU) | ~1 a cada 2–6 h | 1 a cada 4–7 h. |
| Ouro: venda ÷ moeda que cai | D2 ~2–5× (a venda ao NPC domina); D3: o ouro do chão domina | 2–6× (original: até 70×). |
| Curva de XP | cresce ~1,1–1,3× por nível no meio do jogo; o tempo por nível cresce aos poucos | os minutos por nível crescem de forma monótona; pico de 88 min no 47. |

---

## 5. Ajustes propostos (arquivo, atual → novo, porquê, efeito)

**Aplicados em dados** (`/home/claude/lote5/Content_ajustado`, por `aplicar_ajustes.py`; registro linha a linha em `ajustes_aplicados.log`). O espelho nos geradores está em `patches/*.diff`, com as cópias editadas em `Tools_ajustado/`. Nenhum id foi renomeado.

### 5.1 Tabela dos ajustes

| Id | Arquivo (gerador) | Atual → novo | Porquê | Efeito |
|---|---|---|---|---|
| A1 | `monsters_ramos2.json` (`gerar_ramos2.py`, `COMUM_DE_ESPECIAL`) | `monster_garganta_morcego`: loot `loot_canions_morcego_albino`→`loot_canions_morcego`, vida 1,8→0,8, xp 4→1 | comum com tabela de raro (100% item raro) | saque/h no 26–34: ~0,5–1,0 M → ~45–70 mil |
| A1 | idem | `monster_caldeira_salamandra`: `loot_escoria_salamandra`→`loot_escoria_elemental_fornalha`, vida 2→1, xp 4→1 | idem | idem |
| A1 | `monsters_ramificacoes.json` (`gerar_ramificacoes.py`) | `monster_tri_caranguejo`: `loot_tirano`→`loot_vespera_afogado`, vida 3,2→1,5, xp 14→1 | comum com tabela e XP de chefe | lendário/h no 47–50: 3,2 → 0,15 |
| A1 | idem | `monster_auto_sentinela`: `loot_sentinela`→`loot_acropole_guardiao_bronze`, xp 3,5→1 | comum com tabela de elite | saque/h no 39–43: 1,5 M → ~90 mil |
| A1 | `monsters_fabula.json` (`gerar_fabula.py:75-77`) | filho, filha e bebê: `loot_fab_familia`→`loot_fab_castelo` | comum com tabela de elite | — |
| A2 | `loot_midgame.json`, `loot_merlin.json`, `loot.json`… (`gerar_regioes.py`, `add_monster(..., instancia=True)`) | chefe de instância: arma "chefe" 1,0→0,4; armadura 1,0→0,6 (12 entradas) | dungeon repetível sem limite | ótimo: saque 28 M → 15,7 M (A1+A2) |
| A3 | 45 tabelas de comum (`regular_economia.py`, regra idempotente) | + entrada "lendario" 0,001 | o campo não tinha lendário | 1º lendário 30 h → 9 h; 0,15–0,19/h |
| A7 | `items.json` | `weapon_guerreiro_malho_capataz` e `staff_mago_cetro_capataz`: `min_level` 6→11 | a arma de chefe do 16 estava marcada nível 6 | deixa de anular as armas do 7–10 |
| A8 | 69 monstros comuns nv 31+ (`gerar_regioes.py`, `vida_tardia`) | `health_mult` × (1−0,012(L−30)), mín 0,68 | TTK subia de 6 para 10 golpes | 6,2–6,8 golpes até o 60 |
| A9 | `loot_novos.json` + `monsters_ramos2.json` (`gerar_ramos2.py:108`) | Titãs: `loot_table_id` null → `loot_tita` | o DropLoot saía sem soltar nada | recompensa à altura |
| A10 | `map_vale.json` (`gerar_regioes.py:1241`) | `xp_multiplier` 1,5→1,8 | o Vale era pulado | TTK no 10–12: ~10 s → ~4 s |
| A11 | `items_midgame.json` (`gerar_regioes.py:186`) | `staff_mago_cajado_*`: 0,92×→1,05× o poder da espada (mata 48→55 … acrópole 141→161) | Mago ~25% mais lento | Mago 49,4 h ≈ Guerreiro 47,9 h (acerto 25%) |
| — | `Tools/simular_progressao.py:39-41` | `gap_player` com a regra de 14/11 | o simulador oficial errava ~10% | 45,0 h → 40,3 h (bate com o servidor) |

Depois dos ajustes, o `regular_economia.py` (versão do `Tools_ajustado`) roda por último, como no `gerar_tudo.py`. Ele recalcula moedas e preços dos monstros e itens alterados. Exemplo: o Caranguejo do Triângulo passa de 225–418 para 148–276 moedas.

### 5.2 Propostas que exigem `.cs` (não aplicadas; efeito simulado com `--codigo`)

| Onde | Atual → novo | Efeito |
|---|---|---|
| `Shared/Content/Venda.cs:19-22` | multiplicadores com teto de 60% do preço (seção 1, #3) | saque ótimo 15,7 M → 7,4 M; acaba "vender por mais que a loja" |
| `GameServer.Map.cs:1172` | `LegendaryBossChance` 0,025 quando `m.Zone.Instanced` | lendário/h na dungeon 1,04 → 0,53 |
| `GameServer.Map.cs:1186`, `:1219` | `Quality == "lendario"` entra no `LegendarySetDrop` e sai do `SetPieceDrop` | os 125 itens de `items_lendarios.json` passam a cair como lendário (raro), não como conjunto comum |
| `World.cs:754` | `ShopQuality` Excelente só para admin, ou venda de item comprado = preço-base | fecha a arbitragem do Santuário |
| `Shared/Content/Refine.cs:26` | `40·n²` → `40·n²·(1+L/20)` | sumidouro de moeda no 40–60 |

### 5.3 Sobre os lendários novos (`/home/claude/merlin/trabalho_pc/Content/items_lendarios.json`)

- **Ids:** nenhum colide com o Content. O Harness validou o pacote com eles instalados: só os 5 erros conhecidos.
- **Números:** coerentes com o guia.
  - Peitoral Pedra Viva (45): 845 de armadura, contra 662 do regional do 44 (1,28×).
  - Demoníaco (60): 1.108, contra 830 do regional do 56.
  - A espada lendária do 45 tem 158 de DPS, igual à espada regional do 56. Isso é +18% pela qualidade: 6–7 níveis de vantagem, aceitável para lendário.
- **Preço:** o `price` dele (fórmula 3150·(L/20)^2,5) vai ser sobrescrito pelo `regular_economia.py` quando o arquivo entrar no `Content`. Tudo bem.
- **O que corrigir antes de instalar:** o drop, que é o problema #2. Os itens "nascem sem drop", mas na prática cairiam a 2,5% por abate comum.
- **Guia, seção 4.1 ("conjuntos valem metade"):** está desatualizada. O `PisoDeNivel` (14/11) já sobe toda peça para 90% da curva quando o servidor carrega o conteúdo. Medido no JSON cru: 0,45–0,6; em jogo: ≥0,9.

---

## 6. Validação e arquivos

**Harness.** O Content da pasta `/home/claude/merlin/repo3/Harness/bin/Release/net8.0` foi copiado para `backup_harness_Content`, testado e **restaurado** (`diff -rq` sem diferença).

| Conteúdo testado | Resultado do `dotnet Harness.dll --validar` | Registro |
|---|---|---|
| `Content_original` | `VALIDACAO 5 erro(s)`, todos "caixa_pandora_N: sem efeito", da base | `validar_original.txt` |
| `Content_ajustado` | os mesmos 5, nada novo | `validar_ajustado.txt` |
| `Content_ajustado` + `items_lendarios.json` + `shops_lendarios.json` | os mesmos 5 | `validar_ajustado_com_lendarios.txt` |

Não rodei `dotnet test` nem o jogo. Os testes podem conferir valores de monstro ou item.

**Arquivos em `/home/claude/lote5/`:**

| Arquivo | O que é |
|---|---|
| `RELATORIO_NUMEROS.md` | este relatório |
| `progressao.csv` | por nível × 4 variantes: minutos, kills, TTK, golpes, XP/abate, moedas, saque/h, poção, lendário/h |
| `resumo_faixas.txt` | o resumo por faixa da seção 3.2 |
| `conteudo.py` | carregador e fórmulas do servidor com arquivo:linha |
| `simular_lote5.py` | simulador de progressão, economia e raridade (`--content`, `--classe`, `--cenario campo\|otimo`, `--codigo`, `--csv`) |
| `ttk_faixas.py`, `ranking_areas.py`, `gerar_csv.py` | TTK por faixa, ranking de áreas num nível, geração do CSV |
| `aplicar_ajustes.py` | `ORIGEM DESTINO`: gera `Content_ajustado` e roda o regulador |
| `ajustes_aplicados.log` | cada mudança feita |
| `Content_ajustado/` | o patch aplicado |
| `Content_ajustado_com_lendarios/` | o patch + os lendários novos, só para teste |
| `Tools_ajustado/`, `patches/*.diff` | os geradores e o simulador oficial corrigidos (`gerar_regioes`, `gerar_ramos2`, `gerar_ramificacoes`, `gerar_fabula`, `regular_economia`, `simular_progressao`) |

**Próximo passo para o programador:**

1. Aplicar os `.diff` em `Tools/`.
2. Pôr a tabela `loot_tita` em `loot_novos.json` e o `min_level` do Capataz em `items.json`.
3. Rodar `gerar_tudo.py` e depois `python3 Tools/simular_progressao.py --ate 60`.
4. Rodar `dotnet test`.
5. Decidir as 5 propostas de código da seção 5.2. A mais urgente é a do problema #2, antes de instalar os lendários.

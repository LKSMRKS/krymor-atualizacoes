# Economia e drops (14/11, pedido do dono)

> "Itens, missões, fabricados, lojinha — tudo progressivo, matematicamente, de acordo com o nível; o item tem que valer
> o que cobra. Todo o jogo deve seguir essa premissa." · "Drops fracos: use o sistema de drop do MU."

## Régua única (`Tools/regular_economia.py`, roda no fim do `gerar_tudo.py`)

| O quê | Fórmula |
|---|---|
| Moedas por abate | C(L) = 3 + 2,2·L + 0,025·L² (mín. 70%, máx. 130%) × vida^0,6 × (elite 2,5 · raro 5 · chefe 8) |
| Arma de referência | P(L) = C(L) × (8 + 4,5·L) — ≈ abates no próprio nível para comprar a arma da faixa |
| Preço de qualquer equipamento | P(L) × peso do espaço × força^1,5 × (1 + extras) × (conjunto 1,25 · lendário 1,6) |
| Venda | 1/4 do preço × qualidade × variação × refino (`Shared/Content/Venda.cs`) |
| Missão | C(L) × trabalho (mate N: N+6 · chefe 25 · coleta 0,8N+6 · caçada/defesa 14 · conversa 4…); repetível ×0,6 |
| Fabricação | relatório: valor dos materiais ÷ valor do produto (bom entre 0,3 e 1,5) |

* **força** = atributo principal do item ÷ curva do nível (a mesma do `PisoDeNivel`, 100%). Item fraco custa menos.
* **peso do espaço**: arma 1 · peitoral 0,8 · escudo 0,6 · calça 0,5 · elmo 0,45 · botas 0,4 · luvas 0,35 · anel 0,7 ·
  colar 0,8 · capa 0,9 · asas 1,5 · montaria 2 · mascote 0,8 · bracelete 1.
* **extras**: % de poder ×6, crítico ×4, velocidade ×3, esquiva ×4, redução de dano ×5... (até +200%).
* Todo equipamento tem valor (dá para vender/desmanchar), inclusive os que só caem. Entrar na loja continua sendo pela
  lista da loja. Cosméticos (skin, mascote, montaria) nunca ficam abaixo da régua e podem custar mais pela aparência.

## Drop estilo MU (`Shared/Content/OpcoesMu.cs`, `GameServer.Map.cs`)

Escada de qualidade, cada degrau ~2,5× mais raro (cheia do nível 18; antes do nível 3 tudo cai liso):

| Qualidade | Chance | Linhas | Sorte | Opção +N | Excelentes ★ |
|---|---|---|---|---|---|
| Comum | resto | 0 | — | — | — |
| Incomum | 22% | 1 | 15% | 30% (+1/+2) | — |
| Raro | 9% | 2 | 25% | 45% (até +3) | — |
| Épico | 3% | 3 | 35% | 60% (até +4) | — |
| Excelente | 0,8% (nível 15+) | 3 ×1,5 | 50% | 75% | 1–2 (V13+: 2–3) |

* **Sorte**: +5% de crítico. **Opção +N**: arma +2% de dano por N; armadura + (2 + 0,25·nível)·N de armadura.
* **Excelentes** (como no MU): arma — vida ao matar, recurso ao matar, velocidade de ataque, dano +%, golpe excelente
  (crítico), dano por nível; armadura — vida máx. +4%, recurso, redução de dano +4%, reflete +5%, esquiva +4%,
  moedas ao matar +30%.
* Item NÃO cai com refino (+N): só as joias caem, e o jogador sobe se quiser (decisão do dono, 14/11 tarde).
* No chão, o item bom mostra o nome com a raridade e as opções (★, Sorte, Opção +N).
* Conjuntos V1–V15 (Variacao.cs) seguem iguais; V4+ também ganham Sorte/Opção e V10+ Excelentes.

## Coleção do dono (`Tools/gerar_colecao_dono.py`)

* 25 asas `asas_*` (todas as classes, G = voar): comuns 25–29 e incomuns 30–38 com o Aurélio, o Mestre das Asas (Astrália); raras
  40–48, épicas 50–56 e lendárias 58–60 de chefe da faixa (5% / 2,5% / 0,8%, elite ÷8) e na Loja de Cristais.
* 15 capas (12 novas + Real, Gelo e Floresta repaginadas) com estampa por shader, recorte da barra e enfeite 3D.
* 6 skins: Macaco Vermelho, Esqueleto, Fantasma e Alien no Mascate; Tubarão e Lava de chefe e na Loja de Cristais.

## Caixa de Pandora +1..+5 (`GameServer.Pandora.cs`)

Caixa do Kundun do MU: não abre na mão — **jogada no chão estoura** e espalha 2+N a 4+N itens da faixa (+1 1–12 …
+5 49–60) que qualquer um perto pode pegar, mais moedas para quem abriu. Equipamento da classe (incomum → Excelente),
peça de conjunto V, arsenal, joias, tônicos; +4/+5 podem trazer asa/capa/skin da coleção; 3% traz outra Pandora +N+1.
Cai de chefe (10%) e elite (1%). No Santuário (teste) a Pandora, a Guardiã das Caixas, vende todas.

## Receitas do Caos

Lendárias (30%): a arma-base é a do arsenal da mesma família no degrau do nível e as joias completam ~50% do valor da
lendária. Outras receitas do Caos com materiais abaixo de 25% do produto têm as joias multiplicadas até 50%.

## Poder alinhado item a item (`Tools/alinhar_poder.py`, 14/11)

Força = atributo principal ÷ curva de 100% do nível. Loja/região 1,00 · arsenal 0,92–1,12 · conjuntos do dono (Meshy)
1,15 + linhas da variação V1–V15 · lendários 1,15 + 1 linha. Os lendários do trabalhador foram para 42–50: os últimos
conjuntos do dono (nível 50, em V10–V15) continuam sendo o topo. Asas: curva única × raridade (comum 0,75 → lendária 1,2);
capas e skins também numa curva só. O teste `Poder1411Tests` confere todos os itens.

## Ritmo de drop (`Docs/RITMO_DE_DROP.md`, gerado pelo `RitmoDeDropTests` com o sorteio real)

Sozinho no campo (270 abates/h): equipamento a cada ~2 min (quase tudo comum/cinza), melhor que comum a cada ~7 min,
raro a cada ~15 min, épico/V7+ a cada ~50 min, joia a cada ~12 min, Excelente ★ a cada 2–4 h, lendário a cada 10–40 h
(chefe, dungeon e grupo: mais rápido). Mudanças: Excelente 0,8% → 1,3%; lendário de elite 1,2% → 0,5%; lendário no comum
do campo (nível 15+) 0,018%; criatura dourada 3% → 1,2%; chefe de dungeon: arma 60%, armadura 70%, lendário 5%.
Drop de topo: feixe de 14 m, estouro de brilho e som próprio; raro/épico/joia: sininho e aviso.

## Monstros 31+ e refino

Comum do nível 31+: −0,9% de vida por nível (mínimo 75%) → 7–8 golpes até o 60. Refino: +1..+6 = 40·n²;
+7..+9 × (1 + nível/20); +10..+15 × (1 + nível/8), além das joias e da chance de falhar.

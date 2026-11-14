# Formas (transformações), Máscara do Pandemônio e Vinte-e-Um no bar (11/11)

Pedido do dono:
- **Transformações:** quests no estilo Skyrim que dão um item de transformação. O item muda visual, movimentos, habilidades e atributos, dura um tempo e tem recarga.
- **Máscara verde:** te transforma num Pandemônio, um bicho do caos.
- **Bar da vila:** um jogo de cartas com aposta, em que se ganha ou perde dinheiro. Tem que ser um jogo padrão entre apostadores, com a matemática já conhecida.

Regra da casa: **o servidor decide tudo.** O cliente só manda a intenção: usar a relíquia, apostar X, pedir ou parar.

---

## 1. Formas

### Como funciona
1. A quest entrega uma **relíquia**, um consumível **reutilizável** (não some ao usar) e preso à conta (`soulbound`).
2. Usar a relíquia transforma o jogador:
   - pelo tempo de `duration_ms` (45 a 75 segundos);
   - com a recarga do grupo `"transformacao"` (`cooldown_ms`, de 15 a 20 minutos), que o `ConsumableSystem` já controla como qualquer poção. Uma forma por vez.
3. Durante a forma:
   - os 4 botões de habilidade viram os da forma (`basic_skill` mais 3 em `skills`);
   - os atributos mudam (`modifiers`);
   - vale a passiva da forma;
   - valem as restrições (`restricoes`);
   - o visual troca.
4. A forma acaba quando o tempo termina, quando o jogador morre ou quando entra na cidade (`EstadoForma.Encerrar()`). Aí volta o visual e as habilidades da classe.

### As 5 formas

Simples no começo, elaboradas no fim:

| forma | relíquia | nível | dura / recarga | ponto forte | ponto fraco |
|---|---|---|---|---|---|
| **Lobisomem** | Colar da Presa Lunar | 20 | 75 s / 15 min | +35% de dano, +25% de velocidade, +20% de ataque; cura 4% por abate; à noite, +15% | não usa poções nem armas; −20% de resistência mágica; moradores fogem |
| **Pandemônio** (máscara) | Máscara do Pandemônio | 30 | 45 s / 15 min | +40% de velocidade, +30% de ataque; tornado, marreta e saco de surpresas | para 0,9 s para rir a cada 15 s; não usa poções nem armas |
| **Senhor Vampiro** | Cálice Rubro | 38 | 75 s / 15 min | rouba 12% do dano como vida; mordida que cura; vira névoa (invulnerável no trajeto) | de dia, ao ar livre, perde 1% de vida por segundo e causa −20% de dano; não usa poções |
| **Ceifador** | Foice de Osso Lunar | 44 | 60 s / 20 min | +50% de dano; executa alvos abaixo de 20% de vida (dano ×3); cura 5% por abate | −40% de armadura; **não recebe cura nenhuma** |
| **Anjo Vingador** | Pena de Serafim | 55 | 75 s / 20 min | +25% de vida, +30% de velocidade; cura o grupo; +50% de dano contra mortos-vivos e demônios | não usa armas |

- Números em `Content_sugestao/transformacoes.json`.
- Habilidades em `skills_formas.json`. Usam os mesmos `target_mode` e `effects` do jogo (`cone`, `nova`, `dash`, `ground`, `projectile`, `heal_area`), com o campo `class_id` igual ao id da forma.

### Quests

Estão em `quests_formas.json`, no mesmo formato do jogo. Cada cadeia tem 2 etapas e a recompensa da segunda é a relíquia.

| cadeia | quem dá | etapa 1 | etapa 2 |
|---|---|---|---|
| Lobisomem | Sibila, a Vidente (Porto) | derrotar 10 Lobisomens | coletar 5 Sangues Lunares |
| Vampiro | Sibila | derrotar 8 Vampiros | coletar 6 Cinzas Vampíricas |
| Ceifador | Sibila | derrotar 5 Mortes | coletar 4 Ossos Lunares |
| Anjo | Sibila | derrotar 5 Anjos | derrotar o Demônio (chefe) |
| Máscara (3 etapas, ver abaixo) | Tião, o Bobo (praça) | 1 Gorro do Saci (raro) | 3 Cascas da Árvore da Serpente, depois fabricar a máscara na bancada |

- Os materiais novos estão em `items_materiais_formas.json`.
- A chance de drop de cada um está em `loot_adicoes_formas.json`. Essas linhas são para **acrescentar** nas tabelas `loot_bst_*` que já existem.
- **Conferir antes de ligar:**
  - se os monstros do bestiário aparecem nos mapas citados (os spawns são do `Tools/gerar_regioes.py`);
  - o XP das recompensas, rodando `python3 Tools/simular_progressao.py --ate 61`.

### A Máscara do Pandemônio (lore e missão)
**A lenda.** Existiu uma máscara entalhada na casca de uma árvore velha onde mora uma serpente. Quem a vestia virava o Pandemônio. Ela se perdeu num naufrágio. O bisavô do Tião guardou **o feitiço para fabricar outra**, que pede um item raríssimo e a casca daquela árvore.

**A missão** (`quests_formas.json`, giver `bobo_tiao`):

| etapa | id | o que pede |
|---|---|---|
| 1. A Máscara que Ria | `q_mascara_1` | 1 **Gorro do Saci** (`mat_gorro_saci`): 2% em cada Saci, `loot_bst_saci` |
| 2. A Casca da Árvore Velha | `q_mascara_2` | 3 **Cascas da Árvore da Serpente** (`mat_casca_serpente`), arrancadas da árvore na Mata dos Enforcados |
| 3. O Feitiço do Riso | `q_mascara_3` | fabricar a máscara na bancada |

- **Árvore da etapa 2:** um objeto `veio` junto da árvore. Cada lasca leva 3 s e a árvore só solta uma a cada 10 min (`mapas_adicoes.json`).
- **Receita da etapa 3** (`recipes_formas.json`): Gorro, mais 3 Cascas, mais 2 Madeiras Verdes. Regra opcional: só funciona entre 0h e 1h do relógio do jogo.

**O modelo:**
- placa de casca de tronco, curva, com casca rachada e bordas de madeira viva laranja;
- olhos quadrados e boca em fenda, como na referência do dono;
- por dentro dos buracos, um brilho verde: é o feitiço.

### Código
`Server/Combat/Transformacao.cs` é puro e já foi testado (compila em .NET 8). Tem:
- `FormaDefinition`, que lê o json;
- `EstadoForma`, um por jogador, com `Iniciar`, `Tick`, `Encerrar`, `Restrito(...)` e os multiplicadores (`MultDano`, `MultVidaMax`, `MultArmadura`, `MultMovimento`, `MultIntervaloAtaque`, `MultResistMagica`, `MultCuraRecebida`, `RouboDeVida`, `CuraPorAbate`, `DrenoPorSegundo`, `RindoAgora`);
- `SortearSurpresa(rng)`.

Onde ligar no `GameServer`:
1. **Ao usar um consumível com `transform`:** checar `EstadoForma.PodeTransformar`, chamar `ConsumableSystem.TryUse` (aplica a recarga) e **não** consumir o item quando `reusable` for true. Depois, `Iniciar`.
2. **No tick:** se `Tick(now)` devolver true, voltar ao normal. Aplicar `DrenoPorSegundo` (vampiro de dia).
3. **No dano causado:**
   - multiplicar por `MultDano(...)`;
   - curar `dano × RouboDeVida`;
   - a cada abate, curar `MaxHealth × CuraPorAbate`.
4. **No dano recebido:** armadura × `MultArmadura`.
5. **Na cura recebida:** × `MultCuraRecebida`.
6. **No movimento e no intervalo de ataque:** multiplicadores correspondentes.
7. **Pandemônio:** com `RindoAgora(now)` true, o jogador não age nem anda.
8. **Restrições:**
   - `sem_pocoes`: recusa as poções;
   - `sem_armas`: o ataque básico vira `basic_skill`;
   - `npcs_fogem`: NPC não abre diálogo.

**Campos novos (todos opcionais):**
- no consumível: `transform`, `reusable`, `soulbound`;
- na habilidade (`effects`): `lifesteal_pct`, `invulnerable`, `mark_damage_taken_pct`, `heal_pct`, `random_effect`;
- no frame da entidade (protocolo): `form`, com o visual da forma, para o cliente trocar o modelo.

### Visual das formas no cliente
- **Lobisomem, Vampiro, Ceifador e Anjo:** usar os visuais do bestiário que já existem (`werewolf`, `vampire`, `death`, `angel`) quando o frame vier com `form`. O `VisualFactory` já sabe montar esses.
- **Pandemônio:**
  - o corpo do próprio jogador, com a pele tingida de verde-limão;
  - a máscara `itens/item_mascara_pandemonio.glb` presa no osso da cabeça, com um `BoneAttachment3D` na cabeça e a máscara cerca de 0,1 m à frente do rosto (o modelo vem em tamanho real, 0,22 m);
  - durante a forma, um rastro verde nos pés.
- **Arte própria:** inspirada na ideia do "máscara que libera o caos", sem copiar o personagem do filme. Não tem terno amarelo, chapéu nem rosto de ator.

---

## 2. Vinte-e-Um (Blackjack) no bar do Porto

### Por que esse jogo
É o jogo de aposta mais padrão contra a casa, com matemática conhecida. Com as regras abaixo, jogando a estratégia básica, a casa fica com **cerca de 0,5%**.

**Simulação** (`Tools/simular_vinteum`, 2 milhões de rodadas):
- vantagem da casa: **0,43%**;
- jogador que só imita a banca: **5,7%**, que é o valor dos livros;
- resultado das mãos: 4,4% blackjack, 39,1% vitórias, 8,5% empates, 47,9% derrotas.

### Regras
- 6 baralhos; reembaralha quando 75% das cartas já saíram.
- A banca **para no 17 macio**.
- **O blackjack paga 3 para 2.**
- Com Ás ou 10 aberto, a banca confere se tem blackjack. Se tiver, o jogador perde só a aposta inicial.
- **Dobrar:** em quaisquer 2 cartas, inclusive depois de separar.
- **Separar:**
  - uma vez (duas mãos);
  - Ases separados recebem 1 carta cada;
  - 21 depois de separar conta como 21 comum, não como blackjack.
- Sem seguro e sem desistência, para ficar simples na tela.
- **Aposta:** de 10 a 500 moedas, sempre em número par, para o 3 para 2 dar conta exata.
- **Limite de perda por dia:** 3.000 moedas (`LimitePerdaDiaria`). Depois disso, "o dono do bar não aceita mais apostas suas hoje". Isso protege a economia.
- **Saiu ou desconectou no meio da mão:** a mão fecha como "parar". Não dá para fugir da derrota.

### Código (compila e foi testado)
**`Shared/Cassino/VinteUm.cs`:**
- `Carta` tem o código curto `AE`, `10C`, `QO`, que é o mesmo nome dos PNGs;
- `Sapato` embaralha com Fisher–Yates usando o `Random` do servidor;
- `Pontuacao`;
- `RodadaVinteUm`, com `Comecar`, `Pode`, `CustoExtra`, `Agir` e `BancaVisivel` (a segunda carta da banca vai como `??` até o fim).

**`Server/Cassino/MesaVinteUm.cs`:** a mesa do servidor.
- `Apostar(jogador, aposta, hoje)`;
- `Agir(jogador, acao, hoje)`;
- `Abandonar(jogador, hoje)`.
- Cobra e paga pela `ICarteira`, que o `GameServer` implementa com as moedas do jogador. Assim o débito e o crédito passam pelo mesmo caminho de sempre e ficam salvos.
- Devolve um `EstadoMesa` pronto para virar mensagem.

**Protocolo sugerido** (mensagens novas, campos opcionais):

| direção | mensagem |
|---|---|
| cliente → servidor | `vinteum_apostar { aposta }` |
| cliente → servidor | `vinteum_acao { acao: pedir \| parar \| dobrar \| separar }` |
| servidor → cliente | `vinteum_estado { maos[{cartas, total, aposta, resultado}], banca[], total_banca?, mao_atual, encerrada, acoes[], lucro, moedas }` |

**Cliente:**
- abre a tela da mesa ao apertar F perto do Dado, o Carteador;
- mostra as cartas com `Assets/Art/cartas/<codigo>.png` e `verso.png`;
- a tela tem campo de aposta (+/− de 10) e os botões das ações que vierem em `acoes`.

### Onde fica: 2º andar do casarão, a partir do nível 7
Posições em `mapas_adicoes.json`:
- **Mesa:** prop `mesa_cartas` no `map_casarao_andar2.json`, em x 3,0 e y −2,0.
- **Carteador:** NPC `carteador_dado`, "Dado, o Carteador", papel `cassino`, em x 4,3 e y −2,6.
- **Nível:** o servidor recusa abaixo do nível 7 (`MesaVinteUm.NivelMinimo = 7`; `Apostar(..., nivel)`).

**Minigame sentado:**
1. Ao apertar F no Dado, o servidor escolhe uma das 3 cadeiras livres (`_cadeiras_jogador`, em `mapas_adicoes.json`).
2. O servidor teleporta o jogador para a cadeira e trava o movimento enquanto ele estiver na mesa.
3. O cliente abre a tela do 21 e põe o boneco na **pose sentado**: quadril 0,45 m mais baixo, coxas 85° para a frente, joelhos 85° para trás e antebraços apoiados na mesa. Dá para montar com o mesmo `Pose`/`AimArm` do `MannequinVisual`. Se preferir um clipe gravado, eu faço um `anim_sentar.glb` no formato do `anim_soco`.
4. Ao apertar "Levantar", ou ao andar, chama `MesaVinteUm.Abandonar`: a mão aberta fecha como "parar" e o jogador sai da cadeira.
- A Sibila e o Tião já existem e passam a dar as quests das formas e da máscara.

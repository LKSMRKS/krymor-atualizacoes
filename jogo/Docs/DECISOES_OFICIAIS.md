# Decisões oficiais

Data: 30/10/2026. **Este documento prevalece sobre o GDD original em qualquer conflito.**
O GDD não foi reescrito; apenas os trechos conflitantes receberam notas "[DECISÃO OFICIAL]".

## 1. Direção visual
Os lotes 0–8 são a referência visual oficial: fantasia medieval sombria, armaduras marcantes, materiais gastos, ruínas, pedreiras e brejos.
A ambientação de leito marinho e sal negro deixa de ser obrigatória; pode permanecer em regiões específicas quando fizer sentido.

## 2. Classes
Nomes oficiais: **Guerreiro, Arqueira, Mago, Sacerdotisa**.
Não é troca automática dos nomes Bastião/Rastreiro/Caldeiro/Cantor: habilidades e equipamentos das outras três serão redesenhados para os arquétipos quando chegar a vez delas.
**Marco A: apenas Guerreiro.** Arqueira, Mago e Sacerdotisa ficam para marcos posteriores.

### Adaptação do Guerreiro (a partir do Bastião, GDD 5.1)
Mantidos: papel de defensor corpo a corpo, espada ou martelo de uma mão + escudo, Força/Vigor, recurso Fôlego (100 máx., +8/s em combate, +15/s fora, bloqueio zera a regeneração), níveis de desbloqueio, recargas, custos, coeficientes e passivas.
Alterados: apenas nomes com sabor marítimo/industrial, para combinar com a fantasia medieval. Efeitos permanecem iguais.

| Nível | GDD (Bastião) | Guerreiro | Marco A |
|---:|---|---|---|
| 1 | Golpe de Cunha | Golpe de Cunha | sim |
| 5 | Guarda de Comporta | Guarda de Escudo | sim |
| 10 | Varredura de Corrente | Varredura | não |
| 18 | Chamado de Ferro | Chamado de Ferro | não |
| 28 | Passo de Ancoragem | Investida de Escudo | não |
| 40 | Rebate | Rebate | não |
| 60 | Muralha de Lastro | Muralha de Escudos | não |
| 85 | Ruptura de Quilha | Ruptura | não |

Passivas mantêm os nomes do GDD (revisáveis depois). IDs de dados usam `guerreiro` (ex.: `class_guerreiro`, `skill_guerreiro_golpe_de_cunha`).
Nota: a Guarda de Escudo desbloqueia no nível 5; o Marco A usa um personagem de teste com nível configurável para exercitar as duas habilidades.

## 3. Conteúdo
O conteúdo das artes é proposta de expansão do catálogo, não escopo atual.
Brejo dos Sinos Afogados e Odrava ficam fora do Marco A.
Nomes de regiões e inimigos dos lotes poderão substituir ou ampliar os do GDD em marcos posteriores. IDs estáveis só são criados quando o conteúdo entra em um marco.

## 4. Experiência central (preservada)
Progressão lenta; farm e PvE como foco; equipamentos conquistados com esforço; cooperação; punição por erros.
**PvP é secundário e fica fora do protótipo inicial.**

## 5. Arte
PNGs são referências conceituais. O Marco A usa geometria simples. Não reconstruir personagens detalhados por código; não afirmar que modelos 3D existem.

## 6. Escopo e processo
- Trabalhar somente no Marco A, só com Guerreiro. Nenhum sistema de expansão futura.
- Entregas pequenas. Consultar apenas as imagens necessárias a cada etapa.
- Cada entrega informa: implementado; executado e testado; não testado; próximo comando.
- `Docs/STATUS.md` curto para retomar em outra conversa.
- Não assumir Godot/.NET instalados (ver `Docs/AMBIENTE.md`). O que não puder ser executado é marcado "não testado".

## 7. Mudança de prioridade (07/11/2026, pedido do usuário) — prevalece sobre as seções 2, 5 e 6 onde conflitar
- Prioridade: **protótipo jogável e divertido** com o ciclo de farm completo (classe → explorar → matar → loot → equipar →
  nível → inimigos mais fortes), inspirado no ciclo de MU, câmera em 3ª pessoa no estilo Elden Ring.
- **Mago entra agora** junto com o Guerreiro (criação e seleção de personagem; classe persistida). Arqueira e Sacerdotisa seguem para depois.
- **Visual provisório autorizado:** bonecos 3D articulados feitos de esferas, cápsulas, caixas e cilindros (sem cartazes que
  encaram a câmera); armas e armaduras simples, visíveis ao trocar. A especificação de modelos e as 11 animações
  (`ESPECIFICACAO_MODELOS_3D.md`) ficam **adiadas**, não canceladas.
- Aparência separada das regras: vida, dano, alcance, XP, loot e IDs nunca dependem da geometria provisória
  (componente visual substituível por entidade: `Client/Visuals/IEntityVisual.cs`).
- Combate: inimigos com silhueta, comportamento, preparação visível e brecha para contra-ataque; jogador com ataque,
  esquiva e duas habilidades por classe; regras de mover/cancelar documentadas; servidor continua autoritativo.
- Mapa pequeno e conectado com caminhos alternativos, segredo, atalho desbloqueável e chefe opcional; loot de monstros,
  baús, inimigo raro e recompensa do chefe.
- Preservar sistemas, bancos, contas e progresso; não reescrever o projeto. Detalhes e decisões provisórias: `Docs/PROTOTIPO_E8.md`.

## 8. Direção do mundo de farm (08/11/2026, pedido do usuário) — prevalece sobre a seção 7 onde conflitar
Texto integral do pedido: `Docs/PEDIDO_E9_DIRECAO.txt`. Plano e valores provisórios: `Docs/MUNDO_E9.md`.
- Base: RPG de farm inspirado em MU — mapas conectados persistentes, respawn, XP e equipamento como ciclo principal,
  identidade e faixa de poder por região (sem nivelar monstros pelo jogador), chefes de campo e dungeons.
- Câmera e combate em 3ª pessoa preservados; exploração por curiosidade (caminhos, segredos, baús, marcos), sem
  escalada/planador/sobrevivência. Conteúdo original.
- **Personagem novo** (Guerreiro e Mago): nível 1, nada equipado, de cueca opaca, ataque gratuito fraco (soco; faísca
  do Mago, sem cajado e sem custo). Caminho **garantido** para a primeira arma além dos drops. Equipamento visível no corpo.
  Personagens existentes **não perdem** itens nem níveis.
- **Progressão mais lenta, começo acessível**: curva calculada e simulada (não corte arbitrário); combate básico ágil;
  monstros iniciais fracos de verdade (pouco dano), não esponjas de vida. Degraus: fracos perto da cidade → comuns que
  pedem equipamento → grupos/ataques a evitar → elites e farm perigoso → chefes e dungeons.
- Mecânicas desta fase: criação/seleção, inventário e comparação, vida/recurso/consumíveis, XP/níveis/atributos,
  moeda/compra/venda, NPCs, diário de missões (complementa o farm, não obriga), mapa do mundo e transições, grupo,
  morte/renascimento/persistência. **Sem perda de equipamento ou penalidade permanente na morte** sem decisão do usuário.
- Escopo agora: cidade + região inicial (sub-áreas crescentes) + região seguinte mais perigosa com dungeon funcional,
  conexões preparadas para expandir. Conteúdo por definições (monstros, itens, drops, spawns, lojas, missões, regiões, dungeons).
- Validar o percurso antes de ampliar conteúdo: cueca → fracos → primeira arma → primeiras peças → mais fortes →
  outra região → grupo/preparo → dungeon.

## 9. Direção consolidada (08/11/2026, pedido do usuário) — prevalece sobre a seção 8 onde conflitar
Texto integral: `Docs/PEDIDO_DIRECAO_CONSOLIDADA.txt`. Implementação e valores: `Docs/MUNDO_E9.md`, seção 12.
Escolhas do usuário (08/11): (1) faixas reorganizadas — 1–5 aprendizado, 6–10 arredores + dungeon introdutória, 11–16 Vale,
17–20 no próximo mapa; (2) evolução no nível 20 + missão: Guerreiro → Campeão (Investida, Golpe atordoa), Mago → Arcanista
(Lança Arcana, Explosão deixa chamas); (3) armas com estilo próprio + habilidade (R) em algumas especiais, com recarga visível;
(4) mudanças de missão no mundo **por personagem**; só o cosmético é compartilhado.
Também: checkpoint antes do chefe; recompensa garantida na primeira conclusão; 2ª fase de chefe; combos curtos de elite;
corrida fora de combate; nível de portal só recomendado (exceto marcos como evolução); missões de conversar/interagir;
missões no mapa do mundo. Sem salto, escalada, planador ou natação nesta fase.

## 10. Expansão até o midgame (08/11/2026, pedido do usuário) — prevalece sobre a seção 9 onde conflitar
Fontes: `Docs/PLANO_EARLY_MIDGAME_v1.md` (seis entregas M01–M06) e o pacote visual v1 (aparência). Implementação, valores e
verificação: `Docs/MIDGAME_M01_M06.md`. Conciliações feitas (reversíveis): a evolução do nível 20 continua Campeão/Arcanista
(já implementada) e a do 50 usa os ramos do plano; a Galeria Funda conta como D01; o Vale fica na faixa 11–17 e a Mata (R02)
cobre 17–20; chefes de passagem bloqueiam o caminho (selo por personagem) em vez de nível mínimo; a horta usa tempos curtos
provisórios; nenhum item é perdido e não há morte permanente (a penalidade de morte continua decisão do usuário).

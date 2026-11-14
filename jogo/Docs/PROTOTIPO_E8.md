# E8 — Protótipo jogável: Vale das Cinzas

> **Atualizado pelo E9 (08/11/2026):** o Vale virou a região 2 de um mundo maior; início de cueca, curva de XP sem o ×8,
> moeda, loja, missões, grupo e dungeon. Onde este documento conflitar, vale `Docs/MUNDO_E9.md`.

**Data:** 08/11/2026. **Pedido:** chegar rápido a um RPG pequeno e divertido (ciclo de progressão inspirado em MU,
câmera em 3ª pessoa no estilo Elden Ring), com visual provisório simples. Este documento registra o que foi feito e as
decisões provisórias tomadas sem consulta (todas reversíveis, todas em dados quando possível).

## 1. Como jogar
- Duplo clique em **`Jogar.cmd`** (na raiz). Ele compila só se o código mudou, sobe o servidor minimizado (se não estiver
  aberto), migra o banco com backup e abre o cliente já conectado. Contas: reaproveita **alice** e **bruno** (`e6-tokens.json`).
- Primeira vez: se não achar o Godot sozinho, pergunta o caminho uma vez e guarda em `Tools/jogar.config.json`.
- Opções: `Jogar.cmd -Quem bruno` (2ª janela), `Jogar.cmd -NovaConta carla`, `Jogar.cmd -CriarAtalho` (ícone na Área de
  Trabalho), `Jogar.cmd -Parar` (fecha o servidor salvando).

### Controles
| Tecla | Ação | Regras (decididas no servidor) |
|---|---|---|
| W A S D | andar relativo à câmera | velocidade 5 m/s; reduzida durante golpes (ver §3) |
| mouse | gira a câmera; rodinha = zoom | câmera encosta em paredes e colunas |
| clique esquerdo (segurar = sequência) | ataque básico | Guerreiro: Corte em leque; Mago: Seta de Brasa (projétil) |
| Q / E | habilidades da classe | ver §2 |
| Espaço | esquiva (rolamento) | ver §3 |
| Tab ou botão do meio | travar/soltar alvo | a câmera segue o alvo; os golpes miram nele |
| F | pegar item / abrir baú / puxar alavanca | |
| 1 | poção | |
| I | mochila e equipamento (clique para equipar) | |
| Esc | solta o mouse; botão "Trocar de personagem" | |

## 2. Classes (Guerreiro existente + Mago)
| Classe | Clique | Q | E |
|---|---|---|---|
| **Guerreiro** (Força, Fôlego) | **Corte**: leque de 110°, 2,6 m (+alcance da arma), 180+300 ms × velocidade da arma | **Golpe de Cunha** (existente): alvo à frente, 1,6× e −10% armadura; **quebra a preparação** do inimigo | **Guarda de Escudo** (existente): −40% frontal (escudo soma +20%) |
| **Mago** (Sintonia, Mana) | **Seta de Brasa**: projétil 20 m/s até 16 m (paredes bloqueiam) | **Explosão de Cinzas**: área r 2,8 m no ponto mirado (até 12 m), preparação de 650 ms visível para aliados; quebra a preparação | **Passo Etéreo**: salto de 6 m, 400 ms invulnerável |
- Classe gravada no personagem (`characters.class_id`, que já existia). Personagens antigos continuam Guerreiros.
- Criação no lobby: nome 3–16 letras/números, até 4 personagens por conta. Kit inicial: arma da classe equipada + 3 Tônicos.

## 3. Regras de ação (o que permite mover ou cancelar)
| Fase | Mover | Cancelar |
|---|---|---|
| Preparação (windup) de golpe/feitiço | Corte/Seta 35%; Golpe de Cunha/Explosão **parado** | **não cancela**; direção travada |
| Recuperação | Corte 60%, Seta 70%, Golpe 30%, Explosão 40% | **a esquiva cancela**; outros golpes esperam |
| Esquiva | 4,2 m em 380 ms | invulnerável nos 300 ms iniciais; recarga 850 ms; cancela a Guarda |
| Levar golpe | normal (sem atordoar) | 250 ms sem levar outro golpe (evita sequência sem reação) |
- Entrada com **buffer** de 450 ms: apertar durante a recuperação dispara assim que fica livre.

## 4. Inimigos (silhueta, comportamento, preparação visível, brecha)
| Inimigo | Silhueta | Comportamento | Preparação (marca vermelha no chão + brilho) | Brecha (dano ×1,3–1,5, "brecha!") |
|---|---|---|---|---|
| **Caranguejo de Cal** (definição existente) | baixo e largo, 6 patas, pinças | corre e dá **bote** em linha | empina e abre as pinças, 600 ms, faixa no chão | tomba para a frente 950 ms |
| **Saqueador da Vigília** | humanoide preto curvado, cutelo | **talho** em leque 110°; **estocada** quando você foge (2,8–4,2 m) | cutelo erguido 750 ms / recua agachado 650 ms | cutelo cravado no chão 800–1000 ms |
| **Acólito das Cinzas** | túnica e capuz, mãos em brasa | mantém 8 m, **recua** se você chega a 4,5 m; **bola de cinza** (projétil desviável); **repulsão** em volta se encurralado | orbe crescendo nas mãos 900 ms / braços abertos 700 ms | braços caídos 1100 ms |
| **Capataz da Pedreira** (chefe opcional) | 2× maior, ombreiras, marreta | **pancada** em área à frente (r 3,2 m); **varrida** em leque 170° | marreta acima da cabeça 1400 ms, **círculo que se fecha** | marreta presa no chão 1500 ms; **não** é interrompido |
| **Caranguejo Dourado** (raro) | caranguejo dourado com luz | 45% de chance a cada 75 s em um de 4 pontos; **foge** | — | loot garantido (arma rara) |
- Grupos: quem é atacado chama os do mesmo grupo num raio de 14 m. Coleira: voltam ao ponto (curando) se você fugir.
- Zona segura do acampamento: monstros não entram; regeneração rápida fora de combate.
- O validador recusa ataque com preparação < 300 ms ou recuperação < 300 ms.

## 5. Mapa: Vale das Cinzas (`Content/map_vale.json`, ~80 × 80 m)
- **Acampamento da Fogueira** (sul, seguro) → duas trilhas: **Trilha da Pedreira** (oeste, fracos nv 3–4) e **Ruínas do
  Posto** (leste, fracos/médios nv 4–5). Ambas sobem até o **Pátio das Colunas** (centro, fortes nv 7–8) pelo norte.
- **Atalho**: portão entre o acampamento e o Pátio, aberto pela **alavanca do lado do Pátio** (só depois de atravessar).
  Marca por personagem (`character_flags`).
- **Chefe opcional**: **Fosso do Capataz**, ao norte do Pátio, por uma passagem estreita.
- **Segredo**: trecho da muralha oeste é **ilusório** (rachaduras roxas e crânios) → **Nicho Oculto** com baú.
- **Baús** (cada personagem abre 1 vez; vai direto para a mochila): Trilha (Gibão garantido), Ruínas (escudo/manto),
  Pátio (machado/cajado de brasa), Nicho (lança/vara gêmea + armadura).
- Encontros combinados (não cópias paradas): par de caranguejos; acólito atrás de pedras com caranguejo de guarda;
  trio de caranguejos no baú; saqueador sozinho (aprender a brecha); saqueador + caranguejo; dois saqueadores + acólito
  atrás das ruínas; pátio com guardas no portão, acólito entre colunas e dupla final.

## 6. Recompensas e equipamentos (`Content/items.json`, `Content/loot.json`)
| Item | Quem | Diferença perceptível |
|---|---|---|
| Espada de Chapa (existente) | G | poder 12, normal, aceita escudo |
| Machado de Pedreira | G | **2 mãos**, poder 30, **30% mais lento**, +0,3 m |
| Lança de Vigia | G | poder 18, **+1,3 m de alcance**, aceita escudo |
| Malho do Capataz (chefe) | G | 2 mãos, poder 34, +0,5 m |
| Escudo de Tábuas | G | armadura +60, **Guarda 60%** |
| Cajado de Aprendiz | M | poder 10 |
| Cajado de Brasa | M | poder 18, **setas 50% mais rápidas** |
| Vara Gêmea | M | poder 13, **duas setas por disparo**, 60% mais lento |
| Cetro do Capataz (chefe) | M | poder 28, **explosão +1,2 m** |
| Gibão Acolchoado | G/M | armadura +35, vida +40 |
| Cota de Malha | G | armadura +110, **8% mais lento** |
| Manto do Acólito | M | armadura +20, vida +20, **mana 60% mais rápida** |
- **Loot por morte decidido pelo servidor** (tabela por monstro; "combina com a classe" de quem lutou). Cada queda tem
  origem única → reprocessar não duplica; coleta continua exclusiva e idempotente (mesma transação da E3).
- Itens no chão duram 3 min (depois somem do banco). Tônicos somam na pilha.
- A primeira melhoria vem cedo: baú da Trilha (Gibão garantido) e drops de 8–30% nos primeiros inimigos.
- **XP do mapa ×8** (`xp_multiplier`) e por monstro (`xp_mult`): ~9 fracos para o 1º nível; chefe ≈ 1,5 nível.
  As fórmulas do GDD não mudaram; o multiplicador é do protótipo.

## 7. Separação regra × aparência
- Regras (vida, dano, alcance, XP, loot, IDs) ficam em `Content/*.json` e no servidor. O cliente recebe o mapa
  (`MapMessage`) e o catálogo (`CatalogMessage`) do servidor.
- Aparência: `Client/Visuals/`. `IEntityVisual` é o componente substituível por entidade; `VisualFactory` é o único
  lugar que escolhe a forma (classe → humanoide; definição de monstro → humanoide/caranguejo; `model_asset_id` do item →
  `GearFactory`). Para usar modelos `.glb` depois: novo `IEntityVisual` que carregue o arquivo e toque as animações
  pelas mesmas ações do servidor (`windup:*`, `active:*`, `recover:*`, `dodge`). Nada de combate/rede muda.

## 8. Decisões provisórias (tomadas sem consulta; reversíveis)
1. Mago entra agora (antes era Marco posterior). Arqueira e Sacerdotisa continuam fora.
2. Servidor mantém o **modo arena** do Marco A (sem mapa) para os testes antigos; o `ServerHost` sobe o **mapa** por padrão (`--arena` volta).
3. Ajustes do protótipo em dados: `health_mult`, `damage_mult`, `xp_mult` por monstro e `xp_multiplier` do mapa (só no modo mapa).
4. Snapshot continua a cada 100 ms; o cliente suaviza (sem previsão local). Em rede local isso basta; se sentir atraso, previsão é o próximo passo.
5. Morte: renasce no acampamento em 5 s, sem perda (a penalidade do GDD 8 continua pendente).
6. Itens no chão: livres para qualquer jogador (cooperação); baús e alavanca são por personagem.
7. Mago usa o atributo Sintonia (`attunement`, já previsto no validador) e o recurso Mana (100).
8. Nomes de exibição dos itens/monstros novos são provisórios (IDs estáveis).

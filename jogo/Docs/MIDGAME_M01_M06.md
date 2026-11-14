# Expansão até o midgame (níveis 17–60) — entregas M01–M06

**Data:** 08/11/2026 (sessão noturna). **Fonte do pedido:** `Docs/PLANO_EARLY_MIDGAME_v1.md` + pacote visual v1 (referência de aparência).
**Gerador do conteúdo:** `python3 Tools/gerar_regioes.py` (tabelas no próprio arquivo; IDs estáveis; mapas feitos à mão só
ganham peças marcadas `"gerado": true`). Arquivos gerados: `*_midgame.json`, `map_<região>.json`, `recipes.json`, `crops.json`.

## 1. Mundo (8 regiões, 8 dungeons)
| Faixa | Região (campo) | Dungeon (por grupo) | Passagem para a próxima (chefe/rito que libera) |
|---|---|---|---|
| 1–10 | Costa do Cal (+ Porto) | Galeria Funda (7+) | — |
| 11–17 | Vale das Cinzas | — | portal para a Mata (17+) |
| 17–20 | **Mata dos Enforcados** (Vila do Moinho) | Cripta do Sino (18+) | Guardião Retorcido → `selo:mata` |
| 20–30 | Cânions dos Suplicantes | Galerias da Piedade (23+) | Carcereiro das Almas → `selo:elevador` |
| 28–36 | Brejo da Viúva | Mosteiro Afundado (30+) | Rito de Véspera (missão) → `rito:vespera` |
| 34–44 | Véspera, a Cidade do Outro Lado | Mercado dos Mortos (37+) | Passo da Geada (missão) → `passo:geada` |
| 42–50 | Coroa da Geada | Salão da Geada (45+) | Rei Draugr → `selo:geada` |
| 48–56 | Mar de Escória | Fornalha (51+) | Colosso de Escória → `selo:escoria` |
| 55–60 | Acrópole das Cinzas | Labirinto do Juramento (57+, chave) | — (fim do midgame) |
| — | Casa do jogador (instância pessoal, a partir do Porto) | — | comprar a casa (missão) |

Selo de passagem: barreira violeta com runas; some só para quem tem a marca (todos os participantes da luta ganham).
Montaria proibida em dungeon e na casa. Mapa do mundo com 10 regiões.

## 2. Sistemas novos (servidor autoritativo)
- **Ofícios:** forja, bancada, caldeirão, panela (objetos do mapa). Receita conhecida (padrão ou `receita:*` por missão);
  resultado exato, sem sorte; pedido repetido com o mesmo id não duplica. Painel de ofício no cliente.
- **Mineração e coleta:** veios e moitas (`veio`) por personagem com recarga; picareta (`tool:picareta`, missão); 1,5 s de
  coleta, cancela com dano ou movimento.
- **Horta:** canteiros comunitários (Porto, Mata) e da casa: plantar → regar → crescer → colher, no relógio do servidor
  (tempos provisórios curtos, 3–15 min). Planta aparece no canteiro (broto → média → pronta com flor).
- **Armazém comunitário** (12 espaços, pessoal) e **baú da casa** (32, 48 com melhoria). Painel de depósito.
- **Refeição:** um efeito ativo por vez (tecla 2 para guerreiro; mago usa 2 para frasco de mana).
- **Mana do mago:** frascos de mana dropam e são vendidos.
- **Montaria** (V; ×1,9 de velocidade fora de combate; cai ao atacar/levar golpe), **asas** (G; impulso de 6 m, recarga 8 s),
  **companheiro** (B alterna; pequeno draco / fênix; ataca o alvo do dono; 8–12% do dano; cai por um tempo se apanhar).
- **Lendários** (qualidade `lendario`): efeito a cada N golpes; brilho no corpo. 9 lendários entre 38 e 58.
- **Evolução:** 20 (Campeão/Arcanista, já existente) e 50 (Juramento: Vanguarda/Quebra-Selos; Piromante/Tecelão Espectral;
  escolha única).
- **Casa:** compra por missão (`casa:propria`), baú, 4 canteiros, bancada, caldeirão; instância só sua.

## 3. Missões
104 missões novas (28 de história, 13 de região, 19 de ofício, 10 opcionais, 4 de companheiro, 2 da casa, 28 contratos
repetíveis). Tipos novos: minerar, fabricar, plantar, colher (contam só ações feitas depois de aceitar). Cadeias por
`requires`, `requires_flag`/`forbid_flag`, itens/marcas dados ao aceitar, custo em moedas. NPC sempre tem a próxima.

## 4. Ritmo (simulação `Tools/simular_progressao.py --ate 61`, guerreiro, só abates, sem XP de missão)
| Faixa | Plano | Simulado | Ajuste |
|---|---|---|---|
| 1–10 | 1,5–3 h | ≈ 3,4 h | sem mudança (missões dão XP extra que a simulação não conta) |
| 11–20 | 4–7 h | ≈ 6,4 h | Vale ×1,5; Mata ×2,2 |
| 21–35 | 10–16 h | ≈ 13 h | Cânions ×2,5; Brejo ×2,7 |
| 36–50 | 16–25 h | ≈ 21,7 h | Véspera ×3,1; Geada ×3,3 |
| 51–60 | 15–25 h | ≈ 18,5 h | Escória ×3,5; Acrópole ×3,6 |
Dungeons: ×1,15 da região. Os multiplicadores estão em `Tools/gerar_regioes.py` (campo `xp` de cada região) — **provisórios**.
Sem equipamento da faixa o jogador morre rápido (verificado: guerreiro nível 22 com equipamento inicial na Mata).

## 5. Visual (pacote v1)
Corpo-base comum (articulações visíveis, membros finos), equipamento aparece no corpo, personagem novo de cueca.
Kit modular: forja, bancada, caldeirão, panela, armazém, canteiro, árvore seca, lápide, nicho de almas, estacas, cristal de
gelo, rocha de lava, coluna quebrada, estátua de bronze, portão da cidade, pinheiros. Veios com brilho do minério; moitas.
Monstros novos: inseto, voador (morcego, harpia, fênix, draco), larva, caranguejo com espinhos, humanoides variados.
HUD: potes de vidro (vida; mana/fôlego), XP abaixo da barra de poderes, bússola, retrato, registro.
Sons: ~41 efeitos gerados por código (CC0, `Tools/gerar_sons.py`) em toda ação.

## 6. Verificação
- **Implementado e compilado:** servidor, cliente (0 erros/0 avisos), conteúdo validado no carregamento.
- **Testado automaticamente:** 261 testes (13 do midgame + 12 da direção consolidada + os anteriores), incluindo
  “todo mapa cabe num quadro de rede com folga”.
- **Executado no jogo (cliente real, tela virtual):** Mata (vila, canteiros, panela/caldeirão, moitas), montaria, draco,
  selo de passagem, painel da forja, painel do armazém. Capturas `20_*`–`25_*`.
- **Corrigido ao executar:** o mapa da Mata passava de 64 KB e derrubava a conexão → limite de quadro agora 256 KB (+ teste);
  aviso “mouse livre” cobria os painéis; mensagens “pet: ok.”/“Baú aberto.” indevidas; plurais (“Raizs”).
- **Não avaliado por pessoa:** sensação de combate 17–60, dificuldade dos chefes de passagem, tempos da horta, ritmo real.
- **Pendências que continuam:** E6 cenário B (disputa real de item com 2 clientes); penalidade de morte (decisão sua).

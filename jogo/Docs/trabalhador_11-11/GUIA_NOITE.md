# GUIA DA NOITE — Cinzas da Maré (11/11/2026)

Pacote novo. Nada aqui substitui arquivo do programador: são **arquivos novos**. Faça backup antes de copiar por cima de qualquer coisa.

Copie a pasta `Client/` deste zip por cima da `Client/` do jogo (só adiciona arquivos).

## 1. Arsenal com raridades (59 armas)

12 formas × 4 qualidades: **Enferrujada (nv 1–5) → Comum (5–15) → Refinada (15–30) → Excelente (30+)**, mais graveto, aljavas, varinhas e talismãs da bruxa.
- Modelos: `Client/Assets/Models/armas/<asset_id>.glb` — **a origem é a empunhadura** (centro da mão), lâmina/cabo para +Y no Godot, igual às armas do lote 2. Arcos: corda/empunhadura igual ao `MannequinVisual.Bow`.
- Ícones: `Client/Assets/Art/icones/armas/ico_<id>.png` (448px) e `_256.png`.
- Excelente tem gema e runa com brilho (emissivo) — deixe o glow do WorldEnvironment ligado.

Trecho para o `GearFactory.LotTwoWeapon` (o model_asset_id já é o nome do arquivo):

```csharp
// arsenal da noite: qualquer model_asset_id que comece com "arma_" e exista em armas/
if (model.StartsWith("arma_") && ResourceLoader.Exists($"res://Assets/Models/armas/{model}.glb"))
    return $"armas/{model}";
```

| asset_id | nome | tipo | empunhadura | nível | tris |
|---|---|---|---|---|---|
| `arma_faca_enferrujada` | Faquinha Enferrujada | faca | uma mão | 1–5 | 3500 |
| `arma_faca_comum` | Faquinha Comum | faca | uma mão | 5–15 | 3500 |
| `arma_faca_refinada` | Faquinha Refinada | faca | uma mão | 15–30 | 3500 |
| `arma_faca_excelente` | Faquinha Excelente | faca | uma mão | 30+ | 3670 |
| `arma_adaga_enferrujada` | Adaga Enferrujada | adaga | uma mão | 1–5 | 3498 |
| `arma_adaga_comum` | Adaga Comum | adaga | uma mão | 5–15 | 3498 |
| `arma_adaga_refinada` | Adaga Refinada | adaga | uma mão | 15–30 | 3498 |
| `arma_adaga_excelente` | Adaga Excelente | adaga | uma mão | 30+ | 3746 |
| `arma_espada_curta_enferrujada` | Espada Curta Enferrujada | espada | uma mão | 1–5 | 3498 |
| `arma_espada_curta_comum` | Espada Curta Comum | espada | uma mão | 5–15 | 3498 |
| `arma_espada_curta_refinada` | Espada Curta Refinada | espada | uma mão | 15–30 | 3498 |
| `arma_espada_curta_excelente` | Espada Curta Excelente | espada | uma mão | 30+ | 3746 |
| `arma_espada_longa_enferrujada` | Espada Longa Enferrujada | espada | duas mãos | 1–5 | 3498 |
| `arma_espada_longa_comum` | Espada Longa Comum | espada | duas mãos | 5–15 | 3498 |
| `arma_espada_longa_refinada` | Espada Longa Refinada | espada | duas mãos | 15–30 | 3498 |
| `arma_espada_longa_excelente` | Espada Longa Excelente | espada | duas mãos | 30+ | 3746 |
| `arma_katana_enferrujada` | Katana Enferrujada | katana | duas mãos | 1–5 | 3499 |
| `arma_katana_comum` | Katana Comum | katana | duas mãos | 5–15 | 3499 |
| `arma_katana_refinada` | Katana Refinada | katana | duas mãos | 15–30 | 3499 |
| `arma_katana_excelente` | Katana Excelente | katana | duas mãos | 30+ | 3591 |
| `arma_clava_enferrujada` | Clava Enferrujada | clava | uma mão | 1–5 | 3500 |
| `arma_clava_comum` | Clava Comum | clava | uma mão | 5–15 | 3500 |
| `arma_clava_refinada` | Clava Refinada | clava | uma mão | 15–30 | 3500 |
| `arma_clava_excelente` | Clava Excelente | clava | uma mão | 30+ | 3500 |
| `arma_maca_enferrujada` | Maça de Aletas Enferrujada | maca | uma mão | 1–5 | 3500 |
| `arma_maca_comum` | Maça de Aletas Comum | maca | uma mão | 5–15 | 3500 |
| `arma_maca_refinada` | Maça de Aletas Refinada | maca | uma mão | 15–30 | 3498 |
| `arma_maca_excelente` | Maça de Aletas Excelente | maca | uma mão | 30+ | 3576 |
| `arma_machado_mao_enferrujada` | Machadinha Enferrujada | machado | uma mão | 1–5 | 3500 |
| `arma_machado_mao_comum` | Machadinha Comum | machado | uma mão | 5–15 | 3498 |
| `arma_machado_mao_refinada` | Machadinha Refinada | machado | uma mão | 15–30 | 3498 |
| `arma_machado_mao_excelente` | Machadinha Excelente | machado | uma mão | 30+ | 3554 |
| `arma_machado_guerra_enferrujada` | Machado de Guerra Enferrujada | machado | duas mãos | 1–5 | 3500 |
| `arma_machado_guerra_comum` | Machado de Guerra Comum | machado | duas mãos | 5–15 | 3500 |
| `arma_machado_guerra_refinada` | Machado de Guerra Refinada | machado | duas mãos | 15–30 | 3500 |
| `arma_machado_guerra_excelente` | Machado de Guerra Excelente | machado | duas mãos | 30+ | 3578 |
| `arma_lanca_enferrujada` | Lança Enferrujada | lanca | duas mãos | 1–5 | 3498 |
| `arma_lanca_comum` | Lança Comum | lanca | duas mãos | 5–15 | 3498 |
| `arma_lanca_refinada` | Lança Refinada | lanca | duas mãos | 15–30 | 3498 |
| `arma_lanca_excelente` | Lança Excelente | lanca | duas mãos | 30+ | 3590 |
| `arma_arco_curto_enferrujada` | Arco Curto Enferrujada | arco | mão esquerda | 1–5 | 3500 |
| `arma_arco_curto_comum` | Arco Curto Comum | arco | mão esquerda | 5–15 | 3500 |
| `arma_arco_curto_refinada` | Arco Curto Refinada | arco | mão esquerda | 15–30 | 3500 |
| `arma_arco_curto_excelente` | Arco Curto Excelente | arco | mão esquerda | 30+ | 3650 |
| `arma_arco_longo_enferrujada` | Arco Longo Enferrujada | arco | mão esquerda | 1–5 | 3500 |
| `arma_arco_longo_comum` | Arco Longo Comum | arco | mão esquerda | 5–15 | 3500 |
| `arma_arco_longo_refinada` | Arco Longo Refinada | arco | mão esquerda | 15–30 | 3500 |
| `arma_arco_longo_excelente` | Arco Longo Excelente | arco | mão esquerda | 30+ | 3650 |
| `arma_graveto` | Graveto | clava | uma mão | 1–5 | 1016 |
| `arma_graveto_pregos` | Graveto com Pregos | clava | uma mão | 5–15 | 3500 |
| `arma_aljava_comum` | Aljava Comum | aljava | costas | 5–15 | 836 |
| `arma_aljava_refinada` | Aljava Refinada | aljava | costas | 15–30 | 920 |
| `arma_aljava_excelente` | Aljava Excelente | aljava | costas | 30+ | 1082 |
| `arma_varinha_bruxa_n1` | Varinha de Galho | varinha | uma mão (Bruxa) | 1–10 | 3522 |
| `arma_talisma_bruxa_n1` | Talismã de Dente | talisma | mão esquerda (Bruxa) | 1–10 | 3500 |
| `arma_varinha_bruxa_n2` | Varinha de Osso Entalhado | varinha | uma mão (Bruxa) | 10–25 | 352 |
| `arma_talisma_bruxa_n2` | Talismã da Lua Prateada | talisma | mão esquerda (Bruxa) | 10–25 | 3596 |
| `arma_varinha_bruxa_n3` | Varinha do Cristal Flutuante | varinha | uma mão (Bruxa) | 25+ | 3582 |
| `arma_talisma_bruxa_n3` | Talismã do Olho Carmesim | talisma | mão esquerda (Bruxa) | 25+ | 3576 |

**Sugestão de drops:** Enferrujada → praia/porto (goblins, caranguejos); Comum → estrada e mina; Refinada → caverna de cal (carniçal, fantasma); Excelente → chefes (Golem de Cal Viva, Caranguejo-Geodo nv 5 com chance baixa de Comum).

**Regra de empunhadura (do dono):** mão certa, arco na mão esquerda, escudo no antebraço, nada atravessando o corpo. Os modelos já vêm com a origem na pegada; **não testei no boneco animado dentro do jogo** — confira no Walking/Attack e ajuste só o offset do BoneAttachment se precisar.

## 2. Móveis e peças de puzzle

`Client/Assets/Models/interior/`. Cada um tem `_info.json` (em `dados/`) com caixa de colisão, luz sugerida e, nas peças de puzzle, `posicao_no_conjunto_godot` para montar as partes móveis.

| asset_id | nome | medidas (m) | luz |
|---|---|---|---|
| `movel_balcao` | Balcão de taverna (canecas e barril com torneira) | [2.9, 0.76, 1.59] | — |
| `movel_banqueta` | Banqueta de três pés | [0.401, 0.389, 0.528] | — |
| `movel_bau` | Baú de madeira com cintas de ferro | [0.9, 0.604, 0.747] | — |
| `movel_cadeira` | Cadeira de madeira | [0.46, 0.44, 0.995] | — |
| `movel_caixas` | Pilha de caixas de mercadoria com sacos | [1.849, 1.104, 1.22] | — |
| `movel_cama` | Cama de madeira com colchão de palha e cobertor | [1.1, 2.09, 1.0] | — |
| `movel_lareira` | Lareira de pedra com fogo | [2.0, 0.85, 2.594] | OmniLight #ff8a3a alcance 6 em [0, 0.5, 0.2] |
| `movel_mesa` | Mesa de taverna (com canecas, prato e vela) | [1.6, 0.9, 0.937] | OmniLight #ffb347 alcance 2.5 em [0.1, 0.92, -0.25] |
| `movel_prateleira` | Estante com livros, garrafas e potes | [1.47, 0.36, 1.9] | — |
| `movel_tapete` | Tapete tecido com losango e franjas | [2.602, 1.6, 0.022] | — |
| `puzzle_alavanca_base` | Alavanca: base de pedra | [0.5, 0.4, 0.59] | — |
| `puzzle_alavanca_cabo` | Alavanca: cabo (gira em X, de -35° a +35°) | [0.09, 0.09, 0.805] | — |
| `puzzle_alavanca` | Alavanca (montada, para prévia) | [0.5, 0.4, 1.355] | — |
| `puzzle_placa_base` | Placa de pressão: moldura | [1.62, 1.62, 0.135] | — |
| `puzzle_placa` | Placa de pressão (montada) | [1.2, 1.2, 0.152] | — |
| `puzzle_placa_tampa` | Placa de pressão: tampa (desce 6 cm quando pisada; runas acendem) | [0.84, 0.84, 0.092] | — |
| `puzzle_runa_brilho` | Runa que acende (traços emissivos) | [0.422, 0.036, 0.918] | OmniLight #7affd8 alcance 3 em [0, 1.0, 0.5] |
| `puzzle_runa` | Pilar de runa (montado, aceso) | [0.842, 0.73, 1.798] | OmniLight #7affd8 alcance 3 em [0, 1.0, 0.5] |
| `puzzle_runa_pedra` | Pilar de runa (pedra) | [0.843, 0.73, 1.8] | — |

Exemplo (dentro da casa / taverna):
```csharp
MapKit.Glb(casa, "interior/movel_mesa", P(2, 3), 0, 1f);
MapKit.Glb(casa, "interior/movel_cadeira", P(2, 2.2f), 180, 1f);
MapKit.Glb(casa, "interior/movel_lareira", P(-3, 0), 90, 1f);   // + OmniLight3D da info.luz
```

Puzzle: `puzzle_alavanca` = `_base` fixa + `_cabo` (gira em X ±40°); `puzzle_placa` = `_base` + `_tampa` (desce 3 cm ao pisar); `puzzle_runa` = `_pedra` + `_brilho` (liga/desliga o emissivo). Coloque cada parte na `posicao_no_conjunto_godot` do info. O servidor decide o estado (aberto/fechado); o cliente só anima.

## 3. Mortos de cal (corpo do manequim, como pedido)

- `humanoides/mo_carnical_cal.glb` — **Carniçal Cáustico**: manequim afinado, pele de gesso rachado, crânio, garras de calcário, calça de mineiro rasgada e capacete com lanterna. 1,75 m.
- `humanoides/mo_fantasma_cal.glb` — **Fantasma de Calcário**: manequim em poeira translúcida, olhos pretos. No Godot deixe o material com Transparency = Alpha e cull desligado; fica melhor com um GPUParticles3D de poeira leve saindo do corpo.
Mesmo esqueleto Mixamo do manequim → usa as animações que já existem.

```csharp
// VisualFactory
"carnical_cal"  => MannequinVisual.MonsterBody("humanoides/mo_carnical_cal", 1.75f),
"fantasma_cal"  => MannequinVisual.MonsterBody("humanoides/mo_fantasma_cal", 1.8f),
```

**Posicionamento sugerido (do seu pedido):** fantasmas e carniçais de cal espalhados na trilha/caverna de cal, 1 a cada ~25–30 m, em grupos de 1–2; nunca na zona inicial. Editar via `Tools/gerar_regioes.py` (não à mão).

## 4. Contorno do NPC (branco / amarelo / verde)

Arquivos: `Client/Assets/Shaders/contorno_npc.gdshader` e `Client/Visuals/NpcContorno.cs`.

```csharp
// quando o nome do NPC aparece / mouse em cima:
NpcContorno.Mostrar(npcNode, temEntrega ? NpcContorno.Estado.Entregar : temMissao ? NpcContorno.Estado.Missao : NpcContorno.Estado.Nada);
// ao sair:
NpcContorno.Esconder(npcNode);
```
O estado de missão vem do servidor (o cliente só pinta).

## 5. Sons (42 arquivos .ogg sintetizados, livres de direitos)

`Client/Assets/Audio/` — passos (areia/pedra/madeira ×4), golpes e impactos, arco/flecha, magias, interface, ambientes em loop (mar, vento, fogueira, caverna), gaivota e música calma do porto (64 s, loop). Volumes já normalizados (~−22 dB médio).
```csharp
var s = new AudioStreamPlayer3D { Stream = GD.Load<AudioStream>("res://Assets/Audio/golpes/espada_corte.ogg") };
AddChild(s); s.Play();
// loops: na importação marque Loop = on nos arquivos *_loop.ogg
```
Passo: sortear 1–4 do material do chão a cada pisada da animação.

## Status
- **Implementado:** arsenal, móveis/puzzle, carniçal e fantasma de cal, contorno de NPC, sons.
- **Executado:** todos os modelos gerados e renderizados (prévias em `previas/`).
- **Não testado:** dentro do Godot (offsets de mão das armas, importação do fantasma translúcido, luzes dos móveis).
- **Próximo comando:** copiar `Client/` por cima, abrir o Godot para importar, `dotnet build`, e testar uma arma de cada tipo no boneco andando.

**Ainda falta (próximas rodadas):** mão-garra de trás do dragão da barra; Rastejante de Gesso, Verme-Broca, Gargulito, Golem de Cal Viva, Gárgula do muro do portão, Espantalho; escada/chão da casa; roupas próprias dos moradores.

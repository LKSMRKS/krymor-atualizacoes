# Cinzas da Maré: atualização de arte de 10/11 (tudo desde o Cal Lote 2)

Este zip **substitui** o "Estudos" mandado mais cedo; use só ele. Copie a pasta `Client/` por cima do `Client/` do jogo e a pasta `backups/` para `backups/` do projeto.

## Status
- **Implementado:**
  - HUD v2;
  - mapa ilustrado;
  - cenário dos estudos;
  - 15 itens refeitos (modelo + ícone);
  - ícones das 12 armas;
  - 4 cabeças de bicho novas;
  - goblins no corpo do personagem principal;
  - cena do print da Costa em código;
  - caranguejos PBR.
- **Executado:** as prévias estão em `previas/`.
- **Não testado:** dentro do Godot (sem Godot/.NET aqui).
- **Nenhum `.cs` existente foi editado.** Só há arquivos novos e a troca de `OrbeHud.cs`.
- **Próximo comando:** abrir o editor do Godot (para importar), `dotnet build` e rodar.

## 1. Já copiado direto na pasta do jogo ("CinzasDaMare - Copia")
Também vem no zip, caso o programador use outra cópia.
- **Caranguejos PBR:** `Models/cr_caranguejinho`, `cr_caranguejo_de_cal`, `cr_caranguejo_de_pedra`, `cr_caranguejo_ermitao_barril`, `cr_caranguejo_escavador`, `cr_caranguejo_pinca_de_ferro`.
  - Mesmos nomes e mesmos clipes dos antigos.
  - Antigos em `backups/cr_antes_pbr_2026-11-10/`.
  - O `cr_tirano_da_mare` continua o antigo (pendente).
- **Cabeças novas:** `Models/cabeca_leao`, `cabeca_corvo`, `cabeca_cobra`, `cabeca_gata`.
  - Recortadas dos modelos Meshy enviados pelo dono, ~9 mil triângulos cada.
  - Antigas em `backups/cabecas_antes_meshy_2026-11-10/`.

## 2. Cabeças de bicho nos personagens da criação
- **Formato do `.glb`:**
  - origem no osso **Head** do manequim; Y para cima, frente +Z;
  - já na proporção do corpo do manequim (1,674 m);
  - vai direto no socket `Head`, sem deslocamento nem escala.
- **Código novo:** `Client/Visuals/CabecaAnimalGlb.cs`.
- **Integração** em `MannequinVisual.SetEvolved`, no lugar do `switch` que chama `Lion`/`Crow`/`Snake`/`Cat`:
```csharp
_animal = CabecaAnimalGlb.Anexar(_headSocket, _classId);
if (_animal is null)
{
    _animal = new Node3D { Name = "CabecaAnimal", Position = new Vector3(0, 0.1f, 0), Scale = Vector3.One * 1.15f };
    _headSocket.AddChild(_animal);
    switch (_classId) { case "guerreiro": Lion(_animal); break; case "mago": Crow(_animal); break; case "arqueiro": Snake(_animal); break; default: Cat(_animal); break; }
}
```
- **Mapeamento:** Guerreiro = leão, Mago = corvo, Arqueiro = cobra, Bruxa = gata.
- **Na criação:** para os personagens padrão já aparecerem com a cabeça do bicho, chamar `SetEvolved(true)` ao montar o boneco da tela de criação e do jogo.
- **Ajuste fino**, se precisar subir ou descer a cabeça: `CabecaAnimalGlb.Ajuste = new Vector3(0, 0.01f, 0)`.
- Prévia do encaixe: `previas/cabecas_encaixe.png`.

## 3. Goblins no corpo do personagem principal (`Models/humanoides/`): VÊM NO COMPLEMENTO (zip separado, ainda assando)
`mo_goblin_saqueador`, `mo_goblin_arqueiro`, `mo_goblin_chefe` (mesmos nomes, substituem os do Lote 3).
- **Corpo:** o do manequim do personagem principal, escalado para a altura de cada goblin. O chefe tem corpo reforçado.
- **Do goblin antigo:** só a cabeça (rosto, olhos, nariz, orelhas, bandana/capuz/elmo) e as armas.
- **Roupa refeita sobre o corpo novo:**
  - todos: túnica, tiras de pano no cinto, cinto com fivela;
  - saqueador: saco nas costas;
  - arqueiro: capa e braçadeiras;
  - chefe: ombreiras com cravos, pele no pescoço e braçadeiras de ferro.
- **Esqueleto e clipes:** mesmo esqueleto do manequim e mesmos 6 clipes: Idle, Walking, Running, Sentado, Mexendo, Conversando.
- O `mo_esqueleto_goblin` não mudou.
- O código não precisa mudar.
- Antigos: `backups/goblins_v1/`.

## 4. Itens refeitos (modelo e ícone)
- **Ícones:** `Art/ico_<item>.png` (128 px) e `Art/ico_<item>_256.png` (256 px), com fundo transparente, contorno e brilho nos mágicos.
- **Lista:** `bolsa`, `carapaca`, `erva`, `essencia`, `joia_brasa`, `joia_mare`, `joia_vazio`, `minerio`, `moedas`, `osso`, `pinca_caranguejo`, `pocao_mana`, `pocao_vida`, `presa`, `seda`.
- **Armas** (render dos modelos atuais): `ico_<arma>_t<n>_<nome>` (espada, machado, cajado, escudo t1..t3).
- **Modelos para drop no chão:** `Models/itens/item_<item>.glb`, que **vêm no complemento** (os ícones já estão aqui).
  - tamanho real (0,1 a 0,3 m), origem no chão;
  - ~4 mil triângulos, textura assada de 512;
  - vidro e brilho em material simples.
  - Para leitura no chão, o jogo pode escalar 2 a 3×.
- **Uso:** dentro do `ui_slot_hab` ou `ui_slot`, com o ícone a ~85% do slot.

## 5. HUD v2 (`Art/ui_*`)
Igual à ficha anterior (Estudos):

| Peça | Uso / fatiamento |
|---|---|
| `ui_moldura` | 9-slice 26 |
| `ui_moldura_fina` | 9-slice 10 |
| `ui_tooltip` | 9-slice 18 |
| `ui_aba` | 3-slice cap 48 |
| `ui_placa_zona` | 3-slice cap 64 |
| `ui_barra_chefe` | 3-slice cap 120 |
| `ui_barra_inferior` | 1800×220; orbes em x 380 / 1420, y 112 |
| `ui_orbe_*` | 640 px; `OrbeHud.cs` novo, `FracRaio` 196/640 |
| `ui_slot_hab` / `_ativo` | slot de habilidade (normal / selecionado) |
| `ui_xp_moldura` | moldura da barra de XP |
| `ui_mapa_moldura` | 9-slice 48 |
| `ui_bussola` | bússola |
| `ui_barra_alvo` | barra do alvo |

- `ico_mapa_*` e `ico_zona_*`: ícones de legenda.
- Backup do HUD v1 em `backups/hud_v1_antes_do_v2/`.
- Prévia: `previas/hud_v2.png`.

## 5b. Barra compacta (igual à referência do dono)
- **Arquivo:** `Art/ui_barra_compacta.png` (1050×236), com orbe à esquerda, placa com 6 slots e orbe à direita.
- **Prévia:** `previas/barra_compacta.png`.
- **Medidas em pixels da textura:**
  - centro dos orbes em (118, 118) e (932, 118);
  - raio do líquido 72, mesmo `OrbeHud` (`FracRaio` 196/640 sobre o orbe de 236);
  - slots de 92 px com topo em y = 102; x = 224, 326, 428, 530, 632, 734;
  - números 1 a 6 escritos pelo código, 14 px abaixo de cada slot;
  - slot selecionado: desenhar `ui_slot_hab_ativo` por cima.
- **Uso:** alternativa menor à `ui_barra_inferior`. Escalar inteira, sem fatiar.

## 6. Tela de mapa
- **`Art/mapa_costa_do_cal.png` (2048²):**
  - cobre o mapa de −130 a +130 em x e y;
  - **norte (y negativo) no topo**;
  - pixel = `(coord + 130) / 260 * 2048`.
- **`Art/mapa_nevoa_textura.png`:** máscara de nuvem para as áreas não exploradas.
- **Marcadores** (coordenadas do mapa):

  | Local | x | y |
  |---|---|---|
  | Porto das Cinzas | 0 | 112 |
  | Galeria Funda | −99 | −64 |
  | Vale | 48 | −112 |
  | Acampamento das Dunas | 0 | 19 |
  | Ruínas do Farol | 60 | −32 |
  | Enseada do Tirano | 88 | −88 |
  | Baía Quebrada | 0 | −86 |

- Atalho: tecla M.
- Prévia: `previas/tela_mapa.png`.

## 7. Cena do print da Costa (a que o dono gostou)
- **`Client/Ambiente/CenaPrintCosta.cs`:**
  - monta lago, coqueiros/pinheiros, 5 rochas, 60 tufos de capim, 12 arbustos, 8 grupos de flores, alga, tronco, seixos e concha;
  - usa os `.glb` de `Models/costa/` que já estão no jogo.
- **Chamada:** `CenaPrintCosta.Montar(raizDoMapa, new Vector2(-31f, 49f));` no build visual do mapa da Costa.
  - A origem (−31, 49) põe o lago do print em cima do `lago` que já existe no `map_costa` (−20, 37).
  - O jogador vê a cena olhando para o norte, como no print.
- **Conversão usada:** câmera do jogo yaw 0 = norte.
  - `mapa_x = origem_x − x_render`
  - `mapa_y = origem_y + y_render`
  - `yaw = yaw_render + 180`
- **Colisão:** `Tools/obstaculos_para_gerar_regioes.json` (11 caixas `invisivel` para rochas e árvores) entra pelo `Tools/gerar_regioes.py`, não à mão.
  - É `invisivel` para o mapa não desenhar a pedra duas vezes.
- **Paredão de penhasco do fundo:** opcional (`comParedao: true`). Cairia em y = 19 e bloquearia a passagem, por isso vem desligado.
- **Luz do print:** sol vindo do nordeste, `DirectionalLight3D.RotationDegrees ≈ (-48, 145, 0)`, céu azul claro. No `CicloDoDia`, a hora ~15h é a que mais se parece.
- **Lista completa com coordenadas absolutas:** `Tools/cena_print_costa.json`.


## 7b. Guia visual da praia (o look do print, só o visual)
O que faz o print ter cara de jogo. Vale para a Costa inteira, não só para a cena:
1. **Paredão de rocha clara no fundo.** Use `CenaPrintCosta.ParedaoVisual` sobre os penhascos do `map_costa`, que já têm colisão:
   - norte: `ParedaoVisual(raiz, new(-112,-109), new(35.2,-109), Vector2.Zero)` e `ParedaoVisual(raiz, new(60.8,-109), new(112,-109), Vector2.Zero)`;
   - sul: `(-112,109)→(-8,109)` e `(8,109)→(112,109)`;
   - oeste: `(-109,-112)→(-109,-9.6)` e `(-109,9.6)→(-109,112)`;
   - leste: `(109,-112)→(109,19.2)` e `(109,44.8)→(109,112)`;
   - penhascos internos da Gruta, altura 0,75: `(-105.6,-48)→(-57.6,-48)` e `(-57.6,-99.2)→(-57.6,-70.4)`.
   - Em todos, o último argumento é `Vector2.Zero` (a face olha para o centro).
   - Se o MapView já desenha penhasco nessas bordas, troque o desenho antigo por este, para não ficar duplicado.
2. **Chão em duas texturas misturadas:**
   - `ter_areia` + `ter_costa_cal` (as lajotas rachadas);
   - mistura por ruído grande (~25 m), cada textura repetindo a cada **6 m**, com normal map ligado;
   - tom levemente escurecido, porque a areia clara estoura com sol forte.
3. **Densidade de vegetação** (é o que dá a vida do print):
   - capim: ~1 tufo a cada 25 m² (variar a, b, c com yaw aleatório);
   - arbustos: 1 a cada ~120 m², em grupos de 2;
   - flores: 1 a cada ~180 m²;
   - seixos, conchas e troncos espalhados perto das rochas.
   - Nada em fileira: sempre posição e giro aleatórios.
4. **Rochas grandes e brancas em primeiro plano:** `costa_rocha_a/b/c` em escala 1,1 a 2,2 (altura um pouco menor que a largura), sempre com 2 ou 3 seixos em volta.
5. **Água de lago com borda de pedras:** `costa_lago` com caniços. Fica azul-turquesa mesmo com o sol alto.
6. **Luz:**
   - sol forte e quente: cor (1, 0.92, 0.8), energia alta, ~48° de altura, vindo do nordeste;
   - sombras nítidas: `angular_distance` pequeno (~3°);
   - céu azul claro (0.42, 0.55, 0.78) como luz ambiente;
   - sem neblina perto, só bem longe.
7. **Contraste dos monstros:** caranguejos coloridos (azul, vermelho) sobre o chão claro, com o shader de contorno do Cal Lote 1 (`contorno_monstro.gdshader`) ligado nos monstros.

## 8. Cenário dos estudos (`Models/`)
Peças: `marco_torre_partida`, `cenario_ponte_arcos`, `cenario_cais`, `cenario_carroca_quebrada`, `cenario_vagonete`, `cenario_trilhos`, `cenario_blocos_cal_a/b/c`, `cenario_estandarte_tridente`, `cenario_tocha_poste`, `cenario_tocha_parede`.
- Colisão em `_colisao.glb` onde existe.
- **Sugestões de posição:**

  | Peça | Onde |
  |---|---|
  | `marco_torre_partida` | (60, −40) |
  | `cenario_cais` | Praia dos Barcos (−84, 96), yaw 180 |
  | vagonete + trilhos | (−96, −64), apontando para a Galeria |
  | tochas | Acampamento da Vigília (34, −90) |

- Tochas: `OmniLight3D` laranja ligada a `CicloDoDia.LuzDasTochas`.

## Pendente (próximos lotes; a lista do dono está em `MEMORIA_ARTE.md`)
- `cr_tirano_da_mare` em PBR.
- Lista de 10 pontos do dono: móveis, moradores com roupa e animações, arsenal grande de armas por raridade, armaduras por corte em 3 níveis, pet, montaria, kit da Galeria, kits das regiões, sons e peças de puzzle.

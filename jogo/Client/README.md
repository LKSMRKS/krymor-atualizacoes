# Cliente Godot (E8) — 3ª pessoa, bonecos articulados

Cliente do protótipo: conecta ao servidor, mostra o mapa que o servidor manda e envia **intenções** (o servidor decide tudo).
Abra pelo `Jogar.cmd` da raiz. Controles, classes, inimigos e regras: `Docs/PROTOTIPO_E8.md`.

- Fluxo: tela de conexão → **lobby** (criar/escolher Guerreiro ou Mago) → mundo.
- `Main.cs`: rede, entrada (câmera, buffer de ações, travar alvo), sincronização e efeitos.
- `ThirdPersonCamera.cs`: câmera orbital (mouse, zoom, encosta nos obstáculos, segue o alvo travado).
- `MapView.cs`: desenha o `MapLayout` recebido (chão por região, muralhas, penhascos, portão/alavanca, baús, tochas).
- `Visuals/`: `IEntityVisual` (componente substituível), `HumanoidVisual`, `CrabVisual`, `GearFactory` (armas/escudo/armaduras),
  `VisualFactory` (único lugar que escolhe a aparência), `TelegraphView` (marca vermelha da preparação dos inimigos).
- `LobbyView.cs`, `InventoryPanel.cs` (tecla I), `HudView.cs`.
- Argumentos depois de `--`: `--host --port --token --autoconnect [--character Nome] [--create Nome:classe]`.
  Para testes automáticos: `--demo luta|esquiva`, `--shots "seg=arquivo.png;..."`, `--quit-after seg`, `--cam-yaw`, `--cam-pitch`, `--cam-zoom`.

## Histórico (E5–E7)
# Cliente Godot (E5) — geometria simples

Cliente de teste do Marco A: conecta ao servidor, mostra o mundo com formas simples e envia **intenções**
(o servidor decide tudo). Não usa as artes dos lotes; elas são só referência.

- Godot **4.7.2 .NET** + .NET SDK 8. O projeto usa `Godot.NET.Sdk/4.7.2` (restaurado do NuGet no primeiro build).
- Este projeto **não faz parte** de `CinzasDaMare.sln` de propósito: `dotnet build`/`dotnet test` da raiz continuam
  independentes do Godot. Compile o cliente separadamente (veja abaixo).
- A lógica testável (estado, entrada, mira, HUD) fica em `Shared/Client` e é coberta por `dotnet test`.
  `Main.cs` é só a casca que fala com o Godot.

## Teclas
| Tecla | Ação |
|---|---|
| WASD / setas | mover |
| mouse | mirar: o personagem olha para o ponto do chão sob o cursor (define o arco frontal da Guarda) |
| Q | Golpe de Cunha no monstro vivo mais próximo |
| E | Guarda de Escudo (reduz 40% dos golpes que vêm de frente; escudo azul translúcido) |
| R | liga/desliga o ataque básico automático |
| F | pegar o item mais próximo |
| 1 | usar a primeira poção da mochila |
| G / H | equipar a primeira arma da mochila / desequipar |

## Argumentos de linha de comando (depois de `--`)
`--host 127.0.0.1 --port 7777 --token <TOKEN> --autoconnect`. Sem eles, preencha o painel de login na tela.

## Se o Godot reclamar do projeto
Abra no editor (`--editor --path .\Client`), clique em **Build** (martelo) e use **Play**. Erros de C# aparecem no painel Output.

## Visual (E7)
- Personagem e monstro são **cartazes** das artes conceituais (`Assets/Art`, ver `ORIGEM.md`): uma pose, sem animação de esqueleto. **Não são modelos 3D.** Se um arquivo faltar, o cliente usa formas simples.
- Animações de cartaz, todas disparadas pelos **eventos do servidor**: avanço ao atacar, clarão ao apanhar, queda ao morrer, pulso ao ativar a Guarda/usar poção/subir de nível/coletar, risco de golpe, faíscas de impacto, números de dano e tremor leve da câmera quando você apanha.
- Câmera mais próxima (fov 38°). Chão de pedra e muros com textura procedural; braseiros que iluminam o chão; estandartes, caixotes, barris, árvores.
- Indicadores discretos: anel fino no chão (azul = você, verde = outro jogador, vermelho = monstro, dourado = item); leque branco bem leve = para onde você olha; leque azul leve = arco da Guarda (aparece com **E**).
- HUD compacto: cartão do jogador (retrato, nome, nível/% de XP, Vida, Fôlego e XP) no canto superior esquerdo; alvo no topo; barra de habilidades com ícones e recarga embaixo; registro de combate curto (5 linhas) à direita.
- Cenário em `ArenaBuilder.cs`, efeitos em `Effects.cs`, HUD em `HudView.cs`.
- Detalhes, limites e o que falta: `Docs/ASSETS_E7.md`. Modelos 3D e animações de verdade: `Docs/ESPECIFICACAO_MODELOS_3D.md` e `Tools/validar_glb.py`.

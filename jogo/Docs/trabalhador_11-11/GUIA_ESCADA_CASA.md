# Escada e piso do casarão (11/11): o boneco sobe de verdade, sem teleporte

**Problemas:**
- O boneco afundava uns 50 cm no assoalho do térreo.
- No 2º andar afundava uns 12 cm.
- A escada era só um portal: o boneco teleportava para cima.

**Solução:** o cliente passa a pôr os pés na altura certa do chão. O servidor continua decidindo só X/Y, como manda a regra.
- Na escada, a altura sobe degrau por degrau, de 0,53 m até 4,63 m.
- O portal para o 2º andar fica **no topo da escada**.
- O mapa `casarao_andar2` já desenha o prédio no mesmo lugar, 4,45 m para baixo. Por isso a troca de mapa acontece com o boneco já lá em cima e não se vê pulo.
- Para descer, é o mesmo caminho ao contrário: no 2º andar o boneco desce pelo vão da escada e o portal fica na base.

Arquivo novo: `Client/Visuals/EscadaCasa.cs`.

## 1. MapView.cs: registrar os prédios

No início da montagem do mapa (no construtor, antes de `AddGround()` / props):
```csharp
Visuals.EscadaCasa.Limpar();
```
Em `CasaraoUpstairs()`, logo antes de `return true;`:
```csharp
Visuals.EscadaCasa.RegistrarAndar2();
```
Em `LotTwoBuilding(...)`, logo depois de `node.AddChild(model);`:
```csharp
if (p.Kind == "casarao_cidade") Visuals.EscadaCasa.RegistrarCasarao(node);
```

## 2. MapView.cs: portal da escada sem arco de pedra

No começo de `AddPortal(MapPortal portal)`:
```csharp
if (Visuals.EscadaCasa.EhPortalDeEscada(portal.X, portal.Y)) return;   // a escada é a passagem; o portal fica invisível
```
Os portais precisam ser adicionados **depois** dos props, para o casarão já estar registrado.

## 3. Onde o jogador, NPCs e monstros recebem a posição a cada quadro

No arquivo que move os visuais a partir do X/Y do servidor (onde se chama `SetViewer(x, y)` e se faz `visual.Root.Position = ...`), troque o Y fixo 0 por:
```csharp
var y = Visuals.EscadaCasa.Altura(x, z);   // x, z = posição do mapa
visual.Root.Position = new Vector3(x, y, z);
```
Faça isso também no alvo da câmera, se ela seguir um ponto e não o nó do boneco. Assim a câmera sobe junto.

Para não dar tranco, suavize a altura:
```csharp
visual.Root.Position = visual.Root.Position with { Y = Mathf.Lerp(visual.Root.Position.Y, y, 1f - Mathf.Exp(-18f * (float)delta)) };
```

## 4. Dados do mapa (pelo `Tools/gerar_regioes.py` / reorganizar_porto, nunca à mão)

Coordenadas para o casarão do Porto em (20, -17), giro 0. Para outro lugar, some a posição do prop às coordenadas locais.

| o quê | coordenada local do modelo | no mapa do Porto |
|---|---|---|
| portal térreo → `casarao_andar2` (topo da escada), raio 0,8 | (-6,5 ; 0,9) | (13,5 ; -16,1) |
| chegada no 2º andar (patamar de cima) | (-6,5 ; 0,2) | — (o mapa do andar2 usa coordenada local) |
| portal 2º andar → térreo (base do vão), raio 0,8 | (-6,5 ; 4,7) | — |
| chegada no térreo (pé da escada) | (-6,5 ; 5,4) | (13,5 ; -11,6) |

**Colisão:**
- No térreo, a faixa da escada (x -7,35..-5,65, z 0,6..4,95 local) tem de ser **andável**. Se o reorganizar gerou um obstáculo "invisivel" com a caixa da escada (o `_colisao.glb` tem uma), remova só esse obstáculo. Deixe o corrimão (x -5,58) como parede.
- No 2º andar, o vão da escada também tem de ser andável. O guarda-corpo continua bloqueando os outros lados.

## Status
- **Implementado:** `EscadaCasa.cs` e os trechos acima.
- **Não testado:** não consegui compilar aqui (não tenho o projeto inteiro nem o dotnet), e não sei o arquivo exato que posiciona os visuais (não estava na cópia que eu tinha).
- **Próximo comando:** aplicar os trechos, `dotnet build`, entrar no casarão e subir a escada.

# Pendentes de 10/11: o que está pronto e ainda NÃO está no jogo

Conferido em 11/11 contra a pasta do jogo, arquivo por arquivo e pixel a pixel. Todo o resto da arte (HUD v2, ícones, mapa, cenário dos estudos, modelos da praça, cabeças, caranguejos) **já está no jogo e igual**.

Copie a pasta `Client/` deste zip por cima da do jogo. Depois aplique os passos abaixo.

## 1. Goblins no corpo do personagem principal
- **Arquivos:** `Models/humanoides/mo_goblin_{saqueador,arqueiro,chefe}.glb`.
- O jogo ainda usa os goblins antigos (Cal Lote 2).
- **Nada a mudar no código:** o `VisualFactory` já carrega `humanoides/mo_goblin_*`. É só sobrescrever os arquivos.
- Mesmo esqueleto e mesmos 6 clipes.

## 2. Itens 3D novos (drop no chão)
- **Arquivos:** `Models/itens/item_*.glb` (15 itens).
- O jogo ainda usa os modelos antigos, de 100 a 400 triângulos.
- Vai também `item_minerio_v2.glb`, que o `MapView` (linha ~2734) já procura.
- **Nada a mudar no código.**

## 3. Barra da gárgula e do dragão (versão final)
- **Arquivos:** `Art/ui_barra_gargula.png`, `ui_barra_gargula_fundo.png`, `ui_pote_mascara_vida/mana.png`, `Shaders/pote_liquido.gdshader` e `Client/BarraGargula.cs`.
- O jogo tem uma prova antiga dessa arte, mas a HUD em uso é a `HudV2` (orbes).
- **Para trocar:** no lugar da `HudV2` de baixo, criar a `BarraGargula`:
```csharp
var b = new BarraGargula(); hud.AddChild(b); b.Escala = tela.X / BarraGargula.W * 0.62f;
b.Position = new(tela.X / 2 - BarraGargula.W * b.Escala / 2, tela.Y - BarraGargula.H * b.Escala);
b.Vida = hp / maxHp; b.Mana = recurso / maxRecurso;   // a cada atualização
// slots: b.SlotRect(0..7) (coordenadas da barra; multiplicar pela Escala); XP: b.XpRect
```
- A barra tem três camadas, em ordem: fundo, líquido (balança, bolhas, respingo quando cheio) e frente. A ordem já está montada no `BarraGargula.cs`.
- Se a `HudV2` continuar, pode deixar as duas e ligar uma opção.

## 4. Praça do Porto montada
- Os modelos já estão no jogo, mas **ninguém os posiciona**.
- **Arquivo:** `Client/Ambiente/PracaDoPorto.cs`. Chamar `PracaDoPorto.Montar(raiz)` ao montar o mapa `porto`. Ele põe:
  - fonte e estátua no centro (0, 2);
  - 4 bancos;
  - 2 barracas;
  - carrinho;
  - barris;
  - 2 árvores;
  - 3 floreiras.
- **No `map_porto`** (via `Tools/gerar_regioes.py`):
  - tirar a `fogueira` (0, 2) e as `tocha` (±3, 5);
  - pôr as colisões de `Tools/praca_obstaculos.json`.
- **Substituídos:** `cenario_lanterna_rua.glb` (poste de ferro com lampião) e `cenario_barril.glb` (barril de aduelas). O jogo ainda tem as versões do Cal Lote 2.

## 5. Cabeças de bicho (arquivos já no jogo, código ainda não usa)
- Hoje `MannequinVisual.SetEvolved` (linha ~2117) ainda monta Lion/Crow/Snake/Cat com formas simples.
- **Trocar** as linhas do `_animal = new Node3D ...` e do `switch` por:
```csharp
_animal = CabecaAnimalGlb.Anexar(_headSocket, _classId);
if (_animal is null) { _animal = new Node3D { Name = "CabecaAnimal", Position = new Vector3(0, 0.1f, 0), Scale = Vector3.One * 1.15f }; _headSocket.AddChild(_animal);
    switch (_classId) { case "guerreiro": Lion(_animal); break; case "mago": Crow(_animal); break; case "arqueiro": Snake(_animal); break; default: Cat(_animal); break; } }
```
- `CabecaAnimalGlb.cs` já está no jogo.

## 6. Cena do print da praia (arquivo já no jogo, não chamado)
- `Ambiente/CenaPrintCosta.cs` está no jogo, mas ninguém chama.
- **Para usar:**
  - `CenaPrintCosta.Montar(raizCosta, new Vector2(-31, 49));`
  - opcional, o paredão nas bordas: `CenaPrintCosta.ParedaoVisual(...)`.
  - As coordenadas estão na FICHA_ATUALIZACAO, item 7b.

## 7. Colisões dos cenários dos estudos
- **Arquivos:** `Models/*_colisao.glb` (8): torre, ponte, cais, carroça, vagonete, blocos de cal.
- Os modelos visuais já estão no jogo, mas as colisões não.

## Status
- **Não testado** no Godot.
- **Próximo comando:** `dotnet build` e rodar.

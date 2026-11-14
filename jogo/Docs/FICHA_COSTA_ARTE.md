# Costa do Cal — tapa artístico (09/11/2026)

Pacote único com **tudo da nova atualização da Costa**: o kit de cenário novo e o **Lote 3** inteiro (criaturas, goblins, acampamento, farol, entrada da Galeria). O Lote 3 tem ficha própria em `FICHA_LOTE3.md`.

**Copiar** a pasta `Client/` do pacote por cima da `Client/` do projeto.

O único arquivo que é substituído é o `Assets/Art/ter_areia.png`, com o relevo `_n.png`. O backup dos originais já está no projeto, em `backups/arte_antes_costa_2026-11-09/`. Todo o resto é arquivo novo.

**Nada foi alterado em `.cs`, `.json`, servidor ou banco de dados.** As seções abaixo trazem o código pronto para quem integra colar.

Os prints de antes e depois estão em `previas/costa/` (`costa_depois.png` é o trecho montado na câmera do jogo).

## O que muda na tela e por quê

| Problema (print do dono) | Solução no pacote |
|---|---|
| chão chapado e listrado, repetição visível | `ter_areia.png` nova: areia de cal em tom médio, ondulação de vento só em manchas, cascalho, lascas de concha e relevo. Segunda textura `ter_costa_cal.png` (lajes de calcário rachadas), misturada em manchas pelo `TerrainShader`, que já existe |
| horizonte sem graça (muros cinza, cones pretos) | `costa_penhasco_a/b`: paredão de cal em camadas no lugar das caixas cinza das bordas. `costa_horizonte_a/b`: serras de cal ao longe, no lugar das bolas do `AddHorizon` |
| monstros se misturam com o gráfico | chão menos saturado e em tom médio (bichos vermelhos, turquesa e laranja destacam pela cor); `contorno_monstro.gdshader` (contorno escuro fino); caranguejos do Lote 3 com uma cor por espécie |
| falta vida: ponte, lago, pedras, mato | 3 rochas, 3 pinheiros-mansos, 2 coqueiros, 3 arbustos, 3 capins, 2 flores, 2 algas, concha, 2 troncos, seixos, ponte, trecho de ravina e lago com água |

## Arquivos

### Texturas — `Client/Assets/Art/`

São 2048 px, emendam nas 4 bordas, e o relevo é normal OpenGL (Godot).

| Arquivo | Uso |
|---|---|
| `ter_areia.png` + `_n` (**substitui**) | chão A da praia. Já entra sozinha, sem código. Também é usada pelo tema neve (que tinge de branco) |
| `ter_costa_cal.png` + `_n` (nova) | chão B da praia: lajes de cal com capim seco e líquen |
| `ter_areia_molhada.png` + `_n` (nova) | beira d'água, trilhas úmidas, decalques (opcional) |
| `*_r.png` | rugosidade de cada uma (opcional; o `TerrainShader` hoje usa 0,94 fixo) |

### Modelos — `Client/Assets/Models/costa/`

Todos com textura PBR embutida, origem no chão e frente +Z.

| Peça | Tamanho do arquivo | Como escalar no jogo |
|---|---|---|
| `costa_rocha_a/b/c` | raio ≈ 1,0–1,15 m, altura 1 m | escala (r, height, r) do obstáculo `rocha` |
| `costa_penhasco_a/b` (+ `_colisao`) | trecho de 10 m (X) × 3 m (fundo) × 8 m | repetir a cada 10 m ao longo do `penhasco`; já sobrepõe 0,4 m de cada lado, sem fresta |
| `costa_pinheiro_a/b/c` (+ `_colisao`) | 6,2 / 8,2 / 5,5 m de altura | escala = height / altura do arquivo |
| `costa_coqueiro_a/b` (+ `_colisao`) | 8,2 / 6,5 m | idem |
| `costa_arbusto_a/b/c` | cerca de 1,2 m | 0,8–1,4 |
| `costa_capim_a/b/c`, `costa_flores_a/b` | 0,5–1,0 m | espalhar (enfeite, sem colisão) |
| `costa_alga_a/b`, `costa_concha_grande`, `costa_tronco_a/b`, `costa_seixos` | enfeites de praia | sem colisão |
| `costa_ponte` | 9,6 m (X) × 5 m (Z) | casa com o portão `ponte_ravina` (9,6 × 2,4) |
| `costa_ravina_trecho` | 10 m (X) × 2,4 m | repetir ao longo da `ravina`; desenhado **sobre** o chão |
| `costa_lago` (+ `_colisao`) | lagoa de 14 × 10 m com água translúcida, margem molhada, pedras e juncos | obstáculo novo (ver seção 6) |
| `costa_horizonte_a/b` | 70 × 22 m | anel além das bordas (seção 4) |

### Shader — `Client/Assets/Shaders/contorno_monstro.gdshader`

Contorno escuro fino em volta dos inimigos (seção 5).

## Código para colar

O código usa `P()`, `R()`, `Root`, `Collider()` e `MapKit.Glb()`, que já existem no `MapView`/`MapKit`.

### 1. Chão — `MapView.TerrainMaterial()`, linha do tema `"praia"`

```csharp
"praia" => ("ter_areia.png", new Color(1f, 1f, 1f), "ter_costa_cal.png", new Color(1f, 0.98f, 0.95f), 0.35f),
```

Logo depois, para a praia, o tile da laje pode ser maior (assim ela não repete):

```csharp
if (Map.Theme == "praia") { m.SetShaderParameter("tile_a", 6f); m.SetShaderParameter("tile_b", 9f); }
```

### 2. Bordas — `case "penhasco":` (trocar o desenho das caixas, mantendo o `Collider`)

```csharp
var w = (float)o.W; var d = (float)o.H;
var along = w >= d; var len = along ? w : d; var depth = along ? d : w;
var n = Math.Max(1, (int)Mathf.Ceil(len / 10f));
var seg = len / n;
// frente do trecho (+Z do modelo) virada para o centro do mapa
var toCenter = new Vector2(-(float)o.X, -(float)o.Y);
var yaw = along ? (toCenter.Y * 1f > 0 ? 0f : 180f) : (toCenter.X > 0 ? 90f : -90f);
var drew = false;
for (var i = 0; i < n; i++)
{
    var t = -len / 2f + seg * (i + 0.5f);
    var node = new Node3D { Position = P(o.X, o.Y) + (along ? new Vector3(t, 0, 0) : new Vector3(0, 0, t)),
        RotationDegrees = new Vector3(0, yaw, 0), Scale = new Vector3(seg / 10f, h / 8f, Math.Max(1f, depth / 3f)) };
    Root.AddChild(node);
    drew |= MapKit.Glb(node, "costa/costa_penhasco_" + (i % 2 == 0 ? "a" : "b"), Vector3.Zero, 0f, 1f);
}
if (!drew) { /* desenho antigo (caixas) aqui */ }
Collider(P(o.X, o.Y, h / 2f), new BoxShape3D { Size = new Vector3(w, h, d) });
break;
```

Atenção ao sinal do yaw: no `MapView`, P(x, y) usa y do mapa como Z. Se o paredão aparecer de costas, troque 0 por 180 (ou 90 por −90).

### 3. Rochas, árvores e o prop `pinheiros`

- **`case "rocha":`** antes de `MapKit.Boulder(...)`:
  ```csharp
  if (!MapKit.Glb(node, "costa/costa_rocha_" + "abc"[(int)R(0, 2.99f)], Vector3.Zero, 0f, 1f)) MapKit.Boulder(this, node, r, h);
  else node.Scale = new Vector3(r, h, r);
  ```
- **`case "arvore":`** com o tema praia (no lugar de `PalmTree`/`DryBush`/`PineTree`):
  ```csharp
  case "praia":
  {
      var (f, baseH) = pick < 0.45f ? ("costa_coqueiro_" + (pick < 0.25f ? "a" : "b"), pick < 0.25f ? 8.2f : 6.5f)
                     : pick < 0.85f ? ("costa_pinheiro_" + "abc"[(int)R(0, 2.99f)], 6.6f)
                     : ("costa_arbusto_" + "abc"[(int)R(0, 2.99f)], 1.2f);
      var sc = f.Contains("arbusto") ? R(1.0f, 1.5f) : Math.Max(h, 5f) / baseH * R(0.9f, 1.15f);
      if (!MapKit.Glb(node, "costa/" + f, Vector3.Zero, 0f, sc)) MapKit.PalmTree(this, node, Math.Max(h, 5f));
      break;
  }
  ```
- **`MapKit` `case "pinheiros":`** com o tema praia: 2–3 `costa_pinheiro_*` com raio de até 2 m, 1 `costa_rocha_*` com escala 0,5 e 3 `costa_capim_*`. Usar o desenho antigo se o arquivo faltar.

### 4. Horizonte — `AddHorizon(size)` com o tema `"praia"` (no lugar das bolas de duna)

```csharp
if (Map.Theme == "praia" && Visuals.GlbCache.Instance("costa/costa_horizonte_a") is not null)
{
    var half = size / 2f;
    for (var a = 0f; a < 360f; a += R(14f, 20f))
    {
        var dist = half + R(45f, 95f);
        var pos = new Vector3(Mathf.Sin(Mathf.DegToRad(a)) * dist, -0.5f, Mathf.Cos(Mathf.DegToRad(a)) * dist);
        var node = new Node3D { Position = pos, RotationDegrees = new Vector3(0, a + 180f, 0), Scale = Vector3.One * R(1.0f, 1.7f) };
        Root.AddChild(node);
        MapKit.Glb(node, "costa/costa_horizonte_" + (R(0, 1) < 0.5f ? "a" : "b"), Vector3.Zero, 0f, 1f);
    }
    return;
}
```

O mar do céu (`sky shader`, opção "mar até o horizonte") continua valendo atrás das serras. Para deixar o mar à vista de um lado, pule os ângulos daquele lado.

### 5. Destaque dos monstros — `GlbCreatureVisual` (no laço que duplica os materiais)

```csharp
private static ShaderMaterial? _contorno;
// ...
_contorno ??= ResourceLoader.Exists("res://Assets/Shaders/contorno_monstro.gdshader")
    ? new ShaderMaterial { Shader = GD.Load<Shader>("res://Assets/Shaders/contorno_monstro.gdshader") } : null;
if (_contorno is not null && !copy.EmissionEnabled) copy.NextPass = _contorno;
```

Valem também para o `MannequinVisual.Monster` (goblins, esqueleto): mesma linha nos `_baseMats`.

A largura padrão é 0,014 m. Para os chefes, use um `ShaderMaterial` duplicado com `largura` 0,03.

### 6. Espalhar vida — `AddScatter(size)` com o tema `"praia"` (além do capim de cartão atual)

```csharp
if (Map.Theme == "praia")
    for (var i = 0; i < 260; i++)
    {
        var x = R(-size / 2f + 4f, size / 2f - 4f); var z = R(-size / 2f + 4f, size / 2f - 4f);
        // (opcional) pular se cair numa estrada ou numa zona "seguro"
        var k = R(0, 1);
        var f = k < 0.5f ? "costa_capim_" + "abc"[(int)R(0, 2.99f)] : k < 0.65f ? "costa_flores_" + (k < 0.58f ? "a" : "b")
              : k < 0.78f ? "costa_seixos" : k < 0.88f ? "costa_alga_" + (k < 0.83f ? "a" : "b") : k < 0.95f ? "costa_arbusto_" + "abc"[(int)R(0, 2.99f)]
              : k < 0.98f ? "costa_tronco_" + (k < 0.965f ? "a" : "b") : "costa_concha_grande";
        var node = new Node3D { Position = new Vector3(x, 0, z), RotationDegrees = new Vector3(0, R(0, 360), 0) };
        Root.AddChild(node);
        MapKit.Glb(node, "costa/" + f, Vector3.Zero, 0f, R(0.85f, 1.25f));
    }
```

Peças sem colisão.

Desempenho: são cerca de 260 instâncias de 1,5 a 4,5 mil triângulos. Se pesar, troque por `MultiMeshInstance3D`, um por arquivo (a malha sai do `GlbCache`).

### 7. Ravina, ponte e lago

- **`case "ravina":`**
  - Repetir `costa/costa_ravina_trecho` a cada 10 m ao longo do comprimento.
  - Escala `(seg / 10, 1, d / 2.4)`, girada 90° se a ravina correr em Z.
  - Usar o desenho antigo se o arquivo faltar.
- **Portão `ponte_ravina`** (`gate kind "ponte"`): quando a bandeira `ponte:ravina` estiver liberada, mostrar `costa/costa_ponte` em (gate.X, gate.Y), com escala (w / 9,6; 1; 1) e girada para cruzar a ravina.
- **Lago**: é um obstáculo **novo** (`kind "lago"`).
  - Entra no mapa por `Tools/gerar_regioes.py`. Não editar o `.json` à mão.
  - Medida sugerida: `shape circle`, `r 5,5`, `height 1`, numa clareira da Costa, longe das estradas, por exemplo a zona "refugio".
  - No `MapView`: `case "lago": MapKit.Glb(node, "costa/costa_lago", Vector3.Zero, 0, 1); Collider(..., new CylinderShape3D { Radius = 5.5f, Height = 1 })`.
  - O servidor só precisa aceitar o kind novo como obstáculo comum (adição, nada muda no que existe).

## Estado (regra do projeto)

- **Implementado:**
  - 3 texturas de chão;
  - 29 modelos do kit da Costa, mais os 22 do Lote 3;
  - o shader de contorno;
  - esta ficha com o código.
- **Executado:**
  - gerei e conferi tudo no Blender;
  - montei e renderizei a cena de prova na câmera do jogo (`previas/costa/costa_depois.png`);
  - fiz backup da `ter_areia` original no projeto.
- **Não testado:**
  - nada foi importado no Godot nem rodado no jogo (não há Godot nem .NET no ambiente de geração);
  - os trechos de código desta ficha não foram compilados.
- **Próximo comando:**
  1. copiar `Client/` para o projeto;
  2. abrir o editor do Godot uma vez, para importar os `.glb`;
  3. a `ter_areia` já aparece sem código;
  4. colar as seções 1 a 7 conforme a prioridade (sugestão: 1, 2, 4, 5, 3, 6, 7);
  5. rodar `dotnet build .\Client\CinzasDaMare.Client.csproj` e `.\Jogar.cmd`.

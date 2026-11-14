# Monstros 1–7 e 6 armaduras (11/11/2026)

Copie a pasta `Client/` deste zip por cima da `Client/` do jogo. Ela só **adiciona** arquivos novos; faça backup antes.

## 1. Monstros no corpo do boneco principal (mesmo esqueleto e mesmos clipes do manequim)

| arquivo | monstro | altura no jogo | onde |
|---|---|---|---|
| `humanoides/mo_rastejante_gesso.glb` | Rastejante de Gesso: cego, boca vertical com dentes, lascas nas costas, garras compridas | 1,4 m | trilha e caverna de cal |
| `humanoides/mo_gargulito.glb` | Gargulito de Estalagmite: pedra, chifres de estalagmite, asinhas, rabo, olhos vermelhos | 1,1 m | caverna, em grupos de 3–4 |
| `humanoides/mo_golem_cal_viva.glb` | Golem de Cal Viva: blocos de cal e rachaduras laranja brilhando | 2,6 m | **chefe da caverna de cal** |
| `humanoides/mo_espantalho.glb` | Espantalho: saco costurado, chapéu de palha, tufos de palha | 1,9 m | campo e plantação |

```csharp
// VisualFactory
"rastejante_gesso" => MannequinVisual.MonsterBody("humanoides/mo_rastejante_gesso", 1.4f),
"gargulito"        => MannequinVisual.MonsterBody("humanoides/mo_gargulito", 1.1f),
"golem_cal_viva"   => MannequinVisual.MonsterBody("humanoides/mo_golem_cal_viva", 2.6f),
"espantalho"       => MannequinVisual.MonsterBody("humanoides/mo_espantalho", 1.9f),
```

Golem: ponha uma `OmniLight3D` laranja fraca (cor `#ff8a20`, energia 0,8, alcance 4) presa no peito. A rachadura já vem brilhando na textura.

## 2. Criaturas com esqueleto próprio (padrão do `GlbCreatureVisual`)

Têm os clipes `idle`, `walk`, `attack_windup`, `attack_strike`, `attack_recover`, `hit` e `death`.

- `criaturas/verme_broca.glb`: **Verme-Broca** de 2,4 m. A broca da cabeça **gira** nos clipes.
  - É a única exceção à regra do corpo do boneco, porque verme não tem corpo humano.
- `criaturas/inseto_lodo.glb`: **Inseto do Lodo**, besouro de 0,55 m com olhos verdes brilhando.
  - Ponha 2–3 em volta de cada slime.

```csharp
"verme_broca" => new GlbCreatureVisual("criaturas/verme_broca", 0.5f),
"inseto_lodo" => new GlbCreatureVisual("criaturas/inseto_lodo", 0.35f),
```

## 3. Gárgula do muro do portão (cenário)

`cenario/cenario_gargula_muro.glb`: a estátua do Meshy (a mesma da barra) sobre uma mísula de pedra a 3,2 m de altura.

- A origem fica no pé da parede.
- A gárgula olha para **+Z**.
- A parede fica a **1,1 m atrás (−Z)**. Encoste esse lado no muro.

```csharp
MapKit.Glb(node, "cenario/cenario_gargula_muro", P(x, y), yaw, 1f);   // yaw: virada para quem chega no portão
// olhos vermelhos: duas OmniLight3D vermelhas pequeninas (energia 0,6, alcance 1,2) a 3,2+1,05 m de altura
```

Se ela for virar monstro ao abrir o portão (acorda e desce), é só usar o `mo_gargulito` em escala maior no mesmo lugar.

## 4. Armaduras do Meshy: separadas e encaixadas no esqueleto do manequim

Arquivos em `armaduras/<id>.glb`. Cada um tem **6 peças** (cada peça é um nó próprio):

| peça | nó |
|---|---|
| capacete | `<id>_capacete` |
| peitoral (com ombreiras; nas aladas, com as asas) | `<id>_peitoral` |
| braçadeiras | `<id>_bracadeiras` |
| luvas | `<id>_luvas` |
| perneiras (com a saia) | `<id>_perneiras` |
| botas | `<id>_botas` |

| id | nome |
|---|---|
| `armadura_abissal` | Armadura Abissal |
| `armadura_anjo` | Armadura do Anjo Alado |
| `armadura_lobo` | Armadura do Lobo Sombrio |
| `armadura_fenix` | Armadura da Valquíria Fênix |
| `armadura_dragao` | Armadura do Dragão Rubro |
| `armadura_serpente` | Armadura da Serpente |

**Como foi feito:**
- Cada armadura veio inteira, com os braços abaixados.
- Posei o manequim igual a ela, copiei os pesos dos ossos do corpo e desfiz a pose. Assim cada peça fica na mesma pose de repouso do manequim e usa **o mesmo esqueleto**: todas as animações já servem.
- Asas presas às costas.
- Saia e placas do quadril nunca seguem os braços.
- Capacete centrado na cabeça.
- Cada armadura ficou com cerca de 39 mil triângulos e texturas de 2048.

Para vestir, adicione no `MannequinVisual.cs` (cada peça usa a malha e o Skin do próprio glb, presos ao `_skel` do boneco):

```csharp
private readonly List<MeshInstance3D> _armadura = new();

/// <summary>Veste uma armadura do Meshy (mesmo esqueleto). pecas vazio = conjunto inteiro;
/// ex.: SetArmadura("armadura_lobo", "capacete", "botas") para só essas peças.</summary>
public void SetArmadura(string? id, params string[] pecas)
{
    foreach (var n in _armadura) n.QueueFree();
    _armadura.Clear();
    if (id is null) return;
    var src = GlbCache.Instance("armaduras/" + id);
    if (src is null) return;
    foreach (var mi in src.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
    {
        var parte = mi.Name.ToString().Replace(id + "_", "");
        if (pecas.Length > 0 && Array.IndexOf(pecas, parte) < 0) continue;
        var copia = (MeshInstance3D)mi.Duplicate();
        _skin.GetParent().AddChild(copia);
        copia.Skeleton = copia.GetPathTo(_skel);
        copia.Skin = mi.Skin;          // poses de encaixe do próprio glb (mesmo repouso do manequim)
        _armadura.Add(copia);
    }
    src.QueueFree();
}
```

**Ao vestir o capacete**, esconda a cabeça, o cabelo e a cabeça de animal do boneco. Senão aparecem pelo visor.

Sugestão de itens:
- conjunto lendário: 6 peças por armadura;
- servidor: `model_asset_id = "armadura_lobo:capacete"`, por exemplo;
- cliente: separa id e peça e chama `SetArmadura`.

## Status

**Implementado:**
- 4 monstros no manequim;
- verme e inseto com esqueleto e clipes próprios;
- gárgula do muro;
- 6 armaduras separadas em peças e com pesos.

**Executado:** tudo gerado e renderizado. As prévias ficam em `previas/`, com as armaduras em pose T, andando e parado.

**Não testado no Godot:**
- `SetArmadura`;
- importação do verme e do inseto;
- as armaduras em golpes com os braços muito levantados. Nos movimentos de ataque mais extremos pode haver um pouco de esticado nas ombreiras.

**Próximo comando:** copiar `Client/`, abrir o Godot para importar, `dotnet build`, vestir uma armadura no boneco e atacar.

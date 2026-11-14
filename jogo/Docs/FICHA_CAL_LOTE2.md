# Cal Lote 2 — ficha de integração (09/11/2026)

Copiar a pasta `Client/` deste pacote por cima da `Client/` do projeto.

Os backups de **tudo que é substituído** já estão no projeto:
- `backups/cal_lote2_2026-11-09/` (texturas `gen_*`, `cenario_barril`, `cenario_caixote`, `cenario_lanterna_rua`, `proj_barril_explosivo`);
- `backups/criaturas_lote2/` (os `cr_*` antigos têm o mesmo conteúdo dos `criaturas/*` do lote 2).

**Nenhum `.cs` existente foi alterado.** O código novo está em 3 arquivos novos, em `Client/Ambiente/`, mais 1 shader. As ligações (as poucas linhas que precisam entrar no `MapView`, no `MannequinVisual` e no `VisualFactory`) estão abaixo, prontas para colar.

`MEMORIA_ARTE.md` resume as regras, entregas e próximos lotes. Serve para o próximo agente ou artista não começar do zero.

## 0. Entra sozinho, sem código

| Arquivo | Efeito |
|---|---|
| `Art/gen_reboco`, `gen_bloco`, `gen_madeira`, `gen_calcamento` (+ `_n`) | Agora em 1024 px com desgaste: reboco caiado com manchas, escorridos e tijolo à mostra; alvenaria com argamassa, quinas gastas e musgo; tábuas com veio e pregos; calçamento irregular com mato nas juntas. **Toda casa, muro e muralha que o código desenha com essas texturas melhora na hora.** |
| `Models/cr_caranguejinho` … `cr_caranguejo_escavador` | **São os caranguejos PBR do Lote 3 com os nomes que o jogo carrega hoje.** Era por isso que o jogo ainda mostrava o caranguejo antigo. O Ermitão já vem com `barrel_throw_windup` e `barrel_throw`. |
| `Models/proj_barril_explosivo.glb` | Barril explosivo de verdade, com pavio e brasa. O que estava no projeto era cópia do barril comum. Origem no centro. |
| `Models/cenario_barril`, `cenario_caixote`, `cenario_lanterna_rua` | Mesmos modelos do lote 2, agora com material PBR desgastado. |

## 1. Arte de verdade nos NPCs — `Models/npcs/npc_<id>.glb`

São 14 pacotes de peças de destaque, presas aos ossos do boneco atual (o NPC continua sendo o `MannequinVisual`):

| id do pacote | NPC | Peças |
|---|---|---|
| `ferreiro` | Ferreiro Brás | avental de couro com bolso e pinça, martelo |
| `curandeira` | Curandeira Ilma | xale verde com franja, bolsa de ervas |
| `capita` | Capitã Ondina | tricorne com debrum de ouro e pena, dragonas, gola |
| `pescador` | Pescador Tibério | chapéu de palha, rede embolada no ombro com boias |
| `mentor` | Mestre Dorval | barba e cabelo brancos, manto com os 4 emblemas das classes |
| `arcanista` | Isaura | luvas azuis, livro aberto flutuando com runa acesa |
| `bruxa` | Bruxa Hazel | chapéu torto com fivela, gato preto de olhos amarelos no ombro |
| `batedor` | Batedor Kaelen | capa com capuz, luneta no cinto |
| `agricultora` | Agricultora Rosa | enxada, cesto de frutas, lenço |
| `bobo` | Bobo Tião | chapéu de 3 pontas com guizos, gola de retalhos |
| `batedora` | Batedora Lia | arco, aljava, faixa na cabeça |
| `mineiro` | Mineiro Otávio | capacete com lanterna acesa, picareta |
| `mascate` | Mascate Bento | mochila gigante com panelas, frigideira, pá, lanterna e saquinhos |
| `guarda` | Guarda Rui | elmo com crista, peitoral, ombreiras, tabardo, lança |

**Como funciona:**
- Cada `.glb` tem nós com o **nome do osso** (`Head`, `Spine2`, `Hips`, `LeftHand`, `RightHand`).
- O conteúdo de cada nó já está no espaço do `Socket()`: metros, +Y cima, +Z frente, origem no osso, pose T.
- Os itens de mão ficam ao longo do braço na pose T: com o braço baixado (Idle), a lança fica em pé e o martelo pende.

**Colar no `MannequinVisual`** (método novo):
```csharp
/// <summary>Veste o pacote de destaque do NPC (Assets/Models/npcs/npc_<id>.glb). false se o arquivo faltar.</summary>
public bool VestirPacote(string id)
{
    if (Visuals.GlbCache.Instance("npcs/npc_" + id) is not { } pack) return false;
    foreach (var ch in pack.GetChildren())
        if (ch is Node3D n3 && Bone(n3.Name) >= 0)
        {
            pack.RemoveChild(n3);
            Socket(n3.Name).AddChild(n3);
            n3.Transform = Transform3D.Identity;
            foreach (var mi in n3.FindChildren("*", nameof(MeshInstance3D), true, false))
                if (mi is MeshInstance3D m3) m3.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        }
    pack.QueueFree();
    return true;
}
```

No `VisualFactory.NpcMannequin`, depois de criar o manequim, chamar `m.VestirPacote(id)` mapeando o `look` para o id. Os looks atuais já batem com vários ids (`ferreiro`, `curandeira`, `capita`, `guarda`, `mentor`, `arcanista`, `bobo`). Para os outros, usar o id do NPC.

- **NPCs femininas** (Ilma, Ondina, Isaura, Hazel, Rosa, Lia): o pacote serve igual no `manequim_fem`. Se o chapéu ficar folgado, use escala 0,95 no nó `Head`.
- **Ao vestir chapéu ou elmo:** esconder o cabelo e o chapéu primitivo do NPC, se houver.

## 2. Vida no ambiente — `Ambiente/VidaAmbiente.cs` (novo)

O que tem:
- gaivotas que circulam, batem asa, planam e pousam (voam quando o herói chega perto);
- siris de enfeite, que são o `caranguejinho` a 0,32×, andando de lado e fugindo do jogador;
- rajadas de areia no vento;
- fogueira completa (modelo, chamas, fagulhas, fumaça e luz que pisca e fica mais forte à noite);
- coluna de fumaça de marco (cerca de 35 m, vista de longe);
- feixe giratório do farol (forte à noite, quase invisível de dia).

**Colar no `MapView`** (mapas abertos, depois de criar o herói/câmera e o chão):
```csharp
var vida = new VidaAmbiente { Metade = (float)Map.HalfSize, Jogador = () => /* posição do herói */ Vector3.Zero };
Root.AddChild(vida);
if (Map.Theme is "praia") { vida.Gaivotas(8); vida.Siris(10); vida.Vento(); }
// em cada prop "fogueira" (no lugar do desenho atual): vida.Fogueira(P(p.X, p.Y));
// acampamento goblin (tendas/"acampamento"): vida.ColunaDeFumaca(P(x, y), grande: true);
// no case "farol": vida.FeixeDoFarol(P(o.X, o.Y, h + 1.2f));
```
A posição do herói vem do mesmo lugar que o `AmbientParticles` já usa para seguir o jogador.

**Goblins fazendo coisas no acampamento:** os 4 `Models/humanoides/mo_*.glb` (os do Lote 3) ganharam clipes de laço:
- `Sentado` (de pernas cruzadas perto do fogo);
- `Mexendo` (mexe a panela);
- `Conversando` (gesticula).

Sugestão: quando um goblin inimigo estiver parado sem alvo (fase `sleep` ou parado há mais de 3 s) a até 4 m de uma fogueira, tocar um desses três em vez do `Idle`. É só visual; o servidor continua decidindo quando ele acorda e ataca.

## 3. Marcos vistos de longe

- **Farol aceso:** `vida.FeixeDoFarol(...)`. O modelo `cenario_farol` do Lote 3 já tem a lanterna acesa.
- **Fumaça do acampamento goblin:** `vida.ColunaDeFumaca(pos, grande: true)` no centro do acampamento.
- **Ossada gigante da Enseada do Tirano:** `Models/costa/marco_ossada_gigante.glb` (+ `_colisao`).
  - Esqueleto de cerca de 37 m de comprimento e 8,5 m de altura, meio enterrado, com cracas.
  - Entra no mapa por `Tools/gerar_regioes.py` como obstáculo novo (aditivo): `kind "ossada_gigante"`, `shape box`, `w 4,6`, `h 30`, `height 8`, na enseada sul.
  - No `MapView`: `case "ossada_gigante": MapKit.Glb(node, "costa/marco_ossada_gigante", Vector3.Zero, 0, 1); Collider(...)`.
  - Dá para passar por baixo das costelas; a colisão cobre só o crânio e a coluna.

## 4. Luz por hora do dia — `Ambiente/CicloDoDia.cs` (novo)

- **Ciclo:** madrugada → manhã → meio-dia (o `Look` atual do tema, sem mudança) → **fim de tarde dourado** (sol baixo e laranja, sombras longas) → crepúsculo violeta → noite azul com estrelas e tochas fortes.
- **Duração:** um dia leva 36 min (`CicloDoDia.DuracaoMinutos`).
- **Sincronia:** a hora vem do relógio UTC, então todos os jogadores veem a mesma hora sem mensagem nova do servidor.
- **Teste:** `CicloDoDia.HoraFixa = 18.2f;` fixa o fim de tarde.

**Colar em `MapView.AddEnvironment`**, no fim. Só em mapas abertos, não em gruta ou dungeon. É preciso guardar o `skyMat` local:
```csharp
if (!cave)
    Root.AddChild(new CicloDoDia().Configurar(_env, _sun, skyMat, _sun.LightColor, _sun.LightEnergy, _look.SunPitch, _look.SunYaw,
        _env.AmbientLightColor, _env.AmbientLightEnergy, _env.FogLightColor, _env.FogDensity, _env.TonemapExposure,
        _look.SkyTop, _look.Horizon, _look.Stars));
```

No laço que pisca as chamas (`foreach (var (light, phase, energy) in _flames)`), multiplicar a energia por `CicloDoDia.LuzDasTochas`. `CicloDoDia.Noite` (0 a 1) serve para acender janelas, vagalumes ou trocar a música.

Se o `GraphicsQuality` reaplicar a névoa, criar o `CicloDoDia` depois disso.

## 5. Mar de verdade — `Ambiente/MarDaCosta.cs` + `Assets/Shaders/mar_costa.gdshader`

É um plano só com o shader, que faz três faixas:
- **areia molhada** brilhante, que escurece onde a água acabou de passar, com risco de espuma;
- **arrebentação** com borda branca e linhas de espuma chegando;
- **mar** com ondas que crescem para longe e cor rasa → funda até o horizonte.

A linha d'água vai e volta (vaivém de cerca de 2,6 m).

**Colar no `MapView`, depois do chão:**
```csharp
if (Map.Theme == "praia") MarDaCosta.Construir(Root, (float)Map.HalfSize, "sul");
```
Lados possíveis: norte (+Z), sul (−Z), leste (+X), oeste (−X).

No `case "penhasco"`, pular só o desenho do lado do mar, mantendo o `Collider`, que continua sendo a parede do servidor:
```csharp
if (MarDaCosta.Lado == "sul" && o.Y < -Map.HalfSize * 0.9) { Collider(...); break; }
```

Usa `ter_areia_molhada.png` (do Cal Lote 1), `fx_agua_n.png` e `fx_espuma.png` (deste pacote).

## 6. Muros e casas com textura e desgaste (Porto)

- Os prédios do lote 2 estão em `Models/` com os mesmos nomes, nós e marcadores `spawn_*`, e as colisões vão junto:
  - `edificio_casarao_cidade`, `edificio_taverna_grande`, `edificio_portao_fortificado`;
  - `ruina_casarao_abandonado`, `ruina_salao_vazado`;
  - `cenario_casa_pequena`, `cenario_casa_sobrado`;
  - `cenario_banca_mercado`, `cenario_forja`, `cenario_cerca`, `cenario_bau`, `cenario_ponte_quebrada`, `cenario_ponte_reparada`.
- **Material novo:** alvenaria com argamassa, quinas gastas, musgo e barro na base; reboco manchado com escorridos, rachaduras e tijolo à mostra; telhas em fiadas com líquen; madeira com veio e acinzentada; ferro com ferrugem.
- Janelas acesas, velas e fogo continuam com o material original.
- **Ruínas:** mais escuras, com fuligem. No salão saíram os cacos que flutuavam sem encostar em nada. No casarão abandonado não há peça solta assim; as que pareciam cacos são pedaços encostados na estrutura.
- **Para usar na cidade:** onde o mapa do Porto tem `casa` (obstáculo `box` de cerca de 9 × 7 m), trocar o desenho por `MapKit.Glb(node, "cenario_casa_sobrado" / "cenario_casa_pequena" / "edificio_casarao_cidade", ...)` com escala para caber no `w × h` do obstáculo. A colisão do servidor não muda.
- **Casas onde dá para entrar** (casarão, taverna): precisam de mudança no mapa e na colisão do servidor. Isso fica para um lote próprio, como combinado.

## Estado (regra do projeto)

- **Implementado:**
  - texturas: 4 de parede e chão, 5 de efeito;
  - modelos: 14 prédios e props refeitos com PBR, 6 `cr_*` PBR, barril explosivo, gaivota animada, fogueira, ossada gigante, 4 goblins com clipes de acampamento, 14 pacotes de NPC;
  - código e shader: 3 classes novas (`MarDaCosta`, `CicloDoDia`, `VidaAmbiente`) e o shader do mar;
  - esta ficha e a memória.
- **Executado:**
  - gerei e conferi tudo no Blender;
  - fiz os prints em `previas/`;
  - guardei os backups no projeto.
- **Não testado:**
  - nada foi importado no Godot nem compilado (não há Godot nem .NET no ambiente de geração);
  - as 3 classes C# novas e o shader podem precisar de um ajuste pequeno na primeira compilação.
- **Próximo comando:**
  1. copiar `Client/`;
  2. abrir o editor do Godot uma vez, para importar os `.glb`;
  3. `dotnet build .\Client\CinzasDaMare.Client.csproj`;
  4. colar as ligações das seções 1 a 5;
  5. `.\Jogar.cmd`.

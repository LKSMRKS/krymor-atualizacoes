# Guia para o programador: pacote de 11/11 à noite

**O que vem aqui:** tudo o que foi feito **depois** do `CinzasDaMare_ATUALIZACAO_2026-11-11.zip` (e do texto da seção 5 dele). Já inclui os pacotes menores `ChaoCosta` e `TapaVisual`, então use **só este**.

**Regras da casa (mantidas):**
- o servidor decide tudo;
- migrações só acrescentam;
- nenhum id existente muda;
- campos novos de protocolo são opcionais;
- arquivo substituído tem backup em `backups/`.

---

## 0. Copiar
Copie `Client/`, `Shared/` e `Server/` por cima das pastas do jogo:
- só arquivos **novos** de código, mais o `Client/Ambiente/KitTapa.cs`, que é nosso;
- as texturas e os modelos que substituem algo têm o original em `backups/`.

Os `.json` de `Content_sugestao/` **não** vão direto para o `Content/`. Os itens da seção 4 dizem o que acrescentar em cada um.

## 1. `MapView.cs`: tapa visual (peças que eram forma crua)
Detalhes de cada modelo estão na seção 5.
1. **`AddProp`**, depois de `var wood = ...`:
   ```csharp
   if (KitTapa.Prop(this, node, p.Kind)) return;
   ```
2. **`AddCraftObject`**, no `case "forja" or "bancada" ...`:
   ```csharp
   if (!KitTapa.Oficina(this, node, propKind)) MapKit.TryBuild(this, node, propKind, wood);
   ```
3. **`AddCraftObject`**, no `case "veio":`, depois de `var y = obj.Yields ?? "";`:
   ```csharp
   if (KitTapa.Veio(node, y)) return true;
   ```
4. **`AddObstacle`**, no começo, depois de calcular `h`:
   ```csharp
   if (KitTapa.Obstaculo(this, Root, P(o.X, o.Y), o.Kind, (float)o.W, (float)o.H, h, (float)o.R, o.Solid)) return;
   ```
5. **`TerrainMaterial()`**, no tema `"praia"`: troque o último número de `0.35f` para `0.15f` (menos lajes de cal no chão da Costa). A textura `ter_costa_cal` nova, mais suave, já vem no pacote.

Se faltar um .glb, cada método devolve `false` e o desenho antigo continua.

## 2. Chão, relevo e profundidade
O passo a passo está em **`LEIA_CHAO.md`**:
- 2 linhas para a névoa de profundidade (`Profundidade.cs`);
- 5 pontos para o relevo suave (`Relevo.cs`), que é só visual: o servidor continua plano;
- as texturas `ter_grama` e `ter_terra` novas trocam só o arquivo.

## 3. `VisualFactory.cs`
```csharp
// switch (visual), antes do case "cerberus" antigo (se houver):
case "cerberus" when Glb("criaturas/cao_tres_cabecas", 2.2f, BoarClips) is { } cer: return cer;
```
O cão de três cabeças usa o mesmo rig e os mesmos clipes do javali:
- `idle`, `walk`, `attack_windup`, `attack_strike`, `attack_recover`, `hit`;
- `death`: dobra as patas e desaba de barriga.

## 4. Conteúdo (`Content_sugestao/` → `Content/`, só acrescentar)

| arquivo de sugestão | vai para | o que é |
|---|---|---|
| `items_materiais_formas.json` | `items_novas.json` (fim) | Sangue Lunar, Cinza Vampírica, Osso Lunar, Madeira Verde, Gorro do Saci, Casca da Árvore da Serpente |
| `consumables_formas.json` | `consumables.json` (fim) | as 5 relíquias de transformação (campos novos e opcionais: `transform`, `reusable`, `soulbound`) |
| `skills_formas.json` | `skills_novas.json` (fim) | 20 habilidades das formas (`class_id` = id da forma) |
| `quests_formas.json` | `quests_novas.json` (fim) | Lobisomem, Vampiro, Ceifador e Anjo (giver `sibila_vidente`) e Máscara em 3 etapas (giver `bobo_tiao`) |
| `recipes_formas.json` | `recipes.json` (fim) | receita da Máscara na bancada |
| `loot_adicoes_formas.json` | as tabelas `loot_bst_*` citadas | uma entrada nova em cada (chance e item) |
| `transformacoes.json` | arquivo novo `Content/transformacoes.json` | as 5 formas (lido pelo `FormaDefinition`) |
| `mapas_adicoes.json` | `map_casarao_andar2.json` e a Mata (via `Tools/gerar_regioes.py`) | mesa de cartas e carteador no 2º andar; Árvore da Serpente com o ponto de coleta da casca na Mata dos Enforcados |

**Conferir antes de ligar:**
- se Lobisomem, Vampiro, A Morte, Anjo, Demônio e Saci têm spawn nos mapas citados;
- o XP das recompensas, com `Tools/simular_progressao.py`.

## 5. Modelos novos e trocados

**Trocados (mesmo nome):**
- `costa/costa_arbusto_a/b/c` (folhas de verdade);
- `costa/costa_tronco_a/b`.

**Novos em `Models/cenario/`** (todos ligados pelo `KitTapa`):
- tochas e acampamento: `cenario_tocha_chao`, `cenario_placa`, `cenario_tenda_lona`;
- veios: `cenario_moita_erva`, `cenario_arvore_tabua`, `cenario_veio_ferro` (rocha com cristais azuis, igual ao ícone do minério);
- muros e obstáculos: `cenario_muro_cal`, `cenario_muro_cal_b`, `cenario_ruina_cal`, `cenario_coluna`, `cenario_barricada` (calcário gasto, com musgo no pé e cor diferente em cada bloco);
- oficinas: `cenario_caldeirao`, `cenario_panela`, `cenario_bancada`, `cenario_armazem`;
- horta: `cenario_espantalho`, `cenario_feno`, `cenario_regadores`;
- enfeites da Costa: `cenario_cranios`, `cenario_ossada`, `cenario_redes`, `cenario_barco_encalhado`, `cenario_acampamento_abandonado`, `cenario_cerca_quebrada`;
- props novos: `cenario_mesa_cartas` e `cenario_arvore_serpente` (kinds `mesa_cartas` e `arvore_serpente`).

**Outros novos:**
- `criaturas/cao_tres_cabecas.glb`;
- `itens/item_mascara_pandemonio.glb` e `Art/ico_mascara_pandemonio.png`;
- `Art/cartas/*.png`: baralho próprio, 52 cartas mais o verso. O nome de cada arquivo é o código da engine (`AE`, `10C`, `QO`...).

**Peso:** texturas de 512 nas peças pequenas e de 1024 nas grandes, com 500 a 5.000 triângulos por peça (o cão tem 33 mil).

## 6. Servidor
Detalhes na **`Docs/FICHA_FORMAS_MASCARA_VINTEUM.md`**.

**Transformações** (`Server/Combat/Transformacao.cs`): um `EstadoForma` por jogador. Os pontos de ligação estão na ficha:
- uso da relíquia;
- tick;
- dano causado e recebido;
- cura;
- movimento;
- habilidades da forma;
- restrições;
- o campo opcional `form` no frame da entidade.

**Vinte-e-Um** (`Shared/Cassino/VinteUm.cs` e `Server/Cassino/MesaVinteUm.cs`):
- uma `MesaVinteUm` por mesa;
- a `ICarteira` é implementada com as moedas do jogador, para salvar como qualquer gasto;
- `Apostar(id, aposta, hoje, nivel)`, `Agir(id, acao, hoje)` e `Abandonar(id, hoje)`;
- nível mínimo 7, apostas de 10 a 500 e limite de perda de 3.000 por dia.

**Mensagens novas sugeridas:**
- do cliente: `vinteum_apostar` e `vinteum_acao`;
- do servidor: `vinteum_estado`.

**Ao sentar:** o servidor escolhe uma cadeira livre, teleporta o jogador e trava o movimento até ele levantar.

## 7. Cliente (telas que ainda não existem)
- **Mesa do 21:**
  - pose sentado (quadril −0,45 m, coxas 85° para a frente, joelhos 85° para trás, braços na mesa);
  - cartas com `Art/cartas`;
  - campo de aposta e botões das `acoes` que o servidor mandar;
  - botão "Levantar".
- **Formas:** quando o frame vier com `form`, trocar pelo visual:
  - Lobisomem, Vampiro, Ceifador e Anjo usam os visuais do bestiário que já existem;
  - Pandemônio usa o corpo do jogador com a pele verde-limão e a máscara presa na cabeça (`BoneAttachment3D`).
- **Barra de tempo da forma:** com o tempo restante e a recarga da relíquia no slot.

## 8. Ferramentas (para refazer ou ajustar)
- `Tools/modelagem/`: scripts Blender de todos os modelos deste pacote;
- `Tools/gerar_cartas.py`;
- `Tools/gerar_chao_suave.py`;
- `Tools/simular_vinteum/` (`dotnet run`): confere a matemática do 21 e testa a mesa e as formas.

## Status
- **Implementado:** tudo acima.
- **Executado:**
  - modelos gerados e renderizados (prévias em `previas/`);
  - C# compilado e testado aqui com .NET 8: 2 milhões de mãos de 21 dão vantagem da casa de cerca de 0,5% contra quem joga certo, como nos livros de cassino; a mesa (nível 7, limite diário) e as formas também foram testadas.
- **Não testado no Godot:**
  - as linhas de integração das seções 1, 2 e 3;
  - o shader do relevo;
  - a névoa de profundidade;
  - as telas da seção 7, que ainda precisam ser feitas.
- **Próximo comando:** copiar as pastas, colar as linhas das seções 1, 2 e 3, `dotnet build`, depois andar pela Costa, pelo Porto, pelo Vale e pelo 2º andar do casarão.

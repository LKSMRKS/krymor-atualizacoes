"""Monta final/CinzasDaMare_Noite_2026-11-11.zip com GUIA_NOITE.md gerado a partir dos _info.json."""
import json, os, glob, shutil, zipfile, sys
R = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ST = os.path.join(R, "final", "noite_stage"); ZIP = os.path.join(R, "final", "CinzasDaMare_Noite_2026-11-11.zip")
shutil.rmtree(ST, ignore_errors=True)

def cp(src, dst):
    d = os.path.join(ST, dst); os.makedirs(os.path.dirname(d), exist_ok=True); shutil.copy2(src, d)

# moveis + puzzle
moveis = []
for f in sorted(glob.glob(f"{R}/interior/*_info.json")):
    i = json.load(open(f)); a = i["asset_id"]; moveis.append(i)
    for ext in (".glb",):
        if os.path.exists(f"{R}/interior/{a}{ext}"): cp(f"{R}/interior/{a}{ext}", f"Client/Assets/Models/interior/{a}{ext}")
    if os.path.exists(f"{R}/interior/{a}.png"): cp(f"{R}/interior/{a}.png", f"previas/interior/{a}.png")
for f in glob.glob(f"{R}/interior/*.png"):
    b = os.path.basename(f)
    if not os.path.exists(os.path.join(ST, "previas/interior", b)): cp(f, f"previas/interior/{b}")
# arsenal
ars = json.load(open(f"{R}/arsenal/_arsenal_info.json"))
for a, i in ars.items():
    if os.path.exists(f"{R}/arsenal/modelos/{a}.glb"): cp(f"{R}/arsenal/modelos/{a}.glb", f"Client/Assets/Models/armas/{a}.glb")
    for suf in ("", "_256"):
        p = f"{R}/arsenal/icones/ico_{a[5:]}{suf}.png"
        if os.path.exists(p): cp(p, f"Client/Assets/Art/icones/armas/ico_{a[5:]}{suf}.png")
cp(f"{R}/arsenal/_arsenal_info.json", "dados/_arsenal_info.json")
if os.path.exists("/tmp/claude-0/arsenal_folha.png"): cp("/tmp/claude-0/arsenal_folha.png", "previas/arsenal_folha.png")
# mortos de cal
for v in ("mo_carnical_cal", "mo_fantasma_cal"):
    cp(f"{R}/mortos/{v}/{v}.glb", f"Client/Assets/Models/humanoides/{v}.glb")
    cp(f"{R}/mortos/{v}/{v}_info.json", f"dados/{v}_info.json")
    if os.path.exists(f"{R}/mortos/{v}/{v}_folha.png"): cp(f"{R}/mortos/{v}/{v}_folha.png", f"previas/{v}_folha.png")
# codigo + audio
for f in glob.glob(f"{R}/noite/**/*", recursive=True):
    if os.path.isfile(f): cp(f, os.path.relpath(f, f"{R}/noite"))
for t in ("build_interior.py", "build_arsenal.py", "build_mortos_cal.py", "sons_sintese.py", "itemkit.py", "pacote_noite.py"):
    cp(f"{R}/tools/{t}", f"Tools/arte/{t}")

# ---------------- guia
L = []; w = L.append
w("# GUIA DA NOITE — Cinzas da Maré (11/11/2026)\n")
w("Pacote novo. Nada aqui substitui arquivo do programador: são **arquivos novos**. Faça backup antes de copiar por cima de qualquer coisa.\n")
w("Copie a pasta `Client/` deste zip por cima da `Client/` do jogo (só adiciona arquivos).\n")
w("## 1. Arsenal com raridades (" + str(len(ars)) + " armas)\n")
w("12 formas × 4 qualidades: **Enferrujada (nv 1–5) → Comum (5–15) → Refinada (15–30) → Excelente (30+)**, mais graveto, aljavas, varinhas e talismãs da bruxa.")
w("- Modelos: `Client/Assets/Models/armas/<asset_id>.glb` — **a origem é a empunhadura** (centro da mão), lâmina/cabo para +Y no Godot, igual às armas do lote 2. Arcos: corda/empunhadura igual ao `MannequinVisual.Bow`.")
w("- Ícones: `Client/Assets/Art/icones/armas/ico_<id>.png` (448px) e `_256.png`.")
w("- Excelente tem gema e runa com brilho (emissivo) — deixe o glow do WorldEnvironment ligado.\n")
w("Trecho para o `GearFactory.LotTwoWeapon` (o model_asset_id já é o nome do arquivo):\n")
w("```csharp\n// arsenal da noite: qualquer model_asset_id que comece com \"arma_\" e exista em armas/\nif (model.StartsWith(\"arma_\") && ResourceLoader.Exists($\"res://Assets/Models/armas/{model}.glb\"))\n    return $\"armas/{model}\";\n```\n")
w("| asset_id | nome | tipo | empunhadura | nível | tris |\n|---|---|---|---|---|---|")
for a, i in ars.items():
    w(f"| `{a}` | {i['nome']} | {i['tipo']} | {i['empunhadura']} | {i['nivel_sugerido']} | {i.get('tris','')} |")
w("\n**Sugestão de drops:** Enferrujada → praia/porto (goblins, caranguejos); Comum → estrada e mina; Refinada → caverna de cal (carniçal, fantasma); Excelente → chefes (Golem de Cal Viva, Caranguejo-Geodo nv 5 com chance baixa de Comum).\n")
w("**Regra de empunhadura (do dono):** mão certa, arco na mão esquerda, escudo no antebraço, nada atravessando o corpo. Os modelos já vêm com a origem na pegada; **não testei no boneco animado dentro do jogo** — confira no Walking/Attack e ajuste só o offset do BoneAttachment se precisar.\n")
w("## 2. Móveis e peças de puzzle\n")
w("`Client/Assets/Models/interior/`. Cada um tem `_info.json` (em `dados/`) com caixa de colisão, luz sugerida e, nas peças de puzzle, `posicao_no_conjunto_godot` para montar as partes móveis.\n")
w("| asset_id | nome | medidas (m) | luz |\n|---|---|---|---|")
for i in moveis:
    luz = i.get("luz"); w(f"| `{i['asset_id']}` | {i.get('nome','')} | {i.get('dims_m','')} | {'OmniLight ' + luz['cor'] + ' alcance ' + str(luz['alcance']) + ' em ' + str(luz['pos_godot']) if luz else '—'} |")
    cp_info = f"{R}/interior/{i['asset_id']}_info.json"; cp(cp_info, f"dados/{i['asset_id']}_info.json")
w("\nExemplo (dentro da casa / taverna):\n```csharp\nMapKit.Glb(casa, \"interior/movel_mesa\", P(2, 3), 0, 1f);\nMapKit.Glb(casa, \"interior/movel_cadeira\", P(2, 2.2f), 180, 1f);\nMapKit.Glb(casa, \"interior/movel_lareira\", P(-3, 0), 90, 1f);   // + OmniLight3D da info.luz\n```\n")
w("Puzzle: `puzzle_alavanca` = `_base` fixa + `_cabo` (gira em X ±40°); `puzzle_placa` = `_base` + `_tampa` (desce 3 cm ao pisar); `puzzle_runa` = `_pedra` + `_brilho` (liga/desliga o emissivo). Coloque cada parte na `posicao_no_conjunto_godot` do info. O servidor decide o estado (aberto/fechado); o cliente só anima.\n")
w("## 3. Mortos de cal (corpo do manequim, como pedido)\n")
w("- `humanoides/mo_carnical_cal.glb` — **Carniçal Cáustico**: manequim afinado, pele de gesso rachado, crânio, garras de calcário, calça de mineiro rasgada e capacete com lanterna. 1,75 m.")
w("- `humanoides/mo_fantasma_cal.glb` — **Fantasma de Calcário**: manequim em poeira translúcida, olhos pretos. No Godot deixe o material com Transparency = Alpha e cull desligado; fica melhor com um GPUParticles3D de poeira leve saindo do corpo.")
w("Mesmo esqueleto Mixamo do manequim → usa as animações que já existem.\n")
w("```csharp\n// VisualFactory\n\"carnical_cal\"  => MannequinVisual.MonsterBody(\"humanoides/mo_carnical_cal\", 1.75f),\n\"fantasma_cal\"  => MannequinVisual.MonsterBody(\"humanoides/mo_fantasma_cal\", 1.8f),\n```\n")
w("**Posicionamento sugerido (do seu pedido):** fantasmas e carniçais de cal espalhados na trilha/caverna de cal, 1 a cada ~25–30 m, em grupos de 1–2; nunca na zona inicial. Editar via `Tools/gerar_regioes.py` (não à mão).\n")
w("## 4. Contorno do NPC (branco / amarelo / verde)\n")
w("Arquivos: `Client/Assets/Shaders/contorno_npc.gdshader` e `Client/Visuals/NpcContorno.cs`.\n")
w("```csharp\n// quando o nome do NPC aparece / mouse em cima:\nNpcContorno.Mostrar(npcNode, temEntrega ? NpcContorno.Estado.Entregar : temMissao ? NpcContorno.Estado.Missao : NpcContorno.Estado.Nada);\n// ao sair:\nNpcContorno.Esconder(npcNode);\n```\nO estado de missão vem do servidor (o cliente só pinta).\n")
w("## 5. Sons (42 arquivos .ogg sintetizados, livres de direitos)\n")
w("`Client/Assets/Audio/` — passos (areia/pedra/madeira ×4), golpes e impactos, arco/flecha, magias, interface, ambientes em loop (mar, vento, fogueira, caverna), gaivota e música calma do porto (64 s, loop). Volumes já normalizados (~−22 dB médio).")
w("```csharp\nvar s = new AudioStreamPlayer3D { Stream = GD.Load<AudioStream>(\"res://Assets/Audio/golpes/espada_corte.ogg\") };\nAddChild(s); s.Play();\n// loops: na importação marque Loop = on nos arquivos *_loop.ogg\n```\nPasso: sortear 1–4 do material do chão a cada pisada da animação.\n")
w("## Status\n- **Implementado:** arsenal, móveis/puzzle, carniçal e fantasma de cal, contorno de NPC, sons.\n- **Executado:** todos os modelos gerados e renderizados (prévias em `previas/`).\n- **Não testado:** dentro do Godot (offsets de mão das armas, importação do fantasma translúcido, luzes dos móveis).\n- **Próximo comando:** copiar `Client/` por cima, abrir o Godot para importar, `dotnet build`, e testar uma arma de cada tipo no boneco andando.\n")
w("**Ainda falta (próximas rodadas):** mão-garra de trás do dragão da barra; Rastejante de Gesso, Verme-Broca, Gargulito, Golem de Cal Viva, Gárgula do muro do portão, Espantalho; escada/chão da casa; roupas próprias dos moradores.\n")
open(os.path.join(ST, "GUIA_NOITE.md"), "w").write("\n".join(L))

if os.path.exists(ZIP): os.remove(ZIP)
with zipfile.ZipFile(ZIP, "w", zipfile.ZIP_DEFLATED) as z:
    for f in glob.glob(f"{ST}/**/*", recursive=True):
        if os.path.isfile(f): z.write(f, os.path.relpath(f, ST))
print(ZIP, round(os.path.getsize(ZIP) / 1e6, 1), "MB")

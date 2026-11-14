# Cinzas da Maré — instruções para o agente

RPG de ação online **privado** (grupo pequeno, rede local/VPN), PvE e farm. Direção atual (08/11/2026, E9): **RPG de farm
inspirado em MU** — níveis 1–60 em 8 regiões + 8 dungeons (midgame: `Docs/MIDGAME_M01_M06.md`), mapas conectados com faixa de poder própria, respawn, XP/equipamento como ciclo principal, chefes de
campo e dungeons; câmera e combate em 3ª pessoa; exploração por curiosidade. Personagem novo começa de cueca no nível 1.
Visual provisório (bonecos de formas simples). Idioma do projeto: **português**. Mundo e valores: `Docs/MUNDO_E9.md` (seção 12 = direção consolidada, prevalece).

## Arquitetura (não é a mesma dos outros projetos do usuário)
- **C# / .NET 8**, solução `CinzasDaMare.sln`: `Shared` (protocolo JSON, conteúdo, geometria do mapa, núcleo do cliente
  em C# puro), `Server` (simulação autoritativa por tick de 50 ms, combate, IA, SQLite), `ServerHost` (exe), `Tests` (xUnit).
- **Cliente Godot 4.7.2 .NET** em `Client/` (**fora da .sln**): só lê entrada, envia *intenções* e desenha o que o servidor manda.
- Rede: TCP próprio (`IServerTransport`), quadros ≤ 256 KB, sem TLS (só localhost/VPN).
- Dois modos do servidor no mesmo `GameServer`: **arena** (Marco A; usado pelos testes antigos) e **mapa** (`--arena` desliga;
  padrão do `ServerHost`). No modo mapa o estado fica em **zonas** (`GameServer.World.cs`): um mapa fixo cada, e uma zona por
  grupo para dungeon (`instanced`). Mundo em `Content/world.json` + `map_*.json`, `shops.json`, `quests.json`, comportamentos e loot.
- Ritmo de progressão: mude dados e rode `python3 Tools/simular_progressao.py --ate 61` antes de escolher números (não chute).
- Conteúdo 17–60 é **gerado**: edite as tabelas de `Tools/gerar_regioes.py` e rode-o (não edite `*_midgame.json`/mapas gerados à mão).
  Sons e texturas: `Tools/gerar_sons.py`, `Tools/gerar_texturas.py`, `Tools/gerar_terreno.py`; interface e ícones de habilidade:
  `Tools/gerar_ui.py` (CC0, feitos por código). Regra: todo objeto novo com textura (o `MapView.TexturizeAll` cobre o que ficar liso).
  Quadros de rede ≤ 256 KB.
- Renderizador Forward+ com volta automática ao OpenGL; efeitos caros por qualidade (`Client/GraphicsQuality.cs`, tecla F10).
  Clima/luz por tema em `MapView.LookFor`.
- Aparência separada das regras: `Client/Visuals/IEntityVisual` + `VisualFactory`. Regras nunca dependem da geometria.
- Quatro classes (Guerreiro, Mago, Bruxa, Arqueiro) no mesmo corpo-base: `MannequinVisual` (`Assets/Models/manequim_*.glb`,
  gerados por `Tools/preparar_manequim.py`); draco: `Tools/rig_dragao.py`. Recompensa por classe: `@classe:g|m|b|a`.

## Comandos reais (Windows, raiz do projeto)
```powershell
dotnet build -c Release                            # solução (Shared, Server, ServerHost, Tests)
dotnet test -c Release --no-build                  # esperado: 356 aprovados, 0 avisos
dotnet build .\Client\CinzasDaMare.Client.csproj   # cliente Godot (0 erros, 0 avisos)
.\Jogar.cmd                                        # compila se mudou, sobe servidor (e6.db) e cliente; -Quem bruno, -NovaConta x, -Parar
.\ServerHost\bin\Release\net8.0\CinzasDaMare.ServerHost.exe --db e6.db --list-accounts
```
Logs: `logs\servidor-AAAAMMDD.log` e `logs\cliente-<data>-<hora>-<pid>.log` (`--log-debug` para mais detalhe);
resumo: `Tools\logs.ps1`. Para investigar erro, leia `logs\` primeiro (formato e áreas: `Shared/Diagnostics/GameLog.cs`).
Cliente com automação (verificação visual): `--autoconnect --character Nome --demo luta|esquiva --shots "seg=arq.png" --quit-after seg`.
Ambiente de nuvem (Linux) pode não ter nuget.org: diga como compilou/testou e o que ficou sem rodar.

## Decisões confirmadas
- `Docs/DECISOES_OFICIAIS.md` prevalece sobre o GDD; seção 7 = mudança de prioridade (Guerreiro + Mago, visual provisório,
  modelos/11 animações adiados). Protótipo: `Docs/PROTOTIPO_E8.md`. Situação e pendências: `Docs/STATUS.md`.
- Servidor decide tudo (dano, XP, loot, posição). Cliente nunca envia resultado.
- Entregas pequenas e jogáveis; cada entrega diz: implementado / executado / não testado / próximo comando.
- **Todo personagem (jogador, NPC, criança, recorrente) é o NOSSO boneco padrão** (`MannequinVisual`): roupa, cabelo,
  barba, tamanho e acessórios por cima dele — nunca boneco de formas próprio (pedido do dono, 12/11).
- **Toda janela/aba do jogo é arrastável e fica SEMPRE dentro da tela** (regra básica, pedido do dono 13/11): registre com
  `Arrastador.Ligar(...)` (janela que se reposiciona sozinha: `porDesvio: true` + some `Arrastador.Desvio(nome)`); o
  Arrastador encolhe a que não cabe e devolve para dentro a que sair (margem de 8 px).
- Pendências abertas que não podem sumir: **E6 cenário B** (disputa real de item com 2 clientes), penalidade de morte.

## Dados que DEVEM ser preservados
- Bancos `e6.db` (contas alice/bruno e progresso) e `game.db`; tokens `e6-tokens.json`, `contas-locais.json`. Nunca apagar,
  recriar ou "limpar" sem pedido explícito. Testes usam bancos temporários.
- Migrações SQLite **só aditivas** (`GameDatabase.Migrations`), com backup automático; nunca editar migração já publicada.
- IDs de conteúdo (`Content/*.json`) e de slots são gravados no banco: renomear só `display_name`, nunca o `id`.
- Protocolo: campos novos opcionais com padrão; não quebrar mensagens existentes.
- Arte do usuário em `Client/Assets/Art` e cópias em `backups/`.

## Fluxo de trabalho
- Mudanças de jogo: use a skill **desenvolvimento-incremental** (`.claude/skills/`; fonte em `claude-config/`,
  instalada por `Tools\instalar-config-claude.ps1`).
- Revisores (`.claude/agents/`) só quando a tarefa justificar — ver `Docs/REVISORES.md`. Confira cada achado no código
  antes de corrigir; não corrija hipótese não verificada.

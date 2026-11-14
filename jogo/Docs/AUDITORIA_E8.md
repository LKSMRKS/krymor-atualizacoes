# Auditoria inicial da E8 (08/11/2026)

**Corrigido em 08/11/2026:** item 1 (teste `SetaQueMataAcolitoComBolaEmVoo_NaoDerrubaOServidor`). Também corrigido o
problema relatado pelo usuário "o servidor cai só de andar": a 120 Hz ou mais, andar girando a câmera mandava uma
MoveIntent por quadro (> 120/s) e o servidor desconectava o jogador; agora no máximo ~20/s (`MoveSender.ShouldSend(x, y, agora)`,
teste `AndarGirandoACamera_NaoPassaDoLimiteDeMensagens_EmQualquerFps`). O `Tick()` agora é protegido e registrado no log.
**Itens 2 a 5 corrigidos em 08/11/2026** (`Tests/AuditFixTests.cs`): marca = área de acerto (mesma função no desenho e no
golpe); sair morto renasce no acampamento; armas rebalanceadas (inicial < achada < chefe); quebrar a preparação põe o
ataque em recarga. **Achado novo na verificação, corrigido:** monstro encostado no jogador parava de atacar (distância
entre corpos ≤ 0 reprovava o alcance mínimo 0) — o caranguejo mordia uma vez e ficava parado.

Feita com `revisor-bugs` e `analista-gameplay` (independentes, só leitura) e medição de desempenho; cada achado
abaixo foi conferido pelo agente principal (reprodução ou leitura do caminho do código).

| # | Achado | Situação | Evidência |
|---|---|---|---|
| 1 | Seta que mata um Acólito com bola em voo derruba o servidor (`ArgumentOutOfRangeException` em `UpdateProjectiles`) | **Confirmado por reprodução** | `GameServer.Map.cs` `OnMapMonsterDied` faz `_projectiles.RemoveAll` durante o laço que depois chama `RemoveAt(i)`; `ServerHost` não captura exceção do `Tick()` |
| 2 | Área real do golpe maior que a marca vermelha (leque e bote) | **Confirmado por reprodução** (Saqueador: corpo fora da marca levou dano); Capataz: até 1,3 m fora | marca `GameServer.cs` `aimRadius = a.Range + 0.3`; acerto `a.Range + m.Def.Radius + 0.3` + raio do jogador |
| 3 | Morrer e desconectar antes de renascer: volta vivo, vida cheia, no local da morte | Confirmado pelo código | `RemoveSession` salva vida 0 e posição; `EnterWorld` ignora vida 0 e usa a posição salva |
| 4 | Vara Gêmea dá ~2,4× o dano da arma inicial (1,7× o prêmio do chefe); Machado tem menos dano/s que a Espada | Confirmado por cálculo | leque de ±4,5° faz as duas setas acertarem o mesmo alvo até ~12 m; intervalo 900 ms; Machado 24 de poder com 1400 ms |
| 5 | Quebrar a preparação libera o mesmo ataque do monstro em 800 ms (ignora a recarga) | Confirmado pelo código | `HitWithSkill` não grava `AttackReadyAt`; só o fim da fase ativa grava |

Também anotado (menor): marca da Explosão some para aliados não-Magos e não inclui o bônus de raio; ataque em sequência
espera o snapshot mostrar o fim da recuperação (hipótese de perda de ~15–20% de cadência — confirmar jogando);
câmera colada no herói junto às paredes; `_hitGrace` não limpa sessões; item "do mundo" sem posição coletável de longe (suspeito).

**Corrigidos em 08/11 (sobras 1 e 4):**
- Marca da Explosão: o servidor manda o raio real (`PlayerView.AimRadius` = raio da habilidade + `aoe_radius_bonus`, a mesma
  conta do acerto). Antes o cliente usava o raio da *própria* classe (Guerreiro aliado via raio errado). A mira do próprio
  Mago também cresce com o Cetro (`ItemInfo.AoeRadiusBonus` no catálogo). Teste: `Sobra1_MarcaDaExplosao_…`.
- Câmera junto de paredes: teste com esfera (não só raio), o chão saiu da camada que a câmera testa, e quando a parede
  encurta a câmera ela sobe até 2,2 m junto da parede e mira um pouco à frente (vê o caminho, não as costas do herói);
  se mesmo assim ficar a menos de 1,15 m, o próprio boneco é escondido. Volta devagar ao sair da parede (sem tremer).
  Conferido em capturas no cliente real (costas na muralha, diagonal, campo aberto inalterado). Sensação: só jogando.
- Continuam abertas: sobras 2 (cadência ao segurar o clique), 3 (esquiva na preparação) e 5.

Desempenho (medido, servidor isolado, 8 jogadores, 10 min simulados, SQLite substituto no Linux): tick médio 0,19 ms,
p99 0,6 ms, máx. 23 ms (autosave) de 50 ms; ~125 KB/s por cliente; maior quadro 13,6 KB de 64 KB. Sem problema.

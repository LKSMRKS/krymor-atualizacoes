# Laboratório (Sandbox) — Fase 1

Protótipos de sensação de jogo, isolados do jogo oficial, ligados a quente pelo **F1 → aba "Laboratório"**
(só existe com o servidor em `--deus`). Fluxo completo e regras de promoção: skill `godot-sandbox-architect`
(`claude-config/skills/godot-sandbox-architect/SKILL.md`).

## Arquivos
| Onde | O quê |
|---|---|
| `Client/Controllers/IPlayerController.cs` | Contrato do jogador + `PlayerHandoff` (posição, velocidade, yaw, visual, câmera, golpe básico) |
| `Client/Controllers/NetworkPlayerController.cs` | O controlador oficial de sempre, atrás do contrato (comportamento idêntico) |
| `Client/Sandbox/Core/PlayerSandboxManager.cs` | Troca a quente oficial ↔ protótipo; bonecos de treino; atmosfera; `CDM_LAB=1` = teste automático |
| `Client/Sandbox/Core/SandboxSettings.cs` | Números calibráveis (buffer, dash, hitstop) + `ISandboxLab` (o que o F1 enxerga) |
| `Client/Sandbox/Core/Hitstop.cs` | Hitstop por `Engine.TimeScale` com relógio real (só no laboratório) |
| `Client/Sandbox/CombatPrototype/` | `SandboxFighter` (CharacterBody3D), FSM Idle/Move/Attack/Dodge/HitReact, `InputBuffer`, Hitbox/Hurtbox, `TrainingDummy` |
| `Client/Sandbox/EnvironmentPrototype/AtmosphereV2.cs` | Contraste/saturação, glow, névoa de altura, perspectiva aérea, SSAO (Forward+), cinza no ar |

## Valores iniciais
Input buffer 180 ms · dash 14 m/s por 0,30 s (i-frames 0,02–0,24 s) · hitstop escala 0,08 por 70 ms × peso do golpe ·
combo 3 golpes (10/12/22 de dano, finalizador empurra 8 m/s) · andar 5 m/s, correr 7,25 m/s.

## Limites conhecidos (Fase 1)
- O servidor continua vendo o herói parado onde o laboratório foi ligado (monstros podem bater nele): ligue o Invulnerável.
- Golpes do protótipo só acertam os bonecos de treino (locais), nunca monstros do servidor.
- Trocar de mapa com o combate v2 ligado desliga o protótipo sozinho.

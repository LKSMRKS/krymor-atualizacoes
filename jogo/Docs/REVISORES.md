# Revisores e quando usá-los

Definições em `.claude/agents/` (subagentes do Claude Code) e procedimento de trabalho em
`.claude/skills/desenvolvimento-incremental/SKILL.md`. Eles carregam quando o Claude abre **esta pasta** como projeto.
A fonte versionada fica em `claude-config/`; `Tools\instalar-config-claude.ps1` copia para `.claude/` (edite em `claude-config/` e reinstale).

| Revisor | Acesso | Quando usar | Quando NÃO usar |
|---|---|---|---|
| `revisor-bugs` | só leitura | antes de entregar mudança de risco: autoridade do servidor, itens/loot, banco/migração, protocolo, concorrência; ou bug relatado | ajuste de texto, valor em JSON, só visual |
| `analista-gameplay` | só leitura | ao fechar um incremento jogável; ao mexer em combate, inimigos, mapa, recompensas | correção interna sem efeito no jogo |
| `revisor-desempenho` | leitura + Bash para medir | lentidão relatada; antes de aumentar muito o conteúdo | "por precaução" em mudança comum |

Regras:
- No máximo o revisor que a tarefa pede; nunca os três em mudança pequena.
- O agente principal **confere cada achado no código** (e, se possível, com teste) antes de corrigir. Achado "suspeito"
  não vira correção sem confirmação. Correções seguem a skill de desenvolvimento incremental.
- Achados de gameplay marcados como hipótese só viram mudança depois do teste humano ou com aval do usuário.

## Sem suporte a subagentes
Se o ambiente não tiver subagentes/skills, o agente principal segue o mesmo texto dos arquivos acima como checklist,
em passos separados (primeiro revisar sem editar, depois conferir, depois corrigir), e diz no relatório que a revisão
não foi independente.

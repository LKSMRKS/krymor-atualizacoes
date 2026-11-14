using System;
using System.Linq;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (análise do jogo — mapas: "dungeon precisa de 'pra onde vou? o que faço?'"): ao entrar numa dungeon aparece o
/// objetivo no quadro de missões e uma pedrinha na bússola apontando para o chefe. Vencido o chefe, o objetivo troca para
/// a saída (portal mais perto). Marcador "objetivo|texto" (o cliente mostra o texto; a bússola aponta X/Y).
/// </summary>
public sealed partial class GameServer
{
    private void ObjetivoDaDungeon(Session s)
    {
        if (s.Zone is not { Instanced: true, Layout: { } layout } z || layout.Kind != "dungeon") return;
        var chefe = z.Monsters.Where(m => m.Def.Boss && m.Master is null && m.State.Alive).OrderByDescending(m => m.Level).FirstOrDefault();
        if (chefe is not null)
            SendTo(s, new QuestMarkerMessage($"objetivo|Objetivo: atravesse {layout.DisplayName} e derrote {chefe.Def.SpeciesName} (a bússola aponta)", chefe.SpawnX, chefe.SpawnY, "objetivo"));
        else ObjetivoSaida(s);
    }

    private void ObjetivoSaida(Session s)
    {
        if (s.Zone?.Layout is not { } layout) return;
        var saida = layout.Portals.OrderBy(p => Distance(p.X, p.Y, s.X, s.Y)).FirstOrDefault();
        if (saida is null) return;
        SendTo(s, new QuestMarkerMessage("objetivo|Objetivo: dungeon vencida! Pegue o baú e saia pelo portal (a bússola aponta)", saida.X, saida.Y, "objetivo"));
    }
}

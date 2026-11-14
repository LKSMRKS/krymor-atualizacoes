using System.Linq;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>13/11 (pedido do dono): o Merlin mora em Astrália (2ª cidade nova), na porta do Observatório. Dá 12 missões em 3
/// atos ("O Grande Arranjo"), manda para as 3 dungeons dele (portais dentro do Observatório) e, no fim, leva aos Reinos
/// Ocultos. Conteúdo gerado por Tools/gerar_cidades.py + Tools/gerar_merlin.py.</summary>
public class MerlinTests
{
    private const string Cidade = "astralia";
    private static MapHarness Real() => new(MapHarness.RealWorld());

    private static void Concluir(MapHarness h, int id, string questId)
    {
        var cid = h.CharacterId(id);
        Assert.True(h.Temp.Db.TryAcceptQuest(cid, questId, false));
        h.Temp.Db.AdvanceQuest(cid, questId, 999, 999);
        Assert.True(h.Temp.Db.TryCompleteQuest(cid, questId, 0, null, 0, System.Array.Empty<GrantItem>(), 0).Success);
    }

    [Fact]
    public void Merlin_MoraEmAstralia_NaoNoPorto_ComDozeMissoesEmCadeia()
    {
        var w = MapHarness.RealWorld();
        Assert.DoesNotContain(w.Maps["porto_das_cinzas"].Npcs, n => n.Id == "merlin_mago");
        var m = w.Maps[Cidade].Npcs.Single(n => n.Id == "merlin_mago");
        Assert.Equal("merlin", m.Look);
        Assert.Equal("missoes", m.Role);
        var qs = w.Quests.Values.Where(q => q.Giver == "merlin_mago").OrderBy(q => q.Id).ToList();
        Assert.Equal(12, qs.Count);
        Assert.True(qs.Select(q => q.Kind).Distinct().Count() >= 7);              // conversar, matar, chefe, defender, escoltar, caçar, coletar, visitar
        for (var i = 1; i < qs.Count; i++) Assert.Equal(qs[i - 1].Id, qs[i].Requires);
        Assert.Contains(qs, q => q.RewardFlags.Contains("merlin:floresta"));
        Assert.Equal("merlin:reinos", qs[^1].RewardFlags.Single());
    }

    [Theory]
    [InlineData("merlin_torre_torta", "monster_merlin_reflexo")]
    [InlineData("merlin_poco_estrelas", "monster_merlin_visitante_caido")]
    [InlineData("merlin_sonho_dragao", "monster_merlin_dragao_sonho")]
    public void DungeonsDoMerlin_SaoInstanciadas_TemChefe_EVoltamParaATorre(string mapa, string chefe)
    {
        var d = MapHarness.RealWorld().Maps[mapa];
        Assert.True(d.Instanced);
        Assert.Equal(chefe, d.FirstClearBoss);
        Assert.Contains(d.Spawns, s => s.Monster == chefe);
        Assert.All(d.Portals, p => Assert.Equal("torre_merlin", p.ToMap));
        Assert.Contains(MapHarness.RealWorld().Regions, r => r.Id == "astralia" && r.Maps.Contains(mapa));
    }

    [Fact]
    public void PortaDoObservatorio_LevaATorre_EOPortalSoAbreDepoisDeAceitarAMissao()
    {
        using var h = Real();
        var porta = h.World.Maps[Cidade].Portals.Single(p => p.Id == "astralia_observatorio");
        Assert.Equal("torre_merlin", porta.ToMap);
        var id = h.Join("ana", level: 22, map: Cidade, x: porta.X, y: porta.Y - 1.6);
        h.Transport.ClientSends(id, new InteractIntent("obj:porta_observatorio"));   // F na porta da torre
        Assert.True(h.TickUntil(() => h.Last<MapMessage>(id).Map.Id == "torre_merlin", 20));

        var portal = h.World.Maps["torre_merlin"].Portals.Single(p => p.ToMap == "merlin_torre_torta");
        id = h.Relocate(id, "torre_merlin", portal.X, portal.Y + 2.2);
        h.Transport.ClientSends(id, new MoveIntent(0, -1));
        h.Ticks(30);
        Assert.Equal("torre_merlin", h.Last<MapMessage>(id).Map.Id);            // sem a missão: portal apagado

        foreach (var q in new[] { "q_merlin_01", "q_merlin_02", "q_merlin_03" }) Concluir(h, id, q);
        var (mx, my) = h.PertoDoNpc("merlin_mago", Cidade);
        id = h.Relocate(id, Cidade, mx, my);
        h.Transport.ClientSends(id, new QuestIntent("q_merlin_04", "accept"));
        h.Ticks(2);
        Assert.Contains("merlin:dg1", h.Temp.Db.GetFlags(h.CharacterId(id)));
        id = h.Relocate(id, "torre_merlin", portal.X, portal.Y + 2.2);
        h.Transport.ClientSends(id, new MoveIntent(0, -1));
        Assert.True(h.TickUntil(() => h.Last<MapMessage>(id).Map.Id == "merlin_torre_torta", 40));
    }

    [Fact]
    public void Merlin_SoLevaAosReinos_DepoisDasQuatroPortas()
    {
        using var h = Real();
        var (mx, my) = h.PertoDoNpc("merlin_mago", Cidade);
        var id = h.Join("leo", level: 35, map: Cidade, x: mx, y: my);
        h.Temp.Db.AddCoins(h.CharacterId(id), 5000, "t:c");
        id = h.Relocate(id, Cidade, mx, my);
        h.Transport.ClientSends(id, new PaidTravelIntent("merlin_mago", "merlin_reino_lua"));
        h.Ticks(1);
        Assert.Equal("unknown_destination", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "merlin:reinos");
        id = h.Relocate(id, Cidade, mx, my);
        h.Transport.ClientSends(id, new PaidTravelIntent("merlin_mago", "merlin_reino_lua"));
        h.Ticks(2);
        Assert.Equal("reino_lua", h.Last<MapMessage>(id).Map.Id);
    }
}

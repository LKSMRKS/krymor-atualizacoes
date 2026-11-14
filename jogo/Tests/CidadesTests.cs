using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>13/11 (pedido do dono): três cidades de interação entre os mapas — Vila das Mós (Vale↔Mata), Astrália
/// (Cânions↔Brejo) e Brasaforte (Geada↔Escória). Conteúdo gerado por Tools/gerar_cidades.py.</summary>
public class CidadesTests
{
    private static MapHarness Real() => new(MapHarness.RealWorld());

    [Theory]
    [InlineData("vila_das_mos", "costa_do_cal", "vale_das_cinzas", "loja_vila_mapas")]
    [InlineData("astralia", "mata_dos_enforcados", "canions_suplicantes", "loja_astralia_alfaiate")]
    [InlineData("candeia", "brejo_da_viuva", "vespera", "loja_candeia_boticario")]
    [InlineData("brasaforte", "coroa_da_geada", "mar_de_escoria", "loja_brasaforte_forja")]
    public void Cidade_TemGente_LojaPropria_PedraDeViagem_ESaidaParaAsDuasRegioes(string cidade, string campoA, string campoB, string loja)
    {
        var w = MapHarness.RealWorld();
        var m = w.Maps[cidade];
        Assert.Equal("cidade", m.Kind);
        Assert.Empty(m.Spawns);                                                   // cidade de interação, sem monstro
        Assert.True(m.Npcs.Count >= 6);
        Assert.Contains(m.Npcs, n => n.Shop == loja);
        Assert.Single(m.Waypoints);
        foreach (var campo in new[] { campoA, campoB })
        {
            Assert.Contains(m.Portals, p => p.ToMap == campo);                     // da cidade para o campo
            Assert.Contains(w.Maps[campo].Portals, p => p.ToMap == cidade);         // e do campo para a cidade
        }
        Assert.Contains(w.Regions, r => r.Id == cidade && r.Maps.Contains(cidade));
        Assert.Contains(w.Quests.Values, q => m.Npcs.Any(n => n.Id == q.Giver));   // missões locais
    }

    [Fact]
    public void DoValeParaAVila_PeloPortalDoCampo()
    {
        using var h = Real();
        var portal = h.World.Maps["vale_das_cinzas"].Portals.Single(p => p.ToMap == "vila_das_mos");
        var dx = portal.X > 0 ? -1 : 1;
        var id = h.Join("ana", level: 14, map: "vale_das_cinzas", x: portal.X + dx * 3.2, y: portal.Y);
        h.Transport.ClientSends(id, new MoveIntent(-dx, 0));
        Assert.True(h.TickUntil(() => h.Last<MapMessage>(id).Map.Id == "vila_das_mos", 60));
    }

    /// <summary>13/11 22:26 (dono): 4 cidades nos níveis 10, 20, 35 e 50, cada uma no caminho entre os dois campos da faixa.</summary>
    [Fact]
    public void QuatroCidades_NosNiveis10_20_35_50_ComMissoesPerto()
    {
        var w = MapHarness.RealWorld();
        foreach (var (cid, nivel) in new[] { ("vila_das_mos", 10), ("astralia", 20), ("candeia", 35), ("brasaforte", 50) })
        {
            var npcs = w.Maps[cid].Npcs.Select(n => n.Id).ToHashSet();
            var locais = w.Quests.Values.Where(q => npcs.Contains(q.Giver) && q.Id.StartsWith("q_cid_")).ToList();
            Assert.NotEmpty(locais);
            Assert.All(locais, q => Assert.InRange(q.MinLevel, nivel - 8, nivel + 12));
        }
    }
}

using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11: mesa de Vinte-e-Um no 2º andar do casarão (crupiê Dado; libera com a missão "Convite para a Mesa").</summary>
public class VinteUmMesaTests
{
    [Fact]
    public void SemConvite_NaoJoga_ComConvite_ApostaEPaga()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("ana", level: 10, map: "casarao_andar2", x: 3.8, y: 3.0);
        var id = h.CharacterId(a);
        h.Temp.Db.AddCoins(id, 1000, "teste:moedas");
        h.Ticks(2);
        h.Transport.ClientSends(a, new VinteUmIntent("dado_crupie", "apostar", 20));
        h.Ticks(2);
        var s0 = h.Last<VinteUmStateMessage>(a);
        Assert.NotNull(s0.Error);
        Assert.Empty(s0.Hands);

        h.Temp.Db.TryAddFlag(id, "vinteum");
        a = h.Relocate(a, "casarao_andar2", 3.8, 3.0);
        h.Ticks(2);
        var before = h.Temp.Db.GetCoins(id);
        h.Transport.ClientSends(a, new VinteUmIntent("dado_crupie", "apostar", 20));
        h.Ticks(2);
        var st = h.Last<VinteUmStateMessage>(a);
        Assert.Null(st.Error);
        Assert.Single(st.Hands);
        for (var i = 0; i < 5 && !h.Last<VinteUmStateMessage>(a).Finished; i++)
        {
            h.Transport.ClientSends(a, new VinteUmIntent("dado_crupie", "parar"));
            h.Ticks(2);
        }
        var fim = h.Last<VinteUmStateMessage>(a);
        Assert.True(fim.Finished);
        Assert.DoesNotContain("??", fim.Dealer);
        Assert.Equal(before + fim.Profit, h.Temp.Db.GetCoins(id));
    }

    [Fact]
    public void Missao_DoDado_EstaNoConteudo()
    {
        var w = MapHarness.RealWorld();
        Assert.True(w.Quests.TryGetValue("q_mesa_vinteum", out var q));
        Assert.Equal(8, q!.MinLevel);
        Assert.Contains(w.Maps["casarao_andar2"].Npcs, n => n.Id == "dado_crupie" && n.Role == "vinteum");
    }
}

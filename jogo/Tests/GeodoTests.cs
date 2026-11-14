using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11 ("o monstro do geodo não dropou nada"): derrubar o Caranguejo-Geodo deixa itens no chão.</summary>
public class GeodoTests
{
    [Fact]
    public void Geodo_AoMorrer_DeixaItensNoChao()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("geo", "monster_r1_caranguejo_geodo", 0, 3, level: 5, group: "solo") });
        using var h = new MapHarness(w);
        var a = h.Join("ana", x: 0, y: 0, level: 30);
        h.Ticks(2);
        for (var i = 0; i < 400 && h.Monster(a, "m_geo")?.Alive == true; i++)
        {
            var cur = h.Monster(a, "m_geo")!;
            var me = h.Me(a);
            // por trás (a frente da casca toma 15%): anda para o lado oposto ao que ele olha
            a = h.Relocate(a, "teste", cur.X - cur.FacingX * 1.6, cur.Y - cur.FacingY * 1.6);
            h.Face(a, cur.X - h.Me(a).X, cur.Y - h.Me(a).Y);
            h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_corte", null));
            h.Ticks(12);
        }
        Assert.False(h.Monster(a, "m_geo")!.Alive, "o geodo morre");
        h.Ticks(2);
        Assert.NotEmpty(h.LastSnapshot(a).Items);
    }
}

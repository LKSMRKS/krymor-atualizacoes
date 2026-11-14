using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11 (escada de verdade do casarão): sobe a escada andando até o topo, troca para o 2º andar, desce pelo vão e volta ao pé da escada.</summary>
public class EscadaCasaraoTests
{
    private string WalkUntilMap(MapHarness h, int id, double dx, double dy, string map)
    {
        h.TickUntil(() =>
        {
            h.Transport.ClientSends(id, new MoveIntent(dx, dy));
            h.Ticks(1);
            return h.Transport.Received<MapMessage>(id).LastOrDefault()?.Map.Id == map;
        }, 300);
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Ticks(2);
        return h.Transport.Received<MapMessage>(id).LastOrDefault()?.Map.Id ?? "";
    }

    [Fact]
    public void Casarao_SobeEDescePelaEscada()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("escadeiro", x: 13.5, y: -12.05, level: 3, map: "porto_das_cinzas");
        Assert.Equal("casarao_andar2", WalkUntilMap(h, id, 0, -1, "casarao_andar2"));
        Assert.InRange(h.Me(id).X, -7.0, -6.0);
        Assert.Equal("porto_das_cinzas", WalkUntilMap(h, id, 0, 1, "porto_das_cinzas"));
        Assert.InRange(h.Me(id).Y, -13.4, -12.3);
    }
}

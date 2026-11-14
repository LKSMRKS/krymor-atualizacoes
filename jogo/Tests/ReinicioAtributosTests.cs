using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11: Sibila (Porto) devolve os pontos gastos, cobrando moedas; presentes e iniciais ficam.</summary>
public class ReinicioAtributosTests
{
    private const string Porto = "porto_das_cinzas";

    [Fact]
    public void Sibila_DevolvePontosGastos_ECobra()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("bia", x: 17, y: 12, level: 5, map: Porto);
        var ch = h.CharacterId(id);
        h.Temp.Db.AddStatPoints(ch, 5);
        var (p0, v0, pts0) = h.Temp.Db.GetStats(ch);
        h.Transport.ClientSends(id, new SpendStatIntent("primary"));
        h.Transport.ClientSends(id, new SpendStatIntent("primary"));
        h.Transport.ClientSends(id, new SpendStatIntent("vigor"));
        h.Ticks(1);
        Assert.Equal((p0 + 2, v0 + 1, pts0 - 3), h.Temp.Db.GetStats(ch));

        // sem moedas: não mexe em nada
        h.Transport.ClientSends(id, new PaidTravelIntent("sibila_vidente", "reset_atributos"));
        h.Ticks(1);
        Assert.Equal("not_enough_coins", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        Assert.Equal((p0 + 2, v0 + 1, pts0 - 3), h.Temp.Db.GetStats(ch));

        h.Temp.Db.AddCoins(ch, 500, "t:c");
        id = h.Relocate(id, Porto, 17, 12);
        h.Transport.ClientSends(id, new PaidTravelIntent("sibila_vidente", "reset_atributos"));
        h.Ticks(1);
        Assert.Equal((p0, v0, pts0), h.Temp.Db.GetStats(ch));
        Assert.True(h.Temp.Db.GetCoins(ch) < 500);

        // de novo: nada para devolver, não cobra
        var coins = h.Temp.Db.GetCoins(ch);
        h.Transport.ClientSends(id, new PaidTravelIntent("sibila_vidente", "reset_atributos"));
        h.Ticks(1);
        Assert.Equal("nothing", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        Assert.Equal(coins, h.Temp.Db.GetCoins(ch));
    }
}

using System.Linq;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11 (social): troca entre jogadores — pede, aceita, põe item e moedas, os dois confirmam, tudo muda de dono.</summary>
public class TrocaTests
{
    [Fact]
    public void Troca_ItemPorMoedas_SoComAsDuasConfirmacoes()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("vendedor", x: 0, y: 0, level: 5, map: "porto_das_cinzas");
        var b = h.Join("comprador", x: 1.5, y: 0, level: 5, map: "porto_das_cinzas");
        var ca = h.CharacterId(a); var cb = h.CharacterId(b);
        var item = h.Temp.Db.CreateInventoryItem(ca, "anel_cobre", "comum", 1, "t:anel")!;
        h.Temp.Db.AddCoins(cb, 500, "t:moedas");
        a = h.Relocate(a, "porto_das_cinzas", 0, 0); b = h.Relocate(b, "porto_das_cinzas", 1.5, 0);

        h.Transport.ClientSends(a, new TradeIntent("pedir", Target: cb)); h.Ticks(1);
        Assert.Equal("pedido", h.Transport.Received<TradeStateMessage>(b).Last().State);
        h.Transport.ClientSends(b, new TradeIntent("aceitar")); h.Ticks(1);
        h.Transport.ClientSends(a, new TradeIntent("item", ItemId: item)); h.Ticks(1);
        h.Transport.ClientSends(b, new TradeIntent("moedas", Coins: 300)); h.Ticks(1);
        Assert.Single(h.Transport.Received<TradeStateMessage>(b).Last().Theirs);
        h.Transport.ClientSends(a, new TradeIntent("confirmar")); h.Ticks(1);
        Assert.Equal("vendedor", h.Temp.Db.GetItemLocation(item)!.OwnerCharacterId == ca ? "vendedor" : "comprador");   // ainda não
        h.Transport.ClientSends(b, new TradeIntent("confirmar")); h.Ticks(1);

        Assert.Equal(cb, h.Temp.Db.GetItemLocation(item)!.OwnerCharacterId);
        Assert.Equal(300, h.Temp.Db.GetCoins(ca));
        Assert.Equal(200, h.Temp.Db.GetCoins(cb));
        Assert.Equal("fechada", h.Transport.Received<TradeStateMessage>(a).Last().State);
    }
}

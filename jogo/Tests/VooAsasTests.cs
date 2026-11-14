using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11: voo com asas — passa por cima de rocha/muro, não da borda; monstros ignoram; atacar pousa.</summary>
public class VooAsasTests
{
    private const string Costa = "costa_do_cal";

    [Fact]
    public void Voo_PassaPorCimaDoMuro_EAtacarPousa()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("bia", x: -10, y: 6, level: 30, map: Costa);
        var ch = h.CharacterId(id);
        h.Temp.Db.CreateInventoryItem(ch, "wings_ferro", "comum", 1, "t:asa");
        id = h.Relocate(id, Costa, -10, 6);
        var wing = h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == "wings_ferro").ItemId;
        h.Transport.ClientSends(id, new EquipIntent(wing));
        h.Ticks(2);
        // a pé o muro (y = 3,2) segura
        for (var i = 0; i < 30; i++) { h.Transport.ClientSends(id, new MoveIntent(0, -1)); h.Ticks(1); }
        Assert.True(h.Me(id).Y > 3.2);
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Transport.ClientSends(id, new FlyIntent(true));
        h.Ticks(2);
        Assert.True(h.Me(id).Flying);
        for (var i = 0; i < 30; i++) { h.Transport.ClientSends(id, new MoveIntent(0, -1)); h.Ticks(1); }
        Assert.True(h.Me(id).Y < 2.0, $"y={h.Me(id).Y}");   // passou por cima
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Transport.ClientSends(id, new CastIntent("skill_guerreiro_corte", null));
        h.Ticks(2);
        Assert.False(h.Me(id).Flying);
    }
}

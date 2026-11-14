using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11: Pedra de Portal do Téo — viaja de qualquer lugar (nível 7+) para pedras já descobertas; vinculada.</summary>
public class PedraPortalTests
{
    private const string Porto = "porto_das_cinzas";

    [Fact]
    public void PedraDePortal_ViajaDeLonge_EEhVinculada()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("bia", x: 10, y: 10, level: 6, map: Porto);
        var ch = h.CharacterId(id);
        h.Temp.Db.TryAddFlag(ch, "wp:wp_dunas");
        h.Temp.Db.CreateInventoryItem(ch, "portal_viagem", "comum", 1, "t:portal");
        id = h.Relocate(id, Porto, 10, 10);
        h.Transport.ClientSends(id, new TravelIntent("wp_dunas"));
        h.Ticks(1);
        Assert.Equal("not_at_waypoint", h.Transport.Received<ActionResultMessage>(id).Last().Reason);   // nível 6

        id = h.Join("ana", x: 10, y: 10, level: 7, map: Porto);
        ch = h.CharacterId(id);
        h.Temp.Db.TryAddFlag(ch, "wp:wp_dunas");
        h.Temp.Db.CreateInventoryItem(ch, "portal_viagem", "comum", 1, "t:portal2");
        id = h.Relocate(id, Porto, 10, 10);
        // 11/11 noite: nível 7 sem a missão do Téo ainda não vale
        h.Transport.ClientSends(id, new TravelIntent("wp_dunas"));
        h.Ticks(1);
        Assert.Equal("not_at_waypoint", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        Assert.True(h.Temp.Db.TryAcceptQuest(ch, "q_pedra_portal", false));
        id = h.Relocate(id, Porto, 10, 10);
        h.Transport.ClientSends(id, new TravelIntent("wp_dunas"));
        h.Ticks(2);
        Assert.Equal("costa_do_cal", h.Last<MapMessage>(id).Map.Id);

        var stone = h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == "portal_viagem");
        h.Transport.ClientSends(id, new DropItemIntent(stone.ItemId));
        h.Ticks(1);
        Assert.Equal("bound", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
    }
}

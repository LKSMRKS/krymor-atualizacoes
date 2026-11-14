using System;
using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>11/11 item 51: asas são item no espaço "asas" (vestir mostra, tirar some) e dão defesa.</summary>
public class AsasItemTests
{
    [Fact]
    public void Asas_VesteNoEspacoDeAsas_ETiraSome()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("vo", x: 0, y: -9.5, level: 30);
        h.Ticks(2);
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), "wings_ferro", "comum", 1, "t:" + Guid.NewGuid())!;
        h.Transport.ClientSends(id, new EquipIntent(item));
        h.Ticks(2);
        Assert.Contains(h.Temp.Db.GetEquipment(h.CharacterId(id)), e => e.Slot == "asas" && e.ItemId == item);
        Assert.Equal("ferro", h.Me(id).Wings);
        h.Transport.ClientSends(id, new UnequipIntent("asas"));
        h.Ticks(2);
        Assert.Null(h.Me(id).Wings);
    }
}

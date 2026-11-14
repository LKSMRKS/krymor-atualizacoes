using System.Linq;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11 (armadura em 5 partes, como no MU): a calça vai no espaço "calca", aparece no boneco e soma os atributos.</summary>
public class CincoPartesTests
{
    [Fact]
    public void Calca_VaiNoEspacoProprio_ESomaAtributos()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("calceiro", x: 0, y: 0, level: 25, map: "porto_das_cinzas");
        var ch = h.CharacterId(id);
        var calca = h.Temp.Db.CreateInventoryItem(ch, "arm_cavaleiro_calca", "comum", 1, "t:calca")!;
        id = h.Relocate(id, "porto_das_cinzas", 0, 0);
        var before = h.Me(id).MaxHealth;
        h.Transport.ClientSends(id, new EquipIntent(calca)); h.Ticks(2);
        Assert.Equal("calca", h.Temp.Db.GetItemLocation(calca)!.Slot);
        Assert.Equal("arm_cavaleiro_calca", h.Me(id).Pants);
        Assert.True(h.Me(id).MaxHealth > before);
    }
}

using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>10/11: a parede rachada só cai com a carga da Nara; a ponte só fica de pé com 6 tábuas, e cada um constrói a sua.</summary>
public class MuroEPonteTests
{
    private const string Costa = "costa_do_cal";

    [Fact]
    public void ParedeRachada_SoCaiComACarga()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("nara", x: -30, y: 6.2, level: 5, map: Costa);
        var cid = h.CharacterId(id);
        h.Transport.ClientSends(id, new InteractIntent("lever:racha_muro"));
        h.Ticks(1);
        Assert.DoesNotContain("parede:explodida", h.Temp.Db.GetFlags(cid));
        h.Temp.Db.TryAddFlag(cid, "explosivo:parede");
        id = h.Relocate(id, Costa, -30, 6.2);
        h.Transport.ClientSends(id, new InteractIntent("lever:racha_muro"));
        h.Ticks(1);
        // 11/11: só a missão não basta — precisa trazer o barril (BarrilMuroTests)
        Assert.Equal("need_keg", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        Assert.DoesNotContain("parede:explodida", h.Temp.Db.GetFlags(cid));
    }

    [Fact]
    public void Ponte_PrecisaDe6Tabuas_EGasta()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("lia", x: 0, y: -51.2, level: 6, map: Costa);
        var cid = h.CharacterId(id);
        h.Transport.ClientSends(id, new InteractIntent("lever:ponte_ravina"));
        h.Ticks(1);
        Assert.DoesNotContain("ponte:ravina", h.Temp.Db.GetFlags(cid));
        for (var i = 0; i < 6; i++) h.Temp.Db.CreateInventoryItem(cid, "mat_tabua", "comum", 1, "t" + i);
        h.Transport.ClientSends(id, new InteractIntent("lever:ponte_ravina"));
        h.Ticks(1);
        Assert.Contains("ponte:ravina", h.Temp.Db.GetFlags(cid));
        Assert.Equal(0, h.Temp.Db.CountInInventory(cid, "mat_tabua"));
    }
}

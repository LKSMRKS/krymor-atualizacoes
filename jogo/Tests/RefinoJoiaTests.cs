using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11: refino estilo MU com joias; Excelente com 3 afixos fortes.</summary>
public class RefinoJoiaTests
{
    private const string Porto = "porto_das_cinzas";

    [Fact]
    public void JoiaDaMare_SobeAteMais6_DepoisPedeBrasa()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("bia", "guerreiro", x: 2, y: -3, level: 10, map: Porto);
        var ch = h.CharacterId(id);
        h.Temp.Db.AddCoins(ch, 50000, "t:c");
        h.Temp.Db.CreateInventoryItem(ch, "joia_mare", "comum", 7, "t:j");
        h.Temp.Db.CreateInventoryItem(ch, "armor_cota_de_malha", "comum", 1, "t:a");
        id = h.Relocate(id, Porto, 2, -3);
        string Armor() => h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == "armor_cota_de_malha").ItemId;
        string? Jewel() => h.Temp.Db.GetInventory(ch).FirstOrDefault(e => e.DefinitionId == "joia_mare")?.ItemId;
        for (var i = 0; i < 6; i++) { h.Transport.ClientSends(id, new RefineIntent(Jewel()!, Armor())); h.Ticks(1); }
        Assert.Equal(6, h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == "armor_cota_de_malha").Temper);
        h.Transport.ClientSends(id, new RefineIntent(Jewel()!, Armor()));
        h.Ticks(1);
        Assert.Equal("wrong_jewel", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        Assert.Equal(1, h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == "joia_mare").Quantity);
        Assert.Equal(6, h.Transport.Received<InventoryMessage>(id).Last().Items.First(e => e.DefinitionId == "armor_cota_de_malha").Plus);
    }

    [Fact]
    public void Refino_RegrasDoMU()
    {
        Assert.Equal("joia_mare", Refine.JewelFor(5));
        Assert.Equal("joia_brasa", Refine.JewelFor(6));
        Assert.Equal("joia_vazio", Refine.JewelFor(9));
        Assert.Equal(1.0, Refine.Chance(6));
        Assert.Equal(6, Refine.OnFail(6));
        Assert.Equal(7, Refine.OnFail(8));
        Assert.Equal(9, Refine.OnFail(10));
        Assert.Equal(3, ItemAffixes.CountFor("excelente"));
    }
}

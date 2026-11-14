using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11: armadura em peças, conjunto de uma classe só, dois anéis, capa no espaço de asas.</summary>
public class ConjuntosEmPecasTests
{
    private const string Porto = "porto_das_cinzas";

    [Fact]
    public void Conjunto_SoDaClasse_AneisEmDoisEspacos_CapaNasAsas()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("bia", "guerreiro", x: 2, y: -3, level: 25, map: Porto);
        var ch = h.CharacterId(id);
        h.Temp.Db.CreateInventoryItem(ch, "arm_cavaleiro_luvas", "comum", 1, "t:1");
        h.Temp.Db.CreateInventoryItem(ch, "arm_elfico_botas", "comum", 1, "t:2");
        h.Temp.Db.CreateInventoryItem(ch, "anel_cobre", "comum", 1, "t:3");
        h.Temp.Db.CreateInventoryItem(ch, "anel_mare", "comum", 1, "t:4");
        h.Temp.Db.CreateInventoryItem(ch, "capa_viajante", "comum", 1, "t:5");
        id = h.Relocate(id, Porto, 2, -3);
        string Item(string def) => h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == def).ItemId;
        foreach (var def in new[] { "arm_cavaleiro_luvas", "arm_elfico_botas", "anel_cobre", "anel_mare", "capa_viajante" })
        {
            h.Transport.ClientSends(id, new EquipIntent(Item(def)));
            h.Ticks(2);
        }
        var eq = h.Temp.Db.GetEquipment(ch).ToDictionary(e => e.Slot, e => e.DefinitionId);
        Assert.Equal("arm_cavaleiro_luvas", eq["luvas"]);
        Assert.False(eq.ContainsKey("botas"));                 // botas élficas são do Arqueiro
        Assert.Equal("anel_cobre", eq["anel1"]);
        Assert.Equal("anel_mare", eq["anel2"]);
        Assert.Equal("capa_viajante", eq["asas"]);
        Assert.Equal("arm_cavaleiro:luvas", h.Content.Items[h.Me(id).Gloves!].ModelAssetId);
    }
}

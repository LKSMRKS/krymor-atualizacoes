using System.Linq;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11: Máquina do Caos (MU) no Porto — 4 Joias da Maré viram 1 da Brasa (80%); falhou, perde as joias.</summary>
public class MaquinaCaosTests
{
    [Fact]
    public void MaquinaDoCaos_TransformaJoiasComChance()
    {
        using var h = new MapHarness(MapHarness.RealWorld(), new System.Random(7).NextDouble);
        var (cx, cy) = h.PertoDoObjeto("porto_caos");
        var id = h.Join("alquimista", x: cx, y: cy, level: 12, map: "porto_das_cinzas");
        var ch = h.CharacterId(id);
        h.Temp.Db.CreateInventoryItem(ch, "joia_mare", "comum", 40, "t:joias");
        id = h.Relocate(id, "porto_das_cinzas", cx, cy);
        for (var i = 0; i < 10; i++) { h.Transport.ClientSends(id, new CraftIntent("r" + i, "porto_caos", "caos_joia_brasa")); h.Ticks(1); }
        Assert.Equal(0, h.Temp.Db.CountInInventory(ch, "joia_mare"));
        var brasa = h.Temp.Db.CountInInventory(ch, "joia_brasa");
        Assert.InRange(brasa, 1, 10);
    }
}

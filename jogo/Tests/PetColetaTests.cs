using System.Linq;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>11/11: pet junta material/consumível do chão (até 5 m do dono); equipamento não; o que o dono jogou fora não.</summary>
public class PetColetaTests
{
    [Fact]
    public void Pet_PegaMaterialPerto_NaoPegaEquipamento_NemOQueODonoJogouFora()
    {
        using var h = new MapHarness();
        var sx = h.World.Map.SpawnX; var sy = h.World.Map.SpawnY;
        var dono = h.Join("dono", x: sx, y: sy);
        var outro = h.Join("outro", x: sx + 1.5, y: sy);
        var cdono = h.CharacterId(dono); var coutro = h.CharacterId(outro);
        h.Temp.Db.TryAddFlag(cdono, "pet:vagalume");
        h.Temp.Db.TryAddFlag(cdono, "petsel:vagalume");
        dono = h.Relocate(dono, h.World.Map.Id, sx, sy);
        h.Ticks(2);

        var mat = h.Temp.Db.CreateInventoryItem(coutro, "mat_retalho", "comum", 1, "t:mat")!;
        var arma = h.Temp.Db.CreateInventoryItem(coutro, "weapon_guerreiro_sword_p1", "comum", 1, "t:arma")!;
        var meu = h.Temp.Db.CreateInventoryItem(cdono, "mat_casca_cal", "comum", 1, "t:meu")!;
        h.Transport.ClientSends(outro, new DropItemIntent(mat));
        h.Transport.ClientSends(outro, new DropItemIntent(arma));
        h.Transport.ClientSends(dono, new DropItemIntent(meu));
        h.Ticks(60);

        var inv = h.Temp.Db.GetInventory(cdono);
        Assert.Contains(inv, e => e.DefinitionId == "mat_retalho");
        Assert.DoesNotContain(inv, e => e.DefinitionId == "weapon_guerreiro_sword_p1");
        Assert.DoesNotContain(inv, e => e.ItemId == meu);
    }
}

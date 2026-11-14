using System.Linq;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>10/11: companheiro é item — equipado no espaço "pet" ele vem junto; tirou, ele vai embora.</summary>
public class PetItemTests
{
    [Fact]
    public void PetAntigo_ViraItem_EOEspacoPetMandaNoCompanheiro()
    {
        using var h = new MapHarness();
        var id = h.Login(h.Precreate("pipa"));
        var cid = h.CharacterId(id);
        h.Temp.Db.TryAddFlag(cid, "pet:vagalume");
        h.Temp.Db.TryAddFlag(cid, "petsel:vagalume");
        var again = h.Relocate(id, h.World.Map.Id, h.World.Map.SpawnX, h.World.Map.SpawnY);
        h.Ticks(1);
        var pet = Assert.Single(h.Temp.Db.GetEquipment(cid), e => e.Slot == GameServer.PetSlot);
        Assert.Equal("pet_vagalume", pet.DefinitionId);
        Assert.Equal("vagalume", h.Last<CompanionsMessage>(again).ActivePet);

        h.Transport.ClientSends(again, new UnequipIntent(GameServer.PetSlot));
        h.Ticks(1);
        Assert.Null(h.Last<CompanionsMessage>(again).ActivePet);
        Assert.Contains(h.Temp.Db.GetInventory(cid), e => e.ItemId == pet.ItemId);

        h.Transport.ClientSends(again, new EquipIntent(pet.ItemId));
        h.Ticks(1);
        Assert.Equal("vagalume", h.Last<CompanionsMessage>(again).ActivePet);

        // o item só é dado uma vez
        var third = h.Relocate(again, h.World.Map.Id, h.World.Map.SpawnX, h.World.Map.SpawnY);
        h.Ticks(1);
        Assert.Single(h.Temp.Db.GetEquipment(cid).Concat<object>(h.Temp.Db.GetInventory(cid)).Where(e => e.ToString()!.Contains("pet_vagalume")));
    }
}

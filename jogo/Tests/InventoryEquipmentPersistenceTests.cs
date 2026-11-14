namespace CinzasDaMare.Tests;

/// <summary>Equipar, consumir e criar itens no inventário: regras de banco (SQLite real, sem rede).</summary>
public class InventoryEquipmentPersistenceTests
{
    private const string Sword = "weapon_guerreiro_sword_p1";
    private const string Tonic = "consumable_c01_tonico_fraco";

    [Fact]
    public void CriarItemNoInventario_UsaOMenorSlotLivre_E_RespeitaACapacidade()
    {
        using var t = new TempDb(inventoryCapacity: 2);
        var c = t.NewCharacter("A");

        var first = t.Db.CreateInventoryItem(c, Tonic, "comum", 3, "e1");
        var second = t.Db.CreateInventoryItem(c, Sword, "comum", 1, "e2");
        var third = t.Db.CreateInventoryItem(c, Sword, "comum", 1, "e3");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(third);
        var inventory = t.Db.GetInventory(c);
        Assert.Equal(new[] { 0, 1 }, inventory.Select(i => i.Slot).ToArray());
        Assert.Equal(3, inventory.Single(i => i.DefinitionId == Tonic).Quantity);
        Assert.Equal(2, t.Db.CountTransactionLog("grant_item"));
    }

    [Fact]
    public void ConsumirUmaUnidade_Decrementa_E_ApagaAInstanciaNaUltima()
    {
        using var t = new TempDb();
        var c = t.NewCharacter("A");
        var stack = t.Db.CreateInventoryItem(c, Tonic, "comum", 2, "e1")!;

        var first = t.Db.TryConsumeOne(c, stack);
        Assert.True(first.Success);
        Assert.Equal(1, first.Remaining);
        Assert.Equal(Tonic, first.DefinitionId);
        Assert.Equal(1, t.Db.GetInventory(c).Single().Quantity);

        var last = t.Db.TryConsumeOne(c, stack);
        Assert.True(last.Success);
        Assert.Equal(0, last.Remaining);
        Assert.Empty(t.Db.GetInventory(c));
        Assert.Null(t.Db.GetItemLocation(stack));

        Assert.Equal("not_in_inventory", t.Db.TryConsumeOne(c, stack).Reason);
        Assert.Equal(2, t.Db.CountTransactionLog("consume_item"));
    }

    [Fact]
    public void ConsumirItemDeOutroPersonagem_EhRejeitado_SemAlterarNada()
    {
        using var t = new TempDb();
        var owner = t.NewCharacter("Dono");
        var thief = t.NewCharacter("Ladrao");
        var stack = t.Db.CreateInventoryItem(owner, Tonic, "comum", 2, "e1")!;

        Assert.Equal("not_in_inventory", t.Db.TryConsumeOne(thief, stack).Reason);
        Assert.Equal(2, t.Db.GetInventory(owner).Single().Quantity);
    }

    [Fact]
    public async Task ConsumoConcorrente_NuncaConsomeMaisDoQueExiste()
    {
        using var t = new TempDb();
        var c = t.NewCharacter("A");
        var stack = t.Db.CreateInventoryItem(c, Tonic, "comum", 3, "e1")!;

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () => { await gate.Task; return t.Db.TryConsumeOne(c, stack); }))
            .ToList();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(3, results.Count(r => r.Success));
        Assert.All(results.Where(r => !r.Success), r => Assert.Equal("not_in_inventory", r.Reason));
        Assert.Empty(t.Db.GetInventory(c));
    }

    [Fact]
    public void Equipar_MoveDoInventarioParaOSlot_E_Desequipar_Devolve()
    {
        using var t = new TempDb();
        var c = t.NewCharacter("A");
        var sword = t.Db.CreateInventoryItem(c, Sword, "comum", 1, "e1")!;

        var equip = t.Db.TryEquip(c, sword, "weapon_main");

        Assert.True(equip.Success);
        Assert.Null(equip.ReplacedItemId);
        Assert.Empty(t.Db.GetInventory(c));
        var equipped = Assert.Single(t.Db.GetEquipment(c));
        Assert.Equal("weapon_main", equipped.Slot);
        Assert.Equal(sword, equipped.ItemId);
        Assert.Equal("equipment", t.Db.GetItemLocation(sword)!.Kind);

        var unequip = t.Db.TryUnequip(c, "weapon_main");
        Assert.True(unequip.Success);
        Assert.Empty(t.Db.GetEquipment(c));
        Assert.Equal(sword, Assert.Single(t.Db.GetInventory(c)).ItemId);
    }

    [Fact]
    public void Equipar_NoSlotOcupado_Troca_SemDuplicarNemPerder()
    {
        using var t = new TempDb();
        var c = t.NewCharacter("A");
        var first = t.Db.CreateInventoryItem(c, Sword, "comum", 1, "e1")!;   // slot 0
        var second = t.Db.CreateInventoryItem(c, Sword, "comum", 1, "e2")!;  // slot 1
        Assert.True(t.Db.TryEquip(c, first, "weapon_main").Success);

        var swap = t.Db.TryEquip(c, second, "weapon_main");

        Assert.True(swap.Success);
        Assert.Equal(first, swap.ReplacedItemId);
        Assert.Equal(second, Assert.Single(t.Db.GetEquipment(c)).ItemId);
        var inventory = Assert.Single(t.Db.GetInventory(c));
        Assert.Equal(first, inventory.ItemId);
        Assert.Equal(1, inventory.Slot); // ocupou o lugar do item que subiu
        Assert.Equal(2, t.Db.GetInventory(c).Select(i => i.ItemId)
            .Concat(t.Db.GetEquipment(c).Select(e => e.ItemId)).Distinct().Count());
    }

    [Fact]
    public void Equipar_ItemQueNaoEstaNoInventario_OuDeOutroDono_EhRejeitado()
    {
        using var t = new TempDb();
        var owner = t.NewCharacter("Dono");
        var other = t.NewCharacter("Outro");
        var sword = t.Db.CreateInventoryItem(owner, Sword, "comum", 1, "e1")!;

        Assert.Equal("not_in_inventory", t.Db.TryEquip(other, sword, "weapon_main").Reason);
        Assert.Equal("not_in_inventory", t.Db.TryEquip(owner, "nao-existe", "weapon_main").Reason);
        Assert.Empty(t.Db.GetEquipment(other));
        Assert.Equal("inventory", t.Db.GetItemLocation(sword)!.Kind);
    }

    [Fact]
    public void Desequipar_ComInventarioCheio_EhRejeitado_EMantemEquipado()
    {
        using var t = new TempDb(inventoryCapacity: 1);
        var c = t.NewCharacter("A");
        var sword = t.Db.CreateInventoryItem(c, Sword, "comum", 1, "e1")!;
        Assert.True(t.Db.TryEquip(c, sword, "weapon_main").Success);
        Assert.NotNull(t.Db.CreateInventoryItem(c, Tonic, "comum", 1, "e2")); // ocupa o único slot

        var result = t.Db.TryUnequip(c, "weapon_main");

        Assert.Equal("inventory_full", result.Reason);
        Assert.Equal(sword, Assert.Single(t.Db.GetEquipment(c)).ItemId);
        Assert.Equal("nothing_equipped", t.Db.TryUnequip(c, "slot_vazio").Reason);
    }

    [Fact]
    public void Equipamento_SobreviveAoReinicio()
    {
        using var t = new TempDb();
        var c = t.NewCharacter("A");
        var sword = t.Db.CreateInventoryItem(c, Sword, "comum", 1, "e1")!;
        Assert.True(t.Db.TryEquip(c, sword, "weapon_main").Success);

        var reopened = new CinzasDaMare.Server.Persistence.GameDatabase(t.DbPath);
        reopened.Initialize();

        Assert.Equal(sword, Assert.Single(reopened.GetEquipment(c)).ItemId);
        Assert.Empty(reopened.GetInventory(c));
    }
}

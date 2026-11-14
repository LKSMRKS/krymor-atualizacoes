using CinzasDaMare.Server.Persistence;

namespace CinzasDaMare.Tests;

public class PersistenceTests
{
    [Fact]
    public void Initialize_CriaSchemaV1_EEIdempotente()
    {
        using var t = new TempDb();
        Assert.Equal(GameDatabase.CurrentSchemaVersion, t.Db.GetSchemaVersion());
        t.Db.Initialize();
        Assert.Equal(GameDatabase.CurrentSchemaVersion, t.Db.GetSchemaVersion());
    }

    [Fact]
    public void PersonagemEItem_SobrevivemAoReinicioDoServidor()
    {
        using var t = new TempDb();
        var id = t.NewCharacter("Aldric");
        t.Db.SaveCharacterState(id, 1, 120, "arena", 3.5, -2.0, 150.5);
        var item = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "teste");
        var claim = t.Db.TryClaimWorldItem(Guid.NewGuid().ToString(), item, id);
        Assert.True(claim.Success);

        // "Reinício": nova instância sobre o mesmo arquivo.
        var reopened = new GameDatabase(t.DbPath);
        reopened.Initialize();

        var ch = reopened.LoadCharacter(id);
        Assert.NotNull(ch);
        Assert.Equal("Aldric", ch!.Name);
        Assert.Equal("arena", ch.MapId);
        Assert.Equal(3.5, ch.PosX, 6);
        Assert.Equal(-2.0, ch.PosY, 6);
        Assert.Equal(150.5, ch.Health, 6);
        Assert.Equal(120, ch.Xp);
        Assert.Equal(10, ch.Strength);

        var inv = reopened.GetInventory(id);
        Assert.Single(inv);
        Assert.Equal(item, inv[0].ItemId);
        Assert.Equal("weapon_guerreiro_sword_p1", inv[0].DefinitionId);
        Assert.Equal(0, inv[0].Slot);
    }

    [Fact]
    public async Task DoisJogadores_NuncaColetamOMesmoItem()
    {
        using var t = new TempDb();
        var a = t.NewCharacter("A");
        var b = t.NewCharacter("B");

        for (var round = 0; round < 20; round++)
        {
            var item = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "rodada" + round);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ta = Task.Run(async () =>
            {
                await gate.Task;
                return t.Db.TryClaimWorldItem(Guid.NewGuid().ToString(), item, a);
            });
            var tb = Task.Run(async () =>
            {
                await gate.Task;
                return t.Db.TryClaimWorldItem(Guid.NewGuid().ToString(), item, b);
            });
            gate.SetResult();
            var results = await Task.WhenAll(ta, tb);

            Assert.Single(results, r => r.Success);
            Assert.Single(results, r => !r.Success && r.Reason == "already_taken");
        }

        Assert.Equal(20, t.Db.GetInventory(a).Count + t.Db.GetInventory(b).Count);
    }

    [Fact]
    public void MesmoRequestId_DevolveResultadoGravado_SemNovaTentativa()
    {
        using var t = new TempDb();
        var a = t.NewCharacter("A");
        var item = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "teste");

        var first = t.Db.TryClaimWorldItem("req-1", item, a);
        var replay = t.Db.TryClaimWorldItem("req-1", item, a);

        Assert.True(first.Success);
        Assert.Equal(first, replay);
        Assert.Single(t.Db.GetInventory(a));
        Assert.Equal(1, t.Db.CountTransactionLog("claim_item"));
    }

    [Fact]
    public void ItemJaColetado_NaoPodeSerColetadoPorOutro_NemPeloMesmo()
    {
        using var t = new TempDb();
        var a = t.NewCharacter("A");
        var b = t.NewCharacter("B");
        var item = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "teste");

        Assert.True(t.Db.TryClaimWorldItem("r1", item, a).Success);
        Assert.Equal("already_taken", t.Db.TryClaimWorldItem("r2", item, b).Reason);
        Assert.Equal("already_taken", t.Db.TryClaimWorldItem("r3", item, a).Reason);
        Assert.Empty(t.Db.GetInventory(b));
        Assert.Equal("inventory", t.Db.GetItemLocation(item)!.Kind);
    }

    [Fact]
    public void ItemsOcupamSlotsDistintos_EInventarioCheioRejeita()
    {
        using var t = new TempDb();
        var a = t.NewCharacter("A");
        for (var i = 0; i < t.Db.InventoryCapacity; i++)
        {
            var it = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "e" + i);
            var r = t.Db.TryClaimWorldItem("req" + i, it, a);
            Assert.True(r.Success);
            Assert.Equal(i, r.Slot);
        }
        var extra = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "extra");
        var full = t.Db.TryClaimWorldItem("reqextra", extra, a);
        Assert.Equal("inventory_full", full.Reason);
        Assert.Equal("world", t.Db.GetItemLocation(extra)!.Kind);
    }

    [Fact]
    public void ItemOuPersonagemInexistente_SaoRejeitados()
    {
        using var t = new TempDb();
        var a = t.NewCharacter("A");
        Assert.Equal("unknown_item", t.Db.TryClaimWorldItem("r1", "nao-existe", a).Reason);
        var item = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "teste");
        Assert.Equal("unknown_character", t.Db.TryClaimWorldItem("r2", item, "ninguem").Reason);
        Assert.Equal("world", t.Db.GetItemLocation(item)!.Kind);
    }

    [Fact]
    public void Backup_GeraArquivoLegivelComOsDados()
    {
        using var t = new TempDb();
        var id = t.NewCharacter("Aldric");
        var backup = Path.Combine(t.Dir, "backup.db");

        t.Db.BackupTo(backup);

        var restored = new GameDatabase(backup);
        restored.Initialize();
        Assert.Equal("Aldric", restored.LoadCharacter(id)!.Name);
    }
}

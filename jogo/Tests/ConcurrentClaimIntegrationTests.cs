namespace CinzasDaMare.Tests;

/// <summary>
/// Integração de CONCORRÊNCIA no banco: várias operações paralelas (threads do mesmo processo, SQLite real)
/// disputando o mesmo item. É diferente do aceite futuro (E6), com dois clientes reais pela rede.
/// </summary>
public class ConcurrentClaimIntegrationTests
{
    [Fact]
    public async Task DuasOperacoesConcorrentes_UmSoVencedor_SemDuplicacao_E_ReplayIdempotente()
    {
        using var t = new TempDb();
        var a = t.NewCharacter("A");
        var b = t.NewCharacter("B");

        for (var round = 0; round < 25; round++)
        {
            var item = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "rodada" + round);
            var reqA = $"A-{round}";
            var reqB = $"B-{round}";
            var logBefore = t.Db.CountTransactionLog("claim_item");

            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var ta = Task.Run(async () => { await gate.Task; return t.Db.TryClaimWorldItem(reqA, item, a); });
            var tb = Task.Run(async () => { await gate.Task; return t.Db.TryClaimWorldItem(reqB, item, b); });
            gate.SetResult();
            var ra = await ta;
            var rb = await tb;

            // um único vencedor
            Assert.True(ra.Success ^ rb.Success);
            var winner = ra.Success ? a : b;
            var loserResult = ra.Success ? rb : ra;
            Assert.Equal("already_taken", loserResult.Reason);

            // sem duplicação: uma localização, um dono, uma posse, um registro de log
            var location = t.Db.GetItemLocation(item);
            Assert.Equal("inventory", location!.Kind);
            Assert.Equal(winner, location.OwnerCharacterId);
            var holders = t.Db.GetInventory(a).Concat(t.Db.GetInventory(b)).Where(e => e.ItemId == item);
            Assert.Single(holders);
            Assert.Equal(logBefore + 1, t.Db.CountTransactionLog("claim_item"));

            // idempotência: repetir cada requisição devolve exatamente o resultado gravado
            Assert.Equal(ra, t.Db.TryClaimWorldItem(reqA, item, a));
            Assert.Equal(rb, t.Db.TryClaimWorldItem(reqB, item, b));
            Assert.Single(t.Db.GetInventory(a).Concat(t.Db.GetInventory(b)), e => e.ItemId == item);
            Assert.Equal(logBefore + 1, t.Db.CountTransactionLog("claim_item"));
        }
    }

    [Fact]
    public async Task MesmaRequisicaoEmParalelo_ExecutaUmaVez_ETodosRecebemOMesmoResultado()
    {
        using var t = new TempDb();
        var a = t.NewCharacter("A");
        var item = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "unico");

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 6)
            .Select(_ => Task.Run(async () => { await gate.Task; return t.Db.TryClaimWorldItem("req-unica", item, a); }))
            .ToList();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal(results[0], r));
        Assert.True(results[0].Success);
        Assert.Single(t.Db.GetInventory(a));
        Assert.Equal(1, t.Db.CountTransactionLog("claim_item"));
    }

    [Fact]
    public async Task OitoDisputantes_ApenasUmLevaOItem()
    {
        using var t = new TempDb();
        var characters = Enumerable.Range(0, 8).Select(i => t.NewCharacter("P" + i)).ToList();
        var item = t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "disputa");

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = characters
            .Select(c => Task.Run(async () => { await gate.Task; return t.Db.TryClaimWorldItem("req-" + c, item, c); }))
            .ToList();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r.Success);
        Assert.All(results.Where(r => !r.Success), r => Assert.Equal("already_taken", r.Reason));
        Assert.Single(characters.SelectMany(c => t.Db.GetInventory(c)), e => e.ItemId == item);
    }
}

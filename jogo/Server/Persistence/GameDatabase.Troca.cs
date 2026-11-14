using Microsoft.Data.Sqlite;

namespace CinzasDaMare.Server.Persistence;

/// <summary>
/// 11/11 (social: troca entre jogadores). Tudo numa transação só: os itens de cada lado mudam de dono (na primeira casa
/// livre da mochila do outro) e as moedas passam de um para o outro. Falhou qualquer parte (item não está mais na
/// mochila, mochila cheia, moedas a menos) = nada muda. Idempotente por requestId.
/// </summary>
public sealed partial class GameDatabase
{
    public (bool Ok, string Reason) TryTrade(string requestId, string a, string b, IReadOnlyList<string> aItems, IReadOnlyList<string> bItems, long aCoins, long bCoins)
    {
        try
        {
            return InTransaction(c =>
            {
                using (var q = Cmd(c, "SELECT 1 FROM processed_requests WHERE request_id=@r", ("@r", requestId)))
                    if (q.ExecuteScalar() is not null) return (true, "already_done");
                if (ReadCoins(c, a) < aCoins || ReadCoins(c, b) < bCoins) throw new TradeAbort("not_enough_coins");
                // 1) tira tudo das duas mochilas para casas provisórias (libera espaço dos dois lados)
                var moves = aItems.Select(i => (Item: i, From: a, To: b)).Concat(bItems.Select(i => (Item: i, From: b, To: a))).ToList();
                var n = 0;
                foreach (var mv in moves)
                    using (var u = Cmd(c, "UPDATE item_locations SET owner_character_id=@t, slot=@s WHERE item_id=@i AND owner_character_id=@f AND location_kind='inventory'",
                        ("@t", mv.To), ("@s", "__troca" + n++), ("@i", mv.Item), ("@f", mv.From)))
                        if (u.ExecuteNonQuery() != 1) throw new TradeAbort("item_moved");
                // 2) cada item na primeira casa livre do novo dono
                foreach (var mv in moves)
                {
                    var slot = FindFreeInventorySlot(c, mv.To, DefinitionOf(c, mv.Item) ?? "");
                    if (slot < 0) throw new TradeAbort("inventory_full");
                    using var u = Cmd(c, "UPDATE item_locations SET slot=@s WHERE item_id=@i", ("@s", slot.ToString()), ("@i", mv.Item));
                    u.ExecuteNonQuery();
                    using var v = Cmd(c, "UPDATE item_instances SET version = version + 1 WHERE id=@i", ("@i", mv.Item));
                    v.ExecuteNonQuery();
                    Log(c, "trade", mv.To, mv.Item, requestId, "de " + mv.From);
                }
                foreach (var (from, to, amount) in new[] { (a, b, aCoins), (b, a, bCoins) })
                {
                    if (amount <= 0) continue;
                    using (var u = Cmd(c, "UPDATE characters SET coins = coins - @n WHERE id=@i", ("@n", amount), ("@i", from))) u.ExecuteNonQuery();
                    using (var u = Cmd(c, "UPDATE characters SET coins = coins + @n WHERE id=@i", ("@n", amount), ("@i", to))) u.ExecuteNonQuery();
                    Log(c, "trade", to, null, requestId, $"+{amount} moedas de {from}");
                }
                MarkProcessed(c, requestId, "trade", $"{aItems.Count}+{bItems.Count}");
                return (true, "ok");
            });
        }
        catch (TradeAbort e) { return (false, e.Message); }
    }

    private sealed class TradeAbort : Exception { public TradeAbort(string reason) : base(reason) { } }
}

using System.Text.Json;

namespace CinzasDaMare.Server.Persistence;

/// <summary>
/// Recompra (pedido 10/11): o item vendido não some na hora — fica "no balcão" do NPC (depósito "recompra") enquanto a
/// loja está aberta. Dá para comprar de volta pelo mesmo preço; ao sair de perto, depois de um tempo ou ao deslogar, o
/// balcão é limpo e o item deixa de existir (o NPC já fez o que quis com ele). Sem tabela nova: usa storage_items.
/// </summary>
public sealed partial class GameDatabase
{
    public const string BuybackContainer = "recompra";

    /// <summary>Como <see cref="TrySell"/>, mas o item vai para o balcão de recompra em vez de ser apagado.</summary>
    public TradeResult TrySellToBuyback(string requestId, string characterId, string itemId, long unitPrice)
    {
        return InTransaction(c =>
        {
            if (Processed(c, requestId) is { } done) return done;
            TradeResult result;
            int quantity = 0;
            string? definitionId = null;
            using (var q = Cmd(c,
                "SELECT i.definition_id, i.quantity FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
                "WHERE i.id=@i AND l.owner_character_id=@c AND l.location_kind='inventory'", ("@i", itemId), ("@c", characterId)))
            using (var r = q.ExecuteReader())
                if (r.Read()) { definitionId = r.GetString(0); quantity = r.GetInt32(1); }
            if (definitionId is null) result = new TradeResult(false, "not_in_inventory", ReadCoins(c, characterId));
            else
            {
                using (var d1 = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i", ("@i", itemId))) d1.ExecuteNonQuery();
                using (var i = Cmd(c, "INSERT INTO storage_items(item_id,character_id,container,created_at) VALUES(@i,@c,@k,@t)",
                           ("@i", itemId), ("@c", characterId), ("@k", BuybackContainer), ("@t", Now()))) i.ExecuteNonQuery();
                var total = unitPrice * quantity;
                using (var u = Cmd(c, "UPDATE characters SET coins = coins + @p WHERE id=@i", ("@p", total), ("@i", characterId)))
                    u.ExecuteNonQuery();
                Log(c, "sell", characterId, itemId, requestId, $"{definitionId} x{quantity} por {total} (balcão)");
                result = new TradeResult(true, "ok", ReadCoins(c, characterId));
            }
            MarkProcessed(c, requestId, "sell", JsonSerializer.Serialize(result));
            return result;
        });
    }

    /// <summary>Compra de volta do balcão pelo preço pago na venda. Falha sem moedas ou sem espaço, sem mudar nada.</summary>
    public TradeResult TryBuyback(string requestId, string characterId, string itemId, long price)
    {
        return InTransaction(c =>
        {
            if (Processed(c, requestId) is { } done) return done;
            TradeResult Fail(string why) { var f = new TradeResult(false, why, ReadCoins(c, characterId)); MarkProcessed(c, requestId, "buyback", JsonSerializer.Serialize(f)); return f; }
            using (var q = Cmd(c, "SELECT 1 FROM storage_items WHERE item_id=@i AND character_id=@c AND container=@k",
                       ("@i", itemId), ("@c", characterId), ("@k", BuybackContainer)))
                if (q.ExecuteScalar() is null) return Fail("gone");
            if (ReadCoins(c, characterId) < price) return Fail("not_enough_coins");
            var slot = WithdrawSlot(c, characterId, itemId, null);
            if (slot < 0) return Fail("inventory_full");
            using (var d = Cmd(c, "DELETE FROM storage_items WHERE item_id=@i", ("@i", itemId))) d.ExecuteNonQuery();
            using (var i = Cmd(c, "INSERT INTO item_locations(item_id,location_kind,owner_character_id,slot) VALUES(@i,'inventory',@c,@s)",
                       ("@i", itemId), ("@c", characterId), ("@s", slot.ToString()))) i.ExecuteNonQuery();
            using (var u = Cmd(c, "UPDATE characters SET coins = coins - @p WHERE id=@i", ("@p", price), ("@i", characterId))) u.ExecuteNonQuery();
            Log(c, "buyback", characterId, itemId, requestId, price.ToString());
            var ok = new TradeResult(true, "ok", ReadCoins(c, characterId));
            MarkProcessed(c, requestId, "buyback", JsonSerializer.Serialize(ok));
            return ok;
        });
    }

    /// <summary>Limpa o balcão: os itens vendidos deixam de existir. Devolve quantos foram apagados.</summary>
    public int PurgeBuyback(string characterId)
    {
        return InTransaction(c =>
        {
            var ids = new List<string>();
            using (var q = Cmd(c, "SELECT item_id FROM storage_items WHERE character_id=@c AND container=@k", ("@c", characterId), ("@k", BuybackContainer)))
            using (var r = q.ExecuteReader())
                while (r.Read()) ids.Add(r.GetString(0));
            foreach (var id in ids)
            {
                using (var d1 = Cmd(c, "DELETE FROM storage_items WHERE item_id=@i", ("@i", id))) d1.ExecuteNonQuery();
                using (var d2 = Cmd(c, "DELETE FROM item_instances WHERE id=@i", ("@i", id))) d2.ExecuteNonQuery();
            }
            return ids.Count;
        });
    }
}

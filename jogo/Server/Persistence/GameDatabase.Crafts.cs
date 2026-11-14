using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CinzasDaMare.Server.Persistence;

public sealed record PlotState(string PlotId, string Seed, long PlantedAt, long? WateredAt, long? ReadyAt);
public sealed record StoredItem(string ItemId, string DefinitionId, string Quality, int Quantity);

/// <summary>
/// Expansão até o midgame: ofício (receitas), coleta de minério, horta, armazém/baú e refeição.
/// Tudo transacional; pedidos com ID repetido devolvem o resultado anterior (não fabricam de novo).
/// Tempos (horta, veios) em milissegundos de relógio real (UTC), passados pelo servidor — nunca do cliente.
/// </summary>
public sealed partial class GameDatabase
{
    // v4: só ACRESCENTA tabelas novas (nada existente muda).
    private const string MigrationV4 = @"
CREATE TABLE storage_items (
  item_id TEXT PRIMARY KEY REFERENCES item_instances(id),
  character_id TEXT NOT NULL REFERENCES characters(id),
  container TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE TABLE node_harvests (
  character_id TEXT NOT NULL REFERENCES characters(id),
  node_id TEXT NOT NULL,
  ready_at INTEGER NOT NULL,
  PRIMARY KEY (character_id, node_id)
);
CREATE TABLE garden_plots (
  character_id TEXT NOT NULL REFERENCES characters(id),
  plot_id TEXT NOT NULL,
  seed TEXT NOT NULL,
  planted_at INTEGER NOT NULL,
  watered_at INTEGER,
  ready_at INTEGER,
  PRIMARY KEY (character_id, plot_id)
);
CREATE TABLE character_buffs (
  character_id TEXT PRIMARY KEY REFERENCES characters(id),
  buff_id TEXT NOT NULL,
  until_ms INTEGER NOT NULL
);";

    // ------------------------------------------------------------ ofício

    /// <summary>
    /// Fabrica: confere e consome todos os ingredientes e entrega o resultado numa transação. Sem espaço, nada muda.
    /// requestKey já processado devolve o mesmo resultado (não fabrica outra vez).
    /// </summary>
    public TradeResult TryCraft(string requestKey, string characterId, IReadOnlyList<(string Item, int Qty)> inputs, GrantItem output)
    {
        return InTransaction(c =>
        {
            if (Processed(c, requestKey) is { } previous) return previous;
            foreach (var (item, qty) in inputs)
                if (CountInInventoryTx(c, characterId, item) < qty)
                    return Remember(c, requestKey, "craft", new TradeResult(false, "missing_items", ReadCoins(c, characterId)));
            foreach (var (item, qty) in inputs) RemoveFromInventory(c, characterId, item, qty);
            if (!GrantInto(c, characterId, output, "craft:" + requestKey)) throw new InventoryFullException();
            Log(c, "craft", characterId, null, requestKey, $"{output.DefinitionId} x{output.Quantity}");
            return Remember(c, requestKey, "craft", new TradeResult(true, "ok", ReadCoins(c, characterId)));
        }, onInventoryFull: () => new TradeResult(false, "inventory_full", GetCoins(characterId)));
    }

    private static TradeResult Remember(SqliteConnection c, string requestKey, string kind, TradeResult result)
    {
        MarkProcessed(c, requestKey, kind, JsonSerializer.Serialize(result));
        return result;
    }

    private static int CountInInventoryTx(SqliteConnection c, string characterId, string definitionId)
    {
        using var cmd = Cmd(c,
            "SELECT COALESCE(SUM(i.quantity),0) FROM item_instances i JOIN item_locations l ON l.item_id = i.id " +
            "WHERE l.owner_character_id=@c AND l.location_kind='inventory' AND i.definition_id=@d", ("@c", characterId), ("@d", definitionId));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Entrega itens (missão ao aceitar, colheita, minério). Falso = mochila cheia (nada entregue).</summary>
    public bool TryGrantItems(string characterId, IReadOnlyList<GrantItem> items, string origin)
    {
        return InTransaction(c =>
        {
            var i = 0;
            foreach (var item in items)
                if (!GrantInto(c, characterId, item, $"{origin}:{i++}")) throw new InventoryFullException();
            return true;
        }, onInventoryFull: () => false);
    }

    // ------------------------------------------------------------ mineração

    public long NodeReadyAt(string characterId, string nodeId)
    {
        using var c = Connect();
        using var q = Cmd(c, "SELECT ready_at FROM node_harvests WHERE character_id=@c AND node_id=@n", ("@c", characterId), ("@n", nodeId));
        return q.ExecuteScalar() is { } v ? Convert.ToInt64(v) : 0;
    }

    public IReadOnlyDictionary<string, long> NodeTimers(string characterId)
    {
        using var c = Connect();
        using var q = Cmd(c, "SELECT node_id, ready_at FROM node_harvests WHERE character_id=@c", ("@c", characterId));
        using var r = q.ExecuteReader();
        var d = new Dictionary<string, long>();
        while (r.Read()) d[r.GetString(0)] = r.GetInt64(1);
        return d;
    }

    /// <summary>Colhe o veio para ESTE personagem (cada um tem a sua recarga: um amigo não apaga o objetivo do outro).</summary>
    public (bool Success, string Reason) TryHarvestNode(string characterId, string nodeId, long nowMs, long cooldownMs, GrantItem item)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT ready_at FROM node_harvests WHERE character_id=@c AND node_id=@n", ("@c", characterId), ("@n", nodeId)))
                if (q.ExecuteScalar() is { } v && Convert.ToInt64(v) > nowMs) return (false, "node_empty");
            if (!GrantInto(c, characterId, item, $"node:{nodeId}:{characterId}:{nowMs}")) throw new InventoryFullException();
            using (var u = Cmd(c, "INSERT INTO node_harvests(character_id,node_id,ready_at) VALUES(@c,@n,@r) " +
                                  "ON CONFLICT(character_id,node_id) DO UPDATE SET ready_at=@r",
                       ("@c", characterId), ("@n", nodeId), ("@r", nowMs + cooldownMs)))
                u.ExecuteNonQuery();
            Log(c, "mine", characterId, null, null, $"{nodeId} {item.DefinitionId} x{item.Quantity}");
            return (true, "ok");
        }, onInventoryFull: () => (false, "inventory_full"));
    }

    // ------------------------------------------------------------ horta

    public IReadOnlyList<PlotState> GetPlots(string characterId)
    {
        using var c = Connect();
        using var q = Cmd(c, "SELECT plot_id, seed, planted_at, watered_at, ready_at FROM garden_plots WHERE character_id=@c", ("@c", characterId));
        using var r = q.ExecuteReader();
        var list = new List<PlotState>();
        while (r.Read())
            list.Add(new PlotState(r.GetString(0), r.GetString(1), r.GetInt64(2), r.IsDBNull(3) ? null : r.GetInt64(3), r.IsDBNull(4) ? null : r.GetInt64(4)));
        return list;
    }

    /// <summary>Planta: consome uma semente da mochila. O canteiro precisa estar vazio.</summary>
    public (bool Success, string Reason) TryPlant(string characterId, string plotId, string seed, long nowMs)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT 1 FROM garden_plots WHERE character_id=@c AND plot_id=@p", ("@c", characterId), ("@p", plotId)))
                if (q.ExecuteScalar() is not null) return (false, "plot_busy");
            if (!RemoveFromInventory(c, characterId, seed, 1)) return (false, "no_seed");
            using (var i = Cmd(c, "INSERT INTO garden_plots(character_id,plot_id,seed,planted_at) VALUES(@c,@p,@s,@t)",
                       ("@c", characterId), ("@p", plotId), ("@s", seed), ("@t", nowMs)))
                i.ExecuteNonQuery();
            Log(c, "plant", characterId, null, null, $"{plotId} {seed}");
            return (true, "ok");
        });
    }

    /// <summary>Rega uma vez: a contagem do crescimento começa aqui (no relógio do servidor; continua com o jogo fechado).</summary>
    public (bool Success, string Reason) TryWater(string characterId, string plotId, long nowMs, long growMs)
    {
        return InTransaction(c =>
        {
            using (var u = Cmd(c, "UPDATE garden_plots SET watered_at=@t, ready_at=@r WHERE character_id=@c AND plot_id=@p AND watered_at IS NULL",
                       ("@t", nowMs), ("@r", nowMs + growMs), ("@c", characterId), ("@p", plotId)))
                if (u.ExecuteNonQuery() != 1) return (false, "not_needed");
            return (true, "ok");
        });
    }

    public (bool Success, string Reason) TryHarvestPlot(string characterId, string plotId, long nowMs, GrantItem crop)
    {
        return InTransaction(c =>
        {
            using (var q = Cmd(c, "SELECT ready_at FROM garden_plots WHERE character_id=@c AND plot_id=@p", ("@c", characterId), ("@p", plotId)))
            {
                var v = q.ExecuteScalar();
                if (v is null) return (false, "plot_empty");
                if (v is DBNull || Convert.ToInt64(v) > nowMs) return (false, "not_ready");
            }
            if (!GrantInto(c, characterId, crop, $"harvest:{plotId}:{characterId}:{nowMs}")) throw new InventoryFullException();
            using (var d = Cmd(c, "DELETE FROM garden_plots WHERE character_id=@c AND plot_id=@p", ("@c", characterId), ("@p", plotId)))
                d.ExecuteNonQuery();
            Log(c, "harvest", characterId, null, null, $"{plotId} {crop.DefinitionId} x{crop.Quantity}");
            return (true, "ok");
        }, onInventoryFull: () => (false, "inventory_full"));
    }

    // ------------------------------------------------------------ armazém comunitário e baú da casa

    public IReadOnlyList<StoredItem> GetStorage(string characterId, string container)
    {
        using var c = Connect();
        using var q = Cmd(c,
            "SELECT i.id, i.definition_id, i.quality, i.quantity FROM storage_items s JOIN item_instances i ON i.id = s.item_id " +
            "WHERE s.character_id=@c AND s.container=@k ORDER BY s.created_at", ("@c", characterId), ("@k", container));
        using var r = q.ExecuteReader();
        var list = new List<StoredItem>();
        while (r.Read()) list.Add(new StoredItem(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3)));
        return list;
    }

    /// <summary>
    /// Mochila → depósito (a mesma instância muda de lugar; nunca duplica). Pedido repetido devolve o resultado anterior.
    /// </summary>
    public TradeResult TryDeposit(string requestKey, string characterId, string itemId, string container, int capacity)
    {
        return InTransaction(c =>
        {
            if (Processed(c, requestKey) is { } previous) return previous;
            string? owner;
            using (var q = Cmd(c, "SELECT owner_character_id FROM item_locations WHERE item_id=@i AND location_kind='inventory'", ("@i", itemId)))
                owner = q.ExecuteScalar() as string;
            if (owner != characterId) return Remember(c, requestKey, "deposit", new TradeResult(false, "not_in_inventory", ReadCoins(c, characterId)));
            if (GridOn)
            {
                var (w, h) = SizeOf(DefinitionOf(c, itemId) ?? "");
                if (StorageArea(c, characterId, container) + w * h > capacity)
                    return Remember(c, requestKey, "deposit", new TradeResult(false, "storage_full", ReadCoins(c, characterId)));
            }
            else
                using (var q = Cmd(c, "SELECT COUNT(*) FROM storage_items WHERE character_id=@c AND container=@k", ("@c", characterId), ("@k", container)))
                    if (Convert.ToInt32(q.ExecuteScalar()) >= capacity)
                        return Remember(c, requestKey, "deposit", new TradeResult(false, "storage_full", ReadCoins(c, characterId)));
            using (var d = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i", ("@i", itemId))) d.ExecuteNonQuery();
            using (var i = Cmd(c, "INSERT INTO storage_items(item_id,character_id,container,created_at) VALUES(@i,@c,@k,@t)",
                       ("@i", itemId), ("@c", characterId), ("@k", container), ("@t", Now())))
                i.ExecuteNonQuery();
            Log(c, "deposit", characterId, itemId, requestKey, container);
            return Remember(c, requestKey, "deposit", new TradeResult(true, "ok", ReadCoins(c, characterId)));
        });
    }

    public TradeResult TryWithdraw(string requestKey, string characterId, string itemId, string container, int? toSlot = null)
    {
        return InTransaction(c =>
        {
            if (Processed(c, requestKey) is { } previous) return previous;
            using (var q = Cmd(c, "SELECT 1 FROM storage_items WHERE item_id=@i AND character_id=@c AND container=@k",
                       ("@i", itemId), ("@c", characterId), ("@k", container)))
                if (q.ExecuteScalar() is null) return Remember(c, requestKey, "withdraw", new TradeResult(false, "not_stored", ReadCoins(c, characterId)));
            var slot = WithdrawSlot(c, characterId, itemId, toSlot);
            if (slot < 0) return Remember(c, requestKey, "withdraw", new TradeResult(false, "inventory_full", ReadCoins(c, characterId)));
            using (var d = Cmd(c, "DELETE FROM storage_items WHERE item_id=@i", ("@i", itemId))) d.ExecuteNonQuery();
            using (var i = Cmd(c, "INSERT INTO item_locations(item_id,location_kind,owner_character_id,slot) VALUES(@i,'inventory',@c,@s)",
                       ("@i", itemId), ("@c", characterId), ("@s", slot.ToString())))
                i.ExecuteNonQuery();
            Log(c, "withdraw", characterId, itemId, requestKey, container);
            return Remember(c, requestKey, "withdraw", new TradeResult(true, "ok", ReadCoins(c, characterId)));
        });
    }

    // ------------------------------------------------------------ refeição (um efeito ativo por personagem)

    public void SetBuff(string characterId, string buffId, long untilMs)
    {
        Write(c =>
        {
            using var u = Cmd(c, "INSERT INTO character_buffs(character_id,buff_id,until_ms) VALUES(@c,@b,@u) " +
                                 "ON CONFLICT(character_id) DO UPDATE SET buff_id=@b, until_ms=@u", ("@c", characterId), ("@b", buffId), ("@u", untilMs));
            u.ExecuteNonQuery();
        });
    }

    public (string BuffId, long UntilMs)? GetBuff(string characterId)
    {
        using var c = Connect();
        using var q = Cmd(c, "SELECT buff_id, until_ms FROM character_buffs WHERE character_id=@c", ("@c", characterId));
        using var r = q.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetInt64(1)) : null;
    }

    /// <summary>Cobra moedas sem pedido de requisição (compra da casa ao concluir a missão). Falso = moedas insuficientes.</summary>
    public bool TryChargeCoins(string characterId, long amount)
    {
        return InTransaction(c =>
        {
            if (ReadCoins(c, characterId) < amount) return false;
            using var u = Cmd(c, "UPDATE characters SET coins = coins - @n WHERE id=@i", ("@n", amount), ("@i", characterId));
            u.ExecuteNonQuery();
            Log(c, "charge", characterId, null, null, amount.ToString());
            return true;
        });
    }
}

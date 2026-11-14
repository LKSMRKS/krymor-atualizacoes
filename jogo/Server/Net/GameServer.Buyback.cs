using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// Recompra (pedido 10/11): o que você vende fica no balcão do NPC enquanto você está na loja. Dá para comprar de volta
/// pelo mesmo preço. Saiu de perto, passou o tempo ou deslogou: o balcão é limpo (o NPC já fez o que quis com o item).
/// </summary>
public sealed partial class GameServer
{
    private const long BuybackKeepMs = 5 * 60_000;

    private sealed partial class Session
    {
        public string? BuybackNpc;
        public long BuybackUntilMs;
        public readonly List<BuybackEntry> BuybackItems = new();
    }

    private void AddBuyback(Session s, string npcId, BuybackEntry entry)
    {
        if (s.BuybackNpc is not null && s.BuybackNpc != npcId) PurgeBuyback(s, send: false);
        s.BuybackNpc = npcId;
        s.BuybackUntilMs = NowMs + BuybackKeepMs;
        s.BuybackItems.Insert(0, entry);
        SendBuyback(s);
    }

    private void SendBuyback(Session s) => SendTo(s, new BuybackMessage(s.BuybackNpc ?? "", s.BuybackItems.ToList()));

    private void PurgeBuyback(Session s, bool send = true)
    {
        if (s.CharacterId is null) return;
        var had = s.BuybackItems.Count > 0;
        _db.PurgeBuyback(s.CharacterId);
        s.BuybackItems.Clear();
        s.BuybackNpc = null;
        if (send && had) SendBuyback(s);
    }

    /// <summary>A cada segundo: longe do NPC ou passou o tempo, o balcão some.</summary>
    private void TickBuyback(Session s, long now)
    {
        if (s.BuybackNpc is null || _tick % 20 != 0) return;
        if (now >= s.BuybackUntilMs || NearNpc(s, s.BuybackNpc) is null) PurgeBuyback(s);
    }

    private void HandleBuyback(Session s, BuybackIntent m)
    {
        var requestId = m.RequestId ?? "";
        if (requestId.Length == 0 || requestId.Length > 64) { Strike(s, "bad_buyback"); return; }
        if (s.BuybackNpc != m.NpcId || NearNpc(s, m.NpcId ?? "") is null) { Reply(s, "buyback", false, "out_of_range"); return; }
        var entry = s.BuybackItems.FirstOrDefault(e => e.ItemId == m.ItemId);
        if (entry is null) { Reply(s, "buyback", false, "gone"); return; }
        var before = s.Coins;
        var r = _db.TryBuyback($"{s.CharacterId}:bb:{requestId}", s.CharacterId!, entry.ItemId, entry.Price);
        s.Coins = r.Coins;
        GameLog.Info("servidor.loja", $"{Who(s)} recompra {entry.DefinitionId} x{entry.Quantity} por {entry.Price}: {r.Reason}");
        Reply(s, "buyback", r.Success, r.Reason);
        if (!r.Success) return;
        s.BuybackItems.Remove(entry);
        SendTo(s, new CoinsMessage(s.Coins, s.Coins - before));
        SendInventory(s);
        SendBuyback(s);
    }
}

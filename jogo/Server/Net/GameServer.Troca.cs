using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 11/11 (social): troca entre jogadores, como no MU — um pede, o outro aceita, cada um põe itens e moedas na mesa,
/// os dois confirmam e o servidor passa tudo numa transação só (GameDatabase.TryTrade). Mexeu na mesa, as duas
/// confirmações caem (ninguém é enganado). Só perto (8 m), vivos e no mesmo mapa; item vinculado não entra.
/// </summary>
public sealed partial class GameServer
{
    internal const double TradeRange = 8.0;

    private sealed class TradeTable
    {
        public Session A = null!, B = null!;
        public bool Open;
        public readonly List<string> ItemsA = new(), ItemsB = new();
        public long CoinsA, CoinsB;
        public bool OkA, OkB;
        public int Seq;
    }

    private readonly Dictionary<Session, TradeTable> _trades = new();

    private void HandleTrade(Session s, TradeIntent m)
    {
        if (!MapMode) return;
        _trades.TryGetValue(s, out var t);
        switch (m.Action)
        {
            case "pedir":
            {
                var other = _sessions.Values.FirstOrDefault(x => x.LoggedIn && x.CharacterId == m.Target);
                if (other is null || ReferenceEquals(other, s)) { TradeClosed(s, "Ninguém para trocar."); return; }
                if (!ReferenceEquals(other.Zone, s.Zone) || Distance(s.X, s.Y, other.X, other.Y) > TradeRange) { TradeClosed(s, "Chegue mais perto para trocar."); return; }
                if (_trades.ContainsKey(other)) { TradeClosed(s, $"{other.Name} já está trocando."); return; }
                if (t is not null) CloseTrade(t, "Troca cancelada.");
                var table = new TradeTable { A = s, B = other };
                _trades[s] = table; _trades[other] = table;
                SendTo(other, new TradeStateMessage("pedido", s.Name!, Array.Empty<InventoryItemView>(), Array.Empty<InventoryItemView>(), 0, 0, false, false, "", s.CharacterId!));
                SendTo(s, new NoticeMessage($"Pedido de troca enviado a {other.Name}.", "info"));
                return;
            }
            case "aceitar" when t is not null && ReferenceEquals(t.B, s) && !t.Open:
                t.Open = true;
                SendTradeState(t);
                return;
            case "cancelar" when t is not null:
                CloseTrade(t, $"{s.Name} cancelou a troca.");
                return;
        }
        if (t is null || !t.Open) return;
        var mine = ReferenceEquals(t.A, s) ? t.ItemsA : t.ItemsB;
        switch (m.Action)
        {
            case "item":
            {
                var entry = _db.GetInventory(s.CharacterId!).FirstOrDefault(e => e.ItemId == m.ItemId);
                if (entry is null || mine.Contains(entry.ItemId) || mine.Count >= 12) return;
                if (Bound(entry.DefinitionId)) { SendTo(s, new NoticeMessage("Item vinculado: não dá para trocar.", "warn")); return; }
                mine.Add(entry.ItemId);
                break;
            }
            case "tirar": mine.Remove(m.ItemId ?? ""); break;
            case "moedas":
            {
                var coins = Math.Clamp(m.Coins, 0, _db.GetCoins(s.CharacterId!));
                if (ReferenceEquals(t.A, s)) t.CoinsA = coins; else t.CoinsB = coins;
                break;
            }
            case "confirmar":
                if (ReferenceEquals(t.A, s)) t.OkA = true; else t.OkB = true;
                if (t.OkA && t.OkB) { FinishTrade(t); return; }
                SendTradeState(t);
                return;
            default: return;
        }
        t.OkA = t.OkB = false;   // a mesa mudou: confirma de novo
        SendTradeState(t);
    }

    private void FinishTrade(TradeTable t)
    {
        if (!ReferenceEquals(t.A.Zone, t.B.Zone) || Distance(t.A.X, t.A.Y, t.B.X, t.B.Y) > TradeRange) { CloseTrade(t, "Longe demais: troca cancelada."); return; }
        var (ok, reason) = _db.TryTrade($"troca:{_runId}:{t.A.CharacterId}:{t.B.CharacterId}:{++_tradeSeq}", t.A.CharacterId!, t.B.CharacterId!,
            t.ItemsA, t.ItemsB, t.CoinsA, t.CoinsB);
        if (!ok)
        {
            CloseTrade(t, reason switch { "inventory_full" => "Mochila cheia: troca cancelada.", "not_enough_coins" => "Moedas a menos: troca cancelada.", _ => "Um item saiu da mochila: troca cancelada." });
            return;
        }
        GameLog.Info("servidor.itens", $"troca {Who(t.A)} ({t.ItemsA.Count} itens, {t.CoinsA} moedas) <-> {Who(t.B)} ({t.ItemsB.Count} itens, {t.CoinsB} moedas)");
        foreach (var p in new[] { t.A, t.B }) { SendInventory(p); var before = p.Coins; p.Coins = _db.GetCoins(p.CharacterId!); SendTo(p, new CoinsMessage(p.Coins, p.Coins - before)); }
        CloseTrade(t, "Troca feita!", true);
        Achieve(t.A, "troca"); Achieve(t.B, "troca");
    }

    private int _tradeSeq;

    private void CloseTrade(TradeTable t, string why, bool done = false)
    {
        _trades.Remove(t.A); _trades.Remove(t.B);
        foreach (var p in new[] { t.A, t.B }) TradeClosed(p, why, done);
    }

    private void TradeClosed(Session s, string why, bool done = false)
    {
        SendTo(s, new TradeStateMessage("fechada", "", Array.Empty<InventoryItemView>(), Array.Empty<InventoryItemView>(), 0, 0, false, false, why));
        SendTo(s, new NoticeMessage(why, done ? "reward" : "info"));
    }

    private void SendTradeState(TradeTable t)
    {
        List<InventoryItemView> Views(Session owner, List<string> ids)
        {
            var inv = _db.GetInventory(owner.CharacterId!);
            return ids.Select(id => inv.FirstOrDefault(e => e.ItemId == id)).Where(e => e is not null)
                .Select(e => new InventoryItemView(e!.ItemId, e.DefinitionId, e.Quality, -1, e.Quantity, e.Temper)).ToList();
        }
        var a = Views(t.A, t.ItemsA); var b = Views(t.B, t.ItemsB);
        SendTo(t.A, new TradeStateMessage("aberta", t.B.Name!, a, b, t.CoinsA, t.CoinsB, t.OkA, t.OkB, "", t.B.CharacterId!));
        SendTo(t.B, new TradeStateMessage("aberta", t.A.Name!, b, a, t.CoinsB, t.CoinsA, t.OkB, t.OkA, "", t.A.CharacterId!));
    }

    /// <summary>Quem saiu/morreu/trocou de mapa: a troca fecha (chamado a cada tick, barato).</summary>
    private void CheckTrades()
    {
        if (_trades.Count == 0) return;
        foreach (var t in _trades.Values.Distinct().ToList())
            if (!IsActive(t.A) || !IsActive(t.B) || !ReferenceEquals(t.A.Zone, t.B.Zone) || !t.A.Combat!.Alive || !t.B.Combat!.Alive
                || Distance(t.A.X, t.A.Y, t.B.X, t.B.Y) > TradeRange * 1.5)
                CloseTrade(t, "Troca cancelada.");
    }
}

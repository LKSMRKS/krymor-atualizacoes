using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>11/11: refino estilo MU — arrastar a joia em cima do equipamento (mochila ou vestido). Regras em <see cref="Refine"/>.</summary>
public sealed partial class GameServer
{
    private void HandleRefine(Session s, RefineIntent m)
    {
        var ch = s.CharacterId!;
        var inv = _db.GetInventory(ch);
        var jewel = inv.FirstOrDefault(e => e.ItemId == m.JewelItemId);
        if (jewel is null || !Refine.IsJewel(jewel.DefinitionId)) { Reply(s, "refine", false, "no_jewel"); return; }
        var target = (object?)inv.FirstOrDefault(e => e.ItemId == m.TargetItemId) ?? _db.GetEquipment(ch).FirstOrDefault(e => e.ItemId == m.TargetItemId);
        (string? defId, int plus) = target switch
        {
            InventoryEntry i => (i.DefinitionId, i.Temper),
            EquippedEntry e => (e.DefinitionId, e.Temper),
            _ => ((string?)null, 0),
        };
        if (defId is null || !_content.Items.TryGetValue(defId, out var def)) { Reply(s, "refine", false, "no_item"); return; }
        // 13/11 (análise — itens: "reforjar"): Joia do Vazio em cima de uma peça de conjunto sorteia a variação de novo
        // (de 2 abaixo a 3 acima da atual, até V14; V15 Full só em chefe). Custa a joia e moedas.
        var qualAtual = target switch { InventoryEntry i2 => i2.Quality, EquippedEntry e2 => e2.Quality, _ => "" };
        if (jewel.DefinitionId == "joia_forja")
        {
            if (Variacao.Of(qualAtual) <= 0) { SendTo(s, new NoticeMessage("A Joia da Forja só reforja peça de conjunto com variação (V1–V14).", "warn")); Reply(s, "refine", false, "not_set"); return; }
            if (Variacao.Of(qualAtual) >= Variacao.Max) { SendTo(s, new NoticeMessage("Peça Full (V15) não se reforja.", "info")); Reply(s, "refine", false, "max"); return; }
        }
        if (jewel.DefinitionId == "joia_forja" && Variacao.Of(qualAtual) is > 0 and var vAtual)
        {
            var custo = 2000L * Math.Max(1, def.MinLevel / 5);
            if (s.Coins < custo) { SendTo(s, new NoticeMessage($"Reforjar custa {custo:N0} moedas.", "warn")); Reply(s, "refine", false, "not_enough_coins"); return; }
            var lo = Math.Max(1, vAtual - 2); var hi = Math.Min(14, vAtual + 3);
            var nova = lo + (int)(_rng() * (hi - lo + 1));
            if (!_db.TryReforge(ch, jewel.ItemId, m.TargetItemId, Variacao.Quality(nova), custo)) { Reply(s, "refine", false, "failed"); return; }
            s.Coins = _db.GetCoins(ch);
            SendTo(s, new CoinsMessage(s.Coins, -custo));
            SendInventory(s); SendEquipment(s); ApplyEquipment(s); SendStats(s);
            SendTo(s, new NoticeMessage($"⚒ Reforjado: {def.DisplayName} V{vAtual} → V{nova} ({Variacao.Faixa(nova)})", nova > vAtual ? (nova >= 10 ? "legend" : "reward") : "warn"));
            GameLog.Info("servidor.itens", $"{Who(s)} reforjou {defId}: V{vAtual} → V{nova}");
            Reply(s, "refine", true, "reforge");
            return;
        }
        if (def.AffixPool is not ("physical_weapon" or "armor") && def.Category is not ("weapon" or "armor" or "helmet" or "offhand" or "gloves" or "boots" or "pants" or "pet"))
        { SendTo(s, new NoticeMessage("Joia só refina arma, armadura e pet.", "warn")); Reply(s, "refine", false, "not_gear"); return; }
        if (plus >= Refine.Max) { SendTo(s, new NoticeMessage($"{def.DisplayName} já está no +{Refine.Max}.", "info")); Reply(s, "refine", false, "max"); return; }
        var need = Refine.JewelFor(plus);
        if (jewel.DefinitionId != need)
        {
            var needName = _content.Items.TryGetValue(need, out var nd) ? nd.DisplayName : need;
            SendTo(s, new NoticeMessage($"Para o +{plus + 1} precisa de {needName}.", "warn"));
            Reply(s, "refine", false, "wrong_jewel"); return;
        }
        var next = plus + 1;
        var cost = Refine.Coins(next, def.MinLevel);
        if (s.Coins < cost) { SendTo(s, new NoticeMessage($"O refino para +{next} custa {cost} moedas.", "warn")); Reply(s, "refine", false, "not_enough_coins"); return; }
        var ok = _rng() < Refine.Chance(next);
        var result = ok ? next : Refine.OnFail(plus);
        if (!_db.TryRefine(ch, jewel.ItemId, m.TargetItemId, result, cost)) { Reply(s, "refine", false, "failed"); return; }
        s.Coins = _db.GetCoins(ch);
        SendTo(s, new CoinsMessage(s.Coins, -cost));
        SendInventory(s);
        SendEquipment(s);
        ApplyEquipment(s);
        SendStats(s);
        GameLog.Info("servidor.itens", $"{Who(s)} refinou {defId}: +{plus} → +{result} ({(ok ? "sucesso" : "falhou")})");
        if (ok)
        {
            SendTo(s, new NoticeMessage($"✦ {def.DisplayName} +{next}!", next >= 7 ? "legend" : "reward"));
            if (next >= 7) Achieve(s, "refino7");
            if (next >= 10) Achieve(s, "refino10");
            if (next >= 13) Achieve(s, "refino13");
            if (next >= 9 && s.Zone is not null)
                foreach (var p in s.Zone!.Players.Where(p => !ReferenceEquals(p, s)))
                    SendTo(p, new NoticeMessage($"✦ {s.Name} refinou {def.DisplayName} até +{next}!", "reward"));
        }
        else SendTo(s, new NoticeMessage(result < plus ? $"O refino falhou: {def.DisplayName} caiu para +{result}." : "O refino falhou.", "warn"));
        Reply(s, "refine", ok, ok ? "ok" : "failed_roll");
    }
}

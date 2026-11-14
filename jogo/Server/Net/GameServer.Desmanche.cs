using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Server.Persistence;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (análise do jogo — itens: "desmanchar e reforjar"): com a forja aberta, botão direito num equipamento o desmancha.
/// Volta material pelo nível (ferro raso → ferro de eco → ferro de geada → basalto) e, pela variação, joias:
/// V4+ dá Joia da Maré, V7+ dá mais, V10+ (Excelente) dá Joia da Brasa, V13+ Joia do Vazio. O item some (não tem volta).
/// Item vestido, vinculado ou de missão não desmancha.
/// </summary>
public sealed partial class GameServer
{
    private void HandleSalvage(Session s, SalvageIntent m)
    {
        var entry = _db.GetInventory(s.CharacterId!).FirstOrDefault(e => e.ItemId == m.ItemId);
        if (entry is null || !_content.Items.TryGetValue(entry.DefinitionId, out var d) || d.Category is "material" or "" or "pet" or "wings" or "mount")
        { SendTo(s, new NoticeMessage("Só dá para desmanchar arma e armadura.", "warn")); return; }
        if (Bound(entry.DefinitionId) || d.Price <= 0) { SendTo(s, new NoticeMessage("Esse item não se desmancha (vinculado ou de missão).", "warn")); return; }
        var lv = d.MinLevel;
        var mat = lv >= 48 ? "mat_basalto" : lv >= 40 ? "mat_ferro_geada" : lv >= 20 ? "mat_ferro_eco" : "mat_ferro_raso";
        var v = Variacao.Of(entry.Quality) is > 0 and var vv ? vv : entry.Quality switch { "incomum" => 4, "raro" => 7, "epico" => 9, "chefe" => 10, "lendario" => 13, _ => 1 };
        var premios = new List<GrantItem> { new(mat, 1 + lv / 15 + (int)(_rng() * 2), "comum") };
        if (v >= 4) premios.Add(new GrantItem("joia_mare", v >= 7 ? 2 : 1, "comum"));
        if (v >= 10) premios.Add(new GrantItem("joia_brasa", 1 + (v >= 12 ? 1 : 0), "comum"));
        if (v >= 13) premios.Add(new GrantItem("joia_vazio", 1, "comum"));
        if (entry.Temper > 0) premios.Add(new GrantItem("joia_mare", Math.Min(5, entry.Temper), "comum"));   // parte do refino volta
        premios = premios.Where(p => _content.Items.ContainsKey(p.DefinitionId) || _content.Consumables.ContainsKey(p.DefinitionId)).ToList();
        if (!_db.TryConsumeOne(s.CharacterId!, entry.ItemId).Success) return;
        if (!_db.TryGrantItems(s.CharacterId!, premios, $"desmanche:{_runId}:{s.CharacterId}:{entry.ItemId}"))
        {
            SendTo(s, new NoticeMessage("Mochila cheia: o material caiu no chão? Não — libere espaço antes de desmanchar.", "warn"));
            _db.TryGrantItems(s.CharacterId!, new List<GrantItem> { new(entry.DefinitionId, 1, entry.Quality) }, $"desmanche_volta:{_runId}:{entry.ItemId}");
            SendInventory(s);
            return;
        }
        var texto = string.Join(", ", premios.Select(p => $"{ItemName(p.DefinitionId)} x{p.Quantity}"));
        SendTo(s, new NoticeMessage($"🔨 {d.DisplayName} desmanchado: {texto}.", "reward"));
        GameLog.Info("servidor.itens", $"{Who(s)} desmanchou {entry.DefinitionId} ({entry.Quality}): {texto}");
        SendInventory(s);
    }
}

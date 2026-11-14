using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Server.Persistence;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 14/11 tarde (pedido do dono: "NPCs com a Caixa de Pandora +1 até +5 que tem drops ao jogar no chão"): a Caixa do
/// Kundun do MU. Não abre pela mochila: o jogador JOGA NO CHÃO e ela estoura, espalhando 2+N a 4+N itens da faixa
/// (+1 nível 1–12 ... +5 49–60) em volta — qualquer um perto pode pegar.
/// Cada item: 40% equipamento da classe de quem abriu (incomum → Excelente, melhor quanto maior o +N), 25% joia de
/// refino, 15% tônico, 12% peça de conjunto V, 8% arma do arsenal; +4/+5 têm chance de peça da coleção (asa/capa/skin).
/// Moedas vão direto para quem abriu. 3%: SORTE GRANDE (uma Pandora +N+1 no meio).
/// Onde ganha: chefes (10%), elites (1%), Santuário (teste).
/// </summary>
public sealed partial class GameServer
{
    public static string PandoraId(int tier) => "caixa_pandora_" + Math.Clamp(tier, 1, 5);
    private static int PandoraTier(string defId) => defId.StartsWith("caixa_pandora_") && int.TryParse(defId[^1..], out var n) ? Math.Clamp(n, 1, 5) : 0;

    private GrantItem? PandoraDoMonstro(MonsterInstance m)
    {
        if (m.Master is not null || m.GodSpawned) return null;
        var chance = m.Def.Boss ? 0.10 : m.Def.Elite ? 0.01 : 0.0;
        return chance > 0 && _rng() < chance ? new GrantItem(PandoraId(CaixaTier(m.Level)), 1, "comum") : null;
    }

    /// <summary>Pandora jogada no chão: estoura e espalha o prêmio. Devolve true quando tratou o pedido.</summary>
    private bool TryAbrirPandora(Session s, InventoryEntry entry)
    {
        var tier = PandoraTier(entry.DefinitionId);
        if (tier == 0 || s.Zone is not { } z) return false;
        if (!_db.TryConsumeOne(s.CharacterId!, entry.ItemId).Success) { Reply(s, "drop_item", false, "not_in_inventory"); return true; }
        var lv = CaixaNivel(tier);
        var cls = s.ClassId;
        var premios = new List<GrantItem>();
        var n = 2 + tier + (int)(_rng() * 3);
        string Qualidade()
        {
            var r = _rng();
            var exc = 0.04 + 0.03 * tier;
            return r < exc ? "excelente" : r < exc + 0.10 + 0.03 * tier ? "epico" : r < exc + 0.40 ? "raro" : "incomum";
        }
        var sorte = false;
        for (var i = 0; i < n; i++)
        {
            var r = _rng();
            if (tier >= 4 && r < (tier == 5 ? 0.07 : 0.04) && ColecaoDaPandora(tier) is { } col) { premios.Add(col); continue; }
            if (r < 0.40 && PickClassItem(cls, lv, set: false) is { } arma)
                premios.Add(new GrantItem(arma.Id, 1, Qualidade()));
            else if (r < 0.52 && PickClassItem(cls, lv, set: true) is { } peca)
            {
                var v = Math.Clamp(2 * tier + (int)(_rng() * (tier + 1)), 1, Variacao.Max);
                premios.Add(new GrantItem(peca.Id, 1, Variacao.Quality(v)));
            }
            else if (r < 0.60 && ArsenalPara(cls, lv) is { } ars)
                premios.Add(new GrantItem(ars.Id, 1, Qualidade()));
            else if (r < 0.85)
            {
                var joia = tier >= 4 && _rng() < 0.12 ? "joia_forja" : tier >= 3 && _rng() < 0.25 ? "joia_vazio" : tier >= 2 && _rng() < 0.45 ? "joia_brasa" : "joia_mare";
                premios.Add(new GrantItem(joia, 1 + (int)(_rng() * tier), "comum"));
            }
            else premios.Add(new GrantItem(tier >= 3 ? "consumable_c05_tonico_forte" : tier == 2 ? "consumable_c02_tonico_medio" : "consumable_c01_tonico_fraco", 2, "comum"));
        }
        if (_rng() < 0.03) { premios.Add(new GrantItem(PandoraId(Math.Min(5, tier + 1)), 1, "comum")); sorte = true; }
        var moedas = (long)(lv * (25 + _rng() * 35) * tier);
        var antes = s.Coins;
        s.Coins = _db.AddCoins(s.CharacterId!, moedas, $"pandora_moedas:{_runId}:{s.CharacterId}:{entry.ItemId}:{NowMs}");
        SendTo(s, new CoinsMessage(s.Coins, s.Coins - antes));
        EspalharNoChao(s, premios, $"pandora:{_runId}:{s.CharacterId}:{entry.ItemId}:{NowMs}");
        var bom = premios.Count(p => p.Quality is "excelente" || Variacao.Of(p.Quality) >= 10 || ColecaoRaras.Contains(p.DefinitionId) || ColecaoEpicas.Contains(p.DefinitionId) || ColecaoLendarias.Contains(p.DefinitionId));
        var nome = ItemName(entry.DefinitionId);
        SendTo(s, new NoticeMessage($"💥 {nome} estourou: {premios.Count} itens no chão e {moedas:N0} moedas!" + (sorte ? " SORTE GRANDE: tem outra Pandora no meio!" : ""), bom > 0 || sorte ? "legend" : "reward"));
        if (bom > 0 || sorte) Broadcast(z, new NoticeMessage($"{s.Name} estourou uma {nome} — tem coisa boa no chão!", "legend"));
        GameLog.Info("servidor.itens", $"{Who(s)} estourou {entry.DefinitionId}: {string.Join(", ", premios.Select(p => $"{p.DefinitionId}[{p.Quality}]x{p.Quantity}"))} +{moedas} moedas");
        Reply(s, "drop_item", true, "ok");
        SendInventory(s);
        return true;
    }

    /// <summary>14/11: espalha os itens em volta do jogador, no chão (qualquer um perto pode pegar).</summary>
    private void EspalharNoChao(Session s, List<GrantItem> premios, string origem)
    {
        if (s.Zone is not { } z) return;
        var agora = NowMs;
        for (var i = 0; i < premios.Count; i++)
        {
            var p = premios[i];
            var ang = i * (Math.PI * 2 / premios.Count) + _rng() * 0.4;
            var raio = 1.2 + _rng() * 1.3;
            var (x, y) = z.Geo.Resolve(s.X + Math.Cos(ang) * raio, s.Y + Math.Sin(ang) * raio, 0.3);
            var id = _db.CreateWorldItemAt(p.DefinitionId, p.Quality, $"{origem}:{i}", x, y, p.Quantity, z.Id);
            z.Items[id] = new WorldItemView(id, p.DefinitionId, x, y, p.Quality);
            z.ItemExpiry[id] = agora + DropLifetimeMs;
        }
    }

    private GrantItem? ColecaoDaPandora(int tier)
    {
        var lista = tier == 5 && _rng() < 0.25 ? ColecaoLendarias : tier == 5 || _rng() < 0.3 ? ColecaoEpicas : ColecaoRaras;
        var ok = lista.Where(_content.Items.ContainsKey).ToList();
        return ok.Count == 0 ? null : new GrantItem(ok[(int)(_rng() * ok.Count) % ok.Count], 1, "comum");
    }

    private ItemDefinition? ArsenalPara(string cls, int lv)
    {
        var pool = _content.Items.Values.Where(d => d.Id.StartsWith("arsenal_") && d.MinLevel <= lv + 2 && d.MinLevel >= lv - 14
            && (d.AllowedClasses.Count == 0 || d.AllowedClasses.Contains(cls))).ToList();
        return pool.Count == 0 ? null : pool[(int)(_rng() * pool.Count) % pool.Count];
    }
}

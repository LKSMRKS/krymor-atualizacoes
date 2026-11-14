using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.World;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Server.Persistence;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (pedido do dono: "as missões precisam ter recompensas melhores... ou até um sistema de baú, que abre e ganha algo,
/// tipo as box +1 até +5, podendo vir itens interessantes"): Caixa das Cinzas +1..+5 (estilo Caixa do Kundun do MU).
/// <list type="bullet">
/// <item>+1 nível 1–12 · +2 13–24 · +3 25–36 · +4 37–48 · +5 49–60 (a faixa do prêmio).</item>
/// <item>Abrir (usar na mochila) sorteia UM prêmio: moedas, joia de refino, peça de conjunto da sua classe numa variação boa,
/// arma da sua classe, decoração da casa — e às vezes o "uou": V10+ ou uma caixa a mais. Sempre vem junto 2 tônicos.</item>
/// <item>Onde ganha: missões (as grandes sempre, as outras às vezes — <see cref="CaixaDaMissao"/>), chefes (35%), elites (4%),
/// chefe de campo (sempre) e eventos.</item>
/// </list>
/// </summary>
public sealed partial class GameServer
{
    public static int CaixaTier(int level) => Math.Clamp(1 + (Math.Max(1, level) - 1) / 12, 1, 5);
    public static string CaixaId(int tier) => "caixa_cinzas_" + Math.Clamp(tier, 1, 5);
    private static int CaixaNivel(int tier) => tier switch { 1 => 10, 2 => 22, 3 => 34, 4 => 46, _ => 58 };

    /// <summary>Missão grande (história, chefe, caçada, escolta, defesa, investigação): sempre; as outras: 30%.</summary>
    public static bool CaixaGarantida(QuestDef q)
        => q.Category is "historia" or "historia_principal" || q.Kind is "boss" or "hunt" or "escort" or "defend" or "interact";

    private GrantItem? CaixaDaMissao(Session s, QuestDef q)
    {
        if (q.Board is not null || q.Daily || q.Kind == "reset") return null;   // contratos e diárias já pagam em dobro
        if (!CaixaGarantida(q) && _rng() >= 0.30) return null;
        return new GrantItem(CaixaId(CaixaTier(Math.Max(q.MinLevel, 1))), 1, "comum");
    }

    private GrantItem? CaixaDoMonstro(MonsterInstance m)
    {
        // 13/11: o raro (Caranguejo Dourado, Sapo Dourado...) SEMPRE solta uma caixa; qualquer monstro, raramente (0,3%)
        var chance = m.Def.Rare || (m.Spawn?.RespawnSeconds >= 300 && m.Def.Boss) ? 1.0 : m.Def.Boss ? 0.35 : m.Def.Elite ? 0.04
            : m.Master is null && !m.GodSpawned ? 0.003 : 0.0;
        return chance > 0 && _rng() < chance ? new GrantItem(CaixaId(CaixaTier(m.Level)), 1, "comum") : null;
    }

    private bool TryOpenCaixa(Session s, string itemId, ConsumableDefinition c, bool noChao = false)
    {
        if (c.Caixa <= 0) return false;
        var tier = c.Caixa;
        var lv = CaixaNivel(tier);
        var cls = s.ClassId;
        var premios = new List<GrantItem> { new(tier >= 3 ? "consumable_c05_tonico_forte" : tier == 2 ? "consumable_c02_tonico_medio" : "consumable_c01_tonico_fraco", 2, "comum") };
        long moedas = 0;
        string texto;
        var r = _rng();
        // variação da peça: 2t a 3t (a +5 chega a V15 raramente)
        int Var() { var v = 2 * tier + (int)(_rng() * (tier + 1)); if (_rng() < 0.06) v += 3; return Math.Clamp(v, 1, Variacao.Max); }
        if (r < 0.30)
        {
            moedas = (long)(lv * (40 + _rng() * 60) * tier);
            texto = $"{moedas:N0} moedas";
        }
        else if (r < 0.34 && tier >= 2)
        {
            // 13/11: skin de bracelete (coelho, executivo, cavalheiro, palhaço, bobo)
            var skins = _content.Items.Values.Where(i => i.Category == "bracelet" && i.MinLevel <= Math.Max(lv, 1) && i.Id != "bracelete_ouro").ToList();   // 14/11: o de ouro só cai de chefe
            if (skins.Count == 0) { moedas = lv * 100L * tier; texto = $"{moedas:N0} moedas"; }
            else
            {
                var sk = skins[(int)(_rng() * skins.Count) % skins.Count];
                premios.Add(new GrantItem(sk.Id, 1, "comum"));
                texto = sk.DisplayName;
            }
        }
        else if (r < 0.50)
        {
            var joia = tier >= 3 && _rng() < 0.15 ? "joia_forja" : tier >= 4 && _rng() < 0.25 ? "joia_vazio" : tier >= 2 && _rng() < 0.45 ? "joia_brasa" : "joia_mare";
            var n = 1 + (int)(_rng() * tier);
            premios.Add(new GrantItem(joia, n, "comum"));
            texto = $"{ItemName(joia)} x{n}";
        }
        else if (r < 0.72 && PickClassItem(cls, lv, set: true) is { } peca)
        {
            var v = Var();
            premios.Add(new GrantItem(peca.Id, 1, Variacao.Quality(v)));
            texto = $"{peca.DisplayName} V{v}";
        }
        else if (r < 0.90 && PickClassItem(cls, lv, set: false) is { } arma)
        {
            var q = tier >= 4 ? "epico" : tier >= 2 ? "raro" : "incomum";
            premios.Add(new GrantItem(arma.Id, 1, q));
            texto = arma.DisplayName;
        }
        else if (r < 0.97 && _content.Consumables.Values.Where(d => d.DecorPoints > 0 && !s.Flags.Contains("decor:" + d.Id)).ToList() is { Count: > 0 } decs)
        {
            var d = decs[(int)(_rng() * decs.Count)];
            premios.Add(new GrantItem(d.Id, 1, "comum"));
            texto = $"{d.DisplayName} (decoração)";
        }
        else if (tier >= 3 && _rng() < 0.5 && new[] { "urso", "leao", "lince", "dinossauro", "avestruz", "condor" }
                     .Where(k => !s.Flags.Contains("mount:" + k)).ToList() is { Count: > 0 } novas)
        {
            // 13/11: a SORTE GRANDE das caixas +3 a +5 pode ser uma das 6 montarias 3D novas
            var k = novas[(int)(_rng() * novas.Count)];
            GrantFlags(s, new[] { "mount:" + k });
            EscolherMontaria(s, k);
            texto = $"SORTE GRANDE: montaria nova — {NomeMontaria(k)}!";
        }
        else
        {
            // o "uou": outra caixa (uma acima, se houver) + 3 joias
            premios.Add(new GrantItem(CaixaId(Math.Min(5, tier + 1)), 1, "comum"));
            premios.Add(new GrantItem("joia_brasa", 3, "comum"));
            texto = $"SORTE GRANDE: Caixa das Cinzas +{Math.Min(5, tier + 1)} e 3 Joias da Brasa";
        }
        if (!_db.TryConsumeOne(s.CharacterId!, itemId).Success) { Reply(s, noChao ? "drop_item" : "use_item", false, "not_in_inventory"); return true; }
        if (noChao)   // 14/11 (dono: "joguei a caixa no chão e caiu a própria caixa"): jogada no chão, ela estoura e o prêmio cai em volta
            EspalharNoChao(s, premios, $"caixa_chao:{_runId}:{s.CharacterId}:{itemId}:{NowMs}");
        else if (!_db.TryGrantItems(s.CharacterId!, premios, $"caixa:{_runId}:{s.CharacterId}:{itemId}:{NowMs}"))
        {
            // mochila cheia: devolve a caixa
            _db.TryGrantItems(s.CharacterId!, new List<GrantItem> { new(c.Id, 1, "comum") }, $"caixa_volta:{_runId}:{s.CharacterId}:{itemId}:{NowMs}");
            SendTo(s, new NoticeMessage("Mochila cheia: libere espaço para abrir a caixa.", "warn"));
            SendInventory(s);
            Reply(s, "use_item", false, "inventory_full");
            return true;
        }
        if (moedas > 0)
        {
            var antes = s.Coins;
            s.Coins = _db.AddCoins(s.CharacterId!, moedas, $"caixa_moedas:{_runId}:{s.CharacterId}:{itemId}:{NowMs}");
            SendTo(s, new CoinsMessage(s.Coins, s.Coins - antes));
        }
        var grande = texto.StartsWith("SORTE") || premios.Any(p => Variacao.Of(p.Quality) >= 10);
        if (texto.StartsWith("SORTE")) Achieve(s, "sorte");
        SendTo(s, new NoticeMessage($"🎁 {c.DisplayName}: {texto}!", grande ? "legend" : "reward"));
        if (grande && s.Zone is { } z) Broadcast(z, new NoticeMessage($"{s.Name} abriu uma {c.DisplayName} e tirou {texto}!", "legend"));
        SendTo(s, new LootNoticeMessage("caixa:" + c.Id, premios.Select(p => p.DefinitionId).ToList()));
        GameLog.Info("servidor.itens", $"{Who(s)} abriu {c.Id}: {texto}");
        SendInventory(s);
        Reply(s, noChao ? "drop_item" : "use_item", true, "ok");
        return true;
    }

    private static string NomeMontaria(string k) => k switch
    {
        "urso" => "Urso de Guerra", "leao" => "Leão Dourado", "lince" => "Lince das Cinzas", "dinossauro" => "Raptor de Guerra",
        "avestruz" => "Avestruz de Corrida", "condor" => "Condor Gigante", _ => k,
    };

    private ItemDefinition? PickClassItem(string cls, int lv, bool set)
    {
        var pool = _content.Items.Values.Where(d => (d.AllowedClasses.Count == 0 || d.AllowedClasses.Contains(cls))
            && d.MinLevel <= lv + 2 && d.MinLevel >= lv - 12
            && (set ? d.Set is { Length: > 0 } && d.Category is "helmet" or "armor" or "gloves" or "pants" or "boots"
                    : d.Category == "weapon" && d.AllowedClasses.Count > 0)).ToList();
        return pool.Count == 0 ? null : pool[(int)(_rng() * pool.Count)];
    }
}

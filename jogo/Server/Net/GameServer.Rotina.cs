using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (análise do jogo, Leva 1 — "começo" e "proposta, tempo e farm", aprovados pelo dono):
/// • Caminho principal: missões marcadas com ★ (nome, diário e marcador dourado no NPC) — o jogador novo sabe o que fazer primeiro.
/// • Ondas: cada NPC mostra no máximo 3 missões NOVAS por vez (as próximas aparecem ao aceitar/entregar).
/// • Quadro de contratos do Porto: 3 contratos diários sorteados por personagem e dia, na faixa do seu nível.
/// • Caçada da semana: uma espécie (e o mapa dela) com XP e drop em dobro; troca toda segunda.
/// • Dungeon em grupo: +10% de XP por companheiro (além do bônus normal de grupo).
/// • Falas: o NPC agradece pelo que você já fez por ele (fala muda com o progresso).
/// </summary>
public sealed partial class GameServer
{
    /// <summary>Linha principal: começo da Costa + a Crônica + as missões de história que fecham cada região.</summary>
    internal static readonly HashSet<string> CaminhoPrincipal = new(StringComparer.Ordinal)
    {
        "q_primeira_arma", "q_batedora", "q_rei", "q_recado_vale", "q_capataz",
        "q_m16", "q_cad_canions_3", "q_m32", "q_cad_vespera_3", "q_m44", "q_m55", "q_m61", "q_m64",
    };

    internal bool Principal(QuestDef q) => CaminhoPrincipal.Contains(q.Id) || q.Category == "historia_principal";

    private string Titulo(QuestDef q) => Principal(q) ? "★ " + q.Name : q.Name;

    /// <summary>No máximo 3 missões novas por NPC (as do caminho principal primeiro, depois as de menor nível).</summary>
    private List<QuestOffer> Ondas(List<QuestOffer> offers)
    {
        var novas = offers.Where(o => o.State == "available").ToList();
        if (novas.Count <= 3) return offers;
        var mostrar = novas.OrderBy(o => o.Name.StartsWith("★") ? 0 : 1)
            .ThenBy(o => _world!.Quests.TryGetValue(o.Id, out var q) ? q.MinLevel : 99).ThenBy(o => o.Id, StringComparer.Ordinal)
            .Take(3).Select(o => o.Id).ToHashSet();
        return offers.Where(o => o.State != "available" || mostrar.Contains(o.Id)).ToList();
    }

    // ------------------------------------------------------------ contratos do dia

    private static string HojeBrasilia() => DateTime.UtcNow.AddHours(-3).ToString("yyyyMMdd");

    private static int Mistura(string a)
    {
        unchecked { var h = 17; foreach (var ch in a) h = h * 31 + ch; return h & 0x7fffffff; }
    }

    /// <summary>Os 3 contratos de hoje para este personagem: os da faixa do nível dele (até 10 abaixo), sorteados pelo dia.</summary>
    private HashSet<string> ContratosDoDia(Session s)
    {
        var dia = HojeBrasilia();
        if (s.ContratosDia == dia && s.ContratosNivel == s.Level && s.Contratos is not null) return s.Contratos;
        var pool = _world!.Quests.Values.Where(q => q.Board == "contratos" && q.MinLevel <= s.Level && q.MinLevel >= s.Level - 10).ToList();
        if (pool.Count < 3) pool = _world.Quests.Values.Where(q => q.Board == "contratos" && q.MinLevel <= s.Level).OrderByDescending(q => q.MinLevel).Take(6).ToList();
        s.Contratos = pool.OrderBy(q => Mistura(dia + s.CharacterId + q.Id)).Take(3).Select(q => q.Id).ToHashSet();
        s.ContratosDia = dia; s.ContratosNivel = s.Level;
        return s.Contratos;
    }

    private sealed partial class Session
    {
        public HashSet<string>? Contratos;
        public string ContratosDia = "";
        public int ContratosNivel;
        public string? RegiaoNova;
    }

    // ------------------------------------------------------------ caçada da semana

    private (string Monstro, string Mapa, string Nome, string NomeMapa)? _cacada;
    private int _cacadaSemana = -1;

    /// <summary>Espécie da semana (comum, de mapa de campo do nível 10 em diante); troca toda segunda (horário de Brasília).</summary>
    internal (string Monstro, string Mapa, string Nome, string NomeMapa)? CacadaDaSemana()
    {
        if (_world is null) return null;
        var agora = DateTime.UtcNow.AddHours(-3);
        var semana = (int)((agora.Date - new DateTime(2026, 1, 5)).TotalDays / 7);   // 05/01/2026 foi segunda
        if (semana == _cacadaSemana) return _cacada;
        var lista = _world.Maps.Values.Where(l => !l.Instanced && l.Spawns.Count > 0 && l.Waypoints.Count > 0 && l.Id is not ("santuario_arena" or "costa_do_cal"))
            .SelectMany(l => l.Spawns.Select(sp => (sp.Monster, l)))
            .Where(x => _content.Monsters.TryGetValue(x.Monster, out var d) && !d.Boss && !d.Elite)
            .GroupBy(x => x.Monster).Select(g => g.First()).OrderBy(x => x.Monster, StringComparer.Ordinal).ToList();
        _cacadaSemana = semana;
        if (lista.Count == 0) return _cacada = null;
        var (mon, lay) = lista[Mistura("cacada" + semana) % lista.Count];
        return _cacada = (mon, lay.Id, _content.Monsters[mon].SpeciesName, lay.DisplayName);
    }

    /// <summary>Multiplicador de XP da rotina: caçada da semana ×2; dungeon em grupo +10% por companheiro.</summary>
    private double RotinaXp(MonsterInstance m, int participantes)
    {
        var mult = 1.0;
        if (CacadaDaSemana() is { } c && c.Monstro == m.Def.Id && c.Mapa == m.Zone.LayoutId) mult *= 2.0;
        if (m.Zone.Instanced && participantes > 1) mult *= 1.0 + 0.10 * (participantes - 1);
        return mult * EventoXp();
    }

    // ------------------------------------------------------------ falas que mudam

    /// <summary>Fala do NPC: o quadro conta a caçada; quem já recebeu ajuda agradece pela última missão entregue.</summary>
    private string Fala(Session s, MapNpc npc)
    {
        if (npc.Role == "placar")
        {
            var p = _db.PlacarCompleto();
            string Top(string titulo, Func<(string Nome, int Nivel, int Renasceu, int Refino, int Conquistas, int Golpe, int Chefes, int Duelos, int Moedas), int> chave, string sufixo)
            {
                var top = p.Where(x => chave(x) > 0).OrderByDescending(chave).ThenBy(x => x.Nome).Take(3).ToList();
                return "• " + titulo + ": " + (top.Count == 0 ? "ninguém ainda" : string.Join(" · ", top.Select((x, i) => $"{(i == 0 ? "🥇" : i == 1 ? "🥈" : "🥉")} {x.Nome} {chave(x):N0}{sufixo}")));
            }
            return npc.Greeting + "\n\n" + string.Join("\n", new[]
            {
                Top("Maior nível", x => x.Nivel, ""),
                Top("Golpe mais forte", x => x.Golpe, " de dano"),
                Top("Chefes diferentes vencidos", x => x.Chefes, ""),
                Top("Duelos ganhos", x => x.Duelos, ""),
                Top("Maior refino vestido", x => x.Refino, ""),
                Top("Mais conquistas", x => x.Conquistas, ""),
                Top("Mais renascimentos", x => x.Renasceu, "×"),
                Top("Mais moedas no bolso", x => x.Moedas, ""),
            });
        }
        if (npc.Role == "recados") return FalaRecados(npc);
        if (npc.Role == "contratos")
        {
            var c = CacadaDaSemana();
            return npc.Greeting + (c is { } cc ? $"\n\nCaçada da semana: {cc.Nome} ({cc.NomeMapa}) — XP e drop em dobro até segunda." : "");
        }
        // 13/11 ("convite para a mesa ficou confuso"): quem é o alvo de uma conversa pedida por outro NPC responde ao recado
        var recado = _world!.Quests.Values.FirstOrDefault(q => q.Kind == "talk" && q.Targets.Count > 0 && q.Targets[0] == npc.Id && q.Giver != npc.Id
            && s.Quests.TryGetValue(q.Id, out var rs) && rs.State == "active");
        if (recado is not null)
        {
            var quem = _world.Maps.Values.SelectMany(l => l.Npcs).FirstOrDefault(n => n.Id == recado.Giver)?.Name.Split(',')[0] ?? "quem te mandou";
            return recado.Id == "q_mesa_vinteum"
                ? $"«O Dado te mandou aqui? Aquele lá só deixa sentar quem paga as contas... e você paga. Pode subir: diga que a Gilda garante você.» (Volte ao Dado, no 2º andar do casarão.)"
                : $"«Ah, recado de {quem}? Entendi. Pode dizer que está tudo certo.» (Volte a {quem}.)";
        }
        var feitas = _world!.Quests.Values.Where(q => q.Giver == npc.Id && !q.Repeatable && s.Quests.TryGetValue(q.Id, out var st) && st.State == "done")
            .OrderByDescending(q => q.MinLevel).ToList();
        if (feitas.Count == 0) return npc.Greeting;
        var ultima = feitas[0];
        var pendentes = _world.Quests.Values.Count(q => q.Giver == npc.Id && !q.Repeatable && !(s.Quests.TryGetValue(q.Id, out var st2) && st2.State == "done"));
        var nome = s.Name ?? "viajante";
        var agradece = (Mistura(npc.Id) % 4) switch
        {
            0 => $"«{nome}! Ainda falam de \"{ultima.Name}\" por aqui — graças a você.»",
            1 => $"«Olha quem voltou. Depois de \"{ultima.Name}\", eu durmo melhor.»",
            2 => $"«{nome}, \"{ultima.Name}\" mudou as coisas por aqui. Obrigado.»",
            _ => $"«Não esqueci \"{ultima.Name}\". Se precisar de algo, estou aqui.»",
        };
        return pendentes == 0 ? agradece + " «Você já fez tudo o que eu podia pedir.»" : agradece + "\n\n" + npc.Greeting;
    }
}

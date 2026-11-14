using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 12/11 (pedido do dono: "desafiar o amigo para um x1, dentro do P"): duelo entre dois membros do mesmo grupo. Quem desafia
/// manda pelo painel do grupo; o outro aceita com Y. Os dois vão para uma Arena só deles (cópia do Santuário da Arena), um de
/// frente para o outro, com vida cheia. 14/11: sem viagem — o x1 começa onde os dois estão (mesmo mapa, até 30 m). Os golpes de área, cone, giro, investida e projéteis acertam o adversário; o dano é
/// uma fração da vida máxima dele (para equilibrar níveis e equipamentos diferentes). Quem cai a 10% perde — ninguém morre,
/// ninguém perde XP nem item; o placar fica gravado (marcas "x1v:" vitórias). Os dois voltam para onde estavam.
/// </summary>
public sealed partial class GameServer
{
    private const double DuelRange = 30.0;   // 14/11: x1 ali mesmo, com o outro por perto
    private readonly Dictionary<string, (string From, long Until)> _duelInvites = new();

    private sealed partial class Session
    {
        public Session? DuelWith;
        public (string Map, double X, double Y)? DuelReturn;
    }

    private bool HandleDuelIntent(Session s, PartyIntent m)
    {
        // 13/11 ("meu amigo aceitou o x1 e não deu nada"): cliente antigo responde ao desafio com o "aceitar" do grupo —
        // se há um desafio pendente para ele, vale como aceitar/recusar o x1
        var acao = m.Action;
        if (acao is "accept" or "decline" && _duelInvites.TryGetValue(s.CharacterId!, out var pend) && NowMs <= pend.Until)
            acao = acao == "accept" ? "duelo_aceitar" : "duelo_recusar";
        switch (acao)
        {
            case "duelo":
            {
                var alvo = _sessions.Values.FirstOrDefault(x => x.LoggedIn && string.Equals(x.Name, m.Target?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (alvo is null || ReferenceEquals(alvo, s)) { Reply(s, "party", false, "not_online"); return true; }
                if (!_partyOf.TryGetValue(s.CharacterId!, out var pa) || !pa.Members.Contains(alvo.CharacterId!))
                { SendTo(s, new NoticeMessage("Só dá para desafiar quem está no seu grupo (P).", "warn")); return true; }
                if (s.DuelWith is not null || alvo.DuelWith is not null) { SendTo(s, new NoticeMessage("Alguém já está num duelo.", "warn")); return true; }
                _duelInvites[alvo.CharacterId!] = (s.CharacterId!, NowMs + InviteLifetimeMs);
                SendTo(alvo, new PartyInviteMessage(s.CharacterId!, s.Name!, "duelo"));
                SendTo(s, new NoticeMessage($"Desafio enviado para {alvo.Name}. Ele aceita com Y (30 s).", "info"));
                GameLog.Info("servidor.grupo", $"{Who(s)} desafiou {Who(alvo)} para um x1");
                return true;
            }
            case "duelo_recusar":
                if (_duelInvites.Remove(s.CharacterId!, out var rec) && _sessions.Values.FirstOrDefault(x => x.CharacterId == rec.From) is { } quem)
                    SendTo(quem, new NoticeMessage($"{s.Name} recusou o x1.", "info"));
                return true;
            case "duelo_aceitar":
            {
                if (!_duelInvites.Remove(s.CharacterId!, out var inv) || NowMs > inv.Until)
                { SendTo(s, new NoticeMessage("O desafio expirou (30 s). Peça para desafiar de novo.", "warn")); GameLog.Info("servidor.grupo", $"{Who(s)} aceitou x1 vencido"); return true; }
                var rival = _sessions.Values.FirstOrDefault(x => x.CharacterId == inv.From && x.LoggedIn);
                if (rival is null || rival.DuelWith is not null || s.DuelWith is not null)
                { SendTo(s, new NoticeMessage("Não deu para começar o x1: o outro saiu ou já está num duelo.", "warn")); return true; }
                // 14/11 ("o x1 é pra ativar onde você estiver, não levar pra lugar nenhum"): os dois lutam ali mesmo
                if (!ReferenceEquals(rival.Zone, s.Zone) || Distance(rival.X, rival.Y, s.X, s.Y) > DuelRange)
                {
                    foreach (var p in new[] { s, rival })
                        SendTo(p, new NoticeMessage($"Para o x1, fiquem no mesmo mapa e perto um do outro (até {DuelRange:0} m).", "warn"));
                    return true;
                }
                StartDuel(rival, s);
                return true;
            }
        }
        return false;
    }

    private void StartDuel(Session a, Session b)
    {
        foreach (var p in new[] { a, b }) p.Combat!.Heal(p.Combat.MaxHealth);   // ali mesmo, com vida cheia
        a.DuelWith = b; b.DuelWith = a;
        foreach (var p in new[] { a, b })
            SendTo(p, new NoticeMessage($"X1: {a.Name} contra {b.Name}! Quem chegar a 10% de vida perde. Ninguém perde nada.", "legend"));
        GameLog.Info("servidor.grupo", $"x1 {Who(a)} x {Who(b)} em {a.Zone?.LayoutId}");
    }

    /// <summary>Golpe do duelista no adversário (chamado nas mesmas formas de alvo dos monstros).</summary>
    private void DuelHit(Session atk, Shared.SkillDefinition skill, double extra = 1.0)
    {
        var alvo = atk.DuelWith;
        if (alvo is null || !ReferenceEquals(alvo.Zone, atk.Zone) || !alvo.Combat!.Alive) return;
        var now = NowMs;
        if (now < alvo.InvulnerableUntilMs) return;
        var nivel = Math.Clamp((double)atk.Level / Math.Max(1, alvo.Level), 0.6, 1.6);
        var dano = Math.Max(1, alvo.Combat.MaxHealth * 0.06 * Math.Clamp(skill.Coefficient, 1.0, 3.5) * nivel * atk.Gear.PowerMult * extra
                                * (1 - Math.Clamp(alvo.Gear.DamageReductionPct, 0, 0.5)));
        var piso = alvo.Combat.MaxHealth * 0.10;
        dano = Math.Min(dano, Math.Max(0, alvo.Combat.Health - piso));
        if (dano > 0) alvo.Combat.TakeDamage(dano);
        Broadcast(atk.Zone!, new CombatEvent(atk.CharacterId!, alvo.CharacterId!, skill.Id, (int)Math.Round(dano), false, false));
        if (alvo.Combat.Health <= piso + 0.5) EndDuel(atk, alvo);
    }

    private void EndDuel(Session vencedor, Session perdedor)
    {
        foreach (var p in new[] { vencedor, perdedor })
            SendTo(p, new NoticeMessage($"X1: {vencedor.Name} venceu {perdedor.Name}!", "legend"));
        var marca = "x1v:" + perdedor.CharacterId + ":" + NowMs;
        _db.TryAddFlag(vencedor.CharacterId!, marca);
        vencedor.Flags.Add(marca);
        Achieve(vencedor, "x1");
        if (vencedor.Flags.Count(f => f.StartsWith("x1v:", StringComparison.Ordinal)) >= 10) Achieve(vencedor, "x1_10");
        GameLog.Info("servidor.grupo", $"x1 venceu {Who(vencedor)} contra {Who(perdedor)}");
        foreach (var p in new[] { vencedor, perdedor })
        {
            p.DuelWith = null;
            p.Combat!.Heal(p.Combat.MaxHealth);
        }
    }

    /// <summary>Saiu do jogo ou do mapa no meio do duelo: o outro vence.</summary>
    private void DuelAbandon(Session s)
    {
        if (s.DuelWith is { } o) { s.DuelWith = null; o.DuelWith = null; SendTo(o, new NoticeMessage($"X1: {s.Name} saiu — vitória sua.", "legend")); }
    }
}

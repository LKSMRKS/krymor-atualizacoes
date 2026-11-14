using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 12/11 (pedido do dono: "casa com decoração, jukebox, buff de decoração, convidar o grupo pelo P"):
/// • Decoração: itens "decor_*" (lojas do Monte do Prefeito) usados DENTRO da sua casa viram marca "decor:id" e aparecem lá
///   (cada peça uma vez). Cada peça vale pontos; ao SAIR de casa você leva o "Conforto do Lar" (1 h), mais forte quanto mais
///   pontos — não substitui refeição ativa de outro tipo.
/// • Convite: dono da casa (marca casa:propria) convida o grupo (P → "Convidar para a casa"); por 10 min, quem foi convidado
///   entra pela mesma porta do Porto e cai na casa do dono.
/// • Passagens com nível TRAVADO (level_lock) e passagens que pedem ASAS equipadas (requires_flag "asas").
/// </summary>
public sealed partial class GameServer
{
    private const string HouseMap = "casa_jogador";
    private const long HouseInviteMs = 10 * 60 * 1000;
    internal static readonly (int Pontos, string Buff)[] DecorTiers = { (170, "buff_casa_4"), (100, "buff_casa_3"), (50, "buff_casa_2"), (10, "buff_casa_1") };

    private sealed partial class Session
    {
        public (string OwnerId, string OwnerName, long Until)? HouseInvite;
    }

    internal int DecorPoints(IEnumerable<string> flags)
        => flags.Where(f => f.StartsWith("decor:", StringComparison.Ordinal))
            .Sum(f => _content.Consumables.TryGetValue(f[6..], out var c) ? c.DecorPoints : 0);

    /// <summary>Usar uma peça de decoração: só dentro da sua própria casa; vira marca e some da mochila.</summary>
    private bool TryUseDecor(Session s, string itemId, ConsumableDefinition c)
    {
        if (c.DecorPoints <= 0) return false;
        if (s.Zone?.LayoutId != HouseMap || s.Zone.OwnerKey != "casa:" + s.CharacterId)
        {
            SendTo(s, new NoticeMessage($"{c.DisplayName}: use dentro da SUA casa (porta atrás da praça do Porto).", "warn"));
            Reply(s, "use_item", false, "wrong_place");
            return true;
        }
        if (s.Flags.Contains("decor:" + c.Id))
        {
            SendTo(s, new NoticeMessage($"{c.DisplayName} já está na sua casa.", "warn"));
            Reply(s, "use_item", false, "already");
            return true;
        }
        if (!_db.TryConsumeOne(s.CharacterId!, itemId).Success) { Reply(s, "use_item", false, "not_in_inventory"); return true; }
        s.Flags.Add("decor:" + c.Id);
        _db.TryAddFlag(s.CharacterId!, "decor:" + c.Id);
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        var pts = DecorPoints(s.Flags);
        var prox = DecorTiers.Reverse().FirstOrDefault(t => t.Pontos > pts);
        SendTo(s, new NoticeMessage($"{c.DisplayName} colocado! Conforto da casa: {pts} pontos" + (prox.Buff is null ? " (máximo)." : $" (próximo nível de bônus em {prox.Pontos})."), "reward"));
        Reply(s, "use_item", true, "ok");
        SendInventory(s);
        return true;
    }

    /// <summary>Saiu da própria casa: leva o bônus de conforto (se não houver outra refeição ativa mais longa).</summary>
    private void HouseComfortOnLeave(Session s, Zone? from)
    {
        if (from?.LayoutId != HouseMap || !MapMode) return;
        var pts = DecorPoints(s.Flags);
        var tier = DecorTiers.FirstOrDefault(t => pts >= t.Pontos);
        if (tier.Buff is null || !_content.Consumables.TryGetValue(tier.Buff, out var buff)) return;
        var ativo = ActiveBuff(s);
        if (ativo is not null && !ativo.Id.StartsWith("buff_casa_", StringComparison.Ordinal)) return;   // refeição tem prioridade
        ApplyFood(s, buff);
        SendTo(s, new NoticeMessage($"{buff.DisplayName}: {ConsumableSummary(buff)}", "reward"));
    }

    private bool HandleHouseIntent(Session s, PartyIntent m)
    {
        if (m.Action != "casa") return false;
        if (!s.Flags.Contains("casa:propria")) { SendTo(s, new NoticeMessage("Você ainda não tem casa (o Prefeito Belmiro, no Monte do Prefeito, vende a escritura).", "warn")); return true; }
        if (!_partyOf.TryGetValue(s.CharacterId!, out var party)) { SendTo(s, new NoticeMessage("Monte um grupo (P) para convidar alguém.", "warn")); return true; }
        var n = 0;
        foreach (var o in _sessions.Values.Where(x => x.LoggedIn && !ReferenceEquals(x, s) && party.Members.Contains(x.CharacterId!)))
        {
            o.HouseInvite = (s.CharacterId!, s.Name!, NowMs + HouseInviteMs);
            SendTo(o, new NoticeMessage($"{s.Name} convidou você para a casa dele(a)! Entre pela porta da casa, atrás da praça do Porto (10 min).", "legend"));
            n++;
        }
        SendTo(s, new NoticeMessage(n == 0 ? "Ninguém do grupo está online." : $"Convite enviado para {n} do grupo.", "info"));
        return true;
    }

    /// <summary>Convite válido para a casa de alguém (dono online ou não).</summary>
    private string? HouseGuestKey(Session s)
        => s.HouseInvite is { } c && NowMs <= c.Until ? "casa:" + c.OwnerId : null;

    /// <summary>Checagens extra de passagem: asas e nível travado. false = não passa (já avisou).</summary>
    private bool PortalGateOk(Session s, MapPortal portal, long now)
    {
        string? motivo = null;
        if (portal.RequiresFlag == "asas")
        {
            var w = OwnedWings(s);
            if (w is null || w.StartsWith("capa:", StringComparison.Ordinal)) motivo = "só se atravessa voando — equipe asas (Máquina do Caos, Santuário Celeste)";
        }
        if (motivo is null && portal.LevelLock && s.Level < portal.MinLevel)
            motivo = $"ainda não — é preciso nível {portal.MinLevel} (você é {s.Level})";
        if (motivo is null) return true;
        if (now >= s.PortalWarnAtMs)
        {
            s.PortalWarnAtMs = now + 3000;
            SendTo(s, new NoticeMessage($"{portal.Label}: {motivo}.", "warn"));
        }
        return false;
    }
}

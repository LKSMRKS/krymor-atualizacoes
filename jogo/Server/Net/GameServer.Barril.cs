using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 11/11 (pedido: "a missão do muro está sem graça, só clicar"): a parede rachada agora cai com um barril de pólvora de
/// verdade. 1) Pega o barril na pilha da Nara (objeto "barris_polvora"): pesado, anda devagar e não ataca. 2) Leva até a
/// rachadura e aperte F: o barril fica no chão. 3) F de novo acende o pavio: 4 s para correr. 4) BUM: a parede cai para
/// quem fez a missão e estava por perto; quem ficou colado no barril leva dano (nunca morre disso).
/// </summary>
public sealed partial class GameServer
{
    private const long FuseMs = 4000, BlastShowMs = 700;
    private const double BlastRadius = 3.5, CarrySpeedMult = 0.65;

    private sealed partial class Session
    {
        /// <summary>O que está carregando nas mãos ("barril" ou null).</summary>
        public string? Carry;
    }

    private sealed class Keg
    {
        public string Id = "";
        public string GateId = "";
        public double X, Y;
        public long LitAtMs, BlowAtMs, ShowUntilMs;   // BlowAtMs 0 = no chão, apagado
        public bool Blown;
        public Session Owner = null!;
    }

    private sealed partial class Zone
    {
        public readonly List<Keg> Kegs = new();
    }

    private int _kegSeq;

    /// <summary>Pilha de barris da Nara: pega um barril (só com a missão feita e a parede ainda de pé).</summary>
    private bool TryPickKeg(Session s, MapObject obj)
    {
        if (obj.Kind != "barris_polvora") return false;
        if (Distance(s.X, s.Y, obj.X, obj.Y) > InteractRange) { Reply(s, "interact", false, "out_of_range"); return true; }
        if (s.Flags.Contains("parede:explodida")) { SendTo(s, new NoticeMessage("A parede já caiu. Os barris ficam para a próxima.", "info")); Reply(s, "interact", false, "already_opened"); return true; }
        if (!s.Flags.Contains("explosivo:parede"))
        {
            SendTo(s, new NoticeMessage("Barris de pólvora da Nara. Sem pavio não adianta: faça a missão dela primeiro.", "warn"));
            Reply(s, "interact", false, "need_quest");
            return true;
        }
        if (s.Carry is not null) { Reply(s, "interact", false, "hands_full"); return true; }
        s.Carry = "barril";
        Dismount(s, null);
        SendTo(s, new NoticeMessage("Você pegou um barril de pólvora. É pesado: anda devagar e não dá para atacar. Leve até a rachadura do muro e aperte F.", "info"));
        Reply(s, "interact", true, "keg_picked");
        return true;
    }

    /// <summary>Rachadura do muro: põe o barril no chão; depois acende o pavio. Falso = não é a rachadura.</summary>
    private bool TryKegAtCrack(Session s, MapGate gate)
    {
        if (gate.Kind != "rachadura") return false;
        var z = s.Zone!;
        if (!s.Flags.Contains("explosivo:parede"))
        {
            SendTo(s, new NoticeMessage("Uma parede rachada. Com pólvora ela cai: fale com a Nara, a mineira, aqui perto.", "warn"));
            Reply(s, "interact", false, "need_explosive");
            return true;
        }
        var mine = z.Kegs.FirstOrDefault(k => ReferenceEquals(k.Owner, s) && k.GateId == gate.Id && !k.Blown);
        if (s.Carry == "barril")
        {
            if (mine is not null) { SendTo(s, new NoticeMessage("Já tem um barril seu aí. Acenda o pavio (F).", "info")); Reply(s, "interact", false, "already_placed"); return true; }
            s.Carry = null;
            z.Kegs.Add(new Keg { Id = "keg" + (++_kegSeq), GateId = gate.Id, X = gate.LeverX, Y = gate.LeverY - 1.2, Owner = s });
            SendTo(s, new NoticeMessage("Barril no pé da parede. Aperte F para acender o pavio — e corra!", "info"));
            Reply(s, "interact", true, "keg_placed");
            return true;
        }
        if (mine is { BlowAtMs: 0 })
        {
            var now = NowMs;
            mine.LitAtMs = now;
            mine.BlowAtMs = now + FuseMs;
            SendTo(s, new NoticeMessage("Pavio aceso! Corra para longe!", "warn"));
            Reply(s, "interact", true, "keg_lit");
            return true;
        }
        if (mine is not null) { Reply(s, "interact", false, "keg_burning"); return true; }
        SendTo(s, new NoticeMessage("Precisa de um barril de pólvora: pegue um na pilha de barris, ao lado da Nara.", "warn"));
        Reply(s, "interact", false, "need_keg");
        return true;
    }

    /// <summary>Por tique: pavio queimando → explosão; barril de quem saiu some.</summary>
    private void UpdateKegs(Zone z, long now)
    {
        if (z.Kegs.Count == 0) return;
        foreach (var k in z.Kegs.ToList())
        {
            if (k.Blown) { if (now >= k.ShowUntilMs) z.Kegs.Remove(k); continue; }
            if (!IsActive(k.Owner) || !ReferenceEquals(k.Owner.Zone, z)) { z.Kegs.Remove(k); continue; }
            if (k.BlowAtMs == 0 || now < k.BlowAtMs) continue;
            k.Blown = true;
            k.ShowUntilMs = now + BlastShowMs;
            var gate = z.Layout?.Gates.FirstOrDefault(g => g.Id == k.GateId);
            foreach (var p in z.Players.ToList())
            {
                if (!p.Combat!.Alive) continue;
                var d = Distance(p.X, p.Y, k.X, k.Y);
                if (d <= BlastRadius)
                {
                    // perto demais: dano forte, mas nunca mata (é uma missão, não uma armadilha)
                    var dmg = Math.Min(p.Combat.Health - 1, p.Combat.MaxHealth * 0.3);
                    if (dmg > 0) p.Combat.TakeDamage(dmg);
                    SendTo(p, new NoticeMessage("Você ficou perto demais da explosão!", "warn"));
                }
                // a parede cai para quem tem a missão e viu de perto (o grupo junto)
                if (gate is not null && d <= 14 && p.Flags.Contains("explosivo:parede") && p.Flags.Add(gate.Flag))
                {
                    _db.TryAddFlag(p.CharacterId!, gate.Flag);
                    SendTo(p, new FlagsMessage(p.Flags.OrderBy(f => f).ToList()));
                    SendTo(p, new NoticeMessage("BUM! A parede rachada veio abaixo: o caminho para dentro do muro está aberto.", "reward"));
                    GameLog.Info("servidor.mapa", $"{Who(p)} viu a parede {gate.Id} cair (barril de {Who(k.Owner)})");
                }
            }
        }
    }

    /// <summary>Barris no chão, para desenhar: "barril" (apagado), "barril_aceso" (T = pavio), "explosao".</summary>
    private IEnumerable<GroundEffectView> KegViews(Zone z, long now)
        => z.Kegs.Select(k => k.Blown
            ? new GroundEffectView(k.Id + "x", "explosao", k.X, k.Y, BlastRadius, Math.Clamp(1 - (k.ShowUntilMs - now) / (double)BlastShowMs, 0, 1))
            : k.BlowAtMs == 0
                ? new GroundEffectView(k.Id, "barril", k.X, k.Y, 0.5, 0)
                : new GroundEffectView(k.Id, "barril_aceso", k.X, k.Y, BlastRadius, Math.Clamp((now - k.LitAtMs) / (double)FuseMs, 0, 1)));
}

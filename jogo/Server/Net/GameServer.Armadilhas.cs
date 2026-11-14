using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 11/11 (dungeon estilo Skyrim, Galeria Funda): placa de pressão que dispara dardos da parede e runas que acendem.
/// - Placa ("placa_pressao"): pisou, faz "clique" e 0,55 s depois os dardos varrem 1,8 m em volta (12% da vida, nunca
///   mata; quem rolou na hora escapa). Recarrega em 3 s. Dá para ver a placa no chão e desviar.
/// - Runa ("runa"): cada personagem acende as runas do mapa (marca "runa:id"); com todas acesas ganha a marca do grupo
///   de runas (RequiresFlag do objeto, ex.: "galeria:runas"), que abre o portão da sala do tesouro.
/// </summary>
public sealed partial class GameServer
{
    internal const double TrapTriggerRadius = 0.7, TrapHitRadius = 1.8, TrapDamagePct = 0.12;
    internal const long TrapDelayMs = 550, TrapCooldownMs = 3000, TrapShowMs = 600;

    private sealed class TrapState
    {
        public long FireAtMs, CooldownUntilMs, ShowUntilMs;
    }

    private sealed partial class Zone
    {
        public readonly Dictionary<string, TrapState> Traps = new();
        public long CreatedMs;
    }

    /// <summary>
    /// 11/11 (dungeon contra o tempo, estilo Castelo de Sangue do MU): o chefe final da dungeon caiu — o tempo desde que a
    /// cópia abriu vira recorde pessoal de quem participou ("recorde:mapa:ms"), e sai o ranking dos 3 mais rápidos.
    /// </summary>
    private void DungeonCleared(MonsterInstance m, IReadOnlyList<Session> participants)
    {
        var z = m.Zone;
        if (!z.Instanced || !m.Def.Boss || m.Master is not null || m.GodSpawned || z.CreatedMs <= 0 || participants.Count == 0) return;
        if (z.Monsters.Any(o => o.Def.Boss && o.Master is null && !ReferenceEquals(o, m) && o.State.Alive)) return;   // ainda há chefe
        var ms = NowMs - z.CreatedMs;
        var mapId = z.Layout!.Id;
        static string Fmt(long t) => $"{t / 60000}:{t / 1000 % 60:00}";
        foreach (var p in participants) Achieve(p, "dungeon");
        foreach (var p in z.Players) ObjetivoSaida(p);   // 13/11: agora o objetivo é a saída
        foreach (var p in participants)
        {
            var old = p.Flags.Where(f => f.StartsWith("recorde:" + mapId + ":")).Select(f => long.TryParse(f.Split(':').Last(), out var v) ? v : long.MaxValue).DefaultIfEmpty(long.MaxValue).Min();
            if (ms < old)
            {
                _db.RemoveFlags(p.CharacterId!, "recorde:" + mapId + ":");
                p.Flags.RemoveWhere(f => f.StartsWith("recorde:" + mapId + ":"));
                _db.TryAddFlag(p.CharacterId!, $"recorde:{mapId}:{ms}");
                p.Flags.Add($"recorde:{mapId}:{ms}");
                SendTo(p, new NoticeMessage($"⏱ {z.Layout.DisplayName} em {Fmt(ms)} — recorde pessoal!" + (old < long.MaxValue ? $" (antes {Fmt(old)})" : ""), "legend"));
            }
            else SendTo(p, new NoticeMessage($"⏱ {z.Layout.DisplayName} em {Fmt(ms)} (seu recorde: {Fmt(old)})", "info"));
        }
        var top = _db.DungeonRecords(mapId, 3);
        if (top.Count == 0) return;
        var line = "🏆 " + z.Layout.DisplayName + ": " + string.Join("  ", top.Select((t, i) => $"{i + 1}º {t.Name} {Fmt(t.Ms)}"));
        foreach (var p in participants) SendTo(p, new NoticeMessage(line, "reward"));
        foreach (var p in participants) if (top.Any(t => t.Name == p.Name)) Achieve(p, "ranking");
        if (top.Any(t => participants.Any(p => p.Name == t.Name) && t.Ms == ms))
            BroadcastWorld(new NoticeMessage($"🏆 Novo tempo no ranking de {z.Layout.DisplayName}: {string.Join(", ", participants.Select(p => p.Name))} em {Fmt(ms)}!", "legend"));
    }

    private void UpdateTraps(Zone z, long now)
    {
        var layout = z.Layout;
        if (layout is null || z.Players.Count == 0) return;
        foreach (var obj in layout.Objects)
        {
            if (obj.Kind != "placa_pressao") continue;
            if (!z.Traps.TryGetValue(obj.Id, out var t)) z.Traps[obj.Id] = t = new TrapState();
            if (t.FireAtMs > 0)
            {
                if (now < t.FireAtMs) continue;
                t.FireAtMs = 0;
                t.ShowUntilMs = now + TrapShowMs;
                t.CooldownUntilMs = now + TrapCooldownMs;
                foreach (var p in z.Players.ToList())
                {
                    if (!p.Combat!.Alive || Distance(p.X, p.Y, obj.X, obj.Y) > TrapHitRadius) continue;
                    if (now < p.InvulnerableUntilMs) { SendTo(p, new NoticeMessage("Desviou dos dardos!", "reward")); continue; }
                    var dmg = Math.Min(p.Combat.Health - 1, p.Combat.MaxHealth * TrapDamagePct);
                    if (dmg > 0) p.Combat.TakeDamage(dmg);
                    SendTo(p, new NoticeMessage("Dardos da parede! Cuidado com as placas no chão.", "warn"));
                }
                continue;
            }
            if (now < t.CooldownUntilMs) continue;
            if (z.Players.Any(p => p.Combat!.Alive && Distance(p.X, p.Y, obj.X, obj.Y) <= TrapTriggerRadius))
            {
                t.FireAtMs = now + TrapDelayMs;
                GameLog.Debug("servidor.mapa", $"armadilha {obj.Id} disparou em {z.Id}");
            }
        }
    }

    private IEnumerable<GroundEffectView> TrapViews(Zone z, long now)
    {
        if (z.Traps.Count == 0 || z.Layout is null) yield break;
        foreach (var obj in z.Layout.Objects)
        {
            if (!z.Traps.TryGetValue(obj.Id, out var t)) continue;
            if (t.FireAtMs > 0) yield return new GroundEffectView("trap:" + obj.Id, "dardos_aviso", obj.X, obj.Y, TrapHitRadius,
                Math.Clamp(1 - (t.FireAtMs - now) / (double)TrapDelayMs, 0, 1));
            else if (now < t.ShowUntilMs) yield return new GroundEffectView("trapx:" + obj.Id + ":" + t.CooldownUntilMs, "dardos", obj.X, obj.Y, TrapHitRadius, 1);
        }
    }

    /// <summary>Runa do quebra-cabeça: acende para este personagem; todas acesas = marca do conjunto (abre o tesouro).</summary>
    private bool TryRune(Session s, MapObject obj)
    {
        if (obj.Kind != "runa") return false;
        if (Distance(s.X, s.Y, obj.X, obj.Y) > InteractRange) { Reply(s, "interact", false, "out_of_range"); return true; }
        var flag = "runa:" + obj.Id;
        if (s.Flags.Contains(flag)) { SendTo(s, new NoticeMessage("Esta runa já está acesa.", "info")); Reply(s, "interact", false, "already_opened"); return true; }
        _db.TryAddFlag(s.CharacterId!, flag);
        s.Flags.Add(flag);
        var runes = s.Zone!.Layout!.Objects.Where(o => o.Kind == "runa" && o.RequiresFlag == obj.RequiresFlag).ToList();
        var lit = runes.Count(o => s.Flags.Contains("runa:" + o.Id));
        if (lit >= runes.Count && obj.RequiresFlag is { } done && s.Flags.Add(done))
        {
            _db.TryAddFlag(s.CharacterId!, done);
            SendTo(s, new NoticeMessage("As três runas brilham juntas... pedra rolando ao longe: a sala do tesouro abriu!", "legend"));
            GameLog.Info("servidor.mapa", $"{Who(s)} acendeu todas as runas ({done})");
            Achieve(s, "runas");
        }
        else SendTo(s, new NoticeMessage($"A runa acende com um zumbido. ({lit}/{runes.Count})", "reward"));
        SendTo(s, new FlagsMessage(s.Flags.OrderBy(f => f).ToList()));
        Reply(s, "interact", true, "rune_lit");
        return true;
    }
}

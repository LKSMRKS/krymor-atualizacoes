using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 12/11 (pedido do dono: "quest igual Skyrim que te transforma em lobisomem, vampiro, ceifador, anjo" + "máscara verde"):
/// formas do pacote do ajudante (Docs/FICHA_FORMAS_MASCARA_VINTEUM.md). A relíquia (consumível reutilizável) transforma
/// por um tempo, com recarga do grupo "transformacao". Nesta primeira versão a forma muda o VISUAL e os ATRIBUTOS
/// (dano, armadura, vida, velocidade, ataque, resistência), a passiva (cura por abate, roubo de vida, bônus à noite,
/// dreno de dia) e a restrição de poções. Cada forma é de uma classe (12/11). As habilidades próprias de cada forma ficam para a próxima.
/// Acaba no tempo, ao morrer ou ao entrar na cidade.
/// </summary>
public sealed partial class GameServer
{
    private Dictionary<string, FormaDefinition>? _formas;

    internal IReadOnlyDictionary<string, FormaDefinition> Formas => _formas ??= LoadFormas();

    private Dictionary<string, FormaDefinition> LoadFormas()
    {
        foreach (var dir in new[] { ContentDir, Path.Combine(AppContext.BaseDirectory, "Content") })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var path = Path.Combine(dir, "transformacoes.json");
            if (!File.Exists(path)) continue;
            try
            {
                var list = JsonSerializer.Deserialize<List<FormaDefinition>>(File.ReadAllText(path)) ?? new();
                return list.ToDictionary(f => f.Id);
            }
            catch (Exception e) { GameLog.Warn("servidor.formas", "transformacoes.json inválido: " + e.Message); }
        }
        return new Dictionary<string, FormaDefinition>();
    }

    internal const string MaskItem = "mascara_pandemonio", MaskSkill = "skill_mascara_furacao", MaskForm = "forma_pandemonio";

    private bool WearsMask(Session s) => _db.GetEquipment(s.CharacterId!).Any(e => e.DefinitionId == MaskItem);

    /// <summary>
    /// 12/11 ("a máscara é um item que gera transformação"): tecla X com a Máscara do Pandemônio vestida. Fora da forma,
    /// transforma (45 s, recarga 4 min); já transformado, o X solta o Furacão (o lançamento normal segue).
    /// Devolve true quando tratou o pedido aqui (transformou ou recusou).
    /// </summary>
    private bool TryMaskTransform(Session s)
    {
        var now = NowMs;
        if (s.Forma.Transformado(now)) return false;   // na forma: X = Furacão
        if (!WearsMask(s)) { SendTo(s, new CastResultMessage(MaskSkill, false, "NotLearned", 0, false, null, false)); return true; }
        if (!Formas.TryGetValue(MaskForm, out var forma)) return true;
        if (s.Zone?.Layout?.Kind == "cidade") { SendTo(s, new NoticeMessage("Aqui não: dentro da cidade a máscara não pega.", "warn")); return true; }
        if (now < s.MaskReadyMs) { SendTo(s, new NoticeMessage($"A máscara ainda está quente: mais {Math.Max(1, (s.MaskReadyMs - now) / 1000)} s.", "warn")); return true; }
        var falha = s.Forma.PodeTransformar(forma, s.Level, s.Combat!.Alive, now);
        if (falha != FalhaForma.Nenhuma)
        {
            SendTo(s, new NoticeMessage(falha == FalhaForma.NivelBaixo ? $"A máscara pesa demais: só a partir do nível {forma.MinLevel}." : "Agora não.", "warn"));
            return true;
        }
        s.MaskReadyMs = now + forma.CooldownMs;
        s.FormBaseHealth = s.Combat.MaxHealth;
        s.Forma.Iniciar(forma, now);
        ApplyEquipment(s);
        s.Combat.Heal(s.Combat.MaxHealth - s.FormBaseHealth);
        SendTo(s, new NoticeMessage($"PANDEMÔNIO! A máscara gruda no rosto ({forma.DurationMs / 1000} s). X = Furacão.", "legend"));
        Broadcast(s.Zone!, new CombatEvent(s.CharacterId!, s.CharacterId!, "forma:" + forma.Id, 0, false, false));
        GameLog.Info("servidor.formas", $"{Who(s)} virou Pandemônio");
        return true;
    }

    /// <summary>Usou a relíquia (consumível com "transform").</summary>
    private void UseRelic(Session s, string itemId, Shared.ConsumableDefinition relic)
    {
        var now = NowMs;
        if (!Formas.TryGetValue(relic.Transform!, out var forma)) { Reply(s, "use_item", false, "not_consumable"); return; }
        if (forma.Class is { } cls && cls != s.ClassId)
        {
            SendTo(s, new NoticeMessage($"{forma.DisplayName} não responde a você: essa forma é só de {cls switch { "guerreiro" => "Guerreiro", "mago" => "Mago", "bruxa" => "Bruxa", "arqueiro" => "Arqueiro", _ => cls }}.", "warn"));
            Reply(s, "use_item", false, "wrong_class");
            return;
        }
        if (s.Zone?.Layout?.Kind == "cidade") { SendTo(s, new NoticeMessage("Aqui não: dentro da cidade a forma não pega.", "warn")); Reply(s, "use_item", false, "not_here"); return; }
        var falha = s.Forma.PodeTransformar(forma, s.Level, s.Combat!.Alive, now);
        if (falha != FalhaForma.Nenhuma)
        {
            SendTo(s, new NoticeMessage(falha switch
            {
                FalhaForma.NivelBaixo => $"{forma.DisplayName}: só a partir do nível {forma.MinLevel}.",
                FalhaForma.JaTransformado => "Você já está transformado.",
                _ => "Agora não.",
            }, "warn"));
            Reply(s, "use_item", false, "cannot_transform");
            return;
        }
        var check = ConsumableSystem.CanUse(s.Combat!, relic, now);
        if (check != UseFailure.None) { Reply(s, "use_item", false, check == UseFailure.Dead ? "dead" : "on_cooldown"); return; }
        if (!relic.Reusable)
        {
            var consumed = _db.TryConsumeOne(s.CharacterId!, itemId);
            if (!consumed.Success) { Reply(s, "use_item", false, consumed.Reason); return; }
        }
        ConsumableSystem.TryUse(s.Combat!, relic, now);
        s.FormBaseHealth = s.Combat!.MaxHealth;
        s.Forma.Iniciar(forma, now);
        ApplyEquipment(s);
        s.Combat!.Heal(s.Combat.MaxHealth - s.FormBaseHealth);   // a vida a mais já vem cheia
        SendTo(s, new NoticeMessage($"Você virou {forma.DisplayName}! ({forma.DurationMs / 1000} s)", "legend"));
        Broadcast(s.Zone!, new CombatEvent(s.CharacterId!, s.CharacterId!, "forma:" + forma.Id, 0, false, false));
        GameLog.Info("servidor.formas", $"{Who(s)} virou {forma.DisplayName}");
        Reply(s, "use_item", true, "ok");
        SendInventory(s);
    }

    /// <summary>Por tique (vivo, modo mapa): fim do tempo, cidade e dreno do vampiro de dia.</summary>
    private void UpdateForma(Session s, long now, double dt)
    {
        if (s.Forma.Ativa is null) return;
        if (s.Forma.Tick(now)) { EndForma(s, "A forma passou. Você voltou ao normal."); return; }
        if (s.Zone?.Layout?.Kind == "cidade") { EndForma(s, "Na cidade a forma se desfaz."); return; }
        var arLivre = s.Zone?.Layout is { } l && !l.Instanced && l.Theme is not ("casarao" or "biblioteca");
        var dreno = s.Forma.DrenoPorSegundo(now, IsNight, arLivre);
        if (dreno > 0 && s.Combat!.Alive)
        {
            s.FormDrain += s.Combat.MaxHealth * dreno * dt;
            if (s.FormDrain >= 1) { var d = Math.Floor(s.FormDrain); s.FormDrain -= d; s.Combat.TakeDamage(Math.Min(d, s.Combat.Health - 1)); }
        }
    }

    private void EndForma(Session s, string? why)
    {
        if (s.Forma.Ativa is null) return;
        s.Forma.Encerrar();
        if (s.Combat is null) return;
        ApplyEquipment(s);
        if (why is not null) SendTo(s, new NoticeMessage(why, "info"));
    }

    /// <summary>Forma ativa (para o desenho): o visual do bestiário da forma, ou null.</summary>
    private string? FormVisual(Session s, long now) => s.Forma.Transformado(now) ? s.Forma.Ativa!.Id : null;

    /// <summary>Ajustes da forma no recálculo do equipamento (chamado do ApplyEquipment).</summary>
    private (double Power, double ArmorMult, double ExtraHealth, double Move, double IntervalMult, double MagicRes, double HealMult) FormaStats(Session s)
    {
        var now = NowMs;
        if (!s.Forma.Transformado(now)) return (0, 1, 0, 0, 1, 0, 1);
        var f = s.Forma.Ativa!;
        var power = f.Modifiers.DamagePct + (IsNight ? f.P("night_damage_pct") : 0);
        var armor = s.Forma.MultArmadura(now);
        if (s.ClassId == "guerreiro") { power += f.P("warrior_damage_pct"); armor *= 1 + f.P("warrior_armor_pct"); }   // 12/11: Guerreiro um pouco mais forte na forma
        return (power, armor, s.FormBaseHealth * f.Modifiers.MaxHealthPct, f.Modifiers.MoveSpeedPct,
            s.Forma.MultIntervaloAtaque(now), f.Modifiers.MagicResistPct, s.Forma.MultCuraRecebida(now));
    }

    /// <summary>Cura por abate (lobisomem, ceifador) para quem participou.</summary>
    private void FormaOnKill(IEnumerable<Session> participants, long now)
    {
        foreach (var p in participants)
        {
            var pct = p.Forma.CuraPorAbate(now);
            if (pct > 0 && p.Combat!.Alive) p.Combat.Heal(p.Combat.MaxHealth * pct);
        }
    }

    /// <summary>Roubo de vida (vampiro) a cada golpe acertado.</summary>
    private void FormaOnHit(Session s, double damage, long now)
    {
        var pct = s.Forma.RouboDeVida(now);
        if (pct > 0 && damage > 0 && s.Combat!.Alive) s.Combat.Heal(damage * pct);
    }

    private bool FormaRestrito(Session s, string restricao) => s.Forma.Restrito(restricao, NowMs);
}

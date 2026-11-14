using CinzasDaMare.Shared;

namespace CinzasDaMare.Server.Combat;

public enum UseFailure { None, Dead, OnCooldown, WrongClass }

public static class ConsumableSystem
{
    /// <summary>Verifica sem alterar estado se o consumível pode ser usado agora.</summary>
    public static UseFailure CanUse(PlayerCombatState player, ConsumableDefinition item, long nowMs)
    {
        if (!player.Alive) return UseFailure.Dead;
        if (item.Classes is { Count: > 0 } && !item.Classes.Contains(player.Class.Id)) return UseFailure.WrongClass;
        if (nowMs < player.GroupReadyAt(item.CooldownGroup)) return UseFailure.OnCooldown;
        return UseFailure.None;
    }

    /// <summary>Poções compartilham recarga por grupo (GDD 12.2). A cura é gradual; use AdvanceHeal a cada tick.</summary>
    public static UseFailure TryUse(PlayerCombatState player, ConsumableDefinition item, long nowMs)
    {
        var check = CanUse(player, item, nowMs);
        if (check != UseFailure.None) return check;
        player.SetGroupCooldown(item.CooldownGroup, nowMs + item.CooldownMs);
        if (item.HealPercentMaxHealth > 0) player.StartHeal(player.MaxHealth * item.HealPercentMaxHealth, item.HealDurationMs, nowMs);
        if (item.ResourcePercent > 0) player.AddResource(player.Class.ResourceMax * item.ResourcePercent);
        return UseFailure.None;
    }
}

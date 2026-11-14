using CinzasDaMare.Shared;

namespace CinzasDaMare.Server.Combat;

public enum CastFailure
{
    None,
    UnknownSkill,
    WrongClass,
    NotUnlocked,
    CasterDead,
    OnCooldown,
    NotEnoughResource,
    NoTarget,
    TargetDead,
    OutOfRange,
    UnsupportedTargetMode,
    NeedsShield,   // 12/11: Guarda de Escudo só com escudo na mão esquerda
}

public readonly record struct CastResult(bool Success, CastFailure Failure, int Damage, bool Crit, bool TargetDied)
{
    public static CastResult Fail(CastFailure failure) => new(false, failure, 0, false, false);
}

/// <summary>
/// Valida e executa a intenção de usar uma habilidade. O cliente nunca envia dano:
/// só a habilidade, o alvo e (no servidor) a distância medida. Falhas não alteram estado.
/// </summary>
public sealed class SkillExecutor
{
    private readonly ContentBundle _content;
    private readonly Func<double> _rng;

    public SkillExecutor(ContentBundle content, Func<double>? rng = null)
    {
        _content = content;
        _rng = rng ?? Random.Shared.NextDouble;
    }

    /// <summary>Valida a intenção sem alterar nenhum estado. Mesma ordem de verificações de TryCast.</summary>
    public CastFailure Validate(PlayerCombatState caster, string skillId, MonsterCombatState? target,
        double distanceMeters, long nowMs)
    {
        if (!_content.Skills.TryGetValue(skillId, out var skill)) return CastFailure.UnknownSkill;
        if (skill.ClassId != caster.Class.Id && skill.ClassId != "todos") return CastFailure.WrongClass;   // 12/11: "todos" = habilidade de item
        if (caster.Level < skill.UnlockLevel) return CastFailure.NotUnlocked;
        if (!caster.Alive) return CastFailure.CasterDead;
        if (nowMs < caster.SkillReadyAt(skill.Id)) return CastFailure.OnCooldown;
        if (caster.Resource < skill.ResourceCost * caster.CostMult) return CastFailure.NotEnoughResource;
        if (Effect(skill, "requires_shield", 0) > 0 && !caster.HasShield) return CastFailure.NeedsShield;

        switch (skill.TargetMode)
        {
            case "single":
                if (target is null) return CastFailure.NoTarget;
                if (!target.Alive) return CastFailure.TargetDead;
                if (double.IsNaN(distanceMeters) || distanceMeters < 0 || distanceMeters > skill.Range)
                    return CastFailure.OutOfRange;
                return CastFailure.None;
            case "self":
            case "cone":        // E8: alvo(s) decididos pelo servidor na hora do golpe
            case "projectile":
            case "ground":
            case "blink":
            case "dash":        // E9: avança e acerta quem estiver no caminho
            case "nova":        // E9: área em volta de si
            case "heal": case "heal_area": case "barrier": case "aura": case "stealth": case "sight": // Bruxa/Arqueiro
                return CastFailure.None;
            default:
                return CastFailure.UnsupportedTargetMode;
        }
    }

    public CastResult TryCast(PlayerCombatState caster, string skillId, MonsterCombatState? target,
        double distanceMeters, long nowMs)
    {
        var failure = Validate(caster, skillId, target, distanceMeters, nowMs);
        if (failure != CastFailure.None) return CastResult.Fail(failure);
        var skill = _content.Skills[skillId];

        // Tudo validado: a partir daqui o estado muda.
        caster.SpendResource(skill.ResourceCost * caster.CostMult);
        caster.AddResource(skill.ResourceGenerated);
        caster.SetSkillCooldown(skill.Id, nowMs + (long)(skill.CooldownMs * caster.CooldownMultFor(skill.Id)));

        if (skill.TargetMode == "self")
        {
            caster.GuardStartedMs = nowMs;
            caster.ActivateGuard(
                nowMs + (long)Effect(skill, "duration_ms", 0),
                Math.Min(0.9, Effect(skill, "frontal_reduction", 0) + caster.GuardBonus),
                Effect(skill, "move_speed_mult", 1.0));
            return new CastResult(true, CastFailure.None, 0, false, false);
        }
        if (skill.TargetMode != "single")
            return new CastResult(true, CastFailure.None, 0, false, false); // efeito resolvido pelo GameServer

        var victim = target!;
        var crit = _rng() < CombatFormulas.BaseCritChance;
        var reduction = CombatFormulas.ArmorReduction(victim.EffectiveArmor(nowMs), caster.Level);
        var damage = CombatFormulas.FinalDamage(
            caster.PhysicalPower, skill.Coefficient, reduction, crit ? CombatFormulas.CritMultiplier : 1.0);
        victim.TakeDamage(damage);

        // A redução de armadura vale depois do golpe que a aplica.
        var shredPct = Effect(skill, "armor_shred_pct", 0);
        var shredMs = (long)Effect(skill, "armor_shred_ms", 0);
        if (shredPct > 0 && shredMs > 0 && victim.Alive)
            victim.ApplyArmorShred(shredPct, nowMs + shredMs);

        return new CastResult(true, CastFailure.None, damage, crit, !victim.Alive);
    }

    /// <summary>Paga custo, gera recurso e inicia a recarga (modos resolvidos pelo GameServer). Chamar após Validate.</summary>
    public void Commit(PlayerCombatState caster, string skillId, long nowMs)
    {
        var skill = _content.Skills[skillId];
        caster.SpendResource(skill.ResourceCost * caster.CostMult);
        caster.AddResource(skill.ResourceGenerated);
        caster.SetSkillCooldown(skill.Id, nowMs + (long)(skill.CooldownMs * caster.CooldownMultFor(skill.Id)));
    }

    /// <summary>
    /// Golpe de habilidade num monstro (E8, modos cone/projétil/área/alvo único no mapa). extraMult: bônus de
    /// "exposto" (monstro em recuperação). Aplica a redução de armadura do efeito, como TryCast.
    /// </summary>
    public CastResult HitMonster(PlayerCombatState caster, SkillDefinition skill, MonsterCombatState victim,
        long nowMs, double extraMult = 1.0)
    {
        if (!victim.Alive) return CastResult.Fail(CastFailure.TargetDead);
        var crit = _rng() < CombatFormulas.BaseCritChance;
        var reduction = CombatFormulas.ArmorReduction(victim.EffectiveArmor(nowMs), caster.Level);
        var damage = CombatFormulas.FinalDamage(caster.PhysicalPower, skill.Coefficient, reduction,
            (crit ? CombatFormulas.CritMultiplier : 1.0) * extraMult);
        victim.TakeDamage(damage);
        var shredPct = Effect(skill, "armor_shred_pct", 0);
        var shredMs = (long)Effect(skill, "armor_shred_ms", 0);
        if (shredPct > 0 && shredMs > 0 && victim.Alive)
            victim.ApplyArmorShred(shredPct, nowMs + shredMs);
        return new CastResult(true, CastFailure.None, damage, crit, !victim.Alive);
    }

    public static double Effect(SkillDefinition skill, string key, double fallback)
        => skill.Effects is not null && skill.Effects.TryGetValue(key, out var v) ? v : fallback;
}

public readonly record struct MonsterHitResult(bool Hit, int Damage, bool PlayerDied);

public static class MonsterCombat
{
    /// <summary>Ataque básico do monstro. Respeita o intervalo de ataque e a Guarda de Escudo (só golpes frontais).</summary>
    public static MonsterHitResult TryAttack(MonsterCombatState monster, PlayerCombatState player,
        bool isFrontal, long nowMs)
    {
        if (!monster.Alive || !player.Alive || nowMs < monster.NextAttackAtMs)
            return new MonsterHitResult(false, 0, false);

        var reduction = CombatFormulas.ArmorReduction(player.Armor, monster.Level);
        var multiplier = player.IsGuarding(nowMs) && isFrontal ? 1.0 - player.GuardReduction : 1.0;
        var damage = CombatFormulas.FinalDamage(monster.Profile.Damage, 1.0, reduction, multiplier);
        player.TakeDamage(damage);
        monster.NextAttackAtMs = nowMs + monster.Profile.AttackIntervalMs;
        return new MonsterHitResult(true, damage, !player.Alive);
    }
}

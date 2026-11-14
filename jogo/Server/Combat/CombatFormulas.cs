namespace CinzasDaMare.Server.Combat;

/// <summary>Fórmulas do GDD 6.1. Arredondamento só no resultado final; dano positivo mínimo 1.</summary>
public static class CombatFormulas
{
    public const double ArmorReductionCap = 0.65;
    public const double BaseCritChance = 0.05;
    public const double CritMultiplier = 1.5;
    public const double ResistanceMin = -0.25;
    public const double ResistanceMax = 0.60;
    /// <summary>GDD 6.1: fora de combate por 10 s, recupera 1% da vida máxima por segundo.</summary>
    public const double OutOfCombatRegenPctPerSecond = 0.01;

    public static double MaxHealth(int level, int vigor, double equipmentBonus = 0)
        => 180 + 14.0 * level + 9.0 * vigor + equipmentBonus;

    public static double PhysicalPower(double weaponDamage, double mainAttribute, int level)
        => weaponDamage + 2.2 * mainAttribute + 1.2 * level;

    public static double ArmorReduction(double armor, int attackerLevel)
    {
        if (armor <= 0) return 0;
        var reduction = armor / (armor + 180 + 18.0 * attackerLevel);
        return Math.Min(reduction, ArmorReductionCap);
    }

    public static double ClampResistance(double resistance)
        => Math.Clamp(resistance, ResistanceMin, ResistanceMax);

    /// <summary>
    /// E9 — faixa de poder por região. 14/11 (dono: "todos os jogos usam essa regra? pensa em jogo de nível baixo, até 60"):
    /// regra suavizada — até 2 níveis acima, dano normal; depois perde 5% por nível, no mínimo 50% (antes: −12% por nível
    /// desde o 1º, mínimo 30%, pesado demais para um jogo que vai só até o 60).
    /// </summary>
    public static double PlayerVsHigherLevel(int playerLevel, int monsterLevel)
    {
        var gap = monsterLevel - playerLevel;
        return gap <= 2 ? 1.0 : Math.Max(0.5, 1.0 - 0.05 * (gap - 2));
    }

    /// <summary>
    /// E9 — golpe de monstro de nível MAIOR que o jogador: +15% por nível (até 2,5x); de nível menor: −8% por nível
    /// (mínimo 50%). É o que faz um inimigo difícil ficar fácil depois de evoluir.
    /// </summary>
    public static double MonsterVsLevel(int monsterLevel, int playerLevel)
    {
        var gap = monsterLevel - playerLevel;
        return gap >= 0 ? Math.Min(2.5, 1.0 + 0.15 * gap) : Math.Max(0.5, 1.0 + 0.08 * gap);
    }

    /// <summary>Dano físico: usa apenas armadura (não resistência).</summary>
    public static int FinalDamage(double power, double coefficient, double armorReduction, double multiplier = 1.0)
    {
        var raw = power * coefficient * (1.0 - armorReduction) * multiplier;
        if (raw <= 0) return 0;
        return Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
    }
}

public readonly record struct MonsterProfile(double Health, double Damage, double Armor, int AttackIntervalMs);

/// <summary>Sementes de monstro do GDD 6.3 (comuns). Elites/chefes ficam para marcos futuros.</summary>
public static class MonsterScaling
{
    // (vida, dano, velocidade de ataque)
    private static readonly Dictionary<string, (double Health, double Damage, double Speed)> Archetypes = new()
    {
        ["combatente"] = (1.0, 1.0, 1.0),
        ["agil"] = (0.7, 0.8, 1.4),
        ["tanque"] = (1.7, 0.8, 0.7),
        ["atirador"] = (0.85, 1.0, 1.0),
        ["conjurador"] = (0.8, 1.2, 1.0),
        ["enxame"] = (0.35, 0.45, 1.0),
    };

    public static bool IsKnownArchetype(string archetype) => Archetypes.ContainsKey(archetype);

    public static MonsterProfile Create(int level, string archetype)
    {
        if (!Archetypes.TryGetValue(archetype, out var a))
            throw new ArgumentException($"Arquétipo desconhecido: {archetype}", nameof(archetype));
        double m = level;
        var health = (180 + 18 * m + 0.45 * m * m) * a.Health;
        var damage = (12 + 2 * m + 0.12 * m * m) * a.Damage;
        var armor = 20 + 5 * m;
        var interval = (int)Math.Round(2000.0 / a.Speed);
        return new MonsterProfile(health, damage, armor, interval);
    }
}

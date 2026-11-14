using CinzasDaMare.Shared;

namespace CinzasDaMare.Server.Combat;

/// <summary>Estado de combate de um jogador, mantido pelo servidor. Tempo sempre passado como nowMs.</summary>
public sealed class PlayerCombatState
{
    private readonly Dictionary<string, long> _skillReadyAt = new();
    private readonly Dictionary<string, long> _groupReadyAt = new();
    private long _guardUntilMs = long.MinValue;
    private double _guardMoveMult = 1.0;
    private double _healRemaining;
    private long _healLastMs;
    private long _healEndMs;

    public string Id { get; }
    public ClassDefinition Class { get; }
    public int Level { get; private set; }
    public int Strength { get; private set; }
    public int Vigor { get; private set; }
    public double WeaponDamage { get; private set; }
    public int AttackIntervalMs { get; private set; } = 1000;
    public double Armor { get; private set; }
    /// <summary>Vida máxima extra dos equipamentos (E8).</summary>
    public double BonusHealth { get; private set; }
    /// <summary>Soma à redução frontal da Guarda (escudo).</summary>
    public double GuardBonus { get; private set; }
    /// <summary>12/11: escudo na mão esquerda (habilidades com "requires_shield").</summary>
    public bool HasShield { get; set; }
    public double ResourceRegenMult { get; private set; } = 1.0;
    /// <summary>11/11 noite: cura recebida (peitoral) e custo de mana/fôlego das habilidades (elmo).</summary>
    public double HealReceivedMult { get; set; } = 1.0;
    public double CostMult { get; set; } = 1.0;
    public double MaxHealth { get; private set; }
    public double Health { get; private set; }
    public double Resource { get; private set; }
    /// <summary>13/11 ("energia não deveria aumentar o foco também?"): recurso máximo = o da classe + a Energia investida.</summary>
    public double ResourceMaxBonus { get; private set; }
    public double MaxResource => Class.ResourceMax + ResourceMaxBonus;
    public void SetResourceBonus(double bonus)
    {
        var cheio = Resource >= MaxResource - 1e-6;
        ResourceMaxBonus = Math.Max(0, bonus);
        Resource = cheio ? MaxResource : Math.Min(Resource, MaxResource);   // cheio continua cheio
    }
    public double GuardReduction { get; private set; }
    public bool Alive => Health > 0;

    public PlayerCombatState(string id, ClassDefinition cls, int level, int strength, int vigor,
        double weaponDamage, double armor = 0)
    {
        // E8: o 3º parâmetro é o atributo PRINCIPAL da classe (Força do Guerreiro, Sintonia do Mago).
        Id = id;
        Class = cls;
        Level = level;
        Strength = strength;
        Vigor = vigor;
        WeaponDamage = weaponDamage;
        Armor = armor;
        MaxHealth = CombatFormulas.MaxHealth(level, vigor);
        Health = MaxHealth;
        Resource = cls.ResourceMax;
    }

    public double PhysicalPower => CombatFormulas.PhysicalPower(WeaponDamage, Strength, Level);

    public long SkillReadyAt(string skillId) => _skillReadyAt.TryGetValue(skillId, out var t) ? t : 0;
    public long GroupReadyAt(string group) => _groupReadyAt.TryGetValue(group, out var t) ? t : 0;
    public bool IsGuarding(long nowMs) => nowMs < _guardUntilMs;
    public double MoveSpeedMultiplier(long nowMs) => IsGuarding(nowMs) ? _guardMoveMult : 1.0;

    /// <summary>Regeneração de Fôlego (GDD 5.1): 8/s em combate, 15/s fora; guarda ativa zera a regeneração.</summary>
    public void TickResource(double dtSeconds, bool inCombat, long nowMs)
    {
        if (!Alive || dtSeconds <= 0 || IsGuarding(nowMs)) return;
        var rate = (inCombat ? Class.ResourceRegenCombat : Class.ResourceRegenIdle) * ResourceRegenMult;
        Resource = Math.Min(MaxResource, Resource + rate * dtSeconds);
    }

    public void TakeDamage(double amount)
    {
        if (amount <= 0) return;
        Health = Math.Max(0, Health - amount);
    }

    /// <summary>14/11: recurso ao derrotar um inimigo (opção Excelente da arma).</summary>
    public void GainResource(double amount)
    {
        if (!Alive || amount <= 0) return;
        Resource = Math.Min(MaxResource, Resource + amount);
    }

    public void Heal(double amount)
    {
        if (!Alive || amount <= 0) return;
        Health = Math.Min(MaxHealth, Health + amount * HealReceivedMult);
    }

    /// <summary>Cura gradual (poção): termina apenas por morte ou ao completar a duração.</summary>
    public void AdvanceHeal(long nowMs)
    {
        if (!Alive) { _healRemaining = 0; return; }
        if (_healRemaining <= 0) return;
        var to = Math.Min(nowMs, _healEndMs);
        if (to <= _healLastMs) return;
        var span = (double)(_healEndMs - _healLastMs);
        var part = to >= _healEndMs ? _healRemaining : _healRemaining * (to - _healLastMs) / span;
        Heal(part);
        _healRemaining -= part;
        _healLastMs = to;
        if (to >= _healEndMs) _healRemaining = 0;
    }

    /// <summary>Define a arma efetiva (equipar/desequipar). Intervalo em ms.</summary>
    public void SetWeapon(double weaponDamage, int attackIntervalMs)
    {
        WeaponDamage = weaponDamage;
        AttackIntervalMs = attackIntervalMs;
    }

    /// <summary>
    /// Equipamentos além da arma (E8). A vida atual NÃO sobe ao equipar (só é limitada ao novo máximo),
    /// para trocar de peça não virar cura.
    /// </summary>
    public void SetGear(double armor, double bonusHealth, double guardBonus, double resourceRegenMult)
    {
        Armor = Math.Max(0, armor);
        BonusHealth = Math.Max(0, bonusHealth);
        GuardBonus = Math.Max(0, guardBonus);
        ResourceRegenMult = Math.Max(0, resourceRegenMult);
        MaxHealth = CombatFormulas.MaxHealth(Level, Vigor, BonusHealth);
        Health = Math.Min(Health, MaxHealth);
    }

    /// <summary>Pontos de atributo gastos (E9): recalcula poder e vida máxima; a vida faltante se mantém.</summary>
    public void SetAttributes(int primary, int vigor)
    {
        var oldMax = MaxHealth;
        Strength = primary;
        Vigor = vigor;
        MaxHealth = CombatFormulas.MaxHealth(Level, Vigor, BonusHealth);
        if (Alive) Health = Math.Clamp(Health + (MaxHealth - oldMax), 1, MaxHealth);
    }

    /// <summary>Encerra a Guarda (esquiva cancela a Guarda).</summary>
    public void CancelGuard() => _guardUntilMs = long.MinValue;

    /// <summary>Subida de nível: recalcula a vida máxima e soma a diferença à vida atual (vida faltante se mantém).</summary>
    public void SetLevel(int level)
    {
        var oldMax = MaxHealth;
        Level = level;
        MaxHealth = CombatFormulas.MaxHealth(level, Vigor, BonusHealth);
        if (Alive) Health = Math.Min(MaxHealth, Health + (MaxHealth - oldMax));
    }

    /// <summary>Regeneração fora de combate (a regra dos 10 s é decidida por quem chama).</summary>
    public void RegenOutOfCombat(double dtSeconds)
        => Heal(MaxHealth * CombatFormulas.OutOfCombatRegenPctPerSecond * dtSeconds);

    /// <summary>Restaura vida salva (ex.: ao entrar no jogo).</summary>
    public void SetHealth(double value) => Health = Math.Clamp(value, 0, MaxHealth);

    /// <summary>Renasce com vida e Fôlego cheios, sem efeitos ativos.</summary>
    public void Revive()
    {
        Health = MaxHealth;
        Resource = MaxResource;
        _healRemaining = 0;
        _guardUntilMs = long.MinValue;
    }

    /// <summary>Modo deus: vida e recurso cheios (só se estiver vivo).</summary>
    public void RestoreAll()
    {
        if (!Alive) return;
        Health = MaxHealth;
        Resource = MaxResource;
    }

    /// <summary>Modo deus: todas as recargas prontas.</summary>
    public void ResetCooldowns() { _skillReadyAt.Clear(); _groupReadyAt.Clear(); }

    internal void SpendResource(double amount) => Resource = Math.Max(0, Resource - amount);
    internal void AddResource(double amount) => Resource = Math.Min(MaxResource, Resource + amount);
    internal void SetSkillCooldown(string skillId, long readyAtMs) => _skillReadyAt[skillId] = readyAtMs;

    /// <summary>Poderes fortalecidos por esfera de nível 2: recarga multiplicada por este fator.</summary>
    public Dictionary<string, double> CooldownMults { get; } = new();
    public double CooldownMultFor(string skillId) => (CooldownMults.TryGetValue(skillId, out var m) ? m : 1.0) * GearCooldownMult;
    /// <summary>11/11: redução de recarga dos equipamentos (capacete): 0,95 = 5% mais rápido.</summary>
    public double GearCooldownMult { get; set; } = 1.0;
    internal void SetGroupCooldown(string group, long readyAtMs) => _groupReadyAt[group] = readyAtMs;

    /// <summary>Quando a Guarda atual começou (para o aparar no tempo certo). Quem ativa a Guarda marca o instante.</summary>
    public long GuardStartedMs { get; set; } = long.MinValue;

    internal void ActivateGuard(long untilMs, double frontalReduction, double moveSpeedMult)
    {
        _guardUntilMs = untilMs;
        GuardReduction = frontalReduction;
        _guardMoveMult = moveSpeedMult;
    }

    internal void StartHeal(double total, int durationMs, long nowMs)
    {
        if (durationMs <= 0) { Heal(total); return; }
        _healRemaining = total;
        _healLastMs = nowMs;
        _healEndMs = nowMs + durationMs;
    }
}

public sealed class MonsterCombatState
{
    public string Id { get; }
    public MonsterDefinition Definition { get; }
    public MonsterProfile Profile { get; }
    public double Health { get; private set; }
    public long NextAttackAtMs { get; internal set; }
    public long ArmorShredUntilMs { get; private set; }
    public double ArmorShredPct { get; private set; }
    public int Level { get; }
    public bool Alive => Health > 0;

    public MonsterCombatState(string id, MonsterDefinition definition)
    {
        Id = id;
        Definition = definition;
        Level = definition.Level;
        Profile = MonsterScaling.Create(definition.Level, definition.Archetype);
        Health = Profile.Health;
    }

    /// <summary>Modo mapa (E8): nível do ponto de spawn e ajustes de vida/dano da definição.</summary>
    public MonsterCombatState(string id, MonsterDefinition definition, int level, double healthMult, double damageMult)
    {
        Id = id;
        Definition = definition;
        Level = level;
        var p = MonsterScaling.Create(level, definition.Archetype);
        Profile = p with { Health = p.Health * healthMult, Damage = p.Damage * damageMult };
        Health = Profile.Health;
    }

    public void HealFull() => Health = Profile.Health;

    /// <summary>Afixo "blindado" de elite: multiplica o dano recebido (1 = normal).</summary>
    public double DamageTakenMult { get; set; } = 1.0;

    public void Heal(double amount)
    {
        if (amount <= 0 || !Alive) return;
        Health = Math.Min(Profile.Health, Health + amount);
    }

    public double EffectiveArmor(long nowMs)
        => nowMs < ArmorShredUntilMs ? Profile.Armor * (1.0 - ArmorShredPct) : Profile.Armor;

    public void TakeDamage(double amount)
    {
        if (amount <= 0) return;
        Health = Math.Max(0, Health - amount * DamageTakenMult);
    }

    internal void ApplyArmorShred(double pct, long untilMs)
    {
        ArmorShredPct = pct;
        ArmorShredUntilMs = untilMs;
    }
}

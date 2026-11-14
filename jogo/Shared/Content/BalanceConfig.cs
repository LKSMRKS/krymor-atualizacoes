namespace CinzasDaMare.Shared;

/// <summary>
/// Parâmetros de protótipo do Marco A (Content/balance.json). TODOS são provisórios e sujeitos a balanceamento;
/// o GDD não define atributos iniciais, alcance de coleta, capacidade de inventário etc.
/// </summary>
public sealed record BalanceConfig(
    int StartingLevel,
    int StartingStrength,
    int StartingVigor,
    int InventoryCapacity,
    double UnarmedWeaponDamage,
    double MoveSpeedMps,
    double ArenaHalfSize,
    double PickupRangeM,
    double MonsterAggroRadiusM,
    double MonsterAttackRangeM,
    double MonsterMoveSpeedMps,
    int MonsterRespawnSeconds,
    int PlayerRespawnSeconds,
    int CombatTimeoutSeconds,
    double SpawnX,
    double SpawnY,
    double MonsterSpawnX,
    double MonsterSpawnY,
    double SeedItemX,
    double SeedItemY,
    int UnarmedAttackIntervalMs,
    double BasicAttackCoefficient,
    double BasicAttackRangeM,
    int StartingPotions,
    double GuardArcDegrees);

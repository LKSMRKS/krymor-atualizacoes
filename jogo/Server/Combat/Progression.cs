namespace CinzasDaMare.Server.Combat;

/// <summary>XP e níveis (GDD 6.2). Valores de K, fator de nível e bônus de grupo vêm do GDD.</summary>
public static class Progression
{
    public const int MaxLevel = 60;   // 13/11 (dono: "depois do 60 o jogo deixa subir mais; isso ainda não é permitido")

    public static int K(int level) => level switch
    {
        <= 19 => 60,
        <= 39 => 150,
        <= 59 => 280,
        <= 79 => 450,
        <= 99 => 650,
        _ => 900,
    };

    /// <summary>XP para passar de L a L+1: 100 x L x K(L). No nível máximo não há nova barra (0).</summary>
    public static long XpToNext(int level) => level >= MaxLevel ? 0 : 100L * level * K(level);

    public static long MonsterXp(int monsterLevel) => 100L * monsterLevel;

    /// <summary>Até 5 níveis abaixo: integral; 6 a 10: 50%; mais de 10: 10%. Monstro acima não dá bônus.</summary>
    public static double LevelDifferenceFactor(int playerLevel, int monsterLevel)
    {
        var below = playerLevel - monsterLevel;
        if (below <= 5) return 1.0;
        if (below <= 10) return 0.5;
        return 0.1;
    }

    /// <summary>
    /// E9 (modo mapa): queda gradual em vez do "integral até 5 níveis abaixo" — empurra o jogador para a próxima área
    /// (degraus por região). Até 2 abaixo: integral; 3: 80%; 4: 60%; 5: 45%; 6: 30%; mais: 15%. Monstro acima: integral.
    /// </summary>
    public static double MapLevelDifferenceFactor(int playerLevel, int monsterLevel)
        => (playerLevel - monsterLevel) switch
        {
            <= 2 => 1.0,
            3 => 0.8,
            4 => 0.6,
            5 => 0.45,
            6 => 0.3,
            _ => 0.15,
        };

    /// <summary>XP do modo mapa (E9): mesma divisão de grupo do GDD, com a queda gradual por diferença de nível.</summary>
    public static long MapKillXp(int monsterLevel, int playerLevel, int participants)
    {
        if (participants < 1) return 0;
        var baseXp = MonsterXp(monsterLevel) * MapLevelDifferenceFactor(playerLevel, monsterLevel);
        return (long)Math.Round(baseXp * (1.0 + 0.35 * (participants - 1)) / participants, MidpointRounding.AwayFromZero);
    }

    /// <summary>XP de um abate para um participante: total x (1 + 0,35 x (N-1)) dividido igualmente entre N.</summary>
    public static long KillXp(int monsterLevel, int playerLevel, int participants)
    {
        if (participants < 1) return 0;
        var baseXp = MonsterXp(monsterLevel) * LevelDifferenceFactor(playerLevel, monsterLevel);
        var groupMultiplier = 1.0 + 0.35 * (participants - 1);
        return (long)Math.Round(baseXp * groupMultiplier / participants, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// E9 (modo mapa): curva que cresce com o nível — começo acessível, evolução arrastada depois.
    /// 100 x L x (30 + 18 x L): nível 1 = 4.800, nível 5 = 60.000, nível 10 = 210.000. Escolhida pela simulação
    /// (Tools/simular_progressao.py; valores em Docs/MUNDO_E9.md). Provisória até o usuário jogar.
    /// </summary>
    public const int MapCurveA = 30;
    public const int MapCurveB = 18;
    public static long MapXpToNext(int level) => level >= MaxLevel ? 0 : 100L * level * (MapCurveA + MapCurveB * level);

    public static long XpToNext(int level, bool mapCurve) => mapCurve ? MapXpToNext(level) : XpToNext(level);

    public static (int Level, long Xp, int LevelsGained) AddXp(int level, long xp, long gained, bool mapCurve = false)
    {
        var levels = 0;
        xp += gained;
        while (level < MaxLevel && xp >= XpToNext(level, mapCurve))
        {
            xp -= XpToNext(level, mapCurve);
            level++;
            levels++;
        }
        if (level >= MaxLevel) xp = 0;
        return (level, xp, levels);
    }
}

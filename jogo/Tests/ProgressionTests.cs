using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Content;
using CinzasDaMare.Shared;

namespace CinzasDaMare.Tests;

/// <summary>Regras de XP e níveis do GDD 6.2, sem rede nem banco.</summary>
public class ProgressionTests
{
    [Theory]
    [InlineData(1, 60)]
    [InlineData(19, 60)]
    [InlineData(20, 150)]
    [InlineData(39, 150)]
    [InlineData(40, 280)]
    [InlineData(60, 450)]
    [InlineData(80, 650)]
    [InlineData(100, 900)]
    [InlineData(119, 900)]
    public void K_SegueATabelaDoGdd(int level, int expected) => Assert.Equal(expected, Progression.K(level));

    [Fact]
    public void XpParaProximoNivel_E_100_x_L_x_K()
    {
        Assert.Equal(6_000, Progression.XpToNext(1));
        Assert.Equal(30_000, Progression.XpToNext(5));
        Assert.Equal(60_000, Progression.XpToNext(10));       // 100 x 10 x 60 (exemplo do GDD)
        Assert.Equal(450_000, Progression.XpToNext(30));      // 100 x 30 x 150
        Assert.Equal(0, Progression.XpToNext(Progression.MaxLevel));
    }

    [Fact]
    public void XpDeMonstroComum_E_100_x_Nivel()
    {
        Assert.Equal(100, Progression.MonsterXp(1));
        Assert.Equal(1200, Progression.MonsterXp(12));
    }

    [Theory]
    [InlineData(10, 10, 1.0)]
    [InlineData(10, 15, 1.0)]   // monstro acima: sem bônus
    [InlineData(15, 10, 1.0)]   // 5 abaixo: integral
    [InlineData(16, 10, 0.5)]   // 6 abaixo
    [InlineData(20, 10, 0.5)]   // 10 abaixo
    [InlineData(21, 10, 0.1)]   // mais de 10 abaixo
    public void FatorDeDiferencaDeNivel(int player, int monster, double expected)
        => Assert.Equal(expected, Progression.LevelDifferenceFactor(player, monster), 9);

    [Fact]
    public void XpDeGrupo_MultiplicaPor1MaisPonto35PorMembroExtra_EDivideIgualmente()
    {
        Assert.Equal(100, Progression.KillXp(1, 1, 1));
        Assert.Equal(68, Progression.KillXp(1, 1, 2));   // 100 x 1,35 / 2 = 67,5 -> 68
        // Quatro pessoas recebem 51,25% da XP individual (GDD): 100 x 2,05 / 4 = 51,25
        Assert.Equal(51, Progression.KillXp(1, 1, 4));
        Assert.Equal(0, Progression.KillXp(1, 1, 0));
    }

    [Fact]
    public void AddXp_SobeUmOuVariosNiveis_EGuardaOResto()
    {
        var one = Progression.AddXp(1, 5_900, 200);                  // 6.100: sobe 1, sobra 100
        Assert.Equal((2, 100L, 1), one);

        var none = Progression.AddXp(1, 0, 5_999);
        Assert.Equal((1, 5_999L, 0), none);

        var many = Progression.AddXp(1, 0, 6_000 + 12_000 + 100); // L1->L2 (6.000), L2->L3 (12.000), sobra 100
        Assert.Equal((3, 100L, 2), many);
    }

    [Fact]
    public void NoNivelMaximo_NaoHaNovaBarra()
    {
        var capped = Progression.AddXp(Progression.MaxLevel, 0, 1_000_000);
        Assert.Equal(Progression.MaxLevel, capped.Level);
        Assert.Equal(0L, capped.Xp);
        Assert.Equal(0, capped.LevelsGained);
    }

    [Fact]
    public void SubirDeNivel_RecalculaVidaMaximaESomaADiferencaAVidaAtual()
    {
        var b = TestContent.Load();
        var p = TestContent.Warrior(b, level: 5);
        p.TakeDamage(100);
        var missing = p.MaxHealth - p.Health;

        p.SetLevel(6);

        Assert.Equal(CombatFormulas.MaxHealth(6, 10), p.MaxHealth, 6);
        Assert.Equal(missing, p.MaxHealth - p.Health, 6); // a vida faltante não muda
    }

    [Fact]
    public void RegeneracaoForaDeCombate_E_1PorCentoDaVidaMaximaPorSegundo()
    {
        var b = TestContent.Load();
        var p = TestContent.Warrior(b, level: 5);
        p.TakeDamage(200);
        var before = p.Health;
        p.RegenOutOfCombat(2.0);
        Assert.Equal(before + p.MaxHealth * 0.01 * 2.0, p.Health, 6);

        p.RegenOutOfCombat(10_000); // nunca passa da vida máxima
        Assert.Equal(p.MaxHealth, p.Health, 6);
    }

    [Fact]
    public void ArmaEquipada_AlteraPoderEIntervaloDeAtaque()
    {
        var b = TestContent.Load();
        var p = TestContent.Warrior(b, level: 5);   // nasce com a espada: 12 + 2,2 x 10 + 1,2 x 5 = 40
        var withSword = p.PhysicalPower;

        p.SetWeapon(4, 1300);                        // mãos nuas: 4 + 22 + 6 = 32

        Assert.Equal(40.0, withSword, 6);
        Assert.Equal(32.0, p.PhysicalPower, 6);
        Assert.Equal(1300, p.AttackIntervalMs);
    }

    [Fact]
    public void Validador_ExigeParametrosDoAtaqueBasico()
    {
        var bad = ContentLoader.LoadBalance(Path.Combine(AppContext.BaseDirectory, "Content")) with
        {
            BasicAttackCoefficient = 0,
            BasicAttackRangeM = 0,
            StartingPotions = -1,
            UnarmedAttackIntervalMs = 10,
        };
        var errors = ContentValidator.ValidateBalance(bad);
        Assert.Contains(errors, e => e.Contains("ataque básico"));
        Assert.Contains(errors, e => e.Contains("starting_potions"));
        Assert.Contains(errors, e => e.Contains("unarmed_attack_interval_ms"));
    }
}

using CinzasDaMare.Server.Combat;

namespace CinzasDaMare.Tests;

public class CombatFormulasTests
{
    [Fact]
    public void VidaMaxima_Nivel1_Vigor10_E284()
        => Assert.Equal(284.0, CombatFormulas.MaxHealth(1, 10), 6);

    [Fact]
    public void PoderFisico_ArmaDano12_Forca10_Nivel1_E35ponto2()
        => Assert.Equal(35.2, CombatFormulas.PhysicalPower(12, 10, 1), 6);

    [Fact]
    public void ReducaoPorArmadura_SegueFormulaDoGdd()
        => Assert.Equal(25.0 / 223.0, CombatFormulas.ArmorReduction(25, 1), 9);

    [Fact]
    public void ReducaoPorArmadura_TemTetoDe65Porcento()
        => Assert.Equal(0.65, CombatFormulas.ArmorReduction(1_000_000, 1), 9);

    [Fact]
    public void ReducaoPorArmadura_SemArmadura_EZero()
        => Assert.Equal(0.0, CombatFormulas.ArmorReduction(0, 10), 9);

    [Fact]
    public void DanoPositivoNuncaFicaAbaixoDe1()
        => Assert.Equal(1, CombatFormulas.FinalDamage(0.1, 1.0, 0.5));

    [Fact]
    public void ResistenciaFicaEntreMenos25E60()
    {
        Assert.Equal(-0.25, CombatFormulas.ClampResistance(-0.9), 9);
        Assert.Equal(0.60, CombatFormulas.ClampResistance(0.99), 9);
    }

    [Fact]
    public void PerfilDeMonstro_Nivel1_Combatente()
    {
        var p = MonsterScaling.Create(1, "combatente");
        Assert.Equal(198.45, p.Health, 6);
        Assert.Equal(14.12, p.Damage, 6);
        Assert.Equal(25.0, p.Armor, 6);
        Assert.Equal(2000, p.AttackIntervalMs);
    }

    [Fact]
    public void PerfilDeMonstro_Tanque_TemMaisVidaEAtaqueMaisLento()
    {
        var comb = MonsterScaling.Create(10, "combatente");
        var tanque = MonsterScaling.Create(10, "tanque");
        Assert.Equal(comb.Health * 1.7, tanque.Health, 6);
        Assert.Equal(2857, tanque.AttackIntervalMs);
    }

    [Fact]
    public void ArquetipoDesconhecido_Falha()
        => Assert.Throws<ArgumentException>(() => MonsterScaling.Create(1, "dragao"));
}

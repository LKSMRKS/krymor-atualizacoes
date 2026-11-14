using CinzasDaMare.Server.Combat;
using CinzasDaMare.Shared;

namespace CinzasDaMare.Tests;

public class SkillTests
{
    private readonly ContentBundle _b = TestContent.Load();

    private SkillExecutor Executor(double roll = 0.99) => new(_b, () => roll);

    [Fact]
    public void GolpeDeCunha_CausaDanoEsperado_GastaFolego_EntraEmRecarga()
    {
        var p = TestContent.Warrior(_b);
        var m = TestContent.Crab(_b);

        var r = Executor().TryCast(p, TestContent.Golpe, m, 2.0, 0);

        Assert.True(r.Success);
        Assert.False(r.Crit);
        Assert.Equal(50, r.Damage); // 35,2 x 1,6 x (1 - 25/223)
        Assert.Equal(85.0, p.Resource, 6);
        Assert.Equal(4000, p.SkillReadyAt(TestContent.Golpe));
        Assert.Equal(m.Profile.Health - 50, m.Health, 6);
    }

    [Fact]
    public void Critico_Causa150PorCento()
    {
        var r = Executor(0.0).TryCast(TestContent.Warrior(_b), TestContent.Golpe, TestContent.Crab(_b), 2.0, 0);
        Assert.True(r.Crit);
        Assert.Equal(75, r.Damage);
    }

    [Fact]
    public void RecargaDe4Segundos_BloqueiaAteOInstanteExato()
    {
        var p = TestContent.Warrior(_b);
        var m = TestContent.Crab(_b);
        var ex = Executor();

        Assert.True(ex.TryCast(p, TestContent.Golpe, m, 2.0, 0).Success);
        Assert.Equal(CastFailure.OnCooldown, ex.TryCast(p, TestContent.Golpe, m, 2.0, 3999).Failure);
        Assert.True(ex.TryCast(p, TestContent.Golpe, m, 2.0, 4000).Success);
    }

    [Fact]
    public void FolegoInsuficiente_Rejeita_SemMudarEstado()
    {
        var p = TestContent.Warrior(_b);
        var ex = Executor();
        // Seis golpes (em alvos novos, para nenhum morrer) custam 90 dos 100 de Fôlego; sobra 10 (< 15).
        for (var i = 0; i < 6; i++)
            Assert.True(ex.TryCast(p, TestContent.Golpe, TestContent.Crab(_b), 2.0, i * 4000L).Success);
        var m = TestContent.Crab(_b);
        var healthBefore = m.Health;

        var r = ex.TryCast(p, TestContent.Golpe, m, 2.0, 24000);

        Assert.Equal(CastFailure.NotEnoughResource, r.Failure);
        Assert.Equal(10.0, p.Resource, 6);
        Assert.Equal(healthBefore, m.Health, 6);
    }

    [Fact]
    public void ForaDeAlcance_Rejeita_SemGastarNada()
    {
        var p = TestContent.Warrior(_b);
        var m = TestContent.Crab(_b);

        var r = Executor().TryCast(p, TestContent.Golpe, m, 10.0, 0);

        Assert.Equal(CastFailure.OutOfRange, r.Failure);
        Assert.Equal(100.0, p.Resource, 6);
        Assert.Equal(0, p.SkillReadyAt(TestContent.Golpe));
        Assert.Equal(m.Profile.Health, m.Health, 6);
    }

    [Fact]
    public void SemAlvo_AlvoMorto_E_HabilidadeDesconhecida_SaoRejeitados()
    {
        var p = TestContent.Warrior(_b);
        var m = TestContent.Crab(_b);
        m.TakeDamage(10_000);
        var ex = Executor();

        Assert.Equal(CastFailure.NoTarget, ex.TryCast(p, TestContent.Golpe, null, 1.0, 0).Failure);
        Assert.Equal(CastFailure.TargetDead, ex.TryCast(p, TestContent.Golpe, m, 1.0, 0).Failure);
        Assert.Equal(CastFailure.UnknownSkill, ex.TryCast(p, "skill_inventada", m, 1.0, 0).Failure);
    }

    [Fact]
    public void GuardaDeEscudo_ExigeNivel5()
    {
        var p = TestContent.Warrior(_b, level: 1);
        var r = Executor().TryCast(p, TestContent.Guarda, null, 0, 0);
        Assert.Equal(CastFailure.NotUnlocked, r.Failure);
        Assert.Equal(100.0, p.Resource, 6);
    }

    [Fact]
    public void GuardaDeEscudo_Dura3s_ReduzMovimentoA50Porcento_Custa20()
    {
        var p = TestContent.Warrior(_b, level: 5);
        p.HasShield = true;

        var r = Executor().TryCast(p, TestContent.Guarda, null, 0, 1000);

        Assert.True(r.Success);
        Assert.Equal(80.0, p.Resource, 6);
        Assert.True(p.IsGuarding(1000));
        Assert.True(p.IsGuarding(3999));
        Assert.False(p.IsGuarding(4000));
        Assert.Equal(0.5, p.MoveSpeedMultiplier(2000), 6);
        Assert.Equal(1.0, p.MoveSpeedMultiplier(4000), 6);
        Assert.Equal(11000, p.SkillReadyAt(TestContent.Guarda));
    }

    [Fact]
    public void GolpeDeCunha_ReduzArmaduraEm10PorCentoPor4s()
    {
        var p = TestContent.Warrior(_b);
        var m = TestContent.Crab(_b);

        Executor().TryCast(p, TestContent.Golpe, m, 2.0, 0);

        Assert.Equal(22.5, m.EffectiveArmor(1000), 6);
        Assert.Equal(22.5, m.EffectiveArmor(3999), 6);
        Assert.Equal(25.0, m.EffectiveArmor(4000), 6);
    }

    [Fact]
    public void QuatroGolpesMatamOCaranguejoNivel1()
    {
        var p = TestContent.Warrior(_b);
        var m = TestContent.Crab(_b);
        var ex = Executor();
        CastResult last = default;
        for (var i = 0; i < 4; i++)
            last = ex.TryCast(p, TestContent.Golpe, m, 2.0, i * 4000L);

        Assert.True(last.TargetDied);
        Assert.False(m.Alive);
    }

    [Fact]
    public void Furia_NaoRegeneraSozinha_SoEnchenBatendoComOBasico()   // 11/11: a Fúria só enche com o ataque básico
    {
        var p = TestContent.Warrior(_b, level: 5);
        var m = TestContent.Crab(_b);
        var ex = Executor();
        ex.TryCast(p, TestContent.Golpe, m, 2.0, 0);           // 100 -> 85
        p.TickResource(1.0, inCombat: true, nowMs: 100);
        p.TickResource(5.0, inCombat: false, nowMs: 200);
        Assert.Equal(85.0, p.Resource, 6);                     // parado, não volta
        ex.TryCast(p, "skill_guerreiro_corte", m, 2.0, 5000);  // básico: +10
        Assert.Equal(95.0, p.Resource, 6);
    }

    [Fact]
    public void AtaqueDeMonstro_RespeitaIntervaloEDano()
    {
        var p = TestContent.Warrior(_b);
        var m = TestContent.Crab(_b);

        var h1 = MonsterCombat.TryAttack(m, p, true, 0);
        var h2 = MonsterCombat.TryAttack(m, p, true, 1999);
        var h3 = MonsterCombat.TryAttack(m, p, true, 2000);

        Assert.True(h1.Hit);
        Assert.Equal(14, h1.Damage);
        Assert.False(h2.Hit);
        Assert.True(h3.Hit);
        Assert.Equal(284.0 - 28, p.Health, 6);
    }

    [Fact]
    public void GuardaDeEscudo_SemEscudo_NaoLevanta()   // 12/11: "consigo usar sem escudo"
    {
        var p = TestContent.Warrior(_b, level: 5);
        var r = Executor().TryCast(p, TestContent.Guarda, null, 0, 1000);
        Assert.False(r.Success);
        Assert.Equal(CastFailure.NeedsShield, r.Failure);
        Assert.False(p.IsGuarding(1000));
    }

    [Fact]
    public void GuardaReduz40PorCentoSoDeGolpesFrontais()
    {
        var p = TestContent.Warrior(_b, level: 5);
        p.HasShield = true;
        Executor().TryCast(p, TestContent.Guarda, null, 0, 0);

        var frontal = MonsterCombat.TryAttack(TestContent.Crab(_b), p, true, 1000);
        var lateral = MonsterCombat.TryAttack(TestContent.Crab(_b), p, false, 1000);

        Assert.Equal(8, frontal.Damage);   // 14,12 x 0,6
        Assert.Equal(14, lateral.Damage);
    }

    [Fact]
    public void JogadorMorreQuandoVidaChegaAZero()
    {
        var p = TestContent.Warrior(_b);
        p.TakeDamage(280);
        var hit = MonsterCombat.TryAttack(TestContent.Crab(_b), p, true, 0);
        Assert.True(hit.PlayerDied);
        Assert.False(p.Alive);
        Assert.Equal(CastFailure.CasterDead,
            Executor().TryCast(p, TestContent.Golpe, TestContent.Crab(_b), 1.0, 0).Failure);
    }

    [Fact]
    public void TonicoFraco_Cura15PorCentoEmQuatroSegundos_ComRecargaDe12s()
    {
        var tonico = _b.Consumables["consumable_c01_tonico_fraco"];
        var p = TestContent.Warrior(_b);
        p.TakeDamage(200);                                    // 84 / 284
        Assert.Equal(UseFailure.None, ConsumableSystem.TryUse(p, tonico, 0));

        p.AdvanceHeal(2000);
        Assert.Equal(84 + 21.3, p.Health, 6);
        p.AdvanceHeal(4000);
        Assert.Equal(84 + 42.6, p.Health, 6);
        p.AdvanceHeal(9000);                                  // sem cura extra
        Assert.Equal(84 + 42.6, p.Health, 6);

        Assert.Equal(UseFailure.OnCooldown, ConsumableSystem.TryUse(p, tonico, 11999));
        Assert.Equal(UseFailure.None, ConsumableSystem.TryUse(p, tonico, 12000));
    }

    [Fact]
    public void Morte_InterrompeACuraDaPocao()
    {
        var tonico = _b.Consumables["consumable_c01_tonico_fraco"];
        var p = TestContent.Warrior(_b);
        ConsumableSystem.TryUse(p, tonico, 0);
        p.TakeDamage(10_000);
        p.AdvanceHeal(4000);
        Assert.False(p.Alive);
        Assert.Equal(0.0, p.Health, 6);
        Assert.Equal(UseFailure.Dead, ConsumableSystem.TryUse(p, tonico, 20000));
    }
}

using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using System.Text;

namespace CinzasDaMare.Tests;

public class CombatGeometryTests
{
    // Jogador na origem olhando para +x; arco total de 120 graus (±60).
    [Theory]
    [InlineData(5.0, 0.0, true)]      // de frente
    [InlineData(5.0, 5.0, true)]      // 45 graus
    [InlineData(5.0, 8.6, true)]      // ~59,8 graus (dentro)
    [InlineData(5.0, 8.8, false)]     // ~60,4 graus (fora)
    [InlineData(0.0, 5.0, false)]     // 90 graus: lateral
    [InlineData(-5.0, 0.0, false)]    // pelas costas
    public void ArcoDe120Graus_ClassificaFrenteELados(double ax, double ay, bool expected)
        => Assert.Equal(expected, CombatGeometry.IsFrontal(1, 0, 0, 0, ax, ay, 120));

    [Fact]
    public void ArcoDe360_EhSempreFrontal_ESobrepostoOuSemOrientacaoContaComoFrontal()
    {
        Assert.True(CombatGeometry.IsFrontal(1, 0, 0, 0, -5, 0, 360));
        Assert.True(CombatGeometry.IsFrontal(1, 0, 3, 3, 3, 3, 120));  // mesma posição
        Assert.True(CombatGeometry.IsFrontal(0, 0, 0, 0, -5, 0, 120)); // orientação nula
    }

    [Fact]
    public void OrientacaoNaoPrecisaVirNormalizada()
        => Assert.True(CombatGeometry.IsFrontal(0, 40, 0, 0, 1, 10, 120));
}

/// <summary>Orientação do jogador e Guarda frontal, decididas no servidor.</summary>
public class FacingTests
{
    private static void FaceMonster(ServerHarness h, int id, bool away)
    {
        var me = h.Me(id);
        var mon = h.Monster(id);
        var sign = away ? -1.0 : 1.0;
        h.Transport.ClientSends(id, new FaceIntent(sign * (mon.X - me.X), sign * (mon.Y - me.Y)));
    }

    private static List<CombatEvent> MonsterHits(ServerHarness h, int id)
        => h.Transport.Received<CombatEvent>(id)
            .Where(e => e.AttackerId == GameServer.MonsterId && e.TargetId == h.CharacterId(id)).ToList();

    [Fact]
    public void Orientacao_PadraoEParaMaisX_E_FaceIntentAtualizaNormalizada()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.EquipShield(a);
        Assert.Equal(1.0, h.Me(a).FacingX, 6);
        Assert.Equal(0.0, h.Me(a).FacingY, 6);

        h.Transport.ClientSends(a, new FaceIntent(0, 5));
        h.Ticks(2);

        Assert.Equal(0.0, h.Me(a).FacingX, 6);
        Assert.Equal(1.0, h.Me(a).FacingY, 6);
    }

    [Fact]
    public void Orientacao_VetorNuloEhIgnorado_E_ValorInvalidoEhRejeitado()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.EquipShield(a);
        h.Transport.ClientSends(a, new FaceIntent(0, 5));
        h.Ticks(2);

        h.Transport.ClientSends(a, new FaceIntent(0, 0));
        h.Transport.ClientSendsRaw(a, Encoding.UTF8.GetBytes("{\"t\":\"face\",\"dirX\":1e999,\"dirY\":0}"));
        h.Ticks(2);

        Assert.Equal(1.0, h.Me(a).FacingY, 6); // continua olhando para +y
        Assert.Single(h.Transport.Received<ErrorMessage>(a), e => e.Reason == "bad_face");
    }

    [Fact]
    public void Guarda_ReduzODanoDeFrente_Em40Porcento()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.EquipShield(a);
        h.WalkToMonster(a);
        var before = MonsterHits(h, a).Count;

        FaceMonster(h, a, away: false);
        h.Transport.ClientSends(a, new CastIntent(TestContent.Guarda, null));
        h.Ticks(55); // dentro dos 3 s de guarda; o monstro ataca a cada 2 s

        var hits = MonsterHits(h, a).Skip(before).ToList();
        Assert.NotEmpty(hits);
        Assert.All(hits, e => Assert.Equal(4, e.Damage)); // 12/11: com o Escudo de Tábuas (armadura + bônus de guarda); sem guarda pelas costas = 11
    }

    [Fact]
    public void Guarda_NaoReduzODanoPelasCostas()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.EquipShield(a);
        h.WalkToMonster(a);
        var before = MonsterHits(h, a).Count;

        FaceMonster(h, a, away: true);
        h.Transport.ClientSends(a, new CastIntent(TestContent.Guarda, null));
        h.Ticks(55);

        var hits = MonsterHits(h, a).Skip(before).ToList();
        Assert.NotEmpty(hits);
        Assert.All(hits, e => Assert.Equal(11, e.Damage)); // sem redução da guarda (só a armadura do escudo)
    }

    [Fact]
    public void Respawn_RestauraAOrientacaoPadrao()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.EquipShield(a);
        h.Transport.ClientSends(a, new FaceIntent(-1, 0));
        h.Move(a, 1, 0, 20);

        var died = false;
        var back = false;
        for (var i = 0; i < 1500 && !back; i++)
        {
            h.Ticks(2);
            var me = h.Me(a);
            if (!me.Alive) died = true;
            else if (died) back = true;
        }

        Assert.True(back);
        Assert.Equal(1.0, h.Me(a).FacingX, 6);
    }
}

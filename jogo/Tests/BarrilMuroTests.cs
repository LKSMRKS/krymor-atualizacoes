using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11: parede rachada cai com barril de pólvora (pegar, pôr no chão, acender, explodir).</summary>
public class BarrilMuroTests
{
    private const string Costa = "costa_do_cal";

    [Fact]
    public void Barril_PegaPoeAcendeEExplode()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        // 11/11: os barris ficam escondidos na enseada (atrás da pedra que se empurra)
        var id = h.Join("bia", x: 109.2, y: 28.4, level: 40, map: Costa);
        var ch = h.CharacterId(id);
        h.Temp.Db.TryAddFlag(ch, "pedra:enseada");
        // sem a missão: não pega
        h.Transport.ClientSends(id, new InteractIntent("obj:nara_barris"));
        h.Ticks(1);
        Assert.Equal("need_quest", h.Transport.Received<ActionResultMessage>(id).Last().Reason);

        h.Temp.Db.TryAddFlag(ch, "explosivo:parede");
        id = h.Relocate(id, Costa, 109.2, 28.4);
        h.Transport.ClientSends(id, new InteractIntent("obj:nara_barris"));
        h.Ticks(3);
        Assert.True(h.Me(id).Carry == "barril", h.Transport.Received<ActionResultMessage>(id).Last().Reason + $" {h.Me(id).X} {h.Me(id).Y}");
        // carregando não ataca
        h.Transport.ClientSends(id, new CastIntent("skill_guerreiro_corte", null));
        h.Ticks(1);
        Assert.Equal("Carrying", h.Transport.Received<CastResultMessage>(id).Last().Failure);

        // anda até a rachadura (perto da alavanca) e põe no chão
        foreach (var (wx, wy) in new[] { (109.2, 32.0), (100.0, 32.0), (90.0, 36.0), (60.0, 36.0), (30.0, 20.0), (20.0, 16.0) })
            h.TickUntil(() => { var dx = wx - h.Me(id).X; var dy = wy - h.Me(id).Y; var l = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy)); h.Transport.ClientSends(id, new MoveIntent(dx / l, dy / l)); h.Ticks(1); return l < 1.0; }, 800);
        h.TickUntil(() => { var dx = -30 - h.Me(id).X; var dy = 6.2 - h.Me(id).Y; var l = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy)); h.Transport.ClientSends(id, new MoveIntent(dx / l, dy / l)); h.Ticks(1); return Math.Abs(h.Me(id).X + 30) < 1.2 && Math.Abs(h.Me(id).Y - 6.2) < 1.2; }, 3000);
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Transport.ClientSends(id, new InteractIntent("lever:racha_muro"));
        h.Ticks(3);
        Assert.True(h.Me(id).Carry is null && h.Me(id).Health > 0, $"carregando={h.Me(id).Carry} vida={h.Me(id).Health} em {h.Me(id).X:0.0};{h.Me(id).Y:0.0}");
        Assert.Contains(h.LastSnapshot(id).Effects!, e => e.Kind == "barril");
        h.Transport.ClientSends(id, new InteractIntent("lever:racha_muro"));
        h.Ticks(3);
        Assert.Contains(h.LastSnapshot(id).Effects!, e => e.Kind == "barril_aceso");
        h.Ticks(90);   // 4,5 s
        Assert.Contains("parede:explodida", h.Transport.Received<FlagsMessage>(id).Last().Flags);
        Assert.True(h.Me(id).Health >= 1);
    }
}

/// <summary>14/11 tarde (dono): segredo = caverninha de rochas; 3 golpes na rocha da frente e o baú fica alcançável.</summary>
public class CavernaSecretaTests
{
    [Fact]
    public void RochaDaFrente_TresGolpes_AbreACaverna()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("cava", x: 79.9, y: 44.0, level: 20, map: "costa_do_cal");
        var ch = h.CharacterId(id);
        for (var i = 0; i < 3; i++) { h.Transport.ClientSends(id, new InteractIntent("obj:sala_secreta_parede")); h.Ticks(2); }
        Assert.Contains("parede:costa_do_cal", h.Temp.Db.GetFlags(ch));
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("baú escondido"));
    }
}

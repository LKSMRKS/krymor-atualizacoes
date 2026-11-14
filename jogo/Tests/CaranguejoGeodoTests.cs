using System.Linq;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>10/11 item 47: caranguejo-geodo (encolhe em pedra até alguém chegar perto; só apanha cheio pelas costas).</summary>
public class CaranguejoGeodoTests
{
    [Fact]
    public void Geodo_Conteudo_PedraCostasEGeodos()
    {
        var w = MapHarness.RealWorld();
        var b = w.Behaviors["geode_crab"];
        Assert.True(b.StoneRest);
        Assert.True(b.FrontTakenMult <= 0.2, "de frente quase não apanha");
        Assert.InRange(b.TurnDegPerSec, 30, 120);
        Assert.Contains(b.Attacks, a => a.Shape == "projectile" && a.Count >= 3);
        Assert.Contains(b.Attacks, a => a.SelfGuardMult <= 0.01);
        var geos = w.Maps["costa_do_cal"].Spawns.Where(s => s.Monster == "monster_r1_caranguejo_geodo").ToList();
        Assert.True(geos.Count >= 3, "um por área");
        Assert.All(geos, s => Assert.Equal(5, s.Level));
    }

    [Fact]
    public void Geodo_DormeComoPedra_EAcordaQuandoChegaPerto()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("g", "monster_r1_caranguejo_geodo", 0, -8, level: 5, group: "solo") });
        w.LivelyMonsters = true;
        using var h = new MapHarness(w, rng: () => 0.5);
        var id = h.Join("ana", x: 0, y: 30, level: 5);
        h.Ticks(10);
        var g = h.Monster(id, "m_g")!;
        Assert.Equal("sleep", g.Action);
        Assert.Equal(0, g.X, 1); Assert.Equal(-8, g.Y, 1);
        id = h.Relocate(id, "teste", g.X, g.Y + 3);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_g")?.Action != "sleep", 40), "acorda com o jogador perto");
    }
}

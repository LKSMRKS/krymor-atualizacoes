using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>13/11: bonecos de treino do Mestre Dorval apanham (dano aparece), não revidam e ficam na praça.</summary>
public class BonecoTreinoTests
{
    [Fact]
    public void Boneco_ApanhaENaoRevida()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        // 13/11: os bonecos ficam onde o dono pôs no editor — o teste vai até o primeiro (1,3 m a leste dele)
        var sp = h.World.Maps["porto_das_cinzas"].Spawns.First(s => s.Monster == "monster_boneco_treino");
        var a = h.Join("treinador", level: 5, map: "porto_das_cinzas", x: sp.X + 1.3, y: sp.Y);
        h.Ticks(5);
        var boneco = h.LastSnapshot(a).Monsters.Where(m => m.DefinitionId == "monster_boneco_treino").OrderBy(m => (m.X - sp.X) * (m.X - sp.X) + (m.Y - sp.Y) * (m.Y - sp.Y)).First();
        var vida0 = boneco.Health;
        h.Face(a, -1, 0);
        h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_corte", boneco.Id));
        var ok = h.TickUntil(() =>
        {
            if (h.Monster(a, boneco.Id) is { } b && b.Health < vida0) return true;
            h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_corte", boneco.Id));
            return false;
        }, 200);
        var me = h.Me(a);
        Assert.True(ok, $"boneco {boneco.X},{boneco.Y} hp {h.Monster(a, boneco.Id)?.Health}/{vida0} eu {me.X},{me.Y} ação {me.Action} ev {string.Join(";", h.Events(a).Take(5))} rej {string.Join(";", h.Transport.Received<CinzasDaMare.Shared.Net.ActionResultMessage>(a).Select(r => r.ToString()).Take(3))}");
        h.Ticks(100);
        Assert.Equal(h.Me(a).MaxHealth, h.Me(a).Health);
    }
}

public class QuedaDeNivelTests
{
    [Fact]
    public void Morrer_No31_SemXp_CaiParaO30_EAbaixoDo30NaoCai()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("g", "monster_r1_goblin_saqueador", 0, 2, level: 60, group: "solo") });
        w.LivelyMonsters = true;
        using var h = new MapHarness(w, rng: () => 0.99);
        var id = h.Join("ana", x: 0, y: -1, level: 31);
        var g = h.LastSnapshot(id).Monsters.First();
        id = h.Relocate(id, "teste", g.X, g.Y - 1.5);
        Assert.True(h.TickUntil(() => !h.Me(id).Alive, 900), "o goblin nível 60 derruba");
        Assert.Equal(30, h.Me(id).Level);
        Assert.Equal(31, h.Temp.Db.GetMaxLevel(h.CharacterId(id)));
    }
}

using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>13/11 (pedido 17:33): reino Era Uma Vez, bichos novos e a escolha do tritão.</summary>
public class FabulaTests
{
    [Fact]
    public void EraUmaVez_PeloTiao_ComVolta_ECincoDaFamilia()
    {
        var w = MapHarness.RealWorld();
        var tiao = w.Maps["porto_das_cinzas"].Npcs.Single(n => n.Id == "bobo_tiao");
        var t = tiao.Travel.Single(x => x.ToMap == "reino_fabula");
        Assert.Equal(24, t.MinLevel);
        var m = w.Maps["reino_fabula"];
        Assert.Contains(m.Portals, p => p.ToMap is "porto_das_cinzas" or "reinos_ocultos");   // 14/11: a volta cai no Coração dos Reinos
        var fam = w.Quests["q_fab_familia"];
        Assert.Equal(5, fam.Count);
        Assert.All(fam.Targets, id => Assert.Contains(m.Spawns, s => s.Monster == id));
        Assert.Equal("fab:malaquias_revelado", m.Npcs.Single(n => n.Id == "bobo_malaquias").ForbidFlag);
        Assert.Contains(w.Quests["q_fab_malaquias_2"].RewardFlags, f => f == "fab:malaquias_revelado");
    }

    [Fact]
    public void Tritao_SalvarOuAcabar_UmFechaOOutro()
    {
        var w = MapHarness.RealWorld();
        var salvar = w.Quests["q_fab_tritao_salvar"];
        var acabar = w.Quests["q_fab_tritao_acabar"];
        Assert.Equal("fab:tritao_morto", salvar.ForbidFlag);
        Assert.Equal("fab:tritao_salvo", acabar.ForbidFlag);
        Assert.Contains("fab:tritao_salvo", salvar.RewardFlags);
        Assert.Contains("fab:tritao_morto", acabar.RewardFlags);
    }

    [Fact]
    public void Macacos_FicamNoGalho()
    {
        var w = MapHarness.RealWorld();
        var macacos = w.Maps["mata_dos_enforcados"].Spawns.Where(s => s.Monster == "monster_fab_macaco_coqueiro").ToList();
        Assert.True(macacos.Count >= 5);
        Assert.All(macacos, s => Assert.True(s.Perch > 0));
    }

    [Fact]
    public void Dentuco_EsfregaAPata_DepoisCabecada()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("d", "monster_fab_dentuco", 0, 6, level: 24, group: "solo") });
        using var h = new MapHarness(w);
        var id = h.Join("ana", x: 0, y: 0, level: 40);
        var acoes = new System.Collections.Generic.List<string>();
        for (var i = 0; i < 160; i++)
        {
            h.Ticks(1);
            var d = h.LastSnapshot(id).Monsters.FirstOrDefault();
            if (d is not null && (acoes.Count == 0 || acoes[^1] != d.Action)) acoes.Add(d.Action);
        }
        var esfrega = acoes.IndexOf("windup:esfrega_pata");
        Assert.True(esfrega >= 0, string.Join(" ", acoes));
        Assert.Contains(acoes.Skip(esfrega), a => a.EndsWith("cabecada"));
    }
}

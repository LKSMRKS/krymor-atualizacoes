using System;
using System.Linq;
using CinzasDaMare.Shared.World;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>13/11 ("dá para atravessar o bobo e todos os NPCs — cada um tem que ter sua hitbox"): ninguém passa por dentro
/// de um NPC nem dos caixotes do bobo da praça.</summary>
public class NpcColisaoTests
{
    [Fact]
    public void AndarContraONpc_ParaNaFrenteDele()
    {
        var w = MapHarness.RealWorld();
        foreach (var m in new[] { "porto_das_cinzas", "astralia", "vila_das_mos" })
        {
            var map = w.Maps[m];
            var geo = new MapGeometry(map);
            foreach (var n in map.Npcs.Take(12))
            {
                // vem de 3 m ao sul andando 6 m em linha reta para o norte, em passos de tick (5,5 m/s × 50 ms)
                double x = n.X, y = n.Y - 3, menor = 99;
                if (!geo.IsFree(x, y, 0.45)) continue;
                for (var i = 0; i < 22; i++)
                {
                    (x, y) = geo.Move(x, y, 0, 0.275, 0.45);
                    menor = Math.Min(menor, Math.Sqrt((x - n.X) * (x - n.X) + (y - n.Y) * (y - n.Y)));
                }
                Assert.True(menor > 0.7, $"{m}/{n.Id}: chegou a {menor:0.00} m do centro");
            }
        }
    }

    [Fact]
    public void CaixotesDoBobo_SaoSolidos()
    {
        var map = MapHarness.RealWorld().Maps["porto_das_cinzas"];
        var bobo = map.Npcs.First(n => n.Look == "bobo");
        var geo = new MapGeometry(map);
        double x = bobo.X + 1.0, y = bobo.Y + 0.3 - 3;   // caixote pequeno ao lado
        for (var i = 0; i < 22; i++) (x, y) = geo.Move(x, y, 0, 0.275, 0.45);
        Assert.True(Math.Abs(y - (bobo.Y + 0.3)) > 0.7);
    }

    /// <summary>13/11 23:39 ("quando eu encosto nos bonecos de teste eles somem"): o boneco não sai do lugar quando você
    /// encosta (antes era empurrado para fora da zona segura — o Porto inteiro — e sumia da cidade).</summary>
    [Fact]
    public void EncostarNoBoneco_ElePermaneceNoLugar()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var sp = h.World.Maps["porto_das_cinzas"].Spawns.First(s => s.Monster == "monster_boneco_treino");
        var id = h.Join("ana", x: sp.X - 2.0, y: sp.Y, level: 5, map: "porto_das_cinzas");
        h.Transport.ClientSends(id, new MoveIntent(1, 0));
        h.Ticks(40);
        var boneco = h.LastSnapshot(id).Monsters.Where(m => m.DefinitionId == "monster_boneco_treino")
            .OrderBy(m => Math.Abs(m.X - sp.X) + Math.Abs(m.Y - sp.Y)).First();
        Assert.True(Math.Abs(boneco.X - sp.X) + Math.Abs(boneco.Y - sp.Y) < 0.3, $"boneco foi para ({boneco.X:0.0}, {boneco.Y:0.0})");
        Assert.NotEqual("sleep", boneco.Action);
    }
}

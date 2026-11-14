using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>13/11 (pedido do dono): plantas novas das dungeons, em labirinto e com formatos diferentes
/// (Tools/gerar_plantas_dg.py). Confere, andando numa grade de 0,5 m com o raio do jogador, que da entrada se chega
/// ao chefe e ao portal do fim, e que o caminho é bem mais longo que a linha reta (é labirinto, não corredor).</summary>
public class PlantasDgTests
{
    [Theory]
    [InlineData("cripta_do_sino")]
    [InlineData("galerias_piedade")]
    [InlineData("mosteiro_afundado")]
    [InlineData("mercado_mortos")]
    [InlineData("salao_geada")]
    [InlineData("fornalha")]
    [InlineData("labirinto_juramento")]
    [InlineData("castelo_vespera")]
    [InlineData("fortaleza_geada")]
    [InlineData("cidadela_escoria")]
    [InlineData("merlin_torre_torta")]
    [InlineData("merlin_poco_estrelas")]
    [InlineData("merlin_sonho_dragao")]
    public void Labirinto_DaEntradaChegaAoChefeEAoFim_PorCaminhoTorto(string mapa)
    {
        var m = MapHarness.RealWorld().Maps[mapa];
        var g = new MapGeometry(m);
        Assert.True(m.Obstacles.Count >= 25, $"{mapa}: só {m.Obstacles.Count} paredes");
        const double passo = 0.5, raio = 0.45;
        var n = (int)Math.Ceiling(m.HalfSize * 2 / passo);
        (int, int) Cel(double x, double y) => ((int)Math.Round((x + m.HalfSize) / passo), (int)Math.Round((y + m.HalfSize) / passo));
        var dist = new Dictionary<(int, int), int>();
        var ini = Cel(m.SpawnX, m.SpawnY);
        Assert.True(g.IsFree(m.SpawnX, m.SpawnY, raio), $"{mapa}: entrada presa");
        var fila = new Queue<(int, int)>();
        dist[ini] = 0;
        fila.Enqueue(ini);
        while (fila.Count > 0)
        {
            var c = fila.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var v = (c.Item1 + dx, c.Item2 + dy);
                if (v.Item1 < 0 || v.Item2 < 0 || v.Item1 > n || v.Item2 > n || dist.ContainsKey(v)) continue;
                if (!g.IsFree(v.Item1 * passo - m.HalfSize, v.Item2 * passo - m.HalfSize, raio)) continue;
                dist[v] = dist[c] + 1;
                fila.Enqueue(v);
            }
        }
        int Ate(double x, double y)
        {
            var (cx, cy) = Cel(x, y);
            var perto = dist.Where(kv => Math.Abs(kv.Key.Item1 - cx) <= 4 && Math.Abs(kv.Key.Item2 - cy) <= 4).Select(kv => kv.Value).ToList();
            Assert.True(perto.Count > 0, $"{mapa}: ({x:0.0},{y:0.0}) inalcançável a pé");
            return perto.Min();
        }
        var chefe = m.Spawns.FirstOrDefault(s => s.Monster == m.FirstClearBoss) ?? m.Spawns.First(s => s.Id.EndsWith("boss"));
        var aPe = Ate(chefe.X, chefe.Y) * passo;
        var reta = Math.Sqrt(Math.Pow(chefe.X - m.SpawnX, 2) + Math.Pow(chefe.Y - m.SpawnY, 2));
        Assert.True(aPe >= reta * 1.4, $"{mapa}: caminho {aPe:0} m, reta {reta:0} m — pouco labirinto");
        foreach (var p in m.Portals) Ate(p.X, p.Y);
        foreach (var s in m.Spawns) Ate(s.X, s.Y);
    }
}

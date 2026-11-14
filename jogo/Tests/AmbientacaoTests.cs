using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>14/11 (pedido do dono: "os próximos lugares estão bem simples comparado com o Porto e a Praia do Cal"):
/// Tools/gerar_ambientacao.py monta cenas da região nos lugares com nome, mata na borda, postes nas estradas, rochas e
/// coleta. Aqui: o mapa ficou cheio (≥ 180 peças) E nada do que foi posto bloqueia o caminho — andando numa grade de
/// 0,5 m com o raio do jogador, da entrada se chega a todos os portais, NPCs e monstros.</summary>
public class AmbientacaoTests
{
    [Theory]
    [InlineData("vale_das_cinzas")]
    [InlineData("mata_dos_enforcados")]
    [InlineData("canions_suplicantes")]
    [InlineData("brejo_da_viuva")]
    [InlineData("vespera")]
    [InlineData("coroa_da_geada")]
    [InlineData("mar_de_escoria")]
    [InlineData("acropole_das_cinzas")]
    [InlineData("reino_lua")]
    [InlineData("reino_transilvania")]
    [InlineData("reino_inferno")]
    [InlineData("reino_celeste")]
    [InlineData("floresta_oculta")]
    [InlineData("monte_prefeito")]
    [InlineData("ruinas_automatos")]
    [InlineData("triangulo_demonio")]
    [InlineData("fundo_triangulo")]
    [InlineData("ilha_serpente")]
    [InlineData("pico_vento")]
    [InlineData("caminho_serpente")]
    public void MapaAmbientado_TemCenario_ENadaBloqueiaOCaminho(string mapa)
    {
        var m = MapHarness.RealWorld().Maps[mapa];
        Assert.True(m.Props.Count >= 180, $"{mapa}: só {m.Props.Count} peças de cenário");
        var g = new MapGeometry(m);
        const double passo = 0.5, raio = 0.45;
        var n = (int)Math.Ceiling(m.HalfSize * 2 / passo);
        (int, int) Cel(double x, double y) => ((int)Math.Round((x + m.HalfSize) / passo), (int)Math.Round((y + m.HalfSize) / passo));
        var visto = new HashSet<(int, int)>();
        var fila = new Queue<(int, int)>();
        var ini = Cel(m.SpawnX, m.SpawnY);
        visto.Add(ini); fila.Enqueue(ini);
        while (fila.Count > 0)
        {
            var c = fila.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var v = (c.Item1 + dx, c.Item2 + dy);
                if (v.Item1 < 0 || v.Item2 < 0 || v.Item1 > n || v.Item2 > n || visto.Contains(v)) continue;
                if (!g.IsFree(v.Item1 * passo - m.HalfSize, v.Item2 * passo - m.HalfSize, raio)) continue;
                visto.Add(v); fila.Enqueue(v);
            }
        }
        bool Chega(double x, double y)
        {
            var (cx, cy) = Cel(x, y);
            for (var i = -5; i <= 5; i++)
                for (var j = -5; j <= 5; j++)
                    if (visto.Contains((cx + i, cy + j))) return true;
            return false;
        }
        // o que fica do lado de lá de um portão com selo (a muralha norte, que abre depois do chefe) é fechado de propósito
        var selos = m.Gates.Where(gt => gt.Kind == "selo").ToList();
        bool AlemDoSelo(double x, double y) => selos.Any(gt => y < gt.Y - 0.5) || m.Gates.Any(gt => Math.Abs(x - gt.X) < 12 && Math.Abs(y - gt.Y) < 12 && gt.Kind == "selo");
        foreach (var p in m.Portals.Where(p => !AlemDoSelo(p.X, p.Y)))
            Assert.True(Chega(p.X, p.Y), $"{mapa}: portal {p.Id} ficou fora de alcance");
        foreach (var npc in m.Npcs) Assert.True(Chega(npc.X, npc.Y), $"{mapa}: NPC {npc.Id} ficou fora de alcance");
        var presos = m.Spawns.Where(s => !AlemDoSelo(s.X, s.Y) && !Chega(s.X, s.Y)).Select(s => s.Id).ToList();
        Assert.True(presos.Count == 0, $"{mapa}: monstros fora de alcance: {string.Join(", ", presos.Take(8))}");
    }
}

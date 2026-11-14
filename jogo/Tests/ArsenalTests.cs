using System;
using System.IO;
using CinzasDaMare.Shared;
using System.Linq;

namespace CinzasDaMare.Tests;

/// <summary>11/11: armas do arsenal (Tools/gerar_arsenal.py) — carregam, têm modelo e ícone, e a curva de poder sobe com a qualidade.</summary>
public class ArsenalTests
{
    [Fact]
    public void Arsenal_CarregaComModeloIconeECurvaCerta()
    {
        var content = ContentLoader.Load(MapHarness.ContentDir);
        var arsenal = content.Items.Values.Where(d => d.Id.StartsWith("arsenal_")).ToList();
        Assert.True(arsenal.Count >= 50, "poucas armas");
        // pasta do projeto (o teste roda dentro de bin/): sobe até achar Client/Assets
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Client", "Assets"))) dir = dir.Parent;
        var client = dir is null ? null : Path.Combine(dir.FullName, "Client", "Assets");
        foreach (var d in arsenal)
        {
            if (client is null) break;   // cópia sem o cliente: só confere os valores
            Assert.True(File.Exists(Path.Combine(client, "Models", "armas", d.ModelAssetId + ".glb")), d.Id + " sem modelo");
            Assert.True(File.Exists(Path.Combine(client, "Art", "icones", "armas", d.IconAssetId + ".png")), d.Id + " sem ícone");
            Assert.Single(d.AllowedClasses);
        }
        foreach (var forma in new[] { "faca", "katana", "machado_guerra", "arco_longo" })
        {
            var p = new[] { "enferrujada", "comum", "refinada", "excelente" }
                .Select(q => content.Items["arsenal_" + forma + "_" + q].BaseStats["weapon_power"]).ToList();
            Assert.True(p.SequenceEqual(p.OrderBy(x => x)) && p.Distinct().Count() == 4, forma);
        }
    }
}

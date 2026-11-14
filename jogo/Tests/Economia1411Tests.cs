using System;
using System.IO;
using System.Linq;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>
/// 14/11 noite (pedidos do dono: "tudo progressivo, matematicamente — o item tem que valer o que cobra"; "drops fracos, use o
/// sistema de drop do MU"; pranchas de asas, capas e skins).
/// </summary>
public class Economia1411Tests
{
    [Fact]
    public void Economia_TodoEquipamentoTemValor_EOPrecoSobeComONivel()
    {
        var content = ContentLoader.Load(MapHarness.ContentDir);
        var gear = content.Items.Values.Where(d => d.Category is "weapon" or "armor" or "helmet" or "gloves" or "boots" or "pants" or "offhand" or "cape" or "wings").ToList();
        Assert.All(gear, d => Assert.True(d.Price > 0, d.Id + " sem valor (não dá para vender)"));
        // armas das regiões (referência): cada faixa custa mais que a anterior
        foreach (var cls in new[] { "guerreiro", "mago" })
        {
            var armas = content.Items.Values.Where(d => d.Category == "weapon" && d.Set is null && d.AllowedClasses.Count == 1 && d.AllowedClasses[0] == cls
                && d.Source.Contains("Loja", StringComparison.OrdinalIgnoreCase) == false && d.Id.Contains("_midgame") == false).ToList();
            var porNivel = armas.GroupBy(d => d.MinLevel / 10).OrderBy(g => g.Key).Select(g => g.Average(d => d.Price)).ToList();
            for (var i = 1; i < porNivel.Count; i++) Assert.True(porNivel[i] > porNivel[i - 1] * 0.9, $"{cls}: faixa {i} mais barata que a anterior");
        }
    }

    [Fact]
    public void OpcoesMu_ComumLiso_ExcelenteSempreTemOpcaoExcelente_EDeterministico()
    {
        Assert.Empty(OpcoesMu.Rolar("x1", "comum", "weapon", 30).Linhas);
        Assert.Empty(OpcoesMu.Rolar("x1", "excelente", "material", 30).Linhas);
        var comSorte = 0;
        for (var i = 0; i < 200; i++)
        {
            var r = OpcoesMu.Rolar("item" + i, "excelente", "weapon", 40);
            Assert.Contains(r.Linhas, l => l.Text.StartsWith("★"));
            Assert.Equal(r.Linhas.Count, OpcoesMu.Rolar("item" + i, "excelente", "weapon", 40).Linhas.Count);
            if (r.Sorte) comSorte++;
        }
        Assert.InRange(comSorte, 60, 140);   // Excelente: ~50% com Sorte
        // degraus: cada qualidade acima traz mais opções em média
        double Media(string q) => Enumerable.Range(0, 300).Average(i => OpcoesMu.Rolar("a" + i, q, "armor", 30).Linhas.Count);
        Assert.True(Media("incomum") < Media("raro") && Media("raro") < Media("epico") && Media("epico") < Media("excelente"));
    }

    [Fact]
    public void Colecao_25Asas15Capas6Skins_ComIconeENiveisEmOrdem()
    {
        var content = ContentLoader.Load(MapHarness.ContentDir);
        var asas = content.Items.Values.Where(d => d.Id.StartsWith("asas_") && !d.Id.StartsWith("asas_set_")).OrderBy(d => d.MinLevel).ToList();
        Assert.Equal(25, asas.Count);
        Assert.All(asas, a => Assert.Equal(4, a.AllowedClasses.Count));
        // a lendária vale mais que a comum (poder e preço)
        Assert.True(asas[^1].BaseStats["power_pct"] > asas[0].BaseStats["power_pct"] * 3);
        Assert.True(asas[^1].Price > asas[0].Price * 5);
        foreach (var c in new[] { "heroi", "mago", "sombria", "real", "corvo", "amaldicoada", "lava", "folhas", "pirata", "celestial", "dragao", "gelo", "luz", "morte", "tempo" })
            Assert.True(content.Items.ContainsKey("capa_" + c), "falta capa_" + c);
        foreach (var s in new[] { "macaco_vermelho", "caveira", "alien", "tubarao", "fantasma", "lava" })
            Assert.Equal("bracelet", content.Items["bracelete_" + s].Category);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Client", "Assets"))) dir = dir.Parent;
        if (dir is null) return;
        foreach (var d in asas) Assert.True(File.Exists(Path.Combine(dir.FullName, "Client", "Assets", "Art", "ico_" + d.Id + ".png")), d.Id + " sem ícone");
    }
}

public class Pandora1411Tests
{
    [Fact]
    public void CaixaDePandora_JogadaNoChao_EstouraEmItens_ENaoAbreNaMao()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("pan", x: 10, y: 10, level: 30, map: "porto_das_cinzas");
        var ch = h.CharacterId(id);
        h.Temp.Db.CreateInventoryItem(ch, "caixa_pandora_3", "comum", 2, "t:pandora");
        id = h.Relocate(id, "porto_das_cinzas", 10, 10);
        var caixa = h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == "caixa_pandora_3");
        h.Transport.ClientSends(id, new UseItemIntent(caixa.ItemId));
        h.Ticks(1);
        Assert.Equal(2, h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == "caixa_pandora_3").Quantity);   // na mão não abre
        var antes = h.LastSnapshot(id).Items.Count;
        var moedas = h.Temp.Db.GetCoins(ch);
        h.Transport.ClientSends(id, new DropItemIntent(caixa.ItemId));
        h.Ticks(2);
        Assert.Equal(1, h.Temp.Db.GetInventory(ch).First(e => e.DefinitionId == "caixa_pandora_3").Quantity);
        Assert.True(h.LastSnapshot(id).Items.Count - antes >= 5, "+3 espalha de 5 a 7 itens");
        Assert.DoesNotContain(h.LastSnapshot(id).Items, i => i.DefinitionId == "caixa_pandora_3");
        Assert.True(h.Temp.Db.GetCoins(ch) > moedas);
    }
}

public class Poder1411Tests
{
    /// <summary>14/11 (dono: "confere um por um — tudo alinhado com a progressão"): Tools/alinhar_poder.py.</summary>
    [Fact]
    public void Poder_CadaFamiliaNaSuaFaixa_ELendarioNaoPassaOsConjuntosDoDono()
    {
        var c = ContentLoader.Load(MapHarness.ContentDir);
        var fr = new System.Collections.Generic.Dictionary<string, double> { ["armor"] = 1.0, ["helmet"] = 0.35, ["pants"] = 0.5, ["gloves"] = 0.33, ["boots"] = 0.4 };
        foreach (var d in c.Items.Values.Where(d => d.MinLevel >= 3))
        {
            var lend = d.Quality == "lendario" || d.Id.StartsWith("lendaria_") || d.Id.StartsWith("arma_lend_") || d.Id.StartsWith("legend_");
            var L = d.MinLevel;
            if (d.Category == "weapon" && d.BaseStats.TryGetValue("weapon_power", out var wp))
            {
                var conj = d.Subtype is "staff" or "talisman" or "wand" or "orb";
                var iv = d.BaseStats.GetValueOrDefault("attack_interval_ms", 1000);
                var alvo = ((conj ? 2.2 : 2.6) * L + 8) * iv / 1000 / Math.Max(1, d.BaseStats.GetValueOrDefault("projectile_count", 1));
                Assert.InRange(wp / alvo, 0.88, lend ? 1.2 : 1.16);
            }
            else if (fr.TryGetValue(d.Category, out var f) && d.BaseStats.TryGetValue("armor", out var a) && d.Subtype?.Contains("transform") != true)
            {
                var cls = d.AllowedClasses;
                var (k, b) = d.Subtype == "plate" || cls.SequenceEqual(new[] { "guerreiro" }) ? (14.3, 30.0)
                    : d.Subtype is "robe" or "cloth" || (cls.Count > 0 && cls.All(x => x is "mago" or "bruxa")) ? (6.7, 30.0) : (9.7, 40.0);
                Assert.InRange(a / ((k * L + b) * f), 0.88, 1.2);
            }
        }
        var ultimoDoDono = c.Items.Values.Where(d => d.Set is { Length: > 0 } && d.Quality != "lendario").Max(d => d.MinLevel);
        Assert.True(c.Items.Values.Where(d => d.Quality == "lendario" && d.Set is { Length: > 0 }).All(d => d.MinLevel <= ultimoDoDono),
            "lendário de conjunto acima dos últimos conjuntos do dono");
    }
}

using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11 (pedido do dono): cada transformação tem um reino próprio (Lua, Transilvânia, Inferno, Cidade Celestial,
/// Floresta Oculta). Ler o livro na Biblioteca do Continente abre a viagem com a Sibila (ou o Tião, para a máscara).</summary>
public class ReinosTests
{
    [Theory]
    [InlineData("reino_lua", "monster_lua_lobisomem_alfa")]
    [InlineData("reino_transilvania", "monster_trans_conde")]
    [InlineData("reino_inferno", "monster_inferno_ceifador_rei")]
    [InlineData("reino_celeste", "monster_ceu_demonio_invasor")]
    public void Reino_TemChefe_EVoltaAoPorto(string mapa, string chefe)
    {
        var w = MapHarness.RealWorld();
        var m = w.Maps[mapa];
        Assert.Contains(m.Spawns, s => s.Monster == chefe && s.Level == 38);
        Assert.All(m.Spawns.Where(s => s.Monster != chefe && !s.Id.StartsWith("portoes_")), s => Assert.InRange(s.Level, 35, 37));
        Assert.Contains(m.Portals, p => p.ToMap is "porto_das_cinzas" or "reinos_ocultos");   // 14/11: a volta cai no Coração dos Reinos
    }

    [Fact]
    public void FormasSaoDeNivel35_UmaPorClasse_EMascara20()
    {
        var formas = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<CinzasDaMare.Server.Combat.FormaDefinition>>(
            System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Content", "transformacoes.json")))!.ToDictionary(f => f.Id);
        Assert.Equal("guerreiro", formas["forma_lobisomem"].Class);
        Assert.Equal("arqueiro", formas["forma_vampiro"].Class);
        Assert.Equal("mago", formas["forma_ceifador"].Class);
        Assert.Equal("bruxa", formas["forma_anjo"].Class);
        Assert.All(new[] { "forma_lobisomem", "forma_vampiro", "forma_ceifador", "forma_anjo" }, f => Assert.Equal(35, formas[f].MinLevel));
        Assert.Equal(20, formas["forma_pandemonio"].MinLevel);
    }

    [Fact]
    public void Sibila_SoLevaALua_DepoisDoLivro()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var sib = MapHarness.RealWorld().Maps["porto_das_cinzas"].Npcs.First(n => n.Id == "sibila_vidente");
        var id = h.Join("leo", level: 35, map: "porto_das_cinzas", x: sib.X + 1, y: sib.Y);
        h.Temp.Db.AddCoins(h.CharacterId(id), 2000, "t:c");
        h.Transport.ClientSends(id, new PaidTravelIntent("sibila_vidente", "reino_lua"));
        h.Ticks(1);
        Assert.Equal("unknown_destination", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "livro:lobisomem");
        id = h.Relocate(id, "porto_das_cinzas", sib.X + 1, sib.Y);
        h.Transport.ClientSends(id, new PaidTravelIntent("sibila_vidente", "reino_lua"));
        h.Ticks(2);
        Assert.Equal("reino_lua", h.Last<MapMessage>(id).Map.Id);
    }

    [Fact]
    public void Biblioteca_TemOsQuatroLivrosDasFormas_EADaMascara()
    {
        var b = MapHarness.RealWorld().Maps["biblioteca_continente"];
        foreach (var f in new[] { "livro:lobisomem", "livro:vampiro", "livro:ceifador", "livro:anjo", "livro:mascara" })
            Assert.Contains(b.Objects, o => o.Kind == "livro" && o.GrantFlag == f);
    }
}

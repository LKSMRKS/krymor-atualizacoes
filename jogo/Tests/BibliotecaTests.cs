using System.Linq;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11 (Biblioteca do Continente): a bibliotecária do Porto lista, por região, cada monstro e o grupo de drop dele.</summary>
public class BibliotecaTests
{
    [Fact]
    public void Biblioteca_ListaMonstrosDaCostaComPecasDeConjunto()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var lib = h.Server.BuildLibrary();
        Assert.NotEmpty(lib);
        var costa = lib.Where(e => e.Region.Contains("Costa")).ToList();
        Assert.NotEmpty(costa);
        Assert.All(costa, e => Assert.NotEmpty(e.Drops));
        Assert.Contains(costa, e => e.Drops.Any(d => d.StartsWith("Peça de conjunto")));
        // um chefe sempre vai mais alto que um bicho comum da mesma região
        Assert.Contains(lib, e => e.Tag == "Chefe" && e.MaxVariation >= 6);
    }

    [Fact]
    public void Bibliotecaria_EstaNoPorto()
    {
        var w = MapHarness.RealWorld();
        Assert.Contains(w.Maps["biblioteca_cais"].Npcs, n => n.Role == "biblioteca");
    }

    [Fact]
    public void BibliotecaPequena_SoTemAsRegioesVizinhas()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var lib = h.Server.BuildLibrary(new System.Collections.Generic.HashSet<string> { "porto", "costa" });
        Assert.NotEmpty(lib);
        Assert.All(lib, e => Assert.StartsWith("Costa do Cal", e.Region));
    }
}

/// <summary>12/11: Biblioteca do Continente — escondida no nicho do Vale; livros contam a história e abrem segredos.</summary>
public class BibliotecaContinenteTests
{
    [Fact]
    public void Portal_NoNichoDoVale_LevaABiblioteca()
    {
        var w = MapHarness.RealWorld();
        Assert.Contains(w.Maps["vale_das_cinzas"].Portals, p => p.ToMap == "biblioteca_continente");
        var b = w.Maps["biblioteca_continente"];
        Assert.Contains(b.Npcs, n => n.Role == "biblioteca_continente");
        Assert.True(b.Objects.Count(o => o.Kind == "livro") >= 6);
    }

    [Fact]
    public void LerLivro_MostraTexto_EGravaASegredo()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var livro = MapHarness.RealWorld().Maps["biblioteca_continente"].Objects.First(o => o.Id == "livro_uivo");
        var a = h.Join("ana", level: 15, map: "biblioteca_continente", x: livro.X + 0.8, y: livro.Y);
        h.Ticks(2);
        h.Transport.ClientSends(a, new CinzasDaMare.Shared.Net.InteractIntent("obj:livro_uivo"));
        h.Ticks(2);
        var bm = h.Last<CinzasDaMare.Shared.Net.BookMessage>(a);
        Assert.Contains("Lua", bm.Title);
        Assert.NotNull(bm.Discovery);
        Assert.Contains("livro:lobisomem", h.Temp.Db.GetFlags(h.CharacterId(a)));
    }
}

/// <summary>12/11: Livro dos Monstros pessoal (L): trancado antes do nível 10; depois, só o que você já derrotou.</summary>
public class LivroMonstrosTests
{
    [Fact]
    public void Livro_TrancadoAntesDoDez_DepoisMostraOsVistos()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("nina", level: 5);
        h.Ticks(2);
        h.Transport.ClientSends(a, new CinzasDaMare.Shared.Net.BestiaryIntent());
        h.Ticks(2);
        Assert.True(h.Last<CinzasDaMare.Shared.Net.BestiaryMessage>(a).Locked);

        var b = h.Join("rui", level: 12);
        h.Temp.Db.TryAddFlag(h.CharacterId(b), "visto:monster_r1_caranguejo_de_cal");
        b = h.Relocate(b, "porto_das_cinzas", 0, -9.5);
        h.Ticks(2);
        h.Transport.ClientSends(b, new CinzasDaMare.Shared.Net.BestiaryIntent());
        h.Ticks(2);
        var m = h.Last<CinzasDaMare.Shared.Net.BestiaryMessage>(b);
        Assert.False(m.Locked);
        Assert.Equal(1, m.Seen);
        Assert.All(m.Entries, e => Assert.Equal("monster_r1_caranguejo_de_cal", e.MonsterId));
    }
}

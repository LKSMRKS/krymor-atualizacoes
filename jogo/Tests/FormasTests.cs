using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11: relíquia transforma (visual + atributos), não some ao usar, acaba sozinha; na cidade não pega.</summary>
public class FormasTests
{
    [Fact]
    public void Reliquia_Transforma_ETemRecarga_ENaoSome()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("lupe", level: 36, map: "costa_do_cal", x: 0, y: -60);
        var id = h.CharacterId(a);
        var relic = h.Temp.Db.CreateInventoryItem(id, "reliquia_presa_lunar", "comum", 1, "t:presa")!;
        h.Ticks(2);
        var hpAntes = h.Me(a).MaxHealth;
        h.Transport.ClientSends(a, new UseItemIntent(relic));
        h.Ticks(2);
        var me = h.Me(a);
        Assert.Equal("forma_lobisomem", me.Form);
        Assert.True(me.MaxHealth > hpAntes, "a forma dá vida a mais");
        Assert.Contains(h.Temp.Db.GetInventory(id), e => e.ItemId == relic);   // reutilizável
        // de novo: já transformado / em recarga
        h.Transport.ClientSends(a, new UseItemIntent(relic));
        h.Ticks(2);
        Assert.False(h.Last<ActionResultMessage>(a).Success);
    }

    [Fact]
    public void NaCidade_NaoTransforma()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("lupe", level: 36);
        var relic = h.Temp.Db.CreateInventoryItem(h.CharacterId(a), "reliquia_presa_lunar", "comum", 1, "t:presa")!;
        h.Ticks(2);
        h.Transport.ClientSends(a, new UseItemIntent(relic));
        h.Ticks(2);
        Assert.Null(h.Me(a).Form);
    }

    [Fact]
    public void MissaoDoLobisomem_SoAparecaDepoisDoLivro()
    {
        var w = MapHarness.RealWorld();
        Assert.Equal("livro:lobisomem", w.Quests["q_forma_lobo_1"].RequiresFlag);
    }
}

public class FormasClasseTests
{
    [Fact]
    public void Lobisomem_SoDoGuerreiro()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("mira", "mago", 0, -60, 40, map: "costa_do_cal");
        var relic = h.Temp.Db.CreateInventoryItem(h.CharacterId(a), "reliquia_presa_lunar", "comum", 1, "t:presa")!;
        h.Ticks(2);
        h.Transport.ClientSends(a, new CinzasDaMare.Shared.Net.UseItemIntent(relic));
        h.Ticks(2);
        Assert.Null(h.Me(a).Form);
        Assert.Equal("guerreiro", MapHarness.RealWorld().Quests["q_forma_lobo_1"].Class);
        Assert.Equal(35, MapHarness.RealWorld().Quests["q_forma_lobo_1"].MinLevel);
    }
}

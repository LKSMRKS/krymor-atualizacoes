using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>10/11: modo deus (F1) só com o servidor aberto com --deus; relógio do mundo.</summary>
public class ModoDeusTests
{
    [Fact]
    public void SemDeus_PedidoNaoFazNada()
    {
        using var h = new MapHarness(MapHarness.TestWorld());
        var id = h.Join("ana", x: 0, y: 10, level: 3);
        h.Transport.ClientSends(id, new GodIntent("coins", Amount: 5000));
        h.Ticks(2);
        Assert.False(h.Last<GodCatalogMessage>(id).Enabled);
        Assert.DoesNotContain(h.Transport.Received<CoinsMessage>(id), c => c.Gained == 5000);
    }

    [Fact]
    public void Deus_Nivel_Moedas_Item_Monstro_EHora()
    {
        using var h = new MapHarness(MapHarness.TestWorld());
        h.Server.GodAccounts = new HashSet<string>();
        var id = h.Join("ana", x: 0, y: 10, level: 3);
        h.Ticks(2);

        h.Transport.ClientSends(id, new GodIntent("open"));
        h.Ticks(1);
        var cat = h.Last<GodCatalogMessage>(id);
        Assert.True(cat.Enabled);
        Assert.NotEmpty(cat.Items);
        Assert.NotEmpty(cat.Monsters);

        h.Transport.ClientSends(id, new GodIntent("level", Amount: 12));
        h.Ticks(2);
        Assert.Equal(12, h.Last<ProgressMessage>(id).Level);

        h.Transport.ClientSends(id, new GodIntent("coins", Amount: 5000));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<CoinsMessage>(id), c => c.Gained == 5000);

        var item = cat.Items.First(i => i.Kind == "weapon").Id;
        h.Transport.ClientSends(id, new GodIntent("item", item, 1, 1, "raro"));
        h.Ticks(2);
        Assert.Contains(h.Last<InventoryMessage>(id).Items, i => i.DefinitionId == item);

        var mon = cat.Monsters.First().Id;
        var before = h.LastSnapshot(id).Monsters.Count;
        h.Transport.ClientSends(id, new GodIntent("spawn", mon, 5, 3));
        h.Ticks(4);
        Assert.Equal(before + 3, h.LastSnapshot(id).Monsters.Count);
        h.Transport.ClientSends(id, new GodIntent("clear"));
        h.Ticks(12);
        Assert.Equal(before, h.LastSnapshot(id).Monsters.Count);

        h.Transport.ClientSends(id, new GodIntent("time", Amount: 2130));   // 21:18
        h.Ticks(1);
        Assert.InRange(h.Last<WorldTimeMessage>(id).Hour, 21.2, 21.4);
        Assert.InRange(h.Server.WorldHour, 21.2, 21.4);
    }

    [Fact]
    public void Deus_Invulneravel_NaoMorre()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("m", "monster_r1_caranguejo_de_cal", 0, 7, level: 30, group: "solo") });
        using var h = new MapHarness(w);
        h.Server.GodAccounts = new HashSet<string>();
        var id = h.Join("ana", x: 0, y: 10, level: 1);
        h.Transport.ClientSends(id, new GodIntent("god"));
        h.Ticks(400);
        Assert.True(h.Me(id).Health > 0);
    }
}

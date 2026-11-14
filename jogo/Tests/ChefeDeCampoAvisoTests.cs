using System.Linq;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>11/11: chefe de campo com espera longa avisa o servidor inteiro 5 min e 1 min antes de voltar, e quando surge.</summary>
public class ChefeDeCampoAvisoTests
{
    [Fact]
    public void ChefeDeCampo_AvisaCincoEUmMinutoAntes_EQuandoAparece()
    {
        var boss = MapHarness.Spawn("chefe", "monster_r1_capataz_da_pedreira", 0, 0, level: 6) with { RespawnSeconds = 330 };
        using var h = new MapHarness(MapHarness.TestWorld(new[] { boss }));
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var id = h.Join("caçador", x: 0, y: 4);
        h.Transport.ClientSends(id, new GodIntent("kill"));
        h.Ticks(2);
        id = h.Relocate(id, h.World.Map.Id, 0, 20);   // sai de perto (não trava o renascer)

        bool Has(string txt) => h.Transport.Received<NoticeMessage>(id).Any(n => n.Text.Contains(txt));
        h.TickUntil(() => Has("volta em 5 minutos"), 1400);
        Assert.True(Has("volta em 5 minutos"));
        Assert.False(Has("volta em 1 minuto"));
        h.TickUntil(() => Has("volta em 1 minuto"), 5000);
        Assert.True(Has("volta em 1 minuto"));
        h.TickUntil(() => Has("apareceu em"), 1400);
        Assert.True(Has("apareceu em"));
    }
}

using System.Linq;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>14/11 (dono: "um HUB nos Reinos Ocultos, igual o print do mapa"): o Coração dos Reinos tem as 6 portas dos reinos
/// (cada uma pede o livro OU a marca do Merlin), pedra de viagem, saídas para o Porto e Astrália; a volta dos reinos cai nele.</summary>
public class HubReinosTests
{
    [Fact]
    public void Hub_SeisPortas_PedraDeViagem_EVoltaDosReinos()
    {
        var w = MapHarness.RealWorld();
        var hub = w.Maps["reinos_ocultos"];
        Assert.Equal("cidade", hub.Kind);
        Assert.Empty(hub.Spawns);
        Assert.Single(hub.Waypoints);
        foreach (var r in new[] { "reino_lua", "reino_transilvania", "reino_inferno", "reino_celeste", "floresta_oculta", "reino_fabula" })
        {
            Assert.Contains(hub.Portals, p => p.ToMap == r);
            Assert.Contains(w.Maps[r].Portals, p => p.ToMap == "reinos_ocultos");
        }
        Assert.Contains(hub.Portals, p => p.ToMap == "porto_das_cinzas");
        Assert.True(Marcas.Tem(new[] { "merlin:reinos" }, hub.Portals.Single(p => p.ToMap == "reino_lua").RequiresFlag));
        Assert.True(Marcas.Tem(new[] { "livro:lobisomem" }, hub.Portals.Single(p => p.ToMap == "reino_lua").RequiresFlag));
        Assert.False(Marcas.Tem(new[] { "livro:vampiro" }, hub.Portals.Single(p => p.ToMap == "reino_lua").RequiresFlag));
    }

    [Fact]
    public void PortaFechada_NaoDeixaPassar_ComOLivroDeixa()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var porta = h.World.Maps["reinos_ocultos"].Portals.Single(p => p.ToMap == "reino_lua");
        var id = h.Join("ana", level: 40, map: "reinos_ocultos", x: porta.X * 0.8, y: porta.Y * 0.8);
        h.Transport.ClientSends(id, new MoveIntent(porta.X - porta.X * 0.8, porta.Y - porta.Y * 0.8));
        h.Ticks(30);
        Assert.Equal("reinos_ocultos", h.Last<MapMessage>(id).Map.Id);
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "livro:lobisomem");
        id = h.Relocate(id, "reinos_ocultos", porta.X * 0.8, porta.Y * 0.8);
        h.Transport.ClientSends(id, new MoveIntent(porta.X - porta.X * 0.8, porta.Y - porta.Y * 0.8));
        Assert.True(h.TickUntil(() => h.Last<MapMessage>(id).Map.Id == "reino_lua", 60));
    }
}

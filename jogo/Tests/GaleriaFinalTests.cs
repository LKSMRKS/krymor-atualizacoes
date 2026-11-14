using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11: Galeria Funda — a alavanca do fim e o baú do tesouro só abrem com todos os monstros derrotados.</summary>
public class GaleriaFinalTests
{
    [Fact]
    public void Alavanca_ComMonstrosVivos_NaoAbre()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("ana", x: -97.92, y: -64, level: 12, map: "costa_do_cal");
        h.Ticks(3);
        a = h.Relocate(a, "galeria_funda", 11.25, -29.2);
        h.Ticks(2);
        h.Transport.ClientSends(a, new InteractIntent("lever:tesouro_runas"));
        h.Ticks(2);
        Assert.Equal("galeria_funda", h.Last<MapMessage>(a).Map.Id);
        Assert.Contains(h.Last<MapMessage>(a).Map.Gates, g => g.Id == "tesouro_runas");
        var r = h.Last<ActionResultMessage>(a);
        Assert.False(r.Success);
        Assert.Equal("area_not_clear", r.Reason);
    }
}

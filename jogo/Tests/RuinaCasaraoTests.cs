using System;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11 ("a segunda casa não tem física"): o casarão em ruínas da Costa tem paredes (e a porta da frente aberta).</summary>
public class RuinaCasaraoTests
{
    private static (double X, double Y) W(MapProp p, double lx, double ly)
    {
        var a = p.Yaw * Math.PI / 180.0;
        return (p.X + lx * Math.Cos(a) + ly * Math.Sin(a), p.Y - lx * Math.Sin(a) + ly * Math.Cos(a));
    }

    [Fact]
    public void Paredes_Bloqueiam_Porta_Passa()
    {
        var map = MapHarness.RealWorld().Maps["costa_do_cal"];
        var geo = new MapGeometry(map);
        var ruina = System.Linq.Enumerable.First(map.Props, p => p.Kind == "ruina_casarao");
        var (bx, by) = W(ruina, -7.72, 0);     // parede lateral
        Assert.False(geo.IsFree(bx, by, 0.4));
        var (fx, fy) = W(ruina, 4.5, 5.72);    // frente, fora da porta
        Assert.False(geo.IsFree(fx, fy, 0.4));
        var (dx, dy) = W(ruina, 0, 5.72);      // a porta
        Assert.True(geo.IsFree(dx, dy, 0.4));
    }
}

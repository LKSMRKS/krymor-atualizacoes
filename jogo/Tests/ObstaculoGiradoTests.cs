using System;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>13/11 ("os invisíveis com colisão eu não consigo girar"): caixa com "rot" bloqueia no lugar girado.</summary>
public class ObstaculoGiradoTests
{
    private static MapGeometry Geo(double rot)
    {
        var parede = new MapObstacle("invisivel", "box", 0, 0, 8, 0.6, 0, 3) { Rot = rot };
        var map = new MapLayout("t", "T", 30, 0, 20, 3, 1, Array.Empty<MapZone>(), new[] { parede }, Array.Empty<MapGate>(),
            Array.Empty<MapChest>(), Array.Empty<MapSpawn>(), Array.Empty<MapProp>());
        return new MapGeometry(map);
    }

    [Fact]
    public void CaixaGirada90_BloqueiaNoEixoNovo()
    {
        var reta = Geo(0); var girada = Geo(90);
        Assert.False(reta.IsFree(3, 0, 0.3));      // parede deitada no eixo x
        Assert.True(reta.IsFree(0, 3, 0.3));
        Assert.True(girada.IsFree(3, 0, 0.3));     // girada: agora fica no eixo y
        Assert.False(girada.IsFree(0, 3, 0.3));
        var diagonal = Geo(45);
        Assert.False(diagonal.IsFree(2 * Math.Cos(Math.PI / 4), -2 * Math.Sin(Math.PI / 4), 0.2));
    }
}

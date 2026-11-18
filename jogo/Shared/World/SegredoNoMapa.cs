using System;
using System.Collections.Generic;
using System.Linq;

namespace CinzasDaMare.Shared.World;

/// <summary>
/// 18/11 15h (dono: "a passagem secreta do Vale das Cinzas ainda está aparecendo no mapa"): o que é zona secreta só
/// aparece no mapa (grande e minimapa) depois de achada (marca "lugar:mapa:zona"). Paredes e rochas dentro dela também.
/// </summary>
public static class SegredoNoMapa
{
    public static bool Achado(MapLayout l, MapZone z, IReadOnlySet<string>? flags)
        => flags is not null && flags.Contains($"lugar:{l.Id}:{z.Id}");

    /// <summary>Zonas secretas ainda não achadas (com 1,5 m de folga).</summary>
    public static List<(double X0, double Y0, double X1, double Y1)> Escondidos(MapLayout l, IReadOnlySet<string>? flags)
        => l.Zones.Where(z => z.Kind is "secreto" or "segredo" && !Achado(l, z, flags))
            .Select(z => (z.X - z.W / 2 - 1.5, z.Y - z.H / 2 - 1.5, z.X + z.W / 2 + 1.5, z.Y + z.H / 2 + 1.5)).ToList();

    public static bool Dentro(List<(double X0, double Y0, double X1, double Y1)> rs, double x, double y)
    {
        foreach (var r in rs)
            if (x >= r.X0 && x <= r.X1 && y >= r.Y0 && y <= r.Y1) return true;
        return false;
    }
}

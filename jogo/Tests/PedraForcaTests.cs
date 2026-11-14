using System.Linq;
using CinzasDaMare.Shared.Net;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>10/11: rocha empurrável — só sai do caminho com a Força pedida; aí a passagem abre para este personagem.</summary>
public class PedraForcaTests
{
    [Fact]
    public void Rocha_SoMoveComForca_EAbreAPassagem()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Login(h.Precreate("brutus"));
        var cid = h.CharacterId(id);
        var at = h.Relocate(id, "costa_do_cal", 104.2, 32.0);
        h.Transport.ClientSends(at, new InteractIntent("lever:pedra_enseada"));
        h.Ticks(1);
        Assert.DoesNotContain("pedra:enseada", h.Temp.Db.GetFlags(cid));

        using (var c = new SqliteConnection($"Data Source={h.Temp.DbPath}"))
        {
            c.Open();
            using var u = c.CreateCommand();
            u.CommandText = "UPDATE character_stats SET strength=30 WHERE character_id=$i";
            u.Parameters.AddWithValue("$i", cid);
            u.ExecuteNonQuery();
        }
        var strong = h.Relocate(at, "costa_do_cal", 104.2, 32.0);
        h.Transport.ClientSends(strong, new InteractIntent("lever:pedra_enseada"));
        h.Ticks(1);
        Assert.Contains("pedra:enseada", h.Temp.Db.GetFlags(cid));
    }
}

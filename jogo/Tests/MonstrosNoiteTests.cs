using System.Linq;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11 (mundo vivo): carniçais e fantasmas da Costa só aparecem de noite (19h–5h).</summary>
public class MonstrosNoiteTests
{
    [Fact]
    public void MonstrosDaNoite_AparecemSoDeNoite()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var id = h.Join("vigia", x: 0, y: 0, level: 10, map: "costa_do_cal");
        h.Transport.ClientSends(id, new GodIntent("time", Amount: 1200));
        h.Ticks(5);
        Assert.DoesNotContain(h.LastSnapshot(id).Monsters, m => m.Id.Contains("noite"));
        h.Transport.ClientSends(id, new GodIntent("time", Amount: 2200));
        h.Ticks(5);
        Assert.Contains(h.LastSnapshot(id).Monsters, m => m.Id.Contains("noite"));
    }
}

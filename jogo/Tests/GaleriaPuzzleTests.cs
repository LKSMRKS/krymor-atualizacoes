using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11 (dungeon estilo Skyrim): 3 runas abrem a sala do tesouro; placa de pressão dispara dardos que não matam.</summary>
public class GaleriaPuzzleTests
{
    private const string Galeria = "galeria_funda";

    [Fact]
    public void TresRunas_AbremOTesouro()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var id = h.Join("runista", level: 12);
        foreach (var (rid, x, y) in new[] { ("runa_veio", -30.0, 15.0), ("runa_larvas", 10.0, 13.5), ("runa_sentinela", 31.0, 15.0) })
        {
            h.Transport.ClientSends(id, new GodIntent("travel", Galeria, X: x, Y: y));
            h.Ticks(2);
            h.Transport.ClientSends(id, new InteractIntent("obj:" + rid));
            h.Ticks(2);
        }
        Assert.True(h.Transport.Received<FlagsMessage>(id).Any(f => f.Flags.Contains("galeria:runas")),
            string.Join(" | ", h.Transport.Received<ActionResultMessage>(id).Select(r => r.Reason)) + $" @ {h.Me(id).X},{h.Me(id).Y} ");
    }

    [Fact]
    public void PlacaDePressao_DaDanoMasNaoMata()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var id = h.Join("pisador", level: 12);
        h.Transport.ClientSends(id, new GodIntent("travel", Galeria, X: -16, Y: 7));
        h.Ticks(1);
        var before = h.Me(id).Health;
        h.Ticks(20);
        var after = h.Me(id).Health;
        Assert.True(after < before, $"{before} -> {after} @ {h.Me(id).X},{h.Me(id).Y}");
        Assert.True(after > 0);
    }
}

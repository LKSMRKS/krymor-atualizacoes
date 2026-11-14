using System.Linq;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11 (reset estilo MU): no nível 60, o Mestre Dorval faz renascer — nível 1, +40 pontos, aviso para todos.</summary>
public class RenascerTests
{
    [Fact]
    public void Renascer_VoltaAoNivel1ComPontosDeBonus()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var cedo = h.Join("cedo", x: -21, y: -7, level: 59, map: "porto_das_cinzas");
        h.Transport.ClientSends(cedo, new QuestIntent("q_renascer", "accept")); h.Ticks(1);
        Assert.Equal(59, h.Me(cedo).Level);   // nível 59: ainda não
        var id = h.Join("fenix", x: -21, y: -5, level: 60, map: "porto_das_cinzas");
        var ch = h.CharacterId(id);
        var before = h.Temp.Db.LoadCharacter(ch)!.StatPoints;
        h.Transport.ClientSends(id, new QuestIntent("q_renascer", "accept")); h.Ticks(2);
        Assert.Equal(1, h.Me(id).Level);
        Assert.Equal(before + 40, h.Temp.Db.LoadCharacter(ch)!.StatPoints);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("renasceu"));
    }
}

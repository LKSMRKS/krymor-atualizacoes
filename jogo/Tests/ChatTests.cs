using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11: chat global (todos) e privado ("/nome texto").</summary>
public class ChatTests
{
    [Fact]
    public void Global_ChegaParaTodos_Privado_SoParaODestinatario()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("adm", level: 5);
        var b = h.Join("alice", level: 5);
        var c = h.Join("caio", level: 5);
        h.Ticks(2);
        h.Transport.ClientSends(a, new ChatIntent("oi pessoal"));
        h.Ticks(2);
        Assert.Equal("oi pessoal", h.Last<ChatMessage>(b).Text);
        Assert.Equal("global", h.Last<ChatMessage>(c).Channel);
        h.Ticks(20);
        h.Transport.ClientSends(a, new ChatIntent("/alice segredo"));
        h.Ticks(2);
        var pb = h.Last<ChatMessage>(b);
        Assert.Equal(("privado", "segredo", "adm"), (pb.Channel, pb.Text, pb.From));
        Assert.Equal("global", h.Last<ChatMessage>(c).Channel);   // caio não recebeu o privado
        Assert.Equal("privado", h.Last<ChatMessage>(a).Channel);  // quem mandou vê a cópia
    }
}

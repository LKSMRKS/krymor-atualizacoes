using System.Net.Sockets;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>
/// Rede REAL (sockets TCP em loopback) com dois clientes .NET falando o protocolo do jogo.
/// NÃO substitui o aceite da E6, que usa o cliente Godot e dois processos de jogo de verdade.
/// </summary>
public class TcpIntegrationTests
{
    [Fact]
    public async Task DoisClientesNaRede_LoginSnapshotCastRejeitadoEColetaExclusiva()
    {
        using var h = new TcpHarness();
        var tokenA = h.Temp.Db.ProvisionAccount("alice").Token;
        var tokenB = h.Temp.Db.ProvisionAccount("bruno").Token;
        using var a = new ClientProbe();
        using var b = new ClientProbe();
        await a.ConnectAsync(h.Port);
        await b.ConnectAsync(h.Port);

        a.Client.Send(new LoginRequest(tokenA, Protocol.Version));
        b.Client.Send(new LoginRequest(tokenB, Protocol.Version));
        Assert.True((await a.WaitForAsync<LoginResult>(_ => true)).Ok);
        Assert.True((await b.WaitForAsync<LoginResult>(_ => true)).Ok);
        await a.WaitForAsync<Snapshot>(s => s.Players.Count == 2);
        await b.WaitForAsync<Snapshot>(s => s.Players.Count == 2);

        // Golpe longe do monstro: o servidor rejeita (distância medida por ele).
        a.Client.Send(new CastIntent(TestContent.Golpe, "monster_1"));
        var cast = await a.WaitForAsync<CastResultMessage>(_ => true);
        Assert.False(cast.Success);
        Assert.Equal("OutOfRange", cast.Failure);

        // Os dois disputam o mesmo item com o mesmo requestId.
        var snapshot = await a.WaitForAsync<Snapshot>(s => s.Items.Count > 0);
        var itemId = snapshot.Items[0].ItemId;
        a.Client.Send(new ClaimItemIntent("req-1", itemId));
        b.Client.Send(new ClaimItemIntent("req-1", itemId));
        var ra = await a.WaitForAsync<ClaimResultMessage>(_ => true);
        var rb = await b.WaitForAsync<ClaimResultMessage>(_ => true);

        Assert.True(ra.Success ^ rb.Success);
        Assert.Equal("already_taken", ra.Success ? rb.Reason : ra.Reason);
        await a.WaitForAsync<Snapshot>(s => s.Items.Count == 0);
        await b.WaitForAsync<Snapshot>(s => s.Items.Count == 0);

        var winnerCharacter = ra.Success
            ? (await a.WaitForAsync<LoginResult>(_ => true)).CharacterId!
            : (await b.WaitForAsync<LoginResult>(_ => true)).CharacterId!;
        Assert.Single(h.Temp.Db.GetInventory(winnerCharacter), e => e.DefinitionId == "weapon_guerreiro_sword_p1");
    }

    [Fact]
    public async Task TokenInvalido_NaRede_EhRecusado()
    {
        using var h = new TcpHarness();
        using var c = new ClientProbe();
        await c.ConnectAsync(h.Port);
        c.Client.Send(new LoginRequest("token-falso", Protocol.Version));

        var result = await c.WaitForAsync<LoginResult>(_ => true);
        Assert.False(result.Ok);
        Assert.Equal("invalid_token", result.Error);
    }

    [Fact]
    public async Task QuadroGigante_FazOServidorFecharAConexao()
    {
        using var h = new TcpHarness();
        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", h.Port);
        var stream = tcp.GetStream();
        await stream.WriteAsync(new byte[] { 0x7f, 0xff, 0xff, 0xff }); // anuncia ~2 GB

        using var timeout = new CancellationTokenSource(5000);
        int read;
        try { read = await stream.ReadAsync(new byte[16], timeout.Token); }
        catch (IOException) { read = 0; }

        Assert.Equal(0, read);
    }
}

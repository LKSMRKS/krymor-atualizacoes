using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

public sealed class TcpServerTransport : IServerTransport, IDisposable
{
    private sealed class Connection
    {
        public Connection(int id, TcpClient tcp)
        {
            Id = id;
            Tcp = tcp;
            Stream = tcp.GetStream();
        }

        public int Id { get; }
        public TcpClient Tcp { get; }
        public NetworkStream Stream { get; }
        public Channel<byte[]> Outbox { get; } = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(512) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        public volatile bool Draining;
        public int Closed;
    }

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<int, Connection> _connections = new();
    private int _nextId;

    public event Action<int>? ClientConnected;
    public event Action<int>? ClientDisconnected;
    public event Action<int, byte[]>? MessageReceived;

    /// <summary>Porta efetiva (útil quando se pede a porta 0).</summary>
    public int Port { get; private set; }

    public TcpServerTransport(IPAddress address, int port)
    {
        _listener = new TcpListener(address, port);
        Port = port;
    }

    public void Start()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptLoopAsync);
    }

    public void Send(int clientId, byte[] payload)
    {
        if (!_connections.TryGetValue(clientId, out var c) || c.Draining) return;
        if (!c.Outbox.Writer.TryWrite(payload))
        {
            GameLog.Warn("servidor.rede", $"conexão #{c.Id}: fila de envio cheia (cliente lento demais), desconectando");
            Close(c); // cliente lento demais: derruba
        }
    }

    public void Disconnect(int clientId)
    {
        if (!_connections.TryGetValue(clientId, out var c)) return;
        c.Draining = true;
        c.Outbox.Writer.TryComplete(); // o laço de escrita esvazia a fila e fecha
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(_cts.Token);
                tcp.NoDelay = true;
                var connection = new Connection(Interlocked.Increment(ref _nextId), tcp);
                _connections[connection.Id] = connection;
                ClientConnected?.Invoke(connection.Id);
                _ = Task.Run(() => RunConnectionAsync(connection));
            }
        }
        catch (Exception e) when (IsNetworkError(e)) { }
        catch (Exception e) { GameLog.Error("servidor.rede", "laço de conexões parou", e); }
    }

    private async Task RunConnectionAsync(Connection c)
    {
        var ct = _cts.Token;
        _ = Task.Run(() => WriteLoopAsync(c, ct));
        try
        {
            while (true)
            {
                var frame = await Framing.ReadAsync(c.Stream, ct);
                if (frame is null) break;
                MessageReceived?.Invoke(c.Id, frame);
            }
        }
        catch (Exception e) when (IsNetworkError(e)) { LogNetworkEnd(c, "leitura", e); }
        catch (Exception e) { GameLog.Error("servidor.rede", $"conexão #{c.Id}: erro inesperado na leitura", e); }
        Close(c);
    }

    private async Task WriteLoopAsync(Connection c, CancellationToken ct)
    {
        try
        {
            await foreach (var payload in c.Outbox.Reader.ReadAllAsync(ct))
                await Framing.WriteAsync(c.Stream, payload, ct);
        }
        catch (Exception e) when (IsNetworkError(e)) { LogNetworkEnd(c, "envio", e); }
        catch (Exception e) { GameLog.Error("servidor.rede", $"conexão #{c.Id}: erro inesperado no envio", e); }
        Close(c);
    }

    /// <summary>Queda normal de rede vai como DEBUG; quadro inválido/grande demais é AVISO (indica bug ou abuso).</summary>
    private static void LogNetworkEnd(Connection c, string where, Exception e)
    {
        if (e is OperationCanceledException or ObjectDisposedException) return;
        GameLog.Write(e is InvalidDataException ? LogLevel.Aviso : LogLevel.Debug, "servidor.rede",
            $"conexão #{c.Id} encerrada no {where}: {e.GetType().Name}: {e.Message}");
    }

    private void Close(Connection c)
    {
        if (Interlocked.Exchange(ref c.Closed, 1) == 1) return;
        _connections.TryRemove(c.Id, out _);
        c.Outbox.Writer.TryComplete();
        try { c.Tcp.Close(); } catch { /* já fechado */ }
        ClientDisconnected?.Invoke(c.Id);
    }

    private static bool IsNetworkError(Exception e)
        => e is IOException or SocketException or ObjectDisposedException
            or OperationCanceledException or InvalidDataException;

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* já parado */ }
        foreach (var c in _connections.Values) Close(c);
    }
}

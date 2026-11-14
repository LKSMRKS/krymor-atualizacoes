using System.Net.Sockets;
using System.Threading.Channels;

namespace CinzasDaMare.Shared.Net;

/// <summary>
/// Cliente TCP puro .NET (sem Godot): será reutilizado pelo cliente Godot na E5.
/// Os eventos disparam em threads de fundo; quem consome (UI/Godot) deve repassar para a thread principal.
/// </summary>
public sealed class TcpGameClient : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<byte[]> _outbox = Channel.CreateUnbounded<byte[]>();
    private TcpClient? _tcp;
    private int _disconnected;

    public event Action<NetMessage>? MessageReceived;
    public event Action? Disconnected;
    /// <summary>Por que a conexão caiu (para o log): "servidor fechou", ou o erro de rede.</summary>
    public string DisconnectReason { get; private set; } = "";

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        _tcp = new TcpClient { NoDelay = true };
        await _tcp.ConnectAsync(host, port, ct);
        var stream = _tcp.GetStream();
        _ = Task.Run(() => ReadLoopAsync(stream));
        _ = Task.Run(() => WriteLoopAsync(stream));
    }

    public void Send(NetMessage message) => _outbox.Writer.TryWrite(NetCodec.Encode(message));

    private async Task ReadLoopAsync(NetworkStream stream)
    {
        try
        {
            while (true)
            {
                var frame = await Framing.ReadAsync(stream, _cts.Token);
                if (frame is null) { DisconnectReason = "o servidor fechou a conexão"; break; }
                var message = NetCodec.Decode(frame);
                if (message is not null) MessageReceived?.Invoke(message);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException
                                      or OperationCanceledException or InvalidDataException)
        {
            if (DisconnectReason.Length == 0) DisconnectReason = $"{e.GetType().Name}: {e.Message}";
        }
        RaiseDisconnected();
    }

    private async Task WriteLoopAsync(NetworkStream stream)
    {
        try
        {
            await foreach (var payload in _outbox.Reader.ReadAllAsync(_cts.Token))
                await Framing.WriteAsync(stream, payload, _cts.Token);
        }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException
                                      or OperationCanceledException or InvalidDataException) { }
    }

    private void RaiseDisconnected()
    {
        if (Interlocked.Exchange(ref _disconnected, 1) == 0) Disconnected?.Invoke();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _outbox.Writer.TryComplete();
        try { _tcp?.Close(); } catch { /* já fechado */ }
    }
}

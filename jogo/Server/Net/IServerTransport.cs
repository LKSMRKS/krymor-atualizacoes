namespace CinzasDaMare.Server.Net;

/// <summary>
/// Abstração de transporte. Os eventos podem disparar em qualquer thread; o GameServer só os enfileira.
/// Trocar TCP por ENet/outro transporte não exige mexer nas regras de jogo.
/// </summary>
public interface IServerTransport
{
    event Action<int>? ClientConnected;
    event Action<int>? ClientDisconnected;
    event Action<int, byte[]>? MessageReceived;

    void Send(int clientId, byte[] payload);

    /// <summary>Encerra a conexão depois de enviar o que já está na fila.</summary>
    void Disconnect(int clientId);
}

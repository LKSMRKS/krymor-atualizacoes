using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>Transporte em memória: os "clientes" são chamadas diretas, sem sockets.</summary>
internal sealed class InMemoryTransport : IServerTransport
{
    private readonly Dictionary<int, List<NetMessage>> _received = new();
    private readonly HashSet<int> _disconnectedByServer = new();
    private int _next;

    public event Action<int>? ClientConnected;
    public event Action<int>? ClientDisconnected;
    public event Action<int, byte[]>? MessageReceived;

    public int Connect()
    {
        var id = ++_next;
        _received[id] = new List<NetMessage>();
        ClientConnected?.Invoke(id);
        return id;
    }

    public void ClientSends(int id, NetMessage message) => MessageReceived?.Invoke(id, NetCodec.Encode(message));
    public void ClientSendsRaw(int id, byte[] bytes) => MessageReceived?.Invoke(id, bytes);
    public void ClientDisconnects(int id) => ClientDisconnected?.Invoke(id);

    public void Send(int clientId, byte[] payload)
    {
        var message = NetCodec.Decode(payload);
        if (message is not null && _received.TryGetValue(clientId, out var list)) list.Add(message);
    }

    public void Disconnect(int clientId) => _disconnectedByServer.Add(clientId);

    public IReadOnlyList<T> Received<T>(int id) where T : NetMessage => _received[id].OfType<T>().ToList();
    public bool WasDisconnected(int id) => _disconnectedByServer.Contains(id);
}

/// <summary>Servidor + banco temporário + transporte em memória, avançando o relógio por ticks.</summary>
internal sealed class ServerHarness : IDisposable
{
    private readonly Dictionary<int, string> _characterIds = new();

    public TempDb Temp { get; }
    public ContentBundle Content { get; }
    public BalanceConfig Balance { get; }
    public InMemoryTransport Transport { get; } = new();
    public GameServer Server { get; }

    public ServerHarness(Func<BalanceConfig, BalanceConfig>? patchBalance = null,
        Func<ContentBundle, ContentBundle>? patchContent = null)
    {
        Content = TestContent.Load();
        if (patchContent is not null) Content = patchContent(Content);
        Balance = ContentLoader.LoadBalance(Path.Combine(AppContext.BaseDirectory, "Content"));
        if (patchBalance is not null) Balance = patchBalance(Balance);
        Temp = new TempDb(Balance.InventoryCapacity);
        Server = new GameServer(Content, Temp.Db, Balance, Transport, () => 0.99); // 0,99: sem críticos
    }

    public void Ticks(int n)
    {
        for (var i = 0; i < n; i++) Server.Tick();
    }

    public int Login(string name, string? token = null)
    {
        token ??= Temp.Db.ProvisionAccount(name).Token;
        var id = Transport.Connect();
        Transport.ClientSends(id, new LoginRequest(token, Protocol.Version));
        Ticks(1);
        var result = Transport.Received<LoginResult>(id).LastOrDefault();
        if (result is { Ok: true, CharacterId: not null }) _characterIds[id] = result.CharacterId;
        return id;
    }

    public string CharacterId(int id) => _characterIds[id];

    /// <summary>12/11: a Guarda de Escudo pede escudo na mão esquerda — dá e veste um Escudo de Tábuas.</summary>
    public void EquipShield(int id)
    {
        var item = Temp.Db.CreateInventoryItem(CharacterId(id), "shield_guerreiro_tabuas", "comum", 1, "t:" + System.Guid.NewGuid())!;
        Transport.ClientSends(id, new EquipIntent(item));
        Ticks(2);
    }

    /// <summary>Cria conta + personagem direto no banco (antes do login), com vida/XP opcionais.</summary>
    public (string Token, string CharacterId) Precreate(string name, double? health = null, long? xp = null)
    {
        var (accountId, token) = Temp.Db.ProvisionAccount(name);
        var characterId = Temp.Db.CreateCharacter(accountId, name, "guerreiro", Balance.StartingLevel,
            Balance.StartingStrength, Balance.StartingVigor, GameServer.MapId, Balance.SpawnX, Balance.SpawnY);
        if (health is not null || xp is not null)
            Temp.Db.SaveCharacterState(characterId, Balance.StartingLevel, xp ?? 0, GameServer.MapId,
                Balance.SpawnX, Balance.SpawnY,
                health ?? CombatFormulas.MaxHealth(Balance.StartingLevel, Balance.StartingVigor));
        return (token, characterId);
    }

    public Snapshot LastSnapshot(int id) => Transport.Received<Snapshot>(id).Last();
    public PlayerView Me(int id) => LastSnapshot(id).Players.First(p => p.CharacterId == _characterIds[id]);
    public MonsterView Monster(int id) => LastSnapshot(id).Monsters.First();

    public void Move(int id, double dx, double dy, int ticks)
    {
        Transport.ClientSends(id, new MoveIntent(dx, dy));
        Ticks(ticks);
        Transport.ClientSends(id, new MoveIntent(0, 0));
        Ticks(1);
    }

    public void WalkToMonster(int id, double stopDistance = 2.0)
    {
        for (var i = 0; i < 400; i++)
        {
            var me = Me(id);
            var mon = Monster(id);
            var dx = mon.X - me.X;
            var dy = mon.Y - me.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= stopDistance)
            {
                Transport.ClientSends(id, new MoveIntent(0, 0));
                Ticks(1);
                return;
            }
            Transport.ClientSends(id, new MoveIntent(dx / d, dy / d));
            Ticks(2);
        }
        throw new InvalidOperationException("O jogador não chegou ao monstro.");
    }

    public void Dispose() => Temp.Dispose();
}

/// <summary>Servidor real com TCP em loopback (porta efetiva escolhida pelo SO) e laço de ticks em segundo plano.</summary>
internal sealed class TcpHarness : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public TempDb Temp { get; }
    public TcpServerTransport Transport { get; }
    public GameServer Server { get; }
    public int Port { get; }

    public TcpHarness()
    {
        var content = TestContent.Load();
        var balance = ContentLoader.LoadBalance(Path.Combine(AppContext.BaseDirectory, "Content"));
        Temp = new TempDb(balance.InventoryCapacity);
        Transport = new TcpServerTransport(IPAddress.Loopback, 0);
        Server = new GameServer(content, Temp.Db, balance, Transport, () => 0.99);
        Transport.Start();
        Port = Transport.Port;
        _loop = Task.Run(() =>
        {
            while (!_cts.IsCancellationRequested)
            {
                Server.Tick();
                Thread.Sleep(10);
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        _loop.Wait();
        Transport.Dispose();
        Temp.Dispose();
    }
}

internal sealed class ClientProbe : IDisposable
{
    private readonly ConcurrentQueue<NetMessage> _messages = new();

    public TcpGameClient Client { get; } = new();

    public async Task ConnectAsync(int port)
    {
        Client.MessageReceived += m => _messages.Enqueue(m);
        await Client.ConnectAsync("127.0.0.1", port);
    }

    public async Task<T> WaitForAsync<T>(Func<T, bool> predicate, int timeoutMs = 10000) where T : NetMessage
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            foreach (var m in _messages)
                if (m is T typed && predicate(typed)) return typed;
            await Task.Delay(10);
        }
        throw new TimeoutException($"Mensagem {typeof(T).Name} não chegou em {timeoutMs} ms.");
    }

    public void Dispose() => Client.Dispose();
}

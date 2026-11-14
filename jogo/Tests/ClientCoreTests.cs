using System.Collections.Concurrent;
using System.Diagnostics;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>
/// Lógica do cliente que não depende do Godot (modelo de estado, entrada, mira, HUD).
/// O que depende do Godot (cenas, câmera, teclado real) NÃO é coberto aqui: precisa ser validado a olho.
/// </summary>
public class ClientCoreTests
{
    private static Snapshot SnapshotWith(string me, double x, double y, params MonsterView[] monsters)
        => new(1,
            new[] { new PlayerView(me, "alice", x, y, 340, 340, 100, true, false, 5, 1, 0) },
            monsters,
            new[] { new WorldItemView("item-1", "weapon_guerreiro_sword_p1", 2, 0) });

    private static MonsterView Monster(string id, double x, double y, bool alive = true)
        => new(id, "Caranguejo de Cal", x, y, 100, 198.45, alive);

    // ---------------- entrada ----------------

    [Theory]
    [InlineData(false, false, false, false, 0.0, 0.0)]
    [InlineData(true, false, false, false, 0.0, -1.0)]   // cima = y diminui
    [InlineData(false, true, false, false, 0.0, 1.0)]
    [InlineData(false, false, true, false, -1.0, 0.0)]
    [InlineData(false, false, false, true, 1.0, 0.0)]
    [InlineData(true, true, false, false, 0.0, 0.0)]     // teclas opostas se anulam
    public void Teclas_ViramDirecao(bool up, bool down, bool left, bool right, double ex, double ey)
    {
        var (x, y) = MoveInput.Direction(up, down, left, right);
        Assert.Equal(ex, x, 9);
        Assert.Equal(ey, y, 9);
    }

    [Fact]
    public void Diagonal_ENormalizada()
    {
        var (x, y) = MoveInput.Direction(true, false, false, true);
        Assert.Equal(1.0, Math.Sqrt(x * x + y * y), 9);
        Assert.True(x > 0 && y < 0);
    }

    [Fact]
    public void MoveSender_SoEnviaQuandoADirecaoMuda()
    {
        var sender = new MoveSender();
        Assert.False(sender.ShouldSend(0, 0));   // parado desde o início: nada a enviar
        Assert.True(sender.ShouldSend(1, 0));
        Assert.False(sender.ShouldSend(1, 0));
        Assert.True(sender.ShouldSend(0, 0));    // soltou as teclas
        sender.Reset();
        Assert.True(sender.ShouldSend(1, 0));
    }

    // ---------------- mira ----------------

    [Fact]
    public void Raio_QueDesce_AcertaOChao()
    {
        // câmera em (0,10,0) olhando 45 graus para -z: acerta o chão em z = -10
        var hit = AimMath.GroundHit(0, 10, 0, 0, -Math.Sqrt(0.5), -Math.Sqrt(0.5));
        Assert.NotNull(hit);
        Assert.Equal(0.0, hit!.Value.X, 9);
        Assert.Equal(-10.0, hit.Value.Z, 9);
    }

    [Fact]
    public void Raio_HorizontalOuParaCima_NaoAcertaOChao()
    {
        Assert.Null(AimMath.GroundHit(0, 10, 0, 0, 0, -1));
        Assert.Null(AimMath.GroundHit(0, 10, 0, 0, 1, 0));
    }

    [Fact]
    public void Orientacao_ApontaDoJogadorParaOMouse_EIgnoraDistanciaMinima()
    {
        var f = AimMath.FacingTo(1, 1, 1, 6);
        Assert.NotNull(f);
        Assert.Equal(0.0, f!.Value.X, 9);
        Assert.Equal(1.0, f.Value.Y, 9);
        Assert.Null(AimMath.FacingTo(1, 1, 1.01, 1.01));
    }

    [Fact]
    public void FaceThrottle_LimitaPorTempoEPorMudancaDeAngulo()
    {
        var t = new FaceThrottle();
        Assert.True(t.ShouldSend(1, 0, 0));
        Assert.False(t.ShouldSend(0, 1, 50));        // mudou muito, mas cedo demais
        Assert.False(t.ShouldSend(1, 0.01, 200));    // tempo ok, mudança desprezível
        Assert.True(t.ShouldSend(0, 1, 300));        // tempo ok e 90 graus
        Assert.False(t.ShouldSend(0, 1, 5000));      // igual à última enviada
    }

    // ---------------- modelo ----------------

    [Fact]
    public void Modelo_Login_Snapshot_E_Consultas()
    {
        var m = new ClientModel();
        m.Apply(new LoginResult(true, null, "c1", "alice", 5));
        m.Apply(SnapshotWith("c1", 0, 0, Monster("m-longe", 10, 0), Monster("m-perto", 3, 0), Monster("m-morto", 1, 0, alive: false)));

        Assert.True(m.LoggedIn);
        Assert.Equal("alice", m.Me!.Name);
        Assert.Equal("m-perto", m.NearestAliveMonster()!.Id);
        Assert.Equal("item-1", m.NearestItem()!.ItemId);
    }

    [Fact]
    public void Modelo_LoginRecusado_GuardaOMotivo()
    {
        var m = new ClientModel();
        m.Apply(new LoginResult(false, "invalid_token", null, null, 0));
        Assert.False(m.LoggedIn);
        Assert.Equal("invalid_token", m.LoginError);
    }

    [Fact]
    public void Modelo_InventarioEquipamentoEProgresso_ViramTexto()
    {
        var m = new ClientModel();
        m.Apply(new LoginResult(true, null, "c1", "alice", 5));
        m.Apply(SnapshotWith("c1", 0, 0, Monster("m1", 3, 0)));
        m.Apply(new InventoryMessage(new[]
        {
            new InventoryItemView("i1", "consumable_c01_tonico_fraco", "comum", 0, 3),
            new InventoryItemView("i2", "weapon_guerreiro_sword_p1", "comum", 1, 1),
        }));
        m.Apply(new ProgressMessage(5, 100, 30000, 100));

        var text = HudText.Build(m);

        Assert.Contains("alice  Nv 5   XP 100/30000", text);
        Assert.Contains("Tônico Fraco x3", text);
        Assert.Contains("Espada de Chapa", text);
        Assert.Contains("Arma: mãos nuas", text);
        Assert.Contains("Alvo: Caranguejo de Cal", text);
        Assert.Equal("i1", m.FirstInventoryItemWithPrefix("consumable_")!.ItemId);
        Assert.Equal("i2", m.FirstInventoryItemWithPrefix("weapon_")!.ItemId);
    }

    [Fact]
    public void Modelo_AtaqueAutomatico_LigaDesligaEMorreComOMonstro()
    {
        var m = new ClientModel();
        m.Apply(new LoginResult(true, null, "c1", "alice", 5));
        m.Apply(new ActionResultMessage("auto_attack", true, "started"));
        Assert.True(m.AutoAttackOn);
        m.Apply(new ActionResultMessage("auto_attack", true, "stopped"));
        Assert.False(m.AutoAttackOn);

        m.Apply(new ActionResultMessage("auto_attack", true, "started"));
        m.Apply(new CombatEvent("c1", "m1", "basic_attack", 15, false, true)); // matou o monstro
        Assert.False(m.AutoAttackOn);
        Assert.Contains("Monstro derrotado!", m.Log);
    }

    [Fact]
    public void Modelo_Falhas_ViramMensagensLegiveis_ERegistroTemLimite()
    {
        var m = new ClientModel();
        m.Apply(new LoginResult(true, null, "c1", "alice", 5));
        m.Apply(new CastResultMessage(ClientModel.SkillGolpe, false, "OutOfRange", 0, false, "m1", false));
        Assert.Contains(m.Log, l => l.Contains("Golpe de Cunha falhou: alvo longe demais"));

        for (var i = 0; i < 30; i++) m.Apply(new ErrorMessage("x" + i));
        Assert.Equal(8, m.Log.Count);
    }

    [Fact]
    public void Modelo_SubirDeNivel_VaiParaORegistro_EResetLimpaTudo()
    {
        var m = new ClientModel();
        m.Apply(new LoginResult(true, null, "c1", "alice", 5));
        m.Apply(new ProgressMessage(5, 29950, 30000, 0));
        m.Apply(new ProgressMessage(6, 50, 36000, 100));
        Assert.Contains("Subiu para o nível 6!", m.Log);

        m.Reset();
        Assert.False(m.LoggedIn);
        Assert.Null(m.Snapshot);
        Assert.Empty(m.Log);
    }
}

/// <summary>Cliente de verdade (TcpGameClient + ClientModel) conversando com o servidor por sockets reais.</summary>
public class ClientEndToEndTests
{
    private static async Task PumpUntilAsync(ConcurrentQueue<NetMessage> queue, ClientModel model,
        Func<bool> condition, int timeoutMs = 10000)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            while (queue.TryDequeue(out var message)) model.Apply(message);
            if (condition()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("Condição não atingida a tempo; log: " + string.Join(" | ", model.Log));
    }

    [Fact]
    public async Task ClienteReal_LogaRecebeMundoEEnviaOrientacao_PelaRede()
    {
        using var h = new TcpHarness();
        var token = h.Temp.Db.ProvisionAccount("alice").Token;
        var queue = new ConcurrentQueue<NetMessage>();
        var model = new ClientModel();
        using var client = new TcpGameClient();
        client.MessageReceived += m => queue.Enqueue(m);
        await client.ConnectAsync("127.0.0.1", h.Port);
        client.Send(new LoginRequest(token, Protocol.Version));

        await PumpUntilAsync(queue, model, () => model.LoggedIn && model.Me is not null);

        Assert.Equal("alice", model.Me!.Name);
        Assert.NotNull(model.Progress);
        Assert.Contains(model.Inventory, i => i.DefinitionId == "consumable_c01_tonico_fraco" && i.Quantity == 3);
        Assert.NotNull(model.NearestAliveMonster());
        Assert.NotNull(model.NearestItem());

        client.Send(new FaceIntent(0, 4));
        await PumpUntilAsync(queue, model, () => model.Me is { FacingY: > 0.99 });
        Assert.Equal(0.0, model.Me!.FacingX, 6);

        client.Send(new MoveIntent(0, -1));
        var startY = model.Me!.Y;
        await PumpUntilAsync(queue, model, () => model.Me is not null && model.Me.Y < startY - 0.5);
        client.Send(new MoveIntent(0, 0));
    }

    [Fact]
    public async Task ClienteReal_ComTokenInvalido_RecebeMotivoDaRecusa()
    {
        using var h = new TcpHarness();
        var queue = new ConcurrentQueue<NetMessage>();
        var model = new ClientModel();
        using var client = new TcpGameClient();
        client.MessageReceived += m => queue.Enqueue(m);
        await client.ConnectAsync("127.0.0.1", h.Port);
        client.Send(new LoginRequest("token-falso", Protocol.Version));

        await PumpUntilAsync(queue, model, () => model.LoginError is not null);

        Assert.False(model.LoggedIn);
        Assert.Equal("invalid_token", model.LoginError);
    }
}

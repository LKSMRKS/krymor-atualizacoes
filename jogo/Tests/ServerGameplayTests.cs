using System.Text;
using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>Regras do servidor autoritativo, com transporte em memória (sem sockets, sem Godot).</summary>
public class ServerGameplayTests
{
    private static int ExpectedGolpeDamage(ServerHarness h)
    {
        var b = h.Balance;
        var power = CombatFormulas.PhysicalPower(b.UnarmedWeaponDamage, b.StartingStrength, b.StartingLevel);
        var armor = CombatFormulas.ArmorReduction(25, b.StartingLevel);
        return CombatFormulas.FinalDamage(power, 1.6, armor);
    }

    [Fact]
    public void Login_ComTokenValido_CriaGuerreiroEEnviaEstadoInicial()
    {
        using var h = new ServerHarness();
        var id = h.Login("alice");

        var result = h.Transport.Received<LoginResult>(id).Single();
        Assert.True(result.Ok);
        Assert.Equal("alice", result.Name);
        Assert.Equal(h.Balance.StartingLevel, result.Level);
        Assert.Single(h.Transport.Received<InventoryMessage>(id));
        var me = h.Me(id);
        Assert.True(me.Alive);
        Assert.Equal(h.Balance.SpawnX, me.X, 6);
        Assert.Single(h.LastSnapshot(id).Monsters);
        Assert.Single(h.LastSnapshot(id).Items);
    }

    [Fact]
    public void Login_ComTokenInvalido_EhRecusadoESemEstado()
    {
        using var h = new ServerHarness();
        var id = h.Login("intruso", "token-que-nao-existe");

        var result = h.Transport.Received<LoginResult>(id).Single();
        Assert.False(result.Ok);
        Assert.Equal("invalid_token", result.Error);
        Assert.Empty(h.Transport.Received<Snapshot>(id));
        Assert.Equal(0, h.Server.ConnectedPlayers);
    }

    [Fact]
    public void Login_ContaRevogadaOuJaConectada_EhRecusado()
    {
        using var h = new ServerHarness();
        var token = h.Temp.Db.ProvisionAccount("alice").Token;
        var first = h.Login("alice", token);
        var second = h.Login("alice", token);
        Assert.True(h.Transport.Received<LoginResult>(first).Single().Ok);
        Assert.Equal("already_logged_in", h.Transport.Received<LoginResult>(second).Single().Error);

        var revokedToken = h.Temp.Db.ProvisionAccount("bob").Token;
        Assert.True(h.Temp.Db.RevokeAccount("bob"));
        var third = h.Login("bob", revokedToken);
        Assert.Equal("invalid_token", h.Transport.Received<LoginResult>(third).Single().Error);
    }

    [Fact]
    public void VersaoDeProtocoloErrada_EhRecusada()
    {
        using var h = new ServerHarness();
        var token = h.Temp.Db.ProvisionAccount("alice").Token;
        var id = h.Transport.Connect();
        h.Transport.ClientSends(id, new LoginRequest(token, 999));
        h.Ticks(1);
        Assert.Equal("bad_version", h.Transport.Received<LoginResult>(id).Single().Error);
        Assert.True(h.Transport.WasDisconnected(id));
    }

    [Fact]
    public void IntencaoAntesDoLogin_EhRejeitada_E_AbusoDesconecta()
    {
        using var h = new ServerHarness();
        var id = h.Transport.Connect();
        for (var i = 0; i < 5; i++) h.Transport.ClientSends(id, new MoveIntent(1, 0));
        h.Ticks(1);

        Assert.Equal(5, h.Transport.Received<ErrorMessage>(id).Count(e => e.Reason == "not_logged_in"));
        Assert.True(h.Transport.WasDisconnected(id));
    }

    [Fact]
    public void BytesInvalidos_GeramErro_E_ReincidenciaDesconecta()
    {
        using var h = new ServerHarness();
        var id = h.Transport.Connect();
        for (var i = 0; i < 5; i++) h.Transport.ClientSendsRaw(id, Encoding.UTF8.GetBytes("isto nao e json"));
        h.Ticks(1);

        Assert.Equal(5, h.Transport.Received<ErrorMessage>(id).Count(e => e.Reason == "malformed"));
        Assert.True(h.Transport.WasDisconnected(id));
    }

    [Fact]
    public void ExcessoDeMensagensPorSegundo_Desconecta()
    {
        using var h = new ServerHarness();
        var id = h.Login("alice");
        for (var i = 0; i < 130; i++) h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Ticks(1);
        Assert.True(h.Transport.WasDisconnected(id));
    }

    [Fact]
    public void MensagemDeServidorEnviadaPorCliente_EhRejeitada()
    {
        using var h = new ServerHarness();
        var id = h.Login("alice");
        h.Transport.ClientSends(id, new CombatEvent("eu", "monster_1", "x", 9999, true, true));
        h.Ticks(1);

        Assert.Contains(h.Transport.Received<ErrorMessage>(id), e => e.Reason == "unexpected_message");
        Assert.True(h.Monster(id).Alive);
    }

    [Fact]
    public void Movimento_EhLimitadoPelaVelocidadeDoServidor_EPelaArena()
    {
        using var h = new ServerHarness();
        var id = h.Login("alice");
        var startX = h.Me(id).X;

        h.Transport.ClientSends(id, new MoveIntent(0, -100)); // tenta "correr" 100x mais rápido
        h.Ticks(20);                                          // 1 s de jogo
        var after1s = h.Me(id);
        Assert.InRange(Math.Abs(after1s.Y), h.Balance.MoveSpeedMps * 0.85, h.Balance.MoveSpeedMps * 1.15);
        Assert.Equal(startX, after1s.X, 6);

        h.Ticks(1000);                                        // 50 s contra a parede
        var atWall = h.Me(id);
        Assert.True(Math.Abs(atWall.Y) <= h.Balance.ArenaHalfSize + 1e-9);

        // Direção inválida: o System.Text.Json lê 1e999 como infinito, então a mensagem chega ao servidor
        // e a validação de movimento a rejeita com "bad_move" (sem alterar posição nem direção).
        h.Transport.ClientSendsRaw(id, Encoding.UTF8.GetBytes("{\"t\":\"move\",\"dirX\":1e999,\"dirY\":0}"));
        h.Transport.ClientSendsRaw(id, Encoding.UTF8.GetBytes("{\"t\":\"move\",\"dirX\":0,\"dirY\":-1e999}"));
        h.Ticks(2);
        Assert.Equal(2, h.Transport.Received<ErrorMessage>(id).Count(e => e.Reason == "bad_move"));
        var afterInvalid = h.Me(id);
        Assert.Equal(atWall.X, afterInvalid.X, 6);
        Assert.Equal(atWall.Y, afterInvalid.Y, 6);
        Assert.True(double.IsFinite(afterInvalid.X) && double.IsFinite(afterInvalid.Y));

        // Tipo errado no JSON não decodifica: "malformed".
        h.Transport.ClientSendsRaw(id, Encoding.UTF8.GetBytes("{\"t\":\"move\",\"dirX\":\"abc\",\"dirY\":0}"));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<ErrorMessage>(id), e => e.Reason == "malformed");

        // O servidor segue aceitando movimento válido, ainda limitado pela velocidade.
        h.Transport.ClientSends(id, new MoveIntent(100, 0));
        h.Ticks(20);
        var moved = h.Me(id);
        Assert.InRange(moved.X - atWall.X, h.Balance.MoveSpeedMps * 0.85, h.Balance.MoveSpeedMps * 1.15);
    }

    [Fact]
    public void Golpe_ForaDoAlcance_EhRejeitado_SemDano()
    {
        using var h = new ServerHarness();
        var id = h.Login("alice");

        h.Transport.ClientSends(id, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
        h.Ticks(1);

        var result = h.Transport.Received<CastResultMessage>(id).Single();
        Assert.False(result.Success);
        Assert.Equal("OutOfRange", result.Failure);
        Assert.Equal(h.Monster(id).MaxHealth, h.Monster(id).Health, 6);
    }

    [Fact]
    public void Golpe_DentroDoAlcance_CausaDanoCalculadoPeloServidor_EOutroJogadorVeOResultado()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var b = h.Login("bruno");
        h.WalkToMonster(a);

        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
        h.Ticks(10); // conjuração de 300 ms (6 ticks) + folga para o snapshot

        var cast = h.Transport.Received<CastResultMessage>(a).Last();
        Assert.True(cast.Success);
        Assert.Equal(ExpectedGolpeDamage(h), cast.Damage);
        var seenByB = h.Monster(b);
        Assert.Equal(seenByB.MaxHealth - cast.Damage, seenByB.Health, 6);
        Assert.Contains(h.Transport.Received<CombatEvent>(b), e => e.SkillId == TestContent.Golpe && e.Damage == cast.Damage);
    }

    [Fact]
    public void DanoEnviadoPeloCliente_EhIgnorado()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.WalkToMonster(a);

        var json = "{\"t\":\"cast\",\"skillId\":\"" + TestContent.Golpe + "\",\"targetId\":\"monster_1\",\"damage\":99999}";
        h.Transport.ClientSendsRaw(a, Encoding.UTF8.GetBytes(json));
        h.Ticks(10); // conjuração de 300 ms

        var cast = h.Transport.Received<CastResultMessage>(a).Last();
        Assert.True(cast.Success);
        Assert.Equal(ExpectedGolpeDamage(h), cast.Damage);
        Assert.True(h.Monster(a).Alive);
    }

    [Fact]
    public void Ocupado_Recarga_E_HabilidadeDesconhecida_SaoValidadosNoServidor()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.WalkToMonster(a);

        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId)); // mesmo tick: ocupado
        h.Ticks(16);                                                                         // fim da recuperação (700 ms)
        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId)); // recarga de 4 s em curso
        h.Transport.ClientSends(a, new CastIntent("skill_inventada", GameServer.MonsterId));
        h.Ticks(1);

        var results = h.Transport.Received<CastResultMessage>(a);
        Assert.Equal("Busy", results[0].Failure);
        Assert.Equal("None", results[1].Failure);
        Assert.Equal("OnCooldown", results[2].Failure);
        Assert.Equal("UnknownSkill", results[3].Failure);
    }

    [Fact]
    public void GuardaDeEscudo_AparecePara_OutroJogador_NoSnapshot()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var b = h.Login("bruno");
        h.EquipShield(a);

        h.Transport.ClientSends(a, new CastIntent(TestContent.Guarda, null));
        h.Ticks(2);

        Assert.True(h.Transport.Received<CastResultMessage>(a).Last().Success);
        var aliceSeenByB = h.LastSnapshot(b).Players.Single(p => p.Name == "alice");
        Assert.True(aliceSeenByB.Guarding);
        Assert.Equal(80.0, aliceSeenByB.Resource, 1);
    }

    [Fact]
    public void Monstro_PersegueEAtacaOJogadorProximo()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.Move(a, 1, 0, 20); // anda ~5 m em direção ao monstro (dentro do raio de aggro)
        var before = h.Me(a).Health;

        h.Ticks(160);

        Assert.True(h.Me(a).Health < before);
        Assert.Contains(h.Transport.Received<CombatEvent>(a), e => e.AttackerId == GameServer.MonsterId);
    }

    [Fact]
    public void JogadorMorto_RenasceNoSpawn_ComVidaCheia()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.Move(a, 1, 0, 20);

        var sawDead = false;
        var respawned = false;
        for (var i = 0; i < 1500 && !respawned; i++)
        {
            h.Ticks(2);
            var me = h.Me(a);
            if (!me.Alive) sawDead = true;
            else if (sawDead) respawned = true;
        }

        Assert.True(sawDead);
        Assert.True(respawned);
        Assert.True(h.Me(a).Health > 0);
    }

    [Fact]
    public void MatarOMonstro_ELogoRenascePeloTemporizador()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.WalkToMonster(a);

        var killed = false;
        for (var attempt = 0; attempt < 12 && !killed; attempt++)
        {
            h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
            h.Ticks(90); // recarga de 4 s só começa ao fim da conjuração de 300 ms
            killed = !h.Monster(a).Alive;
        }
        Assert.True(killed);

        h.Ticks(h.Balance.MonsterRespawnSeconds * 20 + 5);
        var respawned = h.Monster(a);
        Assert.True(respawned.Alive);
        Assert.Equal(respawned.MaxHealth, respawned.Health, 6);
    }

    [Fact]
    public void Coleta_ForaDoAlcance_EhRejeitada_DentroDoAlcance_Funciona()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var itemId = h.LastSnapshot(a).Items.Single().ItemId;

        h.Move(a, 0, -1, 30); // afasta ~7 m do item
        h.Transport.ClientSends(a, new ClaimItemIntent("r1", itemId));
        h.Ticks(1);
        Assert.Equal("out_of_range", h.Transport.Received<ClaimResultMessage>(a).Last().Reason);

        h.Move(a, 0, 1, 30); // volta
        h.Transport.ClientSends(a, new ClaimItemIntent("r2", itemId));
        h.Ticks(2);
        var ok = h.Transport.Received<ClaimResultMessage>(a).Last();
        Assert.True(ok.Success);
        Assert.Single(h.Transport.Received<InventoryMessage>(a).Last().Items, i => i.DefinitionId == GameServer.SeedItemDefinition);
        Assert.Empty(h.LastSnapshot(a).Items);
    }

    [Fact]
    public void Coleta_DeDoisJogadores_TemUmSoVencedor_E_IdsDeRequisicaoNaoSeMisturam()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var b = h.Login("bruno");
        var itemId = h.LastSnapshot(a).Items.Single().ItemId;

        h.Transport.ClientSends(a, new ClaimItemIntent("req-1", itemId));
        h.Transport.ClientSends(b, new ClaimItemIntent("req-1", itemId)); // mesmo requestId, outro jogador
        h.Ticks(2);

        var ra = h.Transport.Received<ClaimResultMessage>(a).Single();
        var rb = h.Transport.Received<ClaimResultMessage>(b).Single();
        Assert.True(ra.Success ^ rb.Success);
        Assert.Equal("already_taken", ra.Success ? rb.Reason : ra.Reason);
    }

    [Fact]
    public void RepetirAMesmaColeta_DevolveOMesmoResultado_SemDuplicar()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var itemId = h.LastSnapshot(a).Items.Single().ItemId;

        h.Transport.ClientSends(a, new ClaimItemIntent("req-1", itemId));
        h.Ticks(2);
        h.Transport.ClientSends(a, new ClaimItemIntent("req-1", itemId)); // reenvio (resposta "perdida")
        h.Ticks(2);

        var results = h.Transport.Received<ClaimResultMessage>(a);
        Assert.Equal(2, results.Count);
        Assert.Equal(results[0], results[1]);
        var characterId = h.Transport.Received<LoginResult>(a).Single().CharacterId!;
        Assert.Single(h.Temp.Db.GetInventory(characterId), e => e.DefinitionId == GameServer.SeedItemDefinition);
    }

    [Fact]
    public void Personagem_EItem_SobrevivemAoReinicioDoServidor()
    {
        using var h = new ServerHarness();
        var token = h.Temp.Db.ProvisionAccount("alice").Token;
        var a = h.Login("alice", token);
        var itemId = h.LastSnapshot(a).Items.Single().ItemId;
        h.Transport.ClientSends(a, new ClaimItemIntent("req-1", itemId));
        h.Ticks(2);
        h.Move(a, 0, -1, 10);
        var savedY = h.Me(a).Y;
        h.Transport.ClientDisconnects(a);
        h.Ticks(1); // desconexão salva o personagem

        // "Reinício": novo GameServer sobre o mesmo banco.
        var transport2 = new InMemoryTransport();
        var server2 = new GameServer(h.Content, h.Temp.Db, h.Balance, transport2, () => 0.99);
        var id2 = transport2.Connect();
        transport2.ClientSends(id2, new LoginRequest(token, Protocol.Version));
        server2.Tick();

        Assert.True(transport2.Received<LoginResult>(id2).Single().Ok);
        var inventory = transport2.Received<InventoryMessage>(id2).Single().Items;
        var sword = Assert.Single(inventory, i => i.DefinitionId == GameServer.SeedItemDefinition);
        Assert.Equal(itemId, sword.ItemId);
        // Reentrar não repete o pacote inicial de poções.
        Assert.Equal(h.Balance.StartingPotions,
            Assert.Single(inventory, i => i.DefinitionId == GameServer.StarterPotionDefinition).Quantity);
        var snap = transport2.Received<Snapshot>(id2).Last();
        Assert.Empty(snap.Items); // item semente já coletado: não reaparece
        Assert.Equal(savedY, snap.Players.Single().Y, 6);
    }
}

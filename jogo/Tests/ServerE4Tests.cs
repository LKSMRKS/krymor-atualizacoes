using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>Regras da E4 no servidor: ataque básico, conjuração, XP/níveis, equipar, poção e regeneração.</summary>
public class ServerE4Tests
{
    private const string Sword = GameServer.SeedItemDefinition;
    private const string Tonic = GameServer.StarterPotionDefinition;

    private static int ExpectedBasicDamage(ServerHarness h, double weaponPower)
    {
        var b = h.Balance;
        var power = CombatFormulas.PhysicalPower(weaponPower, b.StartingStrength, b.StartingLevel);
        var armor = CombatFormulas.ArmorReduction(25, b.StartingLevel); // armadura do monstro de nível 1
        return CombatFormulas.FinalDamage(power, b.BasicAttackCoefficient, armor);
    }

    private static IReadOnlyList<CombatEvent> BasicHits(ServerHarness h, int id)
        => h.Transport.Received<CombatEvent>(id)
            .Where(e => e.SkillId == "basic_attack" && e.AttackerId == h.CharacterId(id)).ToList();

    private static InventoryItemView Item(ServerHarness h, int id, string definitionId)
        => h.Transport.Received<InventoryMessage>(id).Last().Items.Single(i => i.DefinitionId == definitionId);

    private static bool HasItem(ServerHarness h, int id, string definitionId)
        => h.Transport.Received<InventoryMessage>(id).Last().Items.Any(i => i.DefinitionId == definitionId);

    private static string ClaimSword(ServerHarness h, int id)
    {
        var itemId = h.LastSnapshot(id).Items.Single().ItemId;
        h.Transport.ClientSends(id, new ClaimItemIntent("claim-" + id, itemId));
        h.Ticks(2);
        return itemId;
    }

    private static void KillMonsterWithBasicAttacks(ServerHarness h, params int[] players)
    {
        foreach (var p in players) h.WalkToMonster(p);
        foreach (var p in players) h.Transport.ClientSends(p, new AutoAttackIntent(GameServer.MonsterId));
        for (var i = 0; i < 800 && h.Monster(players[0]).Alive; i++) h.Ticks(2);
        Assert.False(h.Monster(players[0]).Alive);
        h.Ticks(2);
    }

    // ---------------- pacote inicial e progresso ----------------

    [Fact]
    public void NovoPersonagem_RecebePocoesIniciais_UmaVez_E_ProgressoInicial()
    {
        using var h = new ServerHarness();
        var token = h.Temp.Db.ProvisionAccount("alice").Token;
        var a = h.Login("alice", token);

        Assert.Equal(h.Balance.StartingPotions, Item(h, a, Tonic).Quantity);
        var progress = h.Transport.Received<ProgressMessage>(a).Single();
        Assert.Equal(h.Balance.StartingLevel, progress.Level);
        Assert.Equal(0, progress.Xp);
        Assert.Equal(Progression.XpToNext(h.Balance.StartingLevel), progress.XpToNext);

        h.Transport.ClientDisconnects(a);
        h.Ticks(1);
        var again = h.Login("alice", token);
        Assert.Equal(h.Balance.StartingPotions, Item(h, again, Tonic).Quantity);
    }

    // ---------------- ataque básico ----------------

    [Fact]
    public void AtaqueBasico_CausaDanoNoIntervaloDaArma_CalculadoPeloServidor()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.WalkToMonster(a);

        h.Transport.ClientSends(a, new AutoAttackIntent(GameServer.MonsterId));
        h.Ticks(100); // 5 s de jogo

        var hits = BasicHits(h, a);
        Assert.InRange(hits.Count, 4, 6); // uma pancada por segundo (arma de mãos nuas: 1000 ms)
        Assert.All(hits, e => Assert.Equal(ExpectedBasicDamage(h, h.Balance.UnarmedWeaponDamage), e.Damage));
        var expectedHealth = h.Monster(a).MaxHealth - hits.Sum(e => e.Damage);
        Assert.Equal(expectedHealth, h.Monster(a).Health, 6);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(a), r => r.Action == "auto_attack" && r.Reason == "started");
    }

    [Fact]
    public void AtaqueBasico_ForaDoAlcance_NaoCausaDano_E_NaoPerseguePorConta()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");

        h.Transport.ClientSends(a, new AutoAttackIntent(GameServer.MonsterId));
        h.Ticks(60);

        Assert.Empty(BasicHits(h, a));
        Assert.Equal(h.Monster(a).MaxHealth, h.Monster(a).Health, 6);
    }

    [Fact]
    public void AtaqueBasico_PodeSerDesligado_E_AlvoInvalidoERejeitado()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.WalkToMonster(a);

        h.Transport.ClientSends(a, new AutoAttackIntent("monstro_fantasma"));
        h.Transport.ClientSends(a, new AutoAttackIntent(GameServer.MonsterId));
        h.Ticks(25);
        var before = BasicHits(h, a).Count;
        Assert.True(before >= 1);

        h.Transport.ClientSends(a, new AutoAttackIntent(null));
        h.Ticks(60);

        var actions = h.Transport.Received<ActionResultMessage>(a);
        Assert.Contains(actions, r => r.Reason == "no_target");
        Assert.Contains(actions, r => r.Reason == "stopped");
        Assert.Equal(before, BasicHits(h, a).Count);
    }

    // ---------------- tempo de conjuração ----------------

    [Fact]
    public void Conjuracao_EfeitoSoNoFimDoTempoDeCast_ECustoSoNaResolucao()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.WalkToMonster(a);
        var maxHealth = h.Monster(a).MaxHealth;

        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
        h.Ticks(3); // 150 ms de 300 ms

        Assert.Contains(h.Transport.Received<CastStartedMessage>(a), m => m.SkillId == TestContent.Golpe && m.CastMs == 300);
        Assert.Empty(h.Transport.Received<CastResultMessage>(a));
        Assert.Equal(maxHealth, h.Monster(a).Health, 6);
        Assert.Equal(100.0, h.Me(a).Resource, 6); // nada gasto ainda

        h.Ticks(6);

        Assert.True(h.Transport.Received<CastResultMessage>(a).Single().Success);
        Assert.True(h.Monster(a).Health < maxHealth);
        Assert.True(h.Me(a).Resource < 100.0);
    }

    [Fact]
    public void Conjuracao_BloqueiaOutraAcaoAteOFimDaRecuperacao()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        h.WalkToMonster(a);

        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
        h.Ticks(10);                                      // ainda em recuperação (700 ms = 14 ticks)
        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
        h.Ticks(1);
        Assert.Equal("Busy", h.Transport.Received<CastResultMessage>(a).Last().Failure);

        h.Ticks(10);                                      // livre, mas a recarga de 4 s continua
        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
        h.Ticks(1);
        Assert.Equal("OnCooldown", h.Transport.Received<CastResultMessage>(a).Last().Failure);
    }

    // ---------------- XP e níveis ----------------

    [Fact]
    public void AbaterMonstro_ConcedeXpDoGdd_E_PersisteNoBanco()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");

        KillMonsterWithBasicAttacks(h, a);

        var progress = h.Transport.Received<ProgressMessage>(a).Last();
        Assert.Equal(100, progress.XpGained); // monstro de nível 1: 100 x 1, 4 níveis abaixo: integral
        Assert.Equal(100, progress.Xp);
        Assert.Equal(h.Balance.StartingLevel, progress.Level);
        Assert.Equal(100, h.Temp.Db.LoadCharacter(h.CharacterId(a))!.Xp);
    }

    [Fact]
    public void XpSuficiente_SobeDeNivel_RecalculaVidaMaxima_E_Persiste()
    {
        using var h = new ServerHarness();
        var (token, characterId) = h.Precreate("alice", xp: Progression.XpToNext(5) - 50);
        var a = h.Login("alice", token);

        KillMonsterWithBasicAttacks(h, a);

        var progress = h.Transport.Received<ProgressMessage>(a).Last();
        Assert.Equal(6, progress.Level);
        Assert.Equal(50, progress.Xp);
        Assert.Equal(CombatFormulas.MaxHealth(6, h.Balance.StartingVigor), h.Me(a).MaxHealth, 6);
        Assert.Equal(6, h.Me(a).Level);
        var saved = h.Temp.Db.LoadCharacter(characterId)!;
        Assert.Equal(6, saved.Level);
        Assert.Equal(50, saved.Xp);
    }

    [Fact]
    public void DoisJogadoresQueDaoDano_DividemOXpComBonusDeGrupo()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var b = h.Login("bruno");

        KillMonsterWithBasicAttacks(h, a, b);

        var expected = Progression.KillXp(1, h.Balance.StartingLevel, 2); // 100 x 1,35 / 2 = 68
        Assert.Equal(68, expected);
        Assert.Equal(expected, h.Transport.Received<ProgressMessage>(a).Last().XpGained);
        Assert.Equal(expected, h.Transport.Received<ProgressMessage>(b).Last().XpGained);
    }

    [Fact]
    public void QuemNaoDeuDano_NaoRecebeXp()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var spectator = h.Login("bruno");

        KillMonsterWithBasicAttacks(h, a);

        Assert.Equal(100, h.Transport.Received<ProgressMessage>(a).Last().XpGained);
        Assert.Equal(0, h.Transport.Received<ProgressMessage>(spectator).Last().XpGained);
    }

    // ---------------- regeneração fora de combate ----------------

    [Fact]
    public void ForaDeCombate_Regenera1PorCentoDaVidaPorSegundo()
    {
        using var h = new ServerHarness();
        var (token, _) = h.Precreate("alice", health: 100);
        var a = h.Login("alice", token);
        h.Ticks(2);
        var start = h.Me(a).Health;

        h.Ticks(100); // 5 s

        var max = h.Me(a).MaxHealth;
        Assert.InRange(h.Me(a).Health - start, max * 0.01 * 5 * 0.9, max * 0.01 * 5 * 1.1);
    }

    [Fact]
    public void Regeneracao_SoVoltaDezSegundosDepoisDoCombate()
    {
        using var h = new ServerHarness();
        var (token, _) = h.Precreate("alice", health: 100);
        var a = h.Login("alice", token);
        h.EquipShield(a);

        h.Transport.ClientSends(a, new CastIntent(TestContent.Guarda, null)); // entra em combate
        h.Ticks(2);
        var inCombat = h.Me(a).Health;

        h.Ticks(170); // 8,5 s depois
        Assert.Equal(inCombat, h.Me(a).Health, 6);

        h.Ticks(100); // passou dos 10 s: volta a regenerar
        Assert.True(h.Me(a).Health > inCombat + 3);
    }

    // ---------------- poção pela rede ----------------

    [Fact]
    public void Pocao_ConsomeUmaUnidade_CuraGradualmente_ERecargaCompartilhada()
    {
        using var h = new ServerHarness();
        var (token, characterId) = h.Precreate("alice", health: 100);
        h.Temp.Db.CreateInventoryItem(characterId, Tonic, "comum", 3, "teste");
        var a = h.Login("alice", token);
        var potion = Item(h, a, Tonic);

        h.Transport.ClientSends(a, new UseItemIntent(potion.ItemId));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(a), r => r.Action == "use_item" && r.Success);
        Assert.Equal(2, Item(h, a, Tonic).Quantity);

        h.Transport.ClientSends(a, new UseItemIntent(potion.ItemId)); // dentro dos 12 s
        h.Ticks(2);
        Assert.Equal("on_cooldown", h.Transport.Received<ActionResultMessage>(a).Last().Reason);
        Assert.Equal(2, Item(h, a, Tonic).Quantity);                 // não gastou

        h.Ticks(100);                                                  // cura de 4 s concluída
        var healed = 0.15 * h.Me(a).MaxHealth;
        Assert.True(h.Me(a).Health >= 100 + healed - 1);
        Assert.True(h.Me(a).Health <= h.Me(a).MaxHealth);

        h.Ticks(160);                                                  // passaram 12 s da primeira
        h.Transport.ClientSends(a, new UseItemIntent(potion.ItemId));
        h.Ticks(2);
        Assert.Equal(1, Item(h, a, Tonic).Quantity);
        Assert.Equal(1, h.Temp.Db.GetInventory(characterId).Single(i => i.DefinitionId == Tonic).Quantity);
    }

    [Fact]
    public void Pocao_UltimaUnidade_RemoveOItem_ESegundoUsoFalha()
    {
        using var h = new ServerHarness(patchBalance: b => b with { StartingPotions = 1 });
        var a = h.Login("alice");
        var potion = Item(h, a, Tonic);

        h.Transport.ClientSends(a, new UseItemIntent(potion.ItemId));
        h.Ticks(2);
        Assert.False(HasItem(h, a, Tonic));

        h.Transport.ClientSends(a, new UseItemIntent(potion.ItemId));
        h.Ticks(2);
        Assert.Equal("not_in_inventory", h.Transport.Received<ActionResultMessage>(a).Last().Reason);
    }

    [Fact]
    public void Pocao_ItemNaoConsumivel_EItemDeOutroJogador_SaoRejeitados()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var b = h.Login("bruno");
        var swordId = ClaimSword(h, a);
        var alicePotion = Item(h, a, Tonic);

        h.Transport.ClientSends(a, new UseItemIntent(swordId));
        h.Transport.ClientSends(b, new UseItemIntent(alicePotion.ItemId));
        h.Ticks(2);

        Assert.Equal("not_consumable", h.Transport.Received<ActionResultMessage>(a).Last().Reason);
        Assert.Equal("not_in_inventory", h.Transport.Received<ActionResultMessage>(b).Last().Reason);
        Assert.Equal(h.Balance.StartingPotions,
            h.Temp.Db.GetInventory(h.CharacterId(a)).Single(i => i.DefinitionId == Tonic).Quantity);
    }

    // ---------------- equipar ----------------

    [Fact]
    public void EquiparArma_AlteraODanoDoAtaqueBasico()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var swordId = ClaimSword(h, a);

        h.Transport.ClientSends(a, new EquipIntent(swordId));
        h.Ticks(2);

        Assert.Contains(h.Transport.Received<ActionResultMessage>(a), r => r.Action == "equip" && r.Success);
        var equipped = Assert.Single(h.Transport.Received<EquipmentMessage>(a).Last().Items);
        Assert.Equal(("weapon_main", swordId), (equipped.Slot, equipped.ItemId));
        Assert.False(HasItem(h, a, Sword)); // saiu do inventário

        h.WalkToMonster(a);
        h.Transport.ClientSends(a, new AutoAttackIntent(GameServer.MonsterId));
        h.Ticks(30);

        var expected = ExpectedBasicDamage(h, 12);
        Assert.NotEqual(ExpectedBasicDamage(h, h.Balance.UnarmedWeaponDamage), expected);
        Assert.All(BasicHits(h, a), e => Assert.Equal(expected, e.Damage));
        Assert.NotEmpty(BasicHits(h, a));
    }

    [Fact]
    public void Desequipar_DevolveAoInventario_EODanoVoltaAoDeMaosNuas()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var swordId = ClaimSword(h, a);
        h.Transport.ClientSends(a, new EquipIntent(swordId));
        h.Ticks(2);

        h.Transport.ClientSends(a, new UnequipIntent("weapon_main"));
        h.Ticks(2);

        Assert.Empty(h.Transport.Received<EquipmentMessage>(a).Last().Items);
        Assert.True(HasItem(h, a, Sword));

        h.WalkToMonster(a);
        h.Transport.ClientSends(a, new AutoAttackIntent(GameServer.MonsterId));
        h.Ticks(30);
        Assert.All(BasicHits(h, a),
            e => Assert.Equal(ExpectedBasicDamage(h, h.Balance.UnarmedWeaponDamage), e.Damage));
        Assert.NotEmpty(BasicHits(h, a));
    }

    [Fact]
    public void Equipar_RejeitaItemInexistente_NaoEquipavel_ItemDeOutroJogador_E_NivelBaixo()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var b = h.Login("bruno");
        var swordId = ClaimSword(h, a);
        var potionId = Item(h, a, Tonic).ItemId;

        h.Transport.ClientSends(a, new EquipIntent("nao-existe"));
        h.Transport.ClientSends(a, new EquipIntent(potionId));
        h.Transport.ClientSends(b, new EquipIntent(swordId)); // espada de outra pessoa
        h.Ticks(2);

        var ra = h.Transport.Received<ActionResultMessage>(a).Where(r => r.Action == "equip").ToList();
        Assert.Equal("not_in_inventory", ra[0].Reason);
        Assert.Equal("not_equippable", ra[1].Reason);
        Assert.Equal("not_in_inventory", h.Transport.Received<ActionResultMessage>(b).Last().Reason);
        Assert.Empty(h.Transport.Received<EquipmentMessage>(b).Last().Items); // segue sem nada equipado
    }

    [Fact]
    public void Equipar_ExigeNivelMinimoDoItem()
    {
        using var h = new ServerHarness(patchContent: c =>
        {
            var items = c.Items.ToDictionary(k => k.Key, v => v.Value);
            items[Sword] = items[Sword] with { MinLevel = 50 };
            return new CinzasDaMare.Shared.ContentBundle(c.Classes, c.Skills, items, c.Monsters, c.Consumables);
        });
        var a = h.Login("alice");
        var swordId = ClaimSword(h, a);

        h.Transport.ClientSends(a, new EquipIntent(swordId));
        h.Ticks(2);

        Assert.Equal("level_too_low", h.Transport.Received<ActionResultMessage>(a).Last().Reason);
        Assert.True(HasItem(h, a, Sword));
    }

    [Fact]
    public void Equipar_DuranteAConjuracao_EhRejeitado()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");
        var swordId = ClaimSword(h, a);
        h.WalkToMonster(a);

        h.Transport.ClientSends(a, new CastIntent(TestContent.Golpe, GameServer.MonsterId));
        h.Transport.ClientSends(a, new EquipIntent(swordId));
        h.Ticks(2);

        Assert.Equal("busy", h.Transport.Received<ActionResultMessage>(a).Last(r => r.Action == "equip").Reason);
    }

    [Fact]
    public void Equipamento_SobreviveAoReinicioDoServidor()
    {
        using var h = new ServerHarness();
        var token = h.Temp.Db.ProvisionAccount("alice").Token;
        var a = h.Login("alice", token);
        var swordId = ClaimSword(h, a);
        h.Transport.ClientSends(a, new EquipIntent(swordId));
        h.Ticks(2);
        h.Transport.ClientDisconnects(a);
        h.Ticks(1);

        var transport2 = new InMemoryTransport();
        var server2 = new GameServer(h.Content, h.Temp.Db, h.Balance, transport2, () => 0.99);
        var id2 = transport2.Connect();
        transport2.ClientSends(id2, new LoginRequest(token, Protocol.Version));
        server2.Tick();

        var equipped = Assert.Single(transport2.Received<EquipmentMessage>(id2).Single().Items);
        Assert.Equal(swordId, equipped.ItemId);
        Assert.DoesNotContain(transport2.Received<InventoryMessage>(id2).Single().Items, i => i.DefinitionId == Sword);
    }
}

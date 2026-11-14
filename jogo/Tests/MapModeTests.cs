using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Content;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Tests;

/// <summary>E8 — conteúdo do mapa, geometria e colisão.</summary>
public class MapContentTests
{
    [Fact]
    public void MapaReal_CarregaEPassaNaValidacao()
    {
        var content = TestContent.Load();
        var world = MapHarness.RealWorld();
        Assert.Empty(ContentValidator.ValidateWorld(content, world));
        // E8: o Vale continua com tudo o que tinha (agora é a 2ª região do mundo do E9)
        var vale = world.Maps["vale_das_cinzas"];
        var kinds = vale.Spawns.Select(s => content.Monsters[s.Monster]).ToList();
        Assert.Contains(kinds, d => d.Boss);
        Assert.Contains(kinds, d => d.Rare);
        // (as criaturas do bestiário, "monster_bst_*", e as do pacote de 11/11, "monster_vale_*", somam por cima — não contam como as espécies originais do Vale)
        Assert.Equal(3, kinds.Where(d => !d.Boss && !d.Rare && !d.Id.StartsWith("monster_bst_") && !d.Id.StartsWith("monster_vale_")).Select(d => d.BehaviorId).Distinct().Count());
        Assert.Contains(kinds, d => d.Id.StartsWith("monster_bst_"));
        Assert.Contains(vale.Chests, c => c.Secret);
        Assert.Single(vale.Gates);
        Assert.Contains(vale.Obstacles, o => !o.Solid); // parede ilusória do segredo
    }

    [Fact]
    public void Validador_RecusaAtaqueSemPreparacaoOuSemJanelaDeContraAtaque()
    {
        var content = TestContent.Load();
        var real = MapHarness.RealWorld();
        var bad = real.Behaviors["melee_windup_swing"] with
        {
            Attacks = new[] { real.Behaviors["melee_windup_swing"].Attacks[0] with { WindupMs = 100, RecoveryMs = 50 } },
        };
        var behaviors = real.Behaviors.ToDictionary(kv => kv.Key, kv => kv.Value);
        behaviors[bad.Id] = bad;
        var errors = ContentValidator.ValidateWorld(content, new WorldContent(real.Map, behaviors, real.LootTables));
        Assert.Contains(errors, e => e.Contains("preparação curta"));
        Assert.Contains(errors, e => e.Contains("recuperação curta"));
    }

    [Fact]
    public void Parede_Bloqueia_ParedeIlusoria_Nao_Portao_SoComAMarca()
    {
        var obstacles = new[]
        {
            new MapObstacle("muro", "box", 5, 0, 1, 10, 0, 3),
            new MapObstacle("muralha_ilusoria", "box", -5, 0, 1, 10, 0, 3) { Solid = false },
        };
        var gates = new[] { new MapGate("g", "gate:g", 0, 5, 4, 1, 0, 3) };
        var w = MapHarness.TestWorld(obstacles: obstacles, gates: gates);
        var geo = new MapGeometry(w.Map);

        var (x, _) = geo.Move(3, 0, 4, 0, 0.45);
        Assert.True(x < 4.5 - 0.44, $"atravessou a parede: x={x}");
        var (x2, _) = geo.Move(-3, 0, -4, 0, 0.45);
        Assert.Equal(-7.0, x2, 6);

        var (_, y) = geo.Move(0, 3, 0, 4, 0.45);
        Assert.True(y < 4.5 - 0.44, "portão fechado deveria bloquear");
        var (_, y2) = geo.Move(0, 3, 0, 4, 0.45, new HashSet<string> { "gate:g" });
        Assert.Equal(7.0, y2, 6);

        Assert.False(geo.HasLineOfSight(0, 0, 10, 0));
        Assert.True(geo.HasLineOfSight(0, 0, -10, 0));
    }

    [Theory]
    [InlineData(2.0, 0.0, true)]    // à frente
    [InlineData(-2.0, 0.0, false)]  // atrás
    [InlineData(0.0, 2.0, false)]   // de lado (fora de 110°)
    [InlineData(1.5, 1.2, true)]    // dentro do leque
    [InlineData(5.0, 0.0, false)]   // longe
    public void Cone_DoGolpe(double px, double py, bool expected)
        => Assert.Equal(expected, MapGeometry.InCone(0, 0, 1, 0, px, py, 2.6, 110, 0.5));
}

/// <summary>E8 — lobby: criar/escolher personagem, classe persistida, personagens antigos preservados.</summary>
public class LobbyTests
{
    private static int Lobby(MapHarness h, string token)
    {
        var id = h.Transport.Connect();
        h.Transport.ClientSends(id, new LobbyRequest(token, Protocol.Version));
        h.Ticks(1);
        return id;
    }

    [Fact]
    public void CriaMago_EntraNoMundo_ComCajadoPocoesECatalogoDoMago()
    {
        using var h = new MapHarness();
        var token = h.Temp.Db.ProvisionAccount("carla").Token;
        var id = Lobby(h, token);
        var lobby = h.Transport.Received<LobbyMessage>(id).Last();
        Assert.True(lobby.Ok);
        Assert.Empty(lobby.Characters);
        Assert.Equal(new[] { "guerreiro", "mago", "bruxa", "arqueiro" }, lobby.Classes.Select(c => c.Id).ToArray());

        h.Transport.ClientSends(id, new CreateCharacterIntent("Carla Brasa", "mago"));
        h.Ticks(1);
        var created = h.Transport.Received<LobbyMessage>(id).Last().Characters.Single();
        Assert.Equal("mago", created.ClassId);

        h.Transport.ClientSends(id, new EnterWorldIntent(created.Id));
        h.Ticks(2);
        Assert.True(h.Transport.Received<LoginResult>(id).Last().Ok);
        h.Remember(id, created.Id);
        Assert.Equal("mago", h.Me(id).ClassId);
        Assert.Null(h.Me(id).Weapon); // 10/11: começa sem arma (no soco/faísca); a arma base vem da 1ª missão ou do chão
        Assert.Equal(1, h.Me(id).Level);
        Assert.Equal("teste", h.Transport.Received<MapMessage>(id).Single().Map.Id);
        var catalog = h.Transport.Received<CatalogMessage>(id).Single();
        Assert.Equal("Mana", catalog.ResourceName);
        Assert.Equal(new[] { "basic", "basic_unarmed", "skill1", "skill2", "skill3", "tomo" },
            catalog.Skills.Select(s => s.Slot).Where(x => x != "weapon").Distinct().OrderBy(x => x).ToArray());
        Assert.Contains(catalog.Skills, s => s.Slot == "weapon");   // habilidades das armas do Mago (tecla R)
        Assert.Contains(h.Transport.Received<InventoryMessage>(id).Last().Items,
            i => i.DefinitionId == GameServer.StarterPotionDefinition && i.Quantity == 3);
    }

    [Fact]
    public void NomeInvalido_Repetido_ELimiteDePersonagens()
    {
        using var h = new MapHarness();
        var id = Lobby(h, h.Temp.Db.ProvisionAccount("dani").Token);
        h.Transport.ClientSends(id, new CreateCharacterIntent("x", "mago"));
        h.Transport.ClientSends(id, new CreateCharacterIntent("Dani", "arqueira"));
        h.Transport.ClientSends(id, new CreateCharacterIntent("Dani", "guerreiro"));
        h.Transport.ClientSends(id, new CreateCharacterIntent("DANI", "mago"));
        h.Ticks(1);
        var replies = h.Transport.Received<ActionResultMessage>(id).Where(r => r.Action == "create_character").ToList();
        Assert.Equal(new[] { "bad_name", "unknown_class", "ok", "name_taken" }, replies.Select(r => r.Reason).ToArray());

        for (var i = 0; i < GameServer.MaxCharactersPerAccount; i++) h.Transport.ClientSends(id, new CreateCharacterIntent("Extra" + i, "guerreiro"));
        h.Ticks(1);
        Assert.Equal(GameServer.MaxCharactersPerAccount, h.Transport.Received<LobbyMessage>(id).Last().Characters.Count);
        Assert.Equal("too_many", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
    }

    [Fact]
    public void PersonagemAntigoDaArena_AparecemNoLobby_EntraNoAcampamento_ComNivelXpEItens()
    {
        using var h = new MapHarness();
        var (accountId, token) = h.Temp.Db.ProvisionAccount("alice");
        var oldId = h.Temp.Db.CreateCharacter(accountId, "alice", "guerreiro", 7, 10, 10, GameServer.MapId, 12, -3);
        h.Temp.Db.SaveCharacterState(oldId, 7, 1234, GameServer.MapId, 12, -3, 200);
        var sword = h.Temp.Db.CreateWorldItem(TestContent.Sword, "comum", "seed_test");
        Assert.True(h.Temp.Db.TryClaimWorldItem("r", sword, oldId).Success);
        Assert.True(h.Temp.Db.TryEquip(oldId, sword, GameServer.WeaponSlot).Success);

        var id = Lobby(h, token);
        var listed = h.Transport.Received<LobbyMessage>(id).Last().Characters.Single();
        Assert.Equal(("alice", "guerreiro", 7), (listed.Name, listed.ClassId, listed.Level));

        h.Transport.ClientSends(id, new EnterWorldIntent(oldId));
        h.Ticks(2);
        h.Remember(id, oldId);
        var me = h.Me(id);
        Assert.Equal((h.World.Map.SpawnX, h.World.Map.SpawnY), (me.X, me.Y));
        Assert.Equal(7, me.Level);
        Assert.InRange(me.Health, 200.0, 215.0); // vida salva (+ regeneração dos primeiros ticks no acampamento)
        Assert.Equal(TestContent.Sword, me.Weapon);
        Assert.Equal(1234, h.Transport.Received<ProgressMessage>(id).Last().Xp);
    }

    [Fact]
    public void ClassePersiste_AposReiniciarOServidor()
    {
        var db = new TempDb();
        try
        {
            string characterId;
            string token;
            using (var h = new MapHarness(sharedDb: db))
            {
                token = h.Temp.Db.ProvisionAccount("edu").Token;
                var id = Lobby(h, token);
                h.Transport.ClientSends(id, new CreateCharacterIntent("Edu", "mago"));
                h.Ticks(1);
                characterId = h.Transport.Received<LobbyMessage>(id).Last().Characters.Single().Id;
            }
            using (var h2 = new MapHarness(sharedDb: db))
            {
                var id = Lobby(h2, token);
                var listed = h2.Transport.Received<LobbyMessage>(id).Last().Characters.Single();
                Assert.Equal((characterId, "mago"), (listed.Id, listed.ClassId));
            }
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public void NaoEntraComPersonagemDeOutraConta()
    {
        using var h = new MapHarness();
        h.Precreate("fulano");
        var other = h.Temp.Db.FindCharacterByAccount(h.Temp.Db.AuthenticateToken(h.Precreate("ciclano"))!)!;
        var id = Lobby(h, h.Temp.Db.ProvisionAccount("intruso").Token);
        h.Transport.ClientSends(id, new EnterWorldIntent(other));
        h.Ticks(1);
        Assert.Equal("unknown_character", h.Transport.Received<LoginResult>(id).Last().Error);
    }
}

/// <summary>E8 — ações do jogador: corte em leque, esquiva, quebra de preparação, Mago, equipamentos.</summary>
public class MapCombatTests
{
    private const string Crab = "monster_r1_caranguejo_de_cal";
    private const string Raider = "monster_r1_saqueador_da_vigilia";
    private const string Corte = "skill_guerreiro_corte";
    private const string Seta = "skill_mago_seta_de_brasa";
    private const string Explosao = "skill_mago_explosao_de_cinzas";

    private static int Hits(MapHarness h, int id, string attacker, string target)
        => h.Events(id).Count(e => e.AttackerId == attacker && e.TargetId == target && e.Damage > 0);

    [Fact]
    public void Corte_AcertaQuemEstaNaFrente_NaoQuemEstaAtras()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("a", Crab, 2, 0), MapHarness.Spawn("b", Crab, -2.2, 0, group: "h") });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0);
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent(Corte, null));
        h.Ticks(10);
        Assert.Equal(1, Hits(h, id, h.CharacterId(id), "m_a"));
        Assert.Equal(0, Hits(h, id, h.CharacterId(id), "m_b"));
    }

    [Fact]
    public void Preparacao_TravaADirecao_ENaoPermiteOutraAcao_MasEsquivaCancelaARecuperacao()
    {
        using var h = new MapHarness();
        var id = h.Join("gui", x: 0, y: 0);
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent(Corte, null));
        h.Transport.ClientSends(id, new FaceIntent(-1, 0));   // girar durante a preparação não vale
        h.Ticks(2);
        Assert.StartsWith("windup:", h.Me(id).Action);
        Assert.Equal(1.0, h.Me(id).FacingX, 6);
        h.Transport.ClientSends(id, new DodgeIntent(0, 1));   // esquiva na preparação: recusada
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), r => r.Action == "dodge" && r.Reason == "busy");
        h.TickUntil(() => h.Me(id).Action.StartsWith("recover:"), 20);
        h.Transport.ClientSends(id, new CastIntent(Corte, null)); // durante a recuperação: ocupado
        h.Transport.ClientSends(id, new DodgeIntent(0, 1));       // esquiva cancela a recuperação
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<CastResultMessage>(id), r => r.Failure == "Busy");
        Assert.Equal("dodge", h.Me(id).Action);
    }

    [Fact]
    public void Monstro_MostraAPreparacao_AntesDoDano_ESairDoAlcanceEvita()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 3, 0) });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action.StartsWith("windup:talho"), 60));
        var windupSeenAt = h.Server.CurrentTick;
        Assert.DoesNotContain(h.Events(id), e => e.AttackerId == "m_s");
        // recua para longe durante a preparação
        h.Transport.ClientSends(id, new MoveIntent(-1, 0));
        h.Ticks(14);
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.TickUntil(() => h.Monster(id, "m_s")!.Action.StartsWith("recover"), 30);
        Assert.DoesNotContain(h.Events(id), e => e.AttackerId == "m_s" && e.Damage > 0);
        Assert.True(h.Server.CurrentTick - windupSeenAt >= 750 / GameServer.TickMs - 1);
    }

    [Fact]
    public void Esquiva_DaInvulnerabilidade_ContraOGolpe()
    {
        // caranguejo: golpe sem atraso aleatório (o talho do saqueador agora segura a arma, ver AtaquesComIdentidadeTests)
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", "monster_r1_caranguejo_de_cal", 2.2, 0) });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action.StartsWith("windup:"), 60));
        var monster = h.Monster(id, "m_s")!;
        // espera até ~100 ms antes do golpe e esquiva PARA DENTRO (continua no alcance, mas invulnerável)
        h.TickUntil(() => h.Monster(id, "m_s")!.ActionT >= 0.85, 30);
        h.Transport.ClientSends(id, new DodgeIntent(0.3, 0.1));
        h.TickUntil(() => h.Monster(id, "m_s")!.Action.StartsWith("recover"), 30);
        Assert.Contains(h.Events(id), e => e.AttackerId == "m_s" && e.Evaded && e.Damage == 0);
        Assert.Equal(h.Me(id).MaxHealth, h.Me(id).Health, 6);
        Assert.NotNull(monster);
    }

    [Fact]
    public void GolpeDeCunha_QuebraAPreparacao_EOMonstroNaoAtaca()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 2.0, 0) });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0);
        h.Face(id, 1, 0);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action.StartsWith("windup:"), 60));
        h.Transport.ClientSends(id, new CastIntent(TestContent.Golpe, null)); // sem alvo: o servidor escolhe o da frente
        h.TickUntil(() => h.Events(id).Any(e => e.SkillId == TestContent.Golpe), 20);
        h.Ticks(2);
        var golpe = h.Events(id).Single(e => e.SkillId == TestContent.Golpe);
        Assert.True(golpe.Staggered);
        Assert.StartsWith("recover", h.Monster(id, "m_s")!.Action);
        h.Ticks(10);
        Assert.DoesNotContain(h.Events(id), e => e.AttackerId == "m_s" && e.Damage > 0);
    }

    [Fact]
    public void AcertoNaRecuperacao_CausaMaisDano_Exposto()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 2.0, 0) });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0, level: 30); // vida alta para aguentar o golpe
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent(Corte, null));
        h.TickUntil(() => h.Events(id).Any(e => e.SkillId == Corte), 20);
        var normal = h.Events(id).First(e => e.SkillId == Corte);
        Assert.False(normal.Exposed);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action.StartsWith("recover"), 120));
        h.Ticks(1);
        h.Transport.ClientSends(id, new CastIntent(Corte, null));
        h.TickUntil(() => h.Events(id).Count(e => e.SkillId == Corte) >= 2, 20);
        var exposed = h.Events(id).Where(e => e.SkillId == Corte).Last();
        Assert.True(exposed.Exposed);
        Assert.True(exposed.Damage > normal.Damage, $"{exposed.Damage} deveria ser maior que {normal.Damage}");
    }

    [Fact]
    public void Mago_SetaVoaEAcerta_ParedeBloqueia_EExplosaoAcertaVarios()
    {
        var spawns = new[]
        {
            MapHarness.Spawn("a", Crab, 8, 0), MapHarness.Spawn("b", Crab, 0, -11, group: "x"), MapHarness.Spawn("c", Crab, 1, -11.5, group: "x"),
            MapHarness.Spawn("d", Crab, -8, 0, group: "y"),
        };
        var wall = new[] { new MapObstacle("muro", "box", -4, 0, 1, 4, 0, 3) };
        using var h = new MapHarness(MapHarness.TestWorld(spawns, wall));
        var id = h.Join("mia", "mago", 0, 0);
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent(Seta, null));
        h.Ticks(6);
        Assert.NotEmpty(h.LastSnapshot(id).Projectiles!);
        h.Ticks(15);
        Assert.Equal(1, Hits(h, id, h.CharacterId(id), "m_a"));

        h.Face(id, -1, 0); // atrás da parede
        h.Transport.ClientSends(id, new CastIntent(Seta, null));
        h.Ticks(25);
        Assert.Equal(0, Hits(h, id, h.CharacterId(id), "m_d"));

        h.Transport.ClientSends(id, new CastIntent(Explosao, null, 0.5, -11.2));
        h.Ticks(20);
        Assert.Equal(1, Hits(h, id, h.CharacterId(id), "m_b"));
        Assert.Equal(1, Hits(h, id, h.CharacterId(id), "m_c"));
    }

    [Fact]
    public void Equipamentos_MudamPoderVelocidadeAlcanceEDefesa()
    {
        using var h = new MapHarness();
        var id = h.Join("gui", x: 0, y: 0);
        var c = h.CharacterId(id);
        var axe = h.Temp.Db.CreateInventoryItem(c, "weapon_guerreiro_axe_p2", "comum", 1, "t_axe")!;
        var spear = h.Temp.Db.CreateInventoryItem(c, "weapon_guerreiro_spear_p2", "comum", 1, "t_spear")!;
        var shield = h.Temp.Db.CreateInventoryItem(c, "shield_guerreiro_tabuas", "comum", 1, "t_shield")!;
        var mail = h.Temp.Db.CreateInventoryItem(c, "armor_cota_de_malha", "comum", 1, "t_mail")!;

        CastStartedMessage Swing()
        {
            h.Transport.ClientSends(id, new CastIntent(Corte, null));
            h.Ticks(1);
            var started = h.Transport.Received<CastStartedMessage>(id).Last();
            h.Ticks(20);
            return started;
        }
        var swordSwing = Swing();
        h.Transport.ClientSends(id, new EquipIntent(axe));
        h.Ticks(1);
        var axeSwing = Swing();
        Assert.True(axeSwing.CastMs + axeSwing.RecoveryMs >= (swordSwing.CastMs + swordSwing.RecoveryMs) * 1.25); // machado 30% mais lento

        // escudo não entra com arma de duas mãos; lança (uma mão) aceita escudo
        h.Transport.ClientSends(id, new EquipIntent(shield));
        h.Ticks(1);
        Assert.Equal("two_handed", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        h.Transport.ClientSends(id, new EquipIntent(spear));
        h.Transport.ClientSends(id, new EquipIntent(shield));
        h.Transport.ClientSends(id, new EquipIntent(mail));
        h.Ticks(2);
        var me = h.Me(id);
        Assert.Equal(("weapon_guerreiro_spear_p2", "shield_guerreiro_tabuas", "armor_cota_de_malha"), (me.Weapon, me.Offhand, me.Body));

        // machado (2 mãos) guarda o escudo na mochila automaticamente
        h.Transport.ClientSends(id, new EquipIntent(axe));
        h.Ticks(2);
        Assert.Null(h.Me(id).Offhand);
        Assert.Contains(h.Transport.Received<InventoryMessage>(id).Last().Items, i => i.ItemId == shield);
    }

    [Fact]
    public void Lanca_AcertaDeMaisLonge_QueAEspada()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("a", Crab, 3.9, 0) });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0);
        var spear = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), "weapon_guerreiro_spear_p2", "comum", 1, "t")!;
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent(Corte, null));
        h.Ticks(12);
        Assert.Equal(0, Hits(h, id, h.CharacterId(id), "m_a"));
        h.Transport.ClientSends(id, new EquipIntent(spear));
        h.Ticks(1);
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent(Corte, null));
        h.Ticks(12);
        Assert.Equal(1, Hits(h, id, h.CharacterId(id), "m_a"));
    }

    [Fact]
    public void Armadura_ReduzODanoRecebido()
    {
        int TakeOneHit(bool withMail)
        {
            var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 2.0, 0) });
            using var h = new MapHarness(w);
            var id = h.Join("gui", x: 0, y: 0);
            if (withMail)
            {
                var mail = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), "armor_cota_de_malha", "comum", 1, "t")!;
                h.Transport.ClientSends(id, new EquipIntent(mail));
            }
            h.TickUntil(() => h.Events(id).Any(e => e.AttackerId == "m_s" && e.Damage > 0), 200);
            return h.Events(id).First(e => e.AttackerId == "m_s" && e.Damage > 0).Damage;
        }
        var bare = TakeOneHit(false);
        var armored = TakeOneHit(true);
        Assert.True(armored < bare * 0.85, $"com cota {armored}, sem {bare}");
    }
}

/// <summary>E8 — recompensas: loot por morte sem duplicação, baús, portão-atalho, raro, XP do mapa.</summary>
public class MapRewardTests
{
    private const string Crab = "monster_r1_caranguejo_de_cal";
    private const string Corte = "skill_guerreiro_corte";

    private static void KillWithCortes(MapHarness h, int id, string monsterId)
    {
        for (var i = 0; i < 40 && h.Monster(id, monsterId)?.Alive == true; i++)
        {
            h.Transport.ClientSends(id, new CastIntent(Corte, null));
            h.Ticks(12);
        }
        Assert.False(h.Monster(id, monsterId)!.Alive);
    }

    [Fact]
    public void MonstroMorto_DeixaLootDecididoPeloServidor_UmSoColeta_SemDuplicar()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("a", Crab, 2, 0, level: 1) }, xpMultiplier: 8);
        using var h = new MapHarness(w, rng: () => 0.0); // 0 = todas as entradas da tabela saem (e críticos)
        var a = h.Join("ana", x: 0, y: 0, level: 20);
        var b = h.Join("bia", x: 0, y: 0.6, level: 20);
        h.Face(a, 1, 0);
        KillWithCortes(h, a, "m_a");
        h.Ticks(1);
        var drops = h.LastSnapshot(a).Items;
        // rng 0 = todas as entradas da tabela saem (E9: casca + tônico + peça + arma)
        Assert.Equal(h.World.LootTables["loot_caranguejo"].Entries.Count, drops.Count);
        Assert.Contains(drops, d => d.DefinitionId == GameServer.StarterPotionDefinition);

        var gear = drops.First(d => h.Content.Items.TryGetValue(d.DefinitionId, out var def) && def.Category != "material");
        h.Transport.ClientSends(a, new ClaimItemIntent("r1", gear.ItemId));
        h.Transport.ClientSends(b, new ClaimItemIntent("r1", gear.ItemId));
        h.Ticks(1);
        Assert.True(h.Transport.Received<ClaimResultMessage>(a).Single().Success);
        Assert.False(h.Transport.Received<ClaimResultMessage>(b).Single().Success);
        var owners = new[] { a, b }.Count(p => h.Temp.Db.GetInventory(h.CharacterId(p)).Any(i => i.ItemId == gear.ItemId));
        Assert.Equal(1, owners);

        // tônico do chão soma na pilha que já existe (3 + 1)
        var potion = drops.First(d => d.DefinitionId == GameServer.StarterPotionDefinition);
        h.Transport.ClientSends(a, new ClaimItemIntent("r2", potion.ItemId));
        h.Ticks(1);
        Assert.Equal(4, h.Temp.Db.GetInventory(h.CharacterId(a)).Single(i => i.DefinitionId == GameServer.StarterPotionDefinition).Quantity);

        // XP do mapa: KillXp × multiplicador do mapa
        var gained = h.Transport.Received<ProgressMessage>(a).Last(p => p.XpGained > 0).XpGained;
        // E9: XP do modo mapa (queda gradual por diferença de nível) × multiplicador do mapa
        Assert.Equal((long)Math.Round(Progression.MapKillXp(1, 20, 1) * 8.0), gained);
    }

    [Fact]
    public void ItemDoChao_Expira_ESomeDoBanco()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("a", Crab, 2, 0, level: 1) });
        using var h = new MapHarness(w, rng: () => 0.0);
        var a = h.Join("ana", x: 0, y: 0, level: 20);
        h.Face(a, 1, 0);
        KillWithCortes(h, a, "m_a");
        h.Ticks(1);
        var drop = h.LastSnapshot(a).Items.First();
        h.Ticks(181_000 / GameServer.TickMs);
        Assert.DoesNotContain(h.LastSnapshot(a).Items, i => i.ItemId == drop.ItemId);
        Assert.Null(h.Temp.Db.GetItemLocation(drop.ItemId));
    }

    [Fact]
    public void Bau_AbreUmaVezPorPersonagem_EntregaNaMochila_EPersiste()
    {
        var chests = new[] { new MapChest("b1", 2, 0, "bau_trilha", 0) };
        var db = new TempDb();
        try
        {
            string token;
            using (var h = new MapHarness(MapHarness.TestWorld(chests: chests), sharedDb: db))
            {
                token = h.Precreate("ana", x: 0, y: 0);
                var a = h.Login(token);
                h.Transport.ClientSends(a, new InteractIntent("chest:b1"));
                h.Transport.ClientSends(a, new InteractIntent("chest:b1"));
                h.Ticks(1);
                var replies = h.Transport.Received<ActionResultMessage>(a).Where(r => r.Action == "interact").ToList();
                Assert.Equal(new[] { "chest_opened", "already_opened" }, replies.Select(r => r.Reason).ToArray());
                var inv = h.Temp.Db.GetInventory(h.CharacterId(a));
                Assert.Contains(inv, i => i.DefinitionId == "armor_gibao_acolchoado");
                Assert.Equal(5, inv.Single(i => i.DefinitionId == GameServer.StarterPotionDefinition).Quantity);
                Assert.Contains("chest:b1", h.Transport.Received<FlagsMessage>(a).Last().Flags);
                Assert.Equal(new[] { "armor_gibao_acolchoado", GameServer.StarterPotionDefinition },
                    h.Transport.Received<LootNoticeMessage>(a).Single().DefinitionIds.ToArray());
            }
            using (var h2 = new MapHarness(MapHarness.TestWorld(chests: chests), sharedDb: db))
            {
                var a = h2.Login(token);
                Assert.Contains("chest:b1", h2.Transport.Received<FlagsMessage>(a).Single().Flags);
                h2.Transport.ClientSends(a, new InteractIntent("chest:b1"));
                h2.Ticks(1);
                Assert.Equal("already_opened", h2.Transport.Received<ActionResultMessage>(a).Last().Reason);
                Assert.Single(h2.Temp.Db.GetInventory(h2.CharacterId(a)), i => i.DefinitionId == "armor_gibao_acolchoado");
            }
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public void Bau_ComMochilaCheia_NaoMarcaNemEntrega()
    {
        var chests = new[] { new MapChest("b1", 2, 0, "bau_trilha", 0) };
        using var h = new MapHarness(MapHarness.TestWorld(chests: chests));
        var a = h.Join("ana", x: 0, y: 0);
        var c = h.CharacterId(a);
        for (var i = 0; i < h.Balance.InventoryCapacity; i++) h.Temp.Db.CreateInventoryItem(c, "armor_gibao_acolchoado", "comum", 1, "fill" + i);
        var before = h.Temp.Db.GetInventory(c).Count;
        h.Transport.ClientSends(a, new InteractIntent("chest:b1"));
        h.Ticks(1);
        Assert.Equal("inventory_full", h.Transport.Received<ActionResultMessage>(a).Last().Reason);
        Assert.Equal(before, h.Temp.Db.GetInventory(c).Count);
        Assert.DoesNotContain(h.Temp.Db.GetFlags(c), f => !f.StartsWith("map:") && !f.StartsWith("kit:") && !f.StartsWith("tomo:")); // só mapa visitado (e kits de entrada)
    }

    [Fact]
    public void Alavanca_AbreOPortaoSoParaQuemPuxou_ELongeNaoFunciona()
    {
        var gates = new[] { new MapGate("atalho", "gate:atalho", 0, 5, 6, 1, 1.5, 3.5) };
        var walls = new[] { new MapObstacle("muro", "box", -9, 5, 12, 1, 0, 3), new MapObstacle("muro", "box", 9, 5, 12, 1, 0, 3) };
        using var h = new MapHarness(MapHarness.TestWorld(obstacles: walls, gates: gates));
        var a = h.Join("ana", x: 0, y: 3.5);
        var b = h.Join("bia", x: -1.5, y: 3.5);
        var far = h.Join("leo", x: 0, y: -10);
        h.Transport.ClientSends(far, new InteractIntent("lever:atalho"));
        h.Transport.ClientSends(a, new InteractIntent("lever:atalho"));
        h.Ticks(1);
        Assert.Equal("out_of_range", h.Transport.Received<ActionResultMessage>(far).Last().Reason);
        Assert.Equal("gate_opened", h.Transport.Received<ActionResultMessage>(a).Last().Reason);

        h.Transport.ClientSends(a, new MoveIntent(0, 1));
        h.Transport.ClientSends(b, new MoveIntent(0, 1));
        h.Ticks(30);
        Assert.True(h.Me(a).Y > 6, "quem puxou atravessa");
        Assert.True(h.Me(b).Y < 5, "quem não puxou continua bloqueado");
    }

    [Fact]
    public void Raro_AparecemOuNaoConformeASorte_EFoge()
    {
        var rare = new MapSpawn("r", "monster_r1_caranguejo_dourado", 5, 0, 5, "r") { Chance = 0.5, AltPoints = new[] { new[] { 5.0, 0.0 } } };
        using (var hNo = new MapHarness(MapHarness.TestWorld(new[] { rare }), rng: () => 0.9))
        {
            var id = hNo.Join("ana", x: 0, y: 0);
            Assert.Null(hNo.Monster(id, "m_r"));
        }
        using var h = new MapHarness(MapHarness.TestWorld(new[] { rare }), rng: () => 0.1);
        var p = h.Join("bia", x: 2, y: 0);
        Assert.NotNull(h.Monster(p, "m_r"));
        var start = h.Monster(p, "m_r")!.X;
        h.Ticks(20);
        Assert.Equal("flee", h.Monster(p, "m_r")!.Action);
        Assert.True(h.Monster(p, "m_r")!.X > start + 1, "o raro foge de quem chega perto");
    }

    [Fact]
    public void Chefe_MostraAMarcaDoAtaqueEmArea_AntesDeBater()
    {
        var boss = MapHarness.Spawn("chefe", "monster_r1_capataz_da_pedreira", 0, -6, 10, "chefe");
        using var h = new MapHarness(MapHarness.TestWorld(new[] { boss }));
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_chefe")!.Action == "windup:pancada", 100));
        var m = h.Monster(id, "m_chefe")!;
        Assert.True(m.Boss);
        Assert.Equal(3.2, m.AimRadius, 6);
        Assert.Equal(0.0, m.AimArc, 6);
        Assert.DoesNotContain(h.Events(id), e => e.AttackerId == "m_chefe");
    }

    [Fact]
    public void LoginAntigo_ContinuaFuncionando_NoModoMapa()
    {
        using var h = new MapHarness();
        var id = h.Login(h.Temp.Db.ProvisionAccount("velho").Token);
        Assert.True(h.Transport.Received<LoginResult>(id).Single().Ok);
        Assert.Single(h.Transport.Received<MapMessage>(id));
    }

    [Fact]
    public void Migracao_V1ParaV2_PreservaContasPersonagensEItens_EFazBackup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cdm_mig_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "v1.db");
        try
        {
            var v1 = (string)typeof(GameDatabase).GetField("MigrationV1",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetRawConstantValue()!;
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = "CREATE TABLE schema_version (version INTEGER NOT NULL);" + v1 +
                    "INSERT INTO schema_version VALUES (1);" +
                    "INSERT INTO accounts VALUES ('a1','alice','h',0,'t');" +
                    "INSERT INTO characters VALUES ('c1','a1','alice','guerreiro',6,500,'arena',1,2,300,'t');" +
                    "INSERT INTO character_stats VALUES ('c1',10,10);" +
                    "INSERT INTO item_instances VALUES ('i1','weapon_guerreiro_sword_p1','comum',0,1,1,'t','seed_arena_sword');" +
                    "INSERT INTO item_locations VALUES ('i1','equipment','c1','weapon_main');";
                cmd.ExecuteNonQuery();
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var db = new GameDatabase(path);
            db.Initialize();
            Assert.Equal(GameDatabase.CurrentSchemaVersion, db.GetSchemaVersion()); // E9: v1 → v3 de uma vez
            Assert.True(File.Exists(path + ".pre-v1.bak"));
            var ch = db.LoadCharacter("c1")!;
            Assert.Equal(("alice", 6, 500L), (ch.Name, ch.Level, ch.Xp));
            Assert.Single(db.GetEquipment("c1"), e => e.ItemId == "i1");
            Assert.Empty(db.GetFlags("c1"));
            Assert.True(db.TryAddFlag("c1", "gate:atalho"));
            // E9 (v3): nada retirado; ganha os pontos de atributo dos níveis que já tinha, moedas começam em 0
            Assert.Equal((0L, 3 * 5), (ch.Coins, ch.StatPoints));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}

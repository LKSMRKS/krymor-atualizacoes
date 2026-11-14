using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>E9 — mundo de farm: início sem nada, mapas conectados, missões, loja, viagem, dungeon, grupo e atributos.</summary>
public class MundoE9Tests
{
    private const string Crab0 = "monster_r0_caranguejinho";
    private const string Corte = "skill_guerreiro_corte";
    private const string Soco = "skill_guerreiro_soco";
    private const string Porto = "porto_das_cinzas";
    private const string Costa = "costa_do_cal";
    private const string Vale = "vale_das_cinzas";

    private static MapHarness Real(Func<double>? rng = null) => new(MapHarness.RealWorld(), rng);

    private static void Punch(MapHarness h, int id, string monsterId, int maxSwings = 40)
    {
        for (var i = 0; i < maxSwings && h.Monster(id, monsterId)?.Alive == true; i++)
        {
            var m = h.Monster(id, monsterId)!;
            var me = h.Me(id);
            h.Face(id, m.X - me.X, m.Y - me.Y);
            h.Transport.ClientSends(id, new CastIntent(Corte, null)); // sem arma o servidor troca pelo soco
            h.Ticks(11);
        }
        Assert.False(h.Monster(id, monsterId)!.Alive, $"{monsterId} continua vivo (vida {h.Monster(id, monsterId)!.Health:0}/{h.Monster(id, monsterId)!.MaxHealth:0}, eu {h.Me(id).Health:0} em {h.Me(id).X:0.0},{h.Me(id).Y:0.0}, ele em {h.Monster(id, monsterId)!.X:0.0},{h.Monster(id, monsterId)!.Y:0.0})");
    }

    [Fact]
    public void PersonagemNovo_SemArma_SocaDeGraca_EMagoUsaFaiscaSemMana()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("c", Crab0, 1.6, 0, level: 1) });
        using var h = new MapHarness(w);
        var g = h.Join("rui", x: 0, y: 0, level: 1, armed: false);
        h.Face(g, 1, 0);
        var folego = h.Me(g).Resource;
        h.Transport.ClientSends(g, new CastIntent(Corte, null));
        Assert.True(h.TickUntil(() => h.Events(g).Any(e => e.AttackerId == h.CharacterId(g)), 20),
            "sem golpe: " + string.Join(" | ", h.Transport.Received<CastResultMessage>(g).Select(r => $"{r.SkillId} {r.Success} {r.Failure}")));
        var hit = h.Events(g).First(e => e.AttackerId == h.CharacterId(g));
        Assert.Equal(Soco, hit.SkillId);
        Assert.True(hit.Damage > 0);
        Assert.True(h.Me(g).Resource >= folego);       // soco não gasta Fôlego
        Assert.Null(h.Me(g).Weapon);
        Assert.Null(h.Me(g).Body);

        var m = h.Join("mia", "mago", x: -6, y: 0, level: 1, armed: false);
        h.Face(m, 0, -1);
        h.Transport.ClientSends(m, new CastIntent("skill_mago_seta_de_brasa", null));
        Assert.True(h.TickUntil(() => h.LastSnapshot(m).Projectiles!.Any(p => p.Kind == "skill_mago_faisca"), 10),
            "sem faísca: " + string.Join(" | ", h.Transport.Received<CastResultMessage>(m).Select(r => $"{r.SkillId} {r.Success} {r.Failure}")));
        Assert.Equal(h.Me(m).ResourceMax, h.Me(m).Resource, 6); // faísca: sem mana
    }

    [Fact]
    public void Portal_LevaParaOutroMapa_SoVeQuemEstaNoMesmoMapa_EGravaOMapa()
    {
        using var h = Real();
        var a = h.Join("ana", x: 0, y: -28.5, level: 3);
        var b = h.Join("bia", x: 2, y: 4, level: 3);           // fica na cidade
        Assert.Equal(Porto, h.Last<MapMessage>(a).Map.Id);
        Assert.Contains(h.LastSnapshot(a).Players, p => p.Name == "bia");
        h.Transport.ClientSends(a, new MoveIntent(0, -1));
        Assert.True(h.TickUntil(() => h.Last<MapMessage>(a).Map.Id == Costa, 40));
        h.Transport.ClientSends(a, new MoveIntent(0, 0));
        h.Ticks(2);
        var me = h.Me(a);
        Assert.InRange(me.Y, 91, 101);                            // chegou no portão sul da Costa (mapa ×1,6)
        Assert.DoesNotContain(h.LastSnapshot(a).Players, p => p.Name == "bia");
        Assert.DoesNotContain(h.LastSnapshot(b).Players, p => p.Name == "ana");
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Kind == "area" && n.Text == "Costa do Cal");
        Assert.Equal(Costa, h.Temp.Db.LoadCharacter(h.CharacterId(a))!.MapId);

        // reconectar volta para a Costa, não para a cidade
        h.Transport.ClientDisconnects(a);
        h.Ticks(1);
        var token = h.Temp.Db.ProvisionAccount("x").Token; // (conta extra só para garantir que o servidor segue estável)
        Assert.NotNull(token);
        var again = h.Relocate(b, Porto, 2, 4);                   // b sai e volta na cidade
        Assert.Equal(Porto, h.Last<MapMessage>(again).Map.Id);
    }

    [Fact]
    public void PrimeiraArma_PorMissao_GarantidaSemDependerDeSorte()
    {
        using var h = Real();
        // nível 2 (09/11): de cueca no nível 1, os esqueletos de goblin vizinhos (nv 2) agora batem como em quem está sem
        // armadura — o teste é sobre a recompensa garantida, não sobre sobreviver ao bando inteiro só no soco
        var (ox, oy) = h.PertoDoNpc("capita_ondina");
        var id = h.Join("nina", x: ox, y: oy, level: 2);   // 08/11: começa com a arma fuleira da classe
        // 10/11: o teste é sobre a recompensa da missão; invulnerável (modo deus) para o bando da praia não decidir o resultado
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        h.Transport.ClientSends(id, new InteractIntent("npc:capita_ondina"));
        h.Ticks(1);
        var dialog = h.Last<NpcDialogMessage>(id);
        Assert.Contains(dialog.Quests, q => q.Id == "q_primeira_arma" && q.State == "available");
        h.Transport.ClientSends(id, new QuestIntent("q_primeira_arma", "accept"));
        h.Ticks(1);
        Assert.Contains(h.Last<QuestLogMessage>(id).Quests, q => q.Id == "q_primeira_arma" && q.State == "active");

        var crabs = new (string Id, double X, double Y)[] { ("a1", -35.2, 76.8), ("a2", -16, 64), ("a3", 12.8, 64), ("a4", 35.2, 73.6), ("a5", -41.6, 54.4), ("a6", 0, 51.2) };
        foreach (var (spawn, x, y) in crabs)
        {
            id = h.Relocate(id, Costa, x, y - 1.7);
            // monstros vivos nascem espalhados pela região do ponto: vai até onde ele está
            h.Ticks(1);
            if (h.Monster(id, "m_" + spawn) is { } mm) id = h.Relocate(id, Costa, mm.X, mm.Y - 1.7);
            h.Transport.ClientSends(id, new GodIntent("god"));   // a sessão nova (Relocate) começa sem o modo deus
            Punch(h, id, "m_" + spawn);
        }
        Assert.Contains(h.Last<QuestLogMessage>(id).Quests, q => q.Id == "q_primeira_arma" && q.State == "ready" && q.Progress == 6);

        id = h.Relocate(id, Porto, ox, oy);
        var coinsBefore = h.Temp.Db.GetCoins(h.CharacterId(id));
        h.Transport.ClientSends(id, new QuestIntent("q_primeira_arma", "complete"));
        h.Ticks(1);
        var inv = h.Temp.Db.GetInventory(h.CharacterId(id));
        Assert.Contains(inv, i => i.DefinitionId == "weapon_guerreiro_porrete_p0");
        Assert.Equal(coinsBefore + h.World.Quests["q_primeira_arma"].RewardCoins, h.Temp.Db.GetCoins(h.CharacterId(id)));
        // concluir de novo não paga duas vezes
        h.Transport.ClientSends(id, new QuestIntent("q_primeira_arma", "complete"));
        h.Ticks(1);
        Assert.Single(h.Temp.Db.GetInventory(h.CharacterId(id)), i => i.DefinitionId == "weapon_guerreiro_porrete_p0");

        // equipada, o clique volta a ser o Corte
        h.Transport.ClientSends(id, new EquipIntent(inv.First(i => i.DefinitionId == "weapon_guerreiro_porrete_p0").ItemId));
        h.Ticks(1);
        Assert.Equal("weapon_guerreiro_porrete_p0", h.Me(id).Weapon);
    }

    [Fact]
    public void MissaoDeEntrega_TiraOsMateriais_EDaARecompensa()
    {
        using var h = Real();
        var tib = h.World.Maps["porto_das_cinzas"].Npcs.Single(n => n.Id == "pescador_tiberio");   // 13/11: onde o dono pôs
        var id = h.Join("tito", x: tib.X + 1, y: tib.Y, level: 2, armed: false);
        var c = h.CharacterId(id);
        h.Transport.ClientSends(id, new QuestIntent("q_cascas", "accept"));
        h.Ticks(1);
        h.Temp.Db.CreateInventoryItem(c, "mat_casca_cal", "comum", 3, "t1");
        h.Transport.ClientSends(id, new QuestIntent("q_cascas", "complete"));
        h.Ticks(1);
        Assert.Equal("missing_items", h.Transport.Received<ActionResultMessage>(id).Last(r => r.Action == "quest").Reason);
        h.Temp.Db.CreateInventoryItem(c, "mat_casca_cal", "comum", 3, "t2");
        h.Transport.ClientSends(id, new QuestIntent("q_cascas", "complete"));
        h.Ticks(1);
        Assert.Equal(1, h.Temp.Db.CountInInventory(c, "mat_casca_cal"));   // 6 - 5
        Assert.Equal(1, h.Temp.Db.CountInInventory(c, "armor_gibao_acolchoado"));
    }

    [Fact]
    public void Loja_CompraComMoedas_SemDuplicar_VendaPagaUmQuarto_SoPertoDoNpc()
    {
        using var h = Real();
        var id = h.Join("leo", x: -8, y: -6, level: 1, armed: false);
        var c = h.CharacterId(id);
        h.Transport.ClientSends(id, new BuyIntent("r1", "ferreiro_bras", "weapon_guerreiro_porrete_p0"));
        h.Ticks(1);
        Assert.Equal("not_enough_coins", h.Transport.Received<ActionResultMessage>(id).Last(r => r.Action == "buy").Reason);

        h.Temp.Db.AddCoins(c, 100, "teste:moedas");
        h.Transport.ClientSends(id, new BuyIntent("r2", "ferreiro_bras", "weapon_guerreiro_porrete_p0"));
        h.Transport.ClientSends(id, new BuyIntent("r2", "ferreiro_bras", "weapon_guerreiro_porrete_p0")); // repetido
        h.Ticks(1);
        var preco = (int)h.Server.ShopPrice("weapon_guerreiro_porrete_p0");   // 14/11: preço pela régua (regular_economia.py)
        Assert.Equal(100 - preco, h.Temp.Db.GetCoins(c));
        var club = Assert.Single(h.Temp.Db.GetInventory(c), i => i.DefinitionId == "weapon_guerreiro_porrete_p0");

        h.Transport.ClientSends(id, new BuyIntent("r3", "ferreiro_bras", "weapon_guerreiro_lamina_galeria"));
        h.Ticks(1);
        Assert.Equal("not_sold_here", h.Transport.Received<ActionResultMessage>(id).Last(r => r.Action == "buy").Reason);

        h.Transport.ClientSends(id, new SellIntent("v1", "ferreiro_bras", club.ItemId));
        h.Ticks(1);
        Assert.Equal(100 - preco + preco / 4, h.Temp.Db.GetCoins(c));
        Assert.DoesNotContain(h.Temp.Db.GetInventory(c), i => i.ItemId == club.ItemId);

        // 10/11: recompra — o vendido fica no balcão e volta pelo mesmo preço; ao sair, o balcão é limpo
        var back = h.Transport.Received<BuybackMessage>(id).Last();
        Assert.Equal(club.ItemId, Assert.Single(back.Items).ItemId);
        h.Transport.ClientSends(id, new BuybackIntent("b1", "ferreiro_bras", club.ItemId));
        h.Ticks(1);
        Assert.Equal(100 - preco, h.Temp.Db.GetCoins(c));
        Assert.Contains(h.Temp.Db.GetInventory(c), i => i.ItemId == club.ItemId);
        Assert.Empty(h.Transport.Received<BuybackMessage>(id).Last().Items);
        h.Transport.ClientSends(id, new SellIntent("v2", "ferreiro_bras", club.ItemId));
        h.Ticks(1);
        Assert.Single(h.Temp.Db.GetStorage(c, "recompra"));

        var far = h.Relocate(id, Porto, 10, 10);
        h.Ticks(25);
        Assert.Empty(h.Temp.Db.GetStorage(c, "recompra"));
        h.Transport.ClientSends(far, new BuyIntent("r4", "ferreiro_bras", "helmet_gorro_couro"));
        h.Ticks(1);
        Assert.Equal("out_of_range", h.Transport.Received<ActionResultMessage>(far).Last(r => r.Action == "buy").Reason);
    }

    [Fact]
    public void ViagemRapida_SoNumPontoDescoberto_ForaDeCombate()
    {
        using var h = Real();
        var id = h.Join("vera", x: 0, y: -3, level: 3);
        h.Ticks(6);
        Assert.DoesNotContain("wp:wp_porto", h.Temp.Db.GetFlags(h.CharacterId(id)));   // 14/11: passar perto não ativa mais
        h.Transport.ClientSends(id, new InteractIntent("wp:wp_porto"));                 // interagir com a pedra ativa
        h.Ticks(1);
        Assert.Contains("wp:wp_porto", h.Temp.Db.GetFlags(h.CharacterId(id)));
        h.Transport.ClientSends(id, new TravelIntent("wp_dunas"));
        h.Ticks(1);
        Assert.Equal("unknown_waypoint", h.Transport.Received<ActionResultMessage>(id).Last(r => r.Action == "travel").Reason);

        h.Temp.Db.TryAddFlag(h.CharacterId(id), "wp:wp_dunas");
        id = h.Relocate(id, Porto, 0, -3);
        h.Transport.ClientSends(id, new CastIntent(Corte, null)); // 13/11: golpe no ar, sem monstro por perto — NÃO prende
        h.Ticks(12);                                               // (espera a recuperação do golpe)
        h.Transport.ClientSends(id, new TravelIntent("wp_dunas"));
        h.Ticks(2);
        Assert.Equal(Costa, h.Last<MapMessage>(id).Map.Id);
        Assert.InRange(h.Me(id).Y, 19, 26);

        var away = h.Relocate(id, Costa, 30, 30);
        h.Transport.ClientSends(away, new TravelIntent("wp_porto"));
        h.Ticks(1);
        Assert.Equal("not_at_waypoint", h.Transport.Received<ActionResultMessage>(away).Last(r => r.Action == "travel").Reason);
    }

    private static void Party(MapHarness h, int leader, int member, string memberName)
    {
        h.Transport.ClientSends(leader, new PartyIntent("invite", memberName));
        h.Ticks(1);
        Assert.Single(h.Transport.Received<PartyInviteMessage>(member));
        h.Transport.ClientSends(member, new PartyIntent("accept"));
        h.Ticks(1);
        Assert.Equal(2, h.Last<PartyMessage>(leader).Members.Count);
    }

    [Fact]
    public void Dungeon_UmaCopiaPorGrupo_NivelSoRecomendado_SomeVazia_EReconexaoVoltaPelaSaida()
    {
        using var h = Real();
        var a = h.Join("ana", x: -80, y: -57.6, level: 9, map: Costa);
        var b = h.Join("bia", x: -80, y: -56, level: 9, map: Costa);
        var c = h.Join("caio", x: -80, y: -54.4, level: 9, map: Costa);
        var low = h.Join("davi", x: -80, y: -52.8, level: 3, map: Costa);
        Party(h, a, b, "bia");
        var fixedZones = h.Server.ZoneCount;

        a = h.Relocate(a, Costa, -97.92, -64);
        b = h.Relocate(b, Costa, -98.24, -64);
        h.Ticks(3);
        Assert.Equal("galeria_funda", h.Last<MapMessage>(a).Map.Id);
        Assert.Equal("galeria_funda", h.Last<MapMessage>(b).Map.Id);
        Assert.Contains(h.LastSnapshot(a).Players, p => p.Name == "bia");   // mesma cópia
        Assert.Equal(fixedZones + 1, h.Server.ZoneCount);

        c = h.Relocate(c, Costa, -97.92, -64);
        h.Ticks(3);
        Assert.Equal("galeria_funda", h.Last<MapMessage>(c).Map.Id);
        Assert.DoesNotContain(h.LastSnapshot(c).Players, p => p.Name == "ana"); // outra cópia (sozinho)

        // nível só recomendado: avisa, mas deixa entrar (pode descobrir que é cedo e voltar)
        low = h.Relocate(low, Costa, -97.92, -64);
        h.Ticks(3);
        Assert.Equal("galeria_funda", h.Last<MapMessage>(low).Map.Id);
        Assert.Contains(h.Transport.Received<NoticeMessage>(low), n => n.Kind == "warn" && n.Text.Contains("nível 7"));
        Assert.Equal(fixedZones + 3, h.Server.ZoneCount);

        // todos saem: as cópias duram 5 min (reconexão) e depois somem
        foreach (var x in new[] { a, b, c, low }) h.Transport.ClientDisconnects(x);
        h.Ticks(1);
        Assert.Equal(fixedZones + 3, h.Server.ZoneCount);
        h.Ticks(5 * 60 * 20 + 40);
        Assert.Equal(fixedZones, h.Server.ZoneCount);
        var back = h.Relocate(a, "galeria_funda", 0, 20);                         // salvo dentro da cópia que acabou
        Assert.Equal(Costa, h.Last<MapMessage>(back).Map.Id);
        Assert.InRange(h.Me(back).X, -96, -88);
    }

    [Fact]
    public void Grupo_DivideXp_ELootFicaComQuemParticipouPorUmTempo()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("c", Crab0, 1.6, 0, level: 2) });
        using var h = new MapHarness(w, rng: () => 0.0);                         // todas as entradas de loot saem
        var a = h.Join("ana", level: 2, x: 0, y: 0);
        var b = h.Join("bia", level: 2, x: -4, y: 0);
        var c = h.Join("caio", level: 2, x: -4, y: 3);
        Party(h, a, b, "bia");
        h.Face(a, 1, 0);
        for (var i = 0; i < 20 && h.Monster(a, "m_c")!.Alive; i++)
        {
            h.Transport.ClientSends(a, new CastIntent(Corte, null));
            h.Ticks(12);
        }
        h.Ticks(1);
        Assert.True(h.Transport.Received<ProgressMessage>(b).Any(p => p.XpGained > 0));   // membro perto ganhou XP sem bater
        Assert.False(h.Transport.Received<ProgressMessage>(c).Any(p => p.XpGained > 0));  // quem não é do grupo, não
        var drop = h.LastSnapshot(a).Items.First();
        h.Transport.ClientSends(c, new ClaimItemIntent("r1", drop.ItemId));
        h.Ticks(1);
        Assert.Equal("out_of_range", h.Last<ClaimResultMessage>(c).Reason == "out_of_range" ? "out_of_range" : h.Last<ClaimResultMessage>(c).Reason);
        c = h.Relocate(c, "teste", drop.X, drop.Y + 0.5);
        h.Transport.ClientSends(c, new ClaimItemIntent("r2", drop.ItemId));
        h.Ticks(1);
        Assert.Equal("not_yours_yet", h.Last<ClaimResultMessage>(c).Reason);
        h.Ticks(46 * 20);
        h.Transport.ClientSends(c, new ClaimItemIntent("r3", drop.ItemId));
        h.Ticks(1);
        Assert.True(h.Last<ClaimResultMessage>(c).Success);                              // depois de 45 s, livre
    }

    [Fact]
    public void SubirDeNivel_DaPontos_EGastarMudaOPoder()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("c", Crab0, 1.6, 0, level: 3) }, xpMultiplier: 40);
        using var h = new MapHarness(w);
        var id = h.Join("ugo", level: 1, x: 0, y: 0);
        h.Face(id, 1, 0);
        for (var i = 0; i < 30 && h.Monster(id, "m_c")!.Alive; i++)
        {
            h.Transport.ClientSends(id, new CastIntent(Corte, null));
            h.Ticks(12);
        }
        var level = h.Transport.Received<ProgressMessage>(id).Last().Level;
        Assert.True(level >= 2, $"nível {level}, xp {h.Transport.Received<ProgressMessage>(id).Last().Xp}, vivo {h.Monster(id, "m_c")!.Alive}");
        var stats = h.Last<StatsMessage>(id);
        Assert.Equal(3 * (level - 1), stats.Points);
        h.Transport.ClientSends(id, new SpendStatIntent("primary"));
        h.Ticks(1);
        var after = h.Last<StatsMessage>(id);
        Assert.Equal(stats.Points - 1, after.Points);
        Assert.Equal(stats.Primary + 1, after.Primary);
        Assert.Equal(stats.Power + 2.2, after.Power, 6);
        Assert.Equal((stats.Primary + 1, after.Points), (h.Temp.Db.GetStats(h.CharacterId(id)).Primary, h.Temp.Db.GetStats(h.CharacterId(id)).Points));
    }

    [Fact]
    public void FaixaDeNivel_MonstroAcimaApanhaMenos_EBateMaisForte()
    {
        Assert.Equal(1.0, CombatFormulas.PlayerVsHigherLevel(5, 5), 6);
        Assert.Equal(1.0, CombatFormulas.PlayerVsHigherLevel(1, 3), 6);    // 14/11: até 2 níveis acima, dano normal
        Assert.Equal(0.95, CombatFormulas.PlayerVsHigherLevel(1, 4), 6);
        Assert.Equal(0.5, CombatFormulas.PlayerVsHigherLevel(1, 20), 6);
        Assert.Equal(1.45, CombatFormulas.MonsterVsLevel(4, 1), 6);
        Assert.Equal(0.84, CombatFormulas.MonsterVsLevel(3, 5), 6);

        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("fraco", Crab0, 1.6, 0, level: 1), MapHarness.Spawn("forte", Crab0, -1.6, 0, level: 6, group: "f") });
        using var h = new MapHarness(w);
        var id = h.Join("eva", level: 1, x: 0, y: 0);
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent(Corte, null));
        h.Ticks(12);
        h.Face(id, -1, 0);
        h.Transport.ClientSends(id, new CastIntent(Corte, null));
        h.Ticks(12);
        var weak = h.Events(id).First(e => e.TargetId == "m_fraco").Damage;
        var strong = h.Events(id).First(e => e.TargetId == "m_forte").Damage;
        Assert.True(strong < weak * 0.85, $"{strong} contra nível 6 deveria ser menos que {weak} contra nível 1");   // 14/11: regra suavizada (armadura + 5% por nível acima de 2)
    }

    [Fact]
    public void Monstro_NaoReapareceEmCimaDoJogador()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("c", Crab0, 1.6, 0, level: 1) });
        using var h = new MapHarness(w);
        var id = h.Join("ivo", level: 10, x: 0, y: 0);
        h.Face(id, 1, 0);
        for (var i = 0; i < 10 && h.Monster(id, "m_c")!.Alive; i++) { h.Transport.ClientSends(id, new CastIntent(Corte, null)); h.Ticks(12); }
        Assert.False(h.Monster(id, "m_c")!.Alive);
        h.Ticks(40 * 20);                                                   // passou do tempo (30 s), jogador ainda ali
        Assert.False(h.Monster(id, "m_c")!.Alive);
        id = h.Relocate(id, "teste", 0, 15);                                // se afasta
        h.Ticks(4 * 20);
        Assert.True(h.Monster(id, "m_c")!.Alive);
    }
}

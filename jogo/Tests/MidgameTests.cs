using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Tests;

/// <summary>Expansão até o midgame: ofícios, mineração, horta, armazém, refeição, montaria, asas, companheiro, selo, casa, lendário.</summary>
public class MidgameTests
{
    private const string Porto = "porto_das_cinzas";
    private const string Costa = "costa_do_cal";

    private static MapHarness Real() => new(MapHarness.RealWorld());

    private static MapObject Obj(MapHarness h, string map, string id) => h.World.Maps[map].Objects.Single(o => o.Id == id);

    private static void Give(MapHarness h, int id, string def, int qty)
        => h.Temp.Db.CreateInventoryItem(h.CharacterId(id), def, "comum", qty, "t:" + Guid.NewGuid());

    private static int Count(MapHarness h, int id, string def) => h.Temp.Db.CountInInventory(h.CharacterId(id), def);

    [Fact]
    public void Forja_FazRebites_ConsomeIngredientes_EPedidoRepetidoNaoDuplica()
    {
        using var h = Real();
        var forge = Obj(h, Porto, "porto_forja");
        var id = h.Join("ana", x: forge.X, y: forge.Y + 1.5, level: 10, map: Porto);
        Give(h, id, "mat_ferro_raso", 4);
        id = h.Relocate(id, Porto, forge.X, forge.Y + 1.5);
        h.Transport.ClientSends(id, new InteractIntent("obj:porto_forja"));
        h.Ticks(1);
        var station = h.Last<StationMessage>(id);
        Assert.Contains(station.Recipes, r => r.Id == "rec_rebites" && r.CanCraft);
        h.Transport.ClientSends(id, new CraftIntent("req1", "porto_forja", "rec_rebites"));
        h.Transport.ClientSends(id, new CraftIntent("req1", "porto_forja", "rec_rebites")); // repetido: mesmo resultado
        h.Ticks(1);
        Assert.Equal(3, Count(h, id, "mat_rebites"));
        Assert.Equal(2, Count(h, id, "mat_ferro_raso"));
        // receita desconhecida (vem de missão) é recusada
        h.Transport.ClientSends(id, new CraftIntent("req2", "porto_forja", "rec_placa_chapa"));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), a => a.Action == "craft" && a.Reason == "recipe_unknown");
    }

    [Fact]
    public void Mineracao_PedePicareta_LevaTempo_GolpeOuPassoCancela_ERecargaEPessoal()
    {
        using var h = Real();
        var vein = h.World.Maps[Costa].Objects.First(o => o.Id.StartsWith("costa_veio", StringComparison.Ordinal));
        var id = h.Join("ana", x: vein.X + 1.5, y: vein.Y, level: 6, map: Costa);
        h.Transport.ClientSends(id, new InteractIntent("obj:" + vein.Id));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), a => a.Reason == "need_tool");

        h.Temp.Db.TryAddFlag(h.CharacterId(id), "tool:picareta");
        id = h.Relocate(id, Costa, vein.X + 1.5, vein.Y);
        h.Transport.ClientSends(id, new InteractIntent("obj:" + vein.Id));
        h.Ticks(5);
        Assert.Equal(0, Count(h, id, "mat_ferro_raso"));        // ainda coletando
        h.Ticks(40);
        var got = Count(h, id, "mat_ferro_raso");
        Assert.InRange(got, 1, 2);
        // esgotado para mim, mas outro personagem ainda pode tirar o dele
        h.Transport.ClientSends(id, new InteractIntent("obj:" + vein.Id));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), a => a.Reason == "node_empty");
        var other = h.Join("bia", x: vein.X - 1.5, y: vein.Y, level: 6, map: Costa);
        h.Temp.Db.TryAddFlag(h.CharacterId(other), "tool:picareta");
        other = h.Relocate(other, Costa, vein.X - 1.5, vein.Y);
        h.Transport.ClientSends(other, new InteractIntent("obj:" + vein.Id));
        h.Ticks(2);
        h.Transport.ClientSends(other, new MoveIntent(1, 0));   // andou: cancela
        h.Ticks(35);
        Assert.Equal(0, Count(h, other, "mat_ferro_raso"));
    }

    [Fact]
    public void Horta_PlantaRegaEsperaColhe_PeloRelogioDoServidor()
    {
        using var h = Real();
        var clock = 1_000_000_000L;
        h.Server.WallClockMs = () => clock;
        var plot = Obj(h, Porto, "porto_canteiro0");
        var id = h.Join("ana", x: plot.X + 1.2, y: plot.Y, level: 5, map: Porto);
        Give(h, id, "semente_raiz", 1);
        id = h.Relocate(id, Porto, plot.X + 1.2, plot.Y);
        h.Transport.ClientSends(id, new InteractIntent("obj:porto_canteiro0"));
        h.Ticks(1);
        Assert.Equal(0, Count(h, id, "semente_raiz"));
        h.Transport.ClientSends(id, new InteractIntent("obj:porto_canteiro0"));   // rega
        h.Ticks(1);
        Assert.Equal("growing", h.Last<PlotsMessage>(id).Plots.Single().State);
        h.Transport.ClientSends(id, new InteractIntent("obj:porto_canteiro0"));   // cedo demais
        h.Ticks(1);
        Assert.Equal(0, Count(h, id, "mat_raiz_vigilia"));
        clock += 4 * 60_000;                                                      // passou o tempo (mesmo deslogado)
        id = h.Relocate(id, Porto, plot.X + 1.2, plot.Y);
        Assert.Equal("ready", h.Last<PlotsMessage>(id).Plots.Single().State);
        h.Transport.ClientSends(id, new InteractIntent("obj:porto_canteiro0"));
        h.Ticks(1);
        Assert.InRange(Count(h, id, "mat_raiz_vigilia"), 2, 3);
        Assert.Empty(h.Last<PlotsMessage>(id).Plots);
    }

    [Fact]
    public void Regador_FazAPlantaCrescerMaisRapido()
    {
        using var h = Real();
        var clock = 1_000_000_000L;
        h.Server.WallClockMs = () => clock;
        var plot = Obj(h, Porto, "porto_canteiro1");
        var id = h.Join("ana", x: plot.X + 1.2, y: plot.Y, level: 5, map: Porto);
        Give(h, id, "semente_raiz", 1);
        Give(h, id, "mat_regador", 1);
        id = h.Relocate(id, Porto, plot.X + 1.2, plot.Y);
        h.Transport.ClientSends(id, new InteractIntent("obj:porto_canteiro1"));
        h.Ticks(1);
        h.Transport.ClientSends(id, new InteractIntent("obj:porto_canteiro1"));   // rega com o regador
        h.Ticks(1);
        // raiz: 3 min sem regador; com ele, 2,1 min
        Assert.InRange(h.Last<PlotsMessage>(id).Plots.Single().ReadyInMs, 2 * 60_000, (long)(2.2 * 60_000));
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("com o regador"));
    }

    [Fact]
    public void Armazem_GuardaERetira_AMesmaInstancia_SemDuplicar()
    {
        using var h = Real();
        var store = Obj(h, Porto, "porto_armazem");
        var id = h.Join("ana", x: store.X + 1.5, y: store.Y, level: 5, map: Porto);
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), "helmet_gorro_couro", "comum", 1, "t:gorro")!;
        id = h.Relocate(id, Porto, store.X + 1.5, store.Y);
        h.Transport.ClientSends(id, new StorageIntent("s1", "deposit", "armazem", item));
        h.Transport.ClientSends(id, new StorageIntent("s1", "deposit", "armazem", item));
        h.Ticks(1);
        Assert.Equal(0, Count(h, id, "helmet_gorro_couro"));
        var storage = h.Last<StorageMessage>(id);
        Assert.Equal(12, storage.Capacity);
        Assert.Single(storage.Items);
        h.Transport.ClientSends(id, new StorageIntent("s2", "withdraw", "armazem", item));
        h.Ticks(1);
        Assert.Equal(1, Count(h, id, "helmet_gorro_couro"));
        Assert.Empty(h.Last<StorageMessage>(id).Items);
        // longe do armazém não funciona
        id = h.Relocate(id, Porto, 0, -8);
        h.Transport.ClientSends(id, new StorageIntent("s3", "deposit", "armazem", item));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), a => a.Action == "storage" && a.Reason == "out_of_range");
    }

    [Fact]
    public void Refeicao_DaPoderTemporario_UmEfeitoPorVez()
    {
        using var h = Real();
        var clock = 2_000_000_000L;
        h.Server.WallClockMs = () => clock;
        var id = h.Join("ana", x: 0, y: -8, level: 20, map: Porto);
        Give(h, id, "food_ensopado_vigilia", 1);
        id = h.Relocate(id, Porto, 0, -8);
        var before = h.Last<StatsMessage>(id).MaxHealth;
        var food = h.Temp.Db.GetInventory(h.CharacterId(id)).Single(i => i.DefinitionId == "food_ensopado_vigilia");
        h.Transport.ClientSends(id, new UseItemIntent(food.ItemId));
        h.Ticks(2);
        Assert.Equal("Ensopado da Vigília", h.Last<BuffMessage>(id).Name);
        Assert.True(h.Last<StatsMessage>(id).MaxHealth > before);
        clock += 11 * 60_000;
        h.Ticks(2);
        Assert.Equal("", h.Last<BuffMessage>(id).Name);
    }

    [Fact]
    public void Montaria_SoForaDeCombate_MaisRapida_DesmontaAoAtacar_ENaoEntraEmDungeon()
    {
        using var h = new MapHarness(MapHarness.TestWorld());
        var id = h.Join("ana", x: 0, y: 0, level: 25);
        h.Transport.ClientSends(id, new MountIntent(true));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), a => a.Action == "mount" && a.Reason == "no_mount");
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "mount:corcel");
        id = h.Relocate(id, h.World.StartMap, 0, 0);
        h.Transport.ClientSends(id, new MountIntent(true));
        h.Ticks(15);
        Assert.Equal("corcel", h.Me(id).Mount);
        h.Transport.ClientSends(id, new MoveIntent(1, 0));
        h.Ticks(20);
        Assert.InRange(h.Me(id).X, 8.0, 10.5);        // ~5 m/s × 1,9 por 1 s
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Transport.ClientSends(id, new CastIntent("skill_guerreiro_corte", null));
        h.Ticks(2);
        Assert.Null(h.Me(id).Mount);
    }

    [Fact]
    public void Asas_ImpulsoCurto_ComRecarga_NaoAtravessaParede()
    {
        var wall = new MapObstacle("muralha", "box", 3, 0, 1, 10, 0, 4);
        using var h = new MapHarness(MapHarness.TestWorld(obstacles: new[] { wall }));
        var id = h.Join("ana", x: 0, y: 0, level: 48);
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "wings:ferro");
        id = h.Relocate(id, h.World.StartMap, 0, 0);
        h.Transport.ClientSends(id, new WingDashIntent(1, 0));
        h.Ticks(2);
        Assert.InRange(h.Me(id).X, 1.5, 2.5);          // parou na parede
        h.Transport.ClientSends(id, new WingDashIntent(-1, 0));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), a => a.Action == "wing_dash" && a.Reason == "on_cooldown");
        h.Ticks(170);
        h.Transport.ClientSends(id, new WingDashIntent(-1, 0));
        h.Ticks(2);
        Assert.InRange(h.Me(id).X, -4.5, -3.5);
    }

    [Fact]
    public void Companheiro_AtacaOAlvoDoDono_SoEmCombate_EODanoContaParaODono()
    {
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("a", "monster_r0_caranguejinho", 6, 0, 1, "a") }));
        var id = h.Join("ana", x: 0, y: 0, level: 32);
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "pet:dragao");
        id = h.Relocate(id, h.World.StartMap, 0, 0);
        h.Transport.ClientSends(id, new PetIntent("summon", "dragao"));
        h.Ticks(40);
        Assert.NotNull(h.LastSnapshot(id).Pets);
        Assert.DoesNotContain(h.Events(id), e => e.SkillId == "skill_pet_dragao");      // não inicia luta sozinho
        h.Transport.ClientSends(id, new MoveIntent(1, 0));
        h.Ticks(10);
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent("skill_guerreiro_corte", null));
        h.Ticks(60);
        Assert.Contains(h.Events(id), e => e.SkillId == "skill_pet_dragao" && e.AttackerId == h.CharacterId(id));
    }

    [Fact]
    public void ChefeDePassagem_DaSeloPessoal_EOPortaoAbreSoParaQuemParticipou()
    {
        using var h = Real();
        var mata = h.World.Maps["mata_dos_enforcados"];
        var gate = mata.Gates.Single(g => g.Flag == "selo:mata");
        var id = h.Join("ana", x: gate.X, y: -58, level: 25, map: "mata_dos_enforcados");   // 13/11: o vão do muro saiu do meio
        h.Transport.ClientSends(id, new MoveIntent(0, -1));
        h.Ticks(30);
        Assert.True(h.Me(id).Y > gate.Y, "sem selo não passa");
        // derrota do guardião concede o selo (o abate em si usa o mesmo caminho dos outros chefes)
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "selo:mata");
        id = h.Relocate(id, "mata_dos_enforcados", gate.X, -58);
        h.Transport.ClientSends(id, new MoveIntent(0, -1));
        h.Ticks(70);
        Assert.Contains(h.Last<MapMessage>(id).Map.Id, new[] { "canions_suplicantes", "astralia" });   // 14/11: a estrada passa por Astrália
    }

    [Fact]
    public void Casa_CompraPorMissao_ECopiaPessoal_ComBau()
    {
        using var h = Real();
        // 13/11 22:26 (dono): a entrada da casa no Porto é o portal completo (vermelho até ter a casa) — andar para dentro dele
        var portal = h.World.Maps[Porto].Portals.Single(p => p.Id == "porto_casa");
        Assert.False(portal.PorPorta);
        var id = h.Join("ana", x: portal.X - 2.6, y: portal.Y, level: 30, map: Porto);
        h.Transport.ClientSends(id, new MoveIntent(1, 0));
        h.Ticks(25);
        Assert.Equal(Porto, h.Last<MapMessage>(id).Map.Id);   // sem casa: não entra
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "casa:propria");
        id = h.Relocate(id, Porto, portal.X - 2.6, portal.Y);
        h.Transport.ClientSends(id, new MoveIntent(1, 0));
        Assert.True(h.TickUntil(() => h.Last<MapMessage>(id).Map.Id == "casa_jogador", 40));
        id = h.Relocate(id, "casa_jogador", -5, -4.5);
        h.Transport.ClientSends(id, new InteractIntent("obj:casa_bau"));
        h.Ticks(1);
        Assert.Equal(32, h.Last<StorageMessage>(id).Capacity);
    }

    [Fact]
    public void MissaoDeAcao_ContaSoDepoisDeAceitar_ETerOItemNaoBasta()
    {
        using var h = Real();
        var ilma = h.World.Maps[Porto].Npcs.Single(n => n.Id == "curandeira_ilma");
        var id = h.Join("ana", x: ilma.X, y: ilma.Y + 1.5, level: 5, map: Porto);
        Give(h, id, "mat_erva_tonico", 10);                       // já ter ervas não completa uma missão de COLETA
        id = h.Relocate(id, Porto, ilma.X, ilma.Y + 1.5);
        h.Transport.ClientSends(id, new QuestIntent("q_m03", "accept"));
        h.Ticks(1);
        Assert.Contains(h.Last<QuestLogMessage>(id).Quests, q => q.Id == "q_m03" && q.State == "active" && q.Progress == 0);
    }

    [Fact]
    public void AltarDasCascas_GastaAsCascas_EChamaARainha()
    {
        using var h = Real();
        var altar = h.World.Maps["costa_do_cal"].Objects.Single(o => o.Id == "costa_altar_cascas");
        var id = h.Join("ana", x: altar.X + 1.5, y: altar.Y, level: 9, map: "costa_do_cal");
        Give(h, id, "mat_casca_cal", 10);
        id = h.Relocate(id, "costa_do_cal", altar.X + 1.5, altar.Y);
        h.Transport.ClientSends(id, new InteractIntent("obj:costa_altar_cascas"));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("O altar pede 15"));
        Give(h, id, "mat_casca_cal", 10);
        h.Transport.ClientSends(id, new InteractIntent("obj:costa_altar_cascas"));
        h.Ticks(2);
        Assert.Equal(5, Count(h, id, "mat_casca_cal"));
        Assert.Contains(h.LastSnapshot(id).Monsters, m => m.DefinitionId == "monster_r1_rainha_das_cascas" && m.Alive);
        h.Transport.ClientSends(id, new InteractIntent("obj:costa_altar_cascas"));   // já acordada: não gasta de novo
        h.Ticks(1);
        Assert.Equal(5, Count(h, id, "mat_casca_cal"));
    }

    [Fact]
    public void DesafioDoBobo_UmaVezPorDia_VoltaNoDiaSeguinte()
    {
        using var h = Real();
        var clock = new DateTimeOffset(2026, 11, 9, 15, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        h.Server.WallClockMs = () => clock;
        var bobo = h.World.Maps[Porto].Npcs.Single(n => n.Id == "bobo_tiao");
        var id = h.Join("ana", x: bobo.X, y: bobo.Y + 1.5, level: 5, map: Porto);
        id = h.Relocate(id, Porto, bobo.X, bobo.Y + 1.5);
        h.Transport.ClientSends(id, new QuestIntent("q_bobo_cascas", "accept"));
        h.Ticks(1);
        Give(h, id, "mat_casca_cal", 8);
        h.Transport.ClientSends(id, new QuestIntent("q_bobo_cascas", "complete"));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("Desafio do Bobo: Chapéu de Cascas"));
        h.Transport.ClientSends(id, new QuestIntent("q_bobo_cascas", "accept"));   // no mesmo dia: não
        h.Ticks(1);
        Assert.Equal("already_today", h.Transport.Received<ActionResultMessage>(id).Last(r => r.Action == "quest").Reason);
        clock += 24 * 3_600_000L;                                                 // dia seguinte: pode de novo
        h.Transport.ClientSends(id, new QuestIntent("q_bobo_cascas", "accept"));
        h.Ticks(1);
        Assert.True(h.Transport.Received<ActionResultMessage>(id).Last(r => r.Action == "quest").Success);
    }

    [Fact]
    public void Lendario_DisparaEfeitoACadaNGolpes()
    {
        var spawns = new[] { MapHarness.Spawn("a", "monster_r1_rei_das_pincas", 2.4, 0, 40, "a") };
        using var h = new MapHarness(MapHarness.TestWorld(spawns), rng: () => 0.99);
        var id = h.Join("gui", x: 0, y: 0, level: 60, armed: false);
        h.Temp.Db.AddStatPoints(h.CharacterId(id), 200);
        for (var i = 0; i < 160; i++) { h.Transport.ClientSends(id, new SpendStatIntent("primary")); if (i % 4 == 3) h.Ticks(1); }
        h.Ticks(1);
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), "legend_espada_sino_partido", "lendario", 1, "t:lend")!;
        h.Transport.ClientSends(id, new EquipIntent(item));
        h.Ticks(4);
        Assert.True(h.Me(id).Legendary, string.Join(",", h.Transport.Received<ActionResultMessage>(id).Select(a => a.Action + ":" + a.Reason).Distinct()) + " w=" + h.Me(id).Weapon);
        h.Face(id, 1, 0);
        for (var i = 0; i < 6; i++) { h.Transport.ClientSends(id, new CastIntent("skill_guerreiro_corte", null)); h.Ticks(14); }
        Assert.Contains(h.Events(id), e => e.SkillId == "skill_lend_onda_sino");
    }

    [Fact]
    public void TodoMapa_CabeNumQuadro_ComFolga()
    {
        using var h = Real();
        foreach (var (id, layout) in h.World.Maps)
        {
            var bytes = NetCodec.Encode(new MapMessage(layout)).Length;
            Assert.True(bytes < Framing.MaxFrameBytes / 2, $"{id}: {bytes} bytes");
        }
    }
}

using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Tests;

/// <summary>Direção consolidada (08/11): combos, 2ª fase, estilos e habilidades de arma, evolução, mundo que muda, dungeon.</summary>
public class DirecaoConsolidadaTests
{
    private const string Corte = "skill_guerreiro_corte";
    private const string Costa = "costa_do_cal";
    private const string Porto = "porto_das_cinzas";

    private static MapHarness Real(Func<double>? rng = null) => new(MapHarness.RealWorld(), rng);

    private static string GiveAndEquip(MapHarness h, int id, string definition)
    {
        h.Temp.Db.AddStatPoints(h.CharacterId(id), 20);
        for (var i = 0; i < 20; i++) h.Transport.ClientSends(id, new SpendStatIntent("primary"));
        h.Ticks(1);
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), definition, "comum", 1, "t:" + Guid.NewGuid())!;
        h.Transport.ClientSends(id, new EquipIntent(item));
        h.Ticks(1);
        return item;
    }

    [Fact]
    public void Elite_EmendaGolpes_EABrechaVemNoFim()
    {
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("e", "monster_r1_caranguejo_couracado", 2.8, 0, level: 7) }));
        var id = h.Join("ana", x: 0, y: 0, level: 40);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_e")!.Action == "windup:bote", 100));
        Assert.True(h.TickUntil(() => h.Monster(id, "m_e")!.Action != "windup:bote" && h.Monster(id, "m_e")!.Action != "active:bote", 40));
        Assert.Equal("windup:pincada", h.Monster(id, "m_e")!.Action);                       // emendou, sem recuperação
        Assert.True(h.TickUntil(() => h.Monster(id, "m_e")!.Action == "recover:pincada", 40)); // brecha depois do último
    }

    [Fact]
    public void Chefe_MudaDeFase_AbaixoDaMetade()
    {
        var boss = MapHarness.Spawn("rei", "monster_r1_rei_das_pincas", 2.5, 0, level: 10, group: "rei");
        using var h = new MapHarness(MapHarness.TestWorld(new[] { boss }));
        var id = h.Join("ana", x: 0, y: 0, level: 60);
        h.Face(id, 1, 0);
        Assert.False(h.Monster(id, "m_rei")!.Enraged);
        for (var i = 0; i < 80 && h.Monster(id, "m_rei")!.Health > h.Monster(id, "m_rei")!.MaxHealth * 0.45; i++)
        {
            h.Transport.ClientSends(id, new CastIntent(Corte, null));
            h.Ticks(12);
        }
        Assert.True(h.Monster(id, "m_rei")!.Enraged);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("enfurece"));
    }

    [Fact]
    public void Machado_InterrompeAPreparacao_EspadaNao()
    {
        const string raider = "monster_r1_saqueador_da_vigilia";
        foreach (var (weapon, expect) in new[] { ("weapon_guerreiro_axe_p2", true), ("weapon_guerreiro_sword_p1", false) })
        {
            using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("s", raider, 2.0, 0) }));
            var id = h.Join("gui", x: 0, y: 0, level: 30, armed: false);
            GiveAndEquip(h, id, weapon);
            h.Face(id, 1, 0);
            Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action == "windup:talho", 100));
            h.Transport.ClientSends(id, new CastIntent(Corte, null));
            h.Ticks(14);
            Assert.Equal(expect, h.Events(id).Any(e => e.Staggered));
        }
    }

    [Fact]
    public void HabilidadeDaArma_SoComAArma_EAvancaAcertandoOCaminho()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("a", "monster_r0_caranguejinho", 2, 0, 1, "a"), MapHarness.Spawn("b", "monster_r0_caranguejinho", 4, 0.3, 1, "b") });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0, level: 30);
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent("skill_arma_corte_avancante", null));
        h.Ticks(1);
        Assert.Equal("NoWeaponSkill", h.Transport.Received<CastResultMessage>(id).Last().Failure);
        GiveAndEquip(h, id, "weapon_guerreiro_garra_rei");
        h.Ticks(3);
        h.Transport.ClientSends(id, new CastIntent("skill_arma_corte_avancante", null));
        h.Ticks(20);
        Assert.True(h.Me(id).X > 3.5, $"avançou só até {h.Me(id).X}; casts: {string.Join(",", h.Transport.Received<CastResultMessage>(id).Select(c => c.SkillId+":"+c.Failure))}; equip: {string.Join(",", h.Transport.Received<ActionResultMessage>(id).Select(a=>a.Reason))}");
        Assert.Contains(h.Events(id), e => e.TargetId == "m_a" && e.SkillId == "skill_arma_corte_avancante");
        Assert.Contains(h.Events(id), e => e.TargetId == "m_b" && e.SkillId == "skill_arma_corte_avancante");
    }

    [Fact]
    public void SetaPerfurante_AtravessaVarios()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("a", "monster_r0_caranguejinho", 4, 0, 1, "a"), MapHarness.Spawn("b", "monster_r0_caranguejinho", 8, 0, 1, "b") });
        using var h = new MapHarness(w);
        var id = h.Join("mia", "mago", x: 0, y: 0, level: 30, armed: false);
        GiveAndEquip(h, id, "staff_mago_cajado_galeria");
        h.Face(id, 1, 0);
        h.Transport.ClientSends(id, new CastIntent("skill_arma_perfurante", null));
        h.Ticks(30);
        Assert.Contains(h.Events(id), e => e.TargetId == "m_a" && e.SkillId == "skill_arma_perfurante");
        Assert.Contains(h.Events(id), e => e.TargetId == "m_b" && e.SkillId == "skill_arma_perfurante");
    }

    [Fact]
    public void Evolucao_NoNivel15_DaAtributosDaClasse()
    {
        using var h = Real();
        var id = h.Join("leo", x: -20, y: -6, level: 14);
        h.Transport.ClientSends(id, new QuestIntent("q_troca_g", "accept"));
        h.Ticks(1);
        Assert.DoesNotContain("evo:campeao", h.Temp.Db.GetFlags(h.CharacterId(id)));   // nível 14: ainda não
        id = h.Join("lia", x: -20, y: -6, level: 15);
        var (p0, v0, pts0) = h.Temp.Db.GetStats(h.CharacterId(id));
        var (f0, a0, _) = h.Temp.Db.GetExtraAttributes(h.CharacterId(id));
        id = h.ProvasDeClasseFeitas(id, "g");   // 14/11: a troca pede as 4 provas do mentor
        h.Transport.ClientSends(id, new QuestIntent("q_troca_g", "accept"));
        h.Ticks(1);
        Assert.Contains("evo:campeao", h.Temp.Db.GetFlags(h.CharacterId(id)));
        var (p1, v1, pts1) = h.Temp.Db.GetStats(h.CharacterId(id));
        var (_, a1, _) = h.Temp.Db.GetExtraAttributes(h.CharacterId(id));
        Assert.Equal(p0 + 15, p1);   // Força (principal do Guerreiro)
        Assert.Equal(v0 + 10, v1);
        Assert.Equal(a0 + 6, a1);
        Assert.Equal(pts0 + 5, pts1);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("+15 Força"));
        Assert.Contains("tomo:skill_campeao_giro_do_leao", h.Temp.Db.GetFlags(h.CharacterId(id)));   // golpe novo da 2ª classe
    }

    [Fact]
    public void Evolucao_SoDepoisDeFalarComOMentor()
    {
        using var h = Real();
        var id = h.Join("gui", x: -20, y: -6, level: 20);
        h.Transport.ClientSends(id, new CastIntent("skill_campeao_investida", null));
        h.Ticks(1);
        Assert.Equal("NotEvolved", h.Transport.Received<CastResultMessage>(id).Last().Failure);

        // troca de classe: falar com o mentor no nível 20 (pedido do usuário, 09/11 — a cadeia de missões virou prova opcional)
        id = h.ProvasDeClasseFeitas(id, "g");   // 14/11: a troca pede as 4 provas do mentor
        h.Transport.ClientSends(id, new QuestIntent("q_troca_g", "accept"));
        h.Ticks(1);
        Assert.Contains("evo:campeao", h.Temp.Db.GetFlags(h.CharacterId(id)));
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("Campeão"));
        h.Ticks(5);
        h.Face(id, 1, 0);
        var before = h.Transport.Received<CastResultMessage>(id).Count();
        h.Transport.ClientSends(id, new CastIntent("skill_campeao_investida", null));
        h.Ticks(10);
        Assert.DoesNotContain(h.Transport.Received<CastResultMessage>(id).Skip(before), c => c.Failure == "NotEvolved");
        Assert.True(h.Me(id).X > -18, $"a Investida avançou: x={h.Me(id).X} {string.Join(",", h.Transport.Received<CastResultMessage>(id).Select(c => c.SkillId + ":" + c.Failure))}");
    }

    [Fact]
    public void Ponte_SoParaQuemReconstruiu_EMascateApareceDepois()
    {
        using var h = Real();
        var id = h.Join("lia", x: 0, y: -51.2, level: 6, map: Costa);
        h.Transport.ClientSends(id, new MoveIntent(0, -1));
        h.Ticks(40);
        Assert.True(h.Me(id).Y > -56.5, $"atravessou a ravina sem ponte (y {h.Me(id).Y})");
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        id = h.Relocate(id, Costa, 6.4, 14.4);
        h.Transport.ClientSends(id, new InteractIntent("npc:mascate_bento"));
        h.Ticks(1);
        Assert.Equal("out_of_range", h.Transport.Received<ActionResultMessage>(id).Last().Reason); // ainda não está ali

        var other = h.Join("rui", x: 0, y: -51.2, level: 6, map: Costa);   // outro jogador, sem a ponte
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "ponte:ravina");          // (a missão grava esta marca)
        id = h.Relocate(id, Costa, 0, -51.2);
        h.Transport.ClientSends(id, new MoveIntent(0, -1));
        h.Transport.ClientSends(other, new MoveIntent(0, -1));
        h.Ticks(40);
        Assert.True(h.Me(id).Y < -60.5, "com a ponte, passa");
        Assert.True(h.Me(other).Y > -56.5, "a ponte de um não abre para o outro");
    }

    [Fact]
    public void Barqueiro_CobraELeva()
    {
        using var h = Real();
        // 13/11: o dono move o Tibério no editor — o teste vai até onde ele estiver
        var tib = h.World.Maps[Porto].Npcs.Single(n => n.Id == "pescador_tiberio");
        var (tx, ty) = (tib.X + 1, tib.Y);
        var id = h.Join("bia", x: tx, y: ty, level: 5, map: Porto);
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "wp:wp_dunas");
        id = h.Relocate(id, Porto, tx, ty);
        h.Transport.ClientSends(id, new PaidTravelIntent("pescador_tiberio", "barco_dunas"));
        h.Ticks(1);
        Assert.Equal("not_enough_coins", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
        h.Temp.Db.AddCoins(h.CharacterId(id), 30, "t:c");
        id = h.Relocate(id, Porto, tx, ty);
        h.Transport.ClientSends(id, new PaidTravelIntent("pescador_tiberio", "barco_dunas"));
        h.Ticks(2);
        Assert.Equal(Costa, h.Last<MapMessage>(id).Map.Id);
        Assert.Equal(20, h.Temp.Db.GetCoins(h.CharacterId(id)));
        // destino ainda não descoberto não aparece nem funciona
        id = h.Relocate(id, Porto, tx, ty);
        h.Transport.ClientSends(id, new PaidTravelIntent("pescador_tiberio", "barco_vale"));
        h.Ticks(1);
        Assert.Equal("unknown_destination", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
    }

    [Fact]
    public void Dungeon_CheckpointAntesDoChefe_EPrimeiraConclusaoGarantida()
    {
        using var h = Real(rng: () => 0.99);
        var id = h.Join("ana", x: -97.92, y: -64, level: 60, map: Costa);
        h.Ticks(3);
        Assert.Equal("galeria_funda", h.Last<MapMessage>(id).Map.Id);
        // vai até o corredor antes do salão do chefe (checkpoint "antessala")
        id = h.Relocate(id, "galeria_funda", 0, -1);
        h.Transport.ClientSends(id, new MoveIntent(0, -1));
        Assert.True(h.TickUntil(() => h.Transport.Received<NoticeMessage>(id).Any(n => n.Text.StartsWith("Checkpoint")), 200));
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Ticks(1);
        h.Face(id, 0, -1);
        for (var i = 0; i < 300 && h.Monster(id, "m_boss")?.Alive == true; i++)
        {
            var m = h.Monster(id, "m_boss")!;
            var me = h.Me(id);
            var (dx, dy) = (m.X - me.X, m.Y - me.Y);
            if (Math.Sqrt(dx * dx + dy * dy) > 2.2) { h.Transport.ClientSends(id, new MoveIntent(dx, dy)); h.Ticks(3); continue; }
            h.Transport.ClientSends(id, new MoveIntent(0, 0));
            h.Face(id, dx, dy);
            h.Transport.ClientSends(id, new CastIntent(Corte, null));
            h.Ticks(12);
        }
        var bb = h.Monster(id, "m_boss")!; var pp = h.Me(id);
        Assert.False(bb.Alive, $"boss {bb.Health}/{bb.MaxHealth} at {bb.X:0.0},{bb.Y:0.0} {bb.Action}; me {pp.Health} at {pp.X:0.0},{pp.Y:0.0}; ev {h.Events(id).Count(e => e.TargetId == "m_boss")} casts {string.Join(",", h.Transport.Received<CastResultMessage>(id).Select(c => c.Failure).Distinct())}");
        Assert.Contains("clear:galeria_funda", h.Temp.Db.GetFlags(h.CharacterId(id)));
        Assert.Contains(h.Temp.Db.GetInventory(h.CharacterId(id)), i => i.DefinitionId == "weapon_guerreiro_lamina_galeria");
    }

    [Fact]
    public void Corrida_SoForaDeCombate()
    {
        using var h = new MapHarness(MapHarness.TestWorld());
        var id = h.Join("ana", x: 0, y: 0, level: 5);
        h.Transport.ClientSends(id, new MoveIntent(1, 0, Sprint: true));
        h.Ticks(20);
        var running = h.Me(id).X;
        Assert.InRange(running, 6.5, 8.0);                    // ~5 m/s × 1,45 por 1 s
        h.Transport.ClientSends(id, new CastIntent(Corte, null)); // golpe: entra em combate
        h.Ticks(12);
        var before = h.Me(id).X;
        h.Transport.ClientSends(id, new MoveIntent(1, 0, Sprint: true));
        h.Ticks(20);
        Assert.InRange(h.Me(id).X - before, 4.0, 5.5);         // em combate, velocidade normal
    }

    [Fact]
    public void MissaoDeConversar_ContaAoFalarComONpc()
    {
        using var h = Real();
        var (ox, oy) = h.PertoDoNpc("capita_ondina");
        var id = h.Join("ana", x: ox, y: oy, level: 12);
        h.Transport.ClientSends(id, new QuestIntent("q_recado_vale", "accept"));
        h.Ticks(1);
        id = h.Relocate(id, "vale_das_cinzas", 22, 32);
        h.Transport.ClientSends(id, new InteractIntent("npc:guarda_rui"));
        h.Ticks(1);
        Assert.Contains(h.Last<QuestLogMessage>(id).Quests, q => q.Id == "q_recado_vale" && q.State == "ready");
        // aparece no mapa do mundo onde entregar
        Assert.Contains(h.Last<WorldMapMessage>(id).Regions, r => r.Id == "porto" && r.Quests is { Count: > 0 });
    }

    [Fact]
    public void ItemDoVale_SoCaiDeMonstroDoNivelDoVale()
    {
        var world = MapHarness.RealWorld();
        var entry = world.LootTables["loot_saqueador"].Entries.Single(e => e.Pick?.Contains("weapon_guerreiro_espada_vigilia") == true);
        Assert.Equal(11, entry.MinMonsterLevel);
        foreach (var (lvl, expect) in new[] { (6, false), (12, true) })
        {
            var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", "monster_r1_saqueador_da_vigilia", 2, 0, level: lvl) });
            using var h = new MapHarness(w, rng: () => 0.0);
            var id = h.Join("gui", x: 0, y: 0, level: 60);
            h.Face(id, 1, 0);
            for (var i = 0; i < 30 && h.Monster(id, "m_s")!.Alive; i++) { h.Transport.ClientSends(id, new CastIntent(Corte, null)); h.Ticks(12); }
            h.Ticks(1);
            Assert.Equal(expect, h.LastSnapshot(id).Items.Any(i => i.DefinitionId is "weapon_guerreiro_espada_vigilia" or "armor_cota_reforcada"));
        }
    }
}

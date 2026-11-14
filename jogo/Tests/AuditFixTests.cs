using CinzasDaMare.Server.Combat;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>Correções dos itens 2 a 5 da auditoria da E8 (Docs/AUDITORIA_E8.md).</summary>
public class AuditFixTests
{
    private const string Raider = "monster_r1_saqueador_da_vigilia";

    [Fact]
    public void Item2_MarcaDoLeque_EAAreaDeAcerto_CorpoForaNaoApanha_CorpoEncostandoApanha()
    {
        // Saqueador em (0,0); "isca" à frente dentro do alcance faz ele atacar para +x.
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 0, 0) });
        using var h = new MapHarness(w);
        var isca = h.Join("isca", x: 2.6, y: 0, level: 30);
        var def = h.Content.Monsters[Raider];
        var talho = h.World.Behaviors[def.BehaviorId].Attacks.First(a => a.Id == "talho");
        var drawn = talho.Range + def.Radius + 0.3; // o que o servidor manda desenhar (conferido abaixo)
        var fora = h.Join("fora", x: drawn + GameServer.PlayerRadius + 0.08, y: 0.4, level: 30);
        var dentro = h.Join("dentro", x: drawn + GameServer.PlayerRadius - 0.12, y: -0.4, level: 30);

        Assert.True(h.TickUntil(() => h.Monster(isca, "m_s")!.Action == "windup:talho", 100));
        Assert.Equal(drawn, h.Monster(isca, "m_s")!.AimRadius, 6);
        Assert.True(h.TickUntil(() => h.Monster(isca, "m_s")!.Action.StartsWith("recover"), 60));

        bool Hit(int id) => h.Events(isca).Any(e => e.AttackerId == "m_s" && e.TargetId == h.CharacterId(id) && e.Damage > 0);
        Assert.True(Hit(isca));
        Assert.True(Hit(dentro), "corpo encostando na marca deve apanhar");
        Assert.False(Hit(fora), "corpo fora da marca não pode apanhar");
    }

    [Fact]
    public void Item2_FaixaDoBote_MostraALarguraEOAlcanceReais()
    {
        var crab = "monster_r1_caranguejo_de_cal";
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("c", crab, 3.5, 0) }));
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_c")!.Action == "windup:bote", 100));
        var def = h.Content.Monsters[crab];
        var bote = h.World.Behaviors[def.BehaviorId].Attacks.First(a => a.Id == "bote");
        var half = bote.Radius + def.Radius * 0.5;  // mesma conta do acerto durante o bote
        var m = h.Monster(id, "m_c")!;
        Assert.Equal(-2 * half, m.AimArc, 6);
        Assert.Equal(bote.LungeM + half, m.AimRadius, 6);
    }

    [Fact]
    public void Item3_SairMortoAntesDeRenascer_VoltaNoAcampamento_ComVidaCheia()
    {
        var db = new TempDb();
        try
        {
            string token;
            using (var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 2.0, 0, level: 30) }), sharedDb: db))
            {
                token = h.Precreate("ana", x: 0, y: 0);
                var id = h.Login(token);
                Assert.True(h.TickUntil(() => !h.Me(id).Alive, 600));
                h.Transport.ClientDisconnects(id);   // fecha o jogo antes dos 5 s do renascer
                h.Ticks(1);
            }
            using (var h2 = new MapHarness(MapHarness.TestWorld(), sharedDb: db))
            {
                var id = h2.Login(token);
                h2.Ticks(2);
                var me = h2.Me(id);
                Assert.True(me.Alive);
                Assert.Equal((h2.World.Map.SpawnX, h2.World.Map.SpawnY), (me.X, me.Y));
                Assert.Equal(me.MaxHealth, me.Health, 6);
            }
        }
        finally { db.Dispose(); }
    }

    [Fact]
    public void Item5_QuebrarAPreparacao_PoeOAtaqueEmRecarga()
    {
        // Saqueador perto o bastante para o talho (que tem recarga curta de 300 ms + recuperação no ciclo normal).
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 2.0, 0) });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0, level: 30);
        h.Face(id, 1, 0);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action == "windup:talho", 100));
        h.Transport.ClientSends(id, new CastIntent(TestContent.Golpe, null));
        Assert.True(h.TickUntil(() => h.Events(id).Any(e => e.Staggered), 20));
        var brokenAt = h.Server.CurrentTick;
        Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action.StartsWith("recover"), 4)); // snapshot já mostra a quebra
        // depois da quebra (800 ms) o talho só pode voltar após a recarga dele (300 ms): nada de talho antes de 1100 ms
        Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action == "windup:talho", 200));
        var ms = (h.Server.CurrentTick - brokenAt) * GameServer.TickMs;
        Assert.True(ms >= 800 + 300 - GameServer.TickMs * 2, $"voltou a preparar o talho em {ms} ms");
    }

    [Fact]
    public void Item4_ArmasAchadasSuperamAInicial_EOPremioDoChefeSuperaTodas()
    {
        var b = TestContent.Load();
        double Dps(string itemId, string cls, string basic)
        {
            var item = b.Items[itemId];
            var c = b.Classes[cls];
            var skill = b.Skills[basic];
            var power = CombatFormulas.PhysicalPower(item.BaseStats["weapon_power"], c.StartingPrimary, 5);
            var count = item.BaseStats.TryGetValue("projectile_count", out var n) ? n : 1;
            var cycle = (skill.CastMs + skill.RecoveryMs) * item.BaseStats["attack_interval_ms"] / 1000.0;
            return power * skill.Coefficient * count / (cycle / 1000.0);
        }
        var g = "skill_guerreiro_corte";
        var m = "skill_mago_seta_de_brasa";
        var sword = Dps("weapon_guerreiro_sword_p1", "guerreiro", g);
        foreach (var found in new[] { "weapon_guerreiro_axe_p2", "weapon_guerreiro_spear_p2" })
            Assert.InRange(Dps(found, "guerreiro", g), sword * 1.05, Dps("weapon_guerreiro_malho_capataz", "guerreiro", g) - 1);
        var staff = Dps("staff_mago_aprendiz", "mago", m);
        foreach (var found in new[] { "staff_mago_brasa", "staff_mago_vara_gemea" })
            Assert.InRange(Dps(found, "mago", m), staff * 1.05, Dps("staff_mago_cetro_capataz", "mago", m) - 1);
    }

    [Theory]
    [InlineData("monster_r1_caranguejo_de_cal")]
    [InlineData("monster_r1_saqueador_da_vigilia")]
    public void MonstroEncostadoNoJogador_ContinuaAtacando(string monster)
    {
        // Achado na verificação (08/11): encostado, a distância entre corpos dava 0 ou -0,000…1 e o teste de alcance
        // mínimo recusava todos os ataques: o caranguejo mordia uma vez e ficava parado ao lado do jogador para sempre.
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("s", monster, 1.5, 0) }));
        var id = h.Join("gui", x: 0, y: 0, level: 40);
        h.Ticks(200); // 10 s parado ao lado
        Assert.True(h.Events(id).Count(e => e.AttackerId == "m_s") >= 3, $"{monster} atacou só {h.Events(id).Count(e => e.AttackerId == "m_s")} vez(es) em 10 s");
    }

    [Fact]
    public void Sobra1_MarcaDaExplosao_ChegaAoAliadoGuerreiro_ComORaioDoCetro()
    {
        const string explosao = "skill_mago_explosao_de_cinzas";
        const string cetro = "staff_mago_cetro_capataz";
        using var h = new MapHarness(MapHarness.TestWorld());
        var mia = h.Join("mia", "mago", 0, 0, level: 30);
        var gui = h.Join("gui", "guerreiro", 2, 0, level: 30);
        var skill = h.Content.Skills[explosao];
        var bonus = h.Content.Items[cetro].BaseStats["aoe_radius_bonus"];

        // o catálogo leva o bônus (o próprio Mago desenha a mira com ele)
        var info = h.Transport.Received<CatalogMessage>(mia).Last().Items.Single(i => i.Id == cetro);
        Assert.Equal(bonus, info.AoeRadiusBonus, 6);

        PlayerView MageSeenBy(int viewer) => h.LastSnapshot(viewer).Players.Single(p => p.CharacterId == h.CharacterId(mia));

        // sem o cetro: raio da habilidade
        h.Transport.ClientSends(mia, new CastIntent(explosao, null, 0, -6));
        Assert.True(h.TickUntil(() => MageSeenBy(gui).Action.StartsWith("windup:" + explosao), 10));
        Assert.Equal(skill.Radius, MageSeenBy(gui).AimRadius, 6);
        Assert.True(h.TickUntil(() => MageSeenBy(gui).Action.Length == 0, 80));

        // com o cetro: o aliado Guerreiro recebe o raio maior, que é o mesmo do acerto
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(mia), cetro, "comum", 1, "t_cetro")!;
        h.Transport.ClientSends(mia, new EquipIntent(item));
        h.Ticks((int)(skill.CooldownMs / GameServer.TickMs) + 2); // recarga da Explosão
        h.Transport.ClientSends(mia, new CastIntent(explosao, null, 0, -6));
        Assert.True(h.TickUntil(() => MageSeenBy(gui).Action.StartsWith("windup:" + explosao), 10));
        Assert.Equal(skill.Radius + bonus, MageSeenBy(gui).AimRadius, 6);
    }
}

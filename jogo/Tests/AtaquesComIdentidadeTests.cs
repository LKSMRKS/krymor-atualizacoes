using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>
/// Rodada de feedback 2: ataques com identidade — estocadas em sequência, golpe atrasado (souls), bate-e-volta,
/// areia em leque, raio do olho alado e morteiro que cai no jogador.
/// </summary>
public class AtaquesComIdentidadeTests
{
    private static int HitsFrom(MapHarness h, int id, string monster, string attack)
        => h.Events(id).Count(e => e.AttackerId == monster && e.SkillId == "monster_" + attack && e.Damage > 0);

    [Fact]
    public void Goblin_EstocadasEmSequencia_AcertamVariasVezesNoMesmoAtaque()
    {
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("g", "monster_r1_goblin_saqueador", 2.0, 0) }));
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_g")!.Action == "active:punhaladas", 120));
        var startX = h.Monster(id, "m_g")!.X;
        Assert.True(h.TickUntil(() => h.Monster(id, "m_g")!.Action.StartsWith("recover"), 60));
        Assert.True(HitsFrom(h, id, "m_g", "punhaladas") >= 2, "as estocadas acertam mais de uma vez");
        Assert.True(h.Monster(id, "m_g")!.X <= startX + 1e-6, "avança em direção ao jogador (que está em x menor)");
    }

    [Fact]
    public void Saqueador_GolpeAtrasado_SeguraAPreparacaoCheiaAntesDeSoltar()
    {
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("s", "monster_r1_saqueador_da_vigilia", 2.6, 0) }));
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_s")!.Action == "windup:talho", 100));
        // 750 ms de preparação + até 550 de atraso (rng do teste = 0,99): a barra enche e fica cheia ainda em preparação
        h.Ticks(800 / GameServer.TickMs);
        var m = h.Monster(id, "m_s")!;
        Assert.Equal("windup:talho", m.Action);
        Assert.Equal(1.0, m.ActionT, 6);
        Assert.DoesNotContain(h.Events(id), e => e.AttackerId == "m_s" && e.Damage > 0);
    }

    [Fact]
    public void BateEVolta_RecuaDepoisDoGolpe()
    {
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("g", "monster_r1_goblin_saqueador", 2.0, 0) }));
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        // espera um golpe com recuo (punhalada ou punhalada dupla)
        Assert.True(h.TickUntil(() => h.Monster(id, "m_g")!.Action is "recover:punhalada" or "recover:punhalada_dupla", 400));
        var before = h.Monster(id, "m_g")!.X;
        h.Ticks(8);
        Assert.True(h.Monster(id, "m_g")!.X > before + 1.0, "recuou para longe do jogador");
    }

    [Fact]
    public void Caranguejinho_JogaAreiaEmLeque()
    {
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("c", "monster_r0_caranguejinho", 4.5, 0) }));
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        Assert.True(h.TickUntil(() => h.LastSnapshot(id).Projectiles!.Count(p => p.Kind == "monster_areia") == 3, 120));
        var dirs = h.LastSnapshot(id).Projectiles!.Where(p => p.Kind == "monster_areia").Select(p => Math.Atan2(p.DirY, p.DirX)).ToList();
        Assert.True(dirs.Max() - dirs.Min() > 0.5, "os grãos saem abertos em leque");
    }

    [Fact]
    public void OlhoAlado_Raio_AcertaEmLinhaReta_ENaoQuemEstaAoLado()
    {
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("o", "monster_r2_olho_alado", 7.5, 0) }));
        var alvo = h.Join("alvo", x: 0, y: 0, level: 30);
        var lado = h.Join("lado", x: 4, y: 2.2, level: 30);
        Assert.True(h.TickUntil(() => h.Monster(alvo, "m_o")!.Action == "windup:laser", 100));
        var m = h.Monster(alvo, "m_o")!;
        Assert.Equal("beam", m.AimKind);
        Assert.True(m.AimArc < 0, "marca em faixa");
        Assert.True(h.TickUntil(() => h.Monster(alvo, "m_o")!.Action.StartsWith("recover"), 80));
        Assert.True(HitsFrom(h, alvo, "m_o", "laser") >= 2, "o raio queima mais de uma vez quem fica na linha");
        Assert.Equal(0, h.Events(alvo).Count(e => e.AttackerId == "m_o" && e.TargetId == h.CharacterId(lado) && e.Damage > 0));
    }

    [Fact]
    public void Morteiro_CaiSobreOJogador()
    {
        // capataz a 9 m: as pancadas em marcha começam na frente dele; a brasa do acólito cai em cima do jogador
        using var h = new MapHarness(MapHarness.TestWorld(new[] { MapHarness.Spawn("a", "monster_r1_acolito_das_cinzas", 9, 0) }));
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_a")!.Action == "windup:brasa", 600));
        var m = h.Monster(id, "m_a")!;
        var me = h.Me(id);
        Assert.True(Math.Abs(m.AimX - me.X) < 0.8 && Math.Abs(m.AimY - me.Y) < 0.8, $"marca em ({m.AimX:0.0},{m.AimY:0.0}), jogador em ({me.X:0.0},{me.Y:0.0})");
    }
}

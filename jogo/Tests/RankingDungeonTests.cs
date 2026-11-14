using System.Linq;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>11/11 (dungeon contra o tempo): derrubar o chefe final grava o recorde pessoal e mostra o ranking.</summary>
public class RankingDungeonTests
{
    [Fact]
    public void ChefeFinal_GravaRecordeEMostraRanking()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var id = h.Join("corredor", level: 12);
        h.Transport.ClientSends(id, new GodIntent("god"));
        h.Transport.ClientSends(id, new GodIntent("travel", "galeria_funda", X: 0, Y: -18));
        h.Ticks(40);
        h.Transport.ClientSends(id, new GodIntent("kill"));
        h.Ticks(3);
        var notes = h.Transport.Received<NoticeMessage>(id).Select(n => n.Text).ToList();
        Assert.Contains(notes, t => t.Contains("recorde pessoal"));
        Assert.Contains(notes, t => t.StartsWith("🏆") && t.Contains("corredor"));
        Assert.Contains(h.Temp.Db.GetFlags(h.CharacterId(id)), f => f.StartsWith("recorde:galeria_funda:"));
    }
}

public class SantuarioTests
{
    [Fact]
    public void Admin_ViajaParaOSantuario_ERecebeOMapa()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var id = h.Join("adm", level: 30);
        h.Transport.ClientSends(id, new GodIntent("god"));
        h.Transport.ClientSends(id, new GodIntent("travel", "santuario_ceu", X: 0, Y: -22));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        h.Ticks(60);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"60 ticks levaram {sw.ElapsedMilliseconds} ms");
        Assert.Equal("santuario_ceu", h.Transport.Received<MapMessage>(id).Last().Map.Id);
    }
}

public class DueloNpcTests
{
    [Fact]
    public void Cael_SeRebela_EsperaNaCosta_EVencerDeixaProntaParaEntregar()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var id = h.Join("duelista", x: -16.5, y: -15.0, level: 8);
        h.Transport.ClientSends(id, new QuestIntent("q_duelo_cael", "accept"));
        h.Ticks(1);
        Assert.Contains(h.Last<QuestLogMessage>(id).Quests, q => q.Id == "q_duelo_cael");
        h.Transport.ClientSends(id, new GodIntent("god"));
        h.Transport.ClientSends(id, new GodIntent("travel", "costa_do_cal", X: 6, Y: 96));
        h.Ticks(3);
        // 13/11: ele espera num ponto sorteado; a bússola mostra a área (até 12 m do ponto)
        var e = h.Last<QuestLogMessage>(id).Quests.Single(q => q.Id == "q_duelo_cael");
        h.Transport.ClientSends(id, new GodIntent("travel", "costa_do_cal", X: e.X, Y: e.Y));
        h.Ticks(3);
        Assert.Contains(h.LastSnapshot(id).Monsters, m => m.DefinitionId == "monster_cael_rebelde");
        h.Transport.ClientSends(id, new GodIntent("kill"));
        h.Ticks(3);
        Assert.Contains(h.Last<QuestLogMessage>(id).Quests, q => q.Id == "q_duelo_cael" && q.State == "ready");
        // morto, não renasce (antes renascia sem parar no mesmo lugar)
        h.Ticks(100);
        Assert.DoesNotContain(h.LastSnapshot(id).Monsters, m => m.DefinitionId == "monster_cael_rebelde" && m.Health > 0);
    }
}

public class GargulaPoleiroTests
{
    [Fact]
    public void Gargula_EsperaNoPoleiro_MergulhaQuandoChegaPerto_EDepoisLutaNoChao()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("g", "monster_cal_gargulito", 0, 0, level: 5) with { Perch = 3.3 } });
        using var h = new MapHarness(w);
        var id = h.Join("ana", x: 0, y: 20, level: 5);
        h.Ticks(20);
        var m = h.LastSnapshot(id).Monsters.Single();
        Assert.Equal("perch", m.Action);
        Assert.Equal(3.3, m.Lift, 2);
        id = h.Relocate(id, w.Maps.Keys.First(), 0, 6);
        h.Ticks(3);
        Assert.Equal("dive", h.LastSnapshot(id).Monsters.Single().Action);
        h.Ticks(30);
        var after = h.LastSnapshot(id).Monsters.Single();
        Assert.NotEqual("perch", after.Action);
        Assert.Equal(0, after.Lift, 2);
        Assert.True(Math.Sqrt(after.X * after.X + (after.Y - 6) * (after.Y - 6)) < 2.5);
    }
}

public class DeusAlvoTests
{
    [Fact]
    public void Admin_EscolheOAmigo_EDaNivelEItemParaEle()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var adm = h.Join("adm", level: 30);
        var amigo = h.Join("alice", level: 3);
        h.Ticks(2);
        h.Transport.ClientSends(adm, new GodIntent("target", Extra: "alice"));
        h.Transport.ClientSends(adm, new GodIntent("level", Amount: 12));
        h.Ticks(2);
        Assert.Equal(12, h.Last<ProgressMessage>(amigo).Level);
        h.Transport.ClientSends(adm, new GodIntent("goto"));
        h.Ticks(2);
        var a = h.LastSnapshot(adm).Players;
        var pa = a.First(p => p.Name == "adm"); var pb = a.First(p => p.Name == "alice");
        Assert.True(Math.Abs(pa.X - pb.X) < 3 && Math.Abs(pa.Y - pb.Y) < 3);
    }
}

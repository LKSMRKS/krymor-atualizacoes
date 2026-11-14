using System.Linq;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>09/11: fôlego (correr, esquivar e atacar gastam), caranguejos do lote 2 com defesa/escavação, Pip no slot de companheiro.</summary>
public class FolegoECaranguejosTests
{
    private static WorldContent Lively(params MapSpawn[] spawns)
    {
        var w = MapHarness.TestWorld(spawns);
        w.LivelyMonsters = true;
        return w;
    }

    [Fact]
    public void Esquiva_GastaFolego_EVoltaSozinho()
    {
        using var h = new MapHarness(Lively(), rng: () => 0.99);
        var id = h.Join("ana", x: 0, y: 10, level: 5);
        h.Ticks(2);
        Assert.Equal(100, h.Me(id).Stamina, 0);
        h.Transport.ClientSends(id, new DodgeIntent(1, 0));
        h.Ticks(2);
        Assert.InRange(h.Me(id).Stamina, 70, 80);
        h.Ticks(80);   // 4 s parado
        Assert.Equal(100, h.Me(id).Stamina, 0);
    }

    [Fact]
    public void Correr_GastaFolego_ESemFolegoNaoCorreNemEsquiva()
    {
        using var h = new MapHarness(Lively(), rng: () => 0.99);
        var id = h.Join("ana", x: 0, y: 10, level: 5);
        h.Ticks(2);
        h.Transport.ClientSends(id, new MoveIntent(1, 0, Sprint: true));
        h.Ticks(20);
        var after1s = h.Me(id).Stamina;
        Assert.InRange(after1s, 80, 92);
        Assert.True(h.TickUntil(() => h.Me(id).Stamina <= 0.1, 300), "esvazia correndo");
        h.Transport.ClientSends(id, new MoveIntent(0, 0));
        h.Transport.ClientSends(id, new DodgeIntent(1, 0));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), r => r.Action == "dodge" && !r.Success && r.Reason == "no_stamina");
    }

    [Fact]
    public void CaranguejoDePedra_FechadoNaCarapaca_QuaseNaoApanha()
    {
        var w = MapHarness.RealWorld();
        var b = w.Behaviors["crab_stone"];
        var spikes = b.Attacks.First(a => a.Id == "chuva_de_espinhos");
        Assert.True(spikes.SelfGuardMult <= 0.15);
        Assert.Equal("projectile", spikes.Shape);
        var hermit = w.Behaviors["crab_hermit"].Attacks;
        Assert.Contains(hermit, a => a.Id == "barril_explosivo" && a.AtTarget && a.Radius >= 2);
        Assert.Contains(hermit, a => a.Id == "investida_barril" && a.GuardFrontal);
        Assert.Contains(w.Behaviors["crab_digger"].Attacks, a => a.Burrow);
    }

    [Fact]
    public void Territorios_DoMaisFracoAoMaisForte_ETiranoNoSul()
    {
        var costa = MapHarness.RealWorld().Maps["costa_do_cal"];
        int Lvl(string mon) => costa.Spawns.Where(s => s.Monster == mon).Select(s => s.Level).DefaultIfEmpty(0).Max();
        Assert.True(Lvl("monster_r1_caranguejo_de_pedra") < Lvl("monster_r1_caranguejo_ermitao"));
        Assert.True(Lvl("monster_r1_caranguejo_ermitao") < Lvl("monster_r1_caranguejo_escavador"));
        var tirano = costa.Spawns.Single(s => s.Monster == "monster_r1_tirano_da_mare");
        Assert.True(tirano.Y < -70, "Tirano na enseada sul");
        // círculo seguro no portão: quem chega do Porto (0, 96) já está dentro
        Assert.True(Math.Sqrt(Math.Pow(costa.SpawnX, 2) + Math.Pow(costa.SpawnY - 96, 2)) <= costa.SafeRadius);
    }

    [Fact]
    public void Escavador_AndaPorBaixoAteOAlvo_ENaoApanhaEnterrado()
    {
        var w = Lively(MapHarness.Spawn("e", "monster_r1_caranguejo_escavador", 0, -8, level: 9, group: "solo"));
        using var h = new MapHarness(w, rng: () => 0.99);
        var id = h.Join("ana", x: 0, y: 30, level: 9);
        h.Ticks(1);
        var e0 = h.Monster(id, "m_e")!;
        id = h.Relocate(id, "teste", e0.X, e0.Y + 8);
        var seen = new System.Collections.Generic.List<string>();
        var ok = h.TickUntil(() => { var mm = h.Monster(id, "m_e"); seen.Add(mm is null ? "null" : $"{mm.Action}@{mm.X:0},{mm.Y:0}"); return mm?.Action == "windup:emergir"; }, 600);
        Assert.True(ok, "usa o ataque de escavar: " + string.Join(" ", seen.Distinct().Take(20)));
        var start = h.Monster(id, "m_e")!;
        h.Ticks(12);
        var mid = h.Monster(id, "m_e")!;
        var me = h.Me(id);
        Assert.True(Math.Abs(mid.Y - me.Y) < Math.Abs(start.Y - me.Y), "chega mais perto por baixo da terra");
    }
}

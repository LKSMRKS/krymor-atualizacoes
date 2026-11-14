using CinzasDaMare.Shared.Net;
using System.Linq;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11 (pedidos das 15:50/15:53): física de casas e acampamento, bigorna, loja do Brás e missões do chefe goblin.</summary>
public class Pedidos1550Tests
{
    private static MapGeometry Geo(string map) => new(MapHarness.RealWorld().Maps[map]);

    [Fact]
    public void SalaoVazado_TemParedes_EPortaAberta()
    {
        var w = MapHarness.RealWorld();
        var p = w.Maps["costa_do_cal"].Props.First(x => x.Kind == "ruina_salao");
        var g = Geo("costa_do_cal");
        var a = p.Yaw * System.Math.PI / 180;
        (double, double) W(double lx, double ly) => (p.X + lx * System.Math.Cos(a) + ly * System.Math.Sin(a), p.Y - lx * System.Math.Sin(a) + ly * System.Math.Cos(a));
        var (ex, ey) = W(5.35, 2.0);   // parede leste
        Assert.False(g.IsFree(ex, ey, 0.3));
        var (cx, cy) = W(0, 0);         // meio do salão é livre (antes era um círculo sólido)
        Assert.True(g.IsFree(cx, cy, 0.4));
        var (dx, dy) = W(-0.15, 4.0);   // porta
        Assert.True(g.IsFree(dx, dy, 0.4));
    }

    [Fact]
    public void AcampamentoGoblin_TendaEPalicada_SaoSolidas()
    {
        var w = MapHarness.RealWorld();
        var g = Geo("costa_do_cal");
        foreach (var p in w.Maps["costa_do_cal"].Props.Where(x => x.Kind is "tenda_goblin" or "palicada"))
            Assert.False(g.IsFree(p.X, p.Y, 0.3), $"{p.Kind} em {p.X},{p.Y}");
    }

    [Fact]
    public void BigornaDoBras_ESolida()
    {
        var bras = MapHarness.RealWorld().Maps["porto_das_cinzas"].Npcs.First(n => n.Id == "ferreiro_bras");
        var (x, y) = MapGeometry.BigornaDe(bras);
        Assert.False(Geo("porto_das_cinzas").IsFree(x, y, 0.2));
    }

    [Fact]
    public void LojaDoBras_ConjuntoNovoCustaMaisQueAVitrine_EVendaNaoMuda()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        Assert.True(h.Server.ShopPrice("arm_guerreiro_peitoral") > 1100);
        // 14/11: preços pela régua (nível × força do item): a peça de conjunto do nível 8 vale mais que a vitrine do nível 5
        Assert.True(h.Server.ShopPrice("arm_guerreiro_peitoral") > h.Server.ShopPrice("vitrine_couraca_escarlate"));
        Assert.True(h.Server.ShopPrice("armadura_acolchoada_luvas") < 100);
        var shop = MapHarness.RealWorld().Shops["loja_ferreiro"].Items;
        Assert.Contains("armadura_acolchoada_luvas", shop);
        Assert.Contains("armadura_couro_bronze_botas", shop);
    }

    [Fact]
    public void ChefeGoblin_TemMissaoNoNivel6_EMenosGoblinsEmVolta()
    {
        var w = MapHarness.RealWorld();
        Assert.Contains(w.Quests.Values, q => q.MinLevel == 6 && q.Targets.Contains("monster_r1_goblin_chefe"));
        var costa = w.Maps["costa_do_cal"];
        var chefe = costa.Spawns.First(s => s.Monster == "monster_r1_goblin_chefe");
        Assert.True(costa.Spawns.Count(s => s != chefe && System.Math.Abs(s.X - chefe.X) < 8 && System.Math.Abs(s.Y - chefe.Y) < 8) <= 2);
    }

    [Fact]
    public void PortaBrava_DaUmTapa_MasNaoMata_ETrancadaSoFala()
    {
        // 13/11: o dono pode tirar a porta brava do Porto no editor — o teste monta o mapa de teste com as duas portas
        var brava = new MapObject("p_brava", "porta", 4, 10, "Porta") { Text = "PÁ!", GrantFlag = "porta:brava" };
        var tranc = new MapObject("p_tranc", "porta", -4, 10, "Porta") { Text = "Trancada. Ninguém atende." };
        var t = MapHarness.TestWorld();
        var w = new WorldContent(t.Map with { Id = "porto_das_cinzas", SafeRadius = 0, Objects = new[] { brava, tranc } }, t.Behaviors, t.LootTables);
        using var h = new MapHarness(w);
        var a = h.Join("ze", level: 5, map: "porto_das_cinzas", x: brava.X, y: brava.Y - 0.6);
        h.Ticks(2);
        var antes = h.Me(a).Health;
        h.Transport.ClientSends(a, new CinzasDaMare.Shared.Net.InteractIntent("obj:" + brava.Id));
        h.Ticks(2);
        Assert.True(h.Me(a).Health < antes && h.Me(a).Health > 0);
        a = h.Relocate(a, "porto_das_cinzas", tranc.X, tranc.Y - 0.6);
        h.Ticks(2);
        var vida = h.Me(a).Health;
        h.Transport.ClientSends(a, new CinzasDaMare.Shared.Net.InteractIntent("obj:" + tranc.Id));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<CinzasDaMare.Shared.Net.NoticeMessage>(a), n => n.Text == tranc.Text);
        Assert.True(h.Me(a).Health >= vida);
    }
}

/// <summary>12/11 (17:47): personagens recorrentes — Capitão Fallen (rifle, barba ruiva) e Vacho, o moleque do barril.</summary>
public class RecorrentesTests
{
    [Fact]
    public void FallenEVacho_AparecemEmVariosMapas_ComCadeiaDeMarcas_ELugarLivre()
    {
        var w = MapHarness.RealWorld();
        var fallen = w.Maps.Values.SelectMany(m => m.Npcs.Select(n => (Map: m, Npc: n))).Where(x => x.Npc.Id.StartsWith("fallen_")).ToList();
        var vacho = w.Maps.Values.SelectMany(m => m.Npcs.Select(n => (Map: m, Npc: n))).Where(x => x.Npc.Id.StartsWith("vacho_")).ToList();
        Assert.True(fallen.Count >= 4 && vacho.Count >= 4);
        foreach (var (map, npc) in fallen.Concat(vacho))
        {
            Assert.True(new MapGeometry(map).IsFree(npc.X, npc.Y, 0.4), $"{npc.Id} dentro de obstáculo em {map.Id}");
            // a marca que faz o NPC aparecer vem de alguma missão
            if (npc.RequiresFlag is { } f) Assert.Contains(w.Quests.Values, q => q.RewardFlags.Contains(f));
            Assert.Contains(w.Quests.Values, q => q.Giver == npc.Id);
        }
        Assert.Equal(7, w.Quests["q_cais_paginas"].MinLevel);
    }
}

/// <summary>12/11 (17:47): a 3ª classe (Leão) dá um golpe próprio para cada classe.</summary>
public class TerceiraClasseTests
{
    [Fact]
    public void Leao_GanhaGolpeProprio_AoEntrar()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("leon", level: 55, map: "porto_das_cinzas", x: 0, y: -9.5);
        h.Temp.Db.TryAddFlag(h.CharacterId(a), "classe3");
        a = h.Relocate(a, "porto_das_cinzas", 0, -9.5);
        h.Ticks(2);
        Assert.Contains("tomo:skill_leao_rugido_supremo", h.Temp.Db.GetFlags(h.CharacterId(a)));
    }
}

/// <summary>12/11 (18:59): penalidade por classe, castelos com selo caro, chefes de campo e moradores dos reinos.</summary>
public class FimDeJogoTests
{
    [Fact]
    public void Morte_NaSegundaClasse_Perde25PorCento()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("g", "monster_r1_goblin_saqueador", 0, 2, level: 40, group: "solo") });
        w.LivelyMonsters = true;
        using var h = new MapHarness(w, rng: () => 0.99);
        var id = h.Join("ana", x: 0, y: -1, level: 18);
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "evo:campeao");
        var g = h.Monster(id, "m_g") ?? h.LastSnapshot(id).Monsters.First();
        id = h.Relocate(id, "teste", g.X, g.Y - 1.5);
        Assert.True(h.TickUntil(() => !h.Me(id).Alive, 600));
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("morreu") && (n.Text.Contains("25%") || n.Text.Contains("sem experiência")));
    }

    [Fact]
    public void Castelo_SoAbreComOSelo_EOSeloSomeAoEntrar()
    {
        var w = MapHarness.RealWorld();
        var portao = w.Maps["vespera"].Portals.First(p => p.Id == "castelo_vespera_portao");
        Assert.Equal("selo_castelo_vespera", portao.RequiresItem);
        using var h = new MapHarness(w);
        var a = h.Join("lord", level: 45, map: "vespera", x: portao.X + 4, y: portao.Y);
        h.Ticks(2);
        a = h.Relocate(a, "vespera", portao.X, portao.Y);
        h.Ticks(3);
        Assert.Equal("vespera", h.Last<MapMessage>(a).Map.Id);   // sem selo: fica
        h.Temp.Db.AddCoins(h.CharacterId(a), 60000, "t:selo");
        Assert.True(h.Temp.Db.TryBuy("t1", h.CharacterId(a), "selo_castelo_vespera", 1, 60000).Success);
        a = h.Relocate(a, "vespera", portao.X + 4, portao.Y);
        h.Ticks(2);
        a = h.Relocate(a, "vespera", portao.X, portao.Y);
        h.Ticks(3);
        Assert.Equal("castelo_vespera", h.Last<MapMessage>(a).Map.Id);
        Assert.DoesNotContain(h.Temp.Db.GetInventory(h.CharacterId(a)), i => i.DefinitionId == "selo_castelo_vespera");
    }

    [Fact]
    public void ChefesDeCampo_ERainha_EMoradoresDosReinos()
    {
        var w = MapHarness.RealWorld();
        foreach (var (mapa, chefe) in new[] { ("canions_suplicantes", "monster_canions_rei_dos_canions"), ("brejo_da_viuva", "monster_brejo_viuva_negra"),
                     ("vespera", "monster_vespera_prefeito_sem_rosto"), ("acropole_das_cinzas", "monster_acropole_tita_bronze"), ("costa_do_cal", "monster_r1_rainha_das_cascas") })
            Assert.Contains(w.Maps[mapa].Spawns, s => s.Monster == chefe);
        foreach (var r in new[] { "reino_lua", "reino_transilvania", "reino_inferno", "reino_celeste", "floresta_oculta", "santuario_arena" })
        {
            Assert.NotEmpty(w.Maps[r].Npcs);
            Assert.All(w.Maps[r].Npcs, n => Assert.True(new MapGeometry(w.Maps[r]).IsFree(n.X, n.Y, 0.4), n.Id));
        }
    }
}

/// <summary>12/11 (19:16): modo teste — preparo por região (nível, conjunto, arma, viagem) e refazer missão.</summary>
public class ModoTesteTests
{
    [Fact]
    public void Preparo_DaNivelEquipamentoEViagem_ERefazMissao()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("teste", level: 1);
        h.Ticks(2);
        h.Transport.ClientSends(a, new GodIntent("preset", Arg: "vale13"));
        h.Ticks(3);
        Assert.Equal(13, h.Me(a).Level);
        Assert.Equal("vale_das_cinzas", h.Last<MapMessage>(a).Map.Id);
        Assert.True(h.Temp.Db.GetEquipment(h.CharacterId(a)).Count >= 3, "vestiu o conjunto e a arma");
        // missão concluída volta a poder ser feita
        h.Temp.Db.TryAcceptQuest(h.CharacterId(a), "q_vale_raiz", false);
        h.Transport.ClientSends(a, new GodIntent("quest_reset", Arg: "q_vale_raiz"));
        h.Ticks(2);
        Assert.DoesNotContain(h.Temp.Db.GetQuests(h.CharacterId(a)), q => q.QuestId == "q_vale_raiz");
    }
}

/// <summary>12/11 (19:38): x1 entre membros do grupo, natureza dos monstros e montarias por nível.</summary>
public class X1ETiposTests
{
    [Fact]
    public void X1_DesafioAceito_AliMesmo_ENinguemMorre()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("ana", level: 20, map: "costa_do_cal", x: 0, y: 10);
        var b = h.Join("bia", level: 20, map: "costa_do_cal", x: 5, y: 10);   // a anda até ela (andar vira o rosto)
        h.Ticks(2);
        h.Transport.ClientSends(a, new PartyIntent("invite", "bia"));
        h.Ticks(1);
        h.Transport.ClientSends(b, new PartyIntent("accept"));
        h.Ticks(1);
        h.Transport.ClientSends(a, new PartyIntent("duelo", "bia"));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<PartyInviteMessage>(b), i => i.Kind == "duelo");
        h.Transport.ClientSends(b, new PartyIntent("duelo_aceitar"));
        h.Ticks(2);
        // 14/11: o x1 é ali mesmo (não leva para a arena)
        Assert.Equal("costa_do_cal", h.Last<MapMessage>(a).Map.Id);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.StartsWith("X1:"));
        // a bate em b até alguém vencer (Giro / golpe básico em cone)
        for (var i = 0; i < 400 && !h.Transport.Received<NoticeMessage>(a).Any(n => n.Text.Contains("venceu")); i++)
        {
            var mb = h.Me(b); var ma = h.Me(a);
            var dx = mb.X - ma.X; var dy = mb.Y - ma.Y; var d = Math.Sqrt(dx * dx + dy * dy);
            if (d > 1.6) { h.Transport.ClientSends(a, new MoveIntent(dx / d, dy / d)); h.Ticks(3); continue; }
            h.Transport.ClientSends(a, new MoveIntent(0, 0));
            h.Face(a, mb.X, mb.Y);
            h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_corte", null));
            h.Ticks(8);
        }
        var casts = h.Transport.Received<CastResultMessage>(a).Select(c => c.Failure ?? "ok").GroupBy(x => x).Select(g => g.Key + "=" + g.Count());
        Assert.True(h.Transport.Received<NoticeMessage>(a).Any(n => n.Text.Contains("ana venceu")), string.Join(",", casts) + $" ev={h.Transport.Received<CombatEvent>(a).Count(e => e.TargetId == h.Me(b).CharacterId)} b={h.Me(b).X:0.0},{h.Me(b).Y:0.0} a={h.Me(a).X:0.0},{h.Me(a).Y:0.0}" + $" dist={Math.Abs(h.Me(a).X - h.Me(b).X):0.0} hb={h.Me(b).Health}/{h.Me(b).MaxHealth}");
        Assert.True(h.Me(b).Alive);
    }

    [Fact]
    public void Natureza_GuerreiroBateMaisEmFera()
    {
        Assert.Equal(1.15, CinzasDaMare.Shared.Content.TipoMonstro.Vantagem("guerreiro", "Fera"));
        Assert.Equal(0.85, CinzasDaMare.Shared.Content.TipoMonstro.Vantagem("guerreiro", "Fantasma"));
        Assert.Equal(1.0, CinzasDaMare.Shared.Content.TipoMonstro.Vantagem("mago", "Fera"));
    }
}

public class X1ClienteAntigoTests
{
    [Fact]
    public void X1_AceitarDoGrupo_ValeComoAceitarDesafio()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("ana", level: 20, map: "porto_das_cinzas", x: 0, y: -9.5);
        var b = h.Join("bia", level: 20, map: "porto_das_cinzas", x: 1.5, y: -9.5);
        h.Ticks(2);
        h.Transport.ClientSends(a, new PartyIntent("invite", "bia"));
        h.Ticks(1);
        h.Transport.ClientSends(b, new PartyIntent("accept"));
        h.Ticks(1);
        h.Transport.ClientSends(a, new PartyIntent("duelo", "bia"));
        h.Ticks(1);
        h.Transport.ClientSends(b, new PartyIntent("accept"));   // cliente antigo: "aceitar" do grupo
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<NoticeMessage>(b), n => n.Text.StartsWith("X1:"));
    }
}

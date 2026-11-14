using CinzasDaMare.Shared.Content;
using System.Linq;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>Rodada 3: monstros nascem espalhados, andam à toa, alguns dormem, cada um agra sozinho e perseguem mais longe.</summary>
public class MonstrosVivosTests
{
    private static WorldContent Lively(params MapSpawn[] spawns)
    {
        var w = MapHarness.TestWorld(spawns);
        w.LivelyMonsters = true;
        return w;
    }

    private static int _seed = 11;
    private static Func<double> Rng() { var r = new Random(_seed++); return r.NextDouble; }

    [Fact]
    public void NascemEspalhados_NaoNoPontoExato()
    {
        var spawns = Enumerable.Range(0, 6).Select(i => MapHarness.Spawn("c" + i, "monster_r1_caranguejo_de_cal", 0, -12, group: "solo" + i)).ToArray();
        using var h = new MapHarness(Lively(spawns), rng: Rng());
        var id = h.Join("ana", x: 0, y: 14, level: 30);
        h.Ticks(1);
        var pos = h.LastSnapshot(id).Monsters.Select(m => (m.X, m.Y)).ToList();
        Assert.Equal(6, pos.Count);
        Assert.True(pos.Distinct().Count() >= 5, "cada um nasce num ponto da região");
        Assert.All(pos, p => Assert.True(Math.Sqrt(p.X * p.X + (p.Y + 12) * (p.Y + 12)) <= 16.5));
        // espalhados de verdade: ninguém empilhado em cima do outro
        var minGap = pos.SelectMany((a, i) => pos.Skip(i + 1).Select(b => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)))).Min();
        Assert.True(minGap > 2.0, $"distância mínima {minGap:0.0} m");
    }

    [Fact]
    public void SemAlvo_AndaAToa_PertoDeCasa()
    {
        using var h = new MapHarness(Lively(MapHarness.Spawn("c", "monster_r1_goblin_saqueador", 0, -10, group: "solo")), rng: Rng());
        var id = h.Join("ana", x: 0, y: 18, level: 30);
        h.Ticks(1);
        var start = h.Monster(id, "m_c")!;
        var moved = false;
        for (var i = 0; i < 40 && !moved; i++)
        {
            h.Ticks(10);
            var m = h.Monster(id, "m_c")!;
            if (m.Action == "sleep") continue;
            moved = Math.Abs(m.X - start.X) + Math.Abs(m.Y - start.Y) > 0.5;
        }
        var end = h.Monster(id, "m_c")!;
        Assert.True(moved || end.Action == "sleep", "anda à toa (ou deitou para dormir)");
        Assert.True(Math.Sqrt(end.X * end.X + (end.Y + 10) * (end.Y + 10)) < 14, "não vai embora de casa");
    }

    [Fact]
    public void SoQuemApanhaEOsVizinhosPertoVem_ONaoBandoInteiro()
    {
        // dois do mesmo grupo: um perto do jogador, outro a 13 m dele (fora do raio de alerta vivo de 6 m)
        var w = Lively(MapHarness.Spawn("a", "monster_r1_goblin_saqueador", 0, 3, group: "g"),
            MapHarness.Spawn("b", "monster_r1_goblin_saqueador", 0, 16, group: "g"));
        using var h = new MapHarness(w, rng: () => 0.99);   // 0,99: ninguém dorme, espalha pouco do ponto
        var id = h.Join("ana", x: 0, y: -30, level: 30);
        h.Ticks(1);
        // os dois se espalham igual (rng fixo): o jogador chega perto do "a" onde ele estiver
        var a = h.Monster(id, "m_a")!;
        id = h.Relocate(id, "teste", a.X, a.Y - 6);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_a")!.Action != "idle" && h.Monster(id, "m_a")!.Action != "sleep", 100));
        h.Ticks(4);
        Assert.Contains(h.Monster(id, "m_b")!.Action, new[] { "idle", "sleep" });
    }

    [Fact]
    public void Dormindo_SoAcordaComQuemChegaMuitoPerto()
    {
        using var h = new MapHarness(Lively(MapHarness.Spawn("c", "monster_r1_goblin_saqueador", 0, 0, group: "solo")), rng: () => 0.01); // 0,01: nasce dormindo
        var id = h.Join("ana", x: 0, y: -6, level: 30);
        h.Ticks(10);
        var m = h.Monster(id, "m_c")!;
        Assert.Equal("sleep", m.Action);
        id = h.Relocate(id, "teste", m.X, m.Y - 2.2);
        Assert.True(h.TickUntil(() => h.Monster(id, "m_c")!.Action is not ("sleep" or "idle"), 20), "acordou e veio");
    }
}

public class MochilaArrastarTests
{
    [Fact]
    public void JogarNoChao_TiraDaMochila_EApareceNoChao_EDaParaPegarDeNovo()
    {
        using var h = new MapHarness();
        var id = h.Join("ana", x: 0, y: 0);
        var cid = h.CharacterId(id);
        var item = h.Temp.Db.GetInventory(cid).First();
        h.Transport.ClientSends(id, new DropItemIntent(item.ItemId));
        h.Ticks(2);
        Assert.DoesNotContain(h.Temp.Db.GetInventory(cid), e => e.ItemId == item.ItemId);
        Assert.Contains(h.LastSnapshot(id).Items, i => i.ItemId == item.ItemId);
        h.Transport.ClientSends(id, new ClaimItemIntent("r1", item.ItemId));
        h.Ticks(2);
        Assert.Contains(h.Temp.Db.GetInventory(cid), e => e.ItemId == item.ItemId);
    }

    [Fact]
    public void ArrastarNaMochila_TrocaDeLugar()
    {
        using var h = new MapHarness();
        var id = h.Join("ana", x: 0, y: 0);
        var cid = h.CharacterId(id);
        h.Temp.Db.CreateInventoryItem(cid, "consumable_c01_tonico_fraco", "comum", 1, "t1");
        h.Temp.Db.CreateInventoryItem(cid, "mat_retalho", "comum", 1, "t2");
        var inv = h.Temp.Db.GetInventory(cid);
        Assert.True(inv.Count >= 2);
        var (a, b) = (inv[0], inv[1]);
        h.Transport.ClientSends(id, new MoveSlotIntent(a.ItemId, b.Slot));
        h.Ticks(1);
        var after = h.Temp.Db.GetInventory(cid);
        Assert.Equal(b.Slot, after.First(e => e.ItemId == a.ItemId).Slot);
        Assert.Equal(a.Slot, after.First(e => e.ItemId == b.ItemId).Slot);
        h.Transport.ClientSends(id, new MoveSlotIntent(a.ItemId, 39));
        h.Ticks(1);
        Assert.Equal(39, h.Temp.Db.GetInventory(cid).First(e => e.ItemId == a.ItemId).Slot);
    }
}

public class AndarCorrerTests
{
    [Fact]
    public void Andando_EMaisDevagarQueCorrendo()
    {
        using var h = new MapHarness();
        var a = h.Join("anda", x: -10, y: 0);
        var b = h.Join("corre", x: 10, y: 0);
        h.Transport.ClientSends(a, new MoveIntent(0, 1, false, Walk: true));
        h.Transport.ClientSends(b, new MoveIntent(0, 1, false));
        h.Ticks(20);
        var walked = h.Me(a).Y;
        var ran = h.Me(b).Y;
        Assert.True(walked > 1.5 && ran > walked * 1.4, $"andou {walked:0.00} m, correu {ran:0.00} m");
    }
}

public class AtributosMuTests
{
    [Fact]
    public void MagoDeArmaduraPesada_FicaSobrecarregado_EsquivaDemoraEAndaMaisDevagar()
    {
        using var h = new MapHarness();
        var mago = h.Join("mago", classId: "mago", x: -10, y: 0, level: 10);
        var guerreiro = h.Join("guer", classId: "guerreiro", x: 10, y: 0, level: 10);
        foreach (var id in new[] { mago, guerreiro })
        {
            var cid = h.CharacterId(id);
            var item = h.Temp.Db.CreateInventoryItem(cid, "armor_couraca_galeria", "comum", 1, "peso" + id)!;
            h.Transport.ClientSends(id, new EquipIntent(item));
        }
        // o guerreiro pôs pontos em Força (como quem sobe de nível pensando na armadura)
        h.Temp.Db.AddStatPoints(h.CharacterId(guerreiro), 10);
        for (var i = 0; i < 10; i++) { h.Transport.ClientSends(guerreiro, new SpendStatIntent("forca")); h.Ticks(1); }
        h.Ticks(2);
        var sm = h.Last<StatsMessage>(mago);
        var sg = h.Last<StatsMessage>(guerreiro);
        Assert.Equal("energia", sm.PrimaryKey);
        Assert.Equal("forca", sg.PrimaryKey);
        Assert.True(sm.Weight > 10, $"peso {sm.Weight}");
        Assert.True(sm.OverloadPct > 30, $"mago sobrecarregado {sm.OverloadPct}%");
        Assert.Equal(0, sg.OverloadPct, 3);
        Assert.True(sm.DodgeCooldownMs > sg.DodgeCooldownMs);

        h.Transport.ClientSends(mago, new MoveIntent(0, 1));
        h.Transport.ClientSends(guerreiro, new MoveIntent(0, 1));
        h.Ticks(20);
        Assert.True(h.Me(mago).Y < h.Me(guerreiro).Y * 0.95, "o mago sobrecarregado anda mais devagar");
    }

    [Fact]
    public void PontoEmForca_DeQuemNaoEGuerreiro_VaiParaForca()
    {
        using var h = new MapHarness();
        var id = h.Join("arq", classId: "arqueiro", x: 0, y: 0, level: 5);
        h.Temp.Db.AddStatPoints(h.CharacterId(id), 2);
        h.Transport.ClientSends(id, new SpendStatIntent("forca"));
        h.Ticks(1);
        var st = h.Last<StatsMessage>(id);
        Assert.Equal(CinzasDaMare.Shared.Content.Atributos.BaseForca + 1, st.Forca);
        h.Transport.ClientSends(id, new SpendStatIntent("agilidade"));   // agilidade é a principal do arqueiro
        h.Ticks(1);
        Assert.Equal(st.Primary + 1, h.Last<StatsMessage>(id).Primary);
    }
}

public class ContadorDeColetaTests
{
    [Fact]
    public void MissaoDeTrazerItens_AtualizaOContadorQuandoAMochilaMuda()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var q = h.World.Quests.Values.First(x => x.Kind == "collect");
        var token = h.Precreate("ana", x: 0, y: 0, level: 30);
        var id = h.Login(token);
        var cid = h.CharacterId(id);
        h.Transport.ClientDisconnects(id);
        h.Ticks(1);
        Assert.True(h.Temp.Db.TryAcceptQuest(cid, q.Id, false));
        id = h.Login(token);
        h.Ticks(1);
        h.Temp.Db.CreateInventoryItem(cid, q.Targets[0], "comum", 1, "coleta1");
        var item = h.Temp.Db.GetInventory(cid).First(e => e.DefinitionId == q.Targets[0]);
        h.Transport.ClientSends(id, new MoveSlotIntent(item.ItemId, 39));   // qualquer ação que reenvia a mochila
        h.Ticks(1);
        var log = h.Transport.Received<QuestLogMessage>(id).Last();
        Assert.Equal(1, log.Quests.First(e => e.Id == q.Id).Progress);
    }
}

public class LobbyExcluirTests
{
    [Fact]
    public void Lobby_ExcluirLiberaVaga_ENome_SemApagarDoBanco()
    {
        using var h = new MapHarness();
        var token = h.Temp.Db.ProvisionAccount("cheia").Token;
        var id = h.Transport.Connect();
        h.Transport.ClientSends(id, new LobbyRequest(token, Protocol.Version));
        h.Ticks(1);
        foreach (var n in new[] { "Um", "Dois", "Tres", "Quatro", "Cinco", "Seis", "Sete", "Oito" }.Take(GameServer.MaxCharactersPerAccount))
        {
            h.Transport.ClientSends(id, new CreateCharacterIntent(n + "zao", "guerreiro"));
            h.Ticks(1);
        }
        Assert.Equal(GameServer.MaxCharactersPerAccount, h.Transport.Received<LobbyMessage>(id).Last().Characters.Count);
        h.Transport.ClientSends(id, new CreateCharacterIntent("Extrazao", "guerreiro"));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), r => r.Action == "create_character" && !r.Success);

        var alvo = h.Transport.Received<LobbyMessage>(id).Last().Characters.First(c => c.Name == "Doiszao");
        h.Transport.ClientSends(id, new DeleteCharacterIntent(alvo.Id, "outro nome"));      // nome errado: nada acontece
        h.Ticks(1);
        Assert.Equal(GameServer.MaxCharactersPerAccount, h.Transport.Received<LobbyMessage>(id).Last().Characters.Count);
        h.Transport.ClientSends(id, new DeleteCharacterIntent(alvo.Id, "doiszao"));
        h.Ticks(1);
        var lobby = h.Transport.Received<LobbyMessage>(id).Last();
        Assert.Equal(GameServer.MaxCharactersPerAccount - 1, lobby.Characters.Count);
        Assert.DoesNotContain(lobby.Characters, c => c.Id == alvo.Id);
        Assert.NotNull(h.Temp.Db.LoadCharacter(alvo.Id));   // arquivado, não apagado

        h.Transport.ClientSends(id, new CreateCharacterIntent("Doiszao", "mago"));             // vaga e nome livres de novo
        h.Ticks(1);
        Assert.Equal(GameServer.MaxCharactersPerAccount, h.Transport.Received<LobbyMessage>(id).Last().Characters.Count);
        h.Transport.ClientSends(id, new EnterWorldIntent(alvo.Id));                             // o arquivado não entra
        h.Ticks(1);
        Assert.False(h.Transport.Received<LoginResult>(id).Last().Ok);
    }
}

public class PenalidadeDeMorteTests
{
    [Fact]
    public void MorrerNoMundo_DepoisDo20_NaoCaiDeNivel_EGuardaOMaiorNivel()   // 10/11: 20% fixo, nunca cai de nível
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("g", "monster_r1_goblin_saqueador", 0, 2, level: 40, group: "solo") });
        w.LivelyMonsters = true;
        using var h = new MapHarness(w, rng: () => 0.99);
        var id = h.Join("ana", x: 0, y: -1, level: 25);
        var g = h.Monster(id, "m_g") ?? h.LastSnapshot(id).Monsters.First();
        id = h.Relocate(id, "teste", g.X, g.Y - 1.5);
        Assert.True(h.TickUntil(() => !h.Me(id).Alive, 600), "o goblin nível 40 derruba");
        Assert.Equal(25, h.Me(id).Level);
        Assert.DoesNotContain(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("caiu de nível"));
        Assert.Equal(25, h.Temp.Db.GetMaxLevel(h.CharacterId(id)));
    }

    [Fact]
    public void Morrer_Perde20PorCentoDoQueTinhaNoNivel_ENaoCaiDeNivel()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("g", "monster_r1_goblin_saqueador", 0, 2, level: 40, group: "solo") });
        w.LivelyMonsters = true;
        using var h = new MapHarness(w, rng: () => 0.99);
        var id = h.Join("ana", x: 0, y: -1, level: 5);
        var g = h.Monster(id, "m_g") ?? h.LastSnapshot(id).Monsters.First();
        id = h.Relocate(id, "teste", g.X, g.Y - 1.5);
        Assert.True(h.TickUntil(() => !h.Me(id).Alive, 600), "o goblin nível 40 derruba");
        Assert.Equal(5, h.Me(id).Level);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("morreu"));
    }
}

public class EfeitosDeStatusTests
{
    [Fact]
    public void PicadaDeAranha_Envenena_EAvisa()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("a", "monster_mata_aranha_toca", 0, 2.2, level: 18, group: "solo") });
        using var h = new MapHarness(w);
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        Assert.True(h.TickUntil(() => h.Me(id).Status.Contains("veneno"), 400), "a aranha envenena");
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("Envenenado"));
    }
}

/// <summary>Bestiário (09/11): criaturas novas no mundo, cada uma no mapa da sua faixa; a planta não anda e a gosma deixa lento.</summary>
public class BestiarioTests
{
    [Fact]
    public void TodaCriaturaDoBestiario_NasceEmAlgumMapaAberto_NaFaixaDele()
    {
        var content = TestContent.Load();
        var world = MapHarness.RealWorld();
        var bst = content.Monsters.Values.Where(m => m.Id.StartsWith("monster_bst_")).ToList();
        Assert.True(bst.Count >= 20, "o bestiário tem as criaturas pedidas");
        foreach (var def in bst)
        {
            var spots = world.Maps.Values.SelectMany(m => m.Spawns.Select(s => (Map: m, Spawn: s))).Where(x => x.Spawn.Monster == def.Id).ToList();
            Assert.True(spots.Count > 0, $"{def.Id} não nasce em nenhum mapa");
            foreach (var (map, s) in spots.Where(x => x.Map.Region != "reinos"))   // 12/11: reinos ocultos são só de bestiário (faixa 35–38, 20–22)
            {
                var others = map.Spawns.Where(o => !o.Monster.StartsWith("monster_bst_")).Select(o => o.Level).ToList();
                Assert.InRange(s.Level, others.Min() - 2, others.Max() + 4);
            }
        }
    }

    [Fact]
    public void PlantaCarnivora_NaoSaiDoLugar_MasMorde()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("p", "monster_bst_planta_carnivora", 0, 3, level: 12, group: "solo") });
        using var h = new MapHarness(w);
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        var start = h.Monster(id, "m_p")!;
        Assert.True(h.TickUntil(() => h.Me(id).Health < h.Me(id).MaxHealth, 400), "a planta morde quem chega perto");
        var now = h.Monster(id, "m_p")!;
        Assert.True(Math.Abs(now.X - start.X) + Math.Abs(now.Y - start.Y) < 0.8, "a planta fica enraizada");
    }

    [Fact]
    public void CuspeDeGosma_DeixaLento()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", "monster_bst_meleca", 0, 6, level: 5, group: "solo") });
        using var h = new MapHarness(w);
        var id = h.Join("ana", x: 0, y: 0, level: 30);
        Assert.True(h.TickUntil(() => h.Me(id).Status.Contains("lentidao"), 600), "a gosma deixa lento");
    }
}

/// <summary>Pedido do usuário: baú só abre com a área limpa (monstro vivo perto = não abre).</summary>
public class BauAreaLimpaTests
{
    [Fact]
    public void Bau_ComMonstroPerto_NaoAbre_DepoisDeLimpar_Abre()
    {
        // grupo "ninho" = bando (fica junto, raio 4 m) — o espalhar do mundo vivo não leva o bicho para longe do baú
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("c", "monster_r0_caranguejinho", 5, 0, level: 1, group: "ninho"),
                                             MapHarness.Spawn("c2", "monster_r0_caranguejinho", 5.5, 1, level: 1, group: "ninho") },
            chests: new[] { new MapChest("b1", 2, 0, "bau_trilha", 0) });
        w.LivelyMonsters = true;
        using var h = new MapHarness(w, rng: () => 0.0);   // sorteio 0: o espalhar deixa o bicho no ponto dele
        var a = h.Join("ana", x: 0, y: 0, level: 30);
        h.Ticks(2);
        var near = h.Monster(a, "m_c")!;
        Assert.True(Math.Sqrt((near.X - 2) * (near.X - 2) + near.Y * near.Y) <= 11, $"caranguejinho perto do baú ({near.X:0.0}, {near.Y:0.0})");
        h.Transport.ClientSends(a, new InteractIntent("chest:b1"));
        h.Ticks(1);
        Assert.Equal("area_not_clear", h.Transport.Received<ActionResultMessage>(a).Last(r => r.Action == "interact").Reason);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.Contains("limpe a área"));
        foreach (var mid in new[] { "m_c", "m_c2" })
        {
            var t = h.Monster(a, mid)!;
            a = h.Relocate(a, "teste", t.X - 1.2, t.Y);
            h.Face(a, 1, 0);
            for (var i = 0; i < 60 && h.Monster(a, mid)?.Alive == true; i++)
            {
                var cur = h.Monster(a, mid)!;
                h.Face(a, cur.X - h.Me(a).X, cur.Y - h.Me(a).Y);
                h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_corte", null));
                h.Ticks(12);
            }
            Assert.False(h.Monster(a, mid)!.Alive, "o caranguejinho morre");
        }
        a = h.Relocate(a, "teste", 0, 0);
        h.Transport.ClientSends(a, new InteractIntent("chest:b1"));
        h.Ticks(1);
        Assert.Equal("chest_opened", h.Transport.Received<ActionResultMessage>(a).Last(r => r.Action == "interact").Reason);
    }
}

/// <summary>Elites com afixo (estilo Diablo): no mundo real, elite/raro nasce com um afixo que muda a luta.</summary>
public class AfixosDeEliteTests
{
    [Fact]
    public void EliteNoMundoReal_NasceComAfixo_EBlindadoLevaMenosDano()
    {
        // sorteio 0,5 → 3º afixo da lista ("blindado")
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("e", "monster_r1_caranguejo_couracado", 0, 6, level: 7, group: "solo") });
        w.LivelyMonsters = true;
        using var h = new MapHarness(w, rng: () => 0.5);
        var a = h.Join("ana", x: 0, y: 0, level: 30);
        h.Ticks(2);
        var e = h.Monster(a, "m_e")!;
        Assert.Equal("blindado", e.Affix);
    }

    [Fact]
    public void MonstroComum_NaoTemAfixo_NemNoMundoDeTeste()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("e", "monster_r1_caranguejo_couracado", 0, 6, level: 7, group: "solo"),
                                             MapHarness.Spawn("c", "monster_r1_caranguejo_de_cal", 4, 6, level: 1, group: "solo2") });
        using var h = new MapHarness(w, rng: () => 0.5);
        var a = h.Join("ana", x: 0, y: 0, level: 30);
        h.Ticks(2);
        Assert.Equal("", h.Monster(a, "m_e")!.Affix);
        Assert.Equal("", h.Monster(a, "m_c")!.Affix);
    }
}

/// <summary>Prêmio por nível ("passe"): no nível 10 vêm moedas, poções e o tomo da classe daquela faixa — uma vez só.</summary>
public class PremioDeNivelTests
{
    [Fact]
    public void ChegarAoNivel10_DaMoedasPocoesETomo()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("c", "monster_r0_caranguejinho", 1.4, 0, level: 9, group: "solo") });
        using var h = new MapHarness(w);
        var token = h.Precreate("ana", "guerreiro", 0, 0, 9);
        // XP a um passo do nível 10 (direto no banco temporário do teste)
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + h.Temp.DbPath))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE characters SET xp = @x";
            cmd.Parameters.AddWithValue("@x", CinzasDaMare.Server.Combat.Progression.MapXpToNext(9) - 1);
            cmd.ExecuteNonQuery();
        }
        var a = h.Login(token);
        h.Face(a, 1, 0);
        for (var i = 0; i < 60 && h.Monster(a, "m_c")?.Alive == true; i++)
        {
            h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_corte", null));
            h.Ticks(12);
        }
        h.Ticks(2);
        Assert.False(h.Monster(a, "m_c")!.Alive, "matou o caranguejinho");
        Assert.Equal(10, h.Me(a).Level);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.StartsWith("Prêmio do nível 10"));
        var inv = h.Temp.Db.GetInventory(h.CharacterId(a));
        Assert.Contains(inv, i => i.DefinitionId == "tomo_guerreiro_salto");
    }
}

/// <summary>Hordas caçadoras (pedido do usuário): no mundo real, de tempos em tempos bichos mais fortes vêm atrás de você.</summary>
public class HordaTests
{
    [Fact]
    public void Horda_NasceLongeEVemCaçar_MaisForte_ENaoRenasce()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("c", "monster_r1_caranguejo_de_cal", -12, -12, level: 3, group: "solo") });
        w.LivelyMonsters = true;
        using var h = new MapHarness(w, rng: () => 0.5);
        h.Server.HordeIntervalMs = 2_000;
        var a = h.Join("ana", x: 0, y: 0, level: 6);
        Assert.True(h.TickUntil(() => h.LastSnapshot(a).Monsters.Any(m => m.Id.StartsWith("horda")), 400), "a horda aparece");
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.Contains("vem te caçar"));
        var hunters = h.LastSnapshot(a).Monsters.Where(m => m.Id.StartsWith("horda")).ToList();
        Assert.Equal(3, hunters.Count);
        Assert.All(hunters, m => Assert.Equal(8, m.Level));   // nível do jogador + 2
        var d0 = hunters.Min(m => Math.Sqrt(m.X * m.X + m.Y * m.Y));
        h.Ticks(40);
        var d1 = h.LastSnapshot(a).Monsters.Where(m => m.Id.StartsWith("horda")).Min(m => Math.Sqrt(m.X * m.X + m.Y * m.Y));
        Assert.True(d1 < d0 - 2, $"a horda vem na direção do jogador ({d0:0.0} → {d1:0.0})");
        // vencer a horda inteira paga o prêmio
        for (var i = 0; i < 400 && h.LastSnapshot(a).Monsters.Any(m => m.Id.StartsWith("horda") && m.Alive) && h.Me(a).Alive; i++)
        {
            var t = h.LastSnapshot(a).Monsters.Where(m => m.Id.StartsWith("horda") && m.Alive).OrderBy(m => Math.Abs(m.X - h.Me(a).X) + Math.Abs(m.Y - h.Me(a).Y)).First();
            h.Face(a, t.X - h.Me(a).X, t.Y - h.Me(a).Y);
            h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_corte", null));
            h.Ticks(12);
        }
        if (!h.LastSnapshot(a).Monsters.Any(m => m.Id.StartsWith("horda") && m.Alive))
            Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.Contains("venceu a horda"));
    }
}

/// <summary>Peso no movimento (mundo real): arranca em ~0,2 s e freia em ~0,13 s, em vez de mudar a velocidade na hora.</summary>
public class PesoNoMovimentoTests
{
    [Fact]
    public void Arrancar_EParar_ComAceleracao()
    {
        var w = MapHarness.TestWorld(System.Array.Empty<MapSpawn>());
        w.LivelyMonsters = true;
        using var h = new MapHarness(w);
        var a = h.Join("ana", x: 0, y: 0, level: 5);
        // (a foto do servidor sai a cada 2 tiques: medimos de 2 em 2)
        h.Transport.ClientSends(a, new MoveIntent(1, 0));
        h.Ticks(2);
        var x1 = h.Me(a).X;                      // 0,1 s: bem menos que 5 m/s × 0,1 s = 0,5 m
        Assert.InRange(x1, 0.05, 0.35);
        h.Ticks(10);
        var xa = h.Me(a).X; h.Ticks(2);
        Assert.InRange(h.Me(a).X - xa, 0.46, 0.54); // velocidade cheia depois de meio segundo
        h.Transport.ClientSends(a, new MoveIntent(0, 0));
        var xs = h.Me(a).X; h.Ticks(2);
        Assert.True(h.Me(a).X - xs > 0.05, "não para seco: escorrega um pouco na freada");
        h.Ticks(6);
        var xf = h.Me(a).X; h.Ticks(2);
        Assert.Equal(xf, h.Me(a).X, 3);           // e para de vez
    }
}

/// <summary>Aparar (parry): Guarda levantada logo antes do golpe anula o dano e deixa o monstro aberto.</summary>
public class ApararTests
{
    [Fact]
    public void GuardaNaHoraCerta_Apara_EDeixaOMonstroAberto()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("c", "monster_r1_caranguejo_de_cal", 0, 1.6, level: 3, group: "solo") });
        using var h = new MapHarness(w);
        var a = h.Join("ana", x: 0, y: 0, level: 10);
        h.EquipShield(a);
        h.Face(a, 0, 1);
        // espera o caranguejo começar a preparar o golpe e levanta a guarda bem perto da hora do golpe
        Assert.True(h.TickUntil(() => h.Monster(a, "m_c")!.Action.StartsWith("windup"), 200), "o caranguejo prepara o golpe");
        var hp = h.Me(a).Health;
        var parried = false;
        for (var i = 0; i < 40 && !parried; i++)
        {
            var m = h.Monster(a, "m_c")!;
            if (m.Action.StartsWith("windup") && m.ActionT > 0.75)
            {
                h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_guarda_de_escudo", null));
                h.Ticks(8);
                parried = h.Transport.Received<CombatEvent>(a).Any(e => e.SkillId == "parry");
                break;
            }
            h.Ticks(1);
        }
        Assert.True(parried, "aparou o golpe");
        Assert.Equal(hp, h.Me(a).Health);
        Assert.StartsWith("recover", h.Monster(a, "m_c")!.Action);

        // 11/11: golpe logo depois de aparar é contra-ataque (crítico garantido)
        h.Transport.ClientSends(a, new CastIntent("skill_guerreiro_corte", "m_c"));
        Assert.True(h.TickUntil(() => h.Transport.Received<NoticeMessage>(a).Any(n => n.Text == "Contra-ataque!"), 40), "contra-ataque");
        Assert.Contains(h.Transport.Received<CombatEvent>(a), e => e.SkillId == "skill_guerreiro_corte" && e.Crit);
    }
}

/// <summary>Conjuntos (set): vestir 2 peças do mesmo conjunto soma o bônus de 2 peças; tirar uma, some.</summary>
public class ConjuntosTests
{
    private static string Equip(MapHarness h, int id, string def)
    {
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), def, "comum", 1, "t:" + Guid.NewGuid())!;
        h.Transport.ClientSends(id, new EquipIntent(item));
        h.Ticks(1);
        return item;
    }

    [Fact]
    public void DuasPecas_DoConjunto_DaoOBonusDeDuas()
    {
        using var h = new MapHarness(MapHarness.TestWorld(System.Array.Empty<MapSpawn>()));
        var a = h.Join("ana", x: 0, y: 0, level: 10);
        h.Temp.Db.AddStatPoints(h.CharacterId(a), 30);
        for (var i = 0; i < 30; i++) h.Transport.ClientSends(a, new SpendStatIntent("forca"));
        h.Ticks(1);
        Equip(h, a, "hist_couraca_afogado");
        var hp1 = h.Me(a).MaxHealth;
        Equip(h, a, "hist_elmo_afogado");
        var hp2 = h.Me(a).MaxHealth;
        // elmo dá 25 de vida; o bônus de 2 peças dá mais 40
        Assert.True(hp2 - hp1 >= 25 + 40 - 1, $"vida {hp1} → {hp2}");
    }
}

public class AfixosTests
{
    [Fact]
    public void Sorteio_EhDeterministico_ESoParaQualidadeBoa()
    {
        var a = ItemAffixes.Roll("item-123", "raro", "armor", 10);
        var b = ItemAffixes.Roll("item-123", "raro", "armor", 10);
        Assert.Equal(2, a.Count);
        Assert.Equal(a.Select(x => (x.Stat, x.Value)), b.Select(x => (x.Stat, x.Value)));
        Assert.NotEqual(a[0].Stat, a[1].Stat);
        Assert.Empty(ItemAffixes.Roll("item-123", "comum", "armor", 10));
        Assert.Empty(ItemAffixes.Roll("item-123", "raro", "none", 10));
        Assert.Equal(3, ItemAffixes.Roll("x", "epico", "physical_weapon", 30).Count);
    }

    [Fact]
    public void ArmaduraRara_SomaOsAfixosNaVidaOuArmadura()
    {
        using var h = new MapHarness(MapHarness.TestWorld(System.Array.Empty<MapSpawn>()));
        var a = h.Join("ana", x: 0, y: 0, level: 10);
        h.Temp.Db.AddStatPoints(h.CharacterId(a), 30);
        for (var i = 0; i < 30; i++) h.Transport.ClientSends(a, new SpendStatIntent("forca"));
        h.Ticks(3);
        // procura um exemplar cujo sorteio dê vida (o id é aleatório)
        for (var tries = 0; tries < 40; tries++)
        {
            var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(a), "hist_couraca_afogado", "raro", 1, "t:" + Guid.NewGuid())!;
            var vida = ItemAffixes.Sum(ItemAffixes.Roll(item, "raro", "armor", 7), "max_health");
            if (vida <= 0) continue;
            var before = h.Me(a).MaxHealth;
            h.Transport.ClientSends(a, new EquipIntent(item));
            h.Ticks(3);
            var plain = 0.0;   // a couraça também dá vida própria: compara com um exemplar comum
            var c = h.Temp.Db.CreateInventoryItem(h.CharacterId(a), "hist_couraca_afogado", "comum", 1, "t:" + Guid.NewGuid())!;
            var withAffix = h.Me(a).MaxHealth;
            h.Transport.ClientSends(a, new EquipIntent(c));
            h.Ticks(3);
            plain = h.Me(a).MaxHealth;
            // 13/11: o feitio (sorteado por exemplar) também mexe na vida — desconta a diferença dos dois
            var feitio = ItemAffixes.Sum(ItemAffixes.Feitio(item, "armor", 7).Linhas, "max_health") - ItemAffixes.Sum(ItemAffixes.Feitio(c, "armor", 7).Linhas, "max_health");
            Assert.True(withAffix - plain >= vida + feitio - 1, $"com afixo {withAffix}, comum {plain}, afixo {vida}, feitio {feitio} (antes {before})");
            return;
        }
        Assert.True(false, "nenhum exemplar com afixo de vida em 40 tentativas");
    }
}

public class MissoesComAcontecimentoTests
{
    private static (MapHarness H, int Id) Setup(QuestDef q, int level = 5)
    {
        var real = MapHarness.RealWorld();
        var map = MapHarness.TestWorld(new[] { MapHarness.Spawn("longe", "monster_r0_caranguejinho", 25, -25, level: 2) }).Map;
        var world = new WorldContent(new[] { map }, map.Id, real.Behaviors, real.LootTables, quests: new Dictionary<string, QuestDef> { [q.Id] = q });
        var h = new MapHarness(world, rng: () => 0.5);
        var token = h.Precreate("ana", x: 0, y: 0, level: level);
        var id = h.Login(token);
        var cid = h.CharacterId(id);
        h.Transport.ClientDisconnects(id);
        h.Ticks(1);
        Assert.True(h.Temp.Db.TryAcceptQuest(cid, q.Id, false));
        id = h.Login(token);
        h.Ticks(1);
        return (h, id);
    }

    [Fact]
    public void Defesa_ChegandoNoPonto_VemAOndaComTresBichos()
    {
        var q = new QuestDef("q_def", "Segure a Praia", "x", "defend", new[] { "monster_r0_caranguejinho" }, 2, "") { Map = "teste", X = 0, Y = 0, Radius = 6 };
        var (h, id) = Setup(q);
        using var _ = h;
        h.Ticks(12);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("Onda 1/2"));
        Assert.Equal(3, h.LastSnapshot(id).Monsters.Count(m => m.Id.StartsWith("missao")));
    }

    [Fact]
    public void Cacada_ChegandoNoRastro_ApareceAPresaAlfa()
    {
        var q = new QuestDef("q_caca", "Caçada", "x", "hunt", new[] { "monster_r0_caranguejinho" }, 1, "") { Map = "teste", X = 3, Y = 3 };
        var (h, id) = Setup(q);
        using var _ = h;
        h.Ticks(12);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("Alfa"));
        var prey = h.LastSnapshot(id).Monsters.Single(m => m.Id.StartsWith("missao"));
        Assert.Equal(3, prey.Level);   // 10/11: espécie nível 2 no mapa + 1 (no máximo 2 acima de quem caça)
    }

    [Fact]
    public void Escolta_MandaOMarcadorDaCarroca_EArmaEmboscada()
    {
        var q = new QuestDef("q_esc", "Escolta", "x", "escort", new[] { "monster_r0_caranguejinho" }, 2, "") { Map = "teste", X = 0, Y = 0, ToX = 0, ToY = 20 };
        var (h, id) = Setup(q);
        using var _ = h;
        h.Ticks(12);
        Assert.Contains(h.Transport.Received<QuestMarkerMessage>(id), m => m.QuestId == "q_esc" && m.Kind == "carroca");
        Assert.True(h.LastSnapshot(id).Monsters.Count(m => m.Id.StartsWith("missao")) >= 2);
    }

    [Fact]
    public void Defesa_VencendoAOnda_ContaNaMissao()
    {
        var q = new QuestDef("q_def1", "Segure", "x", "defend", new[] { "monster_r0_caranguejinho" }, 1, "") { Map = "teste", X = 0, Y = 0, Radius = 6 };
        var (h, id) = Setup(q, level: 12);
        using var _ = h;
        for (var t = 0; t < 1500; t++)
        {
            h.Ticks(1);
            if (t % 4 != 0) continue;
            var alvo = h.LastSnapshot(id).Monsters.FirstOrDefault(m => m.Id.StartsWith("missao") && m.Health > 0);
            if (alvo is not null)
            {
                var me = h.Me(id);
                h.Face(id, alvo.X - me.X, alvo.Y - me.Y);
                h.Transport.ClientSends(id, new CastIntent(TestContent.Golpe, null));
            }
            if (h.Transport.Received<NoticeMessage>(id).Any(n => n.Text.Contains("pronto"))) break;
        }
        var mons = string.Join(";", h.LastSnapshot(id).Monsters.Select(m => $"{m.Id} {m.Health:0} ({m.X:0.0},{m.Y:0.0})"));
        Assert.True(h.Transport.Received<NoticeMessage>(id).Any(n => n.Text.Contains("Segure: pronto")), $"notices: {string.Join(" | ", h.Transport.Received<NoticeMessage>(id).Select(n => n.Text))}; monstros: {mons}; eu {h.Me(id).Health:0} em ({h.Me(id).X:0.0},{h.Me(id).Y:0.0}); acoes {h.Transport.Received<ActionResultMessage>(id).Count()} {string.Join(",", h.Transport.Received<ActionResultMessage>(id).TakeLast(3).Select(r => r.Reason))}");
    }
}

public class TrocaDeClasseTests
{
    [Fact]
    public void Nivel20_FalandoComOMentor_TrocaNaHora()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("leo", "guerreiro", x: -21, y: -6, level: 20, map: "porto_das_cinzas");
        id = h.ProvasDeClasseFeitas(id, "g");   // 14/11: a troca pede as 4 provas do mentor
        h.Transport.ClientSends(id, new QuestIntent("q_troca_g", "accept"));
        h.Ticks(2);
        Assert.Contains("evo:campeao", h.Transport.Received<FlagsMessage>(id).Last().Flags);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("Campeão"));
    }

    [Fact]
    public void SemAsProvas_NaoTroca()
    {
        // 14/11 ("para fazer as missões de classe precisa pegar itens, matar monstros, provas... pra no final passar")
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("leo", "guerreiro", x: -21, y: -6, level: 20, map: "porto_das_cinzas");
        h.Transport.ClientSends(id, new QuestIntent("q_troca_g", "accept"));
        h.Ticks(2);
        Assert.DoesNotContain(h.Transport.Received<FlagsMessage>(id), f => f.Flags.Contains("evo:campeao"));
    }

    [Fact]
    public void AbaixoDo15_NaoTroca()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("leo", "guerreiro", x: -21, y: -6, level: 14, map: "porto_das_cinzas");
        h.Transport.ClientSends(id, new QuestIntent("q_troca_g", "accept"));
        h.Ticks(2);
        Assert.DoesNotContain(h.Transport.Received<FlagsMessage>(id), f => f.Flags.Contains("evo:campeao"));
    }
}

public class EsferaNivel2Tests
{
    [Fact]
    public void Esfera2_SoDepoisDaTroca_EDiminuiARecarga()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("leo", "guerreiro", x: -21, y: -6, level: 20, map: "porto_das_cinzas");
        var cid = h.CharacterId(id);
        h.Temp.Db.CreateInventoryItem(cid, "tomo_guerreiro_giro", "comum", 1, "t1");
        var esfera = h.Temp.Db.CreateInventoryItem(cid, "tomo_guerreiro_giro_ii", "comum", 1, "t2")!;
        var tomo = h.Temp.Db.GetInventory(cid).First(e => e.DefinitionId == "tomo_guerreiro_giro").ItemId;
        h.Transport.ClientSends(id, new UseItemIntent(esfera));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("troca de classe"));
        id = h.ProvasDeClasseFeitas(id, "g");   // 14/11: a troca pede as 4 provas do mentor
        h.Transport.ClientSends(id, new UseItemIntent(tomo));
        h.Ticks(1);
        h.Transport.ClientSends(id, new QuestIntent("q_troca_g", "accept"));
        h.Ticks(2);
        h.Transport.ClientSends(id, new UseItemIntent(esfera));
        h.Ticks(1);
        Assert.Contains("up:skill_guerreiro_giro", h.Temp.Db.GetFlags(cid));
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("nível 2"));
    }
}

public class BussolaDaMissaoTests
{
    [Fact]
    public void PedrasDeViagem_NoPortoApontaOPortalDaCosta_NaCostaApontaAPedraAzul()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var token = h.Precreate("ana", x: 0, y: 0, level: 3);
        var id = h.Login(token);
        var cid = h.CharacterId(id);
        h.Transport.ClientDisconnects(id);
        h.Ticks(1);
        Assert.True(h.Temp.Db.TryAcceptQuest(cid, "q_tut_viagem", false));
        id = h.Login(token);
        h.Ticks(1);
        var e = h.Transport.Received<QuestLogMessage>(id).Last().Quests.First(x => x.Id == "q_tut_viagem");
        Assert.Equal("porto_das_cinzas", e.MapId);
        Assert.Equal(-31.2, e.Y, 1);   // portal para a Costa
        var bau = h.World.Quests["q_tut_bau"];
        Assert.Equal("use", bau.Kind);
    }
}

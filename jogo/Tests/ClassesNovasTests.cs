using System.Text.Json;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;

namespace CinzasDaMare.Tests;

/// <summary>Bruxa (cura, barreira, aura) e Arqueiro (Passos Fantasma, emboscada, marca): regras no servidor.</summary>
public class ClassesNovasTests
{
    private const string Raider = "monster_r1_saqueador_da_vigilia";
    private const string Toque = "skill_bruxa_toque_alquimico";
    private const string Barreira = "skill_bruxa_barreira_de_eter";
    private const string Aura = "skill_bruxa_aura_primal";
    private const string Emboscada = "skill_arqueiro_emboscada";
    private const string Passos = "skill_arqueiro_passos_fantasma";
    private const string Olhar = "skill_arqueiro_olhar_do_cacador";

    [Fact]
    public void Conteudo_QuatroClasses_ComHabilidadesDaPropriaClasse()
    {
        var b = TestContent.Load();
        foreach (var id in new[] { "bruxa", "arqueiro" })
        {
            var c = b.Classes[id];
            foreach (var sk in c.Loadout.Values.Append(c.UnarmedBasic!).Append(c.EvolutionSkill!).Append(c.InnateSkill!))
                Assert.Equal(id, b.Skills[sk].ClassId);
        }
        Assert.Equal("fem", b.Classes["bruxa"].Body);
        Assert.Equal("masc", b.Classes["arqueiro"].Body);
    }

    [Fact]
    public void RecompensasPorClasse_TemAsQuatroOpcoes()
    {
        var world = MapHarness.RealWorld();
        var b = TestContent.Load();
        foreach (var q in world.Quests!.Values)
            foreach (var raw in q.RewardItems.Where(r => r.StartsWith("@classe:")))
            {
                var parts = raw[8..].Split('|');
                Assert.True(parts.Length == 4, $"{q.Id}: {raw}");
                string[] order = { "guerreiro", "mago", "bruxa", "arqueiro" };
                for (var i = 0; i < 4; i++)
                {
                    var id = parts[i].Split('*')[0];
                    if (b.Items.TryGetValue(id, out var item) && item.AllowedClasses.Count > 0)
                        Assert.True(item.AllowedClasses.Contains(order[i]), $"{q.Id}: {id} não serve para {order[i]}");
                }
            }
    }

    [Fact]
    public void ToqueAlquimico_CuraOAliadoMaisFerido()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 2.0, 0) });
        using var h = new MapHarness(w);
        var gui = h.Join("gui", x: 0, y: 0, level: 6);
        var bru = h.Join("hazel", "bruxa", x: 0, y: 7, level: 10);
        // 14/11: cura/escudo vão para colega de GRUPO bem ferido (quem só está por perto não conta)
        h.Transport.ClientSends(bru, new PartyIntent("invite", "gui"));
        h.Ticks(2);
        h.Transport.ClientSends(gui, new PartyIntent("accept"));
        h.Ticks(2);
        Assert.True(h.TickUntil(() => h.Me(gui).Health < h.Me(gui).MaxHealth * 0.6, 1500), $"vida {h.Me(gui).Health}/{h.Me(gui).MaxHealth}");
        var before = h.Me(gui).Health;
        h.Transport.ClientSends(bru, new CastIntent(Toque, null));
        Assert.True(h.TickUntil(() => h.Events(bru).Any(e => e.SkillId == Toque), 30));
        var ev = h.Events(bru).First(e => e.SkillId == Toque);
        Assert.True(ev.Heal);
        Assert.Equal(h.CharacterId(gui), ev.TargetId);
        Assert.True(ev.Damage > 0);
    }

    [Fact]
    public void Cura_NaoVaiParaQuemNaoEDoGrupo()
    {
        // 14/11 ("a proteção do meu amigo vem sempre pra mim que estou perto, até no x1")
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 2.0, 0) });
        using var h = new MapHarness(w);
        var gui = h.Join("gui", x: 0, y: 0, level: 6);
        var bru = h.Join("hazel", "bruxa", x: 0, y: 7, level: 10);
        Assert.True(h.TickUntil(() => h.Me(gui).Health < h.Me(gui).MaxHealth * 0.6, 1500), $"vida {h.Me(gui).Health}/{h.Me(gui).MaxHealth}");
        h.Transport.ClientSends(bru, new CastIntent(Toque, null));
        Assert.True(h.TickUntil(() => h.Events(bru).Any(e => e.SkillId == Toque), 30));
        Assert.Equal(h.CharacterId(bru), h.Events(bru).First(e => e.SkillId == Toque).TargetId);
    }

    [Fact]
    public void BarreiraDeEter_AbsorveOGolpe()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 2.2, 0) });
        using var h = new MapHarness(w);
        var bru = h.Join("hazel", "bruxa", x: 0, y: 0, level: 10);
        h.Transport.ClientSends(bru, new CastIntent(Barreira, null));
        Assert.True(h.TickUntil(() => h.Me(bru).Shield > 0, 30));
        Assert.True(h.TickUntil(() => h.Events(bru).Any(e => e.AttackerId == "m_s" && e.Absorbed > 0), 200));
    }

    [Fact]
    public void AuraPrimal_EnfraqueceOsMonstrosPerto()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 3.0, 0) });
        using var h = new MapHarness(w);
        var bru = h.Join("hazel", "bruxa", x: 0, y: 0, level: 10);
        h.Transport.ClientSends(bru, new CastIntent(Aura, null));
        Assert.True(h.TickUntil(() => h.Monster(bru, "m_s")!.Weakened, 30));
    }

    private static int EmboscadaDano(bool escondido, out bool revelado)
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 12.0, 0, level: 30) });
        using var h = new MapHarness(w);
        var arq = h.Join("kaelen", "arqueiro", x: 0, y: 0, level: 30);
        h.Face(arq, 1, 0);
        if (escondido)
        {
            h.Transport.ClientSends(arq, new CastIntent(Passos, null));
            Assert.True(h.TickUntil(() => h.Me(arq).Stealth, 10), "furtivo: " + string.Join(",", h.Transport.Received<CastResultMessage>(arq).Select(r => r.SkillId + ":" + r.Failure)));
        }
        var m = h.Monster(arq, "m_s")!;
        h.Transport.ClientSends(arq, new CastIntent(Emboscada, null, m.X, m.Y));
        Assert.True(h.TickUntil(() => h.Events(arq).Any(e => e.SkillId == Emboscada), 40), "tiro: " + string.Join(",", h.Transport.Received<CastResultMessage>(arq).Select(r => r.SkillId + ":" + r.Failure)));
        revelado = !h.Me(arq).Stealth;
        return h.Events(arq).First(e => e.SkillId == Emboscada).Damage;
    }

    [Fact]
    public void PassosFantasma_Esconde_EAEmboscadaSaiMaisForte()
    {
        var normal = EmboscadaDano(false, out _);
        var ambush = EmboscadaDano(true, out var revelado);
        Assert.True(revelado, "atacar deveria revelar");
        Assert.True(ambush > normal * 1.6, $"emboscada {ambush} vs normal {normal}");
    }

    [Fact]
    public void OlharDoCacador_MarcaAsPresas()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("s", Raider, 10.0, 0) });
        using var h = new MapHarness(w);
        var arq = h.Join("kaelen", "arqueiro", x: 0, y: 0, level: 10);
        h.Transport.ClientSends(arq, new CastIntent(Olhar, null));
        Assert.True(h.TickUntil(() => h.Monster(arq, "m_s")!.Marked, 20));
        Assert.Contains(h.Transport.Received<NoticeMessage>(arq), n => n.Text.StartsWith("Olhar do Caçador"));
    }

    [Fact]
    public void PersonagemSemArma_EntraPeloLobby_ContinuaSemArma()
    {
        // 10/11 (pedido do usuário): ninguém ganha arma ao entrar; a arma base vem da 1ª missão ou do chão
        using var h = new MapHarness();
        var token = h.Precreate("legolas", "arqueiro", armed: false);
        var id = h.Login(token);
        Assert.Null(h.Me(id).Weapon);
        h.Transport.ClientDisconnects(id);
        var lob = h.Transport.Connect();
        h.Transport.ClientSends(lob, new LobbyRequest(token, Protocol.Version));
        h.Ticks(1);
        var ch = h.Transport.Received<LobbyMessage>(lob).Last().Characters.Single();
        h.Transport.ClientSends(lob, new EnterWorldIntent(ch.Id));
        h.Ticks(2);
        h.Remember(lob, ch.Id);
        Assert.Null(h.Me(lob).Weapon);
        Assert.DoesNotContain(h.Transport.Received<InventoryMessage>(lob).Last().Items, i => i.DefinitionId == "arco_arqueiro_galho_p0");
    }

    [Fact]
    public void MarcadoresDosNpcs_AmareloComMissaoNova_VerdeParaEntregar_SomeSemNada()
    {
        var world = MapHarness.RealWorld();
        using var h = new MapHarness(world);
        var id = h.Join("novata", "guerreiro", map: "porto_das_cinzas", x: 0, y: -17.5);
        h.Ticks(25);
        var markers = h.Transport.Received<NpcMarkersMessage>(id).Last().Markers;
        Assert.Equal("principal", markers["capita_ondina"]);        // primeira missão: caminho principal (★, 13/11)
        Assert.Equal("novo", markers["mestre_dorval"]);             // mentor do Guerreiro: prova do primeiro tomo
        Assert.False(markers.ContainsKey("isaura_arcanista"));       // mentora de outra classe: nada para o Guerreiro
    }

    [Fact]
    public void PersonagemNovo_SoTemOBasico_EAprendeOPoderComOTomo()
    {
        using var h = new MapHarness();
        var token = h.Temp.Db.ProvisionAccount("robin").Token;
        var id = h.Transport.Connect();
        h.Transport.ClientSends(id, new LobbyRequest(token, Protocol.Version));
        h.Ticks(1);
        h.Transport.ClientSends(id, new CreateCharacterIntent("Robin", "arqueiro"));
        h.Ticks(1);
        var ch = h.Transport.Received<LobbyMessage>(id).Last().Characters.Single();
        h.Transport.ClientSends(id, new EnterWorldIntent(ch.Id));
        h.Ticks(2);
        h.Remember(id, ch.Id);
        h.Transport.ClientSends(id, new CastIntent(Emboscada, null, 5, 20));
        h.Ticks(1);
        Assert.Equal("NotLearned", h.Transport.Received<CastResultMessage>(id).Last().Failure);

        var tome = h.Temp.Db.CreateInventoryItem(ch.Id, "tomo_arqueiro_emboscada", "comum", 1, "teste_tomo")!;
        h.Transport.ClientSends(id, new UseItemIntent(tome));
        h.Ticks(1);
        Assert.Contains(h.Transport.Received<NoticeMessage>(id), n => n.Text.Contains("aprendeu"));
        Assert.Contains("tomo:skill_arqueiro_emboscada", h.Temp.Db.GetFlags(ch.Id));
        h.Ticks(5);
        var before = h.Transport.Received<CastResultMessage>(id).Count;
        h.Transport.ClientSends(id, new CastIntent(Emboscada, null, 5, 20));
        h.Ticks(20);
        Assert.DoesNotContain(h.Transport.Received<CastResultMessage>(id).Skip(before), r => r.Failure == "NotLearned");
        Assert.Contains(h.Me(id).ClassId, "arqueiro");

        // tomo de nível alto: não deixa ler antes da hora
        var ult = h.Temp.Db.CreateInventoryItem(ch.Id, "tomo_arqueiro_fenix", "comum", 1, "teste_tomo2")!;
        h.Transport.ClientSends(id, new UseItemIntent(ult));
        h.Ticks(1);
        Assert.Equal("level_too_low", h.Transport.Received<ActionResultMessage>(id).Last().Reason);
    }

    [Fact]
    public void ChefeChamaAjudantesNaLuta_EElesSomemQuandoOChefeCai()
    {
        var w = MapHarness.TestWorld(new[] { MapHarness.Spawn("rei", "monster_r1_rei_das_pincas", 4, 0, level: 8) });
        using var h = new MapHarness(w);
        var id = h.Join("gui", x: 0, y: 0, level: 60);
        Assert.True(h.TickUntil(() => h.Transport.Received<NoticeMessage>(id).Any(n => n.Text.Contains("reforços")), 400));
        h.Ticks(2);
        Assert.Contains(h.LastSnapshot(id).Monsters, m => m.Id.StartsWith("m_rei_aj") && m.Alive);
        // chefe cai: ajudantes vão embora
        h.Face(id, 1, 0);
        for (var i = 0; i < 400 && h.Monster(id, "m_rei")?.Alive == true; i++)
        {
            var m = h.Monster(id, "m_rei")!;
            var me = h.Me(id);
            h.Face(id, m.X - me.X, m.Y - me.Y);
            h.Transport.ClientSends(id, new CastIntent("skill_guerreiro_corte", null));
            h.Ticks(8);
        }
        Assert.False(h.Monster(id, "m_rei")!.Alive);
        h.Ticks(25);
        Assert.DoesNotContain(h.LastSnapshot(id).Monsters, m => m.Id.StartsWith("m_rei_aj") && m.Alive);
    }
}

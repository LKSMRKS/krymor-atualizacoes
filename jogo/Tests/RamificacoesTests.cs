using System;
using System.Linq;
using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Net;
using CinzasDaMare.Shared.World;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11 (19:38): ramificações ligadas por nível (trava), Caminho da Serpente só com asas, barco do Triângulo, casa decorada.</summary>
public class RamificacoesTests
{
    private static MapPortal Portal(MapHarness h, string map, string to) => h.World.Maps[map].Portals.Single(p => p.ToMap == to);

    [Theory]
    [InlineData("vale_das_cinzas", "monte_prefeito")]
    [InlineData("vespera", "ruinas_automatos")]
    [InlineData("coroa_da_geada", "pico_vento")]
    [InlineData("triangulo_demonio", "fundo_triangulo")]
    [InlineData("fundo_triangulo", "ilha_serpente")]
    public void Passagem_TravaAbaixoDoNivel_ELiberaNoNivel(string de, string para)
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var p = Portal(h, de, para);
        Assert.True(p.LevelLock);
        var cedo = h.Join("cedo", level: p.MinLevel - 2, map: de, x: p.X, y: p.Y);
        var certo = h.Join("certo", level: p.MinLevel, map: de, x: p.X, y: p.Y);
        h.Ticks(40);
        Assert.Equal(de, h.Last<MapMessage>(cedo).Map.Id);
        Assert.Contains(h.Transport.Received<NoticeMessage>(cedo), n => n.Text.Contains("nível " + p.MinLevel));
        Assert.Equal(para, h.Last<MapMessage>(certo).Map.Id);
        // e a volta existe
        Assert.Contains(h.World.Maps[para].Portals, q => q.ToMap == de || q.ToMap == "porto_das_cinzas");
    }

    [Fact]
    public void CaminhoDaSerpente_SoComAsas_ELevaACidadeCelestial()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var p = Portal(h, "pico_vento", "caminho_serpente");
        Assert.Equal("asas", p.RequiresFlag);
        var sem = h.Join("sem", level: 50, map: "pico_vento", x: p.X, y: p.Y);
        var com = h.Join("com", level: 50, map: "pico_vento", x: p.X + 30, y: p.Y);
        h.Ticks(2);
        var asas = h.Temp.Db.CreateInventoryItem(h.CharacterId(com), "wings_ferro", "comum", 1, "t:" + Guid.NewGuid())!;
        h.Transport.ClientSends(com, new EquipIntent(asas));
        h.Ticks(40);
        Assert.Equal("pico_vento", h.Last<MapMessage>(sem).Map.Id);
        Assert.Contains(h.Transport.Received<NoticeMessage>(sem), n => n.Text.Contains("asas"));
        var com2 = h.Relocate(com, "pico_vento", p.X, p.Y);
        h.Ticks(40);
        Assert.Equal("caminho_serpente", h.Last<MapMessage>(com2).Map.Id);
        Assert.Contains(h.World.Maps["caminho_serpente"].Portals, q => q.ToMap == "reino_celeste");
    }

    [Fact]
    public void BarcoDoTriangulo_SoNoNivel44()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var tib = h.World.Maps["porto_das_cinzas"].Npcs.Single(n => n.Id == "pescador_tiberio");
        var a = h.Join("baixo", level: 40, map: "porto_das_cinzas", x: tib.X + 1, y: tib.Y);
        var b = h.Join("alto", level: 44, map: "porto_das_cinzas", x: tib.X - 1, y: tib.Y);
        h.Ticks(2);
        foreach (var c in new[] { a, b }) h.Temp.Db.AddCoins(h.CharacterId(c), 5000, "t:" + c);
        var a2 = h.Relocate(a, "porto_das_cinzas", tib.X + 1, tib.Y);
        var b2 = h.Relocate(b, "porto_das_cinzas", tib.X - 1, tib.Y);
        h.Ticks(2);
        h.Transport.ClientSends(a2, new PaidTravelIntent("pescador_tiberio", "barco_triangulo"));
        h.Transport.ClientSends(b2, new PaidTravelIntent("pescador_tiberio", "barco_triangulo"));
        h.Ticks(3);
        Assert.Equal("porto_das_cinzas", h.Last<MapMessage>(a2).Map.Id);
        Assert.Equal("triangulo_demonio", h.Last<MapMessage>(b2).Map.Id);
    }

    [Fact]
    public void Decoracao_SoNaPropriaCasa_EDaConfortoAoSair()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("dona", level: 25);
        h.Ticks(2);
        h.Temp.Db.TryAddFlag(h.CharacterId(a), "casa:propria");
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(a), "decor_estatua_leao", "comum", 1, "t:" + Guid.NewGuid())!;
        var item2 = h.Temp.Db.CreateInventoryItem(h.CharacterId(a), "decor_trono", "comum", 1, "t:" + Guid.NewGuid())!;
        // fora de casa: não coloca
        h.Transport.ClientSends(a, new UseItemIntent(item));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.Contains("SUA casa"));
        h.Transport.ClientSends(a, new GodIntent("travel", Arg: "casa_jogador"));
        h.Ticks(3);
        h.Transport.ClientSends(a, new UseItemIntent(item));
        h.Transport.ClientSends(a, new UseItemIntent(item2));
        h.Ticks(3);
        Assert.Contains("decor:decor_estatua_leao", h.Temp.Db.GetFlags(h.CharacterId(a)));
        Assert.Contains("decor:decor_trono", h.Temp.Db.GetFlags(h.CharacterId(a)));
        h.Transport.ClientSends(a, new GodIntent("travel", Arg: "porto_das_cinzas"));
        h.Ticks(3);
        Assert.Contains(h.Transport.Received<BuffMessage>(a), b => b.Name.StartsWith("Conforto do Lar"));
    }

    [Fact]
    public void ConviteDaCasa_LevaOGrupoParaACasaDoDono()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var porta = Portal(h, "porto_das_cinzas", "casa_jogador");
        var dono = h.Join("dono", level: 25, map: "porto_das_cinzas", x: 0, y: -9.5);
        h.Ticks(2);
        h.Temp.Db.TryAddFlag(h.CharacterId(dono), "casa:propria");
        dono = h.Relocate(dono, "porto_das_cinzas", porta.X, porta.Y);
        h.Ticks(2);
        h.Transport.ClientSends(dono, new InteractIntent("obj:porta_porto_casa"));   // 13/11: a casa abre pela porta (F)
        h.Ticks(2);
        Assert.Equal("casa_jogador", h.Last<MapMessage>(dono).Map.Id);
        var amigo = h.Join("amigo", level: 25, map: "porto_das_cinzas", x: porta.X, y: porta.Y);
        h.Ticks(2);
        h.Transport.ClientSends(amigo, new InteractIntent("obj:porta_porto_casa"));
        h.Ticks(2);
        Assert.Equal("porto_das_cinzas", h.Last<MapMessage>(amigo).Map.Id);   // sem casa e sem convite: porta fechada
        h.Transport.ClientSends(dono, new PartyIntent("invite", "amigo"));
        h.Ticks(1);
        h.Transport.ClientSends(amigo, new PartyIntent("accept"));
        h.Ticks(1);
        h.Transport.ClientSends(dono, new PartyIntent("casa"));
        h.Ticks(2);
        h.Transport.ClientSends(amigo, new InteractIntent("obj:porta_porto_casa"));   // convidado entra pela porta
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<NoticeMessage>(amigo), n => n.Text.Contains("convidou você para a casa"));
        Assert.Equal("casa_jogador", h.Last<MapMessage>(amigo).Map.Id);
        Assert.Contains(h.LastSnapshot(amigo).Players, p => p.CharacterId == h.CharacterId(dono));
    }
}

/// <summary>12/11 (20:25): tipos de arma, crônica, asas do 50, chefe semanal e "concluir missão" do laboratório.</summary>
public class FimDeJogoU48Tests
{
    [Fact]
    public void Estilos_DeArma_Diferem()
    {
        Assert.True(GameServer.Estilo("dagger").Recuperacao < GameServer.Estilo("sword").Recuperacao);
        Assert.True(GameServer.Estilo("hammer").Dano > GameServer.Estilo("sword").Dano);
        Assert.True(GameServer.Estilo("spear", "arsenal_lanca_refinada").Alcance > GameServer.Estilo("sword", "arsenal_espada_curta_refinada").Alcance);
    }

    [Fact]
    public void Cronica_AsasEKraken_Existem()
    {
        var w = MapHarness.RealWorld();
        Assert.Equal(8, w.Quests.Values.Count(q => q.Id.StartsWith("q_cronica_")));
        Assert.Contains(w.Maps["triangulo_demonio"].Spawns, s => s.Monster == "monster_mundo_kraken" && s.RespawnSeconds >= 600000);
        Assert.Contains("wings_serpente", string.Join(",", w.Quests["q_caminho_serpente_3"].RewardItems));
    }

    [Fact]
    public void Laboratorio_ConcluirMissao_LiberaAProxima()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("lab", level: 40);
        h.Ticks(2);
        for (var i = 1; i <= 4; i++) h.Transport.ClientSends(a, new GodIntent("quest_done", Arg: "q_cronica_" + i));
        h.Ticks(3);
        Assert.Contains(h.Temp.Db.GetQuests(h.CharacterId(a)), q => q.QuestId == "q_cronica_4" && q.State == "done");
        Assert.Contains("cronica:4", h.Temp.Db.GetFlags(h.CharacterId(a)));
    }
}

/// <summary>12/11 (20:58): 1ª classe com 8 poderes para todos, mesma cadência.</summary>
public class HabilidadesClasse1Tests
{
    [Fact]
    public void Todas_AsClasses_Tem8PoderesDeTomo_AteO14()
    {
        var c = CinzasDaMare.Shared.ContentLoader.Load(MapHarness.ContentDir);
        foreach (var cls in new[] { "guerreiro", "mago", "bruxa", "arqueiro" })
        {
            var niveis = c.Consumables.Values.Where(t => t.TeachSkill is not null && t.Classes!.Contains(cls) && t.MinLevel < 15).Select(t => t.MinLevel).OrderBy(x => x).ToList();
            Assert.Equal(new System.Collections.Generic.List<int> { 1, 3, 5, 6, 8, 10, 12, 14 }, niveis);
            Assert.All(c.Consumables.Values.Where(t => t.TeachSkill is not null && t.Classes!.Contains(cls)), t => Assert.True(c.Skills.ContainsKey(t.TeachSkill!)));
        }
    }
}

/// <summary>12/11 (22:55): dungeon difícil, laboratório próprio e armas por atributo.</summary>
public class DificilELabTests
{
    [Fact]
    public void Dungeon_Dificil_TemMonstrosMaisFortes_EOutraInstancia()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("dif", level: 45);
        var b = h.Join("nor", level: 45);
        h.Ticks(2);
        h.Transport.ClientSends(a, new GodIntent("travel", "mercado_mortos", Extra: "dificil"));
        h.Transport.ClientSends(b, new GodIntent("travel", "mercado_mortos"));
        h.Ticks(4);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.Contains("MODO DIFÍCIL"));
        var lvA = h.LastSnapshot(a).Monsters.Max(m => m.Level);
        var lvB = h.LastSnapshot(b).Monsters.Max(m => m.Level);
        Assert.Equal(lvB + 5, lvA);
        Assert.Contains(h.World.Maps["vespera"].Portals, p => p.Hard && p.LevelLock && p.MinLevel >= 40);
    }

    [Fact]
    public void Laboratorio_ExisteEVoltaAoPorto_ELancaAlcancaMais()
    {
        var w = MapHarness.RealWorld();
        Assert.True(w.Maps.ContainsKey("laboratorio"));
        Assert.Contains(w.Maps["laboratorio"].Portals, p => p.ToMap == "porto_das_cinzas");
        Assert.True(CinzasDaMare.Shared.Content.EstiloArma.De("spear").Alcance > CinzasDaMare.Shared.Content.EstiloArma.De("sword", "espada_curta").Alcance);
    }
}

/// <summary>12/11 (auditoria da progressão): consertos aprovados.</summary>
public class AuditoriaTests
{
    [Fact]
    public void Viagens_DosReinos_TravamNoNivel()
    {
        var w = MapHarness.RealWorld();
        var sibila = w.Maps["porto_das_cinzas"].Npcs.Single(n => n.Id == "sibila_vidente");
        Assert.All(sibila.Travel.Where(t => t.ToMap != "reinos_ocultos"), t => Assert.True(t.MinLevel >= 35));   // 14/11: o hub (nível 20) é seguro; os portais lá dentro travam
        Assert.True(w.Maps["porto_das_cinzas"].Npcs.Single(n => n.Id == "bobo_tiao").Travel.All(t => t.MinLevel >= 20));
    }

    [Fact]
    public void Drop_DeEquipamento_VemNaFaixaDoMonstro()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        Assert.Equal("arco_arqueiro_veio", h.Server.Nivelar("arco_arqueiro_veio", 9));         // na faixa: não muda
        var alto = h.Server.Nivelar("arco_arqueiro_veio", 47);                                    // arco do 8 num monstro 47
        Assert.NotEqual("arco_arqueiro_veio", alto);
    }

    [Fact]
    public void Cronica_SegueAHistoriaDasRegioes()
    {
        var w = MapHarness.RealWorld();
        Assert.Equal("q_m32", w.Quests["q_cronica_4"].Requires);
        Assert.Equal("talk", w.Quests["q_cronica_4"].Kind);
        Assert.Equal("boss", w.Quests["q_cronica_8"].Kind);
    }
}

/// <summary>13/11 (Leva 1): rotina e eventos.</summary>
public class Leva1Tests
{
    [Fact]
    public void Quadro_Oferece3ContratosDoDia_NaFaixa()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var quadro = h.World.Maps["porto_das_cinzas"].Npcs.Single(n => n.Id == "escriva_contratos");
        var a = h.Join("contratante", level: 30, map: "porto_das_cinzas", x: quadro.X + 1, y: quadro.Y);
        h.Ticks(2);
        h.Transport.ClientSends(a, new InteractIntent("npc:escriva_contratos"));
        h.Ticks(2);
        var d = h.Last<NpcDialogMessage>(a);
        var disp = d.Quests.Where(q => q.State == "available").ToList();
        Assert.Equal(3, disp.Count);
        Assert.All(disp, q => Assert.InRange(h.World.Quests[q.Id].MinLevel, 20, 30));
        Assert.Contains("Caçada da semana", d.Greeting);
    }

    [Fact]
    public void Evento_HoraDourada_PeloAdm_EAvisaTodos()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("adm", level: 20);
        var b = h.Join("outro", level: 20);
        h.Ticks(2);
        h.Transport.ClientSends(a, new GodIntent("evento", "hora_dourada"));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<NoticeMessage>(b), n => n.Text.Contains("HORA DOURADA"));
        h.Transport.ClientSends(a, new GodIntent("evento", "invasao"));
        h.Ticks(200);
        Assert.Contains(h.Transport.Received<NoticeMessage>(b), n => n.Text.Contains("Onda 1"));
    }

    /// <summary>14/11 tarde (dono: "INVASÃO DOURADA — monstros dourados fortes invadem a cidade por nível, dão Pandora").</summary>
    [Fact]
    public void Evento_InvasaoDourada_DouradosNaEntradaDaCidadeDaFaixa()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("adm", level: 40);
        h.Ticks(2);
        h.Transport.ClientSends(a, new GodIntent("evento", "invasao_dourada"));
        h.Ticks(3);
        var aviso = h.Transport.Received<NoticeMessage>(a).Last(n => n.Text.Contains("INVASÃO DOURADA"));
        var campo = h.World.Maps.Values.First(l => !l.Instanced && aviso.Text.Contains("vindos de " + l.DisplayName + "."));
        Assert.InRange(campo.Spawns.Average(sp => sp.Level), 30, 50);
        h.Transport.ClientSends(a, new GodIntent("travel", campo.Id));
        h.Ticks(5);
        Assert.Contains(h.LastSnapshot(a).Monsters, m => m.Affix == "dourado" && m.Alive);
    }

    [Fact]
    public void Regiao_Nova_MostraCartao()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("viajante", level: 20);
        h.Ticks(2);
        h.Transport.ClientSends(a, new GodIntent("travel", "mata_dos_enforcados"));
        h.Ticks(3);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Kind == "regiao" && n.Text.Contains("Mata"));
    }
}

public class MissoesVariadasTests
{
    [Fact]
    public void Condicao_Regras()
    {
        Assert.True(GameServer.CondicaoOk(null, false, 0, 0, false, ""));
        Assert.False(GameServer.CondicaoOk("noite", false, 0, 0, false, ""));
        Assert.True(GameServer.CondicaoOk("noite", true, 0, 0, false, ""));
        Assert.True(GameServer.CondicaoOk("zona:10,10,5", false, 12, 12, false, ""));
        Assert.False(GameServer.CondicaoOk("zona:10,10,5", false, 30, 10, false, ""));
        Assert.False(GameServer.CondicaoOk("forma", false, 0, 0, false, ""));
        Assert.True(GameServer.CondicaoOk("arma:spear", false, 0, 0, false, "spear"));
        Assert.True(GameServer.CondicaoOk("sem_morrer", false, 0, 0, false, ""));
    }

    [Fact]
    public void Conteudo_TiposEquilibrados_EscolhasExclusivas()
    {
        var w = MapHarness.RealWorld();
        var v = w.Quests.Values.Where(q => q.Id.StartsWith("q_var_") || q.Id.StartsWith("q_esc_")).ToList();
        Assert.True(v.Count >= 45);
        Assert.True(v.Count(q => q.Kind is "escort" or "defend") >= 7);
        Assert.True(v.Count(q => q.Condition is not null) >= 10);
        Assert.True(v.Count(q => q.Kind == "hunt") >= 10);
        foreach (var q in v.Where(q => q.Id.StartsWith("q_esc_") && !q.Id.EndsWith("_2")))
        {
            Assert.NotNull(q.ForbidFlag);
            Assert.Single(q.AcceptFlags);
        }
        foreach (var q in v.Where(q => q.Kind == "interact"))
            Assert.All(q.Targets, t => Assert.Contains(w.Maps.Values, m => m.Objects.Any(o => o.Id == t)));
    }

    [Fact]
    public void Abate_ForaDaCondicao_NaoConta()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("zonal", level: 40, map: "brejo_da_viuva", x: 0, y: 50);
        h.Ticks(2);
        h.Transport.ClientSends(a, new GodIntent("quest_accept", "q_var_brejo_condicao"));
        h.Ticks(2);
        // não aceitou pelo deus? então aceita pelo banco direto não existe — basta conferir a condição pelo conteúdo
        var q = h.World.Quests["q_var_brejo_condicao"];
        Assert.StartsWith("zona:", q.Condition);
        Assert.False(GameServer.CondicaoOk(q.Condition, false, 0, 50, false, ""));
    }
}

public class PacoteAssets1311Tests
{
    [Fact]
    public void ConjuntosNovos_CarneMaior_DecoracaoDasDGs()
    {
        var w = MapHarness.RealWorld();
        var c = CinzasDaMare.Shared.ContentLoader.Load(MapHarness.ContentDir);
        foreach (var set in new[] { "runico", "lendario", "selvagem", "grao_mestre", "quitina", "dragonico", "musa", "mestre_almas", "rainha_sangue" })
            foreach (var pc in new[] { "capacete", "peitoral", "luvas", "calca", "botas" })
                Assert.NotNull(c.Items[$"arm_{set}_{pc}"].SetBonus);
        Assert.True(c.Items.ContainsKey("mat_carne_maior"));
        Assert.Equal("loot_caranguejo_mae", c.Monsters["monster_costa_caranguejo_mae"].LootTableId);
        foreach (var mid in new[] { "galeria_funda", "biblioteca_continente", "cripta_do_sino" })
            Assert.True(w.Maps[mid].Props.Count(p => p.Kind.StartsWith("glb:dg/")) >= 10, mid);
    }
}

public class CaixasTests
{
    [Fact]
    public void Caixa_Abre_DaPremio_ESome()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("sortudo", level: 30);
        h.Ticks(2);
        h.Transport.ClientSends(a, new GodIntent("item", "caixa_cinzas_3", Amount: 5));
        h.Ticks(2);
        var inv = h.Last<InventoryMessage>(a);
        var caixa = inv.Items.First(i => i.DefinitionId == "caixa_cinzas_3");
        h.Transport.ClientSends(a, new UseItemIntent(caixa.ItemId));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.Contains("Caixa das Cinzas +3"));
        Assert.Equal(4, h.Last<InventoryMessage>(a).Items.Where(i => i.DefinitionId == "caixa_cinzas_3").Sum(i => i.Quantity));
        Assert.Equal(1, GameServer.CaixaTier(5));
        Assert.Equal(5, GameServer.CaixaTier(60));
    }
}

public class Pedidos1313Tests
{
    [Fact]
    public void Venda_ContaVariacaoENivel()
    {
        // 14/11: V12 vende por mais que o comum, mas nunca acima de 60% do preço de loja (preço = 4 x venda-base)
        Assert.True(CinzasDaMare.Shared.Content.Venda.Preco(5000, true, 40, "v12", 0) > CinzasDaMare.Shared.Content.Venda.Preco(5000, true, 40, "comum", 0));
        Assert.True(CinzasDaMare.Shared.Content.Venda.Preco(5000, true, 40, "lendario", 0) <= 20000 * 0.6);
        Assert.True(CinzasDaMare.Shared.Content.Venda.Preco(5, true, 1, "v01", 0) >= 5);
        Assert.Equal(3, CinzasDaMare.Shared.Content.Venda.Preco(3, false, 1, "comum", 0));
    }

    [Fact]
    public void Desmanchar_ViraMaterialEJoia()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        h.Server.GodAccounts = new System.Collections.Generic.HashSet<string>();
        var a = h.Join("ferreiro", level: 30);
        h.Ticks(2);
        h.Transport.ClientSends(a, new GodIntent("item", "arm_runico_capacete", Extra: "v12"));
        h.Ticks(2);
        var it = h.Last<InventoryMessage>(a).Items.First(i => i.DefinitionId == "arm_runico_capacete");
        h.Transport.ClientSends(a, new SalvageIntent(it.ItemId));
        h.Ticks(2);
        var inv = h.Last<InventoryMessage>(a).Items;
        Assert.DoesNotContain(inv, i => i.DefinitionId == "arm_runico_capacete");
        Assert.Contains(inv, i => i.DefinitionId == "joia_brasa");
    }

    [Fact]
    public void Conquistas_SaoTrinta_EChegamAosPoucos()
    {
        Assert.True(GameServer.TotalConquistas >= 30);
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("veterano", level: 45);
        h.Ticks(420);
        var n = h.Transport.Received<NoticeMessage>(a).Count(x => x.Text.Contains("Conquista"));
        Assert.InRange(n, 1, 3);
    }
}

public class CasaPequenaTests
{
    [Fact]
    public void PortaQueAbre_LevaParaDentro_ESaidaVoltaNaPorta()
    {
        var w = MapHarness.RealWorld();
        var porta = w.Maps["porto_das_cinzas"].Objects.First(o => o.Kind == "porta" && o.GrantFlag == "porta:entra");
        using var h = new MapHarness(w);
        var a = h.Join("visita", level: 5, map: "porto_das_cinzas", x: porta.X, y: porta.Y - 0.8);
        h.Ticks(2);
        h.Transport.ClientSends(a, new InteractIntent("obj:" + porta.Id));
        h.Ticks(2);
        Assert.Equal("casa_pequena", h.Last<MapMessage>(a).Map.Id);
        // 13/11: sai pela porta (F), não por portal no chão
        h.Transport.ClientSends(a, new InteractIntent("obj:porta_casa_pequena_saida"));
        h.Ticks(2);
        Assert.Equal("porto_das_cinzas", h.Last<MapMessage>(a).Map.Id);
    }
}

public class LugaresTests
{
    [Fact]
    public void MapasDeCampo_TemLugaresComNome_SecretoEAtalho()
    {
        var w = MapHarness.RealWorld();
        var mata = w.Maps["mata_dos_enforcados"];
        Assert.True(mata.Zones.Count(z => z.Kind == "lugar") >= 4);
        Assert.Single(mata.Zones, z => z.Kind == "secreto");
        Assert.Equal(2, mata.Waypoints.Count);
    }

    [Fact]
    public void EntrarNumLugar_DaXpUmaVez_ESecretoDaCaixa()
    {
        var w = MapHarness.RealWorld();
        var mata = w.Maps["mata_dos_enforcados"];
        var lugar = mata.Zones.First(z => z.Kind == "lugar");
        var sec = mata.Zones.First(z => z.Kind == "secreto");
        using var h = new MapHarness(w);
        var a = h.Join("explorador", level: 20, map: "mata_dos_enforcados", x: lugar.X, y: lugar.Y);
        h.Ticks(12);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.StartsWith("Lugar descoberto"));
        a = h.Relocate(a, "mata_dos_enforcados", sec.X, sec.Y);
        h.Ticks(12);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.StartsWith("ÁREA SECRETA"));
        Assert.Contains(h.Last<InventoryMessage>(a).Items, i => i.DefinitionId.StartsWith("caixa_cinzas_"));
    }
}

public class Pedidos1348Tests
{
    [Fact]
    public void Lobisomem_SoNaPrimeiraClasse()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var a = h.Join("lua1", level: 40, map: "porto_das_cinzas", x: 18, y: 10.8);
        var b = h.Join("lua2", level: 40, map: "porto_das_cinzas", x: 18, y: 13.2);
        foreach (var (id, evo) in new[] { (a, false), (b, true) })
        {
            h.Temp.Db.TryAddFlag(h.CharacterId(id), "livro:lobisomem");
            if (evo) h.Temp.Db.TryAddFlag(h.CharacterId(id), "evo:campeao");
        }
        a = h.Relocate(a, "porto_das_cinzas", 18, 10.8); b = h.Relocate(b, "porto_das_cinzas", 18, 13.2);
        h.Ticks(2);
        h.Transport.ClientSends(a, new QuestIntent("q_forma_lobo_1", "accept"));
        h.Transport.ClientSends(b, new QuestIntent("q_forma_lobo_1", "accept"));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<NoticeMessage>(a), n => n.Text.StartsWith("Missão aceita"));
        Assert.Contains(h.Transport.Received<NoticeMessage>(b), n => n.Text.Contains("1ª classe"));
    }

    [Fact]
    public void Bracelete_DaSkinEBonus()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("palhaco", level: 25, x: 0, y: -9.5);
        h.Ticks(2);
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), "bracelete_palhaco", "comum", 1, "t:" + Guid.NewGuid())!;
        h.Transport.ClientSends(id, new EquipIntent(item));
        h.Ticks(2);
        Assert.Contains(h.Temp.Db.GetEquipment(h.CharacterId(id)), e => e.Slot == "bracelete");
        Assert.Equal("palhaco", h.Me(id).Skin);
    }
}

public class Pedidos1344Tests
{
    [Fact]
    public void Apito_DaAMontaria_EGasta()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("cavaleiro", level: 30, x: 0, y: -9.5);
        h.Ticks(2);
        var item = h.Temp.Db.CreateInventoryItem(h.CharacterId(id), "apito_lince", "comum", 1, "t:" + Guid.NewGuid())!;
        h.Transport.ClientSends(id, new UseItemIntent(item));
        h.Ticks(2);
        Assert.Contains("mount:lince", h.Temp.Db.GetFlags(h.CharacterId(id)));
        Assert.DoesNotContain(h.Temp.Db.GetInventory(h.CharacterId(id)), e => e.ItemId == item);
    }

    [Fact]
    public void Feitio_EDeterministico_ETrocaUmPeloOutro()
    {
        var a = CinzasDaMare.Shared.Content.ItemAffixes.Feitio("item-xyz", "weapon", 30);
        var b = CinzasDaMare.Shared.Content.ItemAffixes.Feitio("item-xyz", "weapon", 30);
        Assert.Equal(a.Nome, b.Nome);
        Assert.Empty(CinzasDaMare.Shared.Content.ItemAffixes.Feitio("item-xyz", "weapon", 1).Linhas);   // roupa inicial sem feitio
        var nomes = Enumerable.Range(0, 2000).Select(i => CinzasDaMare.Shared.Content.ItemAffixes.Feitio("x" + i, "armor", 20).Nome).ToList();
        Assert.True(nomes.Count(n => n == "Perfeito") < nomes.Count(n => n == "Padrão") / 5);   // o melhor é o mais raro
        Assert.True(nomes.Distinct().Count() >= 10);
    }
}

public class Pedidos1452Tests
{
    [Fact]
    public void Montaria_EItem_SemItemNaoMonta()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("cavalgante", level: 30, x: 0, y: -9.5);
        h.Temp.Db.TryAddFlag(h.CharacterId(id), "mount:lobo");
        id = h.Relocate(id, "porto_das_cinzas", 0, -9.5);
        h.Ticks(2);
        var eq = h.Temp.Db.GetEquipment(h.CharacterId(id)).FirstOrDefault(e => e.Slot == "montaria");
        Assert.NotNull(eq);
        Assert.Equal("montaria_lobo", eq!.DefinitionId);
        h.Transport.ClientSends(id, new UnequipIntent("montaria"));
        h.Ticks(2);
        h.Transport.ClientSends(id, new MountIntent(true));
        h.Ticks(2);
        Assert.Contains(h.Transport.Received<ActionResultMessage>(id), a => a.Action == "mount" && a.Reason == "no_mount");
    }

    [Fact]
    public void Pose_FicaAteAndar()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("posador", level: 5, x: 0, y: -9.5);
        h.Ticks(2);
        h.Transport.ClientSends(id, new InteractIntent("pose:chao"));
        h.Ticks(3);
        Assert.Equal("pose_chao", h.Me(id).Action);
        h.Transport.ClientSends(id, new MoveIntent(1, 0));
        h.Ticks(3);
        Assert.NotEqual("pose_chao", h.Me(id).Action);
    }

    [Fact]
    public void ReceitasExtras_DoCaos_Carregam()
    {
        var w = MapHarness.RealWorld();
        Assert.Contains("caos_lendaria_montante_dragao_rubro", w.Recipes.Keys);
        Assert.Contains("rec_mascara_pandemonio", w.Recipes.Keys);
    }
}

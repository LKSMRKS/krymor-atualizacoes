using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Client;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

public class WorldInfoAndCooldownTests
{
    [Fact]
    public void Servidor_EnviaOsDadosDoMundo_AposOLogin()
    {
        using var h = new ServerHarness();
        var a = h.Login("alice");

        var info = h.Transport.Received<WorldInfoMessage>(a).Single();

        Assert.Equal(h.Balance.ArenaHalfSize, info.ArenaHalfSize, 6);
        Assert.Equal(h.Balance.GuardArcDegrees, info.GuardArcDegrees, 6);
        Assert.Equal("arena", info.MapId);
    }

    [Fact]
    public void PosicaoSalvaForaDaArena_EhTrazidaParaDentroAoEntrar()
    {
        using var h = new ServerHarness();
        var (token, characterId) = h.Precreate("alice");
        h.Temp.Db.SaveCharacterState(characterId, h.Balance.StartingLevel, 0, GameServer.MapId, 25, -35, 200);

        var a = h.Login("alice", token);

        var me = h.Me(a);
        Assert.Equal(h.Balance.ArenaHalfSize, me.X, 6);
        Assert.Equal(-h.Balance.ArenaHalfSize, me.Y, 6);
    }

    [Fact]
    public void Modelo_GuardaOsDadosDoMundo_ERestauraOsPadroesNoReset()
    {
        var m = new ClientModel();
        Assert.Equal(20.0, m.ArenaHalfSize, 6);

        m.Apply(new WorldInfoMessage(35, 90, "arena"));
        Assert.Equal(35.0, m.ArenaHalfSize, 6);
        Assert.Equal(90.0, m.GuardArcDegrees, 6);
        Assert.Equal("arena", m.MapId);

        m.Reset();
        Assert.Equal(20.0, m.ArenaHalfSize, 6);
        Assert.Equal(120.0, m.GuardArcDegrees, 6);
    }

    [Fact]
    public void ArenaDoBalance_EMenorQueAAnterior_EContinuaValida()
    {
        var b = CinzasDaMare.Shared.ContentLoader.LoadBalance(Path.Combine(AppContext.BaseDirectory, "Content"));
        Assert.Equal(20.0, b.ArenaHalfSize, 6);
        Assert.Empty(CinzasDaMare.Server.Content.ContentValidator.ValidateBalance(b));
    }

    [Fact]
    public void Recarga_ComecaCheia_DecaiAteZero_ENaoFicaNegativa()
    {
        var t = new CooldownTracker();
        Assert.Equal(0.0, t.Fraction(ClientModel.SkillGolpe, 0), 9);

        t.StartSkill(ClientModel.SkillGolpe, 1_000);
        Assert.Equal(1.0, t.Fraction(ClientModel.SkillGolpe, 1_000), 9);
        Assert.Equal(0.5, t.Fraction(ClientModel.SkillGolpe, 3_000), 9);
        Assert.Equal(1_000, t.RemainingMs(ClientModel.SkillGolpe, 4_000));
        Assert.Equal(0.0, t.Fraction(ClientModel.SkillGolpe, 5_000), 9);
        Assert.Equal(0, t.RemainingMs(ClientModel.SkillGolpe, 99_000));
    }

    [Fact]
    public void Recarga_DaPocao_E_Reset()
    {
        var t = new CooldownTracker();
        t.Start(CooldownTracker.Potion, 0, CooldownTracker.PotionMs);
        Assert.Equal(CooldownTracker.PotionMs, t.RemainingMs(CooldownTracker.Potion, 0));
        t.Reset();
        Assert.Equal(0, t.RemainingMs(CooldownTracker.Potion, 0));
        t.StartSkill("skill_desconhecida", 0);   // ignora habilidade sem duração conhecida
        Assert.Equal(0, t.RemainingMs("skill_desconhecida", 0));
    }

    [Fact]
    public void DuracoesDoHud_ConferemComOsDadosDoServidor()
    {
        var content = TestContent.Load();
        foreach (var (skillId, ms) in CooldownTracker.SkillMs)
            Assert.Equal(content.Skills[skillId].CooldownMs, ms);
        Assert.Equal(CooldownTracker.PotionMs, content.Consumables["consumable_c01_tonico_fraco"].CooldownMs);
    }
}

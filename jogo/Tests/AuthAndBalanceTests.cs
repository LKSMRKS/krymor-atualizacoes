using CinzasDaMare.Server.Content;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;

namespace CinzasDaMare.Tests;

public class AuthTests
{
    [Fact]
    public void TokenProvisionado_Autentica_ErradoOuRevogado_Nao()
    {
        using var t = new TempDb();
        var (accountId, token) = t.Db.ProvisionAccount("alice");

        Assert.Equal(accountId, t.Db.AuthenticateToken(token));
        Assert.Null(t.Db.AuthenticateToken("outro-token"));
        Assert.Null(t.Db.AuthenticateToken(""));
        Assert.Null(t.Db.AuthenticateToken(new string('x', 500)));

        Assert.True(t.Db.RevokeAccount("alice"));
        Assert.Null(t.Db.AuthenticateToken(token));
        Assert.False(t.Db.RevokeAccount("ninguem"));
    }

    [Fact]
    public void OBancoGuardaSoOHash_EOHashNaoServeComoToken()
    {
        using var t = new TempDb();
        var (_, token) = t.Db.ProvisionAccount("alice");
        var hash = TokenHasher.Hash(token);

        Assert.NotEqual(token, hash);
        Assert.Equal(64, hash.Length);
        Assert.Null(t.Db.AuthenticateToken(hash));
    }

    [Fact]
    public void TokensGeradosSaoLongosEDistintos()
    {
        var a = TokenHasher.NewToken();
        var b = TokenHasher.NewToken();
        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 40);
    }

    [Fact]
    public void ItemSemente_EhCriadoUmaVez_ENaoReapareceDepoisDeColetado()
    {
        using var t = new TempDb();
        var c = t.NewCharacter("A");

        var first = t.Db.EnsureSeedItem("semente", "weapon_guerreiro_sword_p1", "comum");
        var again = t.Db.EnsureSeedItem("semente", "weapon_guerreiro_sword_p1", "comum");
        Assert.NotNull(first);
        Assert.Equal(first, again);

        Assert.True(t.Db.TryClaimWorldItem("r1", first!, c).Success);
        Assert.Null(t.Db.EnsureSeedItem("semente", "weapon_guerreiro_sword_p1", "comum"));
    }
}

public class BalanceTests
{
    private static BalanceConfig Load() => ContentLoader.LoadBalance(Path.Combine(AppContext.BaseDirectory, "Content"));

    [Fact]
    public void ParametrosProvisorios_VemDosDados_EPassamNaValidacao()
    {
        var b = Load();
        Assert.Equal(10, b.StartingStrength);
        Assert.Equal(10, b.StartingVigor);
        Assert.Equal(40, b.InventoryCapacity);
        Assert.Equal(2.5, TestContent.Load().Skills[TestContent.Golpe].Range, 6);
        Assert.Empty(ContentValidator.ValidateBalance(b));
    }

    [Fact]
    public void Validador_RejeitaBalanceamentoIncoerente()
    {
        var bad = Load() with
        {
            InventoryCapacity = 0,
            MonsterAggroRadiusM = 1.0,
            MonsterSpawnX = 999,
        };

        var errors = ContentValidator.ValidateBalance(bad);

        Assert.Contains(errors, e => e.Contains("inventory_capacity"));
        Assert.Contains(errors, e => e.Contains("aggro"));
        Assert.Contains(errors, e => e.Contains("monster_spawn"));
    }

    [Fact]
    public void CapacidadeDeInventario_ConfiguravelNoBanco()
    {
        using var t = new TempDb(inventoryCapacity: 2);
        var c = t.NewCharacter("A");
        Assert.True(t.Db.TryClaimWorldItem("r1", t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "e1"), c).Success);
        Assert.True(t.Db.TryClaimWorldItem("r2", t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "e2"), c).Success);
        var third = t.Db.TryClaimWorldItem("r3", t.Db.CreateWorldItem("weapon_guerreiro_sword_p1", "comum", "e3"), c);
        Assert.Equal("inventory_full", third.Reason);
    }
}

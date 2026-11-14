using CinzasDaMare.Server.Net;
using CinzasDaMare.Shared.Content;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>12/11: variações V1–V15 das peças de conjunto (regra de drop estilo MU).</summary>
public class VariacaoTests
{
    [Fact]
    public void Teto_DependeDeQuemSolta()
    {
        Assert.Equal(3, GameServer.VariationCap(7, false, false, false));     // nível 7 comum: até V3/V4
        Assert.True(GameServer.VariationCap(60, false, false, false) <= 9);   // monstro comum nunca passa da Prata
        Assert.True(GameServer.VariationCap(60, true, false, false) <= 11);
        Assert.True(GameServer.VariationCap(35, false, true, false) <= 12);   // chefe abaixo do 40: até Ouro
        Assert.Equal(13, GameServer.VariationCap(45, false, true, false));    // chefe 45: Ouro brilhante
        Assert.Equal(15, GameServer.VariationCap(60, false, true, false));    // só chefe 50+ alcança a Full
    }

    [Fact]
    public void Full_TemOitoLinhas_NoMaximo_EV1NaoTemNenhuma()
    {
        Assert.Equal(8, ItemAffixes.Roll("item-x", "v15", "armor", 50).Count);
        Assert.Empty(ItemAffixes.Roll("item-x", "v01", "armor", 50));
        Assert.Equal(15, Variacao.Of("v15"));
        Assert.Equal(0, Variacao.Of("excelente"));
        var fraca = ItemAffixes.Roll("item-y", "v09", "armor", 30);
        Assert.Equal(3, fraca.Count);
    }
}

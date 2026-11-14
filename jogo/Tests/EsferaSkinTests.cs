using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Tests;

/// <summary>14/11: esfera do macaco vai para a mochila; olhinho do bracelete esconde a skin.</summary>
public class EsferaSkinTests
{
    [Fact]
    public void Esfera_VaiParaAMochila()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("eva", x: 101.6, y: 37.4, level: 20, map: "costa_do_cal");
        var ch = h.CharacterId(id);
        h.Transport.ClientSends(id, new InteractIntent("obj:esfera_macaco_1"));
        h.Ticks(2);
        Assert.Equal(1, h.Temp.Db.CountInInventory(ch, "esfera_macaco_1"));
        // de novo: não duplica
        h.Transport.ClientSends(id, new InteractIntent("obj:esfera_macaco_1"));
        h.Ticks(2);
        Assert.Equal(1, h.Temp.Db.CountInInventory(ch, "esfera_macaco_1"));
    }

    [Fact]
    public void Esfera_QuemJaPegouRecebeAoEntrar()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("ivo", level: 20);
        var ch = h.CharacterId(id);
        h.Temp.Db.TryAddFlag(ch, "esfera:3");
        id = h.Relocate(id, "porto_das_cinzas", 0, 0);
        h.Ticks(1);
        Assert.Equal(1, h.Temp.Db.CountInInventory(ch, "esfera_macaco_3"));
        id = h.Relocate(id, "porto_das_cinzas", 0, 0);
        h.Ticks(1);
        Assert.Equal(1, h.Temp.Db.CountInInventory(ch, "esfera_macaco_3"));
    }

    [Fact]
    public void Olhinho_EscondeASkin()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("leo", level: 20);
        var ch = h.CharacterId(id);
        var br = h.Temp.Db.CreateInventoryItem(ch, "bracelete_cartola", "comum", 1, "t")!;
        h.Transport.ClientSends(id, new EquipIntent(br));
        h.Ticks(2);
        Assert.False(string.IsNullOrEmpty(h.Me(id).Skin));
        h.Transport.ClientSends(id, new SkinVisivelIntent(false));
        h.Ticks(2);
        Assert.True(string.IsNullOrEmpty(h.Me(id).Skin));
        h.Transport.ClientSends(id, new SkinVisivelIntent(true));
        h.Ticks(2);
        Assert.False(string.IsNullOrEmpty(h.Me(id).Skin));
    }
}

/// <summary>14/11: pilhas antigas de 20 se juntam (agora cabem 100).</summary>
public class PilhasTests
{
    [Fact]
    public void Pilhas_SeJuntam_NoOrganizar()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("ana", level: 10);
        var ch = h.CharacterId(id);
        for (var i = 0; i < 4; i++) h.Temp.Db.CreateInventoryItem(ch, "consumable_c01_tonico_fraco", "comum", 20, "p" + i);
        var antes = h.Temp.Db.CountInInventory(ch, "consumable_c01_tonico_fraco");
        h.Transport.ClientSends(id, new MoveSlotIntent("@organizar", -1));
        h.Ticks(2);
        Assert.Equal(antes, h.Temp.Db.CountInInventory(ch, "consumable_c01_tonico_fraco"));
        Assert.Equal(1, h.Temp.Db.GetInventory(ch).Count(i => i.DefinitionId == "consumable_c01_tonico_fraco"));
    }
}

/// <summary>14/11: menu do TAB — objetivos dão cristais azuis da conta, que compram itens.</summary>
public class ObjetivosTests
{
    [Fact]
    public void LoginDoDia_DaCristais_EComprarGasta()
    {
        using var h = new MapHarness(MapHarness.RealWorld());
        var id = h.Join("ana", level: 30);
        h.Transport.ClientSends(id, new ObjetivosIntent("abrir"));
        h.Ticks(2);
        var lista = h.Last<ObjetivosMessage>(id);
        Assert.Equal(1, lista.Lista.First(o => o.Id == "d_login").Progresso);
        h.Transport.ClientSends(id, new ObjetivosIntent("resgatar", "d_login"));
        h.Ticks(2);
        Assert.Equal(5, h.Last<ObjetivosMessage>(id).Cristais);
        // de novo no mesmo dia: não paga outra vez
        h.Transport.ClientSends(id, new ObjetivosIntent("resgatar", "d_login"));
        h.Ticks(2);
        Assert.Equal(5, h.Last<ObjetivosMessage>(id).Cristais);
        // sem cristais suficientes não compra
        h.Transport.ClientSends(id, new ObjetivosIntent("comprar", "caixa_cinzas_3"));
        h.Ticks(2);
        Assert.Equal(0, h.Temp.Db.CountInInventory(h.CharacterId(id), "caixa_cinzas_3"));
    }
}

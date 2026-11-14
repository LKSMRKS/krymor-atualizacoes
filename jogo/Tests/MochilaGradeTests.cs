using System.Linq;
using CinzasDaMare.Shared.Net;
using Xunit;

namespace CinzasDaMare.Tests;

/// <summary>10/11: mochila em grade estilo MU — cada item ocupa o seu retângulo, nada se sobrepõe.</summary>
public class MochilaGradeTests
{
    private static (int W, int H) Size(string def) => def.StartsWith("armor_") ? (2, 3) : def.StartsWith("weapon_") ? (1, 3) : (1, 1);

    [Fact]
    public void Itens_OcupamOSeuTamanho_ESemSobrepor()
    {
        using var t = new TempDb(40);
        t.Db.GridColumns = 8;
        t.Db.ItemSize = Size;
        var c = t.NewCharacter("ana");
        var armor = t.Db.CreateInventoryItem(c, "armor_gibao_acolchoado", "comum", 1, "a")!;
        var sword = t.Db.CreateInventoryItem(c, "weapon_guerreiro_sword_p1", "comum", 1, "b")!;
        var potion = t.Db.CreateInventoryItem(c, "consumable_c01_tonico_fraco", "comum", 1, "c")!;
        var inv = t.Db.GetInventory(c).ToDictionary(e => e.ItemId, e => e.Slot);
        Assert.Equal(0, inv[armor]);      // 2×3 no canto
        Assert.Equal(2, inv[sword]);      // ao lado da armadura (colunas 0–1 ocupadas)
        Assert.Equal(3, inv[potion]);
        // soltar a poção em cima da armadura (casa 9 = linha 1, coluna 1): recusado
        Assert.False(t.Db.TryMoveInventorySlot(c, potion, 9));
        // soltar a espada na linha 3 (casa 24): cabe (1×3 nas linhas 3–5? só há 5 linhas: 3,4 → não cabe)
        Assert.False(t.Db.TryMoveInventorySlot(c, sword, 32));
        Assert.True(t.Db.TryMoveInventorySlot(c, sword, 7));   // última coluna, linhas 0–2
    }

    [Fact]
    public void Trocar_DoisItensDoMesmoTamanho_NaoQuebraOBanco()
    {
        using var t = new TempDb(40);
        t.Db.GridColumns = 8;
        t.Db.ItemSize = Size;
        var c = t.NewCharacter("cai");
        var p1 = t.Db.CreateInventoryItem(c, "consumable_c01_tonico_fraco", "comum", 1, "p1")!;
        var p2 = t.Db.CreateInventoryItem(c, "mat_casca_cal", "comum", 1, "p2")!;
        var inv = t.Db.GetInventory(c).ToDictionary(e => e.ItemId, e => e.Slot);
        Assert.True(t.Db.TryMoveInventorySlot(c, p1, inv[p2]));   // troca (log de 11/11: UNIQUE constraint failed)
        var after = t.Db.GetInventory(c).ToDictionary(e => e.ItemId, e => e.Slot);
        Assert.Equal(inv[p2], after[p1]);
        Assert.Equal(inv[p1], after[p2]);
    }

    [Fact]
    public void Arrumacao_TiraSobreposicao_DeMochilaAntiga()
    {
        using var t = new TempDb(40);
        var c = t.NewCharacter("bia");
        // mochila antiga: casas soltas 0,1,2 (sem grade)
        var a = t.Db.CreateInventoryItem(c, "armor_gibao_acolchoado", "comum", 1, "a")!;
        var b = t.Db.CreateInventoryItem(c, "armor_gibao_acolchoado", "comum", 1, "b")!;
        t.Db.CreateInventoryItem(c, "consumable_c01_tonico_fraco", "comum", 1, "c");
        t.Db.GridColumns = 8;
        t.Db.ItemSize = Size;
        Assert.Equal(0, t.Db.NormalizeInventory(c));
        var inv = t.Db.GetInventory(c).ToDictionary(e => e.ItemId, e => e.Slot);
        Assert.Equal(0, inv[a]);
        Assert.Equal(3, inv[b]);           // a 2ª armadura não pode mais começar na casa 1 (a poção ficou na 2)
    }

    [Fact]
    public void Organizar_GrandesPrimeiro_EIguaisJuntos()
    {
        using var t = new TempDb(40);
        t.Db.GridColumns = 8;
        t.Db.ItemSize = Size;
        var c = t.NewCharacter("cid");
        var p1 = t.Db.CreateInventoryItem(c, "consumable_c01_tonico_fraco", "comum", 1, "a")!;
        var armor = t.Db.CreateInventoryItem(c, "armor_gibao_acolchoado", "comum", 1, "b")!;
        var p2 = t.Db.CreateInventoryItem(c, "consumable_c01_tonico_fraco", "comum", 1, "c")!;
        Assert.True(t.Db.TryMoveInventorySlot(c, p2, 39));   // poção solta lá no fim
        Assert.True(t.Db.OrganizeInventory(c));
        var inv = t.Db.GetInventory(c).ToDictionary(e => e.ItemId, e => e.Slot);
        Assert.Equal(0, inv[armor]);                          // a maior vai para o canto
        Assert.Equal(new[] { 2, 3 }, new[] { inv[p1], inv[p2] }.OrderBy(x => x).ToArray());   // poções lado a lado
        Assert.False(t.Db.OrganizeInventory(c));              // já arrumada: nada muda
    }

    [Fact]
    public void LinhasTrancadas_NaoRecebemItem_EMissaoLibera()
    {
        using var t = new TempDb(96);
        t.Db.GridColumns = 8;
        t.Db.ItemSize = Size;
        var rows = 8;
        t.Db.UsableRows = _ => rows;
        var c = t.NewCharacter("dan");
        for (var i = 0; i < 64; i++) Assert.NotNull(t.Db.CreateInventoryItem(c, "consumable_c01_tonico_fraco", "comum", 1, "p" + i));
        Assert.Null(t.Db.CreateInventoryItem(c, "consumable_c01_tonico_fraco", "comum", 1, "cheia"));   // 9ª linha trancada
        Assert.False(t.Db.TryMoveInventorySlot(c, t.Db.GetInventory(c).First().ItemId, 70));
        rows = 9;   // "Bolsa Maior I"
        Assert.NotNull(t.Db.CreateInventoryItem(c, "consumable_c01_tonico_fraco", "comum", 1, "nova"));
        Assert.Equal(9, t.Db.RowsFor(c));
    }
}

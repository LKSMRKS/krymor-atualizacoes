using Microsoft.Data.Sqlite;

namespace CinzasDaMare.Server.Persistence;

/// <summary>
/// Mochila em grade estilo MU/Diablo (pedido 10/11): cada item ocupa um retângulo de casas (espada 1×3, armadura 2×3,
/// elmo 2×2, poção 1×1…). O "slot" gravado continua sendo um número: a casa do canto de cima à esquerda
/// (linha × colunas + coluna). Sem <see cref="GridColumns"/> (testes antigos), cada item ocupa uma casa só.
/// Nada muda no esquema do banco.
/// </summary>
public sealed partial class GameDatabase
{
    /// <summary>Colunas da grade (0 = desligada: uma casa por item).</summary>
    public int GridColumns { get; set; }

    /// <summary>Tamanho (largura, altura) em casas de cada definição de item; quem liga a grade preenche.</summary>
    public Func<string, (int W, int H)>? ItemSize { get; set; }

    public bool GridOn => GridColumns > 0 && ItemSize is not null;

    /// <summary>
    /// 11/11 item 53: linhas da mochila que o personagem já liberou (missões "Bolsa maior"). As de baixo ficam trancadas
    /// (contam como ocupadas). Null = todas liberadas (testes antigos). A mochila cresce para baixo: nada muda de lugar.
    /// </summary>
    public Func<string, int>? UsableRows { get; set; }

    public int RowsFor(string characterId) => GridOn ? Math.Clamp(UsableRows?.Invoke(characterId) ?? GridRows, 1, GridRows) : 0;
    private int GridRows => GridColumns > 0 ? InventoryCapacity / GridColumns : 0;

    public (int W, int H) SizeOf(string definitionId)
    {
        if (!GridOn) return (1, 1);
        var (w, h) = ItemSize!(definitionId);
        return (Math.Clamp(w, 1, GridColumns), Math.Clamp(h, 1, Math.Max(1, GridRows)));
    }

    /// <summary>Casas ocupadas da mochila (menos o item "except", que está sendo movido).</summary>
    private bool[] Occupied(SqliteConnection c, string characterId, string? except = null)
    {
        var occ = new bool[InventoryCapacity];
        if (GridOn && UsableRows is not null)
            for (var i = RowsFor(characterId) * GridColumns; i < occ.Length; i++) occ[i] = true;   // linhas trancadas
        using var q = Cmd(c,
            "SELECT l.item_id, l.slot, i.definition_id FROM item_locations l JOIN item_instances i ON i.id = l.item_id " +
            "WHERE l.owner_character_id=@c AND l.location_kind='inventory'", ("@c", characterId));
        using var r = q.ExecuteReader();
        while (r.Read())
        {
            if (r.GetString(0) == except || !int.TryParse(r.GetString(1), out var slot)) continue;
            Mark(occ, slot, SizeOf(r.GetString(2)));
        }
        return occ;
    }

    private void Mark(bool[] occ, int slot, (int W, int H) size)
    {
        if (!GridOn) { if (slot >= 0 && slot < occ.Length) occ[slot] = true; return; }
        var (x0, y0) = (slot % GridColumns, slot / GridColumns);
        for (var y = y0; y < y0 + size.H; y++)
            for (var x = x0; x < x0 + size.W; x++)
            {
                var i = y * GridColumns + x;
                if (x < GridColumns && i >= 0 && i < occ.Length) occ[i] = true;
            }
    }

    /// <summary>O item de tamanho "size" cabe com o canto em "slot" (dentro da grade e sem encostar em nada)?</summary>
    public bool FitsAt(bool[] occ, int slot, (int W, int H) size)
    {
        if (slot < 0 || slot >= InventoryCapacity) return false;
        if (!GridOn) return !occ[slot];
        var (x0, y0) = (slot % GridColumns, slot / GridColumns);
        if (x0 + size.W > GridColumns || y0 + size.H > GridRows) return false;
        for (var y = y0; y < y0 + size.H; y++)
            for (var x = x0; x < x0 + size.W; x++)
                if (occ[y * GridColumns + x]) return false;
        return true;
    }

    /// <summary>Primeira casa (de cima para baixo, da esquerda para a direita) onde o item cabe; -1 = mochila cheia.</summary>
    private int FirstFit(bool[] occ, (int W, int H) size)
    {
        for (var i = 0; i < InventoryCapacity; i++)
            if (FitsAt(occ, i, size)) return i;
        return -1;
    }

    /// <summary>Casa livre para um item desta definição (com a grade ligada, respeita o tamanho dele).</summary>
    private int FindFreeInventorySlot(SqliteConnection c, string characterId, string definitionId)
        => FirstFit(Occupied(c, characterId), SizeOf(definitionId));

    private string? DefinitionOf(SqliteConnection c, string itemId)
    {
        using var q = Cmd(c, "SELECT definition_id FROM item_instances WHERE id=@i", ("@i", itemId));
        return q.ExecuteScalar() as string;
    }

    /// <summary>
    /// Arrumação ao entrar no jogo (mochila antiga de casas soltas, ou tamanho de item que mudou): quem está num lugar
    /// válido fica; quem encosta em outro ou sai da grade vai para a primeira casa livre. O que não couber de jeito
    /// nenhum vai para o depósito do armazém (nada se perde). Devolve quantos foram para o depósito.
    /// </summary>
    public int NormalizeInventory(string characterId)
    {
        if (!GridOn) return 0;
        return InTransaction(c =>
        {
            var items = new List<(string Id, int Slot, (int W, int H) Size)>();
            using (var q = Cmd(c,
                "SELECT l.item_id, l.slot, i.definition_id FROM item_locations l JOIN item_instances i ON i.id = l.item_id " +
                "WHERE l.owner_character_id=@c AND l.location_kind='inventory' ORDER BY CAST(l.slot AS INTEGER)", ("@c", characterId)))
            using (var r = q.ExecuteReader())
                while (r.Read())
                    items.Add((r.GetString(0), int.TryParse(r.GetString(1), out var s) ? s : -1, SizeOf(r.GetString(2))));
            var occ = new bool[InventoryCapacity];
            var misplaced = new List<(string Id, (int W, int H) Size)>();
            foreach (var it in items)
            {
                if (FitsAt(occ, it.Slot, it.Size)) Mark(occ, it.Slot, it.Size);
                else misplaced.Add((it.Id, it.Size));
            }
            var stored = 0;
            // os maiores primeiro: encaixam melhor
            foreach (var (id, size) in misplaced.OrderByDescending(m => m.Size.W * m.Size.H))
            {
                var slot = FirstFit(occ, size);
                if (slot >= 0)
                {
                    Mark(occ, slot, size);
                    using var u = Cmd(c, "UPDATE item_locations SET slot=@s WHERE item_id=@i", ("@s", slot.ToString()), ("@i", id));
                    u.ExecuteNonQuery();
                    continue;
                }
                using (var d = Cmd(c, "DELETE FROM item_locations WHERE item_id=@i", ("@i", id))) d.ExecuteNonQuery();
                using (var i = Cmd(c, "INSERT INTO storage_items(item_id,character_id,container,created_at) VALUES(@i,@c,'armazem',@t)",
                           ("@i", id), ("@c", characterId), ("@t", Now())))
                    i.ExecuteNonQuery();
                Log(c, "deposit", characterId, id, null, "arrumacao_da_grade");
                stored++;
            }
            return stored;
        });
    }

    /// <summary>
    /// Arrastar na grade: o item vai para a casa pedida se couber ali (sem contar ele mesmo). Se a casa pedida é o canto
    /// de outro item do mesmo tamanho, os dois trocam (como no MU). Senão, não mexe.
    /// </summary>
    private bool TryMoveInGrid(string characterId, string itemId, int toSlot)
    {
        return InTransaction(c =>
        {
            string? from;
            using (var q = Cmd(c, "SELECT slot FROM item_locations WHERE item_id=@i AND owner_character_id=@c AND location_kind='inventory'",
                ("@i", itemId), ("@c", characterId)))
                from = q.ExecuteScalar() as string;
            if (from is null) return false;
            var size = SizeOf(DefinitionOf(c, itemId) ?? "");
            var occ = Occupied(c, characterId, itemId);
            void Set(string id, int slot)
            {
                using var u = Cmd(c, "UPDATE item_locations SET slot=@s WHERE item_id=@i", ("@s", slot.ToString()), ("@i", id));
                u.ExecuteNonQuery();
            }
            if (FitsAt(occ, toSlot, size)) { Set(itemId, toSlot); return true; }
            string? other;
            using (var q = Cmd(c, "SELECT item_id FROM item_locations WHERE owner_character_id=@c AND location_kind='inventory' AND slot=@s",
                ("@c", characterId), ("@s", toSlot.ToString())))
                other = q.ExecuteScalar() as string;
            if (other is null || other == itemId || !int.TryParse(from, out var fromSlot) || SizeOf(DefinitionOf(c, other) ?? "") != size) return false;
            // 11/11 noite (erro do log "UNIQUE constraint failed ... slot"): a casa é única por dono, então a troca passa
            // por uma casa provisória — antes os dois ficavam um instante na mesma casa e o banco recusava
            using (var u = Cmd(c, "UPDATE item_locations SET slot=@s WHERE item_id=@i", ("@s", "swap:" + itemId), ("@i", itemId))) u.ExecuteNonQuery();
            Set(other, fromSlot);
            Set(itemId, toSlot);
            return true;
        });
    }

    /// <summary>
    /// Botão "Organizar" (10/11 item 53): reempacota a mochila inteira — os maiores primeiro e itens iguais juntos, de cima
    /// para baixo. Tudo ou nada: se algum não couber no novo arranjo, nada muda. Devolve se mudou.
    /// </summary>
    public bool OrganizeInventory(string characterId)
    {
        return InTransaction(c =>
        {
            var items = new List<(string Id, string Def, int Slot, (int W, int H) Size)>();
            using (var q = Cmd(c,
                "SELECT l.item_id, i.definition_id, l.slot FROM item_locations l JOIN item_instances i ON i.id = l.item_id " +
                "WHERE l.owner_character_id=@c AND l.location_kind='inventory'", ("@c", characterId)))
            using (var r = q.ExecuteReader())
                while (r.Read())
                    items.Add((r.GetString(0), r.GetString(1), int.TryParse(r.GetString(2), out var sl) ? sl : -1, SizeOf(r.GetString(1))));
            var occ = new bool[InventoryCapacity];
            var plan = new List<(string Id, int Slot)>();
            foreach (var it in items.OrderByDescending(i => i.Size.W * i.Size.H).ThenByDescending(i => i.Size.H)
                         .ThenBy(i => Group(i.Def)).ThenBy(i => i.Def, StringComparer.Ordinal).ThenBy(i => i.Id, StringComparer.Ordinal))
            {
                var slot = FirstFit(occ, it.Size);
                if (slot < 0) return false;
                Mark(occ, slot, it.Size);
                plan.Add((it.Id, slot));
            }
            var moves = plan.Where(p => items.First(i => i.Id == p.Id).Slot != p.Slot).ToList();
            // duas passadas (o slot é único por dono): primeiro tira do caminho, depois põe no lugar novo
            foreach (var (id, _) in moves)
            {
                using var u = Cmd(c, "UPDATE item_locations SET slot=@s WHERE item_id=@i", ("@s", "org:" + id), ("@i", id));
                u.ExecuteNonQuery();
            }
            foreach (var (id, slot) in moves)
            {
                using var u = Cmd(c, "UPDATE item_locations SET slot=@s WHERE item_id=@i", ("@s", slot.ToString()), ("@i", id));
                u.ExecuteNonQuery();
            }
            return moves.Count > 0;
        });

        static int Group(string def) => def.Split('_')[0] switch
        {
            "weapon" or "staff" or "talisma" or "arco" or "shield" => 0,
            "armor" or "helmet" or "gloves" or "boots" or "pants" or "ring" or "amulet" => 1,
            "consumable" => 2,
            "mat" => 3,
            _ => 4,
        };
    }

    /// <summary>Casas ocupadas por um depósito (área somada dos itens), para a lotação com a grade ligada.</summary>
    private int StorageArea(SqliteConnection c, string characterId, string container)
    {
        var area = 0;
        using var q = Cmd(c, "SELECT i.definition_id FROM storage_items s JOIN item_instances i ON i.id = s.item_id WHERE s.character_id=@c AND s.container=@k",
            ("@c", characterId), ("@k", container));
        using var r = q.ExecuteReader();
        while (r.Read()) { var (w, h) = SizeOf(r.GetString(0)); area += w * h; }
        return area;
    }

    /// <summary>Tirar do depósito direto para uma casa escolhida (arrastar): se não couber ali, vai para a primeira livre.</summary>
    private int WithdrawSlot(SqliteConnection c, string characterId, string itemId, int? preferred)
    {
        var occ = Occupied(c, characterId);
        var size = SizeOf(DefinitionOf(c, itemId) ?? "");
        if (preferred is { } p && FitsAt(occ, p, size)) return p;
        return FirstFit(occ, size);
    }
}

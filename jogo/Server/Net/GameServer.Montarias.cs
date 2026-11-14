using System;
using System.Linq;
using CinzasDaMare.Shared.Diagnostics;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (pedido do dono: "a montaria deve ser um ITEM, assim como o pet — colocar e tirar no inventário; precisa do item
/// pra ter a montaria"). Cada montaria virou o item "montaria_&lt;bicho&gt;" no espaço Montaria. Quem já tinha a marca
/// "mount:x" (missão, caixa, apito) ganha o item uma vez (marca "montitem:x"); a escolhida (ou a primeira) já vem vestida.
/// Montar (V) usa a do espaço Montaria — tirou o item, não monta.
/// </summary>
public sealed partial class GameServer
{
    public const string MountSlot = "montaria";

    private void ConvertMountFlagsToItems(Session s, bool equipNew = false)
    {
        if (!MapMode || s.CharacterId is null) return;
        var escolhida = EscolhidaSalva(s);
        foreach (var kind in s.Flags.Where(f => f.StartsWith("mount:", StringComparison.Ordinal)).Select(f => f[6..]).OrderBy(k => k == escolhida ? 0 : 1).ToList())
        {
            var def = "montaria_" + kind;
            if (s.Flags.Contains("montitem:" + kind) || !_content.Items.ContainsKey(def)) continue;
            var itemId = _db.CreateInventoryItem(s.CharacterId, def, "comum", 1, $"montaria_{s.CharacterId}:{kind}");
            if (itemId is null) continue;   // mochila cheia: tenta de novo no próximo login
            s.Flags.Add("montitem:" + kind);
            _db.TryAddFlag(s.CharacterId, "montitem:" + kind);
            if (_db.GetEquipment(s.CharacterId).All(e => e.Slot != MountSlot)) _db.TryEquip(s.CharacterId, itemId, MountSlot);
            GameLog.Info("servidor.itens", $"{Who(s)} ganhou o item da montaria {kind}");
            if (equipNew && s.LoggedIn) SendTo(s, new Shared.Net.NoticeMessage($"A montaria veio como item ({_content.Items[def].DisplayName}) — fica no espaço Montaria do inventário.", "reward"));
        }
        if (s.LoggedIn) { SendInventory(s); SendEquipment(s); }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 12/11 (auditoria da progressão: "cada drop alinhado com o nível do monstro"): uma espécie usada em vários níveis (bestiário,
/// monstros derivados das ramificações e reinos) carrega a tabela de drop de onde nasceu. Quando o equipamento sorteado é de
/// nível muito diferente do monstro (mais de 6 acima ou 15 abaixo), ele vira a peça EQUIVALENTE — mesma categoria, mesmo tipo
/// de arma e mesma classe — da faixa do monstro. Peças de conjunto (Set) não mudam (prêmio raro proposital).
/// </summary>
public sealed partial class GameServer
{
    private Dictionary<(string Cat, string Sub, string Cls), List<ItemDefinition>>? _equivalentes;

    public string Nivelar(string itemId, int nivel)
    {
        if (!_content.Items.TryGetValue(itemId, out var d) || d.Set is not null) return itemId;
        if (d.Category is not ("weapon" or "armor" or "helmet" or "offhand" or "gloves" or "boots" or "pants")) return itemId;
        if (d.MinLevel <= nivel + 6 && d.MinLevel >= nivel - 15) return itemId;
        _equivalentes ??= _content.Items.Values
            .Where(i => i.Set is null && i.Category is "weapon" or "armor" or "helmet" or "offhand" or "gloves" or "boots" or "pants")
            .GroupBy(i => (i.Category, i.Subtype ?? "", i.AllowedClasses.FirstOrDefault() ?? ""))
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.MinLevel).ToList());
        if (!_equivalentes.TryGetValue((d.Category, d.Subtype ?? "", d.AllowedClasses.FirstOrDefault() ?? ""), out var lista)) return itemId;
        // a melhor peça que o monstro "pode" soltar: nível até nivel+2, a mais alta; se não houver, a mais baixa da lista
        var alvo = lista.LastOrDefault(i => i.MinLevel <= nivel + 2 && i.MinLevel >= nivel - 15) ?? lista.LastOrDefault(i => i.MinLevel <= nivel + 2);
        return alvo?.Id ?? itemId;
    }
}

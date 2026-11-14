using System;
using System.Collections.Generic;
using System.Linq;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// 14/11 ("os itens vêm com nível alto mas não com atributos tão bons, tem que regular isso"): piso dos atributos
/// principais pelo nível do item. Item abaixo da curva sobe até ela (nunca desce); a curva segue as armaduras e armas
/// das regiões (items_midgame), que são a referência de "item certo para o nível". Vale para tudo: conjuntos,
/// lendárias, arsenal, história... Os afixos continuam por cima.
/// </summary>
public static class PisoDeNivel
{
    private const double Folga = 0.9;   // piso = 90% da curva (cada item ainda tem sua personalidade)

    public static ItemDefinition Aplicar(ItemDefinition d)
    {
        if (d.MinLevel < 3 || d.BaseStats.Count == 0) return d;
        var L = d.MinLevel;
        var s = new Dictionary<string, double>(d.BaseStats);
        var mudou = false;
        void Piso(string stat, double alvo)
        {
            alvo = Math.Round(alvo * Folga);
            if (alvo <= 0) return;
            if (!s.TryGetValue(stat, out var v) || v < alvo) { s[stat] = alvo; mudou = true; }
        }

        if (d.Category == "weapon" && s.TryGetValue("weapon_power", out _))
        {
            var intervalo = s.TryGetValue("attack_interval_ms", out var iv) && iv > 0 ? iv : 1000;
            var conjurador = d.Subtype is "staff" or "talisman" or "wand" or "orb";
            var porNivel = conjurador ? 2.2 : 2.6;
            var setas = s.TryGetValue("projectile_count", out var pc) && pc > 1 ? pc : 1;   // vara gêmea, arco de 2 setas...
            Piso("weapon_power", (porNivel * L + 8) * intervalo / 1000.0 / setas);
        }
        else if (Peso(d) is { } w && s.ContainsKey("armor"))
        {
            // armadura: pesada (só guerreiro) > média (arqueiro / misto) > leve (mago, bruxa)
            var cls = d.AllowedClasses;
            var pesada = d.Subtype == "plate" || (cls.Count > 0 && cls.All(c => c == "guerreiro"));
            var leve = d.Subtype is "robe" or "cloth" || (cls.Count > 0 && cls.All(c => c is "mago" or "bruxa"));
            var armadura = pesada ? 14.3 * L + 30 : leve ? 6.7 * L + 30 : 9.7 * L + 40;
            Piso("armor", armadura * w.Armadura);
            Piso("max_health", (3.0 * L + 80) * w.Vida);
        }
        return mudou ? d with { BaseStats = s } : d;
    }

    private static (double Armadura, double Vida)? Peso(ItemDefinition d) => d.Category switch
    {
        "armor" => (1.0, 1.0),
        "helmet" => (0.35, 0.55),
        "pants" => (0.5, 0.45),
        "gloves" => (0.33, 0.3),
        "boots" => (0.4, 0.35),
        _ => null,
    };
}

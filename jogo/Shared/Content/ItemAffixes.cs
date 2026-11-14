using System;
using System.Collections.Generic;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// Afixos de item (estilo Diablo/MU): cada exemplar de qualidade incomum ou melhor tem atributos extras sorteados.
/// O sorteio é DETERMINÍSTICO pelo id do exemplar (o servidor e o cliente chegam ao mesmo resultado, sem coluna nova
/// no banco). Quantidade pela qualidade: incomum 1, raro 2, épico 3, chefe 2, lendário 1 (o lendário já tem efeito
/// próprio). Valores crescem com o nível do item. Só itens com <c>affix_pool</c> diferente de "none".
/// </summary>
public static class ItemAffixes
{
    public sealed record Affix(string Stat, double Value, string Text);

    public static int CountFor(string quality) => quality switch
    {
        "incomum" => 1, "raro" => 2, "epico" => 3, "chefe" => 2, "lendario" => 1, "excelente" => 3, _ => 0,
    };

    private sealed record Kind(string Stat, double Min, double Max, double PerLevel, bool Pct, string Name);

    // valores-base no nível 1 (Min..Max) + PerLevel × nível do item
    private static readonly Kind[] Weapon =
    {
        new("power_pct", 0.03, 0.06, 0.0020, true, "de poder"),   // 14/11: crescem mais com o nível
        new("resource_regen_pct", 0.05, 0.12, 0.002, true, "de regeneração de recurso"),
        new("move_speed_pct", 0.02, 0.04, 0.0004, true, "de velocidade"),
        new("max_health", 6, 14, 3.5, false, "de vida"),
    };

    private static readonly Kind[] Armor =
    {
        new("armor", 2, 5, 1.2, false, "de armadura"),   // 14/11: crescem mais com o nível
        new("max_health", 8, 18, 4.0, false, "de vida"),
        new("move_speed_pct", 0.02, 0.05, 0.0004, true, "de velocidade"),
        new("resource_regen_pct", 0.04, 0.10, 0.0025, true, "de regeneração de recurso"),
        new("power_pct", 0.02, 0.04, 0.0015, true, "de poder"),
    };

    // 12/11: as 8 linhas possíveis de uma peça de conjunto com variação (V1–V15); a Full tem todas, no valor máximo
    private static readonly Kind[] SetLines =
    {
        new("power_pct", 0.02, 0.04, 0.0008, true, "de poder"),
        new("max_health", 8, 18, 2.6, false, "de vida"),
        new("armor", 2, 5, 0.45, false, "de armadura"),
        new("move_speed_pct", 0.02, 0.04, 0.0003, true, "de velocidade"),
        new("resource_regen_pct", 0.04, 0.10, 0.0018, true, "de regeneração de recurso"),
        new("crit_pct", 0.01, 0.025, 0.0003, true, "de chance de crítico"),
        new("dodge_pct", 0.01, 0.02, 0.0002, true, "de esquiva"),
        new("damage_reduction_pct", 0.01, 0.025, 0.0003, true, "de redução de dano"),
    };

    private static IReadOnlyList<Affix> RollVariacao(string itemId, int v, int itemLevel)
    {
        var seed = Fnv(itemId + "#v");
        var (min, max) = Variacao.Lines(v);
        var n = min + (int)(Next(ref seed) % (uint)(max - min + 1));
        if (n == 0) return Array.Empty<Affix>();
        var picked = new List<Affix>(n);
        var used = new HashSet<int>();
        var lvl = Math.Max(1, itemLevel);
        var boost = Variacao.Strength(v);
        for (var i = 0; i < n && used.Count < SetLines.Length; i++)
        {
            var k = (int)(Next(ref seed) % (uint)SetLines.Length);
            while (!used.Add(k)) k = (k + 1) % SetLines.Length;
            var kind = SetLines[k];
            // dentro da variação o valor ainda varia um pouco (mais ou menos); a Full vem sempre no máximo
            var t = v >= Variacao.Max ? 1.0 : Next(ref seed) / (double)uint.MaxValue;
            var val = (kind.Min + (kind.Max - kind.Min) * t + kind.PerLevel * lvl) * boost;
            val = kind.Pct ? Math.Round(val, 3) : Math.Round(val);
            picked.Add(new Affix(kind.Stat, val, kind.Pct ? $"+{val * 100:0.#}% {kind.Name}" : $"+{val:0} {kind.Name}"));
        }
        return picked;
    }

    public static IReadOnlyList<Affix> Roll(string itemId, string quality, string pool, int itemLevel)
    {
        if (Variacao.Of(quality) is > 0 and var vv && !string.IsNullOrEmpty(itemId)) return RollVariacao(itemId, vv, itemLevel);
        var n = CountFor(quality);
        if (n == 0 || string.IsNullOrEmpty(itemId)) return Array.Empty<Affix>();
        var kinds = pool switch { "physical_weapon" => Weapon, "armor" => Armor, _ => null };
        if (kinds is null) return Array.Empty<Affix>();
        var seed = Fnv(itemId);
        var picked = new List<Affix>(n);
        var used = new HashSet<int>();
        var lvl = Math.Max(1, itemLevel);
        var boost = quality == "excelente" ? 1.5 : quality is "epico" or "chefe" or "lendario" ? 1.2 : 1.0;   // 11/11: Excelente (MU)
        for (var i = 0; i < n && used.Count < kinds.Length; i++)
        {
            var k = (int)(Next(ref seed) % (uint)kinds.Length);
            while (!used.Add(k)) k = (k + 1) % kinds.Length;
            var kind = kinds[k];
            var t = Next(ref seed) / (double)uint.MaxValue;
            var v = (kind.Min + (kind.Max - kind.Min) * t + kind.PerLevel * lvl) * boost;
            v = kind.Pct ? Math.Round(v, 3) : Math.Round(v);
            var text = kind.Pct ? $"+{v * 100:0.#}% {kind.Name}" : $"+{v:0} {kind.Name}";
            picked.Add(new Affix(kind.Stat, v, text));
        }
        return picked;
    }

    // ------------------------------------------------------------------ feitio (13/11)
    // Pedido do dono: "mais variações, matematicamente — um mix — pra não parecer que tem poucas coisas; o melhor continua
    // o mais raro, sem quebrar a progressão". Cada EXEMPLAR de equipamento nasce com um FEITIO (sorteado pelo id, igual no
    // servidor e no cliente): a maioria troca um pouco de uma coisa por outra (soma quase zero), poucos só ganham.
    // Com V1–V15 × linhas da variação × 11 feitios, dois itens iguais quase nunca saem idênticos.
    private sealed record FeitioKind(string Nome, int Peso, (string Stat, double Base, double PorNivel, bool Pct, string Texto)[] Linhas);

    private static readonly FeitioKind[] Feitios =
    {
        new("Padrão", 300, Array.Empty<(string, double, double, bool, string)>()),
        new("Afiado", 90, new[] { ("power_pct", 0.04, 0.0, true, "de poder"), ("move_speed_pct", -0.02, 0.0, true, "de velocidade") }),
        new("Leve", 90, new[] { ("move_speed_pct", 0.04, 0.0, true, "de velocidade"), ("armor", -2.0, -0.3, false, "de armadura") }),
        new("Robusto", 90, new[] { ("max_health", 10.0, 2.0, false, "de vida"), ("move_speed_pct", -0.02, 0.0, true, "de velocidade") }),
        new("Ágil", 80, new[] { ("dodge_pct", 0.02, 0.0, true, "de esquiva"), ("max_health", -4.0, -1.0, false, "de vida") }),
        new("Feroz", 80, new[] { ("crit_pct", 0.02, 0.0, true, "de chance de crítico"), ("armor", -1.0, -0.2, false, "de armadura") }),
        new("Encantado", 80, new[] { ("resource_regen_pct", 0.08, 0.0, true, "de regeneração de recurso"), ("power_pct", -0.02, 0.0, true, "de poder") }),
        new("Firme", 80, new[] { ("damage_reduction_pct", 0.02, 0.0, true, "de redução de dano"), ("move_speed_pct", -0.02, 0.0, true, "de velocidade") }),
        new("Equilibrado", 60, new[] { ("power_pct", 0.015, 0.0, true, "de poder"), ("max_health", 4.0, 1.0, false, "de vida") }),
        new("Abençoado", 35, new[] { ("power_pct", 0.03, 0.0, true, "de poder"), ("max_health", 8.0, 2.0, false, "de vida") }),
        new("Perfeito", 15, new[] { ("power_pct", 0.04, 0.0, true, "de poder"), ("max_health", 10.0, 2.5, false, "de vida"), ("crit_pct", 0.015, 0.0, true, "de chance de crítico") }),
    };

    public static bool TemFeitio(string? category, int itemLevel = 2) => itemLevel > 1 && category is "weapon" or "armor" or "helmet" or "gloves" or "boots" or "pants" or "offhand";

    /// <summary>Feitio do exemplar (nome e linhas; "Padrão" = sem linhas). Valores com sinal (o feitio troca um pelo outro).</summary>
    public static (string Nome, IReadOnlyList<Affix> Linhas) Feitio(string itemId, string? category, int itemLevel)
    {
        // a roupa inicial (nível 1) não tem feitio: o começo é igual para todo mundo
        if (string.IsNullOrEmpty(itemId) || !TemFeitio(category) || itemLevel <= 1) return ("", Array.Empty<Affix>());
        var seed = Fnv(itemId + "#feitio");
        var r = (int)(Next(ref seed) % 1000u);
        var f = Feitios[0];
        foreach (var k in Feitios) { if (r < k.Peso) { f = k; break; } r -= k.Peso; }
        var lvl = Math.Max(1, itemLevel);
        var list = new List<Affix>(f.Linhas.Length);
        foreach (var (stat, b, pn, pct, texto) in f.Linhas)
        {
            var v = b + pn * lvl;
            v = pct ? Math.Round(v, 3) : Math.Round(v);
            var sinal = v >= 0 ? "+" : "−";
            list.Add(new Affix(stat, v, pct ? $"{sinal}{Math.Abs(v) * 100:0.#}% {texto}" : $"{sinal}{Math.Abs(v):0} {texto}"));
        }
        return (f.Nome, list);
    }

    /// <summary>Soma de um atributo nos afixos.</summary>
    public static double Sum(IReadOnlyList<Affix> affixes, string stat)
    {
        double s = 0;
        foreach (var a in affixes) if (a.Stat == stat) s += a.Value;
        return s;
    }

    private static uint Fnv(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) { h ^= c; h *= 16777619u; }
        return h == 0 ? 1u : h;
    }

    private static uint Next(ref uint x)
    {
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        return x;
    }
}

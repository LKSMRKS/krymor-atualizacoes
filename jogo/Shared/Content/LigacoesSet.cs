using System.Collections.Generic;
using System.Linq;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// 18/11 19h (dono: "como tem bônus por parte de set, vamos fazer uma mecânica — as ligações entre os espaços acendem
/// quando o set é o mesmo; são 4 ligações num set completo: 1 = +0,5%, 2 = +1,5%, 3 = +4% e todas = +10% (todas as peças
/// recebem 10% a mais de efetividade nos atributos), ativa o círculo no chão e mostra 'conjunto completo' no texto").
/// Ligações (no inventário): elmo–peito, luvas–peito, peito–calça, calça–botas.
/// </summary>
public static class LigacoesSet
{
    public static readonly (string A, string B)[] Pares = { ("head", "body"), ("luvas", "body"), ("body", "calca"), ("calca", "botas") };

    /// <summary>Quantas ligações acendem e de qual conjunto (o que acende mais). slot → id do conjunto (null = sem set).</summary>
    public static (int Acesas, string? Set) Contar(IReadOnlyDictionary<string, string?> setPorSlot)
    {
        var porSet = new Dictionary<string, int>();
        foreach (var (a, b) in Pares)
            if (setPorSlot.TryGetValue(a, out var sa) && sa is not null && setPorSlot.TryGetValue(b, out var sb) && sa == sb)
                porSet[sa] = porSet.GetValueOrDefault(sa) + 1;
        if (porSet.Count == 0) return (0, null);
        var melhor = porSet.OrderByDescending(kv => kv.Value).First();
        return (melhor.Value, melhor.Key);
    }

    public static double Bonus(int acesas) => acesas switch { >= 4 => 0.10, 3 => 0.04, 2 => 0.015, 1 => 0.005, _ => 0 };

    public static bool Aceso(IReadOnlyDictionary<string, string?> setPorSlot, string a, string b)
        => setPorSlot.TryGetValue(a, out var sa) && sa is not null && setPorSlot.TryGetValue(b, out var sb) && sa == sb;
}

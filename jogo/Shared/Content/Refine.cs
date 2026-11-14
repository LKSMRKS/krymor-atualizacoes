using System;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// Refino estilo MU (11/11): arraste uma joia em cima do item na mochila.
/// +1 a +6: Joia da Maré, sempre dá certo. +7 a +9: Joia da Brasa, 75/65/55%; errou = perde 1.
/// +10 a +15: Joia do Vazio, 50% caindo até 25%; errou = perde 2 (nunca abaixo de +9). Custa moedas também.
/// Cada + vale 1 passo; de +10 em diante, 2 passos (o MU também acelera no fim). O brilho da arma cresce do +7.
/// </summary>
public static class Refine
{
    public const int Max = 15;

    public static int Steps(int plus) => plus <= 9 ? plus : 9 + (plus - 9) * 2;

    public static string JewelFor(int current) => current < 6 ? "joia_mare" : current < 9 ? "joia_brasa" : "joia_vazio";

    public static double Chance(int next) => next switch
    {
        <= 6 => 1.0, 7 => 0.75, 8 => 0.65, 9 => 0.55, 10 => 0.5, 11 => 0.45, 12 => 0.4, 13 => 0.35, 14 => 0.3, _ => 0.25,
    };

    public static int OnFail(int current) => current <= 6 ? current : current <= 9 ? Math.Max(6, current - 1) : Math.Max(9, current - 2);

    /// <summary>
    /// 14/11 (dono: "o refino tem que ter progressão: o simples barato, o topo caro — o cara tem que ir farmar quase sempre"):
    /// +1..+6 continua 40·n² (barato); +7..+9 × (1 + nível/20); +10..+15 × (1 + nível/8). No nível 50: +9 = 11 mil,
    /// +12 = 41 mil, +15 = 64 mil (por tentativa; a joia e a chance de falhar continuam valendo).
    /// </summary>
    public static long Coins(int next, int itemLevel = 1)
    {
        var baseC = 40.0 * next * next;
        var L = Math.Max(1, itemLevel);
        return (long)Math.Round(next <= 6 ? baseC : next <= 9 ? baseC * (1 + L / 20.0) : baseC * (1 + L / 8.0));
    }

    public static bool IsJewel(string definitionId) => definitionId is "joia_mare" or "joia_brasa" or "joia_vazio" or "joia_forja";
}

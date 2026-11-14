using System;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// 12/11 (regra de drop estilo MU, aprovada pelo dono): cada peça de conjunto 3D cai numa VARIAÇÃO de V1 a V15. O conjunto é
/// sempre o mesmo; o que muda é quantas linhas de atributo vêm e quão fortes. Quanto melhor, mais dourado.
///   V1–V3 Cinza (0–1 linha fraca) · V4–V6 Bronze (1–2) · V7–V9 Prata (2–3) · V10–V12 Ouro / Excelente (4–5)
///   V13–V14 Ouro brilhante (6–7) · V15 Full (8 linhas, todas no máximo).
/// Gravada na própria qualidade do exemplar ("v01".."v15"): sem coluna nova no banco; as linhas saem do id do exemplar
/// (mesmo resultado no servidor e no cliente, ver <see cref="ItemAffixes"/>).
/// </summary>
public static class Variacao
{
    public const int Max = 15;

    /// <summary>Número da variação (0 = não é variação de conjunto).</summary>
    public static int Of(string? quality)
        => quality is { Length: 3 } && quality[0] == 'v' && int.TryParse(quality.AsSpan(1), out var v) && v is >= 1 and <= Max ? v : 0;

    public static string Quality(int v) => "v" + Math.Clamp(v, 1, Max).ToString("00");

    /// <summary>Linhas de atributo (mínimo, máximo) de cada variação.</summary>
    public static (int Min, int Max) Lines(int v) => v switch
    {
        <= 1 => (0, 0), 2 => (0, 1), 3 => (1, 1), 4 => (1, 1), 5 => (1, 2), 6 => (2, 2),
        7 => (2, 2), 8 => (2, 3), 9 => (3, 3), 10 => (4, 4), 11 => (4, 5), 12 => (5, 5),
        13 => (6, 6), 14 => (7, 7), _ => (8, 8),
    };

    /// <summary>Força dos valores (multiplica o valor-base da linha): 0,55 na V1 até 1,25 na V15.</summary>
    public static double Strength(int v) => 0.55 + 0.70 * (Math.Clamp(v, 1, Max) - 1) / (Max - 1);

    /// <summary>Faixa pelo nome (para o texto do item).</summary>
    public static string Faixa(int v) => v switch
    {
        <= 3 => "Cinza", <= 6 => "Bronze", <= 9 => "Prata", <= 12 => "Ouro", <= 14 => "Ouro brilhante", _ => "Full",
    };

    /// <summary>Conta como Excelente (anúncio dourado, conquista) da V10 em diante.</summary>
    public static bool IsExcellent(string? quality) => Of(quality) >= 10;

    /// <summary>Cor (r, g, b) da variação: cinza → bronze → prata → ouro → dourado radiante.</summary>
    public static (float R, float G, float B) Color(int v) => v switch
    {
        <= 3 => (0.62f, 0.62f, 0.60f),
        <= 6 => (0.80f, 0.52f, 0.28f),
        <= 9 => (0.82f, 0.86f, 0.94f),
        <= 12 => (1.00f, 0.78f, 0.25f),
        <= 14 => (1.00f, 0.80f, 0.30f),
        _ => (1.00f, 0.72f, 0.18f),   // Full: ouro mais fundo (não lavado de branco)
    };
}

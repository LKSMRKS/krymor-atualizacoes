using System;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// 13/11 ("os itens novos estão sendo vendidos por 5 moedinhas"): a venda de equipamento agora conta a variação (V1–V15),
/// a qualidade antiga, o refino (+N) e o nível do item. Mesma conta no servidor (o que paga) e no cliente (o que mostra).
/// </summary>
public static class Venda
{
    /// <param name="baseSell">venda-base (1/4 do preço da loja, com piso)</param>
    public static long Preco(long baseSell, bool equipamento, int minLevel, string? quality, int plus)
    {
        if (baseSell <= 0) return 0;
        if (!equipamento) return baseSell;
        // piso pelo nível: um item de nível 40 nunca vale menos que ~40 × 6 moedas
        var piso = (long)(Math.Max(1, minLevel) * 6);
        var v = Variacao.Of(quality);
        // 14/11 (auditoria do trabalhador, problema 3: "a venda paga mais que a loja cobra" — lendário vendia por 2,5x o preço):
        // multiplicadores menores e TETO de 60% do preço de loja (V15: 80%); o refino (que custa joia) soma por cima
        var mult = v == Variacao.Max ? 3.2 : v > 0 ? Math.Min(2.4, 1.0 + 0.1 * (v - 1) + (v >= 10 ? 0.3 : 0))
            : quality switch { "incomum" => 1.2, "raro" => 1.5, "epico" => 1.8, "chefe" => 2.0, "excelente" => 2.4, "lendario" => 2.4, _ => 1.0 };
        var teto = baseSell * 4 * (v == Variacao.Max ? 0.8 : 0.6);
        var refino = 1.0 + 0.25 * Math.Max(0, plus);
        return (long)Math.Round(Math.Min(Math.Max(baseSell, piso) * mult, Math.Max(teto, piso)) * refino);
    }
}

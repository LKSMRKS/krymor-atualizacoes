using System;
using System.Collections.Generic;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// 14/11 (pedido do dono: "drops vindo muito fracos — use o mesmo sistema de drop do MU: quais atributos, variações"):
/// opções de exemplar no estilo MU, além das linhas de afixo e do feitio. Sorteadas pelo id do exemplar (servidor e
/// cliente chegam ao mesmo resultado, sem coluna nova no banco), só em equipamento que CAIU melhor que comum
/// (incomum, raro, épico, Excelente, de chefe, lendário e variações V4+). Item de loja comum continua liso.
///   * Sorte: +5% de chance de crítico (MU: "Luck").
///   * Opção +1 a +4 (MU: "Additional damage/defense"): arma +2% de poder por nível; peça de armadura + armadura.
///   * Opções Excelentes (só Excelente / V10+): 1–2 de 6 (V13+: 2–3), como no MU.
/// Ordem de força (cada degrau mais raro): comum &lt; incomum &lt; raro &lt; épico &lt; Excelente &lt; lendário.
/// </summary>
public static class OpcoesMu
{
    public static bool Tem(string? category) => category is "weapon" or "offhand" or "armor" or "helmet" or "gloves" or "boots" or "pants";

    /// <summary>Degrau da qualidade (0 = liso).</summary>
    public static int Degrau(string? quality)
    {
        var v = Variacao.Of(quality);
        if (v > 0) return v switch { <= 3 => 0, <= 6 => 1, <= 9 => 2, <= 12 => 4, _ => 5 };
        return quality switch { "incomum" => 1, "raro" => 2, "epico" => 3, "chefe" => 3, "lendario" => 4, "excelente" => 4, _ => 0 };
    }

    public static bool Excelente(string? quality) => quality == "excelente" || Variacao.Of(quality) >= 10;

    private static readonly double[] ChanceSorte = { 0, 0.15, 0.25, 0.35, 0.5, 0.65 };
    private static readonly double[] ChanceOpcao = { 0, 0.30, 0.45, 0.60, 0.75, 0.90 };

    private sealed record Exc(string Stat, double Valor, double PorNivel, string Texto);

    // MU: mana/8 e vida/8 ao matar, velocidade de ataque +7, dano +2%, dano +nível/20, taxa de dano excelente +10%
    private static readonly Exc[] ExcArma =
    {
        new("heal_on_kill_pct", 0.06, 0, "Vida ao derrotar um inimigo +{0}%"),
        new("resource_on_kill_pct", 0.10, 0, "Recurso ao derrotar um inimigo +{0}%"),
        new("attack_speed_pct", 0.07, 0, "Velocidade de ataque +{0}%"),
        new("power_pct", 0.02, 0.0004, "Dano +{0}%"),
        new("crit_pct", 0.08, 0, "Chance de golpe excelente (crítico) +{0}%"),
        new("power_pct", 0.0, 0.0005, "Dano +{0}% (cresce com o nível do item)"),
    };

    // MU: vida máx. +4%, mana máx. +4%, redução de dano +4%, reflete dano +5%, taxa de defesa +10%, zen ao matar +40%
    private static readonly Exc[] ExcArmadura =
    {
        new("max_health_pct", 0.04, 0, "Vida máxima +{0}%"),
        new("resource_regen_pct", 0.10, 0, "Recurso volta +{0}% mais rápido"),
        new("damage_reduction_pct", 0.04, 0, "Redução de dano +{0}%"),
        new("thorns_pct", 0.05, 0, "Reflete +{0}% do dano"),
        new("dodge_pct", 0.04, 0, "Taxa de defesa (esquiva) +{0}%"),
        new("coin_pct", 0.30, 0, "Moedas ao derrotar inimigos +{0}%"),
    };

    public sealed record Resultado(bool Sorte, int Opcao, IReadOnlyList<ItemAffixes.Affix> Linhas)
    {
        public static readonly Resultado Nada = new(false, 0, Array.Empty<ItemAffixes.Affix>());
    }

    public static Resultado Rolar(string itemId, string? quality, string? category, int itemLevel)
    {
        var d = Degrau(quality);
        if (d == 0 || string.IsNullOrEmpty(itemId) || !Tem(category)) return Resultado.Nada;
        var seed = Fnv(itemId + "#mu");
        double R() => Next(ref seed) / (double)uint.MaxValue;
        var arma = category == "weapon";
        var lvl = Math.Max(1, itemLevel);
        var linhas = new List<ItemAffixes.Affix>();
        var sorte = R() < ChanceSorte[d];
        if (sorte) linhas.Add(new ItemAffixes.Affix("crit_pct", 0.05, "Sorte: +5% de chance de crítico"));
        var opcao = 0;
        if (R() < ChanceOpcao[d])
        {
            opcao = 1;
            var teto = Math.Min(4, d + 1);
            while (opcao < teto && R() < 0.45) opcao++;
            if (arma) linhas.Add(new ItemAffixes.Affix("power_pct", 0.02 * opcao, $"Opção +{opcao}: +{2 * opcao}% de dano"));
            else
            {
                var a = Math.Round((2 + 0.25 * lvl) * opcao);
                linhas.Add(new ItemAffixes.Affix("armor", a, $"Opção +{opcao}: +{a:0} de armadura"));
            }
        }
        if (Excelente(quality))
        {
            var v = Variacao.Of(quality);
            var n = v >= 13 ? (R() < 0.6 ? 2 : 3) : (R() < 0.7 ? 1 : 2);
            var pool = arma ? ExcArma : ExcArmadura;
            var usados = new HashSet<int>();
            for (var i = 0; i < n && usados.Count < pool.Length; i++)
            {
                var k = (int)(Next(ref seed) % (uint)pool.Length);
                while (!usados.Add(k)) k = (k + 1) % pool.Length;
                var e = pool[k];
                var val = Math.Round(e.Valor + e.PorNivel * lvl, 3);
                linhas.Add(new ItemAffixes.Affix(e.Stat, val, "★ " + string.Format(e.Texto, (val * 100).ToString("0.#"))));
            }
        }
        return new Resultado(sorte, opcao, linhas);
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

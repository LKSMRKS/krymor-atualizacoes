using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CinzasDaMare.Shared.World;

/// <summary>
/// 18/11 (dono: "dá pra sentar em cadeiras e sofás pra carregar 1,5× o buff da casa"): onde dá para sentar — bancos de
/// praça, as cadeiras dos cômodos e, na casa, o trono e o sofá da decoração (no lugar que o dono escolheu).
/// Casa = assento dentro de casa (carrega o Conforto do Lar ×1,5).
/// </summary>
public static class AssentosCasa
{
    private static readonly Dictionary<string, (double X, double Y, double Yaw)> Padrao = new()
    {
        ["decor_trono"] = (-2.6, -8.3, 0), ["decor_sofa"] = (-6.5, 6.5, 180),
    };

    /// <summary>18/11 15h: terreno da casa (lote) ou cômodo — sentar aí vale o Conforto do Lar.</summary>
    public static bool TerrenoOuCasa(string? mapa) => mapa is not null && mapa.StartsWith("casa_jogador", StringComparison.Ordinal);

    /// <summary>18/11 15h: o terreno (quintal) da casa — onde vai a "decoração de fora" (decor_terreno_*).</summary>
    public static bool Lote(string? mapa) => TerrenoOuCasa(mapa) && !Interior(mapa);
    /// <summary>Peça de fora (quintal) — só no terreno; as outras só dentro de casa.</summary>
    public static bool DeFora(string? decorId) => decorId is not null && decorId.StartsWith("decor_terreno_", StringComparison.Ordinal);

    public static bool Interior(string? mapa) => mapa is "casa_jogador" or "casa_jogador_dentro" or "casa_jogador_sobrado2" or "casa_jogador_mansao_dentro" or "casa_jogador_mansao_cima";

    public static IEnumerable<(double X, double Y, double Yaw, bool Casa)> Lista(MapLayout l, IEnumerable<string> flags)
    {
        foreach (var p in l.Props)
        {
            // 18/11 15h (dono: "se tem banco, senta — é meio lógico, regra"): todo banco do jogo (o de praça e o modelo novo)
            if (p.Kind is "banco_praca" or "glb:cenario_banco_praca" or "movel_banqueta") yield return (p.X, p.Y, p.Yaw, TerrenoOuCasa(l.Id));
            else if (p.Kind == "movel_cadeira") yield return (p.X, p.Y, p.Yaw, Interior(l.Id));
        }
        if (!Interior(l.Id)) yield break;
        var f = flags as ICollection<string> ?? flags.ToList();
        foreach (var (id, padrao) in Padrao)
        {
            if (!f.Contains("decor:" + id)) continue;
            var pos = f.FirstOrDefault(x => x.StartsWith("decorpos:" + id + "=", StringComparison.Ordinal));
            if (pos is not null)
            {
                var v = pos[(pos.IndexOf('=') + 1)..].Split('|');
                if (v.Length >= 4 && v[0] == l.Id
                    && double.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    && double.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                    && double.TryParse(v[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw))
                    yield return (x, y, yaw, true);
            }
            else if (l.Id == "casa_jogador") yield return (padrao.X, padrao.Y, padrao.Yaw, true);
        }
    }
}

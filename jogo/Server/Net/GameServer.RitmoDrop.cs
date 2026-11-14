using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Content;
using CinzasDaMare.Server.Persistence;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 14/11 (pedido do dono: "fluxo de coisas pequenas entre os itens bons, e o OLOCO de vez em quando — tudo alinhado
/// matematicamente"): mede o ritmo de drop com o MESMO sorteio do servidor (SortearDrops), matando N vezes os monstros
/// de um mapa como um jogador sozinho faria (cada bicho pesa pelo tempo de renascer: o comum aparece muito mais que a
/// elite; chefe fica de fora — é visita, não farm). Devolve quantos de cada tipo saem por abate.
/// Usado pelo RitmoDeDropTests (que confere as metas de Docs/ECONOMIA.md) e pela tabela do relatório.
/// </summary>
public sealed partial class GameServer
{
    public static readonly string[] TiposDeDrop =
        { "equip", "incomum+", "raro+", "epico+", "excelente", "lendario", "conjunto", "conjunto_v7+", "joia", "colecao", "pandora", "caixa", "consumivel", "material" };

    public Dictionary<string, double> SimularDrops(string mapa, string classe, int abates)
    {
        var conta = TiposDeDrop.ToDictionary(t => t, _ => 0.0);
        if (!_zones.TryGetValue(mapa, out var z)) return conta;
        var bichos = z.Monsters.Where(m => !m.Def.Boss && m.Master is null && !m.GodSpawned && m.Def.LootTableId is not null).ToList();
        if (bichos.Count == 0) return conta;
        // peso = 1 / tempo de renascer (o comum volta rápido; a elite, devagar)
        var pesos = bichos.Select(m => m.Spawn is { } sp ? sp.Chance / Math.Max(10, sp.RespawnSeconds) : 1.0 / 30).ToList();   // raro: só aparece às vezes
        var total = pesos.Sum();
        var classes = new[] { classe };
        var rnd = new Random(1411);
        var vivo = _world!.LivelyMonsters;
        _world.LivelyMonsters = true;   // o jogo de verdade roda com o mundo vivo (joias, conjuntos, qualidade)
        for (var k = 0; k < abates; k++)
        {
            var r = rnd.NextDouble() * total;
            var i = 0;
            while (i < bichos.Count - 1 && r >= pesos[i]) { r -= pesos[i]; i++; }
            foreach (var d in SortearDrops(bichos[i], classes) ?? new List<GrantItem>())
            {
                if (_content.Consumables.ContainsKey(d.DefinitionId))
                {
                    conta[d.DefinitionId.StartsWith("caixa_pandora") ? "pandora" : d.DefinitionId.StartsWith("caixa_") ? "caixa" : "consumivel"]++;
                    continue;
                }
                if (!_content.Items.TryGetValue(d.DefinitionId, out var def)) continue;
                if (Refine.IsJewel(d.DefinitionId)) { conta["joia"]++; continue; }
                if (ColecaoRaras.Contains(def.Id) || ColecaoEpicas.Contains(def.Id) || ColecaoLendarias.Contains(def.Id)) { conta["colecao"]++; continue; }
                if (!OpcoesMu.Tem(def.Category)) { conta["material"]++; continue; }
                conta["equip"]++;
                var v = Variacao.Of(d.Quality);
                if (v > 0) conta["conjunto"]++;
                if (v >= 7) conta["conjunto_v7+"]++;
                var deg = v > 0 ? (v >= 10 ? 4 : v >= 7 ? 3 : v >= 4 ? 2 : 0) : d.Quality switch
                {
                    "incomum" => 1, "raro" => 2, "epico" or "chefe" => 3, "excelente" => 4, "lendario" => 5, _ => 0,
                };
                if (deg >= 1) conta["incomum+"]++;
                if (deg >= 2) conta["raro+"]++;
                if (deg >= 3) conta["epico+"]++;
                if (deg == 4) conta["excelente"]++;
                if (deg == 5) conta["lendario"]++;
            }
        }
        _world.LivelyMonsters = vivo;
        foreach (var t in TiposDeDrop) conta[t] /= abates;
        return conta;
    }
}

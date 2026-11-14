using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Server.Persistence;
using CinzasDaMare.Shared;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 14/11 noite (pranchas do dono: asas, capas e skins; Tools/gerar_colecao_dono.py). As peças raras da coleção caem de
/// CHEFE (e, bem mais raro, de elite) da faixa de nível: o item tem nível entre o do monstro −4 e +6... na prática,
/// o chefe solta o que é "da idade" dele. Chance por abate de chefe: rara 5%, épica 2,5%, lendária 0,8% (elite: ÷8).
/// Cai como "comum" (a qualidade de asa/capa/skin não muda atributo).
/// </summary>
public sealed partial class GameServer
{
    private static readonly HashSet<string> ColecaoRaras = new()
    {
        "asas_tempestade", "asas_gelo", "asas_fogo", "asas_sombrias", "asas_celestiais",
        "capa_amaldicoada", "capa_lava", "capa_dragao", "capa_celestial", "capa_morte", "capa_luz", "capa_tempo",
        "bracelete_tubarao", "bracelete_lava",
    };
    private static readonly HashSet<string> ColecaoEpicas = new() { "asas_draconicas", "asas_demoniacas", "asas_vazio", "asas_runicas", "asas_arcanjo",
        "asas_set_cavaleiro_sagrado", "asas_set_gelo_eterno", "asas_set_dragao_fogo", "asas_set_celestial", "asas_set_demoniaco", "asas_set_anjo_caido" };
    private static readonly HashSet<string> ColecaoLendarias = new() { "asas_dragao_anciao", "asas_fenrir", "asas_eclipse", "asas_tempo", "asas_criacao" };

    internal const double ColecaoRara = 0.05, ColecaoEpica = 0.025, ColecaoLendaria = 0.008;

    private GrantItem? ColecaoDrop(MonsterInstance m)
    {
        if (!m.Def.Boss && !m.Def.Elite) return null;
        var mult = m.Def.Boss ? 1.0 : 1.0 / 8;
        foreach (var (lista, chance) in new[] { (ColecaoLendarias, ColecaoLendaria), (ColecaoEpicas, ColecaoEpica), (ColecaoRaras, ColecaoRara) })
        {
            if (_rng() >= chance * mult) continue;
            var cabe = lista.Where(id => _content.Items.TryGetValue(id, out var d) && d.MinLevel >= m.Level - 6 && d.MinLevel <= m.Level + 4).ToList();
            if (cabe.Count == 0) continue;
            return new GrantItem(cabe[Math.Min(cabe.Count - 1, (int)(_rng() * cabe.Count))], 1, "comum");
        }
        return null;
    }
}

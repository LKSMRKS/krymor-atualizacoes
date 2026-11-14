using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Diagnostics;
using CinzasDaMare.Shared.Net;

namespace CinzasDaMare.Server.Net;

/// <summary>
/// 13/11 (pedido do Barto: "monstros migratórios — criaturas raras circulam entre áreas do mundo"): sempre há UM migrante
/// solto no mundo (Lobo Branco, Morcego Albino, Sapo Dourado, Sineiro Perdido, Lobo de Neve Antigo, Salamandra Antiga,
/// Harpia Rainha). A cada 12 minutos, se ninguém o derrubou, ele muda de mapa (um mapa aberto de nível parecido) e corre o
/// boato no chat: "foi visto em ...". Derrubado: solta o saque normal + uma Caixa das Cinzas de um nível acima, e o
/// próximo aparece em outro canto depois de 6 minutos.
/// </summary>
public sealed partial class GameServer
{
    private static readonly string[] MigrantesIds =
    {
        "monster_mata_lobo_branco", "monster_canions_morcego_albino", "monster_brejo_sapo_dourado", "monster_vespera_sineiro",
        "monster_geada_lobo_lendario", "monster_escoria_salamandra", "monster_acropole_harpia_rainha",
    };
    private MonsterInstance? _migrante;
    private long _migranteProximoMs;
    private Dictionary<string, double>? _nivelDoMapa;

    private double NivelDoMapa(string mapId)
    {
        _nivelDoMapa ??= _world!.Maps.Values.Where(m => m.Spawns.Count > 0)
            .ToDictionary(m => m.Id, m => m.Spawns.Average(s => (double)s.Level));
        return _nivelDoMapa.GetValueOrDefault(mapId, 0);
    }

    private void TickMigrantes(long now)
    {
        if (!MapMode || _world is null) return;
        if (_migranteProximoMs == 0) { _migranteProximoMs = now + 3 * 60_000; return; }   // o primeiro sai 3 min depois de abrir
        if (_migrante is { } atual && (atual.Gone || !atual.State.Alive))
        {
            _migrante = null;
            _migranteProximoMs = now + 6 * 60_000;
        }
        if (now < _migranteProximoMs) return;
        _migranteProximoMs = now + 12 * 60_000;
        var defs = MigrantesIds.Where(id => _content.Monsters.ContainsKey(id)).Select(id => _content.Monsters[id]).ToList();
        if (defs.Count == 0) return;
        var def = _migrante?.Def ?? defs[(int)(_rng() * defs.Count) % defs.Count];
        string? deOnde = _migrante?.Zone.Layout?.DisplayName;
        // mapas abertos (não instância, sem cidade) com nível médio a até 10 do bicho, e diferente de onde está
        var opcoes = _zones.Values.Where(z => !z.Instanced && z.Layout is { Kind: "campo" } l && l.Spawns.Count > 0
                                              && Math.Abs(NivelDoMapa(l.Id) - def.Level) <= 10 && !ReferenceEquals(z, _migrante?.Zone)).ToList();
        if (opcoes.Count == 0) opcoes = _zones.Values.Where(z => !z.Instanced && z.Layout is { Kind: "campo" }).ToList();
        if (opcoes.Count == 0) return;
        if (_migrante is not null) _migrante.Gone = true;   // sai de onde estava
        var z = opcoes[(int)(_rng() * opcoes.Count) % opcoes.Count];
        var nivel = (int)Math.Round(Math.Max(def.Level, NivelDoMapa(z.Layout!.Id) + 3));
        if (SpawnEspalhado(z, def, nivel, 1, "migrante") == 0) return;
        _migrante = z.PendingAdds.LastOrDefault(m => m.Evento == "migrante");
        var texto = deOnde is null
            ? $"Boato: um(a) {def.SpeciesName} (nível {nivel}) foi visto(a) em {z.Layout.DisplayName}. Criatura rara — não fica muito tempo no mesmo lugar."
            : $"Boato: o(a) {def.SpeciesName} deixou {deOnde} e agora anda por {z.Layout.DisplayName} (nível {nivel}).";
        foreach (var p in _sessions.Values.Where(x => x.LoggedIn)) SendTo(p, new NoticeMessage(texto, "legend"));
        GameLog.Info("servidor.mundo", $"migrante {def.Id} em {z.Layout.Id} (nível {nivel})");
    }

    private void LootDoMigrante(MonsterInstance m, List<Persistence.GrantItem> drops)
    {
        if (m.Evento != "migrante") return;
        drops.Add(new Persistence.GrantItem(CaixaId(Math.Min(5, CaixaTier(m.Level) + 1)), 1, "comum"));
        foreach (var p in _sessions.Values.Where(x => x.LoggedIn))
            SendTo(p, new NoticeMessage($"O(a) {m.Def.SpeciesName} migrante foi derrubado(a) em {m.Zone.Layout?.DisplayName}!", "info"));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using CinzasDaMare.Shared.Client;

namespace CinzasDaMare.Client;

/// <summary>
/// "Bichinho-guia" (pedido do usuário): de tempos em tempos o Pip, um vagalume de companhia, dá UMA dica do que fazer
/// agora — pontos de atributo, missão pronta, tomo na mochila, item melhor guardado, região fácil demais, sem poção.
/// Só lê o estado que o cliente já tem (não decide nada); não repete a mesma dica em menos de 5 minutos.
/// </summary>
public sealed class GuideHints
{
    private readonly Dictionary<string, double> _lastShown = new();
    private double _next = 25;          // primeira dica ~25 s depois de entrar
    private const double Every = 75;    // depois, no máximo uma a cada 75 s
    private const double Repeat = 300;  // a mesma dica só volta depois de 5 min
    public bool Enabled { get; set; } = true;

    /// <summary>Devolve a dica a mostrar agora (ou null). t = segundos de sessão.</summary>
    public string? Tick(ClientModel m, double t, bool inCombat)
    {
        if (!Enabled || t < _next || inCombat || !m.LoggedIn || m.Map is null) return null;
        foreach (var (key, text) in Candidates(m))
        {
            if (_lastShown.TryGetValue(key, out var when) && t - when < Repeat) continue;
            _lastShown[key] = t;
            _next = t + Every;
            return "Pip, o vagalume-guia: " + text;
        }
        _next = t + 20;   // nada a dizer: olha de novo daqui a pouco
        return null;
    }

    private static IEnumerable<(string Key, string Text)> Candidates(ClientModel m)
    {
        if (m.Stats is { Points: > 0 } st)
            yield return ("pontos", $"você tem {st.Points} ponto(s) de atributo para distribuir — abra com C.");
        if (m.Quests.FirstOrDefault(q => q.State == "ready") is { } ready)
            yield return ("entregar:" + ready.Id, $"a missão \"{ready.Name}\" está pronta — volte e fale com {ready.Giver}.");
        var tome = m.Inventory.Select(i => i.DefinitionId)
            .FirstOrDefault(d => d.StartsWith("tomo_" + m.ClassId + "_", StringComparison.Ordinal));
        if (tome is not null)
            yield return ("tomo:" + tome, $"tem {ClientModel.DisplayName(tome)} na mochila — use para aprender o poder (I, botão direito).");
        foreach (var it in m.Inventory)
        {
            if (m.ItemInfoFor(it.DefinitionId) is not { Slot: not "consumable" and not "material" and not "" } info) continue;
            if (info.Classes.Count > 0 && !info.Classes.Contains(m.ClassId)) continue;
            if ((m.Progress?.Level ?? 1) < info.MinLevel) continue;
            var diff = ItemTooltip.Compare(m, info);
            if (diff.Count > 0 && diff.All(d => d.Better))
            {
                yield return ("melhor:" + it.DefinitionId, $"{info.DisplayName} na mochila é melhor que o que você usa — equipe (I).");
                break;
            }
        }
        if (!m.Quests.Any(q => q.State is "active" or "ready"))
            yield return ("sem_missao", "sem missão ativa — procure quem tem o losango amarelo sobre a cabeça.");
        var potions = m.Inventory.Where(i => i.DefinitionId.Contains("tonico")).Sum(i => i.Quantity);
        if (potions == 0)
            yield return ("pocao", "você está sem tônicos de vida — a curandeira Ilma vende no Porto.");
        // lugar fácil demais: o jogador passou bem do nível dos bichos daqui
        var level = m.Progress?.Level ?? 1;
        var spawns = m.Map!.Spawns;
        if (spawns.Count > 0 && level > spawns.Max(s => s.Level) + 3)
            yield return ("facil:" + m.Map.Id, "aqui já ficou fácil para você — o mapa do mundo (M) mostra a próxima região.");
    }
}

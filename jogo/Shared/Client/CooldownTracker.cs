namespace CinzasDaMare.Shared.Client;

/// <summary>
/// Estimativa VISUAL de recargas para o HUD (o servidor continua sendo a autoridade: ele recusa o que estiver em recarga).
/// As durações espelham Content/skills.json e consumables.json; um teste garante que não divergem.
/// </summary>
public sealed class CooldownTracker
{
    public const string Potion = "potion";
    public const string Mana = "mana";
    public const string Food = "food";
    public const long PotionMs = 12_000;

    public static readonly IReadOnlyDictionary<string, long> SkillMs = new Dictionary<string, long>
    {
        [ClientModel.SkillGolpe] = 4_000,
        [ClientModel.SkillGuarda] = 10_000,
    };

    private readonly Dictionary<string, (long Start, long Duration)> _active = new();

    public void Reset() => _active.Clear();

    public void Start(string key, long nowMs, long durationMs) => _active[key] = (nowMs, durationMs);

    private readonly Dictionary<string, long> _registered = new();

    /// <summary>Durações vindas do catálogo do servidor (E8: habilidades de cada classe).</summary>
    public void Register(string skillId, long durationMs) => _registered[skillId] = durationMs;

    public void StartSkill(string skillId, long nowMs)
    {
        if (_registered.TryGetValue(skillId, out var registered)) { if (registered > 0) Start(skillId, nowMs, registered); }
        else if (SkillMs.TryGetValue(skillId, out var duration)) Start(skillId, nowMs, duration);
    }

    public long RemainingMs(string key, long nowMs)
    {
        if (!_active.TryGetValue(key, out var c)) return 0;
        return Math.Max(0, c.Start + c.Duration - nowMs);
    }

    /// <summary>1 = acabou de começar, 0 = pronto.</summary>
    public double Fraction(string key, long nowMs)
    {
        if (!_active.TryGetValue(key, out var c) || c.Duration <= 0) return 0;
        return Math.Clamp((double)RemainingMs(key, nowMs) / c.Duration, 0.0, 1.0);
    }
}

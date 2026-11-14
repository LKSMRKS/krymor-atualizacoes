using System.Text.Json.Serialization;

namespace CinzasDaMare.Server.Combat;

/// <summary>Modificadores da forma, somados em porcentagem (0,35 = +35%; -0,40 = -40%).</summary>
public sealed record ModificadoresForma(
    [property: JsonPropertyName("max_health_pct")] double MaxHealthPct = 0,
    [property: JsonPropertyName("damage_pct")] double DamagePct = 0,
    [property: JsonPropertyName("armor_pct")] double ArmorPct = 0,
    [property: JsonPropertyName("move_speed_pct")] double MoveSpeedPct = 0,
    [property: JsonPropertyName("attack_speed_pct")] double AttackSpeedPct = 0,
    [property: JsonPropertyName("magic_resist_pct")] double MagicResistPct = 0);

/// <summary>Uma linha de Content/transformacoes.json.</summary>
public sealed class FormaDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; init; } = "";
    [JsonPropertyName("relic")] public string Relic { get; init; } = "";
    [JsonPropertyName("visual")] public string Visual { get; init; } = "";
    [JsonPropertyName("min_level")] public int MinLevel { get; init; }
    /// <summary>12/11 (pedido do dono: "cada transformação é para uma classe"): só esta classe usa a forma.</summary>
    [JsonPropertyName("class")] public string? Class { get; init; }
    [JsonPropertyName("duration_ms")] public int DurationMs { get; init; }
    [JsonPropertyName("cooldown_ms")] public int CooldownMs { get; init; }
    [JsonPropertyName("modifiers")] public ModificadoresForma Modifiers { get; init; } = new();
    [JsonPropertyName("basic_skill")] public string BasicSkill { get; init; } = "";
    [JsonPropertyName("skills")] public List<string> Skills { get; init; } = new();
    [JsonPropertyName("passiva")] public Dictionary<string, double> Passiva { get; init; } = new();
    [JsonPropertyName("restricoes")] public List<string> Restricoes { get; init; } = new();
    [JsonPropertyName("descricao")] public string Descricao { get; init; } = "";

    public double P(string chave) => Passiva.TryGetValue(chave, out var v) ? v : 0;
}

public enum FalhaForma { Nenhuma, NivelBaixo, JaTransformado, Morto }

/// <summary>
/// Forma ativa de um jogador (servidor). A recarga NÃO mora aqui: a relíquia é um consumível reutilizável no grupo
/// "transformacao", então o ConsumableSystem já guarda a recarga como em qualquer poção.
/// Durante a forma, o GameServer consulta os multiplicadores abaixo e troca as habilidades pelas da forma.
/// </summary>
public sealed class EstadoForma
{
    public FormaDefinition? Ativa { get; private set; }
    public long ComecouEm { get; private set; }
    public long TerminaEm { get; private set; }

    public bool Transformado(long nowMs) => Ativa is not null && nowMs < TerminaEm;
    public int RestanteMs(long nowMs) => Ativa is null ? 0 : (int)Math.Max(0, TerminaEm - nowMs);

    public FalhaForma PodeTransformar(FormaDefinition forma, int nivel, bool vivo, long nowMs)
    {
        if (!vivo) return FalhaForma.Morto;
        if (nivel < forma.MinLevel) return FalhaForma.NivelBaixo;
        if (Transformado(nowMs)) return FalhaForma.JaTransformado;
        return FalhaForma.Nenhuma;
    }

    public void Iniciar(FormaDefinition forma, long nowMs)
    {
        Ativa = forma; ComecouEm = nowMs; TerminaEm = nowMs + forma.DurationMs;
    }

    /// <summary>Chamar a cada tick. Devolve true no tick em que a forma acaba (o servidor volta o visual e as habilidades).</summary>
    public bool Tick(long nowMs)
    {
        if (Ativa is null || nowMs < TerminaEm) return false;
        Ativa = null;
        return true;
    }

    /// <summary>Morte, troca de mapa para cidade, desconexão: volta ao normal na hora.</summary>
    public void Encerrar() => Ativa = null;

    public bool Restrito(string restricao, long nowMs) => Transformado(nowMs) && Ativa!.Restricoes.Contains(restricao);

    // ---------------- multiplicadores (1,0 = sem mudança)
    public double MultVidaMax(long nowMs) => Transformado(nowMs) ? 1 + Ativa!.Modifiers.MaxHealthPct : 1;
    public double MultArmadura(long nowMs) => Transformado(nowMs) ? Math.Max(0, 1 + Ativa!.Modifiers.ArmorPct) : 1;
    public double MultMovimento(long nowMs) => Transformado(nowMs) ? 1 + Ativa!.Modifiers.MoveSpeedPct : 1;
    public double MultResistMagica(long nowMs) => Transformado(nowMs) ? Math.Max(0, 1 + Ativa!.Modifiers.MagicResistPct) : 1;
    /// <summary>Intervalo de ataque fica menor quanto maior a velocidade de ataque.</summary>
    public double MultIntervaloAtaque(long nowMs) => Transformado(nowMs) ? 1 / (1 + Ativa!.Modifiers.AttackSpeedPct) : 1;

    /// <summary>Dano causado. noite/arLivre vêm do ciclo do dia e do mapa; alvoMortoVivoOuDemonio da espécie do alvo.</summary>
    public double MultDano(long nowMs, bool noite, bool arLivre, bool alvoMortoVivoOuDemonio, double vidaAlvoPct)
    {
        if (!Transformado(nowMs)) return 1;
        var f = Ativa!;
        var m = 1 + f.Modifiers.DamagePct;
        if (noite) m += f.P("night_damage_pct");
        else if (arLivre) m += f.P("day_damage_pct");
        if (alvoMortoVivoOuDemonio) m += f.P("damage_vs_undead_demon_pct");
        if (f.P("execute_below_pct") > 0 && vidaAlvoPct < f.P("execute_below_pct")) m *= f.P("execute_damage_mult");
        return Math.Max(0, m);
    }

    /// <summary>Cura recebida (poções, curandeira, habilidades de aliados). Ceifador: 0.</summary>
    public double MultCuraRecebida(long nowMs) => Transformado(nowMs) ? Math.Max(0, 1 + Ativa!.P("healing_received_pct")) : 1;

    /// <summary>Fração do dano causado que volta como vida (vampiro).</summary>
    public double RouboDeVida(long nowMs) => Transformado(nowMs) ? Ativa!.P("lifesteal_pct") : 0;

    /// <summary>Fração da vida máxima curada a cada abate (lobisomem, ceifador). Ignora MultCuraRecebida.</summary>
    public double CuraPorAbate(long nowMs) => Transformado(nowMs) ? Ativa!.P("heal_on_kill_pct") : 0;

    /// <summary>Vida perdida por segundo (fração da máxima): vampiro de dia ao ar livre.</summary>
    public double DrenoPorSegundo(long nowMs, bool noite, bool arLivre)
        => Transformado(nowMs) && !noite && arLivre ? Ativa!.P("day_outdoor_health_drain_pct_per_s") : 0;

    /// <summary>Pandemônio: a cada N ms ele para para rir (não age, não anda). Determinístico pelo início da forma.</summary>
    public bool RindoAgora(long nowMs)
    {
        if (!Transformado(nowMs)) return false;
        var cada = Ativa!.P("laugh_every_ms"); var dura = Ativa.P("laugh_stun_ms");
        if (cada <= 0 || dura <= 0) return false;
        var t = (nowMs - ComecouEm) % (long)cada;
        return nowMs - ComecouEm >= cada && t < dura;
    }

    /// <summary>Saco de Surpresas: 0 bomba, 1 bigorna, 2 confete (cura), 3 torta (cegueira). Sorteio do servidor.</summary>
    public static int SortearSurpresa(Random rng) => rng.Next(4);
}

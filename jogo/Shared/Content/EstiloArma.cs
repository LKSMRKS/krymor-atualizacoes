namespace CinzasDaMare.Shared.Content;

/// <summary>
/// 12/11 (dono: "o golpe não muda; o que muda são os atributos — alcance, velocidade, poder. Uma lança full tem mais alcance
/// que uma espada pequena full"): cada tipo de arma traz alcance extra (m), velocidade do golpe básico e poder.
/// Fonte única (servidor aplica; o cliente mostra na ficha do item).
/// </summary>
public readonly record struct EstiloArma(double Alcance, double Dano, double Recuperacao)
{
    public static EstiloArma De(string? tipo, string? id = null)
    {
        var e = tipo switch
        {
            "sword" => new EstiloArma(0.3, 1.0, 1.0),
            "axe" => new EstiloArma(0.2, 1.12, 1.15),
            "club" => new EstiloArma(0.0, 1.18, 1.25),
            "hammer" => new EstiloArma(0.1, 1.28, 1.35),
            "dagger" => new EstiloArma(-0.3, 0.8, 0.65),
            "spear" => new EstiloArma(1.1, 1.05, 1.08),
            "staff" => new EstiloArma(0, 1.08, 1.1),
            "talisman" => new EstiloArma(0, 0.95, 0.9),
            "bow" => new EstiloArma(0, 1.0, 1.0),
            _ => new EstiloArma(0, 1.0, 1.0),
        };
        // tamanho no nome: curta/pequena perde alcance, longa/montante ganha
        if (id is not null && tipo is "sword" or "axe" or "spear")
        {
            if (id.Contains("curta") || id.Contains("pequen") || id.Contains("mao_")) e = e with { Alcance = e.Alcance - 0.3, Recuperacao = e.Recuperacao * 0.95 };
            else if (id.Contains("longa") || id.Contains("montante") || id.Contains("grande") || id.Contains("bastarda")) e = e with { Alcance = e.Alcance + 0.4, Dano = e.Dano * 1.05, Recuperacao = e.Recuperacao * 1.08 };
        }
        return e;
    }

    /// <summary>Texto curto para a ficha do item.</summary>
    public string Texto()
    {
        string Vel(double r) => r <= 0.75 ? "muito rápida" : r < 0.97 ? "rápida" : r <= 1.05 ? "normal" : r <= 1.2 ? "lenta" : "muito lenta";
        string Alc(double a) => a >= 1.0 ? "muito longo" : a >= 0.4 ? "longo" : a >= 0.0 ? "médio" : "curto";
        return $"Alcance {Alc(Alcance)} ({(Alcance >= 0 ? "+" : "")}{Alcance:0.0} m) · velocidade {Vel(Recuperacao)} · poder {(Dano - 1) * 100:+0;-0;0}%";
    }
}

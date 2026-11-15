using System;
using System.Collections.Generic;

namespace CinzasDaMare.Shared.Content;

/// <summary>
/// Atributos estilo MU (todos os personagens têm os quatro; o jogador sobe o que quiser):
/// Força (carregar armadura pesada; dano do Guerreiro), Agilidade (esquiva mais rápida; dano do Arqueiro),
/// Vitalidade (vida) e Energia (recurso e dano mágico de Mago e Bruxa). O atributo principal da classe é o "primário"
/// gravado desde o começo; os outros partem de uma base e recebem os pontos que o jogador quiser.
/// Regra compartilhada: servidor aplica, cliente mostra os mesmos números.
/// </summary>
public static class Atributos
{
    public const string Forca = "forca", Agilidade = "agilidade", Energia = "energia", Vitalidade = "vigor";
    public const int BaseForca = 6, BaseAgilidade = 6, BaseEnergia = 5;

    /// <summary>Qual dos atributos é o primário da classe ("strength" → força, "dexterity" → agilidade, resto → energia).</summary>
    public static string PrimaryKey(string primaryAttribute) => primaryAttribute switch
    {
        "strength" => Forca,
        "dexterity" => Agilidade,
        _ => Energia,
    };

    /// <summary>
    /// 14/11 ("2 são padrões para upar, saca?"): o 2º atributo recomendado de cada classe, estilo MU — o 1º é o principal.
    /// Guerreiro Força+Vitalidade · Arqueiro Destreza+Força · Mago Energia+Destreza · Bruxa Energia+Vitalidade.
    /// </summary>
    public static string Secundario(string classId) => classId switch
    {
        "arqueiro" => Forca,
        "mago" => Agilidade,
        _ => Vitalidade,
    };

    /// <summary>Nome na tela (a chave "agilidade" continua gravada no banco; na tela ela se chama Destreza).</summary>
    public static string Nome(string key) => key switch
    {
        Forca => "Força",
        Agilidade => "Destreza",
        Energia => "Energia",
        Vitalidade => "Vitalidade",
        _ => key,
    };

    /// <summary>Peso de uma peça (armadura/elmo pesam pela proteção; escudo um pouco menos). Pede a mesma Força.</summary>
    public static double Weight(string slot, IReadOnlyDictionary<string, double>? stats)
    {
        if (stats is null || !stats.TryGetValue("armor", out var armor) || armor <= 0) return 0;
        return Math.Round(armor * (slot == "off_hand" ? 0.08 : 0.12), 1);
    }

    /// <summary>0 = carrega bem; 1 = peso muito acima da Força.</summary>
    public static double Overload(double weight, int forca) => weight <= forca || weight <= 0 ? 0 : Math.Clamp((weight - forca) / weight, 0, 1);

    /// <summary>Multiplicador da recarga da esquiva: sobrecarga aumenta; Agilidade (fora a base) diminui até 25%.</summary>
    public static double DodgeMult(double overload, int agilidade)
        => (1.0 + overload) * (1.0 - Math.Min(0.25, Math.Max(0, agilidade - BaseAgilidade) * 0.004));

    /// <summary>Velocidade com sobrecarga: até −25%.</summary>
    public static double SpeedMult(double overload) => 1.0 - 0.25 * overload;
}

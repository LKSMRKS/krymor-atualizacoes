using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CinzasDaMare.Client;

/// <summary>
/// 15/11 noite (pente-fino AAA, item 1: "lógica de 20–50 ms por quadro"): cronômetro por etapa do quadro. O Main marca o
/// fim de cada etapa (<see cref="Marca"/>); o tempo desde a marca anterior soma na etapa. A cada minuto o log de saúde mostra
/// as etapas mais caras (média por quadro e pior quadro) — assim dá para ver onde o FPS vai sem abrir profiler.
/// </summary>
public static class Perfil
{
    private static readonly Stopwatch Relogio = Stopwatch.StartNew();
    private static readonly Dictionary<string, (double Soma, double Pior)> Etapas = new();
    private static readonly Dictionary<string, double> NoQuadro = new();
    private static double _ultima;
    private static int _quadros;

    public static void Inicio() { _ultima = Relogio.Elapsed.TotalMilliseconds; NoQuadro.Clear(); }

    public static void Marca(string etapa)
    {
        var agora = Relogio.Elapsed.TotalMilliseconds;
        NoQuadro[etapa] = NoQuadro.GetValueOrDefault(etapa) + (agora - _ultima);
        _ultima = agora;
    }

    public static void Fim()
    {
        _quadros++;
        foreach (var (k, v) in NoQuadro)
        {
            var e = Etapas.GetValueOrDefault(k);
            Etapas[k] = (e.Soma + v, System.Math.Max(e.Pior, v));
        }
    }

    /// <summary>Resumo e zera: "sync 3,1 (pior 18) · hud 1,2 (4) ...".</summary>
    public static string Resumo(int quantas = 8)
    {
        if (_quadros == 0) return "";
        var q = _quadros;
        var s = string.Join(" · ", Etapas.OrderByDescending(e => e.Value.Soma).Take(quantas)
            .Select(e => $"{e.Key} {e.Value.Soma / q:0.0} (pior {e.Value.Pior:0})"));
        Etapas.Clear(); _quadros = 0;
        return s;
    }
}

using Godot;

namespace CinzasDaMare.Client.Sandbox.Core;

/// <summary>
/// Hitstop do laboratório: desacelera o tempo do motor (<c>Engine.TimeScale</c>) por alguns ms REAIS no impacto.
/// O relógio usado é <c>Time.GetTicksMsec</c> (não escala), então o fim do hitstop não depende do próprio hitstop.
///
/// Nota de promoção: no jogo em rede, mexer em <c>Engine.TimeScale</c> desacelera a interpolação de TODOS os outros
/// (monstros, jogadores). Ao promover, trocar por hitstop só no visual (congelar a animação do atacante e do alvo).
/// </summary>
public static class Hitstop
{
    private static ulong _until;
    private static double _restoreScale = 1.0;

    public static bool Active => _until > 0;

    public static void Trigger(double scale, double ms)
    {
        if (ms <= 0 || scale >= 0.999) return;
        if (_until == 0) _restoreScale = Engine.TimeScale;
        Engine.TimeScale = Mathf.Clamp(scale, 0.01, 1.0);
        var end = Time.GetTicksMsec() + (ulong)ms;
        if (end > _until) _until = end;
    }

    /// <summary>Chamar a cada quadro (o manager chama).</summary>
    public static void Tick()
    {
        if (_until == 0 || Time.GetTicksMsec() < _until) return;
        Engine.TimeScale = _restoreScale;
        _until = 0;
    }

    /// <summary>Desligar o laboratório nunca pode deixar o jogo em câmera lenta.</summary>
    public static void Cancel()
    {
        if (_until == 0) return;
        Engine.TimeScale = _restoreScale;
        _until = 0;
    }
}

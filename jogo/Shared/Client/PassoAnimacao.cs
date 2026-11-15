namespace CinzasDaMare.Shared.Client;

/// <summary>
/// 14/11 (movimento dos inimigos, cliente): velocidade do clipe de andar/correr casada com a velocidade real do corpo,
/// para o pé não deslizar no chão. Matemática pura (testável sem Godot); o cliente usa nos visuais.
/// </summary>
public static class PassoAnimacao
{
    /// <summary>Velocidade do clipe = velocidade do corpo ÷ velocidade "natural" do clipe (a que ele foi animado), limitada.</summary>
    public static float Taxa(float velocidade, float natural, float min, float max)
        => natural <= 0 ? 1f : Math.Clamp(velocidade / natural, min, max);

    /// <summary>Quanto o pé desliza (m/s) com essa taxa: 0 = casado. Positivo = corpo anda mais que o passo.</summary>
    public static float Deslize(float velocidade, float natural, float min, float max)
        => velocidade - natural * Taxa(velocidade, natural, min, max);

    // faixas usadas pelos visuais (clipe natural em m/s, menor e maior taxa sem o clipe ficar esquisito)
    public const float ManequimAndar = 1.6f, ManequimAndarMin = 0.5f, ManequimAndarMax = 2.3f;
    public const float ManequimCorrer = 5.2f, ManequimCorrerMin = 0.6f, ManequimCorrerMax = 1.7f;
    public const float ManequimCorrerAPartir = 3.4f;
    public const float CriaturaAndar = 2.2f, CriaturaAndarMin = 0.45f, CriaturaAndarMax = 2.4f;
    public const float CriaturaCorrer = 7f, CriaturaCorrerMin = 0.6f, CriaturaCorrerMax = 1.7f;
}

using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Profundidade de "campo aberto" (referência do dono, 11/11): perto da câmera o ar é limpo; do meio para o fundo o mundo
/// vai ganhando a cor do céu (névoa de profundidade com perspectiva aérea) e, no gráfico alto, um leve desfoque de longe.
/// Custo: a névoa de profundidade custa o mesmo que a exponencial que já existe (zero a mais); o desfoque de longe só liga
/// no gráfico Alto, em qualidade baixa de bokeh (barato).
/// </summary>
public static class Profundidade
{
    /// <summary>(começo, fim, curva, intensidade máxima 0–1, desfoque a partir de) por tema. Cavernas e interiores: null.</summary>
    private static (float Inicio, float Fim, float Curva, float Max, float Desfoque)? Perfil(string tema) => tema switch
    {
        "praia" => (32f, 210f, 1.6f, 0.62f, 55f),
        "cidade" => (28f, 150f, 1.5f, 0.50f, 50f),
        "vale" => (30f, 190f, 1.6f, 0.66f, 55f),
        "mata" => (22f, 140f, 1.4f, 0.70f, 45f),
        "brejo" => (16f, 110f, 1.2f, 0.80f, 40f),
        "canion" => (40f, 260f, 1.8f, 0.58f, 65f),
        "neve" => (26f, 170f, 1.5f, 0.70f, 50f),
        "lava" => (30f, 180f, 1.6f, 0.55f, 55f),
        "acropole" => (35f, 220f, 1.7f, 0.60f, 60f),
        "fantasma" => (18f, 120f, 1.3f, 0.75f, 45f),
        _ => null,
    };

    public static bool Ativo(string tema) => Perfil(tema) is not null;

    /// <summary>
    /// Chamar no fim de MapView.AddEnvironment, passando o WorldEnvironment criado ali
    /// (trocar `Root.AddChild(new WorldEnvironment { Environment = _env });` por
    /// `var we = new WorldEnvironment { Environment = _env }; Root.AddChild(we); Profundidade.Aplicar(_env, we, Map.Theme);`).
    /// </summary>
    public static void Aplicar(Godot.Environment env, WorldEnvironment we, string tema)
    {
        if (Perfil(tema) is not { } p) return;
        env.FogMode = Godot.Environment.FogModeEnum.Depth;
        env.FogDepthBegin = p.Inicio;
        env.FogDepthEnd = p.Fim;
        env.FogDepthCurve = p.Curva;
        env.FogDensity = p.Max;
        env.FogAerialPerspective = 0.9f;   // o fundo pega a cor do céu (azul de dia, laranja no fim de tarde)
        env.FogSkyAffect = 0.35f;          // o horizonte do céu se mistura com as serras ao longe
        // a névoa baixa de vale (FogHeight/FogHeightDensity) continua como estava

        if (GraphicsQuality.Current == GraphicsQuality.Level.Alto && GraphicsQuality.Modern)
        {
            RenderingServer.CameraAttributesSetDofBlurQuality(RenderingServer.DofBlurQuality.Low, false);
            RenderingServer.CameraAttributesSetDofBlurBokehShape(RenderingServer.DofBokehShape.Box);
            we.CameraAttributes = new CameraAttributesPractical
            {
                DofBlurFarEnabled = true,
                DofBlurFarDistance = p.Desfoque,
                DofBlurFarTransition = 45f,
                DofBlurAmount = 0.05f,
            };
        }
    }

    /// <summary>
    /// A névoa do ciclo do dia (MapView, onde hoje está `_env.FogDensity = look.FogDensity * ...`) passa a usar esta conta
    /// quando o tema tem perfil: de noite a névoa fecha um pouco mais (o fundo some no escuro).
    /// Uso: `_env.FogDensity = Profundidade.Ativo(Map.Theme) ? Profundidade.Densidade(Map.Theme, Night) : look.FogDensity * (...)`.
    /// </summary>
    public static float Densidade(string tema, float noite)
        => Perfil(tema) is { } p ? Mathf.Clamp(p.Max + 0.12f * noite, 0f, 0.95f) : 0f;
}

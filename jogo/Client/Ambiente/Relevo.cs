using System;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Chão que não é totalmente reto (pedido 11/11): ondulações suaves e largas (±0,2–0,5 m, 20–100 m de comprimento) só
/// no visual. O servidor continua vendo o chão plano (X/Y), então nada muda nas regras; o cliente só põe pés, objetos e
/// o terreno na mesma altura. A conta abaixo é IGUAL à do shader do terreno (função `relevo`), então tudo encaixa.
/// Some na borda do mapa (o mar e o horizonte continuam no nível 0) e não existe na cidade, em interiores e cavernas.
/// </summary>
public static class Relevo
{
    public static float Amplitude { get; private set; }
    public static float MeiaLargura { get; private set; } = 1000f;

    /// <summary>Chamar no começo do MapView (antes de montar terreno e objetos): Relevo.Configurar(Map.Theme, (float)Map.HalfSize, Map.Instanced).</summary>
    public static void Configurar(string tema, float meiaLargura, bool instanciado)
    {
        MeiaLargura = meiaLargura;
        Amplitude = instanciado ? 0f : tema switch
        {
            "praia" => 0.25f, "vale" => 0.45f, "mata" => 0.4f, "brejo" => 0.2f, "canion" => 0.5f,
            "neve" => 0.45f, "acropole" => 0.3f, "lava" => 0.3f, "fantasma" => 0.3f,
            _ => 0f,   // cidade, interiores e grutas: chão reto (casas e calçamento)
        };
    }

    /// <summary>Altura do chão no ponto do mapa (x, y do servidor).</summary>
    public static float Altura(double mapX, double mapY)
    {
        if (Amplitude <= 0f) return 0f;
        var x = (float)mapX; var z = (float)mapY;
        var borda = Math.Clamp((MeiaLargura - Math.Max(Math.Abs(x), Math.Abs(z))) / 20f, 0f, 1f);
        return Amplitude * borda * (
            0.55f * MathF.Sin(x * 0.061f + 1.7f) * MathF.Sin(z * 0.047f + 0.4f) +
            0.30f * MathF.Sin((x * 0.8f + z * 0.6f) * 0.093f + 2.1f) +
            0.15f * MathF.Sin((-0.5f * x + 0.87f * z) * 0.19f + 0.9f));
    }

    /// <summary>Trecho para colar no TerrainShader (MapView): uniforms + função + vertex(). Ver LEIA.md.</summary>
    public const string ShaderRelevo = @"
uniform float relevo_amp = 0.0;
uniform float relevo_meia = 1000.0;
float relevo(vec2 p) {
  float f = clamp((relevo_meia - max(abs(p.x), abs(p.y))) / 20.0, 0.0, 1.0);
  return relevo_amp * f * (0.55 * sin(p.x * 0.061 + 1.7) * sin(p.y * 0.047 + 0.4)
       + 0.30 * sin((p.x * 0.8 + p.y * 0.6) * 0.093 + 2.1)
       + 0.15 * sin((-0.5 * p.x + 0.87 * p.y) * 0.19 + 0.9));
}";

    public const string ShaderVertex = @"
void vertex() {
  vec3 w0 = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
  float e = 0.5;
  float h = relevo(w0.xz);
  float hx = relevo(w0.xz + vec2(e, 0.0));
  float hz = relevo(w0.xz + vec2(0.0, e));
  VERTEX.y += h;
  NORMAL = normalize(vec3(-(hx - h) / e, 1.0, -(hz - h) / e));
  wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}";
}

using System;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Qualidade gráfica do cliente (só aparência; nada aqui afeta regras). Três níveis:
/// Alto = sombra de contato (SSAO), luz rebatida (SSIL), névoa com volume, reflexos em poças, sombras nas chamas grandes;
/// Médio = SSAO e névoa com volume; Baixo = só o essencial (para PCs fracos ou o renderizador de compatibilidade).
/// Escolha: argumento "--graficos alto|medio|baixo" ou tecla F10 (alterna e grava em user://graficos.cfg).
/// </summary>
public static class GraphicsQuality
{
    public enum Level { Baixo = 0, Medio = 1, Alto = 2 }

    private const string File = "user://graficos.cfg";
    private static Level? _level;

    public static Level Current
    {
        get
        {
            if (_level is { } l) return l;
            _level = Load();
            return _level.Value;
        }
    }

    /// <summary>Renderizador moderno (Forward+/Mobile). No de compatibilidade (OpenGL) os efeitos de tela não existem.</summary>
    /// <summary>Quanto capim/flores/moitas espalhar (gráfico baixo: metade).</summary>
    public static float ScatterScale => Current == Level.Baixo ? 0.5f : 1f;

    public static bool Modern => RenderingServer.GetCurrentRenderingMethod() != "gl_compatibility";

    public static string Label(Level l) => l switch { Level.Alto => "alto", Level.Medio => "médio", _ => "baixo" };

    public static Level Cycle()
    {
        _level = (Level)(((int)Current + 2) % 3); // alto → médio → baixo → alto
        try
        {
            var cfg = new ConfigFile();
            cfg.SetValue("graficos", "nivel", (int)_level.Value);
            cfg.Save(File);
        }
        catch (Exception e) { GD.PrintErr($"Não gravou {File}: {e.Message}"); }
        return _level.Value;
    }

    private static Level Load()
    {
        var args = OS.GetCmdlineUserArgs();
        for (var i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--graficos")
                return args[i + 1].ToLowerInvariant() switch { "baixo" => Level.Baixo, "medio" or "médio" => Level.Medio, _ => Level.Alto };
        try
        {
            var cfg = new ConfigFile();
            if (cfg.Load(File) == Error.Ok) return (Level)Math.Clamp((int)cfg.GetValue("graficos", "nivel", 2), 0, 2);
        }
        catch (Exception e) { GD.PrintErr($"Não leu {File}: {e.Message}"); }
        return Level.Alto;
    }

    /// <summary>Antisserrilhado e filtro de sombra suave da janela (vale para todos os mapas).</summary>
    public static void ApplyViewport(Viewport vp)
    {
        var q = Current;
        vp.Msaa3D = q == Level.Baixo ? Viewport.Msaa.Disabled : q == Level.Medio ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Msaa4X;
        vp.ScreenSpaceAA = q == Level.Baixo ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        var soft = q == Level.Alto ? RenderingServer.ShadowQuality.SoftHigh : q == Level.Medio ? RenderingServer.ShadowQuality.SoftLow : RenderingServer.ShadowQuality.Hard;
        RenderingServer.DirectionalSoftShadowFilterSetQuality(soft);
        RenderingServer.PositionalSoftShadowFilterSetQuality(soft);
    }
}

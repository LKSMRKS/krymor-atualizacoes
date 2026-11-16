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

    /// <summary>15/11 noite: placa de vídeo dedicada boa (RTX, RX 6000+, Arc, GTX 1080): o gráfico alto roda liso.</summary>
    public static bool PlacaForte()
    {
        try
        {
            var nome = RenderingServer.GetVideoAdapterName().ToUpperInvariant();
            return nome.Contains("RTX") || nome.Contains("RX 6") || nome.Contains("RX 7") || nome.Contains("RX 9") || nome.Contains("ARC") || nome.Contains("GTX 1080");
        }
        catch { return false; }
    }

    public static Level Cycle()
    {
        _level = (Level)(((int)Current + 2) % 3); // alto → médio → baixo → alto
        try
        {
            var cfg = new ConfigFile();
            cfg.Load(File);   // 15/11: mantém o FPS gravado
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

    // 15/11 noite (dono, polimento AAA nº 14: "coloca nas configs pra escolher o FPS"): limite de quadros e sincronia
    // vertical, gravados no mesmo user://graficos.cfg. 0 = sem limite. Padrão: sem limite e V-Sync ligado (como era).
    public static readonly int[] FpsOpcoes = { 30, 60, 90, 120, 144, 165, 240, 0 };
    public static int FpsLimite { get; private set; }
    public static bool VSync { get; private set; } = true;
    public static string FpsLabel(int f) => f == 0 ? "sem limite" : f + " FPS";

    public static void CarregarFps()
    {
        try
        {
            var cfg = new ConfigFile();
            if (cfg.Load(File) == Error.Ok)
            {
                FpsLimite = Math.Max(0, (int)cfg.GetValue("fps", "limite", 0));
                VSync = (bool)cfg.GetValue("fps", "vsync", true);
            }
        }
        catch (Exception e) { GD.PrintErr($"Não leu o FPS de {File}: {e.Message}"); }
        AplicarFps();
    }

    public static void MudarFps(int limite, bool vsync)
    {
        FpsLimite = Math.Max(0, limite); VSync = vsync;
        AplicarFps();
        try
        {
            var cfg = new ConfigFile();
            cfg.Load(File);   // mantém o nível gravado
            cfg.SetValue("fps", "limite", FpsLimite);
            cfg.SetValue("fps", "vsync", VSync);
            cfg.Save(File);
        }
        catch (Exception e) { GD.PrintErr($"Não gravou o FPS em {File}: {e.Message}"); }
    }

    private static void AplicarFps()
    {
        Engine.MaxFps = FpsLimite;
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
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

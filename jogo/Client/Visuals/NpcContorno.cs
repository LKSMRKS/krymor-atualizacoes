using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Contorno do NPC quando o nome aparece / o mouse passa por cima (pedido do dono):
///   Nada     -> branco leve (só conversa)
///   Missao   -> amarelo (tem missão para dar)
///   Entregar -> verde (missão pronta para entregar)
/// Uso: NpcContorno.Mostrar(npcRoot, NpcContorno.Estado.Missao);  e  NpcContorno.Esconder(npcRoot);
/// Funciona em qualquer Node3D: aplica um MaterialOverlay (casca invertida) em todas as malhas filhas.
/// </summary>
public static class NpcContorno
{
    public enum Estado { Nada, Missao, Entregar }

    private static Shader? _shader;
    private static readonly System.Collections.Generic.Dictionary<Estado, ShaderMaterial> Cache = new();

    public static Color Cor(Estado e) => e switch
    {
        Estado.Missao => new Color(1f, 0.82f, 0.18f, 0.7f),
        Estado.Entregar => new Color(0.35f, 1f, 0.35f, 0.7f),
        _ => new Color(1f, 1f, 1f, 0.4f),
    };

    public static ShaderMaterial Material(Estado e)
    {
        if (Cache.TryGetValue(e, out var m)) return m;
        _shader ??= GD.Load<Shader>("res://Assets/Shaders/contorno_npc.gdshader");
        m = new ShaderMaterial { Shader = _shader };
        m.SetShaderParameter("cor", Cor(e));
        m.SetShaderParameter("pulso", e == Estado.Nada ? 0.1f : 0.25f);
        Cache[e] = m;
        return m;
    }

    public static void Mostrar(Node npc, Estado estado)
    {
        var mat = Material(estado);
        Aplicar(npc, mat);
    }

    public static void Esconder(Node npc) => Aplicar(npc, null);

    private static void Aplicar(Node n, Material? mat)
    {
        if (n is GeometryInstance3D g && n is not Label3D && n is not Sprite3D) g.MaterialOverlay = mat;
        foreach (var c in n.GetChildren()) Aplicar(c, mat);
    }
}

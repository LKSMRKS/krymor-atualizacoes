using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 15/11 (dono: "deixe os ícones 3D ali atrás perto da nuca com mais detalhes, mais profundidade, uma caveira parecer
/// uma caveira"): os enfeites das capas premium agora são modelos de verdade (Assets/Models/emblemas/emb_*.glb),
/// com relevo, cavidades e sombra pintada nos vértices. Arquivo faltando ou EMBLEMAS_ANTIGOS=1 → volta o procedural.
/// </summary>
public static class EmblemasGlb
{
    private static readonly bool Antigos = OS.GetEnvironment("EMBLEMAS_ANTIGOS") == "1";

    public static Node3D? Criar(string tipo)
    {
        if (Antigos || GlbCache.Instance("emblemas/emb_" + tipo) is not { } modelo) return null;
        var n = new Node3D { Name = "Emblema" };
        n.AddChild(modelo);
        // quem chama põe o enfeite na altura da gola (escala 1,7, virado para trás); o modelo desce um pouco e sai para
        // fora do volume da gola, para não ficar enterrado nela, e cada tipo tem o seu tamanho
        modelo.Position = new Vector3(0, -0.035f, 0.035f);
        modelo.Scale = Vector3.One * tipo switch
        {
            "dragao" => 1.45f, "leao" => 1.25f, "corvo" => 1.15f, "coroa" => 1.6f, "lua" => 1.15f, "floco" => 1.5f, "flor" => 1.1f,
            "relogio" => 0.85f, _ => 1f,
        };
        foreach (var no in modelo.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            if (no is not MeshInstance3D mi || mi.Mesh is null) continue;
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                // a sombra de oclusão vem na cor dos vértices: liga o "cor do vértice multiplica a cor do material"
                var temCor = mi.Mesh is ArrayMesh am && (am.SurfaceGetFormat(s) & Mesh.ArrayFormat.FormatColor) != 0;
                if (temCor && mi.Mesh.SurfaceGetMaterial(s) is BaseMaterial3D bm) bm.VertexColorUseAsAlbedo = true;
            }
        }
        // o que brilha ilumina um pouco o pano em volta (como no enfeite antigo)
        Color? luz = tipo switch
        {
            "gema" => new Color(0.7f, 0.3f, 1f),
            "gema_azul" => new Color(0.3f, 0.95f, 1f),
            "caveira_roxa" => new Color(0.7f, 0.35f, 1f),
            "sol" => new Color(1f, 0.85f, 0.4f),
            "dragao" => new Color(1f, 0.35f, 0.1f),
            _ => null,
        };
        if (luz is { } c) n.AddChild(new OmniLight3D { LightColor = c, LightEnergy = 0.3f, OmniRange = 0.9f, ShadowEnabled = false });
        return n;
    }
}

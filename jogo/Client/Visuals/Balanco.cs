using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 12/11 noite ("capa, véu e asa para mexer"): troca o material das peças que têm pano/asa solto (cor de vértice com alfa)
/// pelo shader pano_balanco, mantendo as texturas da peça. Cada frame o MannequinVisual passa a velocidade e a curva.
/// Peça sem cor de vértice (placa, elmo, bota) continua com o material de sempre.
/// </summary>
public static class Balanco
{
    private static Shader? _shader;

    /// <summary>Chamar logo depois de pôr a peça no esqueleto (fim do SetArmorPart). Devolve true se a peça tem pano.</summary>
    public static bool Preparar(MeshInstance3D mi, float fase)
    {
        if (mi.Mesh is not ArrayMesh mesh || !TemPano(mesh)) return false;
        _shader ??= GD.Load<Shader>("res://Assets/Shaders/pano_balanco.gdshader");
        if (_shader is null) return false;
        for (var s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            var src = mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s);
            var m = new ShaderMaterial { Shader = _shader };
            if (src is BaseMaterial3D b)
            {
                m.SetShaderParameter("tex_cor", b.AlbedoTexture);
                m.SetShaderParameter("cor_base", b.AlbedoColor);
                if (b.RoughnessTexture is { } orm) { m.SetShaderParameter("tex_orm", orm); m.SetShaderParameter("tem_orm", true); }
                if (b.NormalTexture is { } nm) { m.SetShaderParameter("tex_normal", nm); m.SetShaderParameter("tem_normal", true); }
                if (b.EmissionTexture is { } em) { m.SetShaderParameter("tex_emissao", em); m.SetShaderParameter("tem_emissao", true); m.SetShaderParameter("energia_emissao", b.EmissionEnergyMultiplier); }
            }
            m.SetShaderParameter("fase", fase);
            mi.SetSurfaceOverrideMaterial(s, m);   // o shader já desenha os dois lados (sem o NextPass do lado de dentro)
        }
        return true;
    }

    /// <summary>Velocidade (m/s) e curva (rad/s) do personagem para todas as peças com pano.</summary>
    public static void Atualizar(IEnumerable<MeshInstance3D> pecas, float velocidade, float giro)
    {
        foreach (var mi in pecas)
        {
            if (!GodotObject.IsInstanceValid(mi) || mi.Mesh is null) continue;
            for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                if (mi.GetSurfaceOverrideMaterial(s) is ShaderMaterial sm && sm.Shader == _shader)
                {
                    sm.SetShaderParameter("velocidade", velocidade);
                    sm.SetShaderParameter("giro", giro);
                }
        }
    }

    private static bool TemPano(ArrayMesh mesh)
    {
        for (var s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            var arr = mesh.SurfaceGetArrays(s);
            if (arr[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.Nil) continue;
            var cores = arr[(int)Mesh.ArrayType.Color].AsColorArray();
            for (var i = 0; i < cores.Length; i += 7)
                if (cores[i].A > 0.03f) return true;
        }
        return false;
    }
}

using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Carrega um .glb de Client/Assets/Models uma vez (em tempo de execução, sem depender do editor ter importado) e
/// devolve cópias. Se o arquivo faltar ou falhar, devolve null e quem chamou usa o boneco de formas simples.
/// </summary>
public static class GlbCache
{
    private static readonly Dictionary<string, Node3D?> Templates = new();

    public static Node3D? Instance(string name)
    {
        if (!Templates.TryGetValue(name, out var template))
        {
            template = Load("res://Assets/Models/" + name + ".glb");
            Templates[name] = template;
        }
        return template?.Duplicate() as Node3D;
    }

    private static Node3D? Load(string path)
    {
        if (!Godot.FileAccess.FileExists(path)) { GD.PrintErr($"modelo ausente: {path}"); return null; }
        var doc = new GltfDocument();
        // textura embutida sem recompressão (o padrão em tempo de execução pode recomprimir em Basis: lento e pesado);
        // o Shrink abaixo reduz e gera mipmaps
        var state = new GltfState { HandleBinaryImage = 3 /* EMBED_AS_UNCOMPRESSED */ };
        var err = doc.AppendFromFile(path, state);
        if (err != Error.Ok) { GD.PrintErr($"glb {path}: {err}"); return null; }
        var scene = doc.GenerateScene(state) as Node3D;
        if (scene is not null) Shrink(scene);
        return scene;
    }

    private static readonly Dictionary<ulong, Texture2D> Shrunk = new();

    /// <summary>
    /// Texturas do .glb carregado em tempo de execução vêm sem mipmap e no tamanho cheio (os prédios do lote 2 têm nove de
    /// 2048 px: ~200 MB cada casarão). Aqui: limita a 1024 px (2048 no gráfico Alto) e gera mipmaps — 4× menos memória e
    /// sem "chuvisco" de longe. Feito uma vez por textura (as cópias reaproveitam).
    /// </summary>
    private static void Shrink(Node n)
    {
        var max = GraphicsQuality.Current == GraphicsQuality.Level.Alto ? 2048 : GraphicsQuality.Current == GraphicsQuality.Level.Baixo ? 512 : 1024;
        // PC fraco: CDM_TEXTURA_MAX=256 (ou 512) limita ainda mais
        if (int.TryParse(System.Environment.GetEnvironmentVariable("CDM_TEXTURA_MAX"), out var forced) && forced >= 64) max = Mathf.Min(max, forced);
        if (n is MeshInstance3D lm && lm.Skin is null && lm.Mesh is ArrayMesh am && ComLods(am) is { } lods) lm.Mesh = lods;
        if (n is MeshInstance3D mi && mi.Mesh is { } mesh)
            for (var i = 0; i < mesh.GetSurfaceCount(); i++)
                if (mesh.SurfaceGetMaterial(i) is BaseMaterial3D m)
                    foreach (var slot in new[] { BaseMaterial3D.TextureParam.Albedo, BaseMaterial3D.TextureParam.Normal, BaseMaterial3D.TextureParam.Orm,
                                                 BaseMaterial3D.TextureParam.Roughness, BaseMaterial3D.TextureParam.Metallic, BaseMaterial3D.TextureParam.Emission,
                                                 BaseMaterial3D.TextureParam.AmbientOcclusion })
                        if (m.GetTexture(slot) is Texture2D t) m.SetTexture(slot, Smaller(t, max));
        foreach (var c in n.GetChildren()) Shrink(c);
    }

    /// <summary>
    /// 13/11 (avaliação AAA: o Porto desenhava 7,5 milhões de triângulos por quadro): modelo parado com muitos triângulos
    /// ganha níveis de detalhe (LOD) — de longe o Godot desenha a versão simplificada sozinho, e de perto nada muda.
    /// Feito uma vez por modelo, ao carregar. Bichos com esqueleto (pele) e com formas de mistura ficam como estão.
    /// </summary>
    private static ArrayMesh? ComLods(ArrayMesh m)
    {
        try
        {
            if (m.GetBlendShapeCount() > 0) return null;
            long tris = 0;
            for (var i = 0; i < m.GetSurfaceCount(); i++)
            {
                if ((m.SurfaceGetFormat(i) & Mesh.ArrayFormat.FormatBones) != 0) return null;
                var idx = m.SurfaceGetArrayIndexLen(i);
                tris += (idx > 0 ? idx : m.SurfaceGetArrayLen(i)) / 3;
            }
            if (tris < 4000) return null;
            var im = ImporterMesh.FromMesh(m);
            im.GenerateLods(25f, 60f, new Godot.Collections.Array());
            var r = im.GetMesh(null);
            return r is { } && r.GetSurfaceCount() == m.GetSurfaceCount() ? r : null;
        }
        catch (System.Exception e) { GD.PrintErr($"lod: {e.Message}"); return null; }
    }

    private static Texture2D Smaller(Texture2D t, int max)
    {
        if (Shrunk.TryGetValue(t.GetInstanceId(), out var done)) return done;
        Texture2D result = t;
        try
        {
            if (t.GetImage() is { } img && !img.IsCompressed())
            {
                var w = img.GetWidth(); var h = img.GetHeight();
                if (w > max || h > max)
                {
                    var k = (float)max / Mathf.Max(w, h);
                    img.Resize(Mathf.Max(1, (int)(w * k)), Mathf.Max(1, (int)(h * k)), Image.Interpolation.Lanczos);
                }
                if (!img.HasMipmaps()) img.GenerateMipmaps();
                result = ImageTexture.CreateFromImage(img);
            }
        }
        catch (System.Exception e) { GD.PrintErr($"textura do glb: {e.Message}"); }
        Shrunk[t.GetInstanceId()] = result;
        return result;
    }

    public static T? Find<T>(Node n) where T : Node
    {
        if (n is T t) return t;
        foreach (var c in n.GetChildren())
            if (Find<T>(c) is { } f) return f;
        return null;
    }
}

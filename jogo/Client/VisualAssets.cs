using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Carrega imagens de res://Assets/Art direto do arquivo (não depende de o editor ter importado o recurso).
/// Se um arquivo faltar ou falhar, devolve null e o cliente usa formas simples no lugar (nada quebra).
/// </summary>
public static class VisualAssets
{
    private static readonly Dictionary<string, ImageTexture?> Cache = new();

    /// <summary>Mapa de normais "x_n.png" de uma textura "x.png" (relevo sob luz rasante); null se não houver.</summary>
    public static ImageTexture? Normal(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        var name = (dot < 0 ? fileName : fileName[..dot]) + "_n.png";
        if (Cache.TryGetValue(name, out var cached)) return cached;
        if (!Godot.FileAccess.FileExists("res://Assets/Art/" + name)) { Cache[name] = null; return null; }
        return Load(name);
    }

    /// <summary>Liga o relevo (mapa de normais) num material que já usa a textura.</summary>
    public static void AddRelief(StandardMaterial3D m, string fileName, float scale = 1f)
    {
        if (Normal(fileName) is not { } n) return;
        m.NormalEnabled = true;
        m.NormalTexture = n;
        m.NormalScale = scale;
    }

    public static ImageTexture? Load(string fileName)
    {
        if (Cache.TryGetValue(fileName, out var cached)) return cached;

        ImageTexture? texture = null;
        try
        {
            var image = Image.LoadFromFile("res://Assets/Art/" + fileName);
            if (image is not null && !image.IsEmpty())
            {
                image.GenerateMipmaps();
                texture = ImageTexture.CreateFromImage(image);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Falha ao carregar {fileName}: {e.Message}");
        }

        if (texture is null) GD.PrintErr($"Ativo ausente ou ilegível (usando forma simples): {fileName}");
        Cache[fileName] = texture;
        return texture;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// 13/11 ("tira aquela imagem do carregamento; usa imagens mais imersivas do jogo — prints do game, sem textos"):
/// a tela de carregamento mostra prints do próprio jogo. O cliente tira uma foto só do mundo 3D (sem interface, sem nomes)
/// de vez em quando enquanto você joga — câmera própria num SubViewport que olha o mesmo mundo — e guarda as 16 últimas
/// em user://cargas/. Junto vão as fotos que vêm com o jogo (res://Assets/Art/cargas/*.jpg).
/// </summary>
public static class FotosDeCarga
{
    private const string Pasta = "user://cargas";
    private const string Padrao = "res://Assets/Art/cargas";
    private const int Maximo = 16;
    private static readonly Random Rng = new();

    /// <summary>Uma foto ao acaso (ou nulo se não houver nenhuma).</summary>
    public static Texture2D? Sortear()
    {
        var todas = Lista();
        // 13/11 ("deixou preto na parte de carregar mundo; usa o mapa do jogo por enquanto"): sem nenhuma foto ainda, a arte do mapa
        if (todas.Count == 0) return ResourceLoader.Exists("res://Assets/Art/mapa_mundo.png") ? GD.Load<Texture2D>("res://Assets/Art/mapa_mundo.png") : null;
        // não repete a última
        var arq = todas[Rng.Next(todas.Count)];
        if (todas.Count > 1 && arq == _ultima) arq = todas[(todas.IndexOf(arq) + 1) % todas.Count];
        _ultima = arq;
        try
        {
            var img = Image.LoadFromFile(arq);
            return img is null ? null : ImageTexture.CreateFromImage(img);
        }
        catch { return null; }
    }

    private static List<string> Lista()
    {
        var l = new List<string>();
        foreach (var pasta in new[] { Pasta, Padrao })
        {
            using var d = DirAccess.Open(pasta);
            if (d is null) continue;
            foreach (var f in d.GetFiles())
                if (f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    l.Add(pasta + "/" + f);
        }
        return l;
    }

    private static bool _ocupado;
    private static string? _ultima;

    /// <summary>
    /// 13/11 ("tira print de um ângulo maneiro da cidade, de cima, ou da biblioteca do Merlin, e fica trocando as imagens —
    /// pra dar vontade de explorar"): cartão-postal — foto aérea de um ponto bonito (alvo no chão, câmera alta e afastada,
    /// olhando para baixo). Cada ponto tem um arquivo fixo (postal_&lt;nome&gt;.jpg), refeito de tempos em tempos.
    /// </summary>
    public static void Postal(Node host, Camera3D cam, string nome, Vector3 alvo, float yawGraus, float distancia, float altura)
    {
        var a = Mathf.DegToRad(yawGraus);
        var pos = alvo + new Vector3(Mathf.Sin(a) * distancia, altura, Mathf.Cos(a) * distancia);
        var t = new Transform3D(Basis.Identity, pos).LookingAt(alvo + new Vector3(0, 1.5f, 0), Vector3.Up);
        Tirar(host, cam, t, "postal_" + nome);
    }

    /// <summary>Já tem o postal (e é recente: menos de 3 dias)?</summary>
    public static bool TemPostal(string nome)
    {
        var f = Pasta + "/postal_" + nome + ".jpg";
        if (!Godot.FileAccess.FileExists(f)) return false;
        var quando = Godot.FileAccess.GetModifiedTime(f);
        return (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - quando < 3 * 86400;
    }

    /// <summary>Tira a foto do que a câmera vê agora (só o 3D). Assíncrono: pronto em 2 quadros.</summary>
    public static void Tirar(Node host, Camera3D cam) => Tirar(host, cam, null, null);

    private static async void Tirar(Node host, Camera3D cam, Transform3D? pose, string? nome)
    {
        if (_ocupado || !GodotObject.IsInstanceValid(cam) || cam.GetWorld3D() is not { } mundo) return;
        _ocupado = true;
        try
        {
            // nomes, números de dano e placas (Label3D/Sprite3D) vão para a camada 20, que a câmera da foto não vê
            foreach (var n3 in host.GetTree().Root.FindChildren("*", "Label3D", true, false)) if (n3 is VisualInstance3D vi) vi.Layers = 1u << 19;
            foreach (var n3 in host.GetTree().Root.FindChildren("*", "Sprite3D", true, false)) if (n3 is VisualInstance3D vi) vi.Layers = 1u << 19;
            var vp = new SubViewport
            {
                Size = new Vector2I(1280, 720), World3D = mundo, RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                Msaa3D = Viewport.Msaa.Msaa2X, TransparentBg = false,
            };
            var c = new Camera3D { Fov = cam.Fov, Near = cam.Near, Far = cam.Far, Environment = cam.Environment, Attributes = cam.Attributes, CullMask = cam.CullMask & ~(1u << 19) };
            vp.AddChild(c);
            host.AddChild(vp);
            // um pouco mais alto e atrás que a câmera do jogo: vista de "cartão-postal"
            c.GlobalTransform = pose ?? cam.GlobalTransform.TranslatedLocal(new Vector3(0, 0.6f, 1.2f));
            if (pose is not null) c.Fov = 55f;
            c.Current = true;
            for (var i = 0; i < 3; i++) await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            var img = vp.GetTexture().GetImage();
            vp.QueueFree();
            if (img is null || Vazia(img)) return;
            DirAccess.MakeDirRecursiveAbsolute(Pasta);
            if (nome is not null) { img.SaveJpg($"{Pasta}/{nome}.jpg", 0.88f); return; }
            var n = 0;
            try { using var f = Godot.FileAccess.Open(Pasta + "/proxima.txt", Godot.FileAccess.ModeFlags.Read); n = int.Parse(f?.GetAsText().Trim() ?? "0"); } catch { n = 0; }
            img.SaveJpg($"{Pasta}/foto_{n % Maximo:00}.jpg", 0.86f);
            try { using var f = Godot.FileAccess.Open(Pasta + "/proxima.txt", Godot.FileAccess.ModeFlags.Write); f?.StoreString(((n + 1) % Maximo).ToString()); } catch { }
        }
        catch (Exception e) { GD.Print("foto de carregamento: " + e.Message); }
        finally { _ocupado = false; }
    }

    /// <summary>Foto quase toda preta ou de uma cor só (câmera dentro de parede, tela de morte) não serve.</summary>
    private static bool Vazia(Image img)
    {
        float soma = 0, soma2 = 0; var n = 0;
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var c = img.GetPixel((x * 2 + 1) * img.GetWidth() / 16, (y * 2 + 1) * img.GetHeight() / 16);
                var l = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
                soma += l; soma2 += l * l; n++;
            }
        var media = soma / n; var desvio = Mathf.Sqrt(Mathf.Max(0, soma2 / n - media * media));
        return media < 0.08f || desvio < 0.035f;
    }
}

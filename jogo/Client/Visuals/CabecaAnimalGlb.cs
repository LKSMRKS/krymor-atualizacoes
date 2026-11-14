using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Cabeças de bicho novas (recortadas dos modelos Meshy enviados pelo dono): cabeca_leao (Guerreiro), cabeca_corvo (Mago),
/// cabeca_cobra (Arqueiro), cabeca_gata (Bruxa). Origem = osso Head do manequim, Y para cima, frente +Z,
/// tamanho já na proporção do corpo do manequim (1,674 m). Vai direto no socket "Head", sem deslocamento nem escala.
/// Uso em MannequinVisual.SetEvolved (no lugar de Lion/Crow/Snake/Cat):
///   if (CabecaAnimalGlb.Anexar(_headSocket, _classId) is { } cab) _animal = cab; else { ...código antigo... }
/// Para os personagens padrão da criação já nascerem com a cabeça do bicho: chamar SetEvolved(true) ao montar o boneco.
/// </summary>
public static class CabecaAnimalGlb
{
    /// <summary>Ajuste fino, se precisar subir/descer a cabeça no pescoço (metros, espaço do socket).</summary>
    public static Vector3 Ajuste = Vector3.Zero;

    public static string Arquivo(string classId) => classId switch
    {
        "guerreiro" => "cabeca_leao",
        "mago" => "cabeca_corvo",
        "arqueiro" => "cabeca_cobra",
        _ => "cabeca_gata",
    };

    /// <summary>Instancia a cabeça da classe e prende no socket; null se o .glb faltar (aí o chamador usa a cabeça de formas simples).</summary>
    public static Node3D? Anexar(Node3D headSocket, string classId)
    {
        if (GlbCache.Instance(Arquivo(classId)) is not { } g) return null;
        var raiz = new Node3D { Name = "CabecaAnimal", Position = Ajuste };
        raiz.AddChild(g);
        headSocket.AddChild(raiz);
        return raiz;
    }
}

using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Mar de verdade na borda de um mapa (Cal lote 2): faixa de areia molhada, arrebentação com espuma e mar com ondas até o
/// horizonte, num único plano com o shader res://Assets/Shaders/mar_costa.gdshader. Só visual: a colisão continua sendo a do
/// servidor (o "penhasco" daquele lado). Sem o shader, não faz nada.
/// Uso (MapView, depois do chão): MarDaCosta.Construir(Root, (float)Map.HalfSize, "sul");
/// lado: "norte" (+Z do jogo), "sul" (−Z), "leste" (+X), "oeste" (−X).
/// </summary>
public static class MarDaCosta
{
    /// <summary>Lado onde o mar foi posto neste mapa (o MapView pode pular o desenho do penhasco desse lado).</summary>
    public static string? Lado { get; private set; }

    public static Node3D? Construir(Node3D raiz, float metade, string lado, float recuo = 2f, float largura = 900f, float alcance = 700f, float faixa = 16f)
    {
        const string arquivo = "res://Assets/Shaders/mar_costa.gdshader";
        if (!ResourceLoader.Exists(arquivo)) return null;
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>(arquivo) };
        if (VisualAssets.Load("ter_areia_molhada.png") is { } areia) mat.SetShaderParameter("areia_molhada", areia);
        if (VisualAssets.Normal("ter_areia_molhada.png") is { } areiaN) mat.SetShaderParameter("areia_n", areiaN);
        if (VisualAssets.Load("fx_agua_n.png") is { } aguaN) mat.SetShaderParameter("agua_n", aguaN);
        if (VisualAssets.Load("fx_espuma.png") is { } esp) mat.SetShaderParameter("espuma", esp);

        // plano: X = ao longo da praia, Z local = da areia (−14 m) para o mar (+alcance). Subdividido perto da praia para as ondas.
        var malha = new PlaneMesh
        {
            Size = new Vector2(largura, alcance + faixa), SubdivideWidth = 180, SubdivideDepth = 160, Orientation = PlaneMesh.OrientationEnum.Y,
            // desloca os vertices para a linha d'agua cair em z local = 0 (o shader le VERTEX.z como distancia da praia)
            CenterOffset = new Vector3(0, 0, (alcance + faixa) / 2f - faixa),
        };
        var inst = new MeshInstance3D { Name = "Mar", Mesh = malha, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        var corpo = new Node3D { Name = "MarDaCosta" };
        corpo.AddChild(inst);
        // a "linha d'agua" fica 'recuo' metros para dentro do mapa a partir da borda
        var borda = metade - recuo;
        (corpo.Position, corpo.RotationDegrees) = lado switch
        {
            "norte" => (new Vector3(0, 0.03f, borda), new Vector3(0, 0, 0)),
            "sul" => (new Vector3(0, 0.03f, -borda), new Vector3(0, 180, 0)),
            "leste" => (new Vector3(borda, 0.03f, 0), new Vector3(0, 90, 0)),
            _ => (new Vector3(-borda, 0.03f, 0), new Vector3(0, -90, 0)),
        };
        raiz.AddChild(corpo);
        Lado = lado;
        return corpo;
    }
}

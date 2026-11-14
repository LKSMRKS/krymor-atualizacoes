using Godot;

namespace CinzasDaMare.Client;

/// <summary>
/// Partículas do ar em volta do jogador (pedido: "folhas, pedrinhas, poeira no ar"): uma caixa de ~36 m que segue o
/// personagem e solta o que combina com o lugar — folhas na mata/vale, grãos de areia na praia/cânion, neve na geada,
/// brasas na escória, esporos no brejo, cinza/poeira na cidade e nas ruínas. Só visual; some na qualidade baixa (F10).
/// </summary>
public sealed partial class AmbientParticles : Node3D
{
    private readonly CpuParticles3D _main = new();
    private readonly CpuParticles3D _motes = new();
    private string _theme = "";

    public AmbientParticles()
    {
        Name = "ParticulasDoAr";
        AddChild(_main);
        AddChild(_motes);
        foreach (var p in new[] { _main, _motes })
        {
            p.EmissionShape = CpuParticles3D.EmissionShapeEnum.Box;
            p.LocalCoords = false;    // a partícula fica no mundo; só a fonte acompanha o jogador
            p.Emitting = false;
        }
        // poeirinha brilhante parada no ar (em todo lugar, bem fraca): dá profundidade à luz
        _motes.Mesh = Quad(0.03f, Unlit(new Color(1f, 0.95f, 0.8f, 0.22f), additive: true));
        _motes.Amount = 22;
        _motes.Lifetime = 7f;
        _motes.EmissionBoxExtents = new Vector3(14, 3, 14);
        _motes.Position = new Vector3(0, 2.5f, 0);
        _motes.Gravity = new Vector3(0, 0.02f, 0);
        _motes.InitialVelocityMin = 0.02f; _motes.InitialVelocityMax = 0.12f;
        _motes.Spread = 180f;
    }

    public void SetTheme(string theme, bool enabled)
    {
        _theme = theme;
        _main.Emitting = false;
        _motes.Emitting = enabled && theme is not "masmorra" and not "cripta";
        if (!enabled) return;
        _main.Position = new Vector3(0, 7f, 0);
        _main.EmissionBoxExtents = new Vector3(18, 4, 18);
        _main.Direction = new Vector3(1f, -0.4f, 0.3f);
        _main.Spread = 35f;
        _main.AngularVelocityMin = -180f; _main.AngularVelocityMax = 180f;
        _main.AngleMin = 0; _main.AngleMax = 360;
        _main.DampingMin = 0.2f; _main.DampingMax = 0.6f;
        switch (theme)
        {
            case "mata" or "vale" or "fantasma":   // folhas secas caindo e rodopiando
                Set(Quad(0.12f, Leaf(theme == "fantasma" ? new Color(0.45f, 0.38f, 0.30f) : new Color(0.55f, 0.42f, 0.18f))), 16, 9f,
                    new Vector3(0.25f, -0.55f, 0), 0.4f, 1.0f);
                break;
            case "praia" or "canion" or "acropole":   // grãos de areia e pedrinhas levados pelo vento, baixos
                Set(Quad(0.025f, Unlit(new Color(0.92f, 0.84f, 0.66f, 0.3f), false)), 40, 3.5f, new Vector3(0, -0.2f, 0), 2.5f, 5f);
                _main.Position = new Vector3(0, 0.8f, 0);
                _main.EmissionBoxExtents = new Vector3(18, 0.8f, 18);
                _main.Direction = new Vector3(1f, 0.05f, 0.25f);
                _main.Spread = 12f;
                break;
            case "neve":
                Set(Quad(0.05f, Unlit(new Color(1f, 1f, 1f, 0.75f), false)), 150, 8f, new Vector3(0.15f, -1.1f, 0.05f), 0.2f, 0.6f);
                _main.Direction = new Vector3(0.3f, -1f, 0.1f);
                break;
            case "submerso":   // 12/11: bolhas subindo (Fundo do Triângulo)
                Set(Quad(0.06f, Unlit(new Color(0.75f, 0.95f, 1f, 0.55f), true)), 70, 6f, new Vector3(0, 0.9f, 0), 0.4f, 1.2f);
                _main.Position = new Vector3(0, 0.3f, 0);
                _main.EmissionBoxExtents = new Vector3(18, 0.4f, 18);
                _main.Direction = new Vector3(0, 1, 0);
                _main.Spread = 12f;
                break;
            case "lava":   // brasas subindo
                Set(Quad(0.045f, Unlit(new Color(1f, 0.55f, 0.15f, 0.8f), true)), 45, 4.5f, new Vector3(0, 0.55f, 0), 0.3f, 0.9f);
                _main.Position = new Vector3(0, 0.5f, 0);
                _main.EmissionBoxExtents = new Vector3(18, 0.5f, 18);
                _main.Direction = new Vector3(0, 1, 0);
                break;
            case "brejo":   // esporos verdes boiando
                Set(Quad(0.04f, Unlit(new Color(0.7f, 0.95f, 0.45f, 0.4f), true)), 30, 8f, new Vector3(0, 0.04f, 0), 0.05f, 0.25f);
                _main.Position = new Vector3(0, 1.8f, 0);
                _main.EmissionBoxExtents = new Vector3(16, 1.6f, 16);
                _main.Spread = 180f;
                break;
            case "cidade" or "masmorra" or "cripta":   // cinza fina caindo devagar
                Set(Quad(0.035f, Unlit(new Color(0.75f, 0.72f, 0.68f, 0.3f), false)), 25, 9f, new Vector3(0.05f, -0.18f, 0), 0.05f, 0.2f);
                break;
            default: return;
        }
        _main.Emitting = true;
    }

    private void Set(Mesh mesh, int amount, float life, Vector3 gravity, float vmin, float vmax)
    {
        _main.Mesh = mesh;
        _main.Amount = amount;
        _main.Lifetime = life;
        _main.Gravity = gravity;
        _main.InitialVelocityMin = vmin;
        _main.InitialVelocityMax = vmax;
    }

    /// <summary>Segue o jogador (chame a cada quadro).</summary>
    public void Follow(Vector3 player) => GlobalPosition = player;

    private static QuadMesh Quad(float size, Material mat) => new() { Size = new Vector2(size, size), Material = mat };

    private static StandardMaterial3D Unlit(Color c, bool additive) => new()
    {
        AlbedoColor = c, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    /// <summary>Folha: quadrado com sombra (recebe luz) e as duas faces visíveis.</summary>
    private static StandardMaterial3D Leaf(Color c) => new()
    {
        AlbedoColor = c, Roughness = 0.9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled,
    };
}

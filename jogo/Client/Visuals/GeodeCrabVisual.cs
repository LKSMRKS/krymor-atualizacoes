using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 10/11 item 47: Caranguejo-Geodo. Usa o corpo do Caranguejo de Pedra (tingido de cinza-arroxeado) e põe por cima geodos
/// roxos nas costas que piscam devagar, mais o "buraco" (ponto fraco) brilhando atrás. Dormindo vira pedra: fica encolhido
/// e os geodos quase apagam. Ao preparar a chuva de geodos eles brilham forte.
/// </summary>
public sealed class GeodeCrabVisual : IEntityVisual
{
    private readonly IEntityVisual _inner;
    private readonly StandardMaterial3D _glow;
    private readonly StandardMaterial3D _hole;
    private float _t;

    public GeodeCrabVisual(IEntityVisual inner)
    {
        _inner = inner;
        var h = inner.Height;
        _glow = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.25f, 0.85f), EmissionEnabled = true, Emission = new Color(0.7f, 0.3f, 1f),
            EmissionEnergyMultiplier = 1.5f, Roughness = 0.15f, Metallic = 0.2f,
        };
        _hole = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.08f, 0.02f, 0.12f), EmissionEnabled = true, Emission = new Color(1f, 0.55f, 0.95f),
            EmissionEnergyMultiplier = 0.8f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var back = new Node3D { Name = "Geodos", Position = new Vector3(0, h * 0.55f, -h * 0.12f) };
        Root.AddChild(back);
        var rng = new RandomNumberGenerator { Seed = 4711 };
        for (var i = 0; i < 9; i++)
        {
            var size = rng.RandfRange(0.10f, 0.22f) * h;
            var crystal = new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(size * 0.5f, size, size * 0.5f) },
                MaterialOverride = _glow,
                Position = new Vector3(rng.RandfRange(-0.32f, 0.32f) * h, rng.RandfRange(0f, 0.08f) * h, rng.RandfRange(-0.05f, 0.25f) * h),
                RotationDegrees = new Vector3(rng.RandfRange(-35, 35), rng.RandfRange(0, 360), rng.RandfRange(-35, 35)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            back.AddChild(crystal);
        }
        // o buraco das costas: anel escuro com brilho rosado, virado para trás (−Z)
        var hole = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.06f * h, OuterRadius = 0.13f * h },
            MaterialOverride = _hole,
            Position = new Vector3(0, h * 0.42f, -h * 0.38f),
            RotationDegrees = new Vector3(90, 0, 0),
        };
        Root.AddChild(hole);
        var light = new OmniLight3D { LightColor = new Color(0.7f, 0.35f, 1f), LightEnergy = 0.6f, OmniRange = 2.5f * h / 1.3f, Position = new Vector3(0, h * 0.9f, -h * 0.1f) };
        back.AddChild(light);
    }

    public Node3D Root => _inner.Root;
    public float Height => _inner.Height;

    public void Update(in VisualFrame frame, float delta)
    {
        _t += delta;
        var f = frame;
        var stone = f.Action == "sleep" || f.Action.StartsWith("windup:casca") || f.Action.StartsWith("active:casca");
        // dormindo: fica encolhido na pose da carapaça fechada
        if (f.Action == "sleep") { f.Action = "windup:casca_de_pedra"; f.ActionT = 1f; }
        var charging = f.Action.StartsWith("windup:chuva");
        var blink = 0.5f + 0.5f * Mathf.Sin(_t * (charging ? 9f : 1.6f));
        _glow.EmissionEnergyMultiplier = stone ? 0.15f + 0.15f * blink : charging ? 1.5f + 2.5f * blink : 0.6f + 1.2f * blink;
        _hole.EmissionEnergyMultiplier = f.Action.StartsWith("recover:") ? 2.2f : 0.7f + 0.4f * blink;
        _inner.Update(in f, delta);
    }

    public void SetGear(string? weapon, string? offhand, string? body) => _inner.SetGear(weapon, offhand, body);
    public void Hit(Vector3 fromDirection) => _inner.Hit(fromDirection);
    public void Dispose() => _inner.Dispose();
}

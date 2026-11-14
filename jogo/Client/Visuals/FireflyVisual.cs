using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Pip, o vagalume-guia, como companheiro de verdade (slot de companheiro): voa em volta da altura do ombro do dono,
/// pisca e ilumina um pouco em volta. O servidor só manda onde ele está (atrás do dono); o voo é só desenho.
/// </summary>
public sealed class FireflyVisual : IEntityVisual
{
    public Node3D Root { get; } = new() { Name = "Pip" };
    public float Height => 2.2f;
    private readonly Node3D _body = new();
    private readonly OmniLight3D _light;
    private readonly StandardMaterial3D _glow;
    private float _t;

    public FireflyVisual()
    {
        Root.AddChild(_body);
        _glow = VisualKit.Glow(new Color(0.75f, 1f, 0.55f), 5f);
        VisualKit.Part(_body, VisualKit.Sphere(0.06f), _glow, Vector3.Zero);
        VisualKit.Part(_body, VisualKit.Sphere(0.045f), VisualKit.Mat(new Color(0.18f, 0.16f, 0.12f), 0.8f), new Vector3(0, 0.02f, 0.07f));
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(_body, VisualKit.Sphere(0.05f), VisualKit.Unshaded(new Color(0.85f, 0.95f, 1f, 0.35f), true),
                new Vector3(side * 0.06f, 0.04f, 0.03f), null, new Vector3(1f, 0.25f, 0.6f));
        _light = new OmniLight3D { LightColor = new Color(0.7f, 1f, 0.5f), LightEnergy = 0.7f, OmniRange = 2.4f };
        _body.AddChild(_light);
    }

    public void Update(in VisualFrame frame, float delta)
    {
        _t += delta;
        _body.Position = new Vector3(Mathf.Cos(_t * 1.3f) * 0.35f, 1.9f + 0.18f * Mathf.Sin(_t * 2.3f), Mathf.Sin(_t * 1.1f) * 0.3f);
        var blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_t * 1.7f));
        _light.LightEnergy = 0.4f + 0.5f * blink;
        _glow.EmissionEnergyMultiplier = 2f + 4f * blink;
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) { }
    public void Dispose() => Root.QueueFree();
}

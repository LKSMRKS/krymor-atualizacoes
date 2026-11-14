using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Monstro meleca (gosma): bolha translúcida que quica e se espalha. Preparação: encolhe e treme;
/// golpe: estica para a frente; recuperação: esparramada no chão. Por dentro, ossinhos e uma moeda engolidos.
/// </summary>
public sealed class SlimeVisual : IEntityVisual
{
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly Node3D _blob, _scaleRoot;
    private readonly MeshInstance3D _skin;
    private float _time, _hop, _hitT = 1f, _deathT, _fade;
    private bool _wasAlive = true;
    private Vector3 _squash = Vector3.One;

    public Node3D Root { get; } = new() { Name = "Meleca" };
    public float Height { get; }

    public SlimeVisual(float scale, Color color)
    {
        Height = 1.0f * scale;
        _scaleRoot = VisualKit.Joint(Root, Vector3.Zero);
        _scaleRoot.Scale = Vector3.One * scale;
        _blob = VisualKit.Joint(_scaleRoot, Vector3.Zero, "Bolha");
        var gel = _flash.Add(new StandardMaterial3D
        {
            AlbedoColor = new Color(color.R, color.G, color.B, 0.72f), Roughness = 0.12f, Metallic = 0.05f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, RimEnabled = true, Rim = 0.6f, RimTint = 0.3f,
            EmissionEnabled = true, Emission = color.Darkened(0.4f), EmissionEnergyMultiplier = 0.35f,
        });
        _skin = VisualKit.Part(_blob, VisualKit.Sphere(0.5f), gel, new Vector3(0, 0.42f, 0), null, new Vector3(1.1f, 0.85f, 1.1f));
        // coisas engolidas, visíveis através da gosma
        var bone = VisualKit.Mat(new Color(0.78f, 0.73f, 0.64f), 0.7f);
        VisualKit.Part(_blob, VisualKit.Capsule(0.035f, 0.26f), bone, new Vector3(0.12f, 0.3f, -0.05f), new Vector3(20, 40, 70));
        VisualKit.Part(_blob, VisualKit.Sphere(0.07f), bone, new Vector3(-0.15f, 0.36f, 0.08f));
        VisualKit.Part(_blob, VisualKit.Cylinder(0.06f, 0.06f, 0.015f, 10), VisualKit.Mat(new Color(0.9f, 0.7f, 0.25f), 0.3f, 0.8f),
            new Vector3(0.02f, 0.22f, 0.12f), new Vector3(60, 0, 20));
        // olhos boiando perto da superfície
        var eye = VisualKit.Mat(new Color(0.95f, 0.95f, 0.9f), 0.3f);
        var pupil = VisualKit.Mat(new Color(0.05f, 0.05f, 0.05f), 0.3f);
        foreach (var side in new[] { -1f, 1f })
        {
            VisualKit.Part(_blob, VisualKit.Sphere(0.085f), eye, new Vector3(side * 0.16f, 0.58f, 0.36f));
            VisualKit.Part(_blob, VisualKit.Sphere(0.04f), pupil, new Vector3(side * 0.16f, 0.58f, 0.43f));
        }
        // poça embaixo
        VisualKit.Part(_scaleRoot, VisualKit.Cylinder(0.62f, 0.62f, 0.02f, 20), _flash.Add(VisualKit.Mat(color.Darkened(0.35f), 0.15f)), new Vector3(0, 0.01f, 0));
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) => _hitT = 0f;

    public void Update(in VisualFrame f, float delta)
    {
        _time += delta;
        if (!f.Alive)
        {
            if (_wasAlive) { _deathT = 0f; _fade = 0f; }
            _wasAlive = false;
            _deathT = Mathf.Min(1f, _deathT + delta / 0.5f);
            _blob.Scale = new Vector3(1f + 0.8f * _deathT, Mathf.Max(0.08f, 1f - _deathT), 1f + 0.8f * _deathT);
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.2f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive) { _wasAlive = true; Root.Visible = true; }
        var colon = f.Action.IndexOf(':');
        var phase = colon < 0 ? f.Action : f.Action[..colon];
        var t = f.ActionT;
        // anda quicando
        _hop += f.Speed * delta * 2.2f;
        var hopY = f.Speed > 0.3f ? Mathf.Abs(Mathf.Sin(_hop * Mathf.Pi)) * 0.18f : 0f;
        var wob = 0.05f * Mathf.Sin(_time * 4f);
        var target = new Vector3(1f + wob, 1f - wob, 1f + wob);
        var z = 0f;
        switch (phase)
        {
            case "windup": target = new Vector3(1.15f, 0.7f, 1.15f) + Vector3.One * 0.04f * Mathf.Sin(_time * 40f) * t; break;
            case "active": target = new Vector3(0.8f, 1.15f, 1.5f); z = 0.35f; break;
            case "recover": target = new Vector3(1.35f, 0.55f + 0.4f * t, 1.35f); break;
        }
        if (_hitT < 1f) { _hitT = Mathf.Min(1f, _hitT + delta / 0.25f); target += new Vector3(0.2f, -0.2f, 0.2f) * (1f - _hitT); }
        _squash = VisualKit.Approach(_squash, target, phase == "active" ? 22f : 10f, delta);
        _blob.Scale = _squash;
        _blob.Position = new Vector3(0, hopY, VisualKit.Approach(_blob.Position.Z, z, 14f, delta));
        _flash.Apply(_hitT < 1f ? 1f - _hitT : 0f, f.WindupGlow, new Color(0.6f, 1f, 0.3f));
    }

    public void Dispose() => Root.QueueFree();
}

/// <summary>
/// Planta carnívora: caule grosso enraizado, folhas no chão e uma cabeça de mandíbulas com dentes.
/// Não anda (balança no lugar). Preparação: recua a cabeça e abre a boca; golpe: bote para a frente e fecha.
/// </summary>
public sealed class PlantVisual : IEntityVisual
{
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly List<Node3D> _stem = new();
    private readonly Node3D _head, _upper, _lower;
    private float _time, _hitT = 1f, _deathT, _fade, _lean, _open;
    private bool _wasAlive = true;

    public Node3D Root { get; } = new() { Name = "PlantaCarnivora" };
    public float Height { get; }

    public PlantVisual(float scale, Color leaf, Color mouth)
    {
        Height = 1.9f * scale;
        var root = VisualKit.Joint(Root, Vector3.Zero);
        root.Scale = Vector3.One * scale;
        var green = _flash.Add(VisualKit.Mat(leaf, 0.75f));
        var dark = _flash.Add(VisualKit.Mat(leaf.Darkened(0.35f), 0.8f));
        var red = _flash.Add(VisualKit.Mat(mouth, 0.5f));
        var tooth = VisualKit.Mat(new Color(0.92f, 0.88f, 0.76f), 0.4f);
        // folhas largas no chão
        for (var i = 0; i < 6; i++)
        {
            var a = i * Mathf.Tau / 6f;
            VisualKit.Part(root, VisualKit.Sphere(0.32f), i % 2 == 0 ? green : dark, new Vector3(Mathf.Sin(a) * 0.45f, 0.05f, Mathf.Cos(a) * 0.45f),
                new Vector3(0, Mathf.RadToDeg(a), 0), new Vector3(0.55f, 0.12f, 1.4f));
        }
        // caule em segmentos (curva para a frente)
        Node3D parent = root;
        var y = 0.1f;
        for (var i = 0; i < 5; i++)
        {
            var seg = VisualKit.Joint(parent, new Vector3(0, i == 0 ? y : 0.28f, 0));
            VisualKit.Part(seg, VisualKit.Cylinder(0.085f - i * 0.008f, 0.1f - i * 0.008f, 0.3f, 8), i % 2 == 0 ? green : dark, new Vector3(0, 0.14f, 0));
            if (i is 1 or 3)
                foreach (var side in new[] { -1f, 1f })
                    VisualKit.Part(seg, VisualKit.Sphere(0.16f), green, new Vector3(side * 0.16f, 0.12f, 0), new Vector3(0, 0, side * 35), new Vector3(1.2f, 0.12f, 0.5f));
            _stem.Add(seg);
            parent = seg;
        }
        _head = VisualKit.Joint(parent, new Vector3(0, 0.32f, 0.05f), "Cabeca");
        VisualKit.Part(_head, VisualKit.Sphere(0.24f), dark, new Vector3(0, 0, -0.08f), null, new Vector3(1f, 0.9f, 0.8f));
        _upper = VisualKit.Joint(_head, new Vector3(0, 0.02f, 0.02f));
        _lower = VisualKit.Joint(_head, new Vector3(0, -0.02f, 0.02f));
        foreach (var (jaw, s) in new[] { (_upper, 1f), (_lower, -1f) })
        {
            VisualKit.Part(jaw, VisualKit.Sphere(0.3f), green, new Vector3(0, s * 0.06f, 0.22f), null, new Vector3(1f, 0.32f, 1f));
            VisualKit.Part(jaw, VisualKit.Sphere(0.27f), red, new Vector3(0, s * 0.035f, 0.22f), null, new Vector3(0.92f, 0.22f, 0.92f));
            for (var k = 0; k < 7; k++)
            {
                var a = Mathf.Lerp(-1.25f, 1.25f, k / 6f);
                VisualKit.Part(jaw, VisualKit.Cylinder(0f, 0.025f, 0.09f, 4), tooth,
                    new Vector3(Mathf.Sin(a) * 0.27f, -s * 0.01f, 0.22f + Mathf.Cos(a) * 0.27f), new Vector3(s > 0 ? 180 : 0, 0, 0));
            }
        }
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) => _hitT = 0f;

    public void Update(in VisualFrame f, float delta)
    {
        _time += delta;
        if (!f.Alive)
        {
            if (_wasAlive) { _deathT = 0f; _fade = 0f; }
            _wasAlive = false;
            _deathT = Mathf.Min(1f, _deathT + delta / 0.9f);
            foreach (var s in _stem) s.Rotation = new Vector3(0.35f * _deathT, 0, 0.05f * _deathT);
            _upper.Rotation = new Vector3(-0.5f * _deathT, 0, 0);
            _lower.Rotation = new Vector3(0.5f * _deathT, 0, 0);
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.4f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive) { _wasAlive = true; Root.Visible = true; }
        var colon = f.Action.IndexOf(':');
        var phase = colon < 0 ? f.Action : f.Action[..colon];
        var t = f.ActionT;
        float lean = 0.05f * Mathf.Sin(_time * 1.3f), open = 0.12f + 0.06f * Mathf.Sin(_time * 2.1f);
        switch (phase)
        {
            case "windup": lean = -0.22f * t; open = 0.2f + 0.55f * t; break;
            case "active": lean = 0.38f; open = t < 0.4f ? 0.75f : 0.0f; break;
            case "recover": lean = 0.25f * (1f - t); open = 0.05f; break;
        }
        if (_hitT < 1f) { _hitT = Mathf.Min(1f, _hitT + delta / 0.25f); lean -= 0.15f * (1f - _hitT); }
        _lean = VisualKit.Approach(_lean, lean, phase == "active" ? 26f : 9f, delta);
        _open = VisualKit.Approach(_open, open, 20f, delta);
        for (var i = 0; i < _stem.Count; i++)
            _stem[i].Rotation = new Vector3(_lean * (0.5f + i * 0.12f), 0, 0.04f * Mathf.Sin(_time * 1.1f + i));
        _head.Rotation = new Vector3(-_lean * 0.8f, 0, 0);
        _upper.Rotation = new Vector3(-_open, 0, 0);
        _lower.Rotation = new Vector3(_open * 0.7f, 0, 0);
        _flash.Apply(_hitT < 1f ? 1f - _hitT : 0f, f.WindupGlow, new Color(1f, 0.3f, 0.2f));
    }

    public void Dispose() => Root.QueueFree();
}

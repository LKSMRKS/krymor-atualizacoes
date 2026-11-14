using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Larva de Crosta (pacote visual v1): corpo segmentado cor de osso, cabeça com mandíbulas bordô.
/// Preparação: contrai os segmentos e ergue a cabeça; golpe: estica e morde; recuperação: achatada (brecha).
/// </summary>
public sealed class LarvaVisual : IEntityVisual
{
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly List<Node3D> _segments = new();
    private readonly Node3D _head, _jawL, _jawR, _scaleRoot;
    private float _time, _phase, _hitT = 1f, _deathT, _fade, _stretch = 1f, _rear;
    private bool _wasAlive = true;
    private const int Count = 7;

    public Node3D Root { get; } = new() { Name = "Larva" };
    public float Height { get; }

    public LarvaVisual(float scale, Color? hideColor = null, Color? bandColor = null)
    {
        Height = 0.8f * scale;
        _scaleRoot = VisualKit.Joint(Root, Vector3.Zero);
        _scaleRoot.Scale = Vector3.One * scale;
        var hide = _flash.Add(VisualKit.Hide(hideColor ?? new Color(0.74f, 0.69f, 0.60f), "pele", 0.55f));
        var band = _flash.Add(VisualKit.Hide(bandColor ?? new Color(0.48f, 0.42f, 0.36f), "quitina", 0.7f));
        var jaw = _flash.Add(VisualKit.Mat(new Color(0.42f, 0.18f, 0.22f), 0.6f));
        for (var i = 0; i < Count; i++)
        {
            var r = 0.33f - i * 0.03f;
            var seg = VisualKit.Joint(_scaleRoot, new Vector3(0, r, -i * 0.27f));
            VisualKit.Part(seg, VisualKit.Sphere(r), hide, Vector3.Zero, null, new Vector3(1f, 0.85f, 0.75f));
            VisualKit.Part(seg, VisualKit.Cylinder(r * 1.02f, r * 1.02f, 0.05f, 12), band, new Vector3(0, 0, -r * 0.5f), new Vector3(90, 0, 0));
            // fileira de calos de crosta no dorso (prancha 04)
            for (var k = -1; k <= 1; k++)
                VisualKit.Part(seg, VisualKit.Sphere(0.04f), band, new Vector3(k * r * 0.45f, r * 0.80f - Mathf.Abs(k) * 0.05f, 0.02f));
            // perninhas curvas bordô por baixo (só nos segmentos da frente)
            if (i < 3)
                foreach (var side in new[] { -1f, 1f })
                    VisualKit.Part(seg, VisualKit.Cylinder(0.012f, 0.03f, 0.2f, 4), jaw, new Vector3(side * r * 0.7f, -r * 0.65f, 0.06f), new Vector3(-30, 0, side * 35));
            _segments.Add(seg);
        }
        _head = VisualKit.Joint(_segments[0], new Vector3(0, 0.02f, 0.26f));
        VisualKit.Part(_head, VisualKit.Sphere(0.2f), band, Vector3.Zero, null, new Vector3(1f, 0.85f, 0.9f));
        _jawL = VisualKit.Joint(_head, new Vector3(0.08f, -0.05f, 0.14f));
        _jawR = VisualKit.Joint(_head, new Vector3(-0.08f, -0.05f, 0.14f));
        foreach (var j in new[] { _jawL, _jawR })
        {
            VisualKit.Part(j, VisualKit.Cylinder(0f, 0.035f, 0.22f, 5), jaw, new Vector3(0, 0, 0.1f), new Vector3(90, 0, 0));
            VisualKit.Part(j, VisualKit.Cylinder(0f, 0.025f, 0.14f, 5), jaw, new Vector3(0, -0.05f, 0.08f), new Vector3(120, 0, 0));
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
            _deathT = Mathf.Min(1f, _deathT + delta / 0.6f);
            for (var i = 0; i < _segments.Count; i++)
                _segments[i].Rotation = new Vector3(0, 0, Mathf.Pi * 0.5f * Mathf.SmoothStep(0, 1, _deathT) * (i % 2 == 0 ? 1 : -1) * 0.3f);
            _scaleRoot.Rotation = new Vector3(0, 0, Mathf.Pi * 0.5f * Mathf.SmoothStep(0, 1, _deathT));
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.4f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive) { _wasAlive = true; Root.Visible = true; _scaleRoot.Rotation = Vector3.Zero; }

        _phase += (1.5f + f.Speed * 3f) * delta;
        var colon = f.Action.IndexOf(':');
        var phase = colon < 0 ? f.Action : f.Action[..colon];
        var t = f.ActionT;
        float stretch = 1f, rear = 0f, jaw = 0.15f + 0.1f * Mathf.Sin(_time * 4f);
        switch (phase)
        {
            case "windup": stretch = 1f - 0.35f * Mathf.SmoothStep(0, 1, t); rear = 0.6f * t; jaw = 0.7f * t; break;
            case "active": stretch = 1.35f; rear = 0.15f; jaw = t < 0.5f ? 0.7f : 0f; break;
            case "recover": stretch = 0.95f; rear = -0.25f * (1f - t); jaw = 0.05f; break;
        }
        _stretch = VisualKit.Approach(_stretch, stretch, phase == "active" ? 25f : 10f, delta);
        _rear = VisualKit.Approach(_rear, rear, 10f, delta);
        for (var i = 0; i < _segments.Count; i++)
        {
            var r = 0.33f - i * 0.03f;
            var wave = Mathf.Sin(_phase * 2f - i * 0.9f);
            _segments[i].Position = new Vector3(0.05f * wave, r + 0.04f * Mathf.Max(0, wave), -i * 0.27f * _stretch);
            _segments[i].Rotation = new Vector3(i == 0 ? -_rear : 0, 0, 0);
        }
        _jawL.Rotation = new Vector3(0, jaw, 0);
        _jawR.Rotation = new Vector3(0, -jaw, 0);
        if (_hitT < 1f) _hitT = Mathf.Min(1f, _hitT + delta / 0.25f);
        _flash.Apply(_hitT < 1f ? 1f - _hitT : 0f, f.WindupGlow, new Color(1f, 0.25f, 0.1f));
    }

    public void Dispose() => Root.QueueFree();
}

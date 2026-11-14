using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Carroça da escolta (missão "escort"): caixa de tábuas, duas rodas que giram com o andar, carga de caixotes e uma
/// lanterna acesa no varal (dá para achar de longe). A posição vem do servidor (QuestMarkerMessage); aqui só desliza.
/// </summary>
public sealed partial class CartVisual : Node3D
{
    private readonly Node3D[] _wheels = new Node3D[2];
    /// <summary>10/11 ("devia ter alguém puxando a carroça"): o carroceiro entre os varais, andando quando ela anda.</summary>
    private readonly IEntityVisual? _puller;
    private float _speed;
    private Vector3 _target;
    private bool _placed;

    public CartVisual()
    {
        Name = "Carroca";
        var wood = VisualKit.Hide(new Color(0.55f, 0.40f, 0.26f), "madeira", 0.9f, 1.4f);
        var dark = VisualKit.Hide(new Color(0.32f, 0.22f, 0.14f), "madeira", 0.9f, 1.4f);
        var iron = VisualKit.Mat(new Color(0.25f, 0.25f, 0.27f), 0.45f, 0.8f);
        VisualKit.Part(this, VisualKit.Box(1.3f, 0.12f, 2.1f), wood, new Vector3(0, 0.62f, 0));            // assoalho
        foreach (var sx in new[] { -0.62f, 0.62f })
            VisualKit.Part(this, VisualKit.Box(0.08f, 0.38f, 2.1f), dark, new Vector3(sx, 0.86f, 0));      // laterais
        VisualKit.Part(this, VisualKit.Box(1.3f, 0.38f, 0.08f), dark, new Vector3(0, 0.86f, -1.02f));      // traseira
        VisualKit.Part(this, VisualKit.Box(0.55f, 0.45f, 0.55f), wood, new Vector3(-0.28f, 0.95f, -0.45f)); // carga
        VisualKit.Part(this, VisualKit.Box(0.45f, 0.38f, 0.45f), dark, new Vector3(0.3f, 0.9f, 0.25f), new Vector3(0, 20, 0));
        VisualKit.Part(this, VisualKit.Cylinder(0.22f, 0.22f, 0.5f, 10), wood, new Vector3(0.25f, 0.95f, -0.55f));
        foreach (var sx in new[] { -0.18f, 0.18f })                                                         // varal
            VisualKit.Part(this, VisualKit.Box(0.06f, 0.06f, 1.4f), dark, new Vector3(sx, 0.55f, 1.6f), new Vector3(10, 0, 0));
        for (var i = 0; i < 2; i++)
        {
            var w = new Node3D { Position = new Vector3(i == 0 ? -0.72f : 0.72f, 0.42f, -0.1f) };
            AddChild(w);
            VisualKit.Part(w, VisualKit.Cylinder(0.42f, 0.42f, 0.08f, 14), dark, Vector3.Zero, new Vector3(0, 0, 90));
            VisualKit.Part(w, VisualKit.Cylinder(0.43f, 0.43f, 0.05f, 14), iron, Vector3.Zero, new Vector3(0, 0, 90));
            VisualKit.Part(w, VisualKit.Box(0.1f, 0.78f, 0.06f), wood, Vector3.Zero);
            VisualKit.Part(w, VisualKit.Box(0.1f, 0.06f, 0.78f), wood, Vector3.Zero);
            _wheels[i] = w;
        }
        // lanterna no poste da frente
        VisualKit.Part(this, VisualKit.Box(0.05f, 1.0f, 0.05f), dark, new Vector3(0.55f, 1.2f, 0.95f));
        VisualKit.Part(this, VisualKit.Sphere(0.09f), VisualKit.Glow(new Color(1f, 0.7f, 0.3f), 3f), new Vector3(0.55f, 1.72f, 0.95f));
        AddChild(new OmniLight3D { LightColor = new Color(1f, 0.72f, 0.4f), LightEnergy = 1.4f, OmniRange = 5f, Position = new Vector3(0.55f, 1.8f, 0.95f) });
        _puller = VisualFactory.NpcVisual("aldeao", "carroceiro", "Carroceiro");
        _puller.Root.Position = new Vector3(0, 0, 2.35f);
        AddChild(_puller.Root);
    }

    public void SetTarget(Vector3 p)
    {
        _target = p;
        if (!_placed) { Position = p; _placed = true; }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var step = _target - Position;
        step.Y = 0;
        var dist = step.Length();
        if (dist > 6f) { Position = _target; return; }
        if (dist > 0.01f)
        {
            var move = step * Mathf.Min(1f, dt * 4f);
            Position += move;
            var yaw = Mathf.Atan2(step.X, step.Z);
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(1f, dt * 3f)), 0);
            foreach (var w in _wheels) w.RotateX(move.Length() / 0.42f);
        }
        _speed = Mathf.Lerp(_speed, dist > 0.05f ? Mathf.Min(2.4f, dist * 4f) : 0f, Mathf.Min(1f, dt * 6f));
        _puller?.Update(new VisualFrame { Alive = true, Action = _speed > 0.3f ? "" : "", Speed = _speed }, dt);
    }
}

using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Olho Alado (Galeria Funda): globo ocular pálido com veias, íris vermelha e asas de morcego. Paira a ~1,6 m,
/// carrega o raio com a íris brilhando cada vez mais forte e dispara de olho arregalado.
/// </summary>
public sealed class EyeVisual : IEntityVisual
{
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly Node3D _body, _wingL, _wingR, _iris, _lidTop, _lidBottom;
    private readonly StandardMaterial3D _irisMat;
    private readonly OmniLight3D _light;
    private const float Hover = 1.6f;
    private float _time, _hitT = 1f, _deathT, _fade, _y, _blink = 1f, _blinkAt = 2f;
    private bool _wasAlive = true;

    public Node3D Root { get; } = new() { Name = "OlhoAlado" };
    public float Height => Hover + 0.6f;

    public EyeVisual(float scale = 1f)
    {
        var s = VisualKit.Joint(Root, Vector3.Zero);
        s.Scale = Vector3.One * scale;
        _body = VisualKit.Joint(s, new Vector3(0, Hover, 0));
        var white = _flash.Add(VisualKit.Mat(new Color(0.88f, 0.84f, 0.76f), 0.35f));
        var vein = _flash.Add(VisualKit.Mat(new Color(0.55f, 0.12f, 0.12f), 0.6f));
        var flesh = _flash.Add(VisualKit.Mat(new Color(0.32f, 0.18f, 0.20f), 0.75f));
        VisualKit.Part(_body, VisualKit.Sphere(0.42f), white, Vector3.Zero);
        // veias: tiras finas correndo para trás do globo
        for (var i = 0; i < 7; i++)
        {
            var a = i / 7f * Mathf.Tau;
            VisualKit.Part(_body, VisualKit.Box(0.025f, 0.025f, 0.42f), vein,
                new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, -0.12f), new Vector3(Mathf.Sin(a) * 25, -Mathf.Cos(a) * 25, 0));
        }
        // nervo / cauda carnuda atrás
        VisualKit.Part(_body, VisualKit.Cylinder(0.05f, 0.14f, 0.5f, 8), flesh, new Vector3(0, 0, -0.55f), new Vector3(-90, 0, 0));
        _irisMat = VisualKit.Glow(new Color(0.9f, 0.12f, 0.08f), 1.2f);
        _iris = VisualKit.Joint(_body, new Vector3(0, 0, 0.36f));
        VisualKit.Part(_iris, VisualKit.Cylinder(0.2f, 0.2f, 0.06f, 20), _irisMat, Vector3.Zero, new Vector3(90, 0, 0));
        VisualKit.Part(_iris, VisualKit.Cylinder(0.09f, 0.09f, 0.08f, 16), VisualKit.Mat(new Color(0.02f, 0.01f, 0.01f), 0.2f),
            new Vector3(0, 0, 0.02f), new Vector3(90, 0, 0));
        // pálpebras carnudas (piscam e se abrem no disparo)
        _lidTop = VisualKit.Joint(_body, Vector3.Zero);
        _lidBottom = VisualKit.Joint(_body, Vector3.Zero);
        VisualKit.Part(_lidTop, VisualKit.Sphere(0.445f), flesh, Vector3.Zero).Scale = new Vector3(1f, 0.5f, 1f);
        VisualKit.Part(_lidBottom, VisualKit.Sphere(0.445f), flesh, Vector3.Zero).Scale = new Vector3(1f, 0.5f, 1f);
        _lidTop.GetChild<Node3D>(0).Position = new Vector3(0, 0.2f, -0.06f);
        _lidBottom.GetChild<Node3D>(0).Position = new Vector3(0, -0.2f, -0.06f);
        // asas de morcego
        var wing = _flash.Add(VisualKit.Mat(new Color(0.22f, 0.12f, 0.14f), 0.85f));
        _wingL = VisualKit.Joint(_body, new Vector3(0.36f, 0.08f, -0.1f));
        _wingR = VisualKit.Joint(_body, new Vector3(-0.36f, 0.08f, -0.1f));
        foreach (var (w, side) in new[] { (_wingL, 1f), (_wingR, -1f) })
        {
            VisualKit.Part(w, VisualKit.Capsule(0.025f, 1.0f), flesh, new Vector3(side * 0.45f, 0.12f, 0), new Vector3(0, 0, side * 70));
            VisualKit.Part(w, VisualKit.Box(0.75f, 0.02f, 0.42f), wing, new Vector3(side * 0.42f, 0.02f, -0.14f), new Vector3(0, 0, side * 12));
            VisualKit.Part(w, VisualKit.Box(0.45f, 0.02f, 0.28f), wing, new Vector3(side * 0.9f, 0.12f, -0.2f), new Vector3(0, side * 18, side * 20));
        }
        _light = new OmniLight3D { LightColor = new Color(1f, 0.15f, 0.08f), LightEnergy = 0.4f, OmniRange = 3.5f, Position = new Vector3(0, 0, 0.6f) };
        _body.AddChild(_light);
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) { _hitT = 0f; _blink = 0f; }

    public void Update(in VisualFrame f, float delta)
    {
        _time += delta;
        if (!f.Alive)
        {
            if (_wasAlive) { _deathT = 0f; _fade = 0f; }
            _wasAlive = false;
            _deathT = Mathf.Min(1f, _deathT + delta / 0.7f);
            _body.Position = new Vector3(0, Mathf.Lerp(Hover, 0.35f, Mathf.SmoothStep(0, 1, _deathT)), 0);
            _body.Rotation = new Vector3(Mathf.Pi * 0.4f * _deathT, 0, 0.6f * _deathT);
            _wingL.Rotation = new Vector3(0, 0, -0.9f * _deathT);
            _wingR.Rotation = new Vector3(0, 0, 0.9f * _deathT);
            _light.LightEnergy = 0f;
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.4f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive) { _wasAlive = true; Root.Visible = true; _body.Rotation = Vector3.Zero; }
        var colon = f.Action.IndexOf(':');
        var phase = colon < 0 ? f.Action : f.Action[..colon];
        var id = colon < 0 ? "" : f.Action[(colon + 1)..];
        var charging = phase == "windup" && id == "laser";
        var firing = phase == "active" && id == "laser";

        var flap = Mathf.Sin(_time * (charging ? 16f : 9f));
        _wingL.Rotation = new Vector3(0.1f * flap, 0, 0.75f * flap);
        _wingR.Rotation = new Vector3(0.1f * flap, 0, -0.75f * flap);

        var target = Hover + 0.12f * Mathf.Sin(_time * 2.4f);
        if (charging) target += 0.25f * f.ActionT;
        _y = VisualKit.Approach(_y == 0 ? target : _y, target, 6f, delta);
        var recoil = firing ? -0.08f + 0.03f * Mathf.Sin(_time * 70f) : 0f;
        _body.Position = new Vector3(0, _y, recoil);
        var tremble = charging && f.ActionT > 0.6f ? 0.04f * Mathf.Sin(_time * 55f) : 0f;
        _body.Rotation = new Vector3(phase == "recover" ? 0.35f * (1f - f.ActionT) : 0.05f * Mathf.Sin(_time * 1.7f), 0, tremble);

        // pálpebras: piscadas ocasionais; arregala ao carregar/disparar; meio fechado na recuperação (brecha)
        _blinkAt -= delta;
        if (_blinkAt <= 0f) { _blink = 0f; _blinkAt = 2.5f + (_time * 7.3f % 2.5f); }
        _blink = Mathf.Min(1f, _blink + delta / 0.18f);
        var open = phase == "recover" ? 0.45f : charging || firing ? 1.2f : 0.85f;
        open *= Mathf.Abs(_blink * 2f - 1f) * 0.7f + 0.3f;
        _lidTop.Rotation = new Vector3(-open * 0.9f, 0, 0);
        _lidBottom.Rotation = new Vector3(open * 0.7f, 0, 0);

        var charge = charging ? f.ActionT : firing ? 1f : 0f;
        _irisMat.EmissionEnergyMultiplier = 1.2f + 5f * charge;
        _iris.Scale = Vector3.One * (charging ? 1f - 0.35f * f.ActionT : firing ? 0.6f : 1f);
        _light.LightEnergy = 0.4f + 3.5f * charge;
        if (_hitT < 1f) _hitT = Mathf.Min(1f, _hitT + delta / 0.25f);
        _flash.Apply(_hitT < 1f ? 1f - _hitT : 0f, f.WindupGlow * 0.5f, new Color(1f, 0.25f, 0.1f));
    }

    public void Dispose() => Root.QueueFree();
}

using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// 12/11 (pedido do dono: "mascote como grilo, macaco, águia" + "tartaruga dá escudo, coelho dá velocidade de ataque"):
/// companheiros pequenos feitos de formas simples (como o vagalume), cada um com um jeito de andar:
/// tartaruga passo lento balançando, coelho e grilo pulando, macaco gingando o rabo, águia planando acima do ombro.
/// </summary>
public sealed class MascoteVisual : IEntityVisual
{
    public Node3D Root { get; } = new() { Name = "Mascote" };
    public float Height => _kind == "aguia" ? 2.6f : _kind == "coruja" ? 1.9f : 0.8f;
    private readonly string _kind;
    private readonly Node3D _body = new();
    private Node3D? _tail, _wingL, _wingR, _legsA, _legsB, _head;
    private float _t, _hopT;

    public MascoteVisual(string kind)
    {
        _kind = kind;
        Root.AddChild(_body);
        switch (kind)
        {
            case "tartaruga": Tartaruga(); break;
            case "coelho": Coelho(); break;
            case "grilo": Grilo(); break;
            case "macaco": Macaco(); break;
            case "raposa": Raposa(); break;
            case "coruja": Coruja(); break;
            default: Aguia(); break;
        }
    }

    private static StandardMaterial3D M(float r, float g, float b, float rough = 0.8f) => VisualKit.Mat(new Color(r, g, b), rough);

    private void Olhos(Node3D at, float y, float z, float side, float size = 0.022f)
    {
        foreach (var s in new[] { -1f, 1f })
            VisualKit.Part(at, VisualKit.Sphere(size), M(0.05f, 0.05f, 0.05f, 0.3f), new Vector3(s * side, y, z));
    }

    private void Tartaruga()
    {
        var casco = M(0.28f, 0.42f, 0.22f, 0.6f);
        var pele = M(0.55f, 0.62f, 0.38f);
        VisualKit.Part(_body, VisualKit.Sphere(0.24f), casco, new Vector3(0, 0.16f, 0), null, new Vector3(1f, 0.55f, 1.15f));
        VisualKit.Part(_body, VisualKit.Cylinder(0.25f, 0.27f, 0.05f, 16), M(0.6f, 0.5f, 0.3f), new Vector3(0, 0.1f, 0), null, new Vector3(1f, 1f, 1.12f));
        // placas do casco
        for (var i = 0; i < 5; i++)
            VisualKit.Part(_body, VisualKit.Sphere(0.06f), M(0.22f, 0.33f, 0.18f, 0.5f), new Vector3((i % 2 == 0 ? -1 : 1) * 0.08f * (i / 2), 0.27f, -0.12f + i * 0.06f), null, new Vector3(1f, 0.35f, 1f));
        _head = new Node3D { Position = new Vector3(0, 0.15f, 0.3f) };
        _body.AddChild(_head);
        VisualKit.Part(_head, VisualKit.Sphere(0.08f), pele, Vector3.Zero, null, new Vector3(1f, 0.85f, 1.2f));
        Olhos(_head, 0.03f, 0.06f, 0.04f, 0.014f);
        _legsA = new Node3D(); _legsB = new Node3D(); _body.AddChild(_legsA); _body.AddChild(_legsB);
        foreach (var (x, z, n) in new[] { (-0.17f, 0.14f, _legsA), (0.17f, -0.14f, _legsA), (0.17f, 0.14f, _legsB), (-0.17f, -0.14f, _legsB) })
            VisualKit.Part(n, VisualKit.Sphere(0.06f), pele, new Vector3(x, 0.05f, z), null, new Vector3(1f, 0.7f, 1.2f));
        VisualKit.Part(_body, VisualKit.Sphere(0.04f), pele, new Vector3(0, 0.1f, -0.28f));
    }

    private void Coelho()
    {
        var pelo = M(0.9f, 0.88f, 0.84f);
        VisualKit.Part(_body, VisualKit.Sphere(0.15f), pelo, new Vector3(0, 0.16f, -0.03f), null, new Vector3(1f, 0.95f, 1.2f));
        _head = new Node3D { Position = new Vector3(0, 0.3f, 0.12f) };
        _body.AddChild(_head);
        VisualKit.Part(_head, VisualKit.Sphere(0.1f), pelo, Vector3.Zero);
        Olhos(_head, 0.03f, 0.08f, 0.045f, 0.016f);
        VisualKit.Part(_head, VisualKit.Sphere(0.018f), M(0.95f, 0.55f, 0.6f), new Vector3(0, -0.01f, 0.1f));
        foreach (var s in new[] { -1f, 1f })
        {
            VisualKit.Part(_head, VisualKit.Capsule(0.032f, 0.22f), pelo, new Vector3(s * 0.045f, 0.16f, -0.02f), new Vector3(-10, 0, s * -10));
            VisualKit.Part(_head, VisualKit.Capsule(0.018f, 0.16f), M(0.95f, 0.7f, 0.72f), new Vector3(s * 0.045f, 0.16f, 0.0f), new Vector3(-10, 0, s * -10));
        }
        VisualKit.Part(_body, VisualKit.Sphere(0.05f), M(1f, 1f, 1f), new Vector3(0, 0.18f, -0.2f));
    }

    private void Grilo()
    {
        var verde = M(0.35f, 0.62f, 0.22f, 0.5f);
        VisualKit.Part(_body, VisualKit.Capsule(0.06f, 0.26f), verde, new Vector3(0, 0.12f, 0), new Vector3(90, 0, 0));
        _head = new Node3D { Position = new Vector3(0, 0.14f, 0.15f) };
        _body.AddChild(_head);
        VisualKit.Part(_head, VisualKit.Sphere(0.06f), verde, Vector3.Zero);
        Olhos(_head, 0.02f, 0.04f, 0.035f, 0.016f);
        foreach (var s in new[] { -1f, 1f })
        {
            VisualKit.Part(_head, VisualKit.Cylinder(0.004f, 0.004f, 0.3f, 4), M(0.2f, 0.3f, 0.15f), new Vector3(s * 0.03f, 0.1f, 0.12f), new Vector3(55, 0, s * 15));
            // patas traseiras grandes (saltadoras)
            VisualKit.Part(_body, VisualKit.Capsule(0.022f, 0.22f), verde, new Vector3(s * 0.08f, 0.12f, -0.06f), new Vector3(-35, 0, s * 20));
        }
        _wingL = new Node3D { Position = new Vector3(-0.03f, 0.18f, -0.02f) }; _wingR = new Node3D { Position = new Vector3(0.03f, 0.18f, -0.02f) };
        _body.AddChild(_wingL); _body.AddChild(_wingR);
        foreach (var w in new[] { _wingL, _wingR })
            VisualKit.Part(w, VisualKit.Box(0.05f, 0.01f, 0.22f), M(0.45f, 0.55f, 0.25f, 0.4f), new Vector3(0, 0, -0.05f));
    }

    private void Macaco()
    {
        var pelo = M(0.45f, 0.3f, 0.18f);
        var cara = M(0.85f, 0.68f, 0.52f);
        VisualKit.Part(_body, VisualKit.Capsule(0.11f, 0.3f), pelo, new Vector3(0, 0.3f, 0), new Vector3(15, 0, 0));
        _head = new Node3D { Position = new Vector3(0, 0.55f, 0.04f) };
        _body.AddChild(_head);
        VisualKit.Part(_head, VisualKit.Sphere(0.11f), pelo, Vector3.Zero);
        VisualKit.Part(_head, VisualKit.Sphere(0.075f), cara, new Vector3(0, -0.02f, 0.06f), null, new Vector3(1.1f, 0.9f, 0.8f));
        Olhos(_head, 0.02f, 0.11f, 0.035f, 0.016f);
        foreach (var s in new[] { -1f, 1f }) VisualKit.Part(_head, VisualKit.Sphere(0.04f), cara, new Vector3(s * 0.11f, 0.01f, 0));
        _legsA = new Node3D(); _legsB = new Node3D(); _body.AddChild(_legsA); _body.AddChild(_legsB);
        VisualKit.Part(_legsA, VisualKit.Capsule(0.035f, 0.22f), pelo, new Vector3(-0.07f, 0.1f, 0));
        VisualKit.Part(_legsB, VisualKit.Capsule(0.035f, 0.22f), pelo, new Vector3(0.07f, 0.1f, 0));
        foreach (var s in new[] { -1f, 1f }) VisualKit.Part(_body, VisualKit.Capsule(0.03f, 0.26f), pelo, new Vector3(s * 0.13f, 0.32f, 0.04f), new Vector3(-20, 0, s * 12));
        _tail = new Node3D { Position = new Vector3(0, 0.2f, -0.1f) };
        _body.AddChild(_tail);
        VisualKit.Part(_tail, VisualKit.Capsule(0.02f, 0.4f), pelo, new Vector3(0, 0.12f, -0.12f), new Vector3(-55, 0, 0));
    }

    // 12/11: raposa de cauda de brasa (anda como o macaco, rabo balançando com ponta brilhando)
    private void Raposa()
    {
        var pelo = M(0.85f, 0.40f, 0.12f); var branco = M(0.95f, 0.92f, 0.85f);
        VisualKit.Part(_body, VisualKit.Capsule(0.09f, 0.34f), pelo, new Vector3(0, 0.22f, 0), new Vector3(90, 0, 0));
        _head = new Node3D { Position = new Vector3(0, 0.3f, 0.2f) };
        _body.AddChild(_head);
        VisualKit.Part(_head, VisualKit.Sphere(0.08f), pelo, Vector3.Zero);
        VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.05f, 0.1f, 6), branco, new Vector3(0, -0.02f, 0.09f), new Vector3(90, 0, 0));   // focinho
        foreach (var sd in new[] { -1f, 1f })
            VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.035f, 0.09f, 4), pelo, new Vector3(sd * 0.045f, 0.08f, -0.01f));            // orelhas
        Olhos(_head, 0.02f, 0.06f, 0.035f, 0.014f);
        _legsA = new Node3D(); _legsB = new Node3D(); _body.AddChild(_legsA); _body.AddChild(_legsB);
        foreach (var (lg, z) in new[] { (_legsA, 0.12f), (_legsB, -0.12f) })
            foreach (var sd in new[] { -1f, 1f })
                VisualKit.Part(lg, VisualKit.Cylinder(0.018f, 0.02f, 0.18f, 5), M(0.25f, 0.15f, 0.1f), new Vector3(sd * 0.05f, 0.09f, z));
        _tail = new Node3D { Position = new Vector3(0, 0.26f, -0.2f) };
        _body.AddChild(_tail);
        VisualKit.Part(_tail, VisualKit.Capsule(0.05f, 0.26f), pelo, new Vector3(0, 0.03f, -0.12f), new Vector3(60, 0, 0));
        VisualKit.Part(_tail, VisualKit.Sphere(0.045f), VisualKit.Glow(new Color(1f, 0.55f, 0.15f), 2.5f), new Vector3(0, 0.1f, -0.24f));
    }

    // 12/11: coruja (pousa no ombro do dono, vira a cabeça e pisca)
    private void Coruja()
    {
        var penas = M(0.48f, 0.38f, 0.28f); var claro = M(0.88f, 0.84f, 0.74f);
        VisualKit.Part(_body, VisualKit.Sphere(0.12f), penas, Vector3.Zero, null, new Vector3(1f, 1.25f, 0.95f));
        VisualKit.Part(_body, VisualKit.Sphere(0.09f), claro, new Vector3(0, -0.02f, 0.05f), null, new Vector3(1f, 1.2f, 0.6f));
        _head = new Node3D { Position = new Vector3(0, 0.16f, 0) };
        _body.AddChild(_head);
        VisualKit.Part(_head, VisualKit.Sphere(0.1f), penas, Vector3.Zero);
        foreach (var sd in new[] { -1f, 1f })
        {
            VisualKit.Part(_head, VisualKit.Sphere(0.035f), claro, new Vector3(sd * 0.04f, 0.0f, 0.08f));
            VisualKit.Part(_head, VisualKit.Sphere(0.018f), VisualKit.Glow(new Color(1f, 0.75f, 0.2f), 1.6f), new Vector3(sd * 0.04f, 0.0f, 0.105f));
            VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.02f, 0.06f, 4), penas, new Vector3(sd * 0.06f, 0.09f, 0));
        }
        _wingL = new Node3D { Position = new Vector3(-0.11f, 0.02f, 0) }; _wingR = new Node3D { Position = new Vector3(0.11f, 0.02f, 0) };
        _body.AddChild(_wingL); _body.AddChild(_wingR);
        VisualKit.Part(_wingL, VisualKit.Box(0.04f, 0.18f, 0.14f), penas, Vector3.Zero);
        VisualKit.Part(_wingR, VisualKit.Box(0.04f, 0.18f, 0.14f), penas, Vector3.Zero);
    }

    private void Aguia()
    {
        var penas = M(0.36f, 0.25f, 0.15f);
        var branco = M(0.95f, 0.93f, 0.88f);
        VisualKit.Part(_body, VisualKit.Capsule(0.08f, 0.32f), penas, Vector3.Zero, new Vector3(90, 0, 0));
        _head = new Node3D { Position = new Vector3(0, 0.05f, 0.2f) };
        _body.AddChild(_head);
        VisualKit.Part(_head, VisualKit.Sphere(0.075f), branco, Vector3.Zero);
        VisualKit.Part(_head, VisualKit.Cylinder(0.0f, 0.03f, 0.08f, 6), M(0.95f, 0.75f, 0.2f), new Vector3(0, -0.01f, 0.08f), new Vector3(90, 0, 0));
        Olhos(_head, 0.02f, 0.05f, 0.04f, 0.013f);
        VisualKit.Part(_body, VisualKit.Box(0.14f, 0.02f, 0.16f), branco, new Vector3(0, 0, -0.22f));
        _wingL = new Node3D { Position = new Vector3(-0.07f, 0.03f, 0) }; _wingR = new Node3D { Position = new Vector3(0.07f, 0.03f, 0) };
        _body.AddChild(_wingL); _body.AddChild(_wingR);
        VisualKit.Part(_wingL, VisualKit.Box(0.5f, 0.02f, 0.2f), penas, new Vector3(-0.25f, 0, -0.02f));
        VisualKit.Part(_wingR, VisualKit.Box(0.5f, 0.02f, 0.2f), penas, new Vector3(0.25f, 0, -0.02f));
    }

    public void Update(in VisualFrame frame, float delta)
    {
        _t += delta;
        var moving = frame.Speed > 0.4f;
        switch (_kind)
        {
            case "tartaruga":
                _body.Position = new Vector3(0, 0, 0);
                _body.Rotation = new Vector3(0, 0, moving ? 0.08f * Mathf.Sin(_t * 6f) : 0f);
                if (_legsA is not null && _legsB is not null)
                {
                    var k = moving ? 0.05f * Mathf.Sin(_t * 6f) : 0f;
                    _legsA.Position = new Vector3(0, 0, k); _legsB.Position = new Vector3(0, 0, -k);
                }
                if (_head is not null) _head.Position = new Vector3(0, 0.15f, 0.3f + 0.02f * Mathf.Sin(_t * 1.5f));
                break;
            case "coelho" or "grilo":
            {
                // pula andando; parado, um pulinho de vez em quando
                var rate = moving ? (_kind == "grilo" ? 5f : 3.2f) : 0.6f;
                _hopT += delta * rate;
                var ph = _hopT % 1f;
                var hop = moving || ph < 0.25f ? Mathf.Max(0f, Mathf.Sin(ph * Mathf.Pi)) : 0f;
                _body.Position = new Vector3(0, hop * (_kind == "grilo" ? 0.35f : 0.22f), 0);
                _body.Rotation = new Vector3(-hop * 0.3f, 0, 0);
                if (_wingL is not null && _wingR is not null)
                {
                    var buzz = hop > 0.1f ? 0.5f * Mathf.Sin(_t * 60f) : 0f;
                    _wingL.Rotation = new Vector3(0, 0, buzz); _wingR.Rotation = new Vector3(0, 0, -buzz);
                }
                break;
            }
            case "coruja":
                // no ombro do dono; parada gira a cabeça para os lados; andando, abre as asas de leve
                _body.Position = new Vector3(0.28f, 1.62f, -0.05f);
                if (_head is not null) _head.Rotation = new Vector3(0, 1.2f * Mathf.Sin(_t * 0.5f) * (Mathf.Sin(_t * 0.23f) > 0 ? 1 : 0), 0);
                if (_wingL is not null && _wingR is not null)
                {
                    var abre = moving ? 0.35f + 0.15f * Mathf.Sin(_t * 10f) : 0f;
                    _wingL.Rotation = new Vector3(0, 0, abre); _wingR.Rotation = new Vector3(0, 0, -abre);
                }
                break;
            case "macaco" or "raposa":
                _body.Position = new Vector3(0, moving ? 0.04f * Mathf.Abs(Mathf.Sin(_t * 8f)) : 0f, 0);
                _body.Rotation = new Vector3(0, 0, moving ? 0.12f * Mathf.Sin(_t * 8f) : 0.04f * Mathf.Sin(_t * 1.4f));
                if (_tail is not null) _tail.Rotation = new Vector3(0, 0.6f * Mathf.Sin(_t * 2.5f), 0);
                if (_legsA is not null && _legsB is not null && moving)
                {
                    _legsA.Rotation = new Vector3(0.5f * Mathf.Sin(_t * 8f), 0, 0);
                    _legsB.Rotation = new Vector3(-0.5f * Mathf.Sin(_t * 8f), 0, 0);
                }
                if (_head is not null) _head.Rotation = new Vector3(0, 0.4f * Mathf.Sin(_t * 0.7f), 0);
                break;
            default:
            {
                // águia: plana em círculo largo acima do dono, batendo de vez em quando
                _body.Position = new Vector3(Mathf.Cos(_t * 0.6f) * 0.9f, 2.4f + 0.2f * Mathf.Sin(_t * 1.3f), Mathf.Sin(_t * 0.6f) * 0.9f);
                _body.Rotation = new Vector3(0, -_t * 0.6f, 0.35f);
                var flap = (moving || Mathf.Sin(_t * 0.5f) > 0.7f) ? 0.6f * Mathf.Sin(_t * 9f) : 0.08f * Mathf.Sin(_t * 2f);
                if (_wingL is not null && _wingR is not null) { _wingL.Rotation = new Vector3(0, 0, flap); _wingR.Rotation = new Vector3(0, 0, -flap); }
                break;
            }
        }
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) { }
    public void Dispose() => Root.QueueFree();
}

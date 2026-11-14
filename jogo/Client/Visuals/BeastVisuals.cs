using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>Aparência de um quadrúpede (lobo, javali, lagarto, basilisco, sapo, corcel, quimera, centauro).</summary>
public sealed class BeastStyle
{
    public float Scale = 1f;
    public Color Body = new(0.30f, 0.28f, 0.27f);
    public Color Belly = new(0.45f, 0.40f, 0.34f);
    public Color Accent = new(0.78f, 0.73f, 0.64f);   // presas, chifres, juntas (osso)
    public Color Eyes = new(1f, 0.35f, 0.15f);
    public string Head = "lobo";                       // lobo | javali | lagarto | sapo | cavalo | leao | cabra
    public bool Spikes, Tusks, Horns, Saddle, Mane, LongTail = true, Plates;
    public float LegLength = 0.55f, BodyLength = 1.1f, BodyHeight = 0.42f;
    public bool Rider;                                  // centauro: tronco humanoide em cima
    public int Heads = 1;                               // 3 = cão de três cabeças (as de lado acompanham o pescoço)
    public bool Fur;                                    // pelagem desgrenhada (tufos) — rato, chupa-cabra
}

/// <summary>
/// Quadrúpede articulado (4 patas de 2 segmentos, corpo, pescoço, cabeça, cauda). Preparação: abaixa e recua o corpo
/// (bote/investida/raspa o chão); golpe: avança; recuperação: tropeça (brecha). Também usado pelo corcel (montaria).
/// </summary>
public sealed class BeastVisual : IEntityVisual
{
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly BeastStyle _s;
    private readonly Node3D _body, _neck, _head, _jaw, _tail;
    private readonly List<(Node3D Hip, Node3D Knee, float Phase, bool Front)> _legs = new();
    private float _phase, _time, _hitT = 1f, _deathT, _fade;
    private bool _wasAlive = true;
    private Vector3 _bodyRot;
    private float _bodyZ, _bodyY;

    public Node3D Root { get; } = new() { Name = "Quadrupede" };
    public float Height { get; }

    public BeastVisual(BeastStyle s)
    {
        _s = s;
        Height = (s.LegLength + s.BodyHeight + 0.35f) * s.Scale;
        var scale = VisualKit.Joint(Root, Vector3.Zero);
        scale.Scale = Vector3.One * s.Scale;
        // couro com textura: lagarto/serpente/sapo de escamas; o resto de pele/pelo
        var scaly = s.Head is "lagarto" or "serpente" or "sapo";
        var body = _flash.Add(VisualKit.Hide(s.Body, scaly ? "escama" : "pele", 0.8f, scaly ? 3f : 2f));
        var belly = _flash.Add(VisualKit.Hide(s.Belly, "pele", 0.85f));
        var bone = _flash.Add(VisualKit.Mat(s.Accent, 0.6f));
        var eyes = VisualKit.Glow(s.Eyes, 2.5f);
        var L = s.BodyLength;
        _body = VisualKit.Joint(scale, new Vector3(0, s.LegLength + s.BodyHeight * 0.5f, 0), "Corpo");
        VisualKit.Part(_body, VisualKit.Capsule(s.BodyHeight * 0.5f, L + s.BodyHeight), body, Vector3.Zero, new Vector3(90, 0, 0),
            new Vector3(1.0f, 1f, 0.95f));
        VisualKit.Part(_body, VisualKit.Box(s.BodyHeight * 0.7f, s.BodyHeight * 0.3f, L * 0.8f), belly, new Vector3(0, -s.BodyHeight * 0.32f, 0));
        if (s.Spikes || s.Mane)
            for (var i = 0; i < 6; i++)
                VisualKit.Part(_body, VisualKit.Cylinder(0f, 0.07f, s.Mane ? 0.22f : 0.3f, 4), s.Mane ? body : bone,
                    new Vector3(0, s.BodyHeight * 0.48f, L * 0.45f - i * L * 0.16f), new Vector3(-25, 0, 0));
        if (s.Plates)
            for (var i = 0; i < 4; i++)
                VisualKit.Part(_body, VisualKit.Box(s.BodyHeight * 1.05f, 0.08f, L * 0.22f), _flash.Add(VisualKit.Mat(s.Body.Lightened(0.15f), 0.5f, 0.4f)),
                    new Vector3(0, s.BodyHeight * 0.45f, L * 0.36f - i * L * 0.24f), new Vector3(6, 0, 0));
        if (s.Saddle)
        {
            VisualKit.Part(_body, VisualKit.Box(s.BodyHeight * 1.1f, 0.12f, 0.55f), _flash.Add(VisualKit.Mat(new Color(0.30f, 0.18f, 0.12f), 0.8f)),
                new Vector3(0, s.BodyHeight * 0.5f, 0.05f));
            VisualKit.Part(_body, VisualKit.Box(s.BodyHeight * 1.15f, 0.25f, 0.06f), _flash.Add(VisualKit.Mat(new Color(0.39f, 0.18f, 0.22f), 0.9f)),
                new Vector3(0, s.BodyHeight * 0.2f, 0.05f));
        }
        // pescoço e cabeça
        _neck = VisualKit.Joint(_body, new Vector3(0, s.BodyHeight * 0.15f, L * 0.55f), "Pescoco");
        VisualKit.Part(_neck, VisualKit.Capsule(s.BodyHeight * 0.28f, s.Head == "cavalo" ? 0.7f : 0.4f), body,
            new Vector3(0, s.Head == "cavalo" ? 0.28f : 0.1f, 0.12f), new Vector3(s.Head == "cavalo" ? 35 : 70, 0, 0));
        _head = VisualKit.Joint(_neck, new Vector3(0, s.Head == "cavalo" ? 0.55f : 0.18f, s.Head == "cavalo" ? 0.35f : 0.3f), "Cabeca");
        switch (s.Head)
        {
            case "javali":
                VisualKit.Part(_head, VisualKit.Box(0.32f, 0.3f, 0.42f), body, new Vector3(0, 0, 0.12f));
                VisualKit.Part(_head, VisualKit.Cylinder(0.1f, 0.13f, 0.14f, 8), belly, new Vector3(0, -0.04f, 0.38f), new Vector3(90, 0, 0));
                break;
            case "lagarto":
                VisualKit.Part(_head, VisualKit.Box(0.26f, 0.16f, 0.5f), body, new Vector3(0, 0, 0.2f));
                break;
            case "sapo":
                VisualKit.Part(_head, VisualKit.Sphere(0.3f), body, new Vector3(0, 0.05f, 0.08f), null, new Vector3(1.3f, 0.7f, 1f));
                VisualKit.Part(_head, VisualKit.Sphere(0.27f), belly, new Vector3(0, -0.06f, 0.12f), null, new Vector3(1.2f, 0.45f, 0.95f));   // papo claro
                foreach (var side in new[] { -1f, 1f })   // olhos saltados no alto da cabeça
                {
                    VisualKit.Part(_head, VisualKit.Sphere(0.09f), body, new Vector3(side * 0.17f, 0.2f, 0.12f));
                    VisualKit.Part(_head, VisualKit.Sphere(0.055f), eyes, new Vector3(side * 0.18f, 0.24f, 0.17f));
                }
                break;
            case "cavalo":
                VisualKit.Part(_head, VisualKit.Box(0.22f, 0.24f, 0.55f), body, new Vector3(0, 0, 0.18f), new Vector3(20, 0, 0));
                foreach (var side in new[] { -1f, 1f })
                    VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.05f, 0.16f, 4), body, new Vector3(side * 0.08f, 0.18f, -0.02f));
                break;
            case "cabra":
            {
                // 14/11 ("a cabra parece um cavalo — coloca ela com chifrões curvados"): cabeça curta em cunha, barbicha,
                // orelhas de lado e dois chifrões de carneiro enrolados para trás e para baixo
                VisualKit.Part(_head, VisualKit.Box(0.22f, 0.22f, 0.24f), body, new Vector3(0, 0.02f, 0.02f));
                VisualKit.Part(_head, VisualKit.Box(0.15f, 0.15f, 0.24f), body, new Vector3(0, -0.04f, 0.2f), new Vector3(18, 0, 0));
                VisualKit.Part(_head, VisualKit.Cylinder(0.01f, 0.045f, 0.16f, 6), _flash.Add(VisualKit.Mat(s.Accent, 0.9f)), new Vector3(0, -0.17f, 0.18f), new Vector3(10, 0, 0));   // barbicha
                foreach (var side in new[] { -1f, 1f })
                {
                    VisualKit.Part(_head, VisualKit.Box(0.14f, 0.04f, 0.07f), body, new Vector3(side * 0.16f, 0.04f, -0.02f), new Vector3(0, 0, side * -20));   // orelha
                    // chifrão: 11 gomos que saem do alto da cabeça, vão para trás, descem e voltam enrolando (espiral)
                    var raio = 0.13f;
                    for (var i = 0; i < 11; i++)
                    {
                        var t = i / 10f;
                        var a = Mathf.Lerp(-0.3f, 4.6f, t);                  // ângulo da volta (rad)
                        var r = raio * (1f - t * 0.45f);
                        var pos = new Vector3(side * (0.09f + t * 0.1f), 0.14f + Mathf.Sin(a) * r, -0.04f - Mathf.Cos(a) * r + 0.06f);
                        var grossura = 0.055f * (1f - t * 0.6f);
                        VisualKit.Part(_head, VisualKit.Sphere(grossura), bone, pos, null, new Vector3(1f, 1f, 1.25f));
                    }
                }
                break;
            }
            case "leao":
                VisualKit.Part(_head, VisualKit.Sphere(0.36f), _flash.Add(VisualKit.Mat(s.Accent.Darkened(0.4f), 0.9f)), new Vector3(0, 0.02f, 0), null, new Vector3(1.2f, 1.2f, 0.9f));
                VisualKit.Part(_head, VisualKit.Box(0.28f, 0.26f, 0.34f), body, new Vector3(0, 0, 0.22f));
                break;
            case "rato":
            {
                // 10/11 ("o rato parece um cachorro"): cabeça em cunha fina, focinho rosado pontudo, orelhas grandes e
                // redondas, bigodes — nada de focinho quadrado de cão
                var pink = _flash.Add(VisualKit.Mat(new Color(0.85f, 0.55f, 0.55f), 0.7f));
                VisualKit.Part(_head, VisualKit.Cylinder(0.035f, 0.15f, 0.42f, 8), body, new Vector3(0, -0.01f, 0.18f), new Vector3(90, 0, 0));
                VisualKit.Part(_head, VisualKit.Sphere(0.035f), pink, new Vector3(0, -0.01f, 0.4f));
                foreach (var side in new[] { -1f, 1f })
                {
                    VisualKit.Part(_head, VisualKit.Cylinder(0.11f, 0.11f, 0.02f, 12), body, new Vector3(side * 0.12f, 0.15f, -0.02f), new Vector3(90, side * 25, 0));
                    VisualKit.Part(_head, VisualKit.Cylinder(0.075f, 0.075f, 0.022f, 12), pink, new Vector3(side * 0.12f, 0.15f, -0.012f), new Vector3(90, side * 25, 0));
                    for (var w = -1; w <= 1; w++)
                        VisualKit.Part(_head, VisualKit.Cylinder(0.004f, 0.004f, 0.22f, 3), bone, new Vector3(side * 0.09f, -0.01f + w * 0.02f, 0.34f), new Vector3(0, 0, 90 + side * (10 + w * 12)));
                }
                break;
            }
            default: // lobo
                VisualKit.Part(_head, VisualKit.Box(0.26f, 0.24f, 0.3f), body, new Vector3(0, 0, 0.05f));
                VisualKit.Part(_head, VisualKit.Box(0.16f, 0.13f, 0.3f), body, new Vector3(0, -0.04f, 0.28f));
                foreach (var side in new[] { -1f, 1f })
                    VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.06f, 0.16f, 4), body, new Vector3(side * 0.09f, 0.18f, -0.02f));
                break;
        }
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(_head, VisualKit.Sphere(0.035f), eyes, new Vector3(side * 0.09f, 0.06f, s.Head == "cavalo" ? 0.2f : 0.2f));
        _jaw = VisualKit.Joint(_head, new Vector3(0, -0.1f, 0.18f), "Mandibula");
        if (s.Head is "lobo" or "lagarto" or "leao")
            VisualKit.Part(_jaw, VisualKit.Box(0.14f, 0.05f, 0.26f), body, new Vector3(0, 0, 0.1f));
        if (s.Tusks)
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.04f, 0.26f, 5), bone, new Vector3(side * 0.13f, -0.02f, 0.38f), new Vector3(-50, side * 15, 0));
        if (s.Horns)
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(_head, VisualKit.Cylinder(0f, 0.06f, 0.4f, 5), bone, new Vector3(side * 0.14f, 0.2f, 0.0f), new Vector3(-30, 0, side * -40));
        if (s.Heads > 1)
            foreach (var side in new[] { -1f, 1f })
            {
                var extra = (Node3D)_head.Duplicate();
                _neck.AddChild(extra);
                extra.Position = _head.Position + new Vector3(side * 0.3f, -0.04f, -0.06f);
                extra.Rotation = new Vector3(0.1f, side * 0.35f, side * 0.15f);
                VisualKit.Part(_neck, VisualKit.Capsule(s.BodyHeight * 0.24f, 0.42f), body, new Vector3(side * 0.16f, 0.06f, 0.1f), new Vector3(70, 0, side * -30));
            }
        if (s.Fur)
            for (var i = 0; i < 10; i++)
                VisualKit.Part(_body, VisualKit.Cylinder(0f, 0.05f, 0.16f, 4), body,
                    new Vector3((i % 2 == 0 ? -1 : 1) * s.BodyHeight * 0.3f, s.BodyHeight * 0.38f, L * 0.45f - i * L * 0.1f), new Vector3(-50, 0, (i % 2 == 0 ? 25 : -25)));
        // cauda
        _tail = VisualKit.Joint(_body, new Vector3(0, s.BodyHeight * 0.2f, -L * 0.6f), "Cauda");
        VisualKit.Part(_tail, VisualKit.Cylinder(0.02f, 0.07f, s.LongTail ? 0.7f : 0.25f, 5), body, new Vector3(0, -0.05f, -0.3f), new Vector3(-70, 0, 0));
        // patas
        foreach (var (z, front) in new[] { (L * 0.42f, true), (-L * 0.42f, false) })
            foreach (var side in new[] { -1f, 1f })
            {
                var hip = VisualKit.Joint(_body, new Vector3(side * s.BodyHeight * 0.38f, -s.BodyHeight * 0.2f, z));
                VisualKit.Part(hip, VisualKit.Capsule(0.06f, s.LegLength * 0.6f), body, new Vector3(0, -s.LegLength * 0.25f, 0));
                var knee = VisualKit.Joint(hip, new Vector3(0, -s.LegLength * 0.5f, 0));
                VisualKit.Part(knee, VisualKit.Sphere(0.055f), bone, Vector3.Zero);
                VisualKit.Part(knee, VisualKit.Capsule(0.045f, s.LegLength * 0.6f), body, new Vector3(0, -s.LegLength * 0.25f, 0));
                VisualKit.Part(knee, VisualKit.Box(0.1f, 0.05f, 0.14f), bone, new Vector3(0, -s.LegLength * 0.5f, 0.03f));
                _legs.Add((hip, knee, (front ? 0 : Mathf.Pi) + (side > 0 ? Mathf.Pi * 0.5f : 0), front));
            }
        if (s.Rider)
        {
            // centauro: tronco e cabeça humanoides sobre o corpo
            var torso = VisualKit.Joint(_body, new Vector3(0, s.BodyHeight * 0.4f, L * 0.45f));
            VisualKit.Part(torso, VisualKit.Capsule(0.16f, 0.6f), body, new Vector3(0, 0.3f, 0));
            VisualKit.Part(torso, VisualKit.Sphere(0.14f), bone, new Vector3(0, 0.75f, 0), null, new Vector3(0.85f, 1.15f, 0.95f));
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(torso, VisualKit.Capsule(0.05f, 0.55f), body, new Vector3(side * 0.22f, 0.32f, 0.1f), new Vector3(40, 0, side * 10));
            VisualKit.Part(torso, VisualKit.Cylinder(0.02f, 0.02f, 1.4f, 5), bone, new Vector3(0.28f, 0.4f, 0.3f), new Vector3(70, 0, 0));
            _neck.Visible = false;
        }
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }
    public void Hit(Vector3 fromDirection) => _hitT = 0f;

    public void Update(in VisualFrame f, float delta)
    {
        _time += delta;
        var L = _s.LegLength;
        if (!f.Alive)
        {
            if (_wasAlive) { _deathT = 0f; _fade = 0f; }
            _wasAlive = false;
            _deathT = Mathf.Min(1f, _deathT + delta / 0.6f);
            var k = Mathf.SmoothStep(0f, 1f, _deathT);
            _body.Rotation = new Vector3(0, 0, Mathf.Pi * 0.5f * k);
            _body.Position = new Vector3(0, L + _s.BodyHeight * 0.5f - (L * 0.7f) * k, 0);
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.4f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive) { _wasAlive = true; Root.Visible = true; _body.Rotation = Vector3.Zero; }
        _phase += f.Speed * delta * (5.5f / Mathf.Max(0.6f, _s.Scale));
        var amp = Mathf.Clamp(f.Speed / 3f, 0f, 1.2f);
        var colon = f.Action.IndexOf(':');
        var phase = colon < 0 ? f.Action : f.Action[..colon];
        var t = f.ActionT;
        var rot = new Vector3(0.03f * Mathf.Sin(_phase * 2f) * amp, 0, 0);
        float y = L + _s.BodyHeight * 0.5f + 0.03f * Mathf.Abs(Mathf.Sin(_phase)) * amp, z = 0, jaw = 0.05f, headPitch = 0;
        switch (phase)
        {
            case "windup":
                rot.X = 0.18f * t; y -= 0.15f * t; z = -0.2f * t; jaw = 0.4f * t; headPitch = 0.25f * t;
                if (t > 0.6f) rot.Z = 0.04f * Mathf.Sin(_time * 50f);
                break;
            case "active": rot.X = -0.1f; z = 0.35f; jaw = t < 0.5f ? 0.6f : 0.05f; headPitch = -0.2f; break;
            case "recover": rot.X = 0.12f * (1f - t); y -= 0.08f * (1f - t); rot.Z = 0.06f * Mathf.Sin(_time * 8f) * (1f - t); break;
        }
        if (_hitT < 1f) { _hitT = Mathf.Min(1f, _hitT + delta / 0.25f); rot.X -= 0.18f * VisualKit.Snap(_hitT, 0.2f); }
        _bodyRot = VisualKit.Approach(_bodyRot, rot, phase == "active" ? 28f : 12f, delta);
        _bodyY = VisualKit.Approach(_bodyY == 0 ? y : _bodyY, y, 12f, delta);
        _bodyZ = VisualKit.Approach(_bodyZ, z, phase == "active" ? 25f : 10f, delta);
        _body.Rotation = _bodyRot;
        _body.Position = new Vector3(0, _bodyY, _bodyZ);
        _head.Rotation = new Vector3(headPitch + 0.05f * Mathf.Sin(_time * 1.7f), 0, 0);
        _jaw.Rotation = new Vector3(jaw, 0, 0);
        _tail.Rotation = new Vector3(0, 0.3f * Mathf.Sin(_time * 3f + _phase), 0);
        foreach (var (hip, knee, p, front) in _legs)
        {
            var s = Mathf.Sin(_phase + p);
            hip.Rotation = new Vector3(0.6f * s * amp + (phase == "windup" && front ? -0.3f * t : 0), 0, 0);
            knee.Rotation = new Vector3(-0.5f * Mathf.Max(0, Mathf.Cos(_phase + p)) * amp, 0, 0);
        }
        _flash.Apply(_hitT < 1f ? 1f - _hitT : 0f, f.WindupGlow, new Color(1f, 0.25f, 0.1f));
    }

    public void Dispose() => Root.QueueFree();
}

/// <summary>Aracnídeo/inseto: aranha (8 patas, salto), escorpião (cauda), besouro (carapaça). Preparação: ergue a frente.</summary>
public sealed class InsectVisual : IEntityVisual
{
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly Node3D _body, _tail;
    private readonly List<(Node3D Hip, float Phase, float Side)> _legs = new();
    private readonly float _scale;
    private float _phase, _time, _hitT = 1f, _deathT, _fade;
    private bool _wasAlive = true;
    private Vector3 _rot;

    public Node3D Root { get; } = new() { Name = "Inseto" };
    public float Height { get; }

    public InsectVisual(string kind, float scale, Color body, Color accent)
    {
        _scale = scale;
        Height = 0.9f * scale;
        var s = VisualKit.Joint(Root, Vector3.Zero);
        s.Scale = Vector3.One * scale;
        var m = _flash.Add(VisualKit.Mat(body, 0.7f, 0.1f));
        var a = _flash.Add(VisualKit.Mat(accent, 0.6f));
        var eyes = VisualKit.Glow(new Color(1f, 0.3f, 0.2f), 2.5f);
        _body = VisualKit.Joint(s, new Vector3(0, 0.45f, 0));
        var legs = kind == "aranha" ? 4 : 3;
        if (kind == "besouro")
        {
            VisualKit.Part(_body, VisualKit.Sphere(0.55f), m, Vector3.Zero, null, new Vector3(1f, 0.6f, 1.3f));
            VisualKit.Part(_body, VisualKit.Box(0.06f, 0.3f, 1.3f), a, new Vector3(0, 0.25f, 0));
            VisualKit.Part(_body, VisualKit.Cylinder(0f, 0.12f, 0.5f, 5), a, new Vector3(0, 0.15f, 0.75f), new Vector3(60, 0, 0));
        }
        else
        {
            VisualKit.Part(_body, VisualKit.Sphere(0.32f), m, new Vector3(0, 0, 0.25f), null, new Vector3(1f, 0.7f, 1f));
            VisualKit.Part(_body, VisualKit.Sphere(0.42f), m, new Vector3(0, 0.08f, -0.35f), null, new Vector3(1f, 0.8f, 1.2f));
            if (kind == "aranha") VisualKit.Part(_body, VisualKit.Box(0.06f, 0.02f, 0.4f), a, new Vector3(0, 0.42f, -0.35f));
        }
        foreach (var side in new[] { -1f, 1f })
        {
            VisualKit.Part(_body, VisualKit.Sphere(0.04f), eyes, new Vector3(side * 0.09f, 0.12f, 0.5f));
            VisualKit.Part(_body, VisualKit.Cylinder(0f, 0.04f, 0.2f, 4), a, new Vector3(side * 0.08f, -0.08f, 0.55f), new Vector3(80, 0, 0));
        }
        for (var i = 0; i < legs; i++)
            foreach (var side in new[] { -1f, 1f })
            {
                var hip = VisualKit.Joint(_body, new Vector3(side * 0.25f, 0, 0.3f - i * 0.22f));
                hip.Rotation = new Vector3(0, side * (0.4f - i * 0.25f), 0);
                VisualKit.Part(hip, VisualKit.Capsule(0.035f, 0.5f), m, new Vector3(side * 0.2f, 0.1f, 0), new Vector3(0, 0, side * 60));
                var knee = VisualKit.Joint(hip, new Vector3(side * 0.42f, 0.22f, 0));
                VisualKit.Part(knee, VisualKit.Sphere(0.04f), a, Vector3.Zero);
                VisualKit.Part(knee, VisualKit.Capsule(0.03f, 0.7f), m, new Vector3(side * 0.12f, -0.3f, 0), new Vector3(0, 0, side * 20));
                _legs.Add((hip, i * 1.7f + (side > 0 ? Mathf.Pi : 0), side));
            }
        _tail = VisualKit.Joint(_body, new Vector3(0, 0.1f, -0.7f));
        if (kind == "escorpiao")
        {
            var seg = _tail;
            for (var i = 0; i < 4; i++)
            {
                VisualKit.Part(seg, VisualKit.Sphere(0.12f - i * 0.015f), m, new Vector3(0, 0.12f, -0.05f));
                seg = VisualKit.Joint(seg, new Vector3(0, 0.22f, -0.04f));
                seg.Rotation = new Vector3(-0.5f, 0, 0);
            }
            VisualKit.Part(seg, VisualKit.Cylinder(0f, 0.07f, 0.25f, 5), a, new Vector3(0, 0.05f, 0.12f), new Vector3(70, 0, 0));
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(_body, VisualKit.Box(0.14f, 0.1f, 0.35f), a, new Vector3(side * 0.28f, 0, 0.7f), new Vector3(0, side * -20, 0));
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
            _deathT = Mathf.Min(1f, _deathT + delta / 0.5f);
            _body.Rotation = new Vector3(0, 0, Mathf.Pi * Mathf.SmoothStep(0, 1, _deathT));
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.4f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive) { _wasAlive = true; Root.Visible = true; }
        _phase += f.Speed * delta * 7f / Mathf.Max(0.6f, _scale);
        var amp = Mathf.Clamp(f.Speed / 3f, 0f, 1.2f);
        var colon = f.Action.IndexOf(':');
        var phase = colon < 0 ? f.Action : f.Action[..colon];
        var target = Vector3.Zero;
        var y = 0.45f;
        switch (phase)
        {
            case "windup": target.X = -0.45f * f.ActionT; y = 0.45f + 0.1f * f.ActionT; break;
            case "active": target.X = 0.2f; y = 0.6f; break;
            case "recover": target.X = 0.15f * (1f - f.ActionT); y = 0.38f; break;
        }
        if (_hitT < 1f) { _hitT = Mathf.Min(1f, _hitT + delta / 0.25f); target.X -= 0.2f * VisualKit.Snap(_hitT, 0.2f); }
        _rot = VisualKit.Approach(_rot, target, phase == "active" ? 28f : 12f, delta);
        _body.Rotation = _rot;
        _body.Position = new Vector3(0, y + 0.02f * Mathf.Sin(_phase * 2f) * amp, 0);
        foreach (var (hip, p, side) in _legs)
            hip.Rotation = new Vector3(0, hip.Rotation.Y, 0.25f * Mathf.Max(0, Mathf.Sin(_phase + p)) * amp * side);
        _tail.Rotation = new Vector3(phase == "windup" ? -0.4f * f.ActionT : 0.05f * Mathf.Sin(_time * 2f), 0, 0);
        _flash.Apply(_hitT < 1f ? 1f - _hitT : 0f, f.WindupGlow, new Color(1f, 0.25f, 0.1f));
    }

    public void Dispose() => Root.QueueFree();
}

/// <summary>Voador: morcego, harpia, fênix e pequeno draco (asas que batem, paira acima do chão, mergulha no golpe).</summary>
public sealed class FlyerVisual : IEntityVisual
{
    private readonly VisualKit.FlashGroup _flash = new();
    private readonly Node3D _body, _wingL, _wingR;
    private readonly float _hover;
    private float _time, _hitT = 1f, _deathT, _fade, _y;
    private bool _wasAlive = true;
    private readonly Color _glow;
    private readonly bool _glows;

    public Node3D Root { get; } = new() { Name = "Voador" };
    public float Height { get; }

    public FlyerVisual(string kind, float scale, Color body, Color wing, bool glows = false)
    {
        _hover = kind == "draco" ? 0.9f : 1.3f;
        Height = (_hover + 0.6f) * scale;
        _glow = wing;
        _glows = glows;
        var s = VisualKit.Joint(Root, Vector3.Zero);
        s.Scale = Vector3.One * scale;
        var m = _flash.Add(glows ? VisualKit.Glow(body, 1.2f) : VisualKit.Mat(body, 0.8f));
        var w = _flash.Add(glows ? VisualKit.Glow(wing, 1.6f) : VisualKit.Mat(wing, 0.85f));
        _body = VisualKit.Joint(s, new Vector3(0, _hover, 0));
        VisualKit.Part(_body, VisualKit.Capsule(0.16f, 0.6f), m, Vector3.Zero, new Vector3(80, 0, 0));
        VisualKit.Part(_body, VisualKit.Sphere(0.15f), m, new Vector3(0, 0.12f, 0.32f));
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(_body, VisualKit.Sphere(0.03f), VisualKit.Glow(kind == "fenix" ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.3f, 0.2f), 3f),
                new Vector3(side * 0.06f, 0.16f, 0.44f));
        if (kind is "draco")
        {
            VisualKit.Part(_body, VisualKit.Box(0.14f, 0.1f, 0.22f), m, new Vector3(0, 0.1f, 0.5f));
            foreach (var side in new[] { -1f, 1f })
                VisualKit.Part(_body, VisualKit.Cylinder(0f, 0.04f, 0.18f, 4), _flash.Add(VisualKit.Mat(new Color(0.62f, 0.45f, 0.24f), 0.4f, 0.7f)),
                    new Vector3(side * 0.07f, 0.26f, 0.28f), new Vector3(-40, 0, 0));
            VisualKit.Part(_body, VisualKit.Cylinder(0.02f, 0.06f, 0.6f, 5), m, new Vector3(0, -0.02f, -0.5f), new Vector3(-80, 0, 0));
        }
        if (kind is "harpia")
            VisualKit.Part(_body, VisualKit.Sphere(0.14f), _flash.Add(VisualKit.Mat(new Color(0.75f, 0.68f, 0.58f), 0.6f)), new Vector3(0, 0.2f, 0.36f));
        if (kind is "fenix")
            for (var i = 0; i < 3; i++)
                VisualKit.Part(_body, VisualKit.Box(0.05f, 0.02f, 0.5f), w, new Vector3((i - 1) * 0.08f, 0, -0.5f), new Vector3(0, (i - 1) * 15, 0));
        _wingL = VisualKit.Joint(_body, new Vector3(0.12f, 0.05f, 0.05f));
        _wingR = VisualKit.Joint(_body, new Vector3(-0.12f, 0.05f, 0.05f));
        foreach (var (wingNode, side) in new[] { (_wingL, 1f), (_wingR, -1f) })
        {
            VisualKit.Part(wingNode, VisualKit.Box(0.7f, 0.03f, 0.35f), w, new Vector3(side * 0.35f, 0, 0));
            VisualKit.Part(wingNode, VisualKit.Box(0.4f, 0.03f, 0.25f), w, new Vector3(side * 0.85f, 0, -0.08f), new Vector3(0, side * 20, 0));
            VisualKit.Part(wingNode, VisualKit.Capsule(0.02f, 1.1f), m, new Vector3(side * 0.5f, 0.02f, 0.15f), new Vector3(0, 0, 90));
        }
        if (glows)
            s.AddChild(new OmniLight3D { LightColor = wing, LightEnergy = 1.4f, OmniRange = 4f, Position = new Vector3(0, _hover, 0) });
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
            _body.Position = new Vector3(0, Mathf.Lerp(_hover, 0.15f, Mathf.SmoothStep(0, 1, _deathT)), 0);
            _body.Rotation = new Vector3(0, 0, Mathf.Pi * 0.5f * _deathT);
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.4f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive) { _wasAlive = true; Root.Visible = true; _body.Rotation = Vector3.Zero; }
        var colon = f.Action.IndexOf(':');
        var phase = colon < 0 ? f.Action : f.Action[..colon];
        var flap = Mathf.Sin(_time * (phase == "windup" ? 18f : 11f));
        _wingL.Rotation = new Vector3(0, 0, 0.7f * flap);
        _wingR.Rotation = new Vector3(0, 0, -0.7f * flap);
        var target = _hover + 0.08f * Mathf.Sin(_time * 3f);
        if (phase == "windup") target += 0.4f * f.ActionT;
        if (phase is "active" or "attack") target -= 0.4f;
        _y = VisualKit.Approach(_y == 0 ? target : _y, target, phase == "active" ? 20f : 8f, delta);
        _body.Position = new Vector3(0, _y, phase is "active" or "attack" ? 0.3f : 0);
        _body.Rotation = new Vector3(phase is "active" or "attack" ? 0.4f : 0.05f, 0, 0.05f * Mathf.Sin(_time * 2f));
        if (_hitT < 1f) _hitT = Mathf.Min(1f, _hitT + delta / 0.25f);
        _flash.Apply(_hitT < 1f ? 1f - _hitT : 0f, f.WindupGlow, new Color(1f, 0.25f, 0.1f));
    }

    public void Dispose() => Root.QueueFree();
}

using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// Caranguejo articulado (corpo achatado, 6 patas de 2 segmentos, 2 pinças que abrem, olhos em hastes).
/// Bote: empina e abre as pinças (preparação), avança fechando (golpe) e fica tombado para a frente (recuperação = brecha).
/// </summary>
public sealed class CrabVisual : IEntityVisual
{
    private sealed class Leg
    {
        public Node3D Hip = null!, Knee = null!;
        public float Side, Offset;
    }

    private readonly VisualKit.FlashGroup _flash = new();
    private readonly Node3D _body, _clawR, _clawL, _pincerR, _pincerL;
    private readonly List<Leg> _legs = new();
    private readonly bool _golden;
    private float _phase, _time, _hitT = 1f, _deathT, _fade;
    private float _yaw, _strikeAt;
    private int _strikeSeq = -1;
    private string _strikePhase = "";
    private bool _wasAlive = true;
    private Vector3 _bodyRot;
    private float _bodyY;

    public Node3D Root { get; } = new() { Name = "Caranguejo" };
    public float Height { get; }

    public CrabVisual(float scale, bool golden, Color? tint = null)
    {
        _golden = golden;
        Height = 0.95f * scale;
        var scaleRoot = VisualKit.Joint(Root, Vector3.Zero);
        scaleRoot.Scale = Vector3.One * scale;
        // pacote visual: carapaça cor de osso com borda de espinhos, pernas escuras e uma pinça maior com interior bordô
        var shellColor = golden ? new Color(0.95f, 0.75f, 0.25f) : tint ?? new Color(0.76f, 0.71f, 0.62f);
        var shell = _flash.Add(golden ? VisualKit.Hide(shellColor, "quitina", 0.25f, 2.2f, 0.85f) : VisualKit.Hide(shellColor, "quitina", 0.6f));
        var dark = _flash.Add(VisualKit.Hide(golden ? new Color(0.6f, 0.4f, 0.1f) : (tint?.Darkened(0.35f) ?? new Color(0.55f, 0.50f, 0.44f)), "quitina", 0.7f, 3f, golden ? 0.6f : 0f));
        var tip = _flash.Add(VisualKit.Mat(new Color(0.25f, 0.18f, 0.15f), 0.6f));
        var inner = _flash.Add(VisualKit.Mat(new Color(0.42f, 0.18f, 0.22f), 0.7f));

        _body = VisualKit.Joint(scaleRoot, new Vector3(0, 0.45f, 0), "Carapaca");
        VisualKit.Part(_body, VisualKit.Sphere(0.5f), shell, Vector3.Zero, null, new Vector3(1.25f, 0.55f, 0.95f));
        // carapaça em placas (prancha 04): crista central e duas placas laterais angulosas
        VisualKit.Part(_body, VisualKit.Box(0.16f, 0.10f, 0.62f), shell, new Vector3(0, 0.24f, -0.02f), new Vector3(0, 0, 45), new Vector3(0.8f, 0.8f, 1f));
        foreach (var side in new[] { -1f, 1f })
            VisualKit.Part(_body, VisualKit.Box(0.34f, 0.06f, 0.5f), dark, new Vector3(side * 0.28f, 0.17f, -0.02f), new Vector3(0, 0, side * -22));
        VisualKit.Part(_body, VisualKit.Sphere(0.32f), dark, new Vector3(0, -0.08f, 0), null, new Vector3(1.4f, 0.5f, 1.1f));
        for (var i = -2; i <= 2; i++)
            VisualKit.Part(_body, VisualKit.Cylinder(0.02f, 0.06f, 0.12f, 6), dark, new Vector3(i * 0.17f, 0.2f, 0.05f - Mathf.Abs(i) * 0.04f));
        // espinhos na borda da carapaça (silhueta ameaçadora)
        for (var i = 0; i < 12; i++)
        {
            var a = i / 12f * Mathf.Tau;
            if (Mathf.Abs(Mathf.Sin(a)) > 0.9f && Mathf.Cos(a) > 0) continue;
            VisualKit.Part(_body, VisualKit.Cylinder(0f, 0.05f, 0.16f, 4), shell, new Vector3(Mathf.Sin(a) * 0.6f, 0.02f, Mathf.Cos(a) * 0.45f),
                new Vector3(Mathf.Cos(a) * 80f, 0, -Mathf.Sin(a) * 80f));
        }
        // olhos em hastes (mostram a frente)
        foreach (var side in new[] { -1f, 1f })
        {
            VisualKit.Part(_body, VisualKit.Cylinder(0.02f, 0.025f, 0.22f, 6), dark, new Vector3(side * 0.12f, 0.24f, 0.36f), new Vector3(15, 0, 0));
            VisualKit.Part(_body, VisualKit.Sphere(0.05f), _flash.Add(VisualKit.Glow(golden ? new Color(1f, 0.9f, 0.4f) : new Color(0.15f, 0.05f, 0.05f), golden ? 2f : 0.3f)),
                new Vector3(side * 0.12f, 0.36f, 0.4f));
        }

        // patas
        for (var i = 0; i < 3; i++)
            foreach (var side in new[] { -1f, 1f })
            {
                var hip = VisualKit.Joint(_body, new Vector3(side * 0.5f, -0.02f, 0.2f - i * 0.22f));
                hip.Rotation = new Vector3(0, side * (0.25f - i * 0.25f), 0);
                VisualKit.Part(hip, VisualKit.Cylinder(0.035f, 0.05f, 0.42f, 5), dark, new Vector3(side * 0.18f, 0.06f, 0), new Vector3(0, 0, side * 70));
                var knee = VisualKit.Joint(hip, new Vector3(side * 0.36f, 0.13f, 0));
                VisualKit.Part(knee, VisualKit.Sphere(0.05f), shell, Vector3.Zero);
                VisualKit.Part(knee, VisualKit.Cylinder(0.042f, 0.022f, 0.5f, 5), shell, new Vector3(side * 0.09f, -0.22f, 0), new Vector3(0, 0, side * 22));
                VisualKit.Part(knee, VisualKit.Cylinder(0.022f, 0f, 0.14f, 4), tip, new Vector3(side * 0.19f, -0.51f, 0), new Vector3(0, 0, side * 22));
                _legs.Add(new Leg { Hip = hip, Knee = knee, Side = side, Offset = i * 2.1f + (side > 0 ? Mathf.Pi : 0) });
            }

        // pinças
        (_clawR, _pincerR) = Claw(-1f, shell, dark, tip, inner, 1.55f);
        (_clawL, _pincerL) = Claw(1f, shell, dark, tip, inner, 0.9f);

        if (golden)
        {
            var light = new OmniLight3D { LightColor = new Color(1f, 0.8f, 0.3f), LightEnergy = 1.6f, OmniRange = 4f, Position = new Vector3(0, 0.8f, 0) };
            scaleRoot.AddChild(light);
        }
    }

    private (Node3D Arm, Node3D Pincer) Claw(float side, Material shell, Material dark, Material tip, Material inner, float size)
    {
        var arm = VisualKit.Joint(_body, new Vector3(side * 0.32f, 0.02f, 0.38f));
        VisualKit.Part(arm, VisualKit.Capsule(0.06f, 0.36f), dark, new Vector3(0, 0, 0.16f), new Vector3(90, 0, 0));
        var hand = VisualKit.Joint(arm, new Vector3(0, 0.02f, 0.34f));
        hand.Scale = Vector3.One * size;
        VisualKit.Part(hand, VisualKit.Box(0.05f, 0.08f, 0.2f), inner, new Vector3(0, 0.02f, 0.22f));
        VisualKit.Part(hand, VisualKit.Sphere(0.15f), shell, new Vector3(0, 0, 0.1f), null, new Vector3(0.8f, 0.7f, 1.3f));
        // dedo fixo em crescente (três lâminas curvando para cima) e dedo móvel com interior bordô
        for (var i = 0; i < 3; i++)
            VisualKit.Part(hand, VisualKit.Box(0.085f, 0.06f, 0.13f), i == 2 ? tip : shell,
                new Vector3(side * 0.03f, -0.05f + i * i * 0.025f, 0.24f + i * 0.1f), new Vector3(-i * 22f, 0, 0));
        var pincer = VisualKit.Joint(hand, new Vector3(-side * 0.02f, 0.06f, 0.2f));
        VisualKit.Part(pincer, VisualKit.Box(0.075f, 0.05f, 0.2f), shell, new Vector3(0, 0, 0.09f));
        VisualKit.Part(pincer, VisualKit.Box(0.05f, 0.03f, 0.18f), inner, new Vector3(0, -0.03f, 0.09f));
        VisualKit.Part(pincer, VisualKit.Cylinder(0.03f, 0f, 0.12f, 4), tip, new Vector3(0, -0.01f, 0.24f), new Vector3(90, 0, 0));
        return (arm, pincer);
    }

    public void SetGear(string? weapon, string? offhand, string? body) { }

    public void Hit(Vector3 fromDirection) => _hitT = 0f;

    private string _strikePhase2 = "";

    public void Update(in VisualFrame f, float delta)
    {
        _time += delta;
        if (!f.Alive)
        {
            if (_wasAlive) { _deathT = 0f; _fade = 0f; }
            _wasAlive = false;
            _deathT = Mathf.Min(1f, _deathT + delta / 0.5f);
            var k = Mathf.SmoothStep(0f, 1f, _deathT);
            _body.Rotation = new Vector3(0, 0, Mathf.Pi * k);         // vira de barriga para cima
            _body.Position = new Vector3(0, 0.45f + 0.25f * Mathf.Sin(Mathf.Pi * k) - 0.15f * k, 0);
            foreach (var leg in _legs) leg.Knee.Rotation = new Vector3(0, 0, leg.Side * (0.4f + 0.3f * Mathf.Sin(_time * 25f) * (1f - _fade)));
            if (_deathT >= 1f) _fade = Mathf.Min(1f, _fade + delta / 1.4f);
            _flash.Apply(0f, 0f, Colors.Red, _fade);
            Root.Visible = _fade < 0.99f;
            return;
        }
        if (!_wasAlive)
        {
            _wasAlive = true;
            Root.Visible = true;
        }

        _phase += f.Speed * delta * 5.5f;
        var amp = Mathf.Clamp(f.Speed / 3f, 0f, 1.2f);
        var targetRot = new Vector3(0, 0, 0.04f * Mathf.Sin(_phase) * amp);
        var targetY = 0.45f + 0.03f * Mathf.Abs(Mathf.Sin(_phase)) * amp + 0.01f * Mathf.Sin(_time * 3f);
        float clawRaise = 0f, clawOpen = 0.15f + 0.1f * Mathf.Sin(_time * 2.5f), clawForward = 0f;

        var colon = f.Action.IndexOf(':');
        var phase = colon < 0 ? f.Action : f.Action[..colon];
        var atkId = colon < 0 ? "" : f.Action[(colon + 1)..];
        var t = f.ActionT;
        if (f.ActionSeq != _strikeSeq || (phase == "active" && _strikePhase != "active")) _strikeAt = _time;
        _strikeSeq = f.ActionSeq;
        _strikePhase = phase;
        // caranguejo anda de lado; na pinçada lateral vira o flanco e golpeia com uma pinça só
        var sideways = phase is "chase" or "flee" or "return" || (phase == "" && f.Speed > 0.6f) ? 1.3f
            : atkId is "pinca_lateral" && phase is "windup" or "active" ? 0.85f : 0f;
        _yaw = VisualKit.Approach(_yaw, sideways, 6f, delta);
        if (phase == "active") t = Mathf.Min(1f, (_time - _strikeAt) / 0.25f);
        // golpes com corpo (feedback: "o caranguejo deveria bater a pinça no chão ou dar um bote jogando o corpo"):
        // - bote/investida: agacha e recua, depois se joga para a frente com as duas pinças à frente;
        // - pancada (beliscão, pinçada, martelo...): empina e ergue as pinças bem alto, e BATE no chão de uma vez;
        // - areia/conchas (à distância): cava e joga para a frente.
        var dash = atkId is "bote" or "investida";
        var ranged = atkId.Contains("areia") || atkId.Contains("conchas");
        var recoverT = phase == "recover" ? Mathf.Min(1f, (_time - _strikeAt) / 0.6f) : 0f;
        if (phase == "recover" && _strikePhase2 != "recover") { _strikeAt = _time; recoverT = 0f; }
        _strikePhase2 = phase;
        switch (phase)
        {
            case "windup":
            {
                var k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.4f));
                if (dash)
                {
                    targetRot.X = 0.2f * k;               // abaixa a frente, pronto para se jogar
                    targetY -= 0.2f * k;
                    clawRaise = 0.2f * k;
                    clawForward = 0.5f * k;
                    clawOpen = 0.6f * k;
                }
                else if (ranged)
                {
                    targetRot.X = 0.3f * k;               // cava a areia
                    clawRaise = -0.7f * k;
                    targetRot.Z += 0.06f * Mathf.Sin(_time * 18f) * k;
                }
                else
                {
                    targetRot.X = -0.6f * k;              // empina bem
                    targetY += 0.08f * k;
                    clawRaise = 1.5f * k;                 // pinças lá no alto
                    clawOpen = 0.9f * k;
                }
                if (t > 0.7f) targetRot.Z += 0.05f * Mathf.Sin(_time * 55f);   // tremidinha antes de soltar
                break;
            }
            case "active":
            {
                var hit = Mathf.Min(1f, t / 0.35f);       // o golpe chega no começo; o resto é o peso parado
                if (dash)
                {
                    targetRot.X = 0.45f;                  // corpo inteiro jogado para a frente
                    targetY -= 0.05f;
                    clawRaise = 0.25f;
                    clawForward = 1.1f;
                    clawOpen = hit < 0.6f ? 0.9f : 0.1f;  // fecha no alvo
                }
                else if (ranged)
                {
                    targetRot.X = -0.25f;
                    clawRaise = 0.9f * hit;
                    clawForward = 0.6f;
                }
                else
                {
                    targetRot.X = Mathf.Lerp(-0.6f, 0.5f, hit);    // tomba para a frente junto
                    targetY -= 0.16f * hit;
                    clawRaise = Mathf.Lerp(1.5f, -0.85f, hit);     // BATE no chão
                    clawOpen = 0.1f;
                    if (hit >= 1f) targetRot.Z += 0.04f * Mathf.Sin(_time * 40f) * (1f - t);   // tremor do impacto
                }
                break;
            }
            case "recover":
            {
                // fica com as pinças no chão um instante (a brecha) e levanta devagar
                var r = recoverT < 0.55f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (recoverT - 0.55f) / 0.45f);
                targetRot.X = (dash ? 0.4f : 0.45f) * r;
                targetY -= 0.18f * r;
                clawRaise = (dash ? 0.1f : -0.8f) * r;
                clawForward = dash ? 0.8f * r : 0f;
                clawOpen = 0.05f;
                targetRot.Z += 0.03f * Mathf.Sin(_time * 9f) * r;
                break;
            }
            case "flee":
                targetRot.X = -0.1f;
                break;
            case "sleep":
                // dormindo: barriga no chão, pinças recolhidas, respira devagar
                targetY -= 0.28f;
                targetRot.X = 0.08f;
                clawRaise = -0.6f;
                clawOpen = 0f;
                targetY += 0.015f * Mathf.Sin(_time * 1.6f);
                break;
        }
        if (_hitT < 1f)
        {
            _hitT = Mathf.Min(1f, _hitT + delta / 0.25f);
            targetRot.X -= 0.25f * VisualKit.Snap(_hitT, 0.2f);
        }

        var rate = phase is "active" ? 38f : phase is "windup" ? 9f : 12f;   // prepara devagar, golpe rápido, volta com peso
        _bodyRot = VisualKit.Approach(_bodyRot, targetRot, rate, delta);
        _bodyY = VisualKit.Approach(_bodyY == 0 ? targetY : _bodyY, targetY, rate, delta);
        _body.Rotation = _bodyRot + new Vector3(0, _yaw, 0);
        _body.Position = new Vector3(0, _bodyY, 0);

        foreach (var leg in _legs)
        {
            var s = Mathf.Sin(_phase + leg.Offset);
            leg.Hip.Rotation = new Vector3(0, leg.Side * (0.25f - 0.25f * (_legs.IndexOf(leg) / 2)) + 0.35f * s * amp, 0.25f * Mathf.Max(0f, Mathf.Cos(_phase + leg.Offset)) * amp * leg.Side);
            leg.Knee.Rotation = new Vector3(0, 0, -leg.Side * (phase == "recover" ? 0.35f : 0f));
        }

        foreach (var (arm, pincer, side) in new[] { (_clawR, _pincerR, -1f), (_clawL, _pincerL, 1f) })
        {
            arm.Rotation = new Vector3(-clawRaise + 0.15f * Mathf.Sin(_phase) * amp, side * (0.35f - clawForward * 0.35f), 0);
            pincer.Rotation = new Vector3(-clawOpen, 0, 0);
        }

        var flash = _hitT < 1f ? 1f - _hitT : 0f;
        _flash.Apply(flash, f.WindupGlow, new Color(1f, 0.25f, 0.1f));
    }

    public void Dispose() => Root.QueueFree();
}

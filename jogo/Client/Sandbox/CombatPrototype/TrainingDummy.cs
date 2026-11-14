using CinzasDaMare.Client.Sandbox.Core;
using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client.Sandbox.CombatPrototype;

/// <summary>
/// Boneco de treino do laboratório (só local): recebe golpes pela <see cref="Hurtbox"/>, é empurrado (knockback),
/// pisca e mostra o dano; volta sozinho ao ponto de origem. Opcionalmente ataca a cada ~2,6 s com um golpe
/// telegrafado (anel no chão que fica vermelho) — serve para calibrar esquiva, i-frames e reação a golpe.
/// </summary>
public sealed partial class TrainingDummy : CharacterBody3D
{
    private const double SwingEvery = 2.6, Windup = 0.7, Active = 0.12;
    private readonly SandboxSettings _settings;
    private readonly Hurtbox _hurt;
    private readonly Hitbox _hit;
    private readonly Node3D _body;
    private readonly MeshInstance3D _ring;
    private readonly StandardMaterial3D _bodyMat, _ringMat;
    private readonly Vector3 _home;
    private Vector3 _push;
    private double _flash, _t;
    private bool _swingOpen;
    private float _hp = 100f;
    private Node3D? _target;
    // 12/11: modo "mob" (espécie real do jogo): visual do monstro, persegue, telegrafa e ataca
    private readonly IEntityVisual? _mob;
    private readonly bool _boss;
    private float _speed;
    private string _act = "";
    private float _actT;
    private int _seq;
    private double _cycle = -1;

    public TrainingDummy(SandboxSettings settings, Vector3 home, double phase)
    {
        Name = "BonecoDeTreino";
        _settings = settings;
        _home = home;
        _t = phase;
        CollisionLayer = 0;
        CollisionMask = 1;
        MotionMode = MotionModeEnum.Floating;
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.6f }, Position = new Vector3(0, 0.8f, 0) });
        _body = new Node3D();
        AddChild(_body);
        _bodyMat = VisualKit.Mat(new Color(0.55f, 0.42f, 0.28f), 0.9f);
        _bodyMat.EmissionEnabled = true;
        _bodyMat.Emission = Colors.Black;
        VisualKit.Part(_body, VisualKit.Cylinder(0.06f, 0.06f, 0.9f, 8), _bodyMat, new Vector3(0, 0.45f, 0));
        VisualKit.Part(_body, VisualKit.Cylinder(0.32f, 0.36f, 0.75f, 12), _bodyMat, new Vector3(0, 1.15f, 0));
        VisualKit.Part(_body, VisualKit.Sphere(0.2f), _bodyMat, new Vector3(0, 1.72f, 0));
        VisualKit.Part(_body, VisualKit.Box(1.0f, 0.08f, 0.08f), _bodyMat, new Vector3(0, 1.35f, 0));
        VisualKit.Part(_body, VisualKit.Cylinder(0.4f, 0.45f, 0.08f, 16), VisualKit.Mat(new Color(0.35f, 0.3f, 0.26f), 0.9f), new Vector3(0, 0.04f, 0));
        _ringMat = VisualKit.Unshaded(new Color(1f, 0.6f, 0.5f, 0f));
        _ringMat.NoDepthTest = true;
        _ring = VisualKit.Part(this, VisualKit.Sector(2.2f, 360f, 40, 0.3f), _ringMat, new Vector3(0, 0.05f, 0));
        _hurt = new Hurtbox(Team.Enemy, 0.45f, 1.8f);
        AddChild(_hurt);
        _hurt.Hit += OnHit;
        _hit = new Hitbox(Team.Enemy, new SphereShape3D { Radius = 2.2f }, new Vector3(0, 0.9f, 0));
        AddChild(_hit);
        Position = home;
        if (settings.DummySpecies.Length > 0)
        {
            _boss = settings.DummyBoss;
            _mob = VisualFactory.Monster(settings.DummySpecies, _boss, false, settings.DummyVisual, settings.DummySpecies + phase);
            _body.Visible = false;
            AddChild(_mob.Root);
            _hp = _boss ? 600f : 160f;
            var nome = new Label3D { Text = settings.DummyName, FontSize = 36, OutlineSize = 8, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0, _mob.Height + 0.4f, 0), NoDepthTest = true, Modulate = new Color(1f, 0.85f, 0.6f) };
            AddChild(nome);
        }
    }

    public void SetTarget(Node3D? target) => _target = target;

    public void Tick(double dt)
    {
        if (_mob is not null) { TickMob(dt); return; }
        // empurrão que freia + volta ao ponto
        _push = _push.Lerp(Vector3.Zero, 1f - Mathf.Exp(-5f * (float)dt));
        var back = (_home - GlobalPosition) * 1.5f;
        back.Y = 0;
        Velocity = _push + back;
        MoveAndSlide();
        _flash = Mathf.Max(0, _flash - dt);
        _bodyMat.Emission = new Color(1f, 0.9f, 0.7f) * (float)(_flash / 0.12 * 1.2);
        _body.Rotation = new Vector3(Mathf.Clamp(-_push.Length() * 0.05f, -0.5f, 0), 0, 0);

        if (!_settings.DummiesAttack) { _ringMat.AlbedoColor = new Color(1f, 0.6f, 0.5f, 0f); if (_swingOpen) { _hit.Close(); _swingOpen = false; } return; }
        _t += dt;
        var c = _t % SwingEvery;
        var start = SwingEvery - Windup - Active;
        if (c >= start && c < start + Windup)
        {
            // telegrafia: começa clara e fica vermelha perto do golpe (mesma leitura do jogo oficial)
            var k = (float)((c - start) / Windup);
            _ringMat.AlbedoColor = new Color(1f, 0.75f - 0.6f * k, 0.6f - 0.5f * k, 0.15f + 0.45f * k);
        }
        else if (c >= start + Windup)
        {
            _ringMat.AlbedoColor = new Color(1f, 0.1f, 0.05f, 0.75f);
            if (!_swingOpen) { _swingOpen = true; _hit.Open(new HitInfo(this, GlobalPosition, 8, 6f, 1f, "boneco")); }
            _hit.SweepOverlaps();
        }
        else
        {
            _ringMat.AlbedoColor = new Color(1f, 0.6f, 0.5f, 0f);
            if (_swingOpen) { _hit.Close(); _swingOpen = false; }
        }
    }

    /// <summary>
    /// Mob v2: vê o herói a 14 m e corre até ficar a ~1,8 m; então prepara (anel clareando → vermelho, 0,7 s), golpeia (0,12 s)
    /// e se recupera (0,6 s) — durante preparação e recuperação não gira nem anda (é a janela para punir ou esquivar).
    /// Longe de casa (16 m) desiste e volta. Golpe recebido empurra e interrompe a corrida (não a preparação).
    /// </summary>
    private void TickMob(double dt)
    {
        _push = _push.Lerp(Vector3.Zero, 1f - Mathf.Exp(-5f * (float)dt));
        _flash = Mathf.Max(0, _flash - dt);
        var me = GlobalPosition;
        var alvo = _target is { } t && IsInstanceValid(t) && t.IsVisibleInTree() ? t.GlobalPosition : (Vector3?)null;
        var paraCasa = _home - me; paraCasa.Y = 0;
        var dir = Vector3.Zero;
        var dist = 99f;
        if (alvo is { } a) { var d = a - me; d.Y = 0; dist = d.Length(); dir = dist > 0.01f ? d / dist : Vector3.Zero; }
        var ataca = _settings.DummiesAttack;
        var vel = Vector3.Zero;
        if (_cycle >= 0)
        {
            _cycle += dt;
            if (_cycle < Windup)
            {
                var k = (float)(_cycle / Windup);
                _act = "windup:mob"; _actT = k;
                _ringMat.AlbedoColor = new Color(1f, 0.75f - 0.6f * k, 0.6f - 0.5f * k, 0.15f + 0.45f * k);
            }
            else if (_cycle < Windup + Active)
            {
                _act = "active:mob"; _actT = (float)((_cycle - Windup) / Active);
                _ringMat.AlbedoColor = new Color(1f, 0.1f, 0.05f, 0.75f);
                if (!_swingOpen) { _swingOpen = true; _hit.Open(new HitInfo(this, GlobalPosition, _boss ? 18 : 8, _boss ? 9f : 6f, 1f, "mob")); }
                _hit.SweepOverlaps();
            }
            else if (_cycle < Windup + Active + 0.6)
            {
                if (_swingOpen) { _hit.Close(); _swingOpen = false; }
                _act = "recover:mob"; _actT = (float)((_cycle - Windup - Active) / 0.6);
                _ringMat.AlbedoColor = new Color(1f, 0.6f, 0.5f, 0f);
            }
            else { _cycle = -1; _act = ""; }
        }
        else if (ataca && alvo is not null && dist < 14f && paraCasa.Length() < 16f)
        {
            if (dist > 1.8f) { vel = dir * (_boss ? 2.6f : 3.4f); _act = "chase"; }
            else { _cycle = 0; _seq++; }
            if (dir != Vector3.Zero) Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(dir.X, dir.Z), 1f - Mathf.Exp(-10f * (float)dt)), 0);
        }
        else if (paraCasa.Length() > 0.4f)
        {
            vel = paraCasa.Normalized() * 2.5f;
            _act = "";
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(paraCasa.X, paraCasa.Z), 1f - Mathf.Exp(-8f * (float)dt)), 0);
        }
        Velocity = vel + _push;
        MoveAndSlide();
        _speed = Mathf.Lerp(_speed, vel.Length(), 1f - Mathf.Exp(-12f * (float)dt));
        var frame = new VisualFrame { Speed = _speed, Action = _act, ActionT = _actT, ActionSeq = _seq, Alive = true, WindupGlow = _act.StartsWith("windup") ? _actT : 0f };
        _mob!.Update(frame, (float)dt);
    }

    private void OnHit(HitInfo hit)
    {
        var away = GlobalPosition - hit.Origin;
        away.Y = 0;
        away = away.LengthSquared() > 1e-4f ? away.Normalized() : Vector3.Forward;
        _push = away * hit.Knockback;
        _flash = 0.12;
        _mob?.Hit(-away);
        _hp -= hit.Damage;
        if (_hp <= 0) _hp = _mob is null ? 100f : _boss ? 600f : 160f;   // boneco não morre: o número volta
        Popup($"-{hit.Damage:0}", hit.Knockback > 6f);
    }

    private void Popup(string text, bool big)
    {
        var label = new Label3D
        {
            Text = text, FontSize = big ? 64 : 44, OutlineSize = 10, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = big ? new Color(1f, 0.75f, 0.3f) : Colors.White, Position = new Vector3(0, 2.1f, 0), NoDepthTest = true,
        };
        AddChild(label);
        var tw = label.CreateTween();
        tw.TweenProperty(label, "position", new Vector3(GD.Randf() - 0.5f, 2.9f, 0), 0.6);
        tw.Parallel().TweenProperty(label, "modulate:a", 0f, 0.6).SetDelay(0.2);
        tw.TweenCallback(Callable.From(label.QueueFree));
    }
}

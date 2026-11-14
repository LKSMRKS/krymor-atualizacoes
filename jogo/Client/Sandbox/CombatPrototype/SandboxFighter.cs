using System.Collections.Generic;
using CinzasDaMare.Client.Controllers;
using CinzasDaMare.Client.Sandbox.Core;
using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client.Sandbox.CombatPrototype;

/// <summary>
/// Combate &amp; Movimentação v2 (Laboratório). Corpo físico LOCAL (CharacterBody3D) que toma o boneco do jogador
/// emprestado enquanto o laboratório está ligado. Nada aqui fala com o servidor.
///
/// Peças: máquina de estados (<see cref="CombatState"/>), buffer de comandos (<see cref="InputBuffer"/>),
/// hitbox/hurtbox por sinais (<see cref="Hitbox"/>/<see cref="Hurtbox"/>), hitstop (<see cref="Hitstop"/>).
/// Quadro: <see cref="ProcessFrame"/> lê a entrada contínua e desenha; <see cref="PhysicsFrame"/> move (MoveAndSlide).
/// </summary>
public sealed partial class SandboxFighter : CharacterBody3D, IPlayerController
{
    public const float WalkSpeed = 5.0f, RunSpeed = 7.25f, Accel = 16f;
    private const uint WallMask = 1;   // paredes/colisores estáticos do mapa (mesma camada que a câmera testa)

    public SandboxSettings Settings { get; }
    public Hurtbox Hurtbox { get; }
    public Vector3 WishDir { get; private set; }
    public bool Sprint { get; private set; }
    public string AttackVisualId { get; private set; } = "skill_guerreiro_corte";
    public Vector3 Forward => new(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
    public StateId State => _state.Id;
    public InputBuffer Buffer { get; } = new();
    /// <summary>Golpe acertou alguém (o manager usa para tremer a câmera e mostrar número).</summary>
    public event System.Action<Hurtbox, HitInfo>? Landed;
    public event System.Action<HitInfo>? WasHit;

    private readonly Dictionary<StateId, CombatState> _states = new();
    private CombatState _state;
    private readonly Hitbox _hitbox;
    private IEntityVisual? _visual;
    private ThirdPersonCamera? _cam;
    private float _yaw;            // visual: atan2(x, z), igual ao Main
    private float _speedShown;
    private bool _active;
    private int _visualSeq;
    private StateId _lastVisualState;
    private int _lastAttackSeq;

    public SandboxFighter(SandboxSettings settings)
    {
        Name = "LutadorSandbox";
        Settings = settings;
        CollisionLayer = 0;            // ninguém colide com o protótipo (não empurra o mundo oficial)
        CollisionMask = WallMask;
        MotionMode = MotionModeEnum.Floating;   // plano do mapa; a altura do chão é posta à mão (como o oficial)
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.7f }, Position = new Vector3(0, 0.85f, 0) });
        Hurtbox = new Hurtbox(Team.Player, 0.45f, 1.7f);
        AddChild(Hurtbox);
        Hurtbox.Hit += OnHurt;
        _hitbox = new Hitbox(Team.Player, new SphereShape3D { Radius = 0.95f }, new Vector3(0, 1.0f, 1.15f));
        AddChild(_hitbox);
        _hitbox.Landed += (h, i) => { Hitstop.Trigger(Settings.HitstopScale, Settings.HitstopMs * i.HitstopWeight); Landed?.Invoke(h, i); };
        foreach (var s in new CombatState[] { new IdleState(this), new MoveState(this), new AttackState(this), new DodgeState(this), new HitReactState(this) })
            _states[s.Id] = s;
        _state = _states[StateId.Idle];
        SetProcess(false);
        SetPhysicsProcess(false);   // quem chama o quadro é o manager (ordem controlada)
    }

    // ------------------------------------------------------------ IPlayerController

    public string Id => "sandbox:combate_v2";
    public bool IsActive => _active;
    public new Vector3 Position => GlobalPosition;
    public new Vector3 Velocity { get => base.Velocity; set => base.Velocity = value; }
    public float FacingYaw => _yaw;

    public void Activate(in PlayerHandoff h)
    {
        _visual = h.Visual;
        _cam = h.Camera;
        if (h.BasicSkillId is { Length: > 0 } sk) AttackVisualId = sk;
        GlobalPosition = new Vector3(h.Position.X, 0, h.Position.Z);
        base.Velocity = new Vector3(h.Velocity.X, 0, h.Velocity.Z);   // momentum entra no protótipo
        // o yaw do visual vem da direção em que o boneco olhava
        _yaw = h.FacingYaw;
        Buffer.Clear();
        ChangeState(base.Velocity.LengthSquared() > 0.25f ? StateId.Move : StateId.Idle);
        _active = true;
    }

    public PlayerHandoff Deactivate()
    {
        _active = false;
        _hitbox.Close();
        Hitstop.Cancel();
        return new PlayerHandoff(GlobalPosition, base.Velocity, _yaw, _visual, _cam, AttackVisualId);
    }

    public void RequestMove(Vector2 direction, bool sprint)
    {
        WishDir = new Vector3(direction.X, 0, direction.Y);
        Sprint = sprint;
    }

    public void RequestAttack(string slot) => Buffer.Push("attack", Time.GetTicksMsec());
    public void RequestDodge(Vector2 direction)
    {
        if (direction.LengthSquared() > 0.01f) WishDir = new Vector3(direction.X, 0, direction.Y);
        Buffer.Push("dodge", Time.GetTicksMsec());
    }

    public bool HandleInput(InputEvent e)
    {
        if (!_active) return false;
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && Input.MouseMode == Input.MouseModeEnum.Captured)
        { RequestAttack("basic"); return true; }
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            switch (k.Keycode)
            {
                case Key.Space or Key.Ctrl: RequestDodge(Vector2.Zero); return true;
                case Key.Q or Key.E or Key.R or Key.T: return true;   // habilidades oficiais não disparam no servidor durante o teste
            }
        }
        return false;
    }

    public void ProcessFrame(double delta)
    {
        if (!_active) return;
        // entrada contínua (WASD relativo à câmera; Shift corre) — mesma convenção do oficial
        var yaw = _cam?.Yaw ?? 0f;
        var typing = GetViewport().GuiGetFocusOwner() is LineEdit;
        var up = !typing && (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up));
        var down = !typing && (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down));
        var left = !typing && (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left));
        var right = !typing && (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right));
        var (x, y) = Shared.Client.CameraMove.Direction(up, down, left, right, yaw);
        RequestMove(new Vector2((float)x, (float)y), !typing && Input.IsKeyPressed(Key.Shift));
        if (Input.MouseMode == Input.MouseModeEnum.Captured && Input.IsMouseButtonPressed(MouseButton.Left) && Buffer.Count == 0 && _state.Id != StateId.Attack)
            RequestAttack("basic");   // segurar o botão continua o combo
        DriveVisual((float)delta);
    }

    public void PhysicsFrame(double delta)
    {
        if (!_active) return;
        Buffer.Expire(Time.GetTicksMsec(), Settings.InputBufferMs);
        var next = _state.Tick(delta);
        if (next is { } n && n != _state.Id) ChangeState(n);
        MoveAndSlide();
        var p = GlobalPosition;
        GlobalPosition = new Vector3(p.X, 0, p.Z);
        _hitbox.SweepOverlaps();
    }

    // ------------------------------------------------------------ usados pelos estados

    public bool ConsumeBuffered(string action) => Buffer.TryConsume(Time.GetTicksMsec(), Settings.InputBufferMs, out _, action);

    /// <summary>Aproxima a velocidade da desejada (aceleração exponencial, independente do FPS).</summary>
    public void Steer(Vector3 target, double dt, float rate = Accel)
        => base.Velocity = base.Velocity.Lerp(target, 1f - Mathf.Exp(-rate * (float)dt));

    public void FaceTowards(Vector3 dir, double dt, float rate)
        => _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(dir.X, dir.Z), 1f - Mathf.Exp(-rate * (float)dt));

    public void SetFacingInstant(Vector3 dir) { if (dir.LengthSquared() > 1e-4f) _yaw = Mathf.Atan2(dir.X, dir.Z); }

    /// <summary>Golpe vira para onde o jogador quer ir; parado, para a frente da câmera (como o oficial).</summary>
    public void SnapFacingToAim()
    {
        if (WishDir.LengthSquared() > 0.01f) { SetFacingInstant(WishDir); return; }
        if (_cam is null) return;
        var (fx, fy) = Shared.Client.CameraMove.Forward(_cam.Yaw);
        SetFacingInstant(new Vector3((float)fx, 0, (float)fy));
    }

    public void OpenHitbox(float damage, float knockback, float hitstopWeight, string id)
    {
        _hitbox.Rotation = new Vector3(0, _yaw, 0);
        _hitbox.Open(new HitInfo(this, GlobalPosition, damage, knockback, hitstopWeight, id));
    }

    public void CloseHitbox() => _hitbox.Close();

    private void OnHurt(HitInfo hit)
    {
        if (!_active || _state.Id == StateId.Dodge) return;
        var away = GlobalPosition - hit.Origin;
        away.Y = 0;
        away = away.LengthSquared() > 1e-4f ? away.Normalized() : -Forward;
        base.Velocity = away * hit.Knockback;
        _visual?.Hit(-away);
        Hitstop.Trigger(Settings.HitstopScale, Settings.HitstopMs * hit.HitstopWeight);
        WasHit?.Invoke(hit);
        ChangeState(StateId.HitReact);
    }

    private readonly int[] _entered = new int[5];

    private void ChangeState(StateId id)
    {
        _entered[(int)id]++;
        _state.Exit();
        _state = _states[id];
        _state.Enter();
    }

    // ------------------------------------------------------------ visual (o mesmo boneco do jogador)

    private void DriveVisual(float dt)
    {
        if (_visual is null) return;
        // o hitbox acompanha o corpo (filho do CharacterBody3D) e o visual acompanha a posição física
        var ground = EscadaCasa.Altura(GlobalPosition.X, GlobalPosition.Z);
        _visual.Root.GlobalPosition = GlobalPosition + new Vector3(0, ground, 0);
        _visual.Root.Rotation = new Vector3(0, _yaw, 0);
        var flat = new Vector3(base.Velocity.X, 0, base.Velocity.Z);
        _speedShown = Mathf.Lerp(_speedShown, flat.Length(), 1f - Mathf.Exp(-12f * dt));
        // número de sequência: muda a cada golpe novo do combo para a animação recomeçar (cancelamento visível)
        var attackSeq = _state is AttackState at ? at.Seq : _lastAttackSeq;
        if (_state.Id != _lastVisualState || attackSeq != _lastAttackSeq) { _visualSeq++; _lastVisualState = _state.Id; _lastAttackSeq = attackSeq; }
        if (_visual is IPlayerVisual pv)
            pv.MoveDirLocal = flat.LengthSquared() > 1e-4f ? (_visual.Root.Basis.Inverse() * flat).Normalized() : Vector3.Back;
        _visual.Update(new VisualFrame
        {
            Speed = _state.Id is StateId.Move or StateId.Idle ? _speedShown : 0f,
            Action = _state.VisualAction,
            ActionT = _state.VisualT,
            ActionSeq = _visualSeq,
            Alive = true,
        }, dt);
    }

    public string Describe()
        => $"estado {_state.Id}{(_state is AttackState a ? $" (golpe {a.Step + 1}/3)" : "")} · buffer [{Buffer.Describe()}] · " +
           $"vel {new Vector3(base.Velocity.X, 0, base.Velocity.Z).Length():0.0} m/s{(Hurtbox.Invulnerable ? " · INVENCÍVEL" : "")} · " +
           $"golpes {_entered[(int)StateId.Attack]} esquivas {_entered[(int)StateId.Dodge]} reações {_entered[(int)StateId.HitReact]} · pos ({GlobalPosition.X:0.0}, {GlobalPosition.Z:0.0})";
}

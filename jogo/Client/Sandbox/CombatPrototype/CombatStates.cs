using Godot;

namespace CinzasDaMare.Client.Sandbox.CombatPrototype;

public enum StateId { Idle, Move, Attack, Dodge, HitReact }

/// <summary>
/// Um estado da máquina do lutador. Cada estado decide: velocidade desejada, o que mostrar no visual e para onde ir.
/// Estados não alocam por quadro e não conhecem a rede: só o <see cref="SandboxFighter"/> (dono) e o tempo.
/// </summary>
public abstract class CombatState
{
    protected readonly SandboxFighter F;
    protected double T;   // segundos dentro do estado (tempo do motor: o hitstop desacelera aqui também)
    protected CombatState(SandboxFighter fighter) => F = fighter;

    public abstract StateId Id { get; }
    public virtual void Enter() => T = 0;
    public virtual void Exit() { }
    /// <summary>Avança o estado; devolve o próximo (ou null para ficar).</summary>
    public abstract StateId? Tick(double dt);
    /// <summary>Ação mostrada no boneco ("windup:id", "active:id", "recover:id", "dodge", "").</summary>
    public virtual string VisualAction => "";
    public virtual float VisualT => 0f;
}

public sealed class IdleState : CombatState
{
    public IdleState(SandboxFighter f) : base(f) { }
    public override StateId Id => StateId.Idle;

    public override StateId? Tick(double dt)
    {
        T += dt;
        F.Steer(Vector3.Zero, dt);
        if (F.ConsumeBuffered("dodge")) return StateId.Dodge;
        if (F.ConsumeBuffered("attack")) return StateId.Attack;
        return F.WishDir.LengthSquared() > 0.01f ? StateId.Move : null;
    }
}

public sealed class MoveState : CombatState
{
    public MoveState(SandboxFighter f) : base(f) { }
    public override StateId Id => StateId.Move;

    public override StateId? Tick(double dt)
    {
        T += dt;
        var speed = (F.Sprint ? SandboxFighter.RunSpeed : SandboxFighter.WalkSpeed) * (float)F.Settings.MoveScale;
        F.Steer(F.WishDir * speed, dt, SandboxFighter.Accel * (float)F.Settings.AccelScale);
        if (F.WishDir.LengthSquared() > 0.01f) F.FaceTowards(F.WishDir, dt, 14f);
        if (F.ConsumeBuffered("dodge")) return StateId.Dodge;
        if (F.ConsumeBuffered("attack")) return StateId.Attack;
        return F.WishDir.LengthSquared() <= 0.01f ? StateId.Idle : null;
    }
}

/// <summary>
/// Combo de 3 golpes. Cada golpe: preparação → ativo (hitbox ligada) → recuperação.
/// Janelas de cancelamento:
///  - esquiva cancela a preparação e a recuperação (nunca a fase ativa — o golpe tem compromisso);
///  - o próximo golpe do combo entra a partir de <see cref="ComboStep.ChainFrom"/> da recuperação, se estiver no buffer.
/// </summary>
public sealed class AttackState : CombatState
{
    public readonly struct ComboStep
    {
        public readonly double Windup, Active, Recovery, ChainFrom, Lunge;
        public readonly float Damage, Knockback, HitstopWeight;
        public ComboStep(double windup, double active, double recovery, double chainFrom, double lunge, float damage, float knockback, float hitstop)
        { Windup = windup; Active = active; Recovery = recovery; ChainFrom = chainFrom; Lunge = lunge; Damage = damage; Knockback = knockback; HitstopWeight = hitstop; }
        public double Total => Windup + Active + Recovery;
    }

    public static readonly ComboStep[] Combo =
    {
        new(0.12, 0.10, 0.30, 0.05, 1.2, 10, 3.0f, 0.7f),
        new(0.10, 0.10, 0.32, 0.05, 1.4, 12, 3.5f, 0.85f),
        new(0.20, 0.14, 0.45, 0.20, 2.2, 22, 8.0f, 1.4f),   // finalizador: mais lento, empurra longe
    };

    private int _step;
    private int _seq;
    private bool _hitOpen;

    public AttackState(SandboxFighter f) : base(f) { }
    public override StateId Id => StateId.Attack;
    public int Step => _step;

    public override void Enter()
    {
        base.Enter();
        _step = 0;
        StartStep();
    }

    private void StartStep()
    {
        T = 0;
        _seq++;
        _hitOpen = false;
        F.SnapFacingToAim();
    }

    public override void Exit()
    {
        F.CloseHitbox();
        _hitOpen = false;
    }

    private ComboStep S => Combo[_step];

    public override StateId? Tick(double dt)
    {
        T += dt;
        var s = S;
        var inWindup = T < s.Windup;
        var inActive = !inWindup && T < s.Windup + s.Active;
        var inRecovery = !inWindup && !inActive;

        // avanço do golpe (o corpo vai junto com a lâmina) só na preparação e no ativo
        var lungeTime = s.Windup + s.Active;
        var forward = F.Forward;
        F.Steer(inRecovery ? Vector3.Zero : forward * (float)(s.Lunge / lungeTime), dt, inRecovery ? 18f : 30f);

        if (inActive && !_hitOpen) { _hitOpen = true; F.OpenHitbox(s.Damage, s.Knockback, s.HitstopWeight, "golpe" + (_step + 1)); }
        if (!inActive && _hitOpen) { _hitOpen = false; F.CloseHitbox(); }

        // cancelamentos
        if (!inActive && F.ConsumeBuffered("dodge")) return StateId.Dodge;
        if (inRecovery && T - s.Windup - s.Active >= s.ChainFrom && _step < Combo.Length - 1 && F.ConsumeBuffered("attack"))
        {
            _step++;
            StartStep();
            return null;
        }
        if (T >= s.Total) return F.WishDir.LengthSquared() > 0.01f ? StateId.Move : StateId.Idle;
        return null;
    }

    public override string VisualAction
    {
        get
        {
            var s = S;
            var id = F.AttackVisualId;
            return T < s.Windup ? "windup:" + id : T < s.Windup + s.Active ? "active:" + id : "recover:" + id;
        }
    }

    public override float VisualT
    {
        get
        {
            var s = S;
            if (T < s.Windup) return (float)(T / s.Windup);
            if (T < s.Windup + s.Active) return (float)((T - s.Windup) / s.Active);
            return (float)Mathf.Clamp((T - s.Windup - s.Active) / s.Recovery, 0, 1);
        }
    }

    public int Seq => _seq;
}

/// <summary>Esquiva: arranque na velocidade do slider, i-frames no meio, freada no fim; ataque pode cancelar o finzinho.</summary>
public sealed class DodgeState : CombatState
{
    public const double Duration = 0.30;
    public const double IFramesFrom = 0.02, IFramesTo = 0.24;
    private Vector3 _dir;
    private bool _saltou;
    private double Prep => F.Settings.DodgeWindupMs / 1000.0;

    public DodgeState(SandboxFighter f) : base(f) { }
    public override StateId Id => StateId.Dodge;

    public override void Enter()
    {
        base.Enter();
        _dir = F.WishDir.LengthSquared() > 0.01f ? F.WishDir.Normalized() : -F.Forward;   // parado: recua
        F.SetFacingInstant(_dir);
        _saltou = false;
        if (Prep <= 0) Saltar();
    }

    private void Saltar()
    {
        _saltou = true;
        F.Hurtbox.GrantIFrames((IFramesTo - IFramesFrom) * 1000.0 / Mathf.Max(0.05, Engine.TimeScale));
        F.Velocity = _dir * (float)F.Settings.DashVelocity;
    }

    public override StateId? Tick(double dt)
    {
        T += dt;
        // 12/11: preparação (agacha e freia, sem invulnerabilidade) antes do salto
        if (!_saltou)
        {
            F.Steer(Vector3.Zero, dt, 20f);
            if (T < Prep) return null;
            T = 0; Saltar();
        }
        // curva de velocidade: forte no começo, cai no fim (dá sensação de peso)
        var k = (float)Mathf.Clamp(1.0 - T / Duration, 0, 1);
        F.Steer(_dir * (float)F.Settings.DashVelocity * (0.25f + 0.75f * k * k), dt, 40f);
        if (T >= Duration * 0.75 && F.ConsumeBuffered("attack")) return StateId.Attack;
        if (T >= Duration) return F.WishDir.LengthSquared() > 0.01f ? StateId.Move : StateId.Idle;
        return null;
    }

    public override string VisualAction => "dodge";
    public override float VisualT => (float)Mathf.Clamp(T / Duration, 0, 1);
}

/// <summary>Reação a golpe: empurrão (knockback) que freia, sem controle por um instante; esquiva cancela depois de 60%.</summary>
public sealed class HitReactState : CombatState
{
    public const double Duration = 0.35;
    public HitReactState(SandboxFighter f) : base(f) { }
    public override StateId Id => StateId.HitReact;

    public override StateId? Tick(double dt)
    {
        T += dt;
        F.Steer(Vector3.Zero, dt, 8f);   // o knockback (já aplicado na Velocity) vai se apagando
        if (T >= Duration * 0.6 && F.ConsumeBuffered("dodge")) return StateId.Dodge;
        return T >= Duration ? StateId.Idle : null;
    }
}

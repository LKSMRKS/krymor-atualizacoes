using System;
using Godot;

namespace CinzasDaMare.Client.Controllers;

/// <summary>
/// Controlador OFICIAL do jogador (12/11): o mesmo código de sempre do Main (teclado → intenções ao servidor), só que
/// atrás do contrato <see cref="IPlayerController"/>. Não muda nada no comportamento: o Main continua dono da lógica e
/// este nó apenas a liga/desliga e expõe o estado.
///
/// Pausar = <c>SetProcess(false)</c> + <c>SetPhysicsProcess(false)</c> e manda "parar" ao servidor uma vez (para o boneco
/// oficial não continuar andando sozinho enquanto o protótipo controla). O nó nunca é destruído.
/// </summary>
public sealed partial class NetworkPlayerController : Node, IPlayerController
{
    private readonly Action<long> _continuousInput;
    private readonly Func<InputEvent, bool> _input;
    private readonly Func<PlayerHandoff> _snapshot;
    private readonly Action<(double X, double Y), bool> _move;
    private readonly Action<string> _attack;
    private readonly Action<(double X, double Y)> _dodge;
    private PlayerHandoff _last;
    private bool _active = true;   // nasce ligado: é o controlador do jogo

    public NetworkPlayerController(Action<long> continuousInput, Func<InputEvent, bool> input, Func<PlayerHandoff> snapshot,
        Action<(double X, double Y), bool> move, Action<string> attack, Action<(double X, double Y)> dodge)
    {
        Name = "ControladorOficial";
        _continuousInput = continuousInput;
        _input = input;
        _snapshot = snapshot;
        _move = move;
        _attack = attack;
        _dodge = dodge;
    }

    public string Id => "oficial";
    public bool IsActive => _active;
    public Vector3 Position => _snapshot().Position;
    public Vector3 Velocity => _snapshot().Velocity;
    public float FacingYaw => _snapshot().FacingYaw;

    public void Activate(in PlayerHandoff handoff)
    {
        // o servidor é dono da posição: o boneco volta ao que o servidor diz (instantâneo). O handoff só é guardado.
        _last = handoff;
        _active = true;
        SetProcess(true);
        SetPhysicsProcess(true);
    }

    public PlayerHandoff Deactivate()
    {
        _last = _snapshot();
        _move((0, 0), false);   // para no servidor: nada de "andar sozinho" enquanto pausado
        _active = false;
        SetProcess(false);
        SetPhysicsProcess(false);
        return _last;
    }

    /// <summary>Chamado pelo Main no mesmo ponto do quadro de antes (ordem preservada).</summary>
    public void ProcessFrame(double delta)
    {
        if (IsActive) _continuousInput((long)Time.GetTicksMsec());
    }

    public void PhysicsFrame(double delta) { }   // a física do jogador oficial está no servidor

    public bool HandleInput(InputEvent e) => IsActive && _input(e);

    public void RequestMove(Vector2 direction, bool sprint) => _move((direction.X, direction.Y), sprint);
    public void RequestAttack(string slot) => _attack(slot);
    public void RequestDodge(Vector2 direction) => _dodge((direction.X, direction.Y));
}

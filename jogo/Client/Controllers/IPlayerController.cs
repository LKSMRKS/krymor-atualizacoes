using CinzasDaMare.Client.Visuals;
using Godot;

namespace CinzasDaMare.Client.Controllers;

/// <summary>
/// Contrato de quem controla o personagem do jogador neste cliente (12/11: base para o Laboratório/Sandbox e para a
/// promoção de protótipos ao jogo oficial).
///
/// Hoje existem dois:
///  - <see cref="NetworkPlayerController"/> (oficial): lê o teclado/mouse e manda INTENÇÕES ao servidor
///    (MoveIntent, CastIntent, DodgeIntent). Quem decide posição, dano e tempo é o servidor (autoritativo).
///  - Protótipos do Sandbox (<c>res://Sandbox/</c>): rodam só neste cliente, com física local, para calibrar a sensação.
///
/// Quem troca um pelo outro é o <c>PlayerSandboxManager</c>: <see cref="Deactivate"/> devolve o estado do personagem
/// (<see cref="PlayerHandoff"/>) e o próximo controlador começa dali em <see cref="Activate"/>.
/// </summary>
public interface IPlayerController
{
    /// <summary>Identificador estável (aparece no log e no Laboratório).</summary>
    string Id { get; }

    /// <summary>Ligado = recebe quadro e entrada; desligado = pausado, mas o nó e o estado continuam vivos.</summary>
    bool IsActive { get; }

    // ---- estado físico (o que se passa de um controlador ao outro)
    /// <summary>Posição dos pés no mundo do Godot (X/Z = mapa; Y = altura do chão).</summary>
    Vector3 Position { get; }
    /// <summary>Velocidade atual em m/s (o "momentum" que passa adiante na troca).</summary>
    Vector3 Velocity { get; }
    /// <summary>Para onde o personagem olha (radianos, mesma convenção da câmera: 0 = -Z).</summary>
    float FacingYaw { get; }

    // ---- ciclo de vida
    void Activate(in PlayerHandoff handoff);
    /// <summary>Pausa o controlador e devolve o estado do personagem neste instante.</summary>
    PlayerHandoff Deactivate();

    // ---- quadro e entrada (chamados por quem hospeda, só enquanto ativo)
    void ProcessFrame(double delta);
    void PhysicsFrame(double delta);
    /// <summary>true = consumiu o evento (o resto do jogo não o vê).</summary>
    bool HandleInput(InputEvent e);

    // ---- contratos de movimentação e combate (comandos de alto nível; quem implementa decide como executar)
    /// <summary>Direção no plano do mapa (x, y do servidor), já relativa à câmera. (0,0) = parar.</summary>
    void RequestMove(Vector2 direction, bool sprint);
    /// <summary>"basic", "skill1", "skill2", "weapon" ou "key:Q"...</summary>
    void RequestAttack(string slot);
    void RequestDodge(Vector2 direction);
}

/// <summary>Estado do personagem entregue de um controlador ao outro na troca a quente.</summary>
public readonly record struct PlayerHandoff(
    Vector3 Position,
    Vector3 Velocity,
    float FacingYaw,
    IEntityVisual? Visual,
    ThirdPersonCamera? Camera,
    string? BasicSkillId);

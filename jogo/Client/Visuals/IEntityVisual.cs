using Godot;

namespace CinzasDaMare.Client.Visuals;

/// <summary>
/// O que o visual precisa saber a cada quadro. Vem do snapshot do servidor (ação, progresso, vida) e da
/// suavização do cliente (velocidade). Nenhuma regra de jogo depende do visual.
/// </summary>
public struct VisualFrame
{
    public float Speed;          // m/s no chão (para o ciclo de passos)
    public string Action;        // "windup:id", "active:id", "recover:id", "dodge", "chase", "flee", "" ...
    public float ActionT;        // 0..1 dentro da fase
    public int ActionSeq;
    public bool Alive;
    public bool Guarding;
    public float WindupGlow;     // 0..1: brilho de preparação (inimigos)
}

/// <summary>
/// Componente visual substituível por entidade. Hoje: bonecos articulados de formas simples (HumanoidVisual,
/// CrabVisual). Depois: um visual que carregue .glb e toque animações, implementando a mesma interface,
/// sem mexer em combate, inventário ou rede. A raiz fica na posição da entidade (pés no chão, frente = +Z).
/// </summary>
public interface IEntityVisual
{
    Node3D Root { get; }
    /// <summary>Altura aproximada (para barra de vida, números e mira).</summary>
    float Height { get; }
    void Update(in VisualFrame frame, float delta);
    /// <summary>Troca as peças visíveis (IDs de modelo vindos do catálogo; null = nada).</summary>
    void SetGear(string? weapon, string? offhand, string? body);
    /// <summary>Clarão + recuo rápido ao levar golpe.</summary>
    void Hit(Vector3 fromDirection);
    void Dispose();
}

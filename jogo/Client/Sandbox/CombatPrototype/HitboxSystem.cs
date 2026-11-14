using System;
using System.Collections.Generic;
using Godot;

namespace CinzasDaMare.Client.Sandbox.CombatPrototype;

/// <summary>
/// Camadas de física só do laboratório (bits 10–13, que o jogo oficial não usa: paredes = 1, chão = 2).
/// Hitbox de um time procura a Hurtbox do outro time — "camadas dinâmicas": quem troca de time só troca a máscara.
/// </summary>
public static class SandboxLayers
{
    public const uint PlayerHurt = 1u << 9;    // camada 10
    public const uint EnemyHurt = 1u << 10;    // camada 11
    public const uint PlayerHit = 1u << 11;    // camada 12
    public const uint EnemyHit = 1u << 12;     // camada 13

    public static uint HurtLayer(Team t) => t == Team.Player ? PlayerHurt : EnemyHurt;
    public static uint HitLayer(Team t) => t == Team.Player ? PlayerHit : EnemyHit;
    /// <summary>Hitbox do time T acerta a hurtbox do time oposto.</summary>
    public static uint HitMask(Team t) => t == Team.Player ? EnemyHurt : PlayerHurt;
}

public enum Team { Player, Enemy }

/// <summary>O que um golpe carrega (valor, sem alocar).</summary>
public readonly record struct HitInfo(Node3D Attacker, Vector3 Origin, float Damage, float Knockback, float HitstopWeight, string AttackId);

/// <summary>
/// Hurtbox: a área do corpo que pode ser acertada. Emite <see cref="Hit"/>; com i-frames ativos, ignora o golpe.
/// </summary>
public sealed partial class Hurtbox : Area3D
{
    public event Action<HitInfo>? Hit;
    public Team Team { get; private set; }
    /// <summary>Invencível até este instante (ms reais).</summary>
    public ulong InvulnerableUntil { get; set; }
    public bool Invulnerable => Time.GetTicksMsec() < InvulnerableUntil;

    public Hurtbox(Team team, float radius, float height)
    {
        Name = "Hurtbox";
        Monitoring = false;     // quem procura é a hitbox; a hurtbox só é encontrada
        Monitorable = true;
        SetTeam(team);
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = radius, Height = height }, Position = new Vector3(0, height / 2f, 0) });
    }

    public void SetTeam(Team team)
    {
        Team = team;
        CollisionLayer = SandboxLayers.HurtLayer(team);
        CollisionMask = 0;
    }

    public void GrantIFrames(double ms) => InvulnerableUntil = Math.Max(InvulnerableUntil, Time.GetTicksMsec() + (ulong)ms);

    /// <summary>true = o golpe entrou.</summary>
    public bool Receive(in HitInfo hit)
    {
        if (Invulnerable) return false;
        Hit?.Invoke(hit);
        return true;
    }
}

/// <summary>
/// Hitbox: a área do golpe. Só acerta na fase ativa do ataque (<see cref="Open"/> / <see cref="Close"/>).
/// A área fica sempre "monitorando" (barato: máscara só das hurtboxes do outro time) e o portão é <c>_open</c> — assim
/// abrir/fechar vale no MESMO passo de física (com <c>SetDeferred(Monitoring)</c> um golpe curto podia abrir e fechar
/// antes de a física enxergar). Cada abertura acerta cada alvo uma vez só (lista reaproveitada, sem alocar).
/// </summary>
public sealed partial class Hitbox : Area3D
{
    public event Action<Hurtbox, HitInfo>? Landed;
    public Team Team { get; private set; }
    private readonly HashSet<Hurtbox> _already = new();
    private HitInfo _info;
    private bool _open;

    public Hitbox(Team team, Shape3D shape, Vector3 offset)
    {
        Name = "Hitbox";
        Monitorable = false;
        Monitoring = true;
        SetTeam(team);
        AddChild(new CollisionShape3D { Shape = shape, Position = offset });
        AreaEntered += OnAreaEntered;
    }

    public void SetTeam(Team team)
    {
        Team = team;
        CollisionLayer = SandboxLayers.HitLayer(team);
        CollisionMask = SandboxLayers.HitMask(team);
    }

    public void Open(in HitInfo info)
    {
        _info = info;
        _already.Clear();
        _open = true;
        SweepOverlaps();   // quem já está dentro leva o golpe neste passo
    }

    public void Close()
    {
        _open = false;
    }

    /// <summary>Área que já estava sobreposta quando a hitbox abriu também conta (o sinal só avisa entradas novas).</summary>
    public void SweepOverlaps()
    {
        if (!_open || !IsInsideTree()) return;
        foreach (var a in GetOverlappingAreas()) OnAreaEntered(a);
    }

    private void OnAreaEntered(Area3D area)
    {
        if (!_open || area is not Hurtbox hurt || hurt.Team == Team || !_already.Add(hurt)) return;
        var info = _info with { Origin = GlobalPosition };
        if (hurt.Receive(info)) Landed?.Invoke(hurt, info);
    }
}

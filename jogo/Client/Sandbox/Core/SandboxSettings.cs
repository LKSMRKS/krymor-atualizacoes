using System;

namespace CinzasDaMare.Client.Sandbox.Core;

/// <summary>
/// Valores calibráveis ao vivo pela aba "Laboratório (Sandbox)" do F1. Uma instância só, lida a cada quadro pelos
/// protótipos (sem alocação, sem evento por valor). Ao aprovar, os números vencedores viram constantes/dados oficiais.
/// </summary>
public sealed class SandboxSettings
{
    /// <summary>Por quanto tempo um comando apertado "espera" a ação atual acabar (ms).</summary>
    public double InputBufferMs { get; set; } = 180;
    /// <summary>Velocidade da esquiva (m/s) — a distância sai de velocidade × duração.</summary>
    public double DashVelocity { get; set; } = 14;
    /// <summary>Escala de tempo durante o hitstop (0,05 = quase congela; 1 = sem hitstop).</summary>
    public double HitstopScale { get; set; } = 0.08;
    /// <summary>Duração do hitstop em ms reais.</summary>
    public double HitstopMs { get; set; } = 70;
    /// <summary>Bonecos de treino também atacam (para testar i-frames e reação a golpe).</summary>
    public bool DummiesAttack { get; set; } = true;
    /// <summary>12/11 ("movimento AAA: mais lento, esquiva com preparação"): escala da velocidade de andar/correr.</summary>
    public double MoveScale { get; set; } = 1.0;
    /// <summary>Escala da aceleração (menor = arranca e para mais devagar, sensação de peso).</summary>
    public double AccelScale { get; set; } = 1.0;
    /// <summary>Preparação da esquiva (ms): agacha antes de pular; a invulnerabilidade só começa depois.</summary>
    public double DodgeWindupMs { get; set; } = 0;
    /// <summary>12/11 ("testar o v2 contra os mobs que já existem"): espécie usada nos bonecos (vazio = boneco de madeira).
    /// Com espécie, o boneco usa o visual do monstro, persegue o herói, telegrafa e ataca (movimento v2 de mob).</summary>
    public string DummySpecies { get; set; } = "";
    public string DummyName { get; set; } = "";
    public string DummyVisual { get; set; } = "";
    public bool DummyBoss { get; set; }

    public event Action? Changed;
    public void NotifyChanged() => Changed?.Invoke();
}

/// <summary>O que a aba do F1 enxerga do laboratório (o painel não conhece as classes dos protótipos).</summary>
public interface ISandboxLab
{
    SandboxSettings Settings { get; }
    bool CombatV2 { get; set; }
    bool EnvironmentV2 { get; set; }
    /// <summary>Uma linha de estado para mostrar no painel (estado da máquina, buffer, alvo...).</summary>
    string Status { get; }
    /// <summary>Recria os bonecos (troca de espécie).</summary>
    void RespawnDummies();
}

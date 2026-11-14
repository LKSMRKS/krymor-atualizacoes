namespace CinzasDaMare.Server.Net;

/// <summary>
/// Fôlego (pedido do usuário, 09/11): correr, esquivar e atacar gastam (o golpe básico em ritmo normal quase se paga:
/// farmar no clique não cansa; habilidades, esquivas e corrida é que esvaziam); a barra volta sozinha depois de um instante
/// parado de gastar. Esvaziou = "sem fôlego": não corre até recuperar um terço. Separado do recurso da classe
/// (Fúria/Mana/Éter/Foco). Só no mundo vivo — os testes antigos de arena/mapa seguem sem esse custo.
/// </summary>
public sealed partial class GameServer
{
    internal const double StaminaMax = 100;
    internal const double StaminaSprintPerSec = 13;
    internal const double StaminaDodge = 24;
    internal const double StaminaBasic = 5;
    internal const double StaminaSkill = 12;
    internal const double StaminaRegenPerSec = 34;
    internal const long StaminaDelayMs = 450;
    internal const double StaminaRecoverToSprint = 33;

    private sealed partial class Session
    {
        public double Stamina = StaminaMax;
        public long StaminaRegenAtMs;
        public bool Winded;                       // esvaziou: só volta a correr acima de StaminaRecoverToSprint
    }

    private bool StaminaOn => LivelyMovement;

    /// <summary>
    /// 12/11 ("existe algum atributo que sobe estamina? acho muito curta"): Vitalidade (vigor) dá fôlego. Cada ponto acima do
    /// inicial deixa correr, esquivar e atacar 1,2% mais baratos e a barra volta 0,6% mais rápido (até +80% / +40%).
    /// </summary>
    private double Folego(Session s) => 1 + Math.Min(0.8, Math.Max(0, (s.Combat?.Vigor ?? 0) - (s.Combat?.Class.StartingVigor ?? 0)) * 0.012);

    /// <summary>Gasta fôlego; devolve false se não havia nada (a ação não acontece). Pode ficar no zero, não abaixo.</summary>
    private bool SpendStamina(Session s, double amount, long now)
    {
        if (!StaminaOn) return true;
        if (s.Stamina <= 0.5) return false;
        s.Stamina = Math.Max(0, s.Stamina - amount / Folego(s));
        s.StaminaRegenAtMs = now + StaminaDelayMs;
        if (s.Stamina <= 0) s.Winded = true;
        return true;
    }

    /// <summary>Por tique: corrida consome, senão recupera (mais devagar com a guarda erguida).</summary>
    private void TickStamina(Session s, long now, double dt, bool sprinting)
    {
        if (!StaminaOn) return;
        if (sprinting)
        {
            s.Stamina = Math.Max(0, s.Stamina - StaminaSprintPerSec * dt / Folego(s));
            s.StaminaRegenAtMs = now + StaminaDelayMs;
            if (s.Stamina <= 0) s.Winded = true;
            return;
        }
        if (now < s.StaminaRegenAtMs || now < s.DodgeUntilMs) return;
        var rate = StaminaRegenPerSec * (s.Combat!.IsGuarding(now) ? 0.5 : 1.0) * (1 + s.Gear.StaminaRegenPct) * (1 + (Folego(s) - 1) / 2);   // 11/11: fôlego da calça; 12/11: vitalidade
        s.Stamina = Math.Min(StaminaMax, s.Stamina + rate * dt);
        if (s.Winded && s.Stamina >= StaminaRecoverToSprint) s.Winded = false;
    }

    private bool CanSprint(Session s) => !StaminaOn || (!s.Winded && s.Stamina > 0.5);
}
